# 標題畫面 ＋ 三欄存讀檔 UI ＋ 遊戲總流程

> 返回 [文件總覽](README.md)
>
> 底層存檔見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)（含 §14 進度層）；UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)。
>
> **狀態：✅ 程式完成（2026-07-03），待 Unity 實機驗證與換素材。** 佔位視覺（純色底＋內建字型＋純色鈕），之後換正式標題圖／卡片／按鈕素材即可。

把「一 Play 就跳進關卡」改成 **標題畫面 → 三欄存讀檔畫面 → 玩家選擇** 的正式進入流程。全程式建構、零手動接線，仿 `UIManager` / `SaveManager` / `MapManager`。

> **遊戲正式名稱**：中文《**燃燈劫**》／英文 **Burning Lamp: Rebirth of Ruin**。`TitlePanel` 目前用這組名稱當佔位主標／副標（之後換標題圖時沿用）。

---

## 1. 玩家流程

```
開遊戲
  └─ 標題畫面（TitlePanel）：標題 ＋ 中間偏右佛陀動畫 ＋「開始遊戲」
        └─（按開始 → 播一次佛陀動畫，播完才切換）
        └─ 三欄存讀檔（SaveSlotPanel）：欄位 1 / 2 / 3
              ├─ 空欄 → 新建遊戲 ──┐
              ├─ 有檔 → 繼續        │
              ├─ 有檔 → 覆蓋（先問）─┤（＝在該欄重新開始）
              └─ 有檔 → 刪除（先問，測試用）
                                    │
        新建 ──────────────────────┘
          ├─ 有開場場景（Intro 在 Build Settings）→ 播開場鏈 → …… → 邪佛廣場（洞穴出口出生）
          └─ 沒有開場場景 → 直接進邪佛廣場（洞穴出口出生）
        繼續 → 載入該欄存檔 → 直接進邪佛廣場（中央出生）
```

- **一欄 = 一條獨立進度線 = 一個角色**（`slotIndex` 對應）。三欄的周目與完成關卡可以完全不同。
- **周目（大進度）= `generation`**、**完成關卡數（小進度）= `clearedModules` 去重數**（見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) §14）。卡片只讀 `profiles.json` 摘要（`GetSlotProfile`），不載完整檔。
- **進邪佛廣場（Map 12）= 自動存檔點**：踏進廣場就存一次，並把出生點旗標 `hubIntroSpawnDone` 設為 true。

---

## 2. 新增／改動的檔案

| 檔案 | 內容 |
|---|---|
| `Assets/Scripts/Flow/GameFlowManager.cs` | **總流程指揮**（＋`GameFlowBootstrap` 開機自動生）：開機顯示標題；新建/繼續/覆蓋/刪除；載場景＋等 MapManager 就緒→進廣場。**新建有開場時走 `NewGameIntroRoutine`：先用 `ScreenFader` 蓋黑 → 關選單 → 載 Intro → 淡出**，避免關選單時露出後面的標題面板（「標題閃一下才進漫畫」）。 |
| `Assets/Scripts/Flow/ScreenFader.cs` | 全螢幕黑幕淡入/淡出（跨場景常駐、`sortingOrder` 30000 蓋在所有 UI 上、用 unscaledTime 因過場可能暫停）。`ScreenFader.Ensure().FadeTo(alpha,dur)`。用於切場景時先蓋黑再切，遮掉選單淡出/場景切換的破綻。<br>**2026-08-18 另加閃光層**：`ScreenFader.Flash(color, in, out)` / `ClearFlash()`——全螢幕閃一下（打雷擊中、爆炸這種瞬間衝擊）。是**與黑幕分開**的獨立 Image + CanvasGroup + 協程：閃光要能在黑幕淡出到一半時插進來，共用會互相打斷。`raycastTarget` 與 `blocksRaycasts` 恆 false，**永遠不擋點擊**（黑幕蓋著時是要擋的，兩者需求相反）。重複呼叫取代上一次。 |
| `Assets/Scripts/UI/Panels/TitlePanel.cs` | 標題畫面（Window 層、全螢幕佔位）：標題圖（`Resources/UI/TitlePanel/TitlePanel_TW`，3:1）＋中間偏右佛陀動畫（`Resources/UI/TitlePanel/BuddhaTitle/BuddhaTitle_01..NN`，自動偵測幀數、按開始才播一次、用 unscaledTime 因暫停中）＋開始遊戲鈕（圖 `Resources/UI/Common/StartGameBtn`＋程式補字）→**動畫播完再多停 `BuddhaEndHold` 秒才**開存讀檔畫面（無圖則退回文字/直接開）。播放中鎖按鈕防重複、回標題自動重置回第一幀。位置/大小常數在檔案上方（`BuddhaFps`/`BuddhaOffset`/`BuddhaDisplaySize`/`TextGroupX`/`TitleWidth`/`TitleY`/`StartBtnWidth`/`StartBtnY`）。 |
| `Assets/Scripts/UI/TitleFireFx.cs` | **標題火焰特效**（UI 端、用 unscaledTime 因面板暫停）：① 全螢幕持續落火（火星/帶尾火條飄落閃爍，`UiFallingEmber`）② 標題燃燒（背後脈動火光＋沿標題往上竄、柔邊高斯羽化＋寬度抖動的火舌，`UiRisingFlame`）。**不是**用 MapsTable 火雨（那是世界端 SpriteRenderer、綁相機/deltaTime，暫停中不動且會被 UI 蓋掉）——改在 Canvas 上重做、複用火雨的程序生成佔位圖 `SceneEffectSprites`。由 `TitlePanel` 以 `EnableFireFx` 開關、`Init(emberRoot,titleRect,titleGlow)` 啟動。密度/大小/速度常數在 `TitleFireFx` 上方（`EmbersPerSecond`/`FlamesPerSecond`/`EmberFall*`/`TitleGlowAlpha` 等）。（曾試做佛陀兩肩蒸騰濃煙〔照搬 `SceneFxEmitter` fbm 煙塊〕，2026-07-03 決定不放、已移除。） |
| `Assets/Scripts/UI/Panels/SaveSlotPanel.cs` | 三欄存讀檔畫面。**2026-08-01 換上正式素材**：滿版底圖＋三張卡片框；空欄＝「空欄位」＋新建遊戲，有檔＝角色 idle 圖／圓台／周目／裝備中的武器 icon ＋ 進入遊戲／刪除角色（ConfirmPopup 再問一次）。見 §4.5。 |
| `Assets/Scripts/Save/SaveManager.cs` | **改**：`SuppressAutoLoad`；槽位 API（`GetSlotProfile`/`SlotOccupied`/`StartNewGameInSlot`/`LoadSlot`/`DeleteSlot`）；`ReincarnateInPlace(carryIds)`（in-place 輪迴）；`CreateCharacter` 帶 slotIndex。 |
| `Assets/Scripts/Save/SaveConstants.cs` | **改**：`SlotCount=3`、`HubEntranceCaveExit/Center`、`IntroSceneName`、`MainSceneName`。 |
| `Assets/Scripts/Save/ProfileRoster.cs` / `CharacterSave.cs` | **改**：加 `slotIndex`。 |
| `Assets/Scripts/Map/MapManager.cs` | **改**：`SuppressAutoStart`；進 `HubMapId` 時依旗標覆寫落點（首次洞穴出口→之後中央）＋自動存。 |

> 面板放 **Window 層**（不是 Overlay）：UI Canvas 本就在遊戲世界相機之上，整片不透明底即可蓋住背後場景；且放 Window 層，覆蓋/刪除用的 `ConfirmPopup`（Popup 層）才會顯示在面板之上。

---

## 3. 開機行為（重要）

`GameFlowBootstrap`（`BeforeSceneLoad`）在任何場景 `Start` 前把兩個靜態旗標設 true：

- `SaveManager.SuppressAutoLoad = true` → 不再一進場自動載/建 `test001`，改由存讀檔畫面決定。
- `MapManager.SuppressAutoStart = true` → 不再自動進 `startModule`，改由流程 `GoToMap`/開場鏈驅動。

> **抑制是「開機當下」的，之後由各流程分支自己設定**：
> - 新建＋播開場鏈 → 先把 `SuppressAutoStart` 設回 **false**，交還給既有開場流程（Intro→MainScene 後 MapManager 照舊自動進 `Main`＝Main_Cave 11，再過場到廣場）。**若這裡沒設回 false，墜落動畫後 MainScene 會一片黑（沒有任何關卡被載入）。**
> - 繼續 / 無開場直接進廣場 → 維持 **true**，由流程明確 `GoToMap(廣場)`，避免和自動進 Main_Cave 打架。

**要暫時回到舊的「直接進關卡」測試流程**：在任何 `BeforeSceneLoad` 前把 `GameFlowManager.TitleFlowEnabled = false` 即可（bootstrap 會整個略過，兩個旗標維持 false）。

### 測試快捷：直接進某關卡（DevQuickStart，Editor-only）

反覆測單一關卡（例如紅嫁衣）不必每次從標題→讀檔→廣場→開傳送門。用選單 **`Project Tools/測試/直接進關卡`** 一鍵切換：

- **紅嫁衣 (RedBridalGown)** / **初始洞窟 (Main_Cave)**：按 Play 直接載入該 module 首張地圖（跳過標題流程；有勾＝目前選的）。
- **邪佛廣場 (Main_Square)**：直接進廣場（map 12，是 Main 模組的**非首圖**，所以用「地圖 id」進、不是用 module）。
- **關閉（走正式標題流程）**：恢復正式流程。

原理：`Assets/Editor/DevQuickStart.cs`（Editor-only、不進 build）在 `AfterAssembliesLoaded`（早於 `GameFlowBootstrap`）把 `TitleFlowEnabled` 關掉，再用 `MapManager.DevStartModuleOverride`（進 module 首圖）或 `MapManager.DevStartMapId`（直接進某地圖，如廣場 12）覆寫開機目標——**不動場景序列化的 `MapManager.startModule`（＝Main）**，所以關掉後正式開場鏈照舊。狀態存 EditorPrefs（只影響本機編輯器）。要加別的關卡／地圖就在 `DevQuickStart.cs` 複製一個選單項、填 module 名或地圖 id。

---

## 4. Unity 端要做的接線（照做）

1. **Build Settings 場景**
   - `MainScene` 保持在清單第一個（scene 0）。
   - **想要新建遊戲播開場鏈** → 把 `Assets/Scenes/Intro.unity` 加進 Build Settings（順序不拘）。加了之後，新建遊戲會自動播開場鏈；沒加則新建遊戲直接進邪佛廣場（方便你現在就測）。
   - 場景名要和 `SaveConstants.IntroSceneName`（`"Intro"`）、`MainSceneName`（`"MainScene"`）一致；改名就改常數。

2. **邪佛廣場（Main_Square，Map 12）兩個具名落點**
   - 在地圖編輯器替 Main_Square 放兩個具名 entrance：`caveExit`（下方洞穴出口）、`center`（廣場中央），名稱要與 `SaveConstants.HubEntranceCaveExit/Center` 一致。
   - 若暫時沒放：`MapManager.ResolveSpawnPos` 會退回該圖的 `playerSpawn`→地圖中心，不會壞，只是出生點不精準。

3. **其他不用接**：`GameFlowManager` / `SaveManager` / `UIManager` 都自動生成。

4. **測試種子物品**：`InventoryLauncher.seedTestItems` 若維持 true，新建遊戲的空角色進 MainScene 時仍會被塞 12 武器＋雜物。要驗「乾淨新局」就把它關掉（或它本來只在背包空時塞一次）。

---

## 4.5 正式素材版面（2026-08-01）

工程版的純色佔位卡片已換成美術素材。**版面座標全部寫在「底圖原生像素空間」（1672×941），整個 frame 等比放大到蓋滿畫面**——與 `InventoryPanel` / `ForgingPanel` / `GachaPanel` 同一套作法；透明留白的補償沿用 `ArtSpec` + `PlaceArt()`（重出圖時畫布比例變了會出警告，不會靜默跑位，見 [PROBLEMS.md](PROBLEMS.md) D12）。

**素材**（`Assets/Resources/`）

| 圖 | 畫布 | 內容邊界框 | 用途 |
|---|---|---|---|
| `UI/SelectSavePanel/SelectSavePanel_Bg` | 1672×941 | 整張 | 滿版背景（佛殿）＝版面座標系本身 |
| `UI/SelectSavePanel/SelectSavePanel_Frame` | 692×886 | 24,10,639,862 | 一張卡片的外框。**頂端「欄位」紅底牌與背後的圓形佛像浮雕都畫在這張圖裡**，程式只疊字 |
| `UI/SelectSavePanel/SelectSavePanel_ActorBase` | 612×408 | 112,156,385,135 | 角色腳下的圓台 |
| `UI/Common/SelectSavePanel_Btn` | 914×273 | 19,35,865,209 | 按鈕底板（只有一張圖、沒有按下版 → 用 `ColorTint` 做回饋，同 `CloseBtn_2`） |

**卡片內容**

- **空欄**：中央「空欄位」＋一顆置中的「新建遊戲」。
- **有檔**：左半＝圓台 → 角色；右半＝「一周目」＋武器 icon；下方兩顆並排的「進入遊戲」「刪除角色」。
  （曾試過在角色後面鋪一張方形底板 `SelectSavePanel_ActorBg`，實機看起來跟卡片框自帶的圓形浮雕打架、視覺上是歪的，**2026-08-01 決定拿掉**；角色直接站在浮雕前面。）
- **周目用中文數字**（`CjkNumber()`：1→一、23→二十三，>99 退回阿拉伯數字）。
- 中日韓字串會自動逐字加空格（`Spaced()`，符合本作 UI 排版習慣）；含英數的字串不套，所以英文版不會被拆散。

**角色圖怎麼來**

1. 讀該欄存檔的周目旗標 `血統` → [`BloodlineTable`](GACHA_SYSTEM.md) 的 `SpriteFolder`；沒喝過血統藥劑就是 `Base`。
2. `PlayerSpriteLibrary.Instance.GetFrames(<血統>, "idle", 1f)[0]` ＝ 該血統 idle 的第一幀（走地圖素材管線，不佔 Resources）。
3. 用 `TryGetVisibleBox()` 的**不透明像素邊界框**正規化：不同血統的圖留白不一樣，直接用整張圖會忽大忽小、腳也踩不準圓台。程式讓「可見內容」剛好 `ActorH` 高、底部對齊 `ActorFeetDy`。
4. 該血統沒有 idle 圖 → 印 Warning 並退回 `Base`；連 `Base` 都載不到 → 只畫圓台、不畫角色（版面不會塌）。

素材是 `idle_right`（角色朝右）。要讓角色面向另一邊，改 `ActorFlipX = true` 即可（偏移量會跟著鏡射）。

**武器 icon** 取該欄存檔 `inventory.equipment["Weapon"]` 的道具 id → `ItemTable` 的 `IconPath`（＝背包裡看到的那張 icon）。空手就不畫。

> ⚠ 這個畫面**不會載入存檔到遊戲裡**。為了拿外型與武器，它用 `SaveSystem.LoadCharacter()` 直接從磁碟**偷看一眼**該角色的 `character.json`，不動 `SaveManager.Current`、不觸發 `ApplyToSystems`。真正的載入仍然是玩家按「進入遊戲」時走 `GameFlowManager.ContinueGame(slot)`。

**移除的東西**：舊版的「覆蓋（新建）」與畫面底部的「返回」鈕都拿掉了（照示意圖）。要重開一欄就先「刪除角色」再「新建遊戲」；返回標題按 **ESC**。卡片上也不再顯示「完成 N 關」與「上次遊玩時間」（`CharacterProfile` 仍有這兩筆資料，要顯示隨時可加回來）。

**字串**走 `Language.GetText`，`LanguageTable.csv` 的 **5001–5099「選擇存檔」段**。表還沒就緒時會退回硬寫的中文（不會變成一排 `[cn:5001]`）。

**版面常數**都在 `SaveSlotPanel.cs` 上方，實機若有偏移微調即可：卡片 `CardCy`/`CardPitch`/`CardW`/`CardH`；卡內元件一律寫成「相對卡片中心的偏移」（`HeadDy`/`EmptyDy`/`ActorBaseDx,Dy,W`/`ActorDx,ActorFeetDy,ActorH`/`CycleDx,Dy`/`WeaponDx,Dy,H`/`BtnDy`/`BtnSingleW`/`BtnPairW,BtnPairDx`），所以三張卡片共用同一組數字。

---

## 5. 實機驗證步驟

1. 開 Unity 等編譯，Console 無紅錯（新程式都在預設 `Assembly-CSharp`）。
2. 進 Play：應出現 **標題畫面**（不再直接進關卡）。按「開始遊戲」→ 三欄存讀檔畫面。
3. **新建**：點空欄「新建遊戲」→（沒加 Intro）直接進邪佛廣場，玩家在洞穴出口；Console 有 `已存檔`（進廳自動存）。回標題再看該欄 → 卡片變成「角色圖＋一周目＋武器 icon」。
4. **繼續**：點該欄「進入遊戲」→ 進邪佛廣場，這次玩家在**中央**（`hubIntroSpawnDone` 已 true）。
5. **角色圖**：卡片上的角色＝該存檔血統的 `idle` 第一幀，腳要踩在圓台上；喝過血統藥劑的存檔應該換成該血統的外型。
6. **武器 icon**：把武器裝上／卸下再回標題，卡片右邊的 icon 要跟著出現／消失。
7. **刪除**：按「刪除角色」→ ConfirmPopup 問 → 確定 → 卡片變空欄。
8. **三欄獨立**：三欄各自新建、各自不同進度，互不影響。

---

## 6. 輪迴（資料層已完成，選物 UI 待後）

`SaveManager.ReincarnateInPlace(IList<int> carryItemIds)`：在**同一欄位**把角色重開一輪——

- `generation`(周目) +1；`stats` / `progress` 全部歸零；`hubIntroSpawnDone` 重置（回「第一次進廣場」）。
- 只帶入 `carryItemIds` 指定的物品，數量自動夾到 **`min(周目, 7)`**（第 N 次輪迴帶 N 件，上限 7）；0/負值會被略過。
- **倉庫（storages）不動**（暫定；之後若要一起重置再改）。
- 重置後由 `GameFlowManager` 帶玩家回廣場（洞穴出口）。

**待後續**（等邪佛戰／抽關卡系統成形）：玩家輸給邪佛後的「挑選要帶哪幾件」選物 UI、觸發輪迴的接線、以及進度階段判定（可進關卡／邪佛要求對決／已破最終關）。

---

## 7. 這一輪未做 / 待補

- 「遊戲中回標題」鈕（要放進既有 [設定面板](UI_SYSTEM.md) `SettingsPanel`）——依先前討論，之後再放。
- 正式建名輸入框（目前新建用預設名「存檔N」；`UIBuilder.InputField` 已可用）。卡片上也還沒有地方顯示角色名。
- ~~正式素材（卡片框、按鈕）替換佔位視覺~~ → **已完成（2026-08-01，見 §4.5）**；標題畫面 `TitlePanel` 的素材另計。
- 輪迴選物 UI 與邪佛戰／最終關卡的流程接線（見 §6）。

---

*建立於 2026-07-03：標題／三欄存讀檔 UI ＋ GameFlowManager 總流程（新建/繼續/覆蓋/刪除、進廣場自動存、出生點旗標）＋ SaveManager 槽位與 in-place 輪迴 API。佔位視覺，待 Unity 實機驗證與換素材。*

*2026-08-01：存讀檔畫面換上正式素材（滿版底圖＋卡片框＋角色 idle 圖／圓台／方形底板＋武器 icon＋周目中文數字），移除「覆蓋」與「返回」鈕，刪除角色改走 ConfirmPopup 二次確認，字串進 `LanguageTable.csv` 5001–5099 段。見 §4.5。*

---

## 「繼續遊戲」回到上次的位置（2026-08-01，schema v3）

**原本的問題**：`GameFlowManager.ContinueGame` 的落點是**寫死**的 `GoToMap(HubMapId, "center")`，完全不看存檔進度。
而按「新建遊戲」的當下 `CreateCharacter()` 就已經把角色寫進磁碟了，所以「新建 → 看到開場第一句對話 → 關掉 → 重開」
會直接被丟到邪佛廣場，**開場山道劇情、墜落、初始洞窟的睜眼醒來三段全部被跳過**。
存檔裡跟「走到哪」有關的原本只有 `hubIntroSpawnDone` 一個布林，而且只用來決定廣場的落點。

**作法**：`ProgressDTO` 新增 `lastMapId` / `lastEntrance`，記「上次待在哪」。

- **只記 `HubModule`（Main）的地圖**：開場山道 13/14、初始洞窟 11、邪佛廣場 12。由 `MapManager.PlaceAndSetup`
  在 `row.module == SaveConstants.HubModule` 時呼叫 `SaveManager.RecordLastLocation(row.id, entrance)`。
- **關卡（其他 module）刻意不記**。關卡是 extraction 模型（`RunProgress` 純記憶體、通關才落袋、死亡/離開歸零），
  記了會讓重開遊戲回到一個東西都不見的關卡裡。所以這兩欄的值永遠是「最後一次待在 Main 的位置」，
  在關卡中離開＝回到進關卡前的廣場，正好符合設計。
- 這幾張圖一輪只經過一次，所以記完直接 `SaveNow()` 落地（不靠 dirty 自動存），
  否則玩家在山道劇情中途直接關掉視窗就可能沒存到。
- `ContinueGame` 改讀 `LastMapId`/`LastEntrance`；**`mapId <= 0`（v2 以前的舊存檔）→ 退回廣場中央**，行為與改動前一致。
  `GoToHubRoutine` 泛化成 `GoToMapRoutine(mapId, entrance)`，回廳的路徑不變。
- schema **v2 → v3**：純新增欄位，Newtonsoft 對缺欄給型別預設（0 / null），`Migrate()` 不需要新增搬資料的程式碼。

**行為**：山道播到一半離開 → 回山道**重播那一段**（cutscene 是 `autoStartOnEnter`，本來就會重播）；
洞窟離開 → 回洞窟（`EnterEffect=1` 的睜眼醒來會再播一次，這是刻意不特別處理的）；
廣場或關卡中離開 → 回廣場；輪迴後 `progress` 整個換掉 → 兩欄歸零 → 回廣場。

**動到的檔**：`Save/CharacterSave.cs`（ProgressDTO 兩個新欄）、`Save/SaveConstants.cs`（版本號 3）、
`Save/SaveManager.cs`（`LastMapId`/`LastEntrance`/`RecordLastLocation`）、`Map/MapManager.cs`（PlaceAndSetup 記錄）、
`Flow/GameFlowManager.cs`（`ContinueGame` + `GoToMapRoutine`）。
