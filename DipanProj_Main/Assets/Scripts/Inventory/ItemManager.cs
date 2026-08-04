using UnityEngine;
using Dipan.Rules;

namespace Dipan.Inventory
{
    /// <summary>
    /// **產生一件物品的唯一工廠。**
    ///
    /// 表格（ItemTable / WeaponTable）只是模板；玩家真正拿到的那一件還要附加表格裡沒有的東西——
    /// 裝備要骰孔數與孔位、能力珠要骰等級。這些一律在這裡完成，所以只要走這個工廠，
    /// 怪物掉落、觸發鏈 giveItem、祭壇抽選、作弊面板、關卡結算…全部自動吃到同一套規則。
    ///
    /// 【鐵則】任何「憑空生出一件物品」的程式碼都要走 <see cref="Create"/> 或 <see cref="Give"/>，
    /// 不要自己 new ItemStack——否則那條路徑生出來的裝備會沒有孔、珠子會沒有等級，
    /// 而且不會報錯，非常難查。
    /// （搬運、退回背包這類「東西本來就存在」的路徑不算產生，要原封不動搬既有的 ItemStack。）
    ///
    /// 見 readme/GEM_SOCKET.md。
    /// </summary>
    public static class ItemManager
    {
        static GemDatabase _gems;

        /// <summary>能力珠定義表（第一次存取時自動載入）。</summary>
        public static GemDatabase Gems
        {
            get
            {
                if (_gems == null)
                {
                    _gems = new GemDatabase();
                    _gems.LoadAuto();
                }
                return _gems;
            }
        }

        /// <summary>關掉 Domain Reload 後 static 不會歸零，進 Play 時由 PlayModeStaticReset 呼叫。</summary>
        public static void ResetForPlayMode() => _gems = null;

        // ───────────────────────── 產生 ─────────────────────────

        /// <summary>
        /// 產生一件全新的物品（含依規則骰出來的實例資料）。
        /// count 只對可疊道具有意義；有實例資料的物品一律一件一格，count 會被夾成 1。
        /// </summary>
        public static ItemStack Create(int itemId, int count = 1)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            if (d == null || count <= 0) return ItemStack.Empty;

            var inst = CreateInstance(d);
            if (inst != null) count = 1;   // 有實例 → 不可疊
            return new ItemStack { ItemId = itemId, Count = count, Inst = inst };
        }

        /// <summary>
        /// 依物品定義骰出實例資料；不需要實例的物品（一般可疊道具）回 null。
        /// 裝備 → 骰孔數 + 隨機孔位；能力珠 → 骰等級。
        /// </summary>
        public static ItemInstance CreateInstance(ItemData d)
        {
            if (d == null) return null;
            int cycle = CurrentCycle();

            if (d.IsGem)
                return ItemInstance.ForGem(RandomRules.RollGemLevel(cycle));

            if (d.IsEquippable)
                return ItemInstance.FromSocketLayout(RandomRules.RollSocketLayout(cycle));

            return null;
        }

        /// <summary>指定孔數產生一件裝備（作弊面板/測試用；-1 = 照常骰）。</summary>
        public static ItemStack CreateWithSockets(int itemId, int socketCount)
        {
            var st = Create(itemId, 1);
            if (st.IsEmpty || socketCount < 0) return st;
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            if (d == null || !d.IsEquippable) return st;
            st.Inst = ItemInstance.FromSocketLayout(RandomRules.LayoutFor(socketCount));
            return st;
        }

        /// <summary>指定等級產生一顆能力珠（作弊面板/強化石用；level 夾 1~3）。</summary>
        public static ItemStack CreateGem(int itemId, int level)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            if (d == null || !d.IsGem) return ItemStack.Empty;
            return new ItemStack { ItemId = itemId, Count = 1, Inst = ItemInstance.ForGem(Mathf.Clamp(level, 1, 3)) };
        }

        // ───────────────────────── 給玩家 ─────────────────────────

        /// <summary>
        /// 產生並交給玩家（關卡內進臨時包、廣場進真背包）。回傳放不下的剩餘數量。
        /// 這是「取得物品」的建議入口——它會先產生完整實例，再交給 RunProgress。
        /// </summary>
        public static int Give(int itemId, int count = 1)
        {
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(itemId) : null;
            if (d == null || count <= 0) return count;

            // 需要實例的物品：一件一件產生（每件各自骰孔數/等級），否則六件裝備會共用同一份實例。
            if (NeedsInstance(d))
            {
                int left = 0;
                for (int i = 0; i < count; i++)
                    left += GiveStack(Create(itemId, 1));
                return left;
            }
            return GiveStack(new ItemStack { ItemId = itemId, Count = count, Inst = null });
        }

        /// <summary>把一個「已經存在的」ItemStack 交給玩家（不重新產生實例）。掉落物撿取、結算落袋走這條。</summary>
        public static int GiveStack(ItemStack st)
        {
            if (st.IsEmpty) return 0;
            if (RunProgress.Exists) return RunProgress.Instance.GiveStack(st);

            var inv = InventorySystem.Instance;
            return inv != null ? inv.AddStack(st) : st.Count;
        }

        /// <summary>這種物品需不需要實例資料（裝備、能力珠都要）。</summary>
        public static bool NeedsInstance(ItemData d) => d != null && (d.IsGem || d.IsEquippable);

        // ───────────────────────── 珠子小工具 ─────────────────────────

        /// <summary>把「背包裡的一顆珠子」轉成可以塞進孔位的 GemRef。不是珠子回 null。</summary>
        public static GemRef ToGemRef(ItemStack st)
        {
            var inv = InventorySystem.Instance;
            var d = (inv != null && !st.IsEmpty) ? inv.GetData(st.ItemId) : null;
            if (d == null || !d.IsGem) return null;
            int lv = (st.Inst != null && st.Inst.level > 0) ? st.Inst.level : 1;
            return new GemRef { itemId = st.ItemId, level = lv };
        }

        /// <summary>把孔位裡的珠子還原成背包裡的一格（等級原封不動帶回去）。</summary>
        public static ItemStack FromGemRef(GemRef g)
        {
            if (g == null || g.itemId <= 0) return ItemStack.Empty;
            return new ItemStack { ItemId = g.itemId, Count = 1, Inst = ItemInstance.ForGem(Mathf.Clamp(g.level, 1, 3)) };
        }

        /// <summary>這顆珠子（背包格或孔位）對應的能力定義；查不到回 null。</summary>
        public static GemData DataOf(GemRef g)
        {
            var inv = InventorySystem.Instance;
            var d = (g != null && inv != null) ? inv.GetData(g.itemId) : null;
            return (d != null && d.GemID > 0) ? Gems.Get(d.GemID) : null;
        }

        static int CurrentCycle()
        {
            var sm = Dipan.Save.SaveManager.Instance;
            return sm != null ? Mathf.Max(1, sm.Cycle) : 1;
        }
    }
}
