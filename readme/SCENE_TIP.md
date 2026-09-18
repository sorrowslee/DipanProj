# 場景說明 (Scene Tip)

> 返回 [文件總覽](README.md)

進到一張「有名字」的地圖時：**先**由一個卍字從天而降把主角送進場（第 0 節，2026-09-17 加），
卍字淡出後畫面上方淡入一張金色毛筆場景名、底下墊一條血紅分隔線，停留一下自動淡出。
**整段（卍字＋名字）都暫停遊戲、鎖住操作**，播完才把場面交還給遊戲——
所以名字一定看得完整，這張圖的**進場自動劇情**、進場對話／教學也一定接在它後面
（2026-08-27 起：一進圖是正常畫面 → 跳名字 → 名字淡出後劇情模式才整套出現；之前是劇情演完才跳）。

| 東西 | 在哪 |
|---|---|
| 資料 | `Assets/Data/MapsTable.csv` 的 **`SceneTip` 欄**（第 12 欄） |
| 面板 | `Assets/Scripts/UI/Panels/SceneTipPanel.cs` |
| 卍字進場 | `Assets/Scripts/Flow/LevelEnterManjiController.cs`（第 0 節） |
| 觸發 | `MapManager.FireEnterTriggersRoutine`（與進場觸發同一條等待鏈） |
| 文字圖 | `Assets/Resources/UI/Texts/<語言>/SceneTipPanel_Text_<key>.png` |
| 分隔線 | `Assets/Resources/UI/SceneTipPanel/SceneTipPanel_Bg.png`（全場景共用一張） |

---

## 0. 卍字進場（進場表演的第一段）

> **狀態：✅ 程式完成（2026-09-17），⏳ 未編譯、未實機驗證。**
> 程式＝`Assets/Scripts/Flow/LevelEnterManjiController.cs`，接線在 `MapManager.FireEnterTriggersRoutine` 開頭。

作者要的節奏：**進場 → 卍字從天而降（此時遊戲已暫停，只有卍字在動）→ 落地、主角出現 → 跳場景名 → 名字收掉才正式開打。**

它是過關/死亡那支「卍字離場」（[LEVEL_END_FLOW.md](LEVEL_END_FLOW.md)）的**倒放**，兩支首尾呼應：

| | 離場（過關／死亡） | 進場（本節） |
|---|---|---|
| 方向 | 淡入 → 縮小吞人 → 飛上天淡出 | 從天而降 → 落地放大吐人 → 原地淡出 |
| 顏色 | 金（神聖）→ 紫（墮落） | 紫 → 金 |
| 長度 | 0.45＋1.25＋0.95 ≈ **2.65 秒** | 0.85＋0.95＋0.40 ≈ **2.20 秒** |
| 程式 | `LevelExitManjiController` | `LevelEnterManjiController` |

卍字圖是**同一張**（`Resources/InitialStory/Manji`，載不到就用離場那支的程序生成備援——所以兩邊長得一樣，
改一邊的圖另一邊跟著變）；`SortingOrder` 同樣寫死 **25000**（壓在世界特效 22000 與角色之上，理由見 LEVEL_END_FLOW 的雷點）。

### 0.1 什麼時候播 —— **與場景說明共用同一份判定**

> **「這一趟第一次進到一張 `SceneTip` 有填的地圖」就播。**

沒有新的 CSV 欄位、也沒有第二份去重紀錄：判定直接讀第 2 節那個 `_shownSceneTips`
（**只 `Contains` 不 `Add`**，名額留給場景說明自己去 Add；文字圖還沒畫、名額被還回去時，
那一趟再進來連卍字一起重播，兩者永遠同步）。

| 情況 | 卍字 | 場景名 |
|---|---|---|
| 標題／讀檔 → 邪佛廣場 | ✅ | ✅ |
| 廣場 → 紅嫁衣（跨 module） | ✅ | ✅ |
| 紅嫁衣房間互跳（柴房↔儲藏室…） | ❌ | ❌ |
| 過關／死亡 → 回廣場 | ✅ | ✅ |
| 開場：初始森林 1 → 森林 2 → 初始洞窟 | ❌ | ❌ |
| **開場第一次走到邪佛廣場** | ❌ | ❌ |（見 §0.1b）
| 之後每一次回廣場（過關／死亡／讀檔） | ✅ | ✅ |

前三張開場地圖不播，靠的正是「它們的 `SceneTip` 本來就刻意留空」——不必另外寫一條例外。

### 0.1b 唯一的例外：**開場第一次抵達邪佛廣場**（2026-09-18）

> 作者實機回報：「第一次從初始洞窟進到邪佛大廳時，會先出現邪佛跟主角的劇情對話，
> **同時**背景在進行卍字特效把玩家送到地面，然後又跳『邪佛廣場』的場景 tip，這超奇怪的。」

那一刻玩家已經被邪佛的**開場劇情**接手了，再疊一段「卍字把主角從天上送下來」＋一張場景名，
就是三件事同時演。所以那一次**兩樣都不播**，之後每一次回廣場照常。

**判準用存檔既有的 `hubIntroSpawnDone`**，沒有新欄位、沒有新旗標：
那個旗標本來就是為了「第一次進廣場走洞穴出口、之後走中央」而存在的（`MapManager.PlaceAndSetup`），
語意完全吻合——**「第一次抵達廣場」這件事，專案裡已經有人在記了，再記一份只會漂移。**

實作是仿 `_wakeUpWanted` 的老模式：`PlaceAndSetup` 判定並記在 `_skipEnterShow`，
`FireEnterTriggersRoutine` 開頭讀一次就清掉。兩個要點：

- ⚠ **每次進圖都重新賦值**（不是「只在成立時設 true」）：漏掉 else 的話旗標會殘留到下一張圖，
  變成「那張圖也莫名其妙不播」，而且只在特定進圖順序下重現，最難查。
- ⚠ **場景說明的名額照樣佔掉**（`_shownSceneTips.Add` 照跑，只是不 `Show`）：
  開場玩家可能走回洞窟再繞回廣場，那還在同一段開場敘事裡，不該突然冒出名字。
  等真的出發打關卡再回來（跨 module ⇒ 清空）才恢復正常。

⚠️ **代價（務必知道）：`SceneTip` 沒填的地圖＝不跳名字，也不會有卍字進場。**
目前 `BloodFang_*`、`Future_*` 整組的 `SceneTip` 都是空的 → 進那些關卡兩樣都沒有。
新關卡要有卍字，就照第 1 節畫一張名字圖、把 `SceneTip` 填上——**一件事同時開兩個開關，這是刻意的**。
真的哪天需要「有卍字但不跳名字」（或反過來），才值得為它加一個 MapsTable 欄位；
在那之前多一欄就是多一份會漂移的真相。

### 0.2 主角怎麼消失又出現

- 卍字還在天上時，主角走 `Dipan.Cutscene.PlayerVisibility.Hide()`（劇情 `hidePlayer` 用的同一支）整個藏起來——
  **不是 `SetActive(false)`**：那支會連**影子、碰撞、暗場景的提燈光圈**一起關，三個坑寫在它的註解裡。
- 落地後 `Show(false)` 讓 renderer 回來，同時把主角的 `localScale` 從 2% 放大回原尺寸（＝被吐出來），
  這段期間**影子先按著不開**——影子是獨立物件、不跟著縮放，先放出來會看到「小主角配一團原尺寸的影子」。
  吐完才開影子。（離場那支沒處理這件事，是它已知的小瑕疵。）
- 血統特效（頭光／光環／繞行／拖尾）也是獨立物件、`PlayerVisibility` 抓不到，
  2026-09-17 一併在 `PlayerAnimator.BodyFxVisible` 擋掉（見 [PROBLEMS.md](PROBLEMS.md) **G11**）。

### 0.3 為什麼掛在等待鏈的**最前面**

接線在 `FireEnterTriggersRoutine` 的**第一個 `yield` 之前**，兩個理由，別隨手往後搬：

1. `StartCoroutine` 會同步跑到第一個 `yield`，所以主角是**與 `LoadingPanel` 關閉同一幀**被藏起來的——
   晚一步玩家就會看到主角站在那裡閃一幀、才被卍字蓋掉。
2. 「藏主角」與「開始播」之間不隔任何 `yield` ⇒ 不存在「中途換圖 `yield break` ⇒ 主角隱藏狀態殘留」的路徑。

暫停用 `UIManager.SetExternalHold` 的**具名多載**（owner = `"LevelEnterManji"`，見 PROBLEMS **D13**），
時間軸全程 unscaled；每幀增量**夾上限 0.05 秒**，因為本特效正好接在讀取頁關閉後的第一幀（整場最長的一幀），
不夾的話卍字會「瞬移半段」才開始動。

### 0.4 調整

節奏與外觀常數都在 `LevelEnterManjiController` 檔頭（`DescendTime` / `UnwrapTime` / `FadeOutTime`、
`BigSizeMul` / `SmallSizeMul` / `DescendDistMul` / `RotateSpeed`），旁邊註明了各自對應離場的哪一段。
刻意比離場短：後面還接著場景說明（約 1.73 秒）＋可能的進場劇情，整段暫停太久玩家會煩（同第 4 節壓短停留的理由）。

---

## 1. 要讓一張地圖跳名字，做兩件事

1. **畫一張文字圖**，放 `Assets/Resources/UI/Texts/tw/SceneTipPanel_Text_<key>.png`。
   英文版放 `en/` 底下、**檔名一模一樣**（不要加 `_tw`/`_en` 尾綴，見 [LOCALIZATION.md](LOCALIZATION.md)）。
   還沒畫英文版沒關係——`LocalizedArt` 會自動退回繁中，不會開天窗。
2. **`MapsTable.csv` 那一列的 `SceneTip` 欄填 `<key>`**。留空 = 這張圖不跳。

就這樣，不用改程式。

### key 怎麼取名

**key 不是地圖的 `Name` 欄。** `Name` 是程式/檔案的內部名（`Main_Square`），
key 是美術命名（`BuddhaSquare`）——**刻意不綁在一起**：綁了的話，哪天地圖檔改名
（或想把廣場改叫 `Main_Hub`）圖就跟著壞，而且壞法是「安靜地沒東西跳出來」，最難查。

路徑前綴 `UI/Texts/SceneTipPanel_Text_` 寫死在 `SceneTipPanel.TextPathPrefix`：
**規則留在程式、CSV 只填會變的那一段**。之後要搬資料夾或改命名，只改那一個常數。

---

## 2. 什麼時候跳（去重規則）

> **進到一張有填 `SceneTip` 的地圖就跳；同一趟關卡內同一個 key 只跳一次；
> 跨 module 進新關卡時把「已跳過」的紀錄清空。**

去重用的是 **key，不是地圖 id**——所以**整個關卡的房間可以全部填同一個 key**：
不管玩家先走進哪一間都會跳一次，之後房間互跳都不會再跳。
紅嫁衣 10 張圖就是這樣填的（也因此不必去猜玩家實際從哪一間進去）。

紀錄放在 `MapManager._shownSceneTips`（純執行期 `HashSet<string>`，**不進存檔**），
在跨 module 的那一段（`LoadMapRoutine` 的 `moduleEntry` 分支，就在 `TriggerChain.ClearLevelFlags()` 旁邊）清空。

推演幾個情況：

| 情況 | 結果 |
|---|---|
| 廣場 → 打紅嫁衣 | 跨 module → 清空 → 進第一間房跳「紅嫁衣」 |
| 紅嫁衣房間互跳（柴房↔儲藏室↔…） | 同一個 key 已跳過 → **不跳** |
| 過關／死亡回廣場 | 跨 module → 清空 → 跳「邪佛廣場」 |
| 開場：初始森林1 → 森林2 → 初始洞窟 → 廣場 | **四張圖全在同一個 `Main` module 裡**（是房間互跳、不是換關卡）。前三張沒填 → 不跳；走到廣場才第一次跳 |
| 標題 → 讀檔 → 直接進廣場 | `_loadedModule` 是空的 ⇒ 算跨 module → 跳 |

⚠️ **邪佛廣場、初始洞窟、初始森林 1/2 同屬 `Main` module**，所以
「每個 module 顯示一次」這種規則在這裡是行不通的（廣場會被前面三張圖吃掉）。
規則必須是「每張有名字的地圖」＋「用 key 去重」，這是這個設計的關鍵。

---

## 3. 顯示時機（為什麼掛在 FireEnterTriggersRoutine）

`MapManager.FireEnterTriggersRoutine` 是進圖後那條**等待鏈**：
**卍字進場（第 0 節）** → 等進場全螢幕特效（睜眼醒來）播完 → 等「趴地→起身」演完 → **跳場景說明**
→ **開演進場自動劇情**、等它演完 → 才點火進場觸發點。

場景說明插在**進場特效之後、劇情開演之前**（2026-08-27 作者拍板；第一版是劇情演完才跳，
作者在紅嫁衣書房實測覺得順序反了）。作者要的畫面節奏是：**一進圖先看到正常的遊戲畫面（主角在、HUD 在）
→ 跳名字 → 名字淡出後劇情模式才整套出現**（藏主角、演員登場、Skip、上下黑邊）——
不是一進圖就進劇情模式、只是先暫停跳個名字。
掛在這裡而不是另開一支協程，是因為**「等過場播完」的邏輯只該有一份**——
另寫一份遲早會跟這份漂移。

**做法**：`PlaceAndSetup` 判定「這張圖這一趟要跳名字」（`SceneTip` 有填且 key 還沒跳過）時
**不呼叫 `CutsceneDirector.MaybeAutoStart`**，改記 `_autoCutscenePending`，由等待鏈在名字整段播完（含淡出）後才呼叫；
沒名字要跳的圖（開場山道 13/14、房間互跳）仍在 `PlaceAndSetup` 同幀開演——主角一幀都不會露出來，零行為變化。
⚠️ 名字淡出那 0.4 秒玩家已經可以動（第 4 節的已知行為），劇情要到那之後才鎖輸入，所以玩家可能在開演前走個半步；
`hidePlayer` 記的是開演那一幀的位置，收尾放回那裡，不會有事。
`playCutscene` 中途啟動的劇情走觸發鏈，本來就在名字之後，不受影響。

⚠️ **而且要等它整段播完才點火**。第一版沒等，結果進紅嫁衣時「初始對話直接疊在名字上面」
（2026-08-24 作者實測回報）——名字在 `Overlay` 層、對話在 `Window` 層，名字會蓋在對話上。

**等的是 `SceneTipPanel.IsPlaying`（含淡出），不是 `IsOpen`**：`IsOpen` 在 `DoClose` 的第一行
就變 false，那時淡出才剛開始，等它等於「名字還看得見就放行」。`IsPlaying` 讀的是
`gameObject.activeSelf`——UIPanel 淡出跑完會把物件 `SetActive(false)`，那才是整段結束。

**已知且刻意的小行為**：`UIManager.Recompute` 是用 `IsOpen` 算暫停的，所以
**淡出那 0.4 秒玩家已經可以動了**（名字還在淡）。這 0.4 秒不會有對話跳出來（那邊等的是 `IsPlaying`），
比整段凍到透明為止手感好，所以不打算改。

⚠️ **刻意不是「只鎖輸入、不暫停」**：紅嫁衣一進場就有怪，鎖了輸入不暫停等於站著挨打。

⚠️ **`FireEnterTriggersRoutine` 開頭不能再因為「這張圖沒有觸發層」就提早 return**——
場景說明也掛在這條鏈上，沒有進場觸發點的地圖照樣要跳名字。
`regions == null` 的檢查已經移到真的要跑迴圈之前。

---

## 4. 表演與版面

時間軸（全程 unscaled 時間）：`淡入 FadeInSeconds → 停留 HoldSeconds → 淡出 FadeOutSeconds → 關閉`。
預設總長約 **1.73 秒**（0.33 + 1.0 + 0.4；2026-08-28 作者嫌原本 2.6 秒太長，整段等比縮短 1/3）。

**停留刻意壓短**：整段是暫停遊戲的，而**邪佛廣場每過一關/死一次回來都會再跳一次**，
每輪凍 3 秒會很煩。三四個字 1 秒看得完。

**半透明黑幕（2026-08-28 加）**：名字後面鋪一層黑幕（同一般 UI 視窗的遮罩感）——沒有它畫面看起來完全正常、
卻不能動，玩家分不出遊戲被暫停了。**是面板自己鋪的、不是 UIManager 共用遮罩**：共用那張只服務
Window 層（`UpdateBackdrop` 過濾 `Layer == Window`），本面板是 Overlay 層搆不到；自己掛在面板最底層
還順便跟著面板的 CanvasGroup 一起淡入淡出。照 Overlay 守則 `raycastTarget=false`（暫停由 `PausesGame` 負責）。

面板是 `UILayer.Overlay`：蓋在 HUD 與視窗之上，不入堆疊、ESC 不會誤關（同 `BossIntroPanel`）。
⚠️ **這層鋪滿整個畫面，底下每一個 Graphic 都必須 `raycastTarget = false`**，
否則會靜默吃掉玩家的點擊（見 `UILayer.System` 註解裡的同一條守則）。

換圖時面板會自己收掉（`Update` 檢查 `MapManager.IsLoading`）——
跨 module 換圖是一段長達數秒的協程、讀取頁又不暫停遊戲（**PROBLEMS B8** 的通則），
不主動收的話上一張圖的名字會跟到下一張去。

**調版面**：Play 模式中在 Hierarchy 選 `[UIManager] → Layer_Overlay → SceneTipPanel`
（第一次跳過之後才存在），Inspector 即時調，下次進圖立刻套用（版面每次 `Begin` 重算）。
⚠️ 面板是執行期程式生成，**Play 模式調的值退出後不會保存**——調到滿意要把數值回填 `SceneTipPanel.cs` 的預設值。

| Inspector 欄位（英文標籤） | 預設 | 意思 |
|---|---|---|
| `Fade In Seconds` | 0.33 | 淡入秒數 |
| `Hold Seconds` | 1.0 | 完全顯示後停留多久（整段暫停遊戲，所以刻意壓短） |
| `Fade Out Seconds` | 0.4 | 淡出秒數 |
| `Dim Backdrop` | 勾 | 名字後面要不要鋪半透明黑幕（暫停感的視覺提示） |
| `Dim Alpha` | 0.6 | 黑幕濃度（與 UIManager 共用遮罩同濃度；Linear 色彩空間，見 PROBLEMS E11） |
| `Text Height` | 165 | 文字圖顯示高度（寬依原圖比例） |
| `Text Max Width` | 900 | 寬度上限（0 = 不限）；英文名很長時改由寬度決定尺寸 |
| `Text Center Y` | 175 | 文字中心相對畫面中心的垂直位移（+ 往上） |
| `Show Bg` | 勾 | 要不要顯示分隔線 |
| `Bg Sprite Path` | `UI/SceneTipPanel/SceneTipPanel_Bg` | 分隔線圖 |
| `Bg Width` | 610 | 分隔線顯示寬度（高依原圖比例） |
| `Bg Center Y` | 115 | 分隔線中心相對畫面中心的垂直位移 |

（數值都是 CanvasScaler 參考解析度 **1920×1080** 下的像素。）

**為什麼用「高度」而不是「寬度」定文字尺寸**：場景名字數不一樣（`邪佛廣場` 四字 vs `紅嫁衣` 三字），
用高度定尺寸，兩者的**字**才會一樣大。實測兩張圖的「金字佔整張圖的比例」幾乎一致
（寬 0.89/0.87、高 0.74/0.75），所以同一個 `Text Height` 兩張都對得起來——
**之後畫新的場景名圖，維持這個留白比例就不必逐張調版面。**

---

## 5. 目前有名字的場景

| 地圖 | key | 文字圖 |
|---|---|---|
| `Main_Square`(12) 邪佛廣場 | `BuddhaSquare` | tw 1117×375 ／ en 1117×366 |
| 紅嫁衣 10 張全部（1~10） | `RedBridalGown` | tw 2113×744 ／ en 2172×724 |

初始洞窟(11)、初始森林 1/2(13/14)、Future 系列刻意留空（開場那三張是劇情場景，不跳名字）。

⚠️ 紅嫁衣那兩張原圖寬 2113 / 2172 **超過匯入設定的 `Max Size 2048`**，
Unity 會把它們縮到 2048 才用。目前顯示尺寸遠小於此，看不出差別；
之後若要放很大再考慮把那兩張的 Max Size 調到 4096（見 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) 的素材尺寸規範）。

---

## 6. 排查

| 症狀 | 先看 |
|---|---|
| 完全沒跳 | Console 有沒有 `[MapManager] 場景說明「…」`。沒有 → CSV 那一列的 `SceneTip` 是空的、或那一趟已經跳過了（同 key 只跳一次） |
| 卍字進場沒播（名字有跳） | 兩者共用同一份判定，不該發生。先看 Console 有沒有 `LevelEnterManji` 相關錯誤；再確認 `MapManager.FireEnterTriggersRoutine` 開頭那段沒被移走（第 0.3 節） |
| 卍字播了但主角沒出現／整個不見 | `PlayerVisibility` 的 `Show` 沒被呼叫到。停 Play 再 Play 一次（`PlayModeStaticReset` 會歸零）；若必現，看 `LevelEnterManjiController.RestorePlayer` 是否被 OnDestroy 之外的路徑跳過 |
| 進圖後遊戲永遠不開始（畫面正常但不能動） | `LevelEnterManjiController.IsPlaying` 殘留成 true，等待鏈卡住。確認 `PlayModeStaticReset` 裡那一行 `LevelEnterManjiController.ResetForPlayMode()` 還在 |
| 有 Log 但畫面沒東西 | 接著會有 `[SceneTipPanel] MapsTable 的 SceneTip 填了「x」但沒有對應的文字圖` → 檔名或資料夾錯了。⚠️ 這種情況**名額會還回去**，補上圖之後同一趟再進來還跳得出來 |
| 跳出來但字太大/太小/位置不對 | Play 模式選 `Layer_Overlay → SceneTipPanel` 調，調完回填程式預設值（第 4 節） |
| 進場對話又疊在名字上 | `FireEnterTriggersRoutine` 那道 `while (tip.IsPlaying)` 等待被拿掉了，或改成等 `IsOpen`（見第 3 節） |
| 一進圖就進劇情模式（主角不見、演員已在），名字才跳 | `PlaceAndSetup` 那道「這一趟要跳名字就不呼叫 `MaybeAutoStart`」的判定被拿掉了（見第 3 節） |
| 有名字的圖劇情永遠不開始 | `FireEnterTriggersRoutine` 名字之後那段 `_autoCutscenePending` → `MaybeAutoStart` 被拿掉、或協程在名字之前就 `yield break` 了（看 Console 有沒有印「場景說明」） |
| 每輪回廣場被凍太久 | 調 `Hold Seconds`（第 4 節）。整段長度 = 淡入＋停留＋淡出 |
| 房間互跳一直重跳 | 那幾張圖填了**不同的 key**。同一個關卡要共用同一個名字就填同一個 key |
| 換了語言沒換圖 | `UIManager` 會在語言切換時把面板全部丟掉重建，正常不會發生；先確認 `en/` 底下的檔名與 `tw/` **完全同名** |
