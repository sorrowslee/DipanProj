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
///
/// ⚠ <b>上面那條「== null 自動重建」的慣例，對「陣列／集合型」的 UnityEngine.Object 快取不成立。</b>
/// 停止 Play 被銷毀的是<b>容器裡的元素</b>，容器本身（`Sprite[]`、`List&lt;Sprite&gt;`、`Dictionary&lt;_, Sprite&gt;`）
/// 是純 C# 物件、<b>永遠不會變 null</b> → `if (arr == null) arr = Load()` 不會觸發，第二次 Play 拿到一整包已銷毀的物件
/// （症狀：該素材整組不見／變白塊，且只有第二次以後的 Play 會出現）。
/// 兩種解法擇一：
///   (a) 在該類別自己判元素，例：`if (arr == null || arr.Length == 0 || arr[0] == null) arr = Load();`
///       —— 見 <c>SegmentedLightningColumn.IsStale</c>（2026-07-27 修）。
///   (b) 在本檔明確把該 static 設回 null。
/// <b>新增任何「陣列／集合型的 UnityEngine.Object static 快取」時，務必照上面其中一種處理。</b>
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
        Dipan.Save.SaveManager.DevFreshCharacter = false;
        Dipan.Save.SaveManager.DevPreClearedModules = null;
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

        // 資料表快取（provider 載入的靜態表）：清掉，下次存取重讀。
        // 尤其 Language——若曾在 provider 接好前載過一次會殘留空表，之後接好也不重載 → 字串全變 [lang:id]。
        Dipan.Localization.Language.ResetForPlayMode();
        Dipan.UI.ForgingPanel.ResetForPlayMode();   // 鍛造台的鎖定版本號（背包靠它輪詢重畫）
        Dipan.UI.SlotDragController.ResetForPlayMode();   // 拖放層的鎖定鉤子＋殘留的拖曳狀態
        SceneFxTable.ResetForPlayMode();
        ScreenFxTable.ResetForPlayMode();
        SegmentedLightningColumn.ResetForPlayMode();   // 拼接雷柱的 Sprite 快取（字典型，容器不會變 null）
        MapDepthSort.ResetForPlayMode();               // zOrder 超範圍的一次性警告旗標

        // 抽選系統的資料表（池登記表／各池基本表／血統表／血統系列表）＋血統系統的單例。
        // 四張表都是「provider 提供 TextAsset、載一次就快取」，殘留的話 provider 接好也不重載。
        Dipan.Gacha.GachaPoolTable.ResetForPlayMode();
        Dipan.Gacha.GachaRollTable.ResetForPlayMode();
        Dipan.Gacha.BloodlineTable.ResetForPlayMode();
        Dipan.Gacha.BloodlineSeriesTable.ResetForPlayMode();   // 血統系列表（表A：系列 → 三階段的反查索引）
        Dipan.Gacha.BloodlineSystem.ResetForPlayMode();
        Dipan.Gacha.BloodlineTransformFxRunner.ResetForPlayMode();   // 變身演出的 IsPlaying（殘留會鎖死背包熱鍵）
        Dipan.UI.BloodlineIntroPanel.ResetForPlayMode();             // 立繪揭示面板的 IsShowing（同上，殘留一樣鎖死熱鍵）
        Dipan.Localization.LocalizedArt.ResetForPlayMode();          // 「缺某語言的圖」只警告一次的名單（不清的話下一輪 Play 就不再提醒）

        // 能力珠鑲嵌（見 readme/GEM_SOCKET.md）
        Dipan.Inventory.ItemManager.ResetForPlayMode();   // 能力珠定義表快取（GemTable）
        Dipan.Inventory.ForgeSockets.ResetForPlayMode();  // 孔位除錯覆寫（DebugCount）
        WeaponManager.ResetForPlayMode();                 // 玩家專屬的武器解析器（static 委派，會指向上一輪的玩家）
        Dipan.UI.ItemIcons.ResetForPlayMode();            // 珠子疊圖的 Sprite 快取（字典型快取，容器不會變 null）
        Dipan.UI.IconFit.ResetForPlayMode();              // icon 內容邊界框快取（同樣是字典型的 Sprite 快取）

        // 劇情演出「隱藏主角」的開關：殘留成 true 的話，下一輪 Play 的 Hide() 會直接 return
        // ⇒ 主角再也藏不起來（要重開 Unity 才會好）。同一家族：BloodlineTransformFxRunner.IsPlaying。
        Dipan.Cutscene.PlayerVisibility.ResetForPlayMode();

        // 發光物登記表：清上一輪殘留（AtmosphereController 取最近發光地上物靠它）。
        LightSource.ClearAll();
    }
}
