using System.Text;
using UnityEngine;
using Dipan.MapRuntime;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Dipan.Diagnostics
{
    /// <summary>
    /// 系統資訊 / 效能診斷面板（純程式建構、零接線）。
    ///
    /// 用途：在「實機 build」上驗證系統狀況——特別是排查「Windows build 幀數低、Mac/編輯器卻順」的問題。
    /// 開場自動生成（仿 VfxManager / SaveBootstrap），預設關閉，按 <b>P</b> 開關。
    ///
    /// 顯示：
    ///   - FPS（平滑）＋ frame time(ms)＋ 1% low（卡頓指標）
    ///   - CPU 主執行緒 ms / GPU ms（FrameTimingManager）→ 直接判斷「CPU bound 還是 GPU bound」
    ///   - 顯示卡名稱 + 繪圖 API（在 Windows 上可立刻看出是不是跑在「內顯」上）
    ///   - VSync / 目標幀率 / 螢幕刷新率 / 解析度 / 全螢幕模式
    ///   - 畫質等級 / 色彩空間 / 記憶體
    ///   - （僅編輯器）Draw Call / Batches / SetPass / 三角形數
    ///
    /// 註：Draw Call 等渲染計數在「player build」沒有公開 runtime API（UnityStats 是 Editor-only），
    ///     所以在 build 上改用 <b>GPU ms</b> 當作 GPU 負載指標（要先在 Player Settings 開啟 Frame Timing Stats，
    ///     本專案已一併開啟）。
    /// </summary>
    public class PerfHud : MonoBehaviour
    {
        // ---- 自動生成（零接線） ----
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            var go = new GameObject("[PerfHud]");
            go.AddComponent<PerfHud>();
            DontDestroyOnLoad(go);
        }

        // ---- 設定 ----
        [SerializeField] private KeyCode toggleKey = KeyCode.P;
        [SerializeField] private bool visibleOnStart = false;
        [SerializeField] private float sampleInterval = 0.5f; // 統計刷新間隔（秒）

        // ---- 狀態 ----
        private bool _visible;

        // FPS / frame time 取樣
        private float _accumTime;
        private int _frames;
        private float _dispFps;
        private float _dispMs;
        private float _worstMs;          // 取樣區間內最差一幀（卡頓指標）
        private float _worstMsShown;
        private float _intervalWorstMs;

        // FrameTimingManager（CPU/GPU ms）
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private double _cpuMainMs = -1;
        private double _gpuMs = -1;
        private double _cpuRenderMs = -1;

        // 靜態系統資訊（快取一次）
        private string _sysInfo;

        private GUIStyle _box;
        private GUIStyle _label;
        private GUIStyle _btn;
        private Texture2D _bgTex;

        private void Awake()
        {
            _visible = visibleOnStart;
            BuildStaticSysInfo();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                _visible = !_visible;

            // 面板開著時，V 切 VSync、T 切目標幀率（也可用面板上的按鈕點）
            if (_visible)
            {
                if (Input.GetKeyDown(KeyCode.V)) CycleVSync();
                if (Input.GetKeyDown(KeyCode.T)) CycleTarget();
                if (Input.GetKeyDown(KeyCode.F)) MapSpriteLoader.ToggleSceneFilterMode();
            }

            // 用 unscaled，暫停（Time.timeScale=0）時也能量測
            float dt = Time.unscaledDeltaTime;
            _accumTime += dt;
            _frames++;

            float ms = dt * 1000f;
            if (ms > _intervalWorstMs) _intervalWorstMs = ms;

            if (_accumTime >= sampleInterval)
            {
                _dispFps = _frames / _accumTime;
                _dispMs = (_accumTime / _frames) * 1000f;
                _worstMsShown = _intervalWorstMs;
                _accumTime = 0f;
                _frames = 0;
                _intervalWorstMs = 0f;

                SampleFrameTimings();
            }
        }

        private void SampleFrameTimings()
        {
            // 需要 Player Settings → Frame Timing Stats 開啟（本專案已開）。
            FrameTimingManager.CaptureFrameTimings();
            uint got = FrameTimingManager.GetLatestTimings(1, _timings);
            if (got > 0)
            {
                _cpuMainMs = _timings[0].cpuMainThreadFrameTime;
                _cpuRenderMs = _timings[0].cpuRenderThreadFrameTime;
                _gpuMs = _timings[0].gpuFrameTime;
            }
            else
            {
                _cpuMainMs = _gpuMs = _cpuRenderMs = -1;
            }
        }

        private void BuildStaticSysInfo()
        {
            var sb = new StringBuilder(512);
            sb.Append("GPU : ").Append(SystemInfo.graphicsDeviceName).Append('\n');
            sb.Append("API : ").Append(SystemInfo.graphicsDeviceType)
              .Append("  (").Append(SystemInfo.graphicsDeviceVersion).Append(")\n");
            sb.Append("VRAM: ").Append(SystemInfo.graphicsMemorySize).Append(" MB\n");
            sb.Append("CPU : ").Append(SystemInfo.processorType)
              .Append("  x").Append(SystemInfo.processorCount).Append('\n');
            sb.Append("RAM : ").Append(SystemInfo.systemMemorySize).Append(" MB");
            _sysInfo = sb.ToString();
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            var sb = new StringBuilder(1024);

            // --- FPS / frame time ---
            sb.Append("<b>== 效能 (P 關閉) ==</b>\n");
            sb.Append(string.Format("FPS : {0,5:0.0}   ({1:0.00} ms/幀)\n", _dispFps, _dispMs));
            sb.Append(string.Format("最差一幀 : {0:0.0} ms  ({1:0} fps 瞬時)\n",
                _worstMsShown, _worstMsShown > 0.001f ? 1000f / _worstMsShown : 0f));

            // --- CPU vs GPU（關鍵：判斷瓶頸在哪） ---
            if (_gpuMs >= 0 || _cpuMainMs >= 0)
            {
                sb.Append("\n<b>-- CPU / GPU 耗時 --</b>\n");
                sb.Append(string.Format("CPU 主緒 : {0}\n", FmtMs(_cpuMainMs)));
                sb.Append(string.Format("CPU 渲染 : {0}\n", FmtMs(_cpuRenderMs)));
                sb.Append(string.Format("GPU      : {0}\n", FmtMs(_gpuMs)));
                sb.Append("瓶頸 : ").Append(BottleneckGuess()).Append('\n');
            }
            else
            {
                sb.Append("\n(CPU/GPU ms 無資料：需開 Player Settings → Frame Timing Stats)\n");
            }

            // --- 渲染計數（僅編輯器有 API） ---
#if UNITY_EDITOR
            sb.Append("\n<b>-- 渲染 (僅編輯器) --</b>\n");
            sb.Append(string.Format("Draw Calls : {0}\n", UnityStats.drawCalls));
            sb.Append(string.Format("Batches    : {0}  (省下 {1})\n", UnityStats.batches, UnityStats.dynamicBatchedDrawCalls + UnityStats.staticBatchedDrawCalls));
            sb.Append(string.Format("SetPass    : {0}\n", UnityStats.setPassCalls));
            sb.Append(string.Format("Tris/Verts : {0} / {1}\n", UnityStats.triangles, UnityStats.vertices));
#else
            sb.Append("\n(Draw Call 計數僅編輯器可讀；build 上看 GPU ms)\n");
#endif

            // --- 顯示 / 同步設定 ---
            sb.Append("\n<b>-- 顯示設定 --</b>\n");
            var res = Screen.currentResolution;
            sb.Append(string.Format("解析度 : {0}x{1} @{2:0.#}Hz\n",
                Screen.width, Screen.height, res.refreshRateRatio.value));
            sb.Append("模式   : ").Append(Screen.fullScreenMode).Append('\n');
            sb.Append(string.Format("VSync  : {0}   目標幀率 : {1}\n",
                QualitySettings.vSyncCount,
                Application.targetFrameRate < 0 ? "不限(-1)" : Application.targetFrameRate.ToString()));
            sb.Append("畫質   : ").Append(QualitySettings.names[QualitySettings.GetQualityLevel()])
              .Append("   色彩 : ").Append(QualitySettings.activeColorSpace).Append('\n');

            // --- 記憶體 ---
            long mono = System.GC.GetTotalMemory(false);
            long reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
            sb.Append("\n<b>-- 記憶體 --</b>\n");
            sb.Append(string.Format("Mono GC : {0:0.0} MB\n", mono / 1048576.0));
            if (reserved > 0)
                sb.Append(string.Format("Unity 保留 : {0:0.0} MB\n", reserved / 1048576.0));

            // --- 系統 ---
            sb.Append("\n<b>-- 系統 --</b>\n");
            sb.Append(_sysInfo);

            // 畫出來
            string text = sb.ToString();

            // 依畫面高度自動縮放字級，確保整段都在畫面內、底部不被裁掉
            // （解決小 Game view / 低解析度視窗時最下面資訊被切掉的問題）
            int lines = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
            float availH = Screen.height - 24f;            // 上下各留一點邊
            float perLine = (availH / lines) / 1.28f;       // 估算可用的每行高度
            _label.fontSize = Mathf.Clamp(Mathf.FloorToInt(perLine), 9, 15);
            _label.normal.textColor = FpsColor(_dispFps);   // FPS 顏色提示（整段染色）

            float w = 380f;
            var content = new GUIContent(text);
            float contentH = _label.CalcHeight(content, w);
            const float ctrlRowH = 58f;   // 控制鈕（兩列：VSync/FPS ＋ 場景濾波）的高度
            float h = Mathf.Min(contentH + 18f + ctrlRowH, Screen.height - 16f);
            GUILayout.BeginArea(new Rect(10, 10, w, h), _box);

            // 控制鈕：點一下切換（遠端用滑鼠最穩；也可用鍵盤 V / T）
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("VSync(V): " + VSyncName(), _btn)) CycleVSync();
            if (GUILayout.Button("目標FPS(T): " + TargetName(), _btn)) CycleTarget();
            GUILayout.EndHorizontal();

            // 場景貼圖濾波即時切換（比較大螢幕上「柔化 vs 硬像素」用）
            if (GUILayout.Button("場景濾波(F): " + SceneFilterName(), _btn))
                MapSpriteLoader.ToggleSceneFilterMode();

            GUILayout.Label(content, _label);
            GUILayout.EndArea();
        }

        // ---- VSync / 目標幀率 切換 ----
        // VSync 純軟體設定，可隨時改：0=關、1=每次刷新同步、2=每兩次。
        // Application.targetFrameRate 只有在 vSyncCount=0（VSync 關）時才生效。
        private static readonly int[] TargetOptions = { -1, 30, 60, 120, 144, 240 };

        private void CycleVSync()
        {
            QualitySettings.vSyncCount = (QualitySettings.vSyncCount + 1) % 3;
        }

        private void CycleTarget()
        {
            int cur = Application.targetFrameRate;
            int idx = 0;
            for (int i = 0; i < TargetOptions.Length; i++)
                if (TargetOptions[i] == cur) { idx = i; break; }
            Application.targetFrameRate = TargetOptions[(idx + 1) % TargetOptions.Length];
        }

        private static string VSyncName()
        {
            switch (QualitySettings.vSyncCount)
            {
                case 0: return "關";
                case 1: return "開·每刷新";
                default: return "開·每2刷";
            }
        }

        private static string TargetName()
        {
            int t = Application.targetFrameRate;
            string s = t < 0 ? "不限" : t.ToString();
            if (QualitySettings.vSyncCount > 0) s += "(VSync開,忽略)";
            return s;
        }

        private static string SceneFilterName()
        {
            return MapSpriteLoader.SceneFilterMode == FilterMode.Point ? "Point(硬像素)" : "Bilinear(柔化)";
        }

        private string BottleneckGuess()
        {
            if (_gpuMs < 0 || _cpuMainMs < 0) return "—";
            double cpu = System.Math.Max(_cpuMainMs, _cpuRenderMs);
            double load = System.Math.Max(cpu, _gpuMs);    // 真正在算的時間
            double frame = _dispMs;                         // 實際每幀時間

            // 若真正的負載遠低於每幀時間 → 不是算不動，是被 VSync/刷新率擋住（剩下時間都在等垂直同步）
            if (frame > 0.001f && load < frame * 0.6)
            {
                return QualitySettings.vSyncCount > 0
                    ? "受 VSync 限制（鎖在螢幕刷新率；實際負載很低、非效能問題）"
                    : "受幀率上限/其他限制（實際負載很低）";
            }
            if (_gpuMs > cpu * 1.15) return "GPU bound（填充率/特效/HDR？）";
            if (cpu > _gpuMs * 1.15) return "CPU bound（腳本/物理/GC？）";
            return "CPU≈GPU 接近";
        }

        private static string FmtMs(double ms)
        {
            return ms < 0 ? "—" : string.Format("{0:0.00} ms", ms);
        }

        private static Color FpsColor(float fps)
        {
            if (fps >= 58f) return new Color(0.5f, 1f, 0.5f);
            if (fps >= 30f) return new Color(1f, 0.85f, 0.3f);
            return new Color(1f, 0.4f, 0.4f);
        }

        private void EnsureStyles()
        {
            if (_box != null) return;

            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            _bgTex.Apply();

            _box = new GUIStyle(GUI.skin.box);
            _box.normal.background = _bgTex;
            _box.padding = new RectOffset(10, 10, 8, 8);

            _label = new GUIStyle(GUI.skin.label);
            _label.richText = true;
            _label.fontSize = 14;
            _label.alignment = TextAnchor.UpperLeft;
            _label.wordWrap = false;
            _label.normal.textColor = Color.white;

            _btn = new GUIStyle(GUI.skin.button);
            _btn.fontSize = 12;
            _btn.padding = new RectOffset(6, 6, 4, 4);
        }
    }
}
