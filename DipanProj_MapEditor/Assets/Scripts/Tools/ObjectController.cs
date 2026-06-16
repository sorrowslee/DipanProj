using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 地上物工具：選了素材後左鍵放置、點選既有物件、拖曳移動。
    /// 翻轉/縮放/旋轉/刪除由 EditorUI 的選取面板呼叫本元件的 API。
    /// </summary>
    public class ObjectController : MonoBehaviour
    {
        public ObjectInstance Selected { get; private set; }

        Camera _cam;
        EditorUI _ui;
        ObjectView _view;
        bool _dragging;
        Vector2 _dragOffset;

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

            if (_ui.CurrentTool != EditTool.Object) { _dragging = false; return; }

            // ESC：取消選取（移除藍框，方便看擺放結果）
            if (Input.GetKeyDown(KeyCode.Escape)) { Deselect(); return; }

            var map = session.Map;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (Input.GetMouseButtonDown(0) && !_ui.IsPointerOverUI(Input.mousePosition))
            {
                Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition); w.z = 0;
                var hit = Pick(w, map);
                if (ctrl)
                {
                    // Ctrl+左鍵：移動命中的物件（避免誤移，移動一律要 Ctrl）
                    if (hit != null)
                    {
                        UndoManager.Push();   // 移動前快照（整段拖曳算一步）
                        Selected = hit;
                        _dragging = true;
                        _dragOffset = new Vector2(hit.x - w.x, hit.y - w.y);
                    }
                }
                else
                {
                    // 一般左鍵：點到物件＝只選取（不移動）；點空白＝放置新物件
                    if (hit != null)
                    {
                        Selected = hit;
                        _dragging = false;
                    }
                    else if (!string.IsNullOrEmpty(_ui.SelectedObjectAssetId))
                    {
                        UndoManager.Push();   // 放置前快照
                        Selected = Place(_ui.SelectedObjectAssetId, w, map);
                        _dragging = false;
                        _ui.ClearObjectBrush();   // 放完即取消筆刷，避免下一次點擊又放一個
                    }
                }
            }
            else if (_dragging && Input.GetMouseButton(0) && Selected != null)
            {
                Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition); w.z = 0;
                float nx = w.x + _dragOffset.x;
                float ny = w.y + _dragOffset.y;
                SnapToNeighbors(Selected, ref nx, ref ny, map);   // 邊緣/中心磁吸對齊
                Selected.x = nx; Selected.y = ny; Selected.sortKey = ny;
                _view.Apply(Selected, map);
            }
            else if (Input.GetMouseButtonUp(0))
            {
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

        // ---- 供 UI 選取面板呼叫 ----

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

        /// <summary>直接把選取物件移到指定座標（座標輸入框用）。</summary>
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

        /// <summary>複製當前選取的物件（含大小/翻轉/角度），生成在旁邊並選取它。</summary>
        public void DuplicateSelected()
        {
            if (Selected == null) return;
            var map = MapSession.Instance.Map;
            float off = map.tileSize * 0.5f;
            var copy = new ObjectInstance
            {
                assetId = Selected.assetId,
                x = Selected.x + off,
                y = Selected.y - off,
                rot = Selected.rot,
                flipX = Selected.flipX,
                flipY = Selected.flipY,
                scaleX = Selected.scaleX,
                scaleY = Selected.scaleY,
                zOrder = Selected.zOrder,
            };
            copy.sortKey = copy.y;
            map.GameLayer.objects.Add(copy);
            _view.Apply(copy, map);
            Selected = copy;
            _dragging = false;
        }

        public void DeleteSelected()
        {
            if (Selected == null) return;
            MapSession.Instance.Map.GameLayer.objects.Remove(Selected);
            _view.Remove(Selected);
            Selected = null;
            _dragging = false;
        }

        public void Deselect() { Selected = null; _dragging = false; }

        void Reapply()
        {
            if (Selected != null) _view.Apply(Selected, MapSession.Instance.Map);
        }
    }
}
