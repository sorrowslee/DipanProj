using System;
using System.IO;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.IO
{
    /// <summary>.dipanmap 檔（JSON）的存檔／讀檔。</summary>
    public static class MapSerializer
    {
        public const string Extension = ".dipanmap";

        public static void Save(MapData map, string path)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            string json = JsonConfig.Serialize(map);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, json);
            Debug.Log($"[MapSerializer] 已存檔：{path}");
        }

        public static MapData Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"找不到地圖檔：{path}");
            string json = File.ReadAllText(path);
            var map = JsonConfig.Deserialize<MapData>(json);
            if (map == null || map.format != "dipanmap")
                throw new InvalidDataException($"不是有效的 .dipanmap 檔：{path}");
            return map;
        }

        public static bool TryLoad(string path, out MapData map)
        {
            try { map = Load(path); return true; }
            catch (Exception e) { Debug.LogError($"[MapSerializer] 讀檔失敗：{e.Message}"); map = null; return false; }
        }
    }
}
