# 地圖載入器 — Unity 端接線步驟（首版）

主遊戲端讀取 `.dipanmap` 並重建關卡。程式已寫好,以下是你在 Unity 裡要做的事。

## 0. 我已經幫你做好的

- 加了 Newtonsoft 套件到 `Packages/manifest.json`(`com.unity.nuget.newtonsoft-json`)。
- 已同步一次:把 RedBridalGown 的 PNG＋`catalog.json`＋`RedBridalGown_01.dipanmap` 複製進 `Assets/StreamingAssets/MapAssets/`。
- 新程式都在 `Assets/Scripts/Map/`:`MapLoader.cs`(主元件)、`MapModel.cs`、`MapIO.cs`、`MapCoords.cs`、`MapSpriteLoader.cs`。
- 同步功能做成了 Unity 選單:**`Project Tools → Sync Map Assets`**(C#、免終端機,跑完自動 Refresh)。也保留 CLI 版 `Tools/sync_map_assets.sh` 供自動化用。

> **`Sync Map Assets` 一鍵做兩件事:**
> 1. **拉地圖**:從地圖編輯器 `DipanProj_MapEditor/Maps/<模組>/*.dipanmap` 複製進遊戲端 `GameAssets/Modules/<模組>/Maps/`。
>    (編輯器 Maps 下的子資料夾名 = 模組名,需對應 `GameAssets/Modules` 內既有模組;對不上會警告並略過,防打錯字。)
> 2. **推素材**:把 `GameAssets` 的 Environment/Tiles/Background PNG＋地圖 → `StreamingAssets/MapAssets/`,並生成 `catalog.json`。
>
> 之後每次在編輯器存完地圖、或加了新素材,點一下 **`Project Tools → Sync Map Assets`** 再 Play / 打包即可,不必再手動 copy 地圖。

## 1. 開啟 Unity → 等套件編譯

開啟專案,Unity 會自動抓 Newtonsoft 套件並編譯。Console 沒紅錯誤再往下。

## 2. 關掉舊測試場景物件(MainScene)

舊的測試牆與地板會跟新地圖衝突,先停用(不用刪):

- `Wall_Left` / `Wall_Right` / `Wall_Top` / `Wall_Bottom` → 取消勾選(停用)。它們是舊的 Environment 邊界,新地圖會自己生牆。
- `Grid`(底下的 `Ground` 舊 Tilemap)→ 停用。新地圖用背景圖。
- `EnemySpawner`(MonsterSpawner)→ **保留啟用**(它當生怪工廠,MapLoader 會呼叫它在地圖出生點生怪),但把它的 **`Auto Spawn` 取消勾選**——否則它會額外繞著自身位置亂生一堆怪。地圖的 `monsterSpawn` 出生點(每格生一隻對應 monsterId 的怪)會由 MapLoader 處理。

`MainActorSpawner`、`GameManagers`、`GroundEffectManager`、`VfxManager`、`Main Camera` 都**保留**。

## 3. 加入 MapLoader

1. 在 Hierarchy 建一個空物件,命名 `MapLoader`,Transform 歸零 `(0,0,0)`。
2. 加上 `Map Loader` 元件(Add Component → 搜尋 MapLoader)。
3. 確認欄位(預設值通常就對):
   - **Map Path** = `Modules/RedBridalGown/Maps/RedBridalGown_01.dipanmap`
   - **Environment Layer Name** = `Environment`
   - **Blocker Layer Name** = `Water`(本圖沒水塘,用不到)
   - 其餘開關保持預設(背景/地磚/物件/牆/出生點/相機都會自動處理)。

## 4. 設為初始場景

`File → Build Settings`,確認 `Scenes/MainScene` 在清單最上(已經是了)。按 **Play**。

## 預期結果

- 背景 `Stage_LivingRoom1` 鋪滿畫面(18×10),相機自動置中到 `(9, -5)`、size 5。
- 4 個家具(方桌、兩張圓凳、灶台)出現在正確位置與大小。
- 玩家生在出生點 `(3, -8)`。
- 走到牆邊/家具會被擋住;對牆和家具開槍會反彈/穿透(看武器配方)。
- Console 出現 `[MapLoader] 載入完成：RedBridalGown_01...`。

## 已知首版限制(非 bug,之後再處理)

- **家具永遠畫在玩家之上**:家具用編輯器的高 sortingOrder,玩家還沒納入 Y-sort,所以站在家具前也會被蓋住。動態角色的 Y-sort 是下一步。
- **可破壞家具**:目前家具是實心 Environment(擋路＋反彈),還不會被打爆。每個家具已是獨立 GameObject,之後加 `Destructible`(HP)元件 + 在 `PlayerController.HandleBulletHit` 對 Environment 上有 Destructible 的目標扣血即可,Destroy 後路自動開。
- **牆/水 = 可走層三態子格**(2026-06-25 起,取代舊的 environment trigger 牆模型):可走層改成**三態子格位元圖**,解析度 = 每個 tile 切 `walkSubdiv`×`walkSubdiv` 子格(新地圖預設 4×4,可細膩描邊)。每子格 `'0'`=可走、`'1'`=牆(擋＋反彈,Environment layer)、`'2'`=水/坑(只擋腳、子彈穿過,`Water` layer)。碰撞盒大小 = `tileSize / walkSubdiv`,由 `MapLoader.BuildCellColliders` 直接讀位元圖生成。牆/水**直接在「可走」工具裡塗**,不再需要 environment trigger。
  - **environment 牆 trigger 已徹底移除**(2026-06-25):舊的「環境/牆」trigger 類型已從編輯器(triggerTypes.json + TriggerType.cs 預設)、那顆「依不可走格建立牆 trigger」便利按鈕、以及遊戲端的 legacy 牆模型全部移除。牆/水改成單一可走層三態,不再有「bitmap ＋ trigger 兩層」的疊層。既有 RedBridalGown 地圖已用 `migrate_walksubdiv.py` 一次性無損轉檔(env trigger 的牆→`'1'`、不可走非牆→`'2'`),不需手動重做。
  - **編輯器**:在「可走」工具面板用三個筆刷(可走綠/牆紅/水藍)直接塗子格,並可選筆刷大小(1/2/4/8 子格)。見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)。
- **水塘/深坑**:用 `Blocker Layer Name` 指定的 layer(預設 `Water`)。新模型下「不可走但非 environment」自動就是它;要讓它生效,需到 `Project Settings → Physics 2D` 把 `Player↔Water`、`Enemy↔Water` 的碰撞矩陣打勾(沒設 layer 會略過並警告)。

## 故障排除

- **看不到圖/紫色方塊**:八成是 catalog 沒同步。重跑 `./Tools/sync_map_assets.sh RedBridalGown`。
- **家具大小不對**:確認沒去改 `MapSpriteLoader` 的 PPU(必須 256,對齊編輯器)。
- **子彈不反彈牆**:確認 `Environment` 是 layer 3、MapLoader 的 Environment Layer Name 填對。
