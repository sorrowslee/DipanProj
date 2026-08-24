using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 進場的「場景說明」：走進一張有名字的地圖時，畫面上方淡入一張金色毛筆場景名，
    /// 底下墊一條血紅分隔線，停留一下再淡出。
    /// **整段暫停遊戲、鎖住操作**，播完才把場面交還給遊戲——所以名字一定看得完整，
    /// 進場對話/教學也一定接在它後面（見下方「誰呼叫」）。
    ///
    /// 表演時間軸（全程 unscaled 時間，與所有 UI 動畫一致）：
    /// <code>
    ///   0                          淡入（FadeInSeconds；由 UIPanel 的 CanvasGroup 負責）
    ///   FadeInSeconds              停留（HoldSeconds）
    ///   FadeInSeconds + Hold       淡出（FadeOutSeconds）→ 關閉
    /// </code>
    ///
    /// <b>資料來源</b>：MapsTable 的 <b>SceneTip 欄</b>（填 key，留空 = 這張圖不顯示）。
    /// 文字圖 = <c>Resources/UI/Texts/SceneTipPanel_Text_&lt;key&gt;</c>。
    /// **前綴寫死在這裡是刻意的**：CSV 只填會變的那一段（key），規則留在程式，
    /// 之後要搬資料夾或改命名只改這一個常數，不必回頭改每一列資料。
    /// 語言資料夾（tw/en）由 <see cref="Dipan.Localization.LocalizedArt"/> 自動解析，缺 en 自動退回 tw。
    /// 分隔線底版 = <c>Resources/UI/SceneTipPanel/SceneTipPanel_Bg</c>（全場景共用一張，與語言無關）。
    ///
    /// <b>⚠ key 不是地圖 Name</b>：Name 是程式/檔案的內部名（<c>Main_Square</c>），
    /// key 是美術命名（<c>BuddhaSquare</c>）。兩者刻意不綁在一起——地圖檔改名時圖不會跟著壞，
    /// 而且壞法會是「安靜地沒東西跳出來」，最難查。
    ///
    /// <b>誰呼叫</b>：<c>MapManager.FireEnterTriggersRoutine</c>——在「進場全螢幕特效（睜眼醒來）」與
    /// 「進場自動劇情」都播完之後、進場觸發點點火之前，所以不會蓋在過場上。
    /// ⚠ 那邊會**等整段播完（含淡出）才點火進場觸發**，靠的是 <see cref="IsPlaying"/>；
    /// 沒有那道等待的話，進場對話會直接疊在名字上（2026-08-24 作者實測回報）。
    /// 「同一趟關卡同一個 key 只顯示一次」的去重也在那裡（用 key 判定，不是地圖 id）。
    ///
    /// <b>調版面</b>：Play 模式中在 Hierarchy 選 [UIManager] → Layer_Overlay → SceneTipPanel
    /// （第一次跳過之後才存在），在 Inspector 即時調，下次進圖立刻套用（版面每次 Begin 重算）。
    /// ⚠️ 面板是執行期程式生成，Play 模式調的值退出後不會保存——調到滿意要把數值回填本檔的預設值。
    /// 見 readme/SCENE_TIP.md。
    /// </summary>
    public class SceneTipPanel : UIPanel
    {
        // Overlay：蓋在 HUD 與視窗之上，不入堆疊、ESC 不會誤關（同 BossIntroPanel）。
        // ⚠ 這層鋪滿整個畫面 → 底下每一個 Graphic 都必須 raycastTarget=false，否則會吃掉玩家的點擊。
        public override UILayer Layer => UILayer.Overlay;
        // 暫停＋鎖操作：先看完名字，才開始這張圖的機制（進場對話/教學）。
        // ⚠ 刻意**不是**「只鎖輸入不暫停」——紅嫁衣一進場就有怪，鎖了輸入不暫停等於站著挨打。
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;
        // 淡入與淡出秒數不同，但基底只有一個 FadeDuration ⇒ 用一個欄位頂著，
        // 在 OnOpen/OnClose 各自設定（兩者都在 StartFade 之前被呼叫，見 UIPanel.DoOpen/DoClose）。
        public override float FadeDuration => _fadeNow;

        // ───────── 表演節奏（秒，unscaled）─────────
        [Header("表演節奏（秒，unscaled）")]
        [Tooltip("淡入秒數")]
        public float FadeInSeconds = 0.5f;
        [Tooltip("完全顯示後停留多久才開始淡出。整段是暫停遊戲的，而邪佛廣場每一輪都會再跳一次，所以刻意壓短")]
        public float HoldSeconds = 1.5f;
        [Tooltip("淡出秒數")]
        public float FadeOutSeconds = 0.6f;

        // ───────── 版面（CanvasScaler 參考解析度 1920×1080 下的尺寸；每次 Begin 重算）─────────
        [Header("場景名文字圖（1920×1080 參考解析度）")]
        [Tooltip("文字圖顯示高度（寬依原圖比例）。用高度而不是寬度定尺寸：字數不同的場景名（三字/四字）看起來才一樣大")]
        public float TextHeight = 165f;
        [Tooltip("寬度上限（0 = 不限）。英文名很長時改由寬度決定尺寸，避免超出畫面")]
        public float TextMaxWidth = 900f;
        [Tooltip("文字中心相對畫面中心的垂直位移（+ 往上）")]
        public float TextCenterY = 175f;

        [Header("分隔線底版")]
        [Tooltip("要不要顯示底下那條血紅分隔線")]
        public bool ShowBg = true;
        [Tooltip("底版圖（Resources 路徑，不含副檔名）。與語言無關，全場景共用")]
        public string BgSpritePath = "UI/SceneTipPanel/SceneTipPanel_Bg";
        [Tooltip("底版顯示寬度（高依原圖比例）")]
        public float BgWidth = 610f;
        [Tooltip("底版中心相對畫面中心的垂直位移（+ 往上）")]
        public float BgCenterY = 115f;

        /// <summary>文字圖的 Resources 路徑前綴。CSV 的 SceneTip 欄接在這後面。**改命名規則只改這裡**。</summary>
        public const string TextPathPrefix = "UI/Texts/SceneTipPanel_Text_";

        /// <summary>
        /// 還在表演中（**含淡出**）。呼叫端用它等「整段真的播完」再往下走。
        ///
        /// ⚠ 刻意用 <c>gameObject.activeSelf</c> 而不是 <see cref="UIPanel.IsOpen"/>：
        /// <c>IsOpen</c> 在 <c>DoClose</c> 的第一行就變 false，那時淡出**才剛開始**——
        /// 等它等於在名字還看得見的時候就放行，對話照樣會疊上來（只是疊得比較淡）。
        /// UIPanel 的淡出跑完會把物件 SetActive(false)，所以 activeSelf 才是「整段結束」。
        /// 也刻意不用 static 旗標：關掉 Domain Reload 之後 static 會殘留（見 PROBLEMS D22）。
        /// </summary>
        public bool IsPlaying => gameObject.activeSelf;

        Image _text, _bg;
        float _fadeNow = 0.35f;   // 目前這一次淡入/淡出要用的秒數（見 FadeDuration）
        float _t;                 // 開演至今（unscaled 秒）
        float _endTime;           // 開始淡出的時刻
        bool _running;

        /// <summary>
        /// 跳一次場景說明。key = MapsTable 的 SceneTip 欄。
        /// 回傳有沒有真的跳出來（key 空、沒有 UIManager、或文字圖不存在都回 false —— 一律不擋流程）。
        /// </summary>
        public static bool Show(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (UIManager.Instance == null) return false;   // 單場景測試沒有 UIManager，安靜略過

            var sprite = UIBuilder.LoadSprite(TextPathPrefix + key);
            if (sprite == null)
            {
                // UIBuilder.LoadSprite 已經印過「找不到 Sprite」，這裡補上「該放哪、檔名要叫什麼」。
                Debug.LogWarning(
                    $"[SceneTipPanel] MapsTable 的 SceneTip 填了「{key}」但沒有對應的文字圖，這次不顯示。" +
                    $"請放 Assets/Resources/UI/Texts/tw/SceneTipPanel_Text_{key}.png（英文版放 en/ 底下、檔名要完全同名）。");
                return false;
            }

            var p = UIManager.Instance.Open<SceneTipPanel>();
            if (p == null) return false;
            p.Begin(sprite);
            return true;
        }

        // OnBuild 只建骨架；尺寸/座標/圖每次 Begin 重算（讓 Inspector 調完下次進圖就生效）。
        // 疊層順序（先建 = 最底）：分隔線底版 → 場景名文字。
        protected override void OnBuild()
        {
            _bg = UIBuilder.Image(transform, "Divider", null);
            _bg.preserveAspect = true;
            _bg.raycastTarget = false;      // ⚠ Overlay 層鋪滿畫面，不關掉會吃掉玩家的點擊
            _bg.enabled = false;
            var brt = _bg.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);

            _text = UIBuilder.Image(transform, "SceneName", null);
            _text.preserveAspect = true;
            _text.raycastTarget = false;
            _text.enabled = false;
            var trt = _text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>套版面、起算計時。已經在演的時候再被呼叫 = 換圖並重新起算。</summary>
        void Begin(Sprite textSprite)
        {
            // ── 場景名：用「高度」定尺寸，字數不同的場景名看起來才一樣大；太寬才改由寬度上限收 ──
            float h = Mathf.Max(1f, TextHeight);
            float aspect = textSprite.rect.height > 0f ? textSprite.rect.width / textSprite.rect.height : 3f;
            float w = h * aspect;
            if (TextMaxWidth > 1f && w > TextMaxWidth) { w = TextMaxWidth; h = w / Mathf.Max(0.01f, aspect); }

            _text.sprite = textSprite;
            _text.enabled = true;
            _text.rectTransform.sizeDelta = new Vector2(w, h);
            _text.rectTransform.anchoredPosition = new Vector2(0f, TextCenterY);

            // ── 分隔線底版（每次 Begin 載入：Resources 有快取，Inspector 改路徑下次進圖即換圖）──
            var bgSprite = ShowBg ? UIBuilder.LoadSprite(BgSpritePath) : null;
            _bg.sprite = bgSprite;
            _bg.enabled = bgSprite != null;
            if (bgSprite != null)
            {
                float ba = bgSprite.rect.height > 0f ? bgSprite.rect.width / bgSprite.rect.height : 10f;
                _bg.rectTransform.sizeDelta = new Vector2(BgWidth, BgWidth / Mathf.Max(0.01f, ba));
                _bg.rectTransform.anchoredPosition = new Vector2(0f, BgCenterY);
            }

            _t = 0f;
            _endTime = Mathf.Max(0f, FadeInSeconds) + Mathf.Max(0f, HoldSeconds);
            _running = true;
        }

        void Update()
        {
            if (!IsOpen || !_running) return;

            // 換圖就立刻收：不要讓上一張圖的名字跟到下一張去（同 B8 的通則——讀取頁不暫停遊戲，
            // 跨 module 換圖是一段長達數秒的協程，這期間 Update 照跑）。
            if (MapManager.Instance != null && MapManager.Instance.IsLoading) { Finish(); return; }

            _t += Time.unscaledDeltaTime;
            if (_t >= _endTime) Finish();
        }

        void Finish()
        {
            if (!_running) return;
            _running = false;
            if (UIManager.Instance != null) UIManager.Instance.Close(this);
        }

        // 淡入/淡出秒數在這裡設定：兩個鉤子都在 UIPanel 開始跑淡入淡出之前被呼叫。
        protected override void OnOpen() { _fadeNow = Mathf.Max(0f, FadeInSeconds); }

        protected override void OnClose() { _fadeNow = Mathf.Max(0f, FadeOutSeconds); _running = false; }
    }
}
