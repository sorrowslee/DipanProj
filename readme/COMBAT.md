# 戰鬥傷害系統 (Combat System：HP/MP ＋ 中央傷害管線)

> 返回 [文件總覽](README.md)
>
> 受擊反應（閃爍/擊退/無敵）見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)；武器傷害欄位見 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)；可破壞地上物見 [DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md)；存檔見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)；HUD 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)。
>
> **狀態：✅ 地基 + 掛勾完成（2026-06-25）、待 Unity 實機驗證。** 玩家 HP/MP、中央傷害結算、武器耗魔、怪物接觸傷害、血/魔 HUD、存檔皆已接。傷害加成／減傷／DOT 為「掛勾就位、數值之後接 CSV/buff」。

把「武器傷害 → 玩家加成 → 怪物減傷 → 無敵判定 → DOT/debuff」統一收進**一個地方**結算，並給玩家加上 **HP（血量）** 與 **MP（魔力）**。設計沿用專案紀律：傷害「數值修正」集中在 `CombatSystem`，**目標不自己算加成、彈道系統更不算傷害**（同「彈道不算傷害」「UI 純呈現」哲學）。

---

## 1. 三個核心

| 元件（`Assets/Scripts/Combat/`） | 角色 |
|---|---|
| `DamageInfo`（struct）＋ `DamageType`（enum）＋ `ICombatModifiers`（介面） | 一次傷害的完整描述（來源/目標/原始傷害/方向/類型/是否DOT/是否真實傷害）；取代過去到處傳的「裸 float」 |
| `CombatSystem`（static） | **中央傷害結算的唯一入口**：`Apply(DamageInfo)`。所有傷害都從這裡走一遍 |
| `CombatStats`（MonoBehaviour） | 一個戰鬥單位的「數值層」：HP＋MP＋回復＋加成/減傷掛勾＋事件。目前掛在玩家身上 |

### `CombatSystem.Apply` 的管線（順序）
1. **攻擊方加成**：讀來源（`info.Source`）身上的 `ICombatModifiers.OutgoingDamageMultiplier`（沒有 = ×1）。
2. **受擊方減傷／抗性**：讀目標身上的 `ICombatModifiers.IncomingDamageMultiplier`（沒有 = ×1）。
3. （未來）暴擊、屬性克制等——`DamageInfo.Type` 已帶著走，留待擴充。
4. 交給目標的 `IDamageable.TakeDamage(最終傷害, 方向)` 結算——**無敵時間 / 白光閃爍 / 擊退仍由各目標自己的 `HitReactionHandler` 處理**（沿用既有機制，不重做）。

> `DamageType.True` 或 `IgnoreModifiers = true` → 跳過 1、2 的修正（真實傷害）。

### 為什麼是「層在 IDamageable 之上」而非重寫
專案早就有一條**單一傷害漏斗**：所有武器（子彈/雷射/拋物線/連鎖/雷擊/地面特效）最後都打到 `IDamageable.TakeDamage`（怪物 `MonsterController`、可破壞物 `DestructibleObject` 實作它）。本系統**不重寫**這條路，而是在它前面插一個 `CombatSystem`：算完數值修正後才呼叫 `IDamageable.TakeDamage`。所以既有武器程式幾乎不動、無敵時間照舊運作。

---

## 2. HP / MP（玩家）

`CombatStats` 掛在玩家身上，持有 HP、MP、回復速率、加成/減傷掛勾，**任何變動發事件**（`OnHealthChanged` / `OnManaChanged` / `OnDeath`）讓血/魔條訂閱重繪（同 InventorySystem 的資料/呈現分離）。

* 在 `PlayerController` 的 Inspector 調：`PlayerMaxHealth`（預設 100）、`PlayerMaxMana`（預設 50）、`HealthRegenPerSec`（預設 0）、`ManaRegenPerSec`（預設 5）。
* `PlayerController` 現在實作 `IDamageable`——所以**玩家是可被傷害的目標**（怪物接觸、未來陷阱/DOT 都打得到）。受傷時：先過 `HitReactionHandler`（無敵中則完全忽略），再 `CombatStats.ApplyHealthDelta(-傷害)`；血量歸零 → `OnDeath` → `PlayerController.Die()`（目前僅標記死亡，重生/讀檔流程之後接）。
* 怪物維持**輕量 HP**（`MonsterController` 自管，不掛 `CombatStats`），只實作 `ICombatModifiers` 提供減傷掛勾。

### MP（魔力）＝武器發射的消耗
* `WeaponTable.csv` 新增 **`ManaCost` 欄**（第 17 欄）；**留空 / 缺欄 = 1**。目前所有武器都填 1。
* **離散武器**（直射/環繞/拋物線/連鎖/雷擊…）：每發射一次扣一次 `ManaCost`，**不夠就不發射**（不重置射速計時、不播發射特效）。
* **持續武器**（雷射/火焰噴射器/佛光）：**啟動瞬間扣一次、之後每秒扣一次**（`PlayerController.DrainContinuousMana`）；魔力耗盡 → 自動關閉光束 / 銷毀佛光。
* MP 回復由 `ManaRegenPerSec` 每幀回補。

---

## 3. 加成 / 減傷（掛勾就位，數值之後接）

依使用者規劃「加成跟減傷目前都還沒做，但預期會做」——所以**管線與欄位都備好，數值先給中性值**：

| 修正 | 在哪 | 目前值 | 之後怎麼接 |
|---|---|---|---|
| 玩家攻擊加成 | `CombatStats.OutgoingDamageBonusPercent`（百分比） | 0（×1） | 由裝備/buff/CSV 改這個值；`OutgoingDamageMultiplier` 自動換算 |
| 玩家受擊減傷 | `CombatStats.IncomingDamageReductionPercent` | 0（×1） | 同上；夾在 0~90% |
| 怪物受擊減傷 | `MonsterController.DamageReductionPercent` ＋ `MonsterData.csv` 的 `DamageReduction` 欄 | 0（×1） | 在 CSV 填百分比即可（已可讀） |

> 因為修正集中在 `ICombatModifiers`，**之後做護甲、屬性抗性、易傷 debuff，只要改這些單位回傳的乘數**，不必動 `CombatSystem` 或任何武器程式。

---

## 4. 怪物接觸傷害（怪碰到玩家就扣血）

* 元件 `EnemyContactDamage`（`MonsterController` 在 Start 自動掛上、用 `MonsterData` 的 `ContactDamage` 設定）。
* **關鍵陷阱**：專案的 Layer Collision Matrix 把 **Enemy×Player 關閉**（怪物穿過玩家、不互推），所以 `OnCollision` / `IsTouching` 都**不會**回報。改用 `Physics2D.Distance(怪collider, 玩家collider)` 做**幾何重疊判定**（不受碰撞矩陣影響）。
* 反覆接觸由**玩家自己的無敵時間**（`PlayerInvincibleTimeMs`，預設 1000ms）節流：碰到扣一次血 → 進入無敵 → 期間貼著也不再扣。
* 傷害一律走 `CombatSystem.Apply(怪物, 玩家, ContactDamage, 方向)`，所以吃玩家減傷、未來也吃怪物攻擊加成。
* `MonsterData.csv` 新增 `ContactDamage`（留空 = 10）、`DamageReduction`（留空 = 0）兩欄。

---

## 5. DOT / debuff（持續扣血）

* 地面特效（火堆/毒霧等，見 [GROUND_EFFECT.md](GROUND_EFFECT.md)）的範圍傷害現在也走 `CombatSystem.Apply(..., isDot: true)`——所以**減傷/抗性對 DOT 一樣生效**。
* `DamageInfo.IsDot` 旗標已備好，供未來區分「DOT 的 i-frame 規則」「debuff 專屬表現」用。
* 完整的「狀態效果（中毒/燃燒/流血）框架」尚未做——目前 DOT＝地面特效的週期傷害。要做獨立 debuff 系統時，讓它每拍呼叫 `CombatSystem.Apply(..., isDot:true)` 即可接上同一條管線。

---

## 6. 血 / 魔 HUD

* `HudPanel`（`Assets/Scripts/UI/Panels/HudPanel.cs`，HUD 層、**不暫停、不擋輸入、不遮罩**）：左上角 HP（紅）＋ MP（藍）兩條，每幀讀玩家 `CombatStats` 的當前/上限設定條寬與數字。玩家死亡/換地圖重生會自動重找。
* 由 `PlayerController.Start` 開啟（`UIManager.Open<HudPanel>()`）。全程式建構、零接線，建在 [UI_SYSTEM.md](UI_SYSTEM.md) 的 UI 框架上。
* 調整：條的大小/顏色/位置常數在 `HudPanel.cs` 上方（`BarWidth/BarHeight/Gap/HpColor/MpColor`）。

---

## 6.5 浮動傷害數字（頭上 -N）

被打到時在頭頂跳出「-3」往上飄、彈一下再淡出。玩家與怪物共用。

* 元件：`Assets/Scripts/Combat/DamageNumberManager.cs`（懶漢單例、自動生成、零 prefab，風格同 VfxManager）＋ `DamageNumberInstance.cs`（單一數字的上升/pop/淡出動畫）。
* **世界座標 TextMesh**（不走 Canvas），共用 UI 的內建字型（`UIBuilder.DefaultFont`），畫在角色/特效之上（`sortingOrder` 600）。
* **觸發點＝「確定吃到傷害之後」**：在 `MonsterController.TakeDamage` 與 `PlayerController.TakeDamage` 過了無敵判定（`HitReactionHandler.TryHitReaction`）才呼叫 `DamageNumberManager.Show(gameObject, amount)`。所以**被無敵時間擋掉的攻擊不跳數字**，且數字 = **減傷後的最終傷害**（CombatSystem 已先算完修正）。
* 顏色自動分辨：目標是玩家（tag `Player`）→ **紅**（受傷）；其他（怪物）→ **暖黃**（打到怪）。數字獨立於目標存在，怪物被打死的最後一下數字照樣飄完（不會隨怪物銷毀消失）。
* 調整：大小/壽命/上升速度/顏色/排序常數在 `DamageNumberManager.cs` 上方（`CharacterSize/FontSize/Lifetime/RiseSpeed/SortingOrder/PlayerHurtColor/EnemyHurtColor`）。
* 泛用入口：`DamageNumberManager.ShowAt(worldPos, amount, color)`——之後要對家具/其他來源跳數字、或做「治療綠字」「暴擊大字」都複用這個。

---

## 7. 存檔

* `StatsDTO`（`CharacterSave.stats`）新增 `maxHealth / health / maxMana / mana` 欄。
* `SaveManager`：玩家在 Start 時 `BindPlayerStats(_stats)` 註冊、OnDestroy 時 `UnbindPlayerStats`。存檔時把玩家 HP/MP 寫進 `stats`；載入角色時還原回玩家。
* **`maxHealth == 0` 視為「沒存過」**（新角色）→ 還原時不覆蓋玩家初始的滿血滿魔。所以新角色照 Inspector 滿血滿魔，舊存檔則沿用上次數值。
* 玩家不是常駐單例（每場景生成），故用 `SaveManager` 的**靜態欄位**橋接（同檔案 IO 邊界：資料層不認識檔案）。

---

## 8. 怎麼加一個新東西（速查）

* **新武器要不同耗魔** → 在 `WeaponTable.csv` 那列填 `ManaCost`（留空 = 1）。
* **某怪很硬（減傷）** → 在 `MonsterData.csv` 填 `DamageReduction`（百分比）。
* **某怪很痛（接觸傷害）** → 在 `MonsterData.csv` 填 `ContactDamage`。
* **玩家裝備加攻擊/減傷** → 改玩家 `CombatStats.OutgoingDamageBonusPercent` / `IncomingDamageReductionPercent`（之後接裝備系統時在那裡寫入）。
* **新傷害來源**（陷阱、新 AOE…） → 呼叫 `CombatSystem.Apply(來源, 目標, 傷害, 方向)` 即可，自動吃所有修正與無敵判定。**不要**自己 `GetComponent<IDamageable>().TakeDamage`。

---

## 9. 相關檔案

* `Assets/Scripts/Combat/DamageInfo.cs` — `DamageInfo` / `DamageType` / `ICombatModifiers`
* `Assets/Scripts/Combat/CombatSystem.cs` — 中央傷害結算
* `Assets/Scripts/Combat/CombatStats.cs` — HP/MP 數值層（事件、回復、加成/減傷掛勾）
* `Assets/Scripts/Combat/EnemyContactDamage.cs` — 怪物接觸傷害（幾何重疊判定）
* `Assets/Scripts/Combat/IDamageable.cs` — 低階受傷介面（怪物 + 可破壞物，未改）
* `Assets/Scripts/UI/Panels/HudPanel.cs` — 血/魔 HUD
* `Assets/Scripts/Combat/DamageNumberManager.cs` / `DamageNumberInstance.cs` — 頭上浮動傷害數字
* `Assets/Scripts/PlayerController.cs` — 實作 IDamageable、接 CombatStats、MP 耗魔、傷害改走 CombatSystem
* `Assets/Scripts/AI/MonsterController.cs` — 實作 ICombatModifiers（減傷）、掛接觸傷害
* `Assets/Scripts/AI/MonsterData.cs` / `MonsterSpawner.cs` — `ContactDamage` / `DamageReduction` 欄
* `Assets/Scripts/Weapon/WeaponData.cs` / `WeaponManager.cs` — `ManaCost` 欄
* `Assets/Scripts/Combat/GroundEffectInstance.cs` — DOT 改走 CombatSystem
* `Assets/Scripts/Save/CharacterSave.cs` / `SaveManager.cs` — `StatsDTO` HP/MP + 存讀
* 資料表：`Assets/Data/WeaponTable.csv`（`ManaCost`）、`Assets/Data/MonsterData.csv`（`ContactDamage` / `DamageReduction`）

---

## 10. 待辦 / 之後可接

- [ ] 玩家死亡流程（`PlayerController.Die`）：重生 / 讀檔 / 結束畫面。
- [ ] 加成/減傷的實際來源：裝備屬性、buff、CSV 數值（管線已就位）。
- [ ] 獨立狀態效果（debuff）框架：中毒/燃燒/流血計時器，每拍走 `CombatSystem.Apply(isDot:true)`。
- [ ] 屬性克制 / 抗性：用 `DamageType` 做表。
- [ ] 暴擊（在 `CombatSystem` 管線加一步、來源 `ICombatModifiers` 提供暴擊率）。
- [ ] 回血/回魔道具、MP 不足的玩家提示（可用 `AlertPanel.Toast`）。
- [ ] 怪物 HP 也想要事件/條時，再決定要不要讓怪物也用 `CombatStats`。

---

*建立於 2026-06-25：玩家 HP/MP（CombatStats）＋ 中央傷害結算（DamageInfo/CombatSystem，所有武器與地面特效改走它）＋ 武器耗魔（WeaponTable ManaCost）＋ 怪物接觸傷害（EnemyContactDamage，幾何重疊）＋ 怪物減傷掛勾（MonsterData DamageReduction）＋ 血/魔 HUD（HudPanel）＋ 存檔（StatsDTO HP/MP）。加成/減傷/DOT 為掛勾就位、數值之後接。待 Unity 實機驗證。*
