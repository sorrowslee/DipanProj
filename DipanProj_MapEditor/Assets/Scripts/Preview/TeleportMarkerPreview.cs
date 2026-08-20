using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using DipanMapEditor.Core;
using DipanMapEditor.Data;
using DipanMapEditor.Tools;
using DipanMapEditor.UI;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 「傳送點對位」模式：在編輯器裡用**遊戲真正的傳送點特效**把每個 teleport 的外型落點畫出來，並可直接拖曳定位。
    ///
    /// 為什麼需要：門的美術是畫在背景圖裡的（地圖資料裡沒有「門在哪」這件事），所以外型位置只能人工對。
    /// 以前只有一個黃十字＋「設定外型位置→點一下→存檔→進遊戲看」的循環，等於盲調；
    /// 這裡把遊戲那 48 幀動畫照原尺寸、原幀率播出來，所見即所得，一次拖到位。
    ///
    /// 資料來源刻意跟遊戲同一份，外型改了編輯器自動跟著改：
    ///   - 配方：主專案 <c>Assets/Data/VfxTable.csv</c> 的 ID 6 那列（AniPath / AniNumber / AnimFPS / Scale）
    ///   - 圖：主專案 <c>Assets/Resources/&lt;AniPath&gt;_NN.png</c>（NN 從 01 起，同 VfxManager 的 D2 命名）
    /// 跨專案直接讀磁碟 PNG 的做法沿用 <see cref="PreviewSpriteLoader"/>（劇情演員預覽），不必同步素材。
    ///
    /// ⚠ 落點規則必須與主遊戲 <c>MapLoader.BuildTeleportMarkers</c> 一致：
    /// 有 markerX/markerY 就用它，否則退回「格子平均中心」。改一邊要兩邊一起改。
    /// </summary>
    public class TeleportMarkerPreview : MonoBehaviour
    {
        /// <summary>傳送點的 trigger 類型 id（同 triggerTypes.json 與主遊戲 MapLoader.teleportTypeId）。</summary>
        public const string TeleportTypeId = "teleport";
        /// <summary>傳送點外型的 VfxTable id（同主遊戲 MapLoader.teleportVfxId）。</summary>
        public const int TeleportVfxId = 6;

        // ⚠ 以下四個 key 與兩個預設值是主遊戲 TeleportAnchor 的鏡像，改一邊要兩邊一起改。
        public const string KeyX = "markerX";
        public const string KeyY = "markerY";
        public const string KeyW = "markerW";
        public const string KeyH = "markerH";
        /// <summary>沒填寬高時的預設踩踏矩形（世界單位）。門通常「寬而矮」，所以預設不是正方形。</summary>
        public const float DefaultW = 1.0f;
        public const float DefaultH = 0.6f;

        // VfxEffects 的匯入 PPU（主專案 GameEffectTextureImportSettings 統一設 100）。
        const float VfxPixelsPerUnit = 100f;
        // 排序：同主遊戲 VfxManager 的全域預設 22000。高於編輯器地上物帶（最高 18999），
        // 低於照明預覽(32766)與底部 UI 參考層(32767)。⚠ sortingOrder 實質 16-bit，別填更大。
        const int SortOrder = 22000;
        const string PrefKey = "dipan.teleportAlignMode";

        /// <summary>對位模式開關（由 EditorUI 頂部列「傳送點對位」鈕切換，PlayerPrefs 記住）。</summary>
        public bool Enabled { get; private set; }

        // ---- VfxTable 的一列（只取畫圖需要的欄位）----
        class VfxDef { public string aniPath; public int aniNumber; public float fps = 24f; public float scale = 1f; }

        static VfxDef _def;
        static Sprite[] _frames;
        static bool _loadTried;

        readonly Dictionary<TriggerRegion, SpriteRenderer> _renderers = new Dictionary<TriggerRegion, SpriteRenderer>();
        readonly HashSet<TriggerRegion> _live = new HashSet<TriggerRegion>();
        readonly List<TriggerRegion> _dead = new List<TriggerRegion>();
        float _animTimer;
        EditorUI _ui;
        Transform _root;
        Camera _cam;

        void Awake() => Enabled = PlayerPrefs.GetInt(PrefKey, 0) != 0;

        /// <summary>切換對位模式。</summary>
        public void Toggle() => SetEnabled(!Enabled);

        public void SetEnabled(bool on)
        {
            Enabled = on;
            PlayerPrefs.SetInt(PrefKey, on ? 1 : 0);
            if (!on) ClearRenderers();
        }

        /// <summary>丟掉已載入的動畫（「刷新素材」時呼叫，改完 VfxTable 或換圖後才會重讀）。</summary>
        public static void ClearCache()
        {
            _frames = null; _def = null; _loadTried = false;
        }

        // ---------------- 落點規則（與主遊戲 MapLoader 一致）----------------

        /// <summary>傳送點外型的落點：有錨點 markerX/markerY 用它，否則退回格子平均中心。</summary>
        public static bool TryMarkerPos(TriggerRegion r, MapData map, out Vector2 pos)
        {
            pos = Vector2.zero;
            if (r == null || map == null) return false;
            if (r.Params != null
                && r.Params.TryGetValue("markerX", out var vx) && vx != null && TryF(vx, out float mx)
                && r.Params.TryGetValue("markerY", out var vy) && vy != null && TryF(vy, out float my))
            { pos = new Vector2(mx, my); return true; }

            if (r.cells == null || r.cells.Count == 0) return false;
            Vector2 sum = Vector2.zero; int n = 0;
            Vector2 origin = MapCoords.Origin(map);
            foreach (var c in r.cells)
            {
                if (c == null || c.Length < 2) continue;
                sum += MapCoords.CellCenter(c[0], c[1], map.tileSize, origin); n++;
            }
            if (n == 0) return false;
            pos = sum / n; return true;
        }

        /// <summary>這個傳送點有沒有設錨點（有＝點模式、沒有＝舊的格子模式）。</summary>
        public static bool HasAnchor(TriggerRegion r)
            => r?.Params != null && r.Params.ContainsKey(KeyX) && r.Params.ContainsKey(KeyY);

        /// <summary>踩踏矩形的尺寸（沒填或填 0 用預設）。同主遊戲 TeleportAnchor.TouchSize。</summary>
        public static Vector2 TouchSize(TriggerRegion r)
        {
            float w = DefaultW, h = DefaultH;
            if (r?.Params != null)
            {
                if (r.Params.TryGetValue(KeyW, out var vw) && TryF(vw, out float pw) && pw > 0.001f) w = pw;
                if (r.Params.TryGetValue(KeyH, out var vh) && TryF(vh, out float ph) && ph > 0.001f) h = ph;
            }
            return new Vector2(w, h);
        }

        /// <summary>寫入踩踏矩形尺寸（會夾在合理範圍，避免拖成 0 或整張圖）。</summary>
        public static void SetSize(TriggerRegion r, float w, float h)
        {
            if (r == null) return;
            if (r.Params == null) r.Params = new Dictionary<string, object>();
            r.Params[KeyW] = Mathf.Clamp(w, MinSize, MaxSize);
            r.Params[KeyH] = Mathf.Clamp(h, MinSize, MaxSize);
        }

        public const float MinSize = 0.2f;
        public const float MaxSize = 20f;

        /// <summary>踩踏矩形（以錨點為中心；沒設錨點就以格子平均中心為中心，代表「轉換後會長這樣」）。</summary>
        public static bool TryTouchRect(TriggerRegion r, MapData map, out Rect rect)
        {
            rect = new Rect();
            if (!TryMarkerPos(r, map, out Vector2 c)) return false;
            Vector2 s = TouchSize(r);
            rect = new Rect(c.x - s.x * 0.5f, c.y - s.y * 0.5f, s.x, s.y);
            return true;
        }

        /// <summary>寫入外型錨點（拖曳中每幀呼叫；Undo 由呼叫端在按下當幀 Push 一次）。</summary>
        public static void SetMarker(TriggerRegion r, Vector2 world)
        {
            if (r == null) return;
            if (r.Params == null) r.Params = new Dictionary<string, object>();
            r.Params["markerX"] = world.x;
            r.Params["markerY"] = world.y;
        }

        /// <summary>清掉外型錨點 → 退回格子平均中心。</summary>
        public static void ResetMarker(TriggerRegion r)
        {
            if (r?.Params == null) return;
            r.Params.Remove("markerX");
            r.Params.Remove("markerY");
        }

        /// <summary>此傳送點是否會在遊戲中顯示外型（勾掉「使用傳送點外型」＝不顯示）。預設 true，同遊戲端。</summary>
        public static bool ShowsMarker(TriggerRegion r)
        {
            if (r?.Params == null) return true;
            if (!r.Params.TryGetValue("showMarker", out var v) || v == null) return true;
            string s = v.ToString().Trim();
            if (s.Length == 0) return true;
            if (bool.TryParse(s, out bool b)) return b;
            return s != "0";
        }

        /// <summary>右下角「改大小」把手的邊長（世界單位）。</summary>
        public const float HandleSize = 0.22f;

        /// <summary>把手中心＝踩踏矩形的右下角。</summary>
        public static bool TryHandlePos(TriggerRegion r, MapData map, out Vector2 pos)
        {
            pos = Vector2.zero;
            if (!TryTouchRect(r, map, out Rect rect)) return false;
            pos = new Vector2(rect.xMax, rect.yMin);
            return true;
        }

        /// <summary>命中「改大小」把手（優先於拖曳本體，否則角落永遠抓不到）。</summary>
        public bool TryPickHandle(Vector2 world, out TriggerRegion region)
        {
            region = null;
            var map = MapSession.Instance?.Map;
            var trig = map?.TriggerLayer;
            if (map == null || trig?.regions == null) return false;

            float half = HandleSize;   // 抓取範圍給到把手邊長，避免要對得太準
            float best = half * half;
            foreach (var r in trig.regions)
            {
                if (r == null || r.typeId != TeleportTypeId) continue;
                if (!TryHandlePos(r, map, out Vector2 p)) continue;
                float d2 = (p - world).sqrMagnitude;
                if (d2 <= best) { best = d2; region = r; }
            }
            return region != null;
        }

        /// <summary>
        /// 點選命中測試：world 落在某個傳送點的踩踏矩形內、或錨點附近（取最近的一個）。
        /// 矩形可以被拖得很小，所以錨點附近一律算命中，免得小矩形抓不到。
        /// </summary>
        public bool TryPick(Vector2 world, out TriggerRegion region)
        {
            region = null;
            var map = MapSession.Instance?.Map;
            var trig = map?.TriggerLayer;
            if (map == null || trig?.regions == null) return false;

            float radius = Mathf.Max(0.4f, map.tileSize * 0.55f);
            float best = float.MaxValue;
            foreach (var r in trig.regions)
            {
                if (r == null || r.typeId != TeleportTypeId) continue;
                if (!TryMarkerPos(r, map, out Vector2 p)) continue;
                bool inRect = TryTouchRect(r, map, out Rect rect) && rect.Contains(world);
                float d2 = (p - world).sqrMagnitude;
                if (!inRect && d2 > radius * radius) continue;
                // 同時命中多個時取錨點最近的
                if (d2 < best) { best = d2; region = r; }
            }
            return region != null;
        }

        // ---------------- 渲染 ----------------

        void Update()
        {
            var map = MapSession.Instance?.Map;
            if (!Enabled || map == null) { if (_renderers.Count > 0) ClearRenderers(); return; }

            EnsureLoaded();
            if (_frames == null || _frames.Length == 0) return;

            float fps = (_def != null && _def.fps > 0f) ? _def.fps : 24f;
            _animTimer += Time.deltaTime;
            int frame = Mathf.Abs((int)(_animTimer * fps)) % _frames.Length;
            float scale = (_def != null && _def.scale > 0f) ? _def.scale : 1f;

            _live.Clear();
            var trig = map.TriggerLayer;
            if (trig?.regions != null)
            {
                foreach (var r in trig.regions)
                {
                    if (r == null || r.typeId != TeleportTypeId) continue;
                    if (!TryMarkerPos(r, map, out Vector2 pos)) continue;
                    _live.Add(r);

                    var sr = GetRenderer(r);
                    sr.sprite = _frames[frame];
                    // 勾掉「使用傳送點外型」的照樣畫，但壓成半透明 —— 讓你知道那顆在遊戲裡不會出現。
                    sr.color = ShowsMarker(r) ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                    sr.transform.position = new Vector3(pos.x, pos.y, 0f);
                    sr.transform.localScale = Vector3.one * scale;
                }
            }

            // 清掉已刪除/換圖後不存在的
            _dead.Clear();
            foreach (var kv in _renderers) if (!_live.Contains(kv.Key)) _dead.Add(kv.Key);
            foreach (var k in _dead)
            {
                if (_renderers.TryGetValue(k, out var sr) && sr != null) Destroy(sr.gameObject);
                _renderers.Remove(k);
            }
        }

        SpriteRenderer GetRenderer(TriggerRegion r)
        {
            if (_renderers.TryGetValue(r, out var sr) && sr != null) return sr;
            if (_root == null) _root = new GameObject("[TeleportMarkerPreview]").transform;
            var go = new GameObject("TeleportMarker");
            go.transform.SetParent(_root, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortOrder;
            _renderers[r] = sr;
            return sr;
        }

        void ClearRenderers()
        {
            foreach (var kv in _renderers) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _renderers.Clear();
            if (_root != null) { Destroy(_root.gameObject); _root = null; }
        }

        void OnDisable() => ClearRenderers();

        // ---------------- 標籤與提示 ----------------

        void OnGUI()
        {
            if (!Enabled) return;
            // 名稱標籤與操作提示只在 Trigger 工具下出現 —— 那才是能拖曳對位的地方。
            // 在物件/可走/照明等工具下光盤照畫（要拿它對齊背景的門或地上物），但不該再蓋一層文字。
            if (_ui == null) _ui = FindObjectOfType<EditorUI>();
            if (_ui == null || _ui.CurrentTool != EditTool.Trigger) return;
            var map = MapSession.Instance?.Map;
            if (map == null || _renderers.Count == 0) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            var vp = EditorUI.ViewportRect;
            var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            st.normal.textColor = new Color(1f, 0.95f, 0.4f, 0.95f);

            foreach (var kv in _renderers)
            {
                var r = kv.Key;
                if (kv.Value == null) continue;
                Vector3 sp = _cam.WorldToScreenPoint(kv.Value.transform.position);
                if (sp.z < 0f) continue;
                float gx = sp.x, gy = Screen.height - sp.y;
                if (!vp.Contains(new Vector2(gx, gy))) continue;
                string label = string.IsNullOrEmpty(r.name) ? "傳送點" : r.name;
                GUI.Label(new Rect(gx - 70f, gy - 46f, 140f, 18f), label, st);
            }

            var hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            hint.normal.textColor = Color.cyan;
            GUI.Label(new Rect(vp.x, vp.yMax - 26f, vp.width, 22f),
                "傳送點對位模式：Trigger 工具下直接拖曳外型即可對齊門（半透明＝該傳送點勾掉了「使用傳送點外型」）", hint);
        }

        // ---------------- 載入（跨專案讀主專案磁碟）----------------

        void EnsureLoaded()
        {
            if (_loadTried) return;
            _loadTried = true;

            string mainAssets = MainAssetsRoot();
            _def = LoadDef(Path.Combine(mainAssets, "Data", "VfxTable.csv"), TeleportVfxId);
            if (_def == null || string.IsNullOrEmpty(_def.aniPath) || _def.aniNumber <= 0)
            {
                Debug.LogWarning($"[TeleportMarkerPreview] 讀不到 VfxTable 的 ID {TeleportVfxId}（{Path.Combine(mainAssets, "Data", "VfxTable.csv")}），傳送點對位預覽停用。");
                _frames = null;
                return;
            }

            string resRoot = Path.Combine(mainAssets, "Resources");
            var sprites = new List<Sprite>(_def.aniNumber);
            for (int i = 1; i <= _def.aniNumber; i++)
            {
                // 同主遊戲 VfxManager 的命名：{AniPath}_{編號:D2}
                string rel = _def.aniPath.Replace('/', Path.DirectorySeparatorChar) + "_" + i.ToString("D2") + ".png";
                string path = Path.Combine(resRoot, rel);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[TeleportMarkerPreview] 找不到第 {i} 幀：{path}");
                    continue;
                }
                try
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    // markNonReadable=true：丟掉 CPU 端副本（48 幀 512² 省一半記憶體）
                    tex.LoadImage(File.ReadAllBytes(path), true);
                    sprites.Add(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                              new Vector2(0.5f, 0.5f), VfxPixelsPerUnit));
                }
                catch (System.Exception e) { Debug.LogWarning($"[TeleportMarkerPreview] 第 {i} 幀載入失敗：{e.Message}"); }
            }
            _frames = sprites.Count > 0 ? sprites.ToArray() : null;
            if (_frames != null)
                Debug.Log($"[TeleportMarkerPreview] 傳送點外型已載入 {_frames.Length}/{_def.aniNumber} 幀（{_def.aniPath}，{_def.fps}fps，Scale {_def.scale}）。");
        }

        /// <summary>主專案 Assets 目錄（＝ 這個編輯器專案的姊妹目錄）。同 PreviewSpriteLoader 的推導方式。</summary>
        static string MainAssetsRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;   // DipanProj_MapEditor
            string repoRoot = Directory.GetParent(projectRoot).FullName;               // DipanProj
            return Path.Combine(repoRoot, "DipanProj_Main", "Assets");
        }

        /// <summary>從 VfxTable.csv 撈出指定 id 那列（欄序同主遊戲 VfxManager.LoadEffects）。</summary>
        static VfxDef LoadDef(string csvPath, int id)
        {
            if (!File.Exists(csvPath)) return null;
            string[] lines;
            try { lines = File.ReadAllLines(csvPath); } catch { return null; }
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var v = lines[i].Split(',');
                if (v.Length < 5) continue;
                if (!int.TryParse(v[0].Trim(), out int rowId) || rowId != id) continue;
                var d = new VfxDef { aniPath = v[2].Trim() };
                int.TryParse(v[3].Trim(), out d.aniNumber);
                if (TryF(v[4], out float fps) && fps > 0f) d.fps = fps;
                if (v.Length > 5 && TryF(v[5], out float sc) && sc > 0f) d.scale = sc;
                return d;
            }
            return null;
        }

        static bool TryF(object o, out float f)
            => float.TryParse(o == null ? "" : o.ToString().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
