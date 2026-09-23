using UnityEngine;
using UnityEngine.SceneManagement;
using Dipan.UI;

/// <summary>
/// 淡出黑幕（螢幕特效 id 4，2026-09-23）：畫面平順淡成全黑，播完才接鏈。
/// 新手夢境教學的收尾用：佛掌壓到玩家、drama 43 關掉 → 淡出 → teleportTo 13 回山道。
///
/// <para>作者先後試過漩渦、黑暗吞噬、破幻術切細、三條直裂痕、照片式碎鏡，最後拍板「做簡單的淡出就好」，那些都已移除。</para>
///
/// <para>★ 播放中**暫停遊戲＋鎖輸入**（具名持有者，見 PROBLEMS D13）。
/// ★ 收尾：先接鏈（teleportTo 同一幀開讀取頁），再**維持全黑 3 幀**才停 blit，讀取頁晚一幀出來也不會閃回場景。
/// ★ 所有結束路徑（播完、被換場景打斷）都會解鎖並呼叫 onDone，HUD 才會被 ScreenFxPlayer 復原。</para>
/// </summary>
[DisallowMultipleComponent]
public class FadeOutController : MonoBehaviour
{
    public static FadeOutController Instance { get; private set; }
    public static bool IsPlaying => Instance != null && Instance._playing;

    const string HoldOwner = "FadeOut";

    [Tooltip("淡到全黑要幾秒（ScreenFxTable 有填就以表為準）。")]
    public float duration = 1.5f;

    Material _mat;
    Camera _cam;
    FadeOutBlit _blit;
    bool _playing;
    int _blackTail;
    float _elapsed, _duration;
    System.Action _onDone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[FadeOut]");
        go.AddComponent<FadeOutController>();
        DontDestroyOnLoad(go);
    }

    /// <summary>播一次。<paramref name="onDone"/> 在畫面全黑時呼叫。shader／相機不在時直接呼叫 onDone（鏈照走）。</summary>
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

        Shader sh = Resources.Load<Shader>("Shaders/ScreenFadeOut");
        if (sh == null)
        {
            Debug.LogWarning("[FadeOut] 找不到 Resources/Shaders/ScreenFadeOut，淡出停用（鏈仍會往下走）。");
            enabled = false;
            return;
        }
        _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_mat != null) Destroy(_mat);
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        _cam = null; _blit = null; _blackTail = 0;
        if (_playing)
        {
            _playing = false;
            UIManager.Instance?.SetExternalHold(HoldOwner, false, false);
            var cb = _onDone; _onDone = null;
            cb?.Invoke();
        }
    }

    void PlayInternal(System.Action onDone, float durationOverride)
    {
        if (_mat == null) { onDone?.Invoke(); return; }
        _blackTail = 0;
        if (_cam == null) _cam = Camera.main;
        if (_cam != null && (_blit == null || _blit.gameObject != _cam.gameObject))
        {
            _blit = _cam.GetComponent<FadeOutBlit>();
            if (_blit == null) _blit = _cam.gameObject.AddComponent<FadeOutBlit>();
        }
        if (_blit == null) { onDone?.Invoke(); return; }

        _onDone = onDone;
        _duration = durationOverride > 0f ? durationOverride : duration;
        _elapsed = 0f;
        _playing = true;
        _mat.SetFloat("_Fade", 0f);
        _blit.Material = _mat;
        _blit.enabled = true;
        UIManager.Instance?.SetExternalHold(HoldOwner, true, true);   // 暫停＋鎖輸入（具名持有者）
    }

    void LateUpdate()
    {
        if (_blackTail > 0)
        {
            if (--_blackTail == 0 && _blit != null) { _blit.Material = null; _blit.enabled = false; }
            return;
        }
        if (!_playing) return;

        _elapsed += Time.unscaledDeltaTime;   // 暫停中也要播
        float nt = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        _mat.SetFloat("_Fade", nt * nt * (3f - 2f * nt));
        if (nt >= 1f) Finish();
    }

    void Finish()
    {
        _playing = false;
        UIManager.Instance?.SetExternalHold(HoldOwner, false, false);
        _mat.SetFloat("_Fade", 1f);

        // 先接鏈（teleportTo 同一幀開讀取頁），再維持全黑 3 幀才停 blit（見類別註解）。
        var cb = _onDone; _onDone = null;
        cb?.Invoke();
        _blackTail = 3;
    }
}

/// <summary>掛在主相機上，把畫面經淡出材質做一次全螢幕 Blit。由 FadeOutController 自動加掛/停用。</summary>
[DisallowMultipleComponent]
public class FadeOutBlit : MonoBehaviour
{
    public Material Material;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (Material == null) { Graphics.Blit(src, dst); return; }
        Graphics.Blit(src, dst, Material);
    }
}
