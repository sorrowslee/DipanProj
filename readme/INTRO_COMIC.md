# 序章開場漫畫 (Intro Comic：整頁 + 導讀式鏡頭)

> 返回 [文件總覽](README.md)｜墜落動畫見 [INTRO_FALL.md](INTRO_FALL.md)｜UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)

開場序章的漫畫用**「整頁成品圖 + 導讀式鏡頭」**演出：每一「頁」是一張**你自己排好版的完成圖**（panels 已拼好、含白框/對白），程式把整頁放大、鏡頭依序對焦每一「格」（你指定的矩形），平移＋縮放讓那一格填滿畫面、看得清楚；按空白鍵鏡頭移到下一格，整頁看完換下一頁。最後幾頁設 Fullscreen（下墜 13~15：全篇幅、自動每秒換），播完接 [INTRO_FALL.md](INTRO_FALL.md) 的墜落程式動畫。

全程式建構（零 prefab），自己在 `Awake` 建一套 Screen-Space Overlay Canvas（放在自生的子物件上）。**對齊與解析度由「你出的整頁高解析圖」決定，程式只負責鏡頭運動**，所以版面永遠對得準、夠清晰。

> 一條龍：**開場漫畫（3 頁導讀 + 下墜 13~15 全篇幅）→ 墜落程式動畫（IntroFall）→ 正式遊戲場景 `MainScene`（落在 Tutorial_Cave）**。

> ℹ️ **設計沿革**：早期試過「白框分鏡從不同方向滑入、累積、清空」與「整頁由上往下遮罩揭示」兩種做法，都因為**個別格對齊難、近正方圖兩側黑邊、靜止感**等問題放棄。最終定案＝**整頁成品圖 + 鏡頭平移對焦**（本文件描述的版本）。

---

## 0. 整段在哪裡播

放在獨立的 `Intro` 場景（同 [INTRO_FALL.md](INTRO_FALL.md) 第 0 節的理由）。場景裡有兩個控制器：先播 `IntroComicController`（漫畫），播完啟用 `IntroFallController`（墜落），墜落播完載入 `MainScene`。

> **打包提醒**：`Intro` 與 `MainScene` **兩個場景都要在 build 裡，且 Intro 排第 0 個**（開機載入的場景）。`BuildScript.cs` 的 `options.scenes` 已設成 `{ "Assets/Scenes/Intro.unity", "Assets/Scenes/MainScene.unity" }`。只放 MainScene 的話，遠端執行檔會直接從遊戲場景開始、看不到開場（見 [BUILD_AND_DEPLOY.md](BUILD_AND_DEPLOY.md)）。

---

## 1. Unity 接線

1. 在 `Intro` 場景放一個空物件 `[IntroComic]`，掛 **`IntroComicController`**。
2. 把場景裡的 `[IntroFall]`（`IntroFallController`）**整個 GameObject 先停用**（取消勾選 active），拖到 `IntroComicController` 的 **`FallToTrigger`** 欄。漫畫播完會自動啟用它（它的 `OnEnable` 就開始墜落）。
3. 頁面圖放在 `Assets/Resources/InitialStory/`：`Page_01`、`Page_02`、`Page_03`（三頁導讀）＋ `Story_13`、`Story_14`、`Story_15`（下墜全篇幅）。控制器用 `Resources.Load<Texture2D>` 載＋`Sprite.Create`（不挑 import 類型）。
4. 按 Play：延遲 `StartDelay` 秒後自動進第一頁、鏡頭推到第一格 → 空白鍵一格格推進。

> **改圖工作流**：控制器只讀 `Resources/InitialStory/`。你更新頁面圖時，**直接存/覆蓋到那個資料夾**（不是 `GameAssets/Main/InitialStory/`，那是另一份、不會自動同步），回 Unity 等它重新匯入再 Play。

**操作鍵**：空白鍵 / 點畫面＝下一格／下一頁；`R`＝重播；`Esc`＝直接跳結尾（同 Skip）。

---

## 2. 資料結構：`ComicPage` ＋ `Focus`

`Pages` 清單在 **Inspector 右鍵元件標題 →「帶入預設頁面排版…」** 可一鍵填入預設（Page_01~03 + 下墜 13~15）；之後就能展開逐格調。執行時若 `Pages` 為空才自動帶預設。

**一頁 `ComicPage`**：

| 欄位 | 說明 |
|---|---|
| `Image` | 頁面圖名（`Resources/InitialStory/` 下檔名，不含副檔名），例 `Page_01` |
| `Focuses` | 依序對焦的鏡頭清單（見下）。鏡頭一格一格移過去；**留空＝整頁當一格** |
| `Fullscreen` | 全篇幅模式（給下墜 13~15）：不對焦、整張 **cover** 滿版、靜止、依 `HoldSeconds` 自動換 |
| `HoldSeconds` | Fullscreen 自動換頁停留秒數；>0＝到時自動下一頁，<=0＝等按鍵 |

**一個鏡頭 `Focus`**（每格/每組一個）：

| 欄位 | 說明 |
|---|---|
| `Area` | 這格/這組在頁面上的矩形（x,y=左上角，w,h，皆頁面比例 0~1，**y 由上往下**）。**要綁兩格就框住兩格**，中心會落在兩格之間 |
| `Zoom` | 縮放微調：1＝這格剛好填滿；>1 更近(裁更多)、<1 拉遠(留邊) |
| `XOffset` | 左右微調：**正數＝內容往右移**、負數往左。單位＝螢幕寬比例(0.1≈10%) |
| `YOffset` | 上下微調：**正數＝內容往上移**（修正「這格太靠下」）、負數往下。單位＝螢幕高比例 |

---

## 3. 怎麼微調

* **鏡頭框哪一格** → 改該 `Focus.Area`（頁面比例矩形）。
* **某格太大/太小** → `Zoom`（>1 拉近、<1 拉遠）。
* **某格沒對準** → `XOffset`／`YOffset`（正 X＝右、正 Y＝上）。
* **兩格綁一組看**（中心在兩格之間） → 用一個 `Focus`，`Area` 框住兩格。預設已把 **第一頁 2,3** 與 **第三頁 10,11** 綁成一組。
* **填滿 vs 完整顯示** → 全域 `FillScreen`：開＝每格填滿畫面(cover，可能裁一點邊)；關＝完整顯示整格(fit，邊緣會看到鄰格)。每格 `Zoom` 在此基礎上微調。
* **鏡頭快慢** → `PanSeconds`（移到下一格）、`EnterSeconds`（進每頁從整頁推進到第一格）。
* **下墜節奏** → 13~15 設 `Fullscreen`、`HoldSeconds`（每張停留秒數）。下墜預設整張 **fit/cover 靜止**（早期試過「填滿＋往下飄」、已移除，因為主角在圖中位置不一容易裁到/太亂；改成出圖時把主角擺中間一帶即可）。

> 全域參數（元件上方）：`FillScreen`、`PanSeconds`、`EnterSeconds`、`ClearFadeSeconds`、`StartDelay`、舞台底 `ShowBackdrop`/`BackdropColor`/`ShowVignette`。

---

## 4. 右上角「Skip」（兩段式）

右上角有一個**粗體放大、無背景、帶外框**的 `Skip` 字樣（程式建立，`BuildSkip`），漫畫一開始就在、永遠在最上層。點它或按 `Esc`：

1. **漫畫進行中按** → 結束漫畫、**接上墜落動畫**（啟用 `FallToTrigger`）。此時漫畫畫面清掉、只留 Skip 浮在墜落之上（canvas 提到 sortingOrder 1100）。
2. **墜落動畫中再按一次** → **結束墜落、進入 trigger 場景**（呼叫 `FallToTrigger.Skip()` → 載入它的 `NextSceneName`）。

點畫面其他地方仍是「下一格」；只有點到右上角 Skip 區域才會跳關（用 `RectTransformUtility.RectangleContainsScreenPoint` 判定，不會誤觸發 advance）。字級在 `BuildSkip` 的 `fontSize`。

---

## 5. 首版排版（預設，全部可改）

三頁皆置中、兩側暗角；鏡頭依 `Focuses` 順序對焦。

| 頁 | 圖 | 鏡頭（Focuses，由上往下） |
|---|---|---|
| 1 | Page_01 | 1 上寬 → **2+3 綁一組** → 4 下寬 |
| 2 | Page_02 | 5 直幅左 → 6 右上 → 7 右中 → 8 右下 |
| 3 | Page_03 | 9 上 → **10+11 綁一組** → 12 下 |
| 下墜 | Story_13→14→15 | Fullscreen、每張 `HoldSeconds≈1` 自動換 → 接墜落動畫 |

> 各 `Focus.Area` 是憑頁面版型估的起點，實機用 `Zoom`/`XOffset`/`YOffset` 逐格微調。

---

## 6. 相關檔案

* `Assets/Scripts/Intro/IntroComicController.cs` — 漫畫播放器（Canvas、鏡頭平移/縮放對焦、換頁淡出、Skip、下墜 cover，全部在這）。
* `Assets/Scripts/Intro/IntroFallController.cs` — 接在後面的墜落動畫（見 [INTRO_FALL.md](INTRO_FALL.md)）。
* `Assets/Resources/InitialStory/Page_01`～`Page_03` — 三頁導讀成品圖（控制器讀這份）。
* `Assets/Resources/InitialStory/Story_13`～`Story_15` — 下墜全篇幅圖。

> ⚠️ **build 大小**：`Resources/InitialStory/` 的大圖會被烘進 `resources.assets.resS`，未壓縮會超過 GitHub 100MB 限制。請把這批圖的匯入設定壓縮（Max Size／Crunch），見 [PROBLEMS.md](PROBLEMS.md) A9。

---

## 7. 待辦 / 已知限制

- [ ] **各格 `Focus` 微調**：`Area`/`Zoom`/`Offset` 待逐格對到滿意。
- [ ] **回看 / 倒退**：目前只能往前。
- [ ] **音效**：換格、換頁、下墜的聲音待音訊系統（見 [TODO.md](TODO.md)）。
- [ ] **漫畫前的標題畫面 / 選角**：之後 Intro 場景可長成入口。

---

*建立於 2026-06-29：開場漫畫定案為「整頁成品圖 + 導讀式鏡頭」（鏡頭平移/縮放對焦每格、可綁格、下墜 Fullscreen cover、右上角兩段式 Skip、整頁圖放 Resources/InitialStory、右鍵帶入預設）。落點改 `MainScene` 的 Tutorial_Cave。*
