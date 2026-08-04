using UnityEngine;

namespace Dipan.Rules
{
    /// <summary>
    /// 全遊戲**所有機率**的唯一來源。
    ///
    /// 刻意寫在程式裡而不是做成 CSV：這個遊戲的機率設定會非常多、而且彼此有條件關係（周目、關卡深度、
    /// 稀有度…），用表格記錄不完也不好讀。集中在這一個類別，要調就調下面「調整區」，
    /// 不必翻遍整個專案找散落的 Random.Range。
    ///
    /// 【調整原則】
    ///   - 所有數字集中在檔案最上方的「調整區」，每一條都要有中文註解說明它影響什麼。
    ///   - 對外一律提供語意化的方法（RollSocketCount / RollGemLevel…），呼叫端不該自己算權重。
    ///   - 新增一種隨機時，在調整區加權重、在下面加一個對應方法。
    ///
    /// 見 readme/GEM_SOCKET.md。
    /// </summary>
    public static class RandomRules
    {
        // ══════════════════════════════ 調整區 ══════════════════════════════

        /// <summary>
        /// 裝備掉落時的「孔數」權重，索引 = 孔數（0~6）。
        /// 目前是暫定的平均分配（各 1/7 ≈ 14%）。
        /// 之後要做「第一周目不給高孔數」，改 <see cref="SocketCountWeightsByCycle"/> 那張表即可。
        /// </summary>
        static readonly int[] SocketCountWeights = { 14, 14, 14, 14, 14, 14, 14 };

        /// <summary>
        /// 依周目覆寫孔數權重（索引 0 = 第 1 周目、索引 1 = 第 2 周目…）。
        /// 長度不足時，超出的周目一律用最後一列。整個陣列留空 = 一律用 <see cref="SocketCountWeights"/>。
        ///
        /// 【怎麼調】想讓第 1 周目最多只出到 2 孔，就填：
        ///   { new[]{ 40, 35, 25, 0, 0, 0, 0 }, ... }
        /// </summary>
        static readonly int[][] SocketCountWeightsByCycle = null;

        /// <summary>能力珠掉落時的「等級」權重，索引 0 = Lv1、1 = Lv2、2 = Lv3。目前平均分配。</summary>
        static readonly int[] GemLevelWeights = { 1, 1, 1 };

        /// <summary>依周目覆寫珠子等級權重。規則同上，null = 一律用 <see cref="GemLevelWeights"/>。</summary>
        static readonly int[][] GemLevelWeightsByCycle = null;

        // ═══════════════════════════ 對外的骰法 ═══════════════════════════

        /// <summary>骰一件裝備有幾個孔（0 ~ ItemInstance.SocketMax）。cycle = 周目（1 起算）。</summary>
        public static int RollSocketCount(int cycle = 1)
        {
            int[] w = PickByCycle(SocketCountWeightsByCycle, SocketCountWeights, cycle);
            return Mathf.Clamp(WeightedPick(w), 0, Dipan.Inventory.ItemInstance.SocketMax);
        }

        /// <summary>
        /// 骰出孔位佈局：長度 = SocketMax 的布林陣列，true = 這個孔有開。
        /// **開哪幾個孔是隨機位置**（例如 2 孔武器可能開的是第 1、4 孔），不是固定開前面幾個。
        /// </summary>
        public static bool[] RollSocketLayout(int cycle = 1) => LayoutFor(RollSocketCount(cycle));

        /// <summary>指定孔數，隨機挑位置開（給作弊面板/測試用）。</summary>
        public static bool[] LayoutFor(int count)
        {
            int max = Dipan.Inventory.ItemInstance.SocketMax;
            var layout = new bool[max];
            count = Mathf.Clamp(count, 0, max);

            // 洗牌取前 count 個位置（Fisher-Yates）
            var idx = new int[max];
            for (int i = 0; i < max; i++) idx[i] = i;
            for (int i = max - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (idx[i], idx[j]) = (idx[j], idx[i]);
            }
            for (int i = 0; i < count; i++) layout[idx[i]] = true;
            return layout;
        }

        /// <summary>骰一顆能力珠的等級（1~3）。cycle = 周目（1 起算）。</summary>
        public static int RollGemLevel(int cycle = 1)
        {
            int[] w = PickByCycle(GemLevelWeightsByCycle, GemLevelWeights, cycle);
            return Mathf.Clamp(WeightedPick(w) + 1, 1, 3);
        }

        // ═══════════════════════════ 共用小工具 ═══════════════════════════

        /// <summary>依權重挑一個索引。權重全為 0 或陣列為空時回 0。</summary>
        public static int WeightedPick(int[] weights)
        {
            if (weights == null || weights.Length == 0) return 0;
            int total = 0;
            for (int i = 0; i < weights.Length; i++) if (weights[i] > 0) total += weights[i];
            if (total <= 0) return 0;

            int roll = Random.Range(0, total);
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0) continue;
                roll -= weights[i];
                if (roll < 0) return i;
            }
            return weights.Length - 1;
        }

        /// <summary>周目對照表取值：表為 null/空 → 用預設；周目超出表長 → 用最後一列。</summary>
        static int[] PickByCycle(int[][] table, int[] fallback, int cycle)
        {
            if (table == null || table.Length == 0) return fallback;
            int i = Mathf.Clamp(cycle - 1, 0, table.Length - 1);
            return table[i] ?? fallback;
        }
    }
}
