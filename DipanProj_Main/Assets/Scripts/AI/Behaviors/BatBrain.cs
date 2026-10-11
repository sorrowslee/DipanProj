using UnityEngine;

/// <summary>
/// 蝙蝠類（`BrainType=Bat`，第一隻：吸血鬼陣營「血蝠」Bloodbat，2026-10-11）。
/// 繼承 <see cref="FlyingBrain"/>：盤旋、V 字俯衝、咬的命中幀全部沿用基底，這裡只多了「倒吊」這件蝙蝠才有的事。
/// 見 readme/BOSS_MODULE.md §12。
///
/// <list type="bullet">
///   <item><b>休息＝倒吊</b>：在棲點上方 <see cref="HangHeightInHumans"/> 個人身高的高處播 <c>hang</c>，不起伏。
///         出生時就倒吊在出生點上方。</item>
///   <item><b>醒來</b>（目標進入 DetectionRange，或被打）：先往下一掉（鬆爪），再展翅進盤旋。</item>
///   <item><b>失去目標</b>：不回出生點，而是在**地圖上方一帶**隨機挑一個飛得到的點重新倒吊
///         （作者 2026-10-11 拍板「隨便找個點再次倒掛，但盡量是整張地圖上方一點的位置」）。</item>
/// </list>
///
/// <para>坐在地上的 <c>idle</c> 不在這支裡：那是部隊列隊（和平段）用的，走 NPC 系統擺（NpcTable），
/// NPC 本來就會播 idle（作者 2026-10-11 確認）。</para>
/// </summary>
public class BatBrain : FlyingBrain
{
    /// <summary>倒吊的高度（人身高的倍數）。比盤旋高（1.25）再高一截＝掛在屋簷／樹枝的感覺。</summary>
    protected virtual float HangHeightInHumans => 1.7f;

    /// <summary>俯衝放慢（作者 2026-10-11 實測「太迅速」）：25fps × 0.6 ⇒ 起手 0.67 秒、命中在 0.93 秒、落點在命中前 0.53 秒鎖定。
    /// 只放慢 attack，不動 CSV 的 AnimFPS——那會連振翅一起變慢。</summary>
    protected override float AttackFpsMul => 0.6f;

    /// <summary>
    /// 發現玩家後先倒掛著再待 ≈3 秒才醒（作者 2026-10-11：偵查範圍要大，但不想玩家完全沒看到倒掛的模樣）。
    /// ±0.4 秒隨機：一群一起發現時不會同一瞬間全部鬆爪。倒掛期間會轉頭面向目標（HandleVisuals 的面向），等於「牠注意到你了」的預告。
    /// </summary>
    protected override float WakeDelaySeconds() => Random.Range(2.6f, 3.4f);

    const float RoostBandFraction = 0.3f;  // 新棲點從「可走範圍最上面這一成比例」裡挑
    const int RoostTries = 40;
    const float WakeDropSeconds = 0.28f;   // 鬆爪往下掉的時間
    const float WakeDropDistance = 0.8f;   // 掉多少（世界單位）

    float _wakeStart, _wakeFromH;

    protected override MonsterAnimator.State RestPose => MonsterAnimator.State.Hang;
    protected override float RestHeight => HangHeightInHumans * HumanHeight;

    // ── 醒來：鬆爪往下掉（加速），掉完交給基底進盤旋（盤旋高度比倒吊低，基底會繼續往下靠）──
    protected override void BeginWake()
    {
        _wakeStart = Time.time;
        _wakeFromH = _height;
    }

    protected override bool TickWake(float dt)
    {
        float k = Mathf.Clamp01((Time.time - _wakeStart) / WakeDropSeconds);
        _height = _wakeFromH - WakeDropDistance * k * k;   // 像重力一樣越掉越快
        return k >= 1f;
    }

    // ── 失去目標：到地圖上方另找棲點 ──
    protected override void OnTargetLost()
    {
        _home = PickRoost();
    }

    Vector2 PickRoost()
    {
        var nav = MapNavGrid.Instance;
        if (nav == null || !nav.Ready) return _spawnGround;

        Rect b = nav.WalkableBounds();
        float yMin = b.yMax - b.height * RoostBandFraction;
        Vector2 fallback = _spawnGround;
        bool haveFallback = false;
        for (int i = 0; i < RoostTries; i++)
        {
            Vector2 p = new Vector2(Random.Range(b.xMin, b.xMax), Random.Range(yMin, b.yMax));
            if (!Flyable(p)) continue;
            if (HasFlyLine(_ground, p)) return p;          // 直線飛得到 ⇒ 就是它
            if (!haveFallback) { fallback = p; haveFallback = true; }   // 飛不直達的先記著（基底會沿牆滑過去，卡住會就地休息）
        }
        return fallback;
    }
}
