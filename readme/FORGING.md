# 鍛造介面（ForgingPanel）

> 鐵匠鋪的鍛造台：把武器／裝備放上鐵砧，往左右六個孔鑲嵌寶石。
> **目前版本＝版面與拖放已完成，鑲嵌與兩顆按鈕的功能還沒接**（見 §7 缺口）。

---

## 1. 一句話

按 **Y** 開啟 → 面板靠左、**背包自動一起開**靠右 → 從背包把武器／裝備拖到鐵砧中央那一格
（**是借放不是搬移**：東西留在背包原位、那一格鎖住壓黑，左鍵點鐵砧取下解鎖，見 §4）。
左三右三共 6 個鑲嵌孔目前**全部上鎖**（換成有鎖鏈的外框圖），因為裝備還沒有「孔位數」這個屬性。

---

## 2. 檔案一覽

| 檔案 | 角色 |
|---|---|
| `Assets/Scripts/UI/Panels/ForgingPanel.cs` | 面板本體（版面、開關、鐵砧放上/取下、孔位解鎖、tooltip） |
| `Assets/Scripts/UI/ForgeSlotWidget.cs` | 單格元件（鐵砧格與 6 個鑲嵌孔共用）。實作 `ISlotView` |
| `Assets/Scripts/Inventory/ForgeAnvilSlot.cs` | **鐵砧那一格：只記「東西在背包哪一格」的參照，不持有物品**（見 §4） |
| `Assets/Scripts/Inventory/ForgeSocketGrid.cs` | 6 個鑲嵌孔的資料容器（`IItemGrid`，容量 6，含解鎖數） |
| `Assets/Scripts/Inventory/ForgeSockets.cs` | **「一件裝備有幾個孔」的唯一查詢入口**（現在固定回 0） |
| `Assets/Scripts/UI/StorageBagCoordinator.cs` | 加了 `forgeKey = Y` 與並排規則 |
| `Assets/Scripts/UI/Panels/InventoryPanel.cs` | 「鍛造開著時點裝備＝放上鐵砧」＋被借走那格壓黑 |
| `Assets/Scripts/UI/Panels/InventorySlotWidget.cs` | 加了 `locked`：被借走的格子不能拖、不能點、不能收 |

素材：

| 檔案 | 用途 | 尺寸 |
|---|---|---|
| `Resources/UI/ForgingPanel/ForgingPanel_Bg.png` | 整張面板底圖（＝版面座標系） | 1536×1024 |
| `Resources/UI/ForgingPanel/ForgingPanel_ItemFrame.png` | 方框：**沒有鎖鏈**的版本（鐵砧＋已開啟的孔） | 1448×1296 |
| `Resources/UI/ForgingPanel/ForgingPanel_ItemFrameWithChain.png` | 方框：**有鎖鏈**的版本（上鎖的孔） | 1412×1302 |
| `Resources/UI/Common/ForgingPanel_Btn.png` | 底部兩顆長按鈕的底板 | 2416×676 |
| `Resources/UI/Common/CloseBtn_2.png` | 右上關閉鈕（只有一張，沒有按下版 → 走 ColorTint） | 519×481 |

---

## 3. 版面座標（量自示意圖）

整個面板是「一張底圖 ＋ 在上面疊互動格子」，座標一律在**底圖原生像素空間 1536×1024**（左上為原點、y 向下），
整個 frame 用 `localScale` 等比縮放塞進畫面——與 `InventoryPanel` 同一套作法。要調位置就改 `ForgingPanel` 上方那組常數。

```
六個鑲嵌孔   方框本體 153×156
             左欄 cx=307   右欄 cx=1224
             三排 cy = 280 / 491 / 701
鐵砧中央格   cx=768  cy=502   232×240
底部兩顆鈕   cy=893  寬 386   左 cx=465 / 右 cx=1068
關閉鈕       cx=1355 cy=103   寬 88
標題「鍛造」 cx=766  cy=100   字級 68（毛筆字 Bakudai-Bold）
```

顯示大小由 `displayHeight`（預設 **636**）決定：1536×1024 的圖 → 954×636，靠左擺在 `PairLeftX = -447`，
右邊留給並排的背包（背包 `PairRightX = 400`、正方形 900×900）。
> ⚠ 這個 X 是用**看得見的美術**算的，不是整張圖的寬度——鍛造底圖 1536 裡真正不透明的內容只有 x 127~1408，左右各約 127px 透明留白。2026-08-07 重算過（原本是 `-483`，是拿整張圖寬度排的，中間會空一大塊）。詳見 [INVENTORY.md](INVENTORY.md) 的「並排位置」。

### 透明邊補償（ArtSpec）

方框／鎖鏈／按鈕／關閉鈕都是「整張畫布輸出、內容只佔中間一塊」的 AI 素材，
直接塞進方框會又小又偏。所以 `ForgingPanel` 沿用 **`GachaPanel` 那套 `ArtSpec`**：
記下每張圖「內容的邊界框」，由 `PlaceArt` 反推方框該多大、要偏移多少，讓**內容**剛好落在指定位置。

> ⚠️ **重新輸出任何一張圖之後**，`LoadArt` 會比對**畫布比例**並印警告（不會靜默跑位）。
> 看到警告就用影像工具重量「不透明像素的邊界框（左, 上, 寬, 高）」並更新 `ForgingPanel` 裡的 `ArtSpec`。
>
> 比的是**比例**不是像素數，因為匯入設定的 `Max Size`（本專案預設 2048）會把大圖等比縮小
> （例：按鈕底板 2416×676 進到遊戲是 2048×573），而 `PlaceArt` 全走比值、等比縮放不影響結果。
> 詳見 [PROBLEMS.md](PROBLEMS.md) D12。

目前記錄值：

```
ForgingPanel_Bg                  1536×1024   內容 (0,0,1536,1024)     ← 整張都是內容
ForgingPanel_ItemFrame           1448×1296   內容 (145,60,1153,1176)  ← 只記「方形本體」，兩側尖角自然凸出去
ForgingPanel_ItemFrameWithChain  1412×1302   內容 (109,65,1171,1185)  ← 同上，本體對齊靠這組數字
ForgingPanel_Btn                 2416×676    內容 (7,66,2388,559)
CloseBtn_2                       519×481     內容 (17,14,475,459)
```

### 上鎖＝換一張外框圖，不是疊鎖鏈

**鎖鏈不是獨立圖層**——外框圖本身就有「無鎖鏈／有鎖鏈」兩個版本，切換上鎖只是把兩張圖互相 enable/disable
（`ForgeSlotWidget.SetFrameArt` 掛圖、`Locked` 的 setter 切換）。
早期版本是把一張 `ForgingPanel_Chain.png` 疊在格子上，但那張鎖鏈圖的比例（1.44）跟接近正方的格子對不上，
不管怎麼縮放都不好看，所以改成現在這種「兩張成品框」的做法。

> 🧹 舊的 `ForgingPanel_Chain.png` 已經隨這次改版從 Resources 移除（Resources 底下的檔案
> **不論有沒有用到都會全數打包進 build**，留著只是白白撐大體積）。

兩張框的畫布尺寸與本體位置都不同（1360×1210 vs 1412×1302），而且本體比例差約 3%
（1045:1084 vs 1171:1185）。所以：

- 各自用自己的 `ArtSpec` 擺；
- `PlaceArt` 對這兩張**同時指定寬與高**（`CellW`×`CellH`）而不是只給寬度、讓高度依比例算——
  否則切換上鎖時方框會上下跳個幾像素。

對齊基準是**方形本體的外緣**——那才是玩家看到的「格子」，兩張框的尖角大小也已經做成一致。

---

## 4. 鐵砧是「借放」不是「搬移」（重要）

倉庫是**真的把物品搬過去**，鐵砧不是——**物品自始至終留在背包原位不動**：

```
玩家把背包第 12 格的劍拖上鐵砧
   → ForgeAnvilSlot 只記下「來源＝背包道具格 12、物品 ID＝xxx」
   → 背包第 12 格的劍**還在那裡**，但被鎖起來：壓黑、不能拖、不能點、也不能把別的東西放進去
   → 左鍵點鐵砧＝取下，那一格立刻解鎖
```

這樣設計的理由：鍛造只是「拿起來加工」，不該打亂玩家精心排好的背包；
而且面板隨時關掉都不會有東西跑位或落在奇怪的地方（關面板＝自動取下，沒有東西要退回）。

實作上的幾個關鍵點：

- **`ForgeAnvilSlot` 仍實作 `IItemGrid`**，是為了讓 UI 端沿用同一套「讀格子畫 icon」的程式；
  但 `SetAt` / `AddItem` **刻意都不做事**——放上鐵砧不是一次搬運，真正的入口是
  `PlaceFromGrid(index)` / `PlaceFromEquip(slot)`。
- **鐵砧不能當拖曳來源**（`ForgeSlotWidget.CanDragOut = false`）。它沒有真的持有物品，
  讓共用搬運把它當「搬出去」會在背包憑空多複製一份。取下改成**左鍵點鐵砧**。
- **收下時走 `CustomDrop` 而不是 `SlotDragController.Drop`**。共用搬運的 `Resolve` 一律是
  「先塞目標、再清來源」，走它就會把背包那一格清空——正是我們不要的。
- **背包怎麼知道要壓黑**：放上鐵砧完全沒動到背包資料，所以不會觸發 `InventorySystem.OnChanged`。
  改用 `ForgingPanel.LockVersion`（每次鐵砧變動 +1），`InventoryPanel.Update` 輪詢它來重畫，
  作法與既有的 `SlotDragController.DraggingItemId` 輪詢一致。
- ⚠ **鎖必須擋在 `SlotDragController`，不能只擋在格子元件的 `OnBeginDrag`**。
  Unity 的 EventSystem 在滑鼠按下時就填好 `eventData.pointerDrag` 了，而 `Drop` 是從它讀來源的——
  只在 `OnBeginDrag` 裡 `return` 的話會變成「沒有懸浮圖示、但放到別格照樣把東西搬走」。
  所以 `SlotDragController` 有 `IsSlotLocked` 鉤子（`ForgingPanel` 開啟時掛上、關閉時拆掉），
  `Begin` 與 `Drop` 各自檢查來源與目標，另外 `Drop` 一開頭就 `if (_src == null) return`。
  詳見 [PROBLEMS.md](PROBLEMS.md) D11。
  格子元件上的 `InventorySlotWidget.locked` 旗標**只負責壓黑的外觀**（重畫才更新、會慢一拍），
  行為上的把關一律走即時查詢的 `SlotDragController.Locked(this)`。
- **來源被抽掉會自動取下**：`ForgeAnvilSlot.Validate()` 掛在 `InventorySystem.OnChanged` 上，
  比對「來源那一格現在還是不是同一個物品 ID」，不是就自動取下（東西不會遺失，鐵砧只是不再指著它）。
  所以在鐵砧上放著東西時按背包的**重整**鈕，鐵砧會自動清空——這是刻意的，排序後索引就不對了。
- **裝備欄也能拖**：把身上穿的武器拖上鐵砧一樣是借放（不會卸下），那個裝備欄會壓黑。
- **從倉庫拖過來不支援**：鎖定/解鎖是對「背包那一格」做的，倉庫格不在範圍內，會跳提示請先放進背包。

### 鑲嵌孔仍然是「真的收東西」

六個孔走的是 `ForgeSocketGrid`（一般的 `IItemGrid` 容器、走共用搬運），與鐵砧不同。
目前遊戲裡還沒有寶石、孔也全鎖著，所以完全碰不到；等真的做寶石時要回頭決定
「鑲嵌是把寶石消耗掉、還是也做成借放式預覽」，屆時可能要比照鐵砧改寫。

---

## 4.5 拖放的地基

除了上面鐵砧那條特例，其餘完全重用既有的那一套，**沒有為鍛造另寫一份搬運邏輯**：

```
ForgeSlotWidget (實作 ISlotView)
      │ OnBeginDrag / OnDrop
      ▼
SlotDragController  ──►  InventoryActions.Resolve  ──►  IItemGrid 的 API
（全域拖曳＋懸浮 ghost）      （純搬運規則）              （InventorySystem / ForgeSocketGrid）
```

因為 ghost 掛在共用的 root canvas、放開時讀 `eventData.pointerDrag` 上的 `ISlotView`，
**背包 ↔ 鍛造台的拖放天生互通**，跟背包 ↔ 倉庫、背包 ↔ 傳送門是同一件事。

**「只收武器／裝備」的把關放在 UI 端**（`ForgeSlotWidget.OnDrop` 先驗證再放行），
不是放在容器的 `SetAt`——因為跨容器拖放走的是 `SetAt`（先塞目標再清來源），
若在 `SetAt` 拒收會讓來源被清空造成**物品消失**。這一點與 `ScriptSlotGrid` 的註解同源，別改。

其它行為：

- 拖起某件東西時，**收得下的空格會亮黃光**（`SetDropHighlight`，與背包裝備欄同款）。
- 拖了不該放的東西上鐵砧 → 跳 toast「只能放入武器或裝備」，來源留在原位不會消失。
- 滑鼠移到有東西的格子上 → 顯示浮動 tooltip（名稱／TipStats／TipLore，與背包同款）。
  **被鐵砧借走而壓黑的背包格仍然看得到 tooltip**（只擋點擊與拖曳），玩家才知道那是什麼。
  > 2026-08-07 背包分成裝備包＋消耗品包之後，鐵砧的鎖定**完全不用改**——鎖定用的是「跨兩包的扁平格子編號」，切段的設計就是為了讓這種地方一行都不動。但「移除鑲嵌」前算空格要改成 `FreeSlotCount(BagKind.Item)`：珠子不可裝備，依分包規則會回到**消耗品包**，算兩包相加會誤判成放得下。
- 鍛造開著時，**點一下背包裡的武器／裝備就會放上鐵砧**（省得一定要用拖的）。

---

## 5. 開關與並排

- **Y**：`StorageBagCoordinator` 開/關（`forgeKey`）。兩種情況會被鎖住：新手教學強制階段（`TutorialManager.HardLock`）、血統變身演出中（`BloodlineTransformFxRunner.IsPlaying`——本面板 `PausesGame=true`，開下去會把演出凍住，見 [PROBLEMS.md](PROBLEMS.md) D14）。細節見 [STORAGE.md](STORAGE.md)。
- `ForgingPanel.OnOpen` **強制把背包一起開**（沒有背包就沒東西可拖），並排由 `StorageBagCoordinator` 統一擺：
  鍛造靠左、背包靠右。
- `OnClose` 會把鐵砧**取下**（只是解鎖，沒有東西要退回），孔上的東西則退回背包，再把背包關掉。
- 玩家若單獨把背包關掉（按 B 或 ESC），`ForgingPanel.Update` 偵測到就一起收掉——這兩個面板是一組的。

> ESC 的行為：背包是後開的，所以在視窗堆疊的最上層 → 第一下 ESC 關背包，鍛造下一幀跟著關。
> 這與傳送門 `ScriptsPanel` 的行為一致。

---

## 6. 鑲嵌孔的解鎖（**已做好，等資料接上**）

鏈路長這樣：

```
鐵砧上那件裝備
   │  ForgeSockets.Of(ItemData)  ← ★ 唯一的 seam，現在固定回 0
   ▼
ForgeSocketGrid.UnlockedCount = N
   │
   ▼
ForgingPanel.RefreshSockets()
   → index < N 的孔：拿掉鎖鏈、可以放東西
   → index ≥ N 的孔：蓋鎖鏈、不收拖放也不能拖出
```

**孔位變少時**（換成孔比較少的裝備、或把裝備從鐵砧拿走）
`ReturnClosingSockets` 會把被關掉那幾孔的東西退回背包，不會憑空消失。

### 將來要接上時只要動兩個地方

1. **孔位數**：`ItemTable.csv` 加一欄 `SocketCount`（0~6）→ `ItemData` 加欄位、`ItemDatabase` 解析 →
   把 `ForgeSockets.Of()` 換成 `return Mathf.Clamp(d.SocketCount, 0, MaxSockets);`
2. **什麼算寶石**：`ForgingPanel.IsGem(ItemData)` 現在一律回 `false`（所以孔就算開了也放不進東西），
   做出寶石道具後換成 `d.Category == "Gem"` 之類即可。

介面端完全不必改。

> **想先看解鎖的樣子**：在 Console 或作弊面板設 `Dipan.Inventory.ForgeSockets.DebugCount = 4;`
> 就會強制開 4 個孔（-1 = 關閉，走正常查詢）。

---

## 7. 目前的缺口（都記在 TODO.md）

- **「移除鑲嵌」「拆除裝備」兩顆按鈕沒有功能**，按下去只跳「這個功能還沒做好」。
- **裝備沒有孔位數屬性** → 六個孔永遠全鎖（見 §6）。
- **沒有寶石這種道具** → 就算孔開了也放不進東西（見 §6）。
- **鍛造結果不進存檔**：鐵砧只是參照、孔位是純記憶體，關面板就散掉。等鑲嵌真的會改變裝備屬性時，
  才需要決定「鑲好的寶石存在哪」（大概是 `ItemStack` 上加附加資料，屆時 `InventoryDTO` 要一起改）。
- **鑲嵌孔還是「真的收東西」的容器**，與鐵砧的借放式作法不一致（見 §4 結尾）。等做出寶石時一併決定。
- **開啟方式是熱鍵 Y**，之後要改成鐵匠 NPC 的互動點（走 `openPanel` 觸發，`panelId=forge`；
  參考 GACHA_SYSTEM 的 `openPanel` 作法，`InteractionManager.BuildKindRegistry()` 已是可註冊的表）。

---

## 8. 語言表

介面上的字全部走 `Language.GetText(id)`，字串在 `Assets/Data/LanguageTable.csv` 的 **4001–4099「鍛造介面」** 段：

| id | 中文 |
|---|---|
| 4001 | 鍛造 |
| 4002 | 移除鑲嵌 |
| 4003 | 拆除裝備 |
| 4004 | 這個功能還沒做好 |
| 4005 | 只能放入武器或裝備 |
| 4006 | 鍛造台只認得背包裡的東西，請先放進背包 |

> ⚠️ 字串是在 `OnBuild()`（面板第一次建立時）取的，所以中途切語言不會即時更新標題／按鈕文字。
> 全遊戲的面板目前都是這個行為，等真的要做語言切換時再統一處理。

*2026-08-07 更新：背包改成裝備包＋消耗品包雙包分頁（見 [INVENTORY.md](INVENTORY.md)）。鍛造這邊只有兩處受影響——「移除鑲嵌」算空格改用 `FreeSlotCount(BagKind.Item)`（珠子回的是消耗品包），以及並排位置 `PairLeftX` 由 `-483` 改成 `-447`（改用看得見的美術重算）。鐵砧的借放/鎖格用的是扁平格子編號，一行未動。*
