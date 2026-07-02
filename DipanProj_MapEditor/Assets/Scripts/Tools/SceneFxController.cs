using System.Collections.Generic;
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

        // 編輯器內即時預覽：每個開啟預覽的特效對應一個實際運行的特效物件（與遊戲端同一套 SceneFxEmitter/PortalFx）。
        readonly Dictionary<SceneFxInstance, GameObject> _previews = new Dictionary<SceneFxInstance, GameObject>();
        readonly Dictionary<SceneFxInstance, string> _sigs = new Dictionary<SceneFxInstance, string>();
        readonly List<SceneFxInstance> _tmp = new List<SceneFxInstance>();

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

        void OnMapChanged(MapData _) { Selected = null; Mode = PlaceMode.None; ClearAllPreviews(); }

        void Update()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;

            // 預覽維護（不論當前是哪個工具都要跑）：清掉被刪的、參數變了就重建 → 編輯器內即時反映。
            MaintainPreviews(session.Map);

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
            DestroyPreview(Selected);        // 刪特效 → 正在跑的預覽一併移除
            map.sceneFx.Remove(Selected);
            Selected = null;
            Mode = PlaceMode.None;
        }

        public void Select(SceneFxInstance fx) { Selected = fx; Mode = PlaceMode.None; }
        public void BeginPlaceStart() { if (Selected != null) Mode = PlaceMode.Start; }
        public void BeginPlaceEnd() { if (Selected != null) Mode = PlaceMode.End; }
        public void Deselect() { Selected = null; Mode = PlaceMode.None; }

        // ---- 即時預覽（與遊戲端同一套 SceneFxEmitter / PortalFx / SceneFxTable，複製到編輯器專案）----

        public bool IsPreviewing(SceneFxInstance fx) => fx != null && _previews.ContainsKey(fx);

        /// <summary>切換某特效的即時預覽（顯示/隱藏）。</summary>
        public void TogglePreview(SceneFxInstance fx)
        {
            if (fx == null) return;
            if (_previews.ContainsKey(fx)) DestroyPreview(fx);
            else RebuildPreview(fx);
        }

        void MaintainPreviews(MapData map)
        {
            if (_previews.Count == 0) return;

            // 移除已被刪除的特效之預覽
            _tmp.Clear();
            foreach (var kv in _previews) if (!map.sceneFx.Contains(kv.Key)) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++) DestroyPreview(_tmp[i]);

            // 參數/起終點變了就重建（移動點或改欄位即時反映）
            _tmp.Clear();
            foreach (var kv in _previews) _tmp.Add(kv.Key);
            for (int i = 0; i < _tmp.Count; i++)
            {
                var fx = _tmp[i];
                string sig = Sig(fx);
                if (_sigs.TryGetValue(fx, out var old) && old == sig) continue;
                RebuildPreview(fx);
            }
        }

        void RebuildPreview(SceneFxInstance fx)
        {
            DestroyPreview(fx);
            var go = BuildPreviewGO(fx);
            _previews[fx] = go;
            _sigs[fx] = Sig(fx);
        }

        void DestroyPreview(SceneFxInstance fx)
        {
            if (_previews.TryGetValue(fx, out var go))
            {
                if (go != null) Destroy(go);
                _previews.Remove(fx);
            }
            _sigs.Remove(fx);
        }

        void ClearAllPreviews()
        {
            foreach (var kv in _previews) if (kv.Value != null) Destroy(kv.Value);
            _previews.Clear();
            _sigs.Clear();
        }

        // 與遊戲端 MapLoader.BuildSceneFx 完全相同的建構邏輯（stream 弧線 / portal 矩形）。
        GameObject BuildPreviewGO(SceneFxInstance fx)
        {
            var look = SceneFxTable.Get(fx.fxId);
            Vector3 start = new Vector3(fx.startX, fx.startY, 0f);
            Vector3 end = fx.hasEnd
                ? new Vector3(fx.endX, fx.endY, 0f)
                : start + new Vector3(0f, 2.5f * Mathf.Max(0.2f, fx.h), 0f);

            var go = new GameObject("FxPreview");
            go.transform.SetParent(transform, false);
            go.transform.position = start;

            if (look.kind == 1)
            {
                go.AddComponent<PortalFx>().Configure(look, start, end);
            }
            else
            {
                Vector3 dir = end - start;
                float len = dir.magnitude;
                Vector3 perp = (len > 1e-4f) ? new Vector3(-dir.y, dir.x, 0f).normalized : Vector3.right;
                Vector3 ctrl = (start + end) * 0.5f + perp * fx.bulge;
                go.AddComponent<SceneFxEmitter>().Configure(
                    look, start, ctrl, end, fx.w, fx.h, fx.loop, fx.intermittent, fx.interval);
            }
            return go;
        }

        static string Sig(SceneFxInstance f)
            => $"{f.fxId}|{f.startX}|{f.startY}|{f.hasEnd}|{f.endX}|{f.endY}|{f.bulge}|{f.w}|{f.h}|{f.loop}|{f.intermittent}|{f.interval}";
    }
}
