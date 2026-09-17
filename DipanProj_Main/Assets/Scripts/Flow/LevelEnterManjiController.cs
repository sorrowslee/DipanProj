using System.Collections;
using UnityEngine;
using Dipan.UI;

namespace Dipan.Flow
{
    /// <summary>
    /// 「卍字進場」世界座標特效（全程式、零 prefab）——<see cref="LevelExitManjiController"/>（過關/死亡離場）的**倒放**，
    /// 兩者首尾呼應：離場是「金 → 紫、吞入玩家、飛上天」，進場就是「從天而降、紫 → 金、把玩家吐出來」。
    ///   ① 卍字從畫面上方旋轉落下（紫色、較小、淡入），落地時減速停在玩家位置。
    ///   ② 卍字放大、紫 → 金，同時把主角從幾乎不見放大回原尺寸（＝被吐出來）。
    ///   ③ 卍字（大、金）原地旋轉淡出，畫面只剩站在場景裡的主角。
    ///
    /// 播完由呼叫端（<c>MapManager.FireEnterTriggersRoutine</c>）接著跳場景說明（SceneTip），名字淡出後才正式開打。
    ///
    /// **自己負責暫停＋鎖操作**（同 EyeOpenController 的做法）：`UIManager.SetExternalHold` 的
    /// **具名多載**（owner = <see cref="HoldOwner"/>，見 readme/PROBLEMS.md D13）——不用舊的兩參數多載，
    /// 免得把別人掛的 hold 一起解掉。整段時間軸一律 `unscaledDeltaTime`，所以暫停中照樣播。
    ///
    /// 主角的隱藏走 <see cref="Dipan.Cutscene.PlayerVisibility"/>（劇情 hidePlayer 用的同一支）：
    /// 它會連影子、碰撞、暗場景光圈一起關——**別自己 SetActive(false)**，那三個坑在那支的註解裡。
    ///
    /// 卍字圖：與離場/開場墜落同一張 `Resources/InitialStory/Manji`（載不到就用離場那支的程序生成備援）。
    /// </summary>
    public class LevelEnterManjiController : MonoBehaviour
    {
        // ── 節奏（要調表演改這裡）──
        // 刻意比離場短：離場後面接的是結算畫面，進場後面還要接場景說明（約 1.73 秒）＋劇情，
        // 整段暫停太久玩家會煩（同 SCENE_TIP 把停留壓短的理由）。對照離場：0.95 / 1.25 / 0.45。
        const float DescendTime = 0.85f;  // 從天而降（對應離場的「飛上天」）
        const float UnwrapTime  = 0.95f;  // 放大＋把主角吐出來（對應離場的「縮小吞入」）
        const float FadeOutTime = 0.40f;  // 原地淡出（對應離場的「淡入」）

        // ── 外觀（與離場對稱，改了要兩邊一起看）──
        const float RotateSpeed  = 210f;  // 旋轉速度（度/秒）。刻意與離場**同方向**——反轉會有廉價的倒帶感
        const float BigSizeMul   = 6.0f;  // 展開後的卍字大小 = 玩家高度 × 此（＝離場的 StartSizeMul）
        const float SmallSizeMul = 1.15f; // 落地瞬間的卍字大小 = 玩家高度 × 此（＝離場的 EndSizeMul）
        const float DescendDistMul = 7.5f;// 起始高度 = 玩家高度 × 此（略小於離場的 9.0：讓卍字更快進入視野）
        const float PlayerTinyScale = 0.02f;  // 主角「還在卍字裡」時的縮放（＝離場吞入的終點）
        const float MaxAlpha = 0.95f;
        // 排序同離場：要壓在世界特效（VfxManager 預設 22000）與角色之上。16-bit 安全（<32767）。
        const int SortingOrder = 25000;

        // UIManager.SetExternalHold 的具名持有者（見 PROBLEMS D13）。
        const string HoldOwner = "LevelEnterManji";

        static readonly Color Gold   = new Color(1f, 0.80f, 0.42f, 1f);   // 神聖（吐出玩家時）
        static readonly Color Purple = new Color(0.56f, 0.30f, 0.78f, 1f); // 墮落（從天而降時）

        /// <summary>目前是否有卍字進場特效在播（給等待鏈輪詢用；本特效同時只會有一份）。</summary>
        public static bool IsPlaying { get; private set; }

        /// <summary>
        /// 時間軸用的每幀增量：unscaled（整段是暫停遊戲播的），但**夾上限 0.05 秒**。
        /// 理由：本特效正好接在「讀取頁關閉」後的第一幀——那一幀常常是整場最長的一幀（剛載完圖、GC/貼圖上傳），
        /// 不夾的話 `unscaledDeltaTime` 可能一次吃掉 0.3~0.5 秒，卍字會「瞬移半段」才開始動。
        /// </summary>
        static float Dt => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        bool _holding;          // 本實例是否掛著 UIManager 的 external hold（OnDestroy 保險解除用）
        bool _finished;         // 時間軸已正常跑完（OnDestroy 就不必再動 IsPlaying，免得誤踩下一輪）
        Transform _player;      // 吞著的玩家（被打斷時要還原縮放）
        Vector3 _playerOrigScale = Vector3.one;
        BlobShadow _shadow;     // 吐出期間先關著的影子（結束才開）

        /// <summary>
        /// 進入 Play 模式時把 static 歸零（本專案已關 Domain Reload，見 <c>PlayModeStaticReset</c>）。
        /// 不加的話：只要有一次 Play 是在特效中途按停止，<see cref="IsPlaying"/> 會殘留成 true
        /// ⇒ 下一次進圖的等待鏈永遠卡在「等卍字播完」，症狀是**進圖後遊戲再也不開始**。
        /// </summary>
        public static void ResetForPlayMode() => IsPlaying = false;

        /// <summary>播放進場特效；播完呼叫 onDone。player 可為 null（就只播卍字、不吐人）。</summary>
        public static void Play(Transform player, System.Action onDone)
        {
            var go = new GameObject("[LevelEnterManji]");
            var c = go.AddComponent<LevelEnterManjiController>();
            c.StartCoroutine(c.Run(player, onDone));
        }

        IEnumerator Run(Transform player, System.Action onDone)
        {
            IsPlaying = true;
            _player = player;

            // 暫停遊戲＋鎖住操作（具名 hold，不會誤解別人的）。整段用 unscaled 時間，所以照樣演。
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetExternalHold(HoldOwner, true, true);
                _holding = true;
            }

            // 玩家尺寸/落點（此時玩家已被 PlaceAndSetup 放到出生點/傳送落點上）。
            SpriteRenderer psr = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            float playerH = (psr != null && psr.bounds.size.y > 0.01f) ? psr.bounds.size.y : 1.95f;
            Vector3 center = player != null ? player.position : Vector3.zero;

            // 主角先整個藏起來（連影子、碰撞、暗場景光圈）——卍字還在天上時場上不該有他。
            if (player != null)
            {
                _playerOrigScale = player.localScale;
                Dipan.Cutscene.PlayerVisibility.Hide();
                player.localScale = _playerOrigScale * PlayerTinyScale;   // 先縮好，第 ② 段才放得回來
            }

            // 卍字物件
            var manjiGo = new GameObject("Manji");
            manjiGo.transform.position = center;
            var sr = manjiGo.AddComponent<SpriteRenderer>();
            sr.sprite = LevelExitManjiController.ManjiSprite;   // 與離場共用同一張圖/同一份備援
            sr.sortingOrder = SortingOrder;

            float spriteWorld = sr.sprite.bounds.size.y;   // scale=1 時的世界高（512px/100ppu≈5.12）
            if (spriteWorld < 0.01f) spriteWorld = 1f;
            float bigScale   = (playerH * BigSizeMul) / spriteWorld;
            float smallScale = (playerH * SmallSizeMul) / spriteWorld;
            float dist = playerH * DescendDistMul;

            float angle = 0f;

            // ── 階段 ①：從天而降（紫、小、淡入；起步快、落地慢）──
            // 位移曲線 (1-k)² 就是離場「加速上升 k²」的倒放：t=0 在最高點、t=T 剛好停在玩家腳下。
            for (float t = 0f; t < DescendTime; t += Dt)
            {
                float k = t / DescendTime;
                float h = (1f - k) * (1f - k);
                angle += RotateSpeed * 1.4f * Dt;   // 下降中轉快一點（同離場上升段）
                Vector3 pos = center + Vector3.up * (dist * h);
                float scale = Mathf.Lerp(smallScale * 0.5f, smallScale, k);
                SetManji(sr, manjiGo.transform, pos, angle, scale, Purple, MaxAlpha * Mathf.Clamp01(k * 2f));
                yield return null;
            }

            // ── 階段 ②：放大 + 把主角吐出來（紫→金）──
            // 這一刻主角才現身：renderer 開回來，但**影子先按著**——影子是獨立物件、不會跟著縮放，
            // 現在放出來會看到「小主角配一團原尺寸的影子」。等吐完再開。
            if (player != null)
            {
                Dipan.Cutscene.PlayerVisibility.Show(false);   // false = 不搬回位置（他整段都沒動過）
                _shadow = player.GetComponent<BlobShadow>();
                if (_shadow != null) _shadow.SetVisible(false);
            }
            for (float t = 0f; t < UnwrapTime; t += Dt)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / UnwrapTime);
                angle += RotateSpeed * Dt;
                float scale = Mathf.Lerp(smallScale, bigScale, k);
                Color col = Color.Lerp(Purple, Gold, k);
                SetManji(sr, manjiGo.transform, center, angle, scale, col, MaxAlpha);
                if (player != null)
                    player.localScale = Vector3.Lerp(_playerOrigScale * PlayerTinyScale, _playerOrigScale, k);
                yield return null;
            }
            RestorePlayer();

            // ── 階段 ③：原地淡出（大、金）──
            for (float t = 0f; t < FadeOutTime; t += Dt)
            {
                float k = t / FadeOutTime;
                angle += RotateSpeed * Dt;
                SetManji(sr, manjiGo.transform, center, angle, bigScale, Gold, MaxAlpha * (1f - k));
                yield return null;
            }

            Destroy(manjiGo);
            ReleaseHold();
            _finished = true;
            IsPlaying = false;
            onDone?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>把主角還原成正常狀態（縮放、影子）。重複呼叫安全。</summary>
        void RestorePlayer()
        {
            if (_player != null) _player.localScale = _playerOrigScale;
            if (Dipan.Cutscene.PlayerVisibility.IsHidden) Dipan.Cutscene.PlayerVisibility.Show(false);
            if (_shadow != null) { _shadow.SetVisible(true); _shadow = null; }
            _player = null;
        }

        void ReleaseHold()
        {
            if (!_holding) return;
            _holding = false;
            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(HoldOwner, false, false);
        }

        /// <summary>保險：被場景切換/中途停止 Play 打斷時，也要把暫停解掉、主角還原，否則遊戲會永遠凍住＋主角消失。</summary>
        void OnDestroy()
        {
            RestorePlayer();
            ReleaseHold();
            if (!_finished) IsPlaying = false;   // 正常播完的那一路已經清過了，別覆蓋掉可能已開始的下一輪
        }

        static void SetManji(SpriteRenderer sr, Transform tr, Vector3 pos, float angle, float scale, Color col, float alpha)
        {
            tr.position = pos;
            tr.localEulerAngles = new Vector3(0f, 0f, angle);
            tr.localScale = Vector3.one * scale;
            var c = col; c.a = Mathf.Clamp01(alpha);
            sr.color = c;
        }
    }
}
