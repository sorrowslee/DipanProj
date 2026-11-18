# 兌換系統（兌換所）

> 返回 [文件總覽](README.md)
>
> 2026-11-15 建（分支 `feat/exchange`，⏳ **未編譯、未實測**）。靈感：輪迴流小說《無限恐怖》主神空間的兌換系統，
> 作者要求「玩家能很方便地找到想要的兌換物」，可以當成電商產品頁來做。
> 本檔是兌換所的**現狀說明＋規格**；做過什麼見 [PROGRESS.md](PROGRESS.md) 2026-11-15 條，缺口見 [TODO.md](TODO.md)〈兌換系統〉。

---

## 1. 入口

| 入口 | 怎麼開 |
|---|---|
| **測試熱鍵 N** | `StorageBagCoordinator.exchangeKey`（暫用，跟鍛造 Y 一樣受教學鎖／選單鎖／打字中限制） |
| **地圖編輯器 `openPanel`／NPC 對話後開介面** | `panelId` 填 **`exchange`**；`arg` 可留空，或填分類英文名（`Bloodline`／`Weapon`／`Armor`／`Amulet`／`Ring`／`Gem`／`Consumable`／`Material`）＝開啟時先切到那一類 |
| 程式 | `Dipan.UI.ExchangePanel.OpenWith(arg)` |

分派寫在 `InteractionManager.OpenPanelById`（跟祭壇 `gacha` 同一個 switch）。

---

## 2. 稀有度定義（全遊戲通用）

2026-11-15 作者定：**白 → 藍 → 黃 → 紫** 四階。之前專案沒有稀有度，這是第一份定義。

| 值 | 色名 | 品名 | 顏色（程式） | CSV 可填 |
|---|---|---|---|---|
| 1 | 白 | 一般 | 淡骨色 `#DBD4C4` | `1`／`白`／`一般`／`凡品`／`white` |
| 2 | 藍 | 良品 | `#669EEB` | `2`／`藍`／`良品`／`blue` |
| 3 | 黃 | 珍品 | `#E0B852` | `3`／`黃`／`珍品`／`yellow` |
| 4 | 紫 | 極品 | `#BD85ED` | `4`／`紫`／`極品`／`神品`／`purple` |

> 2026-11-17 裝備體系改名：凡品→**一般**、神品→**極品**（「神品」保留給未來更高一階；舊名 CSV 照樣能解析）。裝備的品質＝內建效果數，見 [EQUIPMENT.md](EQUIPMENT.md) §1。

- **單一真相**：`Scripts/Exchange/ExchangeData.cs` 的 `ItemRarity`（列舉）＋`RarityInfo`（顏色、色名、品名、解析）。
  之後掉落、抽選、背包要顯示稀有度，一律引用它，不要再各自定義顏色。
- 留空、0、看不懂的值一律當白（看不懂會印 warning）。
- **稀有度色是「語意色」**：只能用在文字、細外框、小菱形、細色條。UI 規範（[art_direction/UI_ICON_GUIDELINE.md](art_direction/UI_ICON_GUIDELINE.md) §1.2）
  禁止的紫色指的是面板與框體材質；稀有度紫是作者指定的例外，**不可大面積鋪色**。
- ~~目前稀有度只存在兌換表~~ → **2026-11-17 裝備體系起，稀有度是物品本身的屬性**（見 [EQUIPMENT.md](EQUIPMENT.md) §1）：裝備＝這一件身上幾條內建效果；非裝備＝ItemTable 的 `Rarity` 欄（留空＝一般）。兌換表不再寫稀有度，改寫生成規格 `Spec`（§3）。
- 列表上的品質：裝備＝`Spec` 寫死的品質（名稱顯示「極品 名稱」、上色）；裝備沒寫死＝「隨機品質」（語言表 10055，不上色）；非裝備＝ItemTable 的 Rarity。
- 面板上的呈現：名字用稀有度色、格框內一圈 2px 稀有度細框、列表與大圖左／上方有一條稀有度色條。

---

## 3. 資料表：`Assets/Data/ExchangeTable.csv`

由場景 `GameManagers` 上的 `GachaTableProvider` 的 **Exchange Table CSV** 欄提供（2026-11-15 已直接寫進 `MainScene.unity`，不用手動拖）。

```
ExchangeId,ItemId,Category,Spec,Cost,SortOrder,Note
1001,10003,Weapon,q=4,proofB:1;merit:100,5,極品樹妖的枯枝
```

| 欄 | 意義 |
|---|---|
| `ExchangeId` | 這一列的代號（正整數、唯一）。**收藏存的是它**，改號會讓玩家的收藏失效 |
| `ItemId` | ItemTable 的 ID；不存在就略過該列（印 warning） |
| `Category` | 分類英文名（見 §1 的 arg 清單，另有 `Other`）；**留空＝依 ItemTable 推斷**：血統藥劑→Bloodline、EquipSlot 武器→Weapon、胸甲/鞋/手套→Armor、護身符→Amulet、戒指→Ring、能力珠→Gem、Consumable/Potion→Consumable、Material→Material |
| `Spec` | **生成規格**（2026-11-17 取代原本的 `Rarity` 欄，同一個位置）：語法同 [EQUIPMENT.md](EQUIPMENT.md) §6.1（`q=4`、`fx=2@1`、`s=1`、`lv=2`）。**兌換那一刻才照它產生**——每次兌換效果與孔數各骰各的，珠子等級也是。留空＝全隨機。非裝備／非珠子填了會警告並忽略。寫死的品質超過這件能長的效果數（例：某武器能吃的效果不到 3 種卻寫 `q=4`），載表時就警告 |
| `Cost` | `貨幣:數量`，多種貨幣用分號：`proofB:1;merit:100`（代號見 §5）。只寫數字＝功德值。沒有價格的列會被略過 |
| `SortOrder` | 「推薦排序」用的順序（小的在前）；留空＝`ExchangeId×10` |
| `Note` | 只給人看（物品名），程式不讀 |

- `#` 開頭的列是註解。
- **2026-11-17 重建**（作者定）：舊的 118 列全部下架，只留 65 列——極品樹妖的枯枝（`1001`，10003、`q=4`、B 級證明 1＋功德值 100；**物品 10003 第④階段才建，之前載表會警告「不在 ItemTable 裡，略過」**）、24 種能力珠（合併後保留的舊道具 409／414／425 不賣）、小回血瓶／小回魔瓶、38 列血統藥劑。保留原本的 `ExchangeId` 與價格（收藏不會失效）。**所有裝備一律寫死極品**；之後加新道具時 AI 要先問作者稀有度。
- （以下是舊紀錄）⚠ **2026-11-15 的 118 列全是暫定值**：除了銅錢（101，2026-11-16 改名功德值）以外，ItemTable 每一件都列進來；稀有度與價格依類型套公式（武器輪流白藍黃紫、起始血統藥劑藍、進階藥劑黃／紫、珠子藍黃交錯…），
  只是為了測試排序與篩選，**請作者重新定價與定稀有度**、刪掉不想賣的列（例：劇本道具、GM 測試武器）。

---

## 4. 面板（`Scripts/UI/Panels/ExchangePanel.cs`）

### 4.1 功能

| 功能 | 說明 |
|---|---|
| 分類下拉 | 全部／血統／武器／盔甲／護身符／戒指／能力珠／道具／材料／其他 |
| 名稱搜尋 | 標題列中間的輸入框，**打字即篩**；比對物品名稱（子字串、不分大小寫） |
| 稀有度篩選 | 「全部」＋白藍黃紫四顆，**可複選**；都不選或四顆全選＝全部 |
| 只看買得起 | 依目前持有的貨幣判斷 |
| 只看收藏（★） | **收藏一律用右鍵**（2026-11-16 作者定）：三種檢視都是右鍵點物品＝加入收藏，再按一次＝取消。星星只是標記（亮＝已收藏；列表／大圖沒收藏時畫暗星，縮圖沒收藏不畫）。列表／縮圖的 tooltip 最後一行、大圖的小提示會寫「右鍵：加入收藏／取消收藏」。詳情彈窗另有一顆可點的星 |
| 排序下拉 | 推薦（`SortOrder`）／價格低→高／價格高→低／稀有度高→低／稀有度低→高／依名稱；同分時一律退回推薦順序 |
| 三種檢視 | **列表**：一列一件（稀有度色條、圖示、名稱、分類・品名、持有數、價格、★），**滑鼠移上去跳 tooltip**（圖示＋說明＋稀有度＋價格＋右鍵收藏提示）。**縮圖**：120 方格＋下方價格，滑鼠移上去跳同一個 tooltip。**大圖**：卡片，**說明直接寫在卡片下半**（不跳 tooltip） |
| 價格 | 買不起的價格標紅；貨幣還沒開放顯示「尚未開放」 |
| 底列 | 「共 N 件」＋「重設篩選」 |
| 錢包 | 標題列右側：功德值＋B／A／S 證明四組「圖示＋數字」（即時更新，2026-11-16，見 §5） |
| 篩選記憶 | 面板實例會被快取，關掉再開保留上次的條件與檢視模式（換場景不保證） |

點任何一件 → **詳情／兌換彈窗**（`ExchangeDetailPopup.cs`，Popup 層）：標題＝物品名（稀有度色）、左邊大圖＋稀有度・分類＋持有數＋★、
右邊說明（TipStats）與劇情（TipLore，斜體）、單價、數量（－／＋／最大，上限 99）、合計（買不起標紅）、兌換後剩餘、〔兌換〕〔取消〕。
點彈窗外或 ESC＝取消。

兌換成功：扣錢 → 走 `RunProgress.GiveItem`（取得物品的統一入口，裝備／珠子會在這時骰孔數與等級）→ 跳「獲得 X ×N」提示；
**背包滿的部分掉在玩家腳下**（比照抽選，`ExchangeService.Grant`）。

### 4.2 版面座標（底圖 `ExchangePanel_Bg.png` 原生像素 1483×955，左上為原點；外框 `FrameScale` 1.08）

| 區塊 | 位置 |
|---|---|
| 標題帶（金線之上） | y 72～182 |
| 標題「兌換所」 | x 80，寬 320 |
| 搜尋框 | x 420～870（2026-11-16 從 960 縮短，讓位給四組錢包） |
| 錢包 | x 890～1336（功德值一組 180 寬、證明三組各 82 寬，間隔 6） |
| 關閉鈕 | 標題帶右端 (1340, 86) 82×82（2026-11-16 從外框右上角外面移進來） |
| 工具列 | y 200～256：分類 62～292、稀有度 308～696、只看買得起 716～866、★ 876～932、排序 948～1208、檢視三鈕 1238～1422 |
| 結果區 | x 62～1422、y 268～858（右側 14px 捲軸） |
| 底列 | y 864～900 |

格子尺寸：列表 72 高；縮圖 120×152（一列約 10 個）；大圖 316×440（一列 4 張）。改常數就換版面（`ExchangePanel` 檔頭）。

詳情彈窗沿用同一張底圖、縮 0.74 顯示，座標見 `ExchangeDetailPopup.OnBuild`。

### 4.3 美術素材（`Resources/UI/ExchangePanel/`）

2026-11-15 由 AI 操作 ChatGPT「燃燈劫」專案產生（對話〈Generate Exchange Shop Frame〉，4 次額度、零被擋），照 UI_ICON_GUIDELINE，風格錨點＝背包／倉庫／彈窗／按鈕的總覽圖。
工作檔在 `TempImage/exchange/`（不進 git）。

| 檔案 | 尺寸 | 用途 | 九宮格邊界（L,B,R,T） |
|---|---|---|---|
| `ExchangePanel_Bg` | 1483×955 | 主面板與詳情彈窗的底圖（蓮花頂飾、標題帶金線分隔） | — |
| `ExchangePanel_Tile` | 256×256 | 品項格框、工具列按鈕底（**Sliced**） | 14,14,14,14 |
| `ExchangePanel_Field` | 708×50 | 搜尋框、下拉鈕、小按鈕底（**Sliced**） | 24,10,24,10 |
| `ExchangePanel_Icon_{List,Grid,Card,Search,Star,Sort}` | 500×500 | 檢視三鈕、放大鏡、收藏星、排序 | — |

兩張要九宮格的圖 meta 已設 `spriteMeshType: Full Rect`（Tight 網格不能 Sliced）。
另外借用既有素材：關閉鈕 `Common/CloseBtn_*`、兌換鈕 `InventoryPanel/inventoryPanel_Cell_Selected`（深緋紅）、取消鈕 `inventoryPanel_Cell_UnSelected`（深石）。

---

## 5. 貨幣（2026-11-16 定案：功德值＋B／A／S 劇本證明）

規則見 [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) §8。原本預留的「勳章＋點數」已撤銷，改由證明取代。

| 代號（CSV） | 別名 | 名稱（語言表） | 圖示（ItemTable） | 存在哪 | 排序權重（功德值等值，暫定） |
|---|---|---|---|---|---|
| `merit` | `copper`、`功德`、`功德值` | 功德值（10046） | 101 `item_merit` | `SaveManager.Currency`（原銅錢的金錢數字） | 1 |
| `proofB` | `B` | B級證明（11001） | 106 `item_proofB` | `SaveManager.GetProofs`（`StatsDTO.proofB`） | 500 |
| `proofA` | `A` | A級證明（11002） | 107 `item_proofA` | 同上 `proofA` | 1500 |
| `proofS` | `S` | S級證明（11003） | 108 `item_proofS` | 同上 `proofS` | 5000 |

- **登記表**：`ExchangeData.cs` 的 `ExchangeCurrencies`。`Get()` 會先把別名換成正式代號，所以舊表的 `copper:500` 照樣能用（表已全改成 `merit:`）。
- **價格寫法**：`proofB:1;merit:100`＝B 級證明 1 張＋功德值 100；只寫數字＝功德值。證明的定價原則（總量固定、一般收 1 張、稀有的綁 S）見 CORE_LOOP §8.2。
- **錢包**：標題列右側四組並排（功德值、B、A、S；順序 `ExchangeCurrencies.WalletOrder`），每組滑鼠移上去顯示貨幣名。搜尋框因此從 540 縮成 450（`SearchW`），錢包從 x 890 起（`WalletX0`）。
- **即時更新**：功德值與證明的變動都走 `SaveManager.OnCurrencyChanged`。
- **多種貨幣一起付**：先全部檢查夠不夠，再依序扣；萬一中途某一種扣失敗，前面扣過的會用 `Refund` 退回。
- 證明道具 106～108 跟功德值 101 一樣**不進背包**：取得時（`RunProgress.GiveStack`）直接加進錢包。⚠ 這條只加張數、不寫「領過」紀錄——正規發證明一律走觸發鏈 `grantProof`。
- ⏳ 現在 118 列價格都還是功德值暫定值，沒有任何品項收證明；定價時一起改（作者 2026-11-16：數值之後調平衡再做）。

## 6. 收藏

存在角色的**終身旗標** `兌換收藏`（`lifetimeFlags`，值＝分號分隔的 ExchangeId）：跨輪迴保留、換角色才清空。
為此 `SaveManager` 多了 `GetLifetimeFlagValue(key)`（讀原始字串）。

---

## 7. 程式檔案

| 檔案 | 角色 |
|---|---|
| `Scripts/Exchange/ExchangeData.cs` | `ItemRarity`／`RarityInfo`（稀有度）、`ExchangeCategory`／`ExchangeCategoryInfo`（分類與推斷）、`ExchangeCurrency`／`ExchangeCurrencies`（貨幣登記）、`CostPart`、`ExchangeEntry` |
| `Scripts/Exchange/ExchangeTable.cs` | 讀 CSV（懶載入、provider 沒就緒時不建快取） |
| `Scripts/Exchange/ExchangeService.cs` | 買不買得起、最多買幾個、兌換（扣款＋發貨＋背包滿掉地上）、收藏 |
| `Scripts/UI/Panels/ExchangePanel.cs` | 主面板（含程式建的下拉選單 `DropMenu`） |
| `Scripts/UI/Panels/ExchangeDetailPopup.cs` | 詳情／兌換彈窗 |
| `Scripts/UI/ExchangeEntryView.cs` | 品項的滑鼠進／出／點事件轉接 |

順帶改到的既有檔：`GachaTableProvider`（兌換表欄位）、`MainScene.unity`（掛上 CSV）、`InteractionManager.OpenPanelById`（`exchange`）、
`StorageBagCoordinator`（N 鍵）、`PlayModeStaticReset`（清兩個快取）、`SaveManager`（`GetLifetimeFlagValue`）、
`UIManager.TextInputFocused` ＋ 各熱鍵腳本（見 [PROBLEMS.md](PROBLEMS.md) **K4**）。語言表 **10001–10052**。

---

## 8. 之後可以加的（未做）

- 限量／每輪限購（`Stock` 欄＋存檔）、折扣與「今日推薦」、最近兌換紀錄、比較（大圖模式兩件並排）。
- 依「是否已解鎖」顯示（例：打通某關才上架）——可以沿用抽選的解鎖清單或通用條件（[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §2.6）。
- 稀有度搬進 ItemTable，讓背包、掉落、抽選結算也顯示稀有度框。
