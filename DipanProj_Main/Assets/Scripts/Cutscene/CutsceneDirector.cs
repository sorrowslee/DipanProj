using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;
using Dipan.UI;
using Dipan.Drama;

namespace Dipan.Cutscene
{
    /// <summary>
    /// 劇情演出排程器：讀 <see cref="MapData.cutscene"/>，用協程依序執行步驟（支援 parallelNext 並行），
    /// 串接鎖輸入、A* 走位、對話（沿用 DramaTable）、置中漫畫、運鏡、螢幕特效、旗標、結束交棒。
    /// 由 <see cref="MapManager"/> 在每次載圖完成後呼叫 <see cref="MaybeAutoStart"/>。
    /// 半演出半漫畫的開場設計見 readme（CUTSCENE_DIRECTOR）。
    /// </summary>
    public class CutsceneDirector : MonoBehaviour
    {
        static CutsceneDirector _active;

        /// <summary>目前是否有劇情正在自動演出（給 MapManager 判斷「等劇情演完再點火進場觸發」）。</summary>
        public static bool IsPlaying => _active != null;
        static readonly List<GameObject> _standing = new List<GameObject>();

        Dipan.MapRuntime.Cutscene _cs;
        float _tileSize = 1f;
        readonly Dictionary<string, CutsceneActor> _actors = new Dictionary<string, CutsceneActor>();
        MapCameraController _cam;
        bool _skip;
        GameObject _comicGo;
        GameObject _fadeGo;
        UnityEngine.UI.Image _fadeImg;

        /// <summary>載圖完成後呼叫：此圖有演出且 autoStartOnEnter 就開演。</summary>
        public static void MaybeAutoStart(MapData map, GameObject player)
        {
            if (map == null || map.cutscene == null) return;
            var cs = map.cutscene;
            if (!cs.autoStartOnEnter || cs.steps == null || cs.steps.Count == 0) return;

            if (_active != null) { Destroy(_active.gameObject); _active = null; }   // 換圖收掉上一個
            for (int i = 0; i < _standing.Count; i++) if (_standing[i] != null) Destroy(_standing[i]);
            _standing.Clear();

            var go = new GameObject("[CutsceneDirector]");
            var dir = go.AddComponent<CutsceneDirector>();
            _active = dir;
            dir._cs = cs;
            dir._tileSize = map.tileSize > 0f ? map.tileSize : 1f;
            dir.StartCoroutine(dir.Run());
        }

        void Update()
        {
            // ESC 略過整段演出＝**開發用**：中止剩餘步驟後會直接跳到最後的 end 執行交棒
            // （例如初始森林 2 的 end='fall' 會立刻接墜落→初始洞窟，玩家等於一鍵跳完整段開場）。
            // 正式打包一律不給用，玩家不能跳過劇情；Development Build 仍可用，方便測後段流程。
            // 同慣例見 IntroComicController / IntroFallController 的 AllowSkip。
            if (DevSkip.Allowed && _cs != null && _cs.skippable && !_skip && Input.GetKeyDown(KeyCode.Escape))
                _skip = true;   // 略過：中止剩餘步驟，直接收尾＋交棒
        }

        IEnumerator Run()
        {
            _cam = FindObjectOfType<MapCameraController>();
            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(_cs.lockInput, false);

            BuildActors();

            var steps = _cs.steps;
            var bg = new List<Coroutine>();   // background 步驟：啟動後不擋，最後才等它們收尾
            int i = 0;
            while (i < steps.Count && !_skip)
            {
                // 並行群組：parallelNext 串起的步驟同時開始、全做完才往下；background 步驟則丟背景、主線立刻往下。
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
            foreach (var c in bg) if (c != null) yield return c;   // 等背景動作收尾（例如一家人走完）再結束

            // 交棒：找最後一個 end 步驟的去向（被略過時也照它走）。
            CutsceneStep endStep = null;
            for (int k = steps.Count - 1; k >= 0; k--) if (steps[k].type == "end") { endStep = steps[k]; break; }

            Cleanup(endStep != null);
            if (endStep != null) DoHandoff(endStep.assetId);
            if (_active == this) _active = null;
            Destroy(gameObject);
        }

        void BuildActors()
        {
            foreach (var a in _cs.actors)
            {
                CutsceneActor rt = (a.kind == "player")
                    ? CutsceneActor.Player(a.id, a.facing, 0f)
                    : CutsceneActor.Npc(a.id, a.spriteFolder, new Vector2(a.x, a.y), a.facing, a.scale, a.animFps, 3f, _tileSize, a.flying);
                if (!string.IsNullOrEmpty(a.id)) _actors[a.id] = rt;
                if (!a.spawnAtStart) rt.SetActive(false);
            }
        }

        CutsceneActor Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _actors.TryGetValue(id, out var a);
            return a;
        }

        IEnumerator RunStep(CutsceneStep s)
        {
            if (_skip) yield break;
            switch (s.type)
            {
                case "move": yield return MoveStep(s); break;
                case "face": { var a = Find(s.actorId); if (a != null) a.Face(s.facing); break; }
                case "dialogue": yield return DialogueStep(s); break;
                case "wait": yield return WaitUnscaled(s.seconds); break;
                case "camera": yield return CameraStep(s); break;
                case "cameraFollow": yield return CameraFollowStep(s); break;
                case "comic": yield return ComicStep(s); break;
                case "spawn": { var a = Find(s.actorId); if (a != null) a.SetActive(true); break; }
                case "despawn": { var a = Find(s.actorId); if (a != null) a.SetActive(false); break; }
                case "fade": yield return FadeStep(s); break;
                case "screenFx": yield return ScreenFxStep(s); break;
                case "setFlag": if (!string.IsNullOrEmpty(s.flag)) TriggerChain.SetFlag(s.flag); break;
                case "end": break;   // 在主迴圈結束後統一處理
            }
        }

        IEnumerator MoveStep(CutsceneStep s)
        {
            var a = Find(s.actorId);
            if (a == null || !s.hasPos || a.tr == null) yield break;
            a.EnsureVisible();   // 移動一個還沒現身的演員 → 讓它現身（否則隱藏剛體不會動、卡守門時間）
            Vector2 target = new Vector2(s.x, s.y);
            a.SetMoveSpeed(s.speed > 0f ? s.speed : 2f);
            float guard = 0f, stall = 0f, best = float.MaxValue;
            while (!a.Reached(target, 0.3f) && !_skip && guard < 20f)
            {
                a.TickMove(target);
                float dist = ((Vector2)a.tr.position - target).magnitude;
                if (dist < best - 0.02f) { best = dist; stall = 0f; }   // 有進步就重置
                else stall += Time.deltaTime;
                // 已很近卻停止進步（A* 到不了精確點、在終點鬼打牆）→ 視為抵達，收乾淨。
                // 用 Time.deltaTime（受暫停影響）：對話暫停時計時凍結，不會誤把「離目標近」的背景 NPC 判定成已抵達而停住。
                if (stall > 0.6f && dist < 1.0f) break;
                guard += Time.deltaTime;
                yield return null;
            }
            a.StopMove();
            if (!string.IsNullOrEmpty(s.facing)) a.Face(s.facing);
        }

        IEnumerator DialogueStep(CutsceneStep s)
        {
            var data = DramaDatabase.Instance != null ? DramaDatabase.Instance.Get(s.dramaId) : null;
            if (data != null && data.Type == 2) DramaTalkController.Play(data.TalkGroup);
            else DramaPanel.Show(s.dramaId);

            float t = 0f; bool opened = false;
            while (t < 0.5f) { if (DramaOpen()) { opened = true; break; } t += Time.unscaledDeltaTime; yield return null; }
            if (opened) while (DramaOpen() && !_skip) yield return null;
        }

        static bool DramaOpen()
            => UIManager.Instance != null && (UIManager.Instance.IsOpen<DramaPanel>() || UIManager.Instance.IsOpen<TalkPanel>());

        IEnumerator CameraStep(CutsceneStep s)
        {
            if (_cam == null) _cam = FindObjectOfType<MapCameraController>();
            if (_cam != null)
            {
                if (s.hasPos) _cam.SetFocusPoint(new Vector2(s.x, s.y));
                if (Mathf.Abs(s.zoom - 1f) > 0.001f) _cam.SetCameraZone(s.zoom, Vector2.zero);
                else _cam.ClearCameraZone();
            }
            yield return WaitUnscaled(s.seconds > 0f ? s.seconds : 1f);
        }

        IEnumerator CameraFollowStep(CutsceneStep s)
        {
            if (_cam == null) _cam = FindObjectOfType<MapCameraController>();
            if (_cam != null) { _cam.SetFocusPoint(null); _cam.ClearCameraZone(); }   // 放回跟隨玩家
            yield return WaitUnscaled(s.seconds > 0f ? s.seconds : 0.5f);
        }

        IEnumerator ComicStep(CutsceneStep s)
        {
            foreach (var kv in _actors) kv.Value.StopMove();   // 期間演員暫停
            ShowComic(s.assetId);
            yield return WaitUnscaled(s.seconds > 0f ? s.seconds : 3f);
            HideComic();
        }

        IEnumerator FadeStep(CutsceneStep s)
        {
            bool toBlack = s.assetId != "in";               // 預設淡出到全黑；"in"＝從黑淡回
            float dur = s.seconds > 0f ? s.seconds : 1f;
            EnsureFade();
            float from = _fadeImg != null ? _fadeImg.color.a : (toBlack ? 0f : 1f);
            float to = toBlack ? 1f : 0f;
            float t = 0f;
            while (t < dur && !_skip) { t += Time.deltaTime; SetFadeAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / dur))); yield return null; }
            SetFadeAlpha(to);
            if (!toBlack) HideFade();                        // 淡入完成 → 移除黑幕
        }

        void EnsureFade()
        {
            if (_fadeGo != null) return;
            _fadeGo = new GameObject("[CutsceneFade]");
            var canvas = _fadeGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;                        // 在對話框(UILayer.Window=100)之下、遊戲世界/角色之上 → 全黑但對話框(尖叫)仍浮在黑幕上可見
            _fadeGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            _fadeImg = _fadeGo.AddComponent<UnityEngine.UI.Image>();
            _fadeImg.raycastTarget = false;
            _fadeImg.color = new Color(0f, 0f, 0f, 0f);
            var rt = _fadeImg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        void SetFadeAlpha(float a) { if (_fadeImg != null) _fadeImg.color = new Color(0f, 0f, 0f, a); }
        void HideFade() { if (_fadeGo != null) { Destroy(_fadeGo); _fadeGo = null; _fadeImg = null; } }

        IEnumerator ScreenFxStep(CutsceneStep s)
        {
            int id = 0; int.TryParse(s.assetId, out id);
            bool done = false;
            ScreenFxPlayer.Play(id, () => done = true, s.seconds > 0f ? s.seconds : -1f);
            // 螢幕特效是「相機後處理」（在所有 UI 之下），劇情黑幕(UI 疊層)會蓋住它 → 開始這一步就把黑幕即時移除，
            // 特效才看得到。同一幀移除、特效已啟動，不會閃出清晰場景（馬賽克從暗色粗格起、與黑幕銜接）。
            HideFade();
            float guard = 0f;
            while (!done && !_skip && guard < 30f) { guard += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator WaitUnscaled(float seconds)
        {
            if (seconds <= 0f) { yield return null; yield break; }
            float t = 0f;
            while (t < seconds && !_skip) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // ---- 置中漫畫（輕量全螢幕 canvas；黑底＋置中圖，停留 N 秒）----
        void ShowComic(string pathId)
        {
            HideComic();
            Sprite sprite = string.IsNullOrEmpty(pathId) ? null : Resources.Load<Sprite>(pathId);

            _comicGo = new GameObject("[CutsceneComic]");
            var canvas = _comicGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            _comicGo.AddComponent<UnityEngine.UI.CanvasScaler>();

            if (sprite != null)
            {
                // 置中、保持比例、縮到約螢幕 90%×80% 內；不加黑底、不拉全螢幕（場景照樣看得到）。
                var img = new GameObject("img");
                img.transform.SetParent(_comicGo.transform, false);
                var im = img.AddComponent<UnityEngine.UI.Image>();
                im.sprite = sprite;
                im.preserveAspect = true;
                var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Screen.width * 0.9f, Screen.height * 0.8f);
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                Debug.LogWarning($"[Cutscene] 漫畫圖找不到（Resources.Load 失敗）：'{pathId}'。" +
                    "圖要放在某個 Resources 資料夾下、filePath 不含副檔名。");
            }
        }

        void HideComic()
        {
            if (_comicGo != null) { Destroy(_comicGo); _comicGo = null; }
        }

        void Cleanup(bool destroyNpcs)
        {
            HideComic();
            HideFade();
            foreach (var kv in _actors)
            {
                var a = kv.Value;
                if (a.isPlayer) { a.Cleanup(); continue; }   // 一定還原玩家控制
                if (destroyNpcs) { a.Cleanup(); continue; }  // 有 end 交棒換圖 → 銷毀
                a.StopMove();                                // 同圖結束、沒交棒 → 留在原地站著(idle)
                if (a.go != null) _standing.Add(a.go);       // 記著，下次開演前清掉，避免重進時堆疊
            }
            _actors.Clear();
            if (_cam == null) _cam = FindObjectOfType<MapCameraController>();
            if (_cam != null) { _cam.SetFocusPoint(null); _cam.ClearCameraZone(); }
            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(false, false);
        }

        // 結束交棒：assetId 可為 mapId 數字 / "map:12" → MapManager.GoToMap；"scene:名稱" → 載場景。
        // "fall"（接墜落動畫）需依你的場景結構接線，見教學。
        void DoHandoff(string target)
        {
            if (string.IsNullOrEmpty(target)) return;
            if (target == "fall")
            {
                // 劇情尾段接墜落：載入 Intro 場景只播全螢幕頁（Story_13~15）→ 側/正墜落 → 卍字 → 回 MainScene 起關到初始洞窟（11 睜眼）。
                // 切場景前先用跨場景常駐黑幕(ScreenFader,30000)壓黑並自動淡出：撐過場景載入(hold) → 墜落漫畫背景就位後淡出(fade)。
                // 自清式：協程跑在常駐的 ScreenFader 上、會延續到 Intro 場景，不靠 IntroComic 去清 → 一路黑到漫畫、無亮閃、也不會卡在全黑。
                Dipan.Flow.ScreenFader.Ensure().BlackThenFadeOut(0.5f, 0.4f);
                Dipan.Intro.IntroComicController.FallTailOnly = true;
                MapManager.SuppressAutoStart = false;
                MapManager.BootStartMapId = Dipan.Save.SaveConstants.PostFallMapId;   // 墜落尾段結束回 MainScene 後起關到初始洞窟
                UnityEngine.SceneManagement.SceneManager.LoadScene(Dipan.Save.SaveConstants.IntroSceneName);
                return;
            }
            if (target.StartsWith("scene:"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(target.Substring(6));
                return;
            }
            string t = target.StartsWith("map:") ? target.Substring(4) : target;
            if (int.TryParse(t, out int mapId))
            {
                if (MapManager.Instance != null) MapManager.Instance.GoToMap(mapId, null);
                return;
            }
            Debug.LogWarning($"[Cutscene] end 去向無法解析：'{target}'（用 mapId 數字、'map:12' 或 'scene:Intro'）。'fall' 請見教學接線。");
        }
    }
}
