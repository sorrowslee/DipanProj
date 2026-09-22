using UnityEngine;

/// <summary>
/// **束縛玩家**（鏈動作 `bindPlayer`）：鎖住移動、**放行攻擊**，並在腳下放一個循環特效當「牢」。
///
/// <para>用在夢境教學：玩家被骨牢關住不能走，只能原地不斷攻擊逼近的佛掌，最後被壓倒。</para>
///
/// <para>鎖的部分完全沿用既有行為——<see cref="PlayerController.Bound"/> 與教學的
/// <c>TutorialManager.FireOnly</c> 共用同一個 Update 分支（鎖移動、放行開火、開火時仍依滑鼠轉身）。
/// 所以這支不碰輸入，只負責「開關旗標 ＋ 管特效的生命週期」。</para>
///
/// <para>⚠ <b>刻意不上 `SetExternalHold`</b>：那會把攻擊一起擋掉，而這裡要的正是「只能打、不能跑」。</para>
///
/// <para>⚠ 特效是 <c>Loop=1</c> 的循環特效，**它不會自己消失**——一定要有人呼叫 <see cref="Unbind"/>。
/// 為了不讓玩家在「忘了解綁」時永遠卡住，本元件在 <c>OnDisable</c>（換圖／死亡）會自己清乾淨。</para>
/// </summary>
[DisallowMultipleComponent]
public class PlayerBind : MonoBehaviour
{
    VfxInstance _cage;
    VfxManager _vfx;

    /// <summary>目前是否被束縛。</summary>
    public bool IsBound => PlayerController.Bound;

    /// <summary>
    /// 綁住玩家。<paramref name="cageVfxId"/> ＝ VfxTable 的循環特效（0 ＝ 只鎖、不放視覺）。
    /// <paramref name="sizeMul"/> 是相對那一列 Scale 的額外倍率。
    /// </summary>
    public void Bind(int cageVfxId, float sizeMul = 1f)
    {
        PlayerController.Bound = true;

        if (cageVfxId <= 0) return;
        if (_vfx == null) _vfx = Object.FindObjectOfType<VfxManager>();
        if (_vfx == null)
        {
            Debug.LogWarning("[PlayerBind] 找不到 VfxManager，骨牢視覺略過（束縛仍生效）。");
            return;
        }

        ClearCage();
        // 畫在腳下 ⇒ 對齊 FeetWorldPos，不要用 transform（玩家的 pivot 在腳底所以兩者相同，
        // 但寫成 FeetWorldPos 才不會在之後換 pivot 時默默偏掉）。
        var pc = GetComponent<PlayerController>();
        Vector2 pos = pc != null ? pc.FeetWorldPos : (Vector2)transform.position;
        _cage = _vfx.SpawnLoop(cageVfxId, pos, sizeMul, 3600f);   // 壽命給很長＝靠 Unbind 收，不靠逾時
    }

    /// <summary>解除束縛並收掉牢籠特效。呼叫幾次都安全。</summary>
    public void Unbind()
    {
        PlayerController.Bound = false;
        ClearCage();
    }

    void ClearCage()
    {
        if (_cage != null) Destroy(_cage.gameObject);
        _cage = null;
    }

    void OnDisable()
    {
        // 換圖／死亡時玩家物件被停用：一定要把 static 的 Bound 放掉，
        // 否則下一場會帶著「不能移動」進去，而且完全沒有錯誤訊息。
        Unbind();
    }

    /// <summary>進 Play 時清 static 狀態（Domain Reload 已關）。由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() => PlayerController.Bound = false;
}
