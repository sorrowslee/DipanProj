using System.Collections.Generic;

namespace Dipan.MapRuntime
{
    /// <summary>
    /// 劇情演出資料（runtime 端鏡像，欄位與 DipanProj_MapEditor 的 Cutscene 一致）。
    /// 掛在 MapData.cutscenes（清單）；主遊戲端只讀，由 CutsceneDirector 依序執行 steps。
    /// 座標為世界座標。詳細欄位語意見編輯器端 CutsceneData.cs 註解。
    /// </summary>
    public class Cutscene
    {
        public string id = "";            // 演出識別名（觸發鏈 playCutscene 用 cutsceneId 指定；空＝第一段）
        public bool autoStartOnEnter = true;
        public string requireFlag = "";   // 播放條件旗標（"!name"＝否定）；留空＝不檢查。自動播與 playCutscene 都吃
        public string setFlag = "";       // 演完（含被略過）要設成立的旗標；留空＝不寫
        public bool skippable = true;
        public bool lockInput = true;
        public bool memoryFx = false;     // 回憶特效：整段套「泛黃老照片＋柔邊暈影」後處理，演完自動移除
        public bool hidePlayer = false;   // 演出期間隱藏主角（影子/碰撞/身上光源一起關），收尾現身回原位
        public bool hideHud = false;      // 演出期間關閉底部血量 HUD，收尾恢復
        public List<CutsceneActor> actors = new List<CutsceneActor>();
        public List<CutsceneStep> steps = new List<CutsceneStep>();
    }

    /// <summary>劇情演員。kind="npc"（路線 B 逐格動畫）/ "player"（接管場上玩家）。</summary>
    public class CutsceneActor
    {
        public string id;
        public string kind = "npc";
        public string spriteFolder = "";
        public float x, y;
        public string facing = "down";
        public bool spawnAtStart = true;
        public bool flying = false;   // 飛行：直線移動、不受可走層
        public float scale = 1f;
        public float animFps = 8f;
    }

    /// <summary>
    /// 演出步驟。type：move/face/dialogue/bubble/wait/camera/cameraFollow/comic/fade/spawn/despawn/screenFx/setFlag/end。
    /// 預設做完才進下一步；parallelNext=true 與下一步並行。角色僅左右兩向。欄位語意見編輯器端 CutsceneData.cs。
    /// </summary>
    public class CutsceneStep
    {
        public string type = "wait";
        public string actorId = "";
        public bool hasPos = false;
        public float x, y;
        public string facing = "";      // left / right
        public int dramaId = 0;         // dialogue → DramaTable.csv
        public int langId = 0;          // bubble → LanguageTable.csv 的 id（頭上對話框的文字）
        public float seconds = 0f;
        public float speed = 0f;        // move：走路速度（格/秒）；0＝用預設
        public string assetId = "";
        public float zoom = 1f;
        public string flag = "";
        public bool parallelNext = false;
        public bool background = false;   // 背景執行：啟動後不擋後續
    }
}
