using UnityEngine;

namespace Dipan.Drama
{
    /// <summary>一段劇情的資料：一張圖 + 一段文字。對應 DramaTable.csv 一列。</summary>
    public class DramaData
    {
        public int ID;
        public string ImagePath;   // catalog id：相對 GameAssets 的路徑、不含副檔名（例：Modules/RedBridalGown/Drama/drama_sample）
        public string Text;        // 內文（CSV 內 \n 會轉成換行）
        public Sprite Image;       // 由 DramaDatabase 從 Resources 載入（可能為 null）
    }
}
