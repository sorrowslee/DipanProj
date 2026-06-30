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
- 全程式建構、零 prefab，圖走 `Resources`（同 `UIBuilder` 慣例）。

### 3. 串接（`MapManager.LoadMapRoutine`）

`StartLevel` / `GoToMap` → `LoadMapInternal` 改成啟動協程，流程：

1. 開 `LoadingPanel`、`SetModule(row.module)` 換對應圖。
2. 等一幀讓它淡入，再**停留 `loadingScreenHoldSeconds` 秒**（預設 2，`WaitForSecondsRealtime`，不受暫停影響）——讓載入圖先看得到、不會一閃而過。
3. `ClearTransientGameplay()` 清上一張地圖暫態物件。
4. `yield` 跑 `mapLoader.LoadMapRoutine`（進度餵進載入頁，映射到 0~0.9）。
5. 失敗 → 關載入頁、結束；成功 → 放玩家／相機／氛圍／場景特效／怪／觸發點，進度拉到 1。
6. 關 `LoadingPanel`（淡出）。

`_loading` 旗標擋重入（傳送 watcher 在載入期間又觸發會被忽略）。**首次進場也吃載入頁**（`UIManager` 由 `UIBootstrap` 在 `BeforeSceneLoad` 就建好）。每次「載一張地圖」（第一關或換關傳送）都走同一套。

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
- `Assets/Scripts/Map/MapLoader.cs` — `LoadMapRoutine` / `BuildObjectsRoutine` / `LastLoadOk` / `objectsPerFrame`。
- `Assets/Scripts/Map/MapManager.cs` — `LoadMapRoutine`（串接載入頁）/ `loadingScreenHoldSeconds` / `_loading`。
- `Assets/Resources/Loading/<module>.png` — 各關卡載入圖。

---

*建立於 2026-06-30：地圖載入改「分幀 + 載入頁」——解決進場／換圖同步建圖造成的凍住。載入頁依關卡 module 顯示對應圖、停留 2 秒、鎖輸入不暫停；地上物分批建、回報進度。*
