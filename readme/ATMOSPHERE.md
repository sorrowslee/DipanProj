# 場景氛圍後處理 (Atmosphere Post-Process)

> 返回 [文件總覽](README.md)

讓不同地圖有不同「調性」的全螢幕後處理：室外白天維持正常，走過傳送點進到古墓就自動變陰森。**由資料驅動**——每張地圖的氛圍型別寫在 `MapsTable.csv` 的 `Atmosphere` 欄，跟相機的 `MapMode` 同一套思路。

採 **Built-in 算繪管線**的全螢幕後處理（`OnRenderImage` + `Graphics.Blit`），不依賴 URP 2D 燈光，也沒動到專案既有的自訂 shader。

## 三種型別（MapsTable.csv 的 `Atmosphere` 欄）

| 值 | 名稱 | 效果 | 適用 |
|---|---|---|---|
| `1` | 正常 | 不做任何處理（passthrough） | 室外、白天、大廳等 |
| `2` | 幽暗場景 + 打光 | 壓暗 + 冷色去飽和 + 暈影 + 提燈光圈；**周邊提亮到約 35%**，遠處美術仍看得見；中心偏暖、周邊偏冷 | 室內、想保留場景美術的陰森場景（紅嫁衣目前用這個） |
| `3` | 噩夢場景 + 打光 | 同上但**周邊近全黑（6%）**、統一冷色，最壓迫 | Boss 房、最恐怖的關底等 |

> **留空 / 缺欄 / 無法解析 = 預設 1（正常）**，所以舊地圖、未填的地圖都不受影響。
> type 2 與 type 3 的**提燈光圈半徑相同**（玩家身邊一小圈），只差在周邊壓多暗、暖冷處理。

## 運作方式

- **自動生成、零接線**：`AtmosphereController` 用 `[RuntimeInitializeOnLoadMethod]` 自生成、跨地圖常駐（同 PerfHud 模式），每場景把後處理元件 `AtmosphereBlit` 掛到 `Camera.main`。
- **地圖驅動**：`MapManager.LoadMapInternal` 載圖時呼叫 `AtmosphereController.ApplyMapAtmosphere(row.atmosphere)` 切換當前模式；換圖即時生效。
- **type 1**：控制器把 Blit 材質設 `null` → 直接 passthrough，等於關閉，零開銷。
- **type 2/3**：用同一個 shader `Custom/Atmosphere`，靠 `_Mode`（2 或 3）切換外觀。控制器每幀餵入玩家螢幕位置與光圈半徑，半徑以**油燈式 Perlin 明滅**輕微呼吸。
- **UI 不受影響**：後處理只作用在主相機算繪的畫面；Screen Space Overlay 的 HUD／面板在其後合成，不會被壓暗。

## 檔案

- `Assets/Scripts/Atmosphere/AtmosphereController.cs` — 單例、地圖驅動、餵 shader 參數。
- `Assets/Scripts/Atmosphere/AtmosphereBlit.cs` — 掛在主相機上的 Blit 進入點。
- `Assets/Resources/Shaders/Atmosphere.shader` — `Custom/Atmosphere`，`_Mode` 切 2/3。
- 接線：`Map/MapTable.cs`（解析 `atmosphere` 欄）、`Map/MapManager.cs`（載圖時套用）、`Data/MapsTable.csv`（`Atmosphere` 欄）。

## 怎麼調

- **每張地圖選型別**：改 `MapsTable.csv` 該列的 `Atmosphere`（1／2／3）。想讓某個房間更恐怖就改 3。
- **光圈大小**：`AtmosphereController.cs` 上方的 `InnerRadius` / `OuterRadius`（兩者等比例改，越小圈越小；目前 0.13 / 0.28）。
- **壓暗程度、冷暖色、去飽和、暈影**：`Atmosphere.shader` 的 `frag` 內，type 2／type 3 兩段各自的常數（周邊亮度 `lerp(0.35,…)`／`lerp(0.06,…)`、冷暖 tint、`0.85`／`0.82` 曝光、去飽和量等）。
- **呼吸快慢/幅度**：`AtmosphereController.cs` 的 `BreatheMin`/`BreatheMax` 與 `LateUpdate` 內 Perlin/sin 的係數。

## 備註

- 這是 Built-in 管線的覆蓋層方案，刻意不走 URP。若日後要「火把實際打光、牆會投影」那種逐光源互動，才需評估遷移 URP（會牽動既有自訂 shader）。
- 設計沿革見 [PROGRESS.md](PROGRESS.md)。先前用 git 分支比較過 spotlight（只光圈）/ full（三層）/ colorgrade（只調色）三種雛形，最後整併成本檔的 type 2（= full-soft）與 type 3（= full）。
