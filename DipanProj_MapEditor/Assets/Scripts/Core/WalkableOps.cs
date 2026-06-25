using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 可走層三態位元圖的讀寫（子格解析度）。每列一字串：
    /// '0' = 可走、'1' = 牆（擋＋反彈子彈）、'2' = 水/坑（擋腳、子彈穿過）。
    /// 座標為子格 (fx,fy)，範圍 [0, FineWidth) × [0, FineHeight)。範圍外一律視為牆。
    /// </summary>
    public static class WalkableOps
    {
        public const char Walk = '0';
        public const char Wall = '1';
        public const char Water = '2';

        /// <summary>讀某子格的狀態字元；範圍外回 '1'（牆）。</summary>
        public static char GetState(MapData map, int fx, int fy)
        {
            var rows = map?.WalkableLayer?.blocked;
            if (rows == null || fy < 0 || fy >= rows.Count) return Wall;
            string row = rows[fy];
            if (fx < 0 || fx >= row.Length) return Wall;
            char c = row[fx];
            return (c == Walk || c == Wall || c == Water) ? c : Wall;
        }

        /// <summary>設某子格的狀態字元。</summary>
        public static void SetState(MapData map, int fx, int fy, char state)
        {
            var rows = map?.WalkableLayer?.blocked;
            if (rows == null || fy < 0 || fy >= rows.Count) return;
            string row = rows[fy];
            if (fx < 0 || fx >= row.Length) return;
            var arr = row.ToCharArray();
            arr[fx] = state;
            rows[fy] = new string(arr);
        }

        /// <summary>是否會擋腳（牆或水都擋）。</summary>
        public static bool IsBlocked(MapData map, int fx, int fy) => GetState(map, fx, fy) != Walk;
    }
}
