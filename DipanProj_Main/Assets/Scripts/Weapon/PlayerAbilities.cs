using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;

/// <summary>
/// **玩家的能力容器**——這個遊戲的戰力核心。
///
/// 【設計意圖】攻擊時不再直接讀「裝備中那把武器的表格資料」，而是讀這個容器。
/// 換裝備或改鑲嵌時重算一次，把所有來源的能力累加起來：
///
///     裝備中的武器  → 提供「基底」（發射模式：一般子彈 / 雷射 / 近戰 / 拋物線…）
///     武器內建能力  ┐
///     其他裝備能力  ├→ 各自查表得到「數值」→ 全部相加 → 套到基底的拷貝上
///     所有鑲嵌的珠子┘
///
/// 【疊加規則】**數值相加，不是等級相加**。等級只在單一來源內部有意義：
///   武器內建反彈Lv3(5次) + 6顆Lv3珠(各5次) + 護身符Lv3(5次) + 戒指Lv3(5次) = 45 次。
///   所以 GemTable 永遠只需要 Lv1/Lv2/Lv3 三欄，不會有「Lv18 查不到表」的問題。
///   能力**沒有上限**——玩家可以把所有孔都塞反彈，換取極端 build（代價是不會穿透/追蹤/分裂）。
///
/// 【依模式過濾（2026-08-26）】珠子改的欄位對「目前武器的發射模式」無效就**不套用**——
///   佛光不會反彈、雷射沒有子彈大小、召喚不吃傷害。哪些有效由 <see cref="WeaponModeSpec"/> 決定（單一真相），
///   鍛造介面的「這顆珠對這把武器沒效果」提示也查同一張表（<see cref="IsGemEffective(WeaponMode, GemData)"/>）。
///   唯一的例外是「迅捷珠 × 拋物線」：Speed 對拋物線本身無意義，但珠子的 +% 會換算成**縮短飛行秒數**（丟得更快）。
///
/// 【為什麼一定要拷貝】配方在整個遊戲裡是共用的單一物件（怪物也讀同一份），
/// 就地改欄位會污染其他武器與怪物、而且永久累積。見 <see cref="RecipeEntry.Clone"/>。
///
/// 見 readme/GEM_SOCKET.md。
/// </summary>
public class PlayerAbilities
{
    /// <summary>一個欄位累加出來的修正量：固定值相加、百分比另外相加，最後 (基礎 + flat) * (1 + percent)。</summary>
    public struct Mod
    {
        public float flat;
        public float percent;
        public bool IsZero => Mathf.Approximately(flat, 0f) && Mathf.Approximately(percent, 0f);
        public float Apply(float baseValue) => (baseValue + flat) * (1f + percent);
    }

    readonly Dictionary<string, Mod> _recipe = new Dictionary<string, Mod>(System.StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Mod> _weapon = new Dictionary<string, Mod>(System.StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Mod> _player = new Dictionary<string, Mod>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>最後一次重算時的裝備版本號，用來判斷要不要重算。</summary>
    public int BuiltVersion { get; private set; } = -1;

    /// <summary>角色屬性區（最大生命、減傷…）。目前只累加、還沒有人套用效果，先預留給屬性系統。</summary>
    public IReadOnlyDictionary<string, Mod> PlayerMods => _player;

    /// <summary>查某個角色屬性的修正（沒有就回全 0）。</summary>
    public Mod PlayerMod(string field) => _player.TryGetValue(field, out var m) ? m : default;

    // ───────────────────────── 珠子有效性（給 UI 與套用共用）─────────────────────────

    /// <summary>
    /// 這顆珠子對這個發射模式有沒有效果。屬性珠（Target=Player）一律有效（跟武器無關）。
    /// 「迅捷 × 拋物線」算有效（換算成縮短飛行秒數）。
    /// </summary>
    public static bool IsGemEffective(WeaponMode mode, GemData gem)
    {
        if (gem == null || string.IsNullOrEmpty(gem.Field)) return false;
        if (gem.Target == GemTarget.Player) return true;
        if (mode == WeaponMode.Parabolic && gem.Field.Equals("Speed", System.StringComparison.OrdinalIgnoreCase)) return true;
        return WeaponModeSpec.IsEffective(mode, gem.Field);
    }

    /// <summary>同上，直接吃武器；沒有武器（空手）時回 true（沒東西可以無效）。</summary>
    public static bool IsGemEffective(WeaponData weapon, GemData gem)
    {
        if (weapon == null || weapon.Recipe == null) return true;
        return IsGemEffective(weapon.Recipe.Mode, gem);
    }

    // ───────────────────────── 重算 ─────────────────────────

    /// <summary>
    /// 掃過所有裝備欄，把「裝備內建能力 + 鑲在上面的珠子」累加成一份修正表。
    /// 由 PlayerController 在背包/鑲嵌變動時呼叫。
    /// </summary>
    public void Rebuild(InventorySystem inv)
    {
        _recipe.Clear(); _weapon.Clear(); _player.Clear();
        if (inv == null) { BuiltVersion = -1; return; }
        BuiltVersion = inv.LoadoutVersion;

        foreach (var kv in inv.EquippedItems())
        {
            var st = kv.Value;

            // ① 裝備本身內建的能力
            //    武器表/裝備表要長「內建能力等級」欄位是之後的事（目前武器的基礎值直接寫在配方表裡，
            //    見 readme/GEM_SOCKET.md）。這裡先留一個明確的掛點，屆時解析出來丟進 Add 即可。
            //    AddBuiltIn(inv.GetData(st.ItemId));

            // ② 鑲在這件上面的珠子
            var inst = st.Inst;
            if (inst == null || !inst.HasSockets) continue;
            for (int i = 0; i < inst.sockets.Count; i++)
            {
                var gem = inst.GemAt(i);
                if (gem == null) continue;
                var gd = ItemManager.DataOf(gem);
                if (gd == null) continue;
                Add(gd.Target, gd.Field, gd.ValueAt(gem.level), gd.IsPercent);
            }
        }
    }

    /// <summary>累加一筆修正。</summary>
    public void Add(GemTarget target, string field, float value, bool isPercent)
    {
        if (string.IsNullOrEmpty(field) || Mathf.Approximately(value, 0f)) return;
        var table = TableOf(target);
        table.TryGetValue(field, out var m);
        if (isPercent) m.percent += value; else m.flat += value;
        table[field] = m;
    }

    Dictionary<string, Mod> TableOf(GemTarget t)
    {
        switch (t)
        {
            case GemTarget.Weapon: return _weapon;
            case GemTarget.Player: return _player;
            default: return _recipe;
        }
    }

    bool HasAny => _recipe.Count > 0 || _weapon.Count > 0;

    // ───────────────────────── 套用 ─────────────────────────

    /// <summary>
    /// 把累加出來的能力套到一把武器上，回傳**玩家專屬的深拷貝**（原始表格資料一個位元組都不動）。
    /// 沒有任何修正時直接回傳原件，省下不必要的配置。
    /// </summary>
    public WeaponData Resolve(WeaponData baseWeapon)
    {
        if (baseWeapon == null || !HasAny) return baseWeapon;

        var w = ShallowCopy(baseWeapon);
        WeaponMode mode = (w.Recipe != null) ? w.Recipe.Mode : WeaponMode.Normal;

        // ── 武器層欄位（只套對此模式有效的）──
        if (Eff(mode, "Damage")) w.Damage = Get(_weapon, "Damage").Apply(w.Damage);
        if (Eff(mode, "BulletScale")) w.BulletScale = Get(_weapon, "BulletScale").Apply(w.BulletScale);
        if (Eff(mode, "BeamWidth")) w.BeamWidth = Get(_weapon, "BeamWidth").Apply(w.BeamWidth);
        w.ManaCost = Mathf.Max(0f, Get(_weapon, "ManaCost").Apply(w.ManaCost));   // 通用欄

        // ── 配方層欄位（一定要在拷貝上動）──
        if (w.Recipe != null && _recipe.Count > 0)
        {
            var r = w.Recipe.Clone();
            ApplyToRecipe(r);
            w.Recipe = r;
        }
        return w;
    }

    static bool Eff(WeaponMode mode, string field) => WeaponModeSpec.IsEffective(mode, field);

    void ApplyToRecipe(RecipeEntry r)
    {
        var d = r.Data;
        if (d == null) return;
        WeaponMode mode = r.Mode;

        // ── 反彈 ──
        // ⚠ 光把次數加上去沒有用：程式判斷「這發會不會反彈」還要看 HasBounce 與 BounceTarget。
        //    原本不反彈的武器鑲上反彈珠，必須把這兩個一起打開，否則等於白鑲。
        //    連鎖閃電的跳數已改成獨立欄 ChainCount（2026-08-26），反彈珠對連鎖不再有作用。
        var bounce = Get(_recipe, "MaxBounces");
        if (!bounce.IsZero && Eff(mode, "MaxBounces"))
        {
            d.MaxBounces = Mathf.Max(0, Mathf.RoundToInt(bounce.Apply(d.MaxBounces)));
            if (d.MaxBounces > 0)
            {
                d.HasBounce = true;
                if (r.BounceTarget == BounceTarget.None) r.BounceTarget = BounceTarget.Environment;   // 預設對牆反彈
            }
        }

        // ── 穿透 ──
        // ⚠ -1 代表「無限穿透」，不能直接 +1（會變成 0 = 不穿透）。已經無限就維持無限。
        var pierce = Get(_recipe, "PierceCount");
        if (!pierce.IsZero && d.PierceCount >= 0 && Eff(mode, "PierceCount"))
            d.PierceCount = Mathf.Max(0, Mathf.RoundToInt(pierce.Apply(d.PierceCount)));

        // ── 追蹤 ──
        var homing = Get(_recipe, "HomingTurnSpeed");
        if (!homing.IsZero && Eff(mode, "HomingTurnSpeed"))
        {
            d.HomingTurnSpeed = Mathf.Max(0f, homing.Apply(d.HomingTurnSpeed));
            if (d.HomingTurnSpeed > 0f) d.HasHoming = true;
        }

        // ── 分裂 / 散射 ──
        var split = Get(_recipe, "SpreadCount");
        if (!split.IsZero && Eff(mode, "SpreadCount"))
        {
            d.SplitCount = Mathf.Clamp(Mathf.RoundToInt(split.Apply(d.SplitCount)), 1, SafeMaxSpreadCount);
            // 分裂行為（SplitBehavior）只有會飛的子彈用；雷射/拋物線/連鎖/落雷直接讀 SplitCount 當道數/顆數
            if (d.SplitCount > 1 && (mode == WeaponMode.Normal || mode == WeaponMode.Orbital)) d.HasSplit = true;
        }
        if (Eff(mode, "SpreadAngle")) d.SpreadAngle = Get(_recipe, "SpreadAngle").Apply(d.SpreadAngle);

        // ── 速度 ──
        // ⚠ 拋物線：Speed 對它無意義，但「迅捷珠」的 +% 換算成縮短飛行秒數（丟得更快）；固定值部分忽略。
        var speed = Get(_recipe, "Speed");
        if (!speed.IsZero)
        {
            if (mode == WeaponMode.Parabolic)
                d.FlightTime = Mathf.Max(SafeMinFlightTime, d.FlightTime / Mathf.Max(0.01f, 1f + speed.percent));
            else if (Eff(mode, "Speed"))
                d.Speed = Mathf.Max(SafeMinSpeed, speed.Apply(d.Speed));
        }
        if (Eff(mode, "FlightTime")) d.FlightTime = Mathf.Max(SafeMinFlightTime, Get(_recipe, "FlightTime").Apply(d.FlightTime));

        // ── 一般數值 ──
        if (Eff(mode, "Radius")) d.Radius = Mathf.Max(0.01f, Get(_recipe, "Radius").Apply(d.Radius));
        if (Eff(mode, "LifeTime") && d.LifeTime >= 0f) d.LifeTime = Mathf.Max(0.05f, Get(_recipe, "LifeTime").Apply(d.LifeTime));
        // ⚠ 發射間隔減到 0 或負數 = 每一幀都發射 → 瞬間卡死，一定要夾下限。
        if (Eff(mode, "FireInterval")) d.FireInterval = Mathf.Max(SafeMinFireInterval, Get(_recipe, "FireInterval").Apply(d.FireInterval));
        if (Eff(mode, "RotationSpeed")) d.RotationSpeed = Get(_recipe, "RotationSpeed").Apply(d.RotationSpeed);
        if (Eff(mode, "TrailStep")) d.TrailStep = Mathf.Max(0f, Get(_recipe, "TrailStep").Apply(d.TrailStep));
        if (Eff(mode, "Range") && d.BeamRange >= 0f) d.BeamRange = Mathf.Max(0.1f, Get(_recipe, "Range").Apply(d.BeamRange));   // CSV 欄名 Range → ProjectileData.BeamRange；-1（雷射無限）不動
        // ⚠ 傷害節拍同理：0 會變成每幀結算。
        if (Eff(mode, "DotInterval")) d.DotInterval = Mathf.Max(SafeMinDotInterval, Get(_recipe, "DotInterval").Apply(d.DotInterval));
        if (Eff(mode, "ArcHeight")) d.ArcHeight = Get(_recipe, "ArcHeight").Apply(d.ArcHeight);
        if (Eff(mode, "OrbitalCount")) d.OrbitalCount = Mathf.Clamp(Mathf.RoundToInt(Get(_recipe, "OrbitalCount").Apply(d.OrbitalCount)), 1, SafeMaxSpreadCount);
        if (Eff(mode, "OrbitalRadius")) d.OrbitalRadius = Mathf.Max(0.1f, Get(_recipe, "OrbitalRadius").Apply(d.OrbitalRadius));

        // ── 主遊戲側欄位（掛在 RecipeEntry 上，不在 ProjectileData）──
        if (Eff(mode, "AreaRadius")) r.AreaRadius = Mathf.Max(0f, Get(_recipe, "AreaRadius").Apply(r.AreaRadius));
        if (Eff(mode, "ChainCount")) r.ChainCount = Mathf.Max(0, Mathf.RoundToInt(Get(_recipe, "ChainCount").Apply(r.ChainCount)));
        if (Eff(mode, "ChainRadius")) r.ChainRadius = Mathf.Max(0.1f, Get(_recipe, "ChainRadius").Apply(r.ChainRadius));
        if (Eff(mode, "AimConeAngle")) r.AimConeAngle = Mathf.Clamp(Get(_recipe, "AimConeAngle").Apply(r.AimConeAngle), 0f, 180f);
        if (Eff(mode, "SnapRadius")) r.SnapRadius = Mathf.Max(0f, Get(_recipe, "SnapRadius").Apply(r.SnapRadius));
        if (Eff(mode, "MeleeAngle")) r.MeleeAngle = Mathf.Clamp(Get(_recipe, "MeleeAngle").Apply(r.MeleeAngle), 1f, 360f);
        if (Eff(mode, "DashDistance")) r.DashDistance = Mathf.Max(0.1f, Get(_recipe, "DashDistance").Apply(r.DashDistance));
        if (Eff(mode, "DashWidth")) r.DashWidth = Mathf.Max(0.1f, Get(_recipe, "DashWidth").Apply(r.DashWidth));
        if (Eff(mode, "SummonCount")) r.SummonCount = Mathf.Max(1, Mathf.RoundToInt(Get(_recipe, "SummonCount").Apply(r.SummonCount)));
        if (Eff(mode, "SummonMaxAlive")) r.SummonMaxAlive = Mathf.Max(1, Mathf.RoundToInt(Get(_recipe, "SummonMaxAlive").Apply(r.SummonMaxAlive)));
        if (Eff(mode, "SummonRadius")) r.SummonRadius = Mathf.Max(0.1f, Get(_recipe, "SummonRadius").Apply(r.SummonRadius));
        // 集氣時間縮減：表裡存的是百分點（30＝縮短 30%），珠子填固定值就是加百分點；上限 99 免得除零
        if (Eff(mode, "ChargeTimeReduction")) r.ChargeTimeReduction = Mathf.Clamp(Get(_recipe, "ChargeTimeReduction").Apply(r.ChargeTimeReduction), -1000f, 99f);
        // 連擊：珠子加發數；上限 16（一次扣扳機最多 16 發，效能保護）
        if (Eff(mode, "BurstCount")) r.BurstCount = Mathf.Clamp(Mathf.RoundToInt(Get(_recipe, "BurstCount").Apply(r.BurstCount)), 1, SafeMaxBurstCount);
        if (Eff(mode, "BurstInterval")) r.BurstInterval = Mathf.Max(SafeMinBurstInterval, Get(_recipe, "BurstInterval").Apply(r.BurstInterval));
    }

    // ══════════════════ 安全夾值（不是遊戲平衡，是「會把遊戲弄壞」的下限）══════════════════
    // 能力刻意沒有上限（玩家要堆反彈堆到 50 次是設計允許的），但下面這幾個一旦被減到 0
    // 會直接讓遊戲當掉或除零，所以無論如何都要擋住。平衡用的上限之後另外討論、記在 readme。

    /// <summary>發射間隔最低值（秒）。減到 0 = 每幀發射 → 瞬間數千顆子彈。</summary>
    const float SafeMinFireInterval = 0.02f;
    /// <summary>飛行速度最低值。</summary>
    const float SafeMinSpeed = 0.05f;
    /// <summary>拋物線飛行秒數最低值（0 會除零）。</summary>
    const float SafeMinFlightTime = 0.05f;
    /// <summary>持續傷害節拍最低值（秒）。</summary>
    const float SafeMinDotInterval = 0.02f;
    /// <summary>單次發射的子彈數上限（分裂/散射/環繞）。純粹是效能保護。</summary>
    const int SafeMaxSpreadCount = 64;
    /// <summary>連擊一次扣扳機最多幾發（效能保護）。</summary>
    const int SafeMaxBurstCount = 16;
    /// <summary>連擊每發最短間隔；0 會變成同一幀全射出去。</summary>
    const float SafeMinBurstInterval = 0.02f;

    static Mod Get(Dictionary<string, Mod> table, string field)
        => table.TryGetValue(field, out var m) ? m : default;

    /// <summary>複製一份 WeaponData（欄位全抄，Recipe 先沿用參照，由呼叫端決定要不要換成拷貝）。</summary>
    static WeaponData ShallowCopy(WeaponData s) => new WeaponData
    {
        ID = s.ID, Name = s.Name, Damage = s.Damage, ManaCost = s.ManaCost,
        RecipeID = s.RecipeID, WeaponSpritePath = s.WeaponSpritePath,
        SpriteAngleOffset = s.SpriteAngleOffset, WeaponAniPath = s.WeaponAniPath,
        WeaponAniNumber = s.WeaponAniNumber, AnimFPS = s.AnimFPS,
        BulletScale = s.BulletScale, CastVisualScale = s.CastVisualScale,
        BeamStyle = s.BeamStyle, BeamColor = s.BeamColor, BeamWidth = s.BeamWidth,
        PixelBeamSet = s.PixelBeamSet,
        FireEffectID = s.FireEffectID, HitEffectID = s.HitEffectID,
        TrailEffectID = s.TrailEffectID, SummonEffectID = s.SummonEffectID,
        Recipe = s.Recipe, BulletPrefab = s.BulletPrefab,
        WeaponSprite = s.WeaponSprite, WeaponSprites = s.WeaponSprites,
        BeamMuzzleSprite = s.BeamMuzzleSprite, BeamImpactSprite = s.BeamImpactSprite,
    };

    // ───────────────────────── 給 UI 看的說明 ─────────────────────────

    /// <summary>把目前累加出來的能力整理成人看得懂的字串（tooltip/除錯用）；沒有能力回空字串。</summary>
    public string Describe()
    {
        if (!HasAny && _player.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        AppendTable(sb, _recipe);
        AppendTable(sb, _weapon);
        AppendTable(sb, _player);
        return sb.ToString().TrimEnd('\n');
    }

    static void AppendTable(System.Text.StringBuilder sb, Dictionary<string, Mod> t)
    {
        foreach (var kv in t)
        {
            sb.Append(kv.Key).Append(' ');
            if (!Mathf.Approximately(kv.Value.flat, 0f)) sb.Append(kv.Value.flat >= 0 ? "+" : "").Append(kv.Value.flat.ToString("0.##"));
            if (!Mathf.Approximately(kv.Value.percent, 0f)) sb.Append(' ').Append(kv.Value.percent >= 0 ? "+" : "").Append((kv.Value.percent * 100f).ToString("0.#")).Append('%');
            sb.Append('\n');
        }
    }
}
