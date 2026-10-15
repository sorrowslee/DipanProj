using System;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Localization;

namespace Dipan.UI
{
    /// <summary>
    /// 二選一倒數面板（觸發鏈 <c>timedChoice</c> 的 UI）。見 readme/bloodfang/GAP.md §2、readme/TRIGGER_CHAIN.md §3。
    ///
    /// 行為：暫停遊戲、置中一張彈窗——標題一行、副標一行、一條倒數條、兩顆選項鈕。
    /// 玩家按 A／按 B／倒數歸零，三條路**互斥、只走一條**，結果交給呼叫端的回呼（延一幀，避開面板關閉當幀重入，
    /// 同 TRIGGER_CHAIN §5「對話接對話延一幀」的理由）。
    ///
    /// ⚠ 不可 ESC 關閉：關了就三條路都沒走到＝劇本鏈卡死。
    /// ⚠ 倒數走 <c>Time.unscaledDeltaTime</c>：面板本身 PausesGame，timeScale=0。
    /// ⚠ 被別的流程關掉（換圖／死亡／CloseAll）時**不呼叫回呼**、印 Warning——那時劇本已經不在了，硬接 next 只會在新地圖上亂跑。
    /// ⚠ 開啟時若已有別的東西鎖著輸入（對話面板、鎖輸入的演出…），延後一幀再試，最多 <see cref="MaxDeferFrames"/> 幀。
    ///
    /// 美術：第一版借 <see cref="ConfirmPopup"/> 的背板與 LongBtn（作者之後給圖再換），版面常數在本檔上方。
    /// 面板裡沒有任何寫死字串，全部是語言表 id（AGENTS.md 鐵則）。
    /// </summary>
    public class ChoicePanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => false;     // 自己畫暗底
        public override bool CloseOnEscape => false;    // 三條路之外沒有第四條

        public enum Result { OptionA = 0, OptionB = 1, Timeout = 2 }

        /// <summary>秒數沒填／≤0 時的預設倒數（劇本需要逾時分支，不允許無限等）。</summary>
        public const float DefaultSeconds = 15f;
        /// <summary>開啟前等「別人的輸入鎖」最多幾幀（約 5 秒）；超過就強開並警告，免得鏈永遠卡住。</summary>
        const int MaxDeferFrames = 300;
        /// <summary>面板一開先擋多久的點擊：玩家多半是用左鍵點掉上一段對話來到這裡，手還沒放開就會誤選。</summary>
        const float OpenInputGuard = 0.4f;

        // ── 版面（背板原圖像素座標，左上原點、填中心點；沿用 ConfirmPopup 的背板與量測）──
        const float BgW = 1448f, BgH = 1086f;
        const float DisplayHeight = 660f;
        const float TitleCx = 725f, TitleCy = 430f, TitleW = 1040f, TitleH = 120f;
        const int TitleFontMax = 64, TitleFontMin = 34;
        const float SubCx = 725f, SubCy = 540f, SubW = 1040f, SubH = 70f;
        const int SubFont = 36;
        const float BarCx = 700f, BarCy = 640f, BarW = 860f, BarH = 18f;
        const float CountCx = 1190f, CountCy = 640f, CountW = 120f, CountH = 60f;
        const int CountFont = 40;
        const float BtnW = 300f, BtnH = 200f, BtnY = 798f;
        const float ACx = 548f, BCx = 900f;
        const int BtnFontMax = 40, BtnFontMin = 22;

        static readonly Color TitleColor = new Color(0.95f, 0.93f, 0.85f);
        static readonly Color SubColor = new Color(0.85f, 0.55f, 0.50f);
        static readonly Color BarBgColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color BarFillColor = new Color(0.72f, 0.10f, 0.08f, 1f);

        RectTransform _frame;
        Text _title, _sub, _count, _labelA, _labelB;
        RectTransform _barFill;

        float _total, _left;
        bool _decided;
        Action<Result> _onDone;

        // ───────────────────────── 對外入口 ─────────────────────────

        /// <summary>
        /// 開二選一倒數。<paramref name="subTextId"/> ≤0 ＝ 沒有副標。<paramref name="seconds"/> ≤0 ＝ <see cref="DefaultSeconds"/>。
        /// <paramref name="onDone"/> 只會被呼叫一次（三選一），且在面板關閉後的下一幀。
        /// </summary>
        public static void Show(int titleId, int subTextId, int optionAId, int optionBId, float seconds, Action<Result> onDone)
        {
            TryOpen(titleId, subTextId, optionAId, optionBId, seconds > 0f ? seconds : DefaultSeconds, onDone, 0);
        }

        static void TryOpen(int titleId, int subTextId, int optA, int optB, float seconds, Action<Result> onDone, int tries)
        {
            if (UIManager.Instance == null)
            {
                Debug.LogWarning("[ChoicePanel] 沒有 UIManager，無法開二選一面板（鏈在此中止）。");
                return;
            }
            if (UIManager.IsGameplayInputBlocked && tries < MaxDeferFrames)
            {
                TriggerChainRunner.NextFrame(() => TryOpen(titleId, subTextId, optA, optB, seconds, onDone, tries + 1));
                return;
            }
            if (tries >= MaxDeferFrames)
                Debug.LogWarning($"[ChoicePanel] 等了 {MaxDeferFrames} 幀輸入鎖仍未解除（可能有長時間的 SetExternalHold），強制開啟。");

            var p = UIManager.Instance.Open<ChoicePanel>();
            if (p != null) p.Apply(titleId, subTextId, optA, optB, seconds, onDone);
        }

        // ───────────────────────── 建構 ─────────────────────────

        protected override void OnBuild()
        {
            var dim = UIBuilder.SolidPanel(transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
            dim.raycastTarget = true;

            var frameGO = UIBuilder.Create("Frame", transform);
            _frame = UIBuilder.Rect(frameGO);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = Vector2.zero;
            _frame.sizeDelta = new Vector2(BgW, BgH);
            float scale = DisplayHeight / BgH;
            _frame.localScale = new Vector3(scale, scale, 1f);

            var bg = UIBuilder.Image(frameGO.transform, "BG", UIBuilder.LoadSprite("UI/PopupPannel/PopupPanelBG"));
            UIBuilder.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            _title = UIBuilder.Text(_frame, "Title", "", TitleFontMax, TitleColor, TextAnchor.MiddleCenter);
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = TitleFontMin;
            _title.resizeTextMaxSize = TitleFontMax;
            _title.verticalOverflow = VerticalWrapMode.Truncate;
            Place(UIBuilder.Rect(_title), TitleCx, TitleCy, TitleW, TitleH);

            _sub = UIBuilder.Text(_frame, "Sub", "", SubFont, SubColor, TextAnchor.MiddleCenter);
            Place(UIBuilder.Rect(_sub), SubCx, SubCy, SubW, SubH);

            // 倒數條：底＋填色。用「填色的右錨點 = 剩餘比例」縮短，不用 Image.fillAmount（無 sprite 的 Image 吃不到 Filled）。
            var barBg = UIBuilder.Image(_frame, "BarBG", null, BarBgColor);
            barBg.raycastTarget = false;
            Place(barBg.rectTransform, BarCx, BarCy, BarW, BarH);
            var fill = UIBuilder.Image(barBg.transform, "Fill", null, BarFillColor);
            fill.raycastTarget = false;
            _barFill = fill.rectTransform;
            _barFill.anchorMin = new Vector2(0f, 0f);
            _barFill.anchorMax = new Vector2(1f, 1f);
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;

            _count = UIBuilder.Text(_frame, "Count", "", CountFont, TitleColor, TextAnchor.MiddleCenter);
            Place(UIBuilder.Rect(_count), CountCx, CountCy, CountW, CountH);

            _labelA = BuildOptionButton("OptionA", ACx, () => OnPick(Result.OptionA));
            _labelB = BuildOptionButton("OptionB", BCx, () => OnPick(Result.OptionB));
        }

        Text BuildOptionButton(string name, float cx, UnityEngine.Events.UnityAction onClick)
        {
            Sprite N = UIBuilder.LoadSprite("UI/Common/LongBtn_normal");
            Sprite P = UIBuilder.LoadSprite("UI/Common/LongBtn_pressed");
            var b = UIBuilder.Button(_frame, name, "", onClick, Color.white, N);
            var img = b.GetComponent<Image>();
            img.preserveAspect = true;
            b.targetGraphic = img;                       // 程式建按鈕需手動指（PROBLEMS D4）
            b.transition = Selectable.Transition.SpriteSwap;
            var ss = b.spriteState;
            ss.pressedSprite = P; ss.highlightedSprite = N; ss.selectedSprite = N;
            b.spriteState = ss;
            Place((RectTransform)b.transform, cx, BtnY, BtnW, BtnH);

            var t = UIBuilder.Text(b.transform, "Label", "", BtnFontMax, TitleColor, TextAnchor.MiddleCenter);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = BtnFontMin;
            t.resizeTextMaxSize = BtnFontMax;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            UIBuilder.Stretch(t.rectTransform, 24f, 24f, 20f, 20f);
            return t;
        }

        // ───────────────────────── 流程 ─────────────────────────

        void Apply(int titleId, int subTextId, int optA, int optB, float seconds, Action<Result> onDone)
        {
            _onDone = onDone;
            _decided = false;
            _total = _left = Mathf.Max(0.5f, seconds);
            if (_title != null) _title.text = titleId > 0 ? Language.GetText(titleId) : "";
            if (_sub != null) _sub.text = subTextId > 0 ? Language.GetText(subTextId) : "";
            if (_labelA != null) _labelA.text = optA > 0 ? Language.GetText(optA) : "";
            if (_labelB != null) _labelB.text = optB > 0 ? Language.GetText(optB) : "";
            RefreshTimer();
            BlockInputFor(OpenInputGuard);
        }

        void Update()
        {
            if (!IsOpen || _decided) return;
            _left -= Time.unscaledDeltaTime;
            if (_left <= 0f) { _left = 0f; RefreshTimer(); Decide(Result.Timeout); return; }
            RefreshTimer();
        }

        void RefreshTimer()
        {
            float k = _total > 0f ? Mathf.Clamp01(_left / _total) : 0f;
            if (_barFill != null) _barFill.anchorMax = new Vector2(k, 1f);
            if (_count != null) _count.text = Mathf.CeilToInt(_left).ToString();
        }

        void OnPick(Result r)
        {
            if (_decided) return;
            if (!TryConsumeInput()) return;   // 開啟後 OpenInputGuard 秒內的點擊不算
            Decide(r);
        }

        void Decide(Result r)
        {
            if (_decided) return;
            _decided = true;
            var cb = _onDone;
            _onDone = null;
            Debug.Log($"[ChoicePanel] 結果：{r}（剩 {_left:0.0} 秒）");
            UIManager.Instance.Close(this);
            if (cb != null) TriggerChainRunner.NextFrame(() => cb(r));
        }

        protected override void OnClose()
        {
            if (_decided) return;
            // 不是自己決定的關閉（換圖／死亡／CloseAll）：劇本已經不在了，不接任何一條。
            _decided = true;
            _onDone = null;
            Debug.LogWarning("[ChoicePanel] 面板在玩家選擇前被別的流程關閉，三條接續都不會執行。");
        }

        // 座標映射：錨到 frame 左上角、anchoredPosition=(px,-py)（與 ConfirmPopup 一致）
        void Place(RectTransform rt, float px, float py, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(px, -py);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
