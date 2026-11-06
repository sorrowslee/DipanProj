using UnityEngine;
using Dipan.Localization;

namespace Dipan.Drama
{
    /// <summary>一句頭像對話。對應 DramaTalkTable.csv 一列。
    /// 同一個 <see cref="Group"/> = 一串對話；同群組內依 <see cref="Id"/>（流水號）由小到大依序播放。
    ///
    /// 雙立繪：一句可同時擺左、右兩個立繪（<see cref="LeftAvatarPath"/> / <see cref="RightAvatarPath"/>），
    /// <see cref="SpotlightSide"/> 指定「現在誰在說話」——聚光側立繪正常亮、另一側壓暗（背光感），
    /// 姓名牌匾也擺在聚光側、顯示 <see cref="Name"/>（說話者）。任一側留空 = 那側不顯示立繪。
    ///
    /// 立繪路徑兩種寫法（解析見 <see cref="DramaTalkDatabase"/>）：
    ///   1) catalog id（相對 GameAssets、不含副檔名，例 Modules/RedBridalGown/Talk/redBridalGown）。
    ///   2) <c>Actor_&lt;情緒&gt;</c>（例 Actor_Angry）＝主角情緒立繪：依目前血統載
    ///      GameAssets/Main/Characters/Talk/&lt;血統&gt;/&lt;情緒&gt;.png（情緒大小寫不拘）。</summary>
    public class DramaTalkData
    {
        public int Id;               // 流水號（唯一；同群組內由小到大播放）
        public int Group;            // 群組編號（一串對話 = 同一群組；DramaTable Type=2 的 TalkGroup 指向這裡）
        public string Name;          // 說話人姓名（顯示在聚光側姓名牌匾上）
        public string LeftAvatarPath;  // 左側立繪路徑（catalog id 或 Actor_<情緒>）；留空 = 左側無立繪
        public string RightAvatarPath; // 右側立繪路徑（catalog id 或 Actor_<情緒>）；留空 = 右側無立繪
        public int SpotlightSide;    // 聚光側（說話者）：1 = 左、2 = 右（其餘 / 留空視為 1）
        public string Text;          // 對話內容原始欄值（CSV 內 \n 會轉成換行）；可填 lang:<id> 走語言表。顯示一律用 DisplayText

        /// <summary>
        /// 要顯示給玩家的台詞（2026-11-06）。<see cref="Text"/> 寫成 <c>lang:&lt;id&gt;</c>（例 <c>lang:3011</c>）
        /// ＝取 <c>LanguageTable.csv</c> 該 id 的「當前語言」字串；其餘照原樣當明文（舊資料向下相容）。
        /// 每次讀取即時查表，所以切語言立即生效、不必重載對話表。
        /// </summary>
        public string DisplayText => Localize(Text);

        /// <summary>
        /// 欄值 → 顯示字串：<c>lang:&lt;id&gt;</c>（前綴不分大小寫、前後空白忽略）→ <see cref="Language.GetText(int)"/>；
        /// 前綴後面不是整數就當明文原樣顯示（寫錯一眼看得出來）；找不到 id 由 GetText 回 <c>[lang:id]</c> 佔位。
        /// </summary>
        public static string Localize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            string t = raw.Trim();
            if (t.Length > 5 && t.StartsWith("lang:", System.StringComparison.OrdinalIgnoreCase)
                && int.TryParse(t.Substring(5).Trim(), out int id))
                return Language.GetText(id);
            return raw;
        }

        // （2026-10-26 刪除逐句立繪微調欄 LeftScale/LeftOffsetX/…：立繪的大小與位置只在 PortraitTable.csv 依「圖」維護）

        public Sprite LeftAvatar;    // 載好的左立繪（解析失敗 / 留空＝null，TalkPanel 自動隱藏）
        public Sprite RightAvatar;   // 載好的右立繪（同上）

        // 立繪排版資料（與 Avatar 同時解析）：人物在圖檔裡的實際位置/大小 ＋ 該張圖的固定微調。
        // TalkPanel 用它把人物對齊到固定落點，吸收掉各張素材留白與畫布比例的差異。見 PortraitFit。
        public PortraitFit LeftFit = PortraitFit.None;
        public PortraitFit RightFit = PortraitFit.None;
    }
}
