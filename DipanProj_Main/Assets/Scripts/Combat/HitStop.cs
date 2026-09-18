using UnityEngine;

/// <summary>
/// **命中定格（hit stop）**：在重擊的那一瞬間把時間壓到幾乎停住，再彈回正常速度。
/// 格鬥／動作遊戲「重量感」的核心——**同樣的動畫，加了 0.06 秒的定格就會從「碰到」變成「砸到」**。
///
/// <para>用法一行：<c>HitStop.Play(0.06f)</c>。誰都能叫，不必自己管協程或還原。</para>
///
/// <para><b>為什麼不用協程</b>：協程要掛在某個物件上，而會呼叫這支的多半是「打到人的那個東西」——
/// 怪、子彈、一次性傷害圈——**它們很可能在定格結束前就被銷毀**（怪被反殺、傷害圈 0.12 秒自毀），
/// 協程跟著中斷 ⇒ <c>Time.timeScale</c> 永遠卡在 0.05，整個遊戲變成慢動作。
/// 所以定格由一個 <c>DontDestroyOnLoad</c> 的常駐載體用 <c>unscaledDeltaTime</c> 倒數，與呼叫者的生死無關。</para>
///
/// <para>⚠ <b>還原時只還原「自己設的那個值」</b>：專案用 <c>timeScale = 0</c> 當暫停
/// （<c>UIManager</c> 開面板時），如果定格期間玩家開了背包，無條件寫回 1 就會**把暫停解除掉**。
/// 所以還原前先確認 <c>timeScale</c> 仍是定格值——被別人改過就什麼都不做，讓對方作主。</para>
///
/// <para>⚠ 定格期間 <c>Time.deltaTime</c> 幾乎是 0，所以**吃 deltaTime 的演出會一起慢下來**
/// （同 readme/PROBLEMS.md D13/D14 的機制，只是這裡是刻意的、而且只有 0.06 秒）。
/// 要在定格期間照常速播的東西，用 <c>Time.unscaledDeltaTime</c>。</para>
/// </summary>
public static class HitStop
{
    /// <summary>定格期間的時間流速。0 會讓某些吃 deltaTime 的系統整格卡住，留一點點比較安全。</summary>
    public const float DefaultScale = 0.05f;

    static HitStopRunner _runner;

    /// <summary>
    /// 定格 <paramref name="seconds"/> 秒（**真實時間**，不受 timeScale 影響）。
    /// 已經在定格中時取「剩餘時間較長」的那個，不會疊加變成慢動作。
    /// </summary>
    public static void Play(float seconds, float scale = DefaultScale)
    {
        if (seconds <= 0.0001f) return;
        if (Time.timeScale <= 0.0001f) return;   // 遊戲本來就是暫停的（開著面板）→ 不要插手

        EnsureRunner();
        if (_runner != null) _runner.Begin(seconds, Mathf.Clamp(scale, 0f, 0.9f));
    }

    static void EnsureRunner()
    {
        if (_runner != null) return;
        var go = new GameObject("~HitStopRunner");
        go.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<HitStopRunner>();
    }

    /// <summary>關閉 Domain Reload 時的 static 快取重置（見 readme/PROBLEMS.md I8）。</summary>
    public static void ResetForPlayMode() { _runner = null; }
}

/// <summary>定格的常駐載體。見 <see cref="HitStop"/> 的說明——它單獨存在就是為了「不會被銷毀」。</summary>
public class HitStopRunner : MonoBehaviour
{
    float _left;
    float _appliedScale;

    public void Begin(float seconds, float scale)
    {
        // 已在定格中 → 取剩餘時間較長者（連續命中不會疊成慢動作）
        if (_left > 0f) { _left = Mathf.Max(_left, seconds); return; }
        _appliedScale = scale;
        _left = seconds;
        Time.timeScale = scale;
    }

    void Update()
    {
        if (_left <= 0f) return;
        _left -= Time.unscaledDeltaTime;   // ⚠ 一定要 unscaled：定格中 deltaTime 幾乎是 0，會永遠倒數不完
        if (_left > 0f) return;

        _left = 0f;
        // 只還原「自己設的那個值」——期間被別人改過（開面板 ⇒ 0）就放手，見 HitStop 的類別註解。
        if (Mathf.Approximately(Time.timeScale, _appliedScale)) Time.timeScale = 1f;
    }

    void OnDisable()
    {
        // 保險：載體萬一被關掉（切場景的極端情況），也要把時間還回去，絕不留下一個慢動作的遊戲。
        if (_left > 0f && Mathf.Approximately(Time.timeScale, _appliedScale)) Time.timeScale = 1f;
        _left = 0f;
    }
}
