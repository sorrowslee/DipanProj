# 柴房佛燈教學（紅嫁衣關第一間房・強制引導）

紅嫁衣關第一間房 **柴房（RedBridalGown_Woodshed, map 1）** 的強制新手教學：
一進柴房 → 走去撿地上的佛燈（靠近按 F）→ 撿起後佛燈地上物消失 → 提示按 B 開背包 → 手指指佛燈格、只放行它 → 點一下裝上佛燈 → 按 B 關背包 → 放行玩家 → 按住左鍵/空白鍵點亮佛燈 → 完成。做完寫永久旗標，之後（此生）不再強制、佛燈也不再出現。

佛燈＝既有道具/武器 **8「佛光」**（`ItemTable` 8，`WeaponID=8`，`RecipeTable` 21 `IsAura`）。此教學沒有新增任何武器，直接沿用它。

## 流程與所在程式

強制流程寫死在 `Assets/Scripts/UI/TutorialManager.cs`（沿用既有傳送門教學那套 phase 狀態機 + 四個通用面板：`GuideFingerPanel`/`TutorialBlockerPanel`/`TutorialDimPanel`/`TutorialHintPanel`）。新增的 phase：`LampGuidePick → LampForceF → LampOpenBag → LampClickEquip → LampCloseBag → LampLight → Done`。

- **啟動**：柴房地圖上的 `onEnter`「進場觸發(自動)」點名 **`柴房教學起點`**，一進圖廣播 `TriggerChain.OnTriggerFired`，`TutorialManager` 收到就開始（gate 於 `LampDoneFlag`，做過就不跑）。
- **撿取**：pickup 觸發點名 **`柴房佛燈`**（`itemId=8`）。走到可撿範圍→`SetExternalHold` 定住玩家、`AllowInteract` 放行按 F。撿完 pickup 廣播 `柴房佛燈` → 進裝備步驟。
- **裝備**：`AllowBag` 放行 B 鍵開背包（`HardLock` 仍鎖倉庫 K）→ `InventoryPanel.FindGridSlotRect(8)` 手指指佛燈格＋`TutorialBlockerPanel` 只放行它 → 點一下 → `InventorySystem.GetEquipped(Weapon)==8` 偵測到裝上 → 提示按 B 關背包。
- **點亮**：放行玩家 → 讀 `PlayerController.IsAuraActive`（佛光光環開著＝按住左鍵/空白鍵中）持續 0.35 秒 → 完成，寫 `LampDoneFlag`。

## ⚠️ 新手教學「寫死清單」（改動前必看，集中在 `TutorialManager.cs` 上方常數）

| 寫死的東西 | 值 | 常數 |
|---|---|---|
| 啟動教學的 onEnter 觸發點名 | `柴房教學起點` | `LampStartTrig` |
| 佛燈 pickup 觸發點名 | `柴房佛燈` | `LampPickupTrig` |
| 佛燈道具/武器 id | `8` | `LampItemId` |
| 撿走佛燈旗標（pickup setFlag＋地上物 disappearFlag） | `永久:woodshedLampTaken` | `LampTakenFlag` |
| 整段教學做過的永久記號 | `永久:woodshedLampTutorialDone` | `LampDoneFlag` |

編輯器那 3 個名字（onEnter 名、pickup 名、以及 pickup 的 `setFlag`＝佛燈 disappearFlag）改了，這裡要一起改。

## 地圖佈置（`RedBridalGown_Woodshed.dipanmap`）

- **佛燈地上物**（`weapon_sacredLamp`, cell(10,4)）新增欄位 `disappearFlag: "永久:woodshedLampTaken"` —— 撿走後立即消失、下次進來（旗標已成立）根本不生。
- **pickup 觸發點** `柴房佛燈`：cells `[[10,4]]`，params `itemId=8`、`repeat=永久`、`setFlag=永久:woodshedLampTaken`、`toRealBag=true`。
- **onEnter 觸發點** `柴房教學起點`：0 格、無 params。

> 這張圖已同時寫進編輯器來源（`DipanProj_MapEditor/Maps/...`）與遊戲端 `StreamingAssets/MapAssets/...`，**dipanmap-only 改動不必再跑 Sync Map Assets**；但兩個 Unity 專案的 C# 改動需 Unity 重新編譯。

### 傳送點門（作者手動設定）

柴房北面傳送點 `傳送點1`（去儲藏室）要「撿到佛燈才開」，在編輯器把它設 `初始停用`＝✓、`enableFlag`＝
`永久:woodshedLampTutorialDone`（整段教學做完才開）或 `永久:woodshedLampTaken`（撿到就開）。

## 兩個新的「通用」能力（非教學專屬，可重用）

1. **地上物 `disappearFlag`**（`ObjectInstance` 新欄位，兩專案都加、編輯器物件面板有「消失旗標」欄）：
   旗標為 true 時該地上物消失——進圖時已成立＝不生，關卡中途成立＝由 `MapObjectRevealer` 立即銷毀（碰撞一併移除）。是「出現旗標 `appearFlag`」的相反。用法：pickup `setFlag` → 撿走 X → X 的地上物消失。
2. **pickup `toRealBag=true`**（`InteractionManager` pickup 參數）：
   這個拾取點直接進真背包 `InventorySystem`（不走關卡臨時包 `RunProgress`）。給「起始/教學道具」用——關卡內撿了要能**當場開背包裝備**、且**死亡也保留**（佛燈就是靠這個，否則關卡內撿到的東西只進臨時包、通關才落袋，背包 UI 看不到、無法裝備）。
3. **`PlayerController.IsAuraActive`**：唯讀屬性，佛光（IsAura）光環是否正開著。供教學偵測「玩家真的點亮佛燈」。
