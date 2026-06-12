# 彈道系統 (Sorrows.Ballistics)

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
| `IsOrbital` / `OrbitalRadius` / `OrbitalCount` | 是否環繞 / 環繞半徑 / 環繞數量 |

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
* `HashSet<int> _hitObjects`：防止同一幀對同一目標重複觸發命中。
* `_isDestroyed` 旗標：`Destroy` 呼叫後立即阻止同幀繼續執行命中邏輯。
* 穿透邏輯：命中目標在 `PierceableLayers` 內時，若 `PierceCount > 0` 則不銷毀並遞減；若 `PierceCount < 0`（例如 -1）則不銷毀且不遞減（無限穿透）。
* 存活時間：`LifeTime < 0`（例如 -1）時不因時間銷毀；否則每幀倒數，歸零時銷毀。
* `OnGroundLanded` 事件：拋物線彈抵達落點時觸發（見 [GROUND_EFFECT.md](GROUND_EFFECT.md) 的拋物線章節）。

## IBulletBehavior（行為介面）
| 行為 | 說明 |
|------|------|
| `BounceBehavior` | 牆壁反彈（`Vector2.Reflect`），命中 `NonBounceLayers` 內的目標時不反彈 |
| `SplitBehavior` | 扇形分裂，支援 OnSpawn / OnHit / OnDeath 三種觸發時機 |
| `RotationBehavior` | 飛行中持續自轉 |
| `OrbitalBehavior` | 以指定 Transform 為圓心環繞飛行，穿透時繼續環繞，反彈時脫軌飛出 |
| `ParabolicBehavior` | 接管移動的拋物線（假高度視覺、飛行中不撞 layer）；見 [GROUND_EFFECT.md](GROUND_EFFECT.md) |
