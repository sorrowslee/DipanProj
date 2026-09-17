using System.Collections.Generic;
using UnityEngine;
using Sorrows.Ballistics;

/// <summary>
/// 怪物「使用一把武器」的統一入口（模組化 seam）。給 boss／強怪掛：<see cref="Configure"/> 指定
/// WeaponTable 的武器 ID，Brain 每幀呼叫 <see cref="TryUse"/> 決定是否施放；冷卻（配方 FireInterval）
/// 與召喚上限都在這裡結算，Brain 只管「想不想放」。
///
/// 依配方型別分派：
///   ‧ <c>Summon</c>  → 召喚（<see cref="SummonSystem"/>）。
///   ‧ <c>Normal</c>  → **直飛投射彈**（2026-09-17 接上）：走 <see cref="WeaponCastService"/>，
///     與玩家的 <c>ShootNormal</c> 是同一份彈道生成程式，所以分裂／反彈／追蹤／平行／穿透／軌跡
///     這些配方欄位對怪物一樣有效。第一個使用者＝狂族弩手（BrainType=Archer）拿「狂族十字弓」。
///   ‧ 其餘模式（Laser／Parabolic／SkyStrike／Chain／Orbital／Melee／Dash／GroundCast）仍是 Phase 2 的後續，
///     等它們一種一種搬進 WeaponCastService 之後，這裡不必改就會自動支援。
///
/// **怪物的子彈打得到誰**：由 <see cref="TryUse"/> 傳進來的 target 決定——碰撞層取「目標所在的那一層」
/// （射玩家＝Player 層，射敵對怪＝Enemy 層）。所以牠的箭天生不會被自己人擋住、也不會誤傷同伴，
/// 不需要在命中時再補一次陣營判斷。真正的「能不能傷害」仍由 <c>CombatSystem</c> 查 <c>FactionRelations</c> 決定。
///
/// 設計刻意「擁有者無關」：召喚邏輯只依賴 transform 位置與傳入的配方，
/// 未來要讓主角也能召喚，把 <see cref="TrySummon"/> 抽成共用靜態helper、雙方各持一份 alive 清單即可。
/// </summary>
public class MonsterWeaponUser : MonoBehaviour
{
    private MonsterController _owner;
    private int _weaponId = -1;
    private WeaponData _weapon;      // 懶解析：WeaponManager 開場才載好，故第一次 TryUse 時才抓
    private bool _resolved;
    private float _cooldown;         // 剩餘冷卻秒
    private readonly List<GameObject> _summoned = new List<GameObject>();  // 追蹤召喚出的分身（給同時上限用）
    private Collider2D _selfCol;     // 自己的身體框（算出手點用：從框外緣射出，箭才不會疊在身上）
    private VfxManager _vfx;         // 擊中特效用（懶取）
    private bool _vfxLookedUp;
    private int _envMask = -1;       // 障礙層（Environment + Water），與 MonsterActuator 同一份定義
    private bool _noPrefabWarned;
    private float _nextResolveAt;    // 下次可以重試解析的時間（WeaponManager 還沒載好時的節流）
    private bool _noManagerWarned;

    public void Configure(MonsterController owner, int weaponId)
    {
        _owner = owner;
        _weaponId = weaponId;
        _resolved = false;
        _weapon = null;
    }

    void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
    }

    /// <summary>
    /// 確保武器已解析。**任何會看 <see cref="Ready"/> 或 <see cref="TryUse"/> 的路徑都要先經過這裡**——
    /// 起手緩衝是在 <see cref="Resolve"/> 裡才寫進 `_cooldown` 的，沒解析就問 Ready 會得到「冷卻是 0＝可以放」的假答案。
    /// （2026-09-17 的空砲 bug 就是這樣來的，見 readme/PROBLEMS.md **F25**。）
    /// </summary>
    private void EnsureResolved()
    {
        if (_resolved) return;
        if (Time.time < _nextResolveAt) return;   // WeaponManager 還沒載好，等一下再試
        Resolve();
    }

    private void Resolve()
    {
        if (_weaponId <= 0) { _resolved = true; return; }

        var wm = FindObjectOfType<WeaponManager>();
        if (wm == null)
        {
            // ⚠ 這是**時序問題**（WeaponManager 開場才載好），不是設定錯誤 ⇒ 不標記已解析、稍後重試。
            //   舊版在這裡就 _resolved = true，萬一怪比 WeaponManager 早一步初始化，牠這輩子都不會再嘗試解析武器。
            //   節流是為了避免每幀 FindObjectOfType。
            _nextResolveAt = Time.time + 0.5f;
            if (!_noManagerWarned)
            {
                _noManagerWarned = true;
                Debug.LogWarning("[MonsterWeaponUser] 場景暫時找不到 WeaponManager，稍後重試（若持續出現才是真的沒放）。");
            }
            return;
        }

        _resolved = true;   // 找得到 WeaponManager 之後才定案：以下失敗都是設定問題，重試也沒用
        _weapon = wm.GetWeapon(_weaponId);
        if (_weapon == null)
        {
            Debug.LogWarning($"[MonsterWeaponUser] 武器 ID {_weaponId} 在 WeaponTable 找不到。");
            return;
        }
        // 起手緩衝：生成後先等一個冷卻週期才第一次施放，避免一冒出來就立刻出手。
        float interval = (_weapon.Recipe != null && _weapon.Recipe.Data != null) ? _weapon.Recipe.Data.FireInterval : 1f;
        _cooldown = Mathf.Max(0.1f, interval);
    }

    /// <summary>
    /// 冷卻好了沒。
    /// ⚠ **這個 getter 有副作用：它會先確保武器已解析**。必須如此——起手緩衝是解析時才寫進冷卻的，
    /// 少了這一步，Brain 在怪剛生成時會得到 `Ready == true` 的假答案，
    /// 等它把出手動作演完才在 <see cref="TryUse"/> 裡撞上剛被寫入的冷卻 ⇒ **動作播了、東西沒出去**。
    /// </summary>
    public bool Ready
    {
        get { EnsureResolved(); return _cooldown <= 0f; }
    }

    /// <summary>
    /// Brain 呼叫：冷卻好了就依配方型別施放。回傳「這次是否真的施放」。
    /// <paramref name="target"/> 只有投射型武器需要（決定射向與子彈打得到哪一層）；召喚型忽略它。
    /// </summary>
    public bool TryUse(Transform target = null)
    {
        EnsureResolved();
        if (_weapon == null || _weapon.Recipe == null) return false;
        if (_cooldown > 0f) return false;

        RecipeEntry recipe = _weapon.Recipe;
        bool cast = false;

        if (recipe.Mode == WeaponMode.Summon)
            cast = TrySummon(recipe);
        else if (recipe.Mode == WeaponMode.Normal)
            cast = TryFireProjectile(recipe, target);
        else
            Debug.LogWarning($"[MonsterWeaponUser] 武器 '{_weapon.Name}' 的模式 {recipe.Mode} 還沒搬進 WeaponCastService（見該檔說明），怪物暫時不能用。");

        if (cast)
        {
            float interval = (recipe.Data != null) ? recipe.Data.FireInterval : 1f;
            _cooldown = Mathf.Max(0.1f, interval);
            if (_owner != null) _owner.NotifySkillCast();   // 施法成功 → 讓怪演 attack 出手動作
        }
        return cast;
    }

    /// <summary>擁有者（boss）死亡時呼叫：回收所有還活著的召喚分身——boss 死了、招式（召喚物）也該及時撤掉。</summary>
    public void RecallSummons()
    {
        foreach (var go in _summoned)
            if (go != null) Destroy(go);
        _summoned.Clear();
    }

    // ── 投射型：直飛彈 ──────────────────────────────────────────────────────────
    /// <summary>
    /// 朝 <paramref name="target"/> 射一發。彈道／分裂／反彈／追蹤全部交給 <see cref="WeaponCastService"/>，
    /// 這裡只負責「怪物特有」的三件事：出手點、射向、以及命中之後對目標結算傷害。
    /// </summary>
    private bool TryFireProjectile(RecipeEntry recipe, Transform target)
    {
        if (target == null) return false;
        if (_weapon.BulletPrefab == null)
        {
            // BulletPrefab 由 WeaponManager 統一指派；沒有就代表 WeaponManager 的 Inspector 沒設 Bullet Prefab。
            if (!_noPrefabWarned)
            {
                _noPrefabWarned = true;
                Debug.LogWarning($"[MonsterWeaponUser] 武器 '{_weapon.Name}' 沒有 BulletPrefab（檢查場景 WeaponManager 的 Bullet Prefab 欄位），怪物射不出東西。");
            }
            return false;
        }

        Vector2 selfPos = transform.position;
        Vector2 dir = (Vector2)target.position - selfPos;
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir.Normalize();

        if (_envMask == -1) _envMask = LayerMask.GetMask("Environment", "Water");

        var ctx = new WeaponCastService.CastContext
        {
            Owner        = gameObject,
            Origin       = selfPos + dir * MuzzleOffset(),
            Direction    = dir,
            OwnerScale   = 1f,                              // 怪物體型不放大牠的箭（箭是武器的，不是牠身體的一部分）
            TargetLayers = 1 << target.gameObject.layer,    // 射玩家＝Player 層；射敵對怪＝Enemy 層
            EnvLayer     = _envMask,
            OnHit        = OnProjectileHit,
            OnTrailPoint = null,                            // 軌跡特效目前只有玩家武器在用
        };
        // ⚠ 回傳 FireNormal 的結果，不要無條件 return true：
        //   recipe.Data 是 null（配方沒建好）或武器沒有子彈圖時，子彈根本沒生成，
        //   無條件回 true 會讓上層以為射出去了 ⇒ 照樣進冷卻、照樣播動畫，變成**沒有任何警告的空砲**。
        return WeaponCastService.FireNormal(_weapon, recipe.Data, in ctx);
    }

    /// <summary>
    /// 出手點要推出自己的身體框，否則箭一生出來就疊在怪身上（看起來像從肚子裡冒出來）。
    /// 用碰撞框的實際大小算，換一隻體型差很多的怪也不必改數字。
    /// </summary>
    private float MuzzleOffset()
    {
        if (_selfCol == null) _selfCol = GetComponent<Collider2D>();
        return _selfCol != null ? _selfCol.bounds.extents.magnitude + 0.1f : 0.4f;
    }

    /// <summary>
    /// 子彈命中：對目標結算傷害＋播擊中特效。
    /// 傷害走中央 <c>CombatSystem</c>（它會查 <c>FactionRelations</c> 決定這一下算不算數、要不要打折），
    /// 所以這裡不做任何陣營判斷；打到牆這種沒有 IDamageable 的東西會自動被略過。
    /// </summary>
    private void OnProjectileHit(BulletInstance bullet, GameObject hitTarget, RaycastHit2D hit)
    {
        if (hitTarget == null || _weapon == null) return;

        // ⚠⚠ **這個 callback 的壽命比「射出它的那隻怪」長**：箭飛在半空中的時候，射手可能已經被打死、
        //    GameObject 已經被 Destroy。Unity 被 destroy 的物件在 C# 端**不是真的 null**（只有覆寫過的
        //    `==` 才看得出來），所以 `_weapon != null` 這種純 C# 欄位的檢查**完全擋不住**——
        //    一碰 `transform` / `gameObject` 就 MissingReferenceException。
        //    實測症狀：一次清掉一群弓箭手時，空中每一顆箭命中都拋一次，Console 整頁紅字（見 readme/PROBLEMS.md **F26**）。
        //    `this == null` 走的是 UnityEngine.Object 覆寫的 ==，這是唯一正確的判斷方式。
        if (this == null) return;

        // 擊退方向用「箭的飛行方向」而不是「從射手到目標」：箭可能已經反彈／追蹤過好幾次，射手也可能離很遠，
        // 沿飛行方向擊退才符合畫面。順帶也少一個對 transform 的依賴。
        Vector2 dir = (bullet != null && bullet.Velocity.sqrMagnitude > 0.0001f)
            ? bullet.Velocity.normalized
            : ((Vector2)hitTarget.transform.position - (Vector2)transform.position).normalized;

        CombatSystem.Apply(gameObject, hitTarget, _weapon.Damage, dir);

        if (_weapon.HitEffectID > 0)
        {
            if (!_vfxLookedUp) { _vfxLookedUp = true; _vfx = FindObjectOfType<VfxManager>(); }
            if (_vfx != null)
            {
                Vector2 at = (hit.point != Vector2.zero) ? hit.point
                           : (bullet != null ? (Vector2)bullet.transform.position : (Vector2)hitTarget.transform.position);
                _vfx.Spawn(_weapon.HitEffectID, at, 0f, _weapon.BulletScale);
            }
        }
    }

    // 召喚：委派給玩家/怪物共用的 SummonSystem（同時上限用本元件自己的 _summoned 清單追蹤）。
    private bool TrySummon(RecipeEntry recipe)
    {
        int vfxId = (_weapon != null) ? _weapon.SummonEffectID : 0;
        return SummonSystem.Cast(gameObject, transform.position, recipe, _summoned, MonsterFaction.Enemy, vfxId);
    }
}