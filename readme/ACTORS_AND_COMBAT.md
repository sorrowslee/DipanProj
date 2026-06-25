# 角色與戰鬥 (Player / Monster AI / Hit Reaction)

> 返回 [文件總覽](README.md)

主遊戲端的「玩家控制 → 怪物 AI → 受擊反應」一條龍。武器發射的細節在 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)；地面特效與拋物線在 [GROUND_EFFECT.md](GROUND_EFFECT.md)；雷射在 [LASER.md](LASER.md)。

> **傷害「數值」怎麼算（玩家 HP/MP、傷害加成、減傷、玩家受傷、武器耗魔、怪物接觸傷害）見 [COMBAT.md](COMBAT.md)。** 本檔的受擊反應（白光閃爍/擊退/無敵）是「表現層」；COMBAT.md 是「數值層」——傷害先過中央 `CombatSystem` 算完修正，才呼叫這裡的 `TakeDamage` 觸發反應。玩家的「受擊參數寫死、預留接觸傷害」現已由 COMBAT.md 接上（`PlayerController` 已實作 `IDamageable` + `CombatStats`）。

## 玩家控制器 (PlayerController)

* `Rigidbody2D (Dynamic)`，透過 `FixedUpdate` 設定 `_rb.velocity` 移動。
* 持有 `WeaponManager` 引用與 `CurrentWeaponID`，透過 `SwitchWeapon(id)` 切換武器。**初始武器為武器表最後一號（最高 ID）**；按 `E` 呼叫 `WeaponManager.SwitchToPreviousWeapon()` 往前切（往較小 ID、繞回最高）。
* 發射時從 `WeaponManager` 取得 `WeaponData` → `RecipeEntry` → `ProjectileData`，並將 `BounceTarget` 映射為 `NonBounceLayers`。
* `ResolvePierceableLayers(RecipeEntry)`：依 `BlockedByEnvironment` 決定是否把 `EnvLayer` 加入 `PierceableLayers`，所有 `Shoot*` 路徑共用此方法，讓 CSV 欄位對所有武器（環繞、直射、分裂…）都能生效。
* **環繞彈**：`ProjectileData.IsOrbital` 為真時走 `ShootOrbital`；每次發射前會先銷毀本玩家上一輪尚未消失的環繞子彈，再依 `OrbitalCount` 重新生成（`OnDestroy` 時亦會清掉，避免引用已銷毀的玩家 Transform）。
* **環繞群組生命週期**：每顆環繞彈的個別 `LifeTime` 會被 `PlayerController` 覆寫為 `-1`（不自動超時），改由 `_orbitalGroupExpireTime = Time.time + recipe.LifeTime` 統一管理；`Update()` 偵測到群組到期時呼叫 `ClearActiveOrbitalBullets()` 一次銷毀整組，確保所有環繞彈一起出現一起消失，不會因單顆事件錯位。`recipe.LifeTime < 0` 時群組永不到期。
* **傷害串接**：`HandleBulletHit` 方法接收命中事件，使用 `WeaponData.Damage` 計算傷害（不再寫死數值）。
* **翻轉邏輯**：攻擊時依滑鼠方向翻轉，移動時依移動方向翻轉，`isFacingRightByDefault` 控制圖片原始朝向。

## 怪物 AI 系統 (Monster AI)

### MonsterSensor
* 玩家參考在 `Start()` 時快取一次（`FindGameObjectWithTag`），之後每幀直接讀取快取，不重複搜尋全場景。
* 若快取遺失（玩家死亡/重生），自動嘗試重新尋找。

### Brain & Actuator
* `IMonsterBrain` 介面：`Think(actuator, player)` 決策邏輯，目前實作 `ChaseBrain`（追擊）。
* `MonsterActuator`：執行物理移動（`_rb.velocity`），維護 `IsMoving` 狀態旗標供動畫系統查詢。

### MonsterController
* 管理怪物血量（`MaxHealth`, `_currentHealth`）。
* `Initialize(MonsterData)`：由 `MonsterSpawner` 呼叫，從 CSV 資料初始化怪物，`AutoAdjustCollider` 只在此處呼叫一次。
* `Start()` 中若 `_brain == null`（手動放置的怪物），才執行預設初始化並呼叫 `AutoAdjustCollider`。
* `IsFacingRightByDefault`：控制圖片原始朝向，翻轉邏輯根據此值正確決定面向玩家的方向。

### MonsterSpawner
* 從 CSV 讀取怪物資料（`ID, Name, HP, BrainType, Weapon, Scale, PrefabPath, InvincibleTimeMs, KnockbackThreshold, KnockbackPercent`）。
* `EnemyLayer`（Inspector 設定）：動態設定生成怪物的 Layer，不寫死 Layer 編號。
* 初始面向透過 `FindGameObjectWithTag("Player")` 決定，不依賴 `PlayerController` 具體類別。

## 受擊反應系統 (Hit Reaction System)

通用的受擊反應元件 `HitReactionHandler`，可掛載在任何有 `SpriteRenderer` + `Rigidbody2D` 的角色或怪物上。

### 視覺效果
* **白光閃爍**：受擊瞬間使用自訂 `Custom/SpriteFlash` Shader，透過 `MaterialPropertyBlock` 控制 `_FlashAmount` 參數，讓角色快速閃爍白光（3 次，每次 0.06 秒）。所有角色效果一致。
* **半透明無敵**：無敵時間內，角色 Alpha 降低至 0.4，視覺上呈現半透明狀態，表示不可被傷害。

### 可配置參數（怪物由 CSV 驅動，玩家暫時寫死）

| 參數 | 說明 |
|------|------|
| `InvincibleTimeMs` | 受擊後無敵時間（毫秒），0 表示無無敵時間 |
| `KnockbackThreshold` | 觸發擊退的最低傷害值，單次傷害 ≥ 此值才會擊退 |
| `KnockbackPercent` | 擊退距離，以角色圖片世界寬度的百分比計算（例如 50 = 圖寬的 50%） |

### MonsterData.csv 新增欄位

| CSV 欄位 | 說明 |
|----------|------|
| `InvincibleTimeMs` | 怪物受擊後的無敵時間（毫秒） |
| `KnockbackThreshold` | 觸發怪物擊退的最低傷害值 |
| `KnockbackPercent` | 擊退距離（圖片世界寬度的 %） |

### 運作流程
1. `TakeDamage(damage, hitDirection)` 被呼叫。
2. `HitReactionHandler.TryHitReaction()` 檢查是否在無敵中：若無敵則回傳 `false`，傷害被完全忽略。
3. 若非無敵，同時啟動三個效果（各自獨立計時）：
   * **白光閃爍**（固定 3 次閃爍）
   * **擊退位移**（若 `damage ≥ KnockbackThreshold` 且 `KnockbackPercent > 0`，沿受擊方向位移）
   * **無敵時間**（若 `InvincibleTimeMs > 0`，進入半透明無敵狀態）
4. 擊退期間，怪物 AI 暫停移動決策，玩家輸入移動被忽略。

### 擊退方向
* 怪物被子彈擊中時：方向為子彈位置 → 怪物位置（推離子彈）。
* 玩家被怪物接觸時：方向由未來的接觸傷害系統提供（預留介面）。
