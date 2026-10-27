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
    /// 這一次 TakeDamage 是「演戲傷害」（兩族互打 ×1/100，見 FactionRelations）＝ true。
    /// 只在 Apply 呼叫目標 TakeDamage 的那一瞬間成立（單執行緒、同步呼叫，結束即清）。
    /// MonsterController.TakeDamage 據此**不跳傷害數字、不印 log**——30 隻互毆每下都浮個 0 會把畫面與 Console 洗成彈幕。
    /// 白光閃爍照舊（打起來要有戲）。
    /// </summary>
    public static bool CurrentHitTheatrical { get; private set; }

    /// <summary>
    /// 這一次 TakeDamage 是持續傷害（DOT：燃燒、中毒…）＝ true。同 <see cref="CurrentHitTheatrical"/>，只在呼叫目標 TakeDamage 的那一瞬間成立。
    /// 2026-10-27 為聖子艾爾文的 dodge 加：他會在「被打到的第一下」改成閃避（MonsterController.DamageInterceptor），
    /// 但身上掛著的燃燒每跳一次都去觸發閃避會很怪——那不是「被攻擊」，是已經中了的狀態在結算。
    /// </summary>
    public static bool CurrentHitIsDot { get; private set; }

    /// <summary>
    /// 結算一次傷害。回傳「實際打進目標的最終傷害」（被無敵時間擋掉時，IDamageable 內部會忽略，但這裡仍回傳計算值）。
    /// </summary>
    public static float Apply(in DamageInfo info)
    {
        if (info.Target == null || info.BaseAmount <= 0f) return 0f;

        float amount = info.BaseAmount;

        // 0) 陣營傷害乘數（怪 × 怪才查；玩家相關一律 1）：兩族互打＝演戲 1/100（見 FactionRelations）。
        //    放在最前面、且不受 IgnoreModifiers/True 影響——這是「劇本舞台效果」，不是數值加成。
        bool theatrical = false;
        if (info.Source != null)
        {
            var srcMc = info.Source.GetComponent<MonsterController>();
            var dstMc = info.Target.GetComponent<MonsterController>();
            // 來源不一定是怪物本體：揮擊／踐踏／咬擊／自爆都是另生一顆短命的 ImpactDamageArea（掛 EnemyContactDamage）去打，
            // 那顆物件身上沒有 MonsterController ⇒ 以前這裡查不到陣營、乘數整個被略過，
            // 兩族互打變成**全額傷害**（2026-10-15 作者回報：表演戰的血族士兵很快被打光）。改成退回讀它的 EnemyContactDamage.Faction。
            MonsterFaction? srcFaction = srcMc != null ? srcMc.Faction : (MonsterFaction?)null;
            if (srcFaction == null)
            {
                var ecd = info.Source.GetComponent<EnemyContactDamage>();
                if (ecd != null) srcFaction = ecd.Faction;
            }
            if (srcFaction != null && dstMc != null)
            {
                float fm = FactionRelations.DamageMultiplier(srcFaction.Value, dstMc.Faction);
                if (fm < 0.999f) { amount *= fm; theatrical = true; }
            }
        }

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

        CurrentHitTheatrical = theatrical;
        CurrentHitIsDot = info.IsDot;
        try { damageable.TakeDamage(amount, info.HitDirection); }
        finally { CurrentHitTheatrical = false; CurrentHitIsDot = false; }

        // 這一下被目標閃掉了（進入虛體無敵，例：聖子艾爾文的 dodge）⇒ 不播擊中特效。
        var hitMc = info.Target.GetComponent<MonsterController>();
        if (hitMc != null && hitMc.IsUntargetable) return 0f;

        // 4) 三階血統的擊中特效：玩家打中怪物時，在那隻怪身上播一次（見 BloodlineHitFx）。
        //    掛在這裡是因為**這裡是全遊戲傷害的單一入口**——子彈／雷射 tick／連鎖閃電／AOE 爆炸
        //    四條路都會經過，之後新增武器模式也不必回來補。條件判斷全在 NotifyHit 裡，
        //    沒掛這一層時它就是一個 static null 檢查。
        BloodlineHitFx.NotifyHit(info.Source, info.Target);
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
