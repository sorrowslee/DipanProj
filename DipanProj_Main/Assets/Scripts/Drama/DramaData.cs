using UnityEngine;

namespace Dipan.Drama
{
    /// <summary>一段劇情的資料。對應 DramaTable.csv 一列。
    /// Type=1：一張大圖 + 一段文字（DramaPanel）；Type=2：頭像對話（用 TalkGroup 連到 DramaTalkTable）。</summary>
    public class DramaData
    {
        public int ID;
        public string ImagePath;   // catalog id：相對 GameAssets 的路徑、不含副檔名（例：Modules/RedBridalGown/Drama/drama_sample）
        public string Text;        // 內文（CSV 內 \n 會轉成換行）
        public Sprite Image;       // 由 DramaDatabase 從 Resources 載入（可能為 null）

        public int Type = 1;       // 劇情類型：1 = 大圖+文字（預設）、2 = 頭像對話
        public int TalkGroup;      // Type=2 時：對應 DramaTalkTable.csv 的「群組編號」（Type=1 時不使用）
    }
}
