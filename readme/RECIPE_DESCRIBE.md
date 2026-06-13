# 配方表 (RecipeTable.csv) 欄位說明與使用教學

本文件詳細說明 `DipanProj_Main/Assets/Data/RecipeTable.csv` 中每個欄位的用途、填寫規則與實際範例。

---

## 欄位總覽

| 欄位 | 型別 | 必填 | 說明 |
|------|------|------|------|
| ID | 整數 | 是 | 配方唯一識別碼，武器表透過此 ID 引用配方 |
| Name | 字串 | 是 | 配方名稱，供人閱讀辨識用，程式不使用 |
| Speed | 小數 | 是 | 子彈飛行速度（單位/秒） |
| Radius | 小數 | 是 | 子彈碰撞判定半徑，用於 `Physics2D.CircleCast` |
| LifeTime | 小數 | 是 | 子彈存活時間（秒），超過即自動銷毀；**-1** 表示不因時間銷毀 |
| FireInterval | 小數 | 是 | 發射間隔（秒），控制射速 |
| RotationSpeed | 小數 | 是 | 子彈飛行時的自轉速度（度/秒），0 為不旋轉 |
| PierceCount | 整數 | 是 | 穿透次數，0 為不穿透；**-1** 表示無限穿透（不遞減） |
| SpreadCount | 整數 | 是 | 散射/分裂數量，1 為不分裂 |
| SpreadAngle | 小數 | 是 | 散射總角度（度），子彈在此角度範圍內均勻展開 |
| SplitTiming | 字串 | 否 | 分裂觸發時機，僅在 SpreadCount > 1 時有效 |
| SubRecipeID | 整數 | 否 | 分裂後子彈使用的配方 ID，空白表示繼承自身屬性 |
| BounceTarget | 字串 | 是 | 反彈對象，決定子彈碰到什麼會反彈 |
| MaxBounces | 整數 | 是 | 最大反彈次數，僅在 BounceTarget 非 None 時有效 |
| HomingTurnSpeed | 小數 | 否 | 追蹤轉向速度（度/秒），0 或留空為不追蹤 |
| IsOrbital | 整數 | 否 | 是否為環繞型彈道（1 = 是，留空或 0 = 否） |
| OrbitalRadius | 小數 | 否 | 環繞半徑，僅在 IsOrbital = 1 時有效 |
| OrbitalCount | 整數 | 否 | 環繞數量，每次發射生成幾顆環繞子彈 |
| BlockedByEnvironment | 整數 | 否 | 子彈是否會被地形障礙物阻擋（1 或留空 = 會被擋，0 = 穿透地形不被銷毀） |
| GroundEffectID | 整數 | 否 | 命中時鏈式觸發的地面特效 ID，引用 `GroundEffectTable.csv` 對應列；留空或 0 = 不觸發 |
| GroundEffectTrigger | 字串 | 否 | 地面特效觸發時機（`OnSpawn` / `OnHit` / `OnDeath`），目前僅實作 `OnHit`；留空 = `OnHit` |
| GroundEffectHitTarget | 字串 | 否 | 地面特效命中過濾（`Enemy` / `Environment` / `Any` / `Ground`），決定打到哪類 layer 才生成；留空 = `Enemy`，與 BounceTarget 獨立 |
| IsParabolic | 整數 | 否 | 是否為拋物線型彈道（1 = 是，留空或 0 = 否；與 IsOrbital 互斥） |
| ArcHeight | 小數 | 否 | 拋物線弧頂的視覺高度（世界單位，假高度），僅在 IsParabolic = 1 時有效 |
| LaunchSource | 字串 | 否 | 發射來源（`Player` / `Offscreen`），`Offscreen` 從攝影機視野外隨機方向飛入；留空 = `Player` |
| LandingScatterRadius | 小數 | 否 | 拋物線專用：落點隨機半徑（世界單位）。每顆炸彈的最終落點 = 扇形目標 + `Random.insideUnitCircle * 半徑`；留空或 0 = 不隨機 |
| IsLaser | 整數 | 否 | 是否為持續型雷射光束（1 = 是，留空或 0 = 否；與 IsOrbital / IsParabolic 互斥） |
| dotInterval | 小數 | 否 | 雷射專用：傷害節拍（秒）。光束每 N 秒對當下掃到的所有目標各結算一次傷害（傷害值取武器表 Damage）；留空 = 0.5 |
| BeamRange | 小數 | 否 | 雷射專用：光束最大射程（世界單位）。Speed / LifeTime 對光束無意義，改用此欄位限制長度；留空 = 20 |
| BlastRadius | 小數 | 否 | 拋物線專用：落地殺傷半徑（世界單位）。留空 / 0 = 落地不傷害；> 0 = 落地瞬間以武器表 Damage 對半徑內怪物炸一次。與地面特效獨立、可並存 |
| TrailStep | 小數 | 否 | 軌跡點間距（世界單位）：> 0 時子彈每飛這麼遠就沿路種一個特效（搭配武器表 `TrailEffectID`）。子彈反彈/分裂/追蹤後的彎折路徑都會跟著種。地刺武器靠這個沿路長出尖刺。0 或留空 = 無軌跡 |

---

## 各欄位詳細說明

### ID
- 配方的唯一編號，不可重複
- 武器表（WeaponTable.csv）的 `RecipeID` 欄位會引用這個 ID
- 其他配方的 `SubRecipeID` 也會引用這個 ID

### Name
- 純粹供開發者辨識用的名稱
- 程式不會讀取或顯示這個值，寫中文英文都可以

### Speed（飛行速度）
- 數值越大，子彈飛越快
- 建議範圍：5 ~ 100
- 參考值：10 = 慢速、20 = 一般、40 = 高速

### Radius（碰撞半徑）
- 子彈的碰撞判定大小，影響「打不打得到」
- 數值越大，判定範圍越寬，越容易命中
- 建議範圍：0.05 ~ 0.5
- 參考值：0.1 = 標準子彈

### LifeTime（存活時間）
- 子彈在場上存活的最大秒數
- 超過時間後自動銷毀，避免子彈飛出地圖外永遠不消失
- 設為 **-1**：不因時間銷毀（可與碰撞、分裂等機制搭配）
- 建議範圍：1 ~ 10（一般子彈）
- 參考值：3 = 一般用途

### FireInterval（發射間隔）
- 兩次發射之間的最短等待時間（秒）
- 數值越小，射速越快
- 參考值：0.1 = 極高射速、0.2 = 快速、0.5 = 中速、1.0 = 慢速
- 計算射速：每秒發射次數 = 1 / FireInterval（例如 0.2 = 每秒 5 發）

### RotationSpeed（自轉速度）
- 子彈飛行時繞自身中心旋轉的速度（度/秒）
- 純視覺效果，不影響飛行軌跡
- 設為 0 表示不旋轉
- 參考值：360 = 每秒轉一圈、720 = 每秒轉兩圈

### PierceCount（穿透次數）
- 子彈可以穿過多少個目標（怪物）而不被銷毀
- 設為 0：碰到任何目標就銷毀
- 設為 1：穿過第一個目標，碰到第二個才銷毀
- 設為 **-1**：無限穿透，每次命中可穿透層目標時都不銷毀且不遞減計數
- 穿透只作用於 `EnemyLayer`（怪物層），碰到牆壁仍會停下（除非有反彈）

### SpreadCount（散射數量）
- 控制一次發射產生幾顆子彈
- 設為 1：正常發射一顆子彈（不分裂）
- 設為 3：展開成 3 顆子彈（散彈效果）
- **注意**：當 SpreadCount > 1 時，必須填寫 SplitTiming 欄位

### SpreadAngle（散射角度）
- 散射子彈展開的總角度（度）
- 子彈會在此角度範圍內均勻分布
- 例如 SpreadCount=3、SpreadAngle=60：三顆子彈分布在 -30°、0°、+30°
- 當 SpreadCount = 1 時，此欄位無效

### SplitTiming（分裂時機）
- 控制分裂在什麼時候發生
- 可用的值：

| 值 | 說明 |
|------|------|
| `OnSpawn` | 發射時立即分裂，原始子彈消失，展開成多顆子彈 |
| `OnHit` | 命中目標時分裂，產生額外的子彈 |
| `OnDeath` | 子彈存活時間結束時分裂 |
| （空白） | 不分裂，SpreadCount = 1 時留空即可 |

- **OnSpawn** 最常用於散彈（Shotgun）效果
- **OnHit** 可做出「命中後爆裂」的效果
- **OnDeath** 可做出「延時炸彈」的效果

### SubRecipeID（分裂子彈配方 ID）
- 指定分裂後產生的子彈使用哪個配方
- 留空：分裂子彈繼承父彈的所有屬性（速度、半徑、存活時間等），但不會再次分裂
- 填寫 ID：分裂子彈使用指定配方的屬性（可以做出「先散射、再反彈」等組合效果）

### BounceTarget（反彈對象）
- 決定子彈碰到什麼東西會反彈

| 值 | 說明 |
|------|------|
| `None` | 不反彈，碰到任何東西都停下或銷毀 |
| `Environment` | 碰到牆壁/障礙物會反彈，碰到怪物不反彈 |
| `Enemy` | 碰到怪物會反彈，碰到牆壁不反彈 |

### MaxBounces（最大反彈次數）
- 子彈最多反彈幾次
- 僅在 BounceTarget 不是 None 時有效
- 超過次數後，子彈碰到反彈對象會直接銷毀

### HomingTurnSpeed（追蹤轉向速度）
- 子彈自動追蹤最近目標的轉向速度（度/秒）
- 設為 0 或留空表示不追蹤
- 數值越大，轉向越靈敏
- 參考值：90 = 慢速追蹤、180 = 一般追蹤、360 = 高速追蹤

### IsOrbital（是否為環繞型彈道）
- 設為 1：子彈以玩家為圓心環繞飛行
- 留空或設為 0：一般直線飛行（預設）
- 每次觸發發射（含依 `FireInterval` 的連射）時，會先清除該玩家上一輪仍在場上的環繞子彈，再重新生成一組
- 環繞型子彈使用 `Speed` 欄位作為切線速度（繞圈移動速度），半徑越小、Speed 越大，轉得越快
- **群組生命週期**：同一輪 `OrbitalCount` 顆環繞彈由 `PlayerController` 接管統一銷毀。每顆子彈內部的 `LifeTime` 會被覆寫為 `-1`（不自動超時），改由 `PlayerController` 記錄群組到期時間（= `Time.time + recipe.LifeTime`），時間到時將整組一次 `Destroy`，確保所有環繞彈一起出現、一起消失，不會因單顆子彈的個別事件而錯位
- 若 `LifeTime` 設為 `-1`，群組就不會自動到期，環繞彈會持續存在直到下一次發射或玩家銷毀時被清掉

### OrbitalRadius（環繞半徑）
- 子彈繞玩家飛行的軌道半徑
- 僅在 IsOrbital = 1 時有效
- 數值越大，軌道越寬
- 參考值：1 = 緊貼玩家、2 = 一般距離、5 = 遠距離

### OrbitalCount（環繞數量）
- 每次發射生成幾顆環繞子彈
- 子彈會等距排列在軌道上（例如 3 顆 = 每隔 120° 一顆）
- 僅在 IsOrbital = 1 時有效

### BlockedByEnvironment（是否被地形阻擋）
- 控制子彈碰到地形障礙物（`EnvLayer`）時是否會被銷毀
- 留空或設為 `1`：會被地形擋下（預設行為，向下相容既有配方）
- 設為 `0`：地形被加入「可穿透層（PierceableLayers）」，子彈碰到地形不會被銷毀
- **注意**：此設定「不會被銷毀」的判斷會走穿透邏輯，因此需要 `PierceCount` 為 `-1`（無限穿透）或 `> 0` 才能持續穿過地形；若 `PierceCount = 0`，仍會在第一次命中地形時被銷毀
- 子彈穿過地形時仍會觸發一次 `OnBulletHitObject` 事件（之後對同一片地形不會再觸發），預留地形擊中特效擴充用
- 此欄位適用於所有配方，不只環繞彈；最常用於 `IsOrbital = 1` 搭配 `PierceCount = -1` 的環繞武器，讓護盾型彈道不會卡在牆邊

### GroundEffectID（地面特效鏈式觸發）
- 設為 `0` 或留空：不觸發地面特效（預設）
- 設為 `> 0` 的整數：對應到 `Assets/Data/GroundEffectTable.csv` 內的同 ID 地面特效，當該配方的子彈命中**符合 `GroundEffectHitTarget` 過濾條件**的目標時，會在命中位置生成該地面特效
- 地面特效是「主遊戲端」的獨立系統，跟彈道系統分離；位置 = 命中點，傷害、範圍、動畫由 `GroundEffectTable` 自行定義
- 觸發後子彈本身的傷害、穿透、反彈、分裂等行為**完全不受影響**（地面特效是額外附加效果）

### GroundEffectTrigger（地面特效觸發時機）
- 與 `GroundEffectID` 搭配使用，留空時預設為 `OnHit`
- 可用值：

| 值 | 說明 |
|------|------|
| `OnHit` | 子彈命中時觸發（首版唯一支援的時機；命中目標還會經 `GroundEffectHitTarget` 過濾） |
| `OnSpawn` | 子彈生成時觸發（**目前未實作**，填寫會在 Console 印出 Warning） |
| `OnDeath` | 子彈存活結束時觸發（**目前未實作**，填寫會在 Console 印出 Warning） |

- 注意：`OnHit` 依賴 `BulletInstance` 的「同目標只觸發一次 `OnBulletHitObject`」機制，因此一顆子彈撞同一個物件只會生成一次地面特效；穿透時若打到不同目標會分別觸發。

### GroundEffectHitTarget（地面特效命中過濾）
- 與 `GroundEffectID` / `GroundEffectTrigger=OnHit` 搭配使用，控制**命中哪一類 layer 的目標**才會觸發地面特效
- 與 `BounceTarget` 是**獨立**的兩個概念（反彈 vs 觸發地面特效），可自由組合
- 留空時預設為 `Enemy`（沿用首版只認怪物的行為，向下相容）
- 可用值：

| 值 | 說明 |
|------|------|
| `Enemy`（預設） | 只有打到怪物（`EnemyLayer`）才觸發地面特效；打到牆不會放火 |
| `Environment` | 只有打到障礙物（`EnvLayer`）才觸發；可做「火油彈封路」之類玩法 |
| `Any` | 怪物或障礙物都會觸發 |
| `Ground` | **拋物線專用**：拋物線最終落地（所有彈跳結束）時觸發；非拋物線武器設此值會 no-op |

- 過濾邏輯：`PlayerController.HandleBulletHit` 取得命中目標的 `GameObject.layer`，與 `EnemyLayer` / `EnvLayer` 做位元 AND 比對；拋物線則是 `HandleParabolicLanded` 帶 `hitGround = true` 進來；再依本欄位決定是否呼叫 `GroundEffectManager.Spawn`
- 傷害結算與此欄位**無關**：傷害仍只發生在怪物上（牆沒有 HP），`Environment` / `Any` 設定下打到牆只會放出地面特效、不會結算傷害
- 仍受 `BulletInstance` 的「同目標只觸發一次」機制限制，因此一顆子彈撞同一面牆只會放一次地面特效

### IsParabolic / ArcHeight / LaunchSource / LandingScatterRadius / BlastRadius（拋物線型彈道）
- 啟用條件：`IsParabolic = 1`，與 `IsOrbital` **互斥**（兩個欄位都填 1 時，`ProjectileData.CreateBehaviors` 會把兩個 behavior 都加進去，行為衝突，請避免）
- 主要設計目的：**作為地面特效的觸發載體**，例如丟炸彈、丟油罐——飛行中不對任何目標造成傷害，落地時觸發 `GroundEffectHitTarget = Ground` 的地面特效
- **落地殺傷（`BlastRadius`）**：若要讓炸彈「落地當下就炸傷一圈怪」，填 `BlastRadius > 0`，落地瞬間以**武器表 `Damage`** 對半徑內怪物做一次性 AOE（吃怪物無敵時間、擊退由爆心朝外）。與地面特效**獨立**：可只炸傷、可只留火、也可兩者並存。留空 / 0 = 維持「落地不傷害」原行為

#### 行為流程
1. **發射**：依 `LaunchSource` 決定起點，目標永遠是滑鼠所在的世界座標
   - `Player`（預設）：從玩家當前位置發射
   - `Offscreen`：從攝影機 viewport 邊緣外 1 單位的「隨機方向」位置發射飛入；多顆炸彈時每顆都會**獨立重抽**一個視野外起點
2. **飛行**：`ParabolicBehavior` 在 `OnSpawn` 把 `BulletInstance.CollisionMask` 清成 `0`，整段飛行**不會撞到任何 layer**（也不會觸發 `OnBulletHitObject`）；地面位置由起點線性插值到目標，視覺上額外加 `4 * ArcHeight * t * (1 - t)` 的 Y 偏移製造弧線
3. **落地**：抵達目標時呼叫 `BulletInstance.RaiseGroundLanded(landPos)`，主遊戲收到後依 `GroundEffectHitTarget` 決定是否生成地面特效，並把 `LifeTime` 設為 0 讓 `BulletInstance` 下一幀清掉
4. **生命週期**：`LifeTime` 由本行為控制，CSV 的 `LifeTime` 欄位實質上不影響拋物線

#### 欄位對照（與一般彈不同）
- `Speed`：**飛行時間（秒）**，**不是**速度。`Speed = 1` 代表不論遠近、扇形哪一支，都用 1 秒抵達落點 → 同一發射出去的多顆炸彈會**同時落地**；遠的飛快、近的飛慢，這是預期行為
- `ArcHeight`：弧頂的「假高度」Y 偏移絕對值（世界單位）。直接寫 `2.5` 就是弧頂上抬 2.5 單位
- `LaunchSource`：發射來源；`Offscreen` 取攝影機 `orthographicSize` × `aspect` 算 viewport 邊界，從攝影機中心射隨機方向找到出視野的距離 + 1 單位緩衝
- `LandingScatterRadius`：**落點誤差半徑**（世界單位）。最終落點 = 扇形目標 + `Random.insideUnitCircle * 半徑`（圓盤內均勻分布），多顆炸彈時各自獨立隨機，避免堆疊在同一點。`0` 或留空 = 不隨機
- `BlastRadius`：**落地殺傷半徑**（世界單位）。`> 0` 時落地瞬間以**武器表 `Damage`** 對半徑內怪物炸一次（多顆炸彈各自在自己落點各炸一圈）。`0` 或留空 = 落地不傷害。Damage = 0 的炸彈即使填了 `BlastRadius` 也不會造成傷害（記得在武器表給 `Damage`）
- `SpreadCount`：**一發射出幾顆炸彈**。**重要**：拋物線版的分裂不需要 `SplitTiming`，留空也會生效（一般彈仍需要 `SplitTiming` 才會走 SplitBehavior）
- `SpreadAngle`：扇形總角度（度）。以「玩家 → 滑鼠」為基準軸，N 顆炸彈在 ±SpreadAngle/2 範圍內等角度分布；扇形目標到玩家的距離 = 玩家到滑鼠距離（看起來像一片弧形落點）
  - `SpreadCount = 3, SpreadAngle = 60`：三個目標分別在 -30° / 0° / +30° 方向、與滑鼠等距的扇形上
  - `SpreadCount = 1`：單顆，仍會吃 `LandingScatterRadius` 的隨機誤差

#### 與其他欄位的互動
- `PierceCount` / `BounceTarget` / `MaxBounces`：飛行中不參與 layer 命中，這些欄位**無意義**（即使有 BounceBehavior 也不會被觸發）
- `RotationSpeed`：仍會旋轉 sprite，做「翻滾炸彈」效果還滿合適
- `SpriteAngleOffset`：會跟著 velocity 方向轉；但拋物線 velocity 含 Y 高度分量，會在升弧時往上仰、降弧時往下指，看美術風格決定要不要設
- `IsOrbital`：互斥
- `SplitTiming` / `SubRecipeID`：拋物線**直接讀 `SpreadCount` / `SpreadAngle`** 自行展扇形，不走 SplitBehavior，因此 `SplitTiming` 留空即可；填了也會被 SplitBehavior 嘗試解析，但因為 OnHit 不會被觸發、OnSpawn 又會額外再炸一輪，**不建議混搭**
- `GroundEffectHitTarget = Ground`：**標準搭配**，落地放地面特效

#### 範例配方
```
12, 玩家丟出火焰拋物線彈, 1, 0.1, 10, 0.5, 0, 0, 5, 60, , , None, 0, 0, , , , , 1, OnHit, Ground, 1, 2.5, Player, 1.5
```
固定飛行時間 1 秒，弧高 2.5 單位；一次丟 5 顆，分布在玩家 → 滑鼠方向 ±30° 的扇形上、與滑鼠等距，並在每個扇形目標 1.5 單位半徑內隨機落點。落地後生成地面特效 ID 1（火焰燃燒）。

### TrailStep（軌跡特效 / 地刺類武器）
- 概念：`TrailStep > 0` 時，子彈每飛這麼遠就沿路「種」一個特效（由武器表 `TrailEffectID` 指定要種什麼）。**這是把「移動的載體」和「視覺」拆開**——載體是一顆正常子彈（吃滿 RecipeTable 行為），視覺是沿路種出的 Vfx。
- **地刺武器就是這樣做的**：一顆**隱形**（武器表不填飛行圖）的正常子彈，沿路每隔 `TrailStep` 種一根尖刺 Vfx。因為它是正常子彈，所以**自動繼承全部行為**：反彈 → 尖刺軌跡跟著折、分裂 → 尖刺分岔成多條、追蹤 → 尖刺蛇行咬向敵人、散射 → 一次多條尖刺線。
- 傷害：走**武器表 `Damage`**（子彈正常命中結算，建議 `PierceCount = -1` 讓整條線穿透所有敵人），**不是地表 DOT**。
- 分裂繼承：分裂出的子彈會繼承父彈的 `OnTrailPoint`（同一個尖刺 Vfx）與 `TrailStep`，所以分岔的每條線都會種刺。
- 與其他欄位：`Speed`（線推進速度）、`LifeTime`（線多長/多遠 = Speed×LifeTime）、`Radius`（命中寬度）、`BounceTarget`/`MaxBounces`、`HomingTurnSpeed`、`SpreadCount`/`SpreadAngle` 全部適用。`RotationSpeed`（自轉）對「種在地上的刺」沒意義，屬不相干欄位。
- 範例組合：武器「地裂刺」`RecipeID=19, Damage=3, TrailEffectID=3`（隱形、不填飛行圖）；配方 19 `Speed=16, Radius=0.3, LifeTime=0.6, PierceCount=-1, TrailStep=1.5`；VfxTable ID 3「地刺」= earthSpik 序列圖（`Scale=0.35, Loop=0`）。要加反彈就把配方 19 的 `BounceTarget` 填 `Environment`，尖刺線就會撞牆折射。

### GroundEffectTable.csv 欄位（簡述，獨立於本文件）
- `ID, Name, Radius, Duration, DamageInterval, Damage, AniPath, AniNumber, AnimFPS, TileSize`
- `Radius`：AOE 偵測半徑（`Physics2D.OverlapCircle` 用此值），**同時嚴格決定 tile 鋪面範圍**——tile 中心點落在 `Radius` 內才會保留
- `Duration`：地面特效存活秒數，`-1` = 永久（待外部銷毀）
- `DamageInterval`：`0` = 生成瞬間單次爆裂、之後不再傷害；`> 0` = 每 N 秒週期 DOT
- `Damage`：每次傷害的絕對值（不串接武器表 Damage，方便獨立調整）
- `AniPath / AniNumber / AnimFPS`：序列圖路徑前綴 / 張數 / 播放速度，與 `WeaponTable.csv` 同套規則，存活期間循環播放
- `TileSize`：單個 tile 的世界尺寸（同時是格子間距），留空或 `<= 0` 預設為 `1`
- **渲染採真實圓形掃描（aligned scanline）**：
  - 以原點為中心掃整數網格 `(i, j)`，當 `(i*TileSize)² + (j*TileSize)² ≤ Radius²` 就放一個 tile
  - 所有 tile 嚴格對齊網格（**無半步偏移**），上下左右對稱
  - 鋪面範圍與傷害判定都嚴格依 `Radius`，視覺上的圓形邊界 = 傷害的圓形邊界
  - **解析度決定圓滑度**：建議 `TileSize ≤ Radius / 4`，否則低解析度會看起來偏方塊
    - 範例 `Radius=1.5, TileSize=1` → 直徑跨 3 顆 → **3×3 = 9 顆**（看起來像方塊，正常現象）
    - 範例 `Radius=1.5, TileSize=0.5` → 直徑跨 6 顆 → **29 顆**（八邊形，勉強圓）
    - 範例 `Radius=1.5, TileSize=0.3` → 直徑跨 10 顆 → **~81 顆**（明顯圓形）
    - 範例 `Radius=1.5, TileSize=0.1` → 直徑跨 30 顆 → **~700 顆**（平滑圓形，會跳 Warning）
  - **效能保險**：當實際生成的 tile 數 > 500 時，`GroundEffectInstance` 會在 Console 印一次 `LogWarning`，但仍會照數量生成（不自動降級）
- **準備美術**：建議讓單張 sprite 的世界尺寸 ≈ `TileSize`（例如 `TileSize = 1` 時，PNG 100×100 px、PPU 100，native = 1×1），不然 tiles 會出現重疊或縫隙
- 同一目標的 DOT 限流靠 `MonsterController` 的 `HitReactionHandler.IsInvincible`（怪物無敵中刷不到傷害）

### 環繞型彈道與其他行為的交互

| 組合 | 效果 |
|------|------|
| 環繞 + 穿透 | 碰到怪物不消失，繼續環繞 |
| 環繞 + 反彈(Enemy) | 碰到怪物後脫軌，以反彈角度飛出 |
| 環繞 + 反彈 + 追蹤 | 碰到怪物脫軌反彈後，自動追蹤下一個目標 |
| 環繞 + 分裂(OnHit) | 碰到怪物時分裂出多顆子彈 |
| 環繞 + 自轉 | 環繞時武器自身旋轉（純視覺效果） |

---

## 範例配方

### 基礎直射彈
最簡單的子彈，直線飛行、碰到東西就停。

```
ID, Name,     Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces
10, BasicShot,    15,    0.1,        3,          0.3,             0,           0,           1,           0,            ,            , None,                  0
```

### 散彈（Shotgun）
一次射出 5 顆子彈，在 90 度扇形內展開。

```
ID, Name,    Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces
20, Shotgun5,   12,    0.1,        2,          0.8,             0,           0,           5,          90, OnSpawn,                , None,                  0
```

### 穿透雷射
高速、可穿透 3 個目標的直線攻擊。

```
ID, Name,  Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces
30, Laser,    60,   0.05,        5,          1.0,             0,           3,           1,           0,            ,            , None,                  0
```

### 彈跳球
碰到牆壁反彈，最多彈 5 次。

```
ID, Name,       Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces
40, BounceBall,    25,    0.1,        8,          0.5,           360,           0,           1,           0,            ,            , Environment,           5
```

### 分裂爆彈
子彈存活時間結束時，分裂成 8 顆散射彈（使用配方 ID=10 的 BasicShot）。

```
ID, Name,      Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces
50, TimeBomb,      8,    0.2,      1.5,          2.0,             0,           0,           8,         360, OnDeath,              10, None,                  0
```

- 這顆子彈飛 1.5 秒後消失，消失瞬間爆開成 8 顆 BasicShot（配方 ID=10）
- 8 顆子彈在 360 度全方向展開
- SubRecipeID=10 讓爆開的子彈使用 BasicShot 的屬性

### 連鎖反彈穿透彈
碰到怪物會反彈，最多彈 3 次，且可穿透 1 個目標。

```
ID, Name,      Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces
60, ChainShot,    30,    0.1,        5,          0.4,             0,           1,           1,           0,            ,            , Enemy,                 3
```

### 環繞護盾
3 顆子彈以半徑 2 繞玩家旋轉，碰到怪物即消失。

```
ID, Name,        Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces, HomingTurnSpeed, IsOrbital, OrbitalRadius, OrbitalCount
90, OrbitalShield,    5,    0.1,        -1,          2.0,             0,           0,           1,           0,            ,            , None,                  0,               0,         1,             2,            3
```

- Speed=5 控制繞圈速度，LifeTime=-1 讓子彈不因時間消失
- IsOrbital=1，OrbitalRadius=2，OrbitalCount=3：3 顆等距環繞
- 碰到怪物造成傷害後消失，按一次攻擊重新召喚 3 顆

### 環繞穿透護盾
同上，但碰到怪物不消失，持續環繞造成多次傷害。

```
ID, Name,             Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces, HomingTurnSpeed, IsOrbital, OrbitalRadius, OrbitalCount
91, OrbitalPierceShield, 5,    0.1,        -1,          2.0,             0,           -1,           1,           0,            ,            , None,                  0,               0,         1,             2,            3
```

- PierceCount=-1 無限穿透；LifeTime=-1 不因時間消失，持續環繞

### 環繞反彈彈
環繞時碰到怪物會脫軌反彈飛出，以反彈角度飛走。

```
ID, Name,          Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces, HomingTurnSpeed, IsOrbital, OrbitalRadius, OrbitalCount
92, OrbitalBounce,     8,    0.1,        -1,          2.0,             0,           0,           1,           0,            ,            , Enemy,                 3,               0,         1,             2,            3
```

- BounceTarget=Enemy + MaxBounces=3：碰到怪物後脫軌反彈飛出，最多彈 3 次

---

## 常見問題

### Q: SpreadCount > 1 但什麼都射不出來？
**A:** 確認 `SplitTiming` 欄位有填寫。SpreadCount > 1 時必須指定分裂時機（通常填 `OnSpawn`）。

### Q: 分裂子彈跟父彈一樣會再分裂，變成無限分裂？
**A:** 不會。如果 `SubRecipeID` 留空，分裂出的子彈會繼承父彈屬性但不帶分裂行為。如果 `SubRecipeID` 指向另一個有分裂的配方，才會再次分裂（可以用來做多層分裂效果）。

### Q: BounceTarget 設為 Environment，子彈碰到怪物會怎樣？
**A:** 子彈碰到怪物時會正常觸發命中事件（造成傷害），但不會反彈。如果沒有穿透（PierceCount=0），子彈會在命中後銷毀。若 PierceCount=-1，則可無限穿透怪物。

### Q: 想做散彈 + 反彈的組合效果？
**A:** 在散彈配方的 `SubRecipeID` 填入一個有反彈的配方 ID。例如：

```
ID, Name,          Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces
70, BouncePellet,     20,    0.1,        5,          0.3,             0,           0,           1,           0,            ,            , Environment,           3
80, BounceShot,       20,    0.1,        5,          0.8,             0,           0,           3,          60, OnSpawn,              70, None,                  0
```

- 武器使用配方 ID=80（BounceShot）
- 發射時分裂成 3 顆，每顆使用配方 ID=70（BouncePellet）
- 每顆散彈都能碰牆反彈 3 次

### Q: 環繞彈的 Speed 代表什麼？
**A:** Speed 在環繞型彈道中代表切線速度（繞圈的移動速度）。角速度 = Speed / OrbitalRadius。例如 Speed=5、OrbitalRadius=2，角速度 = 2.5 rad/s，大約每 2.5 秒轉一圈。

### Q: 環繞彈碰到怪物後，設定反彈會怎樣？
**A:** 子彈會脫離軌道，以反彈角度飛出去，之後就像普通子彈一樣直線飛行。如果同時有追蹤（HomingTurnSpeed > 0），脫軌後會自動追蹤下一個目標。

### Q: 環繞彈的 LifeTime 怎麼設定？
**A:** 設為正數則每幀倒數，歸零時銷毀。設為 **-1** 表示不因時間銷毀（可一直環繞直到被其他機制銷毀）。也可用很大的正數（如 9999）近似長時間存在。

---

## 雷射光束型武器（IsLaser）詳解

雷射是**持續掃射型**武器：按住攻擊鍵時維持一條（或多條）光束，砲口跟著玩家、瞄準跟著滑鼠。它和「會飛的子彈」本質不同（一條當下就存在的線），底層由獨立的 `LaserBeam` 元件用 **line-march（逐段行進）** 每幀重算路徑，但**讀的是 RecipeTable 上同名的既有欄位**，所以對填表者透明。

### 啟用方式
- `IsLaser = 1`（與 `IsOrbital` / `IsParabolic` 互斥，三者只能擇一）
- 傷害走武器表 `Damage`，每 `dotInterval` 秒結算一次（會被怪物無敵時間擋掉，屬正常）
- `BeamRange` 控制最大長度；**光束粗細（視覺 + 命中判定）統一由 WeaponTable 的 `BeamWidth` 控制**（所見即所得），配方 `Radius` 對雷射不生效
- 外觀（貼圖 / 顏色 / 寬度 / 流動速度）在 **WeaponTable** 設定，不在這裡

### 吃得下的既有欄位（行為複用）

| 欄位 | 對雷射的意義 |
|------|------|
| `PierceCount` | 光束穿過幾個敵人才被擋住。`-1` = 穿到底 / 到牆；`0` = 打到第一個就停 |
| `Radius` | 雷射**不使用**此欄（粗細改由 WeaponTable 的 `BeamWidth` 一欄控制，視覺=命中） |
| `HomingTurnSpeed` | **追蹤**：光束起始朝滑鼠，中段自然彎曲咬住最近的敵人（賣點）。數值意義與追蹤彈一致 |
| `BounceTarget` + `MaxBounces` | **反彈**：光束打到可反彈表面會折射，變成多段折線 |
| `BlockedByEnvironment` | 牆壁擋不擋光束（同既有語意） |
| `SpreadCount` + `SpreadAngle` | **一發多道**：扇形射出 N 道光束（每道獨立追蹤 / 反彈 / 穿透） |
| `SplitTiming=OnHit` + `SubRecipeID` | **命中分裂**：光束掃到敵人時在命中點生成 SubRecipeID 子彈，**節流綁在 dotInterval tick**（避免每幀爆量） |

### 不生效 / 互斥的欄位
- `Speed`、`LifeTime`、`FireInterval`：對持續光束無意義（按住就在），填了會被忽略
- `RotationSpeed`、`SplitTiming=OnSpawn/OnDeath`：對雷射無意義，填了不報錯但不生效
- `IsOrbital` / `IsParabolic`：與 `IsLaser` 互斥

### 範例

```
ID, Name,         ..., PierceCount, ..., BounceTarget, MaxBounces, HomingTurnSpeed, ..., IsLaser, dotInterval, BeamRange
14, 雷射追蹤光束, ...,          -1, ..., None,                  0,             180, ...,       1,         0.3,        18
15, 雷射反彈光束, ...,          -1, ..., Environment,           3,               0, ...,       1,         0.3,        15
```

- **ID 14**：無限穿透 + 追蹤（180°/s 彎曲）的死光，掃過一排敵人並咬向最近目標，每 0.3 秒一跳傷害
- **ID 15**：無限穿透 + 牆壁反彈 3 次的光束，可繞角打到掩體後的敵人

### 常見問題

**Q: 追蹤光束會彎，那它怎麼判定打到誰？**
A: 光束沿彎曲路徑逐段做 `CircleCast`，路徑上每個敵人都會被記錄，由主遊戲在 tick 時結算傷害。

**Q: 我想做「掃到敵人就放火」的雷射？**
A: 填 `GroundEffectID` + `GroundEffectTrigger=OnHit` + `GroundEffectHitTarget=Enemy`，光束命中點每跳會釋放地面特效（同樣綁 dotInterval 節流）。

**Q: 想換不同風格的雷射（藍光 / 紅光 / 像素風）？**
A: 行為配方不動，只改 **WeaponTable** 的 `BeamTexturePath` / `BeamColor` / `BeamWidth` / `ScrollSpeed`。同一份程式 + 同一份配方可換無限種外觀。
