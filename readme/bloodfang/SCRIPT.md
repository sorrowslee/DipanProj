# 血狂之爭 — 劇本事件表（接手 AI 照這份編排）

> 返回 [本資料夾總覽](README.md)｜缺口與功能規格見 [GAP.md](GAP.md)｜任務順序見 [TASKS.md](TASKS.md)
>
> 這份是**規格**，不是現況。每一行都標了「用什麼機制」，機制全部指向 `readme/TRIGGER_CHAIN.md`／`NPC_SYSTEM.md`／`FACTION.md`／`CUTSCENE_DIRECTOR.md` 既有章節。新功能三個：**`timedChoice`**（GAP §2）、**編輯器多段演出**（GAP §8）、**兩隻頭目 Brain**（GAP §9）。
> **2026-10-15 修訂**：作者補充「依玩家血統分流」——接待／引路的人換、和談後血族與狂族玩家**沒有選擇**（直接入自己族）、只有非血狂族才有二選一與隱藏線；頭目改為新設計。README §4 的 Q1–Q13 已拍板（照建議）。

---

## 1. 劇本一句話

玩家從**大門**進入府邸，依玩家血統由**血族接待員／狂族接待員／人類老管家**其中一位引路，穿過**迎賓大廳 → 長廊 → 和談大廳**。和談破裂之後依血統分三種走法：

| 玩家血統／選擇 | 和談之後 | 攻略路 | 頭目 | 結局 |
|---|---|---|---|---|
| **血族**（或人類選 A） | 狂族偷襲血族駐地（表演戰），血族首領成為同伴 | **東側**狂族駐地（武器室→訓練場→大營） | 狂族首領 | 回大門，血族首領給血族藥劑 |
| **狂族**（或人類選 B） | 鏡像 | **西側**血族駐地（釀酒室→餐廳→花園） | 血族首領 | 回大門，給狂族藥劑 |
| **人類倒數歸零**（隱藏線） | 兩族圍攻和談廳 | 長廊中段伏擊 | **雙首領** | 在迎賓大廳結算，兩首領各掉一瓶藥劑 |

設計意圖（作者）：血族／狂族玩家只能看到自己那一條，**輪迴換血統再來**才看得到別條；人類才有選擇與隱藏線。所有路線最後都回到迎賓大廳打頭目，勝後從大門離開。

---

## 2. 房間與連接（現況，已盤點 2026-10-15）

```
            ┌───────────── 20 和談大廳 MainLobby (31x25) ─────────────┐
            西門→21                 南門→19                  東門→24
 21 釀酒室 BrewingRoom          19 長廊 Gallery            24 武器室 ArmoryRoom
      ↓南→22                        ↓南→18                       ↓南→25
 22 餐廳 Diningroom                                       25 訓練場 TrainingGround
      ↓南→23                                                     ↓南→26
 23 花園 Garden                                           26 大營 GrandCamp
      ↓南→18西門                                                 ↓南→18東門
            └──── 18 迎賓大廳 GuessLobby (23x20) [南門→17 / 北門→19 / 西門→23 / 東門→26] ────┘
                                        ↓南→17
                              17 大門 InitialScene (24x27) [玩家出生點 / 北門→18 / 鏡頭區 zoom1.6]
```

| MapsTable ID | 圖 | 劇本角色 | 現有 trigger／NPC | 氛圍 |
|---|---|---|---|---|
| 17 | `BloodFang_InitialScene` | 大門 | 玩家出生點、北門傳送點（名稱 `傳送點1`）、camZone；NPC：血族接待員(npc 2)、狂族接待員(npc 3)、人族管家(npc 4)——**出現條件與條件對話都已填好**（T0 盤點），只缺回程隱藏、`next` 與 `disappearFlag` | 1 |
| 18 | `BloodFang_GuessLobby` | 迎賓大廳（去程走廊／回程 boss 房） | 四個傳送點 | 17 |
| 19 | `BloodFang_Gallery` | 長廊（隱藏線回撤戰場） | 南北傳送點 | 17 |
| 20 | `BloodFang_MainLobby` | 和談大廳 | 南西東傳送點；`IsLevelStart=1`（Q1 拍板：T1 改到 17） | 17 |
| 21 | `BloodFang_BrewingRoom` | 左路 1（血族區） | 東南傳送點、出生點-血蝠(35, nextWhen=生出來時) | 2 |
| 22 | `BloodFang_Diningroom` | 左路 2 | 南北傳送點（⚠ 南門 `targetEntrance` 打錯成 `BloodFang_Garde_north`，T1 修正為 `BloodFang_Garden_north`）、出生點-血術士(36)、出生點-血族士兵(15) | 17 |
| 23 | `BloodFang_Garden` | 左路 3 | 南北傳送點、出生點-血蝠(35) | 17 |
| 24 | `BloodFang_ArmoryRoom` | 右路 1（狂族區） | 南北傳送點 | 17 |
| 25 | `BloodFang_TrainingGround` | 右路 2 | 南北傳送點 | 1 |
| 26 | `BloodFang_GrandCamp` | 右路 3 | 南北傳送點 | 17 |

傳送點名稱（`togglePortal` 的 `target` 要用這些**名稱**）：各圖一律叫 `傳送點-南方`／`傳送點-北方`／`傳送點-西方`／`傳送點-東方`，**例外**：21 釀酒室叫 `傳送點-東邊`／`傳送點-南邊`；17 大門叫 `傳送點1`。寫 JSON 前**重讀該圖確認**。

可用角色（素材都在 `DipanProj_Main/Assets/GameAssets/Modules/BloodFang/`）：

| 用途 | ID | 備註 |
|---|---|---|
| NPC 血族士兵／狂族士兵／人族管家 | NpcTable 2／3／4 | 管家只有 idle+walk；士兵有 attack |
| 怪 血族士兵 | MonsterData 15（Faction=Vampire ✅） | MeleeChase |
| 怪 狂族士兵 | 16（**Faction 空 ✗**，T1 補 Werewolf） | MeleeChase |
| 怪 戰狼／狂族弩手／狂族皇家衛士 | 17／18／19（Faction 空，T1 補 Werewolf） | Pounce／Archer／LeapSlam |
| 怪 血蝠／血術士 | 35／36（Faction 空，T1 補 Vampire） | Bat／Archer；兩隻都 ⏳ 未實測 |
| 狂族首領 | MonsterData **38**，新 Brain `WerewolfLord` | 設計與素材方案見 GAP §9 |
| 血族首領 | MonsterData **39**，新 Brain `VampireLord` | 同上 |
| Talk 立繪 | `Modules/BloodFang/Talk/{VampireSoldier,WerewolfSoldier}_{normal,fear}`、`Housekeeper_normal` | 首領立繪暫借士兵的 |

---

## 3. 主線事件表（逐圖）

格式：**觸發 → 機制 → 參數重點 → 接續**。trigger 名稱就是最後要寫進 JSON 的 `name`（全中文、同圖不重名）。「0格」＝動作型、`cells=[]`。

> **2026-10-15 作者第二輪調整（以血族為例，狂族鏡像）**：
> ① 選邊後首領**不當場加入**：說完「走右邊，快點！」（drama 54）**東門立刻出現**，接著 `exit_vampire`（狂族首領直接退場、血族首領速度 6 衝向東門消失，約 2 秒）；**同伴在第一張攻略圖（24／21）進場時才在玩家身邊出現**（`同伴加入`）。表演戰的小兵互打**傷害為 0**（`FactionRelations.DamageMultiplier` 表演戰時回 0）。
> ② 人類玩家在選擇面板跳出前，先播 drama 58（兩族首領各一句拉攏）。
> ③ **傳送點一律「初始停用」、由劇情開門**（作者提議）：四張劇情圖（17／18／19／20）的**所有**傳送點與六張攻略圖的回頭門都勾初始停用，不用 enableFlag（回訪時要開的門由該圖的鏈重新打開）。開門點：17 北門＝大門引路演完；18 北門＝迎賓引路演完、18 南門＝首領死後；19 北門＝長廊引路演完、19 南門＝拒選伏擊清光；20 東／西門＝首領說完「走右邊／左邊」、20 南門＝拒選圍攻清光。原本進場後才 `togglePortal` 封門的節點（`封全門-*`、`封南門-和談`）已刪除。攻略圖往前的南門維持一開始就開。

### 3.0 血統分流的兩種寫法（本表反覆用到，先看懂）

| 情境 | 寫法 | 為什麼 |
|---|---|---|
| **三選一、之後的事都一樣**（例：三段引路演出，演完沒有後續） | 三顆串成一條鏈，每顆掛 `conditions`＋**`onBlocked=跳過這顆繼續`**：`X-血族 → X-狂族 → X-管家` | 三個條件互斥，只有一顆會成立並執行；其餘兩顆「跳過、接自己的 next」。成立的那顆執行完也接 next，但下游兩顆條件都不成立、一路跳到底，**不會多做事**。 |
| **三選一、之後走不同的路**（例：和談後的表態） | 三顆 **`watchFlag`** 監聽同一個旗標，各自掛 `conditions` | `watchFlag` 被 `fireOnFlag` 啟動時走 `Activate`→先查條件，不成立整條中止（不寫 `onBlocked`），所以三條各自獨立、只有一條會跑。**不能**用上一種串法——成立的那顆接 next 會把下一條支線也跑一遍。 |

條件字串（`AppearCondition`，`flag:` 比對旗標**名字**，T0 已查證）：
- 血族 `series:2`；狂族 `series:3`；其他 `!series:2|!series:3`（`|` 是 AND）。
- 三階：血族該隱 `bloodline:22`、狂族芬里爾 `bloodline:32`。

### 3.1 大門（17）— 迎接與引路起點

| # | 事件 | 機制 | 重點 |
|---|---|---|---|
| A1 | 進圖：依玩家血統只出現一位接待者（**現況已做好**，只補回程隱藏） | NPC `conditions` | 血族接待員 `series:2\|!flag:bfTalkDone`；狂族接待員 `series:3\|!flag:bfTalkDone`；人族管家 `!series:2\|!series:3\|!flag:bfTalkDone`（現況少了 `!flag:bfTalkDone`，T3 補上） |
| A2 | 接待員對話（**現況已做好，不動**） | NPC `dramaId`＋`conditionalDramas` | 血族：`bloodline:22`→26（該隱）、`series:2`→24；狂族：`bloodline:32\|series:3`→27（芬里爾）、空→25；管家：28 |
| A3 | 對話結束 → 該接待者親自引路 | NPC `next` → `playCutscene` 0格 | 血族接待員 `next=引路-大門-血族`、狂族接待員 `next=引路-大門-狂族`、管家 `next=引路-大門-管家`。NPC 一次只會出現一隻，所以這三顆**不必**掛條件、也不必串 |
| A3b | 引路開始時 NPC 本體退場（**避免畫面上同時兩個管家**） | NPC `disappearFlag=bfGuideStarted`（三隻都填）＋演出第一步 `setFlag bfGuideStarted` | 演員起點＝**該 NPC 的座標**，NPC 退場與演員現身同一幀，看起來是同一個人開始走 |
| A4 | 引路演出 ×3 段（cutscene `id`：`guide_gate_vampire`／`guide_gate_werewolf`／`guide_gate_human`） | Cutscene（`autoStartOnEnter=false`、`lockInput=false`、`skippable=true`） | 演員 spriteFolder：`VampireSoldier`／`WerewolfSoldier`／`Housekeeper`。步驟：`setFlag bfGuideStarted` → `bubble` 3001「這邊請」 → `move` 到北門傳送點錨點往南 1.5 格 → `face`（寫死朝畫面中央那側；角色只有左右向） → `bubble` 3002（background） → `wait 1` → `despawn`。血族／狂族版台詞改用 3006／3007（語氣不同，見 §6.2）。玩家全程可自由走 |
| A5 | 回程：大門結局 | 見 §3.8（onEnter＋道謝對話＋給藥＋clearLevel） | 2026-10-15 重訂；T4 的踩點過關已移除 |

### 3.2 迎賓大廳（18）— 去程走廊

| # | 事件 | 機制 | 重點 |
|---|---|---|---|
| B1 | 去程進場 | `onEnter` 0格 `進場-去程`，`requireFlag=!bfTalkDone` | `next=封側門-去程` |
| B2 | 封東西側門 | `togglePortal` 0格 `封側門-去程` | `target=傳送點-西方,傳送點-東方`、`show=false`、`next=引路-迎賓-血族` |
| B3 | 引路（三選一，§3.0 第一種串法） | `playCutscene` ×3：`引路-迎賓-血族`（`conditions=series:2`、`cutsceneId=guide_lobby_vampire`）→ `引路-迎賓-狂族`（`series:3`）→ `引路-迎賓-管家`（`!series:2\|!series:3`）；**三顆都 `onBlocked=跳過這顆繼續`** | 三段演出同 A4 模式：引路者從南門前走到北門前，bubble 3003（管家）／3008（血族）／3009（狂族）。這張圖沒有 NPC，演員 `spawnAtStart=true` 直接在南門錨點往北 1.5 格現身 |
| B4～B7 | 回程 boss 戰 | 見 §3.6 | — |

### 3.3 長廊（19）— 去程走廊

| # | 事件 | 機制 | 重點 |
|---|---|---|---|
| C1 | 去程進場 | `onEnter` `進場-去程`，`requireFlag=!bfTalkDone` | `next=引路-長廊-血族` |
| C2 | 引路（三選一） | 同 B3：`引路-長廊-血族`／`-狂族`／`-管家`，cutsceneId `guide_gallery_*` | 南→北，bubble 3004＋3005（管家）；血族／狂族版各一句（3024／3025） |
| C3 | 隱藏線回撤 | 見 §4 | 只有非血狂族會走到 |

### 3.4 和談大廳（20）— 和談破裂、依血統表態、三路分流

> **2026-10-15 作者重訂流程**（T4 實測後）：選了一族＝**該族駐地被另一族偷襲**，首領帶玩家**殺進敵族駐地**（不是逃回己方）；首領全程以「同伴」身分跟著玩家。

| # | 事件 | 機制 | 重點 |
|---|---|---|---|
| D1–D3 | 進場 → 三方和平 → 封東西門 → 和談演出 | 同前 | `進場-和談`（`!bfTalkDone`、每次）→ `三方和平` → `封東西門-和談` → `和談演出`（`talk`，演完寫 `bfTalkDone`） |
| D4 | 和平場景的人 | NPC ×6 | `disappearFlag=bfWarStarted` |
| D6 | 表態分流 | 五顆 `watchFlag`（`fireOnFlag=bfTalkDone`＋血統條件） | 該隱／血族 → 入營對話 → `加入血族`；芬里爾／狂族 → `加入狂族`；其他 → `選邊`（timedChoice） |
| D7a | 加入血族 | `joinFaction`（`setFlag=bfJoinedVampire`） | `next=實戰開戰` |
| D7b | 加入狂族 | `joinFaction`（`setFlag=bfJoinedWerewolf`） | `next=實戰開戰` |
| D7c | 拒選（倒數歸零，只有人類） | `factionWar` `拒絕選邊`（`setFlag=bfRefused`）→ `開戰`（factionWar，`setFlag=bfWarStarted`） | 兩族**真的**打玩家 |
| D8 | 實戰開戰（選了一族時） | `factionWar` `實戰開戰`（**fullDamage 勾**、`setFlag=bfWarStarted`） | 我方守軍 3 隻 vs 敵族突襲 12 隻（一次性，血族線狂族 16\|17 從西側、狂族線血族 15\|35 從東側），互打全額傷害 ⇒ 守軍很快被殺光、敵軍轉頭追玩家，逼玩家往開著的那扇門跑；玩家夠強全清也行（只剩那扇門可走）。和平 NPC 退場 |

**血族線（`bfJoinedVampire`）**

| 順序 | trigger | 內容 |
|---|---|---|
| 同時 | `出生點-血族守軍`（15 ×3，原血族 NPC 站位）、`出生點-狂族突襲`（16\|17 ×12，**西側**＝血族駐地方向） | 條件旗標 `bfJoinedVampire`；實戰：守軍很快陣亡，突襲軍轉追玩家 |
| 1 | `分流-血族`（watchFlag）→ `對話-偷襲-血族`（drama 44） | 血族士兵：「不好了！狂族那些瘋狗偷襲我們！」 |
| 2 | `首領撤離-血族`（playCutscene `exit_vampire`） | 狂族首領走回東門消失；血族首領演員在原地收掉 |
| 3 | `同伴-血族首領`（**companion** 42，生在血族首領原位） | 從這裡開始血族首領跟著玩家，跨圖 |
| 4 | `對話-殺入狂族`（drama 54）→ `開東門` → `封南門-血族` | 「駐紮地被攻陷了……只好衝進狂族的駐紮地」；只剩東門可走 |

**狂族線（`bfJoinedWerewolf`）**：完全鏡像——`出生點-狂族守軍`（16 ×3 東半）、`出生點-血族突襲`（15\|35 ×12 **東側**）、drama 45 → `exit_werewolf`（血族首領走西門）→ 同伴 43 → drama 55 → `開西門` → `封南門-狂族`。

**拒選線（`bfRefused`）**

| 順序 | trigger | 內容 |
|---|---|---|
| 1 | `分流-拒選` → `對話-看不起`（drama 46） | 「你不選邊站，就是看不起我們！」 |
| 2 | `首領退場-拒選`（`leaders_exit`）→ `封南門-拒選`（`setFlag=bfSiegeStart`） | 三扇門全封 |
| 3 | `出生點-圍攻-血族`（15\|36，西門）＋`出生點-圍攻-狂族`（16\|17，東門） | 偵測條件 `flag:bfSiegeStart\|!flag:bfSiegeCleared`；每 5 秒一波、上限 4、各 3 波、同群組 `和談圍攻` |
| 4 | 全滅 → `開南門-拒選`（`setFlag=bfSiegeCleared`） | 只開南門 |
| 回訪 | `進場-拒選回訪`（每次）→ `封東西門-回訪` | 防止從側路繞去迎賓大廳 |

### 3.5 攻略路（血族線走東側 24→25→26；狂族線走西側 21→22→23）

| 圖 | 進場 | 怪物 |
|---|---|---|
| 24 武器室 | `進場-攻略`（`bfJoinedVampire`、每次）→ **`結束表演戰`**（factionWar 沒勾：兩族互打回到演戲傷害）→ `封來路`（北門） | 戰狼 17 ×2、狂族士兵 16 ×3 |
| 25 訓練場 | `進場-攻略` → `封來路`（北門） | 戰狼 ×2、狂族士兵 ×2、狂族弩手 18 ×2 |
| 26 大營 | 同上 | 皇家衛士 19 ×2、狂族弩手 ×3 |
| 21 釀酒室 | `進場-攻略`（`bfJoinedWerewolf`）→ `結束表演戰` → `封來路`（東邊） | **作者佈置，不動** |
| 22 餐廳／23 花園 | `進場-攻略` → `封來路`（北門） | **作者佈置，不動** |

攻略圖的怪物出生點都**不掛條件**（作者左側的佈置也是如此）——它們只在對應那條線走得到。

**清場才開門**（2026-10-15 作者）：六張攻略圖往下一張的傳送點都**初始停用**，該圖所有怪物出生點同屬波次群組 `清場`，最後一顆填 `next=開前門`——全部打死才出現；回頭的門永遠不開。21 釀酒室血蝠出生點原本的 `nextWhen=生出來時` 已清空（否則一生出來就開門）。

### 3.6 迎賓大廳（18）— 首領戰

| 線 | 鏈 |
|---|---|
| 血族 | `進場-回程-血族` → `封全門-血族` → `首領-狂族`（bossIntro 38）→ `出生點-狂族首領`（38、初始停用、全滅推鏈）→ drama 47 → `開大門`（南門、`setFlag=bfBossDead`） |
| 狂族 | `進場-回程-狂族` → `封全門-狂族` → `首領-血族`（39）→ `出生點-血族首領` → drama 48 → `開大門-狂族` |
| 拒選 | `進場-回程-拒選` → `封全門-雙` → `首領-雙-狂族`（40）→ `出生點-狂族首領-雙`（生出來時接續、群組 `雙首領`）→ `首領-雙-血族`（41）→ `出生點-血族首領-雙`（全滅推鏈）→ drama 49 → **`拒選過關`（clearLevel，延時 15 秒撿掉落）** |

雙首領版 40／41 的 `DropTableId` 指向 DropTable 2／3：**必掉 1 階狂族藥劑 303／1 階血族藥劑 302**。

### 3.7 長廊（19）— 拒選線伏擊

| trigger | 內容 |
|---|---|
| `埋伏-長廊`（camZone 踩點，長廊中段 x5–9 × y11–12） | 偵測條件 `flag:bfRefused\|!flag:bfGalleryAmbush` → `封長廊兩門`（`setFlag=bfGalleryAmbush`） |
| `出生點-伏擊-北`（15\|16\|36）＋`出生點-伏擊-南`（16\|17\|15） | 條件 `flag:bfGalleryAmbush\|!flag:bfGalleryCleared`；每 4 秒、上限 5、各 3 波、群組 `長廊伏擊` |
| 全滅 → `開長廊南門`（`setFlag=bfGalleryCleared`） | 只開南門 → 迎賓大廳 |

### 3.8 大門（17）— 主線結局

| trigger | 內容 |
|---|---|
| `結局-血族`（onEnter，`requireFlag=bfBossDead`＋`flag:bfJoinedVampire`）→ `對話-血族道謝`（drama 56）→ `贈藥-血族`（giveItem 302）→ `大門過關`（clearLevel 3 秒） | 「謝謝你救了我，你贏得了血族的尊敬」 |
| `結局-狂族`（鏡像，drama 57、giveItem 303） | — |

（T4 的「大門踩點過關」已移除；`clearLevel` 不能踩點，見 PROBLEMS **B18**。）

---

## 4. 隱藏線（拒選）一覽

20 圍攻（清光才開南門）→ 19 長廊中段伏擊（上下封門、清光只開南門）→ 18 雙首領（各掉一瓶血統藥劑）→ **在 18 直接結算**，不回大門。

---

## 5. 旗標表（全部 **關卡單次 `level`**，`flags.json` id 12～22）

| 旗標 | 誰寫 | 意義 |
|---|---|---|
| `bfGuideStarted` | 大門引路演出第一步 | 接待 NPC 退場 |
| `bfTalkDone` | 和談演出 | 去程結束 |
| `bfWarStarted` | `實戰開戰`（選邊）／`開戰`（拒選） | 和平 NPC 退場 |
| `bfJoinedVampire`／`bfJoinedWerewolf` | 加入陣營 | 血族線／狂族線 |
| `bfRefused` | 拒絕選邊 | 隱藏線 |
| `bfBossDead` | 迎賓大廳開南門 | 大門結局 |
| `bfSiegeStart`／`bfSiegeCleared` | 和談廳圍攻開始／清光 | 圍攻出生點的開關 |
| `bfGalleryAmbush`／`bfGalleryCleared` | 長廊伏擊開始／清光 | 伏擊踩點只觸發一次、出生點開關 |

---

## 6. 對話與字串清單（AI 初稿，作者審）

| DramaTable | 用途 |
|---|---|
| 44／45 | 偷襲：血族士兵（選血族）／狂族士兵（選狂族）喊「另一族偷襲我們」 |
| 46 | 拒選：「你不選邊站，就是看不起我們！」 |
| 47／48 | 主線首領敗北（另一族首領＋同伴首領：「到大門口去，我有東西要給你」） |
| 49 | 雙首領敗北（主角獨白） |
| 50–53 | 表態入營（血族／狂族／該隱／芬里爾） |
| 54／55 | 同伴首領：「駐紮地被攻陷，只好衝進敵族駐地」 |
| 56／57 | 大門道謝＋給藥 |

LanguageTable 3001–3025 見上一版（引路與和談 bubble、選擇面板），不變。

---

## 6.5 同伴與表演戰（新機制，GAP §10）

- **同伴**：MonsterData **42 血族首領・緋夜（同伴）**／**43 狂族首領・嘯月（同伴）**——HP 999、`ContactDamage 0`、Speed 4.8。以 PlayerAlly 身分生成（AllyBrain：跟玩家、看到敵人就衝上去），`IsCompanion`＝不受傷、任何怪都不以它為目標。跨圖跟隨、換關卡自動解散。
- **實戰開戰**：`factionWar` 勾 `fullDamage`——兩族互打全額、敵族照常打玩家。進攻略圖第一張時用沒勾的 `factionWar` 回到演戲傷害。（2026-10-15 取代 `factionShow` 表演戰。）

---

## 7. 命名與分層慣例（接手 AI 一定照做）

- trigger `name`：`類型-說明`，去程／回程／分流用後綴區分（`進場-去程`、`進場-回程-血族`）。**同圖不重名**；鏡像那側名字要帶陣營字樣。
- 動作型（`togglePortal`／`factionWar`／`joinFaction`／`drama` 純鏈節點／`bossIntro`／`playCutscene`／`timedChoice`／`onEnter`／`clearLevel` 鏈動作）一律 `cells=[]`；需要位置的只有：出生點、camZone、踩點型 `clearLevel`、踩觸發的 `drama`。
- 鏈不能成環（LEVEL_END_FLOW 雷點）：本表沒有任何 `next` 指回上游，寫 JSON 後用小腳本把每圖的 `next` 圖畫出來檢查一次。
- 一條鏈中間加條件 → 一定同時設 `onBlocked=跳過這顆繼續`（TRIGGER_CHAIN §2 警告）。本表**只在「三選一、之後都一樣」的引路串**用這招（§3.0 第一種）；**後續不同的分流一律靠入口**（`onEnter` 的 `requireFlag`、`watchFlag` 的 `fireOnFlag`＋`conditions`），每條支線從入口到結尾完全獨立。
- 演出與對話的分工：**有人走位就用 Cutscene，純講話用 drama**（CUTSCENE_DIRECTOR §0）。

---

## 8. Slice 切法（2026-10-15 重訂後）

| Slice | 範圍 | 狀態 |
|---|---|---|
| S1 | 17→20 去程＋和談＋表態 | ✅ 作者實測「大致可以」 |
| S2 | 血族線：表演戰→同伴→東側 24–26 攻略→迎賓大廳狂族首領→大門結局 | ✅ 依新流程交付待測 |
| S3 | 狂族線：鏡像（西側 21–23 用作者的佈置） | ✅ 同上 |
| S4 | 隱藏線：和談圍攻→長廊伏擊→雙首領掉藥 | ✅ 同上 |
| S5 | 兩隻頭目 Brain（T8） | ⏳ |
