# 祭壇抽選系統（GACHA）

玩家在邪佛廣場走到祭壇前按 **F** 開啟抽選面板，花錢抽一樣東西；老虎機表演完把獎品給他。
關卡打通後可以「解鎖新東西進池」，讓後面幾世抽得到前面抽不到的。

> 這份文件同時涵蓋：抽選池的資料結構、金錢改制、血統藥劑、兩種新 trigger、以及面板/表演的版面依據。
> 動抽選、血統、祭壇、金錢相關的東西前先讀這裡。

---

## 1. 拍板的設計規則（作者決定，別自己改）

| 規則 | 說明 |
|---|---|
| **金錢是獨立數字** | 不是背包道具。存 `stats.currency`，顯示在背包面板底部方孔錢右邊。詳見 §4 |
| **血統＝一次性消耗藥劑** | 喝下永久改變本世外型與數值，**本世只能喝一次**；之後拿到任何血統藥劑都不能喝 |
| **輪迴後回到人類外型** | 但血統藥劑是道具，可被輪迴帶物選中留到下一世再喝 |
| **關卡解鎖進池＝永久** | 跨輪迴保留 |
| **允許重複** | 抽到已擁有的東西不移除、不折算、不保底 |
| **單抽 ＋ 十連抽** | 血統池刻意關閉連抽（一次給十瓶藥劑不合理） |

設計上呼應 [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) 的 **power 軸**：抽選花的是資源（金錢），
跟 access 軸（能玩哪一關，永遠免費）分開。

---

## 2. 四層架構（為什麼大項可以隨時增刪）

**程式碼裡沒有任何一個地方寫死「武器 / 裝備 / 血統 / 道具」這些字。**
系統只認兩個抽象：「池」與「獎品是哪個 ItemTable id」。

```
① 池登記表  GachaPoolTable.csv     一列 = 一座祭壇 = 一個池
② 基本表    BaseXxxRoll.csv        一列 = 一個「一開始就抽得到」的東西
③ 解鎖清單  存檔 unlockedRollEntries   關卡打通後加進來的
④ 抽選服務  Dipan.Gacha.GachaService   組池 / 抽 / 扣錢 / 發獎（完全不碰 UI）

實際抽選池 = ②（過濾條件通過的） ∪ ③
```

- **新增一個大項** → 池登記表加一列 ＋ 做一張基本表 ＋ 把表拖進 `GachaTableProvider` ＋ 擺一座祭壇。零程式碼。
- **刪除一個大項** → 刪那一列就好。例如日後把「裝備」併進「武器」＝刪 `armor` 列、把內容貼進 `BaseWeaponRoll.csv`。

> ⚠ 刻意**不**把池種類做成 trigger 型別：編輯器的 `triggerTypes.json` 是「只補不刪」的
> （C# 的 `Defaults()` 才是權威，json 刪掉的內建型別下次啟動會被補回來），做成型別將來刪不乾淨。
> 放 CSV 就沒這問題。

### 2.1 `Assets/Data/GachaPoolTable.csv`

| 欄位 | 說明 |
|---|---|
| `PoolId` | 池代號（英文小寫、唯一）。地圖編輯器的 `openPanel` 用 `arg` 指定它 |
| `DisplayName` | 面板標題顯示用 |
| `BaseTable` | 基本表檔名（不含 `.csv`），要與拖進 provider 的 TextAsset 名稱一致 |
| `SlateSprite` | 這座祭壇對應的石板圖檔名（**僅供擺圖時對照，程式不讀**） |
| `CostSingle` | 單抽價格 |
| `CostMulti` | 連抽價格；**填 0 ＝ 這個池不開放連抽** |
| `MultiCount` | 連抽一次抽幾個（`CostMulti=0` 時忽略） |
| `CostItemId` | 用哪個道具付帳；留空＝銅錢 101。填 101 時走錢包，填其他則扣背包道具 |

目前四列：`weapon` / `armor` / `blood` / `item`。

### 2.2 `Assets/Data/BaseXxxRoll.csv`

| 欄位 | 說明 |
|---|---|
| `ItemId` | ItemTable 的物品 ID |
| `Weight` | 權重，留空＝1。數字越大越容易抽到 |
| `MinCycle` | 第幾周目起才進池；留空＝一開始就有 |
| `RequireFlag` | 旗標成立才進池；前綴 `!` 表否定。沿用觸發鏈的旗標語意（含 `永久:` 前綴） |

`#` 開頭的行是註解，會被略過。

> `BaseArmorRoll.csv` 目前是**空的**——ItemTable 還沒有任何防具。
> 祭壇仍可放、面板仍可開，只會顯示「這座祭壇還沒有可抽的東西」並把抽選鈕反灰。

### 2.3 provider（**新增大項時最容易漏的一步**）

三張靜態表都靠場景上的 `GachaTableProvider` 提供 TextAsset（與 `ItemTableProvider` /
`SceneFxTableProvider` 同套路，見 [Data 資料夾慣例](#) 與 [PROBLEMS.md](PROBLEMS.md) C3）：

- `poolTableCSV` ← `GachaPoolTable.csv`
- `rollTableCSVs[]` ← **所有** `BaseXxxRoll.csv`（一個大項一張，**新增大項記得把陣列長度 +1**）
- `bloodlineTableCSV` ← `BloodlineTable.csv`

漏拖不會報錯，只會在該池打開時顯示「還沒有可抽的東西」，Console 有一則指引警告。

---

## 3. 解鎖：關卡打通後把東西加進池

存在 `CharacterSave.unlockedRollEntries`（`Dictionary<poolId, List<itemId>>`，**放存檔頂層**，
`ReincarnateInPlace` 不清 → 跨輪迴保留）。

**寫入方式＝地圖編輯器的 `unlockRoll` 觸發**，不寫程式：

```
紅嫁衣 boss 死亡旗標 → watchFlag → next = unlockRoll(pool=blood, entry=302)
```

`entry` 可用 `|` 分隔一次解鎖多個。idempotent，重複觸發不會重複加。

API：`SaveManager.UnlockRollEntry / GetUnlockedRollEntries / IsRollEntryUnlocked`。

---

## 4. 金錢：從背包道具改成獨立數字（2026-07-28）

**改制前**：金錢＝可堆疊的背包道具 101 銅錢，會佔背包格（打久了整個背包塞滿銅錢）。
**改制後**：金錢是一個數字，顯示在背包面板底部方孔錢右邊的凹槽。

### 4.1 但掉落還是「當道具處理」

掉落物、按 F 撿、關卡臨時包**仍然把金錢當道具 101**——這樣才能沿用既有的掉落物、
拾取、「通關才落袋／死亡歸零」那一整套，不用為金錢另做一份。
**只有落袋那一刻才轉成數字**，攔截點只有兩個，都在 `RunProgress`：

| 位置 | 行為 |
|---|---|
| `GiveItem(101, n)`，**不在關卡**（廣場） | → `SaveManager.AddCurrency(n)` |
| `GiveItem(101, n)`，**在關卡** | → 照舊進臨時包（通關才落袋） |
| `SettleIntoBag()` 遇到 101 | → `AddCurrency`（所以背包滿也不會掉錢） |

因為 `RunProgress.GiveItem` 本來就是專案裡「取得物品的統一入口」，攔在那一層，
怪物掉落、觸發鏈 `giveItem`、抽選發獎全部自動適用。

### 4.2 安全網與遷移

`SaveManager.SweepMoneyIntoWallet()` 在**載入存檔時**把背包與倉庫裡殘留的 101 收成數字。
它同時是：① 舊存檔的一次性遷移；② 萬一哪個路徑漏攔，下次載入自動收乾淨。

其他補漏：
- `CheatPanel` 給道具改走 `RunProgress.GiveItem`（給 101 會自動變金額）
- `InteractionManager` 的 `toRealBag` 捷徑對金錢例外
- `StorageLauncher` 測試種子不再種銅錢

### 4.3 ItemTable 的 101 那一列要保留

圖示、名稱還要用（地上掉落物標籤、抽選面板的「銅錢不足」訊息、背包底部的錢幣顯示）。
**別刪**。

### 4.4 UI

- 背包面板底部：`InventoryPanel` 的 `MoneyCx/MoneyCy/MoneyW/MoneyH`（底圖像素座標）
- 即時更新靠 `SaveManager.OnCurrencyChanged` 事件——金錢變動**不會**觸發背包的 `OnChanged`
- ⚠ 底部那顆方孔錢本身是**整理背包**按鈕（既有行為），金額只是顯示在它右邊

---

## 5. 血統（一次性藥劑）

### 5.1 為什麼分兩張表

血統藥劑是 `ItemTable` 的一列（`Category=Bloodline`、新欄位 **`BloodlineID`**），
數值與技能放 **`Assets/Data/BloodlineTable.csv`**。

理由：血統的數值只有血統用得到，塞進 ItemTable 會讓其他 99% 的道具多出一堆空欄。
這是專案既有模式的複製（`ItemTable.WeaponID → WeaponTable`）。

`BloodlineTable.csv` 欄位：`Id, Key, DisplayName, SpriteFolder, MaxHpAdd, MoveSpeedMul,
OutgoingDamageBonusPercent, SkillId, Note`。血統要加什麼數值就在這張表加欄。

- `Id=1` 固定保留給「人類（初始外型）」
- ⚠ `SpriteFolder` 目前**全填 `Base`**，因為只有這一組素材。填了不存在的資料夾，
  角色會找不到外型圖**只剩影子**。各血統的 `idle/walk/dead` 幀做好、跑過 Sync 之後再改這欄。
- `SkillId` 是預留欄，**技能系統目前不存在**，填了只會印一則提示

### 5.2 「本世已定型」旗標放哪

存在 **`progress.flags["血統"]`**（值＝血統 Id），刻意放**周目層**——
`ReincarnateInPlace` 會換掉整個 `progress`，所以下一世自動回到未定型狀態，
**不需要任何額外的重置程式碼**。

由此長出一層策略：這一世已定型後還去抽血統池，抽到的藥劑這世喝不了，
但可以佔一個輪迴帶物名額留給下一世。

### 5.3 執行期

`BloodlineSystem`（常駐單例、自動生成、零接線）每幀比對「存檔裡的血統」與「已套用的血統」，
不一致才動作——所以不管存檔載入、換圖、玩家物件重建的順序如何，最後都會收斂到正確狀態。

套用內容：移速倍率、最大生命加減、傷害加成（**用加減差額**而非賦值，避免蓋掉別的來源）、
外型 `SetBloodline`。所有數值都從「第一次看到這個玩家時記下的原始值」算起，
反覆套用不會越疊越大。

喝藥入口：背包面板左鍵/右鍵點血統藥劑 → `ConfirmPopup` 確認 → `BloodlineSystem.TryDrink`。

---

## 6. 新增的觸發與條件（編輯器）

### 6.1 `openPanel` 開啟介面(按F)｜位置型

玩家走到格子上按 F 開啟指定 UI。**刻意做成通用的「開啟介面」而不是「祭壇」專用**，
之後商店、鐵匠、圖鑑都能共用同一種筆刷。

| 參數 | 說明 |
|---|---|
| `panelId` | 要開哪個面板。目前只有 `gacha` |
| `arg` | 傳給面板的參數。祭壇＝抽選池代號（`weapon`/`armor`/`blood`/`item`） |
| `tipName` | 選填。提示文字後綴，例填「抽選」→ 顯示「按 F 鍵抽選」 |

`panelId` → 面板的分派寫在 `InteractionManager.OpenPanelPoint` 的 switch，加新面板就加一個 case。

> ⚠ **祭壇本身是地上物**（`walkable=false` 會擋路），所以感應格一定要包含**祭壇前方站得到的地板**，
> 否則玩家走不進感應範圍——與儲藏室藥水櫃同一個坑（見 [STOREROOM_POTION_TUTORIAL.md](STOREROOM_POTION_TUTORIAL.md)）。
>
> ⚠ 這個 trigger **不呼叫 `TriggerChain.OnCompleted`**（同 `portal`），所以它的 `next` 不會觸發。
> 也刻意**不消耗**互動點——祭壇本來就是可以一直回來抽的。

### 6.2 `unlockRoll` 解鎖抽選內容(鏈動作)

參數 `pool`（池代號）＋ `entry`（物品 id，`|` 分隔可多筆）。見 §3。

### 6.3 通用條件：最低／最高完成關卡數

加在**所有** trigger 都有的「條件」區（`TriggerTypeSet.ChainParams`，寫死在 C#、不進 json）：

| 欄位 | 說明 |
|---|---|
| `requireClearsMin` | 最低完成關卡數：完成數 ≥ 此值才可觸發 |
| `requireClearsMax` | 最高完成關卡數：完成數 ≤ 此值才可觸發。**填 0 ＝只在「一關都還沒通」時成立** |
| `requireClearsScope` | `cycle`（本周目，預設）／`lifetime`（跨輪迴曾達到的最高值） |

**與地上物的「出現條件（完成 N 關）」刻意做成同一組語意與同一組範圍值**
（見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md) 的 `appearAfterClears` / `appearScope`），
這樣「祭壇的圖」與「祭壇的按 F 感應區」可以填一模一樣的條件，
不會出現「圖還沒出來但走過去按得到」。

### 6.4 通用欄位：`onBlocked` 條件不成立時

`中止整條鏈`（預設，維持原本行為）／`跳過這顆繼續`（不執行自己、也不寫 `setFlag`，
直接把棒子交給自己的 `next`）。

**條件卡在鏈中間的節點一定要選「跳過這顆繼續」**，否則後面該做的事會被一起吃掉。
典型案例：邪佛廣場的 `camZone 邪佛全貌 → drama 邪佛對話 → selectScript 讓玩家選擇劇本`，
想讓初次對話只播一次而擋掉它時，若選「中止整條鏈」，玩家回廣場就**拿不到下一張劇本、直接卡死**。

---

## 7. 介面與表演

### 7.1 素材

```
Resources/UI/GachaPanel/GachaPanel_Bg           整個面板最底層的殿堂背景（機台就站在它的地磚上）
Resources/UI/GachaPanel/GachaPanel_GachaBg      機台本體（含內窗、層板、兩側滾筒、底座）
Resources/UI/GachaPanel/GachaPanel_TitleBg      頂部標題橫幅（放池名）
Resources/UI/GachaPanel/GachaPanel_CoinBg       金錢橫條
Resources/UI/GachaPanel/GachaPanel_SelectFrame  中選欄位（金框 + 兩側蓮花旋鈕）
Resources/UI/GachaPanel/GachaPanel_StartIcon    抽選鈕上的法輪圖示
Resources/UI/Common/GachaPanel_StartBtn         抽選鈕底板
Resources/UI/ClearStagePanel/*                  十連結算沿用過關結算那套框與牌匾
```

### 7.2 ⚠ 素材是「整張畫布輸出」的——內容框補償

這批圖的圖案只佔畫布中間一塊，四周是大片透明（例：中選框的圖案只有 852×299，
卻存在 1536×1024 的畫布裡）。**直接把整張圖塞進想要的方框，Unity 會拿整張畫布去對齊，
結果圖案會又小又偏。**

`GachaPanel` 裡有一張 `ArtSpec` 表記下每張圖「不透明內容」在畫布中的邊界框，
由 `PlaceArt()` 反推該給多大的方框、要偏移多少，讓**內容**剛好落在指定位置與尺寸。

> **重新輸出圖檔之後**：數字會對不上，但**不會靜默出錯**——`LoadArt()` 會比對圖檔尺寸，
> 一旦和表裡記的 `fullW/fullH` 不同就印警告叫你重量。
> 重量方法：用任何影像工具看不透明像素的邊界框（左, 上, 寬, 高）。

順帶記一筆：這幾張都是 1536×1024（實際內容小很多），貼圖記憶體約是需要的 4 倍，
之後有空可以裁掉透明邊（裁完只要更新那張表的六個數字）。見 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md)。

**唯一的例外是殿堂背景 `GachaPanel_Bg`**：它是實心 RGB、整張都是內容，沒有透明邊可補償，
所以它的邊界框就是整張畫布（`0,0,1448,1086`）。仍然走 `ArtSpec` 只是為了沿用 `LoadArt` 的尺寸檢查。

### 7.25 殿堂背景怎麼擺（4:3 的圖塞進 16:9 的畫布）

背景圖是 **1448×1086（4:3）**，畫布是 16:9，而且四邊都是實心的——三種擺法只能選一種：

| 做法 | 結果 |
|---|---|
| 橫向拉滿 | 兩側的菩薩浮雕被拉寬 33%，一眼看得出來變形 |
| 等比放大到蓋滿寬度 | 高度變 1440、上下各切掉 180 → 頂端的鈴鐺垂鏈與底部的地磚蓮燈全沒了 |
| **等比對齊高度、兩側留邊** ← 採用 | 構圖完整保留，兩側各空 240 |

選第三個是因為這張圖**上下都有東西**（頂端垂鏈、底部地磚與蓮花燈），切掉哪一邊都可惜；
而它的**左右邊緣本來就幾乎全黑**（量到 RGB 約 6~7/255），只要把底下那層純色底調成同一個黑
（`HallEdge = (0.027, 0.026, 0.028, 1)`，取自圖檔最外緣的平均色），接縫就看不出來，
視覺上仍然是「整片牆鋪滿螢幕」。

> ⚠️ 那層純色底**必須不透明**。原本是 `alpha 0.97`（讓遊戲畫面隱約透一點），
> 現在中間被背景圖蓋住、只剩兩側會透——會變成「兩條隱約看得到場景的直帶」，反而更明顯。

`HallH = 1080`（＝畫布參考高度）是唯一要調的數字；寬度由 `ArtHall` 的比例自己算。

### 7.3 版面以機台圖為錨

機台底圖上畫了**內窗**與**層板橫線**，直欄的格子必須剛好落在層板上，否則道具會浮在橫線上。
所以版面不是隨便量的：

| 常數 | 值 | 意義 |
|---|---|---|
| `WinCenterX` / `WinWidth` | 0.50 / 0.28 | 內窗中心與寬度（佔機台圖寬） |
| `WinTop` / `WinBottom` | 0.19 / 0.73 | 內窗上下緣（佔機台圖高） |
| `ShelfPitch` | 0.10 | 層板間距（佔機台圖高）＝一格的間距 |
| `MachineH` | 840 | 機台高（畫布 1080 的 78%） |

**中選框固定在畫面正中央（y=0），機台再依內窗中心反推位置**——這樣不管機台多大，
「被選中的那一格」永遠在畫面正中間。換機台圖只要重量上面那幾個比例，其他東西會自己跟著對齊。

### 7.4 表演

**結果先算、表演後演**：按下抽選的當下 `GachaService.Roll` 就把錢扣掉、結果算完、獎品也發到手了。
中間那段滾動純粹是把已知結果演出來——所以 **skip 跟不 skip 拿到的東西完全一樣**，
表演途中把面板關掉也不會弄丟東西。

- **待機**：直欄緩慢往下滾（看起來是活的）
- **抽選**：高速滾動 → ease-out 減速、一格一格穿過中選欄位 → 停在結果那格
- **中選框抖動**：表演中上下抖動模仿機器運轉（兩個不同頻率的正弦疊加，不會像節拍器；`ShakeAmp`/`ShakeFreq`）
- **落定**：舞台壓暗 → 道具放大旋轉後定位 → 下方顯示名稱（帶描邊）
- **Skip**：直接跳到結果，不做放大旋轉

**落點怎麼保證正確**：減速階段總共再走 `DecelCells`(12) 格，第 `DecelCells - CenterIndex` 次
補進最上面的那一格，走完後剛好落在中選欄位——所以把 result 排在那一次注入即可。
`Advance()` 是「先 `wraps++` 再取內容」，判定用的是遞增後的值。

> `_offset` 是「已經往下走了多少」，uGUI 的 +y 是往上，所以 `ApplyOffset()` 要**取負號**才是往下滾。
> 正號會變成往上滾，而且每次跨格時內容會往回跳一格（看起來像抽格）。

### 7.5 中獎特寫的「舞台」

放大的道具圖如果直接疊在還亮著的直欄上，會讓人分不清是「中獎特寫」還是「圖跑版」。
所以中獎時在**內窗範圍**鋪一層壓暗（`StageDimAlpha` 0.8）當舞台，背後的格子退下去。
壓暗層建在**中選框之前** → 金框與蓮花旋鈕仍然亮著。

放大圖框刻意做成「寬度略小於內窗、高度可以超出金框」：細長的矛/劍靠高度撐滿
（超出金框沒關係，背景已經暗了，讀起來是「衝出來」而不是破圖），
寬扁的東西則被寬度擋住，不會頂到兩側柱子。

### 7.6 十連結算

十連的滾動太快，玩家記不住抽到什麼（按跳過更是完全沒看到），所以**抽完統一跳一張清單**，
美術沿用過關結算那套（框／「獲得獎勵」牌匾／按鈕）。

- **相同的東西會合併**成「名稱 ×N」
- 排版跟著實際**種類數**走：5 種以內排一排（格子放大）、6 種以上排兩排 → 10 格全滿與只中 3 種都置中對稱
- **跳過也一定會跳結算**（結算是在整段表演跑完之後才叫的）
- 底下鋪一層全螢幕遮罩擋住抽選鈕，必須按「確定」才回到機台

### 7.7 其他面板行為

- `CloseOnEscape = false`：**ESC 不關**。中途被 ESC 關掉會讓表演斷在一半（東西其實已經到手但玩家不知道），
  十連結算也會跟著消失。一律要按「離開」
- 有連抽的池顯示兩顆抽選鈕（單抽／十連並排），沒有連抽的池一顆置中
- 表演中兩顆抽選鈕換成一顆「跳過」，佔同一個位置（不用另外做圖）
- 抽選鈕本體是一張**全透明**的 Image 只負責接點擊，尺寸等於底板圖案的可見範圍——
  這樣點擊區才不會被圖檔那圈透明邊撐大（`StartBtn` 上下各有 16% 透明邊）

---

## 8. 怎麼加一座新祭壇（清單）

1. `GachaPoolTable.csv` 加一列（`PoolId` / 價格 / `BaseTable` 名）
2. 做 `BaseXxxRoll.csv`，填 ItemTable id 與權重
3. 把新表拖進場景 `GachaTableProvider` 的 `rollTableCSVs` 陣列（**陣列長度 +1**）
4. 地圖編輯器：擺祭壇地上物（本體 + 石板），在**祭壇前方可站的地板**畫一塊 `openPanel`
   - 面板 = `gacha`、參數 = 你的 `PoolId`
   - 要「通關 N 關後才開放」就填最低完成關卡數，並讓地上物的出現條件填一樣的數字
5. 存檔 → 主專案跑 `Project Tools → Sync Map Assets`

---

## 9. 怎麼測

- 測試選單 `Project Tools/測試/直接進關卡/**邪佛廣場-1關後**`
  ——建一個全新測試角色並**預先標記紅嫁衣通關**（完成關卡數＝1），
  讓「通關 1 關後才出現」的祭壇直接看得到，不用真的把關卡打完
  （對照組是「邪佛廣場-初始」＝完成 0 關）
- 錢：按 **L** 開作弊面板 →「一鍵快捷」區的「獲得 10,000 元」
  （直接加金錢數字，不走背包/臨時包；在關卡裡按會立刻入帳，與正常掉落行為不同）

---

## 10. 已知取捨 / 待補

- 面板字串目前是 `GachaPanel` 上方的 `const string`，正式多語系時改走 `Language.GetText`（見 [LOCALIZATION.md](LOCALIZATION.md)）
- `BaseArmorRoll.csv` 是空的（ItemTable 沒有防具）
- 血統的 `SkillId` 欄留著但技能系統不存在
- 血統的 `SpriteFolder` 全是 `Base`（等各血統素材）
- `rockSlate_blood` / `rockSlate_item` 兩張石板圖還不存在（目前只有 weapon / armor / script）
- 血統藥劑的 icon 暫時借用回血/回魔瓶的圖
- 抽選面板素材有大量透明邊，貼圖記憶體約是需要的 4 倍（見 §7.2）
- 殿堂背景是 4:3，16:9 畫布下兩側各留 240 由同色底補（見 §7.25）；日後若想真正鋪滿，要嘛請美術出一張 16:9 的，要嘛把左右兩塊做成九宮格拉伸
- `openPanel` 的 `next` 不會觸發（見 §6.1）

---

## 11. 這條線踩過的坑

| 代號 | 症狀 |
|---|---|
| [PROBLEMS.md](PROBLEMS.md) C9 | 編輯器的循環按鈕（options 欄）**空值時顯示第一個選項但沒寫進去**，害整組祭壇失效 |
| [PROBLEMS.md](PROBLEMS.md) D10 | `RectTransform` 沒有 `SetActive`（要 `.gameObject.SetActive`）——括號配對檢查抓不到型別錯誤 |
| [PROBLEMS.md](PROBLEMS.md) E9 | AI 產的 UI 素材是整張畫布輸出，直接用會又小又偏 |
| [PROBLEMS.md](PROBLEMS.md) K2 | 條件擋在觸發鏈中間的節點，會把後面該做的事一起吃掉 |
