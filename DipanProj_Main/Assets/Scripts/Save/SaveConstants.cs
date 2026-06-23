namespace Dipan.Save
{
    /// <summary>存檔系統的共用常數。見 readme/SAVE_SYSTEM.md。</summary>
    public static class SaveConstants
    {
        /// <summary>目前存檔結構版本。改動結構時 +1，並在 SaveSystem 補遷移（見文件 §7）。</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>沒名字的測試角色預設名。正式建角 UI 接上後由玩家輸入覆蓋。</summary>
        public const string DefaultTestCharacterName = "test001";
    }
}
