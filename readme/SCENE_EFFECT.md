# 場景特效 (Scene Effect：地圖級世界端環境表演)

> 返回 [文件總覽](README.md)｜螢幕後處理氛圍見 [ATMOSPHERE.md](ATMOSPHERE.md)；一次性特效見 [VFX.md](VFX.md)；地圖載入見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)。

「場景特效」是**地圖級、世界空間**的環境表演（會在世界裡移動、有落點的物件），由 `MapsTable.csv` 的 `SceneEffect` 欄驅動、換圖即時切換。第一個效果是**火雨**。

> **與 Atmosphere 的分工（重要）**：Atmosphere 是**全螢幕後處理 shader**（螢幕空間，做雨/雪/霧/雜訊那種「整個畫面」的氛圍）；場景特效是**世界物件**（用 sprite、有世界座標落點、會跟地圖對齊）。兩者獨立、可並存。
>
> 為什麼火雨不做成 Atmosphere：① 螢幕 shader 沒辦法用真正的火球 sprite 素材；② 砸進場的火球需要世界落點與落地特效；③ 本作是 **45° 俯視角、看不到天空**，所以「天空紅漩渦」那類螢幕背景在這個視角站不住腳（曾試做 Atmosphere mode 16，因概念不成立已移除）。

---

## 資料驅動：`MapsTable.csv` 的 `SceneEffect` 欄

`MapsTable.csv` 第 8 欄 `SceneEffect`（向下相容：缺欄／留空／無法解析都退回 0）：

| 值 | 效果 |
|---|---|
| 0 / 空 | 無 |
| 1 | 火雨（FireRain） |

`MapTableRow` 多了 `sceneEffect` 欄，解析方式與 `Atmosphere` 一致（`MapTable.cs`）。目前邪佛廣場（`Main_Square`，ID 12）設 `SceneEffect=1`。

---

## 執行：`SceneEffectController`（仿 `AtmosphereController`）

`Assets/Scripts/Map/SceneEffectController.cs`：

- **自動生成、跨地圖常駐**（`[RuntimeInitializeOnLoadMethod]` + `DontDestroyOnLoad`，static `Instance`）。
- `MapManager` 載圖時呼叫 `SceneEffectController.ApplyMapSceneEffect(row.sceneEffect, mapLoader.Map)`（緊接在 `ApplyMapAtmosphere` 後）。
- 每次載圖會先 `StopAndClear()` 清掉上一張地圖的殘留，再依型別啟用對應效果（type 1 啟用 `FireRain`，其餘停用）。

---

## 火雨 (FireRain)

**做法＝仿「火焰拋擲彈」武器（`Sorrows.Ballistics` 的 `ParabolicBehavior`）的弧線**，但純表演、不碰戰鬥系統：

- 以穩定密度持續從**畫面外上方**往相機可視範圍內**拋**火球（起點在畫面上緣外、左右隨機偏一邊 → 斜斜丟進來）。
- 飛行採「地面位置 start→target 線性插值（固定飛行時間）＋ 視覺假高度拋物線 Y 偏移」做出弧線；火球沿飛行方向旋轉（頭在前、尾在後）。
- 落到目標點時播一次**落地火光**（擴散放大 + 淡出）後自毀。
- **範圍跟著相機可視畫面**（落點再夾在地圖範圍內），所以鏡頭走到哪、火球就丟到哪，密度看得到、不浪費。
- **純表演，不傷害任何單位**（完全不經 `CombatSystem`）。

### 可調參數（`FireRain` 元件，Inspector 或程式預設）

| 欄位 | 預設 | 說明 |
|---|---|---|
| `spawnPerSecond` | 1.5 | 每秒丟幾顆（密度） |
| `maxAlive` | 40 | 同時存在上限（保護效能） |
| `flightTime` | 0.7~1.1 | 飛行時間（秒）；越小飛越快 |
| `arcHeight` | 0.5~1.5 | 拋物線弧高；越小越接近直線 |
| `launchAboveTop` | 3 | 起點在畫面上緣外多高開始丟 |
| `throwSideRange` | 3~9 | 斜向丟入的水平偏移（越大越斜） |
| `sizeRange` | 0.6~1.2 | 火球基準大小（`_size`） |
| `sortingOrder` | 30000 | 繪製排序（**須 ≤32767**，見下方提醒） |

> 火球**視覺**在 `TossedFireball.Init` 另外放大（localScale 用 `_size×1.1 / ×2.5`，= 在基準上再放大 2×），**落地火光大小仍依 `_size`**（兩者分開調）。要「只放大火球、不動火光」就改 localScale 倍率；要連火光一起就改 `sizeRange` 或火光的 `_size * 2.7` 倍率。

> ⚠️ **`sortingOrder` 是 16-bit（−32768~32767）**：給超大值（如 2,000,000）會溢位繞回負數 → 火球被排到背景後面、整個看不到。詳見 [PROBLEMS.md](PROBLEMS.md) E4。

---

## 佔位素材（程式生成，零美術）

火球與火光目前用 `SceneEffectSprites` **程式生成**的發光圖（彗星狀火球：亮黃頭＋橘紅尾；落地：橘黃柔光圓），static 快取、整個遊戲共用，**不需要任何美術檔**。

之後補真素材時，把 `FireRain` 取 sprite 的來源（`SceneEffectSprites.Comet()` / `Glow()`）改成讀 `Resources`（或接 `VfxTable`）即可，**行為不變**。

---

## 怎麼加一個新的場景特效（速查）

1. 在 `SceneEffectController.Apply` 加一個 `type == N` 分支，啟用你的效果元件（仿 `FireRain`）。
2. 寫一個 `MonoBehaviour` 效果元件（含 `Begin(map)` / `StopAndClear()`），用 `Update` 自行生成／管理世界物件。
3. 在 `MapsTable.csv` 的 `SceneEffect` 欄填 N、更新表頭說明。
4. 注意：純表演就**別碰 `CombatSystem`**；繪製排序記得 ≤32767。

---

## 相關檔案

- `Assets/Scripts/Map/SceneEffectController.cs` — 控制器 ＋ `FireRain` / `TossedFireball` / `FireImpactFx` ＋ `SceneEffectSprites`（佔位素材）。
- `Assets/Scripts/Map/MapTable.cs` — `MapTableRow.sceneEffect` 欄與解析。
- `Assets/Scripts/Map/MapManager.cs` — 載圖時呼叫 `ApplyMapSceneEffect`。
- `Assets/Data/MapsTable.csv` — `SceneEffect` 欄。

---

*建立於 2026-06-30：場景特效框架（世界端、地圖級、MapsTable 驅動）＋第一個效果「火雨」（仿火焰拋擲彈的拋物線、純表演、跟相機、程式佔位素材）。曾嘗試以 Atmosphere mode 16 做「天空紅漩渦」，因 45° 俯視角看不到天空、概念不成立而移除。*
