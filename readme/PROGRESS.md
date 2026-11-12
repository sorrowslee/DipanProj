# 目前進度 (Current Progress)

> 返回 [文件總覽](README.md) ｜ 這是「做過什麼、怎麼演進」的變更日誌；各系統的「現狀說明」請看對應主題文件。
> **本檔一律倒序（最新在最上）**，新條目直接加在這段註記下方。記錄格式與大小封存規則見 [DOCS_GUIDE.md](DOCS_GUIDE.md)。
> 較舊條目（專案初期 ~ 2026-09-22，共 305 條；2026-08-21、2026-08-27、2026-10-01 三次搬入）已**原文照錄**封存至 [archive/PROGRESS-archive.md](archive/PROGRESS-archive.md)，檔頭附逐條索引；查歷史脈絡去那裡，別當作已遺失。

* [x] **饕餮印第二輪：打不到新手夢境佛掌＋一次吐 3 顆（⏳ 未實測）**（2026-11-12 晚，作者回報）：① 佛掌（MonsterData 34，Scale 6、`Controllable=0`）進場後按了沒反應——找目標時照抄了骨牢的 `!Controllable` 篩選，那欄的意思是「免疫定身」不是「打不到」⇒ 拿掉；另外改用「腳底到頭頂這條身體」判斷距離與在不在畫面內（巨大 boss 的中心常在畫面外），玩家放的投彈者壓進畫面上緣內側、落點收在它下方的身體上（`Context.ClampToView`）。進場前一直跳「範圍內沒有目標」是正常的（佛掌真的還在畫面外）。② 配方 94 `SpreadCount` 3、`LandingScatterRadius` 0.7，同一隻幽靈連吐間隔 0.08→0.1 秒（`OverheadDrop.SpreadSpitGap`）。⚠ 傷害也跟著變 3 倍（每顆 6）。
* [x] **饕餮夢境台詞重寫**（2026-11-12 晚，作者嫌太短太敷衍）：`LanguageTable` 1016 →「在夢中，我是吞盡天地的饕餮，山河日月不過一口，諸天神佛皆是我腹中之物」（英文同步），長度比照路西法 1015。
* [x] **饕餮印實機第一輪調整**（2026-11-12 晚）：① 編譯錯誤：`OverheadDrop.cs` 少 `using Sorrows.Ballistics`（ProjectileData）。② 頭上幽靈看不清楚 → `DropperSize` 1.1→**1.8**、離頭頂空隙 `GapAboveHead` 0.08→**0.3**（× 大小）。③ 爆炸換掉（作者嫌 1 號太糟）→ 新 VfxTable **61**「饕餮印爆炸」：地圖編輯器特效庫 `Explosions/stylized_explosion_006` blue 12 幀，照 `EffectRecolor` 的「冷藍」公式（亮度 × (0.42, 0.60, 1.00) × 0.95）烤成 `Resources/VfxEffects/TaotieSealExplosion/_01～_12`（.meta 以 LuciferWrathExplosion 為範本換 GUID），`Scale` 2.8（96px，再乘 BulletScale 1.5 ≈ 4 單位寬），武器 79 `HitEffectID` 1→61。
* [x] **新手夢境暫時固定饕餮**（2026-11-12 晚，作者要測饕餮印）：`DreamTutorialFlow.DebugForceBloodlineId` 0→**122**（每次夢境教學都是饕餮，Console 會印黃色警告），**測完要改回 0**。
* [x] **饕餮專屬武器「饕餮印」＋新模式 `Mode=Overhead` 頭頂投彈（⏳ 未編譯未實測）**（2026-11-12 晚，作者規格：範圍內每隻怪頭上冒出饕餮鬼魂、從嘴吐炸彈落下爆炸；一般版吃遠射珠只炸身邊、專武全畫面；投彈者與炸彈外型要可換；玩家怪物都能用）：新共用類別 `Scripts/Weapon/OverheadDrop.cs`（照 SweepStrike 的擁有者無關寫法）——找目標（玩家放＝`Range` 內畫面中的敵怪；怪物放＝玩家＋玩家召喚物）→ 0～`DropStagger` 秒陸續冒出投彈者（凝結 0.35 秒）→ 吐炸彈，**投彈者黏著目標頭頂、炸彈每幀追目標當下位置**（作者擔心表演時間長怪會走遠）→ 落地爆炸（被追的那隻一定吃到＋半徑內各一次）。新欄：RecipeTable `MaxTargets`／`DropStagger`、WeaponTable `DropperStyle`（TaotieInk＝共用饕餮幽靈 shader）／`DropperVfxId`（任何序列特效）／`DropperSize`，既有列全部留空；`WeaponData` 兩個複製點（集氣快照、PlayerAbilities.ShallowCopy）都帶上。珠子：遠射＝索敵半徑、分裂＝每隻吐幾顆、連擊＝同一批連吐幾輪（OverheadDrop 自己排程）、迅捷＝縮短掉落秒數、須彌＝炸彈＋爆炸放大。資料：配方 94、武器 79（傷害 6、冷卻 1、無耗魔、Range 18、借武器 4 的炸彈圖與爆炸 VfxTable 1）、道具 79（法印，icon `icon_taotieseal`＝GPT 青銅饕餮紋方印、灰底去背）、表B 122 `DreamWeaponId` 71→79。文件：RECIPE_DESCRIBE（模式說明、欄位、珠子）、BOSS_MODULE、BLOODLINE §5d、TODO。
* [x] **饕餮神格特效：身邊兩隻水墨饕餮幽靈（⏳ 未編譯未實測）**（2026-11-12 傍晚，作者給水墨饕餮參考圖）：新的一層 `BloodlineGhost`（表B 新欄 `GhostStyle`＝第 32 欄，只填 122 `TaotieInk`，其他列不動）＋shader `Custom/BloodlineGhost`＋貼圖 `VfxEffects/BloodlineGhost/taotie_ghost`（GPT 產原創水墨頭，洋紅底轉成墨濃度 alpha、被墨包住的洋紅填成留白）。兩隻一左一右，隨機只出左／只出右／兩隻一起（30%），凝結→浮動停留→尾巴先散；尾巴依離頭距離做 domain warp 擺動；外圍暗紅微光讓墨在暗地板上讀得出來。numpy 預覽 `TempImage/bloodline_Gluttonborn/fx/preview.py`。同時撤掉 TODO／表B 備註裡的「吞噬領域」（作者沒印象）。另：年的 PortraitTable 六張填 OffsetX 往內推（圖中心 618→520）；三張 GPT 版 normal 的透明像素底色是洋紅（對話框出現紫邊）→ 改成鄰近角色色。
* [x] **暴食血統：立繪 happy～hurt 換成 GPT 版（⏳ 未實測）**（2026-11-12 傍晚）：作者用新對話＋短提示詞（CHATGPT_IMAGE_WORKFLOW §7.3 開頭）讓 GPT 重出三階 15 張、直接 commit `445964d0`（純黑底 RGB、尺寸正確）。AI 整理：`cutout_black.py` 去背＋清 <400px 碎塊；新工具 `tools/holes_black.py` 挖掉被手臂與身體包住的純黑底空洞（年 cry／fear／hurt、饕餮 fear／hurt）；饕餮 cry／hurt GPT 畫成近景（比 normal 大約 1.15 倍）→ 以畫布中心縮 1/1.15、頂端對回 188，底部被裁切處往下延伸到 1535（在對話框後面看不到），讓內容框高度跟 normal 一致。右緣：餓鬼 796～797（全對齊）、饕餮 824～882（normal 849）、**年 1210～1483（normal 1299，happy／angry 爪子張開超過 1440）→ 年換表情仍會左右晃**。GPT 原檔留 `portrait/gpt_raw/`、去背版 `portrait/gpt_cut/`。
* [x] **暴食血統：正式序列圖後的調整＋補齊立繪（⏳ 未實測）**（2026-11-12 傍晚，作者回報 4 點）：① 表B `BodyScale` 年 1→**1.4**、饕餮 1→**1.5**。② 饕餮 dead 比 idle 大 26.6%（體積尺度第一幀比 1.266）→ 25 張以腳底為中心縮 0.79 倍、下移 3px（比值 1.002、腳底 198 對 199，PROBLEMS **G23**），餓鬼／年 dead 只差 2～3% 不動。③ 立繪「只有 happy 不一樣」＝負面四張是 normal 複本（GPT 拒畫），作者改找其他 AI 畫（JPEG、1.645 倍、純黑底）→ 新工具 `tools/cutout_black.py` 去背＋預乘縮回原尺寸＋清碎塊，happy～hurt 15 張全換（作者選；兩張 angry 帶暗紅煙霧照用），大小與頂端跟 normal 差 ≤9px。④ `ShadowAnchorTable` 刪 5 列過時錨點（暫代時留下、幀數相同或 manual 沒被重算的 4 列＋剛縮的饕餮 dead）。**作者要做：Sync Map Assets＋計算影子錨點。**
* [x] **暴食血統：第二階「年」重做外型（⏳ 未在 Unity 實測）**（2026-11-12 下午，作者認為 v1 獸頭太醜、怕玩家抵觸）：先試「主角人頭＋獸角」（v2）被退——要的是**有人性輪廓的獸首**；出三種頭型概念圖（石獅／舞獅／偏人形）→ 選石獅，但「沒有暴食感」→ 加兩個暴食元素：①閉嘴但寬到不合理、整排獠牙沿唇線 ②頭後對稱的**獠牙形骨冠**（暗紅漆＋暗金，只在冠上）；另做了 ③喉頭第二張巨口的對照版，作者選不加（A 版）。身體維持 v1 設計（腹部的嘴、骨刺肩、草繩腰帶）。角色圖 `out/Nian_base_1024.png` 已換成新版（舊版留 `Nian_base_1024_old_v1.png`），立繪 normal／happy 重畫（寬體型，負面四張仍複製 normal），覆蓋 `Talk/Gluttonborn/Nian/`；PortraitTable、其他資料不動。約 7 次額度、零被擋（都在第三個對話）。
* [x] **新增暴食血統（Gluttonborn：餓鬼 → 年 → 饕餮），序列圖暫代、立繪負面表情暫代（⏳ 未在 Unity 實測）**（2026-11-12，作者給血統概述、AI 主導產圖；第十二個系列）：
  ① **美術（ChatGPT「燃燈劫」專案，三個對話、約 16 次額度、被擋 6 次）**：角色圖——餓鬼 v2（作者要求赤裸上身＋上身也長嘴）、年 v1（使徒式倒三角巨獸，一次通過）、饕餮 v6（古銅神將版被退「像神將」→ 中年鬍鬚赤腳＋胸口水墨饕餮巨口＋墨紋到手、黑手掌 → 北歐白衣被退 → 純白褲＋**黑腰帶**；黑腰帶與紅腰帶對照版都是程式在 v4 上改色，零額度）。立繪只有三階的 normal／happy（年寬體型 1536×1024），angry／cry／fear／hurt 先複製 normal——GPT 對「滿身長嘴＋負面情緒」一律以暴力規範拒絕。成品瓶 `bloodline_Gluttonborn`（暗紅褐液、古銅饕餮紋章、帶角獸首瓶塞）。
  ② **進專案**：`Talk/Gluttonborn/{Preta,Nian,Taotie}/<情緒>.png`、`Resources/.../bloodline/bloodline_Gluttonborn.png`（.meta 以 Angelborn 為範本換 GUID）、**序列圖暫代**＝寄生體／望月者／尼德霍格（只複製 png）。
  ③ **資料（只新增列、零程式改動）**：表A 第 12 列（`DreamDramaId`=90）、表B 120～122（BodyScale 1、WalkSpeed 空、五屬性佔位；饕餮 `DreamWeaponId` 暫借魔狼爪 71）、ItemTable 3120～3122（外型短句用作者給的）、BaseBloodRoll 3120、ShadowAnchorTable 12 列（暫代複製）、PortraitTable 18 列、夢境台詞 drama 90／TalkGroup 87／DramaTalk 259／LanguageTable 1016（中英）。`git diff --stat`：9 檔 41 行，全是新增。
  ④ **新坑**：Chrome 擴充連到作者公司那台（家裡那台被登出）→ 用 `onThisComputer` 判斷；長對話抓不到母版、被拒後同對話審核變嚴 → 立繪另開新對話；寫進 [CHATGPT_IMAGE_WORKFLOW.md](CHATGPT_IMAGE_WORKFLOW.md) §7.6（含作者這次的審稿口味）。現況 [BLOODLINE.md](BLOODLINE.md) §8；待辦 [TODO.md](TODO.md)〈暴食血統〉。工作檔在 `TempImage/bloodline_Gluttonborn/`（各版本都留著）。**作者要做：Unity 跑 Sync Map Assets；AutoSprite 用 `out/{Preta,Nian,Taotie}_base_1024.png`。**
* [x] **夢境教學血統改回隨機**（2026-11-11，作者測完路西法之怒）：`DreamTutorialFlow.DebugForceBloodlineId` 112→0。
* [x] **掃射可穿牆＋冷卻改從施放算起（⏳ 未編譯未實測）**（2026-11-11，作者：光束被不可走區域擋住很怪、冷卻要短且能靠珠子調）：Sweep 開放 `BlockedByEnvironment`（0＝不檢查牆、直接掃過）；冷卻改成單純 `FireInterval` 從施放算起（拿掉「整發播完才能再發」的發射鎖，玩家與怪物都是），疾發珠才有感。配方 93：`BlockedByEnvironment`=0、`FireInterval` 0.3→1（實際出手間隔約 2.8→1 秒）。
* [x] **掃射改成「朝滑鼠方向固定掃 Range 那麼遠」（⏳ 未編譯未實測）**（2026-11-11，作者試打：只掃到滑鼠位置常常太短）：`SweepStrike.Cast` 掃描長度改成 `Range`（>0 時不管滑鼠多近都掃滿；-1＝舊行為只到滑鼠）；遠射珠可加。配方 93 `Range`=18（≈ 一個畫面寬）。整發變長：掃描 18÷12＝1.5 秒＋延遲＋爆炸，約 2.8 秒才能再發。
* [x] **路西法之怒光球畫成方塊 → 修正（⏳ 未實測）**（2026-11-11，作者回報）：`Custom/AdditiveGlow` 只讀貼圖 rgb、不看 alpha，第一版貼圖 rgb 全白、只有 alpha 是圓 ⇒ 整張方形都亮。改成亮度寫進 rgb（預乘）、衰減改 (1−r²)² 連續柔光（無硬邊）；外圈 1.6 倍、亮芯 0.5 倍。
* [x] **其他血統的 dead 腳底一併對齊 idle（⏳ 未實測）**（2026-11-11，作者要求，接 G24）：差 ≥5px 的 11 個血統 dead 整組往下平移（Ravager 23、Parasite 20、Fafnir 18、Bloodseeker 12、Titan 9、Swarm Emperor 9、Cain 7、Nidhogg 6、Gargoyle 6、MountainGiant 5、Foundation 5 px），GameAssets／StreamingAssets 兩份，影子錨點 dead 列同步；1～4px 的沒動。
* [x] **路西法之怒：源點加紅色能量球（⏳ 未編譯未實測）**（2026-11-11，作者要求）：`SweepStrike` 在光束源點畫一顆同 `BeamColor` 的光球（直徑＝可見身高×0.16≈一顆頭；程式生成圓形柔光貼圖、加色材質 `Custom/AdditiveGlow`，外圈＋偏白亮芯兩層、6Hz 輕微脈動），跟著施放者移動，光束掃完時一起淡出；材質 OnDestroy 收掉。
* [x] **神諭者 → 天使變身時人物往上跳一截（⏳ 未實測）**（2026-11-11，作者回報）：dead 素材的腳底位置跟 idle 不一致（天使趴姿高 0.20 身高、神諭者低 0.09），換外型那一下差約 0.3 身高。天使 dead 整組往下 42px、神諭者往上 10px（整數像素平移，GameAssets／StreamingAssets 兩份），影子錨點 dead 兩列同步。詳見 PROBLEMS **G24**。
* [x] **血統揭示面板：石碑上的名字往上移（⏳ 未實測）**（2026-11-11，作者截圖回報名字偏下）：`BloodlineIntroPanel.NameArea.y` 0.22→0.32（作者看過太高）→ **0.255**（量兩張實機截圖：0.22 低 3.5px、0.32 高 7px，內插讓字心對齊石碑淺色區中心；全部血統共用）。⚠ 一開始誤會成移整塊石碑（`PlateYFromFrameBottom` 105→150→220），已改回 105。
* [x] **天使系列 BodyScale 再調（⏳ 未實測）**（2026-11-11，作者指定）：表B 神諭者 1.3→1.2、天使 1.5→1.6、路西法 1.4→1.5，Note 與 BLOODLINE §8 同步。
* [x] **路西法之怒實機調整（⏳ 未實測）**（2026-11-11，作者看過第一版）
  - 爆炸放大：VfxTable 60 `Scale` 2→6、配方 93 `AreaRadius` 1→3；作者再看覺得太大，縮 1/3 → **`Scale` 4、`AreaRadius` 2**。
  - 光束源點太高：`SweepOriginHeight` 1.8→1.15（頭頂再往上約一顆頭）。
  - 加分散：`SpreadCount`=3、`SpreadAngle`=30（三道光束同時掃）。夢境教學用的也是武器 78，一起生效。
* [x] **路西法之怒換爆炸特效（⏳ 未實測）**（2026-11-11，作者在特效預覽器挑 `Explosions/stylized_explosion_003` 紅色）
  - 14 幀原圖（128×96 像素風）複製到 `Resources/VfxEffects/LuciferWrathExplosion/`，改名 `_01`～`_14`；每張 `.meta` 與資料夾 `.meta` 以 BloodFireBurst 為範本（Point 濾鏡、PPU 100）換新 GUID。
  - VfxTable 新增 **60**（14 張、15fps、`Scale` 2＝畫面約 2.6 寬，配爆炸半徑 1；實機再調），WeaponTable 78 `HitEffectID` 1→60。整發的發射鎖會自動把爆炸長度算成 14÷15≈0.93 秒。
* [x] **路西法之怒做成道具長劍＋夢境教學暫時固定路西法（⏳ 未實測）**（2026-11-11，作者要求）
  - ItemTable 新增 **78 路西法之怒**（`EquipSlot=Weapon`、`WeaponID=78`、說明「長劍」），作弊面板（L）「給道具」輸入 78 就能拿到、一般裝備使用。
  - 新 icon `UI/Icons/Equipment/icon_luciferwrath.png`（500×500，ChatGPT 燃燈劫專案出圖：黑鋼長劍、骨白／黑雙翼護手、深紅血槽、柄頭紅寶石；灰底去背，`.meta` 以 icon_soulhand 為範本換新 GUID）。原圖在 `TempImage/bloodline_Angelborn/work/luciferwrath_icon_gpt.png`。
  - ⚠ `DreamTutorialFlow.DebugForceBloodlineId` 暫改 **112**（每次夢境教學都是路西法，Console 會印黃色警告），**測完要改回 0**。
* [x] **新武器模式 `Sweep` 掃射＋路西法專屬武器「路西法之怒」（⏳ 未編譯未實測）**（2026-11-11，作者規格：巨神兵的大砲）
  - 一發：頭頂上方（腳底＋可見身高×`SweepOriginHeight`）的源點，光束先劈到腳底，再沿地面掃向滑鼠；掃過的線上每隔 `ExplodeSpacing` 排爆炸、延遲 `ExplodeDelay` 炸開。光束不傷人，每顆爆炸對半徑內每個目標各吃一次 `Damage`；吃 `SpreadCount`／`SpreadAngle`（多道同掃）；撞牆就停；整發（含最後一顆爆炸特效）播完才能再發（冷卻＝整發秒數＋`FireInterval`）。
  - 程式：`WeaponModeSpec` 加模式＋4 欄（工坊自動跟上）、`RecipeEntry` 讀欄（Range 空＝12、AreaRadius 空＝1）、**新檔 `Scripts/Weapon/SweepStrike.cs`**（玩家怪物共用：光束用 `SpawnChainVisual` 靜態折線每幀重餵，爆炸排程，擁有者死亡即中止）、`PlayerController.Shoot` 與 `MonsterWeaponUser.TryUse` 各加一個分支。BallisticsSystem 沒動。
  - 資料：RecipeTable 表頭最後加 4 欄（62 欄，既有列留空）＋配方 93；WeaponTable 78（傷害 8、紅色標準光束 0.25、爆炸特效暫代 VfxTable 1）；表B 112 路西法 `DreamWeaponId` 66→78。
  - 文件：RECIPE_DESCRIBE〈Sweep 掃射〉、RECIPE_AND_WEAPON、LASER、BLOODLINE §8、TODO。
* [x] **神諭者攻擊只播到「收杖」→ 表B `AttackFrameRange`=`1-12`（⏳ 未實測）**（2026-11-11，作者回報；作者同日換成 12 幀新攻擊圖）
  - 自動判斷只播 #1-4（後拉蓄力段），前揮 #6-10 完全沒播。原因同 G20 類：12 格跟站姿的輪廓差全在 0.45～0.65、曲線平（振幅 31%），「第一次到峰值 90%」落在 #4；保底要「振幅 <35% **且** 播放比 <20%」才整段照播，這組 33% 沒觸發。照作者規則填覆寫欄不改全域規則；取整段 1-12（#12≈#1，按住連發接得順）。
  - ⚠ 順手修正本人前一筆的失誤：更新天使三階 Note 時用了字串全域取代，誤把 50 築基期／60 蛟人／70 龍奴／80 寄生體 的 Note 也改成「2026-11-11 作者實機定」——已從 git HEAD 還原這 4 行（只有 Note 文字被改、數值未動）。
* [x] **天使血統實機調整：神諭者 dead 縮小、三階 BodyScale（⏳ 未實測）**（2026-11-11，作者截圖回報）
  - 神諭者 dead 比其他動作大：量第一幀（站姿）人物高 idle 113px vs dead 134px（大 19%，寬也大 15%）。原因是 `PlayerAnimator.Setup` 對 dead **刻意不做體積正規化**（躺姿本來就矮，正規化會被放大），所以素材畫大就直接大。修素材：25 張以腳底（x=128, y=195）為中心縮 113/134≈0.84 倍、畫布維持 256×256（RGBa 預乘縮放避免黑邊）；縮完 dead 第一幀 114px≈idle。原圖在 git。
  - 表B `BodyScale`：神諭者 1→1.3、天使 1→1.5、路西法 1→1.4（作者指定），Note 欄同步。
  - 同日追加：**天使 dead 反而小三成**——idle 有展開的翅膀，改用程式同一把尺 √(高×√面積) 比第一幀：idle/dead＝1.348（同法量神諭者縮後 0.993、路西法 0.992，都不用動）。放大會撞 256 畫布 ⇒ 25 張以腳底 (128,193) 為中心放大 1.348、**畫布對稱加大成 308×308**（水平中心不動）；縮完比值 1.000、零裁切。畫布變大是安全的（PPU 與畫布無關、pivotY 有除畫布高、影子表自帶 CanvasW/H），寫成 PROBLEMS **G23**。
  - 通則（G23）：新血統要量「dead 第一幀 vs idle 第一幀」的體積尺度，偏離超過一成就修素材，別期待程式補。
* [x] **作弊面板加「動作」分頁：主角輪播指定動作（含 dead）（⏳ 未編譯未實測）**（2026-11-11，作者需求：地圖裡測得到 idle/walk/attack，dead 看不到）
  - 作者定的 UI：L 面板新分頁，idle／walk／attack／dead 四顆**互斥** toggle、可全關；開一顆主角就一直輪播。
  - `PlayerAnimator.PreviewState`（static `State?`）：預覽中忽略 PlayerController 每幀的 `SetState`（真死 Dead 例外＝關預覽照常死）、`StartAttack` 不受理；姿勢表演（倒下／爬起）優先、演完回到預覽；Setup（換血統）重設狀態後下一幀自動重新進入。dead／attack 本為一次性，預覽時末幀停 `PreviewEndHold`=0.6 秒再重播；attack 播全部幀（逐張檢查素材，不是實戰的起播～結束幀）；walk 用滿速（站著不動照實戰會被速度連動壓慢）。幾何跟著預覽走（dead 時影子用 dead 錨點、血統特效關）。
  - 同日追加（作者）：玩家用 WASD 移動或真的攻擊出去就自動結束預覽，再開 L 按鈕是關的。`PlayerController.HandleVisuals` 看 `_moveInput`（被擊退不算）、`TriggerAttackPose` 開頭（真的發射成功才算）各呼叫一次 `PlayerAnimator.EndPreview()`——它同時清 static 與 `_inPreview`，否則 Update 的「剛關預覽」分支會在同一幀後把狀態重設成 Idle，吃掉剛起手的攻擊。
  - 面板顯示目前血統與四個動作的幀數，沒圖的動作會 toast 提示。說明寫進 BLOODLINE.md〈新血統素材的實機檢查〉。
* [x] **立繪測試 NPC：跟它講話會用「目前血統」輪播主角六種情緒立繪（⏳ 未實測）**（2026-11-11，作者需求：新增血統後一輪對話就能檢查該角色每張立繪）
  - **純資料、零程式**：每個血統的 `Characters/Talk/<血統>/` 都是同一組 6 張（normal/happy/angry/cry/fear/hurt），`Actor_<情緒>` 本來就會依當下血統解析 ⇒ 開一個對話群組 6 句、每句換一個 `Actor_*` 即可。
  - 落點：`DramaTalkTable` 253～258（群組 86，主角放**右側**＝實際劇情最常見的位置、會被鏡像，順便驗翻轉後的對齊）、`DramaTable` 89（TalkGroup 86）、`LanguageTable` 9901～9906（新開「開發測試用 9901–9999」段）、`Maps/Future/Future_Arena.dipanmap` 的「測試說話npc」dramaId 23→89（原本借用邪佛廣場的測試對話）。
  - 用法：先把主角換成要檢查的血統，再去 Future_Arena 跟這隻 NPC 講話。⚠ 若日後某血統多了第 7 種情緒圖，這組 6 句不會自動涵蓋，要在群組 86 補一句。
* [x] **新增天使血統（Angelborn：神諭者 → 天使 → 路西法），序列圖暫代（⏳ 未在 Unity 實測）**（2026-11-11，作者給血統概述、AI 主導產圖；第十一個系列）：
  ① **美術（ChatGPT「燃燈劫」專案同一個對話，約 30 次額度、零被擋）**：三張 1024 角色圖——第一版第一、二階被退（太樸素），作者給兩張參考圖（華麗白金祭司／兜帽空洞面罩天使）後重做通過；路西法改了五版：黑白分身「難看的根源」→ 短白髮＋參考圖盔甲 → 加蛇眼紫火紋、獸人式誇張盔甲 → 變瘦 → 露腹（定稿 v6，程式去紫邊）。18 張立繪（天使、路西法寬體型 1536×1024，作者指定）、成品瓶 `bloodline_Angelborn`。
  ② **進專案**：`Talk/Angelborn/{Oracle,Angel,Lucifer}/<情緒>.png`、`Resources/.../bloodline/bloodline_Angelborn.png`（.meta 以 Qingqiuborn 為範本換 GUID）、**序列圖暫代**＝靈根築基期／血伯爵／該隱（只複製 png）。
  ③ **資料（只新增列、零程式改動）**：表A 第 11 列（`DreamDramaId`=88）、表B 110～112（路西法 `DreamWeaponId` 暫借雷雨天罰 66）、ItemTable 3110～3112、BaseBloodRoll 3110、ShadowAnchorTable 12 列（暫代複製）、PortraitTable 18 列、夢境台詞 drama 88／TalkGroup 85／DramaTalk 252／LanguageTable 1015（中英）。`git diff --stat`：9 檔 41 行，全是新增。
  ④ **新坑**：這次瀏覽器工具的 `file_upload`、`browser_batch` 都被關掉（核准無法彈出），改請作者拖兩次圖；紫色系角色用舊去背會把紫色洗成灰——新工具 `cutout_magenta_keep.py`／`defringe.py`／`fill_small_holes.py`／`fit_manual.py`（在 `TempImage/bloodline_Angelborn/tools/`）。寫進 [CHATGPT_IMAGE_WORKFLOW.md](CHATGPT_IMAGE_WORKFLOW.md) §7.5（含作者這次的審稿口味）；現況 [BLOODLINE.md](BLOODLINE.md) §8；待辦 [TODO.md](TODO.md)〈天使血統〉。工作檔在 `TempImage/bloodline_Angelborn/`（各版本 `out/*_vN.png` 都留著）。**作者要做：Unity 跑 Sync Map Assets；AutoSprite 用 `out/{Oracle,Angel,Lucifer}_base_1024.png`。**
  - 2026-11-11 後續：作者換正式序列圖，已刪除 `ShadowAnchorTable.csv` 的 `characters/angelborn/*` 12 列暫代錨點（222→210 行）；⏳ 待作者跑「計算影子錨點」重算。
* [x] **血狂之爭實測第二輪五項（⏳ 未編譯未實測）**（2026-11-10，作者回報）：
  ① **該隱大門引路第二句**「屬下就送到這兒了」跟後面還是他帶路矛盾 → LanguageTable 3041 改「前面就是迎賓大廳……請您隨屬下來。」。
  ② **狂族士兵走路比站著大**：演出演員與地圖 NPC 沒吃怪物表的逐動作倍率（[PROBLEMS.md](PROBLEMS.md) **G21**）。新增 `MonsterAnimator.RelativeScalesFor`，`CutsceneActor.Npc`／`NpcSpawner` 改傳「相對 idle」的倍率（idle 大小不變）。⚠ 連帶影響所有演出演員／NPC 的 walk／attack 大小（戰狼、血術士等表裡有填倍率的都會跟怪物本體一致）。
  ③ **芬里爾火焰腳印只剩一道**：腳步特效假設一圈兩步，芬里爾跑步 25 幀裡其實 6 步（**G22**）。表B 新欄 `StepFrames`（索引 30），芬里爾填 `4;8;12;16;20;24`；`BloodlineTrail.SetStepFrames` 走到那幾幀才放、左右交替；其他血統留空＝舊行為。
  ④ **和談大廳突襲的血蝠開打時變倒吊** → 演出 `assault_werewolf` 四隻演員換 `Blood Warlock`（scale 1.83、不飛）、出生點改名 `出生點-血族突襲-血術士`（36）。
  ⑤ **和談大廳殺完沒壓迫感** → 出生點新增兩個勾選 `waitClear`（清光才出下一波）、`noLoot`（不掉寶／血球／清場血球；未來經驗值也不給）：`MapLoader` 讀、`MapMonsterRespawner` 處理，編輯器 `TriggerType.cs`＋`triggerTypes.json` 加欄。地圖：突襲軍編群組 → 全滅 → `湧怪-狂族／-血族` 每次清光 2 秒後再湧 12 隻、永遠殺不完（[bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) D8b、[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3.5）。
  **作者要做：主遊戲＋編輯器都重新編譯、Sync Map Assets。** 暫存檔 `TempImage/_fenrir_walk_sheet.png`（芬里爾跑步幀總表）AI 刪不掉，可清掉。
* [x] **血狂之爭：血族／狂族玩家的引路與和談語氣修正（⏳ 未實測）**（2026-11-10，作者四點回報）：① 大門／迎賓／長廊的引路第二句原本三種引路者共用管家的「請往這邊走。」（3002），狂族士兵兇完又說「請」——改成各族自己一句；② 和談大廳開場 `talk` 只有管家版，血族／狂族玩家被帶進來時引路者變成管家——複製出 `talk_vampire／talk_werewolf／talk_cain／talk_fenrir`（帶路演員與報到句不同），`和談演出` 改五顆條件串；③ 互嗆 drama 78 最後「需要一位公道人／你來說」對血族／狂族玩家沒道理——新增 drama 85（續罵）、86（該隱：艾爾文察覺→接 52）、87（芬里爾：夏綠蒂察覺→接 53），人類維持 78；④ 三階：三間房各加 `guide_*_cain／guide_*_fenrir` 演出（士兵對該隱惶恐恭敬、對芬里爾興奮崇敬）。新資料：LanguageTable 3038–3065（中英）、DramaTable 85–87、DramaTalkTable 227–251；四張地圖（17～20）。用腳本模擬五種血統逐句印出確認台詞順序。細節見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.1–3.4、§6。**作者要做：Sync Map Assets。**
* [x] **`map_grid.py` 看得到地上物＋出生點自動檢查**（2026-11-10，作者同意）：原本格子圖只畫可走層，家具在圖上跟空地一樣，練兵場／大營出生點就是這樣擺到家具上的。`dipan.py` 新增 `object_blockers`／`obj_clearance`／`obj_cover`，照遊戲端 `MapLoader.BuildObjectCollision` 算地上物碰撞（catalog 的 footprint 遮罩；沒烘到就照 `ObjectFootprint.Scan` 當場掃；填滿率 ≥0.9 走 alpha 外接框；縮放／翻轉／旋轉；walkable／passThrough 略過、有出現條件的當作會出現）。`map_grid.py` 多畫 `O`（擋大半格）／`o`（擋一角），出生點不可走或離地上物 <0.5 格標 `!` 並列清單；**飛行怪（BrainType Flying／Bat）不檢查**（刻意生在牆上／倒吊）。需要 Pillow。文件：[SCRIPTING_GUIDE.md](SCRIPTING_GUIDE.md) §2.3、§5.1。首次全模組掃描：武器室 8、釀造室 4、餐廳 6、花園 1 個出生點壓到地上物（未修，待作者決定）。
* [x] **血狂之爭 25 訓練場／26 大營：出生點避開新地上物（⏳ 未實測）**（2026-11-10，作者重新佈置兩張圖、加了不少地上物）：照遊戲的碰撞算法（catalog 烘好的 `footprint` 遮罩 × 擺放縮放／翻轉 × 位置；近實心走 alpha 外接框，見 PROBLEMS **B9**）算出每個出生格中心到最近地上物碰撞的距離。訓練場 15 格有 6 格壓在稻草人／盆栽／武器架上；大營除了 6 格壓在地上物上，另有 3 格因可走層重畫落在不可走區。全部搬到最近、淨距 ≥0.5 格的可走格，左右對稱的維持對稱，沒問題的不動；只改出生點 `cells`，怪種／數量／波次不變。新座標見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) 波次表。（當時 `map_grid.py` 看不到地上物，是這次出錯的原因；已補上，見上一條。）**作者要做：Sync Map Assets。**
* [x] **九尾天狐影子校正（只改表）**（2026-11-10，作者實機回報影子對不準）：auto 把扇形尾巴與裙擺當成腳，idle −20.5／walk −17.5（偏身後約 20px）；§5c 的上 55% 重心也失效（尾巴長在上半身，量出 −19.5）。改拆最底帶：兩腳 `[-15~-8]` `[+12~+19]`、中點 ≈ +2 ⇒ idle／walk／attack 三列 manual：X +2、Y 69（鞋底 61＋可見高 6%）、寬 60（裙擺寬）。另刪掉靈狐 idle、玄狐 attack 兩列殘留暫代（正式圖幀數與暫代相同，工具以幀數判斷所以沒重算），待作者跑「計算影子錨點」補回。[SHADOW.md](SHADOW.md) §5c 補一則。
* [x] **揭示面板裁切改對齊框內黑底（⏳ 未編譯未實測）**（2026-11-10，作者：尾巴蓋住左邊框）：0.8 置中裁切比內框寬，而內框左右不對稱（原圖量得 0.217～0.81）。改成 `PortraitClipLeft` 0.225／`PortraitClipRight` 0.80，容器跟著位移、立繪反向補回；且只在新舊立繪有寬體型時啟用 `RectMask2D`。另：第一次出現的青色方塊是編輯器非同步 shader 編譯的替代 shader（裁切變體第一次用到），打包版不會有。
* [x] **血統揭示面板：寬幅立繪改成左右裁切（⏳ 未編譯未實測）**（2026-11-10，作者看了上一版：尾巴整個凸出框外太誇張，問能否遮蔽）：`BloodlineIntroPanel` 的兩張立繪改掛在新的 `PortraitClip`（`RectMask2D`）底下，寬＝框寬 × `PortraitClipWidth` 0.80、高＝整個框高（只裁左右），柔邊 `PortraitClipSoftness` 28px。直式立繪寬 ≤ 0.78 不會被切到。shader 已支援 `UNITY_UI_CLIP_RECT`，沒改。
* [x] **青丘實機回饋：體型、玄狐攻速、揭示面板寬幅立繪（⏳ 未編譯未實測）**（2026-11-10，作者實機）：① 表B `BodyScale` 靈狐 1.1／玄狐 1.4／九尾天狐 1.5（作者指定）；② 玄狐 `AttackAnimSpeed` 0.7（作者嫌攻擊太快）；③ **血統揭示面板的九尾天狐立繪又小又擠在右下角**：`BloodlineIntroPanel.LayoutPortrait` 是「contain 進框（寬 0.78／高 0.70）」，1536×1024 寬體型立繪會先撞寬度上限 ⇒ 人只剩一般立繪一半高、左半邊尾巴把人推到右邊。修法：新增 `WideAspectThreshold`（預設 1.0），寬/高超過它就只照高度縮、允許尾巴超出破碎框；直式與 Base（0.80）行為不變。圖本身沒問題。文件：[BLOODLINE.md](BLOODLINE.md) §5 版面表。
* [x] **新增青丘血統（Qingqiuborn：靈狐 → 玄狐 → 九尾天狐），序列圖暫代（⏳ 未在 Unity 實測）**（2026-11-10，作者給血統概述、AI 主導產圖；第一個女性血統）：
  ① **美術（ChatGPT「燃燈劫」專案，約 24 次額度、零被擋）**：三張 1024 角色圖（洋紅底去背＋程式對齊；靈狐重產兩次——先拿錯參考圖畫成靈根鍊氣期的臉、再退件「太像原本主角」，定稿＝第一版造型＋主角的臉＋衣服不破＋炭灰髮帶；玄狐發光眼由程式改成全黑）；18 張立繪（九尾天狐用寬體型 1536×1024，作者指定）；成品瓶 `bloodline_Qingqiuborn`（深青綠液、九尾狐紋章、坐狐瓶塞）。
  ② **進專案**：`Talk/Qingqiuborn/{SpiritFox,MysticFox,NineTailedCelestialFox}/<情緒>.png`、`Resources/.../bloodline/bloodline_Qingqiuborn.png`（.meta 以 Swarmborn 為範本換 GUID）、**序列圖暫代**＝靈根三階（只複製 png）。
  ③ **資料（只新增列、零程式改動）**：表A 第 10 列（`DreamDramaId`=84，不填會撞 drama 41）、表B 100～102（九尾天狐 `DreamWeaponId` 暫借焚獄炎杖 69）、ItemTable 3100～3102（新編號制）、BaseBloodRoll 3100、ShadowAnchorTable 12 列（暫代複製）、PortraitTable 18 列、夢境台詞 drama 84／TalkGroup 81／DramaTalk 226／LanguageTable 1014（中英）。
  ④ 同日另定**主角台詞不分性別**規則（AGENTS.md、[DRAMA.md](DRAMA.md)）。產圖 SOP 與新坑寫進 [CHATGPT_IMAGE_WORKFLOW.md](CHATGPT_IMAGE_WORKFLOW.md) §7；現況 [BLOODLINE.md](BLOODLINE.md) §8；待辦 [TODO.md](TODO.md)〈青丘血統〉。工作檔在 `TempImage/bloodline_Qingqiuborn/`（含錯誤參考版 `spiritfox_v1_wrongref`、現代服版 `spiritfox_v2_modern`）。**作者要做：Unity 跑 Sync Map Assets。**
* [x] **血統藥劑全面改號：系列藥劑 ID ＝ 3000＋血統 Id（只動資料與文件）**（2026-11-10，作者要求；準備加第 10 個系列，舊規則「30x／32x／33x、末位＝系列序」到第 10 個系列會撞進階藥劑 310，而系列預計會到 20～50 個）：新規則每個系列佔一個十位段 `3SS0／3SS1／3SS2`（第一階／第二階直達／第三階直達），容量到第 99 個系列，藥劑 ID 去掉前面的 3 就是血統 Id；**進階藥劑 310／311 維持原號**。既有 27 瓶系列藥劑一併搬遷（作者：存檔現在不重要、趁早調）：ItemTable 27 列、`BaseBloodRoll.csv` 9 列、`DropTable.csv` 2 格（血狂之爭雙首領必掉）、`BloodFang_InitialScene.dipanmap` 兩個 `giveItem`（編輯器／GameAssets／StreamingAssets 三份）、4 處程式註解。grep 確認程式沒有寫死藥劑 ID；**沒寫存檔遷移**——改號前的存檔若持有血統藥劑，讀檔會找不到那個道具。舊號對照：`30k`→`3k0`、`32k`→`3k1`、`33k`→`3k2`。文件：[BLOODLINE.md](BLOODLINE.md) §3（規則＋對照表）、§7、§8，GEM_SOCKET／INVENTORY／RUN_PROGRESS／GACHA_SYSTEM／TRIGGER_CHAIN／bloodfang/SCRIPT 的引用。
* [x] **鍾馗攻擊只播 3 格：表B 加手填播放範圍 `AttackFrameRange`（⏳ 未編譯未實測）**（2026-11-10，作者回報刀在半空就停）：自動判斷把「刀舉最高」當成出手到底（由上往下劈的動作輪廓最大在舉刀），只播第 9～11 格。作者查問當初為何做自動判斷（2026-08-22 跳過起手、08-26 播到最大幀，見 PROBLEMS G6／G9）後拍板：表B 新欄 `AttackFrameRange`（索引 29，`起-迄` 1 起算，留空＝自動），鍾馗填 `5-17`；`BloodlineDef.AttackFrameStart/End`、`PlayerAnimator.SetAttackRangeOverride`（Setup 後自動重套、-1 還原自動值）、`BloodlineSystem.ApplyTo` 每次寫入。**之後序列圖被抓錯一律用這欄解決**（PROBLEMS **G20**、BLOODLINE §2、ACTORS_AND_COMBAT 2b 已改）。同日：作者給的鍾馗攻擊 4 格（綠底）切成 512 並去背、另一組移除刀光（`TempImage/bloodline_Netherborn/zk_attack/`）。
* [x] **屍毒葫蘆毒雲改成一般特效（只動表格）**（2026-11-09，作者回報毒雲被壓在人物之後；先試「地面特效加 YSort 欄」被作者否決——地面特效就該被所有東西蓋住，已整個還原）：那組圖其實是「從落點冒起的骷髏毒雲」，不是平鋪在地上的東西 ⇒ 畫面搬到 **VfxTable 59**（同一組 `GroundEffect/CorpsePoison` 17 幀 @8fps、Scale 4.69＝原本縮到直徑 6 的大小、一般特效預設排序＝角色之上），武器 45 `HitEffectID`＝59（拋物線落地本來就會在落點播命中特效）；**GroundEffect 10 只留傷害**（AniPath／AniNumber／AnimFPS 清空＝看不見，半徑 3、DOT 0.35 秒、2.1 秒不變）。地面那圈虛線一起畫在角色之上（作者同意不切開）。⚠ 集氣射擊時命中特效會 ×2、傷害半徑不會（同其他武器）。規則寫進 [GROUND_EFFECT.md](GROUND_EFFECT.md)。
* [x] **變身爆炸（煙塵＋電弧）位置偏高（⏳ 未編譯未實測）**（2026-11-09，卡頓修好後作者才注意到）：煙塵與電弧對準 `BodyCenterWorldPos`，倒地期間查的是 Dead 幾何，而 Dead 幾何量的是 dead **第一幀（還站著）** ⇒ 身體中心＝站姿半身高，趴著時就偏高。改：`PlayerAnimator.CacheGeometry` 的 Dead 改用**最後一幀（趴姿）**的可見框算高度與底緣（pivot 照舊用第一幀）；新增 `MapSpriteLoader.GetFrameAlphaBox`（任一幀內容框）、`PlayerSpriteLibrary.TryGetVisibleBoxLastFrame`，預熱也先掃這張。影響範圍：倒地／趴著／爬起期間的 `VisibleBodyHeight`／`FeetWorldPos`／`BodyCenterWorldPos`（玩家死亡倒地時也是），都變成趴姿的真實值。文件：BLOODLINE §5〈尺寸與定位〉。
* [x] **變身卡頓第二輪：預熱到建 Sprite＋像素掃描零垃圾（⏳ 未編譯未實測）**（2026-11-09，作者：第一輪好一點但換外型還是明顯卡）：剩下的大頭是 ① `Sprite.Create` 的 Tight 網格（512px 一張數毫秒，體型≠1 還整套建兩次）② `GetPixels32` 每次 1MB 垃圾觸發 GC。改：`PlayerSpriteLibrary.GetFrames` 逐幀取已套 pivot 的 sprite（不再白建一套）、`PrewarmRoutine` 加分幀建 Sprite（`PlayerAnimator.ResolveTiles` 抽出來與 Setup 共用，`BloodlineSystem` 傳目標高度＝`CharacterWorldHeight×新體型`）、像素掃描改 `PixelsNoAlloc`（重複使用暫存陣列）。詳見 [PROBLEMS.md](PROBLEMS.md) E47 第二輪。
* [x] **喝血統藥變身卡頓：分幀預熱新外型（⏳ 未編譯未實測）**（2026-11-09，作者回報每次換外型那一刻畫面停住）：原因與解法見 [PROBLEMS.md](PROBLEMS.md) **E47**——換外型時同步讀約 80 張 512px 序列圖＋多輪全幀掃像素。新增 `PlayerSpriteLibrary.PrewarmRoutine`（每幀 6ms 預算讀檔解碼，再分幀算體積尺度／可見框／攻擊起訖幀），`BloodlineSystem` 開演時啟動 `PrewarmTransform`（新外型＋揭示面板新舊兩張立繪），趁倒地 2.5 秒做完。同日：`DreamTutorialFlow.DebugForceBloodlineId` 改回 0（新手夢境恢復隨機）。
* [x] **萬魂鬼手第三版：追蹤不再追牆、咬穿後會繞回來再咬、拿掉命中特效（⏳ 未編譯未實測）**（2026-11-09，作者回報「往右射、怪在遠處時鬼魂會先往上飄走繞一圈才回來打怪」）：根因是**追蹤找的是 PierceableLayers，而穿牆配方（`BlockedByEnvironment 0`）的 PierceableLayers 含牆**——鬼頭去追上方的牆，穿過牆（記入已命中）才改追怪。**這是舊 bug，任何「追蹤＋穿牆」的武器都會中**，只是之前全場鎖最近、牆通常不是最近的才沒被注意到。修法：彈道套件 `BulletInstance.HomingLayers`（新欄）、`BallisticsEngine.Spawn`／`Internal_SpawnSplit` 加選填參數 `homingLayers`（沒給＝舊行為）、`SplitBehavior` 傳給分裂子彈、`HomingBehavior` 改找 HomingLayers；主遊戲 `WeaponCastService`（玩家與怪共用的發射）傳 `ctx.TargetLayers`、`PlayerController` 兩處（落點分裂、命中迸發子武器）傳 `EnemyLayer`。另外作者要「穿透後轉一圈回來再咬」：`HomingBehavior` 分散模式（HomingRadius&lt;50）咬到目標 0.5 秒後清命中名單（舊行為不清）。武器 77 拿掉命中特效 56（骷髏煙，跟怪物死亡特效重複）。
* [x] **萬魂鬼手第二版：彈道套件加「飄移」與「分散追蹤」（⏳ 未編譯未實測）**（2026-11-09，作者退件第一版「像操控不靈的飛彈，往左射鬼魂卻從右邊出來」，同意改彈道系統）：原因 ① 放射 288°（往後也有）② `HomingBehavior` 每顆都鎖全場最近（半徑 50），5 顆一起慢慢轉向同一隻。改法：**BallisticsSystem** 新增 `Behaviors/WanderBehavior.cs`（＋.meta）——方向繞原方向做正弦擺盪、每顆隨機相位／頻率、只轉變化量所以平均方向不偏；`HomingBehavior` 加索敵半徑參數，**&lt;50 時只追半徑內、在最近 3 隻隨機挑**，沒怪時每 0.15 秒才重找；`ProjectileData` 加 `HomingRadius`（預設 50＝舊行為）／`WanderAngle`。主遊戲：`WeaponModeSpec` 加兩欄（只給 Normal）、`RecipeEntry.FromFields`／Clone、`RecipeManager` 子彈複製都帶上；RecipeTable 表頭尾端加兩欄、只填 92。配方 92 改：2 波×5 顆、前方 100°、速度 4.5、飄移 35、索敵 5、追蹤 200、穿透 3、飛 4.5 秒、間隔 0.9；武器 77 `BulletScale` 8.4→5.5、判定 0.5。其他武器沒填新欄＝行為不變。同日 PortraitTable：鬼差改 1.15／−139、鍾馗改 1.35／−255（照〈頂端 975 規則〉，作者同意）。文件：[RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md)、[BALLISTICS.md](BALLISTICS.md)、[BLOODLINE.md](BLOODLINE.md) §5d。
* [x] **立繪大小的規範：〈頂端 975 規則〉**（2026-11-09，作者要求「做個規範」）：對話立繪是量內容框自動對齊、人物頂端一律在 1027，PortraitTable 只放大會頂出畫面、只下沉會變小又離頂太遠（酆都 −90 被退）。⇒ 規則：只決定 `Scale`，`OffsetY = 528 − 580 × Scale`，頂端固定≈975；全身立繪建議起手 1.15。寫進 `PortraitTable.csv` 檔頭與 [DRAMA.md](DRAMA.md)〈固定微調〉。酆都 6 列改 1.15／−139；作者同意後鬼差同改 1.15／−139、鍾馗 1.35／−255（OuterToScreen 1 不變）。
* [x] **新手夢境：寫死酆都＋補酆都夢境台詞（⏳ 未編譯未實測）**（2026-11-09，作者委託）：① `DreamTutorialFlow.DebugForceBloodlineId` 0 → **92**（作者要的作弊，測完要改回 0，已記 TODO）。② 發現隱含契約撞號：夢境台詞 drama ＝ `31 + SeriesId`，萬魂是系列 9 ⇒ 40，但 drama 40 早就是「新手夢境教學-初始洞窟對話」——抽到酆都會播錯對話。⇒ 表A 加 `DreamDramaId` 欄（`BloodlineSeriesDef.DreamDramaId`，留空走舊規則），只有萬魂填 83；`DreamTutorialFlow.BloodlineDramaId` 優先用它。③ DramaTable 83「新手夢境教學-酆都大帝」(TalkGroup 80)、DramaTalkTable 225 → `lang:1013`；LanguageTable 1013（夢境段）「在夢中，我是幽冥之主，一聲令下，千萬冤魂便隨著我出巡」＋英文。文件：BLOODLINE §2 表A、§7 第 7b 步。
* [x] **酆都大帝專屬夢境武器「萬魂鬼手」（⏳ 未編譯未實測，只動資料與素材）**（2026-11-09，作者委託）：作者要「萬鬼追魂」——攻擊分裂成約 5 隻、會追蹤、穿透高、速度不快，滿場鬼魂到處咬人。WeaponTable／ItemTable **77**、RecipeTable **92**（Normal：5 顆放射 288°、速度 5、追蹤 180、穿透 5、穿牆、存活 4 秒、間隔 0.7），表B 92 的 `DreamWeaponId` 67（暫借萬蟲巢）→ 77。子彈用素材庫 `pj3_death_wave/black`（引魂幡用的是同一組的紫色版，這把刻意換灰黑，跟引魂幡分開），4 倍最近鄰放大存 `Resources/Weapon/animation/soulGhostSkull/`（.meta 以 soulBannerSkull 為範本換新 GUID），其餘子彈參數照引魂幡 37。icon `Resources/UI/Icons/Equipment/icon_soulhand.png`：ChatGPT「燃燈劫」專案、附兩張已定稿武器 icon 當風格錨點＋酆都立繪的鬼手裁切，一次出圖即通過（暗紫黑鬼手＋古銅枷鎖＋斷鏈，紫色只在爪尖與裂紋）。詳見 [BLOODLINE.md](BLOODLINE.md) §5d。
* [x] **酆都大帝神格特效：身後黑霧＋霧中凝結骷髏（⏳ 未編譯未實測）**（2026-11-09，作者委託）：新元件 `Scripts/Gacha/BloodlineMist.cs`（照 `BloodlineHalo` 模式：獨立物件、畫在角色之下、`BodyFxVisible` 關閉時隱藏）＋新 shader `Resources/Shaders/BloodlineMist.shader`（複製血霧衛士 `BloodMist` 的骨架：兩張煙霧密度圖＋domain warp，改成往上竄的焰形、本體近黑＋窄紫勾邊；`BloodMist` 原檔不動）＋骷髏貼圖 `Resources/VfxEffects/BloodlineMist/soul_skull.png`（ChatGPT 產＋去背）。骷髏每 2.5～4.5 秒用煙霧密度圖溶解凝結 → 停 1 秒 → 上方先散往上飄，左右肩輪流。
  資料：表B 新欄 `MistStyle`（第 28 欄），**只有 92 那列填 `SoulSkull`**，其他列不補。`BloodlineSystem.ApplyGodhoodFx` 加第 ⑦ 段。調參先用 numpy 照同一套算式＋該隱 idle 合成預覽四輪（結論：純黑讀不出形狀要留窄紫邊、紫太多變紫火、骷髏 uv 要修寬高比）。詳見 [BLOODLINE.md](BLOODLINE.md) §5b、實測重點見 [TODO.md](TODO.md)〈萬魂血統〉。
  同日作者放入正式序列圖實測後：① `BodyScale` 以鬼差為 1 ⇒ 鍾馗 1.7、酆都 1.3；② 霧太高（約 1.5 個角色高，要 1.2）⇒ `BloodlineMist` 底部不動、頂端收：`HeightFactor` 2.4→1.55、`CenterAboveBody` 0.30→−0.125，`SkullUv.y` 0.56→0.74、`SkullDrift` 0.09→0.14（換算到同樣的世界位置）。
  ③ 鍾馗攻擊動畫太快 ⇒ 表B 新欄 `AttackAnimSpeed`（索引 28，只填 91＝0.6），`PlayerAnimator.AttackFpsMul` 乘在 attack 幀率上，由 `BloodlineSystem.ApplyTo` 每次套用寫入；只影響動畫、不影響武器攻速（BLOODLINE §2）。
  ④ **鍾馗 6 張立繪全部重繪**（作者指出扛在肩上的大刀不對）。**第一次重繪（AI 自產母版）被作者退件，比原本更糟**：AI 只驗「刀刃在柄的延長線上」，柄卻是斜著穿過臉——數學上共線、物理上根本沒扛在肩上，驗收方法本身錯了。**定案做法：作者自己產了一張刀擺對的 normal（柄幾乎水平靠在右肩、從後頸穿過、刀刃從頭後左側露出），AI 以它為母版用 ChatGPT 產其餘 5 張**，prompt 用文字把這個擺法寫死＋LOCKED，每張收回來與母版並排比對刀與握刀手，六張都一致才放進 `Talk/Soulborn/ZhongKui/`（normal 就是作者原圖）。位置維持作者原圖（眼線約 y=228、環首到 x≈1004，與規格 330／880 不同，等作者決定要不要統一下移）。
  ⑤ 作者實機看對話：三階立繪頭都頂到畫面最上面、鍾馗太小 ⇒ `PortraitTable.csv` 加 18 列（每階 6 情緒）：鬼差／酆都 OffsetY −90（人物頂端 1027→≈937）；鍾馗 Scale 1.35＋OffsetY −270＋OuterToScreen 1（頂端≈960、上半身放大、下半身藏進對話框）。⚠ 對話立繪是量**不透明內容框**自動對齊的（人物頂端一律 1027），所以在圖檔畫布裡上下移圖沒有用——上一條「統一下移 100px」的提議作廢。
  ⑥ 血統揭示面板（`BloodlineIntroPanel`）裡酆都明顯比另兩階低：那個面板是**整張畫布等比縮進框、靠下對齊**（不量內容框、也不吃 PortraitTable），酆都沒戴帽、頭髮頂在 y=164，上方留白多就顯得矮 ⇒ 六張以畫布底、x=470 為錨等比放大 1.09（`tools/portrait_xform.py`），頂端 164→約 40、頭高約 250→272（更接近規格 300）。對話面板量內容框，不受影響。原圖備份 `TempImage/bloodline_Netherborn/portrait/fengdu_pre_scale109/`。
  ⑦ 同一個面板裡主角（輪迴回人類時的 `Talk/Base/normal.png`）也偏低（半身構圖，頭髮頂在 y=143）⇒ **只改 normal**（揭示面板只用 normal），以畫布底、x=512 為錨等比放大 1.073，頂端 143→約 41（對齊鬼差）。對話面板量內容框，不受影響。原圖備份 `TempImage/bloodline_Netherborn/portrait/base_pre_scale/normal.png`。⚠ Base 其餘五張情緒本來就不照規格（頂端 33～233 不一），沒動。被退的兩組備份：`portrait/final_old_badblade/`（最初）、`portrait/rejected_zk2/`（第一次重繪）。
* [x] **新增萬魂血統（Soulborn：鬼差 → 鍾馗 → 酆都大帝），可喝藥變身＋對話立繪正確；序列圖暫代（⏳ 未在 Unity 實測）**（2026-11-09，作者委託；第一次由 AI 主導血統美術）：
  ① **美術（AI 操作 ChatGPT「燃燈劫」專案產圖）**：三張 1024 角色圖（照 [art_direction/CHARACTER_ART_SPEC.md](art_direction/CHARACTER_ART_SPEC.md) §3，去背＋程式對齊腳底 864／兩腳中點 512）——鬼差像主角、鍾馗由作者加上角面具（作者給黑底圖，直接去背沿用，未重產）、酆都經四輪改稿定為「年輕男子＋巨大漆黑鬼手」（書由程式擦掉）；18 張立繪（6 情緒 × 3，鍾馗全程戴面具、表情靠眼孔眼神與肢體）；藥瓶成品圖 `bloodline_Soulborn`（紫液＋儺面鬼王章＋鬼爪瓶塞，比照既有 8 瓶）。約 40 次生圖額度。
  ② **放進專案**：`GameAssets/Main/Characters/Talk/Soulborn/{Warden,ZhongKui,Fengdu}/<情緒>.png`、`Resources/UI/Icons/Items/positions/bloodline/bloodline_Soulborn.png`（.meta 以 Swarmborn 為範本換新 GUID）；**序列圖暫代** `SequenceImage/Soulborn/*` 複製自龍奴／尼德霍格／該隱（只複製 png）。
  ③ **資料**：表A 第 9 列、表B 90～92（佔位數值、BodyScale 1、WalkSpeed 空；酆都 `DreamWeaponId` 暫借萬蟲巢 67）、ItemTable 309／329／339（文案只寫形貌）、`BaseBloodRoll` 309 權重 10、`ShadowAnchorTable` 12 列（暫代來源複製、auto）。**零程式改動**。
  ④ **作者要做**：Unity 跑 `Project Tools → Sync Map Assets`（新圖進 StreamingAssets/catalog；AI 沒跑 `Tools/sync_map_assets.sh`，因為它會無條件覆蓋整個 MapAssets 並從編輯器拉地圖）。實測重點與後續缺口見 [TODO.md](TODO.md)〈萬魂血統〉、[BLOODLINE.md](BLOODLINE.md) §8。原始工作檔在 `TempImage/bloodline_Netherborn/`（含舊版無面具鍾馗、酆都 v1～v5）。
* [x] **Boss 開戰資訊改用劇情「角色介紹」版面（分支 `feat/ui-restyle2`，未 commit；⏳ 未編譯未實測）**（2026-11-09，作者委託）：作者看了艾爾文的 `intro` 覺得比舊的「左頭像＋右水墨名牌」好看 ⇒ `bossIntro` 中段換成 `CharacterIntroPanel`。作者拍板：① 黑霧、「強敵現身」保留；② 進場＝整張淡入＋名字扭曲浮現（沿用 `NameWarpEffect`）；③ 名字統一宋體（思源宋體，不再用毛筆字）；④ **立繪一律在左**、說明在右；⑤ 芙蓉／榕樹妖的稱號與說明由作者提供。
  程式：`CharacterIntroPanel` 加 `Show(..., backdrop)` 多載（Boss 用 false：底下是黑霧）、`Alpha`（CanvasGroup）、`SetNameReveal`——劇情 intro 不呼叫＝行為不變。`BossIntroPanel` 拿掉頭像／名牌／滑入滑出與相關欄位（`PortraitHeight`…`NameFontPath`），改 `IntroFadeInSeconds` 0.6／`NameDelay` 0.3（從淡入起點算）／`HoldSeconds` 1.2→**3.0**（要讀說明）／`IntroFadeOutSeconds` 0.5；介紹面板掛場景根，換人／OnClose／OnDestroy 自己清。全長約 8.7→10 秒。
  資料：`MonsterData.csv` 表尾加 `IntroTitle`／`IntroDesc`（索引 45／46，填 `lang:`／`reveal:`），只填 13、14、38、39、40；語言表新增 1107 倖存長女、1108 芙蓉說明、1109 千年成精、1110 榕樹妖說明（英文是我先翻的，作者可改）。芙蓉的名字與稱號都走 `reveal:redBridalReadLetter`，沒看過家書兩者都是 ???、說明照常顯示。
  舊名牌圖 `BossIntroPanelNameBG` 不再使用（檔案保留）。文件：[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) `bossIntro`、[CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §10.5、[MONSTER_SETUP.md](MONSTER_SETUP.md)、[LOCALIZATION.md](LOCALIZATION.md)。
  ⏳ 待作者：Unity 編譯、Reimport `LanguageTable.csv`／`MonsterData.csv`，打紅嫁衣（看過／沒看過家書各一次）、榕樹妖、血狂之爭雙首領各看一次。

* [x] **UI 外框改版第二＋三批：設定／抽選背景去冥婚造型、關閉鈕統一、小元件去流蘇與壓暗（對話框試換後退回）（分支 `feat/ui-restyle2`，未 commit；⏳ 未編譯未實測）**（2026-11-09，作者委託）：做法同第一批（鎖內容區、外框重畫、逐張量內容區），ChatGPT 燃燈劫專案出圖、以已核准的 Popup 為風格錨點。
  ① `SettingPanelBG`：拿掉屋瓦／帷幔／燭台／銅錢／獸面，改單一暗銅外框＋頂端小蓮花＋底部紅菱；兩條音量列（🎵／🔔圓章＋刻度凹槽）照原位重畫。GPT 版兩條凹槽比程式 `MusicRailY/SfxRailY`（528／772）高 12～13px ⇒ 程式把兩列整段下移 12px（上方空帶用平面補），量過凹槽中心回到 528／771，程式不動。
     同日作者嫌上下留白太多 ⇒ **比照倉庫收緊**：上框（含蓮花）下移 183px、下框（含紅菱）上移 133px，內框到音量列／離開鈕各留 70px；畫布與其他座標不變，只有 `SettingsPanel.CloseCy` 150 → **333**（關閉鈕跟著框角下移）。
  ② ~~`DramaPanelBG`／`DramaPanelNameBG`~~：曾換成新外框（名牌用切角牌母版），**作者看過預覽覺得舊的好很多，同日退回 11-08 版**（與 HEAD 相同），對話 UI 不換。
  ③ `GachaPanel_Bg`（作者選「保留場景插圖」）：只做最小修改——四條紅流蘇改成銅鈴墜、兩支蠟燭換成青銅酥油燈（呼應燃燈），佛像浮雕／蓮花燈／地磚高度／暗度不變。
  ④ **關閉鈕統一**（作者指定）：`ForgingPanel` 改用 `CloseBtn_normal/pressed`（兩態 SpriteSwap，同 SettingsPanel），`ArtClose` 換規格、`CloseW` 88→106（螢幕上與設定面板的關閉鈕同為約 66 單位）；`Common/CloseBtn_2.png`＋.meta **已刪除**（作者授權；備份在 `TempImage/ui_restyle2/applied_backup/`）。[FORGING.md](FORGING.md)、[TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md) 的引用已改。
  組裝預覽 `TempImage/ui_restyle2/preview/`（settings_cmp／settings_tight／gacha_cmp；dialog_cmp 是已退回的版本）。作者決定不換：滑桿把手 `DragIcon`（獅面）、返回圖示（佛像）。保留：`SelectScriptPanel_SpecialFrame` 的「囍」（紅嫁衣專用框，刻意）。
  **第三批（同日接著做，零程式改動、零出圖額度）**：
  ⑤ `ClearStagePanel_ReturnBtn`（結算＋抽選總結鈕）：只把左右鍊子＋紅珠＋紅流蘇的像素設透明（量過鍊子欄 x 180~213 與外框本體 x≥214 分得開），牌子本體逐像素不動；GachaPanel 的 `ArtSumBtn` 規格仍沿用舊邊界框，牌子位置不變。
  ⑥ `LoadingBarPanel_ProgressBarBg`：只擦兩端蓮花端蓋下方（y≥135）的吊飾，進度條與端蓋逐像素不動（程式以整張比例對齊軌道，不受影響）。⚠ `LoadingPanel.BarBottomMargin` 原本把吊穗算在離底距離裡，拿掉後進度條視覺上約高 50 單位；要貼回原位改這個常數即可（未改，待作者實機看）。
  ⑦ `ScriptsPanel_Input`：石磚框 → 背包頁碼底板同款（等比縮成與舊框同為可見 320/500，內面約 160 單位，裝得下 150 的劇本格）。
  ⑧ `GachaPanel_SelectFrame`（亮金）亮度 ×0.74、`GachaPanel_StartIcon` ×0.80（只壓亮度、幾乎不降飽和——降飽和會讓金色發綠）。
  對照圖 `preview/batch3_cmp.png`、`batch3_fix.png`。原圖都在 `applied_backup/`。

* [x] **新增通用 hover 小提示 `HoverTip`；倉庫重整鈕第一個用（⏳ 未編譯未實測）**（2026-11-09，作者委託；作者說之後很多地方會用到）：新檔 `Scripts/UI/HoverTip.cs`——全遊戲共用一份的「一行短字」提示（深石底＋細古金框、淡骨色字），滑鼠停 0.3 秒後浮在元件**正上方**（上方放不下才放下方、左右夾在畫面內），移開／按下／元件停用就收，面板關閉不用手動收。掛法一行 `HoverTip.Attach(go, 語言表id)`，也有 `Func<string>` 版。掛 `UILayer.Popup`（同 ItemTooltip 的理由，PROBLEMS E34）、本體 raycast 全關；static 只存單一 Unity 物件，關 Domain Reload 也會自動重建（I2／I8）。刻意不併進 `ItemTooltip`：那個是物品說明（大圖＋屬性、跟游標），兩者用途不同。語言表新開 **9001–9099 通用 UI 小提示**段，9001＝「重新整理」／英文暫定 `Sort`（功能其實是排序整理）。`StoragePanel` 重整鈕加一行 Attach。同日追加（作者實機看過倉庫後）：背包重整鈕也掛「重新整理」；背包金錢牌掛 **9002「通用貨幣」／Currency**（作者要求寫泛用，之後貨幣可能不是錢）——金錢牌是底圖畫的，另疊透明感應區 `MoneyHover`（見 [INVENTORY.md](INVENTORY.md)）。[UI_SYSTEM.md](UI_SYSTEM.md)〈共用視覺元件〉、[LOCALIZATION.md](LOCALIZATION.md) 分段、[STORAGE.md](STORAGE.md) 已同步。**要在 Unity Reimport LanguageTable.csv**。

* [x] **UI 外框改版第一批：倉庫＋Popup 去掉冥婚造型、改成背包語彙（分支 `feat/ui-restyle2`，未 commit；⏳ 待作者實機看）**（2026-11-09，作者委託）：11-08 那輪是「鎖外形只換材質」，所以倉庫／Popup／設定等介面還留著紅嫁衣冥婚的**造型**（屋瓦、帷幔、紅燭、銅錢、獸面門環、流蘇、牌位）。這批改成「**鎖內容區、外框重畫**」：內容窗口／格網／按鈕位置不動 ⇒ **零程式改動**，外輪廓允許變。換了 10 張：
  ① `StoragePanelBG`、`PopupPanelBG`（ChatGPT 燃燈劫專案，附舊圖＋背包底圖當錨點）：單一暗銅外框＋切角尖刺＋細古金雙線；頂端焦點倉庫＝小寶箱徽、Popup＝小蓮花，底部小紅菱。逐張量內容區（`tools/verify.py`：關鍵矩形須不透明、平整深色）。Popup 第一版內框底線比舊圖高 27px、會被確定／取消鈕壓到 ⇒ 程式把 y≥600 整段下移 30px（內框中段是平面，看不出接縫），底線回到與舊圖一致的 y≈881。
  ② 頁籤 `CellBG_normal/pressed`、`Common/LongBtn_normal/pressed`：同一張 GPT 切角牌母版衍生——normal 頁籤＝深面，選中頁籤與 LongBtn＝程式把內面換成背包選中頁籤的深緋紅（`tools/redface.py`），LongBtn 按下＝更暗。四張外框完全一致。⚠ **LongBtn 連動**：ConfirmPopup、ChoicePanel、SettingsPanel 的離開／返回鈕一起變（作者已同意）。
  ③ 重整鈕底 `RefreshBG_normal/pressed`：圓舷窗＋鉚釘 → 直接沿用背包 `inventoryPanel_Cell_PageNum_Bg`（按下＝紅面版）。
  ④ 格線 `StoragePanel_GridBlank`（從新底圖同位置裁）、`StoragePanel_CellFrame`（改用背包格線單元，色調微調 ≤3）。[STORAGE.md](STORAGE.md) 素材表已同步。
  ⑤ **倉庫外框收緊＋重整鈕移右上（同日，作者看預覽後要求；⏳ 未編譯未實測）**：外框換新後四周留白太大。作者否決「放大格子」（格子刻意與背包等大）與「塞裝飾」，選收緊——內面是平面，程式把四邊框往內平移（左右 70、上 70、下 168px），畫布與內容座標不變；`GridBlank` 跟著從收緊後的底圖重裁。`StoragePanel.cs` 改常數：重整鈕 `RefreshCx/Cy/Size` 765/1250/130 → **852/250/90**（寶箱徽右側、第 V 頁籤正上方）、`PairLeftX` -416 → **-366**；重整鈕圖示內縮由寫死 30 改成 `RefreshSize×0.23`（130 時結果不變）。⚠ 收緊後倉庫可見範圍上下不對稱（上收 70、下收 168），並排時倉庫視覺中心會比背包高約 50 單位——要對齊需加一個 Y 偏移（`anchoredPosition` 目前寫死 y=0），待作者實機看再決定。
  `Cell_number` I～V、`RefreshIcon`、Popup 勾／叉 icon 本來就符合，保留。原圖（11-08 版）備份在 `TempImage/ui_restyle2/applied_backup/`，組裝預覽（照程式座標拼的舊／新對照）在 `TempImage/ui_restyle2/preview/`；退回單張可 `git checkout -- <路徑>`。工具複製在 `TempImage/ui_restyle2/tools/`（gitignore）。
  **下一批（作者已分批）**：SettingPanelBG（底圖畫死兩條滑桿槽）、DramaPanelBG／NameBG、GachaPanel_Bg（重畫；ForgingPanel_Bg 不動）、ClearStage_ReturnBtn／LoadingBar_ProgressBarBg 的紅流蘇、關閉鈕兩款統一、ScriptsPanel_Input、Gacha 亮金件；SelectScriptPanel_SpecialFrame 角落的「囍」字待作者確認是否刻意。

* [x] **立繪整理＋立繪省記憶體（⏳ 程式未編譯未實測）**（2026-11-08，作者委託）：
  ① **程式**：立繪載入後先量內容框、再 `MapSpriteLoader.CompressAndLock`（DXT5＋丟 CPU 副本，每張約 17MB→2MB）；進新關卡時 `DramaTalkDatabase.ReleaseLoadedPortraits()` 釋放上一關的立繪貼圖。只動立繪路徑（`DramaTalkDatabase` 專用 loader），地圖素材／怪物／主角序列圖不受影響（它們之後還要讀像素算碰撞與影子）。見 [DRAMA.md](DRAMA.md)〈立繪的記憶體〉。
  ② **只調畫布、不重畫**（9 張）：Base 的 fear／happy／hurt／normal（1122×1402 等 → 1024×1536，內容等比縮到不超寬、貼底置中）、該隱 cry／happy／hurt 與血伯爵 fear（1023×1537 → 1024×1536，平移 1px）、紅嫁衣 `redBridalGown_cry`（2000×2000 → 同組的 1254×1254，4.7MB→1.5MB）。遊戲內排版靠內容框自動對齊，畫面上的大小與位置不變。
  ③ **重畫**（3 張，ChatGPT 燃燈劫專案）：寄生蟲 `normal`（485×514 → 1024×1536，以同組 angry 為母版、換平靜表情）、狼人士兵 normal／fear（768×921 → 1024×1536，照新規範構圖；灰毛改用洋紅底去背）。
  原圖備份在 `TempImage/ui_restyle/portrait_backup/`；`GameAssets` 與 `StreamingAssets` 兩份已同步、`catalog.json` 的 `pixelSize` 已改（保險起見可再跑一次 Sync Map Assets）。規範補「寬體型例外」（作者指定才可用 1536×1024）與「灰色系角色用洋紅底」。
* [x] **背包圖示第二批：26 件武器／技能改用專用圖示、血統藥 10 張重畫、讀取條左端修正（分支 `feat/ui-restyle`，未 commit；⏳ 待作者實機看）**（2026-11-08，作者委託；ChatGPT「燃燈劫」專案內產圖）：
  ① 背包裡 26 件武器／技能的 icon 原本借用 `Resources/Weapon/single/` 的投射物圖（19 件）或特效動畫第一幀（21 血月鬼爪、22 虛空吞口、24 九霄雷獄、25 幽影突、27 冰封法陣、28 死字咒、29 鏡界折光），不在 `UI/` 底下所以第一輪沒換到。改成新出 `UI/Icons/Equipment/icon_*.png`（500×500，各配 .meta），ItemTable 這 26 列只改 `IconPath`；投射物圖與特效幀完全不動，戰鬥畫面不受影響。特效類 7 件是依名稱重新設計的物件圖示。
  ② 血統藥 8 系列＋`lvup_middle/high` 重畫成同一款粗壯圓肚瓶（系列色＋瓶塞造型＋腹部銅章圖騰），原地覆蓋（原圖在 `applied_backup/`）。7～10 號是第二張圖示表，瓶頸略高、銅籠略華麗，若嫌與前 6 瓶不一致可再重出 SpiritRoot／Swarmborn。
  ③ 讀取條填充 `LoadingBarPanel_ProgressBar`：作者回報左端看起來凸出框外——外形改回逐像素等同原圖（alpha 沿用原圖），外緣補一圈淡金亮邊；顏色仍是新的霧金。
  對照圖：`TempImage/ui_restyle/weapon_icons_overview.png`（白天 18 件）、`evening_icons_overview.png`（晚上 8 件＋血統藥）。圖示表一次 6 個、共用 7 次生圖額度做完 36 張。
* [x] **新增 [CHATGPT_IMAGE_WORKFLOW.md](CHATGPT_IMAGE_WORKFLOW.md)：AI 自行操作 ChatGPT 網頁產圖並放進專案的操作手冊**（2026-11-08，作者委託）：記今天 UI 改版實跑的流程——開工前要跟作者要的 8 項（Mac 連線、專案與 ~/Downloads 授權、Chrome 連線＋ChatGPT 登入、chatgpt.com 多檔下載白名單、Mac 不睡眠、git 規則、範圍清單）、雲端容器↔Mac 兩端分工、tools/ 六支腳本用法與參數選擇、單張迴圈的確切操作與座標、圖示表批次法、prompt 規則、約 30 條踩雷與解法（瀏覽器／取檔／圖本身／Mac 端）、收尾步驟。README 文件地圖與 AI_IMAGE_GEN_GUIDE 已加連結。另：CHARACTER_SPRITE_GUIDELINE 委託清單「朝左或朝右」改為一律朝右。⚠ 工具目前在 gitignore 的 `TempImage/ui_restyle/tools/`，是否搬進 repo 待作者決定。
* [x] **新增角色美術技術規格 [art_direction/CHARACTER_ART_SPEC.md](art_direction/CHARACTER_ART_SPEC.md)（立繪＋遊戲內角色圖）**（2026-11-08，作者委託）：量了全部現有素材（立繪畫布 12 種尺寸；60 個角色 idle 在 256 畫布的腳底離底 21~91px、中心 104~146、同角色動作間身高差最多 22%），找出 PortraitTable 11 列、影子表 manual 24 列、MonsterData 逐動作倍率 17 個值的共同根因＝構圖不一。規格把對齊線用像素寫死：立繪 1024×1536、頭頂 180／眼線 330／頭高 300／畫到腳踝碰底邊／面向側右界 880；基礎圖 1024×1024、頭頂 176／腳底 864／中心 512、最底帶只能有腳；序列 256 單幀腳底 216±3、身高 ±4%。另含退件清單、三個 prompt 範本、驗收數值。同步修正抵觸：兩份 CHARACTER_*_GUIDELINE（黑底→灰底、朝向一律右、補對齊線）、art_direction/README 分工表、[DRAMA.md](DRAMA.md)〈立繪自動對齊〉「給什麼尺寸都不用管」一句。既有素材不追溯。**待作者決定**：立繪全面合規後 `PortraitFit` 改用固定對齊線排版；收圖驗證工具。
* [x] **UI／圖示美術準則改寫成硬性規範，並統一其他美術文件**（2026-11-08，作者委託）：依今天 UI 改版的實際結果，把 [art_direction/UI_ICON_GUIDELINE.md](art_direction/UI_ICON_GUIDELINE.md) 整份重寫（色票取樣自定稿素材、四種材質、純灰 #808080 輸出、外形鎖死、文字底色對照、圖示表批次出圖、10 條退件清單、prompt 範本、驗收清單），可直接貼給 ChatGPT。初版允許的水墨／舊紙／木紋／腐蝕／顆粒質感全部廢止。同步修正抵觸處：[ART_DIRECTION.md](ART_DIRECTION.md) §7（UI 材質改為暗石／暗銅／骨／漆器，細節以 UI 準則為準）與附錄 A 第 1 條（UI 不跟場景色彩劇本）；[AI_COMMISSION_WORKFLOW.md](art_direction/AI_COMMISSION_WORKFLOW.md) §12；[AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md) §6（像素風只限世界內素材，UI／icon 不是像素風）；[INVENTORY.md](INVENTORY.md) icon 規格（新圖 500×500、不可借投射物圖或特效幀）與頁籤顏色。場景、角色、立繪準則與 UI 無抵觸，未動。
* [x] **UI 美術改版第一輪：Resources/UI 換掉 115 張（分支 `feat/ui-restyle`，未 commit；⏳ 待作者實機看）**（2026-11-08，作者委託；AI 用 ChatGPT 網頁逐張重繪）：方向 B「暗石＋暗銅＋細暗古金、低雜訊、無光暈、去紫」，**版型與尺寸全鎖死、零程式改動**。換了：BottomControlPanel_Bg、Icons 43 張（裝備／道具／寶石底座與符號／血統藥）、Inventory 13、Storage 13、Common 14（紫色導引改金）、Forging 3、Gacha 6、Setting 3、Popup 3、Drama 2、SelectSave 3（ActorBase／ActorBg／Frame）、SelectScript 3、Scripts 2、Bloodline 2、ClearStage 2（ItemBg／ReturnBtn）、LoadingBar 的 ProgressBar（亮黃→霧金）、SceneTipPanel_Bg（血紅潑濺→暗銅分隔線）。每張都用 alpha IoU／邊緣相關自動驗形狀（多數 ≥0.9；細線、刻意去火焰的圖示例外，目視確認）。原圖備份在 `TempImage/ui_restyle/applied_backup/`，總對照圖 `TempImage/ui_restyle/ui_restyle_overview.png`；要退回單張可 `git checkout -- <路徑>`。<br>
  **刻意沒換**：Texts/tw、en（GPT 中文字不可靠）、SpeechBubble 與 InGame_TalkBg1/2（水墨對話泡泡同一類）、TitlePanel 佛像 8 幀、SelectScriptPanel/StageImage 與 SelectSavePanel_Bg（場景插畫，本來就暗）、BossIntro 煙霧與 NameBG（毛筆牌）、bloodline_BaseBottle（`BloodlineUseLogoOverlay=false` 沒在用）、LoadingBar 的 ProgressBarBg／ProgressEnd（程式以比例常數對齊軌道，且原本就是暗銅）、CharacterIntro 四張（本來就是細金線）。ClearStagePanel_GainItemBg 被 ChatGPT 以「暴力」拒畫，維持原圖。<br>
  **注意**：BloodlinePanel_NameBg 名字是深血紅字（`NameColor`），所以石碑刻意保留淺色面（第一版畫成深色已退回重做）。HUD 整體比原本略亮。
* [x] **修：劇情演出的演員沒有影子（✅ 作者實測 OK）**（2026-11-07，作者截圖；見 [PROBLEMS.md](PROBLEMS.md) **E46**）：作者回報「NPC 有的有影子有的沒有」——比對截圖，沒影子的夏綠蒂、管家都是**演出演員**，有影子的狼人是地圖 NPC。`CutsceneActor.Npc` 漏掛 `BlobShadow`，補上；`SetActive` 同步影子開關（despawn 時不留殘影）。怪物表上是飛行生物的（迎賓大廳 `assault_werewolf` 的 4 隻血蝠）刻意不掛（作者同意）；**第一版誤用演員的 `flying` 勾選判斷**，害直線離場的夏綠蒂／艾爾文沒影子，同日改查怪物表 BrainType。另查到管家換圖後影子錨點過期（舊表 512 畫布、腳底 135.6px；新圖 256、腳底 27px）：作者跑工具後 idle 重算，walk 因張數沒變被工具跳過（工具只看幀數）⇒ 刪掉 walk 列待重算。
* [x] **血狂之爭加卍字進場＋場景名、新增劇本道具 105；邪佛發牌暫時多放血狂之爭看排版（⏳ 未實測）**（2026-11-07，作者；見 [SCENE_TIP.md](SCENE_TIP.md) §5、[SELECT_SCRIPT.md](SELECT_SCRIPT.md) §2）：作者回報進血狂之爭沒有卍字、直接入場——原因是 MapsTable 17～26 的 `SceneTip` 全空（卍字與場景名共用同一個開關，SCENE_TIP §0.1b）。作者備妥素材（場景名字樣 tw/en、劇本 icon、選卡示意圖、關卡名 tw/en；英文名定為 **Blood Fang**），本次：① MapsTable 17～26 `SceneTip`=`BloodFang`，零程式；② ItemTable 新增 **105 劇本-血狂之爭**（`TargetMapId=17`，WeaponID 留空——104 的 `1` 對劇本無作用，未動）；③ `Main_Square` 的 `讓玩家選擇劇本`（selectScript）`scriptIds` 104→`104|105`，**作弊、只為看兩卡並排排版**，之後要改回（見 TODO）。另修素材檔名 `en/SceneTipPanel_Text_BloodFang.png.png` 雙副檔名（連 .meta 改名、GUID 不變）。**要 Sync Map Assets**（只為 Main_Square；CSV 不用）。
* [x] **修：慢速巡邏 NPC 折返時臉沒轉過來；邪佛立繪再往下沉（⏳ 未編譯未實測）**（2026-11-07，作者截圖）：① 邪佛廣場測試 NPC（速度 0.8）往左走臉朝右——`HandleVisuals` 的 FaceMovement 拿「每幀位移 > 0.002」當轉向門檻，高幀率下慢速角色每幀走不到這麼多，改看平滑後的 X 速度（見 [PROBLEMS.md](PROBLEMS.md) **F46**）。② PortraitTable `Buddha_normal` OffsetY -130→-230（寬螢幕下頭被切，頭頂≈1071→971，比照夏綠蒂）。
* [x] **大營（兵營）第一波改成皇家衛士 ×1＋戰狼 ×3（⏳ 未實測）**（2026-11-07，作者）：衛士留 (10,11)（仍負責接 `等待-第2波`），新增 `出生點-第1波-戰狼`（17）×3 於 (6,9)(14,9)(10,8)、群組 `清場-1`；一開始就擺好，不播出生特效。見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.10。
* [x] **修：換波時卡頓——讀取頁預熱每種怪的 Sprite／碰撞框／血霧材質（⏳ 未編譯未實測）**（2026-11-07，見 [PROBLEMS.md](PROBLEMS.md) **E45**、[RESOURCE_LOADING.md](RESOURCE_LOADING.md) §3）：作者實測第一波結束、第二波出來時非常明顯的卡頓。貼圖本來就預載了，卡的是「這種怪第一次生出來」才做的事：上百張 `Sprite.Create`（Tight 網格要掃透明度）、碰撞框像素框重讀重解碼同一張 PNG、血衛士第一隻才 `Resources.Load` 血霧 shader 與煙霧圖。改：`MonsterAnimator.Setup` 的算倍率＋取幀抽成 `LoadFrames`（＋`PrewarmFrames`），`MonsterController` 的顯示倍率抽成 `ComputeTileSize`、加 `PrewarmVisuals(MonsterData)`；`MonsterSpriteLibrary.PrewarmModuleRoutine` 在讀取頁對本 module 每列怪跑一次（進度 0.5~0.6，一隻一幀）；`BloodMistVisual.Prewarm()`；`GetAlphaLocalBox` 對動畫物件改用幀快取。讀取頁會多花一點時間（Console 會印「已預熱 … ms」）。
* [x] **修：劇本部族怪死了什麼都不掉（⏳ 未編譯未實測）**（2026-11-07，見 [PROBLEMS.md](PROBLEMS.md) **F45**）：作者改回掉落後重玩兩次，雙首領還是沒掉藥。根因不是掉落表——`MonsterController` 死亡時的掉寶段只認 `Faction == Enemy`，狼人／吸血鬼陣營的怪全部略過，所以血狂之爭整個模組的錢、必掉道具、隨機血球、清場血球一直都沒有。改成「已清」記錄看 SpawnKey（排除 PlayerAlly／Neutral），掉寶看死亡當下 `FactionRelations.AttacksPlayer`（結盟後我方守軍被殺不掉）。
* [x] **雙首領的藥劑改回掉在地上（⏳ 未實測）**（2026-11-07）：作者想保留撿戰利品的樂趣——撤掉同日加的「敗北後直接給兩瓶藥＋圖示提示」：40／41 `DropTableId` 1→2／3（必掉 303／302），迎賓大廳刪 `贈藥-雙-狂族`／`贈藥-雙-血族`，`對話-雙首領敗北` 直接接 `拒選過關`（倒數 15 秒）。新的敗北台詞保留。
* [x] **夏綠蒂對話立繪再往下沉、引路手指指向下方時改浮在頭上（⏳ 未編譯未實測）**（2026-11-07，作者截圖）：① 寬螢幕下夏綠蒂帽尖被畫面頂端切掉——PortraitTable 四張 `Duke_Charlotte_*` 的 OffsetY -90→-180（頭頂≈1053→963／1080）。② 拒選線清場的引路手指整隻蓋在玩家身上：`GuideFingerPanel` 世界模式是「手的根部貼在玩家錨點＋1.2、指尖轉向目標」，目標在上方（邪佛廣場）時手往上伸剛好在頭上，目標在下方時手往下伸就壓在身上。改成目標越往下，根部越往「指尖反方向」退一根手指長，正下方時指尖停在頭頂上方 14px（頭頂用 `PlayerController.FeetWorldPos＋VisibleBodyHeight`，PROBLEMS E14）；權重 `-tipDir.y`，目標在上方時位置與舊版完全相同（邪佛廣場教學不變）。
* [x] **拒選線：清場後主角提示＋引路手指、雙首領結尾改台詞並直接給兩瓶藥（⏳ 未編譯未實測）**（2026-11-07，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.4／§3.6／§3.7、[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) `guideFinger`）：作者：和談大廳／長廊清光後南門開了玩家看不到。新增觸發鏈 `guideFinger`（把新手教學的 `GuideFingerPanel.ShowWorldGuide` 開放給劇本，換圖時 `Setup` 自動收、只收自己叫的）；兩張圖的開門節點後接主角獨白（drama 81／82，語言表 3034／3035）→ 手指指向南門。雙首領敗北台詞改成作者要的語氣（drama 49 兩句改走語言表 3036／3037），對話後連續兩顆 `giveItem`（303→302，勾道具圖示提示，兩則同時上下排）再接原本的 `拒選過關`。40／41 的 `DropTableId` 2／3（必掉藥劑）改成 1，否則會拿到四瓶。
* [x] **迎賓大廳去程站崗改成四位門衛（⏳ 未實測）**（2026-11-07，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.6）：作者要把兩排 18 隻 NPC（血族士兵／血術士／血蝠、狂族士兵／弩手／戰狼各 3）改成「看顧大門」：西門上下各一位血族士兵（NpcTable 2）、東門上下各一位狂族士兵（NpcTable 3），面向大廳。東門右上 (19,9) 是牆，兩位都退到 x=18 保持上下對稱。消失旗標沿用 `bfWarStarted`。
* [x] **血狂之爭拒選線三修：圍攻血蝠上牆、雙首領一次開場、兩族聯手不互打（⏳ 未編譯未實測）**（2026-11-07，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.4／§3.6／§3.10、[FACTION.md](FACTION.md)、[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) `bossIntro`）：① 和談大廳第 1 波血蝠從門邊平地改掛左上方牆上 (8,8)(6,9)(4,10)。② 迎賓大廳「強敵現身」演兩次——`bossIntro` 的 `monsterId` 改成可填多位（`40|41`）：`BossIntroPanel` 抽出 `ApplyBoss`，「滑入→撐住→滑出」依人數重複（間隔 `BossGapSeconds` 0.25），霧等最後一位滑出才散；一位時行為不變。地圖改成 `首領-雙`(40|41) → 狂族首領出生點（生出來時）→ 血族首領出生點，刪掉第二顆 bossIntro。編輯器該欄改成文字欄（存檔本來就是字串，舊資料不受影響）。③ 雙首領互打、夏綠蒂旋轉不出血球——同一個根因：兩族開戰會互咬，她鎖定艾爾文，而子彈打的是目標那一層＝自己那一層，一出手就撞到自己（PROBLEMS **F44**）。`FactionRelations` 加 `TribesUnited`（兩族互不敵對、只打玩家與玩家的召喚物），`factionWar` 新勾選 `united`；拒選線 `拒絕選邊`／`開戰` 都勾，整條拒選線（圍攻、伏擊、雙首領）生效。
* [x] **按介面按鈕（演出 Skip 等）時玩家不再順便開火（⏳ 未編譯未實測）**（2026-11-07，見 [PROBLEMS.md](PROBLEMS.md) **F43**）：作者回報對話時按 Skip 會發射武器、按對話繼續則不會。對話面板開著時遊戲輸入本來就被擋；出事的是**沒鎖操作的演出**（頭上泡泡對話）——演出 Skip 是獨立覆蓋層，玩家開火只看左鍵按著沒。`PlayerController` 加 `UpdateMouseFireGate`：左鍵按下時若在可點擊的介面元件上（或介面正擋著輸入），這次按住都不算開火；四處讀左鍵的地方（開火、按住開火時轉身）改讀閘門結果。空白鍵開火、教學補一發不受影響。
* [x] **道具獲得提示加圖示樣式（⏳ 未編譯未實測）**（2026-11-07，見 [UI_SYSTEM.md](UI_SYSTEM.md)〈System 層〉）：作者覺得血狂之爭大門給藥的「獲得 XX」提示太不明顯。新增 `AlertPanel.ToastItem(itemId, count)`：圖示在上、「獲得 名稱 ×數量」在下、停 3 秒；滑鼠移到圖示上跳出共用 `ItemTooltip`、訊息不消失，移開再撐 0.6 秒才淡出。hover 用游標座標判斷而不是 raycast（System 層一律不擋點擊）；`ItemTooltip.Create` 加可選層級參數（預設仍是 Popup，背包／倉庫／鍛造不變），提示這裡傳 System 才不會被 toast 底色蓋住。文字走語言表 8002（`獲得 {0} ×{1}`）。觸發鏈 `giveItem` 新增勾選 `iconToast`（編輯器「道具圖示提示(重要獎勵用)」），**只有勾了才用新樣式**；目前只勾了大門兩顆 `贈藥-*`。<br>
  **補（作者實測）**：滑鼠停在圖示上時倒數結束、被傳回廣場，提示一路掛著不消失（`AlertPanel` 跨場景常駐、hover 時沒有任何東西能打斷它）。加 `AlertPanel.DismissItemToasts()`（遞增序號，活著的道具提示看到序號變了就立刻收掉＋關說明），在 `GameFlowManager` 卍字離場開始時與 `LoadSceneRoutine` 開頭呼叫；另外提示自己每幀檢查換圖（`IsLoading` 或 `CurrentMapId` 變了）。
* [x] **演出鏡頭可以跟著演員走＋部族怪面向改成跟追擊目標一致（⏳ 未編譯未實測）**（2026-11-07，作者實測狂族線）：① 艾爾文撤退時鏡頭先飛到西門、他才開始跑，看不到他撞碎右半邊圓桌。編輯器的 `cameraFollow` 早就能選演員（顯示「鏡頭跟 XX」），但遊戲端 `CameraFollowStep` 忽略 `actorId`、一律回玩家。補上：`CutsceneDirector` 記 `_camFollowActor`，`LateUpdate` 每幀 `SetFocusPoint(演員.VisualCenter())`，演員消失就停在最後位置；`camera`／`intro`／收尾會清掉。和談大廳四段（`exit_*`、`assault_*` 的敵方首領回門）改成 `cameraFollow(首領)` → `move` → `despawn` → 停 0.5 秒 → 回玩家。② 血族士兵追著玩家卻臉朝右——面向規則與 Brain 選目標的規則不一致，記為 PROBLEMS **F42**，`MonsterController` 改成面向「敵怪與玩家較近者」（PlayerAlly 例外）。這是全域行為變更：所有部族互咬的場面都會受影響（變得跟追擊目標一致）。
* [x] **和談大廳首領突圍直線衝刺、撞碎中央圓桌（⏳ 未編譯未實測）**（2026-11-07，見 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) 步驟表 `move`、[bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.4）：作者嫌己方首領撤離時繞過圓桌，想要直線衝向對面門口、順便撞爛桌子；並選擇讓圓桌平常也打得壞（「場地上盡量空曠，不然玩家很難閃」）。<br>
  **作法**：直線＝演員勾既有的 `flying`（關掉 A*／避障；演員本來就沒有碰撞，會直接穿過去）。撞碎＝演出 `move` 新欄位 `breakObjects`（主遊戲 `CutsceneModel`＋編輯器 `CutsceneData`＋編輯器勾選）：`CutsceneDirector.MoveStep` 每幀用半徑 0.7 的 `CircleCastAll` 掃「上一幀→這一幀」（衝刺快時一幀可能跨過薄的東西），路線上有 `DestructibleObject` 就 `TakeDamage(1e9)`，碎片順著衝刺方向飛；`FastForwardStates`（Skip）在瞬移前先把整條路線掃一遍，跳過的人也看得到桌子碎了。圓桌兩半 `hp` -1→1。不可摧毀（`hp=-1`）的物件不受影響。
* [x] **和談大廳突襲演出兩個修正（⏳ 未實測）**（2026-11-07，作者實測血族線）：① 衝進來的演員站定後全部看門口——`move` 的抵達朝向寫反了（跑的時候 `TickMove` 依方向自動轉向是對的，抵達後被 `facing` 轉回去）；兩條線都改成面向大廳。真怪生成時會面向玩家（`MonsterSpawner`），劇情中玩家在大廳中央，換手時不會轉頭。② 血族線開打時東南角三隻狂族站崗士兵瞬間消失——和平 NPC 的消失旗標是 `bfWarStarted`，但只有己方那側有出生點在原位接手。改成兩側站崗士兵在**兩條選邊線**都換成真怪（條件 `flag:bfWarStarted|!flag:bfRefused`）：己方三隻當守軍、敵方三隻跟著突襲軍打玩家；拒選線不變。
* [x] **血狂之爭和談大廳選邊後：敵方首領回門 → 敵軍衝入 → 突圍對話 → 己方首領撤離，演完才開打（⏳ 未實測）**（2026-11-07，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.4）：作者常常看不到首領撤離、怪就已經開打，也分不清「敵軍衝進來」和「首領撤退」誰先。<br>
  **原因**：選邊那一刻（`加入血族/狂族` 寫 `bfJoined*`）三件事同時發生——突襲軍與守軍直接在原位冒出來（出生點條件就是 `bfJoined*`，沒有衝進來的過程）、`實戰開戰` 接在 `加入*` 後面立刻開打、通報對話開始（對話暫停所以怪定格在背景）。對話完才播 `exit_*`，而那段沒鎖操作、沒動鏡頭、遊戲照跑，玩家正在被追，鏡頭跟著玩家⇒看不到。<br>
  **作法（作者拍板順序，零程式）**：`加入*` 不再接 `實戰開戰`（和平維持）；通報對話後插 `突襲演出-*`（新演出 `assault_vampire/werewolf`，鎖操作）：鏡頭跟敵方首領走回自己那側的門消失 → 鏡頭移到另一側門 → 12 個演員分三批衝進大廳站定 → 最後一步 `setFlag bfAssaultIn`（新旗標，關卡單次）＋收演員，同一刻真的怪在原位生出（和平中不動手；Skip 會快轉到這步，所以跳過也不會少怪）。`exit_*` 改寫成只有己方首領、鎖操作、鏡頭跟著跑到門口消失再回玩家，演完才接 `實戰開戰`。突襲軍從「每格隨機」改成固定配置（作者同意），落點從門口 1～3 格往大廳裡散開，才看得出衝進來；守軍條件改成 `bfJoined*＋bfWarStarted`，開打那一刻才和和平站崗的士兵交換（以前兩組會在同一位置重疊一整段劇情）。<br>
  **通則**：演出不暫停遊戲——要讓玩家看清楚的表演，**期間場上不能有已經開打的怪**；做法是用和平狀態（`factionPeace` 未結束）讓真怪先站著，演完再 `factionWar`。演員沒有交棒時會留在原地，但**下一段演出開演會被清掉**，所以「演員走到定位」之後要在同一段演出裡換成真怪。
* [x] **血狂之爭同伴會說話＋決戰時離場、大門道別後走出畫面（⏳ 未編譯未實測）**（2026-11-07，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.6／§3.8／§6.5、[MONSTER_SPEECH.md](MONSTER_SPEECH.md)〈劇本同伴的說話〉、[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3 `companion`）：作者要左右兩路的同伴首領（42 夏綠蒂、43 艾爾文）依個性在頭上說話、門開時催玩家走（5~6 秒一句）；回迎賓大廳時同伴先消失、打完首領再出來講話；大門給完獎勵說「下次再見」後往下走出畫面，玩家等結算。台詞作者授權 AI 依個性寫。<br>
  **為什麼不用 MonsterSpeech**：那套要「發現玩家」才講（同伴是 PlayerAlly，條件套不上）、只有 4 格句子、沒有「門開了」這種事件。改成：MonsterData 表尾加 `CompanionLines`／`CompanionUrgeLines`（索引 43／44，一格多句用全形「／」；只填 42／43，其他列留空）＋新元件 `CompanionSpeech`（`CompanionService.Summon` 有台詞才掛）。「門開了」＝`TriggerChain.PortalOpenedThisMap`：`EnableRegion` 解鎖傳送點時記下 id、換圖清空、被重新封住就不算——一進圖就開著的門與讀檔靠 enableFlag 啟用的門都不算，要的是「門剛被打開」之後。<br>
  **退場**：`companion` 動作加 `vfxId`（現身特效）、`leave`（立即消失／說完往下走）、`leaveLineId`；`CompanionService.LeaveAll` 換上新的 `CompanionLeaveBrain`（收掉夏綠蒂的旋轉姿勢 → 站 1.3 秒讓那句話看完 → 往下走，中心低於鏡頭下緣 15% 視窗就銷毀；走 6 秒還在畫面裡改原地淡出），全部走掉才接 next。退場中的同伴另列 `_leaving`，`DismissAll`（換關卡）一起收。<br>
  **地圖**：18 迎賓大廳 `進場-回程-*` 後插 `同伴暫離-*`、首領出生點後插 `同伴回來-*`（特效 10）；17 大門 `贈藥-*` 後插 `同伴道別-*`（3032／3033）。LanguageTable 新增 3032／3033（含英文）。編輯器 `TriggerType.cs`／`triggerTypes.json` 補 companion 三參數；`validate_map.py` 檢查 companion 的 `vfxId`／`leaveLineId`。驗證：錯誤 0、警告 8（同改前）。<br>
  **已知取捨**：同伴是在迎賓大廳 `onEnter` 才消失——進場卍字／場景名播放的那一小段，它可能還站在玩家旁邊一下。
* [x] **血狂之爭八間房改成三波清場＋觸發鏈新類型「等待」＋出生點「出生特效」（⏳ 未編譯未實測）**（2026-11-07，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.10、[TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3／§3.5b）：作者要六間攻略房（21 釀酒室、22 餐廳、23 花園、24 武器室、25 訓練場、26 大營）＋拒選線兩間（20 和談大廳、19 長廊）都打「好幾波不同的怪、清光一波才出下一波、最後一波清光才開門」，每波配置作者口述、位置 AI 估。<br>
  **為什麼要動程式**：① 作者要波與波之間停 1 秒——出生點被鏈叫醒當幀就生，全系統沒有通用的延時節點（只有 `onEnter`／`clearLevel` 有延遲秒數）；② 通用 `next` 只能接一顆，但一波通常 2～4 顆出生點要同時叫醒；③ 作者要接續產生的怪播出生特效，出生點沒有特效欄（只有召喚武器的 `SummonEffectID` 有）。作者選「通用等待節點」（方案 B）而不是「出生點加延遲欄」，多筆目標選「等待節點自帶同時啟動欄」而不是「每波一個旗標」（後者要登記 16 個旗標）。<br>
  **程式**：`TriggerChain` 加 `wait`（`ExecuteWait`：有縮放時間、換圖作廢、啟動 `targets` 後走自己的 setFlag/next）＋ `TriggerChainRunner.After`（協程延時）；`MapMonsterRespawner` 加 `spawnVfxId`（`Fire` 每生一隻播一次）、`MapLoader` 讀欄位（有填就走 respawner）；`SummonSystem` 抽出 `PlaySpawnVfx` 給召喚與出生點共用（依怪物可見高度縮放）；**`MapClearReward` 的鏈分析加認 `targets`**——不加的話等待節點會把鏈切斷、第 1 波就被當成最後一組掉清場血球。編輯器 `TriggerType.cs` 內建類型＋`triggerTypes.json` 補兩樣；`validate_map.py` 接續欄改成逗號拆開逐筆查、檢查 `spawnVfxId` 在不在 VfxTable。<br>
  **地圖**：八張圖的舊出生點全部換掉（含作者在 21–23 的佈置、20／19 原本的計時湧怪），每圖新增 3 波出生點＋2 顆等待節點；其他 trigger／圖層一律沒動（逐圖比對過）。`validate_map.py BloodFang`：錯誤 0、警告 8（同改前，皆為刻意關著的回頭門）。<br>
  **作者拍板的通則**：**一開始擺好的怪不播出生特效，接續產生的一律播（VfxTable 10 招喚怪物）**——伏兵／圍攻的第 1 波也算接續產生。已寫進 SCRIPTING_GUIDE §3.6。
* [x] **紅嫁衣 boss 有了真名「芙蓉」：看過家書才顯示，沒看過顯示 ???（⏳ 未編譯未實測）**（2026-11-06，見 [DRAMA.md](DRAMA.md)〈台詞與名字走語言表〉）：作者說紅嫁衣是劇本名，boss 應有自己的名字，家書（drama 30）裡已經寫了「愛女芙蓉」。盤點結果：玩家看得到、指 boss 的「紅嫁衣」只有 MonsterData 13 的 `DisplayName`（boss 開戰名牌）；對話名牌一律是「????」；劇本名（劇本道具、結算關卡名、文字圖、劇本卡）與內部名（觸發點名、DramaTable 說明、武器名、英文識別碼）都保留不動。作者拍板：boss 名牌＋新娘所有對話名牌都依「看過家書」切換，旗標關卡單次；`DisplayName` 也要走語言表。做法：上午的 `lang:` 解析抽成共用 `TextRef.Resolve`，加 `reveal:<旗標>:<id>`（不成立顯示語言表 8001「???」），`DramaTalkData.DisplayName`／`TalkPanel` 名牌／`BossIntroPanel` 都改走它。資料：語言表 1106「芙蓉/Furong」、8001「???」（新段「通用：角色名」）；`flags.json` 登記 `redBridalReadLetter`（id 23，關卡單次）；客廳1 `家書所在地` 加 `setFlag`；DramaTalkTable 10 列名牌與 MonsterData 13 改成 `reveal:redBridalReadLetter:1106`。`validate_map.py` 0 錯誤。**要 Sync Map Assets**（旗標登記也靠它同步，PROBLEMS **I4**）。
* [x] **柴房佛燈教學：撿起佛燈傳送點就亮 → 改成點亮後才開（⏳ 未編譯未實測）**（2026-11-06，見 [WOODSHED_LAMP_TUTORIAL.md](WOODSHED_LAMP_TUTORIAL.md)）：作者實測，按 F 撿起佛燈的瞬間傳送點就開了，應該是點亮佛燈之後才開。不是上一條清死連結造成的：8 月起 pickup 的 `next` 就接著開門節點 `新手教學後開啟傳送點`，撿起當下就會開門；`TickLampLight` 點亮完成時只寫旗標、沒有開門。比照儲藏室藥水教學：pickup 的 `next` 清空；`TutorialManager` 加 `LampPortalTrig`，點亮完成時 `TriggerChain.Activate` 那顆 togglePortal。傳送點的 `enableFlag=tutorialBuddleLight` 不動（只在重進地圖時生效，撿了沒點亮就中斷的人重進時門會開著、不會卡死）。<br>**通則**：`enableFlag` 是「重進地圖時記住門開過」，不是「旗標成立就開門」；即時開門的永遠是某顆 togglePortal——查「門為什麼提早開」要找誰 Activate 了 togglePortal，不是看 enableFlag。
* [x] **紅嫁衣三張圖的觸發鏈錯誤清掉：兩個 boss 房的「過關」成環、柴房死連結（⏳ 未實測）**（2026-11-06，見 [LEVEL_END_FLOW.md](LEVEL_END_FLOW.md)〈雷點〉、[WOODSHED_LAMP_TUTORIAL.md](WOODSHED_LAMP_TUTORIAL.md)）：`validate_map.py RedBridalGown` 原本報 3 個錯。① 新娘房與榕樹妖的「過關」(`clearLevel`) 也掛了 `fireOnFlag=redBridalCleared`，`next` 又接回被打敗對話，形成「對話 → 過關 → 對話」的環。這正是 LEVEL_END_FLOW 列出的雷點，之前能跑是靠「對話一次性消耗」和「結算中不重複過關」兩道保險。改成文件的典型接法：清掉「過關」的 `fireOnFlag` 與 `next`，只剩 `觀察boss死亡 → 被打敗對話 →（恢復對話）→ 過關` 這一條路。② 柴房「新手教學-邪佛對話」的 `next=柴房教學起點` 指向不存在的節點；教學其實是 `TutorialManager` 看這顆對話關閉來啟動的，屬於舊版殘留，已清掉。改後 0 錯誤。作者授權修改（這三張是作者親手做的圖）。**要 Sync Map Assets**。
* [x] **整理 DramaTalkTable 編號＋修 DramaTable 六列欄位錯位（⏳ 未實測）**（2026-11-06，見 [PROBLEMS.md](PROBLEMS.md) **L4**）：作者要求修掉重複的 ID 25，並檢查後面的編號有沒有用錯。① 第二個 25（紅嫁衣恢復「我，我真的恢復了…」）到 49 各加 1，吸收空號 50；51 之後不動，文件裡寫過的 ID 範圍都不會過期。播放順序與改前一致（同號原本靠不穩定排序決定先後）。② 盤點時發現更嚴重的問題：DramaTable 62／64／66／69／71／73（血狂之爭 1、2 階分流）的說明欄「1,2階」逗號沒加引號，整列錯一欄，`Type` 被讀成無效值退回大圖面板、`TalkGroup`=0 ⇒ 這六段劇情實際上播錯了。改成「1、2階」。改完全表無重複、嚴格遞增、無孤兒群組、所有 Type 2 指到存在的群組。群組 49／50 在檔案裡被拆成兩處（201–204 是後來補的敵方首領反應），ID 仍遞增、播放順序正確，所以沒有搬。
* [x] **紅嫁衣書房回憶（父親求道長）改成立繪對話（⏳ 未實測）**（2026-11-06，見 [DRAMA.md](DRAMA.md)〈台詞走語言表〉）：作者補了四張立繪（`Modules/RedBridalGown/Talk/`：`Priest_normal`、`Father_normal`／`_sad`／`_grateful`，都是臉朝右的原圖）。`RedBridalGown_Study` 演出六個 `bubble` 換成兩段 `dialogue`，中間保留道長走位：drama 79（群組 76：父親兩句）→ 道長走位 → drama 80（群組 77：道長三句＋父親道謝），ID 217–222，台詞 `lang:1100`–`1105`。道長立繪左（全程 `Priest_normal`）、父親右（哀求與感激＝`grateful`、悲痛＝`sad`，道長說話時延續 `sad`）；`Father_normal` 這次沒用到。Study 這張圖 0 錯誤（同模組另外三個錯誤是既有的：新娘房、榕樹妖的 `過關` 成環，柴房找不到 `柴房教學起點`，都沒動）。**要 Sync Map Assets**（新立繪＋地圖）。回憶泛黃染不到對話框與立繪（CUTSCENE_DIRECTOR §6 已知限制），作者沒要求處理。
* [x] **血狂之爭和談互嗆改成立繪對話＋頭像對話台詞可走語言表（⏳ 未編譯未實測）**（2026-11-06，見 [DRAMA.md](DRAMA.md)〈台詞走語言表〉、[bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.4）：作者要和談大廳兩位首領的對話從頭上泡泡改成立繪對話、保留 Skip。零新機制：`talk` 演出第 5–10 步六個 `bubble` 換成一個 `dialogue`（drama 78 → 群組 75、ID 211–216；夏綠蒂左、艾爾文右、聚光交替），Skip 就是演出原本右上角那顆（對話中按下會關掉面板、`bfTalkDone` 照寫）；管家開場那句維持泡泡。台詞原本在 LanguageTable 3011–3016、有英文，但 `DramaTalkTable.Text` 只吃明文 ⇒ 作者要求這次一起做掉：`DramaTalkData.DisplayText`／`Localize`，`Text` 填 `lang:<id>` 就在播放當下查語言表，舊明文不受影響；`TalkPanel`、`DramaTalkController` 改讀它。`validate_map.py` 0 錯誤。只改了編輯器那份地圖 ⇒ **要 Sync Map Assets**。<br>**順帶發現**：`validate_map.py` 檢查演出 dramaId 的那行比對的是 `type == 'drama'`，實際型別叫 `dialogue` ⇒ 演出裡的對話步驟從沒驗過 dramaId（未修，待作者決定）。
* [x] **人族管家在亮場景整個發亮發霧 → NpcTable 加「單一角色融合強度」`EnvFusion`（⏳ 未編譯未實測）**（2026-11-06，見 [ATMOSPHERE.md](ATMOSPHERE.md)〈角色環境融合〉）：量作者截圖，管家外套原圖暗部 21／中間調 37，畫面上變 60／68——角色環境融合把所有角色暗部抬到同一下限（序章大門很亮），主角有白襯衫撐對比沒事，新管家幾乎全身暗部 ⇒ 整片灰。加 `EnvFusion` 欄（留空＝1＝與改動前逐位元相同），管家填 0.4；`FillPropertyBlock` 多一個 `characterScale` 參數，`HitReactionHandler.EnvFusionScale`、`MonsterController.EnvFusionScale`、`NpcSpawner` 接線。MonsterData 這次沒加（依索引讀，風險另評）。
* [x] **Sync Map Assets 自動清掉來源已不存在的舊圖（⏳ 未在 Unity 跑過）**（2026-11-06，見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)）：作者常換角色圖，舊圖一直殘留在 `StreamingAssets/MapAssets`（同步只覆寫不刪，還會被打包；試算累積 212 個）。`MapAssetSyncTool` 最後加 `PruneStaleFiles`：`Main/`、`Modules/` 底下 GameAssets 同路徑不存在的 **PNG**＋.meta 刪掉、空資料夾一起刪；`.dipanmap` 只警告不刪（`MapLoader.mapPath` 預設還指 `RedBridalGown_01`）。安全前提已查：該資料夾 git 忽略、只有這支工具會寫、所有複製目的地都是 `Rel(srcRoot, …)`、遊戲端只讀。
* [x] **血狂之爭人族管家換新圖後演出角色圖與立繪都變小 → 調回（⏳ 未實測）**（2026-11-06，見 [PROBLEMS.md](PROBLEMS.md) **G19**）：① 演出 npc 演員照原生像素畫（一般 NPC 才有可見高度換算），舊圖人物 276px×`scale 1.81`＝1.95 格，新圖 197px 同倍率只剩 0.71 倍 ⇒ 4 張圖的 Housekeeper 演員 `scale` 1.81→**2.53**（＝1.95÷(197/256)）。② 新立繪是到膝蓋以下的長身構圖，自動對齊把整個人塞進同一高度、臉被縮小 ⇒ `PortraitTable.csv` 加 `Housekeeper_normal` 1.3 倍、往下沉 160。**要 Sync Map Assets**。
* [x] **拿掉臨時包、劇本開門即消耗＋中途離開警告、新增血球（⏳ 未編譯未實測）**（2026-11-06，見 [RUN_PROGRESS.md](RUN_PROGRESS.md) §6/§9/§10、[CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) §6）：作者改版三件事。<br>
  **① 臨時包移除**：`RunProgress.GiveItem/GiveStack` 一律進真背包，死亡也保留；`SettleIntoBag`／`EndRunDiscard`／`TempSnapshot` 刪掉，改成單一 `EndRun()`（只清進度、不動背包）。背包滿的路徑本來就有（giveItem／拾取點掉腳下、掉落物部分撿取），臨時包沒容量上限時很少走到、現在變常態。結算獎勵區作者拍板「先不顯示」→ 傳 null（顯示「無」）。拾取點／作弊面板的 `toRealBag` 變成無作用但保留。<br>
  **② 劇本消耗**：傳送門 `ScriptsPanel` 按下開門 → 方框裡的劇本直接拿掉（不退回背包）、立刻 `SaveNow()`。（同日第一版做成「踏進關卡才扣」`RunProgress.ConsumeScriptFor`，作者看完改成開門就扣，那支已刪。）新檔 `Flow/QuitGuard.cs`：劇本中設定面板「返回廣場」「離開遊戲」改跳 LanguageTable 7001／7002 的警告，視窗 X／Alt+F4 用 `Application.wantsToQuit` 攔下改跳同一個確認框（只在打包版有效）。<br>
  **③ 血球**：新檔 `Combat/BloodOrb.cs`（程序生成半透明紅球、呼吸＋漂浮、碰觸吸收、飛向玩家縮小淡出），機率 `DropChancePercent=10`、回復 `RestoreFraction=0.5`（最大血魔的 50%）都是檔頭常數。`MonsterController.TryDropBloodOrb` 跟掉寶並列、各擲各的；資格同掉寶（`DropsLoot` 且 `DropTableId>0`）。`RunProgress` 每張圖多一份 `orbs`，`InteractionManager` 負責生成／換圖重放／清除。美術圖放 `Resources/Pickups/BloodOrb.png` 就自動替換。<br>
  **開門→進關的空窗**（作者同意追加）：`ScriptsPanel` 開門時寫關卡單次旗標 `關卡:scriptPortalOpened`；`Main_Square` 邪佛對話加 `requireFlag=!關卡:scriptPortalOpened`（擋開門後再領一張）；`QuitGuard.InScript` 也算它（開了門還在廣場就離開也要警告）。`validate_map.py` 改成帶 `永久:`／`關卡:` 前綴的旗標不報未登記。<br>
  **清場血球**（作者實測紅嫁衣打半天沒掉——全關約 11 隻有資格的怪、10% 期望一趟才 1 顆）：新檔 `Combat/MapClearReward.cs`，每張圖最後一戰結束掉 1~2 顆、與 10% 並存。**全自動不用設欄位**：會掉寶的怪死 → 等 2 秒（遊戲時間）→ 沒有活著的敵對怪、`MapMonsterRespawner.HasPendingWaves()` 也沒有進行中波次才掉；資格同掉寶（本圖殺過 `DropTableId>0` 的怪），一圖一趟一次。`validate_map.py` 加「無限波不會清場」警告。<br>
  **血球加大＋拉開距離、怪物不再掉血魔瓶**（作者實測：血球太小、混在掉落物文字裡看不出來）：`WorldSize` 0.55→0.9、`TouchRadius` 0.5→0.65；落點改成離死亡處 1.1～1.6 的外圈、只挑可走格、多顆彼此錯開（`InteractionManager.PickOrbSpot`）。`DropTable.csv` 表 1 拿掉 201／202，銅錢 100%→30%。<br>
  **清場確認改成 5 幀、血球再放大兩倍**（作者：怪死後過幾秒才看到血球；波次房也只要全部打完才掉、不該等）：原本最後一隻死後固定等 2 秒確認沒有下一波，紅嫁衣沒有波次也白等。改成等 5 幀「遊戲沒暫停」的時間（≈0.08 秒；對話暫停不計，下一波一生出來就取消），`ClearSettleSeconds` 換成 `ClearConfirmFrames`。`WorldSize` 0.9→1.8、`TouchRadius`→0.95、落點外圈→1.7～2.3。<br>
  **清場判定改成讀地圖資料（推翻上一條的 2 秒／5 幀等待）**：作者：「為什麼不從地圖編輯器的編輯判斷？等現場有沒有生怪很奇怪。」改成 `MapClearReward` 讀本圖 trigger 建戰鬥鏈圖（`next/nextA/nextB/nextTimeout`＋`setFlag/deathFlag → fireOnFlag/requireFlag` 為邊），每顆出生點算出「下游別組出生點」與所屬組（同叫出者／同 `waveGroup`／進圖就生／同外部條件併組，union-find）。怪死當下：只看這趟開打過的成員——開打過且還接著下游 → 不掉；都清光 → 立刻掉。每條鏈一趟一次（作者：「第二條戰鬥鏈是第二條的事」）。先用 Python 照同規則跑過紅嫁衣與血狂之爭全部地圖確認分組（首領房單／雙分支、長廊南北伏擊、和談圍攻都正確）。respawner 新增 `TryGetSpawnState`／`RegionIdOf`、刪 `HasPendingWaves`；TriggerChain 加 `CurrentRegions`。<br>
  **血球掉落動畫**（作者：要讓玩家知道有東西掉了、做漂亮點；落地前碰到沒效果）：`BloodOrb.Create` 加 `dropFrom`——白紅光暈＋由小放大冒出 → 拋物線飛到落點（飛行中排序 20000 不被角色擋、殘影、地上影子隨高度縮放）→ 落地壓扁＋擴散光環 → 小彈一下 → `_landed=true` 才能吸收。換圖重放不播。踩到的小坑：程序球是紅色，`SpriteRenderer.color` 只能乘暗、不能做白閃，閃光改用另一張白色光暈疊上去。<br>
  **血球看起來只是一個小點、偏在光環左邊 → 球身其實幾乎透明**（PROBLEMS **E32** 重踩）：程序圖用了 `Mathf.SmoothStep` 當門檻函式，球身 alpha 只剩約 5%，看得到的只有左上角的高光。改用自寫的 GLSL 語意 `Smooth()`。之前「太小、再放大兩倍」的回饋都是這個 bug 造成的。<br>
  **bug 修好後球太大 → 縮回當時看到的光點大小**：`WorldSize` 1.8→0.4（截圖量光點約 0.33 格）、`TouchRadius`→0.6；光環／冒出閃光改用固定的 `FxRefSize` 1.8（保留作者認可的動畫尺寸）、光環圓心改成正好在球的落點；多顆間距改用 `MinSpacing` 1.8。<br>
  **再調**：`WorldSize` 0.4→0.2（作者：再縮一半）、`TouchRadius`→0.5；隨機掉落 `DropChancePercent` 10%→5%（作者：太高）。<br>
  **碰觸範圍＋回復特效**：球縮到 0.2 後 `TouchRadius` 0.5 太難碰（判定是腳底點對球心、不含身體寬度）→ 0.9；吸收時在玩家身上播喝血瓶同一個特效 `PlayDrinkPotionVfx`。<br>
  **回復量 50%→20%**（作者：回太多）：`BloodOrb.RestoreFraction` 0.5→0.2。<br>
  **通則**：「撿到即入袋」拿掉了 extraction 的制衡；第 1 周目邪佛會重發紅嫁衣，可反覆進出撿東西——作者打算用之後的「邪佛關卡倒數」補（TODO）。
* [x] **血狂之爭迎賓大廳決戰：士兵退場＋決戰前攔截對話（⏳ 未實測）**（2026-10-29，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.6）：① 大廳兩排士兵（NPC 18 隻）全部填消失旗標 `bfWarStarted`——開戰（三條線都會經過）之後回到大廳就不生；作者之後會調數量與站位，新增的要填同一個旗標。② 三條回程進場鏈在強敵現身前插對話框：血族線艾爾文、狂族線夏綠蒂「你真以為你逃得掉嗎？」，拒選線兩人輪流各兩句（DramaTable 75～77、DramaTalkTable 群組 72～74、ID 205～210）。`validate_map.py` 0 錯誤。
* [x] **夏綠蒂對話立繪放大有壓迫感：1.2 倍＋往下沉 90、外側緣可到畫面邊（推翻 E43 的 0.9，⏳ 未實測）**（2026-10-28，見 [DRAMA.md](DRAMA.md)〈固定微調〉、[PROBLEMS.md](PROBLEMS.md) **E43**）：作者嫌 0.9 太小，接受身體被對話框遮住。照 `SetAvatar` 公式合成比較：只改表的話外緣夾在對話框（±750），多出的寬度把人物推過中線、內側緣到 +274 幾乎貼到對面角色 ⇒ `PortraitTable` 新欄 `OuterToScreen`（填 1＝外側緣界線放寬到畫面邊緣，`TalkPanel` 的 `halfLimit`；`PortraitTable`／`PortraitFit`／`DramaTalkDatabase` 跟著帶值），其他圖不填＝行為不變。夏綠蒂四張：頭頂 1053、內側緣 −13~+64（與 0.9 版位置相近）、16:9 外緣剛好貼畫面左緣。CSV 表頭插入新欄、每列補空欄（欄位依位置讀，Note 移到第 6 欄）。<br>**通則**：寬度主要在下擺的立繪，「放大＋下沉」能把最寬那段藏進對話框，但排版夾限算的是整個人物寬——藏起來的部分照樣會把人推向中央，要放寬的是夾限，不是調位移。
* [x] **頭上對話框外框不見的真正原因：自訂 Graphic 缺 `CanvasRenderer`（推翻下一條的「三角形繞行方向」；作者實機確認方形白底黑框已出現，圓形／尖刺尚未看過）**（2026-10-28，見 [PROBLEMS.md](PROBLEMS.md) **E44**）：`SpeechBubbleGraphic` 繼承 `MaskableGraphic`，但基底類別不會自動補 CanvasRenderer（是 Image／Text 自己宣告的）⇒ 重建時 `canvasRenderer == null` 直接跳過，不畫也不報錯。加 `[RequireComponent(typeof(CanvasRenderer))]` 一行。繞行方向的推論錯在：UI/Default 是 Cull Off，而且框線那圈不經過三角化，真的是剔除也會剩黑線。查法：暫時診斷 log 讀 `canvasRenderer` 當場炸出 `MissingComponentException`（診斷碼已移除）。<br>**通則**：「元件在、零錯誤、什麼都沒畫」先看 Inspector 有沒有 CanvasRenderer；先推敲「連一個像素都沒有」代表哪一層壞了，再去猜幾何。
* [x] **頭上對話框外框不見：三角形改成順時針（❌ 誤診，見上一條）**（2026-10-28，見 [PROBLEMS.md](PROBLEMS.md) **E44**）：作者實機看到字但沒有白底黑框；元件在、無錯誤、尺寸對。`SpeechBubbleGraphic` 照逆時針輪廓送出的三角形全是逆時針，被當背面剔除 ⇒ 全部反轉成順時針。<br>**通則**：自訂 UI `Graphic` 輸出順時針三角形，同 Unity 自己的四邊形。
* [x] **頭上對話框三種形狀：方（直接／官方）、圓（溫和）、尖刺（激動／憤怒）（⏳ 未編譯未實測）**（2026-10-28，見 [MONSTER_SPEECH.md](MONSTER_SPEECH.md)〈三種形狀〉）：作者要「從方形的最小容納範圍往外長出圓形跟尖刺型」。橢圓與尖刺不能用九宮格圖片（拉伸後線條不均、刺被拉扁）⇒ 推翻同日稍早的 SVG/PNG 九宮格，改成程式依文字大小畫外框（新 `SpeechBubbleGraphic`：耳切三角化填白＋3px 框線＋1px 抗鋸齒，尾巴是輪廓的一部分）。圓形貼合「每一行字的四角」而不是外接方框；尖刺數量隨周長、刺長由台詞文字決定。指定：怪物句子開頭 `[刺]`／`[圓]`（可與 `N%:` 並用）、劇情 `bubble` 步驟加「形狀」按鈕（`bubbleShape`）。依語氣把現有怪物台詞標了 51 格（刺 22、圓 29）。<br>**通則**：要「同一個框依內容長大」又要形狀不是矩形時，別用九宮格——九宮格只保證四個角不變形，橢圓、鋸齒這類沿整圈都有特徵的外形一拉就壞；改成依尺寸重建輪廓。
* [x] **頭上對話框改版：白底黑線、字放大、框跟著字長（⏳ 未編譯未實測）**（2026-10-28，見 [MONSTER_SPEECH.md](MONSTER_SPEECH.md)）：作者「NPC 跟怪物在講啥我基本上都看不清楚」。根因不是字型而是**固定框＋縮字**——舊水墨泡泡能放字的區域只有 200×83，9 字以上降到 22px。改成 SVG 畫的白底黑線九宮格（圓角／膠囊／斜切三種，`tools/ui/speech_bubble/`，預設圓角）＋獨立尾巴、字級固定 34（思源宋體 Bold）、一行約 10 字、框跟著長。斷行自己做：先在標點後斷、沒標點才平均分行，兩種取行數少的（「公爵大人最討厭被拒絕。／上一個拒絕他的，／血到現在還沒流乾。」）。怪物與劇情 `bubble` 共用，呼叫端零改動。<br>**通則**：「字太小看不清」先看是不是**框決定字**——固定框＋自動縮字的設計，長句一定會被縮到看不見；要可讀就讓字級固定、框去適應字。
* [x] **角色介紹第一輪實機回饋：夏綠蒂改成跟艾爾文同比例、黑幕加深（推翻上一條「寬幅立繪縮小」，⏳ 未實測）**（2026-10-28，見 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §10.3）：作者覺得版面挺好，但夏綠蒂比艾爾文小很多、黑幕不夠黑看不清字。① 立繪改成同高優先：內側不壓文字欄、外側最多人物寬 4 成超出畫面（`PortraitMaxOffscreen`），夏綠蒂四張都回到高 1040。② 黑幕常數改填「看起來剩幾成亮度」（0.17）、程式依色彩空間換算 alpha（Linear 下 ≈0.98）——首版 α 0.85 在 Linear 下實際只壓到 42%。<br>**通則**：要「壓黑讓字看得清」時，在 Linear 專案裡直接填 alpha 一定會偏淡；常數改成填目標亮度，換算留給程式，下次調的人才不會被數字騙。
* [x] **夏綠蒂立繪重製成寬幅全身後的對位修正（⏳ 未實測）**（2026-10-28，見 [PROBLEMS.md](PROBLEMS.md) **E43**）：新圖 1536×1024、人物寬高比 1.36~1.47。照 `TalkPanel` 公式合成 1920×1080 驗證：沿用 1.3／−150 時權杖越過中線、離主角約 30px ⇒ `PortraitTable` 四張改 0.9、拿掉 −150（補上原本沒有的 fear 列），四種表情頭頂同高、權杖停在中線附近。角色介紹面板首版只考慮直幅（夏綠蒂會寬 1416、左出畫面右壓姓名版）⇒ 加「立繪可用區」：寬度超出就等比縮小、腳底貼回下方；艾爾文不受影響。<br>**通則**：素材換了寬高比，舊的微調值不能沿用——那組值是為了補舊圖的缺陷調的。
* [x] **劇情演出新步驟「角色介紹」`intro`＋血狂之爭和談介紹兩位首領（⏳ 未編譯未實測）**（2026-10-28，見 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §10）：鏡頭移到角色身上→到位後跳介紹（一側立繪、另一側姓名版＋說明）→停幾秒→下一步；兩人就排兩個，自然是「介紹完一位、鏡頭轉去另一位」。全部在地圖編輯器「劇情」分頁可編：演員、立繪左右（右側自動鏡像）、立繪（從 Talk 資料夾挑、附縮圖）、稱號／名字／說明（只填語言表 id，旁邊唯讀顯示那句中文）、秒數、鏡頭偏移；預覽只佔位。新面板 `CharacterIntroPanel` 用作者拆的四張元件，版面照示意圖換算成常數；字型新增思源宋體 Noto Serif TC（OFL，可商用，OTF→TTF）。和談 `talk` 段在管家退場後插入夏綠蒂（左）→艾爾文（右）兩個介紹，文案 LanguageTable 3026–3031；`validate_map.py` 0 錯誤。<br>**通則**：介紹期間要「停住演出」但不能暫停遊戲——鏡頭轉場靠 `Time.deltaTime`，暫停了第二位的鏡頭就移不過去；要的是「時間軸等待」，不是 `timeScale=0`。
* [x] **血狂之爭：血族／狂族玩家的劇情依階級分流（⏳ 未實測）**（2026-10-27，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.4b）：入營後四段（開戰通報、首領突圍、敵方首領敗北、大門道謝）原本所有人共用、而且上一輪寫成只對人類講的口吻。現在每段三選一：非該族／同族 1、2 階／3 階（該隱、芬里爾），新增 DramaTable 61~74（對話群組 58~71）；地圖三張共 8 個觸發點拆成條件鏈（`onBlocked=跳過這顆繼續`），`validate_map.py` 0 錯誤，編輯器與遊戲兩份同步。表態補敵方首領反應（該隱→艾爾文戒備、芬里爾→夏綠蒂驚恐）；拿掉「人類」稱呼。<br>**通則**：一條「選邊之後」的共用劇情，實際進來的人比寫台詞時想的多（選邊的人類、同族、3 階、其他血統）；寫共用台詞前先列出「誰會走到這裡」，不然很容易寫成只對其中一種人講話。
* [x] **血狂之爭對話依兩位首領個性重寫（⏳ 未實測）**（2026-10-27，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §6）：作者給艾爾文三張立繪並口述個性——夏綠蒂（男）老奸巨猾、惜命、殘酷；艾爾文年輕、正義感強、重榮耀，狂族首領是他父王。和談改成夏綠蒂開價「北境三城＋五位皇子作客」、艾爾文拒絕當人質；**選哪一族就是哪一族勢弱突圍**：選血族＝狂族攻破防線、艾爾文道歉「是父王的意思」、夏綠蒂只想讓玩家開路；選狂族＝血族下毒、艾爾文中毒仍不丟下玩家、夏綠蒂嘲諷；拒選＝「牆頭草」／「為了帝國先下手為強」。DramaTalkTable 15 組整組重寫（41→49 句，ID 110~158）、LanguageTable 12 句泡泡（含英文）、艾爾文戰鬥喊話與 Boss 頭像。待補立繪 `Elvin_weak`／`Elvin_sad`／`Duke_Charlotte_fear`。<br>**通則**：作者口述兩條分支時，先用「誰嘲諷誰、誰拉誰」的人際邏輯檢查一遍描述是否自洽——這次兩條路線的描述對調了，照字面寫會出現「夏綠蒂嘲諷自己的盟友」。
* [x] **聖子艾爾文大絕：每一波都有一道正對玩家、單雙波左右交替（⏳ 未實測）**（2026-10-27，見 [BOSS_MODULE.md](BOSS_MODULE.md) §16.2）：作者躲過第一波後站著不動就全部躲掉——6 道對稱展開，中軸是縫。整個扇形轉 ±15°（半個間隔）、單雙波交替（`MonsterWeaponUser.TryUseAt` 加 `fanRotateDeg`）。<br>**通則**：「朝玩家打」的偶數扇形，中軸永遠是安全的縫；要逼玩家移動，就得讓其中一道落在中軸上，而且每一波換邊，否則玩家只要找到一次縫就能一直站著。
* [x] **聖子艾爾文第二輪：收劍浮起來、衝擊波「躲開了還被打到」（⏳ 未實測）**（2026-10-27，見 [PROBLEMS.md](PROBLEMS.md) **G18**、**F41**）：<br>
  **浮起來**：素材沒問題（腳一直在 201~202px），是 attack 第 20~22 幀劍尖掃到腳底線以下、逐幀對齊把整隻抬高最多 16px ⇒ 新表尾欄 `AttackFootLock`（attack 只量第一幀），不用刪圖。<br>
  **難躲**：判定 `Radius 1.25` 是照衝擊波圖的高度給的，玩家碰撞圓在胸口 ⇒ 腳離行進線 1.75 以內都中（約一個身高）。俯視角裡貼地衝擊波的圖高是「往上竄」不是「地上佔多寬」⇒ 縮到 0.6（左右 1.1）。大絕 30° 的推算跟著重算：約 4 單位內站著必中、4.3 外有縫。<br>
  **通則**：判定圓同時決定「沿行進方向」與「垂直行進方向」的厚度；貼地爬的投射物，垂直方向要照它在地上佔多寬給，不是照圖高。
* [x] **聖子艾爾文第一輪實測回饋：出手往前跳＋變大、大絕固定 6 道、閃避改成真正穿透（推翻 §16 第一版的 4~17 幀與隨機組合，⏳ 未實測）**（2026-10-27，見 [BOSS_MODULE.md](BOSS_MODULE.md) §16、[PROBLEMS.md](PROBLEMS.md) **G18**）：<br>
  **往前跳＋變大**是兩件事疊在一起：持劍素材的身體比 idle 往前偏 27px（腳底對齊只管上下）、idle 背上的劍柄把可見高撐到 148（角色本身 136）⇒ attack 被放大 1.088。新表尾欄 `AttackOffsetX`（水平 pivot 補償，一路接到 `MapSpriteLoader`）＋ `AttackScale 0.92`。<br>
  **閃避穿透**：作者看到 GM 飛劍在閃避時還是撞上、噴擊中特效——那一劍是在閃避前一刻命中、走受擊攔截，子彈已經判定命中就被銷毀；加上第 1~3 幀本來就不是虛體。改成第 1 幀起虛體、`BeginDodge` 當下切層，並給 `BulletInstance` 加 `PassThroughCurrentHit()`：玩家命中處理發現目標已閃開 ⇒ 子彈這一下不算、繼續飛、不噴特效。<br>
  **大絕**：6 道 30°（算法見 §16.2：6.5 單位內站著不動必中、約 7 單位外有縫）。<br>
  **通則**：「無敵」和「穿透」對玩家是兩件事——無敵是打到了不痛（子彈消失、有火花），穿透是根本沒打到（子彈飛過去）。要做閃避就得讓命中判定本身說「不算」，只在傷害端擋掉是不夠的。
* [x] **GM 測試飛劍改成扣「最大」血量 10%（推翻上一條的「當前血量」，⏳ 未實測）**（2026-10-27）：作者要每下固定跨一個 10% 門檻、10 下打死；當前血量版越打越少（300 血要 7 下才到 50%）。`GmWeapons.MaxHpFraction`，改 `MaxHealth`。
* [x] **GM 測試武器區間 900～999＋第一把「GM測試飛劍」：每下扣目標當前血量 10%（⏳ 未編譯未實測）**（2026-10-27，見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md)、`Scripts/Weapon/GmWeapons.cs`）：作者測頭目各血量門檻用。規則集中在 `GmWeapons`（區間判斷＋`DamageAgainst`）；玩家的 8 個傷害結算點（子彈／雷射兩處／近戰兩處／連鎖／落雷／拋物線爆炸）改成問它，非 GM 武器原樣回傳 `Damage`；抽選組池時排除區間。武器／配方／物品都是 900（借武器 1 的飛劍圖、配方單發、`HomingTurnSpeed 360`）。打非怪物目標照 `Damage 1`；之後仍經過 `CombatSystem` 加成／減傷。<br>**通則**：「特殊用途的資料」用 ID 區間圈起來，讓所有會「隨機挑」的地方用同一個判斷排除，比在每張隨機表上各自小心可靠。
* [x] **作弊「無敵」改成「不死」；艾爾文縮成跟主角一樣大（⏳ 未實測）**（2026-10-27）：作者：無敵後不閃白、不跳數字，分不出是怪沒打到還是無敵擋掉 ⇒ 推翻上一條的「整下忽略」，改用 `CombatStats` 既有的具名不死保護（照常受傷、最多扣到 1）；拿掉 `PlayerController.CheatInvincible`（Domain Reload 關閉時靜態 bool 跨 Play 殘留、與進 Play 就清空的不死保護會對不上，按鈕狀態改直接讀 `HasDeathGuard`）。按鈕起初一片空白：`UIBuilder.Button` 傳空標籤不建 Text。艾爾文 MonsterData 38／40／43／45 `Scale` 2.1→1、和談演員 7.08→3.37。<br>**通則**：測試用的「無敵」要保留受擊回饋，只拿掉死亡——否則無敵本身會遮住你要測的東西。
* [x] **作弊面板加「玩家」分頁：無敵開關（⏳ 未編譯未實測）**（2026-10-27，見 [TODO.md](TODO.md)〈文件缺口：作弊面板〉）：作者測頭目時一直死。`PlayerController.CheatInvincible`（靜態、不存檔）擋在玩家受傷唯一入口 `TakeDamage` 最前面——扣血只有這條路（`CombatStats.ApplyHealthDelta` 的負值只從這裡來），所以不用到處補。
* [x] **狂族頭目「17 聖子・艾爾文」：近戰＋每 5 秒跳劈三向衝擊波＋受擊前閃避；50% 起每 10% 五波隨機扇形血色衝擊波；取代嘯月（⏳ 未編譯未實測）**（2026-10-27，見 [BOSS_MODULE.md](BOSS_MODULE.md) §16、[PROBLEMS.md](PROBLEMS.md) **G17**）：
  新 `HolyPrinceElvinBrain`（近戰委派 `MeleeChaseBrain`、大絕門檻照抄夏綠蒂、無敵走 Mist 層）；衝擊波＝素材庫 `pj2_ground_shockwave` 黃／紅 ×4（作者否決了第一個候選 `pj1_slash` 月牙劍氣：聖子是跳起來往下劈，要「貼地往前衝」的）；MonsterData 38／40／43 與和談演員、對話講者換掉嘯月，45＝競技場測試列。<br>
  **slash／dodge 會離地、會散開**：逐幀腳底對齊會把跳劈拉回地面、把旋風推得亂跳——G17 寫過但當時隨 mist 圖移除的「只量第一幀」加回來（`alignByFirstFrame`，預設 false）。影子也不自己量，借 attack／idle 的錨點。<br>
  **閃避分兩條路**：子彈有飛行過程 ⇒ 預判（新增 `BulletInstance.Active` 登記表，子彈沒有 Collider 查不到）；雷射／近戰／AOE 沒有 ⇒ 第一下打到時攔下改閃（`MonsterController.DamageInterceptor`，DOT 不算）。預判時間窗的**下限是「無敵開始前」的時間**（第 1~3 幀還打得到），比那更早到的子彈閃了也白閃。<br>
  **判定圓對不上圖**：子彈判定是以 pivot 為圓心的圓、不吃 BulletScale；這張衝擊波主體在前、後面是尖刺，pivot 留中心會讓圓落在尖刺上 ⇒ 改 `.meta` 的 pivot 到 (0.68, 0.5)。<br>
  **通則**：① 「會躲攻擊」的怪要按攻擊有沒有飛行過程分開處理——有的預判、沒有的只能在命中瞬間攔截，兩者看起來一樣是「閃開」；預判窗要從無敵真正開始的那一刻算。② 形狀不對稱的子彈圖，用 pivot 把判定圓搬到主體上，比加大半徑誠實（加大半徑會讓尾巴也能打人）。
* [x] **同伴節奏再改：發呆 4：作戰 6、不跟隨玩家（推翻上一條的 3:3:4，⏳ 未實測）**（2026-10-26，見 [BOSS_MODULE.md](BOSS_MODULE.md) §15.2）：作者：「協同 NPC 跟玩家都在同一張地圖，專注作戰就好」。`WeightFollow 0`、牽繩關閉、`AggroRange` 8→20；跟隨段程式保留。換圖仍由 `RepositionPlayerAllies` 帶過去。
* [x] **同伴節奏改排班制：發呆 3：跟隨 3：作戰 4（⏳ 未實測）**（2026-10-26，見 [BOSS_MODULE.md](BOSS_MODULE.md) §15.2）：作者回報「太常跟隨、像寵物；攻擊與發呆都很少」。原因是第一版的發呆會被玩家移動打斷、只有發呆結束才會考慮作戰。改為每段做完按權重抽下一段（模擬 2.8：2.8：4.4），離玩家 > 8 才強制追回。<br>**通則**：要「比例」就直接用權重排班；用一串條件（誰打斷誰）去湊比例，比例會被最常成立的那個條件吃掉。
* [x] **同伴朝向：跟隨／發呆看玩家、交戰才看敵人（⏳ 未實測）**（2026-10-26，見 [BOSS_MODULE.md](BOSS_MODULE.md) §15.2）：作者回報夏綠蒂跟著玩家走時臉一直朝著怪物。原因是 `MonsterController.Update` 的面向對象＝「敵人優先、沒有才玩家」；`CompanionBrain` 改為每幀依狀態寫 `BrainFacing`。<br>**通則**：Brain 改了「要去哪裡」時，也要問「該看哪裡」——移動與朝向在這個專案是兩條獨立的管線，預設朝向是為一直在打的怪設計的。
* [x] **劇本同伴改用 `CompanionBrain`：很弱但有傷害、打一陣停一陣；夏綠蒂同伴原地旋轉打周圍（⏳ 未編譯未實測）**（2026-10-26，見 [BOSS_MODULE.md](BOSS_MODULE.md) §15）：
  作者回報血族線的夏綠蒂同伴「只會轉圈圈、沒有實際傷害」。原因：同伴沿用玩家召喚物的 `AllyBrain`（靠接觸傷害），同伴列的 `ContactDamage` 是 0；轉圈是 `HandleVisuals` 靠近就自動循環的純動畫。<br>
  做法：節奏（跟隨／發呆 1.5~4 秒／交戰 3~5 秒，離玩家 > 8 收手）與傷害（每 2 秒 1、走 `CombatSystem`）共用；「怎麼打」抽成 `ICompanionAttack`，MonsterData 新表尾欄 `CompanionStyle` 選（空＝近戰、`Spin`＝範圍旋轉）。`CompanionService.Summon` 生成後換腦，玩家召喚物照舊。<br>
  **通則**：「一群角色都要同一種節奏、但每個出手方式不同」——把**節奏**與**出手方式**拆開，傷害規則只留一份在節奏那邊，出手方式只能呼叫它。之後新同伴只要寫出手方式。
* [x] **PortraitTable 接線後第一輪實機調整：榕樹妖、邪佛（⏳ 待作者確認）**（2026-10-26，見 [DRAMA.md](DRAMA.md)〈固定微調〉）：榕樹妖兩張 1.1 倍、OffsetY −50→−10（下巴露出對話框雕飾）；邪佛 1.1→1.3（頭頂≈1071 貼齊畫面上緣），OffsetX −130→0。<br>**通則**：`TalkPanel` 會把立繪**外側緣夾在對話框外緣以內**，所以放大時多出來的寬度一律往畫面中央長；往外的 OffsetX 被夾限吃掉、等於沒填。要往中央推得超過夾限位置才看得到效果。
* [x] **立繪微調只剩 PortraitTable 一個維護點：接上場景、刪除 DramaTalkTable 6 個逐句欄（⏳ 未實測）**（2026-10-26，見 [DRAMA.md](DRAMA.md)）：
  作者要求「以後只要維護 PortraitTable」。盤點：逐句欄只有夏綠蒂 20 句在用、全在左側、值一致（1.3／−150）⇒ 搬成 PortraitTable 三列，沒有衝突。刪欄＝CSV 109 列去掉表尾 6 欄（逐列確認前 7 欄不變）＋`DramaTalkData` 欄位、`DramaTalkDatabase` 解析、`TalkPanel.SetAvatar` 的逐句參數一起拿掉；MainScene 的 `DramaTalkTableProvider.portraitCSV` 接上。<br>
  **副作用**：PortraitTable 原有的 6 列（邪佛、樹臉、紅嫁衣、Girl）從此**第一次**生效，畫面會變，見 TODO。<br>
  **通則**：同一件事有「依圖」與「依句」兩層設定時，資料自然會往比較好填的那一層流（這次全填在句子上），而另一層就沒人發現它壞了。只留一層，壞了才看得見。
* [x] **夏綠蒂對話立繪：上一條寫進 PortraitTable 的放大其實沒生效，改用 DramaTalkTable 逐句欄位（先在競技場 40／41 試，⏳ 待作者確認）**（2026-10-26）：作者回報「還是很小」——查到 `PortraitTable.csv` 從沒拖進 MainScene 的 `DramaTalkTableProvider.portraitCSV`（DRAMA.md 寫「沒拖＝全部走自動對齊、不報錯」，所以一直沒人發現；邪佛那列也一樣無效）。撤掉夏綠蒂那三列以免日後接線時重複放大。<br>**通則**：改「選填、沒接也不報錯」的設定表之前，先確認它真的有被讀到——不然改了等於沒改，而且看不出來。
* [x] **修：地圖編輯器看不到跨模組借來的 NPC／演員（⏳ 編輯器未重開驗證）**（2026-10-26，見 [PROBLEMS.md](PROBLEMS.md) **C16**）：Future_Arena 擺 BloodFang 的夏綠蒂，編輯器預覽只找「本 module＋Main」所以找不到；遊戲端走 catalog 不分模組所以正常。`PreviewSpriteLoader.FrameFiles` 補找其他 module（最後才找，既有預覽零變化）。
* [x] **血狂之爭和談大廳：兩位首領拉開到大廳左右對望；夏綠蒂對話立繪放大；三句改 anger（⏳ 未實測）**（2026-10-26，見 [BOSS_MODULE.md](BOSS_MODULE.md) §14.8）：站位由作者截圖的紅框換算（兩首領間距 3 單位＝180px ⇒ 60px／單位、鏡頭中心 (15.5, −15.0)）。立繪變小的原因：TalkPanel 把每張立繪的不透明內容高正規化成 580，**全身像與半身像在同一個高度下，上半身大小會差很多**——用 PortraitTable 補，不改自動對齊。<br>**通則**：從截圖換算世界座標時，先找畫面裡兩個已知座標的東西量出「每單位幾像素」，比猜鏡頭大小可靠。
* [x] **夏綠蒂公爵的血球換成 `bloodMagicOrb`（pj3_magic_orb 紅，⏳ 未實測）**（2026-10-26，見 [BOSS_MODULE.md](BOSS_MODULE.md) §14.2）：作者從四個候選選 B。32 幀最近鄰 ×4 匯入 `Resources/Weapon/animation/bloodMagicOrb/`（含 .meta），武器 73／74 換外觀；圓球的 `SpriteAngleOffset` 留空。
* [x] **夏綠蒂公爵跳躍落點改成「離地那一刻鎖定」（推翻同日上一條的「追到下墜才鎖」，⏳ 未實測）**（2026-10-26，見 [BOSS_MODULE.md](BOSS_MODULE.md) §14.3）：
  作者：「追到下墜才鎖太難躲；應該是準備起跳時記錄玩家位置往那裡跳，而不是時刻鎖定」。現在蓄力期間持續看目標、jumpStart 第 14 幀離地時記下位置，上升／下墜不再改 ⇒ 起跳後約 1.3 秒可閃。<br>
  **通則**：「追蹤型」攻擊的公平性看**鎖定之後還剩多少時間**，而不是鎖不鎖——v1（剩 1.8 秒以上、但人站著不動才會中）與 v2（剩 0.4 秒）都不對，鎖在動作明顯的那一幀（離地）剛好。
* [x] **夏綠蒂公爵第二輪（作者在競技場看過後）＋取代血狂之爭的「緋夜」（⏳ 未實測）**（2026-10-26，見 [BOSS_MODULE.md](BOSS_MODULE.md) §14、§14.8）：
  競技場 Future_Arena 的測試 NPC 換成 NPC 種類 9（夏綠蒂公爵）。作者回報三點＋追加一點：<br>
  **旋轉時變小**：自動對齊拉的是可見高，旋轉揚起手和帽子（373 vs idle 339px）⇒ 被縮 0.91 倍；`AttackScale 1.1` 抵銷。<br>
  **跳躍沒追到人**：第一版照皇家衛士在起跳當下鎖落點，但公爵蓄力＋上升將近 1.8 秒 ⇒ 改成蓄力與上升期間每幀追、開始下墜才鎖。<br>
  **太容易被擊退**：查程式才發現 `KnockbackThreshold` 是**累積傷害**門檻（文件寫成單次，已改正 ACTORS_AND_COMBAT），距離是整張畫布寬的 % ⇒ 大畫布＋大體型一次退 3 單位。改 40／8。<br>
  **大絕血球反彈 2 次**：另開武器 74／配方 89，Brain 自己再掛一個 `MonsterWeaponUser`（CSV 一隻怪只能填一把）。<br>
  **取代緋夜**：MonsterData 39／41／42、MainLobby 四段演出的演員、DramaTalkTable 18 句講者與頭像、DropTable 名稱；`validate_map.py BloodFang` 0 錯誤。bloodfang GAP §9.2／TASKS T8b 的 VampireLord 設計標為暫停。<br>
  **通則**：① 「這隻怪的跳躍要不要追蹤」取決於**預告有多長**——預告短（皇家衛士 0.47 秒）鎖起跳點才公平，預告長（公爵 1.8 秒）不追就打不到人。② CSV 欄位的語意以程式為準，數值調不出手感時先讀它實際怎麼用。
* [x] **血族頭目「夏綠蒂公爵」：慢走＋原地旋轉 8 方向血球＋跳出畫面再砸下來的跳躍＋50% 起每 10% 一次撞牆反彈大絕（⏳ 未編譯未實測）**（2026-10-26，見 [BOSS_MODULE.md](BOSS_MODULE.md) §14、[PROBLEMS.md](PROBLEMS.md) **F39**）：
  新 `DukeCharlotteBrain`（`BrainType=DukeCharlotte`）＋MonsterData 44、WeaponTable 73、RecipeTable 88。8 方向是配方（`SpreadCount 8／SpreadAngle 315`），不是新程式。`MonsterAnimator` 加 `JumpStart`／`JumpLanding` 兩個狀態與 `FrameSprite`；`LeapSlamBrain` 的落點解析開放共用；`MonsterWeaponUser` 讓環狀爆發從身體中心出手。<br>
  **量幀先於設計**：jumpStart 的 15~25 素材幾乎沒往上移（離地全靠程式）；jumpLanding 1~4 頂邊＝0、頭被畫布切掉。作者決定照播、讓切邊永遠在鏡頭外 ⇒ 每一幀用「那一幀 Sprite 的畫布上緣＋即時鏡頭位置」反推最低高度。不能用量表，因為腳底對齊是逐幀補 pivot 的。<br>
  **審查攔下的坑**：怪物出手點在「朝目標的框外緣」，環狀爆發的圓心會被推到怪前方 2.8 單位（F39）；`SpreadAngle 360` 首尾重疊、要填 315。<br>
  **大絕門檻**：「一次跨多格只放一次」與「大絕中跨過的作廢」（作者拍板）用同一支 `SkipCrossedThresholds`，開招、收招各跑一次。<br>
  **通則**：素材的某幾幀有缺陷（被畫布切到）時，除了重出素材，還可以用**鏡頭**藏——前提是用即時的鏡頭與真正的 Sprite 幾何算，不要寫死高度。
* [x] **劇本編排手冊＋工具：把血狂之爭的經驗萃取成通用做法**（2026-10-15，見 [SCRIPTING_GUIDE.md](SCRIPTING_GUIDE.md)）：作者看完血狂之爭後認為劇本編排可以整段交給 AI，要求把經驗寫成文件以便量產劇本。新增 `readme/SCRIPTING_GUIDE.md`（分工、流程、模式庫、資料落點、坑清單、協作）與 `tools/scripting/`：`dipan.py`（讀寫地圖保持格式、加觸發點、格子可走檢查、鏡像、演員大小自動算、資料表查詢）、`validate_map.py`（整個模組一次驗，含跨圖檢查）、`map_grid.py`（文字格子圖）。原本只放在 AI 工作機上的臨時腳本，下一個 AI 拿不到，所以搬進專案並通用化。用血狂之爭十張圖驗過：0 錯誤、8 個警告全是刻意的回頭門；另外故意塞錯（接續不存在、旗標未登記、入口拼錯、孤兒節點、走位到牆裡）確認全部抓得到。<br>**通則**：驗證器的每一條規則都對應一個真的踩過的坑（入口拼字、清場群組 nextWhen、初始停用＋條件旗標、clearLevel 塗格子、門初始停用卻沒人開）；之後再踩新坑，除了記 PROBLEMS，**能機械檢查的就加進 validate_map.py**。
* [x] **血狂之爭和談大廳改演法：撤掉 0 傷害表演戰，改「敵族大軍真打、殺光守軍後追玩家」（⏳ 未編譯未實測）**（2026-10-15）：作者實測 0 傷害後兩邊隔很遠在揮、看起來很怪 ⇒ 改成選邊後敵族 12 隻湧入（血族線狂族 16|17 西側、狂族線血族 15|35 東側）對我方守軍 3 隻，互打全額傷害，守軍很快陣亡、敵軍轉追玩家，逼玩家往唯一開著的門跑。程式：`FactionRelations.ShowFightActive`→`FullTribeDamage`（不再擋「打玩家」），`factionWar` 加 `fullDamage` 勾選，刪 `factionShow` 型別（主遊戲＋編輯器兩處）。地圖 20：`表演開戰`→`實戰開戰`、四個出生點去掉「表演」字樣。
* [x] **修：近戰招式不吃陣營傷害乘數（表演戰士兵被打光）／演員消失後頭上對話框還在（⏳ 未編譯未實測）**（2026-10-15，PROBLEMS **F38**）：`CombatSystem` 來源沒有 MonsterController 時改讀 `EnemyContactDamage.Faction`（揮擊／踐踏／咬擊／自爆都是另生 ImpactDamageArea 去打）；`MonsterSpeechPanel.Speak(Transform)` 的「還在不在」改成「沒被銷毀且 activeInHierarchy」，despawn 或略過快轉成消失時氣泡立刻收掉。
* [x] **修：劇情 Skip 後演員留在場上（全專案）；和談大廳演出簡化（⏳ 未編譯未實測）**（2026-10-15，PROBLEMS **J6**）：`CutsceneDirector.FastForwardStates` 在略過時把 move／face／spawn／despawn／setFlag 快轉到最終狀態（影響所有有 Skip 的演出，例如紅嫁衣書房：略過後父親與道長會落在演出結尾的位置）。和談演出：管家只說一句「尊貴的客人帶到。」就消失、不走位；兩首領一開場就站在兩張半圓桌之間（隔著中央徽紋相對），管家消失後直接開吵；拿掉「諸位息怒」那句。
* [x] **血狂之爭攻略圖改成「清光所有怪才開往下一張的門」（⏳ 未實測）**（2026-10-15）：21–26 往前的傳送點初始停用；每張圖所有出生點填同一波次群組 `清場`、最後一顆接 `開前門`（一次性出生點填了群組就會被 respawner 追蹤存活，全滅推鏈）。⚠ 21 血蝠出生點的 `nextWhen=生出來時` 必須清掉——「生出來時」那顆會在生成當下就推鏈、全滅時反而不推。
* [x] **血狂之爭：劇情圖所有傳送點改「初始停用、劇情開門」；人類選陣營後加兩首領反應對話（⏳ 未實測）**（2026-10-15）：作者提議「每扇傳送門都勾初始隱藏，劇情走到才開」——採用，因為它比「進場後 togglePortal 封門」好：載入期間就是關的、不會閃，也省掉一整批封門節點。17／18／19／20 所有門＋攻略圖回頭門初始停用，不掛 enableFlag（掛了回程會在不該開的時候開著，例如迎賓大廳北門在打首領時）；刪 `封全門-*`／`封南門-和談`；移除本日稍早加的三個門旗標。選邊面板 nextA／nextB 先接 drama 59／60（己方首領得意、對方首領放話），再接加入陣營。<br>**通則**：劇本地圖的傳送點預設全關、由鏈開門；`enableFlag` 只用在「之後每次回來都該開著」的門，劇本裡這種門比想像中少。
* [x] **血狂之爭第二輪調整：首領快步離場、同伴改在攻略圖加入、拉攏對話、表演戰零傷害、不能用的傳送點不顯示（⏳ 未實測）**（2026-10-15，SCRIPT §3 開頭註記）：作者實測血族線——首領說「我跟著你」卻衝去打仗（AllyBrain 看到敵人就衝）⇒ 改成首領在演出裡快步跑到東門消失、到 24 才以同伴出現；選擇面板前加兩族首領拉攏（drama 58）；表演戰傷害乘數 0。<br>**傳送點「看得到卻踩不動」**：原因是引路演出期間 `TeleportWatcher` 一律不觸發（CUTSCENE_DIRECTOR §4），而門一直顯示。改成去程三扇北門「初始停用＋解鎖旗標」、鏈尾開門；同理把整條劇本裡「永遠只往一個方向走」的門（迎賓／和談東西門、攻略圖回頭門）一律初始停用，取代 onEnter 才封（進圖時會先閃一下）。<br>**通則**：「這扇門現在不能用」優先用 `startDisabled`（＋需要跨換圖記住時加 `enableFlag`）表達，而不是進圖後用 `togglePortal` 關——後者在載入、場景名稱期間門是看得到的。
* [x] **血狂之爭依作者重訂流程重做 S2～S4：表演戰、劇本同伴、兩側攻略、隱藏線圍攻／伏擊／雙首領掉藥（⏳ 程式未編譯、全部未實測）**（2026-10-15，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.4–§3.8、GAP §10）：
  作者實測 T4 後重訂：選了一族＝該族駐地被另一族偷襲（**表演戰**：兩族互咬、不打玩家、玩家打不到），首領以**同伴**身分跟著玩家殺進敵族駐地（血族線走東側 24–26、狂族線走西側 21–23 作者的佈置），迎賓大廳打敵族首領，回大門首領給 1 階血統藥劑（302／303）後過關；拒選＝和談廳兩族圍攻（清光才開南門）→ 長廊中段伏擊（上下封門）→ 雙首領（40／41 掉落表必掉兩族藥劑）→ 在迎賓大廳結算。<br>
  **程式**：`FactionRelations.ShowFightActive`＋`factionShow` 鏈動作；新檔 `CompanionService`＋`MonsterController.IsCompanion`（不受傷、FindNearestEnemy／EnemyContactDamage 跳過）＋`companion` 鏈動作；編輯器兩處型別。**同伴＝重用 PlayerAlly**：跟隨、跨圖保留、新落點重定位都是既有行為，只補「不受傷、不被當目標、換關卡解散」。<br>
  **資料**：MonsterData 42／43 同伴列（HP 999、接觸傷害 0、速度 4.8）、40／41 掉落表 2／3；flags 19–22；DramaTable 44–46 改寫、54–57 新增。地圖：20 拆成三條線（49 顆 trigger、4 段演出）、21–23 移除 T4 加的追兵／援軍（作者佈置逐顆比對確認未變）、24–26 佈置狂族怪、19 伏擊、18 三種首領戰、17 結局。驗證腳本 10 張圖 0 錯誤。
* [x] **血狂之爭 T4（Slice 2 血族線）：和談分流→西側逃亡三房→迎賓大廳狂族首領→大門過關（⏳ 未實測）**（2026-10-15，SCRIPT §3.4 D9a/D10、§3.5、§3.6、A5）：20 `分流-血族`（watchFlag）→ `首領退場`（leaders_exit 演出）→ 開西門 → 封南門 → 對話 44，開戰衛兵兩片（15×4／16×4，條件旗標 bfWarStarted）；21／22／23 各 `進場-逃亡`（每次）封來路、狂族追兵（間隔 8、上限 3、2 波）、21／23 血族援軍；18 `進場-回程-血族` → 封四門 → bossIntro 37 → 首領出生點（初始停用、全滅推鏈）→ 對話 47 → 開南門＋寫 bfBossDead；17 用 camZone 踩點接 clearLevel。驗證腳本（加查 monsterId 存在、初始停用＋條件旗標互斥）6 張圖 0 錯誤。<br>**發現**：`clearLevel` 塗格子不會踩點過關（文件舊描述），改用 camZone 踩點——記 PROBLEMS **B18**、LEVEL_END_FLOW 改正。
* [x] **血狂之爭 T3（Slice 1）：去程四張圖的引路／和談／表態編排（⏳ 未實測）**（2026-10-15，見 [bloodfang/SCRIPT.md](bloodfang/SCRIPT.md) §3.0–§3.4）：AI 直接寫 `.dipanmap` JSON——17 大門三隻 NPC 補 `!flag:bfTalkDone`／`disappearFlag=bfGuideStarted`／`next`＋三段引路演出；18／19 各一顆 `onEnter`＋三段引路（三顆 `playCutscene` 串成鏈、各掛血統條件＋`跳過這顆繼續`）；20 和談 `onEnter→factionPeace→封門→talk 演出`、五顆表態 `watchFlag`（該隱／血族／芬里爾／狂族／其他）、`timedChoice` 選邊、加入陣營／拒選／開戰、6 隻和平段士兵 NPC、`leaders_exit` 演出。驗證腳本檢查：名稱唯一、接續目標存在、無環、旗標都已登記、cutsceneId／dramaId／langId／spriteFolder 都存在、move 目標在可走格、togglePortal 的 target 確實是傳送點——四張圖 0 錯誤。<br>
  **演員大小怎麼算**：演員不走怪物的「自動高度對齊」，`scale` 是純 localScale（PPU 256）。要跟遊戲裡同一隻 NPC 一樣大 ⇒ `scale = 1.95 ÷ (idle 可見像素高 ÷ 256) × NPC/怪物 Scale`。用紅嫁衣書房作者手調的父親（3.0）驗算公式得 2.89，吻合。<br>
  **查到的限制**：演出 `camera` 步驟有給座標時 `zoom` 不生效（`MapCameraController` 有焦點就強制 zoom 1），所以和談演出只平移不推近。<br>
  **通則**：同一張圖去程要每次進圖都重封側門（`togglePortal` 是執行期狀態、換圖重算），所以去程 `onEnter` 用 `repeat=每次`。
* [x] **血狂之爭 T0～T2b：開工查證、資料補齊、`timedChoice` 二選一倒數面板、編輯器多段演出（⏳ 程式未編譯未實測）**（2026-10-15，見 [bloodfang/TASKS.md](bloodfang/TASKS.md)）：
  **T0 查證**（唯讀、10 項全對到程式行號，結論表在 TASKS〈T0 結論〉）：`flag:` 條件比的是名字；遊戲端 `playCutscene` 早就支援依 `cutsceneId` 選段，只有編輯器卡在第 0 段；`togglePortal` 找 target 不限 typeId；22 餐廳南門 `targetEntrance` 打錯字（`Garde`）會讓左路落點錯。作者看完回覆兩條新需求：**依血統換引路人**、**血族／狂族玩家沒有選擇面板也沒有隱藏線**（輪迴換血統再來），頭目交給 AI 設計——規劃四份文件全部改過（SCRIPT §3.0 兩種分流寫法、GAP §8 多段演出、§9 兩隻頭目）。<br>
  **T1 資料**：MonsterData 16–19 填 Werewolf、35/36 填 Vampire、15–19/35/36 無敵幀 0→400、新增 38–41（嘯月／緋夜＋雙首領版，先用替身 Brain；原編 37–40，2026-10-16 合併 develop 時 37 已被菁英血衛士佔用，整批 +1）；flags 12–18 七個 `bf*`（關卡單次）；MapsTable 起點 20→17；餐廳錯字；LanguageTable 3001–3025；DramaTable 44–53／DramaTalkTable 66–93（對話初稿）。每張表都用「舊檔 vs 新檔逐格比對」確認只動到指定格子。<br>
  **T2 `timedChoice`**：`TriggerChain.ExecuteTimedChoice`＋`CompleteBranch`（OnCompleted 的「接指定那條」版）、新面板 `ChoicePanel`（Window／PausesGame／不可 ESC／unscaled 倒數／開啟先擋 0.4 秒點擊／有別人鎖輸入就延後一幀）；編輯器 `TriggerType.cs`＋`triggerTypes.json` 兩處同步，新增 `TriggerParam.isTriggerRef` 讓三個接續欄有「選」按鈕；`ModuleDisplayNames` 加 BloodFang。<br>
  **T2b 多段演出**：`CutsceneController` 加 static `SelectedIndex`／`CurrentOf()`，所有 `MainCutscene` 呼叫點改 `Current`；面板加段落列（◀▶／下拉／新增／複製／**只刪此段**兩段式確認）、id 重複警告；換圖回第 0 段、Undo 留在原段；旗標欄的暫存 key 帶段號（否則切段時輸入框會殘留上一段的值）。<br>
  **規劃與現實不符的一處**：GAP 寫「`nextA/B` 抄 `next` 欄的選按鈕」，實際上 trigger 的 `next` 欄**沒有**選按鈕（只有 NPC 的接續有）——改成新增 `isTriggerRef` 旗標，只給新三欄用，既有 `next` 欄不動。<br>
  **通則**：AI 改 CSV 不要整檔用 csv.writer 重寫（會改掉引號與欄位空白），只解析／重組被改的那幾列，事後用「舊檔 vs 新檔逐格比對」列出每個變動格。
* [x] **血狂之爭劇本：拆成 AI 可接手的規劃文件（只寫文件、未動程式與地圖）**（2026-10-15，見 [bloodfang/README.md](bloodfang/README.md)）：
  作者反映「遊戲基底越完整、進度越慢」，根因是劇本（關卡事件編排）一直沒拆成能交給 AI 的形狀——缺功能（程式）、編排邏輯（資料）、空間擺放（要看畫面）三種工作綁在一起。這次盤點後拆成四份：README（接手指引與分工）、SCRIPT（逐房間事件表：大門→迎賓→長廊→和談→選邊→左路/右路逃亡→迎賓首領戰→大門過關；隱藏線＝拒選→長廊回撤→雙首領）、GAP（能力對照：作者列的五件事只有「二選一倒數面板」真要寫程式，規格為鏈動作 `timedChoice`；NPC 引路第一輪用 Cutscene 零程式）、TASKS（T0 查證→T1 資料補齊→T2 程式→T3～T6 四個 Slice→T7 記錄）。<br>**關鍵事實**：`.dipanmap` 是純 JSON（trigger 在 `layers[type=Trigger].regions`、NPC 在 `npcs`、演出在 `cutscenes`），AI 讀得到也寫得了；血狂之爭 11 張圖的傳送點連接已完整（左路 21→22→23、右路 24→25→26 各回迎賓大廳側門），但劇本邏輯幾乎是空的（只有大門三隻 NPC、左路幾顆出生點）。<br>**規劃時抓到的資料缺口**：`MonsterData` 16 狂族士兵與 17/18/19/35/36 的 `Faction` 欄全空（只有 15 填了 Vampire）→ `joinFaction` 對它們無效；沒有首領怪；六個劇本旗標未登記；`IsLevelStart` 在和談大廳(20)不在大門(17)。<br>**分流寫法的坑（規劃時自己踩到、文件裡已改對）**：用「條件不成立→跳過這顆繼續」串三顆做三路分流不成立——一顆 trigger 只有一個 `next`，通過與跳過走同一條路。正解＝入口分流：`watchFlag(fireOnFlag=旗標)` 各起各的鏈、或多顆 `onEnter` 各掛 `requireFlag`，鏈中間不放條件。<br>**通則**：劇本工作交給 AI 的切法＝「AI 寫 JSON 與程式、作者擺位置與實測、一次一個可單獨驗證的 Slice」；未實機驗證的系統（陣營、偵測條件、Cutscene 多演員）要放在第一個 Slice 一次驗掉，不要分散在後面。
* [x] **菁英血衛士大小統一：attack 不再比 idle 小一圈**（2026-10-13）：作者：「idle 明顯比其他動作大（像 1.2 對 0.8），統一成 1」。實量第一幀可見高：idle 321、walk 316、**attack 411**（雙刀高舉過頭）⇒ 自動高度對齊把 attack 縮成 **0.78**，walk 其實只差 1.02。CSV `AttackScale` **1.28**（抵銷回原圖大小，三組畫布與人物本來就一樣大）、`Scale` 1.5→**1.25**（＝idle 從作者說的 1.2 調到 1）；`WalkScale` 留空。
  **通則**：攻擊動作第一幀是「舉武器起手」的怪，自動對齊幾乎一定會把 attack 縮小——**量幀時順便比一下各動作第一幀的可見高**。
* [x] **血衛士變身改成「idle＋紅霧蓋住／吹散」，移除 mist 序列圖與相關共用程式（⏳ 未編譯未實測）**（2026-10-13，見 [BOSS_MODULE.md](BOSS_MODULE.md) §13.1）：作者：「紅霧比 mist 序列圖好太多，mist 改用 idle 取代——先 idle、紅霧蓋住、怪物消失、紅霧移動，顯現時顛倒過來」。
  作者另加兩條規則：**一進入紅霧就無敵**（霧開始湧出那一刻起，畫面上還看得到怪也打不到；到霧完全散掉才解除）、**任何特殊狀況下怪死了都要收掉紅霧**。
  `BloodMistBrain` 化霧／現身改看霧濃度不看幀號（湧出 0.35 秒、霧到 0.6 時本體淡掉；現身時本體淡回、霧吹散 0.30 秒），加 1.5 秒過場保險；`BloodMistVisual` 核對四條收霧路徑（死亡那幀 `ResetToBody`、Destroy→`OnDestroy`、停用→`OnDisable`、拆地圖連父節點清）。
  移除：`Elite Bloodguard/mist/`（含 .meta）、`MonsterAnimator` 的 `Mist` 動作與倒播、`MonsterSpriteLibrary` 預載、`MapSpriteLoader` 的 `alignByFirstFrame`（三支以 git HEAD 內容還原）。PROBLEMS **G17** 保留為通則並註明程式已移除。⚠ 要重跑 Sync Map Assets 讓 catalog 不再列 mist。
* [x] **血霧改成 Boss 開戰黑霧的紅色版（⏳ 未編譯未實測）**（2026-10-13，見 [BOSS_MODULE.md](BOSS_MODULE.md) §13.4）：作者看了第一版：「mist 序列圖有點醜，只能用在變身階段，後面追蹤移動要靠 shader 血霧；參考打 boss 前的黑霧，換成紅色」。
  第一版的 shader 是純程序 fbm＋Voronoi 黑斑、外型**仿 mist 序列圖**——等於把作者嫌醜的東西複製過去，而且重踩了 `UI/BossAura` 2026-09-04 就記下的坑（純 noise 做不出煙的絲與捲）。
  `BloodMist.shader` 整支改寫成 BossAura 的世界空間版：**共用同兩張煙霧密度圖**、同一套翻騰／疊層／細絮／散射／吹散，全螢幕改成包住怪物的一團（外緣被霧體密度啃成煙狀），黑灰改暗紅→血紅；加 `_WorldOffset` 讓煙在移動時往後流。numpy 照算式＋真的密度圖預覽三輪（第一輪發光圓盤＋霓虹 → 放大霧團、壓暗、散射減半 → 收進 quad 留柔邊）。
  **通則**：**作者已經認可過的同類特效（這裡是 BossAura），新特效先從它改**，不要從零設計；也不要拿作者嫌醜的素材當外觀參考。
* [x] **新怪「菁英血衛士」Elite Bloodguard（37）＋血霧型模組 `BloodMistBrain`（⏳ 未編譯未實測）**（2026-10-13，見 [BOSS_MODULE.md](BOSS_MODULE.md) §13）：
  作者規格：近戰；追擊一律化成血霧（mist 序列圖＋shader 血霧），**霧態無敵**，貼身才現身揮刀，逼玩家在牠近身時反擊；雙手同時下劈一刀，傷害要對準攻擊幀。
  作者拍板四點：子彈**穿過**霧（不是打中無傷）、現身＝**mist 加速倒播**、**距離夠遠才化霧**（近距離用 walk、打得到）、霧態與 `Speed` 同速。<br>
  **量幀**：四組都是 512 畫布、腳底一致（F28 無虞）。attack 第 11 幀刀光掃到最低最前（bbox 右緣 384→496）⇒ `AttackHitFrame 11`；mist 1~6 站姿、7~19 化霧、20~25 散去 ⇒ 化霧用 6~19、第 9 幀起無敵。<br>
  **做法**：攻擊段**委派 `MeleeChaseBrain`**（不重寫踩過坑的門檻與命中判定）；無敵＝`MonsterController.SetUntargetable` 換到新的 **Layer 10 `Mist`**（所有瞄準 Enemy 層的武器天生穿過）＋`TakeDamage` 防線；血霧是程序化 shader `BloodMist`（照 mist 第 13~19 幀的色階與形狀，先用 numpy 移植版對照素材調四輪再進 shader）。<br>
  **寫程式前攔下的坑**（PROBLEMS **G17**）：怪物載圖是**逐幀**量最低像素對齊腳底，霧往下溢出的那幾幀會被整團往上推 ⇒ `GetFrames`／`GetAnimationFrames` 加 `alignByFirstFrame`（整個動作共用第一幀 pivot），影子錨點也改沿用 idle。<br>
  **CSV 起手值**：HP 60、Scale 1.5、ContactDamage（＝揮刀傷害）15、Speed 3.2、AnimFPS 14、AttackInterval 0.8、DetectionRange 25、掉落表 1、Faction 空。<br>
  **動到的共用元件**（全部加法、既有怪零變化）：`MonsterAnimator`（`Mist` 動作、`PlayOneShot` 的 `reverse`）、`MonsterSpriteLibrary`、`MapSpriteLoader`、`MonsterController`、`MeleeChaseBrain.IsBusy`、`TagManager.asset`。
  **通則**：要讓怪「某段時間打不到」，**換層比擋 TakeDamage 對**——武器找目標靠 LayerMask，只擋傷害會變成「子彈撞上去被吃掉、噴特效、卻不扣血」。
* [x] **對話立繪大小還原回 2026-09-13 版（580/130，截到腰）**（2026-10-11）：作者發現對話立繪放大很多、只剩胸口以上。原因是 2026-09-21「新手夢境教學」那次 commit 把 `TalkPanel.cs` 改成 `AvatarHeight 880`／`AvatarOverlap 400`／內側緣距中心 140（當時沒記 PROGRESS、DRAMA.md 也沒跟著改，所以沒人發現）。只還原三個常數：580／130／**距中心 310**（＝9/13 版「距畫面邊 650」在 1920 寬的等價值）；同次加的「錨畫面中心＋外側不超出對話框」與邊緣羽化保留。DRAMA.md 一併改對。
  **通則**：露出在對話框上方的高度物理上限約 503，所以「截到身體哪裡」由 (Height − Overlap) ÷ Height 決定——兩個一起加＝特寫，不是單純放大。
* [x] **B17 第二輪：重算時明確排除正在消失的物件（⏳ 待實測）**（2026-10-11）：第一版實測無效；加診斷 log 後確認「停用碰撞體後同一幀 OverlapCircle 仍打得到桌子自己」（Composite 延後重建）。`RefreshArea` 加 `removed` 參數，查詢結果屬於該物件的一律略過。診斷 log 保留（每打爛一個地上物印一行，確認穩定後可刪）。
* [x] **修：可破壞地上物打爛後，怪物還繞著它原本的輪廓走（⏳ 未實測）**（2026-10-11，PROBLEMS **B17**）：作者把餐廳大餐桌改成可破壞，打爛後血族士兵仍沿著桌子輪廓繞、打不到人。原因：怪物尋徑格只在進場時把家具蓋進去一次。`MapNavGrid` 存底圖 `_baseWalk`＋新增 `RefreshArea(Bounds)`／`Version`；`DestructibleObject.Die` 先停用碰撞體再局部重算；`MonsterActuator` 偵測版本變化就重算路徑。
* [x] **血族士兵 VampireSoldier（15）從暫定值調成可上場的數值（⏳ 未實測）**（2026-10-11）：作者請「新增血族士兵（一般近戰）」——查到 15 號早已存在（`MeleeChase`、命中幀 11、圖 9/15 已 commit：idle 16／walk 21／attack 25，256 畫布一致），但數值是測試用暫定值。作者同意後比照實測過的揮舞型近戰（ZhaYu 大刀 20）改：`Speed` 1→**3**、`AnimFPS` 8→**14**（一刀 3.1→1.8 秒，命中 0.71 秒）、擊退 1／120→**10／50**、`DisplayName` 血族士兵、`Faction` 空→**Vampire**（與血蝠、血術士同族）。影子錨點待作者跑工具。NpcTable 的 NPC 版「血族士兵」（列隊用）不受影響。
* [x] **射手型加「近身模式」：射不到又找不到站位就用身體撞，絕不發呆（⏳ 未實測）**（2026-10-11）：作者附圖——血術士在你正上方窄走道（左櫃子、右椅子），不靠近也不攻擊。原因：直徑 1.6 的血火珠在那條走道哪裡都射不出去（判斷正確），找站位失敗後退回「挪一小步」，目標落在家具上走不過去 ⇒ 發呆。`ArcherBrain` 新 `Phase.Rush`：① 視線被擋且找不到站位、② 有目標卻 5 秒沒出手 ⇒ 走向目標吃接觸傷害，每 0.25 秒檢查射得到就立刻拉弓，最多 3 秒重新評估。所有射手型共用。
* [x] **修：血術士施法後咒文一出手就撞椅子——視線寬度改用子彈真正的半徑（⏳ 未實測）**（2026-10-11，PROBLEMS **F37**）：作者附圖（血術士在桌右上、咒文撞桌消失），先討論原因與解法再動手；作者提「把桌子改可損毀」，我建議不要（打爛之前照樣對著桌子射、而且會改變地圖動線），改成判斷與飛行同一份幾何：`MonsterWeaponUser.ProjectileRadius`、`ArcherBrain` 視線寬度 `max(0.18, 配方 Radius)`、找站位寬度＋0.27、另查起點那一圈。作者選先做這項，**血火咒子彈半徑 0.8 暫不調小**（會連玩家手感一起變）。
* [x] **射擊站位第二輪：不在桌角邊緣停、射不到不發呆、換站位偏好同側（⏳ 未實測）**（2026-10-11）：作者實測上一版「好一點點：往上走兩步、原地抖抖抖、再走兩步，太蠢、易被放風箏」。三個成因與對應：一走到擦邊看得到就停（玩家一動又被擋）⇒ 好站位改用粗視線 0.45 ＋ 目標左右各 0.8 都射得到，途中也只在有餘裕時才停；每次停下都 Observe 0.4~0.8 秒 ⇒ 射不到就直接接下一段（最多連 6 段）；站位失效重找時跳到另一側 ⇒ 每離舊站位 1 單位加 0.6 路長懲罰。
* [x] **修：射手型隔著長桌原地晃、不攻擊——視線被擋改成主動找射擊站位（⏳ 未實測）**（2026-10-11，PROBLEMS **F36**）：作者附圖（血術士在長桌左、玩家在右）。舊邏輯只會「繞一個角度挪一小步＋隨機換邊」，走不出長桌的遮蔽。`ArcherBrain` 新增 `TryFindFiringSpot`：目標周圍三圈每 15° 取候選、篩可走＋射得到、前 6 近者算 A* 路長取最短，一路走過去、途中每 0.5 秒重驗、射得到就停；找不到才退回舊側移。所有射手型（狂族弩手、ZhaYu_Gun、血術士）一起生效。
* [x] **血術士射程延長 1/3：射手型射程改成可逐怪設定（⏳ 未實測）**（2026-10-11）：作者：「射程太近，要一直靠近玩家」。射程原本是 `ArcherBrain` 的全域常數（弩手與 ZhaYu_Gun 共用），所以 MonsterData 表尾加 **`ShootRange`** 欄（索引 39；留空＝5.25，`PreferredRange` 自動按 4.1/5.25 的比例跟著算），血術士填 **7**（＝5.25×4/3）。既有射手零變化。
* [x] **血火咒三修：擊中特效太大、撞牆反彈看不出來、血術士走路偏小（⏳ 未實測）**（2026-10-11）：作者實測附圖。
  ① 擊中特效大到蓋住半個畫面 ⇒ `MonsterWeaponUser` 播擊中特效乘的是 `BulletScale`（8），玩家端是 `CastVisualScale`（1）；改成與玩家同規則（PROBLEMS **F34**；ZhaYu_Gun 的炮彈爆炸也因此縮回與玩家版一致）。
  ② 撞牆沒反彈 ⇒ `BounceBehavior`（BallisticsSystem）反彈時把圓心放在 `hit.point`（牆面接觸點），半徑 0.8 的血火珠整顆陷進牆裡；改用 `hit.centroid`（PROBLEMS **F35**，所有反彈子彈共用，小子彈幾乎不變）。
  ③ 血術士移動時偏小 ⇒ `WalkScale` 0.9→**0.96**。
* [x] **新武器「血火咒」（49）：掌心雷改紅色＋撞牆反彈，血術士與玩家共用（⏳ 未實測、icon 暫代）**（2026-10-11）：
  作者規格：以掌心雷改造、紅色外型、碰牆反彈一次，讓玩家也能裝備；icon 先複製現成的暫代。<br>
  **紅色怎麼來的**：表格沒有染色欄，所以產新素材——掌心雷的雷珠（8 張）與電爆（9 張）**依每個像素的亮度重新上色**（深血紅→赤紅→亮紅→淡粉紅核心，亮度 ^1.6 壓暗），形狀、閃電紋、pixel 硬邊全保留。第一版色帶偏橘／奶油色，壓深一輪才是血紅。產出 `Resources/Weapon/animation/bloodFireOrb/`、`Resources/VfxEffects/BloodFireBurst/`，`.meta` 照原檔（Sprite／Point／PPU 100）換新 guid。<br>
  **資料**：配方 **87**（＝79：間隔 0.8、速度 8、半徑 0.8、壽命 3；**拿掉迸電子武器 48**、`BounceTarget=Environment`、`MaxBounces 1`）；VfxTable **58** 血火爆；WeaponTable 49 從「血術法彈」改成**血火咒**（傷害 4、耗魔 2.5、`BulletScale 8`、擊中特效 58）；ItemTable **49**（icon `Weapon/single/weapon_bloodfirecurse`＝掌心雷 icon 的複本）。<br>
  **為什麼拿掉迸電**：電弧是雷系、跟血火不搭；而且怪物的命中路徑不跑子武器（BOSS_MODULE §8.4），留著的話玩家版與血術士版會是兩種行為。<br>
  **沒做**：沒放進祭壇抽選池（`BaseWeaponRoll`）——要讓玩家拿得到得另外決定來源（抽選／掉落／劇情給）。
* [x] **血術士大小：walk 拉齊＋整體放大**（2026-10-11）：作者實測「walk 時明顯比 idle／attack 大（約 1：0.9），整體太小、要 1.2」。CSV `WalkScale 0.9`（在自動對齊之上再乘，把 walk 拉回與 idle／attack 同大）＋ `Scale` 1→**1.33**（＝1.2÷0.9）⇒ 三個動作都是原 walk 的 1.2 倍。
* [x] **血術士 Blood Warlock：第一隻法系怪，走射手型 `ArcherBrain`（⏳ 未實測、子彈圖暫代）**（2026-10-11，見 [BOSS_MODULE.md](BOSS_MODULE.md) §8.5b）：
  作者：「第一個法系怪物，基本上跟狂族弩手差不多，遠程型」。行為完全吻合 §8 ⇒ **不寫新 Brain**，`BrainType=Archer`。<br>
  **素材**：作者給的是三張精靈圖（attack 2560² ＝ 5×5、idle／walk 2560×1536 ＝ 14／12 格）⇒ 切成 512 單張（25／14／12，畫布一致不中 F28），原圖連 `.meta` 移到 `_claude_tmp/BloodWarlock_sheets/`。<br>
  **放彈幀**：量 bbox——1~5 舉杖、6~7 前舉、7 之後法杖頂端高度不再變 ⇒ `ReleaseFrame 9`。<br>
  **資料**：WeaponTable **49「血術法彈」**（傷害 4、配方 44、子彈圖暫借 `bloodSawblade`、`BulletScale 5`，沒放 ItemTable——怪物用武器只讀 WeaponTable）；MonsterData **36**（HP 25、Speed 2.6、AnimFPS 14、`DetectionRange 25`、`Faction=Vampire`、掉落表 1）。
* [x] **修：血蝠放在地圖上排牆上卻完全看不到（⏳ 未實測）**（2026-10-11，見 [PROBLEMS.md](PROBLEMS.md) **F33**）：作者在 BloodFang_BrewingRoom 最上排牆放 5 個血蝠出生點，進圖看不到。初版把出生點當地面點再往上抬倒掛高度 ⇒ 被抬出地圖頂端；地面點又在牆裡、`FlyTowards`「下一步是牆不走」⇒ 永遠飛不出來。改成**出生點＝空中位置、影子在正下方**（`GroundBelow`），起點在牆裡時不擋。驗算這張圖：5 隻倒掛在 y −1.5、影子落在 y −4.81 的地板（第 19 子格列）。
* [x] **血蝠偵查範圍改成 25（與全專案怪物一致＝全地圖偵測）**（2026-10-11）：作者指出其他怪全是 25，血蝠的 8 是我起手估的、不是「原狀」。倒掛被看見改由「發現後延遲 3 秒醒」保證。
* [x] **血蝠偵查範圍恢復 8＋發現玩家後先倒掛約 3 秒才進攻（⏳ 未實測）**（2026-10-11）：作者要偵查範圍大，又不想玩家完全看不到倒掛的模樣。`FlyingBrain` 加 virtual `WakeDelaySeconds()`（基底 0），`BatBrain` 回 2.6~3.4 秒隨機（一群不同步鬆爪）；等待中目標離開就重新計時、被打立刻醒、歸巢途中發現不等。
* [x] **血蝠倒吊縮小＋俯衝後不再半空停住（⏳ 未實測）**（2026-10-11）：作者實測兩點——① 倒掛明顯比飛行大 ⇒ CSV `HangScale` 填 **0.7**（倒吊可見高 335px vs 飛行約 230px）；② 俯衝後停在張嘴最後一幀太久、像半空中停住 ⇒ `FlyingBrain.CutFrame` 預設從「命中幀＋2 並原地撐住」改成「命中幀」，咬到同一幀就接 walk 往前飛＋拉高；CutFrame 調大時命中後也會沿俯衝方向繼續滑。<br>
  **通則**：飛行怪在空中**任何**停頓都讀成卡住——地面怪「站定把動作做完」的節奏不能直接搬到空中。
* [x] **血蝠放慢＋偵查範圍減半（⏳ 未實測）**（2026-10-11）：作者看過第一版「很棒，但太迅速」，想看倒吊時的狀況。CSV `Speed` 4→3、`DetectionRange` 8→4；俯衝只放慢 attack（`BatBrain` 覆寫 `AttackFpsMul` 0.8→0.6，起手 0.67 秒、命中前 0.53 秒鎖定），不動 `AnimFPS`（會連振翅一起變慢）。<br>
  **連帶修正**：範圍砍半會讓牠盤旋時一直「失去目標」——感測器量的是被抬高的 transform 到玩家的斜邊（盤旋時本來就 ≈3.5）。`FlyingBrain` 改成量**地面距離**，並把「驚醒距離」（`DetectionRange`）與「放棄距離」（× 2）拆開。
* [x] **第一個飛行模組：`FlyingBrain`（基底）＋`BatBrain`（蝙蝠類）＋血蝠 Bloodbat（⏳ 未編譯未實測）**（2026-10-11，見 [BOSS_MODULE.md](BOSS_MODULE.md) §12）：
  作者規格：吸血鬼陣營血蝠，四個動作 idle（坐地，給部隊列隊）／hang（倒吊，真正的待機）／walk（其實是飛行）／attack（往前斜下俯衝咬）。平常倒吊發呆，敵人進範圍就在上方盤旋、到斜上方時俯衝咬，咬沒咬到都繼續往前飛並拉高——V 字路徑，但素材沒有後半段拉升，所以後半段用 walk 補。要先有通用飛行模組再繼承出蝙蝠。<br>
  **難在哪**：俯視角的 Y 同時是高度與深度。沿用 §9 的 `IAirborneVisual`（地面點＋高度），但跳躍只騰空 0.5 秒、蝙蝠**一直**在空中，於是：A* 不能用（拿被抬高的 transform 查格子）→ 地面點由 Brain 自己推、`MapNavGrid` 加只看牆的 `IsFlyableWorld`；`HandleVisuals` 懸停時會把牠切成 idle（＝半空中坐著）→ 加 `BrainPoseActive`／`BrainPose`／`BrainFacing`；盤旋時抬高的身體框會擦到玩家 → 接觸傷害關掉、只在命中幀咬（同 §10.4）。<br>
  **做法**：新 `Behaviors/FlyingBrain.cs`（Rest/Wake/Engage/Attack/Exit/Return 狀態機、盤旋、只從左右兩側出手、落點在命中前 0.4 秒鎖定、`Physics2D.Distance` 判咬、外力位移吸收）＋`Behaviors/BatBrain.cs`（覆寫休息姿勢＝hang、鬆爪下掉、地圖上方 30% 隨機棲點）；`MonsterAnimator` 加第六個動作 `Hang`，**飛行怪 walk/attack/hang 不做自動高度對齊**（坐姿當基準會把倒吊縮成 0.63 倍）；`MonsterData.csv` 表尾加 `HangScale`（索引 38，舊列不補）；掉寶改落在地面點。<br>
  **作者拍板（2026-10-11）**：牆擋、水/坑/家具不擋；失去目標到地圖上方另找點倒吊；倒吊時打得到、被打就醒；**不開擊退免疫特例**，血蝠 CSV `KnockbackThreshold` 填 2000；坐地 idle 走 NPC 系統；大小比照 jump；Y 排序照地面點。<br>
  **資料**：怪物 **35 Bloodbat**（HP 30、`BrainType=Bat`、Scale 0.6、Speed 4、AnimFPS 25、咬 8、俯衝間隔 3、驚醒距離 8、`Faction=Vampire`、`AttackHitFrame 15`、掉落表 1）——全是起手估值。<br>
  **驗證**：python 靜態模擬盤旋＋俯衝（落點鎖定時機掃 1/7/10/11 幀）：鎖在第 7 張時站著不動 100%、隨意走動約 50%、看到起手 0.4 秒內側閃 0%、0.5 秒才閃 100%——鎖得越晚越「出手必中」，鎖在第 10 張連閃都閃不掉。<br>
  **通則**：「一直在空中」的角色，所有讀 transform 的系統都要問一次「它要的是空中的位置還是地面點」——尋徑、掉寶、影子、排序要地面點；碰撞、被瞄準、特效罩身體要空中的位置。
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
