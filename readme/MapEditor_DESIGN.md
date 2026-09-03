# DipanProj_MapEditor 設計與功能文件（v1.0，實作完成版）

> 燃燈計畫的 2D 地圖編輯器。本文件記錄**已實作完成**的編輯器（M0–M6 + 多輪打磨）。
> 路徑：與 `DipanProj_Main`、`BallisticsSystem` 同層級的 `DipanProj_MapEditor/`。
> 狀態：編輯器功能完整可用。**主遊戲端 runtime 載入器早已上線**（見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)；本檔早期寫的「尚未開始」已過時，2026-08-21 修正）。

---

> ### ⚠️ 地磚系統已移除（2026-08-10）
> 本專案的地面一律用**整張背景圖 ＋ 地上物**，不走地磚路線。
> 移除前實測：全部 `.dipanmap` 的 `tiles` 數量為 **0**、`GameAssets/**/Tiles/` 原始素材夾是空的。
>
> 已拿掉：工具「畫」「擦」（`EditTool.TilePaint` / `Erase`）、地磚調色盤、`PaintController`、
> `TilemapView`、`TileBrushPreview`、`TilesetService`、資料層 `TilePlacement` 與 `LayerData.tiles`（兩個專案）、
> 遊戲端 `MapLoader.BuildTiles()`、素材白名單的 `Tiles` 分類（`MapAssetCategories.All` ＋ 兩支 sync 腳本）。
> 被移除的檔案搬到 `DipanProj_MapEditor/_to_delete/tile-system/`（橋接器不能刪檔，需自行刪除）。
>
> **`tileSize` 不是地磚系統**，它是「一格等於幾個世界單位」，可走層、地上物座標、鏡頭、A* 全都靠它，**必須保留**。
>
> 下面文件中提到地磚／`Tiles/`／畫擦工具的段落屬於歷史記錄，未逐段刪除。

## 版面（2026-08-10 重整）

```
┌──────────────────────────────────────────────────────────┐
│ 頂部列 34px：新建/存檔/讀檔 · 聚焦/刷新素材/背景 · 顯示底部ui/照明預覽 · 旗標/特效預覽器 │
├──────┬────────────────────────────────────┬──────────────┤
│ 左側  │                                    │  右側屬性面板  │
│ 工具列 │        場景（Camera.rect 只畫這塊）      │   240px      │
│ 76px  │                                    │  PanelRect   │
├──────┴────────────────────────────────────┴──────────────┤
│ 底部狀態列 26px：地圖名 / module / 尺寸 ‧‧‧ 狀態訊息 ‧ 操作提示    │
└──────────────────────────────────────────────────────────┘
```

**版面尺寸的唯一真相在 `EditorUI`**：`TopBarH` / `RailW` / `StatusH` / `PaletteW` 四個常數，
加上兩個算好的矩形——`PanelRect`（所有右側面板共用）與 `ViewportRect`（場景可視區）。
**新增面板時一律用 `PanelRect`，不要自己寫座標**（以前 7 個面板各寫一份，改版面要改 7 次）。

### 為什麼工具在左側而不是頂部

頂部橫排每加一個工具就往右擠一格，2026 年已經溢出兩次（加場景特效、加照明），
第二次直接把三顆按鈕擠到螢幕外。垂直排列的空間幾乎用不完，是結構上的解法而非止血。

### 相機讓位（`Core/EditorViewport.cs`）

面板以前是 `GUILayout.BeginArea` **蓋在**場景上，地圖右緣永遠有 240px 看不到。
現在把 `Camera.rect` 縮到 `ViewportRect`，面板變成排在旁邊。Unity 自動連帶處理：

- `Camera.aspect` → 可視區比例，聚焦 `FrameMap` 自動正確
- `Camera.ScreenToWorldPoint` → 考慮 `pixelRect`，塗格/放置/拖曳座標自動正確
- `OnPostRender` 的 GL 參考線 → 一併限制在可視區內

> ⚠ **`Camera.rect` 以外不會被清除**，會殘留上一幀畫面（IMGUI 的 box 底圖是半透明的，蓋不住）。
> 所以另外生一台只負責清背景的相機：`cullingMask = 0`、`clearFlags = SolidColor`、rect 全螢幕、
> depth 比主相機低 100，主相機改成只清深度。
> 那台相機**刻意不掛 MainCamera tag** —— 全專案工具都用 `Camera.main` 取相機，掛了就會抓錯。

> ⚠ 加新的 UI 區域時記得同步 `IsPointerOverUI`，漏掉的話點 UI 會連帶在地圖上畫一筆。


## 0. 定案決策

| 項目 | 決定 |
|---|---|
| 形式 | **獨立 Unity 專案**（Unity 2022.3.62f3，與主專案同版本），可打包成獨立執行檔 |
| UI | 自製 **IMGUI**（`OnGUI`）面板 + GL 疊加；整個場景由程式組裝，幾乎不需手動接線 |
| 素材管線 | **PNG sprite + 字串 ID 目錄**；前置同步把 PNG + 自動生成的 catalog 拷進 `StreamingAssets`，runtime 載入；完全不碰 prefab / .asset / .meta / GUID |
| 物件擺放 | **自由變換**（任意位置 + 水平/垂直翻轉 + 縮放 + 旋轉 + 手動圖層 z-order） |
| 本次範圍 | **只做編輯器**（當時的定案；主遊戲 MapLoader 其後已完成，見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)） |
| Tile 尺寸 | 預設 **1 世界單位 = 256px**（依主專案 Grid CellSize 1×1、PPU 256 推得）；新建檔時可改 |
| 畫布尺寸 | 新建時以 **tile 格數**設定（旁標 px 換算）；預設 = **一個螢幕 = 18×10 格**；建好後可改尺寸，**左上角錨定、右/下邊增減** |
| 畫布外觀 | **純黑底**：沒鋪 tile 的地方＝黑（＝不可玩範圍），與參考遊戲一致；淡格線為編輯輔助 |
| 圖層 | **固定三層**（遊戲 / 可走 / Trigger）；自由新增圖層暫不做 |

### 畫布預設尺寸推導（一個螢幕）

主相機為 **Orthographic、size = 5**，解析度 **1920×1080（16:9）**。Orthographic size = 可見高度的一半 →
可見世界範圍 = 高 `2×5 = 10` 單位 × 寬 `10 × 1920/1080 ≈ 17.78` 單位 → **≈ 18×10 格**（1 格 = 1 單位 = 256px，約 4608×2560 sprite 像素）。
大部分關卡 = 一個螢幕；少數大場景把畫布設更大，編輯時靠**拖動畫面**捲動。

---

## 1. 專案結構與檔案職責

```
DipanProj_MapEditor/
├─ Assets/
│  ├─ Scripts/
│  │  ├─ Data/        純可序列化資料類別
│  │  │   ├─ MapData.cs        MapData（整張地圖）+ Vec2；CreateBlank()
│  │  │   ├─ LayerData.cs      LayerType / LayerData / TilePlacement / ObjectInstance / TriggerRegion
│  │  │   ├─ Catalog.cs        Catalog / CatalogItem；EditableModules()
│  │  │   └─ TriggerType.cs    TriggerTypeSet / TriggerTypeDef / ParamType / TriggerParam；Defaults()
│  │  ├─ IO/
│  │  │   ├─ JsonConfig.cs     Newtonsoft 共用設定（縮排、略過 null、列舉轉字串）
│  │  │   ├─ MapSerializer.cs  .dipanmap 存/讀
│  │  │   ├─ CatalogLoader.cs  讀 catalog.json、解析素材實體路徑
│  │  │   └─ TriggerTypeStore.cs  讀/寫 triggerTypes.json（首次生成內建預設）
│  │  ├─ Core/
│  │  │   ├─ MapCoords.cs            格↔世界座標、Tilemap cell 轉換
│  │  │   ├─ MapSession.cs           全域狀態（當前地圖/catalog/triggerTypes）；NewMap/Load/Save/Resize/RestoreFromJson；事件
│  │  │   ├─ EditorCamera.cs         平移/縮放/聚焦
│  │  │   ├─ GridRenderer.cs         GL 格線 + 畫布外框
│  │  │   ├─ TilemapView.cs          runtime Grid+Tilemap；畫格/清格/重建
│  │  │   ├─ BackgroundService.cs     Background 分類 → 可選背景清單
│  │  │   ├─ BackgroundView.cs        背景圖渲染在最底層、拉伸貼齊畫布
│  │  │   ├─ SpriteCache.cs          載 PNG → Texture2D（Point）、切格、整張 sprite，快取
│  │  │   ├─ TilesetService.cs       Tiles 分類 → 可畫 tile 清單；tileId 解析
│  │  │   ├─ ObjectService.cs        Environment 分類 → 可放置物件清單
│  │  │   ├─ ObjectView.cs           地上物 SpriteRenderer 渲染、Y-sort + zOrder、命中盒/原圖角點
│  │  │   ├─ ObjectSelectionOverlay  選取物件的原圖邊界藍框
│  │  │   ├─ ObjectGhostPreview.cs   物件放置半透明幻影預覽
│  │  │   ├─ TileBrushPreview.cs     地磚筆刷 footprint 預覽框
│  │  │   ├─ WalkableOps.cs          可走三態子格位元圖讀寫（可走/牆/水）
│  │  │   ├─ WalkableOverlay.cs      可走工具的綠(可走)/紅(牆)/藍(水)子格疊加
│  │  │   ├─ TriggerOps.cs           區域加/減格、hex 顏色
│  │  │   ├─ TriggerOverlay.cs       trigger 區域依類型色疊加
│  │  │   ├─ UndoManager.cs          快照式 Undo
│  │  │   ├─ UndoHotkey.cs           Cmd/Ctrl+Z
│  │  │   └─ EditorBootstrap.cs      一鍵組裝整個編輯器場景
│  │  ├─ Tools/
│  │  │   ├─ EditTool.cs       工具列舉（TilePaint / Erase / Object / Walkable / Trigger）
│  │  │   ├─ PaintController.cs    地磚畫/擦（含多格 block stamp）
│  │  │   ├─ ObjectController.cs   物件放置/選取/Ctrl 移動/磁吸/翻轉/縮放/旋轉/層/複製/座標/刪除
│  │  │   ├─ WalkableController.cs 可走/牆/水塗刷（三態、可選筆刷大小）
│  │  │   └─ TriggerController.cs  trigger 區域塗刷（加/減格、自動建區域）
│  │  ├─ UI/
│  │  │   └─ EditorUI.cs       所有 IMGUI 面板（工具列、調色盤、各工具面板、對話框、選取面板）
│  │  └─ Editor/              （Editor-only，編輯期工具）
│  │      ├─ AssetSyncTool.cs  選單「DipanMapEditor → 同步素材」
│  │      └─ M0SelfTest.cs     選單「DipanMapEditor → Self Test (M0)」序列化往返驗證
│  └─ StreamingAssets/
│      ├─ MapAssets/          ← 同步腳本/選單產出（PNG + catalog.json，git 忽略）
│      └─ triggerTypes.json   ← trigger 類型定義（進版控）
├─ Maps/                       ← 存檔輸出的 .dipanmap（與 Assets 同層，git 可選擇追蹤）
├─ Tools/sync_assets.sh        ← 開啟前置同步腳本（CLI）
├─ Packages/  ProjectSettings/
```

> **啟動方式**：空場景放一個空物件、掛上 `EditorBootstrap`、按 Play（或打包執行）。Bootstrap 會自動建立相機、各 overlay、Tilemap/Object 視圖、各輸入控制器與 IMGUI 介面。**唯一需要的套件**：`com.unity.nuget.newtonsoft-json`（已加入 manifest）。

---

## 2. 素材同步管線

### 兩種執行方式（做同一件事）

1. **Unity 選單**：`DipanMapEditor → 同步素材（全部 module）`（`AssetSyncTool.cs`，編輯期用，不需終端機）。
2. **CLI**：`Tools/sync_assets.sh`（自動化/打包前用）。`./sync_assets.sh` 搬全部；`./sync_assets.sh <關卡>` 只搬單一 module。

### 動作

1. 從每個來源（`Main` + **所有** `Modules/<關卡>`）底下，只拿 **`Environment/`、`Tiles/`、`Background/`** 三個資料夾的 **PNG**。
2. 依**原相對路徑**拷貝進 `Assets/StreamingAssets/MapAssets/`，**無條件覆蓋**（一律以主專案為準）。
3. 生成 `catalog.json`：每筆 `{ id, path, category, module, pixelSize, ppu }`。
   - `id` = 相對路徑去副檔名（例：`Modules/RedBridalGown/Tiles/tile1`），與主專案/未來 loader 共用同一字串 ID。
   - `category` = 上層資料夾名（`Tiles` / `Environment`）。
   - `module` = `Main`（共用）或關卡名；**編輯器靠它過濾**。
   - `pixelSize` = PNG 寬度（直接讀 PNG 檔頭 IHDR，不依賴 ImageMagick）。

### Module 選擇與隔離（Design B）

- 同步把**全部 module** 都搬進編輯器。**新建地圖時用下拉選一個 module**（地圖檔以 `module` 欄記住）。
- 調色盤/物件清單**只顯示 `Main` + 該地圖的 module**，即使誤同步了別的 module 也不會跑出來 → **不會跨 module 混用素材**。
- 重開既有地圖時，沿用檔案記錄的 `module`。

### 關卡資料夾／分類約定（重要）

每個關卡（`Modules/<關卡>/`）底下，這些子資料夾**同層並列**，編輯器依資料夾名決定素材用途：

| 資料夾 / category | 用途 | 在編輯器 |
|---|---|---|
| `Tiles/` | Scenario 產出的**地磚 texture**（單張或拼接圖） | 依 256px 切格，地磚調色盤、**筆刷鋪地板/牆壁** |
| `Background/` | 整幅**背景圖**（牆＋地板的關卡底圖，黑邊＝邊界） | 鋪在最底層、拉伸貼齊畫布（見 §4.6 背景層） |
| `Environment/` | **地上物** png（桌椅屏風、燈籠、橫梁、酒缸…）。**子資料夾 = 動畫地上物**（見下） | 自由變換物件擺放 |
| `Prefabs/` | Unity prefab（非 PNG，不同步） | 不進編輯器；未來 loader 用 |

#### 動畫地上物（多張圖做成一個物件）

把一個物件的多張幀圖放進 `Environment/` 底下的**一個子資料夾**，就會被當成「一個會動的地上物」：

- 例：`Environment/nature_bonfire_anim/frame_01.png … frame_08.png`。**依檔名排序 = 播放順序**，建議補零命名（`_01`、`_02`…）。
- 同步（**兩種方式都支援**：Unity 選單 `DipanMapEditor → 同步素材`，或 CLI `Tools/sync_assets.sh`）會把整個資料夾收成**一筆** catalog item：`category` 仍是 `Environment`（編輯器照樣列出）、`id` = 資料夾相對路徑、`path` = 第一幀，並附 `frameCount` 與 `frames`（各幀相對路徑）。
- 直接放在 `Environment/` 的**單張 PNG 仍是靜態物件**（與舊行為相同）；只有一張圖的子資料夾也會被當靜態。
- **播放速度（FPS）是「每個放置實例」各自設定**，存在 `.dipanmap` 的 `objects[].animFps`（預設 8），在編輯器選取面板調整 → 所以同一個動畫物件，不同實例可有不同速度。
- 碰撞框 / 血量 / 可破壞沿用第一幀的 footprint 與既有系統，動畫物件與靜態物件在這些面向沒有差別。
| `Monsters/` | 怪物圖（目前不同步） | 怪物出生點只給 id、不需圖 |

> 主專案舊關卡 `Tutorial` 的擺法相反（Environment 放 texture、Tiles 放 .asset），那是 Unity 主專案的舊習慣；**編輯器管線一律照上表新約定**。
> **地磚切格限制**：以 256px 硬切格。tile texture 寬高若非 256 的倍數，邊緣餘數會被裁掉；要完整切請把圖做成 256 的倍數（512 / 768 / 1024…）。

> 因為走 PNG + 字串 ID，**完全不碰 prefab / .asset / .meta / GUID**，跨專案不會斷引用；地圖檔只記 ID，未來 loader 再用 ID 換成真正的 prefab。

---

## 3. 檔案格式

### 3.1 地圖檔 `.dipanmap`（JSON，存於 `Maps/`）

```jsonc
{
  "format": "dipanmap", "version": 1,
  "name": "RedBridalGown_01",
  "module": "RedBridalGown",          // 此地圖可用素材 = Main + 此 module
  "backgroundId": "Modules/RedBridalGown/Background/State_Woodshed",  // 空＝不用背景圖
  "tileSize": 1.0,
  "width": 18, "height": 10,          // tile 格數
  "walkSubdiv": 4,                    // 可走層細分倍率：每 tile 切 4×4 子格（新地圖預設 4；舊地圖無此欄＝1）
  "origin": { "x": 0, "y": 0 },       // 左上角錨點；resize 由右/下邊增減
  "layers": [                         // 固定三層、有序
    {
      "id": "game-1", "name": "遊戲圖層", "type": "Game",
      "visible": true, "opacity": 1.0,
      "tiles":   [ { "x": 3, "y": 5, "tileId": "Modules/RedBridalGown/Tiles/tile1#0" } ],
      "objects": [ { "assetId": "Modules/RedBridalGown/Environment/lantern1",
                     "x": 4.2, "y": 6.0, "rot": 0,
                     "flipX": true, "flipY": false,
                     "scaleX": 1.5, "scaleY": 1.5,
                     "sortKey": 6.0,  // 通常 = y，做 top-down Y-sort
                     "zOrder": 0,     // 手動圖層：>0 往前、<0 往後；同層內再 Y-sort
                     "hp": 1,         // 可破壞血量；-1 = 不可摧毀
                     "animFps": 8,    // 動畫地上物的每實例播放幀率（靜態物件忽略）
                     "appearAfterClears": 0,   // 出現條件：完成 N 關後才出現（0 = 一開始就有）
                     "appearScope": "cycle" } ] // 出現條件範圍：cycle = 每周目重算 / lifetime = 曾達到就永久
    },
    {
      "id": "walk", "name": "可走/牆/水", "type": "Walkable",
      // 三態子格位元圖，每列一字串：'0'=可走、'1'=牆(擋+反彈子彈)、'2'=水/坑(擋腳、子彈穿過)。
      // 解析度 = 子格：列數 = height×walkSubdiv，每列長度 = width×walkSubdiv（預設全 1=牆）。
      "blocked": [ "111111111111...(72 字)", "100000000000...", ... ]
    },
    {
      "id": "trig", "name": "Trigger", "type": "Trigger",
      "regions": [ { "id": "a1b2c3d4", "name": "傳送點1", "typeId": "teleport",
                     "cells": [[10,2],[10,3]],
                     "params": { "entranceId": "door1", "targetMapId": "2", "targetEntrance": "hallDoor" } } ]
      // ⚠ 範例僅示意格式；各 trigger 型別「現在有哪些參數」的正典是編輯器 Data/TriggerType.cs
      //（teleport 目前是 entranceId/targetMapId/targetEntrance/showMarker/linkedFx，
      //  另有 markerX/markerY 錨點（2026-08-20 起），見 §4.5 與 MAP_SYSTEM.md §3.3）
    }
  ]
}
```

- **tileId 格式**：`<catalogId>#<index>`，index = 拼接圖切格後的序號（左上往右、再往下，row-major）。
- **objects**：自由變換；渲染 sortingOrder = `7000 + clamp(zOrder,-1,1)*6000 + clamp(round(-sortKey*100), 0, 5999)`（zOrder 為主、Y-sort 為輔）。**zOrder 只能是 -1 / 0 / +1**。
  > ⚠ 2026-08-18 改過：舊公式是 `1000000 + zOrder*10000 + …`，靠 16-bit 溢位繞回。那在 `zOrder=1` 時會讓地上物的排序值跑到 21960~31960，在主遊戲那邊**壓在所有表演層之上**（傷害數字、特效、離場卍字全被一個燭台蓋住）。見 [PROBLEMS.md](PROBLEMS.md) **E15**。
  > 這條公式在 `DipanProj_MapEditor/Assets/Scripts/Core/ObjectView.cs` 與主遊戲的 `MapDepthSort.cs` 各有一份，**是鏡像，改一邊要兩邊一起改**。全遊戲的排序層配置表寫在 `MapDepthSort.cs` 檔頭。
- **objects 出現條件**：`appearAfterClears`（完成 N 關後才出現，0／缺欄＝一律出現）＋ `appearScope`（`cycle`＝每周目重算〔本周目完成數〕、`lifetime`＝曾達到過就永久出現）。遊戲端 `MapLoader` **進圖當下**依「完成關卡數」判定，未達則不生此物件（連碰撞都沒有）；判定來源見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) §14。
- **walkable**：`blocked` 三態子格位元圖，每列一字串。解析度 = 子格（列數 = height×walkSubdiv、每列長度 = width×walkSubdiv）；`'0'`=可走、`'1'`=牆(擋＋反彈子彈)、`'2'`=水/坑(擋腳、子彈穿過)；初始全 1(牆)。範圍外視為牆。牆/水都直接畫在此層，**不再用 environment trigger**。
- **trigger regions**：每塊 = `cells`（[x,y] 集合）+ `name` + `typeId` + `params`（**值目前以字串存**，未來 loader 依 schema 轉型）。允許重疊、同型多塊。

### 3.2 Trigger 類型定義 `triggerTypes.json`（資料驅動）

```jsonc
{ "types": [
  { "typeId": "teleport",     "displayName": "傳送點",     "color": "#33AAFF",
    "params": [ { "key": "entranceId", "type": "String" }, { "key": "targetMapId", "type": "Int" }, { "key": "targetEntrance", "type": "String" } ] },
  { "typeId": "pickup",       "displayName": "道具拾取點", "color": "#FFCC33",
    "params": [ { "key": "itemId", "type": "String" } ] },
  { "typeId": "playerSpawn",  "displayName": "玩家出生點", "color": "#33FF88", "params": [] },
  { "typeId": "monsterSpawn", "displayName": "怪物出生點", "color": "#FF5555",
    "params": [ { "key": "monsterId", "type": "String" } ] }
] }
```

- 存於 `StreamingAssets/triggerTypes.json`（進版控、不被素材同步覆蓋）；首次找不到時由 `TriggerTypeSet.Defaults()` 生成內建預設。⚠ **上例只是示意**——內建型別早已不只四種（2026-08-21 時點共 23 種：drama/cutscene/giveItem/teleportTo/camZone/openPanel/onEnter/clearLevel…），**正典是編輯器 `Data/TriggerType.cs` 與 §4.5**。
- 新增一種 trigger 筆刷 = 加一筆定義（含參數 schema）。`ParamType` = `String / Int / Float / Bool`。
- **出生點即 trigger 類型**：怪物出生點只帶 `monsterId`，不需要圖。
- 對應的遊戲行為（傳送/拾取/生怪…）由主遊戲端實作（`MapLoader` 與觸發鏈，見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)、[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)）。

### 3.3 素材目錄 `catalog.json`（同步生成，git 忽略）

```jsonc
{ "items": [
  { "id": "Modules/RedBridalGown/Tiles/tile1", "path": "Modules/RedBridalGown/Tiles/tile1.png",
    "category": "Tiles", "module": "RedBridalGown", "pixelSize": 1104, "ppu": 256 },
  { "id": "Modules/RedBridalGown/Environment/lantern1", "path": "Modules/RedBridalGown/Environment/lantern1.png",
    "category": "Environment", "module": "RedBridalGown", "pixelSize": 500, "ppu": 256 },
  // 動畫地上物：一筆指向資料夾，多帶 frameCount + frames（依序）。path = 第一幀。
  { "id": "Modules/RedBridalGown/Environment/nature_bonfire_anim",
    "path": "Modules/RedBridalGown/Environment/nature_bonfire_anim/frame_01.png",
    "category": "Environment", "module": "RedBridalGown", "pixelSize": 500, "ppu": 256,
    "frameCount": 3, "frames": [
      "Modules/RedBridalGown/Environment/nature_bonfire_anim/frame_01.png",
      "Modules/RedBridalGown/Environment/nature_bonfire_anim/frame_02.png",
      "Modules/RedBridalGown/Environment/nature_bonfire_anim/frame_03.png" ] }
] }
```

---

## 4. 編輯器操作總覽

頂部工具列：`新建地圖 | 存檔 | 讀檔 | 聚焦 | 刷新素材 ‖ 畫 擦 物件 可走 Trigger ‖ 地圖資訊… | 狀態訊息`。
右側面板隨工具切換（地磚調色盤 / 物件調色盤 / 可走筆刷 / Trigger 面板）。

### 4.1 新建地圖

對話框：**名稱**、**Module 下拉**（決定可用素材）、**tile 尺寸**（預設 1）、**寬×高（格）**（預設 18×10，旁標 px）。
新建後畫面**純黑**（藍框＝可編輯範圍、淡格線＝編輯輔助），可走層初始全部為牆。

### 4.2 地磚（畫 / 擦）

- 右側調色盤依**拼接圖原始格狀排列**顯示。**在地磚上左鍵拖曳框選一塊**（例如 3×4，青框），右上顯示「選取：tile1 3×4」。
- 「畫」：地圖上**左鍵點/拖** → 整塊一次貼上（以游標格為**左上角**往右下鋪）。滑鼠移到場景上有**筆刷預覽框**（半透明 + 格線）顯示落點。
- 「擦」：左鍵點/拖清掉該格 tile → 回到黑底。
- 一筆拖曳（按下到放開）= 一個 Undo 步。

### 4.3 地上物（物件工具）

操作：
- **放置**：右側選素材 → 滑鼠移到場景出現**半透明幻影預覽**（原尺寸/未翻轉/未旋轉）→ 左鍵點空白處放下。**放完自動取消筆刷**（不會再因點一下又放一個）；要再放就再點一次素材。
- **選取**：左鍵點既有物件 → 出現**藍色選取框**（框＝整張原圖邊界，含透明邊，可看出去背範圍）。單純左鍵點一個物件＝只選它（收回多選）；點空白處＝清空選取。
- **多選**：**Cmd＋點**（Mac Command／Win 鍵）物件＝加選/取消該物件；**空白處左鍵拖方框**＝框選（框碰到的物件都選）。所有選取物件都畫藍框、框選中畫黃色矩形。多選後可一次**複製**（每個各複製一份、選取換成新複本）或**一起移動**（見下）。
- **移動**：**Ctrl + 左鍵拖曳**（一般左鍵不會移動，避免誤移）。**多選時 Ctrl 拖＝整組一起移動**。單選拖曳會**磁吸對齊**鄰近物件（右貼左、左右對齊、上下對齊、中心對齊；門檻約 0.35 格，拉遠即脫離）；**多選一起拖時關閉磁吸**（避免各自吸而亂跳）。
- **取消選取**：按 **ESC**（或面板「取消選取」），藍框消失方便看擺放結果。
- 選取面板（左下）：
  - 座標 **X / Y**：可直接輸入，或用 **－/＋ 按鈕（每次 ±0.1）** 微調。
  - **翻轉H / 翻轉V / 複製**（複製＝在旁邊生一個同樣大小角度的複本並選取它）。
  - **縮小 / 放大 / 旋轉 15°**。
  - **上移層 / 下移層**（zOrder；標題列顯示「層 N」）：手動壓在最上或最下，覆蓋預設 Y-sort。
  - **出現條件（完成 N 關後）**：填 N＝完成 N 關後此地上物才出現（0＝一開始就有）；N>0 時再選**範圍**「每周目」（本周目完成數、輪迴會再隱藏）或「永久」（曾完成過 N 關就一直在）。遊戲端**進圖當下**依完成關卡數判定，未達則整個不生（連碰撞都沒有）。細節見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) §14。
  - **動畫 FPS**（僅動畫地上物顯示，標題列顯示「動畫 N 幀」）：－/＋ 或直接輸入，調整**這個實例**的播放速度（寫入 `animFps`，預設 8）。
  - **播放：循環 / 乒乓(來回)**（僅動畫地上物顯示）：寫入 `pingPong`。**循環** = 0→N-1 繞回 0；**乒乓** = 0→N-1→0 來回（端點各停一幀）。AI 產的循環圖首尾常接不順、播到第二輪會跳一下，**乓乓能讓接縫消失、不必改圖**；缺點是動作會「正放再倒放」，適合佛像呼吸/發光等氛圍動畫，方向性動畫（漩渦/走路）維持循環。預覽即時反映、向下相容（舊圖預設循環）。
  - **可走**（勾選）：寫入 `walkable`，預設不勾。勾選＝這個地上物**不設碰撞、不擋路、不掛可破壞，且畫在角色下方**（排序 5 < 角色 10），走不走交給地圖「可走層」該格判定（例：木板、地毯可踩上去、且角色走在其上）。不勾＝照舊有碰撞（擋腳＋反彈子彈＝像牆）、高排序帶（會蓋住角色，如家具）。
  - **可穿越**（勾選，2026-07-18）：寫入 `passThrough`，與「可走」**互斥**（勾一個會取消另一個）。勾選＝**不設碰撞、不掛可破壞（可穿過）**，但**排序照一般地上物走 Y-sort**（依放置 Y 和角色/彼此正確前後交錯）。給「站著的穿透物」用：鬼魂／煙／光柱這種要能穿過、又要正確前後遮蔽的。跟「可走」的差別＝可走畫在角色腳下（固定低排序，給木板/地毯這種踩得上去的地板物）、可穿越依 Y 交錯（給站立物）。**踩坑**：把站立物（如鬼魂）勾成「可走」會讓它們拿固定低排序、完全不 Y-sort → 彼此遮蔽亂掉、也被玩家蓋，這種要改用「可穿越」。
  - **刪除**。
- **多選時的選取面板**：改**精簡版**（顯示「已選 N 個」＋複製全部／刪除全部／取消選取）；上面那些單物件微調（座標、翻轉、縮放、層、出現條件、動畫 FPS/播放…）要回到**單選**才會出現。整組移動/複製/刪除各算一步 Undo。
- **動畫地上物**：調色盤縮圖左上角有 `▶幀數` 標記；放到地圖上後會**即時循環播放**（編輯器內所見即遊戲內所得），各實例依自己的 `animFps` 速度播。放置/選取/移動/翻轉/縮放/血量等操作與靜態物件完全相同。
- 每個放置/移動/變換動作 = 一個 Undo 步（座標輸入框逐字打字除外，用 ± 按鈕則有 Undo）。

### 4.4 可走 / 牆 / 水（三態子格）

- 切「可走」工具 → 整張畫布顯示**綠（可走）/紅（牆）/藍（水/坑）半透明子格疊加**（只在此工具下顯示）。
- 解析度 = **子格**：每個 tile 切 `walkSubdiv`×`walkSubdiv`（新地圖預設 4×4），可細膩描邊。
- 右側三個筆刷：
  - **可走（綠）** `'0'`：玩家可走、子彈飛過。
  - **牆（紅）** `'1'`：擋玩家 ＋ **反彈子彈**（Environment layer）。
  - **水/坑（藍）** `'2'`：擋玩家、但**子彈穿過**（blocker / `Water` layer）。
- **筆刷大小**：1 / 2 / 4 / 8 / 16 / 32 / 64 / 128 子格（以游標為中心塗一個方塊；面板每列 4 顆）。大筆刷方便快速鋪大片。
- **整張地圖一鍵鈕**：「全部改可走（綠）」/「全部改牆（紅）」（`WalkableOps.FillAll`，含 Undo）。新地圖初始全是牆，要做大片可走時先按「全部改可走」再描牆，比慢慢塗快很多。
- 一筆拖曳 = 一個 Undo 步。新地圖初始全部為牆（紅）。
- **牆/水直接在此工具塗，不再有 environment trigger**（舊的「環境/牆」trigger 類型與「依不可走格建立牆」按鈕已徹底移除）。遊戲端 `MapLoader` 直接讀此三態位元圖生碰撞（見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)）。

### 4.5 Trigger

- 右側「新區域類型」列出 triggerTypes（傳送點/拾取點/玩家、怪物出生點/劇情/事件過場/**鏡頭區**…）。
  - **鏡頭區（camZone）**：玩家踩進就拉遠/位移相機、離開還原。參數 `zoom`（>1 拉遠、<1 拉近、留空=1）、`offsetX`/`offsetY`（右+/左-、上+/下- 世界單位）。細節見 [MAP_SYSTEM.md](MAP_SYSTEM.md) §2.2。
- **選類型後直接在畫布左鍵拖曳即可**（會自動建立一塊該類型區域並開始塗）；或按「＋ 手動新增空區域」。
- 區域以**類型顏色**半透明疊加，當前選取區域加亮（只在 Trigger 工具下顯示）。
- 區域清單：點選編輯、「刪」刪除；當前區域可改**名稱**、選**加格/減格**筆刷塗形狀、依類型 schema 填**參數**。
- **傳送點「外型位置」（2026-07-18）**：傳送點外型特效預設放在你塗的格子「平均中心」（整格塗、精度只有半格，常跟門的美術對不準）。選取傳送點後面板多一區「── 外型位置 ──」：按「**設定外型位置**」再點門正中央，就把外型精準放到那（存 `markerX`/`markerY` 世界座標）；按「**回到中心**」還原。畫布上選取的傳送點會畫**黃十字**＝外型實際落點（藍格＝踩踏功能區），對齊門所見即所得、不用進遊戲試。只動視覺，踩踏格子與玩家落點不變。細節見 [MAP_SYSTEM.md](MAP_SYSTEM.md) §3.3。
- **傳送點對位模式（2026-08-20）**：頂部列「**傳送點對位**」鈕（開了會自動切到 Trigger 工具）。開啟後把**遊戲真正的傳送點特效**（VfxTable ID 6，48 幀 24fps、Scale 0.4、PPU 100）畫在畫布上，整張圖每一顆同時顯示，**直接用滑鼠拖曳就能對齊門**，不必先選 trigger、不必開面板、不用猜座標進遊戲試。
  - 為什麼要有：**門的美術是畫在背景圖裡的**（地圖資料裡沒有「門在哪」這件事，全專案只有 `Main/Environment/rockDoor` 一個門是地上物），所以外型位置在現行資產結構下**無法自動對齊**，只能人工對——那就至少讓人工對的時候看得見。
  - 顯示規則：半透明＝該傳送點勾掉了「使用傳送點外型」（遊戲裡不會出現）；淡黃十字＝精準中心（光盤是柔邊的，沒十字看不出正中心）。**光盤在所有工具下都畫**（要拿它對齊背景的門或地上物），但**名稱標籤與操作提示只在 Trigger 工具下出現** —— 那才是能拖曳的地方，在物件工具下多一層文字只是干擾。
  - 拖曳只在 Trigger 工具下生效，命中半徑 `max(0.4, tileSize*0.55)`；命中就吃掉該次輸入（**不會變成塗格子**），整段拖曳算一次 Undo。
  - 資料來源刻意與遊戲同一份：配方讀主專案 `Assets/Data/VfxTable.csv` 的 ID 6、圖讀 `Assets/Resources/<AniPath>_NN.png`，跨專案直接讀磁碟（沿用 `PreviewSpriteLoader` 的作法，**不必同步素材**）。所以之後改傳送點外型，編輯器會自動跟著換；改完按「刷新素材」重讀。
  - **對位模式下傳送點格子改成「只畫外框、不填色」**：疊加層是 `OnPostRender` 的 GL，**一定畫在所有 sprite 之上**（跟 sortingOrder 無關），填了色就會把外型動畫蓋成一片藍紗。調透明度沒用，只能讓那幾格不要填。見 [PROBLEMS.md](PROBLEMS.md) **E17**。
  - **傳送點已改成「點模式」（2026-08-20）**：拖光盤＝同時搬動外型、踩踏區與落點（三者同一個錨點）；拖**右下角綠色把手**改踩踏矩形大小，面板也有「踩踏 寬/高」數字欄。畫布上**綠框＝玩家腳底進去就會傳送的範圍**，淡黃十字＝錨點。區域清單對點模式的傳送點顯示「（點）」而不是格數。格子只剩舊資料的殘留，點模式下不生效。詳見 [MAP_SYSTEM.md](MAP_SYSTEM.md) §3.3 與主專案 `Map/TeleportAnchor.cs`。
  - 檔案：`Preview/TeleportMarkerPreview.cs`（載入＋渲染＋命中測試）、`Tools/TriggerController.HandleMarkerDrag`（拖曳）、`Core/TriggerOverlay`（黃十字改成對位模式下全畫）、`UI/EditorUI`（頂部列鈕＋面板鈕）、`Core/EditorBootstrap`（掛元件）。
  - ⚠ 落點規則（有 markerX/markerY 用它、否則格子平均中心）在主遊戲 `MapLoader.BuildTeleportMarkers`、編輯器 `TeleportMarkerPreview.TryMarkerPos`、`TriggerOverlay.TryMarkerPos` **共三份**，改一處要三處一起改。
- 允許重疊、同型多塊。塗刷一筆 = 一 Undo 步；新增/刪除區域 = 一 Undo 步。

### 4.6 背景層（手繪底圖工作流）

把整幅「牆＋地板」背景圖鋪在最底，再用物件/可走/Trigger 三層疊上去，做出示意圖等級的關卡。

- **資料**：地圖 `backgroundId` 指向 catalog 的 Background 素材（空＝不用背景、走純黑底+tile）。
- **渲染**（`BackgroundView`）：背景圖在 sortingOrder `-1000`（Tilemap、物件之下），**拉伸貼齊整個畫布範圍**（origin→width×height），故可走格與物件座標都和畫面對齊。每幀同步，換背景/改尺寸/Undo 即時反映。
- **選背景**：
  - *新建對話框* 列出該 module 的 Background，點選即用；「**套用背景長寬比到畫布**」會依高算寬、減少拉伸變形。
  - *頂部「背景」鈕* 在「無 + 各背景」之間循環，可隨時替當前地圖換/清背景。
- **為何乾淨**：俯視外牆是邊界（塗牆即可、玩家走不進去，**不需遮擋**），需要遮擋玩家的內部道具本來就當地上物擺；黑邊＝牆，與黑底設計一致。
- **art 對齊提醒**：拆出來的地上物最好以它在原圖中的大小輸出，放上去 scale=1 才對得準；否則用縮放/磁吸/座標微調。
- **解析度提醒（2026-09-03）**：背景是拉伸貼齊畫布的，所以「每格幾個像素」＝圖寬 ÷ 格數。目前的 1448 寬底圖在 18 格寬只有 80 px/格、
  23 格寬只有 63 px/格，進遊戲會被放大顯示而變軟，跟角色／地上物的銳利度對不上。**整張背景圖每格至少 128px**，
  規則、各地圖建議尺寸與過渡期措施見主專案 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) §4.1。
- **混合**：背景層與 tile 層並存，可逐關決定用背景圖或用 tile 鋪地。

### 4.7 場景特效（可放置的粒子特效：煙/火/冰/毒/傳送門）

頂部「**場景特效**」分頁：逐個放置世界端粒子特效，外觀由 `fxId` 對應 `SceneFxTable.csv`（完整系統見 [SCENE_EFFECT.md](SCENE_EFFECT.md)）。

- **新增**：右側「＋ 新增特效」在地圖中心生一個，加進清單。
- **放置起/終點**：清單點選特效 → 「放置起點(綠)」/「放置終點(紅)」→ 到畫布點一下放。畫布上綠框＝起點、紅框＝終點、黃框＝選取中，中間畫弧線。stream（煙/火…）起→終＝流動路徑；portal（傳送門）起/終＝矩形對角。
- **參數**：`fxId`、`有終點`、`弧線外鼓 bulge`、`大小 w/h`、`循環/間歇/間歇間隔秒`。數字框可清空自由編輯（空＝套預設）。
- **即時預覽**：每個特效旁「**顯示/隱藏**」鈕，在編輯器內跑**與遊戲一模一樣**的特效（同一套程式複製到編輯器），移動點/改參數即時重建、刪除或換地圖自動移除。不用進遊戲就能調。
- 資料存 `.dipanmap` 的 `sceneFx` 清單（獨立於三圖層）。

---

## 5. 相機 / 畫布 / 控制鍵速查

| 操作 | 鍵 |
|---|---|
| 平移畫面 | **中鍵或右鍵拖曳** |
| 縮放 | **滾輪**（以游標為錨點） |
| 聚焦整張地圖 | 工具列「聚焦」 |
| 畫 / 擦 / 塗 | **左鍵**（點或拖） |
| 移動地上物（多選＝整組） | **Ctrl + 左鍵拖曳** |
| 地上物加選（多選） | **Cmd + 左鍵點**（Mac）/ **Win + 左鍵點** |
| 地上物框選 | 物件工具下**空白處左鍵拖方框** |
| 取消物件選取 | **ESC** |
| 復原 | **Cmd + Z**（Mac）/ **Ctrl + Z**（Win） |
| 地磚多選 | 在調色盤上**左鍵拖曳**框選 |

---

## 6. Undo（復原）

- 機制：**動作前對整張地圖做 JSON 快照**（`UndoManager`），`Cmd/Ctrl+Z` 還原最近一筆。
- 快照時機：地磚畫/擦一筆、可走一筆、trigger 一筆、trigger 區域新增/刪除、物件放置、物件 Ctrl 拖曳（整段一步）、物件所有面板動作（翻轉/縮放/旋轉/層/複製/刪除/座標±）。
- 還原走 `OnMapRebuilt` 事件：只重建 Tilemap/物件視圖，**不重新聚焦相機**。
- 上限 80 步，新建/讀檔時清空。
- **限制**：座標/參數**輸入框逐字打字**不逐字 undo；目前**只有 undo、沒有 redo**。

---

## 7. 存檔 / 讀檔

- **存檔**：輸入檔名 → 存成 `Maps/<檔名>.dipanmap`（與 Assets 同層，方便在 Finder/版控看到）。
- **讀檔**：列出 `Maps/` 內 `.dipanmap` 點選載入；載入後重建三層、清掉舊選取與快取。
- 新建/存檔/讀檔對話框**互斥**（開一個自動關其他）。

---

## 8. Unity 端設定與選單

- **一次性**：新場景放空物件、掛 `EditorBootstrap`、Play。
- **選單 `DipanMapEditor`**：
  - `同步素材（全部 module）` — 從主專案搬 Environment/Tiles + 生 catalog + Refresh。
  - `Self Test (M0)` — 建空白地圖→存→讀→比對，驗證序列化（可刪）。
- **打包注意**：格線/疊加用的 `Hidden/Internal-Colored` shader 在打包時可能被剔除，需到 Project Settings → Graphics → Always Included Shaders 加入。

---

## 9. 開發里程碑（狀態）

| # | 內容 | 狀態 |
|---|---|---|
| M0 | 專案骨架 + 資料模型 + 序列化 + 同步腳本 | ✅ 完成 |
| M1 | 畫布：相機平移縮放 + 格線 + Tilemap + 新建對話框 | ✅ 完成 |
| M2 | 地磚筆刷 + 調色盤（後加多格 block stamp + 預覽） | ✅ 完成 |
| M3 | 地上物（放置/翻轉/縮放/Y-sort，後加選取框/Ctrl移動/磁吸/座標/複製/層/幻影/ESC） | ✅ 完成 |
| M4 | 可走/不可走筆刷 + 疊加 | ✅ 完成 |
| M5 | Trigger 類型 + 區域塗刷 + 參數 | ✅ 完成 |
| M6 | 存檔/讀檔 + Undo + 黑底 | ✅ 完成 |
| 後續 | **主遊戲 runtime MapLoader（讀 .dipanmap 重建關卡）** | ✅ 已完成（見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)） |

---

## 10. 已知限制與未來可能項目

- **主遊戲載入器**：基本載入器已完成（見 readme/MAP_LOADER_SETUP.md）。
- **動畫地上物：編輯器端 + 遊戲端皆已完成**（2026-06-22）：編輯器（資料夾→一筆 catalog item、`frameCount`/`frames`、每實例 `animFps`、即時預覽、兩條同步路徑）＋ 遊戲端（`MapModel.ObjectInstance.animFps`、`CatalogItem.frameCount`/`frames`、`MapLoader.BuildObjects` 載入幀序列並原地循環播放 `AnimatedMapObject`，碰撞框/血量沿用第一幀；`MapAssetSyncTool.cs` 與 `MapIO.BuildFromGameAssets` 同步收動畫子資料夾）。
- **地磚切格**：256px 硬切，非倍數會裁邊；未支援每張自訂格數。
- **Undo**：無 redo；輸入框打字不逐字記錄。
- **調色盤捲動**：地磚調色盤目前不捲動，同 module 地磚表很多時可能超出面板。
- **多地圖管理**：一次一張，無清單。
- **新增圖層**：固定三層，未開放自由增層。
- **打包**：須處理 Always Included Shaders；獨立執行檔的 `Maps/` 與 `StreamingAssets` 路徑於不同平台需再確認。

---

## 11. 決策紀錄（討論結果）

1. 形式：獨立 Unity 專案、可打包執行檔；UI 用 IMGUI。
2. 素材：PNG + 字串 ID 目錄；同步全部 module、編輯器內下拉選、依 module 過濾隔離。
3. 分類約定：`Tiles/` = 地磚 texture、`Environment/` = 地上物。
4. 物件：自由變換；移動需 Ctrl；放完自動取消筆刷；磁吸對齊；手動 zOrder 圖層。
5. 畫布：tile 格數設定、預設 18×10、左上錨定可 resize；**純黑底**。
6. 三層固定（遊戲/可走/Trigger）；出生點為 trigger 類型；trigger 允許重疊、命名、資料驅動參數。
7. 一次編輯一張 .dipanmap；有 Undo（無 redo）。

---

*v1.0 — 編輯器 M0–M6 與多輪打磨完成；下一步為主遊戲 runtime 載入器。*

---

## 物件的「層」（zOrder）— 2026-08-19 補強顯示

`層` 不是「稍微前面一點」，它是**整帶平移**：

| 層 | 排序帶 | 實際語意 |
|---|---|---|
| -1 | 1000 ~ 6999 | 永遠被其他地上物蓋住 |
| **0** | **7000 ~ 12999** | 正常 Y 排序 — **玩家與怪物也永遠在這一帶** |
| +1 | 13000 ~ 18999 | **永遠畫在角色前面、完全不參與 Y 排序** |

⚠ **`層 +1` 只適合「放在別的東西上面、玩家永遠站不到它前面」的小型桌上物**（桌上的花瓶、供桌上的香爐／供盤／燭台）——
它們的 sortKey（放置 Y）比腳下那張桌子還高，不往前提一層就會被桌子蓋住。

**大型落地家具千萬不要設 `層 +1`**，否則玩家走到它前面、頭還是會被蓋住，而且怎麼走都沒用。
實際踩過：書房的屏風設成層 1，sortingOrder 13264，玩家站到地圖最底也才 8000。見 [PROBLEMS.md](PROBLEMS.md) **E16**。

### 怎麼看出某個物件的層

1. **選取它** → 物件面板上「上移層／下移層」按鈕的正上方有一行，直接寫出語意並上色
   （層 +1 是橘色警示、層 -1 是藍色、層 0 白色）。面板最上面那行也照舊有「層 N」。
2. **不選取也看得到**：物件工具下，場景上會把**所有 `層 ≠ 0` 的物件**畫外框常駐標示——
   **橘框＋「＋」記號 = 層 +1**、**藍框＋「－」記號 = 層 -1**（`ObjectSelectionOverlay`）。
   這是刻意做的：層設錯不點選就完全看不出來，而症狀（角色被蓋住）又只會在遊戲裡出現。
