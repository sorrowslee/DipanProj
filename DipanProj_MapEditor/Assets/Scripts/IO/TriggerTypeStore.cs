using System.IO;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.IO
{
    /// <summary>
    /// Trigger 類型定義（triggerTypes.json）的讀寫。
    /// 存在 StreamingAssets 根（非 MapAssets，故進版控、不被同步腳本覆蓋）。
    /// 首次找不到檔時生成內建預設（傳送點 / 拾取點 / 玩家、怪物出生點）。
    /// </summary>
    public static class TriggerTypeStore
    {
        public const string FileName = "triggerTypes.json";

        public static string DefaultPath => Path.Combine(Application.streamingAssetsPath, FileName);

        public static TriggerTypeSet Load(string path = null)
        {
            path ??= DefaultPath;
            if (!File.Exists(path))
            {
                var defaults = TriggerTypeSet.Defaults();
                Save(defaults, path);
                Debug.Log($"[TriggerTypeStore] 生成內建預設：{path}");
                return defaults;
            }
            string json = File.ReadAllText(path);
            return JsonConfig.Deserialize<TriggerTypeSet>(json) ?? TriggerTypeSet.Defaults();
        }

        public static void Save(TriggerTypeSet set, string path = null)
        {
            path ??= DefaultPath;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonConfig.Serialize(set));
        }
    }
}
