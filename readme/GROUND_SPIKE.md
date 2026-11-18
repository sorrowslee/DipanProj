# 地刺（從地底刺出的尖刺）

> 返回 [文件總覽](README.md)
>
> 2026-11-17 建立，取代舊的地刺序列圖（`VfxEffects/EarthSpik`、VfxTable 第 3 列，已刪除）。
> 作者要的感覺：像遁地獸那樣，一整排尖刺沿著前進方向從地底刺出來。

## 1. 怎麼運作

- **傷害不在這裡**：仍是那顆看不見的子彈（配方的穿透／反彈／分裂／追蹤全吃）打的傷害；地刺只是畫面。
- 子彈每飛配方 `TrailStep` 的距離，`BulletInstance.OnTrailPoint` → `PlayerController.TrySpawnTrailEffect` →
  武器表 `TrailSpikeID > 0` 時呼叫 `VfxManager.SpawnGroundSpikes` → `GroundSpikeBurst.Spawn` 在當下位置長一組。
  - 同一個呼叫點也照舊處理 `TrailEffectID`（VfxTable 序列圖），兩欄可以同時填。雷射的火焰柱只看 `TrailEffectID`。
- 一組 ＝ `CountMin`～`CountMax` 根刺（在前進方向的垂直方向散開）＋ 腳下一張裂口 ＋ 破土一團揚塵。
- 每根刺的演出：
  1. **刺出**（`RiseTime`）：沿著刺自己的軸從地平線下往上推，easeOutCubic；同時縱向拉長 `Overshoot`，
  2. 到頂後 0.1 秒彈回原長，**停留** `HoldTime`，
  3. **收尾**（`RetractTime`）：`RetractMode=Retract` 縮回地底（先慢後快）；`Fade` 原地淡出。
  - 一叢裡各根錯開 0～`StaggerTime` 秒冒出。
- 方向：圖預設**往右彎**。子彈往左飛時自動左右翻；幾乎垂直飛時隨機翻。傾斜角 `LeanAngle` 朝前進方向倒，上下飛時自動變小。

## 2. 檔案

| 檔案 | 角色 |
|---|---|
| `Assets/Data/GroundSpikeTable.csv` | 設定表（一列一種刺）。拖在 MainScene 的 `VfxManager` → `Ground Spike CSV`（已拖好；編輯器下沒拖也會直接讀資產） |
| `DipanVfx/Runtime/GroundSpike/GroundSpikeData.cs` | 一列的資料 ＋ `GroundSpikeDatabase`（載入、查詢）。**2026-11-18 搬進共用套件 DipanVfx**（namespace `Dipan.Vfx`），見 [DIPAN_VFX.md](DIPAN_VFX.md) |
| `DipanVfx/Runtime/GroundSpike/GroundSpikeBurst.cs` | 一組的演出 ＋ 物件池（用完 `SetActive(false)` 放回去，不 Destroy） |
| `DipanVfx/Runtime/Resources/Shaders/GroundSpike.shader` | `Custom/GroundSpike`：**世界座標的地平線以下不畫**（`_GroundY`）、貼地那段壓暗（`_GroundShade`／`_ShadeHeight`）、淡出（`_Fade`）、整體壓暗（`_Dim`） |
| `Resources/VfxEffects/GroundSpike/` | 圖：`stone_*`（石刺）、`root_*`（樹根刺），各有 `spike1~4`、`crack`、`dust` |
| `Scripts/Combat/VfxManager.cs` | 載入表、`SpawnGroundSpikes` |
| `Scripts/Weapon/WeaponData.cs`／`WeaponManager.cs`／`WeaponModeSpec.cs` | 武器表新欄 `TrailSpikeID`（在 `TrailEffectID` 右邊）；Normal／環繞／拋物線／浮游有效 |

## 3. GroundSpikeTable 欄位（留空＝預設）

| 欄 | 預設 | 說明 |
|---|---|---|
| `ID`／`Name` | — | 武器表 `TrailSpikeID` 指向 `ID` |
| `Sprites` | — | 尖刺圖的 Resources 路徑，`|` 分隔，隨機挑（一叢裡盡量不重複）。**圖的底邊＝地平線、尖端朝上、往右彎** |
| `CountMin`／`CountMax` | 2／3 | 每個點長幾根。**單根的武器填 1／1** |
| `Height` | 1.4 | **最高那張圖**的高度（世界單位，再乘武器 `CastVisualScale`）。同一組圖用同一個縮放，所以矮胖的刺還是比較矮 |
| `HeightJitter` | 0.2 | 每根高度隨機 ±比例 |
| `Spread` | 0.35 | 一叢散開距離（世界單位） |
| `LeanAngle`／`LeanJitter` | 12／8 | 往前進方向傾斜度數／隨機 ±度 |
| `StaggerTime` | 0.06 | 一叢裡各根錯開冒出的最大延遲（秒） |
| `RiseTime` | 0.08 | 刺出秒數 |
| `Overshoot` | 0.08 | 刺出瞬間縱向拉長比例 |
| `HoldTime` | 0.25 | 停留秒數 |
| `RetractMode` | Retract | `Retract`＝縮回地底、`Fade`＝原地淡出 |
| `RetractTime` | 0.15 | 收尾秒數 |
| `CrackSprite`／`CrackSize` | 空／1.2 | 地面裂口（寬度）；空＝不畫 |
| `DustSprite`／`DustSize`／`DustTime` | 空／1.1／0.4 | 揚塵；空＝不畫 |
| `SortingOrder` | 空 | 空＝VfxManager 全域；填小於角色的值畫在腳下（現在兩列都填 5，同舊地刺） |
| `Dim` | 1 | 整體壓暗（暗場景太亮時調低，見 PROBLEMS E11/E12） |

目前兩列：**1 石刺**（地裂刺 6、山崩地裂 65）、**2 樹根刺**（樹妖的枯枝 10003），都填 `Height` 1.5、2～3 根、`Retract`、刺出 0.18 秒、停留 0.3 秒、收回 0.3 秒。

### 3.1「一陣一陣往前刺」的節奏（2026-11-17 作者要求放慢）

往前延伸的速度＝**那顆看不見的子彈的速度**（傷害也是它打的，所以畫面和傷害永遠對得上）；一陣一陣的間隔＝`TrailStep ÷ Speed`。

| 武器（配方） | Speed | TrailStep | 每陣間隔 | 其他 |
|---|---|---|---|---|
| 地裂刺（19） | 20 → **8** | 1.5 → 2 → **1.33** | 0.17 秒 | LifeTime 10 不動（本來就是撞牆才停） |
| 樹妖的枯枝（10003） | 20 → **8** | 1.5 → 2 → **1.33** | 0.17 秒 | 同上 |
| 山崩地裂（65） | 15 → **6** | 1 → 2 → **1.33** | 0.22 秒 | LifeTime 0.8 → **2**，射程維持 12 |

（同日作者再要求「兩根刺之間縮短 1/3」：TrailStep 2 → 1.33。）每一陣從冒出到收完約 0.8 秒，所以畫面上同時會看到 4～5 陣在不同階段（剛冒出／全長／縮回），形成往前推進的波浪。
想要更「一頓一頓」：`TrailStep` 再拉大（間隔變長、每陣更分明）；想要更連續：調小。

## 4. 調手感

> **2026-11-18 起直接在地圖編輯器調**：特效預覽器 →「自製特效（shader）」→ 選石刺／樹根刺，右邊改欄位立刻重播，滿意按「存回 CSV」（見 [DIPAN_VFX.md](DIPAN_VFX.md) §6）。

- 看起來太小／太大：`Height`、`CrackSize`、`DustSize`；整把武器一起放大用武器表 `CastVisualScale`。
- 一排不夠密：配方的 `TrailStep`（地刺 1.5、山崩地裂 1、枯枝 1.5）調小，或 `Spread` 調大。
- 力道不夠：`RiseTime` 調短、`Overshoot` 調大。
- 縮回去不好看：`RetractMode` 改 `Fade`。

## 5. 新增一種刺

1. 圖：照 [CHATGPT_IMAGE_WORKFLOW.md](CHATGPT_IMAGE_WORKFLOW.md)，3×2 圖示表（4 根刺＋裂口＋揚塵）、**洋紅底**、附場景圖＋主角圖當風格錨點；
   prompt 要寫「每根刺底部是乾淨的水平切口、尖端朝上、往右彎、不要地面陰影」。工作檔在 `TempImage/ground_spike/`（`cut_magenta.py` 去背、底部切齊）。
2. 放進 `Resources/VfxEffects/GroundSpike/`，加一列表、武器表填 `TrailSpikeID`。

## 6. 已知限制

- 沒有在 Unity 實機看過（2026-11-17 寫完時沒有編譯器）——大小、時間全部是估的，要作者 Play 後調表。（2026-11-18 已用 Roslyn 編譯通過，見 DIPAN_VFX §7）
- 刺的水平置中用的是圖的外框中心，彎得很厲害的圖根部可能偏一點點。
- 榕樹妖自己的地刺攻擊（VfxTable 11、`VfxEffects/EarthSpik2`）**不是**這個系統，沒動。
