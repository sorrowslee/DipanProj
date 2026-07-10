# Boss 戰鬥模組 (Boss Combat Modules) + 召喚系統

> 返回 [文件總覽](README.md)｜怪物量產見 [MONSTER_SETUP.md](MONSTER_SETUP.md)｜武器/配方見 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)｜傷害結算見 [COMBAT.md](COMBAT.md)｜劇情分支見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §7
>
> **狀態：✅ 框架 + 紅嫁衣 boss（逃跑＋召喚）程式完成（2026-07-09）；✅ 榕樹妖 boss（地刺／三階段／三大絕）程式完成（2026-07-10），待實機微調＋換臉。** 投射型武器供怪物使用（飛劍/落雷…）為 Phase 2。

「一隻強怪＝一個 Brain 模組」。第一個範例是紅嫁衣女殭屍。未來每隻 boss 都新增一個自己的 Brain 類別，其它系統不動。

---

## 1. 模組化地基（三塊）

| 元件（`Assets/Scripts/AI/`） | 角色 |
|---|---|
| `IMonsterBrain.Think(in MonsterContext)` | 決策機介面。簽名已從舊 `Think(actuator, player)` 升級為傳 **`MonsterContext`**（打包 Self/Actuator/Sensor/Player/DeltaTime）。Brain 要更多能力只往 context 加欄位，不改介面。 |
| `MonsterContext`（struct） | 每幀決策脈絡。`ChaseBrain` 只讀 Actuator/Player；boss 級 Brain 另讀 `Self` 拿 `WeaponUser` 施放技能。 |
| `MonsterWeaponUser`（元件，seam） | **怪物「使用一把武器」的統一入口**。`Configure(owner, weaponId)`；Brain 每幀呼叫 `TryUse()`，冷卻（配方 `FireInterval`）與召喚上限都在這裡結算。依配方型別分派：目前實作 `IsSummon`（召喚）；**投射型武器（飛劍/落雷）＝ Phase 2**。 |

**怪物怎麼拿到武器**：`MonsterData.csv` 的 `Weapon` 欄以前閒置，現在**填 WeaponTable 的武器 ID（數字）**＝這隻怪掛 `MonsterWeaponUser` 用那把武器；填 `Contact`／空／非數字 = 不掛（只近戰接觸傷害）。`MonsterController.Initialize` 依此掛上並 `Configure`。

**怎麼加下一隻 boss**：① 寫 `Behaviors/XxxBrain.cs implements IMonsterBrain`；② `MonsterController.Initialize` 的 `switch (data.BrainType)` 加一個 `case "Xxx"`；③ `MonsterData.csv` 那隻怪的 `BrainType` 填 `Xxx`、`Weapon` 填牠要用的武器 ID。就這樣。

---

## 2. 紅嫁衣女殭屍（`RedBridalGownBrain`）

**行為**：躲玩家＋定時召喚家人幽靈當追兵。
- **逃跑**：玩家進入 `FleeRange`(4.0) 內 → 往「反方向」直線逃（用她自己的 `MonsterData.Speed`）；拉開到 `SafeRange`(6.5) 才停（遲滯避免臨界抖動）。**刻意不做繞牆尋路** → 會被牆角/家具卡住讓玩家追上；速度也刻意低於玩家（玩家 5、她預設 3.5）。
- **召喚**：**只看冷卻、不綁逃跑狀態**（她多半在逃，若綁「安全才召」會幾乎不召）。召喚是一把 WeaponTable 武器（見 §3），冷卻/名單/數量/上限全走配方。
- **手感常數**都在 `RedBridalGownBrain.cs` 上方（`FleeRange`/`SafeRange`/`DetectionRange`/`AwayLookahead`）；逃跑速度走 CSV `Speed`。

**與「殺家人」劇情分支的關係（重要）**：`killedFamily` 旗標綁「**編輯器擺放的家人怪出生點**上的死亡觸發旗標」（見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §7）。**boss 召喚出來的分身刻意不帶 `deathFlag`**（`MonsterWeaponUser.TrySummon` 呼叫 `SpawnMonster(id, pos)` 不傳旗標），所以玩家殺召喚分身**不會**誤觸 killedFamily——分支邏輯天生乾淨。

---

## 3. 召喚做成一把武器（表驅動、玩家未來可共用）

召喚是 `RecipeTable` 的一種新型別 `IsSummon`（與雷射/拋物線/連鎖…同層級）。「發射動作」＝呼叫 `MonsterSpawner.SpawnMonster` 生怪。冷卻沿用 `FireInterval`。

**RecipeTable 新增 5 欄**：

| 欄 | 意義 |
|---|---|
| `IsSummon` | 1 = 召喚型（不發射子彈） |
| `SummonIds` | 可召喚的怪物 ID 池，**用 `|` 分隔**（避開 CSV 逗號），例 `8\|9\|10\|11\|12`；每次隨機抽 |
| `SummonCount` | 每次召喚幾隻（空=1） |
| `SummonMaxAlive` | 同一施放者的分身**同時存在上限**，達上限暫停召喚（空=4） |
| `SummonRadius` | 在施放者周圍多遠的環上生成（空=2） |

**現有資料**：配方 26「召喚-紅嫁衣家人」(`IsSummon=1`, `FireInterval=3` 冷卻, `SummonIds=8|9|10|11|12`, `SummonCount=2`, `SummonMaxAlive=5`, `SummonRadius=2`) ← 武器 14「紅嫁衣召喚」 ← 怪物 13 `Weapon=14`。召喚出的家人幽靈用最基本的 `Chase`（ChaseBrain）追玩家。

**玩家側已接（2026-07-09，測試用）**：召喚核心抽成擁有者無關的共用靜態 `SummonSystem.Cast(owner, originPos, recipe, aliveTracker)`——`MonsterWeaponUser`（boss）與 `PlayerController.Shoot`（玩家）都呼叫它，各持一份 alive 清單管同時上限。玩家 Shoot 在「需要 BulletPrefab」的守衛**之前**先攔 `IsSummon`：耗魔→播發射特效→`SummonSystem.Cast`→`_fireTimer=FireInterval`（按住左鍵依冷卻重複召喚）。
- **測試武器＝13 號「御靈水晶」**（RecipeID→27）。配方 27：`IsSummon=1`, `FireInterval=1.5`, `SummonIds=1`（ZhaYu，Main 的怪、**任何地圖都載得到 idle+walk**，故不必先 Sync 就能測）, `SummonCount=1`, `SummonMaxAlive=3`, `SummonRadius=1.5`。切到御靈水晶、按住左鍵即在身邊召喚。要召家人幽靈把 `SummonIds` 改 `8|9|10|11|12`（需在 RedBridalGown 地圖或先 Sync 讓幽靈 walk 幀到位）。
- **陣營制已完成（2026-07-09）**：召喚帶陣營——**玩家召喚＝`PlayerAlly`（協戰）**、**怪物/boss 召喚＝`Enemy`**。見下 §4。
- **召喚特效（2026-07-10）**：召喚型武器可在**武器表**填 `SummonEffectID`（引用 VfxTable），施放時在**每個生怪點播特效、同一幀就生怪（邊播邊出現）**（`SummonSystem.Cast`，玩家/boss 共用；扣魔/冷卻前先 `HasRoom` 確認有空位）。武器 14（紅嫁衣召喚）、13（御靈水晶）已填 VfxTable 10「招喚怪物」。留空 / 0 = 不播、立即生怪。見 [VFX.md](VFX.md)。

---

## 4. 陣營制（玩家召喚＝友軍、怪物召喚＝敵人）

召喚帶 `MonsterFaction`（`SummonSystem.Cast(..., faction)`）：玩家 `PlayerController.Shoot` 傳 `PlayerAlly`、boss `MonsterWeaponUser` 傳 `Enemy`。`MonsterController.Faction` 決定三件事：

| | Enemy（敵怪/boss/其召喚物） | PlayerAlly（玩家召喚的協戰怪） |
|---|---|---|
| **追誰** | 玩家（`MonsterSensor.GetTargetPlayer`） | **`AllyBrain`：附近有敵怪(AggroRange 7)就去打、否則跟玩家(FollowNear 2.2)**；敵怪由 `FindNearestEnemy` 登記表算 |
| **接觸傷害打誰** | 玩家 ＋ 友軍 | 敵怪 |
| **在哪個 Layer** | Enemy(7) | **Ally(8)** |

**Ally 層(8)** 是這次新增的（`ProjectSettings/TagManager.asset`）。用途：玩家子彈打 Enemy 層 → **天生打不到自己的召喚物**；且召喚物不會用物理去推玩家/敵怪。碰撞用 `FactionLayers`（`Assets/Scripts/AI/FactionLayers.cs`）在進場前以 `Physics2D.IgnoreLayerCollision` 設定（Ally 穿過 Player/Enemy/Ally、只被 Environment 擋）——**不必手改 DynamicsManager 碰撞矩陣**，build 與編輯器都自動生效。接觸傷害與友軍找目標走 **`MonsterController.Active` 全場登記表 ＋ `Physics2D.Distance`**（**不用 OverlapCircle**——專案全域 `queriesStartInColliders=false` 會讓 OverlapCircle 貼身漏抓、且不對稱，見 [PROBLEMS.md](PROBLEMS.md) B7）＋中央 `CombatSystem`，玩家與怪物統一結算。**召喚物過傳送點**：換圖清場保留 PlayerAlly、`MapManager.RepositionPlayerAllies` 放好玩家後把它們移到新落點附近（跟著玩家走）。

> 檔案：`MonsterFaction.cs`（列舉）、`FactionLayers.cs`（層解析＋碰撞設定）、`EnemyContactDamage.cs`（改陣營制）、`MonsterController.cs`（Faction 欄位＋FindNearestEnemy）、`MonsterSpawner.cs`（faction 參數＋設層）。

## 5. 接手待辦（Unity / 編輯器端）

- [ ] **重跑 `Project Tools → Sync Map Assets`**：家人幽靈的 `walk` 幀在 `GameAssets` 有、但 `StreamingAssets` 多數只有 `idle`（尚未同步）；不同步召喚出的幽靈會缺走路動畫。順帶把 CSV 也帶到位。
- [ ] **在編輯器紅嫁衣最終房放 boss**：怪物出生點填**怪物 ID 13**（RedBridalGown）。她的 BrainType/Weapon 已在 CSV 設好。
- [ ] **家人怪出生點**（給 killedFamily 用）：預先擺放的家人怪（8~12）出生點填「死亡觸發旗標」`killedFamily`（關卡單次），與 boss 召喚分身無關。
- [ ] **實機調手感**：逃跑速度（CSV `Speed`，現 3.5）、逃/停距離與召喚冷卻/上限（配方 26）。太難就降 `SummonMaxAlive`/拉長 `FireInterval`；她太好抓就升 `Speed`。
- [x] ~~boss 只會追擊、不逃跑不召喚~~ → **已修**（BrainType 沒 Trim，見 [PROBLEMS.md](PROBLEMS.md) F4）。修後若仍要驗證：確認 MonsterData.csv 已被 Unity 重匯入、boss 出生點填怪物 ID 13。
- [ ] **怪打怪傷害忽勝忽敗**（召喚物 vs 敵怪）＝接觸傷害每幀結算＋怪 InvincibleTimeMs=0＋近乎一擊斃命 → 勝負看 Update 順序。**待調數值**：給互毆的怪 `InvincibleTimeMs`>0（300~500）＋平衡 HP/ContactDamage。詳見 [PROBLEMS.md](PROBLEMS.md) F5。
- [ ] boss 召喚的家人幽靈(8~12)若只有站姿/看不到 → 幽靈 walk 幀還沒進 StreamingAssets，跑 `Project Tools → Sync Map Assets`。
- [ ] （Phase 2）要讓怪物射飛劍/落雷 → 把 PlayerController 發射管線抽成共用服務，`MonsterWeaponUser` 非召喚分支接上。

---

## 6. 榕樹妖（`BanyanTreeBrain` ＋ `BossSpike`）— 打「牠的攻擊物」反傷本體

**概念**：跟紅嫁衣完全相反的一種 boss。榕樹妖本體＝**背景的一棵樹、無法直接攻擊**（本體是**無圖隱形的 `MonsterController`**，只管血量＋編排攻擊、不移動）。牠用**地刺**攻擊玩家；玩家要**閃過地刺**，並**攻擊「冒出來的地刺」**把傷害反饋給本體。臉是地上物（`treeFace_vicious.png` 平時／`treeFace_crazy.png` 攻擊時，換臉為待做）。

**兩個檔案**：
| 檔案（`Assets/Scripts/AI/`） | 角色 |
|---|---|
| `Behaviors/BanyanTreeBrain.cs` | boss 腦：讀血量切階段、編排每一波地刺。手感常數全在檔案上方。 |
| `BossSpike.cs` | **地刺攻擊實體**（一次性、自跑狀態機）。單根／一排／放大版都用這同一個實體，差在生成的位置／時間／scale／碰撞框比例。 |

### 6.1 地刺的一生（`BossSpike`）

三段狀態機（`Run()` 協程）：
1. **預警**：地上播「箭頭往下」(VfxTable **13**) `WarnTime`(1.0s)——無碰撞、不傷人、打不到。給玩家反應時間。
2. **冒出（危險窗）**：播「榕樹妖地刺」(VfxTable **11**)，`EruptRise`(0.25s) 後開 `ActiveTime`(1.1s) 的危險窗——這段才開 trigger 碰撞框（**Enemy 層**）＋掛 `EnemyContactDamage`：
   - **碰到玩家扣血**＝沿用怪物接觸傷害那套（走玩家無敵幀、**連續扣**）。
   - 實作 `IDamageable`：**玩家打「露在外面的地刺」→ 傷害轉給榕樹妖本體**（`_boss.TakeDamage`），並讓地刺特效**閃白光**（`VfxInstance.Flash`，同怪物受擊）。收回/預警期間打不到（`_exposed` 擋）。
3. **收回**：關碰撞、等地刺動畫視覺播完，銷毀自己。

### 6.2 地刺的「受傷範圍」＝貼齊可見地刺（base-anchored，關鍵）

地刺的圖是**從底部往上長、實體集中在圖的下半、上半是空的**（`fanfx2_earth_spikes`：160×128、pivot 置中、內容底部貼齊圖底、往上約佔六成高）。所以碰撞框**不能**用「以圖正中心為中心的框」——那會落在刺的上半、圈不到根部，或在大 scale 下上半空白處誤傷。

**做法**：冒出當下讀地刺特效的可見邊界（`VfxInstance.WorldBounds`），把碰撞框**貼齊圖底（地刺基座）往上長**：
- 寬 ＝ 可見寬 × `hitFillW`(0.75)、高 ＝ 可見高 × `hitFillH`(0.60)；框底對齊可見底、往上長。
- **所有地刺共用同一規則**（`Fire()` 的預設參數就是 0.75/0.60）——一般地刺、地刺浪、狂亂地刺、放大版全都貼齊可見刺叢、圈得到根部。
- 後備：抓不到特效可見邊界時，退回固定核心框 `HitBoxW/H`(0.9×1.3)×scale。

> **除錯模式**：`BossSpike.DebugDrawHitbox`（static bool，預設 **false**）＝true 時，每根地刺在危險窗會用 **紅色矩形（LineRenderer）** 把實際受傷範圍畫在 **Game View**（Scene 的 Gizmo 在 Game View 看不到，故自畫）。抓手感時打開、對照紅框調 `hitFill`，調完關掉。

### 6.3 三階段（讀 `MonsterController.HealthFraction`）

| 階段 | 血量 | 行為 |
|---|---|---|
| 1 | >50% | 隨機灑 `P1_Spikes`(3) 根、每 `P1_Interval`(3.0s)。慢而少。 |
| 2 | 50%~20% | 加量加速：`P2_Spikes`(5) 根、每 `P2_Interval`(1.8s)。 |
| 3 | <20% | **只放大絕、不夾一般地刺**，三招隨機輪流，每 `P3_UltimateInterval`(3.5s)。 |

> **測試開關** `ForcePhase`（檔案上方 const）：0＝依血量自動切換（正式用）、1/2/3＝一進關卡就強制那一階段。測完改回 **0**。目前為方便測第三階段留在 3。

### 6.4 第三階段的三招大絕

| 招 | 方法 | 說明 |
|---|---|---|
| 橫掃浪（RowSweep） | `RowSweep()` | 把 `Sweep_TotalRows`(5) 排攤在可走區的 y 範圍、只收「有地刺可放」的排，隨機挑不重複 `Sweep_RowsPerCast`(2) 排，逐排**反方向**、起跑時間用遞增 `startDelay` 錯開＝**推進浪**（`Sweep_ColStagger`/`Sweep_RowGap`）。 |
| 大地刺（GiantSpike） | `GiantSpike()` | 一根放大版，固定在**畫面正中間**（相機中心；`Camera.main` 沒 tag 時退回 `FindObjectOfType`）。`Giant_Scale`(2.24＝原 2.8 的 80%)、`Giant_Damage`(30)。碰撞框同樣 base-anchored 貼齊可見刺。 |
| 狂亂地刺（SpikeStorm） | `SpikeStorm()` | 一次在隨機可走點灑 `Storm_Spikes`(20) 根一般地刺（同 1/2 階段的地刺，量爆多）。 |

**出招頻率（不完全隨機）**：`_lastUlt`/`_ultRepeat` 記上一招與連續次數——**同一招最多連兩次，第三次強制換別招**（若已連放 2 次，這次就從另外兩招裡挑）。所以 A-A-B、B-B-C 可以，A-A-A 不會。

### 6.5 地圖與資料

- **地圖**：`StreamingAssets/MapAssets/Modules/RedBridalGown/Maps/RedBridalGown_TreeDemon.dipanmap`（18×10、walkSubdiv=4）。**只有下半可走**（樹在上方背景），地刺與隨機點都落在這塊。
- **MonsterData.csv 第 14 列**：`BanyanTree`，HP 100、`BrainType=BanyanTree`、`Weapon=Contact`、`Speed=0`（不動）、`ContactDamage=0`（本體不靠碰撞、傷害全走地刺）、`InvincibleTimeMs=0`、`DetectionRange=40`（整場都算得到玩家，本體不動要能一直施壓）。
- **`MonsterController` 支援**：`HealthFraction`／`CurrentHealth` 供 Brain 讀血切階段；`TakeDamage` 為 public（`IDamageable`）供地刺反傷。

### 6.6 手感調整位置

全部在 `BanyanTreeBrain.cs` 上方 const（各階段間隔/數量、三大絕的排數/推進速度/大地刺 scale 與傷害/狂亂數量、`Giant_HitFillW/H`）＋ `BossSpike.cs` 上方 const（`WarnTime`/`ActiveTime`/`PlayerHitInterval`、後備核心框）。改數值即可、不動邏輯。

### 6.7 待辦

- [ ] **換臉**：攻擊時臉切 `treeFace_crazy`、平時 `treeFace_vicious`（`MapLoader` 依 assetId 掛一個 BossFace 元件，Brain 施放時通知切換）。**尚未做**。
- [ ] 測完把 `ForcePhase` 改回 0、`DebugDrawHitbox` 確認為 false、移除 `BanyanTreeBrain` 裡的 `[Banyan]` 除錯 log。
- [ ] 實機微調三階段難度（間隔/數量/大絕頻率）與地刺傷害。

