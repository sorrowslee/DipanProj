using UnityEngine;

/// <summary>
/// **骨牢（怪物端）**：把一隻怪關住一段時間，時間到牢籠崩裂並對牠結算一次大傷害。
/// 由玩家武器 <c>WeaponMode.Cage</c> 施放（見 <c>PlayerController.ShootCage</c>）。
///
/// <para>關住＝<see cref="MonsterController.Caged"/>，語意是「**只鎖移動、放行攻擊**」（怪物端：接觸傷害照算；
/// 動作上，正在揮的那一下播完就回 idle 等牢碎，不再起新的一刀——2026-09-23 作者拍板，實作在 MonsterController.Update），
/// 與玩家端 <c>PlayerController.Bound</c>／<c>TutorialManager.FireOnly</c> 同一套規則（作者拍板）。
/// 實際的攔截點在 <c>MonsterController.Update</c> 的總入口，不是各 Brain——
/// 逐一改每個 Brain 一定會漏，而且之後每加一個新 Brain 都要記得處理。</para>
///
/// <para>⚠ 被關住的怪**照常可以被打**（作者拍板）：骨牢是「活靶」，崩裂傷害是額外的。
/// 所以這支不碰任何受傷判定。</para>
///
/// <para>⚠ 崩裂傷害一定要走 <c>CombatSystem.Apply</c>，**不可以直接扣血**：
/// 直接扣的話吃不到減傷、傷害加成與浮動傷害數字，能力珠也對它無效。</para>
///
/// <para>⚠ 這是個「**有狀態、必須有人收**」的元件（同骨牢玩家版、同循環特效）：
/// 怪死掉、換圖、玩家離開關卡都要解除，否則那隻怪會站著不動而且沒有任何錯誤訊息。
/// <see cref="OnDisable"/> 是最後一道保險。</para>
/// </summary>
[DisallowMultipleComponent]
public class MonsterCage : MonoBehaviour
{
    /// <summary>目前場上由玩家關著的骨牢數。<c>CageMaxTargets</c> 就是拿它來比。</summary>
    public static int ActiveCount { get; private set; }

    /// <summary>進 Play 時歸零（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() => ActiveCount = 0;

    MonsterController _target;
    BoneCageVisual _visual;
    GameObject _caster;       // 誰放的（傷害結算要記來源，才吃得到玩家的加成）
    float _damage;
    float _releaseAt;
    bool _counted;

    /// <summary>
    /// 把一隻怪關起來。回傳是否成功（已經被關住／不可被控制／已死 → false，呼叫端不該扣魔）。
    /// </summary>
    /// <param name="sizeMul">牢籠大小倍率（武器的 <c>BulletScale</c>）。</param>
    public static bool Cage(MonsterController target, GameObject caster, float seconds, float burstDamage, float sizeMul)
    {
        if (target == null || target.IsDead) return false;
        if (!target.Controllable) return false;          // boss 與強怪：CSV 的 Controllable 填 0
        if (target.Caged) return false;                  // 已經關著了，不疊
        if (target.GetComponent<MonsterCage>() != null) return false;

        var cage = target.gameObject.AddComponent<MonsterCage>();
        cage.Begin(target, caster, seconds, burstDamage, sizeMul);
        return true;
    }

    void Begin(MonsterController target, GameObject caster, float seconds, float burstDamage, float sizeMul)
    {
        _target = target;
        _caster = caster;
        _damage = burstDamage;
        _releaseAt = Time.time + Mathf.Max(0.1f, seconds);

        _target.Caged = true;
        ActiveCount++;
        _counted = true;

        var t = target;   // 閉包只抓這個，避免抓整個 this
        float h = target.VisibleBodyHeight;
        if (h <= 0.01f) h = 2f;
        // 尺寸 ＝ max(可見身高 × 0.7, 軀幹寬 × 1.15)：身高不受手上拿什麼影響；軀幹寬保證寬胖的怪（ZhaYu_Bomb 軀幹寬 0.85×身高）
        // 兩隻手不會伸到骨刺外面（作者 2026-09-23：「把怪物完整的包覆在骨牢裡」）。
        // 位置用 idle 的**軀幹 X＋地面線 Y**（見 BuildCageSpot）。身高／軀幹寬都已含體型，大怪自動配大牢（同 F29 的通則）。
        float inner = h * BoneCageVisual.InnerWidthPerBodyHeight;
        var anim = target.GetComponent<MonsterAnimator>();
        float torsoW = anim != null ? anim.CageTorsoWidthLocal * Mathf.Abs(target.transform.lossyScale.x) : 0f;
        inner = Mathf.Max(inner, torsoW * BoneCageVisual.InnerWidthPerTorsoWidth);
        _visual = BoneCageVisual.Spawn(target.gameObject,
            inner * Mathf.Max(0.01f, sizeMul), h,
            () => t != null ? t.FeetWorldPos : (Vector2)target.transform.position,
            BuildCageSpot(target));
    }

    /// <summary>
    /// 籠心的世界座標（每幀呼叫）：idle 的軀幹中心 X ＋ idle 地面線 Y（<c>MonsterAnimator.TryGetCageAnchorLocal</c>），
    /// 再套上**當下**的位置／體型／翻面／離地高度。取不到（舊 Animator 怪、貼圖不可讀）回 null ⇒ BoneCageVisual 退回問影子。
    /// <para>⚠ 為什麼不直接問影子：影子 X 是兩腳中點，拿武器的怪會被拖地的武器拉歪；而且影子錨點逐動作不同，
    /// 每幀跟著走籠子會左右滑。見 PROBLEMS **G15**。</para>
    /// <para>⚠ 翻面時 X 取負（錨點是未翻面的來源圖方向，同 BlobShadow）——被關的怪轉身面向玩家時，籠子會跟著身體對稱移一下，這是對的。</para>
    /// </summary>
    static System.Func<Vector2> BuildCageSpot(MonsterController target)
    {
        var anim = target.GetComponent<MonsterAnimator>();
        if (anim == null || !anim.TryGetCageAnchorLocal(out var local)) return null;
        var sr = target.GetComponent<SpriteRenderer>();
        var air = target.GetComponent<IAirborneVisual>();
        var tr = target.transform;
        return () =>
        {
            if (tr == null) return Vector2.zero;
            Vector3 p = tr.position;
            Vector3 ls = tr.lossyScale;
            float flip = (sr != null && sr.flipX) ? -1f : 1f;
            float airH = air != null ? Mathf.Max(0f, air.AirborneHeight) : 0f;   // 騰空時 transform 被往上推，扣回地面（同 BlobShadow）
            return new Vector2(p.x + local.x * ls.x * flip, p.y + local.y * ls.y - airH);
        };
    }

    void Update()
    {
        if (_target == null || _target.IsDead) { Release(burst: false); return; }
        if (Time.time < _releaseAt) return;
        Release(burst: true);
    }

    /// <summary>提前解除（怪死了、換圖、或之後做「打破牢籠」那類機制時用）。</summary>
    public void ReleaseNow(bool burst) => Release(burst);

    void Release(bool burst)
    {
        // 先把計數與旗標放掉，再做視覺與傷害——中間若丟例外，至少怪不會被永久定住。
        if (_counted) { ActiveCount = Mathf.Max(0, ActiveCount - 1); _counted = false; }
        if (_target != null) _target.Caged = false;

        if (_visual != null)
        {
            if (burst)
            {
                // 崩裂方向：由牢籠往外＝沒有特定方向，交給 ShatterBurst 的「純往外炸」。
                _visual.Burst(Vector2.zero);
            }
            else _visual.Dismiss();
            _visual = null;
        }

        // 崩裂傷害：走一般結算（吃減傷／加成／傷害數字）。怪已經死了就不用補刀。
        if (burst && _damage > 0f && _target != null && !_target.IsDead)
            CombatSystem.Apply(_caster, _target.gameObject, _damage, Vector2.zero);

        Destroy(this);
    }

    void OnDisable()
    {
        // 換圖／怪被停用：static 的計數與怪身上的旗標一定要放掉。
        // 少了這層，下一場的 CageMaxTargets 會被舊的殘留數字佔滿 ⇒ 武器「放不出來」而且沒有錯誤訊息。
        if (_counted) { ActiveCount = Mathf.Max(0, ActiveCount - 1); _counted = false; }
        if (_target != null) _target.Caged = false;
        if (_visual != null) { _visual.Dismiss(); _visual = null; }
    }
}
