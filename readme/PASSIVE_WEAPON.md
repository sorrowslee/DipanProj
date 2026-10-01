# 被動武器掛載系統（Passive Weapons）

> 返回 [文件總覽](README.md)
> 相關：[RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)〈Familiar 浮游〉（被動武器本身怎麼填）、[GEM_SOCKET.md](GEM_SOCKET.md)（珠子怎麼疊到它身上）、[BLOODLINE.md](BLOODLINE.md) 表B、[INVENTORY.md](INVENTORY.md) ItemTable 欄位。
>
> **狀態：2026-10-01 程式完成、⏳ 未編譯未實測。**

---

## 0. 一句話

**被動武器＝「裝備著就自己運作、沒有扳機」的武器。它不佔當前武器，所以可以跟主武器並存，而且任何來源都能掛：
護身符、戒指、胸甲、血統……只要在那張表填 `PassiveWeaponIds`。**

目前唯一的被動型模式是 **`Mode=Familiar` 浮游**（應龍水珠：本體繞身、自動朝最近的怪射子彈）。

---

## 1. 兩種「能力」，只有被動型能嫁接

配方表上的東西要拆成兩類看：

| | 例子 | 能不能掛到護身符／血統上 |
|---|---|---|
| **修飾型**（數值／旗標） | 反彈次數、傷害、射速、環繞數量 | **能**——這是珠子系統（`PlayerAbilities`）早就做好的事：珠子鑲在哪件裝備上都是全身累加 |
| **行為型**（`Mode`：這把武器怎麼出手） | 雷射、拋物線、近戰、召喚 | **不能**——主動模式綁著扳機、瞄準、攻擊動作，同一時間只能有一把「當前武器」 |
| **行為型的被動子類**（`ModeSpec.Passive = true`） | 浮游 Familiar | **能**——沒有扳機、不瞄準、不擺動作，跟主武器沒有衝突 |

所以「應龍水珠放在戒指上」做得到，「雷射放在戒指上」刻意不做。

---

## 2. 資料：一個欄名 `PassiveWeaponIds`，出現在兩張表

| 表 | 欄位位置 | 意義 | 目前填了誰 |
|---|---|---|---|
| `ItemTable.csv` | 第 19 欄（index 18） | 裝備這件就帶著 | **503 應龍護身符**（`EquipSlot=Amulet`，填 `72`＝WeaponTable 的應龍水珠） |
| `BloodlineTable.csv` 表B | 第 27 欄（index 26） | 升到這個血統就帶著 | **62 應龍**（填 `72`） |

- 值＝**WeaponTable 的 ID**，**分號分隔**可填多把（`72;73`）。CSV 不能用逗號。
- **任何可裝備列都能填**（護身符、戒指、胸甲、手套、鞋子、甚至武器——「這把劍附帶一顆水珠」）。
- 指到的武器必須是被動型模式；不是的話載入後第一次重算會印 Warning 並略過（表載入時只切字串、不驗證，因為那時 WeaponTable 可能還沒載好）。
- 舊列不補值：`ItemDatabase.Field`／`CsvUtil.Field` 讀不到就回空字串＝沒有。

被動武器**本身**（外觀、傷害、耗魔、配方）仍然完整住在 `WeaponTable`＋`RecipeTable`，**刻意不另開一張表**——
再開一張只會把同一件事定義兩次。武器工坊 Play 中照樣能調（見 §5）。

> 什麼時候才該開獨立表：出現「不是武器」的被動（每秒回血、踩地留火…）。到時以「類型＋參數」包一張 `PassiveTable`，
> 武器型是其中一種，掛載欄改指它的 ID——欄名與收集流程都能沿用。

---

## 3. 來源與疊加

`Scripts/Weapon/PassiveWeaponSet.cs` 每次重算收集三種來源，**全部收、不互斥**：

```
① 工坊模擬／劇情覆寫的武器是被動型   ← 工坊 Play 中才調得到浮游；夢境武器也能給被動。**背包武器欄不算**（見下）
② 所有裝備欄物品的 ItemData.PassiveWeaponIds
③ 當前血統 BloodlineDef.PassiveWeaponIds
每一把 → WeaponManager.AbilityResolver（吃珠子）→ 玩家專屬拷貝 → WeaponFamiliar 一組
```

- **被動武器不能裝在武器欄**（作者 2026-10-01 拍板）：被動武器的道具列 `EquipSlot` 要填護身符／戒指等、`WeaponID` 留空、靠 `PassiveWeaponIds` 掛載。
  表填錯（`WeaponID` 指到被動型武器）時 `PlayerController.OnInventoryChanged` 印警告並視為空手，本體不會出來。
- **同一把掛兩次＝兩組**（護身符一顆水珠＋血統一顆水珠 ⇒ 兩組各自獨立轉、各自射）。不合併、不加數量。
  多組同軌道時每組相位錯開 `360 ÷ (本體數 × 組數)`，四顆剛好均分一圈。
- **珠子影響全身**（作者 2026-10-01 拍板：「這樣鑲嵌珠更重要、程式也不會錯」）：戒指上的群環珠讓**每一把**被動武器的本體都 +1，
  血統給的也吃。有效性仍過 `WeaponModeSpec.IsEffective`（Familiar 在矩陣裡：群環／環距／疾發／銳利／須彌／遠射＋一般子彈那套；聚氣、連擊無效）。
- **不存檔**：清單完全由表推導，`ItemInstance` 裡沒有任何被動武器的狀態。

---

## 4. 執行：三個掛點

| 檔案 | 角色 |
|---|---|
| `Weapon/PassiveWeaponSet.cs` | 收集來源、解析、簽章比對（`NeedsRebuild`／`Rebuild`）、`Describe()` 除錯 |
| `Weapon/WeaponFamiliar.cs` | **一個元件管多組**：每把浮游武器＝一個 `Group`（本體／冷卻／輪替／相位）；`Sync(清單)` 依序對齊、外型沒變不重生、冷卻沿用 |
| `PlayerController` | `PassiveWeapons`（清單；`CanRunPassive` 為 false 回空）、`SyncPassiveWeapons`（簽章比對）、`TickPassiveWeapons`（在 `HandleFiring` **開頭**、所有 guard 之前推進）、`FireFamiliarShot`（真的射那一下） |
| `Weapon/WeaponModeSpec.cs` | `ModeSpec.Passive` 旗標＋`IsPassive(mode)`；**新增一種被動型模式只要在 `BuildModes` 對它 `.AsPassive()`** |

### 重算時機＝每幀比三個值

`PlayerController.SyncPassiveWeapons` 比對 **背包 `LoadoutVersion`＋血統 Id＋`GetCurrentWeapon()` 參照**，任一變了才重收集。
刻意不在 `BloodlineSystem`／`WeaponManager` 各加事件：來源的變動路徑太多（換裝備、改珠子、喝藥、夢境覆寫、讀檔、工坊每幀改值），比對最不會漏。
重算本身便宜（幾個字典查找＋`Resolve`）。

### `CanFire` vs `CanRunPassive`

| | 條件 | 誰看 |
|---|---|---|
| `CanFire` | 有**主動**武器 ＋ 地圖沒禁武。**當前武器是被動型（只會來自工坊模擬／劇情覆寫）⇒ false**（視為空手：按攻擊鍵沒反應、不轉身） | 主動發射 guard、轉身 |
| `CanRunPassive` | 活著 ＋ 地圖沒禁武。**不要求有主武器** | `PassiveWeapons` |

背包開著時本體仍在、只是不射（發射節奏只在 `HandleFiring` 推進，與主武器一致）；卸下裝備、進禁武地圖、死亡、換血統，本體由 `WeaponFamiliar.LateUpdate` 每幀自問清單、當場收掉。

教學「補一發」（`RequestFireOnce`）**不算**被動的射擊——那個機制在等玩家「出手」。

---

## 5. 怎麼加一個新的被動武器（零程式）

1. `RecipeTable.csv` 加一列，`Mode=Familiar`（或之後的其他被動型模式），填好節奏／索敵／子彈欄（見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)〈Familiar 浮游〉）。
2. `WeaponTable.csv` 加一列指向它，填本體三欄 `FamiliarVfxId`（必填、那列 VfxTable 要 `Loop=1`、`Duration=-1`）／`FamiliarSize`／`FamiliarSpin`。
3. 開 **武器工坊**（`Project Tools → 武器工坊`）選它、Play 中調到滿意、存回 CSV——工坊模擬的被動武器會以「當前武器」身分進清單（§3 ①），本體立刻看得到。
4. 決定誰帶著它：`ItemTable.csv` 某件裝備、或 `BloodlineTable.csv` 某個血統，填 `PassiveWeaponIds`。

要做「新的被動型模式」（不是浮游）：`WeaponModeSpec.BuildModes` 加模式並 `.AsPassive()`，執行端比照 `WeaponFamiliar` 接在 `TickPassiveWeapons`——清單那層不用動。

---

## 6. 怎麼測（實機）

1. 作弊面板（L）「給道具」輸入 ID **503** 應龍護身符（「取得所有武器」那顆鈕只給 `EquipSlot=Weapon`，不會給它）＋任一把主動武器。
2. 裝備護身符、劍到武器欄 → 身邊兩顆水珠自動射、左鍵照樣揮劍。
3. 卸下劍只留護身符 → 水珠照射、按左鍵不轉身。
4. 喝到應龍（血統 62）→ **再多一組**兩顆（四顆均分一圈）；護身符拆掉剩兩顆。
5. 戒指鑲 **群環珠** → 每組都 +1 本體；拆掉回來。
6. 進禁武地圖／開背包／死亡：本體收掉或停火。
7. 工坊選 72 調 `OrbitalCount`／`FamiliarSize` → Play 中即時變。

---

## 7. 已知缺口

- [ ] 未編譯未實測（2026-10-01）。
- [ ] 不同組之間不互相錯開（只在組內錯開），四顆水珠偶爾會同幀出兩發。
- [ ] 被動武器的來源（「血統 應龍」「護身符 應龍水珠」）目前只在 `PassiveWeaponSet.Describe()`，沒接到任何 UI／tooltip。

---

*建立於 2026-10-01：浮游從「當前武器的一種 Mode」解耦成被動武器（同日拍板：被動武器不能裝武器欄）；`PassiveWeaponIds` 掛載欄（ItemTable／BloodlineTable）；`PassiveWeaponSet`；`WeaponFamiliar` 多組化；道具 72（武器型的應龍水珠）移除、新增 503 應龍護身符、應龍血統 62 附帶。WeaponTable 72 應龍水珠仍是被動武器的本體定義。*
