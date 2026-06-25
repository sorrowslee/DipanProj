using UnityEngine;

/// <summary>
/// 中央戰鬥傷害結算。**所有傷害都從這裡走一遍**：武器原始傷害 → 攻擊方加成 → 受擊方減傷／抗性
/// → （未來）暴擊 → 交給目標的 <see cref="IDamageable"/> 結算（無敵時間／閃爍／擊退仍由目標自己處理）。
///
/// 設計邊界（沿用專案紀律）：傷害「數值修正」集中在這裡；**目標不自己算加成**，彈道系統更不算傷害。
/// 加成／減傷的具體數字由各單位的 <see cref="ICombatModifiers"/> 提供（目前多為 1，掛勾就位、之後接 CSV）。
/// 見 readme/COMBAT.md。
/// </summary>
public static class CombatSystem
{
    /// <summary>
    /// 結算一次傷害。回傳「實際打進目標的最終傷害」（被無敵時間擋掉時，IDamageable 內部會忽略，但這裡仍回傳計算值）。
    /// </summary>
    public static float Apply(in DamageInfo info)
    {
        if (info.Target == null || info.BaseAmount <= 0f) return 0f;

        float amount = info.BaseAmount;

        if (!info.IgnoreModifiers && info.Type != DamageType.True)
        {
            // 1) 攻擊方加成（讀來源身上的 ICombatModifiers；沒有 = 乘 1）
            if (info.Source != null)
            {
                var dealer = info.Source.GetComponent<ICombatModifiers>();
                if (dealer != null)
                    amount *= Mathf.Max(0f, dealer.OutgoingDamageMultiplier(info));
            }

            // 2) 受擊方減傷／抗性（讀目標身上的 ICombatModifiers；沒有 = 乘 1）
            var receiver = info.Target.GetComponent<ICombatModifiers>();
            if (receiver != null)
                amount *= Mathf.Max(0f, receiver.IncomingDamageMultiplier(info));
        }

        if (amount <= 0f) return 0f;

        // 3) 交給目標結算（無敵時間／白光閃爍／擊退由各自的 IDamageable + HitReactionHandler 處理）
        var damageable = info.Target.GetComponent<IDamageable>();
        if (damageable == null) return 0f;

        damageable.TakeDamage(amount, info.HitDirection);
        return amount;
    }

    /// <summary>便利多載：一般武器命中（實體傷害）。source 可傳 null。</summary>
    public static float Apply(GameObject source, GameObject target, float baseAmount, Vector2 hitDirection,
        DamageType type = DamageType.Physical, bool isDot = false)
    {
        DamageInfo info = new DamageInfo
        {
            Source = source,
            Target = target,
            BaseAmount = baseAmount,
            HitDirection = hitDirection,
            Type = type,
            IsDot = isDot,
            IgnoreModifiers = false,
        };
        return Apply(info);
    }
}
