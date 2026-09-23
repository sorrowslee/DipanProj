using UnityEngine;
using UnityEngine.SceneManagement;
using Dipan.UI;

/// <summary>
/// 馬賽克淡出（螢幕特效 id 5，2026-09-23）：<see cref="MosaicController"/>（id 3「馬賽克清晰」）的**倒放**——
/// 清晰畫面 → 格子越來越粗 → 同時變暗 → 最後停在全黑，播完才接鏈。
/// 新手夢境教學收尾用（drama 43 關掉 → 馬賽克淡出 → teleportTo 13 回山道）：夢境開頭是「馬賽克收斂成清晰」展開的，
/// 結尾倒過來「清晰碎成馬賽克」收掉，首尾呼應。
///
/// <para>**直接共用** <c>Resources/Shaders/Mosaic.shader</c>（_Progress 1＝清晰、0＝最粗；_Bright 1＝正常、0＝全黑），
/// shader 一行都沒改，只是把兩條曲線倒過來跑。</para>
///
/// <para>★ 與馬賽克清晰不同：本控制器**自己暫停遊戲＋鎖輸入**（具名持有者 <c>MosaicOut</c>，不會解掉別人的鎖，見 PROBLEMS D13）——
/// 它是鏈動作 playScreenFx 直接播的，前面沒有劇情幫忙鎖。
/// ★ 收尾：先接鏈（teleportTo 同一幀開讀取頁），再**維持全黑 3 幀**才停 blit，讀取頁晚一幀出來也不會閃回場景。
/// ★ 所有結束路徑（播完、被換場景打斷）都會解鎖並呼叫 onDone，HUD 才會被 ScreenFxPlayer 復原。</para>
/// </summary>
[DisallowMultipleComponent]
public class MosaicOutController : MonoBehaviour
{
    public static MosaicOutController Instance { get; private set; }
    public static bool IsPlaying => Instance != null && Instance._playing;

    const string HoldOwner = "MosaicOut";

    [Header("時間軸（秒；ScreenFxTable 有填就以表為準）")]
    public float duration = 2.0f;

    [Header("外觀（與馬賽克清晰同一組意義）")]
    [Tooltip("最粗時「垂直方向」的格子數（越小越粗）。")]
    public float minCells = 14f;
    [Tooltip("最細時的格子數（夠大≒原生解析度）。")]
    public float maxCells = 300f;

    AnimationCurve _progress, _bright;
    Material _mat;
    Camera _cam;
    MosaicOutBlit _blit;
    bool _playing;
    int _blackTail;
    float _elapsed, _duration;
    System.Action _onDone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[MosaicOut]");
        go.AddComponent<MosaicOutController>();
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

        Shader sh = Resources.Load<Shader>("Shaders/Mosaic");
        if (sh == null)
        {
            Debug.LogWarning("[MosaicOut] 找不到 Resources/Shaders/Mosaic，馬賽克淡出停用（鏈仍會往下走）。");
            enabled = false;
            return;
        }
        _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };

        // 馬賽克清晰的倒放：一開始很快就碎成明顯的格子（不然前半秒像沒事發生），之後慢慢變得更粗。
        _progress = new AnimationCurve(          // shader 的 _Progress：1＝清晰、0＝最粗
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.25f, 0.62f),
            new Keyframe(0.65f, 0.18f),
            new Keyframe(1.00f, 0.00f));
        _bright = new AnimationCurve(            // 前半段維持亮度（看得清楚在碎），後半段沉到全黑
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.45f, 0.90f),
            new Keyframe(0.80f, 0.35f),
            new Keyframe(1.00f, 0.00f));

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
            _blit = _cam.GetComponent<MosaicOutBlit>();
            if (_blit == null) _blit = _cam.gameObject.AddComponent<MosaicOutBlit>();
        }
        if (_blit == null) { onDone?.Invoke(); return; }

        _onDone = onDone;
        _duration = durationOverride > 0f ? durationOverride : duration;
        _elapsed = 0f;
        _playing = true;
        _mat.SetFloat("_MinCells", minCells);
        _mat.SetFloat("_MaxCells", maxCells);
        Apply(0f);
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
        Apply(nt);
        if (nt >= 1f) Finish();
    }

    void Apply(float nt)
    {
        _mat.SetFloat("_Progress", Mathf.Clamp01(_progress.Evaluate(nt)));
        _mat.SetFloat("_Bright", Mathf.Clamp01(_bright.Evaluate(nt)));
        _mat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
    }

    void Finish()
    {
        _playing = false;
        UIManager.Instance?.SetExternalHold(HoldOwner, false, false);
        _mat.SetFloat("_Bright", 0f);

        // 先接鏈（teleportTo 同一幀開讀取頁），再維持全黑 3 幀才停 blit（見類別註解）。
        var cb = _onDone; _onDone = null;
        cb?.Invoke();
        _blackTail = 3;
    }
}

/// <summary>掛在主相機上，把畫面經馬賽克材質做一次全螢幕 Blit。由 MosaicOutController 自動加掛/停用。</summary>
[DisallowMultipleComponent]
public class MosaicOutBlit : MonoBehaviour
{
    public Material Material;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (Material == null) { Graphics.Blit(src, dst); return; }
        Graphics.Blit(src, dst, Material);
    }
}
