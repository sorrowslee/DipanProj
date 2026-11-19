using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Dipan.Data;
using Dipan.Vfx;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 特效預覽器「自製特效」分頁的資料端（2026-11-18，見 readme/DIPAN_VFX.md §6）。
    ///
    /// DipanVfx 套件在主遊戲用 <c>Resources.Load</c> 取圖、用 TextAsset 讀表；地圖編輯器沒有那些資源，
    /// 所以這裡把套件的取圖接口（<see cref="VfxAssets.SpriteLoader"/>）換成「直接讀 DipanProj_Main/Assets/Resources 底下的 png」，
    /// 表格也直接讀寫 DipanProj_Main/Assets/Data 的 CSV——**編輯器改的就是遊戲用的那一份**，不必同步。
    /// png 的 pivot／PPU 讀同名 .meta（跟遊戲匯入結果一致；讀不到就置中、PPU 100）。
    /// </summary>
    public static class CustomVfxSource
    {
        public static string RepoRoot
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;   // DipanProj_MapEditor
                return Directory.GetParent(projectRoot).FullName;                          // DipanProj
            }
        }

        public static string MainAssets => Path.Combine(RepoRoot, "DipanProj_Main", "Assets");
        public static string DataPath(string table) => Path.Combine(MainAssets, "Data", table + ".csv");

        static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        static bool _installed;

        /// <summary>把套件的取圖接口接到「讀主遊戲檔案」。開預覽器時呼叫（重複呼叫無害）。</summary>
        public static void Install()
        {
            if (_installed && VfxAssets.SpriteLoader == (System.Func<string, Sprite>)LoadSprite) return;
            VfxAssets.SpriteLoader = LoadSprite;
            VfxAssets.TextureLoader = LoadTexture;
            VfxAssets.SortOrderForY = null;   // 編輯器沒有地圖 Y 排序帶：火舌用呼叫端給的排序
            _installed = true;
        }

        static bool _dbLoaded;

        /// <summary>
        /// 確保 DipanVfx 的表都載過一次（地圖上的傳送點對位預覽要用，不必先打開特效預覽器）。
        /// 特效預覽器「重讀表格與圖」會再整批重載。
        /// </summary>
        public static void EnsureDatabases()
        {
            Install();
            if (_dbLoaded) return;
            _dbLoaded = true;
            Load(FlameDatabase.TableName, FlameDatabase.LoadFromText);
            Load(GroundFireDatabase.TableName, GroundFireDatabase.LoadFromText);
            Load("GroundSpikeTable", GroundSpikeDatabase.LoadFromText);
            Load(ExplosionDatabase.TableName, ExplosionDatabase.LoadFromText);
            Load(TeleportFxDatabase.TableName, TeleportFxDatabase.LoadFromText);
            Load(BeamFxDatabase.TableName, BeamFxDatabase.LoadFromText);
            Load(SpiritFxDatabase.TableName, SpiritFxDatabase.LoadFromText);
        }

        static void Load(string table, System.Action<string> apply)
        {
            string path = DataPath(table);
            if (File.Exists(path)) apply(File.ReadAllText(path));
        }

        /// <summary>清掉圖的快取（作者在外面換了 png 後按「重讀」）。</summary>
        public static void ClearSpriteCache()
        {
            foreach (var kv in _sprites)
                if (kv.Value != null) { Object.Destroy(kv.Value.texture); Object.Destroy(kv.Value); }
            _sprites.Clear();
            foreach (var kv in _textures) if (kv.Value != null) Object.Destroy(kv.Value);
            _textures.Clear();
        }

        /// <summary>Resources 相對路徑 → 一般貼圖（靈獸身體的平鋪貼圖；有 mipmap、Repeat）。對應遊戲的 Resources.Load&lt;Texture2D&gt;。</summary>
        public static Texture2D LoadTexture(string resPath)
        {
            resPath = (resPath ?? "").Trim();
            if (resPath.Length == 0) return null;
            if (_textures.TryGetValue(resPath, out var cached) && cached != null) return cached;
            string file = Path.Combine(MainAssets, "Resources", resPath.Replace('/', Path.DirectorySeparatorChar) + ".png");
            if (!File.Exists(file)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                name = Path.GetFileNameWithoutExtension(file),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
            };
            if (!tex.LoadImage(File.ReadAllBytes(file))) { Object.Destroy(tex); return null; }
            tex.wrapModeV = TextureWrapMode.Clamp;
            _textures[resPath] = tex;
            return tex;
        }

        /// <summary>Resources 相對路徑（不含副檔名）→ Sprite；跟遊戲的 Resources.Load&lt;Sprite&gt; 對應。</summary>
        public static Sprite LoadSprite(string resPath)
        {
            resPath = (resPath ?? "").Trim();
            if (resPath.Length == 0) return null;
            if (_sprites.TryGetValue(resPath, out var cached) && cached != null) return cached;

            string file = Path.Combine(MainAssets, "Resources", resPath.Replace('/', Path.DirectorySeparatorChar) + ".png");
            if (!File.Exists(file)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileNameWithoutExtension(file),
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            if (!tex.LoadImage(File.ReadAllBytes(file))) { Object.Destroy(tex); return null; }

            ReadMeta(file + ".meta", out Vector2 pivot, out float ppu, out bool fullRect);
            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0,
                                   fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight);
            sp.name = tex.name;
            _sprites[resPath] = sp;
            return sp;
        }

        /// <summary>從 .meta 讀 pivot（alignment 0＝置中、9＝自訂）、PPU、mesh 類型。只認這次會用到的幾種寫法。</summary>
        static void ReadMeta(string metaPath, out Vector2 pivot, out float ppu, out bool fullRect)
        {
            pivot = new Vector2(0.5f, 0.5f);
            ppu = 100f;
            fullRect = true;
            if (!File.Exists(metaPath)) return;
            int alignment = 0;
            Vector2 custom = pivot;
            foreach (var raw in File.ReadAllLines(metaPath))
            {
                string l = raw.Trim();
                if (l.StartsWith("alignment:")) int.TryParse(l.Substring(10).Trim(), out alignment);
                else if (l.StartsWith("spritePixelsToUnits:"))
                    float.TryParse(l.Substring(20).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out ppu);
                else if (l.StartsWith("spriteMeshType:")) fullRect = l.EndsWith("0");
                else if (l.StartsWith("spritePivot:"))
                {
                    // spritePivot: {x: 0.5, y: 0.075}
                    float x = custom.x, y = custom.y;
                    int ix = l.IndexOf("x:"), iy = l.IndexOf("y:");
                    if (ix >= 0) float.TryParse(l.Substring(ix + 2).Split(',', '}')[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                    if (iy >= 0) float.TryParse(l.Substring(iy + 2).Split(',', '}')[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                    custom = new Vector2(x, y);
                }
            }
            switch (alignment)
            {
                case 9: pivot = custom; break;
                case 7: pivot = new Vector2(0.5f, 0f); break;   // BottomCenter
                default: pivot = new Vector2(0.5f, 0.5f); break;
            }
            if (ppu <= 0f) ppu = 100f;
        }
    }

    /// <summary>
    /// 一張 CSV 的「可就地編修」副本：保留原檔每一行（表頭說明、# 註解、沒動到的列一字不改），
    /// 只把改過的那幾格重新組回去。存檔時寫回 DipanProj_Main/Assets/Data。
    /// </summary>
    public sealed class EditableCsv
    {
        public string TableName { get; private set; }
        public string FilePath { get; private set; }
        public bool Dirty { get; private set; }
        public string Error { get; private set; }

        /// <summary>欄名（括號前）與完整表頭（含說明）。</summary>
        public readonly List<string> Columns = new List<string>();
        public readonly List<string> HeaderCells = new List<string>();

        public sealed class Row
        {
            public int LineIndex;
            public List<string> Cells;
            public bool Edited;
            public int Id => int.TryParse(Cells.Count > 0 ? Cells[0].Trim() : "", out int v) ? v : 0;
        }

        public readonly List<Row> Rows = new List<Row>();
        List<string> _lines = new List<string>();
        bool _bom;

        public static EditableCsv Load(string tableName)
        {
            var t = new EditableCsv { TableName = tableName, FilePath = CustomVfxSource.DataPath(tableName) };
            t.Reload();
            return t;
        }

        public void Reload()
        {
            Columns.Clear(); HeaderCells.Clear(); Rows.Clear(); _lines.Clear();
            Dirty = false; Error = null;
            if (!File.Exists(FilePath)) { Error = "找不到 " + FilePath; return; }
            byte[] bytes = File.ReadAllBytes(FilePath);
            _bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text = new UTF8Encoding(false).GetString(bytes, _bom ? 3 : 0, bytes.Length - (_bom ? 3 : 0));
            _lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            bool header = false;
            for (int i = 0; i < _lines.Count; i++)
            {
                string l = _lines[i];
                if (string.IsNullOrWhiteSpace(l) || l.TrimStart().StartsWith("#")) continue;
                var cells = new List<string>(CsvUtil.SplitLine(l));
                if (!header)
                {
                    header = true;
                    foreach (var c in cells) { HeaderCells.Add(c); Columns.Add(CsvTable.NormalizeHeader(c)); }
                    continue;
                }
                Rows.Add(new Row { LineIndex = i, Cells = cells });
            }
            if (!header) Error = "沒有表頭";
        }

        public int ColumnIndex(string name)
        {
            for (int i = 0; i < Columns.Count; i++)
                if (string.Equals(Columns[i], name, System.StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public Row FindRow(int id)
        {
            foreach (var r in Rows) if (r.Id == id) return r;
            return null;
        }

        public string Get(Row r, int col) => (col >= 0 && col < r.Cells.Count) ? r.Cells[col] : "";

        public void Set(Row r, int col, string value)
        {
            if (col < 0) return;
            while (r.Cells.Count <= col) r.Cells.Add("");
            if (r.Cells[col] == value) return;
            r.Cells[col] = value;
            r.Edited = true;
            Dirty = true;
        }

        /// <summary>目前內容（含未存的修改）組成整份文字——給套件的 LoadFromText 即時套用。</summary>
        public string BuildText()
        {
            var lines = new List<string>(_lines);
            foreach (var r in Rows)
            {
                if (!r.Edited) continue;
                // 去掉尾端多餘的空欄，免得每存一次就多一排逗號；但不少於原本的欄數
                int n = r.Cells.Count;
                while (n > Columns.Count && string.IsNullOrEmpty(r.Cells[n - 1])) n--;
                var sb = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(CsvWriter.Escape(r.Cells[i]));
                }
                lines[r.LineIndex] = sb.ToString();
            }
            return string.Join("\n", lines);
        }

        /// <summary>寫回 DipanProj_Main/Assets/Data（保留原本的 BOM 有無）。</summary>
        public bool Save(out string message)
        {
            try
            {
                string text = BuildText();
                var enc = new UTF8Encoding(_bom);
                File.WriteAllText(FilePath, text, enc);
                Reload();
                message = $"已存回 {TableName}.csv（回到主遊戲的 Unity 視窗會自動重新匯入）";
                return true;
            }
            catch (System.Exception e)
            {
                message = "存檔失敗：" + e.Message;
                return false;
            }
        }
    }
}
