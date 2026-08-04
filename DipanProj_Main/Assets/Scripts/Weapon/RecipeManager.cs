using UnityEngine;
using System.Collections.Generic;
using Sorrows.Ballistics;

public enum BounceTarget { None, Environment, Enemy }
public enum GroundEffectTrigger { OnSpawn, OnHit, OnDeath }
public enum GroundEffectHitTarget { Enemy, Environment, Any, Ground }
public enum LaunchSource { Player, Offscreen }
// 命中迸發子武器的觸發過濾：打到敵人 / 環境(牆壁+可破壞地上物) / All(任一都觸發)
public enum SubWeaponHitTarget { Enemy, Environment, All }

public class RecipeEntry
{
    public int ID;
    public string Name;
    public ProjectileData Data;
    public BounceTarget BounceTarget;
    public int SubRecipeID = -1;
    public RecipeEntry SubRecipe;   // SubRecipeID 解析後的子配方參考（取子配方的 IsChain/ChainCount/ChainRadius 等主遊戲側欄位用）
    public bool BlockedByEnvironment = true;
    public int GroundEffectID = 0;
    public GroundEffectTrigger GroundEffectTrigger = GroundEffectTrigger.OnHit;
    public GroundEffectHitTarget GroundEffectHitTarget = GroundEffectHitTarget.Enemy;
    public LaunchSource LaunchSource = LaunchSource.Player;
    public float BlastRadius = 0f;  // 拋物線落地殺傷半徑；> 0 時落地對半徑內怪物以武器 Damage 炸一次（與地面火堆獨立）
    // 佛光型武器：1 時不發射任何子彈，改在玩家身上維持一個「跟著玩家移動」的 GroundEffect（圓形 AOE）。
    // 純主遊戲側（不碰彈道系統，因為它不發射子彈）。圓的半徑/節拍/外觀走 GroundEffectID 指向的 GroundEffectTable，傷害走武器表 Damage。
    public bool IsAura = false;

    // 連鎖閃電：1 時朝滑鼠射出，命中首怪後在 ChainRadius 內逐跳到最近的怪，跳 ChainCount 次。
    // 目標搜尋與傷害都在主遊戲側結算（LaserBeam 只當折線視覺）。與其他模式互斥。
    public bool IsChain = false;
    public float ChainRadius = 4f;  // 每跳的搜尋半徑（世界單位）
    public int ChainCount = 0;       // 跳躍次數（= MaxBounces 欄；總命中數 = 1 + ChainCount）

    // 落雷模式：1 時從畫面上緣劈下到滑鼠所在點，落地以 BlastRadius 做圓形 AOE（目前九霄雷獄使用）。
    // 吃 SpreadCount/SpreadAngle（多道落點）與 HomingTurnSpeed（落點吸附最近怪，當搜尋半徑用）。與其他模式互斥。
    public bool IsSkyStrike = false;

    // 命中迸發子武器：子彈命中時，在命中點生成「武器表上指定武器」的子彈（子武器有自己的外型/傷害/追蹤）。
    // 與 SubRecipeID 不同：SubRecipeID 是配方(無外型，仿母武器)；SubWeaponOnHit 是武器(自帶外型)。0/留空 = 不觸發。
    public int SubWeaponOnHit = 0;
    public SubWeaponHitTarget SubWeaponHitTarget = SubWeaponHitTarget.Enemy; // 打到哪類目標才迸發（Enemy/Environment/All）

    // 召喚型武器：1 時施放不發射子彈，改在施放者周圍生怪（冷卻用 FireInterval 欄）。目前先做給怪物(boss)，
    // 未來可原樣給主角。名單/數量/上限/半徑走下面四欄，由 MonsterWeaponUser（或未來玩家側）結算。
    public bool IsSummon = false;
    public int[] SummonIds;          // 可召喚的怪物 ID 池（CSV SummonIds 欄，用 | 分隔避開逗號）；每次從中隨機抽
    public int SummonCount = 1;      // 每次施放召喚幾隻
    public int SummonMaxAlive = 4;   // 同一施放者的分身同時存在上限（達上限則暫停召喚）
    public float SummonRadius = 2f;  // 在施放者周圍多遠的環上生成

    // 定點法陣：在滑鼠位置生成 GroundEffect；射程沿用 BeamRange、傷害以武器 Damage 覆寫 GroundEffectTable。
    public bool IsGroundCast = false;

    // 近身扇形攻擊：半徑沿用 BlastRadius，角度用 MeleeAngle；HitEffectID 只在斬擊中心播一次。
    public bool IsMelee = false;
    public float MeleeAngle = 100f;

    // 突進斬：沿瞄準方向移動，遇 Environment 提前停；掃過的膠囊區域各目標受傷一次。
    public bool IsDash = false;
    public float DashDistance = 4f;
    public float DashWidth = 1f;

    // 分段全高雷柱：僅搭配 IsSkyStrike；start + tileable loop + end 從鏡頭頂外鋪到落點。
    public bool UseSegmentedSkyStrike = false;

    // 集氣模式：按住攻擊鍵，放開時才施放；滿 3 秒時傷害 ×3、武器視覺尺寸 ×2。
    // 持續輸入型武器（IsLaser / IsAura）互斥，CSV 即使誤填也會在載入時停用。
    public bool IsChargeMode = false;
    // 集氣時間縮減百分比：30 = 減少 30%；-20 = 延長 20%。CSV 可填 30% / -20%，留空 = 0。
    public float ChargeTimeReductionPercent = 0f;

    /// <summary>
    /// 深拷貝一份配方（連同 <see cref="Data"/>）。
    ///
    /// ⚠ **為什麼一定要拷貝**：RecipeManager 在 Awake 時每個配方只 new 一次，之後
    /// 所有引用它的武器（含怪物的武器、以及把它當 SubRecipeID 的母配方）拿到的都是**同一個物件**。
    /// 玩家的鑲嵌加成如果就地改欄位，會同時改到怪物身上，而且永久累積到重開遊戲為止。
    /// 所以能力容器一律「拷貝一份 → 在拷貝上套加成 → 交給玩家用」。見 readme/GEM_SOCKET.md。
    ///
    /// 子配方（<see cref="SubRecipe"/> / <see cref="ProjectileData.SubProjectileData"/>）維持共用參照——
    /// 玩家的加成刻意不往下傳給分裂出來的子彈；哪天要傳，在這裡多拷一層即可。
    /// </summary>
    public RecipeEntry Clone()
    {
        var c = (RecipeEntry)MemberwiseClone();   // 全欄位淺拷貝（值型別欄位天生獨立）
        c.Data = ClonePd(Data);
        return c;
    }

    /// <summary>深拷貝一份 ProjectileData（子配方維持共用參照，理由同 <see cref="Clone"/>）。</summary>
    public static ProjectileData ClonePd(ProjectileData src)
    {
        if (src == null) return null;
        return new ProjectileData
        {
            Speed = src.Speed,
            Radius = src.Radius,
            LifeTime = src.LifeTime,
            RotationSpeed = src.RotationSpeed,
            FireInterval = src.FireInterval,
            PierceCount = src.PierceCount,
            HasBounce = src.HasBounce,
            MaxBounces = src.MaxBounces,
            HasHoming = src.HasHoming,
            HomingTurnSpeed = src.HomingTurnSpeed,
            HasSplit = src.HasSplit,
            Timing = src.Timing,
            SplitCount = src.SplitCount,
            SpreadAngle = src.SpreadAngle,
            SubProjectileData = src.SubProjectileData,   // 刻意共用（見 Clone 的說明）
            IsOrbital = src.IsOrbital,
            OrbitalRadius = src.OrbitalRadius,
            OrbitalCount = src.OrbitalCount,
            IsParabolic = src.IsParabolic,
            ArcHeight = src.ArcHeight,
            LandingScatterRadius = src.LandingScatterRadius,
            IsLaser = src.IsLaser,
            DotInterval = src.DotInterval,
            BeamRange = src.BeamRange,
            TrailStep = src.TrailStep,
        };
    }
}

public class RecipeManager : MonoBehaviour
{
    public TextAsset RecipeCSV;

    private Dictionary<int, RecipeEntry> _recipes = new Dictionary<int, RecipeEntry>();

    void Awake()
    {
        LoadRecipes();
        ResolveSubRecipes();
        AutoFillMissingSubRecipes();
    }

    public RecipeEntry GetRecipe(int id)
    {
        if (_recipes.TryGetValue(id, out RecipeEntry entry))
            return entry;

        Debug.LogError($"Recipe ID {id} not found!");
        return null;
    }

    private void LoadRecipes()
    {
        if (RecipeCSV == null)
        {
            Debug.LogError("Recipe CSV is not assigned!");
            return;
        }

        string[] lines = RecipeCSV.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] v = lines[i].Split(',');
            if (v.Length < 14) continue; // column 14 (HomingTurnSpeed) 為選填，向下相容

            var entry = new RecipeEntry();
            entry.ID = int.Parse(v[0]);
            entry.Name = v[1].Trim();

            var data = new ProjectileData();
            data.Speed = float.Parse(v[2]);
            data.Radius = float.Parse(v[3]);
            data.LifeTime = float.Parse(v[4]);
            data.FireInterval = float.Parse(v[5]);
            data.RotationSpeed = float.Parse(v[6]);
            data.PierceCount = int.Parse(v[7]);

            int spreadCount = int.Parse(v[8]);
            float spreadAngle = float.Parse(v[9]);
            string splitTimingStr = v[10].Trim();
            string subRecipeStr = v[11].Trim();

            // SplitCount / SpreadAngle 始終記錄到 ProjectileData，方便拋物線等其他發射模式直接讀取；
            // HasSplit 仍只控制是否組裝 SplitBehavior（需 SpreadCount > 1 + 有 SplitTiming）
            data.SplitCount = Mathf.Max(1, spreadCount);
            data.SpreadAngle = spreadAngle;

            if (spreadCount > 1 && !string.IsNullOrEmpty(splitTimingStr))
            {
                data.HasSplit = true;
                data.Timing = ParseSplitTiming(splitTimingStr);
            }

            if (!string.IsNullOrEmpty(subRecipeStr))
            {
                entry.SubRecipeID = int.Parse(subRecipeStr);
            }

            string bounceStr = v[12].Trim();
            entry.BounceTarget = ParseBounceTarget(bounceStr);

            int maxBounces = int.Parse(v[13].Trim());
            if (entry.BounceTarget != BounceTarget.None && maxBounces > 0)
            {
                data.HasBounce = true;
                data.MaxBounces = maxBounces;
            }

            if (v.Length >= 15 && !string.IsNullOrWhiteSpace(v[14]))
            {
                float homingTurnSpeed = float.Parse(v[14].Trim());
                if (homingTurnSpeed > 0f)
                {
                    data.HasHoming = true;
                    data.HomingTurnSpeed = homingTurnSpeed;
                }
            }

            if (v.Length >= 16 && !string.IsNullOrWhiteSpace(v[15]))
            {
                int isOrbital = int.Parse(v[15].Trim());
                if (isOrbital > 0)
                {
                    data.IsOrbital = true;
                    data.OrbitalRadius = (v.Length >= 17 && !string.IsNullOrWhiteSpace(v[16])) ? float.Parse(v[16].Trim()) : 2f;
                    data.OrbitalCount = (v.Length >= 18 && !string.IsNullOrWhiteSpace(v[17])) ? int.Parse(v[17].Trim()) : 3;
                }
            }

            // 留空或 1 = 會被地形擋下（向下相容預設）；0 = 子彈穿透地形不被銷毀。
            entry.BlockedByEnvironment = true;
            if (v.Length >= 19 && !string.IsNullOrWhiteSpace(v[18]))
            {
                entry.BlockedByEnvironment = int.Parse(v[18].Trim()) != 0;
            }

            // 地面特效鏈式觸發：留空 / 0 = 不觸發；> 0 = 引用 GroundEffectTable 對應 ID。
            entry.GroundEffectID = 0;
            if (v.Length >= 20 && !string.IsNullOrWhiteSpace(v[19]))
            {
                entry.GroundEffectID = int.Parse(v[19].Trim());
            }

            entry.GroundEffectTrigger = GroundEffectTrigger.OnHit;
            if (v.Length >= 21 && !string.IsNullOrWhiteSpace(v[20]))
            {
                entry.GroundEffectTrigger = ParseGroundEffectTrigger(v[20].Trim());
            }

            // 地面特效命中過濾：留空 / Enemy = 只有打到怪物才觸發；Environment = 只有打到障礙物才觸發；Any = 兩者都觸發；Ground = 拋物線最終落地時觸發。
            entry.GroundEffectHitTarget = GroundEffectHitTarget.Enemy;
            if (v.Length >= 22 && !string.IsNullOrWhiteSpace(v[21]))
            {
                entry.GroundEffectHitTarget = ParseGroundEffectHitTarget(v[21].Trim());
            }

            // 拋物線武器：留空或 0 = 一般彈道；1 = 啟用拋物線（與 IsOrbital 互斥）
            if (v.Length >= 23 && !string.IsNullOrWhiteSpace(v[22]))
            {
                int isParabolic = int.Parse(v[22].Trim());
                if (isParabolic > 0)
                {
                    data.IsParabolic = true;
                    data.ArcHeight = (v.Length >= 24 && !string.IsNullOrWhiteSpace(v[23])) ? float.Parse(v[23].Trim()) : 2f;
                }
            }

            // 發射來源：留空或 Player = 從玩家位置發射；Offscreen = 從攝影機視野外隨機方向飛入
            entry.LaunchSource = LaunchSource.Player;
            if (v.Length >= 25 && !string.IsNullOrWhiteSpace(v[24]))
            {
                entry.LaunchSource = ParseLaunchSource(v[24].Trim());
            }

            // 拋物線專用：落點隨機半徑（世界單位），實際落點 = 目標 + Random.insideUnitCircle * 半徑
            if (v.Length >= 26 && !string.IsNullOrWhiteSpace(v[25]))
            {
                data.LandingScatterRadius = float.Parse(v[25].Trim());
            }

            // 雷射光束：留空或 0 = 一般彈道；1 = 啟用持續型雷射（與 IsOrbital / IsParabolic 互斥）
            if (v.Length >= 27 && !string.IsNullOrWhiteSpace(v[26]))
            {
                int isLaser = int.Parse(v[26].Trim());
                if (isLaser > 0)
                {
                    data.IsLaser = true;
                    data.DotInterval = (v.Length >= 28 && !string.IsNullOrWhiteSpace(v[27])) ? float.Parse(v[27].Trim()) : 0.5f;
                    data.BeamRange = (v.Length >= 29 && !string.IsNullOrWhiteSpace(v[28])) ? float.Parse(v[28].Trim()) : 20f;
                }
            }

            // 拋物線落地殺傷半徑：> 0 時，拋物線彈落地瞬間以武器 Damage 對半徑內怪物炸一次（在 PlayerController.HandleParabolicLanded 結算）
            if (v.Length >= 30 && !string.IsNullOrWhiteSpace(v[29]))
            {
                entry.BlastRadius = float.Parse(v[29].Trim());
            }

            // 軌跡點間距：> 0 時，子彈每飛這麼遠就觸發 OnTrailPoint，主遊戲沿路種特效（地刺武器靠這個沿路長出尖刺）
            if (v.Length >= 31 && !string.IsNullOrWhiteSpace(v[30]))
            {
                data.TrailStep = float.Parse(v[30].Trim());
            }

            // 佛光型武器：留空 / 0 = 一般武器；1 = 佛光（不發射子彈，改維持跟隨玩家的 GroundEffect 圓形 AOE）。
            // 與 IsOrbital / IsParabolic / IsLaser 互斥；圓的定義由本列 GroundEffectID 指向 GroundEffectTable，傷害走武器表 Damage。
            entry.IsAura = false;
            if (v.Length >= 32 && !string.IsNullOrWhiteSpace(v[31]))
            {
                entry.IsAura = int.Parse(v[31].Trim()) != 0;
            }

            // 連鎖閃電：留空 / 0 = 否；1 = 連鎖閃電。跳躍次數 = 上面的 MaxBounces 欄（v[13]），第一段射程 = BeamRange 欄。
            entry.IsChain = false;
            if (v.Length >= 33 && !string.IsNullOrWhiteSpace(v[32]))
            {
                entry.IsChain = int.Parse(v[32].Trim()) != 0;
            }
            if (entry.IsChain)
            {
                entry.ChainCount = maxBounces;  // 跳躍次數沿用 MaxBounces 欄
                entry.ChainRadius = (v.Length >= 34 && !string.IsNullOrWhiteSpace(v[33])) ? float.Parse(v[33].Trim()) : 4f;
                // 第一段射程：沿用 BeamRange 欄（與雷射同欄）；留空預設 20
                data.BeamRange = (v.Length >= 29 && !string.IsNullOrWhiteSpace(v[28])) ? float.Parse(v[28].Trim()) : 20f;
            }

            // 落雷模式：留空 / 0 = 否；1 = 從畫面上緣劈下到滑鼠點。AOE 半徑沿用 BlastRadius 欄。
            entry.IsSkyStrike = false;
            if (v.Length >= 35 && !string.IsNullOrWhiteSpace(v[34]))
            {
                entry.IsSkyStrike = int.Parse(v[34].Trim()) != 0;
            }

            // 命中迸發子武器：留空 / 0 = 不觸發；> 0 = 命中時在命中點生成此「武器表 ID」的子武器（自帶外型/傷害）。
            entry.SubWeaponOnHit = 0;
            if (v.Length >= 36 && !string.IsNullOrWhiteSpace(v[35]))
            {
                entry.SubWeaponOnHit = int.Parse(v[35].Trim());
            }
            // 迸發過濾：留空 / Enemy = 打到敵人才迸；Environment = 打到牆/家具才迸；All = 任一都迸。
            entry.SubWeaponHitTarget = SubWeaponHitTarget.Enemy;
            if (v.Length >= 37 && !string.IsNullOrWhiteSpace(v[36]))
            {
                entry.SubWeaponHitTarget = ParseSubWeaponHitTarget(v[36].Trim());
            }

            // 召喚型武器：留空 / 0 = 否；1 = 召喚（施放時生怪、不發射子彈，冷卻沿用 FireInterval 欄）。
            entry.IsSummon = false;
            if (v.Length >= 38 && !string.IsNullOrWhiteSpace(v[37]))
            {
                entry.IsSummon = int.Parse(v[37].Trim()) != 0;
            }
            if (entry.IsSummon)
            {
                entry.SummonIds = ParseIntListPipe(v.Length >= 39 ? v[38] : null);
                entry.SummonCount = (v.Length >= 40 && !string.IsNullOrWhiteSpace(v[39])) ? int.Parse(v[39].Trim()) : 1;
                entry.SummonMaxAlive = (v.Length >= 41 && !string.IsNullOrWhiteSpace(v[40])) ? int.Parse(v[40].Trim()) : 4;
                entry.SummonRadius = (v.Length >= 42 && !string.IsNullOrWhiteSpace(v[41])) ? float.Parse(v[41].Trim()) : 2f;
            }

            // 定點法陣（第 42 欄）：滑鼠位置生成 GroundEffect。射程沿用 BeamRange（空=8）。
            entry.IsGroundCast = v.Length >= 43 && !string.IsNullOrWhiteSpace(v[42]) && int.Parse(v[42].Trim()) != 0;
            if (entry.IsGroundCast)
                data.BeamRange = (v.Length >= 29 && !string.IsNullOrWhiteSpace(v[28])) ? float.Parse(v[28].Trim()) : 8f;

            // 近身扇形（第 43～44 欄）：攻擊半徑沿用 BlastRadius，角度空=100。
            entry.IsMelee = v.Length >= 44 && !string.IsNullOrWhiteSpace(v[43]) && int.Parse(v[43].Trim()) != 0;
            entry.MeleeAngle = (v.Length >= 45 && !string.IsNullOrWhiteSpace(v[44])) ? float.Parse(v[44].Trim()) : 100f;

            // 突進斬（第 45～47 欄）：距離空=4、掃擊寬空=1。
            entry.IsDash = v.Length >= 46 && !string.IsNullOrWhiteSpace(v[45]) && int.Parse(v[45].Trim()) != 0;
            entry.DashDistance = (v.Length >= 47 && !string.IsNullOrWhiteSpace(v[46])) ? float.Parse(v[46].Trim()) : 4f;
            entry.DashWidth = (v.Length >= 48 && !string.IsNullOrWhiteSpace(v[47])) ? float.Parse(v[47].Trim()) : 1f;
            entry.UseSegmentedSkyStrike = v.Length >= 49 && !string.IsNullOrWhiteSpace(v[48])
                                           && int.Parse(v[48].Trim()) != 0;

            // 集氣模式（第 50 欄）：留空 / 0 / false = 關；1 / true = 開。
            entry.IsChargeMode = v.Length >= 50 && ParseOptionalBool(v[49]);
            if (entry.IsChargeMode && (data.IsLaser || entry.IsAura))
            {
                Debug.LogWarning($"Recipe {entry.ID} '{entry.Name}' 的集氣模式與持續型武器互斥，已自動停用。");
                entry.IsChargeMode = false;
            }
            entry.ChargeTimeReductionPercent = v.Length >= 51 ? ParsePercent(v[50]) : 0f;

            entry.Data = data;
            _recipes[entry.ID] = entry;
        }

        Debug.Log($"Loaded {_recipes.Count} recipes from CSV.");
    }

    // 解析用 | 分隔的整數清單（召喚 ID 池用；CSV 是逗號分隔，故名單改用 | 避開）。空 = 空陣列。
    private static int[] ParseIntListPipe(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new int[0];
        string[] parts = s.Split('|');
        List<int> list = new List<int>();
        foreach (string p in parts)
            if (int.TryParse(p.Trim(), out int id)) list.Add(id);
        return list.ToArray();
    }

    private static bool ParseOptionalBool(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string normalized = value.Trim();
        return normalized == "1" || normalized.Equals("true", System.StringComparison.OrdinalIgnoreCase);
    }

    private static float ParsePercent(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0f;
        string normalized = value.Trim();
        if (normalized.EndsWith("%"))
            normalized = normalized.Substring(0, normalized.Length - 1).Trim();
        if (!float.TryParse(normalized, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float percent))
        {
            Debug.LogWarning($"無法解析集氣時間縮減 '{value}'，已使用 0%。");
            return 0f;
        }
        // 100% 會讓集氣時間歸零，因此上限設為 99%；負值代表延長，最低容許 -1000%。
        return Mathf.Clamp(percent, -1000f, 99f);
    }

    private void ResolveSubRecipes()
    {
        foreach (var kvp in _recipes)
        {
            var entry = kvp.Value;
            if (entry.SubRecipeID > 0 && _recipes.TryGetValue(entry.SubRecipeID, out RecipeEntry subEntry))
            {
                entry.Data.SubProjectileData = subEntry.Data;
                entry.SubRecipe = subEntry;   // 保留子配方參考（落雷接連鎖時會讀 sub 的 IsChain/ChainCount/ChainRadius）
            }
        }
    }

    private void AutoFillMissingSubRecipes()
    {
        foreach (var kvp in _recipes)
        {
            var data = kvp.Value.Data;
            if (!data.HasSplit || data.SubProjectileData != null) continue;

            var sub = new ProjectileData();
            sub.Speed = data.Speed;
            sub.Radius = data.Radius;
            sub.LifeTime = data.LifeTime;
            sub.FireInterval = data.FireInterval;
            sub.RotationSpeed = data.RotationSpeed;
            sub.PierceCount = data.PierceCount;
            sub.HasBounce = data.HasBounce;
            sub.MaxBounces = data.MaxBounces;
            sub.HasHoming = data.HasHoming;
            sub.HomingTurnSpeed = data.HomingTurnSpeed;
            sub.IsOrbital = data.IsOrbital;
            sub.OrbitalRadius = data.OrbitalRadius;
            sub.OrbitalCount = data.OrbitalCount;
            sub.TrailStep = data.TrailStep;
            data.SubProjectileData = sub;
        }
    }

    private static BounceTarget ParseBounceTarget(string value)
    {
        return value switch
        {
            "Environment" => BounceTarget.Environment,
            "Enemy" => BounceTarget.Enemy,
            _ => BounceTarget.None
        };
    }

    private static SplitTiming ParseSplitTiming(string value)
    {
        return value switch
        {
            "OnSpawn" => SplitTiming.OnSpawn,
            "OnHit" => SplitTiming.OnHit,
            "OnDeath" => SplitTiming.OnDeath,
            _ => SplitTiming.OnSpawn
        };
    }

    private static GroundEffectTrigger ParseGroundEffectTrigger(string value)
    {
        return value switch
        {
            "OnSpawn" => GroundEffectTrigger.OnSpawn,
            "OnHit" => GroundEffectTrigger.OnHit,
            "OnDeath" => GroundEffectTrigger.OnDeath,
            _ => GroundEffectTrigger.OnHit
        };
    }

    private static GroundEffectHitTarget ParseGroundEffectHitTarget(string value)
    {
        return value switch
        {
            "Environment" => GroundEffectHitTarget.Environment,
            "Any" => GroundEffectHitTarget.Any,
            "Ground" => GroundEffectHitTarget.Ground,
            "Enemy" => GroundEffectHitTarget.Enemy,
            _ => GroundEffectHitTarget.Enemy
        };
    }

    private static LaunchSource ParseLaunchSource(string value)
    {
        return value switch
        {
            "Offscreen" => LaunchSource.Offscreen,
            "Player" => LaunchSource.Player,
            _ => LaunchSource.Player
        };
    }

    private static SubWeaponHitTarget ParseSubWeaponHitTarget(string value)
    {
        return value switch
        {
            "Environment" => SubWeaponHitTarget.Environment,
            "All" => SubWeaponHitTarget.All,
            "Any" => SubWeaponHitTarget.All,   // 容錯：Any 視同 All
            "Enemy" => SubWeaponHitTarget.Enemy,
            _ => SubWeaponHitTarget.Enemy
        };
    }
}
