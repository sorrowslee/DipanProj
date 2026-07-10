using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物「使用一把武器」的統一入口（模組化 seam）。給 boss／強怪掛：<see cref="Configure"/> 指定
/// WeaponTable 的武器 ID，Brain 每幀呼叫 <see cref="TryUse"/> 決定是否施放；冷卻（配方 FireInterval）
/// 與召喚上限都在這裡結算，Brain 只管「想不想放」。
///
/// 依配方型別分派：目前實作「召喚」(<c>RecipeEntry.IsSummon</c>)；投射型武器（飛劍／落雷…）
/// 供怪物使用為 Phase 2——待把 PlayerController 的發射管線抽成「不綁玩家」的共用服務後，
/// 這裡多一個分支呼叫該服務即可（怪物與玩家共用同一套武器系統）。
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

    private void Resolve()
    {
        _resolved = true;
        if (_weaponId <= 0) return;

        var wm = FindObjectOfType<WeaponManager>();
        if (wm == null)
        {
            Debug.LogWarning("[MonsterWeaponUser] 場景找不到 WeaponManager，怪物無法使用武器。");
            return;
        }
        _weapon = wm.GetWeapon(_weaponId);
        if (_weapon == null)
        {
            Debug.LogWarning($"[MonsterWeaponUser] 武器 ID {_weaponId} 在 WeaponTable 找不到。");
            return;
        }
        // 起手緩衝：生成後先等一個冷卻週期才第一次施放，避免一冒出來就立刻召喚。
        float interval = (_weapon.Recipe != null && _weapon.Recipe.Data != null) ? _weapon.Recipe.Data.FireInterval : 1f;
        _cooldown = Mathf.Max(0.1f, interval);
    }

    public bool Ready => _cooldown <= 0f;

    /// <summary>Brain 呼叫：冷卻好了就依配方型別施放。回傳「這次是否真的施放」。</summary>
    public bool TryUse()
    {
        if (!_resolved) Resolve();
        if (_weapon == null || _weapon.Recipe == null) return false;
        if (_cooldown > 0f) return false;

        RecipeEntry recipe = _weapon.Recipe;
        bool cast = false;

        if (recipe.IsSummon)
            cast = TrySummon(recipe);
        else
            Debug.LogWarning($"[MonsterWeaponUser] 武器 '{_weapon.Name}' 非召喚型；投射型武器供怪物使用為 Phase 2（待發射管線抽出）。");

        if (cast)
        {
            float interval = (recipe.Data != null) ? recipe.Data.FireInterval : 1f;
            _cooldown = Mathf.Max(0.1f, interval);
        }
        return cast;
    }

    // 召喚：委派給玩家/怪物共用的 SummonSystem（同時上限用本元件自己的 _summoned 清單追蹤）。
    private bool TrySummon(RecipeEntry recipe)
    {
        int vfxId = (_weapon != null) ? _weapon.SummonEffectID : 0;
        return SummonSystem.Cast(gameObject, transform.position, recipe, _summoned, MonsterFaction.Enemy, vfxId);
    }
}