using UnityEngine;

/// <summary>
/// 可破壞地上物：有 HP,被子彈/光束命中時扣血,歸零時在自身位置播一次破壞特效
/// (VfxTable 的 ID,沿用既有 VFX 系統),然後銷毀自己 —— 碰撞一併消失,原本擋路/反彈的格子自動開通。
///
/// 由 MapLoader 在生成地上物時掛上並 Configure;傷害由 PlayerController.HandleBulletHit /
/// HandleBeamTick 在命中 Environment 層且帶此元件的目標時呼叫 TakeDamage。
/// </summary>
public class DestructibleObject : MonoBehaviour, IDamageable
{
    [Tooltip("血量;被武器 Damage 扣到 <= 0 即破壞")]
    public float MaxHP = 20f;

    [Tooltip("破壞時播的特效 = VfxTable 的 ID;0 = 不播。檔名/張數/FPS 都在 VfxTable 那一列設定。")]
    public int DestroyVfxId = 5;

    [Tooltip("破壞時把此旗標設為 true（給觸發鏈 requireFlag 用，例：打破珍貴供品→改變關卡走向）。" +
             "由地圖編輯器物件的「破壞觸發旗標」欄填入、每個擺放各自設定；空＝不寫。")]
    public string BreakFlag;

    float _hp;
    bool _dead;
    static VfxManager _vfx;   // 全場唯一,快取共用

    /// <summary>由 MapLoader 設定血量與破壞特效 ID。</summary>
    public void Configure(float maxHP, int destroyVfxId, string breakFlag = null)
    {
        MaxHP = maxHP;
        DestroyVfxId = destroyVfxId;
        BreakFlag = breakFlag;
        _hp = maxHP;
    }

    void Awake() { _hp = MaxHP; }

    /// <summary>受擊扣血;歸零則破壞。hitPoint 目前用於未來擴充(例如朝命中方向噴碎片),v1 特效固定播在物件中心。</summary>
    public void TakeDamage(float damage, Vector2 hitPoint)
    {
        if (_dead || damage <= 0f) return;
        _hp -= damage;
        if (_hp <= 0f) Die();
    }

    void Die()
    {
        _dead = true;

        // 破壞寫旗標（資料驅動）：物件有填「破壞觸發旗標」時設為 true，供觸發鏈條件用（例：打破供品→改變劇情走向）。
        if (!string.IsNullOrEmpty(BreakFlag)) TriggerChain.SetFlag(BreakFlag);

        if (DestroyVfxId > 0)
        {
            if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
            if (_vfx != null) _vfx.Spawn(DestroyVfxId, transform.position, 0f);
            else Debug.LogWarning("[DestructibleObject] 場景找不到 VfxManager,破壞特效略過。");
        }
        Destroy(gameObject);
    }
}
