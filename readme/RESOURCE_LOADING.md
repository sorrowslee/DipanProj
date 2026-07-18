# 資源載入與載入頁 (Resource Loading & Loading Screen)

> 返回 [文件總覽](README.md)｜地圖載入器見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)；多圖串接見 [MAP_SYSTEM.md](MAP_SYSTEM.md)；UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)。

地圖資源（背景、地磚、地上物、怪物圖）走「執行期從磁碟載 PNG → `Texture2D.LoadImage`」的素材管線（`MapSpriteLoader`）。這是**同步**操作，一次全載會卡住主執行緒。本頁說明把載入改成**分幀 + 載入頁**的做法。

---

## 問題：進場／換圖會凍住一下

原本 `MapManager` 載圖是**同步**的：`LoadMap()` 在同一幀把背景、地磚、每個地上物（逐張讀 PNG、解碼、掃 alpha 算碰撞框）、牆碰撞、怪物全部建完。地上物一多，那一幀就會明顯卡住（進場／換圖凍住數百毫秒～數秒）。

---

## 解法：分幀載入 + 載入頁（兩者一起）

> 重點：**真正解卡的是「分幀」**，載入頁只是遮住過程。只貼載入頁、底下還是同步載，畫面一樣會凍——所以兩者一起做。

### 1. 分幀載入（`MapLoader.LoadMapRoutine`）

`MapLoader` 新增 `LoadMapRoutine(path, onProgress)` 協程版（與同步 `LoadMap` 走同一批建構方法）：

- 階段間 `yield`：背景 → 地磚 → **地上物（分批建）** → 牆碰撞 → 傳送點。
- 地上物是最重的一段（逐張載圖＋alpha 掃描），改成**每幀建 `objectsPerFrame` 個**（預設 8，Inspector 可調）。
- 透過 `onProgress(0~1)` 回報進度；結果寫進 `LastLoadOk`（呼叫端判斷成敗）。
- 同步 `LoadMap` 保留（給 `loadOnAwake` 測試用、向下相容）。

### 2. 載入頁（`LoadingPanel`，UI Overlay 層）

`Assets/Scripts/UI/Panels/LoadingPanel.cs`，建在既有 UI 框架（見 [UI_SYSTEM.md](UI_SYSTEM.md)）上：

- **Overlay 層**：壓在所有 UI（含 HUD）之上。
- `BlocksGameplayInput = true`：載入期間鎖玩家輸入（`PlayerController` 已遵守此旗標）。
- `PausesGame = false`：**不暫停**——分幀載入靠協程逐幀推進，暫停會讓逐幀 `yield` 停擺。
- 顯示**該關卡的載入圖**（`Resources/Loading/<module>.png`，找不到退純黑底）+ 底部進度條 + 百分比。
- **進度條＝美術素材（2026-07-18）**：`Resources/UI/LoadingBarPanel/`（底框蓮花吊飾＋深色軌道、金色填充、金色端蓋），錨在畫面底部中央。進度用 **RectMask2D** 遮罩裁切金色填充＝可見右緣＝遮罩寬度，金色端蓋放在同一個寬度上 → **端蓋一定黏在金條尾巴**（別用 `Image.Filled`，裁切邊會跟算出的端蓋對不上）。兩張素材沒畫在對齊位置（底框軌道 y[52,120]、金填充 y[78,180]），填充要**垂直壓成軌道高(×0.667)＋頂端對齊**才落進軌道。「載入中…XX%」用**預設字型**＋燙金漸層（新增 `UIVerticalGradient` BaseMeshEffect）＋暗描邊。版面常數 `BarWidth`/`BarBottomMargin`(越大越高)/`PercentGap`(越小越低)可調。
- 全程式建構、零 prefab，圖走 `Resources`（同 `UIBuilder` 慣例）。

### 3. 串接與「module 級預載」（`MapManager.LoadMapRoutine`）— 很重要

**設計原則：讀取頁只在「進入新大地圖（module）」時出現；同一個 module 內房間互跳不出讀取頁。**

因為資源是「每張圖用到才載」，若每次換圖都彈讀取頁，RedBridalGown 內房間互跳也讀取＝很怪。改成**依 `module` 分兩條路徑**（`MapManager` 記著 `_loadedModule`，`row.module != _loadedModule` 即為「進入新 module」）：

**A. 跨 module（進入新大地圖，如 Main→RedBridalGown、回 Main、首次進場）：**
1. 開 `LoadingPanel`、`SetModule(row.module)`、停留 `loadingScreenHoldSeconds` 秒（預設 2，`WaitForSecondsRealtime`）。
2. `ClearTransientGameplay()` 清暫態物件。
3. **預載整個 module 資源**：`mapLoader.PreloadModuleRoutine(module, …)`——把 catalog 內屬於該 `module`＋`Main`（共用）的每筆圖先解碼快取（最重的磁碟讀取＋PNG 解碼一次做完）。進度 0~0.6。
4. 分幀建目標圖 `mapLoader.LoadMapRoutine`。進度 0.6~0.95。
5. 放玩家／相機／氛圍／場景特效／怪／觸發點（`PlaceAndSetup`）。
6. `_loadedModule = row.module`；關 `LoadingPanel`。

**B. 同 module（房間互跳）：**
- **不出讀取頁**。因為資源已在進 module 時預載、全在快取，直接**同步** `mapLoader.LoadMap()` 快速建圖（命中快取、很快）→ `PlaceAndSetup` → 即時切換。

`_loading` 旗標擋重入（傳送 watcher 在載入期間又觸發會被忽略）。首次進場（`_loadedModule` 為空）＝跨 module，一定出讀取頁（`UIManager` 由 `UIBootstrap` 在 `BeforeSceneLoad` 就建好）。

> **記憶體取捨**：預載＝把整個 module 的圖解碼常駐記憶體，換到別 module 也不會自動釋放（為了房間互跳零讀取）。若記憶體吃緊，可加「離開 module 時清掉上一包快取」。
>
> **怪物貼圖**目前不在預載範圍（走 `MonsterSpriteLibrary` 另一套快取，該怪第一次出現時載入、之後快取）；要連怪一起預載可再擴充。

---

## 載入圖怎麼放

- 路徑：`Assets/Resources/Loading/<module>.png`（例：`Loading/Main.png`、`Loading/RedBridalGown.png`）。
- 依**關卡 module 名**命名，`LoadingPanel.SetModule` 自動對應；找不到用純黑底。
- 建議尺寸 **1920×1080（16:9）**，對齊 UI 參考解析度；匯入型別需為 **Sprite**。

---

## 可調參數

| 在哪 | 欄位 | 預設 | 說明 |
|---|---|---|---|
| `MapLoader` | `objectsPerFrame` | 8 | 地上物每幀建幾個（小=每幀更輕但載較久、大=快但每幀較重） |
| `MapManager` | `loadingScreenHoldSeconds` | 2 | 載入頁出現後、開始載入前的停留秒數（設 0 = 立刻載） |

---

## 之後可做

- **開場 Intro 期間預載**：利用開場漫畫／墜落動畫那段時間，在背景把第一關先載好，進 `MainScene` 就不必當場載（體感最佳）。
- 大底圖 / 大量家具的 `Texture2D.LoadImage` 仍是同步解碼，必要時再做更細的分批或快取策略。

---

## 相關檔案

- `Assets/Scripts/UI/Panels/LoadingPanel.cs` — 載入頁。
- `Assets/Scripts/Map/MapLoader.cs` — `LoadMapRoutine` / `BuildObjectsRoutine` / `PreloadModuleRoutine`（module 預載）/ `LastLoadOk` / `objectsPerFrame`。
- `Assets/Scripts/Map/MapManager.cs` — `LoadMapRoutine`（跨/同 module 分支）/ `PlaceAndSetup` / `_loadedModule` / `loadingScreenHoldSeconds` / `_loading`。
- `Assets/Resources/Loading/<module>.png` — 各關卡載入圖。

---

*建立於 2026-06-30：地圖載入改「分幀 + 載入頁」——解決進場／換圖同步建圖造成的凍住。*
*更新於 2026-07-02：改成 **module 級**——跨 module（進大地圖）才出讀取頁並預載整包資源；同 module 房間互跳命中快取、同步快速建圖、不出讀取頁。*
