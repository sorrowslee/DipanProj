using UnityEngine;
using System.Collections.Generic;
using Dipan.Data;

public class GroundEffectManager : MonoBehaviour
{
    public TextAsset GroundEffectCSV;
    public GameObject GroundEffectPrefab;
    public LayerMask EnemyLayer;
    [Tooltip("可破壞地上物所在層;留空(0)時自動以名稱 \"Environment\" 解析")]
    public LayerMask EnvironmentLayer;

    private readonly Dictionary<int, GroundEffectData> _effects = new Dictionary<int, GroundEffectData>();

    void Awake()
    {
        if (EnvironmentLayer == 0) EnvironmentLayer = LayerMask.GetMask("Environment");
        LoadEffects();
    }

    public GroundEffectData GetEffect(int id)
    {
        if (_effects.TryGetValue(id, out GroundEffectData data))
            return data;

        Debug.LogError($"GroundEffect ID {id} not found!");
        return null;
    }

    /// <param name="damageOverride">
    /// &lt; 0（預設）= 用 GroundEffectTable 的 Damage；&ge; 0 = 改用此值
    /// （佛光等「載體型」特效把武器表 Damage 餵進來）。
    /// </param>
    /// <param name="visualScale">
    /// 純視覺縮放（直接設 localScale）。**不影響傷害判定**——判定走 OverlapCircle(Radius)。
    /// 想讓「看到的＝打得到的」請改用 <paramref name="radiusScale"/>。
    /// </param>
    /// <param name="radiusScale">
    /// 半徑倍率：**視覺與傷害一起**縮放，兩者永遠一致。佛光這種跟著玩家的光環用它接體型倍率。
    /// </param>
    public GroundEffectInstance Spawn(int id, Vector2 position, float damageOverride = -1f,
                                      float visualScale = 1f, float radiusScale = 1f)
    {
        if (GroundEffectPrefab == null)
        {
            Debug.LogError("GroundEffectPrefab is not assigned on GroundEffectManager!");
            return null;
        }

        GroundEffectData data = GetEffect(id);
        if (data == null) return null;

        GameObject go = Instantiate(GroundEffectPrefab, position, Quaternion.identity);
        GroundEffectInstance instance = go.GetComponent<GroundEffectInstance>();
        if (instance == null)
        {
            Debug.LogError("GroundEffectPrefab is missing the GroundEffectInstance component!");
            Destroy(go);
            return null;
        }

        instance.Initialize(data, EnemyLayer | EnvironmentLayer, damageOverride, radiusScale);
        if (visualScale > 0f && !Mathf.Approximately(visualScale, 1f))
            go.transform.localScale = Vector3.one * visualScale;
        return instance;
    }

    private void LoadEffects()
    {
        if (GroundEffectCSV == null)
        {
            Debug.LogError("GroundEffect CSV is not assigned!");
            return;
        }

        // 2026-08-26 起依表頭名稱取值（欄位可重排、# 註解列、空白=預設），見 CsvTable。
        var table = CsvTable.Parse(GroundEffectCSV.text, "GroundEffectTable");
        table.Require("ID", "Name", "Radius", "Duration", "DamageInterval", "Damage", "AniPath");
        foreach (var err in table.Errors) Debug.LogError(err);

        foreach (var row in table.Rows)
        {
            var data = new GroundEffectData();
            data.ID = row.GetInt("ID", 0);
            if (data.ID <= 0) continue;
            data.Name = row.Get("Name");
            data.Radius = row.GetFloat("Radius", 1f);
            data.Duration = row.GetFloat("Duration", 1f);
            data.DamageInterval = row.GetFloat("DamageInterval", 0f);
            data.Damage = row.GetFloat("Damage", 0f);
            data.AniPath = row.Get("AniPath");
            data.AniNumber = row.GetInt("AniNumber", 0);
            data.AnimFPS = row.GetFloat("AnimFPS", 0f);
            data.TileSize = row.GetFloat("TileSize", 1f);
            if (data.TileSize <= 0f) data.TileSize = 1f;

            // 渲染模式：留空 / Tile = tile 鋪滿（預設）；
            // Single = 單張縮放到直徑的圓暈（靜態）；
            // Glow = 單張 + Custom/AuraGlow 加色發光 + 燈火忽強忽弱明滅 + 微幅呼吸縮放（佛光用）。
            data.SingleSprite = false;
            data.GlowFlicker = false;
            string mode = row.Get("RenderMode");
            if (!string.IsNullOrEmpty(mode))
            {
                bool isGlow = mode.Equals("Glow", System.StringComparison.OrdinalIgnoreCase);
                data.GlowFlicker = isGlow;
                // Glow 蘊含單圖模式
                data.SingleSprite = isGlow || mode.Equals("Single", System.StringComparison.OrdinalIgnoreCase);
            }

            // 背景旋轉符號：留空 = 沒有這一層。與 RenderMode 完全無關，三種模式都能掛。
            // 在這裡就把圖載好（同 AniPath 的做法），生成特效時不必再碰 Resources。
            data.SigilPath = row.Get("SigilPath");
            if (!string.IsNullOrEmpty(data.SigilPath))
            {
                data.SigilSprite = Resources.Load<Sprite>(data.SigilPath);
                if (data.SigilSprite == null)
                {
                    // 最常見原因：PNG 的 Texture Type 不是 Sprite，或路徑打錯／不在 Resources 底下。
                    Debug.LogWarning($"Ground effect sigil sprite not found at '{data.SigilPath}' for effect '{data.Name}'（檢查圖是否匯入為 Sprite 類型）。");
                }
            }

            // 發光半徑：留空 / <=0 = 不發光。> 0 時特效會掛 LightSource 真的照亮暗場景。
            data.LightRadius = row.GetFloat("LightRadius", 0f);

            if (!string.IsNullOrEmpty(data.AniPath) && data.AniNumber > 0)
            {
                var sprites = new Sprite[data.AniNumber];
                bool allLoaded = true;
                for (int f = 0; f < data.AniNumber; f++)
                {
                    string framePath = $"{data.AniPath}_{(f + 1):D2}";
                    sprites[f] = Resources.Load<Sprite>(framePath);
                    if (sprites[f] == null)
                    {
                        Debug.LogWarning($"Ground effect sprite not found at '{framePath}' for effect '{data.Name}'.");
                        allLoaded = false;
                    }
                }
                data.AnimationSprites = allLoaded ? sprites : null;
            }

            _effects[data.ID] = data;
        }

        Debug.Log($"Loaded {_effects.Count} ground effects from CSV.");
    }
}
