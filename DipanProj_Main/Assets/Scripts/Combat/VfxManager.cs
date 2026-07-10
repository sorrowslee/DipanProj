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
    // 角色/怪物改走 Y 排序帶（MapDepthSort），擊中/發射特效預設要抬到那之上才不會被角色/地上物蓋掉。
    // 需畫在角色腳下的特效（如地刺）仍在 VfxTable 自填低 SortingOrder，不受此預設影響。
    public int SortingOrder = 22000;   // 高於角色/地上物 Y 排序帶（16-bit 安全，<32767）
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
    public VfxInstance Spawn(int id, Vector2 position, float angleDeg = 0f, float extraScale = 1f)
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
        // 每個特效可在 VfxTable 自填 SortingOrder（例如地刺設低於角色讓它在腳下）；留空才用本 Manager 的全域預設。
        sr.sortingOrder = data.HasSortingOrder ? data.SortingOrder : SortingOrder;
        if (VfxMaterial != null) sr.sharedMaterial = VfxMaterial;

        var instance = go.AddComponent<VfxInstance>();
        instance.Initialize(data, sr);   // 已設 localScale = data.Scale
        // extraScale：在 VfxTable 的 Scale 之上再乘一個倍率（召喚特效用來縮放到怪物大小）。
        if (extraScale > 0f && !Mathf.Approximately(extraScale, 1f))
            go.transform.localScale = Vector3.one * (data.Scale * extraScale);
        return instance;
    }

    /// <summary>在指定座標播特效，並縮放到「世界高度 ≈ targetWorldHeight × VfxTable.Scale」——
    /// VfxTable 的 Scale 在此當「相對目標的倍率」（1 = 與目標等高、1.3 = 放大 30%）。用於召喚特效跟著怪物大小。</summary>
    public VfxInstance SpawnSizedToHeight(int id, Vector2 position, float targetWorldHeight, float angleDeg = 0f)
    {
        VfxData data = GetEffect(id);
        if (data == null || data.AnimationSprites == null || data.AnimationSprites.Length == 0)
            return Spawn(id, position, angleDeg);
        float nativeH = data.AnimationSprites[0].bounds.size.y;   // 第一幀原生世界高（由匯入 PPU 決定）
        float extra = (nativeH > 0.0001f && targetWorldHeight > 0f) ? targetWorldHeight / nativeH : 1f;
        return Spawn(id, position, angleDeg, extra);
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
            // 留空 = 用 VfxManager 全域 SortingOrder；填了 = 本特效專屬排序（地刺填低於角色的值 → 畫在腳下）
            if (v.Length > 8 && !string.IsNullOrWhiteSpace(v[8]))
            {
                data.HasSortingOrder = true;
                data.SortingOrder = int.Parse(v[8].Trim());
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
