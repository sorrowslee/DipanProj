using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Dipan.UI
{
    /// <summary>
    /// 設定面板（呈現層）。背景用 SettingPanelBG.png 整張當底，依量到的像素座標在上面疊互動元件
    /// （兩條音量 slider 的把手、右上關閉鈕、底部離開遊戲鈕）。座標都在「背景原圖像素空間」(1123x1401)，
    /// 整個 frame 等比縮放塞進畫面——做法與 InventoryPanel / StoragePanel 一致。
    ///
    /// 目前狀態（見 readme/TODO.md）：
    ///   - 音樂 / 音效 slider 只做到「可拖曳」，**尚未接上實際音訊**（專案還沒有音訊系統）。值先存記憶體。
    ///   - 關閉鈕（右上 X）→ 關閉本面板；離開遊戲鈕 → 跳出確認彈窗（ConfirmPopup，暫時自刻、之後換正式美術）。
    ///
    /// 風格對齊專案：全程式建構、零 prefab / Inspector 接線。
    /// </summary>
    public class SettingsPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;     // 共用遮罩由 UIManager 鋪在所有視窗最底層
        public override bool CloseOnEscape => true;

        // ── 背景原圖尺寸 ──
        const float BgW = 1123f, BgH = 1401f;

        // ── 版面座標（量自背景原圖，左上為原點，X→右、Y→下；實機若有偏移微調這裡即可）──
        // 音樂 slider 軌道：左端 x、右端 x、垂直中心 y（把手中心會在 [左,右] 之間移動）
        const float MusicRailLeft = 470f, MusicRailRight = 865f, MusicRailY = 528f;
        // 音效 slider 軌道
        const float SfxRailLeft = 470f, SfxRailRight = 865f, SfxRailY = 772f;
        // 拖曳把手（DragIcon）大小
        const float HandleSize = 92f;

        // 右上關閉鈕（中心 + 大小）
        const float CloseCx = 1018f, CloseCy = 150f, CloseSize = 110f;
        // 底部離開遊戲鈕（中心 + 寬高；LongBtn 原圖 612x408，比例 1.5:1）
        const float ExitCx = 561f, ExitCy = 985f, ExitW = 312f, ExitH = 208f;
        const float ExitIconSize = 130f;   // 門 icon 疊在離開鈕上的大小

        [Tooltip("面板顯示高度（CanvasScaler 參考單位，1080 為滿版）。")]
        public float displayHeight = 1040f;

        // 音量值先存記憶體（之後接 settings.json 持久化 + 音訊系統，見 readme/TODO.md）
        static float _musicVol = 0.7f;
        static float _sfxVol = 0.7f;

        RectTransform _frame;
        Slider _musicSlider, _sfxSlider;

        protected override void OnBuild()
        {
            // frame：原圖尺寸、置中、等比縮放塞進畫面
            var frameGO = UIBuilder.Create("Frame", transform);
            _frame = UIBuilder.Rect(frameGO);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = Vector2.zero;
            _frame.sizeDelta = new Vector2(BgW, BgH);
            float scale = displayHeight / BgH;
            _frame.localScale = new Vector3(scale, scale, 1f);

            // 背景
            var bg = UIBuilder.Image(frameGO.transform, "BG", UIBuilder.LoadSprite("UI/SettingPanel/SettingPanelBG"));
            UIBuilder.Stretch(bg.rectTransform);
            bg.raycastTarget = true;   // 吃掉空白處點擊（不穿到遊戲）

            // 兩條音量 slider（軌道與圖示已畫在背景上，這裡只疊可拖曳的把手）
            _musicSlider = BuildSlider("MusicSlider", MusicRailLeft, MusicRailRight, MusicRailY, _musicVol,
                                       v => _musicVol = v);
            _sfxSlider = BuildSlider("SfxSlider", SfxRailLeft, SfxRailRight, SfxRailY, _sfxVol,
                                     v => _sfxVol = v);

            BuildCloseButton();
            BuildExitButton();
        }

        protected override void OnOpen()
        {
            // 重新開啟時，把把手同步到目前記憶值
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(_musicVol);
            if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(_sfxVol);
        }

        // ── 音量 slider：軌道與圖示在背景上，這裡建一個 Unity Slider 把 DragIcon 當把手 ──
        Slider BuildSlider(string name, float railLeft, float railRight, float railY, float initVal,
                           UnityAction<float> onChanged)
        {
            float w = railRight - railLeft;
            float cx = (railLeft + railRight) * 0.5f;

            var go = UIBuilder.Create(name, _frame);
            Place(UIBuilder.Rect(go), cx, railY, w, HandleSize);

            // 透明命中底：點軌道任意處也能跳到該位置
            var track = go.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0f);
            track.raycastTarget = true;

            var slider = go.AddComponent<Slider>();

            // Handle Slide Area：填滿整個軌道、無 padding → 把手中心可達兩端
            var area = UIBuilder.Create("Handle Slide Area", go.transform);
            UIBuilder.Stretch(UIBuilder.Rect(area));

            // 把手 = DragIcon
            var handleImg = UIBuilder.Image(area.transform, "Handle", UIBuilder.LoadSprite("UI/Common/DragIcon"));
            handleImg.preserveAspect = true;
            handleImg.raycastTarget = true;
            var hrt = handleImg.rectTransform;
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.anchoredPosition = Vector2.zero;
            // Slider 會驅動把手的 anchor（x=值、y=0..1），所以寬用 sizeDelta.x、高 = 軌道高(=HandleSize)，
            // 故 sizeDelta.y 設 0 即得到 HandleSize x HandleSize 的方形把手。
            hrt.sizeDelta = new Vector2(HandleSize, 0f);

            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = null;             // 不用填色（軌道是背景圖）
            slider.handleRect = hrt;
            slider.targetGraphic = handleImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(initVal);
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        // ── 右上關閉鈕（兩態 SpriteSwap）→ 關閉本面板 ──
        void BuildCloseButton()
        {
            Sprite N = UIBuilder.LoadSprite("UI/Common/CloseBtn_normal");
            Sprite P = UIBuilder.LoadSprite("UI/Common/CloseBtn_pressed");
            var b = UIBuilder.Button(_frame, "CloseBtn", "", () => UIManager.Instance.Close(this), Color.white, N);
            var img = b.GetComponent<Image>();
            img.preserveAspect = true;
            b.targetGraphic = img;                       // 程式建按鈕需手動指（見 PROBLEMS D4）
            b.transition = Selectable.Transition.SpriteSwap;
            var ss = b.spriteState;
            ss.pressedSprite = P; ss.highlightedSprite = N; ss.selectedSprite = N;
            b.spriteState = ss;
            Place((RectTransform)b.transform, CloseCx, CloseCy, CloseSize, CloseSize);
        }

        // ── 底部離開遊戲鈕（兩態 SpriteSwap）→ 跳出確認彈窗 ──
        void BuildExitButton()
        {
            Sprite N = UIBuilder.LoadSprite("UI/Common/LongBtn_normal");
            Sprite P = UIBuilder.LoadSprite("UI/Common/LongBtn_pressed");
            var b = UIBuilder.Button(_frame, "ExitBtn", "", OnClickExit, Color.white, N);
            var img = b.GetComponent<Image>();
            img.preserveAspect = true;
            b.targetGraphic = img;
            b.transition = Selectable.Transition.SpriteSwap;
            var ss = b.spriteState;
            ss.pressedSprite = P; ss.highlightedSprite = N; ss.selectedSprite = N;
            b.spriteState = ss;
            Place((RectTransform)b.transform, ExitCx, ExitCy, ExitW, ExitH);

            // 門 icon 疊在按鈕中央（不擋點擊）
            var icon = UIBuilder.Image(b.transform, "DoorIcon", UIBuilder.LoadSprite("UI/SettingPanel/SettingPanelExitIcon"));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(ExitIconSize, ExitIconSize);
        }

        void OnClickExit()
        {
            // 暫時用自刻的確認彈窗（之後換正式美術，見 readme/TODO.md）
            ConfirmPopup.Show("確定要離開遊戲嗎？", QuitGame);
        }

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;   // 編輯器內：停止 Play 方便測試
#else
            Application.Quit();
#endif
        }

        // 座標映射：錨到 frame 左上角、anchoredPosition=(px,-py)（與 InventoryPanel 一致）
        void Place(RectTransform rt, float px, float py, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(px, -py);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
