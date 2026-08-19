using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Save;
using Dipan.Flow;
using Dipan.Gacha;
using Dipan.Inventory;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// 三欄存讀檔畫面（Window 層、全螢幕）。每欄 = 一條獨立進度線（一個角色）。
    ///
    /// 版面＝一張整圖底 <c>SelectSavePanel_Bg</c>（1672×941，就是滿版背景）＋在上面疊三張卡片，
    /// 座標全部寫在「底圖原生像素空間」，整個 frame 等比縮放塞滿畫面——與 ForgingPanel / InventoryPanel 同一套作法。
    /// 卡片外框（含頂端「欄位」紅底牌與背後的圓形佛像浮雕）都畫在 <c>SelectSavePanel_Frame</c> 這張圖裡，
    /// 程式只負責把文字與互動元件疊上去。
    ///
    /// 兩種狀態：
    /// - **空欄**：卡片中央「空欄位」＋一顆「新建遊戲」。
    /// - **有檔**：左邊角色 idle 第一幀＋腳下圓台(<c>ActorBase</c>)，
    ///   右邊「N 周目」＋該角色武器欄裝備的武器 icon；下方「進入遊戲」「刪除角色」兩顆鈕。
    ///
    /// 角色外型：讀該欄存檔的周目旗標「血統」→ <see cref="BloodlineTable"/> 的 SpriteFolder →
    /// <see cref="PlayerSpriteLibrary"/> 取 <c>&lt;血統&gt;/idle</c> 的第一幀；沒喝過血統藥劑就是 Base。
    /// 圖用不透明像素邊界框正規化，所以不同血統的留白差異不會讓角色忽大忽小、腳也一定踩在圓台上。
    ///
    /// ⚠ 這裡刻意**不載入**存檔到遊戲裡——只用 <see cref="SaveSystem.LoadCharacter"/> 偷看一眼拿外型與武器，
    /// 真正的載入仍然由玩家按「進入遊戲」時走 <see cref="GameFlowManager.ContinueGame"/>。
    ///
    /// 座標為量測值（量自示意圖），實機若偏移微調本檔上方常數即可。見 readme/TITLE_AND_SAVE_UI.md。
    /// </summary>
    public class SaveSlotPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool CloseOnEscape => true;      // ESC = 返回標題（露出底下的 TitlePanel）
        public override bool ShowBackdrop => false;

        const string Dir = "UI/SelectSavePanel/";
        const string CommonDir = "UI/Common/";
        const string TitleFontPath = "Fonts/Bakudai/Bakudai-Bold";   // 毛筆字（同 GachaPanel / ForgingPanel）

        // ── 語言表 id（LanguageTable.csv 的 5001–5099「選擇存檔」段）──
        const int TxtTitle = 5001, TxtSlotHead = 5002, TxtEmpty = 5003, TxtNewGame = 5004;
        const int TxtEnterGame = 5005, TxtDeleteChar = 5006, TxtDeleteAsk = 5007;
        const int TxtCycleFmt = 5008, TxtCorrupt = 5009;

        // ───────── 底圖原生座標（量自示意圖，1672×941；左上為原點、y 向下）─────────
        const float BgW = 1672f, BgH = 941f;

        const float TitleCx = 836f, TitleCy = 176f;
        const int TitleFont = 38;

        // 三張卡片：中央那張正好在畫面中線，左右各差一個 CardPitch。
        // CardW/CardH＝Frame 圖「內容邊界框」在畫面上的大小（外框的裝飾角會自然落在這個範圍內）。
        const float CardCy = 499f, CardPitch = 440f;
        const float CardW = 426f, CardH = 575f;

        // ── 卡片內元件（相對卡片中心的偏移；x 右為正、y 下為正）──
        const float HeadDy = -235f;                       // 「欄位 N」（紅底牌畫在 Frame 裡，這裡只放字）
        const int HeadFont = 30;

        const float EmptyDy = -26f;                       // 「空欄位」
        const int EmptyFont = 40;

        // 角色區（左半邊）。角色直接站在卡片框自帶的圓形浮雕前，不另外鋪底板。
        const float ActorBaseDx = -95f, ActorBaseDy = 136f, ActorBaseW = 160f;  // 腳下圓台（左緣要離卡片內框線 ≥20）
        const float ActorDx = -102f, ActorFeetDy = 130f, ActorH = 200f;       // 角色（ActorH＝不透明內容的高度）
        const bool ActorFlipX = false;                    // 素材是 idle_right；要讓角色面向另一邊就改 true

        // 資訊區（右半邊）
        const float CycleDx = 112f, CycleDy = -83f;       // 「一周目」
        const int CycleFont = 30;
        const float WeaponDx = 112f, WeaponDy = 34f, WeaponH = 130f;   // 武器 icon（示意圖約 160，這裡收到 130）

        // 按鈕
        // 卡片內框線在 ±195（相對卡片中心）。按鈕外緣要留 ~20 的內縮，否則尖角會壓在框線上。
        const float BtnDy = 199f;
        const float BtnSingleW = 300f;                    // 空欄：一顆「新建遊戲」置中（外緣 ±150，很寬鬆）
        const float BtnPairW = 170f, BtnPairDx = 92f;     // 有檔：兩顆並排（外緣 ±177、兩顆間隔 14）
        const int BtnFontSingle = 34, BtnFontPair = 24;

        // 角色圖的來源 PPU：PlayerSpriteLibrary 以 tileSize=1 取幀時就是這個值（見 MapSpriteLoader.TileNativePx）。
        const float PpuBase = 256f;
        const string DefaultBloodline = "Base";

        static readonly Color TextGold = new Color(0.94f, 0.87f, 0.70f);
        static readonly Color TextDim = new Color(0.62f, 0.58f, 0.52f);
        static readonly Color TextWarn = new Color(0.86f, 0.42f, 0.36f);

        RectTransform _frame;
        readonly List<GameObject> _cards = new List<GameObject>();

        // ───────────────────────── 建版面 ─────────────────────────

        protected override void OnBuild()
        {
            // 畫面比例不是 16:9 時，frame 之外會露出來 → 先鋪一層深色底，避免看到透明。
            var pad = UIBuilder.SolidPanel(transform, "Pad", new Color(0.035f, 0.03f, 0.04f, 1f));
            pad.raycastTarget = true;

            var frameGO = UIBuilder.Create("Frame", transform);
            _frame = UIBuilder.Rect(frameGO);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = Vector2.zero;
            _frame.sizeDelta = new Vector2(BgW, BgH);
            ApplyScale();

            // 底圖（整張＝面板畫布，所以直接拉伸貼齊 frame）
            var bg = UIBuilder.Image(frameGO.transform, "BG", LoadArt(ArtBg));
            UIBuilder.Stretch(bg.rectTransform);
            bg.raycastTarget = true;   // 吃掉空白處點擊，不穿到底下的標題畫面

            var title = MakeText(_frame, "Header", Spaced(Txt(TxtTitle, "選擇存檔")), TitleFont, TextGold);
            Place(title.rectTransform, new Vector2(TitleCx, TitleCy), 900f, 100f);
        }

        protected override void OnOpen()
        {
            ApplyScale();     // 解析度可能在上次開啟後變了
            Refresh();
        }

        /// <summary>把 1672×941 的版面等比放大到蓋滿整個畫面（cover）。</summary>
        void ApplyScale()
        {
            if (_frame == null) return;
            float w = 1920f, h = 1080f;
            if (_frame.parent is RectTransform p && p.rect.width > 1f && p.rect.height > 1f)
            { w = p.rect.width; h = p.rect.height; }
            float s = Mathf.Max(w / BgW, h / BgH);
            _frame.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>依 roster 重畫三張卡片（新建/刪除後呼叫）。</summary>
        public void Refresh()
        {
            for (int i = 0; i < _cards.Count; i++) if (_cards[i] != null) Destroy(_cards[i]);
            _cards.Clear();

            int n = SaveConstants.SlotCount;
            float startCx = BgW * 0.5f - CardPitch * (n - 1) * 0.5f;
            for (int i = 0; i < n; i++)
                BuildCard(i, new Vector2(startCx + i * CardPitch, CardCy));
        }

        void BuildCard(int slot, Vector2 center)
        {
            // 每張卡片一個「跟 frame 一樣大」的容器，這樣裡面的元件仍可以用底圖絕對座標擺，
            // 重畫時整包 Destroy 就好。
            var cardGO = UIBuilder.Create($"Card{slot}", _frame);
            _cards.Add(cardGO);
            UIBuilder.Stretch(UIBuilder.Rect(cardGO));
            var card = cardGO.transform;

            var frame = MakeArt(card, "Frame", ArtFrame);
            PlaceArt(frame, ArtFrame, CardW, center, CardH);

            var head = MakeText(card, "SlotHead",
                Spaced($"{Txt(TxtSlotHead, "欄位")} {slot + 1}"), HeadFont, TextGold);
            Place(head.rectTransform, center + new Vector2(0f, HeadDy), CardW - 90f, 60f);

            var prof = SaveManager.Instance != null ? SaveManager.Instance.GetSlotProfile(slot) : null;

            if (prof == null) BuildEmptyCard(card, slot, center);
            else BuildFilledCard(card, slot, center, prof);
        }

        // ── 空欄 ──
        void BuildEmptyCard(Transform card, int slot, Vector2 center)
        {
            var empty = MakeText(card, "Empty", Spaced(Txt(TxtEmpty, "空欄位")), EmptyFont, TextDim);
            Place(empty.rectTransform, center + new Vector2(0f, EmptyDy), CardW - 80f, 80f);

            MakeButton(card, "NewGameBtn", Spaced(Txt(TxtNewGame, "新建遊戲")), BtnFontSingle,
                       center + new Vector2(0f, BtnDy), BtnSingleW, () => DoNewGame(slot));
        }

        // ── 有檔 ──
        void BuildFilledCard(Transform card, int slot, Vector2 center, CharacterProfile prof)
        {
            var save = prof.corrupt ? null : PeekSave(prof);

            if (save != null)
            {
                BuildActor(card, center, BloodlineFolderOf(save));
                BuildCycleText(card, center, Math.Max(1, prof.generation));
                BuildWeaponIcon(card, center, EquippedWeaponItemId(save));
            }
            else
            {
                // 讀不到內容（損毀或檔案不見）：不畫角色，只留一行警告與「刪除角色」。
                var bad = MakeText(card, "Corrupt", Spaced(Txt(TxtCorrupt, "存檔損毀")), CycleFont, TextWarn);
                Place(bad.rectTransform, center + new Vector2(0f, EmptyDy), CardW - 80f, 80f);

                MakeButton(card, "DeleteBtn", Spaced(Txt(TxtDeleteChar, "刪除角色")), BtnFontSingle,
                           center + new Vector2(0f, BtnDy), BtnSingleW, () => AskDelete(slot));
                return;
            }

            MakeButton(card, "EnterBtn", Spaced(Txt(TxtEnterGame, "進入遊戲")), BtnFontPair,
                       center + new Vector2(-BtnPairDx, BtnDy), BtnPairW,
                       () => { if (GameFlowManager.Instance != null) GameFlowManager.Instance.ContinueGame(slot); });

            MakeButton(card, "DeleteBtn", Spaced(Txt(TxtDeleteChar, "刪除角色")), BtnFontPair,
                       center + new Vector2(BtnPairDx, BtnDy), BtnPairW, () => AskDelete(slot));
        }

        void AskDelete(int slot)
        {
            ConfirmPopup.Show(Txt(TxtDeleteAsk, "確定要刪除這個角色嗎？刪除後無法復原。"), () =>
            {
                if (GameFlowManager.Instance != null) GameFlowManager.Instance.DeleteSlotForTest(slot);
                else if (SaveManager.Instance != null) SaveManager.Instance.DeleteSlot(slot);
                Refresh();
            });
        }

        void DoNewGame(int slot)
        {
            // 名字先用預設（正式建名輸入框之後再補，見 readme/TODO.md）。
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.StartNewGame(slot, $"存檔{slot + 1}");
        }

        // ───────────────────────── 卡片內容 ─────────────────────────

        /// <summary>圓台 → 角色（由後往前疊；角色的腳踩在圓台上緣附近）。</summary>
        void BuildActor(Transform card, Vector2 center, string bloodline)
        {
            var sp = ActorIdleFrame(bloodline, out Vector2 visSize, out Vector2 visOffset);

            var baseImg = MakeArt(card, "ActorBase", ArtActorBase);
            PlaceArt(baseImg, ArtActorBase, ActorBaseW, center + new Vector2(ActorBaseDx, ActorBaseDy));

            if (sp == null) return;

            var img = UIBuilder.Image(card, "Actor", sp, Color.white);
            img.raycastTarget = false;
            img.preserveAspect = false;   // 尺寸由下面精算，不要讓 preserveAspect 再插手

            float fullW = sp.rect.width, fullH = sp.rect.height;
            float targetCx = center.x + ActorDx;
            float feetY = center.y + ActorFeetDy;
            float visW = visSize.x * PpuBase, visH = visSize.y * PpuBase;

            if (fullW > 1f && fullH > 1f && visW > 1f && visH > 1f)
            {
                // 用「不透明內容」而不是整張圖來對齊：不同血統的留白不一樣，直接用整張圖會忽大忽小、腳也踩不準。
                float k = ActorH / visH;
                float dx = visOffset.x * PpuBase * k;
                float dy = visOffset.y * PpuBase * k;          // visOffset 是世界座標（y 向上）
                float visCenterY = feetY - ActorH * 0.5f;      // 版面座標 y 向下
                Place(img.rectTransform,
                      new Vector2(targetCx - (ActorFlipX ? -dx : dx), visCenterY + dy),
                      fullW * k, fullH * k);
            }
            else
            {
                // 量不到邊界框（圖不可讀等）→ 退回「整張圖底部貼齊腳底」，大小抓個保守值。
                float h = ActorH * 1.25f;
                float w = (fullH > 1f) ? h * (fullW / fullH) : h;
                Place(img.rectTransform, new Vector2(targetCx, feetY - h * 0.5f), w, h);
            }

            if (ActorFlipX)
            {
                var s = img.rectTransform.localScale;
                img.rectTransform.localScale = new Vector3(-Mathf.Abs(s.x), s.y, s.z);
            }
        }

        void BuildCycleText(Transform card, Vector2 center, int generation)
        {
            string raw = Txt(TxtCycleFmt, "{0}周目").Replace("{0}", CjkNumber(generation));
            var t = MakeText(card, "Cycle", Spaced(raw), CycleFont, TextGold);
            Place(t.rectTransform, center + new Vector2(CycleDx, CycleDy), CardW * 0.55f, 60f);
        }

        void BuildWeaponIcon(Transform card, Vector2 center, int weaponItemId)
        {
            if (weaponItemId <= 0) return;
            var inv = InventorySystem.Instance;
            var data = inv != null ? inv.GetData(weaponItemId) : null;
            if (data == null || data.Icon == null) return;

            var img = UIBuilder.Image(card, "Weapon", data.Icon, Color.white);
            img.raycastTarget = false;
            img.preserveAspect = true;    // icon 一律 256×256 正方，這裡只是保險
            Place(img.rectTransform, center + new Vector2(WeaponDx, WeaponDy), WeaponH, WeaponH);
        }

        // ───────────────────────── 讀存檔（只偷看，不載入）─────────────────────────

        /// <summary>直接從磁碟讀某個角色的完整存檔，只為了拿外型與武器；不會改變目前的活躍角色。</summary>
        static CharacterSave PeekSave(CharacterProfile prof)
        {
            if (prof == null || string.IsNullOrEmpty(prof.characterId)) return null;
            try
            {
                if (SaveSystem.LoadCharacter(prof.characterId, out var save, out _)) return save;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSlotPanel] 讀取角色 {prof.characterId} 的存檔失敗：{e.Message}");
            }
            return null;
        }

        /// <summary>這個角色本世的血統資料夾；沒喝過血統藥劑（或血統表載不到）就是 Base。</summary>
        static string BloodlineFolderOf(CharacterSave save)
        {
            var flags = save != null && save.progress != null ? save.progress.flags : null;
            if (flags != null
                && flags.TryGetValue(GachaConstants.BloodlineFlagKey, out string v)
                && int.TryParse(v, out int id) && id > 0)
            {
                var def = BloodlineTable.Get(id);
                if (def != null && !string.IsNullOrEmpty(def.SpriteFolder)) return def.SpriteFolder;
            }
            return DefaultBloodline;
        }

        /// <summary>這個角色武器欄裝備的道具 id（0＝空手）。</summary>
        static int EquippedWeaponItemId(CharacterSave save)
        {
            var eq = save != null && save.inventory != null ? save.inventory.equipment : null;
            if (eq != null && eq.TryGetValue(EquipSlot.Weapon.ToString(), out int id) && id > 0) return id;
            return 0;
        }

        /// <summary>取某血統 idle 的第一幀，順便回傳它的不透明內容邊界框（世界單位 @ PPU 256）。</summary>
        static Sprite ActorIdleFrame(string bloodline, out Vector2 visSize, out Vector2 visOffset)
        {
            visSize = default; visOffset = default;
            try
            {
                var lib = PlayerSpriteLibrary.Instance;
                if (lib == null) return null;

                string folder = bloodline;
                if (!lib.Has(folder, "idle"))
                {
                    if (!string.Equals(folder, DefaultBloodline, StringComparison.OrdinalIgnoreCase))
                        Debug.LogWarning($"[SaveSlotPanel] 血統「{folder}」沒有 idle 圖，改用 {DefaultBloodline}。");
                    folder = DefaultBloodline;
                }

                var frames = lib.GetFrames(folder, "idle", 1f);
                if (frames == null || frames.Length == 0 || frames[0] == null) return null;

                lib.TryGetVisibleBox(folder, "idle", out visSize, out visOffset);
                return frames[0];
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSlotPanel] 取角色 idle 圖失敗：{e.Message}");
                return null;
            }
        }

        // ───────────────────────── 小工具 ─────────────────────────

        /// <summary>語言表取字；表還沒就緒（回傳 [cn:id] 佔位）時退回硬寫的中文，標題畫面不會變成一排編號。</summary>
        static string Txt(int id, string fallback)
        {
            string s = null;
            try { s = Language.GetText(id); } catch { }
            if (string.IsNullOrEmpty(s) || s.StartsWith("[")) return fallback;
            return s;
        }

        /// <summary>中日韓文字之間插一個空格（本作 UI 的既有排版習慣）。含非中文字元就原樣回傳，免得英文被拆散。</summary>
        static string Spaced(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length * 2);
            foreach (char c in s)
            {
                if (c == ' ') continue;
                if (c < 0x2E80) return s;      // 有英數/符號就不套（"Select Save"、"Cycle 1"）
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>1→一、11→十一、23→二十三。超出範圍就用阿拉伯數字（顯示不會壞，只是不夠典雅）。</summary>
        static string CjkNumber(int n)
        {
            if (n < 1 || n > 99) return n.ToString();
            const string digits = "零一二三四五六七八九";
            if (n < 10) return digits[n].ToString();
            if (n == 10) return "十";
            int t = n / 10, o = n % 10;
            string head = (t == 1) ? "十" : digits[t] + "十";
            return o == 0 ? head : head + digits[o];
        }

        // ───────────────────────── 版面座標映射 ─────────────────────────

        /// <summary>錨到 frame 左上角、以底圖像素座標（左上原點、y 向下）擺一個中心點（同 ForgingPanel / InventoryPanel）。</summary>
        static void Place(RectTransform rt, Vector2 px, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(px.x, -px.y);
            rt.sizeDelta = new Vector2(w, h);
        }

        Text MakeText(Transform parent, string name, string content, int fontSize, Color color)
        {
            var t = UIBuilder.Text(parent, name, content, fontSize, color, TextAnchor.MiddleCenter);
            var f = UIBuilder.LoadFont(TitleFontPath);
            if (f != null) t.font = f;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>一顆「長條底板 ＋ 文字」的按鈕。命中區是一張全透明 Image，底板與文字是它的子物件（同 ForgingPanel）。</summary>
        Button MakeButton(Transform parent, string name, string label, int fontSize,
                          Vector2 centerPx, float width, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIBuilder.Button(parent, name, "", onClick, new Color(1f, 1f, 1f, 0f));
            var hit = b.GetComponent<Image>();
            hit.raycastTarget = true;                        // 全透明仍接得到點擊（uGUI 不看 alpha）
            b.targetGraphic = hit;                           // 程式建鈕必須手動指（見 PROBLEMS D4）

            // SelectSavePanel_Btn 只有一張圖（沒有按下版），所以用 ColorTint 做回饋（同 ForgingPanel 的關閉鈕）。
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(1f, 0.85f, 0.5f, 0.16f);
            cb.pressedColor = new Color(1f, 0.75f, 0.35f, 0.30f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;

            float h = width / ArtBtn.Aspect;
            Place((RectTransform)b.transform, centerPx, width, h);

            var plate = MakeArt(b.transform, "Plate", ArtBtn);
            PlaceArtCentered(plate, ArtBtn, width);
            plate.transform.SetAsFirstSibling();

            var t = MakeText(b.transform, "Label", label, fontSize, TextGold);
            UIBuilder.Center(t.rectTransform, width, h);
            return b;
        }

        // ───────────────────────── 美術圖擺位（ArtSpec / PlaceArt，同 ForgingPanel）─────────────────────────

        struct ArtSpec
        {
            public string path;
            public float fullW, fullH;    // 圖檔完整尺寸
            public float bx, by, bw, bh;  // 內容邊界框（左上為原點）
            public ArtSpec(string path, float fullW, float fullH, float bx, float by, float bw, float bh)
            { this.path = path; this.fullW = fullW; this.fullH = fullH; this.bx = bx; this.by = by; this.bw = bw; this.bh = bh; }
            public float Aspect => bw / bh;
        }

        // 底圖是「整張都是內容」的那種（＝滿版背景）→ 邊界框＝整張畫布。
        static readonly ArtSpec ArtBg = new ArtSpec(Dir + "SelectSavePanel_Bg", 1672, 941, 0, 0, 1672, 941);
        static readonly ArtSpec ArtFrame = new ArtSpec(Dir + "SelectSavePanel_Frame", 692, 886, 24, 10, 639, 862);
        static readonly ArtSpec ArtActorBase = new ArtSpec(Dir + "SelectSavePanel_ActorBase", 612, 408, 112, 156, 385, 135);
        static readonly ArtSpec ArtBtn = new ArtSpec(CommonDir + "SelectSavePanel_Btn", 914, 273, 19, 35, 865, 209);

        /// <summary>建一張美術圖（不擋點擊）。載不到就留一個透明的殼，版面不會塌。</summary>
        static Image MakeArt(Transform parent, string name, ArtSpec spec)
        {
            var img = UIBuilder.Image(parent, name, LoadArt(spec), Color.white);
            img.raycastTarget = false;
            img.preserveAspect = false;   // 尺寸由 PlaceArt 精算
            return img;
        }

        /// <summary>把圖擺好，讓「內容」剛好是 contentW 寬、中心落在 centerPx（底圖像素座標，左上原點）。</summary>
        static void PlaceArt(Image img, ArtSpec spec, float contentW, Vector2 centerPx, float contentHOverride = 0f)
        {
            ComputeArtRect(spec, contentW, contentHOverride, out float rectW, out float rectH, out float dx, out float dy);
            Place(img.rectTransform, new Vector2(centerPx.x - dx, centerPx.y + dy), rectW, rectH);
        }

        /// <summary>同上，但擺在父物件的正中央（用在按鈕底板這種「子物件以自身中心為原點」的場合）。</summary>
        static void PlaceArtCentered(Image img, ArtSpec spec, float contentW, float contentHOverride = 0f)
        {
            ComputeArtRect(spec, contentW, contentHOverride, out float rectW, out float rectH, out float dx, out float dy);
            UIBuilder.Center(img.rectTransform, rectW, rectH, new Vector2(-dx, dy));
        }

        static void ComputeArtRect(ArtSpec spec, float contentW, float contentHOverride,
                                   out float rectW, out float rectH, out float dx, out float dy)
        {
            float contentH = contentHOverride > 0f ? contentHOverride : contentW / spec.Aspect;
            rectW = contentW * (spec.fullW / spec.bw);
            rectH = contentH * (spec.fullH / spec.bh);

            // 內容中心相對「畫布中心」的偏移（圖檔座標左上原點）
            float ox = (spec.bx + spec.bw * 0.5f) - spec.fullW * 0.5f;
            float oy = spec.fullH * 0.5f - (spec.by + spec.bh * 0.5f);
            dx = ox * (rectW / spec.fullW);
            dy = oy * (rectH / spec.fullH);
        }

        /// <summary>
        /// 載圖並檢查它跟 ArtSpec 記的還對不對得上（重新輸出圖檔時把靜默偏移變成明確警告）。
        /// ⚠ 比的是**畫布比例**而不是像素數——匯入設定的 Max Size 會等比縮小圖，那對 PlaceArt 的比值算式完全無害
        /// （理由與 ForgingPanel.LoadArt 相同，見 PROBLEMS D12）。
        /// </summary>
        static Sprite LoadArt(ArtSpec spec)
        {
            var sp = LoadSprite(spec.path);
            if (sp != null)
            {
                float w = sp.rect.width, h = sp.rect.height;
                if (w > 0f && h > 0f && spec.fullH > 0f)
                {
                    float got = w / h, want = spec.fullW / spec.fullH;
                    if (Mathf.Abs(got - want) > want * 0.01f)
                        Debug.LogWarning($"[SaveSlotPanel]「{spec.path}」的畫布比例對不上：實際 {w}x{h}（{got:F3}），" +
                                         $"版面表記的是 {spec.fullW}x{spec.fullH}（{want:F3}）。" +
                                         "圖重新輸出過了嗎？請重新量它的不透明內容邊界框並更新 SaveSlotPanel 的 ArtSpec。");
                }
            }
            return sp;
        }

        static Sprite LoadSprite(string path)
        {
            // UI/Texts/ 底下的是「圖片型文字」：實際檔案在 UI/Texts/<語言>/ 裡，
            // 這裡解析成當前語言的路徑，缺當前語言就退回母版（繁中）。見 Localization/LocalizedArt。
            path = Dipan.Localization.LocalizedArt.ResolveExisting(path);

            var sp = Resources.Load<Sprite>(path);
            if (sp != null) return sp;
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            Debug.LogWarning($"[SaveSlotPanel] 載不到美術 Resources/{path}（沒放圖，或匯入型別不是 Sprite/Texture？）。");
            return null;
        }
    }
}
