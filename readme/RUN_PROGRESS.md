# 關卡進度與臨時包（RunProgress）

> 返回 [文件總覽](README.md)
>
> **一句話**：一趟關卡內，**怪死了不復生、撿過的不再出現、地上沒撿的掉落物留原地**；換地圖來回都記得，**完整離開關卡（回廣場／死亡）才整個重置**。同時管「臨時包」——關卡內的收穫先暫存，**通關才落袋**。
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
| **臨時包** | 關卡內取得的道具／金錢先進這裡，通關 `SettleIntoBag()` 併進真背包，死亡／返回 `EndRunDiscard()` 整包丟棄 |
| **關卡進度（per-map）** | 已清的怪出生點、已取／已觸發的 trigger、已破壞的地上物、還沒撿的地上掉落物 |
| **工程介面** | 按 **F8** 開／關除錯疊層，直接看臨時包內容與目前 run 狀態 |

---

## 3. 生命週期：什麼時候重置

判斷「在不在關卡內」的唯一依據是 **module**：

```
RunProgress.IsLevelModule(module)  ==  module != SaveConstants.HubModule   // HubModule = "Main"
```

`Main` module（邪佛廣場、初始洞窟、開場山道）**不算關卡**——取得的東西直接進真背包、不走臨時包、不記進度。其餘 module 一律視為關卡。

`MapManager` 每次**跨 module 換圖**時呼叫 `OnEnterModule(module)`：

| 情境 | 行為 |
|---|---|
| 進入關卡 module、且 `RunModule` 不同 | `BeginRun(module)` — 清空臨時包與所有進度，開新的一趟 |
| 進入關卡 module、`RunModule` 相同 | 什麼都不做（**同一趟延續**） |
| **同 module 房間互跳** | 根本不呼叫（走 `MapManager` 的 else 分支）→ 進度完整延續 |
| 進入廣場／教學（Hub） | 若還有殘留就 `EndRunDiscard()` 保險清掉 |
| 過關（`LevelEndKind.Clear`） | `SettleIntoBag()` — 臨時包併入真背包並清空、回傳快照給結算畫面 |
| 死亡／主動返回 | `EndRunDiscard()` — 整包丟棄，這趟零收穫 |

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

**掉落物重放**用**原座標**、不重新散開、沿用原 `dropId`，所以換圖回來紅水還在原地那一格。部分撿取（背包吃不下全部）會 `UpdateGroundDropCount` 回寫剩餘數量。

### 什麼東西不記
- **召喚物**：`MonsterController` 只對「有 `SpawnKey` 的地圖出生敵怪」記進度與掉寶，召喚物沒有 `SpawnKey` → 不記、不掉寶（否則可無限刷）。
- **不在 run 期間**：所有 `Mark*` / `Register*` 在 `RunActive == false` 時**內部直接忽略**，呼叫端不必判斷。
- **`repeat=每次` 的觸發點**：`ConsumePoint` 對 `RepeatMode.Always` 不消耗，也就不會進 `consumedTriggers`。

---

## 5. 存活範圍：只在記憶體，不寫存檔

`RunProgress` 的所有內容都是**純記憶體**，關遊戲即消失，**不進 `character.json`**。這是刻意的，且與存檔設計一致：

- 過關才落袋 → 落袋的東西是走 `InventorySystem` 進真背包，**那份**由 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) 存起來。
- 沒過關的一切本來就要歸零，存下來反而錯。
- 所以「關遊戲重開＝這趟關卡從頭」是預期行為，不是缺陷。

> 若之後要做「關卡中途存檔／續玩」，才需要把 `MapRec` 與臨時包序列化進存檔（schema 版本 +1）。目前**沒有這個需求**，見 §8。

---

## 6. 臨時包與取得物品的統一入口

所有「給玩家東西」的路徑都收斂到一支：

```csharp
RunProgress.Instance.GiveItem(itemId, count)
//  關卡內 → 進臨時包，無容量上限，恆回 0
//  廣場   → InventorySystem.AddItem，回傳放不下的剩餘

RunProgress.Instance.GiveItem(itemId, count, toRealBag: true)
//  一律直接進真背包（關卡內也一樣），跳過臨時包
```

呼叫者：`TriggerChain` 的 `giveItem` 動作、`InteractionManager` 的拾取點與地上掉落物撿取。**新增任何給物品的來源，一律走這裡**，不要自己呼叫 `InventorySystem.AddItem`。

**`toRealBag`（2026-08-06 加）**：給「**不屬於這趟關卡收穫**」的來源用——目前是**作弊面板的「給道具」**，以及拾取點的 `toRealBag` 欄（起始／教學道具，如佛燈）。這類東西進臨時包會有兩個症狀：死亡歸零、要通關才落袋，於是「在關卡裡用作弊給自己一把武器來測」會變成東西給了背包卻空的（實際踩過）。走統一入口而不是直接 `AddItem`，是為了保留另外兩件事：需要實例的物品（裝備／能力珠）會先經 `ItemManager` 骰好孔位；銅錢 101 會自動轉成金錢數字。

金錢就是道具：**銅錢 = ItemTable ID 101**（`RunProgress.MoneyItemId`），掉落／臨時包／結算全部把它當一般道具處理。

**結算**：`SettleIntoBag()` 回傳併入前的 `(itemId, count)` 快照，`GameFlowManager` 把它交給 `ResultPanel.Show(...)` 顯示在獎勵區——結算畫面**直接讀臨時包**，不必去分辨真背包裡哪些是這趟撿的。

### 暫定掉寶
`MonsterController.DropRunLoot()`（**暫定公式，正式掉寶表之後換**）：

- 必掉銅錢：`lootMoneyMin`(1) ~ `lootMoneyMax`(5)
- `lootPotionChance`(0.35) 機率掉一瓶藥，201 小回血瓶／202 小回魔瓶 各半

掉在屍體位置，透過 `InteractionManager.DropLoot` 自動登記進 `RunProgress`。

---

## 7. 接線一覽（改東西前先看這張）

| 檔案 | 做什麼 |
|---|---|
| `Flow/RunProgress.cs` | 本體（單例、臨時包、四種記錄、F8 疊層） |
| `Map/MapManager.cs` | 跨 module 換圖時 `OnEnterModule`；載圖後 `RestoreGroundDrops` |
| `Map/MapLoader.cs` | 生怪跳過 `IsSpawnKilled`；建地上物跳過 `IsObjectDestroyed`、`SetRunKey` |
| `Map/DestructibleObject.cs` | 破壞時 `MarkObjectDestroyed` |
| `Map/TriggerChain.cs` | `giveItem` 走 `GiveItem` + `MarkTriggerConsumed`；重建時跳過已消耗 |
| `Combat/InteractionManager.cs` | 拾取點過濾、`DropLoot` 登記、`RestoreGroundDrops` 重放、撿取回寫 |
| `Combat/GroundLoot.cs` | 帶 `RunDropId` 欄位 |
| `AI/MonsterController.cs` | 死亡時 `MarkSpawnKilled` + `DropRunLoot` |
| `AI/MonsterSpawner.cs` | 生怪時把 `spawnKey` 塞給 `MonsterController` |
| `Flow/GameFlowManager.cs` | 關卡結束時 `SettleIntoBag` / `EndRunDiscard`，把快照給 `ResultPanel` |
| `Save/SaveConstants.cs` | `HubModule = "Main"`（判斷是不是關卡的唯一依據） |

---

## 8. 已知限制與待補

- **地上物 key 是清單索引**：`obj#N` 依賴「同一張地圖檔每次解析順序一致」這個性質。之所以夠用，是因為進度**只活在一趟關卡內、不寫存檔**——玩家不可能在一趟關卡進行中跑去編輯器改地圖。若之後要把進度寫進存檔，索引就會在「改過地圖的新版本」對不上舊存檔，屆時必須改成編輯器產生的穩定 GUID。
- **`consumedTriggers` 只在本趟有效**：跨輪迴／跨存檔的「永久看過」另有機制（`repeat` 的 `每周目`／`永久` 自動旗標，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)），兩者不衝突、各管各的。
- **掉寶公式是暫定的**：目前寫死在 `MonsterController` 的 Inspector 欄位，之後要換成資料驅動的掉寶表。
- **背包滿的結算**：`SettleIntoBag` 遇到真背包放不下只印 Warning 並**捨棄**，還沒做「溢出的掉在廣場地上」或「塞倉庫」。
- **中途存檔續玩**：不支援（見 §5）。真要做需序列化 `MapRec` + 臨時包。

---

*建立於 2026-07-27：補記 2026-07-18 commit `4aa7659`「加入關卡儲存機制」的完整設計與接線。此系統落地時剛好排在同日的文件 commit 之後，因此先前所有文件（MAP_SYSTEM §5、INTERACTION、CORE_LOOP_DESIGN §6）都還寫著「Phase 2 尚未實作」，已一併更正。*
