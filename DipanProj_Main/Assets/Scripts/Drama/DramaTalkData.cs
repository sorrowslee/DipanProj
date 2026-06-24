using UnityEngine;

namespace Dipan.Drama
{
    /// <summary>一句頭像對話。對應 DramaTalkTable.csv 一列。
    /// 同一個 <see cref="Group"/> = 一串對話；同群組內依 <see cref="Id"/>（流水號）由小到大依序播放。</summary>
    public class DramaTalkData
    {
        public int Id;            // 流水號（唯一；同群組內由小到大播放）
        public int Group;         // 群組編號（一串對話 = 同一群組；DramaTable Type=2 的 TalkGroup 指向這裡）
        public string Name;       // 說話人姓名（對話介面顯示在姓名底版上）
        public string AvatarPath; // 頭像檔案路徑（字串）
        public int Side;          // 頭像位置：1 = 置左、2 = 置右
        public string Text;       // 對話內容（CSV 內 \n 會轉成換行）

        public Sprite Avatar;     // 載好的頭像（目前未載入＝null，TalkPanel 會自動隱藏；待頭像圖+載入路徑定案後補，見 readme/TODO.md）
    }
}
