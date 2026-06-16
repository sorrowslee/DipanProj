#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using DipanMapEditor.Data;
using DipanMapEditor.IO;

namespace DipanMapEditor.EditorTools
{
    /// <summary>
    /// M0 自我驗證：選單 DipanMapEditor → Self Test (M0)。
    /// 不需進 Play 模式：建一張空白地圖 → 存檔 → 讀回 → 比對 → 印結果。
    /// 用來確認資料模型 + Newtonsoft 序列化在 Unity 內能正確編譯與往返。
    /// （驗證完可刪此檔。）
    /// </summary>
    public static class M0SelfTest
    {
        [MenuItem("DipanMapEditor/Self Test (M0)")]
        public static void Run()
        {
            string path = Path.Combine(Application.persistentDataPath, "_m0_selftest.dipanmap");

            // 1. 建空白地圖（預設一個螢幕 18x10、可走層全不可走）
            var map = MapData.CreateBlank("SelfTest", "RedBridalGown", 1f, 18, 10);

            // 放一塊 tile、一個物件、一個傳送點 trigger，測三層 payload
            map.GameLayer.tiles.Add(new TilePlacement { x = 3, y = 5, tileId = "Modules/RedBridalGown/Tiles/Floor_0" });
            map.GameLayer.objects.Add(new ObjectInstance
            {
                assetId = "Modules/RedBridalGown/Prefabs/Table",
                x = 4.2f, y = 6f, flipX = true, scaleX = 1.5f, scaleY = 1.5f, sortKey = 6f
            });
            var region = new TriggerRegion { id = "tp01", name = "往大廳", typeId = "teleport" };
            region.cells.Add(new[] { 10, 2 });
            region.cells.Add(new[] { 10, 3 });
            region.Params["targetMap"] = "Hall";
            region.Params["targetX"] = 5f;
            region.Params["targetY"] = 5f;
            map.TriggerLayer.regions.Add(region);

            // 2. 存檔
            MapSerializer.Save(map, path);

            // 3. 讀回
            var loaded = MapSerializer.Load(path);

            // 4. 比對
            bool ok =
                loaded != null &&
                loaded.name == "SelfTest" &&
                loaded.width == 18 && loaded.height == 10 &&
                loaded.layers.Count == 3 &&
                loaded.WalkableLayer.blocked.Count == 10 &&
                loaded.WalkableLayer.blocked[0] == new string('1', 18) &&
                loaded.GameLayer.tiles.Count == 1 &&
                loaded.GameLayer.objects.Count == 1 &&
                loaded.GameLayer.objects[0].flipX &&
                loaded.TriggerLayer.regions.Count == 1 &&
                loaded.TriggerLayer.regions[0].cells.Count == 2 &&
                loaded.TriggerLayer.regions[0].Params.ContainsKey("targetMap");

            if (ok)
                Debug.Log($"<color=#33FF88>[M0 SelfTest] 通過 ✓</color>　序列化往返正常。檔案：{path}");
            else
                Debug.LogError($"[M0 SelfTest] 失敗 ✗　讀回的資料與預期不符。檔案：{path}");

            // 順帶印出產生的 JSON 供肉眼檢查
            Debug.Log("[M0 SelfTest] .dipanmap 內容：\n" + File.ReadAllText(path));

            // 另測 trigger 類型預設能生成
            var triggers = TriggerTypeStore.Load();
            Debug.Log($"[M0 SelfTest] Trigger 類型載入 {triggers.types.Count} 種：" +
                      string.Join(", ", triggers.types.ConvertAll(t => t.displayName)));
        }
    }
}
#endif
