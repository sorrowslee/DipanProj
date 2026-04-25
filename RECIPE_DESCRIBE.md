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
| LifeTime | 小數 | 是 | 子彈存活時間（秒），超過即自動銷毀 |
| FireInterval | 小數 | 是 | 發射間隔（秒），控制射速 |
| RotationSpeed | 小數 | 是 | 子彈飛行時的自轉速度（度/秒），0 為不旋轉 |
| PierceCount | 整數 | 是 | 穿透次數，0 為不穿透 |
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
- 建議範圍：1 ~ 10
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
- 環繞型子彈使用 `Speed` 欄位作為切線速度（繞圈移動速度），半徑越小、Speed 越大，轉得越快

### OrbitalRadius（環繞半徑）
- 子彈繞玩家飛行的軌道半徑
- 僅在 IsOrbital = 1 時有效
- 數值越大，軌道越寬
- 參考值：1 = 緊貼玩家、2 = 一般距離、5 = 遠距離

### OrbitalCount（環繞數量）
- 每次發射生成幾顆環繞子彈
- 子彈會等距排列在軌道上（例如 3 顆 = 每隔 120° 一顆）
- 僅在 IsOrbital = 1 時有效

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
90, OrbitalShield,    5,    0.1,     9999,          2.0,             0,           0,           1,           0,            ,            , None,                  0,               0,         1,             2,            3
```

- Speed=5 控制繞圈速度，LifeTime=9999 讓子彈幾乎不會自然消失
- IsOrbital=1，OrbitalRadius=2，OrbitalCount=3：3 顆等距環繞
- 碰到怪物造成傷害後消失，按一次攻擊重新召喚 3 顆

### 環繞穿透護盾
同上，但碰到怪物不消失，持續環繞造成多次傷害。

```
ID, Name,             Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces, HomingTurnSpeed, IsOrbital, OrbitalRadius, OrbitalCount
91, OrbitalPierceShield, 5,    0.1,     9999,          2.0,             0,           99,           1,           0,            ,            , None,                  0,               0,         1,             2,            3
```

- PierceCount=99 讓子彈可穿透大量目標而不消失，持續環繞

### 環繞反彈彈
環繞時碰到怪物會脫軌反彈飛出，以反彈角度飛走。

```
ID, Name,          Speed, Radius, LifeTime, FireInterval, RotationSpeed, PierceCount, SpreadCount, SpreadAngle, SplitTiming, SubRecipeID, BounceTarget, MaxBounces, HomingTurnSpeed, IsOrbital, OrbitalRadius, OrbitalCount
92, OrbitalBounce,     8,    0.1,     9999,          2.0,             0,           0,           1,           0,            ,            , Enemy,                 3,               0,         1,             2,            3
```

- BounceTarget=Enemy + MaxBounces=3：碰到怪物後脫軌反彈飛出，最多彈 3 次

---

## 常見問題

### Q: SpreadCount > 1 但什麼都射不出來？
**A:** 確認 `SplitTiming` 欄位有填寫。SpreadCount > 1 時必須指定分裂時機（通常填 `OnSpawn`）。

### Q: 分裂子彈跟父彈一樣會再分裂，變成無限分裂？
**A:** 不會。如果 `SubRecipeID` 留空，分裂出的子彈會繼承父彈屬性但不帶分裂行為。如果 `SubRecipeID` 指向另一個有分裂的配方，才會再次分裂（可以用來做多層分裂效果）。

### Q: BounceTarget 設為 Environment，子彈碰到怪物會怎樣？
**A:** 子彈碰到怪物時會正常觸發命中事件（造成傷害），但不會反彈。如果沒有穿透（PierceCount=0），子彈會在命中後銷毀。

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
**A:** LifeTime 照常生效。如果想讓環繞彈持續存在直到碰到敵人，設一個很大的值（如 9999）。如果想做限時環繞，設正常的存活時間即可。
