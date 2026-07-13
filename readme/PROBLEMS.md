# 踩坑記錄與解法 (Problems & Solutions)

> 返回 [文件總覽](README.md)
>
> **給接手的人/AI:**
> 1. **第一次看這個專案的文件時,先把這份從頭看一遍**——很多「看起來很怪」的問題這裡已經有答案,別重複踩。
> 2. **以後每遇到一個新坑,就在這裡新增一則**,格式:`症狀 → 原因 → 解法`。寫清楚當下的現象,未來的人才搜得到。

每則用同一個格式:**症狀 / 原因 / 解法**。

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

### A3. 部署 `git push` 失敗 / `fatal: not a git repository`（⚠️ 已淘汰情境）
> **2026-07-03 起部署改用 itch.io + butler，build 不再進 git，本坑不再發生。新流程見 [DEPLOY.md](DEPLOY.md)。** 以下保留存查。
- **症狀**:打包成功但推送失敗;或 stderr 出現 `not a git repository`、`non-fast-forward`。
- **原因**:`DipanProj_Deploy` 還不是 git repo;或本地 main 落後遠端;或從 Unity GUI 啟動的程序拿不到 git 憑證/SSH key。

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

### A9. `git push` 被拒：`File ...resources.assets.resS ... exceeds GitHub's 100 MB limit`（⚠️ 已淘汰情境）
> **根治：2026-07-03 起部署改用 itch.io + butler，build 產物不再進 git，就沒有 GitHub 100MB 單檔限制的問題了（見 [DEPLOY.md](DEPLOY.md)）。** 以下的「壓縮 build 貼圖」仍可作為縮小 build 體積的一般參考。
- **症狀**:Build and Deploy 後 push，GitHub 退回，說某個檔（通常 `*_Data/resources.assets.resS`）超過 100MB。
- **原因**:`resources.assets.resS` 是 Unity 烘進 build 的「資源資料流」——**凡是放在 `Assets/Resources/` 的貼圖都會進這個檔，且在 build 內展開成大尺寸**（接近未壓縮）。本專案 `Resources/InitialStory`（開場漫畫＋墜落大圖）＋ `Resources/UI`（大張面板底圖）疊起來就破百 MB。
- **解法（治本）**:把那批大圖的**匯入設定**壓小——選取 `Resources/InitialStory`（及 UI 大底圖）→ Inspector：`Max Size` 設 1024（或留 2048）＋勾 **Use Crunch Compression**（或 `Compression = Normal`）＋取消 **Generate Mip Maps** → Apply → 重新 Build。觀念：
  - `Max Size`/壓縮改的是「**匯入後的版本**」（存在 Library，編輯器與 build 都用它），**不動原始 PNG**；build 裡裝的是這份處理版（原始 PNG 不會被打進遊戲），所以 build 變小，且**編輯器與 build 解析度一致**。
  - 開場圖走 `Resources.Load` → **吃**匯入設定；地圖素材在 `StreamingAssets`、用 raw bytes 載 → **不吃**匯入設定（原樣複製進 build，要縮得改檔案本身）。
- **解法（治標）**:Deploy repo 改用 Git LFS 追 `*.resS *.assets *.bundle`（遠端 pull 的機器也要裝 LFS）。

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

## C. 地圖編輯器 / 素材同步

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

---

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
- **原因**:`SpriteRenderer.sortingOrder` 雖宣告為 int,**實際是 16-bit(範圍 −32768~32767)**。給超大值(當時填 `2000000`)會**溢位繞回**:`2000000` 繞回後 ≈ **−31616(負)** → 比背景(`sortingOrder = −1000`)還低 → 被不透明背景蓋住、整個看不到。詭異的是地上物用 `1000000` 卻正常——因為 `1000000` 繞回後 ≈ **+16960(正)**,仍在背景之上,剛好沒事。
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

---

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

> 角色立繪／走路動畫的完整設定流程見 [CHARACTER_SETUP.md](CHARACTER_SETUP.md)。

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
