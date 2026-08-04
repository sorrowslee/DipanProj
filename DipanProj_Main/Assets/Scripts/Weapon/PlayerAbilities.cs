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

    readonly Dictionary<string, Mod> _recipe = new Dictionary<string, Mod>();
    readonly Dictionary<string, Mod> _weapon = new Dictionary<string, Mod>();
    readonly Dictionary<string, Mod> _player = new Dictionary<string, Mod>();

    /// <summary>最後一次重算時的裝備版本號，用來判斷要不要重算。</summary>
    public int BuiltVersion { get; private set; } = -1;

    /// <summary>角色屬性區（最大生命、減傷…）。目前只累加、還沒有人套用效果，先預留給屬性系統。</summary>
    public IReadOnlyDictionary<string, Mod> PlayerMods => _player;

    /// <summary>查某個角色屬性的修正（沒有就回全 0）。</summary>
    public Mod PlayerMod(string field) => _player.TryGetValue(field, out var m) ? m : default;

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

        // ── 武器層欄位 ──
        w.Damage = Get(_weapon, "Damage").Apply(w.Damage);
        w.BulletScale = Get(_weapon, "BulletScale").Apply(w.BulletScale);
        w.BeamWidth = Get(_weapon, "BeamWidth").Apply(w.BeamWidth);
        w.ManaCost = Mathf.Max(0f, Get(_weapon, "ManaCost").Apply(w.ManaCost));

        // ── 配方層欄位（一定要在拷貝上動）──
        if (w.Recipe != null && _recipe.Count > 0)
        {
            var r = w.Recipe.Clone();
            ApplyToRecipe(r);
            w.Recipe = r;
        }
        return w;
    }

    void ApplyToRecipe(RecipeEntry r)
    {
        var d = r.Data;
        if (d == null) return;

        // ── 反彈 ──
        // ⚠ 光把次數加上去沒有用：程式判斷「這發會不會反彈」還要看 HasBounce 與 BounceTarget。
        //    原本不反彈的武器鑲上反彈珠，必須把這兩個一起打開，否則等於白鑲。
        var bounce = Get(_recipe, "MaxBounces");
        if (!bounce.IsZero)
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
        if (!pierce.IsZero && d.PierceCount >= 0)
            d.PierceCount = Mathf.Max(0, Mathf.RoundToInt(pierce.Apply(d.PierceCount)));

        // ── 追蹤 ──
        var homing = Get(_recipe, "HomingTurnSpeed");
        if (!homing.IsZero)
        {
            d.HomingTurnSpeed = Mathf.Max(0f, homing.Apply(d.HomingTurnSpeed));
            if (d.HomingTurnSpeed > 0f) d.HasHoming = true;
        }

        // ── 分裂 / 散射 ──
        var split = Get(_recipe, "SpreadCount");
        if (!split.IsZero)
        {
            d.SplitCount = Mathf.Clamp(Mathf.RoundToInt(split.Apply(d.SplitCount)), 1, SafeMaxSpreadCount);
            if (d.SplitCount > 1) d.HasSplit = true;
        }
        d.SpreadAngle = Get(_recipe, "SpreadAngle").Apply(d.SpreadAngle);

        // ── 一般數值 ──
        // ⚠ 拋物線武器的 Speed 語意是「飛行秒數」不是速度，所以夾一個下限避免除零。
        d.Speed = Mathf.Max(SafeMinSpeed, Get(_recipe, "Speed").Apply(d.Speed));
        d.Radius = Mathf.Max(0.01f, Get(_recipe, "Radius").Apply(d.Radius));
        if (d.LifeTime >= 0f) d.LifeTime = Mathf.Max(0.05f, Get(_recipe, "LifeTime").Apply(d.LifeTime));
        // ⚠ 發射間隔減到 0 或負數 = 每一幀都發射 → 瞬間卡死，一定要夾下限。
        d.FireInterval = Mathf.Max(SafeMinFireInterval, Get(_recipe, "FireInterval").Apply(d.FireInterval));
        d.RotationSpeed = Get(_recipe, "RotationSpeed").Apply(d.RotationSpeed);
        d.TrailStep = Mathf.Max(0f, Get(_recipe, "TrailStep").Apply(d.TrailStep));
        d.BeamRange = Get(_recipe, "BeamRange").Apply(d.BeamRange);
        // ⚠ 傷害節拍同理：0 會變成每幀結算。
        d.DotInterval = Mathf.Max(SafeMinDotInterval, Get(_recipe, "DotInterval").Apply(d.DotInterval));
        d.ArcHeight = Get(_recipe, "ArcHeight").Apply(d.ArcHeight);
        d.OrbitalCount = Mathf.Clamp(Mathf.RoundToInt(Get(_recipe, "OrbitalCount").Apply(d.OrbitalCount)), 1, SafeMaxSpreadCount);
        d.OrbitalRadius = Mathf.Max(0.1f, Get(_recipe, "OrbitalRadius").Apply(d.OrbitalRadius));

        // ── 主遊戲側欄位（掛在 RecipeEntry 上，不在 ProjectileData）──
        r.BlastRadius = Mathf.Max(0f, Get(_recipe, "BlastRadius").Apply(r.BlastRadius));
        r.ChainCount = Mathf.Max(0, Mathf.RoundToInt(Get(_recipe, "MaxBounces").Apply(r.ChainCount)));   // 連鎖跳數與反彈共用 MaxBounces 欄
        r.ChainRadius = Mathf.Max(0.1f, Get(_recipe, "ChainRadius").Apply(r.ChainRadius));
        r.MeleeAngle = Mathf.Clamp(Get(_recipe, "MeleeAngle").Apply(r.MeleeAngle), 1f, 360f);
        r.DashDistance = Mathf.Max(0.1f, Get(_recipe, "DashDistance").Apply(r.DashDistance));
        r.DashWidth = Mathf.Max(0.1f, Get(_recipe, "DashWidth").Apply(r.DashWidth));
        r.SummonCount = Mathf.Max(1, Mathf.RoundToInt(Get(_recipe, "SummonCount").Apply(r.SummonCount)));
        r.SummonMaxAlive = Mathf.Max(1, Mathf.RoundToInt(Get(_recipe, "SummonMaxAlive").Apply(r.SummonMaxAlive)));
        r.SummonRadius = Mathf.Max(0.1f, Get(_recipe, "SummonRadius").Apply(r.SummonRadius));
    }

    // ══════════════════ 安全夾值（不是遊戲平衡，是「會把遊戲弄壞」的下限）══════════════════
    // 能力刻意沒有上限（玩家要堆反彈堆到 50 次是設計允許的），但下面這幾個一旦被減到 0
    // 會直接讓遊戲當掉或除零，所以無論如何都要擋住。平衡用的上限之後另外討論、記在 readme。

    /// <summary>發射間隔最低值（秒）。減到 0 = 每幀發射 → 瞬間數千顆子彈。</summary>
    const float SafeMinFireInterval = 0.02f;
    /// <summary>飛行速度最低值。拋物線武器的 Speed 是「飛行秒數」，0 會除零。</summary>
    const float SafeMinSpeed = 0.05f;
    /// <summary>持續傷害節拍最低值（秒）。</summary>
    const float SafeMinDotInterval = 0.02f;
    /// <summary>單次發射的子彈數上限（分裂/散射/環繞）。純粹是效能保護。</summary>
    const int SafeMaxSpreadCount = 64;

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
