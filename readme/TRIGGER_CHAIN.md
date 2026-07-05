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

| 欄位 | 意義 |
|---|---|
| `next` | 完成後啟動的 trigger（名稱優先，找不到再比對 id；同名取第一個，**建議不要重名**） |
| `startDisabled` | 初始停用。停用中＝踩到/按 F 無反應、星星不顯示、綠幕隱藏 |
| `enableFlag` | 「已解鎖」的存檔旗標名：被鏈解鎖時自動寫 1；**重進地圖/讀檔後旗標成立就自動啟用**（門開了就一直開） |
| `requireFlag` | 旗標成立才可觸發/執行；前綴 `!` 表否定（`!killedFamily` = 沒殺過家人才觸發） |
| `setFlag` | 動作完成後寫 1 的旗標名（給別的 trigger 的 requireFlag 用） |
| `linkedFx`（teleport 專用） | 場景特效 id（編輯器場景特效面板可複製）：trigger 停用時特效隱藏、解鎖時顯示（傳送門綠幕） |

旗標存進角色存檔 `progress.flags`（跨存讀檔、跨地圖），SaveManager.GetFlag/SetFlag。

## 3. 新增的動作型類型

- **`giveItem` 給予物品(鏈動作)**：`itemId`、`count`（留空=1）。直接進背包＋中央 toast「獲得 XX」，裝不下的掉玩家腳下。**不用按 F**。
- **`teleportTo` 直接傳送(鏈動作)**：`targetMapId`、`targetEntrance`。直接換圖。**不用踩傳送點**。
  ⚠️ 換圖 = 鏈的終點（setFlag 會先寫掉，next 填了也不會執行）。cutscene 同理。

## 4. 範例編排

**邪佛大廳（✅ 已實裝於 Main_Square，2026-07-05）：看全貌 → 對話 → 給劇本 → 傳送門開**
| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `邪佛全貌` | camZone | zoom=1.8, offsetY=8, next=`邪佛對話`（鏡頭拉伸**到位**才觸發） |
| `邪佛對話` | drama（cells=0 純鏈節點） | dramaId=3, requireFlag=`!hallGateOpen`（門開過不再觸發＝防重複拿劇本）, next=`給紅嫁衣劇本` |
| `給紅嫁衣劇本` | giveItem（角落 1 格） | itemId=104(劇本-紅嫁衣), count=1, next=`劇本開門` |
| `劇本開門` | teleport（門口） | startDisabled=✓, enableFlag=`hallGateOpen`, linkedFx=`2d656e16`(綠幕), targetMapId=1 |

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
