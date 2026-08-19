# 選擇劇本面板（邪佛發牌選關 UI）

> 返回 [文件總覽](README.md)
>
> 核心迴圈企劃見 [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md)；劇本＝背包道具見 [INVENTORY.md](INVENTORY.md)；傳送門開門見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)（`OpenPortal` / `ScriptsPanel`）；周目/完成關卡/存檔見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) §14；關卡與 module 見 [MAP_SYSTEM.md](MAP_SYSTEM.md)。
>
> **狀態：✅ 初始版（單卡紅嫁衣）程式完成（2026-07-15），待 Unity 實機驗證與地圖接線。⏳ 從第 2 輪起「系統自動發牌 3 新 + 1 舊」尚未做——需先有其他關卡＋在存檔加「完整通關」判定（見 §4）。**

這是核心迴圈 **access 軸（邪佛免費發關卡）** 的 UI。邪佛對話後開此面板，玩家在卡片按「領取」拿走一份劇本，之後照現有流程（走到傳送門 → 把劇本放進傳送門 UI → 開門進關卡）。

---

## 1. 用途與整體流程

**設計主軸（[CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md)）**：能玩哪一關由**邪佛發牌、系統決定**，永遠免費、不花資源。每回合擺出「**3 新關 + 1 舊關（業障回響）**」四選一；玩家領一張、去打、過關回廣場，再發下一批。

**劇本＝背包道具**：一份劇本就是 `ItemTable.csv` 裡一個 `IsScript` 道具（有 `TargetMapId` 就算劇本）。紅嫁衣劇本＝道具 **104**（`TargetMapId=1` → RedBridalGown）。領取＝把該道具加進背包，跟舊的 `giveItem` 效果一樣。

**與傳送門 `ScriptsPanel` 的分工**（別搞混）：
- **本面板 `SelectScriptPanel`**＝「選 / 領劇本」（邪佛發牌）。
- **`ScriptsPanel`**（傳送門互動點 F 開）＝「把劇本放進方框 → 開傳送門」。領劇本之後才用到它。

---

## 2. 現況：初始版怎麼運作（單卡紅嫁衣）

**叫出方式＝觸發鏈動作 `selectScript`**（見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)）。取代原本邪佛對話後那個 `giveItem 104`。被 `next` 啟動 → 開面板 → 玩家領取 → 面板關 → 接自己的 `next`（鏡頭聚焦 / 傳送門對話…，其餘流程沒動）。`TutorialManager` 不必改（照樣偵測背包出現道具 104 後強制引導）。

**目前的 `selectScript` 參數（初始腳手架，之後會拿掉，見 §4）**：
| 參數 | 說明 |
|---|---|
| `scriptIds` | 可領取的劇本道具 id，`\|` 分隔。初始＝`104`。 |
| `specialIds` | 其中要用特殊裂紋框的 id，`\|` 分隔。初始＝`104`。 |

**面板行為**：模態、暫停遊戲、`ShowBackdrop`；**`CloseOnEscape=false`**——領取是唯一出路（比照邪佛發牌不可略過）。領取＝`InventorySystem.AddItem` → 關面板 → 回呼開啟端接鏈。

**卡片素材依 module 名自動載入**（不需編輯器逐張設定）：劇本道具 → `TargetMapId` → `MapsTable` → module 名，再組路徑載圖。第 4 格舊關用**特殊裂紋框**。
| 素材 | 路徑（相對 `Resources/`） |
|---|---|
| 示意圖 | `UI/SelectScriptPanel/StageImage/SelectScriptPanel_StageImage_<module>` |
| 關卡名 | `UI/Texts/Text_StageName_<module>` |
| 一般框 | `UI/SelectScriptPanel/SelectScriptPanel_Frame` |
| 特殊框（業障回響/紅嫁衣） | `UI/SelectScriptPanel/SelectScriptPanel_SpecialFrame` |
| 領取鈕 | `UI/SelectScriptPanel/SelectScriptPanel_Btn` ＋字 `UI/Texts/Text_Gain` |

> **加新關就照此命名丟圖即可**（`_<新module>`），程式自動接、零設定。

**版面可調常數**（`SelectScriptPanel.cs` 上方，量測值、實機微調）：`CardHeight`、`StageBoxW/H`（示意圖窗口，以 `preserveAspect` 塞入、留白落在框內黑底）、`StageOffsetY`、`NameWidth`/`NameOffsetY`、`BtnWidth/Height`/`BtnOffsetY`、`CardGap`（多卡並排間距）。

> ⚠️ **踩過的坑**：示意圖必須畫在**框之上**。特殊框中間是**不透明黑底**，把圖放框後方會被整片蓋掉（初版就這樣，只剩名字/按鈕看得到）。已改為框先畫、示意圖疊在窗口區。

**面板已支援多卡**（水平置中排列），所以未來 3新+1舊 主要是補「內容從哪來」，UI 不用重做。

---

## 3. 相關檔案（現況）

| 檔案 | 角色 |
|---|---|
| `Assets/Scripts/UI/Panels/SelectScriptPanel.cs` | 面板本體（全程式建構、依 module 載素材、領取＝AddItem→回呼） |
| `Assets/Scripts/Map/TriggerChain.cs` | `selectScript` 鏈動作（`TypeSelectScript` / `ExecuteSelectScript` / `ParseIds`） |
| `DipanProj_MapEditor/Assets/Scripts/Data/TriggerType.cs` | 編輯器「選擇劇本(鏈動作)」型別（改後**重開編輯器**合併進 `triggerTypes.json`） |
| `Assets/Data/ItemTable.csv` | 劇本道具（104 紅嫁衣；`TargetMapId` 指目的地關卡） |
| `Resources/UI/SelectScriptPanel/`、`Resources/UI/Texts/` | 框/鈕/示意圖/關卡名/領取字 素材 |

> **`Resources/UI/Texts/` 是全專案「圖片型文字」的共用資料夾**，不是本面板專屬。
> 凡是「畫成圖的字」都放那裡；純美術的框、按鈕底、面板底圖不算（不隨語言變，留在各自的面板資料夾）。
>
> ⚠ **實體路徑帶語言資料夾**：`UI/Texts/tw/Text_Gain`、`UI/Texts/en/Text_Gain`（**同名**，不加尾綴）。
> 上表寫的 `UI/Texts/Text_Gain` 是**邏輯路徑**——程式照這樣寫就好，
> `LoadSprite` 會經 `LocalizedArt.ResolveExisting` 換成當前語言，缺圖退回繁中。
> 見 [LOCALIZATION.md](LOCALIZATION.md) §圖片型文字。

**Unity/編輯器接線**：Unity 匯入新腳本 → 重開地圖編輯器 → 在 `Main_Square` 把原本給紅嫁衣劇本的 `giveItem` 換成 `selectScript`（`scriptIds=104`、`specialIds=104`，接回原鏈位置）→ `Sync Map Assets`。

---

## 4. ⏳ 待處理：從第 2 輪起「系統自動發牌 3 新 + 1 舊」

> **核心原則（作者拍板 2026-07-15）**：從第 2 輪起，面板顯示的關卡**由系統依存檔算出來、不讓編輯器配置**。編輯器端最終只保留一顆**無參數**的 `selectScript`（＝「在這裡開啟劇本選擇介面」），內容全由系統準備。第 4 格（未完整通關的舊關）邊框換成 `SelectScriptPanel_SpecialFrame.png`。

### 4.1 分工（最終形態）
- **編輯器**：`selectScript` 改成**無參數**（拿掉 `scriptIds`/`specialIds`），只代表「開啟選劇本介面」。地圖以後不用再為選關動任何東西。
- **系統**：一個「發牌服務」（暫名 `SelectScriptOffer`）依存檔算出本次要擺哪幾張卡（含哪張用特殊框），餵給 `SelectScriptPanel.Open`。**初始紅嫁衣也收進這個服務**（系統看「第 1 周目、無任何進度」就固定發紅嫁衣一張），這樣 trigger 從一開始就能無參數、地圖永不再動。

### 4.2 前置缺口（沒有這些就做不了）
1. **可派發關卡目錄（level pool）**：系統要 roll「3 個新關」，需先有一份「所有可派發關卡」清單（每筆＝module ＋對應劇本道具 id ＋是否教學關/深關）。可從 `ItemTable` 的劇本道具自動推、或另開一張小表。**目前只有紅嫁衣，池子還是空的——要等多做幾關。**
2. **完成度判定（最關鍵、目前存檔最缺）**：要能分「新關（沒玩過）／通關但沒挖乾淨的舊關／100% 完整通關」。但現在 `ProgressDTO` **只有 `clearedModules`（通關＝是/否）布林，沒有『完整通關』概念**。3新+1舊 卡在這裡。需在存檔加「每關是否完整通關（或完成度）」，對應企劃「完整通關＝隱藏 boss／分支／綁定獎勵全解完」。**待拍板：布林（完整/沒完整）還是完成度百分比。**
3. **roll 一次就固定＋存檔**：玩家該次選關時 roll 出 3新+1舊，**把結果（module 清單）直接寫進存檔**（存結果、不存種子，最穩）；重開遊戲同一次是同一批。→ `ProgressDTO` 加一個「本次發牌清單」欄位（例 `currentOffer`）。
4. **開關狀態機**：可開 →（領取一張）→ **本次鎖住不能再開** →（去打、過關回廣場）→ 系統 roll 新一批 → 又可開。需一個「本次發牌已領取」旗標/欄位。**待拍板：何時 roll（過關回廣場當下 / 首次開介面當下）。**

### 4.3 roll 規則（對照企劃 [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) §2）
- **3 新關**：從 level pool 隨機挑，**排除本周目已通關**的（`clearedModules`）。新關不夠時用舊關補足（選單不開天窗）。
- **1 舊關（業障回響）**：從「**通關過但沒 100% 完整**」的關挑（輪迴後這些關重回候選池；100% 完整的關「畢業」不再召回）。**用特殊裂紋框**（`special=true`，UI 已支援）。沒有符合資格的舊關就補一張新關。
- **權重/保底**（企劃有提，v1 可後補）：某秘密拖越多輪 / 玩家上次越接近解開，進第 4 格權重越高，到一定程度保證出現。**v1 可先單純隨機或挑最近一關，之後再加權重。**
- **卡片顯示資訊**（企劃）：揭露獎勵類型＋危險度＋模糊完成度，但實際內容/怪物留白——這是**日後 UI 增強**，非首版必要。

### 4.4 待拍板決策（實作前先定）
- [ ] 完成度：布林（完整/沒完整）vs 百分比。
- [ ] roll 時機：過關回廣場當下 vs 首次開介面當下。
- [ ] 業障回響的權重/保底：v1 要不要做，還是先純隨機。
- [ ] level pool 來源：從劇本道具自動推 vs 另開關卡目錄小表。

### 4.5 實作步驟（等前置到位後）
1. `ProgressDTO`（[SAVE_SYSTEM.md](SAVE_SYSTEM.md)）加：完整通關集合（或完成度）＋ `currentOffer`（本次發牌 module/劇本清單＋哪張 special）＋「已領取」旗標。schema 版本 +1、補遷移。
2. 新增發牌服務 `SelectScriptOffer`：`GetOrRollOffer()`（沒有就 roll 並存、有就回存的那批）、`ConsumeOffer()`（領取後鎖）、`ClearOfferAfterClear()`（過關後清、下次重 roll）。初始情境回紅嫁衣。
3. `selectScript` 動作改成**無參數**：呼叫 `SelectScriptOffer.GetOrRollOffer()` → 轉成 `List<CardSpec>` → `SelectScriptPanel.Open(...)`。移除 `scriptIds`/`specialIds`（編輯器 `TriggerType.cs` 也拿掉那兩個 param）。
4. 領取回呼：`AddItem` 已在面板做；另呼叫 `SelectScriptOffer.ConsumeOffer()` 記「本次已領」。過關流程（`GameFlowManager` / `clearLevel`）接 `ClearOfferAfterClear()`。
5. `MarkModuleCleared` 之外，接上「完整通關」的寫入點（挖乾淨判定，來源＝該關的隱藏 boss/分支/綁定獎勵旗標）。

---

*建立於 2026-07-15：初始版（單卡紅嫁衣）選擇劇本面板 ＋ `selectScript` 觸發鏈動作 ＋ 編輯器型別；素材依 module 自動載、特殊框已支援。記錄「系統自動發牌 3 新 + 1 舊」的設計與前置缺口（level pool、完整通關存檔、roll 固定＋存檔、無參數 trigger + 發牌服務），待其他關卡與完成度到位後實作。*
