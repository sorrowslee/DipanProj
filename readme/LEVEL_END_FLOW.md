# 過關 / 死亡 / 返回廣場 → 卍字離場 → 結算流程

> 玩家「過關、死亡、或從設定面板主動返回」時：播一段**旋轉卍字把玩家包裹吞入、飛上天離場**的世界特效 → 開**結算畫面**（關卡 loading 圖當底、過關/死亡各自標題、獎勵框）→ 按「返回廣場」回邪佛廣場中央。全程式、零 prefab，風格對齊 GameFlowManager / UIManager。

> 🔁 **另一頭**：進場也有一個卍字（本特效的倒放：從天而降、落地把主角吐出來），
> 家在 [SCENE_TIP.md](SCENE_TIP.md) **§0 卍字進場**——因為它的觸發判定與「場景說明」共用同一份狀態。
> 兩支共用同一張卍字圖與同一個 `SortingOrder`，**動外觀/排序要兩邊一起看**。

## 什麼時候讀
- 改過關/死亡的表演節奏、離場特效、結算畫面版面。
- 加新關卡（要在對照表補顯示名）、或想懂 clearLevel 觸發怎麼接。
- 卍字被其他特效蓋住、或結算畫面排版要調時。

## 三種結束方式（`GameFlowManager.LevelEndKind`）
| 種類 | 由誰觸發 | 標題 | 記過關 | 延時 |
|---|---|---|---|---|
| `Clear`（過關） | clearLevel 觸發動作 | 「通關結算」圖 | 是（`MarkModuleCleared`＋存檔） | clearLevel 的「延時觸發」欄位（空＝2 秒） |
| `Death`（死亡） | `PlayerController.Die()` | 「死亡結算」圖（缺圖退回文字「殞命」） | 否 | 固定 `DeathDelaySeconds`（2 秒） |
| `Return`（返回） | 設定面板「返回廣場」鈕 | **不顯示任何標題** | 否 | 短（0.25 秒） |

入口一律 `GameFlowManager.Instance.EndLevel(kind, delaySeconds)`；`_endingLevel` 防重入（同幀多次呼叫只跑一次）。**用「目前是否在一張已載入的關卡地圖」（`MapManager.CurrentMapId > 0`）判斷能不能觸發，不看 `InGame`**——所以 DevQuickStart 直接進關卡（沒跑標題流程、`InGame=false`）也能正常結束關卡。

## 流程分兩階段（`EndLevelRoutine`）
1. **等待階段**（讓表演演完）：
   - **Clear**：`ClearCountdown()`——**不擋操作、不暫停**，玩家可自由移動（撿戰利品等）；上方顯示「X 秒後即將進入結算」（`ExitCountdownPanel`）。倒數用 **scaled time**，所以 boss 對話（會自己暫停遊戲）期間倒數自動凍住，對話結束、玩家能動時才繼續倒數。
   - **Death / Return**：擋操作但不暫停（死亡動畫仍會演），等固定秒數。
2. **離場＋結算**：暫停遊戲（怪物/子彈定住）→ 播卍字特效（`LevelExitManjiController`，unscaled）→ 過關才記進度 → 開 `ResultPanel`。

按「返回廣場」→ `ReturnToHubFromResult()`：淡黑 → 關結算 → **復活/還原玩家**（`PlayerController.ReviveFull()`：血魔補滿、解除死亡定格、還原縮放）→ `GoToMap(HubMapId=12, "center")` → 淡出。

## 相關檔案
| 檔案 | 角色 |
|---|---|
| `Scripts/Flow/GameFlowManager.cs` | 流程指揮：`EndLevel` / `EndLevelRoutine` / `ClearCountdown` / `ReturnToHubFromResult`；關卡 module→顯示名對照表 `ModuleDisplayNames`（**加新關卡在這加一行**）；`IsEndingLevel` |
| `Scripts/Flow/LevelEnterManjiController.cs` | 世界座標卍字**進場**特效（本檔離場的倒放；說明在 [SCENE_TIP.md](SCENE_TIP.md) §0。共用離場這支的 `ManjiSprite`） |
| `Scripts/Flow/LevelExitManjiController.cs` | 世界座標卍字離場特效（沿用開場墜落的卍字圖 `Resources/InitialStory/Manji`，載不到程序生成）。淡入→縮小吞玩家（金→紫）→飛上天淡出。**`SortingOrder = 25000`**（見下方雷點） |
| `Scripts/UI/Panels/ResultPanel.cs` | 結算畫面。底＝`Resources/Loading/<module>.png`；美術元件在 `Resources/UI/ClearStagePanel/`（標題/死亡標題/獎勵框/獲得獎勵標頭/返回按鈕）。`Show(win, showTitle, module, displayName, rewards)`。獎勵格 `RewardsArea`：`PopulateRewards` 排圖示＋×數量的能力還在，但 **2026-11-06 拿掉臨時包後呼叫端一律傳 `rewards=null` → 顯示大「無」字**（作者拍板先不顯示，等之後有正式過關獎勵再接，見 TODO） |
| `Scripts/UI/Panels/ExitCountdownPanel.cs` | 過關延時期間上方的「X 秒後即將進入結算」倒數提示（HUD 層、不擋不暫停） |
| `Scripts/UI/Panels/SettingsPanel.cs` | 「返回廣場」鈕：**只在關卡內顯示**（廣場/標題只有離開遊戲鈕、且離開鈕移回置中）；按下有二次確認 `ConfirmPopup`。**劇本中（2026-11-06）兩顆鈕都改跳「劇本已消耗，離開後就無法再進入」警告**；視窗 X／Alt+F4 也會被 `Flow/QuitGuard.cs` 攔下來跳同一個警告（見 [RUN_PROGRESS.md](RUN_PROGRESS.md) §10） |
| `Scripts/Map/TriggerChain.cs` | `watchFlag`（觀察旗標變動）＋ `clearLevel`（過關鏈動作）兩個 trigger；`fireOnFlag` 自動觸發機制（`AutoFireOnFlag`） |
| `Scripts/PlayerController.cs` | `Die()` 接 `EndLevel(Death)`；`ReviveFull()` |

## 兩顆 trigger：watchFlag（偵測）＋ clearLevel（執行）
**旗標偵測與過關執行拆成兩顆**（2026-07-14 重構），中間可串任意表演（對話→動畫→給獎勵…），不用全擠進過關延時窗。

**① 觀察旗標變動（`watchFlag`）**——監聽旗標、驅動鏈起點：
- 參數 **`fireOnFlag`**：要監聽的旗標名。該旗標**首次成立(false→true)**時，觸發自己的「接續觸發(next)」。本身不做事（同 `onEnter`，改由旗標驅動）。0 格，用「＋手動新增空區域」建立。
- ⚠️ 是「旗標翻成 true 的那一刻」才觸發，**不是**「進場時旗標已 true 就觸發」——後者請用 `onEnter + 條件旗標(requireFlag)`。

**② 過關(結算)（`clearLevel`）**——純鏈動作：
- 參數 **`delaySeconds`（延時觸發）**：被觸發後倒數幾秒才進結算（**留空＝2 秒**）；這段玩家可自由操作（撿戰利品）、上方顯示倒數。
- 由別的 trigger 的「接續觸發」接進來。⚠ **塗格子不會踩點觸發**（它是純鏈動作）；要「走到某處就過關」請用 `camZone`（zoom 留空）踩點、`next` 接它，見 PROBLEMS **B18**。仍保留 `next`（可在開始倒數那刻再觸發別的）。

**典型接法（紅嫁衣）**：
```
boss 出生點 死亡觸發旗標 = redBridalCleared
watchFlag(fireOnFlag=redBridalCleared) → next → 被打敗對話
被打敗對話 → next →（可選）動畫/給獎勵…
最後一個 → next → 過關(結算)  → 倒數(delaySeconds) → 結算
```

`fireOnFlag` 的底層：`TriggerChain.SetFlag` 在旗標**首次成立**時掃描本圖 `fireOnFlag` 對上的 trigger（現在＝watchFlag）、延一幀 `Activate`。

## 劇本證明三行（2026-11-16，⏳ 未實測）

規則見 [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) §8.7；資料來源 `ScriptRecords`（[SAVE_SYSTEM.md](SAVE_SYSTEM.md) §15）。

- **位置**：獎勵框下方，三行由上到下 B／A／S（`ResultPanel` 常數 `ProofRowY0=-290`、`ProofRowGap=46`、字級 32）；有證明時返回鈕從 y −430 移到 −472 讓位。
- **顯示哪個劇本**：結算的 module 在 `ProofTable.csv` 有登記才顯示；沒登記（例：邪佛廣場、教學）整區隱藏、返回鈕留在原位。
- **三種狀態**：沒達成＝灰「B級證明：?????」；以前就領過＝一開始就是金色條件字＋小字「（已領取）」；**這一趟新拿到的**（`ScriptRecords.GrantedThisRun`，`RunProgress.BeginRun` 時清空）＝先灰，面板開 0.8 秒後依序播：搖擺 0.55 秒 → 整行字一顆顆飛散＋碎屑（0.65 秒）→ 金色條件字從 1.35 倍縮回＋背後閃一道金光。全部用真實時間（結算畫面暫停遊戲）。
- 過關、死亡、中途返回一律顯示。證明在打倒頭目當下就入帳＋存檔（`grantProof`），所以打完頭目才死，那一行一樣會播。
- ⚠ **沒達成的一律只顯示 `?????`，絕不顯示條件**（作者：不提示怎麼達成）。條件文字在 `ProofTable.csv`＋語言表 11011 起。
- **新證明的完整演出（2026-11-17 作者規格，⏳ 未實測）**：這一趟**沒有**新證明＝照舊（返回鈕直接在、證明不會出現在獎勵列表）。**有**新證明時：
  1. 返回廣場鈕先藏起來。
  2. 獎勵列表**只排非證明的東西**（功德值、道具…）；證明不論哪一階一律**排在最後**，先不出現。格子大小一開始就照「含證明的總數」算，之後只會讓位、不會突然縮小。
  3. 條件字依序搖擺 → 破碎 → 變金（上面那套）。
  4. 全部變金後隔 0.35 秒，證明照 **B → A → S** 一張張接到列表最後：已在列表上的格子先往旁邊讓位（0.25 秒）→ 新格子的位置上閃光（跟圖示一樣大：程式產生的金色光暈＋四芒星＋幾顆噴出的金點，0.22 秒亮到頂）→ 最亮那刻證明圖示＋「×1」淡入、從 1.25 倍縮回 → 閃光收掉（0.38 秒）。
  5. 返回廣場鈕淡入（0.3 秒）。
  - 演出中按 **ESC 或滑鼠左鍵＝跳過**：直接擺成結束狀態（條件字全金、證明全部接上、返回鈕出現）；跳過那一下不會回廣場。演出結束後 ESC 才是返回廣場。
  - 實作：`ResultPanel.SequenceRoutine`／`FlashAndReveal`／`SkipSequence`。⚠ 演出協程都經 `Run()` 記錄、只停自己的——**不能 `StopAllCoroutines()`**，`UIPanel` 的開關淡入淡出也是同一個物件上的協程，停掉面板會卡在半透明。

## 雷點 / 已知限制（PROBLEMS 類）
- **卍字被特效蓋住 → 排序**：世界特效（`VfxManager` 預設 `SortingOrder=22000`，如榕樹妖死亡火焰）比角色排序（Y 排序帶 ~1~1.7 萬）高。卍字若只設「玩家+5」會被蓋住。**卍字固定用 `SortingOrder=25000`**（>22000、<32767 16-bit 安全；UI 覆蓋層永遠更上層不受影響）。（2026-07-14 榕樹妖火焰蓋住卍字修正）
- **接續觸發(`next`)不能接成環**：例如過關 `next` 接對話、對話 `next` 又接回過關 → 無限互觸發、對話狂彈。**整條鏈的 `next` 不要形成環**。旗標也一樣：別讓某步的「完成寫旗標」設成 watchFlag 正在監聽的同名旗標（會再觸發一次那條鏈）。
  - 防線：`SetFlag` 只在旗標「首次成立」時才跑 `fireOnFlag`（重複設同旗標不再重觸發）；`ExecuteClearLevel` 若 `GameFlowManager.IsEndingLevel` 為真就跳過。
- **編輯器加了新 trigger 類型/參數卻沒出現**：編輯器啟動時的合併只在 `triggerTypes.json` 缺該類型/參數時補進去。改 `TriggerType.cs` 的 `Defaults()` 後**重開編輯器**才會合併（現已支援「對既有類型補新參數」，見 `TriggerTypeStore.cs`）。真的沒出現就檢查 `DipanProj_MapEditor/Assets/StreamingAssets/triggerTypes.json`。
- **延時期間玩家被殘留怪物打死**：流程已在進行，死亡流程會被 `_endingLevel` 擋掉 → 仍走過關結算。boss 房打完通常無其他怪，一般不會遇到。
- **打包鐵則沿用既有**：Intro/MainScene 都要在 Build、MainScene 排第 0（見 INTRO_COMIC / CUTSCENE_TUNNEL）。

## 尚未做（待補）
- ~~**過關獎勵**~~ → **2026-11-16 已接**：獎勵區列出這一趟的**功德值總額 → 新拿到的證明 → 所有取得的道具**（`GameFlowManager.BuildRunRewards`，資料來源 `RunProgress.RunMerit／RunItems`＋`ScriptRecords.GrantedThisRun`，見 [RUN_PROGRESS.md](RUN_PROGRESS.md)〈這一趟的收穫〉）。過關、死亡、中途返回都顯示；功德值含結束時補進來的「地上沒撿的」。超過兩排會自動縮小格子。**滑鼠停在圖示上會跳物品說明**（共用 `ItemTooltip`，掛 System 層才不會被結算畫面壓住；每幀用游標座標判斷，暫停中照樣運作）。以下是舊紀錄：
- **過關獎勵（2026-11-06 改）**：臨時包已拿掉（撿到即入袋、死亡也保留），`GameFlowManager` 改成過關/死亡/返回一律 `RunProgress.EndRun()`（只清進度、不動背包），結算獎勵區**先不顯示**（傳 null → 「無」）。作者之後會加正式的過關獎勵，屆時再把內容傳給 `ResultPanel.Show(..., rewards)`。見 [TODO.md](TODO.md)。
  - *歷史：2026-07-18～2026-11-05 獎勵區顯示的是臨時包 `SettleIntoBag()` 的快照，死亡/返回 `EndRunDiscard()` 零收穫。*
- 卍字吞人時玩家腳下影子是獨立物件、不會跟著縮（約 3 秒特效期間的小瑕疵）。
*2026-07-27 更正：結算獎勵區原記「目前留空」，實際已於 2026-07-18 接上臨時包結算。*
*2026-11-06：臨時包拿掉，結算獎勵區改為先不顯示；劇本中返回廣場／離開遊戲加警告。*
