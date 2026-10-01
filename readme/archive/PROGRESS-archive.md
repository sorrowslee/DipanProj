# PROGRESS 封存區（歷史條目原文照錄）

> 由 [../PROGRESS.md](../PROGRESS.md) 依大小封存規則搬入（規則見 [../DOCS_GUIDE.md](../DOCS_GUIDE.md)）。
> 條目內容**原文一字未改**；本檔**正序（最舊在最上）**。2026-08-21 第一次搬入時，原檔為
> 兩段式（前段倒序＋後段正序），本檔已統一為正序，僅調整條目排列順序、內容不變。
> ⚠ 條目內的相對連結（如 `PROBLEMS.md`、`LASER.md`）是以**原位置 `readme/` 為基準**寫的，封存後差一層目錄——查閱時自行對應到 `../<檔名>`，不改原文。
> 這些是**當時的紀錄快照**：內文描述的機制可能已被後續開發改掉，現狀一律以主題文件與程式碼為準。
> 2026-08-27 第二次搬入：正檔 2026-08-18 ~ 2026-08-22 的 22 條（正檔當時 132KB，超過 96KB 門檻），接在本檔尾端、索引同步補上。
> 2026-10-01 第三次搬入：正檔 2026-08-22 ~ 2026-09-22 的 123 條（正檔當時 464KB，遠超 96KB 門檻；正檔只留最新 45 條、約 64KB），接在本檔尾端、索引同步補上。

## 逐條索引（專案初期 ~ 2026-09-22，共 305 條；無日期者為最早期）

- （早期）確立無限恐怖風格的 2D 世界觀與隧道設定。
- （早期）完成主遊戲與彈道系統的模組解耦，建立明確邊界規範。
- （早期）實作 CSV 資料驅動的子彈配方系統（支援反彈、扇形分裂、穿透、自轉）。
- （早期）解決子彈高頻率生成時的事件訂閱同步問題（Pre-subscribe 模式）。
- （早期）解決子彈起點在 Collider 內部時偵測不到的問題（CheckSpawnOverlap）。
- （早期）修正彈道系統所有硬編碼 Layer 編號，改由主遊戲傳入 LayerMask。
- （早期）實作怪物基礎追擊 AI，完成「射擊 → 命中 → 扣血 → 死亡」的完整 Core Loop。
- （早期）優化 MonsterSensor，快取玩家參考，移除每幀 FindGameObjectWithTag 的效能開銷。
- （早期）規劃 Physics Layer Collision Matrix，解決怪物互卡、怪物推擠玩家的問題。
- （早期）規劃並建立「主資源包 + 場景模組包」的美術目錄架構，完成教學場景地磚的 Tilemap 基礎設定。
- （早期）資料驅動的怪物生成系統（CSV 讀取，動態生成對應數值怪物）。
- （早期）完成 CSV 驅動的配方與武器雙表系統（RecipeTable + WeaponTable），取代 Scriptable…
- （早期）實作 RecipeManager（配方載入、SubRecipeID 二次解析、BounceTarget 語意化）。
- （早期）實作 WeaponManager（武器載入、RecipeID 關聯、PrefabMapping 子彈 Prefab 管理…
- （早期）重構 PlayerController 串接武器系統，傷害數值改由武器表驅動。
- （早期）實作通用受擊反應系統（HitReactionHandler）：白光閃爍、擊退位移、無敵時間。
- （早期）MonsterData.csv 新增受擊反應欄位（InvincibleTimeMs, KnockbackThreshol…
- （早期）PlayerController 新增 TakeDamage 介面與寫死的受擊反應參數，預留未來接觸傷害使用。
- （早期）實作武器序列圖動畫系統：WeaponTable.csv 新增 WeaponAniPath / WeaponAniNumb…
- （早期）擴充 BallisticsEngine.Spawn API 支援 Sprite[] 動畫參數，BulletInstanc…
- （早期）實作環繞型彈道系統（OrbitalBehavior）：RecipeTable.csv 新增 IsOrbital / Or…
- （早期）環繞彈與穿透（繼續環繞）、反彈（脫軌飛出）、分裂、追蹤等行為完全相容。
- （早期）RecipeTable.csv 新增 BlockedByEnvironment 欄位，可讓配方（特別是環繞彈）穿過地形障…
- （早期）環繞彈引入「群組生命週期」：個別子彈 LifeTime 覆寫為 -1，由 PlayerController 統一在 re…
- （早期）實作地面特效鏈式觸發系統：新增 GroundEffectTable.csv、GroundEffectManager / …
- （早期）地面特效改為 tile 鋪面渲染：GroundEffectTable 新增 TileSize 欄位，圓形範圍內每格放一張…
- （早期）地面特效鋪面演進：先試「金字塔（菱形）」演算法但實機呈現過於菱角分明，最終改回「真實圓形掃描」——`(i*TileSiz…
- （早期）修正子彈命中時用「當下武器」造成的跨武器污染：PlayerController 改用 lambda closure 把發…
- （早期）地面特效新增 `GroundEffectHitTarget` 欄位（`Enemy` / `Environment` / …
- （早期）實作拋物線型彈道（`IsParabolic`）：新增 `ParabolicBehavior`（接管移動、Collisio…
- （早期）拋物線進階：`Speed` 欄位語意改為「飛行時間（秒）」（固定時間抵達，與距離無關，多顆同時落地）；支援 `Sprea…
- （早期）實作持續掃射型雷射光束（`IsLaser`）：新增獨立 `LaserBeam` 核心元件（line-march 把追蹤/…
- （早期）雷射完全複用既有配方：吃 `PierceCount`（穿透）、`HomingTurnSpeed`（追蹤彎曲，賣點）、`B…
- （早期）雷射打磨與除錯（一輪實機調校）
- （早期）雷射外型「種類化」與全參數化（見 [LASER.md](LASER.md)）：外觀改由 `BeamStyle`（種類編號…
- （早期）新增火焰噴射器（雷射的「火焰外觀模式」，見 [LASER.md](LASER.md)）：火焰噴射器本質是雷射（按住掃射 …
- （早期）新增「軌跡特效」機制，並以此重做地刺武器（讓地刺吃滿 RecipeTable 行為，見 [BALLISTICS.md](…
- （早期）~~地刺波 `IsGroundWave`（地表特效版）~~：已移除——改用上述「軌跡特效」做法，因為地表特效不會飛、無法…
- （早期）拋物線武器新增落地殺傷半徑（`BlastRadius`，見 [GROUND_EFFECT.md](GROUND_EFFE…
- （早期）實作一次性特效系統（VFX，見 [VFX.md](VFX.md)）：新增 `VfxTable.csv` + `VfxMa…
- （早期）VFX 打磨（見 [VFX.md](VFX.md)）：① VfxTable 新增 per-effect `Sorting…
- （早期）除錯（軌跡/分裂相關）
- （早期）武器切換調整（見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)）：初始武器改…
- （早期）新增佛光型武器（`IsAura`，見 [GROUND_EFFECT.md](GROUND_EFFECT.md)）：以玩家…
- （早期）GroundEffect 新增單圖渲染模式（`GroundEffectTable` 加 `RenderMode` 欄，`…
- （早期）新增連鎖閃電武器（`IsChain`，見 [LASER.md](LASER.md)）：點一下（吃 `FireInterv…
- （早期）連鎖閃電除錯 ＋ 吃散射/追蹤（見 [LASER.md](LASER.md)）：① 修「打地上物卻打不壞」——目標搜尋原…
- （早期）新增命中迸發子武器 `SubWeaponOnHit`（見 [RECIPE_DESCRIBE.md](RECIPE_DES…
- 2026-06-22｜地圖相機模式
- 2026-06-22｜動畫地上物
- 2026-06-22｜牆 = 「環境/牆」(environment) trigger
- 2026-06-23｜道具拾取系統
- 2026-06-23｜ItemTable.csv 搬到 `Assets/Data/`
- 2026-06-23｜劇情系統
- 2026-06-24｜效能診斷面板 PerfHud
- 2026-06-24｜設定面板
- 2026-06-24｜劇情 Type 2＝頭像對話
- 2026-06-25｜可走層改三態子格細分
- 2026-06-30｜牆碰撞橫向合併
- 2026-06-30｜編輯器「可走」工具強化
- 2026-06-30｜動畫地上物乒乓播放
- 2026-06-30｜地圖載入改「分幀＋載入頁」
- 2026-06-30｜過場影片跳過閃爍修正
- 2026-06-30｜場景特效框架＋火雨
- 2026-07-01｜大螢幕畫質修正：UI 去壓縮＋場景濾波
- 2026-07-02｜可放置場景特效系統 SceneFx
- 2026-07-02｜鏡頭區 camZone trigger
- 2026-07-02｜地上物「可走」勾選
- 2026-07-02｜傳送點「使用傳送點外型」開關
- 2026-07-02｜資源載入改「module 級預載」
- 2026-07-03｜存檔進度層 schema v2
- 2026-07-03｜標題畫面＋三欄存讀檔 UI＋總流程
- 2026-07-03｜進場一次性效果系統＋睜眼醒來
- 2026-07-03｜標題畫面美術＋佛陀動畫＋火焰特效
- 2026-07-03｜部署改用 itch.io + butler
- 2026-07-03｜標題流程 build 開機場景修正 + 新建過場黑幕
- 2026-07-03｜角色 Y 排序
- 2026-07-03｜排序續修：地面特效與飛行戰鬥視覺不再被地上物蓋住
- 2026-07-03｜地面特效排序改「用可走與否分上下」＋拋物線 NaN 防呆
- 2026-07-05｜效能與畫質診斷＋修正
- 2026-07-05｜觸發鏈系統：trigger 接 trigger
- 2026-07-05｜觸發鏈實裝與週邊修正
- 2026-07-06｜旗標系統中度收斂＋旗標管理器
- 2026-07-06｜傳送門「放劇本開門」hub ＋ 強制新手教學
- 2026-07-07｜鏡頭聚焦 trigger
- 2026-07-07｜修「對話接對話」關閉當幀重入卡死
- 2026-07-08｜穿隧道洞口光暈改 shader
- 2026-07-08｜走隧道「點左鍵」閃爍提示
- 2026-07-08｜玩家提示圖 trigger
- 2026-07-07｜進場觸發 trigger
- 2026-07-07｜睜眼醒來連動玩家「趴地→起身」
- 2026-07-09｜殺怪／破壞觸發旗標
- 2026-07-09｜旗標管理器改依 id 由小到大排序
- 2026-07-09｜重複規則選項「每次進場」改名「關卡單次」
- 2026-07-09｜測試快捷「直接進某關卡／地圖」DevQuickStart
- 2026-07-09｜Play 模式加速 ＋ static 殘留保險
- 2026-07-09｜破幻術轉場
- 2026-07-09｜破幻術泛化成「播放螢幕特效」＋新增「關卡單次」旗標範圍
- 2026-07-09｜「Sync Map Assets」補上同步 flags.json
- 2026-07-09｜togglePortal 開關傳送點鏈動作
- 2026-07-09｜特效預覽器「匯出換色版」檔名改 2 位補零
- 2026-07-09｜召喚型武器接玩家側
- 2026-07-09｜召喚陣營制（玩家召喚=友軍、怪物召喚=敵人）＋玩家御靈水晶可裝備
- 2026-07-09｜修召喚陣營兩坑：友軍打不到敵怪 ＋ 召喚物過傳送點消失
- 2026-07-09｜主角攻擊動畫接線
- 2026-07-09｜修 boss 不逃跑不召喚（BrainType 沒 Trim）＋ 記錄怪打怪傷害忽勝忽敗
- 2026-07-09｜修召喚三問題：怪打怪傷害、友軍跟隨、御靈水晶消失
- 2026-07-09｜重修「怪打怪傷害」為系統機制（第一擊必互換）＋攻速資料化
- 2026-07-10｜召喚特效（邊播特效邊生怪）＋施放冷卻統一提示
- 2026-07-10｜召喚特效跟著怪物大小
- 2026-07-10｜怪物障礙迴避＋感測範圍說明
- 2026-07-10｜修召喚出生在牆裡＋怪物避障凍結
- 2026-07-10｜怪物尋徑改全域 A*
- 2026-07-10｜榕樹妖 boss 戰鬥模組
- 2026-07-12｜Boss 開戰資訊表演
- 2026-07-10｜榕樹妖 boss：死亡整棵樹燃燒演出 ＋ boss 死亡回收招式 ＋ 手感調整
- 2026-07-13｜修「怪物原地踏步」＋召喚施法動作復原
- 2026-07-13｜地上物「出現條件（完成 N 關後才出現）」＋地上物多選/框選
- 2026-07-13｜特效庫武器化定案
- 2026-07-13｜武器集氣模式
- 2026-07-13｜Pack 4 像素反射雷射
- 2026-07-16｜底部 HUD 血球＋藥水系統
- 2026-07-18｜傳送點外型「精準視覺錨點」＋編輯器點放預覽
- 2026-07-18｜載入頁進度條改用美術素材
- 2026-07-18｜地上物新增「可穿越(passThrough)」＝無碰撞但照常 Y-sort
- 2026-07-18｜榕樹妖地刺：生成安全內縮＋橫掃 5→3 排＋大地刺補可走驗證
- 2026-07-18｜關卡儲存機制：關卡進度與臨時包 `RunProgress`
- 2026-07-20｜劇情演出編輯器（Cutscene）
- 2026-07-23｜作弊面板
- 2026-07-27｜技術債清理：陣列型 static 快取修正＋素材同步白名單收斂＋CSV 工具
- 2026-07-27｜沒裝備武器就不能攻擊＋移除 E 鍵切換
- 2026-07-27｜地圖級「禁用武器」開關（MapsTable 新增 `NoWeapon` 欄）
- 2026-07-27｜對話防連點
- 2026-07-27｜劇情跳過改成「只有開發階段能用」＋修掉「按 ESC 莫名播爬起動畫」
- 2026-07-27｜清掉開場改用劇情編輯器後不再使用的漫畫素材
- 2026-07-28｜測試工具：直接進關卡加「邪佛廣場-1關後」＋作弊面板加「給 10000 元」
- 2026-07-28｜觸發鏈新增 `openPanel`／`unlockRoll` ＋「最低/最高完成關卡數」條件 ＋「條件不成立時」分支
- 2026-07-28｜抽選介面套上正式美術＋十連結算畫面
- 2026-07-28｜血統做成一次性藥劑（`BloodlineID` 欄 + BloodlineSystem）
- 2026-07-28｜金錢不再是背包道具，改成獨立數字
- 2026-07-28｜祭壇抽選系統（GACHA）
- 2026-07-29｜鍛造介面（ForgingPanel，Y 鍵開啟）
- 2026-08-01｜存讀檔畫面換上正式素材（SaveSlotPanel）
- 2026-08-01｜「繼續遊戲」回到上次所在的地圖（schema v3）
- 2026-08-03｜能力珠鑲嵌系統：物品實例 ＋ 能力容器
- 2026-08-04｜能力珠的圖示做成「兩層疊合」＋ UI 貼圖匯入規則與批次工具
- 2026-08-06｜怪物出生點加「重複產生」與「怪物 id 陣列」
- 2026-08-06｜新增「開關(按F)」trigger ＋ 怪物出生點「啟動旗標」
- 2026-08-06｜推翻上一則：出生點的「啟動旗標」拿掉，改吃觸發鏈的通用條件欄位
- 2026-08-06｜作弊面板給的道具跑進臨時包
- 2026-08-07｜背包介面重製：新美術 ＋ 裝備/消耗品雙頁籤 ＋ 分頁
- 2026-08-07｜物品 icon 大小自動正規化（IconFit）
- 2026-08-07｜背包的格子提示重做：hover 改描邊、拖曳提示改呼吸外框（順便挖出 Linear 色彩空間的坑）
- 2026-08-10｜場景照明：單光源升級成多光源，火把/燈籠可調亮度、光色、搖晃
- 2026-08-10｜照明補上「獨立光源」：不綁地上物，直接放一個點就會發光
- 2026-08-10｜地圖編輯器加上「照明預覽」與拖曳光源（順便修掉自己造成的回歸）
- 2026-08-10｜地圖編輯器版面重整：左側工具列＋底部狀態列＋相機讓位
- 2026-08-10｜移除地磚（tile）系統 ＋ 特效預覽器補關閉鈕
- 2026-08-07｜作弊面板加「取得所有武器」一鍵鈕
- 2026-08-17｜地面特效兩個新欄位（SigilPath 背景旋轉符號／LightRadius 發光半徑）＋ 佛光視覺實驗三連（最後全部還…
- 2026-08-18｜血統系統：系列＝三階段（殭屍→毛殭→旱魃）＋ 兩張表 ＋ 逐階進階藥劑
- 2026-08-18｜血統變身演出（倒下 → 天雷 → 煙霧與電弧 →（煙裡換裝）→ 爬起）＋ 四個順手補上的通用能力
- 2026-08-18｜血統表新增體型倍率 `BodyScale` ＋ 變身特效隨體型縮放 ＋ 雷擊點改成劈腳底
- 2026-08-18｜體型倍率連動：佛光圈跟著身體、放大改成腳底錨點、特效改用「可見身體幾何」對位
- 2026-08-18｜文件收尾：血統三大塊的周邊文件同步 ＋ 修掉 10 處「現在寫錯」
- 2026-08-18｜變身雷柱改成純 loop（拿掉頂端雷首）
- 2026-08-18｜修「表演層被地上物蓋住」——Y 排序帶不再靠 16-bit 繞回
- 2026-08-19｜變身表演改成全程暫停 ＋ 新增「血統揭示」立繪面板
- 2026-08-19｜「使用道具」收成唯一入口，並立下「左鍵搬移／右鍵使用」的全遊戲鐵則
- 2026-08-19｜地上物碰撞改成「貼合圖形」——透明處不再擋路
- 2026-08-19｜修「一進書房就卡在書架裡」＋落點防呆
- 2026-08-19｜查出「角色的頭被屏風蓋住」＝「層」設錯，不是排序系統壞了
- 2026-08-19｜移動平滑化：沿牆滑動 ＋ 保守角落校正 ＋ 零摩擦材質
- 2026-08-19｜圖片型文字的多語系：`UI/Texts/<語言>/` ＋ 缺圖退回母版
- 2026-08-19｜修「一進新房間就被彈回上一張圖」——被擊退不算踩到傳送點
- 2026-08-19｜修「換房後玩家被丟到地圖外面」——`Destroy()` 延到幀尾，舊地圖的碰撞還在
- 2026-08-20｜傳送點外型改成「編輯器裡所見即所得」——加「傳送點對位」模式
- 2026-08-20｜傳送點改成「一個點」——錨點＝外型＝踩踏區＝落點；並記下一次「更正確卻更錯」的判定點誤判
- 2026-08-21｜文件體系整頓：AI 契約＋維護規範＋封存機制（仿公司專案的 docs 治理）
- 2026-08-21｜Split Sprite Sheet 批次化：新增兩個資料夾模式
- 2026-08-21｜新增「夜裔」血統系列（夜裔 → 血伯爵 → 該隱）
- 2026-08-22｜作弊面板新增「獲得所有血統藥劑」快捷鈕
- 2026-08-22｜修「Cain / Crimson Count 的攻擊動畫播不出來」——真正的原因是 25 幀從來只播得到 2 幀
- 2026-08-22｜系統訊息獨立成 `UILayer.System`——不再被背包等視窗蓋住
- 2026-08-22｜劇情演出四項擴充：回憶特效／頭上對話框／用 trigger 啟動／隱藏主角
- 2026-08-22｜劇情演出續補：條件旗標／完成寫旗標、關閉血量 HUD、回憶特效在暗地圖上救回來
- 2026-08-22｜修「勾了關閉血量 HUD 卻沒生效」——兩個原因疊在一起，一個是沒同步、一個是 `Start()` 跑太晚
- 2026-08-22｜修「隱藏主角用著用著就永久失效」——static 開關的三個還原路徑少了兩個
- 2026-08-22｜修「劇情剛播完，玩家就被送回上一張圖」——演出把玩家釘在傳送點上
- 2026-08-22｜對話框字級改成自己算，不用 uGUI 的 best-fit
- 2026-08-22｜Skip 收成全遊戲一套：對話與劇情演出都能跳，樣式與開關規則統一
- 2026-08-24｜進場「場景說明」：走進有名字的地圖時跳一次場景名
- 2026-08-26｜RecipeTable 大改：一列一種 `Mode`、欄位依名字讀、能力珠依模式過濾＋鍛造「提示不擋」
- 2026-08-26｜武器工坊：Unity EditorWindow 版的「選外型→選模式→填效果→立刻射出去看」，一鍵存回 CSV
- 2026-08-26｜「巨彈珠」改名「須彌珠」，效果從「子彈變大」擴成「施放大小」——近戰／突進／法陣／落雷／佛光也吃
- 2026-08-26｜能力珠第二批：把武器表／配方表「能拆的欄位」全拆成珠子，8 種 → 25 種
- 2026-08-26｜能力珠圖鑑 GEM_CATALOG.md：25 顆每一顆的功用、三級數值、拿現有武器算的範例、坑與流派搭配
- 2026-08-26｜連擊：扣一次扳機連射 N 發（`BurstCount`／`BurstInterval`），附加在任何離散模式上，＋連擊珠
- 2026-08-26｜平行彈：同方向並排 N 道（`ParallelCount`／`ParallelSpacing`／`ParallelMax…
- 2026-08-26｜玩家攻擊動畫只播到「動作最大幀」——快武器點一下不再原地演完 2 秒收勢
- 2026-08-26｜修「毛殭的飛劍從腹部飛出」——武器出手點改成依可見身高算，不再釘在 transform
- 2026-08-27｜修「主角一出手就長大一圈」——walk/attack 對齊 idle 改量「體積尺度」、掃全幀取中位數
- 2026-08-27｜場景說明改成「先跳名字、再演劇情」——進圖自動劇情等進場等待鏈放行才開始表演
- 2026-08-27｜修回憶特效「畫面兩側像馬賽克」——柔邊模糊從 4-tap 十字改成 13-tap 圓盤
- 2026-08-27｜改名：夜裔系列 → 血族系列、夜裔（第一階）→ 覓血者、`Nightborn` → `Bloodseeker`
- 2026-08-27｜新增「狂族」血統系列（狼人 → 望月者 → 芬里爾／Feralborn：Werewolf → Moonwatcher →…
- 2026-08-28｜NPC 系統第一波：編輯器 NPC 分頁＋可交談/閑晃/開介面的中立 NPC
- 2026-08-28｜場景說明加半透明黑幕＋整段縮短 1/3
- 2026-08-28｜陣營系統第二波：三方陣營劇本地基（狼人×吸血鬼×主角）
- 2026-08-28｜建立美術紀律文件 ART_DIRECTION.md——對照《魔女庭園》拆解「完成品感」從哪來
- 2026-08-28｜美術紀律落地第一波：場景主色染色（AtmoTint）＋玩家常駐體光＋HUD 血球暗場景收斂
- 2026-09-01｜修「血統變身後底部血球 HUD 永久消失」——演出開場 `CloseAll()` 把 HUD 一起關掉、沒人還原
- 2026-09-01｜修「天雷打下來鏡頭偏掉、要等 tip 關掉才滑回來」——暫停中的震動沒有回復力
- 2026-09-02｜測試選單「直接進關卡 → 血狂之爭」改走 module 首圖，讓 `IsLevelStart` 能決定進哪張
- 2026-09-02｜修「踩進鏡頭區拉遠時畫面邊緣露出地圖外黑邊、1~1.5 秒才收」——夾制要夾實際位置，不能只夾目標
- 2026-09-02｜Atmosphere 新增室內系 16~19（室內暖光／莊嚴金輝／冷月室內／燭火幽影）＋ Bloom 前置管線
- 2026-09-02｜【POC】角色場景融合可行性測試——Original / A / B / C 四模式即時比較
- 2026-09-02｜釐清「角色影子偏在腳的斜後方」——查完四種自動算法都有反例，結論是改走資料驅動；本輪改動已全部還原
- 2026-09-03｜角色融合場景：找到色彩處理救不了的根因——背景是全畫面唯一被放大顯示的東西；過渡期上 mipMapBias、砍 Test…
- 2026-09-03｜影子錨點表：每角色每動作一組、工具自動算＋手改覆寫——解掉「idle 偏、走路準」
- 2026-09-03｜殭屍系列三個血統的影子改 manual（殭屍太小、毛殭／旱魃偏掉）
- 2026-09-03｜Split Sprite Sheet 改成「固定切 5×5 ＝ 25 格」，放大過的合圖才切得對
- 2026-09-03｜地上物破壞改成「把自己那張圖炸成碎片」，共用煙塵特效預設關閉
- 2026-09-04｜Boss 開戰前奏定案：黑霧籠罩（兩張灰階密度圖 ＋ shader）＋「強敵現身」文字煙霧凝聚／散去 ＋ 頭目資訊進退場
- 2026-09-04｜怪物常駐體光：暗場景裡怪物終於看得見輪廓
- 2026-09-07｜邪佛廣場改尺寸：90×50 → 48×36，背景解析度 1448 → 2896
- 2026-09-07｜可走層：自動生成 ＋ 編輯器內試走
- 2026-09-07｜背景解析度 ↔ 編輯器格數：訂出換算基準，並揪出 5 張地圖的背景被拉扁
- 2026-09-08｜紅嫁衣逃跑「原地踏步」治本：跑之前先確認有路可跑
- 2026-09-08｜查「她不逃、過幾秒才突然跑」＝擊退窗口凍結決策；順手把跑跑停停做成參數
- 2026-09-08｜逃跑節奏改綁距離：調 `Speed` 不該連帶改掉行為模式
- 2026-09-09｜切圖工具支援 AutoSprite 的 perfect loop 合圖（列數改成自動推算）
- 2026-09-09｜切圖工具改成「從圖本身推測格線」——尺寸與格數都不必固定
- 2026-09-09｜新增第四個血統系列「土裔 Gaiaborn」：石像鬼 → 山嶽巨人 → 泰坦
- 2026-09-09｜石像鬼／泰坦的攻擊動畫只播前 2~4 幀：給 G6 的自動演算法加一道「失效偵測」
- 2026-09-09｜面板上的名字四個字會折到第二行——字級寫死 ＋ uGUI 預設自動換行
- 2026-09-09｜血統表的 `WalkSpeed` 接上了：換血統會真的改移動速度
- 2026-09-09｜泰坦走路時影子落在身後：一段 4~5px 寬的拖曳剪影被當成第二隻腳
- 2026-09-09｜血統圖依「系列」分資料夾：`SequenceImage/` 與 `Talk/` 各多一層
- 2026-09-10｜第三階血統的「神格特效」三層骨架（環繞電光／背後圓盤／移動殘影），先接上該隱
- 2026-09-10｜第五個系列「靈根 SpiritRoot」＋ 每個系列補上二／三階「直達藥劑」
- 2026-09-10｜「靈脈 SpiritVein」全面改名為「靈根 SpiritRoot」
- 2026-09-10｜血統藥劑改名＋文案改成不劇透
- 2026-09-13｜移除 `proud` / `speechless` 兩種情緒立繪
- 2026-09-13｜對話立繪改成自動對齊「人物」，不再量圖檔外框
- 2026-09-13｜新增第六、七個系列：雲龍 Cloudborn（蛟人 → 螭吻 → 應龍）與熾龍 Blazeborn（龍奴 → 法夫納 →…
- 2026-09-13｜應龍與尼德霍格的第三階神格特效：兩個都是零素材成本的「空位」
- 2026-09-13｜第五層特效 `BloodlineAttackFx`：攻擊時才罩上來一次
- 2026-09-13｜尼德霍格的影子偏到身體後方：手改錨點表，沒動演算法
- 2026-09-13｜血統藥劑 icon 重製：一瓶血瓶 ＋ 系列圖騰 ＋ 右下角階級星星
- 2026-09-14｜背包道具區改成 4×3、而且格數變成「改兩個常數」就能換
- 2026-09-14｜背包 tooltip 加大圖預覽（附帶修掉 tooltip 會掉出畫面的老問題）
- 2026-09-14｜血統藥劑改回「一個系列一張成品瓶 ＋ 右下角階級角標」（畫法做成開關）
- 2026-09-14｜物品 tooltip 抽成共用元件 `ItemTooltip`：背包／倉庫／鍛造從此同一份
- 2026-09-14｜倉庫版面重做：格子對齊底圖、大小比照背包（10×10 → 5×5）
- 2026-09-14｜加入第八個血統系列「蟲族 Swarmborn」：寄生體 → 獵殺者 → 蟲皇
- 2026-09-14｜Split Sprite Sheets 的格線推測改用「跨線率」，修好「正確的合圖被判成切錯」
- 2026-09-14｜血統特效第六層 `BloodlineHitFx`：三階血統的擊中特效凌駕一般武器（蟲皇＝咬擊）
- 2026-09-14｜背包暫時放大到各 10 頁（除錯用，之後要還原）
- 2026-09-14｜蟲皇的影子錨點手改（idle／walk）＋ 一個新的量法寫進 SHADOW.md
- 2026-09-15｜通用「偵測條件」：依血統／道具決定 NPC 出不出現、講哪一句（⏳ 未編譯未實測）
- 2026-09-15｜NPC 三修：大小兩邊不一致、狂族士兵永遠在走路、補「初始朝向」（⏳ 未編譯未實測）
- 2026-09-16｜診斷「火焰噴射器的火被地毯擋住」＋ 地圖編輯器「可走／可穿越」兩個勾加面板說明
- 2026-09-16｜紅嫁衣大絕「家人齊聚」＋ pant 喘息破綻（⏳ 未編譯未實測）
- 2026-09-16｜血量門檻台詞改成「跌破就一定喊」——boss 放大絕時喊話的做法（⏳ 未編譯未實測）
- 2026-09-17｜卍字進場：主角現在是被卍字送進場的（⏳ 未編譯未實測）
- 2026-09-17｜家書（dramaId=30）改成可反覆閱讀＋修正一處過期文件
- 2026-09-17｜撲擊型戰鬥模組 `PounceBrain` ＋ 戰狼 Wolf Warrior（⏳ 未編譯未實測）
- 2026-09-17｜三方陣營語意反轉：預設「敵對」，和平改成劇本要明確進入的特例（⏳ 未編譯未實測）
- 2026-09-17｜戰狼實機回報兩則：亮場景的怪物體光、撲擊節奏沒有「停頓」（⏳ 未編譯未實測）
- 2026-09-17｜戰狼「只要我在走動就咬不到我」——衝刺改成全程追蹤＋動畫跟著加速（⏳ 未編譯未實測）
- 2026-09-17｜戰狼「咬的動作有播、血卻沒掉」＝貼身判定用了中心距離（⏳ 未編譯未實測）
- 2026-09-17｜戰狼衝刺「只前進兩步就停」＝程式生成的怪沒開 Rigidbody 內插（⏳ 未編譯未實測）
- 2026-09-17｜戰狼「我一攻擊牠就不還手」＝擊退把牠推出自己的觀望圈（⏳ 未編譯未實測）
- 2026-09-17｜腳底對齊：AI 生成序列圖「每個動作畫在畫布不同高度」的通用防線（⏳ 未編譯未實測）
- 2026-09-17｜戰狼影子「切動作就位移＋忽大忽小」＝錨點演算法對站姿/跑姿判定不同（改表解決）
- 2026-09-17｜戰狼「一跑起來就大一圈」＝程式自己把牠放大的（⏳ 未編譯未實測）
- 2026-09-17｜新武器「狂族十字弓」＝背包是十字弓、射出去是弩矢（純資料、零程式改動，⏳ 未實測）
- 2026-09-17｜射手型怪物模組 `ArcherBrain` ＋ 打通「怪物使用投射型武器」（Phase 2 第一階段，⏳ 未編譯未實測）
- 2026-09-17｜狂族弩手第一輪實測修正：放箭時機對齊序列圖的幀 ＋ 射程砍 1/4（⏳ 未編譯未實測）
- 2026-09-17｜射手型模組的鐵則：動作播了就一定要射出箭（⏳ 未編譯未實測）
- 2026-09-17｜修「每隻新生成的弩手第一發是空砲」——事前問 `Ready`，冷卻卻是執行時才寫進去的（⏳ 未編譯未實測）
- 2026-09-17｜修「清掉一群弓箭手 ⇒ Console 被 MissingReferenceException 洗版」＋ 放箭幀 11→…
- 2026-09-18｜新怪「狂族皇家衛士」＋ 跳躍踐踏模組 `LeapSlamBrain` ＋ 程序化裂地 shader（⏳ 未編譯未實測）
- 2026-09-18｜跳躍踐踏第一輪實機回饋：跳更高＋墜落感、裂痕放大、「蹲了一定要跳出去」（⏳ 未編譯未實測）
- 2026-09-18｜跳躍第二輪：跳到三個身高＋垂直砸下；新增 `MeleeChaseBrain`（出手就要把動作做完）（⏳ 未編譯未實測）
- 2026-09-18｜跳躍第三輪：落地定格一秒＋裂痕慢慢竄開；修「怪一直砍但打不到我」（⏳ 未編譯未實測）
- 2026-09-18｜`MonsterData.csv` 補 `JumpScale` 欄（接在 `AttackScale` 後面）
- 2026-09-18｜修「地面龜裂畫在怪物胸口而不是腳底」＋ 補上怪物版的身體幾何 API（⏳ 未編譯未實測）
- 2026-09-18｜龜裂 shader 重寫：放射狀爆裂 → Voronoi 均勻泥塊（⏳ 未編譯未實測）
- 2026-09-18｜跳躍踐踏加「魄力」：落地定格（hit stop）＋ 下墜殘影（⏳ 未編譯未實測）
- 2026-09-18｜修「調大怪物 Scale 之後近戰就砍不到玩家」——攻擊判定不要自己算圈（⏳ 未編譯未實測）
- 2026-09-18｜近戰正式分成「衝撞型 / 揮舞型」兩種；狼人兵與吸血鬼兵改用揮舞型（⏳ 未編譯未實測）
- 2026-09-18｜開場第一次進邪佛廣場不再播卍字進場與場景名（⏳ 未編譯未實測）
- 2026-09-22｜ZhaYu 系列四隻新怪上線：衝撞／揮舞／射手／自爆，新增自爆模組與「血魔重炮」（⏳ 未編譯未實測）
- 2026-09-22｜修 ZhaYu「走路時瞬移貼到玩家身上、對話框離圖很遠」——腳底對齊把兩張不同畫布的像素混算（⏳ 未編譯未實測）
- 2026-09-22｜`WalkScale`／`AttackScale` 改成「在自動對齊之上再乘」——填 1.1 不再反而變小（⏳ 未編譯未…
- 2026-09-22｜ZhaYu_Gun 手感微調：放彈幀 9 → 14、炮彈擊中爆炸縮一半（⏳ 未編譯未實測）
- 2026-09-22｜從根源修「怪一放大，範圍技就打不到人」——AOE 半徑改成隨體型縮放＋防呆下限（⏳ 未編譯未實測）
- 2026-09-22｜自爆怪：引信改成「真的走到身邊」才點、引信視覺改走 shader 逐漸燒紅＋脈動加速（⏳ 未編譯未實測）
- 2026-09-22｜波次刷怪（總波數／波次群組／全滅接鏈）＋ 掉落表資料化（⏳ 未編譯未實測）
- 2026-09-22｜邪佛手掌的滾滾沙塵：通用「移動拖尾特效」（⏳ 未編譯未實測）
- 2026-09-22｜夢境佛掌收尾三件套：震退回入口＋骨牢束縛＋鏡頭拉遠（⏳ 未編譯未實測）
- 2026-09-22｜修掉「夢境開場播完第一句之後一片黑」（⏳ 未編譯未實測）
- 2026-09-22｜新手夢境教學：邪佛廣場「按左鍵發射武器」的暫停教學（⏳ 未編譯未實測）

---

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

* [x] 新增**命中迸發子武器** `SubWeaponOnHit`（見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)）：子彈命中時在命中點生成「**武器表上指定 ID** 的武器」一發，子武器**自帶外型/傷害/追蹤**（解決 SubRecipeID 只能仿母武器外型的限制——根因是 SubRecipeID 指配方無外型、且彈道層與武器系統解耦只能複製母彈）。在主遊戲側 `PlayerController.HandleBulletHit → TryTriggerSubWeapon → SpawnSubWeaponAt` 實作（彈道層不變）；`RecipeEntry` 加 `SubWeaponOnHit`（武器 ID）+ `SubWeaponHitTarget`（`Enemy`/`Environment`/`All`）。迸發方向取命中面法線往外，吃子武器自己整套配方（散射/追蹤…）。範例：武器 13「蜂巢」配方 24 `SubWeaponOnHit=2, All` → 打到牆/怪迸出武器 2（3 分裂追蹤飛劍）＝ 3 把追蹤飛劍（飛劍圖、非炸彈圖）。

* [x] 地圖相機模式（2026-06-22，見 [MAP_SYSTEM.md](MAP_SYSTEM.md)）：`MapsTable.csv` 新增 `MapMode` 欄（1=整張地圖縮放、2=鏡頭跟隨，預設 2）。`MapManager` 載圖後依模式套用相機（新增 `MapCameraController`，仿 TeleportWatcher 自掛）：跟隨模式固定縮放（角色正常大小）＋鏡頭跟玩家並夾在地圖邊界內。**門檻保護**：寬或高超過門檻（預設 18/10 格）才跟隨，現有適中地圖即使填 2 也維持整張地圖、觀感不變。

* [x] 動畫地上物（多張圖做成一個物件，2026-06-22，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)、[DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md)）：`Environment/` 下的**子資料夾 = 一個動畫物件**，同步收成一筆 catalog item（`frameCount`/`frames`，依檔名排序）。**FPS 每實例可調**（`.dipanmap` 的 `objects[].animFps`，編輯器選取面板設定）。編輯器即時循環預覽；遊戲端 `MapLoader` 掛 `AnimatedMapObject` 原地循環播，碰撞框/血量/可破壞沿用第一幀。四個 catalog 產生器（編輯器 `sync_assets.sh`/`AssetSyncTool.cs`、遊戲端 `MapAssetSyncTool.cs`/`MapIO.BuildFromGameAssets`）一致處理子資料夾（見 [PROBLEMS.md](PROBLEMS.md) C1）。

* [x] 牆 = 「環境/牆」(environment) trigger（2026-06-22，見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)）：牆與可走刻意分開（深坑/水池 = 不可走但子彈穿過）。`MapLoader.BuildCellColliders` 改成**有 environment 區域時：牆 = environment 格、不可走且非 environment = 水塘/深坑**；**沒有 environment 區域時退回舊模型**（不可走=牆、bulletPass=水塘），既有地圖不受影響。編輯器加便利按鈕「依不可走格建立牆 trigger」：一鍵把所有不可走格刷成 environment 區域，建立後切 Trigger 工具（減格）方便挖掉水池格。

* [x] 道具拾取系統（2026-06-23，見 [INTERACTION.md](INTERACTION.md)）：編輯器 `pickup` trigger 加 `count` 欄；遊戲端新增「靠近按 **F** 撿取」一條龍——`InteractionManager`（最近目標→提示→F）＋ `GroundLoot`（地上掉落物，背包滿溢出的掉腳下）＋ `InteractMarker`（純程式畫五角星、閃爍標示觸發點）＋ `PickupTipPanel`（跟隨提示）＋ `AlertPanel`（中央 toast，不暫停/不遮罩、2 秒淡出）。背包 `AddItem` 回傳的「放不下剩餘」剛好做「滿了掉地上」。一次性消耗、當次停留記憶（換圖重建，永久化屬 Phase 2）。

* [x] ItemTable.csv 搬到 `Assets/Data/`（2026-06-23，見 [INVENTORY.md](INVENTORY.md)）：與其他資料表同位置；載入改為比照各表的「拖 TextAsset」——新增 `ItemTableProvider`（場景元件持 CSV 參照），`InventorySystem` 載入時 `FindObjectOfType` 取用、退回 Resources 後備。`ItemDatabase` 拆 `LoadFromTextAsset`（主）/`LoadFromResources`（後備）。

* [x] 劇情系統（2026-06-23，見 [DRAMA.md](DRAMA.md)）：編輯器預設 trigger 加 `drama`「劇情觸發點」（紫色、`dramaId` 欄）。互動複用拾取那套（靠近按 **F**、星星標示——劇情點＝紫星、提示改「按 F 鍵」、一次性消耗），故把 `LootManager` 一般化改名 `InteractionManager`（管掉落物＋拾取點＋劇情點）、`PickupMarker`→`InteractMarker`。新增 `DramaTable.csv`（`Assets/Data`，ID/ImagePath/Text）＋ `DramaData`/`DramaDatabase`/`DramaTableProvider`（同 ItemTable 載入慣例），以及 `DramaPanel`（模態：暫停＋半透明黑遮罩＋大圖+文字、ESC/點任意處關閉）。`DramaPanel.Show(dramaId)` 由 InteractionManager 在按 F 時呼叫。

* [x] 效能診斷面板 PerfHud（2026-06-24，見 [DISPLAY_SETTINGS.md](DISPLAY_SETTINGS.md)）：排查「Windows build 幀數低、Mac/編輯器卻順」。`Assets/Scripts/Diagnostics/PerfHud.cs`（開場自動生成、按 **P** 開關）顯示 FPS/幀時/最差幀、CPU·GPU ms（`FrameTimingManager`，已開 `enableFrameTimingStats`）+ 瓶頸判斷、顯示卡/API/解析度/刷新率/VSync/記憶體，面板上可即時切 VSync(V)/目標幀率(T)。**結論：不是效能問題**——GPU 一幀 ~1.5ms，FPS 被 VSync 鎖在遠端 ATEN 4K HDMI 線路的 ~60Hz（Mac 是 120Hz 才覺得差），記在 [PROBLEMS.md](PROBLEMS.md) E1。

* [x] 設定面板（2026-06-24，見 [TODO.md](TODO.md)）：`SettingsPanel`（背板 `SettingPanelBG` + 兩條音量 slider 可拖曳〔未接音訊〕 + 右上關閉鈕 + 底部離開遊戲鈕＋門 icon）、`ConfirmPopup`（離開確認彈窗，真素材：`PopupPanelBG` + LongBtn + 勾/叉 icon）、`SettingsLauncher`。**按 ESC 開設定**：`UIManager` 加「ESC 根面板」機制（`SetEscapeRootPanel<T>`，同一分支處理「有視窗→關最上層／沒視窗→開設定」，不會關了又重開），另保留 O 備用鍵。音量持久化/實際音訊/畫面設定選單列入 TODO。

* [x] 劇情 Type 2＝頭像對話（2026-06-24，見 [DRAMA.md](DRAMA.md)）：`DramaTable.csv` 尾加 `Type`（1 大圖+文字 / 2 頭像對話，留空=1）、`TalkGroup` 欄。新增 `DramaTalkTable.csv`（流水號/群組/姓名/頭像路徑/位置 1左2右/對話）＋ `DramaTalkData`/`DramaTalkDatabase`（依群組分組、組內依流水號排序）/`DramaTalkTableProvider`/`DramaTalkController`，以及 `TalkPanel`（底部對話框 `DramaPanelBG` + 姓名牌匾 `DramaPanelNameBG`〔擺立繪對側〕 + 立繪〔站姿、排對話框後方被框蓋住、依 Side 左右〕 + 文字、點擊/空白/Enter 換頁、半透明黑遮罩）。**頭像走地圖素材管線**（放 `GameAssets/Modules/<module>/Talk/`、`AvatarPath`=catalog id、`DramaTalkDatabase.ResolveAvatars` 載入）——`Talk` 已加進三處同步分類白名單（`MapAssetSyncTool.cs`/`MapIO.cs`/`sync_map_assets.sh`，記在 [PROBLEMS.md](PROBLEMS.md) C3）。`InteractionManager.TriggerDrama` 依 `Type` 分支：**Type 1＝靠近按 F（放紫星）、Type 2＝碰到自動觸發（`dramaTouchRadius`、不放星星）**。

* [x] 可走層改三態子格細分（2026-06-25，分支 `feat/cellSmaller`，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)、[MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)）：解決「格子太大、可走/不可走判定不夠細」。地圖格式新增 `walkSubdiv`（新地圖預設 4＝每 tile 切 4×4 子格）；可走層 `blocked` 從「tile 解析度 2 態」改成**子格解析度三態**：`'0'` 可走 / `'1'` 牆(擋＋反彈子彈) / `'2'` 水/坑(擋腳、子彈穿過)。碰撞盒大小 = `tileSize/walkSubdiv`。**牆/水直接在編輯器「可走」工具用三筆刷(綠/紅/藍)塗、可選筆刷大小(1/2/4/8 子格)**；地磚/物件/trigger 維持 tile 解析度不動。**徹底移除舊的 environment 牆 trigger**（triggerTypes.json/`TriggerType.cs` 預設/「依不可走格建立牆」按鈕/遊戲端 legacy 牆模型全砍），牆/水單一欄位三選一、不再有「bitmap＋trigger」疊層。新增 `MapCoords` 子格座標(`Fine*`)、`WalkableOps` 三態讀寫。既有 11 張 RedBridalGown 地圖以 `migrate_walksubdiv.py` 一次性無損轉檔（env 牆→`'1'`、不可走非牆→`'2'`，每格展開 N×N；編輯器 Maps/、GameAssets、StreamingAssets 三處）。

* [x] 牆碰撞橫向合併（2026-06-30，見 [PROBLEMS.md](PROBLEMS.md) B6）：`MapLoader.BuildCompositeFromCells` 原本「一個牆子格＝一個 `BoxCollider2D`」，大地圖（放大後外圍整片被塗成牆）會逐一加數萬個 collider＋`GenerateGeometry` 而卡數十秒。改成**同列連續牆格 run-length 合併成一條長 box** 再餵 `CompositeCollider2D`；因 composite 本來就把相鄰 box 併成外框，物理外形與 `hit.normal` 完全一致、零行為變化。實測某 90×50 圖 65,856 子格 → 324 條 box（約 203×）。

* [x] 編輯器「可走」工具強化（2026-06-30，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)）：新建地圖初始全是牆、要塗大片可走很慢 → ① 筆刷尺寸從 `{1,2,4,8}` 擴成 `{1,2,4,8,16,32,64,128}`（每列 4 顆排版）；② 新增「整張地圖」一鍵鈕「全部改可走（綠）／全部改牆（紅）」（`WalkableOps.FillAll`，含 Undo）。

* [x] 動畫地上物乒乓播放（2026-06-30，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)）：AI 產的循環圖首尾常接不順、播到第二輪會跳一下。`AnimatedMapObject` 加**乒乓模式**（0→N-1→0 來回，端點各停一幀＝接縫消失，不用改圖）；`ObjectInstance` 加 `pingPong` 欄（遊戲＋編輯器雙方），編輯器物件面板加「循環／乒乓」切換、預覽即時反映、複製會帶。預設循環、向下相容。乒乓會「正放再倒放」，適合佛像呼吸/發光等氛圍動畫；方向性動畫維持循環。

* [x] 地圖載入改「分幀＋載入頁」（2026-06-30，見 [RESOURCE_LOADING.md](RESOURCE_LOADING.md)）：原本 `MapManager` 同步建圖（背景/地磚/逐張載地上物/牆/怪擠在一幀）→ 進場/換圖凍住。新增 `MapLoader.LoadMapRoutine`（地上物每幀建 `objectsPerFrame` 個、回報進度、`LastLoadOk`）＋ `LoadingPanel`（Overlay 層、依關卡 `Resources/Loading/<module>.png` 顯示、進度條、鎖輸入不暫停）＋ `MapManager.LoadMapRoutine`（開頁→停留 `loadingScreenHoldSeconds` 秒→清場→分幀載→放玩家/怪→關頁，`_loading` 擋重入）。首次進場與每次換關都走同一套。

* [x] 過場影片跳過閃爍修正（2026-06-30，見 [CUTSCENE_TUNNEL.md](CUTSCENE_TUNNEL.md)）：跳過影片時「影片關掉→又閃一下→才真正關閉」。原因＝黑幕淡入蓋住後只 `Pause` 影片、`_video` RawImage 還開著，接著黑幕淡出又露出暫停的最後一幀。修法：轉全黑時 `_video.enabled = false`，淡出只剩黑底、乾淨過渡。

* [x] 場景特效框架＋火雨（2026-06-30，見 [SCENE_EFFECT.md](SCENE_EFFECT.md)）：新增**地圖級世界端**特效系統（`MapsTable.csv` 第 8 欄 `SceneEffect`、`SceneEffectController` 仿 `AtmosphereController` 自動生成/常駐/載圖套用/換圖清殘留）。第一個效果**火雨**：仿「火焰拋擲彈」拋物線，從畫面外上方拋火球進相機可視範圍、落地播火光，**純表演不傷人**，火球/火光用程式生成佔位素材（之後可換真素材）。曾試以 Atmosphere mode 16 做「天空紅漩渦」，因 45° 俯視看不到天空、概念不成立而移除。火球 `sortingOrder` 一度填 2,000,000 溢位繞回負數而看不到，改 30000（記在 [PROBLEMS.md](PROBLEMS.md) E4）。

* [x] 大螢幕畫質修正：UI 去壓縮＋場景濾波（2026-07-01，見 [PROBLEMS.md](PROBLEMS.md) G2/G3）：排查「小視窗/遠端看還好、回家實體大螢幕覺得 UI 糊、場景粗糙」。① UI 糊＝`Resources/UI` 貼圖被 Compressed＋Bilinear 放大露塊狀髒點 → 全部 39 張改 Compression None，並加 `Assets/Editor/UITextureImportSettings.cs`（`AssetPostprocessor`，未來丟進 `Resources/UI` 的新圖首次匯入自動套 不壓縮/關 Mipmap/Sprite/MaxSize≥2048）。② 場景粗糙＝固定世界單位放大＋Point 非整數縮放毛邊＋AI 像素風源圖顆粒；`MapSpriteLoader` 加可切換 `SceneFilterMode`（`SetSceneFilterMode` 即時重套已載入貼圖），PerfHud（P）加「場景濾波(F)」按鈕/F 鍵現場比對，**定案採 `FilterMode.Point` 為預設**，Bilinear 保留比較。評估後未採 Pixel Perfect Camera（與 zoom/整張地圖/平滑跟隨/混雜 PPU 衝突，且美術是 AI 點陣圖）。附帶結論：**遠端桌面不能驗收畫質**（會重壓縮串流）。

* [x] 可放置場景特效系統 SceneFx（2026-07-02，見 [SCENE_EFFECT.md](SCENE_EFFECT.md)、[MapEditor_DESIGN.md](MapEditor_DESIGN.md)）：把「綁在 MapLoader 的佛陀煙霧 hack」演進成通用、可在編輯器逐個放置的粒子特效。編輯器新增「場景特效」分頁（新增特效、放置綠起點/紅終點、填參數）；資料存 `.dipanmap` 的 `sceneFx` 清單（`SceneFxInstance`：fxId/起終點/bulge/w/h/loop/intermittent/interval，兩專案 MapData 皆加）；外觀走 `Assets/Resources/Data/SceneFxTable.csv`（欄含 `Kind`）。兩種 kind：**stream**（`SceneFxEmitter`，弧線粒子流，煙/火/冰/毒；濃煙用花椰菜狀雜訊粒子＋自轉＋高密度）與 **portal**（`PortalFx`，起/終點＝矩形對角的平穩發光漸層光幕）。遊戲端 `MapLoader.BuildSceneFx` 依 kind 生成、換圖清除；舊 `SmokeEmitter`/`smokeObjectIds` hack 移除。**編輯器即時預覽**：每個特效旁「顯示/隱藏」跑與遊戲同一套程式（`SceneFxEmitter`/`PortalFx`/`SceneFxTable` 複製到編輯器 `Scripts/Preview`＋`Resources/Data`），移動點/改參數即時重建、刪除即移除。坑：`sortingOrder` 16-bit，傳送門要高於門的地上物（用 20000）才不被蓋住（見 [PROBLEMS.md](PROBLEMS.md) E4）。

* [x] 鏡頭區 camZone trigger（2026-07-02，見 [MAP_SYSTEM.md](MAP_SYSTEM.md) §2.2）：新增 trigger 類型「鏡頭區」，玩家踩進拉遠/位移相機、離開還原（給「走到佛像腳下拉遠看全貌」）。參數 `zoom`（>1 拉遠）/`offsetX`/`offsetY`。`CameraZoneWatcher`（MapManager 自掛）偵測進出，`MapCameraController` 加 `SetCameraZone/ClearCameraZone`＋平滑過渡（`zoneTransitionTime`），疊加在正常相機之上、換圖還原。`TriggerRegion` 加 `GetFloat`。

* [x] 地上物「可走」勾選（2026-07-02，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md) §4.3）：物件面板加「可走」勾選（`ObjectInstance.walkable`，兩專案皆加），預設不勾。勾選＝該地上物**不設碰撞、不擋路、不掛可破壞，且畫在角色下方**（sortingOrder 5 < 角色 10；否則像木板會壓在角色身上），走不走交給地圖可走層那格判定（例：木板/地毯可踩上去、角色走其上）。`MapLoader.BuildOneObject` 依此跳過 collider 與 `DestructibleObject`、並改用低排序。向下相容（舊物件預設不可走）。

* [x] 傳送點「使用傳送點外型」開關（2026-07-02，見 [MAP_SYSTEM.md](MAP_SYSTEM.md) §3.1/3.3）：傳送點 trigger 加 Bool 參數 `showMarker`（面板顯示「使用傳送點外型」），**預設打勾＝顯示**；取消＝`BuildTeleportMarkers` 跳過該點不生外型（給「放了自己的傳送門 SceneFx / 隱形傳送點」用）。遊戲端 `TriggerRegion.GetBool`（找不到/空＝顯示，向下相容）。順手讓編輯器 `TriggerParam` 支援中文 `label` 與 `boolDefault`（新建區域 Bool 預設值）；`CreateRegion`/`DrawParamField` 配合。

* [x] 資源載入改「module 級預載」（2026-07-02，見 [RESOURCE_LOADING.md](RESOURCE_LOADING.md)）：修正「同 module 房間互跳也彈讀取頁」的怪現象。`MapManager` 記 `_loadedModule`，`LoadMapRoutine` 分兩路：**跨 module**（進大地圖/回 Main/首次）＝讀取頁＋`MapLoader.PreloadModuleRoutine`（把該 module＋Main 的 catalog 圖一次解碼快取）＋分幀建圖；**同 module 房間互跳**＝資源已快取，同步 `LoadMap` 快速建圖、**不出讀取頁**。共用收尾抽成 `PlaceAndSetup`。記憶體取捨：預載常駐不自動釋放；怪物貼圖仍走 `MonsterSpriteLibrary` 首次出現才載（未納入預載）。

* [x] 存檔進度層 schema v2（2026-07-03，見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) §14）：把玩家遊戲進度接進統一角色存檔。**周目（大進度）= `generation`**、**完成關卡數（小進度）= `progress.clearedModules` 去重數**（「關卡」= MapsTable 的一個 `Module`，如 `RedBridalGown`）。`ProgressDTO` 新增 `clearedModules`/`unlockedModules`/`inheritedItems`/`hubIntroSpawnDone`＋`stats.currency`；`SaveManager` 開放進度 API（`MarkModuleCleared` idempotent、`ClearedModuleCount`、`Cycle`、`Currency`/`AddCurrency`/`TrySpendCurrency`、`HubIntroSpawnDone`、`CarryCountForCycle`）；`CharacterProfile` 加 `clearedModuleCount`/`slotIndex` 讓存讀檔 UI 只讀 `profiles.json` 就能畫卡片。設定常數（`HubMapId=12`、`SlotCount=3`、`MaxCarryOnReincarnate=7`、`LevelsToUnlockBoss=7`、Hub 落點名 `caveExit`/`center`）集中在 `SaveConstants`，關卡數量要改就改這裡。v1→v2 只補欄位、無資料搬遷。

* [x] 標題畫面＋三欄存讀檔 UI＋總流程（2026-07-03，見 [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md)）：把「一 Play 就跳進關卡」改成 **標題（`TitlePanel`，主標《燃燈劫》/副標 Burning Lamp: Rebirth of Ruin）→ 三欄存讀檔（`SaveSlotPanel`）→ 玩家選擇**。新增 `GameFlowManager`（＋開機 bootstrap，仿 SaveManager/UIManager 全程式建構）：開機把 `SaveManager.SuppressAutoLoad`／`MapManager.SuppressAutoStart` 設 true 改由流程驅動；**新建**（在該欄建角，有 Intro 就播開場鏈、沒有直接進廣場）／**繼續**（載該欄→進廣場中央）／**覆蓋**（`ConfirmPopup` 先問→重建）／**刪除**（測試用，先問）。**一欄＝一條獨立進度線＝一個角色**（`slotIndex`）。**進邪佛廣場（Map 12）= 自動存檔點**：踏進就存＋設 `hubIntroSpawnDone`；出生點由旗標決定（首次洞穴出口 `caveExit`、之後中央 `center`，`MapManager.PlaceAndSetup` 覆寫落點）。**輪迴資料層** `SaveManager.ReincarnateInPlace(carryIds)`：同欄位 `generation`+1、進度全歸零、帶入 `min(周目,7)` 件（0/負略過）、旗標重置、倉庫不動。UI 為佔位視覺（純色＋內建字型），待換素材。修一坑：開機抑制旗標整場有效導致「墜落動畫後 MainScene 全黑」——新建走開場鏈分支要把 `SuppressAutoStart` 設回 false 交還既有流程（記在 [PROBLEMS.md](PROBLEMS.md) H1）。

* [x] 進場一次性效果系統＋睜眼醒來（2026-07-03，見 [MAP_ENTER_EFFECT.md](MAP_ENTER_EFFECT.md)）：新增一種**進圖時播一次就結束**的螢幕後處理過場，語意是「一次性事件」而非「持續狀態」，故獨立於持續性的 Atmosphere（螢幕氛圍）／SceneEffect（世界特效）。第一個效果「睜眼醒來」用在初始洞窟 Main_Cave（承接墜落昏迷）：全黑→眼皮杏眼狀裂開→沉重眨一下→模糊轉清晰＋亮度回正＋暗角收斂→完全睜開、移除。`Resources/Shaders/EyeOpen.shader`（`_Open` 眼皮遮罩／`_Blur` 圓盤模糊／`_Bright` 亮度暗角）＋`Assets/Scripts/MapFx/EyeOpenController.cs`（自生成單例、3 條 AnimationCurve 驅動時間軸、眨眼藏在 open 曲線、`EyeOpenBlit` OnRenderImage 掛 Camera.main 播完停用，仿 AtmosphereController）。資料驅動：`MapsTable.csv` 加第 9 欄 `EnterEffect`（0 無／1 睜眼，向下相容），`MapManager.PlaceAndSetup` 進圖時 `ApplyMapEnterEffect(row.enterEffect)`；Main_Cave（11）已填 1。開頭「全黑停一下」剛好蓋過 LoadingPanel 收尾、交接無縫。節奏/眨眼/模糊皆可調（controller 的 duration/maxBlur/曲線）。

* [x] 標題畫面美術＋佛陀動畫＋火焰特效（2026-07-03，見 [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md)）：把 `TitlePanel` 從純色佔位換成正式視覺。**① 佛陀開場動畫**：中間偏右放 `Resources/UI/TitlePanel/BuddhaTitle/BuddhaTitle_01..NN`（自動偵測幀數、程式逐格播、用 unscaledTime 因面板暫停）；**按下開始才播一次 → 播完再多停 `BuddhaEndHold`（預設 1 秒）→ 才開 `SaveSlotPanel`**（無幀則直接開）；播放中鎖按鈕防重複、回標題自動重置回第一幀。**② 正式素材置換**：標題圖（3:1）取代文字主副標（⚠ 2026-08-19 起搬到 `Resources/UI/Texts/<語言>/TitlePanel_Title`）；開始鈕改用 `Resources/UI/Common/StartGameBtn`（無字圖）＋程式補「開始遊戲」字；文字群往左（`TextGroupX`）與偏右的佛陀錯開。找不到圖會退回文字/佔位。**③ 標題火焰特效** `Assets/Scripts/UI/TitleFireFx.cs`（UI 端、unscaledTime）：全螢幕持續落火（`UiFallingEmber`）＋標題燃燒（背後脈動火光＋柔邊高斯羽化＋寬度抖動的火舌 `UiRisingFlame`）。**刻意不直接用 MapsTable 火雨**——那是世界端 SpriteRenderer、綁 `Camera.main`/`deltaTime`，面板暫停中不動、且會被不透明 UI 蓋掉；改在 Canvas 上重做、複用火雨的程序生成佔位圖 `SceneEffectSprites`。位置/大小/節奏全在 `TitlePanel`／`TitleFireFx` 上方常數。曾另做「佛陀兩肩蒸騰濃煙」（照搬 `SceneFxEmitter` 的 fbm 花椰菜煙塊做法，因乾淨柔光圓不像煙），評估後**決定不放、已移除**。

* [x] 部署改用 itch.io + butler（2026-07-03，見 [DEPLOY.md](DEPLOY.md)）：淘汰「build 進 git → 推 GitHub → PC `git pull`」的舊流程——git 存整包大二進位 build 必然撞 GitHub 100MB 單檔上限（`.resS` 已 176MB）又讓 repo 歷史無限膨脹。改用 itch 官方 `butler`：位元組級**差分上傳**、無大小限制、有版本、免費。`deploy_only.sh` 從「rsync + git push」改成 `butler push Builds/Windows_Test sorrowslee/dipan:windows`（帶日期版本、排除 Burst 除錯資料夾）；`BuildScript.cs` 拿掉「打包前對齊遠端 git」步；PC 端改用 **itch app** 自動增量更新＋一鍵 Launch，取代 `pull_and_run.bat`。舊 `update_deploy.sh`／`BUILD_AND_DEPLOY.md` 已刪，`DipanProj_Deploy` git repo 退休。踩坑：`broth.itch.ovh`（butler 下載主機）在 HiNet DNS 解不到 → 改從 `itchio.itch.io/butler` 瀏覽器下載繞開（記在 [DEPLOY.md](DEPLOY.md) 疑難排解）。

* [x] 標題流程 build 開機場景修正 + 新建過場黑幕（2026-07-03）：① build 場景 0 從 `Intro` 改成 `MainScene`（`BuildScript.cs` 的 `options.scenes` 順序，Windows/Mac 都改）——加了標題流程後開機要停在 MainScene 顯示標題，Intro 只在「新建」時載入；順序錯會「開機直接播漫畫＋墜落後全黑」（同 H1 根因，記在 [PROBLEMS.md](PROBLEMS.md) A10）。② 新建有開場改走 `GameFlowManager.NewGameIntroRoutine`＋新增可重用 `ScreenFader`（`Assets/Scripts/Flow/ScreenFader.cs`，跨場景常駐黑幕、`sortingOrder` 30000、unscaledTime）：先蓋黑→關選單→載 Intro→淡出，修掉「按新建後標題面板閃一下才進漫畫」。

* [x] 角色 Y 排序（修「人物被較上面的地上物遮蔽」，2026-07-03）：地上物本來就依放置 Y 排序，但玩家/怪物是**固定 `sortingOrder`（10）**、沒進 Y 排序帶，於是永遠被非可走地上物蓋住。抽出單一公式 `MapDepthSort.Order(worldY, zOrder)`（＝既有地上物公式 `1,000,000 - Y*100`，靠 16-bit 繞回落在同一帶）；`MapLoader` 地上物改用它、新增 `YSortByFeet`（每幀依腳底 Y 覆寫 sortingOrder，玩家/怪物在各自 `Start` 自動掛，仿 BlobShadow）→ 角色與地上物依 Y 正確交錯遮蔽。連帶把會被「角色進帶」蓋掉的表演層抬到帶之上：`DamageNumberManager.SortingOrder` 600→24000、`VfxManager.SortingOrder`（擊中/發射特效預設）100→22000（均 16-bit 安全；地刺等「腳下」特效仍在 VfxTable 自填低值不受影響）。占位排序基準 `YSortByFeet.FeetYOffset` 可微調角色被遮的高低。

* [x] 排序續修：地面特效與飛行戰鬥視覺不再被地上物蓋住（2026-07-03，接上一則）：① **地面特效（GroundEffect，如冰/火 AOE）**原本 template sortingOrder 預設 0（地磚層）→ 被所有地上物蓋住。改成 `GroundEffectInstance.ApplyDepthSorting()` 依「中心 Y」進 Y 排序帶（`MapDepthSort`，含小幅 `YSortBias` 讓站在上面的角色仍畫在其上），移動型（跟玩家的佛光）每幀 Y 變動就重算。② **飛行戰鬥視覺抬到地上物之上以維持可讀性**：子彈 `Bullet.prefab` sortingOrder 10→22000、雷射 `LaserBeam` 光束/光暈 50/55/60→`BeamSortingOrder(22000)`＋5/+10。分工：地面 AOE＝floor 效果走 Y 排序（前方地上物該擋就擋）；子彈/雷射/擊中特效＝飛行/命中視覺畫在環境之上（看得清自己的攻擊）。

* [x] 地面特效排序改「用可走與否分上下」＋拋物線 NaN 防呆（2026-07-03，接上）：① 上一步把地面特效整團依中心 Y 進 Y 排序帶，實測大範圍 AOE 只有單一排序值 → 後方 tile 也蓋過地上物（火燒到祭壇上）。改為**固定 `GroundEffectSortingOrder = 8`＝高於可走地上物(5)、低於角色/一般地上物**：火在**可走石板/地毯之上**、在**祭壇/柱子與角色之下**——正好用地上物既有的「可走與否」自動分「火在其上或其下」，不必逐一判斷（見 [GROUND_EFFECT.md](GROUND_EFFECT.md)）。② 修拋物線/火焰拋擲彈的 `transform.position ... NaN` 洗版：`ShootParabolic` 清洗滑鼠世界座標（NaN/Inf→退回玩家前方）＋`BulletInstance` 加安全網（位移非有限就銷毀該彈），記在 [PROBLEMS.md](PROBLEMS.md) F3。③ **佛光例外**：`RenderMode=Glow` 的佛光是「跟著玩家的光環」，改為依中心 Y 每幀進 Y 排序帶（`ApplyAuraYSort`）＝和玩家同進退（玩家在祭壇前光環在前、在後被擋），不像地板火固定壓在低層被祭壇一律蓋住。

* [x] 效能與畫質診斷＋修正（2026-07-05，見 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md)、[PROBLEMS.md](PROBLEMS.md) E5~E7）：針對「fps 正常仍不順、PC build 畫面粗糙、UI 髒」做整輪調查與修正。**① 卡頓（已驗證有感）**：物理 50Hz（Fixed Timestep 0.02）×60Hz 螢幕拍頻＋Player/Monster Rigidbody2D 內插全關 → 角色每秒約 10 次微跳動，與 fps 無關；修法＝兩 prefab 開 `Interpolate`＋Fixed Timestep 改 `0.016666668`（60Hz）。**② 世界畫質**：地圖素材 256px/格在 1080p 只顯示 108px/格（0.42x 縮小），Point 濾波縮小取樣會亂丟像素產生噪點與移動閃爍（Mac Retina 編輯器看不出來、解析度愈低愈慘）；修法＝`MapSpriteLoader` 預設改 Bilinear＋`mipChain: true`（LoadImage 自動生 mipmap，記憶體 +33%），按 F 仍可切回 Point 對比。另查明 Main_Square 特別粗糙主因是 Atmosphere=5 的熱浪扭曲表演（曾試改低頻大波、作者偏好原觀感故還原，僅屬觀感選擇非 bug）。**③ UI 顆粒**：icon 原圖 256~500px 塞進 45~70px 格子（5~10 倍縮小、無 mipmap）；修法＝29 張小 icon meta `maxTextureSize`→128、6 張中型按鈕→512、全部開 mipmap，PNG 檔不動、風格不用換。**④ 制定素材尺寸規範**（PERF_QUALITY_AUDIT.md §4）：世界內素材＝佔幾格×256px、UI＝顯示尺寸×2；並確認 VSync 應保持開啟（KVM 60Hz 下 fps>60 顯示不出來，參 E1）。

* [x] 觸發鏈系統：trigger 接 trigger（2026-07-05，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)）：任何 trigger 完成後可用通用欄位 `next` 啟動同圖另一個 trigger，無限接續（對話→給物品→開門→傳送…）。**① 新動作型 trigger**（不綁位置、被鏈到立即執行）：`giveItem`（直接進背包＋toast，不用按 F）、`teleportTo`（直接換圖，不用踩傳送點）。**② 位置型解鎖**：`startDisabled` 初始停用＋被鏈啟動＝解鎖；`enableFlag` 旗標讓解鎖狀態跨存讀檔記住；teleport 加 `linkedFx` 連動場景特效（傳送門綠幕停用時隱藏、解鎖時亮）。**③ 條件旗標**：`requireFlag`（支援 `!` 否定，例 `!killedFamily`）/`setFlag`，存進角色存檔 progress.flags（SaveManager.GetFlag/SetFlag 新 API）。主遊戲端：新增 `TriggerChain` 靜態管理器（MapManager.SetupWatcher 先 Setup、RefreshTriggers 重建互動點）；teleport/cutscene watcher 每幀動態查 IsActive（解鎖即生效）；InteractionManager 建點時過濾＋`_consumed` 集合防重建復活；DramaPanel/TalkPanel OnClose 通知鏈「對話完成」。編輯器端：trigger 面板新增「觸發鏈/條件（通用）」欄位組（所有類型都有）、giveItem/teleportTo 兩種新類型（TriggerType.cs + triggerTypes.json）、場景特效面板顯示 id 供 linkedFx 參照。

* [x] 觸發鏈實裝與週邊修正（2026-07-05，接同日觸發鏈系統，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)）：**① camZone 入鏈**——「完成」=鏡頭拉伸到位（`MapCameraController.ZoneSettled` 2% 容差）才觸發 next；zoom 留空的 camZone = 隱形踩踏鏈起點；每次進區觸發一次、一次性靠下一節點 requireFlag。**② 邪佛大廳事件鏈實裝**（Main_Square）：`邪佛全貌`(camZone)→`邪佛對話`(drama id3、requireFlag=!hallGateOpen 防重複)→`給紅嫁衣劇本`(giveItem 104)→`劇本開門`(teleport startDisabled+enableFlag=hallGateOpen+linkedFx=綠幕)。新道具 104 劇本-紅嫁衣（ItemTable + icon）。**③ Talk 素材同步改遞迴**——NPC 立繪可放 `Talk/<NPC>/` 子資料夾（三處同步一致，[PROBLEMS.md](PROBLEMS.md) C5；另 C4 記編輯器素材半套、E8 記傳送門光幕 PPU 坑）。**④ 對話立繪微調**——DramaTalkTable 表尾六個選填欄（Left/Right 的 Scale/OffsetX/OffsetY），立繪框寬度改依圖片實際比例自動算（非主角比例的 NPC 圖不再被壓進固定框）。**待辦**：紅嫁衣「沒殺家人→榕樹妖」分支未實作，接手步驟寫在 TRIGGER_CHAIN.md §7（唯一要寫程式的是 MonsterData 加 DeathFlag 欄＋Die() 寫旗標）。

* [x] 旗標系統中度收斂＋旗標管理器（2026-07-06，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §2.5）：整套只留「一種旗標」，差在活多久——名字加 `永久:` 前綴或登記為 life＝跨輪迴（存 `CharacterSave.lifetimeFlags`），否則週目（存 `progress.flags`、輪迴清）。新增 `repeat`（重複規則四模式：每次進場/每次/每周目/永久）、`requireItem`（`itemId`＝須有、`!itemId`＝須無）、`requireCycleMax/Min`（周目上下限）等具名條件欄，全 AND 結算。存檔加終身旗標區、`InventorySystem.CountOf`。**編輯器旗標管理器**：旗標登記表（`flags.json`，id/name/scope）＋管理器 UI；三個旗標欄（條件/完成寫/解鎖）改成「填 id→確認→鎖成名字→刪除清空」的選擇法，不再手打。

* [x] 傳送門「放劇本開門」hub ＋ 強制新手教學（2026-07-06，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §4.9/§4.95）：邪佛給劇本後，靠近傳送門按 F 開 `ScriptsPanel`（強制連背包並排）→ 把劇本拖進單格方框→按鈕開對應傳送點（**目的地＝劇本 `ItemTable.TargetMapId` 決定，天生 hub**；劇本不消耗、留背包，只輪迴才移除）。`portal` 互動 trigger（`linkTeleport` 指向要解鎖的傳送點）＋按鈕 `TriggerChain.OpenPortal`（設目的地覆寫＋解鎖亮綠幕）。**強制新手教學** `TutorialManager`（一次性、寫死）：找邪佛手指→走到門邊定住只能按 F→遮罩＋手指指劇本（只能點）→劇本入框→手指指開啟鈕→按下開門→結束寫 `永久:tutorialPortalDone`。通用元件：`GuideFingerPanel`（指引手指，世界/UI 兩模式）、`TutorialDimPanel`（黑幕：整片全黑／中央留洞）、`TutorialBlockerPanel`（只放行指定元件可點）、`TutorialHintPanel`（上方大字）。傳送門綠幕改用 ScreenSpaceOverlay UI 覆蓋層繪製（免疫氛圍後處理壓暗，見 [PROBLEMS.md](PROBLEMS.md) E8）；黑幕中央洞改用「上下左右四塊實心黑」框出（程序生成圓洞貼圖某些環境不顯示）。

* [x] 鏡頭聚焦 trigger（把新手教學的運鏡改成資料驅動，2026-07-07，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3/§4.95）：新增動作型 trigger **`cameraFocus` 鏡頭聚焦**（被 `next` 串到時飄鏡頭到自己那格中心＋壓黑幕、停留、再拉回，表演完才接 next；參數 `holdSeconds`、`dim`＝中央留洞/整片全黑/無）。`MapCameraController.PlayFocus` 協程做運鏡序列、`TriggerChain.ExecuteCameraFocus` 掛黑幕＋定住玩家。**移除** `TutorialManager` 原本「背包一有劇本就飄鏡頭」的寫死流程，改由地圖上的對話鏈驅動（`給劇本→叫玩家去傳送門(對話)→鏡頭聚焦`），好處是可在給劇本後先補幾句引導對話、對話結束才飄鏡頭，不再「鏡頭已對著門卻跳獲得道具」。編輯器加 `cameraFocus` 類型（TriggerType.cs + triggerTypes.json）。

* [x] 修「對話接對話」關閉當幀重入卡死（2026-07-07，見 [PROBLEMS.md](PROBLEMS.md) D8）：觸發鏈把對話串對話時，前一段對話在 `OnClose` 裡**同步**接鏈又去開新對話 = 重入，導致「正在關的面板把剛開的新面板關掉、`IsOpen` 殘留 true」→ `UIManager` 持續暫停＋擋輸入但面板已停用 → 玩家永久卡死、看不到新對話。解法：新增常駐 `TriggerChainRunner.NextFrame(Action)`，`TriggerChain.NotifyDramaClosed` 改成**延後一幀**接鏈，等舊面板關乾淨再開新的。通則：別在模態面板 `OnClose` 裡同步開另一個模態面板。

* [x] 穿隧道洞口光暈改 shader（2026-07-08，見 [CUTSCENE_TUNNEL.md](CUTSCENE_TUNNEL.md) §2.2）：原本洞口外圈的光是烘進 `MakeTunnelMouth` 貼圖的一圈均勻柔暈（`MouthHalo`），太厚、像刻意的粗邊。改成洞口貼圖只留乾淨亮拱門＋柔邊，外圈光由新 shader `Custom/TunnelMouthGlow`（`Resources/Shaders/`，加法混合的全螢幕光暈）生成：由洞口中心徑向柔和遞減（無硬邊）＋放射光束（角度 fbm、沿半徑不變）＋霧感（低頻 fbm），`_Anim` 餵 `unscaledTime` 微微流動（暫停中也動）。`TunnelWalkController` 加一層 `Glow` Image（黑底之上、洞口之下）、每幀餵參數，暴露 `GlowColor/RayStrength/RaySharp/RayFreq/Haze/Spread/RadiusFrac/CenterYFrac/AnimSpeed` 可調，`MouthHalo`/`MouthHaloWidth` 停用。**光暈完全跟著洞口等比縮放**（半徑/散開/圓心位移都乘當前洞口大小 `_exitCur`，`GlowSpread` 改為「相對洞口大小」的比例）——修掉「洞口放大、光暈沒跟著放大」的問題；**圓心用 `GlowCenterYFrac` 往下對齊拱門視覺中心**（拱門在貼圖裡偏下），修掉「縮小半徑只蓋得到洞口上半」的問題。

* [x] 走隧道「點左鍵」閃爍提示（2026-07-08，見 [CUTSCENE_TUNNEL.md](CUTSCENE_TUNNEL.md) §2.2）：走隧道靠空白鍵或左鍵前進，故走隧道期間在畫面右下角顯示 `Guide_MouseLeft` 提示圖並閃爍（頻率同新手教學 `PlayerHintPanel`＝3.3），收尾時收起、畫在洞口之上白光之下。`TunnelWalkController` 直接畫在隧道自己的 canvas 上（隧道是全螢幕過場、沒有世界玩家可掛，與 `playerHint` trigger 各自獨立），欄位 `HintImage`/`HintHeight`/`HintPos` 可調。

* [x] 玩家提示圖 trigger（移動教學，資料驅動，2026-07-08，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3/§4.95）：新增動作型 trigger **`playerHint` 玩家提示**（被 `next` 串到時在玩家頭上左上／右上各擺一張提示圖、指定張閃爍，到收起時機自動收才接 next；參數 `leftImage`/`rightImage`＝檔名、`flashLeft`/`flashRight`、`hideOn`＝移動/攻擊/任意鍵）。`PlayerHintPanel`（跟隨玩家、閃爍、三種收起判斷）＋`TriggerChain.ExecutePlayerHint`。第一個用途：醒來對話（DramaTable 6）後接 `playerHint`＝**移動教學**（左 `Guide_Wasd` 不閃＋右 `Guide_Press` 閃、收起=移動）；攻擊教學之後放 `Guide_MouseLeft` 照抄。「只出現一次」用通用旗標欄（完成寫 `永久:xxx`＋條件 `!永久:xxx`），不寫死。左右槽 XY 位移是面板常數（編輯器只選左右）。編輯器加 `playerHint` 類型（TriggerType.cs + triggerTypes.json）。

* [x] 進場觸發 trigger（一進地圖自動觸發，2026-07-07，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3/§4）：新增 trigger 類型 **`onEnter` 進場觸發(自動)**——進入地圖、載入完全結束後自動觸發，**不用玩家踩、不塗格子**（編輯器用「＋ 手動新增空區域」建 0 格節點、從區域清單選取設參數）。純鏈起點：自己不做事，靠 `next` 接要做的事（典型＝接 0 格 drama 節點「一進房間就播對話」）。參數 `delaySeconds` 延遲秒數；通用條件（旗標/周目/道具）與 `repeat` 重複規則全可用（`每周目`/`永久` 用與互動點同一套「已觸發:id」自動旗標，`TriggerChain.RepeatAllows/MarkRepeatSeen`）。點火時機由 `MapManager.FireEnterTriggersRoutine` 統一排程：載入頁關閉→**自動等進場效果（睜眼）播完**→等延遲→依區域清單順序點火，前一顆的鏈開了對話會等對話關閉才點下一顆；期間換圖中止。編輯器加類型（TriggerType.cs + triggerTypes.json）、`EyeOpenController` 加 `IsPlaying`、`TriggerChain` 加 `TypeOnEnter`/`DramaPending`。

* [x] 睜眼醒來連動玩家「趴地→起身」（2026-07-07，見 [MAP_ENTER_EFFECT.md](MAP_ENTER_EFFECT.md) §1.5）：EnterEffect=1（睜眼）的地圖，玩家進圖即定格在 `dead` 逐格幀的最後一幀（趴地），睜眼播完後**倒播 dead＝爬起**（零新素材，起身＝死亡動畫倒轉），回 idle 恢復操作後才點火進場觸發（甦醒對話等玩家站起來才開始）。`PlayerAnimator` 加 `HoldLyingPose()`/`PlayWakeUp(onDone)`/`IsWakeUpBusy`（表演中忽略 `SetState`，真死 Dead 例外會打斷）；`MapManager.PlaceAndSetup` 只記需求（`_wakeUpWanted`），趴地與起身都在 `FireEnterTriggersRoutine` 執行——**玩家第一次生成時 `PlayerAnimator.Setup` 在 `Start()` 才載幀，同幀更早的 PlaceAndSetup 拿不到 dead 圖**（記在 [PROBLEMS.md](PROBLEMS.md) G5），協程開跑時已載好、且睜眼開頭全黑蓋住趴下瞬間。起身期間 `SetExternalHold(true,false)` 定住輸入不暫停；血統沒 `dead/` 圖自動跳過。

* [x] 殺怪／破壞觸發旗標（資料驅動、綁擺放，2026-07-09，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §7、[MONSTER_SETUP.md](MONSTER_SETUP.md)、[DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md)）：判斷「有沒有殺某怪／打破某物」→ 寫旗標 → 接觸發鏈 `requireFlag`（例：紅嫁衣關殺家人→`killedFamily`→新娘生氣分支）。刻意**綁在編輯器的擺放**而非 CSV 怪物種類，可讓同種怪在不同地方死寫不同旗標、全在編輯器編。怪物出生點 trigger 加「死亡觸發旗標」欄（`deathFlag`，isFlagRef 從旗標登記表選）；地上物選取面板加「破壞旗標」欄（`ObjectInstance.breakFlag`，只可破壞物件有效）。遊戲端：`MonsterController.Die()`／`DestructibleObject.Die()` 呼叫 `TriggerChain.SetFlag`；`MapLoader.SpawnMonstersFromMap` 讀 `deathFlag` 傳給 `MonsterSpawner.SpawnMonster`→`MonsterController.DeathFlag`，`BuildOneObject` 把 `inst.breakFlag` 傳給 `DestructibleObject.Configure`。編輯器端把旗標選擇欄抽成共用 `EditorUI.DrawFlagFieldCore`（trigger 參數與物件面板共用）、`ObjectController` 複製帶 `breakFlag`、`ObjectInstance`/`MapModel` 兩專案各加欄。**紅嫁衣分支**：偵測機制已完備，待作者在編輯器建 `killedFamily`（周目）、家人怪出生點填它、新娘房拉兩條 drama 分支（生氣打王／感謝→teleportTo 榕樹妖 map10）；兩場頭目戰 AI／技能另做。

* [x] 旗標管理器改依 id 由小到大排序（2026-07-09）：原本 `FlagRegistryStore.Save` 存檔前 `SortByName()` 依名稱字母排 → 一按「儲存」畫面上的清單就重排跳位。改成 `FlagRegistry.SortById()`（依 id），`Save` 與 `Load` 都套 → 存檔順序穩定、開管理器一律 id 順序、新增的排最後不跳。id 不變、遊戲端用名字／id 查表，行為不受影響。

* [x] 重複規則選項「每次進場」改名「關卡單次」（2026-07-09，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §2.5.1）：預設選項字面改成更貼切的「關卡單次」（進這張圖只觸發一次、離圖重進才會再有）。純標籤：`InteractionManager.ParseRepeat` 本來就「非 每次/每周目/永久 → 預設 Visit」，改字不影響行為；另補明確 `case "關卡單次"`（舊值「每次進場」保留相容）。同步改 `TriggerType.cs`/`triggerTypes.json` 選項、相關註解與文件。

* [x] 測試快捷「直接進某關卡／地圖」DevQuickStart（Editor-only，2026-07-09，見 [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md) §3）：反覆測單一關卡不必走「標題→讀檔→廣場→開傳送門」。選單 `Project Tools/測試/直接進關卡` 一鍵切 紅嫁衣／初始洞窟／邪佛廣場／關閉（有勾示意）。`Assets/Editor/DevQuickStart.cs`（不進 build）在 `AfterAssembliesLoaded` 關掉 `GameFlowManager.TitleFlowEnabled`，再用 `MapManager.DevStartModuleOverride`（進 module 首圖）或 `DevStartMapId`（直接進某地圖，如廣場 map12＝非首圖）覆寫開機目標——**不動場景序列化的 `startModule`（＝Main）**，關閉即恢復正式流程。`MapManager.Start` 依覆寫決定 `StartLevel`/`GoToMap`（首次載圖 `PlaceAndSetup`→`PlacePlayer` 會生玩家）。

* [x] Play 模式加速 ＋ static 殘留保險（2026-07-09，見 [PROBLEMS.md](PROBLEMS.md) I2/I3）：開發時按 Play 到跑起來很慢——`EditorSettings` 的 **Enter Play Mode Options 開啟**（`m_EnterPlayModeOptionsEnabled: 1`，選項 3＝Domain/Scene reload 都停用）→ 進 Play 幾乎瞬間。代價是關掉 Domain Reload 後 C# static 不自動歸零，上一輪殘留讓「第二次以後 Play」出錯（黑畫面、重複觸發、**角色只剩影子**）。新增 `Assets/Scripts/PlayModeStaticReset.cs`（`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 每次進 Play 最早期統一重置）：抑制/流程旗標（`MapManager.SuppressAutoStart`／`SaveManager.SuppressAutoLoad`／`GameFlowManager.TitleFlowEnabled`／Dev 覆寫）、`TriggerChain.ResetForPlayMode()`（清集合＋static 事件 `OnTriggerFired`）、`FlagRegistry.Reload()`，以及**四個素材庫懶漢單例** `PlayerSpriteLibrary`/`MonsterSpriteLibrary`/`DramaDatabase`/`DramaTalkDatabase` 的 `ResetForPlayMode()`（它們快取 runtime 載入的 Sprite，第二次 Play 會回傳被銷毀的 sprite → 角色/怪物只剩影子、劇情圖/立繪空白）。另加 `MapManager.DevLoadingHoldSecondsOverride`＋DevQuickStart 在編輯器把載入頁停留 2 秒歸零（build 維持）。**以上全只影響編輯器，打包版不受影響**（Editor-only 腳本不進 build；`PlayModeStaticReset` 在 build 中是重設「已是預設值」的 no-op）。

* [x] 破幻術轉場（幻境崩碎回歸現實，2026-07-09，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3/§7、[MAP_ENTER_EFFECT.md](MAP_ENTER_EFFECT.md) 附節）：紅嫁衣「沒殺家人→對話完→傳去榕樹妖」這段加轉場特效——玩家親眼看到婚境龜裂崩碎、收尾全白，再無縫接跨關載入頁。刻意**做成觸發鏈動作 `illusionBreak` 而非 MapsTable `EnterEffect`**：EnterEffect 綁地圖、在目標圖載完才播（只吃得到新場景）、每次進圖都播；破幻術要的是「崩的是舊幻境、只在這劇情節點播一次」，故由劇本在還在紅嫁衣場景時接鏈播放。新增 `Assets/Scripts/MapFx/IllusionShatterController.cs`（仿 `EyeOpenController`：自生成常駐單例、曲線驅動 `_Progress`/`_Crack`、`unscaled` 時間、blit 掛主相機、`SetExternalHold(true,true)` 暫停擋操作；`Play(onDone, duration)` 播完才回呼，回呼裡先接鏈開載入頁再停 blit＝不閃回幻境）＋ `Resources/Shaders/IllusionShatter.shader`（IQ 兩趟 voronoi 玻璃裂紋＋碎塊依 cell 亂數方向/相位錯開崩落＋色散翻轉＋露白光＋收尾全白）。`TriggerChain` 加 `TypeIllusionBreak`／`ExecuteIllusionBreak`（`duration` 選填）；編輯器加 `illusionBreak` 類型（TriggerType.cs + triggerTypes.json）。串法：`紅嫁衣對話(requireFlag=!killedFamily)→next=破幻術(illusionBreak)→next=送去榕樹妖(teleportTo map10)`。目前是**程序版**（崩碎層＝當前畫面剝暖色濾鏡的複本，露白光）；要真的紅嫁衣畫面崩到榕樹妖畫面得改 capture 版（換圖前抓截圖餵 shader），可後續升級。**只影響開發＋純新增，打包版正常。** 待作者：填紅嫁衣對話（DramaTable/DramaTalkTable）、在最終房間擺這三顆 trigger、Unity 內微調崩碎節奏。

* [x] 破幻術泛化成「播放螢幕特效」＋新增「關卡單次」旗標範圍（2026-07-09，接上則；見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §2.5/§3/§7、[MAP_ENTER_EFFECT.md](MAP_ENTER_EFFECT.md) 附節）：依作者回饋改兩件事。**① 別為每種特效加 trigger 型別**：把專用的 `illusionBreak` trigger 泛化成通用 `playScreenFx`（播放螢幕特效(鏈動作)），填一個 `effectId`；遊戲端新增 `Assets/Scripts/MapFx/ScreenFxPlayer.cs` 依 id 分派（**id 1＝破幻術**→`IllusionShatterController`），以後加特效只動「shader＋控制器＋`ScreenFxPlayer` 一個 case＋編輯器清單」，不再長 trigger 型別。編輯器 `effectId` 欄旁加「**螢幕特效表**」按鈕（`isScreenEffectRef`）開參照彈窗（`EditorUI.ScreenFxCatalog` 列出可填 id、可點填入），解決「不知道能填什麼」。破幻術 shader／控制器沿用不變。命名用「螢幕特效」而非「場景特效」以免和世界端 `SceneEffect`/`SceneFx` 混。**② 新增第三種旗標範圍「關卡單次」**（`killedFamily` 該用這種）：只存記憶體 `TriggerChain._levelFlags`、**不進存檔**、**每次進新 module 由 `MapManager` 呼叫 `TriggerChain.ClearLevelFlags` 歸零**（同 module 房間互跳不清）——所以判的是「這一趟關卡有沒有殺家人」，周目旗標會殘留到下趟就錯。編輯器旗標管理器切換鈕改 3 段循環（周目→永久→關卡單次，`FlagDef.CycleScope`）；兩專案 `FlagRegistry` 各加 `level`/`IsLevel`；`TriggerChain.Resolve` 改三向、`FlagTrue`/`SetFlag`/`ResetForPlayMode` 一併處理。串法更新：`紅嫁衣對話(!killedFamily)→playScreenFx(effectId=1)→teleportTo map10`；`killedFamily` 設關卡單次。**純開發向＋資料驅動，打包版正常。**

* [x] 「Sync Map Assets」補上同步 flags.json（2026-07-09，見 [PROBLEMS.md](PROBLEMS.md) I4）：`MapAssetSyncTool` 原本只同步地圖與美術，從沒複製 `flags.json` → 編輯器改的旗標範圍（如 killedFamily 改關卡單次）在遊戲端不生效、退回當周目讀到存檔殘留值。加 `PullFlagsFromEditor`：把 `DipanProj_MapEditor/flags.json` 複製進 `StreamingAssets/MapAssets/`。修完要重跑一次 Sync 才會推進遊戲。

* [x] togglePortal 開關傳送點鏈動作（可隱藏可復原，2026-07-09，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3）：Boss 房/最終關需要「進門就把傳送門封起來、不讓玩家中途落跑」，且為後續關卡編輯保留「可復原」。新增動作型 trigger **`togglePortal`**（`target`＝傳送點名稱、`show`＝顯示解鎖/隱藏封鎖，預設隱藏）：`show=false`＝`DisableRegion`（加停用集＋隱藏視覺，執行期狀態不寫存檔）、`show=true`＝既有 `EnableRegion`（解鎖＋寫 enableFlag）。**視覺兩條一起管**——`ApplyTeleportVisual` 同時開關 `linkedFx` 綠幕與 `showMarker` 傳送點外型，看起來真的消失/出現（非只擋踩踏）。為此 `MapLoader` 新增 `TeleportMarkerById`（region id→marker）並經 `MapManager` 傳進 `TriggerChain.Setup`；`Setup` 的初始 startDisabled 也改用 `ApplyTeleportVisual`（外型跟著初始狀態）。註：「一開始就封、打贏才開」用既有 `startDisabled`+`enableFlag`+解鎖即可，本動作專給「本來看得到、中途才封」。編輯器加 `togglePortal` 型別（TriggerType.cs + triggerTypes.json）。**純新增，打包版正常。** ／ 追加：`target` 支援**多筆**——編輯器新增 `isPortalList` 參數旗標，`DrawPortalListField` 把該欄畫成多欄、按「＋」加欄「−」刪欄，存成逗號分隔字串；`ExecuteTogglePortal` 改 `Split(',')` 逐一開關（單筆＝無逗號，向下相容）。可一次封/開多個門。修：`DrawPortalListField` 存回時原本會**過濾空欄**，導致按「＋」加的空欄當幀就被濾掉、看似沒反應——改成讀寫都保留空欄（尾逗號留著、遊戲端讀時 Trim 後略過空的），「＋」加欄才會存活可填。（已測試 OK）

* [x] 特效預覽器「匯出換色版」檔名改 2 位補零（2026-07-09，見 [EFFECT_LIBRARY.md](EFFECT_LIBRARY.md)）：`EffectRecolor.ExportColorSet` 原本輸出 `_001.png`（3 位），與遊戲 `VfxManager` 的 `_{i:D2}`（`_01`…）不合、每次都要手動改名。改 `string.Format` 的 `{1:000}`→`{1:00}`（流水號仍 `i+1` 由 1 起算），匯出即 `_01.png`，可直接丟進遊戲免改名。（原色庫 `Effects/` 那邊仍是 `organize_bundle.py` 的 3 位命名，直接複製才需改名——文件已註明分兩種情況。）

* [x] 召喚型武器接玩家側（測試用，2026-07-09，見 [BOSS_MODULE.md](BOSS_MODULE.md) §3）：把召喚核心抽成擁有者無關的共用靜態 `SummonSystem.Cast(owner,originPos,recipe,aliveTracker)`，`MonsterWeaponUser`(boss) 與 `PlayerController.Shoot`(玩家) 共用。玩家 Shoot 在 BulletPrefab 守衛前先攔 `IsSummon`（耗魔→發射特效→Cast→FireInterval 節流；按住左鍵依冷卻重複召喚）。測試武器＝13「御靈水晶」(RecipeID→27：召喚 ZhaYu、FireInterval 1.5、上限 3、半徑 1.5；ZhaYu 任何地圖都有圖故免 Sync 即可測)。**注意**：召喚出的是敵人(Enemy 層、ChaseBrain 追玩家)，此版只驗證管線對玩家也通；玩家召喚=友軍(faction/友軍 AI)屬下一步。純新增，打包版正常。

* [x] 召喚陣營制（玩家召喚=友軍、怪物召喚=敵人）＋玩家御靈水晶可裝備（2026-07-09，見 [BOSS_MODULE.md](BOSS_MODULE.md) §4）：**① 新 Ally 層(8)**（TagManager）給玩家召喚的協戰怪，玩家子彈(打 Enemy 層)天生打不到自己的召喚物、召喚物也不推玩家；碰撞用 `FactionLayers` 於進場前 `Physics2D.IgnoreLayerCollision` 設定（Ally 穿 Player/Enemy/Ally、只被 Environment 擋，**免動 DynamicsManager 矩陣**）。**② `MonsterFaction`{Enemy,PlayerAlly}**：`MonsterController.Faction` 決定追誰(Enemy→玩家/PlayerAlly→最近敵怪 `FindNearestEnemy`)、接觸傷害打誰(`EnemyContactDamage` 改陣營制 hostileMask，Enemy 打 Player\|Ally、Ally 打 Enemy，統一 OverlapCircle+Distance+CombatSystem)、在哪層。`SummonSystem.Cast`/`MonsterSpawner.SpawnMonster` 加 faction 參數；玩家 Shoot 傳 PlayerAlly、boss MonsterWeaponUser 傳 Enemy。**③ 御靈水晶可裝備**：ItemTable 加物品 13(WeaponID 13、icon weapon_crystal)、InventoryLauncher 預設物品欄改塞 1~13。純新增/向下相容，打包版正常。

* [x] 修召喚陣營兩坑：友軍打不到敵怪 ＋ 召喚物過傳送點消失（2026-07-09，見 [BOSS_MODULE.md](BOSS_MODULE.md) §4、[PROBLEMS.md](PROBLEMS.md) B7）：**① 接觸傷害不對稱（友軍撞敵怪不掉血）**＝根因是接觸偵測改用了 `OverlapCircle`，撞上專案全域 `queriesStartInColliders=false`（貼身重疊漏抓、且因大小位置不同而不對稱）。改成維護 `MonsterController.Active` 全場登記表(OnEnable/OnDisable)＋逐一 `Physics2D.Distance` 判重疊（不受該設定影響，同敵人打玩家原本的穩定做法）；`EnemyContactDamage` 與友軍找目標 `FindNearestEnemy` 都改走登記表、移除 OverlapCircle。PlayModeStaticReset 清登記表。**② 召喚物過傳送點消失**＝換圖清場 `DestroyAllOfType<MonsterController>` 把友軍也砍了。改成清場**跳過 PlayerAlly**、`PlaceAndSetup` 放好玩家後 `RepositionPlayerAllies` 把存活友軍移到新落點附近（黃金角散開）。純改動，打包版正常。

* [x] 主角攻擊動畫接線（2026-07-09，見 [CHARACTER_SETUP.md](CHARACTER_SETUP.md)）：主角血統資料夾新增 `attack/` 子資料夾即可播攻擊動畫。**① `PlayerAnimator`**：Attack 從「預留一次性」改為**可循環**（`IsLooping` 加入 Attack）。**② `PlayerController.HandleVisuals`**：按住開火(空白/左鍵)時優先播 `attack`（放開後再撐 `AttackAnimLinger`=0.12s 才回移動狀態，單擊也看得到、連射不閃）；沒有 attack 圖的血統 `Has(Attack)=false` 自動退回 Walk/Idle，行為不變。Base 已放 25 幀 cast。**需跑 `Project Tools → Sync Map Assets`** 才會把 attack 幀＋catalog 推進 StreamingAssets 生效。fps 沿用 PlayerAnimFPS。

* [x] 修 boss 不逃跑不召喚（BrainType 沒 Trim）＋ 記錄怪打怪傷害忽勝忽敗（2026-07-09，見 [PROBLEMS.md](PROBLEMS.md) F4/F5）：**① 已修**——`MonsterSpawner.LoadMonsterData` 的 `BrainType`/`Weapon` 加 `.Trim()`；CSV 值帶前導空白（" RedBridalGown"）害 switch 對不上、掉回 default=ChaseBrain（其他怪 default 也是 Chase 才一直沒露餡）。修後紅嫁衣才會走 `RedBridalGownBrain`（逃跑＋召喚）。**② 已記未修（待作者調數值）**——召喚物 vs 敵怪對打忽勝忽敗＝接觸傷害每幀結算＋怪 InvincibleTimeMs=0＋HP≤ContactDamage 幾乎一擊斃命 → 勝負看 Update 順序。解法：給互毆的怪 InvincibleTimeMs>0(節流) ＋ 平衡 HP/ContactDamage，詳見 F5。

* [x] 修召喚三問題：怪打怪傷害、友軍跟隨、御靈水晶消失（2026-07-09，見 [PROBLEMS.md](PROBLEMS.md) F5/D9、[BOSS_MODULE.md](BOSS_MODULE.md)）：**① 怪打怪忽勝忽敗**＝接觸傷害每幀結算＋一擊斃命 → `EnemyContactDamage` 加「同攻擊者對同目標重擊冷卻 0.5s」(攻擊速率化，不再看 Update 順序) ＋ 測試怪 1~12 調 HP12/ContactDamage4(非一擊死，看得到互毆)。**② 友軍不跟玩家**＝原本只追敵怪、沒敵怪就發呆 → 新增 `AllyBrain`（沒敵怪跟玩家 FollowNear 2.2、有敵怪 AggroRange 7 內去打），`MonsterContext` 加 `Enemy` 欄、`MonsterController` 雙目標(Player 跟隨+Enemy 攻擊)＋面向敵怪、`MonsterSpawner` 對 PlayerAlly 掛 AllyBrain。**③ 御靈水晶常從背包消失**＝SaveManager 開場 RestoreState 先清空再還原「舊角色存檔(沒有新武器)」、而 InventoryLauncher『全空才塞』跑在其後→跳過。改成 InventoryLauncher『缺就補』(＋`InventorySystem.HasAnywhere` 含裝備欄)。純新增/資料調整，打包版正常。

* [x] 重修「怪打怪傷害」為系統機制（第一擊必互換）＋攻速資料化（2026-07-09，見 [PROBLEMS.md](PROBLEMS.md) F5、[COMBAT.md](COMBAT.md)）：推翻前一版「加冷卻＋偷調數值」的錯誤修法（會逼作者每隻怪自己算數值、且沒解決根本的單邊挨打）。**① 致死延後銷毀**：`MonsterController.Die()` 只標記 `_isDead`、真正 `Destroy` 延到本幀 `LateUpdate` → 殺死一隻怪的那幀牠自己的 `EnemyContactDamage` 仍執行一次＝**死也能還手** ⇒ 不管 Update 順序/攻速差，接觸第一下一定雙方互換傷害（玻璃大炮不能無傷輾壓）。**② 攻速資料化**：新增 `MonsterData.AttackInterval` 欄（秒/越小越快/空 0.5），`EnemyContactDamage` 每隻怪各自用；第一擊互換與攻速分離。**③ 還原**先前偷調的 HP/ContactDamage（ZhaYu HP10、幽靈 HP3、接觸 10）。純機制＋資料欄，公平性由系統保證、數值交回作者。

* [x] 召喚特效（邊播特效邊生怪）＋施放冷卻統一提示（2026-07-10，見 [VFX.md](VFX.md)、[BOSS_MODULE.md](BOSS_MODULE.md) §3、[RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)）：① **召喚特效**：召喚型武器（`IsSummon`）可在**武器表**填 `SummonEffectID`（引用 VfxTable，與 FireEffectID/HitEffectID 同放武器表），施放時在**每個生怪點播一次特效、同一幀就生怪（邊播邊出現）**。`SummonSystem.Cast` 加 `summonVfxId` 參數（>0 時 `VfxManager.Spawn` 播特效＋同幀 `SpawnMonster`）＋ `HasRoom` 查空位；`WeaponData`/`WeaponManager` 加欄解析；`PlayerController.Shoot`（玩家 PlayerAlly）與 `MonsterWeaponUser.TrySummon`（boss Enemy）各傳武器的 `SummonEffectID`。資料：武器 14（紅嫁衣召喚）、13（御靈水晶）填 VfxTable 10「招喚怪物」。② **施放冷卻統一**：所有離散武器（一般/拋物/連鎖/雷擊/環繞/召喚）冷卻中「按下」攻擊 → 不動作、不扣魔、跳中央 toast「技能正在冷卻中」（`PlayerController.HandleFiring` 的 `_fireTimer` 閘門加 `ShowCooldownAlert`，節流 0.4s、用既有 `AlertPanel.Toast`）；並修召喚「達同時上限時扣了魔卻沒生怪」＝扣魔/進冷卻前先 `HasRoom` 確認有空位；召喚已達上限時按攻擊另跳「召喚數已達上限」提示（與冷卻提示共用 `ShowSkillAlert` 節流）。另：**攻擊動作只在真的發射出去才擺**——`Shoot` 改回傳「是否真的發射」，`HandleFiring` 只在回 true 時設 `_attackAnimUntil`；`HandleVisuals` 不再看按鍵、改看「發射成功 or 雷射/佛光在放」，所以 CD 中／召喚已滿／魔力不足時角色不再空擺攻擊姿勢。雷射/佛光為按住持續型、無單發冷卻，維持原路徑。純新增/資料，打包版正常。

* [x] 召喚特效跟著怪物大小（2026-07-10，見 [VFX.md](VFX.md)）：召喚特效原本固定大小（puff 64px@PPU100＝0.64 世界單位），配上大怪不成比例（怪高＝`CharacterWorldHeight` 1.95 × Scale）。`VfxManager` 新增 `SpawnSizedToHeight(id,pos,targetHeight)`＋`Spawn` 加 `extraScale` 參數；`SummonSystem.Cast` 改「先生怪 → 讀該怪可見高度（`MonsterController.CharacterWorldHeight` × transform.Scale，同幀已就緒）→ 把特效縮放到該高度」。`VfxTable` 該列 `Scale` 對召喚特效改當「相對怪物高度的倍率」（id 10 由 3 改 1.3＝比怪大 30% 包住牠）。大怪大特效、小怪小特效，仍在生怪同一幀。純新增，打包版正常。

* [x] 怪物障礙迴避＋感測範圍說明（2026-07-10，見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)）：怪物原本 `MonsterActuator.MoveTowards` 直線衝向玩家，遇牆/地上物就被物理擋住卡住。改成局部避障：`CircleCast` 往前（`AvoidLookahead`=1.5）探自身半身寬（`AvoidProbeScale`=0.9）的圓，被擋就往兩側逐步加大角度（18~155°、帶側向遲滯 `_avoidSign`）找最接近原方向的暢通方向滑過去（貼牆繞行、鑽窄縫），一整圈都堵死才 Stop。查 `Environment`＋`Water` 層。`ChaseBrain`/`AllyBrain` 自動吃到；**紅嫁衣 boss 逃跑刻意 `AvoidObstacles=false`** 維持原「被卡住讓玩家追上」設計。另釐清 **`DetectionRange`＝世界單位＝地圖格數**（tileSize=1）：紅嫁衣房間 18×10 格、對角≈20.6，要全房看到玩家設 ≥21（一般怪預設 10、boss 30）。純新增/資料，打包版正常。

* [x] 修召喚出生在牆裡＋怪物避障凍結（2026-07-10，見 [PROBLEMS.md](PROBLEMS.md) F7/F8、[ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)）：① **召喚避牆**：`SummonSystem.FindSpawnPos` 在 `SummonRadius` 環上取點時用 `Physics2D.OverlapCircle(0.35, Environment|Water)` 驗證，撞牆就換角度/往內縮，都不行退回施放者腳下——怪不再出生在牆裡。② **避障不凍住＋解卡**：`SteerAround` 一整圈被擋時改回傳 desired（不再回 zero→Stop）；`MonsterActuator.UpdateStuck` 偵測「想動卻沒位移>0.25s」→ 側滑 0.4s 脫困、換邊。修掉「怪追一追凍在空地」。避障探測參數順手放寬（Lookahead 1.5→1.2、ProbeScale 0.9→0.75，更會鑽縫）。純新增/技術修正，打包版正常。

* [x] 怪物尋徑改全域 A*（2026-07-10，見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)）：局部避障繞不出大障礙（怪離玩家很近卻卡在牆另一邊），改成真 A*。新增 `Scripts/AI/MapNavGrid.cs`（單例）：每次載圖 `MapManager.PlaceAndSetup` → `MapNavGrid.EnsureBuilt(MapCoords.WorldBounds)` 用 `Physics2D.OverlapCircle` 逐格（CellSize 0.5、AgentRadius 0.4 淨空）掃 `Environment`＋`Water` 建可走格（**牆＋地上物自動含**）；`TryFindPath` 八方向 A*（min-heap＋不穿牆角）＋視線平滑（string pulling）出少數航點。`MonsterActuator.MoveTowards` 改：直線可達就直走（細射線判定）、否則跟 A* 路徑（0.35s 重算）、卡住 0.3s 自動側滑解卡、永不凍住；沒 nav 退回舊局部避障。boss 逃跑維持 `AvoidObstacles=false` 走直線。純新增，打包版正常（限制：破壞家具開路後格不即時重建）。

* [x] 榕樹妖 boss 戰鬥模組（地刺／三階段／三大絕，2026-07-10，見 [BOSS_MODULE.md](BOSS_MODULE.md) §6、[PROBLEMS.md](PROBLEMS.md) F14）：與紅嫁衣相反的 boss——本體＝**無圖隱形樹**、不可直接打，用**地刺**攻擊，玩家閃地刺並**打冒出的地刺反傷本體**。新增 `Scripts/AI/BossSpike.cs`（地刺攻擊實體：箭頭預警 VfxTable13→冒出 VfxTable11→危險窗開 Enemy 層 trigger＋`EnemyContactDamage` 連續扣血、實作 `IDamageable` 把傷害轉本體＋特效閃白光→收回自毀）＋ `Behaviors/BanyanTreeBrain.cs`（讀 `HealthFraction` 切三階段：>50% 慢少、50~20% 加量加速、<20% 只放大絕）。**三大絕隨機輪流**（橫掃推進浪／畫面中央放大版大地刺 scale2.24／狂亂 20 根隨機地刺），且**同一招最多連兩次、第三次強制換**。**地刺受傷範圍＝貼齊可見圖的 base-anchored 框**（讀 `VfxInstance.WorldBounds`、框底對齊地刺基座往上長、寬×0.75 高×0.60；因地刺圖從底部往上長、實體只在下半，不能用置中固定框，見 F14）；所有地刺共用同規則。附**除錯紅框** `BossSpike.DebugDrawHitbox`（LineRenderer 畫在 Game View 對照調整，預設 false）。資料：MonsterData.csv 第 14 列 BanyanTree（HP100/不動/接觸0/DetectionRange40）、地圖 `RedBridalGown_TreeDemon.dipanmap`（只有下半可走）。待辦：換臉（vicious↔crazy）、測完關 ForcePhase/除錯 log、實機微調難度。純新增，打包版正常。

* [x] Boss 開戰資訊表演（bossIntro 鏈動作＋BossIntroPanel，2026-07-12，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3）：開戰表演「暫停 → 壓黑底版＋血色暈影淡入、上下電影黑邊滑入 → 中央播 VfxTable 14『警告』特效（播滿 WarnSeconds 消失）→ 左滑入 boss 頭像（Talk 立繪）、右滑入空白姓名牌匾（專屬圖 Resources/UI/BossIntroPanel/BossIntroPanelNameBG）→ 名字以扭曲抖動的半透明毛筆字漸漸復原＋淡入浮現（NameWarpEffect 頂點特效）→ 停留、淡出、接 next」，**不可跳過**（無任何按鍵/點擊捷徑）。**① 新動作型 trigger `bossIntro`**（`monsterId`＋選填 `warnVfxId`）：`TriggerChain.ExecuteBossIntro`，NextFrame 開面板避 D8 重入、表演完才接 next；編輯器 `TriggerTypeSet.Defaults()` 同步加型別（TriggerTypeStore 自動補進舊 triggerTypes.json）。**② `MonsterData.csv` 加 `DisplayName`／`PortraitPath` 兩欄**（`Name` 是程式鍵＝動畫資料夾索引不能顯示用；PortraitPath＝Talk 立繪 catalog id，複用 `DramaTalkDatabase.ResolvePortrait` 管線零新載圖程式）；`MonsterSpawner` 解析（Trim，F4）＋新增 `GetData(id)` 公開查詢。已填紅嫁衣（redBridalGown_angry）與榕樹妖（treeFace_vicious）。**③ `BossIntroPanel`**（UIPanel，Overlay/PausesGame/擋輸入/ESC 不誤關）：警告序列幀借 VfxManager 已載好的 sprites 在 UI 端 **unscaled** 逐格播（世界端 Spawn 會被暫停凍住）；壓迫感配件每項可關（壓黑底版 DimAlpha／電影黑邊 LetterboxHeight／血色暈影 VignetteAlpha＝程序生成漸層+Perlin 呼吸／名字扭曲 NameWarpAmount·Speed）；**節奏/版面全是 public 欄位**（Play 模式選 [UIManager]/Layer_Overlay/BossIntroPanel 即時調、重觸發套用；定案回填程式碼預設值）；資料缺哪塊就略過哪塊不擋流程。**④ 字體**：引入**莫大毛筆**（Bakudai Bold，SIL OFL，`Resources/Fonts/Bakudai/` 含授權檔；原始包 169MB 移到專案根 `FontsSource/`——Resources 底下的檔案**不論有無用到都會全數打包進 build**，別把整包字體 repo 放進去）；只用在 boss 姓名牌（`NameFontPath` 欄，字級 108），全 UI 其他文字維持內建字型；`UIBuilder` 新增通用 `LoadFont(path)`（載不到警告＋退回預設）。臨時測試熱鍵 BossIntroDebugHotkey（按 L 播表演）已於調校完成後移除。純新增/資料，表演參數已由作者實機調校定案。

* [x] 榕樹妖 boss：死亡整棵樹燃燒演出 ＋ boss 死亡回收招式 ＋ 手感調整（2026-07-10，見 [BOSS_MODULE.md](BOSS_MODULE.md) §6.6/§6.7）：① **第三招大絕「狂亂地刺」**（`SpikeStorm`）＝一次隨機灑一大票一般地刺，與橫掃浪/大地刺三招隨機輪流，且「同一招最多連兩次、第三次強制換」（`_lastUlt`/`_ultRepeat`）。② **死亡演出**（新增 `Assets/Scripts/AI/BanyanBossFace.cs`，MapLoader 依 assetId 掛在臉地上物、並把臉改不可破壞）：boss 死 → 臉起火 1 秒 → 臉消失 → 各地陸續起火、間隔越點越短（越冒越多）、用網格鋪滿整棵樹範圍、**無限循環永不熄滅**（VfxTable 16 紅色鬼火 + `VfxManager.SpawnLoop` Duration<0）；不設固定上限（火點數＝範圍÷`FireSpacing`，現約 53 個）。原「發招換 crazy 臉」因兩張素材搭不上**決定不換臉**，管線保留停用。③ ⭐ **boss 死亡→招式立刻回收**（`MonsterController.Die` 同幀）：榕樹妖的地刺（`BossSpike._active` 登記表 + `BossSpike.CancelAll()`，連預警箭頭/地刺特效一起收，含排隊未冒的）；召喚型 boss 的召喚分身（`MonsterWeaponUser.RecallSummons()`，通用，紅嫁衣家人幽靈適用）——boss 死了招式不再傷人。④ 地刺受傷範圍改 base-anchored 貼齊可見圖（見 [PROBLEMS.md](PROBLEMS.md) F14）、階段切換血量資料化（`P2_HpEnter`/`P3_HpEnter`）、大地刺縮到 80%(2.24)、狂亂 20 根、附除錯紅框 `DebugDrawHitbox`。純新增/資料，打包版正常。

* [x] 修「怪物原地踏步」＋召喚施法動作復原（2026-07-13，見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)、[PROBLEMS.md](PROBLEMS.md) F15/F16）：紅嫁衣等 boss 在玩家沒靠近時「一直播走路動畫卻沒真的移動」（原地踏步）。**① 走路/發呆改看『實際位移』**：`MonsterController.HandleVisuals` 原本用指令速度 `_rb.velocity.magnitude` 判斷是否在動——但所有怪的碰撞框都是 trigger（走 A* 導航、不做硬碰撞），逃跑被卡在牆角/A* 目標點不可達而原地微調時，velocity 仍被每幀設成滿的 `MoveSpeed`、實際位置卻幾乎沒變 → 誤播走路。改成每幀量 `transform.position` 的實際位移速度（加指數平滑 tau 0.08 吃單幀抖動；玩家/怪物 Rigidbody2D 已開 Interpolate 故量測穩定），超過 `MoveAnimThreshold`（新增欄位，預設 0.12 世界單位/秒）才走路、否則發呆；此速度也餵給 `MonsterAnimator.SetState` 讓走路 fps 跟真實移動連動。通用修正、對所有怪生效。**② 召喚施法動作復原**：改①後發現紅嫁衣召喚時的「施法動作」其實一直是**走路動畫在頂替**——她的 `attack` 幀從沒同步進 StreamingAssets（`Has(Attack)=false`），施法時的 Attack 請求被 `Has` 擋掉、掉回走路，而她那時剛好被卡在角落播走路；改①讓她原地變 idle 後施法動作就消失了。修法：**施法視窗（`NotifySkillCast` 的 `SkillCastAnimSeconds` 0.6s）內若沒有 attack 幀，退回播走路當出手表演**（只在該 0.6s、平常靜止仍 idle，不回到原地踏步），且原地施法時用 `MoveSpeed` 餵走路 fps 讓節奏正常。之後只要把紅嫁衣的 `attack` 幀 `Project Tools → Sync Map Assets` 進 StreamingAssets，就會自動改播真正的攻擊動畫（程式已接好、零改動）。純程式修正，打包版正常。

* [x] 地上物「出現條件（完成 N 關後才出現）」＋地上物多選/框選（2026-07-13，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md) §3.1/§4.3/§5、[SAVE_SYSTEM.md](SAVE_SYSTEM.md) §14）：兩個地圖編輯器功能。**① 出現條件（進度門檻）**：地上物加兩個每實例欄位 `appearAfterClears`（完成 N 關後才出現，0＝一開始就有）＋ `appearScope`（`cycle`＝每周目重算〔本周目完成數〕／`lifetime`＝曾達到過就永久），編輯器選取面板可填、複製會帶。遊戲端 `MapLoader.BuildOneObject` **進圖當下**判定：cycle 看 `SaveManager.ClearedModuleCount`、lifetime 看新增的跨輪迴高水位 `LifetimeMaxClears`；未達則整個不生（連碰撞都沒有）。沒 SaveManager（編輯器直測）一律照常出現。存檔加頂層 `CharacterSave.lifetimeMaxClears`（同 `lifetimeFlags` 放頂層＝輪迴不重置，`MarkModuleCleared` 更新、載檔取 max 補冷啟動）。兩邊 `ObjectInstance`（編輯器 `LayerData.cs`／遊戲 `MapModel.cs`）同步加欄，舊地圖缺欄＝0/cycle 向下相容。**② 地上物多選＋框選**（`ObjectController` 單選改一組 `_selection`）：**Cmd＋點**＝加選/取消、**空白處左鍵拖方框**＝框選（碰到就選）、單純左鍵點一個＝只選它、點空白＝清空；**複製**＝每個各複製一份、選取換成新複本（可接著一起搬）；**Ctrl＋拖**＝整組一起移動（多選關磁吸避免亂跳、單選保留磁吸）。選取疊加 `ObjectSelectionOverlay` 畫全部選取外框＋黃色框選矩形；選取面板 `EditorUI` 多選顯示精簡版（已選 N 個＋複製/刪除/取消）、單選維持完整面板。整組移動/複製/刪除各一步 Undo。**純編輯器＋存檔欄位，不影響現有打包遊戲行為。**

* [x] 特效庫武器化定案（2026-07-13，見 [EFFECT_WEAPONS.md](EFFECT_WEAPONS.md)）：審閱 398 套動畫／2,421 組變體，最終保留血月鬼爪、虛空吞口、九霄雷獄、幽影突、冰封法陣、死字咒六把；新增泛用 `IsMelee`、`IsGroundCast`、`IsDash`、`UseSegmentedSkyStrike`。九霄雷獄以 start 雷首＋動態 N 節等寬 tileable loop 從鏡頭外鋪到落點，接大型環形爆炸。未入選測試武器及其專屬素材／表列已清除，舊落雷武器 ID 10 亦已移除；`IsSkyStrike` 底層由九霄雷獄繼續使用。Unity 2022.3.62f3 驗證副本乾淨匯入與編譯通過。

* [x] 武器集氣模式（2026-07-13，見 [CHARGE_MODE.md](CHARGE_MODE.md)）：RecipeTable 新增預設 false 的 `集氣模式` 與百分比 `集氣時間縮減`；空白鍵／滑鼠左鍵按住集氣、放開施放，完成後傷害 ×3、視覺 ×2。匯入 `scifi_charge_up_003` 藍／紅各 16 幀作集氣中／完成提示，特效高度依角色當下實際高度調整為 1.15 倍；雷射、佛光等持續輸入武器在載入時強制互斥。換地圖與暫停／阻擋操作 UI 會凍結並保留集氣，恢復後若按鍵仍按住便延續，遭清場的光圈會自動重建。Unity 2022.3.62f3 乾淨匯入與編譯通過。

* [x] Pack 4 像素反射雷射（2026-07-13，見 [PIXEL_REFLECT_LASER.md](PIXEL_REFLECT_LASER.md)）：研究 A／B 兩套 origin／center／impact 與六色變體，選用 A 組藍色 loop 製作武器 29「鏡界折光」。新增 `PixelBeamSet` 與 `PixelLaserBeamVisual`，將中心段沿 ray-march 反射折線平鋪、在轉折補撞擊火花；Recipe 42 用 `BeamRange=-1` 延伸射程，反射則完全服從 `BounceTarget=Environment, MaxBounces=3`。追蹤、散射、穿透與 DOT 均沿用既有雷射配方管線。

* [x] 底部 HUD 血球＋藥水系統（2026-07-16，見 [BOTTOM_HUD.md](BOTTOM_HUD.md)、[INVENTORY.md](INVENTORY.md)）：底部操控列以自繪著色器畫 HP/MP 液體血球（阻尼彈簧搖晃、暗場景調色、去描邊）取代舊左上血條。新增**藥水系統**——藥劑分類（`201` 小回血瓶／`202` 小回魔瓶，`HealHp/HealMp`、`MaxStack=99`），背包兩格藥水格綁定「種類」、按 1/2 喝並播喝藥特效，底部 HUD 兩格**鏡像顯示**（訂閱 `OnChanged` 即時同步）。互動加：拖曳可放欄位黃色高亮、丟錯格自動歸位、右鍵藥水快放。修正背包版面座標到真正背景 `1126×1397`（原本用到舊快取 1133×1388 導致高亮偏位），並修液體球著色器 `ZTest Always → [unity_GUIZTestMode]`（原本會穿透蓋住背包／黑幕）。

* [x] 傳送點外型「精準視覺錨點」＋編輯器點放預覽（2026-07-18，見 [MAP_SYSTEM.md](MAP_SYSTEM.md)、[MapEditor_DESIGN.md](MapEditor_DESIGN.md)）：傳送點外型特效原本放在 trigger 格子的「幾何平均中心」(`RegionCenter`)，而格子以整格塗、平均常落在半格上，跟獨立精準擺放的門美術對不準。**解法**：傳送點 `Params` 可存精準世界座標 `markerX/markerY`（有→外型放那、無→退回格子中心，向下相容）。主遊戲 `MapLoader.BuildTeleportMarkers` 讀它。**編輯器**：Trigger 面板加「── 外型位置 ──」（`EditorUI.DrawTeleportMarkerAnchor`）＝「設定外型位置」(按下後 `TriggerController` 下一次點畫布設 markerX/markerY，仿 SceneFx 點放)＋「回到中心」；`TriggerOverlay` 對選取的傳送點畫**黃十字**＝外型實際落點（所見即所得，對齊門免進遊戲試）。只動視覺、踩踏功能格子與玩家落點不變。純新增/向下相容，兩專案都改、需各自重編譯。

* [x] 載入頁進度條改用美術素材（2026-07-18，見 [RESOURCE_LOADING.md](RESOURCE_LOADING.md)）：`LoadingPanel` 底部進度條由簡易純色條換成美術素材（`Resources/UI/LoadingBarPanel/`：底框蓮花吊飾＋深色軌道、金色填充、金色端蓋）。**① 兩張素材沒畫在對齊位置**（底框軌道 y[52,120]、金色填充 y[78,180]）＝1:1 疊會太粗又掉出框；改成把填充**垂直壓成軌道高(×0.667)＋頂端對齊**才落進軌道。**② 進度用 RectMask2D 遮罩裁切金填充**：可見右緣＝遮罩寬度、金色端蓋放在同一個寬度上 → 端蓋一定黏在金條尾巴（別用 Image.Filled，裁切邊會跟算出的端蓋對不上）。**③ 文字**「載入中…XX%」用預設字型＋燙金漸層（新增 `UIVerticalGradient` BaseMeshEffect）＋暗描邊，錨在畫面底部中央。版面常數（BarWidth/BarBottomMargin/PercentGap）可調。`SetModule`/`SetProgress` 介面沿用、MapManager 不用改。純改動，打包版正常。

* [x] 地上物新增「可穿越(passThrough)」＝無碰撞但照常 Y-sort（2026-07-18，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)）：榕樹妖結局的鬼魂地上物彼此遮蔽亂掉（小孩頭被大人身體蓋）。**根因**：鬼魂全被勾「可走(walkable)」→ 拿固定低排序(5)、**完全沒進 Y-sort**（彼此用建立順序疊、也被玩家蓋）；不是排序鍵(中心 vs 腳底)問題（實測相同）。walkable 原是給「木板/地毯」這種踩得上去、畫在角色腳下的地板物，把「無碰撞」和「固定低排序」綁在一起，缺「無碰撞＋照 Y-sort」的組合。**解法**：`ObjectInstance` 新增 `passThrough`（兩專案 `MapModel.cs`／`LayerData.cs`）＝不生碰撞框、不掛可破壞，但排序照一般地上物走 `MapDepthSort`（依 Y 和角色/彼此正確交錯）。`MapLoader` 碰撞/可破壞條件加 `&& !inst.passThrough`（排序不動，walkable=false 自然走 Y-sort）。編輯器 `EditorUI` 物件面板加「可穿越」勾選＋清乾淨「可走」標籤、兩勾互斥、breakFlag 條件排除。站立的鬼魂/煙/光用「可穿越」，地板類仍用「可走」。純新增/向下相容，打包版正常。

* [x] 榕樹妖地刺：生成安全內縮＋橫掃 5→3 排＋大地刺補可走驗證（2026-07-18，見 [BOSS_MODULE.md](BOSS_MODULE.md) §6.4/§6.8）：場地縮小（加了底部 HUD、可走範圍變小）後地刺會冒在貼著 HUD／左右血魔球的邊緣。**① 生成安全內縮 `SpawnBounds`**＝可走區(`MapNavGrid.WalkableBounds`)再往內縮（`SpawnEdgeInset`0.5 上/左/右、`SpawnBottomInset`1.0 底部 HUD 側），隨機灑/橫掃/大地刺全走它 → 地刺不再貼 HUD 冒。**② 橫掃 `Sweep_TotalRows` 5→3**（仍每次隨機挑 2 排），縮小場地下三排都落在可走內、更分明。**③ `GiantSpike`（大地刺）補遵循可走**：原本用相機中心不檢查可走，改成夾進內縮框＋`IsWalkableWorld` 驗證。查因：`RandomVolley`/`RowSweep` 本來就有透過 nav grid 檢查、對不準的其實是可走區塗到貼 HUD＋GiantSpike 沒檢查。常數都在 `BanyanTreeBrain.cs`。純程式/常數，打包版正常。

* [x] **關卡儲存機制：關卡進度與臨時包 `RunProgress`**（2026-07-18，見 [RUN_PROGRESS.md](RUN_PROGRESS.md)）：**[MAP_SYSTEM.md](MAP_SYSTEM.md) 講的「Phase 2 地圖狀態持久化」落地**，但落點與草案不同——不是掛在 `MapManager` 上的 `MapState`，而是獨立的常駐單例 `RunProgress`，把「一趟關卡」的臨時包與地圖狀態收在一起。**① 四種進度記錄（per-map、限本趟）**：`killedSpawns` 已清出生點（key＝區域 id + 格座標）／`consumedTriggers` 已取或已觸發（key＝trigger 區域 id）／`destroyedObjects` 已破壞地上物（key＝`obj#清單索引`，解掉「地上物沒有穩定 ID」這個 Phase 2 前置）／`drops` 地上未撿掉落物（原座標重放、支援部分撿取回寫）。載圖時由 `MapLoader` 跳過重生、`MapManager` 呼叫 `InteractionManager.RestoreGroundDrops` 重放。**② 臨時包**：關卡內取得的道具/金錢（金錢＝銅錢道具 101）先進臨時包，過關 `SettleIntoBag()` 併入真背包並把快照交給 `ResultPanel` 顯示獎勵，死亡/返回 `EndRunDiscard()` 整包丟棄——[CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) §6 的打寶模型到位。**③ 生命週期看 module**：`Main`（廣場/教學）不算關卡、東西直接進真背包；進不同關卡 module 才 `BeginRun` 重置，**同 module 房間互跳完全延續**。**④ 統一入口** `RunProgress.GiveItem(itemId,count)`（`giveItem` trigger／拾取點／地上掉落物都走它）。**⑤ 暫定掉寶**：地圖出生的敵怪死亡必掉銅錢 1~5、35% 機率掉一瓶藥（201/202），召喚物不掉不記（防無限刷）。**⑥ 工程介面**：按 **F8** 開除錯疊層看臨時包內容。純記憶體、不寫存檔（沒過關的收穫本來就歸零）。

* [x] **劇情演出編輯器（Cutscene）**（2026-07-20，見 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md)）：在地圖編輯器裡排一段「半演出半漫畫」的**地圖內過場**——演員自己走位、說話、運鏡、插入置中漫畫格、淡黑、播螢幕特效，最後交棒到下一張圖或墜落動畫，全部資料驅動存在 `.dipanmap` 的 `cutscene` 欄。**編輯器端**：新 `EditTool.Cutscene`「劇情」分頁（演員／步驟增刪排序、點畫布放座標）、`CutsceneController`、GL 疊層 `CutsceneOverlay`（演員起點方框＋朝向線＋走位折線＋步驟紫十字）、**編輯器內預覽 `CutscenePreview`**（移植版 A* 走位，`dialogue`/`camera`/`screenFx` 用等秒數佔位）。**遊戲端**：`CutsceneDirector` 協程排程器＋`CutsceneActor`（npc 走路線 B 逐格動畫；`player` 直接接管場上玩家、停用 `PlayerController` 改掛臨時 `MonsterActuator`，結束一定還原）。13 種步驟型別 `move/face/dialogue/wait/camera/cameraFollow/comic/fade/spawn/despawn/screenFx/setFlag/end`，兩種並行開關 `parallelNext`（同時開始、整組做完才往下）與 `background`（丟背景、主線立刻往下）。`MapManager` 載圖後 `MaybeAutoStart`，且**進場觸發 `onEnter` 會等演出演完才點火**（避免對話互相蓋掉）。目前用在開場山道 `Main_InitialForest1/2`（13/14，尾段 `end=fall` 接墜落）與初始洞窟 `Main_Cave`(11)。⚠️ 與 `cutscene` **trigger**（穿隧道播影片，見 [CUTSCENE_TUNNEL.md](CUTSCENE_TUNNEL.md)）名字撞了但是兩套東西。

* [x] 作弊面板（2026-07-23，測試工具，尚無專屬文件）：`CheatPanel` + `CheatLauncher`，預設按 **L** 開/關。左側分頁導覽＋右側內容區的可擴充版面，目前只有「給道具」分頁（填物品 ID＋數量 → 直接 `InventorySystem.AddItem` 進**真背包**，不走臨時包）。開啟時暫停遊戲＋擋輸入（方便打字），ESC／右上 X 關閉。全程式建構、零 prefab（同 `SettingsPanel`／`InventoryPanel` 風格），程式註解內附「如何新增一個作弊分頁」。純新增測試工具，不影響正式流程。

* [x] 技術債清理：陣列型 static 快取修正＋素材同步白名單收斂＋CSV 工具（2026-07-27，見 [PROBLEMS.md](PROBLEMS.md) I8/C8）：從 PROBLEMS.md 盤點出的隱患，一次處理掉四項。**① 修一顆會發作的 bug**：`SegmentedLightningColumn`（九霄雷獄落雷柱）的 `Sprite[]` static 快取只判 `arr == null`，但**陣列本身永遠不會變 null**（被銷毀的是元素）→ 關掉 Domain Reload 後第二次 Play 拿到一整包已銷毀的 Sprite、雷柱不見。改用 `IsStale()`（判 null／長度 0／首元素已銷毀）。順帶修掉同一支的既有隱患——`Load()` 永遠回傳非 null 陣列，導致「素材未完整載入」的守衛從來攔不到、素材真缺時直接 NullReference，守衛也改用 `IsStale`。並在 `PlayModeStaticReset.cs` 檔頭寫入通則：**陣列／集合型的 UnityEngine.Object 快取不適用 `== null` 自動重建**。**② 素材同步白名單收斂**：「哪些分類要同步」原本在遊戲端寫三次（`MapIO`／`MapAssetSyncTool`／`sync_map_assets.sh`），新增 `Assets/Scripts/Map/MapAssetCategories.cs` 當單一來源（`All` ＋ `IsRecursive()`），兩支 C# 改為引用，改分類從 3 處變 2 處（shell 版仍獨立，該行上方加了提醒註解）。連帶把「動畫地上物」的觸發開關 `cat == "Environment"` 也換成常數——留字面值的話將來改名會讓動畫地上物**整批靜默消失**。編輯器端 `AssetSyncTool` 加註解標明「只有三類是刻意的，別順手補 Drama/Talk」。**③ Sync 摘要**：`Project Tools → Sync Map Assets` 跑完印出各分類收了幾筆、其中幾筆多幀動畫、各 module 幾筆，某分類掛零跳 Warning——讓 F16 那種靜默漏檔一眼可見。**④ 新增 `Assets/Scripts/Data/CsvUtil.cs`**（純新增、零呼叫）：把 `ItemDatabase` 已驗證的引號解析抽成共用 `SplitLine()`＋`Field/FieldInt/FieldFloat/FieldBool` 防呆取值，**既有 13 處 CSV 解析一行未改**（避免無法編譯驗證的大規模重構），之後遇到「這欄需要能打逗號」時再逐張遷移。已實機驗證：Console 無錯、Sync 摘要正常、動畫地上物/立繪/劇情圖正常、落雷柱連續兩次 Play 皆正常。

* [x] **沒裝備武器就不能攻擊＋移除 E 鍵切換**（2026-07-27，見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)、[RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)）：**取代本檔上方 2026-06 那筆「初始武器＝最高 ID／E 鍵往前切」的舊行為。** 原本玩家空手也能攻擊，根因有兩個：① `WeaponManager.Start()` 強制把 `CurrentWeaponID` 設成武器表最高 ID（專案最早期為了方便測試）；② `PlayerController.OnInventoryChanged` 裡寫著「卸下時保留當前武器」。**改法**：`CurrentWeaponID` 預設 `0`＝無武器（連 `MainScene` 序列化的值也一併改成 0，否則場景值會蓋掉程式預設）、`Start()` 不再指派、`GetWeapon(id<=0)` 安靜回 null（無武器是正常狀態不是錯誤）、卸下改為 `SwitchWeapon(0)` 並立刻清掉光束/佛光/集氣。**關鍵是 `HandleFiring()` 開頭一道 `weapon == null` 的 guard**——放在所有分支之前，一處就擋掉雷射／佛光／集氣／離散全部發射路徑，不必逐條改：不發射、不扣魔（耗魔全在 `Shoot`/`UpdateLaser`/`UpdateAura` 內）、不擺攻擊動作（離散靠 `_attackAnimUntil`、持續型靠 `HandleVisuals` 的 `_activeBeams`/`_activeAura`，前者走不到、後者已清空），連「按左鍵轉身面向滑鼠」也一併擋掉。刻意**不跳提示** toast——需求是完全沒反應，且開場劇情到柴房撿佛燈前玩家本來就空手，提示會一路蹦在那段刻意乾淨的畫面上。**同時移除 E 鍵循環切換**（`SwitchToPreviousWeapon`），武器一律由背包武器欄決定，不再有繞過裝備的途徑。**順手修掉一個既有競爭條件**：舊 `Start()` 的覆寫若晚於 `PlayerController.Start`，會把背包指定的武器蓋掉。已確認柴房佛燈教學不受影響（phase 順序是撿→**裝備**→點亮，`FireOnly` 期間必定已裝備佛燈）、讀檔還原正確（`RestoreState` 結尾 `Raise()` ＋ `PlayerController` 訂閱後的初始同步兩條路都會走到 `OnInventoryChanged`）。

* [x] **地圖級「禁用武器」開關（MapsTable 新增 `NoWeapon` 欄）**（2026-07-27，見 [MAP_SYSTEM.md](MAP_SYSTEM.md)）：劇情用地圖與邪佛廣場不該讓玩家開火（玩家理論上還沒武器，但不能排除用 bug 取得；廣場亂放武器畫面也很怪）。**做成資料驅動而非寫死地圖 id**，之後任何特殊劇情地圖都能直接用。`MapsTable.csv` 加**第 10 欄 `NoWeapon`**（`0` 可用 / `1` 禁用 / **空＝0**，沿用 `SceneEffect`／`EnterEffect` 的「空＝無此特性」慣例，現有 10 列紅嫁衣地圖一個字都不用改、維持 9 欄）。目前填 1 的是 `Main_Cave`(11)、`Main_Square`(12)、`Main_InitialForest1`(13)、`Main_InitialForest2`(14)。**實作只動三處**：`MapTable` 加欄位與第 10 欄解析（缺欄/留空/解析失敗都退回 0）→ `MapManager.PlaceAndSetup` 記進 `WeaponDisabled`（與 `Atmosphere`／`SceneEffect` 同一段、同一個模式）→ `PlayerController` 新增 `CanFire` 屬性（「有裝備武器 ＋ 這張圖沒禁用」的單一判斷），把前一筆剛加的 `weapon == null` guard 換成 `!CanFire`，**其餘一行都不用動**——雷射／佛光／集氣／離散、攻擊動畫、MP 全部自動涵蓋；「按攻擊鍵轉身面向滑鼠」也共用同一個 `CanFire`，確保兩處判斷永遠一致。**只擋玩家發射**：移動、互動按 F、背包、喝藥一律正常，`MonsterWeaponUser`（怪物用武器）走獨立管線不受影響。**確認過三個潛在副作用都不存在**：① 提燈光圈靠「裝備的 `LightRadius`」不靠佛光武器，且這四張圖 Atmosphere 是 1/5/1/1、本來就不打光；② 編輯器專案不讀 `MapsTable.csv`（全專案只有一份、只在遊戲端），不需雙專案同步；③ 怪物武器管線獨立。⚠️ **別把 `NoWeapon=1` 填在新手教學地圖**——柴房佛燈教學要玩家實際開火點亮佛燈，禁用會卡死。順帶補齊 [MAP_SYSTEM.md](MAP_SYSTEM.md) 欄位表裡一直沒補的 `SceneEffect`／`EnterEffect` 兩列，並把 `Atmosphere` 範圍由 1~11 更正為 1~15。

* [x] **對話防連點**（2026-07-27，見 [DRAMA.md](DRAMA.md)「防連點」、[UI_SYSTEM.md](UI_SYSTEM.md)）：劇情對話時猛按左鍵／空白鍵會一次跳掉好幾句，有時**立繪都還沒顯示出來就被跳過**。**作法**：把「前進／關閉一次」節流成每 **0.5 秒**最多一次，**並且面板剛開啟時也先擋一次冷卻**——後者才是「立繪來不及出現」的真正解方（否則上一句的連點慣性會直接吃掉新開的那一句）。**工具做在 `UIPanel` 基底**（純新增，不改任何既有面板行為）：`InputCooldown`(0.5) 常數＋`BlockInputFor(seconds)`（`OnOpen` 呼叫）＋`TryConsumeInput(cooldown)`（入口呼叫，冷卻中回 false 就 return）。**刻意做成 opt-in**：基底不自動套用，否則背包／設定那種要連續操作的面板會變鈍。一律用 `Time.unscaledTime`——對話面板 `PausesGame=true`，用 `Time.time` 會永遠卡在冷卻裡。**套用兩處**：`TalkPanel` 放在 `Next()` 內一處（鍵盤與整片點擊鈕都經過它，兩個入口一次涵蓋）、`DramaPanel` 放在整片關閉鈕的 callback。**ESC 不受節流**（走 `UIManager.CloseOnEscape`，那是明確的「我要跳過」意圖）。序章開場漫畫的翻頁是獨立實作、不走 `UIPanel`，維持原樣。要調節奏改 `UIPanel.InputCooldown` 或個別面板傳自訂秒數。

* [x] **劇情跳過改成「只有開發階段能用」＋修掉「按 ESC 莫名播爬起動畫」**（2026-07-27，見 [DRAMA.md](DRAMA.md)、[CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md)）：作者從未要求過「ESC 可跳過整段劇情」，正式版也不希望玩家這樣跳掉所有劇情。**兩個回報其實是同一個根因**——ESC 同一下會被兩處讀到：① `UIManager` 因 `TalkPanel/DramaPanel.CloseOnEscape=true` 關掉對話；② `CutsceneDirector` 因 `skippable=true` 設 `_skip`，**中止剩餘步驟後仍會跳到最後的 `end` 執行交棒**。所以在 `Main_InitialForest2`（`end='fall'`）按 ESC ＝ 直接跳完整段開場 → 墜落 → 回 `MainScene` 進初始洞窟(11) → 洞窟 `EnterEffect=1` 觸發「趴地→爬起」，看起來像「按 ESC 就莫名播爬起動畫」，其實是一路交棒過去的正常結果（點左鍵只結束當前那句、後面步驟照演，所以不會發生）。**解法**：新增 `Assets/Scripts/DevSkip.cs`（`Allowed => Application.isEditor || Debug.isDebugBuild`），套用在 `CutsceneDirector` 的 ESC 略過與 `TalkPanel`／`DramaPanel` 的 `CloseOnEscape`。**沿用專案既有慣例**——`IntroComicController`／`IntroFallController` 本來就各自有同義的 `AllowSkip`，只是沒套到對話與劇情演出上；用執行期判斷而非 `#if UNITY_EDITOR`，讓 Development Build 仍可跳過、方便測後段流程。**確認打包後不會卡死**：對話播完 `ShowCurrent()` 會自動關閉、`DramaPanel` 整片點擊鈕照常可用、`CutsceneDirector` 的 `_skip` 恆 false 時步驟正常跑完；ESC 失效也不會誤開設定面板（`UIManager` 的 ESC 是「有視窗且允許才關」，不會 fall through）。其他面板（背包/設定/確認彈窗/存讀檔）的 ESC 一律不受影響。**仍待決定**：`VideoPlayerOverlay` 的 `AllowSkip` 是 Inspector 上的 public bool、目前打包後玩家可跳過過場影片，是唯一沒有開發/正式區分的跳過入口。

* [x] 清掉開場改用劇情編輯器後不再使用的漫畫素材（2026-07-27）：開場表演從「三張整頁漫畫＋鏡頭導讀」改成劇情演出編輯器之後，`Resources/InitialStory/` 裡只有 **`Page_01` / `Page_02` / `Page_03`** 真的沒人用了（連同 `.DS_Store` 一起刪，共約 7.6MB）。判定依據四層都查過：① 程式裡只出現在 `IntroComicController.BuildDefaultPages()` 的**預設排版**，而該預設只在 `Pages` 為空時才用，`Intro` 場景已有序列化清單所以永遠走不到；② 場景裡那三頁的 `Fullscreen` 都是 0；③ Intro 場景現在**唯一**進入點是 `CutsceneDirector` 的 `end='fall'`，此時 `FallTailOnly=true` → `Pages.FindAll(pg => pg.Fullscreen)` 把三張全濾掉；④ GUID 零引用、StreamingAssets 無副本、CSV 無引用。**其餘 14 張全部還在用，不能刪**：`SeeButterFly`/`Dangerous`/`Notice`/`HoldHand`/`Thanks`/`Rest`/`Break` 七張是**新劇情演出**（`Main_InitialForest2` 的 `comic` 步驟）在用；`Story_13~15`（Fullscreen=1，墜落尾段會播）、`Story_ActorFall_Front`/`_Side`、`Story_RockWall` 是墜落動畫在用；**`Manji` 最不能刪**——除了開場，`LevelExitManjiController` 每次過關／死亡的卍字離場特效都會載它。

* [x] **測試工具：直接進關卡加「邪佛廣場-1關後」＋作弊面板加「給 10000 元」**（2026-07-28）：抽選祭壇要求至少通過一關，但 `Project Tools/測試/直接進關卡` 原本只有一個「邪佛廣場」＝全新存檔、`clearedModules` 是空的，測不到祭壇。改成兩個入口——**邪佛廣場-初始**（原本的）與**邪佛廣場-1關後**（`DevQuickStart.PreClearedModule="RedBridalGown"`，開場預先塞一筆完成紀錄）。同一機制順便讓 `TutorialManager` 在 `ClearedModuleCount > 0` 時不再啟動新手教學。作弊面板加「獲得 10000 元」按鈕（免得每次打 101 + 10000），並把面板依功能用**分隔線＋不同色塊底版**分組（第一列＝道具 ID／數量／確認給予，第二列＝給錢）。

* [x] **觸發鏈新增 `openPanel`／`unlockRoll` ＋「最低/最高完成關卡數」條件 ＋「條件不成立時」分支**（2026-07-28，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md)、[GACHA_SYSTEM.md](GACHA_SYSTEM.md) §6）：祭壇需要「地上物是圖、互動是觸發」兩件事分開——地圖上擺祭壇圖當地上物，另外畫一顆 `openPanel` 觸發（位置型、靠近按 F）填 `panelId=gacha`＋`poolId`。順手做了兩個通用條件 `requireClearsMin`／`requireClearsMax`（完成關卡數門檻，`requireClearsScope` 決定算本周目還是終身），因為祭壇設定「至少通過一關才開放」。**過程中抓到一個真的遊戲 bug**：邪佛初始對話的守門條件是 `requireCycleMax=1` ＋ `requireItem=!104`，打完紅嫁衣後劇本被消耗掉、周目還是 1 → 兩個條件同時又成立 → **初始對話與新手教學會重播**。加 `requireClearsMax=0` 可以擋住，但擋掉鏈中間那顆會把後面「給紅嫁衣劇本」一起吞掉 → 玩家永遠拿不到劇本、軟鎖，所以另加通用欄位 `onBlocked`（`中止整條鏈`(預設)／`跳過這顆繼續`）。編輯器端 `InteractionManager` 也順手把寫死的 `enum PointKind` 改成**可註冊的 `InteractKind` 表**，之後加互動型別不用再改三處 switch。

* [x] **抽選介面套上正式美術＋十連結算畫面**（2026-07-28，見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md) §7）：先以工程版面做出可玩流程，作者補上素材後整個換掉。素材放 `Resources/UI/GachaPanel/`（機台/標題/金錢條/中選框/啟動 icon）＋ `Resources/UI/Common/GachaPanel_StartBtn.png`。**素材是 AI 產的「整張畫布輸出」**（1536×1024 裡只有中間一塊有東西），所以程式端建了一張 `ArtSpec` 量測表（每張圖的畫布尺寸＋內容 alpha 邊框），`PlaceArt()` 反推 RectTransform 尺寸與偏移，讓「內容框」而不是「整張圖」對齊版面；載入時比對 sprite 實際尺寸，重出圖尺寸變了會**出警告**而不是默默跑位。版面**以機台圖為錨**（機台高 840，直欄視窗與格距都是機台圖的比例常數），所以之後換機台圖只要改一組數字。表演：中選框在轉動時上下抖動、減速停止、單抽中獎走**「舞台」特寫**（整個機台壓暗 80%、icon 放大到 186×180、下方描邊名稱）；十連抽完（含中途 skip）跳**結算面板**，用過關結算的 `ClearStagePanel_ItemBg.png` 當底，重複的合併顯示 ×N。抽選面板**刻意不能用 ESC 關**（`CloseOnEscape=false`），一定要按關閉鈕。

* [x] **血統做成一次性藥劑（`BloodlineID` 欄 + BloodlineSystem）**（2026-07-28，見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md) §5）：作者拍板「血統是消耗道具、喝下去主角**徹底改變外型**、**本世只能用一次**，輪迴後回到人類」。所以不新增裝備欄，只在 `ItemTable.csv` 加一欄 `BloodlineID`（>0＝這是血統藥劑），另開 `BloodlineTable.csv` 放血統本身的資料（`Key/DisplayName/SpriteFolder/MaxHpAdd/MoveSpeedMul/OutgoingDamageBonusPercent/SkillId`）——兩張表分開是因為「藥劑」與「血統」生命週期不同：藥劑會被喝掉，血統要跟著角色一路到輪迴。「本世已定型」用**周目範圍**的旗標 `血統` 記，輪迴自動清空 → 回人類。`BloodlineSystem` 每幀比對「存檔上的血統」與「已套用的血統」，不一致就換外型並補差值屬性。新增藥劑 301（野魂）／302（幽靈）。

* [x] **金錢不再是背包道具，改成獨立數字**（2026-07-28，見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md) §4、[INVENTORY.md](INVENTORY.md)）：抽選要花錢，銅錢（道具 101）若還是可疊道具，背包很快被錢塞滿、而且「有幾格 99 顆」根本無法當貨幣算。改成**存檔裡的一個整數**（`SaveManager` 既有的 currency），背包面板底部銅錢 icon 後面直接顯示總額（`OnCurrencyChanged` 事件即時刷新）。**掉落端不動**——怪物/寶箱照樣「掉道具 101」，由 `RunProgress.GiveItem` 與結算落袋 `SettleIntoBag` 在入口處轉呼叫 `SaveManager.AddCurrency`，所以既有的掉落表、觸發 `giveItem(101)` 全部不用改。另加安全網 `SweepMoneyIntoWallet()`（`ApplyToSystems` 時掃背包，把舊存檔裡殘留的 101 收進錢包），`ItemTable` 的 101 那一列**要保留**（toast 名稱與 icon 還靠它）。`StorageLauncher` 不再開場塞 500 銅錢。

* [x] **祭壇抽選系統（GACHA）**（2026-07-28，見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md)）：核心迴圈的「取得」那一半——玩家在邪佛廣場走到祭壇按 F 開抽選面板，單抽／十連，老虎機直欄捲動 → 減速 → 中選格定住 → 中獎特寫。**整套設計成「大項可以隨時增刪」**：作者明說武器之後可能併進裝備、也可能再開新類，所以刻意**不做成 trigger 型別**（`triggerTypes.json` 是 merge-only，加了拿不掉），改成四層——① `GachaPoolTable.csv` 是大項登記表（`PoolId,DisplayName,BaseTable,SlateSprite,CostSingle,CostMulti,MultiCount,CostItemId`），刪一列＝這個大項消失；② 每個大項一張 `BaseXxxRoll.csv` 基礎表（`ItemId,Weight,MinCycle,RequireFlag`）；③ 存檔的 `unlockedRollEntries` 記「打關解鎖了什麼」；④ 祭壇本身只是地圖上的 `openPanel` 觸發填 `panelId=gacha`＋`poolId=weapon`。**實際候選＝基礎表 ∪ 已解鎖**，所以「打贏紅嫁衣 → 幽靈血統進池」「打贏榕樹妖 → 地刺戢進池」只要在 boss 死亡鏈上接一顆 `unlockRoll(poolId, itemId)`，零程式。新程式集中在 `Assets/Scripts/Gacha/`（`GachaPoolTable`／`GachaRollTable`／`GachaTableProvider`／`BloodlineTable`／`BloodlineSystem`／`GachaService`）＋ `UI/Panels/GachaPanel.cs`。

* [x] **鍛造介面（ForgingPanel，Y 鍵開啟）**（2026-07-29，見 [FORGING.md](FORGING.md)）：鐵匠鋪的鍛造台：中央鐵砧一格放武器／裝備、左右各三個鑲嵌孔放寶石。**這一版做到「版面＋拖放」為止**——兩顆按鈕（移除鑲嵌／拆除裝備）與鑲嵌本身的功能都還沒接。**整套重用既有的拖放地基、沒有另寫一份搬運邏輯**：`ForgeSlotWidget` 實作 `ISlotView` → `SlotDragController` → `InventoryActions.Resolve`，所以背包↔鍛造台的拖放天生互通（同背包↔倉庫、背包↔傳送門）。資料層兩個容器：`ForgeSlotGrid`（容量 1，只收 `IsEquippable`）與 `ForgeSocketGrid`（容量 6，帶 `UnlockedCount` 解鎖數）；**「只收武器／裝備」的把關刻意放在 UI 端的 `OnDrop`**——跨容器拖放走 `SetAt`（先塞目標再清來源），若在 `SetAt` 拒收會讓來源被清空造成物品消失（同 `ScriptSlotGrid` 的註解）。**鑲嵌孔的解鎖鏈路已經打通、只差資料**：孔位數只從 `ForgeSockets.Of(ItemData)` 這一個 seam 查，現在固定回 0 ＝ 六孔全鎖（蓋鎖鏈圖）；將來 `ItemTable` 加 `SocketCount` 欄位後只要改那個函式，面板端一行都不用動（孔位變少時 `ReturnClosingSockets` 會把東西退回背包）。開啟時**強制把背包一起開**並排（鍛造靠左、背包靠右），關閉時把台上與孔上的東西全退回背包再關背包——與傳送門 `ScriptsPanel` 同源；玩家單獨關掉背包時鍛造也會跟著收。版面沿用 `InventoryPanel` 的「一張底圖 ＋ 疊格子、座標在底圖原生像素空間」作法（底圖 1536×1024、`displayHeight` 636），方框／鎖鏈／按鈕／關閉鈕則沿用 `GachaPanel` 的 `ArtSpec` 透明邊補償（重出圖尺寸變了會出警告、不會靜默跑位）。熱鍵 **Y** 掛在 `StorageBagCoordinator`（教學強制階段會鎖），**之後要改成鐵匠 NPC 的 `openPanel` 互動點**。字串進 `LanguageTable.csv` 的 4001–4099 段。⚠️ `ForgingPanel_Btn.png` 目前沒去背（整張不透明、四周深灰 40,40,40），按鈕會露出灰底方塊，等重出透明版即可，程式不用改。

* [x] **存讀檔畫面換上正式素材（SaveSlotPanel）**（2026-08-01，見 [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md) §4.5）：三欄存讀檔從工程版的純色方塊換成美術版面。**沿用專案既有的兩套地基、沒有新發明**：版面是「一張滿版底圖（`SelectSavePanel_Bg`，1672×941）＋座標寫在底圖原生像素空間、整個 frame 等比放大蓋滿畫面」（同 `InventoryPanel`／`ForgingPanel`），透明留白補償用 `ArtSpec` + `PlaceArt()`（同 `GachaPanel`，重出圖畫布比例變了會出警告而不是靜默跑位）。卡片外框 `SelectSavePanel_Frame` **本身就含頂端「欄位」紅底牌與背後的圓形佛像浮雕**，程式只負責疊字與互動元件。**有存檔時**左半邊放圓台 `SelectSavePanel_ActorBase` → 角色圖（曾試著在角色後面鋪方形底板 `SelectSavePanel_ActorBg`，實機看跟卡片框自帶的圓形浮雕打架、視覺上是歪的，當天就拿掉了），右半邊放「一周目」（`CjkNumber()` 轉中文數字）與該角色**武器欄裝備中**的武器 icon（取 `ItemTable.IconPath`，即背包裡那張）。**角色圖來源**：讀該欄存檔的周目旗標 `血統` → `BloodlineTable.SpriteFolder` → `PlayerSpriteLibrary.GetFrames(<血統>,"idle")` 的第一幀，沒喝過血統藥劑就是 `Base`；並用 `TryGetVisibleBox()` 的**不透明像素邊界框**正規化，讓不同血統的留白差異不會使角色忽大忽小、腳一定踩在圓台上（同 `MonsterSetup`／`BossSpike` 用 base-anchored 貼齊框的思路，見 [PROBLEMS.md](PROBLEMS.md) F14）。**關鍵取捨：這個畫面不載入存檔**——為了拿外型與武器，用 `SaveSystem.LoadCharacter()` 直接從磁碟偷看一眼該角色的 `character.json`，不動 `SaveManager.Current`、不觸發 `ApplyToSystems`；真正的載入仍然是按「進入遊戲」時走 `GameFlowManager.ContinueGame(slot)`。**版面精簡**（依作者示意圖）：拿掉「覆蓋（新建）」與畫面底部的「返回」鈕（要重開先刪角色再新建、返回標題按 ESC），卡片也不再顯示「完成 N 關」與「上次遊玩時間」（資料還在 `CharacterProfile`，要顯示隨時能加回來）；「刪除角色」沿用既有的 `ConfirmPopup` 做二次確認。按鈕底板只有一張圖（沒有按下版）→ 用 `ColorTint` 做回饋，同 `CloseBtn_2` 的處理。字串全走 `Language.GetText`，`LanguageTable.csv` 新增 **5001–5099「選擇存檔」段**，並在取字時對 `[cn:id]` 佔位做退回硬寫中文的保險（標題畫面比其他面板更早出現，provider 未就緒時不會變成一排編號）。

* [x] **「繼續遊戲」回到上次所在的地圖（schema v3）**（2026-08-01，見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)）：測試時發現「新建角色 → 看到開場第一句對話 → 關掉 → 重開該角色」會直接出現在邪佛廣場，**開場山道劇情、墜落、初始洞窟睜眼醒來三段全部被跳過**。查下去是這個功能從來沒做過——`ContinueGame` 的落點寫死 `GoToMap(HubMapId, "center")`，完全不看存檔；而按「新建遊戲」的當下 `CreateCharacter()` 就已經把角色寫進磁碟，所以那一欄早就有檔了。存檔裡跟「走到哪」有關的原本只有 `hubIntroSpawnDone` 一個布林，而且只用來決定廣場落點。**作法**：`ProgressDTO` 加 `lastMapId`/`lastEntrance`，由 `MapManager.PlaceAndSetup` 在 **Main module 的圖**（山道 13/14、洞窟 11、廣場 12）呼叫 `SaveManager.RecordLastLocation()` 並立刻 `SaveNow()`（這幾張一輪只經過一次，直接落地比較保險）。**關卡刻意不記**——關卡是 extraction 模型（`RunProgress` 純記憶體、離開歸零），記了會讓重開遊戲回到一個東西都不見的關卡裡；所以這兩欄永遠是「最後一次待在 Main 的位置」，在關卡中離開＝回到進關卡前的廣場，正好符合設計。`ContinueGame` 改讀它，`mapId <= 0`（舊存檔）退回廣場中央，`GoToHubRoutine` 泛化成 `GoToMapRoutine(mapId, entrance)`。schema v2→v3 是純新增欄位，`Migrate()` 不需要搬資料。

* [x] **能力珠鑲嵌系統：物品實例 ＋ 能力容器**（2026-08-03，見 [GEM_SOCKET.md](GEM_SOCKET.md)）：整個遊戲的戰力核心，開發前與作者來回討論了五輪才動工。**核心觀念是「CSV 表只是模板、玩家手上那一件另外存」**——同一把武器 ID 掉落兩次可能一把 2 孔、一把 5 孔而且開的位置還不一樣，這種「每一件都不同」的資訊在只有 `{物品ID, 數量}` 的資料結構裡無處可放。所以 `ItemStack` 加了一個 `ItemInstance`（等級／孔位／鑲的珠子），跟著背包格與裝備欄一起存檔；之後裝備要多屬性（附魔、耐久、詞綴…）就在 `ItemInstance` 加欄位，Newtonsoft 對缺欄給預設值 → 舊存檔不用寫遷移。**能力生效走「能力容器」而不是直接讀武器表**（作者一開始就規劃的方向）：換裝備或改鑲嵌時把「武器基底 + 所有裝備內建能力 + 所有珠子」累加成一份修正表，套到武器配方的**深拷貝**上。這一手同時解掉一個大地雷——`RecipeManager` 每個配方只 `new` 一次，同 RecipeID 的所有武器、**怪物**、以及把它當 `SubRecipeID` 的母配方拿到的都是同一個物件，就地改欄位會污染怪物且永久累積到重開遊戲；拷貝後玩家用自己那份、怪物走 `GetWeapon` 拿原始資料，天生隔離。注入點選在 `WeaponManager.AbilityResolver`（`RefreshCurrentWeapon` 呼叫），**八種發射分支含雷射與佛光全部自動吃到，一個分支都沒改**。**疊加是「數值相加」不是「等級相加」**：每個來源各自查 GemTable 得到數值再全部相加（武器內建 Lv3 5 次 + 6 顆 Lv3 珠各 5 次 + 護身符 5 + 戒指 5 = 45 次），所以表永遠只需要 Lv1~Lv3 三欄、不會有「Lv18 查不到表」的問題；**能力刻意沒有上限**，玩家可以全塞反彈換取極端 build。資料面：`ItemTable.csv` 加第 17 欄 `GemID`（仿既有的 `BloodlineID → BloodlineTable` 慣例）、新開 `GemTable.csv`（一種珠子一列、`Field` 欄**原文照抄 RecipeTable/WeaponTable 的欄位名**，避免同一個功能兩邊命名不一致）。**產生規則收斂到 `ItemManager` 這個唯一工廠**：裝備骰孔數 0~6（**隨機位置**，不是前 N 個）、珠子骰等級 1~3，所以怪物掉落／觸發鏈 giveItem／拾取點／祭壇抽選／作弊面板全部自動吃到同一套規則。**孔數在「東西掉在地上的那一刻」就決定**（作者要求：玩家中途看臨時包發現一把 6 孔武器會更想拚過關），因此臨時包、地上掉落物、跨換圖重放、結算畫面全部改成傳完整的 `ItemStack`；地上標籤與 F8 疊層都會標「(5孔)」。所有機率集中在新的 `RandomRules`（作者要求寫在程式裡：機率設定太多、用表記錄不完），依周目的權重覆寫表已預留。鍛造介面這邊，**孔位面板直接讀寫鐵砧上那件裝備的 `ItemInstance`** → 珠子一拖進孔就鑲上去了，沒有「提交」步驟、關面板也不會消失、存檔自然帶著走；底部改成三顆按鈕（強化裝備／拆除裝備／移除鑲嵌，前兩顆待做），「移除鑲嵌」動作前會先確認背包空位夠。順手修掉兩個既有缺陷：關面板退回背包時 `AddItem` 的回傳值被丟棄（背包滿珠子會**直接消失**）、孔位拖錯東西沒有任何提示。⚠️ 三個容易踩的連動規則寫在 `PlayerAbilities`：反彈光加次數沒用（要同時開 `HasBounce` 並把 `BounceTarget` 從 `None` 設成 `Environment`）、穿透的 `-1` 是無限不能直接 +1、拋物線武器的 `Speed` 語意是「飛行秒數」。**平衡用的數值上限尚未討論**，目前只擋住會讓遊戲當掉的下限（發射間隔 0.02 秒、飛行速度 0.05、DOT 節拍 0.02、單次子彈數 64）。

* [x] **能力珠的圖示做成「兩層疊合」＋ UI 貼圖匯入規則與批次工具**（2026-08-04，見 [GEM_SOCKET.md](GEM_SOCKET.md) §6.5）：作者畫的珠子素材是**珠身與能力符號分開兩張**（珠身還有 lv1~lv3 三種外型，讓玩家從外觀就分得出等級），但專案所有繪製點都假設「一個物品 ＝ 一張 Sprite」。評估過三條路：**A** 執行期把兩張合成一張並快取（繪製點零改動，但要 RenderTexture 疊圖再 ReadPixels，這個環境無法驗證）、**B** 兩層繪製 + 共用 helper、**C** 用編輯器工具預先合成 24 張成品圖（程式零改動，但多一個「改了 icon 要記得重跑」的手動步驟——正是 [PROBLEMS.md](PROBLEMS.md) C 類反覆踩的坑）。**選 B**：繪製點數得出來就 6 處、記憶體最省（3+8＝11 張來源，而不是 24 張成品），而且之後要再加一層（稀有度外框、附魔光暈）很自然。新增 `UI/ItemIcons.cs` 當**畫物品圖示的唯一入口**（uGUI 與世界端各一個多載，內部自動管理疊圖子物件），已接上背包格/裝備欄、倉庫格、鍛造鐵砧與孔位、過關結算獎勵、抽選面板(4 處)、地上掉落物。**鐵則：不要再直接讀 `data.Icon`**——那裡的珠子會變成一顆看不出是什麼能力的空白珠子而且不報錯。資料面 `GemTable.csv` 加 `Icon`／`BaseColor` 兩欄，路徑由程式組出來，所以珠子的 `ItemTable.IconPath` 是**空的**；`BaseColor` 留空會依 `Target` 自動推導（Recipe/Weapon → red 技能珠、Player → blue 屬性珠），作者說之後藍/黃珠要用在角色屬性（加血量一類）。**疊圖的 ArtSpec 是量出來的**：符號縮到 55%（原圖符號 428px 寬、比珠子本體 325px 還寬，1:1 疊會蓋掉整顆珠子還突出邊界），三級各自往上 8/24/14（500px 空間）——因為**三級的紅球中心高度不一樣**，lv2 底下多了底座把球往上推 24px，符號要對齊紅球而不是畫布，否則 lv2 的箭頭會偏低壓在底座上。8 種符號當天補齊後重新驗證，55% 在三個等級都不爆框，數字不用調。**順手把 UI 貼圖的匯入設定訂成規則並做成工具**：原本 `UITextureImportSettings` 只在「第一次匯入」時套用（刻意的，否則會蓋掉 Inspector 上的手動微調），所以**既有的圖永遠不會被修正**；而 UI 貼圖一律「不壓縮」（避免 BC 壓縮露出塊狀髒點，見 [PROBLEMS.md](PROBLEMS.md) G2）代表**尺寸直接等於記憶體**，一張 500×500 未壓縮 RGBA ≈ 1MB，11 張珠子圖就 11MB。現在規則收斂到 `Editor/UIAssetRules.cs` 單一來源（道具 icon → 256、長邊 ≥1000 的滿版底圖 → 2048 不縮、其他 → 512），postprocessor 與新的選單工具 `Editor/UIAssetAudit.cs` 共用同一份；工具分「檢查」（只印報告：目前→建議、估算記憶體、還會標『原圖遠大於需要，建議重出圖』）與「套用建議值」（跳確認後才改，原始 PNG 不動）兩個選單。

* [x] **怪物出生點加「重複產生」與「怪物 id 陣列」**（2026-08-06，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3.5）：原本 `monsterSpawn` 只能「進圖時每格生一隻」，要做「撐住 N 秒」「boss 戰期間一直來雜兵」這類房間就得手動塗一大片格子。現在多兩個欄位：**重複間隔秒**（留空＝原本的一次性，填秒數＝每隔 N 秒生一波、一波仍是「每格各一隻」，塗格語意完全不變）與**同時存在上限**（留空＝10 的保險預設）。怪物 id 欄同時支援 `5|7|9` 這種 `|` 分隔陣列＝隨機挑一種（沿用 `SummonIds`／`scriptIds` 的專案慣例，單一 id 照舊）。**最關鍵的一個決定是「重複產生的怪不記 RunProgress『已清』」**——一次性出生點靠 `SpawnKey` 記「這格的怪死了、本趟不再重生」，重複模式若照記，第一波死光後這個出生點就永遠不再生、功能等於沒做。但掉寶原本跟 `SpawnKey` 綁在同一個 if 裡（有 key 才掉、召喚物不掉以防無限刷），所以把**「記不記進度」與「掉不掉寶」拆成兩個獨立判斷**（`MonsterController.SpawnKey` / 新的 `DropsLoot`），重複產生的怪走「不記進度但照常掉寶」，召喚物維持兩者皆無。⚠️ 這代表**重複產生的房間可以刷錢**，與核心迴圈「關卡一次性、不可無限刷」有張力，靠「同時存在上限壓低＋間隔拉長＋只用在有出口壓力的房間」節制（已寫進文件警語）。實作上新增 `MapMonsterRespawner`（掛 `MapRoot` 下，換圖隨之銷毀＝自動停；用有縮放的 `Time.deltaTime`，開背包/對話暫停時不會偷偷累積；每波先清掉已死參照再依上限決定生幾隻），`MapLoader.SpawnMonstersFromMap` 只負責解析與分派。多 id 的挑選刻意分兩種：重複模式每隻重新亂數挑；一次性模式用**格座標的穩定雜湊**（FNV-1a，不用 `string.GetHashCode` 因為它不保證跨執行一致）——否則同一趟關卡換圖來回，沒殺掉的那隻會突然變成另一種怪。另外踩到一個不直覺的地方：「掛在 MapRoot 下＝換圖會自動停」只對同 module 房間互跳成立，跨 module 換圖是協程、讀取頁又刻意不暫停（`timeScale` 仍是 1），中間好幾秒舊元件還在跑，生出來的怪會躲過清場跟到下一張圖，所以 `Update` 要自己擋 `MapManager.IsLoading` 與 `GameFlowManager.IsEndingLevel`（已記進 [PROBLEMS.md](PROBLEMS.md) B8）。編輯器端只動 schema（`TriggerType.cs` 的內建預設 ＋ `triggerTypes.json`），`TriggerTypeStore.Load` 的合併機制會自動幫既有 json 補上新欄位，**舊地圖不用改、不填就是原本的行為**。順手把兩張測試競技場（`Future_Arena` 15／`Future_Arena2` 16）加進 `Project Tools/測試/直接進關卡` 選單：`DevQuickStart` 的目標字串多支援通用的 `map:<id>` 型（原本只有寫死的 `Hub`/`Hub1` 走地圖 id），之後再加測試地圖只要加一顆 id 常數＋兩個 MenuItem。同時修掉 `MapsTable.csv` 那兩列的兩個錯：Path 少了 `Modules/` 前綴（載不到圖）、`NoWeapon` 誤填 1（競技場裡不能攻擊）。

* [x] **新增「開關(按F)」trigger ＋ 怪物出生點「啟動旗標」**（2026-08-06，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3.5～3.6）：競技場測試時的實際需求——進圖要先花時間整理裝備，希望「走到拉桿按 F 才開始湧怪、想停下來再按一次」。現況有兩個缺口：**沒有任何「純按 F 跑觸發鏈」的 trigger**（`pickup` 會給道具、`drama` 會開對話面板、`openPanel` 會開 UI，三個都會多做一件不想要的事），而且**怪物出生點完全不吃啟用條件**（`MapLoader` 直接掃區域生怪，從來沒查過 `TriggerChain.IsActive`）。**開關這邊**新增位置型 `switch`：靠近出現青綠星星，按 F 就把「切換旗標」翻成成立／取消，自己不做任何事——做什麼由「誰在看這個旗標」決定（出生點的啟動旗標、地上物的 appearFlag/disappearFlag、其他 trigger 的條件旗標…）；第一次開啟時會跑一次自己的 setFlag/next，所以也能當一般機關（開門、播對話）用。因為互動系統前陣子已重構成「可註冊的互動型別表」，這筆只花了表裡加一筆＋一個 `SwitchPoint`。順帶補上兩個對稱的基礎設施：`TriggerChain.ClearFlag`（與 `SetFlag` 對稱、三種範圍都支援）與 `LevelPrefix`（`關卡:` 前綴，與 `永久:` 對稱，強制「關卡單次」範圍——給程式產生、作者沒辦法在旗標登記表登記的自動旗標用；開關的「已跑過鏈」旗標就是靠它每趟關卡歸零，不加的話會落到預設的周目、寫進存檔，之後整個周目再也不跑鏈）——**取消旗標刻意不觸發 `OnFlagFirstSet`／`fireOnFlag`**，但也因此「取消後再成立」會被當成又一次首次成立，所以文件明寫：別把接了 `fireOnFlag` 的旗標拿來當開關的切換旗標。**出生點這邊**做成**持續判定而不是一次性事件**：`MapMonsterRespawner` 每幀判定要不要生，條件取消就停在原地（計時器不累積）、重新成立就繼續——「暫停/恢復」因此不用任何額外狀態，兩邊只靠一個旗標溝通。一次性出生點若有條件擋著也改走 respawner（條件成立時生一波就結束），這條路徑仍帶 `spawnKey`，所以照常記 RunProgress『已清』＋掉寶，語意跟原本一致。
  ⚠️ **這一段當天稍後被推翻、重做過一次，見下一則**：最初是另開一個專屬的 `startFlag`「啟動旗標」欄，理由是「不想動共用的啟用判定路徑」，且以為通用欄位只有「解鎖後永久有效」的語意、做不出可反覆切換的暫停（實際上 `requireFlag` 是每次都重算的持續判定，做得到）。

* [x] **推翻上一則：出生點的「啟動旗標」拿掉，改吃觸發鏈的通用條件欄位**（2026-08-06，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3.5）：功能做完當天作者第一次實際配置競技場，就把旗標填到**通用的「條件旗標＋初始停用」**去了——因為那兩欄本來就顯示在每一顆 trigger 的參數下方，看起來理所當然會生效；而出生點其實不吃通用欄位，於是靜默無效、一進圖就生怪，完全沒有訊息可查。作者的評語是「既然已經有通用的條件旗標，為什麼還要加一個自己用的？這樣各式各樣的條件旗標未來越來越多，會越來越容易填錯」——這個判斷是對的，**同一件事給兩個入口、而且兩個長得幾乎一樣，就是在製造 bug**。所以把 `startFlag` 整個移除，改讓 `monsterSpawn` 走 `TriggerChain.IsActive(region)`：條件旗標／初始停用＋解鎖旗標／周目上下限／道具／完成關卡數一次全部支援。原本擔心的「通用欄位做不出可切換的暫停」是誤判——`requireFlag` 每次都重算，取消就停、恢復就繼續，正是要的行為；而 `startDisabled` 則是另一種語意（一次性解鎖），兩者剛好對應「可暫停」與「開了就不再關」兩種需求，都用得上。**回歸風險是零**：全專案 12 顆出生點只有這顆填了通用欄位，其餘 11 顆條件全空 → `HasChainCondition` 回 false → 走原本進圖直接生完的路徑，一行行為都沒變。實作上只有「有填條件」的出生點才交給 respawner 逐幀判定，**而且連第一波都交給 `Update`**——因為 `MapManager.PlaceAndSetup` 的順序是 `SpawnMonsters()` → `SetupWatcher()`（`TriggerChain.Setup` 在後），載圖當下去查 `IsActive` 會讀到**上一張地圖**的停用集合，只勾「初始停用」的出生點會被誤判成可以生、當場生一波（初始停用被靜默忽略一次，最難查的那種）。順手把同一類「填了卻靜默無效」的坑補上 Console 警告：**初始停用＋條件旗標同時填**（語意互斥→永遠不生怪）、**重複規則填在出生點上**（出生點不看那欄）。

* [x] **作弊面板給的道具跑進臨時包**（2026-08-06，見 [RUN_PROGRESS.md](RUN_PROGRESS.md) §6）：在關卡裡（競技場測試時）用 L 開作弊面板給自己道具，背包卻是空的。原因是「給道具」走 `RunProgress.GiveItem`——那是**取得物品的統一入口**，規則是「關卡內一律進臨時包」，而臨時包要**通關才落袋、死亡歸零**，所以東西確實給了、只是玩家看不到也用不到。作法是給 `GiveItem`／`GiveStack` 加一個 `toRealBag` 參數（預設 false＝維持原規則），作弊面板傳 true。**刻意不改成直接呼叫 `InventorySystem.AddItem`**——那會繞過統一入口的另外兩件事：需要實例的物品（裝備／能力珠）要先經 `ItemManager` 骰孔位，銅錢 101 要轉成金錢數字而不是佔一格背包。順帶一提拾取點本來就有同名的 `toRealBag` 欄（佛燈那類教學道具在用），只是它自己在 `InteractionManager` 裡另外寫了一份邏輯，這次沒動它（動了會改到金錢的分支行為）。

* [x] **背包介面重製：新美術 ＋ 裝備/消耗品雙頁籤 ＋ 分頁**（2026-08-07，見 [INVENTORY.md](INVENTORY.md)）：作者畫好一整套新背包素材（1254×1254 正方形底圖，以及頁籤/重整鈕/箭頭/頁碼四種零件圖），要把原本「7×9 一整包 63 格」改成「上方兩個頁籤（裝備 / 消耗品）、中央 5×4 一頁、底部左右箭頭翻頁」，兩包**各 40 格 = 各 2 頁**，且日後容量可能再加、要能自動多分頁。**資料層的關鍵決定是「不做成兩個獨立容器，而是把同一條扁平陣列切成兩段」**（前 40 裝備包、後 40 消耗品包）——因為「鍛造台鎖住哪一格」「存檔的格位」「新手教學要指哪一格」全都用同一個格子編號在對話，切段讓 `ForgeAnvilSlot`／`ForgingPanel.IsGridLocked`／`SaveManager.FindOwnedStack`／`GridSlotDTO.slot` 一行都不用改；要加格只改 `EquipBagCount`／`ItemBagCount` 兩個常數，`PagesOf()` 自動算出幾頁。**分包規則刻意只有一處**（`InventorySystem.BagFor`：`IsEquippable` → 裝備包、其餘 → 消耗品包），呼應前一週「同一件事不要給兩個入口」的教訓；能力珠不可裝備，與作者確認後歸消耗品包。`AddItem`／`AddStack` 自動路由，所以倉庫點擊搬運、掉落物落袋、觸發鏈給道具全部不用改。**三個「防止物品消失」的護欄**：① `MoveGrid` 跨包一律拒絕（不然裝備混進消耗品包就永遠排序不到正確位置）；② 真的跨包時（背包停在消耗品頁、從倉庫拖一把劍進來）由 `InventoryActions.Resolve` 攔下改走 `AddStack` 丟進正確那一包，**不是在 `SetAt` 拒收**——拒收的寫法會「先塞目標再清來源」而讓東西不見（[STORAGE.md](STORAGE.md) 記過的坑）；③ 一頁若有超出容量的多餘格子直接 `SetActive(false)`，並在 `InventorySlotWidget.Blocked` 加上 `index < 0`，不留 index 越界的格子。**舊存檔遷移**在 `RestoreState` 裡：格號所在的包與物品該去的包對不上就改用 `AddStack`，物品與鑲嵌都不掉、只有排列順序重排一次（會印 Log 說明重排幾件）。介面端：格子**只建一頁 20 個並重複使用**，切頁籤/翻頁只重綁 `index` 不重建物件——新手教學的 `TutorialBlockerPanel.LockTo` 鎖的是 GameObject，重建會讓它指到已銷毀的物件；`FindGridSlotRect(itemId)` 改成**會自動切到那件東西所在的頁籤與頁數**（先掃當前頁避免每幀重切），柴房佛燈（裝備包）／儲藏室藥水與傳送門劇本（消耗品包）三段教學才指得到。零件素材沿用抽選／鍛造那套 **ArtSpec**（量不透明邊界框、`PlaceArt` 反推方框），因為這批圖四周有大片透明留白（箭頭圖 500×500 但內容只有 350×435），直接照原圖擺會整個偏；**左箭頭直接鏡像右箭頭**那張圖，不另外出圖。素材圖一律 `raycastTarget=false`，點擊全靠疊在上面的透明按鈕（美術完整露出、只用 tint 做 hover 回饋），順序上透明按鈕一定要建在素材圖之後。其他：重整鈕改成**只整理當前頁籤那一包**（裝備包依 武器/盔甲/手套/鞋子/護身符/戒指、消耗品包依 藥水/其他），裝備欄順序改成左欄 武器/手套/鞋子、右欄 盔甲/護身符/戒指（照新底圖畫的剪影，與舊版左中放鞋子不同），金錢改**靠右對齊**收在底圖畫好的錢幣左邊（`resizeTextForBestFit` 防爆框），面板顯示高度 1040→900（正方形，與倉庫並排時 `PairRightX` 420→480，倉庫與鍛造的位置不動），七張零件圖的 Max Size 依 `UIAssetRules` 由 2048 改成 512。
  **同日實機跑完後的微調**：① 裝備欄 icon 放大到方框的約 8 成（`178/148/142`，原本 `132/116`）——素材四周本來就有透明留白，給小了在遊戲裡看起來只有格子一半大；道具格 `74→80`、藥水格 `92→108` 一起調。② 頁碼只顯示「現在第幾頁」，總頁數由箭頭亮不亮表達。③ 金錢改**靠左**對齊擺在牌子前段，框縮成 `108×46`（右界 866，背景圖從 878 開始畫錢幣），長數字靠 bestFit 縮字級不會壓到錢幣。④ **並排位置改用「看得見的美術」重算**——這幾張底圖四周都有大片透明留白（背包左右各 ~57px、倉庫 ~52px、鍛造 ~127px），第一版拿整張圖寬度去排，結果中間空出一大塊、背包還被推到快出畫面；改成「兩邊可見美術中間留 40 單位、整組置中」後是 背包 400 / 倉庫 -416 / 鍛造 -447。⑤ 順帶發現一個容易忽略的事：CanvasScaler 用 `MatchWidthOrHeight=0.5`，**畫面比例越窄，可用的參考寬度就越小**（作者的視窗約 1.49 比例 → 參考寬只有 ~1756 而不是 1920），所以固定的並排 X 在窄視窗會把面板切掉一半；`InventoryPanel.PairedX()` 因此加了「夾住不超出畫面右緣」的保護。

* [x] **物品 icon 大小自動正規化（IconFit）**（2026-08-07，見 [INVENTORY.md](INVENTORY.md)、[PROBLEMS.md](PROBLEMS.md) E10）：背包做好後實機一看，同一個格子裡藥水的 icon 明顯比別的小一截。量過 `Resources/UI/Icons/` 全部 30 張才發現根因——**不透明內容佔長邊的比例從 41% 到 100%**（`item_hpPosition_s` 是 500×500 畫布裡只有 146×206，`weapon_sword` 整張畫滿），uGUI 對齊的是整張圖，所以留白多的那張看起來就小 2.4 倍；`preserveAspect` 完全幫不上忙（它只管整張圖的長寬比）。這其實是 [PROBLEMS.md](PROBLEMS.md) **E9 的物品 icon 版**，但 E9 那套「量一次寫成 ArtSpec 常數表」在這裡不適用——**物品 icon 會一直加**，每加一張就要記得回來量、記得更新表，正是 C 類反覆踩的「改了要記得同步」。所以改成**執行期自動量**：新增 `UI/IconFit.cs`，用 `Sprite.vertices`（Tight 網格的頂點）取內容外接框，反推 Image 的 `sizeDelta` 與偏移，讓看得見的那塊正好塞滿呼叫端給的內容框。**關鍵是這條路不需要貼圖開 Read/Write**——開了會多一份 CPU 記憶體，而且新圖還得記得勾，等於把同一個坑換個地方挖；前提只是 icon 的匯入設定是 Mesh Type = Tight（`spriteMeshType: 1`，專案預設就是），萬一哪張是 Full Rect 就自動退回「不縮放」、行為與以前相同。掛在 `ItemIcons.Apply`（畫物品圖示的唯一入口）裡，所以背包格／裝備欄／藥水格／倉庫／鍛造鐵砧與孔位／過關結算／抽選面板／底部 HUD／傳送門劇本方框**一次全部生效**——這正是 08-04 把繪製收斂成單一入口換來的紅利。順帶處理三件事：① 呼叫端的 `sizeDelta` 語意變成「內容框」，背包因此**刪掉所有逐格的 icon 尺寸常數**，改成格框 × `IconFillX/IconFillY`（0.84/0.82）；② `IconFit` 只處理固定尺寸的 icon（`anchorMin==anchorMax`），拉伸型會被跳過，所以把倉庫格與劇本方框的 icon 從四邊拉伸改成固定尺寸；③ 藥水格、底部 HUD 藥水、劇本方框原本各自直接讀 `data.Icon`（繞過唯一入口），一併改成走 `ItemIcons.Apply`。另外把數量文字也規範化：字級改成依格子大小算（`min(寬,高)×0.26` 夾 18~30）並加深色陰影，壓在 icon 亮處才看得清楚。⚠ 已知副作用：留白多的圖，Image 的 rect 會被放大到比格子還大（藥水在 95×92 的格子裡 rect 是 183×183），多出來的全是透明、icon 又是 `raycastTarget=false` 所以不影響點擊，但**之後若要在格子加 `Mask`／`RectMask2D` 要記得這件事**。驗算：正規化後 30 張 icon 的可見長邊全部落在 75.4~79.8（框是 79.8×75.4），沒有一張溢出方框；正規化前是 31.1~75.4。

* [x] **背包的格子提示重做：hover 改描邊、拖曳提示改呼吸外框（順便挖出 Linear 色彩空間的坑）**（2026-08-07，見 [INVENTORY.md](INVENTORY.md)、[PROBLEMS.md](PROBLEMS.md) E11）：作者問「滑鼠移到盔甲欄上整格變成一大塊黃色，這不是拖曳時才該有的提示嗎？」——**先查再改**。量了截圖那塊黃色是 RGB(128, 110, 56)，比對三種可能：只有 hover（α=0.22）預測 (129,106,41)、只有拖曳提示（α=0.30）預測 (148,122,46)、兩層都亮（α=0.454）預測 (178,147,55)。**第一個吻合**，所以確定是 hover 高亮、而且沒有兩層疊在一起。但關鍵是「為什麼 α=0.22 會看起來這麼重」——因為**專案是 Linear 色彩空間**（`m_ActiveColorSpace: 1`），亮色疊暗底時比 Gamma 直覺重很多：同一組值 Gamma 是 RGB(73,62,32)、Linear 是 RGB(129,106,41)，**等於看起來像 α≈0.45**。再乘上新裝備欄是舊版的 3.4 倍面積（104×162 → 221×258，也是一個道具格的 6.5 倍），就從「微微發亮」變成「一大塊黃色看板」。順帶暴露一個設計問題：hover(0.22) 與拖曳提示(0.30) **RGB 完全相同、只差 0.08 alpha**，玩家根本分不出來，拖曳提示形同虛設。改法：新增 `UI/SlotOutline.cs`（四條細線圍一圈，錨點各貼一邊所以貼滿任何大小的格子都成立、線粗不變；左右兩條上下內縮一個線粗避免四角疊兩層變亮）；**hover 改成只描邊不填滿**——格子再大也只是一圈線，跟面積徹底脫鉤，背包與倉庫共用同一套；**「可放這格」改成會呼吸的亮金外框（α 0.40↔1.00）＋ 很淡的固定底光（0.07）**，讓會動的是外框而不是底光（底光一強又會退回一片黃看板）。兩個實作地雷寫進文件：① 拖曳提示現在是「底光 ＋ 外框子物件」的容器，開關**一定要用 SetActive**，只關 `Image.enabled` 的話子物件照樣會畫；② 呼吸要用 `Time.unscaledTime`，背包把遊戲暫停（timeScale=0）時 `Time.time` 是停的。最後把全專案 67 筆寫死的半透明顏色掃過一遍、算出各自的「等效 alpha」交給作者判斷，**刻意沒有一次全改**——那些值當初都是看畫面調到順眼的，本來就已經是 Linear 下對的值，真正會出事的只有「同一個數值後來被套到大很多的區塊」這一種；其中最值得回頭看的是 `UIManager.backdropColor`（0.60 → 等效只有 0.34，開視窗時背景其實沒那麼暗）與文字陰影（0.85 → 等效 0.58）。

* [x] **場景照明：單光源升級成多光源，火把/燈籠可調亮度、光色、搖晃**（2026-08-10，見 [ATMOSPHERE.md](ATMOSPHERE.md)）：作者想在場景裡放火炬、燈籠這種照明物並且真的會發光，先問「原本的『場景特效』能不能沿用」。**先查再做**——查完結論是不能，但也不用從零開始。`SceneFxTable.csv` 那套（煙/火/冰/毒/傳送門）是**粒子發射器**，欄位是每秒噴幾顆、壽命、大小、亂流，它能讓火把上面有火焰，但完全不會照亮周遭，拿來做照明是錯的工具。真正貼近的東西其實已經存在：地圖編輯器的地上物面板早就有「**發光半徑**」欄，`MapLoader` 會依它掛 `LightSource`、`AtmosphereController` 拿去當光圈中心。所以這次的工作不是「做一個照明系統」，而是**把既有的照明地基從單光源解開**。原本卡在三個地方：① `LightSource.Nearest()` 只回最近的一個、shader 也只有一組 `_PlayerPos/_InnerR/_OuterR`（程式碼註解自己寫了「多光同框需改 shader，之後再說」），所以**一整排火炬只有離玩家最近那支會亮**；② 只有暗氛圍（2 幽暗/3 噩夢/9 深海恐怖）吃照明，`Atmosphere=1` 是 passthrough，白天室外的火把完全是死的；③ 只有半徑一個參數，沒有亮度與顏色，而搖晃是**全域共用一組** Perlin 呼吸 → 全場的燈同步明滅，看起來很假。
  **技術路線在兩案之間選了 A**：A＝後處理 shader 陣列（`SetVectorArray` 餵最近 N 盞，shader 內迴圈疊合）；B＝光照圖 RenderTexture（每盞燈畫成 additive quad 到獨立貼圖再當遮罩）。B 的表現力明顯高一階——光源數量無上限、每盞可用不同形狀的光暈貼圖（火焰狀、窗格狀）、之後要做「光被牆擋住」也是它——但要多一張 RT、架構複雜一階。選 A 是因為改動集中在兩個檔、一次 blit 效能最好、最快看得到畫面，**而且欄位設計成 B 也吃得下**：六個欄位（半徑/亮度/光色/搖晃強度/搖晃速度/邊緣柔和）在 B 案下語意完全相同，將來換底層不用回頭重擺地圖。
  **實作要點**：`LightSource` 從「只有 radius 的標記」長成完整的一盞燈（六個欄位 ＋ **每盞自己的亂數種子**），`Breathe(t)` 回這一瞬間的搖晃倍率、同時作用在半徑與亮度上（火焰變大時也變亮，比只縮放半徑自然）；`CollectNearest` 的排序鍵刻意用「**距離 − 半徑**」而不是純距離——遠處一盞大燈的光圈可能仍照到畫面，不該被近處的小燭火擠掉。shader 端多盞用 **screen 疊合**（`v + vi − v·vi`）而不是 `max`，兩圈交界才會自然變亮、不出現硬邊；顏色以亮度加權平均後做**亮度歸一**再套用，所以鬼火照出來是青綠、火把是暖橘，但**換色不會順帶改變明暗**（不歸一的話換成暗色系的光會整片變暗，等於顏色和亮度兩個旋鈕黏在一起）。迴圈加了 `[loop]` **刻意不讓它展開**——這支 shader 已經把 15 種氛圍攤平在同一個 pixel shader、註解明講指令數吃緊才拉到 `target 3.5`，展開 12 次很可能撞編譯上限，那就是 [PROBLEMS.md](PROBLEMS.md) **E3 的全螢幕洋紅**。
  **「環境亮度」是為了解上面第②點**：`MapsTable.csv` 加第 11 欄 `EnvBright`（0~100，**留空/缺欄＝100＝完全不壓暗，舊地圖零行為變化**），只在 `Atmosphere=1` 時生效，把整張圖壓暗到該亮度、再讓場上的燈照回來。刻意**不讓它影響 `Atmosphere>=2`**——那些氛圍的暗度是氛圍本身的定義，讓兩個旋鈕相乘只會讓既有地圖不好調又有回歸風險。它的價值在於「不到幽暗等級、但想讓火把有存在感」的室內走廊/地窖：比起直接設成 `Atmosphere=2`，它不去飽和、不加冷色調，美術原本的顏色都在。
  **編輯器端**：地上物面板在「發光半徑 > 0」時才展開照明細項（不發光的地上物不該被這些欄位洗版），並給六顆**燈種預設**鈕（火把/燭火/燈籠/鬼火/月光/爐火）一鍵套好五個欄位、**但不動發光半徑**（範圍是每個場景各自的事，不該被預設覆蓋）。另外新增 `Core/LightOverlay.cs`：編輯器不跑氛圍後處理，光的實際效果只有進遊戲才看得到，**沒有視覺回饋等於盲填數字**，所以在地上物工具下把每盞燈畫成兩個同心圓（外圈＝照得到的範圍、內圈＝全亮範圍＝邊緣柔和度），圈的顏色就是該盞燈的光色。
  **刻意沒做**：光被牆擋住（需要 B 案或額外的遮蔽圖）、非圓形的光暈形狀（同上）、獨立於地上物的「純光源」放置分頁（跟作者確認後決定先靠地上物欄位涵蓋，火把圖本身自帶光最直覺、既有地圖零遷移）。**同框 12 盞是 `AtmosphereController.MaxLights` 與 shader `MAX_LIGHTS` 兩邊寫死的常數，改一邊一定要改另一邊**——這是這次留下最容易踩的一顆雷。

* [x] **照明補上「獨立光源」：不綁地上物，直接放一個點就會發光**（2026-08-10，見 [ATMOSPHERE.md](ATMOSPHERE.md)）：多光源做完當天作者回報**方向搞錯了**——「我想的是不需要地上物的光源，因為現在很多火炬或是燈籠是直接畫在背景的，如果要有地上物，我就得再把這些圖拆出來做成地上物」。這是我問「放置方式」時把它列成選項二（獨立照明分頁）、作者選了選項一（擴充地上物欄位）的結果；**選項描述沒有點出「你的火炬到底畫在哪裡」這個決定性前提**，所以那次選擇是在資訊不足下做的。教訓是**問選項時要問到「你現在的素材長什麼樣」，而不是只問「你想怎麼放」**——後者聽起來合理但答案取決於前者。
  **好消息是底層不用動**：上一則已經把光源清單、shader 陣列、六個參數欄位、每盞獨立的搖晃相位全部做好，這次只是**再接一個資料來源進同一份清單**。新增 `LightInstance`（id/name/x,y ＋ 半徑/亮度/光色/搖晃強度/搖晃速度/邊緣柔和）掛在 `MapData.lights`，與 `sceneFx` 平行、獨立於三層圖層；遊戲端 `MapLoader.BuildMapLights()` 逐個生一個**沒有任何外觀的空物件**掛 `LightSource`（火炬的圖本來就在背景裡，這裡只補「會發光」這件事），掛在地圖 root 底下所以換圖自動清、`LightSource.OnDisable` 自動退出登記表。`.dipanmap` 是整包 `MapData` 直上 Newtonsoft、沒有欄位白名單，所以 `lights` **自動進存檔也自動進 Undo 快照**，舊地圖缺這個欄位就是空清單、零遷移。
  **編輯器新增「照明」工具**（頂部工具列，在「場景特效」右邊）：操作刻意與場景特效一致（＋新增 → 選取 → 放置位置 → 點畫布），但多做三件針對「擺一整排火炬」的事：① **新增完直接進放置模式**，點一下就定位，不用再按一次按鈕；② 新的光源生在**目前鏡頭中心**而不是地圖中心（大地圖時生在地圖中心會找不到）；③ 加「**複製一盞**」鈕，把所有參數原樣複製並直接進放置模式——一排同款火炬只要調好第一盞，其餘都是點兩下。另外**點畫布上的燈可以直接選取**（不用回清單找），可點半徑與畫面上的中心十字大小一致。
  **`LightOverlay` 擴充成兩種燈都畫**：獨立光源在「照明」工具下顯示（外圈＝照射範圍、內圈＝全亮範圍、**加中心十字**——光源本身沒有圖，沒有十字不知道該點到哪裡），地上物的燈在「地上物」工具下顯示；照明工具下也會把地上物的燈畫成暗一點的圈，排整個房間的照明時才看得到全貌。**兩種燈刻意不同時亮在同一個工具下**，不然一堆圈疊在一起分不清哪個是哪個。
  **順手修掉一個設計瑕疵**：上一則寫的 `LightNumField` 第一個參數 `sel` 從頭到尾沒被用到（欄位存取全走 `Func`/`Action`），這次要給 `LightInstance` 共用時才發現——直接拿掉那個參數，兩種燈共用同一個方法；`ApplyLightPreset` 因為兩邊欄位名不同（`lightColor` vs `color`）所以開了一個多載。
  **兩條路並存、不擇一**：獨立光源是擺場景照明的主力；地上物自帶的燈保留給「這盞燈本身是個能互動的東西」——例如柴房地上的佛燈，**撿走時光要跟著消失**，那是綁在物件生死上才做得到的。差別只在「光要不要跟著某個物件的生死」。
  **刻意沒做**：光源的「點亮旗標」（`enableFlag`：火把要被點燃才亮）——地上物那邊已經有 `appearFlag`/`disappearFlag` 的成熟模式，獨立光源要做等於再拉一套平行的旗標接線，等真的有「點火把開門」這種關卡設計時再一次做完整比較好，現在加只是憑空多一個沒人用的欄位。

* [x] **地圖編輯器加上「照明預覽」與拖曳光源（順便修掉自己造成的回歸）**（2026-08-10，見 [ATMOSPHERE.md](ATMOSPHERE.md)）：作者實測獨立光源「效果不錯，但在編輯器上完全看不到效果」，提三點：① 放完要能立刻看到實際狀況並隨參數變動 ② 要能像拖地上物一樣拖曳 ③ 編輯器要有壓暗效果、不用跟遊戲 shader 一模一樣。
  **先講踩到的坑**：查接線時發現 `EditorBootstrap` 裡 **`LightOverlay` 的註冊行不見了**——是我自己上一輪弄掉的。做獨立光源那次，我從 `/mnt/user-data/uploads` 的**舊快照**複製 `EditorBootstrap.cs` 到工作目錄，那份是「加 LightOverlay 之前」的版本；在它上面加完 LightController 就整份覆蓋回去，等於把 LightOverlay 那行還原了。所以作者連光圈參考線都沒看到，不只是沒有明暗。**教訓正是既有工作慣例寫過的那條「確認檔案用 device_bash 別信 staged 快照」——但我只用在『讀來確認』，沒用在『複製來當編輯基底』**，而後者才是真正會造成回歸的路徑。之後凡是要再次編輯已經送出去的檔案，一律重新 stage 取得當下版本。
  **② 拖曳**：`LightController` 加拖曳狀態機。兩個刻意的細節：位移用「**按下當時的滑鼠世界座標 → 現在的滑鼠世界座標**」算，而不是把燈心貼到滑鼠——否則點到把手邊緣時燈會瞬移一段，手感很差（照抄 `ObjectController` 的 `_dragStart` 做法）；**Undo 只在「真的移動了」的第一幀推一筆**，而且推之前先把座標還原成拖曳起點，快照才會是「拖之前」的樣子（純點選不該產生一筆 Undo，每幀推一筆則會讓 Undo 堆爆掉）。把手在 `LightOverlay` 畫成圓＋十字，**大小與 `PickNearest` 的 `pickR` 綁在同一個式子**（`max(0.4, tileSize×0.5)`）——兩邊不一致就會出現「看得到卻抓不到」這種最難查的錯位。
  **①③ 預覽**：新增 `Core/LightPreview.cs` ＋ `Resources/Shaders/EditorLightPreview.shader`。**關鍵決定是「畫一張蓋滿視野的四邊形」而不是相機後處理（`OnRenderImage`）**——編輯器的參考線（光圈、選取框、格線）全畫在 `OnPostRender`，那是在相機算繪之後、後處理之前，用後處理會把參考線一起壓暗看不清；改成四邊形參與正常算繪，`OnPostRender` 的線就蓋在它上面維持清楚。混合模式用 **相乘（`Blend DstColor Zero`）**，輸出 `lerp(1−環境壓暗, 1, 光照量) × 光色偏移`——這剛好與遊戲端 type 1 分支**完全同一條式子**，所以明暗是真的一致而不是「差不多」。座標改用**世界空間**（四邊形的片段直接拿得到世界座標），半徑就是「格」，不必像遊戲端做 orthographicSize 換算，縮放/平移自動正確。`sortingOrder` 用 **32766**：本專案 sortingOrder 實質是 16-bit（[SCENE_EFFECT.md](SCENE_EFFECT.md) 記過），大基底會繞回負值被背景蓋住；32767 留給「顯示底部ui」參考層，那是對位用的不該被壓暗。搖晃曲線照抄遊戲端，種子以光源物件為 key 快取——**不能每幀重算或用 Random**，否則拖曳/改參數時相位被重設會一直抖。
  **開關放在頂部工具列而不是照明面板**，因為它是「檢視模式」：擺地上物、畫地磚時也會想開著看氣氛；狀態與亮度都記在 PlayerPrefs。**預覽環境亮度是編輯器本地的滑桿、刻意不存進 `.dipanmap`**——那個值的正身是主專案 `MapsTable.csv` 的 `EnvBright` 欄（編輯器根本讀不到 MapsTable），所以面板直接寫「調到滿意後把這個數字填進 EnvBright」，讓滑桿當試色盤而不是第二個真相來源。
  **順手做了一個之前想不到的檢查**：預覽最多畫 32 盞、遊戲同框上限 12 盞，所以面板會算「**光圈有碰到畫面的盞數**」，超過 12 就出示警告說明遊戲會丟掉最遠的、預覽比遊戲亮屬正常。這比「地圖總盞數超過 12 就警告」精確——30 支火把散在整座城堡完全沒問題，會出事的是**同時擠在一個畫面裡**。
  **明講的差異**：預覽只模擬「壓暗＋照亮＋光色」，遊戲的幽暗/噩夢氛圍還會去飽和、加冷色調，實際更陰沉；面板上也寫了這句，免得之後照著預覽調完進遊戲覺得「怎麼不一樣」。

* [x] **地圖編輯器版面重整：左側工具列＋底部狀態列＋相機讓位**（2026-08-10，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)）：作者貼圖回報「照明的選項被擠到外面去了」，並說「之前就覺得這個介面不太友善」。**先量再改**：頂部列是單一橫排，18 顆按鈕加起來約 1160px，中間還夾了一條長度不固定的地圖資訊 label（`地圖：… | module：… | 18×10 格 | tile 1`）——IMGUI 的固定寬 `GUILayout.Button` 不會縮，超出區域就直接畫到畫面外，所以照明／劇情／特效預覽器整排掉出螢幕。**這半年已經發生兩次**（加場景特效一次、加照明一次），是結構問題不是偶發。
  **診斷時把兩個問題分開**：① 按鈕溢出＝排版問題；② **右側面板用 `GUILayout.BeginArea` 直接蓋在場景上**，地圖右緣永遠有 240px 看不到、擺東西得一直平移鏡頭＝結構問題。做了三個版面方案的 HTML mockup 讓作者比較（現況／兩列止血／左側工具列＋相機讓位／再加可收合），作者選了中間那個。
  **核心改動是「相機讓位」**：新增 `Core/EditorViewport.cs`，把 `Camera.rect` 縮到「扣掉左側工具列、右側面板、頂部列、底部狀態列」的中央區域，面板從此是**排在旁邊**而不是蓋在上面。Unity 會自動連帶處理三件事，所以其他程式一行都不用改：`Camera.aspect` 變成可視區比例 → 聚焦（`FrameMap`）自動以可視區為準；`ScreenToWorldPoint` 會考慮 `pixelRect` → 塗格/放置/拖曳的滑鼠座標自動正確；`OnPostRender` 的 GL 參考線也一併被限制在可視區內。
  **⚠ 踩到的坑：`Camera.rect` 以外的區域不會被相機清除**，會殘留上一幀畫面，而 IMGUI 的 box 底圖是半透明的蓋不住。解法是另外生一台「只負責清背景」的相機（`cullingMask = 0`、`clearFlags = SolidColor`、rect 全螢幕、depth 比主相機低 100），主相機改成只清深度。那台相機刻意**不掛 MainCamera tag**——全專案的工具都用 `Camera.main` 或 `GetComponent<Camera>()` 取相機（查過 20 處），不掛 tag 就不會被誤抓。
  **版面**：工具（畫/擦/物件/可走/Trigger/場景特效/照明/劇情）從橫排改成**左側垂直工具列**（`RailW=76`）——垂直空間幾乎用不完，之後再加工具也不會重演這次的溢出；頂部列只留檔案與檢視操作，加上**旗標**（開的是彈窗、不是工具）與**特效預覽器**（佔滿畫面的獨立模式）；地圖資訊與狀態訊息移到**底部狀態列**。頂部列從約 1160px 降到約 740px。
  **順手收斂一個技術債**：右側面板的 rect 原本在 7 個地方各寫一份 `new Rect(Screen.width - PaletteW, TopBarH, PaletteW, Screen.height - TopBarH)`，改版面時等於要改 7 次、漏一個就有面板蓋到狀態列。全部收斂成單一 `PanelRect` 屬性，`ViewportRect` 也放在同一處當「版面唯一真相」供 `EditorViewport` 取用。地上物選取面板（左下角）與多選面板同步往右讓開工具列、往上讓開狀態列，`IsPointerOverUI` 補上這兩塊新的 UI 區域（漏掉的話點工具列會連帶在地圖上塗一筆）。
  **這次刻意沒做**（作者選「專心做版面」）：數字鍵切工具、面板改可折疊分區、工具列改圖示。可收合／可拖曳寬度的面板（C 案）也留著，等版面用一陣子有感覺再說。

* [x] **移除地磚（tile）系統 ＋ 特效預覽器補關閉鈕**（2026-08-10，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md)）：作者說「這專案基本不走 tile 路線了」，要拔掉「畫」「擦」與相關功能，但要求**先確認是否真的完全不需要、拔掉會不會出事**。
  **先驗證再動手**，三條證據都指向可以安全拔：① 掃過全部 69 個 `.dipanmap`（含 build 副本），**`tiles` 總數為 0**——沒有任何一張地圖放過地磚，地面全靠背景圖＋地上物；② `GameAssets/Main/Tiles/` 與 `GameAssets/Modules/RedBridalGown/Tiles/` **原始素材夾是空的**，StreamingAssets 裡那 3 張 `tile1~3.png` 是舊同步留下的殘骸，最新的 catalog 已經沒有 `Tiles` 分類；③ 程式面完全隔離，唯一的跨界只有素材分類白名單。
  **關鍵是把 `tileSize` 和 `tiles` 分清楚**：`tiles` 是放下去的地磚（要拔），`tileSize` 是「一格等於幾個世界單位」——可走層子格、地上物座標、鏡頭框景、A* 導航格全都靠它，**動了會整個專案錯位**。查引用時特意把兩者分開列，確認 `tileSize` 在 20 個檔案有用到、一個都不能碰。另外 `GroundEffectInstance` 也有一個 `BuildTiles()`，那是地面特效鋪格用的**同名巧合**，不是地磚系統。
  **拔除範圍**：工具列舉 `TilePaint`/`Erase`；`PaintController`／`TilemapView`／`TileBrushPreview`／`TilesetService` 四支整檔；EditorUI 的地磚調色盤整段（約 4700 字元，含 `HasTileBrush`/`TileBrushAt`/`TilesetItems`/`DrawPalette`/`SelectTileBlockDefault`）與筆刷狀態欄位；資料層 `TilePlacement` 與 `LayerData.tiles`（**兩個專案都要**，是雙專案鏡像的資料類別）；`MapCoords` 只給 TilemapView 用的 `ToTilemapCell`/`FromTilemapCell`；遊戲端 `MapLoader.BuildTiles()` 與 `buildTiles` 旗標；`MapSession` resize 時裁地磚的邏輯；`M0SelfTest` 放地磚的驗證。**素材白名單也一併移除 `Tiles`**——`MapAssetCategories.All` ＋ 主專案 `sync_map_assets.sh` 的 `CATS` ＋ 編輯器 `sync_assets.sh` 的 `copy_flat`，正是 [PROBLEMS.md](PROBLEMS.md) C8 說的「分類要改多處」那條線，這次是反過來走一遍。
  **`.dipanmap` 相容性零風險**：Newtonsoft 預設忽略 JSON 裡多出來的屬性，舊檔的 `"tiles": []` 讀進來直接被跳過，存檔時不再寫出。因為實際 tiles 數是 0，連「資料遺失」的可能性都沒有。
  **預設工具改成「物件」**（原本是 `TilePaint`，拔掉後會指向不存在的列舉值）；離開特效預覽器時原本也是退回 `TilePaint`，改成**退回進來前停的那個工具**（新增 `_toolBeforePreview`）。
  **特效預覽器的關閉鈕**（作者回報「打開後找不到地方關，只能點其他頁籤」）：做了兩個出口——① 頂部那顆按鈕改成可切換，在預覽器裡會顯示「關閉預覽器」；② 預覽器右上角疊一顆「✕ 關閉」（**畫在 `_preview.Draw()` 之後**，IMGUI 後畫的蓋在上面）。兩者都退回進來前的工具，不會像以前那樣被丟回地磚工具。
  **⚠ 被移除的四支檔案搬到 `DipanProj_MapEditor/_to_delete/tile-system/`**——橋接器不允許刪檔，而且**不能留在 `Assets/` 底下**（Unity 照樣會編譯，會因為找不到 `TilePlacement` 而整包編不過），所以搬到 Assets 外面。作者確認後自行刪除該資料夾即可。

* [x] **作弊面板加「取得所有武器」一鍵鈕**（2026-08-07）：測試各種武器手感時要一顆一顆填 ID 太慢，加一顆按鈕把所有武器一次給進背包（背包滿了就停）。放在「給道具」分頁的「一鍵快捷」那一組，跟「獲得 10,000 元」並排。**兩個刻意的決定**：① **來源是物品表而不是武器表**——背包裝的是「物品」，武器表的一列要有對應的物品才拿得到；`WeaponTable.csv` 目前有 20 把，其中 id 14「紅嫁衣召喚家人」是 Boss 專用、沒有給玩家的物品，從物品表這一側列舉才不會給出玩家根本裝不上的東西（實際會拿到 **19** 把）。② **判斷用 `EquipSlot == Weapon` 而不是 `WeaponID > 0`**——劇本道具「劇本-紅嫁衣」(104) 也填了 `WeaponID`（它要指定關卡用的武器），但它不是武器、裝不上武器欄，用 `WeaponID` 判斷會誤給。另外：**已經有的（背包裡或身上穿著的）會跳過**，連按幾次都不會塞出一堆重複的（要重骰孔位請用「鑲嵌」分頁的「重開孔位」）；給的時候走 `ItemManager.Create` 而不是 `AddItem`，武器需要實例資料、孔位要現場骰，直接 `AddItem` 會拿到一把沒有孔的裸裝（見 [GEM_SOCKET.md](GEM_SOCKET.md)）；ID 先收集再排序才給，因為 `Dictionary` 的走訪順序不保證，不排的話每次按背包裡的排列都不一樣。狀態列會分開回報「新給幾把 / 已經有幾把 / 幾把因裝備包已滿放不下」。之後若要讓某把怪物武器也能被玩家拿到，只要在 `ItemTable.csv` 補一列（`EquipSlot=Weapon` ＋ 對應的 `WeaponID`），這顆鈕會自動涵蓋、不用改程式。

* [x] **地面特效兩個新欄位（SigilPath 背景旋轉符號／LightRadius 發光半徑）＋ 佛光視覺實驗三連（最後全部還原）**（2026-08-17，見 [FALLEN_BUDDHA_LIGHT.md](FALLEN_BUDDHA_LIGHT.md)、[GROUND_EFFECT.md](GROUND_EFFECT.md)、[PROBLEMS.md](PROBLEMS.md) E12/E13）：起點是一個視覺抱怨——裝備佛光時畫面上有**兩個同心同色的圓**，外圈是 `ItemTable` 的 `LightRadius=3.5`（AtmosphereController 提燈光圈，暖色 1.00/0.78/0.52），內圈是 GroundEffect 2 的 `Radius=1.2` 光環（貼圖平均色 172/136/79），色相幾乎相同、只差半徑 2.9 倍，看起來像同一件事畫了兩次；**真正的元兇是兩圓之間那段空白暗環**。<br>**試過三個方向**：① 內圈換紫＋改名「墮落佛光」（敘事有據：DramaTalkTable 第 37 列邪佛親口說「吾贈汝佛燈一盞」，燈本來就是邪佛給的）→ ② 空白環補一個緩緩旋轉的卍字（沿用開場墜落的 `Resources/InitialStory/Manji.png`）→ ③ 卍字改 alpha 混合的暗紫剪影、縮進圓內。另外獨立試了 ④ 拿掉照明、只靠佛光那張發光的圖照路。**四項最後全部還原**，`ItemTable`／`WeaponTable`／`buddhaLight_01.png` 現在與 git 完全一致。<br>**留下來的東西**：GroundEffectTable **第 12 欄 `SigilPath`**（在特效的圓上疊一張自轉的符號，與 RenderMode 無關）與 **第 13 欄 `LightRadius`**（特效存在期間掛一顆 `LightSource` 真的照亮暗場景，接在現成的登記表上、`AtmosphereController` 零改動），兩欄目前**全表留空**；紫色貼圖兩版與換色腳本存在 `readme/variants/`（**刻意不放 `Resources/` 底下**，那裡的圖會無條件烘進 build，見 PROBLEMS A9）；怎麼再開回來寫在 [FALLEN_BUDDHA_LIGHT.md](FALLEN_BUDDHA_LIGHT.md)。<br>**三條疊色通則**（已進 PROBLEMS E12/E13）：① **加色圖層的 `_Intensity` 不等於實際亮度**——貼圖自身 alpha 會先乘一刀（佛光圖中心 alpha 只有 0.549、卍字白圖是 1.0，結果 1.4 對 0.85 實際完全打平）；② **兩個都靠「比較亮」被看見的圖層疊同位置是零和的**，調 alpha 永遠無解，要一層發光、一層吃光（暗剪影還必須蓋在光的上面）；③ **加色永遠做不出「不透明」**，要實心就得換 alpha 混合。附帶：加色的紫疊在暖光池上會變粉紅；**別拿自己合成的暗場景估疊色參數**，要拿實機截圖量地板亮度。<br>**一個架構教訓**：卍字第一版寫死在 `RenderMode=Glow` 分支裡，被一句「我之後做無形力場，那個卍字還會出現嗎？」問破——那等於把符號綁在比武器類型低兩層的「渲染模式」上，汙染了 Glow 的語意。改成獨立 CSV 欄位後才乾淨。**「只有一個使用者，先寫死」在這個專案通常是錯的。**

* [x] **血統系統：系列＝三階段（殭屍→毛殭→旱魃）＋ 兩張表 ＋ 逐階進階藥劑**（2026-08-18，見 [BLOODLINE.md](BLOODLINE.md)）：作者做好了四組角色素材（Base/Jiangshi/Maojiang/Hanba，各 idle/walk/dead/attack × 25 幀）與四組對話立繪（各 8 種情緒），要把血統從「一瓶藥換一個外型」升級成「一個系列三個階段」。<br>**先查清楚才動工，發現這其實是擴充不是從零做**：7/28 做祭壇抽選時已經一併把血統做進去了，`BloodlineTable.csv`＋`BloodlineSystem.cs`＋背包左右鍵喝藥＋`ConfirmPopup` 二次確認＋存檔旗標＋廣場的血統祭壇全都在，`SpriteFolder` 也早就接到 `PlayerAnimator` 與 `DramaTalkDatabase`（立繪）——**缺的只有「三張表全填 Base」與「素材沒跑 Sync」**。所以這次的工作量集中在「系列/階段」這個新概念，既有的喝藥管線一行都沒動。<br>**兩張表，隸屬關係只有一個真相**：新開 **表A `BloodlineSeriesTable.csv`**（`SeriesId, Key, DisplayName, Stage1Id, Stage2Id, Stage3Id`）記系列→三階段，載入時順便建「血統 Id → (系列, 第幾階)」的反查索引；**表B `BloodlineTable.csv`** 只回答「這個血統長什麼樣、數值多少」。**表B 刻意不存 SeriesId/Stage**——兩張表都寫就會對不上。血統 Id 慣例是一個系列吃一個十位段（殭屍 10~12），Id 1 保留給人類。<br>**兩種藥劑、兩個互斥欄位**：`ItemTable` 的 `BloodlineID`＝**系列起始藥劑**（決定本世走哪一系列，一世一次不可逆）；新增第 18 欄 `BloodlineUpgrade`＝**進階藥劑的目標階數**（2 中階／3 高階）。關鍵決定是**進階藥劑全系列通用**——它不指定血統、只指定階數，實際變成什麼由表A 決定，所以**日後加新系列不用再做三瓶藥、程式碼一行不用改**。起始藥劑放血統池（300 元不連抽），進階藥劑放**道具池**（血統池賣的是「選一個系列」，進階是後續的成長消耗品，兩件事分開）；高階刻意比中階稀有（權重 3 : 1），因為必須逐階喝，抽到高階卻還在第一階是會卡著的。<br>**規則與體驗**：必須逐階（第 1 階喝高階藥劑會被擋，並直接告訴玩家「需先進階為『毛殭』」）、不能倒退、輪迴歸零回人類（旗標仍在 `progress.flags` 周目層，**存檔格式零改動**）。作者明確要求「**不能喝的時候右鍵當下就要擋下並說明**」，所以拆成 `Plan`（純計算、不改狀態）與 `TryDrink`（執行）：UI 在按鍵當下就拿 `Plan` 決定要 Toast 理由還是跳確認視窗，`InventoryPanel` 因此**完全不懂任何血統規則**，之後改規則不用回頭動 UI。`TryDrink` 內部會**重新 Plan 一次**——確認視窗開著的期間東西可能被搬走或被別的路徑喝掉，不能信任幾秒前的計算結果。<br>**刻意拿掉舊的三個數值欄**（`MaxHpAdd`／`MoveSpeedMul`／`OutgoingDamageBonusPercent`）：作者說那是屬性系統還沒有時取的概念，這次加的五屬性（行走速度/力量/敏捷/魔力/體力）也一樣是概念、**只存不套用**。兩套並存會在真正的屬性系統做好時打架，所以只留一套。**現階段換血統只有外型與立繪會變、戰力完全不變，這是預期行為**，已在表頭註解、程式註解與文件三處寫死免得日後被當成 bug。順帶解掉一個既有 bug：`ReviveFull()` 會呼叫 `CombatStats.Init()` 把最大生命打回 Inspector 基礎值，而 `BloodlineSystem` 因為 `_appliedId` 沒變不會重套 → **死一次回廣場後血統的 HP 修正就消失**；現在血統不碰數值，這個坑自然不存在（未來接屬性系統時要記得）。<br>**變身演出先留呼叫點**：拍板的表演是「天上打下閃電 → 煙霧籠罩 → 散開時已換外型」，特效之後才做，但 `BloodlineTransformFx.Play(pc, from, to, onSwap, onFinished)` 現在就放好了——**換裝的時機必須夾在演出中間**（煙霧最濃那一幀才換圖），事後才插這個縫會需要回頭動 `BloodlineSystem` 的流程。目前是空實作（立刻 swap、立刻 finish），整條流程現在就能測；演出期間 `_transforming` 會停止收斂，另有 10 秒保險絲避免特效漏叫 `onFinished` 時血統永遠卡在舊外型。<br>**⚠ 需要在 Unity 手動做的兩件事**：① 把 `BloodlineSeriesTable.csv` 拖進 GameManagers 上 `GachaTableProvider` 新增的「血統系列表」欄；② **跑 `Project Tools → Sync Map Assets`**——序列圖與立繪都走 catalog，沒同步的話執行期一張都載不到，角色會變成只剩影子。<br>**收工前跑了一輪獨立複查，修掉三件事**：① 進階藥劑的 icon 其實已經畫好了、只是叫 `bloodline_lvup_middle`／`bloodline_lvup_high`，CSV 原本指向臆測的檔名（會靜默變成空白格）；② **舊存檔救生艇**——存的血統 Id 在表B 找不到時（舊角色留著已刪除的野魂 2／幽靈 3）一律當成未定型，否則會「已定型成一個不存在的血統」：起始藥劑被擋且訊息自相矛盾（「你的血脈已定為『人類』」）、進階藥劑也被擋（找不到所屬系列），本世血統徹底卡死只能靠輪迴；③ `TryDrink` 的 `out` 參數改成**成功與失敗都填訊息**，因為 UI 若自己記住確認視窗開啟前算的 DoneText，在期間狀態改變時會顯示過期文案。另補了一則「表A 階段填出缺口」的載入期警告（會靜默讓後面幾階永遠升不上去）。<br>**刻意沒做**：五屬性的實際效果、角色資訊面板（玩家目前沒地方看自己是什麼血統第幾階）、閃電煙霧特效、技能（`SkillId` 仍是死欄）。**一個記下來的節奏隱憂**：輪迴不保留血統 + 三階段各要一瓶藥 ⇒ 每周目都要重新從人類爬到旱魃，而一周目只玩 7 關，三瓶湊不齊的話旱魃實質上看不到；關卡池只有一關時調了也沒意義，等內容多起來再決定調權重還是改成關卡獎勵。

* [x] **血統變身演出（倒下 → 天雷 → 煙霧與電弧 →（煙裡換裝）→ 爬起）＋ 四個順手補上的通用能力**（2026-08-18，見 [BLOODLINE.md](BLOODLINE.md) §5、[PROBLEMS.md](PROBLEMS.md) D13/D14）：作者篩了三個特效包丟進 `DipanProj_Main/血統特效/`（刻意放 Assets 外面），指定演出流程「先播 dead 倒下 → 畫面外打下閃電 → 煙塵蓋住玩家＋電弧環繞 → 煙裡換模組 → 煙散後倒播 dead 爬起」，另外選了螢幕震動＋白閃當擊中衝擊、電弧留一小段殘電、煙塵用單顆放大。<br>**素材比包名少很多但剛好夠**：三個包只有 5 個動畫序列（作者已篩過，每個資料夾只留 yellow_orig）。閃電是 `start`(2)/`loop`(8)/`end`(2)、64×128 tileable——**與既有九霄雷獄的 `SkyLightningColumn` 結構完全吻合**（同尺寸、同張數、同兩位補零），所以沒有另寫一套：把 `SegmentedLightningColumn` 加上可指定素材路徑的 `Style`（快取改成以路徑為鍵），舊簽章保留委派，武器行為零改變。**`end` 刻意不用**——比對 md5 發現它就是 `start` 倒過來的同兩張圖，而且是「快消散的細電光」，既有雷柱的註解早就寫過「end 會突然收細」，照它的做法讓 loop 一路延伸到擊中點。<br>**四個順手補上的通用能力**（都不是血統專用，之後別的地方可以直接用）：① `PlayerAnimator.PlayFallDown(onDone, fpsMul)`——`PlayWakeUp` 的鏡像，正播 dead 且**播完轉成趴地定格**而不是回 Idle（`SetState(Dead)` 做不到這件事，會被 PlayerController 每幀的 HandleVisuals 塞回 Idle 蓋掉，只有真死才 return 在它之前）；② `MapCameraController.AddShake(秒, 振幅)`——**必須做在這支元件裡**，相機位置每幀由它獨佔寫入，另外掛震屏元件會互相蓋掉（誰先跑取決於 Script Execution Order，是抓不到的隨機 bug），而且偏移要疊在 `SmoothDamp` **之後**，先疊會被平滑吃掉變成軟軟地飄一下；③ `ScreenFader.Flash(色, 進, 退)`——與黑幕分開的獨立圖層、永不擋點擊；④ `UIManager.SetExternalHold(owner, …)` 具名輸入鎖。<br>**兩個真的會壞掉的坑（複查抓到、已修）**：**D13 輸入鎖互踩**——`SetExternalHold` 舊版是單一組布林、沒有持有者概念，演出中玩家被打死時死亡流程也掛了鎖，先結束的那一方會把對方的鎖一起清掉（玩家在結算等待期間又能走能打）；改成 `Dictionary<持有者, (block,pause)>`，舊多載用共用預設 key 所以既有呼叫端行為完全不變。**D14 演出被暫停凍住**——演出期間玩家按 `B` 開背包（`PausesGame=true`）→ timeScale 歸零，而玩家動畫／VfxInstance／雷柱**全部吃 `Time.deltaTime`** → 整段凍在半空中，最後由 `BloodlineSystem` 的保險絲（用 unscaled）先到期、外型直接 pop 出來；解法是演出期間 `StorageBagCoordinator` 查 `BloodlineTransformFxRunner.IsPlaying` 擋掉三個熱鍵（**不能改查 `IsGameplayInputBlocked`**，背包開著時它本來就是 true，那樣 `B` 會關不掉背包）。<br>**另一個隱形坑**：換裝會把趴姿打回站姿——`SetBloodline` 內部重跑 `PlayerAnimator.Setup`，sprite 被換成新血統的 idle 第 0 幀，但趴地定格旗標還在、Update 直接 return 不再更新 → 角色**站著定格**。所以補了 `RefreshLyingPose()`，`onSwap` 之後必叫。同理補了 `CancelPose()`：演出被外力打斷若不解趴姿，`IsWakeUpBusy` 恆真、`SetState` 全被忽略，角色永遠定格而且沒有任何錯誤訊息。<br>**保險絲三層**：`WaitPose` 等表演結束有三個出口（完成／`IsWakeUpBusy` 變 false＝被打斷／逾時 6 秒，**逾時計時用 unscaled**，否則 timeScale=0 時連保險絲都凍住）；`BloodlineSystem.TransformTimeout` 10→20 秒；`IsPlaying` 是 static 已註冊 `PlayModeStaticReset`（殘留會讓下一次 Play 的背包熱鍵全部按不出來）。<br>**刻意沒做**：音效（專案還沒有音訊系統——雷擊與煙爆是這遊戲裡最該有聲音的兩個瞬間，音訊系統做好後第一個補這裡）。節奏與外觀常數全部集中在 `BloodlineTransformFxRunner` 檔頭；煙塵單顆放大會有點糊（64px 像素圖放大的必然），想改成「沿身體撒 3~4 顆錯開時間」只要把 `SmokeBurstCount` 調成 3、`SmokeHeightRatio` 調回 0.9，流程一行不用改。

* [x] **血統表新增體型倍率 `BodyScale` ＋ 變身特效隨體型縮放 ＋ 雷擊點改成劈腳底**（2026-08-18，見 [BLOODLINE.md](BLOODLINE.md) §2、§5）：作者實機跑完三階段後回報「第一階殭屍反而最大，二三階都小小的看不清楚」，另外「閃電好像劈到肩膀，希望劈到腳的位置」。<br>**先量了才發現不是高度問題**：`PlayerAnimator.Setup` 本來就會依 idle 的可見像素高度把每個血統正規化到同一個世界高度，四組素材量出來的可見高也確實差不多（Base 193px／殭屍 174／毛殭 197／旱魃 175）。真正的落差在**可見寬與姿勢**——殭屍是寬站姿的駝背剪影（可見寬 65 但輪廓張得開），毛殭 106、旱魃 91 都是挺直的瘦長站姿，**同樣高度下後兩者看起來就是小一號**，加上暗地板上對比低更難讀。結論是這不該用「再算一次正規化」解，而是給一個**用眼睛校正的旋鈕**：表B 新增第 5 欄 `BodyScale`（以 Base 為 1，目前殭屍 1／毛殭 1.5／旱魃 1.2），乘進 `CharacterWorldHeight` 再交給 `Setup`。**純視覺**——不動碰撞框、不動任何數值（1.5 倍體型的角色打起來跟 1 倍一樣大，這點刻意保留，動 hitbox 會改手感）。<br>**接線**：`PlayerController.SetBloodline(folder, bodyScale)` 加第二個參數（有預設值，既有呼叫端不受影響）＋ `ScaledCharacterHeight` 屬性；`BloodlineSystem.ApplyTo` 的「要不要重跑 Setup」判斷**同時比對外型資料夾與體型倍率**（只比資料夾名的話，調完 CSV 重新載入不會生效）。順手補了 `BlobShadow.Refresh()`——影子本來只在 Start 量一次，換成 1.5 倍體型後腳下會頂著一塊明顯偏小的影子。<br>**特效隨體型縮放**：`BloodlineTransformFx.Play` 的簽章從「兩個資料夾名字串」改成「兩個 `BloodlineDef`」，因為它需要體型倍率。煙霧／電弧的覆蓋高度取 `max(目前實際畫出來的高度, 站立高度 × 倍率)`，倍率則取**變身前後較大的那一個**——只用變身前的話，換成更大的血統時煙霧散開前那一段會露出新外型的頭尾；雷柱粗細也乘同一個倍率。<br>**雷擊點改成腳底**：原本用 `_pc.transform.position`，但玩家的 sprite 是以 transform 為**中心**畫的，所以電柱底端停在胸口／肩膀高度（就是作者看到的症狀）。改成取 `SpriteRenderer.bounds` 的 `center.x` / `min.y`＝可見圖的底部中心＝角色腳下站的位置；煙霧與電弧則改對齊 `bounds.center`（身體可見中心），原本用 transform 會整體偏上。

* [x] **體型倍率連動：佛光圈跟著身體、放大改成腳底錨點、特效改用「可見身體幾何」對位**（2026-08-18，見 [BLOODLINE.md](BLOODLINE.md) §2、[GROUND_EFFECT.md](GROUND_EFFECT.md)、[PROBLEMS.md](PROBLEMS.md) E14）：作者回報「一階進二階後開佛光，光圈還是一階的大小，二階大不少所以很明顯」，並指出「很多特效是根據身體大小播的，體型動態改變後都要跟著改」。<br>**根因不是快取沒更新，是它從來沒看過身體**：佛光其實是兩個圈——內圈 `GroundEffectTable` id 2 的 `Radius=1.2`（**有傷害**）、外圈 `ItemTable` id 8 的 `LightRadius=3.5`（純照明）——**兩個都是寫死的世界半徑**。一階身高 1.95 對直徑 2.4 剛好「籠罩己身」，二階身高變 2.92 而圈不變，光暈就比身體還窄、縮在肚子上。<br>**三個決定**（都問過作者）：① 佛光圈跟著體型放大，**視覺與傷害一起**（作者：「看到的就是打得到的」）；② 提燈照明**不跟**（燈就是燈，不因拿燈的人變大就照更遠）；③ 放大改成**以可見腳底為錨點往上長**。<br>**① 半徑倍率**：`GroundEffectInstance` 加 per-instance `_radiusScale` 與 `Radius` 屬性，所有讀半徑的地方（鋪面、單圖縮放、符號、傷害 OverlapCircle、Gizmo）全部改走它。⚠ 刻意不就地改 `_data.Radius`——那是表格的一列、**全遊戲共用同一個物件**（同 RecipeTable 共用配方的坑）。`Manager.Spawn` 加第 5 個參數 `radiusScale`（與既有的 `visualScale` 分開：後者只放大視覺、傷害仍是原半徑，畫面會騙人）。另加 `SetRadiusScale` 支援中途改，`RebuildVisuals` 刻意排除 `BuildLight`（它是 AddComponent 到自己身上，再叫一次會變兩盞燈）。<br>**③ 腳底錨點**：依體型倒推 sprite pivot `pivotY = fy − (fy − 0.5) / BodyScale`。**BodyScale=1 時剛好回到 0.5**＝原本的置中 pivot，而且 `ApplyFootPivot` 在倍率為 1 時直接 return 原陣列、連 Sprite 都不重建，所以不放大的血統是位元級零影響。實測驗算：毛殭在 1.0 與 1.5 倍下「可見腳底相對 transform 的位移」都是 −0.9997，完全不動、只往上長。刻意**不改 `MapSpriteLoader` 的預設 pivot**（那支是怪物/地上物/背景共用的），改在 `PlayerSpriteLibrary` 就地用 `Sprite.Create` 重建，只影響玩家。附帶好處：`YSortByFeet` 那套「用 transform.y 當腳底代理」的假設在任何體型下仍成立，遮蔽關係不會跑掉。<br>**② 可見身體幾何**：`PlayerAnimator` 在 `Setup` 時把各動作的「可見高」與「可見腳底相對 transform 的位移」**從縮放參數解析算好**存起來，`PlayerController` 對外提供 `VisibleBodyHeight` / `FeetWorldPos` / `BodyCenterWorldPos`。**刻意不讀 `SpriteRenderer.bounds`**——含不含四周透明留白取決於 sprite 的 mesh 型別，在「執行期 `Sprite.Create`」這條管線上沒有保證。佛光、集氣光圈、喝藥特效、變身演出的雷擊點與煙霧全部改用這組。<br>**複查抓到並修掉的三件事**：① 集氣光圈與喝藥特效**大小**有跟（讀 bounds）但**位置**沒跟（釘在 transform），二階會沉 0.46 世界單位到小腿；② **擊退距離被體型帶著跑**——它是「角色圖寬 × 百分比」算的，圖變大就退更遠，1.5 倍體型會被擊退 1.5 倍遠，這違反「體型是純視覺」的約定，補了 `HitReactionHandler.WidthScaleCompensation` 除回去；③ `VisibleBottomFraction` 原本隱含「畫布必為 256px」的假設（目前 400 張角色圖剛好全是 256 所以算對），改成從 `LocalBox` 新增的 `canvas` 欄位取真實畫布尺寸，免得日後有人丟一張 512px 的進來**靜默算錯**。另修 `RebuildVisuals` 每次重建漏一顆 Glow Material、倒下分支對 `_dead` 沒防呆、兩處重複的 `<summary>`。<br>**幾何驗算**（Python 直接讀 PNG 的 alpha bbox 重跑一遍公式）：三個血統在各自倍率下「佛光圓心高於腳底 ÷ 半徑」都等於 **0.8125**——光環相對身體的幾何在任何體型下完全等比，而且圓下緣一律落在腳底之下，貼腳邊的怪打得到。

* [x] **文件收尾：血統三大塊的周邊文件同步 ＋ 修掉 10 處「現在寫錯」**（2026-08-18）：血統系統、變身演出、體型倍率是分好幾輪做的，主題文件（BLOODLINE.md）邊做邊寫，但**周邊的既有文件沒跟上**，而且有幾處變成「寫錯」而不只是「沒寫」——後者比前者危險，照著做會寫回已經修掉的 bug。<br>**修掉的錯（依危險度）**：① `BLOODLINE.md` §5 自己還在教人用 `SpriteRenderer.bounds` 取雷擊點，與同一份文件的 §2 和 PROBLEMS E14 直接矛盾（那是中途版本，後來被 `FeetWorldPos`/`BodyCenterWorldPos` 取代）；② `CHARACTER_SETUP.md` 寫 `SetBloodline("Vampire")` 單參數——**有預設值所以不會編譯錯，但會把 BodyScale 靜默重設成 1**，換血統後體型跑掉、影子與佛光圈一起錯，這種「不報錯只默默錯」最該優先修；③ `INVENTORY.md` 的 ItemTable 欄位表**漏掉第 18 欄 `BloodlineUpgrade`**（欄位表漏一欄＝照它加欄的人會欄位錯位），道具列還停在已刪除的 301 野魂/302 幽靈，互動流程三處都錯（判斷條件、流程、「換外型＋套屬性」）；④ `SHADOW.md` 還寫著「大小只在 Start 算一次…需要的話可改成每幀更新」，`Refresh()` 早就做好了，留著會讓下一個人重造一輪；⑤ `MAP_ENTER_EFFECT.md` 與 `MosaicController.cs` 註解都還說「SetExternalHold 是布林非計數」；⑥ `GACHA_SYSTEM.md` §5.1/§5.3 的欄位清單與「套用內容：移速倍率、最大生命加減、傷害加成」全部過時（頂部橫幅有聲明但沒點名這兩節，而讀者常只跳讀小節）。<br>**補上的缺口**：`CHARACTER_SETUP.md` 新增「顯示高度與體型倍率」＋dead 幀一圖三用的五支 API（並把「順播倒下、倒播爬起」從**建議升格成硬需求**——現在真的被當倒下動畫用了）；`ACTORS_AND_COMBAT.md` 補玩家的外型/幾何/`IsDead`/`RefreshBodyScaledVisuals` 與受擊反應表的 `WidthScaleCompensation`；`UI_SYSTEM.md` **整份文件原本沒出現過 `SetExternalHold`**，補上具名持有者一節（並註明**目前只有變身演出用了具名版，其他七支仍共用預設 key**，不寫的話會被誤以為互踩問題已全面解決）；`MAP_SYSTEM.md` 新增「2.3 螢幕震動」；`TITLE_AND_SAVE_UI.md` 補 `ScreenFader.Flash`；`LASER.md` 補雷柱已通用化；`MONSTER_SETUP.md` 補 `LocalBox.canvas`；`STORAGE.md`/`FORGING.md` 補熱鍵的兩種鎖；`CHARGE_MODE.md` 補位置改對齊身體中心。<br>**收尾檢查**：全 readme 的 `.md` 連結**零斷連**、被引用的 49 個 PROBLEMS 編號**全部存在**、四類過時字串（布林非計數／套屬性／野魂幽靈／SpriteFolder 全是 Base）掃過確認只剩「更正說明」本身。README 文件地圖的 BLOODLINE 那一列也補上——原本看不出變身演出已完成，也看不出**它同時是玩家可見身體幾何 API 的正典**（要定位特效的人不會想到去開血統文件）。

* [x] **變身雷柱改成純 loop（拿掉頂端雷首）**（2026-08-18）：作者實機看了說「start 的動畫跟 loop 接不太上」。量素材後確認有**兩個獨立原因**：① 雷首的不透明像素只有 193/428、**接縫處邊緣寬度 1~2px**，而 loop 是 1262~2039、邊緣 **5~17px**——一根髮絲頂著一根粗電柱；② `capFrame = floor(elapsed × fps × 0.35)`，雷首只有 2 張、約 0.15 秒就播到底然後**整段凍住**，底下 loop 卻在跑 8 幀循環，等於靜止的頭配閃爍的身體。<br>**改法**：`SegmentedLightningColumn.Style` 的雷首前綴**允許留空**（`HasCap`），留空時整根都用 loop。loop 本身上下貫穿可平鋪，純 loop 疊起來零接縫；柱頂本來就延伸到視窗上緣再往上 12%，所以也不會看到「斷頭」。**九霄雷獄維持接雷首、行為零改變**（它節奏短、雷首多半落在畫面外，作者沒反映過問題，不主動動它）。雷首與 end 的素材都留在 `Resources/VfxEffects/TransformLightning/Start/`，想試回來只要把 `LightningStyle` 的第一個參數填回路徑、第三個填 2。<br>**通則**：拼接式特效（頭/身/尾）接不接得起來，看的是**接縫處的邊緣寬度是否相近**，不是「有沒有這張圖」。素材包給了三段不代表三段都該用——先量再接。

* [x] **修「表演層被地上物蓋住」——Y 排序帶不再靠 16-bit 繞回**（2026-08-18，見 [PROBLEMS.md](PROBLEMS.md) **E15**）：作者回報「在紅嫁衣關卡變身，落雷竟然被地上物遮蔽，一部分電柱從地上物底下穿過」。查下去發現**這不是落雷的問題，是既有的系統性 bug**——落雷只是第一個「跨越整個畫面、一定會跟高處物件重疊」的效果，所以最先被看見。<br>**根因**：`MapDepthSort` 的舊公式 `1000000 + zOrder*10000 + round(-Y*100)` **靠 16-bit 溢位繞回**落到 +16960 那一帶（見 PROBLEMS E4）。`zOrder = 0` 時安全（11960~21960，剛好在表演層 22000 之下），但 **`zOrder = 1` 把整帶往上平移 10000 → 21960~31960，直接騎到所有表演層頭上**。而 `zOrder=1` 的正當用途就是「整層往前」：桌上的花瓶、供桌上的香爐/供盤/燭台、屏風——紅嫁衣的祠堂、書房、柴房、客廳各有幾個，實測 sortingOrder 到 **27131~27409**。於是**掉落物名稱標籤(20000)、雷柱與大部分特效(22000)、變身電弧/煙塵(22050/22100)、傳送門與傷害數字(24000)、煙火與離場卍字(25000) 六個表演層全部被一個燭台蓋住**，只有場景火雨(30000) 倖免。E4 當年那句「地上物用 1000000 繞回後剛好沒事」其實只是還沒踩到 zOrder≠0 而已，已回頭補正。<br>**解法**：`MapDepthSort` 改成**完全不繞回**——低基底 `SortBase=7000` ＋ `BandStep=6000` ＋ 把 Y 的貢獻夾在 `[0, BandStep-1]`。世界帶因此固定在 **1000~18999**，全部表演層（20000 起）都在它之上。夾住 Y 還順便讓「zOrder 大的一定在前面」從「地圖高度 < 100 單位才成立」升級成硬保證。`zOrder` 限定 ±1，超出會夾住並印一次警告（放任往上跑就是重演這個 bug）。<br>**驗算過再改**：把 386 個既有地上物**兩兩比較**，新舊公式的相對順序**完全一致**（世界內的遮蔽關係零改變）；低位固定層（背景 -1000、可走地上物 5、地面特效 8、星星 20）仍在世界帶之下。⚠ `DipanProj_MapEditor` 的 `ObjectView.cs` 有同一條公式的**鏡像**，已一起改。<br>**順手做的事**：把**全遊戲的排序層配置表**寫進 `MapDepthSort.cs` 檔頭（從背景 -1000 到 16-bit 上限 32767 逐層列出），以後要加新的固定層先看那張表確認落點，不必再翻五個檔案湊。<br>**通則**：**排序值的「帶」要當成資源來規劃，不能靠繞回碰運氣。** 「繞回後剛好落在對的地方」不是安全，是還沒被觸發的地雷。

* [x] **變身表演改成全程暫停 ＋ 新增「血統揭示」立繪面板**（2026-08-19，見 [BLOODLINE.md](BLOODLINE.md) §5、[PROBLEMS.md](PROBLEMS.md) **D15**/**D16**）：作者問「喝血統的整個過程遊戲是暫停的嗎」，查下去答案是**不是**——演出期間只鎖輸入、`pause` 傳 `false`，所以怪物照打，玩家可能在被鎖住不能閃避的 6 秒裡被打死。作者提了兩個方案（① 暫停播放 ② 限定只能在邪佛廣場喝），評估後選 ①，因為它同時解掉「表演與後面的 UI 面板之間有接縫」這件事。<br>**改成暫停的代價是整條鏈的計時器都要換**：`timeScale=0` 之後 `Time.deltaTime` 恆為 0、`WaitForSeconds` 永不到期，所以玩家姿勢動畫（`PlayerAnimator.UnscaledPose`）、煙塵與電弧（`VfxInstance.Unscaled`）、雷柱（`SegmentedLightningColumn.Unscaled`）、演出協程的 `Wait()` 全部得改。三個旗標**預設都是 `false`**，`Spawn` 系列本來就回傳實體，所以**一個函式簽章都沒動**、一般戰鬥特效行為零改變。螢幕震動與白閃當初就寫成 unscaled，不用碰——這正好說明「若某個元件已經是 unscaled，多半是前人踩過同一個坑」。<br>**新面板 `BloodlineIntroPanel`**（照 `BossIntroPanel` 的樣板）：壓黑遮罩 ＋ 破碎框底版，框內先放**變身前**的血統立繪，1 秒後斑駁剝落、新血統從破口浮現，姓名底版從下方飄入、血統名扭曲成形，停 1 秒後淡出。**資料來源全部是既有管線**——立繪走 `DramaTalkDatabase.ResolvePortrait("Actor_normal", 血統資料夾)`（Talk 立繪同一條 catalog，四個血統早就在 `catalog.json` 裡，零新載圖程式）、姓名底版沿用 boss 開戰資訊那張、毛筆字型也是同一支。唯一真的新寫的是 **`Resources/Shaders/BloodlineDissolve.shader`**：uGUI 材質、hash 值噪兩個八度（粗塊決定哪片先掉、細粒讓邊緣毛躁）＋ 暗紅燒蝕邊，**不吃任何貼圖**；單一 `_Cutoff` 參數同時做正反兩向，所以「舊的剝落」與「新的浮現」共用同一支、只是各掛一份材質推不同數字。載不到著色器會退化成 alpha 淡入淡出，節奏一模一樣。<br>**橫跨兩段的鎖**：世界演出的 `finally` 是先解鎖再回呼，面板又要延一幀才開得起來（避開 `OnClose` 重入，PROBLEMS D8），中間那一兩幀沒人壓著就會解除暫停一瞬間。所以 `BloodlineSystem` 自己掛一份具名 hold `"BloodlinePerformance"` 從喝下去壓到面板淡出結束，兩段各自的鎖照舊不動（`Recompute` 是 OR）。<br>**複查抓到並修掉的五件事**：① **D16**——`UIPanel.DoClose()` 是「先叫 `OnClose` 再開始淡出」，把解鎖掛在 `OnClose` 上等於**淡出才剛開始就把玩家丟回戰場**，畫面還壓著八成不透明度的遮罩、怪已經在打他了；改成面板自己在 `Update` 裡淡到全透明才 Close（`FadeDuration` 收尾階段回 0）。② 順帶把 `IsShowing` 延到回呼真正放行那一刻才清，否則「熱鍵解鎖」會比「解除暫停」早一幀。③ `ESC` 是個沒人注意到的洞——Overlay 演出面板**不入堆疊**，`TopStackPanel()` 看不到它，ESC 會落進「沒視窗就開設定面板」那個分支；`UIManager` 那一支加了 `!_inputBlocked`，順便也保護了 `BossIntroPanel` 與所有過場。④ 姓名底版圖若載不到，牌匾停在畫面外**但名字（它的子物件）照樣淡入**，會有一行血統名孤零零飄在螢幕底下；補了後備圖並讓飄入無條件執行。⑤ `VfxInstance.FlashRoutine` 還留著 `WaitForSeconds`，暫停中會讓白光定格在全白且 `_flashCo` 永遠不歸 null（血統鏈碰不到，但同一個檔剛為了暫停而改，留著就是下一個坑）。<br>**熱鍵封鎖的單一真相**改成 `BloodlineSystem.IsPerforming`（＝世界演出 ∪ 立繪面板）；`TransformTimeout` 20→30 秒，而且保險絲現在**連 external hold 一起放**——漏放的症狀是玩家整場不能動且沒有任何錯誤訊息。<br>**沒做方案 ②**（限邪佛廣場）：一行判斷就能做，而且三種藥劑目前**只從廣場祭壇抽得到**、對玩家零摩擦，但它是個設計決定不是安全修補，先留著。<br>**已知落差**：`Talk/Base/normal.png` 是 1122×1402、其他三張是 1024×1536，面板用「等比縮到框內、靠下對齊」吸收，但第一次喝藥那一幕人類會比殭屍小一圈；重畫成 1024×1536 即可完全對齊。<br>**後續（同日）**：作者換了專屬的姓名石碑 `BloodlinePanel_NameBg`（866×288 淺色）並加了頂端標題 `BloodlinePanel_Title`（866×288，同比例）。換淺色石碑連帶要改字色——原本抄自 `BossIntroPanel` 的暖金色在淺底上會完全看不見，改成深血紅並把 `NameColor` 從 `static readonly` 提升成 Inspector 可調欄位，連「石碑載不到時的後備純色底」也從半透明黑改成淺石色。標題與立繪**在搶同一塊垂直空間**（立繪頭頂 150 vs 標題底邊 140，只留 10 的餘裕），所以立繪可用區與底邊距離跟著重算過。全部版面數字都是拿真實素材用 Python 合成驗證過才填的，不是目測。

* [x] **「使用道具」收成唯一入口，並立下「左鍵搬移／右鍵使用」的全遊戲鐵則**（2026-08-19，見 [INVENTORY.md](INVENTORY.md) 的「左鍵 vs 右鍵」、[PROBLEMS.md](PROBLEMS.md) **D17**）：作者回報「我原本的要求是右鍵使用物品，但左鍵也能使用」。查下去發現「使用」這件事**根本沒有唯一入口**，散在三處各寫一份——背包左鍵、背包右鍵（兩者呼叫同一個私有方法，還互相標註「行為刻意一致」）、以及 `PotionHotkeys` 自己寫的一整套消耗邏輯。最糟的一條是**左鍵點血統藥劑會直接喝掉**，而血統本世不可逆，等於誤點一次就定終身。<br>**解法**：新增非 UI 的 `Inventory/ItemUse.cs` 當唯一入口，照 `BloodlineSystem.Plan`／`TryDrink` 的樣板拆成 `PlanUse`（純計算：能不能用／理由／要不要先跳確認視窗）＋ `TryUse`（真的用，成功失敗都填 message）。背包右鍵、藥水熱鍵 1／2 全部走它，加新的可用道具只要在 `IsUsable`／`PlanUse`／`TryUse` 各補一個分支。<br>**規則**：**左鍵＝搬移／裝備／綁定，永遠不消耗；右鍵＝使用，這是唯一會消耗道具的滑鼠操作。** 左鍵那條 if/else 階梯裡從此不准出現會消耗東西的分支；左鍵點一件「只能使用」的東西＝**安靜地什麼都不做**（作者選的；刻意不 Toast「請按右鍵」，那條規則只需要學一次）。<br>**行為變更**：右鍵回血/回魔藥劑從「放進快捷格」改成**當場喝掉**（作者拍板）；綁定快捷格改由左鍵與拖曳負責，所以新手教學那一步不用動。<br>**三個把關點一起補**，不然規則會在別處漏水：① `InventorySlotWidget` 左右鍵**分別列舉**（原本 `else` 會讓中鍵/側鍵被當左鍵）；② 倉庫的 `ItemSlotWidget` 原本**完全不判斷按鍵**，右鍵也會搬——改成只收左鍵（倉庫裡的東西刻意不能直接使用，要先拿回背包）；③ `SlotDragController.Begin` 只允許左鍵開始拖曳，否則右鍵按住稍微移動就變搬移、原地放開卻是使用，同一個手勢差幾像素兩種結果。<br>**複查抓到並修掉的四件事**：① **喝藥特效會凍住並疊加**——右鍵喝的時候背包開著、`timeScale=0`，而 `PlayDrinkPotionVfx` 生的 `VfxInstance` 沒設 `Unscaled`，連按五下會得到五個定格在第 0 幀、永遠不消失的特效疊在玩家身上，關掉背包才一起播完（正是前一天 PROBLEMS D15 那一家）；② **舊的消耗順序是「先扣瓶再套效果」**，玩家死了或還沒生出來時 `Heal` 直接 return 但瓶子照扣 → 「藥沒了、血沒回、也沒訊息」，改成先確認有活著的 `CombatStats` 再扣；③ 搬移時漏了 `PotionHotkeys` 原本「綁定的道具已從 ItemTable 刪列（`GetData==null`）就清空該格」的清理，不補的話那一格會永遠綁著一個鬼魂、按 1 完全沒反應；④ 熱鍵這條路**跳不出確認視窗**，所以加了「凡是 `PlanUse` 說要確認的東西一律不從熱鍵用」——今天踩不到，但只要有人把血統藥劑誤標成 `Category=Potion`，按一下數字鍵就會不可逆地決定本世血統、連問都不問。<br>**新手教學的依賴**：教學那一步教「左鍵把藥水放進快捷格」，玩家若右鍵把唯一一瓶喝掉會卡在等一個永遠不會發生的條件，所以 `OnSlotRightClicked` 開頭擋 `TutorialManager.HardLock`（該旗標在「開背包 → 點藥水格 → 關背包」整段都是 true，正好蓋住這個窗口）。<br>**通則**：**「兩邊行為刻意一致」是要重構的訊號，不是可以寫在註解裡的設計。** 這次的分岔還不是「行為不同」，而是「兩邊都能做一件本來只該有一個入口的事」——功能看起來完全正常，只是多了一條沒人打算開的門，更難發現。

* [x] **地上物碰撞改成「貼合圖形」——透明處不再擋路**（2026-08-19，見 [PROBLEMS.md](PROBLEMS.md) **B9**、[MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)）：作者回報紅嫁衣書房裡屏風旁邊「看起來明明可以走，卻被卡住」，而且「素材的邊已經切到不能再切，還是會留下一塊簍空處被判定為不可走」。<br>**根因有兩層**：① **地上物擋路靠的是自己身上的 Collider，跟可走層完全無關**——可走層只生成牆/水碰撞與 A\* 尋徑格，所以在可走層把那幾格塗回可走是沒有效果的（這點連作者都不知道，因為**編輯器端完全看不到遊戲會生成什麼碰撞**，掃圖那段程式只存在主遊戲）；② 那顆碰撞是「整張圖不透明像素的**外接矩形**」，而外接矩形只由最外圍那一個像素決定，**只能縮框、不能挖洞**，所以切邊永遠救不了。量了那張屏風：355×483、外接框 341×463，**框內只有 58.9% 是不透明的**，剩下 41% 是空的卻照擋；乘上擺放縮放 1.435 就是一顆 1.91×2.60 格的方塊，正好蓋住作者卡住的那一塊。<br>**解法**：新增 `ObjectFootprint` / `FootprintMask`——把素材切成子格逐格判斷「這格有沒有畫東西」，同一列連續格併成一條 box，全部 `usedByComposite` 交給 `CompositeCollider2D` 合併成單一外框（**與牆同一套做法**）。一格要有 25% 以上的像素不透明才算擋：用「只要有一個不透明像素就擋」的話，AI 去背素材邊緣那圈半透明反鋸齒會讓每個物件四周多出約 0.125 格的假邊（屏風 114→103 格、燈籠 62→41 格）。<br>**照作者的提議烘進 catalog**（原本打算 runtime 算）：遮罩只跟「那張圖長什麼樣」有關，與擺放和地圖無關——因為素材一律以 `PPU=256/tileSize` 載入，一張 w 像素的圖恆為 `w/256` 格寬，**與該地圖的 tileSize 無關**——所以可以在 `Sync Map Assets` 時算好寫進 `catalog.json`（只烘 Environment，動畫物件取第一幀；Monsters/Characters 刻意不烘）。烘在較細的 subdiv 8、遊戲端預設用 4 並自動降取樣，**改解析度不必重跑同步**。<br>**退路一定要留**：catalog 有四個產生器，其中兩支 shell 版不會烘遮罩，所以 `MapSpriteLoader.GetFootprint` 在拿不到遮罩時**當場掃一次**。兩條路都呼叫同一支 `ObjectFootprint.Scan`，否則會變成「同一個物件在有烘/沒烘的機器上擋路範圍不一樣」這種極難查的坑。<br>**三個一起補的防呆**（都是「多顆碰撞」帶出來的，漏掉全是靜默壞掉）：① 碰撞**建在物件本身、不開子物件**——命中判定有 `GetComponent` 與 `GetComponentInParent` 兩種寫法並存，掛子物件會讓前者找不到 `DestructibleObject` ⇒ 打不壞；② 用 Composite 而不是一堆裸方框，否則圓形玩家貼著滑動會在內部接縫拿到一瞬間的斜法線而卡住；③ `MapObjectRevealer` 從收一顆 `Collider2D` 改成收 `Collider2D[]`，不然「靠旗標中途現身」的物件會變成「還沒現身、路已經被擋」。<br>**可破壞物與其他物件分流**：全專案 376 個擺放中 335 個不可破壞、23 個可破壞、18 個不擋路。碰撞一律各自掛在自己的物件上，所以打壞一個花瓶只是 `Destroy(gameObject)`、不牽動別人，與原本行為完全一致。<br>**近乎實心的圖走單框**：遮罩填滿率 ≥ `objectSolidFillThreshold`（預設 0.9）時仍用單一方框——形狀本來就差不多，省一顆 Composite。實測 subdiv 4：書架 100%、椅子 92%（走單框），書架3 86%、教徒 87%、燈籠 71%、書桌 69%、屏風 67%（走貼合），每個物件約 5~8 條 box。<br>**`objectColliderScale` 的語意順手修正**：原本只縮 `size` 不縮 `offset`，等於形狀不是等比縮。現在 size 與 offset 同乘，整個形狀以物件中心等比內縮，相鄰段仍相接不會裂縫（預設 1，今天沒有行為差異）。<br>**待實機確認**：翻轉的 8 個地上物（書房的 `furniture_bookcase1`#4、儲藏室的 `furniture_wood_cabinet4`#3、榕樹妖關的 6 隻鬼魂）碰撞有沒有跟著鏡射——Composite 的幾何是在本地空間生成再吃 transform，負縮放理論上會正確鏡射，但沒實機跑過；另外邪佛廣場有 288 個教徒共用同一張圖，是全專案唯一需要看載入時間的地方。<br>**複查抓到並修掉的兩件事**（都是「寫了註解說要避免、實際卻沒做到」那一類）：① **烘焙與退路其實產出不同形狀**——降取樣是 OR（4 顆子格有 1 顆實心就算擋，等效門檻只有覆蓋率的 1/4），而「直接在目標解析度掃」是整格算覆蓋率，同一張圖 subdiv 4 兩者差 10~38%（教徒 18 vs 13 格）。檔案裡明明白白寫著「兩條路保證一致」卻沒做到，正是它自己警告的那種「有烘/沒烘的機器擋路範圍不一樣」。改成**一律先取得烘焙解析度的那一份（烘好的或當場掃的）再降取樣**，兩條路變成字面上相同的計算。② **最右一欄／最下一列會超出畫布**——`cols = ceil(圖寬/格寬)` 的最後一格是被截短的，但建碰撞條時一律當完整格，於是右邊與下邊多出隱形牆。實測 91 張素材有 27 張中招：屏風多擋 0.227 格、書架 0.254 格——**四分之一格的看不見的牆，正是這次要修掉的那種東西**。已改成把碰撞條夾回畫布邊界（左上本來就精確，所以原本的形狀還是左右不對稱的）。順帶把子格解析度收斂成 1/2/4/8（`SnapSubdiv`）：填 3/5/6/7 會拿不到降取樣而每次重掃，而且 `256/subdiv` 是整數除法、與世界尺寸的 `tileSize/subdiv` 對不起來，形狀會逐欄往右下漂。<br>**通則**：**「碰撞範圍」與「可走層」是兩份獨立的真相**，不要假設塗了其中一個另一個會跟著變；以及**外接矩形這種「只記得極值」的表示法天生無法描述凹形**——「調素材調不出來」時先確認資料結構有沒有辦法表達你要的東西，再繼續調圖。

* [x] **修「一進書房就卡在書架裡」＋落點防呆**（2026-08-19，見 [PROBLEMS.md](PROBLEMS.md) **B10**）：作者把地上物碰撞的子格解析度從 4 調到 8 之後回報「角色的出生點換位置了，被卡住動不了，調回 4 又正常」。<br>**看起來像解析度改壞了，其實完全無關**——先用數學排除：subdiv 4 的碰撞是由 subdiv 8 用 OR 降取樣來的，所以 **4 的覆蓋範圍恆為 8 的超集**，「4 走得過去、8 走不過去」在幾何上不可能成立。改查落點，答案就出來了：**這張地圖根本沒有放「玩家出生點」**，`ResolveSpawnPos` 的順序是「具名落點 → playerSpawn → **地圖中心**」，於是退回地圖中心 (9,-5)，而那裡正好在中央書架 `furniture_bookcase3`（擺在 8.29,-5.92）的碰撞裡。實測 (9,-5) 在 **subdiv 4 與 8 的碰撞內皆為 true**——兩邊都生在書架裡面，差別只在物理推不推得出來：4 的外框接近一個大方塊，Box2D 沿最短方向推出去就沒事；8 的外框是階梯狀、凹角多，圓形玩家被相反方向的接觸法線夾住。**所以「調回 4 就好了」是把問題蓋住，不是修好。**<br>**掃了全部 16 張地圖**：9 張沒有玩家出生點（Main_Square、紅嫁衣的 BridalRoom/Courtyard/Kitchen/LivingRoom1/LivingRoom2/ShrineHall/Storeroom/Study），其中**只有書房的中心真的被擋住**，其餘 8 張是「剛好還沒踩到」——家具往中間挪一點就會複製同一個 bug。<br>**程式端補了 `MapManager.FreeSpotNear`**：三條落點路徑（具名落點／出生點／地圖中心）都再過一次防呆，被 Environment/Water 擋住就以 0.25 格為一圈往外找最近空位（最多 4 格、每圈 16 方向），並把 Warning 從沒頭沒尾的「玩家放在地圖中心」改成點名「多半是這張圖沒放玩家出生點」。<br>⚠ 這段**必須暫時打開 `Physics2D.queriesStartInColliders`**（專案全域是 false，見 [PROBLEMS.md](PROBLEMS.md) **B7**）——它會讓 overlap 查詢略過「重疊在查詢起點」的 collider，而這裡要問的正好是「這個點是不是在東西裡面」，不打開就永遠回答「沒被擋」；`autoSyncTransforms` 也是 false，碰撞剛建好要先 `SyncTransforms()`。<br>**通則**：**「退回預設值」型的後備路徑要能自我檢查。** 「找不到就放地圖中心」看起來很安全，實際上是把一個資料缺口轉成了一個隨機的物理 bug，而且只印 Warning 不擋人，缺口可以躺好幾個月沒人發現。另一條：**「換個設定就正常」通常不是找到原因，是找到了遮罩**——先用幾何/數學排除「這個設定在物理上能不能造成這個症狀」，比反覆試設定快得多。

* [x] **查出「角色的頭被屏風蓋住」＝「層」設錯，不是排序系統壞了**（2026-08-19，見 [PROBLEMS.md](PROBLEMS.md) **E16**）：作者把碰撞子格解析度調到 8 之後回報這個問題。<br>**根因是那個屏風的「層」被設成 +1。** `層 +1` 的排序帶是 13000~18999，而**玩家與怪物永遠在 `層 0` 的 7000~12999**——所以 `層 +1` 的實際語意是「永遠畫在角色前面、完全不參與 Y 排序」。實測 `furniture_bamboo_screen2` 層=1 → sortingOrder 13264；玩家站在 y=-3.5 是 7350、站到地圖最底也才 8000，**怎麼走都贏不了**。<br>`層 +1` 的正當用途是「放在別的東西上面、玩家永遠站不到它前面」的小型桌上物（桌上的花瓶、供桌上的香爐/供盤/燭台）——它們的 sortKey 比腳下那張桌子還高，不往前提一層就會被桌子蓋住。2.6 格高的落地屏風不屬於這類。<br>**這條 bug 本來就在，只是以前被過大的碰撞框擋著看不到**：碰撞改成貼合圖形後（見 **B9**）玩家能走到的範圍變大，才第一次站到「屏風視覺會蓋到人」的那格。全專案 `層≠0` 的只有 9 個，其餘 6 個 `層=1` 都是正當的小型桌上物，但玩家現在能靠更近，同樣的遮蔽感會偶爾出現。<br>**順手把編輯器補起來**（作者回報「在編輯器裡看不到物件是在哪一層」——其實有顯示，但躲在選取那行的最後面，等於看不到）：① 物件面板在「上移層／下移層」上方獨立一行，**寫出語意而不只是數字**並上色（`層 +1　⚠ 永遠蓋住角色（只適合桌上的小東西）`）；② **不點選也看得到**——物件工具下場景上常駐標示所有 `層 ≠ 0` 的物件，橘框＋「＋」= 層 +1、藍框＋「－」= 層 -1。後者是關鍵：層設錯不點選就完全看不出來，而症狀又只在遊戲裡出現，等於沒有任何線索。<br>**通則**：**「整層往前/往後」這種全域旗標是逃生口，不是排序工具。** 它會讓物件退出 Y 排序系統，一旦玩家能站到它附近就露餡；能用位置解決的就不要動層。另一條（這幾天第三次遇到）：**把系統改得更寬鬆，會把一批本來就存在、只是被舊限制擋住的問題一次放出來**——碰撞貼合連續暴露了「落點在書架裡」「層設錯」兩個舊 bug。

* [x] **移動平滑化：沿牆滑動 ＋ 保守角落校正 ＋ 零摩擦材質**（2026-08-19，見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)、[PROBLEMS.md](PROBLEMS.md) **F17**）：作者最早回報「只按右撞到屏風的角就卡住，明明右下通得過，一定要再按下才過得去」。<br>**先釐清一件事**：那不是 bug。`FixedUpdate` 原本只有 `_rb.velocity = 輸入 × 速度`，撞牆全交給 Box2D；單軸輸入撞垂直面，切線分量本來就是 0 → 完全停住是**正確物理**。作者要的是**角落校正**，那要主動寫。另一個真的算缺陷的是「每幀無條件覆寫 velocity，把 solver 修正過的切向速度丟掉」，所以斜推牆是在牆上抖而不是乾淨滑過去。<br>**做法**：在輸入與 velocity 之間插一層 `ResolveMoveVelocity`，**沒撞到東西就原樣回傳**（絕大多數幀只花一次 cast）。① 撞到 → 把速度投影到牆面切線，切線分量 >25% 才算「斜推牆」，並再確認滑動方向本身也通（凹角時 A 面的切線正好指向 B 面，不檢查會讓速度逐幀互換、動畫速度在角落抽動）；② 切線幾乎為 0＝正面撞上 → 左右各試探一次，**只有一側通得過才輕推**。兩側都不通＝真的是牆就該卡住，兩側都通＝窄障礙交給玩家自己決定繞哪邊。這個保守規則就是「不要變成自動駕駛」的全部；③ 順手補上零摩擦 PhysicsMaterial2D（玩家原本沒有，吃預設 friction 0.4，貼牆會被拖慢）。<br>**角落校正順便解掉階梯**：地上物碰撞改成貼合圖形後，斜表面是階梯狀的，每階只有 `tileSize/子格解析度` 高（subdiv 8 = 0.125 格），遠小於校正門檻 0.3，所以玩家沿斜面走會自動跨過每一階、感覺不到階梯的存在。<br>⚠ **複查抓到的致命問題：探測圓不能和碰撞圓等大**（**F17**）。專案全域 `queriesStartInColliders = false`，而整張地圖的牆是**同一顆 CompositeCollider2D**——玩家一貼上牆，等大的圓從圓心射出去時「起點重疊」成立，**那顆 composite 整片被忽略**，探測回報「前方淨空」⇒ 這兩個功能正好在最該生效的那一刻靜默失效，而且接觸間隙只有 0.01，還會逐幀時有時無。**專案在怪物那邊早就踩過並留了註解**（`MonsterActuator.DirectClear`），我第一版還是踩了同一個坑。解法是把探測圓縮 0.05 再把量補回距離。<br>**其餘複查修掉的**：滑動方向未驗證（凹角互換）、角落校正用 `MoveSpeed` 而非當前速度（之後有減速 debuff 會變成加速器）、材質只指 Rigidbody 沒指 collider、XML 註解被 static 欄位插斷而掛錯目標。<br>**通則**：**在這個專案裡，任何「從角色身上射出去」的圓形查詢都要先想一次 `queriesStartInColliders`**——這已經是同一家族的第三次（B7 貼身 OverlapCircle 漏抓、MonsterActuator 的細射線、這次）。要嘛縮小查詢形狀、要嘛改細射線、要嘛改 `Physics2D.Distance`。

* [x] **圖片型文字的多語系：`UI/Texts/<語言>/` ＋ 缺圖退回母版**（2026-08-19，見 [LOCALIZATION.md](LOCALIZATION.md) §圖片型文字）：先做了一次全專案盤點（**2103 張圖**，其中 281 張逐張看過、其餘用檔名掃描與鏡像比對排除），找出所有「把字畫進 PNG 裡」的素材——這些翻譯不了，只能每種語言各出一張。結果是 8 張圖 ＋ 1 組 32 幀動畫，而當時只有 3 張在 `UI/Texts/`。<br>**盤點的兩個意外收穫**：① **`TitlePanel_EN` 早就畫好了但程式一次都沒載過**——`TitlePanel.cs` 把路徑寫死成 `_TW`，所以「圖片型文字的多語系」美術端其實做過一次，只是沒接上；② 判定**不用翻譯**的也列進文件（羅馬數字 `Cell_number_1~5`、鍵帽 `Guide_wasd`、場景上的「囍」與書法春聯、符籙咒文、`rockSlate_*` 其實是圖示不是字），免得下次有人又來問一次。<br>**結構**：`UI/Texts/tw/`（繁中母版）＋ `UI/Texts/en/`，**同一張圖在每個語言資料夾裡同名**、不加 `_tw`/`_en` 尾綴（作者提的，同意——整套機制就是「換資料夾不換檔名」，加尾綴就得為每種語言各寫一次檔名對照，規則本身就沒意義了。`TitlePanel_TW`/`_EN` 因此一起改名成兩邊都叫 `TitlePanel_Title`）。<br>**程式**：新增 `Localization/LocalizedArt.cs`。呼叫端**照舊寫邏輯路徑** `UI/Texts/<name>`，由載圖函式呼叫 `ResolveExisting` 換成當前語言；**缺當前語言自動退回繁中母版**並提示一次（所以英文版能一張一張慢慢補，沒畫的顯示中文、不會開天窗）。全專案 **7 支 `LoadSprite` ＋ `ItemIcons` 全部接上**，「哪一種語言」只有 `LocalizedArt` 知道。<br>**複查抓到並修掉的五件事**：① **切語言後面板不會變**——面板是「建一次之後只顯示/隱藏」，圖在 `OnBuild` 當下就載定了。最糟不是全舊，是**半新半舊**（同一張卡上英文關卡名配中文「領取」鈕，因為有些圖每次重畫時載、有些 OnBuild 載一次）。改成 `UIManager` 訂閱新的 `Language.OnLanguageChanged`，切語言就把快取的面板全丟掉重建（延一幀，否則會在事件處理中途把觸發它的設定面板拆了）。② **`Language.Current` 沒有 `ResetForPlayMode`**——Domain Reload 已關，只要有任何一次切成英文，之後每次 Play 都從英文開始，而**唯一徵兆是美術換成英文版**，幾乎不可能聯想到是殘留。③ **`ItemIcons` 的快取用解析前的路徑當 key**，切語言會直接命中上一個語言那張圖（Sprite 還活著，連重載機會都沒有）→ 改用解析後的路徑當 key。④ 原本只接了 4 支 `LoadSprite`，但文件寫「任何面板都能載」——補齊剩下 3 支，讓宣稱為真。⑤ 缺圖警告原本連「兩邊都沒有」也會喊「沒有 en 版」，那會叫人去找一張本來就不該存在的圖；改成只有母版真的存在才算缺翻譯。<br>**同日補完英文素材與後續修正**：作者把 8 張英文版都畫好了（英文一律**全大寫**——那是英文遊戲 UI 的主流慣例；副標 `Rebirth of Ruin` 例外用 Title Case）。逐張量過畫布與不透明內容框、把每個呼叫端的實際算繪結果算出來比對，結論是**沒有一張爆版或超出容器**，而且作者把留白調得很準（通關標題兩版的字落點只差 1px）。<br>**但抓到一個真的算錯**：`GachaPanel.ArtSumTitle` 是全專案唯一把「內容框」寫死的 localized 圖，那四個數字是量繁中版的 ⇒ 英文版「REWARDS」會被畫成 **352 寬（目標 300）**，而 `LoadArt` 只比畫布比例（tw 3.007 vs en 2.990，差 0.6%）**遠低於 1% 門檻、完全不會警告**。修法是新增 `MeasuredSpec()`：只對 `UI/Texts/` 底下的圖，用實際載到的 sprite 重新量內容框（借用既有的 `IconFit.ContentPx`）；其他圖不動，那些數字是人工依美術判斷微調過的。<br>**還踩到一個自己造的坑（PROBLEMS D18）**：作者把 `Language.Current` 的初始值改成 `Lang.EN` 想測英文，**畫面卻還是中文**——因為前一輪為了防殘留而加的 `ResetForPlayMode()` 裡寫死了 `Current = Lang.CN`，每次進 Play 都把初始值蓋掉。等於同一個「預設語言」有兩份寫死的副本。改成共用 `DefaultLanguage` 常數。<br>**順手補**：`TitlePanel` 的「開 始 遊 戲」原本是寫死字串、沒走語言表，補進 LanguageTable 新開的標題畫面段（6001）。<br>**還沒處理、已記進 TODO**：`DEATH` 的字比 `STAGE CLEAR` 大 12%（繁中版是對齊的，純美術）；英文字在固定寬度下比中文矮 17~23%（想拉齊只要裁畫布留白、不用重畫）；32 幀的 WARNING 走 `VfxTable` 序列圖管線、不經過 `LocalizedArt`；**還沒有玩家可用的語言切換 UI**（全專案沒有一處呼叫 `SetLanguage`）；⚠ `DefaultLanguage` 目前是 `Lang.EN`，出版本前要改回。

* [x] **修「一進新房間就被彈回上一張圖」——被擊退不算踩到傳送點**（2026-08-19，見 [PROBLEMS.md](PROBLEMS.md) **B11**）：作者回報從書房往上走進客廳2，畫面過去一瞬間又退回書房；他自己查出原因——**客廳2 的怪物就站在落點旁邊，玩家還沒看清楚就被擊退推回傳送格**。<br>`TeleportWatcher` 原本的落地防抖是「著陸時未武裝 → 離開所有傳送格才武裝 → 再踩才觸發」，這在「玩家自己走」的前提下正確，但擊退也算「踩到」。<br>**解法**：`TeleportWatcher` / `CutsceneWatcher` 加上「非自主位移不算踩到」——`IsKnockedBack` 為真時踩到觸發點不觸發，**而且解除武裝**。⚠ 只做「跳過這一幀」是不夠的：擊退結束時人還站在那格上，下一幀照樣觸發、只是延後 0.x 秒；解除武裝＝要玩家自己走出去再走回來，正好複用既有的落地防抖語意。**過場點比傳送更該防**（一次性的 `_fired`，誤觸等於白白播掉只能看一次的演出）；鏡頭區刻意不處理（無害）。<br>**作者提的另外兩個選項刻意沒採用**：「把怪物挪遠」只是降低機率、其他地圖照樣會踩到；「調低怪物可見範圍」是拿全域戰鬥參數解一個局部擺放問題。挪怪物值得做，但理由是關卡體驗（一進門還沒看清楚就挨打本身就不好），不是修這個 bug 的手段。<br>**通則**：**「玩家不是自己走過去的，就不該觸發位置型事件。」** 之後若加拉扯／吹飛／輸送帶之類的非自主位移，記得一起加進 `IsPushedAround()`。

* [x] **修「換房後玩家被丟到地圖外面」——`Destroy()` 延到幀尾，舊地圖的碰撞還在**（2026-08-19，見 [PROBLEMS.md](PROBLEMS.md) **B12**）：作者回報書房 ↔ 客廳2 互走，回書房時被卡在傳送點上方動不了。他一開始以為是被怪擊退（前一則），排除怪物後仍會發生，**確認是我前面加的落點防呆造成的**。<br>**根因不在落點防呆本身，在 `Destroy()` 的時機**：同 module 房間互跳走的是**同步版** `LoadMap`，`Teardown()` → 建新圖 → `ResolveSpawnPos()` **全在同一幀**；而 `Teardown` 只呼叫 `Destroy(_root.gameObject)`，物件要等**幀尾**才真的消失 ⇒ 那段時間裡**舊地圖與新地圖的碰撞體同時存在於物理世界**。於是防呆在書房查 (9,-1.5)，查到的是還沒被銷毀的**客廳2 的牆**（客廳2 只有 10 格寬，那個座標在它的座標系裡整片是牆），判定被擋住 → 往外找 → 每個候選點都同時被兩張地圖檢查 → 一路找到兩張圖之外的 (9, 0.5)。<br>**跨 module 換圖不會中**（那條路走協程、中間有 yield，舊碰撞早就沒了），所以症狀只在同 module 房間互跳出現，很容易誤判成別的原因——作者第一次遇到時就先懷疑到怪物擊退去了。<br>**解法兩道**：① 根因——`Teardown()` 在 `Destroy` 前先 `SetActive(false)`，**停用是立即生效的**，碰撞體當下就退出物理世界；這也一併保護了任何「換圖後立刻做物理查詢」的程式（`MapNavGrid` 用 `OverlapCircle` 建障礙格就是一個）。② 防護——`FreeSpotNear` 的候選落點一律限制在地圖範圍內，寧可找不到空位也不要把玩家丟到牆外。<br>**通則**：**`Destroy()` 不是「馬上不見」。** 只要在同一幀內「拆掉舊東西 → 建新東西 → 做物理查詢」，中間就一定要 `SetActive(false)`（或 `DestroyImmediate`），否則查到的是兩份世界疊在一起，而且完全靜默。<br>**另一條**（這次自己犯的）：**加「安全網」型的防呆時，要先確認它拿到的輸入是乾淨的**。落點防呆本身邏輯沒錯，錯在它信任了一個當下並不可信的物理世界，結果把一個原本不存在的問題製造出來。

* [x] **傳送點外型改成「編輯器裡所見即所得」——加「傳送點對位」模式**（2026-08-20，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md) §4.5）：作者回報傳送點放不準，「每一個都要去微調、調完進遊戲看、不對又要回編輯器」。<br>**先查有沒有自動化的可能，結論是沒有**：全 16 張圖共 28 個傳送點（12 個 1 格、15 個 2 格、1 個 3 格——2 格的平均中心必然落在格線上，這就是原本對不準的根因），而**門的美術幾乎全部畫在背景圖裡**（紅嫁衣庭院整張圖只有 1 個地上物「水井」卻有 5 個傳送點；全專案只有 `Main/Environment/rockDoor` 一扇門是地上物）。**資料裡根本沒有「門在哪」這件事，所以沒有可吸附的目標**——任何自動對齊都得先把門從背景拆成地上物。既然只能人工對，那就讓人工對的時候看得見。<br>**做法**：頂部列加「傳送點對位」鈕，開啟後把**遊戲真正的傳送點特效**（VfxTable ID 6，48 幀 24fps、Scale 0.4、PPU 100）畫在畫布上，整張圖每一顆同時顯示、**直接拖曳定位**，不用先選 trigger、不用開面板。半透明＝該點勾掉了「使用傳送點外型」；淡黃十字＝精準中心（光盤柔邊，沒十字看不出正中心）；每顆標區域名稱。拖曳命中就吃掉輸入（不會變成塗格子），整段拖曳算一次 Undo。<br>**資料來源刻意與遊戲同一份**：配方讀主專案 `Assets/Data/VfxTable.csv` 的 ID 6、圖讀 `Assets/Resources/<AniPath>_NN.png`，跨專案直接讀磁碟（沿用 `PreviewSpriteLoader` 的作法），**不必同步素材**——所以之後改傳送點外型，編輯器自動跟著換（按「刷新素材」重讀）。**遊戲端零改動**（`markerX`/`markerY` 機制原本就在）。<br>**通則**：**當一個東西只能人工對，工具的責任就是把「對得準不準」變成看得見的**——這次真正的成本不在微調本身，而在「猜 → 存檔 → 進遊戲 → 回來改」這個沒有回饋的迴圈。<br>⚠ 落點規則（有 markerX/markerY 用它、否則格子平均中心）現在有**三份**：主遊戲 `MapLoader.BuildTeleportMarkers`、編輯器 `TeleportMarkerPreview.TryMarkerPos` 與 `TriggerOverlay.TryMarkerPos`，改一處要三處一起改。

* [x] **傳送點改成「一個點」——錨點＝外型＝踩踏區＝落點；並記下一次「更正確卻更錯」的判定點誤判**（2026-08-20，見 [MAP_SYSTEM.md](MAP_SYSTEM.md) §3.3、[PROBLEMS.md](PROBLEMS.md) **B13**）：作者指出真正的問題不是「看不看得到」而是「**我放傳送點的位置不等於玩家踩到會傳送的點**」，並問能不能不畫格子、拖到哪就在哪傳送。<br>**做法**：`Map/TeleportAnchor.cs` 為正典——`markerX/markerY` 錨點（光盤畫在這、落點也是這）＋ `markerW/markerH` 踩踏矩形，位置自由不再受整格限制。可行的關鍵前提是落點防呆早就做好了：錨點可以落在門上（牆裡），`ResolveSpawnPos` 外面包了 `FreeSpotNear`，會自動推到門前那塊地板。<br>**⚠ 中間走錯一次，值得記住**：當時順手把踩踏判定從 `transform.position` 改成 `PlayerController.FeetWorldPos`，理由是「光盤畫在地上，腳踩上去才合語意」——聽起來更正確，**結果是牆邊的傳送點結構上不可能被觸發**。玩家的碰撞圓在 `transform.position`（胸口），腳底在它下方約 1.0；牆擋的是圓心 ⇒ **腳底永遠無法靠近牆壁一格以內，而門就在牆上**。實測讀數：判定點 (8.71,−1.41)、腳底 (8.71,−2.40)、框 y[−2.17,−1.15] ⇒ 差 0.23 進不去，而且再怎麼走都進不去。已改回 `transform.position`，並把 28 個轉換時多加的補償一併撤銷（現在等價於原本的格子行為）。<br>**通則**：**「哪個座標比較正確」是錯的問題；正確的問題是「這個系統其他部分用哪個座標」。** 物理、可走層、地圖擺放全都以 `transform.position` 為準，觸發判定單獨換座標系就會跟整個系統差一格——而且差的方向剛好是「靠不近牆」，於是只有**牆邊**壞掉、其他地方照常，極難從症狀反推。與 **E14**（特效定位要用腳底）不衝突：**特效對齊視覺、判定對齊碰撞，先問你在跟誰對齊。**<br>**另一個教訓**：我把觸發區從「畫得出來的格子」換成「遊戲裡完全看不見的矩形」，卻沒有同步讓它可見——於是「站在光盤上卻不傳送」變成無從查起。**改動把某個東西變隱形時，讓它可見是同一次改動的一部分，不是之後有空再做。** 已把踩踏矩形、玩家碰撞圓、判定點讀數加進碰撞疊層（P → C）。<br>**編輯器**：對位模式下拖光盤搬位置、拖右下角綠把手改踩踏大小，綠框＝實際觸發範圍；面板另有寬/高數字欄；區域清單顯示「（點）」而非格數。

* [x] **文件體系整頓：AI 契約＋維護規範＋封存機制（仿公司專案的 docs 治理）**（2026-08-21）：作者指出 readme/ 文件已膨脹到「可怕的境界」（PROGRESS 237KB／PROBLEMS 165KB／TODO 57KB），要求參考公司專案 nexus-roulette-client 的 AGENTS/docs/skills/memory 機制整頓。<br>**結構**：① 根目錄 `AGENTS.md` 改寫成薄的 AI 工作契約（常駐鐵則＋「動工前必讀」路由表＋指路，Codex 節原樣保留），新增 `CLAUDE.md`＝`@AGENTS.md` 鏡像入口；② 新增 `readme/DOCS_GUIDE.md`（文件職責分工、新內容放哪的決策樹、真相階層、PROGRESS 記錄格式、大小封存規則、過期處理、收尾檢查）；③ 建 `readme/archive/`（封存原因表在其 README）。<br>**封存（全部原文照錄、零刪除）**：PROGRESS 較舊 160 條搬 `archive/PROGRESS-archive.md`（正檔改單一倒序、~63KB，廢除舊的「前段倒序＋後段正序」兩段式）；PROBLEMS 已淘汰的 A3/A9 搬 `archive/PROBLEMS-archive.md`（原位留存根、編號永不重用，檔頭補分類索引表）；ROADMAP 整份封存（殘餘點子萃取進 TODO 檔尾）；README 底部的日期流水帳移除（路由規則升格進 AGENTS 路由表，其餘內容確認已有家）。<br>**過期修正（逐項對照現碼/CSV 求證後才改）**：MapEditor_DESIGN「runtime 載入器尚未開始」三處（早已上線）、trigger 範例與「內建四種」（現 23 種，正典 TriggerType.cs）；MAP_LOADER_SETUP 首版限制兩條已解（Y-sort＝MapDepthSort、可破壞物）、可走筆刷 1/2/4/8→1~128；MAP_ENTER_EFFECT 附錄「id 1=破幻術」→2（對過 ScreenFxTable.csv）；INTRO_FALL「Intro 排第 0」→ MainScene 第 0（對過 BuildScript.cs，同 PROBLEMS A10）；TODO 濾波「定案 Point」→ 07-05 已改 Bilinear（對過 MapSpriteLoader.cs）；MAP_SYSTEM 補漏掉的 `EnvBright` 第 11 欄＋「欄序以 CSV 為準」；FORGING 加過時 banner（孔位/寶石/存檔以 GEM_SOCKET 為準）；PROPS_IMAGEGEN_LIST 加「無產出進度標記」banner。CHARACTER_SETUP 與 GACHA §5 確認 8/18 已修過、未動。<br>**通則**：**文件失控不是量的問題，是「沒有規則」的問題**——每份文件的職責、新內容該去哪、什麼時候封存，只要有明文規則＋大小門檻，量自然受控。另一條：**封存一律原文照錄＋原位留索引**，讓之後的 AI 查得到歷史脈絡，不會對舊系統做出錯誤評量。

* [x] **Split Sprite Sheet 批次化：新增兩個資料夾模式**（2026-08-21，`Assets/Editor/SpriteSheetSplitter.cs`）：作者要一次處理多張序列圖（新角色 Cain／Crimson Count／Nightborn 每隻 4 個動作各一張 sheet，單張模式要點 12 次）。原單張模式行為完全不變，核心切割抽成 `SplitOne` 三個入口共用。<br>**新入口**：①「切到檔名子資料夾」——選資料夾、掃**第一層** PNG，每張 `B.png` 切到 `該資料夾/B/`（適合「idle.png/walk.png/dead.png 丟在角色資料夾」，切完直接是 route B 結構）；②「整包就地切割」——選資料夾、**遞迴**掃子資料夾，每張 sheet 就地切成幀（適合 sheet 已各自放進動作資料夾的角色包，選角色資料夾一鍵全切）。批次前有確認框、有進度條，結果彙總「切了幾張／跳過幾張／哪些失敗（原檔保留）」。<br>**關鍵守衛：批次模式把剛好 256×256（1×1 格）的 PNG 視為「已是單張幀」直接跳過**——沒有這條，「整包就地切割」跑第二次會把上次切出的每張幀再切一次（改名＋刪原檔），整包被靜默重排。單張模式維持舊行為不加守衛（親手選檔＝明確意圖）。<br>**順手修掉舊模式的一個潛在資料損失**：整張全透明的 sheet 原本會「寫出 0 幀、然後照樣刪掉原圖」；現在 0 幀就不刪、報錯保留原檔。<br>**通則**：**會刪原始檔的批次工具，先想「重跑第二次會發生什麼」**——冪等性（跑幾次結果都一樣）是這類工具的安全底線，靠的是「能辨認自己的輸出」（這裡是 1×1 格守衛）。

* [x] **新增「夜裔」血統系列（夜裔 → 血伯爵 → 該隱）**（2026-08-21，見 [BLOODLINE.md](BLOODLINE.md) §7）：作者把三階段的序列圖（idle/walk/dead/attack 各 25 幀）、八種情緒立繪與藥劑 icon 都放好了，要把它接成可玩的第二個系列。<br>**純資料，程式零改動**——這正是 §7 那條「加新系列不用改程式」第一次被真正驗證：表A `BloodlineSeriesTable.csv` 加 `2,Nightborn,夜裔,20,21,22`；表B `BloodlineTable.csv` 加 20/21/22 三列（五屬性是佔位值，等屬性系統再校）；`ItemTable.csv` 加 **302 血統藥劑・夜裔**（`BloodlineID=20`，欄位數比照 301 的 16 欄）；`BaseBloodRoll.csv` 加一列權重 10，與殭屍同機率。進階藥劑（310/311）全系列通用，一個字都不用動。<br>**刻意保留資料夾名稱裡的空白**（`Crimson Count`）：查過整條管線——catalog 的 `id` 是 `Rel()` 出來的相對路徑、載圖走 `File.ReadAllBytes`、`PlayerSpriteLibrary.Key()` 只 `Trim()` 前後空白，中間空白會原樣保留並正確比對，所以**不需要**為了保險去改資料夾名。反過來說，CSV 的 `SpriteFolder` 就必須照實填 `Crimson Count`，手癢去掉空白反而會對不上——已在 BLOODLINE.md §2 標註。<br>**量過素材才發現的一件事**：夜裔三階的 idle 可見高是 174 → 157 → **138**，該隱只有 Base（193）的 71%。`PlayerAnimator.Setup` 是「依 idle 可見高把每個血統正規化到同一世界高度」，所以**該隱那組圖會被放大約 1.4 倍才畫出來**（其他血統約 1.0~1.1）——像素密度最低、最容易糊，而且 `BodyScale` 再往上加只會更糊。三階的 `BodyScale` 先全填 1，實機看過再調（改 CSV 即時生效）。**通則：`BodyScale` 不是「這個角色多大」，是「正規化之後還差多少」——先量可見高再決定要不要動它。**<br>⚠ **還沒跑 `Project Tools → Sync Map Assets`**（我這邊開不了 Unity）。已確認 catalog 目前不含這三個資料夾，沒同步就喝藥的話角色會只剩影子＋Console 噴「找不到任何外型圖」。<br>**順手修掉兩處已經寫錯的舊敘述**：`DRAMA.md`「血統目前只有 Base」、`TODO.md`「只有 Base 血統可驗、三個血統的 SpriteFolder 都填 Base」——那是 8/18 血統素材進來之前的狀態，留著會讓人以為換外型還沒接通。

* [x] **作弊面板新增「獲得所有血統藥劑」快捷鈕**（2026-08-22，`Assets/Scripts/UI/Panels/CheatPanel.cs`）：測血統系列時要一瓶一瓶填 ID，兩個系列加兩種進階藥劑就是四次。「給道具」分頁的「一鍵快捷」那一列補第三顆鈕，**物品表裡每一種血統藥劑各給一瓶**。<br>**判斷走 `ItemData` 的欄位、不比對名稱前綴**：`IsBloodline`＝`IsBloodlineStarter`(`BloodlineID>0`) ∪ `IsBloodlineUpgrade`(`BloodlineUpgrade>0`)，目前正好抓到 301 殭屍／302 夜裔／310 中階／311 高階。用「ID≥301 且名字開頭是血統藥劑」也對得出同一份清單，但**下次加系列就得回來改這裡**，而欄位判斷不必——這跟「取得所有武器」那顆鈕刻意用 `EquipSlot==Weapon` 而不是 `WeaponID>0` 是同一個理由。<br>給予走與「依 ID 給道具」同一條路（`RunProgress.GiveItem(..., toRealBag:true)`，作弊給的東西不進臨時包）。**與武器鈕不同、刻意不跳過已經有的**：藥劑是消耗品，喝掉就沒了，「再按一次補一瓶」比「已經有就不給」實用。這裡只負責塞進背包，能不能喝（起始藥劑一世一次不可逆、進階藥劑必須逐階）仍由 `ItemUse` / `BloodlineSystem` 判定。

* [x] **修「Cain / Crimson Count 的攻擊動畫播不出來」——真正的原因是 25 幀從來只播得到 2 幀**（2026-08-22，見 [PROBLEMS.md](PROBLEMS.md) **G6**、[ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)）：作者回報那兩個血統攻擊時就是站著，同系列的 Nightborn 卻正常。<br>**先把素材那一側整個排除掉**：三個資料夾 idle/walk/dead/attack 各 25 張、檔名編號連續、catalog 的 `frames` 都是 25 筆且檔案全在、25 幀彼此都不重複——attack 確實是獨立的施法動畫，不是被 idle 蓋掉。<br>**真正的原因是三件事乘起來**：`AttackAnimLinger = 0.12f` ×  `PlayerAnimFPS = 12`（一幀 0.083 秒）⇒ 攻擊姿勢只播得到 **1.4 幀**；再加上 `SetState` 換狀態時 `_idx = 0`，每一發都從第 0 幀重來。所以**所有血統的攻擊動畫從來都只播前兩幀**（25 幀用到 8%），「看不看得出來」完全由第 1 幀長什麼樣決定。量七組素材的 attack 第 1 幀與 idle 輪廓差異，分得乾乾淨淨：Nightborn 85%／旱魃 81%／Base 79%／殭屍 61%／毛殭 54%（第 1 幀就已經是施法姿勢）vs **Crimson Count 20%／Cain 18%**（前 6 幀還在起手）。**前五組素材湊巧「開頭即定格」，把這個限制遮了好幾個月。**<br>**作者拍板的新規則**（動畫語意從「一發＝一個動作」改成「進入施法狀態」，與 0.2 秒射速刻意脫鉤）：① 起播幀自動跳過起手；② 按下開火 → 從起播幀播**一次**；③ 播完還按著 → **定格在最後一幀**，不重播；④ 中途放開照樣播完，**放開再按**才重播。<br>**起播幀怎麼算——刻意不做成 CSV 欄位**：作者明確要求「不想每個動作都去調整，要一條適用所有角色的規則」。作法是 `PlayerSpriteLibrary.GetActionStartFrame` 比對 attack 各幀與 idle 站姿的輪廓（畫布降成 64×64 佔用格、OR 降取樣、idle 取多數決當站姿），取第一個「差異達到該動作**自己峰值** 60%」的幀。**關鍵在相對峰值而非絕對門檻**——各血統動作幅度差很多（峰值 Cain 只有 25%、Nightborn 86%），任何絕對門檻都會對其中一邊失效。實測七個血統：Base／旱魃／Nightborn = 第 1 幀（完全不受影響）、殭屍／毛殭／Cain = 第 6 幀、Crimson Count = 第 4 幀，逐張目視確認起播幀都是一眼看得出的攻擊姿勢。門檻試過 0.75，那會把殭屍砍到剩 13 幀、毛殭剩 10 幀（那兩組本來就沒問題，等於砍過頭）。思路與地上物碰撞遮罩（**B9**）同源：**只跟那張圖有關的資訊就從圖算，別變成人工維護的資料**；差別是這裡只有 runtime 一條計算路徑，沒有 B9 那種「烘焙版與退路版算出不同結果」的風險。<br>**順手查到並修正的兩件事**：① **attack 根本不是循環動畫**——七組素材「最後一幀→第 1 幀」的接縫差異 23~52%，相鄰幀平均只有 3~20%（只有 Base 無縫）；idle 則全部無縫（0.4~3%）。所以 `IsLooping` 把 Attack 算進去本來就是錯的，已改成一次性。② `StreamingAssets/.../Base/idle/` 有一張殘留的 `idle_01.png`（310×500，其餘都 256×256），`GameAssets` 裡沒有——**Sync Map Assets 只推新檔、不刪來源已移除的舊檔**；catalog 目前正確沒收它，但它排序在 `Actor-iso_*` 後面，挑檔規則一變就會變成第 26 幀（已請作者刪除）。<br>**移動中刻意不定格**：2D 單張逐格圖沒辦法上下半身分離，若移動中也定格，「按住開火邊跑」會變成用靜止的施法姿勢滑過地板——那是這遊戲最常見的操作。所以移動中只認 `IsAttackPlaying`（完整播一次就把畫面還給 walk），站定才吃 `_attackPoseHeld` 定格。另留兩個 Inspector 旗標給實機 A/B：**Move Overrides Attack Pose**（移動中完全不播攻擊）與 **Attack Anim Legacy Mode**（整套回到改版前的 0.12 秒＋循環）。<br>**後續（同日）**：作者實機跑過一輪後要求把「按住播完＝定格」再做一版「從起播幀再來一次」對比，所以做成 Inspector 旗標 **Attack Anim Repeat While Held**（目前預設開＝重播），一鍵切換不必重編譯。實作放在 `PlayerAnimator` 的一次性收尾那一行（播到最後一幀時改成跳回起播幀），而**旗標由 `HandleVisuals` 每幀重設**＝「還按著 且 站著不動 且 有開這個選項」——所以放開的那一刻旗標就變 false、當次循環播完自己定格，「放開後照樣把這一次播完」在兩種模式下都成立，收尾行為一致。⚠ 重播的是「起播幀 → 最後一幀」而不是整段繞回第 0 幀：起手是一次性的，每輪都重播會變成「施法到一半又把手放下」。<br>**後續二（同日）**：作者實測回報「按著左鍵播 attack，這時放開改成走路，attack 沒有被中斷」——他要的是**一移動就強制取消攻擊動作、沒播完也砍掉**。這其實就是我當初加的 `MoveOverridesAttackPose`，只是我預設成關的（我原本擔心「移動中完全看不到攻擊動作」太可惜，選了折衷的『完整播一次再還給走路』），溝通時也沒把兩者講清楚。已改成預設行為。<br>⚠ **不是改預設值，是把欄位改名**（`MoveOverridesAttackPose` → `CancelAttackPoseWhenMoving`）：Unity 早就把舊欄位的 false 序列化進場景/prefab 了，只改程式裡的預設值不會生效（**G4** 那個坑）。換新名字＝新的序列化欄位，才會真的吃到 true。<br>**通則**：**「只有某些角色壞掉」常常代表「所有角色都壞掉，只是其他角色的資料剛好蓋住了」。** 動壞掉的那兩組之前，先問「正常的那幾組為什麼正常」——這次的答案不是「它們是對的」，而是「它們碰巧不會踩到」。

* [x] **系統訊息獨立成 `UILayer.System`——不再被背包等視窗蓋住**（2026-08-22，見 [PROBLEMS.md](PROBLEMS.md) **E18**、[UI_SYSTEM.md](UI_SYSTEM.md)）：作者回報「用了夜裔之後，在背包點殭屍血統藥劑完全沒反應，關掉背包才發現提示早就跳過了」。<br>**訊息一直都有跳，只是畫在背包底下**：`AlertPanel`（全遊戲系統訊息唯一入口，32 處呼叫）掛在 `UILayer.HUD`＝sortingOrder 0，背包是 `Window`＝100。而 toast 只顯示 1.6 秒＋淡出 0.4 秒，等玩家關掉面板多半已經淡完 —— **連「有訊息」都不知道**。這不是背包獨有：倉庫／鍛造／抽選／劇本／設定開著時全都一樣，而「操作失敗要給理由」幾乎都發生在面板開著的時候，等於系統訊息最需要被看到的場合正好全部失效。<br>**新增 `UILayer.System`**，`AlertPanel` 改掛這層。**sortingOrder 刻意不照 `i * 100` 排**——那會是 400，反而被 `TutorialHintPanel`(460)、`GuideFingerPanel`(500) 這些自己 `overrideSorting` 的面板蓋住（那兩個硬寫值本身就是這個問題的舊補丁）；改成 **700**，卡在「蓋過所有一般 UI 與教學疊層」與「**不**蓋過全螢幕接管的演出（Intro 1000／隧道 1200／影片 1300／劇情 5000）與黑幕（30000）」之間——過場影片播到一半浮出一行 toast 比訊息被吃掉更糟。完整排序帶已整理成表寫進 UI_SYSTEM.md，之後要加層或手寫 sortingOrder 先看那張表。<br>**⚠ 這層的東西一律 `raycastTarget = false`**：蓋在所有視窗之上，忘記關會靜默擋掉底下的點擊，症狀又變成另一種「點了沒反應」。`AlertPanel` 的底板與文字本來就都是 false（`UIBuilder.Text` 預設關掉），這次沒踩到，但已寫進 `UILayer.System` 的註解當守則。<br>**沒動的**：`MonsterSpeechPanel`、`PickupTipPanel`、`TutorialHintPanel`、`TutorialDimPanel` 維持 HUD——它們是「跟著世界或跟著教學流程」的提示，被視窗蓋住是合理的；`ScreenFxPlayer` 播全螢幕特效時仍只藏 HUD，所以特效期間系統訊息照樣看得到（若日後覺得不該，一併藏 `System` 即可，已註明）。<br>**通則**：**「回饋能不能被看到」跟「回饋有沒有發出」是兩件事，而前者是分層決定的。** 訊息系統掛在會被蓋住的層，等於在最需要它的情境下自動關閉，而且失敗完全靜默——程式正常、log 也有，只有玩家看不到。

* [x] **劇情演出四項擴充：回憶特效／頭上對話框／用 trigger 啟動／隱藏主角**（2026-08-22，見 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §2.5·§6·§7·§8、[PROBLEMS.md](PROBLEMS.md) **E19**/**J3**、[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3）：作者提了四項需求，其中兩項是「先幫我確認做不做得到」。<br>**先回答那兩個確認**：① **劇情原本只能一進圖自動播**——`MaybeAutoStart` 全專案只有 `MapManager.PlaceAndSetup` 一個呼叫點，`autoStartOnEnter=false` 等於停用；更關鍵的是**自動播沒有任何一次性機制**（不查旗標、不吃 `repeat`），所以**每次進這張圖都會重播一次**。② **劇情不需要主角是可以做的，但不能 `SetActive(false)`**（見下）。<br>**① 回憶特效（`memoryFx` 勾選）**：泛黃老照片＋柔邊暈影，四層疊在一起（去飽和後往暖褐偏／邊緣越外圈越模糊／邊緣壓暗中央微亮／極輕靜態顆粒），開演淡入 0.6 秒、收尾一定淡出。**刻意不登記進 `ScreenFxTable`**——那一家（睜眼/破幻術/馬賽克）從簽章開始就是「有總長、播完回呼」的一次性過場，而且統一暫停遊戲＋藏 HUD，三條對「持續狀態」全是反效果。持續型全螢幕後處理的正典是 `AtmosphereController` 那種常駐 blit，所以新寫 `MapFx/MemoryFxController.cs` ＋ `Resources/Shaders/MemoryFx.shader`，用 `unscaledDeltaTime`（對話暫停時照走）、`sceneLoaded` 重掛相機。**判斷準則已寫進 PROBLEMS J3**：「它有沒有一個『結束』的時間點，而且結束前遊戲該不該停？」有 → ScreenFxTable；沒有 → 常駐 blit。<br>**② 頭上對話框（`bubble` 步驟）**：直接移植怪物說話那一套。原本的 `MonsterSpeechPanel` **綁死 `MonsterController`**（到處讀 `mc.IsDead`、`mc.GetComponent<Collider2D>()`），而劇情演員是純 GameObject＋`MonsterAnimator`，不是怪物。**泛化成「一個 `Transform` 目標 ＋ 一個『還在不在』的委派」**，`Speak(MonsterController,…)` 留成薄包裝＝**怪物端零改動**，底板輪流/避邊/鏡像/best-fit 整套美術邏輯完全沿用。頭頂座標補成三段優先序：玩家用 `PlayerController.FeetWorldPos`/`VisibleBodyHeight`（**E14** 的鐵則）、有 Collider2D 用碰撞框（怪物走這條、行為不變）、**只有 SpriteRenderer 用 bounds**——最後這段是必要的，劇情 npc 演員身上根本沒有 Collider2D。台詞走 `LanguageTable` 的 id（玩家可見字串一律 `Language.GetText`），搭 `background` 就能邊走邊講。<br>**③ `playCutscene` 觸發鏈動作**：新增動作型 trigger（`triggerTypes.json` ＋ 編輯器 `TriggerType.Defaults()` ＋ 遊戲端 `TriggerChain` 的常數/case/`ExecutePlayCutscene`，照 `playScreenFx` 的樣板）＋ `CutsceneDirector.PlayById`。把「一進圖自動播」關掉改用它之後，**觸發鏈整套守門條件立刻全部可用**——條件旗標／重複規則（關卡單次·每次·每周目·永久）／周目上下限／完成關卡數。**刻意不在 CutsceneDirector 裡自己做一套旗標**：觸發鏈那套已經很完整且作者熟悉，再造一份只會多一個要同步的真相。演完才接 next；⚠ 該段結尾有 `end` 交棒換圖時鏈就此結束（同 `teleportTo`）；開不成時印 Warning 並直接接 next，不讓鏈卡死。<br>**④ 隱藏主角（`hidePlayer` 勾選）——這項的坑最多**：直覺的 `player.SetActive(false)` 會留下**三個殘留**。(a) **影子留在原地**：`BlobShadow` 的影子是**獨立 GameObject 不是子物件**（刻意避免被角色 flipX/縮放二次影響），停用玩家連它的 `LateUpdate` 也停 → 影子定格不動也不消失；(b) **暗場景的光圈留在原地**：`AtmosphereController` 每幀以玩家為心算裝備的 `LightRadius`，跟 renderer 開不開無關 → 空地上浮一圈沒有主人的光；(c) **碰撞還在擋路**：演員 A\* 走位被看不見的玩家撞開。正解是**逐項關掉**（收成 `Cutscene/PlayerVisibility.cs`）：SpriteRenderer（含子物件）＋新加的 `BlobShadow.SetVisible`＋Collider2D，再由 `PlayerVisibility.IsHidden` 讓氛圍光源跳過玩家。位置在隱藏時記下、收尾放回；**有 `end` 交棒換圖時刻意不還原位置**（新圖自己安排落點）。同時勾 `hidePlayer` 又放 `player` 演員會印 Warning 並以隱藏為準。<br>**順手做的格式升級**：`.dipanmap` 的 `cutscene` 從單一物件改成 **`cutscenes` 清單**（舊欄位保留讀取相容，`MapData.NormalizeCutscenes()` 在三個讀檔點搬移：遊戲端 `MapSerializer.Load`、編輯器 `MapSerializer.Load` 與 `MapSession.RestoreFromJson`＝Undo）。**這次仍只用第 0 段**，純粹是為了「之後真的要做一張圖多段劇情時，不必再動檔案格式、既有地圖也不用轉檔」——全專案目前只有 `Main_InitialForest1`(4 步)/`2`(31 步) 兩張有劇情，趁只有兩張的時候改成本最低。<br>**通則**：**「這個系統能不能做 X」的答案常常是「機制在別的系統裡已經有了，只是沒接過來」。** 這次四項有三項是接線而不是發明——頭上對話接怪物說話、重複控制接觸發鏈、隱藏接既有的 renderer/collider/光源開關；真正需要新寫的只有回憶特效的 shader。動手前先問「這件事在別處是不是已經解過了」，比從頭設計快得多，也少一份要同步的真相。<br>⚠ **還沒實機驗證**（我這邊開不了 Unity）：回憶特效的濃淡、`MemoryFx.shader` 能不能正常編譯、頭上對話框掛在沒有 Collider2D 的演員身上時的高度是否合適，都要作者跑一次再調。**改完記得在 Unity 按 Cmd+R 重新匯入**（橋接寫入常不自動觸發重匯入）。

* [x] **劇情演出續補：條件旗標／完成寫旗標、關閉血量 HUD、回憶特效在暗地圖上救回來**（2026-08-22，見 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §1.5·§6、[PROBLEMS.md](PROBLEMS.md) **J4**）：接續同日前一則的四項擴充，作者實機看過後提了三件事。<br>**① 進圖自動播加旗標守門**：Cutscene 加 `requireFlag`／`setFlag` 兩欄，**完全沿用既有那套**——地圖只存旗標裸名（＋條件的否定 `!`）、生命週期由全域 `flags.json` 決定、編輯器一樣是「輸入旗標 id → 按確認 → 撈出名稱鎖定顯示」（直接複用 `DrawFlagFieldCore`，那支本來就是給 trigger 參數與地上物破壞旗標共用的），遊戲端走同一支 `TriggerChain.FlagTrue`/`SetFlag`。**「只播一次」＝條件填某旗標的「沒有」＋完成寫旗標填同一個**，而**播幾次由那個旗標的生命週期決定**：關卡單次＝每趟關卡一次（作者要的紅嫁衣情境）、周目＝這周目一次、永久＝一輩子一次。**刻意不在劇情裡自造 `repeat` 欄位**：那會變成第二份「這件事發生過沒有」的真相，而旗標登記表已經把生命週期收成單一來源。編輯器順手加了防呆——條件與寫入填同一個旗標時，若條件是「有」而不是「沒有」會警告「第一次永遠不會播」（這個填反了很難自己看出來，症狀是整段劇情從來不觸發）。<br>**② `hideHud` 關閉底部血量 HUD**：演出常演在畫面下方、被液體血球擋住。**只關 `BottomHudPanel`、刻意不用 `SetLayerVisible(HUD,false)`**——頭上對話框（`MonsterSpeechPanel`）與提示也住在 HUD 層，整層藏掉連演員說的話都看不到（這正好是前一則才剛加的功能，差點自己踩自己）。收尾**還原成開演前的樣子而不是無條件打開**：開場山道 13/14 本來就沒有血球 HUD，無條件 `Open` 會憑空生出一個。<br>**③ 回憶特效在紅嫁衣看不清楚——方向錯了，不是強度不夠**（**J4**）：紅嫁衣全 10 張圖 `Atmosphere` 都是 **2（幽暗＋打光）**，除了玩家提燈那一圈以外壓到接近全黑；而我做的三層**全部是乘法或壓暗**——泛黃是「顏色 × 暖褐」（`0 × 任何值 = 0`）、暈影是「邊緣再乘 <1」（本來就黑）、柔邊是「模糊」（黑的模糊還是黑）。**整套效果在暗場景上是自動失效的，而且失效得很安靜**：每一行程式都在跑、參數也確實套用了，所以第一反應才會是「調大一點」。<br>**修法兩層**（作者選的）：**(a) 回憶期間淡掉整套場景氛圍**——`AtmosphereController.SetBypass(0~1)`，在 `Atmosphere.shader` **最後一行**與原始畫面內插（放最後＝15 個 mode 一致地淡掉，不必逐 mode 改），與回憶的淡入淡出同步。語意也對：回憶不是「現在這個黑房間」，不該有提燈的黑暗感。⚠ 副作用是天氣與提燈光圈一起淡掉，這是刻意的，要保留就把 `SuspendAtmosphere` 改 false。**(b) 上下黑邊**（各 11% 高，做在後處理裡、隨強度滑入滑出）——它是「直接把那一塊塗黑」，**與場景明暗完全無關**，全黑的地圖上也一眼看得出進入過場。作者另外評估過的「褪色提亮／白霧邊／舊膠卷刮痕／宣紙墨暈」先不做，等這兩層看過再說。<br>**通則**：**乘法／壓暗類的畫面效果需要畫面本來就有亮度，在暗場景上必然失效；要在暗場景可靠地被看見，得用加法（提亮、白霧、發光）或幾何（黑邊、遮罩、輪廓）。** 全螢幕後處理的驗收條件應該包含「它在最暗與最亮的 `Atmosphere` 上還成不成立」——之後任何「中毒發綠」「受傷泛紅」都適用。另一條：**這種 bug 的第一反應會是「參數調大一點」，而參數確實有生效，所以會一路調到過頭還是看不見**；先問「這個運算在極端輸入下數學上會發生什麼」比調參數快得多。<br>⚠ **一樣還沒實機驗證**：`Atmosphere.shader` 本來就是「15 種分支攤平在同一個 pixel shader、指令數大」（檔頭有註記 `#pragma target 3.5`），這次多加了一個分支與一次 `tex2D`，**要確認沒有超過上限變成洋紅**。回憶的濃淡與黑邊高度也要實機再調。

* [x] **修「勾了關閉血量 HUD 卻沒生效」——兩個原因疊在一起，一個是沒同步、一個是 `Start()` 跑太晚**（2026-08-22，見 [PROBLEMS.md](PROBLEMS.md) **C10**/**D21**）：作者回報勾了「演出期間關閉血量HUD」進遊戲測試 HUD 還在。<br>**原因一（假的）：遊戲讀的地圖是舊的。** 比對三份 `.dipanmap` 就一眼看出來——編輯器那份（`DipanProj_MapEditor/Maps/`）有 `hideHud:true` ＋ 新填的條件/寫入旗標，而遊戲實際讀的 `StreamingAssets/MapAssets/` 那份是 17 分鐘前的舊版、**根本沒有這個欄位**（反序列化拿到預設 false，一切正常、零錯誤訊息）。同 **B3** 的根因，但「新增欄位」的症狀特別容易被誤判成「功能沒做出來」。**順手發現更陰險的第二半**：`flags.json` 是另一份要同步的東西，作者新建的 `redBridalFatherTalk`（關卡單次）只在編輯器那份裡，遊戲端那份還是 8/16 的舊版；而 `TriggerChain.Resolve` **查不到旗標時不報錯、直接退回「周目」**——所以就算地圖同步了，那段劇情也會從「一趟關卡一次」**靜默降級**成「一周目一次」，功能看起來完全正常，可能好幾天後才發現。兩份檔案我先幫忙同步過去了，之後照樣要跑 `Sync Map Assets`。<br>**原因二（真的 bug）：`PlayerController.Start()` 也會開 HUD，而且比我關掉的時機晚。** 血量 HUD 有兩個會主動打開它的來源——`MapManager.PlaceAndSetup`（在 `MaybeAutoStart` **之前**，所以關得掉）與 `PlayerController.Start()`（玩家**初次生成**時）。而 Unity 的 `Start()` 是在「建立這個物件的那支程式跑完之後」才呼叫 ⇒ **比 MaybeAutoStart 晚**，把剛關掉的 HUD 又開回來。玩家跨圖不重生，所以**只有「這趟第一次生成玩家」的那張圖會踩到**，同 module 房間互跳反而正常——症狀有地圖選擇性，極難反推。<br>**解法：改成每幀維持**（`EnforceHudHidden` 在 `Update` 裡跑），不管之後誰去開都蓋不過演出；**刻意不去改每一個會開 HUD 的地方**——那會變成一份要同步的清單，加第三個來源時又會漏。同時把「有人想開過」記起來，收尾才知道要還原成開著。<br>**通則兩條**：① **`Start()` / `Awake()` 不在你以為的位置**——在同一幀「建立物件 → 對全域狀態下指令」，那個物件的 `Start()` 會在你之後執行並可能覆蓋你。判準是「**這個狀態有幾個來源會去寫它？**」，一旦超過一個，一次性設定就不可靠。② **「查不到就退回預設值」的設計，會把「忘記同步」變成一個安靜的行為改變而不是看得見的錯誤**——這種地方值得在退回預設時印一行 Log。

* [x] **修「隱藏主角用著用著就永久失效」——static 開關的三個還原路徑少了兩個**（2026-08-22，見 [PROBLEMS.md](PROBLEMS.md) **D22**）：作者回報「剛剛好的功能被修壞了，勾了隱藏主角但主角還在場上」。<br>**根因不是我上一輪改壞的，是這個功能從第一版就少了還原路徑，只是要滿足條件才會露出來**：`PlayerVisibility.IsHidden` 是一個純 C# 的 static bool，而本專案**已關閉 Domain Reload** ⇒ 它不會在每次 Play 歸零。只要有一次 Play 是在演出播到一半時按停止（作者測 HUD 那件事時幾乎一定發生過），`Show()` 就沒機會跑、`IsHidden` 殘留成 true；下一次 Play 進 `Hide()` 的第一行 `if (IsHidden) return;` 直接返回 ⇒ **從此再也不隱藏，重新 Play 沒用、重進地圖沒用，只有重開 Unity 才好，而且完全沒有錯誤訊息**。<br>**同一個根因還有第二種觸發方式，而且 build 也會中**：演出被換圖打斷時，`StartCutscene` 直接 `Destroy` 掉還在跑的上一個 director，協程當場中斷、`Cleanup` 永遠不執行 ⇒ 玩家永遠隱形、回憶特效永遠掛著、輸入永遠鎖著；而症狀出現在「下一張圖」，跟真正的原因隔了一次換圖。<br>**修法兩道**：① 加 `PlayerVisibility.ResetForPlayMode()` 註冊進 `PlayModeStaticReset`（那個檔案的檔頭本來就在警告這一類，我加新 static 時漏了照做），`Hide()` 另加保險——狀態說「藏著」但目標物件已經不在＝狀態壞了、重來一次；② `CutsceneDirector` 把「對全域狀態動過的手」（輸入鎖／回憶特效／隱藏主角／血量 HUD）收成一支**冪等的 `ReleaseGlobals()`**，`Cleanup` 與 **`OnDestroy`** 都會走到。<br>**⚠ 修第 ② 點時踩到 `Destroy()` 的延遲（同 B12）**：換演出時若只是 `Destroy(舊 director)`，舊的 `OnDestroy` 安全網會在**幀尾**才跑——那時新演出已經把主角藏好，安全網當場把它放出來。所以改成**先同步呼叫舊的 `ReleaseGlobals()` 再 `Destroy`**，讓幀尾那次變成 no-op。<br>**通則**：**任何「進入某狀態 → 之後要還原」的 static 開關，都要同時回答三個問題**：① Play 停止時誰還原（`PlayModeStaticReset`）② 持有者被硬銷毀時誰還原（`OnDestroy` 安全網）③ 還原與「下一個持有者開始」的先後（`Destroy` 是延遲的，要先同步釋放）。少任何一項，症狀都是「用著用著就壞了、而且不會自己好」——最糟的是它會被誤判成「上一次改動改壞的」，把排查方向整個帶偏。

* [x] **修「劇情剛播完，玩家就被送回上一張圖」——演出把玩家釘在傳送點上**（2026-08-22，見 [PROBLEMS.md](PROBLEMS.md) **B14**）：作者回報從客廳2 往下走進書房、書房的進場劇情自動開演，演完的瞬間人回到客廳2。<br>**先排除掉最像的兩個**：`end` 的去向是空的（不交棒），書房也沒有 `teleportTo` 之類的鏈動作——全專案能呼叫 `GoToMap` 的只剩 `TeleportWatcher` / `CutsceneWatcher` / `TriggerChain.teleportTo` / `CutsceneDirector.DoHandoff` / `GameFlowManager`，逐一對照後只可能是傳送點。<br>**根因是一個平常被「玩家會自己走開」蓋住的前提**：`targetEntrance` 指的就是**對面那顆傳送點的錨點**——書房的 `Study_north` 就是通回客廳2 的那扇門，所以**剛落地時玩家腳下踩的正是回頭路**。平常沒事是因為 `TeleportWatcher` 有落地防抖（著陸時未武裝、要離開再踩回來才算），而玩家一落地就會自己走開。**但自動播的劇情會把玩家釘在那個位置十幾秒**，勾了「隱藏主角」的話收尾還會**用程式把玩家搬回開演前的位置**——這段期間只要武裝狀態被翻成 true，下一幀就判定「踩到傳送點」，而人根本沒動過。<br>**解法兩道，都照 B11 那條規則（玩家不是自己走過去的，就不該觸發位置型事件）**：① `TeleportWatcher` / `CutsceneWatcher` 在 `CutsceneDirector.IsPlaying` 期間**一律不觸發、並持續解除武裝**——⚠ 只「跳過這一幀」不夠，演出結束時人還站在那顆傳送點上，下一幀照樣觸發（與 B11 的擊退是同一個教訓）；② 任何「用程式把玩家搬過去」之後都要呼叫新的 `MapManager.DisarmPositionTriggers()`，目前呼叫者是 `PlayerVisibility.Show`，而且**只有真的位移時才搬、才解除**（多數情況主角整段沒動過，搬回去等於白做還多製造一次非自主位移）。<br>**順手加了診斷**：`TeleportWatcher` 每次觸發印一行「踩到傳送點「X」→ 地圖 N；玩家位置 …」（一次換圖一行，與 MapManager 的「進入地圖」同量級）。這類「我沒走過去卻被傳走」的問題，關鍵資訊就是「哪顆、當時人在哪」，之前完全沒有線索。<br>**通則**：**「玩家站著不動」不是安全狀態，而是一個會累積風險的狀態。** 位置型觸發的設計前提是「玩家會走開」，任何把玩家長時間釘在原地的機制（演出、教學、對話、暈眩）都會讓落地防抖這種「靠玩家自己離開」的防護失效。做這類機制時要主動問一句：**玩家被釘住的那個位置，本身是不是某個觸發區？**

* [x] **對話框字級改成自己算，不用 uGUI 的 best-fit**（2026-08-22，見 [PROBLEMS.md](PROBLEMS.md) **E20**、[MONSTER_SPEECH.md](MONSTER_SPEECH.md)）：作者回報同一句台詞「這個給你吧，就剩兩張了」改寫成「這個給你吧`\n`就剩兩張了」之後**字大了一號**——他用 `\n` 只是想控制斷句位置，不是想改字級。<br>**原因**：`resizeTextForBestFit` 是「在這個框裡找一個塞得下的最大字級」，它看的是**排版結果**而不是文字內容。兩行各 5 個字塞得下更大的字，一行 11 個字要折行只能用小字 ⇒ **作者眼中「同一句話」的兩種寫法，在 best-fit 眼中是兩種不同的排版問題**。<br>**順手查出同一家族的第二個來源，而且更難發現**：底板是兩張水墨泡泡**隨機輪流**，而兩張的奶油內文區大小不一樣（220×98 vs 200×83）⇒ **同一句台詞每次講都可能是不同字級**，完全沒有規律。作者八成也感覺到了「忽大忽小」，但這條光看台詞是查不出來的。<br>**解法**：改成自己算（`ComputeFontSize`）——① **參考框固定用兩張底板中較小的那個**（抽到哪張都一樣大，而且大的那張一定塞得下）；② 中日韓字寬 ≈ 1 個字級、ASCII ≈ 0.55、行高 ≈ 字級 × 1.15，從上限往下找第一個塞得下的；③ **有手動 `\n` 時先跑一輪「每段各佔一行」**——沒有這輪的話，字級會被挑到「那一段還要再自動折一次」的大小，手動排的兩行變成三行、斷在作者不要的位置，正是他用 `\n` 想避免的事；④ `verticalOverflow = Overflow` 兜底，估不準寧可溢出一點也不要被裁掉。<br>**驗算**（參考框 200.6×82.7）：兩種寫法都得到 **35**，作者的六句台詞落在 22~35。順帶得出**填台詞的字數感**已寫進 MONSTER_SPEECH：**每行 5~6 個字最一致**，一行 9 個字就會掉到下限 22（那時卡的是寬度不是高度，所以只調 `MaxFont` 沒有用，要放大得調 `BubbleWidth`）。<br>**通則**：**「自動縮放到剛好塞滿」這類 API 的輸入是排版，不是語意。** 只要作者能用不影響語意的方式（換行、標點、空格）改變排版，同一段內容就會得到不同大小，而作者的心智模型是「這句話有多長」。要一致就得自己定規則：**先決定「什麼東西應該決定大小」，再讓其他東西不影響它。**

* [x] **Skip 收成全遊戲一套：對話與劇情演出都能跳，樣式與開關規則統一**（2026-08-22，見 [UI_SYSTEM.md](UI_SYSTEM.md)「Skip 的統一樣式」、[DRAMA.md](DRAMA.md)、[CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md)、[PROBLEMS.md](PROBLEMS.md) **J2**）：作者要「劇情對話」與「劇情演出」都能跳過，各自一個編輯器勾選、預設可跳、**外觀跟序章那顆一樣**。<br>**樣式抽成單一真相** `Assets/Scripts/UI/SkipButton.cs`（右上角、白色粗體 78px、黑外框、無底板），序章 `IntroComicController.BuildSkip` 也改成呼叫它——不然「統一樣式」三天後就會各自漂移。提供兩個入口：`Create`（建在既有 canvas 上）與 `CreateOverlay`（自帶 Canvas＋Scaler＋Raycaster，給不是 `UIPanel` 的表演用）。<br>**① 對話（`TalkPanel`）**：`drama` trigger 新增 `canSkip`（Bool，預設 true）。**只有一句的群組不顯示 Skip**，即使勾了也一樣——按 Skip 跟按下一句同義，多一顆鈕只是噪音。**略過＝關閉面板**，不必為它另外接鏈：`OnClose` 本來就會 `TriggerChain.NotifyDramaClosed()`，所以行為與「一句一句點到底」完全相同（有 `next` 接 `next`、沒有就結束）。兩個實作細節：Skip **必須建在最後一個 sibling**（`TalkPanel` 有一片全螢幕「點擊換下一句」鈕，排錯順序按了只會換頁），以及**與換頁共用防連點**（上一段對話的連點慣性剛好落在 Skip 上把整組跳掉是最糟的誤觸）。<br>**② 劇情演出**：`skippable` 欄位早就有，但一直被 `DevSkip` 鎖成開發限定。改成玩家可見：右上角 Skip 覆蓋層（sortingOrder **5100**＝壓在置中漫畫 5000 之上、也在對話面板 Window=100 之上，**播對話時也按得到**），ESC 同效。按下時**連同開著的對話面板一起關掉**——不關的話「跳過」看起來像沒反應，玩家還得自己把對話點完。<br>**刻意保留「略過＝快轉到結局」**（仍執行 `end` 交棒與 `setFlag`，見 **J2**）：那正是玩家按 Skip 想要的——跳過表演、流程照走。編輯器面板勾了「可略過」時會直接把這句提示寫在下面，免得又被當成 bug。<br>**`DevSkip` 的定位跟著改寫**：以前它是「跳過劇情」的總開關，現在**能不能跳由資料決定**（作者逐段勾選、玩家可見），`DevSkip` 只留給「作者沒開放、但開發時想硬跳」的路徑（對話面板的 ESC）。這條寫進了 DevSkip.cs 的檔頭，否則下一個人會照舊把新的 skip 也包進 `DevSkip.Allowed`。<br>**演出裡的 `dialogue` 步驟一律不給對話自己的 Skip**（`DramaTalkController.Play(group, allowSkip:false)`）：那時右上角已經有演出的 Skip，兩顆疊在同一個位置、語意還不一樣（跳一段對話 vs 跳整段演出）。<br>**⚠ 一個例外，作者實測後補的**：接上之後作者回報「開場第一句只有一句話卻出現 Skip」——查下去那顆**不是對話的 Skip 而是演出的 Skip**（`Main_InitialForest1` 的演出第 2 步就是 `dialogue`，而該段 `skippable` 預設為 true）；對話端的一句話規則其實有生效（而且雙重確定：演出裡的 dialogue 一律傳 `allowSkip:false`，就算傳 true 也會被 `Count>1` 擋掉）。兩顆按鈕同位置同外觀（正是「統一樣式」的代價），所以看起來像 bug。<br>作者據此拍板**序章整段是全遊戲唯一的例外**：初始森林 1 → 初始森林 2 →（那段演出）→ 墜落動畫 1/2 **正式版全程不顯示 Skip**、開發階段照跳；**初始洞窟(11) 起回到一般規則**。判斷收成 `DevSkip.SkipAllowedHere`（地圖清單＝新增的 `SaveConstants.IsNoSkipMap`），三個 Skip 入口都查它；序章漫畫/墜落原本各自寫的 `AllowSkip` 也改成 `DevSkip.Allowed`，讓「序章不給跳」只有一份實作。<br>**`IsNoSkipMap` 刻意與既有的 `IsIntroCutsceneMap` 分成兩支**（即使目前地圖清單一樣）：那支問的是「要不要顯示血球 HUD」，這支問的是「能不能跳過」——兩件不同的事，之後只要有一張圖想「不顯示 HUD 但可以跳」，共用一支就會被迫一起改。<br>**也刻意不用「把那兩張圖的可略過取消勾選」來做**：那樣連開發時也跳不了，而反覆測後段流程正是最需要跳的時候。這條規則要的是「正式版不給、開發版給」，那是 `DevSkip` 的語意，資料開關表達不了。<br>**通則**：**「同一個功能在三個地方各做一次」的成本不在第一次寫，在第四次改。** 這次把樣式收成一支、把「誰能跳」的規則從程式（DevSkip）搬到資料（編輯器勾選），之後要加第四個 Skip 只剩接線。另一條：**統一樣式的代價是「不同語意的東西長得一樣」**——同位置同外觀的兩顆 Skip 撞在一起時，玩家（和作者）沒辦法分辨，所以要嘛讓它們不同時出現，要嘛接受並記錄下來。

* [x] **進場「場景說明」：走進有名字的地圖時跳一次場景名**（2026-08-24，見 [SCENE_TIP.md](SCENE_TIP.md)、[MAP_SYSTEM.md](MAP_SYSTEM.md) §2）：作者要「剛進入一個場景時跳出場景名」，且**只從邪佛廣場之後開始**——初始森林 1/2、初始洞窟不跳，目前只有邪佛廣場與紅嫁衣。素材（金色毛筆字 tw/en ＋ 共用的血紅分隔線底版）作者已備好。<br>**先討論出來的一件事：那條「地圖 → 文字圖」的線要牽在哪。** 檔案 `SceneTipPanel_Text_BuddhaSquare.png` 躺在 `UI/Texts/` 裡，沒有任何地方說它屬於哪張地圖。評估過四種：MapsTable 加欄填 key／不加欄直接用現成的 `Name` 欄推導／加欄填完整路徑／另開一張 SceneTipTable。**選了「加欄填 key」**，關鍵理由是**`Name` 和圖名本來就是兩件事**——`Main_Square` 是程式/檔案的內部名、`BuddhaSquare` 是美術命名，作者自己就沒把圖取名成 `Main_Square`。焊在一起的代價是「地圖檔改名 → 圖安靜地不出現」，這種錯最難查。填完整路徑則是把規則攤進資料，之後搬資料夾要改每一列；前綴 `UI/Texts/SceneTipPanel_Text_` 因此寫死在 `SceneTipPanel.TextPathPrefix`，**規則留程式、CSV 只填會變的那一段**。<br>**去重規則被一個地圖表事實逼出來**：一開始想的是「每進一個 module 顯示一次」，但查 MapsTable 才發現**邪佛廣場(12)、初始洞窟(11)、初始森林 1/2(13/14) 全部屬於同一個 `Main` module**——開場那條路線整段是「房間互跳」不是「換關卡」，用 module 當粒度的話廣場永遠會被前面三張圖吃掉。所以規則改成**「進到一張有填 SceneTip 的地圖就跳、同一趟關卡內同一個 key 只跳一次」，去重用 key 不是地圖 id**。這個選擇順手解掉第二個問題：紅嫁衣 10 個房間**全部填同一個 key** 就成立——不管玩家先走進哪一間都會跳、之後房間互跳都不跳，而且**不必去猜玩家實際從哪一間進關卡**（`IsLevelStart` 指的是客廳2，但 `StartLevel` 全專案只有開機測試路徑在呼叫，正式是從廣場傳送進去的，進哪一間並不由那一欄決定）。紀錄是 `MapManager` 的一個 `HashSet<string>`，跨 module 時清空、**不進存檔**。<br>**顯示時機掛在 `FireEnterTriggersRoutine`**：那條協程已經是進圖後的等待鏈（等睜眼醒來 → 等趴地起身 → 等進場自動劇情 → 才點火進場觸發），場景說明插在「等完劇情、點火之前」。**刻意不另開一支協程**——「等過場播完」的邏輯只該有一份，另寫一份遲早漂移。⚠️ **代價是那支協程開頭的 `if (regions == null) yield break;` 必須往後搬**：原本沒有觸發層的地圖會整支跳掉，場景說明就跟著不見（這是加功能到既有協程時最容易漏的一種——早退條件是為舊用途寫的）。<br>**版面用「高度」而不是「寬度」定尺寸**：`邪佛廣場` 四字、`紅嫁衣` 三字，用寬度定尺寸的話後者的字會大一大截。量過兩張圖的「金字佔整張圖的比例」幾乎一致（寬 0.89/0.87、高 0.74/0.75），所以同一個 `TextHeight` 兩張都對得起來——**這也成了之後畫新場景名圖的規範：維持這個留白比例就不必逐張調版面**。預設值是從作者給的示意圖量出來的（金字寬佔畫面 30.8%、中心在畫面高 33.8% 處），節奏與版面全部做成 Inspector 欄位，實機再調。<br>**通則**：**「A 要對應到 B」時，先問「A 和 B 是不是同一件事的兩個名字」。** 是的話可以推導（省一欄）；不是的話就得有一欄明寫，硬推導＝把兩個獨立的命名空間焊死，將來其中一邊改名就會**靜默失效**。另一條：**去重的粒度要挑「使用者心中的那個單位」，不是資料表剛好有的那個欄位**——這次資料表有 `Module` 可用，但玩家心中的「一個場景」跟 module 不是一回事。<br>**後續（同日）**：作者實測回報「進紅嫁衣時初始對話已經觸發了，名字還在上面秀」。**根因是我把它掛在點火之前、卻沒有等它**——名字在 `Overlay` 層、對話在 `Window` 層，於是名字蓋在對話上。改成 **`PausesGame=true` ＋ `BlocksGameplayInput=true`，且 `FireEnterTriggersRoutine` 等它整段播完才點火**（作者拍板「先秀完這元件，再開始遊戲中的機制」）。⚠️ **等的是新加的 `IsPlaying`（讀 `gameObject.activeSelf`）而不是 `IsOpen`**：`IsOpen` 在 `DoClose` 第一行就變 false，那時淡出**才剛開始**，等它等於「名字還看得見就放行」，只是把疊圖變淡而已。**刻意不做成「只鎖輸入、不暫停」**：紅嫁衣一進場就有怪，鎖輸入不暫停＝站著挨打。**停留秒數同時從 2 秒壓到 1.5 秒**（總長 2.6 秒）——整段暫停之後，「邪佛廣場每過一關/死一次回來都會再跳一次」的重複成本才是主要代價，三四個字 1.5 秒看得完。**通則**：**把一個表演插進既有流程時，「插在哪一行」和「要不要等它」是兩個決定，而後者很容易漏。**順序對了不代表不會疊——只要沒等，兩件事就是並行的；而 UI 分層會讓「後發生的」被「先發生的」蓋住，症狀看起來像插錯位置。<br>⚠️ **還沒實機驗證**（我這邊開不了 Unity）：字級/位置/節奏都要作者跑一次再調。另外紅嫁衣那兩張原圖寬 2113/2172 **超過匯入設定的 Max Size 2048**，Unity 會縮到 2048 才用（目前顯示尺寸遠小於此，看不出差別）。

* [x] **RecipeTable 大改：一列一種 `Mode`、欄位依名字讀、能力珠依模式過濾＋鍛造「提示不擋」**（2026-08-26，見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)（全文重寫）、[RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md)、[GEM_SOCKET.md](GEM_SOCKET.md)、[PROBLEMS.md](PROBLEMS.md) **L1～L3**、**D23**）：作者說配方表「欄位多到自己不會填」，而且佛光這種以角色為中心的武器鑲了反彈珠也沒用、希望鑲嵌時能事先警告。研究後發現三個結構性原因：① 51 欄裡有 10 個 `IsXxx` 旗標，互斥只靠 `PlayerController` 的 if/else 順序（Summon > Laser > Aura > … > Normal），兩個都填 1 靜默取優先者；② **同一欄在不同模式語意不同**——`Speed` 在拋物線是飛行秒數、`MaxBounces` 在連鎖是跳數、`HomingTurnSpeed` 在連鎖是錐半角、在落雷是**世界單位的搜尋半徑**（Lv3 追蹤珠 +300 ＝ 整張圖吸附，bug 級）；③ 前 14 欄 `float.Parse` 強制必填，所以佛光也得填 `Radius=0.1, LifeTime=1` 佔位（33 列有 19 列是這種佔位值）。<br>**拍板的決定**：`Mode` 單欄並**直接刪掉** 10 個 `Is*`（不留過渡期）；解析器改依表頭名稱取值；語意錯位改成獨立欄（`FlightTime`／`ChainCount`／`AimConeAngle`／`SnapRadius`）；`BeamRange→Range`、`BlastRadius→AreaRadius`（語意一致的共用欄保留一欄）；刪從沒實作過第二個值的 `GroundEffectTrigger`；`SplitTiming` 留空＝OnSpawn；鑲嵌**提示不擋**；迅捷珠 × 拋物線換算成縮短飛行秒數；反彈珠對連鎖改為無效。<br>**架構上的一個關鍵轉折**：作者原本想把「預覽武器效果」做在地圖編輯器，那會逼出「發射邏輯抽成兩個 Unity 專案共用的套件＋ `IWeaponWorld` 介面」這種最大最危險的改動（2140 行的 PlayerController 要拆）。討論後改成**做在遊戲端**（測試地圖＋仿 L 鍵作弊面板），發射邏輯原地不動，只留三個掛點：`RecipeEntry.FromFields`（CSV 與面板共用的唯一建構入口）、`WeaponManager.SimulationOverride`（`GetCurrentWeapon` 優先回它，所有發射路徑自動吃到）、`WeaponModeSpec` 帶每欄型別／預設／範圍（面板產輸入框用）。**通則：先問「這功能一定要在那個工具裡嗎」，答案常常是換個地方做就少掉一整層架構。**<br>**單一真相 `WeaponModeSpec`**（`Assets/Scripts/Weapon/WeaponModeSpec.cs`）：每欄的規格＋每種模式吃哪些欄／哪些必填／欄位在該模式叫什麼，三處共用——載入檢查（無效欄有值 Warning、必填缺 Error）、珠子有效性（`PlayerAbilities` 套用時過濾、鍛造介面提示）、未來模擬面板。**而且 `RecipeEntry.FromFields` 對無效欄根本不讀**（不只是警告），所以表上填錯不可能改變行為。加欄／加模式只改這個檔。<br>**沒有 Unity 也要驗到底**：`CsvTable`／`WeaponModeSpec`／`RecipeEntry` 刻意寫成不依賴 UnityEngine，在 Cowork 容器裡用 mono `mcs` 編譯跑單元測試；CSV 轉換用 python 做「舊解析語意 vs 新解析語意」33 列逐列比對（0 差異，火焰噴射的 `HasSplit/OnSpawn` 是已知無作用的例外），再用 C# 實際解析新表 dump 出來跟舊語意比第二次。**這兩層驗證抓到三個真 bug**：mcs 不吃區域函式（改 lambda，Unity 本身沒差）、`WeaponModeSpec` 的共用欄組陣列宣告在 `_modes` 之後 → 靜態初始化時是 null（**D23**）、雷射的 `SpreadCount` 一欄兩用（道數＋命中分裂數）而 `SplitTiming` 只認 OnHit（**L2**）。<br>**鑲嵌為什麼只能「提示不擋」**：珠子可以鑲在護身符／戒指上跨裝備疊加到當前武器（GEM_SOCKET §3），所以「有沒有效」是**珠子 × 參考武器**——鐵砧上是武器就看那把、是防具就看目前裝備的武器，換武器答案就變；擋死會讓同一顆珠子時而能鑲時而不能。做法收在 `GemEffectiveness.cs`：拖進孔位 toast、孔位灰顯（`ForgeSlotWidget.Dimmed`，顏色設在 `ItemIcons.Apply` 之前讓珠子符號跟著壓暗）、tooltip 兩處說明、背包 tooltip 鑲嵌清單標「（對目前武器無效）」；語言表 4013～4016。<br>**改動範圍**：新增 `CsvTable.cs`／`WeaponModeSpec.cs`／`RecipeEntry.cs`／`GemEffectiveness.cs`；重寫 `RecipeManager`／`WeaponManager`／`PlayerAbilities`；`PlayerController` 只改引用（分支鏈改 `switch (Mode)`）；`ProjectileData` 加 `FlightTime`；五張表（Recipe／Weapon／Gem／GroundEffect／Vfx）全部換成依表頭取值；RecipeTable 51→45 欄依模式分群、WeaponTable 分群重排（欄名不變）。**通則**：**「欄位不會填」通常不是文件問題，是同一個名字被拿去裝好幾種意思**——先把語意拆開（一欄一義、一列一模式），文件自然就短了。另一條：**只要規則能寫成純 C#，就把它從 MonoBehaviour 裡拆出來**，這樣沒有 Unity 的環境也能驗，而這次三個真 bug 全是這樣抓到的。<br>⚠ **還沒實機驗證**（我這邊開不了 Unity）：驗證清單見對話交付；御靈水晶（召喚）武器表填了 `WeaponSpritePath` 會印一條「對召喚模式無效」的 Warning，是預期行為，要不要清那一格由作者決定。

* [x] **武器工坊：Unity EditorWindow 版的「選外型→選模式→填效果→立刻射出去看」，一鍵存回 CSV**（2026-08-26，見 [WEAPON_WORKBENCH.md](WEAPON_WORKBENCH.md)）：RecipeTable 大改驗證通過後，作者要「快速正確地產生武器」——先選外型、系統幫忙處理配方（下拉／互斥欄不能填／數值提示）、合二為一，還要能當場鑲珠子測、配特效表做「擊中敵人會起火的飛劍」。他提了兩案：地圖編輯器加武器編輯（存檔→Sync→進遊戲測）、或遊戲內 uGUI 面板即時測。<br>**選了第三案：`EditorWindow`**。判斷依據是「改一個數值到看見效果有多遠」與「編輯 UI 要花多少」——地圖編輯器案把回饋迴圈拉長到分鐘級、還得把 `WeaponModeSpec` 鏡像一份到編輯器專案（同 SceneFx 三支的複製慣例，之後加欄要兩邊改）；uGUI 案每個下拉／數值框都要全程式手刻。IMGUI 的下拉／滑桿／灰掉／HelpBox 一行一個，Play 中照樣能開、能直接碰場景裡的 `WeaponManager`、能 `AssetDatabase.ImportAsset` 寫回 CSV；這是作者一個人用的開發工具，不進 build，用 Editor 專屬 API 完全正當。專案本來就有 `Project Tools` 選單一家（BuildScript／Sync Map Assets／Split Sprite Sheet／直接進關卡），它排在旁邊很自然。<br>**資料模型刻意是「欄名 → 值」字典，不是 `RecipeEntry`**：`RecipeEntry.FromFields` 是單向的（字典 → 物件），存檔需要反方向；與其寫 `ToFields()` 再維護一份對稱邏輯，不如讓視窗編輯的就是 CSV 那一層——讀進來是字典、改的是字典、存回去也是字典，`FromFields` 只在「套用模擬」那一刻才跑。表頭與 `#` 分組註解由新加的 `WeaponModeSpec.HeaderCells`／`GroupCommentLines` 產生，所以存出來的表頭永遠跟程式一致；`CsvWriter` 用 mono 做過 round-trip（讀 → 寫 → 讀，33 列字典完全相等）。<br>**模擬武器要吃到真鑲的珠子**：作者說鑲嵌會「隨機亂鑲」、勾選項目做不完，所以不做假鑲嵌，走真的鍛造。原本 `SimulationOverride` 直接回傳、不過 `AbilityResolver`，改成 setter 觸發 `RefreshCurrentWeapon`、一併解析成 `_simResolved`——珠子一變 `RefreshLoadout` 就重算，跟正常武器同一條路。`GemEffectiveness.ReferenceWeapon` 在模擬中一律回模擬武器（否則鐵砧上放的是背包那把、提示會對錯對象）。⚠ 模擬武器不占武器欄，珠子要鑲在背包裝備的武器或防具上——視窗的「開 6 孔」按鈕在武器欄空的時候會直接說這件事。<br>**存檔 ≠ 套用**：`RecipeManager`／`WeaponManager` 在 `Awake` 只載一次，存了檔執行中的遊戲仍用舊表、下次 Play 才生效；刻意不做熱重載（怪物手上持有 `WeaponData` 參照，熱換有風險），「立刻看」的需求由模擬機制涵蓋，兩件事分開反而清楚。存檔時列依 ID 排序、只寫 spec 認得的欄，表頭不認得的欄載入時就警告「存檔會被丟掉」。<br>**刻意不做的**：不產 ItemTable 列（只在驗證區提示「這把武器不能裝備」）、不刪武器／配方（牽動 SubRecipeID／SubWeaponOnHit／ItemTable 的引用，先用 git）。<br>**通則**：**開發工具先問「誰用、在哪用」再選 UI 技術。** 只有作者用、只在 Editor 用，IMGUI 就是最短路徑；為它做 uGUI 或搬進另一個專案，付出的是好幾倍的 UI 成本與一份要同步的規則。另一條（延續上一則）：**規則只寫一份，其他地方「畫」它**——視窗沒有自己的欄位清單，欄位從哪來、必填、範圍、下拉內容全從 `WeaponModeSpec` 讀，所以之後加欄／加模式視窗自動跟上。<br>⚠ **Editor 程式我這邊編譯不了**（Cowork 只有 mono，沒有 UnityEditor），這支要作者開 Unity 才知道有沒有寫錯 API；純 C# 的 `CsvWriter`／`HeaderCells` 已經測過。

* [x] **「巨彈珠」改名「須彌珠」，效果從「子彈變大」擴成「施放大小」——近戰／突進／法陣／落雷／佛光也吃**（2026-08-26，見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) §4）：作者測血月鬼爪時發現近戰武器沒有任何「放大」可調——規格表裡 `BulletScale` 只給會飛子彈的三種模式，近戰只剩傷害與疾發兩顆珠有用，太苛。<br>**改法**：`BulletScale` 語意改成「施放大小」，對非子彈模式**範圍與視覺一起放大**（近戰半徑＋揮砍特效、突進寬度＋特效、法陣半徑＋圖、落雷 AOE＋雷柱、佛光光圈、拋物線落地爆炸半徑），所見即所得——刻意不做「只放大特效」，那會重演佛光那次「畫面變大傷害圈沒變」的騙人狀況（法陣走 `GroundEffectManager.Spawn` 的 `radiusScale` 而非 `visualScale`，理由同）。雷射／連鎖不動，粗細本來就是 `BeamWidth`。<br>**一個順手的簡化**：這幾條分支原本用 `CastVisualScale`（集氣快照＝2）放大視覺，現在改讀 `BulletScale`——集氣快照的 `BulletScale` 本來就 ×2，所以集氣時視覺仍 ×2，**範圍也跟著 ×2**（原本集氣只放大視覺），與「所見即所得」一致。子彈類的發射／擊中／軌跡特效仍用 `CastVisualScale`，不隨須彌珠變大。<br>**通則**：珠子的「有效」不只是「程式有沒有讀那一欄」，還要問「這種模式下這個概念叫什麼」——「子彈大小」對近戰無意義，但「施放大小」對每一種模式都有意義；先把概念泛化，再決定有效性，比直接判無效好得多。

* [x] **能力珠第二批：把武器表／配方表「能拆的欄位」全拆成珠子，8 種 → 25 種**（2026-08-26，見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) §4、[GEM_SOCKET.md](GEM_SOCKET.md) §6.5）：作者決定配方表保留不拆，改成把兩張表逐欄檢視、能疊數值的欄位每個做成一顆珠（連射程、存活時間都要），外型先借現有符號。<br>**新增 17 顆**（GemTable 9~25、ItemTable 409~425）：綿延 `LifeTime`、遠射 `Range`、廣角 `SpreadAngle`、群環 `OrbitalCount`、環距 `OrbitalRadius`、頻擊 `DotInterval`、連鎖 `ChainCount`、引雷 `ChainRadius`、鎖敵 `AimConeAngle`、吸落 `SnapRadius`、群召 `SummonCount`、眾生 `SummonMaxAlive`、廣刃 `MeleeAngle`、長驅 `DashDistance`、聚氣 `ChargeTimeReduction`、省魔 `ManaCost`、粗束 `BeamWidth`。有效性照舊由 `WeaponModeSpec` 推導，鍛造「提示不擋」自動涵蓋。<br>**程式只改兩處**（`PlayerAbilities`）：`Range` 加 ≥0 守門（雷射 -1 無限不能被珠子加成有限）、補 `ChargeTimeReduction` 的套用（原本累加了沒人讀）。<br>**刻意沒拆**：`Radius`／`AreaRadius`／`DashWidth`／`FlightTime`（與須彌、迅捷重疊）、純視覺欄、加了會變差的欄（落點散布、軌跡間距）、ID／枚舉／開關類（那是「附魔」不是疊數值，另議）。理由寫在 RECIPE_DESCRIBE §4。<br>⏳ icon 全是暫借的（TODO 已記）；數值只是起手值，平衡上限仍待一起討論。

* [x] **能力珠圖鑑 [GEM_CATALOG.md](GEM_CATALOG.md)：25 顆每一顆的功用、三級數值、拿現有武器算的範例、坑與流派搭配**（2026-08-26）：作者要求把所有珠子的功用與使用範例詳細寫進文件。同時把「改珠子數值或加珠子要同步更新圖鑑」寫進 AGENTS.md 路由。圖鑑裡順手記了幾個平衡上的觀察：銳利珠固定值對高傷慢速武器沒感覺、三分裂追蹤飛劍配方已填集氣縮減 90% 再鑲聚氣珠會接近零集氣、鎖敵珠對已填 180 的連鎖閃電是浪費孔。

* [x] **連擊：扣一次扳機連射 N 發（`BurstCount`／`BurstInterval`），附加在任何離散模式上，＋連擊珠**（2026-08-26，見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) §3.13、[GEM_CATALOG.md](GEM_CATALOG.md) #26）：作者發現漏做很多遊戲都有的「連發」。研究後決定**不做新 Mode**、做成像集氣那樣的附加欄組（連擊是「怎麼扣扳機」，跟「射什麼」正交；做成 Mode 會把 11 種模式每種再乘一次）。<br>**規則**：第一發照常，之後每 `BurstInterval` 秒自動補一發，不看按鍵不看冷卻，**魔只在扣扳機那發扣一次**（作者實測後拍板：一份魔力連射 N 發，否則跟按著連射沒差別）、每發各自分裂；召喚滿了中止；冷卻 `FireInterval` 從最後一發算起；集氣放開整串 ×3；換武器／換圖／禁武作廢。Laser／Aura／Orbital 無效（環繞每次施放會清上一組，連擊等於白射）。<br>**動到**：`WeaponModeSpec`（欄組 `Burst`，8 個離散模式 `.Eff(Burst)`）、`RecipeEntry`（兩欄）、`PlayerAbilities`（套用＋夾值 ≤16 發、≥0.02 秒）、`PlayerController`（`_burstRemaining／_burstTimer／_burstWeapon`、`AfterShot()` 把兩條路徑的 `_fireTimer` 設定收成一處、`CancelBurst()` 掛在禁武／換武器／換圖／銷毀）。RecipeTable 加兩欄＋分組註解（47 欄）；測試用配方 43「三連擊直線彈」＋武器 30「三連飛劍」（ItemTable 30，外型借飛劍）；連擊珠 GemID 26／ItemTable 426（icon 暫借 rapid）。<br>純 C# 部分（規格／解析／CSV）在 Cowork 用 mcs 驗過：11 種模式有效性正確、表頭 47 欄、配方 43 讀出 3／0.08、Orbital 填了 BurstCount 會 Warning 且不讀。`PlayerController` 要進 Unity 才知道。<br>**同日修**：作者測了連擊；① 耗魔改成只扣一次；② 武器工坊改不了名字——名稱欄從 `TextField` 換成 `DelayedTextField`（按 Enter／離開欄位才提交；即時版每一幀把 Trim 過的值塞回去，中文輸入法組字中的字串會被打斷，怎麼打都留不住）。③ 換成 Delayed 後作者又回報「編輯器改了、CSV 沒改」——改完名直接點儲存，按鈕不搶焦點、欄位還沒提交就存了舊值。儲存鈕改成 `RequestSave`：先 `GUI.FocusControl(null)`、下一次 OnGUI 的 Repaint 尾端才 `SaveAll`（`FlushPendingSave`）。④ 連擊中途移滑鼠後面幾發會轉向 → 起連擊時鎖住方向與落點（`_burstAimLocked`），五條離散發射路徑（一般／法陣／拋物線／連鎖／落雷）全改走 `AimWorldPoint()`／`AimDirectionToMouse()`，不再各自讀 `Input.mousePosition`。

* [x] **平行彈：同方向並排 N 道（`ParallelCount`／`ParallelSpacing`／`ParallelMaxWidth`）＋平行珠**（2026-08-26，見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) §3.14、[GEM_CATALOG.md](GEM_CATALOG.md) #27）：作者從弓箭傳說那類遊戲挑了 Front Arrow，先問「15 道平行會不會有問題」。研究出三個真問題：① 畫面高 10 單位、15 道 × 0.45 ＝ 6.3 單位寬，往上下射半排生在畫面外；② 並排出生點會落在牆裡，被 `CheckSpawnOverlap` 瞬殺（走廊裡射 15 道可能 13 道一出生就沒了，同 B5 那家）；③ 平行 × 分裂 × 連擊相乘，15×5×5＝375 顆會卡。<br>**拍板**：全部從玩家出生、飛出去 0.15 秒「散開再拉直」（新 `LaneBehavior`，側向速度線性衰減；走速度向量所以 CircleCast 碰撞照常）；總寬鎖 `ParallelMaxWidth`（預設 3）超過就壓縮間距；一次扣扳機總顆數 ≤ 128（`MaxBulletsPerTrigger`），超過砍道數並 Warning 一次。只有 Normal／Parabolic（拋物線＝落點並排）。<br>**彈道系統的一個小掛點**：`BallisticsEngine.Spawn` 多一個 `extraBehavior` 工廠參數（每顆子彈一個新實例），`BulletInstance.SpawnExtraBehavior`／`IsSpawning`；`Internal_SpawnSplit` 在母彈 `IsSpawning` 時把工廠傳給子彈——這樣 OnSpawn 分裂的整排才會一起散開，OnHit／OnDeath 分裂（在遠處）不繼承。發射端額外行為排在配方行為之後，分裂先用純前進方向產子彈、側向速度才加上去。<br>測試：配方 45「三道平行直線彈」＋武器 32「三道飛劍」（ItemTable 32）；平行珠 GemID 27／ItemTable 427（icon 暫借 split）。RecipeTable 50 欄。純 C# 部分 mcs 驗過（有效性、50 欄、配方 45 讀出 3／0.45／3、Chain 填了不讀且 Warning）；`LaneBehavior`／`BallisticsEngine`／`PlayerController` 要進 Unity。

* [x] **玩家攻擊動畫只播到「動作最大幀」——快武器點一下不再原地演完 2 秒收勢**（2026-08-26，見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md) 攻擊動畫 2b）：作者發現飛劍點一下，劍飛到畫面另一頭了角色還在原地做 attack（25 幀 12fps ≈ 2 秒，子彈卻在按下那幀就出去）。討論了四條路（放開就切／播到最大幀／最短撐 FireInterval／把子彈延到出手幀），作者又提到 AutoSprite 做的序列圖常多出第二拳、手放到別處之類的無謂動作，於是拍板：**attack 的有效段就是起播幀 → 最大幀**，後面當不存在；點一下播完這段回 Idle，按住照原規則（重播／定格在最大幀），移動優先不變。<br>**最大幀怎麼來**：`PlayerSpriteLibrary` 算起播幀時本來就有每一幀跟站姿的輪廓差異曲線，結束幀＝第一次到峰值 90%（`ActionEndPeakRatio`）那格＋1 幀尾巴（`ActionEndTailFrames`）；同一次掃描、同一份快取（`ComputeActionRange`）。取「第一次到 90%」不取嚴格最大：兩拳的圖第二拳若更開會抓錯。作者拍板**不加血統表覆寫欄**（看不出第幾幀，重做圖比較快）。<br>改動：`PlayerSpriteLibrary.GetActionEndFrame`／`GetActionFrameCount`／`BloodlinesWith`；`PlayerAnimator._attackEnd`／`AttackEndFrame`，`Advance` 的 Attack 上限改成結束幀；`HandleVisuals` 不用動（`IsAttackPlaying` 在結束幀就轉 false）。新選單 `Project Tools → 角色 → 攻擊動畫幀數報告`（`Editor/AttackAnimReport.cs`）印每個血統的總幀／起播／結束。⏳ 未實機驗證，各血統抓到哪一幀先跑報告看。

* [x] **修「毛殭的飛劍從腹部飛出」——武器出手點改成依可見身高算，不再釘在 transform**（2026-08-26，見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md) 武器出手點）：作者換血統測試，殭屍正常、毛殭（BodyScale＞1）的劍從肚子出來。原因：所有發射路徑的出生點都是 `transform.position`，那對人類體型剛好是身體中心，體型放大後身體往上長、transform 不動（同集氣光圈那次的問題，那時只修了光圈）。<br>改法：新增 `MuzzleWorldPos`＝可見腳底＋可見身高 × `MuzzleHeightRatio`（0.5，人類體型與 transform 幾乎重合所以殭屍不變），子彈／雷射／連鎖／環繞圈／發射特效／瞄準方向全改走它；近戰／突進／法陣的範圍中心刻意留在 transform（站的位置）。⏳ 未實機驗證。

* [x] **修「主角一出手就長大一圈」——walk/attack 對齊 idle 改量「體積尺度」、掃全幀取中位數**（2026-08-27，見 [CHARACTER_SETUP.md](CHARACTER_SETUP.md) 顯示高度段、[PROBLEMS.md](PROBLEMS.md) **G7**）：作者回報 Base 攻擊時角色明顯變大、收手縮回去，AutoSprite 各動作的大小抓不準；希望每個血統的每個動作都以**該血統自己的 idle** 為準對齊（不是跨血統一樣大）。<br>**先查現況**：程式 8/18 起就有「逐動作高度正規化」，所以不是沒做，是量錯了東西——量的是可見高度、而且只量第一幀。Base 的 attack 高度只多 4% 但整個人粗了 14%；Nightborn 的 attack 是蹲姿，高度反而矮 → 被放大。七組血統 25 幀全量測後，試了三種指標並排對照圖（純高度／純面積／幾何平均）：純面積對 Base、Nightborn 最準，但對 Jiangshi 這種「身體沒變大、只是手伸很長」的會多縮 10%；**幾何平均是折衷**（Base 0.917、Jiangshi 0.945、Nightborn 0.961、其餘 0.99~1.06）。<br>**改法**：`PlayerSpriteLibrary.GetActionSize`（掃全幀、每幀量可見框高與 √不透明像素數、各取中位數、快取）、`PlayerAnimator.StateTile` 改成 idle 尺度 ÷ 該動作尺度；`AttackAnimReport` 多印 walk/attack 的縮放與原始量測值。絕對顯示高度仍由 idle 高度決定（跨血統的 `BodyScale` 規則不變）、dead 照舊不正規化、幾何快取（`_visH`／`_footRel`）沿用同一組 tile。<br>**刻意不做**：血統表手填 `AttackScale` 欄（作者對起播幀已拍板「抓歪了重做圖比找幀號快」，照同一精神）、離線縮圖（放大會撞畫布邊、每加血統要記得跑）。**沒一起做**：各動作腳底對齊（現在各動作置中 pivot、縮放不同時腳底會偏——Nightborn 切 idle→attack 約 0.13 單位）、怪物端 `MonsterAnimator.StateTile` 同樣只量第一幀高度，兩者都待作者拍板。<br>**通則**：**「已經正規化過了」要追問「正規化的是哪個量、量的是哪一幀」**——高度只是大小的一個投影，姿勢一變就跟「這個人有多大」脫鉤；對齊大小要配一個對姿勢不敏感的量。另一條：**用中位數當代表幀，不要用第一幀**——序列圖的第一幀常是起手，剛好最不典型。⏳ 未實機驗證（Cowork 開不了 Unity）：進 Play 先跑 `Project Tools → 角色 → 攻擊動畫幀數報告`，對照上面那組數字；再看 Base 出手是不是不再長大。

* [x] **場景說明改成「先跳名字、再演劇情」——進圖自動劇情等進場等待鏈放行才開始表演**（2026-08-27，見 [SCENE_TIP.md](SCENE_TIP.md) §3、[CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md)）：作者把紅嫁衣首張圖設成書房（有進場自動劇情）實測，結果先演劇情、演完才跳「紅嫁衣」，覺得順序反了。8/24 做場景說明時是刻意插在「等完劇情之後」的（怕名字蓋在過場上），這次作者拍板反過來。<br>**為什麼不是單純把兩段對調**：直覺做法是把 `MaybeAutoStart` 從 `PlaceAndSetup` 搬進 `FireEnterTriggersRoutine` 的「名字之後」，但劇情開演的前置（鎖輸入／藏主角／關血量 HUD／擺演員）都在 `Run` 開頭做，搬過去就變成名字跳的 2.6 秒畫面上「演員還沒擺位、該藏的主角站在那裡、HUD 還開著」，名字淡出後演員才憑空出現。所以改成**開演不動、只把「開始跑步驟」延後**：`MapManager` 加 `EnterSequenceBusy`（`PlaceAndSetup` 設 true、等待鏈在名字整段播完後設 false），`CutsceneDirector.Run` 在 `BuildActors` 之後、第一個步驟之前等它放行；等待鏈的順序改成 進場特效 → 趴地起身 → 場景說明 → 放行劇情、等它演完 → 進場觸發。<br>**兩個小坑**：① 劇情端不能直接輪詢 `SceneTipPanel.IsPlaying`——名字是載入頁關掉後幾幀才 `Show`，那幾幀 `IsPlaying` 是 false，劇情會照樣先跑；要等的是「等待鏈走到哪」，不是「面板開著沒」。② 等待鏈每一條提早 `yield break` 前都要放行，否則換圖中止時旗標卡 true、下一張圖的劇情永遠不開始；`LoadMapRoutine` 開頭也多一道保險放行。沒有 `SceneTip` 的地圖（開場山道 13/14）劇情只晚 1~2 幀開始，且那幾幀舞台已就位、輸入已鎖，看不出來。<br>**通則**：**「開演」和「開始表演」是兩個時間點。** 準備工作（藏人、擺位、鎖輸入）要在切圖的第一幀做完才不露餡，真正的表演才可以往後排；要調整過場的先後順序時，動的是「開始表演」那個點，不要把整段搬走。<br>**後續（同日，推翻上一段）**：作者實測後說不對——他要的不是「舞台先就位、暫停跳名字、再開演」，而是**一進圖先看到正常的遊戲畫面（主角在場、HUD 在、沒演員沒黑邊沒 Skip）→ 跳名字 → 名字淡出後劇情模式才整套出現**。我上一段避免的「演員憑空出現」正是他要的節奏（名字就是劇情模式的開場提示）。於是拿掉 `EnterSequenceBusy` 整套，改成：`PlaceAndSetup` 判定「這一趟要跳名字」（`SceneTip` 有填且 key 沒跳過）時**不呼叫 `MaybeAutoStart`**、記 `_autoCutscenePending`，等待鏈在名字整段播完後才呼叫；沒名字的圖仍同幀開演（零行為變化、主角不露幀）。**通則修正**：上一段的「準備工作要在第一幀做完」只在「玩家不該看到切換前狀態」時成立；當作者要的體驗本身就是「先看到正常狀態、再切進演出」，露出反而是設計的一部分——**先問作者要的畫面節奏是什麼，再決定哪個時間點該動**，不要用「不露餡」當預設目標。⏳ 未實機驗證：進紅嫁衣書房應為 黑幕 → 正常畫面（主角在）→ 名字 → 名字淡出後主角消失、演員出現、Skip、黑邊 → 劇情。

* [x] **修回憶特效「畫面兩側像馬賽克」——柔邊模糊從 4-tap 十字改成 13-tap 圓盤**（2026-08-27，見 [PROBLEMS.md](PROBLEMS.md) **J5**）：作者看紅嫁衣書房劇情的截圖，發現左右邊緣的燈籠、柱子變成一塊塊錯位的方塊。原因是 `MemoryFx.shader` 的柔邊層只取 4 個樣本（中心＋上下左右各 6px），4 個點相距 6px 就不是模糊、是四個重影疊在一起，硬邊物件看起來像格子；中央半徑趨近 0 所以正常。改成中心＋內圈 6＋外圈 6 的圓盤取樣（相鄰點約 3px、權重近似高斯），加 `#pragma target 3.0`。<br>**後續（同日）**：作者決定**關掉邊緣模糊**（`BlurPx = 0`）——npc 走到畫面邊邊整個人被糊掉，這層跟「演員會走到哪」本質衝突；shader 的圓盤模糊留著備用，暈影不動。<br>**通則**：**模糊的 tap 數要跟半徑成比例**——半徑加大而取樣點不變，得到的是重影；驗收看最大半徑處的一個硬邊物件就夠。另一條：全螢幕的「邊緣處理」要先問「演出會不會用到邊緣」。⏳ 未實機驗證：確認 shader 編譯沒紅字（target 3.0）、兩側 npc 清楚可見。

* [x] **改名：夜裔系列 → 血族系列、夜裔（第一階）→ 覓血者、`Nightborn` → `Bloodseeker`**（2026-08-27，見 [BLOODLINE.md](BLOODLINE.md) §1）：作者要求全專案改名。動到：`BloodlineSeriesTable`（Key／DisplayName／Note）、`BloodlineTable`（20 列的 Key／DisplayName／SpriteFolder，21/22 的 Note）、`ItemTable` 302（名稱「血統藥劑・血族」、TipStats「覺醒「血族」血脈」、IconPath）、`BaseBloodRoll` 備註；資料夾 `Characters/SequenceImage/Nightborn` 與 `Characters/Talk/Nightborn` → `Bloodseeker`（GameAssets 與 StreamingAssets 兩邊、含 `.meta` 一起改名保留 guid）、100 張幀檔 `Nightborn-iso_*` → `Bloodseeker-iso_*`、icon `bloodline_Nightborn` → `bloodline_Bloodseeker`、`catalog.json` 路徑字串；程式只有註解（CheatPanel、PlayerSpriteLibrary 的量測數據）。**存檔安全**：存檔存的是血統 Id（20）不是資料夾名，`SpriteFolder` 由表反查，舊存檔不受影響。<br>**刻意不改**：PROBLEMS／PROGRESS／archive 裡的「Nightborn／夜裔」是當時的紀錄，照 DOCS_GUIDE 不改寫；改在 BLOODLINE.md §1 留一句對照，接手的人搜到舊名知道是同一個。**通則**：改名時先分清「識別用的 Key／資料夾名」「玩家看到的 DisplayName」「進存檔的東西」三層——這次三層剛好分開（存檔存 Id），所以能一次改乾淨；若存檔存的是資料夾名就得另做遷移。<br>**後續（同日）**：作者把「血族」（系列層級）的英文正式定為 **Bloodborn**，與第一階「覓血者」的 **Bloodseeker** 分開：系列表 Key 改 `Bloodborn`、藥劑 icon 改 `bloodline_Bloodborn`（ItemTable 302 IconPath 跟著改）；第一階的 Key／外型資料夾／幀檔維持 `Bloodseeker`。⏳ 要作者做：Unity 按 Cmd+R 重匯入（資料夾改名靠 .meta 認得，不會丟 import 設定）；跑一次 `Project Tools → Sync Map Assets` 確認 catalog 與 StreamingAssets 一致；進遊戲喝 302 看 icon、外型與立繪。

* [x] **新增「狂族」血統系列（狼人 → 望月者 → 芬里爾／Feralborn：Werewolf → Moonwatcher → Fenrir）**（2026-08-27，見 [BLOODLINE.md](BLOODLINE.md) §1·§2·§3·§8）：作者把序列圖（三階各 idle/walk/dead/attack 25 幀、256²）、立繪（8 種情緒，除 `normal` 外暫代）與藥劑 icon 放好，要我接上。作者口頭先說第二階叫「覓血者」，跟剛改好的血族第一階撞名、資料夾也是 `Moonwatcher`——問過確認是打太快，第二階是**望月者**。照 §7 做：表A 加 `3,Feralborn,狂族,30,31,32`；表B 加 30/31/32（`BodyScale` 1／1.3／1.5 憑印象、五屬性偏力量體力佔位）；ItemTable 303「血統藥劑・狂族」（`BloodlineID=30`、icon `bloodline_Feralborn`）；BaseBloodRoll 303 權重 10。程式零改動（全專案確認沒有寫死血統名）。<br>**量素材時查到一個真問題**：狼人的攻擊動畫會只播 2 幀、芬里爾 3 幀。起播／結束幀演算法（G6）用「跟 idle 站姿的差異」當動作曲線，前提是 attack 的起手接近 idle；狼人 idle 直立瘦長（204×74）、attack 整段前傾寬站（~170×150），第 1 幀差異就是峰值 100% ⇒ 起播 0、結束 1。望月者正常（起播 9、結束 15）。記在 TODO，兩條修法（重做圖／改成相對 attack 第 1 幀的曲線）等作者拍板。**通則**：**自動演算法的「前提」要在新素材進來時重驗**——G6 那套對七組素材都對，是因為七組的 attack 起手都站著；換一種畫法（整段變身姿勢）前提就破了，而且症狀是「動畫像沒播」，跟 G6 當初的症狀一模一樣、原因相反。⏳ 要作者做：Cmd+R → `Project Tools → Sync Map Assets`（新資料夾要進 catalog／StreamingAssets）→ 跑攻擊動畫幀數報告對照上面數字 → 抽祭壇或作弊面板拿 303 喝下去看三階外型、`BodyScale`、立繪。

* [x] **NPC 系統第一波：編輯器 NPC 分頁＋可交談/閑晃/開介面的中立 NPC**（2026-08-28，見 [NPC_SYSTEM.md](NPC_SYSTEM.md)）：作者要 NPC 系統（放置、原地/來回走動、對話、對話後開介面；未來護送與多方陣營先留餘地）。討論拍板：NpcTable.csv **另開分表**（不混 MonsterData）、開介面用 **panelId 名稱＋參數欄**（沿 openPanel 慣例）、頭上標示用**對話泡泡**（程式畫、零素材）、觸發鏈接口（next/setFlag）第一波就做。<br>**架構上的三個關鍵決定**：① **NPC 建立在怪物地基上**（MonsterController/MonsterAnimator/MonsterActuator/BlobShadow/YSortByFeet 全沿用）＝ A* 巡邏、換圖清場、未來護送的 HP/受擊全部免費；新增陣營 `Neutral`（放 Ally 層：玩家子彈天生打不到）。② **圖沿用 `Monsters/SequenceImage/` 角色圖庫**（劇情演員早已把非怪物角色放這裡）＝三處同步工具、MonsterSpriteLibrary、編輯器預覽（PreviewSpriteLoader 直讀磁碟）**零改動**。③ **「誰能傷誰」收斂成 `FactionRelations.cs` 單一函式**（接觸傷害、友軍找目標都改查它）＝未來三方陣營/玩家變陣營只把它改成查表，呼叫端不動。<br>**互動的接法**：NPC 會移動，不走建點制——`NpcAgent.Active` 登記表每幀比距離（同掉落物模式）；對話沿用 DramaTable（Type 1/2 都通），「對話關閉才續」用新的 `TriggerChain.CompleteAfterDramaAction`（region 版的孿生，給非 trigger 的對話來源）；開介面把 `OpenPanelPoint` 的 switch 抽成 `OpenPanelById` 唯一對應表（祭壇 trigger 與 NPC 共用）。一次性語意：對話可反覆聊、鏈每次進圖只跑第一次（關卡單次）。<br>**編輯器**：新「NPC」分頁（照場景特效/照明模式：清單＋畫布拖曳＋放置模式）；路徑點「連續點畫布加點」、可拖曳；角色預覽所有工具下都顯示（idle 呼吸動畫）；NpcTable 直讀主專案 CSV（面板可重讀）。`.dipanmap` 加 `npcs` 清單，兩專案 NpcInstance 鏡像。範例資料：NpcTable **ID 1**（`Family_Father`，示範村民）。<br>**通則**：加「新種類的場上角色」前先盤點既有地基——這次八成功能是組裝（怪物外觀/導航、劇情對話、互動註冊表、陣營層、編輯器分頁模式全是現成的），真正新寫的只有 brain、agent、分頁與資料層。**後續（同日修）**：作者實測發現走路中的 NPC 會「一下轉向玩家、一下被拉回行進方向」左右抖——最初版讓「玩家走近 2.6 格就轉頭看他」與「走路面向移動方向」兩條規則互搶。作者拍板：**平時完全不看玩家**（走路看移動方向、原地保持原朝向），**按 F 對話那一刻才轉向玩家、對話結束轉回對話前的朝向**再續走。改法：NpcSpawner 的 `DetectionRange` 改 0（sensor 永遠回 null → HandleVisuals 不再被玩家搶面向）、`NpcAgent` 在 Interact 前記住 flipX、對話關閉回呼裡轉回。**通則**：兩條都「合理」的面向規則同時作用時，抖動不是調參數能解的——要指定唯一的優先權（這裡＝行進方向），把「看玩家」壓縮成一個明確的時刻（對話中）。⏳ **要作者做（Unity 一次性）**：GameManagers 掛 `NpcTableProvider`、把 `Assets/Data/NpcTable.csv` 拖進 Npc CSV 欄；重開兩個 Unity 讓新腳本編譯；編輯器擺一個 NPC（角色下拉選 1）＋填一個現有 dramaId → Sync → 進遊戲驗：泡泡/按F交談/對話中站住/結束恢復走動、來回走動會繞家具、暗場景泡泡可見。

* [x] **場景說明加半透明黑幕＋整段縮短 1/3**（2026-08-28，見 [SCENE_TIP.md](SCENE_TIP.md) §4）：作者實測嫌兩件事——名字跳出來時畫面完全正常卻不能動（看不出遊戲被暫停）、顯示時間太長。改 `SceneTipPanel`：① 面板自己鋪一層黑幕（`DimBackdrop`/`DimAlpha`，預設 0.6＝與共用遮罩同濃度）——**不能用 UIManager 共用遮罩**，那張只服務 Window 層（`UpdateBackdrop` 過濾 `Layer == Window`），本面板是 Overlay；掛面板最底層還順便跟 CanvasGroup 一起淡入淡出，照 Overlay 守則 `raycastTarget=false`。② 三段節奏等比 ×⅔：0.5/1.5/0.6 → **0.33/1.0/0.4**（總長 2.6→1.73 秒）。⏳ 未實機驗證：進廣場看名字後面壓黑、約 1.7 秒收掉、淡出跟名字同步。

* [x] **陣營系統第二波：三方陣營劇本地基（狼人×吸血鬼×主角）**（2026-08-28，見 [FACTION.md](FACTION.md)）：作者要做「兩族原本和平 → 旗標開戰三方互打 → 玩家選邊，己方不打主角、主角武器也打不到己方」的劇本。討論定案：和平期用 NPC 擺（打不到、可對話）、開戰「換演員」（NPC 退場＋戰鬥版怪物入場）；未選邊時不寫「不打主角」特例，靠兩族擺得近、索敵挑最近讓他們先互咬；陣營選擇只活在這趟劇本；**兩族互打傷害 ×1/100（演戲，殺敵主力是玩家）**、對玩家正常。<br>**兌現上一波留的插座**：`FactionRelations` 從寫死改成「關係矩陣＋執行期狀態」（WarActive／PlayerAllied，關卡單次——掛在 `TriggerChain.ClearLevelFlags` 一起清）；新增 `DamageMultiplier`（兩族互打 0.01）與 `ApplyLayer`（**「玩家武器打不打得到」＝放哪個 Layer**：結盟/和平＝Ally 層子彈天生打不到、開戰未結盟＝Enemy 層——所有武器路徑零改動；互打走登記表＋Distance 不吃 Layer，結盟的狼人照樣咬得到吸血鬼）。`MonsterFaction` 加 Werewolf/Vampire、`MonsterData.csv` 尾欄加 `Faction`、`BrainType=War`（WarBrain：追最近敵對目標——敵對怪或玩家挑近的）；`MonsterController` 目標選擇一般化（enemyTarget 依 `HasMonsterFoes`、playerTarget 依 `AttacksPlayer`、faceTarget＝enemyTarget??playerTarget——Neutral NPC「平時不看玩家」順帶改由關係表保證，不再依賴 DetectionRange=0）。`CombatSystem` 怪×怪查乘數＋`CurrentHitTheatrical`（演戲傷害**不跳傷害數字、不印 log**——30 隻互毆會洗版；白光照閃有打鬥感）。觸發鏈加 `factionWar`／`joinFaction` 兩鏈動作（編輯器 TriggerType.Defaults＋triggerTypes.json 兩處同步）；NPC 加**消失旗標**（進圖已成立不生、中途成立即時退場——和平演員退場用）。<br>**通則**：「A 打不打得到 B」在這專案有兩個正交的機制——**數值上能不能傷（FactionRelations）與物理上打不打得到（Layer）**；把「切層」也收進 FactionRelations（ApplyLayer）之後，兩者永遠同步，不會出現「打得到但零傷害」或「傷得了但子彈穿過」的半套狀態。**後續（同日）**：作者素材未到位、先擱置驗證——FACTION.md 擴寫成**配置手冊＋驗證清單**（§4 替身方案：借 Family_*／Ghost_* 現有圖即可全機制測完；§5 一條龍含具體欄位值；§6-A 回歸清單＝「現有功能不受影響」的檢查，**現在就能跑、不需新素材**；§3 逐條分析為何改動對既有內容零影響——Faction 空欄=Enemy、演戲抑制只在雙方都是部族時觸發、ResetScenario 無狀態時 no-op）。⏳ 近期只需跑 §6-A 回歸；陣營本體之後照 §5→§6-B~E。

* [x] **建立美術紀律文件 ART_DIRECTION.md——對照《魔女庭園》拆解「完成品感」從哪來**（2026-08-28，見 [ART_DIRECTION.md](ART_DIRECTION.md)）：作者觀察 Steam《魔女庭園》(Garden of Witches，Team Tapas，正式版當日 92% 好評)，玩法設定與本作相近但畫面「有完成品的感覺」，而本作「設計圖都不錯、組起來不漂亮」。逐張截圖分析後歸納六個機制：一張圖一個色彩劇本（背景固有色被主色染過、強調色稀缺）、明度階層（地板最暗最灰→角色→特效最亮）、場景邊緣近黑剪影裝框、特效與主美術同一手繪語言、全屏質感層、接地暗斑＋常駐環境粒子。寫成 readme/ART_DIRECTION.md：六大紀律（每條含產素材端與引擎端掛點）＋附錄A「給繪圖 AI 的 8 條產圖檢核表」（作者委託 ChatGPT 產圖時直接附上）＋附錄B 截圖自檢表＋落地優先順序（1 Atmosphere 統一調色、2 vignette＋地面壓暗、3 邊框剪影包最優先——不新增角色/武器素材即可見效）。README 文件地圖與 AGENTS 路由表各加一列。<br>**通則**：**單件素材品質 ≠ 畫面品質**——「每件素材都好看但畫面散」的病灶在畫面經營層（明度分配、色彩統一、質感一致），解法是給「組合」立紀律，不是重畫素材；日後審美術問題先過 ART_DIRECTION 附錄B 的自檢表再談改素材。<br>**後續（同日）——分域準則資料夾**：作者拍板美術分工（場景/地上物/人物圖/立繪/UI icon＝ChatGPT 繪製並自擬準則；場景特效/氛圍 shader＝Claude），總綱之下開 `readme/art_direction/` 收各領域分域準則（索引含分工表）。先產出 Claude 負責域兩份：[art_direction/VFX_GUIDELINE.md](art_direction/VFX_GUIDELINE.md)（風格語言＝手繪色塊＋黑色負形、特效色服從場景強調色、發光疊層 E11~E13 整串等效亮度、排序帶查 MapDepthSort、定位走可見身體幾何 E14、最暗/最亮場景雙驗收、氛圍粒子走 SceneFxTable 加列）與 [art_direction/SHADER_GUIDELINE.md](art_direction/SHADER_GUIDELINE.md)（每個 mode 三句話＝主色染色/明度塑形/動態元素、J4 暗場景成立性必驗、呼吸不閃爍＋獨立相位、SetBypass 相容、全屏質感層規格＝宣紙紋等效 alpha 3~5% 起手）。GPT 各領域準則待作者徵詢後放入同資料夾。<br>**後續（同日）——GPT 六份準則收錄**：作者拿總綱給 GPT 擬出六份（場景、地上物、角色圖、立繪、UI icon 五領域＋AI 委託工作流程 SOP），審閱後全數收錄 `readme/art_direction/`（各檔頭加一行指回總綱）。審閱結論：與總綱零衝突，色彩劇本/明度階層/裝框等章節正確承接；「pixel art／pixel clusters」的畫風定義**已對照實際素材驗證屬實**（旱魃立繪與序列圖確為像素質感暗黑奇幻風）。亮點：母版制度＋LOCKED/CHANGE ONLY 委託格式（治 AI 繪圖跨張漂移）、「其他都不要變」的正式定義、驗收順序「先問是不是同一個角色、再問漂不漂亮」。原始檔留在專案根 `gpt美術製作準則/`（可刪，收錄版為正本）。

* [x] **美術紀律落地第一波：場景主色染色（AtmoTint）＋玩家常駐體光＋HUD 血球暗場景收斂**（2026-08-28，見 [ATMOSPHERE.md](ATMOSPHERE.md)〈場景主色染色〉〈玩家常駐體光〉〈HUD 暗場景收斂〉、[BOTTOM_HUD.md](BOTTOM_HUD.md)）：作者拿紅嫁衣書房開/關氛圍 shader 的對比截圖問「還要調什麼」，按 [ART_DIRECTION.md](ART_DIRECTION.md) 診斷出三件事並全部實作。<br>**① AtmoTint（紀律一）**：MapsTable 第 13 欄 `AtmoTint`（RRGGBB、空=不染），暗從「灰的暗」變「有主題的暗」。染法三守則：亮度不變（tint 歸一化到像素自身亮度，不會把畫面再壓暗＝E11）、按暗度加權（亮部/燈池/紅燈籠不被污染＝強調色保護）、放所有 mode 之後 bypass 之前（任何氛圍可疊、回憶照樣淡掉）。紅嫁衣 10 張圖填 `5A2430` 暗絳紅（**已明說**：只動 ID 1~10，其餘留空）。<br>**② 玩家體光（紀律二）**：`BuildLights` 在玩家無發光裝時補一盞恆定微光（半徑 1.2 格、亮度 0.35、不呼吸）——只照出自己輪廓、照不了路，柴房教學的點燈壓力不變；有佛燈時讓位不疊加。<br>**③ HUD 收斂（紀律七）**：新 static `AtmosphereController.DarknessLevel`（各氛圍→0~1，bypass 時同步歸零），`BottomHudPanel` 每幀餵給 `LiquidOrb.SetSceneDim`，最暗場景血球亮度 ×0.65 平滑過渡。<br>**一個差點上線的雷**：AtmoTint 讓「正常亮圖」也會啟用 shader，此時佛燈/體光會經 mode 1 的 `lightShift` 在大白天染出一圈暖色——`BuildLights` 加守門：非照明模式（不是 2/3/9、也沒有 EnvBright 壓暗）直接回 0 盞。**通則**：**放寬「shader 啟用條件」時，要重新盤點 shader 裡所有「以前只在特定模式下有輸入」的路徑**——照明資料以前「有餵＝有在用」，條件一放寬這個等式就破了。⏳ 未實機驗證，驗收清單：紅嫁衣任一房「暗部帶絳紅、燈籠橘紅不變粉」；卸下佛燈仍看得見自己輪廓但看不清一格外；柴房教學開頭仍然黑到必須撿燈；血球在紅嫁衣比廣場暗一階、回憶演出時亮回來；廣場（Atmosphere=5）與初始森林（=1 未填 AtmoTint）畫面零變化；編輯器照明預覽不含染色屬正常。<br>**後續（同日）——首輪實機回饋調參**：作者截圖對比，染色方向對（「暗紅的暗」成立）但整片糊成粉紗——中間調的地板全被算進「暗部」（權重上限 0.6 太寬）＋強度 0.5 太重，對比與燈池突出度被吃掉。收斂成：權重範圍 smoothstep 0.10~0.60 → **0.04~0.42**（中間調放過）、`SceneTintStrength` 0.5 → **0.32**。**通則**：**全屏調色的驗收標準不是「有沒有味道」而是「層次還在不在」**——主色染色只該接管「本來就沒資訊的暗部」，中間調是層次的載體、動它就是動對比。<br>**後續（同日）——二輪實機通過（書房）**：0.32＋窄權重版作者截圖確認：暗部透絳紅、地板層次保留、燈池突出、紅燈籠不變粉；有/無佛燈差異正確（佛燈＝照明一大圈、體光＝只剩身體輪廓的存在感、恐怖壓迫感不減）；血球收斂融入石雕框。書房這張圖染色/體光/HUD 三項定案。⏳ 其餘驗收待跑：柴房教學點燈壓力、廣場與初始森林零變化回歸、回憶演出時 HUD 亮回。<br>**後續（同日）——GPT 美術回饋微調兩項**：作者把截圖給 GPT 徵詢，回饋與 Cowork 診斷重合（書櫃黑洞兩邊都點名）。做了兩項：① 體光光色 DefaultWarm → 中性微暖白 `(1.00,0.96,0.90)`（體光是「角色可讀性」不是「一盞燈」，中性色才不會像自帶燈籠；只改色相不加亮度，守住佛燈價值）。② **傳送點冷光**：查證截圖的藍白光暈＝VfxTable id 6 標記（不是 SceneFx 綠 Portal），序列圖無亮度旋鈕 → 改為 `BuildTeleportMarkers` 給每個標記掛冷藍 `LightSource`（1.6/0.5/微搖/瀰漫）——暗場景傳送點自帶冷光池、亮場景零影響；掛標記物件上所以 togglePortal/換圖自動熄。全遊戲色溫規則就此定案：**傳送點＝冷色、火光＝暖色、體光＝中性**。書櫃補光（回饋③）作者自行在編輯器擺燈，首次試擺踩到「燈擺在走道被地上物碰撞卡住」——用「照明」分頁的獨立光源（無碰撞）而非帶燈地上物即可，待作者重試。⏳ 未實機驗證：暗場景傳送點周圍一圈冷藍、體光不再偏橘、togglePortal 關傳送點時冷光同滅。<br>**後續（同日）——書櫃補光定案＋新坑 E21**：作者擺補光燈，第一版半徑過大整個房間被抬亮、且發現「燈開再大書櫃還是黑」——這套照明是乘法還原不是加光，近黑素材永遠照不亮，記成 [PROBLEMS.md](PROBLEMS.md) **E21**（含兩條解法：光暈托剪影／素材端提亮）。定案＝光暈托剪影：獨立光源半徑 1.8、亮度 0.3、櫃體中央偏上——剪影與書冊可辨、黑重感保留。書房至此完成整輪畫面經營（壓暗→染色→體光→HUD 收斂→傳送點冷光→焦點托光），作為後續房間的參數範本。<br>**後續（同日）——紅嫁衣十房逐間巡檢完成＋鬼霧模式接上照明**：作者每間房截「無佛燈/有佛燈」雙狀態圖逐間過。結果：儲藏室/柴房/廚房/新娘房/樹妖房佈景**免修**（柴房「體光不破壞點燈壓力」回歸驗收通過；新娘房婚床帳內留黑是懸念、勿補光；廚房掛豬＝焦點範本）；起居室補「囍字暗紅微光」、起居室2 宴席桌補燭光、神明廳供桌燭光經比對**保留作者原版參數**（我的 1.5/0.4 起手值收太窄——**通則：給的照明參數是起手值，兩版並排時作者眼睛是最終驗收**）；庭院為樞紐房、多傳送點冷光＝路標（建議選配：月光照中央囍地紋，作者自決）。**樹妖房揪出真缺陷**：Atmosphere=14 鬼霧不讀光源清單，佛燈「有照光半徑卻全無效果」——修法＝mode 14 壓暗 0.70 改 `lerp(0.70,1.0,v)`＋lightShift（沒燈＝與舊版逐像素相同、零回歸），BuildLights 守門加 14，shader 檔頭/ATMOSPHERE.md 同步。⏳ 樹妖房實機驗：佛燈在鬼霧圖有光池、無燈畫面與改前全同。<br>**後續（同日）——庭院揪出照明系統第二個缺陷（E23）**：作者實測庭院發現「人走到哪、另一邊的燈整排熄掉」——同框 12 盞上限被樞紐房打爆（門樓燈籠＋側牆燈籠＋六傳送點冷光＋月光＋體光），且挑燈基準是玩家位置，全圖一屏時被踢的燈就在畫面內。修：上限 12→20（MaxLights／MAX_LIGHTS／編輯器 GameMaxLights 三處同步）、挑燈基準玩家→**相機中心**（全圖模式挑選恆定；跟隨模式相機≒玩家零行為差）。記 [PROBLEMS.md](PROBLEMS.md) **E23**（通則：截斷基準要選觀察者不是角色）。編號補記：原擬編 E22，發現另一 session 已用掉 E22（鏡頭震動）、且 E21 被點名誤植於 J 段（索引已註記維持原位）——E23 這次照規矩放進 E 段正確位置。⏳ 實機驗：庭院左右走動所有燈恆亮、跟隨模式房間行為不變、PerfHud 確認 20 盞上限幀數無感。

* [x] **修「血統變身後底部血球 HUD 永久消失」——演出開場 `CloseAll()` 把 HUD 一起關掉、沒人還原**（2026-09-01，見 [PROBLEMS.md](PROBLEMS.md) **D24**、[BOTTOM_HUD.md](BOTTOM_HUD.md)）：作者回報在場景中喝血統藥劑、變身完血條整條不見而且不再出現，並說「之前應該沒這問題」。<br>**根因**：`BloodlineTransformFxRunner.Run()` 開場的 `UIManager.CloseAll()`（本意是關掉背包那類會蓋住演出的視窗）**是遍歷全部面板、不分層**，HUD 層的 `BottomHudPanel` 被一起關掉；而全專案只有 `PlayerController.Start()`（只在玩家初次生成那一幀）與 `MapManager.PlaceAndSetup()`（只在換圖時）會把它開起來，喝藥變身兩者都不觸發 ⇒ HUD 永久消失。<br>**「之前沒問題」是假象**：這個坑從變身演出上線（2026-08-19）就在，只是**換一次圖就會被 `PlaceAndSetup` 補開回來**——喝完藥有走傳送點就看不出來，所以看起來時好時壞，而且「換張圖就好了」正好把真正的原因蓋掉。<br>**解法**：`BloodlineSystem` 加 `_hudWasOpen`——`PlayTransform()` 在叫 `BloodlineTransformFx.Play` **之前**先記（之後再問永遠是 false），`FinishPerformance()` 解完 hold 後由新的 `RestoreHud()` 開回來。**還原點刻意挑 `FinishPerformance` 而不是 Fx 的 `finally`**：它是「世界演出 ＋ 立繪揭示」兩段的唯一共同出口（正常結束／玩家中途死掉／保險絲逾時／`OnDestroy` 都會走到），放 Fx 那邊只蓋得住前半段，而且血球會在立繪揭示那一段從底下透出來。開回來加了跟 `PlayerController.Start()` 同一條守衛 `!IsIntroCutsceneMap(CurrentMapId)`，免得違反「開場山道劇情場景(13/14)不顯示血球」。`BloodlineTransformFx` 檔頭坑清單加第 4 條、`CloseAll()` 那行加交叉引用，指回這裡。<br>**通則**：**`CloseAll()` 這種「一次清乾淨」的 API 不分層，任何演出用它清場前都要先記下 HUD 狀態、收尾還原**；而還原點要挑「所有離開路徑的共同出口」，不要挑其中一段的 `finally`。另一條：**「換個場景就好了」是排查的紅旗不是線索**——它代表另有一個系統在定期重建這個狀態，把漏還原的地方蓋掉，症狀因此看起來偶發。同一家族：D13（鎖要具名）、D21（HUD 關了又被 `Start()` 開回來）。<br>⏳ 未實機驗證（Cowork 開不了 Unity）：進遊戲喝一瓶血統藥劑，變身＋立繪演完後血球條應立刻回到畫面底部；順帶驗開背包/鍛造熱鍵正常、輸入鎖有解。

* [x] **修「天雷打下來鏡頭偏掉、要等 tip 關掉才滑回來」——暫停中的震動沒有回復力**（2026-09-01，見 [PROBLEMS.md](PROBLEMS.md) **E22**、[MAP_SYSTEM.md](MAP_SYSTEM.md)）：作者附上正常/偏掉的兩張對照圖回報，變身天雷擊中那一瞬間整個畫面偏移，之後整段演出與立繪 tip 都維持偏掉的構圖，tip 關掉才自己滑回來。<br>**根因不在演出、在相機**：`MapCameraController.LateUpdate` 的震動是最後直接疊在 `transform.position` 上、**刻意不寫回 `_vel`／`desired`**，靠「下一幀 SmoothDamp 會把鏡頭拉回基準」保證不累積——原註解甚至寫明了「震完自然回到正確位置，不會累積漂移」。漏掉的前提是 **SmoothDamp 要真的在動**：它吃 `Time.deltaTime`，`timeScale = 0` 時完全不動；而震動本身是 `unscaledDeltaTime`（刻意的，暫停中播的演出要看得到震）。**有位移、沒有回復力** ⇒ 0.25 秒震動 ≈ 15 幀隨機漫步累積成一個固定偏移；血統變身整段（世界演出＋立繪揭示）都在 `timeScale = 0` 下，所以要等 tip 關掉時間恢復，SmoothDamp 才開始把鏡頭拉回來。<br>**解法**：新增 `_shakeApplied` 記下這一幀疊了多少，**下一幀開頭先扣掉**再算基準（`cur = position - _shakeApplied`）——「不累積」改由自己保證，與 `timeScale` 無關。`Apply()`（換圖時會直接寫 `transform.position`）順手 `StopShake()` ＋ 清 `_shakeApplied`，免得下一幀扣掉一個不存在的偏移。<br>**通則**：**「這個偏移之後會被 XX 拉回來」是隱性依賴，要問 XX 在暫停時還跑不跑。** 專案裡「演出＝unscaled、遊戲邏輯＝scaled」兩條時間軸並存（D15），任何「A 造成位移、B 負責回復」的配對，只要 A 是 unscaled 而 B 是 scaled，暫停時就單邊生效。**暫時性的偏移要自己記住、自己還原，不要借別人的收斂行為當還原機制。**<br>順帶記一筆：查表時發現 **E21 被誤植在 J 段開頭**（分類索引已加註），照 DOCS_GUIDE「永不重編號」維持原位不搬。<br>⏳ 未實機驗證：喝血統藥劑看天雷擊中時鏡頭「震一下就回正」、整段演出構圖不歪；順便驗一般戰鬥中的震動（非暫停）行為沒變、換圖進場鏡頭不歪。

* [x] **測試選單「直接進關卡 → 血狂之爭」改走 module 首圖，讓 `IsLevelStart` 能決定進哪張**（2026-09-02，見 [PROBLEMS.md](PROBLEMS.md) **B15**）：作者新增 `BloodFang_InitialScene2`（MapsTable #19）想跟原本的 #17 比較，把 #19 的 `IsLevelStart` 改 1、#17 改 0，測試選單卻始終進 #17。<br>**原因**：`DevQuickStart` 的血狂之爭當初是照競技場複製的 `map:<id>` 型（`BloodFangMapId = 17`），走 `MapManager.DevStartMapId → GoToMap(17)`，**完全繞過 `IsLevelStart`**；只有紅嫁衣／初始洞窟那種 `Set("<module>")` 型會經 `MapTable.FindLevelStart` 讀該欄。等於「正式流程已經換圖、測試選單還進舊圖」。<br>**改法**（作者拍板要跟紅嫁衣同一套）：`SetBloodFang()` 改成 `Set("BloodFang")`、刪掉 `BloodFangMapId` 常數、選單標題 `血狂之爭 (BloodFang_InitialScene)` → `血狂之爭 (BloodFang)`（名稱不再綁死某張圖），檔頭註解補上「首張＝該 module `IsLevelStart=1` 那列，改 CSV 就能換」。`map:<id>` 型保留給邪佛廣場／競技場 2 這種「非模組首圖」的單張測試地圖。<br>**通則**：同一個入口有兩種語意不同的目標型別（吃資料表 vs 寫死 id）時，差別藏在字串前綴裡、複製貼上時語意不會跟著複製；新關卡加測試入口一律用 module 型。<br>**同日：把規則寫進文件（作者要求「以後不用再找我改」）**：[TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md)〈測試快捷〉整節改寫——兩種目標型別對照表（module 型讀 `IsLevelStart`／map 型寫死 id）、**新關卡入口一律 module 型**、「換某關從哪張圖開始＝只改 MapsTable、不用動程式」、加新入口要複製哪三行、EditorPrefs 舊值要重點一次的警告、目前選單項一覽；AGENTS.md 路由表加一列、README 文件地圖那列補「加/改直接進關卡選單」；`DevQuickStart.cs` 檔頭加「加新入口的規則」段指回文件。<br>⏳ 要作者做：Unity 重編譯後**重新點一次選單**「血狂之爭 (BloodFang)」——EditorPrefs 裡存的舊值 `map:17` 不會自己更新（識別特徵：那一項沒打勾），重點後按 Play 應進 `BloodFang_InitialScene2`。

* [x] **修「踩進鏡頭區拉遠時畫面邊緣露出地圖外黑邊、1~1.5 秒才收」——夾制要夾實際位置，不能只夾目標**（2026-09-02，見 [PROBLEMS.md](PROBLEMS.md) **E24**、[MAP_SYSTEM.md](MAP_SYSTEM.md) §2.2）：作者在血狂之爭大門口放 camZone（`zoom=1.5`／`offsetY=2`）想拉遠看宅邸全貌，**從地圖右緣那側走進區域**時右邊整條露黑，附了兩張截圖。<br>**兩個獨立的洞疊在一起**：① `orthographicSize` 在 `LateUpdate` 第 2 步**當幀立刻**變大，相機位置卻要靠 `SmoothDamp` 追，而 `ClampToBounds` 只夾「玩家位置算出的目標」、**從不夾相機的實際位置**——視窗一放大，相機還停在貼邊的舊位置就已超界；而且 zoom 的指數過渡（`zoneTransitionTime=0.4` → 收斂約 0.78 秒）讓合法區間一直內縮，`followSmoothTime=0.12` 的 SmoothDamp 永遠落後半拍，兩個過渡互相追逐 ⇒ 破綻剛好 1~1.5 秒。② `offsetX/Y` 加在夾制**之外**（`desired = basePos + _offsetCur`），`offsetY=2` 等於合法地把鏡頭頂出上緣 2 格，這一項與過渡無關、不會自己恢復。<br>**驗算吻合**（24×27 圖、`followViewHeightTiles=10`）：正常 halfW≈7、貼右界時相機 x≈17；zoom 1.5 後 halfW≈10.5、上限降到 13.5 ⇒ 超界 3.5 單位＝截圖那條黑帶。「從中間進區看不出來」也解釋得通：離邊界遠，同樣的滯後不越界。<br>**解法**：兩道都補，只在跟隨模式且非 `_focus` 時做（對準點刻意不夾邊界，保留）——offset 併入後再夾一次；**`SmoothDamp` 之後、疊震動之前，對 `_cam.transform.position` 再夾一次**（關鍵的一道，保證任何一幀都合法）。<br>**代價已寫進文件**：貼邊時 zoom/offset 會被夾住打折，要看更遠得從資料端留空間（那側多留幾格景、或 camZone 往內挪）。<br>**通則**：**夾制要夾最終寫進 transform 的值**——任何在夾制之後才加的位移（offset、震動、後續修正）都等於繞過夾制；同一家族的還有 E22（震動疊在 SmoothDamp 之後、要自己記得扣掉）。另一條：**「視窗大小」與「視窗位置」由兩套不同速度的過渡各自驅動時，中間態必然出現不合法組合**，不能靠「位置追上去就好」，要每幀強制合法。⏳ 未實機驗證（Cowork 開不了 Unity）：從右緣走進大門 camZone 應全程不露黑、鏡頭平順拉遠；順帶驗貼邊時 offsetY 被夾住是否還看得到想看的景（看不到就調地圖或區域位置）、一般房間跟隨與鏡頭聚焦 trigger 行為不變。

* [x] **Atmosphere 新增室內系 16~19（室內暖光／莊嚴金輝／冷月室內／燭火幽影）＋ Bloom 前置管線**（2026-09-02，見 [ATMOSPHERE.md](ATMOSPHERE.md)〈室內系〉、[PROBLEMS.md](PROBLEMS.md) **E25**）：作者要四種適合室內場景的氛圍，拍板「效能優先、氛圍盡量就好」。<br>**四種共用 shader 裡同一段基底**（`_Mode > 15.5` 一個分支），彼此只差七個常數（`dim`／`desat`／`vigDepth`／`bloomK`／`breathK`／`lightW`／`stoneLift` ＋ 兩個 tint）——各寫一段等於把同一條式子放進指令預算四次，而這支 shader 已把 19 種氛圍攤平、指令數吃緊。**設計上的關鍵是 `lightW`**：它決定色彩往哪邊內插——0＝按像素亮度（暗部冷、亮部暖/金，**不需要場上有燈**，16/17 用）、1＝按照明係數 `v`（燈池暖、其餘冷＝冷暖對比，18/19 用，必須有燈）。有了它，「靠亮度分明暗」與「靠燈分冷暖」兩種完全不同的手法才塞得進同一條式子。<br>**Bloom 走前置 pass 而非單 pass 硬做**：bloom 的柔來自大半徑，而半徑大、tap 不夠就是重影（J5 通則），塞單 pass 要三十幾個 tap、逼近編譯上限。改成新 shader `AtmosphereBloom`（pass0 在 1/4 解析度做 4-tap 降採樣＋soft-knee 亮部抽取——4-tap 是為了**防閃爍**，小點光源單點降採樣一移動就跳；pass1 在 1/8 做 9-tap tent），主 shader 雙線性放大**加法**疊回（放大自帶柔化；用加法是因為乘法類效果在暗畫面會安靜失效＝J4）。合計約 1.4 次全螢幕取樣，**比單 pass 硬做還便宜**，且 `BloomEnabled` 只在 16/17 為 true，其餘 17 種氛圍完全不經過。<br>**取捨（已寫進文件）**：16/17 刻意不吃照明，「中央較亮」用暈影做＝零成本——代價是暈影的中央是**螢幕**中央，室內小房間走「整張地圖」相機模式時＝地圖中央，玩家在角落會覺得自己站在暗處；要改成「玩家周圍亮」是把 16/17 加進 `BuildLights` 白名單一行，但那些圖得實際擺光源點。17 的「石材略提亮」是用「低飽和＋中高亮度」猜石頭，灰衣服/鐵器會被一起提亮。<br>**過程中揪出的坑（E25）**：mode 分支是由大到小的**開區間** if-else，type 15 是 `_Mode > 14.5`，任何 >15 的編號都會掉進雜訊；而且這種開區間**有兩處**（frag 主分支段、取樣前的 UV 位移段），只改一處會得到「顏色對了但畫面還在撕裂」的半套症狀。主分支段把新分支加在前面、位移段改成閉區間並加註解。**通則**：開區間的 if-else 鏈對「往上加一個值」不安全——它把「目前最大的編號」偷偷寫成了「以後所有更大的編號」。<br>接線同步：`BuildLights` 白名單加 18/19、`DarknessLevel` 加 18(0.35)/19(0.5)（16/17 是亮場景走 default 0）、`AtmosphereBlit` 加可選雙 pass、MapsTable 表頭欄名說明加四種（**照規矩沒有動任何一列資料**）。順手修 SHADER_GUIDELINE §3.1 過期的 `MaxLights(12)` → 20。**後續（同日）——首輪實機：16/17 幾乎沒差別，兩個原因（[PROBLEMS.md](PROBLEMS.md) **E26**）**：作者並排截圖回報只差一點點。① 色彩內插直接用 `lum` 當權重，而畫面大半是中間調（灰石材 lum≈0.55~0.6）＝全畫面停在內插中點，`lerp(暖,更暖,0.55)` 與 `lerp(冷,金,0.55)` 都收斂到接近中性、差不到 3%——**兩端設得再開，中點都一樣**。② Bloom 門檻 0.62 是「亮點溢出」的直覺，但室內是亮場景、整片石材才 0.55~0.6，沒有像素過得了門檻 ⇒ bloom 恆為 0，「柔和 Bloom」等於沒做。**改**：亮度路徑改走 `smoothstep(0.5-split, 0.5+split, lum)` 並新增 `split` 常數（17=0.20 陡＝冷暖分離、16=0.35 平緩＝整張同調性）；bloom 門檻 0.45／knee 0.30；其他常數一併拉開（16 dim 0.94・暈影 0.50・bloomK 0.40・暖到 (1.08,0.98,0.86)；17 暈影 0.15・stoneLift 0.20・冷藍 (0.86,0.92,1.10)→金 (1.18,1.06,0.78)）。**通則**：用「畫面亮度」當後處理權重前，要先問這張畫面的亮度分布長什麼樣——線性權重會把所有中間調壓成同一個結果，而多數場景幾乎全是中間調。與 AtmoTint 當初把暗度權重收窄「放過中間調」是同一個道理的正反兩面。**後續（同日）——二輪還是差不多，量了截圖才挖到真根因：門檻用錯色彩空間**：作者回報「還是差不多」。拿兩張截圖做數值分析（避開 HUD、sRGB→linear→percentile）：畫面亮度中位數 0.32（截圖值）、p99 才 0.49；而專案跑 **Linear**，shader 拿到的是 linear 值——截圖上 0.32 的中間灰，`lum` 其實只有 **0.083**，實測分布 `p25=0.055 / p50=0.083 / p90=0.135 / p99=0.20`，**整張畫面都擠在 0.02~0.20**。於是我前兩版所有門檻（`smoothstep(0.5±split)`、bloom 0.62→0.45、石材 `smoothstep(0.15,0.55)`）**全部高於 p99**：`mixw` 恆為 0 ⇒ `litTint` 從頭沒參與、`split` 完全沒作用（量出來 R−B：16=+0.093、17=+0.036，純粹只是兩個 `baseTint` 的差），bloom 兩版都恆為 0。**修**：新增 `pivot` 常數＝0.085（中間調中心）、`split` 改 linear 尺度（16=0.060 涵蓋 p10~p95 平緩、17=0.030 只涵蓋 p35~p85 陡）、bloom 門檻 **0.09**／knee 0.06、石材門檻 `smoothstep(0.06,0.16,mx)`、bloomK 拉到 16=0.70／17=0.30。**通則（貴的一條）**：**shader 裡的每一個亮度門檻都是 Linear 空間的數字，不是看截圖估的那個亮度**——Linear 專案裡「看起來一半亮」實際只有 0.08 上下，憑直覺填 0.5 永遠不觸發，而且**症狀是「效果好像沒做」而不是報錯**，會讓人一直往「顏色不夠飽、強度不夠」的方向白調（這次白調一輪）。同一家族 E11。順帶印證 AtmoTint 的 `smoothstep(0.04,0.42)` 之所以是這組小數字，正因為它是實測調出來的 linear 尺度。**方法也留下來了**：E26 附了「量一張截圖的 linear percentile」的程式碼片段，以後定任何亮度門檻都照做，SHADER_GUIDELINE §2.2（E11 那條）也補了這半。**後續（同日）——三輪：門檻量級對了、bloom 生效了，但兩個 mode 又撞在一起**：量測顯示 bloom 確實開始作用（p90 0.136→0.185），但 16/17 的 R−B 差距從 +0.057 掉到 **−0.007**（17 的 R−B 從 +0.036 暴衝到 +0.100）。兩個原因：① **`pivot` 的語意想錯了**——我把它當「中間調中心」設在中位數 0.085，那代表一半畫面在分界之上、配陡曲線後直接翻到 `litTint`（金），整片石材變金 ⇒ 又跟「暖」分不開。正確語意是「**顏色翻面的分界**」：要「中間調維持底色、只有亮部才轉色」就得放在中間調**之上**（p85~p90＝0.14）。② **bloom 加回去的是場景原色**，石材偏暖褐 ⇒ bloom 越強畫面越暖，17 的 0.30 把自己的冷調沖掉。**修**：17 pivot 0.085→0.14、split 0.030→0.050、bloomK 0.30→0.15；bloom 疊加改乘 `litTint`（光暈帶該 mode 的高光色）。**通則**：**「權重曲線的中心點」要照效果的意圖擺，不是照畫面的統計中心擺**——分界放在中位數＝把一半畫面判給另一端；想要「只有少數地方變色」，分界就要放在那少數地方的門口。統計告訴你刻度在哪，意圖決定你把刀切在哪一格。另一條：**加法類效果（bloom/光暈）疊回去的是場景原色，會沖淡 mode 自己的調性**，要嘛染成該 mode 的高光色、要嘛壓低權重。**後續（同日）——四輪：17 過關；16 揪出 bloom 的移動斑紋（[PROBLEMS.md](PROBLEMS.md) **E27**）**：作者回報 17 沒問題，16 氛圍也對，但**一移動就有一片斑紋跟著爬、停下來後還會再動 0.5 秒**。根因是 prefilter 的抗鋸齒覆蓋不足——從全解析度直接輸出到 1/4 卻只用 4-tap／offset 半個 texel（只平均中心 2×2，而 1/4 降採樣需要覆蓋 4×4），高頻紋理（石磚縫、環形紋樣）欠採樣產生 moiré，降到 1/8 再放大 8 倍就成了大塊斑紋，相機一動格點相對場景移動、斑紋就爬。**那半秒不是 bloom 有延遲**（它零時間累積），是相機 `followSmoothTime=0.12` 的 SmoothDamp 餘滑——鏡頭還要滑 0.4~0.5 秒才停。只有 16 看得到也吻合：17 的 bloomK 0.15 把同樣的斑紋壓到看不見。**修**：prefilter 改 9-tap tent／offset 一整個 texel，`_Spread` 1.0→1.6；成本 0.39→0.7 次全螢幕取樣。**通則**：**降採樣的 tap 覆蓋要跟降採樣倍率相稱**（降 N 倍就要平均 N×N），是 J5「模糊 tap 數要跟半徑成比例」的孿生兄弟——J5 少 tap 得重影，這裡少 tap 得 moiré，而且 **moiré 只在畫面移動時才看得出來、靜態截圖完全正常**，所以會動的效果一定要動起來驗收。另一條診斷技巧：**「瑕疵在移動停止後還持續一小段」通常不是效果有延遲，而是畫面本身還在動**（相機平滑/慣性），先問「這半秒相機停了沒」比去找不存在的時間累積快。**後續（同日）——五輪：9-tap 仍有殘留，改逐級 2x 降採樣**：作者回報「好多了但還是會動」。一次從全解析度跳 1/4，就算 9 個 tap 覆蓋也只有 4×4，moiré 殘留照樣爬。改成 `src →(無材質 Blit) 1/2 →(4-tap box + threshold) 1/4 →(9-tap 模糊) 1/8`，合計覆蓋 8×8；第一級不帶材質——`Graphics.Blit` 縮一半時硬體 bilinear 本來就平均 2×2＝**免費的 box filter**，所以總取樣量 0.64 次全螢幕，**比單級塞 9 個 tap 的 0.7 還低**。同時把 threshold 從全解析度那級移到 1/4 那級（非線性運算作用在高頻資料上會把微小亮度差放大成「有/沒有」、自己製造閃爍）。**通則**：**抗鋸齒要用「多級小步」而不是「一步塞更多 tap」**——每級 2x 可白拿硬體 bilinear 的 box filter，一次跳 4x 得自己補還補不滿，多級效果更好且總成本更低。另一條：**非線性運算（threshold/pow/step）要放在資料已經平滑之後**。⏳ 待六輪實機；若仍有殘留，下一步依序是 `_Spread` 1.6→2.2、bloomK 0.70→0.55。<br>⏳ 待五輪實機。<br>⏳ 待四輪實機。<br>⏳ 待三輪實機。<br>⏳ 待作者二輪實機：16 應該明顯偏暖且光瀰漫、17 石材偏冷而浮雕/金紋轉金。<br>⏳ 未實機驗證（Cowork 開不了 Unity），驗收清單在下面那則回覆與 ATMOSPHERE.md。

* [x] **【POC】角色場景融合可行性測試——Original / A / B / C 四模式即時比較**（2026-09-02，見 `Scripts/Diagnostics/CharacterEnvPoc.cs` 檔頭）：作者拿血狂之爭 `GuessLobby`（Atmosphere=16 室內暖光）回報「角色透視比例都對，但看起來還是貼在背景上」，要求**不動角色原圖**、純用 runtime rendering 驗證能不能改善。遊戲中 **P → G** 循環四種模式（0 原狀／1 接地／2 接地+色彩／3 全部+邊緣），面板上顯示目前是哪一種。
  **診斷先於實作**：翻設定時發現這件事專案已經做了一半——`AtmoTint`（2026-08-28）就是「暗部往主色染、亮部不動」，而 mode 16 對整個畫面（含角色）已做過一次暖色統一＋去飽和 0.05。**所以原方案裡的 ambient/highlight tint 與 saturation 大半是重複做第二次，效果會遠小於預期**。真正沒被覆蓋的只有一件事：**全螢幕後處理對角色與場景一視同仁，永遠不會改變「角色暗部比場景暗多少」**——那個相對差就是貼圖感的來源，只能在角色自己的 sprite 上動。因此把重心從 tint 改成 **黑階抬升（`_BlackLift`）**。
  **另一條假設（順便驗證）**：mode 16 的 `bloomK = 0.70` 是四種室內氛圍最強、bloom 抽取門檻是 **0.09 linear**，而場景 linear 亮度 p50≈0.083——意思是石材與燭火有一半以上在發光瀰漫，**角色若整體偏暗就完全不參與 bloom，成為畫面上唯一的硬邊**。這很可能才是「剪下來貼上去」的大宗。`_LumBoost` 就是把角色亮部推過那道門檻；光暈長在角色**外面**，所以不違反「不可 blur 角色」的限制。
  **實作**：擴充既有的 `Custom/SpriteFlash`（角色 material 早就被 `HitReactionHandler` 換成它）加一組參數，**全部預設 0＝shader 整段跳過、逐位元等於加這功能之前**；參數走 MaterialPropertyBlock 逐角色餵，**與 `_FlashAmount` 寫在同一個 block**（`SetPropertyBlock` 是整包覆蓋的，分兩處寫會讓角色一挨打就把色彩沖掉一瞬間）。`BlobShadow` 加第二層接觸陰影（腳底小而稍深、外圈同步轉淡 0.30→0.20，共用同一張程序生成柔邊圓、零新素材）。
  **踩到／避開的雷**：① `baseTint`/`litTint` 是**乘法係數不是顏色**，宣告成 `Color` 會被 Linear 專案自動做一次 gamma→linear 轉換而扭曲（1.08 這種 >1 的值更直接失真）→ 改宣告 `Vector` + `SetVector`。② 邊緣偵測要用**貼圖原始 alpha** 而非 `c.a`——`c.a` 已乘過 `SpriteRenderer.color.a`，無敵閃爍期間是 0.4，相減恆為負、邊緣會在每次挨打時消失。③ POC 區塊用 `float` 中間變數算，不直接在 `fixed3` 上累加：`fixed` 精度 1/256 會把 0.008 這種黑階抬升量化成 2 個 step。④ 所有門檻沿用 mode 16 的 `pivot 0.085 / split 0.060`，**不是照 sRGB 直覺填**（E11/E26 的老雷：填 0.5 那種值等於效果完全不參與，症狀是「好像沒做」而不是報錯）。
  **第一輪實測（2026-09-02，作者提供兩張場景各四模式截圖，照 E26 量 linear percentile）**：
  | | 暗場景（Atmosphere=16 石材大廳） | 亮場景（米白石材＋金裝飾大廳） |
  |---|---|---|
  | 場景 linear p50 | 0.078 | **0.220**（亮 2.8 倍） |
  | 角色暗部比場景暗幾倍（原狀） | 15.3 倍 | **46.6 倍** |
  | 同上（開 Test B 後） | 6.7 倍 | 20.9 倍 |
  | 角色死黑像素佔比 | 6.5% → **0%** | 7.3% → **0%** |
  結論三條：① **黑階抬升確實是主因、方向正確**，死黑歸零、落差砍半以上。② **場景越亮，角色的死黑越突兀**（46.6 vs 15.3 倍），所以**抬升量不能是固定常數**——改成由 `SceneLuma`（該場景的 linear 中位數）÷ `TargetDarkRatio`（目標落差倍數）推算，換場景只要改一個有物理意義的數字，這也正是未來 Profile 該存的東西。③ **首版的中性灰加法方向是錯的**：實測角色暗部 R/B 從 1.81 掉到 1.51，而場景暗部是暖褐（實機量到最暗帶 R/B≈1.93），等於角色被拉得比場景更冷、色相上反而更不融入 → 抬升量改成乘一個歸一化的場景暗部色向量（`_LiftTint`）。
  另外兩項假設**被證偽**，記下來免得日後重做：**「角色沒參與 bloom」不成立**——白外套本來就有 15.9% 的像素過門檻；**Test C（邊緣融合）貢獻約等於零**——所有指標與 Test B 差在雜訊級（p99 差 0.0007），1px 在實際顯示尺寸下不可見。
  **Test A（腳下接觸陰影）已從 POC 移除**（2026-09-02）：真正的瓶頸是影子的**定位**而不是濃度，而定位改走資料驅動另案處理（見上一條）。模式因此從四個縮成三個：0 原狀 / 1 色彩 / 2 色彩+邊緣（**Shift+G** 可反向循環，方便來回比對相鄰兩個）。
  **場景氛圍定案：Atmosphere 17「莊嚴金輝」**（2026-09-02，作者實機比 16/17 後選定——16 的 `bloomK` 是 0.70，看起來「有種朦朧的感覺」；17 只有 0.15 所以清晰得多）。POC 參數隨之全部對齊 17 重量一次：
  | | 對 16 | 對 17 |
  |---|---|---|
  | `SceneLuma`（場景 linear 中位數） | 0.220 | **0.429** |
  | `BlackLift`（= SceneLuma ÷ 14） | 0.0157 | **0.0306** |
  | `EnvBase` 暗側乘法色 | 暖 1.08/0.98/0.86 | **冷藍** 0.86/0.92/1.10 |
  | `EnvLit` 亮側乘法色 | 暖 1.14/1.04/0.90 | **金** 1.18/1.06/0.78 |
  | `EnvPivot` / `EnvSplit` | 0.085 / 0.060 | **0.140 / 0.050** |
  | `LumBoost` | 0.22 | **0.10** |
  兩個值得記的點：① **同一張圖套不同氛圍，linear 中位數差到兩倍**（1 是 0.220、16 是 0.279、17 是 0.429——17 的 `dim=1.0` 不壓暗又有 `stoneLift=0.20`），所以任何「照場景亮度縮放」的參數換氛圍都要重量。② **暗部色幾乎不用改**（1.339/0.966/0.695 → 1.361/0.972/0.667）：17 的 `baseTint` 明明是冷藍的，但石材素材本身夠暖，最終畫面的暗部仍偏暖（實測 R/B=2.04）——**要量最終畫面，不能只照 shader 常數推**。
  ⏳ **本階段到此收尾，POC 尚未結案、後續會繼續**：對齊 17 的參數還沒看過實機（`BlackLift` 翻倍，**褲子會不會發灰是唯一風險**，太灰就把 `TargetDarkRatio` 從 14 往上調）；`MapsTable` 的 Atmosphere 欄由作者自己填。剩餘缺口見 [TODO.md](TODO.md)。若確認有效才進入下一階段模組化成正式的 Character Environment System；若否，整套可連同 `CharacterEnvPoc.cs` 一起刪除（另三支檔案裡標了【POC】的段落）。

* [x] **釐清「角色影子偏在腳的斜後方」——查完四種自動算法都有反例，結論是改走資料驅動；本輪改動已全部還原**（2026-09-02，見 [PROBLEMS.md](PROBLEMS.md) **E28**、[TODO.md](TODO.md)、[SHADOW.md](SHADOW.md)）：做角色融合 POC 時發現影子沒對準腳。過程分兩段——
  **前半是量測失誤**：拿「腳的重心」當角色位置基準，而走路時兩腿前後分開、那個重心每一幀都在身體中心兩側擺動數十像素，於是連續三個版本每次量出來都「更偏」（+57 → -87 → -76 → 0），全是量測噪音。把實際數值印出來（`rect`/`pivot`/`PPU`/`lossyScale`/`bounds`）才發現這條管線的 renderer bounds 是 **FullRect**，四種算法數學上等價、真正偏差只有 12px。
  **後半是真的有問題**：作者回報「idle 偏、走路正常」，換成「只取最底部一帶的水平中心」後主角 idle 仍偏、毛殭偏得更誇張——**長袍、披風、背包、爪子這些突出物本來就垂到腳邊，最底部的不透明像素根本不是腳**。至此四種自動算法全部有反例。
  **結論**：這件事違反專案鐵則（能用資料解決就不要靠程式猜），該由每個角色一組偏移值調一次到位。**`BlobShadow.cs` 已還原到未動過的狀態**，資料化缺口記在 TODO。
  **通則**：驗證位置正確性時基準不能選會隨動畫變動的部位；連續兩次修正都讓數字變糟就該懷疑量測方法；印數值永遠比猜便宜；**「大部分角色看起來對、少數明顯錯」這種症狀，就是該把它資料化的訊號**。

* [x] **角色融合場景：找到色彩處理救不了的根因——背景是全畫面唯一被放大顯示的東西；過渡期上 mipMapBias、砍 Test C**（2026-09-03，見 [PROBLEMS.md](PROBLEMS.md) **E29**、[PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) §4.1、`Scripts/CharacterMipBias.cs`）：承接 09-02 的 POC。先用素材原檔量數據確認黑階抬升方向沒錯（角色 linear 中位亮度 0.007~0.019、純黑 20~40%，場景底圖中位 0.13）；再把「螢幕上每像素幾個貼圖像素」逐類算一遍——**所有底圖都是 1448×1086 拉伸貼齊地圖**，標準房 80 px/格、血狂之爭 45~63 px/格，1080p 一格 108 px ⇒ 背景放大 1.35~2.4 倍變軟；角色 124 px/格、地上物 250~500 px/格都是縮小顯示、銳利。「銳利的角色貼在軟掉的背景上」就是貼紙感，血狂之爭最嚴重正因它 px/格最低。PERF_QUALITY_AUDIT 當年以 256px/格地磚稽核，改整張背景後沒人重算。<br>**作者拍板兩段走**：① 過渡期 `CharacterMipBias`——`mipMapBias = log2(角色px/格 ÷ 背景px/格)`（角色＝PPU÷lossyScale、背景＝貼圖寬÷地圖世界寬，比值與相機/螢幕/整圖或跟隨模式全無關、每張地圖一個常數，大廳約 +1.0），掛點 `MapLoader.BuildBackground/Teardown`、兩個 Animator 的 Setup、`PlayModeStaticReset`；PerfHud **P → M** 即時開關並顯示主角 bias。② 背景解析度規則寫進文件（每格 ≥128px；各地圖現況與建議尺寸表；`Main_Square` 90×50 要另外決定），作者換完圖把 `DefaultEnabled` 改 false 再對一次。順手砍 POC 的 Test C（shader 邊緣區塊與參數、`Mode.Full`、Shift+G），模式剩 0 原狀／1 色彩。<br>**通則**：同一畫面所有素材的取樣密度要一致——有一類被放大、其他被縮小，那一類就自成一層；診斷只要每類算一次「貼圖像素÷佔幾格」跟螢幕 px/格比。另一條：**管線換了，依舊管線做的稽核要重跑**。<br>**後續（同日）——首輪四張 A/B 截圖**：作者已自行把 GuessLobby 底圖換成 3200×2400（139 px/格）、InitialScene 2880×3840（120 px/格），所以那張圖上 M 的 bias 算出來是 0（角色 123 px/格 < 背景，永遠不銳化）＝**M 在這張圖已無事可做、解析度不一致這條在這張圖已由換圖解決**（MainLobby 45、Gallery 65、紅嫁衣 80 仍待換）。G 開/關四張圖量不出差別（角色暗部中位 0.022 兩邊一樣；截圖經縮放，精度有限），肉眼也看不出——先加診斷再調數值：PerfHud 的 G 鈕後面顯示主角 MPB 裡實際的 `_EnvOn`／`_BlackLift`（沒跟著變＝參數根本沒送到 shader），並加第三態「色彩×2」（黑階抬升、環境色、亮部抬升全乘 2）確認方向。**後續（同日）——G 有送到、也有作用，只是眼睛看不出**：作者附 Retina 截圖，鈕上 `_EnvOn=1 lift=0.031`；在原尺寸截圖上量角色暗部：G0 中位 0.0196、316 px 近全黑；G1 中位 0.0416、0 px 低於 0.03——**數值上抬了兩倍、死黑歸零，但作者與我在正常觀看尺寸都看不出來**。另外量到抬升後暗部 R/B 從 2.23 掉到 1.55：場景最暗帶實測 R/B 2.8（比前一版量的 2.04 暖得多，換底圖後要重量），`LiftTint` 改 1.49/0.99/0.52。**通則**：量得出來 ≠ 看得出來——角色只佔畫面 2~3%，暗部再抬兩倍也只是幾十個像素從 #0a 變 #1a；這條的性價比要用眼睛不是用數字定。**後續（同日）——作者拍板：「1 色彩」剛好、「2 色彩×2」太過**（×2 時褲子與外套暗部明顯發灰發暖）。所以黑階抬升這條**留下、力道定 ×1**，並改成**換圖時只在 Atmosphere 17 自動開**（`AtmosphereController.ApplyMapAtmosphere → CharacterEnvPoc.OnAtmosphereChanged`；其他氛圍關——這組常數是量 17 的大廳來的，暗房套同樣抬升會把角色抬得比場景亮）。G 鍵仍可隨時手動切著比。**再後續（同日）——轉正＋自動算**：作者問「所以每種 Atmosphere 都要跑一次校？」——不用。會隨場景變的只有場景中位亮度與暗部／亮部顏色，兩者都能**進圖後量最終畫面**：`AtmosphereBlit` 在後處理之後把畫面逐級縮到 32×18、`AsyncGPUReadback` 讀回（不卡幀），算中位、最暗 15% 均色、最亮 15% 均色，餵給改名後的 `Scripts/Atmosphere/CharacterEnvFusion.cs`（黑階抬升＝中位÷14、上限 0.06；暗側/亮側環境色直接用量到的歸一色）。換圖後第 20 幀與第 110 幀各量一次。用紅嫁衣書房驗算：地板中位 0.016 → 抬升 0.001＝等於關（作者比較兩張後也選「關」那張）；大廳 0.36 → 0.026≈拍板的量。**這是 E26「量最終畫面」原則的自動化版本**：人工校準從「每種氛圍一組」降到「抽三張看一眼」。P → G 三態、Shift+G 重量、按鈕顯示場景數據；POC 標記全部拿掉。**通則**：當參數只依賴「畫面統計」時，讓程式在執行期量畫面，比人工為每種情境填表更不會過期。⏳ 待作者抽驗大廳／紅嫁衣書房／廣場。文件：[ATMOSPHERE.md](ATMOSPHERE.md)〈角色環境融合〉。

* [x] **影子錨點表：每角色每動作一組、工具自動算＋手改覆寫——解掉「idle 偏、走路準」**（2026-09-03，見 [SHADOW.md](SHADOW.md)〈定位：影子錨點表〉、[PROBLEMS.md](PROBLEMS.md) **E28** 後續）：作者要求先研究「程式能不能一勞永逸」，不行再做手動。把全部序列圖每一幀的「最底一帶水平中心」相對畫布中心量出來，機制立刻清楚——**AutoSprite 各動作的腳在畫布裡不在同一個位置**：主角 idle 25 幀全在中心左 25px、walk 兩腳跨在中心兩側（中位 +10、擺幅 27）；Y 也一樣（狼人 idle 腳底 27px、walk 46px）。`BlobShadow` 用 `transform`（畫布中心）當 X、idle 第 0 幀量一次當 Y，所以 idle 必偏、walk 剛好像對。純程式全中已證明有反例（E28），所以做成**混合**：`ShadowAnchorMath`（最底 6% 帶的水平中心取全幀中位數；一個動作一組固定值、不逐幀——逐幀會跟著跨步滑）→ `Assets/Data/ShadowAnchorTable.csv`（Key＝`Characters|Monsters/<角色>/<動作>`，`Source=manual` 永不覆寫）→ Project Tools「計算影子錨點」（只算新的／重算所有 auto／只出圖三項；遞迴掃 GameAssets 原檔、每個角色一張拼圖到 `TempImage/ShadowAnchors/`，灰橢圓＋紅十字，不用進遊戲就能看全部角色）。遊戲端 `PlayerAnimator`／`MonsterAnimator` 實作 `IShadowAnchorSource`，`BlobShadow` 每幀用**當前 sprite** 的 PPU／pivot／lossyScale／flipX 換算（bodyScale 的腳底 pivot 自動跟上），換動作位移平滑 0.08 秒、轉身直接跳；表裡沒有的角色 runtime 用**同一條演算法**當場算（B9 的教訓：烘焙與退路不能各算各的），所以第一次跑工具前遊戲就已經比原本準。<br>**後續（同日）——一版演算法實機被打回**：作者跑完工具、掛好 Provider 進遊戲，主角 idle 影子仍偏，截圖看影子壓在**近腳**上。原因：一版取「最底 6% 帶的水平平均」，而 3/4 俯視的站姿兩腳一近一遠、遠腳比近腳高 20 多 px、根本不在帶內，量到的是單腳；我的 Python 預覽把「壓在近腳上」看成對了。二版改成**找腳**：最底 15% 帶內把有像素的欄連成段，兩段以上取最左最右段的中點與兩段底的平均（＝兩腳之間的接觸點），一段就退回可見框中心；寬＝max(兩腳跨距, 框寬×0.75)——後來作者回報殭屍系列、狼人這種瘦長角色影子縮成一小顆，改回框寬（＝舊版的寬）；再回報殭屍、毛殭 idle「偏了」，量截圖發現 X 其實在兩腳中間、是 **Y 壓在近腳鞋底**（衣襬把兩腿連成一段、量不到遠腳，Y 就落在最低列），半顆橢圓吊在角色下面看起來偏低偏外——一段時 Y 改成往上抬可見高×6%（`SingleRunLiftFraction`），與兩段時「兩腳底平均」同高。**通則**：截圖先量再改——這次「偏移」量出來是高度不是水平。重量結果主角 idle X 從 −24 變 +1、Y 從 30 變 41（在兩腳之間、比近腳底高一點），其餘血統 idle 全部落在 ±4px 內。**再後續（同日）——覓血者影子往上飄**：作者換覓血者血統，影子高到披風中段。二版「取最左、最右段」在覓血者 idle 抓到的是腳兩側垂下來的**兩條破布**（底比腳高 15~25px），改成「取**最低的兩段**當腳」——布條再怎麼垂也不會比腳低。重量：覓血者 idle Y 52.5→43.5（近腳 38、遠腳 49 的中點），主角不變。**再後續（同日）——dead 偏很大**：躺姿沒有腳，找腳演算法抓到的是頭髮或衣角，影子縮成一小顆貼在頭邊。`dead` 動作改另一套：只取序列最後 1/3 幀（前段是倒下過程）、X 用剪影中心、寬＝剪影寬；Y 先用正中心，作者回報「太往上」——上半被身體遮住、只剩上緣露出來像飄在身後，改成剪影底緣往上 min(高×25%, 寬×15%)（`LyingCenterFraction`／`LyingCenterMaxOfWidth`——該隱跪坐的死姿又高又窄，只用高度會把影子推到半身），影子從身體下緣露出來才像壓在地上（倒下過程用 `AnchorSmoothTime` 平滑帶過）。**通則**：**「最低的像素」不等於「接觸地面的點」**——俯視角的兩腳本來就一高一低，影子要放在兩腳之間；驗證位置正確性時要看「身體壓不壓在影子上」而不是「影子有沒有碰到腳」。<br>**通則**：「程式猜不準」不等於「程式沒用」——讓程式算出八成正確的預設值、把人工量降到只修反例，比純手填或純自動都好；關鍵是**表要能區分 auto 與 manual**，重算才不會把手改沖掉。另一條：資料驅動的定位一律存**像素、畫布座標、未翻面方向**，換算成世界座標交給知道 PPU／pivot／縮放／翻面的那一端做，表才不會跟顯示縮放綁死。<br>⏳ 要作者做：Project Tools → 角色 → 計算影子錨點 → 看拼圖 → GameManagers 掛 `ShadowAnchorTableProvider` 拖入 CSV → 進遊戲驗（清單在 TODO）。用 Python 先照同一條演算法出了六個血統的預覽（`TempImage/claude_fusion/anchor_preview_*.png`），主角 idle/walk、毛殭、狼人都落在腳下；芬里爾奔跑姿與旱魃長袍是預期要手改的那種。

* [x] **殭屍系列三個血統的影子改 manual（殭屍太小、毛殭／旱魃偏掉）**（2026-09-03，見 [SHADOW.md](SHADOW.md)〈手動調整影子〉）：作者實機截圖回報殭屍影子像一顆點、毛殭與旱魃明顯歪。**只改 `Assets/Data/ShadowAnchorTable.csv` 的 9 列（三個血統 × idle/walk/attack），演算法一行沒動**，`Source` 全部改 `manual`、`Note` 寫明原因；`dead` 三列維持 auto（拼圖看起來正確）。
  **每個角色歪的機制不同，各自對應演算法的一種反例**：
  - **殭屍（Jiangshi）不是歪、是小**——這個血統站姿窄，可見框寬只有 64px（主角 Base 99、visH 相近），`Width=max(兩腳跨距, 框寬)` 拿到的就是 64；`BodyScale` 又是 1.0（毛殭 1.5、旱魃 1.2），三階擺一起時它的影子最小。idle/walk 寬 64/72 → **86**（≈可見身高一半，對齊 Base 的 99/193 比例），attack 130 → 92（原本把前伸的雙臂算進框寬，換動作時影子會爆大一圈）。
  - **毛殭（Maojiang）**：白毛爪垂到腳邊，最底 15% 帶內只連得出**一段**（近腳），走「一段」分支 → X 用可見框中心（框右緣是外張的毛爪，中心被往右拉）、Y 只取近腳鞋底。改成兩腳中點／兩腳底平均：idle `X -5→0`、`Y 38.9→44`、`W 110→99`。
  - **旱魃（Hanba）**：長袍下擺的**破布條**在帶內連成一段，而且它的底幾乎跟遠腳一樣低（實測 200 vs 200），「取最低的兩段」剛好挑到布條而不是遠腳 → 影子壓在近腳上偏左。idle `X -15→-2`、walk `X -10.2→-1`、attack `X -29.2→-8`。
  **驗法**（不用開 Unity）：用 Python 照 CSV 的語意（X 相對畫布中線、Y 從畫布底往上、寬×`WidthFactor` 1.1、高＝寬一半）把橢圓畫回原始序列圖，每個角色一張 4 動作 × 4 幀的拼圖，改完重畫再看一次；圖在 `TempImage/ShadowAnchors/_claude/`（gitignored）。量的時候以 **Base 為校準基準**——它的值作者已認可，反推出「Y＝兩腳鞋底平均、W≈max(可見身高×0.5, 兩腳跨距×1.15)」，三個血統照同一把尺填，動作之間才不會跳大小。
  **通則**：`E28` 的「別再往自動偵測腳投工」在這次又被驗證兩次——**布條與遠腳一樣低**、**毛爪把可見框中心帶偏**，都是像素層面無解的反例；表存在就是為了這個。另一條：**手改前先找一列「作者已經認可是對的」當比例尺**，比憑感覺調每個角色更省來回。

* [x] **Split Sprite Sheet 改成「固定切 5×5 ＝ 25 格」，放大過的合圖才切得對**（2026-09-03，`Assets/Editor/SpriteSheetSplitter.cs`）：作者把噴水池的合圖用 Upscayl 放大 4 倍（1280×1280 → **5120×5120**）後再切，切出來是錯的。原因：工具寫死 `CellSize = 256`、格數用 `圖寬÷256` **推算**，所以 5120 被算成 **20×20 ＝ 400 格**，每格 256px——把每一幀又切成 16 塊。改成**格數固定 `GridCols/GridRows = 5`、每格大小由圖自己算（圖寬÷5）**：1280 的原生圖每格 256、5120 的放大圖每格 1024，兩者都是 25 張。
  **順手要處理的是守衛**：舊的冪等守衛是「剛好 256×256 ＝ 已是單張幀就跳過」，改成 5×5 後這條不再成立（放大後的幀是 1024×1024，不是 256），「整包就地切割」跑第二次會把上次切出來的幀再切一次、整包被靜默重排——這正是 2026-08-21 那次批次化留下的教訓。改成**用檔名辨認自己的輸出**（結尾 `_` 加兩位以上數字，例 `walk_01`）安靜跳過；第二道保險是尺寸：256／512／1024 **都不是 5 的倍數**，真的漏網也會被「寬高要能被 5 整除」擋下並列入報告，不會切壞。
  **通則**：**「用固定的格子大小去推格數」與「用固定的格數去推格子大小」，在素材可能被放大時是完全不同的兩件事**——來源固定是 5×5 的話，格數才是不變量，格子大小是變數；寫死變數那一邊，素材一換解析度就靜默切錯。另一條（重申 2026-08-21）：**會刪原始檔的批次工具，改了切法就要回頭檢查「重跑第二次會發生什麼」**，守衛要跟著切法一起換。
  ⚠ **Editor 程式我這邊編譯不了**（Cowork 沒有 UnityEditor），要作者開 Unity 才知道 API 有沒有寫錯。文件：[CHARACTER_SETUP.md](CHARACTER_SETUP.md) 的切割段已同步。

* [x] **地上物破壞改成「把自己那張圖炸成碎片」，共用煙塵特效預設關閉**（2026-09-03，`Assets/Scripts/Map/ShatterBurst.cs`、見 [DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md)〈破壞演出＝程序化碎片〉）：作者原本要找一顆共用的破壞特效，想想覺得「石雕、木桶、布幔破起來一模一樣很奇怪」，問能不能用 shader 讓噴泉當場四分五裂。
  **判斷：這件事不需要 shader。** 頂點位移式的碎裂 shader 仍然要先有「切好的碎片網格」——切割那一步跑不掉，只是把移動搬到 GPU；以本專案一次頂多爆幾個物件的量級，CPU 這邊量不出成本。溶解 shader 是「消失」不是「碎裂」，適合當疊加層不適合當主體。所以走**程序化碎片**：破壞當下把物件**自己那張圖**切成 3×4＝12 塊，每塊 `Sprite.Create` 指向**同一張貼圖的不同區域**（不複製貼圖），生成短命 SpriteRenderer 順著擊退方向飛開、旋轉、縮小、淡出 0.6 秒。**材質差異因此是免費附帶的**——碎塊本來就是那張圖，石頭爆石頭色、噴泉爆石雕帶水花；動畫物件取當前那一幀，水花會停在被打爆的瞬間。零素材、零 prefab，與 BlobShadow／怪物量產 route B 同一個路數。
  **踩到的第一個雷（讀介面比讀變數名可靠）**：`DestructibleObject.TakeDamage(float, Vector2 hitPoint)` 的第二個參數**根本不是命中座標**——`IDamageable`／`DamageInfo.HitDirection` 一路傳的都是**擊退方向**，`CombatSystem` 餵的是 `info.HitDirection`。舊註解寫「hitPoint 目前用於未來擴充(例如朝命中方向噴碎片)」，而它從來沒被用過，所以沒人發現名字是錯的。第一版照名字當座標寫，碎片方向會整組歪掉。已正名為 `hitDirection` 並在兩個檔案註明。
  **其他刻意的決定**：碎片掛在被破壞物件的**父節點**下（來源同一幀就 `Destroy`，同 BlobShadow 的理由），換圖拆地圖時一併清掉；碎片**不掛任何 Collider**（掛了會擋路、還會被武器目標搜尋當命中對象，同 B4 那類坑）；沿用來源的材質與 `sortingOrder`（不然會鑽到地板或角色底下）；`localScale` 用 lossyScale 換算回根節點底下（噴泉是 scale 7.4，不換算碎片會變一堆小屑）；切格用**整數像素**（`Sprite.Create` 的 rect 給小數會在格邊取樣到隔壁格，而 256/3 一定有小數）；`Sprite.Create` 產生的 Sprite **不會**跟 SpriteRenderer 一起回收，`OnDestroy` 要手動 Destroy，否則一路累積到換場景。
  **v1 刻意不做**：不用 Voronoi 尖角（0.6 秒內又在縮小，肉眼讀不出矩形；要更碎再換 `Sprite.OverrideGeometry` 餵多邊形頂點，仍是 SpriteRenderer、仍合批）；不跳過全透明格子（要 `GetPixels32()` 掃整張圖，1024² 配置 4MB 會造成 GC 突波，而空格只是畫不出東西——真要省，catalog 的 `FootprintMask` 本來就知道哪些格是空的）。
  **舊的共用特效沒有刪**：`DestructibleObject.PlayLegacyDestroyVfx` 預設 false，VfxTable ID 5 那一列原封不動；打開就回舊行為，兩個都開＝碎片＋煙塵兩層。
  **通則**：**「每種材質看起來不一樣」不一定要每種材質畫一份素材——讓演出從素材本身長出來就行**。碎片取自物件自己的貼圖，這件事一次解決全部物件，之後加新家具也不用補圖。另一條：**參數名寫錯又從來沒被用過的程式碼，是最會騙人的那一種**——照名字寫會靜默錯，要回頭讀它真正的來源（這裡是 `IDamageable` 的介面註解與 `CombatSystem` 的呼叫點）。
  **✅ 2026-09-03 實機已驗過，作者回報手感 OK，v1 常數不動**（碎片數 3×4、0.6 秒、初速＝可見高度 ×1.0、Drag 0.12、SinkAccel 2.2、SpinMax 320、EndScale 0.72、RadialBlend 0.55）。之後要分材質手感或換 Voronoi 尖角碎塊時，以這組數字當基準。

* [x] **Boss 開戰前奏定案：黑霧籠罩（兩張灰階密度圖 ＋ shader）＋「強敵現身」文字煙霧凝聚／散去 ＋ 頭目資訊進退場**（2026-09-04，新增 `Assets/Resources/Shaders/BossAura.shader`＋`SmokeDissolve.shader`，改 `UI/Panels/BossIntroPanel.cs`、`Map/TriggerChain.cs`；素材 `Resources/UI/Texts/{tw,en}/BossInfo_Warning.png`、`Resources/UI/BossIntroPanel/BossIntroPanel_Smoke1|2.png`；見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) 的 `bossIntro` 段）
  **成品節奏**：黑邊滑入＋紅霧湧入佈滿全螢幕翻騰 → 霧聚攏到畫面中央 → 「強敵現身」從霧裡凝聚成形 → 撐一拍 → **文字與霧被同一陣風吹散** → 頭像/名牌滑入、壓黑底版與血色暈影跟著淡進來 → 名字浮現 → 收。
  **同一天走了四版**，前三版的失敗是這條記錄的重點：
  ① VfxTable 14 的霓虹 WARNING 序列圖 → 調性不搭，移除。
  ② 「電視雜訊／訊號干擾」相機後處理 → 兩次都被退回（第一版照 `Atmosphere` type 15 加重、還做了 CRT 關機收合；第二版壓輕了但方向本來就不對），檔案已刪。
  ③ 紅光背景用**純程序 fbm** → 作者評「**很像廉價的畫面，後面紅色背景一點質感都沒有**」，退回。
  ④ 作者出兩張灰階煙霧密度圖當原料 → 現在這版。
  **⚠ 第 ③ 版的診斷是這整件事最值得記的一條：fbm 生得出「雲斑」，生不出煙的「絲」與「捲」。** 煙霧的捲曲邊、拉伸絮、濃淡層次是**形狀**，不是噪聲；用 noise 去模仿一張美術圖，再怎麼加八度、加 domain warping 也只是「更複雜的雲」。對照 [ART_DIRECTION.md](ART_DIRECTION.md) 紀律四「質感要像**畫出來的**，不是渲染出來的」——徑向漸層＋fbm 正是那條明文禁止的東西。同時還違反紀律二（背景中央亮度接近文字亮度＝畫面上有兩個焦點層）與紀律一（整片都是強調色＝沒有強調色）。
  **正解是分工：貼圖提供形狀、shader 提供行為**（VFX 的標準作法，也對應紀律四「買來的特效包是原料，改色統一之後才是素材」）。作者用 Scenario 出兩張 1254² 灰階煙霧圖，shader 負責翻騰、流動、聚攏、上色、吹散。**以後遇到「這個效果做不出質感」，先判斷缺的是形狀還是行為——缺形狀就要素材，別硬用 noise 補。**
  **兩張素材的驗收與匯入設定**（作者出圖、我量測後改設定）：
  - Smoke1（厚重霧體）結構好但偏亮：中位亮度 80/255、純黑只佔 2.4%，比較像「一整片濃霧」。**不必重生**——shader 端用 `smoothstep(_DensityLo,_DensityHi)` 做密度 remap 就能壓回有濃有淡，這個 remap 順便當「霧變濃／變稀」的旋鈕。
  - Smoke2（細絮煙流）幾乎是教科書級密度圖：54.5% 純黑、最亮 255、高頻能量是 Smoke1 的兩倍。
  - 兩張都是真灰階、分布均勻無構圖主體（**有主體的圖疊多層會露餡**，挑圖時要看這個）。接縫差約 8~11/255 不是無縫，用三層不同旋轉角度＋不同縮放疊加就散掉了。
  - **⚠ 匯入設定三項是必要條件，改錯直接壞掉**：`Wrap Mode`=**Repeat**（要平鋪捲動，Clamp 會把邊緣像素拖成長條）、`Generate Mip Maps`=**開**（多層縮放取樣，關著縮小時細絮會閃爍）、`sRGB (Color Texture)`=**關**（它們是密度資料不是顏色；Linear 專案下當 sRGB 取樣，Smoke1 的中位 80 會被壓成約 20，霧會變得非常稀薄）。
  **shader 實作重點**：
  - 兩支都是 **uGUI Image 用**，照 `UI/BloodlineDissolve` 的樣板（Stencil、`UNITY_UI_CLIP_RECT`），掛 material 照 `BloodlineIntroPanel`（OnBuild 建 `HideAndDontSave` 實例、OnDestroy 銷毀、載不到就退化成沒有前奏但表演照跑）。
  - **domain warp 是「翻騰」的來源**：拿細絮圖當扭曲場去擾動厚重層的取樣座標。少了它，霧只會整片平移，看起來像一張圖在滑動而不是在翻滾。
  - **細絮兩層用 `max` 不用相加**：相加會把兩層的絲糊成一片灰，max 保留最亮的那幾絲。
  - **「聚攏到中央」＝ 取樣座標往外拉（內容往中心縮）＋ 徑向遮罩收窄 ＋ 中央加濃**，三件事同一個 `_Gather` 參數驅動。
  - **霧是半透明壓在遊戲畫面上，不是蓋掉它**（作者要「留一點場景輪廓透出來」）：`_MaxOpacity` < 1，霧稀薄處還有一個隨半徑上升的 alpha 底線 `_SceneDarken`＝一層暗紅紗，把場景壓暗、染上主色但仍讀得出形狀（紀律一「染成主色」＋紀律二「背景最暗、文字最亮」）。
  - `SmokeDissolve` **一支做正反兩個方向**（`_Progress` 1→0 凝聚、0→1 消散），同 `BloodlineDissolve` 的設計哲學。煙感的關鍵是**一塊塊散開而不是整體變淡**：湍流位移往上飄（`p²` 曲線，前段撐著後段才走）＋三層拖尾取樣＋噪點 alpha 閾值＋上方先散。
  - **⚠ `_Pad` 是必要的**：uv 位移只能在圖自己的範圍內取樣，煙一飄出圖框就被切平。文字 Image 顯示尺寸放大 `SmokePad`(1.6) 倍、shader 內把 uv 內縮同樣倍率，外圈就是可以飄出去的空白。**兩邊倍率必須一致**。內縮後越界的取樣要自己乘 inside 遮罩——sprite 的 wrapMode 是 Clamp，越界會拖成長條。
  - **霧與文字吃同一個 `_T` 與同一組風的參數**（`_Rise`/`_Turb`/`_EdgeSoft`/`_UpBias`）才會是同一陣風；霧的淡出刻意排在吹散**後半段**，太早淡會變成「霧先不見、字才散」。
  - 文字圖走 `LocalizedArt`：程式只寫邏輯路徑 `UI/Texts/BossInfo_Warning`，`UIBuilder.LoadSprite` 自動改寫成當前語言資料夾，中英切換零額外程式。
  **舊東西沒有刪**：VfxTable 14 那一列與序列圖素材原封不動；trigger 參數 `warnVfxId` 保留但不再被讀取。面板整段表演藏 HUD 層、關閉時復原。
  **⚠ 第一次實機後的修正（同日，作者回報「紅霧太少，看到的是紅絲不是霧」）——這是一個算錯，不是品味問題：**
  `_DensityLo` 原本設 0.30，而 **Smoke1 單層取樣的中位數就是 0.314**（80/255）——門檻剛好卡在中位數上，等於把一半以上的霧體直接砍成 0，只剩 Smoke2 的亮絲（接近 1.0）推得過門檻。畫面上就變成「黑底上的紅色藤蔓」。**教訓：remap 的門檻要對照素材的實際直方圖定，不能憑感覺。** Smoke1 的分位數記在這裡供之後調參：5%=0.051、25%=0.173、50%=0.314、75%=0.478、95%=0.694。
  一起修的四件事（作者要求「整片濃厚紅霧蓋住背景，聚攏時要有滾滾紅塵的密度感」）：
  - **厚重層兩次取樣改用 screen 混合 `1-(1-a)(1-b)` 取代加權平均**：兩層霧疊在一起物理上是**更不透明**，平均只會把值拉回中位、霧永遠濃不起來。這是「整片濃厚」的關鍵一步（中位密度 0.31 → 0.53）。
  - **細絮改用乘法疊上去、不用加法**：加法會讓細絲在「沒有霧的地方」也自己長出來，那正是實機看到的紅藤蔓。乘法（`dens *= 1 + wisp * _WispBoost`）讓細絲只在有霧處提亮。
  - **「滾滾」的來源是渦流，不是壓縮**：原本聚攏只做「取樣座標往外拉」＝霧只是變小，不會翻滾。加上**繞中心旋轉且角速度隨半徑遞減**（中心快、外圍幾乎不動）才滾得起來；旋轉要在等比座標下做（先乘 `_Aspect`），否則寬螢幕會轉成歪斜的橢圓。另外聚攏時 domain warp 強度加倍（`_GatherWarpBoost`），翻騰跟著加劇。
  - **佈滿階段的徑向遮罩要完全不衰減**：`r0` 要大於畫面對角距離（16:9 約 1.02），原本 0.95 會把四角吃掉一圈，看起來就不是「整個畫面都是霧」。聚攏後的半徑也刻意留大——滾滾紅塵是一大團在翻，不是縮成一顆球。
  濃度改後的分布（`_DensityLo` 0.08／`_Hi` 0.78／`_MaxOpacity` 0.98／`_SceneDarken` 0.55）：畫面最稀薄處約 55% 不透明（背景隱約可見）、中位約 69%、濃處 97%，邊緣因 `_EdgeDarken` 再壓到約 84%。
  **⚠ 第二次實機後的修正（同日，作者回報「還是沒有紅霧的感覺」——畫面讀起來是紅色牆面上的黑色汙漬）。這是一個觀念錯誤，值得單獨記：**
  **霧的濃淡要做在「不透明度」上，不是做在「顏色」上。** 前一版把 alpha 寫成 `max(dens * 0.98, 底線)`，大部分區域被夾在 0.98 ⇒ **alpha 幾乎恆定**、整片蓋死背景；濃淡全部跑到顏色去（`_DeepColor` 近黑 ↔ `_GlowColor` 亮紅），於是畫面變成「紅底＋黑斑」的二色圖，眼睛讀成一張不透明的貼圖。
  **人之所以認得出某個東西是霧，線索是「透過它能看到後面的東西、而且各處程度不一」。** 所以：顏色要幾乎不變（紅霧就是紅的，`_DeepColor` 改成暗紅、**不可以是黑的**），變化留給 alpha（`lerp(_FogMinAlpha 0.45, _MaxOpacity 0.95, body)` 連續變化，薄處背景透 47%、濃處只透 6%）。
  一起修的三件事：
  - **細絮不參與遮蔽**：把 Smoke2 的高頻塞進 alpha 會讓霧的輪廓破碎銳利，像墨漬或苔癬。真實煙霧的高頻出現在**亮度**上不是輪廓上，所以細絮改成只加在顏色上（乘 body，只在有霧處發亮）。
  - **邊緣壓暗改成壓顏色、不壓 alpha**：壓 alpha 會讓四周變成一圈實心暗紅，霧感更差。
  - remap 區間放寬到 `0.10~0.95`，讓 body 的分布是 0.15／0.50／0.84（25/50/75 分位）而不是塞滿 1；流速 `_FlowSpeed` 1.0→1.8（太慢會讓翻騰讀起來像一張貼圖在滑動）。
  **⚠ 第三次實機後的修正（同日，作者回報「好像沒改到，跟上一張差不多」）——兩個都是觀念層的錯：**
  - **① 在近乎全黑的場景裡，「霧薄處透出背景」和「霧薄處畫黑色」在視覺上是同一件事。** 上一版把濃淡從顏色搬到 alpha，物理上對了，但 boss 房本身就接近全黑 ⇒ 透出來的還是黑 ⇒ 畫面看起來幾乎沒變。**這是「改對了但看不出來」的典型**：修正的維度沒有錯，錯在那個維度在這個場景裡不產生可見差異。
  - **② 只做「遮蔽」的霧永遠是一層有洞的紅膜。真實的霧會散射光線——濃的地方比背景更亮，那個「亮起來」才是眼睛認出霧的線索。** 所以混合模式改成 **premultiplied alpha（`Blend One OneMinusSrcAlpha`）**：rgb 可以超過 alpha 該有的量，多出來的就是散射光（`_Scatter`）。一般 UI 的 `SrcAlpha OneMinusSrcAlpha` 做不到這件事。⚠ 代價是 frag 要自己把 rgb 乘上 alpha，頂點色 alpha（CanvasGroup 淡入淡出）必須同時乘進 rgb 與 a，`UNITY_UI_CLIP_RECT` 的裁切也要同時乘 rgb——漏掉任何一個都會在淡出或裁切邊界留下發光殘影。
  - **③ 尺度錯了：霧團太小。** 取樣縮放原本 1.00/1.63（＋細絮 1.35/2.30），1254px 的圖鋪在全螢幕上橫向排了十幾個小團，讀起來是「斑駁的牆面紋理」不是霧。改成 0.42/0.75（細絮 0.60/1.05），畫面上只剩三五個大團。**放大後圖會變糊，但那正好——霧本來就是柔的。** 縮放改小後同樣的 uv 偏移在畫面上跑得更快，`_FlowSpeed` 要往回收（1.8→0.8）。
  改後的明暗（估算）：霧濃處約 sRGB (219,89,69) 的亮紅、霧薄處約 (117,59,56) 的暗紅，亮度差兩倍且都在紅色調內——有明暗起伏、不會出現黑斑。
  **⚠ 定案：霧改成黑霧，不是紅霧（作者判斷）。** 「強敵現身」文字圖本身就是紅的，紅字疊在紅霧上完全沒有對比、字根本讀不出來。改成**黑為主、灰白為輔**之後，紅色成為畫面上唯一的強調色、只屬於文字——這才符合 [ART_DIRECTION.md](ART_DIRECTION.md) 紀律一（強調色是稀缺資源）與紀律二（背景最暗、文字最亮）。**這一步早該做**：前面調了三輪霧的質感，卻沒注意到「紅底紅字」這個更根本的問題，是把力氣花錯地方。以後做任何「文字疊在效果上」的演出，**先確定文字與底的明度/色相對比夠**，再談效果好不好看。
  黑霧的顏色上限受文字亮度約束：`_GlowColor`（霧濃處）刻意壓在中灰，加上散射與霧脊加亮後最亮約 sRGB 110~130，文字亮處約 200，對比才拉得開。要找回一點血色的話把 `_GlowColor` 往**暖灰**偏一點就好，不要調成紅的。
  **⚠ 最終定調：濃重籠罩，不是薄霧**（作者：「不要再用這麼薄會看到背景的霧了，我希望是濃重的霧氣籠罩整個畫面」）。三個 alpha 全部拉到接近 1（`_FogMinAlpha` 0.92／`_MaxOpacity` 1.0／`_SceneDarken` 0.90），背景基本被蓋住。
  **這是一個刻意的取捨，也推翻了前面幾輪的方向**：霧的層次原本想靠「透出背景的程度」表現（那是物理上正確的霧），但在近乎全黑的 boss 房裡，透出來的暗背景與畫上去的黑在視覺上無法區分，試了三輪都不行。改成不透之後，**層次完全交給顏色的明暗**——`_DeepColor`↔`_GlowColor` 的落差、散射光 `_Scatter`、細絮的白絲。少了背景的干擾，這條路反而好控制。想要「透得出背景的薄霧」就把 `_FogMinAlpha` 拉回 0.5 上下，但要有心理準備會回到黑斑的老問題。
  同時修掉一個 premultiplied 混合的典型陷阱：**吹散裁切原本只乘在 alpha 上，沒乘散射光** ⇒ 霧散開之後畫面會留下一整片發光殘影。散射光是「加上去的光」、不受 alpha 約束，任何會讓霧消失的係數都必須同時乘它。
  **⚠ 演出順序改版（作者定案）：霧全程籠罩，只有文字先散，霧留到最後才散。**
  `黑霧籠罩 → 強敵現身（凝聚）→ 強敵現身散去（霧不動）→ 頭像/名牌左右進入（疊在霧上）→ 名字浮現 → 撐一拍 → 頭像/名牌左右滑出 → 霧散去 → 收`（全長約 8.7 秒）。改動重點：
  - 文字與霧的吹散**時機拆開**（`TextBlowSeconds` / `FogBlowSeconds` 兩個獨立參數，shader 端各吃各的 `_Progress`）。舊版兩者共用一條曲線＝「同一陣風吹走」；新版的概念是「**霧是舞台**，文字與 boss 資訊在上面輪流上下場」。
  - 頭像/名牌加了**滑出**（`SlideOutSeconds`）：與進場共用同一組座標、方向相反，但退場用 **ease-in**（1→0 的三次方）才有被拉走的力道；用 ease-out 會變成慢慢飄出去、收尾軟掉。
  - 霧的聚攏加上限 `GatherMax`（預設 0.35）：霧要全程籠罩住兩側的頭像與名牌，收太緊兩側就空了。設 0 = 完全不聚攏。
  - **血色暈影預設關閉**（`VignetteAlpha` 0）：作者要求拿掉那層紅色底色漸層——霧本身已是全畫面暗底，再疊一層暗紅只會把黑霧染回紅的。
  - **壓黑底版（`_dim`）現在只剩一個用途：霧不可用時的後備**。shader 或密度圖載不到時前奏整段跳過（各段時長歸零、直接從頭像演起），那時要靠它壓住場景，否則頭像會疊在明亮的房間上。
  ⚠ **未實機驗證**：Cowork 這邊編譯不了 Unity。三處可調：節奏在面板 Inspector 的「前奏」那組（Play 中即時調、重觸發就套用）、霧的濃淡與顏色在 `BossAura.shader` 的 Properties 預設值、煙的質感在 `SmokeDissolve.shader`。**濃霧版的體積感靠 `_Scatter`（散射加光）與 `_GlowColor`↔`_DeepColor` 的落差**；覺得太亮跟文字搶戲就降 `_GlowColor`；霧團太碎則是把四個取樣縮放一起調小。Linear 色彩空間疊色比直覺重（PROBLEMS **E11**）。

* [x] **怪物常駐體光：暗場景裡怪物終於看得見輪廓**（2026-09-04，改 `Assets/Scripts/AI/MonsterSpawner.cs`；見 [ATMOSPHERE.md](ATMOSPHERE.md)〈怪物常駐體光〉）：作者回報紅嫁衣場景裡怪物的輪廓完全看不清楚、連紅嫁衣女殭屍都一樣，要求比照「主角沒帶佛燈時身上的微光」。**這是全域機制，不是某個關卡的特例**——以後每隻怪、每張暗地圖都適用。
  **做法：沿用既有機制，一行新系統都沒加。** 主角那個微光是 `AtmosphereController.BuildLights` 裡寫死的「玩家常駐體光」；怪物這邊只要在生成時掛一顆 `LightSource`，氛圍系統自己就會收走（它本來就是「會發光的世界物件」的通用標記）。半徑 1.1×CSV 的 `Scale`、亮度 0.30、柔邊 0.30、`flicker=0` 恆定不呼吸，光色**陰冷青白**——刻意與玩家的微暖白區隔，暗場景一眼分得出敵我（作者指定）。
  **⚠ 掛點是 `MonsterSpawner.SpawnMonster`，不是 `MonsterController.Start()`——這點是這條記錄的重點：** **NPC 也是用 `MonsterController` 當地基**（`NpcSpawner` 一樣 `AddComponent<MonsterController>`，連 `MonsterAnimator`/`MonsterActuator`/`BlobShadow` 都沿用），掛在 Start 裡的話 NPC 會跟著發鬼光。放在 SpawnMonster ＝ 怪物有、NPC 沒有，意圖明確，也不必依賴「元件加入順序 vs Start 時序」這種脆弱前提。**以後要給「所有怪物」加什麼東西，都要先想一下 NPC 會不會被掃到。**
  其他刻意的決定：prefab **自帶 `LightSource` 的怪不覆蓋**（設計上就會發光的怪，尊重原設定）；死亡不必特別關光（`Die()` 之後 `LateUpdate` 就 `Destroy`，`LightSource.OnDisable` 自動退出登記表）；恆定不呼吸是因為場上一堆怪一起明滅畫面會到處閃（玩家體光當初也是同一個理由）。
  **⚠ 已知限制**：體光與場景的燈**共用同一個 20 盞名額**（`AtmosphereController.MaxLights`）。排序鍵是「距離 − 半徑」，體光半徑很小 ⇒ 不會擠掉燈籠火把（安全的那一半）；但反過來，**某張圖若擺了非常多盞燈，怪物體光可能進不了前 20 名**。症狀是「這張圖的怪就是沒有光」，先去數那張圖的發光地上物數量，不要先懷疑這段程式。
  **⚠ 第一次實機後的修正：參數照抄玩家體光是錯的。** 作者回報新娘房的紅嫁衣 boss 與她召喚的怪「看起來似乎沒有光圈」。查證過程（都排除了）：那張圖 `Atmosphere=2` 會吃照明；全圖只有 4 盞燈、0 個發光地上物，離 20 盞名額差很遠；boss（id 13）`PrefabPath` 空、走程式建怪那條線，`Scale=1`，也沒有自帶 `LightSource` 會讓我跳過；地圖出生點／重生器／召喚三條路全部經過 `SpawnMonster`。**真正的原因是強度不足**——boss 站的位置離最近那盞燈（radius 3）超過 3.7 格、確實在暗處，但體光只有亮度 0.35，在幽暗（壓暗 0.8）的底亮 0.2 上只提到 0.3 上下，肉眼讀不出輪廓。
  **通則：玩家體光與怪物體光的目的不同，數值不能互抄。** 玩家體光要「微弱到照不了路」（玩家本來就知道自己在哪，只需要一點提示，而且要維持點燈壓力）；**怪物體光要被「認出來」**，門檻高得多。改成半徑 1.5、亮度 0.55（玩家仍是 1.2／0.35）。
  **順手補了一個工具：`F9` 印出這一幀真正餵給 shader 的光源清單**（`AtmosphereController.DumpLightsIfRequested`）。光「看起來沒作用」時，原因可能是氛圍不吃照明／沒掛上／被名額擠掉／太弱，**光看畫面分不出是哪一種**，這次就是查了半天才確定是最後一種。快照會印氛圍 type、吃不吃照明、本幀盞數，以及每盞的來源物件名／座標／半徑／亮度。同家族：`P` 效能面板、`F8` 關卡進度。
  **⚠ 第二次實機後：新娘房的 boss 與她的召喚物仍然完全沒有光，一般怪物卻很明顯（作者提供左右對比截圖，差異極大）。**
  走查所有已知生成路徑——地圖出生點（`MapLoader`，含 `gated`/重複產生交給 `MapMonsterRespawner` 的分支）、`SummonSystem`（`MonsterWeaponUser` 委派給它）——**全部都會經過 `SpawnMonster`**；boss（id 13）`PrefabPath` 空、`Scale=1`、沒有自帶 `LightSource`；那張圖只有 4 盞燈、`Atmosphere=2` 會吃照明。**帳面上完全對不上**，而我沒有執行環境、拿不到執行期事實。
  所以改成兩手：
  - **保險**：在 `MonsterController.Start` 也補一道（與 `BlobShadow`／`YSortByFeet` 並列，**每隻怪一定會跑到**），兩邊都呼叫同一個 `MonsterSpawner.AttachMonsterGlow`、方法自己判重。**任何沒經過 `SpawnMonster` 的漏網路徑都會被接住。** ⚠ 這一道必須用 `GetComponent<NpcAgent>() == null` 排除 NPC——NPC 沿用整套怪物地基，連影子都是靠 Start 掛的。
  - **診斷**：`AttachMonsterGlow` 在 Editor 下印一行 `[MonsterGlow] 掛上體光：<物件名> …`（`#if UNITY_EDITOR`，不進 build）。**沒印到＝生成路徑沒被涵蓋；印了但畫面沒光＝被 20 盞名額擠掉或參數太弱**（再按 F9 分辨）。
  **通則：查不出原因又拿不到執行期資料時，與其反覆猜，不如「把所有路徑都接住」＋「讓下一次能一眼分辨」。** 前者讓症狀當場消失，後者讓真正的原因下次自己浮出來。
  **⚠ 找到真正的原因了（診斷 log 立了大功）：`LightSource` 那套光是乘法，在深色地板上等於無效。**
  加了 log 之後，作者的 Console 顯示 boss 與召喚物**都有掛上、參數也對**（`r=1.50 i=0.55`），但畫面上就是看不見。查 `Atmosphere.shader` 的 type 2 分支才發現關鍵：`col.rgb *= lerp(0.35, 1.0, v)` —— **光的作用是「把壓暗還原」，不是「加光」**。所以光圈亮不亮完全取決於該處原本的畫面有多亮：
  - 淺色石板地（原亮度 ~0.5）：`0.175 → 0.33`，很明顯
  - 新娘房的深色木地板（原亮度 ~0.15）：`0.053 → 0.099`，兩個都是「很暗」，**肉眼分不出來**
  同一份程式、同樣的參數，**差別只在腳下地板的顏色**。而且**再怎麼調 `intensity` 都沒用**——0.55 調到 1.0 也只是把 0.15 還原成 0.15。
  **這條專案已經踩過一次**：`MemoryFxController` 檔頭寫著「紅嫁衣全 10 張圖除了提燈那一圈以外接近全黑，而泛黃／暈影／柔邊全都是乘法或壓暗，**在黑色上乘任何顏色還是黑** ⇒ 整套回憶效果等於失效」。同一個房間家族、同一個機制、同一個坑。
  **解法：新增 `Assets/Scripts/CharacterGlow.cs`——角色背後的加色光暈**（`Custom/AuraGlow`，`Blend One One`，專案既有的加色 shader），照 `BlobShadow` 的範式做（程序生成徑向漸層、獨立物件、LateUpdate 跟隨、角色銷毀自動清）。體光因此變成**兩層互補**：`LightSource` 讓周圍地面亮（亮地板上有效、有光照感），`CharacterGlow` 保證輪廓在任何地板上都看得見。光暈畫在角色**之下**（sortingOrder −1）⇒ 只有 sprite 的透明區亮起來，是「剪影浮出來」不是「整隻怪發光」。
  **通則：「把環境調亮」與「讓物件自己發光」是兩件事，乘法式照明只能做前者。** 凡是「不管背景多暗都必須看得見」的需求（角色可讀性、UI 提示、關鍵物件），都不能靠乘法照明，要用加色。
  ⚠ **未實機驗證**：Cowork 這邊編譯不了 Unity。**嫌怪在暗處看不清楚時該調的是 `MonsterGlowAdditive`（加色光暈，預設 0.30），不是 `MonsterGlowIntensity`**；Linear 色彩空間疊色比直覺重約一倍（PROBLEMS **E11**），預設值刻意保守。只在「吃照明」的氛圍才看得見（幽暗 2／噩夢 3／深海恐怖 9／鬼霧 14／冷月 18／燭火幽影 19，或 type 1＋環境壓暗），亮場景 shader 根本不讀光源、零副作用。

* [x] **邪佛廣場改尺寸：90×50 → 48×36，背景解析度 1448 → 2896**（2026-09-07，轉檔腳本 `resize_main_square.py`、改 `Main/Maps/Main_Square.dipanmap`；相關 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) §4.1、[MapEditor_DESIGN.md](MapEditor_DESIGN.md) §4.6）：作者回報廣場「格數太多、圖片解析度太低、又是玩家最常待的地方，感覺太粗糙」。
  **關鍵發現：48×36 不是等比縮小，是「把背景還原成它原本的長寬比」。** `Stage_Square.png` 一直是 4:3（1448×1086），卻被拉伸貼到 90:50（1.8:1）的畫布 ⇒ **背景在遊戲裡被橫向拉寬 35%**，沒有人發現過。`36 × 1448/1086 = 48.0` 剛好整數——這組數字等於編輯器新建對話框那顆「套用背景長寬比到畫布」算出來的。
  **所以移植是兩軸不同倍率**：`x_new = x_old × 48/90 = ×0.5333`、`y_new = y_old × 36/50 = ×0.72`。**任何人之後改地圖尺寸都要先想清楚是不是等比**，直接乘一個數會整張歪掉。地上物尺寸沒有數學上的正確答案（背景本身變形了），作者拍板統一 ×0.72（跟縱向對齊；俯視角下站立物的大小感看縱向）。
  **`MapSession.ResizeMap` 幫不上忙**：它是「左上角錨定、右/下邊增減」，只裁可走層位元圖、改 width/height，**物件與 trigger 座標完全不動**（註解明寫「超出範圍不強制刪，使用者自理」）。在編輯器裡直接改尺寸的結果是 308 個地上物原地不動、大半掉到圖外。所以走腳本轉檔。
  **兩個踩過會壞掉的地方**：
  ① **trigger 的 `cells` 不能用「相交即納入」重新光柵化**。舊格橫向只剩 0.533 格寬，相交法會讓相鄰區域共用邊界格——三座祭壇（x33~35 / 36~38 / 39~41）會黏成一片，玩家站一格同時觸發兩個 gacha 面板。改用**舊格中心映射後 floor 再去重**，算出來是 {17,18} / {19,20} / {21,22}，彼此不沾邊。
  ② **可走層重取樣要無偏**。偏向牆 ⇒ 隱形牆（**B9** 的經典症狀）、偏向可走 ⇒ 玩家走進背景牆裡。採**面積多數決**（每個新子格對它覆蓋到的舊子格加權投票，平手取中心點舊值）。實測形狀吻合率 99.42%、可走面積與等比預期只差 −0.12%、連通性 99.6%（34 個孤島，舊圖本來就有 97 個，沒有新增）。
  **移植前先盤出「不能碰的硬契約」，這是無痛的關鍵**：`entranceId` 的 `caveExit`/`center` 寫死在 `SaveConstants.cs`；13 個 trigger 的 **`name` 是鏈的連接鍵**（`next`/`linkTeleport` 都指名字），一個字都不能改；`sceneFx` 的 id `2d656e16` 被傳送點的 `linkedFx` 指名；檔名不變 ⇒ MapsTable ID 12 的 `Path` 不用動。轉完逐項自動驗過（含三座祭壇不重疊、全部座標在畫布內、`sortKey` 仍恆等於 `y`）。
  **內容比想像好搬**：308 個地上物裡 288 個是同一張 `Cultist`、12 個是 `Torch2`（與 12 盞獨立光源一一對應，光源固定在火把上方 0.4 格，照公式搬會自動維持），真正要肉眼對位的獨特物件只有 8 個（邪佛、3 祭壇、3 石板、門）。
  **流程上刻意把兩個變因分開驗**：公式只跟 width/height 有關、與圖的像素數無關，所以「先轉檔（背景還是舊圖，位置關係已是最終樣子）→ 再換圖」，萬一有問題能立刻分辨是算錯還是圖不對。
  **⚠ 未實機驗證**（Cowork 這邊跑不了 Unity）。原檔備份在 `DipanProj_MapEditor/Maps/Main/Main_Square.dipanmap.bak_90x50_20260907`（副檔名不是 `.dipanmap`，不會出現在編輯器讀檔列表）。
  **⚠ 待決**：可走層 `walkSubdiv` 仍是 4，子格 360×200 → 192×144（x 掉 47%、y 掉 28%）。**提到 8 就是 384×288、兩軸都比舊的還細、幾乎無損**，兩端都是資料驅動（`MapData.Subdiv` / `MapModel.Subdiv`），沒有任何地方寫死 4，只有新建地圖的預設值是 4；編輯器 UI 沒有改這欄的地方，要手改 JSON。牆碰撞子格會 72,000 → 110,592，但 **B6** 的 run-length 合併還在（實測 65,856 格 → 324 條 box），量級無感。
  **背景最終用 4x：5792×4344 = 121 px/格**（同日先上 2x 的 2896×2172＝60 px/格，作者擔心容量而保守；比對全專案後改用 4x）。**決策依據是「跟自己既有的標準對齊」**：BloodFang 那批底圖全部落在 120~139 px/格，維持 2x 會讓玩家待最久的廣場變成全遊戲最糊的一張。容量的疑慮有現成對照——`Stage_BloodFang_Diningroom` 是 3764×6688 = **25.2M 像素**，4x 的廣場是 5792×4344 = **25.2M 像素，完全同量級**（檔案 37.5MB、記憶體 128MB、解碼時間都一樣），這個規格專案裡已經在跑了；StreamingAssets 總量 495MB，多 27MB 只增 5%。
  顯示端：1080p 跟隨模式一格 = 108 螢幕px，2x 的 60 要被 GPU **放大 1.79 倍**（糊，而且那種情況下 mipmap 那 8MB 是白吃的——同 §2「Point 只適合 ≥1:1 放大」的道理），4x 的 121 是**縮小 0.90 倍取樣**（銳利，mipmap 才真正發揮作用）。
  **⚠ 但別誤會 Upscayl 的作用**：原圖 1448 攤在 48 格上只有 30 px/格 的真實資訊，放大幾倍都不會多出細節。4x 買到的是「AI 重建的銳利邊緣」＋「GPU 改成縮小取樣」，要真正的 121 px/格 資訊得重產原圖。
  `CharacterMipBias` 這張圖的 bias 從 log2(124/16)≈**+2.95** 降到 log2(124/120.7)≈**+0.04**（等於不再壓角色細節），但**全域還不能關掉**——18×10 那批舊圖仍是 80 px/格。
  **⚠ 改地圖尺寸會連帶弄壞「螢幕比例」寫死的表演元素——這次是新手教學的鏡頭聚焦遮罩**（作者實機截圖回報「遮罩太大且偏移」，改 `Assets/Scripts/UI/Panels/TutorialDimPanel.cs`）。`cameraFocus` 的「中央留洞」黑幕，洞的大小是 `HoleHalfX/HoleHalfY` 兩個**螢幕比例常數**、永遠置中，**與地圖、與 trigger 位置完全無關**，所以地上物縮 0.72 之後它一點都沒跟著動：傳送門從佔螢幕 26%×46% 掉到 19%×33%，洞還是 28%×48% ⇒ 從「比門大 4~8% 的貼身留白」變成「大 45%」。解法就是同一個倍率——兩個常數也 ×0.72（0.14/0.24 → **0.101/0.173**）。
  **通則：改地圖尺寸時，要一起檢查所有「螢幕比例寫死」的表演元素。** 它們不像世界座標會被轉檔腳本掃到，只能靠肉眼在遊戲裡發現。判斷法：這個 UI 的大小是不是配著「畫面上某個世界物件有多大」調出來的？是的話就要跟著同一個倍率縮。
  **順帶查明一個既有限制（這次不修）**：`ExecuteCameraFocus` 的鏡頭落點＝**trigger 格子的中心**，格解析度 ±0.5 格。這顆的 cells 中心是 (27.00,−13.50)、門實際在 (27.27,−13.41)，差 0.27 格＝1080p 上 29px——洞其實是準確置中的，是鏡頭沒把門對到正中央。這正是傳送點當初做 `markerX/markerY` 錨點的同一個理由（門的美術畫在背景圖裡、格子永遠對不齊，見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md) §4.5），**但 `cameraFocus` 沒有這套錨點**。29px ≈ 門寬的 1/11，實際遊戲看不太出來，先不動；真的要對準得給 cameraFocus 也加一組錨點（要同時動編輯器的參數 schema 與主遊戲）。
  **⚠ 續：洞縮小之後，另外兩個問題才浮出來——作者回報「還是沒對準」**（同日，改 `TutorialDimPanel.cs`＋`TriggerChain.cs`＋`triggerTypes.json`＋編輯器 `TriggerType.cs`；完整三段式病因見 [PROBLEMS.md](PROBLEMS.md) **E30**）。
  **先量再修**：截圖用 PIL 取幾條**穿過地板**（紋理均勻、不受門的明暗干擾）的水平線印亮度剖面——黑幕是均勻 alpha 疊加、洞內外差 2.5 倍，邊界一眼可見。量出來洞 = 螢幕 20.6%、中心 680（畫面中心 678）＝**洞的大小與位置都是對的**，門中心偏右 25px＝0.26 格。**要修的是鏡頭不是洞。**（⚠ 第一次我對整張圖做邊緣偵測，抓到的是門的石框、不是洞的邊界——畫面內容會騙人，要挑紋理均勻的區域取樣。）
  **① 視窗比例把洞的寬度弄窄了（一直都在的舊 bug，洞夠大時看不出來）**：跟隨模式 `orthographicSize` 固定 ⇒ **畫面高永遠 10 格，但畫面寬幾格取決於視窗寬高比**（16:9 是 17.8 格、作者的 1356×968＝1.40 視窗只有 14.0 格）。半寬寫成「螢幕寬的比例」，在窄視窗框住的世界範圍就少 21%——實測門寬 299px、洞只有 274px，左右各切 12px。**改成用世界格數定義**（`HoleTilesW/H` = 3.59×3.46 格，配門的 3.09×2.73 格），`ShowSpotlight()` 內高直接除以 `ViewTiles`(10)、寬再除以 `Screen.width/Screen.height`。驗算過 1.40／16:9／21:9／4:3 四種視窗，洞框住的世界範圍恆為 3.59×3.46 格。
  **② 給 `cameraFocus` 加 `focusX`/`focusY` 錨點**（世界座標，**兩個都填才生效**、留空退回格子中心 ⇒ 舊地圖零影響）——與傳送點的 `markerX`/`markerY` 同一個設計。刻意不用 `GetFloat` 判斷有沒有填：它 parse 失敗回 0，而 0 是合法座標，分不出「沒設」與「設成 0」。**`triggerTypes.json` 是資料驅動的，加兩個 param 定義編輯器面板就自動長出欄位、不用改編輯器程式**（`TriggerType.cs` 的 `Defaults()` 也同步加，免得 json 遺失重生成就沒了）。這顆填 (27.272, −13.417)＝`rockDoor` 的視覺中心——拆圖確認不透明像素 bbox 是置中的（dx=0.0），所以視覺中心等於物件座標；修正量 +26px，與截圖量到的 +25px 吻合。
  **通則：同一個「沒對準」的症狀可能疊了好幾個來源，只修一個會再撞到下一個。** 這次是三層：地圖改尺寸沒帶到螢幕比例常數（洞太大）＋ 螢幕比例在 X 軸本來就不等於世界大小（左右被切）＋ 聚焦點只有格子精度（偏移）。**「螢幕比例」與「世界大小」在正交投影下只有 Y 軸穩定對應**（`orthographicSize` 固定 ⇒ 畫面高恆 N 格），X 軸還乘了視窗寬高比——凡是要框住／對齊世界物件的 UI，尺寸一律用世界單位定義再換算回螢幕比例。
  順手把 `resize_main_square.py` 補上 `focusX/focusY` 的換算（與 `markerX/markerY` 同一條），下次改地圖尺寸不會漏掉錨點。

* [x] **可走層：自動生成 ＋ 編輯器內試走**（2026-09-07，新增 `DipanProj_MapEditor/Assets/Scripts/Core/WalkableAutoGen.cs`、`Tools/PlaytestController.cs`，改 `WalkableOps`／`WalkableOverlay`／`WalkableController`／`EditorUI`／`EditorBootstrap`／`EditTool`；見 [MapEditor_DESIGN.md](MapEditor_DESIGN.md) §4.4）：作者回報「塗可走層太麻煩，不管筆刷多大都一樣，而且還得做完進遊戲看看符不符合」。
  **這是兩個痛點**：前半是「第一次鋪很花力氣」，後半是「每次改完的驗證循環太長」。分別對應這次做的兩件事。
  **① 自動生成（依背景圖）**：點一塊地板當種子 → 拖容差滑桿（即時預覽）→ 套用。
  **做之前先拿全庫 22 張手塗的可走層當標準答案實測**，而不是憑感覺挑演算法——這幾個結論都是實測翻出來的：
  - **亮度門檻（Otsu）不行**：平均 IoU 只有 57%，而且 **5 張完全失敗**。原因是「地板比較亮」這個假設在室內場景不成立（有些圖地板比牆暗）。改成「以種子點的顏色為基準」才不會賭錯方向。
  - **「侵蝕→取與種子連通的一塊→膨脹回來」是整件事的關鍵**：誤判區（山壁被火光照亮的面，亮度色相都跟地板一樣）都是**零碎不連通**的，先侵蝕切斷它們與主地板的細連接就能一次濾掉。邪佛廣場 IoU **66% → 91%**。
  - **色度（去亮度）比 RGB 好**（73%→76%）：同一片地板在光照下和陰影下 RGB 差很多、色度幾乎不變。
  - **多種子點反而更差**（58%）：取「與最近種子的距離」等於放寬條件，會洩漏到牆上。所以只吃單一種子。
  **真正該看的指標不是準確率，是「還要塗幾筆」**：平均 IoU 76% 聽起來普通，但**錯誤集中在少數大塊**（最大 3 塊佔 73% 的錯誤），需要補塗的塊數**中位數只有 6 筆、最多 14 筆**。跟「從全牆開始整張手塗」比是數量級的差別——所以定位寫成「一鍵鋪好底稿」而不是「一鍵完成」。
  **⚠ 有硬天花板，文件與程式註解都寫明了**：背景圖裡沒有「可走」這個資訊、只有顏色。(a) 牆的受光面跟地板同色同亮、(b) 作者**刻意**讓某塊深色地面可走——第二類是設計意圖，任何純影像方法都猜不到。
  **② 試走**：頂部列「試走」鈕，WASD 走、Tab 切換可走層疊加、Esc 結束。**刻意與主遊戲對齊三件事**，不然「編輯器裡走得過」不代表遊戲裡走得過：碰撞用**圓**（半徑同 `CircleCollider2D` 0.4）、**分軸移動**（先試 X 再試 Y ⇒ 沿牆滑動而不是斜貼牆卡死）、相機 `orthographicSize` 固定成跟隨模式的值（畫面高 10 格）並夾在地圖邊界內——**視野一樣才看得出「這條路在遊戲裡會不會太窄」**。
  **實作上刻意的幾個決定**：
  - 自動生成**預覽階段完全不動地圖資料**（`AutoPreview` 只是一張 bool 遮罩，交給 `WalkableOverlay` 畫成青/橘），按「套用」才寫入、算一次 Undo。橘色＝「現在可走、套用後會變牆」，那是最該檢查的地方。
  - 背景圖降取樣**有快取**（key＝背景 id＋子格尺寸）：背景動輒 2500 萬像素，拖滑桿時只重跑後面幾步。`Dilate` 也**拆成橫縱兩次一維掃描**（方形結構元素可分離，O(n·2r) 而非 O(n·r²)，r=2 快 6 倍）——即時預覽是這功能好不好用的關鍵。
  - 讀背景像素**逐子格列 `GetPixels`**，不一次讀整張（5792×4344 一次會配置上百 MB）。
  - `ApplyMask` 是**整張覆蓋**，水/坑 `'2'` 會被沖掉 ⇒ **先自動生成再塗水**，已寫進註解與文件。
  - 試走**借用 `EditTool` 當模式旗標**（同 `EffectPreview` 的範式），所有既有 controller 本來就會檢查 `CurrentTool == 自己的工具`，所以自動全部停用，不必逐個改。
  **⚠ 未編譯驗證**（Cowork 這邊跑不了 Unity）。但因為改了 `Dilate` 的實作與邊界處理，**把 C# 邏輯逐行翻回 Python 重跑了一次**邪佛廣場：IoU **89.9%**（比原型的 87.0% 還好，因為 C# 版邊界不環繞、更正確），確認沒寫壞。容差預設值也照這次結果從 30 改成 **35**（30→83.6%、35→89.9%、50→87.5%）。
  **⚠ 同日實機修了三輪 UI（都是「功能對、但作者用不出來」）**：
  ① **可走面板沒有 `ScrollView`**（其他面板都有，只有它以前內容短塞得下），加了自動生成那區就爆版，**被截掉的正好是「③ 套用」鈕** —— 看不到等於功能不存在。**通則：往任何面板加東西前，先確認那個面板有沒有 ScrollView。**
  ② **生成失敗時面板什麼都不顯示**（`AutoPreview` 為 null 就整區不畫），看起來像按鈕憑空消失。改成失敗原因用橘字印在面板上，並補一顆「重新生成預覽」（套用/取消後不必重選種子）。
  ③ **預覽用「差異色」是錯的設計**：原本青＝會變可走、橘＝現在可走但這次沒抓到。作者看到路中間那張「可破壞的桌子」是橘色（那是對的：桌子破壞後才該能走，影像分不出來），想拿綠筆塗掉它，**塗完顏色卻不變** —— 因為那格仍然同時滿足「現在可走」與「預覽沒抓到」兩個條件。**預覽該顯示的是「套用後的結果」，不是「跟現況的差異」**；差異色讓作者的手工修改在預覽裡完全看不到效果。改成一律綠/紅、語意與平常筆刷一致。
  ④ **③ 那個「預覽中筆刷改預覽本身」的做法當天就出事，已推翻**（見 [PROBLEMS.md](PROBLEMS.md) **C11**）：作者生成後直接用筆刷修訓練場、存檔、進遊戲 —— 角色卡死不能動，回編輯器重開發現整張變回全牆。**塗的東西全在那張沒套用的記憶體遮罩裡，檔案存的還是原資料**（實測 6048 子格全牆、0 個可走）。而「讀別張圖再讀回來是對的」是幻覺：換地圖沒清預覽，讀回同尺寸的圖時預覽又生效了。
  **改成三道一起補**：換地圖就清掉自動生成狀態（`ReferenceEquals` 比對 `MapData` 參考）／預覽中一動筆刷就先自動套用再塗（不再有「看得到但沒生效」的中間狀態）／存檔前還有預覽就直接幫他套用。
  **通則：任何「預覽／暫存」狀態都必須回答三個問題——換資料時會不會清掉？存檔時會不會被遺漏？使用者的編輯會不會掉進去出不來？** 三個有一個沒答就會變成資料遺失。更根本：**不要讓 UI 上看得到的東西與實際資料不一致**；非得有暫態的話，就讓任何一個「會改資料的動作」自動把它落地。
  **通則：自動化工具的預覽要顯示「結果」而不是「過程」或「差異」，而且要讓人能就地修改** —— 自動生成一定有猜錯的地方（可破壞物、設計意圖），手工修正不是例外流程而是**必經流程**，設計時就要把它算進去。
  **通則：挑演算法之前先找一份「標準答案」**。這次全靠手上 22 張已經塗好的可走層當 ground truth，才會發現「亮度門檻」有五張整個失敗、「多種子」聽起來合理實際更差——這些光看畫面或憑直覺都不會知道。另一條：**衡量指標要對齊真正的痛點**，準確率 76% 讓人想放棄，換成「還要塗幾筆＝6」才看得出這功能值得做。

* [x] **背景解析度 ↔ 編輯器格數：訂出換算基準，並揪出 5 張地圖的背景被拉扁**（2026-09-07，見 [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) **§4.2**、[AGENTS.md](../AGENTS.md) 路由表）：
  作者要求「以 `BloodFang_GuessLobby`（3200×2400 配 23×20 格）為基準，以後給解析度就能推算格數」。
  **查下去發現這張圖本身是反例**：3200×2400 是 4:3（1.333），23×20 卻是 1.15 —— 背景是**拉伸貼齊畫布**的，
  所以這張圖在遊戲裡被**水平壓縮了 13.8%**（圓形地板變橢圓）。以它反推會得到「X 軸除 139.1、Y 軸除 120」這種兩軸不一致的規則，
  等於把壓縮固化成專案標準，所以沒有照做，改為實測全庫 26 張後訂出公式。<br>
  **§4.1 漏掉的那一半**：2026-09-03 那次稽核只問「px/格 夠不夠」，沒問「格數比對不對」。
  結果作者照建議把底圖都換大了（GuessLobby 1448→3200、MainLobby 1403→3750、Gallery 971→1800、InitialScene→2880×3840），
  **px/格 全部達標，卻有 5 張因為格數比沒跟著調而變形**：17 InitialScene(1.185)、18 GuessLobby / 23 Garden / 24 ArmoryRoom(0.862)、
  25 TrainingGround(0.857)、26 GrandCamp(0.875)。**換底圖時只改圖不改格數，就會製造這種「越換越歪」的狀況。**<br>
  **換算公式（§4.2）**：把 `W:H` 化簡成最簡整數比 `a:b` → `n = floor(W ÷ (128×a))` → 格數 = `(a×n) × (b×n)`。
  關鍵在於 **`a:b` 決定了合法格數只能是這個比的整數倍**：4:3 的圖只能是 4×3、8×6、…、24×18、28×21，
  **中間的值（例如 23×20）一律變形**。文件附了常見產圖尺寸的快查表與各比例的格數階梯表，直接挑一階即可。<br>
  **順手把 §4.1 的現況表整個重量**（原表是 2026-09-03 的數字，底圖換過之後已全面過期，還留著「建議換成 XXX」的待辦），
  改成 26 張全表實測、px/格 分 X/Y 兩軸列、加上「變形」欄與狀態標記。<br>
  **通則**：**拉伸貼齊的背景有兩條獨立的檢查，`px/格` 和 `格數比`，過一條不代表過另一條。**
  前者決定糊不糊、後者決定歪不歪，而且**後者在編輯器裡看不出來**（畫布本來就照格數比顯示，圖被拉扁了看起來仍然「填滿」）。
  驗收一律算 `變形 = (格數寬÷格數高) ÷ (圖寬÷圖高)`，必須是 1.000。<br>
  **同日追加修正（作者看表抓到的）**：本條初稿把門檻寫成「±0.01 內可接受」，於是把 **20 MainLobby(0.992)**
  與 **22 Diningroom(0.987)** 放行成「比例正確」。作者回報「MainLobby 好像也有點變形」——**是對的**。
  **真正的判準不是「變形數字夠不夠小」，而是「格數在不在階梯上」**：階梯上的點變形必然 <0.1%，
  所以只要看到 0.99x 就代表不在階梯上。待重製清單因此從 6 張增為 **8 張**：
  MainLobby 31×25 → **30×24**（125px/格，只縮 1 格寬 1 格高，成本最低）、Diningroom 15×27 → **18×32**。
  唯一可以停在 0.99x 的例外是**圖本身不是精確整數比**（BrewingRoom 2274×1416 = 1.60593 ≠ 8:5＝1.6），
  那種殘差改格數解不掉、要改就得改圖——所以 BrewingRoom 的 24×15 是正確的，它的問題是 px/格 只有 94.6。

* [x] **紅嫁衣逃跑「原地踏步」治本：跑之前先確認有路可跑**（2026-09-08，見 [PROBLEMS.md](PROBLEMS.md) **F18**、[BOSS_MODULE.md](BOSS_MODULE.md) §2）：
  作者重製新娘房、把場景加大讓逃跑型 boss 有地方閃躲後，回報她仍然「常常表演走路動作，即使根本沒路可走」。<br>
  **F15 那次只修掉一半**：當時把動畫判定從「指令速度」改成「實際位移」，擋得住完全靜止的假走路，
  **擋不住原地小幅來回**——而這正是實際發生的事。真正的鏈條是：
  ① `RedBridalGownBrain` 的逃跑目標是憑空外推的 `pos + 反方向×2`，**沒驗證可不可走**；
  ② 背後是牆時那點落在牆裡，`MapNavGrid.TryFindPath` 把終點 `NearestWalkable()` 吸附回來，
  她貼牆時最近可走格**就是她腳下那格** → `start == goal` → **回傳 true、路徑只有一個航點＝自己的格中心**；
  ③ `MonsterActuator` 於是滿速朝自己格中心衝、到了反轉，加上每 0.3s 觸發的 ±75° 側滑解卡左右換邊，
  抖動位移剛好高過 `MoveAnimThreshold`(0.12) → 動畫判定認定「在走」。<br>
  **修法兩層**：**① 決策端**（`RedBridalGownBrain`）逃跑前掃 ±150° 找「落點可走＋視線通得過＋不會更靠近玩家」的方向，
  偏離小的優先、加方向遲滯避免左右跳；全都不行就 `Stop()` → 位移 0 → idle。
  ⚠ 起點自己嵌在不可走格或沒有 nav 時**要退回舊行為**，否則起點不可走會讓視線檢查一律 false、變成永久 idle 卡死。
  **② 移動端**（`MonsterActuator`，通用）新增「放棄期」：連 2 次側滑都沒有效位移**且 A\* 確實無解**才停 0.6s、期滿重試；
  「且 A\* 無解」這個條件是刻意加的——被玩家或其他怪擋一下時路徑是通的，少了它會讓互相推擠的怪動不動就站著發呆。<br>
  **通則**：**尋徑 API 對不可走終點做最近格吸附，會把「沒路可走」偽裝成「有一條長度為零的路」**——憑空算出來的目標點
  （逃跑／閃避／亂走）呼叫尋徑前要自己先驗可走性，別只看回傳的 true。另外「絕不凍住」的避障設計對**追擊**是對的、
  對**逃跑**會直接變成原地踏步，兩種行為需要不同的失敗處理。<br>
  ⏳ **未實機驗證**（作者端 Unity 編譯＋進新娘房實測待做）。

* [x] **查「她不逃、過幾秒才突然跑」＝擊退窗口凍結決策；順手把跑跑停停做成參數**（2026-09-08，見 [PROBLEMS.md](PROBLEMS.md) **F19**、[BOSS_MODULE.md](BOSS_MODULE.md) §2）：
  作者實測前一條的修正後回報：靠近紅嫁衣時她原地不動、過幾秒才突然開始跑；覺得這個「跑跑停停」的節奏其實不錯，
  但自己沒提過這需求，懷疑是新做出來的 bug，要求檢查。<br>
  **不是 bug、也不是新的**。根因在 `MonsterController.Update`：`if (!_hitReaction.IsKnockedBack) { … Think(); }`
  ——**擊退期間整段跳過決策**。窗口只有 0.1s，但紅嫁衣 `InvincibleTimeMs=0` 沒有無敵幀，
  每發命中都走一次受擊流程、跨過 `KnockbackThreshold`(10) 就再開一次擊退 → 連射時窗口首尾相連 → 她大半時間沒在做決策。
  **前一條的修正只是讓它現形**：以前這段時間她因為擊退位移仍被判定「在動」而照樣播走路，
  **F15／F18 的「原地踏步」有一大部分其實就是這個**；假走路一修掉，同一段停頓就露出本相＝站著不動。<br>
  **排除掉的兩個嫌疑**（都做了實證，不是憑感覺）：① 逃跑方向掃描過嚴 → 把新娘房可走層離線重建 nav 格與膨脹規則，
  跑遍 1167 個可走格 × 16 方位 × 3 距離共 39716 組，判定「沒路可走」的只有 **1.7%**（還是不含家具的樂觀值），
  撐不起幾秒的停頓；② 候選點落到地圖外越界 → `MapNavGrid.WorldToIndex` 對 x/y 都有 `Mathf.Clamp`，不會拋例外。<br>
  **處置**：擊退是共用受擊反饋，**不動**；改把節奏做成明確參數 `FleeBurstSeconds`(1.5)／`FleeRestSeconds`(0.8)
  （跑滿一段就站著喘一段，喘息期間召喚照常；被牆卡住的幀不算進 Burst，免得她在死角空轉完額度又接著喘）。<br>
  **通則**：**好手感如果是 bug 或副作用長出來的，要馬上把它變成參數**——這次的節奏長度其實等於玩家射了多久，
  換武器就變；而且 §5 那條「給互毆的怪加 `InvincibleTimeMs`」一旦執行，這個節奏會**無預警消失**且事後極難聯想，
  所以已在該待辦旁加了指向 F19 的警告。另一條：**受擊反饋（擊退／硬直）會順便凍結 AI 決策，等於一份隱形的行為預算**，
  看到「boss 被打時不做該做的事」先查這裡。<br>
  ⏳ **未實機驗證**（節奏參數待作者實測手感）。

* [x] **逃跑節奏改綁距離：調 `Speed` 不該連帶改掉行為模式**（2026-09-08，見 [PROBLEMS.md](PROBLEMS.md) **F20**、[BOSS_MODULE.md](BOSS_MODULE.md) §2）：
  作者嫌紅嫁衣移動太快，把 `MonsterData.csv` 的 `Speed` 從 3.5 調到 0.5，結果她變成幾乎不動、
  偶爾動一下沒走兩步就停，和預期的「一樣的移動模式、只是走得慢」差很多（已先調回 3.5）。<br>
  **是前一條加的節奏參數綁錯基準**：`FleeBurstSeconds`(1.5) 是「跑多久」，所以每段跑的**距離** ＝ `Speed × 秒數`
  跟著等比縮水——3.5 → 5.25 格、0.5 → **0.75 格**（不到一個身位），而喘息 0.8 秒不變。
  時間上的動靜比其實沒變（65:35），變的是每次動作走多遠。<br>
  **修法**：Burst 改成 `FleeBurstDistance`(5 世界單位) ＋ `FleeBurstMaxSeconds`(6) 保險上限；
  累積量**量實際位移**（撞牆原地磨不算），單幀增量夾在 `MoveSpeed × dt × 1.5` 內排除擊退／傳送的位移。<br>
  **順手查出、刻意沒修的三個同類絕對值**（記在 F20 的清單裡，要做慢速怪時再回頭處理）：
  `MoveAnimThreshold`(0.12) 在 Speed 0.5 下佔 24% 會讓慢速怪假 idle、`UnstickSeconds`(0.4) 在慢速下側滑挪不到 0.2 格常脫不了困、
  `SafeRange`(6.5) 是絕對距離所以慢速時她永遠拉不開、`_fleeing` 恆真。<br>
  **通則**：**行為節奏參數要先問它該綁時間還是綁距離**——移動類（跑一段／巡邏／後退／衝刺）綁距離才會隨 `Speed` 正確縮放，
  綁時間等於讓 `Speed` 同時控制兩件事，調一個必壞另一個；冷卻、前搖、無敵時間那類才綁時間。
  另一條：**資料化的速度＋寫死的絕對門檻擺在一起就是定時炸彈**，CSV 改一個數字，門檻的相對意義就變了，
  而症狀出現在完全不相干的地方。<br>
  ⏳ **未實機驗證**。

* [x] **切圖工具支援 AutoSprite 的 perfect loop 合圖（列數改成自動推算）**（2026-09-09，見 [PROBLEMS.md](PROBLEMS.md) **G8**、[CHARACTER_SETUP.md](CHARACTER_SETUP.md)、[MONSTER_SETUP.md](MONSTER_SETUP.md)）：
  作者回報 `Gargoyle/idle` 的合圖（1280×768）用 `Project Tools → Split Sprite Sheet` 切不了、說尺寸不符。<br>
  **`SpriteSheetSplitter` 把格數寫死 5×5**，檢查 `高 % 5 == 0` → 768 % 5 = 3 被擋。
  逐格量 alpha 驗過那張圖其實完全正常：5 欄 × 3 列（每格 256）＝ 15 格、**15 格全有內容**——
  **AutoSprite 的 perfect loop 是列數變少，不是欄數變少、也不是 25 格的殘缺版**。<br>
  **修法**：欄數維持固定 5，格邊長 ＝ 寬÷5、列數 ＝ 高÷格邊長（格子是正方形所以能反推），
  再驗 `高 % 格邊長 == 0`（圖沒被裁過）與列數上限 `MaxRows`(10)。
  驗算 1280×1280→5×5、1280×768→5×3、1280×512→5×2、5120×3072→5×3 全部正確，
  而 256/512/1024 的單張幀仍被擋（見下）。<br>
  **⚠ 欄數刻意沒一起改成自動**：批次「整包就地切割」的第二道冪等保險靠的就是
  「單張幀邊長 256／512／1024 都不是 5 的倍數」；欄數一旦自由推算，正方形的單張幀會被判成合法的 1×1／2×2 合圖，
  上次切出來的幀會被再切一次（改名＋刪原檔）＝整包靜默重排。<br>
  **順手把「張數不滿 25 會怎樣」寫進 MONSTER_SETUP**：載入依 catalog 的 `frameCount`，有幾張播幾張
  （`ZhaYu/walk` 只有 8 張、`Ghost_*` 的 idle 只有 1 張＝靜態姿勢，都正常），
  但 **`AnimFPS` 是「每秒幾幀」不是「整個動作幾秒」→ 循環時間 ＝ 張數 ÷ AnimFPS**，
  換 perfect loop 要把 `AnimFPS` 一起改成新張數，否則動作會快將近一倍；
  另附幀順序是**檔名字典序**（編號務必補零）與 **Sync 不刪舊檔**（不影響播放、只是垃圾檔）兩點。<br>
  **通則**：**寫死的資料形狀假設要分清楚哪一維真的固定、哪一維只是當時剛好**；把可變的那維改成從資料反推，
  並先查固定的那維有沒有兼著別的職責（這裡兼著冪等守衛）——「全部改成自動」會順手拆掉保險，而且當下毫無症狀。<br>
  ⏳ **未實機驗證**（待作者在 Unity 編譯後對 Gargoyle idle 實跑一次）。

* [x] **切圖工具改成「從圖本身推測格線」——尺寸與格數都不必固定**（2026-09-09，見 [PROBLEMS.md](PROBLEMS.md) **G8** 的 2026-09-09 續、[CHARACTER_SETUP.md](CHARACTER_SETUP.md)）：
  作者接下來要把角色／怪物序列圖高清化（放大 2~4 倍），加上 AutoSprite 的 perfect loop 張數浮動，
  **尺寸與格數兩個維度都保證不了**，早上那個「固定 5 欄＋列數推算」的解撐不住。<br>
  **關鍵觀察**：AutoSprite 每一格的角色四周都留白 → **切對時沒有任何一格的內容會碰到格線，切錯就幾乎每格都被攔腰切開。**
  這個性質跟解析度、格數、張數全都無關，所以一次解決兩個維度。<br>
  **演算法**（`GuessGrid`）：候選格邊長 ＝ 圖寬與圖高的**公因數**（格子是正方形）→ 濾掉格邊長 <32、格數 <2 或 >64 →
  算每個候選的**貼邊率** → 取最低 → 仍 >10% 就判定認不出格線、不切並附候選一覽。<br>
  **實測分離度**（真合圖 vs 該擋下的）：Gargoyle idle **0%**、紅嫁衣 walk **0%**；
  `ZhaYu/idle` 500×500 立繪 76%、`Ghost_GrandMa` 408×612 立繪 33%、已切好的 256 幀 31%。
  ⚠ 後三個**光看尺寸的幾何過濾擋不住**（500×500 會產生 5×5 候選、256×256 會產生 8×8/4×4/2×2 候選），
  **真正的守門員是貼邊率**。<br>
  **批次守衛改成三道**：① 檔名 `_數字` 結尾跳過；② **「整包就地切割」限定：同資料夾 PNG >1 張 ＝ 已切好，跳過**
  （作者的製程：要切的動作資料夾只放一張合圖，重切就清空資料夾）——「切到檔名子資料夾」**不**套用（來源本來就多張 sheet）；
  ③ 認不出格線不切。單張模式只有第三道。<br>
  **通則**：**當「資料的形狀」再也保證不了時，改去找「資料內容裡不變的性質」**；
  挑訊號要挑**分離度大**的（0% vs 31%~100%，中間空得很開），分離度小的當守衛遲早誤判。<br>
  ⏳ **未實機驗證**（演算法已用 Unity 的左下原點索引在真實素材上離線重現、5×3 與 5×5 都推對，但待作者在 Unity 編譯後實跑）。

* [x] **新增第四個血統系列「土裔 Gaiaborn」：石像鬼 → 山嶽巨人 → 泰坦**（2026-09-09，見 [BLOODLINE.md](BLOODLINE.md) §7）：
  作者把三階的序列圖（`SequenceImage/Gargoyle`／`MountainGiant`／`Titan`）、八種情緒的說話立繪（`Talk/<同名>`）與藥劑 icon（`bloodline_Gaiaborn`）都放好之後，照 §7 的六步做完資料端。<br>
  **改動全部是 CSV，程式零改動**：表A `BloodlineSeriesTable.csv` 加 `4,Gaiaborn,土裔,40,41,42`；
  表B `BloodlineTable.csv` 加 40/41/42 三列（`SpriteFolder` 照資料夾實際名稱、`BodyScale` 一律先填 1、五屬性為佔位）；
  `ItemTable.csv` 加 304 血統藥劑・土裔（`BloodlineID=40`、`BloodlineUpgrade` 留空＝兩欄互斥）；
  `BaseBloodRoll.csv` 加 304 進血統祭壇池、權重 10（與其他三瓶同）。進階藥劑 310/311 全系列通用，**不必為土裔另做**。<br>
  **確認過「加系列」真的不用碰程式**：`grep` 整個 `Assets/Scripts` 沒有任何寫死的血統／系列清單，
  變身演出與立繪揭示面板（`BloodlineIntroPanel`）的立繪、血統名都是從表B ＋ Talk catalog 現查的；
  抽選面板的「血統」大項也早就在 `GachaPoolTable.csv` 裡、指向 `BaseBloodRoll`。這是當初把規則收斂進 `BloodlineSystem`、UI 不懂任何血統規則換來的。<br>
  **交叉驗證腳本跑過**（欄數對齊、表A→表B 每個階段 Id 都查得到、304 的 icon 檔實際存在、血統池每個 ItemId 都在 ItemTable）：全數通過。<br>
  ⏳ **作者端還要在 Unity 做三件事**：① `Project Tools → Sync Map Assets`（不同步的話執行期一張圖都載不到，角色只剩影子）；
  ② `Project Tools → 角色 → 計算影子錨點`（新角色在 `ShadowAnchorTable` 還沒有列）；③ 實機看過再定三階的 `BodyScale`。<br>
  ⚠ **幀數比其他血統少**：石像鬼 idle 15／walk 22、山嶽巨人 idle 22／walk 14、泰坦 idle 24／walk 24／attack 23，
  其他血統各動作一律 25（逐格播，不影響正確性）。已記進 [TODO.md](TODO.md)。<br>
  ⏳ **未實機驗證**。

* [x] **石像鬼／泰坦的攻擊動畫只播前 2~4 幀：給 G6 的自動演算法加一道「失效偵測」**（2026-09-09，見 [PROBLEMS.md](PROBLEMS.md) **G9**）：
  作者回報新血統攻擊只有起手抖一下、出拳沒播。**不是素材壞掉**——G6 的起播／結束幀是拿「與 idle 站姿的輪廓差異」
  當動作進度曲線，前提是「起手 ≈ 站姿 → 出手到底 → 曲線有一個峰」；石像鬼的 idle 是蹲踞石像姿、attack 全程站起來揮擊，
  曲線變成一條在高檔震盪的平線（第 0 幀就是峰值），「第一次到峰值 90%」就抓在最前面。<br>
  **量化**：用「振幅」＝(峰值−最低)÷峰值 量這條曲線有多少結構，13 個血統分得很開——
  芬里爾 84%／該隱 74%／望月者 65%／血伯爵 59%／旱魃 55%／山嶽巨人 52%／毛殭 49%／殭屍 44%
  ‖ 石像鬼 30%／泰坦 24%／狼人 21%／覓血者 19%／Base 17%。<br>
  **改法**：`ComputeActionRange` 末尾加「振幅 < 0.35 **且** 播放幀數比 < 0.2 ⇒ 退回整段照播」。
  **兩條一起判是關鍵**：只看振幅會誤傷 Base（17% 但播 9/25、表演正常）、只看幀數比會誤傷山嶽巨人
  （播 4/25 但振幅 52%＝那就是它的動作長度）。離線全量驗算：命中石像鬼(2→25)、狼人(2→25)、覓血者(2→25)、
  泰坦(4→23) 四組，**其餘九組一格不動**。順手撈出覓血者也壞著，沒人發現過。<br>
  **試過不採用**：曲線改比對 attack 自己第 1 幀 → Base 9→3、旱魃 9→2 更糟；結束幀改「最後一次到 90%」→
  四組都修好但毛殭 13→20、旱魃 9→21，推翻「只播第一拳」那個刻意設計。<br>
  **通則**：每一條「從資料自動算」的規則都有一個沒寫出來的資料形狀假設，要為它加一道「這個假設成立嗎」的偵測，
  而不是把門檻調來調去——調門檻是在同一個假設裡搬椅子。<br>
  ⏳ **未實機驗證**。

* [x] **面板上的名字四個字會折到第二行——字級寫死 ＋ uGUI 預設自動換行**（2026-09-09，見 [PROBLEMS.md](PROBLEMS.md) **E31**）：
  「山嶽巨人」在血統揭示面板的石碑上變成兩行。算得出來的必然結果：字區寬 ＝ `PlateW`(360) × `NameArea.width`(0.60) ＝ 216px、
  字級寫死 56、毛筆字全形等寬 ⇒ 216÷56 ＝ 3.86 個字，而 `UIBuilder.Text` 的預設是 `horizontalOverflow = Wrap`。
  在此之前所有血統名都是 2~3 字，把這個上限藏了好幾個月。<br>
  **改法**：`BloodlineIntroPanel.FitFontSize` 依字數夾字級 `min(56, 字區寬 ÷ 字數)`——四個字 54、五個字 43、
  三個字仍是 56（位元級零影響）。**刻意不把 `NameArea` 拉寬**：那一欄當初就是為了避開石碑左右的尖刺裝飾才收到 0.60。<br>
  ⏳ **未實機驗證**。

* [x] **血統表的 `WalkSpeed` 接上了：換血統會真的改移動速度**（2026-09-09，見 [BLOODLINE.md](BLOODLINE.md) §4）：
  作者回報「血統表裡的 WalkSpeed 好像都沒作用」，希望轉換血統後數值按表走。查證屬實——
  `BloodlineSystem.ApplyTo()` 第 2 步的註解白紙黑字寫著刻意什麼都不做（那是屬性系統還沒有時的立場，
  避免與未來的屬性系統變成兩套來源打架）。<br>
  **改法**：`ApplyTo` 加一行 `pc.SetMoveSpeed(def.WalkSpeed)`，並在 `PlayerController` 新增 `SetMoveSpeed()`。
  **為什麼要新開一支而不是直接寫 `pc.MoveSpeed`**：「正常走 ＝ 走路動畫 1 倍速」的基準有**兩份**、都是初始化時抄走
  `MoveSpeed` 的（`PlayerAnimator.ReferenceSpeed` 與 `AnimatorSpeedByVelocity.ReferenceSpeed`）——
  只改速度不改基準，速度兩倍、走路動畫也會跟著播兩倍速。`SetMoveSpeed` 刻意**不重跑** `PlayerAnimator.Setup`
  （那支會重載圖並把 sprite 打回 idle 第 0 幀，變身演出正趴著時呼叫就是 BLOODLINE §5 的坑 2），
  基準是 public 欄位、直接設就好。<br>
  **其餘四個屬性維持不套用**，理由寫進了 §4／§8：力量沒有攻擊力欄位可對（傷害在武器表）、敏捷遊戲裡沒有任何對應物；
  魔力／體力要接到 `CombatStats` 的上限，得先解掉「`ReviveFull()` 呼叫 `CombatStats.Init()` 把上限打回 Inspector 值」
  那個坑——舊版血統加 HP 就是死在這（死一次回廣場修正就消失）。<br>
  ⚠ **表裡的數字還沒改**：`WalkSpeed` 現在是人類 5、其他 12 個血統一律 10（屬性系統前的佔位值），
  接上之後等於一喝藥就兩倍速。刻意不代作者填值，已記進 [TODO.md](TODO.md)。留空／≤0 ＝不套用。<br>
  ⏳ **未實機驗證**。

* [x] **泰坦走路時影子落在身後：一段 4~5px 寬的拖曳剪影被當成第二隻腳**（2026-09-09，見 [SHADOW.md](SHADOW.md) §5b）：
  作者回報泰坦走路時影子明顯偏後方、停下來就回到腳下。**演算法沒有動**（2026-09-03 已定版，一律改表）。<br>
  **查證**：`ShadowAnchorTable` 的 `characters/titan/walk` 是 **-23.8**，而同角色 idle 是 **+7.8**——差 32px。
  離線逐幀量 walk 的最底 15% 帶，24 幀都有兩組剪影：一段 `[-67~-63]`（只有 4~5px 寬，是拖在左後方的布條／髮絲）
  與真正的腳那一大段。演算法「兩段以上取最低兩段當腳、中點」就把錨點拉到了那一側。
  **濾掉寬度 <10px 的段後重算，24 幀的真實兩腳中點中位數是 +9**。<br>
  **修法**：`AnchorX` -23.8 → **8**（實測 +9 與 idle 的 7.8 取齊，換動作時不會跳）、`Source` 改 `manual`、
  `Note` 寫明原因。**只動泰坦那一列**。<br>
  **順手全表核對**（把 13 個血統 × idle/walk/attack 的表值與「濾掉窄段後的實測腳中點」逐列比對）：
  其餘 auto 列差距都在 ±9px 內，沒有第二個需要手改的；毛殭 attack/idle 差距大是因為那兩列本來就是作者手改過的
  （原因寫在 Note），以作者的值為準。芬里爾 walk 差 +9px 是次大的，還在可接受範圍，先不動。<br>
  **這是同一種誤判的第三次**（旱魃的破布條、毛殭的前伸毛爪、泰坦的拖曳剪影），所以把「怎麼認、怎麼填」
  寫成 SHADOW.md §5b，並在〈常見狀況速查〉補一列，下次直接查表。<br>
  ⚠ `WidthPx` 129 也是被同一段窄剪影撐出來的（idle 是 147），**先沒動**——作者只回報位置。<br>
  ⏳ **未實機驗證**（改完 CSV 要讓 Unity 重新匯入才會生效）。

* [x] **血統圖依「系列」分資料夾：`SequenceImage/` 與 `Talk/` 各多一層**（2026-09-09，見 [PROBLEMS.md](PROBLEMS.md) **C12**、[BLOODLINE.md](BLOODLINE.md) §2）：
  作者反映兩個資料夾各躺 13 個血統資料夾、越來越難找。改成 `Base` 留在根層（人類不屬任何系列），
  其餘 12 個收進四個系列資料夾：`Jiangshi/`、`Bloodborn/`、`Feralborn/`、`Gaiaborn/`（名字＝表A 的 `Key`）。
  殭屍系列因此出現 `Jiangshi/Jiangshi`——**刻意保留**，規則統一成「資料夾名就是表A 的 Key」，加系列時不用想。<br>
  **動手前先把所有引用點追過一遍，結果比預期好**：四個掃描器裡有三個（`MapAssetSyncTool`、`MapIO`、
  `Tools/sync_map_assets.sh`）本來就是遞迴的（走到「直接含 PNG 的葉資料夾」為止），消費端
  `PlayerSpriteLibrary`（鍵＝Marker 之後的整段尾巴）與 `DramaTalkDatabase`（字串串接）也從不解析層數
  ⇒ **只要 `BloodlineTable.SpriteFolder` 改成 `Feralborn/Werewolf` 這種相對路徑，這六條全部自動接上、零程式改動**。
  地圖編輯器專案只碰 `Monsters/SequenceImage`，完全不受影響。<br>
  **唯一要改的是 `ShadowAnchorTool.Scan`**：它寫死兩層（`GetDirectories` 當角色、再 `GetDirectories` 當動作），
  加一層後會把系列當角色、血統當動作，然後在血統資料夾裡找不到直接的 PNG → `continue` → **整個系列靜默消失、一列都不產生**。
  改成與 Sync 工具同一條規則（`AllDirectories` ＋「沒有直接 PNG 就跳過」，角色名＝葉資料夾的上層相對路徑，新增 `RelDir`）。<br>
  **實際做的六件事**：① 搬 12 個資料夾 ×2（含各自的 `.meta`，Unity 才認得是移動而不是刪除＋新增）；
  ② `BloodlineTable.csv` 12 列的 `SpriteFolder`；③ `ShadowAnchorTable.csv` 48 個 Key 補系列段
  （**含三筆 manual 手改與昨天改的 `titan/walk`，不補就全部失效**）；④ `ShadowAnchorTool` 改遞迴；
  ⑤ `StreamingAssets/MapAssets/Main/Characters/` 整包移出（Sync 只推不刪，不清的話舊路徑 1269 張會跟新路徑並存；
  順便清掉本來就殘留的 102 張）；⑥ `SequenceImage/Nightborn` 空殼（0 張 PNG、狂族改名前的殘留）移出。
  ⑤⑥ 都移到專案根的 `_to_delete/`（約 248MB）等作者自己刪。<br>
  **驗算**：`SpriteFolder` 13 筆全部對得到 SequenceImage 與 Talk 的實體資料夾；模擬 catalog 尾巴 × 26 個鍵全中；
  `ShadowAnchorTable` 的 52 個 characters 列 ↔ 52 個實際動作資料夾，**孤兒 0、遺漏 0**。<br>
  ⏳ **未實機驗證**；作者要在 Unity 跑 `Sync Map Assets` 重建 StreamingAssets 與 catalog。

* [x] **第三階血統的「神格特效」三層骨架（環繞電光／背後圓盤／移動殘影），先接上該隱**（2026-09-10，見 [BLOODLINE.md](BLOODLINE.md) §5b、[VFX.md](VFX.md)）：
  作者要讓四個第三階（旱魃／該隱／芬里爾／泰坦）「像神明一樣」——渾身電光、背後血色圓盤、走路留流光。
  這次做的是**三層通用骨架＋資料驅動接法**，只填該隱一列讓他先看觀感，其餘三個之後填表即可。<br>
  **素材成本意外是零**：特效庫裡 `fx3_lightning_aura`（＝變身電弧的來源）本來就有 **blue/green/orange/red/violet/yellow 六色**、各 22 幀，
  四個血統各挑一色就好，連換色都不必。該隱用 red，複製進 `Resources/VfxEffects/BloodlineAura/Cain/`（.meta 沿用
  TransformAura 的匯入設定＝Point 濾鏡、PPU 100、無 mipmap，只換 guid）。<br>
  **資料驅動**：`BloodlineTable.csv` 尾端加五欄 `AuraVfxId / HaloStyle / HaloColor / TrailStyle / TrailColor`。
  ⚠ **現有 12 列一個字都沒動**——`CsvUtil.Field` 讀不到的欄位回空字串，所以「舊列 = 12 欄 = 三層全關」天生成立。
  只有該隱那列補了 `32,Disc,A01018,Fade,C0202A`。**刻意不在程式裡判斷「是不是第三階」**：誰有特效由表決定，
  之後想給二階加一點光、或讓某隻 boss 共用同一套，都只要填表。<br>
  **三層各自踩到的既有結論（都不是重新發明）**：<br>
  ① **環繞電光** `BloodlineAura`：生法直接沿用變身演出那三行（`SpawnLoopSizedToHeight` + `SetParent`）。
  但常駐比短演出多兩件事——**排序必須每幀接管**（VfxTable 的 22050 是表演層固定值，常駐會變成「玩家走到柱子後面、
  電光還飄在柱子前面」，改成與角色同一條 Y 排序算式 +1）；**生死要有人負責**（`Duration=-1` 永不自毀）。
  另加了防呆：若表格忘了填 `Loop=1/Duration=-1`，特效播完就沒、而元件看到 null 會補生一顆 ⇒ **每幀生一顆特效**，
  所以「生出來馬上就沒」連三次就停用該層並印警告。<br>
  ② **背後圓盤** `BloodlineHalo`：**圖是程式畫的**（同心環＋放射線＋外緣光暈，另有 Cracked／Crescent／Stone 三種樣式備用）。
  這與 BossAura 檔頭「純程序 noise 生不出煙的絲與捲、形狀要美術畫」**不衝突**——那條講的是**有機形狀**，圓盤是**幾何形狀**。
  貼圖是 **premultiplied（RGB = alpha）**所以透明區 RGB = 0，用 `AdditiveGlow` 不會疊出方塊（**E12** 佛光那個坑）。
  **畫在角色之下**：圓盤中央被角色不透明的身體擋住、只露外圈，所以不會跟身上的加色電光互相洗掉（**E13** 的零和陷阱）——
  ⚠ 反過來疊到角色之上就會踩到。獨立物件而非子物件、程序生成貼圖、加色，這三條都是從 `CharacterGlow`／`BlobShadow` 沿用的。
  刻意**不做 static 快取**（CharacterGlow 有，因為要餵全場的怪；玩家只有一個），順便避開 **I8** 的陣列型 static 殘留。<br>
  ③ **移動殘影** `BloodlineTrail`：全專案搜過**沒有任何現成殘影機制**（`TrailEffectID` 那套是子彈沿路種特效，不是角色），
  所以新寫。作法最省：不畫新圖，每走一段距離把**當下那一格 sprite** 複製成獨立 SpriteRenderer 留在原地、染色淡出。
  三個要點：殘影**不能掛在玩家底下**（會跟著走）、要連 `flipX` 與 `lossyScale` 一起複製（角色圖是執行期 `Sprite.Create` 的）、
  alpha 填「感覺值的一半」（**E11** Linear 疊色）。<br>
  **共通**：位置一律走 `BodyCenterWorldPos` / `ScaledCharacterHeight`，不碰 `transform.position` 與 `bounds`（**E14**）；
  兩層「撐過體型變更」的效果已加進 `PlayerController.RefreshBodyScaledVisuals()`（芬里爾 1.5 倍體型才不會停在舊尺寸）；
  三層都由 `BloodlineSystem.ApplyTo` 收斂式套用，玩家物件重建（換圖／死亡回廣場）會自動重掛。<br>
  ✅ **實機看過了**（作者截圖）：三層都正確生成、圓盤確實畫在角色背後、排序沒問題。<br>
  **第一輪回饋（同日）**：① **該隱不要環繞電光**——電弧的白色像素太搶眼、把角色本身蓋過去，
  他要的是「只有背後那圈紅環」⇒ 該隱的 `AuraVfxId` 清空（id 32 與 22 張素材留著，其他血統要用可直接接）。
  ② **紅環太細** ⇒ `BloodlineHalo.RingWidth` 從 0.10 加粗到 **0.20**，並改成
  「主環半徑隨粗細自動內縮」（不縮的話加粗會撞上外緣柔化被切平）＋ **Play 中拉滑桿即時重畫**。<br>
  **第二輪（同日，做旱魃時連帶修的）**：加粗之後才發現**高斯環根本做不出「粗的線」**——
  它的寬度同時是柔化距離，一加粗整條就散成一團暈。改成**平頂環帶**（中間實心、只有邊緣柔化）才是作者要的粗線。
  順手把 `Mathf.SmoothStep` 全部換成自寫的 `SStep`：Unity 那支是「在 from/to 之間插值」不是 GLSL 的門檻語意，
  寫成 `SmoothStep(0f,1f,Clamp01(x))` 剛好等價、換個門檻就靜默算錯。兩條都記進 [PROBLEMS.md](PROBLEMS.md) **E32**。
  另加 `FillAmount`（圓盤內部填充濃度，0=只有環、1=實心面），與 `RingWidth` 一樣**Play 中拉滑桿即時重畫**。<br>
  **旱魃**（`Cracked` 龜裂赤日輪 ＋ 焦紅殘影）已填表，同樣**先不給電光**。<br>
  **順手建立了離線預覽**：把 `Shape()` 原樣搬到 Python，模擬石板地上的 Linear 加色畫出四種樣式
  （`TempImage/BloodlineHalo/`）。**改形狀不必進 Unity 就能先看**——這次就是靠它發現「加粗變暈」與
  Crescent／Stone 的實際樣子。⚠ 改算式要記得同步過去，否則預覽會騙人。<br>
  **第三輪（同日）**：旱魃第一版給 `Cracked`（龜裂赤日輪），作者實機的評語是**「跟該隱太像」**——
  兩個都是罩住全身的大圓環、只差顏色，遠看分不出來。改成新樣式 **`Disk`：只比頭大一圈、貼在腦後的
  金色實心圓**（佛像頭光），象徵旱災之神。**通則：血統之間的辨識度要靠輪廓與位置，不能只靠顏色。**<br>
  連帶把**尺寸與位置改成由樣式決定**（`StyleDefaults`）——身光與頭光本來就是兩種東西，
  不該共用一組全域數字。`Disk` 的 0.36 / 0.34 是**拿作者的實機截圖量出來的**
  （程式偵測角色暗色範圍得可見高 216px、頭部在上緣 75px 內），不是憑感覺給。<br>
  **定位方式值得留著**：把候選頭光**直接疊在他的截圖上**（用暗度遮罩讓圓只畫在非角色像素上，
  模擬「畫在他後面」），四個候選一次看完就定案，省掉全部的實機來回。圖在 `TempImage/BloodlineHalo/hanba_disk.png`。<br>
  **第四輪（同日）**：作者回報**血統變身時人倒下了、旱魃的頭光還留在原地**，提議「乾脆進 dead 動作就關掉」。
  同意，而且理由比「不好對位」更硬：`dead/` 是一圖三用（死亡＋變身的倒下／爬起），
  趴著時「頭」在**水平方向**的某一端，而定位是「從身體中心**往上**偏移」——**往上偏多少都對不到**，
  是定位模型不成立、不是參數不準。<br>
  作法：`PlayerAnimator` 開放 `CurrentGeomState` 與 **`BodyFxVisible`**（白名單：idle/walk/attack 才顯示），
  三層都問它。**判斷條件刻意用 `GeomState`——它就是那三個幾何屬性（`VisibleHeight`/`FeetOffsetY`/
  `BodyCenterOffsetY`）查的同一個真相**，它回 Dead 正好等於「定位基準已經失效」，不必另外定義狀態。<br>
  **白名單而非「不是 Dead 就顯示」**：之後加新動作（受傷／施法…）會預設關閉，不會冒出特效浮在半空的意外。<br>
  **刻意不做淡出**：變身演出全程 `timeScale = 0`，用 `Time.deltaTime` 淡出會整段凍住、
  改用 unscaled 又會讓「開背包暫停」時也在淡——硬切沒有這個兩難，而且倒下那一刻緊接著就是天雷與煙霧爆開，
  看不出來。<br>
  **通則**：任何「掛在角色身上、靠身體幾何定位」的持續型效果都要問 `BodyFxVisible`——
  這跟 `RefreshBodyScaledVisuals()` 那條是同一類問題的兩面（一個是尺寸會變，一個是姿勢會變）。<br>
  **第五輪（同日）**：作者發現該隱與旱魃走路都有殘影（那是三層裡的第三層，設計表裡本來就有，
  但這幾輪注意力都在圓盤上、沒被檢視過），順勢拍板一條企劃原則：**一個血統只掛一層特效**。
  理由是產能——血統會做到 20 個以上，一個角色吃掉兩三層的話很快不夠分，而且到後面每個血統
  都變成「環＋電光＋殘影」的排列組合，反而每個都長得像。⇒ 兩列的 `TrailStyle` 清空，
  **環繞電光與移動殘影兩層現在都沒有血統在用**（留給後面的血統）。<br>
  連帶的推論記進 TODO：**辨識度的預算變緊了**，20 個血統要 20 個看得出差別的東西，
  而「換個顏色」已經被旱魃第一版驗證過不夠——填下一批之前該先做一次盤點與分配表。<br>
  **第六輪（同日）：芬里爾的火焰腳印**（作者指定「跟殘影類似，火焰搖曳很快消失」）。
  依「一個血統一層」原則，芬里爾就只有這一層、沒有圓盤。<br>
  **`BloodlineTrail` 加第二種樣式 `Fire`**：與 `Fade`（複製角色 sprite 當虛影）的差別是「放什麼」——
  `Fire` 在**腳下**放一個一次性的 Vfx。**火本身的一切（動畫、幀率、壽命、排序）都在 VfxTable 那一列**，
  元件只管「什麼時候放、放在哪」⇒ **要改火燒多久改 CSV 就好**。順勢補上第 18 欄 `TrailVfxId`
  （`Fade` 不吃它；沒填會印警告而不是靜默沒反應），Dust 之後接上也是同一條路。<br>
  **素材挑選**：特效庫三個火焰候選——`fx2_fire_burst` 是爆炸不是搖曳（排除）、
  `wills fire_A` 是細小火苗但 120 幀、`Fire/fire_looping_001` 是完整火焰帶火舌且**只有 12 幀**，
  選了最後者的 orange 版（12 幀 @18fps ≈ 0.67 秒，正好對應「很快消失」）。<br>
  **左右腳交替偏移是垂直於行進方向算的**，不是世界 X 軸——俯視角可以往任意方向走，
  用 X 軸偏的話往上下走時腳印會排成一直線。<br>
  排序走 VfxTable 的 `SortingOrder=8`（地面特效層），**這一層刻意不接管排序**：
  腳印是留在地上的一次性特效，本來就該被柱子擋住，跟常駐環繞特效的情況不同。<br>
  **第七輪（同日）**：作者回報火焰腳印「太下面了」。原因是 **`VfxManager` 生出來的 sprite 是中心 pivot**——
  把中心擺在 `FeetWorldPos` 等於特效有一半落在腳底線以下。量截圖佐證：火高 46px、角色高 213px
  （比例 0.216，與設定的 `FootFxSizeRatio` 0.20 吻合 ⇒ 大小沒問題），中心對齊等於低了 23px。
  加 `FootFxYRatio`（預設 0.5 = 底部貼齊腳底）補償。<br>
  **這條寫進 [VFX.md](VFX.md) 的 API 說明**而不是只修這一處——`Spawn`／`SpawnSizedToHeight`
  都是「中心對齊」，**任何「在腳下／地面放特效」的地方都要補半個高度**；命中／爆炸那種以命中點為中心的不受影響。<br>
  **第八輪（同日）：泰坦的踏地衝擊環**。作者從三個提案裡選了「每一步踩出環形衝擊波」，
  並**否決了附加的震屏**（理由是干擾玩家——正確，而且會跟受擊／爆炸的震動混在一起分不清）。
  素材挑選時發現名字會騙人：`pj2_ground_shockwave` 其實是**單向噴發**，真正的環形擴散是
  `impfx1_dust_impact_A`（96×96 白灰圓環＋外飛碎屑），染 `B09070` 土褐。<br>
  **連帶修掉一個抽象錯誤**：樣式原本叫 `Fire`，但泰坦放的是衝擊環卻得填 `Fire`。
  改成 **`Step`（底部貼齊腳底，給立起來的東西）／`Ground`（中心對齊腳底，給躺平貼地的東西）**——
  **樣式決定的是「機制與怎麼對齊」，素材一律由 `TrailVfxId` 指定**，換張圖不該要新樣式。
  `Fire`／`Dust` 留為 `Step` 的別名。<br>
  這個分法也**修正了 pivot 補償的適用範圍**：上一輪學到的「要抬半個高度」只對立起來的特效成立，
  躺平的環反而就是要中心對齊，抬了會往上飄。`FootFxYRatioOverride` 留 -1 就依樣式自動。<br>
  **每個腳步特效的相對大小走 VfxTable 的 `Scale` 欄**（衝擊環 1.6、火 1），不動元件的全域值——
  素材相關的參數跟著素材走。<br>
  **第九輪（同日）**：實機後作者說衝擊環**太小**，而且「我以為你要用左圖」——指的是提案乙的
  `fx1_impact_dust`（104×24 橙褐塵捲）。**同意換**：巨人踩地應該是揚起塵土，
  `impfx1_dust_impact_A` 的幾何環偏「魔法」，質感不對。改用 id **35**（新增），
  舊的衝擊環留為 id 34 備選——**血統表改一個數字就能切回來比較**。<br>
  **這一輪學到的縮放陷阱**：`SpawnSizedToHeight` 縮的是**高度**，寬度按原比例跟著走。
  塵土素材寬高比 4.33 ⇒ `Scale` 從衝擊環的 1.6 降到 **0.65**，但畫面上的**寬度反而從 1.12 翻倍到 1.98 單位**
  （高度則從 1.12 壓到 0.46，正是「貼地」要的）。**看到小於 1 的 Scale 不代表畫出來比較小**——
  已寫進 [VFX.md](VFX.md) 的 API 說明，扁素材調大小前要先算一次寬度。<br>
  染色也拿掉了（`fx1_impact_dust` 的 brown 版本身就是土色）。<br>
  **第十輪（同日）**：作者實機回報三件事——頻率太快、位置太下面、還要再大一點。<br>
  **頻率改成跟走路動畫的幀同步**（不是把間距調大）。用「走了多遠放一個」的話，一步放幾個取決於移動速度，
  而泰坦 `WalkSpeed` 2、芬里爾 5 差 2.5 倍 ⇒ **調好一邊另一邊必定錯**。
  `PlayerAnimator` 公開 `WalkFrame`／`WalkFrameCount`，元件算出循環相位、在 0.25／0.75 跨越時各放一個
  ⇒ 一個走路循環固定兩個，跟速度無關。相位跨越要處理循環繞回（0.9→0.05），已寫自測驗過六組案例。
  拿不到走路幀時仍退回距離模式。<br>
  **位置：承認 `Ground` 的中心對齊是錯的判斷。** 上一輪我把 `Step`／`Ground` 分成「底部貼齊」與「中心對齊」，
  理由是躺平的環圓心該在腳的位置——實機證明兩者一樣偏下。真正的原因是 **`FeetWorldPos` 已經是可見身體的
  最底緣**，任何往下延伸的東西都會跑到角色前面，跟躺不躺平無關。兩種樣式的預設補償統一成 0.5。
  樣式區分先留著（語意仍有意義、也還有覆寫），但別再以為它們的對齊方式不同。<br>
  **大小**：id 35 的 `Scale` 0.65 → 0.85（寬 1.98 → 2.58 世界單位）。<br>
  **第十一輪（同日）**：作者看過揚塵版後說「效果太差，換一個」，改採當初提案的**丙：身體周圍浮空繞行的碎石**
  （素材＝`pj1_rock_brown` dark）。這是四層裡唯一**新增機制**的一次——
  新元件 `BloodlineOrbit`，第 19 欄 `OrbitVfxId`。<br>
  **三個讓它像「繞著轉」而不是「貼在畫面上畫圈」的細節**：①**軌道壓扁**（俯視角的水平圓投影是扁橢圓，
  不壓扁會像在角色臉前畫圈）②**前後換排序**（轉到畫面上方＝身後 ⇒ 低於角色；下方＝身前 ⇒ 高於角色）
  ③**遠近縮放**（後面小、前面大）。第二點是關鍵，少了它就只是一圈貼在同一平面的東西。<br>
  泰坦改成只有這一層，先前做的踏地揚塵（35）與衝擊環（34）都留著沒刪、目前沒有血統在用。<br>
  **這一層對「20+ 血統辨識度不夠分」是有價值的投資**：它開的是一個機制類型，
  之後「環繞的骨頭／符咒／冰晶」都是換一張圖的事。<br>
  **第十二輪（同日）：化神期（靈根系列三階，Id 52）的青雷**。作者指定用環繞電光層＋藍色。
  **這次完全沒動程式**——複製 `fx3_lightning_aura` 的 blue 版 22 張、VfxTable 加一列 37、
  血統表填一格，就完成了。骨架的目的就是這個。<br>
  紅色電弧當初被該隱退回（太搶眼），藍色配修真題材反而合適，而且比紅色安靜。<br>
  **至此五個第三階各一層，四層機制全部都有血統在用**：
  該隱＝背後大圓環／旱魃＝頭後實心圓／芬里爾＝腳下火焰腳印／泰坦＝環繞碎石／化神期＝青雷。<br>
  **第十三輪（同日）**：作者說青雷「播得太快」。我第一次理解錯，去調 `AnimFPS`（那是把同一輪拉長成慢動作），
  作者更正：要的是**播一輪 → 停 2~3 秒 → 再播一輪**的**間歇**。幀率恢復 20，
  改在 `BloodlineAura` 加 `PulseGap`（預設 2.5 秒）。<br>
  **作法是「播完一輪就自毀、空檔過了再生一顆」，刻意不是把 renderer 關掉再打開**——
  那樣動畫會在隱藏期間繼續跑，再現身時從播到一半的地方接，看起來像卡了一下。重生才會每次都從第 0 幀開始。<br>
  「一輪多長」由元件從 VfxTable 算（張數 ÷ AnimFPS，`GetEffect` 本來就是 public），
  所以之後改表格的幀率不必回頭同步程式。<br>
  **第十四輪（同日）**：作者要求移除喝完血統藥劑跳的「血脈已定：xxx」Toast——
  「都有血脈 UI 了，不太需要額外再說明」。確實重複：後面緊接著就是四秒的立繪揭示面板。<br>
  **改法是讓 `TryDrink` 成功時把 message 填成空字串**，而不是去 UI 那邊拿掉 Toast 呼叫——
  `ItemUse` 本來就有「message 為空 ＝ 這次使用不需要對玩家說話」的約定（回血藥就是這樣），
  所以三個呼叫端一行都不用改，也不會有人漏改。<br>
  ⚠ **失敗的訊息保留**：「不能喝的時候要在按鍵當下就擋下並說明理由」是明確要求過的體驗。
  `DrinkPlan.DoneText` 與語言表 2013/2014 都留著（只是沒人顯示），要恢復就在呼叫端 Toast 它。<br>
  ⏳ 五個都還沒實機驗證觀感。`Dust` 樣式名沒有血統在用，機制本身早已可用——它要一組塵土序列圖，記在 [TODO.md](TODO.md)。

* [x] **第五個系列「靈根 SpiritRoot」＋ 每個系列補上二／三階「直達藥劑」**（2026-09-10，見 [BLOODLINE.md](BLOODLINE.md) §3）：
  作者放好靈根三階的序列圖與立繪（`SpiritRoot/{Foundation, Nascent Soul, Divine Form}`，已照 2026-09-09 的系列分層放）
  與藥劑 icon，一併要求「每個血統都加二階與三階藥劑，喝下去直接跳到那一階」。<br>
  **靈根**：表A `5,SpiritRoot,靈根,50,51,52`；表B 加 50 築基期／51 元嬰期／52 化神期
  （`SpriteFolder` 分別是 `SpiritRoot/Foundation`、`SpiritRoot/Nascent Soul`、`SpiritRoot/Divine Form`
  —— **後兩個含空白，照資料夾實際名稱填**，同血伯爵的 `Crimson Count`）；`ItemTable` 加 305、`BaseBloodRoll` 加 305 權重 10。<br>
  **直達藥劑：查完程式發現不用改任何東西**。`PlanStarter` 只做兩件事——查 `BloodlineID` 在不在表B、本世是否已定型；
  **從來沒有檢查「必須是第一階」**（第一階只是填表慣例，不是規則）。所以「`BloodlineID` 指到第 3 階」天生就合法，
  語意剛好就是「跳過前面直接變成那一階」。於是十瓶直達藥劑純粹是 `ItemTable` 加十列。<br>
  **編號規則**（新訂，寫進 §3）：`30x` 第一階／`32x` 第二階直達／`33x` 第三階直達，末位固定是系列序
  （1 殭屍／2 血族／3 狂族／4 土裔／5 靈根）；`310`／`311` 維持是全系列通用的進階藥劑。
  三瓶共用同一張系列 icon（作者指定）。<br>
  **既有規則自然延伸、沒有特例**：喝完 321（毛殭＝第 2 階）的人再喝 310（中階）會被 `PlanUpgrade` 擋掉
  （「已在此之上」），喝 311（高階）才過。<br>
  **驗算**：五個系列 × 三階全部在表B；15 瓶藥劑的 `BloodlineID` 逐一反查表A，實際落點與預期階數完全相符；
  icon 檔案都存在；ItemTable 無重複 ID。<br>
  ⚠ **兩件刻意沒做的**：① 十瓶直達藥劑**沒放進任何池**——放血統池會把「選系列」的抽中率稀釋成三分之一，
  比較像是關卡獎勵或 `unlockRoll` 的東西，作者決定；② 靈根三階的 `WalkSpeed` **留空**（＝不套用、維持 Inspector 的 5），
  沒跟著其他血統填 10，因為那些 10 本來就是待重填的佔位值。兩件都記進 §8。<br>
  ℹ 順帶：表B 這段期間被加了 `AuraVfxId`／`HaloStyle`／`HaloColor`／`TrailStyle`／`TrailColor` 五個新欄，
  目前只有旱魃與該隱填了值。新加的三列照既有寫法**只填到 `Note` 就結束、不補尾端逗號**，也沒代填任何光環設定。<br>
  ⏳ **未實機驗證**；要跑 `Sync Map Assets`（靈根的圖還沒進 StreamingAssets）與影子錨點工具（表裡還沒有這三個角色）。

* [x] **「靈脈 SpiritVein」全面改名為「靈根 SpiritRoot」**（2026-09-10，作者要求）：
  加進去的同一天改名，所以**沒有留任何相容路徑**——舊名在專案裡已完全不存在（`grep` 過整包，程式／CSV／文件／檔名皆零殘留）。<br>
  **改了六類東西**：① 四個資料夾（`GameAssets` 與 `StreamingAssets` 各兩份的 `Characters/SequenceImage/`、`Characters/Talk/`），
  **連 `.meta` 一起改名**，Unity 才認得是重新命名而不是刪除＋新增；② icon `bloodline_SpiritVein.png`（＋`.meta`，保留 GUID）；
  ③ 表A 的 `Key`／`DisplayName`／`Note`；④ 表B 三列的 `SpriteFolder`（`SpiritRoot/...`）與 `Note`；
  ⑤ `ItemTable` 三瓶（305／325／335）的 `IconPath`；⑥ 文件 BLOODLINE／PROGRESS／DRAMA。<br>
  **順手把 `catalog.json` 的 338 處路徑也一起換掉**（作者已經跑過 Sync，StreamingAssets 裡已有舊名那份），
  所以**這次不必重跑 Sync Map Assets 就能直接玩**——驗算過 36 筆 SpiritRoot 項目（12 個動作 ＋ 24 張立繪）
  的 `path` 全部指到實體檔、零筆殘留 SpiritVein。<br>
  ⚠ **本檔昨天那條「新增第五個系列」的內文也被一併換成新名**（同日改名、留舊名只會讓人搜不到）。
  對照已寫進 [BLOODLINE.md](BLOODLINE.md) §1 的系列說明裡，比照當年 Nightborn → Feralborn 的做法。<br>
  ⏳ **未實機驗證**。

* [x] **血統藥劑改名＋文案改成不劇透**（2026-09-10，作者拍板，見 [BLOODLINE.md](BLOODLINE.md) §3）：
  原本 15 瓶叫「血統藥劑・毛殭」這種「藥劑名就寫出會變成誰」的格式，等於在背包裡先把結果講完了。
  作者要的是**名稱只說「幾階＋哪個系列」、說明用形貌暗示**，讓玩家喝之前不知道會變成什麼。<br>
  **改法**：名稱一律 `<階>階<系列>藥劑`（301 1階殭屍藥劑 … 335 3階靈根藥劑）；
  `TipStats` 從「喝下直接覺醒「毛殭」血脈」改成**只有形貌描述**（「體生白毛，刀槍不入」），15 瓶各寫一句
  ——**開頭的「喝下後，」與結尾的「本世不可更改系列」都不要**（作者：喝的時候確認視窗本來就會提示，tip 裡再寫是贅述）；`TipLore` 把會點名的十條重寫（例如 332 原本寫「第一個殺人者的血」＝直接點出該隱），
  **301~305 那五條是作者自己寫的、本來就沒點名，原文保留**。`Description` 維持通用的「一次性的血統藥劑」。<br>
  **懸念的揭曉點剛好是現成的**：確認視窗用 `data.Name`（藥劑名，不洩漏），而 `DoneText`（「血脈已定：{0}」）
  與立繪揭示面板用 `def.DisplayName`（真正的血統名）——**玩家是在變身演出結束、立繪浮現的那一刻才知道自己變成了誰**。
  這是既有流程自然給的，一行程式都不用改。<br>
  **驗算**：15 瓶的 `TipStats`＋`TipLore` 逐字掃過，確認沒有任何一句出現階段血統名（系列名不算，藥劑名本來就有）；
  名稱 15 個互不重複；ItemTable 欄數無異常。`BaseBloodRoll` 的五條 Note 與 BLOODLINE §3 的對照表同步改名。<br>
  ⏳ **未實機驗證**。

* [x] **移除 `proud` / `speechless` 兩種情緒立繪**（2026-09-13，見 [DRAMA.md](DRAMA.md)）：
  作者做立繪時判斷這兩種表情幾乎用不到，決定 16 個血統全部移除。<br>
  **先查引用再動手**：程式面**沒有任何地方寫死情緒清單**——`DramaTalkDatabase.ResolvePortrait` 是把 `Actor_<情緒>`
  字串直接拼成 catalog id（`Main/Characters/Talk/<SpriteFolder>/<情緒小寫>`），立繪由 `MapAssetSyncTool` **遞迴掃資料夾**
  自動收進 catalog（所以 2026-09-09 加的系列層不影響這條路徑），唯一的真實引用來源是 CSV。
  全專案（含所有 `.dipanmap`、`NpcTable`、`MonsterData.PortraitPath`）掃下來：`speechless` **零引用**；
  `proud` 只有 `DramaTalkTable.csv` 的 ID 26（Group 10 → DramaTable ID 11，紅嫁衣恢復結尾）
  與 ID 29（Group 13 → DramaTable ID 14，序章靠柵欄休息）兩列，已改成 `Actor_Happy`。
  **就算刪了沒改 CSV 也不會壞**：找不到圖回 null → 那側立繪不顯示、對白照跑，只留一行 `LogWarning`。<br>
  **刪除範圍**：`Assets/GameAssets/Main/Characters/Talk/` 下 16 個血統資料夾的
  `proud.png`／`speechless.png` ＋ `.meta`，共 64 檔。`catalog.json` 的 64 個條目跑一次
  `Project Tools → Sync Map Assets` 重生。<br>
  **通則**：**Sync Map Assets 只複製、不清理** `StreamingAssets/MapAssets/`，
  所以刪素材或改資料夾結構之後，那邊的舊檔／舊路徑會原地留著變成孤兒，遊戲端仍讀得到已經「刪掉」的圖，
  而且該目錄整個在 `.gitignore` 裡、**git 還原不會碰它**，只能手動清或整個砍掉重 Sync。
  旁證：`StreamingAssets/MapAssets/Main/Talk/` 下還留著一整套 `actor1_*` 立繪，GameAssets 早已沒有對應來源。<br>
  **順手**：DRAMA.md 的情緒清單原本漏列 `hurt`（實際每個血統都有），這次一併補上。

* [x] **對話立繪改成自動對齊「人物」，不再量圖檔外框**（2026-09-13，見 [PROBLEMS.md](PROBLEMS.md) **E33**、[DRAMA.md](DRAMA.md)〈立繪自動對齊〉）：
  作者反映立繪越做越多、美術尺寸沒統一，對話時人物到處飄，只能在 `DramaTalkTable.csv` 一句一句填 offset 補，很麻煩。<br>
  **先量再改**：132 張立繪掃下來，不透明內容佔畫布 **0.717~1.000**、左右留白 **0~16%**、畫布比例也不統一
  （1024×1536 之外還有 1122×1402、1149×1369、500×500）。舊算法 `TalkPanel.SetAvatar` 量的是**圖檔**
  （高固定、寬＝高×圖檔比例、左立繪貼圖檔左緣），所以**留白就是誤差**——`Girl_Fear` 右留白 0%、`Girl_Smile` 15.8%，
  同一角色兩張表情差約 78px。算下來人物內側緣散佈 **217px**、人物高度散佈 59px。<br>
  **改法**：量**不透明內容框**，三件事都對齊「人」——人物高度固定、人物**內側緣**（朝畫面中央那邊）固定、人物底緣固定。
  留白多的圖只是圖檔被放大，人物一樣大、一樣位置。右立繪是鏡像的，鏡像後原圖右緣正好變成畫面上的內側緣，
  所以左右共用同一個對齊值、不必分兩套。內容框直接用 `MapSpriteLoader.GetAlphaLocalBox`（地上物碰撞用的同一支、有快取），
  **刻意不烘進 catalog.json**：catalog 有四個產生器、只有部分會烘，共用現成掃描才不會「不同機器算出不同結果」；
  對話面板是模態暫停的，第一次掃圖感覺不到。掃不到時自動退化成舊的量圖檔行為，不會不顯示。<br>
  **順手修掉的設計問題**：微調其實有兩種被混在一起填在**句子**上——補償尺寸誤差的（已被自動對齊解掉）、
  和刻意的構圖選擇（邪佛要比人大、要更沉）。後者是**那張圖的屬性、不是那句話的屬性**，
  填在句子上的後果是同一張圖出現幾次就要填幾次（邪佛那組 `1.1/-130/-130` 重複填了 10 列）。
  新增 `Assets/Data/PortraitTable.csv`（一張圖一列、整張表選填），與每句微調**疊加**（Scale 相乘、Offset 相加）；
  `DramaTalkTable` 那 6 欄退回「只放這一句的特例」，17 列已搬移清空、剩 7 列是主角逐句的演出值（那 7 列彼此衝突，確定不是圖的屬性）。<br>
  **改動**：新增 `Drama/PortraitFit.cs`（內容框 ＋ 該圖固定微調）、`Drama/PortraitTable.cs`、`Data/PortraitTable.csv`；
  改 `TalkPanel.cs`（`AvatarSideMargin` → `AvatarInnerX`，語意從「圖檔外緣」變成「人物內側緣」、值 220 → 650）、
  `DramaTalkDatabase.cs`（抽出 `ResolveCatalogId`、新增 `ResolveFit`）、`DramaTalkData.cs`、`DramaTalkTableProvider.cs`（加 Portrait CSV 欄）、
  `PlayModeStaticReset.cs`。<br>
  **通則**：**排版參數要先問「它量的是素材還是內容」**——素材留白沒有強制規範時，任何「量圖檔外框」的對齊都會隨素材飄，
  而且症狀容易被誤判成「美術畫歪了」。這個專案已經栽在同一件事上三次：地上物碰撞（**B9**）、角色影子（**E28**）、這次的立繪。<br>
  **實機第一版頭頂被切掉**（同日修）：`AvatarHeight` 量的東西從「圖檔高」換成「人物高」之後，
  人物頂端 = `576.9 − AvatarOverlap + AvatarHeight` = **1137**，而畫面高只有 1080（CanvasScaler 參考解析度）⇒ 超出 57px，
  角與頭髮被切平。⚠ **舊算法其實也超出 43px**（人物高中位 646），只是正規化之後變成
  「**全部角色一起切在同一條線上**」才變得明顯。改成 `AvatarHeight` 580／`AvatarOverlap` 130
  ⇒ 頂端 1027、留 53px，人物同時小了 12%、下移 30px。
  **通則**：**把一個量從「素材尺寸」換成「內容尺寸」時，沿用舊的數值等於偷偷放大**——
  660 本來是連留白一起算的，換成只算人物之後，同一個數字代表的東西就變大了。
  這類換算要連帶重算它的**邊界**（這裡是畫面高），不能只換語意。<br>
  ⏳ 仍**未實機驗證**的部分：`AvatarInnerX = 650`（兩人距離）是用中位數推的；
  且 `PortraitTable.csv` 要先拖進場景 `DramaTalkTableProvider` 的 **Portrait CSV** 欄才會生效。

* [x] **新增第六、七個系列：雲龍 Cloudborn（蛟人 → 螭吻 → 應龍）與熾龍 Blazeborn（龍奴 → 法夫納 → 尼德霍格）**（2026-09-13，見 [BLOODLINE.md](BLOODLINE.md) §1·§2·§3）：
  作者把兩個系列的素材都放好了——六個血統各 `idle/walk/dead/attack` 序列圖與 6 種情緒立繪，
  連 `bloodline_Cloudborn`／`bloodline_Blazeborn` 兩張藥劑 icon 都已經在 `Resources/UI/Icons/Items/positions/bloodline/`。
  照 §7 的步驟接上，**程式零改動**。<br>
  **階序是作者指定的，跟從立繪推的不一樣**：雲龍是 **蛟人(Jiao) → 螭吻(Chiwen) → 應龍(Yinglong)**——
  立繪上 Chiwen 戴斗笠、最接近人形，看圖會誤以為它是第一階；**螭吻在第二階**是作者的設定，
  後面有人覺得「龍生九子的螭吻怎麼排中間」時不要自作主張改。熾龍是 **龍奴(Thrall) → 法夫納(Fafnir) → 尼德霍格(Nidhogg)**，
  第二階 Fafnir 是完全龍形、第三階 Nidhogg 回到人形但穿熔岩鎧甲。<br>
  **改了四張表**：表A 加 `6,Cloudborn,雲龍,60,61,62` 與 `7,Blazeborn,熾龍,70,71,72`；
  表B 加六列（`SpriteFolder` = `Cloudborn/Jiao` 這種帶系列層的相對路徑）；
  `ItemTable` 加六瓶（306/326/336 雲龍、307/327/337 熾龍，末位延續系列序 6/7）；`BaseBloodRoll` 加 306、307 權重 10。
  **進階藥劑 310/311 不用動**——它全系列通用，這正是 2026-08-18 那個設計決定現在省下來的工。<br>
  **驗算**：七個系列 × 三階 = 21 個血統逐一反查——序列圖四個動作資料夾全在、立繪 6/6 全在、
  每個血統都對得到唯一一瓶藥劑、七瓶第一階都在血統池、兩張 icon 都存在、表B 19 欄與 ItemTable 18 欄的欄數與既有列一致。<br>
  **刻意留空/填 1 的**：`BodyScale` 全填 1、`WalkSpeed` 留空（＝不改速度，與靈根同樣處理——那些 10 本來就是待重填的佔位值）、
  五屬性按系列定位給概念值（雲龍偏魔力敏捷、熾龍偏力量體力）。六個血統都**還沒實機看過**，
  第三階的神格特效欄（§5b）也還沒填。<br>
  **素材已經同步過了**（作者放素材時就跑過 `Sync Map Assets`）：catalog 已含兩個系列的 36 筆立繪與 548 筆序列圖，
  StreamingAssets 實體檔也在，所以**填完表就能直接進遊戲測**，不必再跑一次。<br>
  ⏳ **要作者做**：抽祭壇或用作弊面板拿 306／307 喝下去，看三階外型、`BodyScale` 與立繪。

* [x] **應龍與尼德霍格的第三階神格特效：兩個都是零素材成本的「空位」**（2026-09-13，見 [BLOODLINE.md](BLOODLINE.md) §5b）：
  作者要給兩個新系列的第三階做特效（一水一火），要求先查庫存、挑帥氣又好用的。<br>
  **先盤點「還有哪些輪廓與位置沒被占走」**，因為 §5b 的原則是**一個血統只掛一層、辨識度靠輪廓與位置而不是顏色**。
  現況：該隱＝背後身光大環、旱魃＝腦後頭光實心圓、芬里爾＝腳下火焰腳印、泰坦＝身體周圍環繞碎石、化神期＝全身青電弧。
  **五個位置各被占一個，剩下的空位是「移動殘影（`Fade`）」與「Halo 的 `Crescent`／`Stone`／`Cracked` 三個沒人用的樣式」。**<br>
  **配法**：應龍＝**`Fade` 青色殘影**（`TrailColor=7FCFF0`）——`Fade` 這層從沒有血統用過，
  它是唯一「只在移動時出現、留在身後」的輪廓與位置，而「行雲流水」本來就是動起來才有的東西；
  尼德霍格＝**`Stone` 熔岩岩輪**（`HaloColor=B04008`）——`Stone` 是分成 12 塊的**斷續厚環**，
  與該隱那圈連續細環在輪廓上分得開。**兩個都不必加 VfxTable、不必複製素材，只填表B 兩格。**<br>
  **刻意沒給尼德霍格 `Cracked`**（龜裂赤日輪）：那是一整片連續的大圓面，正是旱魃第一版被作者退回的原因
  （「跟該隱太像」）——同樣的錯不踩第二次。也**沒有走 `AuraVfxId` 路線**（特效庫裡的 `fanfx2_wind_spell` 藍版
  拿來當應龍的雲氣纏身其實很漂亮），因為那會與化神期的全身青電弧同層同位置又同色系。<br>
  **查素材時順手記下的庫存**：特效庫每個動畫都有 6~7 種顏色變體（`Effects/<包>/<動畫>/<色>/`），
  所以「要一個藍版／橙版」通常不必重做圖，複製對應顏色資料夾即可。適合之後做環繞層的候選：
  `fanfx2_wind_spell`（風刃盤旋，最後幾幀收成一圈雲氣）、`fx2_magic_swirl`、`fx1_aura_energy`（實心圓盤＋外環，
  但那是圓盤輪廓、會跟 Halo 撞）。<br>
  **通則**：**特效的「空位」要盤點的是輪廓與位置，不是素材有沒有。** 素材庫很大、顏色變體很多，
  真正稀缺的是「看一眼就知道是誰」的位置——目前只有五個位置在用，加上這兩個是七個，
  而血統會做到 20 個以上，所以每次配之前都要先看這張占用表（§5b〈目前填了誰〉）。<br>
  **實機退回，同日改成「罩住全身」的第二版**：作者看過後說應龍的殘影「勉強還可以」但他要的是
  **一顆水球把角色籠罩在裡面**，尼德霍格的岩輪「完全不行」、要火焰 VFX。
  兩個都改成 `AuraVfxId`：應龍＝**38 水球籠罩**（`fx2_bubble_loop` 藍版，7 幀循環），
  尼德霍格＝**39 熔岩火爆**（`symmetrical_explosion_004` 橙版，12 幀環狀火爆，靠 `PulseGap` 變成間歇噴發）。<br>
  **挑素材的方法比挑到哪一個更重要**：火焰類素材幾乎都是**實心的一團**，罩上去會把角色糊掉。
  改成**量中央區的 alpha** 去篩（取中間幀、量中央 36%）：`fanfx1_power_up` 中央 alpha **233/255** ⇒ 直接淘汰、
  `fire_looping_001`／`fanfx2_fire_spell` 都有白色核心同樣淘汰；`fx2_bubble_loop` 是 68（半透明、人看得見）、
  `symmetrical_explosion_004` ≈ 0（中空環狀）。掃完全庫才發現**「中空 ＋ 循環」的罩身素材只有三種**
  （`fx3_lightning_aura`、`fx2_bubble_loop`、`scifx*_scan_loop`），所以第四種只能靠 `PulseGap`
  ——**它讓「一次性的爆發」也能當常駐層**（播一輪、留白、再播一輪），這是庫存之外多出來的一整類選擇。<br>
  **順手查證**：`VfxTable` 的 `Scale` 欄對 Aura 層**不生效**——`BloodlineAura` 走 `SpawnLoopSizedToHeight`，
  一律縮到「角色高 × 1.25」，素材原始尺寸多少都不影響。<br>
  **通則**：**替「要疊在角色身上」的特效挑素材時，第一個要問的不是好不好看，是中間空不空。**
  好看的火焰幾乎都是實心的，而實心＝蓋住角色＝白做。用中央 alpha 當篩選條件可以在翻圖之前就砍掉一半候選。<br>
  **第三版（同日）：水球改成「繞在身邊的一顆」、尼德霍格全部撤掉**。作者看過第二版後說兩個都不行——
  水球要**縮小、變成繞在身邊**（像泰坦的碎石但只要一顆、大一點），尼德霍格的先移除、設計再想。<br>
  **這一版動到程式**（前兩版都是純填表）：`BloodlineOrbit` 的 `Count`(4) 與 `SizeRatio`(0.13)
  **是寫在元件上的固定值、全血統共用**，泰坦的「一圈小碎石」與應龍的「一顆大水球」沒辦法並存。
  照 CSV 鐵則開成資料：表B 加 `OrbitCount` / `OrbitSize` 兩欄（19~20 欄），
  **留空／0 ＝ 用元件上的值**，所以泰坦那一列一格都不用改、行為完全不變；
  `SetEffect` 多收兩個參數，並把「顆數／大小」也算進「設定有沒有變」的判斷
  （只比 vfxId 的話「同一顆球改成兩顆」會不重生）。<br>
  **素材搬家**：水球從 `Resources/VfxEffects/BloodlineAura/Yinglong/` 移到 `BloodlineOrbit/Yinglong/`
  （路徑代表它屬於哪一層，留在 Aura 底下會誤導），VfxTable 38 一併改名成「應龍・環繞水球」；
  尼德霍格的火爆素材 24 檔與 VfxTable 39 整列移除，素材放到專案根的 `_to_delete/`。<br>
  **通則**：**「罩住全身」與「繞在身邊」聽起來很近，其實是兩個不同的層**
  （`BloodlineAura` 一整張圖罩上去 / `BloodlineOrbit` 離散物件繞軌道）。
  這次三版裡有兩版是在這兩者之間來回，所以描述這類需求時要先講清楚是哪一種，再談素材長相。<br>
  ⏳ **未實機驗證**：`OrbitSize=0.4` 是泰坦碎石(0.13)的三倍、`OrbitCount=1`；
  軌道半徑仍是全域的 `RadiusRatio`(0.40)，一顆大球繞起來會不會離身體太近，實機看過再調。

* [x] **第五層特效 `BloodlineAttackFx`：攻擊時才罩上來一次**（2026-09-13，見 [BLOODLINE.md](BLOODLINE.md) §5b）：
  作者在特效預覽器裡挑好了尼德霍格要的圖（`Explosions/stylized_explosion_002` red，10 幀），
  要求的是「**攻擊時才出現、罩在他身上、播慢一點；單擊播一次，壓住攻擊則播一次後隔幾秒再播**」。<br>
  **這是既有四層都做不到的時機**——`BloodlineAura`／`Halo`／`Trail`／`Orbit` 全是「站著就一直在」的常駐視覺，
  沒有任何一層綁得到「出手那一瞬間」。所以新開第五層。<br>
  **關鍵是找到「玩家出手了」的單一節點**：`PlayerController.TrySpawnFireEffect` 是
  離散武器／雷射／召喚／佛光**四條發射路徑唯一的共同節點**，所以通知只掛這一處就涵蓋全部。
  ⚠ 掛在該方法**最前面**、在「這把武器有沒有 `FireEffectID`」的 early-return 之前——
  放後面的話沒填發射特效的武器就不會通知，而那剛好是大多數武器。<br>
  **節奏用一個冷卻時間戳就夠**：`NotifyAttack()` 檢查 `Time.time - _lastAt < Gap` 就直接 return。
  「單擊每次都播得到」不必另外寫——兩次點擊之間本來就超過冷卻。表B 加 `AttackVfxId` / `AttackFxGap` 兩欄（21~22）。<br>
  **播放速度刻意不在元件裡做**：走 VfxTable 那列的 `AnimFPS`（10 幀 ÷ 8 ≒ 1.25 秒）。
  兩邊各自計時就會長出「表格改了幀率、特效長度卻沒變」這種對不起來的狀態。<br>
  **踩到一個排序的坑**：第一版照其他血統特效填了 `SortingOrder=22050`，但那是**變身演出**那種要蓋滿畫面的值；
  `BloodlineAura`／`Orbit` 之所以可以留著它，是因為那兩層**每幀自己接管排序**，而這一層沒有
  ⇒ 爆炸會浮在柱子與牆的前面。改成留空＝用 VfxManager 全域預設。<br>
  **通則**：**抄一列現成的表格設定時，要問「那一列的值是誰在用、有沒有人事後覆寫它」**。
  同一個欄位在「每幀被接管」與「照著用」兩種元件底下，填一樣的值會得到完全不同的結果。<br>
  **實機抓到一個漏接**（同日修，見 [PROBLEMS.md](PROBLEMS.md) **F21**）：作者用**持續型武器**按著左鍵，
  只播了第一次就再也沒播，看起來像 `AttackFxGap` 沒作用。**冷卻沒壞，是事件只響了一次**——
  `TrySpawnFireEffect` 雖然是四條發射路徑的共同節點，但**離散武器每發都經過、持續型只有按下那一幀經過**
  （程式本來就寫明「持續存在期間不每幀重播砲口特效」）。修法是兩種分開接：
  `PlayerController` 開一個 `IsContinuousFireActive`（與攻擊姿勢那段共用同一個判斷、不另立真相），
  `BloodlineAttackFx.Update` 每幀問它，兩條路都走同一個 `NotifyAttack()`。<br>
  **通則（比這個 bug 本身重要）**：**「玩家正在攻擊」在這個專案不是一個事件，是兩種東西**——
  離散武器是一連串瞬間事件、持續型武器是一段持續狀態。任何要綁攻擊的功能
  （特效、耗魔、集氣、成就計數…）都要先問「我要的是每一發，還是整段期間」，
  只接一邊必然漏掉另一半，而且**症狀會偽裝成別的地方壞掉**——這次就偽裝成「冷卻欄位沒作用」。<br>
  **換素材（同日）**：作者在預覽器裡挑到更合的一張——`Magic Bursts/directional_particle_burst_002` red
  （14 幀 96×128，**從地面往上竄的火焰噴發**），取代原本的球狀爆炸 `stylized_explosion_002`（已移到 `_to_delete/`）。
  **換素材連帶要換錨點**：量了每一幀的 alpha bbox 才確認這張圖的內容是「幀 1 貼在底邊、逐幀往上長、最後散在頂部」
  ⇒ **它的底邊就是地面**，照原本的「中心對齊身體中心」擺會整個懸空。
  加了 `AnchorAtFeet`（預設打勾）＝底邊對齊 `FeetWorldPos`；`SpawnSizedToHeight` 收的是中心，所以自己往上推半個高度。
  FPS 8 → 10（14 幀 ÷ 10 ≒ 1.4 秒，與原本的 1.25 秒相近）。<br>
  **通則**：**換特效素材時要一起問「這張圖的原點在哪」**——同樣是「罩在角色身上」，
  球狀爆炸的錨點是身體中心、往上竄的火焰是腳底，換圖不換錨點就會浮在半空。
  量 alpha bbox 隨幀數的移動方向（往上長／往外擴）就能判斷，不必進遊戲試。<br>
  ⏳ **未實機驗證**：`AttackFxGap=3` 秒與 `AnimFPS=10`（1.4 秒一輪）都是憑感覺給的，
  壓住連射時的節奏要實際打過才知道；高度走 `HeightRatio`(1.25)＝火舌會竄過頭頂約四分之一個身高。

* [x] **尼德霍格的影子偏到身體後方：手改錨點表，沒動演算法**（2026-09-13，見 [SHADOW.md](SHADOW.md)〈手動調整影子〉）：
  作者回報實機影子偏在角色一側。照路由表的規矩——**演算法已定版，單一角色不對一律改表**。<br>
  **怎麼確定是離群值而不是素材本來就偏**：把同一批新素材的 idle `AnchorX` 排一起看——
  `base` +1、`thrall` +1.2、`jiao` −0.5，而 `nidhogg` **−36**。同樣的站姿，真實兩腳中點不可能差 37px。
  再離線量一次（取剪影最底 8px、切成水平段、濾掉寬 <10px 的窄段、取各幀中位數）：
  nidhogg 與 thrall／jiao 落在同一水準（差 1~3px），證實它的接地點其實跟同批一樣，
  是**披風／尾巴那類窄剪影被當成第二隻腳**把錨點拉到身體後方——與旱魃（2026-09-03）、泰坦（2026-09-09）
  同一個坑，[PROBLEMS.md](PROBLEMS.md) 與 SHADOW.md §5b 都已經記過。<br>
  **改法**：`idle` −36 → **−2**、`walk` −23.9 → **−2**（walk 對齊 idle，換動作時不會跳），`Source` 改 `manual`、
  Note 寫明原因。`attack`(+1) 與 `dead`(−2) 本來就正常，沒動；`AnchorY`(50.5/54) 與 `WidthPx`(91/98)
  與同批一致，也沒動。<br>
  **通則**：**這張表最好用的除錯工具是「把同一批角色的同一個動作排在一起看」**——
  單看一列不知道 −36 是對是錯，並排就一眼看出誰是離群值。新素材進來時掃一遍 idle 那一欄，
  比進遊戲一個一個看快得多。<br>
  **同批另外三個也一起修了**（作者補了應龍／螭吻／法夫納的實機截圖，症狀相同）：
  `fafnir` idle/walk −32.5/−22.5 → **−5**；`chiwen` −25.4/−19 → **−1**；
  `yinglong` idle/walk/attack −23/−17.2/−22.5 → **0**（尾巴往左下拖地，是這批裡被騙得最兇的一個，
  連 attack 都被同一條尾巴拉走）。<br>
  **找到一個能「算出」正確值的方法**（記進 [SHADOW.md](SHADOW.md) §5c）：
  第一版我用「最底 8px 的接地段」去量，結果對應龍失效——它的尾巴本來就接地，量到的就是尾巴。
  改成量**剪影上 55% 的重心**：站姿角色的腳在軀幹正下方，而拖地的尾巴／衣襬／翅膀**只會出現在下半部**，
  所以只看上半身就不受干擾。拿三個已驗證的角色回測（base/thrall/jiao），重心與表值的差都在 ±8 內
  ⇒ 可以直接當 `AnchorX` 用；而四個壞掉的角色重心全部落在 ±6 內，等於直接證明「它們本來就該接近 0」。<br>
  ⚠ 這個方法**只對站姿成立**（idle/walk/attack）——`dead` 是趴著的，軀幹與腳的上下關係不存在，四個角色的 dead 都沒動。<br>
  **法夫納二修（同日）**：重心法給的 −5 實機還是沒對準——因為它**還有第二個前提「腳在軀幹正下方」，
  而法夫納是四足前傾的龍形**：頭與胸在右、翅膀往左後張開，重心被拉到 −5.5，但四隻爪其實在軀幹左邊約 10px。
  改用「接地層」逐幀量最底 8px 的中心：21 幀全部是 −14（它的尾巴要到距底 16px 以上才出現，不會混進來），
  設 **−15**。<br>
  **交叉檢查才是最可靠的那一步**：法夫納的 `attack`(−13) 與 `dead`(−12) 一直是 auto 算的、也一直正常
  ——**同一角色四個動作本來就該落在同一個水準**，−5 明顯不合群，−15 才對得上。
  這個檢查不需要任何量法，翻一下表就看得到，應該在動手前先做。

* [x] **血統藥劑 icon 重製：一瓶血瓶 ＋ 系列圖騰 ＋ 右下角階級星星**（2026-09-13，見 [BLOODLINE.md](BLOODLINE.md) §3〈背包圖示〉）：
  作者畫了共用血瓶 `bloodline_BaseBottle` 與七張白色系列圖騰 `bloodline_Logo_<系列Key>`，
  要把原本「一個系列一張成品圖、三階共用」改成疊圖，並在背包格右下角用銅／銀／金星標出一二三階。<br>
  **做法**：整段收進既有的唯一入口 `UI/ItemIcons.cs`（能力珠那套的第二個客戶），
  `BaseOf`／`OverlayOf` 各多一條血統分支，再加第三層 `StarOf`；uGUI 與世界端（地上掉落物）兩個多載都接。
  ArtSpec：圖騰內容寬 38%、中心下移 7.5%；星星內容寬 32%、中心 (+34%, −31%)。<br>
  **關鍵是「系列與階數不另存欄位」**：拿 ItemTable 的 `BloodlineID` 丟 `BloodlineSeriesTable.TryLocate`
  反查表A，回來的 `series.Key` **刻意就是 logo 檔名**、`stage` 就是星星級數。
  結果是**加新系列一行程式都不用改**——表A 加一列、丟一張 `bloodline_Logo_<Key>.png` 就有圖；
  ItemTable 那 21 列也不必新增欄位（`IconPath` 統一改填共用血瓶）。
  **通則：能從既有的唯一真相反查出來的東西，就不要在第二張表再存一次**（表A 的註解早就寫了同一句話）。<br>
  **踩到的兩件事**：<br>
  ① **疊圖不能直接呼叫 `IconFit.Fit`**——它會把第一次的 `sizeDelta` 記進 `IconFitBox` 當基準框，
  而疊圖的基準框是「底圖正規化後的 rect」，會隨 sprite 換而變，記住第一次的值之後就全歪。
  改成自己用公開的 `IconFit.ContentPx` 算（新增 `PlaceByContent` / `PlaceByContentWorld` 兩個小工具）。<br>
  ② **縮放一定要以「不透明內容」為準、不能以畫布**：七張 logo 六張是 1254×1254（內容佔 96%）、
  `SpiritRoot` 卻是 500×500（佔 82%），照畫布縮的話靈根的圖騰會小一號。
  珠子那層維持原本「不正規化」的算式沒動（它兩張圖同尺寸，正規化反而會破壞量好的比例）。<br>
  **驗證方式**（這台開不了 Unity）：用 PIL 在本機把 `ItemIcons` 的算式**原樣重跑一遍**
  （含 `IconFit` 那段 k 值推導），輸出七系列 × 三階 21 格的模擬背包圖比對，
  圖騰下移量就是這樣從 2.1%／5.5%／7.5%／10% 四版並排挑出來的（對齊液體 bbox 中心會浮在上緣，
  對齊最寬列會壓到瓶底）。預覽圖在 `TempImage/_preview_bloodline.png`，不需要可刪。<br>
  **沒動的東西**：兩瓶全系列通用的進階藥劑（310／311）不屬於任何系列，維持自己的成品圖、也不畫星星（作者指定）；
  舊的七張成品圖留在原地沒刪（已無人引用）。<br>
  ⚠ 六張 1254×1254 的 logo 當時**還沒有 .meta**（Unity 尚未匯入過），開 Unity 時才會匯入。
  **（2026-09-14 更正）**：不需要手動調 Max Size——`Editor/UIAssetRules.cs` 的 postprocessor
  會對 `/Resources/UI/Icons/` 底下的新圖自動套 **256 ＋ 不壓縮**，logo 在 Icons 底下，規則自動生效。

* [x] **背包道具區改成 4×3、而且格數變成「改兩個常數」就能換**（2026-09-14，見 [INVENTORY.md](INVENTORY.md)〈道具區的格線是程式鋪的〉）：
  作者回報背包裡的東西看不清楚（尤其剛重製的血統藥劑），想試 4×4／4×5／3×3 幾種版面「直到看順眼為止」。<br>
  **先量出一個會改變選項的事實**：格子大小**由列數決定，欄數只影響左右留白**——
  網格區可用空間是 577×467，高度才是瓶頸（節距 = min(577÷欄, 467÷列)）。
  所以 5×4 → **4×4 格子一樣大**（116.8），白改；要變大只能減列數：3 列 → 144.2（+31%）、2 列 → 223（太誇張）。
  **通則：作者列出的選項如果有一個「改了等於沒改」，先把這件事講清楚再動手，比做完再解釋省事。**<br>
  **真正的阻礙是素材不是程式**：`inventoryPanel_Bg.png` 上**畫死了 5×4 的金屬格線**（連交叉點鉚釘都在圖上），
  所以舊程式的 `GridCx/GridCy` 只是硬編座標去對位，換格數＝重畫整張背景。
  解法是**把格線從底圖搬到程式**：① 產 `inventoryPanel_GridBlank`（572×462）蓋掉舊格線——
  內容不是純色，是**從底圖 20 個格子的乾淨區隨機拼貼**出來的，連噪點都是原圖那一批（純色在 Linear 空間下對不上，見 **E11**）；
  ② 產 `inventoryPanel_CellFrame`（110×110）＝從底圖裁下**一個格單元**，左右上下各含**半條暗縫**，
  所以照節距平鋪就自動還原原圖的「雙線＋鉚釘」，連九宮格都不用做。<br>
  **結果**：欄列數搬進 `InventorySystem.PageCols/PageRows`（資料層，跟容量/分頁同一個真相），
  面板改成用 `CellPitch`／`CellCenter` 算，`PageSlots` ＝ 兩者相乘。**改 4×3 → 3×3 只要動那兩個數字**，素材一張都不用碰。
  兩包容量改成 `PageSlots * 2` ＝ 24（作者指定暫定兩頁），版面再變也自動維持兩頁。<br>
  **縮容量（40→24）為什麼安全**：`RestoreState` 早就對「格號越界／分錯包／被占」一律改走 `AddStack` 丟進正確那一包，
  只印 log 不掉東西。⚠ 唯一例外是舊存檔某一包裝超過 24 件 → 塞不下的會沒了，已記進 INVENTORY.md。<br>
  **驗證方式**（這台開不了 Unity）：用 PIL 把 `CellPitch`／`CellCenter` 的算式原樣重跑，
  拿**真的新素材**（不是示意圖）合成整張面板，確認拼貼底板看不出接縫、格框平鋪的線條與鉚釘對得上、
  icon 在新格子裡的大小比例正確。<br>
  **通則：把「畫在底圖上的版面」搬成「程式鋪的版面」，成本只是兩張從原圖裁出來的素材，
  換來的是版面參數化**——同一招之後要調藥水格、裝備欄排數也能用。

* [x] **背包 tooltip 加大圖預覽（附帶修掉 tooltip 會掉出畫面的老問題）**（2026-09-14，見 [INVENTORY.md](INVENTORY.md)〈呈現層〉）：
  作者要「滑到血瓶上時，tip 上方再出現一張原圖大小的圖」——因為格子只有 124px，
  三層疊出來的血統藥劑在格子裡看不出是哪個系列。<br>
  **做法**：tooltip 最上面加一張 220×220 的預覽，**走 `ItemIcons.Apply`**（畫物品圖示的唯一入口），
  所以血統的血瓶＋圖騰＋星星、珠子的能力符號全部自動正確，這裡一行疊圖邏輯都不用寫。
  素材本身是 500×500，畫成 220 是原圖在縮、不會糊。<br>
  **版面上的一個坑**：`VerticalLayoutGroup` 會把直接子物件的寬度撐滿（`childForceExpandWidth`），
  把 Image 直接丟進去會被拉成 424×220 的長方形。作法是放一個**只負責占高度的容器**
  （`LayoutElement.preferredHeight`），真正的圖當它的子物件、用固定 `sizeDelta` 置中。<br>
  **順手修掉的既有問題**：`PositionTooltip` 原本只處理左右翻面、**完全沒有上下夾制**，
  在畫面下半 hover 時 tooltip 會往下掉出螢幕——本來就有，只是文字短的時候看不太出來，加了 220 的圖之後必現。
  改成 pivot 的 y 固定在上緣、用位置把頂端夾在面板內；**刻意不翻 pivot**（翻 pivot 會讓 tooltip 在游標上下跳）。
  另外 `ShowTooltip` 要先 `LayoutRebuilder.ForceRebuildLayoutImmediate` 再定位，
  否則 `ContentSizeFitter` 還沒重算、第一幀會拿上一件的高度去夾制，位置閃一下。<br>
  **通則**：**新功能讓一個既有的偷懶處現形時，順手把它修對**——這個夾制不是新需求，是本來就該有的。<br>
  `TipPreviewAllItems` 常數：現在所有物品都畫預覽（一致性），改 false 就只有血統藥劑畫。<br>
  **同日第二版（作者看過模擬圖後）**：改成**一律開在游標上方**（pivot 的 y 固定在下緣、頂端超出上緣時整個往下壓，
  不翻方向），圖與字一起放大——寬 460→520、預覽 220→280、字級 26/22/20 → 30/26/24，
  並把這些尺寸全部抽成常數集中在一處，之後要再調只動那一區。

* [x] **血統藥劑改回「一個系列一張成品瓶 ＋ 右下角階級角標」（畫法做成開關）**（2026-09-14，見 [BLOODLINE.md](BLOODLINE.md) §3）：
  9/13 做的是「共用血瓶＋白色系列圖騰」疊圖，作者看過實機想比較原本的成品瓶版本——
  成品瓶每個系列有自己的顏色、造型與生物，辨識度明顯高於「同一個紅瓶子換圖騰」。<br>
  **做成 `ItemIcons.BloodlineUseLogoOverlay` 一個常數切換**，兩條路徑都留著（目前 `false` ＝ 成品瓶），
  作者實機比完再刪掉沒選上的那條與對應素材。之所以切換成本是零：
  **兩種畫法的檔名都是用系列 `Key` 組出來的**（`bloodline_<Key>` vs `bloodline_Logo_<Key>`），
  查表邏輯 `TryBloodlineArt` 也共用，所以不必改 CSV、不必改素材、不必改呼叫端。<br>
  **角標（Ⅰ／Ⅱ／Ⅲ）兩種畫法都保留**，位置與大小常數不變（內容寬 32%、中心 (+34%, −31%)）。
  作者這期間把 `inventoryPanel_ItemLv1/2/3` 從五角星換成六邊形羅馬數字角標，
  因為**檔名沒變、而且縮放是以「不透明內容」算的**，程式一行都不用動就跟上了。<br>
  **通則：把「選哪一個」做成一個布林常數，比先挑一個做完再改回來便宜太多**——
  前提是兩條路徑共用同一套查表與擺放算式，否則開關會變成兩份要各自維護的程式。<br>
  ⏳ 選定之後的收尾：刪掉開關與沒選上的路徑、刪對應素材、把 ItemTable 那 21 列的 `IconPath`
  改回各自的成品瓶（目前填的是 `bloodline_BaseBottle`，程式不讀）。

* [x] **物品 tooltip 抽成共用元件 `ItemTooltip`：背包／倉庫／鍛造從此同一份**（2026-09-14，見 [UI_SYSTEM.md](UI_SYSTEM.md)、[INVENTORY.md](INVENTORY.md)、[STORAGE.md](STORAGE.md)）：
  作者要求「按 K 的倉庫格子，tooltip 也要跟背包一樣」。**這不是加功能，是還債**——
  三個面板（`InventoryPanel`／`StoragePanel`／`ForgingPanel`）各抄了一份幾乎一樣的 tooltip，
  所以當天稍早加的大圖預覽、字級放大、「一律開在游標上方」全部只落在背包，另外兩個停在舊樣子。
  [TODO.md](TODO.md) 與 UI_SYSTEM 早就記著「（可選）抽成共用元件」，這次直接做掉。<br>
  **做法**：新增 `Scripts/UI/ItemTooltip.cs`（純 C# 類別、不是 MonoBehaviour），
  版面／字級／定位規則／鑲嵌文案全部收進去，面板端只剩三行——
  `Create(transform)`、hover 進出 `Show/Hide`、`Update` 裡 `Follow()`。
  三個面板各自刪掉約 40~60 行重複程式。<br>
  **兩個面板差異怎麼處理**（抽共用最容易卡住的地方）：<br>
  ① 鍛造有一段自己的文案（「這顆珠子對這把武器無效」「鐵砧上那件鑲了無效的珠子」）——
  不塞進共用元件，改成 `Show(st, extraStats)` 的第二個參數由面板算好帶進來，接在 `TipStats` 後面。
  **通則：共用元件負責版面，面板負責只有它知道的內容。**<br>
  ② 倉庫原本只傳 `itemId`，所以鑲嵌內容與珠子等級顯示不出來；它的 hover 本來就拿得到整個 `ItemStack`，
  順手改成傳 stack ⇒ **倉庫的 tooltip 這次不只樣式一致，資訊也補齊了**。<br>
  **通則：同一個東西在三個地方各抄一份，遲早會分歧成三個樣子**——
  「維持一致」如果要靠人記得改三處，那就是還沒做完；抽成一份之後一致性才是結構保證的。<br>
  ⚠ **當天踩的坑（已記 [PROBLEMS.md](PROBLEMS.md) I10）**：刪 `StoragePanel` 那段欄位時，
  **把夾在中間、與 tooltip 無關的 `RectTransform _highlight` 一起刪掉了**，作者一開 Unity 就是 8 個 `CS0103`。
  這個環境沒有 C# 編譯器，而**括號平衡與關鍵字殘留檢查都抓不到「被刪掉的東西」**。
  補了一道「用到但沒宣告」的正規表示式檢查（收集所有 `_xxx` 的使用與宣告相減），
  之後改完 C# 一律先跑它再回報。<br>
  ⚠ **實機發現的第二件事（已記 [PROBLEMS.md](PROBLEMS.md) E34）**：背包與倉庫**並排同開**時，
  背包的 tooltip 被倉庫面板整片壓住——因為 tooltip 掛在自己面板底下，而兩個面板是 Window 層的兄弟節點，
  `SetAsLastSibling()` 只能排到自己面板內部的最後、跨不出去。改成掛 **`UILayer.Popup` 層**
  （`UILayer` 的註解本來就寫著那一層是給「確認框、提示(tooltip)」的，設計早就預留了、只是沒人用上），
  為此在 `UIManager` 加了 `LayerRoot(UILayer)`。⚠ 換層之後 tooltip 不再跟著面板隱藏，
  所以三個面板的 `OnClose` 都必須呼叫 `Hide()`（本來就有，但改掛載位置時要回頭確認一遍）。<br>
  **通則：浮在面板之上的東西（tooltip、拖曳圖示、飄字）不要掛在面板底下**——
  同層的兄弟面板會整片蓋過你的子物件，`SetAsLastSibling` 救不了。

* [x] **倉庫版面重做：格子對齊底圖、大小比照背包（10×10 → 5×5）**（2026-09-14，見 [STORAGE.md](STORAGE.md)〈格子與頁籤的版面〉、[PROBLEMS.md](PROBLEMS.md) **D25**）：
  作者實機截圖：倉庫的頁籤與格子整片往右下溢出到外框之外，而且格子比背包小一大截。<br>
  **根因不是偏移，是那組座標從來沒對過底圖**：`GridX0=167`／`CellW=84.8`／10×10／頁籤 x 寫死五個數字，
  而底圖掃出來的格線是 **10 欄 × 9 列**、格線區 x 196~921 / y 408~1096（節距 72.6×76.5）。
  **通則：「實機看若有偏移再微調常數」這種註解，八成代表那組值當初是憑印象填的**——
  與其微調，不如把底圖掃一次把真值量出來。<br>
  **做法**：沿用背包 4×3 那一套——`StoragePanel_GridBlank`（740×702，從 90 個格內乾淨區拼貼）蓋掉底圖畫死的格線，
  `StoragePanel_CellFrame`（73×77，含半條線的格單元）依欄列數平鋪；座標改成由
  `CellPitch` / `CellTopLeft` 算，**欄列數改 `StorageSystem.DefaultCols/Rows` 就換版面**。
  頁籤也不再寫死 x，改成對齊每一欄的中心（5 欄剛好 5 頁）。<br>
  **為什麼選 5×5**：目標是「格子與背包一樣大」。背包一格螢幕約 89 單位；倉庫 `FrameScale=0.72`，
  算下來 5×4 是 93（+4%，但上下各空一條）、**5×5 是 88（−1%，且剛好填滿格線區）**。容量每頁 100 → 25（五頁 125）。<br>
  **踩到一個存檔的坑（D25）**：`ItemGridData.RestoreFrom` 會**用存檔裡的 `cols/rows` 覆寫程式的設定**，
  所以改小格數之後舊存檔一載入就變回 10×10——UI 只畫得出 25 格、資料卻有 100 格，後面 75 格拿不出來。
  拿掉那一行（格數一律以程式為準）。**通則：版面/容量這種由程式與美術決定的東西不要存進存檔**，
  否則它會在讀檔時反過來覆蓋程式，症狀還是「改了沒反應」這種最難查的形式。
  ⚠ 縮容量時舊存檔塞不下的物品會消失，已寫進 STORAGE.md。

* [x] **加入第八個血統系列「蟲族 Swarmborn」：寄生體 → 獵殺者 → 蟲皇**（2026-09-14，見 [BLOODLINE.md](BLOODLINE.md) §7）：
  純資料新增，**程式一行沒動**——這正是 §7 那份清單想保證的事。<br>
  **動到的四張表**：表A `BloodlineSeriesTable` 加 `8,Swarmborn,蟲族,80,81,82`；
  表B `BloodlineTable` 加 80／81／82 三列（`SpriteFolder` = `Swarmborn/Parasite`／`Swarmborn/Ravager`／`Swarmborn/Swarm Emperor`，
  依慣例只填到 `Note`、神格特效五欄留空）；`ItemTable` 加三瓶 **308／328／338**（`<階>階蟲族藥劑`，
  `BloodlineID` 指 80／81／82，插在 337 之後維持血統藥劑分群）；`BaseBloodRoll` 加 308、權重 10 比照其他七瓶。
  最後把 30 筆素材（12 個動作資料夾 ＋ 18 張立繪）收進 catalog。<br>
  ⚠ **同步踩了一個坑並已修（[PROBLEMS.md](PROBLEMS.md) C13）**：這次在終端機跑了 `Tools/sync_map_assets.sh` 而不是 Unity 的 `Project Tools → Sync Map Assets`。bash 版是 C# 版的**應急子集**——不收 `Environment/` 底下的動畫資料夾、而且完全不算 `footprint`，結果 catalog 一次掉了 8 筆會動的地上物（佛像／信徒／傳送門／兩支火把…）與 **178 筆地上物的佔地格**。實體 PNG 沒少，所以只要還原 catalog.json 再把蟲族 30 筆照舊格式併回去就修好了（最終 diff ＝純新增 526 行）。**下次仍請在 Unity 裡跑一次官方 Sync 當正規化**。<br>
  **icon 零工作**：`ItemIcons` 是拿 `BloodlineID` 反查表A 的 `Key` 去組檔名的，
  作者已經把 `bloodline_Swarmborn.png` 放進 `positions/bloodline/`，三瓶自動變成「成品瓶＋Ⅰ/Ⅱ/Ⅲ 角標」。
  **這就是 §3 那段「加新系列不必改任何程式」的實際驗收**。<br>
  **第三階資料夾名含空白**（`Swarm Emperor`，同血族的 `Crimson Count`／靈根的 `Nascent Soul`）——照資料夾實際名稱填，沒有去掉。<br>
  ⚠ **留了一個素材缺口：蟲皇的 `dead/` 只有一張**（其餘兩階各 25 張）。dead 是一圖三用（死亡＋變身的倒下/爬起），
  所以蟲皇變身時會瞬間倒地、瞬間爬起。已記 [TODO.md](TODO.md)。<br>
  ⚠ `BodyScale` 全填 1、`WalkSpeed` 留空、五屬性是佔位值，**六個數字都還沒實機看過**；
  蟲皇的神格特效（§5b 五欄）刻意留空，等作者決定掛哪一層——「一個血統只掛一層」的額度要規劃著用。

* [x] **Split Sprite Sheets 的格線推測改用「跨線率」，修好「正確的合圖被判成切錯」**（2026-09-14，見 [PROBLEMS.md](PROBLEMS.md) **C14**）：
  症狀是蟲皇 `dead` 那張 1280×1280 三個選單都拒切，同角色其他三個動作都正常。<br>
  **根因是防呆對準了代理指標**：初版判「內容有沒有**碰到**格線」（貼邊率），前提是「AutoSprite 每格四周都留白」。
  倒下展翅那 5 幀翅膀張到 212~256px（格子就是 256），貼邊率 20% 超過 10% 的門檻——
  但實測正解格線上只有 **2 個像素**同時不透明、alpha 僅 2 與 5（抗鋸齒羽化），**根本沒切到角色**。
  貼邊不等於切穿，真正要防的是切穿。<br>
  **做法**：`GuessGrid` 的判準換成 `CrossRatio()`＝內部格線兩側同時是實體像素（alpha ≥ 16，濾掉羽化）的點數 ÷ 內部格線總長度，
  門檻 2%；**外框不算**（貼著圖的外緣是正常的）。分離度反而更寬：正解 **0.00%** vs 最近的錯誤答案 **19%**（舊版是 20% vs 31%）。<br>
  **維持「不假設排版與尺寸」這條原則**——AutoSprite 的排版會是 5×5／5×3／4×3／5×2，素材還會被重繪工具放大 2~4 倍，
  所以判準裡沒有任何一個數字是格數或圖片尺寸，只靠「格子是正方形」與「線不該切穿角色」兩個真正不變的前提。<br>
  ⚠ **新版唯一的風險點已知並已處理**：`cell` 剛好是正解的倍數時也會零跨線（實測 4×6 合圖的 2×3 排版同為 0.00%，
  因為它的格線是正解的子集），靠**「同分取較小的 cell」**排除——迴圈順序與嚴格 `<` 是這條的實作，改動它會以
  「把 4 幀併成一張切出來」這種不報錯的形式壞掉。另加一道「有內容的格子 ≥ 2」擋掉單張圖被切成 4 格的情形。<br>
  **回歸驗證**：把專案裡 **108 個已切好的動作資料夾全部拼回合圖 → 0 誤判**；改拼成 4／3／2 欄都切回原排版；
  放大 2×／3× 一樣切對（cell 自動變 512／768）；四個非合圖樣本（單張立繪／已切幀／立繪）全部擋下。
  ⚠ **這個環境沒有 C# 編譯器**（同 I10），所以驗證是用 Python 重現同一套算式跑的，程式本身還沒進 Unity 編過——
  下次開 Unity 時請實際對蟲皇 dead 跑一次 `Project Tools → Split Sprite Sheet` 當最終驗收。

* [x] **血統特效第六層 `BloodlineHitFx`：三階血統的擊中特效凌駕一般武器（蟲皇＝咬擊）**（2026-09-14，見 [BLOODLINE.md](BLOODLINE.md) **§5c**）：
  作者拍板的**通則**，不是蟲皇特例——以後每個三階血統只要在表B 填 `HitVfxId`，
  打到怪物時就蓋掉該武器原本的 `HitEffectID`，而且播在**被打的那隻怪身上**、大小跟著牠走。<br>
  **與第五層 `AttackVfxId` 的差別是「在誰身上」**：第五層出手就播、罩自己；第六層打中才播、播對方。
  兩層都不佔「一個血統只掛一層」的額度（平常看不到）。<br>
  **通知點選在 `CombatSystem.Apply` 的結尾**——那是全遊戲傷害的單一入口，子彈／雷射 tick／連鎖閃電／
  AOE 爆炸四條路一條都不會漏，**之後新增武器模式也不必回來補**。
  對照第五層接的是「玩家有沒有出手」，而出手在這個專案是兩種東西（離散事件 vs 持續狀態），
  得接兩條路、第一版還漏了一半（PROBLEMS **F21**）；第六層接的「有沒有造成傷害」只有一個入口，天生沒這問題。
  `CombatSystem` 只多一行，四道判斷（來源是玩家／目標是怪／有沒有掛／該怪冷卻）全在 `NotifyHit` 裡。<br>
  ⚠ **讓位有邊界：打到怪才讓，打到牆不讓。** `TrySpawnHitEffect` 多收一個 `hitEnemy`；
  雷射與子彈用現成的 `hitEnemy`、連鎖閃電用 `IsOnEnemyLayer`（連鎖目標可能是可破壞地上物，不能寫死 true）、
  分段雷柱的地面爆炸／拋物線落地／定點法陣一律 false（那是落點視覺不是命中回饋）。
  近戰扇形與範圍施放那兩處刻意不動——它們在傷害**之前**播、播的是揮砍動畫。<br>
  **節流是「每隻怪各自計時」**（`HitFxGap`，預設 0.35 秒）：雷射每個 DOT tick 都命中同一隻怪，
  不節流會狂閃；但全域計時又會讓「AOE 打中五隻」只有一隻看得到。分怪計時同時解掉兩件事。<br>
  素材：`fx3_bite` 的 **green 原色版** 17 幀 64×64 @15fps（Super Pixel Effects Pack 3），
  複製進 `Resources/VfxEffects/BloodlineHitFx/SwarmEmperor/` 時**檔名從三位數 `_001` 改成兩位 `_01`**
  （`VfxManager` 只認兩位補零，不改一張都載不到）；`VfxTable` 加 id 40；表B 加 `HitVfxId`／`HitFxGap` 兩欄（23~24）。<br>
  ⚠ 同 C14 那則：**這個環境沒有 C# 編譯器**，語法只做了括號平衡與宣告/使用比對，**還沒進 Unity 編過**。

* [x] **背包暫時放大到各 10 頁（除錯用，之後要還原）**（2026-09-14，見 [INVENTORY.md](INVENTORY.md)〈增減容量安全嗎〉）：
  `EquipBagCount` / `ItemBagCount` 由 `PageSlots * 2`（各 24 格、兩頁）改成 `PageSlots * 10`（各 120 格、10 頁），
  `GridCount` 48 → 240。**只有這兩行**——面板、存檔、教學一行都不用動，因為頁數是 `PagesOf(bag)` 算出來的、
  格子只建一頁重複使用，專案裡沒有任何寫死的頁數。<br>
  ⚠ **這是暫時值，常態設定是 `* 2`**；常數旁與 INVENTORY.md 都標了還原方式。<br>
  **順手把「增減容量安全嗎」寫成文件**（作者提到之後會做背包擴充道具、容量會隨時增減）：
  **放大完全安全**——容量不存進存檔，`RestoreState` 一律以程式常數為準；
  **縮小只有一種情況會掉東西**——舊存檔某一包的件數超過新容量時，`AddStack` 塞不下。
  ⚠ 特別記下**背包與倉庫的差異**：倉庫的 `ItemGridData.RestoreFrom` 會用存檔的 `cols/rows` 覆寫程式設定（D25），
  背包沒有那條路。<br>
  ⚠ 也記下**未來的擋路點**：`const` 是編譯期的值，要做「背包擴充道具」得改成 `static int` ＋ 變動時重建 `_grid`；
  `GridCount` 是存檔格號的值域，縮小時要保留 `AddStack` 那條保命路徑。

* [x] **蟲皇的影子錨點手改（idle／walk）＋ 一個新的量法寫進 SHADOW.md**（2026-09-14，見 [SHADOW.md](SHADOW.md)〈5c〉）：
  作者實機回報影子偏掉。**沒動演算法**（已定版，PROBLEMS **E28**），照〈手動調整影子〉改表。<br>
  **一眼認出是離群值**：蟲皇 idle `-25.5`／walk `-25.1`，而同批的寄生體 `-3.8`、獵殺者 `+1.5`，全表其他角色都在 ±8 內
  ——第八次踩到「垂到腳邊的東西被當成第二隻腳」（前七次：旱魃、毛殭、泰坦、尼德霍格、法夫納、螭吻、應龍）。<br>
  ⚠ **但這次三個既有量法同時失效**：②身體重心 −21.6、③接地層 −8.5、腿部帶 −25.5 ——三個方法三個答案，
  因為那片翅膀**從肩膀一路垂到腳邊**，每一帶都被它佔到（法夫納當初也是量不出來，最後只能靠實機回饋硬調）。<br>
  **改用「拆最底帶的段結構」**：把距底 0/2/4/8/12/…px 每一層的不透明段（x 範圍、寬、中心）印出來，
  真相立刻浮現——距底 0~8px 是**三段細腳**（中心 −22 / −8 / +5），而翅膀要到距底 **40px** 才出現。
  ⇒ 接地範圍 −23~+6、整體中心 **−8.5**；auto 的 −25.5 是因為演算法「取最低的兩段」（−22 與 −8），
  **第三個接地點 (+5) 稍高被漏掉**，中點就被拉到左邊。<br>
  **通則已寫進 SHADOW.md**：**有三個以上接地點時，填「所有接地段的整體中心」而不是最低兩段的中點**；
  這個拆段法只要幾行程式，比試數字可靠得多，之後遇到量不出來的角色先用它。<br>
  walk 對齊 idle 的 −8.5（同尼德霍格的做法），換動作時影子不會跳；兩列都標 `manual`、Note 寫明理由。

* [x] **通用「偵測條件」：依血統／道具決定 NPC 出不出現、講哪一句（⏳ 未編譯未實測）**（2026-09-15，見 [TRIGGER_CHAIN.md §2.6](TRIGGER_CHAIN.md)＋[NPC_SYSTEM.md](NPC_SYSTEM.md)）：
  作者要「血族玩家對血族 NPC 說『是同族啊』、其他人說『外來者不要靠近』」，以及
  「血狂之爭門口依血統換不同 NPC 出現」。做成**一套通用條件**而不是 NPC 專屬功能。<br>
  **為什麼能這麼省**：專案早就有三套各自寫死的條件機制（`TriggerChain.RequirementMet` 的六個條件欄、
  地上物的 `appearAfterClears`/`appearFlag`/`disappearFlag`、NPC 的 `disappearFlag`），
  這次只是多開一條**共用**的：新檔 `Scripts/Map/AppearCondition.cs` 一支求值器 ＋ 編輯器一個 UI 元件，
  **四個使用端同時受益**——觸發點（怪物出生點免費附贈，因為它本來就走 `RequirementMet`）、
  NPC 出現與否、NPC 講哪一句、地上物出現與否。既有條件欄**一個都沒動**，純加法，舊地圖缺欄＝空＝永遠成立。<br>
  **格式決定：存成一個字串** `series:2|!item:104`（`!`＝沒有、`|`＝AND）。
  因為 `TriggerRegion` 的參數是 `Dictionary<string,object>` 塞不進巢狀結構，用字串四個地方才能共用同一個 parser；
  作者在編輯器看不到字串，只看到 `[偵測種類▼][id][選][有/沒有][−]` 的清單。<br>
  ⚠ **「血族」是系列不是血統**——血族＝`SeriesId 2`（覓血者 20／血伯爵 21／該隱 22）。
  偵測種類因此做了**兩種**：「血統系列」（三階都算，日常用這個）與「血統」（只認一階，寫該隱限定台詞用）。
  只做後者的話，玩家喝進階藥劑變成血伯爵，血族 NPC 就不認得他了。<br>
  ⚠ **判定時機刻意不一致，這是設計不是 bug**：出現與否**只在進圖生成當下問一次**（作者拍板：
  中途變身不要在眼前憑空換人，換下一張圖才反應）；講哪一句是**按 F 當下**問（沒有視覺變化不會穿幫）。<br>
  **刻意不做 OR**：一加上去清單就得長出括號與優先序、要在 IMGUI 上排邏輯樹。
  「血族或狂族」用「多擺一隻各填一條」就能表達。<br>
  **未知種類一律當成立、不擋人**＋Console 印一次——「東西莫名其妙不見了」比「東西多出現」難查太多。<br>
  ⚠ 無存檔測試（DevQuickStart／編輯器直測）時血統**照實＝人類**，所以血統條件一律不成立；
  不做「沒存檔就放行」的特例，否則門口那三隻會全部一起冒出來。要測請用測試選單進關。<br>
  編輯器條件列的「選」清單直讀主專案 `BloodlineSeriesTable.csv`／`BloodlineTable.csv`／`ItemTable.csv`
  （`Preview/ConditionRefTables.cs`，同 `NpcTableEditor` 的直讀磁碟作法）。
  `NpcInstance` 兩專案鏡像已同步加欄（`conditions`＋`conditionalDramas`），`ObjectInstance` 加 `conditions`。<br>
  **未驗證**：全部只過人工檢查（括號平衡、命名空間、鏡像欄位），**Unity 還沒編譯過、也還沒實機擺過**。

* [x] **NPC 三修：大小兩邊不一致、狂族士兵永遠在走路、補「初始朝向」（⏳ 未編譯未實測）**（2026-09-15，見 [NPC_SYSTEM.md](NPC_SYSTEM.md)＋PROBLEMS **C15**／**G10**）：
  作者擺了血族士兵／狂族士兵之後回報三件事，根因各自獨立。<br>
  **① 編輯器小、遊戲巨人**：兩邊算大小的方式**從來就不同**——遊戲端把 idle 的**可見像素高度**正規化到
  `CharacterWorldHeight` 1.95 世界高（與畫布大小無關），編輯器端是 `PPU = 256/tileSize`（＝角色在畫布裡佔多少就多大）。
  示範村民 ZhaYu 畫布 500px、可見 451px → 編輯器 1.76 格，**湊巧接近 1.95，所以這個落差被蓋住到今天**；
  iso 去背的兩隻士兵畫布 256、可見只有 135／193px → 編輯器 0.53／0.75 格，作者只好把 `Scale` 填 3，
  遊戲就變 1.95×3＝**5.85 格高**。修法：`PreviewSpriteLoader.Load` 加 `normalizeHeight` 參數，
  `NpcView` 傳 1.95 走與遊戲相同的公式；`NpcTable` 兩列 `Scale` 回填 1。
  **劇情演出預覽刻意不動**（預設 `normalizeHeight=0`＝舊行為）——那邊的演員走位是照舊基準排的，改了會全部跑掉。<br>
  **② 狂族士兵永遠是 walk**：`WerewolfSoldier/idle/` 是**空資料夾**，17 張圖全在拼錯的 `idlle/`。
  遊戲端與編輯器**都有「idle 取不到退回 walk」的 fallback**，所以一路不報錯。圖已搬回 `idle/`、`idlle/` 已刪
  （⚠ **要跑一次 `Sync Map Assets`**；本次已順手把 StreamingAssets 那份也搬正，不跑也能先測）。<br>
  **③ 補初始朝向**：`NpcInstance` 兩專案鏡像加 `faceLeft`（bool，預設 false＝向右，舊地圖缺欄＝維持原行為），
  編輯器面板「行為」下方加`向右／向左`兩顆按鈕、預覽即時翻面，`NpcSpawner` 生成當下設一次 `sr.flipX`。
  **只要設一次就夠**：NPC 是 Neutral＋`DetectionRange=0` → `HandleVisuals` 的 faceTarget 恆為 null，
  「面向玩家」那條永遠不會覆寫它；巡邏 NPC 一起步才由 `FaceMovement` 接手。<br>
  **順手修**：編輯器「複製一個」`NpcController.DuplicateSelected` 漏複製昨天新加的 `conditions` 與
  `conditionalDramas`（複製出來的 NPC 會掉條件），已補上並對條件對話做深拷貝。<br>
  **未驗證**：人工檢查（鏡像欄位、括號、公式與遊戲端逐項對算）為主，**Unity 還沒編譯過、也還沒實機看過大小與朝向**。

* [x] **診斷「火焰噴射器的火被地毯擋住」＋ 地圖編輯器「可走／可穿越」兩個勾加面板說明**（2026-09-16，見 PROBLEMS **E35**）：
  作者回報火焰噴射器朝地毯噴，火團在地毯邊界被一刀切齊。**不是碰撞，是排序**：火焰噴射器
  （WeaponTable **ID 7** → RecipeID **20**(Laser)＋**TrailEffectID 4**）畫面上的火是沿路種的 VfxTable
  **ID 4「火球」，`SortingOrder` 寫死 8**（地面特效帶，刻意壓在角色腳下）；而那張地毯
  （`RedBridalGown_BridalRoom` 的 `carpet_phoenix_xi`）勾的是**「可穿越」而不是「可走」**，
  `passThrough` 只免掉碰撞、**排序照常走 Y 排序帶** ⇒ `MapDepthSort.Order(-10.69, -1)` = **2069 ≫ 8**。<br>
  **排除碰撞的兩個證據**：`walkable || passThrough` 兩者都跳過 `BuildObjectCollision`（地毯身上零 collider）；
  該圖可走層在地毯那一帶全是 `0`。**`zOrder` 救不了**——世界帶最低是 1000，永遠高於腳下特效帶。<br>
  **資料面未改**（作者選擇自己在編輯器裡改：取消「可穿越」、改勾「可走」→ 存檔 → `Sync Map Assets`）。
  全專案掃過，`passThrough=true` 的 24 筆擺放裡只有這張地毯勾錯，其餘都是榕樹妖場那批站立的 `ghost_*`。<br>
  **這次真正動的是編輯器 UI**：作者說「這兩個功能不常用很容易忘」，所以把差別**寫在面板上**而不是只留在文件裡——
  `EditorUI.DrawObjectInspector` 兩個 Toggle 的標籤補上用途舉例（可走＝地毯／木板／蒲團；可穿越＝鬼魂／煙／光），
  底下各加一行灰色說明小字（新 helper `EditorUI.FlagHint`，`richText`＋`wordWrap`＋左縮排）：
  可走＝無碰撞＋固定畫在角色腳下、**玩家走不走得過去仍看可走層**；可穿越＝無碰撞但照常依 Y 前後遮蔽、
  **鋪在地上的別勾**。⏳ **未編譯**（純 IMGUI 顯示，不動任何資料或存檔格式）。

* [x] **紅嫁衣大絕「家人齊聚」＋ pant 喘息破綻（⏳ 未編譯未實測）**（2026-09-16，見 [BOSS_MODULE.md](BOSS_MODULE.md) §2/§3＋[MONSTER_SETUP.md](MONSTER_SETUP.md)）：
  作者要「血量剩一半時一口氣把 MonsterData 2~12 的鬼魂**各叫一隻**出來，然後喘 10 秒給玩家打」。
  原本的逃跑＋定時召喚**一行沒動**，這是加在旁邊的第二招。<br>
  **四個缺口與各自的解法**：① 既有 `SummonSystem.Cast` 是「從池子隨機抽 `SummonCount` 隻」，抽 11 次會重複
  → RecipeTable 加一欄 **`SummonEachOnce`**（每個 ID 各一隻、不重複、**生成角度平均分開**；十幾隻各自隨機取角會
  擠成一團互相卡位，還看不出陣仗）＋新的 `FindSpawnPosNear`（先試分配到的角度，被牆擋才小幅偏擺、逐圈內縮，
  整個方向沒位置才退回原本的隨機找點）。② 平時那把的 `SummonMaxAlive=2` 會把大絕夾成只出 2 隻
  → 大絕走**另一把獨立的武器**（15→配方 28），因為 `MonsterWeaponUser` 是「一個元件＝一把武器＋一份分身名單」，
  分開掛才有獨立額度；Brain 在 `EnsureInit` 自己 `AddComponent` 第二個。③ 於是 `MonsterController.Die` 的
  `GetComponent<MonsterWeaponUser>()` **只收得到第一把** → 改 `GetComponents` 逐一 `RecallSummons`
  （否則 boss 死了那 11 隻還在場上追殺玩家，違反 §6.7 的通用原則）。④ 動畫沒有「喘」這個狀態
  → `MonsterAnimator` 加 `State.Pant`。<br>
  **加一個新動作比想像中便宜**：`MonsterSpriteLibrary.GetFrames(怪名, 動作)` 與 Sync 工具本來就是
  **通用字串／掃「直接含 PNG 的葉資料夾」**，載圖、同步、影子錨點三者**都不用改**；只動 `MonsterAnimator` 五處
  （列舉、幀陣列、Setup、`FramesFor`、`Resolve`）。而且沒有 pant 圖的怪**不會噴 log**（`GetFrames` 找不到只是
  靜靜回 null 並快取），所以不會汙染 Console。`Resolve` 讓 pant 退回 **idle 而不是 walk**——喘息本來就站著不動，
  退成走路會變回原地踏步那個老 bug。<br>
  **⚠ 踩過一次才改對的地方**：pant 的起播原本寫成「Brain 每幀檢查『出手 0.6 秒到了沒』」，
  但**被打時的擊退窗口會整段跳過 `Think()`**（PROBLEMS **F19**）——玩家猛打時 pant 會延後、甚至整段不播。
  改成 `MonsterController.PlayPant(秒, 延遲)` **呼叫一次就把起訖排好**，動畫每幀自己判讀，與 Brain 有沒有被跳過無關。
  同理 pant 的動畫優先度壓過 attack，所以**一定要延後** `SkillCastAnimSeconds`，否則出手動作會被當場蓋掉。<br>
  **拍板的設計**：只放一次（`_ultUsed`，喘完不再放）；喘息期間**完全停擺**（不逃不召——那段 `return` 刻意擺在
  平時召喚之前，否則她會一邊喘一邊繼續召 2 隻，破綻就不成立）；11 隻**不佔**平時的同時上限。<br>
  **未驗證**：人工檢查（欄位對照、括號平衡、CSV 欄數 50→51 全列一致）為主，**Unity 還沒編譯過**。
  ⚠ 作者要跑 `Sync Map Assets` 把 `RedBridalGown/pant/`（50 張，目前拿 idle 頂替、真圖製作中）帶進 StreamingAssets；
  真圖到位後要再跑一次「計算影子錨點」。<br>
  **▸ 同日實機回饋兩修**：① **鬼魂被召到牆外**（新娘房可走區小、一次 11 隻就滿場踩雷）——生怪點的「避開牆」
  用的是 `Physics2D.OverlapCircle`，但全域 `queriesStartInColliders=false` 會讓**起點落在 collider 內部的那個
  collider 被整個略過** ⇒ 埋進牆裡反而通過檢查（新記 PROBLEMS **B16**；同設定的另一種症狀是 B7）。改成一律問
  **`MapNavGrid.IsWalkableWorld`**（可走層 ＋ 物理碰撞 ＋ 怪身淨空已聯集好的同一份真相，A* 走的也是它），
  抽成 `IsSpawnSpotOk` 給隨機召喚與定角召喚共用；順便把定角搜尋從「只往內縮」改成**先往外找、再往內擠**
  （作者：放遠一點沒關係，出界才不行），最後的退路也從「生在 boss 腳下」改成先試整張可走區的隨機點。
  ② **喘氣太急**——pant 照 CSV `AnimFPS`(25) 播，50 張兩秒一輪。加 `MonsterAnimator.PantFpsMul` 讓它獨立放慢
  （刻意不跟走路那條共用速度連動：她喘的時候站著不動，連動會被壓到 `MinMul` 變成另一個數字）；
  作者試過 0.5 仍嫌快，**現值 0.25**（＝6.25fps、8 秒一輪）。⚠ CSV 的 `AnimFPS` 是 idle/walk/attack/pant **共用**的，
  所以喘息的快慢只能另開倍率、不能改它——作者第一時間就是去 CSV 找而找不到，Tooltip 已補上算式與對照表。
  ③ **首尾接不起來的跳接**（作者：「喘三口氣後很明顯會出現破綻」）——加 `PantPingPong`(true)＝**乒乓來回播**
  （0→N→0→…，兩端不重播同一幀）。喘氣是吸↔吐的往復，倒著播在語意上本來就成立，接縫因此消失，
  也不必為了對接去修素材。做成可關的開關：素材若哪天是**不對稱**動作（身體逐漸下沉那種），倒放會像倒帶。
  附帶效果：乒乓一趟來回 98 幀 ＝ 15.7 秒 > pant 的 10 秒 ⇒ **她永遠走不完一趟，也就永遠看不到任何接縫**。

* [x] **血量門檻台詞改成「跌破就一定喊」——boss 放大絕時喊話的做法（⏳ 未編譯未實測）**（2026-09-16，見 [MONSTER_SPEECH.md](MONSTER_SPEECH.md)）：
  作者在紅嫁衣句子4 填了 `50%: 家人們，一起出來吧`，本意是「放大絕時喊這句」，實測卻沒喊。<br>
  **原因**：`N%:` 前綴原本只是**解鎖**——跌破門檻後那句才被放進隨機池，之後每隔 `SpeakIntervalSeconds`(boss 5 秒)
  擲一次 `BossSpeakChance`(0.9) 再從池子裡**隨機挑一句**。所以跌破那一刻不但不保證喊，還要跟另外三句搶。
  劇情節點用隨機池表達不出來。<br>
  **改法**：`MonsterSpeech` 多一條**強制播報**路徑（`TryAnnounceThreshold`），每幀檢查「還沒喊過 ＋ 血量已跌破」
  的門檻句，有就當場講——**不看間隔、不擲機率、也不要求 `IsAwareOfPlayer`**（會掉血就表示玩家早就動手了），
  刻意擺在「發現玩家」那一關之前。喊過的句子仍留在隨機池裡，舊行為不變。<br>
  **兩個邊界**：① 一發大傷害同時跨過好幾個門檻（滿血→5% 同時跨 30% 與 10%）→ **只喊門檻最低的那句**
  （劇情上最後面的那句），其餘一併記成已播報，否則下一幀接著喊會變連珠炮。② 喊完把隨機那條的下一次時間
  往後推一個完整間隔，免得緊接著又冒一句把劇情台詞蓋掉。<br>
  **順帶確立一個做法**：boss「放招時喊話」**不必把台詞寫死在 Brain 裡**——把台詞的 `N%` 填成和招式的血量門檻
  同一個數字（紅嫁衣兩邊都是 50），兩者讀同一個血量就自然同時發生。已寫進 BOSS_MODULE §2 給下一隻 boss 抄。

* [x] **卍字進場：主角現在是被卍字送進場的（⏳ 未編譯未實測）**（2026-09-17，見 [SCENE_TIP.md](SCENE_TIP.md) §0）：
  過關/死亡那支「卍字離場」現在有了對稱的另一半——**進場鏡頭是它的倒放**：卍字從天而降（紫、小、淡入、
  起步快落地慢）→ 落地放大轉金、把主角從 2% 縮放吐出來 → 原地淡出，接著才跳場景名、名字收掉才開打。
  全程暫停遊戲、`unscaled` 時間，程式 `Flow/LevelEnterManjiController.cs`（約 2.2 秒；刻意比離場的 2.65 短，
  後面還接著 1.73 秒的場景說明）。<br>
  **這次最關鍵的決定是「哪些圖要播」不開新開關**：直接與場景說明共用 `MapManager._shownSceneTips`
  （只 `Contains` 不 `Add`，名額留給場景說明去 Add）。一行判定就同時得到作者要的四件事——
  廣場與劇本入口都播、劇本內房間互跳不重播、**開場那三張（初始森林 1/2、初始洞窟）自動排除**
  （它們的 `SceneTip` 本來就刻意留空）、以及「去重規則全專案只有一份、不會漂移」。
  代價寫在 SCENE_TIP §0.1：`SceneTip` 空的地圖（目前 `BloodFang_*`、`Future_*` 整組）兩樣都沒有——
  這是刻意的耦合，真要拆才加 MapsTable 欄位。<br>
  **接線位置也是刻意的**：掛在 `FireEnterTriggersRoutine` 的**第一個 `yield` 之前**。`StartCoroutine`
  會同步跑到第一個 `yield`，所以藏主角與 `LoadingPanel` 關閉是同一幀（晚一步就會看到主角閃一幀才被蓋掉）；
  而且「藏主角 → 開始播」之間不隔 `yield`，就沒有「中途換圖 `yield break` ⇒ 主角永遠隱身」的路徑。<br>
  **三個踩進去才知道的地方**：① 主角要走 `PlayerVisibility`（劇情 `hidePlayer` 同一支）而不是 `SetActive(false)`，
  它會連影子、碰撞、提燈光圈一起關；② 吐出主角那段**影子要先按著**——影子是獨立物件不跟著縮放，
  先放出來會變成「小主角配一團原尺寸的影子」（離場那支就有這個已知瑕疵，這次進場順手避開）；
  ③ 每幀增量**夾上限 0.05 秒**——本特效正好接在讀取頁關閉後的第一幀（整場最長的一幀），
  不夾的話 `unscaledDeltaTime` 一次吃掉大半段，卍字會「瞬移」才開始動。<br>
  **順手修掉一個既有的坑**（[PROBLEMS.md](PROBLEMS.md) **G11**）：`PlayerVisibility` 關不掉血統特效
  （頭光／光環／繞行／拖尾的視覺都是獨立 GameObject，`GetComponentsInChildren` 抓不到）→
  主角藏起來了、地上還浮著一圈沒有主人的光。五層全讀同一顆 `PlayerAnimator.BodyFxVisible`，
  在那裡加一條 `PlayerVisibility.IsHidden` 就全解決，**劇情 `hidePlayer` 的同一個症狀也一起好了**。<br>
  卍字圖與 `SortingOrder`(25000) 都與離場共用（`LevelExitManjiController.ManjiSprite` 開成 internal），
  兩支才不會長得不一樣；`PlayModeStaticReset` 補了 `LevelEnterManjiController.ResetForPlayMode()`
  （`IsPlaying` 殘留會讓等待鏈永遠卡住＝進圖後遊戲再也不開始）。

* [x] **家書（dramaId=30）改成可反覆閱讀＋修正一處過期文件**（2026-09-17，見 [PROBLEMS.md](PROBLEMS.md) **K3**）：
  作者回報「客廳1 的家書看完一次星星就不見了，書房的封靈符（22）卻能重複看，做法明明一樣」。
  **不是程式問題**：書房那顆的 `重複規則` 填了 `每次`，客廳這顆留空 ⇒ 預設 `關卡單次`，觸發後 `ConsumePoint`
  移除星星並寫進 `RunProgress.consumedTriggers`，**整趟關卡都不再出現**。
  已在 `RedBridalGown_LivingRoom1.dipanmap` 的該 trigger 補上 `"repeat": "每次"`
  （編輯器正本／GameAssets／StreamingAssets 三份一起改，不必先跑 Sync 也能測；不必清存檔，那層記錄不寫檔）。<br>
  **順帶修掉一個會誤導人的文件錯誤**：TRIGGER_CHAIN §2.5.1 把 `關卡單次` 寫成「離圖重進復活」——
  那是加 `RunProgress` 之前的舊行為，實際上同一趟關卡內離開房間再回來**不會**復活
  （INTERACTION.md〈一次性與記憶範圍〉一直是對的，兩份不同步）。已改對並互相指路。

* [x] **撲擊型戰鬥模組 `PounceBrain` ＋ 戰狼 Wolf Warrior（⏳ 未編譯未實測）**（2026-09-17，見 [BOSS_MODULE.md](BOSS_MODULE.md) §7）：
  狗／狼這類掠食者的節奏——**觀望（繞著玩家左右橫移）→ 蓄力 0.4 秒 → 鎖定方向直線撲擊（×3 速）→ 收招 0.7 秒**，
  五段狀態機，`BrainType=Pounce` 就能給任何獸型怪復用。第一隻使用者是 BloodFang 的**怪物 17「Wolf Warrior」戰狼**
  （素材 idle 23／walk(run) 17／attack(bite) 25 張，256×256，已在 catalog 與 StreamingAssets）。<br>
  **這次真正的工作是「先確認哪些東西不用寫」**：① 傷害不用寫——`EnemyContactDamage` 每 `AttackInterval` 秒扣一次
  `ContactDamage`，所以撲擊撞到與貼身咬**本來就是同一筆傷害**（作者拍板維持同傷、零改動共用元件）；
  ② attack 動畫不用寫——`MonsterController.HandleVisuals` 只要「距離 ≤ `AttackRange`(1.3) 且有 attack 幀」就自動演攻擊動作。
  於是整支 Brain 沒有一行傷害或動畫程式，只管「什麼時候站、什麼時候衝」。<br>
  **兩個既有的坑在設計階段就先避開**：① **衝刺綁距離不綁時間**（落點＝起跑時目標位置再延伸 1.6，`ChargeMaxSeconds` 只是保險）——
  綁時間的話 CSV `Speed` 一調小撲擊距離就縮水成「原地抽動」，同 **F20**；② **衝刺的收尾檢查放在 `Think()` 最前面**——
  擊退窗口會整段跳過 `Think()`（**F19**），放在 `Charge` 分支裡的話，被打斷後牠會帶著 3 倍速跑去做別的事。<br>
  **目標取 `ctx.Enemy` 與 `ctx.Player` 的近者**（同 `WarBrain`），所以同一支 Brain 對 `Enemy` 陣營與三方陣營都成立、
  不必寫陣營特例；戰狼的 `Faction` 填 `Werewolf`（作者拍板），和平期不打玩家、開戰後也會撲吸血鬼。<br>
  **實測前先知道的兩個限制**：衝刺時跑步動畫**不會變快**（`MonsterAnimator` 的 fps 連動上限寫死 1，要改得動共用元件、留給作者決定），
  以及 `InvincibleTimeMs` 目前填 0＝被連射時牠會站著不做決策（F19 的隱形行為預算）。

* [x] **三方陣營語意反轉：預設「敵對」，和平改成劇本要明確進入的特例（⏳ 未編譯未實測）**（2026-09-17，見 [FACTION.md](FACTION.md) §0）：
  作者把戰狼（`Faction=Werewolf`）放進競技場想測撲擊模組，牠站著不動、也不傷人。查下來不是 bug——
  `WarActive` 預設 false，部族怪要等劇本按下 `factionWar` 才會動；而和平期的兩族在原設計裡是**用 NPC 擺的**，
  戰鬥版怪物的出生點本來就該填「條件旗標＝部族開戰」，開戰才登場。作者現在的用法系統沒設想過。<br>
  **真正的病灶是 `Faction` 欄同時承載了兩件事**：「這隻怪是狼人族」（種族標籤）與「牠的敵意由一顆全域劇本開關決定」。
  作者拍板：**「放上去就照自己的模組戰鬥」才該是預設，「不戰鬥」才是劇本要求的特例。**<br>
  他一開始提的是「把 `WarActive` 預設改成 true」，但那行不通：① `ResetScenario()` 與 `ResetForPlayMode()` 都會把它設回 false，
  **撐不過第一次換圖**；② `StartWar()` 開頭是 `if (WarActive) return;`，預設 true 會讓 `factionWar` 變成空操作；
  ③ `Hostile(狼,吸)` 直接回傳它，預設 true 會讓血狂之爭一進場兩族就自己打起來。
  要做就得**把語意整個反轉**，不能只動一個初始值。<br>
  **做法**：`WarActive`（預設 false）→ **`PeaceActive`（預設 false）**，六處判定同步反轉；`StartWar()` → `EndPeace()`、
  新增 `StartPeace()`；新增鏈動作 **`factionPeace`「三方陣營和平」**（主遊戲 `TriggerChain` ＋ 編輯器 `TriggerType`）。
  `factionWar` 的 typeId 與顯示名**刻意不改**——對作者而言按下去的效果一樣（兩族開始互咬＋攻擊玩家），
  而且沒進過和平段的地圖按它也安全（本來就敵對，只是重套一次 Layer）。<br>
  **回歸安全性用真值表機械驗證**：把反轉前後的 `Hostile`／`AttacksPlayer`／`HasMonsterFoes`／`ApplyLayer` 各實作一份，
  窮舉五個陣營 × 三種結盟狀態 × 兩種劇本狀態比對——① 新 `peace` ≡ 舊 `war=!peace` 全部一致；
  ② **Enemy／PlayerAlly／Neutral 三個非部族陣營在預設狀態下逐條零變化**；③ 只有部族在預設狀態改變（正是本次要的）。
  目前填了部族的怪只有 17 戰狼一隻。<br>
  **時機上的判斷**：`FACTION.md` 標的是「程式完成／未實機驗證（素材未到位，先擱置）」，血狂之爭還沒有任何既有內容
  依賴舊語意——**現在反轉零成本，等劇本做起來再反轉就得回頭重擺所有 trigger**。

* [x] **戰狼實機回報兩則：亮場景的怪物體光、撲擊節奏沒有「停頓」（⏳ 未編譯未實測）**（2026-09-17，
  見 [PROBLEMS.md](PROBLEMS.md) **E36** ＋ [BOSS_MODULE.md](BOSS_MODULE.md) §7.1）：<br>
  **① 大白天每隻怪都發光、而且一直閃**——兩個獨立成因剛好同時出現。怪物體光是兩層：`LightSource` 走照明系統、
  亮場景自然無效；但 `CharacterGlow` 是**加色**光暈，不受「這張圖吃不吃照明」限制，所以照樣亮
  （當年那句註解「亮場景不會有任何副作用」只對第一層成立）。閃爍則是 `LateUpdate` **每幀**用 `bounds` 算直徑，
  而逐格素材多半 trim 過、每幀高度都不同 ⇒ 光暈忽大忽小。**暗場景其實一直在閃**，只是被黑暗蓋掉。<br>
  修法：`CharacterGlow` 加 `OnlyInLitAtmosphere`（預設 false＝零行為變化，怪物體光掛時設 true），查新開的
  **`AtmosphereController.LightsEnabled`**——那條「吃不吃照明」的判定原本抄在兩個地方（`BuildLights` 與 F9 快照），
  順手收斂成單一真相，以後加新氛圍不會再漏改一邊。尺寸改成「遇過的最大可見高度」單調快取、中心偏移量一次。<br>
  **② 狼一直在移動，沒有掠食者的停頓**——第一版的 `Stalk` 每幀都在繞圈，作者要的是「停→移一小段→停→衝」。
  拆成 `StalkHold`（站定）／`StalkStep`（側移一段固定距離就停）交替 1~3 輪；撲法改成兩種隨機
  （撞過去／停在面前咬）。<br>
  **這次最有價值的做法是把狀態機用 python 重跑一份靜態模擬**（統計各階段時間佔比、每分鐘撲擊次數），
  在還沒進 Unity 之前就抓到兩個死結：**(a)** 貼身咬沒有時間上限 ⇒ 玩家站著不動時狼永遠黏著磨血，
  60 秒只撲得出 1 次、貼身 53 秒；**(b)** 咬到上限後的「退開」也是一次 `StalkStep`，而退開第一幀還在 `BiteRange` 內，
  被那條「貼身就中斷側移」的捷徑立刻踢回去 ⇒ 咬→退→咬的無限循環。兩個在程式碼上都看不出來。
  修完的數據：玩家站著不動 12.8 次/分、站立佔 78%；走走停停 4.2 次/分、站立佔 55%。<br>
  順帶把戰狼的 `Speed` 從我原本填的 2.5 調成 **3.5**——2.5 比玩家(5)慢太多，模擬顯示玩家只要持續移動，
  狼就一直卡在 `Approach` 追不上，撲擊模組幾乎沒機會演（1.6 次/分 → 4.2 次/分）。

* [x] **戰狼「只要我在走動就咬不到我」——衝刺改成全程追蹤＋動畫跟著加速（⏳ 未編譯未實測）**（2026-09-17，
  見 [BOSS_MODULE.md](BOSS_MODULE.md) §7.3 第 5~7 點）：<br>
  作者實機回報三件事，查下來是三個獨立成因：<br>
  **① 撲擊撲到的是「半秒前的你」**。衝刺原本是「起跑鎖定方向、全程直線」（那是我當初問、作者在
  「預告→側閃→反擊」的框架下選的）。但衝 3~5 單位要 0.3~0.5 秒，玩家在這段時間已經走掉 1.5~2.5 單位。
  **靜態模擬把它量出來了：玩家繞圈側移時命中率只有 12%，站著不動卻是 99%**——所以這個 bug 只有在玩家
  走動時才看得出來，難怪第一次實測沒發現。改成**每幀朝目標當下的位置**衝之後是 99%。<br>
  ⚠ 作者同時明確要求**不要做預判落點**（不算玩家前方的攔截點）：那會變成「只要出手就一定咬到」，
  走位完全失去意義。躲不躲得掉由 `ChargeMaxSeconds`(0.8) 決定，不是由演算法的聰明程度決定。
  撲空也照樣播咬的動作（`NotifySkillCast` 強制播，不靠 `AttackRange` 自動判定——撲空時距離不夠不會播，
  畫面上會變成「衝過去然後呆站著」）。<br>
  **② 作者說「衝過去的速度跟平常移動時一樣」——其實位移早就是 3 倍了**（3.5×3＝10.5，玩家 5）。
  沒有突襲感的真正原因是 `MonsterAnimator` 的走路 fps 連動 `Clamp(速度/ReferenceSpeed, MinMul, 1)`
  **上限寫死 1**：腳步頻率跟散步一樣、位置卻在飛 ⇒ 視覺上是「滑過去」不是「衝過去」。
  加了 `MaxMul`（預設 2.5，對既有怪零變化——一般怪的實際速度恆等於 `Speed`，吃不到上限）。
  **通則：這類「會短暫加速」的怪，突襲感是動畫節奏給的，不是位移數字給的**，調再快的數字都沒用。<br>
  **③ 「在我身邊等啊等」**：`StalkHold` 要等距離超過 `StalkRange × 1.25`＝6.25 才回去追，遲滯開太大，
  玩家只是在走動、還沒真的跑遠，狼就站在原地不跟上。改成 `StalkExitRange` 5.6。<br>
  **兩個實測前就該知道的數字**：改完之後繞圈側移的命中率是 99%（全程追蹤＋速度是玩家 2.1 倍，
  在追逐問題上幾乎必中），嫌太黏就調 `ChargeMaxSeconds`→`ChargeSpeedMul`；另外玩家**全速直線逃**時
  狼（3.5）追不上玩家（5），會一直卡在 `Approach` 撲不出來——設計上合理，但想讓牠更纏人就得動 `Speed`。

* [x] **戰狼「咬的動作有播、血卻沒掉」＝貼身判定用了中心距離（⏳ 未編譯未實測）**（2026-09-17，
  見 [PROBLEMS.md](PROBLEMS.md) **F22**）：<br>
  作者回報「我根本沒移動，牠還是咬不到我」。上一輪我以為是「撲擊鎖定起跑點」造成的，改成全程追蹤之後
  **問題還在**——因為那只是第二層，真正的根因在更下面：<br>
  **Brain 的到達判定用「中心距離 ≤ 1.3」，而傷害結算用「碰撞框邊緣距離 ≤ 0.02」，兩把不同的尺。**
  實測幾何：戰狼身體框半寬 ≈0.45（可見寬 0.70 ＋ `HitboxPadding` 0.2）、玩家 `CircleCollider2D` 半徑 0.5
  ⇒ **中心距離 1.3 的時候兩個框還差 0.35 沒碰到**。於是狼衝到 1.3 就停下來播咬的動作，`EnemyContactDamage`
  那邊卻判定「沒碰到」⇒ 動作有、傷害沒有。靜態模擬用真實框尺寸重算，**改動前的咬中率是 0%**——
  站著不動也一樣，難怪怎麼試都咬不到。改用 `Physics2D.Distance`（與接觸傷害同一份幾何）之後：
  站著不動 90%／一直走動 83%／繞圈側移 80%。<br>
  **這個誤差跟體型綁死**，換一隻大一點或小一點的怪，寫死的 1.3 就又錯一次、錯的方向還不一定——
  所以通則是「**判定『碰到了沒』的地方超過一處時，它們必須讀同一份幾何**」，中心距離只能拿來判遠近。
  辨識特徵也記進 F22 了：**動作有播、數字沒動 ⇒ 幾乎一定是兩套判定用了不同的尺**，
  這種 bug 在畫面上看起來像「AI 很笨」，很容易一路往行為邏輯查下去。<br>
  另外兩項一起修：**① 衝刺時限改成依起跑距離動態算**（固定 0.8 秒 ×10.5 ＝ 只能跑 8.4 單位，
  作者形容「不管我跟他多遠，他都只前進固定距離」，離遠一點就衝到一半停下來咬空氣）；
  **② 觀望的交替輪數 1~3 → 1~2**（作者回報「在我旁邊晃啊晃 3~4 次」，觀望是為了壓迫感，太多輪變發呆）。

* [x] **戰狼衝刺「只前進兩步就停」＝程式生成的怪沒開 Rigidbody 內插（⏳ 未編譯未實測）**（2026-09-17，
  見 [PROBLEMS.md](PROBLEMS.md) **E37**）：<br>
  這個 bug 連改三輪都沒修好，**因為根因不在 AI 邏輯，在物理時序**。作者第三次回報「站在原地不動，
  狼第三輪衝刺才咬到，而且一樣是短衝刺，往前跑兩步就咬」。<br>
  **查到的是兩件單獨看都不像問題的事疊在一起**：① `Monster.prefab`／`Player.prefab` 的 `m_Interpolate` 都是 1、
  `MonsterController` 的註解也寫著「Rigidbody2D 已開 Interpolate（見 E5）」——但那**只對 CSV 有填 `PrefabPath`
  的舊怪成立**；route B（程式量產）的怪是 `new GameObject` ＋ `AddComponent`，拿到的是 Unity 預設值 **None**，
  程式裡從頭到尾沒有一行設過它。② 物理 60Hz，沒有內插時 `transform.position` 只在有物理步進的那一幀更新
  ⇒ 畫面跑得比 60fps 快就**有整幀位移是 0**。<br>
  於是我那個「單幀位移低於預期就當撞牆」的判定，在起跑緩衝（0.12 秒 ≈ **1.26 單位**）一過就被第一個
  「沒動」的幀中斷——那正是作者看到的「往前跑兩步」，也解釋了「撲三四次才累積到貼身」。<br>
  **而且同一個地雷埋在 `MonsterActuator.UpdateStuck` 裡**（`moved < MoveSpeed × Time.deltaTime × 0.4`）——
  那是**所有怪**共用的卡住偵測，高幀率下會讓怪莫名其妙側滑解卡、累積夠了甚至進「放棄期」站住 0.6 秒。
  補上內插一併解決。<br>
  **修法**：`MonsterActuator.Awake` 補 `interpolation = Interpolate`（與 prefab、與 E5 的要求一致，畫面也更順）；
  `PounceBrain` 的撞牆判定改成每 0.2 秒結算一次累積位移（即使哪天內插被關掉也不會壞）。
  另加 `PounceBrain.DebugLog` 開關，印出每次衝刺的「結束原因／起跑距離／實際跑了多遠／結束時的邊緣距離」，
  下次手感對不上可以直接看數字，不必再靠猜。<br>
  **這次最該記住的教訓寫進 E37 的第 3 條通則**：**靜態模擬驗得了「邏輯對不對」，驗不了「引擎的時序」**——
  模擬裡每一幀都有位移，不存在物理與渲染不同步。前三輪的模擬每次都說「沒問題」，實機每次都不對，
  症狀本身就在指向時序，我卻一直往行為邏輯裡找。另外兩條通則：prefab 上調的設定程式生成的物件一個字都不繼承；
  不要用單幀位移判斷「有沒有在動」。

* [x] **戰狼「我一攻擊牠就不還手」＝擊退把牠推出自己的觀望圈（⏳ 未編譯未實測）**（2026-09-17，
  見 [PROBLEMS.md](PROBLEMS.md) **F23**）：<br>
  作者自己猜到方向（「是不是被擊退後又重算觀察次數」），查下來確實是，而且是**三層疊起來**的：
  ① 擊退距離 ＝ `sprite.bounds.size.x`（**整張圖寬、含透明邊**）×`KnockbackPercent/100` ⇒ 戰狼 256px/PPU 256
  ＝1.0 單位 ×50% ＝**每次推 0.5 單位**；② `InvincibleTimeMs=0` ⇒ 每發命中都走一次受擊流程、擊退窗口期間
  `Think()` 整段不跑（**F19**）；③ **Brain 的「站定觀望」是絕對靜止的** ⇒ 一段 0.6~1.2 秒的站定會被推
  2~3 次＝1~1.5 單位，**必定**超出觀望圈外緣 ⇒ 判定「目標走遠了」回去追 ⇒ 走回來 ⇒ **重抽輪數** ⇒ 無限循環。<br>
  **第一版修正只改了「不重抽輪數」，模擬顯示完全沒用**（擊退每 0.35 秒一次時撲擊仍是 0.1 次/分）——
  因為真正卡住的是「站定期間被推出去」這件事本身，輪數根本沒機會減。
  第二版把「站定」改成**保持距離**（被推出觀望圈就自己走回來，不中斷觀望、不消耗輪數）才解掉：
  同條件下撲擊 **0.1 → 10.4 次/分**；沒被打時距離遠小於觀望半徑，照樣站著不動，節奏感不受影響。<br>
  **兩條通則寫進 F23**：① 任何「站在原地等」的 AI 狀態都要想「被擊退推出去會怎樣」——受擊反饋是共用的，
  它不知道你的 Brain 有距離門檻，最省事的解法是讓那個狀態**主動維持距離**而不是絕對靜止；
  ② **「進度」類的計數不要在狀態切換時無條件重抽**，否則任何能打斷狀態的外力都變成無限重置。<br>
  火力夠猛時仍然壓制得住牠（模擬：每 0.2 秒擊退一次 ⇒ 撲擊 0.2 次/分），這是設計上可接受的——
  那個 DPS 下戰狼撐不到一秒；真要讓牠在彈雨中還手，調 CSV 的 `InvincibleTimeMs`／`KnockbackThreshold`／
  `KnockbackPercent`，不要再動 Brain。

* [x] **腳底對齊：AI 生成序列圖「每個動作畫在畫布不同高度」的通用防線（⏳ 未編譯未實測）**（2026-09-17，
  見 [SHADOW.md](SHADOW.md)〈腳底對齊〉）：<br>
  作者回報戰狼「影子對位不正確，而且 idle 跟 walk 會忽大忽小」，並問能不能用程式修，因為「AI 產序列圖還不成熟，常有這問題」。
  **量了全部 65 幀之後，數據跟直覺不一樣**：幀與幀之間其實很穩（idle 高度極差 2px＝1%、腳底極差 0；
  walk 8%／5px；attack 9%／0）。真正的差異在**動作之間**：<br>
  | 動作 | 可見寬 | 可見高 | 腳底距畫布底 |<br>
  | idle | 181 | 143 | **56px** ｜ walk | 221 | 111 | **69px** ｜ attack | 199 | 144 | 55px |<br>
  **13px 聽起來很小，但角色會被 `CharacterWorldHeight ÷ idle可見高` 放大**（戰狼 3.49 倍）⇒ 在遊戲裡是
  **0.19 世界單位＝玩家身高的 10%**，切 idle↔walk 時整隻怪上下跳，影子跟著跳。<br>
  **做法**：載幀時量每幀「不透明內容最底端距畫布底幾 px」，把差異補償到該幀 Sprite 的
  `pivot.y = 0.5 + (該幀腳底 - 基準腳底) / 畫布高`。基準取 **idle 第一幀**——這一點是關鍵：
  碰撞框與顯示大小也都是用 idle 算的，所以基準幀的 pivot 維持 0.5、**角色的絕對位置與碰撞框一個像素都不動**，
  只有其他動作被拉齊；順帶還讓碰撞框**更準**（它本來只對 idle 準）。驗算：三個動作的腳底位移
  從相差 0.191 單位 → **0.000 單位**。<br>
  **影子不用另外處理**：`BlobShadow` 換算錨點時本來就讀當前 sprite 的 pivot（`Vector2 pivotPx = sp.pivot;`），
  補償會被它自動吸收——這也是既有架構早就預留的路（那行註解寫著「bodyScale > 1 時 pivot 被往下移，這裡自動跟上」）。<br>
  **刻意不做的**：尺寸正規化。walk 比 idle 寬 22%、矮 22% 是**姿勢**（奔跑伸展壓低）不是缺陷——
  用面積看 idle √(181×143)=161、walk 157、attack 169，**差異只有 8%**；強制統一高度會把 walk 放大 29%
  變成超巨大的狼，比現在更怪。作者拍板不做。<br>
  ⚠ **影子對不準另有其因**：`ShadowAnchorTable.csv` 裡有 `wolf archers`、`werewolfsoldier`，**就是沒有 `wolf warrior`**
  ——沒有錨點資料時 `BlobShadow` 退回自動量測，本來就不準。作者要在 Unity 跑一次
  `Project Tools → 角色 → 計算影子錨點` 補上那三列。

* [x] **戰狼影子「切動作就位移＋忽大忽小」＝錨點演算法對站姿/跑姿判定不同（改表解決）**（2026-09-17，
  見 [SHADOW.md](SHADOW.md)〈四足獸型的常見誤判〉）：<br>
  作者跑完「計算影子錨點」後回報影子仍會 shift、walk 的影子大一圈，並明確要求**不要改影子演算法**。<br>
  **查下來原因不是素材位置**：三個動作「不透明內容的水平中心」相對畫布中心只差 3px
  （idle −2.0／walk −5.0／attack −4.5），工具算的 `AnchorX` 卻是 **idle −28.8／walk −3.8／attack −31**。
  差在演算法對姿勢的判定——它在「最底 15% 帶」找腳段：**idle 的 3/4 正面站姿四腳重疊只連成一段**
  （走「一段＝可見框中心」那條，偏到 −28.8），**walk 的側面奔跑前後腳分開連成兩段**（走「兩腳中點」，−3.8）。
  `WidthPx` 同理（`max(兩腳跨距, 框寬)`，跑姿框寬本來就大 22%）。<br>
  **解法就是 SHADOW.md 早就寫好的那條**（「披風/長袍/爪子垂到腳邊會算歪，這就是 manual 存在的理由」）：
  三列的 `AnchorX`／`WidthPx` 手改成同一組（−4／190）、`Source` 改 `manual`。
  驗算：影子 X 位移 **0.371 → 0.000 世界單位**、寬變化 **22% → 0%**；垂直方向早已由前一則的〈腳底對齊〉吸收（極差 0.027）。<br>
  **順手把通則寫進文件**：**四足獸型（狼/狗/豹）幾乎一定會中這條**——站姿與跑姿的腳段數天生不同，
  之後每加一隻這類怪，跑完工具就順手把三列的 X／寬拉齊、`Source` 設 `manual`。

* [x] **戰狼「一跑起來就大一圈」＝程式自己把牠放大的（⏳ 未編譯未實測）**（2026-09-17，
  見 [PROBLEMS.md](PROBLEMS.md) **G12**）：<br>
  作者說「walk 明顯比 idle 大很多」，要照當年主角 **G7** 的做法處理。查下來**不是素材問題**：
  `MonsterAnimator.Setup` 本來就有「逐動作**高度**正規化」（`tileSize × idle可見高 ÷ 該動作可見高`）——
  對人型很有效，但四足獸的 walk 是**側面奔跑、身體壓低**（高 111 vs idle 143）⇒ 倍率 **×1.288**，
  把本來就比較長的奔跑姿勢**再放大 29%**：等效寬 221→**285px**，idle 才 181 ⇒ **差 57%**。
  **57% 的落差裡素材只貢獻 22%，另外 35% 是程式放大的。**<br>
  **為什麼不照抄 G7 的指標**：G7 的「√(高 × √面積)」對戰狼算出來是 **walk 要放大 20%**，跟肉眼相反——
  walk 雖然長，但矮 22%、實際畫到的像素還少 10%。兩個姿勢的長寬比根本不同（181×143 近方形 vs 221×111 扁長），
  **不存在能讓它們「看起來一樣大」的單一縮放**，只有取捨。所以作者拍板改成**手填**。<br>
  **做法**：`MonsterData.csv` 表尾加 `IdleScale`/`WalkScale`/`AttackScale`（pant 沿用 idle），
  **留空＝維持原本的自動高度對齊 ⇒ 既有 16 隻怪一個像素都不變**，有填就完全覆寫。
  戰狼填 `WalkScale=0.9`：等效寬差距 **57% → 10%**。<br>
  **順手修掉前一則腳底對齊的一個漏洞**：pivot 補償只算了像素差，沒考慮「各動作的 tileSize 不同」——
  而既有的自動高度正規化本來就會讓它們不同，所以**腳底其實還沒真的對齊**。公式補上
  `× (基準動作 tileSize ÷ 該動作 tileSize)` 之後，三個動作的腳底位移極差 **0.0000**（不管有沒有填縮放）。<br>
  **通則寫進 G12**：「視角/姿勢換了」的素材不能用任何單一尺度自動對齊，只能給人一個旋鈕；
  自動正規化留給「同一視角、只是畫粗一圈」的情況。還有一句給下一個人——
  **看到「某個動作特別大」先去看有沒有人在替你正規化**，而不是先怪素材。

* [x] **新武器「狂族十字弓」＝背包是十字弓、射出去是弩矢（純資料、零程式改動，⏳ 未實測）**（2026-09-17）：<br>
  作者問「弓／火槍這類武器總不能把整把弓扔出去，是不是要在 `WeaponTable` 加欄位」。
  **答案是不用**——專案本來就有兩條互不相干的取圖管線，只是從沒被當成一件事寫下來：<br>
  | | UI 側（背包／儲藏／抽獎／存檔欄）｜ 世界側（真正射出去的東西） |<br>
  | 欄位 | `ItemTable.IconPath` ｜ `WeaponTable.WeaponSpritePath`（欄名叫「子彈圖」不是「武器圖」） |<br>
  | 路徑 | `ItemDatabase.cs:104` → `ItemData.Icon` → `ItemIcons.Apply()` ｜ `WeaponManager.cs:250` → `WeaponData.WeaponSprite` |<br>
  兩張表只靠 `ItemTable.WeaponID` ↔ `WeaponTable.ID` 對應，**圖各走各的**；抽選池／存檔／解鎖清單存的都只有
  `ItemId`，所以也不受影響。既有武器其實早就在用（ID 1 飛劍的 icon 是 `UI/Icons/Equipment/weapon_sword` 256px、
  子彈圖是 `Weapon/single/weapon_sword` 1260px，兩個不同檔案）。<br>
  **這次加的三處**：`ItemTable` 33（icon＝十字弓）、`WeaponTable` 33（子彈圖＝`Weapon/single/flyObj/arrows`、
  `Damage 3`／`ManaCost 1`／`BulletScale 2.5`／`SpriteAngleOffset 0`）、配方**沿用現成的 `RecipeTable` 44
  「單發直飛無能力」**（不新開列——44 全欄留空＝全預設，正好就是「單發直飛、不穿透不反彈」）。<br>
  **`BulletScale` 怎麼定的**：子彈世界尺寸 ＝ `Bullet.prefab` 的 `localScale`(0.1) × `PlayerScale`(1) × `BulletScale`，
  而 sprite 本身 ＝ 像素 ÷ PPU(100)。弩矢 500px ⇒ 0.5 單位，飛劍 1260px ⇒ 1.26 單位，差 2.5 倍，
  所以 `BulletScale` 填 2.5 讓兩者視覺份量相當。**這是算出來的初值，實際順不順眼要進武器工坊 Play 中微調。**<br>
  **`SpriteAngleOffset` 填 360（不是 0，見 PROBLEMS E38）**：`BallisticsEngine.cs:130` 是「飛行角度 ＋ 補正」，
  等於要求圖在 0 度時箭頭朝右(+X)，`arrows.png` 剛好就是水平朝右 ⇒ 數學上補正該是 0。
  **但填 0 會讓子彈完全不旋轉**——兩處都寫成 `if (SpriteAngleOffset != 0f)` 才套 rotation，
  0 被當成「這顆子彈不跟著飛行方向轉」的旗標。作者實測「往下射，箭頭還是朝右」就是這個。
  **改填 `360`**（≡ 0 度、且 `!= 0f`，也在武器工坊 −360~360 的合法範圍內）。<br>
  ⚠ **匯入設定的坑（通則，以後每張新武器圖都會中）**：`Editor/GameEffectTextureImportSettings.cs` 對
  `Resources/Weapon/` 底下的**新圖**一律套 **Point**，但既有 7 張武器圖**全都是 Bilinear**（後來被改過）。
  500px 的圖在遊戲裡縮到 0.5 單位，Point 會有明顯鋸齒 ⇒ 這次把 `arrows` 與 `weapon_crossbow` 兩張的
  **Filter Mode 手動改回 Bilinear**。以後丟新武器圖進來記得順手檢查這一欄。<br>
  **icon 另存一份**：十字弓原圖放在 `Weapon/single/`（500px／Max Size 2048），直接拿來當 icon 能動
  （ID 13 御靈水晶就是這樣），但會把一張 2048 的大貼圖拉進 UI ⇒ 另複製一份到
  `Resources/UI/Icons/Equipment/weapon_crossbow.png`，meta 比照其他 icon（**Max Size 128**、不壓縮、關 Mipmap）。<br>
  **刻意沒做**（作者拍板）：不加進 `BaseWeaponRoll.csv` 抽選池；`HitEffectID` 留空（同 ID 31「飛劍-無能力」）。<br>
  ⏳ **待驗**：未進遊戲實測——要確認 icon 在背包顯示正常、射出來是弩矢且大小順眼、箭頭朝向對。

* [x] **射手型怪物模組 `ArcherBrain` ＋ 打通「怪物使用投射型武器」（Phase 2 第一階段，⏳ 未編譯未實測）**（2026-09-17，
  見 [BOSS_MODULE.md](BOSS_MODULE.md) §8）：作者要一隻「弓箭手」——先觀察、依射程決定要不要靠近、
  中間有障礙物要先算會不會擋住飛行物、不用移動就射得到就原地射、每發之間停下來 idle。<br>
  **卡點不在 Brain，在地基**：`MonsterWeaponUser` 只實作了 `Mode=Summon`，碰到投射型武器直接吐
  「投射型武器供怪物使用為 Phase 2——待把 PlayerController 的發射管線抽成不綁玩家的共用服務」。
  量了一下那個 Phase 2：`PlayerController` **2293 行、11 種模式、9 個 `Shoot*` 方法**散在 999~1900 行，
  而且深綁玩家狀態（滑鼠瞄準／血統出手點／耗魔／集氣／連擊／能力珠／`HandleBulletHit`）⇒
  一次全抽等於重寫 900 行＋重測 27 把武器。**作者拍板分階段：這次只把 `Normal` 搬進共用服務。**<br>
  **新檔 `Assets/Scripts/Weapon/WeaponCastService.cs`**：把「發射」抽成
  誰射的／從哪射／往哪射／打得到哪一層／命中要做什麼 全是參數的 `CastContext`。
  玩家 `ShootNormal` 與怪物 `TryFireProjectile` 從此共用同一份彈道生成程式 ⇒
  分裂／反彈／追蹤／平行／穿透／軌跡只有一份實作，**配方那些欄位對怪物全部有效**。
  `ParallelOffsets`／`ResolvePierceableLayers`／`ResolveNonBounceLayers` 也搬進去，`PlayerController` 留薄封裝
  （拋物線等模式還在用這些名字，不能直接刪）。`PlayerController` 2293 → 2257 行。<br>
  ⭐ **`TargetLayers` ＝「目標所在的那一層」是關鍵設計**（射玩家→Player 層、射敵對怪→Enemy 層）：
  用 layer 先擋掉，怪物的箭天生不會被自己人擋住、也不會誤傷同伴，**不必在命中 callback 裡補陣營判斷**；
  真正「能不能造成傷害」仍由 `CombatSystem` 查 `FactionRelations`。<br>
  **刻意沒一起抽進服務的**：瞄準與出手點、耗魔／集氣／連擊／能力珠、以及**命中後的效果鏈**——
  硬把玩家的命中鏈搬進去，會把 `TryTriggerSubWeapon`／`TryTriggerGroundEffect` 整串玩家專屬狀態拖過來，
  服務就變成第二個 `PlayerController`。**這條原則寫進 §8.4，之後搬其餘模式照它走。**<br>
  **`ArcherBrain` 的核心是一個判斷句** `CanShootFrom()` ＝ 距離在 `MinRange`(2)~`ShootRange`(7) 之間
  **且** `CircleCast` 打得過去。三個踩到／想清楚的點：
  ① **射程必須是「行為射程」不能用子彈壽命**——配方 44 是 `Speed 15 × LifeTime 3 ＝ 45 單位`，畫面高才 10，
  照抄的話牠會從畫面外射你；7 ≈ 畫面寬一半。
  ② **視線用 `CircleCast` 不用 `Linecast`**（箭有體積，擦邊的細線實際會撞柱子），
  且障礙層用 `LayerMask.GetMask("Environment","Water")`＝**與 `MonsterActuator`/`MapNavGrid` 同一份真相**，
  不會出現「牠不開火但你看不出哪裡被擋」。
  ③ **視線起點要推出自己的身體框**，否則起始圓會重疊到牠正貼著的那面牆 ⇒ **站在牆邊就永遠不開火**。<br>
  移動照 `PounceBrain` 的教訓**綁距離不綁時間**（PROBLEMS F20），一次只挪 1.2~2.2 單位就回去重新評估；
  途中一旦滿足條件就立刻停下來射（不然會「明明走到位了還要再走兩步才開火」）。<br>
  **新怪物 18「Wolf Archers」狂族弩手**：`BrainType=Archer`、`Weapon=33`（狂族十字弓）、`Faction=Werewolf`、
  HP 25／Speed 3／接觸傷害 5。素材沿用 BloodFang 的 `Wolf Archers`（idle 20／walk 14／attack 25 張）。<br>
  ⚠ **已知會不對的地方（實測時先看這個）**：attack **25 張 × AnimFPS 14 ＝ 1.79 秒**，
  而 `MonsterController.SkillCastAnimSeconds` 預設只有 **0.6 秒** ⇒ 拉弓拉到一半會切回 idle。
  要嘛拉高那隻怪的 `AnimFPS`，要嘛調 `SkillCastAnimSeconds`（**全怪共用的 public 欄位**，動它要看其他怪）。<br>
  **順手修對的舊文件**：`RECIPE_AND_WEAPON.md` 的〈SpriteAngleOffset 設定說明〉原本寫「圖本身就朝右 → 填 0」，
  那正是 PROBLEMS **E38** 的坑（0 ＝ 不旋轉），已改成「填 360」並附警告。

* [x] **狂族弩手第一輪實測修正：放箭時機對齊序列圖的幀 ＋ 射程砍 1/4（⏳ 未編譯未實測）**（2026-09-17，
  見 [BOSS_MODULE.md](BOSS_MODULE.md) §8.2b、[PROBLEMS.md](PROBLEMS.md) **F24**）：
  作者實測回報兩點——「**武器都還沒提起來，弓箭就射出來了**」、「射程有點太遠，砍 1/4 試試」。<br>
  **① 放箭比動畫早＝順序反了，不是動畫慢**：第一版在 `Draw` 站定 0.55 秒後才 `TryUse()`，
  而 attack 動畫是 `MonsterWeaponUser` **施放成功之後**才 `NotifySkillCast()` 起播的
  ⇒ 箭先飛出去、動畫才開始播。改成 **`BeginDraw` 進來就讓動畫起播，演到「弩舉到定位」那一幀才放箭**。<br>
  ⭐ **時機寫成「幀號」不是「秒數」**：`MonsterAnimator.SetState` 切 Attack 時幀索引歸零、以 CSV `AnimFPS` 起播，
  所以第 N 幀 ＝ `(N-1) ÷ AnimFPS` 秒 ⇒ **改 CSV 的 AnimFPS，時機自動跟著對**，換一隻拉弓節奏不同的射手只要改 `ReleaseFrame`。
  把 25 張 attack 拼成一張聯絡表看過：1~3 預備、4~9 往前推、**10~12 完全水平前伸到位**、13~19 維持、20~25 收弩
  ⇒ `ReleaseFrame = 11`（@14fps ＝ 0.71 秒）。<br>
  ⚠ **順帶解掉「動畫播不完」那條待辦**：`NotifySkillCast()` 只延 `SkillCastAnimSeconds`（**預設 0.6 秒**），
  比拉弓到放箭（0.71 秒）還短 ⇒ 只叫一次動畫會在放箭前切回 idle；但**每幀無腦續**又會讓最後一次多撐 0.6 秒、
  拖過結尾去演第二輪前段（像「射完又舉一次弩」）。正解是 `KeepAttackPose`：
  **只在「再續一次也不會超過這套動作的結束時間」時才續**。
  **刻意沒改 `SkillCastAnimSeconds`**——它是 `MonsterController` 的 public 欄位、全怪共用，為一隻怪動它會波及所有會施法的怪。
  收弩的後半段（`FollowThroughSeconds` 0.8）與射擊間隔的 idle **重疊**，所以節奏沒有被拉長。<br>
  **② 射程 7 → 5.25**（砍 1/4，≈ 畫面寬的 1/3）。`PreferredRange` 同比例 5.5 → 4.1
  （維持 ≈ ShootRange × 0.79）——**這兩個要一起調**，否則牠想站的位置會落在射程外，變成一直跑位卻不開火。<br>
  **通則已寫進 PROBLEMS F24**：任何有預備動作的怪物攻擊（拉弓、舉杖、掄石頭）都吃這一條，
  節奏要定成「動畫起播 → 第 K 幀出手 → 收尾與冷卻重疊」，不要定成「等 N 秒 → 出手 → 播動畫」。

* [x] **射手型模組的鐵則：動作播了就一定要射出箭（⏳ 未編譯未實測）**（2026-09-17，
  見 [BOSS_MODULE.md](BOSS_MODULE.md) §8 開頭的鐵則段）：作者實測回報
  「**常常十字弓已經提起來了，卻不射箭**……只要播放 attack 動作，就一定要射箭出去，不然看起來很像 bug；
  再來**弓箭滿場飛也很好玩**，不需要停止」。<br>
  **兩個來源**：① `Draw` 階段每幀重驗 `CanShootFrom`，拉弓那 0.7 秒內玩家很容易走出射程或閃到柱子後
  ⇒ 取消放箭；② `TryUse()` 撞到武器配方自己的 `FireInterval` 冷卻會回 false ⇒ 動作照播、箭不會出去
  （目前 idle 1.2~1.8s ≫ FireInterval 0.3s 所以撞不到，但換一把慢武器就會中）。<br>
  **修法：把所有檢查前移到「決定拉弓的那一刻」**（`Observe`），包含新加的 `WeaponReady()`；
  `Draw` 階段**一個取消條件都不留**，進去就必然放箭。
  另外把「動作播了卻沒射出去」做成**無條件 warning**（不藏在 `DebugLog` 後面）——
  走到那裡就代表鐵則被破壞，而且一定是設定問題（`Weapon` 欄沒填武器 ID／`Mode` 不是 `Normal`／
  `WeaponManager` 的 Bullet Prefab 沒設），不是手感問題。<br>
  ⭐ **這是模組層級的設計原則，不是這隻怪的調整**：作者明確交代「弓箭手類的攻擊模組都要記得這件事情，
  除非後面有例外，有例外就另開一個弓箭手 2 的模組」。所以鐵則寫在 §8 最顯眼處、`Draw` 分支的註解裡，
  §8.5 的 SOP 也加了一條——**之後寫任何射手型 Brain 都照抄：檢查全部放在「決定出手」那一刻，
  出手動作開始之後不准反悔。** 需要「可取消的瞄準」就另開一支 Brain，不要改這支。<br>
  **通則**：這條其實不限射手——**任何「預備動作 → 出手」的怪物攻擊，中途取消都會被玩家讀成 bug**，
  因為玩家看到的是動作、不是狀態機。要嘛別起手，要嘛做完。

* [x] **修「每隻新生成的弩手第一發是空砲」——事前問 `Ready`，冷卻卻是執行時才寫進去的（⏳ 未編譯未實測）**
  （2026-09-17，見 [PROBLEMS.md](PROBLEMS.md) **F25**）：上一輪加了「動作播了就一定要射」的鐵則之後，
  作者回報**還是有舉起十字弓卻沒東西射出去**，並附了 Console log。<br>
  **log 本身就是證據**：每次 `[MonsterGlow] 掛上體光：Wolf Archers`（新怪生成）之後約 3 秒就跟著一次
  `[Archer] … 播了 attack 卻沒射出東西`，然後那隻就正常了 ⇒ **不是隨機，是「每隻怪的第一次」**。<br>
  **根因**：`MonsterWeaponUser` 是懶解析（`WeaponManager` 開場才載好，所以 `Resolve()` 放在 `TryUse()` 第一行），
  而「**起手緩衝**」的冷卻**正是在 `Resolve()` 裡才寫進 `_cooldown`** 的。於是
  ① 怪剛生成 `_cooldown = 0`；② Brain 事前檢查 `Ready`（`=> _cooldown <= 0f`）拿到 **true 的假答案**
  （`Ready` 不會觸發 `Resolve`）；③ 整套拉弓動作演完；④ `TryUse()` 第一行才 `Resolve()`、把起手緩衝寫進冷卻；
  ⑤ **下一行就被自己剛設的冷卻擋掉**。<br>
  **修法**：解析抽成 `EnsureResolved()`，**`Ready` 也走它**（getter 有副作用，但這個副作用是必要的，已寫在註解）。
  順手把 `Resolve()` 的失敗分兩種：找不到 `WeaponManager` 是**時序問題** ⇒ 不定案、0.5 秒後重試
  （舊版在這裡就 `_resolved = true`，**萬一怪比 WeaponManager 早初始化，牠這輩子都不會再嘗試解析武器**——
  這是還沒被踩到但遲早會踩的隱形地雷）；武器 ID 找不到是設定問題 ⇒ 定案不重試。<br>
  **順手抓到第二個空砲來源**：`TryFireProjectile` 無條件 `return true`，但它呼叫的 `FireNormal`
  在配方沒建好時其實一顆子彈都沒生 ⇒ 上層以為射出去了、照樣進冷卻與播動畫，變成**連 warning 都沒有的空砲**。
  已讓 `FireNormal` 回報實際生成數、`TryFireProjectile` 原樣回傳。<br>
  **Brain 端配合**：`Observe` 遇到「射得到但武器冷卻中」改成**原地等 0.15 秒**再評估，而不是跑去移動——
  牠明明站在射得到的位置，跑開只會看起來很蠢，而且回來還要再走一趟。<br>
  ⭐ **兩條通則寫進 F25**：① **任何「先問狀態、再執行」的 API，若狀態是在執行路徑上才初始化的，
  事前檢查就會拿到假答案**——懶初始化的元件必須讓「查詢」與「執行」走同一個 `EnsureXxx()`；
  ② **回傳值要表示「我真的做了」，不是「我試過了」**。
  第一條的隱蔽之處在於它不報錯、不每次發生，只在「物件剛建立的第一次」出現，很容易被當成偶發的手感問題。

* [x] **修「清掉一群弓箭手 ⇒ Console 被 MissingReferenceException 洗版」＋ 放箭幀 11→14（⏳ 未編譯未實測）**
  （2026-09-17，見 [PROBLEMS.md](PROBLEMS.md) **F26**）：作者放了一堆弓箭手後開始打，
  Console 瞬間刷滿 `MissingReferenceException: The object of type 'MonsterWeaponUser' has been destroyed`
  （不閃退、同一時間戳重複幾十行）。<br>
  **根因：子彈的 callback 活得比射它的怪久。** 命中 callback 綁的是 `MonsterWeaponUser` 的實例方法
  (`OnHit = OnProjectileHit`)，而箭飛在半空中時射手可能已經被打死、`GameObject` 被 `Destroy`
  ⇒ 命中那一刻去讀 `transform.position`／`gameObject` 就炸。一次清場＝空中每顆箭各拋一次。<br>
  ⚠ **真正的陷阱**：**Unity 被 destroy 的物件在 C# 端不是真的 null**，只有 `UnityEngine.Object` 覆寫的 `==`
  看得出來。原本那行 `if (hitTarget == null || _weapon == null) return;` **完全擋不住**——
  `_weapon` 是純 C# 的 `WeaponData`，元件被 destroy 不會把它變 null，檢查照樣通過、下一行就炸。
  正解是 `if (this == null) return;`。<br>
  順帶把擊退方向從「射手→目標」改成**箭的飛行方向**（`bullet.Velocity`）：箭可能已經反彈／追蹤過好幾次、
  射手也可能離很遠，沿飛行方向擊退才符合畫面，而且少一個對 `transform` 的依賴。<br>
  **掃過同類風險**：`PlayerController` 的七個 `HandleBulletHit`／`TrySpawnTrailEffect` callback **沒有**這個問題——
  玩家是常駐物件，專案裡沒有任何地方 `Destroy` 玩家。這條目前只對怪物成立。<br>
  **取捨（已記進 §8.6 待辦）**：射手死掉時，牠已經射出去的箭**不再造成傷害**（箭照樣飛完，只是不結算）。
  要讓「死人的箭照樣殺人」得在發射當下就把陣營快照起來，因為 `CombatSystem.Apply` 需要 source `GameObject`
  查 `FactionRelations`，而那個物件已經沒了。<br>
  ⭐ **通則**：**任何「發射後就獨立存在」的東西（子彈、地面特效、召喚物、協程），只要 callback 指向發射者的
  實例方法，發射者死亡之後就會踩這條。** 寫這種 callback 時先問：「它有沒有可能在擁有者死掉之後才被呼叫？」<br>
  **同一輪的手感調整**：`ReleaseFrame` 11 → **14**（作者覺得箭還是太早飛出去）。
  第 14 幀落在「維持瞄準」那段的開頭而不是「剛舉定」的 10~12，@14fps ＝ 0.93 秒。
  **通則：放箭幀寧可比「動作到位」再晚一兩幀**——玩家的眼睛需要一點時間確認武器已經舉好。

* [x] **新怪「狂族皇家衛士」＋ 跳躍踐踏模組 `LeapSlamBrain` ＋ 程序化裂地 shader（⏳ 未編譯未實測）**
  （2026-09-18，見 [BOSS_MODULE.md](BOSS_MODULE.md) **§9**）：作者要「一開場跳過來踩一下、之後就是普通近戰」的怪。
  做成 **`BrainType=LeapSlam`**＝「一次跳躍 ＋ 既有 `ChaseBrain`」的合成——跳完就把每一幀委派給追擊，本 Brain 不再介入。<br>
  ⭐ **最先踩到的其實是素材**：`jump/` 有 25 張，整組播完角色會**跳兩次**。用 python 量每一幀不透明像素的
  **底邊 y**（PIL 的 `alpha.getbbox()`）才看出來：1~13 一輪、14~25 又一輪，中間 5~11 是騰空。
  **通則：拿到一個新動作資料夾，先量幀再寫程式**——「一個資料夾＝一次動作」是沒有根據的假設，
  而且量幀這件事一行 python 就做得到，比在 Unity 裡一張張點快得多。<br>
  **時機一律寫幀號、不寫秒數**（`TakeoffFrame=5`／`LandFrame=11`／`EndFrame=13`，1-based＝檔名編號）：
  除以 `AnimFPS` 換算成秒，所以改 CSV 的幀率時整段節奏與落地時機自動跟著對（同 §8.2b 的放箭幀）。
  算出來 @13fps ＝ 蓄力 0.31s／騰空 0.46s／落地緩衝 0.15s。<br>
  ⭐ **`LeapRangeMax` 其實是速度旋鈕，不是距離旋鈕**：騰空秒數由幀固定 ⇒ 距離越遠＝飛越快。
  原本設 7.0，換算 15 單位/秒（玩家 5、戰狼衝刺 10.5）＝畫面上讀起來是瞬移不是跳躍，**在寫完之後自己算出來才改成 5.0**。
  **通則：任何「動畫時間固定、距離可變」的位移，距離上限就是速度上限，訂數字前先除一下。**<br>
  **落點鎖定起跳當下的位置、刻意不追蹤**（作者拍板）：蓄力那 4 幀就是預告窗口，看到蹲下就該閃。
  §7 的撲擊可以追蹤是因為牠一輪一輪不停撲；**這一記是一次性的，閃掉就該真的閃掉**。
  落點另外用 `MapNavGrid.HasLineOfSight` 夾進走得到的地方——牆的另一側不該因為會跳就跳得過去。<br>
  **傷害零新程式**：落地丟一個短命圓形 trigger（`LeapSlamImpact`）掛既有的 `EnemyContactDamage`，
  陣營、玩家無敵幀、中央結算全部沿用（同榕樹妖地刺）。⚠ 但因此**實際殺傷＝`LeapRadius` ＋ 目標碰撞框半徑**
  （走邊緣距離，同 **F22**），1.6 實際打得到 2.1。<br>
  **裂痕是 shader 不是素材**（`Resources/Shaders/GroundCrack.shader`）：極座標切扇區長主裂＋兩倍條數的次級分支＋
  中心碎坑＋外掃塵浪，`_Progress` 控制裂到多遠。理由是「每次落地都該不一樣、而且大小要跟殺傷半徑對得起來」——
  序列圖做不到這兩件事。用 alpha 混合不用加色（暗痕要能實心遮住地板，同背景符號層的結論）。<br>
  ⭐ **這次唯一動到共用元件的地方：騰空**。俯視角的 Y 同時是「高度」與「深度」，把 `transform.y` 推上去
  會讓**影子飛到半空、Y 排序把牠判成往畫面上方走了一步**。所以加了介面 `IAirborneVisual`，
  `BlobShadow` 與 `YSortByFeet` 各三行把高度扣回地面（影子留地上、隨高度縮小變淡）。
  **沒實作這個介面的角色完全不變**（高度 0 直接 early return，連 renderer 都不碰）。
  另外騰空期間要暫關 `Rigidbody2D.interpolation`——開著的話 rb 用上一個物理步的位置往回補畫面，
  和「每幀直接改 transform」互相拉扯，會抖。<br>
  **動畫加了 one-shot**（`MonsterAnimator.PlayOneShot`）：既有播放一律循環，而且 `HandleVisuals` 每幀都會
  `SetState` 覆寫狀態 ⇒ 有頭有尾的動作靠 `SetState` 撐不過下一幀。one-shot 期間直接壓過 `SetState`，
  要交還控制權得明確 `CancelOneShot()`。**沒圖時回 false 而不是自動退回**——退回去循環播走路只會變成
  「滑過去然後莫名其妙炸一下」，不如乾脆不跳（Brain 直接轉追擊）。<br>
  ⚠ **jump 刻意不做自動高度對齊、也沒有 `JumpScale` 欄**：自動那套是把該動作的可見高拉成跟 idle 一樣，
  而跳躍的可見高**本來就是動作的內容**；正規化等於把它抵銷，而且越蜷縮的幀被放得越大 ⇒ 騰空時怪會膨脹一圈
  （實測 1.05~1.31 倍，同 **G12** 的機制但發生在幀與幀之間）。所以 jump 一律沿用 idle 的 tileSize。<br>
  檔案：新增 `Behaviors/LeapSlamBrain.cs`／`Combat/LeapSlamImpact.cs`／`Map/GroundCrackFx.cs`／
  `IAirborneVisual.cs`／`Resources/Shaders/GroundCrack.shader`；
  改 `MonsterAnimator`（Jump 狀態＋one-shot）／`MonsterController`（`Anim`、`AirborneVisualHeight`、`LeapSlam` case、兩個新欄）／
  `MonsterData`＋`MonsterSpawner`（讀表）／`BlobShadow`／`YSortByFeet`／`PlayModeStaticReset`；
  `MonsterData.csv` 加表尾兩欄與 **ID 19 `Wolf Royal Guard`**（HP45／Speed3.2／接觸12／AnimFPS13／Faction=Werewolf；
  `LeapDamage`／`LeapRadius` 留空＝24／1.6，要調直接填）。

* [x] **跳躍踐踏第一輪實機回饋：跳更高＋墜落感、裂痕放大、「蹲了一定要跳出去」（⏳ 未編譯未實測）**
  （2026-09-18，見 [BOSS_MODULE.md](BOSS_MODULE.md) **§9.3／§9.5／§9.6**）：作者實機三點回饋——
  跳得不夠高、地板龜裂太小太短、**常常準備跳但沒跳出去**。<br>
  ⭐ **第三點的根因兩個都不在「取消邏輯」裡**（程式碼裡本來就沒有任何中途取消）：<br>
  ① **擊退整段跳過 `Think()`**（**F19**）。§8 的射手不怕這條——牠只是「等時間到再射一次」，被跳過幾幀無所謂；
  但跳躍的位移是**每幀算出來的**，被跳過的幀等於**沒有發生**。玩家連射時擊退窗口首尾相連，
  整段騰空 0.46 秒會被吃光 ⇒ 蹲下、動畫演完、人在原地。
  修法：`MonsterController.SuppressKnockbackInterrupt`（Brain 起跳開、落地關），期間 Think 照跑；
  `TickLeap` 另外每幀把 `rb.velocity` 歸零，連擊退位移也壓制。
  ⭐ **通則：任何「每幀自己寫 transform」的演出都要想一次「被擊退跳過 Think 會怎樣」。**
  用 `act.MoveTowards` 的（如 §7 撲擊）不會中這條，因為 velocity 在擊退期間還在。<br>
  ② **落點被夾回起跳點 ⇒ 原地跳**。第一版只沿直線往回退，退到 0.2 都不通就回傳起跳點，而
  **玩家站的格子對尋徑格來說常常是不可走的**（家具膨脹了一圈）⇒ 比想像中常發生。
  改成 `ResolveLanding`：超出射程取射程邊緣（＝射程內離目標最近的點）、直線被擋就往兩側散開試
  0°/±18°/±36°/±54°/±72°、取「離目標最近的可達點」，**一個都沒有也還是跳**。
  鐵則寫進 §9.3：**蹲下去了就一定要跳出去**（與 §8 射手「動作播了就一定要射」同一條）。<br>
  **墜落感**：抬升 0.55 → **1.5**（＋素材自帶 0.25 ≈ 一個身高），曲線從對稱拋物線 `4t(1−t)` 改成分段——
  頂點前走 sin 前四分之一（接近頂點趨緩＝滯空）、頂點後走 `1−v²`（越掉越快）。
  上升平均 6.5 單位/秒、下降末速 12.4。
  ⚠ **頂點要對齊素材最高的那一幀**（`PeakFrame=8`）：不對齊會出現「圖已經在下墜、程式還在往上抬」的橡皮筋感。
  **通則：程式加的弧線是替素材加強，不是自己演一套。**<br>
  **裂痕**：視覺倍率 1.35 → **2.2**（`LeapRadius=1.6` ⇒ 畫面上 7.0 × 3.5 的橢圓，角色高 1.95），
  停留 3.2 → **9.0** 秒、淡出 1.6 → **3.5** 秒。只放大視覺，殺傷仍嚴格走 `LeapRadius`。<br>
  **順手提出但還沒做**（記進 §9.10）：騰空中怪的碰撞框跟著抬高，但框高 1.9 > 抬升 1.5，
  所以**從玩家頭上飛過時仍可能觸發接觸傷害**，會稀釋「閃開落點」的意義。等實機確認再處理。

* [x] **跳躍第二輪：跳到三個身高＋垂直砸下；新增 `MeleeChaseBrain`（出手就要把動作做完）（⏳ 未編譯未實測）**
  （2026-09-18，見 [BOSS_MODULE.md](BOSS_MODULE.md) **§9.6／§10**）：作者實機兩點——跳還是太矮、
  **「他已經舉起劍了，我躲閃，他竟然還能移動並保持舉劍的動作」**。<br>
  **跳躍高度改成以「角色身高」為單位**（`HopHeightInBodies = 3.0` × `CharacterWorldHeight` × Scale
  ⇒ 5.85 單位＝三個牠疊起來）。作者的描述本來就是相對身高的（「主角頭上三個怪物的高度」），
  寫死世界單位的話換一隻體型不同的怪就得重調。演進：0.55 → 1.5 → 3 個身高。<br>
  ⭐ **「垂直落下」的關鍵是水平那條曲線，不是垂直那條**：第二版已經把垂直改成「滯空＋加速墜落」了，
  作者還是覺得像飛過去——因為水平仍然等速 ⇒ 再高的跳也是斜斜地飛。
  新增 `HorizCurve`：**到頂點就走完 85% 的水平位移**，剩 15% 留給整個下降段 ⇒
  跳滿 5 單位時上升段水平 12.0 單位/秒、下降段只剩 2.1，落下那一半幾乎垂直。
  **通則：拋物線的「重量感」是水平與垂直的比例給的，只調垂直調不出來。**<br>
  跳這麼高需要時間演，`JumpFpsMul` 1.0 → 0.65（蓄力 0.47s／騰空 0.71s／整段 1.42s）；
  1.0 之下騰空只有 0.46 秒，要在裡面升降 5.85 單位 ⇒ 垂直 25 單位/秒，像被彈射。<br>
  ⚠ **影子的衰減常數也得跟著改**（`BlobShadow` 0.55/0.45 → 0.18/0.10）：影子留在地面、沿途滑向落點，
  是玩家判斷「牠要砸哪裡」的**唯一線索**。照「跳 1.5 單位」訂的舊值在 5.85 高時會把影子縮到 0.24 倍、
  透明度剩 0.28 ⇒ 最需要看到落點預告的時候反而看不見。
  **通則：任何「隨高度衰減」的視覺，訂常數時要連最大高度一起代進去看。**<br>
  ⭐ **第二點不是這隻怪的 bug，是 `ChaseBrain` 的天生行為**：它只管「離目標 > 0.2 就一直走」，
  attack 動畫則是 `HandleVisuals` 在「距離 ≤ AttackRange 1.3」時**自動播**的——**兩件事互不相干**，
  所以怪當然會一邊舉著劍一邊追。**所有 `BrainType=Chase` 且有 attack 圖的怪都是這樣**，
  只是以前的怪要嘛沒有 attack 圖、要嘛沒人盯著看。<br>
  `ChaseBrain` 一個字沒改（既有怪零影響），新開 **`MeleeChaseBrain`**：貼身（**碰撞框邊緣距離**，
  不是 `AttackRange` 的中心距離，見 **F22**）才揮、揮的期間 `act.Stop()` 完全不動、attack 走 one-shot
  播完整輪、**沒有任何中途取消**。搭配 `MonsterController.BrainControlsAttackPose`（新旗標，預設 false）
  關掉 `HandleVisuals` 的自動判定——**不設這個就等於白做**，因為怪走過去的路上早就自動舉劍了。
  攻擊節奏 `max(AttackInterval, 動作時長)` 且**從開始算不從結束算**，否則 CSV 的攻速 0.8 會變成實際 1.7 秒一刀。<br>
  §9 的跳躍踐踏跳完之後，委派對象也從 `ChaseBrain` 換成 `MeleeChaseBrain`。<br>
  檔案：新增 `Behaviors/MeleeChaseBrain.cs`；改 `LeapSlamBrain`（高度／水平曲線／委派對象）、
  `MonsterController`（`BrainControlsAttackPose`、`MeleeChase` case）、`MonsterAnimator`（`FrameCount`）、`BlobShadow`（衰減常數）。

* [x] **跳躍第三輪：落地定格一秒＋裂痕慢慢竄開；修「怪一直砍但打不到我」（⏳ 未編譯未實測）**
  （2026-09-18，見 [BOSS_MODULE.md](BOSS_MODULE.md) **§9.2／§10.2／§10.4**）：作者兩點——
  高度 ok 了但「沒有那種壓迫感」、以及**附圖**回報「維持這個距離怪就砍不到我，他們一直攻擊但我不會受傷」。<br>
  ⭐⭐ **第二點是我上一輪引進的回歸，而且是「必然發生」不是「偶爾」**：`MeleeChaseBrain` 拿同一個
  `AttackSlack`(0.25) 同時當「起手揮劍」與「停止移動」的門檻 ⇒ 怪**系統性地停在自己打不到的地方**。
  量出來：狂族皇家衛士框半寬 0.77 ＋ 玩家半徑 0.5 ⇒ 接觸傷害要求中心距離 ≤ **1.27**，
  而怪一到 **1.52** 就停 ⇒ 每次都差 0.25，就是作者截圖裡那個間隙。
  拆成兩個門檻：起手 0.25（動作提早一拍比較自然）、停止 **0.02**（＝與傷害判定同一條線）。
  ⭐ **通則：「決定出手」的門檻可以寬，「停止移動」的門檻必須等於傷害判定的門檻。**
  兩者共用一個數字，就會做出一隻永遠站在自己攻擊範圍外的怪。<br>
  ⭐ **順便補上真正的近戰判定**：量 attack 每幀 bbox 的右緣發現**第 9~12 幀劍已經掃出畫布外**，
  而接觸傷害完全不知道劍在哪裡——它只看兩個身體框有沒有碰到。所以改成**在命中幀（第 9 幀）
  對前方開一次 `ImpactDamageArea`**（把 §9 踐踏用的一次性傷害圈泛用化，`LeapSlamImpact` 改名為它，兩邊共用），
  半徑＝框半寬＋`AttackReach`(0.55)、圈心往面向方向偏半個身位 ⇒ 打得到中心距離 2.21，怪停在 1.29 ⇒ 站著不動必中。
  同時**關掉怪身上的接觸傷害**（`DisableContactDamage`）：傷害只來自揮擊，玩家貼著走位躲掉揮擊就不掉血，
  這才是近戰該有的樣子。命中幀會夾進實際張數，免得換一隻 attack 只有 6 張的怪時一輩子揮空。<br>
  **壓迫感＝砸下來之後的停頓**：高度與速度都到位之後缺的是停頓，所以加 `LandHoldSeconds`(1.0)——
  落地後**定格在最後一幀**一秒才交棒給追擊。one-shot 天生停在結束幀，所以不必另外做，只要延後交棒。
  ⚠ 但**落地那一刻就要把擊退與內插還給系統**（不是等定格結束）：那一秒是玩家的輸出窗口，
  牠該會被打退、該有受擊回饋。<br>
  **裂痕慢慢延伸**（作者說「這個功能可能做不到，做不到就算了」——其實 shader 的 `_Progress` 本來就是
  「裂到多遠」，只是第一版給了 0.22 秒）：`CrackSeconds` 0.22 → **0.85**，剛好在定格那一秒裡竄完。
  <br>　↳ **後記（同日）**：作者實際看了之後改回「猛烈的一次全部出現」⇒ `CrackSeconds` **0.06**、塵浪 0.32。
  定格那一秒仍然保留——**壓迫感來自停頓，不是來自裂痕長得慢**。<br>
  時間軸：蓄力 0.47s → 騰空 0.71s → 落地 1.18s → 定格到 2.18s → 交棒。

* [x] **`MonsterData.csv` 補 `JumpScale` 欄（接在 `AttackScale` 後面）**
  （2026-09-18，作者要求）：原本刻意不開這一欄（理由：jump 不該吃自動高度對齊），
  但那個理由只反對「**留空時走自動**」，不反對「手填覆寫」——手填永遠是作者看畫面決定的，比演算法準。
  所以欄位開了，**留空的語義維持原樣＝沿用 `IdleScale`、不做自動對齊**（與 Idle/Walk/AttackScale 三欄不同，
  那三欄留空是走自動）。踐踏兩欄的索引順延到 27/28。
  改 `MonsterData`／`MonsterSpawner`（讀表）／`MonsterAnimator.Setup`（多收一個參數）／`MonsterController`（傳入）。

* [x] **修「地面龜裂畫在怪物胸口而不是腳底」＋ 補上怪物版的身體幾何 API（⏳ 未編譯未實測）**
  （2026-09-18，見 [PROBLEMS.md](PROBLEMS.md) **G13**）：作者回報跳躍落地的龜裂出現在怪的胸口。<br>
  ⭐ **根因：route B 怪物的 sprite pivot 是「畫布中心」，不是腳底——與玩家相反。**
  `MonsterSpriteLibrary.GetFrames` 的「腳底對齊」只把**各動作之間**拉到同一條線，
  **基準幀（idle）的 pivot 刻意維持 0.5**（為了讓角色的絕對位置與碰撞框完全不動）。
  所以 `transform.position` ＝ 畫布中心，離腳底有半個可見身高。
  實測：idle 可見底邊在畫布下緣往上 28px、pivot 在 128px ⇒ 差 100px，
  換算 PPU 102 再乘 Scale 1.3 ＝ **腳底在 transform 下方 1.27 世界單位**（可見高 2.54）⇒ 正好是胸口。<br>
  補上怪物版的 `FeetWorldPos` / `BodyCenterWorldPos` / `VisibleBodyHeight`（仿玩家那三個，
  由 `FitVisibleBoxCollider` 量可見框時一併算好，走後備路徑時退回碰撞框底邊），
  裂痕、踐踏傷害圈、揮擊傷害圈三處都改對準腳底——順便讓「看到的裂痕範圍」就是「打得到的範圍」。<br>
  ⭐ **通則：寫任何「掛在角色身上／腳下」的東西之前，先確認那個角色的 pivot 在哪裡。**
  玩家的 pivot 在腳底，所以「用 transform 當腳底」在玩家身上是對的，搬到怪身上就整個偏掉半個身高。
  這是玩家版 **E14** 的怪物版，而且更隱蔽。<br>
  ⚠ **另一個陷阱：`BlobShadow` 沒有這個問題**（它走影子錨點，本來就把 pivot 換算進去了）⇒
  「影子是對的、特效是歪的」完全可能同時出現，**不要因為影子看起來對就以為 transform 是腳底**。

* [x] **龜裂 shader 重寫：放射狀爆裂 → Voronoi 均勻泥塊（⏳ 未編譯未實測）**
  （2026-09-18，見 [BOSS_MODULE.md](BOSS_MODULE.md) §9.5）：作者附乾裂泥地的照片
  「我想要的是**平均**的那種龜裂，不是你做的這種**中間有個洞**的」。<br>
  ⭐ **這是「選錯演算法」不是「參數沒調好」**：第一版是「中心碎坑 ＋ N 條主裂往外竄 ＋ 分支」——
  那是**玻璃／衝擊波**的裂法（有中心、有方向性）；而乾裂泥地是**整片均勻的不規則多邊形**
  （泥塊各自收縮拉開，沒有中心）。兩者在演算法上沒有交集，所以整支重寫。
  **通則：先確認要模擬的是哪一種物理現象，再選演算法——「裂痕」不是一個圖樣，是好幾個。**<br>
  新作法：**jittered grid Voronoi**，用 F2−F1 畫出泥塊之間的交界，再疊一層更密更細的二級裂。
  三塊交會的頂點處 F2−F1 天然會寬一點，剛好就是泥裂那種 Y 型節點，不必另外做。<br>
  ⚠ **邊界不能拿正圓去淡出**：裂縫線本來就會穿過圓周，每條被切斷的裂縫都會留下一個漸層的三角形殘影
  ⇒ **整圈長出尖刺，像海膽**（第一次預覽就是這樣）。改成以**泥塊為單位**抖動半徑
  （整塊一起進、一起出），邊界變成自然的鋸齒，擴散時也變成「一塊一塊裂開」。<br>
  ⚠ 翻土亮邊 `_RimAmount` 從 0.9 降到 **0.3**：第一版只有 7 條主裂撐得住，密集的 Voronoi 下
  每條縫都鑲一圈亮邊 ⇒ 整片看起來在發光。<br>
  ⭐ **交件前先用 python（numpy）把同一套演算法跑成 PNG 給作者看**，不必等 Unity 編譯——
  上面那兩個問題（尖刺、發光）都是在預覽圖上發現並修掉的，省掉兩輪來回。
  **這招對任何程序化 shader 都適用，值得變成習慣。**

* [x] **跳躍踐踏加「魄力」：落地定格（hit stop）＋ 下墜殘影（⏳ 未編譯未實測）**
  （2026-09-18，見 [BOSS_MODULE.md](BOSS_MODULE.md) §9.6b／§9.6c）：作者「還是感受不到魄力，
  有可能在角色旁邊加上速度線一類的嗎？」<br>
  ⭐ **高度與速度到位之後，剩下的魄力幾乎不在角色身上，而在「畫面有沒有反應」**——
  原本砸到地的那一刻，除了地上多一圈裂痕，整個畫面什麼事都沒發生。<br>
  **落地定格**（新 `Scripts/Combat/HitStop.cs`）：砸到地那一瞬間把 `timeScale` 壓到 0.05、0.06 秒再彈回。
  **同樣的動畫，加了這 0.06 秒就從「碰到地面」變成「砸到地面」**，是這次兩層裡效果最大的。<br>
  ⚠ **定格絕對不能用協程**：會呼叫它的都是「打到人的那個東西」（怪、一次性傷害圈），
  **很可能在定格結束前就被銷毀**（怪被反殺、傷害圈 0.12 秒自毀）⇒ 協程中斷 ⇒
  `timeScale` 永遠卡在 0.05，整個遊戲變慢動作。改用 `DontDestroyOnLoad` 的常駐載體以 `unscaledDeltaTime` 倒數。
  **通則：任何「設了某個全域狀態、之後要還原」的計時，載體必須比呼叫者長命。**<br>
  ⚠ **還原時只還原「自己設的那個值」**：專案用 `timeScale = 0` 當暫停，定格期間玩家開了背包還無條件寫回 1，
  **會把暫停解除掉**。被別人改過就放手。<br>
  **下墜殘影**（新 `Scripts/Afterimage.cs`）：下降段每 0.035 秒留一個半透明剪影、0.22 秒平方衰減淡出
  ⇒ 一條幾乎垂直的殘影柱（約 11 個）。只在**下降段**留——上升是「躍起」、下降才是「砸落」，
  全程都留會變成一條沒有重點的長尾巴。<br>
  ⭐ **作者問的是速度線，我建議換成殘影**：俯視角 2D 的角色是「往畫面下方掉」，放射狀速度線得畫成
  角色上方的拖尾，可讀性遠不如橫向捲軸或 3D；而且速度線是偏卡通的語彙，跟本專案陰暗寫實的美術會打架。
  **殘影傳達同一件事，而且它就是角色自己的剪影，風格上永遠不會出戲。**<br>
  **還沒做、但機制都現成、各只要一兩行的兩層**（作者這次沒選，記在 §9.6d）：
  `MapCameraController.AddShake`（API 已存在，血統變身在用）、
  VfxTable **34 泰坦・踏地衝擊環**＋**35 踏地揚塵**（現成素材，`SortingOrder` 已經是 8 ＝與裂痕同層畫在腳下）。

* [x] **修「調大怪物 Scale 之後近戰就砍不到玩家」——攻擊判定不要自己算圈（⏳ 未編譯未實測）**
  （2026-09-18，見 [PROBLEMS.md](PROBLEMS.md) **F27**）：作者把 `Scale` 調到 1.5 後回報砍不到，
  並且問「**不能因為我調大小就得再調整攻擊範圍啊？這太怪了**」——這個直覺完全正確，那是設計錯誤。<br>
  ⭐ **兩件事疊在一起**：<br>
  ① **寫死的世界單位**：`揮擊圈半徑 = 框半寬 + 0.55`。怪放大時**劍也跟著放大**，但那個 0.55 不會 ⇒ 相對變短。<br>
  ② **兩邊的垂直基準不一致，而落差隨 Scale 線性長大**（致命的是這條）：圈心用**怪的腳底**，
  而**玩家的 `CircleCollider2D` offset 是 0 ⇒ 圓心就在牠的 transform**。route B 怪物 pivot 在畫布中心（**G13**），
  腳底離 transform 半個身高，那個差距 **∝ Scale**。實測圈邊緣離玩家的圓還差多少才碰到：
  **Scale 1.3 ⇒ 0.27、1.5 ⇒ 0.23、2.0 ⇒ 0.12** ⇒ **Scale 就是那個隱形的開關**。<br>
  **修法**：命中判定**不要自己算圈**，改成問「兩個碰撞框的邊緣距離」（`Physics2D.Distance`），
  而且與「決定出手」用**同一個函式**；判定過了才在**目標的碰撞框中心**開一個半徑 0.2 的小圈 ⇒ 必中。
  踐踏那種真 AOE 無法這樣做，圓心一律改用**怪的 transform**（與玩家圓同基準），不再用腳底。
  裂痕與揚塵**仍然畫在腳底**——視覺歸視覺、判定歸判定，兩者刻意不一致。<br>
  ⭐ **通則一：「決定出手」與「判定命中」必須用同一把尺。** 各自獨立設定就沒有任何東西保證後者涵蓋前者，
  而且**在某一個體型下剛好會過、換一個體型就不會**。（§10.2 的「停止移動門檻必須等於傷害判定門檻」是同一條，
  這是第二次踩。）<br>
  ⭐ **通則二：凡是「以某點為圓心開範圍」的判定，先問「對方的碰撞框是以什麼為基準」。**
  這個專案玩家 pivot 在腳底、route B 怪物 pivot 在畫布中心，**兩邊 transform 的語意不同**；
  拿其中一邊的「視覺位置」去跟另一邊的「碰撞框」比，誤差會隨體型放大。

* [x] **近戰正式分成「衝撞型 / 揮舞型」兩種；狼人兵與吸血鬼兵改用揮舞型（⏳ 未編譯未實測）**
  （2026-09-18，作者拍板，見 [BOSS_MODULE.md](BOSS_MODULE.md) **§10.0**）：作者測 ID 16 狼人兵時發現
  牠也會「舉著攻擊動作還在移動」，於是把近戰的模式定義下來——
  **衝撞型**（紅嫁衣的家人幽靈那種，一路貼上去磨）＝`Chase`；
  **揮舞型**（狂族皇家衛士那種，追到定點站定把攻擊動畫做完）＝`MeleeChase`。<br>
  ⭐ **判準就是「有沒有 attack 圖」**，因為那正好是「這隻怪有沒有揮武器這個動作」的定義。
  對照現況完全吻合：ZhaYu 與 12 隻家人幽靈沒有 attack 圖（天生衝撞型）、
  吸血鬼兵 25 張／狼人兵 24 張／皇家衛士 12 張有（揮舞型）。ID 15、16 改成 `MeleeChase`。<br>
  ⚠ **順手修掉一個會讓怪完全無害的陷阱**：`MeleeChaseBrain` 會把「碰到就痛」關掉、改由揮擊命中幀結算，
  **但沒有 attack 圖就永遠不會揮** ⇒ 那隻怪一點傷害都沒有，而且不會有任何錯誤訊息。
  現在 `EnsureConfigured` 偵測到沒圖就**整段退回衝撞型並印 warning**。
  **通則：一個「把既有機制關掉、換成新機制」的模組，必須先確認新機制的前提成立，否則要能自己退回去。**<br>
  ⭐⭐ **命中幀從寫死的常數搬進 CSV（`AttackHitFrame`）**，因為量完三隻才發現**比例完全靠不住**：
  皇家衛士 9/12（**75%**）、狼人兵 8/24（**33%**）、吸血鬼兵 11/25（**44%**）。
  任何「乘個比例」的猜法都會在某一隻上完全錯掉。
  量法：印 attack 每幀不透明像素的 bbox，**寬度／邊緣突然暴增的那一幀**就是武器揮出去的時刻
  （一行 python + PIL，比在 Unity 裡一張張點快得多）。留空退回張數×0.7 只是讓沒量過的新怪能動。<br>
  **已知**：狼人兵其實是左右兩段連擊（幀 8 與 15），目前只認第一段；要連擊得讓 `AttackHitFrame` 可填多個。

* [x] **開場第一次進邪佛廣場不再播卍字進場與場景名（⏳ 未編譯未實測）**
  （2026-09-18，見 [SCENE_TIP.md](SCENE_TIP.md) **§0.1b**）：作者回報第一次從初始洞窟進大廳時，
  **邪佛的開場劇情對話、卍字進場、「邪佛廣場」場景 tip 三件事同時演**。<br>
  那一刻玩家已經被開場劇情接手，不該再疊一段「把主角從天上送下來」。所以那一次兩樣都不播，
  之後每一次回廣場（過關／死亡／讀檔）照常。<br>
  ⭐ **判準用存檔既有的 `hubIntroSpawnDone`**，沒有新 CSV 欄位、沒有新旗標——
  那個旗標本來就是為了「第一次進廣場走洞穴出口、之後走中央」而存在的，語意完全吻合。
  **通則：要判斷「是不是第一次做某件事」之前，先找找專案裡有沒有人已經在記這件事了。**
  再記一份的代價不是多一個欄位，是多一份**會漂移**的真相。<br>
  實作仿 `_wakeUpWanted` 的老模式（`PlaceAndSetup` 記、`FireEnterTriggersRoutine` 消化）。兩個要點：
  ⚠ **每次進圖都重新賦值**而不是「只在成立時設 true」——漏掉 else 旗標會殘留到下一張圖，
  而且只在特定進圖順序下重現；⚠ **場景說明的名額照樣佔掉**（Add 照跑、只是不 Show），
  因為開場玩家可能走回洞窟再繞回廣場，那還在同一段開場敘事裡。

* [x] **ZhaYu 系列四隻新怪上線：衝撞／揮舞／射手／自爆，新增自爆模組與「血魔重炮」（⏳ 未編譯未實測）**
  （2026-09-22，見 [BOSS_MODULE.md](BOSS_MODULE.md) **§11**）：作者丟進 `GameAssets/Main/Monsters/SequenceImage/`
  四套 DarkMonster 素材。⭐ **四隻裡只有一隻要寫新程式**，其餘三隻是「既有 Brain ＋ 填一列 CSV」：<br>
  ‧ **ZhaYu**（ID 1，idle/walk 各 21 張）＝**換外觀**，不是新怪。舊圖（idle 1 張、walk 8 張）已刪。
    只改了 `AnimFPS` 8 → 14 —— **張數換了就要一起換 fps**，否則 21÷8 ＝ 2.6 秒一循環會變慢動作。<br>
  ‧ **ZhaYu_HugeSword**（ID 20）→ `MeleeChase` 揮舞型。`AttackHitFrame=15`。<br>
  ‧ **ZhaYu_Gun**（ID 21）→ `Archer` 射手型，拿新武器 34。`ReleaseFrame=9`。<br>
  ‧ **ZhaYu_Bomb**（ID 22）→ 新的 `SuicideBomb` 自爆型。<br>
  ⭐⭐ **`ArcherBrain` 的放彈幀從全域常數搬進 CSV（`ReleaseFrame`，留空＝14 ⇒ 狂族弩手零變化）**：
  這一步不是為了大槍才做的，是因為**放彈幀是「那套 attack 序列圖的性質」，不是所有射手共用的手感**。
  弩手 25 張、第 14 幀才「舉定穩住」；大槍只有 22 張、**幀 6 就完全水平舉定、6~22 一動也不動**，
  沿用 14 會晚半秒才吐炮彈。這與 `AttackHitFrame` 當初搬進 CSV 是同一個道理
  （**通則：凡是「第幾幀」這種綁在素材上的數字，都該住在資料表，不該住在共用模組的常數區**）。<br>
  ⭐ **兩隻怪的幀都是量出來的，不是猜的**（量法同 §10.4：印每幀不透明像素 bbox ＋ 目視對照）：
  巨劍兵 8~13 是**把劍舉在頭上蓄力**（bbox 寬反而變窄）、14 開始下劈、**15~16 掃到最遠**、17 之後拖在地上
  ⇒ 取 15；大槍 1 垂下、3~5 抬起、**6 舉定後 bbox 完全不再變化** ⇒ 取 9（舉定後穩住三幀才射，
  同 §8.2b「放的時機寧可比動作到位再晚一兩幀」）。
  ⚠ **只看 bbox 會選錯**：巨劍兵幀 9~12 的寬度暴增其實是「把劍舉高」而不是「揮出去」，
  要配一張併排縮圖用眼睛確認——**寬度暴增是線索，不是答案**。<br>
  ⭐ **新武器「血魔重炮」（武器 34／配方 46／道具 34）**：`Mode` 留空＝Normal ——
  那是目前 `WeaponCastService` 唯一搬給怪物用的模式（§8.4），所以怪拿它開火才會通。
  炮彈 `Speed 12`（比弩矢 15 慢，要有重量感）、`Radius 0.25`（比預設 0.1 粗）、擊中接 VfxTable 1 爆炸。
  ⚠ `SpriteAngleOffset` 填 **360 不是 0**（0 ＝不旋轉，炮彈會永遠朝右，PROBLEMS **E38**）。
  玩家也拿得到（照常進 ItemTable，可鑲珠可掉落）。<br>
  **`SuicideBombBrain` 的三個設計決定**：<br>
  ① **進了引信就一定會爆**——`Fuse` 階段沒有任何中途取消，這是照抄 §8 射手型的鐵則。
  一隻「嗶嗶叫兩聲又把引信收回去」的炸彈怪，畫面上看起來就是壞掉的怪。<br>
  ② **被玩家打死不會爆**（作者拍板）：爆炸只發生在引信燒完那一刻，而怪一死 `Update` 就提前 return
  ⇒ **這是天生行為，一行特例都不用寫**。「搶在引信燒完前拆掉它」因此成為真正的操作空間。<br>
  ③ **接觸傷害關掉**（同 `MeleeChaseBrain`）：自爆怪擦過你身邊不該扣血，牠的全部威脅就是那一下。<br>
  ⚠⚠ **自毀不能用 `TakeDamage(超大數字)`**：那條路會先問 `HitReactionHandler`，怪正在無敵幀內就一滴血都不扣
  ⇒ **「爆炸放了、怪沒死」**，而 Brain 已經進了終結狀態 ⇒ 牠會站在原地不動、不再追人也不再爆。
  目前 `InvincibleTimeMs` 填 0 碰不到，但那是 CSV 隨時能改的值。
  所以 `MonsterController` 加了一個 **`Kill()`**（直接走 `Die()`，略過受擊反應，死亡流程照跑）。
  **通則：一個「一定要成功」的自毀，不該把成敗交給另一條會拒絕它的路徑**（同 PROBLEMS **F19**
  「無敵時間是一份隱形的行為預算」）。<br>
  ⭐ **判定圈用怪的 `transform`、視覺用腳底**——兩者刻意不同，理由同 §9.4／PROBLEMS **F27**：
  玩家的碰撞圓 offset 是 0（圓心＝transform），而 route B 怪物 pivot 在畫布中心、腳底差半個身高，
  **那個落差隨 `Scale` 線性長大** ⇒ 判定圈以腳底為心的話，怪一放大就炸不到人。<br>
  自爆特效從特效庫挑 `expfx1_epic_explosion_A` 紅色 14 幀 → `Resources/VfxEffects/SuicideExplosion/`、
  VfxTable **ID 42**。火球大小**依實際半徑等比縮放**（`extraScale = BombRadius ÷ 1.8`），
  所以 CSV 調大殺傷半徑時畫面會跟著變大，不會出現「炸得到卻看不出來」。<br>
  **CSV 表尾新增四欄**：`ReleaseFrame`／`BombDamage`／`BombRadius`／`BombFuse`，既有 19 列全部留空（退路值接手）。<br>
  ⏳ **還沒做**：Unity 內跑 `Project Tools → Sync Map Assets`（新圖還沒進 catalog，目前 catalog 裡的 ZhaYu
  仍是舊的 1＋8 張）；影子錨點（`Project Tools → 角色 → 計算影子錨點`）；把四隻擺進地圖出生點實測。

* [x] **修 ZhaYu「走路時瞬移貼到玩家身上、對話框離圖很遠」——腳底對齊把兩張不同畫布的像素混算（⏳ 未編譯未實測）**
  （2026-09-22，見 [PROBLEMS.md](PROBLEMS.md) **F28**）：作者換了一批 walk 圖之後，ZhaYu 變成
  「走過來→快到時整隻跨一段距離貼到我身上→動作變 idle→扣血→又退回去」反覆震盪，頭上對話框也離圖很遠。<br>
  ⭐ **根因**：`MapSpriteLoader.GetAnimationFrames` 的 pivot 補償公式裡，
  `(baselineBottomPx − bp.y × 0.5)` 這一項要算的是「**基準幀**的腳底離**基準幀**畫布中心多遠」，
  但 `bp.y` 是**當前這一幀**的畫布高 ⇒ **兩張不同畫布的像素被混在一起相減**。
  全部動作同尺寸時剛好等價、**靜默算對**，所以藏了很久；
  ZhaYu 的 idle 是 **256px**、walk 換成 **500px** 之後就爆了——
  walk 的 pivot 被算成 **1.0369**（跑到畫布上緣外），整組 walk 幀**往下位移 1.15 世界單位**（×Scale 1.2 ＝ **1.38**）。<br>
  **三個症狀是同一件事**：走路時圖在下、停下切 idle 彈回上 ⇒「瞬移又退回去」；
  碰撞框用 idle 建的、固定在 transform，圖卻跑掉 ⇒ **看到的距離不是真實距離**
  （這也是更早那次「圖還沒碰到就被撞」的真正原因）；對話框掛 `FeetWorldPos + VisibleBodyHeight`，圖偏走了它沒有。<br>
  **解法**：基準的「腳底 px」與「畫布高 px」**必須成對傳、量自同一張圖**——
  `GetAnimationFrames` 多收 `baselineCanvasPx`，`BaselineBottomPx` 改回傳 `Vector2Int(腳底px, 畫布高px)`
  （`GetFrameBottomPx` 本來就兩個都給了，舊版把畫布高丟掉）。
  實測：ZhaYu idle/walk 腳底位移都是 −0.9891（對齊），
  **其餘三隻與所有既有怪（全 256px）新舊 pivot 完全相同 ⇒ 零行為變化**。<br>
  ⚠ **這個坑 [MONSTER_SETUP.md](MONSTER_SETUP.md) 早就預告過**：「不能假設畫布是 256px……
  漏除會**靜默算對**，哪天丟一張 512px 的進來才會爆」。那一天就是今天。<br>
  ⭐⭐ **繞路兩輪的教訓（記在這裡）**：症狀全部長得像碰撞問題，於是先後改了兩版**共用的碰撞判定**
  （接觸框改腳底框、`ChaseBrain` 改邊緣距離），第一版還讓**全場的怪都碰不到玩家**（框貼在「視覺腳底」＝
  沉到 transform 下方半個身高），兩輪都沒修好，最後**全部回退**。
  真正的線索一直都在作者那句話裡：「**紅嫁衣那邊的鬼魂一樣是衝撞型，完全正常**」——
  同樣的 Brain、同樣的碰撞設計，差別只在素材。
  **通則：兩個東西長得一樣、只有一個壞，先比對素材差異，不要先改共用系統。**
  當時只要 `ls` 一下兩邊的畫布尺寸，五秒就會看到 500 vs 256。<br>
  順手：ZhaYu 的 walk 換成 8 張 ⇒ `AnimFPS` 14 → **9**（walk 0.89 秒／idle 2.33 秒一循環）。

* [x] **`WalkScale`／`AttackScale` 改成「在自動對齊之上再乘」——填 1.1 不再反而變小（⏳ 未編譯未實測）**
  （2026-09-22，作者拍板，見 [PROBLEMS.md](PROBLEMS.md) **G14**）：作者覺得 ZhaYu_HugeSword 攻擊的圖偏小，
  把 `AttackScale` 填 1.1，**結果反而更小**。<br>
  ⭐ **根因**：舊版手填是「**覆寫**自動高度對齊」（`tile = baseTile × manualScale`），而
  **自動倍率本來就不是 1**——它要把該動作的可見高拉到與 idle 一致，而攻擊／奔跑姿勢通常**比站姿矮**
  （前傾、壓低、武器往側邊掃）⇒ 倍率多半 > 1。實測 ZhaYu_HugeSword 的 attack 是 **1.252**、
  戰狼 walk **1.336**、皇家衛士 jump 1.138。**手填只要小於自動倍率就會縮**，而且沒有任何錯誤訊息。<br>
  作者的心智模型才是對的：「`Scale` 欄是基準，後面每個動作的 scale 都是**在它之上**再調整」。
  現在 `Tile()` 改成 `自動對齊結果 × manualScale` ⇒ **1.0 ＝ 跟留空一樣、1.1 ＝ 比平常大一成**。
  `jump` 的基準同步改成 `idleTile`（它刻意不走自動對齊，見 BOSS_MODULE §9.7）。<br>
  ⚠ **既有手填值一併換算**（新值 ＝ 舊值 ÷ 自動倍率），逐隻驗算顯示高度全部維持不變：
  戰狼 `WalkScale` **0.98 → 0.733**；`IdleScale`（自動倍率恆為 1）與 `JumpScale`（不走自動對齊）**原值保留**
  ⇒ 狼人兵／狂族弩手 1.1、皇家衛士 1.2 都不動。ZhaYu_HugeSword 的 `AttackScale` 維持 1.1，
  新語意下正好是作者要的「比平常大一成」（顯示高 2.340 → 2.574）。<br>
  順手：作者換過 ZhaYu_HugeSword 的 attack 圖（`custom_cast` → `attack_right`，仍 25 張），
  **命中幀重量**：新圖 11 高舉過頭、**12~13 劍掃到最遠**（bbox 右緣觸邊、寬度最大 196）⇒ `AttackHitFrame` **15 → 12**。<br>
  **通則：一個「留空＝自動、有填＝手動」的欄位，手填值要嘛乘在自動結果上，要嘛就別叫同一個名字**——
  「覆寫」型語意讓人永遠算不準該填多少，因為他得先知道演算法算出來是幾倍，
  而那個數字在 CSV 裡看不到、在遊戲裡也看不到。

* [x] **ZhaYu_Gun 手感微調：放彈幀 9 → 14、炮彈擊中爆炸縮一半（⏳ 未編譯未實測）**
  （2026-09-22，作者實機回報）：① 放彈太早——作者指定是 `attack_14` 那張槍才真的出膛，
  `ReleaseFrame` **9 → 14**（22 張 @14fps ⇒ 0.93 秒後放彈）。
  這一欄就是為了逐怪可調才搬進 CSV 的（見 PROBLEMS **F28** 上面那段與 §8.2b），改 CSV 即可、不動程式。<br>
  ② 擊中爆炸範圍太誇張：原本接 VfxTable **ID 1「爆炸」**，圖是 500×500 @PPU100 ⇒ Scale 1 就是
  **5 個世界單位**，而畫面高才 10 ⇒ 半個畫面都是火球。<br>
  ⚠ **沒有直接改 ID 1 的 Scale**——武器 4「火焰拋擲彈」也在用它，那把沒問題、不該被連動。
  改成**新增 VfxTable ID 43「炮彈爆炸(小)」**（同一組圖、`Scale` 0.5 ＝ 2.5 世界單位），
  武器 34 的 `HitEffectID` 1 → 43。
  **通則：調某把武器的特效大小之前先查那個 VfxTable ID 還有誰在用——共用的就複製一列改，不要就地改。**

* [x] **從根源修「怪一放大，範圍技就打不到人」——AOE 半徑改成隨體型縮放＋防呆下限（⏳ 未編譯未實測）**
  （2026-09-22，作者要求「這個雷之前踩過，請從根源修正」，見 [PROBLEMS.md](PROBLEMS.md) **F29**）：
  `ZhaYu_Bomb` 體型 1 正常，調到 1.5 就「引信照點、火球照炸、玩家完全不掉血」。<br>
  ⭐ **根因是 F27 的第一條死因換個地方又出現**：觸發與殺傷用了**兩把不同步的尺**——
  引信走碰撞框的**邊緣距離**（框隨 `Scale` 變大 ⇒ 觸發時的中心距離跟著變遠），
  殺傷卻是直接拿 CSV 的 `BombRadius` 當世界單位（**完全不隨體型變**）。
  實測（對角最壞情況）：`Scale` 1.0 觸發 2.36／殺傷 2.30（**早就在臨界**）、1.5 → 3.11／2.30、2.0 → 3.86／2.30。<br>
  **解法兩層，缺一不可**：<br>
  ① `MonsterController.ScaledRadius()` ——**怪的所有 AOE 半徑一律走它**，CSV 的半徑欄語意變成
  「**體型 1 時的半徑**」。特效倍率吃縮放後的半徑 ⇒ 火球跟著變大，不會「炸得到卻看不出來」。<br>
  ② **防呆下限**「引信點著就一定炸得到」：`radius ≥ 自身框半對角 ＋ FuseSlack`。
  只做①還會**從另一端破**——引信距離裡有一截固定量（`FuseSlack`）不隨體型縮，
  體型小到 `Scale` < 0.47 同一個坑會反向出現。
  ⭐ 這條式子**不必知道玩家多大**：玩家半徑在觸發與殺傷兩邊都有、自動抵銷。<br>
  驗算七種體型（0.5～3.0）全部保證命中。<br>
  ⚠ **順手修掉同型的未爆彈**：`LeapSlamBrain` 的 `LeapRadius` 是一樣的寫法，
  狂族皇家衛士（`Scale` 1.5）的踐踏範圍其實一直相對縮水。現在 1.6 → **2.4**；
  想維持原樣就在 `LeapRadius` 填 1.067（＝1.6 ÷ 1.5）。<br>
  ⭐⭐ **最該記的一條**：F27 修近戰揮擊時**明講「範圍 AOE 沒辦法這樣做」**，
  那句話等於在 AOE 這邊留了一個洞，這次就是從那個洞掉下去的。
  **修一類 bug 時，被自己排除掉的那個例外要當場補上**，否則它只是在等下一個體型。

* [x] **自爆怪：引信改成「真的走到身邊」才點、引信視覺改走 shader 逐漸燒紅＋脈動加速（⏳ 未編譯未實測）**
  （2026-09-22，作者回報「離玩家還很遠就停下來爆炸」＋想要「逐漸變紅、滴滴滴、然後爆炸」的感覺）<br>
  ⭐ **距離的根因不是爆炸範圍太大（作者的猜想），是引信用了框對框的邊緣距離**：
  身體框貼合整個可見身體 ⇒ **高度就是身高**，而俯視角的 Y 軸同時是地面深度
  ⇒ 玩家從上／下方接近時，離怪還有**一個半身位**就已經算「碰到框」。
  改成**只取水平半徑的圓形距離**（`兩者 bounds.extents.x 之和 ＋ FuseSlack`）：各方向門檻一致、
  不受這隻怪畫得多高影響，而且半徑取自碰撞框（已含 `Scale`）⇒ 體型變大時門檻自動跟著長（不重蹈 F29）。
  `FuseSlack` 0.35 → **0.05**。實測 `Scale` 1.5：水平 2.43／垂直 2.46 → **一律 2.13**，
  此時怪的「圖」邊緣離玩家碰撞圓只剩 0.20 ⇒ 視覺上就是走到身邊才點火。
  防呆下限同步改成 `radius ≥ 自身水平半徑 ＋ FuseSlack`。<br>
  ⭐⭐ **引信視覺改走 shader 的加法發光**（`Custom/SpriteFlash` 新增 `_ChargeAmount`／`_ChargeColor`，
  預設 0 ＝ 那一行不執行、其他角色逐位元無變化），由 **`HitReactionHandler.SetCharge()`** 寫入。
  底光隨進度爬升（越燒越紅）＋ 疊一層**方波**脈動、週期 0.22 → 0.05 秒（「滴、滴、滴」的斷點感，
  用 sin 會變成平滑呼吸、沒有緊迫性）。<br>
  兩個理由都是踩過才知道的：① 這些怪本體幾乎**全黑**，乘法 tint（黑 × 紅 ＝ 黑）**根本看不出來**，
  只有加亮才會「燒起來」；② `SpriteRenderer.color` 會被**受擊無敵閃爍**改成半透明再還原
  ⇒ 引信顏色會被一起洗掉。而且 `HitReactionHandler.ApplyPropertyBlock` 是那個 renderer
  **唯一的 MPB 寫入點**（`SetPropertyBlock` 是整包覆蓋的），自己另開一條會把環境融合參數沖掉。
  **通則：要在角色身上加 shader 效果，一律從既有的那個唯一入口加，不要另開一條路。**<br>
  ⚠ 順手讓受擊**不會**清掉充能發光（`ResetVisuals` 刻意不碰 `_chargeAmount`）——
  引信點著後連挨打都不能打斷它。<br>
  ⭐ **鐵則逐項複查**（作者特別要求）：玩家跑掉 ❌／玩家不見了 ❌（引爆不讀 target）／
  被擊退 ❌（擊退窗口跳過 `Think()`，但引信走 `Time.time`、窗口結束第一幀就爆）／被打沒死 ❌
  ／**被打死 ✅＝唯一例外**（留給玩家的拆彈窗口）。列表寫進 BOSS_MODULE §11.2。<br>
  ⏳ **「滴滴滴」目前只有視覺**：專案還沒有音效系統，要真的有聲音得先做一套。

* [x] **波次刷怪（總波數／波次群組／全滅接鏈）＋ 掉落表資料化（⏳ 未編譯未實測）**
  （2026-09-22，為「新手夢境教學」在邪佛廣場做吸血鬼倖存者式湧怪。見
  [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) **§3.5b**、[RUN_PROGRESS.md](RUN_PROGRESS.md)〈掉寶〉）<br>
  ⭐ **八成的東西本來就在**：`MapMonsterRespawner` 為了「同時存在上限」**早就在追蹤每個出生點生的怪還活著哪些**，
  全滅偵測直接掛在那份名單上。新增的只有兩個 CSV 欄位：`maxWaves`（總波數，留空＝無限＝舊行為）與
  `waveGroup`（波次群組，留空＝自己一組）。<br>
  ⭐⭐ **「全滅之後做什麼」沒有開新欄位**：出生點本來就有通用的 `接續觸發`／`完成寫旗標`，
  這次只是替它定義了「什麼叫完成」——**這一組怪被清空的那一刻**，然後走既有的 `TriggerChain.OnCompleted`。
  順帶讓**一次性出生點**也能收尾 ⇒「把這房間的怪殺光就開門」零程式就能編。
  （代價：有填鏈的一次性出生點得改走 respawner 才追蹤得到存活，`MapLoader` 多判一個 `wantsClearChain`。）<br>
  ⚠ **一隻都沒生出來的那一波不計數**（作者拍板）：否則玩家躲著不打時，波數會空轉跑完、
  **一隻怪都沒出現就宣告全滅**。清場不會誤判——`IsLoading`／`IsEndingLevel` 期間 `Update` 整段提前 return。<br>
  ⭐ **掉落表**（作者要求）：`DropRunLoot()` 的註解本來就寫著「暫定掉寶，正式掉寶公式之後換」，這次換掉。
  新增 `DropTable.csv`（一列一張表、8 個**獨立**掉落槽，格式 `itemId:機率%:數量`）＋ `DropTable.cs` ＋ `DropTableProvider`，
  `MonsterData.csv` 加 `DropTableId` 引用。<br>
  ⚠ **留空＝完全不掉寶**（作者拍板），所以導入時**既有 22 隻怪一律填 1**＝表 ID 1「一般小怪」
  ＝原本寫死的那組（銅錢 1~5、血瓶 17.5%、魔瓶 17.5%）⇒ **零行為變化**（20 萬次模擬比對過分布）。
  `lootMoneyMin/Max`／`lootPotionChance` 三個 Inspector 欄位從此沒人讀，留著只為不動既有 prefab 的序列化資料。<br>
  另外新增**夢境專用怪 ID 30~33**（ZhaYu 四種的低血量版，`DropTableId` 留空＝不掉寶，圖沿用同一批＝零素材成本）。<br>
  ⏳ **需要 Unity 接線一步**：把 `Assets/Data/DropTable.csv` 拖進 GameManagers 上的 `DropTableProvider`。
  沒掛會印 warning 並讓所有怪不掉寶（不會靜靜壞掉）。

* [x] **邪佛手掌的滾滾沙塵：通用「移動拖尾特效」（⏳ 未編譯未實測）**
  （2026-09-22，作者要「手掌在地上緩慢拖著沙塵壓過來」的壓迫感）<br>
  **選素材**：把特效庫的候選按**實際比例**（`Buddha_Hand` 是 Scale 3 ⇒ 顯示高 5.85 世界單位，畫面高才 10）
  疊到手掌圖上做對照，作者選了 `Smoke Bursts/directional_smoke_burst_002/gray`（厚實的翻騰煙團），
  **不要**貼地的捲曲塵浪（`fx1_impact_dust`）——那個是一次性衝擊的對稱造型，拖行時讀起來像裝飾花紋。
  ⭐ **按實際比例合成預覽比看素材縮圖準得多**：單看縮圖時捲曲塵浪很漂亮，疊上去才發現它被手掌的體積壓過去。<br>
  素材複製進 `Resources/VfxEffects/DustTrail/`（18 幀）並**先壓暗去飽和**（`x0.62`＋去一半飽和）——
  特效庫素材偏亮，暗黑場景直接用會像貼紙（EFFECT_LIBRARY 的建議）。VfxTable 新增 **ID 44「拖行沙塵(重物)」**，
  `SortingOrder` 填 **8**（< 角色的 10）⇒ 沙塵沉在腳下、不會蓋住手掌。<br>
  **做成通用功能**：新元件 `MonsterMoveTrail` ＋ CSV 表尾欄 `MoveTrailFx`
  （格式 `vfxId:大小倍率:每秒幾個`，多層用 `|` 分隔；留空＝不掛，既有怪零影響）。
  `Buddha_Hand` 填 `44:1:5|44:0.55:3`（主層＋一層小的錯開，兩層相位隨機錯開才不會同時冒出來像一團）。<br>
  ⭐ **關鍵是「種在身後」不是「種在腳下」**：拖尾要讀得出**行進方向**。種正腳下的話，
  怪停著時會原地堆成一坨、移動時又跟得太緊，看起來像牠在冒煙而不是在推開地面。
  往反方向退 0.45 個身寬，那團塵就留在「牠剛剛輾過的地方」。左右交替散開 ＋ 大小/位置隨機，避免排成一條直線。<br>
  ⚠ 身寬取自**碰撞框**（已含體型）⇒ 改 `Scale` 時拖尾的散開幅度自動跟著長（同 **F29** 的通則，不再寫死世界單位）。
  位置用 `FeetWorldPos`（畫在地上的東西一律對腳底，不要用 transform ＝畫布中心，見 **G13**）。<br>
  順手清掉 `Buddha_Hand` 從 `ZhaYu_Bomb` 複製來、對 `Chase` 型無效的 `BombDamage/BombRadius/BombFuse` 三個殘留值。

* [x] **夢境佛掌收尾三件套：震退回入口＋骨牢束縛＋鏡頭拉遠（⏳ 未編譯未實測）**
  （2026-09-22，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) **§3.4b**）：手掌只有一張圖、只能從上往下壓，
  玩家站在別的位置對打會很怪 ⇒ 先把他轟回入口、綁住、鏡頭拉遠，佛掌才壓下來。
  整段在正式關卡打邪佛時可以原樣重用。<br>
  ⭐ **鏡頭拉遠是零程式**：`camZone` 的 `zoom` 本來就有，而且它**每幀查 `TriggerChain.IsActive`**。
  ⚠ 但**不能用「初始停用」**——`camZone` 是位置型，被鏈 `Activate` 到只會解鎖、**不接 next**
  ⇒ 放在鏈中間整條鏈會斷在那裡。正解是**用條件旗標**：`bindPlayer` 寫 `dreamHandPhase`，
  `camZone` 填 `requireFlag` 讀它 ⇒ 旗標一成立鏡頭就拉遠，鏈照常往下跑。
  **通則：位置型 trigger 只能當鏈的終點，要它在鏈中間生效就改用旗標。**<br>
  ⭐ **`bindPlayer` 沒有寫新的輸入鎖**：`PlayerController.Bound` 與教學的 `TutorialManager.FireOnly`
  **共用同一個 Update 分支**（鎖移動、放行開火、開火時仍依滑鼠轉身）——兩者要的行為一模一樣，
  共用就不會有「改了一邊忘了另一邊」的漂移。
  ⚠ 束縛**刻意不上 `SetExternalHold`**：那會把攻擊一起擋掉，而這裡要的正是「只能打、不能跑」。<br>
  ⭐ **`pushPlayer` 到位之後才接 next**：不等的話骨牢與鏡頭會在玩家還在半空中飛的時候就發生。
  位移用 `Rigidbody2D.MovePosition`（直接寫 transform 會跟物理打架）、曲線**先快後慢**＝被打飛的手感、
  輸入鎖用**具名** `SetExternalHold`（**D13**）。<br>
  ⚠ **循環特效一定要有人收**：骨牢是 `Loop=1`，靠 `bindPlayer(bind=0)` 收；
  忘了收也不會把玩家永久卡住——`PlayerBind.OnDisable`（換圖／死亡）會自己解綁清特效。
  **static 的狀態旗標一定要有這層保險**，否則下一場會帶著「不能移動」進去，而且完全沒有錯誤訊息。<br>
  **骨牢視覺先用暫代素材**：取 `EarthSpik2`（榕樹妖地刺）的**第 16~27 幀**——那一段高度固定 83px、
  只有微小晃動 ＝ 天然的循環段——換成骨白色存成 `VfxEffects/BoneCage/`，VfxTable **ID 45**
  （`Loop=1`、`SortingOrder 9` ＜ 角色的 10 ⇒ 骨牢在玩家身後、不擋住他自己）。
  ⭐ **先用暫代素材把整段跑起來**：這段演出的成敗在**節奏**，不在骨牢畫得多細；節奏對了再產正式圖。<br>
  佛掌 `Scale` 4 → **5**（顯示高 9.75），配 `camZone zoom 1.8`（視野 18 單位）⇒ 佛掌約佔畫面一半；
  拖尾同步加量 `44:1.3:6|44:0.7:4`（身寬從碰撞框量，散開幅度自動跟著體型長）。<br>
  ⚠⚠ **踩到一個沒有錯誤訊息的坑**：編輯器的 trigger 類型**正本是 `triggerTypes.json`**，
  `TriggerType.cs` 只在「首次無檔時」生成它 ⇒ **只改 `.cs` 的話新欄位不會出現在面板上**。
  上一輪加的 `maxWaves`／`waveGroup` 就是這樣沒進面板的，這次一起補進 json（見 MapEditor_DESIGN 的註記）。<br>
  地圖 `DreamTutorial_Square` 三份 `.dipanmap` 已同步寫入，`flags.json` 新增 `dreamHandPhase`（關卡單次）並同步到 StreamingAssets。

* [x] **修掉「夢境開場播完第一句之後一片黑」（⏳ 未編譯未實測）**
  （2026-09-22，作者回報「剛進遊戲顯示『最近，我常做奇怪的夢』，下一步就沒東西了，畫面一片黑」。
  見 [PROBLEMS.md](PROBLEMS.md) **H2**）<br>
  ⭐ **不是編譯錯誤**（`Library/ScriptAssemblies/Assembly-CSharp.dll` 比所有 `.cs` 都新＝編得過），
  也不是資料壞掉（地圖／旗標／劇情表／shader 全部比對過乾淨）。**Console 一行紅字都沒有**，
  唯一的線索是一行只有這次跑才出現的黃字：`[DreamTutorial] 等玩家換好外觀等了 15 秒還沒好，先往下播對話。`<br>
  **連鎖**：`WaitForPlayerReady` 用**同一個 15 秒上限**同時等「地圖載完」和「外觀換好」
  ⇒ 這次剛丟進一批新素材、第一次 Play 邊玩邊跑 Asset Pipeline Refresh、載圖超過 15 秒 ⇒ 超時放行
  ⇒ **在載入頁還開著時就把開場對話播出去** ⇒ 地圖載完呼叫 `TriggerChain.Setup()`，
  它會**清掉所有未結的對話完成回呼** ⇒ 玩家按完那一句、面板照常關閉、`NotifyDramaClosed()` 照常呼叫，
  **但已經沒有回呼可以叫** ⇒ `PlayDrama` 空等滿 `DramaTimeout = 120` 秒，而黑幕是流程第 5 步才撤的 ＝ 全黑兩分鐘。<br>
  **三層都補**：①`WaitForPlayerReady` **分開計時**——15 秒只在「地圖已就緒」之後才累計，
  載圖另給 `MapReadyTimeout = 90s` 的寬鬆硬保險絲；②`PlayDrama` **同時盯面板自己的開關狀態**
  （`UIManager.IsOpen<TalkPanel/DramaPanel>()`），回呼與面板狀態誰先到都算數，
  順手補上「面板沒開起來就跳過」（以前 drama 找不到 id 會空等滿 120 秒）；
  ③`TriggerChain.Setup` 清掉未結回呼時**印警告**，不再靜默吞掉。<br>
  ⭐ **通則一**：「超時就照走」的保險絲**不能把不同性質的等待綁在同一個上限裡**——
  一個是外部載入（不可控、可以很久），一個是自家狀態切換（幾幀）；綁在一起＝慢的逼快的提早放行。<br>
  ⭐ **通則二**：只有一格的完成回呼隨時會被覆蓋或清空，跨換圖／跨載入還要存活的流程**必須有第二個獨立訊號**。<br>
  ⭐ **通則三**：**全黑畫面 ＋ Console 沒紅字** ＝ 八成有協程停在 `yield` 上、而黑幕正是它負責撤的。
  先找那支流程的保險絲常數，再反推它在等誰。<br>
  ⚠ 順帶發現（**不是**這次的 bug）：`MainScene` 上 `DramaTalkTableProvider` 的 `portraitCSV` 是空的
  ——那個欄位是這次加 `DropTableProvider` 時 Unity 重新序列化才寫進場景檔的，值**一直**都是空。
  `PortraitTable` 對空表是容忍的（全部走自動對齊、不報錯）。要啟用就把 `Assets/Data/PortraitTable.csv` 拖進去。

* [x] **新手夢境教學：邪佛廣場「按左鍵發射武器」的暫停教學（⏳ 未編譯未實測）**
  （2026-09-22，作者要「對話完、暫停遊戲提示按左鍵，按下去就恢復並且真的射出武器」。
  見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3「playerHint」）<br>
  ⭐ **沒有新造輪子**：`playerHint`（玩家提示）本來就有「收起時機=攻擊、收起後才接 next」，
  缺的只有「暫停」跟「上方文字條」兩件事 ⇒ 加成它的兩個**選填欄位**（`pause`／`textId`），
  留空＝舊行為，既有兩顆（洞窟 WASD、初始森林）逐位元無變化。之後三段教學（WASD／左鍵／E）
  都能用同一顆 trigger 在編輯器排，不必回頭改程式。<br>
  ⭐⭐ **「按下左鍵」和「開火判定」不是同一件事**：`HandleFiring` 讀的是
  `Input.GetMouseButton`＝**當下按著沒**，不是「剛剛按過」。解除暫停的那一刻玩家多半已經放開了
  ⇒ 教學過了卻**一發都沒射出去**。補法是 `PlayerController.RequestFireOnce()`：開一段**強制開火窗口**
  （0.35 秒），期間開火判定一律當成玩家按著。<br>
  ⭐⭐⭐ **第一版只補「一幀」，作者實測回報「按下去只解除暫停、沒射出武器」**（同日修）。
  一幀不夠的根因是 **`Shoot()` 對雷射／佛光這種持續型武器直接 `return false`**——它們走
  `UpdateLaser`／`UpdateAura` 的持續路徑，一幀 firing=true 等於開一瞬間又關掉，**畫面上根本看不出來**；
  離散武器也只有一次機會，冷卻／魔力任一條件沒對上就靜默失敗。
  ⇒ 改成窗口，並讓持續型與離散型**各自回報「真的射出去了沒」**。<br>
  ⚠ 用**窗口**而不是一個等著被消費的永久旗標——呼叫端與 `PlayerController.Update` 誰先跑不保證
  （早一幀晚一幀都可能），但若那期間玩家開了背包／被別的面板擋住，永久旗標會一直留著、
  **等他關掉面板才莫名其妙射出一發**。<br>
  ⭐ **這條路徑失敗的症狀是「什麼都沒發生」，所以一定要有訊息**：補一發時印一則 Log
  （武器名／模式／可否開火／冷卻剩餘），射不出來時分三種情況講原因——沒裝備武器、
  這張地圖設了 `NoWeapon`、整段窗口都沒射出（冷卻中或魔力不足）。
  以前這三種**全部沒有任何 Console 訊息**，只能用猜的。<br>
  ⭐ **秒收問題**：這顆接在對話後面，而玩家多半是用**左鍵點掉對話**的——面板一開他手還按著，
  `MinVisible`(0.35s) 一過就被當成「做到了」，教學等於沒出現過。
  所以暫停模式**開場已按著就要求先放開**，下一次按下才算數。<br>
  ⚠ 暫停是 `SetExternalHold` 的**具名**鎖（**D13**），並且 `OnClose` 也一定解鎖：
  面板被別的流程關掉（換圖／死亡）而沒解鎖的話，玩家會帶著「不能動＋`timeScale=0`」進下一場，
  **完全沒有錯誤訊息**（同骨牢 `PlayerBind.OnDisable` 的理由）。<br>
  ⚠ **條件旗標沒加**：`requireFlag` 不成立預設是「整條鏈中止」，這顆卡在鏈中間會把**放怪一起吃掉**。
  夢境本來就一輩子只跑一次，不需要旗標；日後真要加必須同時填「條件不成立時＝跳過這顆繼續」。<br>
  改動：`PlayerHintPanel`（暫停模式／文字條／補射／重新按一次）、`PlayerController.RequestFireOnce`、
  `TriggerChain.ExecutePlayerHint`（讀兩個新欄位；**防呆從「兩張圖都沒有就跳過」放寬成「圖與文字都沒有才跳過」**——
  純文字的教學是合法用法）、`LanguageTable.csv` **1011**「按下左鍵發射武器」、
  編輯器 `TriggerType.cs` ＋ **`triggerTypes.json`**（⚠ 面板正本是 json，只改 `.cs` 新欄位不會出現在面板上）。
  地圖 `DreamTutorial_Square` 鏈改成 `進場對話(41) → 左鍵發射教學 → 怪物出生點1`，三份 `.dipanmap` 已同步（md5 相同）。<br>
  ⏳ **待實測**：⚠ 這顆的提示是**純文字條**（`leftImage`/`rightImage` 留空）——想改成頭上放圖就在編輯器填 `Guide_MouseLeft`。
  測試階段請先在進廣場前裝備好武器（武器之後再安排）。
