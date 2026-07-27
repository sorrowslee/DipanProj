using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Drama;

namespace Dipan.UI
{
    /// <summary>
    /// 頭像對話面板（劇情 Type=2）。底部一個對話框 + 姓名牌匾（擺在聚光側）+ 對話文字，
    /// 點畫面任意處 / 空白鍵 / Enter 換下一句，最後一句後關閉。模態、暫停遊戲。
    ///
    /// 由 <see cref="DramaTalkController"/> 在玩家觸發 Type=2 劇情點時 <see cref="Show"/>(lines) 開啟，
    /// lines 已由 DramaTalkDatabase 依流水號排好序、並解析好左右立繪 sprite。外觀走真素材
    /// （DramaPanelBG / DramaPanelNameBG），做法與 SettingsPanel / ConfirmPopup 一致（整張背板 + 量測座標）。
    ///
    /// 雙立繪：一句可同時擺左、右兩個立繪（<see cref="DramaTalkData.LeftAvatar"/> / <see cref="DramaTalkData.RightAvatar"/>）。
    /// <see cref="DramaTalkData.SpotlightSide"/> = 說話者那一側：聚光側立繪正常亮、另一側壓暗（保留原色相、純調暗），
    /// 姓名牌匾擺在聚光側、顯示說話者姓名。任一側 sprite=null（留空 / 載不到）那側自動隱藏。
    /// </summary>
    public class TalkPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;    // 半透明黑遮罩（UIManager 共用，鋪在對話框+立繪後方，把場景壓暗）
        // ESC 關掉整段對話＝**開發用**，正式打包不給玩家跳過劇情（見 DevSkip）。
        // 關掉後按 ESC 完全沒反應：UIManager 的 ESC 是「有視窗且允許才關」，不會 fall through 去開設定面板。
        public override bool CloseOnEscape => DevSkip.Allowed;

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

        // 立繪（站姿、排在對話框「後方」＝被對話框蓋住、左立繪錨左下/右立繪錨右下）。原圖比例 1086:1448≈0.75。
        const float AvatarHeight = 660f;                       // 立繪在畫面上的高度（越大越大隻；寬度自動 = 高×比例）
        const float AvatarAspect = 1086f / 1448f;              // 寬 = 高 × 此比例
        const float AvatarSideMargin = 220f;                    // 距畫面左/右邊（越大越往中間靠）
        const float AvatarOverlap = 100f;                      // 立繪底部沉入對話框多少（**越大越往下＝被對話框蓋住越多、露出越少**；負值＝往上露出更多）

        // 非聚光側（沒在說話的人）壓暗：整體調暗、保留原色相（灰色 tint 乘上去＝背光感）。聚光側用純白＝原色。
        static readonly Color SpotlightColor = Color.white;
        static readonly Color DimmedColor = new Color(0.42f, 0.42f, 0.42f, 1f);

        RectTransform _frame;
        Image _plate, _avatarLeft, _avatarRight;
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

            // 立繪：先建＝排在對話框「後方」（被對話框蓋住）。左、右各一，無圖時各自隱藏。位置固定（左立繪錨左下、右立繪錨右下）。
            _avatarLeft = BuildAvatar("AvatarLeft", right: false);
            _avatarRight = BuildAvatar("AvatarRight", right: true);

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

            bool spotRight = l.SpotlightSide == 2;   // 聚光（說話者）在右側

            // 姓名牌匾擺在聚光側、顯示說話者姓名。
            Place(_plate.rectTransform, spotRight ? PlateRightCx : PlateLeftCx, PlateY, PlateW, PlateH);

            // 左、右立繪各自顯示（有圖才顯示）；非聚光側壓暗（保留原色相）。可用 CSV 選填欄微調縮放/位移。
            SetAvatar(_avatarLeft, l.LeftAvatar, dim: spotRight, right: false,
                      scale: l.LeftScale, offX: l.LeftOffsetX, offY: l.LeftOffsetY);
            SetAvatar(_avatarRight, l.RightAvatar, dim: !spotRight, right: true,
                      scale: l.RightScale, offX: l.RightOffsetX, offY: l.RightOffsetY);
        }

        // 設定單一立繪：套 sprite、亮/暗、有圖才啟用；並依該句的縮放/位移調整大小與位置。
        // 大小：高 = AvatarHeight × scale，寬依 **sprite 實際長寬比** 自動算（不同比例的 NPC 立繪不會被硬塞進主角比例的框）。
        // 位置：在標準落點上加 (offX, offY)（+X 往右、+Y 往上；右側立繪雖水平鏡像，位移方向仍以畫面為準）。
        void SetAvatar(Image avatar, Sprite sprite, bool dim, bool right,
                       float scale = 1f, float offX = 0f, float offY = 0f)
        {
            avatar.sprite = sprite;
            avatar.enabled = sprite != null;
            avatar.color = dim ? DimmedColor : SpotlightColor;
            if (sprite == null) return;

            float h = AvatarHeight * Mathf.Max(0.05f, scale);
            float aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : AvatarAspect;
            float w = h * aspect;

            var rt = avatar.rectTransform;
            rt.sizeDelta = new Vector2(w, h);

            float boxTop = BottomMargin + BgH * (DisplayWidth / BgW);
            float bottomY = boxTop - AvatarOverlap + offY;
            rt.anchoredPosition = right
                ? new Vector2(-(AvatarSideMargin + w * 0.5f) + offX, bottomY)
                : new Vector2(AvatarSideMargin + offX, bottomY);
        }

        // 建一個立繪 Image：站姿、排在對話框「後方」（被對話框蓋住），貼畫面左下 / 右下角；底部 = 對話框上緣 - AvatarOverlap。
        // 立繪原圖臉朝右，所以「右側立繪一律水平翻轉」(localScale.x=-1) 讓臉朝向畫面中央 → 與左側對望。
        // 翻轉以立繪水平中軸為準（pivot.x=0.5）原地鏡像、不位移。
        Image BuildAvatar(string name, bool right)
        {
            var avatar = UIBuilder.Image(transform, name, null);
            avatar.preserveAspect = true;
            avatar.raycastTarget = false;
            avatar.enabled = false;

            var rt = avatar.rectTransform;
            float w = AvatarHeight * AvatarAspect;
            rt.sizeDelta = new Vector2(w, AvatarHeight);

            // 對話框上緣（距畫面底）= 底邊距 + 對話框實際顯示高度；立繪底部沉入框內 AvatarOverlap。
            float boxTop = BottomMargin + BgH * (DisplayWidth / BgW);
            float bottomY = boxTop - AvatarOverlap;

            if (right)
            {
                // 右側：錨右下、pivot 水平置中 + 底部。距右邊 AvatarSideMargin（量到立繪外緣）。localScale.x=-1 原地水平翻轉。
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(-(AvatarSideMargin + w * 0.5f), bottomY);
                rt.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                // 左側：錨左下、pivot 左下，不翻轉（原圖臉朝右＝朝向畫面中央，正好）。
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(AvatarSideMargin, bottomY);
                rt.localScale = Vector3.one;
            }
            return avatar;
        }

        /// <summary>開啟時先擋一次冷卻：避免上一段對話的連點慣性直接把第一句（連同立繪）跳掉。</summary>
        protected override void OnOpen()
        {
            BlockInputFor(InputCooldown);
        }

        void Next()
        {
            // 防連點：0.5 秒內不管按幾次都只算一次（見 UIPanel.TryConsumeInput）。
            // 鍵盤與整片點擊鈕都經由這裡，所以節流放這一處就涵蓋兩個入口。
            // 註：ESC 關閉不走這裡、不受節流——那是明確的「我要跳過」意圖。
            if (!TryConsumeInput()) return;

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
            TriggerChain.NotifyDramaClosed();   // 觸發鏈：對話關閉 = 該劇情點動作完成（無待結鏈時無事）
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
