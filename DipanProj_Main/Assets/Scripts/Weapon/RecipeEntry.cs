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

    // ── 起點等級（2026-11-17 裝備體系，見 readme/EQUIPMENT.md §4、§9）──
    // CSV 的行為欄填的是「等級」，讀表時換算成數值放進 Data／下面的 BurstCount 等欄（下游照舊讀數值）。
    // 這裡留著起點等級，是因為能力容器要「起點等級＋全身效果等級」加總後再查一次表。
    public int PierceLv, BounceLv, SplitLv, HomingLv, BurstLv, ParallelLv;
    /// <summary>分裂相鄰兩顆的角度（度）。總角度＝間距×（顆數－1），超過 <see cref="SpreadMaxAngle"/> 就壓縮。</summary>
    public float SpreadSpacing = DefaultSpreadSpacing;
    /// <summary>分裂扇形的總角度上限（度）；廣角效果加在這裡之外。</summary>
    public float SpreadMaxAngle = DefaultSpreadMaxAngle;
    public const float DefaultSpreadSpacing = 15f;
    public const float DefaultSpreadMaxAngle = 90f;

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
    /// <summary>true = 池裡**每個 ID 各召一隻**（忽略 <see cref="SummonCount"/>、不重複抽、生成角度平均分開）。
    /// 給「一口氣把一整組怪叫齊」的大絕用（紅嫁衣的家人齊聚）；false = 照 SummonCount 隨機抽。</summary>
    public bool SummonEachOnce = false;
    /// <summary>true = 在**滑鼠點的位置**召喚（離施放者超過 <c>Data.BeamRange</c>＝CSV 的 Range 就拉回射程邊緣，空=8；
    /// 點在牆上／水裡就找最近站得住的點）；false = 在施放者身邊一圈（舊行為）。墨家機關塔用（2026-11-24）。</summary>
    public bool SummonAtCursor = false;

    // ── 定點法陣一圈（Mode=GroundCast，2026-11-25 克蘇魯的古神黑洞）──
    /// <summary>&gt; 0＝不看滑鼠，以**施放者腳底**為圓心、這個半徑（× BulletScale）排一圈放 <see cref="CastCount"/> 個地面特效；
    /// 第一個在正上方、其餘平均分開（左右對稱）。留空／0＝舊行為（滑鼠處放 1 個，CastCount 不看）。</summary>
    public float CastRingRadius = 0f;
    /// <summary>一圈放幾個（只在 <see cref="CastRingRadius"/> &gt; 0 時有效）；留空＝1。</summary>
    public int CastCount = 1;

    // ── 骨牢（Mode=Cage）──
    /// <summary>困住幾秒；時間到牢籠崩裂並結算一次傷害。</summary>
    public float CageSeconds = 5f;
    /// <summary>這根武器同時最多關住幾隻（關滿就不再施放、也不扣魔）。**能力珠可加**。</summary>
    public int CageMaxTargets = 1;
    /// <summary>從玩家身上量，這個半徑內隨機挑一隻還沒被關的怪。</summary>
    public float CageRadius = 8f;
    /// <summary>崩裂傷害 = 武器 Damage × 這個倍率。</summary>
    public float CageBurstMul = 5f;

    // ── 近戰 / 突進 ──
    public float MeleeAngle = 100f;
    public float DashDistance = 4f;
    public float DashWidth = 1f;

    // ── 集氣 ──
    public bool ChargeMode = false;
    /// <summary>集氣時間縮減百分比：30 = 減少 30%；-20 = 延長 20%。</summary>
    public float ChargeTimeReduction = 0f;

    // ── 連擊 ──
    /// <summary>扣一次扳機連射幾發；1＝不連擊。第一發走正常發射，其餘由 PlayerController 依 <see cref="BurstInterval"/> 補出去。</summary>
    public int BurstCount = 1;
    public float BurstInterval = 0.1f;

    // ── 平行彈 ──
    /// <summary>同方向並排幾道；1＝不平行。全從玩家出生、飛出去前 0.15 秒散開拉直（LaneBehavior）。</summary>
    public int ParallelCount = 1;
    public float ParallelSpacing = 0.45f;
    public float ParallelMaxWidth = 3f;

    // ── 迴旋（Mode=Boomerang）──
    /// <summary>迴旋的 Range 留空（或 ≤0）時用的每趟往外距離。</summary>
    public const float BoomerangDefaultRange = 5f;
    /// <summary>浮游（Mode=Familiar）的 Range 留空時的索敵半徑（從玩家身上量）。</summary>
    public const float FamiliarDefaultRange = 8f;
    /// <summary>趟數：飛出去再回到玩家身上＝1；&gt;1 會穿過玩家往身後再飛（鐘擺）。每趟往外距離＝<c>Data.BeamRange</c>（CSV 的 Range）。</summary>
    public int BoomerangCount = 1;

    // ── 掃射（Mode=Sweep）──
    /// <summary>掃射的 Range 留空時的最遠掃描距離（從腳底量）。</summary>
    public const float SweepDefaultRange = 12f;
    /// <summary>靈獸（Mode=Spirit）留空時的游速、存活秒數（2026-11-19）。</summary>
    public const float SpiritDefaultSpeed = 4f, SpiritDefaultLifeTime = 10f;
    /// <summary>光束落點沿地面往前推的速度（每秒世界單位）。</summary>
    public float SweepSpeed = 12f;
    /// <summary>光束源點高度：從施放者腳底往上幾個「可見身高」。</summary>
    public float SweepOriginHeight = 1.8f;
    /// <summary>掃過的線上每隔多遠排一顆爆炸（世界單位）。</summary>
    public float ExplodeSpacing = 1.2f;
    /// <summary>光束掃過某一點之後再過幾秒才在那裡爆炸。</summary>
    public float ExplodeDelay = 0.3f;

    // ── 頭頂投彈（Mode=Overhead）──
    /// <summary>頭頂投彈的 Range 留空時的索敵半徑（從施放者量）。</summary>
    public const float OverheadDefaultRange = 6f;
    /// <summary>一發最多打幾個目標（由近到遠）；0＝不限（OverheadDrop 另有效能上限）。</summary>
    public int MaxTargets = 0;
    /// <summary>各目標頭上的投彈者在 0～此秒數內隨機陸續冒出。</summary>
    public float DropStagger = 0.4f;

    // ── 狀態效果（2026-11-18，readme/STATUS_EFFECT.md §4、§6）──
    /// <summary>配方上的狀態效果起點（原始清單字串「名稱:等級;…」，查 GemTable 狀態效果列）。能力容器把它跟全身效果等級相加；怪物武器直接用。</summary>
    public string StatusLv = "";
    /// <summary>DoT 算法：true＝狀態表固定值；false（預設）＝命中那一下的傷害×狀態表百分比。</summary>
    public bool StatusDmgFlat;

    /// <summary>持續型（按住生效）：Laser / Aura。沒有發射間隔、不吃集氣。</summary>
    public bool IsContinuous => WeaponModeSpec.Get(Mode).Continuous;

    /// <summary>
    /// 佛光的結算節拍倍率（不是 CSV 欄）：佛光每一下的間隔寫在地面特效表（GroundEffectTable.DamageInterval），
    /// 攻速效果由能力容器算成 1 ÷（1＋攻速總和）放這裡，生成佛光時帶給 <see cref="GroundEffectInstance.IntervalScale"/>。1＝照表。
    /// </summary>
    public float AuraIntervalScale = 1f;

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
        d.Speed = Fl("Speed", mode == WeaponMode.Spirit ? SpiritDefaultSpeed : 15f);
        d.Radius = Fl("Radius", 0.1f);
        d.LifeTime = Fl("LifeTime", mode == WeaponMode.Spirit ? SpiritDefaultLifeTime : 3f);
        d.RotationSpeed = Fl("RotationSpeed", 0f);
        // 行為欄填的是等級（2026-11-17）：讀進來馬上換算成數值，下游照舊讀數值。換算曲線＝GemTable（見 EquipAttributes.LevelValue）。
        e.PierceLv = Math.Max(0, In("PierceLv", 0));
        d.PierceCount = LvToInt("Pierce", e.PierceLv);
        e.BlockedByEnvironment = Bo("BlockedByEnvironment", true);

        // ── 多發 ──
        e.SplitLv = Math.Max(0, In("SplitLv", 0));
        int spreadCount = 1 + LvToInt("Split", e.SplitLv);
        d.SplitCount = Math.Max(1, spreadCount);
        e.SpreadSpacing = Math.Max(0f, Fl("SpreadSpacing", DefaultSpreadSpacing));
        e.SpreadMaxAngle = Math.Max(0f, Math.Min(360f, Fl("SpreadMaxAngle", DefaultSpreadMaxAngle)));
        // 沒有扇形欄的模式（頭頂投彈：每個投彈者吐幾顆、不分扇形）維持 0，跟改版前一樣
        d.SpreadAngle = WeaponModeSpec.IsEffective(mode, "SpreadMaxAngle")
            ? ComputeSpreadAngle(d.SplitCount, e.SpreadSpacing, e.SpreadMaxAngle, 0f) : 0f;
        string timingStr = G("SplitTiming");
        // 分裂行為（SplitBehavior）只有會飛的子彈用；雷射只認 OnHit（命中分裂），道數是直接讀 SplitCount 展開的；
        // 其他模式（拋物線/連鎖/落雷）也是直接讀 SplitCount 當顆數/道數，不走 SplitBehavior。
        if (mode == WeaponMode.Normal || mode == WeaponMode.Orbital || mode == WeaponMode.Familiar)
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
        e.BounceLv = Math.Max(0, In("BounceLv", 0));
        int maxBounces = LvToInt("Bounce", e.BounceLv);
        if (maxBounces > 0)
        {
            // 有反彈等級但反彈對象留空＝對牆（跟鑲反彈珠同一條規則）
            if (e.BounceTarget == BounceTarget.None) e.BounceTarget = BounceTarget.Environment;
            d.HasBounce = true;
            d.MaxBounces = maxBounces;
        }

        // ── 追蹤 ──
        e.HomingLv = Math.Max(0, In("HomingLv", 0));
        float homing = EquipAttributes.LevelValue("Homing", e.HomingLv);
        if (homing > 0f) { d.HasHoming = true; d.HomingTurnSpeed = homing; }
        d.HomingRadius = Fl("HomingRadius", HomingBehavior.DefaultAcquireRadius);   // 2026-11-09：<50＝只追附近、在最近 3 隻裡隨機挑
        d.WanderAngle = Math.Max(0f, Fl("WanderAngle", 0f));                          // 2026-11-09：左右蛇行幅度（度）

        // ── 射程 / 範圍 ──
        d.BeamRange = Fl("Range", mode == WeaponMode.GroundCast ? 8f : mode == WeaponMode.Boomerang ? BoomerangDefaultRange
                                 : mode == WeaponMode.Familiar ? FamiliarDefaultRange
                                 : mode == WeaponMode.Sweep ? SweepDefaultRange
                                 : mode == WeaponMode.Overhead ? OverheadDefaultRange
                                 : mode == WeaponMode.Summon ? 8f : 20f);   // Summon：SummonAtCursor 的最遠施放距離（同 GroundCast）
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
        d.FlightTime = Fl("FlightTime", mode == WeaponMode.Overhead ? 0.35f : 1f);   // 頭頂投彈＝炸彈掉落秒數，預設要短
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
        e.SummonEachOnce = Bo("SummonEachOnce", false);
        e.SummonAtCursor = Bo("SummonAtCursor", false);

        // ── 定點法陣一圈 ──
        e.CastRingRadius = Fl("CastRingRadius", 0f);
        e.CastCount = In("CastCount", 1);

        // ── 骨牢 ──
        e.CageSeconds = Fl("CageSeconds", 5f);
        e.CageMaxTargets = In("CageMaxTargets", 1);
        e.CageRadius = Fl("CageRadius", 8f);
        e.CageBurstMul = Fl("CageBurstMul", 5f);

        // ── 近戰 / 突進 ──
        e.MeleeAngle = Fl("MeleeAngle", 100f);
        e.DashDistance = Fl("DashDistance", 4f);
        e.DashWidth = Fl("DashWidth", 1f);

        // ── 集氣（持續型的 ChargeMode 欄本來就無效 → G() 回 null → false）──
        e.ChargeMode = Bo("ChargeMode", false);
        e.ChargeTimeReduction = Math.Max(-1000f, Math.Min(99f, CsvFieldParse.Percent(G("ChargeTimeReduction"), 0f)));

        // ── 連擊（Laser/Aura/Orbital 欄無效 → 保持 1）──
        e.BurstLv = Math.Max(0, In("BurstLv", 0));
        e.BurstCount = 1 + LvToInt("Burst", e.BurstLv);
        e.BurstInterval = Math.Max(0.02f, Fl("BurstInterval", 0.1f));

        // ── 平行彈（只有 Normal/Parabolic/Familiar 欄有效 → 其他模式保持 1）──
        e.ParallelLv = Math.Max(0, In("ParallelLv", 0));
        e.ParallelCount = 1 + LvToInt("Parallel", e.ParallelLv);
        e.ParallelSpacing = Math.Max(0.05f, Fl("ParallelSpacing", 0.45f));
        e.ParallelMaxWidth = Math.Max(0.1f, Fl("ParallelMaxWidth", 3f));

        // ── 迴旋：穿牆＋無限穿怪是這個模式的本質，不讀表（PierceLv／BlockedByEnvironment 對它是無效欄）──
        e.BoomerangCount = Math.Max(1, In("BoomerangCount", 1));
        if (mode == WeaponMode.Boomerang)
        {
            d.PierceCount = -1;
            e.BlockedByEnvironment = false;
            if (d.BeamRange <= 0f) d.BeamRange = BoomerangDefaultRange;   // Range 填 -1／0 對迴旋沒有意義
        }

        // ── 掃射 ──
        e.SweepSpeed = Math.Max(0.5f, Fl("SweepSpeed", 12f));
        e.SweepOriginHeight = Math.Max(0f, Fl("SweepOriginHeight", 1.8f));
        e.ExplodeSpacing = Math.Max(0.1f, Fl("ExplodeSpacing", 1.2f));
        e.ExplodeDelay = Math.Max(0f, Fl("ExplodeDelay", 0.3f));
        if (mode == WeaponMode.Sweep && e.AreaRadius <= 0f) e.AreaRadius = 1f;   // 爆炸半徑留空＝1

        // ── 頭頂投彈 ──
        e.MaxTargets = Math.Max(0, In("MaxTargets", 0));
        e.DropStagger = Math.Max(0f, Fl("DropStagger", 0.4f));
        if (mode == WeaponMode.Overhead && e.AreaRadius <= 0f) e.AreaRadius = 1f;   // 爆炸半徑留空＝1
        if (mode == WeaponMode.Overhead && d.BeamRange <= 0f) d.BeamRange = OverheadDefaultRange;

        // ── 狀態效果（通用欄）──
        e.StatusLv = G("StatusLv") ?? "";
        string dmgCalc = G("StatusDmgCalc");
        if (!string.IsNullOrEmpty(dmgCalc))
        {
            if (string.Equals(dmgCalc, "Flat", StringComparison.OrdinalIgnoreCase)) e.StatusDmgFlat = true;
            else if (!string.Equals(dmgCalc, "Percent", StringComparison.OrdinalIgnoreCase))
                problems.Add($"[Warning] StatusDmgCalc「{dmgCalc}」不認得（Percent／Flat），先當 Percent。");
        }

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
            HomingRadius = src.HomingRadius,
            WanderAngle = src.WanderAngle,
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

    // ═══════════════════════════ 等級與分裂角度 ═══════════════════════════

    /// <summary>純等級屬性的「等級 → 整數數值」（穿透數、反彈次數、多幾顆…）。</summary>
    public static int LvToInt(string attribute, int level)
        => (int)Math.Round(EquipAttributes.LevelValue(attribute, level));

    /// <summary>
    /// 分裂扇形的總角度（2026-11-17，見 readme/EQUIPMENT.md §4.3）：
    /// 自然寬度＝間距×（顆數－1），超過上限就壓到上限，再加上廣角效果的角度。
    /// 到 360° 時回傳 360×（N－1）÷N：下游一律用「總角度÷（N－1）、置中展開」排，這樣剛好平均分一整圈、首尾不重疊
    /// （以前山崩地裂、夏綠蒂填 315° 就是在繞這個問題）。
    /// </summary>
    public static float ComputeSpreadAngle(int count, float spacing, float maxAngle, float extraAngle)
    {
        if (count <= 1) return 0f;
        float natural = (count - 1) * Math.Max(0f, spacing);
        float total = Math.Min(natural, Math.Max(0f, maxAngle)) + Math.Max(0f, extraAngle);
        if (total >= 359.999f) return 360f * (count - 1) / count;
        return total;
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
