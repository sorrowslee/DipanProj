# 可破壞地上物 (Destructible Objects)

> 返回 [文件總覽](README.md)｜地圖載入見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)

地圖載入器生成的地上物(家具等)可被武器打爆;爆掉後碰撞消失,原本擋路/反彈的位置自動開通。

## 運作方式

- `MapLoader` 生成每個地上物時,除了 SpriteRenderer + 貼合碰撞框,還掛上 `DestructibleObject`(在 `DipanProj_Main/Assets/Scripts/Map/DestructibleObject.cs`)並設定血量與破壞特效 ID。

> **動畫地上物**(多張圖做成一個物件,見 readme/MapEditor_DESIGN.md):`MapLoader.BuildObjects` 對 `catalog` 標記為動畫的物件,載入幀序列並掛 `AnimatedMapObject`(原地循環播放,速度 = `.dipanmap` 的 `objects[].animFps`,每實例可不同)。**碰撞框 / 血量 / 可破壞一律以「第一幀」建立**,動畫只換顯示用的 sprite——所以動畫物件同樣可破壞、被打爆時碰撞與動畫一起消失,與靜態物件無異。

### 血量

- 每個地上物的血量由**地圖編輯器**寫入 `.dipanmap` 的 `objects[].hp`(預設 **1**,打一下就壞)。`MapLoader` 讀 `inst.hp` 設給 `DestructibleObject`;`hp == 0` 退回 `MapLoader.objectMaxHP`(全域後備)。
- **`hp == -1`(或任何負值)= 不可摧毀**:不掛 `DestructibleObject`,但仍保留碰撞框 → 等於一面打不爆的牆(擋玩家/怪物＋反彈子彈),用來擺「固定障礙物」。

### 傷害來源(全部統一走 `IDamageable`)

怪物(`MonsterController`)與地上物(`DestructibleObject`)都實作 **`IDamageable`**(`Assets/Scripts/Combat/IDamageable.cs`),所有傷害點用同一條路徑結算,因此**任何能造成傷害的武器都能破壞地上物**:

- **一般子彈**:`PlayerController.HandleBulletHit` → `ApplyDamage(target)`(命中任何 CollisionMask 都會回呼,含 Environment)。
- **雷射 / 火焰噴射器**:`LaserBeam` 把敵人命中與「環境命中(牆/地上物)」分成兩個回呼——敵人走 `OnBeamDamageTick`,環境走 **`OnBeamEnvironmentTick`**(新加)。`PlayerController.HandleBeamEnvironment` 用後者扣血,**不會在牆上噴擊中特效或分裂**。
  > 原因:`LaserBeam` 原本只回報可穿透層(敵人),把 Environment 當不可破壞的牆,所以雷射本來打不爆地上物;新增環境回呼後補上。
- **地刺等軌跡彈**:本質是一般子彈,走 `HandleBulletHit`,自動涵蓋。
- **拋物線落地爆炸**:`TryApplyParabolicBlast` 的 `OverlapCircle` 範圍含 `EnemyLayer | EnvLayer`,對範圍內 `IDamageable` 結算。
- **地面特效(火堆/毒霧等)**:`GroundEffectInstance` 的範圍傷害改打 `IDamageable`,範圍層含 Environment(`GroundEffectManager.EnvironmentLayer`,留空自動以名稱解析)。
- 血量歸零 → 在物件中心播一次破壞特效(`VfxManager.Spawn`)→ `Destroy(gameObject)`。碰撞隨之消失,玩家/子彈即可通過。

## 破壞特效 = 沿用 VFX 系統(VfxTable)

破壞特效不另起一套,直接用既有的一次性特效系統(見 [VFX.md](VFX.md))。

- **專屬欄位**:`VfxTable.csv` 新增 **ID 5「地上物破壞」**。`DestructibleObject.DestroyVfxId` 預設指向它(由 `MapLoader.objectDestroyVfxId` 設定)。
- **目前是暫借**:ID 5 現在借用「爆炸」的序列圖(`VfxEffects/Explosive/Explosive`, 8 張, 20 FPS)頂著用。
- **檔名 / 張數 / FPS 就是這一列的三個欄位**(這就是「拉出來可設定的參數」):
  - `AniPath`  = 序列圖路徑前綴(相對 `Assets/Resources/`,不含 `_01` 編號與副檔名)
  - `AniNumber`= 序列圖張數
  - `AnimFPS`  = 播放幀率
  - 另有 `Scale`(縮放)、`Loop`(0 播一輪自毀)、`Duration`、`SortingOrder`。

### 之後換成真正的破壞特效(零改程式)

1. 把破壞特效的序列圖放到 **`DipanProj_Main/Assets/Resources/VfxEffects/ObjectDestroy/`**,檔名 `ObjectDestroy_01.png` … `ObjectDestroy_NN.png`(遵照其他 VFX 的資料夾慣例:`Resources/VfxEffects/<特效名>/<特效名>_NN.png`)。
2. 把 `VfxTable.csv` 第 ID 5 列改成:
   `5, 地上物破壞, VfxEffects/ObjectDestroy/ObjectDestroy, <張數>, <FPS>, 1, 0, ,`
3. 完成。`DestroyVfxId` 仍是 5,不必動任何程式或 Inspector。

> 若想針對不同家具用不同破壞特效,可在 VfxTable 再加列,並改用 per-object 設定(目前是全域同一個,見下方限制)。

## 可調參數(MapLoader Inspector)

| 欄位 | 預設 | 說明 |
|---|---|---|
| `Objects Destructible` | true | 關掉 = 地上物變不可破壞(純實心) |
| `Object Max HP` | 20 | **後備**血量:僅當某地上物在 `.dipanmap` 沒寫 `hp` 時才用;一般以編輯器每物件 `hp`(預設 1)為準 |
| `Object Destroy Vfx Id` | 5 | 破壞特效的 VfxTable ID;0 = 不播特效 |

## v1 行為與限制(之後可擴充)

- **全域血量**:所有地上物同一個 `Object Max HP`、同一個破壞特效。未來可改為 per-object(例如從 catalog 多一個欄位帶 HP / 特效 ID / 是否不可破壞)。
- **子彈仍會反彈**:打到地上物時會扣血,但子彈仍照武器配方反彈/穿透(地上物在 Environment 層)。若要「特定武器打中即同歸於盡(子彈消失、家具也消失)」,需在命中流程加入 per-weapon 的 consume 旗標,屬後續工作。
- **特效播在物件中心、不隨物件大小縮放**:大型家具的破壞特效不會自動放大。需要的話可在 VfxTable 的 `Scale` 調,或未來依物件包圍盒縮放。
- **全傷害來源已涵蓋**:子彈、雷射/火焰噴射器、地刺、拋物線爆炸、地面特效皆可破壞地上物(統一走 `IDamageable`)。
- **邊角案例**:`BlockedByEnvironment=0`(子彈/雷射可穿透地形)的配方,Environment 被當可穿透層;子彈仍會在穿過時回呼造成傷害,雷射則走敵人回呼(`HandleBeamTick` 也走 `ApplyDamage`,仍會扣血)。一般阻擋型配方走環境回呼,皆正常。

## 相關檔案

- `DipanProj_Main/Assets/Scripts/Combat/IDamageable.cs` — 共用受傷介面(怪物 + 地上物)
- `DipanProj_Main/Assets/Scripts/Map/DestructibleObject.cs` — 血量 / 受擊 / 破壞特效 / 自毀(實作 `IDamageable`)
- `DipanProj_Main/Assets/Scripts/Map/MapLoader.cs` — 生成時掛 `DestructibleObject`,血量取自地圖 `inst.hp`
- `DipanProj_Main/Assets/Scripts/PlayerController.cs` — `ApplyDamage` 統一入口;子彈/雷射/拋物線爆炸
- `DipanProj_Main/Assets/Scripts/AI/MonsterController.cs` — 實作 `IDamageable`
- `DipanProj_Main/Assets/Scripts/Combat/GroundEffectInstance.cs` / `GroundEffectManager.cs` — 地面特效改打 `IDamageable`,範圍含 Environment
- `BallisticsSystem/Runtime/Core/LaserBeam.cs` — 新增 `OnBeamEnvironmentTick` 環境命中回呼
- `DipanProj_Main/Assets/Data/VfxTable.csv` — ID 5「地上物破壞」特效列
