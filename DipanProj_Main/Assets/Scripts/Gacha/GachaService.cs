using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;
using Dipan.Save;
using Dipan.UI;

namespace Dipan.Gacha
{
    /// <summary>
    /// 抽選的資料層：組池、抽、扣錢、發獎。**完全不碰 UI**——面板只是把結果演出來。
    ///
    /// 設計上這裡不知道「武器 / 裝備 / 血統 / 道具」是什麼，只認 poolId。
    /// 一個池抽出來的東西一律是 ItemTable 的物品 id，所以四個池共用同一套發放邏輯。
    ///
    /// 實際抽選池 = 基本表（過濾條件通過的） ∪ 存檔的永久解鎖清單。
    /// 解鎖由觸發鏈動作 <c>unlockRoll</c> 寫入（見 TriggerChain），跨輪迴保留。
    /// </summary>
    public static class GachaService
    {
        /// <summary>一次抽選的結果。</summary>
        public class RollResult
        {
            public bool Ok;
            public string Reason;        // Ok=false 時的失敗原因（拿去 Toast）
            public List<int> ItemIds = new List<int>();   // 抽到的道具 id（依序）
            public int SpentAmount;
            public int CostItemId;
        }

        // ───────────────────────── 組池 ─────────────────────────

        /// <summary>
        /// 組出某個池目前實際可抽的清單（基本表過濾後 ∪ 存檔解鎖清單）。
        /// 重複的 itemId 只留一份（權重取基本表那份；解鎖進來的預設權重 1）。
        /// </summary>
        public static List<GachaRollEntry> BuildCandidates(GachaPoolDef pool)
        {
            var result = new List<GachaRollEntry>();
            if (pool == null) return result;

            var seen = new HashSet<int>();
            int cycle = CurrentCycle();

            // 1) 基本表（要過 MinCycle / RequireFlag）
            foreach (var e in GachaRollTable.Get(pool.BaseTable))
            {
                if (e == null || e.ItemId <= 0) continue;
                if (e.MinCycle > 0 && cycle < e.MinCycle) continue;
                if (!FlagOk(e.RequireFlag)) continue;
                if (!ItemExists(e.ItemId))
                {
                    Debug.LogWarning($"[GachaService] 基本表「{pool.BaseTable}」裡的 itemId {e.ItemId} 在 ItemTable 找不到，略過。");
                    continue;
                }
                if (!seen.Add(e.ItemId)) continue;
                result.Add(e);
            }

            // 2) 存檔解鎖清單（關卡打通後 unlockRoll 加進來的；跨輪迴保留、不受 MinCycle 限制）
            var sm = SaveManager.Instance;
            if (sm != null)
            {
                foreach (int id in sm.GetUnlockedRollEntries(pool.PoolId))
                {
                    if (id <= 0 || !seen.Add(id)) continue;
                    if (!ItemExists(id))
                    {
                        Debug.LogWarning($"[GachaService] 池「{pool.PoolId}」的解鎖清單裡有 itemId {id}，但 ItemTable 找不到，略過。");
                        continue;
                    }
                    result.Add(new GachaRollEntry { ItemId = id, Weight = 1, Note = "（關卡解鎖）" });
                }
            }

            return result;
        }

        /// <summary>玩家目前身上有多少「這個池用來付帳的道具」。</summary>
        public static int MoneyHeld(GachaPoolDef pool)
        {
            if (pool == null) return 0;
            var inv = InventorySystem.Instance;
            return inv != null ? inv.CountOf(pool.CostItemId) : 0;
        }

        /// <summary>這個池的付帳道具名稱（給 UI 顯示，例如「銅錢」）。</summary>
        public static string MoneyName(GachaPoolDef pool)
        {
            if (pool == null) return "錢";
            var inv = InventorySystem.Instance;
            var d = inv != null ? inv.GetData(pool.CostItemId) : null;
            return d != null ? d.Name : $"#{pool.CostItemId}";
        }

        /// <summary>
        /// 現在能不能抽（給 UI 決定按鈕要不要反灰）。reason 是不能抽的理由。
        /// <paramref name="candidates"/> 可傳入已經組好的候選清單避免重複組池（組一次要掃全表＋配置集合，
        /// UI 一次刷新會問好幾遍）；傳 null 就自己組。
        /// </summary>
        public static bool CanRoll(GachaPoolDef pool, bool multi, out string reason, List<GachaRollEntry> candidates = null)
        {
            reason = null;
            if (pool == null) { reason = "找不到這座祭壇的設定"; return false; }
            if (multi && !pool.AllowsMulti) { reason = "這座祭壇不提供連抽"; return false; }
            var cands = candidates ?? BuildCandidates(pool);
            if (cands.Count == 0) { reason = "這座祭壇還沒有可抽的東西"; return false; }

            int cost = multi ? pool.CostMulti : pool.CostSingle;
            if (MoneyHeld(pool) < cost) { reason = $"{MoneyName(pool)}不足（需要 {cost}）"; return false; }
            return true;
        }

        // ───────────────────────── 抽 ─────────────────────────

        /// <summary>
        /// 抽一次（單抽或連抽）。**這裡就把錢扣掉、結果算完、獎品也發到手了**——
        /// 面板的老虎機表演純粹是把已知結果演出來，所以玩家 skip 跟不 skip 拿到的東西完全一樣，
        /// 表演中途關掉面板也不會弄丟東西。
        /// </summary>
        public static RollResult Roll(string poolId, bool multi)
        {
            var res = new RollResult();
            var pool = GachaPoolTable.Get(poolId);
            if (pool == null)
            {
                res.Reason = $"找不到抽選池「{poolId}」";
                Debug.LogWarning($"[GachaService] {res.Reason}（檢查 GachaPoolTable.csv 的 PoolId 與 openPanel 的 arg 是否一致）。");
                return res;
            }
            var candidates = BuildCandidates(pool);
            if (!CanRoll(pool, multi, out string why, candidates)) { res.Reason = why; return res; }

            int count = multi ? Mathf.Max(1, pool.MultiCount) : 1;
            int cost = multi ? pool.CostMulti : pool.CostSingle;

            // 扣錢（CanRoll 已確認夠，這裡再擋一次以防中間狀態變了）
            var inv = InventorySystem.Instance;
            if (inv == null) { res.Reason = "沒有背包系統"; return res; }
            if (inv.RemoveItem(pool.CostItemId, cost) > 0)
            {
                res.Reason = $"{MoneyName(pool)}不足";
                return res;
            }

            for (int i = 0; i < count; i++)
                res.ItemIds.Add(PickWeighted(candidates));

            // 允許重複：不從池裡移除、不做保底，抽到已經有的就是再拿一份。
            Grant(res.ItemIds);

            res.Ok = true;
            res.SpentAmount = cost;
            res.CostItemId = pool.CostItemId;
            return res;
        }

        static int PickWeighted(List<GachaRollEntry> candidates)
        {
            if (candidates == null || candidates.Count == 0) return 0;   // 保險：空池不該走到這（CanRoll 已擋），但別讓它丟例外
            int total = 0;
            for (int i = 0; i < candidates.Count; i++) total += Mathf.Max(1, candidates[i].Weight);
            int r = Random.Range(0, total);
            for (int i = 0; i < candidates.Count; i++)
            {
                r -= Mathf.Max(1, candidates[i].Weight);
                if (r < 0) return candidates[i].ItemId;
            }
            return candidates[candidates.Count - 1].ItemId;   // 浮點/邊界保險
        }

        // ───────────────────────── 發獎 ─────────────────────────

        // 走 RunProgress.GiveItem（取得物品的統一入口）：祭壇在廣場 → 直接進真背包；
        // 背包滿的部分掉在玩家腳下，比照觸發鏈 giveItem 的處理。
        static void Grant(List<int> itemIds)
        {
            if (itemIds == null) return;
            var inv = InventorySystem.Instance;
            int dropped = 0;   // 連抽時背包滿可能一次溢出好幾件，Toast 聚合成一條，不要連噴十次

            for (int i = 0; i < itemIds.Count; i++)
            {
                int id = itemIds[i];
                if (id <= 0) continue;

                int leftover = RunProgress.Exists ? RunProgress.Instance.GiveItem(id, 1)
                                                  : (inv != null ? inv.AddItem(id, 1) : 1);
                if (leftover <= 0) continue;

                var d = inv != null ? inv.GetData(id) : null;
                string display = d != null ? d.Name : $"#{id}";
                if (InteractionManager.Exists)
                {
                    var player = GameObject.FindGameObjectWithTag("Player");
                    Vector2 pos = player != null ? (Vector2)player.transform.position : Vector2.zero;
                    InteractionManager.Instance.DropLoot(id, leftover, pos);
                    dropped += leftover;
                }
                else
                {
                    Debug.LogWarning($"[GachaService] 背包滿且沒有 InteractionManager，{display} ×{leftover} 遺失。");
                }
            }

            if (dropped > 0)
                AlertPanel.Toast(dropped > 1 ? $"背包已滿，{dropped} 件掉落在腳下" : "背包已滿，抽到的東西掉落在腳下");
        }

        // ───────────────────────── 小工具 ─────────────────────────

        static bool ItemExists(int itemId)
        {
            var inv = InventorySystem.Instance;
            return inv != null && inv.GetData(itemId) != null;
        }

        static int CurrentCycle()
        {
            var sm = SaveManager.Instance;
            int c = sm != null ? sm.Cycle : 1;
            return c <= 0 ? 1 : c;
        }

        // 沿用觸發鏈的旗標語意（前綴 ! 表否定、「永久:」前綴走終身旗標）。
        static bool FlagOk(string requireFlag)
        {
            if (string.IsNullOrEmpty(requireFlag)) return true;
            string s = requireFlag.Trim();
            bool neg = s.StartsWith("!");
            string key = neg ? s.Substring(1).Trim() : s;
            if (key.Length == 0) return true;
            return TriggerChain.FlagTrue(key) != neg;
        }
    }
}
