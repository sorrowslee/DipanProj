# 場景氛圍後處理 (Atmosphere Post-Process)

> 返回 [文件總覽](README.md)

讓不同地圖有不同「調性」的全螢幕後處理：室外白天維持正常，走過傳送點進到古墓就自動變陰森。**由資料驅動**——每張地圖的氛圍型別寫在 `MapsTable.csv` 的 `Atmosphere` 欄，跟相機的 `MapMode` 同一套思路。

採 **Built-in 算繪管線**的全螢幕後處理（`OnRenderImage` + `Graphics.Blit`），不依賴 URP 2D 燈光，也沒動到專案既有的自訂 shader。

## 型別（MapsTable.csv 的 `Atmosphere` 欄）

分兩系：**陰森系（2/3）**用提燈光圈把玩家身邊壓出一小圈可見區；**末日炎熱系（4/5/6）**不壓暗、改暖色調並帶「熱浪扭曲」（畫面微微晃動）。

| 值 | 名稱 | 效果 | 適用 |
|---|---|---|---|
| `1` | 正常 | 不做任何處理（passthrough） | 室外、白天、大廳等 |
| `2` | 幽暗場景 + 打光 | 壓暗 + 冷色去飽和 + 暈影 + 提燈光圈；**周邊提亮到約 35%**，遠處美術仍看得見；中心偏暖、周邊偏冷 | 室內、想保留場景美術的陰森場景（紅嫁衣目前用這個） |
| `3` | 噩夢場景 + 打光 | 同上但**周邊近全黑（6%）**、統一冷色，最壓迫 | Boss 房、最恐怖的關底等 |
| `4` | 烈日曝曬 | 過曝漂白的暖白光 + 高對比 + 頂部刺眼天光 + 熱浪 | 正午、被太陽曝乾的廢土戶外 |
| `5` | 焦土餘燼 | 暗橙紅 + 煙塵壓暗 + 餘燼暖光底 + 熱浪 | 大火燒過的廢土黃昏、火場 |
| `6` | 沙塵暴 | 橙褐沙塵霧罩整個畫面、能見度與對比下降 + 熱浪 | 沙塵暴、乾旱風沙場景 |

> **留空 / 缺欄 / 無法解析 = 預設 1（正常）**，所以舊地圖、未填的地圖都不受影響。
> type 2 與 type 3 的**提燈光圈半徑相同**（玩家身邊一小圈），只差在周邊壓多暗、暖冷處理。
> type 4/5/6 不用提燈光圈，靠暖色調級 + 暈影 + 熱浪；**熱浪扭曲**＝以滾動正弦輕微位移取樣 UV（`_Time` 驅動、無需貼圖）。

## 運作方式

- **自動生成、零接線**：`AtmosphereController` 用 `[RuntimeInitializeOnLoadMethod]` 自生成、跨地圖常駐（同 PerfHud 模式），每場景把後處理元件 `AtmosphereBlit` 掛到 `Camera.main`。
- **地圖驅動**：`MapManager.LoadMapInternal` 載圖時呼叫 `AtmosphereController.ApplyMapAtmosphere(row.atmosphere)` 切換當前模式；換圖即時生效。
- **type 1**：控制器把 Blit 材質設 `null` → 直接 passthrough，等於關閉，零開銷。
- **type 2~6**：用同一個 shader `Custom/Atmosphere`，靠 `_Mode`（2~6）切換外觀。陰森系（2/3）控制器每幀餵入玩家螢幕位置與光圈半徑，半徑以**油燈式 Perlin 明滅**輕微呼吸；炎熱系（4/5/6）在 shader 內以 `_Time` 做熱浪扭曲，不需控制器額外參數。
- **UI 不受影響**：後處理只作用在主相機算繪的畫面；Screen Space Overlay 的 HUD／面板在其後合成，不會被壓暗。

## 檔案

- `Assets/Scripts/Atmosphere/AtmosphereController.cs` — 單例、地圖驅動、餵 shader 參數。
- `Assets/Scripts/Atmosphere/AtmosphereBlit.cs` — 掛在主相機上的 Blit 進入點。
- `Assets/Resources/Shaders/Atmosphere.shader` — `Custom/Atmosphere`，`_Mode` 切 2~6。
- 接線：`Map/MapTable.cs`（解析 `atmosphere` 欄）、`Map/MapManager.cs`（載圖時套用）、`Data/MapsTable.csv`（`Atmosphere` 欄）。

## 怎麼調

- **每張地圖選型別**：改 `MapsTable.csv` 該列的 `Atmosphere`（1~6）。想讓某房間更恐怖改 3；要末日炎熱改 4／5／6。
- **光圈大小**（type 2/3）：`AtmosphereController.cs` 上方的 `InnerRadius` / `OuterRadius`（兩者等比例改，越小圈越小；目前 0.13 / 0.28）。
- **各型別的調色／壓暗／冷暖／去飽和**：`Atmosphere.shader` 的 `frag` 內，type 2~6 各自一段的常數（如 type2 周邊亮度 `lerp(0.35,…)`、type4 過曝 `*1.22+0.06`、type5 橙紅 tint、type6 沙塵色 `lerp(…,float3(0.62,0.50,0.34),0.30)` 等）。
- **熱浪扭曲強度/快慢**（type 4/5/6）：`Atmosphere.shader` 頂部 frag 內熱浪段的位移幅度 `0.0014` / `0.0011` 與頻率 `38.0`/`21.0`/`30.0`、時間係數 `3.0`/`2.1`/`1.7`。
- **呼吸快慢/幅度**（type 2/3）：`AtmosphereController.cs` 的 `BreatheMin`/`BreatheMax` 與 `LateUpdate` 內 Perlin/sin 的係數。

## 備註

- 這是 Built-in 管線的覆蓋層方案，刻意不走 URP。若日後要「火把實際打光、牆會投影」那種逐光源互動，才需評估遷移 URP（會牽動既有自訂 shader）。
- 設計沿革見 [PROGRESS.md](PROGRESS.md)。陰森系先前用 git 分支比較過 spotlight／full／colorgrade 三種雛形，整併成 type 2（= full-soft）與 type 3（= full）；末日炎熱系同樣分支比較 noon／ember／dust，整併成 type 4／5／6。
- 熱浪扭曲是全螢幕均勻的克制版微晃；若日後要更真實（只在地面/火源附近、或更強的地平線蜃景），可改成漸層遮罩或局部套用。
