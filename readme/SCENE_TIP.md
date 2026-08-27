# 場景說明 (Scene Tip)

> 返回 [文件總覽](README.md)

進到一張「有名字」的地圖時，畫面上方淡入一張金色毛筆場景名、底下墊一條血紅分隔線，
停留一下自動淡出。**整段暫停遊戲、鎖住操作**，播完才把場面交還給遊戲——
所以名字一定看得完整，這張圖的**進場自動劇情**、進場對話／教學也一定接在它後面
（2026-08-27 起：一進圖是正常畫面 → 跳名字 → 名字淡出後劇情模式才整套出現；之前是劇情演完才跳）。

| 東西 | 在哪 |
|---|---|
| 資料 | `Assets/Data/MapsTable.csv` 的 **`SceneTip` 欄**（第 12 欄） |
| 面板 | `Assets/Scripts/UI/Panels/SceneTipPanel.cs` |
| 觸發 | `MapManager.FireEnterTriggersRoutine`（與進場觸發同一條等待鏈） |
| 文字圖 | `Assets/Resources/UI/Texts/<語言>/SceneTipPanel_Text_<key>.png` |
| 分隔線 | `Assets/Resources/UI/SceneTipPanel/SceneTipPanel_Bg.png`（全場景共用一張） |

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
等進場全螢幕特效（睜眼醒來）播完 → 等「趴地→起身」演完 → **跳場景說明** → **開演進場自動劇情**、等它演完 → 才點火進場觸發點。

場景說明插在**進場特效之後、劇情開演之前**（2026-08-27 作者拍板；第一版是劇情演完才跳，
作者在紅嫁衣書房實測覺得順序反了）。作者要的畫面節奏是：**一進圖先看到正常的遊戲畫面（主角在、HUD 在）
→ 跳名字 → 名字淡出後劇情模式才整套出現**（藏主角、演員登場、Skip、上下黑邊）——
不是一進圖就進劇情模式、只是先暫停跳個名字。
掛在這裡而不是另開一支協程，是因為**「等過場播完」的邏輯只該有一份**——
另寫一份遲早會跟這份漂移。

**做法**：`PlaceAndSetup` 判定「這張圖這一趟要跳名字」（`SceneTip` 有填且 key 還沒跳過）時
**不呼叫 `CutsceneDirector.MaybeAutoStart`**，改記 `_autoCutscenePending`，由等待鏈在名字整段播完（含淡出）後才呼叫；
沒名字要跳的圖（開場山道 13/14、房間互跳）仍在 `PlaceAndSetup` 同幀開演——主角一幀都不會露出來，零行為變化。
⚠️ 名字淡出那 0.6 秒玩家已經可以動（第 4 節的已知行為），劇情要到那之後才鎖輸入，所以玩家可能在開演前走個半步；
`hidePlayer` 記的是開演那一幀的位置，收尾放回那裡，不會有事。
`playCutscene` 中途啟動的劇情走觸發鏈，本來就在名字之後，不受影響。

⚠️ **而且要等它整段播完才點火**。第一版沒等，結果進紅嫁衣時「初始對話直接疊在名字上面」
（2026-08-24 作者實測回報）——名字在 `Overlay` 層、對話在 `Window` 層，名字會蓋在對話上。

**等的是 `SceneTipPanel.IsPlaying`（含淡出），不是 `IsOpen`**：`IsOpen` 在 `DoClose` 的第一行
就變 false，那時淡出才剛開始，等它等於「名字還看得見就放行」。`IsPlaying` 讀的是
`gameObject.activeSelf`——UIPanel 淡出跑完會把物件 `SetActive(false)`，那才是整段結束。

**已知且刻意的小行為**：`UIManager.Recompute` 是用 `IsOpen` 算暫停的，所以
**淡出那 0.6 秒玩家已經可以動了**（名字還在淡）。這 0.6 秒不會有對話跳出來（那邊等的是 `IsPlaying`），
比整段凍到透明為止手感好，所以不打算改。

⚠️ **刻意不是「只鎖輸入、不暫停」**：紅嫁衣一進場就有怪，鎖了輸入不暫停等於站著挨打。

⚠️ **`FireEnterTriggersRoutine` 開頭不能再因為「這張圖沒有觸發層」就提早 return**——
場景說明也掛在這條鏈上，沒有進場觸發點的地圖照樣要跳名字。
`regions == null` 的檢查已經移到真的要跑迴圈之前。

---

## 4. 表演與版面

時間軸（全程 unscaled 時間）：`淡入 FadeInSeconds → 停留 HoldSeconds → 淡出 FadeOutSeconds → 關閉`。
預設總長約 **2.6 秒**（0.5 + 1.5 + 0.6）。

**停留刻意壓短到 1.5 秒**：整段是暫停遊戲的，而**邪佛廣場每過一關/死一次回來都會再跳一次**，
每輪凍 3 秒會很煩。三四個字 1.5 秒看得完。

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
| `Fade In Seconds` | 0.5 | 淡入秒數 |
| `Hold Seconds` | 1.5 | 完全顯示後停留多久（整段暫停遊戲，所以刻意壓短） |
| `Fade Out Seconds` | 0.6 | 淡出秒數 |
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
| 有 Log 但畫面沒東西 | 接著會有 `[SceneTipPanel] MapsTable 的 SceneTip 填了「x」但沒有對應的文字圖` → 檔名或資料夾錯了。⚠️ 這種情況**名額會還回去**，補上圖之後同一趟再進來還跳得出來 |
| 跳出來但字太大/太小/位置不對 | Play 模式選 `Layer_Overlay → SceneTipPanel` 調，調完回填程式預設值（第 4 節） |
| 進場對話又疊在名字上 | `FireEnterTriggersRoutine` 那道 `while (tip.IsPlaying)` 等待被拿掉了，或改成等 `IsOpen`（見第 3 節） |
| 一進圖就進劇情模式（主角不見、演員已在），名字才跳 | `PlaceAndSetup` 那道「這一趟要跳名字就不呼叫 `MaybeAutoStart`」的判定被拿掉了（見第 3 節） |
| 有名字的圖劇情永遠不開始 | `FireEnterTriggersRoutine` 名字之後那段 `_autoCutscenePending` → `MaybeAutoStart` 被拿掉、或協程在名字之前就 `yield break` 了（看 Console 有沒有印「場景說明」） |
| 每輪回廣場被凍太久 | 調 `Hold Seconds`（第 4 節）。整段長度 = 淡入＋停留＋淡出 |
| 房間互跳一直重跳 | 那幾張圖填了**不同的 key**。同一個關卡要共用同一個名字就填同一個 key |
| 換了語言沒換圖 | `UIManager` 會在語言切換時把面板全部丟掉重建，正常不會發生；先確認 `en/` 底下的檔名與 `tw/` **完全同名** |
