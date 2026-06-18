using UnityEngine;

namespace Dipan.MapRuntime
{
    /// <summary>
    /// 格座標 ↔ 世界座標。慣例與編輯器一致：
    /// 格 (0,0) 在地圖左上角，gx 往右、gy 往下；世界 Y 往上，故 gy 越大世界 Y 越小。
    /// origin = 地圖左上角的世界座標。
    /// </summary>
    public static class MapCoords
    {
        public static Vector2 Origin(MapData map) => new Vector2(map.origin.x, map.origin.y);

        public static Vector2 CellTopLeft(int gx, int gy, MapData map)
            => new Vector2(map.origin.x + gx * map.tileSize, map.origin.y - gy * map.tileSize);

        public static Vector2 CellCenter(int gx, int gy, MapData map)
            => new Vector2(map.origin.x + (gx + 0.5f) * map.tileSize, map.origin.y - (gy + 0.5f) * map.tileSize);

        /// <summary>地圖在世界空間的包圍盒（左上 origin、往右下展開）。</summary>
        public static Rect WorldBounds(MapData map)
        {
            float w = map.width * map.tileSize;
            float h = map.height * map.tileSize;
            return new Rect(map.origin.x, map.origin.y - h, w, h);
        }

        public static Vector2 WorldCenter(MapData map)
        {
            float w = map.width * map.tileSize;
            float h = map.height * map.tileSize;
            return new Vector2(map.origin.x + w * 0.5f, map.origin.y - h * 0.5f);
        }

        /// <summary>地圖格 (gx,gy)（左上原點、y 往下）→ Unity Tilemap cell（左下原點、y 往上）。</summary>
        public static Vector3Int ToTilemapCell(int gx, int gy, int height)
            => new Vector3Int(gx, height - 1 - gy, 0);

        /// <summary>世界座標 → 地圖格 (gx,gy)（左上原點、y 往下）。超出地圖範圍可能回負或過界值，呼叫端自行判斷。</summary>
        public static Vector2Int WorldToCell(Vector2 world, MapData map)
        {
            int gx = Mathf.FloorToInt((world.x - map.origin.x) / map.tileSize);
            int gy = Mathf.FloorToInt((map.origin.y - world.y) / map.tileSize);
            return new Vector2Int(gx, gy);
        }
    }
}
