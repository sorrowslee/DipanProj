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

**邪佛大廳：對話 → 給劇本 → 傳送門開**
| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `邪佛對話` | drama（Type2 碰到自動觸發） | next=`給劇本` |
| `給劇本` | giveItem（角落） | itemId=紅嫁衣劇本, next=`開門` |
| `開門` | teleport（門口） | startDisabled=✓, enableFlag=`hallGateOpen`, linkedFx=`<綠幕id>`, targetMapId=… |

**紅嫁衣：沒殺家人 → 對話完傳送到榕樹妖**
| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `紅嫁衣對話` | drama | requireFlag=`!killedFamily`, next=`送去榕樹妖` |
| `送去榕樹妖` | teleportTo（角落） | targetMapId=10 |

（殺家人的行為由之後的系統寫 `killedFamily` 旗標：`TriggerChain.SetFlag("killedFamily")` 或任何 trigger 的 setFlag。）

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
