using System;
using System.Collections;
using UnityEngine;
using Dipan.Flow;
using Dipan.UI;

namespace Dipan.Gacha
{
    /// <summary>
    /// 血統變身演出：倒下 → 天雷擊中 → 煙霧與電弧籠罩 →（煙裡換外型）→ 爬起。
    ///
    /// 時間軸（常數在 <see cref="BloodlineTransformFxRunner"/> 檔頭，要調節奏改那裡）：
    /// <code>
    /// 0.00s  關所有面板 → 鎖操作 ＋ 暫停遊戲（timeScale=0，之後全程 unscaled 計時）
    /// 0.00s  玩家 dead 正向播放，倒下 ─────────── 約 2.1s
    /// 2.08s  趴地定格；閃電柱從畫面外生成
    /// 2.20s  【擊中】螢幕震動 + 白閃
    /// 2.25s  煙塵爆開（放大蓋住玩家）+ 環繞電弧亮起
    /// 2.58s  煙霧最濃 →【換成新血統，仍趴著】
    /// 2.95s  煙塵播完消散 → 露出新外貌趴在地上
    /// 2.95s  電弧殘留繼續繞 ──────────────── 0.6s
    /// 3.55s  電弧淡出
    /// 3.75s  倒播 dead，爬起來 ─────────────── 約 2.1s
    /// 5.83s  演出結束 → 交棒給 BloodlineIntroPanel（立繪揭示）
    /// </code>
    ///
    /// <b>整段演出在遊戲暫停下播放</b>（<c>timeScale = 0</c>）。這樣怪物不會在玩家被鎖住不能閃避的
    /// 六秒裡把他打死，也和接在後面的 <c>BloodlineIntroPanel</c> 連成同一段凍結時間、中間沒有空窗。
    /// 真正橫跨兩段的鎖由 <c>BloodlineSystem</c> 持有，本檔只負責自己這一段。
    ///
    /// ⚠ <b>三個踩過的坑</b>（改這個檔之前先讀）：
    /// <list type="number">
    /// <item><b>暫停播放 ⇒ 每一個計時器都必須是 unscaled。</b>
    ///   （2026-08-19 之前是反過來的：pause 傳 false、全部吃 <c>Time.deltaTime</c>，
    ///   代價就是怪物照打。改成暫停之後，下面每一項都得跟著換，漏掉哪一項那一項就整段凍在第一格：
    ///   玩家姿勢動畫 <c>PlayerAnimator.UnscaledPose</c>、煙塵與電弧 <c>VfxInstance.Unscaled</c>、
    ///   雷柱 <c>SegmentedLightningColumn.Unscaled</c>、本檔的 <c>Wait()</c>。
    ///   螢幕震動與白閃本來就是 unscaled，不用動。）
    ///   開頭仍然要 <c>CloseAll()</c>——背包若開著會整片蓋在演出上面。</item>
    /// <item><b>換裝會把趴姿打回站姿。</b>SetBloodline 內部重跑 PlayerAnimator.Setup，
    ///   把 sprite 換成新血統的 idle 第 0 幀；但趴地定格旗標還在、Update 直接 return 不再更新
    ///   → 角色站著定格。所以 onSwap 之後一定要 RefreshLyingPose()。</item>
    /// <item><b>所有離開路徑都要解鎖。</b>玩家死了、換場景、例外 —— 只要沒解 SetExternalHold，
    ///   玩家就永久卡住不能動。本檔用 try/finally 保證。</item>
    /// <item><b>開場的 <c>CloseAll()</c> 會把底部血球 HUD 一起關掉，還原不在本檔。</b>
    ///   還原由 <c>BloodlineSystem.RestoreHud</c> 在 <c>FinishPerformance</c> 做（世界演出＋立繪兩段的唯一共同出口）。
    ///   漏還的症狀是「變身完血條不見、之後也不再出現」，換一張圖又會自己好——見 readme/PROBLEMS.md D24。</item>
    /// </list>
    /// </summary>
    public static class BloodlineTransformFx
    {
        /// <summary>
        /// 播放變身演出。
        /// </summary>
        /// <param name="pc">玩家（可能為 null，例如還沒生出來就喝了藥）。</param>
        /// <param name="from">變身前的血統（可為 null）。用來取體型倍率。</param>
        /// <param name="to">變身後的血統（可為 null）。用來取體型倍率。</param>
        /// <param name="onSwap">換外型的動作。演出到「該換了」的那一刻呼叫，必須呼叫、且只呼叫一次。</param>
        /// <param name="onFinished">整段演出結束。必須呼叫（BloodlineSystem 靠它解除暫停收斂）。</param>
        public static void Play(PlayerController pc, BloodlineDef from, BloodlineDef to,
                                Action onSwap, Action onFinished)
        {
            if (pc == null)
            {
                // 沒有玩家可以演（理論上喝不到藥，但保險）：直接換完收工。
                onSwap?.Invoke();
                onFinished?.Invoke();
                return;
            }

            var go = new GameObject("[BloodlineTransformFx]");
            var runner = go.AddComponent<BloodlineTransformFxRunner>();
            runner.Begin(pc, from, to, onSwap, onFinished);
        }
    }

    /// <summary>
    /// 變身演出的協程宿主（照 <c>LevelExitManjiController</c> 的樣板：自建 GameObject、跑完自毀）。
    /// 節奏與外觀常數全部集中在檔頭。
    /// </summary>
    public sealed class BloodlineTransformFxRunner : MonoBehaviour
    {
        // ══════════ 節奏常數（要調表演改這裡）══════════

        /// <summary>倒下動畫的速率倍率（1 = 與初始洞窟爬起同節奏；>1 更快）。</summary>
        const float FallFpsMul = 1f;
        /// <summary>爬起動畫的速率倍率。</summary>
        const float WakeFpsMul = 1f;

        /// <summary>倒地後、雷柱出現前的空拍（讓「躺定了」有一瞬停頓）。</summary>
        const float PauseAfterFall = 0.00f;
        /// <summary>雷柱生成到「算擊中」之間的時間（讓電光先竄下來一瞬）。</summary>
        const float LightningLeadIn = 0.12f;
        /// <summary>雷柱整體存在時間。</summary>
        const float LightningDuration = 0.55f;
        /// <summary>雷柱粗細倍率。</summary>
        const float LightningScale = 1.5f;
        /// <summary>雷柱動畫幀率。</summary>
        const float LightningFps = 20f;

        /// <summary>擊中震動：秒數與振幅（世界單位）。</summary>
        const float ShakeDuration = 0.25f;
        const float ShakeAmplitude = 0.16f;
        /// <summary>擊中白閃：峰值不透明度、衝上去的時間、退回去的時間。</summary>
        const float FlashPeakAlpha = 0.85f;
        const float FlashInSeconds = 0.05f;
        const float FlashOutSeconds = 0.20f;

        /// <summary>擊中後到煙塵爆開的間隔。</summary>
        const float SmokeDelayAfterHit = 0.05f;
        /// <summary>煙塵爆開後到「換外型」的間隔（＝煙最濃的那一刻，煙塵 10 幀 @15fps 的第 6 幀）。</summary>
        const float SwapDelayAfterSmoke = 0.33f;
        /// <summary>煙塵整段的長度（10 幀 @15fps ≈ 0.67s）。</summary>
        const float SmokeTotalSeconds = 0.70f;

        /// <summary>煙霧散後，電弧還要繞多久（殘電）。</summary>
        const float AuraLingerAfterSmoke = 0.60f;
        /// <summary>電弧收掉後、開始爬起前的空拍。</summary>
        const float PauseBeforeWake = 0.20f;

        // ══════════ 外觀常數 ══════════

        /// <summary>煙塵放大到玩家可見高度的幾倍（要蓋住整個趴著的身體，所以 >1）。</summary>
        /// <remarks>
        /// 素材只有 64×64 一張，放這麼大顆粒會變粗——這是像素圖放大的必然，與專案整體風格一致。
        /// 若覺得太糊，改法是「沿身體撒 3~4 顆小的、錯開時間」：把 <see cref="SmokeBurstCount"/>
        /// 調成 3、<see cref="SmokeHeightRatio"/> 調回 0.9 左右即可，不必改流程。
        /// </remarks>
        const float SmokeHeightRatio = 1.6f;
        /// <summary>
        /// 煙塵顆數。1 = 單顆放大（目前採用）；>1 = 沿身體撒開、每顆延遲 SmokeBurstStagger。
        ///
        /// ⚠ **刻意用 <c>static readonly</c> 而不是 <c>const</c>。** 用 const 的話它是編譯期常數，
        /// 編譯器會判定 <c>SpawnSmoke</c> 裡「多顆模式」那一整段永遠走不到 → CS0162 unreachable code 警告。
        /// 那段是**留著隨時可切換的替代做法**（見 SmokeHeightRatio 的說明），不是死碼，不該為了消警告刪掉；
        /// 改成 static readonly 就兩全：值一樣、行為一樣、那段照樣編譯（不會爛掉），也沒有警告。
        /// </summary>
        static readonly int SmokeBurstCount = 1;
        const float SmokeBurstStagger = 0.08f;
        /// <summary>多顆模式下，散佈的水平半徑（相對玩家可見高度的比例）。</summary>
        const float SmokeSpreadRatio = 0.45f;

        /// <summary>環繞電弧放大到玩家可見高度的幾倍。</summary>
        const float AuraHeightRatio = 1.25f;

        /// <summary>VfxTable 的 id。</summary>
        const int SmokeVfxId = 30;
        const int AuraVfxId = 31;

        /// <summary>
        /// 變身雷柱的素材（與九霄雷獄同一套拼接邏輯，只是換外觀）。
        ///
        /// ⚠ **刻意不接頂端雷首（`Start`），整根都用 loop。** 這組素材的雷首是「尚未成形的細電光」，
        /// 邊緣只有 1~2px，而 loop 的邊緣是 5~17px——接在一起就是一根髮絲頂著一根粗電柱，接縫很明顯；
        /// 而且雷首只有 2 張、0.15 秒就播完定格，底下 loop 還在跑 8 幀循環，變成靜止的髮絲蓋在閃爍的柱子上。
        /// loop 本身上下貫穿可平鋪，純 loop 疊起來零接縫，柱頂又延伸到畫面外，不會看到斷頭。
        /// （`end` 同理不用：實測它就是 `start` 倒過來的同兩張圖。素材都還留著，想試隨時填回來。）
        /// </summary>
        static readonly SegmentedLightningColumn.Style LightningStyle =
            new SegmentedLightningColumn.Style(
                null,                                        // 不用雷首
                "VfxEffects/TransformLightning/Loop/Loop",
                0, 8, 22000);

        // ══════════ 執行期 ══════════

        /// <summary>
        /// 世界演出進行中（不含後面的立繪面板）。
        ///
        /// 一般查詢請用 <c>BloodlineSystem.IsPerforming</c>——那個把「世界演出＋立繪揭示」
        /// 兩段合起來算，是熱鍵封鎖該看的單一真相。本旗標只代表這一段，
        /// 給需要精確區分兩段的地方用。
        /// </summary>
        public static bool IsPlaying { get; private set; }

        /// <summary>
        /// 進 Play 時歸零（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。
        /// ⚠ 一定要註冊：若上一輪在演出中途按停止，IsPlaying 會殘留成 true，
        /// 下一次 Play 背包/倉庫/鍛造熱鍵會全部按不出來，而且完全沒有錯誤訊息。
        /// </summary>
        public static void ResetForPlayMode() => IsPlaying = false;

        /// <summary>
        /// 掛 UIManager external hold 用的持有者名稱。**一定要用具名版**——
        /// 演出中玩家若被打死，死亡流程也會掛 hold，共用預設持有者的話兩邊會互相清掉對方的鎖。
        /// </summary>
        const string HoldOwner = "BloodlineTransformFx";

        PlayerController _pc;
        PlayerAnimator _anim;
        Action _onSwap;
        Action _onFinished;
        bool _swapped;
        bool _held;
        bool _interrupted;          // 演出被中止（玩家死了 / 表演被打斷）→ 收尾時要把趴姿解掉
        VfxInstance _aura;          // Loop=1/Duration=-1，生死完全由這裡控，漏刪會變成永久掛在玩家身上的電弧
        MapCameraController _camCtrl;
        float _fromBodyScale = 1f;
        float _toBodyScale = 1f;

        public void Begin(PlayerController pc, BloodlineDef from, BloodlineDef to,
                          Action onSwap, Action onFinished)
        {
            _pc = pc;
            _anim = pc != null ? pc.GetComponent<PlayerAnimator>() : null;
            _onSwap = onSwap;
            _onFinished = onFinished;
            _fromBodyScale = from != null && from.BodyScale > 0.01f ? from.BodyScale : 1f;
            _toBodyScale = to != null && to.BodyScale > 0.01f ? to.BodyScale : 1f;
            IsPlaying = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var anim = _anim;
            var vfx = FindObjectOfType<VfxManager>();
            var cam = Camera.main;
            // MapCameraController 掛在 MapManager 上（不是相機上），所以用 FindObjectOfType。
            _camCtrl = FindObjectOfType<MapCameraController>();

            try
            {
                // ── 關掉所有面板（背包還開著會整片蓋在演出上），然後鎖操作＋暫停遊戲 ──
                // pause=true 是刻意的：演出期間玩家不能閃避，不凍住世界的話怪物會把他打死。
                // 代價是「所有計時器都要 unscaled」——見檔頭坑 1，下面每一個 Unscaled 旗標都是為此而設。
                // ⚠ CloseAll() 是**遍歷全部面板、不分層**，所以 HUD 層的底部血球條也會被一起關掉，
                //   而且沒有任何人會自動把它開回來（PlayerController.Start 只在初次生成跑、MapManager 只在換圖跑）。
                //   還原的責任在 BloodlineSystem.RestoreHud——它在 FinishPerformance（兩段表演的唯一共同出口）
                //   依表演前記下的 _hudWasOpen 開回去。**這裡別自己開回來**：立繪面板還要接著演，
                //   在這一段就把血球放回畫面反而會從立繪底下透出來。
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.CloseAll();
                    UIManager.Instance.SetExternalHold(HoldOwner, true, true);
                    _held = true;
                }
                // 玩家的倒下／趴地／爬起改吃 unscaled，否則 timeScale=0 時角色會倒到一半凍住。
                if (anim != null) anim.UnscaledPose = true;

                // 特效要「始終蓋得住玩家」→ 取**變身前後較大的那個體型**當基準。
                // 只用變身前的話，換成更大的血統時煙霧散開前那一段會露出新外型的頭尾。
                float charH = CoverHeight();

                // ── ① 倒下 ──
                if (anim != null)
                {
                    bool fallDone = false;
                    anim.PlayFallDown(() => fallDone = true, FallFpsMul);
                    yield return WaitPose(anim, () => fallDone);
                    // 表演被打斷（例如演出中真的被怪打死 → SetState(Dead) 會 CancelWakeUp）→ 收工。
                    if (!fallDone) { _interrupted = true; yield break; }
                }
                yield return Wait(PauseAfterFall);
                if (!Alive()) { _interrupted = true; yield break; }

                // ── ② 天雷從畫面外打下來 ──
                // 擊中點取「可見圖的底部中心」＝角色腳下站的位置，不是 transform.position。
                // transform 是 sprite 的中心，直接拿它當擊中點的話電柱會停在胸口/肩膀高度。
                Vector2 impact = FootPoint();
                if (cam != null)
                {
                    var bolt = SegmentedLightningColumn.Spawn(impact, cam, LightningStyle,
                        LightningScale * BodyScaleFactor, LightningFps, LightningDuration);
                    if (bolt != null) bolt.Unscaled = true;   // 演出期間 timeScale=0，不設的話雷柱定格在第一幀
                }
                yield return Wait(LightningLeadIn);
                if (!Alive()) { _interrupted = true; yield break; }

                // ── ③ 擊中：震動 + 白閃 ──
                if (_camCtrl != null) _camCtrl.AddShake(ShakeDuration, ShakeAmplitude);
                ScreenFader.Flash(new Color(1f, 1f, 1f, FlashPeakAlpha), FlashInSeconds, FlashOutSeconds);
                yield return Wait(SmokeDelayAfterHit);
                if (!Alive()) { _interrupted = true; yield break; }

                // ── ④ 煙塵 + 環繞電弧 ──
                if (vfx != null)
                {
                    Vector2 p = BodyCenter();
                    _aura = vfx.SpawnLoopSizedToHeight(AuraVfxId, p, charH * AuraHeightRatio, -1f);
                    if (_aura != null)
                    {
                        _aura.Unscaled = true;                          // 演出期間 timeScale=0，不設就定格
                        _aura.transform.SetParent(_pc.transform, true); // 跟著玩家
                    }
                    StartCoroutine(SpawnSmoke(vfx, charH));
                }

                // ── ⑤ 煙最濃的那一刻換外型 ──
                yield return Wait(SwapDelayAfterSmoke);
                DoSwap(anim);
                if (!Alive()) { _interrupted = true; yield break; }

                // ── ⑥ 等煙散完，電弧再留一小段殘電 ──
                yield return Wait(Mathf.Max(0f, SmokeTotalSeconds - SwapDelayAfterSmoke));
                yield return Wait(AuraLingerAfterSmoke);
                if (_aura != null) { Destroy(_aura.gameObject); _aura = null; }
                yield return Wait(PauseBeforeWake);
                if (!Alive()) { _interrupted = true; yield break; }

                // ── ⑦ 爬起來 ──
                if (anim != null)
                {
                    bool wakeDone = false;
                    anim.PlayWakeUp(() => wakeDone = true, WakeFpsMul);
                    yield return WaitPose(anim, () => wakeDone);
                }
            }
            finally
            {
                // ⚠ 坑 3：不管是正常結束、玩家消失、還是中途 yield break，都要走到這裡。
                if (_aura != null) { Destroy(_aura.gameObject); _aura = null; }
                DoSwap(anim);                       // 保底：中途中斷也要確保外型換過（_swapped 擋重複）
                if (_camCtrl != null) _camCtrl.StopShake();
                ScreenFader.ClearFlash();
                // 被中止時要把趴姿解掉。不解的話 _lyingHold 一直是 true → IsWakeUpBusy 恆真 →
                // SetState 全被忽略 → 角色永遠定格在趴姿，而且完全沒有錯誤訊息。
                if (_interrupted && anim != null) anim.CancelPose();
                // 姿勢動畫還給遊戲時間。漏還的話之後任何暫停（開背包）中角色都會繼續倒地/爬起動畫。
                if (anim != null) anim.UnscaledPose = false;
                ReleaseHold();
                var cb = _onFinished; _onFinished = null;
                cb?.Invoke();
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 等一段「倒下／爬起」表演結束。三個出口：完成、表演被打斷、逾時。
        ///
        /// ⚠ 為什麼要看 <c>IsWakeUpBusy</c> 而不只是等旗標：演出中若玩家真的被打死，
        /// <c>PlayerAnimator.SetState(Dead)</c> 會 <c>CancelWakeUp(invokeDone:false)</c> ——
        /// 回呼**不會**被叫到，只等旗標會永遠卡住。這是 MapManager 起身表演用過的同一個樣板。
        /// 逾時上限是最後一道保險（表演正常最長約 2.1 秒）。
        /// </summary>
        static IEnumerator WaitPose(PlayerAnimator anim, Func<bool> done)
        {
            const float MaxPoseSeconds = 6f;
            float t = 0f;
            while (!done() && anim != null && anim.IsWakeUpBusy && t < MaxPoseSeconds)
            {
                // ⚠ 逾時保險絲用 unscaled：萬一有人在演出期間把 timeScale 設成 0
                // （某個 PausesGame 的面板被開起來），用 deltaTime 的話連保險絲都會一起凍住。
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (t >= MaxPoseSeconds)
                Debug.LogWarning("[BloodlineTransformFx] 倒下/爬起表演逾時，強制往下走。");
        }

        /// <summary>煙塵：單顆放大，或多顆沿身體錯開時間撒。</summary>
        IEnumerator SpawnSmoke(VfxManager vfx, float charH)
        {
            if (SmokeBurstCount <= 1)
            {
                if (_pc != null)
                    MarkUnscaled(vfx.SpawnSizedToHeight(SmokeVfxId, BodyCenter(), charH * SmokeHeightRatio));
                yield break;
            }

            float spread = charH * SmokeSpreadRatio;
            for (int i = 0; i < SmokeBurstCount; i++)
            {
                if (_pc == null) yield break;
                // 沿身體左右鋪開（-spread ~ +spread），高度隨機一點點避免排成一直線。
                float t = SmokeBurstCount == 1 ? 0f : (i / (float)(SmokeBurstCount - 1)) * 2f - 1f;
                Vector2 p = BodyCenter()
                          + new Vector2(t * spread, UnityEngine.Random.Range(-0.15f, 0.15f) * charH);
                MarkUnscaled(vfx.SpawnSizedToHeight(SmokeVfxId, p, charH * SmokeHeightRatio));
                yield return Wait(SmokeBurstStagger);
            }
        }

        /// <summary>演出期間 timeScale=0，特效不切 unscaled 就會定格在第一幀。</summary>
        static VfxInstance MarkUnscaled(VfxInstance v)
        {
            if (v != null) v.Unscaled = true;
            return v;
        }

        /// <summary>換外型（只會生效一次）。⚠ 坑 2：換完一定要把趴姿重新定回去。</summary>
        void DoSwap(PlayerAnimator anim)
        {
            if (_swapped) return;
            _swapped = true;
            _onSwap?.Invoke();
            _onSwap = null;
            // SetBloodline → PlayerAnimator.Setup 會把 sprite 重設成新血統的 idle 第 0 幀（站姿），
            // 但趴地定格旗標還在、Update 直接 return 不再更新 → 不重定的話角色會站著不動。
            if (anim != null) anim.RefreshLyingPose();
        }

        /// <summary>
        /// 演出可以繼續嗎：玩家還在、演出物件還在、而且玩家沒死。
        /// ⚠ <c>IsDead</c> 這一項很重要——演出期間玩家是被鎖住不能閃避的，被怪打死完全可能發生。
        /// 死了還繼續演會變成「屍體爬起來」，而且死亡流程（EndLevel）也掛了自己的輸入鎖，
        /// 兩邊會打架。任一項不成立就中止（finally 會收乾淨並解掉趴姿）。
        /// </summary>
        bool Alive() => _pc != null && this != null && !_pc.IsDead;

        /// <summary>
        /// 等待 seconds 秒。**一定要 unscaled**——整段演出跑在 <c>timeScale = 0</c> 底下，
        /// 用 <c>Time.deltaTime</c> 的話這個迴圈永遠不會前進，演出會停在第一個 Wait 上。
        /// 動畫／特效那邊也各自切成 unscaled（見檔頭坑 1），四者共用同一條時間軸。
        /// </summary>
        static IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f) yield break;
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // ══════════ 幾何助手 ══════════

        /// <summary>變身前後較大的那個體型倍率。特效尺寸與雷柱粗細都乘它。</summary>
        float BodyScaleFactor => Mathf.Max(_fromBodyScale, _toBodyScale);

        /// <summary>
        /// 特效要蓋住的高度：取「目前實際畫出來的高度」與「變身後的站立高度」較大者。
        /// 前者反映當下的姿勢（趴著時比較矮），後者保證換成更大的血統時煙霧仍然蓋得住。
        /// </summary>
        float CoverHeight()
        {
            float standing = _pc != null && _pc.CharacterWorldHeight > 0f
                ? _pc.CharacterWorldHeight : 1.95f;
            return Mathf.Max(CurrentRenderedHeight(), standing * BodyScaleFactor);
        }

        /// <summary>玩家目前實際畫出來的高度（跟著姿勢變，趴著時比較矮）。取不到時用設定值兜底。</summary>
        float CurrentRenderedHeight()
        {
            float h = _pc != null ? _pc.VisibleBodyHeight : 0f;
            if (h <= 0.01f) h = _pc != null && _pc.CharacterWorldHeight > 0f ? _pc.CharacterWorldHeight : 1.95f;
            return h;
        }

        /// <summary>
        /// 角色腳下站的位置。
        /// ⚠ 不要用 <c>transform.position</c>（那是畫布中心，雷柱會停在胸口/肩膀高度），
        /// 也不要自己讀 <c>SpriteRenderer.bounds</c>（含不含透明留白不保證）——
        /// 一律走 PlayerController 那組從縮放參數解析算出來的幾何。
        /// </summary>
        Vector2 FootPoint()
            => _pc != null ? _pc.FeetWorldPos : Vector2.zero;

        /// <summary>角色身體的可見中心（煙霧與電弧對齊這裡，才會正好罩住身體而不是偏上）。</summary>
        Vector2 BodyCenter()
            => _pc != null ? _pc.BodyCenterWorldPos : Vector2.zero;

        void OnDestroy()
        {
            // 場景被切換 / 物件被外力銷毀時，協程的 finally 不保證跑完 → 這裡再保一次全部收乾淨。
            // 特別是電弧：它 Duration=-1 且掛在玩家身上，漏刪會永遠繞著玩家轉。
            if (_aura != null) { Destroy(_aura.gameObject); _aura = null; }
            if (_camCtrl != null) _camCtrl.StopShake();
            ScreenFader.ClearFlash();
            if (_anim != null)
            {
                _anim.CancelPose();          // 沒收尾就被銷毀 → 角色會永遠定格在趴姿
                _anim.UnscaledPose = false;  // 姿勢動畫還給遊戲時間
            }
            ReleaseHold();
            var cb = _onFinished; _onFinished = null;
            cb?.Invoke();
        }

        /// <summary>解除自己掛的輸入鎖並清 IsPlaying。可重複呼叫。</summary>
        void ReleaseHold()
        {
            if (_held && UIManager.Instance != null)
                UIManager.Instance.SetExternalHold(HoldOwner, false, false);
            _held = false;
            IsPlaying = false;
        }
    }
}
