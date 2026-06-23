using UnityEngine;

namespace Dipan.Drama
{
    /// <summary>一段劇情的資料：一張圖 + 一段文字。對應 DramaTable.csv 一列。</summary>
    public class DramaData
    {
        public int ID;
        public string ImagePath;   // 相對 Resources、不含副檔名（例：UI/Drama/drama_sample）
        public string Text;        // 內文（CSV 內 \n 會轉成換行）
        public Sprite Image;       // 由 DramaDatabase 從 Resources 載入（可能為 null）
    }
}
