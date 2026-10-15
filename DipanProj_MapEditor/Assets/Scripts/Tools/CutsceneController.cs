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
    ///
    /// 多段（2026-10-15，readme/bloodfang/GAP.md §8）：一張圖可以有多段演出（`MapData.cutscenes` 清單），
    /// 面板、畫布點放、疊層、預覽一律操作「目前選取的那段」<see cref="Current"/>。
    /// 遊戲端用 trigger「播放劇情(鏈動作)」的 cutsceneId 對應各段的 id；**第 0 段**另外是 autoStart 唯一會看的段、
    /// 也是 cutsceneId 留空時播的段。
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        public enum PlaceMode { None, ActorPos, StepPos }

        public CutsceneActor SelectedActor { get; private set; }
        public CutsceneStep SelectedStep { get; private set; }
        public PlaceMode Mode { get; private set; } = PlaceMode.None;

        Camera _cam;
        EditorUI _ui;

        /// <summary>目前選取第幾段（static：疊層 CutsceneOverlay 也要讀）。越界時由 <see cref="CurrentOf"/> 夾回範圍。</summary>
        public static int SelectedIndex { get; private set; }

        /// <summary>這張圖「目前選取的那段」；沒有任何段＝null。</summary>
        public static Cutscene CurrentOf(MapData map)
        {
            if (map == null || map.cutscenes == null || map.cutscenes.Count == 0) return null;
            if (SelectedIndex < 0 || SelectedIndex >= map.cutscenes.Count)
                SelectedIndex = Mathf.Clamp(SelectedIndex, 0, map.cutscenes.Count - 1);
            return map.cutscenes[SelectedIndex];
        }

        public Cutscene Current => CurrentOf(MapSession.Instance?.Map);

        void Start() { _cam = Camera.main; _ui = FindObjectOfType<EditorUI>(); }

        void OnEnable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged += OnMapChanged;
                MapSession.Instance.OnMapRebuilt += OnMapRebuilt;
            }
        }
        void OnDisable()
        {
            if (MapSession.Instance != null)
            {
                MapSession.Instance.OnMapChanged -= OnMapChanged;
                MapSession.Instance.OnMapRebuilt -= OnMapRebuilt;
            }
        }
        // 換圖（新建／開檔）＝回到第 0 段；Undo 還原（Rebuilt）＝留在原本那段（CurrentOf 會夾回範圍），
        // 不然作者在編第 2 段時按一下 Undo 就被丟回第 0 段。
        void OnMapChanged(MapData _) { SelectedIndex = 0; SelectedActor = null; SelectedStep = null; Mode = PlaceMode.None; }
        void OnMapRebuilt(MapData _) { SelectedActor = null; SelectedStep = null; Mode = PlaceMode.None; }

        void Update()
        {
            var session = MapSession.Instance;
            if (session == null || session.Map == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ui == null) return;
            if (_ui.CurrentTool != EditTool.Cutscene) { Mode = PlaceMode.None; return; }

            var cs = CurrentOf(session.Map);
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

        /// <summary>沒有任何段時建第 0 段；回傳目前那段。</summary>
        public Cutscene EnsureCutscene()
        {
            var map = MapSession.Instance?.Map;
            if (map == null) return null;
            if (map.cutscenes == null || map.cutscenes.Count == 0)
            {
                UndoManager.Push();
                if (map.cutscenes == null) map.cutscenes = new System.Collections.Generic.List<Cutscene>();
                map.cutscenes.Add(new Cutscene());
                SelectedIndex = 0;
            }
            return Current;
        }

        /// <summary>
        /// **只刪目前這段**（2026-10-15 前是 cutscenes.Clear()，多段時會整份清掉）。刪完選前一段。
        /// </summary>
        public void RemoveCutscene()
        {
            var map = MapSession.Instance?.Map;
            var cur = CurrentOf(map);
            if (cur == null) return;
            UndoManager.Push();
            map.cutscenes.RemoveAt(SelectedIndex);
            SelectedIndex = Mathf.Max(0, SelectedIndex - 1);
            SelectedActor = null; SelectedStep = null; Mode = PlaceMode.None;
        }

        /// <summary>切到第 i 段。</summary>
        public void SelectCutscene(int i)
        {
            var map = MapSession.Instance?.Map;
            if (map?.cutscenes == null || i < 0 || i >= map.cutscenes.Count) return;
            SelectedIndex = i;
            SelectedActor = null; SelectedStep = null; Mode = PlaceMode.None;
        }

        /// <summary>
        /// 新增一段空演出並選它。第 1 段以後預設**不**自動播（遊戲端 autoStart 只看第 0 段，勾了也沒用，
        /// 預設關掉免得作者以為它會自己播），並給一個不重複的 id（playCutscene 的 cutsceneId 靠它對應）。
        /// </summary>
        public void AddCutscene()
        {
            var map = MapSession.Instance?.Map;
            if (map == null) return;
            UndoManager.Push();
            if (map.cutscenes == null) map.cutscenes = new System.Collections.Generic.List<Cutscene>();
            var cs = new Cutscene();
            if (map.cutscenes.Count > 0) { cs.autoStartOnEnter = false; cs.id = UniqueId(map, "scene" + (map.cutscenes.Count + 1)); }
            map.cutscenes.Add(cs);
            SelectedIndex = map.cutscenes.Count - 1;
            SelectedActor = null; SelectedStep = null; Mode = PlaceMode.None;
        }

        /// <summary>
        /// 複製目前這段（演員＋步驟深拷貝）並選新的那段；id 加 `_copy`。
        /// 同一張圖的幾段引路演出長得幾乎一樣，複製後只改演員外觀與台詞最快。
        /// </summary>
        public void DuplicateCurrent()
        {
            var map = MapSession.Instance?.Map;
            var cur = CurrentOf(map);
            if (cur == null) return;
            UndoManager.Push();
            var copy = IO.JsonConfig.Deserialize<Cutscene>(IO.JsonConfig.Serialize(cur));   // 同存檔用的序列化設定，欄位一個不漏
            copy.autoStartOnEnter = false;
            copy.id = UniqueId(map, (string.IsNullOrWhiteSpace(cur.id) ? "scene" : cur.id.Trim()) + "_copy");
            map.cutscenes.Insert(SelectedIndex + 1, copy);
            SelectedIndex = SelectedIndex + 1;
            SelectedActor = null; SelectedStep = null; Mode = PlaceMode.None;
        }

        /// <summary>同圖段落 id 是否重複（忽略大小寫與前後空白，同遊戲端 FindCutscene 的比對）。空 id 不算重複。</summary>
        public static bool IsDuplicateId(MapData map, Cutscene cs)
        {
            if (map?.cutscenes == null || cs == null || string.IsNullOrWhiteSpace(cs.id)) return false;
            string want = cs.id.Trim();
            foreach (var o in map.cutscenes)
                if (o != null && o != cs && string.Equals(o.id?.Trim(), want, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string UniqueId(MapData map, string baseId)
        {
            string id = baseId; int n = 2;
            while (map.cutscenes.Exists(c => c != null && string.Equals(c.id?.Trim(), id, System.StringComparison.OrdinalIgnoreCase)))
                id = baseId + "_" + (n++);
            return id;
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
            var cs = Current; if (cs == null || a == null) return;
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
            var cs = Current; if (cs == null || s == null) return;
            UndoManager.Push(); cs.steps.Remove(s);
            if (SelectedStep == s) SelectedStep = null; Mode = PlaceMode.None;
        }

        public void SelectStep(CutsceneStep s) { SelectedStep = s; SelectedActor = null; Mode = PlaceMode.None; }
        public void BeginPlaceStepPos() { if (SelectedStep != null) Mode = PlaceMode.StepPos; }

        public void MoveStep(CutsceneStep s, int dir)
        {
            var cs = Current; if (cs == null || s == null) return;
            int i = cs.steps.IndexOf(s); int j = i + dir;
            if (i < 0 || j < 0 || j >= cs.steps.Count) return;
            UndoManager.Push();
            cs.steps[i] = cs.steps[j]; cs.steps[j] = s;
        }
    }
}
