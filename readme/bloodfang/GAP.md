# 血狂之爭 — 缺口分析與功能規格

> 返回 [本資料夾總覽](README.md)｜事件表見 [SCRIPT.md](SCRIPT.md)｜任務順序見 [TASKS.md](TASKS.md)
>
> 結論先講：作者列的五件事裡，原本只有一件要寫程式（二選一倒數面板）。**2026-10-15 作者補充需求後變成三件新功能**：① `timedChoice` 倒數面板（§2）② 編輯器多段演出（§8，因為引路人要依血統換）③ 兩隻頭目 Brain（§9，作者交給 AI 設計）。其餘全是填 `.dipanmap` 與 CSV。另有一批「資料缺口」（§6）要先補，不補的話編排寫了也不會動。

---

## 1. 能力對照表（作者的需求 → 現有機制）

| 作者提的需求 | 現有機制 | 文件 | 判定 |
|---|---|---|---|
| 布敵人的 trigger（追兵、波次） | `monsterSpawn`＋`spawnInterval`／`maxAlive`／`maxWaves`／`waveGroup`／`requireFlag`（條件旗標＝旗標一成立就開始生）、一次性出生點全滅推 `next` | TRIGGER_CHAIN §3.5、§3.5b | ✅ 純資料 |
| 傳送點開啟關閉時機 | `togglePortal`（多筆 target、show on/off；執行期狀態、換圖重算）＋ `startDisabled`／`enableFlag` | TRIGGER_CHAIN §3 | ✅ 純資料 |
| NPC 帶路時的劇情 | **Cutscene**：npc 演員 `move`／`bubble`／`despawn`、`lockInput=false` 可讓玩家自己跟著走；由 `playCutscene` 鏈動作或 NPC 對話的 `next` 啟動 | CUTSCENE_DIRECTOR §1、§2.5、§8 | ✅ 純資料（方案 A，§3）；真正「跟隨型 NPC」是新功能（方案 B，本批不做） |
| 和談大廳的劇情 | Cutscene（多演員走位＋`bubble` 互嗆＋`camera`）＋ `setFlag` 交棒觸發鏈 | CUTSCENE_DIRECTOR | ✅ 純資料 |
| 二選一的倒數元件 | **沒有**。最接近的是 `playerHint`（有 `pause`、有收起時機、但沒有選項與逾時分支）與 `SelectScript` 面板（選劇本，固定語意） | — | ❌ **新功能 `timedChoice`**（§2） |
| 選邊後被另一邊攻擊／不選兩邊都攻擊 | `factionPeace`→`factionWar`→`joinFaction`；預設敵對、和平是特例；結盟切 Ally 層 | FACTION §0、§2 | ✅ 純資料（⏳ 系統未實機驗證，§5） |
| 依血統出現不同接待員、講不同話 | NPC `conditions`（`series:2`）＋`conditionalDramas`（`bloodline:22`） | TRIGGER_CHAIN §2.6、NPC_SYSTEM §2 | ✅ 純資料（⏳ 未實機驗證） |
| 頭目登場表演 | `bossIntro`（`monsterId`→讀 MonsterData 的 DisplayName／PortraitPath） | TRIGGER_CHAIN §3 | ✅ 純資料（首領要有怪物列，§6） |
| 從大門離開＝過關 | `clearLevel` 踩點＋`requireFlag` | LEVEL_END_FLOW | ✅ 純資料（＋`ModuleDisplayNames` 一行程式） |
| 回程進同一張圖走不同劇情 | 多顆 `onEnter` 各掛 `requireFlag`，依序點火各查條件 | TRIGGER_CHAIN §3 onEnter | ✅ 純資料 |
| 選邊後三路分流 | `watchFlag`（旗標首次成立起鏈） | LEVEL_END_FLOW | ✅ 純資料（SCRIPT D8 註解有「為什麼不能用鏈中條件」） |
| **依玩家血統換接待／引路的人**（2026-10-15 補） | NPC `conditions`／`conditionalDramas`（大門現況已做好）＋多段演出、鏈上三顆 `playCutscene` 各掛 `conditions` | TRIGGER_CHAIN §2.6、SCRIPT §3.0 | ✅ 資料；⚠ 編輯器只能編第 0 段 → **新功能 §8** |
| **血族／狂族玩家沒有選擇、人類才有選擇與隱藏線**（2026-10-15 補） | 和談演出 `setFlag` → 五顆 `watchFlag` 各掛 `conditions` 分流 | SCRIPT §3.4 D6 | ✅ 純資料 |
| **頭目攻擊方式**（2026-10-15 補，交給 AI 設計） | 沒有對應 Brain | BOSS_MODULE | ❌ **新功能 §9** |

---

## 2. 新功能規格：`timedChoice` 二選一倒數（鏈動作）

### 2.1 行為

被鏈啟動時**暫停遊戲、鎖輸入**，開一個置中面板：標題一行、兩個按鈕、底下一條倒數。玩家點 A → 關面板、`Activate(nextA)`；點 B → `Activate(nextB)`；倒數歸零 → `Activate(nextTimeout)`。三條路**互斥、只走一條**。純鏈驅動、玩家踩不觸發、格子畫角落或 0 格。

### 2.2 參數（編輯器面板欄位／JSON `params` 鍵）

| 鍵 | 編輯器標籤 | 型別 | 意義 |
|---|---|---|---|
| `textId` | 標題(語言表id) | int | 面板標題，`Language.GetText` |
| `subTextId` | 副標(語言表id/空=無) | int | 倒數旁的小字（SCRIPT 用 3023「沉默即是與兩族為敵」） |
| `optionA` | 選項A(語言表id) | int | 按鈕 A 文字 |
| `nextA` | 選A接續 | trigger 名 | 同 `next` 語意（名稱優先、再比 id） |
| `optionB` | 選項B(語言表id) | int | 按鈕 B 文字 |
| `nextB` | 選B接續 | trigger 名 | — |
| `seconds` | 倒數秒(空=15) | float | 0 或留空時仍要有個預設，**不允許無限等**（劇本需要逾時分支） |
| `nextTimeout` | 逾時接續 | trigger 名 | 留空＝歸零只關面板、不接任何東西 |

通用欄位照常（`requireFlag`／`setFlag`／`repeat`…）；**`next` 欄不用**（三條路已經各有欄），填了印一次 Warning 並忽略，免得作者以為它會接。

### 2.3 程式掛點（照既有慣例，全部都有範本可抄）

| 動什麼 | 範本 | 說明 |
|---|---|---|
| `Scripts/Map/TriggerChain.cs` | `bossIntro`／`playerHint` 的 case | 加 `TypeTimedChoice = "timedChoice"`；`Activate` 的 switch 加 case → 讀參數 → `ChoicePanel.Show(...)`，三個回呼各自 `TriggerChainRunner.NextFrame(() => Activate(nextX))`（對話接對話那條「延一幀避免重入」的理由，TRIGGER_CHAIN §5） |
| `Scripts/UI/Panels/ChoicePanel.cs`（新） | `BossIntroPanel`／`ConfirmPopup`／`SelectScriptPanel` | `UIPanel`、`UILayer.Window`、**`PausesGame=true`**；全程式建構（UI_SYSTEM.md）；倒數用 **`Time.unscaledDeltaTime`**（遊戲是暫停的）；**不可 ESC 關閉**（關了就沒有任何分支被走到＝鏈卡死）；`OnClose` 若面板是被別的流程關掉（換圖／死亡）→ 不接任何 next、印 Warning |
| `DipanProj_MapEditor/.../TriggerType.cs` `Defaults()` ＋ `Assets/StreamingAssets/triggerTypes.json` | `bossIntro` 那筆 | **兩處同步**（FACTION §7 同一條雷）；新型別要**重開編輯器**才合併進 json（LEVEL_END_FLOW 雷點）。顯示名「二選一倒數(鏈動作)」。`nextA`／`nextB`／`nextTimeout` 欄要有「選」按鈕列本圖 trigger（抄 `next` 欄的做法） |
| `Scripts/PlayModeStaticReset.cs` | 既有列表 | 若 `ChoicePanel` 有任何 static（實例快取／`_quitting`）就登記 `ResetForPlayMode`（PROBLEMS I2/I5） |
| `Assets/Data/LanguageTable.csv` | — | 面板沒有任何寫死字串；全部 `textId` 來 |
| `Scripts/Flow/GameFlowManager.cs` `ModuleDisplayNames` | 既有列 | 順手加 `{"BloodFang","血狂之爭"}`（結算畫面用；與本功能無關但同一批） |

**美術**：第一版用 `UIBuilder` 的通用框＋文字＋兩顆按鈕＋一條 `Image.fillAmount` 倒數條即可，**不要先做美術**（作者之後給圖再換）。風格對齊 `ConfirmPopup`。

### 2.4 邊界

- 倒數期間玩家不能開背包／設定（面板在 Window 層、PausesGame；既有閘門會擋）。
- 面板打開時若已有對話面板開著（鏈上一顆是 drama，drama 完成＝面板關閉才接 next，所以不會發生；但 Cutscene `dialogue` 步驟結束與 `playCutscene` 接 next 也延一幀）——仍加防呆：`UIManager` 有其他 Window 層面板開著就延後一幀再開。
- 兩個 next 都空、逾時也空 → 等於「只是暫停一下」，印 Warning。
- 無存檔單場景測試照常可用（不碰 SaveManager）。

### 2.5 驗收（作者照打勾）

- [ ] 編輯器 trigger 型別清單出現「二選一倒數(鏈動作)」、八個欄位都在、`nextA/B/Timeout` 有「選」。
- [ ] 鏈接到它：遊戲暫停、面板出現、標題／選項／副標都是語言表的字（沒有 `[lang:id]`）。
- [ ] 按 A → 面板關、Console 出現 A 那條鏈第一顆的 log（例：「玩家結盟：Vampire」）；B 同理；什麼都不按 15 秒 → 逾時鏈。
- [ ] 三種情況都**只有一條**鏈被走到（Console 不會同時出現兩族的 log）。
- [ ] 倒數在對話／背包不可能同時開的前提下走 unscaled time（面板開著時 `Time.timeScale=0` 仍會倒數）。
- [ ] 第二次 Play（Domain Reload 關閉）面板仍正常（PROBLEMS I5 那類殘留）。

---

## 3. NPC 引路：兩個方案

### 方案 A（本批採用）：劇情演出，零程式

每張去程地圖一段 Cutscene（`autoStartOnEnter=false`，由 `onEnter`→`playCutscene` 或 NPC 對話 `next`→`playCutscene` 啟動）：管家演員在入口現身、`bubble` 一句、`move` 到出口傳送點前、`face` 回頭、`bubble` 一句（`background`）、`despawn`。`lockInput=false` 讓玩家自己走。

限制（接手 AI 要知道、作者要看效果）：
- **玩家可以不理他**——演出跑完管家就消失，玩家慢慢走也沒事；但「管家等玩家」做不到。
- **演出期間傳送點不觸發**（CUTSCENE_DIRECTOR §4）：玩家比管家先到門口要等演出結束才能過；演出最長＝走位秒數＋兩句 bubble，可接受。
- ~~一張圖只有一段演出、引路固定管家~~ → **2026-10-15 修訂**：引路人依血統換（血族接待員／狂族接待員／管家），每張去程地圖三段演出，靠 §8 編輯器多段 UI；和談大廳那段仍共用一段（管家當司儀，SCRIPT D5）。
- `lockInput=false` 時 HUD／暫停行為：文件沒寫明，**T0 要查 `CutsceneDirector` 確認 `lockInput=false` 不會把玩家 `SetExternalHold`**。
- `skippable=true` 時玩家按 Skip 會快轉到結尾（管家直接消失）——合理。

### 方案 B（Phase 2，作者看完 A 再決定）：`NpcBrain` 加 `lead` 行為

NPC_SYSTEM §7 已預留插座（`behavior` 是字串欄、`escort` 模式構想「玩家在半徑內才沿路徑前進」）。引路＝護送的反向：**玩家在半徑內 NPC 才往前走下一個路徑點，玩家太遠就停下來等（轉身面向玩家）；走到最後一點後寫 `setFlag`／接 `next`／可選消失。** 編輯器只要在「行為」加一個選項、沿用既有路徑點 UI；主遊戲 `NpcBrain` 加一個 case、`NpcInstance` 不加欄。工時約半天，但**跨圖要每張圖各擺一隻**（NPC 隨換圖重生），與方案 A 一樣。

---

## 4. Cutscene 多段：T0 查證結果

- **遊戲端支援**：`playCutscene` 的 `cutsceneId` → `MapData.FindCutscene(id)`，id 比對忽略大小寫與前後空白；留空＝第 0 段（`Scripts/Map/MapModel.cs:47`、`CutsceneDirector.PlayById`）。
- **編輯器只編第 0 段**：`CutsceneController` 與 `EditorUI.DrawCutscenePanel` 都寫死 `MainCutscene`；存檔會保留整份清單（AI 直接寫進去的第 1 段以後不會被作者存檔洗掉），但「刪除演出」是 `cutscenes.Clear()`，會全刪。
- **結論**：本劇本需要每張去程地圖三段引路演出（SCRIPT §3.1–§3.3），作者選擇**補編輯器多段 UI**（§8），T2b 實作。
- 另外查到：演出結束後留在場上的 npc 演員，會在**下一段演出開演時**被清掉（`StartCutscene` 開頭清 `_standing`）——SCRIPT D8b 的「首領退場」演出就是利用這一點。

## 5. 會用到、但「程式完成未實機驗證」的系統（S1 實測前先知道會壞在哪）

| 系統 | 狀態 | 壞了優先懷疑 |
|---|---|---|
| 三方陣營（`factionPeace`／`factionWar`／`joinFaction`、切層、演戲傷害） | ⏳ 2026-08-28 程式完成、2026-09-17 預設反轉**未編譯未實測** | FACTION §6 的 A→B→C→D 清單逐項測；最可能：`MonsterData` 的 `Faction` 欄沒填（§6）、互毆怪沒 `InvincibleTimeMs`（PROBLEMS F5） |
| 偵測條件（NPC `conditions`／`conditionalDramas`） | ⏳ 2026-09-15 未編譯未實測 | 格式字串拼錯（`series:2\|!flag:bfTalkDone`）；無存檔測試時血統＝人類，只會看到管家（TRIGGER_CHAIN §2.6「無存檔測試時」）——**要測血族版請用測試選單進關且角色有血統** |
| `flag:` 條件存的是名字還是 id | ✅ T0 查證：**名字**（編輯器輸入 id、確認後寫入名字；`AppearCondition` 用名字查 `FlagTrue`） | — |
| 一次性出生點 `startDisabled`＋被鏈解鎖後立刻生 | ✅ T0 查證：勾初始停用＝`gated` → 交給 respawner，解鎖後下一幀生 | — |
| `waveGroup` 內一顆 `nextWhen=生出來時`、另一顆全滅推鏈（SCRIPT B6c） | 未驗證組合 | 退路寫在 SCRIPT B6c |
| `togglePortal` 封門後，從被封的傳送點**落點**進圖會不會被立刻送回 | 文件說封門＝踩踏功能停用（進停用集） | 不會才對；會的話改成「先進圖再封」（onEnter 已經是進圖後點火） |
| 血蝠（35）飛行、血術士（36）射手 | ⏳ 2026-10-11 未實測 | 追兵組合先不用 35；36 壞了換 15 |
| `bossIntro` 讀替身的 `PortraitPath` | 首領替身借士兵立繪 | 路徑填 `Modules/BloodFang/Talk/WerewolfSoldier_normal`（catalog id，不含副檔名） |
| 同圖多顆 `onEnter` 依序點火、各查條件 | 文件寫明 | 若兩顆都點火（條件沒擋住）→ 檢查 `!bfTalkDone` 是否存成 `!` 前綴 |
| 鏈中「三選一」串法（`conditions`＋`onBlocked=跳過這顆繼續`） | 程式路徑 T0 已讀過（`TriggerChain.Activate` 340–363 行），但全專案**還沒有任何地圖用過 `conditions` 鍵** | 三段引路都沒播 → 看 Console「條件不成立 → 跳過」那行，多半是條件字串拼錯 |
| 兩族互打是演戲傷害 ×1/100 | 程式事實（`FactionRelations.DamageMultiplier`） | 己方援軍／衛兵打不死敵人、只是吸引火力；**這是設計，不是 bug** |

---

## 6. 資料缺口（T1 要補的，全部是 CSV／JSON，零程式）

Q1–Q13 已拍板（README §4），下表是定案版。

| # | 檔 | 缺什麼 | 補法 |
|---|---|---|---|
| G1 | `MonsterData.csv` | 16 狂族士兵 `Faction` 空 | 填 `Werewolf`。不動其他欄 |
| G2 | `MonsterData.csv` | 17／18／19、35／36 `Faction` 空 | 17/18/19 填 `Werewolf`、35/36 填 `Vampire`。副作用（Q13，作者已知）：在其他地圖與 Enemy 陣營的怪不再互打 |
| G3 | `MonsterData.csv` | 沒有首領怪 | **新增 38／39／40／41**（GAP §9）：38 嘯月（複製 19 的列、`BrainType=LeapSlam` 替身 → T8 換 `WerewolfLord`）、39 緋夜（複製 36、替身 `Archer` → `VampireLord`）、40／41 雙首領版（HP ×0.6）。`DisplayName`／`PortraitPath`／`Faction`／`Scale 1.4`；句子欄放二階段喊話（`40%: …`） |
| G4 | `MonsterData.csv` | 互毆怪 `InvincibleTimeMs` 全是 0 | **15–19、35、36（Q12 拍板：全部部族怪）**＋新增 38–41 填 400（FACTION §5.3、PROBLEMS F5） |
| G5 | `flags.json` | 七個 `bf*` 旗標 | id 12–18，`scope=level`（SCRIPT §5，含新增的 `bfGuideStarted`） |
| G6 | `LanguageTable.csv` | 3001–3099 段 | SCRIPT §6.2；`LOCALIZATION.md` 分段那行補上 |
| G7 | `DramaTable.csv`／`DramaTalkTable.csv` | 44–53／Group 41–50；句子 ID 從 66 起 | SCRIPT §6.1 |
| G8 | `MapsTable.csv` | `IsLevelStart` 在 20 | **Q1 拍板**：17 改 1、20 改 0 |
| G9 | `NpcTable.csv` | — | 不缺 |
| G10 | 首領外觀 | 沒有 | §9.4 兩階段（先借圖、再 AI 自繪） |
| G11 | `BloodFang_Diningroom.dipanmap` | 南門 `targetEntrance=BloodFang_Garde_north`（打錯字，T0 發現） | **Q10 拍板**：改成 `BloodFang_Garden_north`；**主遊戲 StreamingAssets 那份要 Sync 才會更新** |

## 7. 刻意不做（記 TODO，別順手做）

- ~~跟隨型 NPC（方案 B）~~ → 2026-10-15 改用 §10「劇本同伴」（PlayerAlly＋IsCompanion），引路仍用演出。
- 血狂之爭**劇本道具**（ItemTable 新列「劇本-血狂之爭」`TargetMapId=17`）與邪佛發牌（`selectScript`）——正式進關途徑；測試用 `DevQuickStart` 已有入口。
- 過關獎勵內容（臨時包已會結算，不用動）。
- 首領**產圖**素材（AI 先自繪一版，§9.4）、正式平衡數值。
- 遊戲端「非第 0 段也能自動播」（§8.2 已知限制）。
- 和平段兩族 NPC 的「來回走動」路徑（第一輪全 `idle`）。
- 左右路的環境敘事（地上物、照明）——作者的美術範圍。

---

## 8. 新功能規格：編輯器「多段演出」（2026-10-15 作者選定）

**為什麼要做**：作者補充「引路的人依玩家血統換」⇒ 同一張圖要有三段演出（血族／狂族／管家）。遊戲端**早已支援**（T0 查證 #2：`MapData.FindCutscene(id)` 依 `id` 找段、不分大小寫），但編輯器的「劇情」分頁只看得到、只能編第 0 段，而且「刪除演出」會把整份清單清空。不補的話第 1、2 段只有 AI 改得動，作者無法在編輯器微調走位。

### 8.1 行為

- 劇情分頁頂端加一列：**段落下拉**（顯示 `#序號 id`，id 空顯示「（第一段，無 id）」）＋「＋新增段落」＋「複製此段」＋「刪除此段」。
- 選了哪一段，下面整個面板（演員、步驟、畫布上的演員把手與路徑、▶ 預覽）都操作那一段。
- **「刪除此段」只刪目前這段**（現況 `RemoveCutscene` 是 `cutscenes.Clear()`）。刪的是第 0 段時跳確認：第 0 段是 `autoStartOnEnter` 唯一會檢查的段、也是 `cutsceneId` 留空時播的段。
- **id 欄**移到段落列旁邊，並檢查同圖重複（重複時紅字警告，存檔照存）。
- 「複製此段」＝深拷貝（演員＋步驟），id 加 `_copy`——三段引路長得幾乎一樣，複製後只改演員外觀與台詞最快。
- 存檔格式**不變**（本來就是清單）。

### 8.2 程式掛點（全部在 `DipanProj_MapEditor`，主遊戲零改動）

| 檔 | 改什麼 |
|---|---|
| `Tools/CutsceneController.cs` | 加 `SelectedIndex`（換圖／Undo 還原時夾回範圍）＋ `Current` 屬性；7 處 `MainCutscene` 改 `Current`；`EnsureCutscene` 只在清單空時新增；`RemoveCutscene` 改成刪 `Current`；新增 `AddCutscene()`／`DuplicateCurrent()` |
| `UI/EditorUI.cs` `DrawCutscenePanel`（約 2722 行） | 段落列 UI；原本的 `map.MainCutscene` 改 `Current` |
| `Core/CutsceneOverlay.cs` | 畫布疊層改畫 `Current` |
| `Preview/CutscenePreview.cs` | 不用改（本來就吃傳進來的 `Cutscene`） |
| `Data/MapData.cs` | `MainCutscene` 保留（遊戲端語意＝第 0 段），不再被編輯器 UI 使用 |

已知限制（記 TODO，不在本批）：遊戲端 `MaybeAutoStart` 只看第 0 段的 `autoStartOnEnter`。本劇本所有段都是 `playCutscene` 啟動，不受影響。

### 8.3 驗收

- [ ] 開血狂之爭 17 大門：段落下拉看得到三段引路，逐段切換時畫布上的演員跟著換。
- [ ] 第 1 段改一個走位點 → 存檔 → 重開 → 改動還在，其他兩段沒被動到。
- [ ] 「刪除此段」只少一段；Undo 能還原。
- [ ] 舊地圖（只有一段或沒有演出）行為與以前完全一樣。

---

## 9. 新功能規格：兩隻頭目（2026-10-15 作者：「攻擊方式完全交給 AI 規劃」）

### 9.0 設計原則

- **兩隻打法要明顯不同**：狂族＝**貼身壓迫**（逼玩家走位閃大招），血族＝**遠程控場**（逼玩家追著打、處理地上的危險區）。玩家選的陣營不同，迎戰的就是不同的考題。
- **盡量重用已寫好的零件**，新程式只寫「編排」：揮擊命中幀與一次性傷害圈 `ImpactDamageArea`（§10.4／§9.4）、跳躍騰空 `IAirborneVisual`＋程序化裂地 `GroundCrack`（§9.5／§9.6）、怪物投射 `WeaponCastService.FireNormal`（§8.4）、召喚武器 `Mode=Summon`（§3）、死亡回收 `RecallSummons`（§6.7）、血量門檻喊話（MonsterData 句子欄）。
- **兩段式**：血量 ≤ 40% 進第二階段（一生一次，伴隨喊話與外觀變化），招式加強而不是換一套——玩家前半學到的東西後半仍然有用。
- **範圍技一律 `MonsterController.ScaledRadius()`**（PROBLEMS F29）；傷害一律走 `CombatSystem`；招式實體一律登記、`Die()` 時回收（BOSS_MODULE §6.7）。

### 9.1 狂族首領「嘯月」— MonsterData 38、`BrainType=WerewolfLord`

> ⚠ **2026-10-27 作者決定：狂族首領改由「17 聖子・艾爾文」擔任**（38／40／43、和談演員、對話講者全部換過，見 BOSS_MODULE §16）。下面這套 `WerewolfLord` 設計**不採用、不作現行依據**。

| 招式 | 觸發 | 行為 | 數值（初版） |
|---|---|---|---|
| **爪擊** | 貼身、冷卻 1.2s | 照 `MeleeChaseBrain` 的寫法：站定揮完（`BrainControlsAttackPose`），命中幀開 `ImpactDamageArea` | 半徑 1.1、傷害 12 |
| **躍擊裂地** | 距離 3.5–8、冷卻 7s | 照 `LeapSlamBrain` 的跳躍段（蓄力→騰空→落地），**但可以重複用**（LeapSlam 一生只跳一次）；落地裂地＋傷害圈 | 半徑 1.8、傷害 20 |
| **狼嚎** | HP ≤ 70% 起、冷卻 20s | 站定 1.2s 播 attack＋鏡頭輕震，召喚戰狼（怪 17）2 隻 | 新召喚配方：`SummonIds=17`、`SummonCount=2`、`SummonMaxAlive=3` |
| **狂暴**（二階段） | HP ≤ 40%，一次 | 喊話（句子欄 `40%: …`）、身體紅色脈動、移速 ×1.35、躍擊冷卻 7→4s；**落地後再兩圈外擴衝擊波**（延遲 0.3／0.6s，半徑 2.8／3.8，各 10 傷害，**圈與圈之間有縫**——站在縫裡就不會被打到，這是給玩家的解題點） | — |

打法：玩家拉開距離就會被躍擊，貼近要吃爪擊；二階段三圈衝擊波逼玩家學會「看裂痕、站縫」。

### 9.2 血族首領「緋夜」— MonsterData 39、`BrainType=VampireLord`

> ⚠ **2026-10-26 作者決定：血族首領改由「夏綠蒂公爵」擔任**（39／41／42、和談演員、對話全部換過，見 BOSS_MODULE §14.8）。下面這套 `VampireLord` 設計**暫停、不作現行依據**；要不要把其中的招式（血池、化蝠閃現、喚蝠）加到公爵身上待作者決定。

| 招式 | 觸發 | 行為 | 數值（初版） |
|---|---|---|---|
| **保持距離** | 常駐 | 維持與玩家 5–7 的距離（紅嫁衣的「掃描可逃方向」寫法＋射手型 `CanShootFrom` 視線判斷） | 速度用 CSV |
| **血咒扇射** | 冷卻 3s、有視線 | 舉杖到放彈幀，**扇形 5 發**（±30°）。Brain 自己算 5 個方向、呼叫 5 次 `FireNormal` | 新武器（複製 49 血火咒、傷害 8、不反彈） |
| **血池** | 冷卻 9s | 玩家腳下先出紅色預警圈 1.0s → 湧出血池 4s，每 0.5s 一次小傷害圈；**首領站在血池裡每秒回 2% 血** | 半徑 1.5、每跳 5 傷害 |
| **化蝠閃現** | 玩家貼近（< 2.5）或冷卻 12s | 原地爆開一群蝙蝠（純視覺）→ 0.6s 隱身無敵 → 在玩家**另一側** 5 單位的可走點現身 → 立刻扇射一次 | — |
| **喚蝠** | HP ≤ 60% 起、冷卻 18s | 召喚血蝠（怪 35）2 隻 | 新召喚配方：`SummonIds=35`、`SummonMaxAlive=3`。⚠ 血蝠未實測，壞了換 15 |
| **血宴**（二階段） | HP ≤ 40%，一次 | 喊話、扇射 5→7 發且一次兩輪、血池一次兩個（玩家腳下＋前進方向預判）、閃現冷卻 12→7s | — |

打法：她會一直拉開距離、在你腳下放血池，追她就要穿過血池；別讓她站在血池裡回血。

### 9.3 隱藏線雙首領

- 新增 MonsterData **40／41**（嘯月／緋夜雙首領版）：同 Brain、HP 改成 38／39 的 **0.6 倍**，兩隻一起上才不會太硬。
- Brain 讀「場上是否還有另一位首領」：**有的話召喚停用**（不然場面會失控）。另一位死掉之後恢復。
- 兩族首領彼此也敵對（FactionRelations），會互相打——但只是演戲傷害 ×1/100。

### 9.4 外觀：兩階段

| 階段 | 做法 | 目的 |
|---|---|---|
| **A 借圖**（Brain 開發與測試用） | 38／40 `Name=Wolf Royal Guard`（idle/walk/attack/jump 都有）、39／41 `Name=Blood Warlock`（idle/walk/attack）；`Scale` 放大 1.4 讓牠看起來像首領 | Brain 不等素材就能寫完、能測 |
| **B AI 自繪序列圖**（給作者看效果） | Python＋PIL 程序化畫一套剪影風序列圖（`WerewolfLord/`、`VampireLord/` 資料夾，idle/walk/attack(/jump)，每動作 8–12 幀、512 畫布、朝右），放進 `Modules/BloodFang/Monsters/SequenceImage/`；`Name` 欄換過去即可切換 | 作者覺得可以就保留、不行就產圖替代；**不影響 Brain**（Brain 只認動作名，不認圖） |

⚠ 自繪序列圖的品質上限是「看得出是什麼、動作讀得懂」，不會接近產圖水準——這一點先講清楚。

### 9.5 程式掛點

| 檔 | 內容 |
|---|---|
| `AI/Behaviors/WerewolfLordBrain.cs`（新） | 狀態機：Chase／Claw／LeapWindup／Airborne／Land／Howl；二階段旗標 |
| `AI/Behaviors/VampireLordBrain.cs`（新） | 狀態機：Kite／Cast／PoolCast／BatOut／BatIn／Summon；二階段旗標 |
| `Combat/BloodPool.cs`（新） | 血池實體：預警圈→存在期每 0.5s 開 `ImpactDamageArea`→淡出；全場登記表＋`CancelAll()`；`IsInsideAnyPool(pos)` 給首領回血 |
| `AI/MonsterController.cs` | `Initialize` 的 BrainType switch 加兩個 case；`Die()` 呼叫 `BloodPool.CancelAll()`（§6.7 原則） |
| `RecipeTable`／`WeaponTable` | 新增三把怪物專用武器（狼嚎召喚、喚蝠召喚、扇射彈），**ItemTable 不加**（玩家拿不到） |
| `MonsterData.csv` | 新增 38／39／40／41 |
| `readme/BOSS_MODULE.md` | 新增 §16 嘯月、§17 緋夜（§13 菁英血衛士、§14 夏綠蒂公爵、§15 劇本同伴已使用；2026-10-26 改號）（現狀文件） |

沒有 C# 編譯器：寫完跑 PROBLEMS I10 的「用到但沒宣告」檢查；跳躍段與揮擊段**照抄** LeapSlam／MeleeChase 已寫好的流程，不重新發明（那兩支的坑都記在 BOSS_MODULE §9.3、§10.3）。

---

## 10. 新功能（2026-10-15 作者重訂流程後追加）：表演戰、劇本同伴

作者實測 T4 後重訂：選邊之後「另一族偷襲、兩族在和談廳打起來，但這是表演——傷害很小、玩家打不到任何一方」，以及「首領從頭到尾跟著玩家、會出手但沒殺傷力、敵怪不以他為目標」。

| 功能 | 做法 | 程式 |
|---|---|---|
| ~~**表演戰** `factionShow`~~（2026-10-15 同日撤掉，改 `factionWar` 的 `fullDamage`「實戰」：`FactionRelations.FullTribeDamage`） | 原做法：FactionRelations 加 `ShowFightActive`：兩族互咬照舊（本來就 ×1/100），`AttacksPlayer(部族)` 回 false ⇒ 不打玩家、`ApplyLayer` 放 Ally 層 ⇒ 玩家打不到。`factionPeace`／`factionWar`／換關卡都會結束它 | `AI/FactionRelations.cs`、`TriggerChain` case |
| **劇本同伴** `companion` | （⚠ 2026-10-26 起行為改為 `CompanionBrain`、有微弱傷害，見 BOSS_MODULE §15；下列為原始規劃）**重用玩家召喚物 PlayerAlly**：AllyBrain 跟玩家＋衝敵人、MapManager 換圖本來就保留 PlayerAlly 並移到新落點。只加 `MonsterController.IsCompanion`：`TakeDamage` 直接略過、`FindNearestEnemy` 與 `EnemyContactDamage` 跳過它。無殺傷力＝同伴列 `ContactDamage 0`。換關卡由 `TriggerChain.ClearLevelFlags` 呼叫 `CompanionService.DismissAll()`（不然會以 PlayerAlly 身分跟到廣場） | 新檔 `AI/CompanionService.cs`、`MonsterController`、`EnemyContactDamage`、`TriggerChain`、`PlayModeStaticReset` |

編輯器：`TriggerType.cs` 與 `triggerTypes.json` 兩處都加了 `factionShow`（已撤，改 factionWar 的 fullDamage 勾選）與 `companion`（monsterId、召喚勾選、X/Y）。
