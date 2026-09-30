# 彈道系統 (Sorrows.Ballistics)

> ⚠ **2026-08-26 RecipeTable 大改**：`IsXxx` 旗標已收成一欄 `Mode`、`BeamRange→Range`、`BlastRadius→AreaRadius`、拋物線飛行秒數獨立成 `FlightTime`、連鎖跳數獨立成 `ChainCount`、錐角／吸附半徑獨立成 `AimConeAngle`／`SnapRadius`。本文提到的欄名已同步更新；完整欄位與各模式吃哪些欄以 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) 為準。

> 返回 [文件總覽](README.md)

獨立的彈道 Package，採用 Data-Driven（資料驅動）與 Strategy Pattern（策略模式）設計。**只管子彈生成／飛行／碰撞／行為，絕不算傷害**（邊界規範見 [ARCHITECTURE.md](ARCHITECTURE.md)）。

> 雷射（`LaserBeam`）雖然也住在彈道系統內，但與「會飛的子彈」本質不同，獨立記在 [LASER.md](LASER.md)。

## ProjectileData（純 C# 類別）
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
| `Mode=Orbital` / `OrbitalRadius` / `OrbitalCount` | 是否環繞 / 環繞半徑 / 環繞數量 |
| `TrailStep` | 軌跡點間距（世界單位）；**>0** 時每飛這麼遠觸發一次 `OnTrailPoint`，0 = 無軌跡 |

## BallisticsEngine（靜態引擎）
```
Spawn(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit)
```
* 在子彈初始化前預先訂閱 `OnBulletHitObject` 事件（Pre-subscribe 模式），確保第 0 幀分裂彈不漏接。
* `Internal_SpawnSplit`：供分裂行為遞迴生成子彈，並繼承父彈的所有 LayerMask 設定與事件。
* `SpawnBeam(...)`：純程式生成雷射光束的工廠（見 [LASER.md](LASER.md)）。

## BulletInstance（子彈實體）
* 使用 `Physics2D.CircleCast` 做連續碰撞偵測（避免穿牆）。
* `CheckSpawnOverlap()`：生成時做一次 `OverlapCircle` 近距離檢查，處理子彈起點已在 Collider 內部時偵測不到的問題。
* `HashSet<int> _hitObjects`：**一顆子彈一輩子對同一個目標只回報一次命中**（穿透彈穿過一隻怪不會每幀都打它）。`ClearHitHistory()`（2026-09-30 加）清空它，之後同一目標可以再被打一次——迴旋每次轉向都呼叫。
* `_isDestroyed` 旗標：`Destroy` 呼叫後立即阻止同幀繼續執行命中邏輯。
* 穿透邏輯：命中目標在 `PierceableLayers` 內時，若 `PierceCount > 0` 則不銷毀並遞減；若 `PierceCount < 0`（例如 -1）則不銷毀且不遞減（無限穿透）。
* 存活時間：`LifeTime < 0`（例如 -1）時不因時間銷毀；否則每幀倒數，歸零時銷毀。
* `OnGroundLanded` 事件：拋物線彈抵達落點時觸發（見 [GROUND_EFFECT.md](GROUND_EFFECT.md) 的拋物線章節）。
* `OnTrailPoint` 事件（**沿路種特效的鉤子**）：`TrailStep > 0` 時，子彈每飛 `TrailStep` 世界距離就回報一次「經過此點」（用實際位移累計，故反彈/追蹤/分裂後的彎折路徑都能正確跟著種）。**彈道系統不知道種的是什麼**（尖刺／火痕…），由主遊戲在 callback 內決定（例如 `VfxManager.Spawn`），維持解耦。分裂出的子彈會繼承父彈的 `OnTrailPoint` 與 `TrailStep`。**地刺類武器**＝隱形子彈（無飛行圖自動隱形）＋沿路種尖刺 Vfx，因此自動吃滿反彈/分裂/穿透/追蹤等所有彈道行為。`Spawn(...)` 末尾參數 `onTrailPoint` 接此事件。

## IBulletBehavior（行為介面）
| 行為 | 說明 |
|------|------|
| `BounceBehavior` | 牆壁反彈（`Vector2.Reflect`），命中 `NonBounceLayers` 內的目標時不反彈 |
| `SplitBehavior` | 扇形分裂，支援 OnSpawn / OnHit / OnDeath 三種觸發時機 |
| `RotationBehavior` | 飛行中持續自轉 |
| `OrbitalBehavior` | 以指定 Transform 為圓心環繞飛行，穿透時繼續環繞，反彈時脫軌飛出 |
| `ParabolicBehavior` | 接管移動的拋物線（假高度視覺、飛行中不撞 layer）；見 [GROUND_EFFECT.md](GROUND_EFFECT.md) |
| `BoomerangBehavior` | 迴旋：直線飛 range → 每幀追擁有者飛回 → 還有趟數就穿過擁有者往身後再飛（鐘擺）→ 最後一趟碰到擁有者銷毀；每次轉向清命中名單；壽命自己管（每趟保險上限、超時淡出）。見下節 |

## LaneBehavior（平行彈，2026-08-26）

`Behaviors/LaneBehavior.cs`：出生時加一個側向速度、在 `duration` 秒內線性衰減到 0（側向初速＝2×偏移÷duration）。位移走速度向量，所以 `BulletInstance` 的 CircleCast 碰撞照常。由發射端透過 `BallisticsEngine.Spawn(..., extraBehavior: () => new LaneBehavior(...))` 工廠掛上；`Internal_Create` 把工廠存進 `BulletInstance.SpawnExtraBehavior`，`OnSpawn` 期間（`IsSpawning`）分裂出的子彈由 `Internal_SpawnSplit` 繼承同一個工廠——整排平行彈連同它們的 OnSpawn 分裂一起散開。設計理由與用法見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) §3.14。

## BoomerangBehavior（迴旋，2026-09-30）

`Behaviors/BoomerangBehavior.cs`：建構參數 `(Transform owner, Func<Vector2> returnPoint, float range, int trips)`，同 LaneBehavior 由發射端用 `extraBehavior` 工廠掛上（每顆子彈一個實例、有狀態）。

- **三段式軌跡**（軸＝第一趟發射方向，整把不變）：
  - **Out**：淚滴前半，前進 `range·(1−cosθ)/2`、側向（左正）`k·sinθ·(1−cosθ)`，θ 0→π；錨點固定在出手點。總寬＝`range × WidthRatio(0.5)`。
  - **Loop**（趟數−1 個半圈）：以 `_center` 為中心的橢圓 `σ·(axis·range·cos t − left·b·sin t)`，t 0→π，從 σ 那頭的終點順時針繞到另一頭；每半圈 σ 反號。中心以時間常數 `CenterFollowSeconds(0.35)` 平滑追 `returnPoint()`（進入時中心＝出手點，位置連續）。
  - **Return**：淚滴後半（軸＝σ·axis），θ π→2π；錨點 smoothstep 從開始收回時的中心漸移到擁有者，θ＝2π 正好在擁有者身上 ⇒ 銷毀。
- **接點連續**：淚滴在終點的切線是橫向、橢圓在端點的切線也是橫向（同方向）；**半短軸 `b = 2√2·k`** 讓橢圓端點曲率半徑 `b²/range` 等於淚滴終點的 `8k²/range`，連彎度都接得上。
- **為什麼不是每趟一個淚滴**（第二版）：尖端固定在擁有者身上，每趟都要急轉、對準身體中心穿過，看起來像「減速後硬擠過去」（作者：像蝴蝶在飛）。中間趟改繞擁有者轉，就只剩最後收回那一個尖端。
- **等速推進**：每幀「小步長倍增 → 二分」求沿路徑前進 `speed·dt` 的參數（`Advance`）。⚠ 不能用導數線性估計（淚滴尖端附近前進量與 Δθ 是二次關係，會連續幾幀減速到 1/15）；不能直接拿「到段尾的弦長」判斷（淚滴整圈起點＝終點）。一段走完剩下的步長帶進下一段（一幀最多跨三段）。
- **速度下限**：Loop 與 Return（θ>π）段若中心／錨點被擁有者往後拖、抵掉前進，改用世界距離二分（`AdvanceWorld`）讓實際速度不低於 speed。
- **命中名單**：每經過一個終點、以及 Loop 過 t＝π/2（掠過擁有者身旁）各 `ClearHitHistory()` 一次；趟數 N ⇒ 共 2N−1 次。
- **保險**：每段上限 `(段長/speed)·TripTimeSlack(2) + TripTimeExtra(0.5)`（淚滴半段長與半橢圓長在 OnSpawn 積分一次）；超時或 owner 被銷毀 ⇒ 淡出 `FadeSeconds(0.25)`（`CollisionMask = 0`、速度與 alpha 線性歸零）。暫停（dt＝0）時不動速度（設 0 會讓 BulletInstance 把圖轉到 0 度）。
- **穿牆／穿怪不是它做的**：主遊戲給的 `PierceCount = -1`、`PierceableLayers` 含環境層，所以 `OnHit` 永遠回 false。
- ⚠ 既有限制（不是迴旋特有）：`CircleCast` 每幀只取第一個命中物；**子彈正在穿過某個東西（牆、已命中的怪）時，它背後貼著的目標要等穿出來才會被偵測到**。迴旋全程穿牆，比一般穿透彈更常遇到。

