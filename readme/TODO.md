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

`CheatPanel` + `CheatLauncher`（2026-07-23，commit `11a9ed7`）已可用但**沒有專屬文件**。按 **L** 開/關，左側分頁＋右側內容區；開啟時暫停＋擋輸入。程式註解內已寫「如何新增一個作弊分頁」。目前兩個分頁：

- **給道具**：填 ID＋數量 → 直接進真背包（不走臨時包）；一鍵快捷有「獲得 10,000 元」與「**取得所有武器（每種一把）**」（2026-08-07 加）。
- **鑲嵌**：改裝備中武器的孔數、每種能力珠各給一顆 Lv3、給測試防具 501/502。

> 「取得所有武器」是從**物品表**列舉 `EquipSlot = Weapon` 的物品，不是從武器表。目前 `WeaponTable.csv` 有 **20** 把武器，其中 **id 14「紅嫁衣召喚家人」是 Boss 專用、沒有對應的玩家物品**，所以按下去會拿到 **19** 把。之後若要讓某把怪物武器也能被玩家拿到，就在 `ItemTable.csv` 補一列（`EquipSlot=Weapon`＋對應的 `WeaponID`）即可，作弊鈕會自動涵蓋、不用改程式。

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

---

## 祭壇抽選系統：缺口清單 — 2026-07-28（見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md)）

整套流程已可玩，以下是「先做到能動、內容或素材還沒到位」的部分。

**資料 / 內容**

- [ ] **裝備池是空的**（`BaseArmorRoll.csv` 沒有任何一列）——因為專案目前**根本還沒有任何防具道具**。等有防具了再填。目前走到裝備祭壇按抽選會出「這個祭壇還沒有可抽的東西」。
- [ ] **道具池要不要留**：`GachaPoolTable.csv` 有 `item` 這一列，但地圖上還沒放道具祭壇。要嘛去廣場擺一座，要嘛把那一列刪掉（刪掉不影響其他池）。
- [ ] **血統的 `SpriteFolder` 全部填 `Base`**：`BloodlineTable.csv` 三個血統（人類/野魂/幽靈）都指向唯一存在的那組角色序列圖，所以**現在喝血統藥劑外型不會真的變**（屬性有變）。等各血統的 idle/walk/dead 序列圖產出來，把 `SpriteFolder` 改成各自的資料夾即可，程式不用動。
- [ ] **血統的 `SkillId` 全部留空**：作者說過血統「甚至可能新增技能」。欄位已備好，但目前沒有技能系統可接。
- [ ] **血統藥劑 301/302 用的是暫代 icon**，要正式的藥瓶圖。

**素材**

- [ ] **牌子只有 `rockSlate_weapon.png` 與 `rockSlate_armor.png` 兩張**——血統祭壇、（如果要做的話）道具祭壇還沒有對應的石牌美術，現在只能先借用或不放。`GachaPoolTable.csv` 的 `SlateSprite` 欄已預留。
- [ ] **抽選面板素材的透明留白偏多**（AI 整張畫布輸出，見 [PROBLEMS.md](PROBLEMS.md) E9）。目前用程式端的 `ArtSpec` 量測表補償、不影響顯示，但每張圖都比實際內容大不少，會多吃一點記憶體與載入。之後有空可以把素材裁到內容框再同步更新 `ArtSpec`（**改一邊沒改另一邊會跑位**，所以要一起改；載入時的尺寸不符警告會提醒）。

**程式 / 行為**

- [ ] **`openPanel` 的 `next` 目前不會接**：面板關閉時沒有回頭通知 `TriggerChain`（不像 drama 有 `NotifyDramaClosed`）。所以「抽完 → 接一段對話」這種編排現在做不到。要做的話比照 drama 加一個關閉通知即可。
- [ ] **抽選面板的字串還沒進語言表**：「抽選」「單抽」「十連」「金錢不足」等都是程式裡的中文字面值，還沒走 `Language.GetText(id)`（見 [LOCALIZATION.md](LOCALIZATION.md)）。做多語系時要一起收。
- [ ] **十連結算面板一次最多好看地放 10 筆**（`n<=5` 一列、否則兩列）。如果之後有「二十連」之類的，版面要重排。
- [ ] **抽選沒有保底 / 沒有稀有度**：目前純權重隨機（`Weight` 欄），連抽十次全是同一把也可能發生。要保底或稀有度分層的話再加欄位。

**作者手動要補的（編輯器裡填）**

- [ ] 三座祭壇的 **`panelId` 要重點一次**——編輯器的循環按鈕舊 bug（見 [PROBLEMS.md](PROBLEMS.md) C9）讓它們實際存的是空字串。同時 `最低完成關卡數` 也要重填 `1`（欄位從 `requireClears` 改名成 `requireClearsMin`，舊的鍵已經是死資料）。
- [ ] `邪佛對話` 設 **`最高完成關卡數=0` ＋ `條件不成立時=跳過這顆繼續`**；`初入場景對話` 設 **`最高完成關卡數=0`**。
- [ ] 確認 `給紅嫁衣劇本` 這顆 trigger 還需不需要（第 2 周目起的門要改走「放劇本進傳送門」那條路，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §4 的連帶待辦）。

---

## 鍛造介面 (ForgingPanel) — 2026-07-29 建立

版面與拖放已完成（按 **Y** 開啟，強制連背包並排；從背包把武器／裝備拖到鐵砧中央那一格）。
完整說明見 [FORGING.md](FORGING.md)。程式檔：`Assets/Scripts/UI/Panels/ForgingPanel.cs`、
`Assets/Scripts/UI/ForgeSlotWidget.cs`、`Assets/Scripts/Inventory/ForgeSlotGrid.cs`／`ForgeSocketGrid.cs`／`ForgeSockets.cs`。

尚缺 / 待補：

- [ ] **兩顆按鈕沒有功能**：「移除鑲嵌」「拆除裝備」按下去只跳「這個功能還沒做好」toast，事件刻意還沒接。
- [ ] **裝備沒有「孔位數」屬性** → 六個鑲嵌孔永遠全鎖。解鎖鏈路已經做好，接法：`ItemTable.csv` 加一欄 `SocketCount`(0~6)
      → `ItemData` 加欄位、`ItemDatabase` 解析 → 把 `ForgeSockets.Of()` 改成讀它。**面板端一行都不用改。**
      （想先看解鎖的樣子：設 `Dipan.Inventory.ForgeSockets.DebugCount = 4;`）
- [ ] **沒有「寶石」這種道具** → 孔就算開了也放不進東西。做出寶石後把 `ForgingPanel.IsGem(ItemData)` 從 `false` 改成
      `d.Category == "Gem"` 之類即可。
- [x] ~~`ForgingPanel_Btn.png` 沒去背~~ → **已補上透明版**（2026-07-29），`ArtSpec` 已改讀 alpha 邊界框 `(7,66,2388,559)`。
- [ ] **按鈕底板的 `maxTextureSize` 可以調小**：這張原圖 2416px 寬、畫面上只顯示約 240px，匯入時已被 Max Size 壓到 2048 仍是 8~10 倍過取樣。
      依 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) 的規範（大按鈕 256~512）改成 512 即可，程式端不用動（見 [PROBLEMS.md](PROBLEMS.md) D12）。
- [ ] **鍛造結果不進存檔**：鐵砧與孔位都是純記憶體，關面板一律退回背包。等鑲嵌真的會改變裝備屬性時，才需要決定
      「鑲好的寶石存在哪」（大概是 `ItemStack` 上加附加資料，屆時 `InventoryDTO` 要一起改）。
- [ ] **開啟方式是熱鍵 Y**，之後要改成鐵匠 NPC 的互動點：走 `openPanel` 觸發填 `panelId=forge`
      （`InteractionManager.BuildKindRegistry()` 已是可註冊的表，參考 [GACHA_SYSTEM.md](GACHA_SYSTEM.md) 的祭壇作法）。
      接上之後 `StorageBagCoordinator.forgeKey` 可以拿掉。
- [ ] **座標為量測值**：七個方框、兩顆按鈕、關閉鈕、標題的位置都是照示意圖量的常數（`ForgingPanel.cs` 上方）。
      實機若有偏移，微調那幾個常數即可（同 InventoryPanel / StoragePanel 的做法）。

---

## 選擇存檔畫面（SaveSlotPanel，2026-08-01 換上正式素材後）

見 [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md) §4.5。版面與資料都通了，剩下的是內容與微調：

- [ ] **卡片上沒有角色名**：`CharacterProfile.name` 有資料，但示意圖沒有這個欄位所以沒放。等正式建名輸入框做出來
      （目前新建一律用預設名「存檔N」）再一起決定要不要顯示、放哪。
- [ ] **「覆蓋（新建）」已移除**：現在要重開一欄必須先「刪除角色」再「新建遊戲」。如果實際玩起來覺得多一步很煩，
      再決定是要把覆蓋加回來，還是在刪除的 ConfirmPopup 上多一顆「直接重開」。
- [ ] **「完成 N 關」「上次遊玩時間」不再顯示**：資料都還在 roster（`clearedModuleCount` / `lastPlayedUtc`），
      純粹是照示意圖精簡掉。之後若想要，卡片右半邊「一周目」下方還有空間。
- [ ] **角色朝向**：素材是 `idle_right`，目前直接用（`ActorFlipX = false`）。實機看起來若該面向另一邊，
      把 `ActorFlipX` 改 `true` 即可（偏移量會跟著鏡射，不用改別的常數）。
- [ ] **只有 Base 血統可驗**：`Characters/SequenceImage/` 目前只有 `Base`。血統換外型的分支（`BloodlineTable.SpriteFolder`）
      程式已接，但要等真的多一個血統資料夾（例 `Vampire`）才測得到。目前三個血統的 `SpriteFolder` 都填 `Base`
      （見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md) 的缺口清單），所以喝了藥劑這裡也不會變。
- [ ] **素材的 `maxTextureSize`**：`SelectSavePanel_Bg` 是 1672px 寬的滿版底圖沒問題，但
      `SelectSavePanel_Btn`（914×273，畫面上只顯示約 200~300px 寬）與 `SelectSavePanel_Frame`（692×886 → 顯示約 426 寬）
      都有過取樣空間，依 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) 的規範可調小，程式端不用動。
- [ ] **座標為量測值**：卡片位置與卡內所有元件的偏移都是照示意圖量出來的常數（`SaveSlotPanel.cs` 上方）。
      卡內元件一律寫成「相對卡片中心的偏移」，三張卡片共用同一組數字，實機有偏移微調那一組即可。
- [ ] **非 16:9 螢幕**：底圖是 16:9，程式用 cover（取 `max(寬比, 高比)`）放大並在後面鋪一層深色底。
      超寬螢幕上左右會被裁掉一些背景（卡片本身留有足夠邊界不會被切），實機確認一下就好。

---

## 能力珠鑲嵌（2026-08-03 主功能完成後）

見 [GEM_SOCKET.md](GEM_SOCKET.md)。整條鏈路（產生 → 掉落 → 鑲嵌 → 生效 → 存檔）已通，以下是刻意留到之後的：

- [ ] **數值上限尚未討論**（最重要）：能力刻意沒有上限，目前只擋住「會把遊戲弄壞」的下限
      （發射間隔 0.02 秒、飛行速度 0.05、DOT 節拍 0.02、單次子彈數 64，寫在 `PlayerAbilities` 的安全夾值區）。
      作者要求等系統完成後**一起確認平衡用的上限並記錄下來**，兩邊都要保持警覺：某些設定過火會讓遊戲直接死掉。
- [ ] **「強化裝備」按鈕**（鍛造介面左）：裝備本身也能強化三次，功能未接（按了跳提示）。
- [ ] **「拆除裝備」按鈕**（鍛造介面中）：功能未接。**語意也還沒定義**——疑似是「把裝備分解、產出能力珠」
      （作者提到珠子的等級「在怪物掉落，或是拆解武器時給予」）。要決定：產出的珠子種類是隨機還是跟裝備有關？
      裝備上原本鑲著的珠子一起還回來還是毀掉？會不會失敗？
- [ ] **「鑲嵌珠強化石」道具**：背包裡對強化石按右鍵 → 對珠子按左鍵 → 該珠升一級。
      **只能對背包裡未滿 Lv3 的珠子用，不能對已經鑲在裝備上的珠子用。**
- [ ] **武器/裝備的「內建能力等級」欄位**：目前武器的基礎值直接來自配方表（過渡期，作者之後會重整武器表與配方表、
      做一批正式的基礎武器）。`PlayerAbilities.Rebuild()` 裡已經留好掛點（`AddBuiltIn` 那一行註解），屆時解析出來丟進 `Add()` 即可。
- [ ] **角色屬性區（`GemTarget.Player`）只累加、還沒有人套用效果**：最大生命／減傷／移速那類珠子已經可以填進 GemTable，
      容器也會正確累加，但還沒接到角色數值上。等屬性系統做出來再接。
- [x] ~~珠子的 icon 是借用的~~ 2026-08-04 正式素材全數到齊：珠身三級 `gemBase_red_lv1~3` + 8 種能力符號
      `gemIcon_bounce/pierce/speed/damage/homing/split/scale/rapid`，都是 500×500。
      疊圖規格（符號 55%、三級各自上移 8/24/14）已驗證八種都不爆框，見 [GEM_SOCKET.md](GEM_SOCKET.md) §6.5。
- [ ] **測試防具 501/502 的 icon 仍是借用的**（護身符借 `weapon_bronzeMirror`、戒指借 `weapon_talisman`）。
- [ ] **屬性珠的藍色珠身還沒畫**：`Target=Player` 的珠子會去找 `gemBase_blue_lv1~3`，目前不存在
      （只會印 Warning、不會壞）。等真的做屬性珠時再補。
- [ ] **抽選面板顯示珠子時等級固定 Lv1**：面板只拿得到 itemId，實際等級是 `RunProgress.GiveItem` 發獎當下才骰的。
      要顯示正確等級得讓 `GachaService` 改成回傳完整實例。開寶石池之前不影響。
- [ ] **抽選還沒有「寶石池」**：加法見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md) §8，零程式碼
      （`GachaPoolTable.csv` 加一列 + 新建 `BaseGemRoll.csv` + 拖進 `GachaTableProvider` 陣列 + 地圖擺 `openPanel`）。
      ⚠ 最容易漏的是「拖進 provider 陣列」那一步，漏了不會報錯、只會顯示「還沒有可抽的東西」。
- [ ] **孔數機率目前是暫定的平均分配**（0~6 各 14%）。作者說第一周目不該給高孔數，
      `RandomRules.SocketCountWeightsByCycle`（依周目的權重對照表）已經預留，填進去即可，不用改程式。

---

## 怪物出生點「重複產生」— 2026-08-06 建立（見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3.5）

重複產生（每隔 N 秒生一波）＋ 怪物 id 陣列已可用。尚缺 / 待確認：

- [x] ~~沒有「停止條件」~~ / ~~不吃通用欄位~~ → **2026-08-06 改用觸發鏈的通用條件欄位解決**：`monsterSpawn` 現在走 `TriggerChain.IsActive`，條件旗標／初始停用＋解鎖旗標／周目上下限／道具／完成關卡數對出生點全部有效。條件旗標是持續判定 → 配「開關(按F)」就是「按 F 開始／再按一次暫停」；填 `!旗標名` 就是「這個旗標一成立就停止湧怪」。見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3.5～3.6。
      > 中途曾為此另開一個專屬的 `startFlag`「啟動旗標」欄，**已移除**——通用條件旗標和專屬啟動旗標長得幾乎一樣、擺在同一個面板上，作者第一次用就填到通用欄位去（靜默無效、一進圖就生怪）。教訓：**同一件事不要給兩個入口**。
- [ ] **經濟平衡未定**：重複產生的怪照常掉寶（銅錢＋35% 藥），理論上可以掛機刷錢。等真的擺出第一個「湧怪房」後再回頭看要不要限總量、或改成前 N 隻才掉。
- [ ] **未實機驗證**：程式與編輯器 schema 都改好了，但這台環境跑不了 Unity——第一次用要確認：編輯器面板出現「重複間隔秒／同時存在上限」兩欄、填 3 秒進遊戲會每 3 秒補怪、留空的舊出生點行為完全沒變。

---

## 背包介面重製 — 2026-08-07 建立（見 [INVENTORY.md](INVENTORY.md)）

新美術＋裝備/消耗品雙頁籤＋分頁已完成，同日實機跑過並依回饋微調三輪（版面尺寸 → icon 正規化 → 格子提示）。尚缺 / 待確認：

- [x] ~~未實機驗證~~ 2026-08-07 實機跑過，並依回饋改了三輪：
      ① 裝備欄 icon 放大、頁碼只顯示當前頁、金錢改置左、並排位置改用「看得見的美術」重算；
      ② 物品 icon 依不透明內容自動正規化（`IconFit`）、數量字級依格子大小算＋加陰影；
      ③ hover 高亮改成只描邊不填滿（`SlotOutline`）、「可放這格」改成會呼吸的亮外框。
- [ ] **鍛造格的高亮還是舊的整片上色**（`ForgeSlotWidget` 自己有一份 `_dropHi`，沒接 `SlotOutline`）。背包/倉庫已改，鍛造沒動——實際看過覺得突兀再統一。
- [ ] **`UIManager.backdropColor` 值得回頭看**：寫 0.60，在 Linear 空間下等效只有 0.34，所以開視窗時背景其實沒那麼暗（見 [PROBLEMS.md](PROBLEMS.md) E11 與 2026-08-07 掃出來的半透明數值清單）。同組還有文字陰影 0.85 → 等效 0.58。**不要照清單全改**，看到哪裡不對再查。
- [ ] **消耗品包可能比裝備包先滿**：藥水 + 材料 + 劇本 + 血統藥劑 + 能力珠都擠在消耗品包，而珠子是一顆一格不可疊。真的不夠就把 `InventorySystem.ItemBagCount` 調大（介面會自動多一頁），不用改程式。
- [ ] **背包滿了的溢出處理仍未做**：`RunProgress.SettleIntoBag` 放不下時只印 Warning 就捨棄（既有問題，分包後變成「某一包滿了」）。
- [ ] **頁籤上沒有文字**（只有圖示），照示意圖做。之後若要加「裝備 / 消耗品」字樣，記得走 `Language.GetText` 不要硬寫。
- [ ] **舊存檔第一次載入會重排一次**背包順序（依分類分配到兩包，東西與鑲嵌都不掉）。Console 會印重排幾件，看到不用緊張。
- [ ] **`SaveSlotPanel` 的武器圖還是直接讀 `data.Icon`**（沒走 `ItemIcons`），所以不吃 icon 正規化。那個畫面有自己一套量好的 ArtSpec，動之前要先確認不會歪。
- [ ] **堆疊分割、右鍵快速搬運、稀有度底色、排序規則細化** 仍未做（沿用舊的待辦）。

---

## 血統系統 — 2026-08-18 建立（見 [BLOODLINE.md](BLOODLINE.md)）

系列＝三階段（殭屍→毛殭→旱魃）、兩張表、逐階進階藥劑、外型與立繪切換全部完成。以下是缺口。

**要在 Unity 手動做的（沒做的話功能等於沒上）：**

- [ ] **把 `Assets/Data/BloodlineSeriesTable.csv` 拖進 GameManagers 上 `GachaTableProvider` 的「血統系列表」欄。** 漏拖不會報錯，只會在 Console 印一則指引警告，然後所有進階藥劑都喝不了（找不到系列）。
- [x] ~~跑 `Project Tools → Sync Map Assets`~~ → **已跑**（2026-08-18 確認 catalog 已含 Jiangshi/Maojiang/Hanba 的 SequenceImage 四種動作與 Talk 八種情緒）。⚠ 日後再動這些資料夾要記得重跑，否則換血統後角色會只剩影子。
- [x] ~~進階藥劑的 icon~~ → **已到位**：`bloodline_lvup_middle`（310）、`bloodline_lvup_high`（311），CSV 已指過去。

**功能缺口：**

- [x] ~~變身演出（閃電＋煙霧）~~ → **已做**（2026-08-18，見 [BLOODLINE.md](BLOODLINE.md) §5）：倒下 → 天雷 → 煙霧＋電弧 →（煙裡換裝）→ 爬起，約 6 秒。
- [x] ~~變身時遊戲不暫停、怪物照打~~ → **已改成全程暫停**（2026-08-19，見 [PROBLEMS.md](PROBLEMS.md) **D15**）。演出鏈上所有計時器改吃 unscaled（三個 `Unscaled` 旗標＋協程的 `Wait`）。
- [x] ~~喝完藥沒有「你變成了什麼」的揭示~~ → **已做 `BloodlineIntroPanel`**（2026-08-19，見 [BLOODLINE.md](BLOODLINE.md) §5）：舊立繪斑駁剝落 → 新立繪浮現 ＋ 姓名牌飄入。
- [ ] **進 Unity 後第一次跑要確認 `BloodlineDissolve.shader` 有被匯入**（新檔，Unity 需要重新編譯著色器）。載不到不會壞——面板會退化成 alpha 淡入淡出並在 Console 印一則警告——但那就看不到斑駁效果了。
- [ ] **立繪揭示面板的版面要實機確認**：預設值已依作者給的示意圖算過並用 Python 合成驗證過（見 BLOODLINE.md §5 的版面表），但螢幕上看還是可能要微調。Play 模式中在 `[UIManager] → Layer_Overlay → BloodlineIntroPanel` 直接拉 `底版顯示高度` / `立繪可用區` / `立繪底邊距離` / `姓名底版中心高度` / `名字區域`，重喝一次即時生效；**調完記得回填程式碼預設值**（Play 模式的值不會保存）。
- [ ] **姓名石碑用的是 `BloodlinePanel_NameBg`（866×288 淺色石碑），不是 boss 那張深色牌匾。** 因此 `NameColor` 是深血紅——**日後若換回深色底版，字色一定要同步改回亮色**，否則字會整個看不見。
- [ ] **`Talk/Base/normal.png` 比例與其他三張不同**（1122×1402 vs 1024×1536）。面板已用「等比縮到框內、靠下對齊」吸收，但第一次喝藥（人類 → 殭屍）那一幕人類會比殭屍小一圈。重畫成 1024×1536 就完全對齊。
- [ ] **只用 `normal` 表情**。想讓不同血統用不同表情（例如旱魃用 `proud`），把 `BloodlineIntroPanel.PortraitEmotion` 改成從表B 讀一個新欄位即可。
- [ ] **「只能在邪佛廣場喝血統藥劑」這個方案沒做**（2026-08-19 評估過）。實作成本是 `BloodlineSystem.Plan()` 開頭一行 `CurrentMapId != SaveConstants.HubMapId` ＋ 一句語言表文字，而且三種藥劑目前**只從廣場祭壇抽得到**、對玩家零摩擦。純粹是設計決定：若之後想讓血統藥劑從 boss 掉落，這條限制會變成「得先打完那關帶回廣場」，要一起想。
- [ ] **體型倍率不影響碰撞框**：1.5 倍體型的角色 hitbox 跟 1 倍一樣大。目前刻意保留（動 hitbox 會改手感），但若之後體型差距拉大到影響判讀就要一起處理。
- [ ] **佛光的傷害半徑會跟著體型放大**（作者拍板「看到的就是打得到的」）。半徑 ×1.5 等於面積 ×2.25，而每拍傷害不變 ⇒ 大體型血統的佛光 DPS 實質更高。目前唯一走 `radiusScale` 的就是佛光，之後做平衡時記得這條。
- [ ] **新增「持續掛在玩家身上」的效果時，記得在 `PlayerController.RefreshBodyScaledVisuals()` 補一行**，否則體型改變後它會停在舊尺寸（影子、佛光光環、集氣光圈都是這樣接的）。相關通則見 [PROBLEMS.md](PROBLEMS.md) E14。
- [ ] **`BodyScale` 的數值要實機看過再定**：目前殭屍 1／毛殭 1.5／旱魃 1.2 是憑印象給的。素材量出來的可見高其實差不多（Base 193px／殭屍 174／毛殭 197／旱魃 175），差別在姿勢與可見寬，所以只能用眼睛調。改 CSV 即時生效，不用改程式。
- [ ] **變身表演沒有音效**（專案還沒有音訊系統）。雷擊、煙爆、立繪剝落是這遊戲裡最該有聲音的三個瞬間，音訊系統做好後第一個補這裡。呼叫點在 `BloodlineTransformFxRunner` 的階段 ③ ④，以及 `BloodlineIntroPanel` 的溶解起點。
- [ ] **實機看過再調節奏**：總長約 6 秒，其中倒下與爬起各佔 2.08 秒（dead 25 幀 @12fps）。常數在 `BloodlineTransformFxRunner` 檔頭，覺得拖就把 `FallFpsMul` / `WakeFpsMul` 調大。
- [ ] **煙塵放大後可能偏糊**（64px 像素圖放大到玩家身高 ×1.6 的必然）。想改成「沿身體撒 3~4 顆、錯開時間」的話：`SmokeBurstCount` 調 3、`SmokeHeightRatio` 調回 0.9，流程一行不用改。
- [ ] **五個屬性只存不套用**（行走速度/力量/敏捷/魔力/體力）。等角色屬性系統。套用點就在 `BloodlineSystem.ApplyTo()` 的第 2 段註解處。⚠ 在屬性系統做好之前**不要偷偷改 `CombatStats` 或 `MoveSpeed`**——舊版就是這樣搞出兩套來源，已經拿掉一次了。
- [ ] **玩家沒地方「事後」查自己的血統與階段**。喝下去當下有立繪揭示面板，但之後就沒地方看了。之後跟角色資訊面板一起做（`BloodlineSystem.CurrentDisplayName` / `CurrentSeries` / `CurrentStage` 都已備好）。
- [ ] **`SkillId` 仍是死欄**（技能系統不存在，填了只印一則 log）。
- [ ] **只有殭屍一個系列**。吸血鬼系列已有藥劑 icon（`bloodline_Vampire.png`）但沒有序列圖與立繪。加新系列的步驟見 [BLOODLINE.md](BLOODLINE.md) §7（進階藥劑不用動，程式碼一行不用改）。

**待觀察 / 之後再決定：**

- [ ] **三階段在一個周目內湊不湊得齊**。輪迴不保留血統 + 三階段各要一瓶藥 ⇒ 每周目都要從人類重爬，而一周目只玩 7 關。目前進階藥劑在道具池（中階權重 3、高階 1）。關卡池只有一關時調了也沒意義，等內容多起來再決定是調權重、還是把進階藥劑改成關卡/boss 獎勵。
- [ ] **抽選面板字串未走語言表**（`GachaPanel` 仍是 `const string`，全面板的既有問題）。血統系統本身的字串已走 `Language.GetText`（2001–2099 段）。
