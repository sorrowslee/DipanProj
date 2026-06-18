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
/// - 牆 = walkable 不可走格（預設）→ Environment layer 實心碰撞（擋＋反彈子彈）；
///   不可走 ∩ bulletPass trigger（水塘/深坑）→ blocker layer（只擋腳、子彈飛過）。
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
    // 不可走格預設 = 牆(擋＋反彈)。只有被此 trigger 標記的不可走格才變「水塘/深坑」(擋腳、子彈飛過)。
    public string bulletPassTriggerTypeId = "bulletPass";
    public string playerSpawnTypeId = "playerSpawn";
    public string monsterSpawnTypeId = "monsterSpawn";
    public string teleportTypeId = "teleport";

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
        if (fitCameraToMap) FitCamera();

        Debug.Log($"[MapLoader] 載入完成：{_map.name}（{_map.width}×{_map.height}, module={_map.module}）");
        return true;
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
    void BuildObjects()
    {
        var layer = _map.GameLayer;
        if (layer?.objects == null || layer.objects.Count == 0) return;

        var objRoot = new GameObject("Objects");
        objRoot.transform.SetParent(_root, false);

        const int SortBase = 1000000, BandStep = 10000;
        const float SortScale = 100f;

        foreach (var inst in layer.objects)
        {
            var item = _catalog.Find(inst.assetId);
            var sprite = _sprites.GetWholeSprite(item, _map.tileSize);
            if (sprite == null) { Debug.LogWarning($"[MapLoader] 地上物找不到：{inst.assetId}"); continue; }

            string leaf = inst.assetId;
            int slash = leaf.LastIndexOf('/');
            if (slash >= 0) leaf = leaf.Substring(slash + 1);

            var go = new GameObject($"Obj_{leaf}");
            go.transform.SetParent(objRoot.transform, false);
            go.layer = _envLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortBase + inst.zOrder * BandStep + Mathf.RoundToInt(-inst.sortKey * SortScale);

            go.transform.position = new Vector3(inst.x, inst.y, 0f);
            go.transform.localScale = new Vector3(
                (inst.flipX ? -1f : 1f) * inst.scaleX,
                (inst.flipY ? -1f : 1f) * inst.scaleY, 1f);
            go.transform.rotation = Quaternion.Euler(0, 0, inst.rot);

            if (addObjectColliders)
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
            if (objectsDestructible && inst.hp >= 0)
            {
                var d = go.AddComponent<DestructibleObject>();
                float hp = inst.hp > 0 ? inst.hp : objectMaxHP;   // >0 用編輯器血量;==0 退回全域後備值
                d.Configure(hp, objectDestroyVfxId);
            }
        }
    }

    // ---- 牆 / 擋腳碰撞 ----
    // 核心原則：玩家/怪物「能不能走」一律以 walkable 層為準（= 編輯器可走疊加所見）。
    //   牆   = 不可走格(預設)            → Environment layer（擋腳＋反彈子彈）
    //   水塘 = 不可走格 ∩ bulletPass     → blocker layer（只擋腳，子彈飛過）
    void BuildCellColliders()
    {
        // bulletPass(水塘/深坑) 標記格
        var passCells = new HashSet<long>();
        var trigLayer = _map.TriggerLayer;
        if (trigLayer?.regions != null)
        {
            foreach (var r in trigLayer.regions)
            {
                if (r.typeId != bulletPassTriggerTypeId || r.cells == null) continue;
                foreach (var c in r.cells)
                    if (c != null && c.Length >= 2) passCells.Add(Key(c[0], c[1]));
            }
        }

        // walkable 不可走格子 → 預設牆；被 bulletPass 標記者改為水塘
        var wallCells = new HashSet<long>();      // 擋腳＋反彈
        var blockerCells = new HashSet<long>();   // 只擋腳
        var walk = _map.WalkableLayer;
        if (walk?.blocked != null)
        {
            for (int y = 0; y < walk.blocked.Count && y < _map.height; y++)
            {
                string row = walk.blocked[y];
                if (string.IsNullOrEmpty(row)) continue;
                for (int x = 0; x < row.Length && x < _map.width; x++)
                {
                    if (row[x] != '1') continue;             // '1' = 不可走
                    long k = Key(x, y);
                    if (passCells.Contains(k)) blockerCells.Add(k);
                    else wallCells.Add(k);
                }
            }
        }

        if (buildWalls && wallCells.Count > 0)
            BuildCompositeFromCells("Map_Walls (Environment)", wallCells, _envLayer);

        if (buildBlockers && blockerCells.Count > 0)
        {
            if (_blockerLayer < 0)
                Debug.LogWarning($"[MapLoader] 有 {blockerCells.Count} 格「不可走但非環境」(水塘/深坑)，" +
                    $"但找不到 layer「{blockerLayerName}」，已略過。要啟用請設定該 layer 並開啟物理矩陣 Player/Enemy↔{blockerLayerName}。");
            else
                BuildCompositeFromCells($"Map_Blockers ({blockerLayerName})", blockerCells, _blockerLayer);
        }
    }

    void BuildCompositeFromCells(string name, HashSet<long> cells, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.position = Vector3.zero;
        go.layer = layer;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        var composite = go.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Manual;

        foreach (long k in cells)
        {
            Unkey(k, out int gx, out int gy);
            var box = go.AddComponent<BoxCollider2D>();
            box.usedByComposite = true;
            box.size = new Vector2(_map.tileSize, _map.tileSize);
            box.offset = MapCoords.CellCenter(gx, gy, _map);
        }

        composite.GenerateGeometry();
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

    static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;
    static void Unkey(long k, out int x, out int y) { x = (int)(k >> 32); y = (int)(uint)k; }
}
