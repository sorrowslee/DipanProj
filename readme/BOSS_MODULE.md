# Boss 戰鬥模組 (Boss Combat Modules) + 召喚系統

> 返回 [文件總覽](README.md)｜怪物量產見 [MONSTER_SETUP.md](MONSTER_SETUP.md)｜武器/配方見 [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)｜傷害結算見 [COMBAT.md](COMBAT.md)｜劇情分支見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §7
>
> **狀態：✅ 框架 + 紅嫁衣 boss（逃跑＋召喚）程式完成（2026-07-09），待 Unity 實機驗證＋擺放。** 投射型武器供怪物使用（飛劍/落雷…）為 Phase 2。

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

**未來給主角用**：召喚邏輯寫成擁有者無關（只靠 transform 位置＋配方）。要讓主角召喚，把 `MonsterWeaponUser.TrySummon` 抽成共用靜態 helper、玩家側 `PlayerController.Shoot` 加一個 `if (recipe.IsSummon)` 分支呼叫即可（雙方各持一份 alive 清單管上限）。**本期未接玩家側**（守住「不動玩家戰鬥、降風險」的範圍決定）。

---

## 4. 接手待辦（Unity / 編輯器端）

- [ ] **重跑 `Project Tools → Sync Map Assets`**：家人幽靈的 `walk` 幀在 `GameAssets` 有、但 `StreamingAssets` 多數只有 `idle`（尚未同步）；不同步召喚出的幽靈會缺走路動畫。順帶把 CSV 也帶到位。
- [ ] **在編輯器紅嫁衣最終房放 boss**：怪物出生點填**怪物 ID 13**（RedBridalGown）。她的 BrainType/Weapon 已在 CSV 設好。
- [ ] **家人怪出生點**（給 killedFamily 用）：預先擺放的家人怪（8~12）出生點填「死亡觸發旗標」`killedFamily`（關卡單次），與 boss 召喚分身無關。
- [ ] **實機調手感**：逃跑速度（CSV `Speed`，現 3.5）、逃/停距離與召喚冷卻/上限（配方 26）。太難就降 `SummonMaxAlive`/拉長 `FireInterval`；她太好抓就升 `Speed`。
- [ ] （Phase 2）要讓怪物射飛劍/落雷 → 把 PlayerController 發射管線抽成共用服務，`MonsterWeaponUser` 非召喚分支接上。
