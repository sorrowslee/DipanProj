using System.IO;
using UnityEngine;

namespace Dipan.Save
{
    /// <summary>
    /// 集中算所有存檔路徑——唯一碰 Application.persistentDataPath 的地方。
    /// Win: %userprofile%\AppData\LocalLow\&lt;Company&gt;\&lt;Product&gt;\saves\
    /// Mac: ~/Library/Application Support/&lt;Company&gt;/&lt;Product&gt;/saves/
    /// 見 readme/SAVE_SYSTEM.md §2。
    /// </summary>
    public static class SavePaths
    {
        public const string SavesDir = "saves";
        public const string ProfilesFile = "profiles.json";
        public const string CharacterFile = "character.json";
        public const string SettingsFile = "settings.json";
        public const string CharDirPrefix = "char_";

        /// <summary>saves/ 根目錄。</summary>
        public static string Root => Path.Combine(Application.persistentDataPath, SavesDir);

        /// <summary>角色名冊。</summary>
        public static string ProfilesPath => Path.Combine(Root, ProfilesFile);

        /// <summary>全域設定（不屬於任何角色，故放在 persistentDataPath 根而非 saves/）。</summary>
        public static string SettingsPath => Path.Combine(Application.persistentDataPath, SettingsFile);

        /// <summary>某角色的資料夾。</summary>
        public static string CharDir(string characterId) => Path.Combine(Root, CharDirPrefix + characterId);

        /// <summary>某角色的存檔主檔。</summary>
        public static string CharacterPath(string characterId) => Path.Combine(CharDir(characterId), CharacterFile);

        public static void EnsureRoot() => Directory.CreateDirectory(Root);
        public static void EnsureCharDir(string characterId) => Directory.CreateDirectory(CharDir(characterId));
    }
}
