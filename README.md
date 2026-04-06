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
| `LifeTime` | 存活時間 |
| `FireInterval` | 發射間隔（秒） |
| `RotationSpeed` | 飛行時自轉速度（度/秒） |
| `PierceCount` | 穿透次數，0 為不穿透 |
| `HasBounce` / `MaxBounces` | 是否反彈 / 最大反彈次數 |
| `HasSplit` / `SplitCount` / `SpreadAngle` / `Timing` | 是否分裂 / 數量 / 角度 / 時機 |
| `SubProjectileData` | 分裂產生的子彈配方（透過 SubRecipeID 查表解析） |

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
* 穿透邏輯：命中目標在 `PierceableLayers` 內且 `PierceCount > 0` 時不銷毀，消耗一次穿透次數。

#### IBulletBehavior（行為介面）
| 行為 | 說明 |
|------|------|
| `BounceBehavior` | 牆壁反彈（`Vector2.Reflect`），命中 `NonBounceLayers` 內的目標時不反彈 |
| `SplitBehavior` | 扇形分裂，支援 OnSpawn / OnHit / OnDeath 三種觸發時機 |
| `RotationBehavior` | 飛行中持續自轉 |

### 3.2 配方與武器系統 (Recipe & Weapon System)

採用 CSV 雙表架構，實現「配方」與「武器」的完全分離。

#### 配方表 (RecipeTable.csv)
定義子彈的飛行行為配方，存放於 `Assets/Data/RecipeTable.csv`。
彈道系統的 `ProjectileData` 欄位決定了此表的欄位結構。

| CSV 欄位 | 對應 ProjectileData | 說明 |
|----------|-------------------|------|
| `ID` | — | 配方唯一識別碼 |
| `Name` | — | 配方名稱 |
| `Speed` | `Speed` | 飛行速度 |
| `Radius` | `Radius` | 子彈判定半徑 |
| `LifeTime` | `LifeTime` | 存活時間 |
| `FireInterval` | `FireInterval` | 發射間隔（秒） |
| `RotationSpeed` | `RotationSpeed` | 自轉速度（度/秒） |
| `PierceCount` | `PierceCount` | 穿透次數 |
| `SpreadCount` | `SplitCount` | 散射/分裂數量 |
| `SpreadAngle` | `SpreadAngle` | 散射/分裂角度 |
| `SplitTiming` | `Timing` | 分裂時機（OnSpawn / OnHit / OnDeath） |
| `SubRecipeID` | `SubProjectileData` | 分裂子彈配方 ID（二次解析） |
| `BounceTarget` | — | 反彈對象（None / Environment / Enemy），由遊戲端映射為 LayerMask |
| `MaxBounces` | `MaxBounces` | 最大反彈次數 |

#### 武器表 (WeaponTable.csv)
定義武器的遊戲屬性，存放於 `Assets/Data/WeaponTable.csv`。

| CSV 欄位 | 說明 |
|----------|------|
| `ID` | 武器唯一識別碼 |
| `Name` | 武器名稱 |
| `Damage` | 傷害數值 |
| `RecipeID` | 對應配方表的 ID |
| `BulletPrefabPath` | 子彈 Prefab 路徑（PrefabMapping 模式） |
| `WeaponSpritePath` | 武器圖檔路徑 |

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
* 從 CSV 讀取怪物資料（`ID, Name, HP, BrainType, Weapon, Scale, PrefabPath`）。
* `EnemyLayer`（Inspector 設定）：動態設定生成怪物的 Layer，不寫死 Layer 編號。
* 初始面向透過 `FindGameObjectWithTag("Player")` 決定，不依賴 `PlayerController` 具體類別。

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

---

## 5. 待辦事項與未來規劃 (Next Steps & Roadmap)

1. **武器組合系統 (Weapon Loadout System)**：
   * 武器外觀（Prefab）與彈道功能（Behavior）分離，兩者獨立定義。
   * 玩家可自由將外觀 + 多個彈道功能組合成一把武器（招式）。
   * 支援從大廳帶入房間，並在房間內自由調整組合。

2. **AI 狀態機擴充**：
   * 根據 CSV 傳入的 `BrainType`，動態賦予怪物不同行為模式（巡邏 Patrol、逃跑 RunAway、遠程攻擊）。
   * 將 BrainType 字串比對改為更安全的列舉或工廠模式。

3. **玩家損血與無敵時間**：
   * 怪物接觸玩家時觸發損血。
   * 損血後進入短暫無敵時間，防止連續扣血。

4. **場景與關卡機制**：
   * 實作佛像與隧道入口的場景互動與邪惡能量的視覺/機制表現。

5. **更多彈道行為擴充**：
   * 透過 `IBulletBehavior` 實作追蹤彈、蛇行彈、延遲爆炸等軌跡。
