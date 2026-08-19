using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Flow;

namespace Dipan.UI
{
    /// <summary>
    /// 標題畫面（Window 層、全螢幕）。＝標題文字 ＋ 中間偏右的佛陀動畫 ＋「開始遊戲」鈕。
    /// 流程：按下開始 → 播一次佛陀動畫（BuddhaTitle_01..NN）→ 播完才開三欄存讀檔畫面。
    /// **其餘為佔位視覺**（純色底 + 內建字型 + 純色鈕），之後換上正式標題圖與按鈕素材即可。
    /// 見 readme/TITLE_AND_SAVE_UI.md。
    /// </summary>
    public class TitlePanel : UIPanel
    {
        // Window 層（不是 Overlay）：UI Canvas 已在遊戲世界相機之上，整片不透明底就能蓋住背後場景；
        // 且放 Window 層，覆蓋/刪除的 ConfirmPopup（Popup 層）才會顯示在本面板之上。
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => false;        // 標題不因 ESC 關閉
        public override bool ShowBackdrop => false;         // 自己就是整片不透明底

        // ───────────── 佛陀動畫設定（要調就改這裡）─────────────
        const string BuddhaFramePrefix = "UI/TitlePanel/BuddhaTitle/BuddhaTitle_"; // 幀路徑前綴（Resources 下、不含編號與副檔名）
        const int    BuddhaMaxFrames   = 64;    // 自動偵測幀數的上限（載到 null 就停，加幀免改程式）
        const float  BuddhaFps         = 8f;    // 動畫播放速度（幀/秒）：8 幀 ÷ 8 fps = 1 秒
        const float  BuddhaEndHold     = 0.3f;    // 動畫播完後多停留幾秒才轉場（停在最後一幀）
        const float  BuddhaDisplaySize = 950f;  // 顯示邊長（像素，維持長寬比）
        static readonly Vector2 BuddhaOffset = new Vector2(480f, -170f); // 相對畫面中心的位移（正 X = 偏右、負 Y = 往下，把下半身切邊推出畫面）

        // 標題文字與開始鈕整體往左偏移（負 X），與偏右的佛陀錯開。
        const float TextGroupX = -380f;

        // ───────────── 標題圖 / 開始鈕圖（正式素材，皆 3:1）─────────────
        // 標題圖：圖片型文字，實際檔案在 UI/Texts/<語言>/TitlePanel_Title
        // （繁中＝燃燈劫、英文＝Burning Lamp: Rebirth of Ruin）。**兩邊同名**，靠資料夾分語言。
        const string TitleImagePath    = "UI/Texts/TitlePanel_Title";
        const string StartBtnImagePath = "UI/Common/StartGameBtn";      // 開始鈕圖（無字，字由程式補）
        const float  TitleWidth    = 820f;   // 標題圖寬（高 = 寬 / 3）
        const float  TitleY        = 140f;   // 標題圖 Y（相對畫面中心）
        const float  StartBtnWidth = 460f;   // 開始鈕寬（高 = 寬 / 3）
        const float  StartBtnY     = -150f;  // 開始鈕 Y

        const int TxtStartGame = 6001;   // 「開始遊戲」（LanguageTable 標題畫面段 6001–6099）

        // ───────────── 火焰特效 ─────────────
        // ⚠ static readonly 不是 const：這是個「關掉試試看」的開關，const 的話一改成 false
        //   整段火焰程式就會被判定為 unreachable（CS0162）。同 SaveSlotPanel.ActorFlipX。
        static readonly bool EnableFireFx = true;   // 全螢幕落火 ＋ 標題燃燒（見 TitleFireFx）

        Sprite[] _buddhaFrames;
        Image _buddha;
        Button _startBtn;
        Image _titleGlow;             // 標題背後火光
        RectTransform _titleRect;     // 標題圖/文字的 rect（火舌掛在其上）
        bool _playing;   // 動畫播放中：擋住重複點擊

        protected override void OnBuild()
        {
            // 全螢幕底（佔位：深色）。之後換成標題背景圖：UIBuilder.Image(transform,"BG",UIBuilder.LoadSprite("UI/Title/Background"))
            var bg = UIBuilder.SolidPanel(transform, "BG", new Color(0.06f, 0.05f, 0.08f, 1f));
            bg.raycastTarget = true;

            // 佛陀動畫（中間偏右）。先建 → 排在文字/按鈕之前，讓文字與按鈕畫在其上、不被蓋住。
            _buddhaFrames = LoadBuddhaFrames();
            _buddha = UIBuilder.Image(transform, "Buddha",
                (_buddhaFrames != null && _buddhaFrames.Length > 0) ? _buddhaFrames[0] : null);
            _buddha.preserveAspect = true;
            _buddha.raycastTarget = false;
            UIBuilder.Anchor(_buddha.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                BuddhaOffset, new Vector2(BuddhaDisplaySize, BuddhaDisplaySize));

            // 標題背後火光（燃燒氛圍，脈動由 TitleFireFx 驅動）。排在標題圖之前 → 畫在標題圖背後。
            _titleGlow = UIBuilder.Image(transform, "TitleGlow", SceneEffectSprites.Glow(),
                new Color(1f, 0.45f, 0.12f, 0f));   // 起始透明，交給 fx 脈動
            _titleGlow.raycastTarget = false;
            UIBuilder.Anchor(_titleGlow.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(TextGroupX, TitleY), new Vector2(TitleWidth * 1.25f, (TitleWidth / 3f) * 2.0f));

            // 標題圖（正式素材，3:1）。找不到就退回文字佔位。
            var titleSprite = UIBuilder.LoadSprite(TitleImagePath);
            if (titleSprite != null)
            {
                var titleImg = UIBuilder.Image(transform, "Title", titleSprite);
                titleImg.preserveAspect = true;
                titleImg.raycastTarget = false;
                UIBuilder.Anchor(titleImg.rectTransform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(TextGroupX, TitleY), new Vector2(TitleWidth, TitleWidth / 3f));
                _titleRect = titleImg.rectTransform;
            }
            else
            {
                var title = UIBuilder.Text(transform, "Title", "燃燈劫", 110,
                    new Color(0.90f, 0.20f, 0.20f), TextAnchor.MiddleCenter);
                UIBuilder.Anchor(title.rectTransform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(TextGroupX, TitleY), new Vector2(1200f, 200f));
                _titleRect = title.rectTransform;
            }

            // 開始遊戲鈕：用正式按鈕圖（3:1、無字），文字由程式補在圖上。
            var btnSprite = UIBuilder.LoadSprite(StartBtnImagePath);
            _startBtn = UIBuilder.Button(transform, "StartButton", Dipan.Localization.Language.GetText(TxtStartGame), OnStart,
                bgColor: Color.white, bgSprite: btnSprite);
            var startImg = _startBtn.GetComponent<Image>();
            startImg.preserveAspect = true;
            _startBtn.targetGraphic = startImg;   // 程式建鈕需手動指（見 PROBLEMS D4）
            var startLabel = _startBtn.GetComponentInChildren<Text>();
            if (startLabel != null) startLabel.fontSize = 34;   // 補在圖上的文字放大些
            UIBuilder.Anchor((RectTransform)_startBtn.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(TextGroupX, StartBtnY), new Vector2(StartBtnWidth, StartBtnWidth / 3f));

            // 火焰特效：全螢幕落火層（最上層、不擋點擊）＋ 標題燃燒。放最後 → 落火畫在最前面。
            if (EnableFireFx)
            {
                var emberRoot = UIBuilder.Create("EmberLayer", transform);
                var emberRt = UIBuilder.Stretch((RectTransform)emberRoot.transform);
                var fx = gameObject.AddComponent<TitleFireFx>();
                fx.Init(emberRt, _titleRect, _titleGlow);
            }
        }

        protected override void OnOpen()
        {
            // 每次回到標題都重置：停在第一幀、允許再次點擊。
            _playing = false;
            if (_startBtn != null) _startBtn.interactable = true;
            if (_buddha != null && _buddhaFrames != null && _buddhaFrames.Length > 0)
                _buddha.sprite = _buddhaFrames[0];
        }

        /// <summary>依前綴自動載入 BuddhaTitle_01、_02…直到載不到為止（加幀免改程式）。</summary>
        static Sprite[] LoadBuddhaFrames()
        {
            var list = new List<Sprite>();
            for (int i = 1; i <= BuddhaMaxFrames; i++)
            {
                var s = Resources.Load<Sprite>(BuddhaFramePrefix + i.ToString("D2"));
                if (s == null) break;
                list.Add(s);
            }
            if (list.Count == 0)
                Debug.LogWarning($"[TitlePanel] 找不到佛陀動畫幀：Resources/{BuddhaFramePrefix}01…（請確認圖與 Sprite 設定）");
            return list.ToArray();
        }

        void OnStart()
        {
            if (_playing) return;   // 動畫播放中忽略重複點擊

            // 沒有幀就退回原本行為：直接開存讀檔畫面。
            if (_buddhaFrames == null || _buddhaFrames.Length == 0)
            {
                GoToSlotSelect();
                return;
            }

            _playing = true;
            if (_startBtn != null) _startBtn.interactable = false;
            StartCoroutine(PlayBuddhaThenContinue());
        }

        /// <summary>播一次佛陀動畫（用 unscaledTime，因為本面板把遊戲暫停），播完才切換到下一個 UI。</summary>
        IEnumerator PlayBuddhaThenContinue()
        {
            float frameDur = 1f / Mathf.Max(0.01f, BuddhaFps);
            for (int i = 0; i < _buddhaFrames.Length; i++)
            {
                if (_buddha != null) _buddha.sprite = _buddhaFrames[i];
                float t = 0f;
                while (t < frameDur)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 播完停在最後一幀多停留一下，再轉場（節奏更沉穩、不會太快切走）。
            float hold = 0f;
            while (hold < BuddhaEndHold)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            GoToSlotSelect();
        }

        void GoToSlotSelect()
        {
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.OpenSlotSelect();
            else UIManager.Instance.Open<SaveSlotPanel>();
        }
    }
}
