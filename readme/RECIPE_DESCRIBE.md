# 配方表 (RecipeTable.csv) 欄位說明與使用教學

> 返回 [文件總覽](README.md) ｜ 雙表架構與程式（RecipeManager／WeaponManager）見 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)
>
> **2026-08-26 大改**：51 欄的舊表已換成 **45 欄、依模式分群**的新表——10 個 `IsXxx` 旗標收成一欄 **`Mode`**，
> 借用欄位改成獨立欄，所有數值欄**空白＝預設**。舊表填法對照見文末[遷移對照](#從舊表遷移對照)。

---

## 0. 先懂這五件事

> **不想手填 CSV？用 [武器工坊](WEAPON_WORKBENCH.md)**（Unity `Project Tools → 武器工坊`）：選外型、選模式、只顯示有效欄、名稱下拉、範圍提示，Play 中立刻射出去看，滿意再存回 CSV。本文是欄位的意義與規則，工坊畫的每一欄都從同一份 `WeaponModeSpec` 來。

1. **一列一種模式**：`Mode` 欄決定這列是什麼武器（一般子彈／環繞／拋物線／雷射／佛光／連鎖／落雷／召喚／法陣／近戰／突進），**留空＝一般子彈**。一列只能一種，互斥由結構保證，不會再有「兩個旗標都填 1 看程式先判哪個」的事。
2. **每種模式只吃它那幾欄**：其餘欄位就算填了也**不會被讀**（不只是警告——`RecipeEntry.FromFields` 根本不讀無效欄），Console 會在載入時印 `[RecipeTable] 配方 N：「X」對某模式無效，會被忽略`。哪些欄對哪個模式有效的**唯一真相是程式 `Assets/Scripts/Weapon/WeaponModeSpec.cs`**——本文的表是從它抄出來的；兩者不一致時以程式為準並回來修文件。
3. **空白＝預設值**：不再有必填的數值欄。每欄的預設寫在下面總覽表；只有「本體必填」（佛光／法陣的 `GroundEffectID`、召喚的 `SummonIds`）缺了會印 Error。
4. **欄位依名字讀、不依位置**：解析器讀第一列表頭（括號前的名字），所以欄位可以任意重排、中間插新欄；表頭括號裡的說明隨你改。`#` 開頭整列是註解。
5. **能力珠對模式無效就不套用**：鑲珠子時鍛造介面會提示「這顆對目前武器沒效果」（提示不擋，換把武器就有用），套用時 `PlayerAbilities` 也會跳過。矩陣見[第 4 節](#4-能力珠--模式)。

---

## 1. 欄位總覽（依表頭分群）

| 群組 | 欄位 | 型別 | 預設 | 說明 |
|---|---|---|---|---|
| **通用** | `ID` | 整數 | — | 配方唯一識別碼，武器表用 `RecipeID` 引用 |
| | `Name` | 字串 | — | 只給人看，程式不讀 |
| | `Mode` | 列舉 | Normal | `Normal`／`Orbital`／`Parabolic`／`Laser`／`Aura`／`Chain`／`SkyStrike`／`Summon`／`GroundCast`／`Melee`／`Dash`（不分大小寫，也接受中文名） |
| | `FireInterval` | 小數 | 0.3 | 發射間隔（秒）；Laser／Aura 無效（按住就在）；Summon＝召喚冷卻 |
| **子彈本體** | `Speed` | 小數 | 15 | 飛行速度（世界單位/秒）；Orbital＝繞圈的切線速度 |
| | `Radius` | 小數 | 0.1 | 碰撞半徑；Laser 改用武器表 `BeamWidth` |
| | `LifeTime` | 小數 | 3 | 存活秒數；-1＝不因時間消失；Orbital＝整組到期時間 |
| | `RotationSpeed` | 小數 | 0 | 自轉（度/秒），純視覺 |
| | `PierceCount` | 整數 | 0 | 穿透數；0＝碰到就停；-1＝無限穿透 |
| | `BlockedByEnvironment` | 布林 | 1 | 0＝穿過牆（需 `PierceCount ≠ 0`） |
| **多發** | `SpreadCount` | 整數 | 1 | 一發幾顆／幾道；1＝不分 |
| | `SpreadAngle` | 小數 | 0 | 扇形總角度（度） |
| | `SplitTiming` | 列舉 | OnSpawn | `OnSpawn` 發射時分／`OnHit` 打中才分／`OnDeath` 飛到底才分。**留空＝OnSpawn**（只填 `SpreadCount` 就會分裂）。Laser 只認 `OnHit` |
| | `SubRecipeID` | 整數 | — | 分裂子彈用的配方；留空＝繼承自身（不再分裂）；SkyStrike＝落點接連鎖的 Chain 配方 |
| **反彈** | `BounceTarget` | 列舉 | None | `None`／`Environment`（牆）／`Enemy`（怪） |
| | `MaxBounces` | 整數 | 0 | 最大反彈次數（需 `BounceTarget ≠ None`） |
| **追蹤** | `HomingTurnSpeed` | 小數 | 0 | 追蹤轉向（度/秒）；0＝不追蹤；90 慢／180 中／360 快；Laser＝光束彎曲速度 |
| **射程／範圍** | `Range` | 小數 | 20（法陣 8） | Laser 光束長度（-1＝無限）／Chain 首段射程／GroundCast 施放距離 |
| | `AreaRadius` | 小數 | 0 | Parabolic 落地爆炸／SkyStrike 落雷 AOE／Melee 扇形半徑 |
| **命中附加** | `GroundEffectID` | 整數 | 0 | 命中／落地時放一個 `GroundEffectTable` 的特效；**Aura／GroundCast 是本體、必填** |
| | `GroundEffectHitTarget` | 列舉 | Enemy | `Enemy`／`Environment`／`Any`／`Ground`（Parabolic 落地專用） |
| | `TrailStep` | 小數 | 0 | >0 時每飛這麼遠沿路種一個武器表 `TrailEffectID` 的特效（地刺、火焰噴射器） |
| | `SubWeaponOnHit` | 整數 | 0 | 命中時在命中點射出這把**武器表 ID** 的武器 |
| | `SubWeaponHitTarget` | 列舉 | Enemy | `Enemy`／`Environment`／`All` |
| **環繞** | `OrbitalRadius` | 小數 | 2 | 軌道半徑 |
| | `OrbitalCount` | 整數 | 3 | 一輪幾顆 |
| **拋物線** | `FlightTime` | 小數 | 1 | **飛行秒數**——不論遠近都飛這麼久才落地（原本借用 `Speed`） |
| | `ArcHeight` | 小數 | 2 | 弧頂視覺高度 |
| | `LaunchSource` | 列舉 | Player | `Player`／`Offscreen`（從畫面外飛進來） |
| | `LandingScatterRadius` | 小數 | 0 | 落點隨機半徑 |
| **雷射** | `DotInterval` | 小數 | 0.5 | 傷害節拍（秒） |
| **連鎖** | `ChainCount` | 整數 | 0 | 跳躍次數（總命中＝1＋跳數；原本借用 `MaxBounces`） |
| | `ChainRadius` | 小數 | 4 | 每跳搜尋半徑 |
| | `AimConeAngle` | 小數 | 0 | 首段鎖定錐半角（度）；0＝要正好瞄到；180＝鎖最近任意方向（原本借用 `HomingTurnSpeed`） |
| **落雷** | `SnapRadius` | 小數 | 0 | 落點吸附半徑（世界單位）；0＝不吸附（原本借用 `HomingTurnSpeed`） |
| | `SegmentedColumn` | 布林 | 0 | 1＝從畫面頂鋪到落點的分段雷柱（九霄雷獄）（原 `UseSegmentedSkyStrike`） |
| **召喚** | `SummonIds` | 整數清單 | — | 可召喚怪物 ID，用 `\|` 分隔，每次隨機抽一個；**必填** |
| | `SummonCount` | 整數 | 1 | 每次召喚幾隻 |
| | `SummonMaxAlive` | 整數 | 4 | 同時存在上限 |
| | `SummonRadius` | 小數 | 2 | 生成半徑 |
| **近戰** | `MeleeAngle` | 小數 | 100 | 扇形總角度 |
| **突進** | `DashDistance` | 小數 | 4 | 突進距離 |
| | `DashWidth` | 小數 | 1 | 掃擊寬度 |
| **集氣** | `ChargeMode` | 布林 | 0 | 1＝按住 3 秒放開才施放（傷害×3、視覺×2）；Laser／Aura 不可（原「集氣模式」） |
| | `ChargeTimeReduction` | 百分比 | 0% | `30%` 縮短 30%、`-20%` 延長 20%（原「集氣時間縮減」）。詳見 [CHARGE_MODE.md](CHARGE_MODE.md) |

---

## 2. 各模式吃哪些欄（模式 × 欄位矩陣）

✓＝有效　（空）＝不讀　★＝本體必填　括號＝該模式下的意思

| 欄位 | Normal | Orbital | Parabolic | Laser | Aura | Chain | SkyStrike | Summon | GroundCast | Melee | Dash |
|---|---|---|---|---|---|---|---|---|---|---|---|
| FireInterval | ✓ | ✓ | ✓ | | | ✓ | ✓ | ✓(冷卻) | ✓ | ✓ | ✓ |
| Speed | ✓ | ✓(切線) | | | | | | | | | |
| Radius | ✓ | ✓ | | | | | | | | | |
| LifeTime | ✓ | ✓(整組) | | | | | | | | | |
| RotationSpeed | ✓ | ✓ | ✓ | | | | | | | | |
| PierceCount | ✓ | ✓ | | ✓ | | | | | | | |
| BlockedByEnvironment | ✓ | ✓ | | ✓ | | | | | | | |
| SpreadCount／SpreadAngle | ✓ | ✓ | ✓(顆數) | ✓(道數) | | ✓(道數) | ✓(落點數) | | | | |
| SplitTiming | ✓ | ✓ | | ✓(只認 OnHit) | | | | | | | |
| SubRecipeID | ✓ | ✓ | | ✓ | | | ✓(接連鎖) | | | | |
| BounceTarget／MaxBounces | ✓ | ✓ | | ✓ | | | | | | | |
| HomingTurnSpeed | ✓ | ✓ | | ✓(彎曲) | | | | | | | |
| Range | | | | ✓(長度) | | ✓(首段) | | | ✓(施放距離) | | |
| AreaRadius | | | ✓(爆炸) | | | | ✓(AOE) | | | ✓(半徑) | |
| GroundEffectID | ✓ | ✓ | ✓ | ✓ | ★(本體) | | ✓(留痕) | | ★(本體) | | |
| GroundEffectHitTarget | ✓ | ✓ | ✓(Ground) | ✓ | | | | | | | |
| TrailStep | ✓ | ✓ | ✓ | ✓(火焰柱) | | | | | | | |
| SubWeaponOnHit／HitTarget | ✓ | ✓ | | | | | | | | | |
| OrbitalRadius／OrbitalCount | | ✓ | | | | | | | | | |
| FlightTime／ArcHeight／LaunchSource／LandingScatterRadius | | | ✓ | | | | | | | | |
| DotInterval | | | | ✓ | | | | | | | |
| ChainCount／ChainRadius／AimConeAngle | | | | | | ✓ | | | | | |
| SnapRadius／SegmentedColumn | | | | | | | ✓ | | | | |
| SummonIds★／Count／MaxAlive／Radius | | | | | | | | ✓ | | | |
| MeleeAngle | | | | | | | | | | ✓ | |
| DashDistance／DashWidth | | | | | | | | | | | ✓ |
| ChargeMode／ChargeTimeReduction | ✓ | ✓ | ✓ | | | ✓ | ✓ | | ✓ | ✓ | ✓ |

**武器表（WeaponTable）欄位** 也依模式分：`Damage` 除 Summon 外都吃；子彈外觀（`WeaponSpritePath`…`BulletScale`）只有 Normal／Orbital／Parabolic；光束外觀（`BeamStyle`／`BeamColor`／`BeamWidth`）是 Laser／Chain／SkyStrike；`PixelBeamSet` 只有 Laser；`TrailEffectID` 是 Normal／Orbital／Parabolic／Laser；`SummonEffectID` 只有 Summon；`ID`／`Name`／`RecipeID`／`ManaCost`／`FireEffectID` 通用。

---

## 3. 各模式說明與範例

下面每節列「這個模式怎麼運作、哪幾欄最重要、一列範例（只列有填的欄）」。範例用 `欄=值` 寫法，實際 CSV 就是把值填進對應欄。

### Normal 一般子彈（`Mode` 留空）
會飛、會撞的子彈，全套彈道行為都吃：穿透／反彈／追蹤／分裂／軌跡／命中迸發／命中放地面特效。
- 分裂：`SpreadCount=3, SpreadAngle=60` 就是一發三顆（`SplitTiming` 留空＝發射時分）。要「打中才炸開」填 `OnHit`、「飛到底才炸開」填 `OnDeath`。`SubRecipeID` 留空時分裂出來的子彈繼承母彈屬性但不再分裂；指向另一個配方可做「先散射再反彈」。
- 反彈：`BounceTarget=Environment, MaxBounces=3`。
- 追蹤：`HomingTurnSpeed=180`。
- 軌跡（地刺）：`TrailStep=1.5` ＋ 武器表 `TrailEffectID`，子彈本身可以是隱形的（武器表不填圖）。反彈／分裂／追蹤的軌跡都會跟著折。
- 命中迸發：`SubWeaponOnHit=12`（武器表 ID）＋ `SubWeaponHitTarget=All`——打到東西時在命中點射出那把武器（蜂巢→蜜蜂）。與 `SubRecipeID` 的差別見 §3.12。

```
ID=8  Name=3分裂追蹤彈  FireInterval=0.2 Speed=20 Radius=0.1 LifeTime=3 PierceCount=3 SpreadCount=3 SpreadAngle=60 HomingTurnSpeed=180 ChargeMode=1 ChargeTimeReduction=90%
ID=19 Name=地刺        FireInterval=1 Speed=20 Radius=0.3 LifeTime=10 PierceCount=-1 SpreadCount=3 SpreadAngle=50 BounceTarget=Environment MaxBounces=3 TrailStep=1.5
ID=24 Name=蜂巢彈      FireInterval=0.6 Speed=12 Radius=0.2 LifeTime=3 RotationSpeed=50 SubWeaponOnHit=12 SubWeaponHitTarget=All
```

### Orbital 環繞
一組子彈以玩家為圓心繞圈（護盾）。`Speed`＝切線速度（角速度＝Speed÷OrbitalRadius）、`LifeTime`＝整組到期秒數（-1＝直到下次發射）；每次發射先清掉上一輪。其餘行為同一般子彈（穿透就不消失；反彈 Enemy 會脫軌飛出；分裂 OnHit 碰到怪時炸開）。

```
ID=9 Name=火焰環繞彈 Mode=Orbital FireInterval=0.3 Speed=4 Radius=0.1 LifeTime=5 PierceCount=-1 BlockedByEnvironment=0 OrbitalRadius=1.5 OrbitalCount=9
```

### Parabolic 拋物線
丟炸彈：飛行中**不撞任何東西**（碰撞遮罩清 0），抵達目標才「落地」。
- `FlightTime`＝飛幾秒落地（不論遠近，所以一發多顆會同時落地）。`ArcHeight` 弧頂假高度。`LaunchSource=Offscreen` 從畫面外隨機方向飛進來（多顆各自重抽）。
- `SpreadCount`／`SpreadAngle`：以「玩家→滑鼠」為軸，N 顆在扇形上、與滑鼠等距（**不需要 `SplitTiming`**）。`LandingScatterRadius` 再給每顆一個隨機落點誤差。
- 落地：`AreaRadius>0` 以武器 `Damage` 炸一圈（吃怪物無敵時間）；`GroundEffectID` ＋ `GroundEffectHitTarget=Ground` 留一灘火。兩者獨立、可並存。
- `RotationSpeed` 仍有效（翻滾炸彈）。穿透／反彈／追蹤對它無意義。

```
ID=12 Name=玩家丟出火焰拋物線彈 Mode=Parabolic FireInterval=0.5 FlightTime=1 ArcHeight=2.5 LandingScatterRadius=3 AreaRadius=1.5 GroundEffectID=1 GroundEffectHitTarget=Ground
ID=13 Name=螢幕外丟出火焰拋物線彈 Mode=Parabolic FireInterval=0.5 SpreadCount=10 SpreadAngle=60 FlightTime=1 ArcHeight=2.5 LaunchSource=Offscreen LandingScatterRadius=3 AreaRadius=1.5
```

### Laser 雷射（持續型）
按住攻擊鍵維持光束，砲口跟玩家、瞄準跟滑鼠；每 `DotInterval` 秒對掃到的目標各結算一次武器 `Damage`。沒有 `FireInterval`、不吃集氣。
- `Range` 光束長度（`-1`＝無限延伸，見 [PIXEL_REFLECT_LASER.md](PIXEL_REFLECT_LASER.md)）；粗細＝武器表 `BeamWidth`（視覺與命中共用，配方 `Radius` 不用）。
- `PierceCount`：`-1` 穿到底；`0` 打到第一個就停。`BounceTarget`＋`MaxBounces`：折線反射。`HomingTurnSpeed`：光束中段彎曲咬住最近的敵人。
- `SpreadCount`／`SpreadAngle`：一發 N 道（每道獨立追蹤／反彈／穿透）。
- `SplitTiming=OnHit`＋`SubRecipeID`：光束掃到敵人時在命中點生成子彈（節流綁 `DotInterval`）。**只認 `OnHit`**，`OnSpawn`／`OnDeath` 對雷射無意義。
- `TrailStep`＋武器表 `TrailEffectID`：火焰噴射器模式——不畫光束，沿路徑每隔 `TrailStep` 鋪一根循環火焰。
- 外觀（種類／顏色／粗細／像素素材）全在武器表。

```
ID=14 Name=單一雷射追蹤光束 Mode=Laser PierceCount=-1 HomingTurnSpeed=180 Range=18 DotInterval=0.3
ID=17 Name=三分裂雷射反彈光束 Mode=Laser PierceCount=-1 SpreadCount=3 SpreadAngle=60 BounceTarget=Environment MaxBounces=3 Range=40 DotInterval=0.3
ID=20 Name=火焰噴射 Mode=Laser PierceCount=-1 SpreadCount=3 SpreadAngle=60 HomingTurnSpeed=180 Range=5 TrailStep=1 DotInterval=0.2
ID=42 Name=鏡界折光 Mode=Laser PierceCount=-1 BounceTarget=Environment MaxBounces=3 Range=-1 DotInterval=0.2
```

### Aura 佛光（持續型）
不發射任何子彈，按住時在玩家身上維持一圈**跟著走**的圓形 AOE。整個圓（半徑／傷害節拍／外觀）由 **`GroundEffectID`（必填）** 指向的 `GroundEffectTable` 列定義，**該列 `Duration` 必須是 -1**（永久，由 `PlayerController` 管生死）；傷害走武器表 `Damage`。其他欄位一律不讀。詳見 [GROUND_EFFECT.md](GROUND_EFFECT.md) 佛光章節、[FALLEN_BUDDHA_LIGHT.md](FALLEN_BUDDHA_LIGHT.md)。

```
ID=21 Name=佛光 Mode=Aura GroundEffectID=2
```

### Chain 連鎖閃電
點一下朝滑鼠射出，命中第一隻後在 `ChainRadius` 內逐跳到還沒打過的最近怪，跳 `ChainCount` 次（總命中＝1＋跳數）；撞牆就停。每跳一樣的武器 `Damage`（也能打可破壞地上物）。
- `Range` 首段射程。`AimConeAngle>0`：首段不必正好瞄到，鎖定「以瞄準方向為軸、半角這麼大的錐內最近」的目標；`180`＝任意方向最近。
- `SpreadCount`／`SpreadAngle`：一發多道，各自獨立連鎖。
- 外觀＝武器表 `BeamStyle`（7 閃電）／`BeamColor`／`BeamWidth`。

```
ID=22 Name=連鎖閃電 Mode=Chain FireInterval=0.5 SpreadCount=3 SpreadAngle=60 Range=15 ChainCount=4 ChainRadius=4 AimConeAngle=180
```

### SkyStrike 落雷
點一下從畫面上緣劈到滑鼠點，落地以 `AreaRadius`（留空 1.2）做圓形 AOE、武器 `Damage` 結算一次（怪＋可破壞家具）。
- `SpreadCount`／`SpreadAngle`：多個落點（扇形分佈，同拋物線）。`SnapRadius>0`：落點吸附到該半徑內最近的可傷害目標。
- `GroundEffectID`：落點留一團地面特效（焦痕／殘電）。
- `SubRecipeID` 指向一個 **Chain** 配方：落點再接一條連鎖閃電（外觀與傷害用「定義該連鎖配方的那把武器」）。
- `SegmentedColumn=1`：全高分段雷柱（九霄雷獄）；否則畫閃電折線（需光束素材）。

```
ID=37 Name=九霄雷獄 Mode=SkyStrike FireInterval=0.9 AreaRadius=1.6 SegmentedColumn=1
```

### Summon 召喚
施放時在身邊生怪，不發射子彈。`SummonIds`（必填，`|` 分隔）每次隨機抽一個；`SummonCount` 每次幾隻、`SummonMaxAlive` 同時上限（滿了不施放也不扣魔）、`SummonRadius` 生成環半徑；冷卻用 `FireInterval`。武器 `Damage` 不用；武器表 `SummonEffectID` 在每個生怪點播特效。目前 boss（紅嫁衣）與玩家（御靈水晶）共用，見 [BOSS_MODULE.md](BOSS_MODULE.md)。

```
ID=27 Name=御靈水晶-召喚雜魚 Mode=Summon FireInterval=1.5 SummonIds=1 SummonCount=1 SummonMaxAlive=1 SummonRadius=1.5
```

### GroundCast 定點法陣
點一下在滑鼠位置（距離受 `Range`，留空 8）放一個 `GroundEffectID`（必填）的地面特效；半徑／持續／DOT 節拍／動畫走 `GroundEffectTable`，**單次傷害由武器表 `Damage` 覆寫**（不是 GroundEffectTable 的 Damage）。黑洞、冰陣、死字咒都是這個。

```
ID=35 Name=虛空吞口 Mode=GroundCast FireInterval=1.2 Range=9 GroundEffectID=3
```

### Melee 近身扇形
以自己為圓心、朝滑鼠方向掃一個扇形：`AreaRadius` 半徑（留空 2）、`MeleeAngle` 總角度（留空 100），範圍內的 `IDamageable` 各結算一次武器 `Damage`；`HitEffectID` 只在扇形中心播一次。

```
ID=34 Name=血月鬼爪 Mode=Melee FireInterval=0.55 AreaRadius=2.1 MeleeAngle=110
```

### Dash 突進斬
往瞄準方向衝 `DashDistance`（撞牆提前停），掃過的膠囊區域（寬 `DashWidth`）內各目標受傷一次。

```
ID=38 Name=幽影突 Mode=Dash FireInterval=0.7 DashDistance=5 DashWidth=1.2
```

### 3.12 `SubRecipeID` vs `SubWeaponOnHit`（兩個都叫 Sub，別搞混）

| | `SubRecipeID` | `SubWeaponOnHit` |
|---|---|---|
| 指向 | **配方**（只有行為） | **武器**（自帶外型／傷害） |
| 外型 | 仿母武器 | 子武器自己的圖 |
| 觸發 | 配合 `SplitTiming` 由彈道系統分裂 | 子彈命中時由主遊戲在命中點生成（`SubWeaponHitTarget` 過濾） |
| 用途 | 「先散射再反彈」這種純行為組合 | 「打到東西迸出一窩長相不同的新武器」（蜂巢→蜜蜂） |

- 迸發方向沿命中面法線往外（生成點會沿法線推出去，避免生在牆裡被自己的重疊檢查瞬殺，見 PROBLEMS **B5**）。
- 子武器只支援 **Normal** 模式；指向其他模式會印 Warning 並不生成。別接成循環（A 迸 B、B 迸 A）。

---

## 4. 能力珠 × 模式

珠子改的欄位對「目前武器的模式」無效時：**鍛造介面提示不擋**（拖進孔位跳一次 toast、孔位上灰顯、tooltip 說明）、**`PlayerAbilities` 套用時跳過**。「目前武器」＝鐵砧上那把（是武器時）或裝備中的武器（珠子鑲在防具上時）。

| 珠子（改的欄位） | Normal | Orbital | Parabolic | Laser | Aura | Chain | SkyStrike | Summon | GroundCast | Melee | Dash |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 反彈 `MaxBounces` | ✓ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| 穿透 `PierceCount` | ✓ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| 迅捷 `Speed` | ✓ | ✓ | ✓（換算成縮短 `FlightTime`） | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| 銳利 `Damage` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ | ✓ |
| 追蹤 `HomingTurnSpeed` | ✓ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| 分裂 `SpreadCount` | ✓ | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ |
| 巨彈 `BulletScale` | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| 疾發 `FireInterval` | ✓ | ✓ | ✓ | ✗ | ✗ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

- 反彈珠對連鎖閃電**不再**增加跳數（跳數已是獨立欄 `ChainCount`）——這是拍板的設計，不是漏做。
- 追蹤珠對連鎖／落雷無效（原本借用 `HomingTurnSpeed` 當錐角／搜尋半徑，會出現「Lv3 追蹤珠＝整張圖吸附」的錯位，已改成獨立欄 `AimConeAngle`／`SnapRadius`）。
- 要加新珠子：`GemTable.Field` 填 RecipeTable／WeaponTable 的欄名，有效性自動從 `WeaponModeSpec` 推導，**不用另外維護**。填了不存在的欄名載入時會 Warning。

---

## 5. `GroundEffectID` 在各模式的角色（與 GroundEffectTable 的接法）

| 模式 | 角色 | 傷害來源 | 額外要求 |
|---|---|---|---|
| Normal／Orbital／Laser | 命中時附加 | GroundEffectTable 的 `Damage` | `GroundEffectHitTarget` 決定打到什麼才放 |
| Parabolic | 落地附加 | GroundEffectTable 的 `Damage` | `GroundEffectHitTarget=Ground` |
| SkyStrike | 落點留痕 | GroundEffectTable 的 `Damage` | — |
| Aura | **本體（必填）** | **武器表 `Damage`** | 該列 `Duration=-1`、`RenderMode=Glow`/`Single` |
| GroundCast | **本體（必填）** | **武器表 `Damage`** | — |

`GroundEffectTable.csv` 欄位（`ID, Name, Radius, Duration, DamageInterval, Damage, AniPath, AniNumber, AnimFPS, TileSize, RenderMode, SigilPath, LightRadius`）與 tile 鋪圓規則見 [GROUND_EFFECT.md](GROUND_EFFECT.md)。

---

## 6. 常見問題

**Q：填了 `SpreadCount=3` 卻只射出 1 顆？**
2026-08-26 起不會了——`SplitTiming` 留空＝發射時分裂。若還是 1 顆，看 Console 有沒有 `[RecipeTable]` 的 Warning（可能 Mode 填錯，或 `SpreadCount` 對該模式無效）。

**Q：Console 印「X 對某模式無效，會被忽略」是壞了嗎？**
沒壞，是提醒你這格填了沒用的東西（值不會被讀）。清掉那格就不印。

**Q：我加了一欄，程式要改哪裡？**
`WeaponModeSpec.BuildFields` 加欄位規格、`BuildModes` 把它加進會吃它的模式、`RecipeEntry.FromFields` 讀它、用到它的發射分支讀 `RecipeEntry`／`ProjectileData`。表頭放哪一群都可以（依名字讀）。

**Q：想做「散彈＋反彈」？**
散彈配方 `SpreadCount=3, SpreadAngle=60`，`SubRecipeID` 指向一個有反彈的配方。

**Q：環繞彈的 `Speed` 代表什麼？**
切線速度。角速度＝Speed÷OrbitalRadius；Speed=5、半徑 2 → 約每 2.5 秒轉一圈。

**Q：能力珠鑲上去沒反應？**
先看鍛造介面有沒有跳「對目前武器沒效果」——那顆珠對這種模式本來就無效（第 4 節）。換把武器就有用。

---

## 從舊表遷移對照

| 舊（2026-08-26 前） | 新 |
|---|---|
| `IsOrbital=1`／`IsParabolic=1`／`IsLaser=1`／`IsAura=1`／`IsChain=1`／`IsSkyStrike=1`／`IsSummon=1`／`IsGroundCast=1`／`IsMelee=1`／`IsDash=1` | `Mode=Orbital`／`Parabolic`／`Laser`／`Aura`／`Chain`／`SkyStrike`／`Summon`／`GroundCast`／`Melee`／`Dash` |
| 拋物線的 `Speed`（＝飛行秒數） | `FlightTime` |
| 連鎖的 `MaxBounces`（＝跳數） | `ChainCount` |
| 連鎖的 `HomingTurnSpeed`（＝錐角） | `AimConeAngle` |
| 落雷的 `HomingTurnSpeed`（＝吸附半徑） | `SnapRadius` |
| `BeamRange` | `Range` |
| `BlastRadius` | `AreaRadius` |
| `UseSegmentedSkyStrike` | `SegmentedColumn` |
| `集氣模式`／`集氣時間縮減` | `ChargeMode`／`ChargeTimeReduction` |
| `GroundEffectTrigger`（只實作過 OnHit） | 刪除 |
| 非子彈模式也得填的 `Radius=0.1, LifeTime=1, SpreadCount=1` 佔位值 | 全部留空 |
| `SpreadCount>1` 必須填 `SplitTiming` | 留空＝`OnSpawn` |

程式側：`RecipeEntry.IsXxx` → `RecipeEntry.Mode == WeaponMode.Xxx`；`BlastRadius` → `AreaRadius`；`IsChargeMode`／`ChargeTimeReductionPercent` → `ChargeMode`／`ChargeTimeReduction`；`ProjectileData.Speed`（拋物線）→ `ProjectileData.FlightTime`。
