using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 可走/不可走位元圖的讀寫。blocked 為每列一字串，'1' = 不可走、'0' = 可走。
    /// 範圍外一律視為不可走。
    /// </summary>
    public static class WalkableOps
    {
        public static bool IsBlocked(MapData map, int x, int y)
        {
            var rows = map?.WalkableLayer?.blocked;
            if (rows == null || y < 0 || y >= rows.Count) return true;
            string row = rows[y];
            if (x < 0 || x >= row.Length) return true;
            return row[x] == '1';
        }

        public static void SetBlocked(MapData map, int x, int y, bool blocked)
        {
            var rows = map?.WalkableLayer?.blocked;
            if (rows == null || y < 0 || y >= rows.Count) return;
            string row = rows[y];
            if (x < 0 || x >= row.Length) return;
            var arr = row.ToCharArray();
            arr[x] = blocked ? '1' : '0';
            rows[y] = new string(arr);
        }
    }
}
