using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// NPC 工具：新增／選取／拖曳／複製／刪除 NPC 擺放，以及**路徑點**的加點／拖曳。
    /// 參數（角色/行為/對話/介面/接鏈）由 EditorUI 的面板編輯；畫布互動集中在這裡（模式同 LightController）。
    ///
    /// 畫布操作：
    ///   ① 直接拖曳 NPC（或其路徑點小方塊）。點誰拖誰；路徑點把手比較小、優先命中。
    ///   ② 「＋ 新增 NPC」生在畫面中心並進放置模式，點一下定位。
    ///   ③ 「＋ 加路徑點」進入**連續加點模式**：每點一下畫布加一個路徑點，ESC / 再按一次按鈕結束。
    /// </summary>
    public class NpcController : MonoBehaviour
    {
        public NpcInstance Selected { get; private set; }

        /// <summary>true＝下一次點畫布是「把選取中 NPC 的站位移到該處」。</summary>
        public bool Placing { get; private set; }
        /// <summary>true＝連續加路徑點模式（每點一下加一點）。</summary>
        public bool AddingWaypoints { get; private set; }
        /// <summary>正在拖曳的路徑點索引；-1＝沒有。</summary>
        public int DraggingWaypoint { get; private set; } = -1;
        /// <summary>true＝正在拖曳 NPC 本體。</summary>
        public bool DraggingNpc { get; private set; }

        Vector2 _dragStartMouse, _dragStartPos;
        bool _dragMoved;

        Camera _cam;
        EditorUI _ui;

        void Start()
        {
            _cam = Camera.main;
            _ui = FindObjectOfType<EditorUI>();
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

        void OnMapChanged(MapData _) { ClearModes(); Selected = null; }

        void ClearModes() { Placing = false; AddingWaypoints = false; DraggingNpc = false; DraggingWaypoint = -1; }

        void Update()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ui == null) return;

            if (_ui.CurrentTool != EditTool.Npc) { ClearModes(); return; }

            var map = session.Map;
            if (map.npcs == null) map.npcs = new System.Collections.Generic.List<NpcInstance>();   // 舊地圖補欄
            if (Selected != null && !map.npcs.Contains(Selected)) { ClearModes(); Selected = null; }

            if (Input.GetKeyDown(KeyCode.Escape)) { ClearModes(); return; }

            Vector3 mw = _cam.ScreenToWorldPoint(Input.mousePosition); mw.z = 0f;

            // ── 拖曳中（NPC 本體或路徑點）──
            if (DraggingNpc || DraggingWaypoint >= 0)
            {
                if (Input.GetMouseButton(0) && Selected != null)
                {
                    Vector2 delta = (Vector2)mw - _dragStartMouse;
                    if (!_dragMoved && delta.sqrMagnitude > 0.0001f)
                    {
                        SetDragTarget(_dragStartPos);   // 先還原成拖曳起點，Undo 快照才是「拖之前」
                        UndoManager.Push();
                        _dragMoved = true;
                    }
                    if (_dragMoved) SetDragTarget(_dragStartPos + delta);
                }
                else
                {
                    DraggingNpc = false; DraggingWaypoint = -1; _dragMoved = false;
                }
                return;
            }

            if (!Input.GetMouseButtonDown(0) || _ui.IsPointerOverUI(Input.mousePosition)) return;

            // ── 連續加路徑點 ──
            if (AddingWaypoints && Selected != null)
            {
                UndoManager.Push();
                if (Selected.waypoints == null) Selected.waypoints = new System.Collections.Generic.List<Vec2>();
                Selected.waypoints.Add(new Vec2(mw.x, mw.y));
                Selected.behavior = NpcInstance.BehaviorPatrol;   // 加了點自動切成來回走動
                return;
            }

            // ── 放置站位 ──
            if (Placing && Selected != null)
            {
                UndoManager.Push();
                Selected.x = mw.x; Selected.y = mw.y;
                Placing = false;
                return;
            }

            // ── 點選：先試「選取中 NPC 的路徑點」（把手小、優先），再試 NPC 本體 ──
            if (Selected != null && Selected.waypoints != null)
            {
                int wp = PickWaypoint(Selected, mw, WaypointPickR(map));
                if (wp >= 0)
                {
                    DraggingWaypoint = wp; _dragMoved = false;
                    _dragStartMouse = mw;
                    _dragStartPos = new Vector2(Selected.waypoints[wp].x, Selected.waypoints[wp].y);
                    return;
                }
            }

            var hit = PickNearest(map, mw);
            if (hit != null)
            {
                Selected = hit;
                DraggingNpc = true; _dragMoved = false;
                _dragStartMouse = mw;
                _dragStartPos = new Vector2(hit.x, hit.y);
            }
        }

        void SetDragTarget(Vector2 pos)
        {
            if (Selected == null) return;
            if (DraggingNpc) { Selected.x = pos.x; Selected.y = pos.y; }
            else if (DraggingWaypoint >= 0 && Selected.waypoints != null && DraggingWaypoint < Selected.waypoints.Count)
            {
                Selected.waypoints[DraggingWaypoint].x = pos.x;
                Selected.waypoints[DraggingWaypoint].y = pos.y;
            }
        }

        public static float NpcPickR(MapData map) => Mathf.Max(0.45f, map.tileSize * 0.55f);
        public static float WaypointPickR(MapData map) => Mathf.Max(0.25f, map.tileSize * 0.3f);

        static int PickWaypoint(NpcInstance npc, Vector3 world, float r)
        {
            int best = -1; float bestSq = r * r;
            for (int i = 0; i < npc.waypoints.Count; i++)
            {
                var w = npc.waypoints[i];
                float dx = w.x - world.x, dy = w.y - world.y, sq = dx * dx + dy * dy;
                if (sq <= bestSq) { bestSq = sq; best = i; }
            }
            return best;
        }

        static NpcInstance PickNearest(MapData map, Vector3 world)
        {
            if (map.npcs == null) return null;
            float r = NpcPickR(map);
            NpcInstance best = null; float bestSq = r * r;
            foreach (var n in map.npcs)
            {
                if (n == null) continue;
                float dx = n.x - world.x, dy = n.y - world.y, sq = dx * dx + dy * dy;
                if (sq <= bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }

        /// <summary>在畫面中心新增一個 NPC（用 NpcTable 的第一列當預設角色），並進放置模式。</summary>
        public void NewNpc(int npcId)
        {
            var map = MapSession.Instance?.Map;
            if (map == null) return;
            if (map.npcs == null) map.npcs = new System.Collections.Generic.List<NpcInstance>();
            UndoManager.Push();

            Vector3 c = _cam != null
                ? new Vector3(_cam.transform.position.x, _cam.transform.position.y, 0f)
                : new Vector3(map.origin.x + map.width * map.tileSize * 0.5f,
                              map.origin.y - map.height * map.tileSize * 0.5f, 0f);

            var npc = new NpcInstance
            {
                id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                npcId = npcId,
                x = c.x, y = c.y,
            };
            map.npcs.Add(npc);
            Selected = npc;
            ClearModes();
            Placing = true;   // 新增完直接點一下定位
        }

        /// <summary>複製選取中的 NPC（含參數與路徑點，路徑點整組平移一格），新的進放置模式。</summary>
        public void DuplicateSelected()
        {
            var map = MapSession.Instance?.Map;
            if (map == null || Selected == null) return;
            UndoManager.Push();
            var s = Selected;
            var copy = new NpcInstance
            {
                id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                name = s.name, npcId = s.npcId,
                x = s.x + map.tileSize, y = s.y,
                behavior = s.behavior, speed = s.speed, dwellSeconds = s.dwellSeconds,
                dramaId = s.dramaId, panelId = s.panelId, panelArg = s.panelArg,
                next = s.next, setFlag = s.setFlag, disappearFlag = s.disappearFlag,
            };
            if (s.waypoints != null)
                foreach (var w in s.waypoints) copy.waypoints.Add(new Vec2(w.x + map.tileSize, w.y));
            map.npcs.Add(copy);
            Selected = copy;
            ClearModes();
            Placing = true;
        }

        public void DeleteSelected()
        {
            var map = MapSession.Instance?.Map;
            if (map == null || Selected == null || map.npcs == null) return;
            UndoManager.Push();
            map.npcs.Remove(Selected);
            Selected = null;
            ClearModes();
        }

        public void RemoveWaypoint(int index)
        {
            if (Selected?.waypoints == null || index < 0 || index >= Selected.waypoints.Count) return;
            UndoManager.Push();
            Selected.waypoints.RemoveAt(index);
        }

        public void Select(NpcInstance n) { Selected = n; ClearModes(); }
        public void BeginPlace() { if (Selected != null) { ClearModes(); Placing = true; } }
        public void ToggleAddWaypoints()
        {
            if (Selected == null) return;
            bool on = !AddingWaypoints;
            ClearModes();
            AddingWaypoints = on;
        }
        public void Deselect() { Selected = null; ClearModes(); }
    }
}
