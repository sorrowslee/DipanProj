using UnityEngine;

/// <summary>
/// 陣營關係的**單一真相**：誰能傷誰（<see cref="Hostile"/>）、會不會攻擊玩家（<see cref="AttacksPlayer"/>）、
/// 怪打怪的傷害乘數（<see cref="DamageMultiplier"/>）、該放哪個 Layer（<see cref="ApplyLayer"/>）。
/// 接觸傷害（EnemyContactDamage）、友軍/部族找目標（MonsterController.FindNearestEnemy）、
/// 中央傷害結算（CombatSystem）都查這裡；玩家子彈打誰由 Layer 決定（打 Enemy 層），
/// 所以「玩家武器打不到某陣營」＝把那個陣營放 Ally 層（ApplyLayer 統一處理）。
///
/// ── 劇本執行期狀態（狼人×吸血鬼三方陣營劇本）──
/// <see cref="WarActive"/>（開戰了沒）與 <see cref="PlayerAllied"/>（玩家結盟哪一族）由觸發鏈動作
/// `factionWar`／`joinFaction` 設定（見 TriggerChain）；生命週期＝**關卡單次**——
/// 換 module 時由 TriggerChain.ClearLevelFlags 呼叫 <see cref="ResetScenario"/> 清空（同關卡單次旗標）。
///
/// ── 規則（2026-08-28 作者定案）──
///  ‧ 和平（WarActive=false）：兩族視同中立——不打人、不被打、玩家武器打不到（Ally 層）。
///  ‧ 開戰：兩族互為敵對（**演戲傷害 ×1/100**：他們的存在是在演戲，殺敵主力是玩家）；
///    兩族攻擊玩家（正常傷害）、切到 Enemy 層（玩家可打）。未選邊時靠「兩族擺得近、索敵挑最近」
///    讓他們先互咬，不寫「不打主角」的特例。
///  ‧ 結盟：該族不再攻擊玩家、切回 Ally 層（玩家武器天生打不到）；兩族之間照打（仍 1/100）。
///  ‧ Enemy（一般怪）與兩族**刻意互不敵對**——既有內容與劇本部族各管各的。
/// 未來更多陣營／更複雜關係：把 Hostile/DamageMultiplier 改成查資料表，呼叫端不動。
/// </summary>
public static class FactionRelations
{
    /// <summary>兩族互打的傷害乘數（演戲用；對玩家、玩家對怪一律 1）。</summary>
    public const float TheatricalDamageMultiplier = 0.01f;

    /// <summary>開戰了沒（factionWar 鏈動作設定；換關卡重置）。</summary>
    public static bool WarActive { get; private set; }

    /// <summary>玩家結盟的部族；null＝未結盟（joinFaction 鏈動作設定；換關卡重置）。</summary>
    public static MonsterFaction? PlayerAllied { get; private set; }

    /// <summary>是不是「劇本部族」（狼人/吸血鬼）。</summary>
    public static bool IsTribe(MonsterFaction f) => f == MonsterFaction.Werewolf || f == MonsterFaction.Vampire;

    // ───────────────────────── 查詢 ─────────────────────────

    /// <summary>a 能不能傷害 b（接觸傷害、怪打怪、找目標都用這條）。</summary>
    public static bool Hostile(MonsterFaction a, MonsterFaction b)
    {
        if (a == b) return false;
        if (a == MonsterFaction.Neutral || b == MonsterFaction.Neutral) return false;

        // 既有規則：一般敵怪 ↔ 玩家召喚物
        if ((a == MonsterFaction.Enemy && b == MonsterFaction.PlayerAlly) ||
            (a == MonsterFaction.PlayerAlly && b == MonsterFaction.Enemy)) return true;

        // 劇本部族：開戰才互咬；玩家召喚物視同玩家（開戰後對「非結盟」的部族敵對）
        if (IsTribe(a) && IsTribe(b)) return WarActive;
        if (a == MonsterFaction.PlayerAlly && IsTribe(b)) return WarActive && PlayerAllied != b;
        if (IsTribe(a) && b == MonsterFaction.PlayerAlly) return WarActive && PlayerAllied != a;

        return false;   // Enemy ↔ 部族：刻意不敵對（各管各的）
    }

    /// <summary>這個陣營會不會攻擊「玩家本人」。</summary>
    public static bool AttacksPlayer(MonsterFaction a)
    {
        if (a == MonsterFaction.Enemy) return true;
        if (IsTribe(a)) return WarActive && PlayerAllied != a;
        return false;
    }

    /// <summary>這個陣營現在有沒有「怪物敵人」要找（MonsterController 據此決定要不要掃登記表）。</summary>
    public static bool HasMonsterFoes(MonsterFaction a)
    {
        if (a == MonsterFaction.PlayerAlly) return true;      // 打 Enemy（既有）＋開戰後打非結盟部族
        if (IsTribe(a)) return WarActive;                     // 開戰後打另一族
        return false;
    }

    /// <summary>怪打怪的傷害乘數：兩族互打＝演戲（1/100）；其餘 1。玩家相關傷害不經此函式（CombatSystem 只對怪×怪查）。</summary>
    public static float DamageMultiplier(MonsterFaction attacker, MonsterFaction target)
        => (IsTribe(attacker) && IsTribe(target)) ? TheatricalDamageMultiplier : 1f;

    // ───────────────────────── Layer（玩家武器打不打得到＝在不在 Enemy 層） ─────────────────────────

    /// <summary>
    /// 依陣營與當前劇本狀態把 go 放到正確的 Layer。找不到對應層就不動（單場景測試容錯）。
    /// <paramref name="inspectorEnemyLayer"/>：MonsterSpawner Inspector 指定的 Enemy 層索引（-1＝用 FactionLayers 解析的）。
    /// </summary>
    public static void ApplyLayer(GameObject go, MonsterFaction f, int inspectorEnemyLayer = -1)
    {
        if (go == null) return;
        int enemyL = inspectorEnemyLayer >= 0 ? inspectorEnemyLayer : FactionLayers.EnemyLayer;
        int allyL = FactionLayers.AllyLayer;

        int want;
        if (f == MonsterFaction.PlayerAlly || f == MonsterFaction.Neutral) want = allyL;
        else if (IsTribe(f)) want = AttacksPlayer(f) ? enemyL : allyL;   // 開戰且未結盟＝可被玩家打；和平/結盟＝打不到
        else want = enemyL;

        if (want >= 0) go.layer = want;
    }

    // ───────────────────────── 劇本狀態切換（TriggerChain 的鏈動作呼叫） ─────────────────────────

    /// <summary>開戰：兩族開始互咬＋攻擊玩家；場上部族怪全部切到可被玩家攻擊的層。</summary>
    public static void StartWar()
    {
        if (WarActive) return;
        WarActive = true;
        RefreshTribeLayers();
        Debug.Log("[FactionRelations] 三方陣營開戰。");
    }

    /// <summary>玩家結盟某一族：該族不再攻擊玩家、玩家武器打不到它（切回 Ally 層）。</summary>
    public static void JoinFaction(MonsterFaction f)
    {
        if (!IsTribe(f))
        {
            Debug.LogWarning($"[FactionRelations] joinFaction 只能結盟部族（Werewolf/Vampire），收到 {f}，忽略。");
            return;
        }
        PlayerAllied = f;
        RefreshTribeLayers();
        Debug.Log($"[FactionRelations] 玩家結盟：{f}。");
    }

    /// <summary>清掉劇本狀態（回和平、未結盟）。換 module 時由 TriggerChain.ClearLevelFlags 呼叫＝「只在這趟劇本內有效」。</summary>
    public static void ResetScenario()
    {
        if (!WarActive && PlayerAllied == null) return;
        WarActive = false;
        PlayerAllied = null;
        RefreshTribeLayers();
    }

    /// <summary>進 Play 模式歸零（已關 Domain Reload；由 PlayModeStaticReset 呼叫）。</summary>
    public static void ResetForPlayMode()
    {
        WarActive = false;
        PlayerAllied = null;
    }

    // 劇本狀態變了 → 把場上「部族」怪的 Layer 重套一遍（其他陣營的層不隨劇本狀態變，不動）。
    static void RefreshTribeLayers()
    {
        var list = MonsterController.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var mc = list[i];
            if (mc == null || !IsTribe(mc.Faction)) continue;
            ApplyLayer(mc.gameObject, mc.Faction);
        }
    }

    // ───────────────────────── 解析（CSV 的 Faction 欄／joinFaction 的參數） ─────────────────────────

    /// <summary>字串 → 陣營（中英皆可、不分大小寫）。認不得回 null。</summary>
    public static MonsterFaction? Parse(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        switch (s.Trim().ToLowerInvariant())
        {
            case "enemy": case "敵人": case "敵怪": return MonsterFaction.Enemy;
            case "neutral": case "中立": return MonsterFaction.Neutral;
            case "werewolf": case "狼人": return MonsterFaction.Werewolf;
            case "vampire": case "吸血鬼": return MonsterFaction.Vampire;
            case "playerally": case "友軍": return MonsterFaction.PlayerAlly;
            default: return null;
        }
    }
}
