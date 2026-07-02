using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
using Dipan.MapRuntime;

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
    public bool buildTiles = true;
    public bool buildObjects = true;
    public bool addObjectColliders = true;
    public bool buildWalls = true;
    public bool buildBlockers = true;
    public bool spawnMonsters = true;
    public bool buildTeleportMarkers = true;
    public bool fitCameraToMap = true;
    // 預設關閉：由 MapManager 驅動。打開 = 不靠 MapManager，Awake 直接建 mapPath（建圖測試用，不生玩家）。
    public bool loadOnAwake = false;

    [Header("地上物碰撞框微調（1=貼合不透明像素）")]
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
        if (buildTiles) BuildTiles();
        if (buildObjects) BuildObjects();
        if (buildWalls || buildBlockers) BuildCellColliders();
        if (buildTeleportMarkers) BuildTeleportMarkers();
        BuildSceneFx();
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

        if (buildTiles) BuildTiles();
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
        if (fitCameraToMap) FitCamera();   // MapManager 會關掉此旗標；保留以相容 loadOnAwake

        LastLoadOk = true;
        onProgress?.Invoke(1f);
        Debug.Log($"[MapLoader] 載入完成（分幀）：{_map.name}（{_map.width}×{_map.height}, module={_map.module}）");
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
            return RegionCenter(r, out center);
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
            Destroy(_root.gameObject);
            _root = null;
        }
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

    // ---- 地磚（本圖為空，但完整支援）----
    void BuildTiles()
    {
        var layer = _map.GameLayer;
        if (layer?.tiles == null || layer.tiles.Count == 0) return;

        var gridGO = new GameObject("Tiles_Grid");
        gridGO.transform.SetParent(_root, false);
        var grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(_map.tileSize, _map.tileSize, 0f);
        float bottom = _map.origin.y - _map.height * _map.tileSize;
        grid.transform.position = new Vector3(_map.origin.x, bottom, 0f);

        var tmGO = new GameObject("Tilemap");
        tmGO.transform.SetParent(gridGO.transform, false);
        var tilemap = tmGO.AddComponent<Tilemap>();
        var renderer = tmGO.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 0;

        var tileCache = new Dictionary<string, Tile>();
        foreach (var t in layer.tiles)
        {
            if (string.IsNullOrEmpty(t.tileId)) continue;
            if (!tileCache.TryGetValue(t.tileId, out var tile))
            {
                var sprite = _sprites.ResolveTileSprite(t.tileId, _catalog, _map.tileSize);
                if (sprite == null) { Debug.LogWarning($"[MapLoader] tile sprite 找不到：{t.tileId}"); continue; }
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                tileCache[t.tileId] = tile;
            }
            tilemap.SetTile(MapCoords.ToTilemapCell(t.x, t.y, _map.height), tile);
        }
    }

    // ---- 地上物 ----
    const int SortBase = 1000000, BandStep = 10000;
    const float SortScale = 100f;

    void BuildObjects()
    {
        var layer = _map.GameLayer;
        if (layer?.objects == null || layer.objects.Count == 0) return;

        var objRoot = new GameObject("Objects");
        objRoot.transform.SetParent(_root, false);

        foreach (var inst in layer.objects) BuildOneObject(inst, objRoot.transform);
    }

    /// <summary>分幀建地上物：每 perFrame 個 yield 一次並回報進度（0~1）。建構內容與 BuildObjects 完全相同。</summary>
    IEnumerator BuildObjectsRoutine(int perFrame, System.Action<float> onProgress)
    {
        var layer = _map.GameLayer;
        if (layer?.objects == null || layer.objects.Count == 0) { onProgress?.Invoke(1f); yield break; }

        var objRoot = new GameObject("Objects");
        objRoot.transform.SetParent(_root, false);

        int total = layer.objects.Count, done = 0;
        foreach (var inst in layer.objects)
        {
            BuildOneObject(inst, objRoot.transform);
            done++;
            if (done % perFrame == 0)
            {
                onProgress?.Invoke((float)done / total);
                yield return null;
            }
        }
        onProgress?.Invoke(1f);
    }

    /// <summary>建一個地上物（載圖、SpriteRenderer、動畫、碰撞框、可破壞）。sync 與分幀版共用。</summary>
    void BuildOneObject(ObjectInstance inst, Transform objRoot)
    {
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
        sr.sortingOrder = SortBase + inst.zOrder * BandStep + Mathf.RoundToInt(-inst.sortKey * SortScale);

        // 動畫地上物：載入幀序列、掛上原地循環播放元件（速度 = 該實例 animFps）。
        // 碰撞框/血量/可破壞仍以第一幀建立（下方），動畫只換顯示用 sprite。
        if (item != null && item.IsAnimated)
        {
            var frames = _sprites.GetAnimationFrames(item, _map.tileSize);
            if (frames != null && frames.Length >= 2)
                go.AddComponent<AnimatedMapObject>().Initialize(sr, frames, inst.animFps, inst.pingPong);
            else
                Debug.LogWarning($"[MapLoader] 動畫地上物「{inst.assetId}」幀載入失敗，退回靜態第一幀。");
        }

        go.transform.position = new Vector3(inst.x, inst.y, 0f);
        go.transform.localScale = new Vector3(
            (inst.flipX ? -1f : 1f) * inst.scaleX,
            (inst.flipY ? -1f : 1f) * inst.scaleY, 1f);
        go.transform.rotation = Quaternion.Euler(0, 0, inst.rot);

        // walkable = true：不設碰撞、不擋路（走不走由地圖可走層判定；例：木板/地毯）。因此也不掛可破壞。
        if (addObjectColliders && !inst.walkable)
        {
            var box = _sprites.GetAlphaLocalBox(item, _map.tileSize);
            var col = go.AddComponent<BoxCollider2D>();
            if (box.ok)
            {
                col.size = box.size * objectColliderScale;
                col.offset = box.offset;
            }
            else
            {
                col.size = sprite.bounds.size;   // 後備：整張圖外框
                col.offset = Vector2.zero;
            }
        }

        // hp < 0（例如 -1）= 不可摧毀:不掛 DestructibleObject,但上面的碰撞框照常 → 等於一般牆壁(擋＋反彈)。
        // walkable 物件沒有碰撞、打不到，也不掛可破壞。
        if (objectsDestructible && !inst.walkable && inst.hp >= 0)
        {
            var d = go.AddComponent<DestructibleObject>();
            float hp = inst.hp > 0 ? inst.hp : objectMaxHP;   // >0 用編輯器血量;==0 退回全域後備值
            d.Configure(hp, objectDestroyVfxId);
        }
    }

    // ---- 場景特效（可放置的粒子特效，煙/火/冰/毒…；由編輯器 map.sceneFx 放置，SceneFxTable 定義外觀）----
    void BuildSceneFx()
    {
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
            if (!RegionCenter(r, out Vector2 center)) continue;
            var inst = _vfx.Spawn(teleportVfxId, center, 0f);
            if (inst != null)
            {
                inst.transform.SetParent(_root, true);   // 掛進 MapRoot，換圖拆除時一併清掉
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

            string idStr = r.GetString("monsterId");
            if (!int.TryParse(idStr, out int monsterId))
            {
                Debug.LogWarning($"[MapLoader] 怪物出生點「{r.name}」的 monsterId 無效:\"{idStr}\",略過。");
                continue;
            }

            foreach (var c in r.cells)
            {
                if (c == null || c.Length < 2) continue;
                spawner.SpawnMonster(monsterId, MapCoords.CellCenter(c[0], c[1], _map));
                spawned++;
            }
        }

        if (spawned > 0) Debug.Log($"[MapLoader] 依地圖出生點生成 {spawned} 隻怪物。");
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
