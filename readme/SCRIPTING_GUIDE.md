# 劇本編排手冊（AI 把口述劇本做成可玩關卡）

> 返回 [文件總覽](README.md)
>
> **這份是什麼**：作者口述一段劇本（誰帶路、去哪、選什麼、打誰、拿什麼），AI 把它做成可以玩的關卡：資料表、旗標、觸發鏈、演出、出生點、傳送門，全部由 AI 直接寫。
> 2026-10-15 的血狂之爭是第一次整段交給 AI 編排（實例在 [bloodfang/](bloodfang/README.md)），本檔是那次的經驗萃取：**流程、模式庫、坑、工具**。
> 開新劇本、或改既有劇本的觸發／演出／出生點／傳送門前，先讀本檔，再依 §2 的清單挑讀主題文件。
> 工具在 `tools/scripting/`（§5），**每次改完地圖都要跑驗證**。

---

## 0. 一句話版

作者講流程 → AI 寫劇本事件表（`readme/<模組>/SCRIPT.md`，唯一真相）→ 一次把疑問問完 → 一條線一條線做（資料表＋`.dipanmap` JSON＋必要程式）→ `validate_map.py` 0 錯誤 → 交作者測、照回饋改。
**作者的手只需要：口述、拍板、編譯、Sync、測、回饋。**

---

## 1. 分工：什麼是 AI 做、什麼還是要作者

| 類別 | 誰做 | 說明 |
|---|---|---|
| 編排邏輯（觸發鏈、旗標、分流、開關門、出生點參數、演出步驟） | **AI** | 直接寫 `.dipanmap` JSON。這是劇本工作的大宗 |
| 資料表（台詞、語言字串、怪物列、掉落、旗標登記、NPC 列） | **AI** | 一律**新增列**；不改既有列的既有欄位（例外要先問） |
| 空間擺放（出生點格子、演員站位、走位目標、踩點區） | **AI**（用 `map_grid.py` 看格子）＋作者事後微調 | AI 看不到畫面，但格子圖＋可走層足夠擺得合理；回報時列出座標讓作者好找 |
| 缺的功能（新鏈動作、新面板、AI 行為） | **AI 寫程式**，但先寫規格給作者看 | 先證明「既有機制組不出來」才動程式（§3 先查模式庫） |
| 地圖本體（背景、可走層、地上物、傳送點位置） | **作者** | AI 不畫可走層、不擺地上物 |
| 美術素材（新角色序列圖、頭像） | **作者**（或另案產圖） | 沒圖時先借現有角色的序列圖 |
| 觀感、手感、時序、距離感 | **只有作者測得出來** | AI 跑不了遊戲。「看起來怪」「跟不上」這類回饋只能靠作者，AI 的工作是把它翻譯成可改的東西（§7） |
| 編譯、Sync Map Assets、git | **作者** | AI 不 commit |

---

## 2. 工作流程

### 2.1 開工前（讀）

1. 根目錄 `AGENTS.md`、本檔。
2. 依劇本會用到的東西挑讀：[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)（**必讀**：§1 核心、§2 通用欄位、§2.6 條件、§3.5 出生點、§3.5b 波次）、[CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md)（有人走位就要）、[DRAMA.md](DRAMA.md)（對話）、[NPC_SYSTEM.md](NPC_SYSTEM.md)、[FACTION.md](FACTION.md)（陣營互打）、[LEVEL_END_FLOW.md](LEVEL_END_FLOW.md)（過關）、[LOCALIZATION.md](LOCALIZATION.md)（字串 id 分段）。
3. 最可靠的範本是**現成地圖**：紅嫁衣 `RedBridalGown_BridalRoom.dipanmap`（作者親手做的 boss 房一整套）、血狂之爭十張（AI 做的，分流／開關門／演出最齊）。用 `validate_map.py <模組> --chain` 印出鏈來讀，比開 JSON 快。

### 2.2 建模組資料夾 `readme/<模組>/`

照 [bloodfang/](bloodfang/README.md) 的四份：

| 檔 | 內容 |
|---|---|
| `README.md` | 作者口述原文（逐字保留）、拍板結果（Q 編號＋答案）、範圍 |
| `SCRIPT.md` | **劇本事件表＝唯一真相**：逐圖逐事件「觸發 → 動作 → 下一步」、旗標表、對話與字串清單、命名慣例。地圖 JSON 只是它的結果 |
| `GAP.md` | 缺口分析：每個需求對到哪個既有機制，對不到的寫新功能規格 |
| `TASKS.md` | 垂直切片任務單（一條線從頭到尾一張，不要按檔案類型切） |

劇本完成、實測穩定後，GAP／TASKS 可封存，留 README＋SCRIPT。

### 2.3 盤點與提問

- 每張圖跑 `map_grid.py` 看可走區、傳送點、作者已擺的東西；MapsTable 確認 map id、`IsLevelStart`。
- **作者已佈置的觸發／怪物不要動**，要動先問（血狂之爭左側三間房就是作者佈置的）。
- 寫 GAP：需求 → 既有機制。大部分都對得到（§3）；真的對不到才列新功能。
- **疑問一次問完**，每題附 AI 的建議做法（作者常會回「照你想的」）。不要一題一題擠牙膏。

### 2.4 一條線一條線做

1. 資料表先補（台詞、字串、旗標、怪物列）——觸發鏈引用的 id 要先存在，驗證才會過。
2. 寫地圖：用 `dipan.py` 的函式寫 Python 腳本改 JSON（§5.2）。**每次動工前重讀地圖檔**：作者在編輯器存檔會重寫整份 JSON，不要用上一輪的記憶。
3. `validate_map.py <模組>`：錯誤必須 0；警告逐條確認是刻意的。
4. 改了程式：沒有 C# 編譯器，跑 PROBLEMS **I10** 的「用到但沒宣告」檢查＋括號配對。
5. 記錄：PROGRESS 一條、新坑進 PROBLEMS、SCRIPT.md 同步改。

### 2.5 交作者測（回報格式）

固定四段，作者照著做不用想：

1. **做了什麼**：用玩家看得到的話講，不是檔案清單。
2. **請你做什麼**：要編譯哪個專案（主遊戲／編輯器）、要不要 Sync Map Assets、從哪進關。規則：
   - 改了 `DipanProj_Main/Assets/Scripts` → 主遊戲重新編譯；改了 `DipanProj_MapEditor/Assets/Scripts` 或 `triggerTypes.json` → 編輯器也要。
   - 改了 `DipanProj_MapEditor/Maps/**` 或 `flags.json` → **Sync Map Assets**（旗標也靠它同步，PROBLEMS **I4**）。
   - 只改 `Assets/Data/*.csv` → 不用 Sync。
3. **預期看到什麼**：勾選清單。
4. **不確定的／要你決定的**：哪裡是估的（數量、座標、秒數）、哪裡可能壞。

### 2.6 照回饋改

作者實測後常常**重訂流程**（血狂之爭在 T4 之後整條重訂過一次）。這很正常：口述時想像的和玩起來的不一樣。不要怕砍掉重做，但每次重訂都把新流程寫回 SCRIPT.md，否則下一輪就不知道哪個才是對的。

---

## 3. 模式庫（需求 → 做法）

先在這裡找，找不到再考慮寫程式。名稱裡的 `xx` 換成自己的。

### 3.1 鏈的基本規則

- 觸發點之間用 **`next` 填名稱**接起來；名稱同圖唯一，名稱就是 API。
- 完成時**先寫 `setFlag`、再啟動 `next`**。
- **動作型節點（鏈動作）0 格**（`cells: []`）；只有要玩家踩的才塗格子。
- 0 格又沒人接的節點**永遠不會跑**（驗證會報「孤兒節點」）。自己會觸發的只有：`onEnter`、`watchFlag`、`teleport`、`monsterSpawn`、塗了格子的。
- 一條鏈不能成環。

### 3.2 進圖就做事：`onEnter`

`onEnter`（0 格）→ `next`。同一張圖去程、回程要做不同事 ⇒ **多顆 `onEnter` 各掛 `requireFlag`**（例：`進場-去程` `requireFlag=!bfTalkDone`；`進場-回程-血族` `requireFlag=bfJoinedVampire`）。要每次進圖都重做（例如重新封門）就 `repeat=每次`。

### 3.3 依血統／道具分流（兩種寫法）

條件語法見 TRIGGER_CHAIN §2.6：`series:2`（血族系列，三階都算）、`bloodline:22`（特定血統）、`item:104`，`!` 否定，`|` 連接＝**AND**（沒有 OR）。

| 情況 | 寫法 | 例子 |
|---|---|---|
| 三選一，**選完後續一樣** | 三顆串在同一條鏈上，各掛 `conditions`＋`onBlocked=跳過這顆繼續` | 引路演出：血族接待／狂族接待／人類管家三段 `playCutscene` 串起來，只有符合的那段會播 |
| 分流後**後續不同** | 多顆 `watchFlag` 盯同一個 `fireOnFlag`，各掛 `conditions` | 和談後表態：五顆 `watchFlag` 都盯 `bfTalkDone`，分別掛 `bloodline:22`／`series:2\|!bloodline:22`／…／`!series:2\|!series:3` |

⚠ 鏈中間節點掛了條件卻沒設 `onBlocked=跳過這顆繼續` ⇒ 條件不成立時整條鏈停在那裡。

### 3.4 玩家選擇：`timedChoice`

暫停遊戲、兩個按鈕＋倒數。`nextA`／`nextB`／`nextTimeout` 三條互斥，**不吃通用 `next`**。文字全填 LanguageTable id。選完要寫旗標就讓 `nextA` 接一顆 `joinFaction`／`watchFlag` 轉接節點的 `setFlag`。
慣例：選項前先讓相關角色各講一句（拉攏），選完先接「選了的那邊的反應」再接後續劇情——直接跳後續會很突兀。

### 3.5 傳送門開關

- **劇本地圖的傳送門一律勾「初始停用」（`startDisabled: true`），劇情走到才用 `togglePortal` `show=true` 打開。** 載入時就是關的，不會閃；也省掉一堆「進圖後封門」節點。
- `togglePortal` 的 `target` 是傳送點的**精確名稱**，可逗號列多個。狀態是執行期的，**換圖回來會重算**（回到初始停用＋鏈）。
- `enableFlag`（旗標成立就永久開）**少用**：只給「之後每次回來都該開著」的門。劇本裡這種門很少，掛了常在不該開的時候開著（例：回程打首領時去程的北門開著）。
- 回頭門（攻略圖往回走的門）永遠不開：初始停用、不接任何開門節點。驗證會對它報警告，確認是刻意的就好。
- 「看得到卻踩不動」的門會被作者當 bug：**演出播放中傳送點一律不觸發**（CUTSCENE_DIRECTOR §4）。所以引路者走到門口消失**之後**才開門。

### 3.6 清光怪才開門

同一張圖所有出生點填**同一個 `waveGroup`**（例：`清場`），其中一顆的 `next` 接 `開前門`。群組全滅才推鏈。
⚠ 群組裡接 `next` 的那顆**不能是 `nextWhen=生出來時`**——那是給不會死的怪用的，生成當下就推鏈、全滅時反而不推（血狂之爭 21 血蝠踩過；驗證會抓）。

### 3.7 封門伏擊

`camZone`（塗踩點區、`zoom` 留空＝純踩點）`setFlag=xxAmbush` → `togglePortal` 關兩扇門 → 出生點用 `conditions=flag:xxAmbush|!flag:xxCleared`＋同一 `waveGroup`（可用重複產生：`spawnInterval`／`maxAlive`／`maxWaves`）→ 全滅 `next` → `togglePortal` 開出口 `setFlag=xxCleared`。

### 3.8 首領戰

`bossIntro`（頭目登場演出）→ 出生點（一次性）→ 全滅 `next` → 對話 → 開門／給道具／過關。
**兩隻首領同時打**：A 出生點 `nextWhen=生出來時` 接 B 的 `bossIntro`，A、B 同一 `waveGroup`，B 的 `next` 接戰後 ⇒ 兩隻都死才推。
同一隻怪在不同場合要不同掉落／數值 ⇒ **MonsterData 開新列**（雙首領版 39／40 只差 `DropTableId`），不要改原列。

### 3.9 引路 NPC（劇情演出版，零程式）

1. 大門 NPC：`conditions` 決定誰出現（＋`!flag:對話完成`），`disappearFlag` 在引路開始時消失，`next` 接引路演出。
2. 每張去程圖：`onEnter` → `playCutscene` 引路（演員從入口走到出口、`bubble` 講話、`despawn`），`lockInput=false` 讓玩家能自由走。
3. 演出結束 `next` → `togglePortal` 開出口。
演員一律用演出的 npc 演員，不是地圖 NPC。地圖 NPC 只負責「站在那裡、可以對話」。

### 3.10 演出（cutscenes）

- **有人走位用演出，純講話用 `drama`**。頭上泡泡用 `bubble`（文字＝LanguageTable），`bg=True` 邊走邊講。
- **演員大小**：`scale = 1.95 ÷ (idle 第一張可見像素高 ÷ 256) × NPC／怪物表的 Scale`；`dipan.actor()` 會自動算。
- **鏡頭**：`camera` 有給座標時 `zoom` 無效；只能平移。
- **Skip**：會把走位／轉向／現身／消失／寫旗標**快轉到最終狀態**，再照常交棒（PROBLEMS **J6**）。所以結尾狀態要設計成「跳過也合理」。
- 同圖結束時 npc 演員**留場**站著，下一段開演時才清；要他消失就明確 `despawn`。
- 一張圖可以有多段演出，用 `playCutscene` 的 `cutsceneId` 挑；`dipan.put_cutscene()` 新增或取代。

### 3.11 陣營互打（兩族＋玩家）

見 FACTION.md。劇本裡的組合：

| 要的效果 | 做法 |
|---|---|
| 兩族站著不動、玩家打不到 | `factionPeace` |
| 兩族互打做樣子、主力是玩家 | `factionWar`（互打傷害 1/100） |
| 兩族**真的**打（一方把另一方殺光再追玩家） | `factionWar` 勾 `fullDamage` |
| 玩家加入一方 | `joinFaction`（那族不打玩家、玩家打不到它） |
| 我方首領跟著玩家、會出手但沒殺傷力、敵人不理它 | `companion`（MonsterData 開同伴列：HP 高、`ContactDamage 0`）；換關卡自動解散 |

**教訓**：一開始做了「0 傷害表演戰」，作者實測說兩邊隔很遠在揮、看起來很怪，改成「敵族大軍真打、殺光守軍再追玩家」。**假打在這個引擎裡不好看；要演打仗就讓它真的打**，用數量差決定誰贏，用「只開一扇門」決定玩家往哪走。

### 3.12 給東西、過關

- `giveItem`（`itemId`）→ `clearLevel`。
- ⚠ **`clearLevel` 塗格子不會踩點過關**（PROBLEMS **B18**）：要嘛被鏈接（`onEnter`／對話後），要嘛用 `camZone` 踩點再接過去。
- 劇本的進入地圖由 MapsTable `IsLevelStart` 決定；模組顯示名在 `GameFlowManager.ModuleDisplayNames`（程式，加新模組時要補）。

---

## 4. 資料落點

| 東西 | 放哪 | 注意 |
|---|---|---|
| 對話（頭像對話框） | `DramaTable.csv`（Type 2＋TalkGroup）＋`DramaTalkTable.csv`（Group 同號，一句一列） | 一組對話一個 dramaId；Type 1＝大圖＋文字、靠近按 F |
| 頭上泡泡、選項文字、標題 | `LanguageTable.csv` | **每個劇本拿一個千位 id 段**，表裡用 `#` 註解列標出來（血狂之爭 3001–3099）；段落清單在 LOCALIZATION.md |
| 旗標 | `DipanProj_MapEditor/flags.json` | `id` 接最大號；劇本旗標一般 `scope=level`（關卡單次），`IsLevel`／`IsLife`／`ScopeLabel` 跟 scope 一致；**命名加模組前綴**（`bf…`） |
| 怪 | `MonsterData.csv` 新增列 | 同一隻怪不同用途開新列（同伴版、雙首領版）；`Faction` 欄是種族標籤 |
| 掉落 | `DropTable.csv` | 怪物列 `DropTableId` 指過去 |
| NPC | `NpcTable.csv`＋地圖 `npcs[]` | 外觀沿用怪物序列圖庫 |
| 地圖 | `DipanProj_MapEditor/Maps/<模組>/*.dipanmap` | 作者 Sync 後才進遊戲 |

---

## 5. 工具（`tools/scripting/`）

需要 Python 3；`actor_scale` 要 Pillow。

### 5.1 三支腳本

| 腳本 | 用途 |
|---|---|
| `validate_map.py <模組> [--chain]` | **改完必跑**。錯誤＝遊戲裡一定壞；警告＝多半是坑。抓：重名、接續不存在、成環、孤兒節點、旗標未登記、旗標有讀沒寫（跨圖）、各種 id 不存在（drama／語言／怪物／道具／演出／序列圖）、傳送點目標入口拼錯（跨圖）、初始停用卻沒人開的門、清場群組沒人推鏈、出生點初始停用＋條件旗標互斥、演出走位目標不可走、`clearLevel` 塗格子、`timedChoice` 填了 `next`。`--chain` 印出每張圖的觸發鏈，讀劇本用 |
| `map_grid.py <模組> <地圖名>` | 文字格子圖：`#` 牆、`.` 可走、`T` 傳送點、`N` NPC、`M` 出生點…。擺任何東西前先看 |
| `dipan.py` | 函式庫，見下。`next_free_id(表, 起, 迄)` 找 id 段裡第一個空號 |

### 5.2 改地圖的標準寫法

```python
import sys; sys.dont_write_bytecode = True; sys.path.insert(0, r'<repo>/tools/scripting'); from dipan import *
P = map_path('BloodFang', 'BloodFang_MainLobby')
m = load(P)
R = by_name(m)                                    # 名稱 → 觸發點
add(m, '對話-偷襲', 'drama', {'dramaId': '44', 'next': '開東門'})
add(m, '開東門', 'togglePortal', {'target': '傳送點-東方', 'show': True})
west = [[6,11],[7,11],[4,13],[5,13]]
spawn(m, '出生點-突襲', '16|17', west, requireFlag='bfJoinedVampire')       # 會檢查格子可走
spawn(m, '出生點-突襲-鏡像', '15|35', mirror_x(m, west), requireFlag='bfJoinedWerewolf')
x, y = center(m, 14, 14)
put_cutscene(m, cutscene('exit', [actor('lead', 'Blood Warlock', x, y, unit_scale=1.39)],
                         [step('bubble', 'lead', langId=3011, seconds=2), step('move', 'lead', 25.4, -13.4, speed=6),
                          step('despawn', 'lead')]))
save(P, m)
```

改完：`python3 tools/scripting/validate_map.py BloodFang`。

### 5.3 型別小抄（踩過）

- `params` 多數是**字串**（`"15"`、`"5"`）；勾選類是 JSON bool（`startDisabled`、`show`、`pause`、`fullDamage`）；`markerX/Y`、`focusX/Y` 是數字。
- 格子 `[x, y]`：x 往右、y 往下；世界座標 y 是負的。`center()`／`cell_of()` 互換。
- 一格塗一隻；出生點 0 格＝不生。

---

## 6. 坑清單（劇本相關）

| 症狀 | 原因 | 見 |
|---|---|---|
| 傳送門顯示著卻踩不動 | 演出播放中傳送點不觸發 | §3.5、CUTSCENE_DIRECTOR §4 |
| 門該關卻開著／該開卻關著 | `togglePortal` 是執行期狀態、換圖重算；`enableFlag` 讓它永久開 | §3.5 |
| `clearLevel` 踩了沒反應 | 它不吃踩點 | PROBLEMS **B18** |
| 按 Skip 後角色還在／頭上對話框還在 | 已修：Skip 快轉到最終狀態、演員隱藏時泡泡跟著收 | PROBLEMS **J6** |
| 兩族互打的傷害倍率沒生效（近戰全額） | 近戰招式另生傷害範圍物件結算；已修成從那個物件讀陣營 | PROBLEMS **F38** |
| 對話期間場上的怪全停住 | 對話面板會暫停遊戲；「打起來」的時間點是對話結束後 | — |
| 清場開門在怪一生出來就開了 | 群組裡接 next 的出生點填了 `nextWhen=生出來時` | §3.6 |
| 出生點永遠不生 | 「初始停用」＋條件旗標一起填（初始停用是一次性解鎖，沒人解鎖就不生） | TRIGGER_CHAIN §3.5 |
| 改了旗標範圍在遊戲裡沒生效 | 沒 Sync | PROBLEMS **I4** |
| 演員比遊戲裡同一隻 NPC 大很多／小很多 | 演員 scale 是純 localScale，不走自動高度對齊 | §3.10 |
| 鏡頭推近沒反應 | `camera` 有座標時 zoom 無效 | §3.10 |
| 鏈中間某顆條件不成立，後面全停 | 沒設 `onBlocked=跳過這顆繼續` | §3.3 |

---

## 7. 跟作者協作

- **回饋翻譯**：作者講的是玩家體驗，AI 要翻成機制。
  - 「看起來很怪」→ 演法不對，**換演法**，不是調數字（0 傷害表演戰 → 真打）。
  - 「玩家跟不上／不知道要去哪」→ 用「只開一扇門」＋「有東西追」逼方向，不要靠文字提示。
  - 「說了跟著你卻跑去打仗」→ 台詞和行為對不上；改台詞或改行為，挑便宜的那個（血狂之爭改成首領快步跑走、到下一張圖才以同伴出現）。
  - 「這個門不該顯示」→ 不只那一扇；把整條劇本同類的門一次改完（§3.5 就是這樣來的）。
- **台詞要短**、句尾要帶動作指示（「走右邊，快點！」），玩家才知道下一步。
- **作者佈置的東西不動**；要動先問。**不要幫作者填預設值**到既有資料列。
- 擴大範圍（順手修別的系統）前先問；修到全專案共用的程式（例：Skip、傷害結算）要在回報裡明講「影響所有劇本」。
- 回報時把「我估的」標出來（數量、座標、秒數），讓作者知道可以直接改哪裡。

---

## 8. AI 目前做不到／要靠作者的

- 看畫面：演出觀感、角色距離、動作是否自然。只能靠作者截圖回報；截圖非常有用，請作者多附。
- 跑遊戲：所有「未實測」都是真的沒測過。
- 新美術：頭目沒有序列圖時只能借圖。
- 可走層、地上物、背景。

---

## 9. 新劇本開工清單

- [ ] 建 `readme/<模組>/` 四份（README 貼口述原文）
- [ ] MapsTable 有每張圖、`IsLevelStart` 指對；`GameFlowManager.ModuleDisplayNames` 有模組名
- [ ] LanguageTable 拿一個千位 id 段並在 LOCALIZATION.md 登記
- [ ] 旗標名加模組前綴、登記 flags.json
- [ ] 每張圖 `map_grid.py` 盤點；作者佈置過的列出來不碰
- [ ] GAP：需求逐條對到 §3 的模式；對不到的寫規格先給作者看
- [ ] 疑問一次問完，附建議
- [ ] 一條線做完 → `validate_map.py` 0 錯誤 → 回報四段 → 等實測
- [ ] 傳送門全部初始停用、劇情開門；回頭門確認刻意
- [ ] PROGRESS／PROBLEMS／SCRIPT.md 同步
