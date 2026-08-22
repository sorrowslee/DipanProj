using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dipan.MapFx
{
    /// <summary>
    /// 回憶特效（泛黃老照片 ＋ 柔邊暈影）——**持續型**全螢幕後處理。
    ///
    /// ★ 為什麼不做成 ScreenFxTable 的一個 id：<see cref="ScreenFxPlayer"/> 那家族（睜眼／破幻術／馬賽克）
    ///   全是「播 N 秒就結束」的一次性過場，而且會暫停遊戲＋藏 HUD。回憶輪廓要的是「整段劇情期間一直掛著、
    ///   期間玩家與演員照常動」，語意屬於 <c>AtmosphereController</c> 那種常駐 blit，所以獨立成這支。
    ///
    /// 用法：<see cref="Begin"/> 淡入、<see cref="End"/> 淡出並自動移除 blit。
    /// 目前唯一呼叫者＝劇情演出的 <c>memoryFx</c> 勾選（<see cref="Dipan.Cutscene.CutsceneDirector"/>），
    /// 演出結束／被略過／中途換圖都保證會 End。
    ///
    /// 疊合順序：本 blit 掛在 Camera.main 上，會和 <c>AtmosphereBlit</c> 依元件加入順序串接
    /// （氛圍先算、回憶疊在其上）。要調濃淡改下面的常數；⚠ Linear 色彩空間疊色比直覺重一倍，
    /// 每次調完務必進遊戲看實機（見 readme/PROBLEMS.md E11）。
    /// </summary>
    [DisallowMultipleComponent]
    public class MemoryFxController : MonoBehaviour
    {
        // ── 外觀常數（要調回憶的濃淡就改這裡）──
        const float FadeSeconds = 0.6f;   // 淡入/淡出秒數
        const float Sepia       = 0.85f;  // 泛黃強度（往暖褐色偏多少）
        const float Desat       = 0.65f;  // 去飽和（老照片不是全彩）
        const float VigStart    = 0.34f;  // 離中心多遠開始「柔邊＋壓暗」（越小＝輪廓越往內縮）
        const float VigPower    = 0.62f;  // 邊緣壓暗量
        const float BlurPx      = 6.0f;   // 邊緣最大模糊半徑（像素）
        const float Grain       = 0.020f; // 顆粒
        static readonly Color Tint = new Color(1.00f, 0.86f, 0.62f, 1f);   // 相紙暖褐

        /// <summary>上下黑邊各佔畫面高度的比例（0＝不要黑邊）。電影過場感，與場景明暗無關。</summary>
        const float Letterbox = 0.11f;

        /// <summary>
        /// 回憶期間是否把整套場景氛圍（Atmosphere）淡掉。
        /// **預設開，而且這是回憶在暗地圖上看不看得見的關鍵**——紅嫁衣全 10 張圖都是「幽暗＋打光」，
        /// 除了提燈那一圈以外接近全黑，而泛黃／暈影／柔邊全都是乘法或壓暗，
        /// 在黑色上乘任何顏色還是黑 ⇒ 整套回憶效果等於失效。把氛圍淡掉讓場景亮回來，效果才出得來。
        /// 語意也對：回憶不是「現在這個黑房間」，不該有提燈的黑暗感。
        /// ⚠ 連天氣（雨雪風霧）與提燈光圈也會一起淡掉。要保留場景原本的暗，把這個改成 false。
        /// </summary>
        const bool SuspendAtmosphere = true;

        public static MemoryFxController Instance { get; private set; }
        /// <summary>目前是否掛著回憶特效（含淡出中）。</summary>
        public static bool IsActive => Instance != null && Instance._target > 0f;

        Material _mat;
        Camera _cam;
        MemoryFxBlit _blit;
        float _amount;   // 目前強度
        float _target;   // 目標強度（1=開、0=關）

        static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("[MemoryFx]");
            go.AddComponent<MemoryFxController>();
            DontDestroyOnLoad(go);
        }

        /// <summary>開始套用回憶特效（淡入）。重複呼叫安全。</summary>
        public static void Begin()
        {
            Ensure();
            if (Instance == null) return;
            Instance._target = 1f;
            Instance.AttachBlit();
        }

        /// <summary>移除回憶特效（淡出後自動卸掉 blit）。沒在跑時呼叫安全。</summary>
        public static void End()
        {
            if (Instance == null) return;
            Instance._target = 0f;
        }

        /// <summary>立刻移除（不淡出）。換場景等硬中斷用。</summary>
        public static void EndImmediate()
        {
            if (Instance == null) return;
            Instance._target = 0f;
            Instance._amount = 0f;
            Instance.DetachBlit();
            if (SuspendAtmosphere) AtmosphereController.SetBypass(0f);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 與 EyeOpen / Mosaic / Atmosphere 同慣例：走 Resources.Load 而不是 Shader.Find
            // （Resources 底下的資產保證進 build、不會被 shader stripping 掃掉）。
            var sh = Resources.Load<Shader>("Shaders/MemoryFx");
            if (sh == null)
            {
                Debug.LogError("[MemoryFx] 找不到 Resources/Shaders/MemoryFx——" +
                               "確認 MemoryFx.shader 在 Assets/Resources/Shaders/ 底下。回憶特效停用。");
                return;
            }
            _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        // 換場景：相機換了一台，blit 要重掛；沒在跑就順手清乾淨（避免留下孤兒 blit）。
        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            _cam = null; _blit = null;
            if (_target > 0f) AttachBlit();
        }

        void AttachBlit()
        {
            if (_mat == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            if (_blit == null)
            {
                _blit = _cam.GetComponent<MemoryFxBlit>();
                if (_blit == null) _blit = _cam.gameObject.AddComponent<MemoryFxBlit>();
            }
            _blit.Material = _mat;
            _blit.enabled = true;
        }

        void DetachBlit()
        {
            if (_blit != null) _blit.enabled = false;
        }

        void Update()
        {
            if (_mat == null) return;

            // 用未縮放時間：劇情中途開對話面板會 Time.timeScale=0，淡入淡出照樣要走完。
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, FadeSeconds);
            _amount = Mathf.MoveTowards(_amount, _target, step);

            // 氛圍旁通與強度同步淡入淡出（含歸零那一幀，所以放在早退之前）。
            if (SuspendAtmosphere) AtmosphereController.SetBypass(_amount);

            if (_amount <= 0.001f && _target <= 0f) { DetachBlit(); return; }

            if (_cam == null || _blit == null) AttachBlit();

            _mat.SetFloat("_Amount", _amount);
            _mat.SetFloat("_Sepia", Sepia);
            _mat.SetFloat("_Desat", Desat);
            _mat.SetFloat("_VigStart", VigStart);
            _mat.SetFloat("_VigPower", VigPower);
            _mat.SetFloat("_BlurPx", BlurPx);
            _mat.SetFloat("_Grain", Grain);
            _mat.SetColor("_Tint", Tint);
            _mat.SetFloat("_Letterbox", Letterbox * _amount);
        }
    }

    /// <summary>掛在主相機上的全螢幕 Blit（同 AtmosphereBlit 範式）。</summary>
    [DisallowMultipleComponent]
    public class MemoryFxBlit : MonoBehaviour
    {
        public Material Material;

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (Material == null) { Graphics.Blit(src, dst); return; }
            Graphics.Blit(src, dst, Material);
        }
    }
}
