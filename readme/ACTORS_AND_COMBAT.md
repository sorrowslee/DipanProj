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
* **感測範圍 `DetectionRange`（世界單位＝地圖格數）**：怪物只在「與玩家**直線距離 ≤ DetectionRange**」時才追（`GetTargetPlayer` 回玩家，否則回 null）。**1 世界單位 = 1 格 tile**（tileSize=1）。一般怪預設 **10**、紅嫁衣 boss 用 **30**。紅嫁衣關卡每張房間都是 **18×10 格**，對角線（房內最遠距離）≈ **20.6**——所以要「整張房間都看得到玩家」設 **≥ 21**（保險用 25 更穩）。改在 `MonsterSensor.DetectionRange`（一般怪）或 `RedBridalGownBrain` 的 `DetectionRange` 常數（boss）。

### Brain & Actuator
* `IMonsterBrain` 介面：`Think(actuator, player)` 決策邏輯，目前實作 `ChaseBrain`（追擊）。
* `MonsterActuator`：執行物理移動（`_rb.velocity`），維護 `IsMoving` 狀態旗標供動畫系統查詢。
* **攻擊動畫觸發（`MonsterController.HandleVisuals` + `MonsterAnimator` 的 `attack` 幀）**：怪演 attack 動畫有兩個來源——① 接觸攻擊：玩家進入 `AttackRange`(預設 1.3) 內；② **施放技能**：`MonsterWeaponUser` 成功施放（如紅嫁衣召喚家人）時呼叫 `MonsterController.NotifySkillCast()`，讓 attack 動畫維持 `SkillCastAnimSeconds`(預設 0.6s)——**不限距離**，所以邊逃邊召的 boss 也看得到出手動作。怪沒有 `attack` 幀時 `MonsterAnimator.Has` 會自動退回走路/發呆、不報錯。
* **玩家攻擊動畫（`PlayerController.HandleVisuals`）＝只有「真的攻擊出去」才擺，且持續武器只擺開頭一次**（2026-07-10）：① 離散武器（子彈/拋物/連鎖/雷擊/環繞/召喚）——每次「發射成功」各擺一次（`_attackAnimUntil` linger；CD 中／魔力不足／召喚已滿都不擺，見 PROGRESS 2026-07-10）。② 持續武器（雷射/佛光）——只在「**開始放的那一下**」（上升緣：`_activeBeams`/`_activeAura` 從無到有）擺**一次**，按住期間恢復站/走姿、不再一直擺（後面一直擺很怪）。**持續 vs 離散的差別天生來自武器發射模式**（`IsLaser`/`IsAura` 會產生 `_activeBeams`/`_activeAura`、離散走 `Shoot`），所以**不寫死武器類型、不需額外欄位**——之後任何走同一套光束/佛光機制的新持續武器自動生效。姿勢時長 `AttackAnimLinger`(0.12s)，離散/持續共用。
* **尋徑：全域 A*（2026-07-10 定案）**：怪物走 `MapNavGrid`（`Scripts/AI/MapNavGrid.cs`）的 A* 尋徑，能真正繞過整片牆/家具、走凹角、只有無路可走才到不了。
  - **格怎麼來（2026-07-10 定案）**：每次載圖後 `MapManager.PlaceAndSetup` 呼叫 `MapNavGrid.EnsureBuilt(mapLoader.Map)`。牆/水/可走以**地圖可走層位元圖**（`MapData.WalkableLayer.blocked`，`'0'` 可走／`'1'` 牆／`'2'` 水）為權威——這是 `MapLoader` 生牆碰撞用的同一份資料、載圖當下就在、與物理時序無關（避免牆是 `CompositeCollider2D`、建格同幀還沒 query-ready 而整片誤判可走，見 PROBLEMS F9）。格解析度＝地圖子格（`tileSize/walkSubdiv`）。淨空（讓路徑離牆一個身位）用**位元圖把牆往外膨脹 `clearCells` 格**（依子格大小自適應，`AgentRadius/子格` 四捨五入）——純位元圖、確定性、保證連通，**不用物理去啃牆**（物理 `OverlapCircle` 會把整片 composite 牆多啃一圈、把窄喉道切斷，害兩個房間不連通，見 PROBLEMS F9）。**地上物家具**（不在位元圖裡）才用 `Physics2D.OverlapCircle`（`Environment`＋`Water`，小半徑 ~0.16）做**聯集**補進格子——這一步在牆膨脹之後跑、半徑遠小於牆淨空，所以只封「真的壓在家具上」的格、碰不到牆（`UnionPhysics` 開關，預設開）。
  - **怎麼走**（`MonsterActuator.MoveTowards`）：① 目標**直線可達**→ 直接走；「直線可達」用 **`MapNavGrid.HasLineOfSight`（和 A* 同一份格子的格視線）**判定，不是物理細射線——這樣「能不能直走」與 A* 障礙（含家具膨脹）一致，不會因細射線穿過家具淨空而誤判可直走、結果撞上家具（見 PROBLEMS F11）。② 否則走 A* 路徑（八方向＋視線平滑成少數航點，`RepathInterval=0.35s` 重算、目標移動夠遠也重算；怪若卡在不可走格會先被導到最近可走格脫困）。
  - **🔑 準則：所有怪物一律用 A* 導航、一律不做硬碰撞（無例外，含 boss、含所有招喚物）**（2026-07-10 定案，見 PROBLEMS F11）。每隻怪身上的碰撞框（貼合圖的身體框＋腳底框）**全設 `isTrigger`**——只做「被打到／接觸傷害」的幾何判定（`queriesHitTriggers=1`、`Physics2D.Distance` 都吃得到），**不擋路**。牆/家具的迴避完全交給 A*（尋徑格已含兩者），所以怪**永遠不會頂在牆角或桌腳上卡死**，只會照路徑平滑走；正常連通的圖不會穿牆（路徑一定走在有淨空的可走格上）。在 `MonsterController.FitVisibleBoxCollider`／`AutoAdjustCollider` 內無條件設定，**不要**再為任何怪加回實體碰撞框。
  - 追擊（`ChaseBrain`）、友軍（`AllyBrain`）、boss 逃跑（`RedBridalGownBrain`）都走 A*。**「讓玩家追得上逃跑的 boss」不靠把她卡住，而是把她的 `MonsterData.Speed` 調慢**（她逃跑會用 A* 自動繞牆）。沒有 `MapNavGrid`（單場景測試）時自動退回局部避障。
  - **感測半徑資料化（2026-07-10）**：怪「多遠能發現玩家」＝ `MonsterSensor.DetectionRange`，現由 `MonsterData.csv` 的 `DetectionRange` 欄設定（`MonsterController.Initialize` 套用）。預設 10；紅嫁衣家人幽靈/ZhaYu 設 25（房對角 ~20.6，全域看得到）、boss 30。**怪不追先分清**：完全不動＝沒發現（查 DetectionRange 與距離，見 PROBLEMS F10）；會動/震盪＝發現了但走不到（查尋徑格，見 PROBLEMS F9）。
  - 可調：`MapNavGrid.AgentRadius`（牆淨空，預設 0.3）、`MapNavGrid.UnionPhysics`（是否把家具聯集進格，預設開）、`MonsterActuator.AvoidObstacles`（總開關）。**限制**：格在載圖時建一次，打破家具開路後格不會即時更新（怪仍會繞原路，之後再補「破壞後重建格」即可）。

### MonsterController
* 管理怪物血量（`MaxHealth`, `_currentHealth`）。
* `Initialize(MonsterData)`：由 `MonsterSpawner` 呼叫，從 CSV 資料初始化怪物，`AutoAdjustCollider` 只在此處呼叫一次。
* `Start()` 中若 `_brain == null`（手動放置的怪物），才執行預設初始化並呼叫 `AutoAdjustCollider`。
* `IsFacingRightByDefault`：控制圖片原始朝向，翻轉邏輯根據此值正確決定面向玩家的方向。

### MonsterSpawner
* 從 CSV 讀取怪物資料（`ID, Name, HP, BrainType, Weapon, Scale, PrefabPath, InvincibleTimeMs, KnockbackThreshold, KnockbackPercent, ContactDamage, DamageReduction, Speed`）。
* `Speed`（移動速度，留空＝3）：由 `MonsterController.Initialize` 設給 `MonsterActuator.MoveSpeed`，每種怪可不同；走路動畫的播放速度（`AnimatorSpeedByVelocity`）也以此為參考。手動放置、未經 Spawner 的怪用 `MonsterActuator` 預設 3。
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
