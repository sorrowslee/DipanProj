using UnityEngine;

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
        public string Text;          // 對話內容（CSV 內 \n 會轉成換行）

        public Sprite LeftAvatar;    // 載好的左立繪（解析失敗 / 留空＝null，TalkPanel 自動隱藏）
        public Sprite RightAvatar;   // 載好的右立繪（同上）
    }
}
