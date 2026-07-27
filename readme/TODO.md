# 待補清單 (TODO)

> 返回 [文件總覽](README.md)
>
> 「目前先做到能動、但還缺一塊」或「暫時做不到、之後再補」的項目集中記在這裡。
> 每補完一項就打勾或移除；新的暫缺項隨手往這裡加。各系統的完整規劃放對應主題文件，這裡只放「缺口」。

---

## 設定面板 (SettingsPanel) — 2026-06-24 建立

設定面板已可用（背景 + 兩條音量 slider 可拖曳 + 右上關閉鈕 + 底部離開遊戲鈕 + 離開確認彈窗）。
程式檔：`Assets/Scripts/UI/Panels/SettingsPanel.cs`、`ConfirmPopup.cs`、`Assets/Scripts/UI/SettingsLauncher.cs`。
**測試開啟方式：按 `O` 開/關設定面板**（Esc 或右上角 X 也能關）。

尚缺 / 待補：

- [ ] **音量沒接上實際音訊**：音樂 / 音效 slider 目前只做到「可拖曳」，值先存記憶體（`SettingsPanel` 的靜態欄位 `_musicVol` / `_sfxVol`）。**專案還沒有音訊系統**——之後要先建 AudioManager / AudioMixer，再把這兩個值接到實際音量（音樂群組、音效群組）。
- [ ] **音量沒持久化**：目前關遊戲就回到預設。之後接 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) 的 `settings.json`（全域設定、不綁角色），開場讀入、改動寫回。
- [x] ~~離開確認彈窗 placeholder~~ → **已換成正式美術**（2026-06-24）：`ConfirmPopup` 用 `PopupPanelBG.png` 背板 + LongBtn 當 OK/No 底 + 勾(`PopupPanelOkIcon`)/叉(`PopupPanelNoIcon`) icon。離開鈕也補上門 icon（`SettingPanelExitIcon`）。座標為量測值，實機若偏移微調 `ConfirmPopup.cs` / `SettingsPanel.cs` 上方常數。
- [ ] **開啟入口**：目前按 **ESC** 開設定（沒有其他視窗時；由 UIManager 的「ESC 根面板」機制處理，設定開著時 ESC / 右上 X 關閉），另保留 `O` 備用鍵。之後若要做「暫停選單 / 主選單」按鈕，直接呼叫 `UIManager.Instance.Open<SettingsPanel>()` 即可，`SettingsLauncher` 可再精簡。
- [ ] **座標為量測值**：slider 把手、關閉鈕、離開鈕的位置是照背景圖量的常數（`SettingsPanel.cs` 上方）。實機若有偏移，微調那幾個常數即可（同 InventoryPanel / StoragePanel 的做法）。

---

## 頭像對話 (TalkPanel) — 2026-06-24

劇情「Type 2＝頭像對話」資料層 + 觸發 + 對話介面已可用（DramaTable `Type`/`TalkGroup`、`DramaTalkTable.csv`、`DramaTalkData`/`Database`/`Provider`/`Controller`、`TalkPanel`，見 [DRAMA.md](DRAMA.md)）。

- [x] ~~對話介面 UI~~ → **已做**（2026-06-24）：`TalkPanel`（對話框 `DramaPanelBG` + 姓名牌匾 `DramaPanelNameBG` 依 Side 擺左/右 + 姓名 + 文字 + 點擊/空白鍵換頁，播完關閉）。`DramaTalkController.Play` 改為開啟它。
- [x] ~~頭像載入~~ → **已接**（2026-06-24）：頭像走地圖素材管線（放 `GameAssets/Modules/<module>/Talk/`、`AvatarPath`=catalog id、跑 Sync Map Assets）。`DramaTalkDatabase.ResolveAvatars` 載成 Sprite，TalkPanel 以站姿擺在說話人側。`Talk` 已加進三處同步白名單（見 [PROBLEMS.md](PROBLEMS.md) C3）。
- [ ] **座標 / 立繪大小微調**：TalkPanel 的對話框、文字、姓名牌匾、立繪高度/擺位都是量測值（`TalkPanel.cs` 上方常數）。實機看立繪大小或位置不合再調（`AvatarHeight`、`AvatarSideMargin` 等）。可能也想做「立繪面向內側翻轉」「換情緒立繪（如 `redBridalGown_angry`）由哪欄指定」。

---

## 畫面 / 效能設定（玩家面向）— 來自 [DISPLAY_SETTINGS.md](DISPLAY_SETTINGS.md)

上架前要補的玩家畫面設定（VSync / 幀率上限 / 視窗模式 / 解析度），完整規劃見 [DISPLAY_SETTINGS.md](DISPLAY_SETTINGS.md) 第三、四節。可與上面的設定面板合併成同一個「設定」介面。

- [ ] `GraphicsSettings` 啟動套用器（依設定套 VSync / 幀率上限 / 視窗模式 / 解析度）。
- [ ] 把畫面設定欄位接進 `settings.json`（同音量，一起持久化）。
- [ ] 畫面設定 UI（VSync 開關、幀率上限、視窗模式、解析度），可直接加進設定面板。
- [ ] 出廠預設拍板（建議 VSync 開；若改 VSync 關必附幀率上限，避免 2D 畫面空轉）。

---

## 畫質：UI 去壓縮 + 場景濾波 — 2026-07-01（已定案，已合回 `develop`）

大螢幕「UI 糊、場景粗糙」的處理，詳見 [PROBLEMS.md](PROBLEMS.md) G2 / G3。

- [x] `Resources/UI` 全部 39 張貼圖 Compression 改 None（去糊）。
- [x] `Assets/Editor/UITextureImportSettings.cs`：新 UI 圖自動套不壓縮預設。
- [x] `MapSpriteLoader.SceneFilterMode` 場景濾波可切換，PerfHud（P）加「場景濾波(F)」按鈕/F 鍵即時切。
- [x] **場景方向定案：採 `FilterMode.Point`（硬像素）為預設**；Bilinear 保留作比較。
- [ ] （選配，之後有需要再做）想更清晰時照 [AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md) 把場景源圖重產得更細緻（顆粒更小）。
- [ ] 回家實體螢幕最終確認 UI 清爽度（遠端桌面看不準，會重壓縮串流）。

---

## 標題／存讀檔／進度流程 — 2026-07-03 建立（見 [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md)、[SAVE_SYSTEM.md](SAVE_SYSTEM.md) §14）

存讀檔進度層（周目＝generation、完成關卡＝clearedModules、金錢、出生點旗標）＋標題／三欄存讀檔 UI＋`GameFlowManager` 總流程（新建/繼續/覆蓋/刪除、進廣場自動存、in-place 輪迴 API）**程式已完成、佔位視覺**，以下為缺口與待接線。

**Unity 端待接（見 [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md) §4）**
- [ ] `Main_Square`（Map 12）在地圖編輯器放兩個具名落點：`caveExit`（下方洞穴出口）、`center`（廣場中央），不填 targetMapId。沒放會退回地圖中心，不會壞。
- [ ] 要新建就播開場鏈 → 把 `Intro.unity` 加進 Build Settings（`MainScene` 保持 scene 0）。沒加則新建直接進廣場。
- [ ] 每個關卡放一個「達成目標」trigger（擊殺 boss／完成囑託／收集道具…），觸發時呼叫 `SaveManager.Instance.MarkModuleCleared("該關 module 名")` 讓完成關卡數 +1（重複進入同關不重算）。

**輪迴 / 邪佛戰（資料層已就緒，玩法流程待做）**
- [ ] 玩家輸給邪佛後「挑選要帶哪幾件」的選物 UI（可帶數＝`min(周目,7)`，`ReincarnateInPlace(carryIds)` 已備）。
- [ ] 觸發輪迴的接線（邪佛戰結果 → 選物 → `ReincarnateInPlace` → 回廣場）。
- [ ] 邪佛戰（第 8 關）＋最終關卡（第 9 關）的流程與關卡本身。
- [ ] 進度「階段」判定（可進關卡／完成 7 關後邪佛要求對決／破最終關後終局刷寶），由 `ClearedModuleCount` + `LevelsToUnlockBoss` + boss/final 旗標推導。
- [ ] **待確認**：輪迴時倉庫（storages）是否也要一起重置？目前 `ReincarnateInPlace` **只清背包、倉庫不動**。

**UI 收尾**
- [ ] 正式建名輸入框（目前新建用預設名「存檔N」；`UIBuilder.InputField` 已可用）。
- [ ] 「遊戲中回標題」鈕放進 `SettingsPanel`（依討論，之後再放）。
- [ ] `TitlePanel` / `SaveSlotPanel` 佔位視覺換正式素材（標題圖、卡片框、按鈕；主標《燃燈劫》、副標 Burning Lamp: Rebirth of Ruin）。

---

*建立於 2026-06-24：設定面板（音量 slider 先可拖曳、未接音訊；離開確認彈窗為暫時 placeholder；開啟用測試鍵 O）。*
*2026-07-03 追加：標題／存讀檔／進度流程的缺口與 Unity 待接線。*

---

## 紅嫁衣「沒殺家人→榕樹妖」劇情分支 — 2026-07-05 規劃

觸發鏈系統已完備（[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)），邪佛大廳鏈已實裝；紅嫁衣分支尚未實作。

- [ ] **怪物死亡寫旗標掛勾**（唯一要寫程式的部分）：MonsterData.csv 加 `DeathFlag` 欄＋`MonsterController.Die()` 呼叫 `TriggerChain.SetFlag`，家人五怪（8~12）填 `killedFamily`。
- [ ] DramaTable/DramaTalkTable 新增紅嫁衣對話（Group 3、DramaTable ID 4）。
- [ ] 編輯器在紅嫁衣最終房放 `紅嫁衣對話`(drama, requireFlag=`!killedFamily`, next=`送去榕樹妖`) + `送去榕樹妖`(teleportTo, targetMapId=10)。
- [ ] 「殺了家人」的另一條分支內容未定義（作者決定）。

**完整接手步驟見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §7**（含檔案位置、行號、測試清單）。

---

## 主角攻擊動畫「沒顯示」— 待除錯（2026-07-09）

攻擊動畫已接線（`attack/` 資料夾 + 按住開火播 cast，見 [CHARACTER_SETUP.md](CHARACTER_SETUP.md)、PROGRESS 同日），但實機**攻擊時沒看到攻擊動作**。程式端已就緒，最可能是素材同步/載入沒到位。回家接手時**依序**檢查：

- [ ] **先跑 `Project Tools → Sync Map Assets`（第一嫌疑）**：25 張 cast 幀目前只在 `GameAssets/Main/Characters/SequenceImage/Base/attack/`，遊戲是從 `StreamingAssets/MapAssets/Main/Characters/SequenceImage/Base/attack/` + catalog 載入的，**StreamingAssets 端還沒有 attack**（已確認）。沒同步 → `PlayerSpriteLibrary.GetFrames(血統,"attack")` 回 null → `Has(Attack)=false` → `HandleVisuals` 自動退回 Walk/Idle（所以攻擊時只會走路/發呆，看起來「沒有攻擊動作」）。
- [ ] 同步後確認 `StreamingAssets/MapAssets/Main/Characters/SequenceImage/Base/attack/` 有 25 張圖，且 catalog（`StreamingAssets/MapAssets/…catalog…json`）有 `Base/attack` 這筆（frameCount=25）。
- [ ] 進 Play 看 Console 有無 `[PlayerAnimator] 血統「Base」找不到…` 警告；若仍抓不到 attack，多半是 catalog 沒收到該葉資料夾（檢查 Sync 工具的角色素材白名單有沒有含 `Characters/SequenceImage` 的子資料夾遞迴）。
- [ ] 關了 Domain Reload 時，若同步後第一次沒更新，重進 Play（`PlayModeStaticReset` 會重置 `PlayerSpriteLibrary` 單例重載）。

**確認能顯示後可再調（都在程式常數，非 bug）：**
- [ ] 攻擊播放速度：目前沿用 `PlayerAnimFPS`（與 idle/walk 共用）。要獨立速度 → 給 `PlayerAnimator` 加 per-state 的 attack fps。
- [ ] 觸發方式：目前是「**按住開火鍵**」就播（吃輸入，沒魔力/冷卻中也會擺姿勢）。若要改「**真的射出去那一下**才播」，改成在實際發射點（Shoot/UpdateLaser/UpdateAura）觸發 + 一次性播完。
- [ ] 殘留時間 `AttackAnimLinger`（PlayerController 常數，現 0.12s）。
- [ ] 大小一致性：attack 幀顯示縮放沿用 idle 可見高度換算；cast 幀主體高度與 idle 差太多會忽大忽小。

---

## 選擇劇本面板：系統自動發牌 3新+1舊 — 2026-07-15 規劃（見 [SELECT_SCRIPT.md](SELECT_SCRIPT.md)）

初始版（單卡紅嫁衣、`selectScript` 鏈動作、素材依 module 自動載）**程式已完成**。從第 2 輪起「邪佛隨機發 3 新 + 1 舊、roll 一次就固定並存檔、內容由系統算不讓編輯器配置」尚未做——**卡在兩個前置**（要先有其他關卡＋在存檔加「完整通關」判定）。完整設計、roll 規則、實作步驟見 [SELECT_SCRIPT.md](SELECT_SCRIPT.md) §4。

**前置缺口（沒有就做不了）**
- [ ] **完整通關判定（最關鍵）**：`ProgressDTO` 目前只有 `clearedModules` 布林，**沒有「100% 完整通關 vs 通關沒挖乾淨」**——3新+1舊 的第 4 格（業障回響舊關）靠它。待拍板：布林 vs 完成度百分比。
- [ ] **可派發關卡目錄（level pool）**：roll 新關需要「所有可派發關卡」清單（module＋劇本道具 id）。目前只有紅嫁衣，要等多做幾關。
- [ ] **roll 結果存檔**：`ProgressDTO` 加 `currentOffer`（本次發牌清單＋哪張 special）＋「本次已領取」旗標；roll 一次寫存檔、重開同一次不變。schema 版本 +1。

**實作（前置到位後）**
- [ ] 發牌服務 `SelectScriptOffer`（`GetOrRollOffer`/`ConsumeOffer`/`ClearOfferAfterClear`；初始情境回紅嫁衣）。
- [ ] `selectScript` 改**無參數**（拿掉 `scriptIds`/`specialIds`，編輯器 `TriggerType.cs` 同步拿掉）；動作改呼叫發牌服務。
- [ ] 過關流程（`GameFlowManager`/`clearLevel`）接 `ClearOfferAfterClear`；領取回呼接 `ConsumeOffer`。
- [ ] 「完整通關」寫入點（挖乾淨判定，來源＝該關隱藏 boss/分支/綁定獎勵旗標）。

**待拍板決策**：完成度布林 vs 百分比；roll 時機（過關回廣場當下 vs 首次開介面當下）；業障回響權重/保底 v1 要不要做；level pool 來源（劇本道具自動推 vs 另開小表）。

---

## 文件缺口：作弊面板（測試工具）— 2026-07-27 記

`CheatPanel` + `CheatLauncher`（2026-07-23，commit `11a9ed7`）已可用但**沒有專屬文件**。按 **L** 開/關，左側分頁＋右側內容區，目前只有「給道具」分頁（填 ID＋數量 → 直接進真背包，不走臨時包）；開啟時暫停＋擋輸入。程式註解內已寫「如何新增一個作弊分頁」。

- [ ] 要不要開一份 `CHEAT_PANEL.md`？（目前判斷：**功能單一＋程式註解已足**，等分頁多起來再開）
- [ ] **測試/除錯按鍵沒有集中清單**，散在各文件與程式裡。目前已知：**L** 作弊面板、**F8** 臨時包除錯疊層、**P** PerfHud（**F** 場景濾波切換、**V** VSync、**T** 目標 FPS）、**O** 設定面板（備用鍵，正式入口是 ESC）、**空白/左鍵** 穿隧道前進、**Esc** 略過劇情演出。建議在 README 或本檔開一小節統一列管。
- [ ] 上架前確認作弊面板要不要以編譯符號（如 `DEVELOPMENT_BUILD`）隔離，避免正式版被玩家按到 L。

---

## 文件缺口：劇情演出編輯器 — 2026-07-27 已補

- [x] ~~`Cutscene` 系統（2026-07-20）完全沒有文件~~ → 已補 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md)。
- [ ] 一張圖只能有一段演出、沒有條件分支、不能用 trigger 手動啟動——這三項是目前的設計上限，之後若開場以外的關卡要用劇情演出，可能得補「trigger 啟動指定演出」的入口。
- [ ] 編輯器端 `CutsceneData.cs` 的型別註解漏列 `fade` 與 `background`（實作有、註解沒跟上），順手時補一下。

---

## CsvUtil：已備好但尚未啟用 — 2026-07-27

`Assets/Scripts/Data/CsvUtil.cs` 已建立（`SplitLine()` 支援雙引號包逗號、`Field/FieldInt/FieldFloat/FieldBool` 防呆取值），但**目前零呼叫**——既有 13 處 CSV 解析刻意一行未改，避免大規模重構的風險。

- [ ] **什麼時候該遷移**：遇到「某張表的某一欄需要能打半形逗號」時，把那**一張表**換成 `CsvUtil.SplitLine()` 即可，不必全面換。目前已知受限的是 `MonsterData` 的「句子1~4」（怪物台詞，見 [MONSTER_SPEECH.md](MONSTER_SPEECH.md)）與 `DramaTalkTable` 的 `Text`。
- [ ] **遷移時要注意**：換過去之後，該 CSV 若原本有**裸露的雙引號**會被當成引號語法解析，要一併檢查既有資料。
- [ ] 已支援引號的兩處（`ItemDatabase`、`Language`）各有一份自己的實作，之後順手時可改成呼叫 `CsvUtil`（純整理，不急）。

---

## 跳過劇情：`VideoPlayerOverlay` 還沒鎖 — 2026-07-27

2026-07-27 已把「ESC 跳過劇情」限制成開發階段專用（`DevSkip.Allowed`，套在 `CutsceneDirector` / `TalkPanel` / `DramaPanel`），開場漫畫與墜落動畫本來就有各自的 `AllowSkip`。**唯一還沒區分開發／正式的跳過入口是過場影片**。

- [ ] **`VideoPlayerOverlay.AllowSkip` 是 Inspector 上的 public bool**（預設 true），打包後玩家可按 ESC 或左鍵跳過過場影片。要不要一併鎖成 `DevSkip.Allowed` 由作者決定——影片跳過的性質和劇情不太一樣（重複看同一段影片很煩，多數遊戲允許跳）。要鎖的話改一行即可。
- [ ] 若之後新增任何「可跳過」的表演，記得套 `DevSkip.Allowed`（見 `Assets/Scripts/DevSkip.cs`）。

---

## 死碼：開場漫畫流程的殘骸 — 2026-07-27 記

開場改走劇情編輯器、`Page_01~03` 也刪掉之後，`GameFlowManager` 裡還留著舊「新建遊戲播漫畫」路線的殘骸。

- [ ] **`GameFlowManager.NewGameIntroRoutine()`（約第 115 行）沒有任何呼叫端**。`StartNewGame` 現在走的是 `NewGameToForestRoutine()`（直接進山道劇情 13）。留著會讓後人以為還有「新建遊戲播漫畫」這條路。確認無誤後可刪。
- [ ] 順帶確認 `Intro` 場景裡 `IntroComicController` 的 `Pages` 清單——前三頁（`Page_01~03`）指向的圖已刪除，雖然 `FallTailOnly` 會把它們濾掉、實際不會載入，但清單留著已失效的項目容易誤導。要清的話在 Intro 場景的 Inspector 刪掉那三筆即可。
