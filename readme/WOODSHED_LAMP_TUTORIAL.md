# 柴房佛燈教學（紅嫁衣關第一間房・強制引導）

> 返回 [文件總覽](README.md)

紅嫁衣關第一間房 **柴房（RedBridalGown_Woodshed, map 1）** 的強制新手教學：
一進柴房（沒撿過佛燈時）→ 播邪佛對話 → 對話關閉＝教學開始 → 走去撿地上的佛燈（靠近按 F）→ 撿起後佛燈地上物消失、往北的傳送點解鎖 → 提示按 B 開背包 → 手指指佛燈格、只放行它 → 點一下裝上佛燈 → 按 B 關背包 → **強制點亮**（鎖移動、只放行開火）：按住左鍵/空白鍵點亮佛燈 → 完成。做完寫永久旗標，之後（此生）不再強制、佛燈也不再出現。

佛燈＝既有道具/武器 **8「佛光」**（`ItemTable` 8，`WeaponID=8`，`RecipeTable` 21 `IsAura`）。此教學沒有新增任何武器，直接沿用它。

## 流程與所在程式

強制流程寫死在 `Assets/Scripts/UI/TutorialManager.cs`（沿用既有傳送門教學那套 phase 狀態機 + 四個通用面板：`GuideFingerPanel`/`TutorialBlockerPanel`/`TutorialDimPanel`/`TutorialHintPanel`）。phase：`LampGuidePick → LampForceF → LampOpenBag → LampClickEquip → LampCloseBag → LampLight → Done`。提示字串走多語系 `Language.GetText(1001–1006)`（見 [LOCALIZATION.md](LOCALIZATION.md)）。

- **啟動**：一進圖 `onEnter`「進場觸發-觸發邪佛對話」（gate `!tutorialBuddleLight`）接 `drama`「新手教學-邪佛對話」（dramaId 20）。**這顆 drama「關閉」時**廣播 `TriggerChain.OnTriggerFired("新手教學-邪佛對話")`，`TutorialManager` 收到就開始（`LampStartTrig` 就是這個名字；gate 於 `LampDoneFlag`，做過就不跑）。
- **撿取**：pickup 觸發點名 **`柴房佛燈拾取`**（`itemId=8`）。走到可撿範圍 → `SetExternalHold` 定住玩家、`AllowInteract` 放行按 F。撿完 pickup 廣播 `柴房佛燈拾取` → 進裝備步驟；pickup 的 `setFlag=tutorialBuddleLight` 同時讓地上物消失、解鎖傳送點。
- **裝備**：`AllowBag` 放行 B 鍵開背包（`HardLock` 仍鎖倉庫 K）→ `InventoryPanel.FindGridSlotRect(8)` 手指指佛燈格＋`TutorialBlockerPanel` 只放行它 → 點一下 → `InventorySystem.GetEquipped(Weapon)==8` 偵測到裝上 → 提示按 B 關背包。
  > 2026-08-07 背包改成雙頁籤＋分頁後，**佛燈（武器）在「裝備」頁籤**。`FindGridSlotRect` 已改成「找不到就自動切到那件東西所在的頁籤與頁數」，所以手指照樣指得到；而且背包的格子物件是**只建一頁 20 個重複使用**、切頁只重綁編號不重建，`TutorialBlockerPanel.LockTo` 鎖的 GameObject 不會失效。動這兩個地方時務必保持這個行為。
- **強制點亮**（`LampLight`）：關背包後 `StartLampLight` **解除全鎖**（`SetExternalHold(false)`，否則連開火都被擋）**但改上 `FireOnly=true`＝鎖移動、只放行開火**——玩家走不掉、只能按住左鍵/空白鍵點燈。`TickLampLight` 讀 `PlayerController.IsAuraActive`（佛光光環開著）**持續 0.35 秒**（避免手滑一下就過）→ 解除 `FireOnly`、寫 `LampDoneFlag`、完成。之後玩家自由，可走去（已解鎖的）傳送點。

## ⚠️ 新手教學「寫死清單」（改動前必看，集中在 `TutorialManager.cs` 上方常數）

| 寫死的東西 | 值 | 常數 |
|---|---|---|
| 啟動教學的觸發點名（drama「關閉」時開始） | `新手教學-邪佛對話` | `LampStartTrig` |
| 佛燈 pickup 觸發點名 | `柴房佛燈拾取` | `LampPickupTrig` |
| 佛燈道具/武器 id | `8` | `LampItemId` |
| 撿走佛燈旗標（pickup `setFlag` ＋ 地上物 `disappearFlag`） | `tutorialBuddleLight` | `LampTakenFlag` |
| 整段教學做過的記號（＝同一顆旗標；撿到即視為做過，`onEnter` 也 gate `!tutorialBuddleLight`） | `tutorialBuddleLight` | `LampDoneFlag` |

> **注意**：`LampTakenFlag` 與 `LampDoneFlag` 現在是**同一顆旗標 `tutorialBuddleLight`**——撿到佛燈就視為「這段做過」，`onEnter` 也用 `!tutorialBuddleLight` 當 gate（撿過就不再播邪佛對話、不再強制）。編輯器那幾個名字（`onEnter`/drama 名、pickup 名、以及 pickup 的 `setFlag`＝佛燈 `disappearFlag`）改了，這裡要一起改。

## 地圖佈置（`RedBridalGown_Woodshed.dipanmap`，現況）

- **佛燈地上物**（`weapon_sacredLamp`, cell(10,4)）：`disappearFlag: "tutorialBuddleLight"`（撿走後立即消失、下次進來旗標已成立＝不生）＋ `lightRadius: 3.0`（地上時就會發光、當暗場景光圈中心，見 [ATMOSPHERE.md](ATMOSPHERE.md)〈照明：光圈由光源驅動〉）。
- **pickup 觸發點** `柴房佛燈拾取`：cells `[[10,4]]`，params `itemId=8`、`setFlag=tutorialBuddleLight`、`requireFlag=!tutorialBuddleLight`、`toRealBag=true`、`next=新手教學結束後開啟傳送點`。
- **onEnter 觸發點** `進場觸發-觸發邪佛對話`：0 格，`requireFlag=!tutorialBuddleLight`、`next=新手教學-邪佛對話`。
- **drama 觸發點** `新手教學-邪佛對話`：`dramaId=20`、`next=柴房教學起點`。（關閉時啟動教學。）
- **togglePortal 觸發點** `新手教學結束後開啟傳送點`：pickup 撿完由 `next` 接到，開傳送點。
- **傳送點** `傳送點1`（去儲藏室）：`startDisabled=true`、`enableFlag=tutorialBuddleLight`——**撿到佛燈就開**。

> 這張圖已同時寫進編輯器來源（`DipanProj_MapEditor/...`）與遊戲端 `StreamingAssets/MapAssets/...`，**dipanmap-only 改動不必再跑 Sync Map Assets**；但兩個 Unity 專案的 C# 改動需 Unity 重新編譯。
>
> ⚠️ dramaId 20 必須真的存在於 `DramaTable.csv` 且**存檔到磁碟**（改了 CSV 記得 Cmd+S），否則 `DramaPanel.Show` 會早退、鏈卡住、教學不啟動。

## 幾個「通用」能力（非教學專屬，可重用）

1. **地上物 `disappearFlag`**（`ObjectInstance` 新欄位，兩專案都加、編輯器物件面板有「消失旗標」欄）：旗標為 true 時該地上物消失——進圖時已成立＝不生，關卡中途成立＝由 `MapObjectRevealer` 立即銷毀（碰撞一併移除）。是「出現旗標 `appearFlag`」的相反。用法：pickup `setFlag` → 撿走 X → X 的地上物消失。
2. **地上物 `lightRadius`**（`ObjectInstance` 新欄位，世界單位）：> 0 時 `MapLoader` 幫它掛 `LightSource`，暗場景下當光圈中心。詳見 [ATMOSPHERE.md](ATMOSPHERE.md)。
3. **pickup `toRealBag=true`**（`InteractionManager` pickup 參數）：這個拾取點直接進真背包 `InventorySystem`（不走關卡臨時包 `RunProgress`）。給「起始/教學道具」用——關卡內撿了要能**當場開背包裝備**、且**死亡也保留**（佛燈就是靠這個，否則關卡內撿到的東西只進臨時包、通關才落袋，背包 UI 看不到、無法裝備）。
4. **`TutorialManager.FireOnly` ＋ `PlayerController` 分支**：`FireOnly=true` 時 `PlayerController.Update` 走一條「鎖移動、保留朝向、只放行開火」的分支（`_moveInput=Vector2.zero` 後照常 `HandleFiring`）。用來做「強制玩家開火一次才放行」這種引導，比 `SetExternalHold`（連開火都擋）細一級。
5. **`PlayerController.IsAuraActive`**：唯讀屬性，佛光（IsAura）光環是否正開著。供教學偵測「玩家真的點亮佛燈」。
