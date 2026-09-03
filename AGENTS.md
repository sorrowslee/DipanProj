# 燃燈劫 (Project Dipankara) — AI Agent 指南

本檔是這個 repo 的 **AI 工作契約**：只放「每次動工都要遵守的常駐規則」與指路。
細節一律住在 `readme/` 的主題文件裡，**這裡不貼內文、只指過去**——文件架構與維護規則見
[readme/DOCS_GUIDE.md](readme/DOCS_GUIDE.md)。動工前先把本檔看完。

---

## 專案是什麼

《**燃燈劫**》（英文 Burning Lamp: Rebirth of Ruin）：Unity 2D 俯視角動作恐怖遊戲（目標上架 Steam；
Built-in Render Pipeline、Unity 2022.3）。核心迴圈與底層架構已完成，玩法高度 **CSV 資料驅動**。

倉庫內有三塊：

| 目錄 | 角色 |
|---|---|
| `DipanProj_Main/` | 主遊戲 Unity 專案 |
| `DipanProj_MapEditor/` | 地圖編輯器 Unity 專案（產 `.dipanmap`，素材經 Sync Map Assets 同步） |
| `BallisticsSystem/` | 彈道模組 `Sorrows.Ballistics`（與主遊戲解耦） |

模組邊界、Layer／Tag／碰撞矩陣、美術資源架構：[readme/ARCHITECTURE.md](readme/ARCHITECTURE.md)。

---

## 常駐鐵則

- **CSV 資料驅動優先**：大多數玩法擴充是「在 CSV 加一列＋既有系統參數」就能做到；動程式前先確認不能用資料解決。CSV 表統一放 `Assets/Data/`＋provider 載入。
- **玩家可見字串一律走 `Language.GetText(id)`**（`LanguageTable.csv`），不准硬寫；「畫成圖的字」放 `Resources/UI/Texts/<語言>/`（同名不同資料夾）。詳見 [readme/LOCALIZATION.md](readme/LOCALIZATION.md)。
- **左鍵＝搬移／裝備／綁定，永遠不消耗；右鍵＝使用**——右鍵是全遊戲唯一會消耗道具的滑鼠操作。「使用道具」唯一入口是 `Inventory/ItemUse.cs`（見 PROBLEMS **D17**）。
- **git 由作者自己掌控**：AI 不主動 commit／開分支／merge／push，除非作者該次明確要求（講一次做一次）。唯讀查詢一律加 `--no-optional-locks`（如 `git --no-optional-locks status --short`）——Cowork 橋接器刪不掉 `index.lock`，留下會擋住作者（見 PROBLEMS **I** 段）。
- **固定 sortingOrder 的排序層配置表寫在 `Assets/Scripts/Map/MapDepthSort.cs` 檔頭**；地圖編輯器 `ObjectView.cs` 有鏡像——**改一邊要兩邊一起改**。
- **溝通、註解、文件一律繁體中文**；跟作者提 Unity Inspector 上的欄位時要給**英文標籤**（Inspector 標籤是自動生成的英文，給中文他找不到）。
- 做完事情**一定要記錄**（見下方「記錄工作流」）——這個專案的文件是接手的命脈。

---

## 動工前必讀路由表（血淚換來的，跳過會重踩）

| 要動的東西 | 先讀 |
|---|---|
| 任何 UI 半透明／alpha 值 | PROBLEMS **E11**（Linear 色彩空間：疊色比直覺重/淡一倍，附診斷公式） |
| 疊任何發光圖層／光暈／改佛光 | PROBLEMS **E12/E13** ＋ [readme/FALLEN_BUDDHA_LIGHT.md](readme/FALLEN_BUDDHA_LIGHT.md)（兩個發光層疊同位置是零和；`_Intensity` ≠ 實際亮度） |
| 掛在玩家身上的特效（定位／縮放） | PROBLEMS **E14**——用 `PlayerController.FeetWorldPos` / `BodyCenterWorldPos` / `VisibleBodyHeight`，別用 `transform.position` 當身體中心、別用 `SpriteRenderer.bounds` 當可見身體 |
| 跨數秒的演出／輸入鎖 | PROBLEMS **D13/D14**（輸入鎖要用具名 `SetExternalHold(owner,…)`；吃 `Time.deltaTime` 的演出會被任何 `PausesGame` 面板凍住） |
| 地上物擋路／可走層／「看起來能走卻走不過去」 | PROBLEMS **B9** ＋ [readme/MAP_LOADER_SETUP.md](readme/MAP_LOADER_SETUP.md)——**擋路碰撞與可走層是兩份獨立的真相**，塗可走層對地上物零作用 |
| 角色影子對不準／加新角色或換序列圖後影子歪 | [readme/SHADOW.md](readme/SHADOW.md)〈定位：影子錨點表〉——演算法已定版，**單一角色不對一律改表**（〈手動調整影子〉手冊）、不再動演算法；**別再往「程式自動偵測腳」投工**（PROBLEMS **E28**） |
| 畫面軟／角色像貼紙／產或換整張背景圖 | PROBLEMS **E29** ＋ [readme/PERF_QUALITY_AUDIT.md](readme/PERF_QUALITY_AUDIT.md) §4.1（背景每格 ≥128px；過渡期 `CharacterMipBias` 換完背景要關）；角色暗部融不進場景 → [readme/ATMOSPHERE.md](readme/ATMOSPHERE.md)〈角色環境融合〉（自動量畫面、只有一顆 `TargetDarkRatio`，別回頭做每氛圍一組參數） |
| 「玩家碰到了沒」的位置判定 | PROBLEMS **B13**——判定對齊碰撞（`transform.position`），特效對齊視覺（腳底）；診斷用碰撞疊層（遊戲中 **P → C**） |
| 填／改 RecipeTable、加武器模式或欄位、動能力珠 | **做武器優先用 [readme/WEAPON_WORKBENCH.md](readme/WEAPON_WORKBENCH.md)（Unity 內的武器工坊，Play 中立刻射出去看）**；欄位意義見 [readme/RECIPE_DESCRIBE.md](readme/RECIPE_DESCRIBE.md)（一列一種 `Mode`、模式 × 欄位矩陣）＋ `Assets/Scripts/Weapon/WeaponModeSpec.cs`（單一真相：加欄／加模式只改它，視窗自動跟上）；珠子有效性見 [readme/GEM_SOCKET.md](readme/GEM_SOCKET.md) |
| 武器／裝備／背包／掉落／存檔 | [readme/GEM_SOCKET.md](readme/GEM_SOCKET.md)（表格只是模板、物品實例、能力容器）＋ [readme/GEM_CATALOG.md](readme/GEM_CATALOG.md)（每顆珠子的功用與範例，改珠子數值或加珠子要同步更新它）＋ [readme/INVENTORY.md](readme/INVENTORY.md) |
| 放/改 NPC、NPC 對話/開介面、未來護送 | [readme/NPC_SYSTEM.md](readme/NPC_SYSTEM.md)（NpcTable 分表、圖沿用 Monsters/SequenceImage 角色圖庫；編輯器與主遊戲的 NpcInstance 是鏡像） |
| 陣營/多方互打劇本、動「誰能傷誰」 | [readme/FACTION.md](readme/FACTION.md)（規則單一真相＝`FactionRelations.cs`：敵對/傷害乘數/切層都只改它；擺劇本照 §3 一條龍） |
| 血統／角色外型／立繪／體型 | [readme/BLOODLINE.md](readme/BLOODLINE.md)（表A 唯一真相；`BodyScale` 純視覺；五屬性只存不套用） |
| 發光／亮度／光圈／環境亮度 | [readme/ATMOSPHERE.md](readme/ATMOSPHERE.md)＋[readme/SCENE_EFFECT.md](readme/SCENE_EFFECT.md)（同框 12 盞上限寫死在兩處） |
| 產任何圖片素材 | [readme/AI_IMAGE_GEN_GUIDE.md](readme/AI_IMAGE_GEN_GUIDE.md) |
| 素材風格／場景調色／畫面觀感（「組起來不漂亮」） | [readme/ART_DIRECTION.md](readme/ART_DIRECTION.md)（畫面經營六紀律＋繪圖 AI 檢核表；產圖操作仍看 AI_IMAGE_GEN_GUIDE） |
| 加／改「直接進關卡」測試選單、想換某關卡從哪張圖開始 | [readme/TITLE_AND_SAVE_UI.md](readme/TITLE_AND_SAVE_UI.md)〈測試快捷〉——**關卡入口一律用 module 型 `Set("<module>")`，進哪張由 MapsTable `IsLevelStart` 決定（改 CSV 即可、不用動程式）**；`map:<id>` 只給非模組首圖。踩過見 PROBLEMS **B15** |
| 第一次接觸專案／遇到怪問題 | [readme/PROBLEMS.md](readme/PROBLEMS.md)（檔頭有分類索引；踩過的坑幾乎都在裡面） |

---

## 文件地圖與記錄工作流

- **文件總覽（主題文件地圖）**：[readme/README.md](readme/README.md)。依任務挑相關的讀，不必全讀。
- **做完事情記錄三件套**（格式與封存規則見 [readme/DOCS_GUIDE.md](readme/DOCS_GUIDE.md)）：
  - 完成了什麼 → `readme/PROGRESS.md`（**倒序、最新加最上面**；重點記「為什麼難、試過什麼不行、通則」）。
  - 踩到新坑 → `readme/PROBLEMS.md`（症狀→原因→解法；編號接該分類最大號、**永不重編**）。
  - 半成品／暫缺 → `readme/TODO.md`（**新節加檔尾**）。
  - 改動讓某主題文件過期 → **同一次改動裡把它改對**，不要留給下一個人。
- `readme/archive/` 是封存區：**非當前真相、不作開發依據**，只用來查歷史脈絡。

---

## ChatGPT / Codex 專用規則

- 本節規則只適用於 ChatGPT / Codex，不適用於其他 AI agent；其他 agent 依使用者另外給予的規則行事。
- 使用者在對話中交代給 ChatGPT / Codex 的後續規則，除非使用者明確表示為全專案或全 agent 共用，否則一律只約束 ChatGPT / Codex。
- ChatGPT / Codex 後續進行任何需要修改專案的任務前，統一使用 `feat/gpt` 分支。
- ChatGPT / Codex 開始工作前先確認本地是否已有 `feat/gpt`；若存在，先刪除再重新建立，不沿用上一次任務的分支內容。
- 新的 `feat/gpt` 必須以最新的 `develop` 為起點：先更新 `develop`，再從更新後的 `develop` 建立 `feat/gpt`。
- 刪除／重建分支與更新 `develop` 前，先確認工作樹狀態，避免覆蓋或遺失尚未提交的使用者修改。
- 純閱讀、說明、分析或不修改專案的任務，不必切換或重建分支。
