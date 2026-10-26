# 血狂之爭 — 分批任務單（接手 AI 依序執行）

> 返回 [本資料夾總覽](README.md)｜規格見 [SCRIPT.md](SCRIPT.md)、[GAP.md](GAP.md)
>
> **順序就是依賴順序**：T0 → **T1＋T2＋T2b（同一輪）** → T3（S1）→ 作者實測 S1（**期間做 T8 頭目**）→ T4（S2）→ 實測 → T5（S3）→ T6（S4）→ T7。
> 每張任務單的「交付」做完就停、照 README §1.3 四段格式回報、**等作者回覆再開下一張**。
> **2026-10-15 修訂**：作者補充血統分流需求、選定補編輯器多段演出、頭目交給 AI 設計 ⇒ 新增 T2b、T8，T1／T3 內容調整。
> 每張都附「不要做」——那是範圍的邊界，碰到了就先問。

進度欄由接手 AI 更新（⬜ 未開始／🔄 進行中／✅ 交付待測／☑ 作者已驗收／❌ 卡住）：

| 任務 | 狀態 | 備註 |
|---|---|---|
| T0 開工查證 | ☑ | 2026-10-15 回報；作者：Q1–Q13 全部照建議 |
| T1 資料補齊 | ✅ | 2026-10-15 交付待測 |
| T2 `timedChoice` | ✅ | 2026-10-15 交付待編譯＋測 |
| T2b 編輯器多段演出 | ✅ | 2026-10-15 交付待編譯＋測 |
| T3 S1 去程＋和談＋表態 | ✅ | 2026-10-15 交付待測；三種血統各測一次 |
| T4 S2 血族線 | ✅ | 2026-10-15 依作者重訂流程**重做**（表演戰＋同伴＋東側攻略），交付待測 |
| T5 S3 狂族線 | ✅ | 2026-10-15 與 T4 同輪做完（鏡像；西側用作者佈置） |
| T6 S4 隱藏線 | ✅ | 2026-10-15 同輪做完（和談圍攻、長廊伏擊、雙首領掉藥） |
| T7 收尾記錄 | ⬜ | |
| T8 兩隻頭目 | ⬜ | GAP §9；等作者同意設計 |

---

## T0 開工查證（唯讀，不改任何檔）

**目標**：把規劃時「從文件推斷」的幾件事對到程式碼，確定後面怎麼寫。

**先讀**：`AGENTS.md` 全文；本資料夾四份；`readme/TRIGGER_CHAIN.md` §1–§3.5b、§5；`readme/CUTSCENE_DIRECTOR.md` §1、§2.5、§3、§4；`readme/NPC_SYSTEM.md` §2–§3；`readme/FACTION.md` §0、§2、§5；`readme/LEVEL_END_FLOW.md` 的 watchFlag／clearLevel 節；`readme/MapEditor_DESIGN.md` 的 `.dipanmap` 格式節；`readme/UI_SYSTEM.md`（T2 要用）。

**查證清單（每項寫一行「結論＋依據的檔案與行」）**：

1. `AppearCondition.cs`：`flag:` 比對的是旗標**名字**還是 **id**？（GAP §5）
2. `CutsceneDirector.cs`＋`TriggerChain.cs` 的 `playCutscene`：`cutsceneId` 是否真的能選第 0 段以外的段？（GAP §4）
3. `CutsceneDirector`：`lockInput=false` 時玩家是否真的可以自由移動（沒有被 `SetExternalHold`）？演出期間 `onEnter` 會等演出結束才點火，那**由 NPC 對話 `next` 啟動**的演出有沒有同樣的互斥？
4. `MapMonsterRespawner`／`MapLoader.SpawnMonstersFromMap`：一次性出生點 `startDisabled=true`、被鏈 `Activate` 後，會不會立刻生？（GAP §5）
5. `TriggerChain.Activate` 對 `watchFlag` 的處理：`watchFlag` 被 `fireOnFlag` 觸發後接 `next` 的路徑（確認 SCRIPT D9 的寫法成立）。
6. `TriggerChain` 的 `togglePortal`：target 名字比對是 `name` 還是 `id`？大小寫／空白敏感？
7. 紅嫁衣 `RedBridalGown_BridalRoom.dipanmap` 的 trigger 層：把 `params` 裡通用欄位的**實際鍵名**（`requireFlag`／`startDisabled`／`repeat`／`onBlocked`／`conditions`…）抄一份成對照表，JSON 一律照它寫。
8. `DipanProj_MapEditor/flags.json` 目前最大 id；`MonsterData.csv` 最大 ID；`DramaTable`／`DramaTalkTable`／`LanguageTable` 最大 id——寫進回報，T1 接號用。
9. `GameFlowManager.ModuleDisplayNames` 的寫法（T2 要加一行）。
10. `BossIntroPanel`／`ConfirmPopup`／`SelectScriptPanel` 三個面板的建構方式（T2 抄哪個）。

**交付**：一段回報（README §1.3 格式）＋ README §4 的 Q1–Q9 請作者拍板。
**不要做**：任何寫入。

### T0 結論（2026-10-15，後面的任務單照這裡寫）

| # | 結論 |
|---|---|
| 1 | `flag:` 條件比對**名字** |
| 2 | 遊戲端 `cutsceneId` 可選任一段（`FindCutscene`，忽略大小寫）；編輯器只編第 0 段、「刪除演出」會全清 → T2b |
| 3 | `lockInput=false` 玩家可自由移動、可按 F；`playCutscene` 遇到演出在跑會略過並直接接 next（不排隊） |
| 4 | 一次性出生點勾 `startDisabled` → 交給 respawner，被鏈解鎖後下一幀生；B6c 組合照程式推演成立（未實測） |
| 5 | `OnCompleted`：先 `SetFlag`（watchFlag 延一幀）再 `Activate(next)` |
| 6 | `togglePortal` target：每段 Trim 後**精確比對名稱**、再比 id；**不限 typeId**，傳送點不可與其他 trigger 撞名 |
| 7 | 鍵名：`next` `requireFlag`（否定 `!名`） `setFlag` `enableFlag` `conditions` `onBlocked`（`跳過這顆繼續`） `repeat`（`每次`/`每周目`/`永久`） `nextWhen`（`生出來時`） `fireOnFlag` `deathFlag` `maxWaves` `waveGroup` `spawnInterval` `maxAlive` 為**字串**；`startDisabled` `show` `pause` 為 **JSON 布林**；`markerX/Y` `focusX/Y` 為 **JSON 數字**。紅嫁衣 BridalRoom 有自環＋非法 `repeat` 值，**不要照抄**；全專案尚無地圖用過 `conditions` |
| 8 | 最大號：flags 11、MonsterData 36、DramaTable 43（TalkGroup 40）、DramaTalkTable 65（Group 40）、LanguageTable 3xxx 段空 |
| 9 | `GameFlowManager.cs:184` `ModuleDisplayNames` 是 Dictionary，加一行 |
| 10 | `ChoicePanel`：特性抄 `SelectScriptPanel`（Window／PausesGame／CloseOnEscape=false／Backdrop）、外框按鈕抄 `ConfirmPopup`、`static Show(...)`＋unscaled 計時抄 `BossIntroPanel` |
| 追加 | 演出留場的 npc 演員在**下一段演出開演時**被清（`StartCutscene` 開頭清 `_standing`）；兩族互打恆為 ×1/100；22 餐廳南門 targetEntrance 打錯字（G11） |

---

## T1 資料補齊（CSV／JSON，零程式）

**目標**：把 GAP §6 的 G1–G11 補齊（定案版），讓後面的編排有東西可指。

**先讀**：GAP §6；`readme/LOCALIZATION.md` 的 CSV 格式與分段慣例；`readme/MONSTER_SETUP.md` 的「CSV 表尾欄位總表」（只看欄位意義）；`readme/DRAMA.md` 的 DramaTalkTable 欄位。

**做法**：
1. `MonsterData.csv`：G1／G2／G4 只改指定列的指定欄（用 python csv 讀→改→寫，**保留原表頭與其他列逐字不變**，用 `git --no-optional-locks diff` 確認只動了那幾格）；G3 新增 37–40 四列（複製來源列再改欄）。
2. `flags.json`：G5 追加七筆（id 12–18）。
2b. `MapsTable.csv`：G8（17→1、20→0）。`BloodFang_Diningroom.dipanmap`：G11 修一個字串（只改那個值，其餘逐字不變）。
3. `LanguageTable.csv`：G6，3001–3023（SCRIPT §6.2），cn 照表、en 自己翻；`LOCALIZATION.md` 分段那行補上。
4. `DramaTable.csv`＋`DramaTalkTable.csv`：G7，對話句子**自己寫初稿**（每段 2–4 句，語氣照 SCRIPT §6.1「方向」欄；主角句用 `Actor_Normal`）。
5. 每個 CSV 改完用 python 重讀一次確認欄數一致、沒有多出逗號。

**交付**：改動清單（表）＋對話全文貼在回報裡讓作者改字＋提醒 G2 的副作用。
**不要做**：改既有列的其他數值（HP／Speed…；`InvincibleTimeMs` 與 `Faction` 是 Q12／G2 拍板的例外）；新增劇本道具。

---

## T2 新功能 `timedChoice`（程式）

**目標**：GAP §2 的規格，做到「編輯器能擺、遊戲能跑、三條路只走一條」。

**先讀**：GAP §2 全部；`readme/UI_SYSTEM.md`；`readme/TRIGGER_CHAIN.md` §5；T0 第 9、10 項的結論；PROBLEMS **D13**（具名輸入鎖）、**I2/I5**（static 殘留）、**I10**（整段刪除誤刪欄位的檢查法）。

**做法**：
1. `TriggerChain.cs`：加型別常數、`Activate` 加 case、參數讀取（照 `bossIntro` case 的讀法）。
2. 新檔 `Scripts/UI/Panels/ChoicePanel.cs`：`UIPanel`、Window 層、`PausesGame`、unscaled 倒數、三個回呼、不可 ESC；美術用 `UIBuilder` 通用元件。
3. 編輯器 `TriggerType.cs` `Defaults()`＋`triggerTypes.json` 兩處加型別（八個參數、顯示名「二選一倒數(鏈動作)」）；`nextA/nextB/nextTimeout` 欄接「選」清單（抄 `next` 欄）。
4. `PlayModeStaticReset.cs` 登記（若有 static）。
5. `GameFlowManager.ModuleDisplayNames` 加 `BloodFang`。
6. 沒有編譯器：改完跑 I10 的「用到但沒宣告」檢查；`grep` 確認新型別字串在遊戲端與編輯器端**拼法一致**。

**交付**：改動檔清單＋GAP §2.5 驗收清單（請作者編譯＋在任何一張測試地圖擺一顆接兩個 `giveItem` 試）。
**不要做**：美術；改 `playerHint`／`SelectScript` 既有行為；順手重構 `TriggerChain` 的 switch。

---

## T2b 編輯器多段演出（程式，MapEditor 專案）

**目標**：GAP §8——劇情分頁能選段、新增、複製、只刪當前段。
**前置**：無（與 T1／T2 同輪）。
**先讀**：GAP §8；`readme/CUTSCENE_DIRECTOR.md` §1、§2；PROBLEMS **I3/I5/I7**（編輯器靜態快取）。
**做法**：照 GAP §8.2 掛點表；`SelectedIndex` 在換圖、Undo 還原（`MapSession.RestoreFromJson`）後夾回範圍；所有 `MainCutscene` 呼叫點改 `Current`（改完 `grep MainCutscene` 只剩 `MapData.cs` 定義）。
**交付**：改動檔清單＋GAP §8.3 驗收清單。
**不要做**：改主遊戲端；改 `.dipanmap` 格式；順手做「條件分支」或「多段自動播」。

## T3 Slice 1：去程＋和談＋表態（編排）

**目標**：SCRIPT §3.0–§3.4 的 A1–A4、B1–B3、C1–C2、D1–D8、D8b 與 D6/D6b 表態分流（到加入陣營／選擇面板按下去、Console 有旗標 log 為止；D9 以後留給 T4）。

**前置**：T1 完成；T2、T2b 作者已驗收。

**先讀**：SCRIPT §3.0–§3.4、§7；README §2；CUTSCENE_DIRECTOR §1、§3、§8；T0 第 7 項的鍵名對照（TASKS 下方〈T0 結論〉）。

**做法（每張圖一個小步驟，做完一張驗一張 JSON）**：
1. **17 大門**：三隻 NPC 的 `conditions` 各補 `|!flag:bfTalkDone`、`disappearFlag=bfGuideStarted`、`next` 各接自己的 `引路-大門-*`；三顆 playCutscene（0 格）；`cutscenes` 寫三段 `guide_gate_*`（演員起點＝該 NPC 座標）。
2. **18 迎賓大廳**：`進場-去程` → `封側門-去程` → 三顆引路串（§3.0 第一種，`onBlocked=跳過這顆繼續`）；三段 `guide_lobby_*`。
3. **19 長廊**：同上，`guide_gallery_*`。
4. **20 和談大廳**：`進場-和談` → `三方和平` → `封東西門-和談` → `和談演出`（第 0 段 `talk`）；五顆表態 watchFlag＋四顆入營 drama＋`選邊`（timedChoice）；`加入血族`／`加入狂族`／`拒絕選邊`／`開戰`；NPC 分頁 6 隻士兵 NPC；第 1 段 `leaders_exit`（演員起點＝`talk` 結尾兩首領站位）。
5. 每張圖：`json.load` 驗證；小腳本列出所有 `name → next`（含 `nextA/nextB/nextTimeout`、NPC 的 `next`、`fireOnFlag` 監聽關係）確認無環、無指向不存在的名字；`cutsceneId` 都對得到某段 `id`；估的座標整理成表。
6. 位置估法見 README §2.2；演出 `move` 目標一定在可走格（CUTSCENE_DIRECTOR §9）。

**交付**：四張圖的改動表＋位置表＋「請作者做什麼」（開編輯器逐張看三段演出、Sync、**用人類／血族／狂族三隻角色各測一次**、再用該隱或芬里爾測三階對話）＋驗收清單（SCRIPT §8 S1 展開）＋GAP §5 的「壞了優先懷疑」對照。
**不要做**：D9 以後；動 21–26；改任何程式。

---

## T4 Slice 2：血族線（編排）

**目標**：D9a、D10、§3.5 左路、§3.6 B4/B5/B6a/B7、A5。
**前置**：T3 作者驗收通過（尤其陣營和平與面板）。首領 38 若 T8a 還沒完成，先用替身 Brain（`LeapSlam`）跑通流程。
**先讀**：SCRIPT §3.4 D9–D10、§3.5、§3.6、§3.1 A5；TRIGGER_CHAIN §3.5（出生點欄位）、§3.5b；LEVEL_END_FLOW。

**做法**：
1. **20**：`分流-血族`（watchFlag）→ `開西門` → `封南門-血族` → `對話-逃向西側`；`出生點-血族衛兵`／`出生點-狂族衛兵`（條件旗標 `bfWarStarted`，兩片靠近）。
2. **21／22／23**：各自 `進場-逃亡`（onEnter, `bfJoinedVampire`）→ 封回路門（只有 21 需要；22／23 的北門是來路，**也封**，否則玩家可以回頭）；追兵出生點（條件旗標 `bfJoinedVampire`、間隔／上限／波數照表、塗靠來路門）；援軍（21 要、22 可省、23 要）。
3. **18**：`進場-回程-血族` → `封全門-血族` → `首領-狂族`（bossIntro 38）→ `出生點-狂族首領`（38、startDisabled、1 格）→ `對話-首領敗北-狂族` → `開大門`（setFlag `bfBossDead`）。
4. **17**：`大門-離開`（clearLevel 踩點 2×2、`requireFlag=bfBossDead`、`delaySeconds=3`）。
5. 驗證同 T3 第 5 步；**另外畫一張跨圖流程**（17→…→17）確認每個門在每個階段的開關狀態沒有把玩家鎖死。

**交付**：同 T3 格式；驗收清單＝SCRIPT §8 S2 展開＋「每一間房追兵有出現、己方不打我、回頭的門是關的、首領死後南門亮、大門踩點進結算」。
**不要做**：狂族線；隱藏線；調怪物數值（追兵太強／太弱只回報，作者決定）。

---

## T5 Slice 3：狂族線（編排，鏡像）

**目標**：D9b、§3.5 右路（24／25／26）、B4'/B5/B6b。
**前置**：T4 驗收通過。
**做法**：把 T4 的結構鏡像過去（旗標換 `bfJoinedWerewolf`、追兵換血族系 `15|36`／`15|35`／`36|35`、援軍換 `16`／`17`／`18`、門換東側、首領換 39）。右路目前**沒有任何出生點**，援軍一定要擺。
**交付**：同 T4。
**不要做**：順手「對稱整理」左路已驗收的東西。

---

## T6 Slice 4：隱藏線（編排）

**目標**：D7c/D9c、§4 長廊回撤、B4''/B5/B6c（**只有非血狂族玩家會走到**；雙首領用 40／41）。
**前置**：T5 驗收通過。
**先讀**：SCRIPT §4、B6c 的退路；GAP §5 第 5 列。
**做法**：
1. **20**：`拒絕選邊`（factionWar, setFlag `bfRefused`, next `開戰`）已在 T3 擺好；`分流-拒選`（watchFlag）→ `對話-兩族追殺`。
2. **19**：兩顆追兵出生點（條件旗標 `bfRefused`、`waveGroup=長廊追兵`）。
3. **18**：`進場-回程-拒選` → `封全門-雙` → `首領-雙-狂族` → `出生點-狂族首領-雙`（`nextWhen=生出來時`）→ `首領-雙-血族` → `出生點-血族首領-雙` → `對話-雙首領敗北` → `開大門`。
4. 若 B6c 的組合實測不成立，照 SCRIPT 的退路改。
**交付**：同 T4。
**不要做**：改 `MapMonsterRespawner` 讓組合成立（那是擴大範圍，先問）。

---

## T8 兩隻頭目（程式＋素材）

**目標**：GAP §9 的嘯月（38/40）與緋夜（39/41）。
**前置**：作者同意 GAP §9 的設計；T1 已建好 38–41 的替身列。**可以在 T3 交付後、作者實測 S1 的期間做**（檔案不重疊）。
**先讀**：GAP §9；`readme/BOSS_MODULE.md` §1、§3、§6.7、§8.4、§9（全部）、§10；`readme/MONSTER_SETUP.md` 表尾欄位；PROBLEMS **F19、F25、F28、F29**、**G13**（怪物 pivot 在畫布中心）、**I10**。

| 子單 | 內容 | 交付 |
|---|---|---|
| T8a | `WerewolfLordBrain`＋狼嚎召喚配方；38/40 `BrainType` 換過去 | 程式＋BOSS_MODULE §16（§13 菁英血衛士、§14 夏綠蒂公爵、§15 劇本同伴已使用）＋「請作者在競技場單獨擺一隻測」的驗收清單 |
| T8b | ~~`VampireLordBrain`＋`BloodPool`＋扇射彈武器＋喚蝠召喚配方；39/41 換過去~~ ⏸ **2026-10-26 暫停**：39／41／42 已換成夏綠蒂公爵（`DukeCharlotteBrain`，BOSS_MODULE §14） | — |
| T8c | AI 自繪序列圖（GAP §9.4 B 階段），附一張「各動作拼成一張」的預覽圖給作者先看 | 圖檔＋預覽；作者同意才把 `Name` 換過去 |

**不要做**：改 `LeapSlamBrain`／`MeleeChaseBrain`／`ArcherBrain` 本體（要共用就抽出來或照抄，改既有 Brain 會影響其他怪）；改 `GroundEffectTable` 的傷害路徑（BOSS_MODULE §9.4）；把首領武器加進 ItemTable。

---

## T7 收尾記錄

**目標**：讓下一個接手的人（或作者自己）不用重讀對話紀錄。
**做法**：
1. `readme/PROGRESS.md` 加**一條**（格式 DOCS_GUIDE §四）：血狂之爭劇本編排＋`timedChoice`；重點寫「AI 直接編 `.dipanmap` 的做法與踩到的坑」「哪些未驗證系統第一次實測的結果」「通則」。
2. 踩到的新坑 → `readme/PROBLEMS.md`（分類接最大號；`.dipanmap` 相關多半是 C 段或 B 段）。
3. `readme/TODO.md` 檔尾新節「血狂之爭劇本（日期）」：GAP §7 的不做清單＋實測後的缺口。
4. 本資料夾 README 檔頭狀態列與本檔進度表更新；`readme/README.md` 文件地圖那行的狀態同步。
5. `readme/TRIGGER_CHAIN.md` §3 加 `timedChoice` 一段（現狀文件，不是 PROGRESS）；`CUTSCENE_DIRECTOR.md` §5「目前用在哪」加血狂之爭四張圖。
6. DOCS_GUIDE §七的收尾檢查跑一遍（連結、編號）。
7. `readme/BOSS_MODULE.md` §16／§17（T8 時已寫）、`CUTSCENE_DIRECTOR.md` §9 把「編輯器只編第 0 段」改成已解；提醒作者專案根目錄有一個空的 `_claude_tmp/`（AI 誤建、沒刪除權限）可自行刪掉。
**交付**：改了哪些文件的清單。
