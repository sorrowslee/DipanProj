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
/// <para><b>目前套用在</b>：<c>CutsceneDirector</c>（地圖內劇情演出的 ESC 略過）、
/// <c>TalkPanel</c> / <c>DramaPanel</c>（對話面板的 ESC 關閉）。
/// 開場漫畫與墜落動畫有各自的 <c>AllowSkip</c>（語意相同，先不動它們，避免無謂改動）。</para>
///
/// <para>⚠️ <b>不要拿它擋一般 UI 的 ESC</b>：背包、設定、確認彈窗那些本來就該讓玩家用 ESC 關，
/// 只有「跳過劇情」性質的才用這支。</para>
/// </summary>
public static class DevSkip
{
    /// <summary>現在允不允許跳過劇情：編輯器內、或 Development Build ＝ true；正式打包 ＝ false。</summary>
    public static bool Allowed => Application.isEditor || Debug.isDebugBuild;
}
