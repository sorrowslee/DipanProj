# 儲藏室 HP 藥水教學（紅嫁衣關第二間房・強制引導）

> 返回 [文件總覽](README.md)

柴房佛燈教學（見 [WOODSHED_LAMP_TUTORIAL.md](WOODSHED_LAMP_TUTORIAL.md)）之後，玩家只能通到上一層的**儲藏室（RedBridalGown_Storeroom）**。這裡教「撿 HP 藥水 → 放進藥水格 → 按 1 喝」，整套沿用佛燈教學那組 phase 狀態機與四個通用面板。

一進儲藏室（沒喝過藥水時）→ 播邪佛對話（dramaId 21）→ 對話關閉＝教學開始 → 走去撿櫃子上的藥水（靠近按 F）→ 撿到後 → 提示按 B 開背包 → 手指指 HP 藥水格、只放行它 → **左鍵**點一下藥水（自動進藥水格＝鍵 1）→ 按 B 關背包 → **強制喝藥**（鎖移動、只放行按 1）→ 喝下一瓶 → 開啟傳送點 1/2/3、教學結束。

藥水＝既有道具 **201「小回血瓶」**（`ItemTable` 201，`HealHp=10`，`Category=Potion`）。pickup 給 2 瓶，喝掉 1 瓶還留 1 瓶。

## 流程與所在程式

寫死在 `Assets/Scripts/UI/TutorialManager.cs`（與佛燈教學同檔、共用面板）。新增 phase：`PotionGuidePick → PotionForceF → PotionOpenBag → PotionClickBind → PotionCloseBag → PotionDrink → Done`。提示字串走 `Language.GetText(1007–1010)`（見 [LOCALIZATION.md](LOCALIZATION.md)）。

- **啟動**：`onEnter`「進場觸發-新手教學」（gate `!tutorialHpPosition`）接 `drama`「新手教學-邪佛提示拾取藥水」（dramaId 21）。drama **關閉時**廣播 `新手教學-邪佛提示拾取藥水` → `BeginPotionTutorial`（`PotionStartTrig`）。
- **撿取**：pickup「新手教學-hp藥水拾取點」（`itemId=201`、`count=2`、`toRealBag=true`）。走到可撿範圍→定住只能按 F；撿完廣播 `新手教學-hp藥水拾取點`（`PotionPickupTrig`）+ `setFlag=tutorialHpPosition`。
- **放進藥水格**：`AllowBag` 放行 B 開背包 → 手指指 HP 藥水格（`FindGridSlotRect(201)`）＋遮罩只放行它 → **左鍵**點一下 → `InventoryPanel.OnSlotClicked` 對藥水呼叫 `AutoPlacePotion`（空位優先＝左格＝鍵 1）→ 偵測 `GetPotionSlot(0)==201` → 提示按 B 關背包。
  > 2026-08-07 背包改成雙頁籤＋分頁後，**藥水在「消耗品」頁籤**。`FindGridSlotRect` 會自動切到正確的頁籤與頁數再回傳格子，手指照樣指得到（同 [WOODSHED_LAMP_TUTORIAL.md](WOODSHED_LAMP_TUTORIAL.md) 的說明）。
- **強制喝藥**（`PotionDrink`）：關背包後 `StartPotionDrink` **解除全鎖**（否則藥水熱鍵被 `IsGameplayInputBlocked` 擋）**但改上 `DrinkOnly=true`＝鎖移動、只放行按 1**。記下當前瓶數 `_potionBaseline`；`TickPotionDrink` 偵測瓶數下降（喝了一瓶）→ 解鎖、`TriggerChain.Activate("新手教學結束開啟傳送點")` 開三個傳送點、完成。

> **為什麼喝藥要用 `DrinkOnly` 而不是 `SetExternalHold`**：`PotionHotkeys` 開頭 `if (UIManager.IsGameplayInputBlocked) return;`——`SetExternalHold(true)` 會把按 1 一起擋掉。所以比照佛燈點亮那步的 `FireOnly`，做一個「鎖移動、但不擋熱鍵」的 `DrinkOnly`（`PlayerController.Update` 讀它把 `_moveInput` 歸零、不開火；`PotionHotkeys` 因 `IsGameplayInputBlocked` 為 false 照常運作）。

## ⚠️ 寫死清單（改動前必看，`TutorialManager.cs` 上方常數）

| 寫死的東西 | 值 | 常數 |
|---|---|---|
| 啟動教學的觸發點名（drama 關閉時開始） | `新手教學-邪佛提示拾取藥水` | `PotionStartTrig` |
| 藥水 pickup 觸發點名 | `新手教學-hp藥水拾取點` | `PotionPickupTrig` |
| HP 藥水道具 id | `201` | `PotionItemId` |
| 撿走藥水旗標（＝pickup `setFlag`；也是「做過」gate） | `tutorialHpPosition` | `PotionTakenFlag` / `PotionDoneFlag` |
| 喝完要 Activate 的開門觸發名 | `新手教學結束開啟傳送點` | `PotionPortalTrig` |

## 地圖佈置（`RedBridalGown_Storeroom.dipanmap`，現況）

- **pickup**「新手教學-hp藥水拾取點」：`itemId=201`、`count=2`、`requireFlag=!tutorialHpPosition`、`setFlag=tutorialHpPosition`、**`toRealBag=true`**（起始/教學道具必加，否則只進關卡臨時包、當場無法開背包用）。
  - **F 感應格是 3×2 一塊**（`[12-14, 2-3]`），不是只放在櫃子那格。因為藥水櫃 `furniture_storage_rack` 是**實心擋路物**（`walkable:false`），玩家會被擋在櫃子前、進不了「只放櫃子那格」的 1.2 感應半徑（`InteractionManager.pickupRadius`，量到最近格中心）。把感應格延伸到櫃子**前方(下方)那排可站的地板**，玩家一走到櫃子前最近格就在半徑內。**通則**：pickup 放在實心家具上時，感應格要含前方可站的地板格，不要只放在家具那格（比全域調大 `pickupRadius` 乾淨，不會連帶放寬撿地上物/傳送點的距離）。手指仍指 6 格中心＝櫃子前緣。
- **onEnter**「進場觸發-新手教學」：`requireFlag=!tutorialHpPosition`、**`next=新手教學-邪佛提示拾取藥水`**（沒有 next 的話 drama 不會播、教學不啟動——這是原本卡住的主因）。
- **drama**「新手教學-邪佛提示拾取藥水」：`dramaId=21`、`requireFlag=!tutorialHpPosition`。
- **togglePortal**「新手教學結束開啟傳送點」：`target=傳送點1,傳送點2,傳送點3`、`show=true`、`requireFlag=tutorialHpPosition`。**不靠 pickup 的 next 觸發，改由程式在喝完藥那一刻 `TriggerChain.Activate` 執行**（這樣才是「喝完才開」，而非「撿到就開」）。
- **傳送點 1/2/3**：`startDisabled=true`、`enableFlag=tutorialHpPosition`（撿過就記住、重進房間直接開；本趟則由上面的 Activate 開）。

> ❓「柴房佛燈拾取是不是寫死在程式？」——**不是**。它是地圖上一顆真正的 `pickup` trigger；程式只是「監聽一個叫這名字的 trigger 廣播」來推進教學 UI（`LampPickupTrig`/`PotionPickupTrig` 常數）。柴房的開門是 pickup 的 `next` 鏈到一顆 togglePortal（撿到就開）；儲藏室要「喝完才開」，所以改成由程式 `Activate` 那顆 togglePortal。

## 與佛燈教學共用/新增的通用能力

- 共用：pickup `toRealBag`、地上物/互動點手指（`FindGridSlotRect`、`TryGetPickupWorld`、`PlayerNearPickup`）、四個教學面板、`Language.GetText`。
- 新增 **`TutorialManager.DrinkOnly` ＋ `PlayerController` 分支**：鎖移動、不開火、放行藥水熱鍵。做「強制喝一次藥」用（`FireOnly` 的藥水版）。
- **藥水左鍵綁定**（2026-07-22 改）：`InventoryPanel.OnSlotClicked` 對藥水改呼叫 `AutoPlacePotion`，讓藥水跟裝備一樣「左鍵點＝自動進格」。見 [INVENTORY.md](INVENTORY.md)。
- ⚠ **右鍵現在會直接喝掉**（2026-08-19 起，見 [INVENTORY.md](INVENTORY.md) 的「左鍵 vs 右鍵」）。教學這一步教的是**左鍵綁定**，玩家若在那一步右鍵把唯一一瓶喝掉，教學會卡在等一個永遠不會發生的條件（`GetPotionSlot(i)==201`）。所以 `InventoryPanel.OnSlotRightClicked` 開頭有 `if (TutorialManager.HardLock) return;`——`HardLock` 在「開背包 → 點藥水格 → 關背包」整段都是 true，正好蓋住這個窗口。**動教學的鎖旗標時記得這條依賴。**
