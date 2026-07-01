using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 場景特效工具：新增特效（在地圖中心生一個）、選取、放置起點/終點（點畫布放）、刪除。
    /// 參數（fxId / 長寬 / 弧度 / 循環 / 間歇…）由 EditorUI 的面板編輯；起/終點由本元件在畫布點放。
    /// 視覺（綠起點、紅終點、連線/弧線）由 <see cref="SceneFxOverlay"/> 畫。
    /// </summary>
    public class SceneFxController : MonoBehaviour
    {
        public enum PlaceMode { None, Start, End }

        public SceneFxInstance Selected { get; private set; }
        public PlaceMode Mode { get; private set; } = PlaceMode.None;

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

        void OnMapChanged(MapData _) { Selected = null; Mode = PlaceMode.None; }

        void Update()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ui == null) return;

            if (_ui.CurrentTool != EditTool.SceneFx) { Mode = PlaceMode.None; return; }

            // 若當前選取已被刪/換地圖，清掉
            if (Selected != null && !session.Map.sceneFx.Contains(Selected)) { Selected = null; Mode = PlaceMode.None; }

            if (Input.GetKeyDown(KeyCode.Escape)) { Mode = PlaceMode.None; return; }

            if (!Input.GetMouseButtonDown(0) || _ui.IsPointerOverUI(Input.mousePosition)) return;

            Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition); w.z = 0;

            if (Mode != PlaceMode.None && Selected != null)
            {
                UndoManager.Push();
                if (Mode == PlaceMode.Start) { Selected.startX = w.x; Selected.startY = w.y; }
                else { Selected.endX = w.x; Selected.endY = w.y; Selected.hasEnd = true; }
                Mode = PlaceMode.None;
                return;
            }

            // 非放置模式：點畫布 → 選最近的特效（起點或終點附近）。
            Selected = PickNearest(w, session.Map);
        }

        SceneFxInstance PickNearest(Vector3 world, MapData map)
        {
            SceneFxInstance best = null;
            float bestD = 1.2f;   // 選取半徑（世界單位）
            foreach (var fx in map.sceneFx)
            {
                float ds = Vector2.Distance(new Vector2(world.x, world.y), new Vector2(fx.startX, fx.startY));
                if (ds < bestD) { bestD = ds; best = fx; }
                if (fx.hasEnd)
                {
                    float de = Vector2.Distance(new Vector2(world.x, world.y), new Vector2(fx.endX, fx.endY));
                    if (de < bestD) { bestD = de; best = fx; }
                }
            }
            return best;
        }

        // ---- 供 EditorUI 面板呼叫 ----

        /// <summary>新增一個特效，起點放在地圖中心、終點在其上方一段，並選取它。</summary>
        public void NewFx()
        {
            var map = MapSession.Instance?.Map;
            if (map == null) return;
            UndoManager.Push();
            Vector2 c = new Vector2(map.origin.x + map.width * map.tileSize * 0.5f,
                                    map.origin.y - map.height * map.tileSize * 0.5f);
            var fx = new SceneFxInstance
            {
                id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                fxId = 1,
                startX = c.x, startY = c.y,
                hasEnd = false,
                endX = c.x, endY = c.y + map.tileSize * 3f,
            };
            map.sceneFx.Add(fx);
            Selected = fx;
            Mode = PlaceMode.None;
        }

        public void DeleteSelected()
        {
            var map = MapSession.Instance?.Map;
            if (map == null || Selected == null) return;
            UndoManager.Push();
            map.sceneFx.Remove(Selected);
            Selected = null;
            Mode = PlaceMode.None;
        }

        public void Select(SceneFxInstance fx) { Selected = fx; Mode = PlaceMode.None; }
        public void BeginPlaceStart() { if (Selected != null) Mode = PlaceMode.Start; }
        public void BeginPlaceEnd() { if (Selected != null) Mode = PlaceMode.End; }
        public void Deselect() { Selected = null; Mode = PlaceMode.None; }
    }
}
