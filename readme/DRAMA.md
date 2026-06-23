# 劇情系統 (Drama：觸發點 ＋ 劇情檢視介面)

> 返回 [文件總覽](README.md)｜靠近按 F 的互動共用機制見 [INTERACTION.md](INTERACTION.md)｜UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)｜資料表載入慣例見 [INVENTORY.md](INVENTORY.md)

玩家走到地圖上的「劇情觸發點」，靠近按 **F** 跳出一個劇情檢視介面（一張大圖 + 一段文字）。觸發互動本身（星星標示、靠近按 F、消耗）完全複用 [INTERACTION.md](INTERACTION.md) 的 `InteractionManager`——本檔只談**劇情專屬**的部分：資料表、檢視介面。

---

## 編輯器：劇情觸發點 trigger

預設 trigger 多了一種 `drama`「劇情觸發點」（紫色 `#AA66FF`），參數 **`dramaId`(Int)**。在編輯器畫一塊 `drama` 區域、填 `dramaId`，遊戲端就會在該處放**紫色星星**、靠近顯示「按 F 鍵」、按 F 開對應劇情。

- 定義在 `DipanProj_MapEditor` 的 `triggerTypes.json` ＋ `TriggerType.cs` 的 `Defaults()`（兩處同步）。
- runtime 由 `InteractionManager` 讀 `region.GetInt("dramaId")`，不需要 triggerTypes.json。
- 觸發後一次性消耗（當次停留不再觸發、離開地圖重建）——與拾取點同模型，永久記錄屬 Phase 2（見 [INTERACTION.md](INTERACTION.md)）。

---

## 資料表 `DramaTable.csv`

位置 `Assets/Data/DramaTable.csv`（與所有資料表同位置）。載入方式比照 `ItemTable`：**把 CSV 拖進場景上 `DramaTableProvider` 元件的 `Drama CSV` 欄**（建議掛在 GameManagers）。`DramaDatabase`（懶漢快取）載入時 `FindObjectOfType<DramaTableProvider>()` 取用、沒拖到退回讀 Resources 並印錯誤指引。

| 欄位 | 說明 |
|---|---|
| `ID` | 劇情唯一編號（編輯器 `dramaId` 指這個） |
| `ImagePath` | 圖路徑（相對 `Resources/`、不含副檔名，例 `UI/Drama/drama_sample`）。留空 = 只有文字 |
| `Text` | 內文。欄位內含逗號請用 `"..."` 包覆；要換行就寫 `\n`（會轉成真換行） |

> 之後要加標題、多頁、語音等欄位，在表尾加欄即可（解析器忽略未知欄、缺欄給預設）。
>
> **圖放哪**：`Assets/Resources/UI/Drama/`，`ImagePath` 填 `UI/Drama/<檔名>`（同 icon 慣例，走 Resources）。

---

## 執行單元

| 檔案 | 角色 |
|---|---|
| `Assets/Scripts/Drama/DramaData.cs` | 一段劇情的資料（ID / ImagePath / Text / 載好的 Image sprite） |
| `Assets/Scripts/Drama/DramaDatabase.cs` | 表載入（`LoadFromText`，懶漢 `Instance`）+ `Get(id)`；圖從 Resources 預載 |
| `Assets/Scripts/Drama/DramaTableProvider.cs` | 場景小元件，持有 `DramaTable.csv` 的 TextAsset 參照（同 `ItemTableProvider`） |
| `Assets/Scripts/UI/Panels/DramaPanel.cs` | 劇情檢視介面（見下） |

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

1. **編輯器**：放「劇情觸發點」trigger、填 `dramaId`。存檔 → `Project Tools → Sync Map Assets`。
2. **Unity 一次性**：GameManagers 上 Add Component → `DramaTableProvider`，把 `Assets/Data/DramaTable.csv` 拖進 `Drama CSV` 欄。劇情圖放 `Assets/Resources/UI/Drama/`，在 `DramaTable.csv` 填 `ImagePath`。
3. **Play**：走近劇情點看到紫星＋「按 F 鍵」→ 按 F → 跳出劇情（暫停＋遮罩、大圖＋文字）→ ESC 或點畫面關閉。

---

## 待辦（之後可加）

- 多頁劇情（連續對話/翻頁）、標題列、角色立繪、打字機逐字顯示、語音/音效。
- 永久記錄（看過不再觸發）——接 Phase 2 與存檔（見 [MAP_SYSTEM.md](MAP_SYSTEM.md)、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)）。
- `event` 型 trigger（開機關、播旗標）——與劇情點類似的「資料驅動事件」，之後界定。

---

*建立於 2026-06-23：劇情觸發點（編輯器 `drama` trigger）＋ `DramaTable.csv` ＋ `DramaPanel`（模態、半透明遮罩、大圖+文字、ESC/點擊關閉）。觸發互動複用 [INTERACTION.md](INTERACTION.md) 的 InteractionManager（靠近按 F、星星標示、一次性消耗）。*
