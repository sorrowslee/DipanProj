using Dipan.Vfx;
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
    [Tooltip("地刺設定表 Assets/Data/GroundSpikeTable.csv（武器表 TrailSpikeID 指向它，見 readme/GROUND_SPIKE.md）")]
    public TextAsset GroundSpikeCSV;
    [Tooltip("火焰設定表 Assets/Data/FlameTable.csv（武器表 FlameStyleID、GroundFireTable 的 FlameID 指向它，見 readme/DIPAN_VFX.md）")]
    public TextAsset FlameCSV;
    [Tooltip("地上燃燒設定表 Assets/Data/GroundFireTable.csv（GroundEffectTable 的 FireStyleID 指向它，見 readme/DIPAN_VFX.md）")]
    public TextAsset GroundFireCSV;
    [Tooltip("爆炸設定表 Assets/Data/ExplosionTable.csv（武器表 HitBurstID 指向它，見 readme/DIPAN_VFX.md）")]
    public TextAsset ExplosionCSV;
    [Tooltip("傳送點外型設定表 Assets/Data/TeleportFxTable.csv（MapLoader 的 teleportFxStyleId 指向它，見 readme/DIPAN_VFX.md）")]
    public TextAsset TeleportFxCSV;
    [Tooltip("光束外觀設定表 Assets/Data/BeamFxTable.csv（武器表 BeamFxID 指向它：雷射／連鎖／掃射，見 readme/DIPAN_VFX.md §4d）")]
    public TextAsset BeamFxCSV;

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
        // DipanVfx 套件（地刺／火焰／地上燃燒，見 readme/DIPAN_VFX.md）：立著的東西（火舌）跟角色一起 Y 排序
        VfxAssets.SortOrderForY = y => MapDepthSort.Order(y, 0);
        var spike = LoadTable(GroundSpikeCSV, "GroundSpikeTable", "Ground Spike CSV");
        if (spike != null) GroundSpikeDatabase.LoadFromText(spike);
        var flame = LoadTable(FlameCSV, "FlameTable", "Flame CSV");
        if (flame != null) FlameDatabase.LoadFromText(flame);
        var fire = LoadTable(GroundFireCSV, "GroundFireTable", "Ground Fire CSV");
        if (fire != null) GroundFireDatabase.LoadFromText(fire);
        var boom = LoadTable(ExplosionCSV, "ExplosionTable", "Explosion CSV");
        if (boom != null) ExplosionDatabase.LoadFromText(boom);
        var tele = LoadTable(TeleportFxCSV, "TeleportFxTable", "Teleport Fx CSV");
        if (tele != null) TeleportFxDatabase.LoadFromText(tele);
        var beamFx = LoadTable(BeamFxCSV, "BeamFxTable", "Beam Fx CSV");
        if (beamFx != null) BeamFxDatabase.LoadFromText(beamFx);
    }

    /// <summary>DipanVfx 的設定表（2026-11-17 地刺、2026-11-18 火焰／地上燃燒）。
    /// 場景沒拖時，編輯器下直接讀資產當退路，免得忘了拖就整排特效不見。</summary>
    static string LoadTable(TextAsset csv, string fileName, string fieldLabel)
    {
#if UNITY_EDITOR
        if (csv == null) csv = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/Data/{fileName}.csv");
#endif
        if (csv == null)
        {
            Debug.LogWarning($"[VfxManager] {fieldLabel} 沒有指定：請把 Assets/Data/{fileName}.csv 拖進 VfxManager 的 {fieldLabel} 欄。");
            return null;
        }
        return csv.text;
    }

    /// <summary>在地上長一組地刺（GroundSpikeTable 第 spikeId 列）。排序層／預設排序跟一般特效相同。</summary>
    public void SpawnGroundSpikes(int spikeId, Vector2 groundPos, Vector2 travelDir, float extraScale = 1f)
        => GroundSpikeBurst.Spawn(spikeId, groundPos, travelDir, extraScale, SortingLayerName, SortingOrder);

    /// <summary>
    /// 炸一次 ExplosionTable 第 <paramref name="id"/> 列（DipanVfx，2026-11-18）。
    /// <paramref name="radius"/> &gt; 0＝範圍半徑；&lt;= 0＝用表的 Radius 再乘 |radius|（傳 -集氣放大倍率；0 視為 1）。
    /// 火球用全域特效排序；衝擊波圈貼地（地面特效層 8，見 MapDepthSort 檔頭）。
    /// </summary>
    public void SpawnExplosion(int id, Vector2 pos, float radius)
    {
        var d = ExplosionDatabase.Get(id);
        if (d == null) return;
        float r = radius > 0f ? radius : d.Radius * (radius < 0f ? -radius : 1f);
        ExplosionBurst.Spawn(d, pos, r, SortingLayerName, SortingOrder, GroundEffectOrder);
    }

    const int GroundEffectOrder = 8;   // = GroundEffectInstance.GroundEffectSortingOrder（地面特效層）

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

    /// <summary>生成「循環播放」的特效（持續燃燒等），lifeSeconds 秒後自毀。不動 VfxTable：複製一份 data 覆寫 Loop/Duration。
    /// extraScale＝在 VfxTable.Scale 之上再乘的倍率（同 Spawn）。用於榕樹妖死亡的持續火焰。</summary>
    public VfxInstance SpawnLoop(int id, Vector2 position, float extraScale = 1f, float lifeSeconds = 600f)
    {
        VfxData src = GetEffect(id);
        if (src == null || src.AnimationSprites == null || src.AnimationSprites.Length == 0)
        {
            Debug.LogWarning($"Vfx ID {id} 無法循環生成（找不到或沒圖）。");
            return null;
        }
        // 複製一份，避免動到共用的表資料（別的呼叫者還會用同一顆 VfxData）。
        var data = new VfxData {
            ID = src.ID, Name = src.Name, AniPath = src.AniPath, AniNumber = src.AniNumber,
            AnimFPS = src.AnimFPS, Scale = src.Scale, Loop = true, Duration = lifeSeconds,
            HasSortingOrder = src.HasSortingOrder, SortingOrder = src.SortingOrder,
            AnimationSprites = src.AnimationSprites
        };

        var go = new GameObject($"VfxLoop_{data.Name}");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = SortingLayerName;
        sr.sortingOrder = data.HasSortingOrder ? data.SortingOrder : SortingOrder;
        if (VfxMaterial != null) sr.sharedMaterial = VfxMaterial;

        var instance = go.AddComponent<VfxInstance>();
        instance.Initialize(data, sr);
        if (extraScale > 0f && !Mathf.Approximately(extraScale, 1f))
            go.transform.localScale = Vector3.one * (data.Scale * extraScale);
        return instance;
    }

    /// <summary>生成循環特效並把最終世界高度精準縮放到 targetWorldHeight；用於需跟角色實際大小同步的集氣光圈。</summary>
    public VfxInstance SpawnLoopSizedToHeight(int id, Vector2 position, float targetWorldHeight, float lifeSeconds = -1f)
    {
        VfxData data = GetEffect(id);
        if (data == null || data.AnimationSprites == null || data.AnimationSprites.Length == 0
            || data.AnimationSprites[0] == null)
            return SpawnLoop(id, position, 1f, lifeSeconds);

        float nativeHeight = data.AnimationSprites[0].bounds.size.y;
        float baseHeight = nativeHeight * Mathf.Max(0.0001f, data.Scale);
        float extraScale = baseHeight > 0.0001f && targetWorldHeight > 0f
            ? targetWorldHeight / baseHeight
            : 1f;
        return SpawnLoop(id, position, extraScale, lifeSeconds);
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

        // 2026-08-26 起依表頭名稱取值（欄位可重排、# 註解列、空白=預設），見 CsvTable。
        var table = Dipan.Data.CsvTable.Parse(VfxCSV.text, "VfxTable");
        table.Require("ID", "Name", "AniPath", "AniNumber", "AnimFPS");
        foreach (var err in table.Errors) Debug.LogError(err);

        foreach (var row in table.Rows)
        {
            var data = new VfxData();
            data.ID = row.GetInt("ID", 0);
            if (data.ID <= 0) continue;
            data.Name = row.Get("Name");
            data.AniPath = row.Get("AniPath");
            data.AniNumber = row.GetInt("AniNumber", 0);
            data.AnimFPS = row.GetFloat("AnimFPS", 0f);
            data.Scale = row.GetFloat("Scale", 1f);
            if (data.Scale <= 0f) data.Scale = 1f;
            data.Loop = row.GetBool("Loop", false);
            data.Duration = row.GetFloat("Duration", 0f);
            // 留空 = 用 VfxManager 全域 SortingOrder；填了 = 本特效專屬排序（地刺填低於角色的值 → 畫在腳下）
            if (row.Has("SortingOrder"))
            {
                data.HasSortingOrder = true;
                data.SortingOrder = row.GetInt("SortingOrder", 0);
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
