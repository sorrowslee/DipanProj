using UnityEngine;

/// <summary>
/// 「開發階段才允許跳過劇情」的統一判斷。
///
/// <para><b>為什麼要有</b>：劇情跳過（ESC）是開發時反覆測試用的便利功能，
/// <b>正式版不該讓玩家用它跳掉所有劇情</b>。這支把判斷收成一處，避免各自寫、寫漏。</para>
///
/// <para><b>為什麼用執行期判斷而不是 <c>#if UNITY_EDITOR</c></b>：
/// 這樣 <b>Development Build</b> 也保有跳過功能，方便在真實打包環境測後段流程；
/// 只有正式 release build 才真正關掉。這是本專案既有的慣例
/// （<c>IntroComicController</c> / <c>IntroFallController</c> 各自已有同義的 <c>AllowSkip</c>）。</para>
///
/// <para><b>目前套用在</b>：<c>TalkPanel</c> / <c>DramaPanel</c>（對話面板的 ESC 關閉）。
/// 開場漫畫與墜落動畫有各自的 <c>AllowSkip</c>（語意相同，先不動它們，避免無謂改動）。</para>
///
/// <para>⚠️ <b>2026-08-22 起「跳過」分成兩種，別再混用</b>：
/// <b>作者用資料開關明確允許的跳過</b>（劇情演出的「可略過」、劇情觸發點的「可略過」）是
/// <b>玩家可見的正式功能</b>，走右上角統一的 <c>Dipan.UI.SkipButton</c>，<b>不受本開關限制</b>；
/// 本開關只留給「作者沒開放、但開發時想硬跳」的路徑（例如對話面板的 ESC）。
/// 換句話說：<b>能不能跳由資料決定，DevSkip 只是開發期的萬能鑰匙。</b></para>
///
/// <para><b>唯一的例外：序章整段</b>（初始森林 1/2、那段演出、兩段墜落動畫）——
/// <b>正式版全程不顯示 Skip</b>，開發階段照跳。<b>初始洞窟(11) 起就回到一般規則</b>。
/// 判斷收在 <see cref="SkipAllowedHere"/>，所有 Skip 入口都查它，別在各處自己寫一份。</para>
///
/// <para>⚠️ <b>不要拿它擋一般 UI 的 ESC</b>：背包、設定、確認彈窗那些本來就該讓玩家用 ESC 關，
/// 只有「跳過劇情」性質的才用這支。</para>
/// </summary>
public static class DevSkip
{
    /// <summary>現在允不允許跳過劇情：編輯器內、或 Development Build ＝ true；正式打包 ＝ false。</summary>
    public static bool Allowed => Application.isEditor || Debug.isDebugBuild;

    /// <summary>
    /// 「玩家現在這個地方到底給不給跳」的**統一判斷**——所有 Skip 入口（劇情演出、對話面板）都查這支。
    ///
    /// 規則只有一條例外：**序章整段（初始森林 1/2、那段演出、兩段墜落動畫）正式版全程不給跳**，
    /// 開發階段照跳。<b>初始洞窟(11) 起就回到一般規則</b>——能不能跳完全由作者在編輯器逐段勾選決定。
    ///
    /// 為什麼把例外寫在程式裡而不是「把那兩張圖的可略過取消勾選」：
    /// 取消勾選的話**開發時也跳不了**，而反覆測後段流程正是最需要跳的時候；
    /// 這條規則要的是「正式版不給、開發版給」，那就是 <see cref="Allowed"/> 的語意，資料開關表達不了。
    /// </summary>
    public static bool SkipAllowedHere
    {
        get
        {
            if (Allowed) return true;   // 開發階段：哪裡都能跳
            int mapId = MapManager.Instance != null ? MapManager.Instance.CurrentMapId : 0;
            return !Dipan.Save.SaveConstants.IsNoSkipMap(mapId);
        }
    }
}
