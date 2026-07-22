using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 馬賽克清晰（像素格由粗到細慢慢收斂）一次性後處理（Built-in 算繪管線）。自動生成、跨地圖常駐，仿 <see cref="IllusionShatterController"/>。
///
/// 經 <see cref="ScreenFxPlayer"/> 呼叫 <see cref="Play"/>（螢幕特效 id 3）。可用於：
///   - 劇情 screenFx 步驟（例：山道劇情亮起後、進場觸發對話前，畫面由馬賽克收斂成清晰）。
///   - 地圖 EnterEffect 欄（進圖播一次）。
///
/// 視覺全在 <c>Resources/Shaders/Mosaic.shader</c>（_Progress 0→1：粗格→細格→原圖）。
///
/// ★ 與睜眼／破幻術不同：本控制器**不自行暫停遊戲、不鎖輸入**。
///   因為它主要用在劇情內，而劇情已經用 SetExternalHold 鎖了輸入；SetExternalHold 是布林設定（非計數），
///   若這裡再自行 hold、播完解除，會把劇情的鎖一起解掉。所以暫停/鎖輸入交給呼叫端（劇情 lockInput）。
///   HUD 層的隱藏/復原一樣由 ScreenFxPlayer 統一處理，本控制器不碰。
/// </summary>
[DisallowMultipleComponent]
public class MosaicController : MonoBehaviour
{
    public static MosaicController Instance { get; private set; }

    /// <summary>馬賽克是否正在播放中（給等待用）。</summary>
    public static bool IsPlaying => Instance != null && Instance._playing;

    [Header("時間軸（秒，可調）")]
    [Tooltip("整段由馬賽克收斂到清晰的總長。")]
    public float duration = 2.0f;

    [Header("外觀（可調）")]
    [Tooltip("最粗時「垂直方向」的格子數（越小越粗）。")]
    public float minCells = 14f;
    [Tooltip("最細時的格子數（夠大≒原生解析度）。")]
    public float maxCells = 300f;

    // 收斂進度曲線（正規化時間 0..1 → _Progress）＋亮度斜坡（0..1，一開始暗、銜接黑幕）。
    AnimationCurve _progress, _bright;

    Material _mat;
    Camera _cam;
    MosaicBlit _blit;
    bool _playing;
    float _elapsed, _duration;
    System.Action _onDone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[Mosaic]");
        go.AddComponent<MosaicController>();
        DontDestroyOnLoad(go);
    }

    /// <summary>
    /// 播一次馬賽克收斂。<paramref name="onDone"/> 在整段播完（畫面已清晰、blit 已停）時呼叫。
    /// <paramref name="durationOverride"/> ≥ 0 覆寫總長。相機還沒好等罕見情況：直接呼叫 onDone（鏈照走、只是沒特效）。
    /// </summary>
    public static void Play(System.Action onDone, float durationOverride = -1f)
    {
        if (Instance == null) AutoSpawn();
        if (Instance == null) { onDone?.Invoke(); return; }
        Instance.PlayInternal(onDone, durationOverride);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Shader sh = Resources.Load<Shader>("Shaders/Mosaic");
        if (sh == null)
        {
            Debug.LogWarning("[Mosaic] 找不到 Resources/Shaders/Mosaic，馬賽克效果停用（鏈仍會往下走）。");
            enabled = false;
            return;
        }
        _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };

        // 前段停留在粗格（讓馬賽克感明顯），後段快速收斂成清晰。
        _progress = new AnimationCurve(
            new Keyframe(0.00f, 0.00f),
            new Keyframe(0.35f, 0.18f),   // 還很粗
            new Keyframe(0.75f, 0.62f),   // 開始明顯變細
            new Keyframe(1.00f, 1.00f));  // 完全清晰
        // 亮度斜坡：一開始近全黑（與劇情黑幕無縫銜接）→ 快速浮出暗色粗格 → 收斂時回到正常亮度。
        _bright = new AnimationCurve(
            new Keyframe(0.00f, 0.05f),   // 幾乎全黑，接住黑幕
            new Keyframe(0.22f, 0.45f),   // 暗色粗格浮現
            new Keyframe(0.70f, 0.85f),
            new Keyframe(1.00f, 1.00f));  // 正常亮度

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_mat != null) Destroy(_mat);
    }

    // 換場景：相機可能換新，清引用。若播放中被場景切換打斷，安全回呼（不卡住鏈）。
    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        _cam = null; _blit = null;
        if (_playing)
        {
            _playing = false;
            var cb = _onDone; _onDone = null;
            cb?.Invoke();   // 呼叫 onDone → ScreenFxPlayer 的包裝會復原 HUD
        }
    }

    void PlayInternal(System.Action onDone, float durationOverride)
    {
        if (_mat == null) { onDone?.Invoke(); return; }   // shader 掛了：直接往下走
        EnsureHooks();
        if (_blit == null) { onDone?.Invoke(); return; }  // 相機還沒好：不半套，直接往下走

        _onDone = onDone;
        _duration = durationOverride >= 0f ? durationOverride : duration;
        _elapsed = 0f;
        _playing = true;

        _mat.SetFloat("_MinCells", minCells);
        _mat.SetFloat("_MaxCells", maxCells);

        _blit.Material = _mat;
        _blit.enabled = true;
        Apply(0f);
        // 註：不做 SetExternalHold（見類別註解）。
    }

    void EnsureHooks()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        if (_blit == null || _blit.gameObject != _cam.gameObject)
        {
            _blit = _cam.GetComponent<MosaicBlit>();
            if (_blit == null) _blit = _cam.gameObject.AddComponent<MosaicBlit>();
        }
    }

    void LateUpdate()
    {
        if (!_playing) return;
        if (_blit == null) { EnsureHooks(); if (_blit != null) _blit.Material = _mat; }

        _elapsed += Time.unscaledDeltaTime;   // 用 unscaled：暫停(timeScale=0)也能播（劇情鎖輸入時通常沒暫停，但保險）
        float nt = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        Apply(nt);
        if (nt >= 1f) Finish();
    }

    void Apply(float nt)
    {
        if (_mat == null) return;
        _mat.SetFloat("_Progress", Mathf.Clamp01(_progress.Evaluate(nt)));
        _mat.SetFloat("_Bright", Mathf.Clamp01(_bright.Evaluate(nt)));
        _mat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
    }

    void Finish()
    {
        _playing = false;

        // 先接鏈（onDone），再停 blit——與其它過場一致的收尾順序。
        var cb = _onDone; _onDone = null;
        cb?.Invoke();   // 呼叫 onDone → ScreenFxPlayer 的包裝會復原 HUD

        if (_blit != null) { _blit.Material = null; _blit.enabled = false; }
    }
}

/// <summary>掛在主相機上，把畫面經馬賽克材質做一次全螢幕 Blit。由 MosaicController 自動加掛/停用。</summary>
[DisallowMultipleComponent]
public class MosaicBlit : MonoBehaviour
{
    public Material Material;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (Material == null) { Graphics.Blit(src, dst); return; }
        Graphics.Blit(src, dst, Material);
    }
}
