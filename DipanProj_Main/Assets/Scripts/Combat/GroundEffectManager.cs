using UnityEngine;
using System.Collections.Generic;

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
    public GroundEffectInstance Spawn(int id, Vector2 position, float damageOverride = -1f)
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

        instance.Initialize(data, EnemyLayer | EnvironmentLayer, damageOverride);
        return instance;
    }

    private void LoadEffects()
    {
        if (GroundEffectCSV == null)
        {
            Debug.LogError("GroundEffect CSV is not assigned!");
            return;
        }

        string[] lines = GroundEffectCSV.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] v = lines[i].Split(',');
            if (v.Length < 9) continue;

            var data = new GroundEffectData();
            data.ID = int.Parse(v[0]);
            data.Name = v[1].Trim();
            data.Radius = float.Parse(v[2]);
            data.Duration = float.Parse(v[3]);
            data.DamageInterval = float.Parse(v[4]);
            data.Damage = float.Parse(v[5]);
            data.AniPath = v[6].Trim();
            data.AniNumber = !string.IsNullOrWhiteSpace(v[7]) ? int.Parse(v[7].Trim()) : 0;
            data.AnimFPS = !string.IsNullOrWhiteSpace(v[8]) ? float.Parse(v[8].Trim()) : 0f;
            data.TileSize = (v.Length >= 10 && !string.IsNullOrWhiteSpace(v[9])) ? float.Parse(v[9].Trim()) : 1f;
            if (data.TileSize <= 0f) data.TileSize = 1f;

            // 渲染模式：留空 / Tile = tile 鋪滿（預設）；
            // Single = 單張縮放到直徑的圓暈（靜態）；
            // Glow = 單張 + Custom/AuraGlow 加色發光 + 燈火忽強忽弱明滅 + 微幅呼吸縮放（佛光用）。
            data.SingleSprite = false;
            data.GlowFlicker = false;
            if (v.Length >= 11 && !string.IsNullOrWhiteSpace(v[10]))
            {
                string mode = v[10].Trim();
                bool isGlow = mode.Equals("Glow", System.StringComparison.OrdinalIgnoreCase);
                data.GlowFlicker = isGlow;
                // Glow 蘊含單圖模式
                data.SingleSprite = isGlow || mode.Equals("Single", System.StringComparison.OrdinalIgnoreCase);
            }

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
