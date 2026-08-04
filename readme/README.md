# 燃燈劫 (Burning Lamp: Rebirth of Ruin) — 開發文件總覽

> **遊戲正式名稱**：中文《**燃燈劫**》／英文 **Burning Lamp: Rebirth of Ruin**。（開發代號 Project Dipankara / 燃燈計畫。）

2D 俯視角動作遊戲（Steam）。核心概念啟發自《無限恐怖》：場景中有一尊**燃燈古佛**為守護者，真正的邪惡能量從佛像背後的**隧道**散發。核心戰鬥迴圈與底層架構已完成，採 CSV 資料驅動的配方／武器系統。

> ⚠️ **要產任何圖片素材前，務必先讀 [AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md)**（已驗證的產圖流程、去背方式、踩過的雷；產「飛行投射道具」時特別看第 7 節）。
>
> 🧯 **第一次看這專案、或遇到怪問題前，先讀 [PROBLEMS.md](PROBLEMS.md)**（踩過的坑與解法）。**之後每遇到一個新坑，請在那裡新增一則（症狀→原因→解法），避免重複踩。**

## 文件地圖（依任務挑相關的讀，不必整包看完）

| 文件 | 內容 | 什麼時候讀 |
|---|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | 雙模組解耦、邊界規範、物理設定（Layer／Tag／碰撞矩陣）、美術資源架構 | 動到跨模組、Layer、物理、資源結構前 |
| [BALLISTICS.md](BALLISTICS.md) | 彈道系統 `Sorrows.Ballistics`：ProjectileData／BallisticsEngine／BulletInstance／行為介面 | 改子彈生成、飛行、碰撞、彈道行為 |
| [RECIPE_AND_WEAPON.md](RECIPE_AND_WEAPON.md) | CSV 雙表（RecipeTable／WeaponTable）、Recipe／WeaponManager、BounceTarget、序列圖、SpriteAngleOffset | 加／改武器、配方、CSV 欄位 |
| [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md) | 玩家控制器、怪物 AI、受擊反應系統（閃爍／擊退／無敵） | 改玩家或怪物行為、受擊反應 |
| [MONSTER_SETUP.md](MONSTER_SETUP.md) | 怪物量產（route B：程式逐格動畫、地圖素材管線載圖、零 prefab/Animator；idle/walk 必備、attack 可選、防呆；CSV 加 AnimFPS） | 量產新怪、換怪物外觀、加怪物動作動畫 |
| [MONSTER_SPEECH.md](MONSTER_SPEECH.md) | 怪物頭上對話框（CSV 句子1~4＋血量%門檻、發現玩家後隨機說、boss 頻率兩倍、水墨泡泡底板隨機輪流＋避邊鏡像） | 加/改怪物台詞、調說話頻率、換對話框底板 |
| [BOSS_MODULE.md](BOSS_MODULE.md) | Boss 戰鬥模組框架（一隻強怪＝一個 Brain 模組：MonsterContext／MonsterWeaponUser seam）＋紅嫁衣女殭屍（逃跑＋召喚）＋召喚做成表驅動武器（RecipeTable IsSummon 五欄） | 做/改 boss 與強怪戰鬥模式、加召喚、讓怪物用武器 |
| [COMBAT.md](COMBAT.md) | 戰鬥傷害系統：玩家 HP/MP（CombatStats）、中央傷害結算（DamageInfo/CombatSystem）、武器耗魔（ManaCost）、怪物接觸傷害＋減傷、血/魔 HUD、頭上浮動傷害數字、加成/減傷/DOT 掛勾 | 改傷害結算、加 HP/MP/魔耗、傷害加成/減傷、接觸傷害、血魔條、傷害數字 |
| [SHADOW.md](SHADOW.md) | 角色腳下橢圓影子（BlobShadow，程序生成、自動跟隨、玩家與怪物自動掛） | 改影子外觀/大小、給新角色加影子 |
| [CHARACTER_SETUP.md](CHARACTER_SETUP.md) | 主角外型：**已改走路線 B（程式逐格動畫、血統換外型）**——Characters/SequenceImage/<血統>/idle·walk·dead、`Bloodline` 欄/`SetBloodline`；下半為舊 Animator 流程（已取代，存參考） | 換主角外型、加血統、加死亡動畫 |
| [ANIMATOR_ADD_STATE.md](ANIMATOR_ADD_STATE.md) | Animator 新增一個狀態的速查（做 clip→加狀態→參數連線→程式觸發→清垃圾，單張/序列圖皆有） | 給角色加新動畫狀態（死亡/攻擊/受傷…） |
| [GROUND_EFFECT.md](GROUND_EFFECT.md) | 地面特效鏈式觸發 AOE ＋ 拋物線型武器 | 改地面特效、拋物線、AOE |
| [LASER.md](LASER.md) | 雷射光束系統 ＋ 雷射外型（BeamStyle 種類／調色盤） | 改雷射、加雷射種類或顏色 |
| [PIXEL_REFLECT_LASER.md](PIXEL_REFLECT_LASER.md) | Pack 4 像素雷射素材拆解、平鋪折線渲染、無限延伸／反射與武器「鏡界折光」 | 改像素雷射、反射上限或擴充 Pack 4 顏色／造型 |
| [VFX.md](VFX.md) | 一次性特效：發射特效（玩家身上）＋ 擊中特效（命中點），VfxTable 原語 | 加／改發射或擊中特效 |
| [EFFECT_LIBRARY.md](EFFECT_LIBRARY.md) | 地圖編輯器端「特效素材庫＋預覽器」：原始包(`allEffects/`)→通用整理腳本(`organize_bundle.py`)→`Effects/`→特效預覽器（瀏覽/換色/匯出）；放 Assets 外、不被 sync、挑好再手動貼進遊戲 | 整理/瀏覽買來的特效包、挑特效貼進遊戲前 |
| [EFFECT_WEAPONS.md](EFFECT_WEAPONS.md) | Effects 全庫審閱、特效武器化原則、已實作武器與下一批候選 | 從特效庫設計／擴充武器、查新武器素材來源 |
| [CHARGE_MODE.md](CHARGE_MODE.md) | RecipeTable 集氣模式：按住／放開流程、3 秒倍率、藍紅集氣特效與持續武器互斥規則 | 啟用集氣武器、調整集氣倍率／特效或排查輸入互斥 |
| [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) | RecipeTable 每個欄位的詳細用法、填寫規則與組合範例 | 填配方表細節時的查詢手冊 |
| [PROGRESS.md](PROGRESS.md) | 已完成項目的變更日誌（做過什麼、怎麼演進） | 想了解歷史脈絡 |
| [ROADMAP.md](ROADMAP.md) | 待辦與未來規劃 | 規劃下一步 |
| [AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md) | 產圖流程與規範 | **任何產圖前** |
| [MapEditor_DESIGN.md](MapEditor_DESIGN.md) | 地圖編輯器設計與 `.dipanmap` 格式 | 改編輯器、看地圖格式 |
| [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md) | 主遊戲端地圖載入器(MapLoader)、素材同步、Unity 接線 | 動地圖載入、牆/可走/出生點、素材同步 |
| [MAP_SYSTEM.md](MAP_SYSTEM.md) | 多圖串接（MapsTable／傳送點／MapManager）＋地圖狀態持久化概念 | 串接關卡多張地圖、傳送點、規劃狀態持久化 |
| [DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md) | 可破壞地上物 + 破壞特效(VfxTable ID 5) | 改地上物破壞、換破壞特效 |
| [UI_SYSTEM.md](UI_SYSTEM.md) | UI 底層框架（uGUI + 全程式建構、多場景常駐、UIManager/UIPanel/UIBuilder、暫停與輸入閘門） | 做任何 UI（背包/設定/HUD…）、加新面板前 |
| [BOTTOM_HUD.md](BOTTOM_HUD.md) | 底部操控列 HUD（燃燈佛框）＋ 左HP/右MP 液體血球（著色器液體、搖晃、懸停數字、暗場景調色）；取代舊左上角 HudPanel | 改底部 HUD、血球外觀/顏色/亮度/搖晃、血瓶槽位置 |
| [INVENTORY.md](INVENTORY.md) | 背包系統（InventorySystem 資料層 + InventoryPanel、ItemTable.csv、icon 規格、格子座標） | 改背包、物品、裝備欄、加 icon |
| [STORAGE.md](STORAGE.md) | 倉庫系統（StorageSystem + IItemGrid、倉庫+背包並排面板、點擊/拖放搬運、每角色獨立、存檔接線） | 改倉庫、做搬運 UI、套倉庫圖 |
| [GEM_SOCKET.md](GEM_SOCKET.md) | **能力珠鑲嵌系統**（物品實例：表格只是模板、玩家手上那一件另外存；能力容器：攻擊讀容器不讀武器表；GemTable 一種珠子一列；ItemManager 唯一工廠骰孔數/等級；RandomRules 全遊戲機率） | **動武器/裝備/背包/掉落/存檔之前**；加能力珠、改鑲嵌、調孔數機率 |
| [FORGING.md](FORGING.md) | 鍛造介面（Y 鍵開；鐵砧中央放武器/裝備、左右 6 個鑲嵌孔依裝備孔位數解鎖、底部兩顆按鈕尚未接功能；重用 ISlotView/SlotDragController 拖放地基） | 改鍛造介面、做鑲嵌/拆裝備、接鐵匠 NPC、加裝備孔位數 |
| [INTERACTION.md](INTERACTION.md) | 互動系統（InteractionManager：靠近按 F、道具拾取點、地上掉落物、星星標示、中央 toast） | 改拾取/掉落/觸發點、星星特效、提示 |
| [GACHA_SYSTEM.md](GACHA_SYSTEM.md) | **祭壇抽選系統**（邪佛廣場走到祭壇按 F 抽選：四層資料架構讓「武器/裝備/血統/道具」大項可隨時增刪、打通關卡解鎖新內容進池、老虎機表演＋十連結算面板；**金錢改成獨立數字不再是背包道具**、血統藥劑一次性換外型；新觸發 `openPanel`／`unlockRoll`、新條件「最低/最高完成關卡數」＋「條件不成立時」） | 加/改抽選大項或池內容、放新祭壇、改抽選介面表演、動金錢或血統 |
| [DRAMA.md](DRAMA.md) | 劇情系統（drama 觸發點、DramaTable.csv、DramaPanel 模態檢視介面） | 加/改劇情、做劇情圖文 |
| [LOCALIZATION.md](LOCALIZATION.md) | 多語系／語言表（`Language.GetText(id)` 唯一取用入口、`LanguageTable.csv` id,cn,en、LanguageTableProvider、預設中文支援英文、id 分段慣例） | 加/改玩家可見字串、切語言、做多語系前 |
| [WOODSHED_LAMP_TUTORIAL.md](WOODSHED_LAMP_TUTORIAL.md) | 柴房佛燈強制新手教學（紅嫁衣第一間房：撿佛燈→裝備→強制點亮的 phase 狀態機；地上物 disappearFlag、pickup toRealBag、FireOnly 鎖移動只放行開火、PlayerController.IsAuraActive） | 改柴房教學流程、教學寫死清單、地圖佈置/傳送門門檻 |
| [STOREROOM_POTION_TUTORIAL.md](STOREROOM_POTION_TUTORIAL.md) | 儲藏室 HP 藥水強制新手教學（紅嫁衣第二間房：撿藥水→左鍵放進藥水格→強制按 1 喝；DrinkOnly 鎖、喝完 `TriggerChain.Activate` togglePortal 開傳送點123） | 改藥水教學流程、教學寫死清單、地圖佈置 |
| [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) | 觸發鏈：trigger 接 trigger（next/startDisabled/旗標條件）、動作型 giveItem/teleportTo/cameraFocus、**進場觸發 onEnter（一進地圖自動觸發、0 格不塗格子）**、傳送點綠幕連動 | 編排「對話→給物品→開門→傳送」這類事件流程、加條件分支、做「一進房間就播對話」 |
| [SAVE_SYSTEM.md](SAVE_SYSTEM.md) | 本地存檔系統（persistentDataPath、多角色 roster、統一角色存檔、轉生繼承、JSON+校驗碼、Steam Cloud 預留）＋§14 進度層（周目=generation、完成關卡=clearedModules、金錢、出生點旗標、進度 API） | 做存檔/讀檔、角色建立/轉生、物品持久化、記錄關卡/周目進度前 |
| [TITLE_AND_SAVE_UI.md](TITLE_AND_SAVE_UI.md) | 標題畫面＋三欄存讀檔 UI＋GameFlowManager 總流程（新建/繼續/覆蓋/刪除、進廣場自動存、出生點旗標、in-place 輪迴 API） | 改開場進入流程、存讀檔畫面、標題、輪迴帶物 |
| [LEVEL_END_FLOW.md](LEVEL_END_FLOW.md) | 過關/死亡/返回廣場 → 卍字離場特效 → 結算畫面 → 回廣場（clearLevel 觸發＋fireOnFlag＋延時觸發＋倒數＋ResultPanel 美術；EndLevel 三模式） | 改過關/死亡表演、離場特效、結算版面、接 clearLevel |
| [RUN_PROGRESS.md](RUN_PROGRESS.md) | **關卡進度與臨時包（RunProgress）**：一趟關卡內怪死了不復生／撿過的不再出現／掉落物留原地（跨換圖記憶、離開關卡才重置）＋臨時包「通關才落袋、死亡歸零」＋取得物品統一入口 `GiveItem`＋F8 除錯疊層 | 改怪物重生/拾取記憶/掉落物、做掉寶、動臨時包或結算獎勵、加「給玩家東西」的新來源 |
| [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) | **劇情演出編輯器**：地圖編輯器「劇情」分頁排一段半演出半漫畫的過場（演員走位/對話/運鏡/置中漫畫/淡黑/螢幕特效/交棒，13 種步驟、`parallelNext` 與 `background` 並行、編輯器內預覽）；資料存 `.dipanmap` 的 `cutscene` | 做/改地圖內的劇情演出、開場山道那類過場；**注意與 CUTSCENE_TUNNEL（穿隧道播影片）是兩套** |
| [SELECT_SCRIPT.md](SELECT_SCRIPT.md) | 選擇劇本面板（邪佛發牌選關 UI；劇本＝背包道具、selectScript 鏈動作、素材依 module 自動載、特殊裂紋框）＋ ⏳ 從第 2 輪起「系統自動 3新+1舊」發牌的設計與前置缺口 | 改選劇本 UI、接邪佛發牌、做/規劃 3新+1舊 系統發牌 |
| [DEPLOY.md](DEPLOY.md) | 打包流程（Project Tools 選單、BuildScript 打包＋驗收、場景順序、打包疑難排解） | 打包、build 出錯時 |
| [ITCH_BUTLER.md](ITCH_BUTLER.md) | **itch.io & butler 手冊**：裝 butler（台灣 DNS 坑）、登入、換機器設定清單、常用指令（`status` 查推送）、PC 用 itch app 取得、費用、itch/butler 疑難排解 | 設定新 Mac、上傳出錯、查推送狀態、**任何 itch/butler 問題** |
| [DISPLAY_SETTINGS.md](DISPLAY_SETTINGS.md) | 效能診斷面板(PerfHud：FPS/CPU·GPU ms/瓶頸/顯卡，按 P) ＋ 上架前玩家畫面設定(VSync/幀率上限/視窗模式)規劃 | 排查幀數/卡頓、做畫面設定選單 |
| [ATMOSPHERE.md](ATMOSPHERE.md) | 場景氛圍後處理（MapsTable 的 Atmosphere 欄 1~15：正常/幽暗/噩夢/烈日/焦土/沙塵/淺海/深海/深海+恐怖/風雪/強風/細雨/大雨/陰森森林鬼霧/電視雜訊；提燈光圈、熱浪、海洋折射、風絲、雨絲、漂移黑霧、雪花噪訊，地圖驅動、換圖即時切換） | 調某地圖陰森/炎熱/海洋/風雨/鬼霧/雜訊氛圍、做新地圖、改光圈/調色/扭曲 |
| [MAP_ENTER_EFFECT.md](MAP_ENTER_EFFECT.md) | 進場一次性效果（MapsTable `EnterEffect` 欄，進圖播一次就結束）：睜眼醒來（後處理杏眼遮罩＋模糊對焦＋亮度暗角，用在初始洞窟）＋**玩家趴地→起身連動（倒播 dead 幀＝爬起，零新素材）**。與 Atmosphere/SceneEffect 的「持續狀態」分工 | 做進場過場（睜眼/昏迷/暈眩…）、改睜眼節奏、改趴地起身、加新進場效果 |
| [SCENE_EFFECT.md](SCENE_EFFECT.md) | 場景特效（世界端）：① 地圖級 SceneEffect（MapsTable 欄，火雨）②**可放置 SceneFx**（編輯器「場景特效」分頁逐個放、SceneFxTable 驅動、stream 煙/火/冰/毒＋portal 傳送門、編輯器即時預覽）。與 Atmosphere（螢幕後處理）分工互補 | 做煙霧圍巾/噴射/傳送門、加新特效種類、調外觀、火雨、編輯器內預覽 |
| [RESOURCE_LOADING.md](RESOURCE_LOADING.md) | 資源載入與載入頁（地圖改「分幀載入＋LoadingPanel」解決進場/換圖凍住；依關卡 Resources/Loading/<module>.png 顯示載入圖、停留秒數、鎖輸入不暫停） | 進場/換圖會卡、做載入頁、放各關載入圖、調分幀批量 |
| [INTRO_COMIC.md](INTRO_COMIC.md) | 序章開場漫畫播放器（獨立 Intro 場景、全程式、`IntroComicController`）：**整頁成品圖＋導讀式鏡頭**——鏡頭平移/縮放對焦每格(Focuses：Area/Zoom/X·YOffset、可綁格)、空白鍵下一格、下墜 13~15 全篇幅 cover、右上角兩段式 Skip、整頁圖放 `Resources/InitialStory`、右鍵帶入預設。播完接墜落、落 `MainScene` 的 Tutorial_Cave | 改開場漫畫、調每格鏡頭框/縮放/位移、Skip、下墜節奏 |
| [INTRO_FALL.md](INTRO_FALL.md) | 序章「持續墜落深淵」程式動畫（獨立 Intro 場景、全程式建構、`IntroFallController`）：側面峽谷岩壁背景無限捲動＋散佈短碎條速度線 → 正面放射速度線＋時空扭曲 shader ＋色調穿越 → 收尾縮小沒入＋淡出載入下一場景 | 改開場墜落動畫、調速度線/山壁/色調/角色大小、接漫畫或進遊戲的串接 |
| [CUTSCENE_TUNNEL.md](CUTSCENE_TUNNEL.md) | 過場鏈「穿隧道→播影片→換圖」（全程式、cutscene 觸發點啟動、一次性）：`CutsceneWatcher`(地圖端串接)＋`TunnelWalkController`(按鍵走出發光拱門、等比放大、白光收尾、Canvas 1200)＋`VideoPlayerOverlay`(StreamingAssets/Video 的 mp4、黑幕先蓋再準備、淡黑換圖、Canvas 1300)；影片放哪/觸發點參數(video/targetMapId)/Unity 接線 | 改穿隧道表演、接/換過場影片、調洞口/步數/晃動、cutscene 觸發點 |
| [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) | **核心迴圈企劃**（設計意圖，非程式規格）：輪迴為主軸、access／power 兩軸分離、邪佛「3新+1舊」四選一與業障回響、正常 3 輪／上限 4 輪破邪佛、帶物 min(周目,7)、20 關深/標準分層、關卡內收穫「通關才落袋、死亡歸零」 | 想確認玩法方向與已拍板的規則；做任何影響經濟/關卡選擇/輪迴節奏的功能前 |
| [PERF_QUALITY_AUDIT.md](PERF_QUALITY_AUDIT.md) | 效能與畫質稽核（2026-07-05 四大根因與修正：Rigidbody2D Interpolate、60Hz 物理、地圖貼圖 Bilinear+mipmap、UI maxTextureSize；素材尺寸規範） | 排查卡頓/畫面粗糙、訂素材尺寸前 |
| [PROPS_IMAGEGEN_LIST.md](PROPS_IMAGEGEN_LIST.md) | 地上物產圖清單（A 家具 12／B 裝飾儀式 12／C 庭院 6／D 廚房 7／E 儲藏 6／F 柴房 4／G 場景大物 2，共 49 件，各附提示詞與 credits 估算） | 要產一批地上物素材時挑清單、估成本 |
| [PROBLEMS.md](PROBLEMS.md) | **踩坑記錄與解法**(症狀→原因→解法) | **第一次看文件時必看**；遇到怪問題、或要把新坑記下來時 |
| [TODO.md](TODO.md) | **待補清單**：目前先做到能動但還缺一塊、或暫時做不到之後再補的項目 | 想知道哪些是半成品/暫缺；做到一半得擱置時把缺口記這裡 |

## 給接手 AI 的提示
- 這些文件是把原本單一大 README 拆開的；**依當前任務挑相關的讀**即可，不必全讀。
- **第一次接觸這專案時，先讀 [PROBLEMS.md](PROBLEMS.md)**：裡面是實際踩過的坑與解法，能省下大量重複除錯。
- **遇到新的坑、或解掉一個非顯而易見的問題後，務必到 [PROBLEMS.md](PROBLEMS.md) 新增一則**（格式：症狀 → 原因 → 解法），讓後面的人/AI 不必重踩。
- 跨模組／邊界相關的改動，先讀 [ARCHITECTURE.md](ARCHITECTURE.md) 的「解耦原則與邊界規範」。
- 大多數玩法擴充是「在 CSV 加一列 ＋ 既有系統參數」就能做到；動程式前先確認不能用資料驅動解決。
- **做到一半得擱置、或某功能暫時缺一塊**時，記到 [TODO.md](TODO.md)（缺口清單），別讓半成品的暫缺資訊散落。
*2026-07-27：文件地圖補上先前漏收的 CORE_LOOP_DESIGN / PERF_QUALITY_AUDIT / PROPS_IMAGEGEN_LIST 三份。*
*2026-07-28：新增 [GACHA_SYSTEM.md](GACHA_SYSTEM.md)（祭壇抽選系統）。*
*2026-07-29：新增 [FORGING.md](FORGING.md)（鍛造介面）。*
*2026-08-03：新增 [GEM_SOCKET.md](GEM_SOCKET.md)（能力珠鑲嵌／物品實例／能力容器）。*
