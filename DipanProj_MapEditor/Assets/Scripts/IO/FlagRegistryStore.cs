using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.IO
{
    /// <summary>
    /// 全域旗標登記表 flags.json 的存讀 + 從所有地圖自動匯入已用到的旗標名。
    /// 路徑固定在編輯器專案根目錄（Assets 的上一層），與 Maps/ 同層，方便同步腳本帶進遊戲。
    /// </summary>
    public static class FlagRegistryStore
    {
        public const string FileName = "flags.json";

        // 這幾格的值是「旗標名」（可能帶 "!" 否定前綴，也可能是舊的 "永久:" 範圍前綴——匯入時都剝掉取裸名）。
        static readonly string[] FlagKeys = { "requireFlag", "setFlag", "enableFlag" };
        const string LifePrefix = "永久:";

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string Path => System.IO.Path.Combine(ProjectRoot, FileName);
        static string MapsRoot => System.IO.Path.Combine(ProjectRoot, "Maps");

        public static FlagRegistry Load()
        {
            try
            {
                if (File.Exists(Path))
                {
                    var reg = JsonConfig.Deserialize<FlagRegistry>(File.ReadAllText(Path)) ?? new FlagRegistry();
                    reg.NormalizeIds();   // 補齊 id（舊檔可能沒有）
                    return reg;
                }
            }
            catch (System.Exception e) { Debug.LogError($"[FlagRegistryStore] 讀取失敗：{e.Message}"); }
            return new FlagRegistry();
        }

        public static void Save(FlagRegistry reg)
        {
            try
            {
                reg.SortByName();
                File.WriteAllText(Path, JsonConfig.Serialize(reg));
                Debug.Log($"[FlagRegistryStore] 已儲存旗標登記表：{Path}（{reg.flags.Count} 個）");
            }
            catch (System.Exception e) { Debug.LogError($"[FlagRegistryStore] 儲存失敗：{e.Message}"); }
        }

        /// <summary>遞迴掃 Maps/ 下所有 .dipanmap，收集用到但尚未登記的旗標名（去重、剝掉 "!" 與 "永久:" 前綴）。回傳新加的名字。</summary>
        public static List<string> ImportUsedFlags(FlagRegistry reg)
        {
            var added = new List<string>();
            if (!Directory.Exists(MapsRoot)) return added;

            foreach (var file in Directory.GetFiles(MapsRoot, "*" + MapSerializer.Extension, SearchOption.AllDirectories))
            {
                if (!MapSerializer.TryLoad(file, out var map) || map?.TriggerLayer?.regions == null) continue;
                foreach (var r in map.TriggerLayer.regions)
                {
                    if (r.Params == null) continue;
                    foreach (var key in FlagKeys)
                    {
                        if (!r.Params.TryGetValue(key, out var v) || v == null) continue;
                        string bare = BareName(v.ToString());
                        if (string.IsNullOrEmpty(bare) || reg.Contains(bare)) continue;
                        // 舊資料若帶 "永久:" 前綴＝該旗標本來就是永久，匯入時把生命週期設對。
                        bool life = v.ToString().Contains(LifePrefix);
                        reg.Add(bare, life ? FlagDef.ScopeLife : FlagDef.ScopeCycle);
                        added.Add(bare);
                    }
                }
            }
            return added;
        }

        /// <summary>剝掉 "!"（否定）與 "永久:"（範圍）前綴，取旗標裸名。</summary>
        static string BareName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = raw.Trim();
            if (s.StartsWith("!")) s = s.Substring(1).Trim();
            if (s.StartsWith(LifePrefix)) s = s.Substring(LifePrefix.Length).Trim();
            return s;
        }
    }
}
