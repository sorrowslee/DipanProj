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

## 地上物碰撞：貼合圖形（2026-08-19）

地上物擋路的範圍**不是**畫在可走層上，而是各自掛在自己 GameObject 上的碰撞（可走層只管牆/水與 A\* 尋徑格）。
2026-08-19 起這顆碰撞從「整張圖不透明像素的**外接矩形**」改成**逐格貼合圖形**——圖上透明的地方就是可以走。
（舊做法只能縮框不能挖洞，斜擺的屏風、燈籠桿兩側、椅腳之間怎麼切素材都救不了；來龍去脈見 [PROBLEMS.md](PROBLEMS.md) **B9**。）

### 怎麼運作

1. **烘焙**：`Project Tools → Sync Map Assets` 掃每張 Environment 素材，切成子格產生「佔位遮罩」，寫進 `catalog.json` 的 `footprint` 欄。
   動畫地上物取第一幀（與 sprite／舊碰撞框的取樣來源一致）。同步完的 Console 摘要會印 `佔位遮罩：N/M 筆地上物已烘`。
2. **遊戲端**：`MapLoader` 依遮罩把「同一列連續的擋路格」併成一條 box，全部 `usedByComposite` 交給 `CompositeCollider2D` 合併成單一外框
   —— **與牆的做法完全一樣**（見 `BuildCompositeFromCells`）。用 Composite 是必要的：一堆裸方框之間會留下內部接縫，圓形的玩家貼著滑動時會卡住、子彈反彈方向也會亂跳。
3. **退路**：catalog 沒有遮罩時（例如用 `Tools/sync_map_assets.sh` 這支 shell 版同步的，它不會烘），遊戲端**當場掃一次**，結果一樣、只是載入慢一點。

⚠ **兩條路必須是字面上相同的計算，這件事要刻意維持**：`GetFootprint` 一律「先取得**烘焙解析度**（subdiv 8）的那一份——烘好的直接用、沒有就當場掃——再降取樣到要用的解析度」。
**不可以改成「拿不到烘焙就直接在目標解析度掃」**：降取樣是 OR（4 顆子格有 1 顆實心就算擋），而直接掃是整格算覆蓋率，
同一張圖 subdiv 4 兩者的實心格數差 10~38%（教徒 18 vs 13）。混用的後果是「有烘過的機器和沒烘過的機器，同一個物件擋路範圍不一樣」，而且完全靜默。

遮罩與地圖無關，所以能預先烘：素材一律以 `PPU = 256/tileSize` 載入，一張 w 像素寬的圖恆為 `w/256` 「格」寬，**與該地圖的 tileSize 無關**。

### MapLoader 上可以調的

| 欄位 | 預設 | 說明 |
|---|---|---|
| 子格解析度（`objectColliderSubdiv`） | **8** | 碰撞格大小 = `tileSize / 這個值`。愈大愈貼合、碰撞條數也愈多。遮罩一律烘在 8，填 ≤8 會自動降取樣，**改這個不必重跑同步**（同步時間與這個值完全無關）。⚠ **只有 1/2/4/8 有意義**，其餘會被 `SnapSubdiv` 往下收斂（填 6 等於 4）——非因數拿不到降取樣，而且 `256/subdiv` 是整數除法、與世界尺寸的 `tileSize/subdiv` 對不起來，形狀會逐欄往右下漂。 |
| 實心判定門檻（`objectSolidFillThreshold`） | 0.9 | 遮罩「填滿率」高於此值 = 這張圖本來就是實心方塊，改用單一方框（省一顆 Composite，形狀幾乎無差）。設 1 = 一律逐格貼合；設 0 = 一律單框（＝回到舊行為）。⚠ **填滿率會隨解析度變**：同一個書架 subdiv 4 是 1.00（→單框，畫面上就是一大塊方形）、subdiv 8 掉到 0.82（→逐格貼合）。走單框的物件 subdiv 4 有 15 個、subdiv 8 只剩 3 個——所以「換解析度」的視覺差異有一半其實來自這條捷徑翻面，不只是格子變細。 |
| 整體內縮（`objectColliderScale`） | 1 | 以物件中心等比縮整個碰撞形狀。調小 = 整圈往內收、玩家更好走。size 與 offset 同乘，相鄰段仍相接、不會裂出縫。 |

實測填滿率（subdiv 4）：書架 100%、椅子 92%（→單框）；書架3 86%、教徒 87%、燈籠 71%、書桌 69%、屏風 67%（→貼合，每個約 5~8 條 box）。

**解析度的實際代價**（全 16 張地圖的碰撞條總數）：

| | subdiv 4 | subdiv 8 |
|---|---|---|
| 一般房間（各張） | 30~63 條 | 100~137 條 |
| 邪佛廣場 | 2047 條 | 3511 條 |
| 全部合計 | 2435 條 | 4412 條 |

一般房間那個量級對 Box2D 完全無感（**牆本來就 324 條在跑**）。唯一有量的是邪佛廣場，而且 100% 來自那
**288 個共用同一張圖的教徒**——那張圖若嫌重，與其調解析度，不如直接把教徒勾「可穿越」（碰撞歸零），比較精準。
**烘焙時間與這個值完全無關**：遮罩一律烘在 subdiv 8，遊戲端用 4 時是多做一步降取樣。

### 看得到碰撞：P → C 疊層

按 **P** 開效能面板，再按 **C**（或點面板上的「碰撞範圍(C)」）就會把**實際生成的碰撞形狀**畫在畫面上：

| 顏色 | 是什麼 |
|---|---|
| 綠 | 地上物（家具、屏風…）——**不在可走層上** |
| 紅 | 牆（可走層塗 `'1'`） |
| 藍 | 水/坑（可走層塗 `'2'`，只擋腳） |
| 黃 | 玩家（圓 ＋ 腳底十字） |
| 橘 | 怪物 |

**綠色與紅色是兩套獨立系統**——這正是最容易誤會的地方：在可走層塗色改不動綠色的部分。

刻意畫「實際 Collider」而不是「佔位遮罩」：遮罩只是輸入，中間還隔著降取樣、實心判定、run 合併、畫布夾取、物件縮放翻轉。
畫遮罩只能證明遮罩對，畫 Collider 才能回答「我到底為什麼走不過去」。
實作是一台 `cullingMask = 0` 的相機排在主相機之後，用 GL 即時模式在 `OnPostRender` 畫，所以**暗場景/幽暗氛圍不會把它壓暗**；
關閉時相機直接 disable ⇒ 零成本。程式在 `Scripts/Diagnostics/CollisionDebugOverlay.cs`。

> ⚠ 用這個疊層時會看到：**玩家的碰撞圓在胸口高度而不是腳底**（圓心在腳底上方約 0.78 格）。
> 這是既有設定、與本節無關，但它會讓玩家「比物件底邊再低 1.18 格」才走得過去。見 [TODO.md](TODO.md)。

### 改這一塊時務必注意

- **碰撞一定要建在地上物本身，不要開子物件**：命中判定有 `GetComponent<IDamageable>()` 與 `GetComponentInParent<IDamageable>()` 兩種寫法並存（見 `PlayerController`），掛到子物件上會讓前者找不到 `DestructibleObject`，症狀是「這個東西打不壞」。
- **「靠旗標中途現身」的物件要整組開關**：一個物件現在可能有很多顆碰撞（Composite ＋ 一堆 box），`MapObjectRevealer` 收的是 `Collider2D[]`，只開關其中一顆會出現「東西還沒現身、路卻已經被擋住」。
- **可破壞物不受影響**：碰撞一律各自掛在自己的物件上，打壞就是 `Destroy(gameObject)`，與原本行為一致，不會牽動其他物件。
- **「可走」「可穿越」兩個勾選行為不變**：勾了就完全不生碰撞（也不掛可破壞）。
- **建碰撞條時要把最後一欄／最下一列夾回畫布邊界**：`cols = ceil(圖寬/格寬)`，最後一格是被截短的，當成完整格會讓右邊與下邊多出隱形牆（實測 91 張素材有 27 張中招，屏風 0.227 格、書架 0.254 格），而且形狀左右不對稱（左上精確、右下外擴）——正是這套機制要修掉的那種東西。

### 副作用（好的）

- 子彈會從屏風的縫穿過去、打空白處不算命中。
- 怪物的 A\* 尋徑格靠「可走層位元圖 ∪ 物理查詢」聯集算，碰撞變準之後尋徑也跟著變準。

## 故障排除

- **看不到圖/紫色方塊**:八成是 catalog 沒同步。重跑 `./Tools/sync_map_assets.sh RedBridalGown`。
- **家具大小不對**:確認沒去改 `MapSpriteLoader` 的 PPU(必須 256,對齊編輯器)。
- **子彈不反彈牆**:確認 `Environment` 是 layer 3、MapLoader 的 Environment Layer Name 填對。
