using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Dipan.Inventory
{
    /// <summary>
    /// **生成規格**：給一件物品時，哪些東西寫死、哪些照規則骰（品質系統，2026-11-17，見 readme/EQUIPMENT.md §6）。
    ///
    /// 語法接在 itemId 後面的中括號，欄位用 <c>;</c> 分隔，**全部選填**：
    /// <code>
    ///   q=4            寫死品質 1~4（也收 一般／良品／珍品／極品、白藍黃紫）
    ///   s=1            寫死孔數 0~6
    ///   fx=2@2|1@1     寫死內建效果：效果ID@等級，多條用 |（效果 ID ＝ GemTable 的 GemID；等級 1~3，省略 @ ＝ Lv1）
    ///   lv=1           寫死珠子等級 1~3（只對能力珠有意義）
    /// </code>
    /// 例：<c>[q=3]</c> 珍品、兩條效果隨機；<c>[fx=2@1]</c> 良品、固定穿透 Lv1；<c>[q=1;s=1]</c> 一般 1 孔；不寫 ＝ 全隨機。
    ///
    /// 這裡**只管語法**（格式、範圍、條數有沒有矛盾）；「這個效果能不能長在這件裝備上」要看物品，
    /// 由 <see cref="ItemQuality"/> 在產生時判斷。兌換表、掉落表、劇本 giveItem、拾取點、作弊面板全部共用這一個解析器。
    /// </summary>
    public class ItemSpec
    {
        /// <summary>寫死品質 1~4；-1 ＝ 照規則骰。</summary>
        public int Quality = -1;
        /// <summary>寫死孔數 0~6；-1 ＝ 照規則骰。</summary>
        public int Sockets = -1;
        /// <summary>寫死的內建效果；null ＝ 不寫死（照品質骰）。</summary>
        public List<Affix> Effects;
        /// <summary>寫死珠子等級 1~3；-1 ＝ 照規則骰。</summary>
        public int GemLevel = -1;

        /// <summary>什麼都沒寫死（＝全隨機）。</summary>
        public bool IsEmpty => Quality < 0 && Sockets < 0 && Effects == null && GemLevel < 0;

        /// <summary>
        /// 寫死的效果決定的品質（效果條數＋1）；沒寫死效果回 -1。
        /// 有寫 q 就以 q 為準（q 比效果條數多時，差的條數會隨機補）。
        /// </summary>
        public int QualityFromEffects => Effects != null ? Effects.Count + 1 : -1;

        // ───────────────────────── 解析 ─────────────────────────

        /// <summary>
        /// 解析一段規格。可以帶或不帶外層中括號；空字串 ＝ 全隨機（回傳 true、spec 為空規格）。
        /// 失敗回 false，<paramref name="error"/> 是給人看的中文原因。
        /// </summary>
        public static bool TryParse(string text, out ItemSpec spec, out string error)
        {
            spec = new ItemSpec();
            error = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            string s = text.Trim();
            if (s.StartsWith("[")) s = s.Substring(1);
            if (s.EndsWith("]")) s = s.Substring(0, s.Length - 1);
            s = s.Replace('；', ';').Replace('＝', '=').Replace('｜', '|');

            foreach (var rawPart in s.Split(';'))
            {
                string part = rawPart.Trim();
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0) { error = $"「{part}」少了等號（應該像 q=3）"; return false; }
                string key = part.Substring(0, eq).Trim().ToLowerInvariant();
                string val = part.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "q":
                        if (!TryParseQuality(val, out spec.Quality)) { error = $"品質 q=「{val}」看不懂（填 1~4 或 一般／良品／珍品／極品）"; return false; }
                        break;

                    case "s":
                        if (!int.TryParse(val, out spec.Sockets) || spec.Sockets < 0 || spec.Sockets > ItemInstance.SocketMax)
                        { error = $"孔數 s=「{val}」要是 0~{ItemInstance.SocketMax}"; return false; }
                        break;

                    case "lv":
                        if (!int.TryParse(val, out spec.GemLevel) || spec.GemLevel < 1 || spec.GemLevel > 3)
                        { error = $"珠子等級 lv=「{val}」要是 1~3"; return false; }
                        break;

                    case "fx":
                        if (!TryParseEffects(val, out spec.Effects, out error)) return false;
                        break;

                    default:
                        error = $"不認得的欄位「{key}」（只有 q／s／fx／lv）";
                        return false;
                }
            }

            if (spec.Quality > 0 && spec.Effects != null && spec.Effects.Count > spec.Quality - 1)
            {
                error = $"q={spec.Quality} 最多 {spec.Quality - 1} 條效果，fx 卻寫了 {spec.Effects.Count} 條";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 解析；失敗時印警告並回傳 null（呼叫端當成「全隨機」處理）。<paramref name="context"/> 寫在警告裡方便找是哪一列。
        /// </summary>
        public static ItemSpec Parse(string text, string context = null)
        {
            if (TryParse(text, out var spec, out var err)) return spec.IsEmpty ? null : spec;
            Debug.LogWarning($"[ItemSpec] {(string.IsNullOrEmpty(context) ? "" : context + "：")}規格「{text}」有誤：{err}。這一件改成全隨機。");
            return null;
        }

        /// <summary>
        /// 把「後面接著規格」的一段字切開：<c>"10003:100:1[q=3]"</c> → 回傳 <c>"10003:100:1"</c>、specText ＝ <c>"q=3"</c>。
        /// 沒有中括號 → 原字串照回、specText ＝ null。掉落表、兌換表這類「一格裡寫 id 與規格」的欄位用。
        /// </summary>
        public static string SplitTail(string token, out string specText)
        {
            specText = null;
            if (string.IsNullOrEmpty(token)) return token;
            int open = token.IndexOf('[');
            if (open < 0) return token;
            int close = token.LastIndexOf(']');
            specText = close > open ? token.Substring(open + 1, close - open - 1) : token.Substring(open + 1);
            return token.Substring(0, open).Trim();
        }

        static bool TryParseQuality(string v, out int q)
        {
            if (int.TryParse(v, out q)) return q >= 1 && q <= 4;
            switch (v)
            {
                case "白": case "一般": case "凡品": q = 1; return true;
                case "藍": case "蓝": case "良品": q = 2; return true;
                case "黃": case "黄": case "珍品": q = 3; return true;
                case "紫": case "極品": case "极品": q = 4; return true;
            }
            q = -1;
            return false;
        }

        static bool TryParseEffects(string v, out List<Affix> list, out string error)
        {
            list = new List<Affix>();
            error = null;
            if (string.IsNullOrWhiteSpace(v)) return true;   // fx= 空 ＝ 寫死「沒有效果」（一般）

            foreach (var raw in v.Split('|'))
            {
                string t = raw.Trim();
                if (t.Length == 0) continue;
                int at = t.IndexOf('@');
                string idText = at >= 0 ? t.Substring(0, at) : t;
                string lvText = at >= 0 ? t.Substring(at + 1) : "1";
                if (!int.TryParse(idText.Trim(), out int id) || id <= 0)
                { error = $"效果「{t}」的 ID 看不懂（要像 2@1，ID 是 GemTable 的 GemID）"; return false; }
                if (!int.TryParse(lvText.Trim(), out int lv) || lv < 1 || lv > 3)
                { error = $"效果「{t}」的等級要是 1~3"; return false; }
                for (int i = 0; i < list.Count; i++)
                    if (list[i].effectId == id) { error = $"效果 {id} 寫了兩次（同一件不能有兩條同種效果）"; return false; }
                list.Add(new Affix { effectId = id, level = lv });
            }
            if (list.Count > ItemInstance.AffixMax)
            { error = $"fx 寫了 {list.Count} 條，一件最多 {ItemInstance.AffixMax} 條"; return false; }
            return true;
        }

        /// <summary>寫回語法字串（不含中括號）；空規格回空字串。</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Quality > 0) sb.Append("q=").Append(Quality);
            if (Sockets >= 0) { if (sb.Length > 0) sb.Append(';'); sb.Append("s=").Append(Sockets); }
            if (Effects != null)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append("fx=");
                for (int i = 0; i < Effects.Count; i++)
                {
                    if (i > 0) sb.Append('|');
                    sb.Append(Effects[i].effectId).Append('@').Append(Effects[i].level);
                }
            }
            if (GemLevel > 0) { if (sb.Length > 0) sb.Append(';'); sb.Append("lv=").Append(GemLevel); }
            return sb.ToString();
        }
    }
}
