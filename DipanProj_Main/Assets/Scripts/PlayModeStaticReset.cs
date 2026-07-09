using UnityEngine;

/// <summary>
/// 已關閉 Domain Reload（EditorSettings 的 Enter Play Mode Options，換取「按 Play 幾乎瞬間」）後，
/// C# 的 <c>static</c> 不會在每次進入 Play 時自動歸零 —— 上一輪 Play 的殘留（旗標、快取、<b>static 事件訂閱</b>…）
/// 會讓「第二次以後的 Play」行為異常（最典型：static 事件累積訂閱者＝重複觸發 / 呼叫到已銷毀物件；
/// 抑制旗標殘留＝進場黑畫面 / 載錯進度）。
///
/// 這支在每次進入 Play 的**最早期**（SubsystemRegistration，早於任何場景與 bootstrap）把會出問題的 static 統一重置。
/// build 每次是全新程序、本來就乾淨，這段等於無害的 no-op。
///
/// 注意：**只重置「純 C# 的可變 static」**（集合／旗標／事件／快取）。
/// UnityEngine.Object 的 static 快取（程序生成的 sprite/texture 等）靠既有的 `if (x == null) x = Build()` 慣例會自動重建
/// （Unity 對已銷毀物件的 `== null` 回 true），不需要在這裡處理。
/// 若之後又踩到「第二次 Play 才出現」的殘留，把該類別的 static 加進這裡清即可。
/// </summary>
public static class PlayModeStaticReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        // 流程 / 抑制旗標：回到乾淨預設，之後由各 bootstrap（GameFlowBootstrap / DevQuickStart）依情境設定。
        // 殘留這些會造成「dev 直接進關卡時 SuppressAutoStart 還是上一輪的 true → MapManager 不啟動 → 全黑」。
        MapManager.SuppressAutoStart = false;
        MapManager.DevStartMapId = 0;
        MapManager.DevStartModuleOverride = null;
        Dipan.Save.SaveManager.SuppressAutoLoad = false;
        Dipan.Flow.GameFlowManager.TitleFlowEnabled = true;

        // 觸發鏈：清 static 集合／快取／**事件訂閱**（OnTriggerFired 跨 Play 會累積 → 重複觸發、呼叫到已銷毀的 TutorialManager 等）。
        TriggerChain.ResetForPlayMode();

        // 素材庫單例：走「執行期從 StreamingAssets 載入的 Texture/Sprite」的懶漢單例——static 快取會回傳
        // 上一輪 Play 結束時被銷毀的 sprite → 角色/怪物只剩影子、劇情圖/立繪變空白。丟掉單例，下次存取重載乾淨的圖。
        PlayerSpriteLibrary.ResetForPlayMode();
        MonsterSpriteLibrary.ResetForPlayMode();
        Dipan.Drama.DramaDatabase.ResetForPlayMode();
        Dipan.Drama.DramaTalkDatabase.ResetForPlayMode();

        // 傷害數字管理器：懶漢單例 + _quitting 守衛。停止 Play 的 OnApplicationQuit 會把 _quitting 設 true，
        // 沒 Domain Reload 就殘留 → 下次 Play 的 Instance 被擋成 null、頭上傷害數字消失。歸零解死結。
        DamageNumberManager.ResetForPlayMode();

        // 旗標登記表快取：丟掉上一輪載入的 flags.json，下次存取重讀。
        Dipan.MapRuntime.FlagRegistry.Reload();

        // 怪物登記表：清上一輪殘留（接觸傷害/友軍找目標靠它）。
        MonsterController.Active.Clear();
    }
}
