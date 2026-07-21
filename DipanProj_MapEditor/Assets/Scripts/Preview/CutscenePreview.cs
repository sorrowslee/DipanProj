using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 編輯器內劇情預覽：spawn 演員 sprite，用移植的 A*（NavGridPreview）＋速度走位、播 idle/walk、轉向，
    /// 依步驟清單跑（move/face/wait/spawn/despawn 忠實；dialogue/comic/screenFx/camera 用「等秒數＋提示」佔位）。
    /// 走位路徑、速度、落點、尺寸盡量貼近遊戲。掛在相機物件上（EditorBootstrap 加）。Esc 略過、跑完自動清除。
    /// </summary>
    public class CutscenePreview : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        string _status = "";
        NavGridPreview _nav;
        Coroutine _co;
        Transform _root;
        readonly List<PActor> _all = new List<PActor>();
        readonly Dictionary<string, PActor> _byId = new Dictionary<string, PActor>();
        bool _skip;
        GameObject _comicGo;
        GameObject _fadeGo;
        UnityEngine.UI.Image _fadeImg;

        public void Play(Cutscene cs, MapData map)
        {
            Stop();
            PreviewSpriteLoader.Clear();   // 每次預覽重量重載，避免用到舊圖/舊尺寸（此專案 Domain Reload 關著，靜態快取會殘留）
            if (cs == null || map == null || cs.steps == null || cs.steps.Count == 0) { _status = "沒有可預覽的步驟"; return; }
            _nav = new NavGridPreview(); _nav.Build(map);
            _root = new GameObject("[CutscenePreviewRoot]").transform;
            _skip = false; IsPlaying = true;
            BuildActors(cs, map);
            _co = StartCoroutine(Run(cs));
        }

        public void Stop()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            HideComic();
            HideFade();
            foreach (var a in _all) if (a.go != null) Destroy(a.go);
            _all.Clear(); _byId.Clear();
            if (_root != null) { Destroy(_root.gameObject); _root = null; }
            IsPlaying = false; _status = "";
        }

        void Update()
        {
            if (!IsPlaying) return;
            if (Input.GetKeyDown(KeyCode.Escape)) _skip = true;
            float dt = Time.deltaTime;
            for (int i = 0; i < _all.Count; i++) _all[i].TickAnim(dt);
        }

        void OnGUI()
        {
            if (!IsPlaying) return;
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            st.normal.textColor = Color.yellow;
            GUI.Label(new Rect(0, Screen.height - 52, Screen.width, 28),
                (string.IsNullOrEmpty(_status) ? "▶ 預覽中…" : "▶ " + _status) + "　（Esc 略過）", st);
        }

        void BuildActors(Cutscene cs, MapData map)
        {
            foreach (var a in cs.actors)
            {
                var pa = PActor.Create(a, map, _root);
                _all.Add(pa);
                if (!string.IsNullOrEmpty(a.id)) _byId[a.id] = pa;
                if (!a.spawnAtStart && pa.go != null) pa.go.SetActive(false);
            }
        }
        PActor Find(string id) { if (string.IsNullOrEmpty(id)) return null; _byId.TryGetValue(id, out var a); return a; }

        IEnumerator Run(Cutscene cs)
        {
            var steps = cs.steps;
            var bg = new List<Coroutine>();
            int i = 0;
            while (i < steps.Count && !_skip)
            {
                var group = new List<Coroutine>();
                while (true)
                {
                    var s = steps[i];
                    var co = StartCoroutine(RunStep(s));
                    i++;
                    if (s.background) { if (co != null) bg.Add(co); if (i >= steps.Count) break; continue; }
                    group.Add(co);
                    if (!s.parallelNext || i >= steps.Count) break;
                }
                foreach (var c in group) yield return c;
            }
            foreach (var c in bg) if (c != null && !_skip) yield return c;
            if (_skip) { Stop(); yield break; }   // Esc 略過 → 立即清除還原
            _status = "演出結束（按 ■ 停止 清除）";   // 演完不自動清：演員停在原地 idle（同遊戲），由使用者按停止還原
        }

        IEnumerator RunStep(CutsceneStep s)
        {
            if (_skip) yield break;
            switch (s.type)
            {
                case "move": yield return MoveStep(s); break;
                case "face": { var a = Find(s.actorId); if (a != null) a.Face(s.facing); break; }
                case "wait": _status = "等待"; yield return Wait(s.seconds > 0f ? s.seconds : 1f); break;
                case "spawn": { var a = Find(s.actorId); if (a != null && a.go != null) a.go.SetActive(true); break; }
                case "despawn": { var a = Find(s.actorId); if (a != null && a.go != null) a.go.SetActive(false); break; }
                case "dialogue": _status = $"對話 #{s.dramaId}（遊戲內播放）"; yield return Wait(1.5f); break;
                case "comic": yield return ComicStep(s); break;
                case "fade": yield return FadeStep(s); break;
                case "screenFx": _status = $"螢幕特效 {s.assetId}（遊戲內播放）"; yield return Wait(s.seconds > 0f ? s.seconds : 1f); break;
                case "camera": _status = "運鏡（預覽不動鏡頭）"; yield return Wait(s.seconds > 0f ? s.seconds : 1f); break;
                case "cameraFollow": _status = "鏡頭跟隨（預覽不動鏡頭）"; yield return Wait(s.seconds > 0f ? s.seconds : 0.5f); break;
                case "setFlag": break;
                case "end": _status = "結束交棒（預覽不換圖）"; break;
            }
        }

        IEnumerator MoveStep(CutsceneStep s)
        {
            var a = Find(s.actorId);
            if (a == null || !s.hasPos || a.tr == null) yield break;
            if (a.go != null && !a.go.activeSelf) a.go.SetActive(true);   // 移動還沒現身的演員 → 自動現身
            _status = "走位";
            Vector2 target = new Vector2(s.x, s.y);
            float speed = s.speed > 0f ? s.speed : 2f;
            float guard = 0f, stall = 0f, best = float.MaxValue;
            while (!a.Reached(target, 0.3f) && !_skip && guard < 20f)
            {
                a.MoveTick(target, speed, Time.deltaTime, _nav);
                float dist = ((Vector2)a.tr.position - target).magnitude;
                if (dist < best - 0.02f) { best = dist; stall = 0f; } else stall += Time.deltaTime;
                if (stall > 0.6f && dist < 1.0f) break;
                guard += Time.deltaTime;
                yield return null;
            }
            a.StopMove();
            if (!string.IsNullOrEmpty(s.facing)) a.Face(s.facing);
        }

        IEnumerator Wait(float sec) { float t = 0f; while (t < sec && !_skip) { t += Time.deltaTime; yield return null; } }

        IEnumerator FadeStep(CutsceneStep s)
        {
            bool toBlack = s.assetId != "in";
            float dur = s.seconds > 0f ? s.seconds : 1f;
            _status = toBlack ? "淡出全黑" : "淡入";
            EnsureFade();
            float from = _fadeImg != null ? _fadeImg.color.a : (toBlack ? 0f : 1f);
            float to = toBlack ? 1f : 0f;
            float t = 0f;
            while (t < dur && !_skip) { t += Time.deltaTime; SetFadeAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / dur))); yield return null; }
            SetFadeAlpha(to);
            if (!toBlack) HideFade();
        }

        void EnsureFade()
        {
            if (_fadeGo != null) return;
            _fadeGo = new GameObject("[PreviewFade]");
            var canvas = _fadeGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5500;   // 在預覽漫畫(6000)之下、場景之上
            _fadeGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            _fadeImg = _fadeGo.AddComponent<UnityEngine.UI.Image>();
            _fadeImg.raycastTarget = false;
            _fadeImg.color = new Color(0f, 0f, 0f, 0f);
            var rt = _fadeImg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        void SetFadeAlpha(float a) { if (_fadeImg != null) _fadeImg.color = new Color(0f, 0f, 0f, a); }
        void HideFade() { if (_fadeGo != null) { Destroy(_fadeGo); _fadeGo = null; _fadeImg = null; } }

        IEnumerator ComicStep(CutsceneStep s)
        {
            for (int i = 0; i < _all.Count; i++) _all[i].StopMove();   // 期間演員暫停
            _status = $"漫畫 {s.assetId}";
            ShowComic(s.assetId);
            yield return Wait(s.seconds > 0f ? s.seconds : 3f);
            HideComic();
        }

        void ShowComic(string pathId)
        {
            HideComic();
            var sprite = LoadComic(pathId);
            _comicGo = new GameObject("[PreviewComic]");
            var canvas = _comicGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            _comicGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            if (sprite != null)
            {
                var img = new GameObject("img");
                img.transform.SetParent(_comicGo.transform, false);
                var im = img.AddComponent<UnityEngine.UI.Image>();
                im.sprite = sprite; im.preserveAspect = true;   // 置中、保持比例、約螢幕 90%×80%、不加黑底
                var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Screen.width * 0.9f, Screen.height * 0.8f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        void HideComic() { if (_comicGo != null) { Destroy(_comicGo); _comicGo = null; } }

        // 從主專案 Resources 讀漫畫圖（預覽在編輯器專案，沒有遊戲 Resources，直接 File IO 讀）。
        static Sprite LoadComic(string pathId)
        {
            if (string.IsNullOrEmpty(pathId)) return null;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string repoRoot = Directory.GetParent(projectRoot).FullName;
                string path = Path.Combine(repoRoot, "DipanProj_Main", "Assets", "Resources",
                                           pathId.Replace('/', Path.DirectorySeparatorChar) + ".png");
                if (!File.Exists(path)) { Debug.LogWarning($"[CutscenePreview] 找不到漫畫圖：{path}（要放在主專案 Assets/Resources 下）"); return null; }
                var tex = new Texture2D(2, 2); tex.LoadImage(File.ReadAllBytes(path));
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch { return null; }
        }

        // ---- 預覽演員 ----
        class PActor
        {
            public GameObject go; public Transform tr; SpriteRenderer sr;
            Sprite[] idle, walk; float animFps; bool sourceFacesRight = true; bool flying;
            int _st; int _frame; float _timer;   // _st: 0=idle 1=walk
            readonly List<Vector2> _path = new List<Vector2>(); int _pathIdx; float _repath;

            public static PActor Create(CutsceneActor a, MapData map, Transform root)
            {
                var pa = new PActor();
                var f = PreviewSpriteLoader.Load(a.spriteFolder, map.module, map.tileSize);
                pa.idle = f != null ? f.idle : null;
                pa.walk = f != null ? f.walk : null;
                pa.animFps = a.animFps > 0f ? a.animFps : 8f;
                pa.flying = a.flying;
                var go = new GameObject("Preview_" + a.id);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(a.x, a.y, 0f);
                if (a.scale > 0f) go.transform.localScale = Vector3.one * a.scale;
                pa.go = go; pa.tr = go.transform;
                pa.sr = go.AddComponent<SpriteRenderer>();
                pa.sr.sortingOrder = 32000;   // 疊在地圖之上；注意 sortingOrder 是 16-bit(-32768~32767)，用超大值會 wrap 成負數躲到背景後面
                pa._st = 0; pa.ApplyFrame(); pa.Face(a.facing);
                return pa;
            }

            public void Face(string dir)
            {
                if (sr == null || string.IsNullOrEmpty(dir)) return;
                bool right = dir == "right";
                sr.flipX = (right != sourceFacesRight);
            }
            public bool Reached(Vector2 target, float tol) => tr != null && ((Vector2)tr.position - target).sqrMagnitude <= tol * tol;
            public void StopMove() { _st = 0; }

            public void MoveTick(Vector2 target, float speed, float dt, NavGridPreview nav)
            {
                Vector2 pos = tr.position;
                Vector2 dir;
                if (!flying && nav != null && nav.Ready && !nav.HasLineOfSight(pos, target))
                {
                    _repath -= dt;
                    if (_path.Count == 0 || _repath <= 0f)
                    {
                        if (nav.TryFindPath(pos, target, _path)) _pathIdx = 0; else _path.Clear();
                        _repath = 0.35f;
                    }
                    if (_path.Count > 0)
                    {
                        while (_pathIdx < _path.Count - 1 && (_path[_pathIdx] - pos).sqrMagnitude < 0.35f * 0.35f) _pathIdx++;
                        Vector2 d = _path[_pathIdx] - pos;
                        dir = d.sqrMagnitude < 1e-6f ? (target - pos).normalized : d.normalized;
                    }
                    else dir = (target - pos).normalized;
                }
                else dir = (target - pos).normalized;

                tr.position = new Vector3(pos.x + dir.x * speed * dt, pos.y + dir.y * speed * dt, tr.position.z);
                _st = 1;
                float ddx = target.x - pos.x;
                if (Mathf.Abs(ddx) > 0.15f) Face(ddx >= 0f ? "right" : "left");
            }

            public void TickAnim(float dt)
            {
                if (sr != null && tr != null)
                    sr.sortingOrder = 30000 + Mathf.RoundToInt(-tr.position.y * 100f);   // 依腳底 Y 排序，避免互蓋閃爍（同遊戲 YSortByFeet 相對順序）
                var frames = _st == 1 ? (walk ?? idle) : (idle ?? walk);
                if (sr == null || frames == null || frames.Length == 0) return;
                if (frames.Length == 1) { if (sr.sprite != frames[0]) sr.sprite = frames[0]; return; }
                float fps = animFps > 0f ? animFps : 8f;
                _timer += dt; float dur = 1f / fps;
                while (_timer >= dur) { _timer -= dur; _frame = (_frame + 1) % frames.Length; }
                sr.sprite = frames[Mathf.Clamp(_frame, 0, frames.Length - 1)];
            }
            void ApplyFrame()
            {
                var frames = _st == 1 ? (walk ?? idle) : (idle ?? walk);
                if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[0];
            }
        }
    }
}
