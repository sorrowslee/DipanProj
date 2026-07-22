using UnityEngine;
using UnityEngine.SceneManagement;
using Dipan.UI;

/// <summary>
/// 進場「睜眼醒來」一次性後處理（Built-in 算繪管線）。自動生成、跨地圖常駐。
///
/// 由「地圖」驅動、但**只在進圖時播一次就結束**（與持續性的 AtmosphereController / SceneEffectController 不同）：
/// 每張地圖的進場效果寫在 MapsTable.csv 的 <c>EnterEffect</c> 欄，MapManager 進圖時呼叫
/// <see cref="ApplyMapEnterEffect"/>；type = 1（睜眼）就播一次時間軸：
///   全黑(承接墜落昏迷) → 眼皮裂開一條縫 → 沉重眨一下 → 逐漸對焦(模糊轉清晰)、亮度回正、完全睜開 → 移除。
///
/// 視覺全在 <c>Resources/Shaders/EyeOpen.shader</c>（眼皮/杏眼遮罩 + 模糊 + 亮度暗角），
/// 播放中把 <see cref="EyeOpenBlit"/> 掛到 Camera.main 做全螢幕後處理，播完停用該 blit 恢復正常畫面。
/// 後處理只作用在主相機畫面；Screen Space Overlay 的 UI 在其後合成，不受影響。
/// 見 readme/MAP_ENTER_EFFECT.md。
/// </summary>
[DisallowMultipleComponent]
public class EyeOpenController : MonoBehaviour
{
    public static EyeOpenController Instance { get; private set; }

    /// <summary>進場效果是否正在播放中（給「進場觸發」等系統等待用：效果播完才觸發，避免對話蓋在睜眼上）。</summary>
    public static bool IsPlaying => Instance != null && Instance._playing;

    [Header("時間軸（秒，可調）")]
    [Tooltip("整段睜眼過場的總長。")]
    public float duration = 4.0f;
    [Tooltip("剛醒時的最大模糊半徑（UV 單位；越大越糊）。")]
    public float maxBlur = 0.18f;

    // 三條曲線（正規化時間 0..1 → 參數）。眨眼藏在 _open 曲線的關鍵影格裡。
    AnimationCurve _open, _bright, _blur;

    Material _mat;
    Camera _cam;
    EyeOpenBlit _blit;
    bool _playing;
    float _elapsed;
    System.Action _onDone;
    float _duration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[EyeOpen]");
        go.AddComponent<EyeOpenController>();
        DontDestroyOnLoad(go);
    }

    /// <summary>由 MapManager 在進圖時呼叫。type = 1 → 播一次睜眼；其他 → 不做事（不打斷進行中的播放）。</summary>
    public static void ApplyMapEnterEffect(int type)
    {
        if (Instance == null) AutoSpawn();
        if (Instance == null) return;
        if (type == 1) Instance.PlayInternal(null, -1f);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Shader sh = Resources.Load<Shader>("Shaders/EyeOpen");
        if (sh == null)
        {
            Debug.LogWarning("[EyeOpen] 找不到 Resources/Shaders/EyeOpen，睜眼效果停用。");
            enabled = false;
            return;
        }
        _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };

        _open = new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.10f, 0f),      // 承接全黑、停一下
            new Keyframe(0.30f, 0.42f),   // 眼皮裂開一條縫（露出模糊偏暗的洞窟）
            new Keyframe(0.42f, 0.12f),   // 沉重眨一下（闔回）
            new Keyframe(0.58f, 0.50f),   // 再睜開
            new Keyframe(1.00f, 1.00f));  // 完全睜開
        _bright = new AnimationCurve(
            new Keyframe(0.00f, 0.12f),
            new Keyframe(0.30f, 0.35f),
            new Keyframe(1.00f, 1.00f));
        _blur = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.50f, 0.70f),
            new Keyframe(1.00f, 0.00f));

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_mat != null) Destroy(_mat);
    }

    // 換場景：相機可能換新，清引用、下次 EnsureHooks 重抓。若正在播放被場景切換打斷，安全解除暫停/擋操作。
    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        _cam = null; _blit = null;
        if (_playing)
        {
            _playing = false;
            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(false, false);
            var cb = _onDone; _onDone = null;
            cb?.Invoke();   // 呼叫 onDone → ScreenFxPlayer 的包裝會復原 HUD
        }
    }

    /// <summary>統一分派入口（給 ScreenFxPlayer；睜眼＝螢幕特效 id 1）：播一次睜眼，播完呼叫 onDone。durationOverride ≥ 0 覆寫總長。</summary>
    public static void Play(System.Action onDone, float durationOverride = -1f)
    {
        if (Instance == null) AutoSpawn();
        if (Instance == null) { onDone?.Invoke(); return; }
        Instance.PlayInternal(onDone, durationOverride);
    }

    void PlayInternal(System.Action onDone, float durationOverride)
    {
        if (_mat == null) { onDone?.Invoke(); return; }
        EnsureHooks();
        if (_blit == null) { onDone?.Invoke(); return; }   // 相機還沒好（少見）；不強播，避免半套
        _onDone = onDone;
        _duration = durationOverride >= 0f ? durationOverride : duration;
        _elapsed = 0f;
        _playing = true;
        _mat.SetFloat("_Feather", 0.06f);
        _blit.Material = _mat;
        _blit.enabled = true;
        Apply(0f);

        // 播放中：暫停遊戲 + 擋玩家操作（接既有 UIManager 的暫停/輸入閘門，播完在 Finish 解除）。
        if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(true, true);
    }

    void EnsureHooks()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        if (_blit == null || _blit.gameObject != _cam.gameObject)
        {
            _blit = _cam.GetComponent<EyeOpenBlit>();
            if (_blit == null) _blit = _cam.gameObject.AddComponent<EyeOpenBlit>();
        }
    }

    void LateUpdate()
    {
        if (!_playing) return;
        if (_blit == null) { EnsureHooks(); if (_blit != null) _blit.Material = _mat; }

        _elapsed += Time.unscaledDeltaTime;   // 用 unscaled：即使暫停(timeScale=0)也能播
        float nt = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        Apply(nt);
        if (nt >= 1f) Finish();
    }

    void Apply(float nt)
    {
        if (_mat == null) return;
        _mat.SetFloat("_Open", Mathf.Clamp01(_open.Evaluate(nt)));
        _mat.SetFloat("_Bright", Mathf.Clamp01(_bright.Evaluate(nt)));
        _mat.SetFloat("_Blur", Mathf.Max(0f, _blur.Evaluate(nt)) * maxBlur);
        _mat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
    }

    void Finish()
    {
        _playing = false;
        if (_blit != null) { _blit.Material = null; _blit.enabled = false; }   // 關掉效果、恢復正常畫面
        if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(false, false);   // 解除暫停/擋操作
        var cb = _onDone; _onDone = null;
        cb?.Invoke();   // 呼叫 onDone → ScreenFxPlayer 的包裝會復原 HUD
    }
}

/// <summary>掛在主相機上，把畫面經睜眼材質做一次全螢幕 Blit。由 EyeOpenController 自動加掛/停用。</summary>
[DisallowMultipleComponent]
public class EyeOpenBlit : MonoBehaviour
{
    public Material Material;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (Material == null) { Graphics.Blit(src, dst); return; }
        Graphics.Blit(src, dst, Material);
    }
}
