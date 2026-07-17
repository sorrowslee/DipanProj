using UnityEngine;
using UnityEngine.SceneManagement;
using Dipan.UI;

/// <summary>
/// 破幻術（幻境崩碎回歸現實）一次性後處理（Built-in 算繪管線）。自動生成、跨地圖常駐，仿 <see cref="EyeOpenController"/>。
///
/// 與睜眼醒來（EnterEffect，綁地圖、進圖播）不同：本效果是**觸發鏈的動作**——由 `playScreenFx` trigger（螢幕特效 id 1）
/// 經 <see cref="ScreenFxPlayer"/> 呼叫 <see cref="Play"/>，在「還在幻境場景時」播放；玩家親眼看到當前場景（＝紅嫁衣婚境）
/// 龜裂崩碎、收尾成全白，播完再由鏈接 teleportTo 傳去現實（榕樹妖）。整段暫停遊戲＋擋操作（用未縮放時間照播）。
///
/// 視覺全在 <c>Resources/Shaders/IllusionShatter.shader</c>（voronoi 裂紋 + 碎塊崩落色散 + 白光）。
/// 播放中把 <see cref="IllusionShatterBlit"/> 掛到 Camera.main 做全螢幕後處理，播完停用。
/// 見 readme/MAP_ENTER_EFFECT.md（離場螢幕特效一節）、readme/TRIGGER_CHAIN.md（playScreenFx 鏈動作，§3）。
/// </summary>
[DisallowMultipleComponent]
public class IllusionShatterController : MonoBehaviour
{
    public static IllusionShatterController Instance { get; private set; }

    /// <summary>破幻術是否正在播放中（給等待用）。</summary>
    public static bool IsPlaying => Instance != null && Instance._playing;

    [Header("時間軸（秒，可調）")]
    [Tooltip("整段崩碎過場的總長。")]
    public float duration = 2.2f;

    [Header("外觀（可調；auto-spawn 時用這些預設）")]
    public float density = 11f;      // voronoi 密度（碎塊數量感）
    public float maxDisplace = 0.55f; // 碎塊最大飛離距離（uv）
    public float maxSpin = 1.2f;     // 碎塊最大翻轉角（弧度）
    public float chromatic = 0.02f;  // 色散強度
    public float voidBright = 1.0f;  // 露出的白光亮度
    public float tintStrength = 0.35f; // 幻術暖色濃度
    public Color tintColor = new Color(1.0f, 0.86f, 0.72f, 1f);

    // 兩條曲線（正規化時間 0..1 → 參數）。
    AnimationCurve _progress, _crack;

    Material _mat;
    Camera _cam;
    IllusionShatterBlit _blit;
    bool _playing;
    float _elapsed, _duration;
    System.Action _onDone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[IllusionShatter]");
        go.AddComponent<IllusionShatterController>();
        DontDestroyOnLoad(go);
    }

    /// <summary>
    /// 播一次破幻術。<paramref name="onDone"/> 在整段播完（畫面已全白、外部 hold 已解除）時呼叫——
    /// 觸發鏈在這裡接 next（通常＝teleportTo 傳去現實）。
    /// <paramref name="durationOverride"/> ≥ 0 覆寫總長（給編輯器 trigger 的 duration 欄）。
    /// 相機還沒好等罕見情況無法掛效果時，直接呼叫 onDone（鏈照常往下走，只是沒特效）。
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

        Shader sh = Resources.Load<Shader>("Shaders/IllusionShatter");
        if (sh == null)
        {
            Debug.LogWarning("[IllusionShatter] 找不到 Resources/Shaders/IllusionShatter，破幻術效果停用（鏈仍會往下走）。");
            enabled = false;
            return;
        }
        _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };

        // 進度：前段慢（讓裂紋先鋪開），後段加速崩碎。
        _progress = new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.28f, 0.10f),   // 定格＋裂紋擴散
            new Keyframe(0.60f, 0.48f),   // 開始崩落
            new Keyframe(1.00f, 1.00f));  // 全白
        // 裂紋亮線：崩碎前段最亮，隨碎塊飛走淡出。
        _crack = new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.22f, 1.00f),
            new Keyframe(0.55f, 0.45f),
            new Keyframe(1.00f, 0f));

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_mat != null) Destroy(_mat);
    }

    // 換場景：相機可能換新，清引用。若播放中被場景切換打斷，安全解除暫停/擋操作並回呼（不卡住鏈）。
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

    void PlayInternal(System.Action onDone, float durationOverride)
    {
        if (_mat == null) { onDone?.Invoke(); return; }   // shader 掛了：直接往下走
        EnsureHooks();
        if (_blit == null) { onDone?.Invoke(); return; }  // 相機還沒好：不半套，直接往下走

        _onDone = onDone;
        _duration = durationOverride >= 0f ? durationOverride : duration;
        _elapsed = 0f;
        _playing = true;

        // 一次性把外觀參數推進材質。
        _mat.SetFloat("_Density", density);
        _mat.SetFloat("_MaxDisp", maxDisplace);
        _mat.SetFloat("_MaxSpin", maxSpin);
        _mat.SetFloat("_CA", chromatic);
        _mat.SetFloat("_VoidBright", voidBright);
        _mat.SetFloat("_TintStrength", tintStrength);
        _mat.SetColor("_TintColor", tintColor);

        _blit.Material = _mat;
        _blit.enabled = true;
        Apply(0f);

        // 播放中：暫停遊戲 + 擋玩家操作（未縮放時間照播；Finish 解除）。
        // 註：HUD 層的隱藏/復原由 ScreenFxPlayer 統一處理（所有全螢幕過場共用），本控制器不碰。
        if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(true, true);
    }

    void EnsureHooks()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        if (_blit == null || _blit.gameObject != _cam.gameObject)
        {
            _blit = _cam.GetComponent<IllusionShatterBlit>();
            if (_blit == null) _blit = _cam.gameObject.AddComponent<IllusionShatterBlit>();
        }
    }

    void LateUpdate()
    {
        if (!_playing) return;
        if (_blit == null) { EnsureHooks(); if (_blit != null) _blit.Material = _mat; }

        _elapsed += Time.unscaledDeltaTime;   // 用 unscaled：暫停(timeScale=0)也能播
        float nt = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        Apply(nt);
        if (nt >= 1f) Finish();
    }

    void Apply(float nt)
    {
        if (_mat == null) return;
        _mat.SetFloat("_Progress", Mathf.Clamp01(_progress.Evaluate(nt)));
        _mat.SetFloat("_Crack", Mathf.Clamp01(_crack.Evaluate(nt)));
        _mat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
    }

    void Finish()
    {
        _playing = false;

        // 解除暫停/擋操作。
        if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(false, false);

        // 先接鏈（onDone → teleportTo → GoToMap 會同步開載入頁蓋住畫面），再停掉 blit ——
        // 這樣停 blit 時載入頁（或已建好的新圖）已在上面，不會閃回幻境畫面。
        var cb = _onDone; _onDone = null;
        cb?.Invoke();   // 呼叫 onDone → ScreenFxPlayer 的包裝會在此時（載入頁已蓋上）復原 HUD

        if (_blit != null) { _blit.Material = null; _blit.enabled = false; }
    }
}

/// <summary>掛在主相機上，把畫面經破幻術材質做一次全螢幕 Blit。由 IllusionShatterController 自動加掛/停用。</summary>
[DisallowMultipleComponent]
public class IllusionShatterBlit : MonoBehaviour
{
    public Material Material;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (Material == null) { Graphics.Blit(src, dst); return; }
        Graphics.Blit(src, dst, Material);
    }
}
