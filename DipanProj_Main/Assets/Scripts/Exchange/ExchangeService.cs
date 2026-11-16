using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Dipan.Inventory;
using Dipan.Save;
using Dipan.UI;

namespace Dipan.Exchange
{
    /// <summary>
    /// 兌換的資料層：算買不買得起、扣款、發貨、收藏。**完全不碰 UI 版面**（面板只呼叫這裡）。
    /// 發貨走 <c>RunProgress.GiveItem</c>（取得物品的統一入口），背包滿的部分掉在玩家腳下——比照抽選。
    /// </summary>
    public static class ExchangeService
    {
        /// <summary>一次兌換的數量上限（面板的數量選擇器也用它）。</summary>
        public const int MaxQuantity = 99;

        // 語言表
        const int TxtNotEnough = 10042;      // {0}不足
        const int TxtNotOpen = 10049;        // 尚未開放
        const int TxtBagFull = 10050;        // 背包已滿，{0} 件掉落在腳下

        // ───────────────────────── 買得起嗎 ─────────────────────────

        /// <summary>買 <paramref name="qty"/> 個要付多少（每種貨幣分開列）。</summary>
        public static List<CostPart> TotalCost(ExchangeEntry e, int qty)
        {
            var list = new List<CostPart>();
            if (e == null) return list;
            for (int i = 0; i < e.Cost.Count; i++)
                list.Add(new CostPart { CurrencyKey = e.Cost[i].CurrencyKey, Amount = e.Cost[i].Amount * Mathf.Max(1, qty) });
            return list;
        }

        /// <summary>能不能買 <paramref name="qty"/> 個；不能時 reason 是給玩家看的理由。</summary>
        public static bool CanAfford(ExchangeEntry e, int qty, out string reason)
        {
            reason = null;
            if (e == null || qty <= 0) { reason = ""; return false; }
            if (!e.CurrenciesAvailable) { reason = Dipan.Localization.Language.GetText(TxtNotOpen); return false; }
            for (int i = 0; i < e.Cost.Count; i++)
            {
                var c = e.Cost[i].Currency;
                long need = (long)e.Cost[i].Amount * qty;
                if (c.HeldNow < need)
                {
                    reason = string.Format(Dipan.Localization.Language.GetText(TxtNotEnough), c.Name);
                    return false;
                }
            }
            return true;
        }

        public static bool CanAfford(ExchangeEntry e, int qty = 1) => CanAfford(e, qty, out _);

        /// <summary>目前最多買得起幾個（上限 <see cref="MaxQuantity"/>；買不起回 0）。</summary>
        public static int MaxAffordable(ExchangeEntry e)
        {
            if (e == null || !e.CurrenciesAvailable) return 0;
            int max = MaxQuantity;
            for (int i = 0; i < e.Cost.Count; i++)
            {
                int unit = e.Cost[i].Amount;
                if (unit <= 0) continue;   // 免費的那一段不限制數量
                max = Mathf.Min(max, e.Cost[i].Currency.HeldNow / unit);
            }
            return Mathf.Max(0, max);
        }

        // ───────────────────────── 兌換 ─────────────────────────

        /// <summary>
        /// 兌換 <paramref name="qty"/> 個。成功回 true（錢已扣、東西已發）；失敗回 false 且什麼都沒動，reason 給玩家看。
        /// 多種貨幣一起付時：先全部檢查過再依序扣，萬一中途某一種扣失敗，前面扣過的會退回。
        /// </summary>
        public static bool Buy(ExchangeEntry e, int qty, out string reason)
        {
            qty = Mathf.Clamp(qty, 1, MaxQuantity);
            if (!CanAfford(e, qty, out reason)) return false;

            var paid = new List<CostPart>();
            var total = TotalCost(e, qty);
            for (int i = 0; i < total.Count; i++)
            {
                var c = total[i].Currency;
                if (total[i].Amount <= 0) continue;
                if (!c.TrySpend(total[i].Amount))
                {
                    for (int k = 0; k < paid.Count; k++) paid[k].Currency.Refund?.Invoke(paid[k].Amount);
                    reason = string.Format(Dipan.Localization.Language.GetText(TxtNotEnough), c.Name);
                    return false;
                }
                paid.Add(total[i]);
            }

            Grant(e.ItemId, qty);
            Debug.Log($"[ExchangeService] 兌換 {e.Name} ×{qty}（{CostText(total)}）");
            return true;
        }

        // 發貨：比照 GachaService.Grant——走 RunProgress.GiveItem，背包滿的掉在玩家腳下。
        static void Grant(int itemId, int qty)
        {
            var inv = InventorySystem.Instance;
            int leftover = RunProgress.Exists ? RunProgress.Instance.GiveItem(itemId, qty)
                                              : (inv != null ? inv.AddItem(itemId, qty) : qty);
            int got = qty - leftover;
            if (got > 0) AlertPanel.ToastItem(itemId, got);
            if (leftover <= 0) return;

            if (InteractionManager.Exists)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                Vector2 pos = player != null ? (Vector2)player.transform.position : Vector2.zero;
                InteractionManager.Instance.DropLoot(itemId, leftover, pos);
                AlertPanel.Toast(string.Format(Dipan.Localization.Language.GetText(TxtBagFull), leftover));
            }
            else
            {
                Debug.LogWarning($"[ExchangeService] 背包滿且沒有 InteractionManager，物品 {itemId} ×{leftover} 遺失。");
            }
        }

        /// <summary>「500 銅錢＋2 勳章」這種文字（log 與提示用）。</summary>
        public static string CostText(List<CostPart> cost)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cost.Count; i++)
            {
                if (i > 0) sb.Append("＋");
                var c = cost[i].Currency;
                sb.Append(cost[i].Amount).Append(' ').Append(c != null ? c.Name : cost[i].CurrencyKey);
            }
            return sb.ToString();
        }

        // ───────────────────────── 收藏 ─────────────────────────
        // 存在角色的終身旗標（lifetimeFlags）裡：跨輪迴保留，換角色才清空。值 ＝ 以分號分隔的 ExchangeId。

        const string FavFlagKey = "兌換收藏";
        static HashSet<int> _fav;
        static string _favRaw;   // 上次解析的原始字串：存檔裡的值變了（換角色、讀檔）就重新解析

        public static void ResetForPlayMode() { _fav = null; _favRaw = null; }

        static HashSet<int> Fav
        {
            get
            {
                var sm = SaveManager.Instance;
                string raw = sm != null ? sm.GetLifetimeFlagValue(FavFlagKey) : null;
                if (_fav != null && raw == _favRaw) return _fav;
                _fav = new HashSet<int>();
                _favRaw = raw;
                if (!string.IsNullOrEmpty(raw))
                    foreach (var s in raw.Split(';'))
                        if (int.TryParse(s.Trim(), out int id)) _fav.Add(id);
                return _fav;
            }
        }

        public static bool IsFavorite(ExchangeEntry e) => e != null && Fav.Contains(e.ExchangeId);

        /// <summary>切換收藏，回傳切換後的狀態。</summary>
        public static bool ToggleFavorite(ExchangeEntry e)
        {
            if (e == null) return false;
            var set = Fav;
            bool now = !set.Remove(e.ExchangeId);
            if (now) set.Add(e.ExchangeId);
            _favRaw = string.Join(";", set);
            var sm = SaveManager.Instance;
            if (sm != null) sm.SetLifetimeFlag(FavFlagKey, _favRaw);
            return now;
        }
    }
}
