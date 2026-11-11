# 用 ChatGPT 網頁自動產圖並放進專案（AI 操作手冊）

> 返回 [文件總覽](README.md)
>
> **給未來的 Claude**：作者請你「自己開 ChatGPT 畫圖、抓回來放進專案」時，先讀完這份再動手。
> 本檔記的是 **2026-11-08 UI 改版**實際跑通的流程：一天內重繪約 140 張 UI 與圖示。
>
> 跟其他文件的分工：
> - 舊的 Scenario／Seedream 流程見 [AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md)。本檔是 ChatGPT 版，兩邊共用「純灰底＋本地去背」的觀念。
> - **畫什麼、畫成什麼樣**看美術準則：
>   - UI 與圖示：[art_direction/UI_ICON_GUIDELINE.md](art_direction/UI_ICON_GUIDELINE.md)
>   - 立繪與角色圖：[art_direction/CHARACTER_ART_SPEC.md](art_direction/CHARACTER_ART_SPEC.md)
> - 本檔只管**怎麼操作**。

---

## 0. 一張圖的全流程（先看懂架構）

這條流程跨**兩台機器**，搞混就會卡住：

| 位置 | 能做的事 | 對應工具 |
|---|---|---|
| **雲端容器**（Claude 自己的環境） | 瀏覽器上傳檔案只吃這裡的路徑（`/mnt/user-data/uploads/...`）、看圖（Read） | Chrome 工具、Read |
| **作者的 Mac**（經桌面 App 連線） | 專案檔、`~/Downloads`、Python 處理圖 | `device_bash`、`device_stage_files` |

```
Mac：prep.py 把舊圖墊成 ChatGPT 能出的比例 ─→ device_stage_files 複製到雲端容器
雲端：Chrome file_upload 上傳 → 打 prompt → 等出圖 → 開檢視器 → 點下載鈕
Mac：圖落到 ~/Downloads → grab.sh 收進 work/ → post.py 裁切／縮回原尺寸／去背／驗證
     → 通過就備份原圖、覆蓋進 Resources（不 commit）
```

---

## 1. 開工前要先跟作者要的東西（一次問完，作者可能會離開電腦）

| # | 項目 | 怎麼確認／怎麼做 |
|---|---|---|
| 1 | **這個對話已連到作者的 Mac** | 有 `mcp__remote-devices__*` 工具，且 `device_bash` 呼叫成功。沒有的話請作者在桌面 App 打開這個對話 |
| 2 | **專案資料夾已連線** | `DipanProj` 整個資料夾，在 `~/mnt/DipanProj` |
| 3 | **`~/Downloads` 已授權** | ChatGPT 的圖只能從這裡收。沒授權就 `device_request_folder_access` 要 `~/Downloads` |
| 4 | **Claude in Chrome 已連線**，作者的 Chrome 已登入 ChatGPT（Plus 以上） | 先 `tabs_context_mcp`；開 `chatgpt.com` 看得到左下角帳號；並確認 ChatGPT「燃燈劫」專案的來源檔還在（見 §3） |
| 5 | **Chrome 允許 chatgpt.com 自動下載多個檔案** | 作者手動設定：`chrome://settings/content/automaticDownloads` →「允許」清單加 `[*.]chatgpt.com`。沒設的話第 3～4 張起下載會無聲失敗，而且 AI 點不到網址列的解除圖示 |
| 6 | **作者離開期間 Mac 不能睡** | 系統設定改不睡眠，或終端機 `caffeinate -dims`；Claude App 與 Chrome 保持開著；**不要動 Claude 在用的分頁群組** |
| 7 | **git 規則** | 分支只在作者要求時開；**不 commit**；唯讀查詢用 `git --no-optional-locks`（AGENTS 鐵則） |
| 8 | **範圍與排除清單** | 先列出「會換／不換」，請作者確認（例：文字圖、劇情插畫、動畫幀通常排除） |

不需要的權限：

- **刪檔權限**：一律用備份取代刪除。
- **系統設定**：不碰。

開工時告訴作者三件事：

1. 大概幾張、要多久（每張 2～3 分鐘；圖示表 6 張一次約 3 分鐘）。
2. ChatGPT 有生圖額度，用完要等幾小時。
3. 遇到驗證碼或額度用完會停下來。

---

## 2. 工具（目前放在 `TempImage/ui_restyle/tools/`）

> ⚠ `TempImage/` 在 .gitignore 裡，工具沒進版控。要長期用的話，請作者決定是否搬進 repo（例：`Tools/ai_image/`）。

| 工具 | 用法 | 做什麼 |
|---|---|---|
| `prep.py` | `python3 tools/prep.py <相對 Resources/UI 的路徑> [來源圖相對 Resources 的路徑]` | 挑最接近的比例（1:1／3:2／2:3）、把舊圖置中墊到灰 `#808080` 畫布；寫出 `work/<key>_src.png` ＋ `work/<key>.json`（key＝路徑的 `/` 換成 `__`）。第二個參數用在**新檔案**：來源不在 UI 底下，會先放進 500×500 透明畫布 |
| `grab.sh` | `bash tools/grab.sh <key> [post.py 參數]` | 取 `~/Downloads` 最新的 png。**超過 40 秒就拒收**（`STALE download`），防止誤套上一張。收成 `work/<key>_gpt.png` 後跑 post.py |
| `post.py` | `python3 tools/post.py <key> <圖> [--apply] [--min-iou X] [--fitbbox] [--killgrey] [--trimgrey] [--greyfill]` | 依 json 裁回原尺寸 → 去背 → 算 IoU（透明遮罩重疊率）與 EDGE（邊緣相關）→ 產生 `work/<key>_new.png` 與 `_cmp.png`（左原圖、右新圖）。有 `--apply` 且通過門檻：原圖備份到 `applied_backup/`（已有就不覆蓋）並覆蓋進 Resources |
| `sheet.py` | `python3 tools/sheet.py <圖示表> <欄> <列> <key1,key2,…> [--apply]` | 把一張多格圖示表切開，每格去背、內容置中放進 500×500 |
| `peek.py` | `python3 tools/peek.py <out.png> <key…>` | 把幾張新圖排成一排、深色底，給自己看 |
| `overview.py` | `python3 tools/overview.py <out.png> <相對 UI 路徑…>` | 上排原圖（備份）、下排新圖的對照表 |

`post.py` 參數怎麼選：

| 情況 | 參數 |
|---|---|
| 透明背景的 UI 部件，GPT 會輕微移位或縮放 | `--fitbbox`：把新圖的內容框縮放對齊到原圖的內容框 |
| 封閉的灰色區域沒被去掉（框的內部、環的中間） | `--killgrey`：全圖低飽和的中灰一律透明 |
| 不透明大底圖四周多出灰邊 | `--trimgrey` |
| 不透明格框的角落是灰的 | `--greyfill`：把灰色填成深色 |

`--min-iou` 門檻：

| 類型 | 門檻 |
|---|---|
| 面板 | 0.85～0.92 |
| 疊圖用的圖示 | 0.6～0.8 |
| 一般圖示 | 0.4～0.5 |
| 細線、刻意改形狀 | 0，但**一定要自己看 `_cmp.png`** |

---

## 3. 單張重繪的標準迴圈（已驗證的操作順序）

瀏覽器座標以 1568×759 視窗為準。**一律以最新一張截圖換算座標**（0.3 倍截圖的座標 ÷ 0.3）。

1. **Mac**：`python3 tools/prep.py <路徑>` → `device_stage_files` 把 `work/<key>_src.png` 送進雲端容器。
   - 路徑太深會被拒：staging 只能在連線資料夾下 7 層以內。`work/` 夠淺。
2. **找元件**：`find`「attach photos file input and chat prompt textbox」。
   - **每次上傳前都要重新 find**：開關過圖片檢視器後，舊 ref 會失效（錯誤訊息：`Element is no longer in the document`）。
3. **上傳**：`file_upload` 傳到「附加相片」那個 ref。
4. **一個 browser_batch 跑完以下步驟**：
   1. wait 3～4
   2. 點文字框 ref
   3. `cmd+a`、`Delete`（清掉殘留草稿）
   4. type **單行** prompt
   5. wait 2
   6. `Return`
   7. wait 10 × 6
   8. 在 (900,400) scroll 往下 10
   9. wait 2
   10. 截圖 0.3 倍
   11. 點圖 (772, 290～330)
   12. wait 3
   13. 點下載鈕 (1303,21)
   14. wait 3
   15. `Escape`
   16. wait 2
   17. `find`
5. **Mac**：`sleep 2; bash tools/grab.sh <key> --fitbbox --apply --min-iou 0.85`。
   - 出現 `STALE download`：表示圖還沒出完或沒點到，回到截圖看狀況，補等或補點，不要硬套。
6. 分數邊緣或刻意改形時，`device_stage_files` 把 `_cmp.png` 拿回來用 Read 看過再決定。

**新對話——一律開在 ChatGPT 的「燃燈劫」專案裡**（2026-11-08 作者指定）：

- 專案網址：`https://chatgpt.com/g/g-p-6a2fa318887c819192c646ad555298a8/project`。直接 `navigate` 過去，頁面上的輸入框會顯示「在 燃燈劫 中開始新對話」，在那裡上傳＋打字就是專案內的新對話。
- 為什麼一定要在專案裡：作者已把 `UI_ART_SPEC_for_ChatGPT.md`、`CHARACTER_ART_SPEC.md`、兩份 CHARACTER 準則、`ART_DIRECTION.md` 放進專案的「來源」，並寫了專案指令（要求先讀對應規範再出圖）。**在專案外開的對話讀不到這些**，規範等於沒給。
- 坑：
  - 側邊欄點「燃燈劫」只會展開／收合清單，不會進專案頁。要用網址，或進任一個專案內對話後點標題列左邊的資料夾圖示。
  - 頁面頂端「對話／工作」切換要在**對話**；「工作」模式不是一般生圖對話。
  - 進去後可點「來源」分頁，確認五份 md 都在。
- 每 12～15 張開一次新對話（同樣從專案網址開）。
- 第一則訊息附上**風格錨點圖**：已定稿的總表，放在 `TempImage/ui_restyle/work/anchor_*.png` 或 `*_overview.png`。
- prompt 開頭寫 `IMAGE 1 is our APPROVED … style`。

**圖示用「圖示表」最有效率**：

- 一張 3:2 圖放 3×2 共 6 個物件，用 `sheet.py` 切。
- 18 張圖示只花 3 次額度，而且同批風格更一致。
- 附 6 張舊圖當參考；prompt 依序描述每一個，並要求物件不可跨格。

---

## 4. Prompt 規則

- **只能單行**：換行會讓訊息提前送出。
- 開頭固定寫 `Generate an image now, no questions.`，不然 ChatGPT 會先反問。
- 結尾固定寫 `plain flat grey #808080 background with nothing else; <ratio> output.`，ratio 一定要用 prep json 裡的比例。
  - 實例：開始鈕是 3:2 卻寫成 1:1，出來的圖被拉扁，最後靠 `--fitbbox` 救回。
- 重繪既有圖一律加 `LOCKED: same shape, position, size and silhouette as the attached image`，並說明哪裡是**留給程式放字／放圖的空白區**。
- 範本見兩份美術準則的 prompt 章節。

---

## 5. 踩過的雷與解法

### 瀏覽器操作

| 症狀 | 原因 | 解法 |
|---|---|---|
| 新對話第一次打字沒進去 | 剛上傳完附件，輸入框還沒準備好 | 先 type 一個 `x`，再點一次輸入框、`cmd+a`／`Delete`、重打；送出前 `zoom` 確認文字在框裡 |
| 訊息被提前送出、只送了半句 | prompt 有換行 | 只用單行 |
| 輸入框有上次沒送的殘留文字 | ChatGPT 保留草稿 | 每次打字前 `cmd+a` → `Delete` |
| `Element is no longer in the document` | 開關檢視器後 DOM 重建 | 每次上傳前重新 `find` |
| browser_batch 逾時 | 一批裡的 wait 太多（約 9 個 10 秒以上） | 每批最多 7 個 10 秒 wait；逾時後先截一張圖看狀態 |
| 點圖沒打開檢視器、下載到上一張 | 出圖後版面還在捲動或位置移動 | 點之前先看截圖；grab.sh 的 40 秒門檻會擋住 |
| 出圖很慢，卡在「正在建立圖像」或「潤飾細節中」70% 以上 | 尖峰時段 | 多等 30～60 秒再截圖，不要急著點 |
| 出現「你比較喜歡哪張圖像？」兩張並排 | ChatGPT 的 A/B 測試 | 點較符合的那張「圖像 N 比較好」→ 往下捲 → 點大圖 → 下載鈕會變成**選單**，點「下載圖像」(1208,61)；關檢視器點右上 X (1543,21) |
| `Escape` 關不掉檢視器 | 焦點在別處 | 點右上 X |
| 「非常抱歉，我們創作的圖像可能違反了…暴力…」 | 血紅潑濺、血漬筆觸被判成暴力 | 改寫成深緋紅漆面／寶石的描述；仍被拒就保留原圖，並在報告註明 |
| 「你目前的圖片生成次數已用完。請於晚上 X 再試一次」 | Plus 額度：一下午約 50～60 次生圖 | 停手，用 `send_later` 排一個到時間後的排程任務接著做；先告訴作者剩什麼、何時續做 |
| 瀏覽器工具報「Multiple Chrome browsers are connected… none has been selected」 | 作者有兩個以上 Chrome 連著擴充功能（顯示名只有 Browser 1／2，作者也分不出來） | 作者同意「兩個都找找看」後：逐一 `select_browser` → `tabs_context_mcp`，**看得到之前在用的 ChatGPT 分頁（燃燈劫專案）的那個就是對的**（2026-11-08 是 Browser 1）。作者沒同意前要先問 |
| 檢視器的下載鈕點了沒反應、下載到舊圖 | 視窗大小變了（759→734 高），右上角按鈕位置跟著移 | 開檢視器後先 `zoom` 右上角 (1180,0)-(1568,50) 看下載圖示的實際位置再點；**切圖表（sheet.py）沒有新鮮度檢查**，一定要先確認下載檔 < 40 秒再切（2026-11-08 晚上因此誤把上一張的半邊套進兩個 icon，馬上重下載覆蓋才修正） |
| 附件顯示「上傳失敗」或跳「因網路問題，上傳失敗」 | 偶發網路問題 | 滑鼠移到失敗的縮圖上，點右上角 ✕ 移除，`find` 重新取 ref 再上傳那一張；確認兩張縮圖都正常顯示再打字 |

### 取得檔案

| 症狀 | 原因 | 解法 |
|---|---|---|
| 想用 JS 把圖轉 base64 讀回來，被擋 | 瀏覽器工具封鎖 base64 回傳 | 不要試 |
| 想 curl 圖片網址 | 安全規範禁止 | 不要試 |
| 用 JS `a.click()` 觸發下載 | 沒有使用者手勢，Chrome 會擋，還會把該站的自動下載權限鎖死 | **只用檢視器的原生下載鈕** |
| 第 3～4 張開始下載無聲失敗 | Chrome 擋多檔下載 | 見 §1 第 5 項的白名單 |
| `file_upload` 說路徑不允許 | 它只吃雲端容器路徑，不吃 Mac 路徑 | 先 `device_stage_files` 送進 `/mnt/user-data/uploads/...` |

### 圖本身

| 症狀 | 原因 | 解法 |
|---|---|---|
| 圖被拉扁或拉長 | prompt 比例和 prep 的比例不同 | 照 json 的 ratio 寫；已經出了就用 `--fitbbox` |
| 部件變窄、變寬、位移（頁籤、名牌） | GPT 沒完全鎖住外形 | `--fitbbox` |
| 環、框的內部殘留灰塊 | 封閉區域的灰碰不到四邊，flood fill 去不掉 | `--killgrey`，或原圖該處是透明時自動挖空 |
| 不透明大圖有灰邊、格框角落是灰的 | GPT 把畫布邊留灰 | `--trimgrey`、`--greyfill` |
| 儲物箱底圖多畫了一排格子 | prompt 描述錯（寫了不存在的「五個頁籤」） | 在同一對話請 GPT「移除那一排」再出；prompt 只描述圖上真的有的東西 |
| 名牌換成深色後字看不見 | 程式裡的字是深色（血統名牌是深血紅字） | **畫牌子前先 grep 程式查字色**；淺字配深底、深字配淺底 |
| 讀取條左端看起來凸出框外 | 程式用固定比例對齊軌道，新圖端蓋的邊色和形狀不同 | 這類「會被程式裁切／對齊」的圖，**alpha 直接沿用原圖**、只換顏色，再補一圈淡金亮邊 |
| 血統藥重畫後看起來跟沒換一樣 | 鎖了外形＋設計，只降了光暈 | 圖示類要真的換風格，就**不要鎖外形**（程式的 IconFit 會自動正規化），改用圖示表重畫 |
| 背包武器圖示完全沒換 | 圖示借用了 `Resources/Weapon/` 的投射物圖和特效幀，不在 `Resources/UI/` 底下 | 先查 ItemTable `IconPath` 的實際路徑；**不覆蓋投射物圖**，另出 `UI/Icons/Equipment/icon_*.png` 並改表 |
| 新圖放進 Resources 但 Unity 抓不到 Sprite | 新檔沒有 .meta，或匯入成 Texture | 用同資料夾現有圖的 .meta 當範本（`textureType: 8`、`spriteMode: 1`），換新 GUID 寫一份 |
| 灰色系角色（灰毛狼人、石像）去背會吃掉身體 | 背景是 #808080 灰，與毛色太接近 | prompt 改要「plain flat solid MAGENTA #FF00FF background」，用 `tools/cutout_magenta.py` 去背（含全圖去洋紅溢色，紅布、紅眼不受影響） |
| 立繪去背 | 立繪是 RGB 灰底；人物碰到畫布底邊，腿間、腋下有封閉灰區 | `tools/cutout.py <in> <out>`：從四邊（含底邊）flood fill，加上與底色幾乎相同的封閉灰區 |
| 扛在肩上／穿過頭後的長柄武器擺錯（鍾馗立繪，2026-11-09 作者連退兩次） | GPT 不保證被遮住的物件前後連續；表情差分以母版為準，母版錯就六張全錯 | ⚠ **只驗「刀刃在柄的延長線上」不夠**——第二次就是柄斜著穿過臉、數學上共線卻根本沒扛在肩上。要用文字寫死物理擺法（例：pole lies almost horizontally across his shoulders, rests on top of his right shoulder, passes BEHIND his neck, blade sticks out behind his head on the left），收圖時用**人眼看「柄有沒有靠在肩上、從後頸過去」**。母版最好請作者過目（這次最後是作者自己產母版），差分寫 LOCKED 武器與握武器的手，每張與母版並排比對 |
| 下載檔名是中文 | ChatGPT 依內容自動命名 | 不用管，grab.sh 只看「最新那一張」 |

### Mac 端

| 症狀 | 原因 | 解法 |
|---|---|---|
| `rm` 被拒 | 連線資料夾預設不能刪 | 不刪；靠 `applied_backup/` 備份 |
| git 留下 `.lock` | 一般 git 指令會寫鎖檔 | 唯讀查詢一律 `git --no-optional-locks` |
| 沒有 scipy | Mac 端只有 PIL＋numpy | flood fill 用 BFS 自己寫；距離場用 MinFilter 疊代近似 |

---

## 6. 收尾（每批都要做）

1. **總對照圖**：上排舊、下排新。用 `SendUserFile` 給作者看。
2. **列出三種清單**：沒換的（排除項）、失敗或被拒的、刻意改形狀的。
3. **在 `readme/PROGRESS.md` 最上面記一筆**，寫清楚：
   - 換了哪些；
   - 備份在哪；
   - 怎麼退回（`git checkout -- <路徑>`，或從 `applied_backup/` 複製回去）。
4. **不 commit**，交給作者看過再決定。
5. **改到資料表**（ItemTable 等）：只動指定欄，用 `git --no-optional-locks diff --stat` 確認行數符合預期。
6. **跨數小時的工作**：每完成一段就用 `SendUserMessage` 回報一次進度（作者的桌面版只看得到轉圈）。

---

## 7. 新增血統的產圖流程（2026-11-10 青丘血統跑通；萬魂 2026-11-09 是第一次）

> 作者給「血統概述」（系列名、三階外型、調性）→ AI 先產三張角色圖給作者審 → 作者用 AutoSprite 做序列圖，
> AI 同時產 18 張立繪＋藥瓶 icon → 素材到齊就新增血統（序列圖沒到用暫代）。產圖一律照
> [art_direction/CHARACTER_ART_SPEC.md](art_direction/CHARACTER_ART_SPEC.md)。青丘全程約 24 次額度、一次也沒被擋。

### 7.1 順序與交付

| 步驟 | 內容 | 交付 |
|---|---|---|
| 1 | 第一階角色圖（1024×1024），**先單獨給作者審**（它決定整系列畫風、也最容易跑掉） | 透明圖＋灰底對齊線對照 |
| 2 | 第二、三階角色圖，一起給作者審 | 三階並排總覽 |
| 3 | 每階 normal 母版 → 同一對話裡 5 張表情差分（prompt 寫「Use the FIRST portrait (NORMAL) as the MASTER」，每張都回指第一張，避免差分疊差分走樣） | 立繪不用先審母版，18 張全畫完一起看 |
| 4 | 藥瓶：附既有 6 瓶拼成的總表當風格錨點，產一張成品瓶 | 500×500，內容高度對齊既有瓶（`bloodline_Soulborn` 的 bbox） |
| 5 | 新增血統（資料表＋素材進專案）。⚠ **三階的 `DreamWeaponId` 一定要填**，沒有專屬武器就先借一把 | 見 [BLOODLINE.md](BLOODLINE.md) §7 |

工作資料夾：`TempImage/bloodline_<系列Key>/`（`ref/ work/ out/ portrait/ icon/ tools/`），工具從上一個血統資料夾的 `tools/` 複製。

### 7.2 角色圖要點

- **主角參考一律從 `GameAssets/Main/Characters/SequenceImage/Base/idle/` ＋ `Talk/Base/normal.png` 取**。⚠ 2026-11-10 踩坑：沿用了萬魂工作夾裡的 `ref/protagonist.png`，那其實是**靈根鍊氣期**，不是主角——萬魂的鬼差也是照它畫的。工作夾裡的舊 ref 不可信，每次都回正典資料夾拿。
- 第一階「保留主角臉、但造型已經轉成該血統」：作者偏好**換成血統服裝**（不是在主角現代服上加耳朵尾巴——那版被退件說「跟原本主角沒差太多」），只有臉是主角的；也不要破爛衣服、不要留主角沒有的配件（紅緞帶）。
- 背景一律**洋紅 `#FF00FF`**＋`tools/cutout_magenta.py`：灰褐、深靛、銀白的角色在灰底會被去掉。
- 對齊：`tools/fit_sprite.py <cut> <out> --top Y`，`--top` 是「最高點要落在哪」。有狐耳／帽冠用 150 左右；**尾巴或飾品高過頭頂時要先估比例再算 `--top`**（例：九尾天狐最高點是尾巴尖，用 109，讓頭頂仍約在 176），不然人會被縮小。
- 遮臉角色（兜帽、面具）GPT 常會畫出發光眼睛：小範圍顏色錯直接用程式改（只在臉部框內把青色像素換成周圍陰影色），**不要重產**——重產連構圖都會變。
- 多尾巴：尾巴往左後上方翹、尾尖在腰到頭高之間，prompt 寫死「never hanging down near the feet」；數量在 1024 圖裡不必數得清（作者 2026-11-10 定：看得出很多條即可）。

### 7.3 立繪要點

- 一般角色 1024×1536（`tools/fit_portrait.py`）；作者指定的**寬體型**用 1536×1024（`tools/fit_portrait_wide.py`，2026-11-10 新寫，畫對齊線 27／120／220 與 x 64／700／1440）。寬體型 PortraitTable 先比照夏綠蒂 1.2／−180／OuterToScreen 1。
- 遮臉角色 6 種表情靠肢體：歪頭＋把玩法器（happy）、前傾＋法器前伸＋尾巴豎起（angry）、低頭摀臉（cry）、退半步雙手護胸（fear）、捂肋（hurt）。⚠ **低頭的那張（cry）頂端會掉 60px**，內容框對齊下會比其他張大一點——想避免就在 prompt 加「keep the head at the same height as the master」。
- 往前伸手／伸法器的那張（angry）容易超過右界 880，prompt 加「nothing beyond x=880 on the right」。

### 7.4 瀏覽器操作的新坑（補 §5）

| 症狀 | 解法 |
|---|---|
| 上傳附件後**第一次 browser_batch 打的字一定沒進去**（連 `x` 都沒有），重新 find 拿 ref 也一樣 | 上傳後先跑一個「點輸入框＋打 x」的 batch 當暖身，下一個 batch 才打正式 prompt（偶爾要第三次）；用座標點 (800,254) 比用 ref 穩 |
| 同一對話第 2 張起，新圖出現在畫面下方、點不到 | 送出後等 70 秒 → **scroll 往下 10 兩次** → 新圖固定在 (775,350)（直式）／(805,320)（橫式）→ 點開 → (1293,23) 下載 |
| 下載後馬上送下一張的 prompt | 先跑 `grab_raw.sh` 收檔再送下一張，否則 40 秒新鮮度檢查會收到錯的那張 |

### 7.5 天使血統的新坑（2026-11-11，全程約 30 次額度、零被擋）

| 症狀 | 原因 | 解法 |
|---|---|---|
| `file_upload` 回「上傳需要本人核准，這裡還無法核准」，**立刻被拒、沒有任何視窗**；`browser_batch` 也回「目前不能用」 | 該工作階段的瀏覽器工具權限不同（青丘、萬魂時兩者都能用）；作者看著也沒跳核准視窗，重試無效 | **不要繞**（不用 JS 塞檔）。改成：把參考圖放進 `TempImage/bloodline_<Key>/ref/`，用 AskUserQuestion 請作者**一次**把要用的圖拖進 ChatGPT 輸入框，之後全部在同一個對話裡做（角色圖產出後就在對話裡，立繪直接引用，不必再上傳）。整個血統只要拖兩次：開頭（主角＋畫風錨點）、立繪前（立繪畫風總表＋藥瓶總表一起拖）。動作改成一個一個呼叫，等待用多個 `wait 10` |
| 紫色寶石、紫色火紋、紫布去背後變灰，胸口寶石被挖成洞 | `cutout_magenta.py` 對全圖做「去洋紅溢色」，紫色（r、b 都高於 g）被當成溢色洗掉；偏洋紅的寶石直接被當背景 | 新工具 `tools/cutout_magenta_keep.py`（色距算 alpha；去溢色只做邊緣帶＋低彩度像素）→ `tools/defringe.py`（清邊緣的洋紅描邊）→ 必要時 `tools/fill_small_holes.py <cut> <gpt> <out> 31 --rect x0,y0,x1,y1`（只在軀幹框內、把被身體包住的窄洞補回原圖色；⚠ 不加 `--rect` 會把羽毛間隙補成洋紅）。**角色有紫／洋紅系配色就不要用舊的 `cutout_magenta.py`** |
| 在檢視器開著時打字，文字跑進「編輯這張圖」的輸入框 | `Escape` 有時關不掉檢視器，畫面上的輸入框是檢視器的局部編輯框 | 送出前一定截圖確認是主對話的輸入框；關檢視器一律點右上 X (1543,21) |
| 同一對話裡改圖（「Edit the LUCIFER…change ONLY…」）結果整張重畫：角色、姿勢、角色色都跟著變 | GPT 的「編輯」不是局部修補 | 每次修改後把舊版與新版並排給作者看，列出被動到的地方讓作者選；舊版都留著（`out/*_vN.png`） |
| 六翼／大翼角色在 1024 角色圖裡人物被迫縮小（路西法 −9%、頭頂落在 y≈245） | 翅膀橫向超過 96～928 | 用 `tools/fit_manual.py <cut> <out> <頭頂y> <腳中點x> [腳底y]` 手動指定頭頂與腳中點（排除法杖、翅膀、角）；縮到翅膀進界內為止，差的體型交給 `BodyScale` |
| 作者對第一、二階不滿意，給參考圖指定「這種感覺」 | 純文字描述的天使／神官容易畫成樸素版 | 參考圖當 MOOD REFERENCE 附上，prompt 寫明「只參考設計與感覺，不照它的畫風／背景／光暈」，並把畫風錨定回已核准的 sprite；參考圖性別與設定不符時先問作者 |

天使系列的審稿紀錄（給下一個血統參考作者口味）：
- 第一階：樸素白袍被退，要**華麗**（金飾肩甲、垂鏈寶石、披風）。
- 第二階：要**兜帽＋空洞面罩**的神秘感，巨大雙翼往上舉；男性體型。
- 第三階：身體／臉黑白分半「就是難看的根源」→ 不要；要**邪異、像毒蛇或什麼都不在乎**的神情、臉上紫火紋；盔甲要有亮點（魔獸世界獸人式的誇張肩甲、獸骨、尖刺）；太壯不行、太瘦也不行；露腹；空手。

