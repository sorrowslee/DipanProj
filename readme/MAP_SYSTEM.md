# 地圖系統：多圖串接與狀態持久化 (Map System — Linking & State Persistence)

> 返回 [文件總覽](README.md)
>
> 單張地圖的 Unity 接線見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)；編輯器操作與 `.dipanmap` 格式見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)；可破壞地上物見 [DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md)。

一個關卡（module）由**多張地圖**組成，本文件定義「如何把多張地圖串接起來」以及「離開再回來時如何維持每張地圖的狀態」。

---

## 0. 範圍分期（很重要，先讀）

本系統刻意分兩期。**Phase 1 的架構必須為 Phase 2 預留位置，不可因為先做 Phase 1 而走回頭路。**

| 期 | 內容 | 狀態 |
|---|---|---|
| **Phase 1：多圖串接** | `MapsTable.csv` + 傳送點/落點 + `MapManager` 執行期換圖；玩家保留並移動（不重生）。 | ✅ 已完成 |
| **Phase 2：地圖狀態持久化** | 每張地圖「離開再回來」都維持狀態：清掉的怪不復生、撿過的道具不再撿、觸發過的事件不再觸發、怪物掉落但沒撿的道具留在原地。**核心玩法，不可簡化。** | ✅ 已完成（2026-07-18） |

> **設計鐵則**：`MapManager` 是持久單例，從 Phase 1 就要存在；Phase 2 的「每張地圖狀態庫」是**加上去**而不是重構。任何 Phase 1 的決定如果會擋住 Phase 2，視為設計錯誤。
>
> ⚠️ **實際落點與草案不同**：Phase 2 最後**沒有**做成掛在 `MapManager` 上的 `MapState`，而是獨立的 `RunProgress` 常駐單例（把「一趟關卡」的臨時包與地圖狀態收在一起）。**完整規格見 [RUN_PROGRESS.md](RUN_PROGRESS.md)**，本文件 §5 保留當初的設計草案供對照。

---

## 1. 名詞

| 名詞 | 意義 |
|---|---|
| 關卡 / Level / **Module** | 一個主題場景，由多張地圖組成（例：`RedBridalGown`）。對應 `.dipanmap` 的 `module` 欄。 |
| 地圖 / Map | 一張 `.dipanmap`，是換圖的最小單位。每張有全域唯一整數 `ID`（見 MapsTable）。 |
| 出生點 / playerSpawn | 玩家**第一次進入該關卡**時的起點；也是傳送落點找不到時的後備落點。 |
| 傳送點 / teleport | 玩家踩到就換到另一張地圖的 trigger 區域。 |
| 落點 / teleportDest | 玩家從別張圖傳送進來時的著陸點，以 `entranceId` 命名。 |

---

## 2. MapsTable.csv（地圖總表）

放在 `DipanProj_Main/Assets/Data/MapsTable.csv`，與 `WeaponTable.csv` 等並列，runtime 以同樣方式載入（仿 `WeaponManager` 讀 CSV）。**它不走地圖素材同步**（那是 `.dipanmap` 本身的事），它就是一張普通資料表。

| 欄位 | 型別 | 說明 |
|---|---|---|
| `ID` | 整數 | **全域唯一**地圖編號。傳送點就是用這個 int 指向目標地圖。 |
| `Name` | 字串 | 地圖顯示/辨識名稱（給人看）。 |
| `Module` | 字串 | 所屬關卡，須對得上該 `.dipanmap` 的 `module` 欄（可互相驗證）。 |
| `Path` | 字串 | 地圖檔相對路徑，格式同 `MapLoader.mapPath`（例：`Modules/RedBridalGown/Maps/RedBridalGown_01.dipanmap`）。`Sync Map Assets` 會把 `.dipanmap` 同步進 `StreamingAssets/MapAssets/`，路徑一致。 |
| `IsLevelStart` | 0/1 | 是否為該 Module 的首張地圖。玩家一進入該關卡就載入這張、在它的 playerSpawn 出生。**每個 Module 必須恰好一張 = 1。** |
| `MapMode` | 1/2 | **相機模式**。`1` = 整張地圖（縮放塞滿畫面、角色變小）；`2` = 鏡頭跟隨（角色維持正常大小、鏡頭跟著玩家走）。**留空 / 缺欄 = 預設 2。** 詳見下方「2.1 相機模式」。 |
| `Atmosphere` | 1~11 | **氛圍後處理**。`1` 正常；`2` 幽暗+打光；`3` 噩夢+打光；`4` 烈日曝曬；`5` 焦土餘燼；`6` 沙塵暴（4/5/6 末日炎熱系、帶熱浪）；`7` 淺海；`8` 深海；`9` 深海+恐怖（7/8/9 海洋系、帶水下折射）；`10` 風雪；`11` 強風（10/11 山頂風系）；`12` 綿綿細雨；`13` 大雨（12/13 雨系）；`14` 陰森森林鬼霧；`15` 電視雜訊。**留空 / 缺欄 = 預設 1。** 換地圖即時切換，所以可「室外白天 → 傳送 → 古墓變陰森」。詳見 [ATMOSPHERE.md](ATMOSPHERE.md)。 |

**規則**
- `ID` 全域唯一整數（不要每個關卡各自從 1 編號），傳送點只存一個 int 最單純。
- 每個 `Module` 恰好一張 `IsLevelStart = 1`（驗證工具會檢查）。
- `Main` 之類的共用/大廳地圖也可登記在此表（`Module` 填 `Main`）。

**範例（目前只填 RedBridalGown 的第一張，之後逐張補上）**
```
ID, Name,   Module,        Path,                                                   IsLevelStart, MapMode, Atmosphere
1,  柴房,   RedBridalGown, Modules/RedBridalGown/Maps/RedBridalGown_01.dipanmap,   1,            2,       2
```

---

### 2.1 相機模式（MapMode）

由 `MapManager` 在每次載圖後，依該地圖列的 `MapMode` 套用相機行為（實作於 `MapCameraController`，仿 `TeleportWatcher` 由 MapManager 自掛、不必手動接線）。

| 模式 | 行為 |
|---|---|
| **1 整張地圖** | 相機置中地圖、`orthographicSize = 地圖高 × tileSize ÷ 2`，把整張地圖塞進畫面（地圖越大角色越小）。＝原本 `MapLoader.FitCamera`。 |
| **2 鏡頭跟隨（預設）** | `orthographicSize` 固定成「標準房間」大小（角色維持正常大小），鏡頭每幀跟著玩家移動，並**夾在地圖邊界內**（不露出地圖外黑邊）。 |

**「夠大才跟隨」門檻（重要）**：`MapMode=2` 不代表一定跟隨——只有地圖**寬或高任一超過門檻**才跟隨；不夠大的地圖即使填 2 也走整張地圖模式。這讓現有的小／適中地圖完全不受預設值 2 影響。

`MapCameraController` 的可調欄位（Inspector）：
- `followViewHeightTiles`（預設 **10**）：跟隨模式畫面顯示的高度（格），決定角色大小（＝標準房間高度，`orthographicSize = 此值 × tileSize ÷ 2`）。
- `maxWholeWidthTiles`（預設 **18**）／`maxWholeHeightTiles`（預設 **10**）：寬或高（格）**超過**此值才算「夠大」。預設值刻意設成現有適中房間的尺寸（18×10），因此所有 18×10 房間維持整張地圖模式，只有更大的地圖（如 `LivingRoom2`，10×18）會跟隨。
- `followSmoothTime`（預設 0.12）：鏡頭追上玩家的平滑時間（秒），0 = 立即對齊。

> 設計理由：現行縮小效果只由「地圖高度」造成（FitCamera 用高度撐滿），門檻用寬/高任一超過判定可同時涵蓋未來的寬地圖。預設門檻保證**不改變任何現有地圖的觀感**。

### 2.2 鏡頭區（camZone trigger — 踩上去拉遠/位移、離開還原）

一種 trigger 類型（編輯器「Trigger」工具的「鏡頭區」，淺藍）：玩家**踩進**該區域時相機平滑拉遠/位移，**離開**還原。用來做「走到佛像/大場景腳下時鏡頭拉遠看全貌」。

- **參數**（都留空有預設）：
  - `zoom`：縮放倍率，在本地圖正常縮放上再乘。`1`=不變、**>1 拉遠**（看更廣）、<1 拉近。留空=1。
  - `offsetX`：鏡頭右(+)/左(-)位移（世界單位）。留空=0。
  - `offsetY`：鏡頭上(+)/下(-)位移。要往上看到高處目標就給正值。留空=0。
- **運作**：`CameraZoneWatcher`（由 `MapManager.SetupWatcher` 自掛，仿 TeleportWatcher）每幀看玩家在不在鏡頭區，進/離開時通知 `MapCameraController.SetCameraZone / ClearCameraZone`。縮放與位移**疊加在正常相機（跟隨或整張地圖）之上**，過渡平滑（`MapCameraController.zoneTransitionTime`，預設 0.4 秒）。換圖自動還原、不殘留。
- 兩種相機模式都有效：整張地圖模式以地圖中心為基準加位移；跟隨模式拉遠若超出邊界則該軸自動置中。

---

## 3. 傳送點（單一型別、三欄互連模型）

採「**單一傳送點型**」：每個傳送點既是出口也是入口（一扇門 = 一個 trigger）。**不另設「落點」型別**——傳送進來時就落在「目標圖那個同名傳送點」上。

### 3.1 trigger 參數（資料驅動，改 `triggerTypes.json` 即可，編輯器渲染零改）

`teleport` 一種型別、三欄：

| 參數 | 型別 | 說明 |
|---|---|---|
| `entranceId` | String | **本傳送點的名字**，在這張圖內不重複（供別圖指定要落在這） |
| `targetMapId` | Int | 目標地圖的 MapsTable `ID` |
| `targetEntrance` | String | 目標地圖裡要落地的傳送點 `entranceId`；留空 = 落在目標圖的 playerSpawn |
| `showMarker` | Bool | 是否顯示傳送點外型特效（面板顯示「使用傳送點外型」）。**預設打勾＝顯示**；取消＝這個傳送點不生外型（見 3.3）。舊地圖沒這欄一律當顯示（向下相容） |

例：map1 的門 `entranceId=door1`、`targetMapId=2`、`targetEntrance=door2`；map2 的門 `entranceId=door2`、`targetMapId=1`、`targetEntrance=door1`。踩 door1 → 落在 map2 的 door2 上，走開再踩才會回去（靠落地防抖，見 4.1）。

> 編輯器原本 `teleport` 帶 `targetMap`(String)/`targetX`/`targetY`、另有沒在用的 `teleportDest` 型別。本模型把 `teleport` 改成上面三欄、並**移除 `teleportDest` 型別**（落點併入傳送點本身）。純改 `triggerTypes.json` ＋ 同步 `TriggerType.cs` 的 `Defaults()`，編輯器渲染零改。
>
> 兩個專案獨立、編輯器讀不到 `MapsTable.csv`，所以填 `targetMapId` 是**手打整數 ID**（需先在 MapsTable 約定好編號）。日後可讓編輯器載一份地圖清單做下拉，屬選用增強。

### 3.2 落地解析順序
1. 換到 `targetMapId` 指向的地圖。
2. 在新圖找 `entranceId == targetEntrance` 的傳送點，把玩家放在它中心。
3. 找不到（或 `targetEntrance` 留空）→ 退回該圖的 `playerSpawn`。
4. 都沒有 → 報 Warning，放在地圖中心避免卡死。

### 3.3 傳送點特效（視覺標記）
每張圖載入時，`MapLoader.BuildTeleportMarkers` 會在**每個 teleport 區域中心**放一個持續循環的標記特效，讓玩家看得到傳送點在哪。
- 複用 VFX 系統：`VfxTable.csv` 的特效，須是 `Loop=1` + `Duration=-1`（無限循環、由外部管理生死）。
- 由 `MapLoader.teleportVfxId` 指定（預設 **6**；填 0 = 全部不放）。特效掛進 MapRoot，換圖拆圖時一併清掉。
- **逐點關閉**：傳送點參數 `showMarker` 取消勾選時，`BuildTeleportMarkers` 會跳過那個傳送點、不生外型（例：那裡已放自己的傳送門 SceneFx、或要隱形傳送點）。預設顯示、舊地圖無此欄一律顯示。
- **外型位置＝精準錨點（2026-07-18）**：外型預設放在 teleport 格子的「平均中心」（整格塗、精度只有半格，常跟門的美術對不準）。傳送點參數可存 `markerX`/`markerY`（世界座標）＝外型的精準落點——`BuildTeleportMarkers` 有就放那、沒有才退回格子中心（向下相容）。**在編輯器點放＋畫布黃十字即時預覽**對齊門（見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md) §4.5）。只影響外型視覺，落地解析（§3.2）與踩踏功能格子都不變。
- **目前 ID 6「傳送點」暫借爆炸序列圖**（`VfxEffects/Explosive/Explosive`）頂著用。**換圖**：把 `VfxTable.csv` 第 6 列的 `AniPath`/`AniNumber`/`AnimFPS`（必要時 `Scale`/`SortingOrder`）改成新素材即可，零改程式。`SortingOrder=5`（低於角色的 10，畫在腳下像地面光圈）。

---

## 4. 執行期架構（Phase 1）

### 4.1 元件

- **`MapManager`（場景持久單例）**：整個地圖系統的大腦，**跨換圖不被拆掉**。
  - 持有：當前 `Module`、玩家物件參照、（Phase 2 的）每張地圖狀態庫。
  - `StartLevel(module)`：查 MapsTable 該 module 的 `IsLevelStart` 那張 → 載入 → **生一次玩家**於 playerSpawn。
  - `GoToMap(targetMapId, targetEntrance)`：給傳送點呼叫 → 拆舊圖 → 載新圖 → 把**既有玩家**移到落點。
- **`MapLoader`（重構）**：把現有「在 Awake 一次性建圖」抽成可重入的 `LoadMap(path)`：
  - 載入前**拆掉舊圖**並清場（見 4.3）。
  - 建背景/地磚/地上物/牆碰撞/出生點（沿用現有邏輯）。
  - **不再自己生玩家**（原本靠 `RepositionPlayerSpawn` 移 `MainSpawner`）——改由 `MapManager` 控管「生一次、之後只移動」。
  - 換圖後重新 `FitCamera`。
- **`TeleportWatcher`**：每幀把玩家世界座標換成地圖格，比對 teleport 區域的格集合；踩到就呼叫 `MapManager.GoToMap`。**含落地防抖（必要）**：因為落地後玩家就站在目標傳送點上，必須「直到玩家離開所有 teleport 格才重新武裝」，否則會立刻被彈回來。

### 4.2 玩家生命週期
- 玩家**保留同一個物件、只移動**：HP、當前武器、狀態跨地圖延續。
- 關卡第一次進入時由 `MapManager` 生一次（沿用 `MainSpawner` 的 prefab），之後每次換圖只 `transform.position` 移到落點。
- 玩家物件**不掛在 MapRoot 底下**（否則拆圖會被一起 Destroy）。

### 4.3 拆場清單（換圖時要清乾淨）
換圖前必須清掉屬於「上一張地圖」的所有東西，否則會殘留：
- `MapRoot`（背景/地磚/地上物/牆碰撞）整顆 Destroy。
- 場上所有怪物。
- 飛行中的子彈（`Sorrows.Ballistics`）。
- 地面特效（GroundEffect）、一次性 VFX。
- 玩家身上的環繞彈群組、雷射/火焰群組、佛光等持續型武器實例。

> 這塊是換圖功能**主要的工程成本**，CSV 與傳送只是配套。清不乾淨的徵狀通常是「跳圖後上一張的怪/彈還在」。

---

## 5. 地圖狀態持久化（Phase 2 — ✅ 已完成，實作為 `RunProgress`）

> 玩家離開一張地圖再回來，這張地圖**必須維持離開時的狀態**。這是核心玩法需求，**不能用「每次重建」打發**。
>
> ### ✅ 已於 2026-07-18 實作 → **請直接看 [RUN_PROGRESS.md](RUN_PROGRESS.md)**
>
> 實際行為：**同一趟關卡內**（含跨地圖來回）怪死了不復生、撿過的不再出現、破壞的家具不重建、沒撿的掉落物留原地；**完整離開關卡**（回廣場／死亡）才整個重置。
>
> 與下方草案的三點差異：
> 1. 狀態庫**不是** `MapManager` 上的 `MapState`，而是獨立單例 `RunProgress`（順帶管「臨時包」，見 [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) §6）。
> 2. 生命週期是**「一趟關卡」**而非「一張地圖」——重置的單位是 module 不是 mapId。
> 3. **不寫存檔**（純記憶體），因為沒過關的收穫本來就要歸零。
>
> 以下 5.1~5.4 為當初的設計草案，**保留供對照**（欄位名多數沿用了，落點不同）。

### 5.1 必須持久化的狀態（至少這些）
- **怪物**：被清掉的怪不復生；中途離開時**沒打完的怪**之後如何處理見 5.4。
- **道具 trigger（pickup）**：撿過的道具不再出現、不能再撿。
- **事件 trigger（event）**：觸發過的一次性事件不再觸發（開過的機關、播過的劇情旗標…）。
- **掉落物**：怪物掉落但玩家**沒撿**的道具，要保留在原地，回來還在。
- （延伸，之後界定）被打爆的家具是否保持破壞、可重複觸發的事件如何標記。

### 5.2 設計方向
- `MapManager` 持有 `Dictionary<int mapId, MapState>`（每張地圖一份狀態），**跨換圖常駐**。
- `MapState` 內容草案：
  - `deadSpawns`：哪些怪物出生實例已死（鍵 = 出生區域 id + 格座標）。
  - `consumedTriggers`：已消耗的一次性 trigger 區域 `id` 集合（pickup / event 共用）。
  - `groundLoot`：地上未撿的掉落物清單（`itemId` + 世界座標 + 來源）。
  - （延伸）`destroyedObjects`：已破壞的地上物實例 id。
- **載入一張圖的流程改為**：① `MapLoader` 照常建場 → ② 套用該 `mapId` 的 `MapState`：跳過 `deadSpawns` 的怪物出生、隱藏/不生 `consumedTriggers` 的道具與事件、依 `groundLoot` 補回地上掉落物、（延伸）跳過 `destroyedObjects`。
- **離開一張圖時**：把當前場上的可變狀態寫回該 `mapId` 的 `MapState`（誰死了、撿了什麼、地上還剩什麼）。

### 5.3 前置需求（Phase 1 就要注意，否則 Phase 2 做不到）
- **穩定 ID**：所有「會被消耗 / 改變」的東西都需要跨載入穩定的 ID。
  - Trigger 區域：`.dipanmap` 的 `TriggerRegion` **已有 `id`**（編輯器產生），可直接當鍵。✅
  - 怪物出生實例：用「出生區域 id + 格座標」當實例鍵。✅（不需改檔）
  - ~~**地上物 `ObjectInstance` 目前沒有穩定 id** ⚠️~~ → **已解**：實作直接用 **`objects` 清單索引**（`obj#N`），靠「同一張地圖檔每次解析順序一致」成立。夠用是因為進度不寫存檔；若之後要存檔，才需要改成編輯器產生的 GUID。見 [RUN_PROGRESS.md](RUN_PROGRESS.md) §4。
- ~~**尚未存在、Phase 2 需要的系統**：`pickup` 的道具/物品系統、`event` trigger 類型與其行為、怪物掉落物系統。~~ → pickup 道具系統（[INTERACTION.md](INTERACTION.md)）與怪物掉落物系統（暫定公式，見 [RUN_PROGRESS.md](RUN_PROGRESS.md) §6）**都已完成**；`event` trigger 型別仍**未做**（目前用觸發鏈的動作型 trigger 涵蓋大部分需求，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)）。

### 5.4 判定規則（✅ 已拍板並實作）
- **「清空」判定**：**逐出生點記錄**（`killedSpawns`），不做整張圖的「已清」旗標。玩家中途離開再回來 → **維持當時剩餘**（死掉的不生、沒死的照生）。
- **狀態存活範圍**：**單場記憶體、不寫存檔**。關遊戲／死亡／回廣場都重置，這是刻意的——沒過關的收穫本來就歸零。
- **跨地圖全域旗標**：已由**旗標系統**解決（周目／永久／關卡單次三種生命週期），見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)。
- **狀態範圍邊界**：實際列管四項——已清出生點、已消耗 trigger、已破壞地上物、地上未撿掉落物。見 [RUN_PROGRESS.md](RUN_PROGRESS.md) §4。

---

## 6. 驗證工具（Project Tools 選單，建議一併做）

照本專案習慣（`BuildScript` 會驗 `_Data`），加一個地圖系統健檢：
- 每個傳送點的 `targetMapId` 都存在於 MapsTable。
- 每個被指到的 `targetEntrance` 在目標圖有對應同名 `entranceId` 的傳送點（或允許退回 playerSpawn）。
- 同一張圖內 `entranceId` 不重複。
- 每個 Module 恰好一張 `IsLevelStart = 1`。
- MapsTable 的 `Path` 檔案實際存在、`Module` 與該 `.dipanmap` 的 `module` 欄一致。

---

## 7. 開放問題 / 待討論

**✅ 已拍板（2026-07-18 隨 `RunProgress` 落地）**
1. ~~地上物穩定 ID 怎麼配~~ → **用載入順序索引**（`obj#N`）。不寫存檔所以夠用。
2. ~~掉落物系統設計~~ → 已做：`InteractionManager.DropLoot` + `GroundLoot` + `RunProgress` 登記；掉落公式暫定寫在 `MonsterController`。
3. ~~狀態存活：session 內存 vs 存檔永久化~~ → **只在記憶體**，不進存檔。
4. ~~中途離開時未清的怪：整批重生 vs 維持剩餘~~ → **維持剩餘**（逐出生點記錄）。

**⏳ 仍未拍板**
1. `event` trigger 的「事件」如何定義：資料驅動（參數帶事件 id + 行為碼）還是逐個寫程式？（目前用觸發鏈的動作型 trigger 頂著）
2. 正式掉寶表（取代 `MonsterController` 的暫定公式）。
3. 結算時真背包放不下的溢出物怎麼處理（目前只印 Warning 並捨棄）。
6. 跨地圖/跨關卡的全域旗標表。

---

## 8. 分期任務清單

**Phase 1（現在做）— 多圖串接**
- [x] `MapsTable.csv`（`Assets/Data/`）+ 載入器 `MapTable.cs`（仿 WeaponTable）。
- [x] 改 `triggerTypes.json` ＋ `TriggerType.cs` 的 `Defaults()`：teleport 改三欄（`entranceId`/`targetMapId`/`targetEntrance`），移除 `teleportDest` 型別。〔編輯器 AI 已完成〕
- [x] `MapManager.cs`（持久單例）：`StartLevel` / `GoToMap`、清場、玩家保留並移動。
- [x] `MapLoader.cs` 重構成可重入 `LoadMap`（含拆舊圖 Teardown）、不再自己生玩家、catalog/sprites 移到 Awake、`SpawnMonsters` 拆公開、加 `TryGetPlayerSpawn`/`TryGetEntrance`。
- [x] `TeleportWatcher.cs`（每幀比對玩家格 + 落地防抖）。
- [x] 玩家保留並移動（生一次、之後只移動）：`MainSpawner.SpawnPlayer` 回傳 GameObject + `SpawnOnStart` 旗標；`PlayerController.ClearPersistentWeaponsForMapChange`。
- [ ] 驗證工具（Project Tools）— 尚未做（選用）。
- [ ] **Unity 接線**（見第 9 節）＋ 編輯器在地圖上補畫 teleport 區域、MapsTable 補列 — 由使用者操作。

> ~~⚠️ Phase 1 暫態行為：重進一張圖會**重新生怪**~~ → **已由 Phase 2 解決**（2026-07-18）：同一趟關卡內清過的怪不再重生。見 [RUN_PROGRESS.md](RUN_PROGRESS.md)。

**Phase 2 — 地圖狀態持久化（✅ 2026-07-18 完成，見 [RUN_PROGRESS.md](RUN_PROGRESS.md)）**
- [x] 狀態庫（落點改為獨立單例 `RunProgress`，非 `MapManager` 上的 `MapState`）。
- [x] 載入時套用狀態（`MapLoader` 跳過已清出生點／已破壞地上物；`MapManager` 載圖後重放掉落物）。
- [x] 怪物死亡記錄（`killedSpawns`，key＝區域 id + 格座標）。
- [x] pickup 道具系統 + `consumedTriggers`。
- [x] 掉落物系統 + `drops`（原座標重放、支援部分撿取）。
- [x] 地上物穩定 ID（`obj#清單索引`）+ `destroyedObjects`。
- [x] 狀態存活範圍拍板：**只在記憶體、不寫存檔**（沒過關的收穫本來就歸零）。
- [ ] `event` trigger 類型 + 行為（仍未做；目前用觸發鏈的動作型 trigger 涵蓋）。

---

## 9. Unity 接線（Phase 1，程式已就緒）

程式已寫好，剩下在 Unity 場景（`MainScene`）接線：

1. **匯入 `MapsTable.csv`**：放在 `Assets/Data/`（已建）。Unity 會自動 import 成 TextAsset。
2. **掛兩個元件**（建議放在既有的 `GameManagers` 或任一持久空物件上）：
   - **`MapTable`**：把 `MapsTable.csv` 拖進 `Maps CSV` 欄。
   - **`MapManager`**：`mapLoader` / `mapTable` / `playerSpawner` 三個參照留空會自動 `FindObjectOfType`；`Start Module` 填 `RedBridalGown`；勾 `Auto Start Level`。
3. **`MapLoader`**（場景上既有那個）：**取消勾選 `Load On Awake`**（改由 MapManager 驅動）。其餘 Layer/開關維持原樣。
4. **`MainSpawner`**：`SpawnOnStart` 會被 MapManager 在 Awake 自動關閉（不必手動，但手動取消也行）。確認 `PlayerMappings` 有 Player prefab、玩家 prefab 的 tag = `Player`。
5. **`MonsterSpawner`（EnemySpawner）**：`AutoSpawn` 取消勾選（純由地圖出生點生怪）。
6. **`TeleportWatcher`**：不必手動掛，`MapManager` 會自己 `AddComponent`。
   **`MapCameraController`**：同樣不必手動掛，`MapManager` 會自己 `AddComponent` 並依 `MapMode` 驅動；`MapManager.Awake` 會自動把 `MapLoader.fitCameraToMap` 關掉（改由相機控制器接管，整張地圖模式行為一致）。要微調跟隨縮放或門檻，可在執行後於 `MapManager` 物件上的 `MapCameraController` 改參數。
7. **地圖端**：在編輯器替每張地圖畫 `teleport` 區域、填 `entranceId`/`targetMapId`/`targetEntrance`，並在 `MapsTable.csv` 補上對應地圖列（含 `IsLevelStart`）。跑 `Project Tools → Sync Map Assets` 把 `.dipanmap` 同步進 StreamingAssets。

> 行為驗證：進 Play → 玩家出生在首張圖的 playerSpawn → 走到 teleport 格 → 換到目標圖、落在同名 `entranceId` 的傳送點 → 走開再踩才會跳回（落地防抖）。

### 新增 / 改動的程式檔
- 新增：`Assets/Data/MapsTable.csv`、`Assets/Scripts/Map/MapTable.cs`、`MapManager.cs`、`TeleportWatcher.cs`、`MapCameraController.cs`（相機模式：整張地圖 / 鏡頭跟隨）。
- 改動：`Map/MapLoader.cs`（重構可重入）、`Map/MapModel.cs`（`TriggerRegion.GetInt`）、`Map/MapCoords.cs`（`WorldToCell`）、`MainSpawner.cs`（回傳 GameObject + `SpawnOnStart`）、`PlayerController.cs`（`ClearPersistentWeaponsForMapChange`）。
- 相機模式新增（2026-06-22）：`MapsTable.csv` 加 `MapMode` 欄、`MapTable.cs` 解析 `mode`、`MapManager.cs` 接管相機（`SetupCamera` + 關閉 `MapLoader` 自動 FitCamera）、新增 `MapCameraController.cs`。

---

*建立於 2026-06-18：定調多圖串接（Phase 1，現在做）與地圖狀態持久化（Phase 2，核心玩法、不可簡化、之後實作）。Phase 1 架構須為 Phase 2 預留位置。2026-06-18 稍晚：Phase 1 程式完成（MapsTable/MapTable/MapManager/TeleportWatcher + MapLoader 重構），待 Unity 接線。*

*2026-07-27 更正：**Phase 2 早已於 2026-07-18 完成**（commit `4aa7659`「加入關卡儲存機制」），實作為獨立單例 `RunProgress` 而非草案的 `MapManager.MapState`。本文件先前一直寫著「之後實作」，§0 分期表、§5、§5.3/5.4、§7 開放問題、§8 里程碑均已更正；完整規格見 [RUN_PROGRESS.md](RUN_PROGRESS.md)。*
