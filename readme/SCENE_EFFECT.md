# 場景特效 (Scene Effects)

> 返回 [文件總覽](README.md)｜螢幕後處理氛圍見 [ATMOSPHERE.md](ATMOSPHERE.md)；一次性特效見 [VFX.md](VFX.md)；地圖載入見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)；編輯器見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)。

專案有**兩套**世界端的環境特效，名字相近、用途不同，先分清楚：

| 系統 | 誰驅動 | 範圍 | 例子 | 程式 |
|---|---|---|---|---|
| **SceneEffect（地圖級）** | `MapsTable.csv` 的 `SceneEffect` 欄（整張地圖一種） | 整張地圖 | 火雨 FireRain | `SceneEffectController` |
| **SceneFx（可放置）** | 編輯器「場景特效」工具逐個放置（`.dipanmap` 的 `sceneFx` 清單） | 單點/單塊 | 煙霧圍巾、火/冰/毒噴射、傳送門 | `SceneFxEmitter` / `PortalFx` |

兩者都與 Atmosphere（全螢幕後處理 shader）**獨立可並存**。

---

## 一、SceneEffect（地圖級，MapsTable 驅動）

`MapsTable.csv` 第 8 欄 `SceneEffect`（0/空=無、1=火雨），`MapManager` 載圖時呼叫 `SceneEffectController.ApplyMapSceneEffect(type, map)`（仿 `AtmosphereController`：自動生成、常駐、換圖即時切換並清殘留）。

**火雨（FireRain）**：仿「火焰拋擲彈」拋物線，從畫面外上方拋火球進**相機可視範圍**、落地播火光，純表演不傷人。參數在 `FireRain` 元件（`spawnPerSecond` 密度、`fallSpeed`、`arcHeight` 弧高、`flightTime`、`sizeRange`…）。目前邪佛廣場（`Main_Square`）設 `SceneEffect=1`。

---

## 二、SceneFx（可放置的粒子特效）

在地圖編輯器「**場景特效**」分頁逐個放置，存進 `.dipanmap` 的 `sceneFx` 清單，遊戲端 `MapLoader.BuildSceneFx` 載圖時依 `fxId` 對應 `SceneFxTable` 逐一生成。外觀走表、放置走實例——同「配方/武器」分表哲學。

### 實例欄位（`SceneFxInstance`，編輯器放置）

| 欄位 | 說明 |
|---|---|
| `fxId` | 特效編號（對應 `SceneFxTable` 的一列） |
| `startX/Y` | 起點（世界座標）；編輯器放**綠點** |
| `hasEnd` / `endX/Y` | 是否有終點；終點放**紅點**。stream＝流動路徑終點；portal＝矩形對角 |
| `bulge` | 弧線外鼓量（stream 用；垂直起→終連線的偏移，0=直線、正負決定鼓哪邊） |
| `w` / `h` | 大小倍率（X/Y） |
| `loop` / `intermittent` / `interval` | 循環 / 間歇 / 間歇開關各持續幾秒 |

### 外觀表（`Assets/Resources/Data/SceneFxTable.csv`）

一列一種特效外觀。欄位：`Id,Name,R,G,B,EmitPerSecond,LifeMin,LifeMax,SizeStart,SizeEnd,PeakAlpha,Turbulence,SortingOrder,Kind`。

- `Kind`：`stream`＝弧線粒子流（煙/火/冰/毒）；`portal`＝發光矩形傳送門（起/終點＝對角）。留空/缺＝stream。
- 內建列：1 煙、2 火、3 冰、4 毒氣、5 傳送門（綠）。**加新種類＝加一列**、不動程式；換顏色＝複製一列改 RGB 給新 Id。

### 兩種 Kind 的行為

**stream（`SceneFxEmitter`）**：沿「起點→控制點→終點」的二次貝茲弧線持續冒柔邊粒子，一路放大、邊飄邊自轉＋亮度隨機、先淡入再淡出。濃煙的「滾滾澎湃」感靠：花椰菜狀雜訊粒子貼圖＋自轉＋高密度大顆長壽（都在表裡調）。

**portal（`PortalFx`）**：起/終點＝矩形兩對角，畫一片**平穩發光的漸層能量光幕**（柔邊、縱向中央略亮、極緩慢細微呼吸，不流動不閃爍——平靜如湖水）。柔邊用「左右羽化 × 上下羽化」**相乘**（可分離、圓角矩形）——⚠️ 不可用 `min(ax,ay)`，那會在對角線形成脊線、放大後變明顯的 **X**（同 GroundEffect 早期「金字塔/菱形」稜角問題）。改法與貼圖解析度提高記在 [PROGRESS.md](PROGRESS.md)；**遊戲端與編輯器端的 `PortalFx.cs` 要同步改**。

### 排序注意（重要）

- `SortingOrder` 是 16-bit（≤32767），**別填上百萬會溢位繞回負數而看不到**（見 [PROBLEMS.md](PROBLEMS.md) E4）。
- 本專案排序層：背景 -1000 < 地磚 0 < 角色/怪 10 < 地上物（約 17000~22000，SortBase 繞回）。
- **煙/火等要蓋在最前** → 用 25000。**傳送門要顯示在門洞（地上物）之上** → 用 20000（低於煙、仍高於門）。填太低會被門的圖蓋住。

---

## 三、編輯器操作與即時預覽

在「場景特效」分頁：**＋新增特效** → 清單點選 → **放置起點(綠)/放置終點(紅)** 到畫布點放 → 填 `fxId` 等參數。每個特效旁有 **顯示/隱藏** 鈕＝**編輯器內即時預覽**，跑的是與遊戲**同一套** `SceneFxEmitter`/`PortalFx`/`SceneFxTable`（複製一份到編輯器專案），所以**所見即遊戲所得**；移動起/終點或改參數會即時重建預覽，刪除特效或換地圖預覽自動移除。

> **雙專案鏡像**：`SceneFxEmitter.cs` / `PortalFx.cs` / `SceneFxTable.cs` 與 `SceneFxTable.csv` 在**遊戲**（`Assets/Scripts/Map`、`Assets/Resources/Data`）與**編輯器**（`Assets/Scripts/Preview`、`Assets/Resources/Data`）**各有一份**（同 MapData/MapCoords 的鏡像慣例）。改特效外觀/行為要**兩邊一起改**才會一致。

---

## 四、做法速查

**煙霧圍巾（邪佛）**：放兩個 `fxId=1`（煙）特效，各自起點放肩、終點放頭側、`bulge` 一正一負往外鼓成兩條弧。

**傳送門**：一個 `fxId=5`，勾「有終點」，綠起點放門洞左上、紅終點放右下（矩形對角）。顏色換＝表裡複製 Portal 列改 RGB。

**噴射（火/冰/毒）**：`fxId=2/3/4`，起點放噴口；要方向就放終點、否則預設朝上噴。

---

## 五、素材

粒子/光幕目前都用**程式生成的佔位素材**（花椰菜煙塊、柔光圓、漸層矩形），零美術、static 快取。之後補真素材時，把對應 sprite 來源改成讀 `Resources`（或接 VfxTable）即可，行為不變。

---

## 相關檔案

- 地圖級：`Assets/Scripts/Map/SceneEffectController.cs`（含 `FireRain`）、`Assets/Data/MapsTable.csv`（`SceneEffect` 欄）。
- 可放置（遊戲）：`Assets/Scripts/Map/SceneFxEmitter.cs`、`PortalFx.cs`、`SceneFxTable.cs`、`Assets/Resources/Data/SceneFxTable.csv`；`MapLoader.BuildSceneFx`；`MapModel.SceneFxInstance`。
- 可放置（編輯器）：`Assets/Scripts/Tools/SceneFxController.cs`（工具＋預覽）、`Core/SceneFxOverlay.cs`（畫起/終點/弧線）、`Data/LayerData.cs`（`SceneFxInstance`）、`Scripts/Preview/*`（鏡像的特效程式）。

---

*建立於 2026-06-30、改寫於 2026-07-02：從最早「綁在 MapLoader 的佛陀煙霧 hack」演進為通用可放置的 SceneFx 系統（編輯器工具＋SceneFxTable＋stream/portal 兩 kind＋即時預覽）。地圖級 FireRain（MapsTable SceneEffect 欄）並存。*
