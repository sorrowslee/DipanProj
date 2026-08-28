using System.Collections.Generic;

namespace DipanMapEditor.Data
{
    /// <summary>
    /// 一個 NPC 擺放實例（「NPC」分頁放置）。與主遊戲 <c>Dipan.MapRuntime.NpcInstance</c> **鏡像**，
    /// 改欄位要兩邊一起改。種類資料（外觀資料夾/縮放/速度…）查主專案 <c>Assets/Data/NpcTable.csv</c>
    /// （編輯器直讀磁碟，見 Preview/NpcTableEditor）。座標為世界座標。
    /// 行為：idle＝原地不動；patrol＝沿 [站位→waypoints…] 乒乓來回。未來擴充（護送 escort…）＝加 behavior 值。
    /// </summary>
    public class NpcInstance
    {
        public const string BehaviorIdle = "idle";
        public const string BehaviorPatrol = "patrol";

        /// <summary>系統生成的短 id（觸發鏈/記錄保留用）。</summary>
        public string id;
        /// <summary>好認的名字（清單顯示用；空＝顯示 NPC 種類名）。</summary>
        public string name = "";
        /// <summary>NpcTable.csv 的 ID（決定外觀/預設速度等）。</summary>
        public int npcId;
        /// <summary>站位（世界座標）。</summary>
        public float x, y;
        /// <summary>行為：idle / patrol。</summary>
        public string behavior = BehaviorIdle;
        /// <summary>patrol 的路徑點（不含站位；NPC 走 站位→wp1→…→尾端 再倒回）。</summary>
        public List<Vec2> waypoints = new List<Vec2>();
        /// <summary>走動速度覆寫；0＝用 NpcTable 的 Speed。</summary>
        public float speed = 0f;
        /// <summary>抵達每個路徑點停留秒數（0＝不停留）。</summary>
        public float dwellSeconds = 0f;
        /// <summary>按 F 交談播的對話（DramaTable 的 ID；0＝不對話）。</summary>
        public int dramaId = 0;
        /// <summary>對話結束後開的介面（沒填 dramaId 時＝按 F 直接開）。目前可填：gacha。</summary>
        public string panelId = "";
        /// <summary>介面參數（例：抽選池代號；未來買賣介面＝shopId）。</summary>
        public string panelArg = "";
        /// <summary>對話結束後啟動的 trigger 名稱（同觸發鏈 next；每次進圖只跑第一次）。</summary>
        public string next = "";
        /// <summary>對話結束後寫的旗標（同觸發鏈 setFlag；每次進圖只跑第一次）。</summary>
        public string setFlag = "";
    }
}
