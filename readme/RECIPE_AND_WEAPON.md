# 配方與武器系統 (Recipe & Weapon System)

> 返回 [文件總覽](README.md)

採用 CSV 雙表架構，實現「配方（飛行行為）」與「武器（遊戲屬性／外觀）」的完全分離。

## 配方表 (RecipeTable.csv)
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
| `IsLaser` | `IsLaser` | 是否為持續型雷射光束（1 = 是，留空 = 否；與 IsOrbital / IsParabolic 互斥）；雷射細節見 [LASER.md](LASER.md) |
| `dotInterval` | `DotInterval` | 雷射傷害節拍（秒）：每 N 秒對當下掃到的目標各結算一次傷害；留空 = 0.5 |
| `BeamRange` | `BeamRange` | 雷射最大射程（世界單位）；Speed / LifeTime 對光束無意義，改用此欄限制長度；留空 = 20 |

> 地面特效相關欄位（`GroundEffectID` / `GroundEffectTrigger` / `GroundEffectHitTarget`）、拋物線欄位（`IsParabolic` / `ArcHeight` / `LaunchSource` / `LandingScatterRadius` / `BlastRadius` 落地殺傷半徑）、佛光欄位（`IsAura`：跟隨玩家的圓形 AOE）也在 RecipeTable，記在 [GROUND_EFFECT.md](GROUND_EFFECT.md)。
> 連鎖閃電欄位（`IsChain` / `ChainRadius`，跳躍次數借 `MaxBounces`、首段射程借 `BeamRange`）、天降雷擊欄位（`IsSkyStrike`，AOE 半徑借 `BlastRadius`、散射借 `SpreadCount`/`SpreadAngle`、落點吸附借 `HomingTurnSpeed`）記在 [LASER.md](LASER.md)。
> 命中迸發子武器欄位（`SubWeaponOnHit` 指向**武器表**的武器、`SubWeaponHitTarget` = Enemy/Environment/All）——子武器自帶外型，與只仿母武器外型的 `SubRecipeID` 不同，詳見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)。
> 軌跡欄位 `TrailStep`（沿路種特效，做地刺類武器）見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) 與 [BALLISTICS.md](BALLISTICS.md) 的 `OnTrailPoint`。

## 武器表 (WeaponTable.csv)
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
| `BeamStyle` | 雷射**種類編號** 1~10（外型一包預設，見 [LASER.md](LASER.md)）：1鏡光 2標準 3脈衝 4離子 5電漿 6虛線 7閃電 8針狀 9洪流 10微光；留空 = 2 |
| `BeamColor` | 雷射**顏色編號** 1~10：1紅 2橙 3黃 4綠 5青 6藍 7紫 8洋紅 9白 10琥珀金；留空 = 9（白） |
| `BeamWidth` | 雷射粗細（**視覺與命中判定共用此欄**，所見即所得）；留空 = 0.5 |
| `FireEffectID` | 發射特效 ID（引用 `VfxTable`）：發射時在玩家身上播一次、朝瞄準方向；留空 / 0 = 不觸發。見 [VFX.md](VFX.md) |
| `HitEffectID` | 擊中特效 ID（引用 `VfxTable`）：子彈／光束命中怪物、障礙物、拋物線落地時在命中點播一次；留空 / 0 = 不觸發。見 [VFX.md](VFX.md) |
| `TrailEffectID` | 軌跡特效 ID（引用 `VfxTable`）：沿子彈飛行路徑每隔配方的 `TrailStep` 距離種一個（**地刺類武器**靠這個沿路長出尖刺）；留空 / 0 = 不觸發。見 [VFX.md](VFX.md) |
| `SummonEffectID` | 召喚特效 ID（引用 `VfxTable`）：**召喚型武器**（`IsSummon`）施放時在**每個生怪點**播一次，怪物**同一幀一起出現**（邊播特效邊出現）；留空 / 0 = 不播、無特效直接生怪。見 [VFX.md](VFX.md)、[BOSS_MODULE.md](BOSS_MODULE.md) |

## 序列圖動畫設定說明

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

## SpriteAngleOffset 設定說明

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

## RecipeManager
* 在 `Awake()` 時從 CSV 載入所有配方，建立 `Dictionary<int, RecipeEntry>` 索引。
* 二次解析 `SubRecipeID`，將 ID 解析為 `ProjectileData` 物件引用。
* `BounceTarget` 以語意化字串（None / Environment / Enemy）儲存，由 `PlayerController` 在發射時映射為 `LayerMask`。

## WeaponManager
* 在 `Start()` 時從 CSV 載入所有武器，透過 `RecipeManager` 解析 `RecipeID` 為 `RecipeEntry` 引用。
* 使用 `PrefabMapping` 序列化列表模式（與 `MonsterSpawner` 一致），在 Inspector 中拖入子彈 Prefab。
* 雷射武器額外把 `BeamStyle` / `BeamColor` 編號透過 `BeamStyleLibrary` 解析為外型參數與顏色（見 [LASER.md](LASER.md)）。
* **初始武器 = 武器表最後一號**：`Start()` 載入後把 `CurrentWeaponID` 設為 `_weaponIDs`（已排序）的最後一個（= 最高 ID），覆寫 Inspector 的預設值。
* **切換方向：`SwitchToPreviousWeapon()`（往較小 ID）**：玩家按 `E` 往前切（最高 ID → … → 最低 → 繞回最高）。`WeaponData` 的欄位另含 `FireEffectID` / `HitEffectID` / `TrailEffectID`（引用 VfxTable，見 [VFX.md](VFX.md)）。

## BounceTarget 映射邏輯
`PlayerController` 在發射時將 `BounceTarget` 語意值轉為 `NonBounceLayers`：
* `Environment`（反彈障礙物）→ `NonBounceLayers = EnemyLayer`
* `Enemy`（反彈怪物）→ `NonBounceLayers = EnvLayer`
* `None`（不反彈）→ `NonBounceLayers = EnvLayer | EnemyLayer`
