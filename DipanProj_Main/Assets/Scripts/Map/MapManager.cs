using System.Collections;
using UnityEngine;
using Dipan.MapRuntime;
using Dipan.UI;
using Dipan.Save;

/// <summary>
/// 地圖系統的大腦（場景持久單例）：負責「進入關卡」與「換地圖」，跨換圖不被拆掉。
///
/// 流程：
/// - StartLevel(module)：查 MapsTable 該 module 的首張地圖 → 載入 → 生一次玩家於 playerSpawn。
/// - GoToMap(targetMapId, targetEntrance)：給傳送點呼叫 → 清場 → 載新圖 → 把既有玩家移到落點。
///
/// 設計：玩家「保留同一物件、只移動」（HP/武器/狀態跨圖延續）；換圖前清掉上一張的怪/彈/特效。
/// Phase 2（地圖狀態持久化：怪清掉不復生、道具/事件/掉落物保留）之後掛在本元件上。
/// 見 readme/MAP_SYSTEM.md。
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [Header("參照（留空會自動 FindObjectOfType）")]
    public MapLoader mapLoader;
    public MapTable mapTable;
    public MainSpawner playerSpawner;
    [Tooltip("穿隧道過場表演（場景上掛一個 TunnelWalkController，拖進來；給 cutscene 觸發點用）")]
    public Dipan.Cutscene.TunnelWalkController tunnelWalk;
    [Tooltip("過場影片播放器（場景上掛一個 VideoPlayerOverlay，拖進來；cutscene 的 video 欄有值才會用）")]
    public Dipan.Cutscene.VideoPlayerOverlay videoOverlay;

    [Header("起始關卡")]
    public string startModule = "RedBridalGown";
    public bool autoStartLevel = true;

    [Header("載入頁")]
    [Tooltip("載入頁出現後、開始實際載入前的等待秒數（讓載入圖先停留一下，不會一閃而過）。")]
    public float loadingScreenHoldSeconds = 2f;

    GameObject _player;
    TeleportWatcher _watcher;
    CutsceneWatcher _cutscene;
    CameraZoneWatcher _camZone;
    MapCameraController _camera;
    int _currentMapId = -1;
    bool _loading;   // 載入進行中：擋掉重入（例如傳送 watcher 在載入期間又觸發）
    string _loadedModule;   // 已進入/預載的大地圖 module；跨 module 才出讀取頁＋預載，同 module 房間互跳不讀取
    bool _wakeUpWanted;   // 本次進圖要演「趴地→起身」（EnterEffect=1 睜眼醒來連動）；FireEnterTriggersRoutine 消化

    public int CurrentMapId => _currentMapId;

    /// <summary>目前這張地圖是否禁止玩家使用武器（MapsTable 的 NoWeapon 欄）。
    /// 由 <see cref="PlaceAndSetup"/> 在每次換圖時依該列更新；PlayerController 的發射 guard 讀它。</summary>
    public bool WeaponDisabled { get; private set; }
    public bool IsLoading => _loading;
    public string CurrentModule => _loadedModule;   // 目前所在大地圖 module（結算/記過關用）

    /// <summary>
    /// 抑制開場自動進關卡。由 GameFlowManager 在開機時設 true——改由標題→存讀檔 UI 決定要進哪。
    /// GameFlow 不存在時維持 false = 舊行為（依 autoStartLevel 自動進 startModule，方便單場景測試）。
    /// </summary>
    public static bool SuppressAutoStart = false;

    /// <summary>
    /// （測試用）覆寫「開機自動進哪個 module」——**不動場景序列化的 startModule**，所以正式開場鏈仍進 Main。
    /// 由 Editor-only 的 DevQuickStart 選單設定（Project Tools/測試/直接進關卡）。空/null＝不覆寫、走 startModule。
    /// </summary>
    public static string DevStartModuleOverride;

    /// <summary>
    /// （測試用）&gt;0 = 開機直接進「這張指定地圖 id」（例：邪佛廣場 12，是模組首圖以外的圖）。
    /// 優先於 <see cref="DevStartModuleOverride"/>。由 Editor-only 的 DevQuickStart 選單設定。0 = 不用。
    /// </summary>
    public static int DevStartMapId;

    /// <summary>
    /// （流程用）&gt;0 = 這個場景載入後，Start() 直接起關到指定 MapId（一次性，用完歸零）。
    /// 由 GameFlowManager 新遊戲 / 劇情 end='fall' 墜落尾段設定，優先於所有其他起關邏輯。
    /// </summary>
    public static int BootStartMapId;

    /// <summary>
    /// （測試用）&gt;=0 = 覆寫載入頁「停留秒數」（開發時設 0，省掉每次進關卡那段刻意的等待）。&lt;0 = 不覆寫、用 Inspector 的 loadingScreenHoldSeconds。
    /// 由 Editor-only 的 DevQuickStart 在編輯器 Play 設 0；build 沒那支腳本，維持正式的停留秒數（讓玩家看載入圖）。
    /// </summary>
    public static float DevLoadingHoldSecondsOverride = -1f;

    void Awake()
    {
        Instance = this;
        if (mapLoader == null) mapLoader = FindObjectOfType<MapLoader>();
        if (mapTable == null) mapTable = FindObjectOfType<MapTable>();
        if (playerSpawner == null) playerSpawner = FindObjectOfType<MainSpawner>();

        // 由本元件接管生玩家，避免 MainSpawner 自己又在 Start 生一個。
        if (playerSpawner != null) playerSpawner.SpawnOnStart = false;

        // 相機由本元件依 MapMode 接管（整張地圖 / 鏡頭跟隨），關掉 MapLoader 的自動 FitCamera。
        if (mapLoader != null) mapLoader.fitCameraToMap = false;
    }

    void Start()
    {
        if (BootStartMapId > 0) { int m = BootStartMapId; BootStartMapId = 0; GoToMap(m, null); return; }   // 流程指定起關（新遊戲山道 13 / 墜落後洞窟 11），優先於一切
        if (!autoStartLevel || SuppressAutoStart) return;
        if (DevStartMapId > 0) GoToMap(DevStartMapId, null);   // 測試：直接進指定地圖（如邪佛廣場 12；落點由 PlaceAndSetup 依 Hub 旗標決定）
        else StartLevel(string.IsNullOrEmpty(DevStartModuleOverride) ? startModule : DevStartModuleOverride);
    }

    // ================= 對外 API =================

    /// <summary>進入一個關卡：載入該 module 的首張地圖，玩家生在 playerSpawn。</summary>
    public void StartLevel(string module)
    {
        if (mapTable == null) { Debug.LogError("[MapManager] 沒有 MapTable。"); return; }
        var row = mapTable.FindLevelStart(module);
        if (row == null) return;
        startModule = module;
        LoadMapInternal(row, entrance: null);
    }

    /// <summary>換到指定地圖，玩家落在 targetEntrance（留空 = 目標圖 playerSpawn）。給 TeleportWatcher 呼叫。</summary>
    public void GoToMap(int targetMapId, string targetEntrance)
    {
        if (mapTable == null) { Debug.LogError("[MapManager] 沒有 MapTable。"); return; }
        var row = mapTable.Get(targetMapId);
        if (row == null) { Debug.LogError($"[MapManager] MapsTable 找不到地圖 ID {targetMapId}。"); return; }
        LoadMapInternal(row, targetEntrance);
    }

    // ================= 內部流程 =================

    void LoadMapInternal(MapTableRow row, string entrance)
    {
        if (mapLoader == null) { Debug.LogError("[MapManager] 沒有 MapLoader。"); return; }
        if (_loading)
        {
            Debug.LogWarning("[MapManager] 載入進行中，忽略這次換圖請求（避免重入）。");
            return;
        }
        StartCoroutine(LoadMapRoutine(row, entrance));
    }

    /// <summary>
    /// 換圖。分兩種情況：
    /// - **跨 module（進入新大地圖）**：出讀取頁 → 停留 → 預載整個 module 的資源 → 分幀建圖 → 放玩家…。
    /// - **同 module（房間互跳）**：資源已在進 module 時預載完，直接快速建圖、**不出讀取頁**、即時切換。
    /// 載入期間 `_loading` 擋重入；跨 module 期間玩家輸入被載入頁鎖住。
    /// </summary>
    IEnumerator LoadMapRoutine(MapTableRow row, string entrance)
    {
        _loading = true;
        bool moduleEntry = row.module != _loadedModule;
        mapLoader.fitCameraToMap = false;   // 相機由本元件依 MapMode 接管

        if (moduleEntry)
        {
            // ── 進入新大地圖：讀取頁 + 預載整個 module 資源 + 分幀建圖 ──
            LoadingPanel lp = (UIManager.Instance != null) ? UIManager.Instance.Open<LoadingPanel>() : null;
            if (lp != null) { lp.SetModule(row.module); lp.SetProgress(0f); }
            yield return null;
            float hold = DevLoadingHoldSecondsOverride >= 0f ? DevLoadingHoldSecondsOverride : loadingScreenHoldSeconds;
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

            ClearTransientGameplay();

            // 進入新關卡（module）：清掉「關卡單次」旗標——所以像 killedFamily 這種「這趟有沒有殺家人」每次進關重算。
            // （同 module 房間互跳走 else 分支、不清，旗標在整趟關卡內延續。）
            TriggerChain.ClearLevelFlags();

            // 一趟關卡的臨時包/進度：進「非廣場」module = 開新的一趟（重置臨時包與已清怪/已取物/掉落物）；
            // 進廣場/教學則保險清掉殘留。同 module 房間互跳走 else 分支、不呼叫，本趟進度延續。見 RunProgress。
            RunProgress.Instance.OnEnterModule(row.module);

            // 預載本 module（+Main 共用）的所有素材貼圖 → 進去後房間互跳不必再讀取。進度 0~0.6。
            yield return StartCoroutine(mapLoader.PreloadModuleRoutine(
                row.module, p => { if (lp != null) lp.SetProgress(p * 0.6f); }));

            // 分幀建目標圖。進度 0.6~0.95。
            yield return StartCoroutine(mapLoader.LoadMapRoutine(
                row.path, p => { if (lp != null) lp.SetProgress(0.6f + p * 0.35f); }));

            if (!mapLoader.LastLoadOk)
            {
                Debug.LogError($"[MapManager] 載入地圖失敗：{row.path}");
                if (lp != null && UIManager.Instance != null) UIManager.Instance.Close<LoadingPanel>();
                _loading = false;
                yield break;
            }

            PlaceAndSetup(row, entrance);
            _loadedModule = row.module;
            if (lp != null) lp.SetProgress(1f);

            yield return null;
            if (lp != null && UIManager.Instance != null) UIManager.Instance.Close<LoadingPanel>();
        }
        else
        {
            // ── 同 module 房間互跳：資源已預載，直接同步建圖（快），不出讀取頁 ──
            ClearTransientGameplay();
            if (!mapLoader.LoadMap(row.path))
            {
                Debug.LogError($"[MapManager] 載入地圖失敗：{row.path}");
                _loading = false;
                yield break;
            }
            PlaceAndSetup(row, entrance);
            yield return null;
        }

        _loading = false;

        // 進場觸發（onEnter）：載入完全結束（載入頁已關、玩家已就位）後，自動點火本圖的「進場觸發」點。
        StartCoroutine(FireEnterTriggersRoutine());
    }

    /// <summary>
    /// 依序點火本圖所有「進場觸發(自動)」（typeId=onEnter）的觸發點：
    /// 1) 先等進場一次性效果（睜眼醒來）播完，對話才不會蓋在效果上。
    /// 2) 每顆各自檢查：啟用狀態＋條件（旗標/周目/道具）＋重複規則（每周目/永久 已觸發過就跳過）。
    /// 3) 有填「延遲秒數」就再等（用未縮放時間：暫停中也照走）。
    /// 4) 點火＝OnCompleted：寫完成旗標、啟動接續觸發（next）——它自己不做事，純鏈起點。
    /// 5) 多顆依區域清單順序點火；前一顆若開了對話，等對話關閉才點下一顆（避免兩段對話相撞）。
    /// 期間換圖就中止（新圖會有自己的一輪）。見 readme/TRIGGER_CHAIN.md。
    /// </summary>
    IEnumerator FireEnterTriggersRoutine()
    {
        var regions = mapLoader != null ? mapLoader.Map?.TriggerLayer?.regions : null;
        if (regions == null) yield break;
        int mapAtStart = _currentMapId;

        // 睜眼醒來連動（1/2）：先趴地定格（此時玩家的 Start 已跑完、dead 幀已載入；睜眼開頭全黑蓋住切換瞬間）。
        PlayerAnimator wakeAnim = null;
        if (_wakeUpWanted)
        {
            _wakeUpWanted = false;
            var animComp = _player != null ? _player.GetComponent<PlayerAnimator>() : null;
            if (animComp != null && animComp.HoldLyingPose()) wakeAnim = animComp;
        }

        // 等進場效果（睜眼）播完（用未縮放時間輪詢；效果本身會暫停遊戲）。
        while (ScreenFxPlayer.IsAnyPlaying) yield return null;   // 等任何進場全螢幕過場（睜眼/馬賽克…）播完

        // 睜眼醒來連動（2/2）：倒播 dead＝爬起（定住玩家輸入、不暫停），起身完才點火進場觸發。
        if (wakeAnim != null)
        {
            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(true, false);
            bool wakeDone = false;
            wakeAnim.PlayWakeUp(() => wakeDone = true);
            while (!wakeDone && wakeAnim != null && wakeAnim.IsWakeUpBusy) yield return null;
            if (UIManager.Instance != null) UIManager.Instance.SetExternalHold(false, false);
            if (_currentMapId != mapAtStart || _loading) yield break;
        }

        // 等「進場自動劇情」演完再點火進場觸發：避免劇情的對話與進場觸發對話互相蓋掉。
        // （劇情由 MaybeAutoStart 在 PlaceAndSetup 內同步開演，此時 IsPlaying 已為 true。）
        while (Dipan.Cutscene.CutsceneDirector.IsPlaying)
        {
            if (_currentMapId != mapAtStart || _loading) yield break;   // 劇情交棒換圖 → 中止（新圖自有一輪）
            yield return null;
        }

        foreach (var r in regions)
        {
            if (r == null || r.typeId != TriggerChain.TypeOnEnter) continue;
            if (_currentMapId != mapAtStart || _loading) yield break;   // 期間換圖 → 中止

            // 前一顆點火的鏈若開了對話，等它關閉再點下一顆（避免對話面板互撞）。
            while (TriggerChain.DramaPending) yield return null;
            if (_currentMapId != mapAtStart || _loading) yield break;

            if (!TriggerChain.IsActive(r)) continue;        // 停用中/條件不成立
            if (!TriggerChain.RepeatAllows(r)) continue;    // 每周目/永久 已觸發過

            float delay = r.GetFloat("delaySeconds", 0f);
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            if (_currentMapId != mapAtStart || _loading) yield break;
            if (!TriggerChain.IsActive(r)) continue;        // 延遲期間條件可能已改變，再查一次

            TriggerChain.MarkRepeatSeen(r);
            Debug.Log($"[MapManager] 進場觸發「{r.name}」點火。");
            TriggerChain.OnCompleted(r);
        }
    }

    /// <summary>建好地圖後的共同收尾：落點/玩家/相機/氛圍/場景特效/怪/觸發點（兩種載入路徑共用）。</summary>
    void PlaceAndSetup(MapTableRow row, string entrance)
    {
        _currentMapId = row.id;

        // 邪佛廣場出生點：第一次（開場鏈抵達）用洞穴出口；之後（繼續/回廳/輪迴）一律用中央，省得每次跑遠。
        // 由存檔旗標 hubIntroSpawnDone 決定，覆寫傳入的 entrance。
        bool isHub = row.id == SaveConstants.HubMapId;
        if (isHub && SaveManager.Instance != null)
            entrance = SaveManager.Instance.HubIntroSpawnDone
                ? SaveConstants.HubEntranceCenter
                : SaveConstants.HubEntranceCaveExit;

        Vector2 pos = ResolveSpawnPos(entrance);
        PlacePlayer(pos);
        RepositionPlayerAllies(pos);   // 玩家召喚物跟著過傳送點：移到玩家新落點附近
        SetupCamera(row.mode);
        MapNavGrid.EnsureBuilt(mapLoader.Map);   // 建怪物 A* 尋徑格（含牆＋地上物）
        // 依 MapsTable 的 NoWeapon 欄決定這張圖能不能用武器（劇情地圖／大廳禁用）。
        // 只記狀態，實際攔截在 PlayerController.HandleFiring 的 guard——那裡一處就擋掉所有發射路徑。
        // 換圖時 ClearPersistentWeaponsForMapChange 已清掉光束/佛光；集氣狀態跨圖保留，進禁用圖第一幀由 guard 取消。
        WeaponDisabled = row.noWeapon;

        // 依 MapsTable 的 Atmosphere 欄套用氛圍後處理（換圖即時切換，室外→古墓自動變氛圍）。見 AtmosphereController。
        // EnvBright（環境亮度）只在 Atmosphere=1 生效：壓暗整張圖、讓場上的火把/燈籠把周圍照回來。
        AtmosphereController.ApplyMapAtmosphere(row.atmosphere, row.envBright);
        // 場景特效（世界端，如火雨）：依 SceneEffect 欄，換圖即時切換、自動清殘留。見 SceneEffectController。
        SceneEffectController.ApplyMapSceneEffect(row.sceneEffect, mapLoader.Map);
        // 進場一次性全螢幕過場：依 EnterEffect 欄（＝ScreenFxTable 的 id，0=無/1=睜眼/2=破幻術/3=馬賽克…），
        // 統一經 ScreenFxPlayer 分派，與劇情 screenFx 共用同一份 id → 同 id 同效果。見 ScreenFxPlayer / ScreenFxTable。
        ScreenFxPlayer.Play(row.enterEffect, null);

        // 睜眼醒來（EnterEffect=1）連動玩家「趴地 → 起身」表演：記下需求，實際趴地在 FireEnterTriggersRoutine
        // 開頭才做——因為玩家第一次生成時 PlayerAnimator.Setup 在 Start() 才載幀，這裡（同幀更早）還拿不到 dead 圖；
        // 協程開跑時 Start 已執行完，且睜眼開頭是全黑（眼皮閉合），看不到趴下前的站姿。
        _wakeUpWanted = ScreenFxTable.WakeUpPose(row.enterEffect);   // 由表決定此進場特效要不要連動玩家趴地→起身（目前只有睜眼）
        mapLoader.SpawnMonsters();
        SetupWatcher();

        // 血球 HUD 顯示規則：開場山道劇情場景(初始森林13/14)不顯示，初始洞窟(11)起才顯示。
        // 玩家跨圖不重生（PlayerController.Start 只在初次生成時開 HUD、且已擋掉劇情場景），
        // 所以進到洞窟(或之後任何非劇情場景)時由這裡負責把 HUD 開起來。
        if (UIManager.Instance != null)
        {
            if (SaveConstants.IsIntroCutsceneMap(row.id))
            { if (UIManager.Instance.IsOpen<BottomHudPanel>()) UIManager.Instance.Close<BottomHudPanel>(); }
            else
            { if (!UIManager.Instance.IsOpen<BottomHudPanel>()) UIManager.Instance.Open<BottomHudPanel>(); }
        }

        // 劇情演出：此圖有 cutscene 且設 autoStart 就開演（半演出半漫畫的開場等）。見 CutsceneDirector。
        Dipan.Cutscene.CutsceneDirector.MaybeAutoStart(mapLoader.Map, _player);

        // 關卡進度：把這張地圖「還沒撿走的掉落物」在原座標重放（換圖回來紅水還在原地）。見 RunProgress / InteractionManager。
        if (InteractionManager.Exists) InteractionManager.Instance.RestoreGroundDrops(_currentMapId);

        Debug.Log($"[MapManager] 進入地圖 #{row.id}「{row.name}」(module={row.module})，落點={pos}。");

        // 進 Main module 的圖（開場山道 13/14、初始洞窟 11、邪佛廣場 12）= 存檔檢查點：
        // 記下「玩家現在在哪」，讓「繼續遊戲」能接回開場流程，而不是永遠被丟到廣場。
        // **關卡（其他 module）刻意不記**——關卡是 extraction 模型（臨時包純記憶體、離開歸零），
        // 記了會讓重開遊戲回到一個東西都不見的關卡裡。所以這兩欄永遠是最後一次待在 Main 的位置。
        if (SaveManager.Instance != null && row.module == SaveConstants.HubModule)
        {
            SaveManager.Instance.RecordLastLocation(row.id, entrance);

            // 廣場另外標記「已抵達過」（決定下次的落點是洞穴出口還是中央）。
            if (isHub) SaveManager.Instance.HubIntroSpawnDone = true;

            // 這幾張圖一輪只會經過一次，直接落地寫檔比較保險（否則靠 dirty 自動存/離開遊戲才寫，
            // 玩家在山道劇情中途直接關掉視窗就可能沒存到，回來又被丟回廣場）。
            SaveManager.Instance.SaveNow();
        }
    }

    /// <summary>落點解析：具名落點 → playerSpawn → 地圖中心。三條路都會再過一次「挪開障礙物」防呆。</summary>
    Vector2 ResolveSpawnPos(string entrance)
    {
        if (!string.IsNullOrEmpty(entrance) && mapLoader.TryGetEntrance(entrance, out var p)) return FreeSpotNear(p);
        if (mapLoader.TryGetPlayerSpawn(out var sp)) return FreeSpotNear(sp);
        Debug.LogWarning($"[MapManager] 地圖「{mapLoader.Map?.name}」找不到傳送落點也沒有玩家出生點，" +
                         "只能退回地圖中心——**地圖中心很可能正好在家具或牆裡**。請在地圖編輯器補一個「玩家出生點」。");
        return FreeSpotNear(MapCoords.WorldCenter(mapLoader.Map));
    }

    // ---- 落點防呆：把玩家挪到最近的可站位置 ----
    // 實際踩過：紅嫁衣書房沒有放「玩家出生點」，落點退回地圖中心 (9,-5)，而那裡正好在中央書架的碰撞裡，
    // 玩家一進場就卡在書架中間動不了。原本這裡只印一則 Warning 就把玩家丟進去，
    // 而且訊息沒提「中心可能在牆裡」，所以症狀看起來像別的東西（例如誤以為是碰撞解析度改壞了）。
    const float SpawnProbeRadius = 0.4f;      // ≈ 玩家碰撞圓的世界半徑（0.5 × PlayerScale 0.8）
    const float SpawnSearchStep = 0.25f;      // 往外找的每一圈間距（格）
    const float SpawnSearchMaxRadius = 4f;    // 最多找幾格；再遠就不是「挪一下」而是地圖真的有問題
    const int SpawnSearchDirs = 16;           // 每圈試幾個方向

    /// <summary>
    /// 若落點被 Environment/Water 擋住，往外螺旋找最近的空位；沒被擋就原樣回傳。
    /// 找不到空位時印 Error 並回原點（寧可卡住也不要把玩家丟到地圖外面）。
    /// </summary>
    Vector2 FreeSpotNear(Vector2 pos)
    {
        int mask = LayerMask.GetMask(mapLoader.environmentLayerName, mapLoader.blockerLayerName);
        if (mask == 0) return pos;   // layer 名稱設錯就不做防呆，維持原行為

        // ⚠ 專案全域 queriesStartInColliders = false（見 readme/PROBLEMS.md B7），
        //    那會讓 overlap 查詢**略過「重疊在查詢起點」的 collider**——而這裡要問的正好就是
        //    「這個點是不是在東西裡面」，不打開就會永遠回答「沒被擋」。用完立刻還原。
        //    另外 autoSyncTransforms 也是 false，碰撞剛建好要先手動同步一次才查得到。
        bool prevQuery = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = true;
        try
        {
            Physics2D.SyncTransforms();
            if (Physics2D.OverlapCircle(pos, SpawnProbeRadius, mask) == null) return pos;

            // 候選點一律限制在地圖範圍內。這是第二道防線：萬一物理世界裡混進了不該有的東西
            // （例如換圖同一幀舊地圖的碰撞還沒被銷毀），往外找有可能一路找到地圖外面去，
            // 那比原本卡住還糟——玩家會出現在牆外、什麼都碰不到。寧可找不到也不要出界。
            Rect bounds = MapCoords.WorldBounds(mapLoader.Map);

            for (float d = SpawnSearchStep; d <= SpawnSearchMaxRadius; d += SpawnSearchStep)
            {
                for (int i = 0; i < SpawnSearchDirs; i++)
                {
                    float a = i * Mathf.PI * 2f / SpawnSearchDirs;
                    Vector2 c = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (!InsideMap(c, bounds)) continue;
                    if (Physics2D.OverlapCircle(c, SpawnProbeRadius, mask) != null) continue;

                    Debug.LogWarning($"[MapManager] 落點 {pos} 被地上物/牆擋住，已挪到 {c}（相距 {d:0.00} 格）。" +
                                     "多半是這張地圖沒放「玩家出生點」而退回了地圖中心——建議去地圖編輯器補一個。");
                    return c;
                }
            }

            Debug.LogError($"[MapManager] 落點 {pos} 被擋住，且 {SpawnSearchMaxRadius} 格內找不到空位，玩家會卡住。" +
                           "請在地圖編輯器補「玩家出生點」，或檢查該處的地上物碰撞（效能面板 P → C 可以看碰撞範圍）。");
            return pos;
        }
        finally { Physics2D.queriesStartInColliders = prevQuery; }
    }

    /// <summary>候選落點是否在地圖範圍內（留一個身體半徑的邊距，免得貼著邊界）。</summary>
    static bool InsideMap(Vector2 p, Rect b)
        => p.x >= b.xMin + SpawnProbeRadius && p.x <= b.xMax - SpawnProbeRadius
        && p.y >= b.yMin + SpawnProbeRadius && p.y <= b.yMax - SpawnProbeRadius;

    /// <summary>玩家保留並移動：沒有就生一次（透過 MainSpawner），有就移到落點。</summary>
    void PlacePlayer(Vector2 pos)
    {
        if (_player == null) _player = GameObject.FindGameObjectWithTag("Player");

        if (_player == null)
        {
            if (playerSpawner == null) playerSpawner = FindObjectOfType<MainSpawner>();
            if (playerSpawner != null)
            {
                playerSpawner.transform.position = pos;   // SpawnPlayer 以 spawner 位置生成
                _player = playerSpawner.SpawnPlayer(playerSpawner.DefaultPlayerID);
            }
        }

        if (_player != null) _player.transform.position = pos;
        else Debug.LogError("[MapManager] 無法生成/找到玩家（檢查 MainSpawner 的 PlayerMappings 與 Player tag）。");
    }

    /// <summary>玩家的召喚物(PlayerAlly)跟著過傳送點：換圖清場時保留，這裡把存活的移到玩家新落點附近（黃金角散開免堆疊）。</summary>
    void RepositionPlayerAllies(Vector2 center)
    {
        var list = MonsterController.Active;
        int k = 0;
        for (int i = 0; i < list.Count; i++)
        {
            MonsterController mc = list[i];
            if (mc == null || mc.IsDead || mc.Faction != MonsterFaction.PlayerAlly) continue;
            float ang = k * 2.399963f;   // 黃金角，讓多隻散開不重疊
            Vector2 off = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.2f;
            mc.transform.position = center + off;
            k++;
        }
    }

    /// <summary>清掉屬於上一張地圖的暫態物件：怪物、飛行子彈、雷射、地面特效、一次性 VFX、地上掉落物，以及玩家身上持續型武器。</summary>
    void ClearTransientGameplay()
    {
        if (_player == null) _player = GameObject.FindGameObjectWithTag("Player");
        if (_player != null)
        {
            var pc = _player.GetComponent<PlayerController>();
            if (pc != null) pc.ClearPersistentWeaponsForMapChange();
        }

        // 怪物：清敵怪，但**保留玩家的召喚物(PlayerAlly)**——它們跟玩家一起過傳送點（下方 PlaceAndSetup→RepositionPlayerAllies 會移到玩家新落點）。
        foreach (var mcx in FindObjectsOfType<MonsterController>())
            if (mcx != null && mcx.Faction != MonsterFaction.PlayerAlly) Destroy(mcx.gameObject);
        DestroyAllOfType<Sorrows.Ballistics.BulletInstance>();
        DestroyAllOfType<Sorrows.Ballistics.LaserBeam>();
        DestroyAllOfType<GroundEffectInstance>();
        DestroyAllOfType<VfxInstance>();

        // 地上掉落物與互動點（拾取/劇情）屬「當前地圖」的暫態物件，換圖一併清掉（永久化屬 Phase 2）。
        if (InteractionManager.Exists) InteractionManager.Instance.ClearAll();
    }

    static void DestroyAllOfType<T>() where T : Component
    {
        var arr = FindObjectsOfType<T>();
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] != null) Destroy(arr[i].gameObject);
    }

    /// <summary>依 MapsTable 的 MapMode 套用相機模式（整張地圖 / 鏡頭跟隨）。元件仿 TeleportWatcher 自掛。</summary>
    void SetupCamera(int mode)
    {
        if (_camera == null)
            _camera = GetComponent<MapCameraController>() ?? gameObject.AddComponent<MapCameraController>();
        _camera.Apply(mapLoader.Map, mode, _player != null ? _player.transform : null);
    }

    /// <summary>
    /// 觸發鏈解鎖/旗標變動後重建互動點（星星/提示要即時出現或消失）。
    /// teleport/cutscene 是每幀動態判定（TriggerChain.IsActive），不需重建。由 TriggerChain 呼叫。
    /// </summary>
    public void RefreshTriggers()
    {
        if (InteractionManager.Exists) InteractionManager.Instance.RebuildPoints();
    }

    /// <summary>
    /// 解除「位置型觸發」（傳送點／過場點）的武裝——**任何「用程式把玩家搬過去」之後都要呼叫**。
    /// 規則是 readme/PROBLEMS.md **B11** 那條：**玩家不是自己走過去的，就不該觸發位置型事件**。
    /// 目前呼叫者：劇情演出收尾把主角放回開演前的位置（<see cref="Dipan.Cutscene.PlayerVisibility"/>）。
    /// </summary>
    public void DisarmPositionTriggers()
    {
        if (_watcher != null) _watcher.Disarm();
        if (_cutscene != null) _cutscene.Disarm();
    }

    void SetupWatcher()
    {
        // 觸發鏈：先重建（計算 startDisabled/enableFlag 的初始啟用狀態、藏綠幕），
        // 之後的 watcher / 互動點建立都會查它過濾。見 TriggerChain / readme/TRIGGER_CHAIN.md。
        TriggerChain.Setup(mapLoader.Map, this, mapLoader.SceneFxById, mapLoader.TeleportMarkerById);

        if (_watcher == null)
            _watcher = GetComponent<TeleportWatcher>() ?? gameObject.AddComponent<TeleportWatcher>();
        _watcher.Setup(mapLoader.Map, mapLoader.teleportTypeId,
                       _player != null ? _player.transform : null, this);

        // cutscene 觸發點：踩到就播過場（穿隧道 → 之後接影片）→ GoToMap。一次性。
        if (_cutscene == null)
            _cutscene = GetComponent<CutsceneWatcher>() ?? gameObject.AddComponent<CutsceneWatcher>();
        _cutscene.Setup(mapLoader.Map, mapLoader.cutsceneTypeId,
                        _player != null ? _player.transform : null, this, tunnelWalk, videoOverlay);

        // 互動點：建立「靠近按 F」的拾取點 / 劇情點 + 星星標示特效（由 InteractionManager 統一管理，
        // 與地上掉落物共用同一套互動）。Setup 會清舊的、重建新的（= 當次停留記憶）。
        InteractionManager.Instance.SetupInteractPoints(mapLoader.Map, mapLoader.pickupTypeId, mapLoader.dramaTypeId);

        // 鏡頭區：踩到就拉遠/位移相機、離開還原（給「站到佛陀腳下拉遠看全貌」用）。
        if (_camZone == null)
            _camZone = GetComponent<CameraZoneWatcher>() ?? gameObject.AddComponent<CameraZoneWatcher>();
        _camZone.Setup(mapLoader.Map, mapLoader.camZoneTypeId,
                       _player != null ? _player.transform : null, _camera);
    }
}
