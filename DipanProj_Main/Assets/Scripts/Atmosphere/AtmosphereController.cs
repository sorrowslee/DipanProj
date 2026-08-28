using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 場景氛圍後處理（Built-in 算繪管線）。自動生成、跨地圖常駐，並把後處理掛到 Camera.main。
///
/// 由「地圖」驅動：每張地圖的氛圍型別寫在 MapsTable.csv 的 <c>Atmosphere</c> 欄，
/// MapManager 載圖時呼叫 <see cref="ApplyMapAtmosphere"/> 設定當前模式：
///   1 = 正常（不做處理，passthrough）——室外白天等場景用。
///       ※ 但若該地圖的「環境亮度」(MapsTable 的 EnvBright 欄) 小於 100，會改走 shader 的 mode 1 分支：
///         整張圖壓暗到指定亮度，場上的燈把周圍照回來 → 這就是「室內昏暗走廊，火把提供氣氛光」的用法。
///   2 = 幽暗場景 + 打光（周邊提亮、看得到美術；中心暖周邊冷）。
///   3 = 噩夢場景 + 打光（周邊近全黑、統一冷色，最壓迫）。
///   4 = 烈日曝曬、5 = 焦土餘燼、6 = 沙塵暴（末日炎熱系，帶克制的熱浪扭曲）。
///   7 = 淺海、8 = 深海、9 = 深海+恐怖（海洋系，帶水下折射晃動；9 再套潛水燈光圈）。
///   10 = 風雪（陰冷暴風：冷灰調 + 翻騰白霧 + 不規則橫向風絲）；11 = 強風（去白霧、只留斜向風絲）；
///   12 = 綿綿細雨／毛毛雨（＝大雨的半速半密度）；13 = 大雨（細密雨點往下落）；
///   14 = 陰森森林鬼霧（畫面偏暗、陰綠冷調 + 漂移黑霧、偶爾一陣濃）；
///   15 = 電視雜訊（雪花 + 掃描線 + 滾動同步條 + 偶發水平撕裂 + 灰調閃爍）。
/// 視覺差異全在 <c>Resources/Shaders/Atmosphere.shader</c>（用 _Mode 切 1~15）。
///
/// ── 照明（2026-08-10 改多光源）──
/// 光源有兩種來源，兩者一視同仁塞進同一份清單餵給 shader：
///   ① 玩家身上的發光裝（ItemTable 的 LightRadius，取所有裝備欄最大值）→ 以玩家為心。
///   ② 場上的發光地上物（<see cref="LightSource"/>，由 MapLoader 依編輯器「發光半徑」掛）→ 火把/燈籠/香爐。
/// 每幀取離玩家最近的 <see cref="MaxLights"/> 盞，換算成 viewport 座標與半徑餵進 shader 陣列。
/// 每盞燈各自做油燈式呼吸（自己的亂數種子，不會整場同步）。完全沒有光源 → 暗氛圍下就是全暗。
///
/// 後處理只作用在主相機算繪出的畫面；Screen Space Overlay 的 UI（HUD／面板）在其後合成，不會被壓暗。
/// </summary>
[DisallowMultipleComponent]
public class AtmosphereController : MonoBehaviour
{
    public static AtmosphereController Instance { get; private set; }

    /// <summary>同框光源數上限。必須與 Atmosphere.shader 裡的 MAX_LIGHTS 完全一致（改一邊要改另一邊）。</summary>
    public const int MaxLights = 12;

    // ── 光圈可調參數 ──
    // 光圈大小由「光源的發光半徑(世界單位)」決定，換算成 viewport 比例後餵給 shader。
    private const float DefaultSoftness = 0.46f;  // 內圈(全亮)半徑 = 外圈半徑 × 此比例（沿用原本 0.13/0.28 的比例）
    private const float FallbackOuterVp = 0.28f;  // 相機非正交時的後備外圈（viewport 比例）

    // ── 玩家常駐體光（美術紀律二：主角永遠看得見自己）──
    // 沒有任何發光裝時，暗場景（2/3/9、或 EnvBright<100）給玩家一圈「快熄的香頭」等級微光：
    // 只照出自己身體的輪廓，照不了路——刻意弱於一個身位，**不能破壞柴房教學「要點燈才看得見路」的壓力**。
    // 嫌太亮/太暗改這兩個數即可（半徑=世界格、亮度=1 為標準火把）。
    private const float PlayerAmbientRadius    = 1.2f;
    private const float PlayerAmbientIntensity = 0.35f;
    private const float PlayerAmbientSoftness  = 0.30f;   // 比預設更瀰漫的柔邊（內圈小）
    /// <summary>體光光色＝偏中性的微暖白——刻意跟火把/燈籠的暖橘（DefaultWarm）區隔：
    /// 體光是「角色可讀性」不是「一盞燈」，用中性色角色不會看起來像自帶燈籠（GPT 美術回饋 2026-08-28）。</summary>
    private static readonly Color PlayerAmbientColor = new Color(1.00f, 0.96f, 0.90f, 1f);

    // ── 場景主色染色（MapsTable 的 AtmoTint 欄；美術紀律一：色彩劇本）──
    // 把畫面「暗部」往地圖主色的色相拉（亮度不變），燈池中心/亮部幾乎不動。
    // 這是整體強度（再乘上 shader 內按暗度的權重）；想全遊戲染重/染淡改這個數。
    private const float SceneTintStrength = 0.32f;   // 0.5 首版實測整片糊成粉紗（連中間調都被拉），收到 0.32
    // 掃裝備欄取最大發光半徑用（靜態陣列，避免每幀 Enum.GetValues 產生 GC）。
    private static readonly Dipan.Inventory.EquipSlot[] EquipSlots =
    {
        Dipan.Inventory.EquipSlot.Weapon, Dipan.Inventory.EquipSlot.Chest, Dipan.Inventory.EquipSlot.Boots,
        Dipan.Inventory.EquipSlot.Gloves, Dipan.Inventory.EquipSlot.Amulet, Dipan.Inventory.EquipSlot.Ring,
    };

    private int _mode = 1;         // 當前氛圍型別（1=正常；2=幽暗；3=噩夢…）。預設 1，等地圖設定。
    private float _envDark = 0f;   // 環境壓暗量 0~1（＝1 − 環境亮度/100）。只在 _mode==1 生效。
    private float _bypass = 0f;    // 氛圍旁通 0~1（1＝完全還原原始畫面）。見 SetBypass。
    private Color _sceneTint = Color.white;  // 場景主色染色（AtmoTint 欄解析結果）
    private float _tintAmount = 0f;          // 0 = 不染；>0 = 染色整體強度（SceneTintStrength）

    private Material _mat;
    private Camera _cam;
    private AtmosphereBlit _blit;
    private Transform _player;
    private float _seed;

    // 每幀重用的緩衝（避免 GC）。
    private readonly List<LightSource> _lights = new List<LightSource>(MaxLights);
    private readonly Vector4[] _lightData = new Vector4[MaxLights];   // xy=viewport 位置, z=外圈半徑, w=內圈半徑
    private readonly Vector4[] _lightTint = new Vector4[MaxLights];   // rgb=光色, a=亮度

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("[Atmosphere]");
        go.AddComponent<AtmosphereController>();
        DontDestroyOnLoad(go);
    }

    /// <summary>
    /// 由 MapManager 在載圖時呼叫，套用當前地圖的氛圍型別（MapsTable 的 Atmosphere 欄）
    /// 與環境亮度（EnvBright 欄，0~100，100＝不壓暗）。
    /// </summary>
    public static void ApplyMapAtmosphere(int type, int envBright = 100, string tintHex = "")
    {
        if (Instance == null) AutoSpawn();
        if (Instance == null) return;
        Instance._mode = type;
        Instance._envDark = 1f - Mathf.Clamp01(envBright / 100f);

        // AtmoTint 欄：6 碼 RRGGBB（不含 #）。留空/解析失敗 = 不染色（舊行為）。
        Instance._tintAmount = 0f;
        if (!string.IsNullOrWhiteSpace(tintHex))
        {
            if (ColorUtility.TryParseHtmlString("#" + tintHex.Trim(), out Color c))
            {
                Instance._sceneTint = c;
                Instance._tintAmount = SceneTintStrength;
            }
            else
            {
                Debug.LogWarning($"[Atmosphere] AtmoTint「{tintHex}」不是合法的 RRGGBB，忽略不染色。");
            }
        }
    }

    /// <summary>
    /// 目前場景的「黑暗程度」0~1，給**不吃後處理的 UI**（Screen Space Overlay 的 HUD）跟著收斂亮度用
    /// （美術紀律七：血球等高亮 UI 在暗場景要調暗，別當畫面上最亮的東西）。
    /// 噩夢/深海恐怖=1、幽暗=0.8、深海/鬼霧=0.5、正常=EnvBright 的壓暗量、其餘氛圍=0。
    /// 回憶演出 SetBypass 淡掉氛圍時，這個值也跟著淡回 0（HUD 同步亮回來）。
    /// </summary>
    public static float DarknessLevel
    {
        get
        {
            var i = Instance;
            if (i == null) return 0f;
            float d;
            switch (i._mode)
            {
                case 3: case 9: d = 1f; break;
                case 2:         d = 0.8f; break;
                case 8: case 14: d = 0.5f; break;
                default:        d = (i._mode <= 1) ? i._envDark : 0f; break;
            }
            return d * (1f - i._bypass);
        }
    }

    /// <summary>
    /// 暫時淡出整套氛圍（0＝正常、1＝完全還原原始畫面）。**目前唯一呼叫者是回憶演出**
    /// （<c>Dipan.MapFx.MemoryFxController</c>）：幽暗／噩夢地圖畫面幾乎全黑，任何乘法式的
    /// 色調效果在那上面都等於失效，所以回憶期間把氛圍整個淡掉、讓場景亮回來再套回憶色。
    /// 語意也對——回憶不是「現在這個黑房間」，不該有提燈的黑暗感。
    /// ⚠ 它會連天氣（雨雪風霧）與提燈光圈一起淡掉，這是刻意的。
    /// </summary>
    public static void SetBypass(float v)
    {
        if (Instance == null) return;
        Instance._bypass = Mathf.Clamp01(v);
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

        // 氛圍 1（正常）且沒設環境亮度 → 完全不處理，材質設 null → AtmosphereBlit 直接 passthrough。
        // （有設環境亮度就要走 shader，才能壓暗並讓場上的燈照回來。）
        if (_mode <= 1 && _envDark <= 0.001f && _tintAmount <= 0.001f)
        {
            _blit.Material = null;
            return;
        }

        _blit.Material = _mat;
        _mat.SetFloat("_Mode", _mode);
        _mat.SetFloat("_EnvDark", _envDark);
        _mat.SetFloat("_Bypass", _bypass);
        _mat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
        _mat.SetColor("_SceneTint", _sceneTint);
        _mat.SetFloat("_TintAmount", _tintAmount);

        int count = BuildLights();
        _mat.SetFloat("_LightCount", count);
        _mat.SetVectorArray("_LightData", _lightData);
        _mat.SetVectorArray("_LightTint", _lightTint);
    }

    /// <summary>
    /// 收集這一幀要餵給 shader 的光源，填進 _lightData / _lightTint，回傳實際盞數。
    /// 順序：玩家身上的發光裝優先（一定看得到），其餘名額給最近的發光地上物。
    /// </summary>
    private int BuildLights()
    {
        // 只有「吃照明」的模式才收集光源：2 幽暗 / 3 噩夢 / 9 深海恐怖 / 14 鬼霧、或 type 1 且有環境壓暗。
        // 其他模式 shader 本來就不讀 v/lightShift；而「只染色（AtmoTint）啟用 shader」的亮圖若照收光源，
        // 玩家體光/佛燈會透過 mode 1 分支的 lightShift 在大白天染出一圈暖色——這裡直接擋掉。
        bool lit = _mode == 2 || _mode == 3 || _mode == 9 || _mode == 14 || (_mode <= 1 && _envDark > 0.001f);
        if (!lit)
        {
            for (int i = 0; i < MaxLights; i++) { _lightData[i] = Vector4.zero; _lightTint[i] = Vector4.zero; }
            return 0;
        }

        int n = 0;
        float t = Time.time;
        Vector3 from = _player != null ? _player.position : _cam.transform.position;

        // ① 玩家身上的發光裝（提燈）：以玩家為心，沿用單光源時代的暖色與呼吸手感。
        // ⚠ 劇情把主角藏起來時（PlayerVisibility.IsHidden）要一起跳過——否則空地上會浮著一圈沒有主人的光。
        float pr = Dipan.Cutscene.PlayerVisibility.IsHidden ? 0f : PlayerEquippedLightRadius();
        if (pr > 0f && _player != null)
        {
            float slow = Mathf.PerlinNoise(_seed, t * 0.55f);
            float fast = Mathf.Sin(t * 3.2f + _seed) * 0.5f + 0.5f;
            float breathe = Mathf.Lerp(0.93f, 1.06f, Mathf.Clamp01(0.85f * slow + 0.15f * fast));
            Write(n++, _player.position, pr * breathe, DefaultSoftness, LightSource.DefaultWarm, breathe);
        }
        else if (_player != null && !Dipan.Cutscene.PlayerVisibility.IsHidden)
        {
            // 玩家常駐體光（沒有任何發光裝時）：恆定不呼吸、半徑小於一個身位、瀰漫柔邊。
            // 目的只有一個——暗場景裡永遠看得見「我人在這」；照不了路，點燈壓力不變（見檔頭常數註解）。
            Write(n++, _player.position, PlayerAmbientRadius, PlayerAmbientSoftness,
                  PlayerAmbientColor, PlayerAmbientIntensity);
        }

        // ② 場上的發光地上物（火把/燈籠/香爐…），取最近的填滿剩餘名額。
        LightSource.CollectNearest(from, _lights, MaxLights - n);
        for (int i = 0; i < _lights.Count && n < MaxLights; i++)
        {
            var ls = _lights[i];
            float breathe = ls.Breathe(t);
            Write(n++, ls.transform.position, ls.radius * breathe,
                  Mathf.Clamp01(ls.softness), ls.color, ls.intensity * breathe);
        }

        // 沒用到的槽位清成「不發光」，避免上一幀的殘值被 shader 讀到。
        for (int i = n; i < MaxLights; i++)
        {
            _lightData[i] = Vector4.zero;
            _lightTint[i] = Vector4.zero;
        }
        return n;
    }

    // 把一盞燈換算成 shader 要的格式：世界座標→viewport、世界半徑→viewport 比例。
    private void Write(int slot, Vector3 world, float worldRadius, float softness, Color color, float intensity)
    {
        Vector3 v = _cam.WorldToViewportPoint(world);
        float outerVp = OuterViewportFromWorld(worldRadius);
        _lightData[slot] = new Vector4(v.x, v.y, outerVp, outerVp * softness);
        _lightTint[slot] = new Vector4(color.r, color.g, color.b, Mathf.Max(0f, intensity));
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
