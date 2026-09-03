using UnityEngine;

/// <summary>
/// 可破壞地上物：有 HP,被子彈/光束命中時扣血,歸零時把<b>自己那張圖</b>炸成碎片
/// (<see cref="ShatterBurst"/>),然後銷毀自己 —— 碰撞一併消失,原本擋路/反彈的格子自動開通。
///
/// <para><b>2026-09-03 起不再播共用的破壞特效</b>：VfxTable 那顆煙塵套在石雕、木桶、布幔上都長一樣,
/// 看起來像「每種材質都同一種破法」。改成碎片後,碎塊直接來自該物件的貼圖,材質差異免費附帶。
/// 舊路徑<b>沒有刪掉</b>,VfxTable 的那一列也還在——把 <see cref="PlayLegacyDestroyVfx"/> 打開就會照舊播,
/// 要 A/B 比對或想「碎片＋煙塵」兩層一起上時直接開即可。</para>
///
/// 由 MapLoader 在生成地上物時掛上並 Configure;傷害由 PlayerController.HandleBulletHit /
/// HandleBeamTick 在命中 Environment 層且帶此元件的目標時呼叫 TakeDamage。
/// </summary>
public class DestructibleObject : MonoBehaviour, IDamageable
{
    [Tooltip("血量;被武器 Damage 扣到 <= 0 即破壞")]
    public float MaxHP = 20f;

    [Tooltip("破壞時播的特效 = VfxTable 的 ID;0 = 不播。檔名/張數/FPS 都在 VfxTable 那一列設定。\n"
             + "⚠ 目前預設不播（見 Play Legacy Destroy Vfx），破壞演出改由程序化碎片負責。")]
    public int DestroyVfxId = 5;

    [Tooltip("是否播舊的共用破壞特效（VfxTable 的 Destroy Vfx Id）。\n"
             + "預設關閉：共用煙塵讓每種材質看起來都同一種破法,已改用「把自己那張圖炸成碎片」。\n"
             + "打開 = 碎片與煙塵兩層一起播（要 A/B 比對時用）。")]
    public bool PlayLegacyDestroyVfx = false;

    [Tooltip("破壞時是否炸出碎片（程序化,取自本物件當前那一幀的圖）。關掉 = 直接消失。")]
    public bool PlayShatter = true;

    [Tooltip("破壞時把此旗標設為 true（給觸發鏈 requireFlag 用，例：打破珍貴供品→改變關卡走向）。" +
             "由地圖編輯器物件的「破壞觸發旗標」欄填入、每個擺放各自設定；空＝不寫。")]
    public string BreakFlag;

    float _hp;
    bool _dead;
    static VfxManager _vfx;   // 全場唯一,快取共用

    // 關卡進度：本張地圖唯一的地上物 key（由 MapLoader 設）；破壞時記進 RunProgress，換圖回來不再重建。
    int _runMapId = -1;
    string _runKey;

    /// <summary>由 MapLoader 設定血量與破壞特效 ID。</summary>
    public void Configure(float maxHP, int destroyVfxId, string breakFlag = null)
    {
        MaxHP = maxHP;
        DestroyVfxId = destroyVfxId;
        BreakFlag = breakFlag;
        _hp = maxHP;
    }

    /// <summary>由 MapLoader 設定關卡進度用的地圖 id 與物件 key（破壞時記進 RunProgress，本趟不再重建）。</summary>
    public void SetRunKey(int mapId, string key)
    {
        _runMapId = mapId;
        _runKey = key;
    }

    void Awake() { _hp = MaxHP; }

    /// <summary>
    /// 受擊扣血;歸零則破壞。
    /// <paramref name="hitDirection"/> 是<b>擊退方向</b>(來源 → 目標,見 <c>DamageInfo.HitDirection</c>),
    /// <b>不是命中座標</b>——舊版這個參數叫 hitPoint 但從來沒被用到,別被名字騙了。碎片會順著它飛。
    /// </summary>
    public void TakeDamage(float damage, Vector2 hitDirection)
    {
        if (_dead || damage <= 0f) return;
        _hp -= damage;
        if (_hp <= 0f) Die(hitDirection);
    }

    void Die(Vector2 hitDirection)
    {
        _dead = true;

        // 破壞寫旗標（資料驅動）：物件有填「破壞觸發旗標」時設為 true，供觸發鏈條件用（例：打破供品→改變劇情走向）。
        if (!string.IsNullOrEmpty(BreakFlag)) TriggerChain.SetFlag(BreakFlag);

        // 關卡進度：記本趟這個地上物已破壞（換圖回來不再重建）。非 run 期間由 RunProgress 內部忽略。
        if (!string.IsNullOrEmpty(_runKey) && RunProgress.Exists)
            RunProgress.Instance.MarkObjectDestroyed(_runMapId, _runKey);

        var sr = GetComponentInChildren<SpriteRenderer>();

        // 破壞演出：把自己那張圖炸成碎片。動畫地上物會取「當前這一幀」（噴泉的水花因此會停在被打爆的瞬間）。
        // 必須在 Destroy 之前呼叫——ShatterBurst 要讀這個物件的 transform 與 sprite。
        if (PlayShatter && sr != null) ShatterBurst.Play(sr, hitDirection);

        // 舊的共用破壞特效：預設關閉（見檔頭）。程式與 VfxTable 都保留,打開即回舊行為。
        if (PlayLegacyDestroyVfx && DestroyVfxId > 0)
        {
            if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
            if (_vfx != null)
            {
                // 特效大小跟著地上物：縮放到「這個物件的可見高度」（同招喚特效的做法，見 VfxManager.SpawnSizedToHeight）。
                if (sr != null && sr.bounds.size.y > 0.0001f)
                    _vfx.SpawnSizedToHeight(DestroyVfxId, sr.bounds.center, sr.bounds.size.y);
                else
                    _vfx.Spawn(DestroyVfxId, transform.position, 0f);
            }
            else Debug.LogWarning("[DestructibleObject] 場景找不到 VfxManager,破壞特效略過。");
        }
        Destroy(gameObject);
    }
}
