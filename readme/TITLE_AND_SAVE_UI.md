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
| `Assets/Scripts/Flow/GameFlowManager.cs` | **總流程指揮**（＋`GameFlowBootstrap` 開機自動生）：開機顯示標題；新建/繼續/覆蓋/刪除；載場景＋等 MapManager 就緒→進廣場。 |
| `Assets/Scripts/UI/Panels/TitlePanel.cs` | 標題畫面（Window 層、全螢幕佔位）：標題圖（`Resources/UI/TitlePanel/TitlePanel_TW`，3:1）＋中間偏右佛陀動畫（`Resources/UI/TitlePanel/BuddhaTitle/BuddhaTitle_01..NN`，自動偵測幀數、按開始才播一次、用 unscaledTime 因暫停中）＋開始遊戲鈕（圖 `Resources/UI/Common/StartGameBtn`＋程式補字）→**動畫播完再多停 `BuddhaEndHold` 秒才**開存讀檔畫面（無圖則退回文字/直接開）。播放中鎖按鈕防重複、回標題自動重置回第一幀。位置/大小常數在檔案上方（`BuddhaFps`/`BuddhaOffset`/`BuddhaDisplaySize`/`TextGroupX`/`TitleWidth`/`TitleY`/`StartBtnWidth`/`StartBtnY`）。 |
| `Assets/Scripts/UI/TitleFireFx.cs` | **標題火焰特效**（UI 端、用 unscaledTime 因面板暫停）：① 全螢幕持續落火（火星/帶尾火條飄落閃爍，`UiFallingEmber`）② 標題燃燒（背後脈動火光＋沿標題往上竄、柔邊高斯羽化＋寬度抖動的火舌，`UiRisingFlame`）。**不是**用 MapsTable 火雨（那是世界端 SpriteRenderer、綁相機/deltaTime，暫停中不動且會被 UI 蓋掉）——改在 Canvas 上重做、複用火雨的程序生成佔位圖 `SceneEffectSprites`。由 `TitlePanel` 以 `EnableFireFx` 開關、`Init(emberRoot,titleRect,titleGlow)` 啟動。密度/大小/速度常數在 `TitleFireFx` 上方（`EmbersPerSecond`/`FlamesPerSecond`/`EmberFall*`/`TitleGlowAlpha` 等）。（曾試做佛陀兩肩蒸騰濃煙〔照搬 `SceneFxEmitter` fbm 煙塊〕，2026-07-03 決定不放、已移除。） |
| `Assets/Scripts/UI/Panels/SaveSlotPanel.cs` | 三欄存讀檔畫面：卡片顯示周目/完成關卡/上次遊玩；新建/繼續/覆蓋（ConfirmPopup）/刪除。 |
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

## 5. 實機驗證步驟

1. 開 Unity 等編譯，Console 無紅錯（新程式都在預設 `Assembly-CSharp`）。
2. 進 Play：應出現 **標題畫面**（不再直接進關卡）。按「開始遊戲」→ 三欄存讀檔畫面。
3. **新建**：點空欄「新建遊戲」→（沒加 Intro）直接進邪佛廣場，玩家在洞穴出口；Console 有 `已存檔`（進廳自動存）。回標題再看該欄 → 顯示「周目 1・完成 0 關・上次遊玩 …」。
4. **繼續**：點該欄「繼續」→ 進邪佛廣場，這次玩家在**中央**（`hubIntroSpawnDone` 已 true）。
5. **完成關卡**（暫時用程式/測試呼叫）：`SaveManager.Instance.MarkModuleCleared("RedBridalGown")` → 回廳存檔 → 存讀檔畫面該欄顯示「完成 1 關」；再呼叫同一 module 不會 +1。
6. **覆蓋**：對有檔的欄按「覆蓋」→ ConfirmPopup 問 → 確定 → 該欄變回周目 1、完成 0。
7. **刪除**：按「刪除（測試）」→ 確定 → 卡片變空欄。
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
- 正式建名輸入框（目前新建用預設名「存檔N」；`UIBuilder.InputField` 已可用）。
- 正式素材（標題圖、卡片框、按鈕）替換佔位視覺。
- 輪迴選物 UI 與邪佛戰／最終關卡的流程接線（見 §6）。

---

*建立於 2026-07-03：標題／三欄存讀檔 UI ＋ GameFlowManager 總流程（新建/繼續/覆蓋/刪除、進廣場自動存、出生點旗標）＋ SaveManager 槽位與 in-place 輪迴 API。佔位視覺，待 Unity 實機驗證與換素材。*
