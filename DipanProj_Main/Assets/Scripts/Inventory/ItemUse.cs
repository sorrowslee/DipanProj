using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>
    /// **「使用一件道具」的唯一入口。**
    ///
    /// 遊戲裡只有一種使用方式：**在背包裡對它按滑鼠右鍵**（藥水另外還有快捷格熱鍵 1／2，
    /// 但那條路最後也是走進這裡）。左鍵一律只做「搬移／裝備／綁定快捷格」這類**不消耗**的事，
    /// 詳見 readme/INVENTORY.md 的「左鍵 vs 右鍵」。
    ///
    /// 為什麼要有這一支：使用行為原本散在三個地方——背包左鍵、背包右鍵、藥水熱鍵，
    /// 各自寫一份判斷與消耗。結果就是「左鍵也能喝血統藥劑」這種**沒人打算做、但兩邊都寫了**的行為
    /// （2026-08-19 作者回報）。收成一支之後，加新的可用道具只要在 <see cref="PlanUse"/> 與
    /// <see cref="TryUse"/> 各補一個分支，三個呼叫端全部自動支援。
    ///
    /// 分工（照 <c>BloodlineSystem.Plan</c>／<c>TryDrink</c> 的樣板）：
    /// <list type="bullet">
    /// <item><see cref="PlanUse"/> — **純計算**，不改任何狀態。回答「能不能用／不能用的理由／
    ///   用之前要不要先跳確認視窗」。UI 拿它決定要 Toast、要跳窗、還是安靜地什麼都不做。</item>
    /// <item><see cref="TryUse"/> — 真的用下去。**成功與失敗都會填 message**，呼叫端直接 Toast 即可
    ///   （message 為空＝這次使用不需要對玩家說話，例如喝一瓶回血藥）。</item>
    /// </list>
    ///
    /// ⚠ <b>本檔刻意不碰 UI</b>（沒有 Toast、沒有確認視窗）。確認視窗是 UI 的事，
    /// 所以 <see cref="Plan.ConfirmText"/> 只是「請你先問一下玩家」的通知；藥水熱鍵那條路
    /// 根本不看它（藥水從來不需要確認）。
    /// </summary>
    public static class ItemUse
    {
        /// <summary>
        /// 一次使用的規劃結果。<see cref="Ok"/> 為 false 時：
        /// <see cref="Reason"/> 有字＝要告訴玩家為什麼不行；
        /// <see cref="Reason"/> 為空＝**這東西根本沒有使用行為**（武器、材料、能力珠…），
        /// 此時 UI 應該安靜地什麼都不做，不要跳「無法使用」之類的廢話。
        /// </summary>
        public struct Plan
        {
            public bool Ok;
            public string Reason;
            /// <summary>非空 = 用之前要先跳確認視窗，內容就是這段字（例：血統不可逆的警告）。</summary>
            public string ConfirmText;
        }

        /// <summary>
        /// 這件道具**有沒有使用行為**（右鍵按下去會發生事情嗎）。
        /// 加新的可用道具類別時，這裡與 <see cref="PlanUse"/>／<see cref="TryUse"/> 三處要一起加。
        /// </summary>
        public static bool IsUsable(ItemData d)
            => d != null && (d.IsPotion || d.IsBloodline);

        /// <summary>
        /// 純計算：現在能不能用這件道具。不改任何狀態，可以隨便呼叫。
        /// </summary>
        public static Plan PlanUse(int itemId)
        {
            var inv = InventorySystem.Instance;
            if (inv == null || itemId <= 0) return None();

            var d = inv.GetData(itemId);
            if (!IsUsable(d)) return None();          // 沒有使用行為 → 安靜地不做事
            if (inv.CountOf(itemId) <= 0) return None();   // 手上其實沒有（UI 慢一幀）→ 同上

            // 血統藥劑：規則全部由 BloodlineSystem 管（本世只能一種系列、要逐階、不可逆…）。
            // ⚠ 不能喝的時候要**在按下右鍵的當下就說明理由**，不要先跳確認視窗、按完才發現沒反應
            //   （例：還在第一階卻拿到高階藥劑，會直接告訴玩家要先進階為「毛殭」）。
            if (d.IsBloodline)
            {
                var bp = Dipan.Gacha.BloodlineSystem.Plan(itemId);
                return bp.Ok
                    ? new Plan { Ok = true, ConfirmText = bp.ConfirmText }
                    : new Plan { Ok = false, Reason = bp.Reason };
            }

            // 回血／回魔藥劑：**滿血滿魔也照喝照扣**，與快捷格熱鍵 1／2 完全一致（刻意不擋，見 BOTTOM_HUD.md）。
            // 兩條路行為若不同，玩家會覺得其中一邊壞了。
            //
            // ⚠ 但「沒有活著的玩家可以套效果」要擋：CombatStats 死了之後 Heal/RestoreMana 會直接 return，
            //    而扣瓶是無條件的 → 玩家會看到「藥沒了、血沒回、也沒有任何訊息」。
            //    這是 BloodlineSystem 特地防過的同一種壞法。靜靜地不做事就好（本來也不該按得到）。
            if (LivePlayerStats() == null) return None();
            return new Plan { Ok = true };
        }

        /// <summary>場上活著的玩家 CombatStats；沒有玩家、或玩家已死 → null。</summary>
        static CombatStats LivePlayerStats()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var stats = player != null ? player.GetComponent<CombatStats>() : null;
            return (stats != null && !stats.IsDead) ? stats : null;
        }

        /// <summary>
        /// 真的把道具用下去。回傳是否成功；<paramref name="message"/> **成功失敗都可能有字**，
        /// 有字就直接 Toast 給玩家看，空字串代表這次不需要說話。
        ///
        /// 內部會**重新規劃一次**——確認視窗開著的期間狀態可能變了（東西被搬走、被喝掉、
        /// 血統被別的途徑改掉），所以呼叫端不要拿幾秒前的 <see cref="Plan"/> 當結論。
        /// </summary>
        public static bool TryUse(int itemId, out string message)
        {
            message = null;

            var inv = InventorySystem.Instance;
            if (inv == null || itemId <= 0) return false;
            var d = inv.GetData(itemId);
            if (!IsUsable(d)) return false;

            // 血統藥劑：整套規則＋存檔＋變身表演都在 BloodlineSystem 裡，訊息也由它產生。
            if (d.IsBloodline)
                return Dipan.Gacha.BloodlineSystem.TryDrink(itemId, out message);

            if (d.IsPotion)
                return DrinkPotion(inv, d, itemId);

            return false;
        }

        /// <summary>
        /// 喝一瓶回血／回魔藥劑：套效果 → 扣一瓶 → 在玩家身上播喝藥特效 → 該種類用完就清空快捷格。
        /// 這段原本住在 <c>PotionHotkeys</c> 裡，背包右鍵也要喝之後搬到這裡共用——
        /// 兩條路一定要是同一段程式，不然「按 1」和「右鍵」的行為早晚會走鐘。
        /// </summary>
        static bool DrinkPotion(InventorySystem inv, ItemData d, int itemId)
        {
            if (inv.CountOf(itemId) <= 0) { SyncPotionSlots(inv); return false; }

            // ⚠ 先確認有活著的玩家可以套效果，**再**扣瓶。
            //    反過來的話（舊版就是）玩家死掉/還沒生出來時會「藥沒了、血沒回、也沒訊息」。
            var stats = LivePlayerStats();
            if (stats == null) return false;

            if (d.HealHp > 0) stats.Heal(d.HealHp);
            if (d.HealMp > 0) stats.RestoreMana(d.HealMp);
            inv.RemoveItem(itemId, 1);

            var pc = stats.GetComponent<PlayerController>();
            if (pc != null) pc.PlayDrinkPotionVfx();   // 不管喝哪種藥都播

            SyncPotionSlots(inv);
            return true;
        }

        /// <summary>
        /// 快捷格上綁的那一種藥水已經沒了 → 清空該格。
        /// ⚠ 掃過所有格子而不是只清「剛才用的那一格」：從背包右鍵喝的時候，
        /// 我們手上只有 itemId、不知道它綁在第幾格（也可能同時綁在兩格）。
        /// </summary>
        static void SyncPotionSlots(InventorySystem inv)
        {
            for (int i = 0; i < InventorySystem.PotionSlotCount; i++)
            {
                int id = inv.GetPotionSlot(i);
                if (id > 0 && inv.CountOf(id) <= 0) inv.SetPotionSlot(i, 0);
            }
        }

        /// <summary>「這東西沒有使用行為」——Ok=false 且 Reason 為空，UI 看到就安靜地不做事。</summary>
        static Plan None() => new Plan { Ok = false, Reason = null, ConfirmText = null };
    }
}
