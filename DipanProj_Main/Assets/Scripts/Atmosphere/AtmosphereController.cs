using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 場景氛圍後處理（Built-in 算繪管線）。自動生成、跨地圖常駐，並把後處理掛到 Camera.main。
///
/// 由「地圖」驅動：每張地圖的氛圍型別寫在 MapsTable.csv 的 <c>Atmosphere</c> 欄，
/// MapManager 載圖時呼叫 <see cref="ApplyMapAtmosphere"/> 設定當前模式：
///   1 = 正常（不做任何處理，passthrough）——室外白天等場景用。
///   2 = 幽暗場景 + 打光（周邊提亮、看得到美術；中心暖周邊冷）。
///   3 = 噩夢場景 + 打光（周邊近全黑、統一冷色，最壓迫）。
///   4 = 烈日曝曬、5 = 焦土餘燼、6 = 沙塵暴（末日炎熱系，帶克制的熱浪扭曲）。
/// 視覺差異全在 <c>Resources/Shaders/Atmosphere.shader</c>（用 _Mode 切 2~6）。
///
/// 提燈光圈（type 2/3 共用）的玩家螢幕位置、半徑每幀餵給 shader，半徑以油燈式 Perlin 明滅輕微呼吸。
/// 後處理只作用在主相機算繪出的畫面；Screen Space Overlay 的 UI（HUD／面板）在其後合成，不會被壓暗。
/// </summary>
[DisallowMultipleComponent]
public class AtmosphereController : MonoBehaviour
{
    public static AtmosphereController Instance { get; private set; }

    // ── 提燈光圈可調參數（viewport 比例，以畫面「高度」為 1；type 2/3 共用）──
    private const float InnerRadius = 0.13f;   // 圓內全亮半徑
    private const float OuterRadius = 0.28f;   // 圓外到此完全壓到底亮
    private const float BreatheMin  = 0.93f;   // 呼吸時半徑最小倍率
    private const float BreatheMax  = 1.06f;   // 呼吸時半徑最大倍率

    private int _mode = 1;   // 當前氛圍型別（1=正常/off；2=幽暗；3=噩夢）。預設 off，等地圖設定。

    private Material _mat;
    private Camera _cam;
    private AtmosphereBlit _blit;
    private Transform _player;
    private float _seed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[Atmosphere]");
        go.AddComponent<AtmosphereController>();
        DontDestroyOnLoad(go);
    }

    /// <summary>由 MapManager 在載圖時呼叫，套用當前地圖的氛圍型別（MapsTable 的 Atmosphere 欄）。</summary>
    public static void ApplyMapAtmosphere(int type)
    {
        if (Instance == null) AutoSpawn();
        if (Instance != null) Instance._mode = type;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Shader sh = Resources.Load<Shader>("Shaders/Atmosphere");
        if (sh == null)
        {
            Debug.LogWarning("[Atmosphere] 找不到 Resources/Shaders/Atmosphere，氛圍後處理停用。");
            enabled = false;
            return;
        }
        _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        _seed = Random.value * 1000f;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_mat != null) Destroy(_mat);
    }

    // 換場景：相機／玩家可能換新，清掉引用、下一幀重新抓。（地圖型別不重置，由 MapManager 重新套用。）
    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        _cam = null;
        _player = null;
        _blit = null;
    }

    private void EnsureHooks()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;   // 相機可能還沒生出來（地圖載入中），下一幀再試

        if (_blit == null || _blit.gameObject != _cam.gameObject)
        {
            _blit = _cam.GetComponent<AtmosphereBlit>();
            if (_blit == null) _blit = _cam.gameObject.AddComponent<AtmosphereBlit>();
        }

        if (_player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }
    }

    private void LateUpdate()
    {
        EnsureHooks();
        if (_cam == null || _blit == null || _mat == null) return;

        // type 1（正常）：不處理，材質設 null → AtmosphereBlit 直接 passthrough。
        if (_mode <= 1)
        {
            _blit.Material = null;
            return;
        }

        _blit.Material = _mat;
        _mat.SetFloat("_Mode", _mode);   // 2 或 3，shader 依此切換外觀

        // 油燈式呼吸：Perlin 慢漂為主 + 一點慢正弦，輕微調整光圈半徑。
        float t = Time.time;
        float slow = Mathf.PerlinNoise(_seed, t * 0.55f);
        float fast = Mathf.Sin(t * 3.2f + _seed) * 0.5f + 0.5f;
        float mix  = Mathf.Clamp01(0.85f * slow + 0.15f * fast);
        float breathe = Mathf.Lerp(BreatheMin, BreatheMax, mix);

        // 玩家螢幕位置（viewport 0..1）；找不到玩家就退回畫面中心。
        Vector2 vp = new Vector2(0.5f, 0.5f);
        if (_player != null)
        {
            Vector3 v = _cam.WorldToViewportPoint(_player.position);
            vp = new Vector2(v.x, v.y);
        }

        _mat.SetVector("_PlayerPos", new Vector4(vp.x, vp.y, 0f, 0f));
        _mat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
        _mat.SetFloat("_InnerR", InnerRadius * breathe);
        _mat.SetFloat("_OuterR", OuterRadius * breathe);
    }
}
