# 關卡進度（RunProgress）＋ 劇本消耗 ＋ 血球

> 返回 [文件總覽](README.md)
>
> **一句話**：一趟關卡內，**怪死了不復生、撿過的不再出現、地上沒撿的掉落物／血球留原地**；換地圖來回都記得，**完整離開關卡（回廣場／死亡）才整個重置**。
>
> ⚠️ **2026-11-06 作者改版：臨時包已拿掉**——關卡內撿到的東西**直接進真背包、死亡也保留**；**劇本在傳送門按下開門那一刻就消耗**（§10）；新增**血球**（§9）。舊的「通關才落袋、死亡歸零」已不存在，看到別處文件這樣寫就是過期了。
>
> 設計依據：[CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) §6。程式：`Assets/Scripts/Flow/RunProgress.cs`（2026-07-18 加入）。

---

## 1. 它解決什麼

原本的行為是「每次載圖都重建全部」——離開房間再回來，怪重生、拾取點復活、地上掉落物消失。這對「一趟關卡要在多張地圖間來回跑」的關卡結構完全不能用。

`RunProgress` 是**跨換圖常駐的單例**，把「這一趟關卡」的可變狀態集中記著，`MapLoader` / `InteractionManager` 載圖時據此**跳過重生、原地重放**。

> ⚠️ 這是 [MAP_SYSTEM.md](MAP_SYSTEM.md) §5 講的「Phase 2 地圖狀態持久化」的**實作**，但落點與當初草案不同：狀態庫**不是**掛在 `MapManager` 上的 `MapState`，而是獨立的 `RunProgress` 單例，且**只活在記憶體、不寫存檔**（見 §5）。

---

## 2. 三大職責

| 職責 | 內容 |
|---|---|
| **取得物品的統一入口** | `GiveItem`／`GiveStack`：**一律進真背包**（銅錢轉金錢數字），回傳放不下的剩餘（§6） |
| **關卡進度（per-map）** | 已清的怪出生點、已取／已觸發的 trigger、已破壞的地上物、還沒撿的地上掉落物、還沒吸的血球 |
| **劇本消耗** | 開新的一趟時從背包扣掉一份指向該關卡的劇本並存檔（§10） |
| **工程介面** | 按 **F8** 開／關除錯疊層，看目前 run 狀態與每張圖的清怪／掉落物／血球數 |

---

## 3. 生命週期：什麼時候重置

判斷「在不在關卡內」的唯一依據是 **module**：

```
RunProgress.IsLevelModule(module)  ==  module != SaveConstants.HubModule   // HubModule = "Main"
```

`Main` module（邪佛廣場、初始洞窟、開場山道）**不算關卡**——不記進度。其餘 module 一律視為關卡。

`MapManager` 每次**跨 module 換圖**時呼叫 `OnEnterModule(module)`：

| 情境 | 行為 |
|---|---|
| 進入關卡 module、且 `RunModule` 不同 | `BeginRun(module)` — 清空所有進度，開新的一趟 |
| 進入關卡 module、`RunModule` 相同 | 什麼都不做（**同一趟延續**） |
| **同 module 房間互跳** | 根本不呼叫（走 `MapManager` 的 else 分支）→ 進度完整延續 |
| 進入廣場／教學（Hub） | 若還有殘留就 `EndRun()` 保險清掉 |
| 過關／死亡／主動返回 | 一律 `EndRun()` — 只清進度，**不動背包**（撿到的早就在背包裡） |

> 所以玩家實際感受到的規則就是：**關卡內怎麼跑都記得；離開這一關再回來，全部重來。** 這正是 [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) §6 要的「關卡每趟重來（fresh reset）」。

---

## 4. 四種進度記錄與它們的穩定 key

穩定 ID 沒有另外做 GUID 系統，直接用「同一張地圖檔每次解析順序一致」這個性質產生：

| 記錄 | key 生成 | 誰記 | 誰讀 |
|---|---|---|---|
| `killedSpawns` 已清出生點 | `$"{區域id}#{格x},{格y}"` | `MonsterController.LateUpdate` | `MapLoader.SpawnMonstersFromMap` |
| `destroyedObjects` 已破壞地上物 | `$"obj#{objects 清單索引}"` | `DestructibleObject.Die` | `MapLoader.BuildOneObject` |
| `consumedTriggers` 已取／已觸發 | trigger 區域的 `id`（編輯器產生，本來就穩定） | `InteractionManager.ConsumePoint`、`TriggerChain` 的 `giveItem` | `InteractionManager.RebuildPoints`、`TriggerChain` |
| `drops` 地上掉落物 | `RunProgress` 自己發號的流水 `dropId` | `InteractionManager.DropLoot` | `InteractionManager.RestoreGroundDrops`（由 `MapManager` 在載圖後呼叫） |

| `orbs` 地上血球 | 與 `drops` 共用流水號 | `InteractionManager.DropBloodOrb` | `InteractionManager.RestoreGroundDrops`（同一支一起重放） |

**掉落物重放**用**原座標**、不重新散開、沿用原 `dropId`，所以換圖回來紅水還在原地那一格。部分撿取（背包吃不下全部）會 `UpdateGroundDropCount` 回寫剩餘數量。血球同理（原座標、原 id）。

### 什麼東西不記
- **召喚物**：`MonsterController` 只對「有 `SpawnKey` 的地圖出生敵怪」記進度與掉寶，召喚物沒有 `SpawnKey` → 不記、不掉寶（否則可無限刷）。
- **不在 run 期間**：所有 `Mark*` / `Register*` 在 `RunActive == false` 時**內部直接忽略**，呼叫端不必判斷。
- **`repeat=每次` 的觸發點**：`ConsumePoint` 對 `RepeatMode.Always` 不消耗，也就不會進 `consumedTriggers`。

---

## 5. 存活範圍：只在記憶體，不寫存檔

`RunProgress` 的所有內容都是**純記憶體**，關遊戲即消失，**不進 `character.json`**。這是刻意的，且與存檔設計一致：

- 撿到的東西是走 `InventorySystem` 進真背包，**那份**由 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) 存起來（dirty 自動存＋退出鉤子），所以中途關遊戲東西也在。
- 劇本在傳送門開門時就消耗並立刻存檔（§10），中途離開＝放棄這趟；「繼續遊戲」一律回廣場。
- 所以「關遊戲重開＝這趟關卡從頭（而且劇本沒了）」是預期行為，不是缺陷。

> 若之後要做「關卡中途存檔／續玩」，才需要把 `MapRec` 序列化進存檔（schema 版本 +1）。目前**沒有這個需求**，見 §8。

---

## 6. 取得物品的統一入口

所有「給玩家東西」的路徑都收斂到一支：

```csharp
RunProgress.Instance.GiveItem(itemId, count)
//  一律進真背包（關卡內、廣場都一樣），回傳放不下的剩餘——呼叫端自己把剩的掉在地上
//  （giveItem、拾取點都已經這樣做：「背包已滿，X 掉落地上」）

RunProgress.Instance.GiveItem(itemId, count, toRealBag: true)
//  toRealBag 已無作用（2026-11-06 拿掉臨時包後），參數只是為了相容保留
```

呼叫者：`TriggerChain` 的 `giveItem` 動作、`InteractionManager` 的拾取點與地上掉落物撿取。**新增任何給物品的來源，一律走這裡**，不要自己呼叫 `InventorySystem.AddItem`。

**`toRealBag`（2026-08-06 加，2026-11-06 起無作用）**：當初給作弊面板與拾取點的起始／教學道具（如佛燈）跳過臨時包用。臨時包拿掉後所有來源本來就進真背包，欄位與參數保留只為了不動既有地圖資料與呼叫端。走統一入口而不是直接 `AddItem` 的理由仍然成立：需要實例的物品（裝備／能力珠）會先經 `ItemManager` 骰好孔位；銅錢 101 會自動轉成金錢數字。

金錢就是道具：**銅錢 = ItemTable ID 101**（`RunProgress.MoneyItemId`），掉落時當一般道具處理，**撿起來那一刻**轉成金錢數字。

**結算**：作者拍板（2026-11-06）**獎勵區先不顯示**——`GameFlowManager` 傳 `rewards=null` 給 `ResultPanel.Show(...)`（框內顯示「無」）。之後有正式的過關獎勵再接（見 [TODO.md](TODO.md)）。

### 掉寶（資料驅動，2026-09-22 起）

`MonsterController.DropRunLoot()` 擲一次這隻怪的 **`DropTableId`**（`Assets/Data/DropTable.csv`），
掉在屍體位置，透過 `InteractionManager.DropLoot` 自動登記進 `RunProgress`。

- **一列 ＝ 一張掉落表**，有 8 個**獨立**掉落槽（`Drop1`~`Drop8`），彼此不互斥、各擲一次骰。
- 槽的格式 `itemId:機率%:數量`：`101:100:1-5`（銅錢必掉 1~5）／`201:17.5`（數量留空＝1）／`301`（機率留空＝100%）。
- ⚠ **`DropTableId` 留空／0 ＝ 完全不掉寶**（作者拍板）。給夢境教學怪這種「只是拿來打爽的」用。
- 表 **ID 1「一般小怪」**：**2026-11-06 起只掉錢——銅錢 30% 掉 1~5，血瓶／魔瓶移除**（作者：回血改由血球負責，§9）。下一行講的是當初導入時的內容（銅錢 100% 1~5、血瓶 17.5%、魔瓶 17.5%）：
  既有的怪導入時全部填 1 ⇒ **零行為變化**（20 萬次模擬比對過分布）。
- 掉不掉還有一層前置條件：`DropsLoot`（召喚物／NPC 一律 false，避免無限刷）。

> ⚠ `MonsterController` 的 Inspector 欄位 `lootMoneyMin`／`lootMoneyMax`／`lootPotionChance`
> **已經沒有人讀了**，留著只是為了不動到既有 prefab 的序列化資料。要調掉落請改 CSV。

> 需要 Unity 接線：把 `Assets/Data/DropTable.csv` 拖進場景 GameManagers 上的 **`DropTableProvider`**
> （同 SceneFxTableProvider／ItemTableProvider 的做法）。沒掛會印 warning 並讓所有怪不掉寶，不會靜靜壞掉。

---

## 7. 接線一覽（改東西前先看這張）

| 檔案 | 做什麼 |
|---|---|
| `Flow/RunProgress.cs` | 本體（單例、取得物品入口、五種記錄、F8 疊層） |
| `UI/Panels/ScriptsPanel.cs` | 傳送門面板：按下開門 → 劇本消耗＋存檔（§10） |
| `Map/MapManager.cs` | 跨 module 換圖時 `OnEnterModule`；載圖後 `RestoreGroundDrops` |
| `Map/MapLoader.cs` | 生怪跳過 `IsSpawnKilled`；建地上物跳過 `IsObjectDestroyed`、`SetRunKey` |
| `Map/DestructibleObject.cs` | 破壞時 `MarkObjectDestroyed` |
| `Map/TriggerChain.cs` | `giveItem` 走 `GiveItem` + `MarkTriggerConsumed`；重建時跳過已消耗 |
| `Combat/InteractionManager.cs` | 拾取點過濾、`DropLoot`／`DropBloodOrb` 登記、`RestoreGroundDrops` 重放（含血球）、撿取回寫、`ClearAll` 換圖清除 |
| `Combat/BloodOrb.cs` | 血球本體：外觀、碰觸判定、回復、吸收動畫、**機率／回復比例／清場數量等常數**（§9） |
| `Combat/MapClearReward.cs` | 清場血球：讀地圖 trigger 資料分析戰鬥鏈＋判定（§9.1） |
| `Map/MapMonsterRespawner.cs` | `TryGetSpawnState`／`RegionIdOf`：給清場判定問某顆出生點開打了沒、清光了沒 |
| `Map/TriggerChain.cs` | `CurrentRegions`：目前地圖的 trigger 清單（給戰鬥鏈分析） |
| `Flow/QuitGuard.cs` | 劇本中離開的警告：設定面板文案判斷＋攔截視窗關閉（`Application.wantsToQuit`）（§10） |
| `Combat/GroundLoot.cs` | 帶 `RunDropId` 欄位 |
| `AI/MonsterController.cs` | 死亡時 `MarkSpawnKilled` + `DropRunLoot` + `TryDropBloodOrb` |
| `AI/MonsterSpawner.cs` | 生怪時把 `spawnKey` 塞給 `MonsterController` |
| `Flow/GameFlowManager.cs` | 關卡結束時 `EndRun()`；結算獎勵區傳 null |
| `UI/Panels/SettingsPanel.cs` | 劇本中「返回廣場」「離開遊戲」改跳劇本已消耗警告 |
| `Save/SaveConstants.cs` | `HubModule = "Main"`（判斷是不是關卡的唯一依據） |

---

## 8. 已知限制與待補

- **地上物 key 是清單索引**：`obj#N` 依賴「同一張地圖檔每次解析順序一致」這個性質。之所以夠用，是因為進度**只活在一趟關卡內、不寫存檔**——玩家不可能在一趟關卡進行中跑去編輯器改地圖。若之後要把進度寫進存檔，索引就會在「改過地圖的新版本」對不上舊存檔，屆時必須改成編輯器產生的穩定 GUID。
- **`consumedTriggers` 只在本趟有效**：跨輪迴／跨存檔的「永久看過」另有機制（`repeat` 的 `每周目`／`永久` 自動旗標，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)），兩者不衝突、各管各的。
- **背包滿**：取得當下放不下的會掉在玩家腳下（giveItem、拾取點）或留在地上（掉落物部分撿取），本趟內跨場景保留；離開關卡沒撿就沒了。
- **中途存檔續玩**：不支援（見 §5）。真要做需序列化 `MapRec`。
- **反覆進出刷東西**：撿到即入袋＋第 1 周目 0 通關時邪佛會重發紅嫁衣劇本，理論上可反覆進出紅嫁衣撿東西。作者計畫用之後的「邪佛關卡倒數」制衡（見 [TODO.md](TODO.md)）。

---

## 9. 血球（BloodOrb，2026-11-06）

怪物死亡時有機率掉一顆**半透明紅色圓球**，玩家**碰到就吸收**（不用按 F）。

| 規則 | 內容 |
|---|---|
| **掉落機率** | **5%**（原 10%，作者實測嫌太高，2026-11-06 改）——常數 `BloodOrb.DropChancePercent`，在 **`Assets/Scripts/Combat/BloodOrb.cs` 最上面的「可調參數」區**。全域一個值，所有怪共用 |
| **回復量** | **最大血量 × 20%＋最大魔力 × 20%**（最大值的 20%，不是當前值的 20%；原 50%，作者 2026-11-06 實測嫌太多）——常數 `BloodOrb.RestoreFraction`，同一區 |
| **吸收條件** | 血或魔**至少一項沒滿**才吸收，吸收時兩項一起補，**玩家身上播跟喝血瓶一樣的回復特效**（`PlayerController.PlayDrinkPotionVfx`）；**滿血滿魔碰到沒反應**（球留著） |
| **誰會掉** | 跟掉寶同一層資格：地圖生成的敵怪（`DropsLoot`）、在關卡 run 內、且 **`DropTableId` > 0**。夢境教學怪、召喚物、NPC、`DropTableId` 留空的怪都不掉。**要讓某關的怪不掉血球＝把它們的 `DropTableId` 留空**（同時也不掉寶） |
| **和掉寶的關係** | 互不相干，各擲各的（`MonsterController.TryDropBloodOrb`） |
| **落點** | 離怪物死亡處 **1.7～2.3** 世界單位的外圈隨機挑一個**可走**格（`BloodOrb.DropDistMin/Max`），避開死亡處的掉落物 icon 與名稱文字；多顆會彼此錯開（作者 2026-11-06） |
| **跨場景保留** | 同掉落物：`RunProgress` 記座標，換圖回來原地重放；離開劇本（回廣場／死亡）才清掉 |
| **碰觸判定** | 玩家 `transform.position`（碰撞位置）到球心距離 ≤ `TouchRadius`（0.9；判定是腳底點對球心、不算身體寬度，所以給寬一點）——判定對齊碰撞，見 PROBLEMS **B13** |

### 9.1 清場血球（每條戰鬥鏈結束掉 1~2 顆，2026-11-06）

作者實測紅嫁衣「打了半天沒掉」——全關有資格的怪才 11 隻左右，10% 期望值一整趟約 1 顆。所以加一條**保底**：每場戰鬥（戰鬥鏈）結束時掉 1~2 顆，當作前往下一處前的回血。**與 10% 隨機掉並存**（作者拍板保留）。

**規則（作者拍板）**：**直接讀地圖編輯器的資料，順著鏈追到最後一個生怪的 trigger，那一組的怪全死光那一刻當下就掉**（不等待、不觀察現場）。不用逐張設定，佈置時不會因為忘了設而漏掉。程式：`Combat/MapClearReward.cs`。

- **什麼算同一條戰鬥鏈**：怪物出生點靠「接續觸發」（`next`／`nextA`／`nextB`／`nextTimeout`，中間經過對話、選擇等任何節點都行），或「完成寫旗標／死亡寫旗標 → `watchFlag(fireOnFlag)`／以該旗標為 `requireFlag` 的出生點」串起來的。
- **只在最後一組掉**：還接著別的出生點的那一組（中間的波次）打完**不掉**；鏈上最後一組（後面不再接出生點）全部死光**當下掉**。例：第一波 → 對話 → 第二波 → 第三波，只有第三波打完掉一次。
- **分組**（同一組＝同一場，要整組打完）：被同一個東西叫出來的、同一個 `waveGroup` 的、進圖就生的（含 `onEnter` 叫出來的）全部併成一組「進圖戰鬥」；靠外部旗標條件出來、沒接在任何東西後面的，條件相同的併一組。
- **分支**：**這一趟從沒開打過的出生點一律不算**——二選一只走一邊，走過的那邊打完就掉（例：血狂之爭首領房的單首領／雙首領）。
- **另一條鏈另外算**：沒接在別的出生點後面的（例：走到某處才叫出的伏兵），是第二條鏈，**打完也會再掉一次**（作者：「第二條戰鬥鏈是第二條的事情」）。
- **單一出生點的多波**（`spawnInterval`＋`maxWaves`）：N 波出完且場上清光才算這顆打完。
- **資格**：本圖這一趟至少殺過一隻 `DropTableId > 0` 的怪（跟掉寶同一條線，作者：「DropTable 有填的就有機會掉血球」）。Boss 有填就照掉。
- **每條鏈一趟只給一次**（`RunProgress` 每張圖記「已結束的戰鬥鏈」；F8 疊層會顯示數量）。掉在最後一隻怪附近，之後跟一般血球一樣跨場景保留。
- **數量**：`BloodOrb.ClearDropMin`／`ClearDropMax`（1／2），在 `BloodOrb.cs` 頂端常數區。

2026-11-06 實際地圖的分組結果（照上面規則算）：紅嫁衣每張有怪的圖都是一組「進圖戰鬥」；血狂之爭首領房四顆首領出生點一組（只算開打的那支分支）、長廊南北伏擊一組、主大廳「表態-血族」「表態-其他」各一組＋和談圍攻一組。

**不會掉／要注意的情況**：
| 情況 | 為什麼 | 防呆 |
|---|---|---|
| 沒有怪的圖 | 沒有戰鬥 | — |
| **無限波**（`spawnInterval>0`、`maxWaves` 留空） | 永遠打不完 | `validate_map.py` 警告「無限波…不會掉清場血球」 |
| 全是 `DropTableId` 留空的怪（夢境教學那類） | 刻意不給獎勵 | — |
| 鏈上「下一組」因為條件不成立而沒生（例：`onBlocked` 中止整條鏈） | 開打過的那組後面接著出生點 ⇒ 被當成中間波次、等下一組 | 佈置時避免「接得到但永遠不會生」的出生點 |
| 下一波不是用鏈叫出來，而是別的系統（劇情演出步驟、NPC 對話…）叫的 | 地圖資料上看不出兩組有關係 ⇒ 各算一條鏈、各掉一次 | 波次一律用「接續觸發」或旗標串起來 |

> 歷史：同一天先後試過「最後一隻死後等 2 秒看有沒有下一波」→「等 5 幀」，作者指出應該直接從地圖編輯的資料判斷，改成現在的鏈分析（`ClearSettleSeconds`／`ClearConfirmFrames`／`HasPendingWaves` 已刪）。

**外觀（先做來看效果，之後可能換美術圖）**：程序生成的半透明紅球（中心較透、邊緣亮紅、左上白色高光），帶呼吸脈動與上下漂浮。吸收時縮小、飛向玩家身體中心、淡出（0.28 秒）。地上有一塊柔和橢圓影子。**大小**：球 `WorldSize` 0.2；光環與冒出閃光固定用 `FxRefSize` 1.8 當基準（作者認可的效果，跟球脫鉤）；多顆同時掉彼此至少隔 `MinSpacing` 1.8。
- **掉落動畫（2026-11-06，作者：讓玩家知道有東西掉了；落地前碰到沒效果）**：① 屍體位置炸一團白紅光暈、球由小放大冒出來（0.12 秒）→ ② 拋物線飛到落點（0.5 秒，畫在角色上面、拖淡紅殘影、地上影子跟著跑、離地越高影子越小越淡）→ ③ 落地壓扁＋擴散一圈紅色光環 → ④ 再小彈一下 → ⑤ 落定，**這時才可以吸收**（`BloodOrb.Landed`）。換圖回來重放的血球直接在地上、不播動畫。各段秒數／高度在 `BloodOrb.cs` 的「掉落動畫」常數區（`DropPopTime`、`DropFlightTime`、`DropArcHeight`、`DropBounceHeight`、`RingTime`…）。用遊戲時間，對話暫停時整段凍住。
- **換成美術圖**：把圖放在 **`Resources/Pickups/BloodOrb.png`**（圓形、去背），有這張就自動改用它，大小仍由 `BloodOrb.WorldSize`（直徑 0.2 世界單位）決定。
- 大小、碰觸半徑、脈動、漂浮、排序都在 `BloodOrb.cs` 最上面的常數區。

---

## 10. 劇本消耗與離開警告（2026-11-06）

**消耗時機＝在傳送門放上劇本、按下開門那一刻**（作者拍板 2026-11-06）：
`ScriptsPanel.OnStartPressed` 開門成功後把方框裡那一份直接拿掉（不退回背包），**立刻 `SaveNow()`**（避免開門後馬上關遊戲、重開劇本又回來）。
開門失敗（找不到傳送點）不扣。關掉面板沒按開門 → 照舊退回背包。

> 歷史：同一天第一版是「踏進關卡那一刻才扣」（`RunProgress.ConsumeScriptFor`，由 `MapManager` 呼叫），作者看完改成開門就扣，那支已刪除。

| 劇本中途離開的警告 | 說明 |
|---|---|
| 「劇本中」的判斷 | `QuitGuard.InScript`＝**已經進到關卡裡**（`RunProgress.RunActive`）**或是開了門、還站在廣場上**（旗標 `關卡:scriptPortalOpened`） |
| 開門→進關卡之間的空窗 | 開門時 `ScriptsPanel` 寫旗標 **`關卡:scriptPortalOpened`**（常數 `ScriptsPanel.PortalOpenedFlag`；關卡單次＝只在記憶體、跨 module 就清）。① `Main_Square`「邪佛對話」加條件 `requireFlag=!關卡:scriptPortalOpened` → 開門後走回邪佛面前**不會再拿到一張劇本**；② 這段時間離開遊戲也會跳警告。走進關卡就清掉，死了／返回回到廣場時邪佛照常能重發 |

**離開警告**（字串在 `LanguageTable` 7001／7002）：

| 離開方式 | 劇本中（`QuitGuard.InScript`＝`RunActive`） | 不在劇本中 |
|---|---|---|
| 設定面板「返回廣場」 | 「劇本已消耗，離開後就無法再進入。確定要返回廣場嗎？」 | （廣場不顯示這顆鈕） |
| 設定面板「離開遊戲」（含開了門還在廣場時） | 「劇本已消耗，離開後就無法再進入。確定要離開遊戲嗎？」 | 原本的「確定要離開遊戲嗎？」 |
| 視窗 X／Alt+F4 | 攔下來（`Application.wantsToQuit` 回 false）改跳同一個確認框，按確認才真的關 | 直接關 |
| 過關／死亡 | 不警告 | — |

⚠ 視窗關閉攔截**只在打包版有效**；Unity 編輯器按停止 Play 不會經過 `wantsToQuit`。

> ⚠ 文案說「無法再進入」，但**目前第 1 周目、0 通關時邪佛會重發紅嫁衣劇本**（`Main_Square` 邪佛對話的條件 `requireCycleMax=1`、`requireClearsMax=0`、`requireItem=!104`），否則玩家死了回廣場會沒有關卡可進。等「邪佛關卡倒數」做好後文案與行為才會一致，見 [TODO.md](TODO.md)。

---

*2026-11-06：作者改版——拿掉臨時包（撿到即入袋、死亡保留、結算獎勵區先不顯示）、劇本開門就消耗＋中途離開警告（§10）、新增血球（§9）。*
*建立於 2026-07-27：補記 2026-07-18 commit `4aa7659`「加入關卡儲存機制」的完整設計與接線。此系統落地時剛好排在同日的文件 commit 之後，因此先前所有文件（MAP_SYSTEM §5、INTERACTION、CORE_LOOP_DESIGN §6）都還寫著「Phase 2 尚未實作」，已一併更正。*
