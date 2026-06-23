# 背包系統 (Inventory)

> 返回 [文件總覽](README.md)｜底層框架見 [UI_SYSTEM.md](UI_SYSTEM.md)｜倉庫與「背包↔倉庫互搬」見 [STORAGE.md](STORAGE.md)｜存檔見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)
>
> **2026-06-23 更新**：背包已接入「共用 slot 拖放/搬運系統」，可與倉庫**拖放＋點擊互搬**（含裝備）；新增**重整鈕**（整理道具格）；**移除底部名稱列**（tooltip 已顯示名稱）；資料層加 `CaptureState/RestoreState`（存檔）、`SetEquipped`、`SortGrid`，並實作 `IItemGrid`。詳見下文與 [STORAGE.md](STORAGE.md)。

背包＝建在 UI 底層框架上的第一個面板。嚴守**資料層 / 呈現層分離**：`InventorySystem`（純資料、有什麼/加減/裝卸、發事件）與 `InventoryPanel`（只訂閱事件繪圖、操作回呼資料層）。背景用整張示意圖當底、不拆圖,只在上面疊互動格子放 icon。

---

## icon 規格（美術）

- **統一正方形 `256×256` 透明 PNG**（單體置中、去背）。裝備與道具共用同一規格、同一張圖——同一件裝備在道具格是道具、裝到左欄是裝備。
- 放置位置（相對 `Resources/`）：
  - `UI/Icons/Equipment/`：可裝備（武器/胸甲/鞋子/手套/護身符/戒指）
  - `UI/Icons/Items/`：非裝備（消耗品/材料/任務物…）
- 命名用英文/數字,對應 `ItemTable.csv` 的 `IconPath`（不含副檔名）。例：`UI/Icons/Equipment/weapon_sword`。
- 顯示大小由程式縮放(你不用管)：**道具格 70px、裝備欄 120px**(置中,square)。

---

## 背景與格子座標（量自 `inventoryPanelBG.png`，1122×1402）

背景圖：`Resources/UI/InventoryPanel/inventoryPanelBG.png`。座標都在**背景原圖像素空間**（左上為原點、y 向下）；整個 `Frame` 等比縮放塞進畫面（預設顯示高 1040 參考單位），格子座標因此不必隨解析度改。

| 區域 | 參數 |
|---|---|
| 道具格 7×9 | 左 `x=466`、橫距 `80.857`；頂 `y=280`、縱距 `87.111`。格 (c,r) 中心 = `(466+(c+0.5)*80.857, 280+(r+0.5)*87.111)` |
| 裝備欄 6 格 | 左欄 `x=152`、右欄 `x=308.5`；三列 `y=410 / 705 / 1000`。順序：左上武器·右上胸甲·左中鞋子·右中手套·左下護身符·右下戒指 |
| 重整鈕（整理道具格） | 中心 `RefreshCx/RefreshCy`（預設 `970,1268`）、`RefreshSize`（預設 120）。沿用倉庫那組 Refresh 素材（`UI/StoragePanel/RefreshBG_*`/`RefreshIcon`），按下呼叫 `InventorySystem.SortGrid()` |
| ~~底部名稱列~~ | **已移除**（tooltip 已顯示名稱）。`NameBarX/Y/W/H` 常數仍在但未使用 |

> 這些常數寫在 `InventoryPanel.cs` 最上方,改版面只改那裡。`UIPanel` 的座標映射 `Place(rt, px, py, w, h)`：錨到 frame 左上角、`anchoredPosition=(px,-py)`。

---

## 資料表 `ItemTable.csv`

位置 `Assets/Data/ItemTable.csv`（與 WeaponTable / RecipeTable / MapsTable 等所有資料表同位置）。載入方式比照其他表：**把 CSV 拖進場景上 `ItemTableProvider` 元件的 `Item CSV` 欄**（建議掛在 GameManagers）。`InventorySystem` 載入時 `FindObjectOfType<ItemTableProvider>()` 取用、交給 `ItemDatabase` 解析。

> 為什麼需要 `ItemTableProvider`：`InventorySystem` 是自動生成的常駐單例、場景上沒有可拖檔的物件，所以由這個被動 provider 持有 CSV 參照（其他表的 Manager 本身就在場景，直接拖在 Manager 上）。沒掛 / 沒拖時會退回讀 `Resources`（舊位置）並印錯誤指引。**icon 仍走 `Resources/UI/Icons`，與表的位置無關。**

| 欄位 | 說明 |
|---|---|
| `ID` | 物品唯一編號 |
| `Name` | 名稱 |
| `Category` | 分類字串（Weapon/Currency/Material/Consumable…，分類/顯示用） |
| `EquipSlot` | 可裝備到哪格：`Weapon/Chest/Boots/Gloves/Amulet/Ring`；`None` = 不可裝備 |
| `IconPath` | icon 路徑（相對 Resources、不含副檔名） |
| `MaxStack` | 可疊上限（1 = 不可疊） |
| `Description` | 簡短說明（目前未顯示,保留欄位） |
| `TipStats` | **tooltip 上半（正楷）**：功能/屬性。之後可能改由裝備屬性組字,現階段純讀此欄 |
| `TipLore` | **tooltip 下半（斜體）**：劇情描述 |
| `WeaponID` | **對應 `WeaponTable` 的武器 ID**。裝備此武器到武器欄 → 玩家切到該武器能力（見下方「裝備→使用武器」）。非武器留空 |

目前 15 筆：**12 把武器**（ItemTable ID `1~12`，`WeaponID` 同號對應 `WeaponTable` 1~12，`EquipSlot=Weapon`）+ 3 個雜物（ID `101~103`：銅錢/卷軸/符紙）。武器 icon 在 `UI/Icons/Equipment/`，雜物在 `UI/Icons/Items/`。

> **CSV 寫法**：欄位內含逗號的長文字請用雙引號包覆,例如 `"傷害 5，直線飛行"`;引號內要放一個雙引號就寫 `""`。需要換行就在文字裡寫 `\n`(會被轉成換行)。`ItemDatabase` 用支援引號的解析器讀取。

---

## 資料層 `InventorySystem`（純資料、跨場景常駐單例）

- 持有 63 道具格（`ItemStack[]`）+ 6 裝備欄（`Dictionary<EquipSlot,int>`）；懶漢單例,第一次存取自動建立 + `DontDestroyOnLoad`（換地圖物品延續）。
- **不含任何 UI**——任何變動觸發 `event Action OnChanged`,UI 訂閱重繪。
- API：`AddItem(id,count)`（先疊堆再放空格,回傳放不下的剩餘）、`RemoveAt`、`MoveGrid(from,to)`、`EquipFromGrid(index)`、`Unequip(slot)`、`GetGrid/GetEquipped/GetData/HasAnyItem`。
- **（2026-06-23 新增）**：
  - 實作 **`IItemGrid`**（`Capacity/GetAt/SetAt/MoveWithin/DisplayName`），讓背包道具格能與倉庫共用搬運/拖放程式（見 [STORAGE.md](STORAGE.md)）。
  - `SetEquipped(slot,itemId)`：直接設定某裝備欄（拖放裝備/跨容器用，會觸發 OnChanged → 裝備↔武器連動）。
  - `SortGrid()`：整理道具格（合併同物品＋依 ID 排序＋壓實，不動裝備欄；重整鈕用）。
  - `CaptureState()/RestoreState(InventoryDTO)`：存檔快照（純資料、不碰檔案；由 `SaveManager` 呼叫，見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)）。

---

## 呈現層 `InventoryPanel`（繼承 `UIPanel`）

- 旗標：`Window` 層、`PausesGame=true`、`BlocksGameplayInput=true`、`ShowBackdrop=true`、ESC 可關。
- `OnBuild` 建一次：frame＋背景＋6 裝備欄＋63 道具格（各是透明命中區 `Image` + 子 icon + 子數量 + `InventorySlotWidget`）＋共用高亮＋**重整鈕**。
- `OnOpen` 訂閱 `OnChanged` 並 `Redraw()`；`OnClose` 退訂。`Redraw` 從 `InventorySystem` 讀資料設每格 icon/數量。
- **互動**：
  - 移入 → 高亮該格 + 浮動 tooltip（名稱/功能/劇情）。
  - **點擊**：倉庫沒開時，點道具格的可裝備物品 → 裝備（原裝的換回該格）、點裝備欄 → 卸回第一個空格；**倉庫開著時**，點道具格 → 整堆送到倉庫當前分頁（見 [STORAGE.md](STORAGE.md)）。
  - **拖放**（透過共用 `SlotDragController`）：格內重排/合併/交換、拖到裝備欄＝裝備、拖去倉庫＝存放（含裝備）。
  - **重整鈕**：整理道具格。
  - **與倉庫並排**：倉庫＋背包同開時各自左右移（`StorageBagCoordinator` 控；背包右移位置 `PairRightX`）。
- **tooltip**：移到物品上跳出浮動說明（掛在 panel root、不受 frame 縮放、跟著游標、近右邊自動翻到左側、不擋 hover）。三段：**名稱（粗體金）**＋ **`TipStats`（正楷）**＋ **`TipLore`（斜體）**;高度由 `VerticalLayoutGroup + ContentSizeFitter` 自動撐開,空欄自動隱藏該段。

### 裝備 → 使用武器（與戰鬥連動）

裝備武器欄的武器後,玩家就改用那把武器的能力:

- 連結靠 `ItemTable.WeaponID` → `WeaponTable.ID`。
- 橋接在 `PlayerController`:`Start` 訂閱 `InventorySystem.OnChanged`,武器欄內容變動時取該物品的 `WeaponID`,呼叫 `WeaponManager.SwitchWeapon(weaponID)`。卸下武器（欄位清空）時保留當前武器、不切換。`OnDestroy` 退訂。
- **E 鍵循環切換保留不動**,與裝備並存:不按 E 時「裝備哪把就用哪把」;按 E 仍可在所有武器間循環（此時當前武器可能與裝備欄不同,屬暫時測試行為）。
- 邊界:`InventorySystem` 仍是純資料層、不認識戰鬥;由 `PlayerController`（既有戰鬥整合點,本就持有 `WeaponManager`）做連結。

### 相關檔案

- `Assets/Scripts/Inventory/ItemData.cs`（ItemData + EquipSlot 列舉）
- `Assets/Scripts/Inventory/ItemDatabase.cs`（CSV + icon 載入；`LoadFromTextAsset` 主、`LoadFromResources` 後備）
- `Assets/Scripts/Inventory/ItemTableProvider.cs`（場景元件，持有 `Assets/Data/ItemTable.csv` 的 TextAsset 參照）
- `Assets/Scripts/Inventory/InventorySystem.cs`（資料層 + 事件 + 單例）
- `Assets/Scripts/UI/Panels/InventoryPanel.cs`（面板）
- `Assets/Scripts/UI/Panels/InventorySlotWidget.cs`（格子互動元件，已實作 `ISlotView` + 拖放）
- `Assets/Scripts/UI/InventoryLauncher.cs`（測試：只負責種子物品；開關鍵 B 已移到 `StorageBagCoordinator`）
- 共用搬運（與倉庫同套，見 [STORAGE.md](STORAGE.md)）：`UI/ISlotView.cs`、`UI/SlotDragController.cs`、`UI/InventoryActions.cs`、`UI/StorageBagCoordinator.cs`
- `Assets/Data/ItemTable.csv`（與其他資料表同位置）、`Assets/Resources/UI/InventoryPanel/inventoryPanelBG.png`、`Assets/Resources/UI/Icons/...`

---

## 怎麼測

1. 開 Unity 等編譯(無紅錯)。
2. 把 `InventoryLauncher` 掛到場景任一物件、按 **Play**（首次自動塞測試物品）。開關鍵 B 由 `StorageBagCoordinator`（開場自動生成）接管。
3. 按 **B** 開背包：有 icon、遊戲暫停、背景變暗;移游標看高亮+tooltip;倉庫沒開時點武器類道具 → 裝到武器欄、點武器欄 → 卸回背包;ESC 關。
4. 倉庫＋背包互搬見 [STORAGE.md](STORAGE.md)（K 開倉庫、B 並排叫出背包、拖放/點擊互搬）。

> 若背景或 icon 顯示為白框/紫塊,多半是 PNG 沒被 Unity 匯入成 **Sprite** 類型(2D 專案預設應為 Sprite;必要時在 Inspector 把 Texture Type 設 Sprite (2D and UI))。

---

## 待辦（之後可加）

- ✅ **拖放重排 / 跨格搬移**（已做，透過共用 `SlotDragController`）。
- ✅ **存檔串接**（已做，`CaptureState/RestoreState` → 角色存檔，見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)）。
- tooltip 上半改由**裝備實際屬性組字**(取代目前純讀 `TipStats`);右鍵快速使用/卸下、堆疊分割（按住搬一半）、稀有度底色、排序規則細化、搜尋。
- 撿道具/掉落物系統接 `AddItem`。
- （可選）背包浮動 tooltip 目前各面板各建一份（背包、倉庫各有），日後可抽成共用元件。

---

*建立於 2026-06-22：背包 v1（資料層 InventorySystem + 呈現層 InventoryPanel,整張背景 + 量測座標疊互動格,點擊裝/卸,hover 名稱）。建在 [UI_SYSTEM.md](UI_SYSTEM.md) 底層上。*
*2026-06-23 更新：接入共用 slot 拖放/搬運系統（與倉庫互拖、含裝備）；新增重整鈕（SortGrid）；移除底部名稱列；資料層加 IItemGrid / SetEquipped / SortGrid / Capture·RestoreState。見 [STORAGE.md](STORAGE.md)、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)。*
