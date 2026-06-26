using UnityEngine;

// 依實際移動速度自動縮放動畫播放速度（走路腳步跟移動速度同步，避免「腳滑」）。
// 純程式、零 Animator 參數：用 Animator.speed 控制整個 Animator；靜止時設回 1，
// 所以 Idle / 死亡等不移動的狀態照常播（特別是死亡不會因速度 0 而凍住）。
// 仿 BlobShadow 的做法：玩家與怪物在各自 Start 自動掛上即可，全角色通用、不必逐隻設定。
// 見 readme/CHARACTER_SETUP.md。
public class AnimatorSpeedByVelocity : MonoBehaviour
{
    [Tooltip("角色的『正常移動速度』。動畫倍率 = 實際速度 / 此值，所以正常走 = 1×（滿幀最順）；" +
             "只有實際速度低於正常時（減速 debuff／類比半推）動畫才按比例變慢。" +
             "玩家在 PlayerController.Start 帶入 MoveSpeed、怪物帶入 MonsterActuator.MoveSpeed。")]
    public float ReferenceSpeed = 5f;

    [Tooltip("最慢倍率（放慢時最低播放速度，避免掉到太低 fps 變超卡）")]
    public float MinMul = 0.6f;

    [Tooltip("最快倍率")]
    public float MaxMul = 2f;

    [Tooltip("速度低於此值視為靜止")]
    public float MoveThreshold = 0.1f;

    private Animator _anim;
    private Rigidbody2D _rb;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (_anim == null) return;

        float spd = (_rb != null) ? _rb.velocity.magnitude : 0f;

        // 靜止／Idle／死亡：正常速度（1×），不要把不動的狀態也減速
        if (spd <= MoveThreshold || ReferenceSpeed <= 0.01f)
        {
            _anim.speed = 1f;
            return;
        }

        // 移動中：播放速度跟著實際速度連續縮放
        _anim.speed = Mathf.Clamp(spd / ReferenceSpeed, MinMul, MaxMul);
    }

    void OnDisable()
    {
        if (_anim != null) _anim.speed = 1f;   // 還原，避免被停用後卡在某倍率
    }
}
