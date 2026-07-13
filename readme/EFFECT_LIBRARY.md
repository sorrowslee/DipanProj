# 特效素材庫與預覽器（地圖編輯器端）

> 返回 [文件總覽](README.md)｜遊戲端一次性特效系統見 [VFX.md](VFX.md)｜地面特效見 [GROUND_EFFECT.md](GROUND_EFFECT.md)｜產圖見 [AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md)

在**地圖編輯器**裡瀏覽／輪播「買來的像素特效包」，挑到喜歡的再**手動複製進遊戲**用。素材只留在編輯器端、**永不被同步進遊戲**，要用才自己貼過去。

---

## 資料夾佈局（都在 `DipanProj_MapEditor/`，且刻意都放在 `Assets/` 之外）

| 位置 | 內容 | 版控 |
|---|---|---|
| `allEffects/` | 下載回來的**原始特效包**（每包一個子資料夾，結構各異） | 忽略 |
| `Effects/` | **整理好的特效庫**（預覽器讀這裡）：`<包名>/<動畫>/<顏色>/<動畫>_NNN.png` | 忽略 |
| `Effects_Recolored/` | 預覽器「匯出換色版」烤出來的 PNG | 忽略 |

**為什麼放 `Assets/` 外**：預覽器用 **File IO 直接讀檔**、不需要 Unity 把圖 import 成 Sprite。這些是**好幾萬張圖**，若放進 `Assets/`（含 `Resources/` 或 `StreamingAssets/`），Unity 會逐一生 `.meta`、開專案與進 Play 都會明顯變慢。放 `Assets/` 外，Unity 完全不追蹤，編輯器不變慢。

**為什麼不會被 sync 進遊戲**：素材同步（選單 `同步素材` / `Tools/sync_assets.sh`）只從 `DipanProj_Main/GameAssets` 的 `Environment` / `Tiles` / `Background` 掃，且只寫進編輯器的 `StreamingAssets/MapAssets`。`Effects/` 不在任何掃描範圍，天生不會流進遊戲。

---

## 兩支整理腳本（`Tools/`）

### `organize_effects.py` — 單一、結構統一的 Gigapack
針對 unTied「Super Pixel Effects Gigapack」那種統一結構（`PNG/<類別>/<動畫>/<動畫>_large_<色>/frameXXXX.png`）。只取 **PNG ＋ large**，輸出 `Effects/<類別>/<動畫>/<色>/`。
- 用法：`python3 Tools/organize_effects.py`（`--dry` 只統計不寫檔）。

### `organize_bundle.py` — 一大包、每包結構都不一樣（通用）
針對「一次買一大包、每包內部布局各異」（FX／投射物／wills 系列…）。**自動偵測**，不管結構怎樣都通用：
- 遞迴找「直接含 `frameXXXX.png` 的資料夾」＝一個動畫序列；spritesheet／tileset／UI／靜態圖沒有連號序列，**自動跳過**。
- 只取 **large**（skip small）；沒有 size 標記的（如 wills 系列）一律保留。
- 變體(顏色/風格)自動判：`<名稱>_large_<色>[_字尾]` 的色、或 `style_A~D`、否則 `default`。size/色在中段、後面還有字尾（如 `_small_blue_back`）也能正確解析。
- 正規化輸出：`Effects/<包名>/<動畫>/<變體>/<動畫>_NNN.png`。
- 用法：`python3 Tools/organize_bundle.py`（全部）｜`... "Fantasy" "Sci-fi"`（**只處理名稱含這些字串的包**，量大時分批跑避免逾時）｜`--dry` 只統計。
- 來源 `allEffects/`、輸出 `Effects/`；`Super Pixel Effects Gigapack` 會**自動跳過**（改用上面那支整理，避免重複）。
- 之後再買新包：解壓縮丟進 `allEffects/`（一包一子資料夾）、跑一次即可。

> ⚠️ 腳本以**複製**方式寫入、可重跑，但**不刪舊檔**（相容於某些環境不允許刪檔）。若曾用舊版規則跑出過「名稱含 `_large_`/`_small_` 的垃圾資料夾」，正確命名不會含這兩個 token，可安全清掉：
> `find DipanProj_MapEditor/Effects -maxdepth 2 -type d \( -name '*_large_*' -o -name '*_small_*' \) -exec rm -rf {} +`

---

## 預覽器（地圖編輯器「特效預覽器」頁籤）

工具列點「**特效預覽器**」：左側是依包/類別分組的動畫清單（含搜尋框），右側大視窗輪播選中的動畫。控制項：
- **顏色選擇**（各動畫實際有的色）、**FPS**（預設 15、可 −/＋）、**播放/暫停/重播**。
- **暗黑風換色**：原色／壓暗／暗紅／冷藍／去飽和壓暗，即時預覽。
- **匯出換色版**：把套色後的幀烤成 PNG 到 `Effects_Recolored/`（換色只影響預覽，要用進遊戲得先匯出）。

讀取來源 ＝ `Effects/`（`EffectLibrary.Root` 指向 `Assets/` 外）。相關程式：
`Core/EffectLibrary.cs`（掃描＋載幀、Point 濾鏡）、`Core/EffectRecolor.cs`（逐像素換色＋匯出）、`UI/EffectPreviewUI.cs`（面板）、`Tools/EditTool.cs`（新增 `EffectPreview`）、`UI/EditorUI.cs`（頁籤接線；開預覽器時收起其他彈窗、點別的頁籤自動離開）。

---

## 挑好一個特效 → 用進遊戲的步驟

1. 在預覽器挑好某**動畫＋顏色**（或用「匯出換色版」得到暗色版）。
2. 把那個資料夾的幀複製進遊戲 `DipanProj_Main/Assets/Resources/VfxEffects/<新名>/`。
3. **檔名對齊遊戲載入格式**（`VfxManager` 讀 `_NN`＝`_{i:D2}`：`_01`~`_09`、`_10`…、`_100`…）：
   - **用「匯出換色版」得到的幀**：已經是 **2 位補零**（`_01`…，2026-07-09 起 `EffectRecolor.ExportColorSet` 改對齊），**直接複製、免改名**。
   - **直接複製原色庫 `Effects/` 的幀**：那邊是 `_NNN`（3 位，`organize_bundle.py` 輸出），需改名 `_001`→`_01`。快速改名：
     ```bash
     for f in *_[0-9][0-9][0-9].png; do n="${f##*_}"; n="${n%.png}"; \
       mv "$f" "${f%_*}_$(printf %02d $((10#$n))).png"; done
     ```
4. 在 `Assets/Data/VfxTable.csv` 加一列（`AniPath`／張數／`AnimFPS`＝15／`Scale`／`Loop`…），欄位細節見 [VFX.md](VFX.md)。
5. **大小要調 `Scale`**：新圖以 **PPU 100** 匯入，世界大小 ＝ `原生px ÷ 100 × Scale`；依原本特效的視覺大小換算一個起始值，實機再微調。

> 範例：九霄雷獄使用 `fanfx2_lightning_spell_tileable_start/loop` 組成全高雷柱，落點另接 `epic_explosion_B`；入選幀分別放進 `Resources/VfxEffects/SkyLightningColumn` 與 `SkyLightningExplosion`。細節見 [LASER.md](LASER.md) 落雷模式。

---

## 注意事項
- 這些素材風格偏鮮豔；暗黑恐怖場景建議先用預覽器「換色」壓暗/偏色再匯出使用。
- 部分包有 `black`/`pink`/`white` 等額外顏色，預覽器照樣可播（只是那幾個顏色鈕底色顯示灰，無傷）。
- **一律只留在編輯器端**：不要把 `Effects/` 或 `allEffects/` 整包複製進 `DipanProj_Main`；遊戲只收你手動挑好、貼進 `Resources/VfxEffects/` 的那幾個。

---

*建立於 2026-07-08：地圖編輯器端特效素材庫（`allEffects/` 原始包 → `organize_bundle.py` 通用整理 → `Effects/` → 特效預覽器瀏覽/換色/匯出）。整理庫放 `Assets/` 外避免 Unity 匯入拖慢；不被 sync；挑好再手動複製進遊戲（Scale 調整；直接複製原色庫需 `_NNN`→`_NN` 改名，「匯出換色版」自 2026-07-09 起已直接輸出 2 位命名、免改名）。*
