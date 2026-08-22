# 踩坑記錄與解法 (Problems & Solutions)

> 返回 [文件總覽](README.md)
>
> **給接手的人/AI:**
> 1. **第一次看這個專案的文件時,先把這份從頭看一遍**——很多「看起來很怪」的問題這裡已經有答案,別重複踩。
> 2. **以後每遇到一個新坑,就在這裡新增一則**,格式:`症狀 → 原因 → 解法`。寫清楚當下的現象,未來的人才搜得到。

每則用同一個格式:**症狀 / 原因 / 解法**。


## 分類索引（找特定編號一律用「搜尋」，例如搜 `E11`）

| 代號 | 分類 | 編號範圍與備註 |
|---|---|---|
| A | 打包與部署 (Build & Deploy) | A1~A10（A3、A9 已淘汰，原文封存、存根在原位） |
| B | 地圖載入 (Map Loader) | B1~B14 |
| C | 地圖編輯器 / 素材同步 | C1~C10（⚠ C6/C7 排在 C1 前面） |
| D | 存檔 / 常駐單例 (Save & Persistent Singletons) | D1~D22 |
| E | 效能 / 顯示 (Performance & Display) | E1~E20 |
| F | 戰鬥 / 傷害 (Combat) | F1~F17（⚠ G 章整段插在 F3 與 F4 之間） |
| G | 角色圖像 / 序列化 (Character Visuals & Serialization) | G1~G5（位置在 F3 之後） |
| H | 流程 / 存讀檔 (Game Flow & Save UI) | H1 |
| I | 開發環境 / 工具（Cowork 橋接器） | I1~I9 |
| J | 螢幕特效 / 進場過場 (Screen FX) | J1~J4 |
| K | 互動 / 拾取 (Interaction & Pickup) | K1~K2 |

> ⚠ 編號**不保證依閱讀順序遞增**（歷史造成，維持現狀）。新增條目：放進所屬分類、編號接該分類目前最大號；**永不重編號、永不重用舊編號**——全專案文件與 PROGRESS 大量引用這些編號。條目淘汰時整則原文搬 [archive/PROBLEMS-archive.md](archive/PROBLEMS-archive.md) 並在原位留存根（規則見 [DOCS_GUIDE.md](DOCS_GUIDE.md)）。

---

## A. 打包與部署 (Build & Deploy)

### A1. Windows 版打包出來 `_Data` 缺核心資料(開遊戲跳 `Data folder not found`)
- **症狀**:`Build and Deploy` 顯示成功、`errors=0`,但 `Builds/Windows_Test/DipanProject_Data` 裡**只有 `Managed / Plugins / Resources / StreamingAssets / app.info / boot.config`**,**沒有 `globalgamemanagers` / `data.unity3d` / `level0` / `resources.assets` / `sharedassets0.assets`**。Windows 端開遊戲跳「Data folder not found」。
- **原因**:**增量打包沿用了舊的不完整資料**。某台第一次打包失敗(例如當時 Windows 模組沒裝好),留下壞的 `_Data` 與被汙染的增量快取 `Library/Bee`;之後 Unity 判定「只有 script 變」→ 做 **script-only / 增量打包,不重建 player data**,一直沿用那份壞資料。log 特徵:`player data was not rebuilt`、`Do a clean build`、`Run script only build`、`2 items updated`。
- **解法**:做一次 **clean build**。手動:關 Unity → 刪 `DipanProj_Main/Builds/Windows_Test` 與 `DipanProj_Main/Library/Bee` → 重 build。**已自動防呆**:`BuildScript` 現在於正常打包後檢查 `_Data`,缺核心資料就**自動清輸出 + `BuildOptions.CleanBuildCache` 重建一次**。

### A2. `Build target 'StandaloneWindows64' not supported`
- **症狀**:打包 Windows 版時 Console/Editor.log 出現此例外(常在 Postprocess 階段);或 `IsBuildTargetSupported` 為 false。
- **原因**:這台 Mac 沒裝(或裝不完整)**Windows Build Support (Mono)** 模組,且必須對「正在開這專案的那個 Unity 版本」安裝。
- **解法**:Unity Hub → Installs → 對 `2022.3.62f3` Add Modules → 勾 **Windows Build Support (Mono)** → **完全關閉再重開** Unity。`BuildScript` 現在會先檢查,沒裝就擋下並提示。

### A3. 部署 `git push` 失敗 / `fatal: not a git repository`（⚠️ 已淘汰情境，原文已封存）
> **2026-07-03 起部署改用 itch.io + butler，build 不再進 git，本坑不再發生**（新流程見 [DEPLOY.md](DEPLOY.md)）。原文照錄於 [archive/PROBLEMS-archive.md](archive/PROBLEMS-archive.md)。編號 A3 保留、永不重用。

### A4. Windows 端「檔案都在、還是跳 Data folder not found」
- **症狀**:`exe` 與 `DipanProject_Data` 看起來都在,仍報錯。
- **原因**:(a) `exe` 與 `_Data` **不在同一層**(解壓多包了一層、或被分到不同層);或 (b) `_Data` 其實不完整(見 A1)。
- **解法**:確保 `DipanProject.exe` 與 `DipanProject_Data/` **平行同層**。用 zip 傳時:Mac 端進資料夾「裡面」全選再壓;Windows 端用 **7-Zip 解到短路徑**(內建解壓器對深路徑/巨量小檔會靜默漏檔)。

### A5. Console 中文變成 `??????`
- **症狀**:`Script Output` 那行的中文顯示成一堆 `?`。
- **原因**:`BuildScript` 用 Process 讀腳本輸出時沒指定編碼(預設非 UTF-8)。
- **解法**:已修——`ProcessStartInfo.StandardOutputEncoding/StandardErrorEncoding = UTF8`。

### A6. 抓不到「這次打包」的 Editor.log
- **症狀**:`grep ~/Library/Logs/Unity/Editor.log` 找不到 build 內容,或內容是別的專案(例如 `DipanProj_MapEditor`)。
- **原因**:`Editor.log` 只留**最近一個 Unity 視窗**的紀錄,會被其他專案/關閉動作覆蓋。
- **解法**:在「正確那個 Unity 視窗」用 Console `⋮ → Open Editor Log` 看這次打包紀錄;若要乾淨完整的 log,可暫時用批次模式 `Unity -batchmode -quit -projectPath <主專案> -executeMethod BuildScript.BuildAndDeploy -logFile <輸出檔>`(注意這會跑完整部署)。

### A7. 終端機貼上多行指令報 `no such file or directory`(路徑明明存在)
- **症狀**:貼上含 `\` 換行或從聊天複製的指令,zsh 把「路徑+參數」當成一個檔名。
- **原因**:複製時混入**不換行空格(non-breaking space)**或行尾 `\` 被破壞。
- **解法**:用**單行指令**,或寫成 `.sh` 檔執行;最保險是「`bash ` 後把檔案**拖進終端機**」讓系統補正確路徑。

### A8. 批次模式 log 出現 licensing 紅字(`Access token is unavailable` / `Pro License: NO`)
- **症狀**:批次模式(`-batchmode`)打包的 log 開頭有授權相關訊息。
- **原因**:批次模式 + Personal 授權的**正常現象**,與打包成敗無關。
- **解法**:忽略。真正的失敗看 BuildStep 與 `_Data` 是否完整。

### A9. `git push` 被拒：`File ... exceeds GitHub's 100 MB limit`（⚠️ 已淘汰情境，原文已封存）
> **2026-07-03 起部署改用 itch.io + butler，build 產物不再進 git，本坑不再發生**（見 [DEPLOY.md](DEPLOY.md)）。原文照錄於 [archive/PROBLEMS-archive.md](archive/PROBLEMS-archive.md)——其中「壓縮 build 貼圖」（Max Size／Crunch、`Resources.Load` 吃匯入設定而 `StreamingAssets` 不吃）仍是縮小 build 體積的有效一般參考。編號 A9 保留、永不重用。

### A10. build 開機直接播漫畫、墜落後全黑（或反過來：完全看不到開場）— 場景順序
> **2026-07-03 更新**：加了「標題流程」後，開機**場景 0 改成 `MainScene`**（不再是 Intro）。以下依新設計。
- **症狀**:
  - **開機直接播開場漫畫、墜落動畫結束後一片黑**（不是從標題開始）→ Intro 被排成場景 0 了。
  - 或反過來：**新建遊戲時看不到開場** → Intro 根本沒包進 build。
- **原因**:build 的**第 0 個場景 = 開機載入的場景**。標題流程要求開機停在 `MainScene`（由 `GameFlowManager` 顯示標題），Intro 只在「新建遊戲」時才被載入。若 Intro 排第 0：開機直接跑 Intro 的漫畫→墜落，墜落後 `LoadScene("MainScene")`，但 `GameFlowBootstrap` 開機已把 `MapManager.SuppressAutoStart` 設 true、又沒經過「新建」流程去解除它 → MainScene 的 MapManager 不啟動 → **全黑**（同 H1 的根因，因開機場景錯而重現）。
- **解法**:`BuildScript.cs` 的 `options.scenes` 要含兩個、且 **`MainScene` 排第 0、`Intro` 第二**：`{ "Assets/Scenes/MainScene.unity", "Assets/Scenes/Intro.unity" }`（`BuildWindows` 與 `BuildMacLocal` 兩個方法都要一致）。Intro→MainScene 是用**場景名稱**載入（非 build index），所以順序不影響開場鏈的接續。

---

## B. 地圖載入 (Map Loader)

### B1. 編輯器顯示可走、遊戲卻走不過去
- **症狀**:編輯器可走疊加是綠的,遊戲裡角色卻被擋。
- **原因**:早期版本用 environment trigger 當「玩家阻擋」來源,與可走層不同步就會打架。
- **解法**:已改為**玩家能不能走一律以可走層為準**。**牆/水的判定**(2026-06-25 更新):可走層改成**三態子格**——`'0'` 可走 / `'1'` 牆(擋＋反彈子彈) / `'2'` 水/坑(擋腳、子彈穿過),直接在編輯器「可走」工具塗。舊的 environment 牆 trigger 已徹底移除(不再有 bitmap＋trigger 兩層不同步的問題)。見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)、[MapEditor_DESIGN.md](MapEditor_DESIGN.md)。

### B2. 雷射 / 火焰噴射器打不爆地上物
- **症狀**:子彈能破壞家具,雷射類不行。
- **原因**:彈道系統的 `LaserBeam` 原本只回報「敵人」命中,把 Environment 當不可破壞的牆,不回報傷害。
- **解法**:`LaserBeam` 新增 `OnBeamEnvironmentTick` 環境命中回呼;傷害統一走 `IDamageable`。見 [DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md)。

### B3. 改了地圖/素材,遊戲沒更新
- **症狀**:在編輯器存了地圖或加了素材,主遊戲跑起來沒變。
- **原因**:主遊戲 runtime 讀的是 `StreamingAssets/MapAssets`,不是編輯器專案;沒同步就不會更新。
- **解法**:`Project Tools → Sync Map Assets`(會從編輯器拉地圖 + 推素材進 StreamingAssets)。見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)。

### B4. 連鎖閃電打地上物卻打不壞(同類:新武器只搜 EnemyLayer 都會中這個)
- **症狀**:連鎖閃電碰到可破壞家具,家具不會壞;閃電把家具當牆擋住、不結算傷害。
- **原因**:`ShootChain` 的目標搜尋只用 `EnemyLayer`,而可破壞地上物在 **Environment 層**,所以根本不會被列為連鎖目標 → 碰到只當牆停住。**任何新武器若自己做目標搜尋、只搜 EnemyLayer,都會犯一樣的錯**(對照規範:任何能造成傷害的武器都要能破壞地上物,見 [DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md))。
- **解法**:搜尋遮罩改成 `EnemyLayer | EnvLayer`,再用 **`IDamageable` 過濾**(怪物與可破壞家具都實作它,純牆沒有 → 自動排除、不浪費跳躍)。傷害一律走 `ApplyDamage` → `IDamageable`。已修(`FindNearestDamageable` + 首段擋路家具當首節點)。

### B5. 命中迸發子武器(SubWeaponOnHit):打到牆壁不會生出子武器,打怪/家具卻會
- **症狀**:蜂巢彈打到怪或家具會迸出飛劍,打到**牆壁**卻沒有(看起來沒生出來)。
- **原因**:子武器生成在「命中點」=牆面**表面上**,而每顆子彈一生成會做一次 `CheckSpawnOverlap`(處理「起點卡在 collider 內」)。子武器生在牆面內 → 立刻判定撞到牆 → 不穿透就**當場銷毀**。牆是實心永遠在,所以必中;家具會被母彈炸掉、怪是小圓,子武器剛好閃過或目標已消失,才偶爾正常 → 造成「打牆不會、打怪/家具會」的不一致。**任何「在命中點生成新子彈」的機制都會犯這個雷**。
- **解法**:把子武器生成點**沿命中面法線往外推**一段(至少蓋過子武器自己的判定半徑),生在牆面外的空地就不會被自己的重疊檢查瞬殺。已修(`TryTriggerSubWeapon` 內 `spawnPos + baseDir * max(0.35, subRadius+0.2)`)。同理拋物線/雷射反彈也都用 `point + normal * 偏移` 避開起點重疊。

### B6. 地圖一放大,進這張圖要等數十秒(進場凍住)
- **症狀**:把某張地圖在編輯器放大很多後,進入該圖要等幾十秒才動;小地圖正常。
- **原因**:牆碰撞 `MapLoader.BuildCompositeFromCells` 原本**一個牆「子格」就 `AddComponent<BoxCollider2D>()` 一次**,全掛同一物件再 `CompositeCollider2D.GenerateGeometry()`。子格數 = `width × height × walkSubdiv²`,放大後爆量;且**放大時新增的整片空白區域常被預設塗成牆(`'1'`)**,等於整張圖幾乎全是牆。實測 90×50、subdiv 4 的圖 → 72,000 子格、其中 65,856 格是牆 → 對同一物件連加 6.5 萬個 collider 元件 + 6.5 萬格幾何合併,就是那數十秒。成本與牆格數成正比。
- **解法**:兩條一起。① **資料面**:別把放大出來、玩家到不了的整片區域塗成牆,只描可走區邊界一圈即可。② **程式面**:`BuildCompositeFromCells` 改成**橫向 run-length 合併**——同一列連續的牆格併成一條長 box 再餵 composite。因為 `CompositeCollider2D`(geometryType=Polygons)本來就把相鄰 box 併成同一塊外框,**合併後的物理外形與 `hit.normal`(反彈)與逐格版完全一致**,且專案沒有任何程式引用個別牆 collider,所以零行為變化。實測同圖 box 數 **65,856 → 324(約 203 倍)**。(新增 `AddRunBox` 輔助方法;build 時印 `N 子格 → M 條合併 box` 方便核對。)
- **注意**:此優化只解決「建碰撞」的卡頓,**不縮減底圖過大的資源載入**(背景大圖、家具逐張同步解碼仍是另一塊,見資源載入分析)。

---

### B7. 新的接觸/範圍傷害用 OverlapCircle 找目標，貼身重疊時漏抓（且不對稱：A 打得到 B、B 打不到 A）
- **症狀**：加陣營制後，敵人接觸傷害打得到玩家召喚的友軍，**友軍撞上敵怪卻打不死牠**（明明看起來重疊了）。兩邊用同一支 `EnemyContactDamage`、邏輯對稱，卻只有一方生效。
- **原因**：改用 `Physics2D.OverlapCircle`/`OverlapCircleNonAlloc` 找敵對目標。專案全域 **`queriesStartInColliders = 0`（false）**（`ProjectSettings/Physics2DSettings.asset`），這會讓 overlap/cast 查詢**略過「重疊在查詢起點」的 collider**。兩隻怪深度重疊時，各自的中心可能落在對方 collider 內 → 被對方的查詢排除；因兩者大小/相對位置不同，常常變成「一方抓得到、另一方抓不到」的不對稱漏抓。（同一個雷/同雷射砲口貼身怪打不到，PlayerController 有註解、記於本檔他處。）
- **解法**：接觸/貼身傷害**別用 OverlapCircle 找目標**。改維護一份全場怪物登記表（`MonsterController.Active`，OnEnable/OnDisable 進出），逐一用 **`Physics2D.Distance(colA, colB)`** 判重疊——它是兩個 collider 的直接距離運算，**不受 `queriesStartInColliders` 影響**（這也是原本「敵人打玩家」一直穩定的原因，那段本來就用 `Physics2D.Distance`）。玩家目標用 tag 找、怪物目標用登記表濾陣營。**通則**：本專案任何「貼身/重疊」判定優先用 `Physics2D.Distance` 或既有目標清單，不要用 OverlapCircle 當唯一偵測。（2026-07-09 記）

### B8. 掛在 MapRoot 下的「每幀做事」元件，跨 module 換圖時會多活好幾秒（東西會跟到下一張圖）
- **症狀**：做「怪物出生點每隔 N 秒生一波」時，離開關卡回廣場，**上一張圖的怪會出現在邪佛廣場**，位置還是舊圖的世界座標（可能卡在牆裡）。明明元件掛在 `MapRoot` 下、換圖會隨之銷毀。
- **原因**：「掛在 MapRoot 下＝換圖自動停」這個直覺**只對同 module 的房間互跳成立**（那條路徑是同一幀跑完 `ClearTransientGameplay()` ＋ `LoadMap()`）。跨 module 是協程、橫跨好幾秒：開讀取頁（`LoadingPanel` 刻意 `PausesGame=false`，所以 **`timeScale` 還是 1、`Update` 照跑、`Time.deltaTime` 照走**）→ 停留 `loadingScreenHoldSeconds`（預設 2 秒）→ `ClearTransientGameplay()` 清光場上的怪 → 分幀預載素材（可達數秒）→ **最後**才 `Teardown()` 銷毀舊 MapRoot。中間那一大段舊元件都還活著；而它生出來的怪是 `Instantiate` 到場景根、**不在 MapRoot 底下**，於是躲過那一次清場，一路跟到新地圖。
- **解法**：這類元件的 `Update` 開頭一律加載入中 guard —— `if (MapManager.Instance != null && MapManager.Instance.IsLoading) return;`（`MapMonsterRespawner` 就是這樣做的，另外也擋 `GameFlowManager.IsEndingLevel`，避免過關倒數／死亡等待那兩段「不暫停」的時間還在冒怪）。**通則：不要用「物件掛在 MapRoot 下」當作『換圖就會停』的保證，讀取頁不暫停遊戲。**（2026-08-06 記）

### B9. 地上物「看起來明明有空隙、卻走不過去」；把素材的邊切掉也沒用
- **症狀**：斜擺的屏風、細桿的燈籠、有椅腳的椅子——圖上明明是透明的地方，玩家一走進去就被擋住。作者把素材的邊「切到不能再切」，那塊簍空處還是照擋。另外在**可走層把那幾格塗回可走也完全沒效果**。
- **原因**：兩件事一起造成的。
  ① **地上物擋路靠的是自己身上的 Collider，不是可走層**。可走層的三態子格位元圖只生成「牆/水」的碰撞與 A* 尋徑格（`MapLoader.BuildCellColliders`）；地上物是另外一顆掛在自己 GameObject 上的碰撞（`MapLoader.BuildOneObject`）。兩套系統彼此不知道對方存在，所以塗可走層對地上物零作用。（A* 那邊反過來：`MapNavGrid` 用「位元圖 ∪ 物理 OverlapCircle」聯集，才把家具補進不可走。）
  ② **那顆碰撞是「整張圖不透明像素的外接矩形」**（`MapSpriteLoader.GetAlphaLocalBox`）。外接矩形只由最外圍那**一個**像素決定，所以**只能縮框、不能挖洞**——切邊永遠救不了簍空。實測 `furniture_bamboo_screen2.png`：355×483、外接框 341×463，但**框內只有 58.9% 的像素是不透明的**，其餘 41% 是空的卻照擋；擺放縮放 1.435 之後那顆框有 1.91 格寬 × 2.60 格高。
- **解法**：2026-08-19 起地上物碰撞改成**貼合圖形**——把素材切成子格逐格判斷「這格有沒有畫東西」（`ObjectFootprint` / `FootprintMask`），同一列連續的格併成一條 box，全部 `usedByComposite` 交給 `CompositeCollider2D` 合併成單一外框。遮罩在 `Project Tools → Sync Map Assets` 時烘進 `catalog.json`（只烘 Environment 分類、動畫物件取第一幀），catalog 沒有時遊戲端當場掃當退路。調整入口在 `MapLoader` 的「地上物碰撞」那一組：子格解析度、實心判定門檻、整體內縮。細節見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)。
- **三個一起要注意的地方**（改成多顆碰撞後才會出現，漏掉都是靜默壞掉）：
  ① **碰撞一定要掛在物件本身、不能開子物件**——命中判定有兩種寫法並存（`PlayerController` 有 `GetComponent<IDamageable>()` 也有 `GetComponentInParent<IDamageable>()`），掛到子物件上會讓前者找不到 `DestructibleObject`，症狀是「這個東西打不壞」。
  ② **一定要用 `CompositeCollider2D`**：一堆小方框排在一起會留下內部接縫，圓形的玩家貼著表面滑動時會在接縫上拿到一瞬間的斜法線而卡住，子彈反彈方向也會亂跳。牆本來就是這樣做的（`BuildCompositeFromCells` 的註解已寫明合併後外形與 `hit.normal` 與逐格版一致）。
  ③ **「靠旗標中途現身」的物件要整組開關**：`MapObjectRevealer` 原本只收一顆 `Collider2D`，多顆之後只開關其中一顆會變成「東西還沒現身、路卻已經被擋住」（或反過來）。已改成收 `Collider2D[]`，由 `MapLoader` 傳 `go.GetComponents<Collider2D>()`。
- **實作時自己踩到的兩個**（給之後要動這塊的人）：① **「較粗解析度」只能由烘焙解析度降取樣得到，不可以改成直接在該解析度掃**——降取樣是 OR、直接掃是整格算覆蓋率，同一張圖差 10~38%，混用就會變成「有烘/沒烘的機器擋路範圍不一樣」。② **遮罩的最後一欄／最下一列是被截短的**（`cols = ceil(圖寬/格寬)`），建碰撞條時一定要夾回畫布邊界，否則右邊與下邊會多出隱形牆（實測 91 張素材有 27 張中招，屏風 0.227 格、書架 0.254 格），而且形狀會左右不對稱（左上精確、右下外擴）。
- **通則**：**「碰撞範圍」與「可走層」是兩份獨立的真相，不要假設塗了其中一個另一個會跟著變。** 另外，**外接矩形這種「只記得極值」的表示法，天生無法描述凹形**——遇到「調素材調不出來」時先確認資料結構有沒有辦法表達你要的東西，再繼續調圖。（2026-08-19 記）

### B10. 進某張圖就卡在家具中間動不了（而且「改回舊設定就好了」是假象）
- **症狀**：把地上物碰撞的子格解析度從 4 調到 8 之後，一進紅嫁衣書房，角色就出現在中央書架裡面、完全動不了；調回 4 就正常。看起來像是解析度改壞了。
- **原因**：**跟解析度無關。這張地圖沒有放「玩家出生點」(playerSpawn)。** `MapManager.ResolveSpawnPos` 的順序是「具名落點 → playerSpawn → **地圖中心**」，所以退回了地圖中心 (9,-5)，而那裡正好在 `furniture_bookcase3`（中央書架，擺在 8.29,-5.92）的碰撞範圍裡。
  - 兩種解析度**都是生在書架裡面**（實測 (9,-5) 在 subdiv 4 與 subdiv 8 的碰撞內皆為 true）。差別只在物理推不推得出來：subdiv 4 的外框接近一個大方塊，Box2D 沿最短方向把玩家推出去就沒事了；subdiv 8 的外框是階梯狀、凹角多，圓形的玩家容易被相反方向的接觸法線夾住而出不來。
  - **所以「調回 4 就好了」只是把問題蓋住**，不是修好。這類「換個設定就正常」的假象最容易讓人往錯的方向查。
  - 原本的程式只印一則 `找不到傳送落點與玩家出生點，玩家放在地圖中心。`，**沒有提「地圖中心可能在牆裡」**，所以看到 Console 也不會聯想到這裡。
- **解法**：兩件事一起做。
  - **資料**：在地圖編輯器幫缺的地圖補「玩家出生點」。實測 16 張圖有 **9 張沒有**（Main_Square、紅嫁衣的 BridalRoom/Courtyard/Kitchen/LivingRoom1/LivingRoom2/ShrineHall/Storeroom/Study），其中**目前只有書房的中心真的被擋住**——其餘 8 張是「剛好還沒踩到」，只要哪天把家具往中間挪一點就會複製同一個 bug。
  - **程式（已做）**：`MapManager.FreeSpotNear` —— 三條落點路徑（具名落點／出生點／地圖中心）都會再過一次防呆，若被 Environment/Water 擋住就以 0.25 格為一圈往外找最近的空位（最多 4 格），並把 Warning 寫清楚「多半是沒放玩家出生點」。找不到空位則印 Error。
  - ⚠ 這段**必須暫時打開 `Physics2D.queriesStartInColliders`**（專案全域是 false，見 **B7**）——它會讓 overlap 查詢略過「重疊在查詢起點」的 collider，而我們要問的正好就是「這個點是不是在東西裡面」，不打開就會永遠回答「沒被擋」。另外 `autoSyncTransforms` 也是 false，碰撞剛建好要先 `Physics2D.SyncTransforms()` 才查得到。
- **怎麼快速確認**：效能面板 **P → C** 打開碰撞疊層，看玩家的黃圈是不是壓在綠色（地上物）裡面。
- **通則**：**「退回預設值」型的後備路徑要能自我檢查。** 「找不到就放地圖中心」看起來很安全，但它把一個資料缺口變成了一個隨機的物理 bug；而且因為它只印 Warning 不擋人，缺口可以存在好幾個月都沒人發現。（2026-08-19 記）

### B11. 一進新房間就被彈回上一張圖（被怪擊退到傳送點上）
- **症狀**：從書房往上走進客廳2，畫面有一瞬間過去了，然後又退回書房。
- **原因**：**被擊退算成「踩到傳送點」**。`TeleportWatcher` 的落地防抖是「著陸時未武裝 → 玩家離開所有傳送格才武裝 → 之後再踩才觸發」，這在「玩家自己走」的前提下是對的。但客廳2 的怪物就站在落點旁邊：玩家一進門走離傳送格（武裝）→ 還沒看清楚就被怪打、擊退**推回傳送格** → 判定成「踩到」→ 又被送回書房。
- **解法**：`TeleportWatcher` / `CutsceneWatcher` 都加上「**非自主位移不算踩到**」——玩家 `HitReactionHandler.IsKnockedBack` 為真時踩到觸發點，不觸發，**而且解除武裝**。
  ⚠ **只是「跳過這一幀」是不夠的**：擊退結束時玩家還站在那格上，下一幀照樣觸發，只是延後 0.x 秒。解除武裝＝要玩家自己走出去再走回來，正好複用既有的落地防抖語意。
- **過場點（cutscene）比傳送更該防**：它是一次性的（`_fired`），被擊退誤觸等於白白播掉一段只能看一次的演出。
- **鏡頭區（CameraZoneWatcher）刻意不處理**：被擊退進鏡頭區只是鏡頭跟著變，離開就還原，無害。
- **通則**：**「玩家不是自己走過去的，就不該觸發位置型事件。」** 之後若加拉扯／吹飛／輸送帶之類的非自主位移，記得一起加進 `TeleportWatcher.IsPushedAround()`。
- **順帶一提**：這件事**不該用「把怪物挪遠一點」或「調低怪物可見範圍」來修**——前者只是降低機率（其他地圖照樣會踩到），後者是拿全域戰鬥參數解一個局部擺放問題。挪怪物值得做，但那是關卡體驗的理由（玩家一進門還沒看清楚就挨打本身就不好），不是修這個 bug 的手段。（2026-08-19 記）

### B12. 換房後玩家被丟到地圖外面 ／ 換圖那一幀的物理查詢會查到「上一張地圖的牆」
- **症狀**：書房 ↔ 客廳2 互走，回到書房時玩家被放在傳送點上方、完全動不了。Console：`落點 (9.00, -1.50) 被地上物/牆擋住，已挪到 (9.00, 0.50)`——而 **(9, 0.5) 在地圖外面**（地圖 y 範圍是 0 ~ -10）。書房那個位置實際上是空的（可走層算過，半徑 0.4 的圓一個牆子格都沒碰到）。
- **原因**：**`Destroy()` 延到幀尾才生效，而「同 module 房間互跳」整段在同一幀跑完。**
  `MapManager.LoadMapRoutine` 的 else 分支（同 module）走的是**同步版** `mapLoader.LoadMap(row.path)`：`Teardown()` → 建新圖 → `ResolveSpawnPos()` 全在同一幀。`Teardown` 只呼叫 `Destroy(_root.gameObject)`，物件要等**幀尾**才真的消失 ⇒ 那段時間裡**舊地圖與新地圖的碰撞體同時存在於物理世界**。
  於是落點防呆在書房查 (9,-1.5)，查到的是**還沒被銷毀的客廳2 的牆**（客廳2 只有 10 格寬，(9,-1.5) 在它的座標系裡整片是牆），判定「被擋住」→ 往外找 → 每個候選點都同時被兩張地圖的幾何檢查 → 一路找到兩張圖之外才「乾淨」。
  **跨 module 換圖不會中**：那條路走協程、中間有 `yield`，舊碰撞早就銷毀了。所以症狀只在同 module 房間互跳出現，很容易誤判成別的原因。
- **解法（兩道）**：
  1. **根因**：`MapLoader.Teardown()` 在 `Destroy` 之前先 `_root.gameObject.SetActive(false)`。**停用是立即生效的**，碰撞體當下就退出物理世界。這也一併保護了任何「換圖後立刻做物理查詢」的程式（例如 `MapNavGrid` 用 `OverlapCircle` 建障礙格）。
  2. **防護**：`MapManager.FreeSpotNear` 的候選落點一律**限制在地圖範圍內**（`MapCoords.WorldBounds`）。寧可找不到空位也不要把玩家丟到牆外——那比原本卡住還糟。
- **通則**：**`Destroy()` 不是「馬上不見」。** 只要在同一幀內「拆掉舊東西 → 建新東西 → 做物理查詢」，中間就一定要 `SetActive(false)`（或 `DestroyImmediate`），否則查到的是兩份世界疊在一起，而且完全靜默。（2026-08-19 記）

### B13. 傳送點「我放的位置」跟「實際會傳送的位置」對不齊 —— 以及「改用腳底判定」為什麼是錯的
- **症狀**：傳送點的觸發格跟門的美術對不齊，改成自由擺放的矩形之後，把矩形放在門上又變成**完全不會觸發**（其他傳送點正常）。
- **第一層原因（對不齊）**：傳送點原本是 Trigger 層塗的整格，只有整格精度，而門畫在背景圖裡、位置任意。**解法**：改成 `TeleportAnchor` 的「錨點＋矩形」，位置自由。
- **第二層原因（改腳底之後完全不觸發）——這條才是重點**：當時的推論是「光盤畫在地上，應該用**腳底** `PlayerController.FeetWorldPos` 判定」。看起來更正確，實際上讓**牆邊的傳送點結構上不可能被觸發**：
  - 玩家的碰撞圓在 **`transform.position`（胸口高度）**，腳底在它下方約 **1.0 個世界單位**。
  - 牆是照可走層蓋的，擋的也是那個碰撞圓 ⇒ 玩家頂到牆時**胸口**離牆只剩一個半徑。
  - ⇒ **腳底永遠無法靠近牆壁一格以內。而門就在牆上。**
  - 實測（書房，P→C 疊層讀數）：判定點 (8.71, −1.41)、腳底 (8.71, −2.40)、框 y[−2.17, −1.15] ⇒ 腳底差 0.23 進不去，**而且再怎麼走都進不去**。
- **解法**：判定點改回 **`transform.position`**。矩形位置自由擺放（第一層的修正）保留，那才是原本要解的問題。
- **通則**：**「哪個座標比較正確」是錯的問題；正確的問題是「這個系統其他部分用哪個座標」。** 物理、可走層、地圖擺放、角色 sprite 的 pivot 全都以 `transform.position` 為準，觸發判定單獨換成腳底，就會跟整個系統差一整格——而且差的方向剛好是「靠不近牆」，於是只有**牆邊**的觸發區壞掉，其他地方照常，非常難從症狀反推。
- **與 E14 的關係**：E14 說「定位特效要用 `FeetWorldPos` 不要用 `transform.position`」，那是對的——**特效是畫給人看的，對齊視覺**。這一則說觸發判定要用 `transform.position`——**判定是跟物理較勁的，對齊碰撞**。兩者不衝突，選哪個要看「你在跟誰對齊」。
- **診斷工具**：按 **P** 再按 **C** 開碰撞疊層，會畫出傳送點的踩踏矩形（青綠）、玩家碰撞圓（黃）、腳底十字，畫面底部並列出判定點座標、框範圍、「判定點在框內＝是/否」、「啟用＝是/否」。**任何「明明碰到了卻沒反應」先開這個看。**（2026-08-20 記）

### B14. 劇情剛播完，玩家就被送回上一張圖
- **症狀**：從客廳2 往下走進書房，書房的進場劇情自動開演；演完的瞬間畫面一切，人回到客廳2。像是劇情的 `end` 交棒設錯，但 `end` 的去向其實是空的。
- **原因**：**進圖落點常常就站在「回去的那顆傳送點」上面**——`targetEntrance` 指的就是對面那顆傳送點的錨點（書房的 `Study_north` 就是通回客廳2 的門），所以剛落地時玩家腳下踩的正是回頭路。平常沒事，是因為 `TeleportWatcher` 有落地防抖（著陸時未武裝，要離開再踩回來才算），而玩家一落地就會自己走開。
  **但自動播的劇情會把玩家釘在那個位置十幾秒**，而且如果勾了「隱藏主角」，收尾還會**用程式把玩家搬回開演前的位置**。這段期間只要武裝狀態被翻成 true（玩家被挪動過、位置被程式改寫、或條件式判定讓 `onTeleport` 短暫變 false），下一幀就會判定「踩到傳送點」——而人根本沒動過。
- **解法**（兩道，都照 **B11**「玩家不是自己走過去的，就不該觸發位置型事件」那條規則）：
  1. `TeleportWatcher` / `CutsceneWatcher` 在 `CutsceneDirector.IsPlaying` 期間**一律不觸發，並持續解除武裝**。⚠ 只「跳過這一幀」不夠——演出結束時人還站在那顆傳送點上，下一幀照樣觸發（這點與 B11 的擊退是同一個教訓）。
  2. 任何「用程式把玩家搬過去」之後都要呼叫 `MapManager.DisarmPositionTriggers()`。目前呼叫者是 `PlayerVisibility.Show`（劇情收尾放回原位），而且**只有真的位移時才搬、才解除**。
- **診斷**：`TeleportWatcher` 現在會在每次觸發時印一行「踩到傳送點「X」→ 地圖 N；玩家位置 …」。看到這行卻不記得自己走過去，就是這一類問題。
- **通則**：**「玩家站著不動」不是安全狀態，而是一個會累積風險的狀態。** 位置型觸發的設計前提是「玩家會走開」，任何把玩家長時間釘在原地的機制（演出、教學、對話、暈眩）都會讓落地防抖這種「靠玩家自己離開」的防護失效。做這類機制時要主動問一句：**玩家被釘住的那個位置，本身是不是某個觸發區？**（2026-08-22 記）

## C. 地圖編輯器 / 素材同步

### C6. 從 `DipanProj_MapEditor/Effects` 複製 PNG 到遊戲後，VFX／動畫子彈完全隱形，Console 報 `Animation sprite not found`
- **症狀**：檔案確實存在於 `Assets/Resources/Weapon/` 或 `Assets/Resources/VfxEffects/`，路徑、檔名與幀數也正確，但 `Resources.Load<Sprite>` 回傳 null，武器或特效完全看不到。
- **原因**：`Effects/` 在 Unity `Assets` 外，複製進主遊戲時沒有 `.meta`；Unity 第一次匯入若仍是一般 Texture（不是 Sprite），用 `Resources.Load<Sprite>` 就載不到。原專案只有 `Resources/UI` 的自動匯入器，武器／VFX 沒有同等防呆。
- **解法**：已新增 `Assets/Editor/GameEffectTextureImportSettings.cs`；新放進 `Resources/Weapon`、`Resources/VfxEffects`、`Resources/GroundEffect` 的圖片第一次匯入會自動設為 Single Sprite、PPU 100、Point、關 Mipmap、透明、無壓縮。已經生成錯誤 `.meta` 的舊圖需在 Inspector 手動套相同設定或 Reimport；匯入後再確認 `Resources.Load<Sprite>(路徑)` 不為 null。

### C7. 從 Effects 匯入的動畫子彈只剩一個小點，命中特效反而比飛行物清楚
- **症狀**：子彈確實有飛出去，也能命中及播放 HitEffect，但飛行途中小到無法辨識武器外型。
- **原因**：誤把 `WeaponTable.BulletScale` 當成圖片的最終世界縮放。實際生成還會乘 `BulletPrefab.transform.localScale`（目前 **0.1**）與 `PlayerScale`（Player prefab 目前 **0.8**）；因此填 1 的 32px 圖最後只有 `0.32×0.1×0.8=0.0256` 世界單位，幾乎是一個點。舊武器源圖多達數百像素，所以原本填 1～5 沒那麼明顯；Effects 投射物多為 32～96px，必須反算。
- **解法**：用 `目標世界寬 ÷ (原生像素寬/PPU × BulletPrefabScale × PlayerScale)` 算 BulletScale。以目前 PPU100／prefab0.1／玩家0.8，32px 飛行物要約 0.64 格寬就填 25；96px 飛行物要約 1 格寬就填約 13。Effects 動畫子彈必須依原圖尺寸個別反算，不要用命中特效的 Scale 推測子彈 Scale（兩者生成路徑不同）。

### C1. 動畫地上物同步後在「地上物」清單看不到(category 變成資料夾名)
- **症狀**:在 `Environment/<子資料夾>/` 放了多張幀圖、同步素材後,編輯器「地上物」工具的清單裡找不到這個動畫物件。檢查 `catalog.json` 會看到那些幀變成**一張一筆獨立項目、`category` = 子資料夾名**(例如 `Teleport`),而非收成一筆 `category: Environment` 的動畫物件。
- **原因**:**素材同步有兩條路徑,改了一條忘了另一條**。① CLI `Tools/sync_assets.sh`、② Unity 選單 `DipanMapEditor → 同步素材`(`Assets/Editor/AssetSyncTool.cs`)。兩者都會重生 `catalog.json`。舊版兩者都用「遞迴掃所有 PNG + `category = 上層資料夾名`」,所以子資料夾的幀會被當成各自獨立、且 category 變成子資料夾名 → 被物件清單的 `category == Environment` 過濾掉。**只改了其中一條(例如只改 bash)、卻用另一條(Unity 選單)同步,就會中這個雷**。
- **解法**:**兩條同步路徑必須一起維護**。現已都改成:Environment 直接擺的單張 = 靜態;**子資料夾 = 一個動畫物件**(多幀收成一筆,`category` 仍為 Environment、`id` = 資料夾相對路徑,含 `frameCount`/`frames`,依檔名排序)。改動其一時務必同步另一(`sync_assets.sh` 與 `AssetSyncTool.cs`,以及未來遊戲端的 `MapAssetSyncTool.cs` 與 `MapIO.BuildFromGameAssets`)。重新用任一方式同步後即正常顯示。

### C2. IMGUI 選取面板的「＋」按鈕沒反應 / 改完被回退(數值卡在下限)
- **症狀**:物件選取面板裡「－/＋ + 文字框」這種數值控制(動畫 FPS、血量),`－` 正常但 `＋` 按了沒反應;尤其值被 `－` 減到下限後,`＋`/`－` 看起來都失效。
- **原因**:IMGUI 一次 `OnGUI` 由上往下執行。文字框把「當下 buffer」鎖進回傳變數 `sf`,而 `＋` 按鈕排在文字框**之後**且會在按下時同時改 `sel.值` 與 `buffer`;到了結尾那行「`if (sf != buffer) → 視為使用者打字、回寫 sel`」就會用**舊的 `sf`** 把剛剛 `＋` 的改動**回退**掉。`－` 因為排在文字框**之前**,文字框讀到的是新值,所以不受影響——於是只有 `＋`(或所有「在文字框之後又寫 buffer 的按鈕」)壞掉。
- **解法**:① 把「未編輯時 `buffer = sel.值.ToString()`」的同步移到**繪製文字框之前**;② 文字框的回寫**只在真的聚焦該欄位打字時**才做(`editing && sf != buffer`)。這樣 ±按鈕直接讀寫 `sel.值`、不會被回退。已修(`EditorUI.cs` 的 FPS 與血量兩處;X/Y 座標本來就沒在按鈕內動 buffer,故不受影響)。**之後在 IMGUI 加「±＋文字框」控制都照這個寫法。**

### C3. 在 GameAssets 新增一種素材子資料夾(例 `Talk/` 頭像),放了圖、跑了 Sync Map Assets,遊戲卻載不到
- **症狀**:把新素材(例如頭像立繪)放進 `GameAssets/Modules/<module>/Talk/`、跑了 `Project Tools → Sync Map Assets`,但遊戲端載入時找不到(catalog 裡沒有該項目、`catalog.Find(id)` 回 null)。
- **原因**:**素材同步只收「分類白名單」內的資料夾**。三個同步產生器各有一份 `Cats` 白名單(原本 `{ Environment, Tiles, Background, Drama }`),新資料夾名稱不在裡面就**整個被忽略**,根本不會進 catalog / StreamingAssets。同 [C1](#c1-動畫地上物同步後在地上物清單看不到category-變成資料夾名) 的根因家族:**同步有多條路徑、且靠白名單過濾**。
- **解法**:把新分類名稱**同時**加進三處的 `Cats`/`CATS`(漏一個就會「用某條路徑同步時又不見」):
  - `Assets/Editor/MapAssetSyncTool.cs`(Project Tools 選單用)
  - `Assets/Scripts/Map/MapIO.cs`(`BuildFromGameAssets`,編輯器後備)
  - `Tools/sync_map_assets.sh`(CLI)
  加完**重跑 Sync Map Assets**。catalog 用 `category = 上層資料夾名`,但 runtime 是用 **id(= 相對路徑)** `catalog.Find` 取圖,所以 category 叫什麼不影響載入(頭像不進編輯器清單,不必像 Drama 那樣考慮 category 過濾)。已修:`Talk` 已加入三處白名單(頭像對話立繪)。

### C4. 編輯器讀圖後整片空白（格線正確、素材全消失、地磚調色盤說「沒有可畫的地磚」）
- **症狀**:編輯器讀某張圖（例 Main_Square），格線大小正確、也顯示「已讀入」，但背景/地上物/地磚全部沒出現；調色盤顯示「沒有可畫的地磚」。Console 無紅字。
- **原因**:**編輯器的 `StreamingAssets/MapAssets` 是同步拷貝（gitignored、不進版控），不完整就會這樣**。實例：catalog 只剩 RedBridalGown＋Tutorial、`Main/` 整個不見 → Main 的圖檔讀得進（.dipanmap 是資料）、但所有素材 id 都解析不到 → 全部隱形。同步工具是「**先清空目標再重建**」，中途失敗/中斷就會留下半套素材而且**不會報錯**。
- **解法**:重跑同步即可：Unity 選單 `DipanMapEditor → 同步素材（全部 module）`，或 CLI `DipanProj_MapEditor/Tools/sync_assets.sh`（不帶參數 = Main＋全部 module）。跑完重新 Play 讓 bootstrap 重載 catalog。**通則:編輯器「讀得到圖但看不到東西」先查 catalog.json 的 module 分佈是否齊全（python 一行就能數），不是查地圖檔。**

### C5. NPC 立繪放在 Talk 子資料夾（Talk/Buddha/…），對話面板始終不顯示該立繪
- **症狀**:DramaTalkTable 填了 `Main/Talk/Buddha/Buddha_normal`，圖也放在 `GameAssets/Main/Talk/Buddha/`，但對話面板該側立繪永遠空白。Console 有「找不到立繪（catalog id…）」警告。
- **原因**:三條素材同步路徑對一般分類**只收分類資料夾第一層的 PNG**（`TopDirectoryOnly`），子資料夾整個略過（Environment 的子資料夾另有「動畫物件」語意）→ `Talk/Buddha/` 沒進 StreamingAssets 也沒進 catalog，`catalog.Find` 自然 null。另兩個常見疊加雷：CSV 路徑**帶了 `.png` 副檔名**（catalog id 一律不帶）、檔名打錯（本例曾是 `nuddha_normal.png`）。
- **解法**:已把 **Talk 類別改成遞迴收子資料夾**（id=相對路徑去副檔名，例 `Main/Talk/Buddha/Buddha_normal`），三處同步一起改：`Tools/sync_map_assets.sh`、`Assets/Editor/MapAssetSyncTool.cs`、`Assets/Scripts/Map/MapIO.cs`。改完**重跑 Sync**。**通則:立繪/劇情圖欄位填的是 catalog id——不帶副檔名；填完先確認 catalog.json 裡真的有這個 id，沒有就是同步沒收到（白名單/層級/檔名三個方向查）。**

### C8. 要新增／改名一個素材分類時，該去哪裡改（白名單已收斂成單一來源）
- **症狀**：C1／C3／C5／I4／F16 反覆出現的同一個母題——新增一種素材資料夾、或改了同步範圍，結果「有些地方生效、有些沒生效」，而且**漏改不會報錯，只會靜默少同步**，要進遊戲看到東西不見才發現。
- **原因**：「哪些資料夾要同步」這份白名單原本在遊戲端**寫了三次**（`Assets/Scripts/Map/MapIO.cs`、`Assets/Editor/MapAssetSyncTool.cs`、`Tools/sync_map_assets.sh`），三份各自獨立，改一處另外兩處不會跟著動。
- **解法**：2026-07-27 已把**兩份 C# 收斂成單一來源** `Assets/Scripts/Map/MapAssetCategories.cs`（`All` 陣列＋`IsRecursive()`），`MapIO` 與 `MapAssetSyncTool` 都引用它。**現在改分類只有 2 個地方要動**：
  1. `Assets/Scripts/Map/MapAssetCategories.cs` 的 `All`（兩支 C# 自動跟著變）
  2. `Tools/sync_map_assets.sh` 的 `CATS=(...)`（仍是獨立的 shell/python 實作，該行上方已加註解提醒）
- **兩個容易誤會的點**：
  - **`DipanProj_MapEditor` 的 `AssetSyncTool` 只同步 Environment／Tiles／Background 是刻意的**，不是漏的。`Drama`／`Talk` 是遊戲端的劇情大圖與對話立繪，地圖編輯器的調色盤用不到——**不要「順手幫它補上」**（該檔註解已標明）。
  - **分類名稱若拿來當「判斷條件」，一定要用常數不要打字面值**。例如「動畫地上物」的觸發開關是 `cat == MapAssetCategories.Environment`；當初若留成 `cat == "Environment"`，將來改名時 `All` 迴圈會變、這行不會變，動畫地上物會**整批靜默消失**。判斷條件目前已全部收斂，只剩「當輸出標籤用」的字面值（寫錯只是 category 標籤不同，風險低）。
- **順帶**：`Sync Map Assets` 跑完現在會印**同步摘要**（每個分類收了幾筆、其中幾筆是多幀動畫、各 module 幾筆；某分類掛零會跳 Warning）。**懷疑漏同步時第一件事就是看那段摘要**，比進遊戲試快得多。（2026-07-27 記）

### C9. 地圖編輯器的「選項循環按鈕」明明沒設定，卻顯示第一個選項——存進 `.dipanmap` 的其實是空字串
- **症狀**：編輯器 trigger 參數裡那種**點一下換下一個選項**的欄位（例：`面板：gacha`、`條件不成立時：中止整條鏈`），新建的 trigger 一打開就顯示第一個選項，看起來已經設定好了。但**進遊戲該設定沒作用**；而且**點第一下按鈕不會換**（畫面不變），要點第二下才跳到下一個選項。
- **原因**：按鈕的顯示與遞增用同一個 `IndexOf` 結果。值是空字串時 `IndexOf` 回傳 **-1**，顯示端卻 fallback 成 `options[0]`（於是畫面「看起來有值」），遞增端算 `(-1+1) % n = 0` → 又回到 `options[0]`（於是「第一下沒反應」）。兩個症狀是同一個 -1 造成的。**真正的狀態是「未設定」，不是 options[0]**——所以存進 `.dipanmap` 的是空字串，遊戲端讀到空的自然不生效。
- **解法**：把「未設定」當成獨立狀態顯示與處理（`EditorUI.cs`）：
  ```csharp
  bool unset = idx < 0;
  if (GUILayout.Button(unset ? "（未設定）" : cur))
      r.Params[p.key] = p.options[unset ? 0 : (idx + 1) % p.options.Length];
  ```
  這樣未設定會明白寫著「（未設定）」，點第一下就落到 `options[0]`。**通則：任何「用 IndexOf 找目前值」的 UI，-1 要當成第三種狀態顯示出來，不要 fallback 成 [0]——fallback 會讓「沒填」偽裝成「填了預設值」，而且這種錯誤不會報錯、只會讓資料靜默失效。** ⚠️ 修好之前建的 trigger 要**回去重點一次**那些欄位（本例是三座祭壇的 `panelId` 全都是空字串）。（2026-07-28 記）

---

### C10. 編輯器裡剛加的欄位/旗標「進遊戲完全沒生效」——兩份東西要同步，而且漏掉旗標表是靜默錯的
- **症狀**：在地圖編輯器勾了新的劇情選項（例：「演出期間關閉血量 HUD」）、存好檔、進遊戲測試，**完全沒有作用**，看起來像程式沒寫好。
- **原因**：**遊戲讀的不是編輯器那份檔案。** 編輯器存的是 `DipanProj_MapEditor/Maps/…`，主遊戲 runtime 讀的是 `DipanProj_Main/Assets/StreamingAssets/MapAssets/…`；沒跑 `Project Tools → Sync Map Assets` 之前，遊戲讀到的還是舊版（這是 **B3** 的同一個根因，但**新增欄位**的症狀特別容易被誤判成「功能沒做出來」——舊版檔案裡那個欄位根本不存在，反序列化拿到預設值 `false`，一切正常沒有任何錯誤訊息）。
- **⚠ 更陰險的第二半：`flags.json` 是另一份要同步的東西，漏掉是「靜默降級」而不是壞掉。** 旗標的生命週期只存在旗標登記表裡（方案乙，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §2.5），編輯器授權檔在 `DipanProj_MapEditor/flags.json`、遊戲讀 `StreamingAssets/MapAssets/flags.json`。`TriggerChain.Resolve` 查不到那個旗標時**不會報錯，直接退回「周目」**。所以新建的「關卡單次」旗標若沒同步過去，遊戲端會把它當周目旗標用——**功能看起來完全正常**（劇情確實只播一次），只是「一趟關卡一次」悄悄變成「一周目一次」，可能好幾天後才發現。
- **解法**：改完編輯器端的地圖或旗標表，**進遊戲測試前先跑 `Project Tools → Sync Map Assets`**。排查時的快速判斷：直接開 `StreamingAssets/MapAssets/` 底下那份 `.dipanmap` 搜尋你新加的欄位名——**沒搜到就是沒同步，不用去看程式**；旗標則比對兩份 `flags.json` 的筆數。
- **通則**：**「編輯器改了、遊戲沒變」永遠先確認遊戲讀的是哪一份檔案，再去懷疑程式。** 另一條更重要：**「查不到就退回預設值」的設計會把「忘記同步」變成一個安靜的行為改變**，而不是一個看得見的錯誤——這種地方值得在退回預設值時印一行 Log。（2026-08-22 記）

## D. 存檔 / 常駐單例 (Save & Persistent Singletons)

### D1. 退出 Play 跳「Some objects were not cleaned up when closing the scene. (Did you spawn new GameObjects from OnDestroy?) → [InventorySystem]」
- **症狀**:停止 Play 時 Console 出現上述警告,列出的物件是某個**懶漢常駐單例**(例如 `[InventorySystem]`)。不影響執行,但每次停 Play 都跳。
- **原因**:懶漢單例的 `Instance` getter「找不到就 `new` 一個」。`SaveManager.OnDestroy` 為了退訂事件而呼叫 `InventorySystem.Instance` ——但關場景時 `InventorySystem` 可能**已經先被銷毀**,於是那個 getter 在 **OnDestroy 期間又生出一個新的 `[InventorySystem]`**,正好就是警告問的「Did you spawn new GameObjects from OnDestroy?」。**任何在 `OnDestroy`/`OnDisable`/`OnApplicationQuit` 裡呼叫其他懶漢單例 `Instance` getter 的程式都會中這個雷**。
- **解法**:**在物件生命週期早期(Awake/Start)就把要互動的單例參照快取起來,teardown 時只用快取欄位、絕不呼叫會「找不到就建立」的 getter**。Unity 的 `==` 對已銷毀物件會視為 `null`,所以 `if (_cached != null) _cached.OnChanged -= ...;` 在對方已被銷毀時自動跳過、不會重生。已修(`SaveManager` 加 `_inv` 快取欄位 + `Inv` getter 只在正常流程用;`OnDestroy` 改用 `_inv`)。

### D2. 靜態助手「方法名與型別同名」→ 方法內用 `型別.靜態成員` 報 CS0119
- **症狀**:在 `UIBuilder` 加了 `public static InputField InputField(...)` 後,方法內寫 `InputField.LineType.SingleLine` 編譯報 `CS0119: 'UIBuilder.InputField(...)' is a method, which is not valid in the given context`。但 `AddComponent<InputField>()` 那種「泛型參數位置」卻沒事。
- **原因**:方法名 `InputField` 與型別 `UnityEngine.UI.InputField` **同名**。在「**用簡單名做成員存取**」的位置(`InputField.LineType`),C# 把 `InputField` 解析成**方法群組**而非型別 → 對方法做 `.LineType` 不合法。泛型參數/回傳型別位置因為文法只接受型別,反而能正確解析成型別(所以既有的 `Button`/`Text`/`Image` 助手沒事——它們從不寫 `型別.靜態成員`)。
- **解法**:在這種「方法名 == 型別名」的助手裡,**對型別的靜態成員存取一律用完整命名空間**:`UnityEngine.UI.InputField.LineType.SingleLine`(`AddComponent` 也順手 `<UnityEngine.UI.InputField>` 保險)。已修。**之後在 UIBuilder 加同名助手都照此。**

### D3. DTO 換命名空間後,另一個命名空間的檔案找不到它 (CS0246 / CS0029)
- **症狀**:把 `StorageDTO` 從 `Dipan.Save` 搬到 `Dipan.Inventory`(為守「資料層不依賴存檔」邊界)後,`SaveSystem.cs` 報 `CS0246 找不到 StorageDTO` + `CS0029 List<StorageDTO> 無法轉成 List<Dipan.Inventory.StorageDTO>`。
- **原因**:`SaveSystem.cs` 沒 `using Dipan.Inventory`,裸寫的 `StorageDTO` 解析不到搬走後的型別。
- **解法**:跨命名空間引用 DTO 時,**要嘛在檔案頂端 `using`、要嘛寫完整命名空間**。本專案存檔層刻意「依賴資料層、不反向」,故存檔層碰資料層 DTO 時用 `Dipan.Inventory.XxxDTO` 完整名(`SaveSystem`/`SaveManager` 已照此;`CharacterSave` 因有 `using Dipan.Inventory` 可裸寫)。

### D4. 程式建立的 uGUI `Button` 不會自動指定 `targetGraphic`(SpriteSwap / `btn.image` 失效)
- **症狀**:用程式 `AddComponent<Image>()` + `AddComponent<Button>()` 建按鈕後,設 `transition = SpriteSwap` 沒效果、或讀 `btn.image` 是 null;按下不換圖、也拿不到背景 Image。
- **原因**:Unity 的 `targetGraphic` 只在 **Editor 的 `Reset()`** 時自動指到同物件第一個 `Graphic`。**執行期 `AddComponent<Button>()` 不會自動指**,所以 `targetGraphic`(以及 `btn.image`)是 null,ColorTint/SpriteSwap transition 與 image 存取都失效。點擊本身仍可用(靠 GraphicRaycaster 命中 Image,與 targetGraphic 無關),所以容易以為「按鈕正常」卻只是 transition 壞掉。
- **解法**:程式建按鈕後**手動 `btn.targetGraphic = btn.GetComponent<Image>();`** 再設 transition/spriteState。已在倉庫頁籤與重整鈕照此(`StoragePanel`)。**之後用 `UIBuilder.Button` 又要換圖/讀 image 時都要補這行。**

### D5. 兩個同層視窗並排時,共用遮罩把下面那個蓋黑、且擋住它的 hover/點擊
- **症狀**:倉庫＋背包並排(同 `UILayer.Window`、都 `ShowBackdrop=true`)時,**下面那個面板變黑、滑過去沒有高亮也沒 tooltip**;單獨開其中一個卻正常。
- **原因**:`UIManager` 只有**一張共用半透明黑遮罩**,設計假設「一次一個 modal」——`UpdateBackdrop` 把它鋪在**堆疊最上層那個 backdrop 視窗的正下方**。兩個同層視窗都開時,遮罩就卡在兩者之間,**蓋住下面那個**;遮罩 `raycastTarget=true` 又半透明 → 下面面板變黑 + 滑鼠事件被吃掉(hover/tooltip/點擊全失效)。
- **解法（最終）**:把 `UpdateBackdrop` 改成「只要有任一 Window 層面板要遮罩就鋪一張,並 **`SetAsFirstSibling()` 放在所有視窗最底層**」,而不是「鋪在最上層視窗的正下方」。如此**不論開幾個同層視窗,都只有一張遮罩、永遠在全部視窗後面**——不蓋任何面板、不擋 hover、也不可能疊加(全程只有一張 `_backdrop`)。`ShowBackdrop` 維持單純 `true` 即可。
  - 〔早期權宜做法(已淘汰):並排時把 `ShowBackdrop` 動態關掉 → 雖然不蓋面板,但並排時就完全沒有壓黑遮罩,觀感不佳。最終改用上面 UIManager 的做法,並排時仍有一層在後面。〕

### D6. `Button.rectTransform` 編譯不過(`Selectable` 沒有這個屬性)
- **症狀**:程式建 `Button` 後寫 `btn.rectTransform` 取它的 RectTransform 編譯報錯(找不到成員)。
- **原因**:`rectTransform` 是 **`Graphic`** 的屬性(`Image`/`Text` 有);`Button`/`Selectable` 並非 `Graphic`,沒有這個屬性。容易誤以為所有 uGUI 元件都有 `rectTransform`。
- **解法**:用 **`(RectTransform)btn.transform`** 或 `UIBuilder.Rect(btn)`(專案助手)取得。既有 `StoragePanel` 就是用 `(RectTransform)b.transform`。已在 `DramaPanel` 的整片透明關閉鈕照此。**之後對 `Button` 取 RectTransform 一律這樣寫,別用 `.rectTransform`。**

### D7. 字串插值裡寫三元運算子,整串爆 `CS8076/CS8361/CS1003/CS1525`,**整個專案編不過、所有腳本掛不上(Add Component 搜不到新元件)**
- **症狀**:加了一行 `Debug.LogError($"...（{vp != null ? vp.url : "?"}）...")` 後,Console 一次跳四個錯(`CS8076 Missing close delimiter '}'`、`CS8361 A conditional expression cannot be used directly in a string interpolation`、`CS1003`、`CS1525`)。更要命的是:**只要專案任何一個腳本編譯失敗,整個 Assembly-CSharp 就建不起來,於是「所有」腳本都無法掛上,Add Component 也搜不到新寫的元件**——很容易誤以為是「新腳本沒被 Unity 認得/沒匯入」,其實是別處(或本檔)有編譯錯誤擋住全部。
- **原因**:C# 字串插值 `$"...{運算式}..."` 裡,`:` 是「格式分隔符」(如 `{x:F2}`)。直接寫三元 `a ? b : c`,編譯器把第一個 `:` 當成格式起點,後面整段就解析錯亂。
- **解法**:把三元式**用括號包起來** `{(a ? b : c)}`,或**抽成一行變數**再插入(本專案採後者,最不易再踩):
  ```csharp
  string u = (vp != null) ? vp.url : "?";
  Debug.LogError($"...（{u}）...");
  ```
- **連帶通則**:「Add Component 搜不到某腳本」**第一步永遠先看 Console 有沒有紅字**——任一編譯錯誤都會讓全部腳本掛不上,先清掉編譯錯誤,別急著 Reimport/找新腳本沒被認得的理由。

### D8. 觸發鏈「對話接對話」→ 對話關閉當幀玩家整個卡死（遊戲永久暫停、看不到新對話）
- **症狀**:用觸發鏈把 `對話 → …(giveItem) → 對話 → …` 串起來，跑到「前一段對話結束、要接下一段對話」時，畫面卡住：**玩家不能動、遊戲像被暫停、也沒有任何新對話跳出來**。前一段對話能正常播完、拿到道具的 toast 也有，就是接著卡死。
- **原因**:對話面板（`TalkPanel`/`DramaPanel`）在自己的 `OnClose` 裡**同步**通知觸發鏈接下去（`TriggerChain.NotifyDramaClosed`）。若鏈的下一步又是「開一段新對話」，等於在「面板正在關」的呼叫堆疊裡又去 `Open` 同一個面板 = **重入**。`UIPanel.DoClose` 的順序是先 `IsOpen=false`→`OnClose()`→再 `StartFade(0, deactivate)`：重入時新面板在 `OnClose` 中被 `DoOpen`（`IsOpen` 又設回 true），但控制權回到外層 `DoClose` 後那句 `StartFade(0, deactivate)` 把**剛開好的新面板又淡出停用**，而 `IsOpen` 停在 true → `UIManager.Recompute()` 看到「還有面板開著」持續**暫停＋擋輸入**，但那面板已被停用（看不見、Update 不跑、按鍵無法翻頁）→ 永久卡死。
- **解法**:對話關閉後的接鏈**延後一幀**再跑，等舊面板那一幀完全關乾淨、`Recompute` 已解除暫停，下一幀再開新對話就不會重入。新增常駐小幫手 `Assets/Scripts/Map/TriggerChainRunner.cs`（`NextFrame(Action)`，自動生成、`Update` 不受 timeScale 影響故暫停中仍會跑），`TriggerChain.NotifyDramaClosed` 改成 `TriggerChainRunner.NextFrame(() => OnCompleted(r))`。中間只差一幀、無感。見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §5。
- **通則**:**永遠不要在一個模態面板的 `OnClose` 裡同步開另一個模態面板**（或任何會 `Open` 面板的邏輯）。要接續就延後一幀。

---

### D9. 背包裡的（測試）道具「時有時無/常常消失」——測試種道具與存檔還原的順序衝突
- **症狀**：某個新加的武器/道具（例：御靈水晶 13）明明進了背包，卻常常又不見了，時好時壞。
- **原因**：`SaveManager`（`[DefaultExecutionOrder(-500)]`，很早）開場會**自動載入活躍角色** → `InventorySystem.RestoreState` **先清空背包再依存檔還原**。若角色存檔是在「還沒有這個道具」時建立的（存檔裡沒有它），還原後背包就沒有它。而測試種道具的 `InventoryLauncher`（執行序 0，跑在 SaveManager 之後）原本寫成「**背包全空才塞**」→ 還原後背包非空 → 跳過不補 → 道具永遠缺；偶爾在全新空存檔時才會出現＝時有時無。
- **解法**：`InventoryLauncher` 改成「**缺哪把就補哪把**」（`for id 1..13: if(!HasAnywhere(id)) AddItem(id)`），因為它跑在 `RestoreState` 之後，就能把舊存檔缺的測試武器補回；補回後進廣場自動存檔會把它寫進存檔、之後就穩定。新增 `InventorySystem.HasAnywhere`（背包格+裝備欄都算，避免已裝備的又被重複補）。**通則**：任何「開場給預設物品」的邏輯要意識到它與存檔還原的先後——還原是清空重建，給道具要嘛在還原之後補、要嘛正式走存檔。（純測試用；正式改走撿道具/掉落系統。）（2026-07-09 記）

### D10. `CS1061 'RectTransform' does not contain a definition for 'SetActive'`
- **症狀**：新寫的面板編不過，錯在 `_summaryRoot.SetActive(true)` 這一行。看起來完全正常的一行。
- **原因**：`SetActive` 是 **`GameObject`** 的方法，不是 `Transform`／`RectTransform` 的。專案裡程式建 UI 的慣例是把 `UIBuilder` 回傳的 `RectTransform` 存成欄位（`RectTransform _summaryRoot`），所以手很自然就打成 `_root.SetActive(...)`——但 `RectTransform` 只有 `gameObject.SetActive(...)`。
- **解法**：`_summaryRoot.gameObject.SetActive(true)`。**通則（給 AI／給沒開 IDE 就大量寫 UI 程式碼的情況）：交檔前的自檢不能只做括號配對。** 這類「型別對不上但語法完全合法」的錯只有編譯器抓得到，所以至少要對常見誤用做一次字串掃描，例如：
  ```
  rg '\b_?[A-Za-z]*(Rect)?Transform[A-Za-z_]*\.SetActive\('
  ```
  同類還有 `Button.rectTransform`（見 D6）、`Image.sprite = Texture2D`、`Transform.position` 當 `RectTransform.anchoredPosition` 用。（2026-07-28 記）

### D11. 把格子「鎖起來」只在 `OnBeginDrag` 裡 return，結果東西照樣被拖走（沒有懸浮圖示，但真的搬了）
- **症狀**：鍛造台把裝備放上鐵砧後，背包來源那一格應該鎖住不能動。格子元件的 `OnBeginDrag` 明明已經 `if (locked) return;`，實測卻還是「拖得動」——把它拖到別格放開，東西真的換位置了（只是拖曳過程中沒有跟著游標的半透明圖示）。
- **原因**：**Unity 的 EventSystem 在滑鼠按下時就把 `eventData.pointerDrag` 設成那個格子了**，跟我們的 `OnBeginDrag` 有沒有做事完全無關。而共用的 `SlotDragController.Drop` 是這樣拿來源的：
  ```csharp
  var src = e.pointerDrag != null ? e.pointerDrag.GetComponent<ISlotView>() : _src;
  ```
  → 就算 `Begin` 從沒被呼叫（`_src` 是 null、也沒建 ghost），放開時 `Drop` 仍從 `pointerDrag` 撿回那個被鎖的來源並執行 `InventoryActions.Resolve`。所以症狀才是「看起來沒在拖、卻真的搬走了」。
- **解法**：**鎖要擋在共用拖放層，不能只擋在格子元件。** `SlotDragController` 新增
  ① `_src == null` 就直接 return（沒經過 `Begin` 的拖曳一律不成立）；
  ② 一個 `IsSlotLocked` 查詢鉤子（面板開啟時掛、關閉時拆），`Begin`／`Drop` 各自檢查來源與目標。
  格子元件那邊的 `if (locked) return;` 保留，但只當「提早收工」，不是唯一防線。
- **通則**：只要是「共用事件基礎設施 + 個別元件想擋掉某些操作」的結構，擋的地方要在**真正執行動作的那一層**；上游元件的 early-return 常常擋不住框架已經填好的狀態。另外 `OnEndDrag` 的收尾（清 ghost、還原 `blocksRaycasts`）**不要跟著 early-return 一起跳過**，否則狀態會殘留。（2026-07-29 記，見 [FORGING.md](FORGING.md) §4）

### D12. 面板一開就洗版「圖的尺寸跟版面表記的不一樣」，但圖根本沒動過（元凶是匯入設定的 Max Size）
- **症狀**：開鍛造面板，Console 一次噴七八條 `[ForgingPanel]「UI/Common/ForgingPanel_Btn」尺寸是 2048x573，但版面表記的是 2416x676`。去看檔案，明明就是 2416×676，沒人動過。
- **原因**：`ArtSpec` 的自檢是拿 `sprite.rect` 跟「PNG 檔案尺寸」比，但 `sprite.rect` 是**匯入之後**的尺寸——`.meta` 的 `maxTextureSize`（本專案預設 **2048**）會把超過的圖**等比縮小**再進遊戲。2416×676 → 2048×573（乘 0.8477）就是這麼來的。
- **解法**：**改成比「畫布比例」而不是像素數**。`PlaceArt` 的算式全是比值（`fullW/bw`、`rectW/fullW`…），等比縮放完全不影響擺放結果，所以那種情況本來就不該報警；真正要抓的是「重新輸出時畫布比例變了 ＝ 內容在畫布裡的相對位置跑掉」。`ForgingPanel` 與 `GachaPanel` 的 `LoadArt` 都已改成比 `w/h`，差 1% 以上才警告。
- **附帶提醒**：反過來說，看到這個警告時要先分辨兩種情況——① 只是 Max Size 縮圖（無害，現在不會再報）；② **真的換了圖**（那就要重量內容邊界框、更新 `ArtSpec`）。這次兩種同時發生：`ForgingPanel_Btn` 是①，`ForgingPanel_ItemFrame` 是②（1360×1210 換成了 1448×1296）。
- **順帶**：`Max Size` 會縮圖也意味著**素材開太大是白費的**。底部長按鈕在畫面上只有 ~240px 寬，卻放了 2416px 的原圖（進遊戲仍是 2048），等於 8~10 倍過取樣。依 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) 的規範（大按鈕 256~512），把這張的 `maxTextureSize` 調到 512 就好，記憶體與載入都省。（2026-07-29 記）


### D13. 兩個過場/演出同時進行時，先結束的那個把還在生效中的輸入鎖一起解掉（玩家在不該能動的時候能動）

- **症狀**：血統變身演出（約 6 秒、期間玩家被鎖住不能閃避）中途被怪打死 → 死亡流程 `GameFlowManager.EndLevel` 也掛了自己的輸入鎖，接著變身演出跑完、解除鎖 → **死亡結算等待期間玩家又能走能打**。反過來也會發生：死亡流程先解鎖，把演出的鎖清掉。
- **原因**：`UIManager.SetExternalHold(block, pause)` 舊版是**單一組布林**（`_extBlock` / `_extPause`），沒有「誰掛的」概念，所以任何一方呼叫 `SetExternalHold(false, false)` 都會把全部人的需求一起清空。這與面板的需求不同——面板是逐一列舉 `_panels` 重算的，天生就是「任一要求就生效」。
- **解法**：`_extBlock`/`_extPause` 改成 **`Dictionary<string, (bool block, bool pause)>` 具名持有者**，`Recompute` 一併 OR 進去。舊的兩參數多載保留、內部用一個共用的預設 key（既有呼叫端行為完全不變，它們本來就共用同一組旗標）。**新程式一律用具名版** `SetExternalHold("我的系統名", true, false)`。
- **通則**：只要一個全域狀態可能被「兩個生命週期重疊的系統」同時要求，就不能用單一組布林——不是改計數器就是改持有者表。這個 bug 的可怕之處是它只在「兩件事剛好重疊」時才出現，平常測一百次都不會遇到。

### D14. 演出播到一半整個凍住不動，20 秒後結果才「跳」出來（元凶是某個 `PausesGame` 的面板）

- **症狀**：血統變身演出播到一半玩家按了 `B`（背包），畫面整個凍結——玩家趴著、電弧停在半空、雷柱不動；等很久之後新外型才突然 pop 出來。
- **原因**：兩件事湊在一起。① `InventoryPanel` / `StoragePanel` / `ForgingPanel` 都是 `PausesGame => true`，一開就 `Time.timeScale = 0`。② 演出用到的東西——`PlayerAnimator.Update`、`VfxInstance.Update`、`SegmentedLightningColumn.Update`、以及演出協程自己的等待——**全部吃 `Time.deltaTime`**，timeScale 歸零就整組停擺。最後是 `BloodlineSystem` 的逾時保險絲（用 `unscaledTime`）先到期，強制把外型套上去，所以看起來是「卡住很久然後結果直接跳出來」。
- **解法**：兩層。① 演出開頭 `UIManager.CloseAll()` 關掉已開的面板；② **演出期間鎖住會暫停遊戲的熱鍵**——`StorageBagCoordinator` 查 `BloodlineTransformFxRunner.IsPlaying`。⚠ 不能改成查 `IsGameplayInputBlocked`，因為背包開著時它本來就是 true，那樣按 `B` 會關不掉背包。
- **通則**：**做任何「跨數秒、吃 `Time.deltaTime` 的演出」之前，先問「這段期間玩家能不能開出一個 `PausesGame` 的面板」。** 能的話就得擋。另外，演出自己的「逾時保險絲」一定要用 `unscaledDeltaTime`，否則連保險絲都會被凍住（保險絲的意義就是在事情出錯時仍能跑）。
- **後續（2026-08-19）**：變身演出改成**刻意全程暫停**（見 **D15**），所以「面板把它凍住」這個死法本身已經不存在了。但**熱鍵仍然要擋**——理由從「會凍住」變成「會整片蓋在表演上面」。查詢對象也從 `BloodlineTransformFxRunner.IsPlaying` 換成 `BloodlineSystem.IsPerforming`（世界演出 ∪ 立繪面板）。另外補上 `ESC`：`UIManager` 的「沒有視窗開著就開設定面板」那個分支加了 `!_inputBlocked`，否則不入堆疊的 Overlay 演出面板不會被 `TopStackPanel()` 看到，玩家按 ESC 就能在演出上面疊一個設定面板。
### D15. 把一段演出改成「暫停播放」，結果演出整個不動了（每一個計時器都要跟著換）

- **症狀**：為了讓玩家在變身的 6 秒裡不會被怪打死，把 `SetExternalHold` 的 `pause` 從 `false` 改成 `true`。世界確實凍住了——但演出也一起凍住：角色倒到一半定格、雷柱停在第一幀、煙塵不播，只有螢幕震動和白閃還在動。
- **原因**：`Time.timeScale = 0` 之後，`Time.deltaTime` 恆為 0、`WaitForSeconds` 永遠不會到期。演出鏈上**每一個**吃遊戲時間的東西都會停：`PlayerAnimator` 的姿勢動畫、`VfxInstance` 的動畫與壽命、`SegmentedLightningColumn` 的幀推進、演出協程自己的 `Wait()`。震動與白閃之所以沒事，是因為 `MapCameraController.ConsumeShakeOffset` 與 `ScreenFader` **當初就寫成 unscaled**。
- **解法**：給每個元件一個 **預設 `false` 的 `Unscaled` 旗標**，由演出的持有者在生出它的當下打開；一般戰鬥特效因此行為零改變。
  - `VfxInstance.Unscaled` → 內部一律走 `Dt` 屬性（連 `FlashRoutine` 的 `WaitForSeconds` 也換成 `WaitForSecondsRealtime`，否則暫停中白光會定格在全白、`_flashCo` 永遠不歸 null）
  - `SegmentedLightningColumn.Unscaled`
  - `PlayerAnimator.UnscaledPose`（**只**影響倒下／趴地／爬起；走路待機仍吃遊戲時間，暫停時本來就該停）
  - 演出協程的 `Wait()` 直接寫死 unscaled
  - `Spawn` 系列本來就會回傳實體，所以**一個函式簽章都不用改**，生出來設旗標就好
- **通則**：**「這段演出要不要暫停」是一個會擴散到整條呼叫鏈的決定，不是一個布林。** 決定改之前先把鏈上所有計時器列出來——動畫、特效、協程等待、物理、以及**保險絲**。保險絲尤其容易漏，而它一旦被凍住就等於整個安全網失效。反過來說，若某個元件當初就寫成 unscaled，那多半是前人踩過同一個坑留下的，不要「順手改成 deltaTime 保持一致」。

### D16. 面板淡出還沒開始，遊戲就已經解除暫停了（`UIPanel.DoClose` 是先叫 `OnClose` 再淡出）

- **症狀**：血統揭示面板播完自動關閉，收尾有 0.4 秒淡出。但玩家在淡出**還沒淡多少**的時候就已經被丟回戰場——畫面上還壓著八成不透明度的黑遮罩和立繪，怪物已經在打他了，而他完全看不到場面。
- **原因**：`UIPanel.DoClose()` 的順序是 `IsOpen = false` → **`OnClose()`** → `StartFade(0)`。把「解除暫停」的回呼掛在 `OnClose` 上，等於**淡出開始的那一刻就解鎖**，整段淡出時間玩家都是「能被打、但看不見」。這在 `BossIntroPanel` 上不明顯（開戰資訊淡出時開打反而合理），到了「保護玩家」為目的的表演上就變成 bug。
- **解法**：**不要把收尾淡出交給 `UIPanel`。** 面板自己在 `Update` 裡把 `CanvasGroup.alpha` 淡到 0（unscaled），淡完才呼叫 `UIManager.Close(this)`；同時把 `FadeDuration` 改成「收尾階段回 0」，讓 `DoClose` 立刻收掉、`OnClose` 當場觸發回呼。開場淡入仍然用 `UIPanel` 的那一套。
- **順帶**：`IsShowing` 這種「表演進行中」的旗標也要延到**回呼真正放行的那一刻**才清，不能在 `OnClose` 就清——早一幀清的話「熱鍵解鎖」會比「解除暫停」早，玩家能在暫停狀態下按 B 開背包。
- **通則**：**「面板關閉」和「面板從畫面上消失」是兩個不同的時間點**，中間差一整個淡出。凡是「面板消失前玩家不該恢復控制」的表演，回呼就得綁在後者，而 `OnClose` 給的是前者。

### D17. 同一件事寫在兩個入口，長出一個「沒人打算做」的行為（左鍵也能喝掉不可逆的血統藥劑）

- **症狀**：作者回報「我原本的要求是在背包裡點**右鍵**使用物品，但我測試，點**左鍵**也能使用」。實際上左鍵點血統藥劑會直接跳確認視窗然後喝下去——而血統是**本世不可逆**的，等於一次誤點就定終身。
- **原因**：「使用道具」這件事**沒有唯一入口**，散在三個地方各寫一份：
  ① `InventoryPanel.OnSlotClicked`（左鍵）的 if/else 階梯裡有一條 `IsBloodline → TryDrinkBloodline`；
  ② `InventoryPanel.OnSlotRightClicked`（右鍵）也有一條，兩邊呼叫同一個私有方法；
  ③ `PotionHotkeys.Use` 自己寫了一整套「套效果 → 扣一瓶 → 播特效 → 清快捷格」。
  ①②是不同時期各自加上去的，當時還互相標註「兩邊行為刻意一致」——**那句話本身就是警訊**：兩個入口需要靠註解維持一致，代表它們遲早不一致。而且 `InventorySlotWidget.OnPointerClick` 寫的是 `if (右鍵) … else …`，所以**中鍵、側鍵也會被當成左鍵**。
- **解法**：收成一支非 UI 的 `Inventory/ItemUse.cs`，UI 只負責顯示。
  - `PlanUse(itemId)` 純計算（能不能用／理由／要不要先跳確認視窗），`TryUse(itemId, out message)` 真的用。照 `BloodlineSystem.Plan`／`TryDrink` 的樣板。
  - 立一條**全遊戲的鐵則**：**左鍵＝搬移／裝備／綁定（永不消耗），右鍵＝使用（唯一會消耗的滑鼠操作）**。左鍵那條 if/else 階梯裡從此不准出現任何會消耗東西的分支。
  - 三個把關點一起補齊，不然規則會在別的地方漏水：`InventorySlotWidget` 左右鍵**分別列舉**（不要 `else`）、倉庫的 `ItemSlotWidget` 改成只收左鍵、`SlotDragController.Begin` 只允許左鍵開始拖曳（否則右鍵按住稍微移動就變搬移，同一個手勢差幾像素兩種結果）。
- **通則**：**「兩邊行為刻意一致」是一個要重構的訊號，不是一個可以寫在註解裡的設計。** 只要一件會改變狀態的事有兩個以上的入口，它們就會分岔——不是今天，是下一次有人只改了其中一邊的時候。順帶一提，這次的分岔還不是「行為不同」，而是「**兩邊都能做一件本來只該有一個入口的事**」，這種更難發現：功能看起來完全正常，只是多了一條沒人打算開的門。

### D18. 改了預設值卻沒生效——因為 `ResetForPlayMode` 裡還寫死了第二份

- **症狀**：想臨時測英文版，把 `Language.Current` 的初始值從 `Lang.CN` 改成 `Lang.EN`，進 Play **畫面還是中文**。改對了、也重編譯了，就是沒反應。
- **原因**：`Language.ResetForPlayMode()` 裡有一行 `Current = Lang.CN;`（為了防「上一輪切成英文的殘留帶到下一輪」而加的，本身是對的）。而 `PlayModeStaticReset` 在每次進 Play 最早期就會呼叫它 ⇒ **欄位初始值在那一瞬間就被覆蓋掉**。等於同一個「預設語言」被寫死在兩個地方，改其中一個永遠無效。
- **解法**：抽成一個常數，兩邊都讀它。
  ```csharp
  public const Lang DefaultLanguage = Lang.CN;   // 要改預設語言只改這裡
  public static Lang Current = DefaultLanguage;
  ...
  ResetForPlayMode() { …; Current = DefaultLanguage; }
  ```
- **通則**：**關掉 Domain Reload 之後寫的每一個 `ResetForPlayMode`，都是「這個 static 的初始值」的第二個副本。** 只要它把值寫死，欄位初始值就永遠是死的。凡是 reset 要還原成某個預設，那個預設一定要是常數、不能各寫各的——否則下一個人（或三天後的自己）會盯著一行明明改對的程式碼懷疑人生。這其實就是 **D17** 那條「同一件事不要有兩個入口」在 static 初始化上的變形。

### D19. 為了消除警告而刪掉「留著隨時可切換」的程式碼——`const` 開關造成的 CS0162

- **症狀**：建置時跳 `warning CS0162: Unreachable code detected`，指到的那段程式明明是刻意留著的替代做法（例如「煙塵改成撒 3~4 顆」「角色素材翻面」「關掉火焰特效」）。
- **原因**：那些開關寫成 `const bool` / `const int`。**const 是編譯期常數**，所以 `if (SmokeBurstCount <= 1)` 在編譯時就被判定恆真，另一條分支變成 unreachable。危險的是這個警告會誘導人「把用不到的程式刪掉」——但那段不是死碼，是**下次要調表現時直接改一個數字就能切換的備援路徑**，刪了就得重寫。
- **解法**：把開關從 `const` 改成 `static readonly`。值一樣、行為一樣、效能差異可忽略，但編譯器不再把它當編譯期常數 ⇒ 兩條分支都會編譯（**不會爛掉**）、也沒有警告。
  ```csharp
  static readonly int  SmokeBurstCount = 1;    // BloodlineTransformFxRunner
  static readonly bool ActorFlipX      = false; // SaveSlotPanel
  static readonly bool EnableFireFx    = true;  // TitlePanel
  ```
- **通則**：**`const` 是給「這輩子都不會變的事實」用的**（陣列長度、表格欄位索引、數學常數）。凡是「之後可能想改改看」的旋鈕與開關，一律 `static readonly`——否則你會在某次建置被警告推著去刪掉自己刻意留的後路。判準很簡單：**這個值我未來會不會為了調整而改它？會的話就不要 const。**

### D20. 著色器警告 `use of potentially uninitialized variable (<函式名>)`——條件式 return 的假警告

- **症狀**：`Shader warning in 'Custom/EyeOpen': use of potentially uninitialized variable (blurSample)`。點名的是**函式名**不是變數名，而且那個函式每一條路徑明明都有 return。
- **原因**：著色器編譯器把回傳值當成一個「以函式為名的隱含變數」。函式裡有**提早 return**（`if (r <= 0.00001) return tex2D(...);` 後面還有一段最後才 `return c;`）時，它證明不了每條路徑都寫過那個隱含變數 → 報這個警告。**是假警告**，但每次建置都會跳。
- **解法**：改成**單一出口**——先算好預設值，需要時才在 `if` 裡覆蓋，最後只有一個 `return`。
  ```hlsl
  fixed3 c = tex2D(_MainTex, uv).rgb;   // r 太小就直接是答案
  if (r > 0.00001) { c *= 0.28; c += …; }
  return c;
  ```
- ⚠ 改寫時**要驗權重總和沒變**（這裡是 `0.28 + 8×0.09 = 1.0`），否則畫面亮度會悄悄跑掉。
- **通則**：HLSL/Cg 的函式盡量寫成單一出口。C# 那邊「提早 return 減少巢狀」是好習慣，但在著色器裡會換來這個雜訊警告，而雜訊警告最大的成本是**讓人開始忽略警告視窗**。

### D21. 開演時把 HUD 關掉，HUD 還是在——`Start()` 比「建立它的那支程式」晚跑
- **症狀**：劇情演出勾了「演出期間關閉血量 HUD」，開演時程式確實呼叫了 `Close<BottomHudPanel>()`，**畫面上血球照樣在**。而且只在「進關卡的第一張圖」發生，同 module 房間互跳反而正常。
- **原因**：**血量 HUD 有兩個會主動打開它的來源，其中一個的時機在關閉之後。**
  1. `MapManager.PlaceAndSetup` 依地圖決定開/關——它在呼叫 `CutsceneDirector.MaybeAutoStart` **之前**，所以開演時關掉是有效的；
  2. `PlayerController.Start()` 也會開一次（玩家**初次生成**時）——而 Unity 的 `Start()` 是在「建立這個物件的那支程式跑完之後」才呼叫，**比 `MaybeAutoStart` 晚** ⇒ 把剛關掉的 HUD 又開回來。
  玩家跨圖不重生，所以只有「這趟第一次生成玩家」的那張圖會踩到，其餘房間都正常——症狀有地圖選擇性，極難反推。
- **解法**：**不要只在開演時關一次，改成每幀維持**（`CutsceneDirector.EnforceHudHidden` 在 `Update` 裡呼叫）。這樣不管之後誰去開都蓋不過演出，也不必去修改每一個會開 HUD 的地方（那會變成一份要同步的清單）。同時把「有人想開過」記起來，收尾才知道要還原成開著。
- **通則**：**`Start()` / `Awake()` 不在你以為的位置。** 只要你在某一幀「建立一個物件，然後對全域狀態下指令」，那個物件的 `Start()` 會在你的程式跑完之後才執行，並且可能把你剛設定的狀態改掉。判斷準則：**「這個狀態有幾個來源會去寫它？」**——一旦超過一個，一次性的設定就不可靠，要嘛改成每幀維持（權威式），要嘛讓所有來源查同一個開關。前者省事、後者乾淨，但後者的清單會隨時間腐爛。（2026-08-22 記）

### D22. 「隱藏主角」只有第一次 Play 有效，之後永遠失效（要重開 Unity 才會好）
- **症狀**：劇情勾了「演出期間隱藏主角」，一開始正常，某次之後**主角就再也藏不起來**了——重新 Play 沒用、重進地圖沒用，只有重開 Unity 才恢復。看起來像功能被改壞了。
- **原因**：`PlayerVisibility.IsHidden` 是一個**純 C# 的 static bool**，而本專案已關閉 Domain Reload（見 `PlayModeStaticReset`）⇒ **它不會在每次 Play 歸零**。只要有一次 Play 是在演出播到一半時按停止（測試時很常見），`Show()` 就沒機會執行，`IsHidden` 殘留成 `true`；下一次 Play 進到 `Hide()` 的第一行 `if (IsHidden) return;` 直接返回 —— **從此再也不隱藏，而且完全沒有錯誤訊息**。
- **同一個根因還有第二種觸發方式（build 也會中）**：演出**被換圖打斷**時，`StartCutscene` 直接 `Destroy` 掉還在跑的上一個 director，協程當場中斷、`Cleanup` 永遠不執行 ⇒ 玩家永遠隱形、回憶特效永遠掛著、輸入永遠鎖著。
- **解法**（兩道都要）：
  1. 加 `PlayerVisibility.ResetForPlayMode()` 並註冊進 `PlayModeStaticReset`；`Hide()` 另外加保險——狀態說「藏著」但目標物件已經不在了就視為狀態壞掉、重來一次。
  2. `CutsceneDirector` 把「對全域狀態動過的手」收成一支冪等的 `ReleaseGlobals()`，`Cleanup` 與 **`OnDestroy`** 都會走到，被硬銷毀時至少還原得回來。
- **⚠ 修第 2 點時會踩到 `Destroy()` 的延遲**（同 **B12**）：`StartCutscene` 換演出時若只是 `Destroy(舊 director)`，舊的 `OnDestroy` 會在**幀尾**才跑——那時新演出已經把主角藏好了，安全網當場把它放出來。所以要**先同步呼叫舊的 `ReleaseGlobals()` 再 `Destroy`**，讓幀尾那次變成 no-op。
- **通則**：**任何「進入某狀態 → 之後要還原」的 static 開關，都要同時回答三個問題**：① Play 停止時誰還原（→ `PlayModeStaticReset`）；② 持有者被硬銷毀時誰還原（→ `OnDestroy` 安全網）；③ 還原的動作跟「下一個持有者開始」的順序誰先誰後（→ `Destroy` 是延遲的，要先同步釋放）。少任何一項，症狀都是「用著用著就壞了、而且不會自己好」。同一家族：`BloodlineTransformFxRunner.IsPlaying`、`BloodlineIntroPanel.IsShowing`、`DamageNumberManager._quitting`。（2026-08-22 記）

## E. 效能 / 顯示 (Performance & Display)

### E1. Windows build「幀數低 / 不順」,但 Mac 與 Unity 編輯器都很順
- **症狀**:Windows build 在 PC 上跑覺得幀數低、不流暢;Mac build 與編輯器都很順。**從專案最初(只有四道牆+幾隻怪+主角)就這樣**,內容很少不該卡。
- **原因**:**不是效能問題,是顯示線路 + VSync。** 用效能面板(`PerfHud`,見 [DISPLAY_SETTINGS.md](DISPLAY_SETTINGS.md))實測:GPU 一幀只畫 **~1.5ms**(RTX 3060)、CPU 主緒 **~0.3ms**,引擎能力遠超數百 fps;但 FPS 鎖在 **59.9**、每幀 **16.68ms** 且**最差一幀也是 16.7ms(零掉幀)** → 那 16ms 幾乎全是 **VSync 在等垂直同步**。該遠端 PC 透過 **ATEN 4K HDMI 裝置(KVM/延長器/擷取)**出畫面,其 EDID 只提供 **~59.9Hz**(進階顯示設定只有 59.885/59.940/59.950 可選);而開發用的 Mac 是 **120Hz**,兩邊一比才覺得 Windows「卡」。本質是**顯示線路 60Hz vs Mac 120Hz 的落差**,不是遊戲跑不動。
- **解法**:
  - **先用面板自證**:`PerfHud`(按 **P** 開)→ 看「瓶頸」會顯示「受 VSync 限制」;點 **VSync(V)** 切到「關」→ FPS 立刻飆到數百(伴隨畫面撕裂),證明引擎沒問題、限制在螢幕刷新率。
  - **要高刷體驗**:把支援高刷的螢幕**直接插進顯卡**(繞過 ATEN/KVM 那顆裝置),VSync 維持開著,遊戲會自動跑到螢幕刷新率。
  - **不需要為效能優化任何東西**。一般玩家用自己的螢幕直連顯卡,順暢度=自己螢幕的刷新率,不會碰到這個。上架前要做的是「玩家畫面設定選單」(VSync/幀率上限/視窗模式),見 [DISPLAY_SETTINGS.md](DISPLAY_SETTINGS.md)。
  - **通則**:看到「某平台幀數低」先別急著怪 code——先用 `PerfHud` 看 **GPU ms / CPU ms / 是否被 VSync 鎖在刷新率**,區分「真的算不動」與「只是被顯示同步/線路擋住」。KVM、HDMI 延長器、擷取盒、遠端桌面串流都常把刷新率鎖在 60Hz(甚至 30Hz)。

### E2. 某張地圖左右兩側不是純黑、而是藍色(場景外露出底色)
- **症狀**:某些地圖(例 `RedBridalGown_LivingRoom2`)左右兩側、地圖沒覆蓋到的地方是**藍色**,不是純黑;其他地圖看起來卻是黑的。誤以為是「這張地圖的問題」。
- **原因**:那片顏色是 **Main Camera 的背景(Solid Color clear)色**,不是地圖。場景相機底色原本是藍 `RGB(0.192,0.302,0.475)`(Unity 預設藍)。會不會看到只取決於**地圖有沒有填滿畫面**:整張地圖模式 / 夠大的圖填滿畫面 → 看不到底色;**鏡頭跟隨且地圖比畫面窄**(如關卡起始圖 LivingRoom2)→ 左右露出相機底色 → 看到藍。所以是相機底色問題,與單張地圖無關。
- **解法**:讓相機底色一律純黑。在 `MapCameraController.Apply()` 取得 `_cam` 後**強制** `clearFlags = SolidColor`、`backgroundColor = Color.black`(每次載圖、每種相機模式都套,不依賴場景設定);並把場景 `MainScene.unity` 的 `m_BackGroundColor` 也改成黑(避免載圖前閃藍 / 編輯器預覽一致)。**通則:畫面上「地圖以外」的顏色 = 相機 clear 色,要改去相機,不是去找地圖。**

### E3. 套了某後處理/氛圍(shader)後,整個畫面變成一片洋紅/粉紫
- **症狀**:改了 `Atmosphere.shader`(氛圍後處理)後,Game 視窗整片**洋紅色**(亮粉紫 ≈ `RGB(230,46,243)`)。而且**不管 `Atmosphere` 填哪個 type 都一樣**洋紅,連原本正常的型別也是。
- **原因**:洋紅是 **Unity 的「shader 編譯失敗」錯誤色**(error/magenta shader),不是某個 type 的調色。因為整個效果是**同一個 shader**(`Custom/Atmosphere`,用 `_Mode` 切型別),**只要任何一處編譯錯誤,整支 shader 就掛掉 → 所有 type 全變洋紅**。實際撞到的雷:在 type 15 電視雜訊裡用了 `float line = ...`,而 **`line` 是 HLSL 保留字**(幾何著色器圖元),整支編不過。容易誤判成「指令數太多 / 某個 type 的數值問題」而亂調。
- **解法**:
  - **先看 Console 的紅字**:shader 編譯錯誤會明確寫「哪一行、什麼錯」(例:`undeclared identifier` / `syntax error` / `reserved keyword`)——那是最快的線索,別瞎猜。
  - 本次解法:變數別用保留字,`line` 改名 `ln`。其他易撞的保留字/內建名:`line`、`point`、`triangle`、`vector`、`matrix`、`sample`、`texture`、`sampler`、`in`/`out`/`inout`,以及內建函式名 `cross`/`mul`/`dot`(當區域變數雖可 shadow,但若同段又要呼叫該函式就會出事)。
  - **通則:全螢幕單一純色(尤其洋紅)≈ shader 沒編過**,先去 Console 找編譯錯誤,而不是調該效果的參數。順帶:多型別共用一支 shader 時,所有 `_Mode` 分支會被攤平進同一個 pixel shader,指令量偏大,必要時加 `#pragma target 3.5` 提高上限(但那是「指令過多」的解,與本則的語法錯誤是兩回事)。

### E4. 程式生成的 SpriteRenderer 給大 sortingOrder，物件存在卻整個看不到
- **症狀**:做場景特效(火雨,見 [SCENE_EFFECT.md](SCENE_EFFECT.md))時,Hierarchy 看得到火球物件一直在生,**畫面上卻什麼都沒有**。
- **原因**:`SpriteRenderer.sortingOrder` 雖宣告為 int,**實際是 16-bit(範圍 −32768~32767)**。給超大值(當時填 `2000000`)會**溢位繞回**:`2000000` 繞回後 ≈ **−31616(負)** → 比背景(`sortingOrder = −1000`)還低 → 被不透明背景蓋住、整個看不到。詭異的是地上物用 `1000000` 卻正常——因為 `1000000` 繞回後 ≈ **+16960(正)**,仍在背景之上,看起來沒事。
  > ⚠️ **後續（2026-08-18）：那個「剛好沒事」其實只是還沒踩到。** 地上物的公式是
  > `1000000 + zOrder*10000 + …`，`zOrder = 1` 繞回後會落到 **21960~31960**，正好壓在所有表演層之上。
  > 詳見下面的 **E15**。教訓：**靠溢位繞回「剛好落在對的地方」不是安全，是還沒被觸發的地雷。**
- **解法**:`sortingOrder` 一律用**合法範圍內(≤32767)**的值。火雨改用 `30000`(在合法範圍、又高於地上物繞回後的 ~17000~22000,確保畫在最前)。**通則:任何 runtime 建的 `SpriteRenderer`,sortingOrder 別超過 32767;要「畫在最上層」用接近上限的值(如 30000),不要用上百萬的數字。**

### E5. fps 明明 60(甚至數百)，角色移動仍有規律的微抖動(judder)
- **症狀**:PC 上 VSync 60fps 或解開 VSync 讓 fps 狂升,走路時仍「說不出的不順」;fps 數字完全正常。
- **原因**:兩件事疊加:① 物理 Fixed Timestep 是 0.02(**50Hz**),與 60Hz 螢幕形成拍頻——每 6 幀就有 1 幀物理沒更新或更新兩次;② Player/Monster 的 Rigidbody2D **Interpolate 全關**,角色位置直接吃 50Hz 物理步進 → 每秒約 10 次「跳半格」。相機(LateUpdate+SmoothDamp)是平滑的,角色是抖的,對比下更明顯。**抖動與 fps 高低無關**,所以拉高 fps 沒用;KVM 鎖 60Hz 下 fps>60 根本顯示不出來,只會撕裂(參 E1)。
- **解法**:Player/Monster prefab 的 Rigidbody2D `Interpolate = Interpolate`(m_Interpolate: 1);TimeManager `Fixed Timestep` 改 `0.016666668`(60Hz)。**通則:fps 正常卻不順,先查「物理頻率 vs 螢幕刷新率」與 Rigidbody 內插,不是查效能。**詳見 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) §1。

### E6. PC build 畫面粗糙有噪點、鏡頭移動時閃爍,Unity 編輯器裡卻好好的
- **症狀**:1080p PC build 世界畫面「髒、粗糙」,鏡頭移動時整個畫面微微蠕動閃爍;Mac 編輯器 Game view 看不出問題。
- **原因**:地圖素材 256px/格,但相機顯示 10 格高 → 1080p 一格只有 **108px**,素材被**縮小到 0.42 倍**;而 runtime 載圖用 **FilterMode.Point 且無 mipmap**——Point 在「放大」時是像素風,在「縮小」時是災難:每個螢幕像素只隨機挑 1 個原圖像素、丟掉週邊 5 個 → 噪點+移動閃爍。Mac Retina 的 Game view 像素密度接近 1:1 取樣,所以編輯器看不出來(解析度愈低愈慘)。
- **解法**:`MapSpriteLoader` 預設改 `FilterMode.Bilinear`,`new Texture2D(..., mipChain: true)`(LoadImage 會自動生成 mipmap,記憶體 +33%)。遊戲內按 **F** 可即時 A/B 對比 Point/Bilinear。**通則:貼圖會被「縮小」顯示時,必須 Bilinear+mipmap;Point 只適合 ≥1:1 的整數倍放大。**詳見 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) §2。

### E7. UI icon/按鈕看起來很髒、顆粒很大,懷疑是美術風格問題
- **症狀**:背包 icon、關閉鈕等 UI 元件顆粒感重、邊緣髒,懷疑黑暗像素風格不適合。
- **原因**:**與風格無關,是縮小倍率**。icon 原圖 256~500px,實際顯示只有 45~70px(= 5~10 倍縮小),匯入又是 Bilinear+無 mipmap+maxTextureSize 2048(不會被縮) → GPU 只取 2×2 texel,等於在 10×10 區域亂抽 4 點 → 顆粒與髒邊。
- **解法**:**不用重畫**,改 .meta 的 `maxTextureSize` 讓匯入器先做高品質縮圖:小 icon → **128**、中型按鈕 → **512**、面板背景不動。**通則:UI 圖的原始尺寸 ≈ 顯示尺寸 × 2 就好,大圖硬塞小格子一定髒。**素材尺寸規範見 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) §4。

### E8. 傳送門綠幕「突然消失」，變成門周遭一片綠光
- **症狀**:場景特效的傳送門（SceneFx kind=portal）原本是貼合門洞的一片綠色光幕，某次改版後光幕不見了，只剩門周圍一大片淡淡綠光。
- **原因**:程式生成貼圖的 **PPU 沒跟著解析度改**。`PortalFx.FillSprite()` 把貼圖從 64px 提到 256px（修對角脊線那次），但 `Sprite.Create(..., pixelsPerUnit: 64)` 沒改 → sprite 從 1×1 世界單位變 **4×4**，`localScale=_size` 再乘上去 → 光幕變 4 倍大（3.5×4.5 的門洞矩形變成 14×18 的大綠罩）。門洞內只剩貼圖中央一小塊、整體被攤薄，看起來就是「綠幕消失、周遭泛綠光」。
- **解法**:程式生成 sprite 一律 **PPU = 貼圖邊長**（`Sprite.Create(tex, rect, pivot, n)`），sprite 恆為 1×1 世界單位、由 localScale 控制實際大小。**通則:改程式生成貼圖的解析度時，檢查 `Sprite.Create` 的 pixelsPerUnit 是否跟著改——n 與 PPU 綁死（或抽同一個常數），否則所有用 localScale 定尺寸的物件全部默默變大/變小。**

### E9. AI 產的 UI 素材貼上去「位置怪怪的、周圍一大圈空白」——素材是整張畫布輸出，內容只佔中間一小塊
- **症狀**：拿 AI 產的介面素材（機台、標題條、按鈕底…）照設計稿的尺寸放進 uGUI，結果元件之間的間距全跑掉、對不齊；把 Image 拉到「看起來對」的大小時，實際圖案又太小。用 `preserveAspect` 也救不回來，因為那只保住整張圖的比例。
- **原因**：AI 出圖幾乎都是**固定畫布**（1536×1024、1000×250 這種），實際內容只畫在畫布中間一塊，四周是**透明留白**，而且每張圖的留白比例都不一樣。uGUI 的 `RectTransform` 對齊的是**整張圖**，不是「有顏色的那塊」——所以你以為在對齊機台，其實在對齊一張比機台大 40% 的透明矩形。**這不是匯入設定或 pivot 的問題，重設 pivot 也只是換一個對不準的方式。**
- **解法**：**別去改圖，改成「量一次、程式反推」**。對每張素材量出「內容的 alpha 邊界框」，寫成一張表：
  ```csharp
  struct ArtSpec { public string path; public float fullW, fullH, bx, by, bw, bh; }
  // 例：機台圖畫布 1536×1024，內容框 (350,8) 寬 835 高 978
  ```
  再用一個 `PlaceArt(img, spec, contentH, center)` 由「我希望內容框多高／中心在哪」**反推**整張圖該給多大的 `sizeDelta` 與該偏移多少。版面常數從此全部描述**內容框**，跟畫布留白脫鉤。量邊界框可以用 PIL：`Image.open(p).getchannel('A').getbbox()`。
  **關鍵配套**：`LoadArt` 載入時比對 sprite 的實際尺寸和 `fullW/fullH`，不符就 `Debug.LogWarning`。因為這張表是「量出來的常數」，**素材重出圖／改尺寸時它會默默失效**——有這個警告，重出圖會當場報，而不是進遊戲才發現版面整個歪掉。（2026-07-28 記）

### E10. 同一個格子裡，有的物品 icon 大得剛好、有的小得只剩一半——留白比例不一樣（E9 的物品 icon 版）
- **症狀**：背包格子大小完全一樣、`sizeDelta` 也一樣，畫出來卻是劍塞滿整格、藥水只有格子的三分之一。`preserveAspect` 打開也沒用。
- **原因**：跟 E9 同一個根因，只是發生在**數量會一直增加的物品 icon** 上。2026-08-07 量過 30 張 icon，**不透明內容佔長邊的比例從 41% 到 100%**（`item_hpPosition_s` 是 500×500 的畫布裡只有 146×206，`weapon_sword` 則是整張都畫滿）。uGUI 對齊的是整張圖，所以留白多的那張看起來就小 2.4 倍。
- **為什麼不能照 E9 的解法**：E9 那套「量一次、寫成常數表」對**固定的幾張面板素材**很好用，但物品 icon 會一直加，每加一張就要記得回來量、記得更新表——正是本專案一再踩的那種「改了要記得同步」的坑（見 C 類）。
- **解法**：**改成執行期自動量**。用 `Sprite.vertices`（sprite 的緊貼網格頂點）取內容的外接框，再反推 Image 的 `sizeDelta` 與偏移，讓「看得見的那塊」正好塞滿呼叫端給的內容框。實作在 `UI/IconFit.cs`，並掛在 `ItemIcons.Apply`（畫物品圖示的唯一入口）裡，所以背包／倉庫／鍛造／結算／抽選／HUD 一次全部生效。
  - 這條路**不需要貼圖開 Read/Write**（開了會多一份 CPU 記憶體，而且新圖還得記得勾——又是一個會忘的步驟）。前提是 icon 的匯入設定是 **Mesh Type = Tight**（`spriteMeshType: 1`，本專案的預設就是），Unity 才會依 alpha 產生貼合外形的網格。萬一哪張是 Full Rect，頂點就是四個角 → 自動退回「不縮放」，行為與以前相同、不會炸。
  - 呼叫端給的 `sizeDelta` 語意從此變成**內容框**（看得見的那塊會塞滿它），不再是「整張圖多大」。
  - ⚠ 副作用：留白多的圖，`Image` 的 rect 會被放大到**比格子還大**（例如藥水在 95×92 的格子裡 rect 是 183×183），只是多出來的部分全是透明。icon 是 `raycastTarget=false` 所以不影響點擊；但**若之後要在格子上加 `Mask`／`RectMask2D`，要記得這件事**。
  - 每次畫都要從「呼叫端最初設定的那個框」重算，不能拿「現在的 sizeDelta」當基準（會越畫越大）。原始值存在 `IconFitBox` 這個小元件上。（2026-08-07 記）

### E11. 半透明的 UI 疊色「比調的時候重很多」——專案是 Linear 色彩空間
- **症狀**：滑鼠移到裝備欄上，整格變成一大塊黃色看板。程式裡寫的是 `alpha = 0.22`，照直覺應該只是「微微發亮」。反過來也有：壓黑用的遮罩明明寫 0.6，實際看起來根本沒暗多少。
- **原因**：專案設定是 **Linear 色彩空間**（`ProjectSettings.asset` 的 `m_ActiveColorSpace: 1`）。Linear 的混色是在「線性光」上做的，不是人眼直覺的 sRGB。結果只有一句話：**亮色疊在暗底上比你以為的重很多；暗色疊在亮畫面上比你以為的淡很多。**
  實測：`(1, 0.82, 0.3, α=0.22)` 疊在 RGB(20,20,20) 上——
  Gamma 空間會是 **RGB(73, 62, 32)**，Linear 空間是 **RGB(129, 106, 41)**，
  等於「看起來像 Gamma 的 α≈0.45」，**足足重了一倍**。（作者截圖量出來是 (128, 110, 56)，與 Linear 的預測吻合。）
- **怎麼判斷是不是這個問題**：把畫面上那塊顏色的 RGB 量出來，用 `a*C^2.2 + (1-a)*BG^2.2` 開 1/2.2 次方算一遍。**對得上 = Linear 疊色，不是有兩層疊在一起。** 這招也能反過來排除「是不是不小心兩個高亮同時亮著」——兩層疊起來會明顯更亮。
- **解法**：
  1. **不要照 Gamma 的直覺填 alpha**。深底上的亮色高光，α 大概要填「感覺值的一半」。
  2. **更好的做法是別用大面積半透明**。背包 hover 就是改成**只描邊不填滿**（`UI/SlotOutline.cs`）——格子再大也只是一圈線，跟面積脫鉤，順便把「滑鼠在這裡」和「這格可以放」兩種提示從視覺上分開。
  3. ⚠ **不要拿著清單一次全改**。這些數值當初都是「看畫面調到順眼」定下來的，也就是說**它們本來就已經是 Linear 下看起來對的值**；真正會出事的是「同一個數值後來被套用到大很多的區塊」（背包 hover 就是：舊裝備欄 104×162 → 新的 221×258，面積 3.4 倍）。所以是**看到哪裡不對再回頭查**。
- 全專案寫死的半透明數值清單（含等效 alpha）已掃過一遍，見 2026-08-07 的對話產出；重點是 `UIManager.backdropColor`（0.60 → 等效只有 0.34，視窗背後其實沒那麼暗）、`UIBuilder` 輸入框底（0.10 → 等效 0.30）、三個面板的按鈕 hover/pressed tint（0.16/0.28 → 等效約兩倍）、文字陰影（0.85 → 等效 0.58）。（2026-08-07 記）

### E12. 調加色（Additive）圖層的亮度，`_Intensity` 這個數字騙人
- **症狀**：兩個加色圖層疊在一起，A 的 `_Intensity` 是 1.4、B 是 0.85，照理 A 該亮很多，實際上 B 完全把 A 壓過去、A 等於不存在。怎麼微調 `_Intensity` 都救不回來。
- **原因**：`Custom/AuraGlow` 的算式是
  `col = 貼圖RGB × 貼圖alpha × 顏色rgb × 顏色a × _Intensity`
  **貼圖自身的 alpha 會先乘一刀**，而且兩張圖差很多。實例：佛光 `buddhaLight_01.png` 中心 alpha 只有 **0.549**，卍字 `Manji.png` 是白色去背、筆畫 alpha 是 **1.0**。再乘上佛光的明滅係數（0.45~1.0，中值約 0.7）：
  - 佛光實際量 = 0.55 × 0.7 × 1.4 = **0.42**
  - 卍字實際量 = 1.0 × 0.30(color.a) × 0.85 = **0.43**
  兩者**完全打平**。而卍字是實心筆畫、面積又大 1.85 倍，柔光的佛光自然完敗。
- **解法**：**比較兩個加色圖層的強弱，一定要把「貼圖 alpha × 顏色 a × `_Intensity`」整串算出來，不能只看 `_Intensity`。** 拿 Python/小工具把 PNG 的不透明區平均 alpha 量一下最快。
- **順帶兩條**：
  1. **加色的紫疊在暖色光池上會變粉紅**。加色只能往上加，紫的紅通道加到本來就偏紅的地板 → 粉紅。想在暖光裡還讀得出紫，紅通道要壓低（實測 (0.62,0.30,0.95) → 改 (0.40,0.16,0.98) 才對）。
  2. **不要拿自己合成的暗場景估參數**。實機的提燈光池比想像中亮得多（從截圖量到暖光池中心約 RGB 0.47/0.37/0.27），照合成圖調好的值一進遊戲就被洗掉。**要估疊色參數就拿實機截圖量地板亮度再算。**（2026-08-17 記）

### E13. 兩個「發光」的圖層疊在同一個位置，永遠只看得到其中一個
- **症狀**：想在一個發光的圓上再疊一個發光的符號，讓兩個都看得清楚。把圓調亮 → 符號不見；把符號調實 → 圓不見；中間值 → 兩個都糊。調了三輪 alpha 都無解。
- **原因**：**這是零和的，不是參數問題。** 兩層同位置、同色相、同時出現，而且**都靠「比較亮」被看見**——在加色混合下，亮度是唯一的可辨識維度，一層佔了另一層必然讓位。繼續調 alpha 是在同一個維度裡搬預算，跳不出去。
- **解法**：**讓兩層不要爭同一個維度。** 最有效的是明暗反相——
  - 一層維持**加色發光**（亮）
  - 另一層改成 **alpha 混合的暗色剪影**（吃光）
  在被照亮的地板上，暗剪影的輪廓極清楚，而且完全不跟發光層搶亮度。
  ⚠️ **暗剪影必須畫在光的「上面」**（`sortingOrder` 較大）。畫在下面會被加色光直接填亮而消失。
- **推論**：**加色（`Blend One One`）永遠做不出「不透明」**——它只讓底下變亮，遮不住任何東西。所以「把加色圖層的 alpha 調到 1 讓它變實心」這件事本質上做不到，要實心就得換成 alpha 混合。
- 同一條道理的另一個版本見 [E11](#e11-半透明的-ui-疊色比調的時候重很多專案是-linear-色彩空間)：**優先用明暗對比與描邊，別靠大面積半透明。**（2026-08-17 記）

---


### E14. 角色「可以變大」之後，所有靠 `transform.position` 或 `SpriteRenderer.bounds` 定位／定大小的東西全部要重驗

- **症狀**：血統二階體型 1.5 倍之後，佛光光環縮在肚子上、雷擊劈到肩膀而不是腳、集氣光圈沉到小腿、被打時被擊退得比一階遠。
- **原因**：三個平常成立、角色一變大就同時失效的**隱含假設**。
  1. **`transform.position` ≒ 身體中心**。玩家 sprite 是置中 pivot，所以 transform 一直約等於身體中心，大家就直接拿它當「角色在哪」。改成腳底錨點放大之後，身體整個往上長，transform 變成「腳踝附近」——釘在 transform 上的光環、光圈全部會沉下去。
  2. **`SpriteRenderer.bounds` ≒ 可見身體**。bounds 含不含四周透明留白，取決於 sprite 的 mesh 型別（Tight／FullRect），在「執行期 `Sprite.Create`」這條管線上**沒有保證**。拿它當「腳在哪」會偏低、當「多高」會偏大。
  3. **寫死的世界半徑就是「剛好」**。佛光的 `Radius=1.2` 對 1.95 高的角色剛好罩住，對 2.92 高的就變成比身體還窄——它從來沒看過身體，只是以前身體只有一種高度。
- **解法**：
  - 幾何**不要用 bounds 猜**，改從縮放參數解析算：`PlayerAnimator` 在 `Setup` 時把各動作的「可見高」與「可見腳底相對 transform 的位移」算好存起來，`PlayerController` 對外提供 `VisibleBodyHeight` / `FeetWorldPos` / `BodyCenterWorldPos`。要蓋住身體的用 `BodyCenterWorldPos`，要對準腳的用 `FeetWorldPos`。
  - 會**撐過體型變更**的持續型效果（影子、佛光光環、集氣光圈）集中在 `PlayerController.RefreshBodyScaledVisuals()` 重新對齊；**之後再加這類效果記得在那裡補一行**。
  - 寫死半徑的地面特效改成支援 **per-instance 半徑倍率**（`GroundEffectInstance.Radius = _data.Radius × _radiusScale`）。⚠ 絕對不要就地改 `_data.Radius`——那是 GroundEffectTable 的一列、**全遊戲共用同一個物件**（同 RecipeTable 共用配方的坑）。
  - 被圖寬帶著跑的**數值**要補償回去：擊退距離是「角色圖寬 × 百分比」，圖變大就退更遠；`HitReactionHandler.WidthScaleCompensation` 把體型倍率除掉，維持「體型是視覺、不動數值」。
- **通則**：**引入「同一個角色會有多種顯示大小」這件事，等於把所有寫死的世界單位常數都變成可疑的。** 動手前先全專案搜一遍 `transform.position`（特效生成處）、`bounds`、以及任何以世界單位寫死的半徑／偏移，逐一問「這個數字是不是預設角色只有一種大小」。

### E15. 天上劈下來的落雷，竟然被場景裡的一個燭台／花瓶擋住（表演層被地上物蓋掉）

- **症狀**：血統變身的落雷在紅嫁衣關卡播放時，**一部分電柱從地上物「底下」穿過去**。同一批地圖裡，頭上傷害數字、過關的離場卍字、傳送門特效、變身煙塵也都會被同一個物件蓋住。換到別張圖就正常。
- **原因**：`MapDepthSort` 的舊公式是 `1000000 + zOrder*10000 + round(-Y*100)`，**靠 16-bit 溢位繞回**落到 +16960 那一帶（見 E4）。這在 `zOrder = 0` 時是安全的（11960~21960，剛好在表演層 22000 之下），但 **`zOrder = 1` 會把整帶往上平移 10000 → 21960~31960，直接騎到所有表演層頭上**。
  - `zOrder = 1` 的正當用途是「整層往前」——桌上的花瓶、供桌上的香爐/供盤/燭台、屏風。紅嫁衣的祠堂、書房、柴房、客廳各有幾個，實測 sortingOrder 到 **27131~27409**。
  - 於是：掉落物名稱標籤(20000)、雷柱與大部分特效(22000)、變身電弧/煙塵(22050/22100)、傳送門與傷害數字(24000)、煙火與離場卍字(25000)——**六個表演層全部被一個燭台蓋住**。只有場景火雨(30000) 倖免。
  - 這是**既有的系統性 bug**，不是落雷帶進來的；落雷只是第一個「跨越整個畫面、一定會跟高處物件重疊」的效果，所以最先被看見。
- **解法**：`MapDepthSort` 改成**完全不繞回**——低基底 ＋ 把 Y 的貢獻夾在一個 band 內：
  ```csharp
  SortBase = 7000; BandStep = 6000; MinZOrder = -1; MaxZOrder = 1;
  int y = Mathf.Clamp(Mathf.RoundToInt(-worldY * 100f), 0, BandStep - 1);
  return SortBase + Mathf.Clamp(zOrder, -1, 1) * BandStep + y;
  ```
  世界帶因此固定在 **1000~18999**，全部表演層（20000 起）都在它之上。Y 的貢獻夾住還順便讓「zOrder 大的一定在前面」變成硬保證（舊版在地圖高度 > 100 單位時會失效）。zOrder 超出 ±1 會夾住並印一次警告——放任它往上跑就是重演這個 bug。
  - **驗算過再改**：386 個既有地上物兩兩比較，新舊公式的相對順序**完全一致**（世界內的遮蔽關係零改變），而且低位固定層（背景 -1000、可走地上物 5、地面特效 8、星星 20）仍在世界帶之下。
  - ⚠ `DipanProj_MapEditor` 的 `ObjectView.cs` 有**同一條公式的鏡像**，已一起改。
- **通則**：**排序值的「帶」要當成資源來規劃，不能靠繞回碰運氣。** 任何新的固定層，先看 `MapDepthSort` 檔頭那張配置表確認落點；任何會讓世界帶變寬的改動（加 zOrder 層數、放更高的地圖），先確認帶頂仍低於 20000。
### E16. 角色的頭被地上物蓋住（而且不管站多前面都蓋）
- **症狀**：玩家明明站在屏風的**前面（畫面下方）**，屏風卻畫在他頭上。走到更下面也沒用，永遠被蓋住。
- **原因**：那個地上物的**「層」被設成 +1**。看 [MapDepthSort](../DipanProj_Main/Assets/Scripts/Map/MapDepthSort.cs) 的配置表就懂了——`層 +1` 是 **13000~18999**，而**玩家與怪物永遠在 `層 0` 的 7000~12999**。所以 `層 +1` 的意思實際上是「**永遠畫在角色前面、完全不參與 Y 排序**」。
  實測紅嫁衣書房的 `furniture_bamboo_screen2`：層=1 → sortingOrder 13264；玩家站在 y=-3.5 是 7350，站到地圖最底 y=-10 也才 8000。**怎麼走都贏不了。**
- **`層 +1` 是給什麼用的**：**放在別的東西上面、玩家永遠站不到它前面的小東西**——桌上的花瓶、供桌上的香爐/供盤/燭台。這類物件的 sortKey（放置 Y）比它腳下那張桌子還高，不往前提一層就會被桌子蓋住。**大型落地家具絕對不能設**。
- **解法**：在地圖編輯器選取該物件 → 面板上會顯示「層 1」→ 按一次「**下移層**」變成「層 0」→ 存檔 → 跑 `Sync Map Assets`。
- **編輯器已補強顯示**（2026-08-19）：① 物件面板在「上移層／下移層」上方獨立一行寫出語意並上色（層 +1 橘色警示「⚠ 永遠蓋住角色」）；② **不點選也看得到**——物件工具下，場景上會把所有 `層 ≠ 0` 的物件畫外框常駐標示（橘框＋「＋」= 層 +1、藍框＋「－」= 層 -1，見 `ObjectSelectionOverlay`）。原本只有面板最上面那行結尾小小一個「層 1」，實際上等於看不到。
- **為什麼是現在才發現**：地上物碰撞改成貼合圖形之後（見 **B9**），玩家能走到的範圍變大，才第一次站到「屏風視覺會蓋到人」的那格。**這條 bug 本來就在，只是以前被過大的碰撞框擋著看不到。**
- ⚠ **同一批要留意的還有 6 個**（全專案 `層≠0` 只有 9 個）：客廳1 的 `decor_vase`、祠堂的 `ritual_incense_burner`／`ritual_offering_plate`×2／`light_candlestick`×2、柴房的 `weapon_sacredLamp`。這些都是正當用途的小型桌上物，但**碰撞縮小之後玩家能更靠近，同樣的遮蔽感會偶爾出現**，看到再逐一處理即可。（柴房的 `container_wood_crate` 是 `層 -1`，那是「壓在別人下面」，不受影響。）
- **通則**：**「整層往前/往後」這種全域旗標是逃生口，不是排序工具。** 它會讓該物件退出 Y 排序系統，一旦玩家能站到它附近就露餡。能用位置解決的就不要動層。（2026-08-19 記）

### E17. 編輯器裡想看的 sprite 永遠被半透明疊加層蓋住（GL 疊加層一定畫在 sprite 之上）
- **症狀**：地圖編輯器加了「傳送點對位」預覽（把遊戲真正的傳送點特效以 SpriteRenderer 畫在畫布上）後，那個光盤被 trigger 格子的半透明藍色蓋掉，只看得到一片藍紗，根本分不出外型的實際位置。
- **原因**：編輯器所有的疊加層（`TriggerOverlay` / `WalkableOverlay` / `ObjectSelectionOverlay` / `SceneFxOverlay` / `LightOverlay`）都是在相機的 **`OnPostRender`** 裡用 GL 畫的。`OnPostRender` 顧名思義是**整個場景（含所有 SpriteRenderer）畫完之後**才跑 —— 所以**它畫的東西必然疊在所有 sprite 之上，跟 sortingOrder 完全無關**。sprite 的排序值調到 32766 也一樣被蓋。
- **解法**：**讓那塊不要畫**，而不是調透明度或排序。`TriggerOverlay` 在對位模式下把傳送點的格子從 `GL.QUADS` 填色改成 `GL.LINES` 只畫外框——踩踏功能區照樣看得見，中間完全讓給外型動畫。調 alpha 是無效的：再淡也是一層紗，而且會同時吃掉動畫的顏色。
- **要真的「浮上來」只有一條路**：另開一台 depth 更高的相機、只渲染該圖層（同主遊戲互動星星的 `InteractOverlay` 作法）。編輯器這邊為了一個對位預覽不值得，就用「不畫」解。
- **通則**：**`OnPostRender` 的 GL ＝ 最上層，沒有商量餘地。** 在編輯器裡要讓某個 sprite 看得清楚，能做的只有「叫疊加層讓開」或「另開相機」，改 sortingOrder 是白費力氣。（2026-08-20 記）

### E18. 系統提示「跳了等於沒跳」——開著背包操作，訊息被背包蓋住
- **症狀**：喝過夜裔血統之後，開背包點殭屍血統藥劑，**畫面完全沒反應**；把背包關掉才看到「你的血脈已定為『該隱』，這一世不能再改變」那行提示還停在畫面上。玩家的認知是「這個按鍵壞了」，而不是「有訊息但我沒看到」。
- **原因**：`AlertPanel`（全遊戲系統訊息 toast 的唯一入口，32 處呼叫 `AlertPanel.Toast`）的 `Layer` 是 `UILayer.HUD`＝sortingOrder 0，而背包是 `UILayer.Window`＝100。**訊息確實跳了，只是畫在背包底下。** toast 顯示 1.6 秒 + 淡出 0.4 秒，玩家關背包時多半已經淡完，於是連「有訊息」都不知道。這不是背包獨有——任何 Window 面板（倉庫、鍛造、抽選、劇本、設定）開著時都一樣，而「操作失敗要給理由」這件事**幾乎都發生在面板開著的時候**，等於系統訊息最需要被看到的場合正好全部失效。
- **解法**：新增 `UILayer.System`（2026-08-22），`AlertPanel` 改掛這層。sortingOrder **刻意不照 `i * 100` 排**——那會是 400，反而被 `TutorialHintPanel`(460)、`GuideFingerPanel`(500) 這些自己 `overrideSorting` 的面板蓋住；改成 **700**，卡在「蓋過所有一般 UI 與教學疊層」與「不蓋過全螢幕接管的演出（Intro 1000 / 隧道 1200 / 影片 1300 / 劇情 5000）與黑幕（30000）」之間。完整排序表在 [UI_SYSTEM.md](UI_SYSTEM.md)「分層與排序」。
- **⚠ 放進 System 層的東西一律 `raycastTarget = false`**：它蓋在所有視窗之上，忘記關掉的話會靜默擋住底下面板的點擊——症狀又會變成另一種「點了沒反應」。`AlertPanel` 的底板與文字本來就都是 false（`UIBuilder.Text` 預設關掉），所以這次沒踩到。
- **通則**：**「回饋能不能被看到」跟「回饋有沒有發出」是兩件事，而前者是分層決定的。** 一個訊息系統掛在會被蓋住的層，等於在最需要它的情境下自動關閉；而且失敗是靜默的——程式看起來完全正常，log 也有，只有玩家看不到。另一條：**這種 bug 的症狀會偽裝成「功能壞了」**，先問「是不是根本沒跳」之前，先確認「是不是跳了但看不到」。

### E19. 把角色藏起來，結果影子還留在原地（而且暗場景的光圈也留在原地）
- **症狀**：劇情要「這段戲沒有主角」，於是把玩家 `SetActive(false)`。人是不見了，但**腳下那塊橢圓影子還停在原地**；在幽暗/噩夢氛圍的地圖上，**提燈的光圈也還亮在同一個位置**，空地上浮著一圈沒有主人的光。另外劇情演員走位到那附近會被一個看不見的東西撞開。
- **原因**：三件事各自獨立，共同點是**它們都不住在角色物件的階層裡，或不由角色的 renderer 決定**。
  1. **影子是獨立 GameObject，不是子物件**——`BlobShadow` 刻意把影子生成在角色外面（否則會被角色的 `flipX` 與體型縮放二次影響）。停用角色 → `BlobShadow.LateUpdate` 也跟著停 → 影子**既不跟隨也不消失**，定格在最後一幀的位置。
  2. **氛圍光圈由 `AtmosphereController` 每幀重算**，它讀的是「玩家 transform + 裝備的 `LightRadius`」，跟角色的 `SpriteRenderer` 開不開完全無關。
  3. **碰撞體不會因為看不見就失效**——`Collider2D` 只看 `enabled`，A\* 走位照樣被擋。
- **解法**：**不要停用整個角色物件，改成逐項關掉**。已收成 `Assets/Scripts/Cutscene/PlayerVisibility.cs`（劇情演出的 `hidePlayer` 勾選走這支）：關 `SpriteRenderer`（含子物件）＋ `BlobShadow.SetVisible(false)`（新加的 API）＋ `Collider2D`（含子物件），並由 `PlayerVisibility.IsHidden` 讓 `AtmosphereController.BuildLights` 跳過玩家光源。位置在隱藏時記下來，現身時放回去。
- **通則**：**「把某個東西藏起來」不是一個操作，是一份清單。** 判斷準則是「有哪些系統是**主動去找**這個角色、而不是**掛在**這個角色底下的」——影子（獨立物件）、光照（每幀掃）、物理（獨立的 collider 世界）、AI 目標搜尋、鏡頭跟隨，這些都不會因為 renderer 關掉而停止。之後要藏怪物、藏 NPC 時同一份清單重跑一次。
- **相關**：[SHADOW.md](SHADOW.md)（影子為什麼是獨立物件）、[ATMOSPHERE.md](ATMOSPHERE.md)（光源怎麼收集）、[CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §7。（2026-08-22 記）

### E20. 同一句台詞，加了 `\n` 手動換行之後字忽然變大一號
- **症狀**：對話框台詞從「這個給你吧，就剩兩張了」改寫成「這個給你吧`\n`就剩兩張了」（只是想控制斷句位置），字**明顯大了一號**。同一段對話裡幾句話字級忽大忽小。
- **原因**：uGUI 的 `resizeTextForBestFit` 是「**在這個框裡找一個塞得下的最大字級**」——它看的是排版結果，不是文字內容。兩行各 5 個字塞得下更大的字，一行 11 個字要折行、只能用小字，所以**作者眼中「同一句話」的兩種寫法，在 best-fit 眼中是兩種完全不同的排版問題**。
- **同一家族的第二個來源（更難發現）**：`MonsterSpeechPanel` 的底板是**兩張水墨泡泡隨機輪流**，而兩張的「奶油內文區」大小不一樣（220×98 vs 200×83）⇒ **同一句台詞每次講都可能是不同字級**，而且沒有規律可循。
- **解法**：不用 best-fit，**自己算字級**（`MonsterSpeechPanel.ComputeFontSize`）：
  ① 用**固定的參考框**（兩張底板取較小的那個奶油區）→ 抽到哪張都一樣大，而且大的那張一定塞得下；
  ② 中日韓字寬 ≈ 1 個字級、ASCII ≈ 0.55、行高 ≈ 字級 × 1.15，從上限往下找第一個塞得下的；
  ③ **有手動 `\n` 時先跑一輪「每段各佔一行」**（尊重作者排的斷句，否則字級會被挑到「那一段還要再自動折一次」的大小，手動的兩行變成三行、斷在作者不要的地方）；
  ④ `verticalOverflow = Overflow` 兜底，估不準時寧可溢出一點也不要被裁掉。
- **通則**：**「自動縮放到剛好塞滿」這類 API 的輸入是排版，不是語意。** 只要作者能用不影響語意的方式（換行、標點、空格）改變排版，同一段內容就會得到不同大小——而作者的心智模型是「這句話有多長」。要一致就得自己定義規則：**先決定「什麼東西應該決定大小」（這裡是字數），再讓其他東西（斷行位置）不影響它。**（2026-08-22 記）

## F. 戰鬥 / 傷害 (Combat)

> 系統說明見 [COMBAT.md](COMBAT.md)。


### F1. 怪物碰到玩家不扣血(用 OnCollision / IsTouching 偵測接觸完全沒反應)
- **症狀**:做「怪物接觸玩家就扣血」,用 `OnCollisionEnter2D` / `OnTriggerEnter2D` / `Collider2D.IsTouching` 偵測,但怪物明明貼在玩家身上卻**從不觸發**。
- **原因**:專案的 **Layer Collision Matrix 把 `Enemy×Player` 關閉**(設計上怪物穿過玩家、不互推,見 [ARCHITECTURE.md](ARCHITECTURE.md))。而 `OnCollision*`、`OnTrigger*`、`IsTouching` **全部依賴物理系統的碰撞配對**——該層對被關掉,就不會產生任何接觸事件/配對,自然偵測不到。**任何「靠物理事件偵測 Enemy↔Player 接觸」的機制都會中這個雷**。
- **解法**:用 **`Physics2D.Distance(colliderA, colliderB)`**(回傳 `ColliderDistance2D`,讀 `isOverlapped` / `distance`)做**幾何重疊判定**——它直接算兩個 collider 的幾何距離,**不經過碰撞矩陣**,所以層對關閉也照算。`EnemyContactDamage` 就是每幀對玩家 collider 做這個判定;反覆接觸由玩家自己的無敵時間(`HitReactionHandler`)節流。同理:任何需要「忽略碰撞矩陣的純幾何查詢」都可用 `Physics2D.Distance` / `OverlapCircle` 這類 query API(它們吃的是 LayerMask 參數,不看矩陣)。

### F2. `[RequireComponent(typeof(Collider2D))]` 用抽象基底類別會出問題
- **症狀**:在元件上標 `[RequireComponent(typeof(Collider2D))]`,執行期 `AddComponent` 該元件時、若物件還沒有任何 collider,Unity 嘗試自動補一個 `Collider2D` 卻失敗(`Collider2D` 是抽象類別,不能實例化)。
- **原因**:`RequireComponent` 會在缺件時嘗試 `AddComponent(該型別)`,但 **`Collider2D` 是抽象基底**(具體的是 `BoxCollider2D` / `CircleCollider2D`…),無法被實例化。
- **解法**:別對抽象基底用 `RequireComponent`。本專案 `EnemyContactDamage` 改成**不標 RequireComponent**,程式內 `GetComponent<Collider2D>()` 取用 + null 檢查即可(怪物的 collider 由 `MonsterController.AutoAdjustCollider` 保證存在)。要強制需求就指定**具體**型別(如 `BoxCollider2D`)。

### F3. Console 狂洗 `transform.position assign attempt for 'Bullet(Clone)' is not valid. Input position is { NaN, NaN, 0 }`
- **症狀**:發射（尤其**拋物線／火焰拋擲彈**）時 Console 每幀噴一堆 `BulletInstance.cs` 的 `transform.position ... NaN` 錯誤。
- **原因**:子彈的**落點/速度被算成 NaN**，之後 `transform.position += Velocity*dt` 每幀寫入 NaN 位置就報錯、且壞彈不會自己消失 → 洗版。拋物線的落點源頭是 `Camera.main.ScreenToWorldPoint(Input.mousePosition)`——滑鼠在遊戲視窗外、或相機該幀尚未就緒時，這個世界座標偶爾會是 NaN/Inf，一路傳進 `ParabolicBehavior` 的 `_arcEnd`，`Lerp` 到 progress>0 後整個變 NaN。
- **解法**:兩層防護。① **來源清洗**：`PlayerController.ShootParabolic` 取得滑鼠世界座標後檢查 NaN/Inf，異常就退回「玩家前方一格」，這發仍打得出去。② **彈道核心安全網**：`BulletInstance.Update` 在寫入位置前檢查位移是否有限，非有限就直接銷毀該彈——**任何來源**的 NaN 彈都會被擋掉、不再洗版（通用防呆）。

---

## G. 角色圖像 / 序列化 (Character Visuals & Serialization)

> 角色立繪／走路動畫的完整設定流程見 [CHARACTER_SETUP.md](CHARACTER_SETUP.md)。
>
> ⚠️ 本章之後會**接回 F 系列（F4 起）**——G 是後來插進來的，編號沒有重排，`### F4` 以後仍屬「F. 戰鬥 / 傷害」。用搜尋找編號即可，別依賴閱讀順序。

### G1. 角色進遊戲變一團黑／剪影，但在 prefab 預覽裡看得到
- **症狀**：主角（或任何角色）放進遊戲後變成全黑剪影、只剩輪廓；但在 Prefab 編輯／預覽視窗裡卻正常（只是偏暗）。換不同角色圖都一樣黑——「從第一個角色就這樣」。
- **原因**：`Player.prefab` 的 **SpriteRenderer 的 `Color`（色調 Tint）被設成很暗的顏色**（實際值 RGB ≈ 0.12 / 0.07 / 0.07，約 10% 亮度的暗褐色）。SpriteRenderer 會把圖片**乘上**這個顏色，等於把整張圖壓暗；再進到本來就昏暗的氛圍地圖（提燈光圈，見 [ATMOSPHERE.md](ATMOSPHERE.md)）就直接變純黑剪影。因為 Tint 綁在 prefab 上、與圖片無關，所以**換哪張角色圖都一樣黑**。材質是 `Sprites-Default`（無光照）本身沒問題，純粹是 Tint。
- **解法**：把該 SpriteRenderer 的 **Color 設回純白**（RGBA 全 255，尤其 **A=255** 別半透明）。Inspector：選 Player → Sprite Renderer → Color 色塊 → 設白。白色 = 不染色 = 顯示圖片原色。改完氛圍地圖裡仍會偏暗有氣氛，但不再是純黑。**通則：角色「整張均勻變暗／變色」先檢查 SpriteRenderer 的 Color，不是圖、不是材質、也不是光。**

### G2. UI（背包/面板）在大螢幕看起來糊糊的、有塊狀髒點
- **症狀**：小的 Game view 或遠端桌面看還好，回家用實體大螢幕全螢幕看，背包 / 面板背景與 icon「糊糊的、說不出哪裡不對」。
- **原因**：`Resources/UI` 下的貼圖匯入時被 **Compressed（BC/DXT，quality 50）**＋ **Bilinear**。壓縮會在有漸層/細節/文字的 UI 圖上留 4×4 塊狀髒點，Bilinear 再糊一層；面板小的時候看不出來，放大到全螢幕就全暴露。與像素風格無關，純粹是匯入設定。
- **解法**：把 UI 貼圖的 **Compression 改 None（未壓縮）**。本次已把 `Resources/UI` 下全部 39 張改掉；並加了 `Assets/Editor/UITextureImportSettings.cs`（`AssetPostprocessor`）——**以後丟進 `Resources/UI/` 的新圖，第一次匯入就自動套「不壓縮／關 Mipmap／Sprite／Max Size≥2048」**，不必手動改（只在首次匯入套，之後手動微調不會被蓋）。**通則：UI 這種要銳利的圖一律不壓縮、關 Mipmap，且畫得 ≥ 顯示尺寸別放大。** VRAM 代價很小（UI 量少），但別對「全遊戲所有圖」無腦套。
- **補充（驗收方式）**：**遠端桌面不能拿來驗收畫質**——遠端會把畫面重新有損壓縮再串流，會同時掩蓋好與壞。像素級的銳利/粗糙一律回本機實體螢幕看。

### G3. 場景在大螢幕顯得「低解析度 / 粗糙」（非整數放大 + 硬像素）
- **症狀**：場景（地磚/家具）在大螢幕看起來顆粒粗、邊緣毛躁，像低解析度；小視窗卻還好。
- **原因**：相機是**固定世界單位**（`MapCameraController` 跟隨模式 orthoSize 固定），畫面永遠顯示同樣多的世界，**視窗越大＝每個源像素被放越大**。加上場景圖用 `FilterMode.Point`（`MapSpriteLoader`）且**沒有 Pixel Perfect Camera**，非整數倍縮放會讓像素大小不均、邊緣閃爍。源圖其實不是低解析度（`TileNativePx = 256`），顆粒感有一部分是 AI「像素風」源圖本身畫進去的。
- **解法**：`MapSpriteLoader` 加了可切換常數 **`SceneFilterMode`**。實驗比較後**定案採 `FilterMode.Point`（硬派像素，預設）**；`Bilinear`（柔化）保留作比較用。執行期可用 **PerfHud（按 P）→「場景濾波(F)」按鈕或按 F** 即時來回切換（`ToggleSceneFilterMode` 會把已載入的貼圖即時重套濾波）——切換是臨時預覽，重開回預設 Point。注意 F 只在 P 面板開著時生效（避免與「靠近按 F 拾取」互動鍵衝突）。**未採 Pixel Perfect Camera**——它要求整數倍縮放與統一 PPU，會跟本專案的 zoom / 整張地圖模式 / 平滑跟隨 / 混雜 PPU 直接衝突，且美術是 AI 生成點陣圖非手排像素，回報低。若之後想更清晰，正解是照 [AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md) 把場景源圖重產得更細緻（顆粒更小）。

### G4. 改了程式裡 `public` 欄位的預設值，遊戲卻沒生效（例：VfxManager.SortingOrder）
- **症狀**：把某 MonoBehaviour 的 `public int SortingOrder = 100;` 在程式改成 `= 22000`，進遊戲卻還是舊行為（如冰凍特效仍被地上物蓋住）。
- **原因**：`public`（或 `[SerializeField]`）欄位的值是**序列化在場景 / prefab 檔裡**的。一旦該元件被放進場景（如 `VfxManager` 掛在 MainScene），Unity 讀的是**場景檔存的那個值**，**程式碼裡的預設值只對「還沒序列化過的新實例」有效**（例如純 `AddComponent` 出來的）。所以改預設值不會動到已存在場景/prefab 上的那顆。
- **解法**：改**場景/prefab 上那顆**——在 Inspector 選到該物件把值改掉；或直接改場景/prefab 檔裡序列化的那行（如 MainScene 的 `SortingOrder: 100` → `22000`）。⚠️ 若場景正開著，改檔後要讓 Unity **Reload 場景**才會載入新值；且**別在改檔後又存一次場景**，否則記憶體的舊值會覆寫回去。想「一律吃程式值」可改用 `const`（不序列化，如 `DamageNumberManager.SortingOrder`）或在 `Awake` 內強制指定。


### G5. 玩家「生成的同一幀」對 PlayerAnimator 下指令沒反應（例：進圖立刻趴地失敗）
- **症狀**：進圖時想讓玩家立刻擺出某個逐格動畫姿勢（如睜眼醒來的趴地定格），第一次生成玩家時完全沒效果；但之後換圖再進（玩家已存在）就正常。
- **原因**：`PlayerAnimator.Setup`（載入 idle/walk/dead 逐格幀）是在 `PlayerController.Start()` 呼叫的；而 `MapManager.PlaceAndSetup` 在 Instantiate 玩家後**同一幀同步**繼續往下跑——此時只有 `Awake` 執行過、`Start` 還沒跑，幀陣列全是 null，`HoldLyingPose()` 之類查 `_dead` 的 API 判定「沒圖」直接跳過。之後的進圖玩家早已 `Start` 完，所以「只有第一次會壞」，非常隱蔽。
- **解法**：對「剛生成的物件」要用到其 `Start` 初始化結果時，**至少等一幀**（丟進 coroutine / `TriggerChainRunner.NextFrame`）再下指令。本案把「趴地＋起身」整段搬進 `MapManager.FireEnterTriggersRoutine`（協程，載入頁關閉後才跑，`Start` 必已執行），`PlaceAndSetup` 只記 `_wakeUpWanted` 需求旗標；視覺上趴下前的站姿瞬間被睜眼開頭的全黑（眼皮閉合）蓋住。**通則：Instantiate 後同幀只能依賴 `Awake` 做完的事；依賴 `Start` 的操作要延一幀。**

### G6. 某些血統的攻擊動畫「完全播不出來」，同系列的其他血統卻正常
- **症狀**：Cain 與 Crimson Count 攻擊時看不到任何攻擊動作（就是站著），同系列的 Nightborn 完全正常。素材、檔名編號、catalog 都查過沒問題。
- **原因**：**不是那兩組壞掉，是所有血統的攻擊動畫從來都只播得到前兩幀**，那五個「正常」的只是剛好第 1 幀就已經是攻擊姿勢。三件事乘起來：① `PlayerController.AttackAnimLinger = 0.12f`——攻擊姿勢只維持 0.12 秒；② `PlayerAnimFPS = 12`——一幀 0.083 秒，0.12 秒只夠播 **1.4 幀**（全套 25 幀，用到 8%）；③ `PlayerAnimator.SetState` 換狀態時 `_idx = 0`——每發都從第 0 幀重來，永遠推不到後面。於是「看不看得出來」完全由**第 1 幀長什麼樣**決定。量過七組素材的 attack 第 1 幀與 idle 的輪廓差異：Nightborn 85%、旱魃 81%、Base 79%、殭屍 61%、毛殭 54%（第 1 幀就已經是施法姿勢）vs **Crimson Count 20%、Cain 18%**（前 6 幀還在起手，跟站著沒兩樣）。分得乾乾淨淨。**前五組素材湊巧「開頭即定格」，把這個限制遮了好幾個月**，直到有兩組正常做了起手動畫才露餡。
- **解法**（2026-08-22）：攻擊動畫改成「按下／放開」邊緣驅動的**一次性**播放，並自動跳過起手：`PlayerSpriteLibrary.GetActionStartFrame` 比對 attack 各幀與 idle 站姿的輪廓，取第一個「差異達到該動作**自己峰值** `ActionStartPeakRatio`(0.6)」的幀當起播幀。**一定要用相對自己的峰值、不能用絕對門檻**——各血統動作幅度差很多（峰值 Cain 只有 25%、Nightborn 有 86%），任何絕對門檻都會對其中一邊失效。實測結果：Base/旱魃/Nightborn 起播幀 = 第 1 幀（不受影響）、殭屍/毛殭/Cain = 第 6 幀、Crimson Count = 第 4 幀。細節見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)。
- **順手查到的兩件事**：① **attack 不是無縫循環**——七組素材的「最後一幀 → 第 1 幀」接縫差異 23~52%，而相鄰幀平均只有 3~20%（只有 Base 是無縫的）；idle 則全部無縫（接縫 0.4~3%）。所以把 attack 當循環播本來就是錯的，它天生有起有收。② `StreamingAssets/.../Base/idle/` 裡有一張殘留的 `idle_01.png`（310×500，其他都是 256×256），`GameAssets` 裡沒有它——**Sync Map Assets 只推新檔、不刪來源已移除的舊檔**。catalog 目前正確地沒收它，但它排序在 `Actor-iso_*` 後面，挑檔規則一變就會變成第 26 幀。
- **通則**：**「只有某些角色壞掉」常常代表「所有角色都壞掉，只是其他角色的資料剛好蓋住了」。** 在動那兩組素材之前，先問「正常的那幾組為什麼正常」——這次的答案不是「它們是對的」，而是「它們碰巧不會踩到」。


---

### F4. 紅嫁衣 boss（BrainType=RedBridalGown）還是用追擊、不逃跑不召喚
- **症狀**：怪物有生出來、也會攻擊，但 boss 只會像一般怪一樣追玩家（`RedBridalGownBrain` 的逃跑＋召喚完全沒作用）。
- **原因**：`MonsterSpawner.LoadMonsterData` 讀 `BrainType`/`Weapon` 時**沒有 `.Trim()`**。CSV 欄位值常帶前導空白（例 `13,RedBridalGown,50, RedBridalGown, 14,...` → `BrainType = " RedBridalGown"`），`switch (data.BrainType){ case "RedBridalGown": }` 對不上 → 掉回 `default = new ChaseBrain()`。**其他怪剛好 default 也是 Chase，所以這個 bug 一直被藏著**，直到出現第一個非 Chase 的 BrainType 才爆。（`Weapon` 因為 `MonsterController.Initialize` 讀取時有 `.Trim()` 才沒中招，但 brain 是 Chase、根本不會呼叫 `MonsterWeaponUser`，所以也不召喚。）
- **解法**：`LoadMonsterData` 的 `data.BrainType = values[3].Trim();`、`data.Weapon = values[4].Trim();`（已修，2026-07-09）。**通則**：CSV 欄位拿來做字串比對（switch/等值）前一律先 Trim，別靠來源沒有空白。

### F5. 召喚物 vs 敵怪對打「忽勝忽敗」（有時 1 打 3 全身而退，有時 3 隻打不過 1 隻）
- **症狀**：玩家召喚的協戰怪與敵怪互毆，結果很不穩定、像擲骰子——同樣的隻數有時輕鬆全滅對方、有時反被秒殺。
- **原因**：接觸傷害是 `EnemyContactDamage.Update` **每一幀**對重疊的敵對目標結算一次；而**所有怪 `InvincibleTimeMs = 0`（無無敵時間節流）**，加上 `HP(3~10) ≤ ContactDamage(10)` 幾乎**一擊斃命**。於是「兩隻重疊的那一幀，誰的 `Update` 先被 Unity 呼叫，誰就先把對方打死」——`Update` 在同型元件間的執行順序**不保證**，於是勝負看起來是隨機的。**這套接觸傷害原本只為「怪→玩家」設計**（玩家自己有無敵時間 + `HitReactionHandler` 節流），從沒為「怪→怪」平衡過，陣營制把它暴露出來。
- **解法（資料/平衡，作者決定數值）**：
  1. **給會互毆的怪 `InvincibleTimeMs > 0`**（`MonsterData.csv`，建議先試 300~500）：`HitReactionHandler` 會做無敵時間＋白閃，把接觸傷害從「每幀」節流成「每 N 毫秒一次」，戰鬥就改由 **HP/傷害** 決定、不再靠 Update 順序＝穩定可預期。
  2. **平衡 HP vs ContactDamage**：目前近乎一擊必殺，想要「互毆幾下才分勝負」就拉高 HP 或調低 ContactDamage（例如雜兵 HP 拉到 30、接觸傷害降到 5）。
  3. （備案，若不想逐隻設 InvincibleTimeMs）可在 `EnemyContactDamage` 加「同一攻擊者對同一目標的重擊冷卻(dict 記 nextHitTime)」，但那也會改到「怪打玩家」的節奏，**優先用做法 1**。
- **狀態：✅ 已修（2026-07-09，改用系統機制、不靠調數值）**：關鍵是**「第一擊必互換」由系統保證**，而不是靠平衡數值。
  - **① 致死延後銷毀（核心）**：`MonsterController.Die()` 只標記 `_isDead`，真正 `Destroy` 延到本幀 `LateUpdate`。這樣「殺死這隻怪的那一幀」，這隻怪自己的 `EnemyContactDamage` 仍會執行一次 → **死掉也能還手**。⇒ 兩隻怪一接觸，不管誰的 Update 先跑、不管攻速差多少，第一下一定雙方互換傷害（攻速快/攻高/血少的玻璃大炮撞上去，也一定吃到對方的反擊，不能全程無傷輾壓）。
  - **② 攻速資料化**：接觸「之後多久打一次」＝攻速，做成 `MonsterData.csv` 的 `AttackInterval` 欄（秒，越小越快，空＝0.5），由 `EnemyContactDamage` 每隻怪各自用。第一擊互換（①）與攻速（②）分離：先互換、後看攻速/傷害/血量。
  - **③ 還原**：先前「偷調」的測試怪 HP/ContactDamage 已還原（ZhaYu HP10、幽靈 HP3、接觸傷害 10）。公平性由機制保證，數值單純由作者依設計調。
  - 傷害本身仍走中央 `CombatSystem`（見 [COMBAT.md](COMBAT.md)）；本則只解決「接觸的施加時機/互換」。

### F6. Boss（紅嫁衣）召喚出來的是玩家的怪（ZhaYu），不是她的家人幽靈
- **症狀**：玩家用御靈水晶（武器 13）召喚 ZhaYu；去打紅嫁衣 boss，她也召喚 ZhaYu，而不是家人幽靈——像是「召喚共用同一管道、大家召一樣的東西」。
- **原因**：**不是共用管道**。`SummonSystem.Cast(recipe, ...)` 讀的是**呼叫端各自傳入的 recipe**（玩家 `PlayerController.Shoot` 傳當前武器的配方、boss `MonsterWeaponUser` 傳自己武器的配方），兩邊 SummonIds 本來就各自獨立。真正原因是**資料填錯**：`MonsterData.csv` 的 boss（怪物 13 RedBridalGown）`Weapon` 欄填成 **13**（＝御靈水晶，配方 27、`SummonIds=1` 召喚 ZhaYu），應該是 **14**（紅嫁衣召喚家人，配方 26、`SummonIds=2|3|…|12` 召喚家人幽靈）＝boss 被裝上了玩家的測試水晶。
- **解法**：把 `MonsterData.csv` 怪物 13 的 `Weapon` 改成 `14`。**通則**：boss/怪物「召喚錯東西／用錯技能」先順 `MonsterData.Weapon → WeaponTable 的 RecipeID → RecipeTable 的 SummonIds` 這條資料鏈查是否指錯，多半是資料、不是程式（召喚共用 `SummonSystem` 但吃各自的配方）。

---

### F7. 召喚出來的怪出生在牆裡／不可走區，走不出來
- **症狀**：boss（或玩家）召喚時，怪出生在牆/不可走格（例：boss 逃到房間右邊，召喚環 `SummonRadius` 剛好探進右側牆），怪卡在牆裡動不了。
- **原因**：`SummonSystem.Cast` 原本只在施放者周圍 `SummonRadius` 環上隨機取點，**沒檢查該點可不可走**。
- **解法**：`SummonSystem.FindSpawnPos` 用 `Physics2D.OverlapCircle(p, 0.35, Environment|Water)` 驗證候選點，撞牆就換角度、由外往內縮找空位，都不行退回施放者腳下（一定可走）。**通則**：任何「在某點生實體」的機制（召喚/掉落/傳送落點）都該先驗證該點不在牆/水裡。

### F8. 怪物追一追停在空地不動（避障誤判「被包住」而凍結）
- **症狀**：怪物在可走空地上追玩家，追到某點就完全不動了，即使玩家站到旁邊也追不到。
- **原因**：加了局部避障後，`SteerAround` 在「所有探測方向都被擋」時回 `Vector2.zero` → `Stop()`，一旦誤判（或怪的高碰撞框卡在牆邊）就會每幀凍在原地。
- **解法**：避障**永不回 zero**（一整圈都被擋也仍朝目標推，交給物理/解卡處理）＋新增**解卡**：`MonsterActuator.UpdateStuck` 偵測「想動卻幾乎沒位移超過 0.25s」→ 往側邊滑 0.4s 脫困、換邊重試。**通則**：局部避障不要用「偵測不到出路就停」當唯一手段，會因誤判凍住；改成「永不凍住＋卡住就側滑脫困」。（2026-07-10 記）

### F9. 加了 A* 尋徑，怪物卻在原地「一上一下」震盪、走不到玩家
- **症狀**：召喚出來的怪明明離玩家很近，卻在原地不停上下抖動、完全過不來（尤其紅嫁衣 boss 房，房間中上方有一整塊牆）。
- **原因**：第一版 `MapNavGrid` 用 **`Physics2D.OverlapCircle` 逐格掃牆**來建可走格。但牆是 `MapLoader` 把可走層 `'1'` 格合併成的**一整片 `CompositeCollider2D`**，建格當下（載圖同幀）它常還沒 `SyncTransforms`/query-ready → OverlapCircle 抓不到 → **整張格子被當成全可走**。A* 於是給出一條「直穿中間那塊牆」的直線路徑；怪照著撞進牆、被牆擋住幾乎沒位移 → 觸發解卡側滑（垂直於水平推力＝上下）→ 每 0.4s 換邊 → 看起來就是「一上一下」永遠卡在牆前。（boss 房 `walkSubdiv=4`＝72×40 子格，牆塊在上緣中間，正好夾在怪與玩家之間。）
- **解法**：改用**地圖可走層位元圖**（`MapData.WalkableLayer.blocked`，`'0'` 可走／`'1'` 牆／`'2'` 水）當可走格的**權威來源**——這份資料就是 `MapLoader` 生牆碰撞用的同一份、載圖當下就在記憶體、**與物理時序無關**，不會有 composite 還沒 ready 的坑。再用 `OverlapCircle`（Environment/Water）做**聯集**補上位元圖沒有的「地上物家具」碰撞、順便在牆/家具周圍留 `AgentRadius` 淨空。`EnsureBuilt` 改吃 `MapData`（不再是 `Rect`），`MapManager` 呼叫改成 `MapNavGrid.EnsureBuilt(mapLoader.Map)`。**通則**：需要「格子化的可走資訊」時，優先用**資料層既有的權威位元圖**去建，不要靠「載圖同幀的物理查詢」——尤其牆是 CompositeCollider2D 時，query-ready 有時序風險，會整片誤判。物理查詢只拿來做「補充聯集（家具）＋淨空」這種錯了也只是少留一點餘裕、不會破壞拓撲的用途。（後續「牆淨空過度侵蝕」「家具沒被繞」「怪還是卡在牆/家具上」的演進與最終解，見 F11。）（2026-07-10 記）

### F10. 怪物離玩家遠一點就「發呆」不追（感測半徑 DetectionRange 太小、又沒資料化）
- **症狀**：修好 A* 尋徑格（F9）後，怪離玩家近時會來追，但玩家一站到房間另一頭，怪就杵在原地不動——不是卡住、是**根本沒發現玩家**。（表現和「A* 壞掉」很像，容易混淆：F9 是「發現了但走不到」＝會動會震盪；F10 是「沒發現」＝完全不動。）
- **原因**：Enemy 陣營靠 `MonsterSensor.GetTargetPlayer()`＝`dist <= DetectionRange` 才回傳玩家給 `ChaseBrain`，否則回 null → Brain `Stop()`。而 `DetectionRange` **預設只有 10**，且 `MonsterData.csv` 根本**沒有這個欄位**（只有 boss 的 `RedBridalGownBrain` 在程式裡覆寫成 30），所以召喚出來的家人幽靈全用預設 10。紅嫁衣 boss 房 18×10、對角約 20.6，玩家在對角就超過 10 → 幽靈看不到 → 發呆。
- **解法**：把 `DetectionRange` **資料化**——`MonsterData` 加欄、`MonsterSpawner` 解析第 16 欄（index 15）、`MonsterController.Initialize` 把它套到 `_sensor.DetectionRange`（boss 級 Brain 仍可在 Think 內再覆寫）。`MonsterData.csv` 加 `DetectionRange` 欄，家人幽靈(2–12)與 ZhaYu(1) 設 **25**（涵蓋整個 boss 房對角）、boss(13) 設 30。**通則**：怪「看不看得到玩家」是**每關/每怪都要能調的手感值**，別寫死在程式預設；資料化後不同關卡（大廳 vs 窄房）可各設合適的感測半徑。**除錯提示**：怪不追時先分清是「沒發現（完全不動）」還是「發現了走不到（會動/震盪）」——前者查 `DetectionRange` 與距離，後者查尋徑格（F9）。（2026-07-10 記）

### F11. A* 尋徑對了，怪卻一直「卡在牆/家具上」——最後改成「非 boss 怪純 A* 導航、不做硬碰撞」
- **症狀**：F9 修好尋徑格後，怪還是一關卡過一關：先是紅嫁衣房會頂著牆震盪；修好牆後，換小關卡被一張椅子/桌子擋住繞不過去；再修，換卡在桌角。一路打地鼠。
- **原因（分兩層，花了很多時間才看清）**：
  1. **牆淨空用物理去啃 → 把喉道切斷**。原本除了位元圖，還用 `Physics2D.OverlapCircle(AgentRadius=0.4)` 逐格留淨空，但它會把整片 `CompositeCollider2D` 牆多啃掉一圈，紅嫁衣房兩個房間之間的窄喉道被切斷（可走格 1408→920、A* 找不到路）。→ 改成**只用位元圖把牆膨脹 1 格**（確定性、保證連通，1408→1212），物理只留給「家具」用小半徑聯集。
  2. **真正的根**：怪的碰撞框是**整張圖**（約 0.9×2.2 世界單位，俯視角的圖很高），拿它當「移動碰撞」，怪一走到牆/家具旁邊，高框就頂進去卡死——**A* 算得出路也走不動**。先改成「腳底小框」移動、身體框當 trigger 打擊判定，好了一大半；但家具窄縫仍會卡（腳框再小，桌角還是頂得到、離散格子也很難精準反映窄縫）。
- **最終解（定案）**：**所有怪物（含 boss、含所有招喚物）一律靠 A* 導航、身上碰撞框全設 `isTrigger`（不做硬碰撞，無例外）**。牆/家具的迴避全交給尋徑格（本來就含兩者），怪只照路徑平滑走，**沒有任何實體框去頂／卡**，所以永遠不會卡死；打擊/接觸傷害走 trigger 幾何判定（`queriesHitTriggers=1`、`Physics2D.Distance`）不受影響。連逃跑的紅嫁衣 boss 也拿掉硬碰撞、改用 A* 繞牆逃，「讓玩家追得上」改用把她 `Speed` 調慢達成（不再靠被卡住）。另外把 `DirectClear`（判斷可否直走）也改成用**和 A* 同一份格子的格視線**（`HasLineOfSight`），避免細射線穿過家具淨空而誤判可直走。
- **通則（貴的教訓）**：① 當「A* 路徑是對的、怪卻走不到」時，先懷疑**移動用的物理碰撞**，別再一直修尋徑格——尤其俯視角把「整張高圖」當碰撞框，天生會頂牆。② 若已經有可靠的格子導航（關卡小、家具少、A* 成本可忽略），**讓所有 AI 怪純導航、一律不硬碰撞**是最省事也最不會卡的做法（本專案定為準則，見 ACTORS_AND_COMBAT.md）；連「需要被追上」的逃跑 boss 也是——那用「調慢速度」解決，不要用硬碰撞把她卡住。硬碰撞只留給玩家。③ 別為了「保留既有物理行為」而一路補丁——早點退一步問「這東西到底需不需要硬碰撞」。（2026-07-10 記）

### F12. 移除硬碰撞後，怪被擊退會「飛穿牆再被拉回來」；離牆太近又變成「完全不退」
- **症狀**：怪改成純 A* 導航、碰撞框全 trigger（F11）後，被擊退時因為沒有實體碰撞擋著，會直接**飛穿牆**、再被 A* 拉回來，很怪。第一版修法（照尋徑格夾距離）又出反效果：怪**離牆不夠遠時乾脆完全不退**，應該要退到牆邊才對（紅嫁衣關卡的小怪刻意調得很好推、退很遠，這問題特別明顯）。
- **原因**：① 擊退（`HitReactionHandler.ApplyKnockback`）是直接給 `Rigidbody2D.velocity` 飛一小段，trigger 不擋牆 → 穿牆。② 第一版用 `MapNavGrid` 的**可走格**夾距離，但尋徑格為了「怪身淨空」已經把牆往外**膨脹**一圈——那圈是真地板、卻在格子上標成不可走。怪一站進那圈，夾制就以為「下一步不可走」→ 夾成 0 → 完全不退。
- **解法**：改用**物理射線偵測「真正的牆」**（`Physics2D.Raycast` 打 `Environment`＋`Water`），把擊退距離夾到牆面之前（留 0.15 邊距讓身體別陷進去）。後面沒牆＝照原距離退；後面有牆＝退到牆邊為止（退不夠遠也還是退到能退的最遠處，不會是 0，除非已貼著牆）。**通則**：牽涉「實體要停在牆前」的位移（擊退、擊飛、衝刺），要用**實際碰撞幾何**（physics raycast/cast 打牆層）當基準，**別用尋徑格**——尋徑格是「規劃用、含 agent 淨空的膨脹圖」，拿來當物理邊界會多夾一圈。（2026-07-10 記）

### F13. 移除硬碰撞後，怪追到玩家旁邊「停著不撞」、完全不扣血
- **症狀**：怪靠接觸傷害扣血，但玩家站著不動時，怪會停在身邊一段距離、不再靠近、也不扣血，很好笑。
- **原因**：`ChaseBrain.StopDistance`（追擊停止距離）沿用舊的 **1.0**（原意是「別貼太近、免得硬推玩家」）。但接觸傷害（`EnemyContactDamage`）要**兩個碰撞框重疊**才判定：怪身體框半寬約 0.45 ＋ 玩家框半寬約 0.2 ≈ 0.65，卻停在 1.0 → 永遠差一截碰不到。以前有硬碰撞時「停在 1.0」剛好、現在怪是 trigger 不會推玩家，就該讓牠貼上去重疊。
- **解法**：把 `ChaseBrain.StopDistance` 與 `AllyBrain.AttackStop` 從 1.0 縮到 **0.2**——怪/友軍會貼到目標身上重疊，接觸傷害才吃得到（trigger 不互推，重疊沒問題）。**通則**：接觸傷害＝「碰撞框重疊」；靠接觸傷害的角色，停止距離要**小於「雙方框半寬之和」**才碰得到，別沿用「保持距離」的舊值。（與 F11/F12 同源＝移除硬碰撞的連帶調整）（2026-07-10 記）

### F14. 榕樹妖地刺「看得到卻打不到／站旁邊被打死」——攻擊物碰撞框沒對齊可見圖
- **症狀**：地刺（`BossSpike`）的傷害範圍對不上眼睛看到的刺。放大版大地刺特別明顯：一版站在刺旁邊不受傷（框太小又太高）、改大後又站在**空地**被打死（框罩到圖的空白上半）；一般地刺／地刺浪則是紅框都落在刺的**上半、圈不到根部**。
- **原因**：地刺特效圖（`fanfx2_earth_spikes`，160×128、pivot 置中）是**從底部往上長、實體只佔圖的下半、上半是空的**。舊碰撞框是「**以圖正中心為中心**的固定核心框」（0.9×1.3×scale）：① 中心在圖正中央，但刺的實體在下半 → 框落在刺上半、圈不到基座；② 放大版 scale 一拉大，框跟著往上罩到空白上半 → 站在沒刺的地方也被判定。小 scale 時因為絕對偏差小、勉強能接受，大 scale 就爆開。
- **解法**：碰撞框改成**貼齊「可見地刺」的 base-anchored 框**——冒出當下讀特效顯示邊界（`VfxInstance.WorldBounds`），**框底對齊圖底（地刺基座）往上長**，寬＝可見寬×0.75、高＝可見高×0.60，且**所有地刺共用同一規則**（`BossSpike.Fire` 預設 `hitFillW/H`）。另加**除錯紅框**（`BossSpike.DebugDrawHitbox`，用 LineRenderer 畫在 Game View）方便對照調整。**通則**：當「碰撞判定」要對齊「一張美術圖」時，別用手寫固定框硬猜，去讀該圖的實際顯示邊界來貼合；且要注意**圖的實體不一定置中**（像從地面往上長的東西是底部貼齊），框要 anchor 到實體那一側、不是圖的幾何中心。（2026-07-10 記，見 [BOSS_MODULE.md](BOSS_MODULE.md) §6.2）

### F15. 怪物「原地踏步」——播走路動畫卻沒真的移動（尤其紅嫁衣等會逃跑的 boss）
- **症狀**：玩家沒靠近時，怪物（特別是紅嫁衣 boss）一直播走路動畫，但位置沒有真的改變，像在原地踏步。
- **原因**：`MonsterController.HandleVisuals` 用「**指令速度**」`_rb.velocity.magnitude > 0.1` 判斷是否在動。但**所有怪的碰撞框都是 trigger**（走 A* 導航、不做硬碰撞，見 F11），逃跑被卡在牆角、或 A* 目標點不可達而在原地微調時，`MoveTowards` 仍每幀把 `velocity` 設成滿的 `MoveSpeed`、實際位置卻幾乎沒變 → 指令在動、人沒動 → 誤判成走路。
- **解法**：改看「**實際位移速度**」——每幀量 `transform.position` 的位移 ÷ `Time.deltaTime`（加指數平滑 tau 0.08 吃單幀抖動；玩家/怪物 Rigidbody2D 已開 Interpolate 故量測穩定），超過 `MoveAnimThreshold`（新增欄位，預設 0.12 世界單位/秒）才播走路、否則 idle；此速度也餵 `MonsterAnimator.SetState` 讓走路 fps 跟真實移動連動。通用、對所有怪生效。**通則**：判斷「角色有沒有在動」要看**實際位移**、別看指令速度——尤其「trigger 碰撞（不會被牆擋停）＋外部尋徑」時，指令速度與真實位移會嚴重不一致。（2026-07-13 記，見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)）

### F16. boss 召喚/攻擊時沒有出手動作（施法時只發呆，或明明畫了 attack 幀卻不播）
- **症狀**：紅嫁衣召喚家人時原本有「施法動作」，修掉 F15 的原地踏步後施法動作消失、變成召喚時站著不動。更一般的情形：某隻 boss/怪明明畫了 `attack` 幀，遊戲裡卻從來不播攻擊動畫。
- **原因**：怪的 `attack` 幀只放在 `GameAssets/.../<怪名>/attack/`，**沒有同步進 `StreamingAssets`**（紅嫁衣的 StreamingAssets 端只有 `idle`/`walk`）。遊戲是從 StreamingAssets 載圖，`MonsterSpriteLibrary.Has(Attack)` 因此是 **false** → `HandleVisuals` 施法時的 Attack 請求被 `Has` 擋掉、掉回「移動→走路」。F15 之前她召喚時剛好被卡在牆角（velocity 滿）→ 播走路，被誤看成「施法動作」；F15 把原地改判成 idle 後，那個假施法走路就沒了。**根因＝boss 的 attack 幀漏 Sync**，與「主角攻擊動畫沒顯示」同源（見 [TODO.md](TODO.md)）。
- **解法**：兩層。**① 程式即時保底**：`HandleVisuals` 在施法視窗（`_skillCastAnimUntil`／`NotifySkillCast`）內若沒有 attack 幀，退回播**走路**當出手表演（只在該 0.6s，平常靜止仍 idle、不回到原地踏步），且原地施法時用 `MoveSpeed` 餵走路 fps 讓節奏正常。**② 治本**：跑 `Project Tools → Sync Map Assets` 把 `RedBridalGown/attack`（及其他怪的 attack）推進 StreamingAssets，`Has(Attack)` 變 true 後就自動改播真正的攻擊動畫（程式已接好、零改動）。**通則**：怪的 `attack`（或任何新動作葉資料夾）加了圖，一定要重跑一次 Sync，否則遊戲端 `Has` 抓不到、靜默退回其他狀態、不報錯，很難察覺。（2026-07-13 記，見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)、[MONSTER_SETUP.md](MONSTER_SETUP.md)）

---

### F17. 用「和碰撞圓等大的圓」做前方探測，一貼上牆就探不到（沿牆滑動/角落校正靜默失效）
- **症狀**：寫了「撞牆就沿牆滑」「轉角自動推一把」，實機卻幾乎不會觸發；偶爾生效偶爾不生效，找不到規律。
- **原因**：專案全域 **`queriesStartInColliders = false`**，而 **整張地圖的牆是同一顆 `CompositeCollider2D`**（`MapLoader.BuildCompositeFromCells`）。玩家一貼上牆，用**等大的圓**從圓心射出去時「起點重疊」成立，**那顆 composite 會被整片忽略**，探測回報「前方淨空」→ 修正邏輯正好在最該生效的那一刻失效。接觸間隙只有 `contactOffset = 0.01`，所以還會逐幀時有時無。
  更糟的是**牆和地上物是不同的 collider**：貼著屏風、左邊是牆時，屏風被起點重疊吞掉、牆沒有 → 兩側探測結果不對稱 → 角落校正可能往錯的方向推。
- **解法**：**探測圓要比實際碰撞圓小**（`PlayerController.ProbeInset = 0.05`，要大於 contactOffset），並把縮掉的量補回探測距離。`PlayerController.SetupMoveProbe` 就是這樣算 `_probeRadius` 的。
- **這個坑專案踩過兩次**：怪物那邊早就有註解——`AI/MonsterActuator.cs` 的 `DirectClear`：「用細射線（非圓）：牆是單一 CompositeCollider2D，圓一碰到牆就會因 queriesStartInColliders=false 整片被忽略而誤判暢通」。同一家族還有 **B7**（貼身重疊時 `OverlapCircle` 漏抓，解法是改用 `Physics2D.Distance`）。
- **通則**：**在這個專案裡，任何「從角色身上射出去」的圓形查詢都要先想一次 `queriesStartInColliders`。** 要嘛縮小查詢形狀、要嘛改用細射線、要嘛改用 `Physics2D.Distance`／既有登記表。（2026-08-19 記）

## H. 流程 / 存讀檔 (Game Flow & Save UI)

> 系統說明見 [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md)、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)。

### H1. 加了標題流程後，開場墜落動畫結束、進 MainScene 卻一片黑（沒有任何關卡被載入）
- **症狀**：接上「標題→存讀檔」流程後，新建遊戲會正常播開場漫畫＋墜落動畫，但墜落結束載入 MainScene 後**整個畫面全黑、無錯誤訊息**；改流程前墜落後會正常出現在 Main_Cave（地圖 11）。
- **原因**：`GameFlowBootstrap` 在開機（BeforeSceneLoad）把 `MapManager.SuppressAutoStart` 設 true，目的是讓標題畫面蓋在「空的 MainScene」上、不要一進場就自動進關卡。但這個**靜態旗標整場有效**——開場鏈播完由 `IntroFallController` 載入 MainScene 時，那個新的 MapManager 也被壓住，`autoStartLevel` 不會 `StartLevel("Main")`（Main 模組首圖＝Main_Cave 11）→ 沒有任何地圖被建 → 全黑。原本能進 Main_Cave 正是靠這條自動進關卡。
- **解法**：抑制只該作用在「開機當下的空 MainScene」，之後由各流程分支自己決定。`GameFlowManager` 在**新建＋播開場鏈**分支載入 Intro 場景前，把 `MapManager.SuppressAutoStart` 設回 **false**，交還給既有開場流程（Intro→MainScene 自動進 Main_Cave→過場到廣場）；**繼續 / 無開場直接進廣場**分支則維持 **true**、由流程明確 `GoToMap(廣場)`，避免和自動進 Main_Cave 打架。**通則：跨場景的「一次性抑制旗標」別設成整場有效，要在每個流程分支明確設定它的值。**

---

## I. 開發環境 / 工具（Cowork Claude App 橋接器）

> 這節不是遊戲程式的坑，是用 Claude 桌面 App 的 Cowork 開發時、「AI 讀寫本機專案檔」用的橋接器踩到的坑。

### I1. Claude App 橋接器一直失敗 / 看不到專案目錄 / 檔案寫不回硬碟（元凶是 VPN）
- **症狀**：用 Claude 桌面 App 的 Cowork，資料夾明明還連著，AI 卻一直回報「device not connected / 橋接器失敗」——讀不到專案目錄、`device_commit_files` 寫不回本機，所以 `git status` 看不到任何變動（AI 說改好了、硬碟上卻沒東西）。重開 App 偶爾好一下又壞。
- **原因**：**電腦開著 VPN**。實際情境：早上在家待命開了 VPN，之後沒關就帶著同一台（裝著 Claude App 的）電腦回公司接不同網路 → VPN 改了對外網路路徑，Claude App 與雲端 session 之間的橋接連線就一直建立不起來或中途斷掉。**與資料夾有沒有連、Unity、專案程式全都無關。**
- **解法**：**把 VPN 關掉**。實測：一關 VPN，橋接器立刻恢復、AI 讀得到目錄也寫得回檔。若關了還沒好，再重開 Claude App 讓它重連；仍不行就**開一個新對話**（新 session 會綁到當下活著的橋接器，舊對話可能還綁在斷掉的橋接器上），或請 AI 把改好的檔打包成 zip 由你自己 `unzip -o` 覆蓋（不靠橋接也能落地）。
- **通則**：Cowork 橋接器忽然「連不上 / 寫不回」時，**先查網路層變動**（VPN、切換 Wi-Fi/網段、Proxy、公司防火牆）——這類最容易被忽略、卻最常是真兇；其次才是重連 App 或換新對話重綁。（2026-07-09 記）

### I2. 按 Play 進 Play 模式等很久（Domain Reload）＋ 關掉後的 static 殘留保險
- **症狀**：遊戲專案每次按 Play 到真正跑起來要等很久（連只顯示標題也慢），開發很痛苦。
- **原因**：`EditorSettings` 的 **Enter Play Mode Options 沒開**（`m_EnterPlayModeOptionsEnabled: 0`），每次進 Play 都做完整的 **Domain Reload（重載所有腳本組件）＋ Scene Reload**；腳本量一大就明顯慢。
- **解法**：開 **Edit → Project Settings → Editor → Enter Play Mode Settings**（＝把 `m_EnterPlayModeOptionsEnabled` 設 1；選項 `3` = Domain/Scene reload 都停用）→ 進 Play 幾乎瞬間。改過腳本那次仍要重新編譯＋載入，無法避免；沒改程式的 Play 才會秒進。
- **代價與保險（重要）**：關掉 Domain Reload 後 **C# `static` 不會每次 Play 自動歸零**，上一輪殘留會讓「**第二次以後的 Play**」行為異常——最典型是 **static 事件累積訂閱者**（`TriggerChain.OnTriggerFired` → 重複觸發／呼叫到已銷毀物件）與**抑制旗標殘留**（`MapManager.SuppressAutoStart` 沒被重設 → dev 直接進關卡時全黑）。已加 `Assets/Scripts/PlayModeStaticReset.cs`（`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`，每次進 Play 最早期統一重置這些 static，並呼叫 `TriggerChain.ResetForPlayMode()` 清集合/事件、`FlagRegistry.Reload()` 清快取）。**之後若又踩到「第二次 Play 才出現」的殘留，把該類別的 static 加進 `PlayModeStaticReset` 清即可。** UnityEngine.Object 的 static 快取（程序生成 sprite 等）靠既有 `if (x==null) x=Build()` 會自動重建（Unity 對已銷毀物件 `==null` 回 true），不必處理。
- **附帶**：`MapManager` 加 `DevLoadingHoldSecondsOverride`，`DevQuickStart` 在編輯器測試時把載入頁停留秒數設 0（build 沒那支腳本，維持正式秒數讓玩家看載入圖）。（2026-07-09 記）

### I3. 關掉 Domain Reload 後「第二次以後的 Play」角色/怪物只剩影子、劇情圖空白
- **症狀**：開了 Enter Play Mode Options（見 I2）後，**第一次 Play 正常；第二次進同一關，角色本體不見、只剩腳下影子，還能移動**（怪物同理；劇情大圖／對話立繪也可能變空白）。
- **原因**：走「執行期從 StreamingAssets 載入 Texture/Sprite」的**懶漢單例**（`PlayerSpriteLibrary`／`MonsterSpriteLibrary`／`DramaDatabase`／`DramaTalkDatabase`）把載好的 `Sprite[]` 快取在 static 字典，回傳時只查字典、**不檢查 sprite 是否已被銷毀**。關掉 Domain Reload 後，第一次 Play 結束時那些 runtime texture 被 Unity 銷毀，但單例＋快取還在 → 第二次 Play 回傳「已銷毀的 sprite」→ 貼到 `SpriteRenderer` 就是空的。影子還在是因為 `BlobShadow` 是程序生成、用 `if(x==null)重建` 會自動復原（`==null` 對已銷毀物件回 true）。**任何「static 快取 runtime 載入的 UnityEngine.Object、又不做 `==null` 檢查」都會犯這個雷**。
- **解法**：給這四個單例各加 `ResetForPlayMode()`（`_instance = null`，下次存取重載乾淨的圖），由 `Assets/Scripts/PlayModeStaticReset.cs` 在每次進 Play 呼叫。用 `Resources.Load` 的（背包 icon 等）不受影響——那是資產、不會被銷毀。**通則**：關掉 Domain Reload 後，凡是 static 快取「runtime 生成／載入的 sprite／texture／material」的類別，都要在 `PlayModeStaticReset` 丟掉重建。（2026-07-09 記）

### I4. 「Sync Map Assets」沒把 `flags.json` 帶進遊戲 → 旗標範圍變更（如改成關卡單次）在遊戲端沒生效
- **症狀**：在編輯器旗標管理器把某旗標改了範圍（例 `killedFamily` 周目 → 關卡單次）並「儲存」，也跑了 `Project Tools → Sync Map Assets`，但**遊戲行為沒變**——`killedFamily` 依舊被當周目、讀到上次寫進存檔的舊 `true`（明明這趟沒殺家人卻被判成有）。
- **原因**：`MapAssetSyncTool.SyncMapAssets`（＝「Sync Map Assets」選單，也對應 `Tools/sync_assets.sh`）**只**同步地圖 `.dipanmap` 與美術素材（Environment/Tiles/Background/Drama/Talk）＋生 catalog，**從來沒有複製 `flags.json`**。編輯器把 `flags.json` 存在 `DipanProj_MapEditor/flags.json`（專案根），遊戲端 `FlagRegistry` 讀的是 `DipanProj_Main/Assets/StreamingAssets/MapAssets/flags.json`——兩者沒有任何同步橋接，所以遊戲那份一直停在最早手動放的舊版（只有 `hallGateOpen`）。旗標查不到＝`IsLevel`/`IsLife` 都回 false ＝退回預設「周目」→ 去讀存檔 → 讀到殘留值。（文件曾寫「同步會帶 flags.json」，與實作不符。）
- **解法**：`MapAssetSyncTool` 加一步 `PullFlagsFromEditor`——把 `DipanProj_MapEditor/flags.json` 複製進 `StreamingAssets/MapAssets/flags.json`（放在拉地圖之後）。之後每次 Sync 都會帶過去。修完**重跑一次 Sync Map Assets** 才會把目前的 flags.json 推進遊戲。註：關卡單次旗標一旦生效，遊戲只讀記憶體、**不再讀存檔**，所以之前殘留在 `progress.flags` 的舊 `true` 會被無視（無害死資料，不必洗存檔；除非日後又把該旗標改回周目才會被翻出來）。（2026-07-09 記）

### I5. 關掉 Domain Reload 後「第二次以後的 Play」頭上傷害數字不再出現
- **症狀**：開了 Enter Play Mode Options（見 I2）後，怪物／角色被打時**頭上的浮動傷害數字消失了**——之前明明會跳。第一次 Play（或改過腳本、觸發重編譯的那次）正常，第二次以後就不出現；戰鬥、扣血、擊中特效都正常，唯獨數字沒了。
- **原因**：`DamageNumberManager` 是**懶漢單例＋ `_quitting` 守衛**：`Instance` 取用時 `if (_quitting) return null;` 擺在「建立 GameObject」之前，而 `_quitting` 只在 `Awake` 裡被設回 false。編輯器**停止 Play 時 `OnApplicationQuit` 會把 `_quitting` 設成 true**。以前每次 Play 都有 Domain Reload 把 static 歸零所以沒事；關掉之後 `_quitting = true` 殘留到下一次 Play → `Show()` 取 `Instance` 被守衛擋成 null → 直接 return、不生數字。而且因為物件從沒被建立，`Awake` 也沒機會把 `_quitting` 設回 false ＝**死結**。（與 I3 同源＝關 Domain Reload 後的 static 殘留，但這裡卡的是「阻止建立」的旗標而非銷毀的 sprite；`VfxManager` 沒有 `_quitting` 守衛、走 `if(null)重建` 自癒，故擊中特效不受影響——只有傷害數字中招。）
- **解法**：給 `DamageNumberManager` 加 `public static void ResetForPlayMode() { _quitting = false; _instance = null; }`，並在 `Assets/Scripts/PlayModeStaticReset.cs` 每次進 Play 時呼叫（放在其他 `ResetForPlayMode` 旁）。**通則同 I2/I3**：關掉 Domain Reload 後，凡是「用 static 旗標擋住單例建立／或 static 快取 runtime 物件」的類別，都要在 `PlayModeStaticReset` 歸零。打包版每次都是全新程序、本來就乾淨，這段是無害 no-op。（2026-07-09 記）

### I6. Console 跳 `FMOD failed to switch back to normal output ... Cannot call this command after System::init.`（可忽略）
- **症狀**：編輯器 Console 出現一則紅字 `FMOD failed to switch back to normal output … : "Cannot call this command after System::init. " (32)`，戰鬥／畫面／遊戲邏輯全部正常。
- **原因**：這是 **Unity 引擎內建音訊層（底層用 FMOD 驅動 AudioManager）** 的警告，**不是專案程式碼**（本專案目前還沒有音訊系統，這不是遊戲音效）。Unity 想把音訊輸出「切回預設裝置」但 FMOD 已經 `init` 過、不能再切，就印這行。常見觸發＝**音訊輸出裝置變動**：藍牙耳機連/斷、HDMI 螢幕的音訊通道斷開重連、切換遠端桌面、進出 Play 模式時系統音訊焦點跳動。本機走遠端／ATEN 4K HDMI（見 E1），HDMI/遠端桌面的音訊裝置最容易在進 Play 或視窗切換時被系統重新指派而命中此情境。
- **解法**：**可忽略**——單次警告、不會 crash、不影響執行與打包，正式 build 一般不出現。嫌煩按 Console `Clear`；真的常跳就重開一次 Unity、或確認接的音訊輸出裝置穩定不要中途斷。**與角色/怪物/動畫程式無關**（能進 Play 看到它就代表腳本已正常編譯）。（2026-07-13 記）

### I7. 資料表從 `Resources/Data` 搬到 `Assets/Data` 後，字串／特效表變空（`[lang:id]`、睜眼趴地失效、馬賽克秒數讀不到）
- **症狀**：把數個 CSV 從 `Assets/Resources/Data` 移到 `Assets/Data`（統一存放位置）後，明明檔案還在、內容也對，遊戲端卻讀不到——語言字串全變 `[lang:1001]` 這種占位、睜眼特效後玩家不趴地起身、`ScreenFxTable` 整張讀不到。
- **原因**：**兩件事疊在一起**。(a) `Assets/Data` 底下的 CSV **不在 `Resources/`、不會被 `Resources.Load` 找到**，也不會自動打進 build——非 Resources 資產要「有人在場景裡拿著它的序列化參照」才進得了 build。本專案的作法是每張表配一個 **provider MonoBehaviour**（`ItemTableProvider`／`LanguageTableProvider`／`ScreenFxTableProvider`／`SceneFxTableProvider`…）掛在 `MainScene`、Inspector 拖進對應 `TextAsset`，各表 `LoadXxx` 改成**先找 provider、找不到才退回 `Resources.Load`**。搬了表卻**沒把 provider 掛上場景/沒拖 TextAsset**，表就是空的。(b) 就算 provider 後來接好了，**關掉 Domain Reload（見 I2）** 會讓 static 快取殘留——若曾在 provider 接好前 Play 過一次，那張表的 static 已快取成**空**，之後接好也不重載，字串照樣是 `[lang:id]`。
- **解法**：(a) 每張搬到 `Assets/Data` 的表，都要有對應 provider 掛在 `MainScene` 且 TextAsset 已拖進去（漏了就整張空）。(b) 給這些表加 `ResetForPlayMode()`（把 static `_rows`/單例設 null，下次存取重讀），並在 `Assets/Scripts/PlayModeStaticReset.cs` 統一呼叫——目前已納入 `Language` / `SceneFxTable` / `ScreenFxTable`。**通則同 I2/I3/I5**：關掉 Domain Reload 後，凡是 static 快取「provider 載入的資料表」的類別，都要在 `PlayModeStaticReset` 清掉；接好 provider 後**務必重編譯＋重新 Play 一次**讓 `ResetForPlayMode` 生效，別拿殘留空表除錯。（2026-07-22 記）

### I8. 關掉 Domain Reload 後「第二次以後的 Play」某組序列圖整組不見／變白塊（陣列型 static 快取）
- **症狀**：第一次按 Play 一切正常；**停止後再按第二次 Play**，某個用「一組序列圖」畫出來的東西整組消失或變白塊（實例：九霄雷獄的分段落雷柱 `SegmentedLightningColumn`）。Console 不一定報錯，也可能在取 `sprite.bounds` 時才丟 NullReference／MissingReference。單張圖的快取則不會有這問題。
- **原因**：與 I3／I5／I7 同一條線（關掉 Domain Reload 讓 static 殘留），但**多了一個容易漏掉的轉折**。`PlayModeStaticReset.cs` 的既有慣例是「UnityEngine.Object 的 static 快取靠 `if (x == null) x = Load()` 會自動重建」——因為 Unity 對**已銷毀物件**的 `== null` 回 true。**但這條慣例對「陣列／集合型」的快取不成立**：停止 Play 被銷毀的是**容器裡的元素**，而容器本身（`Sprite[]`、`List<Sprite>`、`Dictionary<_, Sprite>`）是**純 C# 物件、永遠不會變 null** → `if (arr == null)` 不會通過 → 快取不重載 → 第二次 Play 拿到一整包已銷毀的 Sprite。
  - 附帶的第二個坑：若 `Load()` 是「先 `new Sprite[n]` 再逐格 `Resources.Load` 填」的寫法，**素材真的缺失時它仍回傳非 null 陣列**，所以連 `arr == null || arr.Length == 0` 這種守衛也攔不到「陣列在、元素全 null」，那句本來要擋下來的警告永遠不會執行，直接一路走到取 `bounds` 時炸掉。
- **解法**：**判「元素還活著沒」，不要只判容器**。兩種寫法擇一：
  - (a) 在該類別自己判，並且**守衛也要用同一個判斷**（順便修掉上面那個攔不到的問題）：
    ```csharp
    static bool IsStale(Sprite[] arr) => arr == null || arr.Length == 0 || arr[0] == null;
    // 載入
    if (IsStale(_start)) _start = Load(StartPath, 2);
    // 守衛（別再寫 _start == null || _start.Length == 0，攔不到全 null 的情況）
    if (camera == null || IsStale(_start) || IsStale(_loop)) { Debug.LogWarning("素材未完整載入"); return; }
    ```
  - (b) 在 `PlayModeStaticReset.cs` 明確把該 static 設回 null。
- **通則（新增任何快取前先看這條）**：**凡是「陣列／集合型的 UnityEngine.Object static 快取」，都不適用 `== null` 自動重建，必須照上面兩種之一處理。** 單一物件的快取（`static Sprite _x`、`static Material _m`）則安全，維持原慣例即可。`PlayModeStaticReset.cs` 的檔頭註解已寫入這條規則。（2026-07-27 記；當時全專案只有 `SegmentedLightningColumn` 一處是陣列型，已修）

### I9. `.git/index.lock` 一直冒出來，每次要下 git 指令前都得先手動刪（AI 在橋接器裡跑 git 造成）
- **症狀**：自己要 `git add` / `git commit` 時被擋，說有另一個 git 程序在跑；去看發現 `.git/index.lock` 又在了。刪掉沒多久又出現，跟 AI 協作的那幾天特別頻繁。
- **原因**：**兩件事相乘**。
  1. **`git status` / `git diff` 不是純唯讀**——它們會順手 refresh index（更新每個檔案的 stat 快取好加速下次比對），為此必須先建立 `.git/index.lock`，用完再 unlink。
  2. **Cowork 橋接器的檔案系統不允許刪除任何檔案**（`rm` / `mv` / `unlink` 一律 `Operation not permitted`，`.git/` 底下也一樣）。
  → git 建了 lock、用完想刪、被拒絕 → **lock 永遠留在原地**，之後任何 git 寫入操作都被它擋住。
  AI 端會看到 `warning: unable to unlink '.../.git/index.lock': Operation not permitted`——**看到這行就代表已經把 lock 留給使用者了**。
  而且 **AI 自己清不掉**（`rm` 和 `mv` 都被拒），只能請使用者手動刪。
- **解法**：**透過橋接器跑的所有 git 一律加 `--no-optional-locks`**（git 專為這種唯讀查詢設計的旗標：不要為了優化去拿鎖）。實測對照：

  | 指令 | 會不會留下 lock |
  |---|---|
  | `git --no-optional-locks status --short` | ✅ 不會 |
  | `git --no-optional-locks diff` / `diff --stat` / `diff --numstat` | ✅ 不會 |
  | `git log` / `git show <ref>:<path>`（純讀 object，不碰 index） | ✅ 不會 |
  | `git status`（不加旗標） | ❌ **會** |
  | `git diff`（不加旗標） | ❌ **會** |

  等效寫法：`GIT_OPTIONAL_LOCKS=0 git status`。
- **通則**：**在橋接器（device_bash）裡把 git 當成「唯讀查詢工具」時，一律帶 `--no-optional-locks`。** 要跟 HEAD 對照舊版內容，用 `git show HEAD:<path>` 讀 object 就好，不必碰 index。已經卡住時只能請使用者手動 `rm .git/index.lock`——AI 在橋接器裡沒有刪檔權限。（2026-07-27 記；此前 AI 每改一批檔案就跑一次 `git status` 驗證，因而反覆製造這個檔案）

---

## J. 螢幕特效 / 進場過場 (Screen FX)

### J1. 改 `ScreenFxTable.csv` 的 `DurationSeconds` 完全沒反應（被呼叫端的時間 override 蓋掉）
- **症狀**：某螢幕特效（如 id 3「馬賽克清晰」）想調播放時長，改 `ScreenFxTable.csv` 的 `DurationSeconds` 不管填多少都沒用；懷疑「沒吃這個欄位」或「cache 沒清」。
- **原因**：**都不是**——程式路徑完全正確。`ScreenFxTable.csv` 的 `DurationSeconds` **只是「預設值」**：分派入口 `ScreenFxPlayer.Play(id, onDone, duration)` 的第三參數 `duration ≥ 0` 就以它為準、**完全不讀 CSV**；`duration < 0`（`-1`）才回退去讀 CSV。三個入口帶不帶 override 不同：`MapsTable` 的 `EnterEffect` 不帶（`Play(id,null)`）→ **永遠吃 CSV**；**劇情 `screenFx` 步驟**帶「停留秒數」(`seconds > 0 ? seconds : -1`)；**觸發鏈 `playScreenFx`** 帶 `duration` 參數。實際元兇：那個馬賽克是從 `Main_InitialForest1.dipanmap` 的**進場演出（cutscene）第 3 步 `screenFx`** 觸發，而那一步的「停留秒數」被填成 `1.0`，於是 `Play(3, …, 1.0)` 用 1 秒、CSV 的 `DurationSeconds` 根本沒被讀到。
- **解法**：看要哪種行為——要吃 CSV 的秒數，就進地圖編輯器「**劇情**」工具、點那個 `screenFx` 步驟，把「停留秒數」**清成 0**（回退讀 CSV）；要就地各設不同時長，就直接調那個「停留秒數」欄位（此時 CSV 只是沒被用到的預設）。**排查通則**：某螢幕特效改 CSV 秒數沒反應時，先確認它是**從哪個入口觸發**——劇情步驟／`playScreenFx` 觸發器都有各自的時間 override 欄位，填了就以呼叫端為準。（別忘了這類進場演出藏在 `.dipanmap` 的 `cutscene` 區塊、要用編輯器頂端「劇情」工具才看得到，不在觸發器/物件層。）（2026-07-22 記）

### J2. 在開場劇情按 ESC 後，主角莫名其妙播「從地上爬起來」的動畫
- **症狀**：初始森林那段開場劇情，按 ESC 略過對話後，主角突然演出「趴地→爬起」；用滑鼠左鍵正常結束對話則不會。看起來像動畫系統出錯或 `PlayerAnimator` 被誤觸發。
- **原因**：**不是動畫的錯，是一路交棒過去的正常結果。** ESC 同一下被兩處讀到：① `UIManager` 依 `CloseOnEscape` 關掉對話面板；② `CutsceneDirector.Update` 依 `skippable` 設 `_skip=true`。而 `_skip` 的語意是「**中止剩餘步驟，但仍執行最後的 `end` 交棒**」——`Main_InitialForest2` 的 `end` 是 `assetId='fall'`，於是一按 ESC 就：跳完整段劇情 → 接墜落動畫 → 回 `MainScene` 起關到初始洞窟(11) → 洞窟 `MapsTable.EnterEffect=1`（睜眼醒來）→ `ScreenFxTable` 該列 `WakeUpPose=1` → `MapManager.FireEnterTriggersRoutine` 執行 `HoldLyingPose()` + `PlayWakeUp()`。點左鍵不會，是因為左鍵只結束當前那句對話，後面二十幾個步驟照演。
- **解法**：ESC 略過改成**只有開發階段能用**——新增 `Assets/Scripts/DevSkip.cs`（`Allowed => Application.isEditor || Debug.isDebugBuild`），套在 `CutsceneDirector` 的 ESC 判斷與 `TalkPanel`／`DramaPanel` 的 `CloseOnEscape`。正式打包玩家按 ESC 完全沒反應，也就不會誤觸發交棒。
- **排查通則**：**看到「某個表演莫名其妙被觸發」時，先確認是不是「跳過機制把流程一路推到了別的場景」**，而不是去追那個表演本身的程式。`CutsceneDirector` 的略過「會執行 end 交棒」這點特別違反直覺——它不是「停在原地」，而是「快轉到結局」。（2026-07-27 記）

### J3. 想加「整段演出都掛著」的全螢幕效果，卻發現 `ScreenFxTable` 那套裝不下
- **症狀**：要做「回憶畫面」——整段劇情期間畫面泛黃、邊緣柔化。直覺是「照 `MAP_ENTER_EFFECT.md` 的三個維護點加一個 `ScreenFxTable` id 就好」，結果做出來的東西行為完全不對：**遊戲被暫停、HUD 被藏起來、播 N 秒就自己結束**，而劇情還在演。
- **原因**：`ScreenFxPlayer` 那一家（睜眼 1／破幻術 2／馬賽克 3）從設計上就是**一次性過場**——`Play(id, onDone, duration)` 的簽章本身就假設「有總長、播完會回呼」，而且 `ScreenFxPlayer` 統一在開始時 `SetLayerVisible(HUD, false)`、各控制器多半自己 `SetExternalHold` 暫停遊戲。這對「幻境崩碎」那種節點式演出是對的，對「持續狀態」則每一條都是反效果。**專案裡「持續型全螢幕後處理」的正典是 `AtmosphereController`**（常駐 blit、`ApplyMapAtmosphere` 切模式、不暫停不藏 HUD），不是 ScreenFx。
- **解法**：回憶特效做成獨立的常駐 blit `Scripts/MapFx/MemoryFxController.cs`（`Begin()`/`End()` 淡入淡出、`unscaledDeltaTime` 所以對話暫停時照走、`sceneLoaded` 時重掛相機），**沒有**登記進 `ScreenFxTable`。
- **判斷準則（下次要加全螢幕效果時先問這句）**：**「它有沒有一個『結束』的時間點，而且結束前遊戲該不該停？」** 有明確總長＋要停 → `ScreenFxTable` 加一列；由別的東西決定何時開何時關＋期間遊戲照跑 → 仿 `AtmosphereController` 寫一支常駐 blit。**塞錯家族的代價不是不能動，是「行為看起來很怪但每一行程式都對」**，很難反推。
- **順帶**：同一台相機上多個 `OnRenderImage` 依**元件加入順序**串接（氛圍先算、回憶疊在其上）；而所有這類後處理都**碰不到 `ScreenSpaceOverlay` 的 UI**，所以置中漫畫、對話框、HUD 一律不會被染色。（2026-08-22 記）

### J4. 螢幕色調效果在幽暗地圖上「幾乎看不出來」——乘法與壓暗作用在黑色上等於沒做
- **症狀**：做好的「回憶」全螢幕效果（泛黃 ＋ 邊緣柔化 ＋ 邊緣壓暗）在開場山道那種明亮場景看得很清楚，一到紅嫁衣關卡就**幾乎看不出來**。直覺是「強度不夠，調大一點」，但調大之後只有玩家提燈那一圈更黃，其餘還是沒變。
- **原因**：**方向錯了，不是強度不夠。** 紅嫁衣全 10 張圖的 `MapsTable.Atmosphere` 都是 **2（幽暗＋打光）**，除了玩家身上發光裝那一圈之外整張畫面被壓到接近全黑。而那三層全部是**乘法或壓暗**：
  - 泛黃 ＝ 顏色 × 暖褐色 → `0 × 任何值 = 0`，黑的還是黑；
  - 暈影 ＝ 邊緣再乘一個 <1 的係數 → 本來就黑，看不出差別；
  - 柔邊 ＝ 模糊 → 黑色模糊之後還是黑色。
  **所以整套效果在暗場景上是自動失效的**，而且失效得很安靜——每一行程式都在跑、參數也確實有套用。
- **解法**：回憶期間**先把整套場景氛圍淡掉**（`AtmosphereController.SetBypass(0→1)`，在 `Atmosphere.shader` 最後一行與原始畫面內插，所有 mode 一致），讓場景亮回來，色調層才有東西可以染。語意上也對——回憶不是「現在這個黑房間」。另外加**上下黑邊**（`Letterbox`，做在後處理裡）：它是「直接把那一塊塗黑」，**與場景明暗完全無關**，在全黑的地圖上也一眼看得出進入過場。
- **通則**：**問「這個效果在最暗（或最亮）的場景上還成不成立」是全螢幕後處理的必要驗收。** 判準很簡單——**乘法／壓暗類的效果需要畫面本來就有亮度**，在暗場景上必然失效；要在暗場景可靠地被看見，得用**加法（提亮、白霧、發光）或幾何（黑邊、遮罩、輪廓）**。這條對之後任何「中毒發綠」「受傷泛紅」「時間變慢」的畫面效果都適用：先想清楚它會被套在哪幾種 `Atmosphere` 上。
- **相關**：[ATMOSPHERE.md](ATMOSPHERE.md)、[CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §6、本檔 **E11**（Linear 色彩空間疊色）、**J3**（持續型 vs 一次性）。（2026-08-22 記）

---

## K. 互動 / 拾取 (Interaction & Pickup)

### K1. 走到擋路家具（櫃子/桌子）前，「按 F」提示出不來、撿不到（教學卡在「走過去按 F」）
- **症狀**：pickup 放在櫃子/桌子這種家具上，玩家走過去卻不出現星星／「按 F」提示、撿不到東西；新手教學會卡在「走過去撿」那步永遠不前進。
- **原因**：F 互動是量「玩家 → 該 pickup **最近感應格的中心**」的距離、在 `InteractionManager.pickupRadius`（預設 **1.2** 世界單位）內才算數（`PlayerNearPickup` / `NearestCellSqr`）。若感應格只放在**家具那一格**，而家具是**實心擋路物**（物件 `walkable:false`，有碰撞體），玩家會被擋在家具**前面**、身體中心進不了家具那格中心的 1.2 內 → 永遠不觸發。**跟 `pickupRadius` 調多大關係不大**：感應點在實心物「裡面」，半徑要開到很大才搆得到，還會連帶放寬撿地上物/踩傳送點的距離、手感變鬆。
- **解法**：把該 pickup 的**感應格延伸到家具前方（玩家站得到的地板）那排**——pickup 支援多格，`NearestCellSqr` 取最近格，玩家一走到家具前、最近格就落在 1.2 內。手指指向用的 `center` 是各格平均，仍會落在家具前緣，位置不會跑掉。**通則**：pickup 放在實心家具上 → 感應格務必含**前方可站的地板格**，別只放在家具那格。實例：儲藏室藥水櫃 `furniture_storage_rack` 的 pickup 從單格 `[13,2]` 擴成 3×2 `[12-14, 2-3]`（含櫃子那排＋前方地板那排）。少數「整體互動都想放寬一點」才考慮調大全域 `pickupRadius`。（2026-07-23 記）

### K2. 給鏈「中間那顆」加條件，結果把後面整段一起吞掉（玩家永遠拿不到後面該給的東西＝軟鎖）
- **症狀**：邪佛廣場的鏈是 `看全貌 → 邪佛對話 → 給紅嫁衣劇本 → 劇本開門`。為了讓「打過一關的老玩家不要再看一次初始對話」，在 `邪佛對話` 上加了 `最高完成關卡數 = 0`。結果對話確實不播了，但**劇本也不再發**、傳送門不開，玩家直接卡死在廣場。
- **原因**：`TriggerChain` 的預設語意是「**條件不成立 → 整條鏈在此中止**」。這對「鏈的第一顆」是對的（整段事件不該發生），但對**鏈中間那顆**就變成「跳過一句對話 = 連同後面所有動作一起取消」。`requireFlag`／`requireCycleMax`／`requireItem` 全都是這個行為，只是以前沒有人在鏈中間加條件所以沒踩到。**順帶一提這條鏈本身也有 bug**：原本的守門是 `requireCycleMax=1` ＋ `requireItem=!104`（背包沒劇本才播），但劇本一旦被消耗掉、周目又還是 1，兩個條件就**同時再度成立** → 初始對話與新手教學會重播。所以才需要「完成關卡數」這個維度的條件。
- **解法**：新增通用欄位 **`onBlocked` 條件不成立時**，兩個選項：`中止整條鏈`（預設，＝舊行為）／**`跳過這顆繼續`**（條件不成立時不做自己的事，但**照樣 `Activate(next)`**）。
  ```csharp
  if (!RequirementMet(r)) {
      string onBlocked = r.GetString(KeyOnBlocked).Trim();
      if (onBlocked == "跳過這顆繼續" || onBlocked == "skip") { … Activate(skipNext.Trim()); return; }
      Debug.Log($"[TriggerChain]「{r.name}」觸發條件不成立，鏈在此中止.");  return;
  }
  ```
  本例正解：`邪佛對話` 設 `最高完成關卡數=0` ＋ `條件不成立時=跳過這顆繼續`，`給紅嫁衣劇本` 保留自己的 `requireItem=!104`（已經有劇本就不重複給）。
- **通則**：**在鏈中間加條件前，先問「這顆被擋掉時，後面那些還該不該發生」**——想「整段取消」用預設，想「只跳過這一句」一定要同時設 `條件不成立時=跳過這顆繼續`。另外原本條件不成立是靜默的，現在會印一行 log，排查「鏈莫名其妙斷在中間」時先看 Console。（2026-07-28 記）
