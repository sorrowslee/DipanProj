using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;
using Dipan.UI;
using Dipan.Drama;

namespace Dipan.Cutscene
{
    /// <summary>
    /// 劇情演出排程器：讀 <see cref="MapData.cutscenes"/>，用協程依序執行步驟（支援 parallelNext 並行），
    /// 串接鎖輸入、A* 走位、對話（沿用 DramaTable）、頭上對話框、置中漫畫、運鏡、螢幕特效、旗標、結束交棒。
    ///
    /// **兩個啟動入口**：
    ///   ‧ <see cref="MaybeAutoStart"/>：<see cref="MapManager"/> 每次載圖完成後呼叫，autoStartOnEnter=true 才開演。
    ///   ‧ <see cref="PlayById"/>：觸發鏈的 <c>playCutscene</c> 動作呼叫。把 autoStartOnEnter 關掉、
    ///     改用 trigger 啟動，就能沿用觸發鏈整套「條件旗標／重複規則（關卡單次·每次·每周目·永久）」來管
    ///     「這段劇情能不能播、播幾次」——**自動播是沒有一次性機制的，每次進圖都會重播**。
    /// 半演出半漫畫的開場設計見 readme（CUTSCENE_DIRECTOR）。
    /// </summary>
    public class CutsceneDirector : MonoBehaviour
    {
        /// <summary>輸入鎖的具名持有者（見 PROBLEMS D13：共用預設 key 會互相解鎖）。</summary>
        const string HoldOwner = "CutsceneDirector";

        static CutsceneDirector _active;

        /// <summary>目前是否有劇情正在自動演出（給 MapManager 判斷「等劇情演完再點火進場觸發」）。</summary>
        public static bool IsPlaying => _active != null;
        static readonly List<GameObject> _standing = new List<GameObject>();

        Dipan.MapRuntime.Cutscene _cs;
        float _tileSize = 1f;
        readonly Dictionary<string, CutsceneActor> _actors = new Dictionary<string, CutsceneActor>();
        MapCameraController _cam;
        bool _skip;
        System.Action _onFinished;   // 由 playCutscene 帶進來：演完（且沒交棒換圖）才接觸發鏈的 next
        bool _hudWasOpen;            // 開演前底部血量 HUD 是不是開著（hideHud 收尾要還原成原樣，不是無條件打開）
        bool _released;              // 全域狀態（輸入鎖/回憶特效/隱藏主角/HUD）是否已還原，避免重複做
        GameObject _comicGo;
        GameObject _skipGo;          // 右上角 Skip 覆蓋層（skippable 時才建；掛在自己身上，一起被銷毀）
        GameObject _fadeGo;
        UnityEngine.UI.Image _fadeImg;

        /// <summary>載圖完成後呼叫：此圖有演出、autoStartOnEnter、且 requireFlag 條件成立就開演。</summary>
        public static void MaybeAutoStart(MapData map, GameObject player)
        {
            var cs = map?.MainCutscene;
            if (cs == null || !cs.autoStartOnEnter) return;
            StartCutscene(cs, map, null);
        }

        /// <summary>
        /// 觸發鏈 <c>playCutscene</c> 的入口：播這張圖 id 指定的那段演出（id 留空＝第一段）。
        /// 演完（且該段沒有 <c>end</c> 交棒換圖）才呼叫 <paramref name="onDone"/> 讓鏈接 next；
        /// 有交棒換圖時鏈就此結束（同 teleportTo 的慣例）。
        /// 回傳 false＝沒開演（找不到／沒步驟／已有演出在跑），呼叫端應直接接 next 不要卡住。
        /// </summary>
        public static bool PlayById(string id, System.Action onDone)
        {
            if (IsPlaying)
            {
                Debug.LogWarning($"[Cutscene] playCutscene「{id}」：已經有一段演出在跑，忽略這次啟動（鏈照常往下）。");
                return false;
            }
            var map = MapManager.Instance != null && MapManager.Instance.mapLoader != null
                ? MapManager.Instance.mapLoader.Map : null;
            if (map == null) { Debug.LogWarning("[Cutscene] playCutscene：拿不到目前地圖，略過。"); return false; }

            var cs = map.FindCutscene(id);
            if (cs == null)
            {
                Debug.LogWarning($"[Cutscene] playCutscene：這張圖找不到 id=「{id}」的演出" +
                                 "（留空＝第一段；地圖上要先在編輯器「劇情」分頁建立演出）。");
                return false;
            }
            return StartCutscene(cs, map, onDone);
        }

        /// <summary>
        /// 播放條件旗標是否成立（`requireFlag`；前綴 "!" ＝否定，留空＝不檢查）。
        /// 語意與觸發點的 `requireFlag` 完全一致，走同一支 <see cref="TriggerChain.FlagTrue"/>，
        /// 生命週期（周目／永久／關卡單次）由全域 flags.json 決定。
        /// </summary>
        static bool FlagConditionMet(Dipan.MapRuntime.Cutscene cs)
        {
            string req = cs.requireFlag;
            if (string.IsNullOrWhiteSpace(req)) return true;
            req = req.Trim();
            bool neg = req.StartsWith("!");
            string key = neg ? req.Substring(1).Trim() : req;
            if (string.IsNullOrEmpty(key)) return true;
            bool ok = TriggerChain.FlagTrue(key) != neg;
            if (!ok) Debug.Log($"[Cutscene] 條件旗標「{req}」不成立，這段演出不播。");
            return ok;
        }

        /// <summary>實際開演。回傳 false＝這段沒東西可演。</summary>
        static bool StartCutscene(Dipan.MapRuntime.Cutscene cs, MapData map, System.Action onDone)
        {
            if (cs == null || cs.steps == null || cs.steps.Count == 0) return false;
            if (!FlagConditionMet(cs)) return false;

            // 換圖收掉上一個。⚠ **一定要先同步 ReleaseGlobals 再 Destroy**：
            // Destroy 延到幀尾才真的執行（見 PROBLEMS B12），舊 director 的 OnDestroy 安全網會在
            // 「新演出已經把主角藏好、把回憶特效打開」之後才跑 → 當場把新演出的設定全部還原掉。
            // 先同步釋放，`_released` 就已經是 true，幀尾那次 OnDestroy 變成 no-op。
            if (_active != null)
            {
                _active.ReleaseGlobals(restorePlayerPosition: false);
                Destroy(_active.gameObject);
                _active = null;
            }
            for (int i = 0; i < _standing.Count; i++) if (_standing[i] != null) Destroy(_standing[i]);
            _standing.Clear();

            var go = new GameObject("[CutsceneDirector]");
            var dir = go.AddComponent<CutsceneDirector>();
            _active = dir;
            dir._cs = cs;
            dir._onFinished = onDone;
            dir._tileSize = map != null && map.tileSize > 0f ? map.tileSize : 1f;
            dir.StartCoroutine(dir.Run());
            return true;
        }

        void Update()
        {
            // ESC ＝右上角 Skip 的鍵盤版（同一個開關）。2026-08-22 起 `skippable` 是**玩家可見**的功能，
            // 不再是開發限定；沒勾「可略過」的演出則 ESC 與 Skip 都不存在。
            if (SkipOffered && !_skip && Input.GetKeyDown(KeyCode.Escape))
                RequestSkip();

            EnforceHudHidden();
        }

        /// <summary>
        /// 略過整段演出。⚠ **語意是「快轉到結局」而不是「停在原地」**——中止剩餘步驟後仍會執行最後的
        /// <c>end</c> 交棒與 `setFlag`，所以 `end='fall'` 的段落按下去會直接接墜落動畫。
        /// 這是刻意的（跳過表演、但流程照走），排查時別誤判成 bug；歷史說明見 readme/PROBLEMS.md **J2**。
        /// </summary>
        void RequestSkip()
        {
            if (_skip) return;
            _skip = true;

            // 按鈕立刻收掉：不然連點會在收尾的那幾幀重複觸發，也避免它殘留在交棒後的新畫面上。
            HideSkip();

            // 正在播對話時按 Skip：協程雖然會結束，但面板還開著（模態＋暫停）——不關掉的話
            // 「跳過」看起來像是沒反應，玩家還得自己把對話點完。
            if (UIManager.Instance != null)
            {
                if (UIManager.Instance.IsOpen<TalkPanel>()) UIManager.Instance.Close<TalkPanel>();
                if (UIManager.Instance.IsOpen<DramaPanel>()) UIManager.Instance.Close<DramaPanel>();
            }
        }

        // 右上角 Skip（全遊戲統一樣式，見 Dipan.UI.SkipButton）。
        // sortingOrder 5100＝壓在劇情置中漫畫(5000)之上、跨場景黑幕(30000)之下；
        // 也在對話面板(Window=100)之上，所以播對話時按得到。排序帶見 readme/UI_SYSTEM.md。
        /// <summary>
        /// 這段演出現在到底給不給跳：作者勾了「可略過」**且** <see cref="DevSkip.SkipAllowedHere"/>
        /// （序章整段正式版全程不給跳，初始洞窟起才照一般規則）。Skip 按鈕與 ESC 共用同一個判斷。
        /// </summary>
        bool SkipOffered => _cs != null && _cs.skippable && DevSkip.SkipAllowedHere;

        void EnsureSkip()
        {
            if (_skipGo != null || !SkipOffered) return;
            _skipGo = Dipan.UI.SkipButton.CreateOverlay("[CutsceneSkip]", 5100, RequestSkip);
            _skipGo.transform.SetParent(transform, false);   // 掛在自己身上：演出被硬銷毀時一起消失，不會留在畫面上
        }

        void HideSkip()
        {
            if (_skipGo != null) { Destroy(_skipGo); _skipGo = null; }
        }

        /// <summary>
        /// 維持「演出期間血量 HUD 關著」。**必須每幀重做，不能只在開演時關一次**——
        /// 血量 HUD 有兩個會主動打開它的來源，而其中一個的時機正好在開演之後：
        ///   ① <c>MapManager.PlaceAndSetup</c>：在呼叫 MaybeAutoStart **之前**開，所以開演時關掉是有效的；
        ///   ② <c>PlayerController.Start()</c>：玩家**初次生成**那一幀才跑，而 Unity 的 Start 是在
        ///      建立它的那支程式跑完之後才呼叫 ⇒ **比 MaybeAutoStart 晚**，會把剛關掉的 HUD 又開回來。
        /// 症狀就是「勾了關閉血量 HUD 卻還在」，而且只在「進關卡第一張圖（玩家在那裡生成）」才發生，
        /// 同 module 房間互跳反而正常——極難反推。用每幀維持是最省事也最保險的做法：
        /// 之後不管誰去開 HUD 都蓋不過演出。
        /// </summary>
        void EnforceHudHidden()
        {
            if (_cs == null || !_cs.hideHud) return;
            if (UIManager.Instance == null || !UIManager.Instance.IsOpen<BottomHudPanel>()) return;
            _hudWasOpen = true;   // 有人想開＝演出結束後本來就該是開著的，記下來給收尾還原
            UIManager.Instance.Close<BottomHudPanel>();
        }

        IEnumerator Run()
        {
            _cam = FindObjectOfType<MapCameraController>();
            // 具名持有者（PROBLEMS D13）：劇情現在可以由 playCutscene 在遊戲中途啟動，
            // 而 cameraFocus / MosaicController 之類也會掛 hold；共用預設 key 的話誰先解鎖就把別人的一起解掉。
            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(HoldOwner, _cs.lockInput, false);

            EnsureSkip();   // 右上角 Skip（只有勾了「可略過」才會出現）

            // 回憶特效：整段演出掛著「泛黃老照片＋柔邊暈影」後處理，Cleanup 一定會關掉。
            if (_cs.memoryFx) Dipan.MapFx.MemoryFxController.Begin();

            // 關閉底部血量 HUD：演出常演在畫面下方、會被血球擋住。
            // ⚠ 只關 BottomHudPanel、不要用 SetLayerVisible(HUD,false) 把整層藏起來——
            //    頭上對話框（MonsterSpeechPanel）與提示也在 HUD 層，整層藏掉連演員說的話都看不到。
            //    做法比照 MapManager 對開場山道(13/14)的處理。
            //    關一次不夠，之後每幀由 EnforceHudHidden 維持（理由見那支的註解）。
            _hudWasOpen = false;
            EnforceHudHidden();

            // 隱藏主角：關 SpriteRenderer＋影子＋碰撞（不能直接 SetActive，理由見 PlayerVisibility 註解）。
            if (_cs.hidePlayer)
            {
                bool hasPlayerActor = false;
                if (_cs.actors != null)
                    foreach (var a in _cs.actors) if (a != null && a.kind == "player") { hasPlayerActor = true; break; }
                if (hasPlayerActor)
                    Debug.LogWarning("[Cutscene] 這段同時勾了「隱藏主角」又放了 player 演員（主角傀儡）——" +
                                     "主角被藏起來就走不動也看不見，兩者只該選一個。這次以隱藏為準。");
                PlayerVisibility.Hide();
            }

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

            // ⚠ 「有 end 步驟」≠「會換圖」：`end` 的去向留空＝就地收尾、不交棒（見 §3）。
            //    npc 的銷毀沿用舊語意（有 end 就收掉），但「玩家要不要放回原位」「鏈要不要接 next」
            //    必須看**真的有沒有交棒**——否則 end 留空的同圖演出會變成「主角不回原位、鏈莫名斷掉」。
            bool handoff = endStep != null && !string.IsNullOrWhiteSpace(endStep.assetId);

            Cleanup(endStep != null, handoff);

            // 完成寫旗標：被 ESC 略過也算演過了（略過本來就會照走 end 交棒，語意一致）。
            // 典型用法＝條件填 "!x"、這裡填 "x" ⇒ 這段一輩子/這周目/這趟關卡只播一次（由旗標的生命週期決定）。
            if (!string.IsNullOrWhiteSpace(_cs.setFlag))
            {
                TriggerChain.SetFlag(_cs.setFlag.Trim());
                // 旗標變了 → 本圖可能有 requireFlag 依賴它的互動點要現身/消失，重建一次（同 TriggerChain.OnCompleted）。
                if (MapManager.Instance != null) MapManager.Instance.RefreshTriggers();
            }

            if (endStep != null) DoHandoff(endStep.assetId);
            if (_active == this) _active = null;

            // 交棒換圖時鏈就此結束（同 teleportTo 慣例）；沒交棒才把棒子還給觸發鏈接 next。
            var cont = _onFinished; _onFinished = null;
            if (!handoff) cont?.Invoke();

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
                case "bubble": yield return BubbleStep(s); break;
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
            // allowSkip:false —— 演出期間畫面右上角已經有「演出自己的 Skip」，
            // 對話面板再長一顆會變成兩顆疊在同一個位置，而且兩者語意不同（跳一段對話 vs 跳整段演出）。
            if (data != null && data.Type == 2) DramaTalkController.Play(data.TalkGroup, allowSkip: false);
            else DramaPanel.Show(s.dramaId);

            float t = 0f; bool opened = false;
            while (t < 0.5f) { if (DramaOpen()) { opened = true; break; } t += Time.unscaledDeltaTime; yield return null; }
            if (opened) while (DramaOpen() && !_skip) yield return null;
        }

        // 頭上對話框：不跳對話視窗，直接在演員頭上冒一個水墨泡泡（沿用怪物說話那套面板與美術）。
        // 文字走語言表（玩家可見字串一律 Language.GetText，見 AGENTS.md 鐵則）。
        // 預設「說完才往下」＝擋住 seconds 秒；要「一邊走一邊講」就把這步驟勾 background。
        IEnumerator BubbleStep(CutsceneStep s)
        {
            var a = Find(s.actorId);
            if (a == null || a.tr == null)
            {
                Debug.LogWarning($"[Cutscene] bubble 步驟找不到演員「{s.actorId}」，略過。");
                yield break;
            }
            string text = Dipan.Localization.Language.GetText(s.langId);
            float dur = s.seconds > 0f ? s.seconds : 2f;
            a.EnsureVisible();
            Dipan.UI.MonsterSpeechPanel.Speak(a.tr, text, dur);
            yield return WaitUnscaled(dur);
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

        /// <param name="destroyNpcs">有 end 步驟＝把 npc 演員收掉（沒有就留在原地站著）。</param>
        /// <param name="handoff">**真的會換圖/換場景**（end 有填去向）。決定主角要不要放回原位。</param>
        /// <summary>
        /// 還原這段演出對「全域狀態」動過的手（輸入鎖／回憶特效／隱藏主角／血量 HUD）。冪等，可重複呼叫。
        ///
        /// ⚠ **一定要能從 <see cref="OnDestroy"/> 走到這裡**：正常收尾走 <see cref="Cleanup"/>，但演出也可能
        /// **被直接銷毀而不收尾**——換圖時 <c>StartCutscene</c> 會 <c>Destroy</c> 掉還在跑的上一個 director，
        /// 協程當場中斷、`Cleanup` 永遠不會執行。漏掉的話玩家會**永遠隱形、畫面永遠泛黃、輸入永遠鎖著**，
        /// 而且症狀出現在「下一張圖」，跟真正的原因隔了一次換圖，極難反推。
        /// </summary>
        void ReleaseGlobals(bool restorePlayerPosition)
        {
            if (_released) return;
            _released = true;

            // 視覺殘留（漫畫／黑幕／Skip）也在這裡收——被硬銷毀時 Cleanup 不會跑，
            // 留下一張全螢幕黑幕或一顆 Skip 卡在畫面上是最糟的失敗方式。
            HideComic();
            HideFade();
            HideSkip();

            if (_cs != null && _cs.memoryFx) Dipan.MapFx.MemoryFxController.End();

            // 血量 HUD 還原成「開演前的樣子」——不是無條件打開。開場山道(13/14)那種本來就沒 HUD 的圖，
            // 無條件 Open 會憑空生出血球。有交棒換圖時也照還原，換圖後 MapManager 會依新地圖再決定一次。
            if (_cs != null && _cs.hideHud && _hudWasOpen && UIManager.Instance != null
                && !UIManager.Instance.IsOpen<BottomHudPanel>())
                UIManager.Instance.Open<BottomHudPanel>();

            // 主角現身：同圖結束 → 放回開演前的位置；有交棒換圖 → 新圖會自己安排落點，不要硬拉回舊座標。
            if (_cs != null && _cs.hidePlayer) PlayerVisibility.Show(restorePlayerPosition);

            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(HoldOwner, false, false);
        }

        void OnDestroy()
        {
            // 安全網：被中途銷毀（換圖打斷、停止 Play）時，至少要把全域狀態還原。
            // 位置不還原——會走到這裡多半是換圖，新圖自己會安排落點。
            ReleaseGlobals(restorePlayerPosition: false);
        }

        void Cleanup(bool destroyNpcs, bool handoff)
        {
            ReleaseGlobals(restorePlayerPosition: !handoff);   // 漫畫/黑幕/Skip 與全域狀態都在裡面收

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
