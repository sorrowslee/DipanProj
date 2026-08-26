using System;
using System.Collections.Generic;

/// <summary>
/// 武器的「發射模式」。RecipeTable 的 <c>Mode</c> 欄一列只能填一種——互斥由這個 enum 保證，
/// 不再靠 10 個 <c>IsXxx</c> 旗標與 if/else 順序。留空＝<see cref="Normal"/>。
/// 每種模式吃哪些欄位、哪些欄位必填、欄位在該模式叫什麼名字，全部寫在 <see cref="WeaponModeSpec"/>。
/// </summary>
public enum WeaponMode
{
    /// <summary>一般子彈：會飛、會撞、吃穿透／反彈／追蹤／分裂全套。</summary>
    Normal = 0,
    /// <summary>環繞：一組子彈繞著玩家轉（護盾）。</summary>
    Orbital,
    /// <summary>拋物線：丟炸彈，飛行中不撞任何東西，落地才炸／放地面特效。</summary>
    Parabolic,
    /// <summary>雷射：按住時維持光束（持續型）。</summary>
    Laser,
    /// <summary>佛光：按住時在玩家身上維持一圈跟著走的 AOE（持續型）。</summary>
    Aura,
    /// <summary>連鎖閃電：打中第一隻後逐跳。</summary>
    Chain,
    /// <summary>落雷：從畫面上緣劈到滑鼠點，落地圓形 AOE。</summary>
    SkyStrike,
    /// <summary>召喚：施放時在身邊生怪。</summary>
    Summon,
    /// <summary>定點法陣：在滑鼠位置放一個地面特效。</summary>
    GroundCast,
    /// <summary>近身扇形：以自己為圓心掃一個扇形。</summary>
    Melee,
    /// <summary>突進斬：往瞄準方向衝一段，掃過的都受傷。</summary>
    Dash,
}

/// <summary>欄位值的型別（給載入檢查與未來「武器效果模擬」面板產輸入框用）。</summary>
public enum FieldKind { Int, Float, Bool, Percent, Enum, Text, IntList }

/// <summary>欄位住在哪張表。</summary>
public enum FieldTable { Recipe, Weapon }

/// <summary>一個 CSV 欄位的規格：名字、型別、預設、範圍、分組、說明。</summary>
public sealed class FieldSpec
{
    public string Name;          // CSV 表頭的欄名（括號前）
    public FieldTable Table;
    public FieldKind Kind;
    public string Group;         // 表頭分組（給文件／面板分區用）
    public string Label;         // 通用中文顯示名（模式可覆寫，見 ModeSpec.Label）
    public string Default = "";  // 留空時的預設值（字串形式；空字串＝沒有預設）
    public float Min = float.NegativeInfinity;
    public float Max = float.PositiveInfinity;
    public string[] Options;     // Enum 型別的可選值
    public string Help = "";     // 一句說明
    /// <summary>所有模式都吃的欄（ID/Name/Mode/RecipeID…）：不做「無效卻有填」檢查。</summary>
    public bool Universal;
}

/// <summary>一種發射模式的規格：吃哪些欄、哪些必填、欄位在此模式的顯示名。</summary>
public sealed class ModeSpec
{
    public WeaponMode Mode;
    public string Label;        // 中文名
    public string Help;         // 一句說明
    /// <summary>持續型（按住生效、放開結束）：Laser / Aura。這類沒有發射間隔、不吃集氣。</summary>
    public bool Continuous;
    /// <summary>會發射「會飛的一般子彈」（可當 SubWeaponOnHit 的子武器）。</summary>
    public bool SpawnsBullets;

    internal readonly HashSet<string> Effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    internal readonly HashSet<string> Required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    internal ModeSpec Eff(params string[] fields) { foreach (var f in fields) Effective.Add(f); return this; }
    internal ModeSpec Req(params string[] fields) { foreach (var f in fields) { Required.Add(f); Effective.Add(f); } return this; }
    internal ModeSpec Lbl(string field, string label) { Labels[field] = label; return this; }
}

/// <summary>
/// **模式 × 欄位規格表——單一真相。** 三個地方共用，不要各寫各的：
/// <list type="number">
/// <item><b>載入檢查</b>（<c>RecipeManager</c>）：該模式無效的欄位有值 → Warning；必填欄缺 → Error。</item>
/// <item><b>能力珠有效性</b>（<c>PlayerAbilities</c>／鍛造介面）：珠子改的欄位對「目前裝備的武器」的模式無效 → 提示不擋、不套用。</item>
/// <item><b>武器效果模擬系統</b>（之後做）：選了 Mode 之後要顯示哪些欄、每欄叫什麼、型別／預設／範圍，全從這裡拿。</item>
/// </list>
/// 加新模式或新欄位：只改這個檔（<see cref="BuildFields"/> 加欄、<see cref="BuildModes"/> 加模式），
/// 檢查、珠子、面板就都跟上了。見 readme/RECIPE_DESCRIBE.md。
/// </summary>
public static class WeaponModeSpec
{
    // ─────────────────────────── 欄位 ───────────────────────────

    // ⚠ 這幾個共用欄組一定要宣告在 _fields/_modes 之前——C# 靜態欄位依宣告順序初始化，
    //   放在後面的話 BuildModes() 跑的時候它們還是 null（單元測試抓到過）。
    // 共用欄組，避免每個模式重抄一遍
    static readonly string[] BulletVisual = { "WeaponSpritePath", "SpriteAngleOffset", "WeaponAniPath", "WeaponAniNumber", "AnimFPS", "BulletScale" };
    static readonly string[] BeamVisual = { "BeamStyle", "BeamColor", "BeamWidth" };
    static readonly string[] Charge = { "ChargeMode", "ChargeTimeReduction" };
    static readonly string[] Multi = { "SpreadCount", "SpreadAngle" };

    static readonly List<FieldSpec> _fields = BuildFields();
    static readonly Dictionary<string, FieldSpec> _fieldByName = IndexFields(_fields);
    static readonly Dictionary<WeaponMode, ModeSpec> _modes = BuildModes();

    /// <summary>所有欄位（RecipeTable 在前、WeaponTable 在後，依表頭順序）。</summary>
    public static IReadOnlyList<FieldSpec> AllFields => _fields;

    public static IEnumerable<FieldSpec> FieldsOf(FieldTable table)
    {
        foreach (var f in _fields) if (f.Table == table) yield return f;
    }

    /// <summary>RecipeTable 表頭應有的欄名（依順序）。</summary>
    public static List<string> ColumnNames(FieldTable table)
    {
        var list = new List<string>();
        foreach (var f in _fields) if (f.Table == table) list.Add(f.Name);
        return list;
    }

    public static FieldSpec GetField(string name)
        => (!string.IsNullOrEmpty(name) && _fieldByName.TryGetValue(name, out var f)) ? f : null;

    // ─────────────────────────── 模式 ───────────────────────────

    public static IEnumerable<ModeSpec> AllModes => _modes.Values;

    public static ModeSpec Get(WeaponMode mode)
        => _modes.TryGetValue(mode, out var m) ? m : _modes[WeaponMode.Normal];

    public static string ModeLabel(WeaponMode mode) => Get(mode).Label;

    /// <summary>解析 CSV 的 Mode 欄：不分大小寫；空白＝Normal；認不得回 false（呼叫端印錯誤）。</summary>
    public static bool TryParseMode(string s, out WeaponMode mode)
    {
        mode = WeaponMode.Normal;
        if (string.IsNullOrWhiteSpace(s)) return true;
        s = s.Trim();
        foreach (WeaponMode m in Enum.GetValues(typeof(WeaponMode)))
        {
            if (string.Equals(m.ToString(), s, StringComparison.OrdinalIgnoreCase)) { mode = m; return true; }
        }
        // 也接受中文名
        foreach (var kv in _modes)
        {
            if (kv.Value.Label == s) { mode = kv.Key; return true; }
        }
        return false;
    }

    /// <summary>這個欄位對這個模式有沒有作用。通用欄（ID/Name/Mode…）一律 true；不認得的欄名回 false。</summary>
    public static bool IsEffective(WeaponMode mode, string field)
    {
        if (string.IsNullOrEmpty(field)) return false;
        var f = GetField(field);
        if (f != null && f.Universal) return true;
        return Get(mode).Effective.Contains(field);
    }

    /// <summary>這個模式下這個欄位是不是本體必填（例：Aura 的 GroundEffectID）。</summary>
    public static bool IsRequired(WeaponMode mode, string field)
        => !string.IsNullOrEmpty(field) && Get(mode).Required.Contains(field);

    /// <summary>欄位在該模式下的顯示名（模式有覆寫就用覆寫，否則用欄位通用名，再不然用欄名本身）。</summary>
    public static string Label(WeaponMode mode, string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (Get(mode).Labels.TryGetValue(field, out var l)) return l;
        var f = GetField(field);
        return (f != null && !string.IsNullOrEmpty(f.Label)) ? f.Label : field;
    }

    /// <summary>該模式吃的欄位（依表頭順序；含通用欄）。給文件產生器與模擬面板用。</summary>
    public static List<FieldSpec> EffectiveFields(WeaponMode mode, FieldTable? table = null)
    {
        var list = new List<FieldSpec>();
        foreach (var f in _fields)
        {
            if (table.HasValue && f.Table != table.Value) continue;
            if (f.Universal || Get(mode).Effective.Contains(f.Name)) list.Add(f);
        }
        return list;
    }

    // ─────────────────────────── 定義 ───────────────────────────

    static FieldSpec F(FieldTable t, string name, FieldKind kind, string group, string label, string def = "",
                       float min = float.NegativeInfinity, float max = float.PositiveInfinity, string help = "", string[] options = null, bool universal = false)
        => new FieldSpec { Table = t, Name = name, Kind = kind, Group = group, Label = label, Default = def, Min = min, Max = max, Help = help, Options = options, Universal = universal };

    static List<FieldSpec> BuildFields()
    {
        const FieldTable R = FieldTable.Recipe, W = FieldTable.Weapon;
        var L = new List<FieldSpec>
        {
            // ── RecipeTable：通用 ──
            F(R, "ID",           FieldKind.Int,   "通用", "配方 ID", "", 1, 99999, "武器表用 RecipeID 引用；不可重複", universal: true),
            F(R, "Name",         FieldKind.Text,  "通用", "配方名稱", "", help: "只給人看，程式不讀", universal: true),
            F(R, "Mode",         FieldKind.Enum,  "通用", "發射模式", "Normal", help: "一列只能一種；留空＝Normal", options: Enum.GetNames(typeof(WeaponMode)), universal: true),
            F(R, "FireInterval", FieldKind.Float, "通用", "發射間隔（秒）", "0.3", 0.02f, 60f, "越小射越快；Laser/Aura 無效（按住就在）"),

            // ── 子彈本體（Normal / Orbital；Parabolic 只吃 RotationSpeed）──
            F(R, "Speed",         FieldKind.Float, "子彈本體", "飛行速度", "15", 0.05f, 500f, "每秒世界單位；Orbital＝繞圈的切線速度"),
            F(R, "Radius",        FieldKind.Float, "子彈本體", "碰撞半徑", "0.1", 0.01f, 5f, "越大越容易打中；Laser 改用武器表 BeamWidth"),
            F(R, "LifeTime",      FieldKind.Float, "子彈本體", "存活秒數", "3", -1f, 600f, "-1＝不因時間消失；Orbital＝整組到期時間"),
            F(R, "RotationSpeed", FieldKind.Float, "子彈本體", "自轉速度（度/秒）", "0", -3600f, 3600f, "純視覺"),
            F(R, "PierceCount",   FieldKind.Int,   "子彈本體", "穿透數", "0", -1, 999, "0＝碰到就停；-1＝無限穿透"),
            F(R, "BlockedByEnvironment", FieldKind.Bool, "子彈本體", "被地形擋", "1", help: "0＝穿過牆（需 PierceCount≠0）"),

            // ── 多發 ──
            F(R, "SpreadCount", FieldKind.Int,   "多發", "分裂／散射數", "1", 1, 64, "一發幾顆／幾道；1＝不分"),
            F(R, "SpreadAngle", FieldKind.Float, "多發", "扇形總角度（度）", "0", 0f, 360f, "N 顆在此角度內平均展開"),
            F(R, "SplitTiming", FieldKind.Enum,  "多發", "分裂時機", "OnSpawn", help: "留空＝OnSpawn（發射時就分）；OnHit＝打中才分；OnDeath＝飛到底才分", options: new[] { "OnSpawn", "OnHit", "OnDeath" }),
            F(R, "SubRecipeID", FieldKind.Int,   "多發", "分裂子彈配方 ID", "", 0, 99999, "留空＝繼承自身；SkyStrike＝落點接連鎖的配方"),

            // ── 反彈 ──
            F(R, "BounceTarget", FieldKind.Enum, "反彈", "反彈對象", "None", help: "None／Environment（牆）／Enemy（怪）", options: new[] { "None", "Environment", "Enemy" }),
            F(R, "MaxBounces",   FieldKind.Int,  "反彈", "最大反彈次數", "0", 0, 999),

            // ── 追蹤 ──
            F(R, "HomingTurnSpeed", FieldKind.Float, "追蹤", "追蹤轉向速度（度/秒）", "0", 0f, 3600f, "0＝不追蹤；90 慢 180 中 360 快"),

            // ── 射程／範圍（語意一致的共用欄）──
            F(R, "Range",      FieldKind.Float, "射程範圍", "射程", "", -1f, 200f, "Laser 光束長度（-1＝無限）／Chain 首段射程／GroundCast 施放距離"),
            F(R, "AreaRadius", FieldKind.Float, "射程範圍", "範圍半徑", "", 0f, 50f, "Parabolic 落地爆炸／SkyStrike 落雷 AOE／Melee 扇形半徑"),

            // ── 命中附加 ──
            F(R, "GroundEffectID",        FieldKind.Int,  "命中附加", "地面特效 ID", "0", 0, 99999, "命中／落地時放一個 GroundEffect；Aura／GroundCast 則是本體必填"),
            F(R, "GroundEffectHitTarget", FieldKind.Enum, "命中附加", "地面特效觸發對象", "Enemy", help: "Enemy／Environment／Any／Ground（Parabolic 落地）", options: new[] { "Enemy", "Environment", "Any", "Ground" }),
            F(R, "TrailStep",             FieldKind.Float,"命中附加", "軌跡點間距", "0", 0f, 20f, ">0 時每飛這麼遠沿路種一個武器表 TrailEffectID 的特效（地刺）"),
            F(R, "SubWeaponOnHit",        FieldKind.Int,  "命中附加", "命中迸發子武器 ID", "0", 0, 99999, "武器表 ID；打中時在命中點射出那把武器"),
            F(R, "SubWeaponHitTarget",    FieldKind.Enum, "命中附加", "迸發觸發對象", "Enemy", help: "Enemy／Environment／All", options: new[] { "Enemy", "Environment", "All" }),

            // ── 環繞 ──
            F(R, "OrbitalRadius", FieldKind.Float, "環繞", "環繞半徑", "2", 0.1f, 20f),
            F(R, "OrbitalCount",  FieldKind.Int,   "環繞", "環繞數量", "3", 1, 64),

            // ── 拋物線 ──
            F(R, "FlightTime",           FieldKind.Float, "拋物線", "飛行秒數", "1", 0.05f, 30f, "不論遠近都飛這麼久才落地（原本借用 Speed 欄）"),
            F(R, "ArcHeight",            FieldKind.Float, "拋物線", "弧頂高度", "2", 0f, 50f, "視覺假高度（世界單位）"),
            F(R, "LaunchSource",         FieldKind.Enum,  "拋物線", "發射來源", "Player", help: "Player／Offscreen（從畫面外飛進來）", options: new[] { "Player", "Offscreen" }),
            F(R, "LandingScatterRadius", FieldKind.Float, "拋物線", "落點隨機半徑", "0", 0f, 20f),

            // ── 雷射 ──
            F(R, "DotInterval", FieldKind.Float, "雷射", "傷害節拍（秒）", "0.5", 0.02f, 10f, "光束每 N 秒對掃到的目標各結算一次"),

            // ── 連鎖 ──
            F(R, "ChainCount",   FieldKind.Int,   "連鎖", "跳躍次數", "0", 0, 999, "總命中＝1＋跳數（原本借用 MaxBounces 欄）"),
            F(R, "ChainRadius",  FieldKind.Float, "連鎖", "每跳搜尋半徑", "4", 0.1f, 50f),
            F(R, "AimConeAngle", FieldKind.Float, "連鎖", "首段鎖定錐半角（度）", "0", 0f, 180f, "0＝要正好瞄到；180＝鎖最近任意方向（原本借用 HomingTurnSpeed 欄）"),

            // ── 落雷 ──
            F(R, "SnapRadius",      FieldKind.Float, "落雷", "落點吸附半徑", "0", 0f, 50f, "落點附近這個半徑內有怪就吸過去（原本借用 HomingTurnSpeed 欄）"),
            F(R, "SegmentedColumn", FieldKind.Bool,  "落雷", "分段全高雷柱", "0", help: "1＝從畫面頂鋪到落點的分段雷柱（九霄雷獄）"),

            // ── 召喚 ──
            F(R, "SummonIds",      FieldKind.IntList, "召喚", "可召喚怪物 ID", "", help: "用 | 分隔，每次隨機抽一個"),
            F(R, "SummonCount",    FieldKind.Int,     "召喚", "每次召喚幾隻", "1", 1, 64),
            F(R, "SummonMaxAlive", FieldKind.Int,     "召喚", "同時存在上限", "4", 1, 999),
            F(R, "SummonRadius",   FieldKind.Float,   "召喚", "生成半徑", "2", 0.1f, 50f),

            // ── 近戰 ──
            F(R, "MeleeAngle", FieldKind.Float, "近戰", "扇形總角度（度）", "100", 1f, 360f),

            // ── 突進 ──
            F(R, "DashDistance", FieldKind.Float, "突進", "突進距離", "4", 0.1f, 50f),
            F(R, "DashWidth",    FieldKind.Float, "突進", "掃擊寬度", "1", 0.1f, 20f),

            // ── 集氣 ──
            F(R, "ChargeMode",          FieldKind.Bool,    "集氣", "集氣模式", "0", help: "1＝按住 3 秒放開才施放（傷害×3、視覺×2）；Laser/Aura 不可"),
            F(R, "ChargeTimeReduction", FieldKind.Percent, "集氣", "集氣時間縮減", "0%", -1000f, 99f, "30%＝縮短 30%；-20%＝延長 20%"),

            // ── WeaponTable：通用 ──
            F(W, "ID",       FieldKind.Int,   "通用", "武器 ID", "", 1, 99999, "與 ItemTable 同號", universal: true),
            F(W, "Name",     FieldKind.Text,  "通用", "武器名稱", "", universal: true),
            F(W, "Damage",   FieldKind.Float, "通用", "傷害", "1", 0f, 99999f, "Laser＝每次 DOT；Summon 不用"),
            F(W, "RecipeID", FieldKind.Int,   "通用", "配方 ID", "", 1, 99999, universal: true),
            F(W, "ManaCost", FieldKind.Float, "通用", "耗魔", "1", 0f, 9999f, "離散每發／持續型每秒", universal: true),

            // ── 子彈外觀 ──
            F(W, "WeaponSpritePath",  FieldKind.Text,  "子彈外觀", "子彈圖路徑", "", help: "相對 Resources、不含副檔名；與 WeaponAniPath 二擇一；留空＝隱形子彈"),
            F(W, "SpriteAngleOffset", FieldKind.Float, "子彈外觀", "圖片角度補正（度）", "0", -360f, 360f),
            F(W, "WeaponAniPath",     FieldKind.Text,  "子彈外觀", "序列圖前綴", ""),
            F(W, "WeaponAniNumber",   FieldKind.Int,   "子彈外觀", "序列圖張數", "0", 0, 999),
            F(W, "AnimFPS",           FieldKind.Float, "子彈外觀", "序列圖幀率", "0", 0f, 120f),
            F(W, "BulletScale",       FieldKind.Float, "子彈外觀", "子彈縮放", "1", 0.05f, 20f),

            // ── 光束外觀 ──
            F(W, "BeamStyle",    FieldKind.Int,   "光束外觀", "雷射種類", "2", 1, 10, "1鏡光2標準3脈衝4離子5電漿6虛線7閃電8針狀9洪流10微光"),
            F(W, "BeamColor",    FieldKind.Int,   "光束外觀", "雷射顏色", "9", 1, 10, "1紅2橙3黃4綠5青6藍7紫8洋紅9白10琥珀金"),
            F(W, "BeamWidth",    FieldKind.Float, "光束外觀", "雷射粗細", "0.5", 0.05f, 10f, "視覺與命中共用"),
            F(W, "PixelBeamSet", FieldKind.Text,  "光束外觀", "像素雷射素材組", "", help: "留空＝shader 雷射；A_Blue"),

            // ── 特效 ID ──
            F(W, "FireEffectID",   FieldKind.Int, "特效", "發射特效 ID", "0", 0, 99999, "VfxTable；發射時在玩家身上播", universal: true),
            F(W, "HitEffectID",    FieldKind.Int, "特效", "擊中特效 ID", "0", 0, 99999, "VfxTable；命中點播"),
            F(W, "TrailEffectID",  FieldKind.Int, "特效", "軌跡特效 ID", "0", 0, 99999, "VfxTable；配合配方 TrailStep 沿路種"),
            F(W, "SummonEffectID", FieldKind.Int, "特效", "召喚特效 ID", "0", 0, 99999, "VfxTable；每個生怪點播一次"),
        };
        return L;
    }

    static Dictionary<string, FieldSpec> IndexFields(List<FieldSpec> fields)
    {
        // 兩張表的欄名不重疊（ID/Name 是通用欄，兩邊語意一致，取先出現的 Recipe 那份即可）
        var d = new Dictionary<string, FieldSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fields) if (!d.ContainsKey(f.Name)) d[f.Name] = f;
        return d;
    }

    static ModeSpec M(Dictionary<WeaponMode, ModeSpec> d, WeaponMode mode, string label, string help, bool continuous = false, bool bullets = false)
    {
        var m = new ModeSpec { Mode = mode, Label = label, Help = help, Continuous = continuous, SpawnsBullets = bullets };
        d[mode] = m; return m;
    }

    static Dictionary<WeaponMode, ModeSpec> BuildModes()
    {
        var d = new Dictionary<WeaponMode, ModeSpec>();

        // 一般子彈：全套彈道行為
        M(d, WeaponMode.Normal, "一般子彈", "會飛的子彈；穿透／反彈／追蹤／分裂／軌跡／命中迸發全吃", bullets: true)
            .Eff("FireInterval", "Speed", "Radius", "LifeTime", "RotationSpeed", "PierceCount", "BlockedByEnvironment")
            .Eff(Multi).Eff("SplitTiming", "SubRecipeID", "BounceTarget", "MaxBounces", "HomingTurnSpeed")
            .Eff("GroundEffectID", "GroundEffectHitTarget", "TrailStep", "SubWeaponOnHit", "SubWeaponHitTarget")
            .Eff(Charge)
            .Eff("Damage", "HitEffectID", "TrailEffectID").Eff(BulletVisual);

        // 環繞：一般子彈 + 軌道
        M(d, WeaponMode.Orbital, "環繞", "一組子彈繞著玩家轉；碰到怪可穿透／反彈脫軌／分裂", bullets: true)
            .Eff("FireInterval", "Speed", "Radius", "LifeTime", "RotationSpeed", "PierceCount", "BlockedByEnvironment")
            .Eff(Multi).Eff("SplitTiming", "SubRecipeID", "BounceTarget", "MaxBounces", "HomingTurnSpeed")
            .Eff("GroundEffectID", "GroundEffectHitTarget", "TrailStep", "SubWeaponOnHit", "SubWeaponHitTarget")
            .Eff("OrbitalRadius", "OrbitalCount").Eff(Charge)
            .Eff("Damage", "HitEffectID", "TrailEffectID").Eff(BulletVisual)
            .Lbl("Speed", "環繞速度（切線）").Lbl("LifeTime", "整組存續秒數（-1＝直到下次發射）");

        // 拋物線：丟炸彈
        M(d, WeaponMode.Parabolic, "拋物線", "丟炸彈：飛行中不撞東西，落地才爆炸／放地面特效", bullets: true)
            .Eff("FireInterval", "RotationSpeed").Eff(Multi)
            .Eff("FlightTime", "ArcHeight", "LaunchSource", "LandingScatterRadius", "AreaRadius")
            .Eff("GroundEffectID", "GroundEffectHitTarget", "TrailStep").Eff(Charge)
            .Eff("Damage", "HitEffectID", "TrailEffectID").Eff(BulletVisual)
            .Lbl("SpreadCount", "一發幾顆").Lbl("AreaRadius", "落地爆炸半徑").Lbl("GroundEffectHitTarget", "地面特效觸發對象（填 Ground）");

        // 雷射：持續光束
        M(d, WeaponMode.Laser, "雷射", "按住維持光束；穿透／反彈／追蹤／多道／命中分裂都吃", continuous: true)
            .Eff("PierceCount", "BlockedByEnvironment").Eff(Multi)
            .Eff("SplitTiming", "SubRecipeID", "BounceTarget", "MaxBounces", "HomingTurnSpeed")
            .Eff("Range", "DotInterval", "GroundEffectID", "GroundEffectHitTarget", "TrailStep")
            .Eff("Damage", "HitEffectID", "TrailEffectID", "PixelBeamSet").Eff(BeamVisual)
            .Lbl("SpreadCount", "光束道數").Lbl("Range", "光束長度（-1＝無限）").Lbl("HomingTurnSpeed", "光束彎曲追蹤速度")
            .Lbl("SplitTiming", "分裂時機（只認 OnHit）").Lbl("TrailStep", "火焰柱間距（配 TrailEffectID）");

        // 佛光：跟著玩家的 AOE
        M(d, WeaponMode.Aura, "佛光", "按住時在身上維持一圈跟著走的 AOE；圓的定義走 GroundEffectID（該列 Duration 必須 -1）", continuous: true)
            .Req("GroundEffectID").Eff("Damage")
            .Lbl("GroundEffectID", "佛光圓（GroundEffectTable ID，Duration=-1）");

        // 連鎖閃電
        M(d, WeaponMode.Chain, "連鎖閃電", "打中第一隻後在半徑內逐跳；撞牆就停")
            .Eff("FireInterval").Eff(Multi).Eff("Range", "ChainCount", "ChainRadius", "AimConeAngle").Eff(Charge)
            .Eff("Damage", "HitEffectID").Eff(BeamVisual)
            .Lbl("SpreadCount", "閃電道數").Lbl("Range", "首段射程");

        // 落雷
        M(d, WeaponMode.SkyStrike, "落雷", "從畫面上緣劈到滑鼠點，落地圓形 AOE；SubRecipeID 可接一條連鎖配方")
            .Eff("FireInterval").Eff(Multi).Eff("SubRecipeID", "SnapRadius", "AreaRadius", "GroundEffectID", "SegmentedColumn").Eff(Charge)
            .Eff("Damage", "HitEffectID").Eff(BeamVisual)
            .Lbl("SpreadCount", "落點數").Lbl("AreaRadius", "落雷範圍半徑").Lbl("GroundEffectID", "落點留痕（地面特效 ID）")
            .Lbl("SubRecipeID", "落點接連鎖（填 Chain 配方 ID）");

        // 召喚
        M(d, WeaponMode.Summon, "召喚", "施放時在身邊生怪；冷卻用 FireInterval")
            .Eff("FireInterval").Req("SummonIds").Eff("SummonCount", "SummonMaxAlive", "SummonRadius")
            .Eff("SummonEffectID")
            .Lbl("FireInterval", "召喚冷卻（秒）");

        // 定點法陣
        M(d, WeaponMode.GroundCast, "定點法陣", "在滑鼠位置放一個地面特效（半徑／節拍走 GroundEffectTable，傷害用武器 Damage）")
            .Eff("FireInterval").Req("GroundEffectID").Eff("Range").Eff(Charge)
            .Eff("Damage", "HitEffectID")
            .Lbl("GroundEffectID", "法陣（GroundEffectTable ID）").Lbl("Range", "施放距離");

        // 近戰
        M(d, WeaponMode.Melee, "近身扇形", "以自己為圓心、朝滑鼠方向掃一個扇形")
            .Eff("FireInterval", "AreaRadius", "MeleeAngle").Eff(Charge)
            .Eff("Damage", "HitEffectID")
            .Lbl("AreaRadius", "攻擊半徑");

        // 突進
        M(d, WeaponMode.Dash, "突進斬", "往瞄準方向衝一段，掃過的目標各受傷一次；撞牆提前停")
            .Eff("FireInterval", "DashDistance", "DashWidth").Eff(Charge)
            .Eff("Damage", "HitEffectID");

        return d;
    }

    // ─────────────────────────── 檢查 ───────────────────────────

    /// <summary>
    /// 對一列配方的「欄名 → 原始值」做規格檢查。回傳問題清單（空＝乾淨）；每條前面帶 [Warning]／[Error]。
    /// 由 RecipeManager 在載入時呼叫、也給模擬面板即時顯示。
    /// </summary>
    public static List<string> Validate(WeaponMode mode, IReadOnlyDictionary<string, string> fields, FieldTable table = FieldTable.Recipe)
    {
        var problems = new List<string>();
        var m = Get(mode);

        // 必填缺
        foreach (var req in m.Required)
        {
            var spec = GetField(req);
            if (spec != null && spec.Table != table) continue;
            if (!fields.TryGetValue(req, out var v) || string.IsNullOrWhiteSpace(v) || IsZeroish(v))
                problems.Add($"[Error] {m.Label} 模式必填「{req}」（{Label(mode, req)}）沒有填。");
        }

        // 無效欄卻有值
        foreach (var kv in fields)
        {
            var spec = GetField(kv.Key);
            if (spec == null || spec.Table != table) continue;
            if (spec.Universal || m.Effective.Contains(kv.Key)) continue;
            if (string.IsNullOrWhiteSpace(kv.Value) || IsDefaultLike(spec, kv.Value)) continue;
            problems.Add($"[Warning] 「{kv.Key}」={kv.Value.Trim()} 對 {m.Label} 模式無效，會被忽略。");
        }

        return problems;
    }

    static bool IsZeroish(string v)
    {
        v = v.Trim();
        return v == "0" || v == "0.0" || v.Equals("none", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>值等於這個欄的預設／「沒有」語意（0、None、空、預設字串）就不當成「有填」。</summary>
    static bool IsDefaultLike(FieldSpec spec, string v)
    {
        v = v.Trim();
        if (v.Length == 0) return true;
        if (!string.IsNullOrEmpty(spec.Default) && string.Equals(v, spec.Default, StringComparison.OrdinalIgnoreCase)) return true;
        switch (spec.Kind)
        {
            case FieldKind.Int:
            case FieldKind.Float:
            case FieldKind.Percent:
                return IsZeroish(v) || v == "0%";
            case FieldKind.Bool:
                return !Dipan.Data.CsvFieldParse.Bool(v);
            case FieldKind.Enum:
                return IsZeroish(v);
            default:
                return false;
        }
    }

    // ─────────────────────────── 寫出 CSV 用 ───────────────────────────

    /// <summary>表頭格：<c>Name(顯示名/說明)</c>。括號內是給人看的，解析時只取括號前；半形逗號換成全形免得撞到分隔符。</summary>
    public static string HeaderCell(FieldSpec f)
    {
        string desc = f.Label ?? "";
        if (!string.IsNullOrEmpty(f.Help)) desc += "/" + f.Help;
        if (!string.IsNullOrEmpty(f.Default)) desc += "/空=" + f.Default;
        desc = desc.Replace(",", "，").Replace("(", "（").Replace(")", "）");
        return desc.Length > 0 ? f.Name + "(" + desc + ")" : f.Name;
    }

    /// <summary>整張表的表頭格（依欄順序）。</summary>
    public static List<string> HeaderCells(FieldTable table)
    {
        var list = new List<string>();
        foreach (var f in _fields) if (f.Table == table) list.Add(HeaderCell(f));
        return list;
    }

    /// <summary>分組註解列的文字（每組一行：「── 群組：欄1 / 欄2」），寫在表頭之後。</summary>
    public static List<string> GroupCommentLines(FieldTable table)
    {
        var lines = new List<string>();
        string cur = null; var names = new List<string>();
        foreach (var f in _fields)
        {
            if (f.Table != table) continue;
            if (f.Group != cur)
            {
                if (cur != null) lines.Add("── " + cur + "：" + string.Join(" / ", names));
                cur = f.Group; names.Clear();
            }
            names.Add(f.Name);
        }
        if (cur != null) lines.Add("── " + cur + "：" + string.Join(" / ", names));
        return lines;
    }
}
