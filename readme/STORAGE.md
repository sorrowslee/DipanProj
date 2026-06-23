# 倉庫系統 (Storage)

> 返回 [文件總覽](README.md)｜背包見 [INVENTORY.md](INVENTORY.md)｜存檔見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)｜UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)
>
> **狀態：✅ 程式 + 真素材完成（2026-06-23），待實機微調座標。** 5 分頁資料層、存檔、點擊＋拖放、切頁、排序皆完成；UI 已接上拆好的素材（外框 StoragePanelBG ＋ CellBG_normal/pressed ＋ Cell_number_1~5 ＋ RefreshBG_normal/pressed ＋ RefreshIcon）。座標依底圖量測填好（格網 10×10、頁籤 5、右下重整鈕），實機看若有偏移微調 `StoragePanel.cs` 上方常數即可。
>
> ⚠️ 若素材顯示成白框：到 Inspector 把該 PNG 的 **Texture Type 設成 Sprite (2D and UI)**（2D 專案多半已是預設）。

倉庫＝玩家存放物品的另一個容器，與背包並排、可互相搬運。**每個角色有自己的倉庫**（存進該角色的存檔；同時只玩一份，但多角色各自獨立，見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) §5）。

---

## 0. 定案決策

| 項目 | 決定 |
|---|---|
| 分頁 | **5 個分頁（I–V），各自獨立格網**（對應底圖頁籤）。每頁一筆 `StorageDTO`、`storageId` = 頁索引 `"0".."4"` |
| 每頁格數 | `StorageSystem.DefaultCols×DefaultRows`，預設 **10×10**（依底圖量測），可調 |
| 版面 | **倉庫＋背包並排**（左倉庫含 5 頁籤、右背包），方便互搬。放不下再縮放微調 |
| 搬運操作 | **點擊與拖放都做、效果相同**（點擊＝丟到對側；拖放＝放到指定格） |
| 排序鈕 | 底部中央，按下對**當前分頁**排序（預設：合併同物品堆＋依物品 ID 排序＋壓實；規則之後可改 `ItemGridData.Sort()`） |
| 存哪 | 該角色 `CharacterSave.storages[]`（一頁一筆，本地、跨平台，見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)） |

### 底圖量測（`StoragePanelBG.png`，原生 1123×1401）
格網左上 ≈ (161, 336)、單格 ≈ 84×84、**10 欄 × 10 列**；頁籤 I–V 中心 x ≈ 245/413/581/749/917、y ≈ 285；重整鈕在底部中央 ≈ (561, 1255)。這些值放在 `StoragePanel.cs` 上方常數，**待拆圖再校準**。

---

## 1. 架構（沿用背包的資料/呈現分離）

```
資料層  IItemGrid（介面）──┬─ InventorySystem（背包，既有）+ 6 裝備欄
                          └─ ItemGridData ── StorageSystem 持有 5 個（5 分頁）
共用 UI  ISlotView ── ItemSlotWidget（倉庫格）/ InventorySlotWidget（背包格、裝備欄）
         SlotDragController（拖曳+ghost，跨面板互通） + InventoryActions（轉移規則）
面板     StoragePanel（倉庫，單開置中/並排左移） + InventoryPanel（真背包，既有）
         StorageBagCoordinator（K/B 開關 + 並排版面）
存檔     SaveManager ── 載入 RestoreState、存檔 CaptureState（寫進 storages[]）
```

資料層：
- **`IItemGrid`**：容器介面（`Capacity / GetAt / SetAt / AddItem / RemoveAt / MoveWithin / GetData / OnChanged`）。
- **`ItemGridData`**：可重用「一片格網」純資料類（實作 `IItemGrid`），含加/減/換/排序/Capture/Restore。**倉庫每一分頁就是一個**。
- **`StorageSystem`**：常駐單例，持有 **5 個 `ItemGridData` 分頁**，聚合 `OnChanged`，`Page(i)`、`CaptureState/RestoreState(List<StorageDTO>)`。與背包共用 `ItemDatabase`。

共用 UI（背包與倉庫同一套搬運）：
- **`ISlotView`**：所有可拖放格子的抽象（道具格/裝備欄/倉庫格都實作）。
- **`SlotDragController`**：全域拖曳＋ghost；放開時讀 `eventData.pointerDrag` 上的 `ISlotView` → **跨面板互拖天生互通**。
- **`InventoryActions`**：純搬運規則（格↔格 放入/合併/交換；格↔裝備欄 裝備/卸下/交換；點擊快速搬）。
- **`ItemSlotWidget`**（倉庫格）/ **`InventorySlotWidget`**（背包道具格＋裝備欄）：都實作 `ISlotView`、拖放走 `SlotDragController`。

面板與協調：
- **`StoragePanel`**：5 分頁倉庫；單開置中、與背包並排時左移。
- **`InventoryPanel`**（既有真背包，含 6 裝備欄、tooltip、裝備↔武器連動）：點擊在倉庫開啟時改送倉庫，並支援拖放。
- **`StorageBagCoordinator`**（開場自動生成）：K 開倉庫、B 開背包；只開一個置中、兩個都開並排。

> 解耦邊界：資料層不認識檔案（存檔由 `SaveManager` 經 Capture/Restore 處理）、不認識 UI；`InventoryActions` 是純邏輯不碰 UI；面板只透過 `IItemGrid` / `InventorySystem` 操作資料、訂閱 `OnChanged` 重繪。與「彈道不算傷害」同一套紀律。

## 2. 開啟方式與版面

- **K**：開/關倉庫。**B**：開/關背包。由 `StorageBagCoordinator`（開場自動生成）統一處理。
- **只開倉庫** → 倉庫置中。**只開背包** → 背包置中。**兩個都開** → 倉庫左、背包右（並排），方便互搬。
- 倉庫開著時按 B 叫出背包並排；再按 B 收背包、倉庫回中。

## 3. 搬運規則（背包↔倉庫共用一套，含裝備欄）

底層為共用系統：`ISlotView`（所有格子的抽象）＋ `SlotDragController`（拖曳，跨面板互通）＋ `InventoryActions`（轉移規則）。

- **互通前提**：**兩個介面都開著**才能互搬。單開倉庫只能整理/瀏覽（點擊不會把東西丟去背包）；單開背包維持原本裝備/卸下。
- **hover**：倉庫與背包**行為一致**——滑過格子有黃色高亮 + 浮動 tooltip（名稱/功能/劇情）。
- **點擊**：
  - 倉庫格（背包也開著時）→ 整堆送到背包；背包道具格（倉庫也開著時）→ 整堆送到倉庫當前分頁；對側滿了只送塞得下的部分。
  - 背包道具格（倉庫沒開）→ 維持原本「點可裝備物品 = 裝備」；裝備欄 → 卸回背包。
- **拖放**（任意格拖到任意格，跨面板也行）：
  - 同格網內 → 交換。
  - 跨格網（背包↔倉庫）→ 空格放入／同物可疊合併（超量留來源）／不同物交換。
  - 道具格 → 裝備欄：槽位相符才裝備（原本裝著的換回來源格）。
  - 裝備欄 → 道具格：卸到空格；該格是相容裝備則交換穿戴。
  - 可從背包或倉庫直接拖去穿/卸（裝備↔武器連動照常觸發）。

## 4. 存檔

- 倉庫內容存在**該角色**的 `CharacterSave.storages[]`（5 分頁、一頁一筆，`storageId` = 頁索引 `"0".."4"`）。
- `SaveManager` 載入角色時 `StorageSystem.RestoreState(storages)`、存檔時 `storages = StorageSystem.CaptureState()`，並訂閱 `StorageSystem.OnChanged` 標記待存（見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) §6、§13）。
- 跨改版安全：還原時找不到的物品 ID 會跳過、count 夾到上限（同背包）。

## 5. 座標校準（若實機有偏移）

UI 已接真素材；座標依底圖量測填好。若實機看頁籤/按鈕/格網有偏移，調對應常數（皆為**底圖原生像素、左上原點、填中心點**；往上＝調小 Y、往右＝調大 X）：

- 倉庫（`StoragePanel.cs` 上方）：`GridX0/GridY0/CellW/CellH`（格網）、`TabCx/TabCy/TabW/TabH`（頁籤）、`RefreshCx/RefreshCy/RefreshSize`（重整鈕）、`FrameScale/SoloX/PairLeftX`（縮放與擺位）。
- 背包（`InventoryPanel.cs`）：`RefreshCx/RefreshCy/RefreshSize`（重整鈕，沿用倉庫那組 Refresh 素材）、`PairRightX`（與倉庫並排時右移量）。

> 重整鈕的精準對位：用看圖工具開該面板底圖 PNG，把游標移到目標徽章中心讀出像素 (x,y)，直接填進 `RefreshCx/RefreshCy`（程式用同一套座標，所見即所得）。每頁格數改 `StorageSystem.DefaultCols/DefaultRows`。icon 規格沿用背包（256×256 透明 PNG）。

> 排序鈕行為：倉庫鈕排序**當前分頁**（`ItemGridData.Sort`）；背包鈕排序**道具格**（`InventorySystem.SortGrid`，不動裝備欄）。規則皆為「合併同物品＋依物品 ID」。

## 6. 怎麼測

1. 開 Unity 等編譯（無紅錯）。
2. 把 `StorageLauncher`＋`InventoryLauncher` 掛到場景任一物件、按 **Play**（首次各自塞測試物品）。開關鍵由自動生成的 `StorageBagCoordinator` 接管，不必手動掛。
3. 按 **K** 開倉庫（置中）。點 **I–V** 切分頁（第 2 頁有一把武器）。按右下**重整鈕**排序。
4. 倉庫開著時按 **B** → 背包並排出現、倉庫左移；再按 B 收背包、倉庫回中。
5. **點擊**：倉庫格→送背包；背包道具格（倉庫開著）→送倉庫當前分頁；背包道具格（倉庫沒開）→裝備；裝備欄→卸回背包。
6. **拖放**：倉庫↔背包任意格互拖（放入/合併/交換）；道具格拖到裝備欄＝裝備、裝備欄拖到道具格＝卸下。
7. 關遊戲再開 → 各分頁與背包內容都在（存進角色存檔）。

## 7. 待辦（之後可加）

- 排序規則細化（目前：合併同物品＋依 ID；之後可加分類優先、稀有度等，改 `ItemGridData.Sort()`）。
- 「靠近倉庫才能開」的世界互動（取代測試用的 K 鍵）。
- 數量拆分（按住搬一半／指定數量）、右鍵快速搬運。
- 背包底圖目前是既有 `inventoryPanelBG`；若要與倉庫風格更統一可再換圖。
- 分頁命名/分類（例如每頁一個用途）；多倉庫（不同地點各一組分頁）資料已可擴充。

---

*建立於 2026-06-23：倉庫 5 分頁資料層、存檔、真素材 UI、切頁、排序完成；並導入「共用 slot 拖放/搬運系統」(ISlotView + SlotDragController + InventoryActions)，背包(含裝備欄)與倉庫可點擊/拖放互搬；StorageBagCoordinator 管 K/B 與單開置中 / 並排左右。待實機校準座標。*
