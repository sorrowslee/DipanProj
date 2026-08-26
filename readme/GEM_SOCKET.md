# 能力珠鑲嵌系統（物品實例 ＋ 能力容器）

> 這是**整個遊戲的戰力核心**。動到武器、裝備、背包、掉落、存檔之前，先讀完這一份。
> 相關：[FORGING.md](FORGING.md)（鍛造介面版面）、[RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)（配方/武器雙表）、[INVENTORY.md](INVENTORY.md)（背包）、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)（存檔）。

---

## 0. 一句話

**CSV 表只是模板；玩家手上那一件另外存。** 攻擊時不看武器表，看「玩家的能力容器」。

---

## 1. 最核心的觀念：表格 vs 這一件

`ItemTable` / `WeaponTable` / `RecipeTable` 定義的是**模板**——「三分裂追蹤飛劍」長什麼樣、傷害多少。
但玩家實際持有的那一把，還有一堆表格裡沒有的東西：有幾個孔、開在哪幾個位置、鑲了哪些珠子、珠子幾級。

```
玩家持有的一件武器
{ itemId: 1, sockets: [ {開, gem:{itemId:401, level:2}}, {開, gem:null}, {鎖}, {鎖}, {開, gem:null}, {鎖} ] }

背包裡的一顆珠子
{ itemId: 401, level: 2 }
```

**孔裡的珠子與背包裡的珠子是同一個結構** → 鑲上/拆下是純搬移、不做轉換 → 珠子強化到幾級，拆下來就還是幾級。

### 為什麼一定要這樣做

同一把武器 ID 掉落兩次，可能一把 2 孔、一把 5 孔，位置還不一樣。這種「每一件都不同」的資訊，
在只有 `{物品ID, 數量}` 的資料結構裡無處可放。

### 之後要加新屬性怎麼辦

在 `ItemInstance` 加欄位就好（附魔、耐久、詞綴、改造次數…）。
存檔是 JSON、用 Newtonsoft 序列化，**缺欄位會自動給型別預設值** → 舊存檔照樣讀得起來，不需要寫遷移。
唯一會壞的是「改掉既有欄位的語意」（例如把 `sockets` 換成別的結構），那才需要遷移。

### 鐵則

| # | 鐵則 |
|---|---|
| 1 | **「憑空產生一件物品」一律走 `ItemManager.Create` / `Give`**，不要自己 `new ItemStack`。否則那條路徑生出來的裝備會沒有孔、珠子會沒有等級，而且**不會報錯**。 |
| 2 | **搬運一律是「移動整份 ItemStack」**（含 `Inst`），來源要清空。只搬 `itemId` 會讓鑲嵌憑空消失。 |
| 3 | **有實例資料的物品一律不疊**（`AddItem` / `SortBag` / `GridToGrid` 的合併都會跳過）。 |
| 4 | **配方是全遊戲共用的單一物件，禁止就地改**——要改先 `RecipeEntry.Clone()`。詳見 §4。 |

---

## 2. 資料檔

### `Assets/Data/ItemTable.csv` 新增第 17 欄 `GemID`

`GemID > 0` ＝這是一顆能力珠，指向 `GemTable.csv` 的同 ID 那一列。
（照既有慣例，與 `BloodlineID → BloodlineTable` 完全同一套模式。）

ID 分段（延續既有慣例）：

| 區段 | 用途 |
|---|---|
| 1~99 | 武器（ItemTable 的 ID 與 WeaponTable 的 ID 同號） |
| 101~199 | 雜物 / 貨幣 / 材料 / 劇本 |
| 201~299 | 藥水 |
| 301~399 | 血統藥劑 |
| **401~499** | **能力珠** |
| **501~599** | **防具**（目前只有測試用的護身符 501 / 戒指 502） |

### `Assets/Data/GemTable.csv`（新）

一種珠子一列，三個等級共用同一列。

```
GemID,Name,Field,Target,Lv1,Lv2,Lv3,Note
1,反彈,MaxBounces,Recipe,1,2,5,...
```

| 欄位 | 說明 |
|---|---|
| `Field` | 要改的欄位名，**原文照抄 RecipeTable / WeaponTable 的欄位名**。這樣同一個功能在兩邊叫同一個名字，不會出現「反彈」在配方表叫 A、在珠子表叫 B 的情形。 |
| `Target` | 這個欄位在哪一層：`Recipe`（配方／子彈行為）／`Weapon`（武器數值）／`Player`（角色屬性，**效果尚未接**，先累加著） |
| `Lv1~Lv3` | 該等級的數值。可填固定值（`1`）或百分比（`10%` / `-20%`）。 |

⚠ 表是用最原始的 `Split(',')` 解析，**欄位值裡不能有逗號**（同 RecipeTable 的限制）。
⚠ `#` 開頭的列是註解，會被跳過。

**要記得把 `GemTable.csv` 拖進場景 `GameManagers` 上的 `GemTableProvider` 元件**，否則所有珠子都不生效（只會印一則 Warning）。

---

## 3. 疊加規則：**數值相加，不是等級相加**

等級只在「單一來源內部」有意義。每個來源各自查表得到數值，全部相加：

```
武器內建反彈 Lv3  → 查表 → 5 次
6 顆 Lv3 反彈珠   → 查表 → 5 × 6 = 30 次
護身符 Lv3        → 查表 → 5 次
戒指 Lv3          → 查表 → 5 次
                            ──────
                            45 次
```

好處：`GemTable` 永遠只需要 Lv1/Lv2/Lv3 三欄，不會有「Lv18 查不到表」的問題。

**能力沒有上限**——玩家可以把所有孔都塞反彈，換取極端 build（代價是不會穿透、不會追蹤、不會分裂）。
這是刻意的設計（給玩家高自由度與取捨空間），不要偷加上限。

百分比是先累加再乘：`最終值 = (基礎 + 所有固定值) × (1 + 所有百分比)`。

---

## 4. 能力容器 `PlayerAbilities`

```
裝備中的武器  → 基底（發射模式：一般子彈 / 雷射 / 近戰 / 拋物線…），深拷貝一份成為玩家專屬
武器內建能力  ┐
其他裝備能力  ├→ 各自查表得到數值 → 全部相加 → 套到那份拷貝上
所有鑲嵌珠子  ┘
```

### 為什麼一定要拷貝（**最重要的一條**）

`RecipeManager` 在 `Awake` 時每個配方只 `new` 一次。之後：

- 同一個 RecipeID 的**所有武器**拿到的是同一個物件
- **怪物**（`MonsterWeaponUser` 走同一張 WeaponTable）也是同一個物件
- 把它當 `SubRecipeID` 的**母配方**還是同一個物件

所以玩家的鑲嵌加成如果就地改欄位，會同時改到怪物身上，而且**永久累積到重開遊戲為止**。
一律 `RecipeEntry.Clone()` 拷貝一份再改。

### 注入點

`WeaponManager.AbilityResolver`（static 委派）由 `PlayerController.Start` 掛上。
`RefreshCurrentWeapon()` 會把武器表的原始資料丟給它、拿回玩家專屬的拷貝。

這個位置的好處：**八種發射分支（含雷射與佛光）全部自動吃到，一個分支都不用改**。
而 `GetWeapon()` 一律回原始資料，所以怪物完全不受影響。

### 重算時機

`PlayerController.OnInventoryChanged()`。除了比武器 ID，**還要比 `InventorySystem.LoadoutVersion`**——
因為「換了珠子但沒換武器」時物品 ID 完全沒變，光比 ID 會漏掉。

⚠ 只有鑲嵌變動時，要順手 `ClearActiveBeams()` / `ClearActiveAura()`：
光束的參數（穿透/反彈/射程）是啟動時一次性寫進去的，之後不會重讀。

### 欄位分屬兩個類別（很容易寫錯）

| 在 `ProjectileData`（彈道系統側） | 在 `RecipeEntry`（主遊戲側） |
|---|---|
| Speed / Radius / LifeTime / FireInterval / RotationSpeed / PierceCount / MaxBounces / HasBounce / HomingTurnSpeed / SpreadCount / SpreadAngle / OrbitalCount / OrbitalRadius / BeamRange（CSV 欄名 `Range`） / DotInterval / TrailStep / FlightTime / ArcHeight | Mode / BounceTarget / AreaRadius / ChainCount / ChainRadius / AimConeAngle / SnapRadius / MeleeAngle / DashDistance / DashWidth / SummonCount / SummonMaxAlive / SummonRadius / GroundEffectID |

### 連動規則（光加數值沒用的欄位）

| 能力 | 除了加數值，還要做什麼 |
|---|---|
| **反彈** | 必須同時 `HasBounce = true`；而且 `BounceTarget` 是 `None` 時要設成 `Environment`（對牆反彈），否則等於白鑲 |
| **穿透** | `-1` 代表**無限穿透**，不能直接 +1（會變成 0 ＝ 不穿透）。已經無限就維持無限 |
| **追蹤** | 數值 > 0 時要 `HasHoming = true` |
| **分裂** | 數量 > 1 時要 `HasSplit = true`（只有 Normal／Orbital；雷射／拋物線／連鎖／落雷直接讀 `SplitCount` 當道數） |
| **迅捷 × 拋物線** | `Speed` 對拋物線無意義，珠子的 +% 換算成**縮短 `FlightTime`**（丟得更快）；固定值部分忽略 |

### 依模式過濾（2026-08-26）

珠子改的欄位對「目前武器的 `Mode`」無效就**不套用**，判斷走 `WeaponModeSpec.IsEffective`（與載入檢查同一份真相）；例：佛光不會反彈、雷射沒有子彈大小、召喚不吃傷害、連鎖閃電的跳數已是獨立欄所以反彈珠對它無效。完整矩陣見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) §4。

**鍛造介面「提示不擋」**（`GemEffectiveness.cs`）：珠子可以鑲在護身符／戒指上跨裝備疊加到當前武器，所以「有沒有效」是**珠子 × 參考武器**——鐵砧上是武器就看那把、是防具就看目前裝備的武器，換武器答案就變，因此**不擋**。做法：拖進孔位時對參考武器無效 → toast（語言表 4013）；孔位上無效的珠子灰顯（`ForgeSlotWidget.Dimmed`）；珠子與鐵砧物品的 tooltip 加一行說明（4014／4015）；背包 tooltip 的鑲嵌清單標「（對目前武器無效）」（4016）。裝備中的武器一換，`InventorySystem.OnChanged` 會讓孔位重畫。

### 安全夾值（不是平衡，是「會把遊戲弄壞」的下限）

能力刻意沒有上限，但下面幾個減到 0 會直接當掉，所以無論如何都要擋（寫在 `PlayerAbilities` 的常數區）：

| 欄位 | 減到 0 的後果 | 下限 |
|---|---|---|
| `FireInterval` | 每一幀都發射 → 瞬間數千顆子彈、卡死 | 0.02 秒 |
| `Speed` | **拋物線武器的 Speed 語意是「飛行秒數」**，0 會除零 | 0.05 |
| `DotInterval` | 持續傷害改成每幀結算 | 0.02 秒 |
| `SpreadCount` | （上限）效能保護 | 64 |

> ⏳ **平衡用的上限尚未討論**。使用者要求等系統完成後一起確認並記錄在這裡。

---

## 5. 產生規則：`ItemManager` 是唯一工廠

所有「憑空生出一件物品」的路徑都走它：怪物掉落、觸發鏈 `giveItem`、拾取點、祭壇抽選、作弊面板、關卡結算。

- **裝備** → 骰孔數（0~6）＋**隨機挑位置**開孔（不是前 N 個）
- **能力珠** → 骰等級（1~3）
- 其他 → 沒有實例資料

**東西掉在地上的那一刻就已經決定好了**——地上掉落物的標籤會直接寫「佛光 (5孔)」，
玩家在關卡中途按 F8 看臨時包也看得到，這是打寶的即時回饋（刻意的設計）。
撿起來走 `GiveStack`，是「搬走地上那一件」，不會重骰。

### 機率統一放在 `RandomRules`

使用者明確要求**所有機率寫在程式裡**（機率設定太多，用表記錄不完）。
數字集中在 `Assets/Scripts/Data/RandomRules.cs` 最上方的「調整區」，每一條都有中文註解。

目前：孔數各 14%（0~6 平均）、珠子等級各 1/3。
要做「第一周目不給高孔數」就填 `SocketCountWeightsByCycle`（依周目的權重對照表，已預留）。

---

## 6. 鍛造介面（見 [FORGING.md](FORGING.md)）

- 孔位面板（`ForgeSocketGrid`）**直接綁定鐵砧上那件裝備的 `ItemInstance`**。
  珠子一拖進孔就已經鑲上去了，**沒有「提交」步驟**，關掉面板也不會消失，存檔時自然跟著那一件寫出去。
- 珠子鑲上去後背包那顆會消失（拖放的移動語意天生如此）——防止複製。
- 底部三顆按鈕（左到右）：**強化裝備**（未做）／**拆除裝備**（未做）／**移除鑲嵌**。
- 「移除鑲嵌」＝把該裝備的珠子**全部**卸下還回背包，玩家自己重鑲。
  **動作前會先確認背包空位夠**，不夠就整個不做（避免拆到一半珠子沒地方放而消失）。
- 版面用「方案 B」：按鈕寬 340，中心 x = 290 / 768 / 1246（底圖原生 1536 寬）。

### 珠子的強化不在鍛造介面

之後做「**鑲嵌珠強化石**」道具：在背包裡對強化石按右鍵 → 對珠子按左鍵 → 該珠升一級。
**只能對背包裡未滿 Lv3 的珠子使用**，不能對已經鑲在裝備上的珠子用。

---

## 6.5 圖示：珠子是「兩層疊合」

一般道具是一張圖（`ItemData.Icon`），但**能力珠是兩層**：

```
底層 珠身   UI/Icons/Items/gems/gemBase/gemBase_<color>_lv<1~3>.png   ← 依等級不同外型
上層 符號   UI/Icons/Items/gems/gemIcons/gemIcon_<icon>.png           ← 一種能力一張
```

8 種能力 × 3 個等級只要 **3 + 8 = 11 張圖**，不是 24 張成品圖；加新能力珠只要多一張符號圖。
所以珠子那幾列的 `ItemTable.IconPath` **是空的**，路徑由 `GemTable` 的 `Icon` / `BaseColor` 兩欄組出來。

`BaseColor` 留空會**自動推導**：`Target = Recipe/Weapon` → `red`（技能珠）、`Target = Player` → `blue`（屬性珠）。
想把屬性珠再細分（藍=生命、黃=防禦）才需要在表裡明寫。

### ⚠ 鐵則：畫物品圖示一律走 `ItemIcons`

`Assets/Scripts/UI/ItemIcons.cs` 是唯一入口，uGUI 與世界端（地上掉落物）各有一個多載。
**不要再直接讀 `data.Icon`** ——那個地方的珠子會變成一顆看不出是什麼能力的空白珠子，而且不會報錯。
2026-08-07 起 `ItemIcons.Apply` 裡還多做一件事：**依不透明內容把 icon 大小正規化**（`UI/IconFit.cs`），所以繞過它的地方連大小都會不一致。

已接上的地方：背包格／裝備欄、倉庫格、鍛造的鐵砧與孔位、過關結算獎勵、抽選面板（4 處）、地上掉落物。

### ArtSpec：疊圖的位置與大小（換素材要重量）

量自 2026-08-04 的素材（皆 500×500）。常數在 `ItemIcons.cs` 最上方：

| 項目 | 值 | 說明 |
|---|---|---|
| 符號大小 | 圖寬的 **55%** | 原圖符號 428px 寬、比珠子 325px 還寬，直接疊會蓋掉整顆珠子並突出邊界 |
| lv1 上移 | 8 / 500 | 三級的**紅球中心高度不一樣**——符號要對齊紅球，不是對齊畫布 |
| lv2 上移 | **24 / 500** | lv2 底下多了底座，把球往上推最多 |
| lv3 上移 | 14 / 500 | lv3 上下都有金框 |

左右不需要偏移。（8 張符號的內容中心平均比畫布中心右偏約 10px、最多 16px，
換算到 80px 的背包格只有 1.4px，看不出來，所以刻意不做每張的偏移設定。）

三級的珠身：lv1 純光珠、lv2 加底座、lv3 加完整環繞金框 —— 80px 的背包格裡仍分得出來。

### 目前的素材（2026-08-04 全部到齊）

珠身 `gemBase/`：`gemBase_red_lv1` / `_lv2` / `_lv3`（500×500）
符號 `gemIcons/`（皆 500×500，已驗證 55% 在三個等級都不爆框）：

| GemID | 珠子 | `Icon` 欄 | 檔名 |
|---:|---|---|---|
| 1 | 反彈珠 | `bounce` | `gemIcon_bounce.png` |
| 2 | 穿透珠 | `pierce` | `gemIcon_pierce.png` |
| 3 | 迅捷珠 | `speed` | `gemIcon_speed.png` |
| 4 | 銳利珠 | `damage` | `gemIcon_damage.png` |
| 5 | 追蹤珠 | `homing` | `gemIcon_homing.png` |
| 6 | 分裂珠 | `split` | `gemIcon_split.png` |
| 7 | 巨彈珠 | `scale` | `gemIcon_scale.png` |
| 8 | 疾發珠 | `rapid` | `gemIcon_rapid.png` |

**加一種新的能力珠**：畫一張 `gemIcon_<key>.png` 丟進 `gemIcons/` → `GemTable.csv` 加一列（`Icon` 填 `<key>`）
→ `ItemTable.csv` 加一列（`GemID` 指過去、`IconPath` **留空**）。零程式改動。
珠身不用另外畫——三張 base 是所有紅珠共用的。

⚠ 素材是 500×500 未壓縮（每張約 1MB）。丟新圖進來後跑一次
`Project Tools → 素材 → UI 貼圖設定：套用建議值`，會縮到 256（見 readme/PERF_QUALITY_AUDIT.md）。

---

## 7. 存檔

- `GridSlotDTO` 加 `inst`（巢狀物件）。
- `InventoryDTO` 加 `equipmentInst`（slot 名 → 實例）。
  刻意與既有的 `equipment`（slot 名 → itemId）**分成兩個欄位**而不是改它的型別——
  這樣既有的讀取端（例如存檔選擇畫面只想知道「裝了哪把武器」好畫 icon）完全不用改。
- 輪迴帶物（`ReincarnateInPlace`）新增一個吃 `ItemStack` 的多載，孔位與鑲嵌會原封不動帶到下一輪。
  只給 itemId 的舊多載會先從背包/裝備欄找出「真的那一件」再帶過去。
- `RestoreState` 會做 `NormalizeInstance`：需要實例卻缺 → 現場補一份；不需要卻有 → 丟掉；孔位數量對不上 → 補齊。
  所以**舊存檔讀進來不會炸**，只是那些裝備會補骰一次孔位。

---

## 8. 檔案地圖

| 檔案 | 角色 |
|---|---|
| `Inventory/ItemInstance.cs` | 「這一件」的資料（等級 / 孔位 / 鑲的珠子）。**之後要加裝備屬性就加在這裡** |
| `Inventory/GemData.cs` | `GemData` ＋ `GemDatabase`（讀 GemTable.csv） |
| `Inventory/GemTableProvider.cs` | 把 CSV 拖進場景提供給 GemDatabase |
| `Inventory/ItemManager.cs` | **產生一件物品的唯一工廠**（骰孔位 / 骰等級 / 交給玩家） |
| `Data/RandomRules.cs` | **全遊戲所有機率的唯一來源** |
| `Weapon/PlayerAbilities.cs` | **能力容器**：累加所有來源、深拷貝配方後套用 |
| `Inventory/ForgeSockets.cs` | 孔位查詢（含開發用 `DebugCount`） |
| `Inventory/ForgeSocketGrid.cs` | 鍛造介面 6 個孔（直接讀寫裝備實例） |
| `Weapon/RecipeManager.cs` | 新增 `RecipeEntry.Clone()` / `ClonePd()` |
| `Weapon/WeaponManager.cs` | 新增 `AbilityResolver` 掛勾與 `RefreshLoadout()` |

---

## 9. 怎麼測

1. 按 **L** 開作弊面板 → 「給道具」給自己一把武器（例：ID 1）→ 裝備它。
2. 切到 **「鑲嵌」分頁** →「重開孔位」填 6 →按下去（武器變成 6 孔，位置隨機）。
3. 同一頁按「每種能力珠各給一顆（Lv3）」。
4. 按 **Y** 開鍛造 → 把武器從背包拖上鐵砧 → 孔位亮起來 → 把珠子拖進去。
5. 關掉面板去打怪，能力應該立刻生效（不用重新裝備）。
6. 「給測試護身符＋戒指」→ 裝備 → 鑲珠子 → 驗證**跨裝備疊加**（珠子鑲在護身符上，武器一樣吃得到）。
7. 背包按重整鈕，確認鑲嵌**沒有被洗掉**（這是最容易出問題的地方）。

開發用捷徑：`Dipan.Inventory.ForgeSockets.DebugCount = 6;` 可以無視實際孔位強制開六孔。

---

## 10. 已知缺口 / 待辦

- [ ] **數值上限**尚未討論（見 §4 的安全夾值——目前只擋「會弄壞遊戲」的下限）
- [ ] 「強化裝備」按鈕未接功能
- [ ] 「拆除裝備」按鈕未接功能（語意待定義：分解成材料？產出珠子？）
- [ ] 「鑲嵌珠強化石」道具未做
- [ ] 武器/裝備的**內建能力等級**欄位未做（目前武器的基礎值直接來自配方表；`PlayerAbilities.Rebuild` 已留掛點）
- [ ] `GemTarget.Player`（角色屬性）只累加、還沒有人套用效果
- [x] ~~珠子的 icon~~ 8 種符號 + 3 張珠身已於 2026-08-04 全數到齊
- [ ] 501/502 測試防具的 icon 仍是借用既有武器圖（等正式素材）
- [ ] 只有紅珠（技能珠）；`Target=Player` 的屬性珠要用的藍珠珠身尚未畫
- [ ] 抽選面板顯示珠子時等級固定 Lv1（面板只拿得到 itemId，實際等級是發獎當下才骰）
- [ ] 抽選還沒有「寶石池」（加法見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md) §8，零程式碼）

*2026-08-07 更新：`ItemIcons.Apply` 加上 `IconFit` 正規化（依不透明內容把 icon 縮放到塞滿格子，見 [INVENTORY.md](INVENTORY.md)）。**珠子的疊圖比例不受影響**——符號的大小與偏移都是以底圖的 rect 為基準算的（符號 = 整張圖的 55%），底圖被放大時符號同倍率跟著放大，疊合關係完全不變；刻意**不**單獨正規化符號，那樣反而會破壞這個比例。實際效果是三個等級的珠身現在會一樣大（原本 lv1/lv2/lv3 的內容佔比是 65%/71%/88%，畫出來大小不一）。另外背包分成裝備包/消耗品包後，**珠子歸消耗品包**（不可裝備），鍛造「移除鑲嵌」算空格要用 `FreeSlotCount(BagKind.Item)`。*
