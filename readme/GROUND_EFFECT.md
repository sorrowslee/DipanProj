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

## 佛光型武器（IsAura：跟隨玩家的圓形 AOE）

「手持佛光、籠罩一圈、圓內怪物持續受傷」的近身光環武器。**本質＝一個會跟著玩家移動的 GroundEffect**——所以不另起一套系統，直接複用本檔的圓形 AOE 傷害與渲染。

* **為什麼能直接複用**：`GroundEffectInstance` 的視覺（tile / 單圖）是掛在自己 transform 底下的子物件，傷害每一拍即時讀 `transform.position` 做 `OverlapCircleAll`。所以**只要每幀把 instance 的 transform 設成玩家位置，視覺圈與傷害圈就一起跟著玩家移動**，GroundEffect 本體一行都不用改。
* **發射路徑**（`PlayerController.UpdateAura`，與 `UpdateLaser` 同級）：偵測到 `IsAura` 武器且按住攻擊 → 若場上沒有佛光就 `GroundEffectManager.Spawn(GroundEffectID, 玩家位置, 武器Damage)` 生一個、記住引用；之後每幀把它移到玩家身上。放開攻擊鍵 / 切武器 → `ClearActiveAura` 銷毀整個佛光（仿雷射的群組生命週期）。**不發射任何子彈、完全不碰彈道系統**。
* **旗標放主遊戲側**：`RecipeEntry.IsAura`（不是 `ProjectileData`）——因為佛光不經過彈道系統，正好守住「彈道系統絕不涉入」的邊界，跟 `BlastRadius` 一樣存在主遊戲側。`RecipeTable.csv` 新增 `IsAura` 欄（第 32 欄）；與 `IsOrbital` / `IsParabolic` / `IsLaser` 互斥。
* **圓的定義走 `GroundEffectID`**：佛光的半徑、傷害節拍（`DamageInterval`）、外觀、`Duration` 都寫在 `GroundEffectTable` 對應列。**佛光圓必須 `Duration = -1`**（永久；生死由 `PlayerController` 管理，按住維持、放開銷毀）。
* **傷害走武器表 `Damage`**：透過新增的 `GroundEffectManager.Spawn(id, pos, damageOverride)` / `GroundEffectInstance.Initialize(..., damageOverride)`——`damageOverride >= 0` 時改用此值取代 `GroundEffectTable.Damage`。所以同一張佛光圓，換武器調 `Damage` 就能改強度。`DamageInterval` 仍由 GroundEffectTable 控制（佛光建議 0.2~0.4 秒一拍）。
* **單圖外觀（`RenderMode = Single` / `Glow`）**：佛光圓用「一張縮放到直徑 `2*Radius` 的發光圓暈」呈現（而非 tile 鋪滿）。`GroundEffectTable` 的 `RenderMode` 欄：留空 / `Tile` = 既有 tile 鋪面（火堆/毒霧）；`Single` = 單圖（靜態）；`Glow` = 單圖 ＋ **加色發光 ＋ 燈火忽強忽弱明滅**（佛光現用此值）。`GroundEffectInstance.BuildSingleSprite` 依 sprite 實際世界尺寸動態縮放（與 PPU 無關），仍掛進 `_tileRenderers` 所以多幀圖照樣會播。佛光圖：`Resources/GroundEffect/buddhaLight/buddhaLight_01.png`（金色放射狀、半透明 RGBA、單幀）。
* **燈火明滅（`RenderMode = Glow`）**：護罩改用 `Custom/AuraGlow` 加色（`Blend One One`）發光材質（執行期建立、隨物件 `OnDestroy` 釋放），並在 `GroundEffectInstance.TickFlicker` 每幀以 **Perlin noise 慢漂 ＋ 快速正弦** 混出不規則明滅，調 `SpriteRenderer.color.a`（→ shader 亮度倍率，範圍 `FlickerMinAlpha`~`FlickerMaxAlpha` = 0.45~1.0、底亮度 `AuraIntensity` = 1.4），另以一條較慢的正弦做 **±3%（`ScalePulse`）呼吸縮放**。**全為純視覺：傷害仍走 `OverlapCircle` 嚴格按 `Radius`，不受縮放影響。** 想調明滅快慢/幅度改 `GroundEffectInstance.cs` 上方那幾個常數。<br>⚠️ `AuraGlow` 額外乘 `tex.a`，刻意不同於 `Custom/AdditiveGlow`：佛光貼圖透明區 RGB 非黑（150,108,52），若用 `AdditiveGlow` 會疊出一個方塊。
* **範圍含可破壞地上物**：傷害層沿用 `EnemyLayer | EnvironmentLayer`，所以佛光也會慢慢燒掉範圍內的可破壞家具（多半是想要的）。不會傷到玩家（玩家在 Player 層、不在傷害遮罩）。
* **範例**：武器 10「佛光」`Damage=1, RecipeID=21`；配方 21「佛光」`IsAura=1, GroundEffectID=2`（其餘彈道欄位填 0 / None，不發射子彈）；GroundEffect 2「佛光」`Radius=1.2, Duration=-1, DamageInterval=0.3, RenderMode=Glow, AniPath=GroundEffect/buddhaLight/buddhaLight`。
* **調整方向**：圈變大（讓快怪更難貼到玩家）→ GroundEffect 2 的 `Radius`（初始 1.2，約籠罩玩家全身）；傷害變高 → 武器 10 的 `Damage`；傷害更密 → GroundEffect 2 的 `DamageInterval` 調小；圈更亮/更淡 → 換 `buddhaLight_01.png`、調底亮度 `AuraIntensity`、或明滅範圍 `FlickerMinAlpha`/`FlickerMaxAlpha`；明滅快慢/呼吸幅度 → `GroundEffectInstance.cs` 上方常數（`ScalePulse` 等）。

## 執行單元
* `GroundEffectManager`（場景單例，掛在主物件上）：CSV 載入、序列圖預載、`Spawn(id, position)` / `Spawn(id, position, damageOverride)` API
* `GroundEffectInstance`（掛在 Prefab 上）：採 **真實圓形掃描（aligned scanline）**渲染——以原點為中心掃整數網格 `(i, j)`，當 `(i*TileSize)² + (j*TileSize)² ≤ Radius²` 就放一個 tile，嚴格對齊網格、上下左右對稱。每個 tile 動態 `AddComponent<SpriteRenderer>` 並繼承 prefab 範本的排序設定，動畫由父物件統一切幀同步顯示。**鋪面範圍嚴格貼齊 Radius**（與 `OverlapCircleAll` 傷害判定一致）。圓滑度取決於解析度：`R / TileSize ≥ 4` 才看得出明顯圓形，例如 `R=1.5、TileSize=0.3` ≈ 81 顆呈圓形；`R=1.5、TileSize=1` 只有 3×3 = 9 顆是 resolution 限制。實際 tile 數 > 500 時會在 Console 印一次 LogWarning，但仍照生成
* 同一目標的 DOT 限流靠 `HitReactionHandler.IsInvincible`，地面特效本身不維護命中表
