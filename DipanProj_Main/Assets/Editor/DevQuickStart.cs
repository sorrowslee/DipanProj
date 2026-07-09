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
/// </summary>
public static class DevQuickStart
{
    // 空=關閉；"RedBridalGown"/"Main"=進該 module 首圖；"Hub"=邪佛廣場（用地圖 id）。
    const string PrefKey = "Dipan.DevQuickStart.Target";
    const string Root = "Project Tools/測試/直接進關卡";

    const string ItemRed  = Root + "/紅嫁衣 (RedBridalGown)";
    const string ItemCave = Root + "/初始洞窟 (Main_Cave)";
    const string ItemHub  = Root + "/邪佛廣場 (Main_Square)";
    const string ItemOff  = Root + "/關閉（走正式標題流程）";

    static string Cur => EditorPrefs.GetString(PrefKey, "");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void Apply()
    {
        // 先清乾淨，避免關閉「Enter Play Mode → Reload Domain」時，上一輪的覆寫殘留。
        MapManager.DevStartMapId = 0;
        MapManager.DevStartModuleOverride = null;
        Dipan.Flow.GameFlowManager.TitleFlowEnabled = true;

        string t = Cur;
        if (string.IsNullOrEmpty(t)) return;   // 關閉＝走正式流程

        Dipan.Flow.GameFlowManager.TitleFlowEnabled = false;   // 跳過標題 / 存讀檔流程
        if (t == "Hub")
        {
            MapManager.DevStartMapId = Dipan.Save.SaveConstants.HubMapId;   // 邪佛廣場（map 12，非模組首圖）
            Debug.Log("[DevQuickStart] 測試模式：直接進邪佛廣場（Main_Square）。要走正式流程：選單 → 關閉。");
        }
        else
        {
            MapManager.DevStartModuleOverride = t;   // 進該 module 的首張地圖
            Debug.Log($"[DevQuickStart] 測試模式：直接進關卡「{t}」首圖。要走正式流程：選單 → 關閉。");
        }
    }

    [MenuItem(ItemRed)]  static void SetRed()  => Set("RedBridalGown");
    [MenuItem(ItemCave)] static void SetCave() => Set("Main");
    [MenuItem(ItemHub)]  static void SetHub()  => Set("Hub");
    [MenuItem(ItemOff)]  static void SetOff()  => Set("");

    // 驗證函式：順便在選單項打勾，讓你一眼看到目前選了哪個。
    [MenuItem(ItemRed, true)]  static bool VRed()  { Menu.SetChecked(ItemRed,  Cur == "RedBridalGown"); return true; }
    [MenuItem(ItemCave, true)] static bool VCave() { Menu.SetChecked(ItemCave, Cur == "Main"); return true; }
    [MenuItem(ItemHub, true)]  static bool VHub()  { Menu.SetChecked(ItemHub,  Cur == "Hub"); return true; }
    [MenuItem(ItemOff, true)]  static bool VOff()  { Menu.SetChecked(ItemOff,  string.IsNullOrEmpty(Cur)); return true; }

    static void Set(string target)
    {
        EditorPrefs.SetString(PrefKey, target ?? "");
        if (string.IsNullOrEmpty(target))
            Debug.Log("[DevQuickStart] 已關閉：下次按 Play 走正式流程（標題 → 存讀檔 → …）。");
        else
            Debug.Log($"[DevQuickStart] 已設定：下次按 Play 直接進「{target}」。（要關掉：同選單 → 關閉）");
    }
}
