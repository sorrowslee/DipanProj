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

    public int CurrentMapId => _currentMapId;
    public bool IsLoading => _loading;

    /// <summary>
    /// 抑制開場自動進關卡。由 GameFlowManager 在開機時設 true——改由標題→存讀檔 UI 決定要進哪。
    /// GameFlow 不存在時維持 false = 舊行為（依 autoStartLevel 自動進 startModule，方便單場景測試）。
    /// </summary>
    public static bool SuppressAutoStart = false;

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
        if (autoStartLevel && !SuppressAutoStart) StartLevel(startModule);
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
            if (loadingScreenHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(loadingScreenHoldSeconds);

            ClearTransientGameplay();

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
        SetupCamera(row.mode);
        // 依 MapsTable 的 Atmosphere 欄套用氛圍後處理（換圖即時切換，室外→古墓自動變氛圍）。見 AtmosphereController。
        AtmosphereController.ApplyMapAtmosphere(row.atmosphere);
        // 場景特效（世界端，如火雨）：依 SceneEffect 欄，換圖即時切換、自動清殘留。見 SceneEffectController。
        SceneEffectController.ApplyMapSceneEffect(row.sceneEffect, mapLoader.Map);
        // 進場一次性效果（如睜眼醒來）：依 EnterEffect 欄，進圖播一次就結束（承接的全黑會蓋過載入頁收尾）。見 EyeOpenController。
        EyeOpenController.ApplyMapEnterEffect(row.enterEffect);
        mapLoader.SpawnMonsters();
        SetupWatcher();
        Debug.Log($"[MapManager] 進入地圖 #{row.id}「{row.name}」(module={row.module})，落點={pos}。");

        // 進邪佛廣場（大廳）= 存檔檢查點：標記已抵達廣場（下次改中央出生）＋自動存檔。
        if (isHub && SaveManager.Instance != null)
        {
            SaveManager.Instance.HubIntroSpawnDone = true;
            SaveManager.Instance.SaveNow();
        }
    }

    /// <summary>落點解析：具名落點 → playerSpawn → 地圖中心。</summary>
    Vector2 ResolveSpawnPos(string entrance)
    {
        if (!string.IsNullOrEmpty(entrance) && mapLoader.TryGetEntrance(entrance, out var p)) return p;
        if (mapLoader.TryGetPlayerSpawn(out var sp)) return sp;
        Debug.LogWarning("[MapManager] 找不到傳送落點與玩家出生點，玩家放在地圖中心。");
        return MapCoords.WorldCenter(mapLoader.Map);
    }

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

    /// <summary>清掉屬於上一張地圖的暫態物件：怪物、飛行子彈、雷射、地面特效、一次性 VFX、地上掉落物，以及玩家身上持續型武器。</summary>
    void ClearTransientGameplay()
    {
        if (_player == null) _player = GameObject.FindGameObjectWithTag("Player");
        if (_player != null)
        {
            var pc = _player.GetComponent<PlayerController>();
            if (pc != null) pc.ClearPersistentWeaponsForMapChange();
        }

        DestroyAllOfType<MonsterController>();
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

    void SetupWatcher()
    {
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
