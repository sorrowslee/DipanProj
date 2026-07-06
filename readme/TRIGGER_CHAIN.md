# 觸發鏈（Trigger 接 Trigger）

> 返回 [文件總覽](README.md) ｜ 主遊戲端：`Assets/Scripts/Map/TriggerChain.cs` ｜ 編輯器端：trigger 面板的「觸發鏈/條件（通用）」欄位組

任何 trigger 完成後可以自動啟動另一個 trigger，接成任意長的鏈：
**對話 → 收到物品 → 傳送門開啟**、**對話 → 直接傳送**、對話 → 對話 → …（無限層）。

## 1. 核心概念

每個 trigger（任何類型）都多了通用欄位 **`next`**：填**同地圖**另一個 trigger 的**名稱**（或 id）。
本 trigger 的動作「完成」時（對話面板關閉、物品入包、拾取完成…），自動啟動 next 指向的 trigger。

被 next 啟動時的語意依目標類型分兩種：

| 目標類型 | 被鏈啟動時 |
|---|---|
| **動作型** `giveItem`／`teleportTo`／`drama` | **立即執行**（給物品／直接傳送／立刻播對話） |
| **位置型** `teleport`／`pickup`／`cutscene` | **解鎖**（配合 `startDisabled`；之後玩家踩到/按 F 才生效） |

**camZone 也能當鏈的起點**：它的「完成」= 玩家踩進區域、**鏡頭拉伸到位的瞬間** → 觸發 next。
（zoom 留空 = 立即到位，等於「純踩踏就觸發鏈」的隱形區域。每次進區都會觸發一次，
一次性請在鏈的下一節點掛 requireFlag，例：邪佛全貌 camZone → next=邪佛對話(requireFlag=`!hallGateOpen`)。）

動作型 trigger 不綁位置——編輯器仍要塗格（區域資料需要格子），**畫在地圖角落即可**，玩家踩到也不會有反應，只有被鏈啟動才執行。

## 2. 通用欄位（全部選填，每種類型都有）

編輯器面板把這些欄位分成四個小節（括號內為編輯器上的中文標籤）：

**條件（可不可以觸發，全部以 AND 結算）**

| 欄位 | 編輯器標籤 | 意義 |
|---|---|---|
| `requireFlag` | 條件旗標 | 旗標成立才可觸發。**從旗標登記表下拉選**（不手打）＋「有/沒有」切換（沒有＝存成 `!名字`）。生命週期由登記表決定，見 §2.5 |
| `requireCycleMax` | 周目上限 | 周目（`SaveManager.Cycle`）**≤** 此值才成立。**初始限定填 `1`**（只第 1 周目、老手不再觸發）。留空＝不限 |
| `requireCycleMin` | 周目下限 | 周目 **≥** 此值才成立（做「玩了好幾輪才出現」的對話）。留空＝不限 |
| `requireItem` | 道具條件 | 背包道具條件（只算背包格、不含裝備欄）：填 `itemId`＝**須有**此道具；前綴 `!`（如 `!104`）＝**須無**此道具。留空＝不檢查 |

**一次性（會不會重複觸發）**

| 欄位 | 編輯器標籤 | 意義 |
|---|---|---|
| `repeat` | 重複規則 | 用循環按鈕切換：`每次進場`(預設)／`每次`／`每周目`／`永久`。見 §2.5。目前只作用在 drama／pickup 互動點 |

**流程（觸發後做什麼）**

| 欄位 | 編輯器標籤 | 意義 |
|---|---|---|
| `next` | 接續觸發 | 完成後啟動的 trigger（名稱優先，找不到再比對 id；**建議不要重名**） |
| `setFlag` | 完成寫旗標 | 動作完成後寫成立的旗標（給別的 trigger 的條件旗標用）。**從旗標登記表下拉選**，生命週期由登記表決定 |

**解鎖（位置型：等鏈解鎖）**

| 欄位 | 編輯器標籤 | 意義 |
|---|---|---|
| `startDisabled` | 初始停用 | 停用中＝踩到/按 F 無反應、星星不顯示、綠幕隱藏 |
| `enableFlag` | 解鎖旗標 | 「已解鎖」旗標：被鏈解鎖時自動寫成立；重進地圖/讀檔後旗標成立就自動啟用（門開了就一直開）。**從旗標登記表下拉選** |
| `linkedFx`（teleport 專用） | 連動特效id | 場景特效 id：trigger 停用時特效隱藏、解鎖時顯示（傳送門綠幕） |

## 2.5 旗標與範圍：整套只有「一種旗標」，差在活多久

一句話：**所有會被記住的東西都是旗標；一個旗標＝一個名字＋一個生命週期**。生命週期兩種：

- **周目**：存 `progress.flags`，**輪迴（`ReincarnateInPlace`）會整包清空** → 下一周目重置。
- **永久**：存 `CharacterSave.lifetimeFlags`，**跨輪迴保存**，只有開新角色才空。

### 2.5.0 旗標管理器（作者實際怎麼用）

生命週期是**旗標自己的屬性**，集中在編輯器 trigger 頁籤的「**旗標**」按鈕 → 旗標管理器裡設定：一列一個旗標，含**系統自動配的 id**＋名稱＋周目/永久切換鈕＋刪除，可新增、可「自動匯入地圖旗標」（掃所有地圖把用到的旗標收編）、按「儲存」寫出 `flags.json`。

之後所有觸發點的「條件旗標／完成寫旗標／解鎖旗標」都用 **id 配置**（不手打名字、不會拼錯）：欄位是一個輸入框，**輸入旗標 id → 按「確認」**，系統就去登記表查該 id、把名稱填上並鎖定顯示（如 `hallGateOpen（周目）`）；此時旁邊出現「刪除」，按了就清空、又變回可輸入。**沒按確認、或查無該 id，就不會有名稱出現＝沒配置成功**（名字正確出現才算數）。條件旗標鎖定後另有「有/沒有」切換（沒有＝存成 `!名字`）。

**運作（方案乙，單一來源）**：地圖裡的觸發點**只存旗標裸名**（＋否定 `!`），不帶生命週期；生命週期只存在 `flags.json`。遊戲端 `FlagRegistry` 讀 `StreamingAssets/MapAssets/flags.json`，`TriggerChain.FlagTrue`/`SetFlag` 查表決定旗標存周目還是終身。所以你在管理器改一次某旗標的生命週期，**所有用到它的觸發點都一起改**。

> `flags.json` 的位置：編輯器授權檔在 `DipanProj_MapEditor/flags.json`；跑「刷新素材／同步腳本」會帶進 `StreamingAssets/MapAssets/flags.json` 給遊戲讀。改名/刪旗標後，已放好的觸發點不會自動更新，記得回去重選。

**旗標分兩種來源**：
- **具名旗標**（你在管理器建的）：只為了**跨 trigger 的劇情邏輯**（A 影響 B），用 `完成寫旗標` 寫、`條件旗標` 讀。
- **自動旗標**（系統用 trigger id 產生，作者看不到也不用命名）：撐起 `重複規則` 的「每周目一次／永久一次」，見 §2.5.1。內部用 `永久:` 前綴標記終身、不進管理器。

> 為什麼「只第 1 周目」用 `周目上限=1` 而不用旗標？因為周目 `Cycle` 天生跨輪迴持久，而周目旗標輪迴會被清。要「跨輪迴只一次」就把旗標設成**永久**，或用 `重複規則=永久`。
> 程式端 `FlagTrue`/`SetFlag`：名字帶 `永久:` 前綴（自動旗標/相容）→ 終身；否則查登記表。無存檔的單場景測試退回記憶體，行為一致但不持久。

### 2.5.1 重複規則（`repeat`）四種模式

決定同一個互動點多久能再觸發一次。四種模式其實就是「同一個自動旗標、不同生命範圍」：

| 編輯器值 | 何時會再觸發 | 機制 |
|---|---|---|
| `每次進場`（預設） | 每次重新進這張地圖 | 當次停留消耗（記憶體），離圖重進復活 |
| `每次` | 每次踩/按都觸發 | 不消耗；自動觸發型（碰到就播）加「離開半徑才重新武裝」避免每幀洗版 |
| `每周目` | 下一個輪迴 | 觸發後寫**周目**自動旗標；建點時已觸發就不現身。輪迴清 → 下周目再觸發 |
| `永久` | 永不（除非開新角色） | 觸發後寫**終身**自動旗標（跨輪迴保存） |

> 「每周目一次」也可以用 `條件旗標=!x + 完成寫旗標=x` 手動做到（周目旗標輪迴會清），但用 `重複規則=每周目` 更省——不必自己命名旗標。`條件旗標`/`完成寫旗標` 建議只留給跨 trigger 的劇情條件。

## 3. 新增的動作型類型

- **`giveItem` 給予物品(鏈動作)**：`itemId`、`count`（留空=1）。直接進背包＋中央 toast「獲得 XX」，裝不下的掉玩家腳下。**不用按 F**。
- **`teleportTo` 直接傳送(鏈動作)**：`targetMapId`、`targetEntrance`。直接換圖。**不用踩傳送點**。
  ⚠️ 換圖 = 鏈的終點（setFlag 會先寫掉，next 填了也不會執行）。cutscene 同理。

## 4. 範例編排

**邪佛大廳（✅ 已實裝於 Main_Square；條件於 2026-07-06 改為周目＋背包判定）：看全貌 → 對話 → 給劇本 → 傳送門開**
| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `邪佛全貌` | camZone | zoom=1.8, offsetY=8, next=`邪佛對話`（鏡頭拉伸**到位**才觸發） |
| `邪佛對話` | drama（cells=0 純鏈節點） | dramaId=3, **requireCycleMax=1**（只第 1 周目）, **requireItem=!104**（背包已有劇本＝談過了，不再播）, next=`給紅嫁衣劇本` |
| `給紅嫁衣劇本` | giveItem（角落 1 格） | itemId=104(劇本-紅嫁衣), count=1, **requireCycleMax=1**, **requireItem=!104**（初始進度＋背包沒劇本才給）, next=`劇本開門` |
| `劇本開門` | teleport（門口） | startDisabled=✓, enableFlag=`hallGateOpen`, linkedFx=`2d656e16`(綠幕), targetMapId=1 |

> **為何從 `!hallGateOpen` 改成周目＋背包**：旗標在輪迴（`ReincarnateInPlace`）會被清空，所以 `!hallGateOpen` 只能擋「同一周目內」重複；第 2 周目旗標被清 → 對話與給劇本又會觸發。改用 `requireCycleMax=1`（跨輪迴持久）＋ `requireNoItem=104`（背包已有就不重給/不重播）後語意才正確：**只在第 1 周目、且尚未持有劇本時**才觸發。
>
> ⚠️ **連帶待辦（第 2 周目的門）**：給劇本被限在第 1 周目後，第 2 周目起這條鏈不跑、`劇本開門`(startDisabled) 不會被解鎖 → 老手進不了關卡。這塊會由**下一階段的「點傳送門→放置劇情道具 UI→開門」**接手（玩家自己把想去的關卡劇本放進傳送門即開），屆時 `劇本開門` 的解鎖改由「放入劇本」驅動，不再靠這條自動鏈。在該 UI 完成前，第 2 周目的門暫時無法開啟。

**紅嫁衣：沒殺家人 → 對話完傳送到榕樹妖（⏳ 未實作，步驟見 §7）**
| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `紅嫁衣對話` | drama | dramaId=`<新>`, requireFlag=`!killedFamily`, next=`送去榕樹妖` |
| `送去榕樹妖` | teleportTo（角落） | targetMapId=10（RedBridalGown_TreeDemon） |

## 5. 運作細節（改程式前讀）

- **完成時機**：pickup=入包後；drama=**面板關閉時**（DramaPanel/TalkPanel 的 OnClose → `TriggerChain.NotifyDramaClosed`）；giveItem=給完立即。
- **停用/條件的判定點**：teleport/cutscene 在 Watcher **每幀動態查** `TriggerChain.IsActive(region)`（解鎖瞬間生效、不必重建格表）；pickup/drama 互動點在**建點時過濾**，解鎖或旗標變動時由 `MapManager.RefreshTriggers()` → `InteractionManager.RebuildPoints()` 重建（已消耗的點不會復活，`_consumed` 集合記著）。
- **每次換圖** `MapManager.SetupWatcher()` 先 `TriggerChain.Setup(...)`：算 startDisabled/enableFlag 的初始啟用態、套 linkedFx 顯隱，**之後**才建各 watcher/互動點。
- 鏈到 drama 型 = 立即播對話（不管它在地圖哪裡、不需要玩家走過去）。
- 對話面板沒開成（DramaTable 缺資料）→ 關閉事件不會來 → 鏈停在該節點，Console 有警告。
- 無 SaveManager 時（單場景測試）旗標退回 session 記憶體，行為一致但不持久。

## 6. 待補 / 邊界

- 鏈只在**同地圖**內找目標；跨圖劇情請用旗標接力（前圖 setFlag → 後圖 requireFlag/enableFlag）。
- pickup/drama 的「一次性」仍是**當次停留記憶**（離圖重進會復活）；要永久一次性請用 requireFlag=`!xxx` + setFlag=`xxx` 的組合，或等地圖狀態持久化 Phase 2（見 [MAP_SYSTEM.md](MAP_SYSTEM.md)）。

## 7. 接手實作指南：紅嫁衣「沒殺家人」分支（⏳ 未實作）

> 目標劇情：玩家在紅嫁衣關卡若**沒有殺害她的家人**，與紅嫁衣對話完畢後傳送到榕樹妖地圖（MapsTable **10** = `RedBridalGown_TreeDemon`）。
> 鏈系統本身已完備，**唯一缺的程式是「殺家人 → 寫 killedFamily 旗標」的掛勾**，其餘全是填表和編輯器操作。

### 步驟 1：怪物死亡寫旗標（唯一要寫程式的部分）

「家人」= MonsterData.csv 的 8~12（Ghost_GrandMa / GrandPa / Mother / YoungBrother / YoungSister）。做法照專案慣例走資料驅動：

1. `MonsterData.csv` **表尾加一欄 `DeathFlag`**（死亡時要寫 1 的旗標名；空 = 無）。家人五列填 `killedFamily`，其他列留空。
2. `MonsterData` 資料類加 `DeathFlag` 欄位、CSV 解析器補讀該欄（空欄給 `""`，向下相容——參考既有 `AnimFPS` 欄的做法）。
3. `MonsterController.Die()`（`Assets/Scripts/AI/MonsterController.cs` 291 行附近）加：
   `if (!string.IsNullOrEmpty(Data.DeathFlag)) TriggerChain.SetFlag(Data.DeathFlag);`
   （`TriggerChain.SetFlag` 會自動走 SaveManager 進存檔、無存檔時退回記憶體；不用自己判斷。）

### 步驟 2：對話資料（填表）

1. `DramaTalkTable.csv`：新開一個群組（目前最大 Group=2，用 **3**），寫紅嫁衣的對話句。
   立繪已在 catalog：`Modules/RedBridalGown/Talk/redBridalGown`；主角側用 `Actor_<情緒>`。
   立繪大小/位置不對就用表尾六個微調欄（LeftScale/LeftOffsetX/…，見 [DRAMA.md](DRAMA.md)）。
2. `DramaTable.csv`：新增一列（目前最大 ID=3，用 **4**）：`4,,紅嫁衣對話,2,3`（Type=2 頭像對話、TalkGroup=3）。

### 步驟 3：編輯器放 trigger（紅嫁衣的最終房間地圖，哪張由作者決定）

1. `紅嫁衣對話`（drama）：塗在紅嫁衣 NPC 位置（Type=2 碰到自動觸發）。參數：
   `dramaId=4`、`requireFlag=!killedFamily`、`next=送去榕樹妖`。
2. `送去榕樹妖`（teleportTo，「直接傳送(鏈動作)」）：塗地圖角落 1 格即可。參數：
   `targetMapId=10`、`targetEntrance=`（空 = 落在目標圖 playerSpawn；要指定落點就在榕樹妖圖放 teleport 區填 entranceId，這裡填同名）。
3. 存檔後跑 `Tools/sync_map_assets.sh`（或 Project Tools → Sync Map Assets）把地圖帶進主遊戲。

### 步驟 4：測試

- 沒殺家人 → 碰紅嫁衣 → 對話 → 播完自動傳送到榕樹妖 ✓
- 先殺任一家人（Console 應出現「旗標 killedFamily = 1」）→ 碰紅嫁衣 → **毫無反應**（requireFlag 擋掉）✓
- 存檔重開後旗標仍在（progress.flags）✓

### 留給作者的設計決定（實作時再問）

- **殺了家人的分支**要做什麼？（另一段對話 `requireFlag=killedFamily`？直接開打？）目前規格只定了「沒殺」分支。
- `killedFamily` 是「殺**任一**家人」就成立（目前設計）；若要「全殺才算」需要另做計數，不在本期。
- 榕樹妖打完之後的流程（回廣場？MarkModuleCleared？）未定義。
