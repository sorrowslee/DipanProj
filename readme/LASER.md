# 雷射系統 (Laser Beam ＋ Appearance / BeamStyle)

> 返回 [文件總覽](README.md)

持續掃射型雷射武器。與「會飛的子彈」本質不同——它是「一條當下就存在的線」，不適用 `BulletInstance`（會移動的點）與 `IBulletBehavior`（為移動設計），因此**新增獨立的 `LaserBeam` 核心元件**，與 `BulletInstance` 平級。設計上仍嚴守解耦邊界：**彈道系統只管幾何 + 渲染 + 回報命中，絕不結算傷害**。

本文分兩半：前半是**光束行為／渲染**（LaserBeam 元件、主遊戲串接），後半是**外型系統**（BeamStyle 種類化、如何加第 11 種）。

---

## 一、LaserBeam（彈道系統，`BallisticsSystem/Runtime/Core/LaserBeam.cs`）
* **line-march（逐段行進）**：每幀從砲口沿瞄準方向逐段行進，把**追蹤（彎曲）、反彈（折線）、穿透、射程**收斂進同一個迴圈產出折線頂點。
  * **牆與敵人分開偵測（關鍵）**：牆（阻擋/反彈層）用**細射線 `Physics2D.Raycast`**（精準、掠射角與薄牆都抓得到、命中點正好在牆面）；敵人（可穿透層）用**粗圓 `CircleCastAll`**（吃滿光束寬度）。早期「厚圓掃描在掠射角漏抓薄牆 → 光束穿牆飛出場外」就是只用一種 cast 造成，分開後根治。
  * 追蹤：每步朝最近敵人轉向（`HomingTurnSpeed` 以名目光速換算曲率，手感與追蹤彈一致），起始朝滑鼠、中段咬敵；已記錄過的目標不再吸引轉向，光束自然彎向下一個敵人。
  * 反彈：命中可反彈表面 `Vector2.Reflect`，光束變多段折線。
  * 穿透：`PierceCount` 控制穿過幾個敵人；`-1` 無限、`0` 打到第一個就停。
  * 直射用單次大段 cast（快路徑），追蹤才用細步行進（精度路徑）。
* **貼身敵人補抓（重要陷阱）**：本專案 `Physics2D.queriesStartInColliders = false`，從砲口出發的 cast 會**忽略重疊在光束起點的碰撞體** → 怪物貼在玩家身上時雷射打不到（飛劍類因為是「從外面飛進來」不受影響）。`MarchBeam` 開頭多做一次 `OverlapCircle`（半徑＝光束半寬）把貼身怪補進命中清單（純記錄傷害、不擋光束、**不動全域物理設定**以免影響子彈系統）。
* **所見即所得的命中寬度**：命中判定半徑 `Radius = BeamWidth / 2`（不再讀配方的 `Radius`），**視覺寬度與命中寬度共用同一欄**，避免兩個欄位互相打架。
* **渲染＝自繪 mesh（刻意不用 LineRenderer）**：每段做成**獨立四邊形**，頂點位置全由程式指定。
  * **為何放棄 LineRenderer**：它的轉角接縫對「厚光束 + 尖反彈」無解——開圓角會把接縫往內縮（看起來離牆很遠就反彈），關圓角又會在尖角把某段寬度擠扁（變細）；兩個 artifact 只能二選一，怎麼調都是修 A 壞 B。
  * **反彈貼牆 + 全程等寬**：轉角頂點放在精準的牆面命中點，相鄰段再**依轉角大小**沿各自方向延伸重疊（直段/追蹤緩彎幾乎不延伸、不會過度重疊變亮；尖反彈才補滿半寬、且延伸量落在畫面外）。每段都是等寬矩形 → 不會再有變細段；亮核確實補到牆面 → 緊貼牆反彈。
  * **頭尾**：端點不延伸（平頭），由砲口 / 命中光暈（SpriteRenderer，`laser_glow` / `laser_impact`）收尾，避免亮核凸出端點節點。
  * mesh 頂點用世界座標，故 `LaserBeam` 物件 transform 歸零（光暈為子物件，每幀用世界 `position` 更新）。
* **雷射質感＝全參數化 shader（`Custom/AdditiveBeam`，不需貼圖）**：純黑底 `Blend One One` 自動去背發光。外型由一組數字（= 一種 `BeamStyle`）驅動，10 種定義見下方〈二、雷射外型系統〉。
  * **縱向能量波**：shader 依 `uv.x`（沿光束世界長度）生成波帶、依 `_Time` 流動 → 一波一波往前（`BandFreq=0` 即均勻無波 = 鏡光）。電漿/閃電再疊 1D 程序雜訊。
  * **白熱核心 + 微脈動**：依橫向 `uv.y` 讓中心趨白、邊緣由 `BeamColor` 染色（**顏色與亮度分離**，波動在亮核也看得到），再加整體微脈動。圓形光暈改用另一支簡單加色 shader `Custom/AdditiveGlow`（避免光束的波帶/截面/白核套到圓暈上）。
* **傷害回報**：自帶 `DotInterval` 計時器，每隔 N 秒透過 `Action<LaserBeam, List<BeamHit>> OnBeamDamageTick` 把「當下掃到的目標」回報給主遊戲，傷害完全由主遊戲結算。
* `BallisticsEngine.SpawnBeam(...)`：純程式生成光束物件的工廠（`RequireComponent` 在 `AddComponent<LaserBeam>` 時自動補上 `MeshFilter` / `MeshRenderer`），傳入 mask + 外型參數(BeamStyle) + onTick callback。**全程純程式建構，不需要 prefab、不需要 Inspector 接線**。

### 主遊戲端（`PlayerController`）
* **持續光束生命週期**：偵測到雷射武器時走 `UpdateLaser`——按住維持一組（依 `SpreadCount` 扇形）光束、每幀更新砲口位置與瞄準方向；放開 / 切武器時 `ClearActiveBeams` 銷毀整組（仿環繞彈的群組管理）。
* **`HandleBeamTick`**：接收光束每 `DotInterval` 的命中回報，對怪物用 `WeaponData.Damage` 結算傷害（無敵時間由 `MonsterController.TakeDamage` 內部的 `HitReactionHandler` 自動處理），並觸發地面特效。
* **OnHit 分裂**：`SplitTiming=OnHit` + `SubRecipeID` 時，在命中點生成子彈，**節流綁在 DotInterval tick**（避免每幀爆量）。
* **外觀資料驅動**：行為在 `RecipeTable`、外觀在 `WeaponTable`——使用者只填 `BeamStyle`（種類編號）/ `BeamColor`（顏色編號）/ `BeamWidth`（粗細）三個簡單值，其餘外型細節由 `BeamStyle` 編號展開。換風格只改編號，程式與配方都不動。

### 互斥與不生效欄位
* `IsLaser` 與 `IsOrbital` / `IsParabolic` 三者互斥。
* `Speed` / `LifeTime` / `FireInterval`（持續光束無發射節奏）、`RotationSpeed`、`SplitTiming=OnSpawn/OnDeath` 對雷射不生效（填了不報錯）。

### 火焰噴射器（火焰外觀模式：雷射的 TrailEffectID > 0）
火焰噴射器 = **一把雷射，但外觀換成火焰 sprite**。因為「按住噴、跟滑鼠掃、持續傷害」本質就是雷射（`Origin`/`AimDirection` 每幀更新 → 完美掃射；`OnBeamDamageTick` → 持續 DOT），只差視覺。

* **啟用**：雷射武器（`IsLaser=1`）的 `WeaponTable.TrailEffectID > 0` → 進入火焰模式。
  * `LaserBeam.DrawBeam` 設為 `false`：**不畫光束 mesh、不畫砲口/命中光暈**，但照常算路徑(`Points`)、命中、DOT、射程、撞牆停。
  * 主遊戲（`PlayerController.UpdateFlameColumn`）沿 `beam.Points` 每隔配方 `TrailStep` 鋪一根 **TrailEffectID 指向的火焰 Vfx**，每幀重新定位 → 火焰柱跟著光束掃。
* **火焰 Vfx 必須是「持續循環」**：VfxTable 該特效設 `Loop=1` + **`Duration=-1`（無限循環，由 PlayerController 持有、放開/切武器時清除）**。不能用一次性（會自毀、無法被重新定位）。
* **復用既有欄位**：`TrailEffectID`（火焰外觀，同地刺）/ `TrailStep`（火焰間距）/ `BeamWidth`（命中寬度）/ `dotInterval`（DOT 節拍）/ `BeamRange`（火焰射程，建議短，例如 5）/ `PierceCount`（穿幾個敵人）。傷害走武器表 `Damage`，每 `dotInterval` 結算。
* **掃射 / 反彈 / 追蹤全部免費**：因為它就是雷射——`BounceTarget` 讓火焰撞牆折射、`HomingTurnSpeed` 讓火焰彎曲咬敵。
* 範例：武器「火焰噴射器」`IsLaser=1, TrailEffectID=4(火球), BeamWidth=1.2, Damage=2`；配方 `dotInterval=0.2, BeamRange=5, TrailStep=0.5, PierceCount=-1`；Vfx 4「火球」`FireBall, Loop=1, Duration=-1, Scale=0.5`。
* 邊界：火焰 Vfx 的生成/管理全在主遊戲（VfxManager），彈道只多開放 `LaserBeam.Points`（唯讀路徑）+ `DrawBeam` 旗標，不碰 Vfx。

---

## 二、雷射外型系統 (Appearance / BeamStyle)

雷射外型**完全參數化**——一種「雷射種類」= 一組數字（送進 `Custom/AdditiveBeam` shader），**不需要任何貼圖**。所以加新種類 = 多一組數字，秒做、零產圖。使用者端只填三個簡單值：`BeamStyle`（種類編號）/ `BeamColor`（顏色編號）/ `BeamWidth`（粗細）。

### 外型的四層拆解
| 層 | 控制 | 來源 |
|---|---|---|
| 顏色 | `BeamColor` 編號 → 調色盤 | WeaponTable（使用者填） |
| 粗細 | `BeamWidth`（視覺＝命中） | WeaponTable（使用者填） |
| 縱向圖樣 + 流動 + 質感 | `BeamStyle` 編號 → 一組 shader 參數 | WeaponTable 填編號，數字定義在 `BeamStyleLibrary` |
| 長度（行為） | `BeamRange` | RecipeTable |

### BeamStyle 參數（`Sorrows.Ballistics.BeamStyle`，對應 shader 欄位）
| 參數 | 視覺意義 |
|---|---|
| `Intensity` | 整體亮度 |
| `EdgeStart` | 截面實心比例（大=實心粗光、小=核心集中邊緣柔） |
| `CoreWidth` / `CoreWhiteness` | 白熱核心寬度 / 趨白程度（白核 0 → 純色不發白，像鏡光） |
| `FlowSpeed` | 波帶流動速度（0=靜止） |
| `BandFreq` | 波帶密度（每世界單位幾個波；**0=均勻無波=鏡光**） |
| `BandDepth` | 波帶明暗深度（0=均勻、1=明暗到底） |
| `BandSharp` | 波帶銳利度（1=平滑波、高=能量包、更高≈虛線） |
| `NoiseAmt` / `NoiseSpeed` | 1D 程序雜訊量 / 翻騰速度（電漿、閃電才開） |
| `FlickerStrength` / `FlickerSpeed` | 整體脈動（呼吸）幅度 / 速度 |

### 內建 10 種種類（定義在 `Assets/Scripts/Weapon/BeamStyleLibrary.cs` 的 `Get()`）
1 鏡光（古鏡，平滑均勻無波）｜2 標準｜3 脈衝｜4 離子/實心｜5 電漿｜6 虛線/節段｜7 閃電/不穩｜8 針狀/狙擊｜9 能量洪流｜10 微光/柔。

### 內建 10 色（`BeamStyleLibrary.Palette`）
1 紅｜2 橙｜3 黃｜4 綠｜5 青｜6 藍｜7 紫｜8 洋紅｜9 白｜10 琥珀金。`BeamStyle` 與 `BeamColor` **正交**（任何種類可配任何顏色 → 10×10 組合）。

### 接手 AI 必讀：如何新增第 11、12… 種雷射
1. 打開 `BeamStyleLibrary.cs`，在 `Get()` 的 switch 多一個 `case 11:`（複製最接近的那組、改數字；參數意義見上表）。
2. 在 `WeaponTable.csv` 表頭說明列的 `BeamStyle(...)` 補上「11xxx」名稱（純文件、給人看）。
3. 顏色同理：在 `BeamStyleLibrary.Palette` 末端加一筆 hex。
4. **不需要**產圖、**不需要**動 shader（除非要全新視覺機制）、**不需要**動彈道系統。
5. 只有當需要「很有機/不規則」的質感（程序雜訊也做不出來）時，才考慮替該種類掛一張選用貼圖——但目前架構刻意全程序化以免產圖。

> 渲染管線：Built-in RP，shader 以 `_Time` 做流動/脈動。光束本體走 `Custom/AdditiveBeam`（全參數化），圓形光暈走 `Custom/AdditiveGlow`（單純加色染色）。
