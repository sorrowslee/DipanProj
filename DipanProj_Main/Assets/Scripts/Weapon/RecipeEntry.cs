using System;
using System.Collections.Generic;
using Sorrows.Ballistics;
using Dipan.Data;

public enum BounceTarget { None, Environment, Enemy }
public enum GroundEffectHitTarget { Enemy, Environment, Any, Ground }
public enum LaunchSource { Player, Offscreen }
// 命中迸發子武器的觸發過濾：打到敵人 / 環境(牆壁+可破壞地上物) / All(任一都觸發)
public enum SubWeaponHitTarget { Enemy, Environment, All }

/// <summary>
/// 一列配方（RecipeTable.csv）解析後的資料。
///
/// <para><b>2026-08-26 大改</b>：10 個 <c>IsXxx</c> 旗標收成一欄 <see cref="Mode"/>（<see cref="WeaponMode"/>），
/// 互斥由 enum 保證；借用欄位的語意錯位改成獨立欄（<see cref="ChainCount"/>／<see cref="AimConeAngle"/>／
/// <see cref="SnapRadius"/>／<c>ProjectileData.FlightTime</c>）；<c>BeamRange→Range</c>（存在 <c>Data.BeamRange</c>）、
/// <c>BlastRadius→AreaRadius</c>。每種模式吃哪些欄看 <see cref="WeaponModeSpec"/>。</para>
///
/// <para><b>建構只有一條路：<see cref="FromFields"/></b>（欄名 → 原始字串）。CSV 走它、之後的「武器效果模擬」面板也走它，
/// 兩邊做出來的配方保證一樣。無效於該模式的欄位在這裡就不讀（不只是警告），所以「表上填錯」不可能改變行為。</para>
///
/// <para>彈道系統看得懂的欄位住在 <see cref="Data"/>（<c>ProjectileData</c>）；主遊戲側自己結算的欄位直接掛在本類別。</para>
/// </summary>
public class RecipeEntry
{
    public int ID;
    public string Name;
    /// <summary>發射模式（互斥）。</summary>
    public WeaponMode Mode = WeaponMode.Normal;
    public ProjectileData Data;

    public BounceTarget BounceTarget = BounceTarget.None;
    public int SubRecipeID = -1;
    public RecipeEntry SubRecipe;   // SubRecipeID 解析後的子配方參考（落雷接連鎖時讀 sub 的 Mode/ChainCount/ChainRadius）
    public bool BlockedByEnvironment = true;

    public int GroundEffectID = 0;
    public GroundEffectHitTarget GroundEffectHitTarget = GroundEffectHitTarget.Enemy;
    public LaunchSource LaunchSource = LaunchSource.Player;

    /// <summary>範圍半徑：Parabolic 落地爆炸／SkyStrike 落雷 AOE／Melee 扇形半徑（原 BlastRadius）。0 = 無。</summary>
    public float AreaRadius = 0f;

    // ── 連鎖閃電 ──
    public int ChainCount = 0;        // 跳躍次數（總命中數 = 1 + ChainCount）
    public float ChainRadius = 4f;    // 每跳的搜尋半徑
    public float AimConeAngle = 0f;   // 首段鎖定錐半角（度）；0 = 要正好瞄到；180 = 鎖最近任意方向

    // ── 落雷 ──
    public float SnapRadius = 0f;     // 落點吸附半徑（世界單位）；0 = 不吸附
    public bool SegmentedColumn = false;   // 分段全高雷柱（原 UseSegmentedSkyStrike）

    // ── 命中迸發子武器 ──
    public int SubWeaponOnHit = 0;
    public SubWeaponHitTarget SubWeaponHitTarget = SubWeaponHitTarget.Enemy;

    // ── 召喚 ──
    public int[] SummonIds = new int[0];
    public int SummonCount = 1;
    public int SummonMaxAlive = 4;
    public float SummonRadius = 2f;

    // ── 近戰 / 突進 ──
    public float MeleeAngle = 100f;
    public float DashDistance = 4f;
    public float DashWidth = 1f;

    // ── 集氣 ──
    public bool ChargeMode = false;
    /// <summary>集氣時間縮減百分比：30 = 減少 30%；-20 = 延長 20%。</summary>
    public float ChargeTimeReduction = 0f;

    /// <summary>持續型（按住生效）：Laser / Aura。沒有發射間隔、不吃集氣。</summary>
    public bool IsContinuous => WeaponModeSpec.Get(Mode).Continuous;

    // ═══════════════════════════ 建構 ═══════════════════════════

    /// <summary>
    /// 從「欄名 → 原始字串」建一列配方。這是**唯一**的建構入口（CSV 與模擬面板共用）。
    /// 找不到的欄／空白一律用預設；對該模式無效的欄**不讀**；發現的問題（[Error]/[Warning] 開頭）加進 <paramref name="problems"/>。
    /// </summary>
    public static RecipeEntry FromFields(IReadOnlyDictionary<string, string> fields, List<string> problems = null)
    {
        problems = problems ?? new List<string>();
        var e = new RecipeEntry();
        var d = new ProjectileData();
        e.Data = d;

        Func<string, string> Raw = col =>
        {
            string v;
            return (fields != null && fields.TryGetValue(col, out v) && !string.IsNullOrWhiteSpace(v)) ? v.Trim() : null;
        };

        e.ID = CsvFieldParse.Int(Raw("ID"), 0);
        e.Name = Raw("Name") ?? "";

        string modeStr = Raw("Mode");
        WeaponMode parsedMode;
        if (!WeaponModeSpec.TryParseMode(modeStr, out parsedMode))
        {
            problems.Add($"[Error] Mode「{modeStr}」不認得（可用：{string.Join("/", Enum.GetNames(typeof(WeaponMode)))}），先當 Normal。");
            parsedMode = WeaponMode.Normal;
        }
        e.Mode = parsedMode;
        WeaponMode mode = e.Mode;

        // 只讀「對此模式有效」的欄；其餘維持預設。這讓表上填錯不可能改變行為。
        Func<string, string> G = col => WeaponModeSpec.IsEffective(mode, col) ? Raw(col) : null;
        Func<string, float, float> Fl = (col, def) => CsvFieldParse.Float(G(col), def);
        Func<string, int, int> In = (col, def) => CsvFieldParse.Int(G(col), def);
        Func<string, bool, bool> Bo = (col, def) => CsvFieldParse.Bool(G(col), def);

        // ── 通用 / 子彈本體 ──
        d.FireInterval = Fl("FireInterval", 0.3f);
        d.Speed = Fl("Speed", 15f);
        d.Radius = Fl("Radius", 0.1f);
        d.LifeTime = Fl("LifeTime", 3f);
        d.RotationSpeed = Fl("RotationSpeed", 0f);
        d.PierceCount = In("PierceCount", 0);
        e.BlockedByEnvironment = Bo("BlockedByEnvironment", true);

        // ── 多發 ──
        int spreadCount = In("SpreadCount", 1);
        d.SplitCount = Math.Max(1, spreadCount);
        d.SpreadAngle = Fl("SpreadAngle", 0f);
        string timingStr = G("SplitTiming");
        // 分裂行為（SplitBehavior）只有會飛的子彈用；雷射只認 OnHit（命中分裂），道數是直接讀 SplitCount 展開的；
        // 其他模式（拋物線/連鎖/落雷）也是直接讀 SplitCount 當顆數/道數，不走 SplitBehavior。
        if (mode == WeaponMode.Normal || mode == WeaponMode.Orbital)
        {
            d.HasSplit = spreadCount > 1;
            d.Timing = ParseSplitTiming(timingStr);   // 留空 = OnSpawn
        }
        else if (mode == WeaponMode.Laser)
        {
            d.HasSplit = spreadCount > 1 && string.Equals(timingStr, "OnHit", StringComparison.OrdinalIgnoreCase);
            d.Timing = SplitTiming.OnHit;
        }
        string subStr = G("SubRecipeID");
        e.SubRecipeID = string.IsNullOrEmpty(subStr) ? -1 : CsvFieldParse.Int(subStr, -1);

        // ── 反彈 ──
        e.BounceTarget = ParseBounceTarget(G("BounceTarget"));
        int maxBounces = In("MaxBounces", 0);
        if (e.BounceTarget != BounceTarget.None && maxBounces > 0)
        {
            d.HasBounce = true;
            d.MaxBounces = maxBounces;
        }

        // ── 追蹤 ──
        float homing = Fl("HomingTurnSpeed", 0f);
        if (homing > 0f) { d.HasHoming = true; d.HomingTurnSpeed = homing; }

        // ── 射程 / 範圍 ──
        d.BeamRange = Fl("Range", mode == WeaponMode.GroundCast ? 8f : 20f);
        e.AreaRadius = Fl("AreaRadius", 0f);

        // ── 命中附加 ──
        e.GroundEffectID = In("GroundEffectID", 0);
        e.GroundEffectHitTarget = ParseGroundEffectHitTarget(G("GroundEffectHitTarget"));
        d.TrailStep = Fl("TrailStep", 0f);
        e.SubWeaponOnHit = In("SubWeaponOnHit", 0);
        e.SubWeaponHitTarget = ParseSubWeaponHitTarget(G("SubWeaponHitTarget"));

        // ── 環繞 ──
        d.IsOrbital = mode == WeaponMode.Orbital;
        d.OrbitalRadius = Fl("OrbitalRadius", 2f);
        d.OrbitalCount = In("OrbitalCount", 3);

        // ── 拋物線 ──
        d.IsParabolic = mode == WeaponMode.Parabolic;
        d.FlightTime = Fl("FlightTime", 1f);
        d.ArcHeight = Fl("ArcHeight", 2f);
        e.LaunchSource = ParseLaunchSource(G("LaunchSource"));
        d.LandingScatterRadius = Fl("LandingScatterRadius", 0f);

        // ── 雷射 ──
        d.IsLaser = mode == WeaponMode.Laser;
        d.DotInterval = Fl("DotInterval", 0.5f);

        // ── 連鎖 ──
        e.ChainCount = In("ChainCount", 0);
        e.ChainRadius = Fl("ChainRadius", 4f);
        e.AimConeAngle = Fl("AimConeAngle", 0f);

        // ── 落雷 ──
        e.SnapRadius = Fl("SnapRadius", 0f);
        e.SegmentedColumn = Bo("SegmentedColumn", false);

        // ── 召喚 ──
        e.SummonIds = CsvFieldParse.IntList(G("SummonIds"));
        e.SummonCount = In("SummonCount", 1);
        e.SummonMaxAlive = In("SummonMaxAlive", 4);
        e.SummonRadius = Fl("SummonRadius", 2f);

        // ── 近戰 / 突進 ──
        e.MeleeAngle = Fl("MeleeAngle", 100f);
        e.DashDistance = Fl("DashDistance", 4f);
        e.DashWidth = Fl("DashWidth", 1f);

        // ── 集氣（持續型的 ChargeMode 欄本來就無效 → G() 回 null → false）──
        e.ChargeMode = Bo("ChargeMode", false);
        e.ChargeTimeReduction = Math.Max(-1000f, Math.Min(99f, CsvFieldParse.Percent(G("ChargeTimeReduction"), 0f)));

        // 規格檢查（必填缺 / 無效卻有填）
        if (fields != null) problems.AddRange(WeaponModeSpec.Validate(mode, fields, FieldTable.Recipe));
        return e;
    }

    // ═══════════════════════════ 拷貝 ═══════════════════════════

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
        c.SummonIds = (int[])SummonIds?.Clone();
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
            FlightTime = src.FlightTime,
            ArcHeight = src.ArcHeight,
            LandingScatterRadius = src.LandingScatterRadius,
            IsLaser = src.IsLaser,
            DotInterval = src.DotInterval,
            BeamRange = src.BeamRange,
            TrailStep = src.TrailStep,
        };
    }

    // ═══════════════════════════ 解析小工具 ═══════════════════════════

    static BounceTarget ParseBounceTarget(string value)
    {
        if (string.IsNullOrEmpty(value)) return BounceTarget.None;
        if (value.Equals("Environment", StringComparison.OrdinalIgnoreCase)) return BounceTarget.Environment;
        if (value.Equals("Enemy", StringComparison.OrdinalIgnoreCase)) return BounceTarget.Enemy;
        return BounceTarget.None;
    }

    static SplitTiming ParseSplitTiming(string value)
    {
        if (string.IsNullOrEmpty(value)) return SplitTiming.OnSpawn;
        if (value.Equals("OnHit", StringComparison.OrdinalIgnoreCase)) return SplitTiming.OnHit;
        if (value.Equals("OnDeath", StringComparison.OrdinalIgnoreCase)) return SplitTiming.OnDeath;
        return SplitTiming.OnSpawn;
    }

    static GroundEffectHitTarget ParseGroundEffectHitTarget(string value)
    {
        if (string.IsNullOrEmpty(value)) return GroundEffectHitTarget.Enemy;
        if (value.Equals("Environment", StringComparison.OrdinalIgnoreCase)) return GroundEffectHitTarget.Environment;
        if (value.Equals("Any", StringComparison.OrdinalIgnoreCase)) return GroundEffectHitTarget.Any;
        if (value.Equals("Ground", StringComparison.OrdinalIgnoreCase)) return GroundEffectHitTarget.Ground;
        return GroundEffectHitTarget.Enemy;
    }

    static LaunchSource ParseLaunchSource(string value)
        => (!string.IsNullOrEmpty(value) && value.Equals("Offscreen", StringComparison.OrdinalIgnoreCase)) ? LaunchSource.Offscreen : LaunchSource.Player;

    static SubWeaponHitTarget ParseSubWeaponHitTarget(string value)
    {
        if (string.IsNullOrEmpty(value)) return SubWeaponHitTarget.Enemy;
        if (value.Equals("Environment", StringComparison.OrdinalIgnoreCase)) return SubWeaponHitTarget.Environment;
        if (value.Equals("All", StringComparison.OrdinalIgnoreCase) || value.Equals("Any", StringComparison.OrdinalIgnoreCase)) return SubWeaponHitTarget.All;
        return SubWeaponHitTarget.Enemy;
    }
}
