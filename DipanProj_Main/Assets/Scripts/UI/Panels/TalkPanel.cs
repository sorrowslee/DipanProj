using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Drama;

namespace Dipan.UI
{
    /// <summary>
    /// 頭像對話面板（劇情 Type=2）。底部一個對話框 + 姓名牌匾（依說話人左右側擺放）+ 對話文字，
    /// 點畫面任意處 / 空白鍵 / Enter 換下一句，最後一句後關閉。模態、暫停遊戲。
    ///
    /// 由 <see cref="DramaTalkController"/> 在玩家觸發 Type=2 劇情點時 <see cref="Show"/>(lines) 開啟，
    /// lines 已由 DramaTalkDatabase 依流水號排好序。外觀走真素材（DramaPanelBG / DramaPanelNameBG），
    /// 做法與 SettingsPanel / ConfirmPopup 一致（整張背板 + 量測座標）。
    ///
    /// 頭像：目前 <see cref="DramaTalkData.Avatar"/> 尚未載入（=null），頭像圖會自動隱藏；
    /// 等頭像圖與載入路徑定案後，只要讓 data.Avatar 有值即可自動顯示（見 readme/TODO.md、DRAMA.md）。
    /// </summary>
    public class TalkPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;    // 半透明黑遮罩（UIManager 共用，鋪在對話框+立繪後方，把場景壓暗）
        public override bool CloseOnEscape => true;

        // ── 對話框背板原圖尺寸 / 顯示大小 ──
        const float BgW = 2246f, BgH = 828f;
        const float DisplayWidth = 1500f;   // 對話框在畫面上的寬度（CanvasScaler 參考單位）
        const float BottomMargin = 24f;     // 對話框距畫面底部

        // ── 版面座標（對話框背板原圖像素，左上為原點、填中心點；實機微調這裡）──
        const float MsgCx = 1180f, MsgCy = 460f, MsgW = 1560f, MsgH = 380f;   // 對話文字區
        const int MsgFont = 52;
        // 姓名牌匾（依 Side 擺左 / 右；y 在對話框上緣）
        const float PlateW = 540f, PlateH = 216f, PlateY = 66f;
        const float PlateLeftCx = 872f, PlateRightCx = BgW - 872f;

        // 立繪（站姿、排在對話框「後方」＝被對話框蓋住、依 Side 擺左/右；錨在畫面左/右下角）。原圖比例 1086:1448≈0.75。
        const float AvatarHeight = 660f;                       // 立繪在畫面上的高度（越大越大隻；寬度自動 = 高×比例）
        const float AvatarAspect = 1086f / 1448f;              // 寬 = 高 × 此比例
        const float AvatarSideMargin = 220f;                    // 距畫面左/右邊（越大越往中間靠）
        const float AvatarOverlap = 100f;                      // 立繪底部沉入對話框多少（**越大越往下＝被對話框蓋住越多、露出越少**；負值＝往上露出更多）

        RectTransform _frame;
        Image _plate, _avatar;
        Text _name, _msg;

        List<DramaTalkData> _lines;
        int _index;

        /// <summary>開啟對話面板並播放一串對話（lines 須已依流水號排序）。</summary>
        public static void Show(List<DramaTalkData> lines)
        {
            if (UIManager.Instance == null || lines == null || lines.Count == 0) return;
            var p = UIManager.Instance.Open<TalkPanel>();
            if (p != null) p.Play(lines);
        }

        protected override void OnBuild()
        {
            // 全螢幕透明「點擊換下一句」鈕（鋪最底層；視覺元件都 raycastTarget=false，點任意處都能換頁）
            var click = UIBuilder.Button(transform, "ClickToAdvance", null, Next, new Color(0, 0, 0, 0));
            UIBuilder.Stretch((RectTransform)click.transform);
            click.targetGraphic = click.GetComponent<Image>();   // 程式建按鈕需手動指（見 PROBLEMS D4）

            // 立繪：先建＝排在對話框「後方」（被對話框蓋住）。站在說話人那一側、無圖時隱藏。
            _avatar = UIBuilder.Image(transform, "Avatar", null);
            _avatar.preserveAspect = true;
            _avatar.raycastTarget = false;
            _avatar.enabled = false;

            // frame：對話框原圖尺寸、底部置中、等比縮放
            var frameGO = UIBuilder.Create("Frame", transform);
            _frame = UIBuilder.Rect(frameGO);
            _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0f);
            _frame.pivot = new Vector2(0.5f, 0f);
            _frame.anchoredPosition = new Vector2(0f, BottomMargin);
            _frame.sizeDelta = new Vector2(BgW, BgH);
            float scale = DisplayWidth / BgW;
            _frame.localScale = new Vector3(scale, scale, 1f);

            // 對話框背板
            var bg = UIBuilder.Image(frameGO.transform, "BG", UIBuilder.LoadSprite("UI/DramaPanel/DramaPanelBG"));
            UIBuilder.Stretch(bg.rectTransform);
            bg.raycastTarget = false;

            // 對話文字
            _msg = UIBuilder.Text(_frame, "Msg", "", MsgFont, new Color(0.95f, 0.93f, 0.85f), TextAnchor.UpperLeft);
            _msg.raycastTarget = false;
            Place(UIBuilder.Rect(_msg), MsgCx, MsgCy, MsgW, MsgH);

            // 姓名牌匾 + 姓名文字（牌匾整片置於對話框上緣，依 Side 擺左/右）
            _plate = UIBuilder.Image(_frame, "NamePlate", UIBuilder.LoadSprite("UI/DramaPanel/DramaPanelNameBG"));
            _plate.preserveAspect = true;
            _plate.raycastTarget = false;
            _name = UIBuilder.Text(_plate.transform, "Name", "", 64, new Color(1f, 0.86f, 0.5f), TextAnchor.MiddleCenter);
            _name.fontStyle = FontStyle.Bold;
            _name.raycastTarget = false;
            var nrt = _name.rectTransform;       // 對齊牌匾的深色匾額區（避開右側流蘇）
            nrt.anchorMin = new Vector2(0.17f, 0.26f);
            nrt.anchorMax = new Vector2(0.83f, 0.83f);
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;
        }

        void Play(List<DramaTalkData> lines)
        {
            _lines = lines;
            _index = 0;
            ShowCurrent();
        }

        void ShowCurrent()
        {
            if (_lines == null || _index < 0 || _index >= _lines.Count) { UIManager.Instance.Close(this); return; }
            var l = _lines[_index];

            _msg.text = l.Text ?? "";
            _name.text = l.Name ?? "";

            bool right = l.Side == 2;   // 立繪在右側

            // 姓名牌匾「跟著立繪同側」：立繪左→牌匾左、立繪右→牌匾右。
            Place(_plate.rectTransform, right ? PlateRightCx : PlateLeftCx, PlateY, PlateW, PlateH);

            // 立繪左 / 右（有圖才顯示）
            _avatar.sprite = l.Avatar;
            _avatar.enabled = l.Avatar != null;
            SetAvatarSide(right);
        }

        // 立繪：站姿、貼畫面左/右邊；底部 = 對話框上緣 - AvatarOverlap。排在對話框後方，越往下＝越被對話框蓋住。
        void SetAvatarSide(bool right)
        {
            var rt = _avatar.rectTransform;
            rt.sizeDelta = new Vector2(AvatarHeight * AvatarAspect, AvatarHeight);

            // 對話框上緣（距畫面底）= 底邊距 + 對話框實際顯示高度；立繪底部沉入框內 AvatarOverlap。
            float boxTop = BottomMargin + BgH * (DisplayWidth / BgW);
            float bottomY = boxTop - AvatarOverlap;

            Vector2 corner = right ? new Vector2(1f, 0f) : new Vector2(0f, 0f);   // 錨在右下 / 左下角
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = corner;
            rt.anchoredPosition = new Vector2(right ? -AvatarSideMargin : AvatarSideMargin, bottomY);
        }

        void Next()
        {
            _index++;
            ShowCurrent();   // 超過最後一句會自動關閉
        }

        void Update()
        {
            if (!IsOpen) return;
            // 空白鍵 / Enter 換下一句（滑鼠點擊走全螢幕鈕）
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Next();
        }

        protected override void OnClose()
        {
            _lines = null;
        }

        // 座標映射：錨到 frame 左上角、anchoredPosition=(px,-py)（與 SettingsPanel / ConfirmPopup 一致）
        void Place(RectTransform rt, float px, float py, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(px, -py);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
