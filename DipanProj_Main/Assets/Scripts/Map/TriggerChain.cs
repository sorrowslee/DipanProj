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
    public const string TypeCameraFocus = "cameraFocus";   // 鏡頭聚焦（鏈動作）：飄鏡頭到自己那格中心＋黑幕，停留後拉回，再接 next
    public const string TypePlayerHint = "playerHint";     // 玩家提示（鏈動作）：玩家頭上左右各擺一張提示圖，到收起時機（移動/攻擊/任意鍵）自動收，再接 next
    public const string TypePlayScreenFx = "playScreenFx"; // 播放螢幕特效（鏈動作）：就地播一次性全螢幕過場特效（依 effectId，如 1=破幻術）、暫停擋操作，播完再接 next（通常＝teleportTo）
    public const string TypeTogglePortal = "togglePortal"; // 開關傳送點（鏈動作）：把 target 指定的傳送點隱藏封鎖(show=false)或顯示解鎖(show=true)，含外型/綠幕，再接 next。Boss 房封門用
    public const string TypeOnEnter = "onEnter";           // 進場觸發（自動）：進圖載入結束後自動觸發，純鏈起點（0 格、不塗格子），見 MapManager.FireEnterTriggersRoutine
    public const string TypeBossIntro = "bossIntro";       // Boss開戰資訊（鏈動作）：暫停＋中央警告特效＋左滑入頭像＋右滑入姓名牌匾，表演完再接 next（見 BossIntroPanel）
    public const string TypeClearLevel = "clearLevel";     // 過關（鏈動作）：被 next 呼叫到就啟動「延時倒數（玩家可動）→ 卍字離場 → 結算 → 返回廣場」流程並記過關（見 GameFlowManager.EndLevel）。旗標偵測已抽到 watchFlag，這裡純鏈動作
    public const string TypeWatchFlag = "watchFlag";       // 觀察旗標變動（自動）：監聽 fireOnFlag 指定的旗標，該旗標「首次成立(false→true)」時觸發自己的 next。本身不做事，只當「旗標驅動的鏈起點」（同 onEnter，改由旗標驅動）。見 AutoFireOnFlag
    public const string TypeUnlockRoll = "unlockRoll";     // 解鎖抽選內容（鏈動作）：把某個物品永久加進某個抽選池（跨輪迴保留）。例：打贏紅嫁衣→血統池+幽靈。pool=池代號(GachaPoolTable 的 PoolId)、entry=物品 id。見 Dipan.Gacha.GachaService
    public const string TypeSelectScript = "selectScript"; // 選擇劇本（鏈動作）：被 next 啟動時開「選擇劇本」面板（邪佛發牌），玩家按領取拿走某張劇本→加進背包→關閉→接 next。取代原本直接 giveItem 給紅嫁衣劇本。scriptIds='|' 分隔可領取劇本道具 id、specialIds='|' 分隔用特殊裂紋框的 id。見 SelectScriptPanel

    // ── 位置型 typeId（玩家踩到／按 F 才生效，被鏈啟動＝「解鎖」）──
    // 這些不進 Activate 的 switch，實際行為由 MapLoader / TeleportWatcher / InteractionManager 建點時處理。
    // 收成一份明表，是為了讓 Activate 的 default 分支能分辨「這是位置型」還是「作者新增了動作型卻忘了加 case」——
    // 後者以前會靜默被當成解鎖處理，查半天查不出來（見下方 default 的警告）。
    public const string TypeTeleport = "teleport";
    public const string TypePickup = "pickup";
    public const string TypeCutscene = "cutscene";
    public const string TypePortal = "portal";           // 傳送門互動（靠近按 F 開 ScriptsPanel）
    public const string TypeOpenPanel = "openPanel";     // 開啟 UI 面板（靠近按 F）：panelId 指定要開哪個面板、arg 傳參數。祭壇抽選＝panelId=gacha、arg=池代號
    public const string TypeCamZone = "camZone";
    public const string TypePlayerSpawn = "playerSpawn";
    public const string TypeMonsterSpawn = "monsterSpawn";

    static readonly HashSet<string> PositionTypes = new HashSet<string>
    {
        TypeTeleport, TypePickup, TypeCutscene, TypePortal, TypeOpenPanel,
        TypeCamZone, TypePlayerSpawn, TypeMonsterSpawn,
    };

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
    const string KeyRequireClearsMin = "requireClearsMin";      // 最低完成關卡數：完成數 ≥ 此值才可觸發（空=不限制）
    const string KeyRequireClearsMax = "requireClearsMax";      // 最高完成關卡數：完成數 ≤ 此值才可觸發（空=不限制；填 0＝只在一關都還沒通時）
    const string KeyRequireClearsScope = "requireClearsScope";  // "lifetime"=看跨輪迴高水位；其餘/空=本周目
    const string KeyOnBlocked = "onBlocked";                    // 條件不成立時：空/「中止整條鏈」=停；「跳過這顆繼續」=改跑自己的 next
    const string KeyFireOnFlag = "fireOnFlag";     // 「旗標一成立就自動觸發本 trigger」：填旗標名。與 onEnter（進場自動）同類、改由旗標驅動。用途：boss 死亡設旗標→clearLevel 自動觸發，不需玩家踩點

    static MapData _map;
    static MapManager _manager;
    static Dictionary<string, GameObject> _fxById;          // sceneFx id → 場上物件（MapLoader 提供）
    static Dictionary<string, GameObject> _teleportMarkerById; // teleport region id → 傳送點內建外型 marker（MapLoader 提供，togglePortal 隱藏/恢復用）
    static readonly HashSet<string> _disabled = new HashSet<string>();       // 目前停用中的 region id
    static readonly Dictionary<string, string> _memFlags = new Dictionary<string, string>(); // 無存檔時的後備旗標
    static readonly HashSet<string> _levelFlags = new HashSet<string>();                      // 關卡單次旗標（進 module 清、不進存檔）
    static readonly Dictionary<string, (int mapId, string entrance)> _teleportOverride = new Dictionary<string, (int, string)>(); // 傳送門：執行期覆寫目的地（劇本決定）

    static TriggerRegion _pendingDramaRegion;   // 等「對話關閉」才算完成的 region（DramaPanel/TalkPanel 關閉時通知）

    /// <summary>換圖後重建：計算每個 trigger 的初始啟用狀態、套用 linkedFx／傳送點外型 顯示/隱藏。由 MapManager 呼叫。</summary>
    public static void Setup(MapData map, MapManager manager, Dictionary<string, GameObject> fxById,
                             Dictionary<string, GameObject> teleportMarkerById = null)
    {
        _map = map;
        _manager = manager;
        _fxById = fxById;
        _teleportMarkerById = teleportMarkerById;
        _disabled.Clear();
        _teleportOverride.Clear();   // 换图 → 清掉上一張圖的傳送門目的地覆寫
        _pendingDramaRegion = null;

        if (map?.TriggerLayer?.regions == null) return;
        foreach (var r in map.TriggerLayer.regions)
        {
            if (!r.GetBool(KeyStartDisabled, false)) continue;
            // startDisabled：預設停用；但 enableFlag 旗標成立 = 之前解鎖過（存檔記住）→ 直接啟用。
            string ef = r.GetString(KeyEnableFlag);
            bool unlocked = !string.IsNullOrEmpty(ef) && FlagTrue(ef);
            if (!unlocked) _disabled.Add(r.id);
            ApplyTeleportVisual(r, visible: unlocked);   // 綠幕＋傳送點外型都跟著初始狀態顯示/隱藏
        }
    }

    /// <summary>
    /// 進入 Play 模式時把所有可變 static 歸零（已關 Domain Reload，否則上一輪殘留會讓「第二次以後 Play」出錯：
    /// static 事件 <see cref="OnTriggerFired"/> 累積訂閱者＝重複觸發／呼叫到已銷毀物件；抑制集合殘留＝解鎖狀態錯亂）。
    /// 由 <c>PlayModeStaticReset</c> 在每次進 Play 最早期呼叫。build 每次全新程序、本來就乾淨，這段等於無害 no-op。
    /// </summary>
    public static void ResetForPlayMode()
    {
        _map = null;
        _manager = null;
        _fxById = null;
        _teleportMarkerById = null;
        _disabled.Clear();
        _memFlags.Clear();
        _levelFlags.Clear();
        _teleportOverride.Clear();
        _pendingDramaRegion = null;
        OnTriggerFired = null;
    }

    // ───────────────────────── 查詢（給各 watcher / InteractionManager 用） ─────────────────────────

    /// <summary>此 trigger 目前是否停用（startDisabled 未解鎖）。停用 = 踩到/按 F 都無反應、不顯示星星。</summary>
    public static bool IsDisabled(TriggerRegion r) => r != null && _disabled.Contains(r.id);

    /// <summary>
    /// 觸發條件是否全部成立（AND）。沒填的條件視為通過。目前支援：
    ///   requireFlag     旗標成立（"!flag" = 否定；旗標名可加 "永久:" 前綴＝終身旗標）
    ///   requireCycleMax 周目 ≤ 值（初始限定填 1）
    ///   requireCycleMin 周目 ≥ 值（老手限定）
    ///   requireClearsMin 最低完成關卡數：完成數 ≥ 值（搭配 requireClearsScope：lifetime=跨輪迴高水位／空=本周目）
    ///   requireClearsMax 最高完成關卡數：完成數 ≤ 值（填 0＝只在「一關都還沒通」時成立，用來擋初次限定的內容）
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

        // 3) 完成關卡數（最低／最高）：與地上物的「出現條件（完成 N 關）」是同一個概念與同一組範圍值
        //    （見 MapLoader 的 appearAfterClears / appearScope），故意做成一樣，
        //    這樣「祭壇的圖」和「祭壇的按 F 感應區」可以填一模一樣的條件，不會一個看不見一個按得到。
        //    範圍：lifetime＝跨輪迴曾達到的最高完成關卡數；其餘/空＝本周目完成關卡數。
        //    無存檔（單場景測試）時視為 0，也就是「有填就擋住」——測試時請用測試選單的「1關後」進場。
        string clearsMinS = r.GetString(KeyRequireClearsMin);
        string clearsMaxS = r.GetString(KeyRequireClearsMax);
        if (!string.IsNullOrEmpty(clearsMinS) || !string.IsNullOrEmpty(clearsMaxS))
        {
            var sm = SaveManager.Instance;
            bool lifetime = r.GetString(KeyRequireClearsScope).Trim() == "lifetime";
            int have = sm == null ? 0 : (lifetime ? sm.LifetimeMaxClears : sm.ClearedModuleCount);
            if (!string.IsNullOrEmpty(clearsMinS) && int.TryParse(clearsMinS, out int cMinClear) && have < cMinClear) return false;
            // 最高值的 0 是有意義的值（「一關都還沒通」），所以不能像最低值那樣用 >0 當「有填」的判斷。
            if (!string.IsNullOrEmpty(clearsMaxS) && int.TryParse(clearsMaxS, out int cMaxClear) && have > cMaxClear) return false;
        }

        // 4) 背包道具（無背包系統時 count 視為 0）。"!itemId"＝須無、"itemId"＝須有。
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

    /// <summary>是否有對話正在播（等面板關閉的鏈節點還沒結）。進場觸發依序點火時據此等待，避免兩段對話撞在一起。</summary>
    public static bool DramaPending => _pendingDramaRegion != null;

    // ── 重複規則（repeat）給「進場觸發」用的判定/標記 ──
    // 與 InteractionManager 的互動點用同一套自動旗標格式（"已觸發:"+id；永久加「永久:」前綴）。
    //   每次進場（預設/空值）、每次：不限制（進場觸發本來就一次進圖只點火一次）。
    //   每周目：觸發後寫周目自動旗標（輪迴清空 → 下周目再觸發）。
    //   永久：觸發後寫終身自動旗標（跨輪迴保存，開新角色才會再觸發）。

    /// <summary>依「重複規則」判定這個 trigger 是否還能觸發（每周目/永久 已觸發過 → false）。</summary>
    public static bool RepeatAllows(TriggerRegion r)
    {
        string s = r?.GetString("repeat");
        if (string.IsNullOrEmpty(s)) return true;
        s = s.Trim();
        if (s == "每周目" || s == "cycle") return !FlagTrue("已觸發:" + r.id);
        if (s == "永久" || s == "life") return !FlagTrue(LifePrefix + "已觸發:" + r.id);
        return true;
    }

    /// <summary>觸發當下標記「已觸發」自動旗標（只有 每周目/永久 兩種模式會寫；其他模式無事）。</summary>
    public static void MarkRepeatSeen(TriggerRegion r)
    {
        string s = r?.GetString("repeat");
        if (string.IsNullOrEmpty(s)) return;
        s = s.Trim();
        if (s == "每周目" || s == "cycle") SetFlag("已觸發:" + r.id);
        else if (s == "永久" || s == "life") SetFlag(LifePrefix + "已觸發:" + r.id);
    }

    // ───────────────────────── 完成 → 接鏈 ─────────────────────────

    /// <summary>
    /// 一個 trigger 的動作完成（撿完、對話關了、給完物品…）：寫 setFlag，然後啟動 next。
    /// 各觸發端（InteractionManager / 本類的動作執行）在動作真正結束時呼叫。
    /// </summary>
    /// <summary>任何 trigger 的動作「完成」時廣播它的名字（新手教學等系統可據此反應，例如「初入場景對話」「邪佛全貌」）。</summary>
    public static event System.Action<string> OnTriggerFired;

    public static void OnCompleted(TriggerRegion r)
    {
        if (r == null) return;
        if (!string.IsNullOrEmpty(r.name)) OnTriggerFired?.Invoke(r.name);
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
        // 延後一幀再接鏈：此刻面板正在 OnClose，若同步接鏈又去開新對話會重入 → 舊面板把新面板關掉、
        // IsOpen 殘留、遊戲永久暫停（玩家卡死）。等這幀面板完全關乾淨，下一幀再繼續鏈。
        if (r != null) TriggerChainRunner.NextFrame(() => OnCompleted(r));
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
            // 預設：條件不成立＝整條鏈就此中止（維持原本行為）。
            // 但「初次限定的對話」這種節點卡在鏈中間時，中止會把後面該做的事（發劇本…）一起吃掉。
            // 填「條件不成立時＝跳過這顆繼續」就改成：不執行自己、也不寫 setFlag，直接把棒子交給自己的 next。
            string onBlocked = r.GetString(KeyOnBlocked).Trim();
            if (onBlocked == "跳過這顆繼續" || onBlocked == "skip")
            {
                string skipNext = r.GetString(KeyNext);
                if (!string.IsNullOrEmpty(skipNext))
                {
                    Debug.Log($"[TriggerChain] 「{r.name}」條件不成立 → 跳過這顆，繼續跑「{skipNext.Trim()}」。");
                    Activate(skipNext.Trim());
                    return;
                }
            }
            Debug.Log($"[TriggerChain] 「{r.name}」觸發條件不成立，鏈在此中止。");
            return;
        }

        switch (r.typeId)
        {
            case TypeGiveItem: ExecuteGiveItem(r); break;
            case TypeTeleportTo: ExecuteTeleportTo(r); break;
            case TypeCameraFocus: ExecuteCameraFocus(r); break;
            case TypePlayerHint: ExecutePlayerHint(r); break;
            case TypePlayScreenFx: ExecutePlayScreenFx(r); break;
            case TypeTogglePortal: ExecuteTogglePortal(r); break;
            case TypeSelectScript: ExecuteSelectScript(r); break;
            case TypeUnlockRoll: ExecuteUnlockRoll(r); break;
            case TypeBossIntro: ExecuteBossIntro(r); break;
            case TypeClearLevel: ExecuteClearLevel(r); break;
            case TypeWatchFlag: OnCompleted(r); break;   // 觀察旗標變動：被 AutoFireOnFlag 觸發＝純轉接（寫 setFlag、接它的 next）
            case TypeOnEnter: OnCompleted(r); break;   // 進場觸發被鏈到＝純轉接：直接完成（寫 setFlag、接它的 next）
            default:
                if (IsDramaType(r)) ExecuteDrama(r);   // 鏈到劇情點 = 立即播對話（對話→對話）
                else
                {
                    // ⚠ 新增動作型 trigger 卻忘了在上面加 case，會靜默掉到這裡被當成「位置型解鎖」，
                    //    症狀是「鏈跑過去了但什麼都沒發生」，很難查。認不得的 typeId 直接吼一聲。
                    if (!PositionTypes.Contains(r.typeId))
                        Debug.LogWarning($"[TriggerChain] typeId「{r.typeId}」（trigger「{r.name}」）不在已知的位置型清單裡，" +
                                         "也沒有對應的動作 case——先當成位置型解鎖處理。" +
                                         "如果它其實是動作型，請到 TriggerChain.Activate 的 switch 補一個 case；" +
                                         "如果它是位置型，請把 typeId 加進 PositionTypes。");
                    EnableRegion(r);                   // 位置型（teleport/pickup/cutscene…）= 解鎖
                }
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

        // 關卡進度：一趟關卡內同一個 giveItem 只給一次（onEnter 會在每次進圖重觸發；不擋住 next）。
        int mapId = MapManager.Instance != null ? MapManager.Instance.CurrentMapId : -1;
        if (RunProgress.Exists && RunProgress.Instance.RunActive
            && RunProgress.Instance.IsTriggerConsumed(mapId, r.id))
        {
            OnCompleted(r);   // 本趟已給過 → 不再給，仍接 next（門/傳送等後續照走）
            return;
        }

        var data = inv.GetData(itemId);
        string display = data != null ? data.Name : $"#{itemId}";
        // 關卡內進臨時包（通關才落袋，恆回 0）、廣場進真背包（可能溢出）。見 RunProgress。
        int leftover = RunProgress.Instance.GiveItem(itemId, count);
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
        // 記本趟已給（非 run 期間由 RunProgress 內部忽略）。
        if (RunProgress.Exists) RunProgress.Instance.MarkTriggerConsumed(mapId, r.id);
        OnCompleted(r);
    }

    // 開啟「選擇劇本」面板（邪佛發牌）：擺出卡片，玩家按領取拿走某張劇本→加進背包→關閉→接 next。
    //   scriptIds  ：可領取的劇本道具 id，用 '|' 分隔（初始＝"104" 紅嫁衣）。
    //   specialIds ：其中要用特殊裂紋框的 id（同樣 '|' 分隔；業障回響/紅嫁衣）。
    // 「加進背包」由面板做（同原本 giveItem 的效果），本動作只負責開面板＋領取後接 next（鏡頭聚焦/傳送門對話…）。
    // scriptIds 為空/無效 → 直接接 next，不卡住鏈。
    static void ExecuteSelectScript(TriggerRegion r)
    {
        var special = ParseIdSet(r.GetString("specialIds"));
        var cards = new List<Dipan.UI.SelectScriptPanel.CardSpec>();
        foreach (int id in ParseIds(r.GetString("scriptIds")))
            cards.Add(new Dipan.UI.SelectScriptPanel.CardSpec(id, special.Contains(id)));

        if (cards.Count == 0)
        {
            Debug.LogWarning($"[TriggerChain] selectScript「{r.name}」scriptIds 為空或無效，直接接 next。");
            OnCompleted(r);
            return;
        }

        // 延一幀開面板（避免與「上一個對話關閉」同幀重入，比照 bossIntro）；領取後（面板已關）再延一幀接 next。
        TriggerChainRunner.NextFrame(() =>
            Dipan.UI.SelectScriptPanel.Open(cards, _ => TriggerChainRunner.NextFrame(() => OnCompleted(r))));
    }

    // 解鎖抽選內容（鏈動作）：把某個物品永久加進某個抽選池，之後在對應祭壇就抽得到。
    //   pool  ：池代號（GachaPoolTable.csv 的 PoolId，例 blood / weapon）
    //   entry ：要解鎖的物品 id，可用 '|' 分隔一次解鎖多個
    // 典型用法：紅嫁衣 boss 死亡旗標 → watchFlag → next 接這顆（pool=blood、entry=302 幽靈血統藥劑）。
    // 解鎖寫進存檔頂層（跨輪迴保留），idempotent——重複觸發不會重複加。無效參數也照樣接 next，不卡鏈。
    static void ExecuteUnlockRoll(TriggerRegion r)
    {
        string pool = r.GetString("pool").Trim();
        var ids = ParseIds(r.GetString("entry"));
        var sm = SaveManager.Instance;

        if (string.IsNullOrEmpty(pool) || ids.Count == 0)
        {
            Debug.LogWarning($"[TriggerChain] unlockRoll「{r.name}」pool 或 entry 沒填（pool=「{pool}」、entry=「{r.GetString("entry")}」），直接接 next。");
        }
        else if (sm == null)
        {
            Debug.LogWarning($"[TriggerChain] unlockRoll「{r.name}」沒有 SaveManager（單場景測試？），解鎖不會保存，直接接 next。");
        }
        else
        {
            foreach (int id in ids)
                if (sm.UnlockRollEntry(pool, id))
                    Debug.Log($"[TriggerChain] unlockRoll：抽選池「{pool}」新增可抽物品 {id}");
        }
        OnCompleted(r);
    }

    // 解析 '|' 分隔的道具 id 清單（空/無效略過）。
    static List<int> ParseIds(string s)
    {
        var list = new List<int>();
        if (string.IsNullOrEmpty(s)) return list;
        foreach (var part in s.Split('|'))
            if (int.TryParse(part.Trim(), out int id) && id > 0) list.Add(id);
        return list;
    }

    static HashSet<int> ParseIdSet(string s)
    {
        var set = new HashSet<int>();
        foreach (int id in ParseIds(s)) set.Add(id);
        return set;
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

    // 過關（鏈動作）：被 next 呼叫到就啟動「延時倒數（玩家可動）→ 卍字離場 → 結算 → 返回廣場」流程。
    // 旗標偵測已抽到 watchFlag，這裡是純鏈動作——由 watchFlag（或任何 trigger）的「接續觸發」接進來。
    //   典型：boss 死亡旗標 → watchFlag → next 接對話/動畫/給獎勵…→ 最後接 clearLevel。
    //   也可「踩點過關」：畫格子讓玩家踩到即過關。
    // 延時觸發(delaySeconds) 秒數內玩家可自由操作（撿戰利品等）、上方顯示倒數；仍保留「接續觸發」（可在開始倒數的瞬間再觸發別的）。
    static void ExecuteClearLevel(TriggerRegion r)
    {
        var gf = Dipan.Flow.GameFlowManager.Instance;
        if (gf == null) { Debug.LogWarning($"[TriggerChain] clearLevel「{r.name}」找不到 GameFlowManager，略過。"); return; }
        if (gf.IsEndingLevel) return;   // 已在結束流程中 → 不重複觸發（雙保險：避免對話被重跑）

        float delay = r.GetFloat("delaySeconds", 2f);   // 延時觸發：空/無效＝2 秒
        gf.EndLevel(Dipan.Flow.GameFlowManager.LevelEndKind.Clear, delay);

        // 寫 setFlag（若有）＋ 接續觸發 next（讓「接續觸發」的 boss 對話等鏈在延時期間跑起來）。只在第一次跑。
        OnCompleted(r);
    }

    // 鏡頭聚焦（鏈動作）：飄鏡頭到自己那格區域中心＋壓黑幕、停留、再拉回，全程定住玩家；表演完才接 next。
    // 聚焦中心＝這個 trigger 畫的格子中心（通常畫在傳送門正中間一格）。
    static void ExecuteCameraFocus(TriggerRegion r)
    {
        if (!RegionCenter(r, out Vector2 center))
        {
            Debug.LogWarning($"[TriggerChain] 鏡頭聚焦「{r.name}」沒畫任何格子，無法決定聚焦中心，直接接 next。");
            OnCompleted(r);
            return;
        }
        var cam = Object.FindObjectOfType<MapCameraController>();
        if (cam == null)
        {
            Debug.LogWarning($"[TriggerChain] 鏡頭聚焦「{r.name}」找不到 MapCameraController，直接接 next。");
            OnCompleted(r);
            return;
        }
        float hold = r.GetFloat("holdSeconds", 1.6f);      // 停留秒數（留空＝1.6）
        string dim = r.GetString("dim");                    // 黑幕樣式："中央留洞"（預設）/"整片全黑"/"無"
        cam.PlayFocus(center, hold,
            onStart: () =>
            {
                UIManager.Instance?.SetExternalHold(true, false);   // 飄鏡頭期間定住玩家（不暫停，鏡頭才會動）
                if (dim == "整片全黑") TutorialDimPanel.ShowFullBlack();
                else if (dim != "無") TutorialDimPanel.ShowSpotlightCenter();   // 空或「中央留洞」→ 中央留洞
            },
            onEnd: () =>
            {
                TutorialDimPanel.Hide();
                UIManager.Instance?.SetExternalHold(false, false);
                OnCompleted(r);   // 聚焦表演結束才接 next（例如接「指引玩家過門」的對話）
            });
    }

    // 播放螢幕特效（鏈動作）：就地播一次性全螢幕過場特效（依 effectId 分派，如 1=破幻術「幻境崩碎回歸現實」；
    // 暫停＋擋操作由各特效控制器自己管），播完才接 next（通常 next = teleportTo，把玩家傳去現實地圖）。
    // 語意例＝玩家親眼看到「當前這張幻境場景」龜裂崩碎、收尾全白，再無縫接跨關載入頁。可填 duration 覆寫特效總長。
    static void ExecutePlayScreenFx(TriggerRegion r)
    {
        int effectId = r.GetInt("effectId", 0);
        float dur = r.GetFloat("duration", -1f);   // 留空 = 用該特效控制器預設總長
        ScreenFxPlayer.Play(effectId, () => OnCompleted(r), dur);   // 未知/為 0 的 id：ScreenFxPlayer 會警告並直接接 next
    }

    // 開關傳送點（鏈動作）：把 target 指定的**一或多個**傳送點隱藏封鎖或顯示解鎖，含外型/綠幕與踩踏功能，立即完成接 next。
    //   target = 要控制的傳送點名稱（可多筆，編輯器按「＋」加欄，存成以逗號分隔的一個字串；傳送點名稱請勿含逗號）；
    //   show   = true 顯示解鎖（＝EnableRegion，會順便寫該傳送點的 enableFlag 存檔記住）、
    //            false 隱藏封鎖（＝DisableRegion，執行期狀態、不寫存檔，換圖/重進房間重算）。留空 show 預設 false（封門是主要用途）。
    // 典型：Boss 房 onEnter→對話→togglePortal(target=門A,門B, show=false) 進門一次封多個門；打贏後 Boss 死亡旗標鏈接 togglePortal(show=true) 一次復原。
    static void ExecuteTogglePortal(TriggerRegion r)
    {
        string targets = r.GetString("target");
        bool show = r.GetBool("show", false);
        if (string.IsNullOrWhiteSpace(targets))
        {
            Debug.LogWarning($"[TriggerChain] togglePortal「{r.name}」沒填 target（要開關的傳送點名），略過（仍接 next）。");
            OnCompleted(r);
            return;
        }
        foreach (var raw in targets.Split(','))   // 多筆以逗號分隔；單筆＝沒逗號＝一個元素，向下相容
        {
            string name = raw.Trim();
            if (name.Length == 0) continue;
            var tp = Find(name);
            if (tp == null) { Debug.LogWarning($"[TriggerChain] togglePortal「{r.name}」找不到傳送點「{name}」，略過這一個。"); continue; }
            if (show) EnableRegion(tp); else DisableRegion(tp);
        }
        OnCompleted(r);   // 動作型：立即完成、接 next
    }

    // Boss 開戰資訊（鏈動作）：暫停遊戲播「Warning 特效＋左滑入 boss 頭像＋右滑入姓名牌匾」開場表演（BossIntroPanel），
    // 表演結束（自動播完或玩家跳過）才接 next。顯示名/頭像資料在 MonsterData.csv 的 DisplayName / PortraitPath 欄，
    // 這裡只填 monsterId（與怪物出生點同一個 ID）；warnVfxId 留空＝面板預設（VfxTable 14 警告）。
    // 延後一幀開面板：此鏈常由對話面板 OnClose 續接，同步開新模態面板會重入卡死（見 PROBLEMS D8）。
    static void ExecuteBossIntro(TriggerRegion r)
    {
        int monsterId = r.GetInt("monsterId", 0);
        if (monsterId <= 0)
        {
            Debug.LogWarning($"[TriggerChain] bossIntro「{r.name}」monsterId 無效（要填 MonsterData.csv 的怪物 ID），直接接 next。");
            OnCompleted(r);
            return;
        }
        int warnVfxId = r.GetInt("warnVfxId", 0);   // 留空/0 = 用面板預設（VfxTable 14）
        TriggerChainRunner.NextFrame(() => Dipan.UI.BossIntroPanel.Show(monsterId, warnVfxId, () => OnCompleted(r)));
    }

    // 玩家提示（鏈動作）：玩家頭上左右各擺一張提示圖，指定張閃爍；到收起時機（移動/攻擊/任意鍵）自動收，收完接 next。
    static void ExecutePlayerHint(TriggerRegion r)
    {
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo == null)
        {
            Debug.LogWarning($"[TriggerChain] 玩家提示「{r.name}」找不到玩家，直接接 next。");
            OnCompleted(r);
            return;
        }
        Sprite left = LoadHintSprite(r.GetString("leftImage"));
        Sprite right = LoadHintSprite(r.GetString("rightImage"));
        if (left == null && right == null)
        {
            Debug.LogWarning($"[TriggerChain] 玩家提示「{r.name}」左右圖都載不到（leftImage/rightImage），直接接 next。");
            OnCompleted(r);
            return;
        }
        bool flashLeft = r.GetBool("flashLeft", false);
        bool flashRight = r.GetBool("flashRight", true);
        var mode = ParseHideMode(r.GetString("hideOn"));
        // 收起（玩家移動/攻擊/按鍵）後才 OnCompleted → 寫 setFlag（可做「只一次」）＋接 next。
        PlayerHintPanel.Show(playerGo.transform, left, flashLeft, right, flashRight, mode, () => OnCompleted(r));
    }

    // 載提示圖：填檔名（放 Resources/UI/Common/ 下，例 Guide_Wasd）或含「/」的完整 Resources 路徑。
    static Sprite LoadHintSprite(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        name = name.Trim();
        var sp = name.Contains("/") ? Resources.Load<Sprite>(name) : Resources.Load<Sprite>("UI/Common/" + name);
        if (sp == null) Debug.LogWarning($"[TriggerChain] 玩家提示圖載不到：「{name}」（放 Resources/UI/Common/ 下、填檔名不含副檔名）。");
        return sp;
    }

    static PlayerHintPanel.HideMode ParseHideMode(string s)
    {
        switch ((s ?? "").Trim())
        {
            case "攻擊": return PlayerHintPanel.HideMode.Attack;
            case "任意鍵": return PlayerHintPanel.HideMode.AnyKey;
            default: return PlayerHintPanel.HideMode.Move;   // 「移動」或留空
        }
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

    // 解鎖位置型 trigger：移出停用集、寫 enableFlag（存檔記住）、顯示 linkedFx＋傳送點外型、要求 MapManager 重建 watcher。
    static void EnableRegion(TriggerRegion r)
    {
        if (!_disabled.Remove(r.id))
        {
            Debug.Log($"[TriggerChain] 「{r.name}」本來就是啟用狀態，無事。");
            return;
        }
        string ef = r.GetString(KeyEnableFlag);
        if (!string.IsNullOrEmpty(ef)) SetFlag(ef);
        ApplyTeleportVisual(r, visible: true);
        if (_manager != null) _manager.RefreshTriggers();
        Debug.Log($"[TriggerChain] 解鎖 trigger「{r.name}」({r.typeId})。");
    }

    // 隱藏/封鎖位置型 trigger（EnableRegion 的反向；togglePortal show=false 用）：加進停用集、隱藏 linkedFx＋傳送點外型、
    // 要求 MapManager 重建 watcher。**執行期狀態、刻意不寫 enableFlag**——換圖/重進房間會依 startDisabled+enableFlag 重算
    // （所以 Boss 房每次進場靠 onEnter 重新封即可；要「跨存讀檔記住封著」的情境再另設計）。
    static void DisableRegion(TriggerRegion r)
    {
        if (!_disabled.Add(r.id))
        {
            Debug.Log($"[TriggerChain] 「{r.name}」本來就停用中，無事。");
            return;
        }
        ApplyTeleportVisual(r, visible: false);
        if (_manager != null) _manager.RefreshTriggers();
        Debug.Log($"[TriggerChain] 隱藏/封鎖 trigger「{r.name}」({r.typeId})。");
    }

    // ───────────────────────── 旗標（存檔 progress.flags；無存檔時退回記憶體） ─────────────────────────

    // ───────────────────────── 傳送門（放劇本開門）─────────────────────────

    /// <summary>
    /// 傳送門 UI 按下「開啟」時呼叫：把名為 teleportName 的傳送點目的地設成劇本指定的關卡，然後解鎖它（亮綠幕）。
    /// 目的地由劇本決定（hub）→ 存執行期覆寫，TeleportWatcher 踩到時優先讀它。回傳是否成功（找得到傳送點）。
    /// </summary>
    public static bool OpenPortal(string teleportName, int targetMapId, string targetEntrance)
    {
        if (string.IsNullOrEmpty(teleportName))
        {
            Debug.LogWarning("[TriggerChain] OpenPortal：portal 互動點沒填「要開的傳送點名」(linkTeleport)，無法開門。");
            return false;
        }
        var r = Find(teleportName.Trim());
        if (r == null)
        {
            Debug.LogWarning($"[TriggerChain] OpenPortal：本地圖找不到傳送點「{teleportName}」。");
            return false;
        }
        if (targetMapId > 0) _teleportOverride[r.id] = (targetMapId, targetEntrance ?? "");
        EnableRegion(r);   // 解鎖 + 寫 enableFlag + 亮 linkedFx 綠幕 + RefreshTriggers
        return true;
    }

    /// <summary>取某個 trigger 區域的世界中心（各格中心平均）。給新手教學手指指向某個觸發點（邪佛/傳送門）用。</summary>
    public static bool TryGetRegionCenter(string name, out Vector2 center) => RegionCenter(Find(name), out center);

    /// <summary>某 trigger 區域的世界中心（各格中心平均）。沒畫格子回 false。</summary>
    static bool RegionCenter(TriggerRegion r, out Vector2 center)
    {
        center = default;
        if (r?.cells == null || r.cells.Count == 0) return false;
        Vector2 sum = Vector2.zero; int n = 0;
        foreach (var c in r.cells)
            if (c != null && c.Length >= 2) { sum += MapCoords.CellCenter(c[0], c[1], _map); n++; }
        if (n == 0) return false;
        center = sum / n; return true;
    }

    /// <summary>TeleportWatcher 用：這個傳送點有沒有被傳送門設定過執行期目的地覆寫。</summary>
    public static bool TryGetTeleportOverride(string regionId, out int mapId, out string entrance)
    {
        if (!string.IsNullOrEmpty(regionId) && _teleportOverride.TryGetValue(regionId, out var v))
        {
            mapId = v.mapId; entrance = v.entrance; return true;
        }
        mapId = -1; entrance = ""; return false;
    }

    // 旗標的生命範圍怎麼決定（方案乙），三選一：
    //   1) 名字帶「永久:」前綴 → 終身（給重複規則的自動旗標、與舊資料相容用）。
    //   2) 否則查旗標登記表 flags.json（FlagRegistry）：登記為 life → 終身；level → 關卡單次；否則周目。
    // 終身存 CharacterSave.lifetimeFlags（跨輪迴）；周目存 progress.flags（輪迴清）；
    // **關卡單次存記憶體 _levelFlags（進新 module 時清、不進存檔）**——做「這趟關卡有沒有殺家人（killedFamily）」這種每次進關重算的判定。
    // 無 SaveManager（單場景測試）時，周目退回記憶體 _memFlags；關卡單次一律走 _levelFlags（行為一致）。
    public const string LifePrefix = "永久:";

    enum FlagScope { Cycle, Life, Level }

    // 回傳（範圍, 去掉前綴的存檔用 key）。
    static (FlagScope scope, string name) Resolve(string key)
    {
        if (key.StartsWith(LifePrefix)) return (FlagScope.Life, key.Substring(LifePrefix.Length));
        if (FlagRegistry.IsLevel(key)) return (FlagScope.Level, key);
        return (FlagRegistry.IsLife(key) ? FlagScope.Life : FlagScope.Cycle, key);
    }

    public static bool FlagTrue(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        var (scope, name) = Resolve(key);
        if (scope == FlagScope.Level) return _levelFlags.Contains(name);   // 關卡單次：只查記憶體
        var sm = SaveManager.Instance;
        if (sm != null) return scope == FlagScope.Life ? sm.GetLifetimeFlag(name) : sm.GetFlag(name);
        return _memFlags.TryGetValue(key, out var v) && v == "1";
    }

    public static void SetFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var (scope, name) = Resolve(key);
        bool wasSet;   // 設定前是否已成立（用來判斷「首次成立」）
        if (scope == FlagScope.Level)
        {
            wasSet = _levelFlags.Contains(name);
            _levelFlags.Add(name);   // 關卡單次：只寫記憶體
        }
        else
        {
            var sm = SaveManager.Instance;
            if (sm != null)
            {
                wasSet = scope == FlagScope.Life ? sm.GetLifetimeFlag(name) : sm.GetFlag(name);
                if (scope == FlagScope.Life) sm.SetLifetimeFlag(name); else sm.SetFlag(name);
            }
            else
            {
                wasSet = _memFlags.TryGetValue(key, out var v) && v == "1";
                _memFlags[key] = "1";
            }
        }
        // 只在「首次成立」時自動觸發：重複設同一旗標不再重跑 fireOnFlag
        // （否則 clearLevel 等被重複觸發 → 接續的對話會一直彈出）。
        if (!wasSet)
        {
            AutoFireOnFlag(key);
            OnFlagFirstSet?.Invoke(key);   // 通知外部（如 MapObjectRevealer：讓 appearFlag 對上的地上物現身）
        }
    }

    /// <summary>
    /// 某旗標「首次成立」時觸發（只在真正從未成立→成立的那一刻，重複 SetFlag 不會再發）。
    /// 由 <see cref="MapObjectRevealer"/> 訂閱，讓 appearFlag 對上此旗標的地上物中途現身。
    /// 傳入字串＝原始旗標 key（與 fireOnFlag / 地上物 appearFlag 存的裸名字一致）。
    /// </summary>
    public static event System.Action<string> OnFlagFirstSet;

    // 旗標成立就自動觸發：掃目前地圖裡 fireOnFlag 對上此旗標、且未停用的 trigger，延一幀 Activate（避免在 SetFlag 當下重入）。
    // 與 onEnter（進場自動）同一類「自動鏈起點」，只是改由旗標驅動。典型：boss 死亡 deathFlag → clearLevel(fireOnFlag=該旗標) 自動過關。
    static void AutoFireOnFlag(string flagKey)
    {
        if (_map?.TriggerLayer?.regions == null || string.IsNullOrEmpty(flagKey)) return;
        string want = flagKey.Trim();
        foreach (var r in _map.TriggerLayer.regions)
        {
            if (r == null) continue;
            string fof = r.GetString(KeyFireOnFlag);
            if (string.IsNullOrEmpty(fof) || fof.Trim() != want) continue;
            if (IsDisabled(r)) continue;
            string key = !string.IsNullOrEmpty(r.name) ? r.name : r.id;
            TriggerChainRunner.NextFrame(() => Activate(key));
        }
    }

    /// <summary>清掉所有「關卡單次」旗標。由 MapManager 在進入新 module（換關卡）時呼叫——所以每次進關這類旗標重算。</summary>
    public static void ClearLevelFlags() => _levelFlags.Clear();

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

    // 傳送點「整體視覺」顯示/隱藏：linkedFx 綠幕 ＋ 傳送點內建外型 marker（showMarker）兩條都套，
    // 確保 togglePortal/解鎖/初始停用時「看起來真的消失/出現」，不是只擋踩踏、圖還留著。
    static void ApplyTeleportVisual(TriggerRegion r, bool visible)
    {
        ApplyLinkedFx(r, visible);
        if (_teleportMarkerById != null && !string.IsNullOrEmpty(r.id)
            && _teleportMarkerById.TryGetValue(r.id, out var marker) && marker != null)
            marker.SetActive(visible);
    }
}
