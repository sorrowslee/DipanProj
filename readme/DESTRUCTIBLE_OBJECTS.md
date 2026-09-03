# 可破壞地上物 (Destructible Objects)

> 返回 [文件總覽](README.md)｜地圖載入見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)

地圖載入器生成的地上物(家具等)可被武器打爆;爆掉後碰撞消失,原本擋路/反彈的位置自動開通。

## 運作方式

- `MapLoader` 生成每個地上物時,除了 SpriteRenderer + 貼合碰撞框,還掛上 `DestructibleObject`(在 `DipanProj_Main/Assets/Scripts/Map/DestructibleObject.cs`)並設定血量與破壞特效 ID。

> **動畫地上物**(多張圖做成一個物件,見 readme/MapEditor_DESIGN.md):`MapLoader.BuildObjects` 對 `catalog` 標記為動畫的物件,載入幀序列並掛 `AnimatedMapObject`(原地循環播放,速度 = `.dipanmap` 的 `objects[].animFps`,每實例可不同)。**碰撞框 / 血量 / 可破壞一律以「第一幀」建立**,動畫只換顯示用的 sprite——所以動畫物件同樣可破壞、被打爆時碰撞與動畫一起消失,與靜態物件無異。

### 血量

- 每個地上物的血量由**地圖編輯器**寫入 `.dipanmap` 的 `objects[].hp`(預設 **1**,打一下就壞)。`MapLoader` 讀 `inst.hp` 設給 `DestructibleObject`;`hp == 0` 退回 `MapLoader.objectMaxHP`(全域後備)。
- **`hp == -1`(或任何負值)= 不可摧毀**:不掛 `DestructibleObject`,但仍保留碰撞框 → 等於一面打不爆的牆(擋玩家/怪物＋反彈子彈),用來擺「固定障礙物」。

### 破壞觸發旗標（打破某物 → 改變劇情）

- 每個可破壞地上物的選取面板有「**破壞旗標**」欄(`ObjectInstance.breakFlag`,編輯器物件面板、輸入旗標 id→確認)。這個物件被打爆時 `DestructibleObject.Die()` 把該旗標設為 true(給觸發鏈 `requireFlag` 用,例:打破珍貴供品→改變關卡走向)。**綁「這一個擺放」**、每個各自設定;空＝不寫。旗標先在編輯器旗標管理器登記(周目/永久),見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)。
- 只有**可破壞**物件(非可走、`hp >= 0`)才寫得出旗標;不可摧毀(`hp = -1`)或可走物件不掛 `DestructibleObject`,此欄無效(面板也不顯示)。

### 傷害來源(全部統一走 `IDamageable`)

怪物(`MonsterController`)與地上物(`DestructibleObject`)都實作 **`IDamageable`**(`Assets/Scripts/Combat/IDamageable.cs`),所有傷害點用同一條路徑結算,因此**任何能造成傷害的武器都能破壞地上物**:

- **一般子彈**:`PlayerController.HandleBulletHit` → `ApplyDamage(target)`(命中任何 CollisionMask 都會回呼,含 Environment)。
- **雷射 / 火焰噴射器**:`LaserBeam` 把敵人命中與「環境命中(牆/地上物)」分成兩個回呼——敵人走 `OnBeamDamageTick`,環境走 **`OnBeamEnvironmentTick`**(新加)。`PlayerController.HandleBeamEnvironment` 用後者扣血,**不會在牆上噴擊中特效或分裂**。
  > 原因:`LaserBeam` 原本只回報可穿透層(敵人),把 Environment 當不可破壞的牆,所以雷射本來打不爆地上物;新增環境回呼後補上。
- **地刺等軌跡彈**:本質是一般子彈,走 `HandleBulletHit`,自動涵蓋。
- **拋物線落地爆炸**:`TryApplyParabolicBlast` 的 `OverlapCircle` 範圍含 `EnemyLayer | EnvLayer`,對範圍內 `IDamageable` 結算。
- **地面特效(火堆/毒霧等)**:`GroundEffectInstance` 的範圍傷害改打 `IDamageable`,範圍層含 Environment(`GroundEffectManager.EnvironmentLayer`,留空自動以名稱解析)。
- 血量歸零 → **把自己那張圖炸成碎片**(`ShatterBurst.Play`)→ `Destroy(gameObject)`。碰撞隨之消失,玩家/子彈即可通過。

## 破壞演出 = 程序化碎片（2026-09-03 起）

**共用特效已預設關閉**。原本所有地上物都播 VfxTable ID 5 的同一顆煙塵,石雕、木桶、布幔破起來一模一樣,
看得出「每種材質都同一種破法」。現在改成 **`ShatterBurst`**(`Assets/Scripts/Map/ShatterBurst.cs`):
破壞當下把**這個物件自己的那張圖**切成 `3×4 = 12` 塊,每塊生成一個短命的 SpriteRenderer,
順著擊退方向飛開、旋轉、縮小、淡出(0.6 秒)。**碎塊來自該物件的貼圖,所以材質差異是免費附帶的**——
石頭爆石頭色、木頭爆木頭色,不必為每個物件畫破壞圖。動畫物件取**當前那一幀**(噴泉的水花會停在被打爆的瞬間)。

- 所有碎片指向**同一張貼圖**(`Sprite.Create` 只換取樣區域,不複製貼圖),沿用來源的材質與 sortingOrder ⇒ 可合批。
- 碎片**不掛任何 Collider**(掛了會擋路,還會被武器的目標搜尋當成命中對象)。
- 碎片掛在被破壞物件的**父節點**下(來源同一幀就被 Destroy),換圖拆地圖時會一併清掉。
- runtime 建的 Sprite 由 `OnDestroy` 收掉(不手動 Destroy 會累積到換場景)。
- ⚠ `IDamageable.TakeDamage` 的第二個參數是**擊退方向**不是命中座標(舊註解把它叫 hitPoint,從來沒被用到)。碎片順著它飛。

手感常數都在 `ShatterBurst` 檔頭(碎片數、存活秒數、初速、阻力、自轉、縮小、往下沉)。**v1 刻意的限制**:
切法是規則網格不是 Voronoi 尖角(0.6 秒內又在縮小,肉眼讀不出矩形;要更碎再換 `Sprite.OverrideGeometry` 餵多邊形頂點,仍是 SpriteRenderer、仍合批);
不跳過全透明格子(那要 `GetPixels32()` 掃整張圖、1024² 會配置 4MB 造成 GC 突波,而空格子只是畫不出東西;
要省的話 catalog 的 `FootprintMask` 本來就知道哪些格子是空的)。

**想換回舊的共用特效**:`DestructibleObject` 的 **`Play Legacy Destroy Vfx`** 打開即可(程式與 VfxTable 那一列都保留);
兩個都開 = 碎片＋煙塵兩層一起播。`Play Shatter` 關掉 = 不炸碎片、直接消失。

## 舊的破壞特效(VfxTable,目前預設不播)

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
| `Object Destroy Vfx Id` | 5 | 舊共用破壞特效的 VfxTable ID;0 = 不播。**目前預設不播**(`DestructibleObject.PlayLegacyDestroyVfx = false`) |

## v1 行為與限制(之後可擴充)

- **碎片手感全域一組**:碎片數/速度/秒數寫在 `ShatterBurst` 檔頭,所有物件共用。之後要分材質(石＝大塊慢、木＝長條翻滾、陶瓷＝小片四散、布＝不切只飄散),開一張 key = assetId 的小表、空白＝預設即可,不必動程式結構。
- **全域血量**:所有地上物同一個 `Object Max HP`。未來可改為 per-object(例如從 catalog 多一個欄位帶 HP / 是否不可破壞)。
- **子彈仍會反彈**:打到地上物時會扣血,但子彈仍照武器配方反彈/穿透(地上物在 Environment 層)。若要「特定武器打中即同歸於盡(子彈消失、家具也消失)」,需在命中流程加入 per-weapon 的 consume 旗標,屬後續工作。
- **碎片初速依可見高度給**(`SpeedPerHeight`),所以大物件碎片飛得遠、比例不會怪;舊的共用特效則是靠 `VfxManager.SpawnSizedToHeight` 縮放。
- **全傷害來源已涵蓋**:子彈、雷射/火焰噴射器、地刺、拋物線爆炸、地面特效皆可破壞地上物(統一走 `IDamageable`)。
- **邊角案例**:`BlockedByEnvironment=0`(子彈/雷射可穿透地形)的配方,Environment 被當可穿透層;子彈仍會在穿過時回呼造成傷害,雷射則走敵人回呼(`HandleBeamTick` 也走 `ApplyDamage`,仍會扣血)。一般阻擋型配方走環境回呼,皆正常。

## 相關檔案

- `DipanProj_Main/Assets/Scripts/Combat/IDamageable.cs` — 共用受傷介面(怪物 + 地上物)
- `DipanProj_Main/Assets/Scripts/Map/DestructibleObject.cs` — 血量 / 受擊 / 破壞演出 / 自毀(實作 `IDamageable`)
- `DipanProj_Main/Assets/Scripts/Map/ShatterBurst.cs` — **程序化碎片**(把物件自己的貼圖切塊飛散,零素材)
- `DipanProj_Main/Assets/Scripts/Map/MapLoader.cs` — 生成時掛 `DestructibleObject`,血量取自地圖 `inst.hp`
- `DipanProj_Main/Assets/Scripts/PlayerController.cs` — `ApplyDamage` 統一入口;子彈/雷射/拋物線爆炸
- `DipanProj_Main/Assets/Scripts/AI/MonsterController.cs` — 實作 `IDamageable`
- `DipanProj_Main/Assets/Scripts/Combat/GroundEffectInstance.cs` / `GroundEffectManager.cs` — 地面特效改打 `IDamageable`,範圍含 Environment
- `BallisticsSystem/Runtime/Core/LaserBeam.cs` — 新增 `OnBeamEnvironmentTick` 環境命中回呼
- `DipanProj_Main/Assets/Data/VfxTable.csv` — ID 5「地上物破壞」特效列
