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

*建立於 2026-06-24：設定面板（音量 slider 先可拖曳、未接音訊；離開確認彈窗為暫時 placeholder；開啟用測試鍵 O）。*
