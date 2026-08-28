using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 讀主專案的 <c>Assets/Data/NpcTable.csv</c>（NPC 種類表），給「NPC」分頁的角色下拉與預覽用。
    /// 直接讀磁碟（同 <see cref="PreviewSpriteLoader"/> 直讀 GameAssets 的作法，不必同步素材）；
    /// 表改了按面板的「重讀 NPC 表」即可。解析依表頭欄名，與遊戲端 NpcDatabase 同規則。
    /// </summary>
    public static class NpcTableEditor
    {
        public class Row
        {
            public int ID;
            public string Name = "";          // 序列圖資料夾名（Monsters/SequenceImage/<Name>/）
            public string DisplayName = "";
            public float Scale = 1f;
            public float AnimFPS = 8f;
            public float Speed = 2f;
            public string ShownName => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
        }

        static List<Row> _rows;

        public static List<Row> Rows { get { if (_rows == null) Load(); return _rows; } }

        public static Row Get(int id)
        {
            var rows = Rows;
            for (int i = 0; i < rows.Count; i++) if (rows[i].ID == id) return rows[i];
            return null;
        }

        public static void Reload() { _rows = null; PreviewSpriteLoader.Clear(); }

        static string CsvPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;   // DipanProj_MapEditor
            string repoRoot = Directory.GetParent(projectRoot).FullName;               // DipanProj
            return Path.Combine(repoRoot, "DipanProj_Main", "Assets", "Data", "NpcTable.csv");
        }

        static void Load()
        {
            _rows = new List<Row>();
            string path = CsvPath();
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[NpcTableEditor] 找不到 {path}——請先在主專案建立 NpcTable.csv。");
                return;
            }

            var lines = File.ReadAllText(path).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length < 2) return;

            var header = lines[0].Split(',');
            var col = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++)
            {
                string k = header[i].Trim().ToLowerInvariant();
                if (k.Length > 0 && !col.ContainsKey(k)) col[k] = i;
            }
            string Cell(string[] v, string key)
                => (col.TryGetValue(key, out int idx) && idx < v.Length) ? v[idx].Trim() : "";
            float Num(string[] v, string key, float fb)
                => float.TryParse(Cell(v, key), out float f) ? f : fb;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var v = lines[i].Split(',');
                if (!int.TryParse(Cell(v, "id"), out int id)) continue;
                var r = new Row
                {
                    ID = id,
                    Name = Cell(v, "name"),
                    DisplayName = Cell(v, "displayname"),
                    Scale = Num(v, "scale", 1f),
                    AnimFPS = Num(v, "animfps", 8f),
                    Speed = Num(v, "speed", 2f),
                };
                if (r.Name.Length > 0) _rows.Add(r);
            }
        }
    }
}
