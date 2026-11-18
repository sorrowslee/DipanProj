# 雷射系統 (Laser Beam ＋ 光束外觀 BeamFxTable)

> ⚠ **2026-08-26 RecipeTable 大改**：`IsXxx` 旗標已收成一欄 `Mode`、`BeamRange→Range`、`BlastRadius→AreaRadius`、拋物線飛行秒數獨立成 `FlightTime`、連鎖跳數獨立成 `ChainCount`、錐角／吸附半徑獨立成 `AimConeAngle`／`SnapRadius`。本文提到的欄名已同步更新；完整欄位與各模式吃哪些欄以 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) 為準。

> 返回 [文件總覽](README.md)

> **2026-11-18 外觀重做**：舊的 `BeamStyle`／`BeamColor`（`BeamStyleLibrary` + `Custom/AdditiveBeam`）、像素雷射 `PixelBeamSet`（`PixelLaserBeamVisual`）、`laser_glow`／`laser_impact` 光暈與 `SpawnChainVisual` **全部拆除**。雷射、連鎖閃電、掃射的外觀統一改由 DipanVfx 的 **`BeamVisual` ＋ `BeamFxTable`** 畫（武器表填 `BeamFxID`），規格見 [DIPAN_VFX.md](DIPAN_VFX.md) §4d。本文只講行為；舊外型系統的說明已刪，鏡界折光的像素雷射文件封存在 [archive/PIXEL_REFLECT_LASER.md](archive/PIXEL_REFLECT_LASER.md)。

> **掃射（`Mode=Sweep`，2026-11-11）** 的光束也用同一套外觀（`BeamFxID`／`BeamWidth`；每幀重餵兩點：源點→地面落點），但光束不傷人、傷害在沿線爆炸，見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)〈Sweep 掃射〉。

持續掃射型雷射武器。與「會飛的子彈」本質不同——它是「一條當下就存在的線」，不適用 `BulletInstance`（會移動的點）與 `IBulletBehavior`（為移動設計），因此**新增獨立的 `LaserBeam` 核心元件**，與 `BulletInstance` 平級。設計上仍嚴守解耦邊界：**彈道系統只管幾何 + 回報命中，絕不結算傷害；也不畫任何東西**（外觀在主遊戲掛 DipanVfx 的 `BeamVisual`）。

---

## 一、LaserBeam（彈道系統，`BallisticsSystem/Runtime/Core/LaserBeam.cs`）
* **line-march（逐段行進）**：每幀從砲口沿瞄準方向逐段行進，把**追蹤（彎曲）、反彈（折線）、穿透、射程**收斂進同一個迴圈產出折線頂點。
  * **牆與敵人分開偵測（關鍵）**：牆（阻擋/反彈層）用**細射線 `Physics2D.Raycast`**（精準、掠射角與薄牆都抓得到、命中點正好在牆面）；敵人（可穿透層）用**粗圓 `CircleCastAll`**（吃滿光束寬度）。早期「厚圓掃描在掠射角漏抓薄牆 → 光束穿牆飛出場外」就是只用一種 cast 造成，分開後根治。
  * 追蹤：每步朝最近敵人轉向（`HomingTurnSpeed` 以名目光速換算曲率，手感與追蹤彈一致），起始朝滑鼠、中段咬敵；已記錄過的目標不再吸引轉向，光束自然彎向下一個敵人。
  * 反彈：命中可反彈表面 `Vector2.Reflect`，光束變多段折線。
  * 穿透：配方 `PierceLv` 控制穿過幾個敵人（lv n＝n 隻，最多 50）；`0` 打到第一個就停。2026-11-17 起取消 -1 無限（2026-11-17 起 CSV 填等級，見 [EQUIPMENT.md](EQUIPMENT.md)）。
  * 直射用單次大段 cast（快路徑），追蹤才用細步行進（精度路徑）。
* **貼身敵人補抓（重要陷阱）**：本專案 `Physics2D.queriesStartInColliders = false`，從砲口出發的 cast 會**忽略重疊在光束起點的碰撞體** → 怪物貼在玩家身上時雷射打不到（飛劍類因為是「從外面飛進來」不受影響）。`MarchBeam` 開頭多做一次 `OverlapCircle`（半徑＝光束半寬）把貼身怪補進命中清單（純記錄傷害、不擋光束、**不動全域物理設定**以免影響子彈系統）。
* **所見即所得的命中寬度**：命中判定半徑 `Radius = BeamWidth / 2`（不再讀配方的 `Radius`）；外觀寬＝`BeamWidth × BeamFxTable.WidthMul`，兩者從同一欄推出。
* **不畫東西**：2026-11-18 起 `LaserBeam` 只算 `Points`（唯讀折線）與命中。外觀由主遊戲依武器 `BeamFxID` 建一條 `BeamVisual`，`Track(beam.Points)`——它在 **LateUpdate** 讀路徑，所以一定拿到這一幀 `MarchBeam` 算好的折線，砲口不會落後一幀。（舊版自繪 mesh、每段獨立四邊形＋轉角延伸、`Custom/AdditiveBeam` 參數化 shader、`laser_glow` 光暈都已刪除。）
* **傷害回報**：自帶 `DotInterval` 計時器，每隔 N 秒透過 `Action<LaserBeam, List<BeamHit>> OnBeamDamageTick` 把「當下掃到的目標」回報給主遊戲，傷害完全由主遊戲結算。
* `BallisticsEngine.SpawnBeam(def, origin, aimDir, masks…, beamWidth, onTick)`：純程式生成光束物件的工廠（只有 `LaserBeam` 一個元件），**不需要 prefab、不需要 Inspector 接線**。

### 主遊戲端（`PlayerController`）
* **持續光束生命週期**：偵測到雷射武器時走 `UpdateLaser`——按住維持一組（依分裂數＝配方 `SplitLv` 的扇形）光束、每幀更新砲口位置與瞄準方向；放開 / 切武器時 `ClearActiveBeams` 銷毀整組（仿環繞彈的群組管理），每道的 `BeamVisual` 改呼叫 `Stop()`（照表的 `FadeTime` 淡出後自毀）。
* **`HandleBeamTick`**：接收光束每 `DotInterval` 的命中回報，對怪物用 `WeaponData.Damage` 結算傷害（無敵時間由 `MonsterController.TakeDamage` 內部的 `HitReactionHandler` 自動處理），並觸發地面特效。
* **OnHit 分裂**：`SplitTiming=OnHit` + `SubRecipeID` 時，在命中點生成子彈，**節流綁在 DotInterval tick**（避免每幀爆量）。
* **外觀資料驅動**：行為在 `RecipeTable`、外觀在 `WeaponTable`——只填 `BeamFxID`（BeamFxTable 哪一列：1 鏡光、2 雷射、3 電光、4 氣功砲、5 紅色雷射；留空＝2）與 `BeamWidth`（命中寬度）。顏色、電弧、光球、脈衝等細節都在 BeamFxTable，在地圖編輯器「特效預覽器 → 自製特效 → 光束」即時調。

### 互斥與不生效欄位
* `Mode` 一列只能一種，雷射與環繞／拋物線天生互斥。
* `Speed` / `LifeTime` / `FireInterval`（持續光束無發射節奏）、`RotationSpeed`、`SplitTiming=OnSpawn/OnDeath` 對雷射不生效（填了不報錯）。

### 火焰噴射器（火焰外觀模式：雷射的 TrailEffectID > 0）
火焰噴射器 = **一把雷射，但外觀換成火焰 sprite**。因為「按住噴、跟滑鼠掃、持續傷害」本質就是雷射（`Origin`/`AimDirection` 每幀更新 → 完美掃射；`OnBeamDamageTick` → 持續 DOT），只差視覺。

* **啟用**：雷射武器（`Mode=Laser`）的 `WeaponTable.TrailEffectID > 0` → 進入火焰模式。
  * 主遊戲**不建 `BeamVisual`**（不畫光束），`LaserBeam` 照常算路徑(`Points`)、命中、DOT、射程、撞牆停。
  * 主遊戲（`PlayerController.UpdateFlameColumn`）沿 `beam.Points` 每隔配方 `TrailStep` 鋪一根 **TrailEffectID 指向的火焰 Vfx**，每幀重新定位 → 火焰柱跟著光束掃。
* **火焰 Vfx 必須是「持續循環」**：VfxTable 該特效設 `Loop=1` + **`Duration=-1`（無限循環，由 PlayerController 持有、放開/切武器時清除）**。不能用一次性（會自毀、無法被重新定位）。
* **復用既有欄位**：`TrailEffectID`（火焰外觀，同地刺）/ `TrailStep`（火焰間距）/ `BeamWidth`（命中寬度）/ `DotInterval`（DOT 節拍）/ `Range`（火焰射程，建議短，例如 5）/ `PierceLv`（穿幾個敵人）。傷害走武器表 `Damage`，每 `DotInterval` 結算。
* **掃射 / 反彈 / 追蹤全部免費**：因為它就是雷射——`BounceTarget` 讓火焰撞牆折射、`HomingLv` 讓火焰彎曲咬敵。
* 範例：武器「火焰噴射器」`Mode=Laser, TrailEffectID=4(火球), BeamWidth=1.2, Damage=2`；配方 `DotInterval=0.2, Range=5, TrailStep=0.5, PierceLv=50`；Vfx 4「火球」`FireBall, Loop=1, Duration=-1, Scale=0.5`。
* 邊界：火焰 Vfx 的生成/管理全在主遊戲（VfxManager），彈道只開放 `LaserBeam.Points`（唯讀路徑），不碰 Vfx。

---

## 二、外觀（BeamFxTable，2026-11-18）

外觀全部搬到 DipanVfx：`BeamVisual` 元件＋`Custom/DipanBeam`（光柱）／`Custom/DipanArc`（電弧）／`Custom/DipanBeamOrb`（光球）三支 shader，一列 BeamFxTable＝一種光束。**規格、欄位、預覽器用法見 [DIPAN_VFX.md](DIPAN_VFX.md) §4d**。目前五列：

| ID | 名稱 | 用在 | 特色 |
|---|---|---|---|
| 1 | 鏡光 | 鏡界折光 29 | 金白、邊緣七彩色散、沿光束滑過的亮紋、反彈點十字星芒 |
| 2 | 雷射 | 預設（武器沒填 BeamFxID） | 青藍、流紋往前推＋能量脈衝 |
| 3 | 電光 | 連鎖閃電 9、雷雨天罰 66 | 淡光暈＋3 條沿路徑鋸齒電弧（帶分岔），每秒重抽 24 次 |
| 4 | 氣功砲 | 雷射追蹤光束 5 | 粗白核藍光柱、脈衝鼓包往前推、發射時往前伸長、5 段電弧繞柱往前竄、大光球頭 |
| 5 | 紅色雷射 | 路西法之怒 78 | 紅、細、快脈衝；不畫砲口（掃射源點有自己的光球） |

加新外觀＝在 BeamFxTable 加一列、武器 `BeamFxID` 指過去，不用改程式。

---

## 三、連鎖閃電（Mode=Chain，複用光束渲染當電弧）

連鎖閃電與雷射「外觀同源、機制不同」：雷射是「按住維持、往前 march＋撞牆反彈」的持續光束；連鎖閃電是「**點一下放一發**（吃 `FireInterval`）、命中首怪後在敵人之間逐跳」的離散攻擊。兩者都長成「一條折線」，所以**外觀都走 `BeamVisual`**（同一張 BeamFxTable），只是折線的點來源不同。

### 機制（全在主遊戲側 `PlayerController.ShootChain`）
* **第一段**：朝**滑鼠方向** `CircleCast`（容差半徑＝`BeamWidth/2`）找沿線第一個目標；先用 `Raycast(EnvLayer)` 抓環境，**撞到純牆就停**（閃電不穿牆）。射程＝配方 `Range` 欄。
* **逐跳**：從上一個目標用 `OverlapCircleAll(ChainRadius)` 找「還沒打過的最近**可造成傷害目標**」，連過去；重複到 `ChainCount` 次用完或附近沒目標。**總命中數 = 1 + ChainCount**。
* **目標 = 怪 + 可破壞地上物**：搜尋遮罩用 `EnemyLayer | EnvLayer`，再用 **`IDamageable` 過濾**——怪物與可破壞地上物（都實作 `IDamageable`）都算合法目標，**純牆（無 `IDamageable`）被排除**，不會被當成跳躍目標、也不浪費跳躍。所以連鎖閃電能打壞家具（符合「任何能造成傷害的武器都能破壞地上物」）。撞到擋路的可破壞家具時也會把它當首節點打。
* **傷害**：每個連到的目標都吃**武器表 `Damage`**（每跳一樣；之後要做「每跳遞減」就在這裡乘衰減係數）。透過 `ApplyDamage` → `IDamageable`，自動吃怪物無敵時間。
* **目標搜尋與傷害都不碰彈道系統**（守住邊界）。

### 視覺
* `BeamVisual.Flash(weapon.BeamFxID, 折線, BeamWidth×PlayerScale, 0.16 秒)`：折線＝玩家→怪A→怪B…（打空時＝玩家→牆／射程末端），閃 0.16 秒邊閃邊淡出後自毀。
* **鋸齒交給外觀**：2026-11-18 起不再先把折線打成鋸齒（`BuildJaggedPath` 已刪）。電光那列的電弧自己在節點之間亂竄、每秒重抽，節點（目標）上釘死。

### 吃得下的配方欄位（行為複用）
連鎖閃電除了自己的 `ChainRadius`，也複用既有欄位：

| 欄位 | 對連鎖閃電的意義 |
|---|---|
| `ChainCount` | **跳躍次數**（總命中數 = 1 + ChainCount） |
| `Range` | **第一段射程**（朝滑鼠找首目標的最遠距離） |
| `SplitLv` / `SpreadSpacing` / `SpreadMaxAngle` | **散射**：一發射出 1＋lv 道扇形分布的連鎖（每道獨立連鎖），與雷射的一發多道同理。例如 `SplitLv 2`＋間距 30 = 3 道，分布在滑鼠方向 ±30° |
| `AimConeAngle` | **首目標自動鎖定**（aim-assist；2026-08-26 前借用 `HomingTurnSpeed`，追蹤珠對連鎖已改為無效）。閃電瞬發、不會飛行轉彎，所以：首段不必精準瞄到，改鎖定「以瞄準方向為軸、**半角 = AimConeAngle（上限 180）** 的扇形錐內、射程內、最近」的目標。`180` = 錐張滿一圈 = 鎖最近任意方向的目標；`0` / 留空 = 不鎖定（要精準瞄準線上才打得到首目標）。逐跳本來就會找最近，所以追蹤只作用在「首目標」 |
| `BeamFxID`/`BeamWidth`（武器表） | 外觀（與雷射同欄）；`BeamWidth` 也是第一段瞄準的容差寬度 |
| `Damage`（武器表） | 每個連到的目標的傷害（每跳一樣） |

> ⚠️ **散射 + 追蹤的交互**：追蹤是「每道在自己的扇形方向附近鎖最近目標」。若 `AimConeAngle` 很大（如 180），每道的錐都張滿一圈 → N 道會鎖到同一隻最近的怪、扇形就收攏在一起。想保留散開的扇形，把 `AimConeAngle` 調小（例如 30~45，只在各自方向附近鎖定）。

* **範例**（目前設定）：武器 11「連鎖閃電」`Damage=3, RecipeID=22, BeamWidth=0.25`（2026-11-18 起外觀＝`BeamFxID=3` 電光；此範例為舊編號，現行連鎖閃電是武器 9）；配方 22「連鎖閃電」`Mode=Chain, ChainCount=4（跳4次）, Range=15, ChainRadius=4, FireInterval=0.5, SplitLv=2, SpreadSpacing=30, SpreadMaxAngle=60, AimConeAngle=180`。
* **調整方向**：跳更多 → `ChainCount`；跳更遠 → `ChainRadius`；首段射更遠 → `Range`；幾道 → `SplitLv`/`SpreadSpacing`；首目標鎖定錐角 → `AimConeAngle`；傷害 → 武器 `Damage`；外觀 → 武器 `BeamFxID`（或改 BeamFxTable 那列）。

> 未做（之後可加）：跳躍/鎖定的視線遮擋（目前純比距離，不檢查兩點之間有沒有牆）、每跳傷害遞減、命中音效。

---

## 四、落雷模式（Mode=SkyStrike；目前武器＝九霄雷獄）

從**畫面上緣外往下劈**到滑鼠所在點，落地造成**圓形範圍傷害**。底層 `Mode=SkyStrike` 目前由九霄雷獄使用；AOE 沿用 `AreaRadius`，視覺以 `SegmentedColumn` 啟用分段全高雷柱。

### 機制（全在主遊戲側 `PlayerController.ShootSkyStrike` / `StrikeAt`）
* **落點**：滑鼠所在世界座標。分裂數 > 1（`SplitLv` > 0）時以「玩家→滑鼠」為基準軸、在扇形（總角度由 `SpreadSpacing`／`SpreadMaxAngle` 算出）上、與滑鼠等距分佈出 N 個落點（同拋物線的扇形落點）。
* **視覺**：九霄雷獄由 `SegmentedLightningColumn` 生成——start 作鏡頭外雷首，依鏡頭頂端到落點距離動態鋪 N 節 tileable loop，保持等寬一路打到地面；不使用會收細的 end。地面另播 WeaponTable `HitEffectID` 的大型爆炸。
  * **2026-08-18 起這支是「通用雷柱」**：`Spawn(impact, cam, Style, scale, fps, duration)` 的 `Style` 可指定素材路徑／張數／排序層，`SegmentedLightningColumn.SkyStrike` 是九霄雷獄的預設外觀。**舊的無 `Style` 簽章保留、武器行為零改變。** 血統變身的天雷就是換一組 `Style` 重用同一套拼接邏輯（見 [BLOODLINE.md](BLOODLINE.md) §5）。
  * Sprite 快取改成**以路徑前綴為鍵的字典**，讓多種外觀各自快取、互不干擾；`ResetForPlayMode()` 已註冊 `PlayModeStaticReset`（陣列型 static 快取的坑，見 [PROBLEMS.md](PROBLEMS.md) I8——字典包陣列同樣適用）。
  * ⚠ **「不使用 end」是刻意的**：end 那兩張是「快消散的細電光」，接在擊中點會突然收細。血統變身用的那組素材實測 `end` 就是 `start` 倒過來的同兩張圖，同樣不用。
  * **`Style` 的雷首（start）可以留空 ＝ 整根都用 loop**（`Style.HasCap`）。⚠ **雷首與 loop 的粗細差太多就別接**——血統變身那組實測雷首邊緣只有 1~2px、loop 是 5~17px，接起來是髮絲頂粗柱；而且雷首張數少、`capFrame` 會讓它提早播完定格，底下 loop 還在循環，變成靜止的頭配閃爍的身體。loop 本身上下貫穿可平鋪，純 loop 零接縫，柱頂又在畫面外，不會斷頭。**九霄雷獄維持接雷首**（節奏短、雷首多半落在畫面外）。
* **圓形 AOE**：落點以 `AreaRadius`（留空＝預設 1.2）半徑 `OverlapCircleAll(Enemy|Env)`，對範圍內 `IDamageable`（**怪與可破壞家具都吃**）以**武器 `Damage`** 結算一次。
* **可選殘留**：`GroundEffectID > 0` 時在落點生成一團地面特效（焦痕/殘電/燃燒…）。
* 目標搜尋與傷害都不碰彈道系統（守住邊界）。

### 吃得下的配方欄位
| 欄位 | 對落雷模式的意義 |
|---|---|
| `AreaRadius` | 落地圓形 AOE 半徑（留空＝1.2） |
| `SplitLv` / `SpreadSpacing` / `SpreadMaxAngle` | **散射**：一次劈 1＋lv 道、落點分佈在滑鼠方向的扇形上 |
| `SnapRadius` | **落點吸附**：>0 時把每個落點吸附到「該落點 `SnapRadius` 半徑（世界單位）內最近的可傷害目標」。留空/0＝精準劈在滑鼠點。（2026-08-26 前借用 `HomingTurnSpeed` 當搜尋半徑，追蹤珠鑲上去會變成整張圖吸附，已改獨立欄） |
| `GroundEffectID` | 落點殘留地面特效（可選） |
| `Damage`（武器表） | AOE 傷害 |
| `SegmentedColumn` | 1＝使用 start＋動態 loop 的全高分段雷柱 |
| `HitEffectID`（武器表） | 分段模式下專門代表地面命中特效，不再兼任雷柱本體 |

* **目前設定**：武器 24「九霄雷獄」`Damage=8, RecipeID=37, HitEffectID=26`；配方 37 `Mode=SkyStrike, SegmentedColumn=1, AreaRadius=1.6, FireInterval=0.9`。
* **想試分裂**：把配方 37 的 `SplitLv` 改 2、`SpreadSpacing` 改 30。**想試落點吸附**：把 `SnapRadius` 設成搜尋半徑（例如 3）。
* **調整方向**：AOE 大小 → `AreaRadius`；幾道 → `SplitLv`/`SpreadSpacing`；吸附範圍 → `SnapRadius`；傷害 → 武器 `Damage`；落點殘留 → `GroundEffectID`。

### 接 SubRecipeID = 落點接連鎖閃電（已實作）
落雷配方填 `SubRecipeID` 指向一個 `Mode=Chain` 配方時，可在落地後從落點接一條連鎖閃電；目前九霄雷獄不使用此能力。

* **連鎖的外觀＋傷害＝「定義該連鎖配方的那把武器」**（`WeaponManager.GetWeaponByRecipeID(sub.ID)` 查出，例如連鎖閃電武器 11）。所以雷擊（黃）與接出來的連鎖（白/該武器的顏色）**可各有顏色、粗細、傷害**——雷擊 AOE 走武器 12 `Damage`、連鎖每跳走武器 11 `Damage`。查不到對應武器才退回發射武器（雷擊）本身。
  * 跳躍次數/半徑用**子配方**的（`ChainCount`/`ChainRadius`）。
* 實作：`RecipeEntry.SubRecipe` 保留子配方參考（`ResolveSubRecipes` 設定）；`WeaponManager` 建 `RecipeID → 武器` 對照；`StrikeAt` 落點查出連鎖武器後呼叫共用的 `PlayerController.RunChain`（與連鎖閃電武器同一套）。
* 目前九霄雷獄不接 SubRecipe，保持「單次落雷＋地爆」；此接點僅保留作未來配方能力。

> 註：若同一條連鎖配方被多把武器引用，`GetWeaponByRecipeID` 取**最低 ID** 那把的外觀/傷害。要讓「雷擊的連鎖」和「直接拿的連鎖武器」長不一樣，得各自用不同配方（複製一份連鎖配方給雷擊指）。

> 未做（之後可加）：落雷前的「預警圈」(telegraph) 再劈下、連鎖的視線遮擋、命中音效。
