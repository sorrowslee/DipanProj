# 目前進度 (Current Progress)

> 返回 [文件總覽](README.md) ｜ 這是「做過什麼、怎麼演進」的變更日誌；各系統的「現狀說明」請看對應主題文件。
> **本檔一律倒序（最新在最上）**，新條目直接加在這段註記下方。記錄格式與大小封存規則見 [DOCS_GUIDE.md](DOCS_GUIDE.md)。
> 較舊條目（專案初期 ~ 2026-09-22，共 305 條；2026-08-21、2026-08-27、2026-10-01 三次搬入）已**原文照錄**封存至 [archive/PROGRESS-archive.md](archive/PROGRESS-archive.md)，檔頭附逐條索引；查歷史脈絡去那裡，別當作已遺失。

* [x] **浮游改成「被動武器」＋掛載系統 `PassiveWeaponIds`：應龍水珠變護身符、應龍血統附帶（⏳ 未編譯未實測）**（2026-10-01，見 [PASSIVE_WEAPON.md](PASSIVE_WEAPON.md)）：
  作者想法：配方表上的能力能不能「自由嫁接」——放武器上裝武器有、放護身符／戒指上裝了就有、放血統裡升到就有；裝備數值這次不動。<br>
  **研究結論**：能力要拆兩類——**修飾型**（反彈、傷害…）珠子系統早就是全身累加、本來就可嫁接；**行為型**（`Mode`）綁扳機與瞄準、只能有一把當前武器，刻意不讓雷射掛戒指；但 **Familiar 沒有扳機**，是行為型裡的被動子類，可以跟主武器並存。問題出在上一條把它做成「當前武器的一種 Mode」：裝了水珠就不能拿劍，跟作者要的正好相反。<br>
  **做法**：`WeaponModeSpec` 加 `ModeSpec.Passive`／`IsPassive`（Familiar 打開）；`ItemTable` 第 19 欄、`BloodlineTable` 第 27 欄同名 **`PassiveWeaponIds`**（分號分隔多把，`ItemDatabase.ParseIdList` 共用）；新 **`PassiveWeaponSet`**（收集「當前武器是被動型」＋裝備欄＋血統三種來源、每把過 `AbilityResolver`、簽章比對）；`WeaponFamiliar` 改成**一個元件管多組**（每把一個 `Group`，邏輯原封搬進去、多組同軌道相位錯開）；`PlayerController` 拔掉 HandleFiring 的 Familiar 分支、`ActiveFamiliarWeapon`→`PassiveWeapons`、新 `CanRunPassive`（不要求有主武器）、`CanFire` 遇被動型回 false（視為空手）、`TickPassiveWeapons` 放 HandleFiring 開頭所有 guard 之前。資料：**武器型的道具 72 移除、新增 503 應龍護身符**（`EquipSlot=Amulet`、`WeaponID` 留空、`PassiveWeaponIds=72`、icon 作者給的 `UI/Icons/Equipment/amulet_waterorb`；第一版只把 72 改欄位沒改號改名，作者指正後重做）；血統 62 填 `72`。WeaponTable 72 應龍水珠不動。**應龍血統 62 的神格環繞層（`OrbitVfxId` 38／`OrbitCount` 1／`OrbitSize` 0.4）清空**——被動武器的本體也是水球 38，疊在一起變一堆；作者拍板三階特效就靠血統附帶的應龍水珠撐。<br>
  **為什麼不另開一張被動表**：被動武器本身已完整住在 WeaponTable／RecipeTable、工坊能調，再開表是同一件事定義兩次；缺的只是「誰帶著」這一個欄。出現「不是武器」的被動（回血、留火）時再以類型＋參數開 `PassiveTable`，掛載欄與收集流程可沿用。<br>
  **為什麼重算用每幀比三個值、不加事件**：來源的變動路徑太多（換裝備、改珠子、喝藥、夢境覆寫、讀檔、工坊每幀改值），`BloodlineSystem` 又沒有變動事件；比 `LoadoutVersion`＋血統 Id＋當前武器參照最便宜也最不會漏。<br>
  **拍板**：珠子影響全身（血統給的被動也吃珠子——「鑲嵌珠更重要、程式也不會錯」）；同一把掛兩次＝兩組獨立運作、不合併；**被動武器不能當一般武器裝在武器欄**（原本留的「武器欄裝 Familiar＝空手帶被動」相容路徑同日拿掉，`PassiveWeaponSet` 來源①只剩工坊模擬／劇情覆寫，`OnInventoryChanged` 遇 `WeaponID` 指到被動型印警告當空手）。<br>
  **同日順手**：`PROGRESS.md` 第三次封存（464KB → 65KB，123 條搬去 archive，原文逐條比對過一字未改）。<br>
  **通則**：「能不能嫁接」先問這個能力有沒有扳機——有扳機的東西只能有一個，沒扳機的才是「來源無關」。
* [x] **新發射模式「浮游」`Mode=Familiar`＋測試武器「應龍水珠」（⏳ 未編譯未實測、icon 暫代）**（2026-10-01）：
  作者規格：本體在玩家身邊環繞、自主發射飛行物攻擊；可加環繞數量、發射頻率、子彈威力與大小。討論後拍板：**裝備就全自動（不按鍵）**、**多本體錯開射、都打最近的**。<br>
  **做法**：新元件 **`Scripts/Weapon/WeaponFamiliar.cs`**（本體生死與擺位＋錯開節奏＋索敵；環繞視覺照 `BloodlineOrbit` 的壓扁軌道／前後換排序／遠近縮放，排序 ±3 壓在血統環繞層 ±2 之上）；`PlayerController` 加 `ActiveFamiliarWeapon`、`HandleFiring` 的 Familiar 分支、`UpdateFamiliar`／`FireFamiliarShot`（扣魔＋`WeaponCastService.FireNormal`＋`HandleBulletHit`，子彈全套行為零重寫）；`WeaponModeSpec`（enum 尾端 `Familiar`、模式定義、WeaponTable 新欄 `FamiliarVfxId`（必填）／`FamiliarSize`／`FamiliarSpin`）；`WeaponData`／`WeaponManager`（只有 Familiar 讀這三欄）＋兩個 WeaponData 複製點；`RecipeEntry`（Familiar 也設 `HasSplit`、`Range` 預設 8）；`PlayerAbilities`（分裂珠對 Familiar 也開 `HasSplit`）。浮游武器裝著時按左鍵角色**不轉身**（不吃攻擊鍵）。<br>
  **為什麼本體生死不放在 HandleFiring**：背包開著時 HandleFiring 不會被呼叫，在背包裡卸下武器本體會一直掛著——所以元件每幀自己問 `ActiveFamiliarWeapon`，發射節奏才跟著 HandleFiring 走（背包開著自然停火）。**為什麼錯開要兩層計時**：只給每個本體各自冷卻的話，沒怪時大家都冷卻好了、怪一進來就同一幀齊射；加一個「任兩發至少隔 `FireInterval÷N`」再輪流出手，才是均勻的噠噠噠，同時每個本體射速不變（疾發珠照常有感）。**為什麼不走 `TrySpawnFireEffect`**：它會通知三階血統的攻擊特效，自動射擊會讓那層一直播。<br>
  **沿用既有欄＝現成珠子直接有效**：群環（本體數）、環距、疾發、銳利、須彌（子彈大小）、遠射（索敵半徑）＋一般子彈那套；集氣／連擊無效（沒有扳機）。GemTable 12／13 的 Note 一併更新。<br>
  **資料**：WeaponTable 表尾加三欄（舊列全補空，加一行群組註解）；新配方 **86**「應龍水珠-浮游射擊」（間隔 1.2、速度 9、判定 0.2、壽命 2、索敵 8、半徑 1、**2 個本體**）、武器／道具 **72**「應龍水珠」（傷害 3、耗魔 0.5、`BulletScale 2`、本體＝VfxTable **38** 應龍水球、大小 0.8、轉速 60）；子彈圖 **`Resources/Weapon/single/weapon_waterorb.png`**（程式畫的 256px 半透明藍水球＋高光＋光暈；meta 複製 thunderbomb 的、`filterMode` 改 Bilinear，平滑圖用 Point 縮小會鋸齒）。icon 暫用同一張子彈圖。作弊面板「取得所有武器」拿得到。<br>
  見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)〈Familiar 浮游〉、[RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)。
* [x] **血月鬼爪爪痕大小定在 `Scale 4.4`**（2026-10-01）：作者先要求再放大一倍（4.4→8.8），看過後要求縮小 0.5 倍，回到 **4.4**（畫面上約 2.8 單位寬，判定半徑 2.1）。
* [x] **血月鬼爪改回原本的爪痕動畫，播快一倍、放大兩倍**（2026-10-01）：作者實測下面那版程序化刀光「還是不太行」，要求恢復原本、改試「原動畫加速放大」。武器 21 的資料還原成改版前（`HitEffectID 22`，`SlashStyle`／`HitEffectEnemyOnly`／`HitEffectAlignBullet` 清空＝與 git HEAD 同值）；VfxTable 22 `AnimFPS` 20→**40**、`Scale` 2.2→**4.4**（畫面上約 2.8 單位寬，判定半徑 2.1）。⚠ VfxTable 22 也被夢境「血月雙爪」（武器 63，沒人用）引用，會一起變。<br>
  **保留沒還原的**：`ShootMelee` 的位置修正（圓心 `MuzzleWorldPos`、怪的身體／碰撞邊緣判定，PROBLEMS F32）——舊爪痕現在從身體中段往前播。刀光程式（`MeleeSlashFx`／`MeleeSlash.shader`／`SlashStyle` 欄）與命中素材（VfxTable 57、`BloodClawHit/`）留著沒人用，要刪見 TODO。
* [x] **血月鬼爪重做：位置對準＋程序化三爪刀光＋命中噴血＋左右爪交替（⏳ 未編譯未實測；2026-10-01 作者實測後撤回外觀，見上一條）**（2026-09-30）：作者：「位置對不準、沒打擊感、特效小小爛爛的」。<br>
  **診斷**（見 PROBLEMS **F32**）：① 圓心是 `transform.position`（腳踝，E14 漏網）；② 扇形角度用怪的 pivot（畫布中心，G13）；③ 特效 64px×2.2＝畫面 1.4 單位，判定卻是半徑 2.1、而且素材是「往前直刺」判定是「110° 橫掃」；④ 揮擊特效佔用 `HitEffectID`，揮空也播、打中反而什麼都沒有；⑤ 頓幀／震屏機制現成但沒接（作者這次選擇不加）。<br>
  **做法**：`ShootMelee` 圓心改 `MuzzleWorldPos`、判定改「怪的 `BodyCenterWorldPos` 或碰撞體 `ClosestPoint` 任一在扇形內」；新 **`MeleeSlashFx`**＋**`Resources/Shaders/MeleeSlash.shader`**（照 `GroundCrackFx` 範本；三道爪痕沿判定扇形掃過、硬色塊三階＋黑色撕裂邊，照 VFX_GUIDELINE §1；排序 21990、登記在 MapDepthSort 檔頭）；WeaponTable 新欄 **`SlashStyle`**（只 Melee 讀；留空＝舊行為，所以沒人用的配方 63/64 夢境爪行為不變）；刀光樣式下 `HitEffectID` 改成打中時在目標身上播，`TrySpawnHitEffect` 多一個選填角度參數（近戰沒有子彈可抄角度）；每刀 `_meleeFlip` 翻轉＝左右爪交替。命中特效新做 **VfxTable 57**（`Splatters/directional_splatter_002` 紅轉 90° 朝右＋`impfx1_quick_impact_A` 白染淡粉疊在起點，最近鄰放大 4 倍，`Resources/VfxEffects/BloodClawHit/`）。<br>
  **資料**：武器 21 `HitEffectID` 22→57、`SlashStyle 1`、`HitEffectEnemyOnly 1`、`HitEffectAlignBullet 1`；其他武器新欄留空（逐列比對過只有 21 變）。VfxTable 22（舊 BloodClaw）保留沒刪，配方 63 血月雙爪還指著它。<br>
  **流程通則**：刀光外觀先用 Python（numpy，與 shader 逐行同公式）在實際背景上渲染逐幀圖＋動圖給作者確認，再寫 shader——主觀的外觀先定案，避免寫進 Unity 後反覆改。
* [x] **擊中特效可設「只在打到怪時播」：血滴子打牆不再噴血（⏳ 未編譯未實測）**（2026-09-30）：作者：「牆壁會流血也太奇怪了」。原本 `TrySpawnHitEffect` 打到什麼都播。WeaponTable 新欄 **`HitEffectEnemyOnly`**（`WeaponData`／`WeaponManager`／`WeaponModeSpec` 特效群，集氣與珠子兩個 WeaponData 複製點都帶上），填 1 ⇒ `hitEnemy=false`（牆、可破壞地上物）不播。**只對子彈／環繞／迴旋／雷射／連鎖有效**——落點型模式（拋物線落地、法陣、落雷）呼叫時不傳 `hitEnemy`，讀進來會永遠不播，所以 `WeaponManager` 依 spec 對無效模式不讀。只有武器 36 填 1，其餘留空；WeaponTable 順手把 19 欄的舊短列補齊到 21（值不變，逐列比對過）。見 RECIPE_AND_WEAPON〈SpriteAngleOffset 設定說明〉末段。
* [x] **血滴子飛行速度加快一倍**（2026-09-30）：作者看過第三版軌跡後要求。配方 73 `Speed` 10→**20**（迴旋去回同速，4 趟全程約 4.9→2.5 秒；每段保險上限由 BoomerangBehavior 依速度自動重算，不用另外調）。
* [x] **迴旋軌跡第三版：中間趟繞主角轉、只有最後一次回手上（⏳ 未在 Unity 實測；C# 已用替身編譯執行）**（2026-09-30）：作者實測第二版「像蝴蝶在飛」——每趟都是一個尖端在主角身上的淚滴，回來時急轉、刻意穿過身體，看起來像減速再硬擠過去。作者：主角只是參考點，中間的迴旋不必飛過身體，只有最後一次要回到身上。<br>
  **改成三段**：出手（淚滴前半）→ (N−1) 個以主角為中心的橢圓半圈（前→右側掠過→後→左側掠過→前…）→ 收回（淚滴後半）。只改 `BoomerangBehavior.cs`，建構參數不變。<br>
  **關鍵（通則）**：兩種曲線要接得順，不只切線要同向，**曲率也要相等**——橢圓半短軸取 `b = 2√2·k`，端點曲率半徑 `b²/R` 才會等於淚滴終點的 `8k²/R`。繞圈中心用指數平滑追主角（進入時＝出手點，否則位置會跳）；繞圈段也套「實際速度不低於 Speed」，否則主角反方向跑時圈被拖住會變慢。<br>
  **驗證**：Python 先模擬，再把 **C# 原檔**配一個最小 Unity 替身（Vector2／Mathf／Transform…）用 .NET 8 的 csc 直接編譯執行，照 BulletInstance.Update 的順序跑：兩邊數字完全一致。靜止時全程等速 10、y −5~5、x ±2.72，4 趟 4.92 秒不觸發保險，清命中名單 7 次；30／144 幀一致；主角往四個方向跑都收得回來，只剩換段那一兩幀略低於 9.5；每幀轉角最大 9°、只出現在繞過終點處、逐幀漸變無尖峰。暫停不轉圖、擁有者消失 16 幀內淡出收掉也驗到。
* [x] **迴旋軌跡改成淚滴形（繞一圈回來）（⏳ 未編譯未實測）**（2026-09-30）：作者實測上一版正常，但「直線去、原路直線回」沒有迴旋鏢的感覺。畫了三種軌跡對照後作者選 **淚滴形**（一出手就往左彎、終點繞圓頭、從右側回到玩家；多趟時往身後反向再繞一個，連成 8 字），寬度拍板**寫死＝射程的一半**。只改 `BallisticsSystem/Runtime/Behaviors/BoomerangBehavior.cs`，建構參數不變，主遊戲零改動。<br>
  **難在哪（通則）**：① 參數曲線要「等速」得每幀解弧長——尖端附近前進量與 Δθ 是二次關係，導數線性估計（連修正一次）實測仍會在出手／穿過玩家時連續幾幀減速到 0.67；改二分法。② 二分的搜尋範圍不能直接取到曲線終點：淚滴起點與終點是同一點，「到終點的弦長≈0」會被誤判成一出手就接住；要從小步長倍增、只在單調段裡找。③ 玩家移動：去程圈固定在出手點，回程錨點 smoothstep 漸移到玩家 ⇒ 一定回到手上；但玩家往反方向跑時錨點後拖會抵掉前進，模擬看到速度掉到 0.89、停在半空——加「回程實際速度不低於 Speed」（世界距離二分）。<br>
  **驗證**：用 Python 移植同一套演算法照 BulletInstance 每幀順序模擬（30／60／144 幀、玩家靜止與四個方向跑、射程 1~20、速度 10~40）：靜止時全程等速、終點 5.00、寬 2.50、左出右回，4 趟 4.78 秒不觸發保險；邊跑邊接都收得回來，只剩「穿過玩家那一幀」偶有一幀變慢。
* [x] **新發射模式「迴旋」`Mode=Boomerang`＋血滴子改成迴旋 4 趟（⏳ 未編譯未實測）**（2026-09-30）：
  作者規格（討論後拍板）：丟出去飛 `Range` 後折返**追著玩家**飛回；`BoomerangCount>1` 時**穿過玩家往身後再飛**（鐘擺），最後一趟碰到玩家才收回；**只有第一趟看滑鼠**；**全程穿牆（反彈不作用）、無限穿怪、每趟都能再打同一隻**；保險用 `LifeTime` 概念但**每趟各自算**上限 ＝ (2×Range÷Speed)×2＋0.5 秒，超時淡出。<br>
  **做法**：彈道模組新 `BoomerangBehavior`（整個飛行邏輯）＋`BulletInstance.ClearHitHistory()`；主遊戲 `WeaponModeSpec`（enum 尾端加 `Boomerang`、新欄 `BoomerangCount`、`Range` 說明）、`RecipeEntry`（`PierceCount=-1`／`BlockedByEnvironment=false` 寫死、`Range` 預設 5）、`WeaponCastService.FireNormal`（Boomerang 時用 extraBehavior 工廠掛行為、`CastContext.ReturnPoint`）、`PlayerController.ShootNormal` 一行（回程點＝`BodyCenterWorldPos`）。**發射分派一行都沒改**——一般子彈本來就走 `default` 分支，迴旋共用整條生成＋命中鏈，工坊／珠子有效性／載入檢查從 spec 自動跟上。<br>
  **為什麼「清命中名單」是關鍵**：`_hitObjects` 讓一顆子彈一輩子只打同一目標一次，回程會穿過去程打過的怪（作者：「不然這武器一點意義都沒有」）。**為什麼分趟計時**：整把只算一個總時間的話，前一趟玩家亂跑拖久了，後面的趟會在半空消失。**為什麼用「距離 ≤ 本幀步長」判定接住**：回程每幀直指玩家，這個條件等於「本幀會到達」，速度被迅捷珠拉很高也不會一幀跳過玩家。<br>
  **資料**：`RecipeTable.csv` 表尾加 `BoomerangCount` 欄（只有 73 填 4，其餘留空）；配方 73 改 `Mode=Boomerang`、名稱「血滴子-迴旋」，清掉變成無效欄的 `LifeTime 4`／`PierceCount 3`／`BounceTarget Environment`／`MaxBounces 4`（要改回舊版就填回這四個、清掉 Mode 與 BoomerangCount）。順手把 CSV 裡只有 51 欄的舊列補齊到 55（骨牢 4 欄當初沒補，值不變）。<br>
  見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)〈Boomerang 迴旋〉、[BALLISTICS.md](BALLISTICS.md)〈BoomerangBehavior〉。
* [x] **餓鬼牙符：咬合方向＋貼牆射不出去（兩處程式修正）（⏳ 未編譯未實測）**（2026-09-24）：
  ① **往左射卻往右咬**：命中特效一律 0 度生成。WeaponTable 新欄 **`HitEffectAlignBullet`**（`WeaponData`／`WeaponManager`／`WeaponModeSpec` 特效群＋子彈外觀有效欄、兩個 WeaponData 複製點），`TrySpawnHitEffect` 多一個選填 `bullet` 參數，填 1 時用子彈當下的 `rotation.z`＋`flipY` 生成；`HandleBulletHit` 傳入子彈。武器 38 填 `HitEffectAlignBullet=1`＋`FlipYWhenLeft=1`（往左飛時子彈與咬合都水平鏡像，不會牙齒倒過來）。見 RECIPE_AND_WEAPON〈SpriteAngleOffset 設定說明〉。
  ② **貼牆往外射一出生就撞牆**：判定半徑 1.6 的出生重疊圓蓋到背後的牆。彈道核心 `BulletInstance` 加 `IsBehindOverlap`，出生檢查與飛行中「起點就重疊」的碰撞，若那個東西在子彈背後／側邊（正在離開它）就不算命中。**對所有子彈生效**，判定 `Radius` 不用縮。見 PROBLEMS **F31**。
* [x] **特效庫第二批武器：作者第三輪調整＋屍毒雲落點偏移（⏳ 未實測）**（2026-09-24）：
  ① 引魂幡改單發（配方 74 拿掉 `SpreadCount 3／SpreadAngle 40`，ItemTable 說明同步）。
  ② 餓鬼牙符咬合特效 VfxTable 52 `Scale` 2→**6.9**：牙符飛行物 256px×0.001×17.2 ≈ 4.4 單位寬，咬合圖原生 64px ⇒ 4.4÷0.64 ≈ 6.9，兩者等寬。⚠ 飛行物的大小吃 `BulletScale`、命中特效只吃 VfxTable `Scale`，**改一邊不會帶動另一邊**。
  ③ **屍毒葫蘆「特效比落點低」**：查過程式，落點、落地殺傷、地面特效三者都在同一個 `landPos`（＝滑鼠點），**傷害位置沒跑掉**；是毒雲素材的接地點在圖的 y≈110/128、Single 模式卻用圖中心對位 ⇒ 畫面上低了約 2.2 單位。把 17 幀補透明邊成 128×220（接地點置中），零程式改動。見 PROBLEMS **E42**。
* [x] **特效庫第二批武器：作者第二輪調整＋兩個 bug（⏳ 未編譯未實測）**（2026-09-24）：
  ① **引魂幡往左飛骷髏頭倒立** ⇒ **加了程式**（這批第一次動程式）：WeaponTable 新欄 `FlipYWhenLeft`（`WeaponData`／`WeaponManager` 讀、`WeaponModeSpec` 子彈外觀群加一欄＝工坊自動出現、集氣與珠子的兩個 WeaponData 複製點都帶上），`BallisticsEngine.Spawn` 多一個選填參數 `flipYWhenMovingLeft`（預設 false，其他呼叫點零變化），`BulletInstance.ApplyFacingFlip` 往左飛開 `flipY`。PlayerController 四個 Spawn 呼叫點＋`WeaponCastService` 都傳入。只有武器 37 填 1。見 PROBLEMS **E40**。
  ② 引魂幡命中骷髏煙 VfxTable 56 `Scale` 2→**4**。⚠ 打到**怪**時若主角血統有 `BloodlineHitFx`，武器命中特效會被血統的換掉（`TrySpawnHitEffect` 既有行為），打牆才看得到武器自己的。
  ③ 餓鬼牙符 `BulletScale` 8.6→**17.2**、判定 `Radius` 0.8→**1.6**。
  ④ 飛蝗石碎塵 VfxTable 53 `Scale` 2.4→**1.6**（縮 1/3）。
  ⑤ **屍毒雲播兩次** ⇒ 地面特效序列圖是循環播放、Duration 比一輪長就會重播。GroundEffect 10 改 `AnimFPS 10→8`、`Duration 3→2.1`、`DamageInterval 0.5→0.35`（總傷害仍約 6 跳）。見 PROBLEMS **E41**。
* [x] **特效庫第二批武器：作者第一輪調整（大小／顏色／道數）（⏳ 未實測）**（2026-09-24）：「放大 1/2」＝×1.5、「放大 1 倍」＝×2。`BulletScale`：血滴子 5→**7.5**、引魂幡 4.2→**8.4**、餓鬼牙符 4.3→**8.6**、柳葉飛刀 5.5→**8.25**、掌心雷 4→**8**、無間輪 5.7→**8.55**、落星羅盤 4.7→**9.4**、屍毒葫蘆 4.2→**8.4**、八卦護身印 4.7→**7.05**；Normal／Orbital 的判定 `Radius` **同比例放大**（不跟 `BulletScale`，要自己對）。血滴子素材換 `pj4_sawblade` **紅**（覆蓋同名 8 幀）。玄冰針匣拿掉平行 3 道（配方 76 → 單發）。八卦護身印 `OrbitalCount` 8→**4**。命中特效：引魂幡改用新的 VfxTable **56**（重用怪物死亡骷髏煙、`Scale` 1→2；**不改 7**，7 是全遊戲怪物死亡共用）、飛蝗石 53 `Scale` 1.2→2.4、霹靂火彈 55 `Scale` 3.6→7.2。屍毒雲 GroundEffect 10 半徑 1.5→**3.0**。<br>
  ⚠ **修正上一輪的錯（通則）**：Parabolic 落地殺傷半徑是 **`AreaRadius × BulletScale`**（`PlayerController.TryApplyParabolicBlast`），而擊中特效大小只吃 VfxTable `Scale`、地面特效只吃 GroundEffectTable `Radius`——**三者互不連動**。上一輪屍毒葫蘆填 `AreaRadius 1.0` × `BulletScale 4.2` ⇒ 實際炸 4.2 單位；這次改 `AreaRadius 0.24`（×8.4 ≈ 2.0）。霹靂火彈 `AreaRadius 1.8 × BulletScale 2 = 3.6`，上一輪特效只有半徑 1.8（傷害比畫面大一倍），這次特效放大後正好對上。**拋物線武器的子彈圖要放大時，記得回頭把 `AreaRadius` 除回去**。
* [x] **特效庫第二批武器化：12 把新武器（武器 36~48、配方 73~85）（⏳ 未編譯未實測、icon 全是代用圖）**（2026-09-24）：作者請 AI 盤點 `DipanProj_MapEditor/Effects` 裡還沒用到、適合當武器的素材，從 15 個提案挑了 12 把。**全部是資料＋素材、零程式改動**（只用現有的 Normal／Parabolic／Orbital 與 `SubWeaponOnHit`）。總表與素材出處見 [EFFECT_WEAPONS.md](EFFECT_WEAPONS.md)〈第二批〉。<br>
  **做法**：飛行物一律從特效庫取、**最近鄰放大 4 倍**（沿用魔狼爪的通則：小像素圖直接當子彈，`BulletScale` 要 30+），存 `Resources/Weapon/animation/<名>/<名>_NN.png`；`SpriteAngleOffset` 一律填 **360**（PROBLEMS E38：填 0 會不旋轉）。`BulletScale` 用「像素 × 0.001 × BulletScale ＝ 世界尺寸」反推出目標大小（多數約 0.5~2 單位）。命中特效另存 `Resources/VfxEffects/`，不放大、靠 VfxTable `Scale` 調。<br>
  **裝備圖／飛行物分圖**：除了霹靂火彈（丟出去的就是那顆彈，icon 與子彈同一張 `weapon_thunderbomb`）以外，11 把都是 icon（`ItemTable.IconPath`）與飛行物（`WeaponTable.WeaponAniPath`）各走各的，跟狂族十字弓同一套。icon 是 AI 用 PIL 畫的**代用圖**（左上角標「代用」），檔名見 EFFECT_WEAPONS，作者之後直接蓋同名檔即可。<br>
  ⚠ **掌心雷跟提案不同**：原想「命中接連鎖閃電」，但 `SubWeaponOnHit` 只支援 Normal 子武器、連鎖閃電是 Chain ⇒ 改成命中迸出 **4 道追蹤電弧**（子武器 **48**，蜂巢→蜜蜂同一套），48 只進 WeaponTable、不進 ItemTable。<br>
  新增：VfxTable **51~55**（血滴子血花／餓鬼咬合／飛蝗石碎塵(重用 ImpactDust 圖、縮小)／掌心雷電爆／霹靂十字爆(X_plosion 64 幀隔幀取 32)）、GroundEffectTable **10 屍毒雲**（`fanfx2_poison` 綠，半徑 1.5、3 秒、每 0.5 秒 1 傷）。重用：7 怪物死亡骷髏煙（引魂幡）、2 冰凍（玄冰針匣）、43 炮彈爆炸(小)＋GroundEffect 8 焚地（落星羅盤）。<br>
  **沒放進抽選池**（`BaseWeaponRoll.csv`）——21~35 號之後的新武器本來就沒進池，要進池作者再決定。數值全是起手值，進武器工坊邊射邊調。
* [x] **魔狼爪改五分裂、角度拉開**（2026-09-24）：配方 72 `SpreadCount` 3→**5**、`SpreadAngle` 30→**100**（每道間隔 15°→25°；月牙判定半徑 1.4 很大，15° 間隔在前幾格距離會糊成一團，看不出分裂）。
* [x] **夢境洞窟禁用武器（只能 WASD 走）＋複查夢境選單鎖**（2026-09-24）：MapsTable 27 `DreamTutorial_Cave` 的 `NoWeapon` 空→**1**（沿用既有「地圖禁武」機制，`PlayerController.CanFire` 會擋；夢境武器照樣在洞窟就裝上，只是到廣場 28 才能開火，廣場 `NoWeapon` 留空）。零程式改動。複查選單鎖：`DreamTutorialFlow.Run` 一開頭 `SetPlayerMenuLock`、離開夢境地圖（27/28 以外）或流程物件銷毀才解 ⇒ 洞窟與廣場全程鎖 B/K/Y/O/ESC 設定；兩張圖上的 trigger 只有 drama／playerHint／teleport／monsterSpawn 這類，沒有 openPanel／selectScript／NPC 這種會開選單的，F 互動也開不出面板。開發工具 L（作弊）、U（UI Demo）、P（效能面板）刻意沒鎖。
* [x] **旱魃焚天火雨：地上火圈範圍 +1/3、火球數 +1/2**（2026-09-24）：火圈半徑是 GroundEffectTable 的表格值、而 8 號現在是全部火焰武器共用 ⇒ **新增 GroundEffect 9「焚地(大)」**（同一組圖，半徑 1.6→**2.13**），只有配方 61 改指 9，其他武器不受影響。配方 61 `SpreadCount` 6→**9**。⚠ 落地爆炸的殺傷半徑（`AreaRadius 0.6 × BulletScale 3`＝1.8）沒動。
* [x] **刪除舊的「火焰燃燒」（GroundEffect 1），全改用自繪的 8；旱魃落地爆炸換成「隕石砸地」（⏳ 未實測）**（2026-09-24）：作者：舊的很難看、直接移除。① 配方 10／11／12（火焰直線彈、8分裂追蹤彈、玩家丟出火焰拋物線彈＝武器 4 火焰拋擲彈）的 `GroundEffectID` 1→8；GroundEffectTable 刪掉 ID 1 那列（**編號不重用**）；ID 8 改名「焚地(火焰燃燒)」。舊素材 `Resources/GroundEffect/fireGround/`（含 .meta）移到專案根目錄 `_to_delete/GroundEffect_fireGround/`（AI 沒有刪除權限）。全專案 grep 過沒有其他地方引用 `fireGround`。② 旱魃落地爆炸：作者從三個候選選 A＝素材庫 `Explosions/epic_explosion_001/orange`（13 幀 128px，砸地→紅橘蘑菇火雲）匯入成 **VfxTable 50「隕石砸地」**。**畫布下方補 96px 透明（128×128→128×224）**，讓原圖的撞擊點（約 y=110）落在圖中心——`VfxManager.Spawn` 以中心對齊、沒有 pivot 偏移可調，不補的話火雲會以落點為中心、撞擊點掉到落點下面。`Scale 2.5`（寬約 3.2＝對齊地上火圈直徑）、18fps；**同日作者實機看過嫌小 ⇒ 放大一倍成 `Scale 5`（寬約 6.4）**。武器 61 `HitEffectID` 1→50；**武器 4 火焰拋擲彈的爆炸仍是舊的 1「爆炸」**（作者只選了旱魃）。文件：GROUND_EFFECT〈配置檔案〉、RECIPE_DESCRIBE 範例、WEAPON_WORKBENCH 範例、BLOODLINE §5d。**通則：素材的「作用點」不在畫布中心時（砸地、腳下起跳…），匯入時補透明把作用點挪到中心。**
* [x] **旱魃焚天火雨的燒地特效重做：自繪「旱魃焚地」（GroundEffect 8）（⏳ 未實測）**（2026-09-24）：作者嫌舊的「火焰燃燒」（GroundEffect 1，Tile 模式把 256×512 的直立火焰一格一格鋪滿圓）很醜，要「地上圓形的著火區域」。素材庫裡沒有圓形地面火的循環素材 ⇒ 用 Python 程序化畫一組像素風：80×80 邏輯像素×2 最近鄰放大＝160px、12 幀無縫循環；三層＝焦黑地面（中心暖紅、邊緣抖動羽化）＋7 條會閃爍的岩漿裂紋＋外圈火環（22 簇外圈火舌＋4 簇內圈＋貼地火帶＋上飄火星）。存 `Resources/GroundEffect/ScorchedGround/`，GroundEffectTable 新增 **ID 8**（半徑 1.6、3 秒、每 0.5 秒 1 傷害、`Single` 模式＝整張縮放到直徑 3.2），配方 61 `GroundEffectID` 1→8。**只換旱魃**；舊的 ID 1 仍被火焰拋擲彈（配方 10/11/12）使用、沒動。生成腳本未存進 repo（要重畫或調色再請 AI 重跑）。
* [x] **焚獄炎杖拿掉命中燒地**（2026-09-24）：配方 70 的 `GroundEffectID 1`／`GroundEffectHitTarget Enemy` 清空（作者：地上不需要著火）。沿路火柱（VfxTable 48）與擊中火焰爆發 17 保留。
* [x] **存檔：夢裡離開＝重做夢、山道裡離開＝從山道開始（schema v4）（⏳ 未編譯未實測）**（2026-09-23）：查到既有 bug——夢境地圖不是 Main、不記 `lastMapId`，夢裡關遊戲後「繼續遊戲」會被當成舊存檔丟到邪佛廣場中央，跳過夢境＋山道。新增 `ProgressDTO.dreamTutorialPending`：新建時 true＋立刻存、第一次進 Main（山道 13）時 false＋立刻存（沿用既有檢查點的 `SaveNow`）、`ContinueGame` 先看它，true 就 `NewGameToDreamRoutine()` 重做。山道那半邊本來就成立（13 是 Main、會記位置、cutscene 重播），沒改。舊存檔缺欄＝false，不受影響。見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)〈新手夢境教學沒做完就離開〉。
* [x] **新手夢境教學：瀕死保護——夢裡不會死，快死了就直接跳結尾（⏳ 未編譯未實測）**（2026-09-23）
  作者擔心：玩家進邪佛廣場完全不攻擊，被小怪打死 ⇒ 走一般死亡流程，整段夢境會壞。拍板「絕對不能進死亡流程；快死就不再出下一波，直接震退＋佛掌」。<br>
  ① **不死保護** `CombatStats.SetDeathGuard(owner, bool)`：受傷最多扣到 1 滴血、不觸發 `OnDeath`。夢境全程開著（`DreamTutorialFlow` 進夢開、`ReleaseDreamLoadout` 關）。這是真正的保證——之後任何時間點（飛行中的子彈、自爆怪的爆炸、結尾演出期間）都死不了。<br>
  ② **瀕死跳結尾** `DreamTutorialFlow.CheckNearDeath`：廣場裡 HP ≤ 20% 且有出生點正在打 ⇒ 新 API `MapMonsterRespawner.AbortActiveWaves()`（不再生怪、場上的怪 `Kill()`、**不推鏈**）、收掉玩家提示、`TriggerChain.Activate("打完小怪後對話")`（drama 42，作者實測後指定：先講「先接下我這掌吧」那句再震退，與正常流程同一條）。會跳過大招提示與後面的波次。<br>
  ③ 為什麼「沒有出生點在打就不跳」：小怪清完、已經在跑結尾時再跳一次會讓震退／骨牢觸發兩次。<br>
  改：`CombatStats`、`MapMonsterRespawner`、`DreamTutorialFlow`、`PlayModeStaticReset`；文件 COMBAT §2、TRIGGER_CHAIN §3.5b〈強制中止〉。
* [x] **蜜蜂圖試換後還原**（2026-09-23）：作者試了一張新的 `weapon_bee.png`（頭朝正右，角度曾改成 360），看過後覺得原本的好 ⇒ 圖從 git HEAD 還原、武器 12／68 的 `SpriteAngleOffset` 改回 **−47**，等於沒變。新圖備份在專案根目錄 `_to_delete/weapon_bee_新圖備份.png`。
* [x] **化神期萬劍歸宗改成冰屬性＋飛劍再換圖**（2026-09-23）：① 配方 60 拿掉命中燒地（GroundEffect 1）；② 武器 60 擊中特效 雷地爆 26 → **冰凍 2**。③ 作者又換了一次 `weapon_sword.png`（500×500 冰晶劍），量劍尖→劍柄：劍尖方向 −134.3° ⇒ 五把飛劍的 `SpriteAngleOffset` 127→**134**；作者要再大 0.3 倍 ⇒ `BulletScale` ×1.3（武器 1/30/31/32：3→**3.9**；60：3.9→**5.07**）。⏳ 未實測。
* [x] **飛劍換新圖後重調大小與角度：五把用 `weapon_sword` 的武器一起改**（2026-09-23）：作者把 `Resources/Weapon/single/weapon_sword.png` 換成新圖（500×500 細長劍，舊圖是 1260×1260 的粗像素劍）。子彈沒有尺寸正規化（大小＝像素 × prefab 0.1 × `BulletScale`），量主軸長度：舊 ≈1725px、新 ≈568px ⇒ **`BulletScale` ×3** 讓飛劍長度回到原本（約 1.7 世界單位）。新圖劍尖方向是 −126.5°（舊圖 −135°），**`SpriteAngleOffset` 135→127** 讓劍尖對準飛行方向。改到：武器 1 三分裂追蹤飛劍／30 三連飛劍／31 飛劍-無能力／32 三道飛劍（1→3）、60 萬劍歸宗（1.3→3.9）。判定半徑 `Radius` 沒動（Normal 的判定不吃 `BulletScale`）。背包 icon 是另一張 `UI/Icons/Equipment/weapon_sword`，不受影響。**通則：換子彈圖時要同時看「畫布尺寸 × 內容佔比」與「圖的朝向」兩件事，兩者都寫死在武器表的數字裡。** ⏳ 未實測。
* [x] **泰坦的山崩地裂：傷害 5→8**（2026-09-23）：中途試過加追蹤（`HomingTurnSpeed 240`），作者看過後改回不追蹤、只要威力強一點 ⇒ 最終只改武器 65 的 `Damage`。⏳ 未實測。
* [x] **冰狼爪 → 魔狼爪（改火屬性、放大 2 倍）（⏳ 未實測）**（2026-09-23）：作者：芬里爾全身是火，拿冰武器很怪。武器 71／配方 72／ItemTable 71 **原地改**：名稱「魔狼爪」、icon `weapon_wolfclaw`、子彈換 `pj1_slash/red`（同樣 4 倍最近鄰放大，存 `Weapon/animation/magicWolfSlash/`）、`BulletScale` 8→16、判定 `Radius` 0.7→1.4（跟著視覺放大）、擊中特效 冰凍 2 → 火焰爆發 17、命中燒地先加後拿掉（作者：打到怪著火就好、地上不用燒）⇒ 最終只有擊中火焰 17。舊的 `Weapon/animation/iceWolfSlash/` 沒人用了（AI 刪不掉，留給作者清）。
* [x] **芬里爾的夢境武器換成新武器「冰狼爪」（三道冰刃劍氣）（⏳ 未編譯未實測）**（2026-09-23）：作者嫌天狼焚爪的爪痕太爛，挑了素材庫 `pj1_slash`（藍，6 幀月牙劍氣），要「發射出去、命中結冰、一次三發、速度中等」。配方 **72**（3 道 30°、速度 12、穿透 3、`Radius 0.7`）、武器 **71**（傷害 8、耗魔 0、`SpriteAngleOffset 360`、`BulletScale 8`、擊中冰凍 2）、**ItemTable 71**（icon `weapon_icewolfclaw`）；表B 芬里爾 `DreamWeaponId` 64→71。**素材放大**：原圖 96×48 太小（子彈 prefab 縮 0.1，要 `BulletScale` 30+ 才看得到、超過工坊上限 20），匯入時用最近鄰放大 4 倍（像素風不糊）存 `Resources/Weapon/animation/iceWolfSlash/`。**通則：小尺寸像素特效要當子彈用，匯入時先整數倍放大**；Normal 的判定半徑不跟 `BulletScale`，要自己對 `Radius`。天狼焚爪 64 與 VfxTable 46 留著、沒人用。
* [x] **該隱的夢境武器換成新武器「血鳴古鐘」（直線一路炸開）（⏳ 未編譯未實測）**（2026-09-23）：作者嫌血月雙爪太弱，挑了素材庫 `fanfx2_shatter`（紅，圓球炸開），要「一直線放 5~6 個、一路放到螢幕外、不追蹤不分裂、比怪大一點、比焚獄炎杖慢」。同樣是**隱形載體＋`TrailStep`**：配方 **71**（速度 5、`LifeTime 2.4`≈12 單位、`TrailStep 2`＝約 6 團且前後重疊成連爆、`Radius 1.3` 讓判定對齊炸開的大小、無限穿透＋`BlockedByEnvironment 0` 穿牆保證炸到螢幕外）、武器 **70**（傷害 10、耗魔 0）、VfxTable **49** 血鳴碎響（27 幀 192px、Scale 1.8≈直徑 3.3，匯入 `Resources/VfxEffects/BloodBellShatter/`）、**ItemTable 70**（icon `Weapon/single/weapon_ancientbell`）；表B 該隱 `DreamWeaponId` 63→70。舊的血月雙爪 63 留著、沒人用。**注意**：傷害來自載體本身（沿線 1.3 半徑的帶子），不是每團爆炸各打一次——每隻怪一條線只吃一次傷害。
* [x] **尼德霍格的夢境武器換成新武器「焚獄炎杖」（地火一路燒向目標）（⏳ 未編譯未實測）**（2026-09-23）：作者挑了素材庫 `Fire/directional_fire_burst_001`（紅），要「像地裂刺一樣從地上一路噴火燒向目標、多方向、追蹤、打到會燃燒」。做法完全沿用地裂刺：**Normal 隱形載體＋`TrailStep` 沿路種特效**，只是多了追蹤與燒地。新增：VfxTable **48** 地獄火柱（12 幀 48×96；`Scale` 2.2 → 作者實機看過嫌不夠高、放大 1.5 倍成 **3.3**；匯入 `Resources/VfxEffects/HellfireTrail/`）、配方 **70**（5 道 80°、追蹤 240、穿透 2、`BurstCount 2`、`TrailStep 0.8`、命中燒地 GroundEffect 1）、武器 **69**（傷害 5、耗魔 0、擊中特效 17）、**ItemTable 69**（作者給的 icon `Weapon/single/weapon_firestaff`，是正式道具但沒進抽選池）；表B 尼德霍格 `DreamWeaponId` 62→69。舊的龍息 62 留在表裡、目前沒人用。
* [x] **修「龍息的火焰被佛掌蓋住」——火團特效 SortingOrder 寫死 8（地面帶）**（2026-09-23）：龍息沿路種的是 VfxTable 4「火球」，`SortingOrder=8`＝排序表的「地面特效」帶，而佛掌（怪物）在 Y 排序帶 7000 起跳 ⇒ 火一定在牠下面。與 PROBLEMS **E35**（火焰噴射器被地毯蓋住）同一個成因。修：新增 **VfxTable 47**（ID 4 的複本、`SortingOrder` 留空＝VfxManager 預設 22000），龍息 `TrailEffectID` 4→47；**ID 4 不動**，所以火焰噴射器（武器 7）維持原樣。⏳ 未實測。
* [x] **新手夢境教學：八個三階血統各配一把夢境武器＋夢裡鎖玩家選單（⏳ 未編譯未實測）**（2026-09-23）
  作者要「拿現有武器調欄位，讓玩家在夢裡玩得很爽」，不做新武器功能。全部用新的 **60 號段**（配方 60~69、武器 60~68），既有武器零變動；對照表見 [BLOODLINE.md](BLOODLINE.md) §5d。<br>
  ① **裝備方式＝劇情武器覆寫**：`WeaponManager.SetScriptedOverride`（優先序 工坊模擬 ＞ 劇情 ＞ 背包），`PlayerController.SetScriptedWeapon` 負責換武器收尾（連擊／光束／佛光／集氣）。**不進背包、不進存檔**，清掉就是回收——所以不需要 ItemTable 列、也沒有「醒來背包多一把」的清理問題。表B 新欄 `DreamWeaponId`（第 26 欄），`DreamTutorialFlow` 在外觀換好後裝上、離開夢境／OnDestroy 收回。<br>
  ② **玩家選單鎖** `UIManager.SetPlayerMenuLock(owner, bool)`：夢裡 B/K/Y/O/ESC 設定全開不起來（作者：「不能開任何 UI 以免出 bug」）。見 [UI_SYSTEM.md](UI_SYSTEM.md)〈玩家選單鎖〉。<br>
  ③ 芬里爾改近戰後原本只有血月鬼爪一種爪痕素材 ⇒ 從素材庫匯入 `fanfx2_claw/orange`（13 幀、128px）成 **VfxTable 46**（`Resources/VfxEffects/FenrirClaw/`），與該隱的紅爪區隔。<br>
  **通則**：① 放射狀分裂角度填 `360 − 360/N`，填 360 頭尾兩顆會重疊。② 「起火」目前只能用命中點放 GroundEffect 1（地面燒 3 秒），怪身上的燃燒狀態還不存在；高頻命中的武器（雷射每 0.2 秒一 tick）別掛，會疊出上百塊地面動畫。③ 拋物線的 `BulletScale` 會同時放大爆炸半徑，想要大火球就把 `AreaRadius` 反除回去。<br>
  改：`RecipeTable`/`WeaponTable`/`VfxTable`/`BloodlineTable`（CSV）、`WeaponManager`、`PlayerController`、`BloodlineTable.cs`、`DreamTutorialFlow`、`UIManager`、`StorageBagCoordinator`、`SettingsLauncher`、`PlayModeStaticReset`；文件 BLOODLINE §5d/§7、UI_SYSTEM。
* [x] **射手型可逐怪取消「觀察」：MonsterData 表尾新欄 `ObserveTime`（索引 37）**（2026-09-23）：作者回報新手夢境教學裡玩家火力太強，ZhaYu_Gun「觀察」完幾乎沒機會出手。查下來 `ArcherBrain` 沒有「觀察次數」，延遲是 ①發現後 Observe 0.4~0.8s ②射程外每挪一步（1.2~2.2 單位）又停一次 Observe ③生成後武器起手緩衝＝`FireInterval`（配方 46＝0.9s）疊出來的。新欄：留空＝原行為；**填 0＝①② 歸零＋取消 ③**（`MonsterWeaponUser.Resolve` 看 `_owner.ObserveTime == 0`）；正數＝固定秒數。**只有 32 號（夢境 ZhaYu_Gun）填 0**，21 號與 18 號狂族弩手留空零變化。舉槍到放彈（`ReleaseFrame`）、「舉了就一定射」鐵則、射後間隔都沒動。改：`MonsterData`/`MonsterSpawner`/`MonsterController`（Inspector `Observe Time`）/`ArcherBrain.ObserveSeconds`/`MonsterWeaponUser`；文件 BOSS_MODULE §8.1、MONSTER_SETUP 表尾欄位總表。⏳ 未編譯未實測。
* [x] **修「ZhaYu_Bomb 被打死不播死亡特效」——自爆 brain 一掛上就把 `DeathVfxId` 歸零**（2026-09-23）：`SuicideBombBrain.EnsureConfigured()` 在第一次 Think 就設 `self.DeathVfxId = 0`（原意：自爆有火球 VfxTable 42，不疊死亡煙霧），連帶讓「被玩家打死」也不播 ID 7；其他 ZhaYu（Chase/MeleeChase/Archer）不會動這個值所以正常。當初註解與 BOSS_MODULE §11 寫明是刻意設計，作者改拍板為「被打死要播」。修：歸零移到引爆流程 `Kill()` 前一行，被打死走 `TakeDamage→Die` 不經過那裡 ⇒ 照常播。BOSS_MODULE §11 同步改寫。**通則：brain 的一次性設定（`EnsureConfigured`）只放「任何死法都成立」的東西，只屬於某個動作的副作用要放在那個動作裡。** ⏳ 未編譯未實測。
* [x] **修「夢境洞窟北傳送點很難觸發」——踩踏矩形用預設 1.0×0.6 太小**（2026-09-23）：`DreamTutorial_Cave` 的「傳送點-北方」有錨點 (9.04, −2.31) 但沒填 `markerW`/`markerH`，退回預設 1.0×0.6（x 8.54~9.54、y −2.61~−2.01）。判定點是 `transform.position`（胸口，B13），玩家站進凹口、看起來已經踩在門上時，胸口約在 (8.35, −1.7)——偏左又偏上，落在小矩形外。修：補 `markerW=2.0`、`markerH=1.4`（x 8.04~10.04、y −3.01~−1.61，蓋住整個 2 格寬凹口），錨點不動（光盤位置、落點不變）。三份 .dipanmap 同步、md5 一致。**通則：有錨點的傳送點一定要依門洞實際大小填寬高，預設值只適合窄門。** ⏳ 未實機驗證。
* [x] **夢境收尾調整：佛掌碰到才觸發＋傷害 0＋新螢幕特效（最終：id 5 馬賽克淡出；id 4 淡出黑幕保留）（⏳ 未編譯未實測）**
  （2026-09-23。作者：「佛掌離玩家還太遠，碰到再結束沒關係，攻擊力填 0 以免弄死玩家」；「轉景前播個像破幻術的新特效」，從 4 個提案選了「漩渦吸入」）<br>
  ① `佛掌逼近` 距離 1.5 → **0**（身體碰到才觸發）；MonsterData **34 Buddha_Hand `ContactDamage` 10 → 0**。<br>
  ② 新螢幕特效 **id 4 夢境漩渦**：越轉越快（中心扭最兇）＋往中心吸入收成一點＋旋轉殘影＋外圈染暗紅＋收尾全黑。
  鏈改成 `被佛掌擊中前對話(43) → 夢醒漩渦(playScreenFx 4；後改名「夢醒黑暗吞噬」) → 夢醒回山道(teleportTo 13)`。見 [MAP_ENTER_EFFECT.md](MAP_ENTER_EFFECT.md)〈夢境漩渦〉。<br>
  新增：`DreamVortexController.cs`、`DreamVortex.shader`、ScreenFxTable 第 4 列、`ScreenFxPlayer` case 4、編輯器 `ScreenFxCatalog` 一列。<br>
  🔧 **同日換掉**：作者看完說漩渦「太 low」，要「跟佛掌出現時的場景吞噬類似，但這次全部轉黑、什麼都不留」⇒ id 4 改成 **黑暗吞噬**
  （`DreamDevourController`＋`DreamDevour.shader`：螢幕後處理版的吞噬線，圓心每幀跟玩家、同一組蠕動起伏與柔邊、純黑、2.5 秒）。漩渦的檔案已移除。<br>
  🔧 **再換一次**：黑暗吞噬作者也說不行，要「用破幻術一樣的特效，切得更細，像鏡子破掉」⇒ id 4 改成 **鏡碎**：
  `DreamShatterController` **直接共用** `IllusionShatter.shader`（加六個參數、預設值＝破幻術原樣）——密度 30、黑底、冷銀裂紋、重力、
  從撞擊點（玩家）一圈圈崩、翻轉鏡面反光。黑暗吞噬的檔案已移除；地圖 trigger 改名「夢醒鏡碎」。<br>
  🔧 **定版：鏡子一步一步裂開**（作者描述：「先一條長裂痕、過 1 秒又一條不同方向、再一條，最後這幾大片像鏡子碎片掉落，整個畫面變黑」、裂痕方向每次隨機）。
  新 shader `MirrorCrack.shader`＋重寫 `DreamShatterController`（時間軸全在 C#：裂痕 0/1/2 秒、錯位、掉落、震動）。
  碎片＝在 3 條裂痕的哪一側；裂痕與碎片邊界同一條帶鋸齒距離函數；掉落用反推取樣；碎片重心 C# 格點統計；隨機方向兩兩夾角 ≥ 38°。
  **破幻術 shader 已還原成原樣**（上一版為了共用加的六個參數拿掉）。總長 4.35 秒。<br>
  🔧 **外觀照照片重做**（作者附真實碎鏡照片：「裂痕線太直了」）：放射狀彎曲長裂痕（極座標 θ(r) 曲線、共用彎曲率保證不交叉）＋
  中心多邊形蜘蛛網（第一次用三角波圓弧變星星狀，改成頂點＋弦）＋分岔＋短裂痕＋厚度（亮芯／暗邊／折射）。
  碎片改成「相鄰長裂痕之間的扇形＋中心一片」，節奏維持 0/1/2 秒三批。出圖前先用 Python 把 shader 邏輯移植到 numpy 在實機截圖上渲染驗證過。<br>
  🔧 **最終定案：簡單淡出**（作者：「都不太行，鏡子特效砍掉，做簡單的淡出就好」）。id 4 改成 **淡出黑幕**（`FadeOutController`＋`ScreenFadeOut.shader`，1.5 秒、暫停＋鎖輸入）；
  鏡碎相關檔案（`DreamShatterController`、`MirrorCrack.shader`）已移除，地圖 trigger 改名「夢醒淡出」。<br>
  🔧 **淡出也不行 → 馬賽克淡出（新 id 5）**：作者要「馬賽克清晰倒過來」。`MosaicOutController` 共用 `Mosaic.shader`、曲線倒放（清晰→越來越粗＋沉暗→全黑，2 秒、暫停＋鎖輸入）；
  地圖 trigger 改名「夢醒馬賽克」、effectId 5。**淡出黑幕（id 4）保留不刪**（作者：之後說不定用得到）。<br>
  ⭐ 通則（這一串的教訓）：**過場特效先做最樸素的版本讓作者在流程裡實際看**，確定真的需要花俏再加——這次連做五版都被否決，淡出反而最合適。<br>
* [x] **夢境收尾：佛掌逼近 → drama 43 → 傳回山道（drama 12 接原流程）（⏳ 未編譯未實測）**
  （2026-09-23，作者要求能用地圖編輯器就用。見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)〈monsterNear〉、§3.5b `nextWhen`）<br>
  全部用 trigger 排：`場景吞噬 → 邪佛手掌出生點(接續時機=生出來時) → 佛掌逼近(monsterNear 34, 1.5) → 被佛掌擊中前對話(43) → 夢醒回山道(teleportTo 13)`。
  山道(13)本來就是 `onEnter → drama 12 → …`，所以傳過去就自動接回原流程，不用動山道地圖。<br>
  新增兩個東西：① 出生點 `nextWhen`（全滅後／生出來時）——佛掌不會死，「全滅才接」永遠接不上；
  ② 鏈動作 `monsterNear`（量兩個碰撞框之間的空隙；觸發時把怪設成 `Caged` 停住——讀取頁不暫停，不停住會在讀取頁後面撞上玩家扣血）。
  `MonsterController.DataId`（MonsterData ID）。<br>
  ⚠ **順手修一個潛伏 bug**：骨牢束縛以為換圖時 `PlayerBind.OnDisable` 會解，但**玩家物件跨圖是同一個、不會被停用** ⇒
  傳回山道後玩家會「不能動＋腳下頂著骨牢」。改在 `MapManager.ClearTransientGameplay` 解綁（TRIGGER_CHAIN 那段說明一併改正）。<br>
  改動：`MonsterNearWatcher.cs`（新）、`TriggerChain`、`MapMonsterRespawner`、`MapLoader`、`MonsterController`、`MapManager`、
  編輯器 `TriggerType.cs`＋`triggerTypes.json`、三份 `DreamTutorial_Square.dipanmap`。<br>
* [x] **夢境廣場改三波＋「按 E 施放大絕招」提示；`playerHint` 收起時機加 `E鍵`（⏳ 未編譯未實測）**
  （2026-09-23，作者要在打一陣子後插「按 E 施放大絕招」——三階大招還沒做，**提示就只是提示**，不偵測有沒有真的放）<br>
  作者選 A 案：每波一顆出生點、用鏈串 ⇒ `怪物出生點1 → 大招提示(E鍵收、顯示時接) → 怪物出生點2 → 怪物出生點3 → 打完小怪後對話`。
  出生點 2、3 是照 1 複製的（同格子、同怪、`maxWaves=1`、`waveGroup` 2/3），**生怪設定由作者自己調**。
  新增 LanguageTable **1012**「按 E 施放大絕招」。未做：提示逾時自動收起（作者未決定要不要）。<br>
  改動：`PlayerHintPanel.HideMode.KeyE`、`TriggerChain.ParseHideMode`、編輯器 `TriggerType.cs`＋`triggerTypes.json`、`LanguageTable.csv`、三份 `DreamTutorial_Square.dipanmap`。<br>
* [x] **`playerHint` 加「接續時機」：夢境攻擊提示改成不擋流程（⏳ 未編譯未實測）**
  （2026-09-23，作者：「跳提示的同時怪就照常產生，只是上方跳提示；不按攻擊就一直被打，按了提示再移除」）<br>
  `playerHint` 的 next 原本一定要等玩家做出收起動作才接 ⇒ 新增 `nextWhen`（收起時＝舊行為／顯示時＝一跳出來就接 next）。
  夢境「左鍵發射教學」改 `顯示時`（`pause=false`）⇒ 對話 → 提示＋怪物出生點同時開始。
  改動：`TriggerChain.ExecutePlayerHint`、編輯器 `TriggerType.cs`＋`triggerTypes.json`、三份 `DreamTutorial_Square.dipanmap`。<br>
* [x] **第一次生怪卡頓修正＋場景吞噬加「吞完後才接續」（⏳ 未編譯未實測）**
  （2026-09-23，作者回報「按下左鍵產怪時一瞬間停滯」、「先全黑，佛掌再出現」）<br>
  ① **卡頓**：怪物庫有自己的 `MapSpriteLoader`，讀取頁的 module 預載只暖到 MapLoader 那一份 ⇒ 每種怪第一次生出來時同步解碼全部幀
  （夢境四種 ZhaYu ≈ 兩百多張）。⇒ `MonsterSpriteLibrary.PreloadModuleRoutine` 在讀取頁分幀預載（連同逐幀腳底 px／腳底基準／影子錨點），
  `MapLoader` 預載跳過怪物圖（原本白解一份還多佔記憶體）。見 PROBLEMS **E39**。<br>
  ② **順序**：`sceneVanish` 加 `nextWhen`（開始時／吞完後）；夢境改「吞完後」⇒ 先全黑 3 秒，佛掌再從黑暗裡出現。
  （`SceneVanish.Play` 加 `onDone`；開不成／重複觸發也會立刻回呼，鏈不會卡住。）<br>
* [x] **新鏈動作 `sceneVanish` 場景吞噬＋夢境佛掌段接上（⏳ 未編譯未實測）**
  （2026-09-23，作者問「佛掌逼近時能不能讓整個場景消失，只剩佛掌、玩家、骨牢」。見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3.4b〈sceneVanish〉）<br>
  作者選的樣式：**由外往內吞噬**／**極暗的血紅虛空**／**佛掌先出現、約 1 秒後才開始吞**。<br>
  ⭐ **不能用黑幕**：角色和地上物同一條 Y 排序帶 ⇒ 改成讓地圖自己消失——背景與預設材質地上物換 `Custom/SceneVanish`
  （global 參數、全圖共用一個材質，吞噬線才在每張圖之間接得起來；圈外換成虛空色**不是變透明**，地上物疊在虛空背景上才會真的看不見），
  其餘（光源、場景特效、掉落物、互動星星）吞噬線越過就關。<br>
  🔧 **作者第一次看完調整**：夢境這顆改成**純黑虛空、不燒紅邊**（`voidColor`＝`edgeColor`＝`#000000`；燒邊是疊加色，填黑＝沒有邊）、
  `startDelay=0`（佛掌一出現就開始吞）。預設值（血紅）不動，留給之後別的劇情用。
  同一輪：「左鍵發射教學」`playerHint` 改成**不暫停**（`pause=false`）——提示照跳、遊戲照走，玩家真的按下左鍵才收提示、接怪物出生點；
  暫停版是時間凍住、一按就瞬間湧怪，作者覺得突兀。<br>
  ⭐ **順序問題**：`monsterSpawn` 的 next 要怪全滅才觸發、佛掌不會死 ⇒ 鏈改成 `骨牢束縛 → 場景吞噬(立即交棒, startDelay=1) → 邪佛手掌出生點`。<br>
  改動：新增 `Map/SceneVanish.cs`、`Resources/Shaders/SceneVanish.shader`；`MapLoader.MapRoot`；`TriggerChain`（`sceneVanish`）；
  編輯器 `TriggerType.cs`＋`triggerTypes.json`；三份 `DreamTutorial_Square.dipanmap`（md5 相同）。<br>
* [x] **夢境教學的束縛骨牢（`bindPlayer`）完善：永久＋不被擊退＋對位與骨牢武器同一套（⏳ 未編譯未實測）**
  （2026-09-23，骨牢武器對位定版後回到夢境教學。見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3.4b）<br>
  ① **永久**：本來就沒有計時器（只有 `bind=0` 或換圖／死亡會解），這次把它寫成明文規格，與骨牢武器（有秒數）分開講清楚。<br>
  ② **不會被擊退**：玩家的 `PlayerKnockbackThreshold=0`＝每一下都擊退，被佛掌一碰就會連人帶籠滑出去 ⇒
  `HitReactionHandler.SuppressKnockback`（受擊照常、只是不位移），`PlayerBind` 綁住時開、解開時關。<br>
  ③ **對位**：玩家端原本還在每幀問影子（骨牢武器已經證明那會隨動作滑動）⇒ 把演算法從 `MonsterAnimator` 抽到
  `BoneCageVisual.ComputeIdleAnchor`／`MakeSpot`／`InnerWidthFor` 共用，`PlayerAnimator` 加 `TryGetCageAnchorLocal`。
  排序每幀同步（G16）玩家端本來就吃得到。實測全部 24 個血統：軀幹偏移多數 < 5px、只有 `Swarm Emperor` −20px；
  軀幹寬只有 `MountainGiant`（0.66×身高）、`Fenrir`（0.63）會讓牢籠比「身高 × 0.7」大一點。<br>
* [x] **骨牢對位第四版：籠心對「軀幹」、不再每幀跟影子（⏳ 未編譯未實測）**
  （2026-09-23，作者附 ZhaYu／ZhaYu_Bomb／ZhaYu_Gun／ZhaYu_HugeSword 四張實測圖：「幾乎每隻被關住後對位都是歪的」。見 PROBLEMS **G15**）<br>
  **根因**：第三版的籠心問影子，而影子 X 是「兩腳中點」——拿武器的怪兩腳之間被拖地的武器佔住，
  `ZhaYu_HugeSword` 影子在軀幹右邊 52px（≈0.6 單位，正好是截圖裡的偏移）；再加上影子錨點**逐動作不同**，
  被關的怪原地揮刀時籠子會跟著左右滑。<br>
  ⇒ `MonsterAnimator.TryGetCageAnchorLocal`：**只用 idle、算一次、快取**——X＝可見框上方 60% 的像素欄質心（軀幹）中位數、
  Y＝idle 影子錨點 Y（地面線不動）；`MonsterCage.BuildCageSpot` 套上位置／體型／翻面／離地高度，
  經 `BoneCageVisual.Spawn` 新參數 `anchorSpot` 傳入（優先於影子；取不到才退回影子）。玩家端 `PlayerBind` 不變。
  `[BoneCage]` log 多印「籠心與當下影子差多少」。<br>
  ⚠⚠ **第一次實測四隻全往左偏 0.6~1.0（比原本更歪）**：量像素用了 `sprite.textureRect`——`Sprite.Create` 的 Tight 網格會裁掉透明邊，
  那是**裁過的框**，X 少算了左透明邊寬。用「軀幹 − 左透明邊」預測 ZhaYu/Gun/Bomb 為 −59/−90/−43px，log 反推 −50/−93/−42，吻合 ⇒ 改用 `sprite.rect`（PROBLEMS G15）。<br>
  ⚠⚠ **第二次實測（位置大致對了，只有 ZhaYu_Gun 完全正確）作者點出兩件事**：
  ① **後面那張的中柱畫在怪身上**：前後片的排序只在 Spawn 抓一次，但怪的排序是 `YSortByFeet` 每幀依腳底 Y 算、每 0.01 單位差 1；
  被關那一幀怪還在走 ⇒ 排序跑掉、back 翻到前面。Gun 是站定射擊的所以剛好對 ⇒ `SyncSorting` 每幀對齊＋`DefaultExecutionOrder(1000)`（PROBLEMS **G16**）。
  ② **要把怪完整包在籠裡**：內徑改成 `max(身高×0.7, 軀幹寬×1.15)`——`ZhaYu_Bomb` 軀幹寬 0.85×身高，只看身高兩手會伸到骨刺外；
  其他三隻仍由身高決定、大小不變（`MonsterAnimator.CageTorsoWidthLocal`、`BoneCageVisual.InnerWidthPerTorsoWidth`）。<br>
  ✅ **第三次實測作者確認對位準了**，只剩一件：狂族皇家衛士揮砍時被關，**一直定格在 attack 最後一幀直到骨牢碎掉**——
  Brain（MeleeChase／LeapSlam）用 one-shot 播揮砍，播完要靠 `Think()` 呼叫 `CancelOneShot`，而被關時 `Think()` 整個被跳過。
  ⇒ `MonsterController.Update` 的 Caged 分支自己收尾：one-shot 播完就 `CancelOneShot`；HandleVisuals 自動循環的 attack
  用新的 `MonsterAnimator.FinishAttackCycle()` 轉成「從目前這幀播到最後一幀就停」；之後 `HandleVisuals(null)`（不自動舉刀、不轉身）。
  Brain 放開後的「播完了沒」都有時間保底（`_phaseUntil`／`_tEnd`），提前收掉不會卡死。<br>
  ⭐ 通則：**腳下的東西對影子、罩住身體的東西對軀幹**；會停留的實體特效錨點要**固定一次**，不要跟著逐動作變的錨點走。<br>
  ⚠ 順帶發現（**未處理，待作者決定**）：`StreamingAssets/.../Monsters/SequenceImage/ZhaYu/` 的 idle 混了一張舊的 500×500 `idle_01.png`、
  walk 混了 8 張舊的 500×500 `walk_01~08.png`（與 21 張新的 256×256 同資料夾；Main 與 Tutorial 模組兩份都有）——
  同 PROBLEMS **F28**，會讓 ZhaYu 播到那幾幀時忽大忽小、位置跳。`GameAssets/Main/.../ZhaYu/walk` 則**只有**舊的 8 張。<br>
  改動：`AI/MonsterAnimator.cs`（新增 `TryGetCageAnchorLocal`）、`Combat/MonsterCage.cs`（`BuildCageSpot`）、`Combat/BoneCageVisual.cs`（`anchorSpot` 參數＋log）、`BlobShadow.cs`（註解）。
* [x] **骨牢做成玩家武器（新模式 `Cage`）＋ 正式骨牢視覺（前後夾層＋從地裡長出來）（⏳ 未編譯未實測）**
  （2026-09-22，作者畫好 `bone_prison_back/front` 兩張圖與骨杖 icon。
  見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)〈Cage 骨牢〉、[MONSTER_SETUP.md](MONSTER_SETUP.md) `Controllable` 欄）<br>
  **一句話**：施放時從半徑內隨機挑一隻怪關住 N 秒，時間到牢籠崩裂並對牠結算一次大傷害。
  作者拍板的四件事：**只鎖移動、放行攻擊**／**困住期間照常可以打**／**boss 靠 CSV 欄免疫**／**同時上限做成可鑲珠加成**。<br>
  ⭐ **「珠子能加困住上限」是零額外成本**：能力珠的有效性本來就自動走 `WeaponModeSpec`
  （`GemTable.Field` 直接對應 CSV 欄名），所以只要把 `CageMaxTargets` 登記成 Cage 模式的有效欄，
  之後 `GemTable.csv` 加一列 `Field=CageMaxTargets`、`Target=Recipe` 就有珠子了，**不必再改程式**。
  加模式／加欄只改 `WeaponModeSpec.cs` 一個檔（載入檢查、珠子、武器工坊視窗自動跟上）。<br>
  ⭐⭐ **怪物定身的攔截點在 `MonsterController.Update` 總入口，不是各 Brain**：
  移動是 Brain 透過 Actuator 下的，逐一去改每個 Brain 一定會漏，而且之後每加一個新 Brain 都要記得處理。
  在總入口攔一次（清 velocity、跳過 `Think()`、照樣跑 `HandleVisuals`）才是單一真相——
  而且近戰動畫與攻擊判定不在 `Think()` 裡，所以**照樣會揮**，正好就是「只鎖移動、放行攻擊」要的。<br>
  ⚠ **選目標不能用 `Physics2D.OverlapCircle`**：專案全域 `queriesStartInColliders=false`，
  貼身重疊的怪反而抓不到（**B7**）。走 `MonsterController.Active` 登記表。
  排除自己的召喚物（PlayerAlly）與中立 NPC——關住自家友軍或村民只會讓人困惑。<br>
  ⚠ **崩裂傷害走 `CombatSystem.Apply`，不直接扣血**：直接扣吃不到減傷、加成與浮動傷害數字，珠子也對它無效。<br>
  ⚠ **`Controllable` 預設「可控」而不是「免疫」**：反過來的話每加一隻新怪都要記得填，忘了就抓不住、而且沒有錯誤訊息。<br>
  ⭐⭐⭐ **視覺不能做成 VfxTable 一列**：`bone_prison` 是**前後夾層**（back 畫在角色後、front 畫在角色前，
  角色夾在中間才像被關住），而 VfxTable 一列只有一個 `SortingOrder`，表達不了。
  所以新增元件 `BoneCageVisual`（兩片 SpriteRenderer，角色是 10 ⇒ back 9／front 11），
  **玩家被綁與怪被關共用同一份**。<br>
  ⭐ **「長出來」是兩件事合起來的，缺一個都不像**：① shader `Custom/BoneCageGrow` 由下往上揭露，
  **門檻依 X 抖動 ⇒ 三根骨刺錯開破土**（單一條水平揭露線看起來像「被地平線切開」）；
  ② 元件對根節點做**縱向超調回彈**（0.72 → 1.06 → 1.0）。①負責冒出來、②負責力道。<br>
  ⚠ **縮放支點在「地面線」而不是圖的中心**（`GroundLineFromBottom`）：
  支點放中心的話，縱向縮放會讓地上的血色法陣跟著上下彈，一看就是在縮圖而不是在生長。<br>
  ⭐⭐⭐ **對位改成跟著「影子」走（同日第二版，作者回報第一版「困得不準」）**。
  第一版自己用 `FeetWorldPos` ＋ 目測的身高倍率算位置與大小 ⇒ 套上去偏掉。
  根因不是常數估錯（實測圖裡的地面線就是 0.33、骨刺內徑就是圖寬的 0.652，目測值是準的），
  而是**「角色站在哪一點」被算了第二份**：影子的錨點是逐角色逐動作量過、已定版的
  （`ShadowAnchorTable.csv`，見 [SHADOW.md](SHADOW.md)），自己另算一份必然對不起來，
  而且**腳下的圈跟影子沒疊在一起，玩家一眼就看得出來**。<br>
  ⇒ `BlobShadow` 開一個 `TryGetGroundSpot(out 中心, out 寬)`（寬回傳**地面尺寸** `_baseW`，
  不是當下 localScale——騰空時影子會縮小，拿那個當基準會讓腳下的東西忽大忽小），
  骨牢的位置與大小全部問它。大小也從「身高 × 倍率」改成「**影子寬 × 倍率**」——
  牢籠是地上的圈，本來就該對齊腳底的範圍而不是身高；體型大的怪影子也大，尺寸自動跟著對。
  問不到影子時會**印警告**說明改用了退路座標（悄悄換一套座標正是查不出原因的那種 bug）。<br>
  ⚠ **順手補一個第一版的漏**：`WeaponModeSpec` 宣告了 Cage 吃 `BulletScale`（工坊上的「牢籠大小」），
  但 `Shoot` 沒把它傳下去 ⇒ **那個旋鈕是死的**。宣告了有效欄卻不讀，比沒有這一欄更難查。<br>  ⭐⭐ **第三版：大小改用「可見身高」，只有位置用影子**（作者第二次實測，骨牢大了一倍多）。
  第二版把**位置與大小都**綁在影子上——位置是對的，大小不是。
  影子寬量的是「底部 15% 帶的跨距」，而**拿武器的怪會把拖在地上的武器一起算進去**：
  實測 `ZhaYu_HugeSword` 影子寬 **3.04**、可見身高才 **2.63**（影子比怪還寬；底部跨距 130px，兩腳根本沒那麼開）。
  ⇒ 位置繼續問影子（站立點的單一真相），**大小改用可見身高 × 0.7**——身高不受手上拿什麼影響。
  順手加一則診斷：影子寬 ＞ 可見身高時直接指出「那隻角色的影子錨點多半把武器算進去了」，
  因為那會讓**影子本身**也偏，不只是骨牢。<br>
  ⚠ **同一次實測釐清了一件事**：作者看到「怪在骨牢左上角」，其實骨牢**套在另一隻怪身上**
  （Console 顯示套的是 `ZhaYu_HugeSword`，畫面上那隻是別隻）——`Cage` 是「半徑內**隨機**挑一隻」，
  場上有多隻時玩家無從得知困到誰。**這是體驗問題不是 bug**。作者拍板：**改成「半徑內 ＋ 畫面上看得到」才納入候選**，隨機性保留。
  ⭐ 通則：**玩家看不到的地方發生的事，等於沒發生**——按下去沒反應的那一發，玩家只會當作武器壞了。<br>
  ⚠ **順帶掃出 6 列過期的影子錨點**（表裡記的畫布／幀數與實際素材不符；`BlobShadow` 會按比例硬湊、不報錯）：
  `monsters/zhayu/walk`（表 500x500/8 幀 → 實際 256x256/**29** 幀）最嚴重、
  `zhayu_hugesword/attack`（25 → **50** 幀）次之，其餘四列只差 1 幀。**要重算一次**。<br>
  ⭐ **兩個「目測常數」改成量出來的**：地面線 0.33、牢籠內徑佔圖寬 0.652，都是對素材做像素分析得到的，
  換圖時重量一次即可（方法記在 `BoneCageVisual` 檔頭）。<br>
  ⚠ **`_Grow=1` 時揭露上界要補 `+_GrowJitter`**，否則抖動最大的那幾列會被切頭 ⇒ 骨刺永遠少一截、而且不會報錯。<br>
  ⭐ **崩裂零新素材**：重用地上物破壞的 `ShatterBurst`（3×4 切塊拋飛淡出）。
  ⚠ 碎片是掛在**本節點底下**的，所以不能馬上 `Destroy` 根節點——要等過碎片壽命（0.6s），否則碎片跟著一起消失、什麼都看不到。<br>
  **夢境教學同步換掉暫代素材**：`bindPlayer` 的 `cageVfxId` **留空 ＝ 用正式骨牢**，填 VfxTable ID 才走舊的單層循環特效
  （暫代的 ID 45 ＝ `EarthSpik2` 染白，留著當退路）。`DreamTutorial_Square` 已改成留空，三份 `.dipanmap` 同步（md5 相同）。<br>
  新增：`WeaponMode.Cage`、`BoneCageVisual.cs`、`MonsterCage.cs`、`Resources/Shaders/BoneCageGrow.shader`；
  `RecipeTable` 表尾 4 欄 ＋ 配方 **47**；`WeaponTable`／`ItemTable` **35 枯骨牢杖**（icon 用作者的 `weapon_bonestaff`）；
  `MonsterData` 表尾 `Controllable`；`MonsterCage.ResetForPlayMode` 已進 `PlayModeStaticReset`。<br>
  ⏳ **待調**：牢籠倍率 `HeightMul` 1.55、地面線 0.32 都是**目測值**，實機看過再調；
  兩張圖自帶暗角與紅輝光，暗場景可能過亮（**E11/E12**），shader 留了 `_Dim`／`_Desat` 兩個旋鈕。
