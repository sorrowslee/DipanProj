using System.Collections.Generic;

namespace Dipan.MapRuntime
{
    /// <summary>
    /// 劇情演出資料（runtime 端鏡像，欄位與 DipanProj_MapEditor 的 Cutscene 一致）。
    /// 掛在 MapData.cutscene；主遊戲端只讀，由 CutsceneDirector 依序執行 steps。
    /// 座標為世界座標。詳細欄位語意見編輯器端 CutsceneData.cs 註解。
    /// </summary>
    public class Cutscene
    {
        public bool autoStartOnEnter = true;
        public bool skippable = true;
        public bool lockInput = true;
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
        public float scale = 1f;
        public float animFps = 8f;
    }

    /// <summary>
    /// 演出步驟。type：move/face/dialogue/wait/camera/cameraFollow/comic/spawn/despawn/screenFx/setFlag/end。
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
        public float seconds = 0f;
        public string assetId = "";
        public float zoom = 1f;
        public string flag = "";
        public bool parallelNext = false;
    }
}
