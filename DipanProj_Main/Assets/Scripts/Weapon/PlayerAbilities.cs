using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;

/// <summary>
/// **玩家的能力容器**——這個遊戲的戰力核心。
///
/// 【設計意圖】攻擊時不直接讀「裝備中那把武器的表格資料」，而是讀這個容器。
/// 換裝備或改鑲嵌時重算一次，把所有來源的效果累加起來，套到武器的拷貝上。
///
/// 【疊加規則（2026-11-17 裝備體系，見 readme/EQUIPMENT.md §4）】**全身等級相加**：
///   武器起點（配方的 PierceLv 等）＋各部位內建特殊效果＋所有鑲嵌珠，同一種效果的等級加總後查一次 GemTable。
///   三種算法：
///   - 百分比（傷害、攻速、射程…）：基礎值 ×（1＋百分比）；越小越好的（攻擊間隔、耗魔、集氣時間）用除法 ÷（1＋百分比），永遠不會歸零。
///   - 純等級（穿透、反彈、分裂…）：查表（起點等級＋效果等級）得到數值。
///   - 加值（環繞數、召喚數、扇形角度…）：起點數值＋查表（效果等級）。
///   一種屬性只會有一種效果（GemTable 保證），所以每個屬性只套一次。
///
/// 【依模式過濾】效果對「目前武器的發射模式」無效就不套用——佛光不會反彈、雷射沒有子彈大小、召喚不吃傷害。
///   哪些有效由 <see cref="EquipAttributes"/>（屬性 → 欄位）＋<see cref="WeaponModeSpec"/>（模式 × 欄位）決定，
///   鍛造介面的提示也查同一份（<see cref="IsGemEffective(WeaponMode, GemData)"/>）。
///
/// 【為什麼一定要拷貝】配方在整個遊戲裡是共用的單一物件（怪物也讀同一份），
/// 就地改欄位會污染其他武器與怪物、而且永久累積。見 <see cref="RecipeEntry.Clone"/>。
/// </summary>
public class PlayerAbilities
{
    /// <summary>全身效果等級（不含武器起點）：GemID → 等級總和。</summary>
    readonly Dictionary<int, int> _levels = new Dictionary<int, int>();

    /// <summary>最後一次重算時的裝備版本號，用來判斷要不要重算。</summary>
    public int BuiltVersion { get; private set; } = -1;

    /// <summary>全身效果等級（含防具類，防具類目前只累加、還沒有人套用）。</summary>
    public IReadOnlyDictionary<int, int> Levels => _levels;

    /// <summary>某種效果的全身等級總和（不含武器起點）。</summary>
    public int LevelOf(int gemId) => _levels.TryGetValue(gemId, out var n) ? n : 0;

    // ───────────────────────── 效果有效性（給 UI 與套用共用）─────────────────────────

    /// <summary>這個效果對這個發射模式有沒有作用。防具類效果一律有效（跟武器無關）。</summary>
    public static bool IsGemEffective(WeaponMode mode, GemData gem) => EquipAttributes.IsEffective(mode, gem);

    /// <summary>同上，直接吃武器；沒有武器（空手）時回 true（沒東西可以無效）。</summary>
    public static bool IsGemEffective(WeaponData weapon, GemData gem)
    {
        if (weapon == null || weapon.Recipe == null) return true;
        return IsGemEffective(weapon.Recipe.Mode, gem);
    }

    // ───────────────────────── 重算 ─────────────────────────

    /// <summary>
    /// 掃過所有裝備欄，把「裝備內建特殊效果 + 鑲在上面的珠子」的等級加總。
    /// 由 PlayerController 在背包/鑲嵌變動時呼叫。
    /// </summary>
    public void Rebuild(InventorySystem inv)
    {
        _levels.Clear();
        if (inv == null) { BuiltVersion = -1; return; }
        BuiltVersion = inv.LoadoutVersion;

        foreach (var kv in inv.EquippedItems())
            AddItem(kv.Value.Inst);
    }

    /// <summary>
    /// 把**一件**裝備的效果等級加進來：① 內建特殊效果（品質系統，affixes）＋ ② 鑲在上面的珠子。
    /// Rebuild 對每個裝備欄各呼叫一次；tooltip 的單件預覽（<see cref="ForItem"/>）只呼叫一次。
    /// </summary>
    public void AddItem(ItemInstance inst)
    {
        if (inst == null) return;

        // ① 裝備內建的特殊效果：效果 ID 就是 GemTable 的 GemID，直接累加
        if (inst.affixes != null)
            for (int i = 0; i < inst.affixes.Count; i++)
            {
                var a = inst.affixes[i];
                if (a != null) AddLevel(a.effectId, a.level);
            }

        // ② 鑲在這件上面的珠子
        if (!inst.HasSockets) return;
        for (int i = 0; i < inst.sockets.Count; i++)
        {
            var gem = inst.GemAt(i);
            if (gem == null) continue;
            var gd = ItemManager.DataOf(gem);
            if (gd == null) continue;
            AddLevel(gd.GemID, gem.level);
        }
    }

    /// <summary>
    /// 只算「這一件自己」的能力容器（tooltip 顯示這件的數值用：基礎＋這件的內建效果＋這件的珠子，不含其他部位）。
    /// 跟實際戰鬥同一套 <see cref="Resolve"/>，所以顯示出來的數字不會跟實際打出去的對不上。
    /// </summary>
    public static PlayerAbilities ForItem(ItemInstance inst)
    {
        var pa = new PlayerAbilities();
        pa.AddItem(inst);
        return pa;
    }

    /// <summary>累加一筆效果等級。</summary>
    public void AddLevel(int gemId, int level)
    {
        if (gemId <= 0 || level <= 0) return;
        _levels.TryGetValue(gemId, out var n);
        _levels[gemId] = n + level;
    }

    bool HasAny => _levels.Count > 0;

    // ───────────────────────── 套用 ─────────────────────────

    /// <summary>
    /// 把累加出來的效果套到一把武器上，回傳**玩家專屬的深拷貝**（原始表格資料一個位元組都不動）。
    /// 沒有任何效果時直接回傳原件——武器起點等級在讀表時就已換算成數值，不需要再算。
    /// </summary>
    public WeaponData Resolve(WeaponData baseWeapon)
    {
        if (baseWeapon == null || !HasAny) return baseWeapon;

        var w = ShallowCopy(baseWeapon);
        WeaponMode mode = (w.Recipe != null) ? w.Recipe.Mode : WeaponMode.Normal;
        RecipeEntry r = w.Recipe != null ? w.Recipe.Clone() : null;   // 一定要在拷貝上動
        var gems = ItemManager.Gems;
        float extraSpread = 0f;

        foreach (var kv in _levels)
        {
            var g = gems.Get(kv.Key);
            if (g == null || !g.IsAttackClass || g.IsStatusEffect) continue; // 防禦／輔助類、狀態效果：不改武器數值（由 StatusAttacker 處理，見 STATUS_EFFECT.md）
            if (!EquipAttributes.IsEffective(mode, g.Attribute)) continue;   // 對這個模式沒作用
            switch (EquipAttributes.CalcOf(g.Attribute))
            {
                case EffectCalc.Percent: ApplyPercent(w, r, mode, g.Attribute, g.ValueAt(kv.Value)); break;
                case EffectCalc.Add:     ApplyAdd(r, g.Attribute, g.ValueAt(kv.Value), ref extraSpread); break;
                case EffectCalc.Level:   ApplyLevel(r, mode, g.Attribute, kv.Value); break;
            }
        }

        // 分裂扇形最後重算一次：分裂數或廣角可能變了
        if (r != null && r.Data != null && WeaponModeSpec.IsEffective(mode, "SpreadMaxAngle"))
            r.Data.SpreadAngle = RecipeEntry.ComputeSpreadAngle(r.Data.SplitCount, r.SpreadSpacing, r.SpreadMaxAngle, extraSpread);

        if (r != null) w.Recipe = r;
        return w;
    }

    static bool Eff(WeaponMode mode, string field) => WeaponModeSpec.IsEffective(mode, field);

    /// <summary>百分比：基礎 ×（1＋p）；越小越好的屬性 ÷（1＋p）。</summary>
    static void ApplyPercent(WeaponData w, RecipeEntry r, WeaponMode mode, string attr, float p)
    {
        if (p <= -0.99f) p = -0.99f;   // 表填了負值也不會除零或翻號
        float mul = 1f + p;
        var d = r != null ? r.Data : null;
        switch (attr)
        {
            case "Damage":
                if (Eff(mode, "Damage")) w.Damage *= mul;
                break;

            case "AttackRate":   // 攻擊頻率：間隔 ÷（1＋p）
                if (r == null) break;
                if (mode == WeaponMode.Aura) r.AuraIntervalScale /= mul;   // 佛光的節拍在地面特效表，生成時帶過去
                if (d != null && Eff(mode, "FireInterval")) d.FireInterval = Mathf.Max(SafeMinFireInterval, d.FireInterval / mul);
                if (d != null && Eff(mode, "DotInterval")) d.DotInterval = Mathf.Max(SafeMinDotInterval, d.DotInterval / mul);
                break;

            case "Speed":
                if (d == null) break;
                // 拋物線／頭頂投彈：Speed 對它們沒意義，換算成縮短飛行（落下）秒數
                if (mode == WeaponMode.Parabolic || mode == WeaponMode.Overhead)
                    d.FlightTime = Mathf.Max(SafeMinFlightTime, d.FlightTime / mul);
                else if (Eff(mode, "Speed"))
                    d.Speed = Mathf.Max(SafeMinSpeed, d.Speed * mul);
                break;

            case "Range":   // 射程：子彈存活時間／雷射與施放類的 Range；-1（無限）不動
                if (d == null) break;
                if (Eff(mode, "LifeTime") && d.LifeTime >= 0f) d.LifeTime = Mathf.Max(0.05f, d.LifeTime * mul);
                if (Eff(mode, "Range") && d.BeamRange >= 0f) d.BeamRange = Mathf.Max(0.1f, d.BeamRange * mul);   // CSV 欄名 Range → ProjectileData.BeamRange
                break;

            case "Size":    // 大小／範圍：施放大小＋光束粗細（合併原須彌與粗束）
                if (Eff(mode, "BulletScale")) w.BulletScale *= mul;
                if (Eff(mode, "BeamWidth")) w.BeamWidth *= mul;
                break;

            case "ManaCost":
                w.ManaCost = Mathf.Max(0f, w.ManaCost / mul);
                break;

            case "OrbitalRadius":
                if (d != null) d.OrbitalRadius = Mathf.Max(0.1f, d.OrbitalRadius * mul);
                break;

            case "ChainRadius":
                if (r != null) r.ChainRadius = Mathf.Max(0.1f, r.ChainRadius * mul);
                break;

            case "DashDistance":
                if (r != null) r.DashDistance = Mathf.Max(0.1f, r.DashDistance * mul);
                break;

            case "ChargeTime":
                // 表裡存的是「縮減百分點」（30＝縮短 30%）：集氣時間 ∝（100－CTR）。÷（1＋p）換回百分點，下游照舊讀。
                if (r != null) r.ChargeTimeReduction = Mathf.Min(99f, 100f - (100f - r.ChargeTimeReduction) / mul);
                break;
        }
    }

    /// <summary>加值：起點數值＋查表得到的值。</summary>
    static void ApplyAdd(RecipeEntry r, string attr, float v, ref float extraSpread)
    {
        if (r == null) return;
        var d = r.Data;
        int n = Mathf.RoundToInt(v);
        switch (attr)
        {
            case "OrbitalCount":   if (d != null) d.OrbitalCount = Mathf.Clamp(d.OrbitalCount + n, 1, SafeMaxSpreadCount); break;
            case "SummonCount":    r.SummonCount = Mathf.Max(1, r.SummonCount + n); break;
            case "SummonMaxAlive": r.SummonMaxAlive = Mathf.Max(1, r.SummonMaxAlive + n); break;
            case "ChainCount":     r.ChainCount = Mathf.Max(0, r.ChainCount + n); break;
            case "SpreadAngle":    extraSpread += v; break;   // 最後重算扇形時加上
            case "MeleeAngle":     r.MeleeAngle = Mathf.Clamp(r.MeleeAngle + v, 1f, 360f); break;
            case "AimConeAngle":   r.AimConeAngle = Mathf.Clamp(r.AimConeAngle + v, 0f, 180f); break;
            case "SnapRadius":     r.SnapRadius = Mathf.Max(0f, r.SnapRadius + v); break;
        }
    }

    /// <summary>純等級：起點等級＋效果等級，加總後查一次表。</summary>
    static void ApplyLevel(RecipeEntry r, WeaponMode mode, string attr, int lv)
    {
        if (r == null) return;
        var d = r.Data;
        switch (attr)
        {
            case "Pierce":
                // 迴旋天生無限穿透（-1），不受影響
                if (d != null && d.PierceCount >= 0) d.PierceCount = RecipeEntry.LvToInt("Pierce", r.PierceLv + lv);
                break;

            case "Bounce":
                if (d == null) break;
                d.MaxBounces = RecipeEntry.LvToInt("Bounce", r.BounceLv + lv);
                // ⚠ 光有次數沒有用：程式判斷「這發會不會反彈」還要看 HasBounce 與 BounceTarget。
                if (d.MaxBounces > 0)
                {
                    d.HasBounce = true;
                    if (r.BounceTarget == BounceTarget.None) r.BounceTarget = BounceTarget.Environment;   // 預設對牆反彈
                }
                break;

            case "Split":
                if (d == null) break;
                d.SplitCount = Mathf.Clamp(1 + RecipeEntry.LvToInt("Split", r.SplitLv + lv), 1, SafeMaxSpreadCount);
                // 分裂行為（SplitBehavior）只有會飛的子彈用；雷射/拋物線/連鎖/落雷直接讀 SplitCount 當道數/顆數
                if (d.SplitCount > 1 && (mode == WeaponMode.Normal || mode == WeaponMode.Orbital || mode == WeaponMode.Familiar)) d.HasSplit = true;
                break;

            case "Homing":
                if (d == null) break;
                d.HomingTurnSpeed = Mathf.Max(0f, EquipAttributes.LevelValue("Homing", r.HomingLv + lv));
                if (d.HomingTurnSpeed > 0f) d.HasHoming = true;
                break;

            case "Burst":
                r.BurstCount = Mathf.Clamp(1 + RecipeEntry.LvToInt("Burst", r.BurstLv + lv), 1, SafeMaxBurstCount);
                break;

            case "Parallel":
                r.ParallelCount = Mathf.Clamp(1 + RecipeEntry.LvToInt("Parallel", r.ParallelLv + lv), 1, SafeMaxParallelCount);
                break;
        }
    }

    // ══════════════════ 安全夾值（不是遊戲平衡，是「會把遊戲弄壞」的界線）══════════════════
    // 效果等級另有 MaxLv 封頂；越小越好的屬性改用除法後不會歸零，下面這些是最後一道保險與效能保護。
    // 子彈乘積（分裂 × 平行 × 連擊）另有 PlayerController.MaxBulletsPerTrigger 守門。

    /// <summary>發射間隔最低值（秒）。</summary>
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
    /// <summary>平行彈最多幾道。</summary>
    const int SafeMaxParallelCount = 16;

    /// <summary>複製一份 WeaponData（欄位全抄，Recipe 先沿用參照，由呼叫端決定要不要換成拷貝）。</summary>
    static WeaponData ShallowCopy(WeaponData s) => new WeaponData
    {
        ID = s.ID, Name = s.Name, Damage = s.Damage, ManaCost = s.ManaCost,
        RecipeID = s.RecipeID, WeaponSpritePath = s.WeaponSpritePath,
        SpriteAngleOffset = s.SpriteAngleOffset, FlipYWhenLeft = s.FlipYWhenLeft, HitEffectAlignBullet = s.HitEffectAlignBullet, HitEffectEnemyOnly = s.HitEffectEnemyOnly, SlashStyle = s.SlashStyle, WeaponAniPath = s.WeaponAniPath,
        WeaponAniNumber = s.WeaponAniNumber, AnimFPS = s.AnimFPS,
        BulletScale = s.BulletScale, CastVisualScale = s.CastVisualScale,
        BeamFxID = s.BeamFxID, BeamWidth = s.BeamWidth,
        FireEffectID = s.FireEffectID, HitEffectID = s.HitEffectID, HitBurstID = s.HitBurstID,
        TrailEffectID = s.TrailEffectID, TrailSpikeID = s.TrailSpikeID, FlameStyleID = s.FlameStyleID, SpiritFxID = s.SpiritFxID, SummonEffectID = s.SummonEffectID,
        FamiliarVfxId = s.FamiliarVfxId, FamiliarSize = s.FamiliarSize, FamiliarSpin = s.FamiliarSpin,
        DropperStyle = s.DropperStyle, DropperVfxId = s.DropperVfxId, DropperSize = s.DropperSize,
        Recipe = s.Recipe, BulletPrefab = s.BulletPrefab,
        WeaponSprite = s.WeaponSprite, WeaponSprites = s.WeaponSprites,
    };

    // ───────────────────────── 給 UI 看的說明 ─────────────────────────

    /// <summary>把全身效果整理成人看得懂的字串（tooltip/除錯用）；沒有效果回空字串。</summary>
    public string Describe()
    {
        if (!HasAny) return "";
        var sb = new System.Text.StringBuilder();
        var gems = ItemManager.Gems;
        foreach (var kv in _levels)
        {
            var g = gems.Get(kv.Key);
            sb.Append(g != null ? g.Describe(kv.Value) : $"#{kv.Key} Lv{kv.Value}").Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }
}
