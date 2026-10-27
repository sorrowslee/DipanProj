using UnityEngine;

/// <summary>
/// GM 測試武器（2026-10-27）。**ID 區間 <see cref="IdMin"/>~<see cref="IdMax"/> 保留給 GM 武器**
/// （WeaponTable 與 ItemTable 同號；配方也放同一段方便辨識，但配方 ID 不受這條規則管）。
///
/// <para>區間內的武器有兩條特例：</para>
/// <list type="number">
///   <item><b>傷害＝目標「最大血量」× <see cref="MaxHpFraction"/></b>（打怪物時；打地上物等非怪物目標照 WeaponTable 的 Damage）。
///         10% ＝ 剛好 10 下打死、每下都跨過一個 10% 門檻（作者 2026-10-27：測頭目的血量門檻用；起初是「當前血量」，越打越少，改掉）。
///         最少 1。之後還是會經過 CombatSystem 的加成／減傷（大多是 1）。</item>
///   <item><b>抽選池永遠不會出現</b>：<c>GachaService.BuildCandidates</c> 組池時直接排除（就算有人把它寫進基本表或解鎖清單）。</item>
/// </list>
/// <para>拿法：作弊面板（L）「給道具」輸入 ID，或「取得所有武器」。</para>
/// <para>傷害改寫只在玩家的結算點（PlayerController 的子彈／雷射／近戰／連鎖／落雷／拋物線爆炸）生效；
/// 被動武器（WeaponFamiliar）與地面特效走自己的傷害路徑，GM 武器做成那些模式時照 Damage 算。</para>
/// </summary>
public static class GmWeapons
{
    public const int IdMin = 900;
    public const int IdMax = 999;

    /// <summary>每一下扣目標最大血量的比例（0.10 ＝ 10%）。</summary>
    public const float MaxHpFraction = 0.10f;

    public static bool IsGm(int id) => id >= IdMin && id <= IdMax;

    /// <summary>這把武器打這個目標該算多少傷害。非 GM 武器原樣回傳 <c>weapon.Damage</c>（既有武器零變化）。</summary>
    public static float DamageAgainst(WeaponData weapon, GameObject target)
    {
        if (weapon == null) return 0f;
        if (!IsGm(weapon.ID) || target == null) return weapon.Damage;
        var mc = target.GetComponentInParent<MonsterController>();
        if (mc == null) return weapon.Damage;
        return Mathf.Max(1f, mc.MaxHealth * MaxHpFraction);
    }
}
