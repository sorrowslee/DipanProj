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

---

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

---

## G. 角色 / 美術顯示 (Character & Sprite Rendering)

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


---

## H. 流程 / 存讀檔 (Game Flow & Save UI)

> 系統說明見 [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md)、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)。

### H1. 加了標題流程後，開場墜落動畫結束、進 MainScene 卻一片黑（沒有任何關卡被載入）
- **症狀**：接上「標題→存讀檔」流程後，新建遊戲會正常播開場漫畫＋墜落動畫，但墜落結束載入 MainScene 後**整個畫面全黑、無錯誤訊息**；改流程前墜落後會正常出現在 Main_Cave（地圖 11）。
- **原因**：`GameFlowBootstrap` 在開機（BeforeSceneLoad）把 `MapManager.SuppressAutoStart` 設 true，目的是讓標題畫面蓋在「空的 MainScene」上、不要一進場就自動進關卡。但這個**靜態旗標整場有效**——開場鏈播完由 `IntroFallController` 載入 MainScene 時，那個新的 MapManager 也被壓住，`autoStartLevel` 不會 `StartLevel("Main")`（Main 模組首圖＝Main_Cave 11）→ 沒有任何地圖被建 → 全黑。原本能進 Main_Cave 正是靠這條自動進關卡。
- **解法**：抑制只該作用在「開機當下的空 MainScene」，之後由各流程分支自己決定。`GameFlowManager` 在**新建＋播開場鏈**分支載入 Intro 場景前，把 `MapManager.SuppressAutoStart` 設回 **false**，交還給既有開場流程（Intro→MainScene 自動進 Main_Cave→過場到廣場）；**繼續 / 無開場直接進廣場**分支則維持 **true**、由流程明確 `GoToMap(廣場)`，避免和自動進 Main_Cave 打架。**通則：跨場景的「一次性抑制旗標」別設成整場有效，要在每個流程分支明確設定它的值。**
