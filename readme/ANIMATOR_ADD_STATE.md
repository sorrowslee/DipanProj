# Animator 新增一個狀態（速查）

> 給角色加一個新動畫狀態（死亡 / 攻擊 / 受傷…）的最短流程。
> 角色整體設定見 [CHARACTER_SETUP.md](CHARACTER_SETUP.md)。

兩種狀態先分清楚（決定怎麼連線）：

- **持續型**（走路那種，按住就播、放開就回）→ 用 **Bool**，狀態間互相 Make Transition 來回切。
- **一次性**（死亡那種，發生就定住）→ 用 **Bool**，從 **Any State → 新狀態**，且新狀態不連出去。

---

## A. 開啟 Animator

- Project → `GameAssets/Main/Characters/Animations/` → **雙擊 `Actor1.controller`**。

## B. 先做動畫 clip

### B-1. 單張圖（定格，如死亡）

- 把圖丟進 `…/Characters/SingleImage/`，Inspector 設 **Sprite Mode = Single、PPU = 250**、Apply。
- Hierarchy → **Create Empty**，命名 `tmp`，選起來。
- 選單 **Window → Animation → Animation**（有時間軸的那個）。
- 視窗中央按 **Create** → 存成 `Actor1_Xxx`（放 Animations 資料夾）。
- 把那張 sprite 從 Project **拖進時間軸空白格** → 第 0 格一個關鍵格。
- ⚠️ 單張**不能**用「拖到物件上自動生 clip」，一定走這條。

### B-2. 序列圖（多格，如連續動作）

- 把圖丟進 `…/Characters/SequenceImage/`，Inspector 設 **Sprite Mode = Multiple、PPU = 250** → **Sprite Editor → Slice：Grid By Cell Count、Column = 格數、Row = 1** → Slice → Apply。
- Hierarchy → **Create Empty**，命名 `tmp`。
- Project 展開該圖，**框選全部子 sprite**（`_0`→最後一格）→ **拖到 `tmp` 上** → 跳存檔 → 存成 `Actor1_Xxx`。

### B-3. 兩種都要做

- 點 `Actor1_Xxx.anim`，Inspector：**循環就勾 Loop Time、一次性（死亡）就取消 Loop Time**。

## C. 把 clip 變成 Animator 的狀態

- 回 Animator（`Actor1.controller`）→ 把 `Actor1_Xxx.anim` **拖進空白格** → 生出新狀態。
- 不要讓它變橘色（預設）；若變橘了，在 `Idle` 右鍵 → **Set as Layer Default State**。

## D. 加參數 + 連線

- 左上 **Parameters** → **+** → **Bool** → 命名（例 `isDead`，全小寫、與程式一致）。
- **一次性狀態**：**Any State** 右鍵 → Make Transition → 點新狀態。
- 選那條線，Inspector：
  - Conditions **+** → 參數 = **true**
  - **取消 Has Exit Time**
  - **Transition Duration = 0**
  - **取消 Can Transition To Self**
- 新狀態**不要**拉任何往外的箭頭（一次性才這樣；持續型則要連回去）。

## E. 程式觸發（一行）

在要觸發的時機呼叫（參數名一字不差）：

```csharp
_animator.SetBool("isDead", true);   // 死亡：在 PlayerController.Die() 內
```

> 持續型則依條件每幀設 true/false（如走路：`_animator.SetBool("isMoving", 速度>0.1f)`）。

## F. 清垃圾

- 刪 Hierarchy 的 **`tmp`**。
- 刪 Animations 資料夾裡**多生出來的控制器**（名字是 `tmp` 之類）。
- ⚠️ **別刪** `Actor1.controller` 與 `Actor1_Xxx.anim`。

---

## 不動的話，逐項對

- [ ] 參數是 **Bool**、名字**和程式 `SetBool("...")` 一字不差**。
- [ ] 線的 Conditions = true、**Has Exit Time 取消**、**Can Transition To Self 取消**。
- [ ] 一次性狀態沒有往外的箭頭。
- [ ] 程式真的有呼叫到 `SetBool`（Console 印得出對應 log）。
- [ ] **驗證法**：Play 時在 Parameters 手動打勾該 Bool → 會切就代表 Animator 沒問題，問題在程式那行有沒有跑到。

---

*建立於 2026-06-25：Animator 新增狀態速查（單張用 Animation 視窗 Create、序列圖用框選多格拖到物件；一次性 = Bool + Any State→狀態 + 取消 Has Exit Time / Can Transition To Self，狀態不連出去）。*
