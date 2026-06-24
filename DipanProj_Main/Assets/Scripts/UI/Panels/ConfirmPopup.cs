using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 通用「是 / 否」確認彈窗。模態、放在 Popup 層（蓋在設定面板等 Window 視窗之上），自己畫一張全螢幕暗底。
    ///
    /// 外觀走真素材：背板 `PopupPanelBG.png`，OK / No 兩顆鈕用 Common 的 `LongBtn`（兩態 SpriteSwap）+
    /// 疊上勾(`PopupPanelOkIcon`)/叉(`PopupPanelNoIcon`) icon。做法與 SettingsPanel 一致（整張背板 + 量測座標）。
    ///
    /// 用法：<see cref="Show"/>("確定要離開遊戲嗎？", onConfirm)。按 OK → 關閉並執行 onConfirm；
    /// 按 No 或 ESC → 只關閉。座標為量測值，實機若偏移微調本檔上方常數即可。
    /// </summary>
    public class ConfirmPopup : UIPanel
    {
        public override UILayer Layer => UILayer.Popup;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => false;    // Popup 層用不到共用遮罩，這裡自己畫暗底
        public override bool CloseOnEscape => true;

        // ── 背板原圖尺寸 / 顯示大小 ──
        const float BgW = 1448f, BgH = 1086f;
        const float DisplayHeight = 660f;   // 彈窗在畫面上的高度（CanvasScaler 參考單位）

        // ── 版面座標（背板原圖像素，左上為原點、填中心點；實機微調這裡）──
        const float MsgCx = 724f, MsgCy = 540f, MsgW = 960f, MsgH = 210f;   // 訊息文字（上方內襯）
        const float BtnW = 300f, BtnH = 200f, BtnY = 798f;                  // 兩顆鈕共用寬高與 y
        const float OkCx = 548f, NoCx = 900f;                                // OK 左、No 右
        const float BtnIconSize = 120f;                                      // 勾/叉 icon 大小

        RectTransform _frame;
        Text _msg;
        Action _onConfirm;

        /// <summary>開啟確認彈窗。任何系統可呼叫。</summary>
        public static void Show(string message, Action onConfirm)
        {
            if (UIManager.Instance == null) return;
            var p = UIManager.Instance.Open<ConfirmPopup>();
            if (p != null) p.Apply(message, onConfirm);
        }

        protected override void OnBuild()
        {
            // 全螢幕暗底（擋住下方點擊、聚焦本彈窗）
            var dim = UIBuilder.SolidPanel(transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
            dim.raycastTarget = true;

            // frame：背板原圖尺寸、置中、等比縮放塞進畫面
            var frameGO = UIBuilder.Create("Frame", transform);
            _frame = UIBuilder.Rect(frameGO);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = Vector2.zero;
            _frame.sizeDelta = new Vector2(BgW, BgH);
            float scale = DisplayHeight / BgH;
            _frame.localScale = new Vector3(scale, scale, 1f);

            // 背板
            var bg = UIBuilder.Image(frameGO.transform, "BG", UIBuilder.LoadSprite("UI/PopupPannel/PopupPanelBG"));
            UIBuilder.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            // 訊息文字（上方內襯）
            _msg = UIBuilder.Text(_frame, "Msg", "", 64, new Color(0.95f, 0.93f, 0.85f), TextAnchor.MiddleCenter);
            Place(UIBuilder.Rect(_msg), MsgCx, MsgCy, MsgW, MsgH);

            // 確定（✓）/ 取消（✗）：LongBtn 當底 + 勾/叉 icon
            BuildIconButton("Ok", OkCx, "UI/PopupPannel/PopupPanelOkIcon", OnYes);
            BuildIconButton("No", NoCx, "UI/PopupPannel/PopupPanelNoIcon", OnNo);
        }

        void BuildIconButton(string name, float cx, string iconRes, UnityEngine.Events.UnityAction onClick)
        {
            Sprite N = UIBuilder.LoadSprite("UI/Common/LongBtn_normal");
            Sprite P = UIBuilder.LoadSprite("UI/Common/LongBtn_pressed");
            var b = UIBuilder.Button(_frame, name, "", onClick, Color.white, N);
            var img = b.GetComponent<Image>();
            img.preserveAspect = true;
            b.targetGraphic = img;                       // 程式建按鈕需手動指（見 PROBLEMS D4）
            b.transition = Selectable.Transition.SpriteSwap;
            var ss = b.spriteState;
            ss.pressedSprite = P; ss.highlightedSprite = N; ss.selectedSprite = N;
            b.spriteState = ss;
            Place((RectTransform)b.transform, cx, BtnY, BtnW, BtnH);

            // 勾/叉 icon 疊在按鈕中央（不擋點擊）
            var icon = UIBuilder.Image(b.transform, "Icon", UIBuilder.LoadSprite(iconRes));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(BtnIconSize, BtnIconSize);
        }

        void Apply(string message, Action onConfirm)
        {
            _onConfirm = onConfirm;
            if (_msg != null) _msg.text = message;
        }

        void OnYes()
        {
            var cb = _onConfirm;          // 先存起來：Close 之後 cb 可能會關掉遊戲
            UIManager.Instance.Close(this);
            cb?.Invoke();
        }

        void OnNo()
        {
            UIManager.Instance.Close(this);
        }

        // 座標映射：錨到 frame 左上角、anchoredPosition=(px,-py)（與 SettingsPanel / InventoryPanel 一致）
        void Place(RectTransform rt, float px, float py, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(px, -py);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
