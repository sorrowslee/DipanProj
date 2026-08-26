# 一次性特效系統 (VFX：發射特效 ＋ 擊中特效)

> 返回 [文件總覽](README.md)

純主遊戲端的「一次性視覺特效」原語。和 [GROUND_EFFECT.md](GROUND_EFFECT.md) 的地面特效是兄弟系統，但**刻意更輕**：

| | 地面特效 (GroundEffect) | 一次性特效 (Vfx) |
|---|---|---|
| 本質 | 停留型 AOE **遊戲機制** | 純表演 **視覺回饋** |
| 傷害 | 有（Damage / DOT） | **無** |
| 範圍 | 有（Radius / tile 鋪面） | 無（單一 sprite） |
| 生命週期 | Duration / 永久 | 播完一輪自毀（或 Loop 撐 Duration） |
| 放哪張表 | 行為 → **RecipeTable**（`GroundEffectID`） | 外觀 → **WeaponTable**（`FireEffectID` / `HitEffectID`） |

**為什麼放 WeaponTable 而不是 RecipeTable**：發射/擊中特效是「這把武器的視覺身分」、不帶任何遊戲數值，與 `WeaponSprite`、`BeamStyle` 同類；放武器表才能讓「共用同一配方行為的多把武器」各掛不同特效。對照 `GroundEffectID` 之所以在配方表，是因為它是會結算傷害的機制。

## 三種用途共用同一原語

* **發射特效 (`FireEffectID`)**：每次發射在**玩家身上**播一次、朝瞄準方向。離散武器（直射/環繞/拋物線）每次射擊播一次（吃 `FireInterval` 節流）；雷射在**按下瞬間**播一次（持續光束不每幀重播）。
* **擊中特效 (`HitEffectID`)**：子彈／光束**命中點**播一次。涵蓋打到怪物、障礙物、拋物線落地三種情況（**首版統一一種特效**，不分表面）。雷射的命中特效綁在 `DotInterval` tick，天然節流。
* **軌跡特效 (`TrailEffectID`)**：沿路徑每隔配方 `TrailStep` 距離鋪一個特效。兩種載體：
  * **子彈**（一般飛行道具）→ 由 `BulletInstance.OnTrailPoint` 觸發，做**地刺類武器**（隱形子彈沿路種尖刺，自動吃滿反彈/分裂/追蹤）。見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) 的 `TrailStep`。
  * **雷射光束**（`Mode=Laser` + `TrailEffectID>0`）→ 沿光束路徑鋪**循環**火焰，做**火焰噴射器**（按住掃射、持續 DOT）。此時火焰 Vfx 須 `Loop=1` + `Duration=-1`（持續循環、由 PlayerController 管理生死）。見 [LASER.md](LASER.md) 的「火焰噴射器」。

三者都引用同一張 `VfxTable.csv`、用同一個 `VfxManager.Spawn(id, position, angle)`。

## 召喚特效（`SummonEffectID`，武器表；特效播完才生怪）

召喚型武器（`Mode=Summon`，見 [BOSS_MODULE.md](BOSS_MODULE.md)）可在 **WeaponTable** 填 `SummonEffectID`（引用 `VfxTable`）——施放時在**每個生怪點**播一次特效，怪物**在同一幀一起出現**（邊播特效邊出現；其他三種特效也是即放即忘，這個只是多在生怪點播一顆特效）。

* 由 `SummonSystem.Cast` 在每個生怪點 `VfxManager.Spawn` 播特效，並在**同一幀** `SpawnMonster`。
* 玩家（`PlayerController.Shoot`）與 boss（`MonsterWeaponUser`）召喚**共用** `SummonSystem.Cast`，都吃這一欄。
* 同時上限（`SummonMaxAlive`）由 `SummonSystem` 一次算好；施放端在扣魔/進冷卻前先用 `SummonSystem.HasRoom` 確認有空位（滿了不扣魔、不動作）。場景找不到 `VfxManager` 則只生怪、不播特效。
* 留空 / 0 = 不播特效、立即生怪（原行為）。目前紅嫁衣召喚（武器 14）、御靈水晶（武器 13）用 `VfxTable` 10「招喚怪物」。
* **特效大小自動跟著目標**（召喚／怪物死亡／地上物破壞共用）：走 `VfxManager.SpawnSizedToHeight`，把特效縮放到「目標的可見高度」——
  - 招喚（id 10）＝該怪可見高度（route B 怪＝`CharacterWorldHeight` 1.95 × `MonsterData.Scale`）；
  - 怪物死亡（id 7，`MonsterController` 死亡）＝該怪 `SpriteRenderer.bounds` 的高度與中心；
  - 地上物破壞（id 5，`DestructibleObject`）＝該地上物 `SpriteRenderer.bounds` 的高度與中心。
  所以**大目標大特效、小目標小特效**。此時 `VfxTable` 該列的 `Scale` 欄對這三種特效改當「**相對目標高度的倍率**」（1 = 與目標等高、1.3 = 比目標大 30% 包住它）。**要調大小就改這一欄**：id 10 目前 1.3；id 5／id 7 目前 1（＝與物件/怪等高），覺得爆裂太小/太大就往上/下調。


## 配置檔案

`Assets/Data/VfxTable.csv`：

| 欄位 | 說明 |
|---|---|
| `ID` | 特效唯一識別碼（WeaponTable 的 FireEffectID / HitEffectID / TrailEffectID 引用此值） |
| `Name` | 僅供辨識，程式不使用 |
| `AniPath` | 序列圖路徑前綴（相對 `Resources/`，不含 `_01` 編號與副檔名） |
| `AniNumber` | 序列圖張數，系統載入 `{AniPath}_01` ~ `{AniPath}_NN`（**2 位補零**，從 `_01` 起算）。⚠️ **必須與實際檔案張數完全相符**：`VfxManager` 是「全有或全無」——只要有任一幀載不到（例如 `AniNumber` 多算一張、或某張命名/匯入不對），整個特效的 `AnimationSprites` 會被設成 `null`、`Spawn` 直接跳過＝**遊戲裡完全看不到**（Console 會有 `Vfx sprite not found at ...` 警告可查）。 |
| `AnimFPS` | 播放幀率（幀/秒） |
| `Scale` | 整體縮放倍率；留空或 ≤ 0 = 1 |
| `Loop` | 是否循環；留空/0 = 播一輪自毀（預設），1 = 循環 |
| `Duration` | 僅 `Loop=1` 時有意義：循環存活秒數。`Duration=0` 退回 1 秒；**`Duration=-1` = 無限循環**（永不自毀、由外部持有並負責清除，例如火焰噴射器的火焰柱）。`Loop=0` 時忽略（壽命 = 一輪動畫長度 = AniNumber / AnimFPS） |
| `SortingOrder` | **本特效專屬排序**；留空 = 用 VfxManager 全域預設（現為 `22000`，畫在角色/地上物之上）。**角色/怪物已改走 Y 排序帶**（`MapDepthSort`，16-bit 繞回後約 1~1.7 萬），所以地刺類想畫「在腳下（角色/地上物之下）」就填一個**低於該帶的小值**（例如 `5`）；擊中爆炸想壓在最上層就留空用全域。**每個特效各自設定、互不影響** |

## 執行單元

* `VfxManager`（場景單例）：`Awake` 載入 CSV、預載序列圖；`Spawn(id, position, angle)` 工廠。**不需要 prefab**——每次 Spawn 自己 `new GameObject` + `SpriteRenderer`。排序預設用 Manager 的 `SortingLayerName` / `SortingOrder`（全域，現為 `22000`＝角色/地上物之上），但**每個特效可在 VfxTable 自填 `SortingOrder` 覆寫**（地刺填 5 畫在腳下、爆炸留空維持上層，改一個不動其他）。`VfxMaterial` 選用。
* `VfxInstance`（Spawn 時動態掛上）：單一 SpriteRenderer 播動畫。
  * `Loop=false`：**銷毀由動畫進度驅動**——每一格顯示滿一個 frameDuration 才前進、播到最後一幀（不繞回第一幀）再撐滿一輪才 `Destroy`。**保證完整播完每一格**，跟子彈/光束速度無關（不管 AnimFPS 多慢都會播完）。
  * `Loop=true`：循環播放，撐滿 `Duration` 秒後 `Destroy`；**`Duration=-1` = 無限循環**（永不自毀、由外部持有並清除，火焰噴射器的火焰柱用此）。
* 與彈道系統**完全分離**：VFX 是純主遊戲表演層，`Sorrows.Ballistics` 不認識它（維持解耦邊界）。

## 串接點（皆讀「發射快照武器」`firedWeapon`）

| 特效 | 觸發方法（PlayerController） | 位置 |
|---|---|---|
| 發射 | `Shoot`（離散）／`UpdateLaser` 按下時（雷射） | 玩家位置，朝滑鼠 |
| 擊中-怪/牆 | `HandleBulletHit` | `hit.point` |
| 擊中-地面 | `HandleParabolicLanded` | 落點 |
| 擊中-雷射 | `HandleBeamTick`（每 `DotInterval` 一跳） | 每個命中點 |

沿用「發射時以 lambda closure 鎖住當下武器」的快照機制，玩家中途切武器不會讓舊子彈誤用新武器的特效（與 GroundEffect 同邏輯）。

## Unity 端一次性設定

1. 場景內建一個空物件、掛 `VfxManager`，把 `VfxTable.csv` 指給 `VfxCSV`，設定 `SortingLayerName` / `SortingOrder`（建議排在角色之上）。
2. 把發射／擊中序列圖放進 `Assets/Resources/` 對應目錄（`{前綴}_01.png` ...）。
3. 在 `VfxTable.csv` 加一列、在 `WeaponTable.csv` 對應武器填 `FireEffectID` / `HitEffectID` 即可，不需動程式。

> 加新特效 = `VfxTable` 多一列 ＋ 武器表填編號，零改程式。這套原語日後也可餵給「怪物死亡煙、撿道具閃光」等需求。

## 血統變身用的特效（2026-08-18）

| ID | 名稱 | 素材 | Loop | SortingOrder | 備註 |
|---|---|---|---|---|---|
| 30 | 變身煙塵 | `VfxEffects/TransformSmoke/`（10 幀） | 0（播完自毀） | 22100 | 放大到玩家身高 ×1.6 蓋住趴著的玩家，換裝就藏在這一瞬 |
| 31 | 變身環繞電弧 | `VfxEffects/TransformAura/`（22 幀） | 1，`Duration=-1` | 22050 | **無限循環、生死由程式控**（`BloodlineTransformFxRunner` 負責 Destroy），掛在玩家 transform 下跟著移動 |

變身用的雷柱**不在這張表裡**——它是「頂端 + 可平鋪身體」的多段組合，走 `SegmentedLightningColumn`
（與九霄雷獄同一套邏輯、只是換 `Style` 指定素材路徑），素材在 `VfxEffects/TransformLightning/{Start,Loop}/`。
完整說明見 [BLOODLINE.md](BLOODLINE.md) §5。

⚠ id 31 這種 `Duration=-1` 的循環特效**一定要有人負責 Destroy**，漏了就會變成永遠掛在玩家身上的裝飾。

### `VfxInstance.Unscaled`（2026-08-19）

`VfxInstance` 多了一個 **預設 `false`** 的 `Unscaled` 旗標：打開之後動畫推進、壽命倒數、
受擊白光的等待全部改吃 `Time.unscaledDeltaTime`／`WaitForSecondsRealtime`。

給「**遊戲暫停期間仍然要播**」的演出用——目前是血統變身（整段 `timeScale = 0`）。
`Spawn` 系列本來就會回傳實體，所以生出來直接 `inst.Unscaled = true` 即可，**不用改任何簽章**，
一般戰鬥特效行為零改變（暫停時本來就該停）。

同樣的旗標 `SegmentedLightningColumn` 也有一份。詳見 [PROBLEMS.md](PROBLEMS.md) **D15**。
