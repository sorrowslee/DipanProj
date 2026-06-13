# 燃燈計畫 (Project Dipankara) — 開發文件總覽

2D 俯視角動作遊戲（Steam）。核心概念啟發自《無限恐怖》：場景中有一尊**燃燈古佛**為守護者，真正的邪惡能量從佛像背後的**隧道**散發。核心戰鬥迴圈與底層架構已完成，採 CSV 資料驅動的配方／武器系統。

> ⚠️ **要產任何圖片素材前，務必先讀 [AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md)**（已驗證的產圖流程、去背方式、踩過的雷；產「飛行投射道具」時特別看第 7 節）。

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

## 給接手 AI 的提示
- 這些文件是把原本單一大 README 拆開的；**依當前任務挑相關的讀**即可，不必全讀。
- 跨模組／邊界相關的改動，先讀 [ARCHITECTURE.md](ARCHITECTURE.md) 的「解耦原則與邊界規範」。
- 大多數玩法擴充是「在 CSV 加一列 ＋ 既有系統參數」就能做到；動程式前先確認不能用資料驅動解決。
