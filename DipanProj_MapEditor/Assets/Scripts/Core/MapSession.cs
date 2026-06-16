using System;
using System.IO;
using UnityEngine;
using DipanMapEditor.Data;
using DipanMapEditor.IO;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 編輯器全域狀態：當前地圖、素材目錄、trigger 類型、當前檔路徑。
    /// 其他系統（相機、格線、Tilemap、UI）透過事件回應地圖切換/變更。
    /// </summary>
    public class MapSession : MonoBehaviour
    {
        public static MapSession Instance { get; private set; }

        public MapData Map { get; private set; }
        public Catalog Catalog { get; private set; } = new Catalog();
        public TriggerTypeSet TriggerTypes { get; private set; } = new TriggerTypeSet();

        /// <summary>當前 .dipanmap 檔路徑（尚未存檔則為 null）。</summary>
        public string CurrentPath { get; private set; }

        /// <summary>地圖被替換（新建/讀檔）時觸發。</summary>
        public event Action<MapData> OnMapChanged;
        /// <summary>地圖尺寸改變時觸發（resize）。</summary>
        public event Action<MapData> OnMapResized;

        /// <summary>Undo 還原後觸發：重建視圖但不重新聚焦相機。</summary>
        public event Action<MapData> OnMapRebuilt;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            // 啟動時載入素材目錄與 trigger 類型（沒有也不報錯）
            Catalog = CatalogLoader.Load();
            TriggerTypes = TriggerTypeStore.Load();
        }

        /// <summary>重新從磁碟載入 catalog（同步腳本跑過後可呼叫刷新）。</summary>
        public void ReloadCatalog()
        {
            Catalog = CatalogLoader.Load();
        }

        // ---- 地圖生命週期 ----

        public void NewMap(string name, string module, float tileSize, int width, int height)
        {
            Map = MapData.CreateBlank(name, module, tileSize, Mathf.Max(1, width), Mathf.Max(1, height));
            CurrentPath = null;
            UndoManager.Clear();
            OnMapChanged?.Invoke(Map);
        }

        /// <summary>由 Undo 還原：替換地圖並重建視圖（不重新聚焦相機）。</summary>
        public void RestoreFromJson(string json)
        {
            var map = JsonConfig.Deserialize<MapData>(json);
            if (map == null) return;
            Map = map;
            OnMapRebuilt?.Invoke(Map);
        }

        /// <summary>當前地圖的 module（無地圖時為空字串）。</summary>
        public string CurrentModule => Map != null ? Map.module : "";

        public bool LoadMap(string path)
        {
            if (!MapSerializer.TryLoad(path, out var map)) return false;
            Map = map;
            CurrentPath = path;
            UndoManager.Clear();
            OnMapChanged?.Invoke(Map);
            return true;
        }

        public void SaveMap(string path = null)
        {
            if (Map == null) { Debug.LogWarning("[MapSession] 沒有地圖可存。"); return; }
            path ??= CurrentPath;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[MapSession] 尚未指定存檔路徑。");
                return;
            }
            MapSerializer.Save(Map, path);
            CurrentPath = path;
        }

        /// <summary>
        /// 改畫布尺寸：左上角錨定，右/下邊增減。
        /// 縮小裁掉右/下、放大往右/下補（可走層補不可走、tile/物件超出範圍者移除）。
        /// </summary>
        public void ResizeMap(int newWidth, int newHeight)
        {
            if (Map == null) return;
            newWidth = Mathf.Max(1, newWidth);
            newHeight = Mathf.Max(1, newHeight);

            // 可走層位元圖：逐列裁切/補 '1'
            var walk = Map.WalkableLayer;
            if (walk != null && walk.blocked != null)
            {
                var rows = walk.blocked;
                for (int y = 0; y < newHeight; y++)
                {
                    string row = y < rows.Count ? rows[y] : "";
                    if (row.Length < newWidth) row = row.PadRight(newWidth, '1');
                    else if (row.Length > newWidth) row = row.Substring(0, newWidth);
                    if (y < rows.Count) rows[y] = row; else rows.Add(row);
                }
                if (rows.Count > newHeight) rows.RemoveRange(newHeight, rows.Count - newHeight);
            }

            // 遊戲層 tile：移除超出新範圍者
            var game = Map.GameLayer;
            if (game?.tiles != null)
                game.tiles.RemoveAll(t => t.x >= newWidth || t.y >= newHeight);

            // 物件以世界座標自由擺放，超出範圍不強制刪（保留，使用者自理）

            Map.width = newWidth;
            Map.height = newHeight;
            OnMapResized?.Invoke(Map);
        }
    }
}
