using System;
using UnityEngine;
using UnityEngine.UI;
using Dipan.Inventory;

namespace Dipan.UI
{
    /// <summary>
    /// 傳送門 UI：一個「放劇本方框」＋一顆「開啟」圓鈕（無底版，素材放 Resources/UI/ScriptsPanel/）。
    /// 開啟時**強制連背包一起開**（並排，方便把劇本從背包拖進方框）；關閉時把未使用的劇本退回背包並關掉背包。
    /// 按下開啟：讀方框那件劇本的目的地關卡 → 開啟對應傳送點 → **劇本就此消耗**（方框清空、不退回背包、立刻存檔）→ 關閉。
    /// （作者拍板 2026-11-06：劇本是消耗品，按下開門那一刻就消失。）
    /// 由 portal 互動點的 F 觸發（見 InteractionManager）。座標為量測常數，實機可微調。見 readme/TRIGGER_CHAIN.md。
    /// </summary>
    public class ScriptsPanel : UIPanel
    {
        public override UILayer Layer => UILayer.Window;
        public override bool PausesGame => true;
        public override bool BlocksGameplayInput => true;
        public override bool ShowBackdrop => true;

        const string ResDir = "UI/ScriptsPanel/";

        // ── 擺位（量測常數；實機不合再調）──
        const float ContentX = -360f;   // 整組往左（與並排到右邊的背包分開）
        const float FrameSize = 300f;   // 方框底圖大小
        const float FrameY = 70f;       // 方框中心 Y（正＝往上）
        const float SlotSize = 150f;    // 方框內可放劇本的格子大小
        const float BtnW = 400f, BtnH = 192f;   // 按鈕放大 2 倍
        const float ButtonY = -130f;    // 按鈕中心 Y（比 -110 再往下 20）

        // 給教學/其它系統掛的鉤子（解耦，不直接依賴 TutorialManager）。
        public static event Action OnOpened;        // 面板開啟
        public static event Action OnScriptPlaced;  // 方框放入了劇本
        public static event Action OnPortalOpened;   // 按下開啟、傳送門成功打開

        readonly ScriptSlotGrid _grid = new ScriptSlotGrid();
        ScriptSlotWidget _slot;
        Button _startBtn;
        string _teleportName;   // 這道傳送門要開哪一個 teleport（由 portal 互動點傳入）

        // ── 給新手教學用：暴露方框/按鈕/容器 ──
        public RectTransform SlotRect => _slot != null ? _slot.Rt : null;
        public RectTransform ButtonRect => _startBtn != null ? (RectTransform)_startBtn.transform : null;
        public bool SlotFilled => _grid.HasScript;

        /// <summary>傳送門開著時回傳它的方框容器（給背包「點劇本→送進來」用）；否則 null。</summary>
        public static Dipan.Inventory.IItemGrid ActiveGridIfOpen()
        {
            var ui = UIManager.Instance;
            var p = ui != null ? ui.Get<ScriptsPanel>() : null;
            return (p != null && p.IsOpen) ? p._grid : null;
        }

        /// <summary>傳送門面板若開著就回傳它（給教學拿方框/按鈕位置）。</summary>
        public static ScriptsPanel OpenInstance()
        {
            var ui = UIManager.Instance;
            var p = ui != null ? ui.Get<ScriptsPanel>() : null;
            return (p != null && p.IsOpen) ? p : null;
        }

        /// <summary>
        /// 「劇本已開門、但人還沒走進關卡」的旗標（關卡單次：只在記憶體、跨 module 就清）。
        /// 按下開門時寫入。用途（作者拍板 2026-11-06）：
        ///   ‧ 邪佛對話（Main_Square「邪佛對話」）加條件 <c>!關卡:scriptPortalOpened</c> → 開門後走回邪佛面前不會再拿到一張劇本；
        ///   ‧ <see cref="Dipan.Flow.QuitGuard.InScript"/> 把它算進「劇本中」→ 開了門還站在廣場上離開遊戲也會跳警告。
        /// 走進關卡（跨 module）時自動清掉；死了／返回回到廣場時又是乾淨的（邪佛照常可以重發）。
        /// </summary>
        public const string PortalOpenedFlag = TriggerChain.LevelPrefix + "scriptPortalOpened";

        /// <summary>由 portal 互動點呼叫：開啟傳送門 UI，並記住要解鎖哪個 teleport 區域。</summary>
        public static void OpenFor(string teleportRegionName)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            var p = ui.Open<ScriptsPanel>();
            if (p != null) p._teleportName = teleportRegionName;
        }

        protected override void OnBuild()
        {
            // 方框底圖
            var frameSprite = Resources.Load<Sprite>(ResDir + "ScriptsPanel_Input");
            var frame = UIBuilder.Image(transform, "Frame", frameSprite,
                                        frameSprite != null ? Color.white : new Color(0.2f, 0.2f, 0.24f, 0.95f));
            frame.preserveAspect = true;
            UIBuilder.Center(frame.rectTransform, FrameSize, FrameSize, new Vector2(ContentX, FrameY));

            // 方框內的劇本格（拖放收/拿）
            _slot = ScriptSlotWidget.Create(transform, SlotSize);
            UIBuilder.Center(_slot.Rt, SlotSize, SlotSize, new Vector2(ContentX, FrameY));
            _slot.Bind(_grid, 0);

            // 開啟圓鈕
            var btnSprite = Resources.Load<Sprite>(ResDir + "ScriptsPanel_StartBtn");
            _startBtn = UIBuilder.Button(transform, "StartBtn", "", OnStartPressed, Color.white, btnSprite);
            var bimg = _startBtn.GetComponent<Image>();
            bimg.preserveAspect = true;
            _startBtn.targetGraphic = bimg;
            UIBuilder.Center((RectTransform)_startBtn.transform, BtnW, BtnH, new Vector2(ContentX, ButtonY));
        }

        protected override void OnOpen()
        {
            // 強制把背包一起開（並排由 StorageBagCoordinator 統一擺）。
            UIManager.Instance?.Open<InventoryPanel>();
            _grid.OnChanged += OnGridChanged;
            RefreshSlot();
            OnOpened?.Invoke();
        }

        protected override void OnClose()
        {
            _grid.OnChanged -= OnGridChanged;
            // 沒按開啟就關閉 → 把方框裡的劇本退回背包，別弄丟。
            if (!_grid.Current.IsEmpty)
            {
                var st = _grid.TakeOut();
                if (st.ItemId > 0 && InventorySystem.Instance != null)
                    InventorySystem.Instance.AddItem(st.ItemId, st.Count);
            }
            // 傳送門與背包是綁定的一組 → 一起關。
            UIManager.Instance?.Close<InventoryPanel>();
        }

        void OnGridChanged()
        {
            RefreshSlot();
            if (_grid.HasScript) OnScriptPlaced?.Invoke();
        }

        void RefreshSlot()
        {
            if (_slot != null) _slot.Refresh();
            if (_startBtn != null) _startBtn.interactable = _grid.HasScript;   // 沒放劇本 → 按鈕不可按
        }

        void OnStartPressed()
        {
            if (!_grid.HasScript) { AlertPanel.Toast("請先把劇本放進方框"); return; }

            var st = _grid.Current;
            var d = InventorySystem.Instance != null ? InventorySystem.Instance.GetData(st.ItemId) : null;
            if (d == null || !d.IsScript) { AlertPanel.Toast("這不是有效的劇本"); return; }

            // 開啟對應傳送點（設目的地＝劇本指定關卡 + 解鎖 + 亮綠幕）。
            bool ok = TriggerChain.OpenPortal(_teleportName, d.TargetMapId, d.TargetEntrance);
            if (!ok) { AlertPanel.Toast("傳送門無法開啟"); return; }

            // 劇本是消耗品（作者拍板 2026-11-06）：按下開門就消失——把方框裡那一份直接拿掉、不退回背包，
            // 並立刻存檔（否則開門後馬上關遊戲，存檔裡劇本還在）。方框清空後 OnClose 也就沒東西可退。
            _grid.TakeOut();
            Debug.Log($"[ScriptsPanel] 開啟傳送門 → 消耗劇本「{d.Name}」(#{d.ID})。");
            Dipan.Save.SaveManager.Instance?.SaveNow();
            TriggerChain.SetFlag(PortalOpenedFlag);   // 開門→進關卡之間的空窗：擋邪佛重發、離開要警告（見常數說明）

            OnPortalOpened?.Invoke();
            UIManager.Instance?.Close(this);
        }
    }
}
