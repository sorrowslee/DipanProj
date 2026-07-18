namespace Dipan.Save
{
    /// <summary>存檔系統的共用常數。見 readme/SAVE_SYSTEM.md。</summary>
    public static class SaveConstants
    {
        /// <summary>目前存檔結構版本。改動結構時 +1，並在 SaveSystem 補遷移（見文件 §7）。</summary>
        /// <remarks>v2（2026-07-03）：ProgressDTO 加關卡進度（clearedModules / inheritedItems / hubIntroSpawnDone）。</remarks>
        public const int CurrentSchemaVersion = 2;

        /// <summary>沒名字的測試角色預設名。正式建角 UI 接上後由玩家輸入覆蓋。</summary>
        public const string DefaultTestCharacterName = "test001";

        // ── 進度／流程設定（可調；關卡數量或編號要改就改這裡）──

        /// <summary>邪佛廣場（大廳）在 MapsTable 的 Map ID。繼續遊戲、自動存檔點都指向它。</summary>
        public const int HubMapId = 12;   // Main_Square

        /// <summary>廣場/教學所屬的 module 名。此 module 的地圖（廣場、初始洞窟）不算「關卡」——
        /// 取得的道具直接進真背包、不走臨時包、不做關卡進度持久化。其餘 module 一律視為關卡。見 RunProgress。</summary>
        public const string HubModule = "Main";

        /// <summary>輪迴帶入物品的上限。第 N 次輪迴帶 min(周目, 上限) 件，達上限後恆為上限。</summary>
        public const int MaxCarryOnReincarnate = 7;

        /// <summary>完成幾個關卡後，邪佛要求對決（此後不能再進一般關卡）。boss／最終關另計，見文件。</summary>
        public const int LevelsToUnlockBoss = 7;

        // ── 存檔欄位（槽位）──

        /// <summary>存讀檔畫面的欄位數（一欄 = 一條獨立進度線 = 一個角色）。</summary>
        public const int SlotCount = 3;

        // ── 邪佛廣場出生點（Main_Square 地圖裡的兩個具名落點；名稱要與地圖編輯器裡設的一致）──

        /// <summary>首次由開場鏈抵達廣場時的落點：下方洞穴出口。</summary>
        public const string HubEntranceCaveExit = "caveExit";

        /// <summary>之後（繼續遊戲／從關卡回來／輪迴）抵達廣場時的落點：廣場中央。</summary>
        public const string HubEntranceCenter = "center";

        /// <summary>新建遊戲要播的開場場景名（需加入 Build Settings）。留空 = 不播開場、直接進廣場（方便測試）。</summary>
        public const string IntroSceneName = "Intro";

        /// <summary>遊戲本體場景名（含 MapManager / 玩家）。</summary>
        public const string MainSceneName = "MainScene";
    }
}
