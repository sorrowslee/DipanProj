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
    /// 姓名牌匾擺在聚光側、顯示說話者姓名；**該句沒填姓名就整片隱藏**（旁白／獨白）。
    /// 立繪則是任一側 sprite=null（留空 / 載不到）那側自動隱藏。
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
        //    580/130 那組（2026-09-13）是 1027，頂端留 53px，但作者實機看覺得「人太小、離太遠、沒魄力」。
        //    現在這組 880/400 算出來是 1057，頂端留 23px。要再調：**縮小人物改 AvatarHeight、整體下移改 AvatarOverlap**。
        //
        // 📐 **放大的正確做法是兩個一起加**：露出在對話框上方的高度 ＝ AvatarHeight − AvatarOverlap，
        //    而它的物理上限只有 1080 − 576.9 ≈ 503（對話框上緣到畫面頂）。所以光加 AvatarHeight 會頂到天花板，
        //    必須同步加 AvatarOverlap 讓人物往下沉——結果就是「人更大、但露出的身體部位更少」＝鏡頭拉近的臉部特寫感。
        //    這一組露出 480、被對話框蓋掉約 45%（腰部以下），是作者要的構圖。
        const float AvatarHeight = 880f;                       // **人物**在畫面上的高度（越大越大隻；圖檔會被連帶放大到超過這個值）
        const float AvatarInnerFromCenter = 140f;              // **人物內側緣**（朝畫面中央那一邊）距**畫面中心**（越小兩人靠越近）
                                                               // ⚠ 2026-09-21 從「距畫面左/右邊 820」改成「距中心 140」：
                                                               //   CanvasScaler 是 match=0.5，UI 座標的畫面寬會隨螢幕比例浮動，
                                                               //   錨在畫面邊會讓立繪在寬螢幕上離置中的對話框越來越遠。
                                                               //   （1920 寬時兩種寫法等價：960 − 820 = 140。）
        const float AvatarOverlap = 400f;                      // **人物底部**沉入對話框多少（**越大越往下＝被對話框蓋住越多、露出越少**；負值＝往上露出更多）
        const float AvatarAspect = 1086f / 1448f;              // 建立時的暫定比例（實際尺寸每次 SetAvatar 依圖重算）

        // ── 立繪邊緣羽化（2026-09-21）──
        // 部分素材的人物一路畫到畫布邊界才被切斷（蟲皇左緣 69%、法夫納左緣 40% 的畫布邊是不透明的），
        // 去背再乾淨也救不回來——邊緣就是一條硬切直線。這裡讓靠近圖檔左右邊界的像素 alpha 漸層淡出，
        // 硬邊化成柔邊融進暗場景。**這是短期補救**，根本解是產圖時要求人物四周留白。
        // ⚠ 對沒切邊的立繪完全無害：它們的邊緣 alpha 本來就是 0，乘上衰減仍是 0。
        // ⚠ 值是「佔圖寬的比例」：0.07 ＝ 1024px 寬的圖，左右各 72px 的漸層。調太大會把貼近邊緣的
        //   手臂／披風也淡掉一截，實機看過再定。
        const float AvatarFeatherX = 0.07f;                    // 左右羽化寬度（0 = 關閉）
        const float AvatarFeatherY = 0f;                       // 上下羽化寬度（0 = 關閉）——
                                                               // 上緣幾乎沒有切邊、下緣被對話框蓋住，預設不處理

        // 非聚光側（沒在說話的人）壓暗：整體調暗、保留原色相（灰色 tint 乘上去＝背光感）。聚光側用純白＝原色。
        static readonly Color SpotlightColor = Color.white;
        static readonly Color DimmedColor = new Color(0.42f, 0.42f, 0.42f, 1f);

        // 左右立繪共用同一份羽化材質（參數相同）。static 快取，Play 模式結束要歸零（見 ResetForPlayMode）。
        static Material _featherMat;

        /// <summary>進 Play 時丟掉羽化材質（已關 Domain Reload；殘留的是上一輪被銷毀的 Material → 立繪會變洋紅）。</summary>
        public static void ResetForPlayMode() { _featherMat = null; }

        /// <summary>取得（必要時建立）立繪的羽化材質。shader 載不到就回 null ＝ 退回 UI 預設材質，只是沒有羽化。</summary>
        static Material FeatherMaterial()
        {
            if (AvatarFeatherX <= 0f && AvatarFeatherY <= 0f) return null;   // 兩軸都關 ＝ 不用自訂材質
            if (_featherMat != null) return _featherMat;

            var sh = Resources.Load<Shader>("Shaders/TalkAvatarFeather");
            if (sh == null)
            {
                Debug.LogWarning("[TalkPanel] 找不到 Resources/Shaders/TalkAvatarFeather，立繪邊緣羽化停用（其餘照常）。");
                return null;
            }
            _featherMat = new Material(sh) { hideFlags = HideFlags.DontSave };
            _featherMat.SetFloat("_FeatherX", AvatarFeatherX);
            _featherMat.SetFloat("_FeatherY", AvatarFeatherY);
            return _featherMat;
        }

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
            // ⚠ 沒填姓名（旁白、獨白、內心話這類「沒有說話者」的句子）→ **整片牌匾隱藏**，
            //    不要留一塊空匾額在那裡。逐句判定，所以同一組對話可以有的句子有牌匾、有的沒有。
            bool hasName = !string.IsNullOrWhiteSpace(l.Name);
            _plate.gameObject.SetActive(hasName);
            if (hasName)
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
        /// 距畫面中心 <see cref="AvatarInnerFromCenter"/>、底部讓<b>人物底緣</b>落在對話框上緣 − <see cref="AvatarOverlap"/>。
        /// 三個量都對「人」，所以美術給多大的畫布、留多少白邊都不會讓人物飄。</para>
        ///
        /// <para><b>外側緣有一條硬上限</b>：人物外側緣不得超出對話框外緣（對話框是置中固定寬，畫面越寬、
        /// 錨在畫面邊的立繪離它越遠）。超出就整個往中央推回去，所以寬體型角色實際上是「外側貼齊對話框邊」，
        /// 窄體型角色維持內側緣對齊、不受影響。</para>
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

            // 以下 x 一律是「相對畫面中心」（anchor 0.5）：往右為正。
            // 對話框外緣因此永遠是 ±halfBox，跟畫面實際多寬無關。
            float contentW = content.x * k;                    // 人物寬（內容框）
            float innerToImgLeft = (canvas.x * 0.5f + rightFromCenter) * k;   // 人物右緣距圖檔左緣
            float halfBox = DisplayWidth * 0.5f;

            float ax;
            if (right)
            {
                // 右側鏡像（localScale.x = −1）：圖內的右緣會出現在畫面的左邊，
                // 所以畫面上「人物內側緣」相對圖檔中心 = −rightFromCenter。
                ax = AvatarInnerFromCenter + rightFromCenter * k + px;

                // 外側緣（畫面右緣）不得超出對話框右緣。
                float outer = ax + (content.x - rightFromCenter) * k;
                if (outer > halfBox) ax -= (outer - halfBox);
            }
            else
            {
                // 左側：pivot (0,0)，ax 量的是「圖檔左緣」相對畫面中心。
                ax = -AvatarInnerFromCenter - innerToImgLeft + px;

                // 外側緣（畫面左緣）不得超出對話框左緣。
                float outer = ax + innerToImgLeft - contentW;
                if (outer < -halfBox) ax += (-halfBox - outer);
            }

            rt.anchoredPosition = new Vector2(ax, imgBottomY);
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

            // 邊緣羽化（見 AvatarFeatherX 的說明）。材質做在 UV 空間，右側立繪的鏡像不影響結果。
            var fm = FeatherMaterial();
            if (fm != null) avatar.material = fm;

            var rt = avatar.rectTransform;
            rt.sizeDelta = new Vector2(AvatarHeight * AvatarAspect, AvatarHeight);

            // ⚠ 兩側都錨在**畫面中心底部**（0.5, 0），不是畫面左右邊：
            //    對話框是置中、固定 1500 寬，立繪若錨在畫面邊，畫面越寬就離對話框越遠
            //    （CanvasScaler match=0.5，UI 座標的畫面寬本來就會浮動）。錨在中心之後，
            //    「對話框邊緣」永遠是 ±DisplayWidth/2，排版與 clamp 都不必知道畫面多寬。
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            if (right)
            {
                // 右側：pivot 水平置中 + 底部（localScale.x=-1 要以水平中軸才是原地翻轉）。
                rt.pivot = new Vector2(0.5f, 0f);
                rt.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                // 左側：pivot 左下，不翻轉（原圖臉朝右＝朝向畫面中央，正好）。
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
