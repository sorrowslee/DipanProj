# 過關 / 死亡 / 返回廣場 → 卍字離場 → 結算流程

> 玩家「過關、死亡、或從設定面板主動返回」時：播一段**旋轉卍字把玩家包裹吞入、飛上天離場**的世界特效 → 開**結算畫面**（關卡 loading 圖當底、過關/死亡各自標題、獎勵框）→ 按「返回廣場」回邪佛廣場中央。全程式、零 prefab，風格對齊 GameFlowManager / UIManager。

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
| `Scripts/Flow/LevelExitManjiController.cs` | 世界座標卍字離場特效（沿用開場墜落的卍字圖 `Resources/InitialStory/Manji`，載不到程序生成）。淡入→縮小吞玩家（金→紫）→飛上天淡出。**`SortingOrder = 25000`**（見下方雷點） |
| `Scripts/UI/Panels/ResultPanel.cs` | 結算畫面。底＝`Resources/Loading/<module>.png`；美術元件在 `Resources/UI/ClearStagePanel/`（標題/死亡標題/獎勵框/獲得獎勵標頭/返回按鈕）。`Show(win, showTitle, module, displayName)`。獎勵格容器 `RewardsArea` **目前留空**、無道具時顯示大「無」字 |
| `Scripts/UI/Panels/ExitCountdownPanel.cs` | 過關延時期間上方的「X 秒後即將進入結算」倒數提示（HUD 層、不擋不暫停） |
| `Scripts/UI/Panels/SettingsPanel.cs` | 「返回廣場」鈕：**只在關卡內顯示**（廣場/標題只有離開遊戲鈕、且離開鈕移回置中）；按下有二次確認 `ConfirmPopup` |
| `Scripts/Map/TriggerChain.cs` | `watchFlag`（觀察旗標變動）＋ `clearLevel`（過關鏈動作）兩個 trigger；`fireOnFlag` 自動觸發機制（`AutoFireOnFlag`） |
| `Scripts/PlayerController.cs` | `Die()` 接 `EndLevel(Death)`；`ReviveFull()` |

## 兩顆 trigger：watchFlag（偵測）＋ clearLevel（執行）
**旗標偵測與過關執行拆成兩顆**（2026-07-14 重構），中間可串任意表演（對話→動畫→給獎勵…），不用全擠進過關延時窗。

**① 觀察旗標變動（`watchFlag`）**——監聽旗標、驅動鏈起點：
- 參數 **`fireOnFlag`**：要監聽的旗標名。該旗標**首次成立(false→true)**時，觸發自己的「接續觸發(next)」。本身不做事（同 `onEnter`，改由旗標驅動）。0 格，用「＋手動新增空區域」建立。
- ⚠️ 是「旗標翻成 true 的那一刻」才觸發，**不是**「進場時旗標已 true 就觸發」——後者請用 `onEnter + 條件旗標(requireFlag)`。

**② 過關(結算)（`clearLevel`）**——純鏈動作：
- 參數 **`delaySeconds`（延時觸發）**：被觸發後倒數幾秒才進結算（**留空＝2 秒**）；這段玩家可自由操作（撿戰利品）、上方顯示倒數。
- 由別的 trigger 的「接續觸發」接進來；也可「踩點過關」（畫格子讓玩家踩到）。仍保留 `next`（可在開始倒數那刻再觸發別的）。

**典型接法（紅嫁衣）**：
```
boss 出生點 死亡觸發旗標 = redBridalCleared
watchFlag(fireOnFlag=redBridalCleared) → next → 被打敗對話
被打敗對話 → next →（可選）動畫/給獎勵…
最後一個 → next → 過關(結算)  → 倒數(delaySeconds) → 結算
```

`fireOnFlag` 的底層：`TriggerChain.SetFlag` 在旗標**首次成立**時掃描本圖 `fireOnFlag` 對上的 trigger（現在＝watchFlag）、延一幀 `Activate`。

## 雷點 / 已知限制（PROBLEMS 類）
- **卍字被特效蓋住 → 排序**：世界特效（`VfxManager` 預設 `SortingOrder=22000`，如榕樹妖死亡火焰）比角色排序（Y 排序帶 ~1~1.7 萬）高。卍字若只設「玩家+5」會被蓋住。**卍字固定用 `SortingOrder=25000`**（>22000、<32767 16-bit 安全；UI 覆蓋層永遠更上層不受影響）。（2026-07-14 榕樹妖火焰蓋住卍字修正）
- **接續觸發(`next`)不能接成環**：例如過關 `next` 接對話、對話 `next` 又接回過關 → 無限互觸發、對話狂彈。**整條鏈的 `next` 不要形成環**。旗標也一樣：別讓某步的「完成寫旗標」設成 watchFlag 正在監聽的同名旗標（會再觸發一次那條鏈）。
  - 防線：`SetFlag` 只在旗標「首次成立」時才跑 `fireOnFlag`（重複設同旗標不再重觸發）；`ExecuteClearLevel` 若 `GameFlowManager.IsEndingLevel` 為真就跳過。
- **編輯器加了新 trigger 類型/參數卻沒出現**：編輯器啟動時的合併只在 `triggerTypes.json` 缺該類型/參數時補進去。改 `TriggerType.cs` 的 `Defaults()` 後**重開編輯器**才會合併（現已支援「對既有類型補新參數」，見 `TriggerTypeStore.cs`）。真的沒出現就檢查 `DipanProj_MapEditor/Assets/StreamingAssets/triggerTypes.json`。
- **延時期間玩家被殘留怪物打死**：流程已在進行，死亡流程會被 `_endingLevel` 擋掉 → 仍走過關結算。boss 房打完通常無其他怪，一般不會遇到。
- **打包鐵則沿用既有**：Intro/MainScene 都要在 Build、MainScene 排第 0（見 INTRO_COMIC / CUTSCENE_TUNNEL）。

## 尚未做（待補）
- 獎勵內容（道具/經驗/金錢）——`ResultPanel.RewardsArea` 已預留，之後把「圖示＋名稱＋×數量」塞進去、大「無」字會自動隱藏（判斷 `childCount`）。
- 死亡專屬結算的獎勵規則（目前死亡結算獎勵區同樣留空）。
- 卍字吞人時玩家腳下影子是獨立物件、不會跟著縮（約 3 秒特效期間的小瑕疵）。
