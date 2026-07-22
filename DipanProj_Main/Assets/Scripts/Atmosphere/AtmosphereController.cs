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
///   7 = 淺海、8 = 深海、9 = 深海+恐怖（海洋系，帶水下折射晃動；9 再套潛水燈光圈）。
///   10 = 風雪（陰冷暴風：冷灰調 + 翻騰白霧 + 不規則橫向風絲）；11 = 強風（去白霧、只留斜向風絲）；
///   12 = 綿綿細雨／毛毛雨（＝大雨的半速半密度）；13 = 大雨（細密雨點往下落）；
///   14 = 陰森森林鬼霧（畫面偏暗、陰綠冷調 + 漂移黑霧、偶爾一陣濃）；
///   15 = 電視雜訊（雪花 + 掃描線 + 滾動同步條 + 偶發水平撕裂 + 灰調閃爍）。
/// 視覺差異全在 <c>Resources/Shaders/Atmosphere.shader</c>（用 _Mode 切 2~15）。
///
/// 提燈光圈（type 2/3 共用）的玩家螢幕位置、半徑每幀餵給 shader，半徑以油燈式 Perlin 明滅輕微呼吸。
/// 後處理只作用在主相機算繪出的畫面；Screen Space Overlay 的 UI（HUD／面板）在其後合成，不會被壓暗。
/// </summary>
[DisallowMultipleComponent]
public class AtmosphereController : MonoBehaviour
{
    public static AtmosphereController Instance { get; private set; }

    // ── 提燈光圈可調參數 ──
    // 光圈大小改成「由光源的發光半徑(世界單位)決定」——玩家身上發光裝的最大半徑，或最近的發光地上物。
    // 沒有任何光源 → 把中心推出畫面，整片壓到「圈外」暗度（暗氛圍下沒燈就全暗）。
    private const float InnerOuterRatio = 0.46f;  // 內圈(全亮)半徑 = 外圈半徑 × 此比例（沿用原本 0.13/0.28 的比例）
    private const float BreatheMin  = 0.93f;      // 呼吸時半徑最小倍率
    private const float BreatheMax  = 1.06f;      // 呼吸時半徑最大倍率
    private const float FallbackOuterVp = 0.28f;  // 相機非正交時的後備外圈（viewport 比例）
    // 掃裝備欄取最大發光半徑用（靜態陣列，避免每幀 Enum.GetValues 產生 GC）。
    private static readonly Dipan.Inventory.EquipSlot[] EquipSlots =
    {
        Dipan.Inventory.EquipSlot.Weapon, Dipan.Inventory.EquipSlot.Chest, Dipan.Inventory.EquipSlot.Boots,
        Dipan.Inventory.EquipSlot.Gloves, Dipan.Inventory.EquipSlot.Amulet, Dipan.Inventory.EquipSlot.Ring,
    };

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
        _mat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));

        // 油燈式呼吸：Perlin 慢漂為主 + 一點慢正弦，輕微調整光圈半徑。
        float t = Time.time;
        float slow = Mathf.PerlinNoise(_seed, t * 0.55f);
        float fast = Mathf.Sin(t * 3.2f + _seed) * 0.5f + 0.5f;
        float mix  = Mathf.Clamp01(0.85f * slow + 0.15f * fast);
        float breathe = Mathf.Lerp(BreatheMin, BreatheMax, mix);

        // 決定「單一光源」：玩家身上發光裝(最大半徑)→玩家；否則最近的發光地上物；都沒有→無光源。
        if (ResolveLightSource(out Vector3 srcWorld, out float srcWorldR))
        {
            Vector3 v = _cam.WorldToViewportPoint(srcWorld);
            float outerVp = OuterViewportFromWorld(srcWorldR);
            _mat.SetVector("_PlayerPos", new Vector4(v.x, v.y, 0f, 0f));
            _mat.SetFloat("_OuterR", outerVp * breathe);
            _mat.SetFloat("_InnerR", outerVp * InnerOuterRatio * breathe);
        }
        else
        {
            // 沒有光源：把中心推出畫面 → 整片壓到「圈外」暗度（暗氛圍下沒燈就全暗）。
            _mat.SetVector("_PlayerPos", new Vector4(-10f, -10f, 0f, 0f));
            _mat.SetFloat("_OuterR", 0.002f);
            _mat.SetFloat("_InnerR", 0.001f);
        }
    }

    // 目前的光圈中心與世界半徑。玩家有發光裝＝以玩家為心；否則取最近的發光地上物；都沒有回 false。
    private bool ResolveLightSource(out Vector3 world, out float worldRadius)
    {
        world = default; worldRadius = 0f;

        float pr = PlayerEquippedLightRadius();
        if (pr > 0f && _player != null) { world = _player.position; worldRadius = pr; return true; }

        Vector3 from = _player != null ? _player.position : _cam.transform.position;
        var nearest = LightSource.Nearest(from);
        if (nearest != null) { world = nearest.transform.position; worldRadius = nearest.radius; return true; }

        return false;
    }

    // 玩家 6 個裝備欄裡最大的發光半徑（沒有發光裝 = 0）。
    private static float PlayerEquippedLightRadius()
    {
        var inv = Dipan.Inventory.InventorySystem.Instance;
        if (inv == null) return 0f;
        float max = 0f;
        for (int i = 0; i < EquipSlots.Length; i++)
        {
            int id = inv.GetEquipped(EquipSlots[i]);
            if (id <= 0) continue;
            var d = inv.GetData(id);
            if (d != null && d.LightRadius > max) max = d.LightRadius;
        }
        return max;
    }

    // 世界半徑 → viewport 外圈比例（以畫面高度為 1）。正交相機：viewport 高 = 2 × orthographicSize 世界單位。
    private float OuterViewportFromWorld(float worldRadius)
    {
        if (_cam != null && _cam.orthographic && _cam.orthographicSize > 0.01f)
            return worldRadius / (2f * _cam.orthographicSize);
        return FallbackOuterVp;   // 非正交相機（理論上不會走到）：退回固定比例
    }
}
