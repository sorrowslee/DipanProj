using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.UI;

namespace DipanMapEditor.Tools
{
    /// <summary>
    /// 劇情工具：管理此圖的 Cutscene（演員 + 步驟），並在畫布點放「演員起點 / 步驟目標座標」。
    /// 參數（型別/台詞id/秒數…）由 EditorUI 的面板編輯；座標由本元件點放（仿 SceneFxController）。
    /// 視覺（演員標記、走位折線）由 <see cref="Core.CutsceneOverlay"/> 畫。
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        public enum PlaceMode { None, ActorPos, StepPos }

        public CutsceneActor SelectedActor { get; private set; }
        public CutsceneStep SelectedStep { get; private set; }
        public PlaceMode Mode { get; private set; } = PlaceMode.None;

        Camera _cam;
        EditorUI _ui;

        void Start() { _cam = Camera.main; _ui = FindObjectOfType<EditorUI>(); }

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
        void OnMapChanged(MapData _) { SelectedActor = null; SelectedStep = null; Mode = PlaceMode.None; }

        void Update()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ui == null) return;
            if (_ui.CurrentTool != EditTool.Cutscene) { Mode = PlaceMode.None; return; }

            var cs = session.Map.cutscene;
            if (cs == null) { SelectedActor = null; SelectedStep = null; Mode = PlaceMode.None; }
            else
            {
                if (SelectedActor != null && !cs.actors.Contains(SelectedActor)) SelectedActor = null;
                if (SelectedStep != null && !cs.steps.Contains(SelectedStep)) SelectedStep = null;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) { Mode = PlaceMode.None; return; }
            if (!Input.GetMouseButtonDown(0) || _ui.IsPointerOverUI(Input.mousePosition)) return;

            Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition); w.z = 0;

            if (Mode == PlaceMode.ActorPos && SelectedActor != null)
            {
                UndoManager.Push();
                SelectedActor.x = w.x; SelectedActor.y = w.y;
                Mode = PlaceMode.None; return;
            }
            if (Mode == PlaceMode.StepPos && SelectedStep != null)
            {
                UndoManager.Push();
                SelectedStep.x = w.x; SelectedStep.y = w.y; SelectedStep.hasPos = true;
                Mode = PlaceMode.None; return;
            }

            PickNearest(w, cs);
        }

        void PickNearest(Vector3 world, Cutscene cs)
        {
            if (cs == null) return;
            float best = 1.2f;
            var v = new Vector2(world.x, world.y);
            foreach (var a in cs.actors)
            {
                float d = Vector2.Distance(v, new Vector2(a.x, a.y));
                if (d < best) { best = d; SelectedActor = a; SelectedStep = null; }
            }
            foreach (var s in cs.steps)
            {
                if (!s.hasPos) continue;
                float d = Vector2.Distance(v, new Vector2(s.x, s.y));
                if (d < best) { best = d; SelectedStep = s; SelectedActor = null; }
            }
        }

        // ---- 供 EditorUI 面板呼叫 ----

        public Cutscene EnsureCutscene()
        {
            var map = MapSession.Instance?.Map;
            if (map == null) return null;
            if (map.cutscene == null) { UndoManager.Push(); map.cutscene = new Cutscene(); }
            return map.cutscene;
        }

        public void RemoveCutscene()
        {
            var map = MapSession.Instance?.Map;
            if (map == null || map.cutscene == null) return;
            UndoManager.Push();
            map.cutscene = null; SelectedActor = null; SelectedStep = null; Mode = PlaceMode.None;
        }

        public void NewActor()
        {
            var cs = EnsureCutscene(); if (cs == null) return;
            var map = MapSession.Instance.Map;
            UndoManager.Push();
            var a = new CutsceneActor
            {
                id = "actor" + (cs.actors.Count + 1),
                kind = "npc",
                x = map.origin.x + map.width * map.tileSize * 0.5f,
                y = map.origin.y - map.height * map.tileSize * 0.5f,
                facing = "left",
            };
            cs.actors.Add(a); SelectedActor = a; SelectedStep = null; Mode = PlaceMode.None;
        }

        public void DeleteActor(CutsceneActor a)
        {
            var cs = MapSession.Instance?.Map?.cutscene; if (cs == null || a == null) return;
            UndoManager.Push(); cs.actors.Remove(a);
            if (SelectedActor == a) SelectedActor = null; Mode = PlaceMode.None;
        }

        public void SelectActor(CutsceneActor a) { SelectedActor = a; SelectedStep = null; Mode = PlaceMode.None; }
        public void BeginPlaceActor() { if (SelectedActor != null) Mode = PlaceMode.ActorPos; }

        public void NewStep()
        {
            var cs = EnsureCutscene(); if (cs == null) return;
            UndoManager.Push();
            var s = new CutsceneStep { type = "wait", seconds = 1f };
            cs.steps.Add(s); SelectedStep = s; SelectedActor = null; Mode = PlaceMode.None;
        }

        public void DeleteStep(CutsceneStep s)
        {
            var cs = MapSession.Instance?.Map?.cutscene; if (cs == null || s == null) return;
            UndoManager.Push(); cs.steps.Remove(s);
            if (SelectedStep == s) SelectedStep = null; Mode = PlaceMode.None;
        }

        public void SelectStep(CutsceneStep s) { SelectedStep = s; SelectedActor = null; Mode = PlaceMode.None; }
        public void BeginPlaceStepPos() { if (SelectedStep != null) Mode = PlaceMode.StepPos; }

        public void MoveStep(CutsceneStep s, int dir)
        {
            var cs = MapSession.Instance?.Map?.cutscene; if (cs == null || s == null) return;
            int i = cs.steps.IndexOf(s); int j = i + dir;
            if (i < 0 || j < 0 || j >= cs.steps.Count) return;
            UndoManager.Push();
            cs.steps[i] = cs.steps[j]; cs.steps[j] = s;
        }
    }
}
