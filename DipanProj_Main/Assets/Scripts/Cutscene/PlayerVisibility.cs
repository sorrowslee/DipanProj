using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Cutscene
{
    /// <summary>
    /// 「演出期間把主角藏起來」的開關（劇情演出的 <c>hidePlayer</c> 勾選用）。
    ///
    /// ⚠ **不能直接 <c>player.SetActive(false)</c>**——踩過三個坑，這支就是為了一次擋掉：
    ///   1. **影子會留在原地**：<see cref="BlobShadow"/> 的影子是獨立 GameObject（刻意不做子物件，
    ///      避免被角色翻轉/縮放二次影響），停用玩家連 BlobShadow 的 LateUpdate 也停 → 影子定格在原地。
    ///   2. **暗場景的光圈會留在原地**：AtmosphereController 每幀以玩家為心算裝備的發光半徑，
    ///      玩家隱形了光圈照樣亮著 → 空地上浮一圈光。
    ///   3. **碰撞還在擋路**：劇情演員的 A* 走位會被一個看不見的玩家撞開。
    ///
    /// 所以做法是「逐項關掉」而不是停用整個物件：SpriteRenderer（含子物件）＋ BlobShadow ＋ Collider2D，
    /// 再由 <see cref="IsHidden"/> 讓 AtmosphereController 跳過玩家光源。
    /// 位置在隱藏時記下來，<see cref="Show"/> 預設會放回去（＝「演完回歸原位」）。
    ///
    /// 見 readme/CUTSCENE_DIRECTOR.md。
    /// </summary>
    public static class PlayerVisibility
    {
        /// <summary>主角目前是否被劇情藏起來（AtmosphereController 用它跳過玩家光源）。</summary>
        public static bool IsHidden { get; private set; }

        static GameObject _player;
        static Vector3 _pos;
        static readonly List<SpriteRenderer> _srs = new List<SpriteRenderer>();
        static readonly List<Collider2D> _cols = new List<Collider2D>();
        static BlobShadow _shadow;

        /// <summary>
        /// 進入 Play 模式時把 static 歸零（本專案已關 Domain Reload，見 <c>PlayModeStaticReset</c>）。
        /// ⚠ **不加這支的症狀非常難查**：只要有一次 Play 是在演出中途按停止（或演出被換圖打斷），
        /// <see cref="IsHidden"/> 會殘留成 true → 下一次 Play 的 <see cref="Hide"/> 直接 return
        /// ⇒ **主角從此再也藏不起來，而且要重開 Unity 才會好**。看起來像「隱藏主角這個功能壞了」。
        /// </summary>
        public static void ResetForPlayMode()
        {
            IsHidden = false;
            _player = null; _shadow = null;
            _srs.Clear(); _cols.Clear();
        }

        /// <summary>藏起主角（記下當前位置）。已經藏著就不重複做。</summary>
        public static void Hide()
        {
            // 保險：狀態說「藏著」但目標已經不在了（換場景／上一輪 Play 殘留）＝狀態壞了，重來一次。
            if (IsHidden && _player == null) ResetForPlayMode();
            if (IsHidden) return;
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) { Debug.LogWarning("[Cutscene] 隱藏主角：找不到玩家（Player tag），略過。"); return; }

            _player = p;
            _pos = p.transform.position;

            _srs.Clear();
            p.GetComponentsInChildren(true, _srs);
            foreach (var sr in _srs) if (sr != null) sr.enabled = false;

            _cols.Clear();
            p.GetComponentsInChildren(true, _cols);
            foreach (var c in _cols) if (c != null) c.enabled = false;   // 別擋住演員走位

            _shadow = p.GetComponent<BlobShadow>();
            if (_shadow != null) _shadow.SetVisible(false);              // 影子是獨立物件，要另外關

            var rb = p.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;

            IsHidden = true;
        }

        /// <summary>讓主角現身。<paramref name="restorePosition"/>＝true 時放回隱藏當下的位置（預設）。</summary>
        public static void Show(bool restorePosition = true)
        {
            if (!IsHidden) return;
            IsHidden = false;

            if (_player != null)
            {
                // 真的有位移才搬（多數情況主角整段都沒動過，搬了等於白做還多一次「非自主位移」）。
                if (restorePosition && ((Vector2)_player.transform.position - (Vector2)_pos).sqrMagnitude > 0.0001f)
                {
                    _player.transform.position = _pos;
                    // ⚠ 用程式把玩家搬過去 ≠ 玩家自己走過去：一定要解除位置型觸發的武裝，
                    //   否則落點正好在傳送點上時（進圖落點本來就是傳送點的錨點）會當場被送回上一張圖。
                    //   同 readme/PROBLEMS.md **B11**（擊退不算踩到）與 **B14**。
                    if (MapManager.Instance != null) MapManager.Instance.DisarmPositionTriggers();
                }
                var rb = _player.GetComponent<Rigidbody2D>();
                if (rb != null) rb.velocity = Vector2.zero;
            }

            foreach (var sr in _srs) if (sr != null) sr.enabled = true;
            foreach (var c in _cols) if (c != null) c.enabled = true;
            if (_shadow != null) _shadow.SetVisible(true);

            _srs.Clear(); _cols.Clear(); _shadow = null; _player = null;
        }
    }
}
