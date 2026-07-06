using System.Collections.Generic;
using UnityEngine;
using Dipan.Inventory;
using Dipan.MapRuntime;
using Dipan.Save;
using Dipan.UI;

/// <summary>
/// 觸發鏈（trigger 接 trigger）的大腦：靜態管理器，每次換圖由 MapManager.Setup 重建。
/// 見 readme/TRIGGER_CHAIN.md。
///
/// 核心概念：任何 trigger 都可以填通用欄位 <c>next</c>（同地圖另一個 trigger 的「名稱或 id」）。
/// 一個 trigger 的動作「完成」後（對話關閉、物品入包…），自動啟動 next 指向的 trigger，
/// next 又可以有自己的 next → 無限接下去。啟動語意依目標類型分兩種：
///   - **動作型**（giveItem / teleportTo / drama 類）：被啟動就立即執行（給物品、傳送、播對話）。
///   - **位置型**（teleport / pickup / cutscene）：被啟動 = 「解鎖」（配合 startDisabled 初始停用），
///     之後玩家踩到/按 F 才生效。teleport 解鎖時會連動顯示 linkedFx 綁定的場景特效（綠幕）。
///
/// 通用欄位（全部選填，存在 region.params）：
///   next          完成後啟動的 trigger（名稱優先，找不到再比對 id）
///   startDisabled 初始停用（等鏈解鎖）；重進地圖時若 enableFlag 旗標成立則自動啟用
///   enableFlag    「已解鎖」狀態的存檔旗標名：解鎖時自動寫 1（跨存讀檔記住門開了）
///   requireFlag   旗標成立才可觸發/執行；前綴 "!" 表否定（例 "!killedFamily" = 沒殺過家人）
///   setFlag       動作完成後寫 1 的旗標名
///   linkedFx      （teleport 專用）場景特效 id：trigger 停用時特效隱藏、解鎖時顯示
///
/// 旗標存進角色存檔 progress.flags（跨存讀檔）；無 SaveManager 時（單場景測試）退回 session 記憶體。
/// </summary>
public static class TriggerChain
{
    public const string TypeGiveItem = "giveItem";
    public const string TypeTeleportTo = "teleportTo";

    // 通用欄位 key
    const string KeyNext = "next";
    const string KeyStartDisabled = "startDisabled";
    const string KeyEnableFlag = "enableFlag";
    const string KeyRequireFlag = "requireFlag";
    const string KeySetFlag = "setFlag";
    const string KeyLinkedFx = "linkedFx";
    const string KeyRequireCycleMax = "requireCycleMax";
    const string KeyRequireCycleMin = "requireCycleMin";
    const string KeyRequireItem = "requireItem";   // 填 itemId=須有；"!itemId"=須無

    static MapData _map;
    static MapManager _manager;
    static Dictionary<string, GameObject> _fxById;          // sceneFx id → 場上物件（MapLoader 提供）
    static readonly HashSet<string> _disabled = new HashSet<string>();       // 目前停用中的 region id
    static readonly Dictionary<string, string> _memFlags = new Dictionary<string, string>(); // 無存檔時的後備旗標

    static TriggerRegion _pendingDramaRegion;   // 等「對話關閉」才算完成的 region（DramaPanel/TalkPanel 關閉時通知）

    /// <summary>換圖後重建：計算每個 trigger 的初始啟用狀態、套用 linkedFx 顯示/隱藏。由 MapManager 呼叫。</summary>
    public static void Setup(MapData map, MapManager manager, Dictionary<string, GameObject> fxById)
    {
        _map = map;
        _manager = manager;
        _fxById = fxById;
        _disabled.Clear();
        _pendingDramaRegion = null;

        if (map?.TriggerLayer?.regions == null) return;
        foreach (var r in map.TriggerLayer.regions)
        {
            if (!r.GetBool(KeyStartDisabled, false)) continue;
            // startDisabled：預設停用；但 enableFlag 旗標成立 = 之前解鎖過（存檔記住）→ 直接啟用。
            string ef = r.GetString(KeyEnableFlag);
            bool unlocked = !string.IsNullOrEmpty(ef) && FlagTrue(ef);
            if (!unlocked) _disabled.Add(r.id);
            ApplyLinkedFx(r, visible: unlocked);
        }
    }

    // ───────────────────────── 查詢（給各 watcher / InteractionManager 用） ─────────────────────────

    /// <summary>此 trigger 目前是否停用（startDisabled 未解鎖）。停用 = 踩到/按 F 都無反應、不顯示星星。</summary>
    public static bool IsDisabled(TriggerRegion r) => r != null && _disabled.Contains(r.id);

    /// <summary>
    /// 觸發條件是否全部成立（AND）。沒填的條件視為通過。目前支援：
    ///   requireFlag     旗標成立（"!flag" = 否定；旗標名可加 "永久:" 前綴＝終身旗標）
    ///   requireCycleMax 周目 ≤ 值（初始限定填 1）
    ///   requireCycleMin 周目 ≥ 值（老手限定）
    ///   requireItem     背包道具：填 itemId=須有；"!itemId"=須無
    /// </summary>
    public static bool RequirementMet(TriggerRegion r)
    {
        if (r == null) return true;

        // 1) 旗標
        string req = r.GetString(KeyRequireFlag);
        if (!string.IsNullOrEmpty(req))
        {
            bool neg = req.StartsWith("!");
            string key = neg ? req.Substring(1).Trim() : req.Trim();
            if (!string.IsNullOrEmpty(key) && FlagTrue(key) == neg) return false;
        }

        // 2) 周目（無存檔時視為第 1 周目，讓單場景測試照樣看得到初始劇情）
        int cycle = CurrentCycle();
        string maxS = r.GetString(KeyRequireCycleMax);
        if (!string.IsNullOrEmpty(maxS) && int.TryParse(maxS, out int cMax) && cycle > cMax) return false;
        string minS = r.GetString(KeyRequireCycleMin);
        if (!string.IsNullOrEmpty(minS) && int.TryParse(minS, out int cMin) && cycle < cMin) return false;

        // 3) 背包道具（無背包系統時 count 視為 0）。"!itemId"＝須無、"itemId"＝須有。
        string itemS = r.GetString(KeyRequireItem);
        if (!string.IsNullOrEmpty(itemS))
        {
            bool mustNotHave = itemS.StartsWith("!");
            string ids = mustNotHave ? itemS.Substring(1).Trim() : itemS.Trim();
            if (int.TryParse(ids, out int iid) && iid > 0)
            {
                bool has = ItemCount(iid) > 0;
                if (has == mustNotHave) return false;   // 須無卻有、或 須有卻無 → 擋
            }
        }

        return true;
    }

    /// <summary>目前周目（= SaveManager.Cycle）；無存檔/無角色時退回 1（＝初始進度），讓單場景測試照常。</summary>
    static int CurrentCycle()
    {
        var sm = SaveManager.Instance;
        int c = sm != null ? sm.Cycle : 1;
        return c <= 0 ? 1 : c;
    }

    /// <summary>背包內某 itemId 的數量；無背包系統時回 0。</summary>
    static int ItemCount(int itemId)
    {
        var inv = InventorySystem.Instance;
        return inv != null ? inv.CountOf(itemId) : 0;
    }

    /// <summary>踩踏/互動型 watcher 的統一入口：停用中或條件不成立 → 此 trigger 視同不存在。</summary>
    public static bool IsActive(TriggerRegion r) => !IsDisabled(r) && RequirementMet(r);

    // ───────────────────────── 完成 → 接鏈 ─────────────────────────

    /// <summary>
    /// 一個 trigger 的動作完成（撿完、對話關了、給完物品…）：寫 setFlag，然後啟動 next。
    /// 各觸發端（InteractionManager / 本類的動作執行）在動作真正結束時呼叫。
    /// </summary>
    public static void OnCompleted(TriggerRegion r)
    {
        if (r == null) return;
        string set = r.GetString(KeySetFlag);
        if (!string.IsNullOrEmpty(set))
        {
            SetFlag(set);
            // 旗標變了 → 本圖可能有 requireFlag 依賴它的互動點要現身/消失，重建一次。
            if (_manager != null) _manager.RefreshTriggers();
        }

        string next = r.GetString(KeyNext);
        if (!string.IsNullOrEmpty(next)) Activate(next.Trim());
    }

    /// <summary>對話型動作：開了 DramaPanel/TalkPanel 後呼叫，等面板關閉才算完成（NotifyDramaClosed）。</summary>
    public static void CompleteAfterDrama(TriggerRegion r)
    {
        if (_pendingDramaRegion != null)
            Debug.LogWarning($"[TriggerChain] 前一個對話完成回呼還沒結（{_pendingDramaRegion.name}），被「{r?.name}」覆蓋。");
        _pendingDramaRegion = r;
    }

    /// <summary>DramaPanel / TalkPanel 關閉時通知（面板 OnClose 呼叫）。無待結 region 時無事。</summary>
    public static void NotifyDramaClosed()
    {
        var r = _pendingDramaRegion;
        _pendingDramaRegion = null;
        if (r != null) OnCompleted(r);
    }

    /// <summary>依名稱（優先）或 id 找到目標 trigger 並啟動：動作型立即執行、位置型解鎖。</summary>
    public static void Activate(string nameOrId)
    {
        var r = Find(nameOrId);
        if (r == null)
        {
            Debug.LogWarning($"[TriggerChain] next=「{nameOrId}」在本地圖找不到對應 trigger（比對名稱與 id 皆無），鏈中止。");
            return;
        }
        if (!RequirementMet(r))
        {
            Debug.Log($"[TriggerChain] 「{r.name}」requireFlag 不成立，鏈在此中止。");
            return;
        }

        switch (r.typeId)
        {
            case TypeGiveItem: ExecuteGiveItem(r); break;
            case TypeTeleportTo: ExecuteTeleportTo(r); break;
            default:
                if (IsDramaType(r)) ExecuteDrama(r);   // 鏈到劇情點 = 立即播對話（對話→對話）
                else EnableRegion(r);                   // 位置型（teleport/pickup/cutscene…）= 解鎖
                break;
        }
    }

    // ───────────────────────── 動作執行 ─────────────────────────

    // 直接給物品（不用按 F）：進背包＋中央 toast；裝不下的掉玩家腳下。完成後接 next。
    static void ExecuteGiveItem(TriggerRegion r)
    {
        int itemId = r.GetInt("itemId", 0);
        int count = Mathf.Max(1, r.GetInt("count", 1));
        var inv = InventorySystem.Instance;
        if (itemId <= 0 || inv == null)
        {
            Debug.LogWarning($"[TriggerChain] giveItem「{r.name}」itemId 無效或無背包系統，略過（仍接 next）。");
            OnCompleted(r);
            return;
        }

        var data = inv.GetData(itemId);
        string display = data != null ? data.Name : $"#{itemId}";
        int leftover = inv.AddItem(itemId, count);
        int added = count - leftover;
        if (added > 0)
            AlertPanel.Toast(added > 1 ? $"獲得 {display} ×{added}" : $"獲得 {display}");
        if (leftover > 0 && InteractionManager.Exists)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Vector2 pos = player != null ? (Vector2)player.transform.position : Vector2.zero;
            InteractionManager.Instance.DropLoot(itemId, leftover, pos);
            AlertPanel.Toast($"背包已滿，{display} ×{leftover} 掉落地上");
        }
        OnCompleted(r);
    }

    // 直接傳送（不用踩傳送點）。換圖 = 鏈的終點（setFlag 會先寫，next 填了也無意義）。
    static void ExecuteTeleportTo(TriggerRegion r)
    {
        // 先把 setFlag 寫掉（換圖後本鏈狀態全清，不能等 OnCompleted）。
        string set = r.GetString(KeySetFlag);
        if (!string.IsNullOrEmpty(set)) SetFlag(set);

        int targetMapId = r.GetInt("targetMapId", -1);
        string targetEntrance = r.GetString("targetEntrance");
        if (targetMapId < 0 || _manager == null)
        {
            Debug.LogWarning($"[TriggerChain] teleportTo「{r.name}」targetMapId 無效或無 MapManager，鏈中止。");
            return;
        }
        _manager.GoToMap(targetMapId, targetEntrance);
    }

    // 鏈到劇情點：立即播對話（不需玩家走過去按 F），對話關閉後接它自己的 next。
    static void ExecuteDrama(TriggerRegion r)
    {
        int dramaId = r.GetInt("dramaId", 0);
        if (dramaId <= 0)
        {
            Debug.LogWarning($"[TriggerChain] 鏈到劇情點「{r.name}」但 dramaId 無效，鏈中止。");
            return;
        }
        CompleteAfterDrama(r);
        var dd = Dipan.Drama.DramaDatabase.Instance.Get(dramaId);
        if (dd != null && dd.Type == 2) Dipan.Drama.DramaTalkController.Play(dd.TalkGroup);
        else Dipan.UI.DramaPanel.Show(dramaId);
        // 面板若沒開成（資料缺），關閉事件不會來 → 鏈停在這，Console 已有各面板的警告可查。
    }

    // 解鎖位置型 trigger：移出停用集、寫 enableFlag（存檔記住）、顯示 linkedFx、要求 MapManager 重建 watcher。
    static void EnableRegion(TriggerRegion r)
    {
        if (!_disabled.Remove(r.id))
        {
            Debug.Log($"[TriggerChain] 「{r.name}」本來就是啟用狀態，無事。");
            return;
        }
        string ef = r.GetString(KeyEnableFlag);
        if (!string.IsNullOrEmpty(ef)) SetFlag(ef);
        ApplyLinkedFx(r, visible: true);
        if (_manager != null) _manager.RefreshTriggers();
        Debug.Log($"[TriggerChain] 解鎖 trigger「{r.name}」({r.typeId})。");
    }

    // ───────────────────────── 旗標（存檔 progress.flags；無存檔時退回記憶體） ─────────────────────────

    // 旗標的生命範圍怎麼決定（方案乙）：
    //   1) 名字帶「永久:」前綴 → 終身（給重複規則的自動旗標、與舊資料相容用）。
    //   2) 否則查旗標登記表 flags.json（FlagRegistry）：登記為 life → 終身，否則周目。
    // 終身存 CharacterSave.lifetimeFlags（跨輪迴）；周目存 progress.flags（輪迴清）。
    // 無 SaveManager（單場景測試）時退回記憶體 _memFlags（用原 key，行為一致但不持久）。
    public const string LifePrefix = "永久:";

    // 回傳（是否終身, 去掉前綴的存檔用 key）。
    static (bool life, string name) Resolve(string key)
    {
        if (key.StartsWith(LifePrefix)) return (true, key.Substring(LifePrefix.Length));
        return (FlagRegistry.IsLife(key), key);
    }

    public static bool FlagTrue(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        var sm = SaveManager.Instance;
        var (life, name) = Resolve(key);
        if (sm != null) return life ? sm.GetLifetimeFlag(name) : sm.GetFlag(name);
        return _memFlags.TryGetValue(key, out var v) && v == "1";
    }

    public static void SetFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var sm = SaveManager.Instance;
        var (life, name) = Resolve(key);
        if (sm != null) { if (life) sm.SetLifetimeFlag(name); else sm.SetFlag(name); }
        else _memFlags[key] = "1";
    }

    // ───────────────────────── 內部 ─────────────────────────

    static TriggerRegion Find(string nameOrId)
    {
        if (_map?.TriggerLayer?.regions == null) return null;
        foreach (var r in _map.TriggerLayer.regions)          // 名稱優先（編輯器好讀）；同名取第一個
            if (r.name == nameOrId) return r;
        foreach (var r in _map.TriggerLayer.regions)
            if (r.id == nameOrId) return r;
        return null;
    }

    static bool IsDramaType(TriggerRegion r) => r.typeId == "drama";

    // teleport 的 linkedFx：停用時隱藏綁定的場景特效（綠幕）、解鎖時顯示。
    static void ApplyLinkedFx(TriggerRegion r, bool visible)
    {
        string fxId = r.GetString(KeyLinkedFx);
        if (string.IsNullOrEmpty(fxId) || _fxById == null) return;
        if (_fxById.TryGetValue(fxId.Trim(), out var go) && go != null) go.SetActive(visible);
        else Debug.LogWarning($"[TriggerChain] 「{r.name}」linkedFx=「{fxId}」在本地圖找不到場景特效。");
    }
}
