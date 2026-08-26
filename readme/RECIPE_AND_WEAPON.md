# 配方與武器系統 (Recipe & Weapon System)

> 返回 [文件總覽](README.md)

採用 CSV 雙表架構，實現「配方（飛行行為）」與「武器（遊戲屬性／外觀）」的完全分離。

## 配方表 (RecipeTable.csv)
定義子彈的飛行行為配方，存放於 `Assets/Data/RecipeTable.csv`。

> **2026-08-26 大改**：45 欄、依模式分群；一列一種 `Mode`；所有數值欄空白＝預設；`#` 開頭整列＝註解。
> **每個欄位的意義、預設、各模式吃哪些欄、範例，全部在 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)**，這裡只講程式怎麼讀。

### 讀表方式（`Dipan.Data.CsvTable`，`Assets/Scripts/Data/CsvTable.cs`）
- **依表頭名稱取值**：先讀第一列表頭（取括號前的名字，不分大小寫），之後 `row.GetFloat("Range")` 這樣取——欄位可以任意重排、中間插新欄、括號裡的說明隨意改。以前 `v[28]` 這種寫死索引的讀法已全面淘汰（RecipeTable／WeaponTable／GemTable／GroundEffectTable／VfxTable 五張表都換了）。
- 空白＝fallback、不丟例外；表頭缺必要欄或重複會進 `Errors` 由呼叫端 `LogError`；表頭有程式不認得的欄名會 `LogWarning` 列出（抓打錯字）。
- 純 C#、不依賴 UnityEngine，可用一般 C# 編譯器跑單元測試（這次大改就是這樣驗的）。

### 規格表（`WeaponModeSpec`，`Assets/Scripts/Weapon/WeaponModeSpec.cs`）——單一真相
每個欄位的型別／預設／範圍／分組／顯示名，以及**每種 `WeaponMode` 吃哪些欄、哪些必填、欄位在該模式叫什麼**，全寫在這一個檔。三處共用：
1. **載入檢查**（`RecipeManager`／`WeaponManager`）：無效欄有值 → Warning；必填缺 → Error。
2. **能力珠有效性**（`PlayerAbilities`／鍛造介面）——見 [GEM_SOCKET.md](GEM_SOCKET.md)。
3. **武器效果模擬系統**（之後做）：選了 Mode 就知道要顯示哪些欄、產什麼輸入框。

**加新欄或新模式只改這個檔**（`BuildFields` 加欄、`BuildModes` 加模式）＋ `RecipeEntry.FromFields` 讀它 ＋ 發射分支用它。

### `RecipeEntry.FromFields`（`Assets/Scripts/Weapon/RecipeEntry.cs`）——唯一的建構入口
「欄名 → 原始字串」進、`RecipeEntry` 出。CSV 走它、之後的模擬面板也走它。**對該模式無效的欄在這裡就不讀**（不只是警告），所以表上填錯不可能改變行為。
彈道系統看得懂的欄位住 `Data`（`ProjectileData`：Speed／Radius／LifeTime／FireInterval／RotationSpeed／PierceCount／Bounce／Homing／Split／Orbital／Parabolic（`FlightTime`）／Laser（`DotInterval`、`BeamRange`＝CSV 的 `Range`）／TrailStep）；主遊戲側自己結算的欄位直接掛在 `RecipeEntry`（`Mode`／`BounceTarget`／`AreaRadius`／`ChainCount`／`ChainRadius`／`AimConeAngle`／`SnapRadius`／`SegmentedColumn`／`GroundEffectID`／`GroundEffectHitTarget`／`SubWeaponOnHit`／`Summon*`／`MeleeAngle`／`Dash*`／`ChargeMode`／`ChargeTimeReduction`／`LaunchSource`／`BlockedByEnvironment`）。
`ProjectileData.IsOrbital／IsParabolic／IsLaser` 是彈道套件內部的組裝旗標，由 `Mode` 推導，主遊戲不要直接讀它們判模式——一律 `recipe.Mode == WeaponMode.Xxx`。

### `BounceTarget` 映射
`PlayerController` 在發射時將語意值轉為 `NonBounceLayers`：`Environment` → `EnemyLayer`；`Enemy` → `EnvLayer`；`None` → `EnvLayer | EnemyLayer`。

## 武器表 (WeaponTable.csv)

`PixelBeamSet` 控制貼圖式雷射外觀：留空沿用 shader 雷射，`A_Blue` 使用 Pack 4 A 組藍色 origin／center／impact 動畫。詳見 [PIXEL_REFLECT_LASER.md](PIXEL_REFLECT_LASER.md)。
定義武器的遊戲屬性，存放於 `Assets/Data/WeaponTable.csv`。2026-08-26 起表頭依用途分群（通用／子彈外觀／光束外觀／特效 ID），欄名不變、依名字讀。

| CSV 欄位 | 說明 |
|----------|------|
| `ID` | 武器唯一識別碼 |
| `Name` | 武器名稱 |
| `Damage` | 傷害數值 |
| `RecipeID` | 對應配方表的 ID |
| `WeaponSpritePath` | 武器圖檔路徑（相對於 `Assets/Resources/`，不含副檔名），與 `WeaponAniPath` 二擇一 |
| `SpriteAngleOffset` | 圖片角度偏移（度），見下方說明 |
| `WeaponAniPath` | 序列圖路徑前綴（相對於 `Assets/Resources/`，不含編號與副檔名），有值時忽略 `WeaponSpritePath` |
| `WeaponAniNumber` | 序列圖張數，系統自動載入 `{WeaponAniPath}_01` ~ `{WeaponAniPath}_NN` |
| `AnimFPS` | 序列圖播放速度（幀/秒），例如 12 = 每秒 12 幀 |
| `BulletScale` | 子彈縮放倍率，預設為 1（留空等同 1），例如 2 = 放大兩倍 |
| `BeamStyle` | 雷射**種類編號** 1~10（外型一包預設，見 [LASER.md](LASER.md)）：1鏡光 2標準 3脈衝 4離子 5電漿 6虛線 7閃電 8針狀 9洪流 10微光；留空 = 2 |
| `BeamColor` | 雷射**顏色編號** 1~10：1紅 2橙 3黃 4綠 5青 6藍 7紫 8洋紅 9白 10琥珀金；留空 = 9（白） |
| `BeamWidth` | 雷射粗細（**視覺與命中判定共用此欄**，所見即所得）；留空 = 0.5 |
| `FireEffectID` | 發射特效 ID（引用 `VfxTable`）：發射時在玩家身上播一次、朝瞄準方向；留空 / 0 = 不觸發。見 [VFX.md](VFX.md) |
| `HitEffectID` | 擊中特效 ID（引用 `VfxTable`）：子彈／光束命中怪物、障礙物、拋物線落地時在命中點播一次；留空 / 0 = 不觸發。見 [VFX.md](VFX.md) |
| `TrailEffectID` | 軌跡特效 ID（引用 `VfxTable`）：沿子彈飛行路徑每隔配方的 `TrailStep` 距離種一個（**地刺類武器**靠這個沿路長出尖刺）；留空 / 0 = 不觸發。見 [VFX.md](VFX.md) |
| `SummonEffectID` | 召喚特效 ID（引用 `VfxTable`）：**召喚型武器**（`Mode=Summon`）施放時在**每個生怪點**播一次，怪物**同一幀一起出現**（邊播特效邊出現）；留空 / 0 = 不播、無特效直接生怪。見 [VFX.md](VFX.md)、[BOSS_MODULE.md](BOSS_MODULE.md) |

## 序列圖動畫設定說明

武器可以使用多張 PNG 序列圖讓飛行外觀產生動畫效果。

**使用方式**：
1. 將序列圖片放入 `Assets/Resources/` 下的對應目錄，檔名依序為 `{前綴}_01.png`、`{前綴}_02.png`、...
2. 在 CSV 中填寫 `WeaponAniPath`（路徑前綴，不含 `_01` 編號與副檔名）、`WeaponAniNumber`（張數）、`AnimFPS`（播放速度）
3. 此時 `WeaponSpritePath` 可留空，系統會使用序列圖的第一幀作為初始顯示

**範例**：
* `WeaponAniPath = Weapon/weapon_fire`、`WeaponAniNumber = 6`、`AnimFPS = 12`
* 系統載入：`Weapon/weapon_fire_01` ~ `Weapon/weapon_fire_06`，以每秒 12 幀循環播放
* 分裂子彈自動繼承動畫設定

**二擇一規則**：有 `WeaponAniPath` 就用動畫，沒有就用 `WeaponSpritePath` 靜態圖片。

## SpriteAngleOffset 設定說明

武器圖片在飛行時需要朝向正確的方向。`SpriteAngleOffset` 用來補償圖片原始角度與飛行方向之間的差異。

**設定方式**：
1. 想像這張圖片從畫面**正左方射向正右方**（→ 方向）
2. 此時圖片的「攻擊端」（例如劍尖）需要朝右
3. 如果原始圖片的攻擊端不是朝右，就需要旋轉一個角度讓它朝右
4. 這個旋轉角度就是 `SpriteAngleOffset`

**角度方向**（Unity 2D 標準）：
* 正值 = 逆時針旋轉
* 負值 = 順時針旋轉

**範例**：
* 圖片的劍尖原本朝右上 45° → `SpriteAngleOffset = -45`（順時針轉 45° 讓劍尖朝右）
* 圖片的劍尖原本朝上 → `SpriteAngleOffset = -90`
* 圖片本身就朝右（如水平飛彈）→ `SpriteAngleOffset = 0`

設定完成後，無論玩家往哪個方向射擊，武器圖片都會自動旋轉到正確角度，攻擊端永遠指向飛行方向。分裂子彈也會自動繼承此設定。

## RecipeManager
* 在 `Awake()` 時用 `CsvTable` 載入所有配方，每列交給 `RecipeEntry.FromFields`，問題（[Error]/[Warning]）逐條印到 Console（含行號）。
* 二次解析 `SubRecipeID`，將 ID 解析為 `ProjectileData` 物件引用；會分裂但沒指定子配方的自動補一份「繼承母彈、不再分裂」。
* `All`（唯讀字典）與 `CreateTransient(fields, problems)`（用欄名字典臨時建一列、不登記）是給武器效果模擬面板的入口。

## WeaponManager
* 在 `Start()` 時用 `CsvTable` 載入所有武器（`BuildWeapon` 依欄名取值），透過 `RecipeManager` 解析 `RecipeID` 為 `RecipeEntry` 引用；武器表欄位也過 `WeaponModeSpec.Validate`（例：召喚武器填了子彈圖會 Warning）。
* **`SimulationOverride`**：不為 null 時 `GetCurrentWeapon()` 一律回它（**也會過 `AbilityResolver`**，所以真鑲的珠子對模擬武器有效）——[武器工坊](WEAPON_WORKBENCH.md) 靠這個讓所有發射路徑打模擬武器；`All` 與 `CreateTransient` 同 RecipeManager。
* 使用 `PrefabMapping` 序列化列表模式（與 `MonsterSpawner` 一致），在 Inspector 中拖入子彈 Prefab。
* 雷射／連鎖／非分段落雷武器額外載入光束素材，`BeamStyle` / `BeamColor` 編號透過 `BeamStyleLibrary` 解析為外型參數與顏色（見 [LASER.md](LASER.md)）。
* **初始沒有武器**：`Start()` 只載表、**不指派任何初始武器**（`CurrentWeaponID = 0` ＝ 無武器，`GetCurrentWeapon()` 回 null）。實際武器由 `PlayerController` 依背包武器欄呼叫 `SwitchWeapon` 決定。（2026-07-27 改；此前這裡會強制設成最高 ID，導致玩家空手也能攻擊，且會蓋掉背包指定的武器。）
* **沒有循環切換**：`SwitchToPreviousWeapon()`（E 鍵）已於 2026-07-27 移除——武器一律由背包武器欄決定，不再有繞過裝備的切換途徑。`WeaponData` 的欄位另含 `FireEffectID` / `HitEffectID` / `TrailEffectID`（引用 VfxTable，見 [VFX.md](VFX.md)）。

## BounceTarget 映射邏輯
`PlayerController` 在發射時將 `BounceTarget` 語意值轉為 `NonBounceLayers`：
* `Environment`（反彈障礙物）→ `NonBounceLayers = EnemyLayer`
* `Enemy`（反彈怪物）→ `NonBounceLayers = EnvLayer`
* `None`（不反彈）→ `NonBounceLayers = EnvLayer | EnemyLayer`
