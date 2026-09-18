# Boss 戰鬥模組 (Boss Combat Modules) + 召喚系統

> 返回 [文件總覽](README.md)｜怪物量產見 [MONSTER_SETUP.md](MONSTER_SETUP.md)｜武器/配方見 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)｜傷害結算見 [COMBAT.md](COMBAT.md)｜劇情分支見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §7
>
> **狀態：✅ 框架 + 紅嫁衣 boss（逃跑＋召喚，2026-07-09；＋大絕「家人齊聚」與 pant 喘息，2026-09-16 程式完成、⏳未編譯未實測）；✅ 榕樹妖 boss（地刺／三階段／三大絕／整棵樹燃燒死亡演出）程式完成（2026-07-10）；✅ boss 死亡回收招式（地刺/召喚物）。** ✅ **射手型 `ArcherBrain` ＋ 怪物投射武器管線 `WeaponCastService`**（2026-09-17，見 §8）程式完成、⏳未編譯未實測。** ✅ **跳躍踐踏型 `LeapSlamBrain` ＋ 程序化裂地 shader**（2026-09-18，見 §9）與 **近戰追擊 `MeleeChaseBrain`（攻擊動作要做完才能再移動，見 §10）** 程式完成、⏳未編譯未實測。** 待實機微調。投射型武器供怪物使用：**`Normal` 直飛彈已打通**，其餘模式（雷射/拋物線/落雷…）仍待搬進 WeaponCastService。

「一隻強怪＝一個 Brain 模組」。第一個範例是紅嫁衣女殭屍。未來每隻 boss 都新增一個自己的 Brain 類別，其它系統不動。

> **非 boss 的可復用行為模組也住這裡**（§7 撲擊型／狗狼）——它們走的是同一套 `IMonsterBrain` 地基與同一份「怎麼加下一隻」SOP，
> 拆成另一份文件只會讓兩邊漂移。（三方陣營的 `WarBrain` 是例外，它的家在 [FACTION.md](FACTION.md)，因為那是陣營玩法的一部分。）

---

## 1. 模組化地基（三塊）

| 元件（`Assets/Scripts/AI/`） | 角色 |
|---|---|
| `IMonsterBrain.Think(in MonsterContext)` | 決策機介面。簽名已從舊 `Think(actuator, player)` 升級為傳 **`MonsterContext`**（打包 Self/Actuator/Sensor/Player/DeltaTime）。Brain 要更多能力只往 context 加欄位，不改介面。 |
| `MonsterContext`（struct） | 每幀決策脈絡。`ChaseBrain` 只讀 Actuator/Player；boss 級 Brain 另讀 `Self` 拿 `WeaponUser` 施放技能。 |
| `MonsterWeaponUser`（元件，seam） | **怪物「使用一把武器」的統一入口**。`Configure(owner, weaponId)`；Brain 每幀呼叫 `TryUse(target)`，冷卻（配方 `FireInterval`）與召喚上限都在這裡結算。依配方型別分派：`Mode=Summon`（召喚）與 **`Mode=Normal`（直飛投射彈，2026-09-17 接上，走 `WeaponCastService`，見 §8.4）**；其餘模式待搬。 |

**怪物怎麼拿到武器**：`MonsterData.csv` 的 `Weapon` 欄以前閒置，現在**填 WeaponTable 的武器 ID（數字）**＝這隻怪掛 `MonsterWeaponUser` 用那把武器；填 `Contact`／空／非數字 = 不掛（只近戰接觸傷害）。`MonsterController.Initialize` 依此掛上並 `Configure`。

**怎麼加下一隻 boss**：① 寫 `Behaviors/XxxBrain.cs implements IMonsterBrain`；② `MonsterController.Initialize` 的 `switch (data.BrainType)` 加一個 `case "Xxx"`；③ `MonsterData.csv` 那隻怪的 `BrainType` 填 `Xxx`、`Weapon` 填牠要用的武器 ID。就這樣。

---

## 2. 紅嫁衣女殭屍（`RedBridalGownBrain`）

**行為**：躲玩家＋定時召喚家人幽靈當追兵。
- **逃跑**：玩家進入 `FleeRange`(4.0) 內 → 往「反方向」逃（用她自己的 `MonsterData.Speed`）；拉開到 `SafeRange`(6.5) 才停（遲滯避免臨界抖動）。速度刻意低於玩家（玩家 5、她預設 3.5），追不追得上靠這個調，**不靠把她卡住**。
  - **走 A\* 尋徑**（`MonsterActuator` 預設 `AvoidObstacles=true`）：會自動繞牆／家具，和其他怪一樣不做硬碰撞。（早期版本曾「刻意不繞路讓她卡住」，**已不是現況**。）
  - **⭐ 跑跑停停的節奏**（2026-09-08）：跑滿 `FleeBurstDistance`(5 世界單位) →`FleeRestSeconds`(0.8) 站著喘，循環；`FleeBurstMaxSeconds`(6) 是保險上限。**Burst 綁距離不綁時間**——綁時間的話 CSV 的 `Speed` 一調小，每段跑的距離就等比縮水（Speed 0.5 時只挪 0.75 格），變成「偶爾抽動一下」，見 [PROBLEMS.md](PROBLEMS.md) **F20**。喘息期間**召喚照常**。目的是給玩家攻擊窗口，玩家不必整場追著她跑。**這是明確參數，刻意不依賴「被打時擊退窗口會阻斷 Think」那個副作用**（見 [PROBLEMS.md](PROBLEMS.md) **F19**）。被牆卡住的那幾幀不算進 Burst，免得她在死角空轉完跑步額度、接著又站著喘。
  - **⭐ 跑之前先確認「真的有路可跑」**（2026-09-08）：以「玩家反方向」為 0°，依 `FleeScanAngles`（0, ±25, ±50, ±75, ±100, ±125, ±150，偏離小的優先）掃描候選方向，每個要同時滿足「落點 `IsWalkableWorld`」＋「`HasLineOfSight` 走得過去」＋「不會反而更靠近玩家」；取第一個可行的當目標，**四面都沒路就 `Stop()` 站住**（動畫自然回 idle）。沒有這一關的話，逃跑目標點會落進牆裡、被 A* 的 `NearestWalkable` 吸附成「她自己腳下那格」，變成滿速在原地來回＝**原地踏步**（成因與通則見 [PROBLEMS.md](PROBLEMS.md) **F18**）。
- **召喚**：**只看冷卻、不綁逃跑狀態**（她多半在逃，若綁「安全才召」會幾乎不召）。召喚是一把 WeaponTable 武器（見 §3），冷卻/名單/數量/上限全走配方。
- **⭐ 大絕「家人齊聚」（2026-09-16，血量 ≤ `UltHpThreshold` 0.5，一輩子只放一次）**：一口氣把家人幽靈
  **每一種各叫一隻**（怪物 2~12 共 11 隻）出來，接著 **pant（喘息）`UltPantSeconds`(10) 秒完全停擺**——不逃、不召、站著喘，那是留給玩家的輸出窗口；喘完回到平時的逃跑＋定時召喚，不會再放第二次。
  - **走另一把獨立的召喚武器**（`UltimateWeaponId` = 武器 15 → 配方 28）。`MonsterWeaponUser` 是「一個元件＝一把武器＋一份分身名單」，**分開掛才能讓大絕那 11 隻不佔平時 `SummonMaxAlive` 的額度**（喘完照樣能再召 2 隻）。Brain 在 `EnsureInit` 時自己 `AddComponent` 第二個（`ctx.Self.WeaponUser` 已快取成第一個，不受影響）。
  - **死亡回收沒漏掉它們**：`MonsterController.Die` 收的是身上**全部**的 `MonsterWeaponUser`（`GetComponents`，2026-09-16 由單數改複數）——否則 boss 死後那 11 隻會留在場上繼續追殺玩家，違反 §6.7。
  - **出手 → 喘息的接縫**：施放成功時 `MonsterWeaponUser` 已呼叫 `NotifySkillCast`（播 attack），所以 pant 要**延後** `SkillCastAnimSeconds`(0.6) 才開始，否則 pant 優先度較高會當場把出手動作蓋掉。⚠ 這個延後**排程在 `MonsterController.PlayPant(秒, 延遲)` 裡、呼叫一次就好**，刻意不是 Brain 每幀輪詢——被打時的擊退窗口會**整段跳過 `Think()`**（見 [PROBLEMS.md](PROBLEMS.md) **F19**），輪詢式寫法會讓玩家猛打時 pant 延後甚至不播。
  - **pant 動畫**：`MonsterAnimator.State.Pant`，圖放 `Monsters/SequenceImage/RedBridalGown/pant/`（**要跑 Sync Map Assets**）。沒有 pant 圖會自動退回 idle（不是 walk——喘息時站著不動，退成走路會變原地踏步），所以素材還沒到位也不會壞。
    兩個 pant 專屬參數在 `MonsterAnimator` 上方：**`PantFpsMul`**（0.25＝幀率是 CSV `AnimFPS` 的四分之一；喘氣照原速播太急促。一輪秒數 ＝ 張數 ÷（AnimFPS × 本倍率））與 **`PantPingPong`**（true＝**乒乓來回播** 0→N→0→…；喘氣是吸↔吐的往復、首尾本來就接不起來，一般循環每輪會跳接一次，倒著播回去接縫自然消失。素材若是不對稱動作就關掉，否則像倒帶）。
  - **施放時的喊話不寫在 Brain 裡**：MonsterData 句子4 填 `50%: 家人們，一起出來吧`，血量門檻句在**第一次跌破時會強制播報**（2026-09-16 起，見 [MONSTER_SPEECH.md](MONSTER_SPEECH.md)），和 `UltHpThreshold` 讀同一個血量 ⇒ 自然同時發生。別的 boss 要「放招時喊話」照抄這個做法：台詞門檻填成和招式門檻同一個數字即可。
  - **手感調整**：`RedBridalGownBrain.cs` 上方三個常數（`UltHpThreshold` / `UltPantSeconds` / `UltimateWeaponId`）；要她一次叫出來的陣仗更散就調配方 28 的 `SummonRadius`。
- **手感常數**都在 `RedBridalGownBrain.cs` 上方（`FleeRange`/`SafeRange`/`DetectionRange`/`AwayLookahead`/`FleeScanAngles`）；逃跑速度走 CSV `Speed`。**她太容易站著不動**＝掃描太嚴（把 `AwayLookahead` 調小，落點更近就更容易可走）；**她太滑溜繞不死**＝把 `FleeScanAngles` 的大角度砍掉（只留 ±75° 內就不會沿牆逃）。

**boss 死亡＝召喚物回收**：紅嫁衣被打敗時，她召喚出來、還活著的家人幽靈會**當場被回收清除**（`MonsterWeaponUser.RecallSummons`，由 `MonsterController.Die` 呼叫）——boss 死了招式不該還在場上。這是通用機制，見 §6.7。

**與「殺家人」劇情分支的關係（重要）**：`killedFamily` 旗標綁「**編輯器擺放的家人怪出生點**上的死亡觸發旗標」（見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §7）。**boss 召喚出來的分身刻意不帶 `deathFlag`**（`MonsterWeaponUser.TrySummon` 呼叫 `SpawnMonster(id, pos)` 不傳旗標），所以玩家殺召喚分身**不會**誤觸 killedFamily——分支邏輯天生乾淨。

---

## 3. 召喚做成一把武器（表驅動、玩家未來可共用）

召喚是 `RecipeTable` 的一種新型別 `Mode=Summon`（與雷射/拋物線/連鎖…同層級）。「發射動作」＝呼叫 `MonsterSpawner.SpawnMonster` 生怪。冷卻沿用 `FireInterval`。

**RecipeTable 新增 5 欄**：

| 欄 | 意義 |
|---|---|
| `Mode=Summon` | 1 = 召喚型（不發射子彈） |
| `SummonIds` | 可召喚的怪物 ID 池，**用 `|` 分隔**（避開 CSV 逗號），例 `8\|9\|10\|11\|12`；每次隨機抽 |
| `SummonCount` | 每次召喚幾隻（空=1） |
| `SummonMaxAlive` | 同一施放者的分身**同時存在上限**，達上限暫停召喚（空=4） |
| `SummonRadius` | 在施放者周圍多遠的環上生成（空=2） |

**RecipeTable 第 6 欄（2026-09-16 新增）**：

| 欄 | 意義 |
|---|---|
| `SummonEachOnce` | 1 = **池裡每個 ID 各召一隻**（忽略 `SummonCount`、不重複抽，且**生成角度平均分開**——十幾隻各自隨機取角會擠成一團、互相卡位又看不出陣仗）。數量仍受 `SummonMaxAlive` 夾，所以那一筆要把上限填到 ≥ 池子大小才會一次到齊。空/0 = 照 `SummonCount` 隨機抽（舊行為） |

**現有資料**（2026-09-16 更新，之前寫的 `8|9|10|11|12`／`MaxAlive=5` 已過時）：

| | 平時召喚 | 大絕「家人齊聚」 |
|---|---|---|
| 武器 | 14「紅嫁衣召喚家人」 | **15「紅嫁衣大絕-家人齊聚」** |
| 配方 | 26（`FireInterval=3`, `SummonIds=2\|3\|…\|12`, `SummonCount=2`, `SummonMaxAlive=2`, `SummonRadius=2`） | **28**（`SummonIds=2\|3\|…\|12`, **`SummonEachOnce=1`**, `SummonMaxAlive=11`, `SummonRadius=3.5`, `FireInterval=0.1`） |
| 掛在哪 | 怪物 13 的 `Weapon=14`（`MonsterController.Initialize` 掛） | `RedBridalGownBrain.EnsureInit` 自己 `AddComponent` 第二個 `MonsterWeaponUser` |

配方 28 的 `FireInterval` 只是「起手緩衝」（`MonsterWeaponUser` 生成後先等一個冷卻週期），**「只放一次」是 Brain 的 `_ultUsed` 旗標控制的，不是靠冷卻**。召喚出的家人幽靈一律用最基本的 `Chase`（ChaseBrain）追玩家。

**玩家側已接（2026-07-09，測試用）**：召喚核心抽成擁有者無關的共用靜態 `SummonSystem.Cast(owner, originPos, recipe, aliveTracker)`——`MonsterWeaponUser`（boss）與 `PlayerController.Shoot`（玩家）都呼叫它，各持一份 alive 清單管同時上限。玩家 Shoot 在「需要 BulletPrefab」的守衛**之前**先攔 `Mode=Summon`：耗魔→播發射特效→`SummonSystem.Cast`→`_fireTimer=FireInterval`（按住左鍵依冷卻重複召喚）。
- **測試武器＝13 號「御靈水晶」**（RecipeID→27）。配方 27：`Mode=Summon`, `FireInterval=1.5`, `SummonIds=1`（ZhaYu，Main 的怪、**任何地圖都載得到 idle+walk**，故不必先 Sync 就能測）, `SummonCount=1`, `SummonMaxAlive=3`, `SummonRadius=1.5`。切到御靈水晶、按住左鍵即在身邊召喚。要召家人幽靈把 `SummonIds` 改 `8|9|10|11|12`（需在 RedBridalGown 地圖或先 Sync 讓幽靈 walk 幀到位）。
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
- [ ] **大絕（2026-09-16）⏳ 未編譯未實測**：跑 `Sync Map Assets` 把 `RedBridalGown/pant/`（50 張，目前是拿 idle 頂替、真圖製作中）帶進 StreamingAssets；實機看 ① 11 隻在 boss 房（10×18）散不散得開、會不會卡牆 ② 10 秒喘息夠不夠玩家輸出 ③ 出手 0.6 秒接 pant 的接縫順不順。真 pant 圖到位後**要再跑一次「計算影子錨點」**。
- [x] ~~boss 只會追擊、不逃跑不召喚~~ → **已修**（BrainType 沒 Trim，見 [PROBLEMS.md](PROBLEMS.md) F4）。修後若仍要驗證：確認 MonsterData.csv 已被 Unity 重匯入、boss 出生點填怪物 ID 13。
- [ ] **怪打怪傷害忽勝忽敗**（召喚物 vs 敵怪）＝接觸傷害每幀結算＋怪 InvincibleTimeMs=0＋近乎一擊斃命 → 勝負看 Update 順序。**待調數值**：給互毆的怪 `InvincibleTimeMs`>0（300~500）＋平衡 HP/ContactDamage。詳見 [PROBLEMS.md](PROBLEMS.md) F5。
  > ⚠ **做這條之前先讀 [PROBLEMS.md](PROBLEMS.md) F19**：擊退窗口會整段跳過 `Think()`，所以 `InvincibleTimeMs` 同時是一份「隱形的行為預算」——給紅嫁衣加了無敵幀，她被打時能做決策的時間會變多，**逃跑手感會跟著變**（她會變得更難纏）。節奏本身已經參數化在 `RedBridalGownBrain`，改完對照 `FleeBurstSeconds`/`FleeRestSeconds` 重調即可，別以為是別處壞掉。
- [ ] boss 召喚的家人幽靈(8~12)若只有站姿/看不到 → 幽靈 walk 幀還沒進 StreamingAssets，跑 `Project Tools → Sync Map Assets`。
- [ ] （Phase 2）要讓怪物射飛劍/落雷 → 把 PlayerController 發射管線抽成共用服務，`MonsterWeaponUser` 非召喚分支接上。

---

## 6. 榕樹妖（`BanyanTreeBrain` ＋ `BossSpike` ＋ `BanyanBossFace`）— 打「牠的攻擊物」反傷本體

**概念**：跟紅嫁衣完全相反的一種 boss。榕樹妖本體＝**背景的一棵樹、無法直接攻擊**（本體是**無圖隱形的 `MonsterController`**，只管血量＋編排攻擊、不移動）。牠用**地刺**攻擊玩家；玩家要**閃過地刺**，並**攻擊「冒出來的地刺」**把傷害反饋給本體。臉是地上物（`treeFace_vicious`）——原設計攻擊時換 `treeFace_crazy` 臉，但**兩張素材搭不上、已決定不換臉**（維持 vicious）；換臉管線（`BanyanBossFace.FlashAttacking`）保留但停用，日後有合適素材再由 brain 呼叫即可恢復。

**三個檔案**：
| 檔案（`Assets/Scripts/AI/`） | 角色 |
|---|---|
| `Behaviors/BanyanTreeBrain.cs` | boss 腦：讀血量切階段、編排每一波地刺。手感常數全在檔案上方。 |
| `BossSpike.cs` | **地刺攻擊實體**（一次性、自跑狀態機）。單根／一排／放大版都用這同一個實體，差在生成的位置／時間／scale／碰撞框比例。 |
| `BanyanBossFace.cs` | 臉地上物的控制器（MapLoader 依 assetId 自動掛）。主責＝**boss 死亡的整棵樹燃燒演出**（見 §6.6）；另含停用中的換臉管線。 |

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

| 階段 | 進入條件 | 行為 |
|---|---|---|
| 1 | 血量 > `P2_HpEnter` | 隨機灑 `P1_Spikes`(3) 根、每 `P1_Interval`(3.0s)。慢而少。 |
| 2 | 血量 ≤ `P2_HpEnter` | 加量加速：`P2_Spikes`(5) 根、每 `P2_Interval`(1.8s)。 |
| 3 | 血量 ≤ `P3_HpEnter` | **只放大絕、不夾一般地刺**，三招隨機輪流，每 `P3_UltimateInterval`(3.5s)。 |

- **階段切換血量已資料化**：`P2_HpEnter` / `P3_HpEnter`（`BanyanTreeBrain.cs` 上方 const，填百分比小數：0.5＝50%，`≤` 該值就進下一階段；`P2` 要比 `P3` 大）。第一階段不用填（滿血就是 P1）。
- **測試開關** `ForcePhase`：0＝依血量自動切換（正式用，現值）、1/2/3＝一進關卡就強制那一階段。測完務必留 0。

### 6.4 第三階段的三招大絕

| 招 | 方法 | 說明 |
|---|---|---|
| 橫掃浪（RowSweep） | `RowSweep()` | 把 `Sweep_TotalRows`(**3**，場地縮小後由 5 改 3) 排攤在**可走內縮框**的 y 範圍、只收「有地刺可放」的排，隨機挑不重複 `Sweep_RowsPerCast`(2) 排，逐排**反方向**、起跑時間用遞增 `startDelay` 錯開＝**推進浪**（`Sweep_ColStagger`/`Sweep_RowGap`）。 |
| 大地刺（GiantSpike） | `GiantSpike()` | 一根放大版，固定在**畫面正中間**（相機中心；`Camera.main` 沒 tag 時退回 `FindObjectOfType`），並**夾進可走內縮框＋驗證落點可走**（相機中心可能落在樹背景／HUD 等不可走處）。`Giant_Scale`(2.24＝原 2.8 的 80%)、`Giant_Damage`(30)。碰撞框同樣 base-anchored 貼齊可見刺（`Giant_HitFillW/H`）。 |
| 狂亂地刺（SpikeStorm） | `SpikeStorm()` | 一次在隨機可走點灑 `Storm_Spikes`(20) 根一般地刺（同 1/2 階段的地刺，量爆多）。 |

**出招頻率（不完全隨機）**：`_lastUlt`/`_ultRepeat` 記上一招與連續次數——**同一招最多連兩次，第三次強制換別招**（若已連放 2 次，這次就從另外兩招裡挑）。所以 A-A-B、B-B-C 可以，A-A-A 不會。

### 6.5 地圖與資料

- **地圖**：`StreamingAssets/MapAssets/Modules/RedBridalGown/Maps/RedBridalGown_TreeDemon.dipanmap`（18×10、walkSubdiv=4）。**只有下半可走**（樹在上方背景），地刺與隨機點都落在這塊。Game 層有 1 個地上物＝臉 `treeFace_vicious`（x≈8.76, y≈-1.20），MapLoader 依 assetId 掛 `BanyanBossFace`、並把它**改成不可破壞**（boss 不能被直接打，臉被打爆會破壞死亡演出）。
- **MonsterData.csv 第 14 列**：`BanyanTree`，HP 100、`BrainType=BanyanTree`、`Weapon=Contact`、`Speed=0`（不動）、`ContactDamage=0`（本體不靠碰撞、傷害全走地刺）、`InvincibleTimeMs=0`、`DetectionRange=40`（整場都算得到玩家，本體不動要能一直施壓）。`DeathVfxId` 在 brain 掛上時被設 0——死亡改用臉的自訂燃燒演出，不放一般死亡特效。
- **`MonsterController` 支援**：`HealthFraction`／`CurrentHealth` 供 Brain 讀血切階段；`TakeDamage` 為 public（`IDamageable`）供地刺反傷。

### 6.6 死亡演出：整棵樹燃燒（`BanyanBossFace`）

boss 血量歸零 → `MonsterController.Die()` 呼叫 `BanyanBossFace.Instance.PlayDeath()`，跑一段燃燒演出（協程）：
1. **臉的位置先起火**（VfxTable **16** 紅色鬼火），燒 `FaceBurnTime`(1.0s)。
2. **臉地上物消失**（火繼續燒）。
3. **各地陸續起火、越冒越多，最後鋪滿整棵樹**：把樹的範圍（以臉為中心的世界盒 `TreeAreaHalfW`/`Top`/`Bottom`）用 `FireSpacing`(1.0) 切成網格火點、洗牌成隨機順序、逐點點燃，**間隔越點越短**（`SpreadStartInterval`→`SpreadMinInterval`，每點 ×`SpreadAccel`(0.88)）＝越冒越多；鋪滿即止、不再新增。
- 火焰用 `VfxManager.SpawnLoop`（複製一份 VfxData 覆寫 Loop/Duration，不動共用表資料）；壽命 `FireLife = -1`＝**無限循環永不熄滅**（著火就一直燒）。
- **不設固定數量上限**：火點數＝樹範圍 ÷ 間距（現值約 **13×4 ＋臉 ≈ 53 個**同時在場）。要更多/更少改 `FireSpacing`；太吃效能就把它調大。手感常數全在 `BanyanBossFace.cs` 上方。

### 6.7 ⭐ boss 死亡 → 招式（技能內容）立刻回收（通用原則）

**boss 死了，牠正在場上的招式必須當場撤掉、不能繼續傷人。** 一律在 `MonsterController.Die()` 那一刻處理（與死亡同幀）：

| Boss | 要回收的東西 | 機制 |
|---|---|---|
| 榕樹妖 | 所有**地刺**（含預警箭頭中、正冒出中、排掃/狂亂裡排隊等 `startDelay` 還沒冒的） | `BossSpike` 維護一份全場登記表（`_active`，Init 註冊 / OnDestroy 註銷）；`BossSpike.CancelAll()` 一次把每根地刺連同它的**預警箭頭 + 地刺特效**一起銷毀（`CancelImmediate`），不留殘影、不再判傷。`Die()` 對 `BanyanTreeBrain` 呼叫。 |
| 紅嫁衣（及任何召喚型 boss） | 還活著的**召喚分身**（家人幽靈…） | `MonsterWeaponUser.RecallSummons()` 把自己 `_summoned` 清單裡還在的分身全數 `Destroy`、清空。`Die()` 對「身上有 `MonsterWeaponUser`」的怪一律呼叫——通用，不限紅嫁衣。 |

> **加下一隻會「留下持續性招式」的 boss 時記得比照**：把該招式的實體登記進一個可一次清除的表（像 `BossSpike._active`），在 `Die()` 收掉。避免「boss 死了、地上的火/毒/分身還在扣血」這種穿幫。

### 6.8 手感調整位置

- `BanyanTreeBrain.cs` 上方 const：階段血量門檻（`P2_HpEnter`/`P3_HpEnter`）、各階段間隔/數量、三大絕的排數/推進速度/大地刺 scale 與傷害/狂亂數量、`Giant_HitFillW/H`。
- **地刺生成安全內縮**（2026-07-18）：`SpawnEdgeInset`(0.5，上/左/右)、`SpawnBottomInset`(1.0，HUD 那一側)＝地刺（隨機灑/排掃/大地刺）只在「可走區再往內縮」的框內生成，避免貼著底部操控列 HUD／左右血魔球冒刺。覺得還太靠邊就把數值調大。所有生成都走 `SpawnBounds(nav)`（= `MapNavGrid.WalkableBounds()` 內縮）。
- `BossSpike.cs` 上方 const：`WarnTime`/`ActiveTime`/`PlayerHitInterval`、後備核心框、除錯 `DebugDrawHitbox`。
- `BanyanBossFace.cs` 上方 const：死亡燃燒的火種類/大小/密度/蔓延節奏/範圍/壽命。

### 6.9 待辦

- [x] ~~換臉（攻擊 crazy / 平時 vicious）~~ → **決定不換臉**（兩張素材搭不上），維持 vicious；管線保留停用。
- [x] ~~死亡演出~~ → **完成**（整棵樹紅色鬼火此起彼落、鋪滿、無限燒）。
- [x] ~~boss 死亡回收招式~~ → **完成**（地刺 `CancelAll` / 召喚 `RecallSummons`）。
- [ ] 測完把 `ForcePhase` 確認為 0、`DebugDrawHitbox` 確認為 false、移除 `BanyanTreeBrain` 裡的 `[Banyan]` 除錯 log。
- [ ] 實機微調三階段難度（間隔/數量/大絕頻率）、地刺傷害、死亡火焰密度/效能。

---

## 7. 撲擊型（狗／狼／豹…）— `PounceBrain`

> **狀態：✅ 程式完成（2026-09-17），⏳ 未編譯、未實機驗證。** 第一隻使用者：**怪物 17「Wolf Warrior」戰狼**（BloodFang）。

`ChaseBrain` 是「一路貼上去磨」，這支是**掠食者的節奏**：保持距離觀望 → 蓄力 → 突然加速直線撲過來 → 收招喘一下。
玩家要讀的是牠的節奏（看到停下來蓄力就準備側身閃），而不是走位拉扯。**狗／狼／豹／蜘蛛都可以復用同一支**，
BrainType 填 `Pounce` 即可。

### 7.1 六段狀態機（2026-09-17 改成「停→移→停→衝」）

| 階段 | 行為 | 常數（`PounceBrain.cs` 檔頭） |
|---|---|---|
| `Approach` | 超出觀望圈 → 正常速度接近 | `StalkRange` 5.0 |
| `StalkHold` | **站定觀望**——掠食者在等破綻。其實是「保持距離」：被擊退推出觀望圈會自己走回來（見 §7.3 第 11 點） | `HoldSecondsMin/Max` 0.6/1.2 |
| ↑ 交替輪數 | 1~2 輪（第一版 1~3，作者回報「在我旁邊晃啊晃 3~4 次」太多） | `StalkRoundsMin/Max` 1/2 |
| `StalkStep` | 往側邊**移動一小段固定距離就停**（落點進來時算好，不每幀追著目標修正） | `StepDistMin/Max` 1.5/2.5、`StepAngleMin/Max` 40/70、`SideFlipChance` 0.4、`StepMaxSeconds` 2.0（保險） |
| `Windup` | 站定蓄力＝**玩家的預告窗口** | `WindupSeconds` 0.4 |
| `Charge` | **全程追著目標當下的位置衝**、速度 ×3；**碰到**／超時／撞到東西就結束，**不管有沒有咬到都播咬的動作** | `ChargeSpeedMul` 3.0、`ChargeTimeSlack` 1.6、`ChargeTimeBuffer` 0.25 |
| `Recover` | 站定收招＝**玩家的輸出窗口**，然後回 `StalkHold` | `RecoverSeconds` 0.7 |

**第一版是「持續繞圈」**（`Stalk` 每幀都在 `MoveTowards` 繞行點），作者實機回報「比較像是不斷地在移動，
移動→移動→移動→攻擊，完全不會停」。掠食者的節奏感全在**站定**那一段，所以拆成 Hold／Step 交替。

**衝刺是「追著你現在的位置」，不是「撲向你剛才站的地方」**（見 §7.3 第 5 點），
而且**時限依起跑距離動態算**（`(距離 ÷ 衝刺速度) × ChargeTimeSlack + ChargeTimeBuffer`）——
固定秒數的話「不管多遠都只前進那麼多」，離遠一點就衝到一半停下來咬空氣。
⭐ `ChargeTimeSlack`(1.6) 是「玩家能不能靠移動躲掉」的旋鈕：**1.0 ＝ 只夠跑完起跑距離（你一動就閃掉）**，
越大越黏。嫌太難纏先調它，再調 `ChargeSpeedMul`。

**貼身一律用碰撞框邊緣距離**（`Physics2D.Distance`），不是中心距離——這是「咬的動作有播、血卻沒掉」的
根因，見 [PROBLEMS.md](PROBLEMS.md) **F22**。`ContactSlack`(0.02) 與接觸傷害同一條線，
`BiteSlack`(0.25) 是觀望時「算不算已經貼身」的決策門檻（寬一點避免邊緣抖動）。

**貼身**：距離 ≤ `BiteRange`(1.3) 就站定咬、不撲（撲出去反而會拉開距離）；
但咬滿 **`BiteMaxSeconds`(1.5)** 會強制退開一步重新拉距離——見 §7.3 第 3 點。

### 7.2 這支 Brain **沒有**傷害程式，也沒有動畫程式

兩件事都是既有系統白送的，加新的撲擊型怪時不必重做：

- **傷害**：`EnemyContactDamage` 每幀檢查重疊，同一目標每 `AttackInterval` 秒扣一次 `ContactDamage`。
  所以「撲擊撞到」與「貼身咬」在數值上**是同一筆傷害**，差別只有動畫。要讓撲擊更痛得另外給接觸傷害加倍率（尚未做，見 §7.5）。
- **attack 動畫**：`MonsterController.HandleVisuals` 只要「目標距離 ≤ `AttackRange`(1.3) 且這隻怪有 attack 幀」
  就自動演攻擊動作。所以咬擊的視覺是現成的，Brain 一行都不用寫。

### 7.3 兩個踩過的坑，寫在這裡免得下一隻重踩

1. **衝刺綁「距離」不綁「時間」**：落點 ＝ 起跑時目標的位置再往前延伸 `ChargeOvershoot`，`ChargeMaxSeconds` 只是保險上限。
   綁時間的話 CSV 的 `Speed` 一調小，每次撲的距離就等比縮水，變成「原地抽動一下」——同 [PROBLEMS.md](PROBLEMS.md) **F20**。
2. **衝刺的收尾檢查放在 `Think()` 最前面**、不放在 `Charge` 分支裡：怪被打時的擊退窗口會**整段跳過 `Think()`**
   （[PROBLEMS.md](PROBLEMS.md) **F19**），窗口一結束要第一時間把 `MoveSpeed`／`AvoidObstacles` 還原，
   否則牠會帶著 3 倍速跑去做別的事。（擊退期間速度沒還原是**無害**的：那段時間 `MoveTowards` 根本沒被呼叫。）

5. **衝刺一定要追著目標「當下」的位置**，不要鎖定起跑點：衝 3~5 單位要 0.3~0.5 秒，玩家在這段時間
   已經走掉 1.5~2.5 單位 ⇒ 撲到的永遠是他半秒前站的地方。靜態模擬：玩家繞圈側移時，鎖定版的命中率只有 **12%**，
   改成追蹤後是 **99%**（站著不動兩者都 ~100%，所以**這個 bug 只有在玩家走動時才看得出來**——作者實機回報
   「我只要有在走動，狼就咬不到我」）。
   ⚠ 但**刻意不做「預判落點」**（不算玩家前方的攔截點）：那會變成「只要出手就一定咬到」，走位完全失去意義。
   躲不躲得掉應該由 `ChargeMaxSeconds` 決定，不是由演算法的聰明程度決定。
6. **撲空也要把咬的動作播出來**：用 `MonsterController.NotifySkillCast()` 強制播，不要靠 `AttackRange` 的自動判定——
   撲空時距離不夠，自動判定不會播，畫面上就變成「衝過去然後呆站著」。
11. **「站在原地等」的狀態要想一下「被擊退推出去會怎樣」**：玩家一開火，擊退每次把狼推 0.5 單位
   （＝整張圖寬 ×`KnockbackPercent/100`），一段 0.6~1.2 秒的站定會被推出觀望圈 ⇒ 回去追 ⇒ 走回來 ⇒
   重抽輪數 ⇒ 永遠撲不出來（作者實機回報「我一攻擊牠就比較不會發動攻擊」）。
   兩處修正：回到觀望圈**不重抽輪數**；「站定」改成**保持距離**（被推遠就自己走回來）。
   模擬：擊退每 0.35 秒一次時撲擊次數 0.1 → 10.4。詳見 [PROBLEMS.md](PROBLEMS.md) **F23**。
10. **⭐ 衝刺「只前進一小段就停」＝撞牆判定被物理時序誤判**（連改三輪都沒修好的那個）：
   route B 的怪沒有繼承 prefab 的 `Rigidbody2D` 內插設定（程式從沒設過 `interpolation`），
   而物理是 60Hz ⇒ 畫面跑得比 60fps 快時**有整幀 `transform` 沒動** ⇒ 看單幀位移的撞牆判定立刻誤判。
   已在 `MonsterActuator.Awake` 補上內插，撞牆判定也改成看 0.2 秒的累積位移。詳見
   [PROBLEMS.md](PROBLEMS.md) **E37**（含三條通則，其中一條是「靜態模擬驗不了引擎時序」——
   這次模擬說沒問題、實機就是不對，就是卡在這裡）。
8. **貼身判定用中心距離＝白做工**：Brain 說「到了」、傷害系統說「還差 0.35 沒碰到」，於是咬的動作播了、
   血一點都沒掉。靜態模擬量到的咬中率是 **0%**（站著不動也一樣）。兩邊必須讀同一份幾何，見 **F22**。
   改用 `Physics2D.Distance` 之後：站著不動 90%、繞圈側移 80%。
9. **衝刺時限不能是固定秒數**：固定 0.8 秒 ×10.5 ＝ 只能跑 8.4 單位，**起跑距離再遠也只前進那麼多**
   ⇒ 作者實機回報「不管我跟他多遠，他都只前進固定距離」。改成依起跑距離算。
7. **「突襲感」是動畫給的，不是數字給的**：衝刺速度早就是 3 倍，但 `MonsterAnimator` 的走路 fps 上限
   當時寫死 1 ⇒ 腳步頻率跟散步一樣，作者實機回報「衝過去的速度跟平常移動時一樣」。加了 `MaxMul` 才對
   （見 [MONSTER_SETUP.md](MONSTER_SETUP.md)）。**之後做任何「會短暫加速」的怪都要記得這一條。**
3. **貼身咬一定要有時間上限**（`BiteMaxSeconds`）：沒有的話，玩家只要站著不動（硬扛、或正在打別的怪），
   狼撲過來之後就**永遠黏在他身上磨血**——靜態模擬跑 60 秒只撲得出 1 次、貼身磨了 53 秒。咬滿就退開一步重新拉距離。
4. **退開那一步不能被「貼身就中斷」的判定踢回來**：`StalkStep` 第一版有一條「距離 ≤ BiteRange 就中斷側移」的捷徑，
   而退開的第一幀人還在 BiteRange 內 ⇒ 立刻回 `StalkHold` ⇒ 又開始咬 ⇒ 又退……**死結**。那條捷徑已移除
   （一步最多 2.5 單位／2 秒，走完自然回 `StalkHold`，本來就不需要捷徑）。

撞牆判定：`velocity` 照送但**實際位移**低於「預期位移 × `StuckMoveRatio`(0.35)」就當撞到、提前進 `Recover`；
起跑後 `ChargeGraceSeconds`(0.12) 內不判（第一幀 velocity 才剛設、位移本來就接近 0，會誤判）。
**撞牆目前沒有額外硬直**（2026-09-17 作者拍板：撞到就結束衝刺，秒數同平常），想加就把 `RecoverSeconds` 改成依 `blocked` 分兩種。

> **這兩個死結都是靜態模擬抓到的**（把狀態機用 python 重跑一份、統計各階段時間佔比與每分鐘撲擊次數），
> 不是實機測出來的。之後改這類「有節奏的 AI」值得照做一次：卡死與節奏失衡在程式碼上看不出來，
> 但只要印出「站著不動佔幾 %、每分鐘撲幾次」就一目了然。
> 調完的參考值（Speed 3.5）：玩家站著不動 ≈ 12.8 次/分、站立佔 78%；玩家走走停停 ≈ 4.2 次/分、站立佔 55%。

### 7.4 目標怎麼取（為什麼陣營不用寫特例）

取 `ctx.Enemy` 與 `ctx.Player` 的**近者**（同 `WarBrain`）。所以同一支 Brain：
`Enemy` 陣營 → 一見到玩家就撲；三方陣營的 `Werewolf`／`Vampire` → 也會撲玩家與對面部族。

> **2026-09-17 起部族的預設是「敵對」**（[FACTION.md](FACTION.md) §0 的語意反轉）：戰狼填 `Faction=Werewolf`
> 放到任何地圖上、什麼 trigger 都不擺，就會照本模組撲擊。要牠站著不動手才擺 `factionPeace`。
> （反轉前是相反的——填了部族就預設不動手，害得「放進測試地圖測撲擊模組」這件事還得先擺一套劇本 trigger。）

### 7.6 加一隻新的撲擊型怪（SOP）

**`BrainType` 填 `Pounce` 就能用**——`PounceBrain` 裡沒有任何「戰狼專屬」的東西。但四足獸有兩個**必踩**的坑，
所以照這份清單走一遍：

| # | 做什麼 | 備註 |
|---|---|---|
| 1 | 素材丟 `Monsters/SequenceImage/<怪名>/idle｜walk｜attack` | `dead` 不需要（死亡走 `DeathVfxId` 特效）；`pant` 選配 |
| 2 | `MonsterData.csv` 加一列 | `BrainType=Pounce`、`Weapon=Contact`；**`AnimFPS` 依張數算**（張數÷fps＝一輪秒數，attack 25 張配預設 8fps 會咬 3 秒） |
| 3 | ⚠ 跑 `Project Tools → 角色 → 計算影子錨點`，**然後手改**三列的 `AnchorX`／`WidthPx` 拉齊、`Source` 改 `manual` | **四足獸必中**：站姿/跑姿的腳段數不同，工具一定算出兩種結果 ⇒ 切動作時影子位移＋忽大忽小。見 [SHADOW.md](SHADOW.md)〈四足獸型的常見誤判〉 |
| 4 | ⚠ 實機看一眼要不要填 `WalkScale` | **四足獸常中**：自動高度正規化會把「壓低身體的奔跑姿勢」放大（戰狼被放大 ×1.288）。見 [PROBLEMS.md](PROBLEMS.md) **G12** |
| 5 | `Project Tools → Sync Map Assets` | 把圖與 CSV 帶進 StreamingAssets |

**完全不用管的**（都是自動的）：腳底對齊、怪物體光、影子掛載、attack 動畫播放、傷害結算、碰撞框貼合。

**手感參數是全域共用的**（`PounceBrain` 檔頭的 `const`，所有撲擊型怪吃同一組）。其中：

- **會自動跟著怪走**：衝刺速度（`ChargeSpeedMul` 是倍率 × CSV `Speed`）、貼身/咬中判定
  （走 `Physics2D.Distance` 的碰撞框邊緣距離，體型多大都對）、各段秒數（本來就不該隨體型變）。
- **不會**：`StalkRange`(5.0)／`StalkExitRange`(5.6)／`StalkNear`(3.0)／`StepDistMin~Max`(1.5~2.5) 是**絕對世界單位**
  ⇒ 一隻小獵犬與一隻巨狼會用一樣大的觀望圈。體型差很多時會有感，那時再把這幾個常數改成從 `MonsterData` 讀即可
  （還沒做，因為目前只有戰狼一隻）。

🔧 手感對不上時先開 `PounceBrain.DebugLog = true`，見 §7.5。

### 7.5 待辦 / 已知限制

- [ ] **未編譯、未實機驗證**。第一次實測看：側移會不會卡牆、蓄力 0.4 秒夠不夠看清楚。
- 🔧 **手感/行為對不上時先開除錯 log**：`PounceBrain.DebugLog = true`（static，在任何地方設一次即可），
  每次衝刺結束會印「結束原因／起跑距離／實際跑了多遠／耗時／結束時的中心距離與**邊緣距離**」。
  `邊緣距離 ≤ 0.02` 才算真的咬到——一眼就看得出是「沒衝到」還是「衝到了但判定沒過」。
- [x] ~~怪一直在動、不會停~~ → 2026-09-17 改成 `StalkHold`／`StalkStep` 交替（見 §7.1）。
- [x] ~~`Faction=Werewolf` 的怪站著不動~~ → 2026-09-17 [FACTION.md](FACTION.md) §0 語意反轉（預設敵對）。
- [x] ~~衝刺時跑步動畫不會變快~~ → 2026-09-17 `MonsterAnimator` 加 `MaxMul`（預設 2.5，對既有怪零變化）。
- [ ] **撲擊命中率**：靜態模擬（用真實的碰撞框尺寸算「兩框重疊」）為 站著不動 90%／一直走動 83%／繞圈側移 80%。
      實機若覺得太黏，旋鈕依序是 `ChargeTimeSlack`(1.6) → `ChargeSpeedMul`(3.0)；
      真要留身法空間得再加「轉向速率上限」（野獸不能瞬間轉向），那是新功能、還沒做。
- [ ] **玩家全速直線逃跑時狼追不上**（Speed 3.5 vs 玩家 5）：會一直卡在 `Approach`、撲不出來。
      設計上合理（逃得掉），但如果想讓牠更纏人，要嘛提高 `Speed`，要嘛讓 `Approach` 階段也有加速。
- [ ] **撲擊傷害目前與咬擊相同**（作者 2026-09-17 選「零改動」）。要讓撲擊更痛，得給 `EnemyContactDamage` 加一個臨時倍率開關。
- [ ] **`InvincibleTimeMs` 是一份隱形的行為預算**（[PROBLEMS.md](PROBLEMS.md) **F19**）：戰狼目前填 0（與其他量產怪一致），
      被連射時擊退窗口首尾相連 ⇒ 牠會站著不做決策。實測若覺得「被打時發呆、節奏亂掉」就填 300~500 再重調節奏。

---

## 8. 射手型（弓／弩／火槍…）— `ArcherBrain` ＋ 怪物投射武器管線

> **狀態：✅ 程式完成（2026-09-17），⏳ 未編譯、未實機驗證。** 第一隻使用者：**怪物 18「Wolf Archers」狂族弩手**（BloodFang），
> 拿武器 33「狂族十字弓」。**同一次改動順便把「怪物使用投射型武器」這條路打通了**（見 §8.4），
> 不再是 §1 表格裡那句「投射型武器＝Phase 2」。

`ChaseBrain` 是貼上去磨、`PounceBrain` 是拉開距離再撲，這支的決策核心是一個判斷句：
**「我站在這裡射得到他嗎？」**——射得到就原地放箭、一步都不動；射不到才移動，而且只挪一小段就重新評估。

> ### ⭐ 射手型模組的鐵則（作者 2026-09-17 拍板，**所有射手型 Brain 都適用**）
>
> **只要 attack 動作播了出去，就一定要射出箭。**
>
> `Draw` 階段**不做任何中途取消**——不重驗射程、不重驗視線、不看武器冷卻。
> 所有該擋的檢查都在 `Observe` 決定拉弓的**那一刻**做完（射程／視線／`WeaponReady`），
> 一旦進了 `Draw`，放箭就是必然。
>
> **為什麼**：拉弓要 0.7 秒，玩家在這段時間很容易走出射程或閃到柱子後面。第一版會因此取消放箭 ⇒
> 作者實機回報「**十字弓已經提起來了卻不射箭，看起來很像 bug**」。
> **弓箭滿場飛是可接受、甚至有趣的；「舉了弩卻沒箭」不是。**
>
> 真的需要「可取消的瞄準」（例如某種會被打斷施法的敵人）⇒ **另開一支 Brain**，不要改這支。

### 8.1 四段狀態機

| 階段 | 行為 | 常數（`ArcherBrain.cs` 檔頭） |
|---|---|---|
| `Observe` | **站定評估**。剛發現目標、移動完一段、射完一發之後都回到這裡 | `ObserveMin/Max` 0.4/0.8 |
| `Reposition` | 往算好的落點**挪一小段就停**（綁距離不綁時間），走完回 `Observe` 重新評估 | `StepDistMin/Max` 1.2/2.2、`StepMaxSeconds` 2.0 |
| `Draw` | **站定拉弓**。進來時就讓 attack 動畫起播，**演到指定的那一幀才放箭**（見 §8.2b）。**進來就一定會放箭，沒有取消條件**（見上方鐵則） | `ReleaseFrame` 14、`FollowThroughSeconds` 0.8 |
| ↑ 放箭後 | 回 `Observe`，但用**較長的秒數**＝射擊間隔（作者指定先寫死在模組裡） | `IdleAfterShotMin/Max` 1.2/1.8 |

### 8.2 核心判斷：`CanShootFrom()` ＝ 距離 ＋ 視線

**這個判斷只在 `Observe` 階段做一次**（決定要不要拉弓的那一刻），連同武器冷卻（`WeaponReady`）一起——
進了 `Draw` 就不再驗（見上方鐵則）。兩個條件都成立才開火：

1. **距離**落在 `MinRange`(2.0) ~ `ShootRange`(5.25) 之間；
2. **飛行物飛得過去**（`HasLineOfFire`）：從出手點往目標掃一條**有粗細的** `CircleCast`（`LineOfSightRadius` 0.18），
   撞到障礙層就是被擋。

⚠ **`ShootRange` 是「行為射程」，不是子彈真正飛得多遠**。配方 44 是 `Speed 15 × LifeTime 3 ＝ 45 世界單位`，
而**畫面高才 10** ⇒ 直接拿子彈壽命當射程的話，牠會從畫面外你看不見的地方開始射你。
**這條對任何「遠程怪」都成立，加火槍手時不要重新發明。**
第一版抓 7（≈ 畫面寬一半），作者實測「有點太遠」⇒ **2026-09-17 砍 1/4 成 5.25**（≈ 畫面寬的 1/3）。
`PreferredRange` 要跟著按比例縮（目前 4.1 ≈ ShootRange × 0.79），否則牠想站的位置會落在射程外、一直跑位。

⚠ **視線檢查用 `CircleCast` 不用 `Linecast`**：箭有體積，那條「數學上剛好通過」的細線，實際射出去會撞在柱子邊上。
而且障礙層用 `LayerMask.GetMask("Environment", "Water")`——**與 `MonsterActuator`／`MapNavGrid` 同一份定義**，
所以「牠覺得射不過去」與「箭真的會被擋下」是同一個真相，不會出現「牠站在那裡一直不開火，你卻看不出哪裡被擋」。

⚠ **視線起點要推出自己的身體框**（`MuzzleOffset()` ＝ 碰撞框 `extents.magnitude + 0.1`）：
不推的話，`CircleCast` 的起始圓會重疊到「牠自己正貼著的那面牆」，變成**站在牆邊就永遠判定射不出去**。

### 8.2b 放箭時機＝序列圖的「那一幀」，不是一個秒數

> **2026-09-17 實測修正**：作者回報「**武器都還沒提起來，弓箭就射出來了**」。

**根因是順序反了**：第一版在 `Draw` 站定 0.55 秒後才 `TryUse()`，而 attack 動畫是
`MonsterWeaponUser` **施放成功後**才呼叫 `NotifySkillCast()` 起播的 ⇒ **箭先飛出去，動畫才開始播**。

正確順序：**進入 `Draw` 就讓動畫起播**（`BeginDraw` 呼叫 `NotifySkillCast`），
等它演到「弩已經舉到定位」那一幀才放箭。

**幀怎麼換算成秒**：`MonsterAnimator.SetState` 切到 Attack 時會把幀索引歸零、以 CSV 的 `AnimFPS` 起播，
所以第 N 幀出現在 `(N-1) ÷ AnimFPS` 秒。於是放箭時機寫成 **幀號**（`ReleaseFrame`）而不是秒數——
**改 CSV 的 `AnimFPS` 時機會自動跟著對**，換一隻拉弓節奏不同的射手只要改這個常數。

`Wolf Archers` 的 attack 25 張：1~3 預備、4~9 往前推、**10~12 完全水平前伸到位**、13~19 維持、20~25 收弩。
第一版取 11（弩剛舉定那一幀），作者實測仍覺得太早 ⇒ **現在是 `ReleaseFrame = 14`**（@14fps ＝ 0.93 秒），
落在「維持瞄準」那段的開頭，視覺上是「舉定、穩住、才放」——**放箭幀寧可比「動作到位」再晚一兩幀**，
因為玩家的眼睛要一點時間確認武器已經舉好。

⚠ **動畫要「續命」，但不能無腦每幀續**（`KeepAttackPose`）：
`NotifySkillCast()` 只把 attack 姿勢延到 `Time.time + SkillCastAnimSeconds`，而那個欄位**預設只有 0.6 秒**，
比拉弓到放箭（0.71 秒）還短 ⇒ 只呼叫一次的話動畫會在放箭前切回 idle。
但每幀無腦續的話，最後一次呼叫會把姿勢多撐 0.6 秒、拖過整套動作的結尾，動畫接著演第二輪前段
（看起來像「射完又舉一次弩」）。所以只在「再續一次也不會超過結束時間」時才續。
收弩的後半段（`FollowThroughSeconds` 0.8）與射擊間隔的 idle **重疊**，不額外拉長節奏。

### 8.3 移動只挪一小段，走完重新評估

`BeginStep()` 依「為什麼射不到」決定落點，三種情況：

| 情況 | 落點 |
|---|---|
| 太遠（> `ShootRange`） | 沿兩點連線靠近到 `PreferredRange`(5.5) |
| 太近（< `MinRange`） | 同一條線往外 ⇒ 等於**後退** |
| 距離剛好、但**視線被擋** | 保持大致距離，繞 `SideAngleMin~Max`(35~65°) 換一個能射穿的角度 |

三種都只走 1.2~2.2 單位就停。**一次走到底是錯的**：途中障礙物關係變了不會發現，而且會變成「一路衝到玩家臉上」。
`Reposition` 途中只要條件已經滿足就**立刻停下來射**，不必把這一步走完——否則會出現「明明已經走到射得到的位置，卻還要再走兩步才肯開火」。

`PreferredRange`(5.5) 比 `ShootRange`(7.0) 短是刻意的：留餘裕，玩家走兩步不會立刻脫離射程、害牠又要重新跑位。

### 8.4 怪物投射武器管線（`WeaponCastService`）— 這次真正的地基改動

原本「發射一發子彈」只存在 `PlayerController` 裡，整段綁死玩家：瞄準來自滑鼠、出手點來自血統體型、
命中層寫死 `EnemyLayer`、傷害來源寫死 `gameObject`。所以 `MonsterWeaponUser` 碰到非召喚武器只能吐 warning。

**`Assets/Scripts/Weapon/WeaponCastService.cs`** 把「發射」本身抽出來，
**誰射的／從哪射／往哪射／打得到哪一層／命中要做什麼**全部變成參數（`CastContext`）：

| | 玩家（`ShootNormal`） | 怪物（`MonsterWeaponUser.TryFireProjectile`） |
|---|---|---|
| `Origin` | `MuzzleWorldPos`（血統體型的出手點） | 身體框外緣，朝目標方向 |
| `Direction` | `AimDirectionToMouse()` | 朝 `TryUse(target)` 傳進來的目標 |
| `OwnerScale` | `PlayerScale` | 1（怪物體型不該放大牠的箭） |
| `TargetLayers` | 怪物層 | **目標所在的那一層**（射玩家＝Player 層、射敵對怪＝Enemy 層） |
| `OnHit` | `HandleBulletHit`（地面特效／子武器迸發／擊中特效） | `CombatSystem.Apply` ＋ 擊中特效 |

> **`FireNormal` 回傳「有沒有真的生出子彈」**，呼叫端要據此決定要不要進冷卻／播出手動畫。
> 無條件當成功會產生**連 warning 都沒有的空砲**（PROBLEMS **F25** 通則二）。

⭐ **`TargetLayers` 這一欄就是「怪物的箭不會被自己人擋住、也不會誤傷同伴」的全部秘密**——
用 layer 先擋掉，不要在命中 callback 裡補陣營判斷。真正的「能不能造成傷害」仍由 `CombatSystem` 查 `FactionRelations`。

**刻意留在呼叫端、沒有一起抽進來的**（它們屬於「誰在射」而不是「怎麼射」）：瞄準與出手點、耗魔／集氣／連擊／能力珠、
以及**命中之後的效果鏈**。硬把玩家的命中鏈搬進服務，會把 `TryTriggerSubWeapon`／`TryTriggerGroundEffect` 整串
玩家專屬狀態一起拖過來，服務就變成第二個 `PlayerController`。

**目前只搬了 `Normal`（直飛彈）**，所以怪物能用的投射武器也只有 Normal——但分裂／反彈／追蹤／平行／穿透／軌跡
這些配方欄位對怪物**全部有效**，因為那是同一份彈道程式。
Laser／Parabolic／SkyStrike／Chain／Orbital／Melee／Dash／GroundCast 仍住在 `PlayerController`；
之後一種一種搬進來，**搬完 `MonsterWeaponUser` 不用改就會自動支援**（它是照 `recipe.Mode` 分派的）。

> 玩家端同時做了三個薄封裝：`ParallelOffsets`／`ResolvePierceableLayers`／`ResolveNonBounceLayers` 的實作都搬進服務，
> `PlayerController` 只留一行轉呼叫（拋物線等其他模式還在用這些名字，不能直接刪）。
> **所以平行彈與穿透／反彈的規則現在只有一份**，改一次玩家與怪物同時生效。

### 8.5 加一隻新的射手型怪（SOP）

`BrainType` 填 `Archer` 就能用，`ArcherBrain` 裡沒有任何「弩手專屬」的東西。

| # | 做什麼 | 備註 |
|---|---|---|
| 1 | 素材丟 `Monsters/SequenceImage/<怪名>/idle｜walk｜attack` | `attack` ＝ 拉弓到放箭那一整套 |
| 2 | 武器：`WeaponTable` 加一列（或沿用現成的），`Mode` 必須是 **Normal** | 背包 icon 與飛出去的圖是**兩個欄位**，見 [PROBLEMS.md](PROBLEMS.md) **E38** 上面那段與 PROGRESS 2026-09-17 那條 |
| 3 | `MonsterData.csv` 加一列：`BrainType=Archer`、**`Weapon` 填武器 ID（數字，不是 `Contact`）** | 填 `Contact` 的話牠不會有 `MonsterWeaponUser`，就只會站著發呆 |
| 4 | ⚠ 子彈圖的 `SpriteAngleOffset` **不要填 0** | 0 ＝「這顆子彈不旋轉」，箭會永遠朝右。水平朝右的圖填 **360**。見 PROBLEMS **E38** |
| 5 | 跑 `Project Tools → 角色 → 計算影子錨點`；四足獸型還要手改成 `manual` | 見 [SHADOW.md](SHADOW.md)〈四足獸型的常見誤判〉 |
| 6 | `Project Tools → Sync Map Assets` | 把圖與 CSV 帶進 StreamingAssets |

**完全不用管的**：傷害結算、attack 動畫、左右翻面（自動面向玩家）、影子、腳底對齊、彈道行為。

⚠ **寫新的射手型 Brain 時，鐵則照抄**：檢查全部放在「決定出手」的那一刻，出手動作開始之後不准反悔。
這條看起來像小事，但它決定了玩家看到的是「一隻會射箭的怪」還是「一隻卡住的怪」。

### 8.6 待辦 / 已知限制

- [x] ~~舉了弩卻不射箭~~ → 2026-09-17 第二輪：移除 `Draw` 的所有中途取消，檢查前移到 `Observe`（見上方鐵則）。
- [ ] **2026-09-17 實測已修三項**（射程太遠 → 砍 1/4；箭比動畫早出去 → 改幀對齊；舉了弩不射 → 鐵則）。
      下一輪看：① 站位會不會在柱子附近來回抖（`SideAngleMin/Max`）；② 射擊間隔 1.2~1.8 秒會不會太密；
      ③ `ReleaseFrame`（目前 14）的放箭時機順不順（覺得早就往後挪、晚就往前挪，一幀 ≈ 0.07 秒）。
- [x] ~~每隻新生成的弩手第一發是空砲~~ → 2026-09-17 第三輪：`MonsterWeaponUser.Ready` 改成會先把武器解析完
      （起手緩衝是解析時才寫進冷卻的，事前問 `Ready` 會拿到假答案）。**根因與兩條通則見 [PROBLEMS.md](PROBLEMS.md) F25**。
      同時 `Observe` 遇到「射得到但武器冷卻中」改成**原地等** `WeaponWaitSeconds`(0.15)，不跑開——
      牠明明站在射得到的位置，跑開只會看起來很蠢。
- 🔧 **若 Console 出現 `[Archer] … 播了 attack 卻沒射出東西`**：那是鐵則被破壞的警報。
      F25 修掉之後，剩下的原因都是**設定問題**（`Weapon` 欄沒填武器 ID／該武器的 `Mode` 不是 `Normal`／
      場景 `WeaponManager` 的 Bullet Prefab 沒設／配方沒建好），不是手感問題。
- 🔧 **手感不對先開除錯 log**：`ArcherBrain.DebugLog = true`（static），會印每次決策「能不能射／為什麼不能／挪去哪」。
- [x] ~~箭比動畫早射出去~~／~~attack 動畫被 0.6 秒的 `SkillCastAnimSeconds` 切掉~~
      → 2026-09-17 改成「動畫先起播、第 `ReleaseFrame` 幀放箭」＋`KeepAttackPose` 續命（見 §8.2b）。
      **沒有改 `SkillCastAnimSeconds`**（那是全怪共用的 public 欄位），而是由 Brain 自己續，其他怪零影響。
- [ ] **兩層射擊節流**：`ArcherBrain` 的 `IdleAfterShot`（行為）與武器配方的 `FireInterval`（武器）**是兩層**，
      實際間隔取較嚴格的那個。想射快一點先改 Brain 的常數，不要去動配方——配方是玩家也在用的那把武器。
- [ ] **射手死掉時，牠已經射出去的箭不再造成傷害**（箭會繼續飛完，只是不結算）。
      這是修 [PROBLEMS.md](PROBLEMS.md) **F26**（清場時 Console 被 MissingReferenceException 洗版）時的取捨——
      `CombatSystem.Apply` 需要 source `GameObject` 查陣營，而射手已經被 Destroy。
      要讓「死人的箭照樣殺人」得在發射當下就把陣營快照起來，目前沒做。
- [ ] **`MinRange`(2.0) 的後退是「挪一步」不是「逃跑」**：玩家一路貼上來，牠會邊退邊被貼身。
      要做成真正的風箏（kiting）得加「後退時也能射」的狀態，那是新功能。
- [ ] **手感參數全域共用**：`ArcherBrain` 檔頭的 `const` 所有射手型怪吃同一組。
      之後若出現「短弓手 vs 長弓手」射程要分開，把 `ShootRange` 改成從 `MonsterData` 讀（同 §7.6 對 `StalkRange` 的說明）。

## 9. 跳躍踐踏型（狂族皇家衛士…）— `LeapSlamBrain` ＋ 程序化裂地

> **狀態：✅ 程式完成（2026-09-18），⏳ 未編譯、未實機驗證。** 第一隻使用者：**怪物 19「Wolf Royal Guard」狂族皇家衛士**（BloodFang）。

**一記下馬威，然後變成普通近戰。** 牠一發現目標就躍過去、落地踩出一圈裂地 AOE，
之後**永遠**改用 `ChaseBrain` 貼上去打。所以這支 Brain ＝「一次跳躍 ＋ 既有追擊」的合成，
`BrainType` 填 `LeapSlam`。

| 階段 | 行為 |
|---|---|
| `Approach` | 目標超出 `LeapRangeMax`(5.0) → 正常速度走近 |
| `Leap` | **一生只跳這一次**：蓄力 → 騰空 → 落地（裂痕＋傷害）→ 收招 |
| `Chase` | 之後每一幀委派 `ChaseBrain`，本 Brain 不再介入 |

### 9.1 ⚠ 素材前提：先量幀，別假設「一個資料夾＝一次動作」

**狂族皇家衛士的 `jump/` 有 25 張，但那是「跳兩次」**——1~13 一輪、14~25 又一輪。
整組播完＝連跳兩下、踩兩次，不是作者要的。所以 `TotalFrames = 13`，只播第一輪。

怎麼確認的（換一隻怪時照做一次，比用眼睛看快也準）：量每一幀**不透明像素的底邊 y**，
底邊上升＝離地。狂族皇家衛士的第一輪：

| 幀 | 1~4 | 5 | 6~9 | 10 | 11 | 12~13 |
|---|---|---|---|---|---|---|
| 底邊 y（像素，越小越高） | 226~227 | 218 | 182→163→173 | 221 | 226 | 227 |
| 意義 | 蓄力下蹲（在地） | **離地** | 騰空（最高在 8） | 下墜 | **觸地** | 落地緩衝 |

一行 python（PIL 讀 alpha 的 `getbbox()`）就印得出來，見 PROGRESS 2026-09-18 那條。

### 9.2 幀號即事件（不要寫秒數）

`TakeoffFrame = 5`、`LandFrame = 11`、`EndFrame = 13`，都是 **1-based ＝資料夾裡的檔名編號**。
Brain 把它們除以 `AnimFPS × JumpFpsMul` 換成秒，所以**改 CSV 的 `AnimFPS`，整段跳躍的節奏與落地時機會
自動跟著對**（同 §8.2b 的射手放箭幀）。狂族皇家衛士 `AnimFPS = 13`、`JumpFpsMul = 0.65` ⇒ 實際 8.45fps：

```
0.00 ─ 0.47s  蓄力（幀1-4）    ＝ 玩家的預告窗口，看到蹲下就該閃
0.47 ─ 1.18s  騰空（幀5-11）   ＝ 0.71 秒升到 5.85 單位再砸下來
1.18 ─ 1.42s  落地緩衝（11-13）
1.42 ─ 2.18s  **定格在最後一幀**（LandHoldSeconds 從落地算 1.0 秒）＝ 玩家的輸出窗口
2.18s ─       交棒給 MeleeChaseBrain（§10）
```

⭐ **落地後定格 1 秒（`LandHoldSeconds`）就是這一招的「重量」**：作者 2026-09-18 回報
「跳的高度 ok 了，但**沒有那種壓迫感**」——高度和速度都到位之後，缺的是**砸下來之後的停頓**。
砸下來 → 定格 → 地裂在腳下一路竄開（`GroundCrackFx.CrackSeconds` 0.85s，剛好在這一秒裡竄完）→ 才起身追人。
one-shot 天生會停在結束幀，所以「定格」不必另外做，只要**延後交棒**即可。

⚠ **落地那一刻就要把擊退與內插還給系統**（不是等定格結束）：這一秒是玩家的輸出窗口，
牠該會被打退、該有受擊回饋。所以 `SuppressKnockbackInterrupt = false`、`rb.interpolation` 還原、
位置不再被壓制，都寫在「落地結算」那一格裡，不在 `FinishLeap`。

⚠ `JumpFpsMul`(0.65) 是**整段**的倍率。跳三個身高需要時間演——1.0 之下騰空只有 0.46 秒，
要在裡面升降 5.85 單位 ⇒ 垂直速度 25 單位/秒（玩家才 5），畫面上像被彈射。
想要「蓄力不變、只有騰空變慢」得做分段 fps（`PlayOneShot` 目前只吃一個倍率），還沒做。

⭐ **`LeapRangeMax` 就是跳躍的速度上限**：騰空秒數由幀固定，距離越遠＝飛越快。
5.0 ÷ 0.46 ≈ **10.9 單位/秒**，剛好和戰狼衝刺（Speed 3.5 × 3）同級；放到 7 就變 15 單位/秒，
畫面上讀起來是瞬移不是跳躍。**要跳更遠，得同時把 `AnimFPS` 調慢**（騰空變久）。

**覺得閃不掉** → `AnimFPS` 從 13 調到 10（蓄力 0.40 秒、騰空 0.60 秒，預告更明顯，但整隻怪的走路/攻擊也會一起變慢）。

### 9.3 ⭐⭐ 鐵則：蹲下去了就一定要跳出去

**作者 2026-09-18 拍板，與 §8 射手的「動作播了就一定要射」同一條**。
`BeginLeap` 一旦呼叫，**沒有任何中途取消**——不重驗距離、不重驗視線、不看目標還在不在。
玩家看到怪蹲下去蓄力、結果牠站在原地不動，讀起來就是 bug，不是「牠判斷了一下決定不跳」。

第一版沒做到，作者實機回報「**常常會準備跳但沒跳出**」。**兩個來源，都不在取消邏輯裡**：

1. **擊退整段跳過 `Think()`**（**F19**）。射手不怕這個——牠只是「等時間到再射一次」，被跳過幾幀無所謂；
   但跳躍的位移是**每幀算出來的**，Think 被跳過那幾幀就完全沒有位移。玩家連射時擊退窗口首尾相連，
   整段騰空 0.46 秒會被吃光 ⇒ 蹲下、動畫演完、人在原地。
   修法：`MonsterController.SuppressKnockbackInterrupt`（Brain 在起跳時開、落地關），期間 `Think()` 照跑。
   ⭐ **通則：任何「每幀自己算位移」的演出都要想一次「被擊退跳過 Think 會怎樣」**——
   §7 的撲擊沒事是因為它用 `act.MoveTowards` 讓 actuator 出力，擊退期間 velocity 還在；
   自己寫 transform 的就會整段消失。
2. **落點被夾回起跳點 ⇒ 原地跳**。第一版的 `ClampLanding` 只沿直線往回退，退到 0.2 都不通就回傳起跳點。
   而**玩家站的格子對尋徑格來說常常是不可走的**（家具膨脹了一圈），所以這比想像中常發生。

**現在的落點規則**（`ResolveLanding`）：

| 情況 | 落點 |
|---|---|
| 目標在射程內、直線可達 | 目標**起跳當下**的位置 |
| 目標超出 `LeapRangeMax` | 沿連線取射程邊緣＝**射程內離目標最近的點** |
| 目標貼臉（< `MinLeapDist` 1.2） | 往目標方向踏一步，不原地跳 |
| 直線被擋 | 以該方向為中線往兩側散開試 **0°/±18°/±36°/±54°/±72°**，每個角度由遠而近找第一個跳得到的距離，最後取**離目標最近**的候選 |
| 一個候選都沒有（被封在牆縫裡） | 還是跳想要的那個點——**寧可跳出去，也不要蹲著不動** |

**落點仍然鎖定「起跳當下」、刻意不追蹤**：蓄力那 4 幀就是預告，看到就該側身閃開。
改成像 §7 的衝刺那樣全程追蹤＝**出手必中**，走位就沒有意義了。
撲擊型可以追蹤，是因為牠一輪一輪不停地撲；**這一記是一次性的，閃掉就該真的閃掉**。

可達性走 `MapNavGrid.HasLineOfSight`——和 A* 同一份障礙圖（含家具膨脹），與 actuator 的判定一致。
**牆的另一側不該因為會跳就跳得過去**，那會破壞地圖的動線設計。

### 9.4 傷害寫在哪：`ImpactDamageArea` ＝ 短命 trigger ＋ 既有接觸傷害

`Scripts/Combat/ImpactDamageArea.cs`（**通用的一次性傷害圈**，§10 的揮擊命中幀也用同一支）。
落地時開一個半徑 `LeapRadius` 的圓形 trigger，掛
`EnemyContactDamage`（重擊間隔給 99 秒 ⇒ 同一目標只吃一次），活 0.12 秒後自毀。
**零新傷害程式**：誰能傷誰查 `FactionRelations`（狼人踩到吸血鬼也會痛、踩到同族不會）、
對玩家走玩家自己的無敵幀、傷害統一進 `CombatSystem`。這是榕樹妖地刺（§6）用很久的同一套。

> ⚠ **實際殺傷範圍 ＝ `LeapRadius` ＋ 目標碰撞框的半徑**（`EnemyContactDamage` 走
> `Physics2D.Distance` 的**邊緣距離**，見 §7.3 第 8 點）。玩家的 `CircleCollider2D` 半徑約 0.5，
> 所以 `LeapRadius = 1.6` 實際上打得到 **2.1** 單位外的玩家。調數值時記得這一條，別以為填多少就是多少。

**刻意不放 Enemy 層**（地刺放是因為它要能被玩家打壞）：這東西是一瞬間的判定、不是實體，
放進 Enemy 層會被玩家的子彈與搜敵邏輯當成目標，平白吃掉一發子彈、連鎖閃電還會往它跳。
留預設層即可——`EnemyContactDamage` 本來就不看 layer。

**為什麼不接 `GroundEffectTable`**：那套的傷害遮罩是「怪物＋可破壞地上物」，是給**玩家武器**用的、
打不到玩家；為了讓怪用而去改它，會動到所有玩家武器共用的傷害路徑。不值得。

### 9.5 裂痕是 shader，不是素材

`Resources/Shaders/GroundCrack.shader` ＋ `Scripts/Map/GroundCrackFx.cs`，一行就生：
`GroundCrackFx.Spawn(落點, 半徑, 怪的SpriteRenderer)`，之後自己跑完「裂開→停留→淡出→銷毀」。

⚠ **位置要傳腳底（`MonsterController.FeetWorldPos`），不是 `transform.position`**：
route B 怪物的 pivot 是**畫布中心**，直接用 transform 會讓裂痕浮在半身高的位置——
作者 2026-09-18 回報「龜裂在怪物胸口播放而不是腳底」，根因就是這個。
狂族皇家衛士實測腳底在 transform 下方 **1.27** 世界單位（可見高 2.54）。詳見 [PROBLEMS.md](PROBLEMS.md) **G13**。
踐踏的傷害圈也一起對準腳底，這樣「看到的裂痕範圍」就是「打得到的範圍」。

**為什麼用 shader 不用序列圖**：裂痕要隨機、每次不一樣、而且大小得跟殺傷半徑對得起來。
序列圖的話每種半徑都要一組、每次落地還長得一模一樣；shader 換一個 `_Seed` 就是全新的裂紋，
半徑由物件 scale 決定、永遠和 AOE 對齊。

畫法：**jittered grid Voronoi**（2026-09-18 依作者的參考圖改寫）。把 uv 放大成格子、每格塞一個隨機
特徵點，對每個像素找最近(F1)與次近(F2)——**F2−F1 接近 0 的地方就是兩塊泥板的交界**，那就是裂縫。
再疊一層格子更密、更細更淡的二級裂（真實泥地大塊裡面還會有小裂）。
三塊交會的頂點處 F2−F1 天然會寬一點，剛好就是泥裂那種 Y 型節點，不必另外做。

> ⚠ **第一版是「中心碎坑 ＋ N 條主裂往外竄 ＋ 分支」**，作者附了乾裂泥地的照片回報
> 「我想要的是**平均**的那種龜裂，不是你做的這種**中間有個洞**的」。
> 那是玻璃／衝擊波的裂法；乾裂泥地是**整片均勻的不規則多邊形**（泥塊各自收縮拉開），
> 沒有中心、沒有方向性。**兩者在演算法上沒有交集，是整支重寫不是調參數。**

`_Progress` 控制「裂到多遠」，所以落地瞬間是一個點、然後一塊塊往外蔓延——
這是唯一有「中心」的東西，而且只在 0.85 秒的演出期間看得到，裂完就是一整片均勻的龜裂。

⚠ **邊界要以「泥塊」為單位抖動**（`rEdge = r + hash(cellId) × 0.22`），不能拿一個正圓去淡出：
裂縫線本來就會穿過圓周，每條被切斷的裂縫都會留下一個漸層的三角形殘影 ⇒ **整圈長出尖刺，像海膽**。
讓整塊泥板一起進、一起出，邊界就是自然的鋸齒狀，擴散時也變成「一塊一塊裂開」而不是一圈平滑的環。

⚠ **`_RimAmount`(0.3) 不要拉高**：翻土的亮邊在這種密集的 Voronoi 上會讓每一條縫都鑲一圈亮邊，
整片看起來在發光。第一版是 0.9（那時只有 7 條主裂，撐得住）。

| 調什麼 | 改哪 |
|---|---|
| 裂痕大小（**只有視覺**） | `LeapSlamBrain.CrackRadiusMul`（**1.47**）。裂痕視覺＝殺傷半徑 × 它——**打不到的邊緣也該裂**，看起來才有份量。演進：1.35（太小）→ 2.2（太大）→ 1.47。`LeapRadius=1.6` 之下 ⇒ 畫面上是 4.7 × 2.35 的橢圓（角色高約 2.5） |
| 裂痕大小（**連傷害一起**） | `MonsterData.csv` 的 `LeapRadius`。視覺會跟著等比變，因為它是乘上去的 |
| 裂多快／留多久／淡多久 | `GroundCrackFx.cs` 檔頭四個常數：裂開 **0.06s**／塵浪 0.32s／停留 **9.0s**／淡出 **3.5s**。<br>⭐ 裂開時間演進：0.22 → 0.85（作者要「慢慢延伸出去」）→ **0.06**（看了之後決定「換回猛烈的一次全部出現」）。0.06 ≈ 4 幀，實質瞬間但留一點爆發；要「第一幀就完整」就填 0。<br>⚠ 與落地定格那 1 秒是兩件事：定格仍在（壓迫感來自停頓），只是裂痕不再用那一秒慢慢長（共約 12.7 秒；3.2＋1.6 作者回報太短）。⚠ 再拉長要留意 overdraw——裂痕是每幀跑 fragment shader 的半透明大 quad，目前一隻怪一生只跳一次所以無所謂，哪天做成可重複跳要回來看 |
| 泥塊大小 | `GroundCrackFx` 生成時隨機給的 `_CellScale`（9~12 ＝「整個圓的直徑上有幾塊」）。直徑 7 單位 ÷ 10 ⇒ 一塊約 0.7 單位。想更碎就調大 |
| 裂縫粗細、形狀、顏色 | shader 的 Properties：`_CrackWidth`(0.045)／`_Jitter`(0.85，0＝規則蜂巢 1＝很亂)／`_FineScale`·`_FineAmount`（二級細裂）／`_PlateShade`(0.10，泥塊面的明暗)／`_RimAmount`(0.3)／`_CrackColor`·`_RimColor` |
| 壓扁程度（俯視透視） | `GroundCrackFx.FlatRatio` 0.5（＝與 `BlobShadow.HeightRatio` 同一套透視） |

⚠ 用 **alpha 混合**不是加色：裂痕是吃光的暗痕，要能實心遮住地板。加色（`Blend One One`）
永遠做不出「不透明」，只會讓地板變亮——同 [GROUND_EFFECT.md](GROUND_EFFECT.md) 背景符號層踩過的結論。
⚠ Linear 色彩空間下疊色比直覺重（[PROBLEMS.md](PROBLEMS.md) **E11**），
**要更明顯優先加寬 `_CrackWidth`，不要加 alpha**。
⚠ shader 有 `#pragma target 3.0`（fragment 內有動態迴圈，SM2.5 編不過）。

排序固定 `8`，與地面特效同一階——蓋在可走的石板/地毯上，但不會蓋住柱子與角色。

### 9.6 騰空怎麼做的：`IAirborneVisual`（這次唯一動到共用元件的地方）

俯視角的 Y 軸同時是「畫面高度」與「地面深度」，要看起來跳起來只能把 `transform.position.y` 往上推——
但所有讀 transform 的東西都會以為牠「往畫面上方走了一步」：**影子跟著飛到半空中**、
**Y 排序基準上移 ⇒ 跳到空中時被其實在牠後面的地上物蓋住**。

所以新增介面 `IAirborneVisual { float AirborneHeight }`（`Scripts/IAirborneVisual.cs`）：
`MonsterController` 實作它，`LeapSlamBrain` 騰空期間每幀寫入抬高量、落地歸 0；
`BlobShadow` 與 `YSortByFeet` 各加三行把那段高度扣回地面（影子留地上，並隨高度**縮小變淡**）。

> **沒有實作這個介面的角色行為完全不變**（兩邊都是「取不到就當 0」，而且高度 0 時直接 early return、
> 連 renderer 都不碰），玩家與既有怪一個像素都不會動。

抬高量：**以「角色身高」為單位，不是寫死的世界單位**——`HopHeightInBodies`(**3.0**) × 身高
（`CharacterWorldHeight` 1.95 × 這隻怪的 Scale）⇒ 狂族皇家衛士跳 **5.85 單位，等於三個牠疊起來**。
作者要的是「跳到主角頭上三個怪物的高度」，那是相對身高的描述；寫死的話換一隻體型不同的怪就得重調。
演進：0.55（第一版「跳得不夠高」）→ 1.5（第二版還是太矮）→ 3 個身高。

⭐ **兩條曲線，各管一件事**：

| 曲線 | 做什麼 |
|---|---|
| `HopCurve`（垂直） | 頂點前走 sin 的前四分之一（越接近頂點越慢＝**滯空**），頂點後走 `1−v²`（**越掉越快**）。上升平均 16.5 單位/秒、**下降末速 31.3**（最後一瞬掉 1.11 單位） |
| `HorizCurve`（水平） | **到頂點就走完 85%**（`HorizAtPeak`），剩下 15% 留給整個下降段 ⇒ 落下那一半幾乎**垂直** |

第一版是對稱拋物線 `4t(1−t)` ＋ 水平等速，讀起來是「飄過去」；第二版改了垂直曲線，作者仍要求
「跳更高、再**垂直**快速落下」——關鍵其實是**水平**那條：不把水平位移擠到上升段，再高的跳也是斜斜飛過去。
跳滿 5 單位時：上升段水平 12.0 單位/秒、下降段只剩 **2.1**。
（頂點處水平速度會有明顯落差，那正是「滯空一下再砸下來」的感覺，不是 bug。）

⚠ **頂點要對齊素材最高的那一幀**（`PeakFrame = 8`，量出來的）。不對齊的話會出現
「圖已經在下墜、程式還在把牠往上抬」的橡皮筋感——**程式加的弧線是在替素材加強，不是自己演一套**。

⚠ **影子的衰減常數跟著改了**（`BlobShadow.AirShrinkPerUnit` 0.55→0.18、`AirFadePerUnit` 0.45→0.10）：
影子留在地面、沿途滑向落點，是玩家判斷「牠要砸哪裡」的**唯一線索**。
照「跳 1.5 單位」訂的舊值，在 5.85 單位高時會把影子縮到 0.24 倍、透明度剩 0.28 ⇒
最需要看到落點預告的時候反而看不見。新值在同樣高度是 0.49 倍 / 0.63——看得出離地，但一直看得見。

⚠ **跳 5.85 單位可能超出攝影機**（正交 size 常見 5~6 ⇒ 畫面高 10~12 單位）。
實測若怪跳出畫面上緣，把 `HopHeightInBodies` 調到 2.0~2.5。

⚠ **騰空期間要暫時關掉 `Rigidbody2D.interpolation`**：開著的話 rb 會用「上一個物理步的位置」往回補畫面，
而我們每幀直接改 transform ⇒ 兩邊互相拉扯，騰空看起來會抖。落地還原（存在 `_savedInterp`）。

⚠ **這是純視覺的高度**：碰撞、傷害、尋徑全部照 transform 走，不因為「跳起來」而改變。

### 9.6b 魄力是怎麼堆出來的（落地定格 ＋ 下墜殘影）

> 作者 2026-09-18：「還是感受不到跳躍踐踏的魄力，有可能在角色旁邊加上速度線一類的嗎？」

⭐ **高度與速度到位之後，剩下的魄力幾乎不在角色身上，而在「畫面有沒有反應」。**
這一招原本的問題是：砸到地的那一刻，除了地上多一圈裂痕，**整個畫面什麼事都沒發生**。

目前做了三層：

| 層 | 做什麼 | 在哪 |
|---|---|---|
| **落地定格**（hit stop） | 砸到地那一瞬間把 `Time.timeScale` 壓到 0.05、**0.06 秒**再彈回 | `HitStopSeconds`；機制在 `Scripts/Combat/HitStop.cs` |
| **下墜殘影** | 下降段每 0.035 秒留一個半透明剪影、0.22 秒淡出 ⇒ 一條幾乎垂直的殘影柱 | `Afterimage*` 四個常數；機制在 `Scripts/Afterimage.cs` |
| **落地揚塵** | VfxTable **41「踐踏揚塵」**，播一次（`Loop=0`） | `LandDustVfxId` / `LandDustLiftY` |

**揚塵的素材**（作者 2026-09-18 從特效預覽器挑的）：`Super Pixel Effects Pack 1 / fx1_impact_dust`（brown, 13 幀），
複製進 `Resources/VfxEffects/ImpactDust/`（檔名改成 `ImpactDust_01`~`_13`，因為 `VfxManager` 組路徑是 `{AniPath}_{序號:D2}`，**兩位數**，原檔是三位數）。

⭐ **為什麼這張特別適合踐踏**（量過每一幀才確認的）：畫布 **104×24** 的極寬扁橫幅、塵土從中央往左右推開，
而且**前 8 幀的不透明像素底邊都貼齊畫布底** ⇒ 它是「**沿著地面擴散**」而不是「往上噴的雲團」，
正是俯視角落地該有的樣子。不透明佔比 45% → 0.2%，淡出很乾淨，適合播一次就收。

⚠ **素材的錨點在畫布中心，但內容貼著畫布底** ⇒ 直接放在腳底的話塵土會有一半沉到地下。
所以往上補 `LandDustLiftY`(0.38 ＝ 24px ÷ 2 ÷ PPU 100 × Scale 3.2)。
**這個數字是「素材怎麼畫」決定的，不是手感**，所以寫死在 Brain 裡而不是開成 CSV 欄。

⚠ **`SortingOrder` 留空**（⇒ `VfxManager` 全域 22000）＝**蓋在角色前面**，塵土會遮住腳。
這比畫在腳下（像泰坦踏地那兩個填 8）衝擊感更強——想改成腳下就在 VfxTable 那一列填 8。

⚠ **新複製進 Resources 的 PNG 要有正確的匯入設定**：我已經照既有 pixel 特效的 `.meta` 產了 13 份
（`textureType: 8` ＝ Sprite、`filterMode: 0` ＝ **Point**、`spritePixelsToUnits: 100`）。
少了這些，`Resources.Load<Sprite>` 會直接載不到（Texture Type 不是 Sprite），
或是 pixel art 被 Bilinear 濾成一團糊（放大 3.2 倍時特別明顯）。

**定格是這兩者裡效果最大的**：同樣的動畫，加了那 0.06 秒就會從「碰到地面」變成「砸到地面」。

⚠ **定格不能用協程**：會呼叫它的都是「打到人的那個東西」（怪、傷害圈），**很可能在定格結束前就被銷毀**
（怪被反殺、`ImpactDamageArea` 0.12 秒自毀）⇒ 協程中斷 ⇒ `timeScale` 永遠卡在 0.05，整個遊戲變慢動作。
所以 `HitStop` 用一個 `DontDestroyOnLoad` 的常駐載體以 `unscaledDeltaTime` 倒數，與呼叫者的生死無關。

⚠ **還原時只還原「自己設的那個值」**：專案用 `timeScale = 0` 當暫停（`UIManager` 開面板），
定格期間玩家開了背包還無條件寫回 1 的話，**會把暫停解除掉**。被別人改過就放手。

### 9.6c 為什麼是殘影不是速度線

作者問的是速度線，我建議換成殘影，理由兩條：
① 俯視角 2D 的角色是「往畫面下方掉」，放射狀速度線得畫成角色上方的拖尾，可讀性遠不如橫向捲軸或 3D；
② 速度線是偏卡通的語彙，跟本專案陰暗寫實的美術會打架。
**殘影傳達同一件事，而且它就是角色自己的剪影，風格上永遠不會出戲。**

殘影**只在下降段留**（`AfterimageFromT` 0.42，頂點在 0.5）：上升是「躍起」、下降才是「砸落」，
全程都留會變成一條沒有重點的長尾巴。

### 9.6d 還沒做、但隨時可以加的兩層

作者這次沒選，機制都是現成的，要加各只有一兩行：

- **鏡頭震動**：`MapCameraController.AddShake(0.35f, 0.25f)`（API 已存在，血統變身在用）。
  「砸下來」最強的訊號——玩家的身體感覺來自畫面本身動了。
- **踏地特效**：`VfxManager.Spawn(34, 腳底)`＋`Spawn(35, 腳底)`
  ＝ VfxTable 的**泰坦・踏地衝擊環**與**踏地揚塵**，現成素材，而且 `SortingOrder` 已經是 8（與裂痕同層、畫在腳下）。
  另外 8「沙土爆炸」也可以疊。
- （再往下還有角色擠壓 squash & stretch、落地把玩家往外推，成本更高一點。）

### 9.7 動畫走 one-shot（`MonsterAnimator.PlayOneShot`）

既有播放一律是**循環**，而跳躍是有頭有尾的動作；更關鍵的是 `HandleVisuals` **每幀**都會依距離/位移
呼叫 `SetState` 覆寫狀態——Brain 光是 `SetState(Jump)` 撐不過下一幀。
所以 `MonsterAnimator` 加了 one-shot：`PlayOneShot(state, 起幀, 迄幀, fps倍率)` 播一次就停在迄幀，
**期間完全忽略外部 `SetState`**，要交還控制權得明確 `CancelOneShot()`（所有權清楚，不會互搶）。

`Jump` 是第五個動作狀態，照 [MONSTER_SETUP.md](MONSTER_SETUP.md) 說的固定五處擴充。
**沒有 jump 圖的怪 `PlayOneShot` 回 false ⇒ Brain 直接跳到 `Chase`**——不硬演。
（這比 `SetState` 的「自動退回走路」好：退回走路只會變成「滑過去然後莫名其妙炸一下」。）

⚠ **jump 的顯示倍率欄 `JumpScale` 留空時「沿用 idle」，不是「走自動對齊」**（2026-09-18 加這一欄時
保留的設計）：自動那套是「把這個動作的可見高度拉成跟 idle 一樣」，而跳躍的可見高度**本來就是動作的內容**。
對它正規化＝把跳躍最重要的那段身體變化整個抵銷，而且越蜷縮的幀被放得越大 ⇒ 騰空時怪會忽然膨脹一圈。
狂族皇家衛士實測：jump 的可見高在 152~190px 之間跳動、idle 是 199 ⇒ 自動對齊會逐幀放大 1.05~1.31 倍。
所以留空時 `jump` 沿用 **idle 的 tileSize**；**有填就完全照填的走**（作者自己看畫面決定，永遠比演算法準）。
同機制的坑見 [PROBLEMS.md](PROBLEMS.md) **G12**。

### 9.8 從 §7 撲擊型繼承過來、預先避開的坑

寫這支時逐條比對過 §7.3，以下是對應處理（**不要改回去**）：

1. **收尾檢查放 `Think()` 最前面**：怪被打時的擊退窗口會整段跳過 `Think()`（**F19**）。
   跳到一半被打斷時，窗口一結束要第一時間把落地結算掉，否則牠會**停在半空**、裂痕與傷害也不會出現。
   `FinishLeap` 有「還沒炸過就補炸一次」的保底。
2. **位置用「經過時間算出的絕對插值」，不是逐幀累加**：所以被跳過幾幀也不會偏，回來自動對位。
3. **距離不綁時間**（**F20**）：跳躍距離由落點決定、時間由動畫幀決定，CSV 的 `Speed` 調小只影響走近的速度，
   不會讓跳躍縮成「原地抽動」。
4. **傷害判定與 Brain 讀同一份幾何**（**F22**）：傷害全交給 `EnemyContactDamage`，沒有自己寫的中心距離判定。
5. **不必擔心「突襲感是動畫給的」**（§7.3 第 7 點）：跳躍動畫是 one-shot、走固定幀率，
   不吃走路 fps 連動，節奏不受移動速度影響。
6. **（2026-09-18 補）擊退不只會跳過決策，還會吃掉位移**：§7 的撲擊只要在窗口結束時還原速度就好，
   因為它的位移是 actuator 出的力；而這支每幀自己寫 transform ⇒ 被跳過的幀等於**沒有發生**。
   所以跳躍期間必須 `SuppressKnockbackInterrupt = true`，而且 `TickLeap` 每幀把 `rb.velocity` 歸零
   （＝連擊退的位移也一併壓制，跳躍全程免疫）。⚠ **這個旗標一定要在收尾還回去**，
   否則這隻怪從此不會再被擊退打斷任何決策。

### 9.9 加一隻新的跳躍踐踏型（SOP）

| # | 做什麼 | 備註 |
|---|---|---|
| 1 | 素材丟 `Monsters/SequenceImage/<怪名>/idle｜walk｜attack｜**jump**` | `jump` 是新動作；`dead` 不需要 |
| 2 | ⚠ **量 jump 的幀**，改 `LeapSlamBrain` 的 `TakeoffFrame`／`LandFrame`／`EndFrame`／`TotalFrames` | 見 §9.1。**這四個目前是全域共用的 const**，第二隻怪的幀序不同時就得改成從 CSV 讀（還沒做，因為目前只有一隻） |
| 3 | `MonsterData.csv` 加一列 | `BrainType=LeapSlam`、`Weapon=Contact`；`AnimFPS` **依 jump 張數算**（張數÷fps＝一輪秒數）；`LeapDamage`／`LeapRadius` 留空就是「接觸傷害×2、半徑 1.6」 |
| 4 | ⚠ 跑 `Project Tools → 角色 → 計算影子錨點` | 會**多算一組 jump**（工具與同步管線都是通用字串，不必改）。騰空幀的腳在畫布裡位置差很多，必要時手改該列並把 `Source` 改 `manual`，見 [SHADOW.md](SHADOW.md) |
| 5 | `Project Tools → Sync Map Assets` | 把 jump 的圖與 CSV 帶進 StreamingAssets |

**完全不用管的**：腳底對齊、怪物體光、影子掛載、傷害結算、碰撞框貼合、跳躍期間的排序與影子補正。

### 9.10 待辦 / 已知限制

- [ ] **未編譯、未實機驗證**。第一次實測看：**跳 5.85 單位會不會超出攝影機上緣**（會就把
      `HopHeightInBodies` 調到 2.0~2.5）、整段 1.42 秒會不會太拖、裂痕的粗細與顏色在實際地板上讀不讀得出來。
- [ ] **跳躍中仍會依目標翻面**（`HandleVisuals` 的 flipX 每幀跑）：玩家若在 0.46 秒的騰空中繞到另一側，
      怪會在空中轉身。要修就是在 one-shot 期間鎖 flipX，但那又會讓「跳完面向錯誤」，先觀察再說。
- [ ] **⚠ 騰空中的接觸傷害還開著**（尚未拍板，2026-09-18 提出）：怪的碰撞框跟著抬高 1.5，但框高約 1.9，
      所以**從玩家頭上飛過去時仍可能觸發 `EnemyContactDamage`**。這會稀釋「閃開落點」的意義——
      玩家明明閃開了踐踏圈，卻在牠飛過時被擦到。要做的話是在騰空期間關掉怪身上的 `EnemyContactDamage`、
      落地再開（傷害全部交給 `LeapSlamImpact`），語義也比較乾淨：**踐踏＝落點的 AOE，不是撞擊**。
      等實機確認真的會發生再處理。
- [ ] **幀號是全域 const**，第二隻怪的 jump 幀序不同就得搬進 CSV（見 §9.9 第 2 步）。
- [ ] **踐踏傷害與接觸傷害可能同幀結算**：落地那一瞬間玩家若貼著怪，會先吃踐踏、接觸傷害那筆被玩家的
      無敵幀吃掉（`InvincibleTimeMs`）。目前看起來是想要的行為，實測若覺得「踩一下掉兩管」再處理。
- [ ] **沒有音效**（同專案其他演出，還沒有音訊系統）。
- 🔧 手感對不上時開 `LeapSlamBrain.DebugLog = true`：每次起跳會印起跳距離／落點／各段時間點／半徑與傷害。

---

## 10. 近戰追擊 — `MeleeChaseBrain`（攻擊動作要做完才能再移動）

> **狀態：✅ 程式完成（2026-09-18），⏳ 未編譯、未實機驗證。** 使用者：`BrainType=MeleeChase`，
> 以及 §9 的跳躍踐踏型跳完之後的追擊階段。

### 10.1 為什麼不是 `ChaseBrain`

`ChaseBrain` 只管一件事：「離目標 > 0.2 就一直走過去」。攻擊動畫則是 `MonsterController.HandleVisuals`
在「距離 ≤ `AttackRange`(1.3)」時**自動播**的。**兩件事互不相干**，於是：

> 作者 2026-09-18 實機回報：「我看到他已經**舉起劍**了，我躲閃，這時怪物竟然**還是能移動追著我並保持著舉劍的動作**，這太詭異了。」

這不是狂族皇家衛士的問題，**所有 `BrainType=Chase` 且有 attack 圖的怪都是這樣**——
只是以前的怪要嘛沒有 attack 圖、要嘛沒人盯著看。
`ChaseBrain` 一個字都沒改（既有怪零影響），新行為另開一支 Brain。

### 10.2 三段

| 階段 | 行為 |
|---|---|
| `Chase` | 走向目標；**已經貼身但還在攻擊間隔內 → 站著等**（貼身了本來就不需要再移動，繞著磨蹭很難看） |
| `Attack` | **站定**，attack 用 one-shot 播完整一輪。**這一段沒有任何中途取消**——目標跑掉了也要揮完 |
| `Recover` | 站定 `RecoverSeconds`(0.12) 的小硬直，然後回 `Chase` |

⚠ **貼身判定用碰撞框的邊緣距離**（`Physics2D.Distance`），不是 `AttackRange` 的中心距離——
同 §7.3 第 8 點與 **F22**：拿中心距離當「開始揮劍」的門檻等於每次都在打不到的距離揮空。

⚠⚠ **但「起手」和「停下來」要用兩個門檻**（2026-09-18 修）：

> 作者附圖回報：「我跟怪物只要維持這個距離，怪物就砍不到我，**他們一直攻擊但我不會受傷**。」

第一版拿同一個 `AttackSlack`(0.25) 當兩者的門檻，於是怪**系統性地停在打不到的地方**：
狂族皇家衛士的框半寬 0.77 ＋ 玩家半徑 0.5 ⇒ 接觸傷害要求中心距離 ≤ **1.27**，
而怪一到 **1.52** 就停下來揮 ⇒ **每次都差 0.25**。不是偶爾打不到，是永遠打不到。

現在分兩個：`AttackSlack`(0.25) 是**起手**門檻（讓動作提早一拍開始，比較自然），
`StopSlack`(0.02) 是**停下來**的門檻（＝與接觸傷害同一條線，怪會真的貼上去）。

⭐ **通則：「決定出手」的門檻可以寬，「停止移動」的門檻必須等於傷害判定的門檻。**
兩者共用一個數字，就會做出一隻永遠站在自己攻擊範圍外的怪。

**攻擊節奏**：`_nextAttackAt = 這一次揮的開始時間 + max(AttackInterval, 動作時長)`。
⚠ 從**開始**算而不是從結束算：否則 CSV 的 `AttackInterval` 會變成「動作時間 ＋ 間隔」，
一隻攻速 0.8 的怪實際上要 1.7 秒才揮一次。動作比間隔長時以動作為準（沒播完當然不能再揮）。
狂族皇家衛士：attack 12 張 @13fps ＝ 0.92 秒一輪 > `AttackInterval` 0.8 ⇒ 播完就能再揮，節奏 0.92 秒一刀。

### 10.3 `BrainControlsAttackPose`（不設就白做）

`MonsterController` 新增的旗標，**本 Brain 自己會設 true**。設了之後 `HandleVisuals` 不再做
「距離 ≤ `AttackRange` 就自動播 attack」的判定，攻擊姿勢完全由 Brain 的 one-shot 控制。

**不設的話這支等於白做**：怪走過去的路上距離一進 1.3 就已經自動舉劍了，
「站定揮劍」只是在牠早就舉著劍之後再舉一次。

- 預設 false ＝既有行為，所有既有怪一個像素都不會變。
- 設了 true 就**一定**要自己播 attack，否則這隻怪永遠不會有攻擊動畫。
- `NotifySkillCast`（召喚等技能的出手動作）不受影響，那條路仍然通。

### 10.4 傷害走「揮擊的命中幀」，不是「碰到就痛」（2026-09-18 改）

**近戰武器有長度**。狂族皇家衛士 attack 的第 9~12 幀劍已經掃出畫布外，
而接觸傷害完全不知道劍在哪裡——它只看兩個身體框有沒有碰到。
所以第一版會出現「劍明明掃過玩家，卻因為身體沒貼上而毫髮無傷」。

現在：**在劍掃出去的那一幀（`HitFrame = 9`）對前方開一次 `ImpactDamageArea`**（§9.4 的同一支通用傷害圈）。

| 參數 | 值 | 說明 |
|---|---|---|
| `HitFrame` | 9 | 劍完全掃出的第一幀。量 attack 每幀 bbox 的**右緣**量出來的：8 開始掃、9~12 掃到畫布外。**幀號即事件**，改 `AnimFPS` 自動跟著對 |
| `SwingReachSlack` | 0.75 | **命中門檻＝碰撞框的邊緣距離**（比起手門檻 0.25 大，代表劍比身體長）。與起手判定**同一個函式** |
| `SwingHitRadius` | 0.2 | 判定過了才在**目標身上**開一個小圈結算 ⇒ 必中 |

⚠⚠ **2026-09-18 二修：不要自己算圈的圓心與半徑。** 第一版是「以腳底為圓心、半徑＝框半寬＋0.55」，
作者把 `Scale` 調到 1.5 之後回報「砍不到的問題又出現了」，而且直覺很準：
**「不能因為我調大小就得再調攻擊範圍啊？」**——對，那是設計錯誤。兩個原因疊在一起：
① `0.55` 是寫死的世界單位，怪放大時劍也放大但它不會；
② **圈心在腳底、而玩家的碰撞圓 offset 是 0（圓心就在牠的 transform）**，
route B 怪物的 pivot 在畫布中心 ⇒ 腳底離 transform 半個身高，**那個落差 ∝ Scale**。
實測圈邊緣離玩家的圓還差多少才碰到：**Scale 1.3 ⇒ 0.27、1.5 ⇒ 0.23、2.0 ⇒ 0.12**，再大就打不到。
完整記錄與兩條通則見 [PROBLEMS.md](PROBLEMS.md) **F27**。

**現在**：命中幀直接問「目標的碰撞框離我的碰撞框多遠」（`SwingReachSlack`），與起手判定同一個函式
⇒ **「起手時打得到、命中幀還沒跑遠 ⇒ 必中」在數學上成立**，而且**完全與體型無關**。
判定過了才在目標的碰撞框中心開一個半徑 0.2 的小圈結算，不會因為圓心算錯而 miss。

⚠ 圈心是從**腳底**（`FeetWorldPos`）往前推，不是 `transform.position`——理由同上（**G13**）。

**揮擊方向是起手當下的目標方向**，揮到一半目標移動也不修正——動作已經定型，
這就是玩家「走位閃掉揮擊」的空間（同 §9.3 落點鎖定的取捨）。

⚠ **命中幀會夾進實際張數**（`Mathf.Clamp(HitFrame, 1, frames)`）：換一隻 attack 只有 6 張的怪時，
第 9 幀永遠不會到 ⇒ 那隻怪會一輩子揮空不傷人。

**接觸傷害預設關掉**（`DisableContactDamage = true`，本 Brain 在 `EnsureConfigured` 裡把怪身上的
`EnemyContactDamage` 停用）：傷害**只**來自揮擊 ⇒ 玩家可以貼著牠走位、躲過揮擊就不掉血，
這才是近戰該有的樣子。想要「貼著會被慢慢磨、揮擊再補一刀」就把那個常數改成 false。

### 10.5 待辦 / 已知限制

- [ ] **未編譯、未實機驗證**。實測看：0.92 秒一刀會不會太慢、`AttackSlack`(0.25) 會不會讓怪貼得太近才揮。
- [ ] **揮劍期間完全不能轉向**（`HandleVisuals` 的 flipX 還是每幀跟著目標跑，所以身體會轉、動作不會停）。
      若看起來怪，要連 flipX 一起在 one-shot 期間鎖住。
- [x] ~~傷害仍是接觸式~~ → 2026-09-18 改走揮擊命中幀，見 §10.4。
- [ ] **揮擊判定是圓形不是扇形**：圈心已經往面向方向偏了半個身位，但背後仍有一小塊會被打到。
      要精確的話得做扇形（角度 ＋ 半徑），目前看不出必要。
- [ ] **沒有「被打斷」的概念**：揮到一半被重擊也會揮完（擊退會把牠推開，但動作照播）。
      要做硬直/破招得另外接 `HitReactionHandler`。
- 🔧 手感對不上時開 `MeleeChaseBrain.DebugLog = true`：每次揮劍印幀數、動作時長、下次可揮的時間、邊緣距離。
