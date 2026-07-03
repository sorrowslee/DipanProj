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
