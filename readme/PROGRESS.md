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
* [x] 實作一次性特效系統（VFX，見 [VFX.md](VFX.md)）：新增 `VfxTable.csv` + `VfxManager` / `VfxData` / `VfxInstance`（仿 GroundEffect 三件套但砍掉 tile / 傷害 / DOT，單一 SpriteRenderer 播一輪自毀，免 prefab、Manager 自建 GameObject）。`WeaponTable` 新增 `FireEffectID`（發射特效，玩家身上朝瞄準方向）/ `HitEffectID`（擊中特效，命中點）兩欄——外觀掛武器表、不污染配方行為。串接四個既有觸發點（`Shoot` / `UpdateLaser` 按下 / `HandleBulletHit` / `HandleParabolicLanded` / `HandleBeamTick`），皆讀發射快照武器；擊中首版統一一種、不分表面（怪/牆/地共用）。與彈道系統完全分離，可複用為未來死亡煙、撿道具閃光等。
