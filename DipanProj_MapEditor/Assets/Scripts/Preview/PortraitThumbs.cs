using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 「角色介紹」步驟的立繪挑選：列出主專案 <c>GameAssets/Modules/&lt;module&gt;/Talk/*.png</c>，
    /// 並把選中的那張載成縮圖給面板顯示。直接讀主專案原始 PNG（同 <see cref="PreviewSpriteLoader"/>），不必同步。
    ///
    /// <para>回傳／接受的都是 **catalog id**（相對 GameAssets、不含副檔名，例 <c>Modules/BloodFang/Talk/Elvin_normal</c>）——
    /// 與 DramaTalkTable 的立繪路徑同一種寫法，遊戲端用同一支 <c>DramaTalkDatabase.ResolvePortrait</c> 載入。</para>
    /// <para>縮圖只載「目前選中的那一張」（立繪單張 1~2K，全部載會吃不少記憶體）；換了圖就換快取。</para>
    /// </summary>
    public static class PortraitThumbs
    {
        static string _texId;
        static Texture2D _tex;
        static string _listModule;
        static List<string> _list;

        static string GameAssetsRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;   // DipanProj_MapEditor
            string repoRoot = Directory.GetParent(projectRoot).FullName;               // DipanProj
            return Path.Combine(repoRoot, "DipanProj_Main", "Assets", "GameAssets");
        }

        /// <summary>某模組 Talk 資料夾下所有 png 的 catalog id（依檔名排序）。模組留空回空清單。</summary>
        public static List<string> ListTalk(string module)
        {
            module = (module ?? "").Trim();
            if (_list != null && _listModule == module) return _list;
            _listModule = module;
            _list = new List<string>();
            if (module.Length == 0) return _list;
            string dir = Path.Combine(GameAssetsRoot(), "Modules", module, "Talk");
            if (!Directory.Exists(dir)) return _list;
            var files = Directory.GetFiles(dir, "*.png");
            System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
            foreach (var f in files) _list.Add($"Modules/{module}/Talk/{Path.GetFileNameWithoutExtension(f)}");
            return _list;
        }

        /// <summary>丟掉清單與縮圖快取（資料夾裡加了新圖時用）。</summary>
        public static void Clear()
        {
            _list = null; _listModule = null;
            if (_tex != null) Object.Destroy(_tex);
            _tex = null; _texId = null;
        }

        /// <summary>catalog id → 縮圖（找不到圖回 null，面板據此顯示警告）。</summary>
        public static Texture2D Get(string catalogId)
        {
            catalogId = (catalogId ?? "").Trim();
            if (catalogId.Length == 0) return null;
            if (catalogId == _texId) return _tex;

            if (_tex != null) Object.Destroy(_tex);
            _tex = null; _texId = catalogId;
            string path = Path.Combine(GameAssetsRoot(), catalogId + ".png");
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(path))) { Object.Destroy(t); return null; }
            _tex = t;
            return _tex;
        }
    }
}
