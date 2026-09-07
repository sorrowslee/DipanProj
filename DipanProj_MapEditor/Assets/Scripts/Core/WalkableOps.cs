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

        /// <summary>
        /// 用一張遮罩覆蓋整張可走層：true → 可走 '0'，false → 牆 '1'。回傳改動的子格數。
        /// 給「自動生成」用（<see cref="WalkableAutoGen"/>）。
        /// ⚠ 這是**整張覆蓋**，水/坑 '2' 也會被沖掉 —— 所以自動生成後才塗水，不要反過來。
        /// 遮罩長度對不上就整個不做（回 -1），避免寫壞資料。
        /// </summary>
        public static int ApplyMask(MapData map, bool[] mask)
        {
            var rows = map?.WalkableLayer?.blocked;
            if (rows == null || mask == null) return -1;
            int fw = map.FineWidth, fh = map.FineHeight;
            if (mask.Length != fw * fh || rows.Count < fh) return -1;

            int changed = 0;
            for (int y = 0; y < fh; y++)
            {
                string row = rows[y];
                if (row == null || row.Length < fw) return -1;
                var arr = row.ToCharArray();
                for (int x = 0; x < fw; x++)
                {
                    char want = mask[y * fw + x] ? Walk : Wall;
                    if (arr[x] != want) { arr[x] = want; changed++; }
                }
                rows[y] = new string(arr);
            }
            return changed;
        }

        /// <summary>把整張可走層填成同一狀態（每列重建一條等長字串，比逐格設快）。回傳改動的格數。</summary>
        public static int FillAll(MapData map, char state)
        {
            var rows = map?.WalkableLayer?.blocked;
            if (rows == null) return 0;
            int changed = 0;
            for (int y = 0; y < rows.Count; y++)
            {
                int len = rows[y]?.Length ?? 0;
                if (len == 0) continue;
                foreach (char c in rows[y]) if (c != state) changed++;
                rows[y] = new string(state, len);
            }
            return changed;
        }
    }
}
