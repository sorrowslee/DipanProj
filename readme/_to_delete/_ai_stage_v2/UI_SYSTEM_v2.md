# UI 系統底層 (UI Framework)

> 返回 [文件總覽](README.md)

玩家面向 UI 的底層框架（背包、設定、地圖…的共同地基）。採 **uGUI（Canvas）+ 全程式建構（code-driven）**，跨場景常駐、零手動接線。背包等具體面板是建在這套地基上的「第一個 panel」，本文件只談地基；背包自己的設計另開文件。

技術選型理由：uGUI 是玩家面向 2D 遊戲最穩的 runtime UI；面板全用程式建（不靠 prefab / Inspector 接線），與本專案既有風格一致（VfxManager / LaserBeam 也是全程式建構）。

---

## 核心元件

| 檔案（`Assets/Scripts/UI/`） | 角色 |
|---|---|
| `UIManager.cs` | **大腦**：跨場景常駐單例（`DontDestroyOnLoad`）。開關任何 UI 的唯一入口；管分層 Canvas、視窗堆疊、暫停、輸入閘門、遮罩。 |
| `UIPanel.cs` | **所有面板的抽象基底**：生命週期 + 面板特性旗標 + 淡入淡出。 |
| `UILayer.cs` | 分層列舉：`HUD / Window / Popup / Overlay`（各一個 Canvas，sortingOrder 遞增）。 |
| `UIBuilder.cs` | **程式建構助手**：`Image / Text / Button / SolidPanel / InputField`（2026-06-23 加 InputField）+ RectTransform 錨點/拉伸 + Resources 載圖 + 內建字型。 |
| `UIBootstrap.cs` | `RuntimeInitializeOnLoadMethod` 在開場前自動生出 UIManager，零手動接線。 |
| `Panels/UIDemoPanel.cs` ＋ `UIDemoLauncher.cs` | 測試/範例面板（按 `U` 開關），驗證底層用，背包做好後可刪。 |

### 共用 slot 拖放/搬運系統（2026-06-23）

讓「背包」與「倉庫」用同一套格子互動程式（跨面板拖放互通）。見 [STORAGE.md](STORAGE.md)、[INVENTORY.md](INVENTORY.md)。

| 檔案（`Assets/Scripts/UI/`） | 角色 |
|---|---|
| `ISlotView.cs` | 所有可拖放格子的共同抽象（背包道具格/裝備欄、倉庫格都實作）：`Grid/GridIndex/IsEquip/Equip/DragIcon()/Rt`。 |
| `SlotDragController.cs` | 全域拖曳＋懸浮 ghost；放開時讀 `eventData.pointerDrag` 上的 `ISlotView` → **跨面板（背包↔倉庫）拖放天生互通**。 |
| `InventoryActions.cs` | 純搬運規則（與 UI 無關）：格↔格 放入/合併/交換、格↔裝備欄 裝備/卸下/交換、點擊快速搬。 |
| `ItemSlotWidget.cs` | 通用格子（倉庫頁與任何 `IItemGrid` 用），實作 `ISlotView`＋點擊/拖放/hover。 |
| `StorageBagCoordinator.cs` | 開場自動生成：K 開倉庫、B 開背包；只開一個置中、兩個都開並排（呼叫各面板 `SetPairedLayout`）。 |

---

## 怎麼開關 UI

```csharp
using Dipan.UI;

UIManager.Instance.Open<InventoryPanel>();     // 開（已開則聚焦）
UIManager.Instance.Close<InventoryPanel>();    // 關
UIManager.Instance.Toggle<InventoryPanel>();   // 切換（背包鍵最常用）
UIManager.Instance.CloseTop();                 // 關堆疊最上層（= ESC 的程式版）
bool open = UIManager.Instance.IsOpen<InventoryPanel>();
```

面板**按型別**取用，UIManager 會自動建立一次並快取重用（之後開關只是顯示/隱藏）。

---

## 怎麼寫一個新面板

繼承 `UIPanel`，覆寫想要的特性旗標，在 `OnBuild()` 用 `UIBuilder` 把版面拼出來。範例見 `UIDemoPanel.cs`。

```csharp
public class InventoryPanel : UIPanel
{
    public override UILayer Layer => UILayer.Window;
    public override bool PausesGame => true;        // 開背包暫停遊戲（可改）
    public override bool ShowBackdrop => true;       // 背後鋪遮罩

    protected override void OnBuild()   // 只會被呼叫一次
    {
        var box = UIBuilder.SolidPanel(transform, "Box", new Color(0.1f,0.1f,0.13f,0.97f));
        UIBuilder.Center(box.rectTransform, 900, 600);
        // …用 UIBuilder.Image / Text / Button 繼續拼…
    }

    protected override void OnOpen()  { /* 刷新資料、訂閱事件 */ }
    protected override void OnClose() { /* 退訂事件 */ }
}
```

### 面板特性旗標（在 `UIPanel` 覆寫）

| 旗標 | 預設 | 作用 |
|---|---|---|
| `Layer` | `Window` | 屬於哪層 Canvas |
| `PausesGame` | `false` | 開啟時 `Time.timeScale=0`（**視面板而定**：背包可暫停、HUD 不暫停、商店可不暫停） |
| `BlocksGameplayInput` | `true` | 開啟時擋住玩家移動/射擊（HUD 類請設 `false`） |
| `CloseOnEscape` | `true` | ESC 是否關本面板（僅作用於堆疊最上層） |
| `ShowBackdrop` | `false` | 背後是否鋪半透明遮罩（擋下方點擊、聚焦本視窗） |
| `InStack` | Window/Popup 才 true | 是否納入視窗堆疊（影響 ESC 逐層關閉、最上層判定） |
| `KeepOpenOnSceneChange` | `false` | 切 Unity 場景時是否保留開啟 |
| `FadeDuration` | `0.12` | 淡入淡出秒數（unscaled，暫停時仍會播） |

---

## 防連點工具（`UIPanel` 提供，opt-in）

對話這類「一下就翻過去」的面板，若被猛按會連跳好幾頁。`UIPanel` 基底提供兩支 protected 方法供面板自行套用：

- `BlockInputFor(seconds)` — 在 `OnOpen()` 呼叫，讓面板剛開啟時先擋一段冷卻（避免上一頁的連點慣性吃掉新內容）
- `TryConsumeInput(cooldown = UIPanel.InputCooldown)` — 前進/關閉的入口呼叫，冷卻中回 `false` 就直接 `return`

預設冷卻 `UIPanel.InputCooldown = 0.5f`，用 `Time.unscaledTime`（`PausesGame` 的面板不能用 `Time.time`）。

**基底不會自動套用**——背包、設定那種需要連續操作的面板不該被節流。目前只有 `TalkPanel` 與 `DramaPanel` 使用，詳見 [DRAMA.md](DRAMA.md) 的「防連點」一節。

---

## 設計重點

- **暫停 / 輸入閘門「視面板而定」**：每個面板自己宣告 `PausesGame` / `BlocksGameplayInput`，UIManager 統合——任一開啟面板要求就生效，全部關掉才解除（`Recompute()`）。
- **輸入閘門怎麼接到玩家**：`PlayerController.Update` 開頭查一次 `UIManager.IsGameplayInputBlocked`，為真就清掉移動輸入並 return（最小侵入，沒有重構玩家的 input）。**任何之後要在開 UI 時停手的系統，都查這個靜態旗標即可。**
- **多場景**：UIManager + 分層 Canvas 由 bootstrap 建一次、`DontDestroyOnLoad` 跨場景存活；切場景時自動關掉非常駐面板（實例仍快取重用）。現在是單場景、未來加場景，底層不用改。
- **解耦邊界（沿用專案紀律）**：UI 是**純呈現層**，不直接抓遊戲邏輯。資料層與呈現層分開——背包應有 `InventorySystem`（純資料：有什麼、加減、發 `OnChanged` 事件）；`InventoryPanel` 只訂閱事件重繪、操作時回呼 `InventorySystem`。這跟「彈道不算傷害」「GroundEffect 資料 vs 視覺」是同一套設計哲學。
- **美術走 Resources**：拆分小圖放 `Assets/Resources/...`，用 `UIBuilder.LoadSprite("相對路徑")` 載入（與 `WeaponSpritePath` 等同套慣例）。
- **共用遮罩（2026-06-23 改）**：只有一張 `_backdrop`。`UpdateBackdrop` 改成「只要有任一 Window 層面板要遮罩就鋪一張，並 `SetAsFirstSibling()` 放在所有視窗最底層」。因此**不論同時開幾個 Window 視窗（如倉庫＋背包並排），都只有一層遮罩、永遠在全部視窗後面**——不會卡在兩個視窗之間蓋住下面那個、也不會疊加。（早期「鋪在最上層視窗正下方」的寫法在並排時會蓋黑下面那個，見 [PROBLEMS.md](PROBLEMS.md) D5。）
- **程式建 Button 要手動指 `targetGraphic`**：`UIBuilder.Button` 在執行期建立時 Unity 不會自動指定 `targetGraphic`，需 SpriteSwap/讀 `btn.image` 時要補 `btn.targetGraphic = btn.GetComponent<Image>();`（見 [PROBLEMS.md](PROBLEMS.md) D4）。

---

## 怎麼驗證底層能動

1. 開 Unity（會自動編譯新腳本，Console 無紅錯）。
2. 把 `UIDemoLauncher` 掛到場景任一物件上、按 **Play**。
3. 按 **U**：跳出測試面板、背後變暗、遊戲暫停、玩家不能動；按 **ESC** 或「關閉」鈕收起、遊戲恢復。

---

## 待辦（背包階段才做，底層已預留接口）

- `ItemTable.csv` + `InventorySystem`（純資料層，CSV 驅動，仿 WeaponTable）。
- `InventoryPanel`（繼承 `UIPanel`，依使用者提供的設計圖 + 拆分小圖以 UIBuilder 建構）。
- ✅ 拖放（共用 `SlotDragController`）、✅ tooltip（背包/倉庫各建一份，浮動跟游標）——已完成。
- 待補：格子堆疊分割、HUD（血條/武器/金錢，走 `HUD` 層、不暫停不擋輸入）、（可選）把 tooltip 抽成共用元件。

---

*建立於 2026-06-22：UI 底層框架（uGUI + code-driven，多場景常駐，視面板而定的暫停/輸入閘門）。背包為下一階段，建在本框架上。*
*2026-06-23 更新：加 `UIBuilder.InputField`；新增「共用 slot 拖放/搬運系統」(ISlotView/SlotDragController/InventoryActions/StorageBagCoordinator) 供背包與倉庫互拖；共用遮罩改為鋪在所有視窗最底層（支援並排視窗、不疊加）。見 [STORAGE.md](STORAGE.md)、[INVENTORY.md](INVENTORY.md)。*
