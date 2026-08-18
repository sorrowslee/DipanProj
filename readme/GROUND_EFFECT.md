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

## 發光半徑（LightRadius）—— 讓地面特效真的照亮暗場景

`GroundEffectTable` 第 13 欄。留空 / `<= 0` ＝不發光；`> 0` 時特效生成的當下會在自己身上掛一顆 `LightSource`，成為場上的一盞燈。

> ⚠️ **這跟「畫一張發光的圖」完全是兩回事。**
> `RenderMode=Glow` 只是把那張圖畫亮，**不會讓周圍的地圖從黑暗中顯現**。真正照亮暗場景（`Atmosphere=2 幽暗` 那類）的是 `AtmosphereController` 的光源系統，而它收光源的唯一管道就是 `LightSource` 的靜態登記表。
> 想要「這個特效能照路」，就得填這一欄。

**生死自動**：`LightSource` 在 `OnEnable` 登記、`OnDisable` 退出，而物件銷毀前 Unity 一定會先呼叫 `OnDisable` → 特效消失的瞬間光就熄，不需要任何手動清理。`AtmosphereController` 一行都不用改。

**光跟著特效走**：`LightSource` 掛在特效本體上，所以像佛光那種每幀跟隨玩家的特效，光自然也跟著玩家。

**其餘參數**（亮度／搖晃／柔和度）不進 CSV，用 `LightSource` 的預設暖橘，要調改 `GroundEffectInstance.cs` 的 `EffectLightIntensity` / `EffectLightFlicker` / `EffectLightSoftness`。

**注意同框上限**：`AtmosphereController.MaxLights = 12`。會發光的地面特效會跟場上的火把／燈籠搶名額（排序鍵是「距離 − 半徑」，跟著玩家的特效距離 0，一定排得進去）。

### 現況：這一欄全表留空（2026-08-17）

玩家的照明目前仍走**原本的提燈**——`ItemTable` 8 佛光的 `LightRadius = 3.5`，裝備著就恆亮，與開不開火無關。本節這一欄**沒有任何一列在用**。

曾經試過「清空提燈、改由佛光的 `LightRadius` 提供，變成開火才有光」，測完覺得不好退回了。經過與怎麼再開起來見 **[FALLEN_BUDDHA_LIGHT.md](FALLEN_BUDDHA_LIGHT.md)**。

⚠️ 兩者**不要同時開**，否則會變成「常亮的提燈」＋「開火時再多一盞」兩層光疊在一起。

**一個待決事項**：裝備的照明是取「最大值」不是累加（`AtmosphereController.PlayerEquippedLightRadius()`：`if (d.LightRadius > max) max = d.LightRadius`）。若要做「多件發光裝備疊加照明範圍」，得先改成累加。


## 背景旋轉符號層（SigilPath）

在地面特效的圓上再疊一張**緩緩自轉的符號**（法陣／符文／卍字…）。**與 `RenderMode` 無關**——`Tile` / `Single` / `Glow` 三種模式都能掛，想要明滅發光卻不要符號就把欄位留空。

> 📌 **現況（2026-08-17）：機制完成，但目前沒有任何一列使用它**（`SigilPath` 全表留空）。
> 當初是為了「墮落佛光」做的，最後決定佛光回到原本的暖金光圈，這一層就閒置備用。
> 要啟用只要在 `GroundEffectTable` 那一列填圖路徑，**程式一行都不用動**。
> 完整經過與「怎麼把卍字開回來」見 **[FALLEN_BUDDHA_LIGHT.md](FALLEN_BUDDHA_LIGHT.md)**。

### 怎麼用
1. 準備一張**白色去背 PNG**（染色才會準），放進 `Resources/` 底下任一處，Texture Type 設為 **Sprite**
2. `GroundEffectTable.csv` 該列的第 12 欄 `SigilPath` 填路徑（相對 Resources、不含副檔名），例：`InitialStory/Manji`
3. 外觀參數在 `GroundEffectInstance.cs` 上方那組 `Sigil*` 常數

### 資料流
```
WeaponTable → RecipeTable(IsAura 等) → GroundEffectID → GroundEffectTable.SigilPath
```
符號綁在**最底層的「那張圓」**，不是綁武器類型。所以兩把武器就算都是 `IsAura`，只要指向不同的 GroundEffect 列，一個有符號一個沒有。
（第一版曾把它寫死在 `RenderMode=Glow` 分支裡，等於汙染了 Glow 的語意——以後任何想要明滅發光的特效都會被迫吃到一個佛教符號。已改成獨立欄位。）

### 參數（`GroundEffectInstance.cs`）
| 常數 | 預設 | 說明 |
|---|---|---|
| `SigilRadiusMul` | 0.95 | 符號直徑 = 特效直徑 × 此倍率。**綁 `Radius`**，改 CSV 的 `Radius` 符號自動跟著變 |
| `SigilRotateSpeed` | 32 | 度/秒，正 = 逆時針左旋。一圈約 11 秒（沿用開場墜落 `IntroFallController.ManjiRotateSpeed`） |
| `SigilColor` | (0.16, 0.05, 0.26) | 深紫近黑。**這是剪影的顏色，不是發光的顏色**（見下） |
| `SigilAlpha` | 0.85 | alpha 混合下＝**覆蓋率**，1 = 完全遮住地板 |
| `SigilBreathe` / `SigilBreatheHz` | 0.02 / 0.42 | 呼吸幅度與速度。**刻意不與本體明滅（1.1）同步**：燈火是搖曳的火，符號是常駐的法，同步閃會顯得廉價 |

### ⭐ 三個反直覺、但別改回去的設計
1. **符號走 alpha 混合，本體走加色——刻意不一樣。**
   符號與本體同位置、同色相、同時出現時，如果兩個都靠「比較亮」被看見，就是**零和的**：本體調亮符號消失、符號調實本體消失，調 alpha 永遠跳不出這個循環（實際調了三輪才想通）。
   解法是換維度：**符號吃光（暗剪影）、本體發光**。所以 `BuildSigil` **刻意不指定材質**，用預設的 `Sprites-Default`（alpha 混合）。接回 `AuraGlow` 就退回互相洗掉的狀態。
   附帶結論：**加色（`Blend One One`）永遠做不出「不透明」**，它只讓底下變亮、遮不住東西。要實心就必須 alpha 混合。
2. **排序是 `order + 1`（蓋在本體之上），不是 -1。**
   暗剪影畫在加色光的**下面**會被光直接填亮而消失，要吃光就得蓋在光上面。仍遠低於玩家：`AuraYSortBias` 0.3 × `MapDepthSort.SortScale` 100 = 低玩家 30 階，+1 翻不過去。統一在 `SetTilesSortingOrder` 內處理，Glow 與非 Glow 兩條路都涵蓋。
3. **符號 renderer 絕對不能加進 `_tileRenderers`。**
   那個清單每幀被 `TickAnimation` 換 sprite、被 `SetTilesSortingOrder` 統一排序——加進去會被本體的動畫幀蓋掉，也會跟本體同層而失去分層效果。

### 算大小要扣掉圖的透明留白
倍率是套在**整張圖**上，不是筆畫上。以 `Manji.png` 為例：
- 筆畫外接框只佔圖寬的 **71%**
- 佛光貼圖的可見圓大約到半徑 **0.80**（亮度衰減到中心的 20%）
- 所以筆畫實際半徑 = `Radius × 倍率 × 0.71`，要 ≤ `Radius × 0.80` 才落在圓內 → **倍率上限約 1.13**

### 載入
符號圖在 `GroundEffectManager` **載表時就 `Resources.Load<Sprite>` 好**存進 `GroundEffectData.SigilSprite`，生成特效時不再碰 Resources。**沒有 static 快取**，所以與「已關閉 Domain Reload」那組坑（[PROBLEMS.md](PROBLEMS.md) I8）無關。載不到只印一則 Warning 並略過這一層，不影響特效本體。
圖在 `Resources/` 底下，**不需要跑 Sync Map Assets**（那是給 `GameAssets` 的地圖素材管線用的）。


## 執行單元
* `GroundEffectManager`（場景單例，掛在主物件上）：CSV 載入、序列圖預載、`Spawn(id, position)` / `Spawn(id, position, damageOverride)` API
* `GroundEffectInstance`（掛在 Prefab 上）：採 **真實圓形掃描（aligned scanline）**渲染——以原點為中心掃整數網格 `(i, j)`，當 `(i*TileSize)² + (j*TileSize)² ≤ Radius²` 就放一個 tile，嚴格對齊網格、上下左右對稱。每個 tile 動態 `AddComponent<SpriteRenderer>` 並繼承 prefab 範本的排序設定，動畫由父物件統一切幀同步顯示。**鋪面範圍嚴格貼齊 Radius**（與 `OverlapCircleAll` 傷害判定一致）。圓滑度取決於解析度：`R / TileSize ≥ 4` 才看得出明顯圓形，例如 `R=1.5、TileSize=0.3` ≈ 81 顆呈圓形；`R=1.5、TileSize=1` 只有 3×3 = 9 顆是 resolution 限制。實際 tile 數 > 500 時會在 Console 印一次 LogWarning，但仍照生成
* **排序（火在地上物之上或之下）**：分兩種：
  * **地板型（tile 火/毒、靜態單圖）**：固定 `GroundEffectSortingOrder = 8`——**高於「可走地上物」(`MapLoader.WalkableObjectSortingOrder = 5`)、低於角色與一般（不可走）地上物（Y 排序帶）**。效果：火在**可踩的石板/地毯（可走物）之上**燃燒、卻在**祭壇/柱子（不可走立體物）與角色之下**。這正好用地上物的「可走與否」自動分了「火該在其上或其下」，不必逐一判斷。（曾試整團依中心 Y 進 Y 排序帶，但單一排序值會讓大範圍 AOE 後方 tile 也蓋過地上物，已改回固定值。）
  * **佛光（`RenderMode = Glow`，跟著玩家的光環）**：例外——依**中心 Y（跟著玩家）每幀進 Y 排序帶**（`ApplyAuraYSort`，帶小幅 `AuraYSortBias` 讓玩家畫在光環之上）。所以玩家走到祭壇**前面**時光環也在前、走到**後面**時被祭壇擋——和玩家同進退，而不是壓在地板層被祭壇一律蓋住。單張圖、無大範圍 tile 問題。（見 `MapDepthSort` / [PROBLEMS.md](PROBLEMS.md) 排序相關。）
* 同一目標的 DOT 限流靠 `HitReactionHandler.IsInvincible`，地面特效本身不維護命中表

## 半徑倍率（per-instance，2026-08-18）

`GroundEffectManager.Spawn(id, pos, damageOverride, visualScale, **radiusScale**)` 的第 5 個參數。

| 參數 | 影響 |
|---|---|
| `visualScale` | 只設 `transform.localScale` → **純視覺**，傷害仍走表格的 `Radius`。看得到不一定打得到 |
| `radiusScale` | **視覺與傷害一起**縮放（`GroundEffectInstance.Radius = _data.Radius × 倍率`），兩者永遠一致 |

要「看到的就是打得到的」一律用 `radiusScale`。目前唯一的使用者是**佛光**——它是「籠罩己身」的光環，
半徑接血統的體型倍率（見 [BLOODLINE.md](BLOODLINE.md)）；身體變大 1.5 倍而圈不變的話，
光暈會比身體還窄、縮在肚子上。

`SetRadiusScale(倍率)` 可以中途改：視覺整組重建、傷害下一拍就吃到新半徑。給「跟著玩家的光環」在玩家
體型改變時同步用。

> ⚠ **絕對不要就地改 `_data.Radius`。** `_data` 是 GroundEffectTable 的一列，**全遊戲共用同一個物件**，
> 改了會污染之後所有用到這個特效的地方（同 RecipeTable 共用配方的坑，見 [GEM_SOCKET.md](GEM_SOCKET.md)）。
> 倍率一律存在 instance 上，所有讀半徑的地方走 `Radius` 屬性。
>
> ⚠ `RebuildVisuals()` **刻意不重建 `BuildLight()`**：那支是 `AddComponent<LightSource>()` 加在自己身上
> （不是子物件），再叫一次會變成兩盞燈；而且發光半徑走 `LightRadius` 欄、不吃倍率，本來就不用重建。
