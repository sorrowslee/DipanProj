using System.Collections.Generic;
using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 地上物工具：選了素材後左鍵放置、點選既有物件、拖曳移動。支援多選。
    /// 翻轉/縮放/旋轉/刪除由 EditorUI 的選取面板呼叫本元件的 API。
    ///
    /// 選取操作：
    ///  ‧ 一般左鍵點物件＝只選它一個（收回多選）；點空白（無筆刷）＝拖曳框選一片、點一下＝清空選取。
    ///  ‧ Cmd（Mac Command / Win 鍵）＋點物件＝加選/取消該物件（多選）。
    ///  ‧ Ctrl＋拖曳＝移動；點到的是選取集合成員→整組一起移動、否則收成單選再拖。
    ///  ‧ 複製＝把選取的每個各複製一份、選取換成新複本；ESC＝取消選取。
    /// </summary>
    public class ObjectController : MonoBehaviour
    {
        // ── 選取集合（最後加入者＝主選取，給單選面板/座標等單物件操作用）──
        readonly List<ObjectInstance> _selection = new List<ObjectInstance>();
        public IReadOnlyList<ObjectInstance> Selection => _selection;
        public int SelectionCount => _selection.Count;
        public ObjectInstance Selected => _selection.Count > 0 ? _selection[_selection.Count - 1] : null;

        Camera _cam;
        EditorUI _ui;
        ObjectView _view;

        // ── 拖曳移動（整組）──
        bool _dragging;
        Vector2 _dragStartWorld;
        readonly Dictionary<ObjectInstance, Vector2> _dragStart = new Dictionary<ObjectInstance, Vector2>();

        // ── 框選（拖方框圈一片）──
        public bool BoxSelecting { get; private set; }
        public Vector2 BoxStartWorld { get; private set; }
        public Vector2 BoxCurWorld { get; private set; }

        void Start()
        {
            _cam = Camera.main;
            _ui = FindObjectOfType<EditorUI>();
            _view = FindObjectOfType<ObjectView>();
        }

        void OnEnable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged += OnMapChanged;
                MapSession.Instance.OnMapRebuilt += OnMapChanged;
            }
        }

        void OnDisable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged -= OnMapChanged;
                MapSession.Instance.OnMapRebuilt -= OnMapChanged;
            }
        }

        void OnMapChanged(MapData _) => Deselect();

        void Update()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_view == null) _view = FindObjectOfType<ObjectView>();
            if (_ui == null || _view == null) return;

            if (_ui.CurrentTool != EditTool.Object) { _dragging = false; BoxSelecting = false; return; }

            // ESC：退出筆刷——取消放置筆刷（不再有幻影/誤放）＋取消選取，回到可點選既有物件
            if (Input.GetKeyDown(KeyCode.Escape)) { Deselect(); _ui.ClearObjectBrush(); return; }

            var map = session.Map;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool cmd = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
                    || Input.GetKey(KeyCode.LeftWindows) || Input.GetKey(KeyCode.RightWindows);

            if (Input.GetMouseButtonDown(0) && !_ui.IsPointerOverUI(Input.mousePosition))
            {
                Vector3 w3 = _cam.ScreenToWorldPoint(Input.mousePosition); w3.z = 0;
                Vector2 w = w3;
                var hit = Pick(w3, map);

                if (ctrl)
                {
                    // Ctrl+左鍵：移動。點到集合成員→拖整組；點到集合外的物件→先收成單選再拖；點空白→不動作
                    if (hit != null)
                    {
                        if (!_selection.Contains(hit)) SelectSingle(hit);
                        UndoManager.Push();   // 移動前快照（整段拖曳算一步）
                        BeginDrag(w);
                    }
                }
                else if (cmd)
                {
                    // Cmd+左鍵：加選/取消該物件（多選）。點空白不動作。
                    if (hit != null) ToggleSelect(hit);
                }
                else
                {
                    // 一般左鍵
                    if (hit != null)
                    {
                        SelectSingle(hit);   // 點到物件＝只選它一個（收回多選）
                        _dragging = false;
                    }
                    else if (!string.IsNullOrEmpty(_ui.SelectedObjectAssetId))
                    {
                        UndoManager.Push();   // 放置前快照
                        SelectSingle(Place(_ui.SelectedObjectAssetId, w3, map));
                        _dragging = false;
                        _ui.ClearObjectBrush();   // 放完即取消筆刷，避免下一次點擊又放一個
                    }
                    else
                    {
                        // 點空白、無筆刷 → 開始框選（拖方框圈一片；沒拖動就當作點空白清空選取）
                        BoxSelecting = true; BoxStartWorld = w; BoxCurWorld = w;
                    }
                }
            }
            else if (_dragging && Input.GetMouseButton(0) && _selection.Count > 0)
            {
                Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition); w.z = 0;
                ApplyDrag(new Vector2(w.x, w.y), map);
            }
            else if (BoxSelecting && Input.GetMouseButton(0))
            {
                Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition); w.z = 0;
                BoxCurWorld = new Vector2(w.x, w.y);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (BoxSelecting)
                {
                    float moved = (BoxCurWorld - BoxStartWorld).magnitude;
                    if (moved < map.tileSize * 0.15f) DeselectAll();          // 幾乎沒拖＝點空白 → 清空選取
                    else SelectInBox(BoxStartWorld, BoxCurWorld, map);        // 有拖＝框選圈到的全選
                    BoxSelecting = false;
                }
                _dragging = false;
            }
        }

        ObjectInstance Place(string assetId, Vector3 world, MapData map)
        {
            var inst = new ObjectInstance
            {
                assetId = assetId,
                x = world.x, y = world.y,
                scaleX = 1f, scaleY = 1f,
                sortKey = world.y,
            };
            map.GameLayer.objects.Add(inst);
            _view.Apply(inst, map);
            return inst;
        }

        ObjectInstance Pick(Vector3 world, MapData map)
        {
            ObjectInstance best = null;
            float bestY = float.PositiveInfinity;   // 越小（越下方）越前面
            foreach (var inst in map.GameLayer.objects)
            {
                if (!_view.TryGetWorldBounds(inst, out var b)) continue;
                if (world.x < b.min.x || world.x > b.max.x || world.y < b.min.y || world.y > b.max.y) continue;
                if (inst.y < bestY) { bestY = inst.y; best = inst; }
            }
            return best;
        }

        // ---- 選取集合操作 ----

        void SelectSingle(ObjectInstance o) { _selection.Clear(); if (o != null) _selection.Add(o); }
        void ToggleSelect(ObjectInstance o) { if (o == null) return; if (!_selection.Remove(o)) _selection.Add(o); }
        void DeselectAll() { _selection.Clear(); }

        void SelectInBox(Vector2 a, Vector2 b, MapData map)
        {
            float minx = Mathf.Min(a.x, b.x), maxx = Mathf.Max(a.x, b.x);
            float miny = Mathf.Min(a.y, b.y), maxy = Mathf.Max(a.y, b.y);
            _selection.Clear();
            foreach (var o in map.GameLayer.objects)
            {
                if (!_view.TryGetWorldBounds(o, out var bd)) continue;
                // AABB 相交（框有碰到物件外框就選）
                if (bd.max.x < minx || bd.min.x > maxx || bd.max.y < miny || bd.min.y > maxy) continue;
                _selection.Add(o);
            }
        }

        // ---- 拖曳移動（整組；單選時保留磁吸對齊）----

        void BeginDrag(Vector2 world)
        {
            _dragging = true;
            _dragStartWorld = world;
            _dragStart.Clear();
            foreach (var o in _selection) _dragStart[o] = new Vector2(o.x, o.y);
        }

        void ApplyDrag(Vector2 world, MapData map)
        {
            Vector2 delta = world - _dragStartWorld;
            if (_selection.Count == 1)
            {
                var o = _selection[0];
                Vector2 st = _dragStart.TryGetValue(o, out var s) ? s : new Vector2(o.x, o.y);
                float nx = st.x + delta.x, ny = st.y + delta.y;
                SnapToNeighbors(o, ref nx, ref ny, map);   // 只有單選才磁吸對齊（多選一起吸會亂跳）
                o.x = nx; o.y = ny; o.sortKey = ny;
                _view.Apply(o, map);
            }
            else
            {
                foreach (var o in _selection)
                {
                    Vector2 st = _dragStart.TryGetValue(o, out var s) ? s : new Vector2(o.x, o.y);
                    o.x = st.x + delta.x; o.y = st.y + delta.y; o.sortKey = o.y;
                    _view.Apply(o, map);
                }
            }
        }

        // ---- 供 UI 選取面板呼叫（單物件操作作用在主選取 Selected）----

        public void FlipH() { if (Selected != null) { Selected.flipX = !Selected.flipX; Reapply(); } }
        public void FlipV() { if (Selected != null) { Selected.flipY = !Selected.flipY; Reapply(); } }

        public void ScaleBy(float factor)
        {
            if (Selected == null) return;
            Selected.scaleX = Mathf.Clamp(Selected.scaleX * factor, 0.05f, 50f);
            Selected.scaleY = Mathf.Clamp(Selected.scaleY * factor, 0.05f, 50f);
            Reapply();
        }

        public void Rotate(float deltaDeg)
        {
            if (Selected == null) return;
            Selected.rot += deltaDeg;
            Reapply();
        }

        /// <summary>上移一層（往前蓋住別人）。</summary>
        public void RaiseZ() { if (Selected != null) { Selected.zOrder++; Reapply(); } }
        /// <summary>下移一層（往後被蓋）。</summary>
        public void LowerZ() { if (Selected != null) { Selected.zOrder--; Reapply(); } }

        /// <summary>直接把主選取物件移到指定座標（座標輸入框用）。</summary>
        public void SetPosition(float x, float y)
        {
            if (Selected == null) return;
            Selected.x = x; Selected.y = y; Selected.sortKey = y;
            _view.Apply(Selected, MapSession.Instance.Map);
        }

        /// <summary>
        /// 把 (x,y) 磁吸到鄰近物件的邊緣或中心（門檻內才吸，拉遠即脫離）。
        /// X / Y 各自獨立判斷，方便橫梁端對端對齊。
        /// </summary>
        void SnapToNeighbors(ObjectInstance moving, ref float x, ref float y, MapData map)
        {
            if (!_view.TryGetWorldBounds(moving, out var mb)) return;
            float ex = mb.extents.x, ey = mb.extents.y;
            float thr = map.tileSize * 0.35f;
            float curX = x, curY = y;
            float bestDX = thr, bestDY = thr, snapX = curX, snapY = curY;
            bool sx = false, sy = false;

            foreach (var o in map.GameLayer.objects)
            {
                if (o == moving) continue;
                if (!_view.TryGetWorldBounds(o, out var ob)) continue;

                // X 候選：右邊貼左、左邊貼右、左對左、右對右、中心對中心
                float[] xc = { ob.min.x - ex, ob.max.x + ex, ob.min.x + ex, ob.max.x - ex, ob.center.x };
                foreach (var c in xc) { float d = Mathf.Abs(c - curX); if (d < bestDX) { bestDX = d; snapX = c; sx = true; } }

                float[] yc = { ob.min.y - ey, ob.max.y + ey, ob.min.y + ey, ob.max.y - ey, ob.center.y };
                foreach (var c in yc) { float d = Mathf.Abs(c - curY); if (d < bestDY) { bestDY = d; snapY = c; sy = true; } }
            }

            if (sx) x = snapX;
            if (sy) y = snapY;
        }

        /// <summary>複製當前選取的每個物件（含大小/翻轉/角度/各欄位），生成在旁邊，並把選取換成那批新複本。</summary>
        public void DuplicateSelected()
        {
            if (_selection.Count == 0) return;
            var map = MapSession.Instance.Map;
            float off = map.tileSize * 0.5f;
            var copies = new List<ObjectInstance>(_selection.Count);
            foreach (var s in _selection)
            {
                var copy = new ObjectInstance
                {
                    assetId = s.assetId,
                    x = s.x + off,
                    y = s.y - off,
                    rot = s.rot,
                    flipX = s.flipX,
                    flipY = s.flipY,
                    scaleX = s.scaleX,
                    scaleY = s.scaleY,
                    zOrder = s.zOrder,
                    hp = s.hp,
                    animFps = s.animFps,
                    pingPong = s.pingPong,
                    walkable = s.walkable,
                    breakFlag = s.breakFlag,
                    appearAfterClears = s.appearAfterClears,
                    appearScope = s.appearScope,
                };
                copy.sortKey = copy.y;
                map.GameLayer.objects.Add(copy);
                _view.Apply(copy, map);
                copies.Add(copy);
            }
            _selection.Clear();
            _selection.AddRange(copies);   // 選取換成新複本（可接著 Ctrl 拖一起搬）
            _dragging = false;
        }

        public void DeleteSelected()
        {
            if (_selection.Count == 0) return;
            var map = MapSession.Instance.Map;
            foreach (var s in _selection)
            {
                map.GameLayer.objects.Remove(s);
                _view.Remove(s);
            }
            _selection.Clear();
            _dragging = false;
        }

        public void Deselect() { _selection.Clear(); _dragging = false; BoxSelecting = false; }

        void Reapply()
        {
            if (Selected != null) _view.Apply(Selected, MapSession.Instance.Map);
        }
    }
}
