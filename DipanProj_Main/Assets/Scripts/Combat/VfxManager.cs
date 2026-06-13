using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 一次性視覺特效（VFX）的場景單例。負責從 VfxTable.csv 載入配方、預載序列圖，
/// 並提供 Spawn(id, position, angle) 工廠。武器表的 FireEffectID / HitEffectID 透過此處生成特效。
/// 不需要 prefab：每次 Spawn 自己 new 一個 GameObject + SpriteRenderer，排序由本元件的序列化欄位決定。
/// </summary>
public class VfxManager : MonoBehaviour
{
    public TextAsset VfxCSV;

    [Header("Sorting（所有 VFX 共用此排序設定）")]
    public string SortingLayerName = "Default";
    public int SortingOrder = 100;
    public Material VfxMaterial;   // 留空 = 用 Unity 預設 Sprite material（一般足夠）

    private readonly Dictionary<int, VfxData> _effects = new Dictionary<int, VfxData>();

    void Awake()
    {
        LoadEffects();
    }

    public VfxData GetEffect(int id)
    {
        if (_effects.TryGetValue(id, out VfxData data))
            return data;

        Debug.LogError($"Vfx ID {id} not found!");
        return null;
    }

    /// <summary>在指定座標生成一次性特效。angleDeg 用於有方向性的特效（如揮砍）；無方向的爆裂填 0 即可。</summary>
    public VfxInstance Spawn(int id, Vector2 position, float angleDeg = 0f)
    {
        VfxData data = GetEffect(id);
        if (data == null) return null;
        if (data.AnimationSprites == null || data.AnimationSprites.Length == 0)
        {
            Debug.LogWarning($"Vfx '{data.Name}' (ID {id}) has no sprites loaded; skipped.");
            return null;
        }

        var go = new GameObject($"Vfx_{data.Name}");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = SortingLayerName;
        sr.sortingOrder = SortingOrder;
        if (VfxMaterial != null) sr.sharedMaterial = VfxMaterial;

        var instance = go.AddComponent<VfxInstance>();
        instance.Initialize(data, sr);
        return instance;
    }

    private void LoadEffects()
    {
        if (VfxCSV == null)
        {
            Debug.LogError("Vfx CSV is not assigned!");
            return;
        }

        string[] lines = VfxCSV.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] v = lines[i].Split(',');
            if (v.Length < 5) continue; // ID, Name, AniPath, AniNumber, AnimFPS 為必要欄位

            var data = new VfxData();
            data.ID = int.Parse(v[0]);
            data.Name = v[1].Trim();
            data.AniPath = v[2].Trim();
            data.AniNumber = !string.IsNullOrWhiteSpace(v[3]) ? int.Parse(v[3].Trim()) : 0;
            data.AnimFPS = !string.IsNullOrWhiteSpace(v[4]) ? float.Parse(v[4].Trim()) : 0f;
            data.Scale = (v.Length > 5 && !string.IsNullOrWhiteSpace(v[5])) ? float.Parse(v[5].Trim()) : 1f;
            if (data.Scale <= 0f) data.Scale = 1f;
            data.Loop = (v.Length > 6 && !string.IsNullOrWhiteSpace(v[6])) && int.Parse(v[6].Trim()) != 0;
            data.Duration = (v.Length > 7 && !string.IsNullOrWhiteSpace(v[7])) ? float.Parse(v[7].Trim()) : 0f;

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
                        Debug.LogWarning($"Vfx sprite not found at '{framePath}' for effect '{data.Name}'.");
                        allLoaded = false;
                    }
                }
                data.AnimationSprites = allLoaded ? sprites : null;
            }

            _effects[data.ID] = data;
        }

        Debug.Log($"Loaded {_effects.Count} vfx from CSV.");
    }
}
