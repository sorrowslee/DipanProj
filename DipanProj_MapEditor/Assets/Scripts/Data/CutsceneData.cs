using System.Collections.Generic;

namespace DipanMapEditor.Data
{
    /// <summary>
    /// 一張地圖的「劇情演出」資料（半演出半漫畫的過場）。
    /// 掛在 MapData.cutscene（可為 null＝此圖沒有演出）。目前一張圖最多一段演出。
    /// 演員先在 actors 定義（起點/外觀），steps 依序引用演員做走位/說話/漫畫/鏡頭等動作。
    /// 座標一律為世界座標（與 SceneFxInstance 相同慣例）。
    /// </summary>
    public class Cutscene
    {
        /// <summary>一進此圖是否自動播放（第二張純演出圖用 true）。</summary>
        public bool autoStartOnEnter = true;
        /// <summary>是否可略過（略過＝直接跳到結尾交棒動作）。</summary>
        public bool skippable = true;
        /// <summary>演出期間是否鎖玩家操作（純演出段 true；含教學可控段用個別步驟控制）。</summary>
        public bool lockInput = true;

        /// <summary>此段演出用到的演員（含主角傀儡）。</summary>
        public List<CutsceneActor> actors = new List<CutsceneActor>();
        /// <summary>依序執行的步驟。</summary>
        public List<CutsceneStep> steps = new List<CutsceneStep>();
    }

    /// <summary>
    /// 一個劇情演員。steps 以 id 引用。
    /// kind="npc"：一般演員，外觀走路線 B 逐格動畫（spriteFolder＝序列圖資料夾名，含 idle/walk）。
    /// kind="player"：主角傀儡，直接接管場上玩家、不另生物件（spriteFolder 忽略）。
    /// </summary>
    public class CutsceneActor
    {
        public string id;
        public string kind = "npc";        // "npc" / "player"
        public string spriteFolder = "";   // npc 的序列圖資料夾名（血統/怪名同機制）
        public float x, y;                 // 起始世界座標（player 忽略，用玩家當前位置）
        public string facing = "down";     // 起始朝向：down/up/left/right
        public bool spawnAtStart = true;   // true＝開場就在場上；false＝等 spawn 步驟才出現
        public float scale = 1f;
        public float animFps = 8f;
    }

    /// <summary>
    /// 一個演出步驟。type 決定用到哪些欄位；未用到的欄位忽略。
    /// 步驟預設「做完才進下一步」；parallelNext=true 則與下一步同時開始（例：走位同時運鏡）。
    /// 角色只有左右兩向（flipX），故 facing 僅 "left"/"right" 有意義。
    /// type 一覽（v1）：
    ///   move         actorId 走到 (x,y)（A* 尋徑），抵達才繼續；facing＝抵達後朝向
    ///   face         actorId 轉向 facing（left/right）
    ///   dialogue     播 DramaTable 的 dramaId（沿用現有劇情系統：大圖或頭像對話），播完才繼續
    ///   wait         等 seconds 秒
    ///   camera       鏡頭移到 (x,y)、縮放 zoom；seconds＝過渡秒數
    ///   cameraFollow actorId 空＝跟玩家，否則跟該演員
    ///   comic        把 assetId 指定的圖放到「畫面正中央」，停 seconds 秒（此期間其他演員暫停）
    ///   spawn        actorId 現身（配合 spawnAtStart=false）
    ///   despawn      actorId 消失（假傳送離場；建議走到畫面邊緣再 despawn）
    ///   screenFx     播 assetId 螢幕特效（柵欄碎裂/淡黑…）；seconds＝停留
    ///   setFlag      設 flag=true
    ///   end          結束演出並交棒；assetId 指定去向（"fall"＝接墜落動畫、或目標 mapId）
    /// </summary>
    public class CutsceneStep
    {
        public string type = "wait";
        public string actorId = "";
        public bool hasPos = false;     // x,y 是否有效（move/camera 點放後為 true）
        public float x, y;
        public string facing = "";      // face / move 抵達後朝向：left / right（空＝依移動方向）
        public int dramaId = 0;         // dialogue：對應 DramaTable.csv 的 dramaId
        public float seconds = 0f;      // wait / camera 過渡 / comic 停留 / screenFx 停留
        public string assetId = "";     // comic 置中圖路徑id / screenFx effectId / end 去向
        public float zoom = 1f;         // camera 縮放
        public string flag = "";        // setFlag
        public bool parallelNext = false; // 與下一步同時開始
    }
}
