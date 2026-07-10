using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;

/// <summary>
/// 執行期尋徑格（A*）。每次載圖後由 MapManager 呼叫 <see cref="EnsureBuilt"/>(map) 重建。
///
/// 可走/不可走以「地圖可走層位元圖」(<see cref="MapData.WalkableLayer"/> 的 blocked) 為準：
/// '0' = 可走、'1' = 牆、'2' = 水 → 後兩者不可走。這份位元圖是 MapLoader 生牆碰撞的**同一份資料**，
/// 載圖當下就存在、與物理時序無關，因此**不會**發生「牆是一整片 CompositeCollider2D、建格當下還沒
/// query-ready，OverlapCircle 抓不到而整片誤判可走」的問題（先前 A* 走不動、怪在原地一上一下就是這個坑）。
///
/// 再用物理 OverlapCircle（Environment/Water 層）做**聯集**：補上位元圖沒有的「地上物家具」碰撞，
/// 並在牆/家具周圍留 <see cref="AgentRadius"/> 淨空，讓算出的路徑天生離牆一個身位、怪塞得下。
/// OverlapCircle 是 overlap 查詢（非 cast），不受專案的 queriesStartInColliders=false 影響，牆/物都抓得到。
///
/// 提供 <see cref="TryFindPath"/>（八方向 A* ＋ 視線平滑）給 <see cref="MonsterActuator"/>。
/// 單場景測試（沒 MapManager 建格）時 Instance 為 null，怪自動退回局部避障。
/// </summary>
public class MapNavGrid : MonoBehaviour
{
    public static MapNavGrid Instance { get; private set; }

    [Header("尋徑格（可調）")]
    public float AgentRadius = 0.3f;      // 掃格淨空半徑（怪身半寬）：路徑會離牆/家具這麼遠。子格常 0.25，設太大（如 0.4）會侵蝕掉近半可走格、把走道掐窄
    public bool UnionPhysics = true;      // 是否聯集物理碰撞（補地上物家具＋牆周圍淨空）

    int _w, _h;
    float _cell;                          // 子格世界邊長 = tileSize / Subdiv
    Vector2 _originTL;                    // 地圖左上角世界座標（gx 往右、gy 往下；世界 Y 往上）
    bool[] _walk;
    bool _ready;

    // A* 暫存（依格數配置、重用避免每次 GC）
    float[] _g, _f; int[] _came, _heap, _heapIndex; int _heapCount;
    readonly List<int> _cells = new List<int>();

    void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public bool Ready => _ready;

    /// <summary>載圖後由 MapManager 呼叫，傳入當前 MapData。</summary>
    public static void EnsureBuilt(MapData map)
    {
        if (map == null) return;
        if (Instance == null)
        {
            var go = new GameObject("[MapNavGrid]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<MapNavGrid>();
        }
        Instance.Rebuild(map);
    }

    public void Rebuild(MapData map)
    {
        _ready = false;
        if (map == null) return;

        _w = Mathf.Max(1, map.FineWidth);
        _h = Mathf.Max(1, map.FineHeight);
        _cell = Mathf.Max(0.01f, MapCoords.FineSize(map));
        _originTL = MapCoords.Origin(map);
        int n = _w * _h;

        LayerData walk = map.WalkableLayer;
        List<string> blocked = walk != null ? walk.blocked : null;

        // 位元圖是「牆/水/可走」的權威來源（'0' 可走；其餘不可走）。先讀成原始牆圖 wall[]（true = 牆）。
        bool[] wall = new bool[n];
        for (int y = 0; y < _h; y++)
        {
            string rowStr = (blocked != null && y < blocked.Count) ? blocked[y] : null;
            for (int x = 0; x < _w; x++)
            {
                char c = (rowStr != null && x < rowStr.Length) ? rowStr[x] : '1';
                wall[y * _w + x] = (c != '0');
            }
        }

        // 淨空（clearance）：把牆「膨脹」clearCells 格，讓路徑離牆一個身位、怪塞得下。
        // clearCells 依子格大小自適應：subdiv=4（子格0.25）→1 格；subdiv=1（子格1.0）→0 格（免得掐死 1-tile 走道）。
        // **只吃位元圖、不碰物理**：物理 OverlapCircle 會把整片 CompositeCollider2D 牆多啃一圈、把兩房間的窄喉道切斷
        // （實測可走 1408→920、變不連通、A* 找不到路）；純位元圖膨脹是確定性的、和關卡設計一致、保證連通（1408→1212）。
        int clearCells = Mathf.Clamp(Mathf.RoundToInt(AgentRadius / _cell), 0, 3);
        _walk = new bool[n];
        for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                int i = y * _w + x;
                if (wall[i]) { _walk[i] = false; continue; }
                bool near = false;
                for (int dy = -clearCells; dy <= clearCells && !near; dy++)
                    for (int dx = -clearCells; dx <= clearCells; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= _w || ny < 0 || ny >= _h || wall[ny * _w + nx]) { near = true; break; }
                    }
                _walk[i] = !near;
            }

        // 地上物家具（椅子/供桌…不在位元圖裡、是獨立 box collider）：用物理聯集補進格子，A* 才會繞過去。
        // 關鍵：這一步在「位元圖膨脹之後」跑，只掃已經離牆 >clearCells 的格；再用一個「比牆淨空還小」的半徑，
        // 所以它**碰不到牆**（牆 composite 在更遠處）、只會封到真正壓在家具上的格 → 家具會被繞、牆的喉道不會被切斷。
        if (UnionPhysics)
        {
            int mask = LayerMask.GetMask("Environment", "Water");
            if (mask != 0)
            {
                Physics2D.SyncTransforms();
                float r = 0.16f;   // 家具半徑：只封「真的壓在家具上」的格，讓小腳框能鑽家具窄縫；又 << 牆淨空(~0.375)碰不到牆
                for (int i = 0; i < n; i++)
                {
                    if (!_walk[i]) continue;
                    if (Physics2D.OverlapCircle(CellCenter(i), r, mask) != null) _walk[i] = false;
                }
            }
        }

        _g = new float[n]; _f = new float[n]; _came = new int[n]; _heap = new int[n]; _heapIndex = new int[n];
        _ready = true;
    }

    Vector2 CellCenter(int i)
    {
        int x = i % _w, y = i / _w;
        return new Vector2(_originTL.x + (x + 0.5f) * _cell, _originTL.y - (y + 0.5f) * _cell);
    }
    bool InB(int x, int y) => x >= 0 && x < _w && y >= 0 && y < _h;
    bool Walk(int x, int y) => InB(x, y) && _walk[y * _w + x];

    int WorldToIndex(Vector2 p)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((p.x - _originTL.x) / _cell), 0, _w - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((_originTL.y - p.y) / _cell), 0, _h - 1);
        return y * _w + x;
    }

    // 起點/終點可能落在不可走格（怪貼牆、玩家站在淨空不足的角落）→ 找最近可走格。
    int NearestWalkable(int idx)
    {
        int cx = idx % _w, cy = idx / _w;
        if (Walk(cx, cy)) return idx;
        int maxR = Mathf.Max(_w, _h);
        for (int r = 1; r <= maxR; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;   // 只掃最外圈
                    if (Walk(cx + dx, cy + dy)) return (cy + dy) * _w + (cx + dx);
                }
        return idx;
    }

    static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
    const float SQRT2 = 1.41421356f;

    /// <summary>A* 尋徑，回傳世界航點清單（不含起點；已做視線平滑）。找不到路回 false。</summary>
    public bool TryFindPath(Vector2 fromW, Vector2 toW, List<Vector2> outPath)
    {
        outPath.Clear();
        if (!_ready || _w <= 0) return false;
        int rawStart = WorldToIndex(fromW);
        int start = NearestWalkable(rawStart);
        int goal = NearestWalkable(WorldToIndex(toW));
        if (start == goal) { outPath.Add(CellCenter(goal)); return true; }

        int n = _w * _h;
        for (int i = 0; i < n; i++) { _g[i] = float.PositiveInfinity; _came[i] = -1; _heapIndex[i] = -1; }
        _heapCount = 0;
        _g[start] = 0f; _f[start] = Heur(start, goal); HeapPush(start);

        bool found = false;
        while (_heapCount > 0)
        {
            int cur = HeapPop();
            if (cur == goal) { found = true; break; }
            int cx = cur % _w, cy = cur / _w;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + DX[d], ny = cy + DY[d];
                if (!Walk(nx, ny)) continue;
                if (DX[d] != 0 && DY[d] != 0 && (!Walk(cx + DX[d], cy) || !Walk(cx, cy + DY[d]))) continue;  // 不穿牆角
                int ni = ny * _w + nx;
                float ng = _g[cur] + ((DX[d] != 0 && DY[d] != 0) ? SQRT2 : 1f);
                if (ng < _g[ni])
                {
                    _g[ni] = ng; _came[ni] = cur; _f[ni] = ng + Heur(ni, goal);
                    if (_heapIndex[ni] < 0) HeapPush(ni); else HeapUp(_heapIndex[ni]);
                }
            }
        }
        if (!found) return false;

        _cells.Clear();
        for (int c = goal; c != -1; c = _came[c]) _cells.Add(c);
        _cells.Reverse();

        // 視線平滑（string pulling）：能直線看到就跳過中間格，航點變少、走起來直順。
        int anchor = 0;
        for (int i = 2; i < _cells.Count; i++)
        {
            if (!LineOfSight(_cells[anchor], _cells[i]))
            {
                anchor = i - 1;
                outPath.Add(CellCenter(_cells[anchor]));
            }
        }
        outPath.Add(CellCenter(_cells[_cells.Count - 1]));   // 終點格中心（最後貼近玩家由 actuator 的直線可達接手）

        // 怪卡在不可走格裡（貼牆/嵌進家具）→ 先把「最近可走格」插到最前面，讓牠先脫困到乾淨地板再照路走，
        // 不會一直頂著障礙磨（配合腳框做小，脫困更順）。
        if (!_walk[rawStart]) outPath.Insert(0, CellCenter(start));
        return outPath.Count > 0;
    }

    /// <summary>世界座標的視線檢查（用和 A* 同一份 _walk 格）：起訖任一格不可走、或直線經過不可走格 → false。
    /// 給 MonsterActuator.DirectClear 用，讓「直線可達」的判定和 A* 障礙（含家具膨脹）一致，避免細射線穿過家具淨空
    /// 而誤判可直走、結果撞上家具卡住。</summary>
    public bool HasLineOfSight(Vector2 fromW, Vector2 toW)
    {
        if (!_ready) return false;
        int a = WorldToIndex(fromW), b = WorldToIndex(toW);
        if (!_walk[a] || !_walk[b]) return false;
        return LineOfSight(a, b);
    }

    float Heur(int a, int b)
    {
        int ax = a % _w, ay = a / _w, bx = b % _w, by = b / _w;
        int dx = Mathf.Abs(ax - bx), dy = Mathf.Abs(ay - by);
        return (dx + dy) + (SQRT2 - 2f) * Mathf.Min(dx, dy);   // octile 距離
    }

    bool LineOfSight(int a, int b)
    {
        int x0 = a % _w, y0 = a / _w, x1 = b % _w, y1 = b / _w;
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy, x = x0, y = y0;
        while (true)
        {
            if (!Walk(x, y)) return false;
            if (x == x1 && y == y1) return true;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }

    // ── min-heap（依 _f 排序）──
    void HeapPush(int node) { _heap[_heapCount] = node; _heapIndex[node] = _heapCount; _heapCount++; HeapUp(_heapCount - 1); }
    int HeapPop()
    {
        int top = _heap[0]; _heapCount--;
        if (_heapCount > 0) { _heap[0] = _heap[_heapCount]; _heapIndex[_heap[0]] = 0; HeapDown(0); }
        _heapIndex[top] = -1; return top;
    }
    void HeapUp(int i)
    {
        while (i > 0) { int p = (i - 1) / 2; if (_f[_heap[i]] >= _f[_heap[p]]) break; Swap(i, p); i = p; }
    }
    void HeapDown(int i)
    {
        while (true)
        {
            int l = 2 * i + 1, r = 2 * i + 2, s = i;
            if (l < _heapCount && _f[_heap[l]] < _f[_heap[s]]) s = l;
            if (r < _heapCount && _f[_heap[r]] < _f[_heap[s]]) s = r;
            if (s == i) break; Swap(i, s); i = s;
        }
    }
    void Swap(int a, int b) { int t = _heap[a]; _heap[a] = _heap[b]; _heap[b] = t; _heapIndex[_heap[a]] = a; _heapIndex[_heap[b]] = b; }
}
