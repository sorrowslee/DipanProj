# 序章開場漫畫 (Intro Comic：韓漫式分鏡播放器)

> 返回 [文件總覽](README.md)｜墜落動畫見 [INTRO_FALL.md](INTRO_FALL.md)｜UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)

開場序章的「一張張分鏡」用**韓漫（webtoon）風格**演出：每格分鏡外面罩一個**白框**，框只露出整張圖**最重要的一塊**；玩家每按一次空白鍵，下一格從指定方向**滑入**畫面、**累積**成構圖；到情緒轉折就**清空前面所有格**換下一組；最後三格（墜落 09~11）標成**全篇幅**，清空全部、用整個畫面、每秒換一張特寫，播完接 [INTRO_FALL.md](INTRO_FALL.md) 的墜落程式動畫。

全程式建構（零 prefab、零美術接線），自己在 `Awake` 建一套 Screen-Space Overlay Canvas，風格同 `IntroFallController`。**「露出哪一塊／擺哪裡／從哪滑入／轉幾度」全部是 Inspector 上的資料，改數字即可、不動程式**——刻意做成方便反覆微調。

> 一條龍：**開場漫畫（01~08 分鏡 + 09~11 全篇幅）→ 墜落程式動畫 → 正式遊戲場景**。

---

## 0. 整段在哪裡播

放在獨立的 `Intro` 場景（同 [INTRO_FALL.md](INTRO_FALL.md) 第 0 節的理由）。場景裡有兩個控制器：先播 `IntroComicController`（漫畫），播完啟用 `IntroFallController`（墜落），墜落播完載入 `SampleScene`。

---

## 1. Unity 接線

1. 在 `Intro` 場景放一個空物件 `[IntroComic]`，掛 **`IntroComicController`**。
2. 把場景裡的 `[IntroFall]`（`IntroFallController`）**整個 GameObject 先停用**（取消勾選 active），拖到 `IntroComicController` 的 **`FallToTrigger`** 欄。漫畫播完會自動啟用它（它的 `OnEnable` 就會開始墜落）。
3. 圖已複製到 `Assets/Resources/InitialStory/Story_01`～`Story_11`（控制器用 `Resources.Load<Texture2D>` 載、runtime 裁切；不挑 import 類型）。
4. 按 Play：延遲 `StartDelay` 秒後自動顯示第一格 → 空白鍵一格格推進 → 墜落 → 進遊戲。

> 不接 `FallToTrigger` 時：若勾 `AutoLoadNextScene` 就直接載 `NextSceneName`；都不設就只發 `OnComplete` 事件（自己接）。

**操作鍵**：空白鍵 / 點畫面＝下一格；`R`＝重播；`Esc`＝直接跳結尾。（正式上線可在 Inspector 改鍵或關掉。）

---

## 2. 資料驅動：一格分鏡 = `ComicPanel` 一筆

`Panels` 清單**留空時自動帶入 Story_01~11 的首版排版**（見 §4）。每一格欄位：

| 欄位 | 說明 |
|---|---|
| `Image` | 圖名（`Resources/InitialStory/` 下檔名，不含副檔名），例 `Story_03` |
| `Cluster` | 叢集編號。**相同編號的格累積在同一畫面；編號一變＝先清空前面所有格**再演下一組 |
| `Pos` | 白框在畫面上的位置（0~1，左下=0,0 右上=1,1；x 0.5＝水平置中） |
| `HeightFrac` | 白框高度 = 參考螢幕高(1080) × 此值；**寬度依裁切後長寬比自動算**（不會變形） |
| `Crop` | **露出原圖哪一塊**（正規化 x,y,w,h；`(0,0,1,1)`＝整張）。**y 由下往上**，臉在上半就拉高 y |
| `Rotation` | 白框旋轉角度（度，正＝逆時針）做出隨興斜貼感 |
| `From` | 從哪個方向滑入：`Left` / `Right` / `Top` / `Bottom` / `FadeOnly`（只淡入不滑） |
| `SlideSeconds` | 該格滑入時間；0＝用全域 `DefaultSlideSeconds` |
| `Punch` | 彈入回彈（overshoot），給「重擊格」（例：抓手腕 05） |
| `Shake` | 滑入後輕微抖動，給「衝擊格」（例：欄杆斷裂 08） |
| `Fullscreen` | 全篇幅特寫：清空所有格、等比放大**覆蓋**整個畫面（給墜落 09~11） |
| `HoldSeconds` | 全篇幅停留秒數；>0＝到時自動演下一格，<=0＝等玩家按鍵 |

---

## 3. 怎麼微調（對應你想討論的三件事）

* **每格罩住哪些、露出哪些重要部分** → 改 `Crop`。想露臉就把臉那塊的正規化矩形填進去（y 由下往上）；想要電影感橫條就壓低 `height`、拉滿 `width`。整張就 `(0,0,1,1)`。
* **每格擺哪裡 / 多大 / 斜幾度** → 改 `Pos`、`HeightFrac`、`Rotation`。
* **每格從哪邊滑入** → 改 `From`；滑入快慢改 `SlideSeconds`（或全域 `DefaultSlideSeconds`）。
* **哪幾格疊一起 / 哪裡清空換氣** → 用 `Cluster`：要疊就同號，要「清空、換下一組構圖」就跳號。
* **兩個情緒爆點** → `Punch`（彈入）給重擊格、`Shake`（抖動）給斷裂格。
* **墜落收尾節奏** → 09~11 設 `Fullscreen=true`、`HoldSeconds=1.1`（每 1.1 秒換一張）。想慢一點就調大。

> 全域手感參數在元件上方：`DefaultSlideSeconds`（滑入時間）、`ClearFadeSeconds`（清空淡出）、`StartDelay`（起播延遲）、白框 `FrameColor`/`FrameBorder`、陰影 `ShowShadow`/`ShadowColor`/`ShadowOffset`。

---

## 4. 首版排版（草案，全部可改）

| 叢集 | 格 | 露出（Crop 意圖） | 位置 / 大小 | 滑入 | 備註 |
|---|---|---|---|---|---|
| 0 登場相遇 | 01 主角登場 | 整張（單獨定場） | 左、最大 | 下 | |
| 0 | 02 山徑相遇 | 中段橫條（步道＋三人） | 右上、中 | 右 | |
| 1 危機 | 03 妹妹與蝴蝶 | 上半（笑臉＋蝶） | 左上 | 上 | |
| 1 | 04 追蝶探向欄杆 | 整張 | 右、大 | 右 | |
| 1 | 05 抓住手腕 | 中央方塊（雙手） | 中下、小 | 下 | `Punch` 重擊 |
| 2 溫情 | 06 一家三口道謝 | 整張 | 偏左、大 | 上 | 清空換氣 |
| 2 | 07 倚欄放鬆 | 整張 | 右 | 右 | |
| 3 斷裂 | 08 欄杆斷裂 | 去掉底部（聚焦驚恐） | 置中、最大 | 左 | `Shake` 抖動 |
| 4 墜落 | 09→10→11 | 全篇幅 cover | 滿版 | 淡入 | 每 1.1 秒換一張 |

> 各格的 `Crop` 是我憑圖估的起點，**實機看了一定會想微調**——直接改 Inspector 數字即可。

---

## 5. 相關檔案

* `Assets/Scripts/Intro/IntroComicController.cs` — 漫畫播放器（Canvas/白框/裁切/滑入/叢集清空/全篇幅/收尾，全部在這）。
* `Assets/Scripts/Intro/IntroFallController.cs` — 接在後面的墜落動畫（見 [INTRO_FALL.md](INTRO_FALL.md)）。
* `Assets/Resources/InitialStory/Story_01`～`Story_11` — 開場分鏡圖（為 `Resources.Load` 複製的一份；原圖在 `GameAssets/Main/InitialStory/`）。

---

## 6. 待辦 / 已知限制（首版）

- [ ] **各格 `Crop` 微調**：首版是憑圖估的，待實機對齊（這是預期會反覆調的部分）。
- [ ] **白框質感**：目前白底 + 純色陰影。要更韓漫可加細描邊、圓角、或紙質底，再決定要不要程序化。
- [ ] **回看 / 倒退**：目前只能往前。若要「上一格」可再加（清空後重建較麻煩，需要時再說）。
- [ ] **跳過整段**：`Esc` 直接到結尾（接墜落）。正式版可改成長按跳過 + 提示。
- [ ] **音效**：分鏡滑入、重擊、斷裂、墜落的聲音待音訊系統（見 [TODO.md](TODO.md)）就緒後接。
- [ ] **與漫畫前的標題畫面 / 選角**：之後 Intro 場景可長成入口（見 [INTRO_FALL.md](INTRO_FALL.md) 第 0 節）。

---

*建立於 2026-06-29：韓漫式開場漫畫播放器（白框 runtime 裁切露出、空白鍵滑入、叢集累積/清空、09~11 全篇幅計時、接 IntroFallController）。排版/裁切/方向全資料驅動，方便反覆微調。*
