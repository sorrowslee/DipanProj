using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 照明工具：新增／選取／移動／複製／刪除「獨立光源」（不綁地上物的照明點）。
    /// 參數（半徑／亮度／光色／搖晃…）由 EditorUI 的面板編輯；位置由本元件在畫布點放。
    /// 視覺（光圈與中心十字）由 <see cref="LightOverlay"/> 畫。
    ///
    /// 操作方式：
    ///   ① **直接拖曳**——按住畫布上的燈拖，跟拖地上物一樣（主要用法）。
    ///   ② 「＋ 新增光源」生在畫面中心並直接進入放置模式，點一下定位。
    ///   ③ 選取後按「放置位置」再點畫布（從清單選了某盞、想重放時用）。
    /// </summary>
    public class LightController : MonoBehaviour
    {
        public LightInstance Selected { get; private set; }

        /// <summary>true＝下一次點畫布是「把選取中的光源移到該處」。</summary>
        public bool Placing { get; private set; }

        /// <summary>true＝正在拖曳（給 LightOverlay 畫拖曳回饋用）。</summary>
        public bool Dragging { get; private set; }

        // 拖曳狀態：用「按下當時的滑鼠世界座標 → 現在的滑鼠世界座標」算位移，
        // 而不是直接把燈心貼到滑鼠——否則點到光圈邊緣時燈會瞬移一段，手感很差。
        Vector2 _dragStartMouse;
        Vector2 _dragStartLight;
        bool _dragMoved;   // 真的移動過才推 Undo（純點選不該產生一筆 Undo）

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

        void OnMapChanged(MapData _) { Selected = null; Placing = false; Dragging = false; }

        void Update()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;

            if (_cam == null) _cam = Camera.main;
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ui == null) return;

            if (_ui.CurrentTool != EditTool.Light) { Placing = false; Dragging = false; return; }

            // 舊地圖沒有 lights 欄位 → 反序列化後可能是 null，這裡補起來（不然一按新增就 NRE）。
            if (session.Map.lights == null) session.Map.lights = new System.Collections.Generic.List<LightInstance>();

            // 若當前選取已被刪／換了地圖，清掉
            if (Selected != null && !session.Map.lights.Contains(Selected)) { Selected = null; Placing = false; Dragging = false; }

            if (Input.GetKeyDown(KeyCode.Escape)) { Placing = false; Dragging = false; return; }

            Vector3 mw = _cam.ScreenToWorldPoint(Input.mousePosition); mw.z = 0f;

            // ── 拖曳中：持續更新位置（第一次真的移動時才推 Undo）──
            if (Dragging)
            {
                if (Input.GetMouseButton(0) && Selected != null)
                {
                    Vector2 delta = (Vector2)mw - _dragStartMouse;
                    if (!_dragMoved && delta.sqrMagnitude > 0.0001f)
                    {
                        // 推 Undo 前先把位置還原成拖曳起點，快照才會是「拖之前」的樣子
                        Selected.x = _dragStartLight.x;
                        Selected.y = _dragStartLight.y;
                        UndoManager.Push();
                        _dragMoved = true;
                    }
                    Selected.x = _dragStartLight.x + delta.x;
                    Selected.y = _dragStartLight.y + delta.y;
                }
                else
                {
                    Dragging = false;
                    _dragMoved = false;
                }
                return;
            }

            if (!Input.GetMouseButtonDown(0) || _ui.IsPointerOverUI(Input.mousePosition)) return;

            // ── 放置模式：把選取中的光源移到點擊處 ──
            if (Placing && Selected != null)
            {
                UndoManager.Push();
                Selected.x = mw.x;
                Selected.y = mw.y;
                Placing = false;
                return;
            }

            // ── 點在哪盞燈上就選它，並直接進入拖曳（跟拖地上物一樣）──
            var hit = PickNearest(session.Map, mw);
            if (hit != null)
            {
                Selected = hit;
                Dragging = true;
                _dragMoved = false;
                _dragStartMouse = mw;
                _dragStartLight = new Vector2(hit.x, hit.y);
            }
        }

        /// <summary>取離 world 最近、且在「可點選半徑」內的光源；沒有回 null。</summary>
        static LightInstance PickNearest(MapData map, Vector3 world)
        {
            if (map.lights == null) return null;
            float pickR = Mathf.Max(0.4f, map.tileSize * 0.5f);   // 中心把手大小，與 LightOverlay 畫的一致
            LightInstance best = null;
            float bestSq = pickR * pickR;
            foreach (var l in map.lights)
            {
                if (l == null) continue;
                float dx = l.x - world.x, dy = l.y - world.y;
                float sq = dx * dx + dy * dy;
                if (sq <= bestSq) { bestSq = sq; best = l; }
            }
            return best;
        }

        /// <summary>在畫面中心新增一盞光源（預設火把），並選取它。</summary>
        public void NewLight()
        {
            var map = MapSession.Instance?.Map;
            if (map == null) return;
            if (map.lights == null) map.lights = new System.Collections.Generic.List<LightInstance>();
            UndoManager.Push();

            // 生在目前鏡頭看得到的中心，不是地圖中心——大地圖時生在地圖中心會找不到它。
            Vector3 c = _cam != null
                ? new Vector3(_cam.transform.position.x, _cam.transform.position.y, 0f)
                : new Vector3(map.origin.x + map.width * map.tileSize * 0.5f,
                              map.origin.y - map.height * map.tileSize * 0.5f, 0f);

            var light = new LightInstance
            {
                id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                name = "",
                x = c.x, y = c.y,
                radius = Mathf.Max(1f, map.tileSize * 3f),
            };
            map.lights.Add(light);
            Selected = light;
            Placing = true;   // 新增完直接進放置模式，點一下就定位（擺一排火炬時最順）
        }

        /// <summary>複製選取中的光源（含所有參數），新的那盞直接進放置模式。擺一整排同款火炬時用。</summary>
        public void DuplicateSelected()
        {
            var map = MapSession.Instance?.Map;
            if (map == null || Selected == null) return;
            UndoManager.Push();
            var copy = new LightInstance
            {
                id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                name = Selected.name,
                x = Selected.x + map.tileSize,
                y = Selected.y,
                radius = Selected.radius,
                intensity = Selected.intensity,
                color = Selected.color,
                flicker = Selected.flicker,
                flickerSpeed = Selected.flickerSpeed,
                softness = Selected.softness,
            };
            map.lights.Add(copy);
            Selected = copy;
            Placing = true;
        }

        public void DeleteSelected()
        {
            var map = MapSession.Instance?.Map;
            if (map == null || Selected == null || map.lights == null) return;
            UndoManager.Push();
            map.lights.Remove(Selected);
            Selected = null;
            Placing = false;
            Dragging = false;
        }

        public void Select(LightInstance l) { Selected = l; Placing = false; Dragging = false; }
        public void BeginPlace() { if (Selected != null) { Placing = true; Dragging = false; } }
        public void Deselect() { Selected = null; Placing = false; Dragging = false; }
    }
}
