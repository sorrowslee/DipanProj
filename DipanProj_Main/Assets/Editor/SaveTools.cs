using UnityEditor;
using UnityEngine;
using Dipan.Save;

namespace Dipan.SaveEditor
{
    /// <summary>
    /// 存檔開發工具（Unity 上方選單 Project Tools）。見 readme/SAVE_SYSTEM.md §10。
    /// </summary>
    public static class SaveTools
    {
        [MenuItem("Project Tools/Save/Open Save Folder", priority = 200)]
        public static void OpenSaveFolder()
        {
            SavePaths.EnsureRoot();
            EditorUtility.RevealInFinder(SavePaths.Root);
            Debug.Log($"[SaveTools] 存檔資料夾：{SavePaths.Root}");
        }

        [MenuItem("Project Tools/Save/Print Save Path", priority = 201)]
        public static void PrintSavePath()
        {
            Debug.Log($"[SaveTools] persistentDataPath = {Application.persistentDataPath}\n" +
                      $"saves 根目錄 = {SavePaths.Root}\n" +
                      $"名冊 = {SavePaths.ProfilesPath}");
        }

        [MenuItem("Project Tools/Save/Wipe All Saves", priority = 220)]
        public static void WipeAllSaves()
        {
            if (!EditorUtility.DisplayDialog("清除所有存檔",
                    $"確定要刪除全部存檔嗎？\n\n{SavePaths.Root}\n\n此動作無法復原。", "刪除", "取消"))
                return;

            try
            {
                if (System.IO.Directory.Exists(SavePaths.Root))
                    System.IO.Directory.Delete(SavePaths.Root, true);
                if (System.IO.File.Exists(SavePaths.SettingsPath))
                    System.IO.File.Delete(SavePaths.SettingsPath);
                Debug.Log("[SaveTools] 已清除所有存檔。");
            }
            catch (System.Exception e) { Debug.LogError($"[SaveTools] 清除失敗：{e.Message}"); }
        }
    }
}
