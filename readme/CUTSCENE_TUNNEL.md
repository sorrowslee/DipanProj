# 過場系統：穿隧道 → 播影片 → 換圖 (Cutscene / Tunnel Walk)

> 全程式建構、零美術/零 prefab/零 Animator。三個元件串成一條一次性的過場鏈，由地圖上的 **cutscene 觸發點** 啟動。
> 目前主要用途：**初始山洞洞口 →（穿隧道表演）→（邪佛影片）→ 邪佛廣場**。

---

## 1. 總覽：這條鏈怎麼跑

玩家走到地圖上一格 **cutscene 觸發點** → 觸發一段過場表演，**一次性**（觸發過就不再觸發，即使走回去）：

```
踩到 cutscene 觸發格
   │
   ▼
穿隧道表演 (TunnelWalkController)
   ‑ 整片黑，遠方發光拱門洞口
   ‑ 每按一次空白鍵/左鍵：畫面晃一下 + 洞口放大一級（等比）
   ‑ 按滿 Steps 下 → 白光罩滿 → 觸發 OnComplete
   │
   ▼
播影片 (VideoPlayerOverlay)         ← cutscene 的 video 欄有填才會走這步
   ‑ 立刻黑幕蓋滿 → 準備 mp4 → 第一幀就緒才顯示 → 全螢幕播放
   ‑ 播完 / 按 Esc / 點擊 → 黑幕淡入
   │
   ▼
換圖 (MapManager.GoToMap)           ← 在黑幕/白幕後面發生，玩家看不到載入
   ‑ 載入 targetMapId，玩家落在 targetEntrance
   ‑ 黑幕/白幕淡出，露出新地圖（邪佛廣場）
```

若 cutscene 的 `video` 欄留空 → 跳過影片那步，穿隧道完直接換圖（白光淡出露出新圖）。

---

## 2. 三個元件

### 2.1 `CutsceneWatcher`（地圖端，掛在 MapManager 上）
`Assets/Scripts/Map/CutsceneWatcher.cs`（仿 `TeleportWatcher`）

- 每幀比對玩家所在格 vs 當前地圖的 `cutscene` 觸發格；踩到就 `Fire()`。
- **一次性**：`_fired` 觸發過就不再觸發。
- **武裝機制 `_armed`**：出生/著陸時若剛好站在觸發格上，要先離開該格才會「武裝」，避免一進場就觸發。
- `Fire()` 讀觸發點參數：`targetMapId`、`targetEntrance`、`video`。
- 串接：`tunnel.Play()` → 監聽 `OnComplete` → `AfterPerformance()`：
  - 有 `video` 且場景有 `VideoPlayerOverlay` → `video.Play(檔名, GoNext)`；
  - 否則直接 `GoNext()` → `MapManager.GoToMap(targetMapId, targetEntrance)`。
- 由 `MapManager.SetupWatcher()` 在**每次換圖後** `Setup(...)` 重建格表、重置狀態。

### 2.2 `TunnelWalkController`（穿隧道表演）
`Assets/Scripts/Cutscene/TunnelWalkController.cs`（namespace `Dipan.Cutscene`）

自己在 Awake 建一套 Screen-Space Overlay Canvas（**sortingOrder 1200**），全程 `unscaledDeltaTime`，可在播放時 `Time.timeScale = 0` 暫停遊戲。

| 欄位 | 預設 | 說明 |
|---|---|---|
| `Steps` | 5 | 要按幾下才徹底走出隧道 |
| `StartDelay` | 0.2 | 起播前延遲（秒） |
| `ExitImage` | 空 | 自備洞口圖（白色去背 PNG）；留空＝程式畫拱門 |
| `ExitColor` | 暖白 | 洞口光顏色 |
| `ExitStartFrac` | 0.30 | 最初（最遠）洞口大小＝螢幕高×此值 |
| `ExitEndFrac` | 1.7 | 走出時（最近）洞口大小（>1 撐滿畫面） |
| `GrowSeconds` | 0.30 | 每按一下放大的動畫時間 |
| `ExitYOffset` | 0 | 洞口垂直位置（螢幕高比例） |
| `ShakeAmount` | 52 | 每步左右橫衝幅度（像素，左右交替） |
| `ShakeSeconds` | 0.26 | 晃動持續時間 |
| `AdvanceKey` / `ClickToAdvance` | Space / true | 前進鍵 |
| `PauseWhilePlaying` | true | 播放期間 `Time.timeScale=0` |
| `FadeOutSeconds` | 0.5 | 走出後白光淡出露出新地圖的時間（換圖在白幕後發生） |
| `PlayOnStart` / `TestPlayKey` | false / T | 測試用 |

- **洞口放大是「等比」**：`ExitSizeAt(step) = a·(b/a)^(step/Steps)`，每步視覺變化比例一致，第一下不會突兀（早期「線性放大」第一下看起來跨太大）。洞口形狀 `MakeTunnelMouth()` 是「半圓頂 ∪ 直壁矩形」＝卡通火車隧道拱門。
- 收尾：按滿 Steps → 白光罩滿 → `OnComplete`（在白幕下換圖）→ `Fadeout` 把白光淡出露出新圖。

### 2.3 `VideoPlayerOverlay`（過場影片播放器）
`Assets/Scripts/Cutscene/VideoPlayerOverlay.cs`（namespace `Dipan.Cutscene`）

自建 Canvas（**sortingOrder 1300**，蓋在隧道 1200 之上）＋ `VideoPlayer` 元件，把 mp4 渲染到 RenderTexture → RawImage 全螢幕（`FitInside`，黑底）。

| 欄位 | 預設 | 說明 |
|---|---|---|
| `SubFolder` | "Video" | 影片放 StreamingAssets 的哪個子資料夾 |
| `AllowSkip` / `SkipKey` | true / Esc | 可按鍵跳過 |
| `ClickToSkip` | true | 可點擊跳過 |
| `PauseWhilePlaying` | true | 播放期間 `Time.timeScale=0` |
| `FadeSeconds` | 0.4 | 結束/跳過後黑幕淡入、換圖、再淡出的時間 |

- `Play(fileName, onDone)`：`fileName` 空＝直接 `onDone()`；否則 `url = StreamingAssets/<SubFolder>/<fileName>`，`Prepare()` → 第一幀就緒 → 顯示並播放。
- **黑幕先蓋再準備**（避免露出場景）：`Play()` 一被呼叫就立刻把 Canvas 開啟、用全黑 BG 蓋滿整個畫面，並先關掉影片 RawImage；等 `prepareCompleted` 第一幀就緒才顯示影片。如此不管首次載入準備多久，畫面都只會是黑的，**不會露出後面的場景**。
- 播完/跳過 → 黑幕淡入 → 全黑時呼叫 `onDone`（＝在黑幕後 `GoToMap` 載入新圖）→ 黑幕淡出露出新圖。
- 影片錯誤（檔案不存在/編碼不支援）→ 印 `[VideoOverlay] 影片錯誤…略過` 並直接走 `onDone`，**不會卡死**。

---

## 3. Unity 接線

1. **MainScene** 各放一個 GameObject：一個掛 `TunnelWalkController`、一個掛 `VideoPlayerOverlay`。
2. 把它們拖進 **`MapManager`** 的 `tunnelWalk` / `videoOverlay` 欄（留空會 `FindObjectOfType` 兜底）。
3. `MapManager` 每次換圖在 `SetupWatcher()` 內 `CutsceneWatcher.Setup(..., tunnelWalk, videoOverlay)`，自動接好。

Canvas 層級（sortingOrder）：開場漫畫 1000（→1100 交棒墜落）、穿隧道 1200、影片 1300。

---

## 4. 影片檔案放哪、怎麼設

- **檔案位置**：`Assets/StreamingAssets/Video/<檔名>.mp4`（例 `EvilBuddha.mp4`）。
  - 用 **H.264 編碼的 mp4** 最保險（Unity VideoPlayer 在 Win/Mac standalone 都吃）。StreamingAssets 原樣複製進 build、不轉檔，所以格式要對。
  - ⚠️ 單一 mp4 **> 100MB** 時 `git push`（deploy）會被 GitHub 拒（同 PROBLEMS A9）→ 壓縮或上 Git LFS。
- **觸發點設定**（地圖編輯器）：在洞口畫一格 `cutscene` 觸發點，填參數：
  - `video` = 影片檔名（例 `EvilBuddha.mp4`；留空＝跳過影片）
  - `targetMapId` = 過場完要去的地圖 ID（邪佛廣場）
  - `targetEntrance` = 目標地圖落點（可留空＝目標圖的 playerSpawn）
  - `cutsceneId` = 哪段過場（目前任意值＝穿隧道）
- 改完觸發點參數後，遊戲端跑一次 **Sync Map Assets**（參數存在 `.dipanmap` 裡，要同步過去）。

### 編輯器端 cutscene 觸發點型別
`DipanProj_MapEditor/Assets/StreamingAssets/triggerTypes.json` + `TriggerType.cs` 的 `Defaults()`：
`typeId="cutscene"`、displayName「事件/過場觸發點」、color `#22CC99`、params `cutsceneId`(String)/`video`(String)/`targetMapId`(Int)/`targetEntrance`(String)。

---

## 5. 注意事項 / 踩過的坑

- **黑幕先蓋再準備影片**：早期是「準備好第一幀才顯示影片、期間靠隧道白光遮」，但隧道收尾會把白光淡出，於是「白光淡出後、影片還沒準備好」那一小段空檔會**露出後面的山洞**（首次跑因載入較慢特別明顯）。改成 `Play()` 一呼叫就立刻黑幕蓋滿、影片 RawImage 先關，第一幀就緒才顯示。代價：白光→黑幕是「硬切」（對恐怖遊戲轉場其實自然）。
- **字串插值裡的三元運算子會編譯爆炸**：`$"...{a ? b : c}..."` 的 `:` 會被當成插值的格式分隔符，整串爆 `CS8076/CS8361/CS1003/CS1525`，**整個 assembly 編不過 → 所有腳本掛不上、Add Component 搜不到新元件**。解法：把三元式用括號包成 `{(a ? b : c)}`，或抽成一行變數再插入。詳見 [PROBLEMS.md](PROBLEMS.md) D7。
- **改程式預設值不會更新場景上已存在的元件**：Unity 對已序列化欄位保留舊值；要調已存在元件就直接在 Inspector 改，或右鍵 → Reset，或把欄位改名吃新預設（通則，見 [INTRO_FALL.md](INTRO_FALL.md)）。
- **VideoPlayer 播 StreamingAssets**：用 `VideoPlayer.url`（不是 VideoClip 資產），`source = VideoSource.Url`。影片放 StreamingAssets 不會進 `resources.assets`。
- **`MapManager.startModule`**：墜落後要落在初始山洞，確認起始 module 指向洞穴所在模組（`Main`）。

---

## 6. 相關檔案

- `Assets/Scripts/Map/CutsceneWatcher.cs` — 過場觸發監看（仿 TeleportWatcher，一次性串接表演）。
- `Assets/Scripts/Cutscene/TunnelWalkController.cs` — 穿隧道互動表演（Canvas 1200、等比放大拱門、左右晃、白光收尾）。
- `Assets/Scripts/Cutscene/VideoPlayerOverlay.cs` — 全螢幕 mp4 播放器（Canvas 1300、黑幕先蓋、淡黑換圖）。
- `Assets/Scripts/Map/MapManager.cs` — `tunnelWalk` / `videoOverlay` 欄位、`SetupWatcher()` 接線。
- `Assets/Scripts/Map/MapLoader.cs` — `cutsceneTypeId = "cutscene"`。
- `Assets/StreamingAssets/Video/` — 過場影片 mp4 放這。
- 編輯器：`DipanProj_MapEditor/.../triggerTypes.json`、`TriggerType.cs`、`Catalog.cs`（Main 模組可編）。

---

*建立於 2026-06-30：整理「穿隧道 → 播影片 → 換圖」過場鏈為專屬文件。本次接上出隧道影片步驟（`VideoPlayerOverlay`，mp4 放 `StreamingAssets/Video/`），並修兩個問題：(1) 字串插值三元運算子導致整個 assembly 編不過、元件掛不上；(2) 影片準備期間會露出後面場景 → 改成 `Play()` 一呼叫就黑幕蓋滿、第一幀就緒才顯示。*

*更新於 2026-06-30：修跳過影片時「影片關掉 → 又閃一下 → 才真正關閉」。原因＝黑幕淡入蓋住後只 `Pause` 影片，`_video`（RawImage）還開著，接著黑幕淡出又露出暫停的最後一幀。修法：`SwitchUnderBlack`（轉全黑時）`_video.enabled = false`，淡出只剩黑底、乾淨過渡到載入頁/新地圖。*
