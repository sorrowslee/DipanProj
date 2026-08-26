# 角色與戰鬥 (Player / Monster AI / Hit Reaction)

> 返回 [文件總覽](README.md)

主遊戲端的「玩家控制 → 怪物 AI → 受擊反應」一條龍。武器發射的細節在 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)；地面特效與拋物線在 [GROUND_EFFECT.md](GROUND_EFFECT.md)；雷射在 [LASER.md](LASER.md)。

> **傷害「數值」怎麼算（玩家 HP/MP、傷害加成、減傷、玩家受傷、武器耗魔、怪物接觸傷害）見 [COMBAT.md](COMBAT.md)。** 本檔的受擊反應（白光閃爍/擊退/無敵）是「表現層」；COMBAT.md 是「數值層」——傷害先過中央 `CombatSystem` 算完修正，才呼叫這裡的 `TakeDamage` 觸發反應。玩家的「受擊參數寫死、預留接觸傷害」現已由 COMBAT.md 接上（`PlayerController` 已實作 `IDamageable` + `CombatStats`）。

## 玩家控制器 (PlayerController)

* `Rigidbody2D (Dynamic)`，透過 `FixedUpdate` 設定 `_rb.velocity` 移動。
* 持有 `WeaponManager` 引用與 `CurrentWeaponID`，透過 `SwitchWeapon(id)` 切換武器。**武器的唯一來源是背包武器欄**（`OnInventoryChanged` → `SwitchWeapon`）：裝備哪把就用哪把，**卸下就是 `CurrentWeaponID = 0`＝沒有武器**。
* **不能開火時，按左鍵／空白鍵完全沒反應**（兩種情形：**沒有裝備武器**，或**這張地圖禁用武器**＝ MapsTable 的 `NoWeapon` 欄，見 [MAP_SYSTEM.md](MAP_SYSTEM.md)）——不發射、不扣魔、不擺攻擊動作、也不轉身面向滑鼠。實作是 `HandleFiring()` 開頭一道 `!CanFire` 的 guard，放在所有分支之前，一處擋掉雷射／佛光／集氣／離散全部路徑，並順手清掉殘留的光束/佛光/集氣。`PlayerController.CanFire` 是「有武器 ＋ 這張圖沒禁用」的單一判斷，發射與「按攻擊鍵轉身」共用它，確保兩處永遠一致。（2026-07-27 改；此前 `Start()` 會強制帶上武器表最高 ID，導致空手也能攻擊。E 鍵循環切換同時移除。）
* 發射時從 `WeaponManager` 取得 `WeaponData` → `RecipeEntry` → `ProjectileData`，並將 `BounceTarget` 映射為 `NonBounceLayers`。
* `ResolvePierceableLayers(RecipeEntry)`：依 `BlockedByEnvironment` 決定是否把 `EnvLayer` 加入 `PierceableLayers`，所有 `Shoot*` 路徑共用此方法，讓 CSV 欄位對所有武器（環繞、直射、分裂…）都能生效。
* **環繞彈**：`Mode=Orbital`（`ProjectileData.IsOrbital` 由它推導） 為真時走 `ShootOrbital`；每次發射前會先銷毀本玩家上一輪尚未消失的環繞子彈，再依 `OrbitalCount` 重新生成（`OnDestroy` 時亦會清掉，避免引用已銷毀的玩家 Transform）。
* **環繞群組生命週期**：每顆環繞彈的個別 `LifeTime` 會被 `PlayerController` 覆寫為 `-1`（不自動超時），改由 `_orbitalGroupExpireTime = Time.time + recipe.LifeTime` 統一管理；`Update()` 偵測到群組到期時呼叫 `ClearActiveOrbitalBullets()` 一次銷毀整組，確保所有環繞彈一起出現一起消失，不會因單顆事件錯位。`recipe.LifeTime < 0` 時群組永不到期。
* **傷害串接**：`HandleBulletHit` 方法接收命中事件，使用 `WeaponData.Damage` 計算傷害（不再寫死數值）。
* **翻轉邏輯**：攻擊時依滑鼠方向翻轉，移動時依移動方向翻轉，`isFacingRightByDefault` 控制圖片原始朝向。
* **外型與體型**（2026-08-18）：`SetBloodline(folder, bodyScale)` 換血統外型與體型倍率；
  `BodyScale` 是**純視覺**（不動碰撞框、不動數值），顯示高度 = `CharacterWorldHeight × BodyScale`
  ＝ `ScaledCharacterHeight`。見 [CHARACTER_SETUP.md](CHARACTER_SETUP.md) 與 [BLOODLINE.md](BLOODLINE.md) §2。
* **可見身體幾何**：`VisibleBodyHeight` / `FeetWorldPos` / `BodyCenterWorldPos`。
  **要定位或縮放「掛在玩家身上」的特效一律用這三個**——`transform.position` 是畫布中心不是身體中心，
  `SpriteRenderer.bounds` 含不含透明留白也沒有保證。見 [PROBLEMS.md](PROBLEMS.md) **E14**。
* **`RefreshBodyScaledVisuals()`**：體型改變後把「還活著且依身體大小」的東西重新對齊
  （腳下影子、佛光光環、集氣光圈）。⚠ **之後再加這類「持續掛在玩家身上」的效果，記得在這裡補一行**，
  否則它會停在舊尺寸。
* **`IsDead`**（公開唯讀）：給演出類系統判斷「該中止了」。演出期間玩家常是被鎖住不能閃避的，
  死了還繼續演會變成屍體爬起來（血統變身踩過，見 [BLOODLINE.md](BLOODLINE.md) §5）。

## 怪物 AI 系統 (Monster AI)

### MonsterSensor
* 玩家參考在 `Start()` 時快取一次（`FindGameObjectWithTag`），之後每幀直接讀取快取，不重複搜尋全場景。
* 若快取遺失（玩家死亡/重生），自動嘗試重新尋找。
* **感測範圍 `DetectionRange`（世界單位＝地圖格數）**：怪物只在「與玩家**直線距離 ≤ DetectionRange**」時才追（`GetTargetPlayer` 回玩家，否則回 null）。**1 世界單位 = 1 格 tile**（tileSize=1）。一般怪預設 **10**、紅嫁衣 boss 用 **30**。紅嫁衣關卡每張房間都是 **18×10 格**，對角線（房內最遠距離）≈ **20.6**——所以要「整張房間都看得到玩家」設 **≥ 21**（保險用 25 更穩）。改在 `MonsterSensor.DetectionRange`（一般怪）或 `RedBridalGownBrain` 的 `DetectionRange` 常數（boss）。

### Brain & Actuator
* `IMonsterBrain` 介面：`Think(actuator, player)` 決策邏輯，目前實作 `ChaseBrain`（追擊）。
* `MonsterActuator`：執行物理移動（`_rb.velocity`），維護 `IsMoving` 狀態旗標供動畫系統查詢。
* **走路 / 發呆判定＝看『實際位移』，不看指令速度（2026-07-13）**：`MonsterController.HandleVisuals` 每幀量 `transform.position` 的實際位移速度（指數平滑 tau 0.08；玩家/怪物 Rigidbody2D 已開 Interpolate 故量測穩定），超過 `MoveAnimThreshold`(新增欄位，預設 0.12 世界單位/秒) 才播走路、否則 idle，並用此速度餵 `MonsterAnimator.SetState` 讓走路 fps 跟真實移動連動。**不可用 `_rb.velocity`**——所有怪的碰撞框都是 trigger（A* 導航、不做硬碰撞），逃跑卡在牆角時 velocity 仍是滿的但沒真的移動，會誤播走路（原地踏步，見 [PROBLEMS.md](PROBLEMS.md) F15）。
* **攻擊 / 施法動畫觸發（`MonsterController.HandleVisuals` + `MonsterAnimator` 的 `attack` 幀）**：怪演 attack 動畫有兩個來源——① 接觸攻擊：玩家進入 `AttackRange`(預設 1.3) 內；② **施放技能**：`MonsterWeaponUser` 成功施放（如紅嫁衣召喚家人）時呼叫 `MonsterController.NotifySkillCast()`，讓 attack 動畫維持 `SkillCastAnimSeconds`(預設 0.6s)——**不限距離**，邊逃邊召的 boss 也看得到出手動作。**沒有 `attack` 幀時**：施法視窗內退回播**走路**當出手表演（只在該 0.6s，平常靜止仍 idle）、其他情況退回走路/發呆，皆不報錯（`MonsterAnimator.Has` 防呆）。⚠️ 若 boss 明明畫了 attack 幀卻不播攻擊動畫，八成是 **attack 幀沒 `Sync Map Assets` 進 StreamingAssets**（`Has(Attack)=false`），見 [PROBLEMS.md](PROBLEMS.md) F16。
* **武器出手點 `PlayerController.MuzzleWorldPos`（2026-08-26）**：子彈／光束／連鎖閃電／環繞圈／發射特效都從「可見腳底往上 `MuzzleHeightRatio`（0.5）× 可見身高」出來，瞄準方向也從這點看向滑鼠。之前全釘在 `transform`——那是人類體型的身體中心，毛殭這種 `BodyScale`＞1 的血統身體往上長、transform 不動，劍會從腹部飛出。近戰／突進／法陣的**範圍中心**仍用 `transform`（那是站的位置，不是出手點）；落雷的落點扇形基準也不變。
* **玩家攻擊動畫（`PlayerController.HandleVisuals`）＝只有「真的攻擊出去」才擺，且持續武器只擺開頭一次**（2026-07-10；**播放方式 2026-08-22 改版**）：① 離散武器（子彈/拋物/連鎖/雷擊/環繞/召喚）——每次「發射成功」呼叫 `TriggerAttackPose()`（CD 中／魔力不足／召喚已滿都不擺，見 PROGRESS 2026-07-10）。② 持續武器（雷射/佛光）——只在「**開始放的那一下**」（上升緣：`_activeBeams`/`_activeAura` 從無到有）擺**一次**。**持續 vs 離散的差別天生來自武器發射模式**（`Mode=Laser`/`Mode=Aura` 會產生 `_activeBeams`/`_activeAura`、離散走 `Shoot`），所以**不寫死武器類型、不需額外欄位**——之後任何走同一套光束/佛光機制的新持續武器自動生效。
* **攻擊動畫怎麼播（2026-08-22 起）**：改成「按下／放開」邊緣驅動，**姿勢長度由動畫本身決定**，不再是寫死的 0.12 秒（舊版那個常數在 12fps 下只夠播 1.4 幀，25 幀的動畫永遠只看得到前兩幀，見 [PROBLEMS.md](PROBLEMS.md) **G6**）。四條規則：
  1. **起播幀自動跳過起手**——`PlayerSpriteLibrary.GetActionStartFrame` 從圖算出「動作真正開始」的那一幀（門檻 `ActionStartPeakRatio`，全遊戲一個常數、不是每個血統一個）。
  2. 按下開火 → 從起播幀播**一次**（`PlayerAnimator.StartAttack`，Attack 已改成一次性、不是循環）。
  **2b. 只播到「動作最大幀＋1 幀」就算播完（2026-08-26）**——`PlayerSpriteLibrary.GetActionEndFrame`：同一條「跟站姿差多少」的曲線，**第一次**到峰值 90%（`ActionEndPeakRatio`）那格＝出手到底，再多播 `ActionEndTailFrames`（1）幀當收勢。後面的幀（AutoSprite 常多出來的第二拳、手放到別處）一律不播。作者拍板：**完全由程式判斷、不加手填覆寫欄**——抓歪了重做圖比找幀號快。取「第一次到 90%」而非嚴格最大值，是怕兩拳的圖第二拳伸得更開會抓到第二拳。下面 3／4 的「播完」「最後一幀」都改指這個結束幀。換圖後用 `Project Tools → 角色 → 攻擊動畫幀數報告` 看每個血統抓到哪一幀。
  3. 播完還按著 → 由 **Attack Anim Repeat While Held** 決定：勾（目前在試）＝**從起播幀再來一次**（反覆出手）；不勾＝**定格在最後一幀**（維持收勢）。⚠ 重播的是「起播幀 → 最後一幀」，不是整段繞回第 0 幀——起手是一次性的，每輪重播會變成「施法到一半又把手放下」。
  4. 中途放開 → 這一次照樣播完（`IsAttackPlaying`），播完才回 Idle/Walk；**放開再按**才會重播。
  ⚠ **移動優先，而且是硬取消**：2D 單張逐格圖沒辦法上下半身分離，移動與攻擊只能擇一顯示。**一開始移動就立刻切回 walk，當次攻擊動作沒播完也直接砍掉**（Inspector 的 **Cancel Attack Pose When Moving**，預設開）——不這樣的話「按住開火邊跑」會變成用施法姿勢滑過地板，而那是這遊戲最常見的操作。停下來時若還按著開火，攻擊動作會從起播幀重新擺一次。關掉這個選項則改成「移動中仍把當次動作完整播完才還給走路」。想整套回到 2026-08-22 之前的行為可勾 **Attack Anim Legacy Mode**（只給 A/B 對比）。
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
| `KnockbackPercent` | 擊退距離，以角色圖片世界寬度的百分比計算（例如 50 = 圖寬的 50%）。**玩家端會先除以 `WidthScaleCompensation`** |
| `WidthScaleCompensation` | 擊退用的「圖寬」補償倍率（預設 1 = 不補償）。**玩家**由 `SetBloodline` 填入 `BodyScale` 除回去——不補償的話 1.5 倍體型的角色會被擊退 1.5 倍遠，血統體型就不再是純視覺了。**怪物不用管**：牠們的顯示大小本來就代表體型差異，大隻的被擊退得遠是合理的 |

> ⚠ 這一欄是「角色可以有多種顯示大小」帶出來的——**任何拿角色圖尺寸去算的『數值』，
> 在引入體型倍率之後都要重新檢查一遍**。通則見 [PROBLEMS.md](PROBLEMS.md) **E14**。

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
* 玩家被怪物接觸時：方向由 `EnemyContactDamage` 提供（接觸傷害**已實作**，見 [COMBAT.md](COMBAT.md)；此處原本寫「預留介面」已過時）。
*2026-07-27 更正：初始武器與 E 鍵切換的敘述已過時（強制指派最高 ID 與 `SwitchToPreviousWeapon` 皆已移除）。*

---

## 移動平滑化（沿牆滑動 ＋ 角落校正）— 2026-08-19

`PlayerController.FixedUpdate` 原本只有一行 `_rb.velocity = 輸入 × 速度`，撞牆完全交給 Box2D。
物理上正確，但有兩個具體症狀：

1. **單軸輸入撞垂直面時切線分量是 0 → 完全停住。** 只按「右」撞到屏風的角，就算右下明明通得過，也得自己再按「下」。
2. **每個 FixedUpdate 無條件覆寫 velocity**，把 solver 上一步算出的「被牆修正過的切向速度」丟掉 → 斜推牆是在牆上抖，不是乾淨地滑。

再加上地上物碰撞改成貼合圖形之後（[PROBLEMS.md](PROBLEMS.md) B9），斜的表面是**階梯狀**的，圓形玩家沿著走會一階一階頓。

### 現在的做法

`ResolveMoveVelocity(desired)` 夾在輸入與 velocity 之間，**沒撞到東西就原樣回傳**（絕大多數幀只花一次 cast）：

1. **沿牆滑動**：撞到就把速度投影到牆面切線；切線分量 > 25% 才算「斜推牆」。會再確認滑動方向本身也通
   （凹角時 A 面的切線正好指向 B 面，不檢查會讓速度逐幀互換）。
2. **角落校正**：切線幾乎為 0 = 正面撞上 → 往左右各試探一次，**只有一側通得過才輕推**。
   兩側都不通 = 真的是牆，照常卡住；兩側都通 = 窄障礙，交給玩家自己決定繞哪邊。
3. **零摩擦材質**：玩家原本沒有 PhysicsMaterial2D，吃 Unity 預設 friction 0.4，貼牆會被拖慢。
   用程式建一份 `HideAndDontSave` 的共用材質，Rigidbody 與 collider 兩邊都指。

### ⚠ 動這段之前一定要知道的

**探測圓必須比實際碰撞圓小一點**（`ProbeInset = 0.05`，並把縮掉的量補回探測距離）。
專案全域 `queriesStartInColliders = false`，而**整張地圖的牆是同一顆 CompositeCollider2D**——
玩家一貼上牆，用等大的圓從圓心射出去時起點就算重疊，**那顆 composite 會被整片忽略**，
探測回報「前方淨空」→ 這兩個功能正好在最該生效的那一刻靜默失效，而且因為接觸間隙只有 0.01，
還會逐幀時有時無、比完全不作用更難查。**專案在怪物那邊已經踩過同一個坑**
（`AI/MonsterActuator.cs` 的 `DirectClear`：「圓一碰到牆就會因 queriesStartInColliders=false 整片被忽略而誤判暢通」）。

其他要點：
- **探測只吃 Environment / Water**。`queriesHitTriggers` 是開的，而怪物碰撞框全是 isTrigger——
  不過濾的話玩家會把怪物當成牆自動繞開，那是「自動閃避」不是移動平滑化。
- **擊退期間不介入**：`FixedUpdate` 在 `IsKnockedBack` 本來就 early return。
- `CornerMaxNudge` 的幾何意義是「**能修正的最大卡進去深度**」，不是「能繞過多寬的障礙」——
  要完全繞過半寬 w 的障礙需要橫移（半徑 + w），不在這個功能的範圍。
- 全部參數（含總開關 `SmoothMovement`）都開在 Inspector；出事把總開關關掉就完全回到舊行為。
