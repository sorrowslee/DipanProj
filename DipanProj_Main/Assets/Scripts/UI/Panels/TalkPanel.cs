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
        // ESC：開發階段一律可關；正式版則「這段對話允許略過時」也可以（與右上角 Skip 同一個開關，見 SkipAvailable）。
        public override bool CloseOnEscape => DevSkip.Allowed || SkipAvailable;

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

        // 立繪（站姿、排在對話框「後方」＝被對話框蓋住、左立繪錨左下/右立繪錨右下）。
        //
        // ⚠ 這三個常數量的都是「**人物**」，不是「圖檔」——排版走 PortraitFit 的不透明內容框
        // （見 SetAvatar 的說明）。素材的留白與畫布比例差很多（內容佔畫布 0.717~1.000、
        // 畫布 1024×1536 之外還有 1122×1402 等），量圖檔會讓人物跟著留白飄。
        // ⚠ 這三個值的上限是**畫面高 1080**（CanvasScaler 參考解析度）：
        //    人物頂端 = (BottomMargin + 對話框顯示高) − AvatarOverlap + AvatarHeight
        //             = 576.9 − AvatarOverlap + AvatarHeight，**超過 1080 頭頂就被切掉**。
        //    660/100 那組（2026-09-13 上午）算出來是 1137 ⇒ 超出 57px，實機看得到角與頭髮被切平。
        //    現在這組是 1027，頂端留 53px 呼吸空間。要再調：**縮小人物改 AvatarHeight、整體下移改 AvatarOverlap**。
        const float AvatarHeight = 580f;                       // **人物**在畫面上的高度（越大越大隻；圖檔會被連帶放大到超過這個值）
        const float AvatarInnerX = 650f;                       // **人物內側緣**（朝畫面中央那一邊）距畫面左/右邊（越大兩人靠越近）
        const float AvatarOverlap = 130f;                      // **人物底部**沉入對話框多少（**越大越往下＝被對話框蓋住越多、露出越少**；負值＝往上露出更多）
        const float AvatarAspect = 1086f / 1448f;              // 建立時的暫定比例（實際尺寸每次 SetAvatar 依圖重算）

        // 非聚光側（沒在說話的人）壓暗：整體調暗、保留原色相（灰色 tint 乘上去＝背光感）。聚光側用純白＝原色。
        static readonly Color SpotlightColor = Color.white;
        static readonly Color DimmedColor = new Color(0.42f, 0.42f, 0.42f, 1f);

        RectTransform _frame;
        Image _plate, _avatarLeft, _avatarRight;
        Text _name, _msg;

        List<DramaTalkData> _lines;
        int _index;
        bool _allowSkip;
        Text _skip;

        /// <summary>
        /// 這段對話現在該不該給「Skip」。兩個條件：
        ///   ① 呼叫端允許（劇情觸發點的「可略過」欄，預設允許；劇情演出裡的 dialogue 步驟則一律不給——
        ///      那時畫面上已經有演出自己的 Skip，兩顆會打架）；
        ///   ② **這個群組不只一句**。只有一句的對話按 Skip 跟按下一句完全一樣，多一顆鈕只是噪音；
        ///   ③ 這個地方給跳（<see cref="DevSkip.SkipAllowedHere"/>：序章整段正式版全程不給跳）。
        /// </summary>
        bool SkipAvailable => _allowSkip && _lines != null && _lines.Count > 1 && DevSkip.SkipAllowedHere;

        /// <summary>開啟對話面板並播放一串對話（lines 須已依流水號排序）。</summary>
        /// <param name="allowSkip">是否顯示右上角 Skip（只有一句時仍不顯示，見 <see cref="SkipAvailable"/>）。</param>
        public static void Show(List<DramaTalkData> lines, bool allowSkip = true)
        {
            if (UIManager.Instance == null || lines == null || lines.Count == 0) return;
            var p = UIManager.Instance.Open<TalkPanel>();
            if (p != null) p.Play(lines, allowSkip);
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

            // 右上角 Skip（全遊戲統一樣式，見 Dipan.UI.SkipButton）。
            // **建在最後＝排在整片「點擊換下一句」鈕之上**，否則點 Skip 只會換下一句。
            _skip = SkipButton.Create(transform, SkipAll);
            _skip.gameObject.SetActive(false);   // 實際要不要顯示由 Play() 依 SkipAvailable 決定
        }

        void Play(List<DramaTalkData> lines, bool allowSkip)
        {
            _lines = lines;
            _index = 0;
            _allowSkip = allowSkip;
            if (_skip != null) _skip.gameObject.SetActive(SkipAvailable);
            ShowCurrent();
        }

        /// <summary>
        /// 略過整段對話：直接關閉面板。**不需要另外接鏈**——`OnClose` 會呼叫
        /// <c>TriggerChain.NotifyDramaClosed()</c>，那正是「這個劇情點的動作完成」的訊號，
        /// 所以行為與「一句一句按到最後一句」完全一致：有 next 就接 next，沒有就結束。
        /// </summary>
        void SkipAll()
        {
            if (!SkipAvailable) return;
            // 與換頁共用防連點：上一段對話的連點慣性剛好落在 Skip 上就整組被跳掉，那是最糟的誤觸。
            if (!TryConsumeInput()) return;
            _index = _lines.Count;          // 標記成已播完（語意清楚，也避免 Update 再進 Next）
            UIManager.Instance.Close(this);
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
            SetAvatar(_avatarLeft, l.LeftAvatar, l.LeftFit, dim: spotRight, right: false,
                      scale: l.LeftScale, offX: l.LeftOffsetX, offY: l.LeftOffsetY);
            SetAvatar(_avatarRight, l.RightAvatar, l.RightFit, dim: !spotRight, right: true,
                      scale: l.RightScale, offX: l.RightOffsetX, offY: l.RightOffsetY);
        }

        /// <summary>
        /// 設定單一立繪：套 sprite、亮/暗、有圖才啟用，並把「**人物**」對齊到固定落點。
        ///
        /// <para><b>對齊的是不透明內容框、不是圖檔外框</b>（<see cref="PortraitFit"/>）：
        /// 縮放讓<b>人物高度</b> = <see cref="AvatarHeight"/>、水平讓<b>人物內側緣</b>（朝畫面中央那一邊）
        /// 落在 <see cref="AvatarInnerX"/>、底部讓<b>人物底緣</b>落在對話框上緣 − <see cref="AvatarOverlap"/>。
        /// 三個量都對「人」，所以美術給多大的畫布、留多少白邊都不會讓人物飄。</para>
        ///
        /// <para><b>右側立繪是鏡像的</b>（localScale.x = −1，讓臉朝向畫面中央），鏡像後原圖的<b>右</b>緣會變成
        /// 畫面上的<b>左</b>緣——正好也是它的內側緣，所以左右兩側都用 <see cref="PortraitFit.ContentRightFromCenter"/>
        /// 這一個值對齊，不必分兩套。</para>
        ///
        /// <para>內容框掃不到時（圖載不到 / catalog 沒這筆）自動退化成「整張圖 = 內容」，
        /// 也就是舊的「量圖檔」行為，不會不顯示。</para>
        ///
        /// <para>縮放/位移有兩層、<b>相乘與相加</b>：<c>fit.imgScale/imgOffset</c> 是<b>那張圖</b>的固定微調
        /// （<see cref="PortraitTable"/>，設定一次），參數 <paramref name="scale"/>/<paramref name="offX"/>/<paramref name="offY"/>
        /// 是<b>那一句</b>的特例（DramaTalkTable 選填欄）。位移一律以畫面為準（+X 右、+Y 上），右側鏡像不影響方向。</para>
        /// </summary>
        void SetAvatar(Image avatar, Sprite sprite, PortraitFit fit, bool dim, bool right,
                       float scale = 1f, float offX = 0f, float offY = 0f)
        {
            avatar.sprite = sprite;
            avatar.enabled = sprite != null;
            avatar.color = dim ? DimmedColor : SpotlightColor;
            if (sprite == null) return;

            // 內容框（掃不到就當「整張圖都是內容」＝舊行為）。單位隨來源，下面只用比值。
            Vector2 canvas = fit.ok ? fit.canvas : new Vector2(sprite.rect.width, sprite.rect.height);
            Vector2 content = fit.ok ? fit.content : canvas;
            float rightFromCenter = fit.ok ? fit.ContentRightFromCenter : canvas.x * 0.5f;
            float bottomFromCenter = fit.ok ? fit.ContentBottomFromCenter : -canvas.y * 0.5f;
            if (canvas.x <= 0f || canvas.y <= 0f || content.y <= 0f) return;

            // k = 讓「人物高度」＝ AvatarHeight 的縮放；圖檔本身會被放大到 canvas * k（留白越多、圖看起來越大）。
            // imgScale 用 >0 判斷而不是 Max：PortraitFit 是 struct，萬一拿到 default(struct)（imgScale=0）
            // 用 Max(0.05f, 0) 會把人物縮成 5% 這種「不像 bug 的 bug」，寧可當 1（＝沒微調）。
            float imgScale = fit.imgScale > 0f ? fit.imgScale : 1f;
            float k = AvatarHeight * Mathf.Max(0.05f, scale) * imgScale / content.y;

            var rt = avatar.rectTransform;
            rt.sizeDelta = new Vector2(canvas.x * k, canvas.y * k);

            float px = offX + fit.imgOffset.x;
            float py = offY + fit.imgOffset.y;

            float boxTop = BottomMargin + BgH * (DisplayWidth / BgW);
            float bottomY = boxTop - AvatarOverlap + py;                      // 人物底緣的目標 y

            // 縱向（兩側相同）：pivot.y = 0，所以 anchoredPosition.y 量的是「圖檔底緣」，
            // 往下扣掉「人物底緣到圖檔底緣」那段留白。
            float imgBottomY = bottomY - (canvas.y * 0.5f + bottomFromCenter) * k;

            if (right)
            {
                // 右側：anchor 右下、pivot (0.5, 0)、localScale.x = −1（以水平中軸原地鏡像）。
                // anchoredPosition.x 量的是「圖檔中心距畫面右邊」（負值），
                // 鏡像後人物內側緣距畫面右邊 = 圖檔中心距右邊 + 人物右緣距圖檔中心。
                rt.anchoredPosition = new Vector2(rightFromCenter * k - AvatarInnerX + px, imgBottomY);
            }
            else
            {
                // 左側：anchor 左下、pivot (0, 0)，anchoredPosition.x 量的是「圖檔左緣距畫面左邊」。
                rt.anchoredPosition = new Vector2(AvatarInnerX - (canvas.x * 0.5f + rightFromCenter) * k + px, imgBottomY);
            }
        }

        // 建一個立繪 Image：站姿、排在對話框「後方」（被對話框蓋住），貼畫面左下 / 右下角。
        // 立繪原圖臉朝右，所以「右側立繪一律水平翻轉」(localScale.x=-1) 讓臉朝向畫面中央 → 與左側對望。
        // 翻轉以立繪水平中軸為準（pivot.x=0.5）原地鏡像、不位移。
        //
        // 這裡只定 anchor / pivot / 翻轉這些「不會變的」；**實際尺寸與位置每次 SetAvatar 依該張圖的內容框重算**
        // （建立當下 sprite 還是 null、enabled=false，看不到）。sizeDelta 只是個不會被用到的暫定值。
        Image BuildAvatar(string name, bool right)
        {
            var avatar = UIBuilder.Image(transform, name, null);
            avatar.preserveAspect = true;
            avatar.raycastTarget = false;
            avatar.enabled = false;

            var rt = avatar.rectTransform;
            rt.sizeDelta = new Vector2(AvatarHeight * AvatarAspect, AvatarHeight);

            if (right)
            {
                // 右側：錨右下、pivot 水平置中 + 底部（localScale.x=-1 要以水平中軸才是原地翻轉）。
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                // 左側：錨左下、pivot 左下，不翻轉（原圖臉朝右＝朝向畫面中央，正好）。
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
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
            if (_skip != null) _skip.gameObject.SetActive(false);
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
