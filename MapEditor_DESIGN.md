# DipanProj_MapEditor 設計規格（審閱版 v0.1）

> 燃燈計畫的 2D 地圖編輯器。本文件鎖定已討論定案的設計，供動手寫程式前審閱。
> 路徑：與 `DipanProj_Main`、`BallisticsSystem` 同層級的 `DipanProj_MapEditor/`。

---

## 0. 定案決策

| 項目 | 決定 |
|---|---|
| 形式 | **獨立 Unity 專案**（Unity 2022.3.62f3，與主專案同版本），**打包成獨立執行檔** |
| UI | 自製 runtime uGUI（不能用 Editor 的 Tile Palette / EditorWindow） |
| 素材管線 | **PNG sprite + 字串 ID 目錄**；前置腳本把 PNG + 自動生成的 catalog 拷進 `StreamingAssets`，runtime 載入 |
| 物件擺放 | **自由變換**（任意位置 + 水平/垂直翻轉 + 縮放） |
| 本次範圍 | **只做編輯器**；主遊戲的 runtime MapLoader 留待後續階段（但輸出格式先設計成 loader-ready） |
| Tile 尺寸 | 預設 **1 世界單位 = 256px**（依主專案 Grid CellSize 1×1、PPU 256、GroundEffect TileSize 1 推得）；新建檔時可自由設定 |
| 畫布尺寸 | 新建時以 **tile 格數**設定（旁標 px/世界單位換算）；預設 = **一個螢幕 = 18×10 格**（見下方推導）。建好後可改尺寸，**左上角錨定、右/下邊增減** |
| 圖層 | **固定三層**（遊戲 / 可走 / Trigger）；自由新增圖層功能暫不做，後續有需要再補 |

### 畫布預設尺寸推導（一個螢幕）

主相機為 **Orthographic、size = 5**，解析度 **1920×1080（16:9）**。Orthographic size = 可見高度的一半 →
可見世界範圍 = 高 `2×5 = 10` 單位 × 寬 `10 × 1920/1080 ≈ 17.78` 單位 → **≈ 18×10 格**（1 格 = 1 單位 = 256px，約 4608×2560 sprite 像素）。
大部分關卡 = 一個螢幕；少數大場景把畫布設更大，編輯時靠**拖動畫面**捲動編輯超出視窗的範圍。

---

## 1. 專案結構

```
DipanProj_MapEditor/
├─ Assets/
│  ├─ Scripts/
│  │  ├─ Data/      MapData / LayerData / ObjectInstance / TriggerType / Catalog（純可序列化類別）
│  │  ├─ IO/        MapSerializer（.dipanmap 讀寫）、CatalogLoader（讀 catalog.json）
│  │  ├─ Core/      EditorCamera（平移/縮放）、GridRenderer、UndoStack、SpriteCache
│  │  ├─ Tools/     TileBrush / ObjectTool / WalkableBrush / TriggerBrush（一筆刷一檔）
│  │  └─ UI/        各面板控制器（圖層列、調色盤、新建對話框、Trigger 參數編輯器）
│  ├─ StreamingAssets/MapAssets/   ← 同步腳本產出（PNG + catalog.json，git 忽略）
│  └─ Scenes/MapEditor.unity
├─ Tools/sync_assets.sh            ← 開啟前置腳本
├─ Packages/  ProjectSettings/
```

---

## 2. 素材同步管線（開啟前置腳本）

`Tools/sync_assets.sh`，每次編輯前手動執行：

1. 從 `../DipanProj_Main/Assets/GameAssets/`（及指定的 tile/環境目錄）掃描所有 **PNG**。
2. 依**原相對路徑**拷貝進 `Assets/StreamingAssets/MapAssets/`，**無條件覆蓋**（一律以主專案為準）。
3. 生成 `catalog.json`：每個可放置素材一筆 `{ id, path, category, pixelSize, ppu }`。
   - `id` = 相對路徑（去副檔名），與主專案/未來 loader 共用同一字串 ID。
   - `category` 由所在資料夾推斷（地板 / 牆壁 / 家具 / …），供右側調色盤分類。
4. （選用）為每張圖生成縮圖，或 runtime 直接縮放顯示。

> 因為走 PNG + 字串 ID，**完全不碰 prefab / .asset / .meta / GUID**，跨專案不會斷引用；地圖檔只記 ID，未來 loader 再用 ID 換成真正的 prefab。

---

## 3. 資料模型

### 3.1 地圖檔 `.dipanmap`（JSON）

```jsonc
{
  "format": "dipanmap", "version": 1,
  "name": "Tutorial",
  "tileSize": 1.0,
  "width": 18, "height": 10,          // 以 tile 格數為單位（預設一個螢幕）
  "origin": { "x": 0, "y": 0 },       // 左上角錨點；resize 由右/下邊增減
  "layers": [                         // 固定三層、有序（game / walkable / trigger）
    {
      "id": "game-1", "name": "遊戲圖層", "type": "game",
      "visible": true, "opacity": 1.0,
      "tiles":   [ { "x": 3, "y": 5, "tileId": "Tiles/GroundStone_0" } ],
      "objects": [ { "assetId": "Furniture/Table",
                     "x": 4.2, "y": 6.0, "rot": 0,
                     "flipX": true, "flipY": false,
                     "scaleX": 1.5, "scaleY": 1.5, "sortKey": 6.0 } ]
    },
    {
      "id": "walk", "name": "可走/不可走", "type": "walkable",
      "blocked": [ "1111…", "1001…" ]  // 每列一字串，'1'=不可走（預設全 1）
    },
    {
      "id": "trig", "name": "Trigger", "type": "trigger",
      "regions": [ { "id": "tp01", "name": "往大廳", "typeId": "teleport",
                     "cells": [[10,2],[10,3]],
                     "params": { "targetMap": "Hall", "targetX": 5, "targetY": 5 } } ]
    }
  ]
}
```

- **game 層**：`tiles`（格對齊，渲染走 runtime Tilemap）＋ `objects`（自由變換的家具，Y-sort 由 `sortKey` 決定）。
- **walkable 層**：每列一字串的位元圖，省空間；初始全為不可走。
- **trigger 層**：`regions` 每個是一塊塗出來的格集合 + 名稱 + 一組參數；**允許重疊**、同型可多塊（靠 `name` 區分）。

### 3.2 Trigger 類型定義 `triggerTypes.json`（資料驅動、編輯器內可增刪）

```jsonc
[
  { "typeId": "teleport", "displayName": "傳送點", "color": "#33AAFF",
    "params": [ { "key": "targetMap", "type": "string" },
                { "key": "targetX", "type": "float" },
                { "key": "targetY", "type": "float" } ] },
  { "typeId": "pickup", "displayName": "道具拾取點", "color": "#FFCC33",
    "params": [ { "key": "itemId", "type": "string" } ] }
]
```

> 新增一種 trigger 筆刷 = 加一筆定義（含參數 schema）。塗出區域時，編輯器依 schema 跳出參數表單。對應的遊戲行為（傳送/拾取邏輯）由未來主遊戲端實作，不在編輯器範圍。**出生點（玩家、怪物）也是 trigger 的一種類型**（例如 `playerSpawn` / `monsterSpawn`，後者參數帶 `monsterId`）。

### 3.3 素材目錄 `catalog.json`（同步腳本生成）

```jsonc
{ "items": [
  { "id": "Tiles/GroundStone_0", "path": "MapAssets/.../GroundStone_0.png",
    "category": "地板", "pixelSize": 256, "ppu": 256 },
  { "id": "Furniture/Table", "path": "MapAssets/.../Table.png",
    "category": "家具", "pixelSize": 512, "ppu": 256 }
] }
```

---

## 4. 編輯器功能

- **新建對話框**：名稱、tile 尺寸（預設 1）、**畫布寬×高（tile 格數，預設 18×10 = 一個螢幕，旁標 px 換算）**、原點。
- **改畫布尺寸**：建好後可改寬高，**左上角錨定、由右/下邊增減**（縮小時裁掉右/下、放大時往右/下補空白）。
- **平移畫面**：畫布大於視窗時可拖動捲動，編輯超出一個螢幕的大場景。
- **圖層面板**：固定三層（遊戲 / 可走 / Trigger）；可顯示隱藏、調透明度、鎖定、選為作用層（暫不開放新增/刪除層）。
- **Layer 1 遊戲圖層**（兩種工具共用此層）
  - *Tile 筆刷*：對齊格子，畫地板/牆壁（runtime Tilemap `SetTile`）。
  - *物件工具*：左鍵放置家具 → 選取後以畫面把手做 **水平翻轉 / 垂直翻轉 / 縮放 / 移動**；Y-sort 排序。
- **Layer 2 可走/不可走**：逐格筆刷，畫布初始全部不可走，可走/不可走兩種筆刷 + 橡皮擦。
- **Layer 3 Trigger**：先選/新增一種 trigger 類型 → 像 Layer 2 那樣塗出區域 → 填該區域參數。
- **右側預覽/調色盤**：讀 catalog 自動分類列出（含縮圖），點選即為當前要放的素材。
- **基礎操作**：平移/縮放、格線顯示、橡皮擦、復原/重做、存檔/讀檔。

---

## 5. 開發里程碑（建議順序）

| # | 內容 | 產出 |
|---|---|---|
| M0 | 專案骨架 + 資料模型 + 序列化 + 同步腳本 | 能 sync 素材、能讀寫空白 .dipanmap |
| M1 | 畫布：相機平移縮放 + 格線 + Tilemap 渲染 + 新建對話框 | 能開新地圖、看到格 |
| M2 | Layer 1 Tile 筆刷 + catalog 調色盤 | 能畫地板牆壁 |
| M3 | Layer 1 物件工具（放置/翻轉/縮放/Y-sort） | 能擺家具 |
| M4 | Layer 2 可走/不可走筆刷 | 能刷可走區 |
| M5 | Layer 3 Trigger 類型 + 區域塗刷 + 參數編輯 | 能放傳送點/拾取點 |
| M6 | 存檔/讀檔 .dipanmap + 復原重做打磨 | 完整可用編輯器 |
| 後續 | 主遊戲 runtime MapLoader（讀 .dipanmap 重建場景） | 另開階段 |

---

## 6. 已拍板的決策（討論結果）

1. **多地圖管理**：不做清單，一次編輯一張 .dipanmap。
2. **改畫布尺寸**：可改，左上角錨定、右/下邊增減。
3. **Trigger 區域**：允許重疊、同型可多塊（靠 `name` 區分）。
4. **新增圖層**：暫不做，先把固定三層做好，後續有需要再補。
5. **出生點**（玩家、怪物）：當作 trigger 類型。

---

*狀態：規格草案，待審閱後進入 M0。*
