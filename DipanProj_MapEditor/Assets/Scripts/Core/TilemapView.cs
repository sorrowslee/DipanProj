using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 遊戲層 tile 的 runtime 渲染容器：程式建立 Grid + Tilemap，
    /// 並提供畫格 / 清格 / 依資料重建。
    /// </summary>
    public class TilemapView : MonoBehaviour
    {
        public Grid Grid { get; private set; }
        public Tilemap Tilemap { get; private set; }

        readonly Dictionary<string, Tile> _tileAssets = new Dictionary<string, Tile>();

        void OnEnable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged += Rebuild;
                MapSession.Instance.OnMapResized += Rebuild;
                MapSession.Instance.OnMapRebuilt += Rebuild;
            }
        }

        void OnDisable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged -= Rebuild;
                MapSession.Instance.OnMapResized -= Rebuild;
                MapSession.Instance.OnMapRebuilt -= Rebuild;
            }
        }

        void EnsureCreated()
        {
            if (Grid != null) return;

            var gridGO = new GameObject("GameTilemap_Grid");
            gridGO.transform.SetParent(transform, false);
            Grid = gridGO.AddComponent<Grid>();

            var tmGO = new GameObject("GameTilemap");
            tmGO.transform.SetParent(gridGO.transform, false);
            Tilemap = tmGO.AddComponent<Tilemap>();
            var renderer = tmGO.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = 0;
        }

        void Rebuild(MapData map)
        {
            if (map == null) return;
            EnsureCreated();

            Grid.cellSize = new Vector3(map.tileSize, map.tileSize, 0f);
            float bottom = map.origin.y - map.height * map.tileSize;
            Grid.transform.position = new Vector3(map.origin.x, bottom, 0f);

            Tilemap.ClearAllTiles();
            BuildFromData(map);
        }

        public void BuildFromData(MapData map)
        {
            var layer = map.GameLayer;
            if (layer?.tiles == null) return;
            foreach (var t in layer.tiles)
                SetCellVisual(t.x, t.y, t.tileId, map);
        }

        // ---- 供 PaintController 呼叫的即時畫格 ----

        public void SetCellVisual(int gx, int gy, string tileId, MapData map)
        {
            if (map == null || string.IsNullOrEmpty(tileId)) return;
            EnsureCreated();
            var tile = GetOrCreateTile(tileId, map);
            if (tile == null) return;
            Tilemap.SetTile(MapCoords.ToTilemapCell(gx, gy, map.height), tile);
        }

        public void ClearCellVisual(int gx, int gy, MapData map)
        {
            if (map == null || Tilemap == null) return;
            Tilemap.SetTile(MapCoords.ToTilemapCell(gx, gy, map.height), null);
        }

        Tile GetOrCreateTile(string tileId, MapData map)
        {
            if (_tileAssets.TryGetValue(tileId, out var cached) && cached != null) return cached;
            var sprite = TilesetService.ResolveSprite(tileId, MapSession.Instance.Catalog, map.tileSize);
            if (sprite == null)
            {
                Debug.LogWarning($"[TilemapView] 無法解析 tile sprite：{tileId}");
                return null;
            }
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            _tileAssets[tileId] = tile;
            return tile;
        }
    }
}
