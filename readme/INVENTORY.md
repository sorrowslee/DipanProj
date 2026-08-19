# 背包系統 (Inventory)

> 返回 [文件總覽](README.md)｜底層框架見 [UI_SYSTEM.md](UI_SYSTEM.md)｜倉庫與「背包↔倉庫互搬」見 [STORAGE.md](STORAGE.md)｜存檔見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)
>
> **2026-06-23 更新**：背包已接入「共用 slot 拖放/搬運系統」，可與倉庫**拖放＋點擊互搬**（含裝備）；新增**重整鈕**（整理道具格）；**移除底部名稱列**（tooltip 已顯示名稱）；資料層加 `CaptureState/RestoreState`（存檔）、`SetEquipped`、`SortGrid`，並實作 `IItemGrid`。詳見下文與 [STORAGE.md](STORAGE.md)。
>
> **2026-07-16 更新**：新增**藥水系統**（藥劑分類、背包兩格藥水格綁定種類、按 1/2 喝、喝藥特效）；拖曳可放的專用欄位**黃色高亮**＋丟錯格**自動歸位**＋**右鍵藥水快放**；版面座標**重量**到新背景 `1126×1397`（原本用到舊快取尺寸導致高亮偏位）；底部 HUD 血瓶槽**鏡像顯示**背包藥水（見 [BOTTOM_HUD.md](BOTTOM_HUD.md)）。
>
> **2026-07-28 更新**：⚠️ **金錢不再是背包道具**——銅錢（101）改成存檔裡的一個數字，背包底部銅錢 icon 後面直接顯示總額（見下方「金錢」一節）。`ItemTable.csv` 新增 **`BloodlineID`** 欄。
>
> **2026-08-18 更新**：`ItemTable.csv` 再新增第 18 欄 **`BloodlineUpgrade`**（血統進階藥劑的目標階數，全系列通用）。血統藥劑的道具列全面改版（301 殭屍 / 310 中階 / 311 高階，舊的野魂 302 幽靈已刪）。**血統的規則正典從 [GACHA_SYSTEM.md](GACHA_SYSTEM.md) §5 移到 [BLOODLINE.md](BLOODLINE.md)。**

>
> **2026-08-07 大改版**：換上新背景 `inventoryPanel_Bg.png`（**1254×1254 正方形**），道具區改成 **5×4 一頁 ＋ 上方兩個頁籤（裝備 / 消耗品）＋ 底部翻頁**。道具格由「一包 63 格」改成 **裝備包 40 + 消耗品包 40**，分包規則只有一條：**穿得上裝備欄的進裝備包，其餘全部進消耗品包**。見下方「分包與分頁」一節。

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

## 分包與分頁（2026-08-07）

道具格是**一條扁平陣列切成兩段**：前 40 格 = 裝備包、後 40 格 = 消耗品包。之所以不做成兩個獨立容器，是因為「鍛造台鎖住哪一格」「存檔的格位」「新手教學要指哪一格」全都用同一個**格子編號**在對話——切段能讓那些地方一行都不用改。

| 概念 | 位置 | 說明 |
|---|---|---|
| 容量 | `InventorySystem.EquipBagCount` / `ItemBagCount` | 目前各 40。**要加格只改這兩個常數**，介面會自動多分幾頁 |
| 一頁幾格 | `InventorySystem.PageSlots` | 20（介面 5×4）。`PagesOf(bag)` 由容量除出來 |
| 分包規則 | `InventorySystem.BagFor(ItemData)` | **唯一的判斷點**：`d.IsEquippable` → 裝備包，其餘 → 消耗品包。要改分類只改這個方法 |
| 誰在哪一包 | `BagOf(gridIndex)` / `BagStart(bag)` / `BagCount(bag)` | 全是 static，格子編號 ↔ 分包的換算 |

被歸到**消耗品包**的東西包含：藥水、材料、劇本、血統藥劑、**能力珠**（珠子不可裝備，所以不在裝備包；2026-08-07 與作者確認過）。

**重整鈕只整理「目前看到的那一包」**（`SortBag(bag)`），排序規則：

- 裝備包：武器 → 盔甲 → 手套 → 鞋子 → 護身符 → 戒指（`SortEquipOrder`），同類再依物品 ID
- 消耗品包：藥水 → 其他，同類再依物品 ID

有實例資料的物品（裝備、能力珠）**一件一格搬過去、絕不合併**，否則鑲嵌與珠子等級會被洗掉。

**幾條為了防止物品消失而存在的規則**（改動時不要拿掉）：

- `MoveGrid(from,to)` **跨包一律拒絕**——不然裝備會被搬進消耗品包，之後永遠排序不到正確位置。
- 真的跨包時（例：背包停在消耗品頁、從倉庫拖一把劍進來），由 `InventoryActions.Resolve` 攔下來改走 `AddStack`「丟進它該去的那一包」，**不是拒收**——拒收的寫法會讓來源被清空而物品消失（見 [PROBLEMS.md](PROBLEMS.md) 與 [STORAGE.md](STORAGE.md)）。
- 一頁的格子若超出該包容量（容量不是 20 的整數倍時會發生），那些格子直接 `SetActive(false)`，而不是留一個 index 越界的格子在那裡。
- `Unequip` 只找**裝備包**的空格；鍛造「移除鑲嵌」前算空位要算**消耗品包**（`FreeSlotCount(BagKind.Item)`）。

**舊存檔遷移**：`RestoreState` 會檢查每一格「這個格號所在的包，跟這件東西該去的包是不是同一個」，對不上就改用 `AddStack` 丟進正確的那一包。物品與鑲嵌都不會掉，只有排列順序被重排一次（Console 會印出重排了幾件）。

---

## 背景與格子座標（量自 `inventoryPanel_Bg.png`，**1254×1254**）

背景圖：`Resources/UI/InventoryPanel/inventoryPanel_Bg.png`。座標都在**背景原圖像素空間**（左上為原點、y 向下）；整個 `Frame` 等比縮放塞進畫面（預設顯示高 900 參考單位，正方形所以寬也是 900），格子座標因此不必隨解析度改。

> ⚠️ **座標一定要對準真正的背景圖尺寸**：2026-07-16 一度用到「舊快取的 1133×1388」量座標，整套被平移＋縮放 → 武器格黃光偏大、藥水格完全沒對準。**改背景圖務必重量**（`BgW/BgH` 與下表全部）。

| 區域 | 參數（`InventoryPanel.cs` 最上方常數） |
|---|---|
| 道具格 5×4（一頁） | 格心 x `GridCx = 403 / 513.4 / 623.8 / 734.2 / 844.6`、y `GridCy = 467 / 577 / 687 / 797`；格框 `95×92` |
| 裝備欄 6 格 | 順序**照背景圖畫的剪影**：左欄由上到下 = 武器 / 手套 / 鞋子，右欄 = 盔甲 / 護身符 / 戒指。三排方框大小不同，所以中心與寬高都逐格列在 `EquipCx/EquipCy/EquipBoxW/EquipBoxH/EquipIconSize`（上排 `220×258`、中下排約 `173×230`） |
| 藥水格 2 格 | 面板最下方突出的兩格，中心 `x=534 / 717`、`y=1098`；格框 `132×172`。左＝鍵1、右＝鍵2 |
| icon 與數量字 | **沒有逐格常數**：內容框 = 格框 × `IconFillX/IconFillY`（0.84 / 0.82），實際大小由 `IconFit` 依每張圖的不透明內容反推（見上一節）；數量字級 = `min(寬,高) × 0.26` 夾 18~30 |
| 頁籤 2 個 | 中心 `x=499 / 749`、`y=338`；plaque 內容寬 `248`、命中框 `248×74`；圖示高 `54` |
| 底列（都在 `BarCy=939`） | 重整鈕 `x=410`（內容寬 190）／上一頁 `x=551`／頁碼 `x=627`／下一頁 `x=703`（三者內容高 66、命中框 58×70）／金錢 `x=812`、`y=937`、框 `108×46` |

**頁碼只顯示「現在第幾頁」**，不顯示總頁數；還有沒有下一頁由箭頭亮不亮表達。

**金錢靠左對齊**（`TextAnchor.MiddleLeft` + `resizeTextForBestFit`）擺在牌子前段。文字框右界必須停在 **x≈866 之前**——背景圖從 x≈878 開始畫錢幣，壓過去就疊在錢幣上。長數字不會撐出框：bestFit 會自動縮字級（實測 `999,999,999` 約在 20 級、`999,999,999,999` 約在 15 級就塞得下，都高於下限 14）。

### 並排位置（與倉庫 / 鍛造 / 傳送門同開時）

⚠️ **並排的 X 要用「看得見的美術」算，不能用整張圖的寬度**。這幾張底圖四周都有大片透明留白：背包 1254 裡真正不透明的是 x 57~1198（左右各 ~57px）、倉庫 1122 裡是 x 52~1070、鍛造 1536 裡是 x 127~1408。第一版用整張圖寬度去排，結果兩個面板中間空出一大塊、背包又被推到快出畫面。

現在的算法是「讓兩邊**看得見的美術**中間只留約 40 單位的縫、整組置中」，得到：`InventoryPanel.PairRightX = 400`、`StoragePanel.PairLeftX = -416`、`ForgingPanel.PairLeftX = -447`。

另外 `InventoryPanel.PairedX()` 會**夾住不讓美術超出畫面右緣**：CanvasScaler 用 `MatchWidthOrHeight = 0.5`，**畫面越窄（非 16:9）可用的參考寬度就越小**（作者的視窗約 1.49 比例 → 參考寬只有 ~1756 而不是 1920），固定值在窄視窗會把背包右半邊切掉。

> 座標映射 `Place(rt, px, py, w, h)`：錨到 frame 左上角、`pivot=(0.5,0.5)`、`anchoredPosition=(px,-py)`、`sizeDelta=(w,h)`。hover 外框與「可放這格」提示都是拉伸貼滿各自的格框（見下一節）。

### 兩種格子提示：hover ＝ 描邊、可放這格 ＝ 呼吸的框

| | 長相 | 什麼時候出現 | 貼在哪 |
|---|---|---|---|
| hover | **一圈靜止的細金框**（`SlotOutline`，不填滿），寬 3.5、色 `(1, 0.88, 0.55, 0.85)` | 滑鼠移過**任何**格子 | 道具格、裝備欄、倉庫格 |
| 可放這格 | **會呼吸的亮金外框**（α 在 0.40↔1.00 之間，速度 4.2）＋ 很淡的固定底光 `(1, 0.85, 0.40, 0.07)` | 拖著東西時 | 裝備欄、藥水格 |

原本兩者都是「整片鋪滿的黃色」、只差 0.08 的 alpha —— 玩家分不出來，拖曳提示等於白做；而且新背景的裝備欄是舊版面積的 3.4 倍、一個道具格的 6.5 倍，同一片顏色在大格子上就變成一大塊黃色看板（[PROBLEMS.md](PROBLEMS.md) **E11**：本專案是 Linear 色彩空間，`α=0.22` 疊在近黑底上看起來等於 Gamma 的 `α≈0.45`）。

**兩個實作上的地雷**：

- 「可放這格」是**一個容器**（底光 Image ＋ 外框子物件），開關一定要用 `SetActive`。**只關 `Image.enabled` 沒用**——外框是子物件，父物件的 Image 被關掉子物件照樣會畫出來。
- 呼吸用 `Time.unscaledTime`。背包會把遊戲暫停（`timeScale = 0`），用一般 `Time.time` 會整個停住不動。

會動的刻意是**外框**而不是底光：底光只要一強，大格子又會退回「一片黃色看板」。要完全拿掉底光就把 `DropHiColor` 的 α 設 0。

### 物品 icon 的大小正規化（`UI/IconFit.cs`）

**格子裡的 icon 一律「不透明內容塞滿內容框」，不是「整張圖塞滿格子」。**

原因：物品 icon 的透明留白差非常多——2026-08-07 量過 30 張，內容佔長邊從 **41%**（`item_hpPosition_s` 500×500 裡只有 146×206）到 **100%**（`weapon_sword` 整張畫滿）。不處理的話同一個格子畫出來，藥水只有劍的 2.4 分之一（作者實機第一眼就看出來了）。

做法：`IconFit` 用 `Sprite.vertices`（Tight 網格的頂點）算出不透明內容的外接框，再反推 Image 的 `sizeDelta` 與偏移。**不需要貼圖開 Read/Write**，也不必為每張圖手寫常數、換圖不用重量——這是它跟下面那套手動 ArtSpec 最大的差別（面板零件圖只有固定幾張、位置又各自不同，手寫常數比較準；物品 icon 會一直加，只能自動）。

呼叫端要知道的三件事：

1. 你設的 `sizeDelta` 語意是**內容框**（看得見的那塊會塞滿它），不是整張圖多大。背包這邊統一用 `格框 × IconFillX/IconFillY`（0.84 / 0.82），所以**不再有逐格的 icon 尺寸常數**。
2. **只處理固定尺寸的 icon**（`anchorMin == anchorMax`）。四邊拉伸型的會被跳過、維持原行為——所以倉庫格與劇本方框的 icon 已改成固定尺寸。
3. 留白多的圖，Image 的 rect 會被放大到**比格子還大**（藥水在 95×92 的格子裡 rect 是 183×183），多出來的全是透明。icon 是 `raycastTarget=false` 不影響點擊，但**之後若要在格子加 `Mask`／`RectMask2D` 要記得這件事**。

生效範圍：掛在 `ItemIcons.Apply`（畫物品圖示的唯一入口）裡，所以背包格／裝備欄／藥水格／倉庫／鍛造鐵砧與孔位／過關結算／抽選面板／底部 HUD 藥水／傳送門劇本方框全部一起生效。**新增畫 icon 的地方一律走 `ItemIcons.Apply`，不要直接讀 `data.Icon`**（否則珠子只剩空白珠身、而且大小又會不一致）。

**數量文字**也一併規範：字級由格子大小算（`min(寬,高) × 0.26`，夾在 18~30），並加深色陰影，壓在 icon 亮處才看得清楚。見 `InventoryPanel.MakeCountText`。

### 零件素材與 ArtSpec（**換圖必讀**）

背景圖只畫了裝備格、格網、金錢牌、藥水格；**頁籤／重整鈕／左右箭頭／頁碼框是獨立 PNG，由程式疊上去**。這些 AI 產的素材都是「內容只佔中間一塊、四周整片透明」，uGUI 對齊的是整張圖，直接照原圖擺一定偏掉（[PROBLEMS.md](PROBLEMS.md) E9）。所以沿用抽選／鍛造那一套：把每張圖**不透明內容的邊界框**量出來寫成 `Art` 常數，`PlaceArt()` 反推 Image 要多大、中心放哪。

| 檔名（`Resources/UI/InventoryPanel/`） | 原圖 | 不透明邊界框 (x, y, w, h) | 用途 |
|---|---|---|---|
| `inventoryPanel_Bg` | 1254×1254 | — | 面板底圖 |
| `inventoryPanel_Cell_Selected` | 867×288 | 26, 34, 803, 244 | 頁籤（選中，紫色） |
| `inventoryPanel_Cell_UnSelected` | 822×278 | 24, 36, 776, 227 | 頁籤（未選中） |
| `inventoryPanel_Cell_EquipmentIcom` | 555×449 | 20, 35, 507, 402 | 裝備頁籤圖示 |
| `inventoryPanel_Cell_ItemIcom` | 482×518 | 69, 46, 349, 453 | 消耗品頁籤圖示 |
| `inventoryPanel_Cell_RefreshButton` | 866×288 | 22, 5, 822, 270 | 重整鈕 |
| `inventoryPanel_Cell_PageButton_Right` | 500×500 | 80, 32, 350, 435 | 下一頁；**上一頁直接鏡像同一張**（`PlaceArt(..., mirror:true)`，不另外出圖） |
| `inventoryPanel_Cell_PageNum_Bg` | 500×500 | 18, 35, 464, 424 | 頁碼底框 |

**素材圖本身 `raycastTarget=false`，點擊全靠疊在上面的透明按鈕**（`MakeGhostButton`）——這樣美術照樣完整露出，只用輕微 tint 當 hover / 按下的回饋。透明按鈕一定要在素材圖**之後**建立，否則收不到點擊。

> Max Size：底圖 1254（長邊 ≥1000）→ 2048；其餘零件圖 → 512。規則見 `Editor/UIAssetRules.cs`，既有檔案用選單工具批次修。

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
| `BloodlineID` | **系列起始藥劑**（第 16 欄 / index 15）：`>0` ＝ 對應 `BloodlineTable.csv` 的血統 Id，慣例上是某個系列的第一階。喝下去決定本世走哪一個血統系列，**一世一次、不可逆、不能改吃別系列**。程式端 `ItemData.IsBloodlineStarter` |
| `GemID` | **能力珠**（第 17 欄 / index 16）：對應 `GemTable` 的 GemID。見 [GEM_SOCKET.md](GEM_SOCKET.md) |
| `BloodlineUpgrade` | **血統進階藥劑**（第 18 欄 / index 17）：值 = 目標階數（`2` 中階、`3` 高階）。**全系列通用**——不指定血統、只指定階數，實際變成哪一種由 `BloodlineSeriesTable.csv` 決定。程式端 `ItemData.IsBloodlineUpgrade` |

> `IsBloodline` = 起始或進階任一（UI 用這個決定要不要走喝藥流程）。
> **血統的完整規則、兩張表、逐階限制一律以 [BLOODLINE.md](BLOODLINE.md) 為準**，這裡只記欄位。

內容（會持續增加）：**武器**（ItemTable ID 與 `WeaponID` 同號對應 `WeaponTable`，`EquipSlot=Weapon`）＋雜物（`101~103`：銅錢/卷軸/符紙）＋**藥水**（`201` 小回血瓶、`202` 小回魔瓶：`Category=Potion`、`HealHp/HealMp=10`、`MaxStack=99`）。分類欄 `Category` 目前用到 `Weapon`、`Currency/Material`、**`Potion`（藥劑，可拖到藥水格、按數字鍵喝）**。武器 icon 在 `UI/Icons/Equipment/`，其餘在 `UI/Icons/Items/`。

> **CSV 寫法**：欄位內含逗號的長文字請用雙引號包覆,例如 `"傷害 5，直線飛行"`;引號內要放一個雙引號就寫 `""`。需要換行就在文字裡寫 `\n`(會被轉成換行)。`ItemDatabase` 用支援引號的解析器讀取。

**血統藥劑三列**（2026-08-18 改版；舊的 `301` 野魂 / `302` 幽靈**已刪除**）：
`301` 血統藥劑・殭屍（`BloodlineID=10`，系列起始）、`310` 血統進階藥劑・中階（`BloodlineUpgrade=2`）、
`311` 血統進階藥劑・高階（`BloodlineUpgrade=3`）。見 [BLOODLINE.md](BLOODLINE.md) §3。

---

## 金錢：不是背包道具，是存檔裡的一個數字（2026-07-28 改）

抽選祭壇要花錢之後，銅錢當可疊道具就不合用了——背包很快被錢塞滿，而且「三格 99 顆」也沒辦法當貨幣算。現在：

- **資料**：金錢存在角色存檔的 currency 欄（`SaveManager.AddCurrency` / `Currency`），**背包裡完全不會有銅錢道具**。
- **顯示**：背包面板背景**底部那個銅錢 icon 後面**直接印總額（`BuildMoneyText()`／`RedrawMoney()`，座標常數 `MoneyCx/MoneyCy/MoneyW/MoneyH`）。面板訂閱 `SaveManager.OnCurrencyChanged`，抽完獎當場就會跳數字。
- **⚠️ 掉落端刻意不動**：怪物掉寶、寶箱、觸發鏈的 `giveItem(101)` **全部照舊寫「給道具 101」**。轉換發生在唯一入口——`RunProgress.GiveItem` 與過關落袋 `SettleIntoBag` 看到 101 就改呼叫 `SaveManager.AddCurrency`。所以既有的掉落表與地圖資料一列都不用改。
- **`ItemTable` 的 101 那一列要保留**：toast 的「獲得 銅錢 ×50」還要靠它拿名稱與 icon。
- **舊存檔遷移**：`SaveManager.ApplyToSystems` 會跑一次 `SweepMoneyIntoWallet()`，把背包/倉庫裡殘留的 101 掃進錢包。`StorageLauncher` 也不再開場塞 500 銅錢（改用作弊面板的「獲得 10000 元」）。

---

## 資料層 `InventorySystem`（純資料、跨場景常駐單例）

- 持有 **80 道具格**（`ItemStack[]`，前 40 裝備包 + 後 40 消耗品包，見上方「分包與分頁」）+ 6 裝備欄 + 2 藥水格綁定；懶漢單例,第一次存取自動建立 + `DontDestroyOnLoad`（換地圖物品延續）。
- **不含任何 UI**——任何變動觸發 `event Action OnChanged`,UI 訂閱重繪。
- API：`AddItem(id,count)`（先疊堆再放空格,回傳放不下的剩餘）、`RemoveAt`、`MoveGrid(from,to)`、`EquipFromGrid(index)`、`Unequip(slot)`、`GetGrid/GetEquipped/GetData/HasAnyItem`。
- **（2026-06-23 新增）**：
  - 實作 **`IItemGrid`**（`Capacity/GetAt/SetAt/MoveWithin/DisplayName`），讓背包道具格能與倉庫共用搬運/拖放程式（見 [STORAGE.md](STORAGE.md)）。
  - `SetEquipped(slot,itemId)`：直接設定某裝備欄（拖放裝備/跨容器用，會觸發 OnChanged → 裝備↔武器連動）。
  - `SortBag(bag)`：整理**單一一包**（合併同物品＋依規定順序排序＋壓實，不動裝備欄、不動另一包；重整鈕用）。`SortGrid()` = 兩包都整理，留給不分包的舊呼叫端。
  - `AddItem` / `AddStack` **會自動判斷丟進哪一包**（倉庫點擊送過來、掉落物落袋、觸發鏈給道具都走這條）。
  - `CaptureState()/RestoreState(InventoryDTO)`：存檔快照（純資料、不碰檔案；由 `SaveManager` 呼叫，見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)）。

---

## 左鍵 vs 右鍵（**全遊戲的鐵則**，2026-08-19 拍板）

> **左鍵＝搬移／裝備／綁定，永遠不消耗任何東西。**
> **右鍵＝使用，這是全遊戲唯一會消耗道具的滑鼠操作。**

為什麼要立這條規則：原本「使用」散在三個地方各寫一份——背包左鍵、背包右鍵、藥水熱鍵。
結果就出現了**沒人打算做、但兩邊都寫了**的行為：**左鍵點血統藥劑會直接喝掉**，
而血統是本世不可逆的，等於一次誤點就定終身（作者實測回報）。
現在收攏成單一入口 `Inventory/ItemUse.cs`，三個呼叫端都走它。

| 操作 | 做什麼 | 會不會消耗 |
|---|---|---|
| **左鍵**背包格 | 傳送門開著＝送劇本進去／鍛造開著＝放上鐵砧／倉庫開著＝送進倉庫／藥水＝綁定到快捷格／可裝備物＝穿上 | **不會** |
| **左鍵**裝備格 | 卸下 | 不會 |
| **左鍵**倉庫格 | 送回背包 | 不會 |
| **右鍵**背包格 | **使用**：回血回魔藥劑＝當場喝掉；血統藥劑＝跳確認視窗後喝 | **會** |
| **右鍵**其他地方 | 沒有動作（倉庫裡的東西要先拿回背包才能用） | — |
| **拖曳** | 搬移。**只有左鍵能拖**（擋在 `SlotDragController.Begin`） | 不會 |
| **數字鍵 1／2** | 喝快捷格綁定的藥水（走同一支 `ItemUse`） | 會 |

**左鍵點一件「只能使用」的東西（血統藥劑、能力珠、材料）＝安靜地什麼都不做。**
刻意不 Toast「請按右鍵」——那條規則只需要學一次，每次點都念會很煩。

### 加一種「可以使用」的新道具

只要動 `Inventory/ItemUse.cs` 三個地方，三個呼叫端（背包右鍵、藥水熱鍵、未來任何入口）自動支援：

1. `IsUsable(ItemData)` — 加上你的判斷（右鍵按下去會不會有事）。
2. `PlanUse(itemId)` — **純計算**：能不能用／不能用的理由／要不要先跳確認視窗。不改任何狀態。
3. `TryUse(itemId, out message)` — 真的用下去。成功失敗都填 `message`，UI 有字才 Toast。

⚠ 兩個容易踩的點：
- **`Plan.Reason` 留空 = 「這東西根本沒有使用行為」**，UI 會安靜地不做事。
  真的有理由要講（例：階段不對）才填字，否則玩家會被「無法使用」洗版。
- **會消耗的東西，效果要先確認套得上去、再扣。** 反過來寫的話玩家會遇到
  「藥沒了、血沒回、也沒訊息」（`DrinkPotion` 就是為此先查 `LivePlayerStats()`）。

---

## 呈現層 `InventoryPanel`（繼承 `UIPanel`）

- 旗標：`Window` 層、`PausesGame=true`、`BlocksGameplayInput=true`、`ShowBackdrop=true`、ESC 可關。
- `OnBuild` 建一次：frame＋背景＋6 裝備欄＋**一頁 20 個道具格**（各是透明命中區 `Image` + 子 icon + 子數量 + `InventorySlotWidget`）＋頁籤/底列素材＋共用高亮＋透明命中按鈕。
- **格子只建一頁並重複使用**：切頁籤/翻頁時只重新綁定 `index`、**不重建物件**。這點很重要——新手教學會鎖定某一格的 GameObject，重建會讓它指到已銷毀的物件。
- `OnOpen` 訂閱 `OnChanged` 並回到上次停留的頁籤與頁數；`OnClose` 退訂。`Redraw` 依「目前這一包的第幾頁」換算出每格的扁平索引再讀資料。
- **每個頁籤各自記住停在第幾頁**（`_pageOf`）；箭頭在第一頁/最後一頁會變暗且不可按（不循環）。
- **互動**：
  - 移入 → 高亮該格 + 浮動 tooltip（名稱/功能/劇情）。
  - **左鍵**：倉庫沒開時，點道具格的可裝備物品 → 裝備（原裝的換回該格）、點裝備欄 → 卸回第一個空格；**倉庫開著時**，點道具格 → 整堆送到倉庫當前分頁（見 [STORAGE.md](STORAGE.md)）。**左鍵永遠不會使用/消耗任何東西**，見上面「左鍵 vs 右鍵」。
  - **右鍵**：使用這件道具（`OnSlotRightClicked` → `ItemUse.PlanUse`／`TryUse`）。這是全遊戲唯一的使用入口。
  - **拖放**（透過共用 `SlotDragController`）：格內重排/合併/交換、拖到裝備欄＝裝備、拖去倉庫＝存放（含裝備）。
  - **重整鈕**：整理**目前這個頁籤**那一包（`SortBag`）。
  - **頁籤**：切換裝備包 / 消耗品包。**左右箭頭**：翻頁；中間顯示 `目前頁/總頁數`。
  - **血統藥劑**（`ItemData.IsBloodline`，**含進階藥劑**）：**只有右鍵會喝，左鍵完全沒有動作**（2026-08-19 改；之前左鍵也會喝，是誤點就定終身的地雷）。流程是 `ItemUse.PlanUse` → `BloodlineSystem.Plan(itemId)` → **不能喝就在按鍵當下 Toast 出理由**（不要先跳確認視窗、按完才發現沒反應）→ 能喝才 `ConfirmPopup` → `ItemUse.TryUse` → `TryDrink`（成功與失敗的訊息都由它回傳）。**面板刻意不懂任何血統規則**，改規則不用回頭動 UI。⚠ 喝下去只換外型與體型、**不套任何屬性**。見 [BLOODLINE.md](BLOODLINE.md) §4。
  - **與倉庫並排**：倉庫＋背包同開時各自左右移（`StorageBagCoordinator` 控；背包右移位置 `PairRightX`）。
- **tooltip**：移到物品上跳出浮動說明（掛在 panel root、不受 frame 縮放、跟著游標、近右邊自動翻到左側、不擋 hover）。三段：**名稱（粗體金）**＋ **`TipStats`（正楷）**＋ **`TipLore`（斜體）**;高度由 `VerticalLayoutGroup + ContentSizeFitter` 自動撐開,空欄自動隱藏該段。

### 裝備 → 使用武器（與戰鬥連動）

裝備武器欄的武器後,玩家就改用那把武器的能力:

- 連結靠 `ItemTable.WeaponID` → `WeaponTable.ID`。
- 橋接在 `PlayerController`:`Start` 訂閱 `InventorySystem.OnChanged`,武器欄內容變動時取該物品的 `WeaponID`,呼叫 `WeaponManager.SwitchWeapon(weaponID)`。卸下武器（欄位清空）時保留當前武器、不切換。`OnDestroy` 退訂。
- **武器欄是武器的唯一來源**（2026-07-27 起）:裝備哪把就用哪把、**卸下就沒有武器**（空手時按攻擊完全沒反應）。原本的 E 鍵循環切換已移除,不再有「當前武器與裝備欄不一致」的情況。
- 邊界:`InventorySystem` 仍是純資料層、不認識戰鬥;由 `PlayerController`（既有戰鬥整合點,本就持有 `WeaponManager`）做連結。

### 給新手教學用的 `FindGridSlotRect(itemId)`

教學要「手指指向背包裡那一格」，但那件東西可能在另一個頁籤或另一頁。所以這個方法**會自動切到該物品所在的頁籤與頁數**再回傳格子：先掃目前這一頁（最常見、順便避免每幀重切頁），沒有才掃全背包並切過去。動這個方法時務必保持這個行為，否則柴房佛燈／儲藏室藥水／傳送門劇本三段教學的手指會指向沒顯示出來的格子。

### 相關檔案

- `Assets/Scripts/Inventory/ItemData.cs`（ItemData + EquipSlot 列舉）
- `Assets/Scripts/Inventory/ItemDatabase.cs`（CSV + icon 載入；`LoadFromTextAsset` 主、`LoadFromResources` 後備）
- `Assets/Scripts/Inventory/ItemTableProvider.cs`（場景元件，持有 `Assets/Data/ItemTable.csv` 的 TextAsset 參照）
- `Assets/Scripts/Inventory/InventorySystem.cs`（資料層 + 事件 + 單例）
- `Assets/Scripts/UI/Panels/InventoryPanel.cs`（面板）
- `Assets/Scripts/UI/Panels/InventorySlotWidget.cs`（格子互動元件，已實作 `ISlotView` + 拖放）
- `Assets/Scripts/UI/InventoryLauncher.cs`（**已停用／no-op**：2026-07-22 起新角色初始背包保持**完全空**，不再自動塞測試物品。開關鍵 B 已移到 `StorageBagCoordinator`。此元件留著不做事，可從場景移除；要臨時塞測試裝備就在 `Start()` 自行 `InventorySystem.Instance.AddItem(id)`，或還原 git 舊版種子碼。）
- 共用搬運（與倉庫同套，見 [STORAGE.md](STORAGE.md)）：`UI/ISlotView.cs`、`UI/SlotDragController.cs`、`UI/InventoryActions.cs`、`UI/StorageBagCoordinator.cs`
- `Assets/Data/ItemTable.csv`（與其他資料表同位置）、`Assets/Resources/UI/InventoryPanel/inventoryPanel_Bg.png` ＋ 七張 `inventoryPanel_Cell_*.png` 零件圖、`Assets/Resources/UI/Icons/...`

---

## 藥水系統（藥劑 / 藥水格 / 高亮 / 自動歸位 / 右鍵快放）

> **2026-07-16 新增。** 底部 HUD 的鏡像顯示與喝藥見 [BOTTOM_HUD.md](BOTTOM_HUD.md)。

**藥劑（Potion）**：`ItemTable.Category=Potion` 的物品（`ItemData.IsPotion`）。喝下依 `HealHp/HealMp` 回血/回魔。目前 `201` 小回血瓶、`202` 小回魔瓶（各回 10、`MaxStack=99`）。

**藥水格（背包內兩格，左＝鍵1、右＝鍵2）**：`Assets/Scripts/UI/PotionSlot.cs`。綁定的是藥劑**種類**（物品 ID）、不是某一瓶：

- 從道具格拖一種藥劑上來 → 記住 ID、顯示 icon ＋背包剩餘數量；那個種類在背包歸零 → 自動清空該格。
- 綁定存在 `InventorySystem`（`GetPotionSlot/SetPotionSlot`，跟背包一起存檔，`InventoryDTO.potionSlots`）。**只記種類、不動背包內容**（不走搬移邏輯）；往格外拖＝解綁清空。
- 使用（喝）由常駐 `PotionHotkeys` 在遊戲中按 1/2 觸發（見 [BOTTOM_HUD.md](BOTTOM_HUD.md)）。

**拖曳時的「可放這格」提示（`dropHi`）**：拖起某類物品 → 把「該類能放、且**空著**的專用欄」亮起來（拖裝備亮對應的空裝備欄、拖藥劑亮空的藥水格），放開時全關。判斷靠 `SlotDragController.DraggingItemId`（全域拖曳中的物品 ID，輪詢式、避開 Domain Reload 殘留），面板 `UpdateDropHighlights` 更新。**拖曳中不做 hover 高亮**（改用這個）。

長相與兩個實作地雷見上方「兩種格子提示」一節——重點是它現在是**容器（底光 ＋ 外框子物件）**，開關一定要 `SetActive`，只關 `Image.enabled` 子物件照樣會畫。

**丟錯格自動歸位（`InventoryActions`）**：

- 藥劑丟到裝備欄 → 自動放到藥水格（`AutoPlacePotion`）。
- 裝備丟到藥水格 → 自動裝到正確裝備欄（`EquipToCorrectSlot`）。
- 丟到一般道具格 → 就是單純重排/搬移（不特別處理）。

**左鍵＝綁定快捷格、右鍵＝當場喝掉**（2026-08-19 分家；在那之前左右鍵都是綁定）：

- **左鍵**（`InventoryPanel.OnSlotClicked` 對 `IsPotion` 分流）→ `InventorySystem.AutoPlacePotion`：
  有空位優先放**最小索引**（1 號優先於 2 號）；全滿則取代 0 號；已綁在某格則不動。
  也可直接**拖曳**到藥水格。可延伸到 N 格（`PotionSlotCount`）。**不消耗。**
- **右鍵**（`OnSlotRightClicked` → `ItemUse`）→ **當場喝掉一瓶**，與按數字鍵 1／2 是同一段程式。

⚠ 右鍵喝的時候背包是開著的，而背包 `PausesGame=true` → `timeScale=0`。
所以 `PlayerController.PlayDrinkPotionVfx` 生出來的特效**一定要 `Unscaled = true`**，
不然連按五下會得到五個定格在第 0 幀、永遠不消失的特效疊在玩家身上（同 [PROBLEMS.md](PROBLEMS.md) **D15** 那一家）。

**相關檔案**：`Inventory/ItemUse.cs`（**使用的唯一入口**）、`UI/PotionSlot.cs`、`UI/PotionHotkeys.cs`（只負責「哪顆鍵對哪一格」，消耗邏輯不在這裡）、`Inventory/InventorySystem.cs`（`GetPotionSlot/SetPotionSlot/AutoPlacePotion/PotionSlotCount`）、`Inventory/InventoryDTO.cs`（`potionSlots` 存檔）、`UI/InventoryActions.cs`（`EquipToCorrectSlot`）、`UI/Panels/InventorySlotWidget.cs`（右鍵分流＋`dropHi`）、`UI/Panels/InventoryPanel.cs`（`UpdateDropHighlights`）。喝藥特效見 [VFX.md](VFX.md)。

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
- tooltip 上半改由**裝備實際屬性組字**(取代目前純讀 `TipStats`);堆疊分割（按住搬一半）、稀有度底色、排序規則細化、搜尋。
- **右鍵喝藥水不擋「已經滿血滿魔」**——與熱鍵 1／2 一致（刻意），但右鍵比熱鍵更容易誤點，之後可考慮在滿的時候擋下並說明。
- **快捷格（HUD 兩個血瓶槽 / 背包裡的藥水格）沒有點擊處理**，右鍵它不會喝。要不要補成「右鍵快捷格＝喝」還沒決定。
- ✅ **撿道具/掉落物系統接 `AddItem`**（已做，見 [INTERACTION.md](INTERACTION.md)：拾取點 + 地上掉落物，靠近按 F）。
- （可選）背包浮動 tooltip 目前各面板各建一份（背包、倉庫各有），日後可抽成共用元件。

---

*建立於 2026-06-22：背包 v1（資料層 InventorySystem + 呈現層 InventoryPanel,整張背景 + 量測座標疊互動格,點擊裝/卸,hover 名稱）。建在 [UI_SYSTEM.md](UI_SYSTEM.md) 底層上。*
*2026-06-23 更新：接入共用 slot 拖放/搬運系統（與倉庫互拖、含裝備）；新增重整鈕（SortGrid）；移除底部名稱列；資料層加 IItemGrid / SetEquipped / SortGrid / Capture·RestoreState。見 [STORAGE.md](STORAGE.md)、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)。*
*2026-07-16 更新：藥水系統（Potion 分類、背包兩格藥水格綁定種類、按 1/2 喝、喝藥特效）；拖曳可放欄位黃色高亮 + 丟錯格自動歸位 + 右鍵藥水快放；版面座標重量到新背景 1126×1397（修正高亮偏位）；底部 HUD 血瓶槽鏡像顯示背包藥水（見 [BOTTOM_HUD.md](BOTTOM_HUD.md)）；ItemTable 加 TargetMapId/TargetEntrance/HealHp/HealMp 欄。*
*2026-07-28 更新：**金錢改成存檔數字、不再是背包道具**（背包底部銅錢 icon 後顯示總額，訂閱 `SaveManager.OnCurrencyChanged`；掉落端仍給道具 101，由 `RunProgress.GiveItem`/`SettleIntoBag` 轉呼叫 `AddCurrency`；`SweepMoneyIntoWallet` 遷移舊存檔）；ItemTable 加 `BloodlineID` 欄＋血統藥劑 301/302，背包點血統藥劑＝確認後換外型。見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md)。*
*2026-08-07 大改版：換新背景 `inventoryPanel_Bg.png`（1254×1254 正方形）；道具區改 **5×4 一頁 + 頁籤（裝備/消耗品）+ 底部翻頁**；道具格由 63 格一包改成 **裝備包 40 + 消耗品包 40**（分包規則只有 `BagFor` 一處：可裝備 → 裝備包、其餘 → 消耗品包，能力珠算消耗品）；重整鈕改成只整理當前頁籤那一包，裝備包依 武器/盔甲/手套/鞋子/護身符/戒指、消耗品包依 藥水/其他；裝備欄順序改成左欄 武器/手套/鞋子、右欄 盔甲/護身符/戒指（照背景圖剪影）；金錢改靠右對齊；頁籤/重整/箭頭/頁碼是獨立 PNG 用 ArtSpec 疊上去（左箭頭鏡像右箭頭）；`FindGridSlotRect` 會自動切頁籤與頁數給新手教學用；跨包拖放改成「丟進正確的那一包」而不是拒收；舊存檔會依分類重排一次（不掉東西）。*
*2026-08-07 微調（實機第一次跑完的回饋）：裝備欄 icon 放大到方框的 ~8 成（`178 / 148 / 142`，原本 `132 / 116`），道具格 icon `74→80`、藥水格 icon `92→108`；頁碼改成只顯示「現在第幾頁」不顯示總頁數；金錢改**靠左**對齊、框縮成 `108×46`（右界 866，錢幣圖從 878 開始）；並排位置改用「看得見的美術」重算（背包 `480→400`、倉庫 `-420→-416`、鍛造 `-483→-447`），並加上畫面右緣的夾制避免窄視窗被切掉。*
*2026-08-07 再微調（實機看到藥水 icon 特別小）：新增 `UI/IconFit.cs` + `IconFitBox`，把物品 icon 改成「不透明內容塞滿內容框」的自動正規化（量過的 30 張 icon 內容佔比從 41% 到 100%，不處理會差 2.4 倍），掛在 `ItemIcons.Apply` 這個唯一入口所以全專案一起生效；背包因此**移除逐格的 icon 尺寸常數**，改成格框 × `IconFillX/IconFillY`；倉庫格與傳送門劇本方框的 icon 從「四邊拉伸」改成固定尺寸（拉伸型不吃正規化）；藥水格、底部 HUD 藥水、劇本方框原本直接讀 `data.Icon`，改成一律走 `ItemIcons.Apply`；數量文字字級改成依格子大小算並加深色陰影。記進 [PROBLEMS.md](PROBLEMS.md) E10。*
*2026-08-07 再微調（作者問「為什麼滑鼠移過去整格變黃」）：查出那是 **hover 高亮**不是拖曳提示，兩者當時都是整片鋪滿的黃色、只差 0.08 alpha，加上新裝備欄面積是舊版 3.4 倍、又是 Linear 色彩空間（`α=0.22` 實際看起來像 `0.45`，見 [PROBLEMS.md](PROBLEMS.md) E11），所以變成一大塊黃色看板。改法：新增 `UI/SlotOutline.cs`，hover 改成**只描邊不填滿**（背包與倉庫共用）；「可放這格」改成**會呼吸的亮金外框＋很淡底光**，兩種提示一眼分得出來。*
