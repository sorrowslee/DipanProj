# 劇情系統 (Drama：觸發點 ＋ 劇情檢視介面)

> 返回 [文件總覽](README.md)｜靠近按 F 的互動共用機制見 [INTERACTION.md](INTERACTION.md)｜UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)｜資料表載入慣例見 [INVENTORY.md](INVENTORY.md)

玩家走到地圖上的「劇情觸發點」，靠近按 **F** 跳出一個劇情檢視介面（一張大圖 + 一段文字）。觸發互動本身（星星標示、靠近按 F、消耗）完全複用 [INTERACTION.md](INTERACTION.md) 的 `InteractionManager`——本檔只談**劇情專屬**的部分：資料表、檢視介面。

---

## 編輯器：劇情觸發點 trigger

預設 trigger 多了一種 `drama`「劇情觸發點」（紫色 `#AA66FF`），參數 **`dramaId`(Int)**。在編輯器畫一塊 `drama` 區域、填 `dramaId`，遊戲端就會在該處放**紫色星星**、靠近顯示「按 F 鍵」、按 F 開對應劇情。

- 定義在 `DipanProj_MapEditor` 的 `triggerTypes.json` ＋ `TriggerType.cs` 的 `Defaults()`（兩處同步）。
- runtime 由 `InteractionManager` 讀 `region.GetInt("dramaId")`，不需要 triggerTypes.json。
- **觸發方式依該 dramaId 在 DramaTable 的 `Type` 而定**：Type 1＝靠近按 F（放紫色星星）；Type 2（頭像對話）＝**碰到自動觸發、且不放星星**（純隱形觸發點，見下方「觸發分支與觸發方式」）。編輯器端不分型別，都只填 `dramaId`。
- 觸發後一次性消耗（當次停留不再觸發）；✅ **同一趟關卡內跨換圖記憶**（離開房間再回來不會重播），完整離開關卡才重置——與拾取點同模型，見 [INTERACTION.md](INTERACTION.md)、[RUN_PROGRESS.md](RUN_PROGRESS.md)。

---

## 資料表 `DramaTable.csv`

位置 `Assets/Data/DramaTable.csv`（與所有資料表同位置）。載入方式比照 `ItemTable`：**把 CSV 拖進場景上 `DramaTableProvider` 元件的 `Drama CSV` 欄**（建議掛在 GameManagers）。`DramaDatabase`（懶漢快取）載入時 `FindObjectOfType<DramaTableProvider>()` 取用、沒拖到退回讀 Resources 並印錯誤指引。

| 欄位 | 說明 |
|---|---|
| `ID` | 劇情唯一編號（編輯器 `dramaId` 指這個） |
| `ImagePath` | **catalog id**：相對 `GameAssets/` 的路徑、不含副檔名（例 `Modules/RedBridalGown/Drama/drama_sample`）。留空 = 只有文字。**僅 Type=1 使用** |
| `Text` | 內文。欄位內含逗號請用 `"..."` 包覆；要換行就寫 `\n`（會轉成真換行）。**僅 Type=1 使用** |
| `Type` | 劇情類型：**1 = 大圖+文字**（DramaPanel，預設）、**2 = 頭像對話**（見下節）。留空 / 無效 = 1（向下相容舊表） |
| `TalkGroup` | **僅 Type=2 使用**：對話群組編號，連到下節「頭像對話」的 `DramaTalkTable.csv` 群組編號。Type=1 時填什麼都不影響 |

> 之後要加標題、多頁、語音等欄位，在表尾加欄即可（解析器忽略未知欄、缺欄給預設）。
> **編輯器不必動**：trigger 仍只填既有的 `dramaId`，type 與對話群組都由 DramaTable 那一列決定（用 dramaId 對照）。

---

## 頭像對話（Type 2）＋ DramaTalkTable.csv

當 DramaTable 某列 `Type=2` 時，觸發後**不開大圖面板**，改去 `DramaTalkTable.csv` 撈該列 `TalkGroup` 指定的群組，把整串對話依序播放（左/右頭像 + 文字）。

### 資料表 `DramaTalkTable.csv`

位置 `Assets/Data/DramaTalkTable.csv`（與其他資料表同位置）。載入方式比照 DramaTable：**把 CSV 拖進場景上 `DramaTalkTableProvider` 元件的 `Talk CSV` 欄**（建議掛在 GameManagers）。`DramaTalkDatabase`（懶漢快取）載入時 `FindObjectOfType<DramaTalkTableProvider>()` 取用、沒拖到退回讀 Resources 並印錯誤指引。

| 欄位 | 說明 |
|---|---|
| `ID` | **流水號**（唯一）。同群組內依此**由小到大**依序播放 |
| `Group` | **群組編號**：同一個群組 = 一串對話。DramaTable Type=2 的 `TalkGroup` 指向這裡 |
| `Name` | **說話人姓名**：顯示在「聚光側」姓名牌匾上（資料層只保存字串） |
| `LeftAvatarPath` | **左側立繪**路徑（兩種寫法見下）。留空 = 左側不顯示立繪 |
| `RightAvatarPath` | **右側立繪**路徑（兩種寫法見下）。留空 = 右側不顯示立繪 |
| `SpotlightSide` | **聚光側＝現在誰在說話**：**1 = 左、2 = 右**（其餘 / 留空視為 1）。聚光側立繪正常亮、另一側**壓暗**（保留原色相），姓名牌匾也擺聚光側 |
| `Text` | 對話內容。含逗號用 `"..."` 包覆；`\n` 轉換行 |
| `LeftScale`/`LeftOffsetX`/`LeftOffsetY` | **左立繪微調**（選填，空=1/0/0）：縮放乘在標準立繪高度上；位移為畫面單位、+X 往右、+Y 往上 |
| `RightScale`/`RightOffsetX`/`RightOffsetY` | **右立繪微調**（同上，右側獨立）。註：立繪寬度已改依**圖片實際比例**自動算，非主角比例的 NPC 圖不會再被壓進固定框 |

**立繪路徑兩種寫法**（`LeftAvatarPath` / `RightAvatarPath` 皆可）：

1. **catalog id**：相對 `GameAssets/` 的路徑、不含副檔名（例 `Modules/RedBridalGown/Talk/redBridalGown`）——每關專屬立繪，走地圖素材管線（同劇情大圖）。
2. **`Actor_<情緒>`**：主角情緒立繪（例 `Actor_Angry`）——依**目前血統**載 `GameAssets/Main/Characters/Talk/<血統>/<情緒>.png`（情緒大小寫不拘）。目前情緒有 `normal`/`happy`/`angry`/`cry`/`fear`/`proud`/`speechless`（未來可增）；血統資料夾依 `BloodlineTable.SpriteFolder` 自動切換，目前有 `Base`／殭屍三階（`Jiangshi`/`Maojiang`/`Hanba`）／夜裔三階（`Nightborn`/`Crimson Count`/`Cain`）。找不到圖 = 那側不顯示（方便人工抓 bug）。

範例（群組 1 = 單側立繪；群組 2 = 左右對話、聚光側交替）：

```
ID,Group,Name,LeftAvatarPath,RightAvatarPath,SpotlightSide,Text
1,1,老者,Modules/RedBridalGown/Talk/avatar_elder,,1,你終於來了
2,1,惡鬼,,Modules/RedBridalGown/Talk/avatar_demon,2,沒錯，納命來!!
5,2,新娘,Modules/RedBridalGown/Talk/redBridalGown,Actor_Normal,1,你終於來了，大俠。
6,2,大俠,Modules/RedBridalGown/Talk/redBridalGown,Actor_Angry,2,是誰在背後操控這一切?快說!
```

> 群組 2：左固定新娘 NPC、右固定主角（`Actor_*` 依血統換情緒）；`SpotlightSide` 切換誰被打亮、誰被壓暗，營造左右對話。

> **主角情緒立繪要進 catalog 才載得到**：`Main/Characters/Talk/<血統>/*.png` 已加進三處同步產生器的掃描（`MapIO.cs`、`MapAssetSyncTool.cs`、`Tools/sync_map_assets.sh`），放好圖後跑 `Project Tools → Sync Map Assets` 即收進 catalog ＋ StreamingAssets（打包後也讀得到）。

### 觸發分支與觸發方式（runtime）

`InteractionManager` 在建立劇情點時就用 `dramaId` 查 `DramaDatabase` 的 `Type`，決定**觸發方式**與**內容**：

| Type | 觸發方式 | 星星標示 | 內容 |
|---|---|---|---|
| 1（大圖+文字） | **靠近按 F**（顯示「按 F 鍵」提示） | 有（紫星） | `DramaPanel.Show(dramaId)` |
| 2（頭像對話） | **碰到自動觸發**（踏進區域 `dramaTouchRadius`＝0.6 內就播，不需按鍵、不顯示提示） | **無**（純隱形觸發點） | `DramaTalkController.Play(TalkGroup)` → TalkPanel |

- 自動觸發點不參與「按 F」的最近目標選取（`InteractPoint.autoTrigger`）。觸發後一律 `ConsumePoint`（星星移除、當次停留不再觸發；並記進 `RunProgress.consumedTriggers`，本趟關卡跨換圖不再出現）。
- `dramaTouchRadius` 在 `InteractionManager` Inspector 可調。Type 1（或查不到資料）維持原本的「按 F」。

### 對話介面 `TalkPanel`（✅ 已接上）

`DramaTalkController.Play(group)` 撈出該群組對話（已排序）→ **依目前血統解析左右立繪 sprite**（`DramaTalkDatabase.ResolveGroupAvatars`，把 `Actor_<情緒>` 換成主角當前血統的情緒圖）→ 開啟 `TalkPanel` 播放：底部一個**對話框**（`DramaPanelBG`）+ **姓名牌匾**（`DramaPanelNameBG`，擺在**聚光側**＝說話者那側）+ 對話文字；**左、右各可有一個立繪同時出現**，非聚光側的立繪**壓暗**（保留原色相、純調暗，背光感）；**點畫面任意處 / 空白鍵 / Enter 換下一句**，最後一句後關閉。模態、暫停遊戲、ESC 可關、**半透明黑遮罩**（`ShowBackdrop=true`，UIManager 共用遮罩鋪在對話框+立繪後方把場景壓暗）。做法同 SettingsPanel（整張背板 + 量測座標，座標常數在 `TalkPanel.cs` 上方、實機可微調）。

- 檔案：`Assets/Scripts/UI/Panels/TalkPanel.cs`。對話框/牌匾素材：`Resources/UI/DramaPanel/DramaPanelBG.png`、`DramaPanelNameBG.png`。
- 壓暗顏色常數 `DimmedColor`（預設灰 0.42，越小越暗）在 `TalkPanel.cs` 上方可調。
- **立繪走地圖素材管線**（與劇情大圖同套，每關專屬、非共用 Resources）；`Actor_<情緒>` 則走 `Main/Characters/Talk/<血統>/`（共用、隨主角血統）：
  - 每關專屬立繪放 `GameAssets/Modules/<module>/Talk/`，`LeftAvatarPath` / `RightAvatarPath` 填 **catalog id**（相對 GameAssets、不含副檔名，例 `Modules/RedBridalGown/Talk/redBridalGown`）。
  - 跑 `Project Tools → Sync Map Assets` 收進 catalog ＋ StreamingAssets。`DramaTalkDatabase.ResolvePortrait` 用 `CatalogLoader` + `MapSpriteLoader` 把每個立繪路徑載成 Sprite（同 `DramaDatabase.ResolveImages`；依解析後 catalog id 快取，同一張圖多句共用只載一次）。
  - **`Talk` 是同步分類白名單的一員**（與 `Environment/Tiles/Background/Drama` 並列）；**主角情緒立繪 `Characters/Talk/<血統>/*.png` 另由三處同步工具的 `Characters/Talk` 掃描收進 catalog**（id 例 `Main/Characters/Talk/Base/angry`，category=`Talk`）。**加新素材分類 / 掃描時三處同步產生器要一起改**（`MapAssetSyncTool.cs`、`MapIO.cs`、`Tools/sync_map_assets.sh`），否則會像 [PROBLEMS.md](PROBLEMS.md) C3 那樣「放了圖卻載不到」。
  - TalkPanel 把左右立繪當**站姿**擺在對話框**後方**（z-order：對話框蓋在立繪上、立繪下半身沉入框後）、左立繪錨左下角、右立繪錨右下角。位置/大小常數在 `TalkPanel.cs` 上方：`AvatarHeight`（大小）、`AvatarOverlap`（**越大越往下＝被對話框蓋住越多、露出越少**）、`AvatarSideMargin`（離左/右邊）。某側沒立繪（載不到 / 留空）時那側自動隱藏。
  - **右側立繪一律水平翻轉**（`localScale.x=-1`，原地鏡像不位移）：因為立繪原圖臉朝右，放右邊要翻成朝左才面向畫面中央、與左側對望。所以 `RightAvatarPath` 的圖**直接放正常朝右的原圖即可**，不必自己先翻好。

### 圖放哪（重要）：每關專屬、走地圖素材管線，不放共用 Resources

劇情圖是**單一關卡才用的圖、不是共用資源**，所以放在該關卡的素材包，比照地圖素材（牆/地磚/家具）的方式載入，而不是放共用的 `Resources/`：

- **放這裡**：`Assets/GameAssets/Modules/<module>/Drama/<檔名>.png`（例 `…/Modules/RedBridalGown/Drama/drama_sample.png`）。
- **同步**：跑 `Project Tools → Sync Map Assets`（或 `Tools/sync_map_assets.sh`）會把 `Drama/` 的 PNG 收進 `catalog.json` ＋ 複製進 `StreamingAssets/MapAssets/`（可打包）。`Drama` 是新加進同步分類的（與 `Environment/Tiles/Background` 並列）。
- **runtime 載入**：`DramaDatabase` 不再用 `Resources.Load`，改用地圖素材管線——`CatalogLoader.Load` 取 catalog、`MapSpriteLoader.GetWholeSprite` 依 `ImagePath`（= catalog id）載圖（PPU 256、Point 濾鏡，像素風一致；UI Image 靠 preserveAspect 縮放，PPU 不影響觀感）。
- **為什麼不放 Resources**：`Resources/` 會被 Unity **無條件打包進每個 build**（共用、永遠載入），與「每關專屬、之後可隨關卡動態下載」的設計相反（見 [ARCHITECTURE.md](ARCHITECTURE.md) 的美術資源架構）。GameAssets/Modules 才是「場景專屬包」。
- **三處同步產生器**（加 `Drama` 分類時要一起改，否則只改一處會像 [PROBLEMS.md](PROBLEMS.md) C1 那樣踩雷）：`Assets/Editor/MapAssetSyncTool.cs`、`Tools/sync_map_assets.sh`、`Assets/Scripts/Map/MapIO.cs`(`BuildFromGameAssets` 的編輯器後備)。

> icon（背包道具）仍走 `Resources/UI/Icons`——那是真正的共用資源，不變。只有「每關專屬的劇情圖」走 GameAssets。

---

## 執行單元

| 檔案 | 角色 |
|---|---|
| `Assets/Scripts/Drama/DramaData.cs` | 一段劇情的資料（ID / ImagePath / Text / 載好的 Image sprite） |
| `Assets/Scripts/Drama/DramaDatabase.cs` | 表載入（`LoadFromText`，懶漢 `Instance`）+ `Get(id)`；圖從 Resources 預載 |
| `Assets/Scripts/Drama/DramaTableProvider.cs` | 場景小元件，持有 `DramaTable.csv` 的 TextAsset 參照（同 `ItemTableProvider`） |
| `Assets/Scripts/UI/Panels/DramaPanel.cs` | 劇情檢視介面（Type=1，見下） |
| `Assets/Scripts/Drama/DramaTalkData.cs` | 一句頭像對話（Id / Group / Name / LeftAvatarPath / RightAvatarPath / SpotlightSide / Text + 載好的左右 Sprite） |
| `Assets/Scripts/Drama/DramaTalkDatabase.cs` | 頭像對話表載入（懶漢 `Instance`）；依群組分組、組內依流水號排序；`GetGroup(group)` |
| `Assets/Scripts/Drama/DramaTalkTableProvider.cs` | 場景小元件，持有 `DramaTalkTable.csv` 的 TextAsset 參照（同 `DramaTableProvider`） |
| `Assets/Scripts/Drama/DramaTalkController.cs` | 頭像對話播放入口 `Play(group)`：開啟 TalkPanel（無 UI 環境才退回 Debug.Log） |
| `Assets/Scripts/UI/Panels/TalkPanel.cs` | 頭像對話面板（對話框 + 姓名牌匾 + 左右雙立繪〔非聚光側壓暗〕 + 文字 + 換頁） |

---

## 劇情檢視介面 `DramaPanel`

繼承 `UIPanel`（見 [UI_SYSTEM.md](UI_SYSTEM.md)），**模態**：

| 特性 | 值 | 說明 |
|---|---|---|
| `Layer` | `Window` | 主視窗層 |
| `PausesGame` | `true` | 開劇情時暫停遊戲（`Time.timeScale=0`） |
| `BlocksGameplayInput` | `true` | 擋住移動/攻擊 |
| `ShowBackdrop` | `true` | 半透明黑遮罩（UIManager 共用遮罩，鋪在視窗後） |
| `CloseOnEscape` | `true` | ESC 關閉 |

- **版面**：置中大面板（參考解析度 1920×1080 下約 1280×940），垂直排列＝**大圖在上（保持比例、盡量大）＋ 文字在下 ＋ 底部小提示「按 ESC 或點畫面任意處關閉」**。
- **關閉**：ESC（UIManager 堆疊最上層）或**點畫面任意處**（整片透明關閉鈕鋪滿、圖/文 `raycastTarget=false` 讓點擊穿到關閉鈕）。
- **沒圖**（`ImagePath` 留空或圖找不到）→ 只顯示文字（圖元件停用）。
- 開法：`DramaPanel.Show(dramaId)`（靜態）——查 `DramaDatabase`，找不到資料就印 Warning、不開。由 `InteractionManager` 在玩家按 F 時呼叫。

---

## 怎麼用（一條龍）

1. **放劇情圖**：把 PNG 放進 `Assets/GameAssets/Modules/<module>/Drama/`，在 `DramaTable.csv` 的 `ImagePath` 填 catalog id（例 `Modules/RedBridalGown/Drama/<檔名>`）。
2. **編輯器**：放「劇情觸發點」trigger、填 `dramaId`。
3. **同步**：`Project Tools → Sync Map Assets`（把劇情圖收進 catalog/StreamingAssets，順便拉地圖）。
4. **Unity 一次性**：GameManagers 上 Add Component → `DramaTableProvider`，把 `Assets/Data/DramaTable.csv` 拖進 `Drama CSV` 欄。**若有用到 Type=2 頭像對話**：再 Add Component → `DramaTalkTableProvider`，把 `Assets/Data/DramaTalkTable.csv` 拖進 `Talk CSV` 欄。
5. **Play**：走近劇情點看到紫星＋「按 F 鍵」→ 按 F。Type=1 跳大圖+文字面板；Type=2 跳底部對話框（姓名牌匾+左右雙立繪〔非聚光側壓暗〕+文字，點畫面/空白鍵換頁）。

### 頭像對話（Type=2）一條龍
1. **DramaTalkTable.csv**：填好群組（`Group`）、每句的 `ID`（流水號決定順序）、`Name`（說話人姓名）、`LeftAvatarPath` / `RightAvatarPath`（catalog id 或 `Actor_<情緒>`，留空＝那側不顯示）、`SpotlightSide`（1聚光左/2聚光右）、`Text`。
2. **DramaTable.csv**：某列設 `Type=2`、`TalkGroup` 填要播的群組編號（`ImagePath`/`Text` 可留空）。
3. **編輯器**：drama trigger 的 `dramaId` 指向上面那列（與 Type=1 完全一樣，編輯器不必改）。
4. **Unity**：掛 `DramaTalkTableProvider` 並拖入 CSV（見上）。
5. **Play**：走近按 F → 跳出底部對話框，依流水號逐句播放（點畫面 / 空白鍵換下一句，最後一句後關閉）。

---

## 防連點（對話不會被猛按跳掉）

> **問題**：對話時猛按左鍵／空白鍵，會一次跳掉好幾句；有時立繪都還沒顯示出來就被跳過。

**作法**：把「前進一次 / 關閉一次」節流成 **每 0.5 秒最多一次**，而且**面板剛開啟時也先擋一次冷卻**——後者才是「立繪還沒出來就被跳掉」的解方（否則上一句的連點慣性會直接吃掉新開的那一句）。

**工具在基底**：`UIPanel` 提供兩支 protected 方法（見 `Assets/Scripts/UI/UIPanel.cs`）：

| 成員 | 作用 |
|---|---|
| `UIPanel.InputCooldown` | 預設冷卻秒數，目前 **0.5** |
| `BlockInputFor(seconds)` | 接下來 N 秒內的 `TryConsumeInput` 一律當作沒按。在 `OnOpen()` 呼叫 |
| `TryConsumeInput(cooldown = InputCooldown)` | 冷卻中回 `false`（這次連點忽略），否則回 `true` 並重新起算 |

**⚠️ 這是 opt-in 的**：基底不會自動套用，只有主動呼叫 `TryConsumeInput` 的面板才有節流——否則背包、設定那種需要連續操作的面板會變得很鈍。

**⚠️ 一律用 `Time.unscaledTime`**：對話面板 `PausesGame = true`，`Time.time` 在暫停時根本不會前進，用它會永遠卡在冷卻裡。

**目前套用在兩處**：

- **`TalkPanel`（Type 2 頭像對話）**：節流放在 `Next()` 內一處——鍵盤（空白／Enter）與整片點擊鈕都經過它，兩個入口一次涵蓋。
- **`DramaPanel`（Type 1 大圖）**：節流放在整片關閉鈕的 callback。

**ESC 只在開發階段有效**：兩個面板的 `CloseOnEscape` 已改成 `DevSkip.Allowed`（＝編輯器內或 Development Build 才 true）。

- **正式打包：按 ESC 對話完全沒反應**，玩家不能跳過劇情。也不會誤開設定面板——`UIManager` 的 ESC 是「有視窗且該視窗允許才關」，不允許就什麼都不做（不會 fall through 到「開根面板」那個分支）。
- **不會卡死**：對話播完 `ShowCurrent()` 會自動 `Close`；`DramaPanel` 的整片點擊關閉鈕照常可用。
- 開發階段的 ESC 走 `UIManager`、不經過防連點節流。

**沒有套用**：序章開場漫畫（`IntroComicController`）的空白鍵翻頁是獨立實作、不走 `UIPanel`，維持原樣。之後若也想要，照同一個模式加即可。

**要調整節奏**就改 `UIPanel.InputCooldown`（全域），或在個別面板呼叫 `TryConsumeInput(自訂秒數)`。

---

## 待辦（之後可加）

- 多頁劇情（連續對話/翻頁）、標題列、角色立繪、打字機逐字顯示、語音/音效。
- 永久記錄（看過不再觸發）——接 Phase 2 與存檔（見 [MAP_SYSTEM.md](MAP_SYSTEM.md)、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)）。
- `event` 型 trigger（開機關、播旗標）——與劇情點類似的「資料驅動事件」，之後界定。

---

*建立於 2026-06-23：劇情觸發點（編輯器 `drama` trigger）＋ `DramaTable.csv` ＋ `DramaPanel`（模態、半透明遮罩、大圖+文字、ESC/點擊關閉）。觸發互動複用 [INTERACTION.md](INTERACTION.md) 的 InteractionManager（靠近按 F、星星標示、一次性消耗）。*
*2026-07-27 更正：「永久記錄屬 Phase 2」兩處已過時——跨換圖記憶已於 2026-07-18 由 `RunProgress` 完成。*
*2026-07-27：新增「防連點」一節（`UIPanel.TryConsumeInput` / `BlockInputFor`，0.5 秒節流＋開啟時先擋一次）。*
