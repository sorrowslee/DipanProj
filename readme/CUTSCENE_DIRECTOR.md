# 劇情演出編輯器（Cutscene Director）

> 返回 [文件總覽](README.md)
>
> **一句話**：在地圖編輯器裡**排一段「半演出半漫畫」的過場**——演員自己走位、說話、運鏡、插入漫畫格、淡黑、播螢幕特效，最後交棒到下一張圖或墜落動畫。全部資料驅動，存在 `.dipanmap` 裡，**不寫程式**。
>
> 程式：編輯器端 `Tools/CutsceneController.cs`＋`Data/CutsceneData.cs`＋`Core/CutsceneOverlay.cs`＋`Preview/CutscenePreview.cs`＋`UI/EditorUI.cs` 的「劇情」分頁；遊戲端 `Scripts/Cutscene/CutsceneDirector.cs`＋`CutsceneActor.cs`＋`PlayerVisibility.cs`＋`Scripts/Map/CutsceneModel.cs`＋`Scripts/MapFx/MemoryFxController.cs`。（2026-07-20 加入；2026-08-22 加回憶特效／頭上對話／trigger 啟動／隱藏主角）

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

掛在 `MapData.cutscenes`（**清單**）。座標一律**世界座標**（同 `SceneFxInstance` 慣例）。編輯器端與遊戲端是**欄位一致的鏡像**（`CutsceneData.cs` ↔ `CutsceneModel.cs`），靠 Newtonsoft 直接序列化進 `.dipanmap`。

> **⚠ 2026-08-22 起格式從單一物件改成清單**（舊欄位 `cutscene` 保留讀取相容）：讀檔時 `MapData.NormalizeCutscenes()` 把舊的單一 `cutscene` 搬進 `cutscenes`，之後永遠只寫清單。三個讀檔點都已接上（遊戲端 `MapIO.MapSerializer.Load`、編輯器 `MapSerializer.Load` 與 `MapSession.RestoreFromJson`＝Undo 還原）。
> **但編輯器目前仍然只編輯／使用第 0 段**（`MapData.MainCutscene`）——先把檔案格式做好，之後真的要「一張圖多段劇情」時不必再動 `.dipanmap` 格式、既有地圖也不用轉檔。

### Cutscene（整段）

| 欄位 | 預設 | 意義 |
|---|---|---|
| `id` | `""` | 演出識別名。給觸發鏈 `playCutscene` 的 `cutsceneId` 指定用；單段時留空即可 |
| `autoStartOnEnter` | true | 一進圖自動開演。搭配下面兩個旗標欄就能做「只播一次」；要接進事件流程再用 `playCutscene`（見 §2.5） |
| `requireFlag` | `""` | **播放條件旗標**：成立才播（前綴 `!`＝「沒有這個旗標才播」）。留空＝不檢查。自動播與 `playCutscene` 都吃這個條件 |
| `setFlag` | `""` | **完成寫旗標**：演完（含被 ESC 略過）把這個旗標設成立。留空＝不寫 |
| `skippable` | true | **可略過**：畫面右上角出現全遊戲統一樣式的 **Skip**（見 [UI_SYSTEM.md](UI_SYSTEM.md)），**ESC 同效**。⚠️ **2026-08-22 起這是玩家可見的正式功能**，不再是開發階段限定——**唯一例外是序章整段**（初始森林 13/14）：正式版全程不顯示 Skip、開發階段照跳，初始洞窟(11) 起才回到一般規則（`DevSkip.SkipAllowedHere`）。<br>⚠️ **略過＝快轉到結局，不是停在原地**——中止剩餘步驟後**仍會執行結尾的 `end` 交棒與 `setFlag`**，所以 `end='fall'` 的段落按下去會直接接墜落動畫。這是刻意的（跳過表演、流程照走），歷史說明見 [PROBLEMS.md](PROBLEMS.md) **J2**。<br>播對話時按 Skip 會**連同對話面板一起關掉**（否則「跳過」看起來像沒反應） |
| `lockInput` | true | 演出期間鎖玩家操作（走 `UIManager.SetExternalHold`） |
| `memoryFx` | false | **回憶特效**：整段演出套「泛黃老照片＋柔邊暈影」全螢幕後處理，演完（含被略過、中途換圖）自動移除。見 §6 |
| `hidePlayer` | false | **演出期間隱藏主角**：關掉外型＋影子＋碰撞＋身上光源，收尾自動現身並回到開演前的位置。見 §7 |
| `hideHud` | false | **演出期間關閉底部血量 HUD**（演在畫面下方時會被血球擋住），收尾恢復成開演前的樣子。⚠ 是**每幀維持**關閉，不是關一次——見 [PROBLEMS.md](PROBLEMS.md) **D21** |
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

### CutsceneStep（步驟）— 14 種型別

| type | 用到的欄位 | 行為 |
|---|---|---|
| `move` | `actorId` `x,y` `speed` `facing` | A* 走到目標，抵達才繼續；`speed` 格/秒（0＝預設 2）；`facing`＝抵達後朝向 |
| `face` | `actorId` `facing` | 轉向 |
| `dialogue` | `dramaId` | 播 `DramaTable` 的該筆（Type 1 大圖／Type 2 頭像對話都行），**面板關閉才繼續** |
| `bubble` | `actorId` `langId` `seconds` | **頭上對話框**：不跳對話視窗，直接在該演員頭上冒一個水墨泡泡（沿用怪物說話那套面板與美術）。文字＝`LanguageTable.csv` 的 id；`seconds` 顯示秒數（0＝2）。預設擋住這段時間，要「邊走邊講」就勾 `background`。見 §8 |
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

### 1.5 只播一次：條件旗標 ＋ 完成寫旗標

**自動播本身沒有一次性機制**——`MaybeAutoStart` 不吃 `repeat`，所以光是勾自動播，每次進這張圖都會重播。標準做法是一組旗標：

| 欄位 | 填什麼 |
|---|---|
| 條件旗標 | 某旗標的**「沒有」**（存成 `!旗標名`） |
| 完成寫旗標 | **同一個旗標** |

於是「沒播過 → 播 → 寫旗標 → 之後條件不成立 → 不再播」。**播幾次由那個旗標的生命週期決定**（在旗標管理器上設，同一個旗標改一次全部跟著改）：

| 旗標生命週期 | 效果 |
|---|---|
| **關卡單次** | 每次重新進這個關卡（module）都會再播一次；同 module 房間互跳不會重播 |
| **周目** | 這一周目只播一次，輪迴後重置 |
| **永久** | 這隻角色一輩子只播一次（跨輪迴保存） |

> **紅嫁衣那個需求**（每次進關卡只播一次、之後回這間房間不再播）＝ 生命週期選 **關卡單次**。

- 機制與觸發點的 `requireFlag`／`setFlag` **完全同一套**：走同一支 `TriggerChain.FlagTrue` / `SetFlag`，地圖只存旗標裸名、生命週期由全域 `flags.json` 決定（方案乙單一來源，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §2.5）。
- 編輯器操作也一樣：**輸入旗標 id → 按「確認」**，系統去登記表撈名稱並鎖定顯示（如 `redBridalIntroPlayed（關卡單次）`）；沒按確認或查無 id 就不會有名稱出現＝沒配置成功。條件旗標旁另有「有／沒有」切換。
- **`setFlag` 被 ESC 略過時也會寫**——略過本來就會照走 `end` 交棒，語意一致（已經看過了）。
- 寫完旗標會順手 `MapManager.RefreshTriggers()`，讓本圖依賴這個旗標的互動點立刻現身/消失。
- 條件不成立時只印一行 Log、不播，**不影響其他任何東西**；由 `playCutscene` 啟動時會回 false，鏈照常接 next。

---

## 2. 編輯器操作

頂部工具列的 **「劇情」** 分頁（`EditTool.Cutscene`）。右側面板：

1. 沒有演出時只有 **「＋ 建立演出」**。
2. **總開關**：autoStart ＋ **條件旗標／完成寫旗標**（見 §1.5，輸入 id → 按確認）／skippable／lockInput
   ＋ **回憶特效**／**隱藏主角**／**關閉血量 HUD** ＋ 演出 id（選填）。
   條件與寫入填同一個旗標且條件是「沒有」時，面板會顯示「✔ 只播一次」；填反了（條件是「有」）會警告「第一次永遠不會播」。
3. **演員區**：新增／選取／刪除；選中後編輯 id、種類、序列圖資料夾、起始朝向、`spawnAtStart`、`flying`、縮放、fps，按 **「放置起點（點畫布）」** 再點畫布定位。
4. **步驟區**：新增／上下移動排序／刪除；選中後按型別顯示對應欄位，`move`/`camera` 有 **「放置目標位置（點畫布）」**。
5. **▶ 預覽演出**（見下）。

**畫布疊層**（`CutsceneOverlay`，GL 繪製，同 `SceneFxOverlay` 範式）：演員起點方框（npc 青／player 金）＋朝向短線、有座標的步驟紫十字、每位演員的**走位折線**（起點→依序 `move` 目標）；選取者黃色加亮。**Esc** 取消放置模式。

### 2.5 用 trigger 啟動（把劇情接進事件流程）

**§1.5 的旗標**已經能解決「播幾次」。這一節解決另一件事：**「什麼時候播」**——不是一進圖，而是走到某個位置、打贏 boss、拿到某個東西之後。

**做法**：把該段的「一進圖自動播」**關掉**，改在地圖上放一顆 **`playCutscene` 播放劇情(鏈動作)** trigger（動作型，不用玩家踩、格子畫在角落即可），由觸發鏈啟動它。除了 §1.5 的旗標之外，觸發鏈自己的守門條件也全部可用——`重複規則`（關卡單次·每次·每周目·永久）／`周目上下限`／`完成關卡數`／`條件不成立時`。詳見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3。

> 兩者可以疊：trigger 的條件管「這個事件流程走不走得到」，Cutscene 自己的 `requireFlag` 管「這段戲該不該再演一次」。只做「進圖只播一次」用 §1.5 就夠，不必為它放 trigger。

| 欄位 | 意義 |
|---|---|
| `cutsceneId` | 要播哪一段（對應 Cutscene 的 `id`）。**留空＝第一段**；目前一張圖只有一段，所以通常留空 |

- **典型接法**：走進房間的 `camZone` → `next` → `playCutscene`（重複規則＝每周目）→ `next` → `togglePortal` 開門。
- **演完才接 next**（同 `cameraFocus`／`playScreenFx` 的慣例）。
- ⚠ **該段結尾若有 `end` 交棒**（換圖／接墜落），鏈就此結束、**不會接 next**（同 `teleportTo` 慣例）。
- 開不成時（找不到那段／沒步驟／已有演出在跑）會印 Warning 並**直接接 next**，不讓鏈卡死。

### 編輯器內預覽（`CutscenePreview`）

按 **▶ 預覽演出** 直接在編輯器裡跑一次，用**移植版 A***（`NavGridPreview`）＋真實速度走位、播 idle/walk、轉向，走位路徑與落點盡量貼近遊戲。

**忠實重現**：`move` / `face` / `wait` / `spawn` / `despawn` / `comic` / `fade`
**佔位（只等秒數＋顯示提示文字）**：`dialogue`（「遊戲內播放」）、**`bubble`**（文字在遊戲端的 `LanguageTable`，編輯器沒有那張表）、`screenFx`、`camera`、`cameraFollow`（預覽不動鏡頭）、`end`（不換圖）。回憶特效與隱藏主角預覽也不套用。

Esc 略過、跑完自動清除。每次預覽會 `PreviewSpriteLoader.Clear()` 重載素材（本專案關掉了 Domain Reload，靜態快取會殘留，見 [PROBLEMS.md](PROBLEMS.md) I3/I5/I7）。

---

## 3. 結束交棒（`end` 的去向）

`CutsceneDirector` 在主迴圈結束後**回頭找最後一個 `end` 步驟**（被 Esc 略過時也照它走），依 `assetId` 分派：

> ⚠️ **「略過也照走 end」是最容易誤判的行為**：在 `Main_InitialForest2`（`end='fall'`）按一下 ESC，會直接中止整段劇情 → 交棒墜落動畫 → 回 `MainScene` 進初始洞窟(11) → 洞窟的 `EnterEffect=1` 觸發「趴地→爬起」。看起來像「按 ESC 就會莫名其妙播爬起動畫」，其實是一路交棒過去的正常結果。2026-07-27 起 ESC 略過已限制為開發階段專用。

| assetId | 行為 |
|---|---|
| 數字 或 `map:12` | `MapManager.GoToMap(mapId)` |
| `scene:名稱` | `SceneManager.LoadScene` |
| `fall` | **接墜落動畫**：常駐黑幕 `ScreenFader.BlackThenFadeOut` 壓黑 → 載 `Intro` 場景、`IntroComicController.FallTailOnly = true`（只播 Story_13~15 全螢幕頁）→ 側／正墜落 → 回 `MainScene` 起關到初始洞窟（`PostFallMapId` = 11，睜眼醒來） |
| 空 | 不交棒，演出結束就地收尾 |

**收尾規則**：有交棒 → npc 演員全部銷毀；沒交棒（同圖結束）→ npc **留在原地站著（idle）**，記進靜態 `_standing`，下次開演前清掉避免重進時堆疊。**玩家控制權一律還原**，鏡頭一律放回跟隨玩家。

---

## 4. 與其他系統的銜接

- **演出期間傳送點／過場點一律不觸發**（`TeleportWatcher` / `CutsceneWatcher` 查 `CutsceneDirector.IsPlaying`，並持續解除武裝）。
  ⚠ 這條是必要的，不是保險：**進圖落點常常就站在「回去的那顆傳送點」上面**（`targetEntrance` 指的就是對面那顆傳送點的錨點），
  而自動播的劇情會把玩家釘在那裡十幾秒；`hidePlayer` 收尾又會用程式把玩家搬回原位。
  少了這道，症狀是「劇情剛播完，人就被送回上一張圖」。見 [PROBLEMS.md](PROBLEMS.md) **B14**。

- **`MapManager` 在載圖完成後呼叫 `CutsceneDirector.MaybeAutoStart(map, player)`**，換圖會先收掉上一段演出。
- **有場景名的圖，自動播延到名字播完才開演**（2026-08-27）：`MapManager.PlaceAndSetup` 判定這一趟要跳 `SceneTip` 時不呼叫 `MaybeAutoStart`，改由進場等待鏈在名字整段播完後呼叫。所以玩家看到的是**正常畫面 → 場景名 → 劇情模式整套出現**（藏主角、演員、Skip、黑邊都在名字之後）。沒名字的圖仍同幀開演。`playCutscene` 中途啟動的不受影響。細節見 [SCENE_TIP.md](SCENE_TIP.md) §3。
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

## 6. 回憶特效（`memoryFx`）

整段演出期間套一層「**泛黃老照片 ＋ 柔邊暈影 ＋ 上下黑邊**」，並**暫時淡掉整套場景氛圍**。勾一個 checkbox 就好，不用排任何步驟。

- 程式：`Scripts/MapFx/MemoryFxController.cs` ＋ `Resources/Shaders/MemoryFx.shader`。開演 `Begin()` 淡入 0.6 秒、收尾 `Cleanup()` 一定 `End()` 淡出（被 ESC 略過、中途換圖也保證關掉）。
- **要調濃淡改 `MemoryFxController` 上方的常數**（`Sepia`／`Desat`／`VigStart`／`VigPower`／`BlurPx`／`Grain`／`Tint`／`Letterbox`／`SuspendAtmosphere`），不要在 shader 裡硬加。⚠ Linear 色彩空間疊色比直覺重一倍（[PROBLEMS.md](PROBLEMS.md) **E11**），每次調完要進遊戲看實機。

### 三層各自在做什麼

| 層 | 內容 | 為什麼需要 |
|---|---|---|
| **淡掉場景氛圍**（`SuspendAtmosphere`，預設開） | `AtmosphereController.SetBypass(0→1)`，把整套氛圍與原始畫面內插 | **這是回憶在暗地圖上看不看得見的關鍵**，見下方警告 |
| **泛黃＋暈影＋顆粒**（柔邊已關） | 去飽和後往暖褐偏／邊緣壓暗中央微亮／極輕靜態顆粒。**邊緣模糊 `BlurPx` 自 2026-08-27 設 0**：npc 走到畫面邊邊整個人會被糊掉（見 PROBLEMS **J5**）；shader 的 13-tap 圓盤模糊留著，要開回來填 4~6 | 老照片的本體 |
| **上下黑邊**（`Letterbox`，各 11% 高） | 後處理直接畫兩條黑邊，隨強度滑入滑出 | **與場景明暗完全無關**，全黑的地圖上也一眼看得出「進入過場」 |

> ### ⚠ 為什麼一定要淡掉場景氛圍（踩過的坑）
> 第一版做出來作者回報「在紅嫁衣關卡看不太清楚」。原因不是強度不夠，**是方向錯了**：紅嫁衣全 10 張圖的 `Atmosphere` 都是 **2（幽暗＋打光）**，除了玩家提燈那一圈以外壓到接近全黑；而泛黃是「顏色乘暖褐」、暈影是「邊緣再壓暗」、柔邊是「模糊」——**乘法與壓暗作用在黑色上等於什麼都沒做**，整套效果在那種圖上自動失效。
> 所以回憶期間先把氛圍淡掉讓場景亮回來，色調層才有東西可以染。語意上也對：**回憶不是「現在這個黑房間」，不該有提燈的黑暗感。**
> ⚠ 副作用是**天氣（雨雪風霧）與提燈光圈也會一起淡掉**，這是刻意的。要保留場景原本的暗，把 `SuspendAtmosphere` 改成 false（但在幽暗地圖上就等於看不見回憶）。
> 通則寫在 [PROBLEMS.md](PROBLEMS.md) **J4**。

> ### ⚠ 為什麼不做成 `ScreenFxTable` 的一個 id
> `ScreenFxPlayer` 那家族（睜眼／破幻術／馬賽克）全是**一次性、播 N 秒就結束**，而且會**暫停遊戲＋藏 HUD**。回憶要的是「整段演出一直掛著、期間玩家與演員照常動」，語意屬於 `AtmosphereController` 那種**常駐 blit**，所以獨立成一支。見 [PROBLEMS.md](PROBLEMS.md) **J3**。

**幾個必然的限制**（都是「相機後處理」這個身分決定的，不是 bug）：

- **UI 不會被染色，黑邊也蓋不住 UI**：後處理只作用在相機畫面，`ScreenSpaceOverlay` 的 UI 在其後合成。所以 `comic` 置中漫畫、對話框、HUD 都維持原色；如果演出下方會被血球擋住，用 §1 的 `hideHud` 勾選關掉它。
- **`fade` 黑幕會蓋住它**：黑幕是 UI（sortingOrder 90），淡到全黑時當然什麼後處理都看不到。
- **合成順序**：`AtmosphereBlit` 先算（此時已被 bypass 淡掉）、`MemoryFxBlit` 疊在其上。

---

## 7. 演出期間隱藏主角（`hidePlayer`）

「這段戲裡沒有主角」時勾它：開演即隱藏，收尾自動現身**並回到開演前的位置**。程式在 `Scripts/Cutscene/PlayerVisibility.cs`。

> ### ⚠ 絕對不能用 `player.SetActive(false)`
> 踩過三個坑，`PlayerVisibility` 就是為了一次擋掉：
> 1. **影子會留在原地**——`BlobShadow` 的影子是**獨立 GameObject、不是子物件**（刻意不做子物件，避免被角色翻轉/縮放二次影響）。停用玩家連 `BlobShadow.LateUpdate` 也停了，影子會**定格在原地不會消失**。所以加了 `BlobShadow.SetVisible(bool)` 另外關。
> 2. **暗場景的光圈會留在原地**——`AtmosphereController` 每幀以玩家為心算裝備的發光半徑，玩家隱形了光圈照樣亮著，空地上浮一圈沒有主人的光。所以 `BuildLights` 會查 `PlayerVisibility.IsHidden` 跳過玩家光源。
> 3. **碰撞還在擋路**——劇情演員的 A\* 走位會被一個看不見的玩家撞開。
>
> 正解是**逐項關掉**：`SpriteRenderer`（含子物件）＋ `BlobShadow` ＋ `Collider2D`（含子物件），再由 `IsHidden` 讓氛圍光源跳過。詳見 [PROBLEMS.md](PROBLEMS.md) **E19**。

- **狀態一定要能還原**：`PlayerVisibility.IsHidden` 是 static，有 `ResetForPlayMode()`（註冊在 `PlayModeStaticReset`）＋ `CutsceneDirector.OnDestroy` 的安全網。少了任何一道，「在演出中途停止 Play」或「演出被換圖打斷」都會讓主角**永久隱形**。詳見 [PROBLEMS.md](PROBLEMS.md) **D22**。
- **和 `player` 演員（主角傀儡）互斥**：兩個一起勾會印 Warning 並**以隱藏為準**——主角被藏起來就走不動也看不見，兩者只該選一個。
- **位置還原的例外**：該段結尾**真的交棒換圖**時不還原位置（新圖會自己安排落點，硬拉回舊座標反而錯）。
  ⚠ 判斷的是「`end` 有沒有填去向」，不是「有沒有 `end` 步驟」——`end` 去向留空＝就地收尾、不換圖，那種情況照樣要放回原位。（2026-08-22 修：原本用「有沒有 end 步驟」判斷，`end` 留空的同圖演出會變成主角不回原位、`playCutscene` 的鏈也莫名斷掉。）

---

## 8. 頭上對話框（`bubble` 步驟）

不跳對話視窗，直接在演員頭上冒一個水墨泡泡說一句話——**沿用怪物說話那一整套**（`MonsterSpeechPanel`，兩張底板隨機輪流、避邊翻面、底板鏡像、文字 best-fit）。見 [MONSTER_SPEECH.md](MONSTER_SPEECH.md)。

| 欄位 | 意義 |
|---|---|
| `actorId` | 誰說話（npc 演員或 `player` 主角傀儡都行） |
| `langId` | 台詞＝`Assets/Data/LanguageTable.csv` 的 id。**玩家可見字串一律走語言表**（AGENTS.md 鐵則），找不到會顯示 `[lang:id]` 佔位 |
| `seconds` | 顯示秒數，0＝2 秒 |
| `background` | 勾了＝**邊走邊講**（主線立刻往下一步）；不勾＝擋住這段時間 |

- **`MonsterSpeechPanel` 已泛化**：內部不再綁 `MonsterController`，改成「一個 `Transform` 目標 ＋ 一個『還在不在』的判斷委派」。`Speak(MonsterController,…)` 保留成薄包裝，**怪物那端零改動**。
- 頭頂/腳下座標的三段優先序：**玩家** → `PlayerController` 的可見身體幾何（`FeetWorldPos`/`VisibleBodyHeight`，[PROBLEMS.md](PROBLEMS.md) **E14**）；**有 Collider2D** → 碰撞框上下緣（怪物走這條，行為完全不變）；**只有 SpriteRenderer** → `bounds`（劇情 npc 演員沒有 Collider2D，一定要有這段）。
- 面板不擋輸入、不暫停，`UILayer.HUD`；開場山道（13/14）雖然關掉了血球 HUD，但那只關 `BottomHudPanel`、不關整層，所以氣泡照樣看得到。
- **`dialogue` 與 `bubble` 怎麼選**：要立繪、要玩家按鍵推進、要一長串對話 → `dialogue`（DramaTable）。一兩句短台詞、不想打斷畫面、想讓兩個角色邊走邊聊 → `bubble`。

---

## 9. 已知限制與待補

- **一張圖仍只用第一段演出**：`.dipanmap` 格式已經是清單（`cutscenes`），但編輯器面板與 `playCutscene` 目前都只操作第 0 段。要真正做「一張圖多段、各自綁旗標」還要補：編輯器的演出列表 UI、`CutsceneController` 的多段選取、預覽的段落切換。
- **沒有條件分支**：一條時間軸走到底。分支請用 `setFlag` 交給 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)。
- **只有左右兩向**：`facing` 的 `up`/`down` 存得進去但沒有效果（角色素材沒有上下向）。
- **`move` 的守門機制**：最長 20 秒、或「距離 < 1.0 且 0.6 秒沒進步」就視為抵達。A* 到不了精確點時會提早收，**別把目標點放在牆裡或不可走格**。
- ~~不能用 `autoStartOnEnter=false` 手動觸發~~ → **2026-08-22 已解**：關掉自動播後用 `playCutscene` 觸發鏈動作啟動，見 §2.5。
- **預覽不動鏡頭**：運鏡效果只能進遊戲看。
- **`comic` 的圖走 `Resources.Load`**（不是地圖素材管線），路徑要在某個 `Resources` 資料夾下、**不含副檔名**；找不到只印 Warning。
- ~~編輯器端 `CutsceneData.cs` 的型別註解漏列 `fade`／`background`~~ → **2026-08-22 已補齊**（順手加 `bubble`），兩端註解與本文件的表一致。
- **回憶特效染不到 UI**：置中漫畫、對話框、HUD 都不會泛黃（相機後處理碰不到 Overlay UI），見 §6。
- **`bubble` 的文字不會自動換行控制**：靠面板的 best-fit 縮字（40→22），一句太長會縮得很小。長台詞請用 `dialogue`。

---

*建立於 2026-07-27：補記 2026-07-20 commit `2f53ab7`「加入劇情編輯器」的完整設計。此系統落地時未寫文件，`CutsceneDirector` 程式註解已預留指向本篇（"見 readme（CUTSCENE_DIRECTOR）"）。*

*2026-08-22 擴充：① 回憶特效 `memoryFx`（§6）；② 頭上對話框 `bubble` 步驟（§8，`MonsterSpeechPanel` 泛化）；③ `playCutscene` 觸發鏈動作，劇情終於能用旗標管重複（§2.5）；④ 演出期間隱藏主角 `hidePlayer`（§7）；⑤ `.dipanmap` 的 cutscene 改存清單，為「一張圖多段劇情」預留格式。*
