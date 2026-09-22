using System.Collections;
using UnityEngine;

/// <summary>
/// **把玩家震退到一個定點**（鏈動作 `pushPlayer`）：在指定秒數內把他從當下位置「推」過去，
/// 而不是瞬移——要的是「被震飛」的過程，瞬移看不出誰把他打飛的。
///
/// <para>用在夢境教學：小怪清空後把玩家震回入口，佛掌才從上方壓下來
/// （手掌只有一張圖、只能從上往下走，玩家站在別的位置對打會很怪）。
/// 正式關卡打邪佛時整段可以重用。</para>
///
/// <para>⚠⚠ <b>跨數秒的演出，三個既有的坑一定要避開</b>：<br/>
/// ① **輸入鎖用具名版** <c>SetExternalHold(owner,…)</c>（PROBLEMS **D13**）——
///    用無名版的話，任何別的系統解鎖時會把我們這份一起解掉。<br/>
/// ② **位移用 `Rigidbody2D.MovePosition` 不是改 `transform.position`**：玩家身上有 Rigidbody2D，
///    直接寫 transform 會跟物理打架（穿牆、或被下一次物理步驟拉回去）。<br/>
/// ③ **吃 `Time.deltaTime` 的演出會被 `PausesGame` 面板凍住**（PROBLEMS **D14**）。這裡是刻意的：
///    震退期間本來就不該有面板開著；真的被凍住也只是慢一點，不會壞。</para>
///
/// <para>⚠ 牆：走的是 <c>MovePosition</c>，所以撞到牆會被擋下來、停在牆前，不會穿過去。
/// 落點要塗在**可走的地面**上。</para>
/// </summary>
[DisallowMultipleComponent]
public class PlayerShove : MonoBehaviour
{
    const string HoldOwner = "PlayerShove";

    /// <summary>正在震退中（給外部查詢，避免重複觸發）。</summary>
    public bool IsShoving { get; private set; }

    Rigidbody2D _rb;
    Coroutine _co;

    void Awake() => _rb = GetComponent<Rigidbody2D>();

    /// <summary>
    /// 把玩家推到 <paramref name="target"/>，耗時 <paramref name="seconds"/> 秒。
    /// <paramref name="onDone"/> 在**到位之後**呼叫（鏈要等震退結束才接下一顆，不然骨牢會在半空中長出來）。
    /// </summary>
    public void Shove(Vector2 target, float seconds, System.Action onDone)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Run(target, Mathf.Max(0.05f, seconds), onDone));
    }

    IEnumerator Run(Vector2 target, float seconds, System.Action onDone)
    {
        IsShoving = true;
        // 具名鎖：震退期間玩家不能操作（見檔頭 ①）
        Dipan.UI.UIManager.Instance?.SetExternalHold(HoldOwner, true, false);

        Vector2 from = transform.position;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            // 先快後慢＝被打飛的手感（等速看起來像被拖著走）
            float e = 1f - (1f - k) * (1f - k);
            Vector2 p = Vector2.Lerp(from, target, e);
            if (_rb != null) _rb.MovePosition(p);
            else transform.position = p;
            yield return null;
        }
        if (_rb != null) _rb.MovePosition(target);
        else transform.position = target;
        if (_rb != null) _rb.velocity = Vector2.zero;

        Dipan.UI.UIManager.Instance?.SetExternalHold(HoldOwner, false, false);
        IsShoving = false;
        _co = null;
        onDone?.Invoke();
    }

    /// <summary>場景被拆掉時別把鎖留著（例如震退途中玩家死亡／換圖）。</summary>
    void OnDisable()
    {
        if (!IsShoving) return;
        IsShoving = false;
        Dipan.UI.UIManager.Instance?.SetExternalHold(HoldOwner, false, false);
    }
}
