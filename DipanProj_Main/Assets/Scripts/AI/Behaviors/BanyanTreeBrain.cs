using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 榕樹妖 Boss 戰鬥模組。本體「不可直接打」——樹是背景、臉是地上物——玩家要打牠的攻擊物「地刺」(<see cref="BossSpike"/>)
/// 來反傷本體。本體＝**無圖隱形的 MonsterController**，只管血量＋編排攻擊。手感常數在下方，要調就改這裡。
///
/// 血量階段（讀 <see cref="MonsterController.HealthFraction"/>）：
///  ‧ &gt;50%：隨機灑地刺（慢、少）。
///  ‧ 50%~20%：加量加速。
///  ‧ &lt;20%：繼續灑地刺，且**每隔幾波夾一招大絕**，兩招輪流——① 橫掃（多排推進浪）② 放大版（指玩家、大傷害）。
/// 排掃的「推進浪」＝同一排地刺用遞增的 startDelay 錯開時間冒出（見 BossSpike）。
/// </summary>
public class BanyanTreeBrain : IMonsterBrain
{
    // ── 通用 ──
    // 【測試開關】強制指定階段，方便一進關卡就測某階段：0 = 依血量自動切換（正式用）、1/2/3 = 強制那一階段。測完記得改回 0。
    const int   ForcePhase       = 0;
    const float DetectionRange   = 40f;   // 整個場地都算得到玩家（本體不動，要能一直施壓）
    const float FirstVolleyDelay = 1.5f;  // 進場後第一波的緩衝

    // 【階段切換血量】填「百分比的小數」：0.5 = 50%、0.1 = 10%。血量『低於等於』該值就進入下一階段。
    // 第一階段不用填（滿血就是 P1）。填的值：P2_HpEnter 要比 P3_HpEnter 大。
    const float P2_HpEnter = 0.7f;   // 血量 ≤ 這個比例 → 進入階段二（例：0.5 = 50% 以下進 P2）
    const float P3_HpEnter = 0.3f;   // 血量 ≤ 這個比例 → 進入階段三（例：0.1 = 10% 以下進 P3）
    const float SpikeDamage      = 10f;   // 一般地刺碰玩家傷害
    const float SpikeScale       = 1f;    // 一般地刺大小

    // ── 地刺生成的「安全內縮」：可走區的最外圈（尤其貼著底部操控列 HUD／左右血魔球）不要冒地刺 ──
    // 隨機灑、排掃、放大版都吃這個內縮框。單位＝世界座標；覺得還太靠邊就把數值調大。
    const float SpawnEdgeInset   = 0.5f;  // 上／左／右 往內縮（避開牆邊、左右血魔球上方）
    const float SpawnBottomInset = 1.0f;  // 底部（HUD 那一側）多縮一點，地刺不貼著操控列冒出

    // ── 階段一 (>50%) ──
    const float P1_Interval = 3.0f;
    const int   P1_Spikes   = 3;

    // ── 階段二 (50%~20%)：加量加速 ──
    const float P2_Interval = 1.8f;
    const int   P2_Spikes   = 5;

    // ── 階段三 (<20%)：只放兩招大絕、隨機輪流（不夾一般地刺）──
    const float P3_UltimateInterval = 3.5f;  // 兩次大絕之間的間隔（秒）

    // ── 大絕一：橫掃（3 排固定位置，每次隨機挑 2 排、逐排反方向推進）──
    // ↑ 場地縮小後由 5 排改 3 排（2026-07-18）：3 排都落在可走範圍內、彼此更分明。
    const int   Sweep_TotalRows   = 3;     // 由上到下共 3 排固定位置
    const int   Sweep_RowsPerCast = 2;     // 每次放幾排
    const float Sweep_ColStep     = 1.2f;  // 每排上地刺的間距（世界單位）
    const float Sweep_ColStagger  = 0.12f; // 同排相鄰兩根冒出的時間差（＝推進速度）
    const float Sweep_RowGap      = 0.3f;  // 第二排比第一排晚多久起跑
    const float Sweep_EdgeMargin  = 0.3f;  // 避開場地上下邊緣（牆）

    // ── 大絕二：放大版地刺（固定在可走範圍正中央）──
    const float Giant_Scale   = 2.24f; // 2.8 縮到 80%
    const float Giant_Damage  = 30f;
    const float Giant_HitFillW = 0.75f; // 碰撞框寬＝可見地刺寬的比例
    const float Giant_HitFillH = 0.60f; // 碰撞框高＝可見地刺高的比例（只涵蓋下半，貼齊基座；圖上半是空的）

    // ── 大絕三：地刺暴（一次隨機灑一大票，同第 1/2 階段那種一般地刺，只是量爆多）──
    const int   Storm_Spikes = 20;    // 一次生成幾根

    bool _inited;
    MonsterController _boss;
    float _nextAttack;
    int   _lastUlt   = -1;  // 上一招大絕（0 橫掃 / 1 大地刺 / 2 狂亂），-1 = 還沒放過
    int   _ultRepeat = 0;   // 同一招連續放了幾次（達 2 → 下一招強制換別招）

    void EnsureInit(in MonsterContext ctx)
    {
        if (_inited) return;
        _inited = true;
        if (ctx.Sensor != null) ctx.Sensor.DetectionRange = DetectionRange;
        _boss = ctx.Self;
        _nextAttack = Time.time + FirstVolleyDelay;
    }

    public void Think(in MonsterContext ctx)
    {
        EnsureInit(ctx);
        if (ctx.Actuator != null) ctx.Actuator.Stop();   // 榕樹妖不移動

        if (ctx.Player == null) return;                  // 沒偵測到玩家 → 待機
        if (Time.time < _nextAttack) return;

        float hp = (_boss != null) ? _boss.HealthFraction : 1f;
        int phase = (ForcePhase != 0) ? ForcePhase
                  : (hp > P2_HpEnter ? 1 : (hp > P3_HpEnter ? 2 : 3));
        Debug.Log($"[Banyan] volley phase={phase} hp={hp:0.00}");   // 暫時除錯

        switch (phase)
        {
            case 1:
                _nextAttack = Time.time + P1_Interval;
                RandomVolley(P1_Spikes);
                break;
            case 2:
                _nextAttack = Time.time + P2_Interval;
                RandomVolley(P2_Spikes);
                break;
            default:   // 3
                Phase3(ctx);
                break;
        }
    }

    // 階段三：只放大絕、隨機輪流（不夾一般地刺）。三招：橫掃浪、放大版、地刺暴。
    void Phase3(in MonsterContext ctx)
    {
        _nextAttack = Time.time + P3_UltimateInterval;

        // 隨機挑一招，但「同一招最多連兩次」：若上一招已連放 2 次，這次就從另外兩招裡挑。
        int pick;
        if (_ultRepeat >= 2 && _lastUlt >= 0)
        {
            int r = Random.Range(0, 2);              // 從另外兩招（跳過 _lastUlt）
            pick = (r < _lastUlt) ? r : r + 1;
        }
        else pick = Random.Range(0, 3);

        if (pick == _lastUlt) _ultRepeat++;
        else { _lastUlt = pick; _ultRepeat = 1; }

        Debug.Log($"[Banyan] ULTIMATE = {(pick == 0 ? "RowSweep" : pick == 1 ? "GiantSpike" : "SpikeStorm")} (repeat={_ultRepeat})");   // 暫時除錯
        switch (pick)
        {
            case 0:  RowSweep();   break;
            case 1:  GiantSpike(); break;
            default: SpikeStorm(); break;
        }
    }

    // 大絕三：地刺暴——一次在隨機可走點灑一大票一般地刺（同 1/2 階段的地刺，量爆多）。
    void SpikeStorm()
    {
        RandomVolley(Storm_Spikes);
    }

    // 在隨機可走點灑一波地刺。
    void RandomVolley(int count)
    {
        var nav = MapNavGrid.Instance;
        if (nav == null || _boss == null) return;
        Rect sb = SpawnBounds(nav);
        for (int i = 0; i < count; i++)
        {
            // 取可走點，但排除貼著邊緣／HUD 的最外圈：多試幾次直到落在內縮框內。
            Vector2 p = Vector2.zero; bool ok = false;
            for (int t = 0; t < 12 && !ok; t++)
                if (nav.TryGetRandomWalkable(out p) && sb.Contains(p)) ok = true;
            if (ok) BossSpike.Fire(p, SpikeScale, _boss, SpikeDamage);
        }
    }

    // 可走區外框再往內縮（排除貼著牆／HUD 的最外圈）＝地刺允許生成的範圍。可走區太小縮到反轉時退回其中心點。
    static Rect SpawnBounds(MapNavGrid nav)
    {
        Rect b = nav.WalkableBounds();
        float xMin = b.xMin + SpawnEdgeInset,   xMax = b.xMax - SpawnEdgeInset;
        float yMin = b.yMin + SpawnBottomInset, yMax = b.yMax - SpawnEdgeInset;
        if (xMax < xMin) { float c = (b.xMin + b.xMax) * 0.5f; xMin = xMax = c; }
        if (yMax < yMin) { float c = (b.yMin + b.yMax) * 0.5f; yMin = yMax = c; }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // 大絕一：把 5 排攤在「可走區的 y 範圍」內、只收「有地刺可放」的排（避免整排落在牆上→看起來只出一排），
    // 隨機挑不重複的 Sweep_RowsPerCast 排、逐排反方向（左→右、右→左…）、起跑時間錯開＝推進浪。
    void RowSweep()
    {
        var nav = MapNavGrid.Instance;
        if (nav == null || _boss == null) return;
        Rect b = SpawnBounds(nav);   // 用「內縮框」＝排掃也不貼邊緣／HUD

        // 先算每一排的可走 x 清單，只收有地刺可放的排。
        var rowTy = new List<float>();
        var rowXs = new List<List<float>>();
        for (int i = 0; i < Sweep_TotalRows; i++)
        {
            float t = (Sweep_TotalRows > 1) ? (float)i / (Sweep_TotalRows - 1) : 0.5f;   // 0..1：含最上/最下排
            float ty = Mathf.Lerp(b.yMin + Sweep_EdgeMargin, b.yMax - Sweep_EdgeMargin, t);
            var xs = new List<float>();
            for (float x = b.xMin; x <= b.xMax; x += Sweep_ColStep)
                if (nav.IsWalkableWorld(new Vector2(x, ty))) xs.Add(x);
            if (xs.Count > 0) { rowTy.Add(ty); rowXs.Add(xs); }
        }
        if (rowTy.Count == 0) return;

        // 隨機挑不重複的排（不夠就有幾排放幾排）。
        int want = Mathf.Min(Sweep_RowsPerCast, rowTy.Count);
        var pick = new List<int>();
        int guard = 0;
        while (pick.Count < want && guard++ < 50)
        {
            int idx = Random.Range(0, rowTy.Count);
            if (!pick.Contains(idx)) pick.Add(idx);
        }

        for (int i = 0; i < pick.Count; i++)
        {
            float ty = rowTy[pick[i]];
            var xs = new List<float>(rowXs[pick[i]]);
            if ((i % 2) != 0) xs.Reverse();                 // 第 0 排左→右、第 1 排右→左…
            float baseDelay = i * Sweep_RowGap;
            for (int k = 0; k < xs.Count; k++)
                BossSpike.Fire(new Vector2(xs[k], ty), SpikeScale, _boss, SpikeDamage, baseDelay + k * Sweep_ColStagger);
        }
    }

    // 大絕二：一根放大版地刺，固定在畫面正中間（用場上相機中心；Camera.main 沒 tag 時退回 FindObjectOfType）。
    void GiantSpike()
    {
        if (_boss == null) return;
        var nav = MapNavGrid.Instance;
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindObjectOfType<Camera>();
        Vector2 pos = (cam != null) ? (Vector2)cam.transform.position
                    : (nav != null ? nav.WalkableBounds().center : Vector2.zero); // 畫面正中間（沒相機退回可走中心）
        // 遵循可走規範：把大地刺夾進「可走內縮框」，且落點必須可走（相機中心可能落在樹背景／HUD 等不可走處）。
        if (nav != null)
        {
            Rect sb = SpawnBounds(nav);
            pos = new Vector2(Mathf.Clamp(pos.x, sb.xMin, sb.xMax), Mathf.Clamp(pos.y, sb.yMin, sb.yMax));
            if (!nav.IsWalkableWorld(pos)) pos = sb.center;
        }
        BossSpike.Fire(pos, Giant_Scale, _boss, Giant_Damage, 0f, Giant_HitFillW, Giant_HitFillH);   // 碰撞框貼齊可見地刺（貼基座、只下半）
    }
}
