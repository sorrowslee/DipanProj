using UnityEngine;
using UnityEditor;

/// <summary>
/// 測試用「直接進某關卡 / 某地圖」快捷（Editor-only：放 Assets/Editor，不會進 build）。
///
/// 開啟後按 Play 會**跳過**「標題 → 存讀檔 → 邪佛廣場 → 開傳送門」流程，直接進指定目標，
/// 方便反覆測。選單在 <b>Project Tools/測試/直接進關卡</b>，有勾＝目前選的；「關閉」＝走正式流程。
///
/// 原理：`AfterAssembliesLoaded`（早於 GameFlowBootstrap 的 BeforeSceneLoad）先關掉
/// <see cref="Dipan.Flow.GameFlowManager.TitleFlowEnabled"/>，再依選擇覆寫 MapManager 開機目標：
///  - **module**（紅嫁衣／初始洞窟）→ 進該關**首張地圖**（<see cref="MapManager.DevStartModuleOverride"/>）。
///  - **地圖 id**（邪佛廣場）→ 直接進**指定地圖**（<see cref="MapManager.DevStartMapId"/>）；因為廣場是 Main 模組的
///    map 12、非首圖，用 module 進不了它。
/// 都**不動場景序列化的 `MapManager.startModule`（＝Main）**，所以「關閉」後正式開場鏈照舊。狀態存 EditorPrefs（只影響本機）。
///
/// 另外：只要選了任一目標（含廣場），就打開 <see cref="Dipan.Save.SaveManager.DevFreshCharacter"/>——
/// 進場時砍掉舊的一次性測試角色、建一個全新乾淨角色（所有旗標／進度／背包歸零），
/// 所以每次按 Play 都能從零開始反覆測（例如新手教學），且完全不動玩家正式三欄存檔。「關閉」則照舊走正式存檔。
///
/// <para><b>邪佛廣場有兩個版本</b>：
///   「初始」＝完成 0 關的乾淨狀態（測開場、新手教學用）；
///   「1 關後」＝額外把 <see cref="PreClearedModule"/> 標記成已通關，讓「要通過 N 關才出現」的東西
///   （抽選祭壇的地上物出現條件 appearAfterClears）直接看得到，不用真的把關卡打完一次。
///   兩者都還是全新測試角色，差別只在進度欄位。</para>
/// </summary>
public static class DevQuickStart
{
    // 空=關閉；"RedBridalGown"/"Main"=進該 module 首圖；"Hub"/"Hub1"=邪佛廣場（用地圖 id）。
    const string PrefKey = "Dipan.DevQuickStart.Target";
    const string Root = "Project Tools/測試/直接進關卡";

    /// <summary>「1 關後」版本要預先標記成已通關的 module。要改成別關就改這裡。</summary>
    const string PreClearedModule = "RedBridalGown";

    const string ItemRed   = Root + "/紅嫁衣 (RedBridalGown)";
    const string ItemCave  = Root + "/初始洞窟 (Main_Cave)";
    const string ItemHub   = Root + "/邪佛廣場-初始 (Main_Square)";
    const string ItemHub1  = Root + "/邪佛廣場-1關後 (Main_Square)";
    const string ItemOff   = Root + "/關閉（走正式標題流程）";

    static string Cur => EditorPrefs.GetString(PrefKey, "");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void Apply()
    {
        // 先清乾淨，避免關閉「Enter Play Mode → Reload Domain」時，上一輪的覆寫殘留。
        MapManager.DevStartMapId = 0;
        MapManager.DevStartModuleOverride = null;
        Dipan.Flow.GameFlowManager.TitleFlowEnabled = true;
        Dipan.Save.SaveManager.DevFreshCharacter = false;   // 先清乾淨（同上，避免關閉時殘留上一輪的 true）
        Dipan.Save.SaveManager.DevPreClearedModules = null; // 同上：不清的話「1關後」選過一次就會黏住
        MapManager.DevLoadingHoldSecondsOverride = 0f;   // 編輯器測試：載入頁不停留那段等待（build 沒這支腳本，維持正式秒數）

        string t = Cur;
        if (string.IsNullOrEmpty(t)) return;   // 關閉＝走正式流程

        Dipan.Flow.GameFlowManager.TitleFlowEnabled = false;   // 跳過標題 / 存讀檔流程
        Dipan.Save.SaveManager.DevFreshCharacter = true;       // 任一測試目標（含廣場）都用全新乾淨角色進場、所有旗標歸零
        if (t == "Hub" || t == "Hub1")
        {
            MapManager.DevStartMapId = Dipan.Save.SaveConstants.HubMapId;   // 邪佛廣場（map 12，非模組首圖）
            if (t == "Hub1")
            {
                // 預先記一關通關 → ClearedModuleCount 與 lifetimeMaxClears 都變 1，
                // 「通過 1 關後才出現」的地上物（抽選祭壇）就會直接出現。
                Dipan.Save.SaveManager.DevPreClearedModules = new[] { PreClearedModule };
                Debug.Log($"[DevQuickStart] 測試模式：直接進邪佛廣場（已預先通關「{PreClearedModule}」＝完成 1 關）。要走正式流程：選單 → 關閉。");
            }
            else
            {
                Debug.Log("[DevQuickStart] 測試模式：直接進邪佛廣場（初始，完成 0 關）。要走正式流程：選單 → 關閉。");
            }
        }
        else
        {
            MapManager.DevStartModuleOverride = t;   // 進該 module 的首張地圖
            Debug.Log($"[DevQuickStart] 測試模式：直接進關卡「{t}」首圖。要走正式流程：選單 → 關閉。");
        }
    }

    [MenuItem(ItemRed)]   static void SetRed()   => Set("RedBridalGown");
    [MenuItem(ItemCave)]  static void SetCave()  => Set("Main");
    [MenuItem(ItemHub)]   static void SetHub()   => Set("Hub");
    [MenuItem(ItemHub1)]  static void SetHub1()  => Set("Hub1");
    [MenuItem(ItemOff)]   static void SetOff()   => Set("");

    // 驗證函式：順便在選單項打勾，讓你一眼看到目前選了哪個。
    [MenuItem(ItemRed, true)]   static bool VRed()   { Menu.SetChecked(ItemRed,   Cur == "RedBridalGown"); return true; }
    [MenuItem(ItemCave, true)]  static bool VCave()  { Menu.SetChecked(ItemCave,  Cur == "Main"); return true; }
    [MenuItem(ItemHub, true)]   static bool VHub()   { Menu.SetChecked(ItemHub,   Cur == "Hub"); return true; }
    [MenuItem(ItemHub1, true)]  static bool VHub1()  { Menu.SetChecked(ItemHub1,  Cur == "Hub1"); return true; }
    [MenuItem(ItemOff, true)]   static bool VOff()   { Menu.SetChecked(ItemOff,  string.IsNullOrEmpty(Cur)); return true; }

    static void Set(string target)
    {
        EditorPrefs.SetString(PrefKey, target ?? "");
        if (string.IsNullOrEmpty(target))
            Debug.Log("[DevQuickStart] 已關閉：下次按 Play 走正式流程（標題 → 存讀檔 → …）。");
        else
            Debug.Log($"[DevQuickStart] 已設定：下次按 Play 直接進「{target}」。（要關掉：同選單 → 關閉）");
    }
}
