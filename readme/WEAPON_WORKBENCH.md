# 武器工坊（Weapon Workbench）

> 返回 [文件總覽](README.md) ｜ 表格欄位的意義見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)、程式面見 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)

**在 Unity 裡「選外型 → 選模式 → 填效果 → 立刻射出去看」的一站式武器編輯器。** 編輯的就是 `WeaponTable.csv`／`RecipeTable.csv` 本身，Play 中改任何值下一發就是新的；珠子用真鑲的測。
Editor-only：`Assets/Editor/WeaponWorkbench.cs`，不進 build。

## 一、怎麼用

1. **開視窗**：`Project Tools → 武器工坊 (Weapon Workbench)`。建議把它停靠在 Game 視窗旁邊。
2. **進 Play**：用 `Project Tools → 測試 → 直接進關卡 → 競技場` 進場最快（不用走標題流程）。視窗頂部會出現「模擬」列。
3. **挑一把武器或按「＋新增」**（也可「複製」現有的當模板）。
4. **武器區**：名稱、配方下拉（或「＋新配方」）、**外型下拉**（掃 `Resources/Weapon/`：單張圖與序列圖都列、有縮圖；選序列會自動填張數、`AnimFPS` 空的話補 12）、傷害／耗魔／特效下拉（`VfxTable` 名稱）／光束外觀下拉（種類、顏色名稱）。**只顯示對目前模式有效的欄**。
5. **配方區**：`Mode` 下拉（中文＋英文，下面一行說明這模式怎麼運作）。換模式後只會看到該模式吃的欄，依 `WeaponModeSpec` 分群；每欄旁邊灰字寫「空＝預設、範圍」，填錯格式或超出範圍立刻紅字。`GroundEffectID`／`SubRecipeID`／`SubWeaponOnHit`／特效 ID 全是名稱下拉，不用記數字。★ 是該模式必填。換模式後表上還留著對新模式無效的值會列出來、一鍵清掉。
6. **改了就自動套用**（預設開）：0.25 秒後重建一把臨時武器塞進 `WeaponManager.SimulationOverride`，按左鍵／空白鍵射出去的就是它——**不用裝備、不用存檔**。狀態列會寫「模擬中：xxx」或錯誤。「解除模擬」回到背包裝的武器。
7. **珠子**：「給每種珠子 Lv3」＋「裝備中武器開 6 孔」（同作弊面板「鑲嵌」分頁），然後遊戲裡按 **Y** 開鍛造真的把珠子拖進孔——模擬武器會吃到（它跟正常武器一樣過鑲嵌解析；鍛造介面的「這顆珠對此武器無效」提示在模擬中也是以模擬武器為準）。⚠ 模擬武器本身不占武器欄，珠子要鑲在**背包裝備的**武器或防具上，所以先用 L 面板拿一把任意武器裝著。
8. **儲存**：把整張 `RecipeTable.csv`／`WeaponTable.csv` 寫回（表頭與 `#` 分組註解由 `WeaponModeSpec` 產生，保證跟程式一致；列依 ID 排序）。⚠ **存檔 ≠ 套用**：執行中的遊戲 `Awake` 只載一次，存了之後下次 Play 才生效；模擬武器不受影響。「重新載入」丟掉未存的修改。
9. **底部驗證區**：`WeaponModeSpec.Validate` 的結果（無效欄有值／必填缺）、數值格式與範圍、佛光指到的地面特效 `Duration` 是不是 -1、`ItemTable` 有沒有指到這把武器的物品列（沒有就不能裝備——工坊**不會**幫你產 ItemTable 列，要上線時自己加）。

## 二、範例：做一把「打中敵人會起火的飛劍」

新增 → 外型選 `單張 Weapon › single › weapon_sword` → `SpriteAngleOffset` 填 135（劍尖朝右上）→ 配方 `Mode` 留一般子彈 → `Speed 20`、`PierceCount 1` → `GroundEffectID` 下拉選「1 火焰燃燒」→ `HitEffectID` 下拉選一個爆點 → 射一發。要三分裂就 `SpreadCount 3`、`SpreadAngle 60`；要追蹤 `HomingTurnSpeed 180`。滿意了按儲存。

## 三、它動到哪些程式

| 檔案 | 角色 |
|---|---|
| `Assets/Editor/WeaponWorkbench.cs` | 視窗本體。讀 CSV 成「欄名 → 值」字典、依 spec 畫欄位、存檔、Play 模擬、珠子按鈕 |
| `Scripts/Weapon/WeaponModeSpec.cs` | 欄位規格（型別／預設／範圍／分組／顯示名）與模式 × 欄位表；`HeaderCells`／`GroupCommentLines` 產 CSV 表頭 |
| `Scripts/Data/CsvTable.cs` | `CsvWriter`：引號跳脫、組整份檔（round-trip 測過：讀 → 寫 → 讀，33 列字典完全相等） |
| `Scripts/Weapon/RecipeManager.cs`／`WeaponManager.cs` | `CreateTransient`（臨時建、不登記）、`All`、`SimulationOverride`（過 `AbilityResolver`，鑲嵌有效） |
| `Scripts/Weapon/GemEffectiveness.cs` | 模擬中「參考武器」一律是模擬武器 |

視窗的欄位清單、下拉內容、必填、範圍**全部來自 `WeaponModeSpec`**——加新欄或新模式只改那個檔，視窗自動跟上，不要在視窗裡另外寫一份。

## 四、刻意不做的

- **不產 `ItemTable` 列**：只提示。物品列牽涉 icon、分類、tooltip 文案，作者自己填。
- **不做勾選式的假鑲嵌**：作者要真鑲（會隨機亂鑲，勾選項目做不完）。
- **不刪武器／配方**：刪除牽動 ItemTable／SubRecipeID／SubWeaponOnHit 的引用，先用 git 或手動改 CSV。
- **不熱重載表格**：存檔後執行中的 `RecipeManager`／`WeaponManager` 不重讀（怪物手上持有 `WeaponData` 參照，熱換有風險）；模擬機制已經涵蓋「立刻看」的需求。

## 五、已知限制

- 外型只掃 `Resources/Weapon/`；圖放別處要手填路徑（欄位仍可直接改 CSV）。
- 序列圖偵測靠檔名結尾 `_NN`（兩位以上數字）。
- `SubRecipeID` 指向「還沒儲存的新配方」時，模擬會印一條 `[RecipeTable] SubRecipeID 不存在` 的 Error（執行中的表沒有它）——先存檔、重進 Play 就好。

*建立於 2026-08-26（接在 RecipeTable 大改之後）。*
