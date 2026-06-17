# 目前進度 (Current Progress)

> 返回 [文件總覽](README.md) ｜ 這是「做過什麼、怎麼演進」的變更日誌；各系統的「現狀說明」請看對應主題文件。

* [x] 確立無限恐怖風格的 2D 世界觀與隧道設定。
* [x] 完成主遊戲與彈道系統的模組解耦，建立明確邊界規範。
* [x] 實作 CSV 資料驅動的子彈配方系統（支援反彈、扇形分裂、穿透、自轉）。
* [x] 解決子彈高頻率生成時的事件訂閱同步問題（Pre-subscribe 模式）。
* [x] 解決子彈起點在 Collider 內部時偵測不到的問題（CheckSpawnOverlap）。
* [x] 修正彈道系統所有硬編碼 Layer 編號，改由主遊戲傳入 LayerMask。
* [x] 實作怪物基礎追擊 AI，完成「射擊 → 命中 → 扣血 → 死亡」的完整 Core Loop。
* [x] 優化 MonsterSensor，快取玩家參考，移除每幀 FindGameObjectWithTag 的效能開銷。
* [x] 規劃 Physics Layer Collision Matrix，解決怪物互卡、怪物推擠玩家的問題。
* [x] 規劃並建立「主資源包 + 場景模組包」的美術目錄架構，完成教學場景地磚的 Tilemap 基礎設定。
* [x] 資料驅動的怪物生成系統（CSV 讀取，動態生成對應數值怪物）。
* [x] 完成 CSV 驅動的配方與武器雙表系統（RecipeTable + WeaponTable），取代 ScriptableObject 配方。
* [x] 實作 RecipeManager（配方載入、SubRecipeID 二次解析、BounceTarget 語意化）。
* [x] 實作 WeaponManager（武器載入、RecipeID 關聯、PrefabMapping 子彈 Prefab 管理）。
* [x] 重構 PlayerController 串接武器系統，傷害數值改由武器表驅動。
* [x] 實作通用受擊反應系統（HitReactionHandler）：白光閃爍、擊退位移、無敵時間。
* [x] MonsterData.csv 新增受擊反應欄位（InvincibleTimeMs, KnockbackThreshold, KnockbackPercent）。
* [x] PlayerController 新增 TakeDamage 介面與寫死的受擊反應參數，預留未來接觸傷害使用。
* [x] 實作武器序列圖動畫系統：WeaponTable.csv 新增 WeaponAniPath / WeaponAniNumber / AnimFPS 欄位，支援多張 PNG 序列圖自動載入與循環播放。
* [x] 擴充 BallisticsEngine.Spawn API 支援 Sprite[] 動畫參數，BulletInstance 內建動畫播放邏輯，分裂彈自動繼承動畫。
* [x] 實作環繞型彈道系統（OrbitalBehavior）：RecipeTable.csv 新增 IsOrbital / OrbitalRadius / OrbitalCount 欄位，子彈以玩家為圓心環繞飛行。
* [x] 環繞彈與穿透（繼續環繞）、反彈（脫軌飛出）、分裂、追蹤等行為完全相容。
* [x] RecipeTable.csv 新增 BlockedByEnvironment 欄位，可讓配方（特別是環繞彈）穿過地形障礙物不被銷毀；PlayerController 抽出 ResolvePierceableLayers 對所有武器路徑通用。
* [x] 環繞彈引入「群組生命週期」：個別子彈 LifeTime 覆寫為 -1，由 PlayerController 統一在 recipe.LifeTime 秒後一次銷毀整組，確保同生同死。
* [x] 實作地面特效鏈式觸發系統：新增 GroundEffectTable.csv、GroundEffectManager / GroundEffectInstance，RecipeTable 新增 GroundEffectID + GroundEffectTrigger 欄位，子彈命中怪物時可在命中點生成停留型 AOE（單次爆裂或週期 DOT，循環動畫）。
* [x] 地面特效改為 tile 鋪面渲染：GroundEffectTable 新增 TileSize 欄位，圓形範圍內每格放一張同步動畫的 sprite；傷害仍以整圓 OverlapCircle 一次計算。
* [x] 地面特效鋪面演進：先試「金字塔（菱形）」演算法但實機呈現過於菱角分明，最終改回「真實圓形掃描」——`(i*TileSize)² + (j*TileSize)² ≤ Radius²` 才保留 tile，圓滑度由 `R / TileSize` 解析度決定（建議 ≥ 4），實際 tile 數 > 500 時印 LogWarning 但仍照生成。
* [x] 修正子彈命中時用「當下武器」造成的跨武器污染：PlayerController 改用 lambda closure 把發射當下的 WeaponData 鎖在 callback，舊子彈不會誤用新武器的 Damage / GroundEffectID。
* [x] 地面特效新增 `GroundEffectHitTarget` 欄位（`Enemy` / `Environment` / `Any`）：與 `BounceTarget` 獨立，可分別設定子彈打到怪物 / 障礙物 / 任一目標時才釋放地面特效，預設 `Enemy` 沿用首版行為。
* [x] 實作拋物線型彈道（`IsParabolic`）：新增 `ParabolicBehavior`（接管移動、CollisionMask=0、視覺假高度），`RecipeTable` 新增 3 欄（IsParabolic / ArcHeight / LaunchSource），`GroundEffectHitTarget` 加上 `Ground` 列舉值；抵達目標落地時透過 `BulletInstance.OnGroundLanded` 事件觸發 `Ground` 過濾的地面特效；支援「玩家位置」與「攝影機視野外隨機方向」兩種發射來源。
* [x] 拋物線進階：`Speed` 欄位語意改為「飛行時間（秒）」（固定時間抵達，與距離無關，多顆同時落地）；支援 `SpreadCount` / `SpreadAngle` 一發多顆的扇形分裂（不需要 `SplitTiming`，獨立分支）；新增 `LandingScatterRadius` 落點隨機半徑欄位，多顆炸彈各自在自己的扇形目標附近圓盤內均勻隨機落點，避免堆疊。
* [x] 實作持續掃射型雷射光束（`IsLaser`）：新增獨立 `LaserBeam` 核心元件（line-march 把追蹤/反彈/穿透/射程收斂進同一迴圈）、`Custom/AdditiveBeam` 加色 shader、`BallisticsEngine.SpawnBeam` 純程式建構工廠；`RecipeTable` 新增 `IsLaser` / `dotInterval` / `BeamRange`，`WeaponTable` 新增 `BeamTexturePath` / `BeamColor` / `BeamWidth` / `ScrollSpeed`（行為與外觀分表，換素材即換風格）。
* [x] 雷射完全複用既有配方：吃 `PierceCount`（穿透）、`HomingTurnSpeed`（追蹤彎曲，賣點）、`BounceTarget` + `MaxBounces`（反彈折線）、`SpreadCount` / `SpreadAngle`（一發多道）、`SplitTiming=OnHit` + `SubRecipeID`（命中分裂，節流綁 dotInterval）；傷害走武器表 `Damage` 以 DOT 節拍結算並吃怪物無敵時間。`PlayerController` 加持續光束生命週期（按住維持、放開/切武器銷毀整組）。
* [x] 雷射打磨與除錯（一輪實機調校）：
  * **命中寬度所見即所得**：改用 `Radius = BeamWidth/2`，視覺與命中共用一欄。
  * **修穿牆飛出場外**：牆用細射線 `Raycast`、敵人用粗圓 `CircleCastAll` 分開偵測（厚圓在掠射角漏抓薄牆是元凶）。
  * **修貼身怪打不到**：本專案 `queriesStartInColliders=false` 會讓砲口 cast 忽略起點重疊的怪，砲口加一次 `OverlapCircle` 補抓（不動全域設定）。
  * **渲染改自繪 mesh**：徹底解決 `LineRenderer` 轉角的兩難（圓角→反彈離牆遠／不圓角→某段被擠扁變細），每段獨立四邊形、轉角依夾角延伸重疊 → 緊貼牆反彈又全程等寬；端帽平頭交給光暈收尾，亮核不凸出頭尾。
  * **雷射質感**：`beam_core` 貼圖加沿長度的能量波帶（配 `ScrollSpeed` 做出一波一波流動）、shader 加白熱核心 + 微脈動（顏色與亮度分離，波動在核心也看得到）。
* [x] 雷射外型「種類化」與全參數化（見 [LASER.md](LASER.md)）：外觀改由 `BeamStyle`（種類編號 1~10）+ `BeamColor`（顏色編號 1~10）+ `BeamWidth` 三欄驅動，使用者只填編號；外型細節（截面/波帶/流動/白核/脈動/雜訊）全部參數化進 `Custom/AdditiveBeam` shader（**不再需要貼圖**），10 種風格集中定義於 `BeamStyleLibrary`，光暈分離出 `Custom/AdditiveGlow`。含「鏡光（古鏡）」種類；`BeamStyle × BeamColor` 正交 = 100 種組合。**加第 11 種 = `BeamStyleLibrary` 多一組數字，零產圖**。
* [x] 新增火焰噴射器（雷射的「火焰外觀模式」，見 [LASER.md](LASER.md)）：火焰噴射器本質是雷射（按住掃射 + 持續 DOT），只換外觀。`LaserBeam` 加 `DrawBeam` 旗標（false = 不畫光束 mesh/光暈、只算幾何與命中）+ 開放唯讀 `Points` 路徑；`BallisticsEngine.SpawnBeam` 加 `drawBeam` 參數。雷射武器 `TrailEffectID > 0` 時進火焰模式：`PlayerController` 沿 `beam.Points` 每隔 `TrailStep` 維護一排循環火焰 Vfx、每幀重新定位（跟著掃）。`VfxInstance` 新增「`Loop=1` + `Duration=-1` = 無限循環（外部管理生死）」。復用 `TrailEffectID`/`TrailStep`（與地刺同欄位，載體換成光束）。範例：武器「火焰噴射器」→ 配方 20(IsLaser, dotInterval 0.2, BeamRange 5, TrailStep 0.5) → Vfx 4「火球」(FireBall, Loop=1, Duration=-1)。
* [x] 新增「軌跡特效」機制，並以此重做地刺武器（讓地刺吃滿 RecipeTable 行為，見 [BALLISTICS.md](BALLISTICS.md) 的 `OnTrailPoint`）：`BulletInstance` 新增 `TrailStep` + 通用事件 `OnTrailPoint`（每飛 TrailStep 距離回報一次經過點，彈道系統不知種的是什麼），`BallisticsEngine.Spawn` 接 `onTrailPoint` 並傳遞給分裂子彈，無圖子彈自動隱形。`RecipeTable` 加 `TrailStep`、`WeaponTable` 加 `TrailEffectID`（引用 VfxTable）。**地刺 = 一顆隱形的正常子彈，沿路每隔 TrailStep 種一根尖刺 Vfx**——因此自動繼承反彈/分裂/穿透/追蹤/散射全部行為（子彈反彈→刺軌跡折、分裂→刺分岔、追蹤→刺蛇行），傷害走武器 Damage（正常命中），不再用地表傷害。
* [x] ~~地刺波 `IsGroundWave`（地表特效版）~~：已移除——改用上述「軌跡特效」做法，因為地表特效不會飛、無法吃 RecipeTable 行為。`GroundWaveEmitter` / `GroundEffectManager.SpawnWave` 一併刪除；earthSpik 從 GroundEffectTable 搬到 VfxTable。範例：武器「地裂刺」→ 配方 19「地刺」(隱形穿透彈, TrailStep=1.5) → Vfx 3「地刺」(earthSpik)。
* [x] 拋物線武器新增落地殺傷半徑（`BlastRadius`，見 [GROUND_EFFECT.md](GROUND_EFFECT.md)）：`RecipeTable` 加第 30 欄 `BlastRadius`，存於 `RecipeEntry`（主遊戲側、不碰彈道系統）。拋物線彈落地時 `PlayerController.HandleParabolicLanded` 以 `Physics2D.OverlapCircleAll` 對 `EnemyLayer` 做一次性 AOE、以**武器表 Damage** 結算（吃怪物無敵時間、擊退由爆心朝外）。與地面特效**獨立可並存**（炸傷一次 ＋ 留火延燒）。炸彈武器（5/6）Damage 0→5、配方 12/13 BlastRadius=1.5。
* [x] 實作一次性特效系統（VFX，見 [VFX.md](VFX.md)）：新增 `VfxTable.csv` + `VfxManager` / `VfxData` / `VfxInstance`（仿 GroundEffect 三件套但砍掉 tile / 傷害 / DOT，單一 SpriteRenderer 播一輪自毀，免 prefab、Manager 自建 GameObject）。`WeaponTable` 新增 `FireEffectID`（發射特效，玩家身上朝瞄準方向）/ `HitEffectID`（擊中特效，命中點）兩欄——外觀掛武器表、不污染配方行為。串接四個既有觸發點（`Shoot` / `UpdateLaser` 按下 / `HandleBulletHit` / `HandleParabolicLanded` / `HandleBeamTick`），皆讀發射快照武器；擊中首版統一一種、不分表面（怪/牆/地共用）。與彈道系統完全分離，可複用為未來死亡煙、撿道具閃光等。
* [x] VFX 打磨（見 [VFX.md](VFX.md)）：① VfxTable 新增 per-effect `SortingOrder` 欄——留空用 VfxManager 全域、填了用自己的（地刺填 <10 畫在角色腳下、爆炸留空維持上層，**改一個不影響其他特效**）。② 一次性動畫改「**逐格完整播完才銷毀**」——銷毀由動畫進度驅動（不再用獨立壽命計時、也不再繞回第一幀），不管 AnimFPS 多慢都保證每一格播完，與子彈/光束速度無關。③ 新增 `Loop=1 + Duration=-1 = 無限循環`（外部管理生死，給火焰噴射器的火焰柱用）。④ 火焰柱第一根不種在角色身上、改從前方 `TrailStep` 起（命中判定不受影響，仍由雷射砲口 OverlapCircle 補抓貼身怪）。
* [x] 除錯（軌跡/分裂相關）：
  * **修 `HomingTurnSpeed` 沒擋空白**：`RecipeManager` 解析第 14 欄時 `float.Parse("")` 會拋 FormatException、**中斷整個配方載入**（症狀：後面的配方全部沒載入、武器找不到配方）。補上「留空就跳過」守衛，符合文件「0 或留空 = 不追蹤」。這是長期潛藏 bug，既有配方剛好都填 0 才沒爆。
  * **修分裂子彈被誤清成隱形**：加「軌跡/隱形子彈」時，`Internal_Create` 對「沒給圖」一律清空 sprite，連「複製母彈而來、本帶圖」的分裂子彈也被清掉 → 所有分裂武器整排消失。改成 `hideIfNoSprite` 只在**初始發射**套用，分裂子彈（`Internal_SpawnSplit`）保留複製來的圖。
* [x] 武器切換調整（見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)）：初始武器改為**武器表最後一號（最高 ID）**；按 `E` 改為**往前切**（`SwitchToPreviousWeapon`：往較小 ID、到最低再繞回最高）。
* [x] 新增佛光型武器（`IsAura`，見 [GROUND_EFFECT.md](GROUND_EFFECT.md)）：以玩家為圓心、按住維持一個**圓形 AOE 光暈**，圓內怪物持續受傷（手持佛光的籠罩感）。**做法＝「一個會跟著玩家移動的 GroundEffect」**，不發射任何子彈、**完全不碰彈道系統**（旗標存主遊戲側 `RecipeEntry.IsAura`）。關鍵：`GroundEffectInstance` 的視覺是子物件、傷害每拍即時讀 `transform.position`，所以 `PlayerController.UpdateAura` 每幀把 instance 移到玩家身上，視覺圈與傷害圈就一起跟著走（GroundEffect 本體零改動）。生命週期仿雷射群組（按住維持、放開/切武器 `ClearActiveAura` 銷毀）。圓的半徑/節拍/外觀走配方 `GroundEffectID` 指向的 `GroundEffectTable`，傷害走武器表 `Damage`（透過新增的 `damageOverride` 餵入）。
* [x] GroundEffect 新增**單圖渲染模式**（`GroundEffectTable` 加 `RenderMode` 欄，`Single` = 放一張縮放到直徑 `2*Radius` 的發光圓暈，給佛光那種柔和光暈用；留空 = 既有 tile 鋪滿，火堆/毒霧不受影響）＋ `Spawn(id, pos, damageOverride)` 多載（`>=0` 時改用此值結算傷害）。佛光圖 `Resources/GroundEffect/buddhaLight/buddhaLight_01.png`（暗琥珀佛燈色、純圓盤、半透明 RGBA，刻意壓低不透明度避免遮住下方怪物；初始 `Radius=1.2` 約籠罩玩家全身）。
* [x] 新增連鎖閃電武器（`IsChain`，見 [LASER.md](LASER.md)）：點一下（吃 `FireInterval`）朝滑鼠射出，命中首怪後在 `ChainRadius` 內逐跳到最近的怪、跳 `ChainCount`（= `MaxBounces` 欄）次，每跳吃滿武器表 `Damage`。**目標搜尋＋傷害全在主遊戲側 `PlayerController.ShootChain`**（守住「彈道系統不算傷害」邊界）；視覺複用雷射的折線 mesh——`LaserBeam` 新增**靜態折線模式** `SetStaticPath(pts, life)`（餵入算好的折線、不 march、不回報傷害，短命淡出後自毀）＋ `BallisticsEngine.SpawnChainVisual` 工廠。外觀走武器表 `BeamStyle`/`BeamColor`/`BeamWidth`（閃電風格填 7），主遊戲在每段間插入鋸齒抖動點做出電弧感。第一段射程沿用 `BeamRange` 欄、撞牆就停（閃電不穿牆）。
* [x] 連鎖閃電除錯 ＋ 吃散射/追蹤（見 [LASER.md](LASER.md)）：① 修「打地上物卻打不壞」——目標搜尋原本只搜 `EnemyLayer`，改成 `EnemyLayer | EnvLayer` 再用 `IDamageable` 過濾（純牆無 IDamageable 自動排除、不浪費跳躍），符合「任何能造成傷害的武器都能破壞地上物」（記在 [PROBLEMS.md](PROBLEMS.md) B4）。② 吃 `SpreadCount`/`SpreadAngle` = 一發多道扇形連鎖（`ShootChain` 迴圈 + `CastOneChain`）；③ 吃 `HomingTurnSpeed` = 首目標自動鎖定（aim-assist，`FindNearestInCone`，半角=HomingTurnSpeed 上限180，180=鎖最近任意方向）。
* [x] 新增天降雷擊武器（`IsSkyStrike`，見 [LASER.md](LASER.md)）：點一下從**畫面上緣外往下劈**到滑鼠所在點，落地以 `BlastRadius` 半徑做**圓形 AOE**（武器 `Damage`、`OverlapCircleAll` 對 `Enemy|Env` 的 `IDamageable`，怪與家具都吃）。複用：視覺＝連鎖閃電的靜態折線（垂直鋸齒閃電，`BuildJaggedPath` 改為依段長細分，長垂直閃電也夠鋸齒）、AOE＝拋物線 `BlastRadius` 那套、散射＝拋物線扇形落點分佈。吃 `SpreadCount`/`SpreadAngle`（多道落點）與 `HomingTurnSpeed`（落點吸附最近怪，當搜尋半徑）。可選 `GroundEffectID` 在落點留焦痕/殘電。**已預留 SubRecipeID 接點**（`StrikeAt` 內 TODO：之後讓雷擊落點接連鎖閃電）。
* [x] 天降雷擊接 SubRecipeID → 連鎖（見 [LASER.md](LASER.md)）：雷擊配方填 `SubRecipeID` 指向 `IsChain` 配方時，落地後從落點接一條連鎖閃電轟旁邊的怪。抽出共用的 `PlayerController.RunChain`（從首目標起連鎖，連鎖閃電武器與雷擊共用）；`RecipeEntry` 新增 `SubRecipe` 參考（`ResolveSubRecipes` 設定）讓雷擊讀到子配方的 `ChainCount`/`ChainRadius`。連鎖用發射武器的 `Damage`/外觀、子配方的次數/半徑（同一般 SubRecipe 邏輯）。目前設定：配方 23 `SubRecipeID=22`、並移除雷擊的擊中特效與地面特效（純雷擊 → 連鎖）。
* [x] 天降雷擊接的連鎖改用「連鎖配方對應武器」的外型/傷害：`WeaponManager` 加 `RecipeID → 武器` 對照（`GetWeaponByRecipeID`），雷擊接連鎖時用該配方對應的武器（連鎖閃電武器，白），所以雷擊（黃）與連鎖（白）可各有顏色/粗細/傷害。
* [x] 新增**命中迸發子武器** `SubWeaponOnHit`（見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)）：子彈命中時在命中點生成「**武器表上指定 ID** 的武器」一發，子武器**自帶外型/傷害/追蹤**（解決 SubRecipeID 只能仿母武器外型的限制——根因是 SubRecipeID 指配方無外型、且彈道層與武器系統解耦只能複製母彈）。在主遊戲側 `PlayerController.HandleBulletHit → TryTriggerSubWeapon → SpawnSubWeaponAt` 實作（彈道層不變）；`RecipeEntry` 加 `SubWeaponOnHit`（武器 ID）+ `SubWeaponHitTarget`（`Enemy`/`Environment`/`All`）。迸發方向取命中面法線往外，吃子武器自己整套配方（散射/追蹤…）。範例：武器 13「蜂巢」配方 24 `SubWeaponOnHit=2, All` → 打到牆/怪迸出武器 2（3 分裂追蹤飛劍）＝ 3 把追蹤飛劍（飛劍圖、非炸彈圖）。
