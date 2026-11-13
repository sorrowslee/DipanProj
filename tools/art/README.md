# tools/art — 美術流水線工具箱（立繪／角色圖／序列圖／UI 圖示）

> 返回 [文件總覽](../../readme/README.md)
> 這些腳本原本散在 `TempImage/<任務>/tools/`（`TempImage/` 不進 git），2026-11-13 全部收進這裡進版控。
> **以後要做下表任何一件事，先來這裡找現成工具，不要重寫。** 新寫的工具也放進對應資料夾、在下表補一行。

## 0. 資料夾

| 資料夾 | 內容 | 狀態 |
|---|---|---|
| `art_remake/tools/` | **主力工具組**（立繪重繪、去背、對齊、模擬面板、序列圖量測、影子）——下面的表都以這組為準 | ✅ 最新 |
| `art_remake/ref/` | 給 ChatGPT 的畫風錨點：`portrait/anchor_portraits.jpg`（核准立繪 4 張）、`anchor_sprites.jpg`（核准角色圖 3 張） | ✅ |
| `bloodline_Angelborn/` `bloodline_Gluttonborn/` `bloodline_Netherborn/` `bloodline_Qingqiuborn/` | 各血統當初的工具（多數已被 `art_remake/tools/` 同名新版取代，留作紀錄）；`*/fx/preview.py` 是 shader 的 numpy 預覽（見 §9） | 舊版 |
| `ui_restyle2/tools/` | UI 圖示換風格流水線（比 `ui_restyle/` 新） | ✅ |
| `ui_restyle/tools/` | 同上舊版（多一支 `sheet.py`：一張多格圖切成多個圖示、`latest.sh`） | 舊版 |
| `buddha_title/tools/` | 標題畫面邪佛動畫（頭手換幀、綠幕去背、調色、morph） | ✅ |

## 1. 開工作夾（每次做事先建一個，用完可整個刪）

腳本假設「工作夾 = `TempImage/<任務>/`」，裡面有 `tools/ ref/ work/ portrait/ out/ backup/`，**在工作夾裡執行**（腳本內的 `../../DipanProj_Main/...` 是相對於工作夾）：

```bash
cd <repo 根目錄>
T=TempImage/<任務名>            # 例 TempImage/moonwatcher_fix
mkdir -p $T/work $T/portrait $T/out $T/backup
cp -r tools/art/art_remake/tools tools/art/art_remake/ref $T/
cd $T
```

⚠ 例外：`sim_intro.py` 用 `DipanProj_Main/Assets/Resources/...`，**要在 repo 根目錄跑**（`python3 TempImage/<任務>/tools/sim_intro.py ...`）。
⚠ 收圖腳本（`grab_raw.sh`、`do_*.sh`）讀的是 `$HOME/mnt/Downloads`、用 GNU `stat -c`——是給 **Claude 橋接器的 Linux 殼**用的；在 Mac 終端機自己跑要把路徑改成 `~/Downloads`、`stat -f %m`。
⚠ `TempImage/` 不進 git：成品裝進遊戲、原始檔放 `ArtSource/`（見 CHARACTER_ART_SPEC §3），工作夾用完可刪。

## 2. 要做什麼 → 用哪支（`art_remake/tools/`，用法見各檔第一行註解）

### 2.1 從 ChatGPT 收圖
| 要做的事 | 工具 |
|---|---|
| 收「最新一張下載」到 `work/<key>_gpt.png`（40 秒內） | `grab_raw.sh <key>` |
| 立繪母版：收圖→去背→貼底→`portrait/<key>_normal_raw.png` | `do_master.sh <key> [grey]` |
| 五張表情差分（下載 -1～-5＝開心/難過/受傷/憤怒/畏懼）：收圖→自動去背→對齊 normal | `do_diff5.sh <key>`（`MAXAGE=秒` 放寬時效） |
| 單張表情重出 | `do_diff.sh <key> <情緒>` |
| 單張立繪（舊流程，含寬版） | `do_portrait.sh <角色> <情緒> [wide]` |
| 角色基礎圖 1024：收圖→去背→對齊→`out/<名>.png`＋新舊並排 | `do_sprite.sh <名稱_vN> [--top Y]` |

### 2.2 去背（依背景選；不確定就用 `autocut.py`）
| 背景／狀況 | 工具 |
|---|---|
| 不確定，自動判斷四角底色 | `autocut.py <in> <out>`（`NOHOLES=1` 關掉挖洞，黑毛角色用） |
| 洋紅 #FF00FF，角色有紫色（寶石、紫火） | `cutout_magenta_keep.py` → 再 `defringe.py` 去紫邊 |
| 洋紅，角色有**發光紫眼／紫光**（色距去背會整個挖掉） | `cutout_magenta_border.py`（只去跟畫布邊相連的背景） |
| 洋紅，灰毛角色（全圖去洋紅溢色） | `cutout_magenta.py` |
| 純黑底（表情差分常用） | `cutout_black.py` ＋ `holes_black.py`（手臂與身體間的黑洞） |
| 純灰底 | `cutout.py` |
| GPT 畫了「假透明棋盤格」 | `cutout_checker.py` |
| 紫寶石被誤挖成小洞 | `fill_small_holes.py <cut> <原圖> <out>` |
| 去背殘留白／淺灰塊 | `edge_white.py [--fix]` |
| 沒連到主體的小碎塊 | `specks.py [--fix]` |

### 2.3 立繪對齊（對話立繪 1024×1536／寬版 1536×1024，規格 CHARACTER_ART_SPEC §2.0）
| 要做的事 | 工具 |
|---|---|
| 標準對齊：頭頂→235、身體中心→470、腳貼底 | `fit_portrait_std.py <cut> <out> [頭頂y]` |
| 依「頭頂→235、腰→800、頭中心→470」放（姿勢特殊、身體比例長、腳可出畫布） | `place_by_lines.py <cut> <out> <頭頂y> <腰y> <頭中心x>` |
| 太寬的角色改寬版 1536×1024（中心 700） | `tall2wide.py <cut> <out> <頭頂y>` |
| 以某點為錨等比縮放＋水平平移 | `portrait_xform.py <in> <out> <scale> <anchor_x> <target_x>` |
| 寬版對齊（舊規格） | `fit_portrait_wide.py` |
| 舊規格（頭頂 180 版，已被 §2.0 取代） | `fit_portrait.py` |
| 表情差分對齊到 normal（IoU 搜縮放＋平移） | `align_diff.py <normal> <diff> <out>` |
| 臉偏離中心線、轉換面板會被裁 → 整組表情水平平移 | 沒有專用腳本：照 BLOODLINE.md「第三版」那段的 Python 片段（`np` 平移＋左緣 32px 漸層） |

### 2.4 角色基礎圖（1024×1024，給 AutoSprite 做序列圖，規格 §3.2）
| 要做的事 | 工具 |
|---|---|
| 對齊：腳底 864、兩腳中點 512、頭頂 176（有耳／角用 `--top 150`） | `fit_sprite.py <cut> <out> [--top Y]` |
| 有翅膀／法杖高過頭：手動指定頭頂與腳中點 | `fit_manual.py <cut> <out> <頭頂y> <腳中點x> [腳底y]` |

### 2.5 安裝進遊戲
| 要做的事 | 工具 |
|---|---|
| 六張立繪覆蓋進 `GameAssets/.../Talk/<路徑>`（自動備份原檔到工作夾 `backup/Talk/`） | `install.sh <key> <系列/角色>`（⚠ 只寫 GameAssets；StreamingAssets 要另外複製或跑 Sync Map Assets） |
| PortraitTable 補列 | `ptable_add.py` ⚠ **過時**：主角血統立繪 2026-11-13 起走腰部以上排法、不查表，別再加 1.15/−139 |

### 2.6 驗收／模擬（改圖或改面板參數後一定跑）
| 要看什麼 | 工具 |
|---|---|
| **血統轉換面板**（BloodlineIntroPanel，參數與程式同步） | `sim_intro.py <out.jpg> <立繪>...`（repo 根目錄跑） |
| **對話框**：主角腰部以上＋NPC 內容框對齊／PortraitTable，同框比較 | `sim_talk2.py <out.jpg> <左圖> <右圖>`（GameAssets 相對路徑；`PT=` 換表測試） |
| 對話框（只有腰部以上版） | `sim_talk.py` |
| 六表情並排檢查（灰底＋235/800 線） | `sheet6.py <key>` |
| 全部已裝立繪總覽 | `overview_portraits.py <out.jpg> [路徑...]` |
| 跟核准立繪並排比畫風／比例 | `cmp_std.py <out.jpg> <立繪>...` |
| 看頭頂實際位置（上半部 50px 格線） | `topgrid.py <out.jpg> <立繪>...` |
| 多張並排、武器與握刀手比對 | `sbs.py`、`weapon_cmp.py` |

### 2.7 序列圖（AutoSprite 產出後）
| 要做的事 | 工具 |
|---|---|
| **量各動作大小與腳底**（dead 比 idle 大／小？腳底差？） | `seq_measure.py <角色資料夾>...` |
| 姿勢差很多時量真實大小（頭部模板比對） | `head_scale.py <角色資料夾> [動作]`（需 opencv） |
| 修 dead 大小與腳底（以腳底縮放、對齊 idle；`--scale 1`＝只平移） | `fix_dead_scale.py [--scale F] <idle 資料夾> <dead 資料夾>`（GA 與 SA 兩份都要跑；PROBLEMS G23／G24） |
| 離線算影子錨點（同 Unity 的 ShadowAnchorMath） | `shadow_anchor.py <SequenceImage 下的角色路徑> [動作...]` → 手動填 `ShadowAnchorTable.csv` |
| 畫上影子看對不對 | `shadow_preview.py <out.jpg> <角色路徑>...` |
| ⚠ 換了序列圖但幀數、畫布都沒變 | Unity「計算影子錨點」不會重算 → 用 `shadow_anchor.py` 或把該列改回 auto 重跑（PROBLEMS G25） |

### 2.8 UI 圖示換風格（`ui_restyle2/tools/`）
`prep.py <相對 UI 的路徑>`（準備來源與比例）→ ChatGPT 產圖 → `grab.sh <key>`（收圖並跑 `post.py`：去灰底、對位、`--apply` 寫回）→ `verify.py`（檢查關鍵區域）→ `overview.py`／`peek.py`（總覽）。一張多格圖示表用 `ui_restyle/tools/sheet.py` 切。`redface.py`：切角牌內面換深緋紅。

### 2.9 其他
| 要做的事 | 工具 |
|---|---|
| 標題畫面邪佛動畫（換頭手幀、綠幕去背、保留紫眼紅岩漿的調色、兩幀 morph） | `buddha_title/tools/`：`compose.py`、`cut_green.py`、`grade.py`、`morph.py`、`grab.sh` |
| 血統鬼影 shader（BloodlineGhost）numpy 預覽 | `bloodline_Gluttonborn/fx/preview.py` |
| 血統血霧 shader（BloodlineMist）numpy 預覽 | `bloodline_Netherborn/fx/preview.py` |

## 3. 常見整套流程（照做）

- **重畫一個血統的對話立繪**：CHATGPT_IMAGE_WORKFLOW.md §8（母版 prompt、差分 prompt、坑）→ `do_master.sh` → `fit_portrait_std.py`（或 `place_by_lines.py`）→ `do_diff5.sh` → `sheet6.py` 看 → `sim_intro.py`＋`sim_talk2.py` 驗 → `install.sh`＋複製到 StreamingAssets。
- **新序列圖放進遊戲後**：`seq_measure.py` 看 dead／walk／attack 跟 idle 差多少 → dead 不對用 `fix_dead_scale.py` → `shadow_anchor.py` 重算影子 → `shadow_preview.py` 看。
- **NPC 立繪換圖**：去背（紫光用 `cutout_magenta_border.py`）→ `sim_talk2.py` 用 `PT=` 試 PortraitTable 數值 → 定案寫進表。
