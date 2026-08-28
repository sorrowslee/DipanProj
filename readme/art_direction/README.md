# 美術規範準則資料夾 (Art Direction Guidelines)

> 返回 [文件總覽](../README.md)｜總綱見 [../ART_DIRECTION.md](../ART_DIRECTION.md)
>
> 這個資料夾放**各製作領域的分域準則**：總綱（ART_DIRECTION.md）定「畫面經營的六大紀律」，
> 這裡的每一份文件把紀律翻譯成**該領域實際動手時的規格**。
> 全遊戲美術一律照這套規範進行；規範有修訂就改這裡（一份內容一個家）。

## 分工

| 領域 | 負責 | 分域準則 |
|---|---|---|
| 場景（完整地圖背景） | ChatGPT（繪圖協力） | [SCENE_GUIDELINE.md](SCENE_GUIDELINE.md) |
| 場景地上物／家具／裝飾物 | ChatGPT | [SCENE_PROP_GUIDELINE.md](SCENE_PROP_GUIDELINE.md) |
| 人物／怪物遊戲內角色圖（序列圖用原圖） | ChatGPT | [CHARACTER_SPRITE_GUIDELINE.md](CHARACTER_SPRITE_GUIDELINE.md) |
| 人物／怪物對話立繪 | ChatGPT | [CHARACTER_PORTRAIT_GUIDELINE.md](CHARACTER_PORTRAIT_GUIDELINE.md) |
| UI／裝備／道具／技能 icon | ChatGPT | [UI_ICON_GUIDELINE.md](UI_ICON_GUIDELINE.md) |
| **場景特效**（SceneFx／VfxTable／地面特效等世界端表演） | **Claude（Cowork）** | [VFX_GUIDELINE.md](VFX_GUIDELINE.md) |
| **氛圍 shader**（Atmosphere 後處理／照明／全屏效果） | **Claude（Cowork）** | [SHADER_GUIDELINE.md](SHADER_GUIDELINE.md) |

**跨領域工作流程**：[AI_COMMISSION_WORKFLOW.md](AI_COMMISSION_WORKFLOW.md)——每次開新對話委託 GPT 繪圖時
「怎麼使用上面那些準則」的 SOP（準則＋範例錨點圖＋母版制度＋LOCKED/CHANGE ONLY 格式）。
ChatGPT 領域的六份文件由 GPT 依總綱擬定、作者驗收後於 2026-08-28 收錄。

> 之後新增領域準則：檔名比照 `<領域>_GUIDELINE.md` 放入本資料夾，
> 並更新上表與 [../README.md](../README.md) 文件地圖。

## 收錄原則

- 每份分域準則開頭註明「本準則實作總綱的哪幾條紀律」。
- 分域準則與總綱衝突時，**以總綱為準**，並回頭修分域準則。
- 領域現況的系統說明（怎麼接線、欄位意義）仍住在各主題文件
  （[ATMOSPHERE.md](../ATMOSPHERE.md)、[SCENE_EFFECT.md](../SCENE_EFFECT.md)、[VFX.md](../VFX.md)…）；
  這裡只放「怎麼做才好看」的規範，不重複系統說明。
