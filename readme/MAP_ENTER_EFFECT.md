# 進場一次性效果 (Map Enter Effect) — 睜眼醒來

> 返回 [文件總覽](README.md)
>
> 與**持續性**的 [ATMOSPHERE.md](ATMOSPHERE.md)（螢幕氛圍後處理）、[SCENE_EFFECT.md](SCENE_EFFECT.md)（世界端場景特效）分工：本系統是**進圖時播一次就結束的過場**。
>
> **狀態：✅ 程式完成（2026-07-03），待 Unity 實機驗證。** 第一個效果：睜眼醒來（用在初始洞窟 Main_Cave）。

一種**進入地圖時播放一次**的螢幕後處理過場，語意是「一次性事件」而非「持續狀態」——所以刻意不塞進 Atmosphere / SceneEffect（那兩個是換圖就套上、一直套著的持續系統）。獨立成一套，之後「闔眼昏迷」「中毒暈眩」「被打到眼前發黑」都能往這裡加型別。

---

## 0. 統一登記：ScreenFxTable（2026-07 整併）

「全螢幕過場特效」原本散成兩條路、id 空間還不一樣（EnterEffect 的 1＝睜眼、screenFx 的 1＝破幻術）。現已整併成**單一登記表**，避免同一種特效兩邊各設一次：

- 唯一表：`Assets/Data/ScreenFxTable.csv`（欄位 `Id,Name,Key,DurationSeconds,WakeUpPose,Notes`）。**（2026-07-22 起與其它資料表一起搬到 `Assets/Data`，靠 `ScreenFxTableProvider` 載入；舊路徑 `Assets/Resources/ScreenFxTable.csv` 已淘汰。見 [PROBLEMS.md](PROBLEMS.md) I 區「資料表搬家」。）**
- 目前 id：**1 = 睜眼醒來**、**2 = 破幻術**、**3 = 馬賽克清晰**。`0` = 無特效。
- **三個填寫入口**共用同一份 id → **同 id 同效果**：
  1. `MapsTable.csv` 的 `EnterEffect` 欄（進目標圖載完後播一次）。
  2. 劇情編輯器（「劇情」工具）的 `screenFx` 步驟（`assetId`＝id）。
  3. 觸發鏈的 `playScreenFx` 動作（`effectId`＝id）。
- 分派：三邊都經 `ScreenFxPlayer.Play(id, onDone, duration)` → switch 到對應控制器（`EyeOpenController` / `IllusionShatterController` / `MosaicController`）。`ScreenFxPlayer.IsAnyPlaying` 供「進場觸發等特效播完」輪詢。

> ### ⏱ `DurationSeconds` 是「預設時間」，會被呼叫端的 override 蓋掉（踩過的坑）
>
> `ScreenFxTable.csv` 的 `DurationSeconds` **只是預設值**——`ScreenFxPlayer.Play(id, onDone, duration)` 的第三個參數 `duration ≥ 0` 就以它為準、**完全不看 CSV**；`duration < 0`（傳 `-1`）才回退去讀 CSV 的 `DurationSeconds`。三個入口帶不帶 override：
>
> | 入口 | override 欄位 | 行為 |
> |---|---|---|
> | `MapsTable` `EnterEffect` | 無 | `Play(id, null)` 不帶時間 → **永遠吃 CSV `DurationSeconds`** |
> | 劇情 `screenFx` 步驟 | 「停留秒數」(`seconds`) | `seconds > 0` → 用 seconds（蓋掉 CSV）；`= 0`/留空 → 回退吃 CSV |
> | 觸發鏈 `playScreenFx` | `duration` 參數 | 有填 → 用它（蓋掉 CSV）；留空 → 回退吃 CSV |
>
> **典型症狀**：改 `ScreenFxTable.csv` 的 `DurationSeconds` 完全沒反應。多半是那個特效是從**劇情 `screenFx` 步驟**觸發，而那一步的「停留秒數」被填了值（例：初始森林 `Main_InitialForest1` 的進場演出第 3 步 mosaic 填了 `seconds=1`），override 掉了 CSV。要吃 CSV 就把該步的「停留秒數」清成 0；要就地各設時長就直接調那個欄位（此時 CSV 只是沒被用到的預設）。
- `WakeUpPose=1`（目前只有睜眼）＝當 EnterEffect 時連動玩家「趴地→起身」（原本程式寫死 `enterEffect==1`，現改讀表）。
- **新增一種螢幕特效的維護點**：①`ScreenFxTable.csv` 加一列 ②寫 shader＋控制器（提供 `static Play(onDone,duration)`）③`ScreenFxPlayer.Play` 加 case ④編輯器 `EditorUI.ScreenFxCatalog` 同步一列。
- ⚠️ 破幻術的 id 從 1 改成 2；既有 `RedBridalGown_BridalRoom.dipanmap` 的 `playScreenFx` effectId 已一併改為 2。

### 馬賽克清晰（id 3）
像素馬賽克格由粗到細慢慢收斂成清晰畫面（`_Progress` 0→1）。shader＝`Resources/Shaders/Mosaic.shader`、控制器＝`MosaicController`。與睜眼／破幻術不同，**它不自行暫停/鎖輸入**，暫停/鎖輸入交給呼叫端（劇情 `lockInput`）。
（原因：它用的是 `SetExternalHold` 的**舊兩參數多載**，那個共用一個預設 key，在劇情內自行 hold 會把劇情的鎖一起解掉。
2026-08-18 起 `SetExternalHold` 已支援**具名持有者**，要改成自行 hold 的話帶 owner 就安全了——見 [PROBLEMS.md](PROBLEMS.md) D13。）典型用法：山道劇情亮起後、進場觸發對話前放一格 `screenFx=3`。

---

## 1. 睜眼醒來（type 1）

用在**初始洞窟 Main_Cave**：主角從高處墜落昏迷，進洞窟場景後「睜開眼睛」的感覺。時間軸（總長 `duration`，預設 2.6 秒，可調）：

```
承接全黑（接墜落昏迷）→ 眼皮裂開一條縫（露出模糊、偏暗的洞窟）→ 沉重眨一下（闔回）
→ 逐漸對焦（模糊轉清晰）、亮度回正、眼皮完全睜開 → 移除後處理、恢復正常畫面
```

視覺：上下對稱的**杏眼狀眼皮遮罩**（中間開得多、兩側窄）＋**景深模糊由大轉 0**（剛醒視線糊）＋**亮度由暗回正**＋**暗角越沒睜開越重**。

### 1.5 玩家「趴地 → 起身」連動（2026-07-07 加）

睜眼（EnterEffect=1）自動連動玩家的甦醒表演，**零新素材**——爬起動畫＝把該血統的 `dead` 逐格幀**倒著播**（趴地幀 → 站立幀）：

```
進圖放好玩家 → 立刻定格在 dead 最後一幀（趴地）→ 睜眼過場播放（遊戲暫停）
→ 睜眼播完 → 倒播 dead＝爬起（定住輸入、不暫停）→ 回 idle、恢復操作 → 才點火「進場觸發」
```

- 程式：`PlayerAnimator.HoldLyingPose()`（趴地定格）＋ `PlayerAnimator.PlayWakeUp(onDone)`（倒播爬起，速率同 BaseFps）；
  `MapManager.PlaceAndSetup` 只記需求（`_wakeUpWanted`），趴地與起身都在 `MapManager.FireEnterTriggersRoutine` 執行——
  因為玩家**第一次生成**時 `PlayerAnimator.Setup` 在 `Start()` 才載幀，PlaceAndSetup 當下拿不到 dead 圖；
  協程開跑時已載好，且睜眼開頭全黑（眼皮閉合）蓋住趴下前的站姿瞬間。
- 表演期間 `SetState` 被忽略（HandleVisuals 每幀塞 Idle 也蓋不掉趴姿）；**真死（Dead）例外**會打斷表演。
- 防呆：該血統沒有 `dead/` 圖 → 整段跳過（只播睜眼、不趴地）。
- 順序保證：**onEnter 進場觸發（對話等）一定在起身完成後才點火**（見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3）。

---

## 2. 檔案與運作

| 檔案 | 角色 |
|---|---|
| `Assets/Resources/Shaders/EyeOpen.shader` | 全螢幕後處理：眼皮/杏眼遮罩(`_Open`) + 圓盤模糊(`_Blur`) + 亮度暗角(`_Bright`)。 |
| `Assets/Scripts/MapFx/EyeOpenController.cs` | 自生成常駐單例；`Play()` 用 3 條 `AnimationCurve`（`_Open`/`_Bright`/`_Blur`，眨眼藏在 open 曲線關鍵影格）驅動時間軸；播放中把 `EyeOpenBlit` 掛到 `Camera.main`，播完停用該 blit 恢復畫面。仿 `AtmosphereController`。 |
| `EyeOpenBlit`（同檔） | 相機上的 `OnRenderImage` 全螢幕 Blit（同 `AtmosphereBlit`）。 |

**資料驅動**：`MapsTable.csv` 第 9 欄 `EnterEffect`（0=無、1=睜眼；向下相容，缺欄/空/無法解析都當 0）。`MapManager.PlaceAndSetup` 進圖時呼叫 `EyeOpenController.ApplyMapEnterEffect(row.enterEffect)`（就在套 Atmosphere / SceneEffect 的同一處）。**每次進該圖都播一次**（非持久狀態、無存檔旗標）。

- 後處理只作用在主相機畫面；Screen Space Overlay 的 UI（HUD/面板）在其後合成、不受影響。相機上同時有 `AtmosphereBlit` 時，睜眼在其後合成（過場疊在氛圍之上），播完停用、回到只剩氛圍。
- 與載入頁的銜接：時間軸開頭是「全黑停一下」，剛好蓋過 `LoadingPanel` 收尾的那一下，交接無縫。
- **播放中暫停＋擋操作**：`Play()` 呼叫 `UIManager.SetExternalHold(true, true)`（新增的「非面板系統暫停/擋輸入」掛勾）→ 遊戲暫停（`Time.timeScale=0`）且玩家不能操作（`PlayerController` 查 `IsGameplayInputBlocked`）；`Finish()` 解除。睜眼時間軸用 `unscaledDeltaTime`，所以暫停中照樣播。此掛勾與面板需求一起參與 `UIManager.Recompute`，**不會被載入頁關閉時的重算覆蓋**（若直接設 `Time.timeScale` 就會被蓋，這是本作法的重點）。場景被切換打斷時 `OnSceneLoaded` 會安全解除。

---

## 3. 怎麼用 / 調整

- **哪張地圖要睜眼**：`MapsTable.csv` 該列 `EnterEffect` 填 `1`（目前 Main_Cave（11）已填）。要拿掉就改回 0/留空。
- **節奏/強度**：改 `EyeOpenController` 的 `duration`（總長）、`maxBlur`（剛醒模糊強度），或 `Awake` 裡三條 `AnimationCurve` 的關鍵影格（眨幾下、裂開多少、亮度曲線）。
- **加新型別**（如闔眼昏迷）：在 `EyeOpenController` 加對應時間軸、`ApplyMapEnterEffect` 依 type 分派，`MapsTable` 的 `EnterEffect` 填新編號即可。

---

## 4. 實機驗證

1. 開 Unity 等編譯，Console 無紅錯（新程式在預設 `Assembly-CSharp`；shader 在 `Resources/Shaders`）。
2. 進入 Main_Cave（新建遊戲走開場鏈墜落後、或直接把 Main 模組設起始關卡測）：畫面應**由全黑 → 眼皮撐開 → 眨一下 → 對焦清晰**，約 2.6 秒後恢復正常、可操作。
3. 若 `MapsTable.csv` 剛改完沒生效：右鍵該 CSV → Reimport（TextAsset 需重新匯入）。
4. 若完全沒效果、Console 出現「找不到 Resources/Shaders/EyeOpen」：確認 shader 檔在 `Assets/Resources/Shaders/`。

---

## 附：離場螢幕特效（含破幻術）—— 不走 EnterEffect，是「觸發鏈動作 `playScreenFx`」

「進場一次性效果」（EnterEffect，本檔上半）是**綁地圖、在目標圖載完後播**的螢幕後處理。另一個需求是**離開/劇情當下、還在舊場景時**播一次過場（例：破幻術「幻境崩碎回歸現實」），這用**觸發鏈動作 `playScreenFx`**（不綁地圖、不走 MapsTable）：由劇本在還在幻境場景時接鏈播放，玩家親眼看到「當前這張場景」崩碎、收尾全白，播完再由鏈接 `teleportTo` 傳去現實地圖。

為什麼破幻術不做成 EnterEffect：EnterEffect 在**目標圖載完後**才播（只吃得到新場景畫面）、且**綁地圖＝每次進該圖都播**。破幻術要的是「崩的是**舊**幻境、只在這個劇情節點播一次」，所以走鏈動作（見紅嫁衣「沒殺家人」分支）。

**資料驅動、不再為每種特效加 trigger 型別**：`playScreenFx` 只有一顆，填一個 `effectId`（欄旁「螢幕特效表」按鈕可查/填）。id → 特效由遊戲端 `ScreenFxPlayer.Play` 分派。id 一律以 `ScreenFxTable.csv` 為準（目前 **1=睜眼醒來、2=破幻術、3=馬賽克清晰**，見本檔開頭；破幻術的 id 在整併時從 1 改成 2）。

- 加一種螢幕特效的三個維護點：① 寫 shader＋控制器（仿 `IllusionShatterController`／`EyeOpenController`）；② `ScreenFxPlayer.Play` 加一個 `case`；③ 更新編輯器「螢幕特效表」清單（`EditorUI.ScreenFxCatalog`）＋本節。
- 破幻術程式：`Assets/Scripts/MapFx/IllusionShatterController.cs`（自生成常駐單例、曲線驅動、`unscaled` 時間、blit 掛主相機、`SetExternalHold` 暫停擋操作）＋ `Resources/Shaders/IllusionShatter.shader`（voronoi 玻璃裂紋＋碎塊崩落色散＋白光）。
- 接線與參數（`effectId`／`duration`）：見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3（`playScreenFx`）與 §7（紅嫁衣分支步驟）。
- ⚠️ 命名：這裡的「螢幕特效」＝一次性全螢幕後處理過場，跟世界端的 **場景特效**（`SceneEffect` 火雨／`SceneFx` 煙霧傳送門，見 SCENE_EFFECT.md）是**兩套不同東西**，別混。
- 破幻術目前是**程序版**（崩碎層＝當前畫面剝掉暖色濾鏡的複本，露出白光）。若之後要「真的從紅嫁衣畫面崩到榕樹妖畫面」的雙層交叉，需在換圖前抓一張畫面截圖餵給 shader（capture 版，plumbing 較多），可再升級。

---

*建立於 2026-07-03：進場一次性效果系統＋第一個效果「睜眼醒來」（後處理版：杏眼遮罩＋模糊對焦＋亮度暗角，MapsTable `EnterEffect` 欄驅動、進圖播一次）。*
*2026-07-09 附記（歷史紀錄——當時破幻術的 id 是 1，**整併後已改為 2**，見上方 ⚠️）：離場螢幕特效改用泛用鏈動作 `playScreenFx`＋`effectId`（不再為每種特效加 trigger 型別）＋編輯器「螢幕特效表」picker。*
