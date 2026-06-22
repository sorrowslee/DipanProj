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
| [BUILD_AND_DEPLOY.md](BUILD_AND_DEPLOY.md) | 打包/部署流程(Project Tools 選單、打包前對齊遠端 main、疑難排解) | 打包、部署、build/git 出錯時 |
| [PROBLEMS.md](PROBLEMS.md) | **踩坑記錄與解法**(症狀→原因→解法) | **第一次看文件時必看**；遇到怪問題、或要把新坑記下來時 |

## 給接手 AI 的提示
- 這些文件是把原本單一大 README 拆開的；**依當前任務挑相關的讀**即可，不必全讀。
- **第一次接觸這專案時，先讀 [PROBLEMS.md](PROBLEMS.md)**：裡面是實際踩過的坑與解法，能省下大量重複除錯。
- **遇到新的坑、或解掉一個非顯而易見的問題後，務必到 [PROBLEMS.md](PROBLEMS.md) 新增一則**（格式：症狀 → 原因 → 解法），讓後面的人/AI 不必重踩。
- 跨模組／邊界相關的改動，先讀 [ARCHITECTURE.md](ARCHITECTURE.md) 的「解耦原則與邊界規範」。
- 大多數玩法擴充是「在 CSV 加一列 ＋ 既有系統參數」就能做到；動程式前先確認不能用資料驅動解決。
