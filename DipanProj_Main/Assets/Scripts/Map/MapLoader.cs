using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
using Dipan.MapRuntime;
using Dipan.Save;

/// <summary>
/// 主遊戲端的 .dipanmap 載入器：把一張地圖建成背景／地磚／地上物／牆碰撞。
///
/// 設計重點：
/// - 本元件是「純建圖器」，由 <see cref="MapManager"/> 驅動（StartLevel / GoToMap）。
///   `LoadMap(path)` 可重入：先拆掉舊圖再建新圖，因此可用於多圖串接的換圖。
/// - **不再自己生玩家**：玩家的生成與落點由 MapManager 統籌（生一次、之後只移動）。
///   本元件只提供 `TryGetPlayerSpawn` / `TryGetEntrance` 查詢落點座標。
/// - 牆/水 = 可走層三態子格位元圖（'1' 牆 → Environment layer 擋＋反彈子彈；'2' 水/坑 → blocker layer 只擋腳、
///   子彈飛過）。解析度 = tileSize / walkSubdiv。詳見 BuildCellColliders。
/// - 素材以 PPU 256 載入，來源 StreamingAssets/MapAssets（可打包），編輯器內後備讀 GameAssets。
/// 見 readme/MAP_SYSTEM.md、readme/MAP_LOADER_SETUP.md。
/// </summary>
public class MapLoader : MonoBehaviour
{
    [Header("地圖檔（相對 StreamingAssets/MapAssets 或 GameAssets 的路徑）")]
    public string mapPath = "Modules/RedBridalGown/Maps/RedBridalGown_01.dipanmap";

    [Header("Layer 名稱")]
    public string environmentLayerName = "Environment";   // 牆＋地上物：擋玩家、反彈子彈
    public string blockerLayerName = "Water";             // 水塘/深坑：只擋腳、子彈飛過

    [Header("Trigger typeId")]
    // 牆/水現在直接畫在可走層三態子格（見下方 BuildCellColliders），不再用 trigger 標記。
    public string playerSpawnTypeId = "playerSpawn";
    public string monsterSpawnTypeId = "monsterSpawn";
    public string teleportTypeId = "teleport";
    public string pickupTypeId = "pickup";
    public string dramaTypeId = "drama";
    public string cutsceneTypeId = "cutscene";
    public string camZoneTypeId = "camZone";

    [Header("開關")]
    public bool buildBackground = true;
    public bool buildObjects = true;
    public bool addObjectColliders = true;
    public bool buildWalls = true;
    public bool buildBlockers = true;
    public bool spawnMonsters = true;
    public bool buildTeleportMarkers = true;
    public bool fitCameraToMap = true;
    // 預設關閉：由 MapManager 驅動。打開 = 不靠 MapManager，Awake 直接建 mapPath（建圖測試用，不生玩家）。
    public bool loadOnAwake = false;

    // ⚠ Inspector 的標籤是 Unity 從英文欄位名自動生成的，看不到底下的中文註解，
    //   所以三個欄位的中文名寫進 Header、細節寫進 Tooltip（滑鼠移上去會顯示）。
    [Header("地上物碰撞：貼合圖形（Subdiv＝子格解析度｜Fill Threshold＝實心判定門檻｜Scale＝整體內縮）")]
    [Tooltip("子格解析度。碰撞格大小 = tileSize / 這個值，愈大愈貼合、碰撞條數也愈多。\n" +
             "只有 1/2/4/8 有意義（其餘會往下收斂，填 6 等於 4）。\n" +
             "遮罩一律烘在 8，所以改這個不必重跑 Sync Map Assets。")]
    // 子格解析度：碰撞格大小 = tileSize / 這個值。愈大愈貼合、碰撞條數也愈多。
    // 遮罩烘在 8（見 ObjectFootprint.BakeSubdiv），這裡填 <= 8 會自動降取樣，**改這個不必重跑同步**
    // （烘焙端一律用 BakeSubdiv，所以同步時間與這個值完全無關）。
    // ⚠ 只有 1/2/4/8 有意義：其餘會被 ObjectFootprint.SnapSubdiv 往下收斂（填 6 等於 4），
    //   原因見該函式（Downsample 要整除、且 256/subdiv 是整數除法，非因數會讓形狀往右下漂）。
    //
    // 實測全 16 張地圖的碰撞條總數：subdiv 4 = 2435 條、subdiv 8 = 4412 條。
    // 一般房間只是 30~60 → 100~140（牆本來就 324 條在跑，這量級無感）；
    // 其中 2047/3511 條集中在邪佛廣場，全部來自那 288 個共用同一張圖的教徒——
    // 那張圖若嫌重，調解析度不如直接把教徒勾「可穿越」（碰撞歸零），比較精準。
    [Range(1, 8)] public int objectColliderSubdiv = 8;

    [Tooltip("實心判定門檻。遮罩「填滿率」高過這個值 = 這張圖本來就是實心方塊，改用單一方框（省一顆 Composite）。\n" +
             "設 1 = 一律逐格貼合；設 0 = 一律單框（＝回到改版前的舊行為，出事時的緊急退路）。\n" +
             "注意填滿率會隨解析度變：同一個書架 subdiv 4 是 1.00（走單框）、subdiv 8 是 0.82（走貼合）。")]
    // 遮罩「填滿率」高於此值 = 這張圖本來就是實心方塊，改用單一方框（省一顆 Composite，形狀幾乎無差）。
    // 設 1 = 一律逐格貼合；設 0 = 一律單框（回到舊行為）。
    // ⚠ 填滿率會隨解析度變：同一個書架在 subdiv 4 是 1.00（→單框，看起來就是一大塊方形），
    //   在 subdiv 8 掉到 0.82（→逐格貼合）。全專案走單框的物件 subdiv 4 有 15 個、subdiv 8 只剩 3 個，
    //   所以「換解析度」的視覺差異有一半其實來自這條捷徑的翻面，不只是格子變細。
    [Range(0f, 1f)] public float objectSolidFillThreshold = 0.9f;

    [Tooltip("整體內縮。以物件中心等比縮整個碰撞形狀，1 = 照圖形，調小 = 整圈往內收、玩家更好走。\n" +
             "縮的是整個形狀（size 與 offset 同乘），相鄰碰撞條仍相接、不會裂出縫。")]
    // 整體縮放碰撞形狀（以物件中心為基準等比縮）。1 = 照圖形；調小 = 整圈往內收，玩家更好走。
    // 對「貼合」與「單框」兩條路都有效；縮的是整個形狀，相鄰碰撞條仍相接，不會裂出縫。
    [Range(0.1f, 1f)] public float objectColliderScale = 1f;

    [Header("地上物可破壞")]
    public bool objectsDestructible = true;
    public float objectMaxHP = 20f;
    public int objectDestroyVfxId = 5;   // 對應 VfxTable.csv;檔名/張數/FPS 在那一列設定

    [Header("傳送點特效（VfxTable ID，須為 Loop=1/Duration=-1 的循環特效；0 = 不放）")]
    public int teleportVfxId = 6;


    // ---- runtime ----
    MapData _map;
    Catalog _catalog;
    MapSpriteLoader _sprites;
    VfxManager _vfx;
    string _assetRoot;
    Transform _root;
    int _envLayer;
    MapObjectRevealer _revealer;   // 本圖「靠旗標中途現身」的地上物顯現管理器（掛物件根下，換圖隨 MapRoot 銷毀）
    MapMonsterRespawner _respawner;   // 本圖「重複產生」的怪物出生點計時器（同上，掛 MapRoot 下，換圖隨之銷毀）
    int _blockerLayer;

    public MapData Map => _map;

    /// <summary>分幀載入的最近一次結果（給 MapManager 在 LoadMapRoutine 之後判斷成敗）。</summary>
    public bool LastLoadOk { get; private set; }

    /// <summary>地上物分幀建立：每幀建幾個（含逐張載圖＋alpha 掃描，是最重的一段）。</summary>
    public int objectsPerFrame = 8;

    void Awake()
    {
        _envLayer = ResolveLayer(environmentLayerName, fallback: 3);
        _blockerLayer = LayerMask.NameToLayer(blockerLayerName); // 可能 -1，沒水塘就不會用到

        // catalog/sprites 只載一次（之後每次換圖只重讀 .dipanmap）。
        _catalog = CatalogLoader.Load(out _assetRoot);
        _sprites = new MapSpriteLoader(_assetRoot);

        if (loadOnAwake)
        {
            LoadMap(mapPath);
            if (spawnMonsters) SpawnMonsters();
        }
    }

    // ================= 對外 API（MapManager 使用）=================

    /// <summary>載入並建出一張地圖（先拆掉舊圖）。成功回 true。不生玩家、不生怪（怪另呼叫 SpawnMonsters）。</summary>
    public bool LoadMap(string path)
    {
        Teardown();

        if (!LoadMapData(path)) return false;

        BuildRoot();
        if (buildBackground) BuildBackground();
        if (buildObjects) BuildObjects();
        if (buildWalls || buildBlockers) BuildCellColliders();
        if (buildTeleportMarkers) BuildTeleportMarkers();
        BuildSceneFx();
        BuildMapLights();
        if (fitCameraToMap) FitCamera();

        Debug.Log($"[MapLoader] 載入完成：{_map.name}（{_map.width}×{_map.height}, module={_map.module}）");
        return true;
    }

    /// <summary>
    /// 分幀（非同步）版的 LoadMap：把建圖拆成多幀，避免一次塞爆主執行緒造成進場凍住。
    /// 與 <see cref="LoadMap"/> 走同一批建構方法，只差「地上物分批建 + 階段間 yield + 回報進度」。
    /// 結果寫進 <see cref="LastLoadOk"/>。不生玩家、不生怪（怪由 MapManager 在之後呼叫 SpawnMonsters）。
    /// onProgress 回報 0~1。
    /// </summary>
    public IEnumerator LoadMapRoutine(string path, System.Action<float> onProgress)
    {
        LastLoadOk = false;
        Teardown();
        onProgress?.Invoke(0.02f);
        yield return null;

        if (!LoadMapData(path)) { onProgress?.Invoke(1f); yield break; }   // 失敗：呼叫端依 LastLoadOk 處理

        BuildRoot();
        if (buildBackground) BuildBackground();
        onProgress?.Invoke(0.1f);
        yield return null;

        onProgress?.Invoke(0.2f);
        yield return null;

        // 地上物：最重的一段（逐張載 PNG + alpha 掃描），分批建、把進度映射到 0.2~0.85。
        if (buildObjects)
            yield return StartCoroutine(BuildObjectsRoutine(
                Mathf.Max(1, objectsPerFrame),
                t => onProgress?.Invoke(Mathf.Lerp(0.2f, 0.85f, t))));

        if (buildWalls || buildBlockers) BuildCellColliders();
        onProgress?.Invoke(0.92f);
        yield return null;

        if (buildTeleportMarkers) BuildTeleportMarkers();
        BuildSceneFx();
        BuildMapLights();
        if (fitCameraToMap) FitCamera();   // MapManager 會關掉此旗標；保留以相容 loadOnAwake

        LastLoadOk = true;
        onProgress?.Invoke(1f);
        Debug.Log($"[MapLoader] 載入完成（分幀）：{_map.name}（{_map.width}×{_map.height}, module={_map.module}）");
    }

    /// <summary>
    /// 預載整個 module（＋Main 共用）的素材貼圖到快取：把 catalog 內屬於該 module/Main 的每筆圖先解碼快取
    /// （最重的磁碟讀取＋PNG 解碼一次做完）。之後同 module 房間互跳因為都命中快取，建圖很快、不必再出讀取頁。
    /// onProgress 回報 0~1。由 MapManager 在「進入新 module」時呼叫。
    /// </summary>
    public IEnumerator PreloadModuleRoutine(string module, System.Action<float> onProgress)
    {
        if (_catalog?.items == null || _sprites == null) { onProgress?.Invoke(1f); yield break; }

        var items = new List<CatalogItem>();
        foreach (var it in _catalog.items)
            if (it != null && (it.module == module || it.module == "Main")) items.Add(it);

        int total = Mathf.Max(1, items.Count), done = 0;
        foreach (var it in items)
        {
            _sprites.GetTexture(it);   // 解碼＋快取主圖（動畫物件＝第一幀）
            if (it.IsAnimated && it.frames != null)
                for (int f = 0; f < it.frames.Count; f++) _sprites.GetFrameTexture(it.frames[f]);
            done++;
            if (done % 3 == 0) { onProgress?.Invoke((float)done / total); yield return null; }
        }
        onProgress?.Invoke(1f);
        Debug.Log($"[MapLoader] 已預載 module「{module}」(+Main) 素材：{items.Count} 筆。");
    }

    /// <summary>依當前地圖的 monsterSpawn 出生點生怪。需在 MonsterSpawner.Awake 之後呼叫（MapManager 在 Start 驅動）。</summary>
    public void SpawnMonsters()
    {
        if (spawnMonsters) SpawnMonstersFromMap();
    }

    /// <summary>玩家出生點（playerSpawn）中心，供 MapManager 放置玩家（關卡開場、或落點找不到時的後備）。</summary>
    public bool TryGetPlayerSpawn(out Vector2 center) => TryGetRegionCenter(playerSpawnTypeId, out center);

    /// <summary>找 entranceId 相符的傳送點中心，供 MapManager 決定傳送落點。</summary>
    public bool TryGetEntrance(string entranceId, out Vector2 center)
    {
        center = Vector2.zero;
        if (string.IsNullOrEmpty(entranceId)) return false;
        var trig = _map?.TriggerLayer;
        if (trig?.regions == null) return false;
        foreach (var r in trig.regions)
        {
            if (r.typeId != teleportTypeId) continue;
            if (r.GetString("entranceId") != entranceId) continue;
            // 落點＝傳送點錨點（沒設才退回格子平均中心）。錨點可能落在門的位置（牆上），
            // 但 MapManager.ResolveSpawnPos 外面包了 FreeSpotNear——被牆/家具擋住會自動推到最近的可站處，
            // 所以「落在門上」是安全的，而且推出來的位置就是門前那塊地板。
            return TeleportAnchor.TryCenter(r, _map, out center);
        }
        return false;
    }

    // ================= 載入 / 拆除 =================

    bool LoadMapData(string path)
    {
        mapPath = path;
        string mapAbs = ResolveMapPath(_assetRoot);
        if (mapAbs == null)
        {
            Debug.LogError($"[MapLoader] 找不到地圖檔：{mapPath}");
            return false;
        }
        try { _map = MapSerializer.Load(mapAbs); }
        catch (System.Exception e) { Debug.LogError($"[MapLoader] 讀地圖失敗：{e.Message}"); return false; }
        return _map != null;
    }

    /// <summary>拆掉上一張地圖建出的所有物件（背景/地磚/地上物/牆碰撞）。玩家與場上怪/彈不在這裡清（見 MapManager）。</summary>
    void Teardown()
    {
        if (_root != null)
        {
            // ⚠ **先 SetActive(false) 再 Destroy**：Unity 的 Destroy 是**延到幀尾**才生效，
            //   而「同 module 房間互跳」走的是同步版 LoadMap、整段在同一幀跑完
            //   （見 MapManager.LoadMapRoutine 的 else 分支）。不先停用的話，
            //   接下來那一段時間裡**舊地圖與新地圖的碰撞體同時存在於物理世界**，
            //   任何在換圖後立刻做的物理查詢都會查到上一張圖的牆——而且座標系還是新圖的，
            //   查出來的結果毫無意義又完全靜默。
            //   實際踩過：MapManager 的落點防呆在書房查 (9,-1.5) 查到了客廳2 的牆，
            //   把玩家一路挪到地圖外面（見 readme/PROBLEMS.md **B12**）。
            //   SetActive(false) 是**立即**生效的，碰撞體當下就退出物理世界。
            _root.gameObject.SetActive(false);
            Destroy(_root.gameObject);
            _root = null;
        }
        _revealer = null;   // 隨 MapRoot 一起銷毀了，清引用
        _respawner = null;  // 同上（不清的話換圖後會拿到已銷毀的元件，重複產生的出生點就失效了）
    }

    /// <summary>地圖檔路徑解析：先 StreamingAssets/MapAssets，再（編輯器）GameAssets。</summary>
    string ResolveMapPath(string assetRoot)
    {
        string p = Path.Combine(assetRoot, mapPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(p)) return p;

        string alt = Path.Combine(CatalogLoader.StreamingDir, mapPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(alt)) return alt;
#if UNITY_EDITOR
        string alt2 = Path.Combine(CatalogLoader.GameAssetsDir, mapPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(alt2)) return alt2;
#endif
        return null;
    }

    void BuildRoot()
    {
        var go = new GameObject($"MapRoot ({_map.name})");
        go.transform.SetParent(transform, false);
        go.transform.position = Vector3.zero;   // 地圖座標即世界座標，root 不偏移
        _root = go.transform;
    }

    // ---- 背景 ----
    void BuildBackground()
    {
        if (string.IsNullOrEmpty(_map.backgroundId)) return;
        var item = _catalog.Find(_map.backgroundId);
        var sprite = _sprites.GetWholeSprite(item, _map.tileSize);
        if (sprite == null) { Debug.LogWarning($"[MapLoader] 背景找不到：{_map.backgroundId}"); return; }

        var go = new GameObject("Background");
        go.transform.SetParent(_root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -1000;

        float w = _map.width * _map.tileSize;
        float h = _map.height * _map.tileSize;
        Vector3 size = sprite.bounds.size;
        if (size.x > 0f && size.y > 0f)
            go.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
        go.transform.position = new Vector3(_map.origin.x + w / 2f, _map.origin.y - h / 2f, 0f);
    }

    // ---- 地上物 ----
    // Y 排序公式集中在 MapDepthSort（角色/怪物共用同一套，見該檔）。這裡只留「可走地上物」的固定低排序。
    const int WalkableObjectSortingOrder = 5;   // 可走地上物：低於角色、高於地磚，畫在角色腳下

    void BuildObjects()
    {
        var layer = _map.GameLayer;
        if (layer?.objects == null || layer.objects.Count == 0) return;

        var objRoot = new GameObject("Objects");
        objRoot.transform.SetParent(_root, false);
        _revealer = objRoot.AddComponent<MapObjectRevealer>();   // 靠旗標中途現身的地上物用

        for (int i = 0; i < layer.objects.Count; i++) BuildOneObject(layer.objects[i], objRoot.transform, i);
    }

    /// <summary>分幀建地上物：每 perFrame 個 yield 一次並回報進度（0~1）。建構內容與 BuildObjects 完全相同。</summary>
    IEnumerator BuildObjectsRoutine(int perFrame, System.Action<float> onProgress)
    {
        var layer = _map.GameLayer;
        if (layer?.objects == null || layer.objects.Count == 0) { onProgress?.Invoke(1f); yield break; }

        var objRoot = new GameObject("Objects");
        objRoot.transform.SetParent(_root, false);
        _revealer = objRoot.AddComponent<MapObjectRevealer>();   // 靠旗標中途現身的地上物用

        int total = layer.objects.Count, done = 0;
        for (int i = 0; i < layer.objects.Count; i++)
        {
            BuildOneObject(layer.objects[i], objRoot.transform, i);
            done++;
            if (done % perFrame == 0)
            {
                onProgress?.Invoke((float)done / total);
                yield return null;
            }
        }
        onProgress?.Invoke(1f);
    }

    /// <summary>建一個地上物（載圖、SpriteRenderer、動畫、碰撞框、可破壞）。sync 與分幀版共用。objIndex＝在 objects 清單的索引，當關卡進度的穩定 key。</summary>
    void BuildOneObject(ObjectInstance inst, Transform objRoot, int objIndex)
    {
        // 關卡進度：本趟已被破壞的地上物不再重建（換圖回來維持破壞狀態）。key＝清單索引，同一張地圖檔每次解析順序一致。
        int mapId = MapManager.Instance != null ? MapManager.Instance.CurrentMapId : -1;
        string objKey = $"obj#{objIndex}";
        if (RunProgress.Exists && RunProgress.Instance.RunActive
            && RunProgress.Instance.IsObjectDestroyed(mapId, objKey)) return;

        // 出現條件①「完成 N 關」：進地圖當下判定（此條件不會在關卡進行中改變），未達則整個不生。
        // 範圍：lifetime=曾達到的最高完成數（永久）、其餘(cycle)=本周目完成數（輪迴重置會再隱藏）。
        // 沒有 SaveManager（如編輯器直測/DevQuickStart 無存檔）時不擋，一律照舊出現，方便測試。
        if (inst.appearAfterClears > 0 && SaveManager.Instance != null)
        {
            int have = (inst.appearScope == "lifetime")
                ? SaveManager.Instance.LifetimeMaxClears
                : SaveManager.Instance.ClearedModuleCount;
            if (have < inst.appearAfterClears) return;
        }

        // 消失旗標：旗標已成立＝這個地上物早該消失（例：上次已撿走佛燈）→ 進圖時根本不生。
        // 旗標尚未成立＝照常建好，並登記給 revealer，等關卡中途旗標成立時銷毀（見文末 RegisterDisappear）。
        bool disappearGated = !string.IsNullOrEmpty(inst.disappearFlag);
        if (disappearGated && TriggerChain.FlagTrue(inst.disappearFlag)) return;

        // 出現條件②「旗標」：與①同時設＝兩者都要滿足(AND)。旗標未成立時「先建好、藏起來」，
        // 等旗標中途成立由 MapObjectRevealer 現身（動畫從第0幀起播）；重進場旗標已成立＝直接顯示。
        bool flagGated = !string.IsNullOrEmpty(inst.appearFlag);
        bool flagTrue  = !flagGated || TriggerChain.FlagTrue(inst.appearFlag);
        bool hidden    = flagGated && !flagTrue;    // 先藏、等旗標
        bool alreadyOn = flagGated && flagTrue;     // 重進場旗標已成立（早已現身過）

        var item = _catalog.Find(inst.assetId);
        var sprite = _sprites.GetWholeSprite(item, _map.tileSize);
        if (sprite == null) { Debug.LogWarning($"[MapLoader] 地上物找不到：{inst.assetId}"); return; }

        string leaf = inst.assetId;
        int slash = leaf.LastIndexOf('/');
        if (slash >= 0) leaf = leaf.Substring(slash + 1);

        var go = new GameObject($"Obj_{leaf}");
        go.transform.SetParent(objRoot, false);
        go.layer = _envLayer;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;   // = 第一幀（GetWholeSprite 用 item.path = 第一幀）
        // 一般地上物：走 Y 排序帶（依放置 Y 與角色一起交錯遮蔽，見 MapDepthSort）。
        // 可走地上物（可踩上去，如木板/地毯）：固定低排序、畫在角色腳下。
        sr.sortingOrder = inst.walkable
            ? WalkableObjectSortingOrder
            : MapDepthSort.Order(inst.sortKey, inst.zOrder);

        // 動畫地上物：載入幀序列、掛播放元件。播放模式＝循環/乒乓/播一次(playOnce)。
        // 起播控制：藏起來→暫停(等現身再 PlayFromStart)；重進場旗標已成立且播一次→定格最後一幀(不重播)；其餘→照播。
        AnimatedMapObject anim = null;
        if (item != null && item.IsAnimated)
        {
            var frames = _sprites.GetAnimationFrames(item, _map.tileSize);
            if (frames != null && frames.Length >= 2)
            {
                bool showLast    = alreadyOn && inst.playOnce;   // 早已跪好 → 定格最後一幀
                bool startPlaying = !hidden && !showLast;        // 藏→暫停；定格→不播；其餘→播
                anim = go.AddComponent<AnimatedMapObject>();
                anim.Initialize(sr, frames, inst.animFps, inst.pingPong, inst.playOnce, startPlaying, showLast);
            }
            else
                Debug.LogWarning($"[MapLoader] 動畫地上物「{inst.assetId}」幀載入失敗，退回靜態第一幀。");
        }

        go.transform.position = new Vector3(inst.x, inst.y, 0f);
        go.transform.localScale = new Vector3(
            (inst.flipX ? -1f : 1f) * inst.scaleX,
            (inst.flipY ? -1f : 1f) * inst.scaleY, 1f);
        go.transform.rotation = Quaternion.Euler(0, 0, inst.rot);

        // walkable = true：不設碰撞、不擋路（走不走由地圖可走層判定；例：木板/地毯）。因此也不掛可破壞。
        // 碰撞一律建在 go 本身（不開子物件）——命中判定有兩種寫法（GetComponent 與 GetComponentInParent，
        // 見 PlayerController），碰撞若掛在子物件上，GetComponent 那幾條路會找不到 DestructibleObject ⇒ 打不壞。
        if (addObjectColliders && !inst.walkable && !inst.passThrough)
            BuildObjectCollision(go, item, sprite);

        // 榕樹妖的臉：掛控制器（發招換 crazy 臉、死亡燃燒演出）。載入 crazy 變體 sprite 交給它。
        bool isBanyanFace = !string.IsNullOrEmpty(inst.assetId) && inst.assetId.Contains("treeFace");
        if (isBanyanFace)
        {
            var crazyItem = _catalog.Find(inst.assetId.Replace("vicious", "crazy"));
            Sprite crazySprite = crazyItem != null ? _sprites.GetWholeSprite(crazyItem, _map.tileSize) : null;
            go.AddComponent<BanyanBossFace>().Setup(sr, sprite, crazySprite);
        }

        // hp < 0（例如 -1）= 不可摧毀:不掛 DestructibleObject,但上面的碰撞框照常 → 等於一般牆壁(擋＋反彈)。
        // walkable 物件沒有碰撞、打不到，也不掛可破壞。
        // 榕樹妖的臉刻意不可破壞（boss 不能被直接攻擊；臉是演出用地上物，被打爆會破壞死亡表演）。
        if (objectsDestructible && !inst.walkable && !inst.passThrough && inst.hp >= 0 && !isBanyanFace)
        {
            var d = go.AddComponent<DestructibleObject>();
            float hp = inst.hp > 0 ? inst.hp : objectMaxHP;   // >0 用編輯器血量;==0 退回全域後備值
            d.Configure(hp, objectDestroyVfxId, inst.breakFlag);   // 破壞時寫此擺放的「破壞觸發旗標」
            d.SetRunKey(mapId, objKey);   // 關卡進度：破壞後本趟不再重建
        }

        // 靠旗標中途現身：先關掉顯示與碰撞、登記給 revealer；旗標成立時再現身（延遲/淡入/動畫起播）。
        // 碰撞一律取「這個物件身上的全部 Collider2D」再交出去——貼合圖形後一個物件可能有很多顆
        // （Composite + 一堆 usedByComposite 的 box），只開關其中一顆會出現「東西還沒現身卻已經擋路」。
        if (hidden && _revealer != null)
        {
            sr.enabled = false;
            var cols = go.GetComponents<Collider2D>();
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
            _revealer.RegisterHidden(inst.appearFlag, go, sr, cols, anim,
                                     inst.appearDelaySeconds, inst.appearFade);
        }

        // 消失旗標：登記給 revealer，等關卡中途此旗標首次成立時把整個物件銷毀（碰撞一併移除）。
        if (disappearGated && _revealer != null)
            _revealer.RegisterDisappear(inst.disappearFlag, go);

        // 發光地上物（火把/燈籠/香爐/地上的佛燈…）：掛 LightSource，AtmosphereController 每幀取最近的幾盞照亮畫面。
        // 編輯器地上物面板的「照明」那一組欄位原樣轉進元件；光色是 6 碼 16 進位字串，解不出來就用預設暖橘。
        if (inst.lightRadius > 0f)
        {
            var light = go.AddComponent<LightSource>();
            light.radius       = inst.lightRadius;
            light.intensity    = inst.lightIntensity > 0f ? inst.lightIntensity : 1f;
            light.flicker      = Mathf.Max(0f, inst.lightFlicker);
            light.flickerSpeed = inst.lightFlickerSpeed > 0f ? inst.lightFlickerSpeed : 1f;
            light.softness     = Mathf.Clamp01(inst.lightSoftness > 0f ? inst.lightSoftness : 0.46f);
            light.color        = ParseLightColor(inst.lightColor);
        }
    }

    /// <summary>
    /// 建一個地上物的碰撞，**貼合圖形**：把素材切成子格，只有「有畫東西」的格子才擋路
    /// （屏風的簍空、燈籠桿兩側、椅腳之間都會通）。回傳主要碰撞元件（取不到遮罩時可能為 null）。
    ///
    /// <para><b>為什麼不用單一方框</b>：舊做法拿「整張圖不透明像素的外接矩形」，只能縮框不能挖洞，
    /// 圖上明明透明的地方照樣擋路，把素材邊切掉也救不了。詳見 <see cref="Dipan.MapRuntime.FootprintMask"/>。</para>
    ///
    /// <para><b>為什麼要 CompositeCollider2D</b>：一堆小方框排在一起會留下「內部接縫」，
    /// 圓形的玩家貼著表面滑動時會在接縫上拿到一瞬間的斜法線而卡住（子彈反彈方向也會亂跳）。
    /// Composite 會把相鄰方框併成單一多邊形外框，接縫消失——**牆本來就是這樣做的**（見 BuildCompositeFromCells）。</para>
    ///
    /// <para>三條路：① 遮罩取不到 → 退回舊的單一 alpha 外接矩形；
    /// ② 遮罩幾乎填滿（<see cref="objectSolidFillThreshold"/>）→ 也用單框，形狀差不多但省一顆 Composite；
    /// ③ 其餘 → 同一列連續格併成一條 box，全部 usedByComposite 交給 Composite 合併。</para>
    ///
    /// <para>座標一律用**本地空間**（相對 sprite 中心），位置/縮放/翻轉交給物件自己的 transform 處理，
    /// 與舊的單框做法完全一致，因此擺放端的行為（含 flipX/flipY）不需要另外處理。</para>
    /// </summary>
    Collider2D BuildObjectCollision(GameObject go, CatalogItem item, Sprite sprite)
    {
        float shrink = Mathf.Clamp(objectColliderScale, 0.1f, 1f);
        var mask = _sprites.GetFootprint(item, objectColliderSubdiv);   // 內部會 SnapSubdiv 收斂成 1/2/4/8

        // ① / ②：沒有遮罩，或這張圖本來就近乎實心 → 單一方框（＝舊行為）。
        if (mask == null || !mask.Ok || ObjectFootprint.FillRatio(mask) >= objectSolidFillThreshold)
            return AddAlphaBoxCollider(go, item, sprite, shrink);

        // ③ 逐格貼合。遮罩以「整張畫布的左上角」為錨點往右下鋪。
        // 畫布尺寸取 GetAlphaLocalBox 的 canvas（由貼圖寬高算出、已快取），不用 sprite.bounds——
        // Sprite.Create 預設是 Tight mesh，bounds 不保證等於整張畫布，用錯會讓整個碰撞形狀偏移。
        // 先把「同一列連續的擋路格」收成一條條 run，全部算完再建元件——
        // 這樣「一條都沒有」時可以乾淨地退回單框，不必事後 Destroy 已建好的 Composite。
        var abox = _sprites.GetAlphaLocalBox(item, _map.tileSize);
        Vector2 canvas = abox.ok ? abox.canvas : (Vector2)sprite.bounds.size;
        float cellSize = _map.tileSize / mask.subdiv;
        float left = -canvas.x * 0.5f;
        float top = canvas.y * 0.5f;

        _runBuf.Clear();
        for (int cy = 0; cy < mask.rows; cy++)
        {
            int runStart = -1;
            for (int cx = 0; cx <= mask.cols; cx++)   // 多跑一格，讓最後一段也能收尾
            {
                bool solid = cx < mask.cols && mask.At(cx, cy);
                if (solid) { if (runStart < 0) runStart = cx; continue; }
                if (runStart < 0) continue;
                _runBuf.Add(new Vector3Int(runStart, cy, cx - runStart));   // (起始欄, 列, 長度)
                runStart = -1;
            }
        }

        if (_runBuf.Count == 0)   // 理論上不會（Scan 全透明會回 null），保險用
            return AddAlphaBoxCollider(go, item, sprite, shrink);

        var rb = go.AddComponent<Rigidbody2D>();   // 必須在 Composite 之前加，否則 Unity 會自動補一顆 Dynamic 的
        rb.bodyType = RigidbodyType2D.Static;

        var composite = go.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Manual;   // 之後開關 collider 不會觸發重算

        for (int i = 0; i < _runBuf.Count; i++)
        {
            int x0 = _runBuf[i].x, cy = _runBuf[i].y, count = _runBuf[i].z;

            // 距畫布左上角的距離（本地單位）。最右一欄／最下一列是被截短的
            // （cols = ceil(圖寬/格寬)，例：355px @ subdiv8 → 12 欄 × 0.125 = 1.5 > 畫布 1.387），
            // 不夾回畫布邊界的話右邊與下邊會多出最多 0.11 格的隱形牆，而且形狀左右不對稱
            // ——正是這次要修掉的那種「看起來沒東西卻走不過去」。
            float xa = x0 * cellSize;
            float xb = Mathf.Min((x0 + count) * cellSize, canvas.x);
            float ya = cy * cellSize;
            float yb = Mathf.Min((cy + 1) * cellSize, canvas.y);
            if (xb <= xa || yb <= ya) continue;   // 整條都在畫布外（理論上不會，遮罩那邊已擋掉）

            var box = go.AddComponent<BoxCollider2D>();
            box.usedByComposite = true;
            // 整個形狀以物件中心等比內縮：size 與 offset 同乘 shrink，相鄰段仍相接、不會裂出縫。
            box.size = new Vector2(xb - xa, yb - ya) * shrink;
            box.offset = new Vector2(left + (xa + xb) * 0.5f, top - (ya + yb) * 0.5f) * shrink;
        }

        composite.GenerateGeometry();
        return composite;
    }

    // 建碰撞時的暫存（避免每個地上物都配置一個 List）。(x=起始欄, y=列, z=長度)
    readonly List<Vector3Int> _runBuf = new List<Vector3Int>();

    /// <summary>舊行為：一顆貼合「不透明像素外接矩形」的方框。遮罩取不到、或圖近乎實心時使用。</summary>
    BoxCollider2D AddAlphaBoxCollider(GameObject go, CatalogItem item, Sprite sprite, float shrink)
    {
        var b = _sprites.GetAlphaLocalBox(item, _map.tileSize);
        var col = go.AddComponent<BoxCollider2D>();
        if (b.ok)
        {
            col.size = b.size * shrink;
            col.offset = b.offset * shrink;
        }
        else
        {
            col.size = sprite.bounds.size * shrink;   // 後備：整張圖外框
            col.offset = Vector2.zero;
        }
        return col;
    }

    /// <summary>把編輯器存的 6 碼 16 進位光色（RRGGBB，不含 #）轉成 Color；空/無效＝預設暖橘。</summary>
    static Color ParseLightColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return LightSource.DefaultWarm;
        string s = hex.Trim().TrimStart('#');
        return ColorUtility.TryParseHtmlString("#" + s, out var c) ? c : LightSource.DefaultWarm;
    }

    // ---- 獨立光源（不綁地上物的照明點；由編輯器「照明」分頁放置，存在 map.lights）----

    /// <summary>
    /// 把 map.lights 逐個生成空物件並掛 <see cref="LightSource"/>。
    /// 這些光源**沒有任何外觀**——火炬/燈籠的圖本來就畫在背景裡，這裡只補「會發光」這件事。
    /// 掛在地圖 root 底下，換圖拆 _root 時一併清掉（LightSource 的 OnDisable 會自動退出登記表）。
    /// </summary>
    void BuildMapLights()
    {
        if (_map?.lights == null || _map.lights.Count == 0) return;

        var root = new GameObject("MapLights");
        root.transform.SetParent(_root, false);

        int n = 0;
        foreach (var li in _map.lights)
        {
            if (li == null || li.radius <= 0f) continue;

            var go = new GameObject(string.IsNullOrWhiteSpace(li.name) ? "Light" : ("Light_" + li.name));
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(li.x, li.y, 0f);

            var light = go.AddComponent<LightSource>();
            light.radius       = li.radius;
            light.intensity    = li.intensity > 0f ? li.intensity : 1f;
            light.flicker      = Mathf.Max(0f, li.flicker);
            light.flickerSpeed = li.flickerSpeed > 0f ? li.flickerSpeed : 1f;
            light.softness     = Mathf.Clamp01(li.softness > 0f ? li.softness : 0.46f);
            light.color        = ParseLightColor(li.color);
            n++;
        }

        // 這張圖有放燈、但氛圍不吃照明時提醒一聲——否則會出現「燈明明放了卻完全沒效果」的靜默失效。
        // （吃照明的是 Atmosphere 2/3/9，以及 Atmosphere=1 但 MapsTable 環境亮度 <100 的地圖。）
        if (n > 0) Debug.Log($"[MapLoader] 獨立光源 {n} 盞。若畫面沒有變化，檢查這張圖的 Atmosphere 與 EnvBright（見 readme/ATMOSPHERE.md）。");
    }

    // ---- 場景特效（可放置的粒子特效，煙/火/冰/毒…；由編輯器 map.sceneFx 放置，SceneFxTable 定義外觀）----

    /// <summary>本張地圖的場景特效實例（sceneFx 的 id → 場上物件）。給觸發鏈 linkedFx 顯示/隱藏綠幕用（見 TriggerChain），換圖時重建。</summary>
    public readonly Dictionary<string, GameObject> SceneFxById = new Dictionary<string, GameObject>();

    /// <summary>本張地圖的傳送點內建外型（teleport region id → marker 特效物件）。給觸發鏈 togglePortal 隱藏/恢復傳送點外型用，換圖時重建。</summary>
    public readonly Dictionary<string, GameObject> TeleportMarkerById = new Dictionary<string, GameObject>();

    void BuildSceneFx()
    {
        SceneFxById.Clear();
        if (_map?.sceneFx == null || _map.sceneFx.Count == 0) return;

        var root = new GameObject("SceneFx");
        root.transform.SetParent(_root, false);   // 掛地圖 root，換圖拆 _root 一併清掉

        foreach (var fx in _map.sceneFx)
        {
            var look = SceneFxTable.Get(fx.fxId);
            Vector3 start = new Vector3(fx.startX, fx.startY, 0f);
            Vector3 end = fx.hasEnd
                ? new Vector3(fx.endX, fx.endY, 0f)
                : start + new Vector3(0f, 2.5f * Mathf.Max(0.2f, fx.h), 0f);   // 沒終點＝從起點朝上噴一段

            var go = new GameObject($"SceneFx_{fx.fxId}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = start;
            if (!string.IsNullOrEmpty(fx.id)) SceneFxById[fx.id] = go;

            if (look.kind == 1)
            {
                // 傳送門：起點/終點＝矩形的對角，畫一塊發光矩形＋內部漂浮光點。
                go.AddComponent<PortalFx>().Configure(look, start, end);
            }
            else
            {
                // 弧線粒子流（煙/火/冰/毒）：控制點外推成弧。
                Vector3 dir = end - start;
                float len = dir.magnitude;
                Vector3 perp = (len > 1e-4f) ? new Vector3(-dir.y, dir.x, 0f).normalized : Vector3.right;
                Vector3 ctrl = (start + end) * 0.5f + perp * fx.bulge;
                go.AddComponent<SceneFxEmitter>().Configure(
                    look, start, ctrl, end, fx.w, fx.h, fx.loop, fx.intermittent, fx.interval);
            }
        }
    }

    // ---- 牆 / 擋腳碰撞 ----
    // 牆/水直接畫在可走層三態子格位元圖（解析度 = tileSize / walkSubdiv，可細膩描邊）：
    //   '1' 牆  → Environment layer（擋腳＋反彈子彈）
    //   '2' 水/坑 → blocker layer（只擋腳、子彈飛過）
    //   '0' 可走 → 不生碰撞
    void BuildCellColliders()
    {
        var wallCells = new HashSet<long>();      // 牆：擋腳＋反彈
        var blockerCells = new HashSet<long>();   // 水/坑：只擋腳
        var walk = _map.WalkableLayer;
        if (walk?.blocked != null)
        {
            int fh = _map.FineHeight, fw = _map.FineWidth;
            for (int y = 0; y < walk.blocked.Count && y < fh; y++)
            {
                string row = walk.blocked[y];
                if (string.IsNullOrEmpty(row)) continue;
                for (int x = 0; x < row.Length && x < fw; x++)
                {
                    char c = row[x];
                    if (c == '1') wallCells.Add(Key(x, y));
                    else if (c == '2') blockerCells.Add(Key(x, y));
                }
            }
        }

        if (buildWalls && wallCells.Count > 0)
            BuildCompositeFromCells("Map_Walls (Environment)", wallCells, _envLayer);

        if (buildBlockers && blockerCells.Count > 0)
        {
            if (_blockerLayer < 0)
                Debug.LogWarning($"[MapLoader] 有 {blockerCells.Count} 子格「水/坑」(只擋腳)，" +
                    $"但找不到 layer「{blockerLayerName}」，已略過。要啟用請設定該 layer 並開啟物理矩陣 Player/Enemy↔{blockerLayerName}。");
            else
                BuildCompositeFromCells($"Map_Blockers ({blockerLayerName})", blockerCells, _blockerLayer);
        }
    }

    // cells 為子格座標；碰撞盒大小 = tileSize / walkSubdiv。
    // 效能：把「同一列連續的格」橫向合併成一條長 box 再餵給 CompositeCollider2D，
    // collider 數量從「牆格數」降到「列段數」（大地圖差幾百倍）。CompositeCollider2D
    // 本來就把相鄰 box 併成多邊形外框，故合併後的物理外形與 hit.normal 與逐格版完全一致。
    void BuildCompositeFromCells(string name, HashSet<long> cells, int layer)
    {
        float cellSize = MapCoords.FineSize(_map);
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.position = Vector3.zero;
        go.layer = layer;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        var composite = go.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Manual;

        // 依列(gy)分組，每列把 gx 排序後找「連續區段」併成一條 box。
        var rowsByY = new Dictionary<int, List<int>>();
        foreach (long k in cells)
        {
            Unkey(k, out int gx, out int gy);
            if (!rowsByY.TryGetValue(gy, out var xs)) { xs = new List<int>(); rowsByY[gy] = xs; }
            xs.Add(gx);
        }

        int boxCount = 0;
        foreach (var kv in rowsByY)
        {
            int gy = kv.Key;
            var xs = kv.Value;
            xs.Sort();
            int runStart = xs[0], prev = xs[0];
            for (int i = 1; i <= xs.Count; i++)
            {
                if (i < xs.Count && xs[i] == prev + 1) { prev = xs[i]; continue; }
                AddRunBox(go, runStart, prev, gy, cellSize);   // 收尾 [runStart..prev]
                boxCount++;
                if (i < xs.Count) { runStart = xs[i]; prev = xs[i]; }
            }
        }

        composite.GenerateGeometry();
        Debug.Log($"[MapLoader] {name}：{cells.Count} 子格 → {boxCount} 條合併 box。");
    }

    /// <summary>在 go 上加一條覆蓋同列 [x0..x1] 的 BoxCollider2D（usedByComposite）。</summary>
    void AddRunBox(GameObject go, int x0, int x1, int gy, float cellSize)
    {
        int count = x1 - x0 + 1;
        var box = go.AddComponent<BoxCollider2D>();
        box.usedByComposite = true;
        box.size = new Vector2(count * cellSize, cellSize);
        Vector2 c0 = MapCoords.FineCellCenter(x0, gy, _map);
        Vector2 c1 = MapCoords.FineCellCenter(x1, gy, _map);
        box.offset = (c0 + c1) * 0.5f;   // 第一格與最後一格中心的中點 = 此段中心
    }

    // ---- 傳送點特效（在每個 teleport 區域中心放一個持續循環的標記特效）----
    // 複用 VFX 系統的「Loop=1 / Duration=-1」（無限循環、外部管理生死）；掛進 MapRoot，換圖拆除時一併清掉。
    void BuildTeleportMarkers()
    {
        TeleportMarkerById.Clear();
        if (teleportVfxId <= 0) return;
        var trig = _map?.TriggerLayer;
        if (trig?.regions == null) return;

        if (_vfx == null) _vfx = FindObjectOfType<VfxManager>();
        if (_vfx == null)
        {
            Debug.LogWarning("[MapLoader] 有傳送點但場景找不到 VfxManager，傳送點特效略過。");
            return;
        }

        int n = 0;
        foreach (var r in trig.regions)
        {
            if (r.typeId != teleportTypeId) continue;
            if (!r.GetBool("showMarker", true)) continue;   // 傳送點勾掉「使用傳送點外型」→ 不生外型（預設顯示）
            // 外型位置＝錨點（＝踩踏區中心＝落點，三者同一個點）；沒設錨點的舊傳送點退回格子平均中心。
            if (!TeleportAnchor.TryCenter(r, _map, out Vector2 center)) continue;
            var inst = _vfx.Spawn(teleportVfxId, center, 0f);
            if (inst != null)
            {
                inst.transform.SetParent(_root, true);   // 掛進 MapRoot，換圖拆除時一併清掉
                if (!string.IsNullOrEmpty(r.id)) TeleportMarkerById[r.id] = inst.gameObject;   // 供 togglePortal 隱藏/恢復外型
                n++;
            }
        }
        if (n > 0) Debug.Log($"[MapLoader] 放置 {n} 個傳送點特效（VfxID {teleportVfxId}）。");
    }

    // ---- 落點查詢 ----
    bool TryGetRegionCenter(string typeId, out Vector2 center)
    {
        center = Vector2.zero;
        var trig = _map?.TriggerLayer;
        if (trig?.regions == null) return false;
        foreach (var r in trig.regions)
        {
            if (r.typeId != typeId) continue;
            if (RegionCenter(r, out center)) return true;
        }
        return false;
    }

    bool RegionCenter(TriggerRegion r, out Vector2 center)
    {
        center = Vector2.zero;
        if (r.cells == null || r.cells.Count == 0) return false;
        Vector2 sum = Vector2.zero;
        int n = 0;
        foreach (var c in r.cells)
        {
            if (c == null || c.Length < 2) continue;
            sum += MapCoords.CellCenter(c[0], c[1], _map);
            n++;
        }
        if (n == 0) return false;
        center = sum / n;
        return true;
    }

    // ---- 怪物出生點：讀 monsterSpawn trigger,在每一格用 MonsterSpawner 生一隻怪 ----
    void SpawnMonstersFromMap()
    {
        var trig = _map?.TriggerLayer;
        if (trig?.regions == null) return;

        MonsterSpawner spawner = null;
        int spawned = 0;

        // 關卡進度：本張地圖已清掉的出生點，本趟不再重生（RunProgress，換圖回來沿用）。
        int mapId = MapManager.Instance != null ? MapManager.Instance.CurrentMapId : -1;
        bool runActive = RunProgress.Exists && RunProgress.Instance.RunActive;

        foreach (var r in trig.regions)
        {
            if (r.typeId != monsterSpawnTypeId || r.cells == null || r.cells.Count == 0) continue;

            if (spawner == null)
            {
                spawner = FindObjectOfType<MonsterSpawner>();
                if (spawner == null)
                {
                    Debug.LogWarning("[MapLoader] 地圖有怪物出生點,但場景找不到 MonsterSpawner(EnemySpawner)。" +
                                     "請保留 EnemySpawner 物件(可把它的 AutoSpawn 取消勾選,改純地圖生怪)。");
                    return;
                }
            }

            // monsterId 可填單一 id（例 5），也可填多個以 '|' 分隔（例 5|7|9）＝從中隨機挑一個生（沿用專案慣例，同 SummonIds/scriptIds）。
            string idStr = r.GetString("monsterId");
            int[] monsterIds = ParseMonsterIds(idStr);
            if (monsterIds.Length == 0)
            {
                Debug.LogWarning($"[MapLoader] 怪物出生點「{r.name}」的 monsterId 無效:\"{idStr}\",略過。");
                continue;
            }

            string deathFlag = r.GetString("deathFlag");   // 出生點「死亡觸發旗標」；此區域每隻怪死都會寫，空＝不寫

            // 交給 MapMonsterRespawner 的兩種情況（可並用）：
            //   ‧ 重複產生：spawnInterval > 0＝每隔 N 秒生一波（一波＝每格各一隻）。
            //   ‧ 有條件：填了觸發鏈通用條件（條件旗標／初始停用…）＝條件不成立前不生（一次性出生點也走這條，成立時生一波就結束）。
            float interval = r.GetFloat("spawnInterval", 0f);
            // 「什麼時候才開始生」直接沿用觸發鏈的**通用條件欄位**（條件旗標／初始停用＋解鎖旗標／周目／道具／完成關卡數），
            // 出生點不另外開一套自己的條件欄位——兩套長得很像的條件並存只會讓人填錯（實際踩過）。
            // 只在「真的有填條件」時才交給 MapMonsterRespawner 逐幀判定，其餘 99% 的出生點維持原本的即時生成路徑。
            bool gated = HasChainCondition(r);
            WarnBadSpawnConditions(r);
            if (interval > 0f || gated)
            {
                var points = new List<Vector2>();
                // 一次性（interval<=0）仍要記 RunProgress『已清』，所以要帶 spawnKey；重複產生刻意留 null。
                var keys = interval > 0f ? null : new List<string>();
                foreach (var c in r.cells)
                {
                    if (c == null || c.Length < 2) continue;
                    points.Add(MapCoords.CellCenter(c[0], c[1], _map));
                    keys?.Add($"{r.id}#{c[0]},{c[1]}");
                }
                if (points.Count == 0) continue;

                if (_respawner == null)
                {
                    // 一定要掛在 MapRoot 下（換圖隨之銷毀）。掛到 MapLoader 自己身上的話不會被銷毀，
                    // 會變成「換圖後還在生上一張圖的怪」。_root 正常情況不會是 null（SpawnMonsters 在建圖後才呼叫）。
                    if (_root == null) { Debug.LogError("[MapLoader] MapRoot 還沒建好，重複產生/等旗標的出生點略過。"); continue; }
                    var go = new GameObject("MonsterRespawner");
                    go.transform.SetParent(_root, false);
                    _respawner = go.AddComponent<MapMonsterRespawner>();
                }
                int maxAlive = r.GetInt("maxAlive", 0);   // 留空/0 = 用保險預設（見 MapMonsterRespawner.DefaultMaxAlive）
                spawned += _respawner.Register(spawner, monsterIds, points, keys, deathFlag, gated ? r : null,
                                               interval, maxAlive, mapId, r.name);
                continue;
            }

            // 一次性、進圖就生（原行為）：每格生一隻，死了記進度、本趟換圖回來不再重生。
            foreach (var c in r.cells)
            {
                if (c == null || c.Length < 2) continue;
                // 本張地圖唯一的出生點 key（區域 id + 格座標）；用來記「這格的怪已清、本趟不再生」。
                string spawnKey = $"{r.id}#{c[0]},{c[1]}";
                if (runActive && RunProgress.Instance.IsSpawnKilled(mapId, spawnKey)) continue;   // 已清 → 不重生
                // 填多個 id 時用 spawnKey 做「穩定挑選」而非每次亂骰：同一格在同一趟關卡換圖來回不會突然變成另一種怪。
                int monsterId = monsterIds.Length == 1
                              ? monsterIds[0]
                              : monsterIds[(int)(StableHash(spawnKey) % (uint)monsterIds.Length)];
                spawner.SpawnMonster(monsterId, MapCoords.CellCenter(c[0], c[1], _map), deathFlag,
                                     MonsterFaction.Enemy, spawnKey);
                spawned++;
            }
        }

        if (spawned > 0) Debug.Log($"[MapLoader] 依地圖出生點生成 {spawned} 隻怪物。");
    }

    /// <summary>
    /// 這顆 trigger 有沒有填任何「會擋住它」的觸發鏈通用條件。
    /// 有填 → 出生點交給 <see cref="MapMonsterRespawner"/> 逐幀查 <see cref="TriggerChain.IsActive"/>（條件取消就暫停、恢復就繼續）；
    /// 沒填 → 走原本的即時生成路徑（也避免在 TriggerChain.Setup 之前就去查它的狀態，見 MapManager.PlaceAndSetup 的呼叫順序）。
    /// </summary>
    static bool HasChainCondition(TriggerRegion r)
    {
        if (r == null) return false;
        if (r.GetBool("startDisabled")) return true;   // 初始停用：要等鏈 Activate 才解鎖
        foreach (var k in ChainConditionKeys)
            if (r.GetString(k).Trim().Length > 0) return true;
        return false;
    }

    /// <summary>
    /// 對怪物出生點常見的兩種「填了卻不會如預期運作」的組合出聲警告。
    /// 這類欄位是**每種 trigger 都會顯示**的通用欄位，出生點又不是全部都吃得到，靜默無效最難查（實際踩過一次）。
    /// </summary>
    static void WarnBadSpawnConditions(TriggerRegion r)
    {
        // ① 初始停用 ＋ 條件旗標：語意互斥。初始停用要靠鏈 Activate 解鎖，但 Activate 會先查條件旗標、
        //    不成立就整條鏈中止 → EnableRegion 永遠跑不到，這顆出生點會變成「永遠不生」。
        if (r.GetBool("startDisabled") && r.GetString("requireFlag").Trim().Length > 0)
        {
            Debug.LogWarning($"[MapLoader] 怪物出生點「{r.name}」同時填了「初始停用」與「條件旗標」——這兩個語意互斥，結果是**永遠不會生怪**。" +
                             "要「按開關才開始／再按一次暫停」請只填條件旗標（取消勾選初始停用）；" +
                             "要「某事件後才開始、之後不再關」才用初始停用＋鏈解鎖。見 readme/TRIGGER_CHAIN.md §3.5。");
        }

        // ② 重複規則：出生點不看它（一次性語意由 RunProgress『已清』決定）。填了不會報錯也不會生效。
        if (r.GetString("repeat").Trim().Length > 0)
        {
            Debug.LogWarning($"[MapLoader] 怪物出生點「{r.name}」填了「重複規則」，但**出生點不看這欄**（不會有任何效果）。" +
                             "「殺掉後本趟不再重生」是 RunProgress 自動處理的；要控制「什麼時候才開始生」請用條件欄位。" +
                             "見 readme/TRIGGER_CHAIN.md §3.5。");
        }
    }

    // TriggerChain.RequirementMet 會判定的條件欄位（enableFlag 不列：它只是「解鎖時要寫的旗標名」，本身不擋人；
    // repeat/onBlocked 也不列：出生點不看它們，改由上面的 WarnBadSpawnConditions 出聲）。
    static readonly string[] ChainConditionKeys =
    {
        "requireFlag", "requireCycleMin", "requireCycleMax", "requireItem",
        "requireClearsMin", "requireClearsMax",
    };

    /// <summary>解析 monsterId 欄：單一 id（"5"）或 '|' 分隔的多個 id（"5|7|9"）。無效值略過，全無效回空陣列。</summary>
    static int[] ParseMonsterIds(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new int[0];
        var list = new List<int>();
        foreach (var part in raw.Split('|'))
        {
            string t = part.Trim();                       // CSV/參數值常帶空白，一律 Trim（見 readme/PROBLEMS F4）
            if (t.Length == 0) continue;
            if (int.TryParse(t, out int id)) list.Add(id);
            else Debug.LogWarning($"[MapLoader] 怪物出生點的 monsterId 有無效項:\"{t}\"，略過這一項。");
        }
        return list.ToArray();
    }

    /// <summary>字串穩定雜湊（FNV-1a）。不用 string.GetHashCode——那個不保證跨執行/跨平台一致。</summary>
    static uint StableHash(string s)
    {
        uint h = 2166136261u;
        for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
        return h;
    }

    // ---- 相機置中對齊地圖 ----
    void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 c = MapCoords.WorldCenter(_map);
        cam.transform.position = new Vector3(c.x, c.y, cam.transform.position.z);
        if (cam.orthographic)
            cam.orthographicSize = _map.height * _map.tileSize * 0.5f;
    }

    // ---- helpers ----
    static int ResolveLayer(string name, int fallback)
    {
        int idx = LayerMask.NameToLayer(name);
        if (idx < 0)
        {
            Debug.LogWarning($"[MapLoader] 找不到 layer「{name}」，改用 {fallback}。");
            return fallback;
        }
        return idx;
    }

    /// <summary>收集某 trigger 類型所有區域涵蓋的格（以 Key 編碼）。</summary>
    HashSet<long> CollectTriggerCells(string typeId)
    {
        var set = new HashSet<long>();
        var trig = _map?.TriggerLayer;
        if (string.IsNullOrEmpty(typeId) || trig?.regions == null) return set;
        foreach (var r in trig.regions)
        {
            if (r.typeId != typeId || r.cells == null) continue;
            foreach (var c in r.cells)
                if (c != null && c.Length >= 2) set.Add(Key(c[0], c[1]));
        }
        return set;
    }

    static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;
    static void Unkey(long k, out int x, out int y) { x = (int)(k >> 32); y = (int)(uint)k; }
}
