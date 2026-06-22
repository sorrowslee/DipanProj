# 背包系統 (Inventory)

> 返回 [文件總覽](README.md)｜底層框架見 [UI_SYSTEM.md](UI_SYSTEM.md)

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
| 底部名稱列 | 中心 `(660,1272)`、約 `360×54`（顯示游標所指物品名） |

> 這些常數寫在 `InventoryPanel.cs` 最上方,改版面只改那裡。`UIPanel` 的座標映射 `Place(rt, px, py, w, h)`：錨到 frame 左上角、`anchoredPosition=(px,-py)`。

---

## 資料表 `ItemTable.csv`

位置 `Resources/Data/ItemTable.csv`（走 Resources、零手動接線,由 `ItemDatabase` 載入）。

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

目前 8 筆：5 把武器（飛劍/彎刀/炸彈/蜂巢/蜜蜂,皆 `EquipSlot=Weapon`）+ 3 個道具（銅錢/卷軸/符紙）。

> **CSV 寫法**：欄位內含逗號的長文字請用雙引號包覆,例如 `"傷害 5，直線飛行"`;引號內要放一個雙引號就寫 `""`。需要換行就在文字裡寫 `\n`(會被轉成換行)。`ItemDatabase` 用支援引號的解析器讀取。

---

## 資料層 `InventorySystem`（純資料、跨場景常駐單例）

- 持有 63 道具格（`ItemStack[]`）+ 6 裝備欄（`Dictionary<EquipSlot,int>`）；懶漢單例,第一次存取自動建立 + `DontDestroyOnLoad`（換地圖物品延續）。
- **不含任何 UI**——任何變動觸發 `event Action OnChanged`,UI 訂閱重繪。
- API：`AddItem(id,count)`（先疊堆再放空格,回傳放不下的剩餘）、`RemoveAt`、`MoveGrid(from,to)`、`EquipFromGrid(index)`、`Unequip(slot)`、`GetGrid/GetEquipped/GetData/HasAnyItem`。

---

## 呈現層 `InventoryPanel`（繼承 `UIPanel`）

- 旗標：`Window` 層、`PausesGame=true`、`BlocksGameplayInput=true`、`ShowBackdrop=true`、ESC 可關。
- `OnBuild` 建一次：frame＋背景＋6 裝備欄＋63 道具格（各是透明命中區 `Image` + 子 icon + 子數量 + `InventorySlotWidget`）＋共用高亮＋底部名稱列。
- `OnOpen` 訂閱 `OnChanged` 並 `Redraw()`；`OnClose` 退訂。`Redraw` 從 `InventorySystem` 讀資料設每格 icon/數量。
- **v1 互動**：左鍵點道具格中的可裝備物品 → 裝備（原裝的換回該格）；左鍵點裝備欄 → 卸回第一個空格；移入 → 高亮該格 + 底部顯示名稱。
- **tooltip**：移到物品上跳出浮動說明（掛在 panel root、不受 frame 縮放、跟著游標、近右邊自動翻到左側、不擋 hover）。三段：**名稱（粗體金）**＋ **`TipStats`（正楷）**＋ **`TipLore`（斜體）**;高度由 `VerticalLayoutGroup + ContentSizeFitter` 自動撐開,空欄自動隱藏該段。

### 相關檔案

- `Assets/Scripts/Inventory/ItemData.cs`（ItemData + EquipSlot 列舉）
- `Assets/Scripts/Inventory/ItemDatabase.cs`（CSV + icon 載入）
- `Assets/Scripts/Inventory/InventorySystem.cs`（資料層 + 事件 + 單例）
- `Assets/Scripts/UI/Panels/InventoryPanel.cs`（面板）
- `Assets/Scripts/UI/Panels/InventorySlotWidget.cs`（格子互動元件）
- `Assets/Scripts/UI/InventoryLauncher.cs`（測試：按 B 開關 + 種子物品）
- `Assets/Resources/Data/ItemTable.csv`、`Assets/Resources/UI/InventoryPanel/inventoryPanelBG.png`、`Assets/Resources/UI/Icons/...`

---

## 怎麼測

1. 開 Unity 等編譯(無紅錯)。
2. 把 `InventoryLauncher` 掛到場景任一物件、按 **Play**（首次會自動塞 8 個測試物品到背包）。
3. 按 **B** 開背包：前 8 格有 icon、遊戲暫停、背景變暗;移游標看高亮+底部名稱;點武器類道具 → 裝到左上武器欄;點武器欄 → 卸回背包;ESC 關。

> 若背景或 icon 顯示為白框/紫塊,多半是 PNG 沒被 Unity 匯入成 **Sprite** 類型(2D 專案預設應為 Sprite;必要時在 Inspector 把 Texture Type 設 Sprite (2D and UI))。

---

## 待辦（之後可加）

- **拖放重排 / 跨格搬移**（資料層 `MoveGrid` 已就緒,差呈現層拖放）。
- **裝備 ↔ 戰鬥連動**：裝備武器時設 `WeaponManager` 當前武器（目前 ItemTable 與 WeaponTable 仍各自獨立）。
- tooltip 上半改由**裝備實際屬性組字**(取代目前純讀 `TipStats`);右鍵快速使用/卸下、堆疊分割、稀有度底色、排序/搜尋(底部列改輸入框)。
- 撿道具/掉落物系統接 `AddItem`；存檔串接(與 [MAP_SYSTEM.md](MAP_SYSTEM.md) Phase 2 狀態持久化一起規劃)。

---

*建立於 2026-06-22：背包 v1（資料層 InventorySystem + 呈現層 InventoryPanel,整張背景 + 量測座標疊互動格,點擊裝/卸,hover 名稱）。建在 [UI_SYSTEM.md](UI_SYSTEM.md) 底層上。*
