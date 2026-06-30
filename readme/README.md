# 燃燈計畫 (Project Dipankara) — 開發文件總覽

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
| [COMBAT.md](COMBAT.md) | 戰鬥傷害系統：玩家 HP/MP（CombatStats）、中央傷害結算（DamageInfo/CombatSystem）、武器耗魔（ManaCost）、怪物接觸傷害＋減傷、血/魔 HUD、頭上浮動傷害數字、加成/減傷/DOT 掛勾 | 改傷害結算、加 HP/MP/魔耗、傷害加成/減傷、接觸傷害、血魔條、傷害數字 |
| [SHADOW.md](SHADOW.md) | 角色腳下橢圓影子（BlobShadow，程序生成、自動跟隨、玩家與怪物自動掛） | 改影子外觀/大小、給新角色加影子 |
| [CHARACTER_SETUP.md](CHARACTER_SETUP.md) | 主角外型：**已改走路線 B（程式逐格動畫、血統換外型）**——Characters/SequenceImage/<血統>/idle·walk·dead、`Bloodline` 欄/`SetBloodline`；下半為舊 Animator 流程（已取代，存參考） | 換主角外型、加血統、加死亡動畫 |
| [ANIMATOR_ADD_STATE.md](ANIMATOR_ADD_STATE.md) | Animator 新增一個狀態的速查（做 clip→加狀態→參數連線→程式觸發→清垃圾，單張/序列圖皆有） | 給角色加新動畫狀態（死亡/攻擊/受傷…） |
| [GROUND_EFFECT.md](GROUND_EFFECT.md) | 地面特效鏈式觸發 AOE ＋ 拋物線型武器 | 改地面特效、拋物線、AOE |
| [LASER.md](LASER.md) | 雷射光束系統 ＋ 雷射外型（BeamStyle 種類／調色盤） | 改雷射、加雷射種類或顏色 |
| [VFX.md](VFX.md) | 一次性特效：發射特效（玩家身上）＋ 擊中特效（命中點），VfxTable 原語 | 加／改發射或擊中特效 |
| [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) | RecipeTable 每個欄位的詳細用法、填寫規則與組合範例 | 填配方表細節時的查詢手冊 |
| [PROGRESS.md](PROGRESS.md) | 已完成項目的變更日誌（做過什麼、怎麼演進） | 想了解歷史脈絡 |
| [ROADMAP.md](ROADMAP.md) | 待辦與未來規劃 | 規劃下一步 |
| [AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md) | 產圖流程與規範 | **任何產圖前** |
| [MapEditor_DESIGN.md](MapEditor_DESIGN.md) | 地圖編輯器設計與 `.dipanmap` 格式 | 改編輯器、看地圖格式 |
| [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md) | 主遊戲端地圖載入器(MapLoader)、素材同步、Unity 接線 | 動地圖載入、牆/可走/出生點、素材同步 |
| [MAP_SYSTEM.md](MAP_SYSTEM.md) | 多圖串接（MapsTable／傳送點／MapManager）＋地圖狀態持久化概念 | 串接關卡多張地圖、傳送點、規劃狀態持久化 |
| [DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md) | 可破壞地上物 + 破壞特效(VfxTable ID 5) | 改地上物破壞、換破壞特效 |
| [UI_SYSTEM.md](UI_SYSTEM.md) | UI 底層框架（uGUI + 全程式建構、多場景常駐、UIManager/UIPanel/UIBuilder、暫停與輸入閘門） | 做任何 UI（背包/設定/HUD…）、加新面板前 |
| [INVENTORY.md](INVENTORY.md) | 背包系統（InventorySystem 資料層 + InventoryPanel、ItemTable.csv、icon 規格、格子座標） | 改背包、物品、裝備欄、加 icon |
| [STORAGE.md](STORAGE.md) | 倉庫系統（StorageSystem + IItemGrid、倉庫+背包並排面板、點擊/拖放搬運、每角色獨立、存檔接線） | 改倉庫、做搬運 UI、套倉庫圖 |
| [INTERACTION.md](INTERACTION.md) | 互動系統（InteractionManager：靠近按 F、道具拾取點、地上掉落物、星星標示、中央 toast） | 改拾取/掉落/觸發點、星星特效、提示 |
| [DRAMA.md](DRAMA.md) | 劇情系統（drama 觸發點、DramaTable.csv、DramaPanel 模態檢視介面） | 加/改劇情、做劇情圖文 |
| [SAVE_SYSTEM.md](SAVE_SYSTEM.md) | 本地存檔系統（persistentDataPath、多角色 roster、統一角色存檔、轉生繼承、JSON+校驗碼、Steam Cloud 預留） | 做存檔/讀檔、角色建立/轉生、物品持久化前 |
| [BUILD_AND_DEPLOY.md](BUILD_AND_DEPLOY.md) | 打包/部署流程(Project Tools 選單、打包前對齊遠端 main、疑難排解) | 打包、部署、build/git 出錯時 |
| [DISPLAY_SETTINGS.md](DISPLAY_SETTINGS.md) | 效能診斷面板(PerfHud：FPS/CPU·GPU ms/瓶頸/顯卡，按 P) ＋ 上架前玩家畫面設定(VSync/幀率上限/視窗模式)規劃 | 排查幀數/卡頓、做畫面設定選單 |
| [ATMOSPHERE.md](ATMOSPHERE.md) | 場景氛圍後處理（MapsTable 的 Atmosphere 欄 1~15：正常/幽暗/噩夢/烈日/焦土/沙塵/淺海/深海/深海+恐怖/風雪/強風/細雨/大雨/陰森森林鬼霧/電視雜訊；提燈光圈、熱浪、海洋折射、風絲、雨絲、漂移黑霧、雪花噪訊，地圖驅動、換圖即時切換） | 調某地圖陰森/炎熱/海洋/風雨/鬼霧/雜訊氛圍、做新地圖、改光圈/調色/扭曲 |
| [INTRO_COMIC.md](INTRO_COMIC.md) | 序章開場漫畫播放器（獨立 Intro 場景、全程式、`IntroComicController`）：**整頁成品圖＋導讀式鏡頭**——鏡頭平移/縮放對焦每格(Focuses：Area/Zoom/X·YOffset、可綁格)、空白鍵下一格、下墜 13~15 全篇幅 cover、右上角兩段式 Skip、整頁圖放 `Resources/InitialStory`、右鍵帶入預設。播完接墜落、落 `MainScene` 的 Tutorial_Cave | 改開場漫畫、調每格鏡頭框/縮放/位移、Skip、下墜節奏 |
| [INTRO_FALL.md](INTRO_FALL.md) | 序章「持續墜落深淵」程式動畫（獨立 Intro 場景、全程式建構、`IntroFallController`）：側面峽谷岩壁背景無限捲動＋散佈短碎條速度線 → 正面放射速度線＋時空扭曲 shader ＋色調穿越 → 收尾縮小沒入＋淡出載入下一場景 | 改開場墜落動畫、調速度線/山壁/色調/角色大小、接漫畫或進遊戲的串接 |
| [PROBLEMS.md](PROBLEMS.md) | **踩坑記錄與解法**(症狀→原因→解法) | **第一次看文件時必看**；遇到怪問題、或要把新坑記下來時 |
| [TODO.md](TODO.md) | **待補清單**：目前先做到能動但還缺一塊、或暫時做不到之後再補的項目 | 想知道哪些是半成品/暫缺；做到一半得擱置時把缺口記這裡 |

## 給接手 AI 的提示
- 這些文件是把原本單一大 README 拆開的；**依當前任務挑相關的讀**即可，不必全讀。
- **第一次接觸這專案時，先讀 [PROBLEMS.md](PROBLEMS.md)**：裡面是實際踩過的坑與解法，能省下大量重複除錯。
- **遇到新的坑、或解掉一個非顯而易見的問題後，務必到 [PROBLEMS.md](PROBLEMS.md) 新增一則**（格式：症狀 → 原因 → 解法），讓後面的人/AI 不必重踩。
- 跨模組／邊界相關的改動，先讀 [ARCHITECTURE.md](ARCHITECTURE.md) 的「解耦原則與邊界規範」。
- 大多數玩法擴充是「在 CSV 加一列 ＋ 既有系統參數」就能做到；動程式前先確認不能用資料驅動解決。
- **做到一半得擱置、或某功能暫時缺一塊**時，記到 [TODO.md](TODO.md)（缺口清單），別讓半成品的暫缺資訊散落。
