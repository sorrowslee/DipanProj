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

### A3. 部署 `git push` 失敗 / `fatal: not a git repository`
- **症狀**:打包成功但推送失敗;或 stderr 出現 `not a git repository`、`non-fast-forward`。
- **原因**:`DipanProj_Deploy` 還不是 git repo;或本地 main 落後遠端(沒先 pull 就 push);或從 Unity GUI 啟動的程序拿不到 git 憑證/SSH key。
- **解法**:
  - 一次性把 `DipanProj_Deploy` 設為 git repo 並接遠端(見 [BUILD_AND_DEPLOY.md](BUILD_AND_DEPLOY.md))。
  - 落後遠端 → 已由 `update_deploy.sh`(打包前 `reset --hard origin/main`)解決。
  - 憑證問題 → 先在終端機手動 `git fetch` / `git push` 一次把憑證帶起來。

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

---

## B. 地圖載入 (Map Loader)

### B1. 編輯器顯示可走、遊戲卻走不過去
- **症狀**:編輯器可走疊加是綠的,遊戲裡角色卻被擋。
- **原因**:早期版本用 environment trigger 當「玩家阻擋」來源,與可走層不同步就會打架。
- **解法**:已改為**玩家能不能走一律以可走層為準**;不可走格預設=牆(擋+反彈子彈);水塘/深坑用 `bulletPass` trigger 標(擋腳、子彈飛過)。見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)。

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
