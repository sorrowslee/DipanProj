# 劇情演出編輯器（Cutscene Director）

> 返回 [文件總覽](README.md)
>
> **一句話**：在地圖編輯器裡**排一段「半演出半漫畫」的過場**——演員自己走位、說話、運鏡、插入漫畫格、淡黑、播螢幕特效，最後交棒到下一張圖或墜落動畫。全部資料驅動，存在 `.dipanmap` 裡，**不寫程式**。
>
> 程式：編輯器端 `Tools/CutsceneController.cs`＋`Data/CutsceneData.cs`＋`Core/CutsceneOverlay.cs`＋`Preview/CutscenePreview.cs`＋`UI/EditorUI.cs` 的「劇情」分頁；遊戲端 `Scripts/Cutscene/CutsceneDirector.cs`＋`CutsceneActor.cs`＋`Scripts/Map/CutsceneModel.cs`。（2026-07-20 加入）

---

## 0. 跟既有兩套過場的分工（很容易搞混）

| 系統 | 是什麼 | 文件 |
|---|---|---|
| **劇情演出（本篇）** | **地圖內**的演出：演員在這張圖上走位、對話、運鏡。資料在 `.dipanmap` | 本文件 |
| **序章漫畫／墜落** | 獨立 `Intro` 場景的**全程式**動畫，資料寫在程式裡 | [INTRO_COMIC.md](INTRO_COMIC.md)、[INTRO_FALL.md](INTRO_FALL.md) |
| **穿隧道→影片→換圖** | `cutscene` **trigger** 啟動的一次性鏈：走出拱門→播 mp4→換圖 | [CUTSCENE_TUNNEL.md](CUTSCENE_TUNNEL.md) |

> 名字撞了但是兩回事：`cutscene` **trigger**（穿隧道）與本篇的 `Cutscene` **演出**（`MapData.cutscene`）沒有關係。

跟 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) 的分工：**觸發鏈負責「事件流程」**（條件、旗標、給物品、開門），**劇情演出負責「有人在畫面上演戲」**。一段演出只有一條時間軸、沒有條件分支；要分支就用觸發鏈的 `requireFlag`，或在演出裡 `setFlag` 之後交給觸發鏈接手。

---

## 1. 資料結構

掛在 `MapData.cutscene`（可為 `null` ＝這張圖沒有演出）。**一張圖最多一段演出**。座標一律**世界座標**（同 `SceneFxInstance` 慣例）。編輯器端與遊戲端是**欄位一致的鏡像**（`CutsceneData.cs` ↔ `CutsceneModel.cs`），靠 Newtonsoft 直接序列化進 `.dipanmap`。

### Cutscene（整段）

| 欄位 | 預設 | 意義 |
|---|---|---|
| `autoStartOnEnter` | true | 一進圖自動開演 |
| `skippable` | true | 可按 **Esc** 略過（略過＝中止剩餘步驟，仍執行結尾交棒） |
| `lockInput` | true | 演出期間鎖玩家操作（走 `UIManager.SetExternalHold`） |
| `actors` | — | 演員清單 |
| `steps` | — | 步驟清單，**依序執行** |

### CutsceneActor（演員）

| 欄位 | 預設 | 意義 |
|---|---|---|
| `id` | — | 步驟以此引用 |
| `kind` | `"npc"` | `npc`＝新生一個物件；`player`＝**接管場上玩家**當傀儡（不另生物件） |
| `spriteFolder` | — | npc 外觀：走**路線 B 逐格動畫**的序列圖資料夾名（含 `idle`/`walk`，同怪物素材管線）。`player` 忽略此欄 |
| `x, y` | — | 起始世界座標（`player` 忽略，用玩家當前位置） |
| `facing` | `"down"` | 起始朝向。**角色只有左右兩向**（`flipX`），實際只有 `left`/`right` 有意義 |
| `spawnAtStart` | true | false ＝ 等 `spawn` 步驟才現身 |
| `flying` | false | **飛行**：關掉 A*／避障走直線，不受可走層限制（蝴蝶／鬼魂／飄浮物） |
| `scale` | 1 | 縮放 |
| `animFps` | 8 | 逐格動畫速度 |

> `player` 演員的實作：停用 `PlayerController`、臨時掛一顆 `MonsterActuator` 用**同一套 A***驅動，結束時 `Cleanup` 一定還原控制權。npc 演員自動掛 `YSortByFeet`，和地上物／玩家正確交錯。

### CutsceneStep（步驟）— 13 種型別

| type | 用到的欄位 | 行為 |
|---|---|---|
| `move` | `actorId` `x,y` `speed` `facing` | A* 走到目標，抵達才繼續；`speed` 格/秒（0＝預設 2）；`facing`＝抵達後朝向 |
| `face` | `actorId` `facing` | 轉向 |
| `dialogue` | `dramaId` | 播 `DramaTable` 的該筆（Type 1 大圖／Type 2 頭像對話都行），**面板關閉才繼續** |
| `wait` | `seconds` | 等待（unscaled） |
| `camera` | `x,y` `zoom` `seconds` | 鏡頭移到指定點、縮放；`seconds`＝停留 |
| `cameraFollow` | `seconds` | 鏡頭放回跟隨玩家 |
| `comic` | `assetId` `seconds` | 把 `Resources` 下的圖**置中**顯示（保持比例、約螢幕 90%×80%，不加黑底），期間演員暫停 |
| `fade` | `assetId` `seconds` | `out`（預設）＝淡出到全黑並**維持**；`in`＝從黑淡回並移除黑幕 |
| `spawn` | `actorId` | 現身（配合 `spawnAtStart=false`） |
| `despawn` | `actorId` | 消失（假傳送離場；建議走到畫面邊緣再 despawn） |
| `screenFx` | `assetId` `seconds` | 播 `ScreenFxTable` 的螢幕特效（1 睜眼／2 破幻術／3 馬賽克），見 [MAP_ENTER_EFFECT.md](MAP_ENTER_EFFECT.md) |
| `setFlag` | `flag` | `TriggerChain.SetFlag`，交棒給觸發鏈 |
| `end` | `assetId` | 結束並交棒，見 §3 |

### 兩個並行開關（差別很重要）

| 開關 | 行為 | 典型用法 |
|---|---|---|
| `parallelNext` | 與**下一步**同時開始，**整組做完才往下** | 走位＋運鏡同時 |
| `background` | 丟到背景跑，**主線立刻往下一步**；所有背景步驟在演出結束前才一起等收尾 | 一家人在後面慢慢走，前景照樣對話 |

---

## 2. 編輯器操作

頂部工具列的 **「劇情」** 分頁（`EditTool.Cutscene`）。右側面板：

1. 沒有演出時只有 **「＋ 建立演出」**。
2. 三個總開關（autoStart／skippable／lockInput）。
3. **演員區**：新增／選取／刪除；選中後編輯 id、種類、序列圖資料夾、起始朝向、`spawnAtStart`、`flying`、縮放、fps，按 **「放置起點（點畫布）」** 再點畫布定位。
4. **步驟區**：新增／上下移動排序／刪除；選中後按型別顯示對應欄位，`move`/`camera` 有 **「放置目標位置（點畫布）」**。
5. **▶ 預覽演出**（見下）。

**畫布疊層**（`CutsceneOverlay`，GL 繪製，同 `SceneFxOverlay` 範式）：演員起點方框（npc 青／player 金）＋朝向短線、有座標的步驟紫十字、每位演員的**走位折線**（起點→依序 `move` 目標）；選取者黃色加亮。**Esc** 取消放置模式。

### 編輯器內預覽（`CutscenePreview`）

按 **▶ 預覽演出** 直接在編輯器裡跑一次，用**移植版 A***（`NavGridPreview`）＋真實速度走位、播 idle/walk、轉向，走位路徑與落點盡量貼近遊戲。

**忠實重現**：`move` / `face` / `wait` / `spawn` / `despawn` / `comic` / `fade`
**佔位（只等秒數＋顯示提示文字）**：`dialogue`（「遊戲內播放」）、`screenFx`、`camera`、`cameraFollow`（預覽不動鏡頭）、`end`（不換圖）

Esc 略過、跑完自動清除。每次預覽會 `PreviewSpriteLoader.Clear()` 重載素材（本專案關掉了 Domain Reload，靜態快取會殘留，見 [PROBLEMS.md](PROBLEMS.md) I3/I5/I7）。

---

## 3. 結束交棒（`end` 的去向）

`CutsceneDirector` 在主迴圈結束後**回頭找最後一個 `end` 步驟**（被 Esc 略過時也照它走），依 `assetId` 分派：

| assetId | 行為 |
|---|---|
| 數字 或 `map:12` | `MapManager.GoToMap(mapId)` |
| `scene:名稱` | `SceneManager.LoadScene` |
| `fall` | **接墜落動畫**：常駐黑幕 `ScreenFader.BlackThenFadeOut` 壓黑 → 載 `Intro` 場景、`IntroComicController.FallTailOnly = true`（只播 Story_13~15 全螢幕頁）→ 側／正墜落 → 回 `MainScene` 起關到初始洞窟（`PostFallMapId` = 11，睜眼醒來） |
| 空 | 不交棒，演出結束就地收尾 |

**收尾規則**：有交棒 → npc 演員全部銷毀；沒交棒（同圖結束）→ npc **留在原地站著（idle）**，記進靜態 `_standing`，下次開演前清掉避免重進時堆疊。**玩家控制權一律還原**，鏡頭一律放回跟隨玩家。

---

## 4. 與其他系統的銜接

- **`MapManager` 在載圖完成後呼叫 `CutsceneDirector.MaybeAutoStart(map, player)`**，換圖會先收掉上一段演出。
- **進場觸發（`onEnter`）會等演出演完才點火**：`MapManager` 迴圈等 `CutsceneDirector.IsPlaying` 變 false，避免劇情對話與進場觸發對話互相蓋掉。演出中途交棒換圖則直接中止（新圖自有一輪）。
- **`fade` 的黑幕 sortingOrder = 90**，刻意壓在 `UILayer.Window`(100) 之下 → 畫面全黑但**對話框仍浮在黑幕上可見**（黑暗中的尖叫）。
- **`screenFx` 開始時會即時移除黑幕**：螢幕特效是相機後處理、在所有 UI 之下，黑幕會蓋住它。同一幀移除且特效已啟動，不會閃出清晰場景。

---

## 5. 目前用在哪

| 地圖 | ID | 用途 |
|---|---|---|
| `Main_InitialForest1` | 13 | 開場山道劇情（第一段，`IsLevelStart`） |
| `Main_InitialForest2` | 14 | 開場山道劇情（第二段，尾段 `end` 接 `fall`） |
| `Main_Cave` | 11 | 初始洞窟 |

這三張同屬 `Main` module，13/14 是 `SaveConstants.IsIntroCutsceneMap`（**不顯示血球 HUD**），初始洞窟 11 起才顯示。

---

## 6. 已知限制與待補

- **一張圖最多一段演出**（`MapData.cutscene` 是單一物件）。同圖要第二段目前只能靠觸發鏈另外湊。
- **沒有條件分支**：一條時間軸走到底。分支請用 `setFlag` 交給 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)。
- **只有左右兩向**：`facing` 的 `up`/`down` 存得進去但沒有效果（角色素材沒有上下向）。
- **`move` 的守門機制**：最長 20 秒、或「距離 < 1.0 且 0.6 秒沒進步」就視為抵達。A* 到不了精確點時會提早收，**別把目標點放在牆裡或不可走格**。
- **不能用 `autoStartOnEnter=false` 手動觸發**：目前沒有「用 trigger 啟動某段演出」的入口，關掉自動播就等於停用。
- **預覽不動鏡頭**：運鏡效果只能進遊戲看。
- **`comic` 的圖走 `Resources.Load`**（不是地圖素材管線），路徑要在某個 `Resources` 資料夾下、**不含副檔名**；找不到只印 Warning。
- **編輯器端 `CutsceneData.cs` 的型別註解漏列 `fade`／`background`**（實作有、註解沒跟上），以本文件的表為準。

---

*建立於 2026-07-27：補記 2026-07-20 commit `2f53ab7`「加入劇情編輯器」的完整設計。此系統落地時未寫文件，`CutsceneDirector` 程式註解已預留指向本篇（"見 readme（CUTSCENE_DIRECTOR）"）。*
