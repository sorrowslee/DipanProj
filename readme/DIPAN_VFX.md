# DipanVfx——自製 shader 特效套件（主遊戲＋地圖編輯器共用）

> 返回 [文件總覽](README.md)｜地刺細節見 [GROUND_SPIKE.md](GROUND_SPIKE.md)｜序列圖特效見 [VFX.md](VFX.md)｜素材庫預覽器見 [EFFECT_LIBRARY.md](EFFECT_LIBRARY.md)
>
> 2026-11-18 建立。作者覺得火焰環繞彈的火、火焰拋擲彈落地的火很難看，要求用「shader＋幾張圖」重做；
> 同時要求**以後 AI 做的 shader 特效（含之前的地刺）都整合進地圖編輯器的特效預覽器**，統一觀看與調整。

---

## 1. 這是什麼、為什麼是套件

`DipanVfx/`（repo 根目錄，跟 `BallisticsSystem/` 並排、同一個 git）是一個 Unity 本地套件 `com.sorrows.dipanvfx`，
**主遊戲與地圖編輯器的 `Packages/manifest.json` 都用 `file:../../DipanVfx` 參照它**。

特效的 shader 與演出程式只有這一份：

- 主遊戲用它播特效。
- 地圖編輯器的預覽器用**同一份程式**播給作者看。

所以預覽器看到的就是遊戲裡的樣子，不會有兩份程式各改各的。

**鐵則：套件裡的程式不能引用主遊戲的類別**（`PlayerController`、`VfxManager`、`MapDepthSort`、`WeaponData`…；它在自己的 asmdef 裡，引用了也編不過）。
套件裡只放「給位置、方向、大小就自己播完」的**純畫面**；什麼時候生、傷害怎麼算，留在主遊戲呼叫端。
這跟彈道系統「不算傷害」是同一種分法（[ARCHITECTURE.md](ARCHITECTURE.md)）。

兩個專案的差異收在 `VfxAssets` 的兩個接口：

| 接口 | 主遊戲 | 地圖編輯器 |
|---|---|---|
| `VfxAssets.SpriteLoader`（取圖） | 留空＝`Resources.Load<Sprite>` | `CustomVfxSource.LoadSprite`：直接讀 `DipanProj_Main/Assets/Resources/<路徑>.png`，pivot／PPU 讀同名 `.meta` |
| `VfxAssets.SortOrderForY`（立著的東西怎麼排） | `VfxManager.Awake` 填 `MapDepthSort.Order(y, 0)`＝跟角色一起 Y 排序 | 留空（舞台上沒有角色） |

設定表一律住主遊戲 `Assets/Data/`。主遊戲由 `VfxManager` 的 TextAsset 欄載入；編輯器直接讀寫那份檔案。

---

## 2. 檔案

| 位置 | 內容 |
|---|---|
| `DipanVfx/package.json`、`Runtime/DipanVfx.Runtime.asmdef` | 套件本體（rootNamespace `Dipan.Vfx`，autoReferenced） |
| `Runtime/VfxAssets.cs` | 接口（上表）、共用材質、程序貼圖（可平鋪噪聲、柔邊圓點）、`ParseColor` |
| `Runtime/Data/CsvTable.cs`、`CsvUtil.cs` | **從主遊戲搬來**（純 C#、namespace `Dipan.Data` 不變、.meta 一起搬 GUID 不變）——套件讀表要用；主遊戲照用不必改 |
| `Runtime/GroundSpike/` | 地刺（2026-11-17 做、11-18 搬進來，namespace 改 `Dipan.Vfx`），見 [GROUND_SPIKE.md](GROUND_SPIKE.md) |
| `Runtime/Flame/FlameData.cs`、`FlameVisual.cs`、`FlameHost.cs` | 火焰（§3） |
| `Runtime/GroundFire/GroundFireData.cs`、`GroundFireBurst.cs` | 地上燃燒（§4） |
| `Runtime/Explosion/ExplosionData.cs`、`ExplosionBurst.cs` | 爆炸（§4b） |
| `Runtime/Teleport/TeleportFx.cs` | 傳送點外型（`TeleportFxData`／`TeleportFxDatabase`／`TeleportMarkerFx`，§4c） |
| `Runtime/Beam/BeamFxData.cs`、`BeamVisual.cs` | 光束外觀（雷射／連鎖閃電／掃射，§4d） |
| `Runtime/VfxEmbers.cs` | 火星粒子（一個元件管全部，上限 400 顆） |
| `Runtime/Resources/Shaders/` | `GroundSpike`、`DipanFlame`、`DipanScorch`、`DipanGlowDot`、`DipanFireball`、`DipanShockRing`、`DipanPortalPool`、`DipanLightBeam`、`DipanBeam`、`DipanArc`、`DipanBeamOrb`（放 Resources 底下 ⇒ 打包一定帶進去、`Shader.Find` 找得到） |
| 主遊戲 `Assets/Data/FlameTable.csv`、`GroundFireTable.csv`、`GroundSpikeTable.csv`、`ExplosionTable.csv`、`TeleportFxTable.csv`、`BeamFxTable.csv` | 設定表（MainScene 的 VfxManager 六欄都已拖好） |
| 主遊戲 `Resources/VfxEffects/Explosion/fireball_1~6.png` | 爆炸火球雲（白色＋alpha＝密度，置中、Full Rect、不壓縮），火球與黑煙共用 |
| 主遊戲 `Resources/VfxEffects/Flame/flame_1~6.png` | 火焰形狀（白色＋alpha＝熱度，pivot＝根部 `(0.5, 0.075)`、Full Rect、不壓縮） |
| 主遊戲 `Resources/VfxEffects/GroundFire/scorch.png`、`scorch_glow.png` | 焦土本體（RGB＋alpha）／裂紋餘燼遮罩（只用 alpha），同尺寸 512 |
| 地圖編輯器 `Scripts/Preview/CustomVfxSource.cs`、`CustomVfxPreviewRig.cs`、`Scripts/UI/CustomVfxPanel.cs` | 預覽器「自製特效」分頁（§6） |

---

## 3. 火焰（FlameTable）

**一張靜態圖就能燒**——`Custom/DipanFlame` 做這些事：

1. 火焰圖只用 alpha 當「熱度」。
2. 兩層往上捲動的噪聲扭曲取樣位置，越往上扭越多 ⇒ 火舌一直流動，沒有序列圖一幀一幀跳的問題。
3. 同一組噪聲把熱度「侵蝕」掉一塊塊 ⇒ 火舌尖端會斷開、忽長忽短。
4. 熱度對應三段色帶（外緣→中段→核心）。**同一組圖換三個顏色就是綠火、藍火、血火**，不必重畫。
5. `_Lean` 讓火往一邊倒；`_Ignite` 從根部長出來；`_Fade` 從尖端往下熄。

混合用 premultiplied alpha。`Additive`（0～1）決定「核心發光、外焰遮蓋」的比例：

- 純加色（1）在亮地板上會被洗白，見 PROBLEMS **E11～E13**。
- 預設 0.55。

**`FlameVisual` 兩種用法**：

- `Attach(id, host)`：跟著子彈跑。
  - **火不跟子彈旋轉，永遠往上燒**（舊環繞彈的問題之一就是火被轉成火尖朝玩家）。
  - 改成依移動速度往反方向倒（`LeanPerSpeed`／`LeanMax`）、拉長（`StretchPerSpeed`／`StretchMax`）。
  - 移動時甩出小火（`TrailRate`）、冒火星（`EmberRate`）。
  - 排序＝宿主的 sortingOrder ＋ `SortingOffset`，前後關係跟原本子彈一樣。
  - 宿主被 Destroy 或停用就自己熄滅。
- `Spawn(data, pos, …, life)`：固定位置燒一段時間。地上燃燒的火舌、甩出的小火用這個。

**`FlameHost`**（掛在子彈上的小標記）：彈道系統的分裂子彈是拿母彈 `Instantiate` 出來的，標記會跟著被複製 ⇒ **分裂出來的子彈自己也會燒**，主遊戲不用管分裂發生在哪。

**武器表 `FlameStyleID`**（2026-11-18，在 `TrailSpikeID` 右邊；`WeaponModeSpec` 的 `BulletVisual` 組，凡是會飛子彈的模式都有效）：

- 有填 ⇒ 主遊戲所有生子彈的地方都呼叫 `WeaponCastService.AttachFlame`：一般／環繞／拋物線／重疊分裂／命中迸發／怪物與浮游走的 `FireNormal`，規則一致。
- 大小＝`FlameTable.Size` × 施放者體型 × 武器 `CastVisualScale`（集氣放大會跟著變大）。
- 通常要**把子彈圖留空**（`WeaponSpritePath`／`WeaponAniPath` 都空＝隱形子彈），不然火跟舊圖疊在一起。

| FlameTable 列 | 用在 | 重點設定 |
|---|---|---|
| 1 環繞火團 | 火焰環繞彈（武器 3） | Size 1.3、Anchor 0.45（火包著子彈）、甩小火 10／秒、火星 5／秒 |
| 2 墜火 | 焚天火雨（武器 61） | Size 1.5、亮度 1.5、捲動快 1.8、拉長最多 0.9（墜落像流星）、甩小火 14／秒 |
| 3 地上火舌 | GroundFireTable 1 的火舌 | Size 1、Anchor 0（根部貼地）、起燃 0.35 秒 |

欄位意思都寫在 CSV 表頭括號裡，留空＝預設。

---

## 4. 地上燃燒（GroundFireTable）

**GroundEffectTable 加欄 `FireStyleID`**：

- 填了就**不畫序列圖**，改由 `GroundFireBurst` 畫；傷害照舊由那個 GroundEffect 結算。
- 範圍＝GroundEffect 的 `Radius` × 縮放，時間＝`Duration`。
- 點火在 `GroundEffectInstance` 第一次 Update，才拿得到 `visualScale` 套上去之後的最終大小。
- GroundEffect 被提早砍掉（換圖等）時，`OnDestroy` 會叫火熄。

一團地上燃燒＝三層：

1. **焦土**（`Custom/DipanScorch`）。貼地，排在地面特效層（8）。
   - 起火時從中心往外燒開，邊緣不規則（`IgniteTime`）。
   - 裂紋餘燼一明一暗，明滅相位跟著噪聲走，不會整片同步閃。
   - 結束時餘燼由外往內冷卻（`FadeTime`），火滅後焦痕再留 `ScorchLinger` 秒才淡掉。
2. **火舌**（`FlameVisual`，FlameTable `FlameID` 那一列）。
   - 範圍內散 `CountMin～CountMax` 條（按範圍半徑放大），中心的大、外圈的小。
   - 每條燒 `TongueLife` 秒就換到別處重燃，所以整片火一直在變。
   - **立著、跟角色一起 Y 排序**：角色走進火裡，前後遮擋是對的。
3. **火星**（`VfxEmbers`）：範圍內往上飄。

`GroundFireBurst` 熄滅那段落在 `Duration` 最後 `FadeTime` 秒內，所以畫面跟傷害同時結束；焦痕多留一下，看得出「火剛滅」。

目前 GroundEffect 8（焚地）、9（焚地・大）都填 `FireStyleID` 1。所以這些武器全部換成新火：

- 火焰拋擲彈（配方 12）
- 火焰直線彈（配方 10）
- 8 分裂追蹤彈（配方 11）
- 落星羅盤（配方 82）
- 焚天火雨（配方 61）

---

## 4b. 爆炸（ExplosionTable，2026-11-18）

作者嫌火焰拋擲彈落地的爆炸（VfxTable 1，8 張卡通像素花朵爆炸、最後一幀還跳回第 3 幀）很糟。改成一次爆炸＝五層（`ExplosionBurst`）：

1. **白光**：加色光點瞬間亮一下就收（`FlashSize`／`FlashTime`）。
2. **火球**（`Custom/DipanFireball`）：3～5 團錯開炸開，從 35% 大小 easeOut 膨脹。
   - 依 `_Age` 冷卻：白熱 → 橘 → 暗紅。
   - **外緣比中心先冷成黑煙色**，同時加色比例跟著降，變成會遮住地板的煙。
   - 噪聲由外往內溶掉，邊燒邊往上飄。
3. **黑煙**：火球之後冒出，同一張圖、同一個 shader 的黑煙模式（`_Smoke=1`），慢慢升起散掉。
4. **衝擊波圈**（`Custom/DipanShockRing`，純程序）：貼地（地面特效層 8），從中心擴到**範圍半徑**。
5. **火星**：往外噴（`VfxEmbers`）。

**武器表新欄 `HitBurstID`**（在 `HitEffectID` 右邊，`WeaponModeSpec` 設 universal）：命中／落地時炸一次。

- 拋物線落地傳**真正的傷害半徑**（`AreaRadius × BulletScale`，跟 `TryApplyParabolicBlast` 同一個算式），須彌珠放大也會跟著變大。
- 其他模式用表的 `Radius` × 集氣放大。
- 三階血統擊中特效凌駕武器的規則照舊：打到怪且血統有擊中特效時不炸。
- 每次命中都炸，雷射這類每跳都算命中的模式慎用。

火焰拋擲彈（武器 4）填 1，原本的 `HitEffectID` 1 清空（VfxTable 1 全專案只有它用）。

⚠ **這把的傷害半徑其實是 5**（AreaRadius 2.5 × BulletScale 2）：舊爆炸圖只畫到半徑 2.5，看起來比實際打到的小一半。數值沒動。

- 新爆炸的衝擊波圈畫在 5。
- 火球／黑煙／白光限制在 `MaxVisualRadius`（2.5）以內，大範圍爆炸也不會整個畫面被火蓋住。

圖：ChatGPT 燃燈劫專案同一個對話，**黑底** 6 種翻滾火球雲。切圖時用連通區塊分開（GPT 的寬火球會跨到隔壁格）。

外觀調校（numpy 模擬）：

- 溶解改成前慢後快：`cut = age^1.6 × 1.1`。原本線性的話，age 0.45 就只剩一小團。
- 冷卻成煙的時間提早、外緣更早：`smoothstep(0.2, 0.7, age + (1-dens)×0.45)`。

---

## 4c. 傳送點外型（TeleportFxTable，2026-11-18）

作者覺得傳送點（VfxTable 6，48 幀灰藍色扁平橢圓光盤）不好看，要求外型差不多、但做漂亮一點。這次**全程序、不用圖**：

- **光池**（`Custom/DipanPortalPool`）：畫在全白方塊上，程式壓成橢圓（`Squash`）。
  - 深色池身：像一灘深水，會蓋住地板（`Opacity`）。
  - 發光細邊：亮度沿著邊流動。
  - 漣漪一圈圈往外擴。
  - 緩慢旋轉的漩渦絲。
  - 中心光、外圈柔光暈。
- **中央光柱**（`Custom/DipanLightBeam`）：細核心＋柔光，亮紋往上捲，越高越淡，底邊在池中心。
- **光點**：從池面慢慢升起（`VfxEmbers`）。
- 出現時淡入（`FadeTime`）。**togglePortal 隱藏後再打開也會淡入**（元件 `OnEnable` 重新淡入）。

**接線**：

- `MapLoader.teleportFxStyleId`（預設 1）：TeleportFxTable 有這列就用 `TeleportMarkerFx.Create`，否則退回舊的 `teleportVfxId` 序列圖。
- 回傳的 GameObject 照舊登記進 `TeleportMarkerById`（togglePortal 用 SetActive），照舊掛冷藍 `LightSource`。
- 排序同舊傳送點：地面層 5；光柱 +1、光點 +2。
- 大小對齊舊圖：光池寬 1.75、高寬比 0.38。

**地圖編輯器的「傳送點對位」也改用同一個元件**：`TeleportMarkerPreview` 直接掛 `TeleportMarkerFx`，排在 22000 蓋過地圖物件；勾掉「使用傳送點外型」的照樣畫成 25% 透明。所見即遊戲。

表格沒有新傳送點這列時，編輯器跟遊戲一樣退回序列圖。

外觀先用 numpy 照公式模擬調過：原本中心白一大片、漣漪太搶。改成：

- 中心光縮小。
- 漣漪與漩渦絲改用邊緣色、強度降低。
- 漩渦扭曲加大到 7，看得出旋臂。
- 光池在方塊裡縮到 0.82，留空間給外圈光暈，免得被方塊邊切掉。

---

## 4d. 光束外觀（BeamFxTable，2026-11-18）

作者覺得雷射從做出來就很粗糙：接口（反彈轉角）是一段段四邊形硬疊、光暈是兩張大圖、像素雷射拉起來很廉價；希望有**鏡光／雷射／電光**三種感覺，而且要有「**一直有動力往前推送**、像龜派氣功」的感覺，最好**有雷光繞著氣功柱**。

這次把雷射、連鎖閃電、掃射的外觀**全部**搬進套件，**全程序、不用圖**。舊的 `BeamStyle`／`BeamColor`／`BeamStyleLibrary`、`Custom/AdditiveBeam`、像素雷射（`PixelBeamSet`、`PixelLaserBeamVisual`、`PixelLaserA_Blue` 素材）、`Resources/Laser/` 光暈圖、`LaserBeam` 的渲染與 `BallisticsEngine.SpawnChainVisual` 都拆掉了。`LaserBeam` 現在只算路徑與命中。

### 組成（`BeamVisual`，一個元件畫一條光束）

- **光柱**（`Custom/DipanBeam`）：沿路徑鋪**一條連續帶子**。
  - 緩彎（追蹤彎曲）用斜接，整條沒有接縫。
  - 轉角超過 120°（幾乎折返的反彈）才斷成兩段，各自圓頭收尾、疊在反彈點上——那裡本來就畫反彈光點。一般反彈（120° 以內）也是斜接、整條連續。（原本 70°，寬的鏡光一反彈就斷、兩個圓頭疊出一團白，同日改。）
  - 頭尾是**圓頭**（uv1 的 cap 跟橫向合成半徑），不再平切。
  - 截面：白熱核心＋光柱本體（`Solid` 調實心程度）＋外暈。
  - **往前推的感覺**：流紋（拉長的噪聲）以 `FlowSpeed` 往前流；能量脈衝一團一團以 `PulseSpeed` 往前推，經過處光柱鼓起變亮（`PulseSpacing`／`PulseAmt`）；外鞘噪聲翻騰（`EdgeNoise`）。
  - 鏡光專用：邊緣七彩色散（`Chroma`）、沿光束滑過的強光亮紋（`Shimmer`）。
  - 要整條完全均勻：`StreakAmt`、`EdgeNoise`、`Shimmer`、`Flicker` 都填 0（外暈的噪聲明暗也跟著 `EdgeNoise` 走，≥0.25 時同原本）。
  - `GrowSpeed`：剛發射時光束往前伸長（判定一開始就是全長，只有畫面晚零點幾秒追上）。
- **電弧**（`Custom/DipanArc`），兩種走法：
  - `Helix`（氣功砲的雷光環繞）：一小段一小段電弧（`ArcLength`）繞著光柱轉（`ArcTwist`）、往前竄（`ArcTravel`）、活 `ArcLife` 秒換一段；每段繞的半徑隨機 0.75～1.25 倍，才不會變成規律的彈簧。繞到前面畫在光柱上面、繞到後面畫在光柱底下並變暗（兩個 mesh，排序 ±1）。
  - `Path`（電光／連鎖閃電）：`ArcCount` 條鋸齒從頭連到尾，每秒重抽 `ArcRate` 次；每個節點（連鎖的目標、反彈點）上釘死，中間亂竄；`ArcBranch` 機率長出短分岔。主線最亮，其他條較暗較寬。
- **光球**（`Custom/DipanBeamOrb`）：白熱核心＋光暈＋旋轉放射光芒（`OrbRays`），呼吸（`OrbPulse`）。砲口（`MuzzleSize`）、命中端（`ImpactSize`）、轉角超過 25° 的反彈點（`JointSize`，鏡光再加十字星芒 `OrbStar`）。
- **火花**：命中端往回濺（`VfxEmbers`，`SparkRate`）。

寬度單位：**可見寬＝呼叫端給的寬度（武器 `BeamWidth`，也就是命中寬度）×`WidthMul`**；表裡寫「×半寬」的欄位都乘可見寬的一半。

### 目前五列

| ID | 名稱 | 武器 | 調法重點 |
|---|---|---|---|
| 1 | 鏡光 | 29 鏡界折光 | 「拿一面大鏡子反射出去」的寬光帶：WidthMul 3.2、平頂（Solid 0.9、CoreWidth 0.55、GlowWidth 1.25）、淡（BodyIntensity 0.3）、冷白、淡色散 0.25、**完全不波動**（StreakAmt／Shimmer／EdgeNoise／Flicker 全 0——作者：一塊一塊的波動在別的武器需要、在鏡光怪怪的）、反彈點小十字星芒（JointSize 1.15）、不畫放射光芒。初版金白粗光柱作者覺得不像鏡光，同日改；暖白版＝ColorMid #FFF5E6、ColorEdge #FFD9A8 |
| 2 | 雷射 | 預設（武器沒填 BeamFxID） | 青藍、脈衝間距 1.4 |
| 3 | 電光 | 9 連鎖閃電、66 雷雨天罰 | 光柱只剩淡光暈（BodyIntensity 0.35、Solid 0）、Path 電弧 3 條、分岔 0.18 |
| 4 | 氣功砲 | 5 雷射追蹤光束 | 粗白核、脈衝鼓起 0.25、EdgeNoise 0.45、GrowSpeed 40、Helix 電弧 5 條、命中光球 3 倍寬 |
| 5 | 紅色雷射 | 78 路西法之怒 | 紅、快脈衝、不畫砲口（掃射源點有自己的光球） |

### 接線

- 武器表 `BeamFxID`（留空＝2）＋`BeamWidth`。工坊有下拉。
- **雷射**（`PlayerController.SpawnLaserBeams`）：每道 `LaserBeam` 配一條 `BeamVisual.Create(BeamFxID, 寬)`＋`Track(beam.Points)`。`BeamVisual` 在 LateUpdate 讀路徑 ⇒ 一定是這一幀算好的折線。放開時 `Stop()`，照 `FadeTime` 淡出後自毀。火焰噴射器模式（`TrailEffectID>0`）不建。
- **連鎖閃電**（`RunChain`、打空時）：`BeamVisual.Flash(BeamFxID, 玩家→目標們, 寬, 0.16 秒)`。不再先把折線打成鋸齒（`BuildJaggedPath` 刪了），鋸齒交給電弧。雷雨天罰 66 的落點連鎖走同一條（外觀取 66 自己的 BeamFxID 3）。
- **掃射**（`SweepStrike`）：每道 `BeamVisual.Create`、`ShowMuzzle=false`，每幀 `SetPath(源點→落點, BeamFadeSeconds)`；停餵就自己淡出。光球顏色取該列 `ColorMid`。
- 物件**不放池**，用完自己 Destroy；呼叫端用 Unity 的 `== null` 判斷就好。排序沿用舊雷射：Default 層 22000（電弧後面 −1、前面 +1、光球 +2、火花 +3）。
- `VfxManager.BeamFxCSV`（MainScene 已拖）、`PlayModeStaticReset` 加 `BeamFxDatabase.ResetForPlayMode()`。

### 調參

外觀先用 numpy 照三支 shader 與 `BeamVisual` 的網格／電弧邏輯模擬調過（腳本在 `TempImage/dipanvfx_work/beam_sim/`：`python3 sim_beams.py [mirror laser electric kame red]` → `beams.png`，參數在 `presets.json`，欄名同 BeamFxTable）：

- 鏡光：初版整條白掉 ⇒ 0.85／0.45；作者看了仍覺得太粗太金、鏡光該微弱偏白 ⇒ 改冷白細光；再回饋太細、太快、太亮 ⇒ 寬 ×3、亮紋放慢、亮度 0.3；實機看仍細（截面只有細芯亮、外暈太淡）⇒ 改成平頂寬光帶（見上表），反彈斷開角 70°→120°。
- 氣功砲電弧原本像規律彈簧或三角波 ⇒ 每段半徑隨機、鋸齒 0.28、段長 1.7。

---

## 5. 做一個新的 shader 特效（以後的固定流程）

1. **程式與 shader 放進 `DipanVfx/`**：純畫面、不引用主遊戲類別；取圖一律走 `VfxAssets.LoadSprite`，排序用 `VfxAssets.OrderForY`。
2. **設定表放主遊戲 `Assets/Data/`**：表頭寫「欄名(說明/空=預設)」，留空＝預設。讀表用 `CsvTable`（套件裡就有）。Database 要有 `LoadFromText(string)`，**預覽器即時套用就靠它**。
3. **主遊戲接線**：`VfxManager` 加 TextAsset 欄並在 MainScene 拖好；`PlayModeStaticReset` 加物件池與表的 `ResetForPlayMode`（Domain Reload 是關的）；呼叫端只負責「什麼時候生」。
4. **接進預覽器**：在 `CustomVfxPanel.Tables` 加一項（標題、表名、`Apply`＝Database 的 `LoadFromText`），在 `CustomVfxPreviewRig` 加一個 `Kind` 與它的重播方式。
5. **圖**：照 [CHATGPT_IMAGE_WORKFLOW.md](CHATGPT_IMAGE_WORKFLOW.md) 在 ChatGPT「燃燈劫」專案產。**會發光的東西（火、光、魔法）用純黑底**，程式把亮度轉成 alpha——洋紅底去背會把半透明邊緣染成粉紅。不發光的東西照舊用洋紅底。工作夾 `TempImage/dipanvfx_work/`（產圖處理腳本與 `.meta` 產生器 `mkmeta.py`）。
6. **驗證**：見 §7。

---

## 6. 地圖編輯器：特效預覽器 →「自製特效（shader）」分頁

工具列「特效預覽器」→ 上方分頁切到「自製特效（shader）」。

- **左**：六張表的每一列（地刺／火焰／地上燃燒／爆炸／傳送點／光束）。「重讀表格與圖」＝從磁碟重讀表和 png，會放棄未存的修改。
- **中**：舞台，用遊戲同一份程式與 shader 即時播。
  - 拍的是遠離地圖的角落 `(5000, 5000)`，有 1 單位格線。
  - 「參考人形」高約 1.95＝遊戲角色可見高度。
  - 鏡頭：近／中／遊戲（orthographicSize 2／3／5）。
  - 「亮地板」用來檢查加色在亮處會不會被洗白。
  - 各種類的重播方式：
    - 地刺：模擬一顆看不見的子彈從左往右飛，可調子彈速度與 TrailStep。
    - 火焰：靜止／環繞（可調半徑、速度、顆數）／墜落。
    - 地上燃燒：可調範圍半徑與秒數，白圈＝傷害範圍；燒完停一下再燒。
    - 爆炸：每 2 秒炸一次，可調範圍半徑（預設 5＝火焰拋擲彈），白圈＝傷害範圍。範圍大時把鏡頭切到「遊戲」才看得到整圈。
    - 傳送點：一直擺著；改欄位立刻重建。地圖上的「傳送點對位」也是同一個元件，存回表後切回地圖就看得到。
    - 光束：四種，可調「粗細」（＝武器表 BeamWidth）。
      - 掃動：從人形手上射出、左右慢慢掃，看流紋、脈衝、電弧。
      - 反彈：固定四折的折線，看轉角接得順不順、反彈光點。
      - 連鎖：每 0.5 秒閃一次（0.16 秒，同遊戲）。
      - 重複發射：每 2.2 秒發一次、1.4 秒後放開，看伸長（GrowSpeed）與淡出（FadeTime）。
- **右**：選到那一列的每一欄都能直接改，**改了立刻重播**。
  - 「存回 CSV」寫回 `DipanProj_Main/Assets/Data/<表>.csv`：只改被動過的列，表頭、註解、其他列一字不動，BOM 有無照原檔。
  - 「放棄修改」重讀檔案。

主遊戲那邊 Unity 回到前景會自動重新匯入表；不放心就對那張 CSV 按 Reimport。

---

## 7. 驗證方式（AI 交付前）

- **編譯**：作者 Mac 上沒有可遠端呼叫的 Unity。2026-11-18 的做法：
  1. 請作者把 `/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/Managed` 複製到 `~/Downloads/UnityManaged_2022.3.62f3`（AI 申請讀 Unity 安裝資料夾會被自動核准擋下）。
  2. 把它連同兩個專案 `Library/ScriptAssemblies` 的套件 dll 與所有 `.cs` 打包，送進雲端容器。
  3. 用 .NET 8 SDK 附的 Roslyn（`csc.dll`，`-langversion:9`、mono 4.7.1-api 參考組件），依序編譯：DipanVfx → Ballistics → 主遊戲 → 主遊戲 Editor → 地圖編輯器 → 地圖編輯器 Editor。主遊戲另外用「沒有 UNITY_EDITOR」再編一次，模擬打包。這次全部 0 錯誤。
- **shader 外觀**：Unity 外沒辦法跑 shader。用 numpy 照抄 frag 的公式（噪聲演算法同 `VfxAssets.NoiseTexture`）渲染幾個時間點看形狀與亮度。這次因此把預設亮度 1.6 調成 1.35，核心色帶起點 0.55 調成 0.65（原本核心一大片白）。
- **仍需作者實機看**：大小、亮度、甩火量都是估的。直接在預覽器調好存回即可。

---

## 8. 已知限制

- 火焰圖必須**單獨一張貼圖、不進圖集、Full Rect**（shader 用 UV 0～1）。
- `LeanMax` 上限 0.45；倒太多會被圖的左右邊切掉。
- 預覽器裡地上燃燒的火舌沒有 Y 排序（舞台沒有角色），遊戲裡有。
- 舊序列圖沒有刪（`Weapon/animation/fire/`、`GroundEffect/ScorchedGround/`）：表上的欄位清空或填 0 就會回到舊畫法。
