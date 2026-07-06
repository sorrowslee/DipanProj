using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Dipan.MapRuntime
{
    /// <summary>一個具名旗標的定義（與編輯器 flags.json 對應）。</summary>
    [System.Serializable]
    public class FlagDef
    {
        public int id;                   // 編輯器配置的編號（遊戲端不使用，僅為與 flags.json 對齊）
        public string name;
        public string scope = "cycle";   // "cycle"（周目，輪迴清）| "life"（永久，跨輪迴）
        public string note;
    }

    [System.Serializable]
    public class FlagRegistryData
    {
        public List<FlagDef> flags = new List<FlagDef>();
    }

    /// <summary>
    /// 遊戲端旗標登記表：載入 StreamingAssets/MapAssets/flags.json（編輯器旗標管理器產出、同步腳本帶進來），
    /// 提供「這個旗標是不是永久（終身）」的查詢。觸發鏈用它決定旗標存周目 progress.flags 還是終身 lifetimeFlags。
    /// 方案乙：地圖只存旗標裸名，生命週期在這份登記表（單一來源）。見 readme/TRIGGER_CHAIN.md。
    /// </summary>
    public static class FlagRegistry
    {
        public const string SubDir = "MapAssets";
        public const string FileName = "flags.json";

        static Dictionary<string, bool> _life;   // 旗標名 → 是否永久

        static void EnsureLoaded()
        {
            if (_life != null) return;
            _life = new Dictionary<string, bool>();
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, SubDir, FileName);
                if (File.Exists(path))
                {
                    var data = MapJsonConfig.Deserialize<FlagRegistryData>(File.ReadAllText(path));
                    if (data?.flags != null)
                        foreach (var f in data.flags)
                            if (!string.IsNullOrEmpty(f.name)) _life[f.name] = (f.scope == "life");
                }
                else Debug.Log("[FlagRegistry] 找不到 flags.json，所有具名旗標一律當周目（尚未建旗標登記表時的預設）。");
            }
            catch (System.Exception e) { Debug.LogWarning($"[FlagRegistry] 載入失敗，全部當周目：{e.Message}"); }
        }

        /// <summary>此旗標是否登記為「永久（終身）」。未登記＝周目（回 false）。</summary>
        public static bool IsLife(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            EnsureLoaded();
            return _life.TryGetValue(name, out var v) && v;
        }

        /// <summary>清快取（換存檔/重載資源後想重讀時用）。</summary>
        public static void Reload() => _life = null;
    }
}
