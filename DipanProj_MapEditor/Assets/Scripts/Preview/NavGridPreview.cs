using System.Collections.Generic;
using UnityEngine;
using DipanMapEditor.Data;
using DipanMapEditor.Core;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 編輯器版 A* 尋徑格（移植自主專案 MapNavGrid）。砍掉物理聯集——編輯器沒有牆碰撞體，
    /// 純用「可走層位元圖＋離牆淨空」，與遊戲端「無地上物家具」情況的路徑一致。
    /// 供劇情預覽 CutscenePreview 走位用，讓編輯器預覽的走位貼近遊戲。
    /// </summary>
    public class NavGridPreview
    {
        public float AgentRadius = 0.3f;

        int _w, _h;
        float _cell;
        Vector2 _originTL;
        bool[] _walk;
        bool _ready;
        float[] _g, _f; int[] _came, _heap, _heapIndex; int _heapCount;
        readonly List<int> _cells = new List<int>();

        public bool Ready => _ready;

        public void Build(MapData map)
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
            bool[] wall = new bool[n];
            for (int y = 0; y < _h; y++)
            {
                string row = (blocked != null && y < blocked.Count) ? blocked[y] : null;
                for (int x = 0; x < _w; x++)
                {
                    char c = (row != null && x < row.Length) ? row[x] : '1';
                    wall[y * _w + x] = (c != '0');
                }
            }
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
        int NearestWalkable(int idx)
        {
            int cx = idx % _w, cy = idx / _w;
            if (Walk(cx, cy)) return idx;
            int maxR = Mathf.Max(_w, _h);
            for (int r = 1; r <= maxR; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        if (Walk(cx + dx, cy + dy)) return (cy + dy) * _w + (cx + dx);
                    }
            return idx;
        }

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        const float SQRT2 = 1.41421356f;

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
                    if (DX[d] != 0 && DY[d] != 0 && (!Walk(cx + DX[d], cy) || !Walk(cx, cy + DY[d]))) continue;
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

            int anchor = 0;
            for (int i = 2; i < _cells.Count; i++)
            {
                if (!LineOfSight(_cells[anchor], _cells[i]))
                {
                    anchor = i - 1;
                    outPath.Add(CellCenter(_cells[anchor]));
                }
            }
            outPath.Add(CellCenter(_cells[_cells.Count - 1]));
            if (!_walk[rawStart]) outPath.Insert(0, CellCenter(start));
            return outPath.Count > 0;
        }

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
            return (dx + dy) + (SQRT2 - 2f) * Mathf.Min(dx, dy);
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

        void HeapPush(int node) { _heap[_heapCount] = node; _heapIndex[node] = _heapCount; _heapCount++; HeapUp(_heapCount - 1); }
        int HeapPop()
        {
            int top = _heap[0]; _heapCount--;
            if (_heapCount > 0) { _heap[0] = _heap[_heapCount]; _heapIndex[_heap[0]] = 0; HeapDown(0); }
            _heapIndex[top] = -1; return top;
        }
        void HeapUp(int i) { while (i > 0) { int p = (i - 1) / 2; if (_f[_heap[i]] >= _f[_heap[p]]) break; Swap(i, p); i = p; } }
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
}
