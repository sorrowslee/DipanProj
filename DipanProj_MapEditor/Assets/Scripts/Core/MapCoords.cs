using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 格座標 ↔ 世界座標 轉換。
    /// 慣例：格 (0,0) 在地圖「左上角」，gx 往右、gy 往下；
    /// 世界座標 Unity 標準 Y 往上，故 gy 越大世界 Y 越小。
    /// origin = 地圖左上角的世界座標。
    /// </summary>
    public static class MapCoords
    {
        /// <summary>某格左上角的世界座標。</summary>
        public static Vector2 CellTopLeft(int gx, int gy, float tileSize, Vector2 origin)
            => new Vector2(origin.x + gx * tileSize, origin.y - gy * tileSize);

        /// <summary>某格中心的世界座標。</summary>
        public static Vector2 CellCenter(int gx, int gy, float tileSize, Vector2 origin)
            => new Vector2(origin.x + (gx + 0.5f) * tileSize, origin.y - (gy + 0.5f) * tileSize);

        /// <summary>世界座標 → 格座標（floor）。可能落在地圖外（負數或 ≥ width/height）。</summary>
        public static Vector2Int WorldToCell(Vector2 world, float tileSize, Vector2 origin)
        {
            int gx = Mathf.FloorToInt((world.x - origin.x) / tileSize);
            int gy = Mathf.FloorToInt((origin.y - world.y) / tileSize);
            return new Vector2Int(gx, gy);
        }

        public static bool InBounds(int gx, int gy, MapData map)
            => gx >= 0 && gy >= 0 && gx < map.width && gy < map.height;

        /// <summary>地圖在世界空間的包圍盒（左上 origin，往右下展開）。</summary>
        public static Rect WorldBounds(MapData map)
        {
            float w = map.width * map.tileSize;
            float h = map.height * map.tileSize;
            // Rect 以左下角為原點：xMin = origin.x, yMin = origin.y - h
            return new Rect(map.origin.x, map.origin.y - h, w, h);
        }

        public static Vector2 Origin(MapData map) => new Vector2(map.origin.x, map.origin.y);

        /// <summary>
        /// 地圖格 (gx,gy)（左上原點、y 往下）→ Unity Tilemap cell（左下原點、y 往上）。
        /// 搭配 TilemapView 把 Grid 物件擺在地圖左下角。
        /// </summary>
        public static Vector3Int ToTilemapCell(int gx, int gy, int height)
            => new Vector3Int(gx, height - 1 - gy, 0);

        /// <summary>Tilemap cell → 地圖格。</summary>
        public static Vector2Int FromTilemapCell(Vector3Int cell, int height)
            => new Vector2Int(cell.x, height - 1 - cell.y);
    }
}
