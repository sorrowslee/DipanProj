# 劇情系統 (Drama：觸發點 ＋ 劇情檢視介面)

> 返回 [文件總覽](README.md)｜靠近按 F 的互動共用機制見 [INTERACTION.md](INTERACTION.md)｜UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)｜資料表載入慣例見 [INVENTORY.md](INVENTORY.md)

玩家走到地圖上的「劇情觸發點」，靠近按 **F** 跳出一個劇情檢視介面（一張大圖 + 一段文字）。觸發互動本身（星星標示、靠近按 F、消耗）完全複用 [INTERACTION.md](INTERACTION.md) 的 `InteractionManager`——本檔只談**劇情專屬**的部分：資料表、檢視介面。

---

## 編輯器：劇情觸發點 trigger

預設 trigger 多了一種 `drama`「劇情觸發點」（紫色 `#AA66FF`），參數 **`dramaId`(Int)**。在編輯器畫一塊 `drama` 區域、填 `dramaId`，遊戲端就會在該處放**紫色星星**、靠近顯示「按 F 鍵」、按 F 開對應劇情。

- 定義在 `DipanProj_MapEditor` 的 `triggerTypes.json` ＋ `TriggerType.cs` 的 `Defaults()`（兩處同步）。
- runtime 由 `InteractionManager` 讀 `region.GetInt("dramaId")`，不需要 triggerTypes.json。
- **觸發方式依該 dramaId 在 DramaTable 的 `Type` 而定**：Type 1＝靠近按 F（放紫色星星）；Type 2（頭像對話）＝**碰到自動觸發、且不放星星**（純隱形觸發點，見下方「觸發分支與觸發方式」）。編輯器端不分型別，都只填 `dramaId`。
- 觸發後一次性消耗（當次停留不再觸發、離開地圖重建）——與拾取點同模型，永久記錄屬 Phase 2（見 [INTERACTION.md](INTERACTION.md)）。

---

## 資料表 `DramaTable.csv`

位置 `Assets/Data/DramaTable.csv`（與所有資料表同位置）。載入方式比照 `ItemTable`：**把 CSV 拖進場景上 `DramaTableProvider` 元件的 `Drama CSV` 欄**（建議掛在 GameManagers）。`DramaDatabase`（懶漢快取）載入時 `FindObjectOfType<DramaTableProvider>()` 取用、沒拖到退回讀 Resources 並印錯誤指引。

| 欄位 | 說明 |
|---|---|
| `ID` | 劇情唯一編號（編輯器 `dramaId` 指這個） |
| `ImagePath` | **catalog id**：相對 `GameAssets/` 的路徑、不含副檔名（例 `Modules/RedBridalGown/Drama/drama_sample`）。留空 = 只有文字。**僅 Type=1 使用** |
| `Text` | 內文。欄位內含逗號請用 `"..."` 包覆；要換行就寫 `\n`（會轉成真換行）。**僅 Type=1 使用** |
| `Type` | 劇情類型：**1 = 大圖+文字**（DramaPanel，預設）、**2 = 頭像對話**（見下節）。留空 / 無效 = 1（向下相容舊表） |
| `TalkGroup` | **僅 Type=2 使用**：對話群組編號，連到下節「頭像對話」的 `DramaTalkTable.csv` 群組編號。Type=1 時填什麼都不影響 |

> 之後要加標題、多頁、語音等欄位，在表尾加欄即可（解析器忽略未知欄、缺欄給預設）。
> **編輯器不必動**：trigger 仍只填既有的 `dramaId`，type 與對話群組都由 DramaTable 那一列決定（用 dramaId 對照）。

---

## 頭像對話（Type 2）＋ DramaTalkTable.csv

當 DramaTable 某列 `Type=2` 時，觸發後**不開大圖面板**，改去 `DramaTalkTable.csv` 撈該列 `TalkGroup` 指定的群組，把整串對話依序播放（左/右頭像 + 文字）。

### 資料表 `DramaTalkTable.csv`

位置 `Assets/Data/DramaTalkTable.csv`（與其他資料表同位置）。載入方式比照 DramaTable：**把 CSV 拖進場景上 `DramaTalkTableProvider` 元件的 `Talk CSV` 欄**（建議掛在 GameManagers）。`DramaTalkDatabase`（懶漢快取）載入時 `FindObjectOfType<DramaTalkTableProvider>()` 取用、沒拖到退回讀 Resources 並印錯誤指引。

| 欄位 | 說明 |
|---|---|
| `ID` | **流水號**（唯一）。同群組內依此**由小到大**依序播放 |
| `Group` | **群組編號**：同一個群組 = 一串對話。DramaTable Type=2 的 `TalkGroup` 指向這裡 |
| `Name` | **說話人姓名**：對話介面顯示在姓名底版上（資料層只保存字串） |
| `AvatarPath` | 頭像檔案路徑（字串）。**載入方式留待對話介面 UI 決定**，資料層只保存字串 |
| `Side` | 頭像位置：**1 = 置左、2 = 置右**（其餘 / 留空視為置左） |
| `Text` | 對話內容。含逗號用 `"..."` 包覆；`\n` 轉換行 |

範例（群組 1 = 兩句一串：先 #1 再 #2）：

```
ID,Group,Name,AvatarPath,Side,Text
1,1,老者,Modules/RedBridalGown/Talk/avatar_elder,1,你終於來了
2,1,惡鬼,Modules/RedBridalGown/Talk/avatar_demon,2,沒錯，納命來!!
3,2,孩童,Modules/RedBridalGown/Talk/avatar_child,1,這個地方陰森森的，好可怕
4,3,孩童,Modules/RedBridalGown/Talk/avatar_child,1,我想爹娘了...
```

### 觸發分支與觸發方式（runtime）

`InteractionManager` 在建立劇情點時就用 `dramaId` 查 `DramaDatabase` 的 `Type`，決定**觸發方式**與**內容**：

| Type | 觸發方式 | 星星標示 | 內容 |
|---|---|---|---|
| 1（大圖+文字） | **靠近按 F**（顯示「按 F 鍵」提示） | 有（紫星） | `DramaPanel.Show(dramaId)` |
| 2（頭像對話） | **碰到自動觸發**（踏進區域 `dramaTouchRadius`＝0.6 內就播，不需按鍵、不顯示提示） | **無**（純隱形觸發點） | `DramaTalkController.Play(TalkGroup)` → TalkPanel |

- 自動觸發點不參與「按 F」的最近目標選取（`InteractPoint.autoTrigger`）。觸發後一律 `ConsumePoint`（星星移除、當次停留不再觸發；換地圖重建——永久化屬 Phase 2）。
- `dramaTouchRadius` 在 `InteractionManager` Inspector 可調。Type 1（或查不到資料）維持原本的「按 F」。

### 對話介面 `TalkPanel`（✅ 已接上）

`DramaTalkController.Play(group)` 撈出該群組對話（已排序）後開啟 `TalkPanel` 播放：底部一個**對話框**（`DramaPanelBG`）+ **姓名牌匾**（`DramaPanelNameBG`，依 `Side` 跟著立繪「同側」：立繪左→牌匾左、立繪右→牌匾右）+ 對話文字；**點畫面任意處 / 空白鍵 / Enter 換下一句**，最後一句後關閉。模態、暫停遊戲、ESC 可關、**半透明黑遮罩**（`ShowBackdrop=true`，UIManager 共用遮罩鋪在對話框+立繪後方把場景壓暗）。做法同 SettingsPanel（整張背板 + 量測座標，座標常數在 `TalkPanel.cs` 上方、實機可微調）。

- 檔案：`Assets/Scripts/UI/Panels/TalkPanel.cs`。對話框/牌匾素材：`Resources/UI/DramaPanel/DramaPanelBG.png`、`DramaPanelNameBG.png`。
- **頭像（立繪）走地圖素材管線**（與劇情大圖同套，每關專屬、非共用 Resources）：
  - 圖放 `GameAssets/Modules/<module>/Talk/`，`DramaTalkTable.csv` 的 `AvatarPath` 填 **catalog id**（相對 GameAssets、不含副檔名，例 `Modules/RedBridalGown/Talk/redBridalGown`）。
  - 跑 `Project Tools → Sync Map Assets` 收進 catalog ＋ StreamingAssets。`DramaTalkDatabase.ResolveAvatars` 用 `CatalogLoader` + `MapSpriteLoader` 載成 Sprite（同 `DramaDatabase.ResolveImages`；同一張圖多句共用只載一次）。
  - **`Talk` 是新加進同步分類白名單的**（與 `Environment/Tiles/Background/Drama` 並列）。**加新素材分類時三處同步產生器要一起改**（`MapAssetSyncTool.cs`、`MapIO.cs`、`Tools/sync_map_assets.sh`），否則會像 [PROBLEMS.md](PROBLEMS.md) C3 那樣「放了圖卻載不到」。
  - TalkPanel 把立繪當**站姿**擺在對話框**後方**（z-order：對話框蓋在立繪上、立繪下半身沉入框後）、說話人那一側（依 `Side`）。位置/大小常數在 `TalkPanel.cs` 上方：`AvatarHeight`（大小）、`AvatarOverlap`（**越大越往下＝被對話框蓋住越多、露出越少**）、`AvatarSideMargin`（離左/右邊）。沒有頭像（載不到）時自動隱藏、其餘照常顯示。

### 圖放哪（重要）：每關專屬、走地圖素材管線，不放共用 Resources

劇情圖是**單一關卡才用的圖、不是共用資源**，所以放在該關卡的素材包，比照地圖素材（牆/地磚/家具）的方式載入，而不是放共用的 `Resources/`：

- **放這裡**：`Assets/GameAssets/Modules/<module>/Drama/<檔名>.png`（例 `…/Modules/RedBridalGown/Drama/drama_sample.png`）。
- **同步**：跑 `Project Tools → Sync Map Assets`（或 `Tools/sync_map_assets.sh`）會把 `Drama/` 的 PNG 收進 `catalog.json` ＋ 複製進 `StreamingAssets/MapAssets/`（可打包）。`Drama` 是新加進同步分類的（與 `Environment/Tiles/Background` 並列）。
- **runtime 載入**：`DramaDatabase` 不再用 `Resources.Load`，改用地圖素材管線——`CatalogLoader.Load` 取 catalog、`MapSpriteLoader.GetWholeSprite` 依 `ImagePath`（= catalog id）載圖（PPU 256、Point 濾鏡，像素風一致；UI Image 靠 preserveAspect 縮放，PPU 不影響觀感）。
- **為什麼不放 Resources**：`Resources/` 會被 Unity **無條件打包進每個 build**（共用、永遠載入），與「每關專屬、之後可隨關卡動態下載」的設計相反（見 [ARCHITECTURE.md](ARCHITECTURE.md) 的美術資源架構）。GameAssets/Modules 才是「場景專屬包」。
- **三處同步產生器**（加 `Drama` 分類時要一起改，否則只改一處會像 [PROBLEMS.md](PROBLEMS.md) C1 那樣踩雷）：`Assets/Editor/MapAssetSyncTool.cs`、`Tools/sync_map_assets.sh`、`Assets/Scripts/Map/MapIO.cs`(`BuildFromGameAssets` 的編輯器後備)。

> icon（背包道具）仍走 `Resources/UI/Icons`——那是真正的共用資源，不變。只有「每關專屬的劇情圖」走 GameAssets。

---

## 執行單元

| 檔案 | 角色 |
|---|---|
| `Assets/Scripts/Drama/DramaData.cs` | 一段劇情的資料（ID / ImagePath / Text / 載好的 Image sprite） |
| `Assets/Scripts/Drama/DramaDatabase.cs` | 表載入（`LoadFromText`，懶漢 `Instance`）+ `Get(id)`；圖從 Resources 預載 |
| `Assets/Scripts/Drama/DramaTableProvider.cs` | 場景小元件，持有 `DramaTable.csv` 的 TextAsset 參照（同 `ItemTableProvider`） |
| `Assets/Scripts/UI/Panels/DramaPanel.cs` | 劇情檢視介面（Type=1，見下） |
| `Assets/Scripts/Drama/DramaTalkData.cs` | 一句頭像對話（Id / Group / AvatarPath / Side / Text） |
| `Assets/Scripts/Drama/DramaTalkDatabase.cs` | 頭像對話表載入（懶漢 `Instance`）；依群組分組、組內依流水號排序；`GetGroup(group)` |
| `Assets/Scripts/Drama/DramaTalkTableProvider.cs` | 場景小元件，持有 `DramaTalkTable.csv` 的 TextAsset 參照（同 `DramaTableProvider`） |
| `Assets/Scripts/Drama/DramaTalkController.cs` | 頭像對話播放入口 `Play(group)`：開啟 TalkPanel（無 UI 環境才退回 Debug.Log） |
| `Assets/Scripts/UI/Panels/TalkPanel.cs` | 頭像對話面板（對話框 + 姓名牌匾 + 文字 + 換頁；頭像待頭像圖到位） |

---

## 劇情檢視介面 `DramaPanel`

繼承 `UIPanel`（見 [UI_SYSTEM.md](UI_SYSTEM.md)），**模態**：

| 特性 | 值 | 說明 |
|---|---|---|
| `Layer` | `Window` | 主視窗層 |
| `PausesGame` | `true` | 開劇情時暫停遊戲（`Time.timeScale=0`） |
| `BlocksGameplayInput` | `true` | 擋住移動/攻擊 |
| `ShowBackdrop` | `true` | 半透明黑遮罩（UIManager 共用遮罩，鋪在視窗後） |
| `CloseOnEscape` | `true` | ESC 關閉 |

- **版面**：置中大面板（參考解析度 1920×1080 下約 1280×940），垂直排列＝**大圖在上（保持比例、盡量大）＋ 文字在下 ＋ 底部小提示「按 ESC 或點畫面任意處關閉」**。
- **關閉**：ESC（UIManager 堆疊最上層）或**點畫面任意處**（整片透明關閉鈕鋪滿、圖/文 `raycastTarget=false` 讓點擊穿到關閉鈕）。
- **沒圖**（`ImagePath` 留空或圖找不到）→ 只顯示文字（圖元件停用）。
- 開法：`DramaPanel.Show(dramaId)`（靜態）——查 `DramaDatabase`，找不到資料就印 Warning、不開。由 `InteractionManager` 在玩家按 F 時呼叫。

---

## 怎麼用（一條龍）

1. **放劇情圖**：把 PNG 放進 `Assets/GameAssets/Modules/<module>/Drama/`，在 `DramaTable.csv` 的 `ImagePath` 填 catalog id（例 `Modules/RedBridalGown/Drama/<檔名>`）。
2. **編輯器**：放「劇情觸發點」trigger、填 `dramaId`。
3. **同步**：`Project Tools → Sync Map Assets`（把劇情圖收進 catalog/StreamingAssets，順便拉地圖）。
4. **Unity 一次性**：GameManagers 上 Add Component → `DramaTableProvider`，把 `Assets/Data/DramaTable.csv` 拖進 `Drama CSV` 欄。**若有用到 Type=2 頭像對話**：再 Add Component → `DramaTalkTableProvider`，把 `Assets/Data/DramaTalkTable.csv` 拖進 `Talk CSV` 欄。
5. **Play**：走近劇情點看到紫星＋「按 F 鍵」→ 按 F。Type=1 跳大圖+文字面板；Type=2 跳底部對話框（姓名牌匾+文字，點畫面/空白鍵換頁，頭像待頭像圖到位）。

### 頭像對話（Type=2）一條龍
1. **DramaTalkTable.csv**：填好群組（`Group`）、每句的 `ID`（流水號決定順序）、`Name`（說話人姓名）、`AvatarPath`、`Side`（1左/2右）、`Text`。
2. **DramaTable.csv**：某列設 `Type=2`、`TalkGroup` 填要播的群組編號（`ImagePath`/`Text` 可留空）。
3. **編輯器**：drama trigger 的 `dramaId` 指向上面那列（與 Type=1 完全一樣，編輯器不必改）。
4. **Unity**：掛 `DramaTalkTableProvider` 並拖入 CSV（見上）。
5. **Play**：走近按 F → 跳出底部對話框，依流水號逐句播放（點畫面 / 空白鍵換下一句，最後一句後關閉）。

---

## 待辦（之後可加）

- 多頁劇情（連續對話/翻頁）、標題列、角色立繪、打字機逐字顯示、語音/音效。
- 永久記錄（看過不再觸發）——接 Phase 2 與存檔（見 [MAP_SYSTEM.md](MAP_SYSTEM.md)、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)）。
- `event` 型 trigger（開機關、播旗標）——與劇情點類似的「資料驅動事件」，之後界定。

---

*建立於 2026-06-23：劇情觸發點（編輯器 `drama` trigger）＋ `DramaTable.csv` ＋ `DramaPanel`（模態、半透明遮罩、大圖+文字、ESC/點擊關閉）。觸發互動複用 [INTERACTION.md](INTERACTION.md) 的 InteractionManager（靠近按 F、星星標示、一次性消耗）。*
