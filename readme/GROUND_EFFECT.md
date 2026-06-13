# 地面特效系統 (Ground Effect System) ＋ 拋物線型武器

> 返回 [文件總覽](README.md)

純主遊戲端的「鏈式觸發」AOE 系統，與彈道系統完全分離。子彈命中怪物時可額外在命中點生成「停留型」地面特效（火焰燃燒、毒霧、冰結等），擁有自己的範圍、存活時間、傷害節拍與循環動畫。拋物線型武器是「地面特效的觸發載體」，因此一併記在這裡。

## 配置檔案
* `Assets/Data/GroundEffectTable.csv`：定義每個地面特效的範圍、存活、傷害、動畫、tile 尺寸
  * 欄位：`ID, Name, Radius, Duration, DamageInterval, Damage, AniPath, AniNumber, AnimFPS, TileSize`
  * `DamageInterval = 0`：生成瞬間單次爆裂；`> 0`：每 N 秒週期 DOT
  * `Duration = -1`：永久存在直到外部銷毀
  * `TileSize`：單個 tile 的世界尺寸（同時是格子間距），預設 1
* `Assets/Resources/Prefabs/GroundEffect/GroundEffect.prefab`：渲染容器（純 SpriteRenderer + GroundEffectInstance）。Prefab 上的 SpriteRenderer 只當 `sortingLayer / sortingOrder / material` 範本，自身不顯示任何 sprite

## 鏈式觸發
* `RecipeTable.csv` 新增三欄：`GroundEffectID`、`GroundEffectTrigger`、`GroundEffectHitTarget`
* `RecipeManager` 解析後存到 `RecipeEntry.GroundEffectID` / `RecipeEntry.GroundEffectTrigger` / `RecipeEntry.GroundEffectHitTarget`
* `PlayerController.HandleBulletHit`：先用命中目標的 `GameObject.layer` 對 `EnemyLayer` / `EnvLayer` 做位元 AND 判斷，傷害仍只在怪物上結算；接著呼叫 `TryTriggerGroundEffect` 依 `GroundEffectHitTarget`（`Enemy` / `Environment` / `Any` / `Ground`）過濾，符合條件才呼叫 `GroundEffectManager.Spawn(id, hit.point)`
* 拋物線武器走另一條路：`HandleParabolicLanded` 帶 `hitGround = true` 進 `TryTriggerGroundEffect`，搭配 `GroundEffectHitTarget = Ground` 才會觸發
* 首版只實作 `OnHit`，`OnSpawn` / `OnDeath` 會在 Console 印出 Warning（待事件鉤子補完）
* `GroundEffectHitTarget` 與 `BounceTarget` 是**獨立**兩個概念（前者控觸發位置、後者控反彈表面），可自由組合：例如 `BounceTarget=Environment` + `GroundEffectHitTarget=Enemy` 表示「子彈在牆上反彈、打到怪物才放火」
* **發射時的武器是「快照」**：`PlayerController.ShootNormal` / `ShootOrbital` / `ShootParabolic` 把當下武器以 lambda closure 鎖在 `OnBulletHitObject` 或 `OnGroundLanded` callback 內，子彈命中／落地時用的是「發射時的武器」，**不是當下武器**；玩家中途切武器不會讓舊子彈誤觸新武器的傷害值或地面特效。分裂彈 / 環繞彈 / 拋物線彈都繼承同一份快照

## 拋物線型武器（IsParabolic）
* `RecipeTable.csv` 新增 5 欄：`IsParabolic`、`ArcHeight`、`LaunchSource`、`LandingScatterRadius`、`BlastRadius`
* `ProjectileData.cs` 新增對應字段，`CreateBehaviors` 在 `IsParabolic = 1` 時組裝 `ParabolicBehavior`
* **`Speed` 欄位語意改變**：拋物線下 `Speed` 解讀為**飛行時間（秒）**，**不是**速度。`Speed = 1` → 不論起點到落點多遠，都飛 1 秒抵達；遠的飛快、近的飛慢。`ParabolicBehavior` 把這個值直接當 `flightDuration` 用，已不再做 `distance / speed` 計算
* `ParabolicBehavior`（彈道系統）：完全接管移動，`OnSpawn` 把 `CollisionMask = 0` / `LifeTime = -1` / `Velocity = 0`，飛行中不撞任何 layer。地面位置線性插值，視覺加 `4 * ArcHeight * t * (1 - t)` 的 Y 偏移製造弧線；抵達目標時呼叫 `instance.RaiseGroundLanded(landPos)` 並把 `LifeTime` 設為 0 讓 `BulletInstance` 自動清理
* `BulletInstance.cs` 新增 `Action<BulletInstance, Vector2> OnGroundLanded` 事件，`PlayerController.ShootParabolic` 在 Spawn 後訂閱
* `PlayerController.ShootParabolic`：
  - **吃 `SpreadCount` / `SpreadAngle`**：以「玩家 → 滑鼠」為基準軸，N 顆炸彈在 ±SpreadAngle/2 範圍內等角度分布，扇形目標到玩家距離 = 玩家到滑鼠距離（拋物線**不需要**填 `SplitTiming`，是另一條獨立分裂路徑，不走 SplitBehavior）
  - **吃 `LandingScatterRadius`**：每顆炸彈在自己的扇形目標 + `Random.insideUnitCircle * 半徑` 內找一個落點（圓盤內均勻分布），多顆獨立隨機，避免堆疊在同一點
  - 為了讓 `SpreadCount` / `SpreadAngle` 在拋物線下生效，`RecipeManager.LoadRecipes` 改為**始終**把 SpreadCount / SpreadAngle 寫入 `ProjectileData`（不再只在 HasSplit 路徑下記錄）
* `PlayerController.ResolveParabolicStartPos`：`LaunchSource = Player` 從玩家位置出發；`Offscreen` 用攝影機 `orthographicSize × aspect` 算 viewport 邊界，從攝影機中心射隨機方向找出視野邊界距離 + 1 單位緩衝；多顆炸彈時 `Offscreen` 每顆都**獨立重抽**起點
* **落地殺傷 `BlastRadius`（新增）**：留空 / 0 時拋物線飛行中與落地都**不對怪物造成傷害**（純地面特效觸發載體，原行為）；填 > 0 時，**落地瞬間**以**武器表 `Damage`** 對落點 `BlastRadius` 半徑內怪物做一次性 AOE 殺傷（`Physics2D.OverlapCircleAll` 打 `EnemyLayer`，吃怪物無敵時間、擊退方向由爆心朝外）。在 `PlayerController.HandleParabolicLanded` 結算，**不碰彈道系統**（維持「彈道絕不算傷害」邊界，`BlastRadius` 存在主遊戲側的 `RecipeEntry`）。
* **`BlastRadius` 與地面特效獨立、可並存**：一顆炸彈可以「落地炸一次（BlastRadius，瞬間）＋ 再留一灘火延燒（GroundEffect 的 `Damage` / `DamageInterval`，持續）」；也可只要其中一個。兩者傷害分別計算（互不串接）
* 互斥：與 `IsOrbital` 互斥（同時填 1 行為衝突）；`PierceCount` / `BounceTarget` / `MaxBounces` 在拋物線下無意義（飛行中不參與命中）；`SplitTiming` / `SubRecipeID` 不建議混搭（拋物線自己處理 SpreadCount，SplitBehavior 的 OnHit 不會觸發、OnSpawn 又會再炸一輪）

> **地刺類武器不在這裡**：早期曾用「沿線連續生成 GroundEffect」做地刺（IsGroundWave），但地表特效不會飛、吃不到反彈/分裂等行為，已移除。地刺改用「軌跡特效」——一顆隱形子彈沿路種尖刺 Vfx，自動繼承所有彈道行為。見 [BALLISTICS.md](BALLISTICS.md) 的 `OnTrailPoint` 與 [VFX.md](VFX.md) 的 `TrailEffectID`。

## 執行單元
* `GroundEffectManager`（場景單例，掛在主物件上）：CSV 載入、序列圖預載、`Spawn(id, position)` API
* `GroundEffectInstance`（掛在 Prefab 上）：採 **真實圓形掃描（aligned scanline）**渲染——以原點為中心掃整數網格 `(i, j)`，當 `(i*TileSize)² + (j*TileSize)² ≤ Radius²` 就放一個 tile，嚴格對齊網格、上下左右對稱。每個 tile 動態 `AddComponent<SpriteRenderer>` 並繼承 prefab 範本的排序設定，動畫由父物件統一切幀同步顯示。**鋪面範圍嚴格貼齊 Radius**（與 `OverlapCircleAll` 傷害判定一致）。圓滑度取決於解析度：`R / TileSize ≥ 4` 才看得出明顯圓形，例如 `R=1.5、TileSize=0.3` ≈ 81 顆呈圓形；`R=1.5、TileSize=1` 只有 3×3 = 9 顆是 resolution 限制。實際 tile 數 > 500 時會在 Console 印一次 LogWarning，但仍照生成
* 同一目標的 DOT 限流靠 `HitReactionHandler.IsInvincible`，地面特效本身不維護命中表
