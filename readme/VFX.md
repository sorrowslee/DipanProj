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
* **擊中特效 (`HitEffectID`)**：子彈／光束**命中點**播一次。涵蓋打到怪物、障礙物、拋物線落地三種情況（**首版統一一種特效**，不分表面）。雷射的命中特效綁在 `dotInterval` tick，天然節流。
* **軌跡特效 (`TrailEffectID`)**：沿子彈**飛行路徑**每隔配方 `TrailStep` 距離種一個（由 `BulletInstance.OnTrailPoint` 觸發）。**地刺類武器**＝隱形子彈沿路種尖刺，因此自動吃滿反彈/分裂/追蹤等彈道行為（見 [BALLISTICS.md](BALLISTICS.md)、[RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) 的 `TrailStep`）。

三者都引用同一張 `VfxTable.csv`、用同一個 `VfxManager.Spawn(id, position, angle)`。

## 配置檔案

`Assets/Data/VfxTable.csv`：

| 欄位 | 說明 |
|---|---|
| `ID` | 特效唯一識別碼（WeaponTable 的 FireEffectID / HitEffectID / TrailEffectID 引用此值） |
| `Name` | 僅供辨識，程式不使用 |
| `AniPath` | 序列圖路徑前綴（相對 `Resources/`，不含 `_01` 編號與副檔名） |
| `AniNumber` | 序列圖張數，系統載入 `{AniPath}_01` ~ `{AniPath}_NN` |
| `AnimFPS` | 播放幀率（幀/秒） |
| `Scale` | 整體縮放倍率；留空或 ≤ 0 = 1 |
| `Loop` | 是否循環；留空/0 = 播一輪自毀（預設），1 = 循環 |
| `Duration` | 僅 `Loop=1` 時有意義：循環存活秒數（≤ 0 退回 1 秒）。`Loop=0` 時忽略（壽命 = 一輪動畫長度 = AniNumber / AnimFPS） |
| `SortingOrder` | **本特效專屬排序**；留空 = 用 VfxManager 全域預設。角色/怪物在 `10`，故地刺類想畫「在腳下」就填 `< 10`（例如 `5`），擊中爆炸想壓在最上層就留空（用全域 `100`）。**每個特效各自設定、互不影響** |

## 執行單元

* `VfxManager`（場景單例）：`Awake` 載入 CSV、預載序列圖；`Spawn(id, position, angle)` 工廠。**不需要 prefab**——每次 Spawn 自己 `new GameObject` + `SpriteRenderer`。排序預設用 Manager 的 `SortingLayerName` / `SortingOrder`（全域），但**每個特效可在 VfxTable 自填 `SortingOrder` 覆寫**（地刺填 5 畫在腳下、爆炸留空維持上層，改一個不動其他）。`VfxMaterial` 選用。
* `VfxInstance`（Spawn 時動態掛上）：單一 SpriteRenderer 播動畫；`Loop=false` 播完一輪 `Destroy`、`Loop=true` 撐滿 `Duration` 後 `Destroy`。
* 與彈道系統**完全分離**：VFX 是純主遊戲表演層，`Sorrows.Ballistics` 不認識它（維持解耦邊界）。

## 串接點（皆讀「發射快照武器」`firedWeapon`）

| 特效 | 觸發方法（PlayerController） | 位置 |
|---|---|---|
| 發射 | `Shoot`（離散）／`UpdateLaser` 按下時（雷射） | 玩家位置，朝滑鼠 |
| 擊中-怪/牆 | `HandleBulletHit` | `hit.point` |
| 擊中-地面 | `HandleParabolicLanded` | 落點 |
| 擊中-雷射 | `HandleBeamTick`（每 `dotInterval` 一跳） | 每個命中點 |

沿用「發射時以 lambda closure 鎖住當下武器」的快照機制，玩家中途切武器不會讓舊子彈誤用新武器的特效（與 GroundEffect 同邏輯）。

## Unity 端一次性設定

1. 場景內建一個空物件、掛 `VfxManager`，把 `VfxTable.csv` 指給 `VfxCSV`，設定 `SortingLayerName` / `SortingOrder`（建議排在角色之上）。
2. 把發射／擊中序列圖放進 `Assets/Resources/` 對應目錄（`{前綴}_01.png` ...）。
3. 在 `VfxTable.csv` 加一列、在 `WeaponTable.csv` 對應武器填 `FireEffectID` / `HitEffectID` 即可，不需動程式。

> 加新特效 = `VfxTable` 多一列 ＋ 武器表填編號，零改程式。這套原語日後也可餵給「怪物死亡煙、撿道具閃光」等需求。
