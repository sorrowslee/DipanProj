# 背包系統 (Inventory)

> 返回 [文件總覽](README.md)｜底層框架見 [UI_SYSTEM.md](UI_SYSTEM.md)｜倉庫與「背包↔倉庫互搬」見 [STORAGE.md](STORAGE.md)｜存檔見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)
>
> **2026-06-23 更新**：背包已接入「共用 slot 拖放/搬運系統」，可與倉庫**拖放＋點擊互搬**（含裝備）；新增**重整鈕**（整理道具格）；**移除底部名稱列**（tooltip 已顯示名稱）；資料層加 `CaptureState/RestoreState`（存檔）、`SetEquipped`、`SortGrid`，並實作 `IItemGrid`。詳見下文與 [STORAGE.md](STORAGE.md)。
>
> **2026-07-16 更新**：新增**藥水系統**（藥劑分類、背包兩格藥水格綁定種類、按 1/2 喝、喝藥特效）；拖曳可放的專用欄位**黃色高亮**＋丟錯格**自動歸位**＋**右鍵藥水快放**；版面座標**重量**到新背景 `1126×1397`（原本用到舊快取尺寸導致高亮偏位）；底部 HUD 血瓶槽**鏡像顯示**背包藥水（見 [BOTTOM_HUD.md](BOTTOM_HUD.md)）。

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

## 背景與格子座標（量自 `inventoryPanelBG.png`，**1126×1397**）

背景圖：`Resources/UI/InventoryPanel/inventoryPanelBG.png`。座標都在**背景原圖像素空間**（左上為原點、y 向下）；整個 `Frame` 等比縮放塞進畫面（預設顯示高 1040 參考單位），格子座標因此不必隨解析度改。

> ⚠️ **座標一定要對準真正的背景圖尺寸**：2026-07-16 一度用到「舊快取的 1133×1388」量座標，整套被平移＋縮放 → 武器格黃光偏大、藥水格完全沒對準。已對真正的 `1126×1397` 重量（邊緣偵測＋疊圖目視驗證）。**改背景圖務必重量**（`BgW/BgH` 與下表全部）。

| 區域 | 參數（`InventoryPanel.cs` 最上方常數） |
|---|---|
| 道具格 7×9 | 左 `GridLeft=464`、橫距 `GridPitchX=79.4`；頂 `GridTop=261`、縱距 `GridPitchY=85.9`。格 (c,r) 中心 = `(464+(c+0.5)*79.4, 261+(r+0.5)*85.9)`；icon `70px` |
| 裝備欄 6 格 | 左欄 `x=152`、右欄 `x=304`；三列 `y=379 / 632 / 884`；格框 `104×162`、icon `100px`。順序：左上武器·右上胸甲·左中鞋子·右中手套·左下護身符·右下戒指 |
| 藥水格 2 格 | 左欄最下方，中心 `x=241 / 334`、`y=1067`；格框 `72×72`、icon `64px`。左＝鍵1、右＝鍵2（見下方「藥水系統」） |
| 重整鈕（整理道具格） | 中心 `RefreshCx/RefreshCy=443/1210`、`RefreshSize=90`。做成透明按鈕蓋在底部方孔錢幣上（不覆蓋美術、hover 輕微 tint），按下呼叫 `InventorySystem.SortGrid()` |

> 座標映射 `Place(rt, px, py, w, h)`：錨到 frame 左上角、`pivot=(0.5,0.5)`、`anchoredPosition=(px,-py)`、`sizeDelta=(w,h)`。裝備欄/藥水格的黃色高亮（`dropHi`）就是拉伸貼滿各自的格框。

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
| `TargetMapId` | **劇本類道具**：放進傳送門後要去的關卡（`MapsTable` ID）；`0`/空 = 非劇本 |
| `TargetEntrance` | 目的地落點名（空 = 目標圖預設出生點） |
| `HealHp` | **藥劑**：喝下回復的生命（`0` = 不回血） |
| `HealMp` | **藥劑**：喝下回復的魔力（`0` = 不回魔） |

內容（會持續增加）：**武器**（ItemTable ID 與 `WeaponID` 同號對應 `WeaponTable`，`EquipSlot=Weapon`）＋雜物（`101~103`：銅錢/卷軸/符紙）＋**藥水**（`201` 小回血瓶、`202` 小回魔瓶：`Category=Potion`、`HealHp/HealMp=10`、`MaxStack=99`）。分類欄 `Category` 目前用到 `Weapon`、`Currency/Material`、**`Potion`（藥劑，可拖到藥水格、按數字鍵喝）**。武器 icon 在 `UI/Icons/Equipment/`，其餘在 `UI/Icons/Items/`。

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
- **武器欄是武器的唯一來源**（2026-07-27 起）:裝備哪把就用哪把、**卸下就沒有武器**（空手時按攻擊完全沒反應）。原本的 E 鍵循環切換已移除,不再有「當前武器與裝備欄不一致」的情況。
- 邊界:`InventorySystem` 仍是純資料層、不認識戰鬥;由 `PlayerController`（既有戰鬥整合點,本就持有 `WeaponManager`）做連結。

### 相關檔案

- `Assets/Scripts/Inventory/ItemData.cs`（ItemData + EquipSlot 列舉）
- `Assets/Scripts/Inventory/ItemDatabase.cs`（CSV + icon 載入；`LoadFromTextAsset` 主、`LoadFromResources` 後備）
- `Assets/Scripts/Inventory/ItemTableProvider.cs`（場景元件，持有 `Assets/Data/ItemTable.csv` 的 TextAsset 參照）
- `Assets/Scripts/Inventory/InventorySystem.cs`（資料層 + 事件 + 單例）
- `Assets/Scripts/UI/Panels/InventoryPanel.cs`（面板）
- `Assets/Scripts/UI/Panels/InventorySlotWidget.cs`（格子互動元件，已實作 `ISlotView` + 拖放）
- `Assets/Scripts/UI/InventoryLauncher.cs`（**已停用／no-op**：2026-07-22 起新角色初始背包保持**完全空**，不再自動塞測試物品。開關鍵 B 已移到 `StorageBagCoordinator`。此元件留著不做事，可從場景移除；要臨時塞測試裝備就在 `Start()` 自行 `InventorySystem.Instance.AddItem(id)`，或還原 git 舊版種子碼。）
- 共用搬運（與倉庫同套，見 [STORAGE.md](STORAGE.md)）：`UI/ISlotView.cs`、`UI/SlotDragController.cs`、`UI/InventoryActions.cs`、`UI/StorageBagCoordinator.cs`
- `Assets/Data/ItemTable.csv`（與其他資料表同位置）、`Assets/Resources/UI/InventoryPanel/inventoryPanelBG.png`、`Assets/Resources/UI/Icons/...`

---

## 藥水系統（藥劑 / 藥水格 / 高亮 / 自動歸位 / 右鍵快放）

> **2026-07-16 新增。** 底部 HUD 的鏡像顯示與喝藥見 [BOTTOM_HUD.md](BOTTOM_HUD.md)。

**藥劑（Potion）**：`ItemTable.Category=Potion` 的物品（`ItemData.IsPotion`）。喝下依 `HealHp/HealMp` 回血/回魔。目前 `201` 小回血瓶、`202` 小回魔瓶（各回 10、`MaxStack=99`）。

**藥水格（背包內兩格，左＝鍵1、右＝鍵2）**：`Assets/Scripts/UI/PotionSlot.cs`。綁定的是藥劑**種類**（物品 ID）、不是某一瓶：

- 從道具格拖一種藥劑上來 → 記住 ID、顯示 icon ＋背包剩餘數量；那個種類在背包歸零 → 自動清空該格。
- 綁定存在 `InventorySystem`（`GetPotionSlot/SetPotionSlot`，跟背包一起存檔，`InventoryDTO.potionSlots`）。**只記種類、不動背包內容**（不走搬移邏輯）；往格外拖＝解綁清空。
- 使用（喝）由常駐 `PotionHotkeys` 在遊戲中按 1/2 觸發（見 [BOTTOM_HUD.md](BOTTOM_HUD.md)）。

**拖曳時的「可放欄位」黃色高亮（`dropHi`）**：拖起某類物品 → 把「該類能放、且**空著**的專用欄」亮黃光（拖裝備亮對應的空裝備欄、拖藥劑亮空的藥水格），放開時全關。判斷靠 `SlotDragController.DraggingItemId`（全域拖曳中的物品 ID，輪詢式、避開 Domain Reload 殘留），面板 `UpdateDropHighlights` 更新。**拖曳中不做 hover 高亮**（改用這個）。

**丟錯格自動歸位（`InventoryActions`）**：

- 藥劑丟到裝備欄 → 自動放到藥水格（`AutoPlacePotion`）。
- 裝備丟到藥水格 → 自動裝到正確裝備欄（`EquipToCorrectSlot`）。
- 丟到一般道具格 → 就是單純重排/搬移（不特別處理）。

**左鍵/右鍵藥水快放**：在道具格對藥劑按**左鍵**（2026-07-22 起，與裝備左鍵自動裝備一致；路徑在 `InventoryPanel.OnSlotClicked` 對 `IsPotion` 分流）或**右鍵**，都會自動放進藥水格（`InventorySystem.AutoPlacePotion`）：有空位優先放**最小索引**（1 號優先於 2 號）；全滿則取代 0 號；已綁在某格則不動。也可直接**拖曳**到藥水格。可延伸到 N 格（`PotionSlotCount`）。`InventorySlotWidget` 依左/右鍵分流。

**相關檔案**：`UI/PotionSlot.cs`、`UI/PotionHotkeys.cs`、`Inventory/InventorySystem.cs`（`GetPotionSlot/SetPotionSlot/AutoPlacePotion/PotionSlotCount`）、`Inventory/InventoryDTO.cs`（`potionSlots` 存檔）、`UI/InventoryActions.cs`（`EquipToCorrectSlot`）、`UI/Panels/InventorySlotWidget.cs`（右鍵分流＋`dropHi`）、`UI/Panels/InventoryPanel.cs`（`UpdateDropHighlights`）。喝藥特效見 [VFX.md](VFX.md)。

---

## 怎麼測

1. 開 Unity 等編譯(無紅錯)。
2. 開關鍵 B 由 `StorageBagCoordinator`（開場自動生成）接管。**新角色初始背包完全空**（`InventoryLauncher` 已 no-op，不再自動塞測試物品）——要看 icon 就先靠撿道具/掉落/劇本取得，或臨時在 `InventoryLauncher.Start()` 自行 `AddItem`。
3. 按 **B** 開背包：有 icon、遊戲暫停、背景變暗;移游標看高亮+tooltip;倉庫沒開時點武器類道具 → 裝到武器欄、點武器欄 → 卸回背包;ESC 關。
4. 倉庫＋背包互搬見 [STORAGE.md](STORAGE.md)（K 開倉庫、B 並排叫出背包、拖放/點擊互搬）。

> 若背景或 icon 顯示為白框/紫塊,多半是 PNG 沒被 Unity 匯入成 **Sprite** 類型(2D 專案預設應為 Sprite;必要時在 Inspector 把 Texture Type 設 Sprite (2D and UI))。

---

## 待辦（之後可加）

- ✅ **拖放重排 / 跨格搬移**（已做，透過共用 `SlotDragController`）。
- ✅ **存檔串接**（已做，`CaptureState/RestoreState` → 角色存檔，見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)）。
- tooltip 上半改由**裝備實際屬性組字**(取代目前純讀 `TipStats`);右鍵快速使用/卸下、堆疊分割（按住搬一半）、稀有度底色、排序規則細化、搜尋。
- ✅ **撿道具/掉落物系統接 `AddItem`**（已做，見 [INTERACTION.md](INTERACTION.md)：拾取點 + 地上掉落物，靠近按 F）。
- （可選）背包浮動 tooltip 目前各面板各建一份（背包、倉庫各有），日後可抽成共用元件。

---

*建立於 2026-06-22：背包 v1（資料層 InventorySystem + 呈現層 InventoryPanel,整張背景 + 量測座標疊互動格,點擊裝/卸,hover 名稱）。建在 [UI_SYSTEM.md](UI_SYSTEM.md) 底層上。*
*2026-06-23 更新：接入共用 slot 拖放/搬運系統（與倉庫互拖、含裝備）；新增重整鈕（SortGrid）；移除底部名稱列；資料層加 IItemGrid / SetEquipped / SortGrid / Capture·RestoreState。見 [STORAGE.md](STORAGE.md)、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)。*
*2026-07-16 更新：藥水系統（Potion 分類、背包兩格藥水格綁定種類、按 1/2 喝、喝藥特效）；拖曳可放欄位黃色高亮 + 丟錯格自動歸位 + 右鍵藥水快放；版面座標重量到新背景 1126×1397（修正高亮偏位）；底部 HUD 血瓶槽鏡像顯示背包藥水（見 [BOTTOM_HUD.md](BOTTOM_HUD.md)）；ItemTable 加 TargetMapId/TargetEntrance/HealHp/HealMp 欄。*
