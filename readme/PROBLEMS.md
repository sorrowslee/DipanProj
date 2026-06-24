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
- **解法**:已改為**玩家能不能走一律以可走層為準**。**牆/水的判定**(2026-06-22 更新):有「環境/牆」(environment) trigger 區域時,牆=environment 格、不可走但非 environment=水塘;沒有 environment 區域的舊地圖則退回舊模型(不可走=牆、`bulletPass`=水塘)。見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)。

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
