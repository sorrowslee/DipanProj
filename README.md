# 燃燈計畫 (Project Dipankara) - 專案開發上下文與架構文件

## 1. 專案概述 (Project Overview)

* **遊戲名稱**：燃燈計畫 (Project Dipankara)
* **發行平台**：Steam
* **遊戲類型**：2D 遊戲 (Top-down / 俯視角視角)
* **世界觀與故事設定**：
  * 核心概念啟發自小說《無限恐怖》。
  * 故事場景中存在一尊佛像（燃燈古佛），**佛像本身是守護者，真正的邪惡能量是從佛像背後的「隧道」散發出來的**。
* **開發狀態**：核心戰鬥系統 (Core Combat Loop) 與底層架構建置完成，已完成 CSV 驅動的配方與武器系統重構。

---

> ## ⚠️ 給 AI / Cowork：要產圖請先讀產圖指南
> 本專案若需要產生任何**圖片素材**（武器、道具、投射物、UI、圖示等），
> **務必先閱讀並遵循 [`AI_IMAGE_GEN_GUIDE.md`](AI_IMAGE_GEN_GUIDE.md)** 再開始。
> 該文件記錄了已驗證可行的產圖流程、踩過的雷、去背方式與注意事項。
> 產**飛行投射道具**時，還要特別遵守該文件第 7 節（無特效、純底、會飛的單體等規則）。

---

## 2. 程式架構設計 (Architecture Design)

本專案採用高度解耦的「模組化」設計，將遊戲主體與通用系統分離，便於日後維護與擴充。

### 2.1 雙模組結構

1. **主遊戲模組 (DipanProj_Main)**：
   * 負責遊戲具體邏輯：玩家控制、怪物 AI、關卡流程、血量與傷害計算。
   * 依賴於彈道系統，但彈道系統不反向依賴主遊戲。
2. **獨立彈道系統 (Sorrows.Ballistics Package)**：
   * 純粹負責「子彈生成、物理飛行、碰撞偵測、特殊飛行行為」。
   * **絕對不涉及**任何遊戲內的「血量、傷害數值」計算。
   * 透過 `System.Action<BulletInstance, GameObject, RaycastHit2D>` 將碰撞事件回報給主遊戲。

### 2.2 解耦原則與邊界規範

* **LayerMask 不寫死**：彈道系統內部不寫死任何 Layer 編號。`PierceableLayers`（可穿透層）與 `NonBounceLayers`（不反彈層）一律由主遊戲在呼叫 `BallisticsEngine.Spawn` 時傳入。
* **傷害數值不進入彈道系統**：子彈命中時只透過 `OnBulletHitObject` 事件回報，傷害計算完全由主遊戲的 `HandleBulletHit` 處理。
* **玩家尋找不依賴具體類別**：怪物 AI 透過 `GameObject.FindGameObjectWithTag("Player")` 尋找玩家，不依賴 `PlayerController` 具體類別。

### 2.3 物理設定 (Physics Settings)

* **Tags（標籤）**：
  * 玩家物件標記為 `Player`（供怪物 AI 索敵使用）。
* **Layers（圖層）**：
  * `Layer 3: Environment`（環境與牆壁）
  * `Layer 6: Player`（玩家）
  * `Layer 7: Enemy`（敵人）
* **Rigidbody2D 設定**：玩家與怪物皆使用 **Dynamic** 模式，`GravityScale = 0`，`FreezeRotation Z`。
* **Layer Collision Matrix 設定**：
  * `Enemy vs Enemy`：**關閉**（怪物之間互相穿透，不互卡）
  * `Enemy vs Player`：**關閉**（怪物不推擠玩家，重疊後以損血機制處理）
  * `Enemy vs Environment`：**開啟**（怪物被牆壁阻擋）
  * `Player vs Environment`：**開啟**（玩家被牆壁阻擋）
* **子彈碰撞**：彈道系統使用 `Physics2D.CircleCast` 搭配 `LayerMask` 做偵測，完全不依賴 Unity 物理碰撞事件，不受 Layer Collision Matrix 影響。

### 2.4 美術與資源架構 (Asset & Resource Architecture)

美術資源放置於 `Assets/GameAssets/`（避開 Unity 強制打包的 `Resources` 資料夾），採用「主資源包 + 獨立場景包」架構：

1. **主資源包 (GameAssets/Main/)**：存放所有場景通用的基礎設施（主選單、通用 UI、角色模型、通用裝備、道具 Icon）。
2. **場景資源包 (GameAssets/Modules/)**：每個場景一個獨立子目錄，打包該場景專屬資源（場景大圖、專屬卡片、Tilemap、怪物 Skin、飛行物外觀）。
3. **載入邏輯**：
   * 初始載入：`Main` 主資源包 + `Modules/Tutorial` 教學場景資源包。
   * 動態下載：玩家解鎖新場景後才下載對應資源包。

---

## 3. 核心系統實作細節 (Core Systems)

### 3.1 彈道系統 (Sorrows.Ballistics)

採用 Data-Driven（資料驅動）與 Strategy Pattern（策略模式）設計。

#### ProjectileData（純 C# 類別）
子彈的配方資料，由 CSV 配方表載入。

| 欄位 | 說明 |
|------|------|
| `Speed` | 飛行速度 |
| `Radius` | 子彈判定半徑（用於 CircleCast） |
| `LifeTime` | 存活時間（秒）；**-1** = 不因時間銷毀 |
| `FireInterval` | 發射間隔（秒） |
| `RotationSpeed` | 飛行時自轉速度（度/秒） |
| `PierceCount` | 穿透次數，0 為不穿透；設為 **-1** 表示無限穿透（不遞減） |
| `HasBounce` / `MaxBounces` | 是否反彈 / 最大反彈次數 |
| `HasSplit` / `SplitCount` / `SpreadAngle` / `Timing` | 是否分裂 / 數量 / 角度 / 時機 |
| `SubProjectileData` | 分裂產生的子彈配方（透過 SubRecipeID 查表解析） |
| `IsOrbital` / `OrbitalRadius` / `OrbitalCount` | 是否環繞 / 環繞半徑 / 環繞數量 |

#### BallisticsEngine（靜態引擎）
```
Spawn(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit)
```
* 在子彈初始化前預先訂閱 `OnBulletHitObject` 事件（Pre-subscribe 模式），確保第 0 幀分裂彈不漏接。
* `Internal_SpawnSplit`：供分裂行為遞迴生成子彈，並繼承父彈的所有 LayerMask 設定與事件。

#### BulletInstance（子彈實體）
* 使用 `Physics2D.CircleCast` 做連續碰撞偵測（避免穿牆）。
* `CheckSpawnOverlap()`：生成時做一次 `OverlapCircle` 近距離檢查，處理子彈起點已在 Collider 內部時偵測不到的問題。
* `HashSet<int> _hitObjects`：防止同一幀對同一目標重複觸發命中。
* `_isDestroyed` 旗標：`Destroy` 呼叫後立即阻止同幀繼續執行命中邏輯。
* 穿透邏輯：命中目標在 `PierceableLayers` 內時，若 `PierceCount > 0` 則不銷毀並遞減；若 `PierceCount < 0`（例如 -1）則不銷毀且不遞減（無限穿透）。
* 存活時間：`LifeTime < 0`（例如 -1）時不因時間銷毀；否則每幀倒數，歸零時銷毀。

#### IBulletBehavior（行為介面）
| 行為 | 說明 |
|------|------|
| `BounceBehavior` | 牆壁反彈（`Vector2.Reflect`），命中 `NonBounceLayers` 內的目標時不反彈 |
| `SplitBehavior` | 扇形分裂，支援 OnSpawn / OnHit / OnDeath 三種觸發時機 |
| `RotationBehavior` | 飛行中持續自轉 |
| `OrbitalBehavior` | 以指定 Transform 為圓心環繞飛行，穿透時繼續環繞，反彈時脫軌飛出 |

### 3.2 配方與武器系統 (Recipe & Weapon System)

採用 CSV 雙表架構，實現「配方」與「武器」的完全分離。

#### 配方表 (RecipeTable.csv)
定義子彈的飛行行為配方，存放於 `Assets/Data/RecipeTable.csv`。
彈道系統的 `ProjectileData` 欄位決定了此表的欄位結構。

> 如需查詢每個欄位的詳細用法、填寫規則與組合範例，請參閱 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)。

| CSV 欄位 | 對應 ProjectileData | 說明 |
|----------|-------------------|------|
| `ID` | — | 配方唯一識別碼 |
| `Name` | — | 配方名稱 |
| `Speed` | `Speed` | 飛行速度 |
| `Radius` | `Radius` | 子彈判定半徑 |
| `LifeTime` | `LifeTime` | 存活時間（秒）；**-1** = 不因時間銷毀 |
| `FireInterval` | `FireInterval` | 發射間隔（秒） |
| `RotationSpeed` | `RotationSpeed` | 自轉速度（度/秒） |
| `PierceCount` | `PierceCount` | 穿透次數；**-1** = 無限穿透 |
| `SpreadCount` | `SplitCount` | 散射/分裂數量 |
| `SpreadAngle` | `SpreadAngle` | 散射/分裂角度 |
| `SplitTiming` | `Timing` | 分裂時機（OnSpawn / OnHit / OnDeath） |
| `SubRecipeID` | `SubProjectileData` | 分裂子彈配方 ID（二次解析） |
| `BounceTarget` | — | 反彈對象（None / Environment / Enemy），由遊戲端映射為 LayerMask |
| `MaxBounces` | `MaxBounces` | 最大反彈次數 |
| `HomingTurnSpeed` | `HomingTurnSpeed` | 追蹤轉向速度（度/秒），0 為不追蹤 |
| `IsOrbital` | `IsOrbital` | 是否為環繞型彈道（1 = 是，留空 = 否） |
| `OrbitalRadius` | `OrbitalRadius` | 環繞半徑，以玩家為圓心的軌道半徑 |
| `OrbitalCount` | `OrbitalCount` | 環繞數量，每次發射生成幾顆環繞子彈（每次發射會先清除上一輪同玩家的環繞子彈） |
| `BlockedByEnvironment` | — | 子彈是否被地形阻擋；留空或 `1` = 會被擋（預設），`0` = 把 `EnvLayer` 加入可穿透層，子彈穿過地形不被銷毀（需搭配 `PierceCount != 0`） |

#### 武器表 (WeaponTable.csv)
定義武器的遊戲屬性，存放於 `Assets/Data/WeaponTable.csv`。

| CSV 欄位 | 說明 |
|----------|------|
| `ID` | 武器唯一識別碼 |
| `Name` | 武器名稱 |
| `Damage` | 傷害數值 |
| `RecipeID` | 對應配方表的 ID |
| `WeaponSpritePath` | 武器圖檔路徑（相對於 `Assets/Resources/`，不含副檔名），與 `WeaponAniPath` 二擇一 |
| `SpriteAngleOffset` | 圖片角度偏移（度），見下方說明 |
| `WeaponAniPath` | 序列圖路徑前綴（相對於 `Assets/Resources/`，不含編號與副檔名），有值時忽略 `WeaponSpritePath` |
| `WeaponAniNumber` | 序列圖張數，系統自動載入 `{WeaponAniPath}_01` ~ `{WeaponAniPath}_NN` |
| `AnimFPS` | 序列圖播放速度（幀/秒），例如 12 = 每秒 12 幀 |
| `BulletScale` | 子彈縮放倍率，預設為 1（留空等同 1），例如 2 = 放大兩倍 |

#### 序列圖動畫設定說明

武器可以使用多張 PNG 序列圖讓飛行外觀產生動畫效果。

**使用方式**：
1. 將序列圖片放入 `Assets/Resources/` 下的對應目錄，檔名依序為 `{前綴}_01.png`、`{前綴}_02.png`、...
2. 在 CSV 中填寫 `WeaponAniPath`（路徑前綴，不含 `_01` 編號與副檔名）、`WeaponAniNumber`（張數）、`AnimFPS`（播放速度）
3. 此時 `WeaponSpritePath` 可留空，系統會使用序列圖的第一幀作為初始顯示

**範例**：
* `WeaponAniPath = Weapon/weapon_fire`、`WeaponAniNumber = 6`、`AnimFPS = 12`
* 系統載入：`Weapon/weapon_fire_01` ~ `Weapon/weapon_fire_06`，以每秒 12 幀循環播放
* 分裂子彈自動繼承動畫設定

**二擇一規則**：有 `WeaponAniPath` 就用動畫，沒有就用 `WeaponSpritePath` 靜態圖片。

#### SpriteAngleOffset 設定說明

武器圖片在飛行時需要朝向正確的方向。`SpriteAngleOffset` 用來補償圖片原始角度與飛行方向之間的差異。

**設定方式**：
1. 想像這張圖片從畫面**正左方射向正右方**（→ 方向）
2. 此時圖片的「攻擊端」（例如劍尖）需要朝右
3. 如果原始圖片的攻擊端不是朝右，就需要旋轉一個角度讓它朝右
4. 這個旋轉角度就是 `SpriteAngleOffset`

**角度方向**（Unity 2D 標準）：
* 正值 = 逆時針旋轉
* 負值 = 順時針旋轉

**範例**：
* 圖片的劍尖原本朝右上 45° → `SpriteAngleOffset = -45`（順時針轉 45° 讓劍尖朝右）
* 圖片的劍尖原本朝上 → `SpriteAngleOffset = -90`
* 圖片本身就朝右（如水平飛彈）→ `SpriteAngleOffset = 0`

設定完成後，無論玩家往哪個方向射擊，武器圖片都會自動旋轉到正確角度，攻擊端永遠指向飛行方向。分裂子彈也會自動繼承此設定。

#### RecipeManager
* 在 `Awake()` 時從 CSV 載入所有配方，建立 `Dictionary<int, RecipeEntry>` 索引。
* 二次解析 `SubRecipeID`，將 ID 解析為 `ProjectileData` 物件引用。
* `BounceTarget` 以語意化字串（None / Environment / Enemy）儲存，由 `PlayerController` 在發射時映射為 `LayerMask`。

#### WeaponManager
* 在 `Start()` 時從 CSV 載入所有武器，透過 `RecipeManager` 解析 `RecipeID` 為 `RecipeEntry` 引用。
* 使用 `PrefabMapping` 序列化列表模式（與 `MonsterSpawner` 一致），在 Inspector 中拖入子彈 Prefab。

#### BounceTarget 映射邏輯
`PlayerController` 在發射時將 `BounceTarget` 語意值轉為 `NonBounceLayers`：
* `Environment`（反彈障礙物）→ `NonBounceLayers = EnemyLayer`
* `Enemy`（反彈怪物）→ `NonBounceLayers = EnvLayer`
* `None`（不反彈）→ `NonBounceLayers = EnvLayer | EnemyLayer`

### 3.3 玩家控制器 (PlayerController)

* `Rigidbody2D (Dynamic)`，透過 `FixedUpdate` 設定 `_rb.velocity` 移動。
* 持有 `WeaponManager` 引用與 `CurrentWeaponID`，透過 `SwitchWeapon(id)` 切換武器。
* 發射時從 `WeaponManager` 取得 `WeaponData` → `RecipeEntry` → `ProjectileData`，並將 `BounceTarget` 映射為 `NonBounceLayers`。
* `ResolvePierceableLayers(RecipeEntry)`：依 `BlockedByEnvironment` 決定是否把 `EnvLayer` 加入 `PierceableLayers`，所有 `Shoot*` 路徑共用此方法，讓 CSV 欄位對所有武器（環繞、直射、分裂…）都能生效。
* **環繞彈**：`ProjectileData.IsOrbital` 為真時走 `ShootOrbital`；每次發射前會先銷毀本玩家上一輪尚未消失的環繞子彈，再依 `OrbitalCount` 重新生成（`OnDestroy` 時亦會清掉，避免引用已銷毀的玩家 Transform）。
* **環繞群組生命週期**：每顆環繞彈的個別 `LifeTime` 會被 `PlayerController` 覆寫為 `-1`（不自動超時），改由 `_orbitalGroupExpireTime = Time.time + recipe.LifeTime` 統一管理；`Update()` 偵測到群組到期時呼叫 `ClearActiveOrbitalBullets()` 一次銷毀整組，確保所有環繞彈一起出現一起消失，不會因單顆事件錯位。`recipe.LifeTime < 0` 時群組永不到期。
* **傷害串接**：`HandleBulletHit` 方法接收命中事件，使用 `WeaponData.Damage` 計算傷害（不再寫死數值）。
* **翻轉邏輯**：攻擊時依滑鼠方向翻轉，移動時依移動方向翻轉，`isFacingRightByDefault` 控制圖片原始朝向。

### 3.4 怪物 AI 系統 (Monster AI)

#### MonsterSensor
* 玩家參考在 `Start()` 時快取一次（`FindGameObjectWithTag`），之後每幀直接讀取快取，不重複搜尋全場景。
* 若快取遺失（玩家死亡/重生），自動嘗試重新尋找。

#### Brain & Actuator
* `IMonsterBrain` 介面：`Think(actuator, player)` 決策邏輯，目前實作 `ChaseBrain`（追擊）。
* `MonsterActuator`：執行物理移動（`_rb.velocity`），維護 `IsMoving` 狀態旗標供動畫系統查詢。

#### MonsterController
* 管理怪物血量（`MaxHealth`, `_currentHealth`）。
* `Initialize(MonsterData)`：由 `MonsterSpawner` 呼叫，從 CSV 資料初始化怪物，`AutoAdjustCollider` 只在此處呼叫一次。
* `Start()` 中若 `_brain == null`（手動放置的怪物），才執行預設初始化並呼叫 `AutoAdjustCollider`。
* `IsFacingRightByDefault`：控制圖片原始朝向，翻轉邏輯根據此值正確決定面向玩家的方向。

#### MonsterSpawner
* 從 CSV 讀取怪物資料（`ID, Name, HP, BrainType, Weapon, Scale, PrefabPath, InvincibleTimeMs, KnockbackThreshold, KnockbackPercent`）。
* `EnemyLayer`（Inspector 設定）：動態設定生成怪物的 Layer，不寫死 Layer 編號。
* 初始面向透過 `FindGameObjectWithTag("Player")` 決定，不依賴 `PlayerController` 具體類別。

### 3.5 受擊反應系統 (Hit Reaction System)

通用的受擊反應元件 `HitReactionHandler`，可掛載在任何有 `SpriteRenderer` + `Rigidbody2D` 的角色或怪物上。

#### 視覺效果
* **白光閃爍**：受擊瞬間使用自訂 `Custom/SpriteFlash` Shader，透過 `MaterialPropertyBlock` 控制 `_FlashAmount` 參數，讓角色快速閃爍白光（3 次，每次 0.06 秒）。所有角色效果一致。
* **半透明無敵**：無敵時間內，角色 Alpha 降低至 0.4，視覺上呈現半透明狀態，表示不可被傷害。

#### 可配置參數（怪物由 CSV 驅動，玩家暫時寫死）

| 參數 | 說明 |
|------|------|
| `InvincibleTimeMs` | 受擊後無敵時間（毫秒），0 表示無無敵時間 |
| `KnockbackThreshold` | 觸發擊退的最低傷害值，單次傷害 ≥ 此值才會擊退 |
| `KnockbackPercent` | 擊退距離，以角色圖片世界寬度的百分比計算（例如 50 = 圖寬的 50%） |

#### MonsterData.csv 新增欄位

| CSV 欄位 | 說明 |
|----------|------|
| `InvincibleTimeMs` | 怪物受擊後的無敵時間（毫秒） |
| `KnockbackThreshold` | 觸發怪物擊退的最低傷害值 |
| `KnockbackPercent` | 擊退距離（圖片世界寬度的 %） |

#### 運作流程
1. `TakeDamage(damage, hitDirection)` 被呼叫。
2. `HitReactionHandler.TryHitReaction()` 檢查是否在無敵中：若無敵則回傳 `false`，傷害被完全忽略。
3. 若非無敵，同時啟動三個效果（各自獨立計時）：
   * **白光閃爍**（固定 3 次閃爍）
   * **擊退位移**（若 `damage ≥ KnockbackThreshold` 且 `KnockbackPercent > 0`，沿受擊方向位移）
   * **無敵時間**（若 `InvincibleTimeMs > 0`，進入半透明無敵狀態）
4. 擊退期間，怪物 AI 暫停移動決策，玩家輸入移動被忽略。

#### 擊退方向
* 怪物被子彈擊中時：方向為子彈位置 → 怪物位置（推離子彈）。
* 玩家被怪物接觸時：方向由未來的接觸傷害系統提供（預留介面）。

### 3.6 地面特效系統 (Ground Effect System)

純主遊戲端的「鏈式觸發」AOE 系統，與彈道系統完全分離。子彈命中怪物時可額外在命中點生成「停留型」地面特效（火焰燃燒、毒霧、冰結等），擁有自己的範圍、存活時間、傷害節拍與循環動畫。

#### 配置檔案
* `Assets/Data/GroundEffectTable.csv`：定義每個地面特效的範圍、存活、傷害、動畫、tile 尺寸
  * 欄位：`ID, Name, Radius, Duration, DamageInterval, Damage, AniPath, AniNumber, AnimFPS, TileSize`
  * `DamageInterval = 0`：生成瞬間單次爆裂；`> 0`：每 N 秒週期 DOT
  * `Duration = -1`：永久存在直到外部銷毀
  * `TileSize`：單個 tile 的世界尺寸（同時是格子間距），預設 1
* `Assets/Resources/Prefabs/GroundEffect/GroundEffect.prefab`：渲染容器（純 SpriteRenderer + GroundEffectInstance）。Prefab 上的 SpriteRenderer 只當 `sortingLayer / sortingOrder / material` 範本，自身不顯示任何 sprite

#### 鏈式觸發
* `RecipeTable.csv` 新增三欄：`GroundEffectID`、`GroundEffectTrigger`、`GroundEffectHitTarget`
* `RecipeManager` 解析後存到 `RecipeEntry.GroundEffectID` / `RecipeEntry.GroundEffectTrigger` / `RecipeEntry.GroundEffectHitTarget`
* `PlayerController.HandleBulletHit`：先用命中目標的 `GameObject.layer` 對 `EnemyLayer` / `EnvLayer` 做位元 AND 判斷，傷害仍只在怪物上結算；接著呼叫 `TryTriggerGroundEffect` 依 `GroundEffectHitTarget`（`Enemy` / `Environment` / `Any` / `Ground`）過濾，符合條件才呼叫 `GroundEffectManager.Spawn(id, hit.point)`
* 拋物線武器走另一條路：`HandleParabolicLanded` 帶 `hitGround = true` 進 `TryTriggerGroundEffect`，搭配 `GroundEffectHitTarget = Ground` 才會觸發
* 首版只實作 `OnHit`，`OnSpawn` / `OnDeath` 會在 Console 印出 Warning（待事件鉤子補完）
* `GroundEffectHitTarget` 與 `BounceTarget` 是**獨立**兩個概念（前者控觸發位置、後者控反彈表面），可自由組合：例如 `BounceTarget=Environment` + `GroundEffectHitTarget=Enemy` 表示「子彈在牆上反彈、打到怪物才放火」
* **發射時的武器是「快照」**：`PlayerController.ShootNormal` / `ShootOrbital` / `ShootParabolic` 把當下武器以 lambda closure 鎖在 `OnBulletHitObject` 或 `OnGroundLanded` callback 內，子彈命中／落地時用的是「發射時的武器」，**不是當下武器**；玩家中途切武器不會讓舊子彈誤觸新武器的傷害值或地面特效。分裂彈 / 環繞彈 / 拋物線彈都繼承同一份快照

#### 拋物線型武器（IsParabolic）
* `RecipeTable.csv` 新增 4 欄：`IsParabolic`、`ArcHeight`、`LaunchSource`、`LandingScatterRadius`
* `ProjectileData.cs` 新增對應字段，`CreateBehaviors` 在 `IsParabolic = 1` 時組裝 `ParabolicBehavior`
* **`Speed` 欄位語意改變**：拋物線下 `Speed` 解讀為**飛行時間（秒）**，**不是**速度。`Speed = 1` → 不論起點到落點多遠，都飛 1 秒抵達；遠的飛快、近的飛慢。`ParabolicBehavior` 把這個值直接當 `flightDuration` 用，已不再做 `distance / speed` 計算
* `ParabolicBehavior`（彈道系統）：完全接管移動，`OnSpawn` 把 `CollisionMask = 0` / `LifeTime = -1` / `Velocity = 0`，飛行中不撞任何 layer。地面位置線性插值，視覺加 `4 * ArcHeight * t * (1 - t)` 的 Y 偏移製造弧線；抵達目標時呼叫 `instance.RaiseGroundLanded(landPos)` 並把 `LifeTime` 設為 0 讓 `BulletInstance` 自動清理
* `BulletInstance.cs` 新增 `Action<BulletInstance, Vector2> OnGroundLanded` 事件，`PlayerController.ShootParabolic` 在 Spawn 後訂閱
* `PlayerController.ShootParabolic`：
  - **吃 `SpreadCount` / `SpreadAngle`**：以「玩家 → 滑鼠」為基準軸，N 顆炸彈在 ±SpreadAngle/2 範圍內等角度分布，扇形目標到玩家距離 = 玩家到滑鼠距離（拋物線**不需要**填 `SplitTiming`，是另一條獨立分裂路徑，不走 SplitBehavior）
  - **吃 `LandingScatterRadius`**：每顆炸彈在自己的扇形目標 + `Random.insideUnitCircle * 半徑` 內找一個落點（圓盤內均勻分布），多顆獨立隨機，避免堆疊在同一點
  - 為了讓 `SpreadCount` / `SpreadAngle` 在拋物線下生效，`RecipeManager.LoadRecipes` 改為**始終**把 SpreadCount / SpreadAngle 寫入 `ProjectileData`（不再只在 HasSplit 路徑下記錄）
* `PlayerController.ResolveParabolicStartPos`：`LaunchSource = Player` 從玩家位置出發；`Offscreen` 用攝影機 `orthographicSize × aspect` 算 viewport 邊界，從攝影機中心射隨機方向找出視野邊界距離 + 1 單位緩衝；多顆炸彈時 `Offscreen` 每顆都**獨立重抽**起點
* 設計重點：拋物線武器**不對怪物造成傷害**，是「地面特效觸發載體」。要做傷害請靠 `GroundEffectTable` 的 `Damage` / `DamageInterval` 設定地面 AOE
* 互斥：與 `IsOrbital` 互斥（同時填 1 行為衝突）；`PierceCount` / `BounceTarget` / `MaxBounces` 在拋物線下無意義（飛行中不參與命中）；`SplitTiming` / `SubRecipeID` 不建議混搭（拋物線自己處理 SpreadCount，SplitBehavior 的 OnHit 不會觸發、OnSpawn 又會再炸一輪）

#### 執行單元
* `GroundEffectManager`（場景單例，掛在主物件上）：CSV 載入、序列圖預載、`Spawn(id, position)` API
* `GroundEffectInstance`（掛在 Prefab 上）：採 **真實圓形掃描（aligned scanline）**渲染——以原點為中心掃整數網格 `(i, j)`，當 `(i*TileSize)² + (j*TileSize)² ≤ Radius²` 就放一個 tile，嚴格對齊網格、上下左右對稱。每個 tile 動態 `AddComponent<SpriteRenderer>` 並繼承 prefab 範本的排序設定，動畫由父物件統一切幀同步顯示。**鋪面範圍嚴格貼齊 Radius**（與 `OverlapCircleAll` 傷害判定一致）。圓滑度取決於解析度：`R / TileSize ≥ 4` 才看得出明顯圓形，例如 `R=1.5、TileSize=0.3` ≈ 81 顆呈圓形；`R=1.5、TileSize=1` 只有 3×3 = 9 顆是 resolution 限制。實際 tile 數 > 500 時會在 Console 印一次 LogWarning，但仍照生成
* 同一目標的 DOT 限流靠 `HitReactionHandler.IsInvincible`，地面特效本身不維護命中表

---

## 4. 目前進度 (Current Progress)

* [x] 確立無限恐怖風格的 2D 世界觀與隧道設定。
* [x] 完成主遊戲與彈道系統的模組解耦，建立明確邊界規範。
* [x] 實作 CSV 資料驅動的子彈配方系統（支援反彈、扇形分裂、穿透、自轉）。
* [x] 解決子彈高頻率生成時的事件訂閱同步問題（Pre-subscribe 模式）。
* [x] 解決子彈起點在 Collider 內部時偵測不到的問題（CheckSpawnOverlap）。
* [x] 修正彈道系統所有硬編碼 Layer 編號，改由主遊戲傳入 LayerMask。
* [x] 實作怪物基礎追擊 AI，完成「射擊 → 命中 → 扣血 → 死亡」的完整 Core Loop。
* [x] 優化 MonsterSensor，快取玩家參考，移除每幀 FindGameObjectWithTag 的效能開銷。
* [x] 規劃 Physics Layer Collision Matrix，解決怪物互卡、怪物推擠玩家的問題。
* [x] 規劃並建立「主資源包 + 場景模組包」的美術目錄架構，完成教學場景地磚的 Tilemap 基礎設定。
* [x] 資料驅動的怪物生成系統（CSV 讀取，動態生成對應數值怪物）。
* [x] 完成 CSV 驅動的配方與武器雙表系統（RecipeTable + WeaponTable），取代 ScriptableObject 配方。
* [x] 實作 RecipeManager（配方載入、SubRecipeID 二次解析、BounceTarget 語意化）。
* [x] 實作 WeaponManager（武器載入、RecipeID 關聯、PrefabMapping 子彈 Prefab 管理）。
* [x] 重構 PlayerController 串接武器系統，傷害數值改由武器表驅動。
* [x] 實作通用受擊反應系統（HitReactionHandler）：白光閃爍、擊退位移、無敵時間。
* [x] MonsterData.csv 新增受擊反應欄位（InvincibleTimeMs, KnockbackThreshold, KnockbackPercent）。
* [x] PlayerController 新增 TakeDamage 介面與寫死的受擊反應參數，預留未來接觸傷害使用。
* [x] 實作武器序列圖動畫系統：WeaponTable.csv 新增 WeaponAniPath / WeaponAniNumber / AnimFPS 欄位，支援多張 PNG 序列圖自動載入與循環播放。
* [x] 擴充 BallisticsEngine.Spawn API 支援 Sprite[] 動畫參數，BulletInstance 內建動畫播放邏輯，分裂彈自動繼承動畫。
* [x] 實作環繞型彈道系統（OrbitalBehavior）：RecipeTable.csv 新增 IsOrbital / OrbitalRadius / OrbitalCount 欄位，子彈以玩家為圓心環繞飛行。
* [x] 環繞彈與穿透（繼續環繞）、反彈（脫軌飛出）、分裂、追蹤等行為完全相容。
* [x] RecipeTable.csv 新增 BlockedByEnvironment 欄位，可讓配方（特別是環繞彈）穿過地形障礙物不被銷毀；PlayerController 抽出 ResolvePierceableLayers 對所有武器路徑通用。
* [x] 環繞彈引入「群組生命週期」：個別子彈 LifeTime 覆寫為 -1，由 PlayerController 統一在 recipe.LifeTime 秒後一次銷毀整組，確保同生同死。
* [x] 實作地面特效鏈式觸發系統：新增 GroundEffectTable.csv、GroundEffectManager / GroundEffectInstance，RecipeTable 新增 GroundEffectID + GroundEffectTrigger 欄位，子彈命中怪物時可在命中點生成停留型 AOE（單次爆裂或週期 DOT，循環動畫）。
* [x] 地面特效改為 tile 鋪面渲染：GroundEffectTable 新增 TileSize 欄位，圓形範圍內每格放一張同步動畫的 sprite；傷害仍以整圓 OverlapCircle 一次計算。
* [x] 地面特效鋪面演進：先試「金字塔（菱形）」演算法但實機呈現過於菱角分明，最終改回「真實圓形掃描」——`(i*TileSize)² + (j*TileSize)² ≤ Radius²` 才保留 tile，圓滑度由 `R / TileSize` 解析度決定（建議 ≥ 4），實際 tile 數 > 500 時印 LogWarning 但仍照生成。
* [x] 修正子彈命中時用「當下武器」造成的跨武器污染：PlayerController 改用 lambda closure 把發射當下的 WeaponData 鎖在 callback，舊子彈不會誤用新武器的 Damage / GroundEffectID。
* [x] 地面特效新增 `GroundEffectHitTarget` 欄位（`Enemy` / `Environment` / `Any`）：與 `BounceTarget` 獨立，可分別設定子彈打到怪物 / 障礙物 / 任一目標時才釋放地面特效，預設 `Enemy` 沿用首版行為。
* [x] 實作拋物線型彈道（`IsParabolic`）：新增 `ParabolicBehavior`（接管移動、CollisionMask=0、視覺假高度），`RecipeTable` 新增 3 欄（IsParabolic / ArcHeight / LaunchSource），`GroundEffectHitTarget` 加上 `Ground` 列舉值；抵達目標落地時透過 `BulletInstance.OnGroundLanded` 事件觸發 `Ground` 過濾的地面特效；支援「玩家位置」與「攝影機視野外隨機方向」兩種發射來源。
* [x] 拋物線進階：`Speed` 欄位語意改為「飛行時間（秒）」（固定時間抵達，與距離無關，多顆同時落地）；支援 `SpreadCount` / `SpreadAngle` 一發多顆的扇形分裂（不需要 `SplitTiming`，獨立分支）；新增 `LandingScatterRadius` 落點隨機半徑欄位，多顆炸彈各自在自己的扇形目標附近圓盤內均勻隨機落點，避免堆疊。

---

## 5. 待辦事項與未來規劃 (Next Steps & Roadmap)

1. **武器組合系統 (Weapon Loadout System)**：
   * 武器外觀（Prefab）與彈道功能（Behavior）分離，兩者獨立定義。
   * 玩家可自由將外觀 + 多個彈道功能組合成一把武器（招式）。
   * 支援從大廳帶入房間，並在房間內自由調整組合。

2. **AI 狀態機擴充**：
   * 根據 CSV 傳入的 `BrainType`，動態賦予怪物不同行為模式（巡邏 Patrol、逃跑 RunAway、遠程攻擊）。
   * 將 BrainType 字串比對改為更安全的列舉或工廠模式。

3. **玩家接觸傷害系統**：
   * 怪物接觸玩家時觸發 `PlayerController.TakeDamage()`（介面已就緒）。
   * 受擊反應（閃爍、擊退、無敵）由 `HitReactionHandler` 自動處理。
   * 玩家受擊參數待建立 CSV 表格驅動。

4. **場景與關卡機制**：
   * 實作佛像與隧道入口的場景互動與邪惡能量的視覺/機制表現。

5. **更多彈道行為擴充**：
   * 透過 `IBulletBehavior` 實作追蹤彈、蛇行彈、延遲爆炸等軌跡。

6. **地面特效擴充**：
   * 補完 `OnSpawn` / `OnDeath` 觸發時機（需要在 `BulletInstance` 增設生成事件與銷毀事件鉤子）。
   * 視需求加入「武器表直接引用地面特效」的純地面型武器（不發射彈直接放置 AOE）。
   * 地面特效可疊加 / 不可疊加策略（例如同一格只能有一團火）。
