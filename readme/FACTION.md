# 陣營系統（三方陣營：狼人×吸血鬼×主角）

> 返回 [文件總覽](README.md)｜NPC 系統見 [NPC_SYSTEM.md](NPC_SYSTEM.md)｜觸發鏈見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)｜怪物與召喚陣營前身見 [BOSS_MODULE.md](BOSS_MODULE.md) §4
>
> **狀態：✅ 程式完成（2026-08-28）／⏳ 未實機驗證（作者素材未到位，先擱置）**。
> 驗證前**只需確認現有功能不受影響**——先跑 §6-A 的回歸清單即可；陣營本體之後照 §5 配置、§6-B~E 驗證。

給「兩族原本和平、旗標開戰三方互打、玩家選邊」這類劇本用；規則全收斂在一個檔案，之後更多陣營＝改那一處。

## 1. 一句話架構

**「誰能傷誰／傷害乘數／放哪個 Layer」只寫在 `AI/FactionRelations.cs` 一處**。接觸傷害、找目標、中央傷害結算（CombatSystem）都查它；「玩家武器打不打得到」＝它決定該陣營放 Enemy 層還是 Ally 層（子彈只打 Enemy 層，**所有武器路徑零改動**；怪物互打走登記表＋`Physics2D.Distance`、不吃 Layer，所以結盟的狼人照樣咬得到吸血鬼）。

陣營列舉（`MonsterFaction`）：`Enemy`（一般怪）／`PlayerAlly`（玩家召喚物）／`Neutral`（NPC）／**`Werewolf`／`Vampire`（劇本部族）**。

## 2. 規則（2026-08-28 作者定案）

| 階段 | 兩族行為 | 玩家武器 | 傷害 |
|---|---|---|---|
| **和平**（預設） | 視同中立：不打人、不被打、閑晃演戲（用 NPC 擺） | **打不到**（Ally 層） | — |
| **開戰**（`factionWar` 鏈動作） | 兩族互咬＋攻擊玩家；靠「兩族擺得近、索敵挑最近」讓他們先互打，不寫特例 | 兩族都打得到（切 Enemy 層） | **兩族互打 ×1/100（演戲）**；對玩家＝正常 |
| **結盟**（`joinFaction` 鏈動作） | 己方不再攻擊玩家；兩族之間照打（仍 1/100，殺敵主力是玩家） | **己方打不到**（切回 Ally 層）、敵族照打 | 玩家對敵族＝正常 |

- 狀態（開戰／結盟）＝**關卡單次**：只活在這趟劇本，換 module 由 `TriggerChain.ClearLevelFlags` 自動清（死亡/回廣場＝重置回和平）。
- 演戲傷害**不跳傷害數字、不印 log**（`CombatSystem.CurrentHitTheatrical`；30 隻互毆會洗版），白光閃爍照舊、有打鬥感。
- `Enemy` 與兩族**刻意互不敵對**——既有內容與劇本部族各管各的。
- 玩家召喚物（PlayerAlly）視同玩家：開戰後打「非結盟」部族，傷害正常。

## 3. 為什麼不會影響現有功能（回歸安全性分析）

改動全是「加分支」，既有路徑逐條對照：

| 既有東西 | 為什麼不受影響 |
|---|---|
| 所有現有怪物 | `MonsterData.csv` 的 `Faction` 是**新的表尾欄、全部留空**＝Enemy，行為與改前逐字相同；`BrainType` switch 只**加** `case "War"` |
| 傷害數字／log | 演戲抑制只在「攻擊方與受擊方**都是部族**」時成立（`DamageMultiplier<1` 才設旗標）——場上沒有部族怪＝永遠不觸發 |
| 玩家召喚物（御靈水晶） | `SpawnMonster` 明確傳 `PlayerAlly`，CSV Faction 覆寫只在「呼叫端傳預設 Enemy」時發生 |
| NPC 第一波 | Neutral 的關係（誰都不打、不被打）與第一波逐條相同；「平時不看玩家」原靠 DetectionRange=0，現在多一層 `AttacksPlayer(Neutral)=false` 保證，行為不變 |
| Layer | `ApplyLayer` 對 Enemy／PlayerAlly／Neutral 的結果與舊 if/else 完全等價；只有部族有新邏輯 |
| 換圖／輪迴 | `ClearLevelFlags` 多呼叫的 `ResetScenario` 在沒有劇本狀態時直接 return（no-op） |
| 觸發鏈 | 只**加**兩個 case；沒擺 `factionWar`/`joinFaction` trigger 的地圖零差異 |

所以**現階段只要跑一次 §6-A 回歸**（拿紅嫁衣現有內容跑一輪）就能安心擱置。

## 4. 沒素材也能測：替身方案

正式素材到位前，可以**全程借現有的圖**把機制驗完（之後只要把 Name／圖換掉，配置一個字都不用改）：

- **和平 NPC 替身**：NpcTable 借 `Family_Father`／`Family_Mother`／`Family_Girl`（Main，已有 idle+walk）。
- **兩族戰士替身**：MonsterData 借紅嫁衣的鬼（`Ghost_Dog` 當狼人、`Ghost_Boy` 當吸血鬼之類，各有 idle/walk）——⚠ 這些是 RedBridalGown module 的素材，**測試地圖要建在 RedBridalGown module** 才載得到（或把圖複製一份到 Main）。
- 測試地圖：RedBridalGown module 隨便新建一張 18×10，照 §5 擺。

## 5. 配置手冊（一條龍，含具體範例值）

> 範例中的旗標名／trigger 名可自取，以下用固定名稱方便對照。**所有新增列照作者規矩：接各表現有最大 ID、不動既有列。**

### 5.1 旗標（編輯器 → Trigger 頁籤 → 旗標管理器）

新增一個旗標 `部族開戰`，生命週期切到**關卡單次**（開戰狀態只活這一趟，跟 FactionRelations 的內建重置同步）。

### 5.2 和平階段（NPC 分頁）

| 誰 | 怎麼填 |
|---|---|
| 兩族圍觀群眾 | NpcTable 各加一列（Name=替身資料夾）→ NPC 分頁擺數個，行為＝原地或來回走動；**消失旗標＝`部族開戰`**（開戰瞬間退場） |
| 狼人首領／吸血鬼首領 | 各擺一個**原地 NPC**，**不填消失旗標**（開戰後留在場邊給玩家選邊）；對話 id 指到選邊對話（見 5.5） |

### 5.3 戰鬥版怪物（MonsterData.csv 各加一列）

關鍵欄位（其餘照一般怪填）：

| 欄 | 狼人戰士範例 | 說明 |
|---|---|---|
| `Name` | `Ghost_Dog`（替身） | 序列圖資料夾名 |
| `HP` | 200 | 互打 1/100 傷不死彼此，HP 主要對玩家有意義 |
| `BrainType` | **`War`** | 追最近敵對目標（敵對怪或玩家，挑近的） |
| `InvincibleTimeMs` | **400** | ⚠ 必填 300~500：互毆怪沒無敵幀＝勝負看 Update 順序（PROBLEMS F5） |
| `ContactDamage` | 10 | 對玩家的正常傷害（互打自動 ×1/100） |
| `DetectionRange` | **25** | 拉大到整房看得到彼此，開戰才會立刻互咬 |
| `Faction` | **`狼人`** | 表尾新欄；另一列填 `吸血鬼`。可填中英（狼人/Werewolf、吸血鬼/Vampire） |

### 5.4 出生點與開戰（Trigger 頁籤）

| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `狼人出生點` | monsterSpawn | 怪物id=狼人戰士、**條件旗標=`部族開戰`**、塗 5~8 格 |
| `吸血鬼出生點` | monsterSpawn | 同上、**兩片塗得靠近**（開戰瞬間彼此最近→先互咬不衝主角）；合計 ≤30 隻 |
| `開戰` | **三方陣營開戰(鏈動作)** | 無參數；**完成寫旗標=`部族開戰`**（一顆同時完成：切關係＋NPC 退場＋怪物入場） |
| （開戰入口） | 隨意：drama／switch／watchFlag… | next=`開戰`。例：某段對話講完開戰、或按開關觸發 |

### 5.5 選邊

| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `加入狼人` | **加入陣營(鏈動作)** | faction=`狼人`（畫角落 0 格即可） |
| `加入吸血鬼` | 加入陣營(鏈動作) | faction=`吸血鬼` |
| 首領 NPC 的接鏈 | （NPC 分頁） | 狼人首領的「接續觸發」=`加入狼人`，吸血鬼首領同理 |

⚠ **NPC 目前只有「消失旗標」沒有「出現旗標」**——開戰後不能讓新 NPC 登場。所以首領採「一直在場、不消失」的擺法；若要「開戰前後不同台詞」或「開戰後才出現的選邊對象」，用 **drama 觸發點＋條件旗標**（`條件旗標=部族開戰` 疊在首領旁，next 接加入陣營）替代。出現旗標記在 TODO，有需要再補。

## 6. 驗證清單（之後測試照這份打勾）

### A. 回歸——現有功能不受影響（**現在就能跑，不需要任何新素材/配置**）

- [ ] 紅嫁衣跑一輪：怪照常追玩家、被打**有傷害數字**、掉寶、boss（逃跑+召喚）正常。
- [ ] 御靈水晶召喚：召喚物打敵怪、玩家子彈穿過召喚物、跟著過傳送點。
- [ ] NPC 第一波：閑晃不看玩家、按 F 交談、對話完轉回朝向。
- [ ] 換圖／死亡回廣場／輪迴／存讀檔：Console 無新紅字或 Warning。
- [ ] 編輯器開啟正常，Trigger 型別清單多了「三方陣營開戰」「加入陣營」兩項（不用就不影響任何地圖）。

### B. 和平階段

- [ ] 兩族 NPC 閑晃，玩家武器打不到（子彈直接穿過）、怪物也不理他們。
- [ ] 首領可交談。

### C. 開戰

- [ ] 觸發開戰鏈瞬間：和平 NPC 消失、兩族戰士從出生點出現、立刻互咬。
- [ ] 互咬**沒有傷害數字洗版、Console 沒有 log 洗版**，但有受擊白光（有打鬥感）。
- [ ] 兩族互相打不死（1/100 演戲）。
- [ ] 玩家走進戰團會被兩族攻擊（正常傷害）；玩家子彈打兩族都有傷害數字。
- [ ] 房間互跳回來：和平 NPC 不復活、戰場延續（旗標記憶）。
- [ ] 完整離開關卡再進：一切重置回和平（關卡單次）。

### D. 選邊

- [ ] 跟狼人首領對話 → Console 出現「玩家結盟：Werewolf」。
- [ ] 己方（狼人）不再攻擊玩家；**玩家子彈穿過狼人**（打不到）；吸血鬼照常可打、也照常打玩家。
- [ ] 狼人 vs 吸血鬼仍互咬（仍 1/100）。
- [ ] （有召喚武器時）召喚物打吸血鬼、不打狼人。

### E. 邊界

- [ ] `加入陣營` 的 faction 填錯字 → Console Warning、鏈照常接 next 不卡死。
- [ ] 選邊後死亡回廣場再進 → 回和平、未結盟（可重新選）。
- [ ] 開著背包/對話時戰場暫停（PausesGame 慣例）、關閉恢復。

## 7. 執行單元／動到的檔

`AI/FactionRelations.cs`（**核心：關係矩陣＋執行期狀態＋ApplyLayer＋Parse**）、`AI/MonsterFaction.cs`（+Werewolf/Vampire）、`AI/Behaviors/WarBrain.cs`（**新**）、`AI/MonsterController.cs`（目標選擇一般化：enemyTarget 依 `HasMonsterFoes`、playerTarget 依 `AttacksPlayer`、faceTarget＝enemyTarget ?? playerTarget；BrainType 加 `War`；演戲傷害不跳數字）、`AI/MonsterSpawner.cs`（CSV `Faction` 欄〔index 22〕＋Layer 交給 ApplyLayer）、`Combat/CombatSystem.cs`（怪×怪查 DamageMultiplier＋`CurrentHitTheatrical`）、`Map/TriggerChain.cs`（`factionWar`／`joinFaction` 鏈動作；ClearLevelFlags 連帶 ResetScenario）、NPC 消失旗標（`NpcInstance.disappearFlag` 兩專案鏡像＋NpcSpawner 進圖過濾＋NpcAgent 輪詢退場＋編輯器面板欄）、編輯器 `TriggerType.cs`＋`triggerTypes.json`（兩型別，**兩處同步**）。

## 8. 邊界與已知取捨

- **未選邊時兩族理論上也會打玩家**（AttacksPlayer=true）——靠站位設計讓他們彼此更近而先互咬（作者拍板：設計即規則）。玩家硬衝進戰團就會被打，合理。
- 開戰/結盟**不寫存檔**：讀檔＝重新進關＝回和平重演，與「只在這趟劇本內有效」一致。
- 兩族戰士互相打不死（1/100）：戰場會一直演到玩家介入——刻意的；「殲滅敵族過關」之類的勝負收尾等劇本具體設計再擺（TODO）。
- 換層只動部族；Enemy/Neutral/PlayerAlly 的層不隨劇本狀態變。
- 未來更多陣營／關係表資料化：改 `FactionRelations` 的 Hostile/DamageMultiplier 成查表即可，呼叫端不動。
