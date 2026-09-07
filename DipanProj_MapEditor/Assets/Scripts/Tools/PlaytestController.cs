using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 試走模式：在編輯器裡直接用 WASD／方向鍵走一遍，即時驗證可走層對不對。
    ///
    /// <para><b>為什麼要有</b>：可走層以前只能「塗完 → 存檔 → Sync Map Assets → 進主遊戲 → 走走看 →
    /// 回來改」，一輪好幾分鐘，而「看起來能走卻走不過去」這種問題往往要走到那一格才會發現
    /// （PROBLEMS <b>B9</b>）。這個模式把驗證循環縮到 0 秒。</para>
    ///
    /// <para><b>刻意與主遊戲對齊的三件事</b>（不然「在編輯器裡走得過」不代表遊戲裡走得過）：
    /// ① 碰撞用<b>圓</b>不是點，半徑同主遊戲的 <c>CircleCollider2D</c>；
    /// ② 分軸移動（先試 X 再試 Y），所以會沿牆滑動而不是卡死；
    /// ③ 相機 <c>orthographicSize</c> 固定成主遊戲跟隨模式的值（畫面高 10 格），並夾在地圖邊界內 ——
    /// 視野一樣，才看得出「這條路在遊戲裡會不會太窄」。</para>
    ///
    /// <para>⚠ 這裡<b>只讀可走層</b>，不管地上物的碰撞 —— 那是主遊戲 <c>MapLoader</c> 依素材 alpha 烘出來的，
    /// 編輯器沒有那套。所以「擋路的家具」在這裡走得過去是正常的，別拿來當 bug（見 PROBLEMS <b>B9</b>：
    /// 擋路碰撞與可走層本來就是兩份獨立的真相）。</para>
    /// </summary>
    public class PlaytestController : MonoBehaviour
    {
        [Header("與主遊戲對齊的參數")]
        [Tooltip("移動速度（世界單位／秒）。主遊戲 PlayerController.MoveSpeed = 5。")]
        public float moveSpeed = 5f;

        [Tooltip("碰撞半徑（世界單位）。主遊戲玩家是 CircleCollider2D，半徑 × PlayerScale；預設 0.4。")]
        public float bodyRadius = 0.4f;

        [Tooltip("畫面顯示的高度（格）。主遊戲 MapCameraController.followViewHeightTiles = 10。")]
        public float viewHeightTiles = 10f;

        [Header("外觀")]
        public Color bodyColor = new Color(0.3f, 0.9f, 0.4f, 0.75f);
        public Color edgeColor = new Color(1f, 1f, 1f, 0.95f);

        /// <summary>試走中玩家的世界座標（EditorUI 的狀態列會顯示）。</summary>
        public Vector2 PlayerPos { get; private set; }

        /// <summary>這一幀有沒有被牆擋住（EditorUI 拿去給即時提示）。</summary>
        public bool BlockedThisFrame { get; private set; }

        EditorUI _ui;
        Camera _cam;
        GameObject _body;
        SpriteRenderer _sr;
        bool _active;

        // 進入試走前的相機狀態，離開時原樣還原（不然作者會發現畫面被搬走了）
        Vector3 _camPosBefore;
        float _camSizeBefore;

        void Update()
        {
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_cam == null) _cam = Camera.main;
            var map = MapSession.Instance?.Map;

            bool want = _ui != null && _ui.CurrentTool == EditTool.Playtest && map != null;
            if (want != _active)
            {
                if (want) Enter(map); else Exit();
            }
            if (!_active || map == null) return;

            Step(map);
        }

        void Enter(MapData map)
        {
            _active = true;
            if (_cam != null) { _camPosBefore = _cam.transform.position; _camSizeBefore = _cam.orthographicSize; }

            PlayerPos = FindStart(map);
            EnsureBody(map);
            SnapCamera(map);
        }

        void Exit()
        {
            _active = false;
            if (_sr != null) _sr.enabled = false;
            if (_cam != null)
            {
                _cam.transform.position = _camPosBefore;
                if (_camSizeBefore > 0f) _cam.orthographicSize = _camSizeBefore;
            }
        }

        void Step(MapData map)
        {
            float dx = 0f, dy = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dx -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dx += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dy += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dy -= 1f;

            var dir = new Vector2(dx, dy);
            if (dir.sqrMagnitude > 1f) dir.Normalize();   // 斜走不加速（同主遊戲）

            float step = moveSpeed * map.tileSize * Time.deltaTime;
            Vector2 p = PlayerPos;
            BlockedThisFrame = false;

            // 分軸推進：X 過不去就只走 Y（＝沿牆滑動）。整體一起判會在斜貼牆時完全卡住。
            if (!Mathf.Approximately(dir.x, 0f))
            {
                var nx = new Vector2(p.x + dir.x * step, p.y);
                if (!Overlaps(map, nx, bodyRadius * map.tileSize)) p = nx; else BlockedThisFrame = true;
            }
            if (!Mathf.Approximately(dir.y, 0f))
            {
                var ny = new Vector2(p.x, p.y + dir.y * step);
                if (!Overlaps(map, ny, bodyRadius * map.tileSize)) p = ny; else BlockedThisFrame = true;
            }
            PlayerPos = p;

            if (_body != null) _body.transform.position = new Vector3(p.x, p.y, 0f);
            SnapCamera(map);
        }

        // ---- 碰撞：圓 vs 子格 ----

        /// <summary>
        /// 圓心 <paramref name="p"/>、半徑 <paramref name="r"/> 的圓有沒有壓到任何「擋腳」的子格。
        /// 掃圓的包圍盒（半徑 0.4 格、子格 0.25 格 ⇒ 大約 4×4 格，很便宜），
        /// 逐格做「圓心到矩形最近點」的距離判定 —— 不是只測圓周幾個點，所以不會穿過細牆。
        /// 範圍外由 <see cref="WalkableOps.IsBlocked"/> 回 true（地圖外＝牆），走不出去。
        /// </summary>
        static bool Overlaps(MapData map, Vector2 p, float r)
        {
            float fs = MapCoords.FineSize(map);
            var lt = MapCoords.WorldToFineCell(new Vector2(p.x - r, p.y + r), map);
            var rb = MapCoords.WorldToFineCell(new Vector2(p.x + r, p.y - r), map);
            for (int fy = lt.y; fy <= rb.y; fy++)
            {
                for (int fx = lt.x; fx <= rb.x; fx++)
                {
                    if (!WalkableOps.IsBlocked(map, fx, fy)) continue;
                    Vector2 tl = MapCoords.FineCellTopLeft(fx, fy, map);
                    float cx = Mathf.Clamp(p.x, tl.x, tl.x + fs);
                    float cy = Mathf.Clamp(p.y, tl.y - fs, tl.y);
                    float ddx = cx - p.x, ddy = cy - p.y;
                    if (ddx * ddx + ddy * ddy < r * r) return true;
                }
            }
            return false;
        }

        // ---- 起點 ----

        /// <summary>起點：玩家出生點 trigger → 沒有就地圖中心；落在牆上就往外螺旋找最近的可走格。</summary>
        Vector2 FindStart(MapData map)
        {
            Vector2 want = MapCoords.CellCenter(map.width / 2, map.height / 2, map.tileSize, MapCoords.Origin(map));
            var trig = map.TriggerLayer;
            if (trig?.regions != null)
            {
                foreach (var reg in trig.regions)
                {
                    if (reg == null || reg.typeId != "playerSpawn" || reg.cells == null || reg.cells.Count == 0) continue;
                    Vector2 sum = Vector2.zero; int n = 0;
                    foreach (var c in reg.cells)
                        if (c != null && c.Length >= 2)
                        { sum += MapCoords.CellCenter(c[0], c[1], map.tileSize, MapCoords.Origin(map)); n++; }
                    if (n > 0) { want = sum / n; break; }
                }
            }

            float r = bodyRadius * map.tileSize;
            if (!Overlaps(map, want, r)) return want;

            var start = MapCoords.WorldToFineCell(want, map);
            for (int rad = 1; rad < Mathf.Max(map.FineWidth, map.FineHeight); rad++)
            {
                for (int dy = -rad; dy <= rad; dy++)
                {
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        if (Mathf.Abs(dx) != rad && Mathf.Abs(dy) != rad) continue;   // 只掃當前這一圈
                        int fx = start.x + dx, fy = start.y + dy;
                        if (!MapCoords.InBoundsFine(fx, fy, map)) continue;
                        Vector2 c = MapCoords.FineCellCenter(fx, fy, map);
                        if (!Overlaps(map, c, r)) return c;
                    }
                }
            }
            return want;   // 整張圖都走不了（例如全牆）：就放中心，讓作者自己看出來
        }

        // ---- 相機：與主遊戲跟隨模式一致 ----

        void SnapCamera(MapData map)
        {
            if (_cam == null || !_cam.orthographic) return;
            _cam.orthographicSize = viewHeightTiles * map.tileSize * 0.5f;

            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            float w = map.width * map.tileSize, h = map.height * map.tileSize;
            float xMin = map.origin.x, xMax = map.origin.x + w;
            float yMin = map.origin.y - h, yMax = map.origin.y;

            float x = (w <= halfW * 2f) ? (xMin + xMax) * 0.5f : Mathf.Clamp(PlayerPos.x, xMin + halfW, xMax - halfW);
            float y = (h <= halfH * 2f) ? (yMin + yMax) * 0.5f : Mathf.Clamp(PlayerPos.y, yMin + halfH, yMax - halfH);
            _cam.transform.position = new Vector3(x, y, _cam.transform.position.z);
        }

        // ---- 玩家標記（程序生成，不需要任何素材）----

        void EnsureBody(MapData map)
        {
            if (_body == null)
            {
                _body = new GameObject("PlaytestBody");
                _body.transform.SetParent(transform, false);
                _sr = _body.AddComponent<SpriteRenderer>();
                _sr.sprite = MakeCircle(64, bodyColor, edgeColor);
                _sr.sortingOrder = 30000;   // 畫在所有東西之上（這是編輯輔助，不是場景內容）
            }
            _sr.enabled = true;
            // sprite 是 64px、PPU 設成「直徑 = 2×bodyRadius 個世界單位」
            float diameter = bodyRadius * 2f * map.tileSize;
            _body.transform.localScale = Vector3.one * diameter;
            _body.transform.position = new Vector3(PlayerPos.x, PlayerPos.y, 0f);
        }

        static Sprite MakeCircle(int size, Color fill, Color edge)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (size - 1) * 0.5f, rOut = size * 0.5f, rIn = rOut - Mathf.Max(1.5f, size * 0.06f);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * size + x] = d > rOut ? Color.clear : (d > rIn ? edge : fill);
                }
            tex.SetPixels(px);
            tex.Apply();
            // PPU = size ⇒ sprite 的世界尺寸剛好 1×1，再用 localScale 縮成想要的直徑
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
