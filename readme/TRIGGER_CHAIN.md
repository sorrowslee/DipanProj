# 觸發鏈（Trigger 接 Trigger）

> 返回 [文件總覽](README.md) ｜ 主遊戲端：`Assets/Scripts/Map/TriggerChain.cs` ｜ 編輯器端：trigger 面板的「觸發鏈/條件（通用）」欄位組

任何 trigger 完成後可以自動啟動另一個 trigger，接成任意長的鏈：
**對話 → 收到物品 → 傳送門開啟**、**對話 → 直接傳送**、對話 → 對話 → …（無限層）。

## 1. 核心概念

每個 trigger（任何類型）都多了通用欄位 **`next`**：填**同地圖**另一個 trigger 的**名稱**（或 id）。
本 trigger 的動作「完成」時（對話面板關閉、物品入包、拾取完成…），自動啟動 next 指向的 trigger。

被 next 啟動時的語意依目標類型分兩種：

| 目標類型 | 被鏈啟動時 |
|---|---|
| **動作型** `giveItem`／`teleportTo`／`drama` | **立即執行**（給物品／直接傳送／立刻播對話） |
| **位置型** `teleport`／`pickup`／`cutscene` | **解鎖**（配合 `startDisabled`；之後玩家踩到/按 F 才生效） |

**camZone 也能當鏈的起點**：它的「完成」= 玩家踩進區域、**鏡頭拉伸到位的瞬間** → 觸發 next。
（zoom 留空 = 立即到位，等於「純踩踏就觸發鏈」的隱形區域。每次進區都會觸發一次，
一次性請在鏈的下一節點掛 requireFlag，例：邪佛全貌 camZone → next=邪佛對話(requireFlag=`!hallGateOpen`)。）

動作型 trigger 不綁位置——編輯器仍要塗格（區域資料需要格子），**畫在地圖角落即可**，玩家踩到也不會有反應，只有被鏈啟動才執行。

## 2. 通用欄位（全部選填，每種類型都有）

編輯器面板把這些欄位分成四個小節（括號內為編輯器上的中文標籤）：

**條件（可不可以觸發，全部以 AND 結算）**

| 欄位 | 編輯器標籤 | 意義 |
|---|---|---|
| `requireFlag` | 條件旗標 | 旗標成立才可觸發。**從旗標登記表下拉選**（不手打）＋「有/沒有」切換（沒有＝存成 `!名字`）。生命週期由登記表決定，見 §2.5 |
| `requireCycleMax` | 周目上限 | 周目（`SaveManager.Cycle`）**≤** 此值才成立。**初始限定填 `1`**（只第 1 周目、老手不再觸發）。留空＝不限 |
| `requireCycleMin` | 周目下限 | 周目 **≥** 此值才成立（做「玩了好幾輪才出現」的對話）。留空＝不限 |
| `requireItem` | 道具條件 | 背包道具條件（只算背包格、不含裝備欄）：填 `itemId`＝**須有**此道具；前綴 `!`（如 `!104`）＝**須無**此道具。留空＝不檢查 |
| `requireClearsMin` | 最低完成關卡數 | 完成關卡數 **≥** 此值才成立。做「打過至少 N 關才開放」（例：抽選祭壇填 `1`）。留空＝不限 |
| `requireClearsMax` | 最高完成關卡數 | 完成關卡數 **≤** 此值才成立。做「只有還沒打過關的新手才觸發」（例：邪佛初始對話填 `0`）。留空＝不限 |
| `requireClearsScope` | 關卡數範圍 | 上面兩個數字算哪一種：`cycle`（預設，本周目完成幾關）／`life`（這隻角色終身累計）。循環按鈕切換 |

> **為什麼另外要有「完成關卡數」而不是只用周目**：邪佛初始對話原本用 `requireCycleMax=1` ＋ `requireItem=!104` 守門，但打完第一關後劇本被消耗掉、周目卻仍是 1 → **兩個條件同時再度成立，初始對話與新手教學會重播**。「這周目打過幾關」才是「還是不是新手」的正確判準。（見 [PROBLEMS.md](PROBLEMS.md) K2）

**條件不成立時要怎樣（`onBlocked`）**

| 欄位 | 編輯器標籤 | 意義 |
|---|---|---|
| `onBlocked` | 條件不成立時 | 上面任一條件不成立時的行為：`中止整條鏈`（**預設**，＝舊行為）／`跳過這顆繼續`（自己不做事，但照樣 `Activate(next)`） |

> ⚠️ **在鏈「中間」那顆加條件前必讀**：預設是「條件不成立 → 整條鏈中止」。這對鏈的**第一顆**是對的（整段事件不該發生），但對中間那顆＝「跳過一句對話」會連同**後面所有動作一起取消**。實例：`邪佛對話` 加了 `最高完成關卡數=0` 之後，後面的 `給紅嫁衣劇本`／`劇本開門` 一起沒了，玩家軟鎖在廣場。正解是同時設 `條件不成立時=跳過這顆繼續`。條件不成立現在會印一行 log，排查「鏈莫名斷在中間」先看 Console。

**一次性（會不會重複觸發）**

| 欄位 | 編輯器標籤 | 意義 |
|---|---|---|
| `repeat` | 重複規則 | 用循環按鈕切換：`關卡單次`(預設)／`每次`／`每周目`／`永久`。見 §2.5。目前作用在 drama／pickup 互動點與 onEnter 進場觸發 |

**流程（觸發後做什麼）**

| 欄位 | 編輯器標籤 | 意義 |
|---|---|---|
| `next` | 接續觸發 | 完成後啟動的 trigger（名稱優先，找不到再比對 id；**建議不要重名**） |
| `setFlag` | 完成寫旗標 | 動作完成後寫成立的旗標（給別的 trigger 的條件旗標用）。**從旗標登記表下拉選**，生命週期由登記表決定 |

**解鎖（位置型：等鏈解鎖）**

| 欄位 | 編輯器標籤 | 意義 |
|---|---|---|
| `startDisabled` | 初始停用 | 停用中＝踩到/按 F 無反應、星星不顯示、綠幕隱藏 |
| `enableFlag` | 解鎖旗標 | 「已解鎖」旗標：被鏈解鎖時自動寫成立；重進地圖/讀檔後旗標成立就自動啟用（門開了就一直開）。**從旗標登記表下拉選** |
| `linkedFx`（teleport 專用） | 連動特效id | 場景特效 id：trigger 停用時特效隱藏、解鎖時顯示（傳送門綠幕） |

## 2.5 旗標與範圍：整套只有「一種旗標」，差在活多久

一句話：**所有會被記住的東西都是旗標；一個旗標＝一個名字＋一個生命週期**。生命週期三種：

- **周目**：存 `progress.flags`，**輪迴（`ReincarnateInPlace`）會整包清空** → 下一周目重置。
- **永久**：存 `CharacterSave.lifetimeFlags`，**跨輪迴保存**，只有開新角色才空。
- **關卡單次**：**只存記憶體、不進存檔**，**每次進「關卡（module）」時歸零**（`MapManager` 換 module 時 `TriggerChain.ClearLevelFlags`；同 module 房間互跳不清）。用在「**這一趟關卡有沒有發生某事**」這種**每次進關要重算**的判定——典型就是 `killedFamily`（這趟有沒有殺家人）：周目旗標會讓「上一趟殺過」殘留到這趟，關卡單次才正確。存讀檔會重置（讀檔＝重新進關），對這類判定剛好。

### 2.5.0 旗標管理器（作者實際怎麼用）

生命週期是**旗標自己的屬性**，集中在編輯器 trigger 頁籤的「**旗標**」按鈕 → 旗標管理器裡設定：一列一個旗標，含**系統自動配的 id**＋名稱＋生命週期切換鈕（**周目 → 永久 → 關卡單次** 循環）＋刪除，可新增、可「自動匯入地圖旗標」（掃所有地圖把用到的旗標收編）、按「儲存」寫出 `flags.json`。

之後所有觸發點的「條件旗標／完成寫旗標／解鎖旗標」都用 **id 配置**（不手打名字、不會拼錯）：欄位是一個輸入框，**輸入旗標 id → 按「確認」**，系統就去登記表查該 id、把名稱填上並鎖定顯示（如 `hallGateOpen（周目）`）；此時旁邊出現「刪除」，按了就清空、又變回可輸入。**沒按確認、或查無該 id，就不會有名稱出現＝沒配置成功**（名字正確出現才算數）。條件旗標鎖定後另有「有/沒有」切換（沒有＝存成 `!名字`）。

**運作（方案乙，單一來源）**：地圖裡的觸發點**只存旗標裸名**（＋否定 `!`），不帶生命週期；生命週期只存在 `flags.json`。遊戲端 `FlagRegistry` 讀 `StreamingAssets/MapAssets/flags.json`，`TriggerChain.FlagTrue`/`SetFlag` 查表決定旗標存周目還是終身。所以你在管理器改一次某旗標的生命週期，**所有用到它的觸發點都一起改**。

> `flags.json` 的位置：編輯器授權檔在 `DipanProj_MapEditor/flags.json`；跑「刷新素材／同步腳本」會帶進 `StreamingAssets/MapAssets/flags.json` 給遊戲讀。改名/刪旗標後，已放好的觸發點不會自動更新，記得回去重選。

**旗標分兩種來源**：
- **具名旗標**（你在管理器建的）：只為了**跨 trigger 的劇情邏輯**（A 影響 B），用 `完成寫旗標` 寫、`條件旗標` 讀。
- **自動旗標**（系統用 trigger id 產生，作者看不到也不用命名）：撐起 `重複規則` 的「每周目一次／永久一次」，見 §2.5.1。內部用 `永久:` 前綴標記終身、不進管理器。

> 為什麼「只第 1 周目」用 `周目上限=1` 而不用旗標？因為周目 `Cycle` 天生跨輪迴持久，而周目旗標輪迴會被清。要「跨輪迴只一次」就把旗標設成**永久**，或用 `重複規則=永久`。
> 程式端 `FlagTrue`/`SetFlag`：名字帶 `永久:` 前綴（自動旗標/相容）→ 終身；否則查登記表。無存檔的單場景測試退回記憶體，行為一致但不持久。

### 2.5.1 重複規則（`repeat`）四種模式

決定同一個互動點多久能再觸發一次。四種模式其實就是「同一個自動旗標、不同生命範圍」：

| 編輯器值 | 何時會再觸發 | 機制 |
|---|---|---|
| `關卡單次`（預設） | 每次重新進這張地圖 | 當次停留消耗（記憶體），離圖重進復活 |
| `每次` | 每次踩/按都觸發 | 不消耗；自動觸發型（碰到就播）加「離開半徑才重新武裝」避免每幀洗版 |
| `每周目` | 下一個輪迴 | 觸發後寫**周目**自動旗標；建點時已觸發就不現身。輪迴清 → 下周目再觸發 |
| `永久` | 永不（除非開新角色） | 觸發後寫**終身**自動旗標（跨輪迴保存） |

> 「每周目一次」也可以用 `條件旗標=!x + 完成寫旗標=x` 手動做到（周目旗標輪迴會清），但用 `重複規則=每周目` 更省——不必自己命名旗標。`條件旗標`/`完成寫旗標` 建議只留給跨 trigger 的劇情條件。

## 3. 新增的動作型類型

- **`giveItem` 給予物品(鏈動作)**：`itemId`、`count`（留空=1）。直接進背包＋中央 toast「獲得 XX」，裝不下的掉玩家腳下。**不用按 F**。
- **`teleportTo` 直接傳送(鏈動作)**：`targetMapId`、`targetEntrance`。直接換圖。**不用踩傳送點**。
  ⚠️ 換圖 = 鏈的終點（setFlag 會先寫掉，next 填了也不會執行）。cutscene 同理。
- **`cameraFocus` 鏡頭聚焦(鏈動作)**：`holdSeconds`（停留秒數，留空=1.6）、`dim`（黑幕樣式：`中央留洞`(預設)／`整片全黑`／`無`）。被鏈啟動時**飄鏡頭到自己那格區域中心＋壓黑幕、停留、再拉回**，全程定住玩家；**表演完才接 next**。聚焦中心＝這個 trigger 畫的格子中心（通常畫在要對準的地方正中一格，例如傳送門中間）。純靠鏈驅動，**玩家踩不觸發**。
  典型用法：`對話 → next → cameraFocus → next → 下一段對話`，讓「講完話 → 鏡頭帶去看目標 → 再講引導詞」的運鏡完全由地圖資料排出來，不用寫程式。
- **`playCutscene` 播放劇情(鏈動作)**：`cutsceneId`（要播哪一段演出，**留空＝第一段**）。被鏈啟動時播這張圖在編輯器「劇情」分頁排好的那段演出，**演完才接 next**。純靠鏈驅動、玩家踩不觸發，格子畫在地圖角落即可。
  **為什麼需要它**：劇情演出原本**只能「一進圖自動播」**，而自動播沒有任何一次性機制——`CutsceneDirector.MaybeAutoStart` 不查旗標也不查 `repeat`，**每次進這張圖都會重播一次**。把該段的「一進圖自動播」關掉、改放這顆 trigger 之後，整套守門條件立刻全部可用：`條件旗標`／`重複規則`（關卡單次·每次·每周目·永久）／`周目上下限`／`完成關卡數`／`條件不成立時`。
  典型用法：`camZone(走進房間) → next → playCutscene(重複規則=每周目) → next → togglePortal(開門)`。
  ⚠ **該段演出結尾若有 `end` 交棒**（換圖／接墜落），鏈就此結束、**不會接 next**（同 `teleportTo`）。開不成時（找不到那段／沒步驟／已有演出在跑）會印 Warning 並直接接 next，不讓鏈卡死。
  ⚠ **只要「進圖只播一次」的話不必放這顆 trigger**——2026-08-22 起 Cutscene 自己就有 `條件旗標`／`完成寫旗標`（同一套旗標登記表），勾自動播＋填一組旗標即可，見 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §1.5。這顆 trigger 要解決的是**「什麼時候播」**（走到某處、打贏 boss、拿到東西之後），不是「播幾次」。
  詳見 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §2.5。
- **`playScreenFx` 播放螢幕特效(鏈動作)**：`effectId`（螢幕特效 id，欄旁有「**螢幕特效表**」按鈕可查/填清單）、`duration`（特效秒數，留空＝該特效預設）。被鏈啟動時**就地播一次性全螢幕過場特效**（依 `effectId` 分派，**id 1＝破幻術**「幻境崩碎回歸現實」：voronoi 玻璃裂紋 → 碎塊帶色散翻轉崩落 → 露白光 → 全白），暫停遊戲＋擋操作、**播完才接 next**。純靠鏈驅動、玩家踩不觸發。
  典型用法：`紅嫁衣對話 → next → playScreenFx(effectId=1) → next → teleportTo`（對話完 → 幻境當場崩碎 → 傳去現實榕樹妖）；破幻術收尾全白剛好蓋過跨 module 載入頁。
  程式（**加一種螢幕特效的三個維護點**）：① 寫該特效的 shader＋控制器（仿 `IllusionShatterController`／`EyeOpenController`，提供 `static Play(onDone, duration)`）；② 遊戲端 `ScreenFxPlayer.Play` 加一個 `case`；③ 更新編輯器「螢幕特效表」清單（`EditorUI.ScreenFxCatalog`）＋本檔／MAP_ENTER_EFFECT.md。這樣**加特效只動資料＋控制器、永遠不用再加 trigger 型別**。
- **`playerHint` 玩家提示(鏈動作)**：`leftImage`／`rightImage`（左上／右上那張的**檔名**，放 `Resources/UI/Common/`、不含副檔名，留空＝該側不顯示）、`flashLeft`／`flashRight`（各自要不要閃，預設左不閃、右閃）、`hideOn`（收起時機：`移動`(預設)／`攻擊`／`任意鍵`）。被鏈啟動時在**玩家頭上左上／右上各擺一張提示圖**（左右位置固定、跟著玩家跑），到收起時機（玩家開始移動／攻擊／按任意鍵）**自動收起才接 next**。左右槽的螢幕位移是 `PlayerHintPanel` 常數（調一次、編輯器只選左右不用填 XY）。純鏈驅動、不擋輸入、不暫停、玩家踩不觸發。
  **做「只出現一次」**＝用通用旗標欄：`完成寫旗標=永久:xxx`＋`條件旗標=!永久:xxx`（收起後才寫旗標，之後 `requireFlag` 不成立就不再顯示）。典型用法：`醒來對話 → next → playerHint`（左圖 `Guide_Wasd` 不閃、右圖 `Guide_Press` 閃、收起=`移動`）＝移動教學；攻擊教學照抄一個放 `Guide_MouseLeft`、收起=`攻擊`。
- **`togglePortal` 開關傳送點(鏈動作)**：`target`（要開關的**傳送點名稱**，＝該傳送點的「名稱」欄；**可多筆**——編輯器按「＋」加一欄、「−」刪一欄，存成逗號分隔字串，一次開關多個門）、`show`（打勾＝顯示解鎖／不打勾＝**隱藏封鎖**，預設隱藏）。被鏈啟動時把這些 target 傳送點**逐一隱藏封鎖或顯示解鎖**——同時處理**踩踏功能**（進/移出停用集）＋**視覺**（`linkedFx` 綠幕＋`showMarker` 傳送點外型都跟著開關，看起來真的消失/出現）。Boss 房封門用：`onEnter → 對話 → togglePortal(target=門A,門B…, show=off)` 進門一次封多個門；打贏後接在 Boss 死亡旗標鏈上 `togglePortal(show=on)` 一次復原；最終關不接復原＝永久封著。（傳送點名稱請勿含逗號。）
  ⚠️ 隱藏是**執行期狀態、刻意不寫存檔**——換圖/重進房間會依 `startDisabled`＋`enableFlag` 重算（所以每次進 Boss 房靠 onEnter 重新封即可）。`show=on` 走既有解鎖邏輯，會順便寫該傳送點的 `enableFlag`（若有設）跨存讀檔記住開著。另：「一開始就封、打贏才開」其實不必用這個——傳送點設 `startDisabled`＋`enableFlag`、Boss 死亡旗標鏈接解鎖它即可（本動作是給「本來看得到、中途才封」的情境）。
- **`bossIntro` Boss開戰資訊(鏈動作)**：`monsterId`（**MonsterData.csv 的怪物 ID**，與怪物出生點同一個 id）、`warnVfxId`（警告特效 id，留空＝14「警告」）。被鏈啟動時播**開戰表演**（分段接力不重疊）：暫停遊戲 → 壓黑底版＋血色暈影淡入、上下**電影黑邊**滑入 → 螢幕中央播「警告」特效（VfxTable 序列幀，播滿 `WarnSeconds` 後**消失**）→ 左側滑入 boss **頭像**（Talk 立繪）、右側滑入**空白姓名牌匾**（專屬圖 `Resources/UI/BossIntroPanel/BossIntroPanelNameBG`）→ 名字以**扭曲抖動的半透明毛筆字**漸漸復原＋淡入浮現在牌匾上 → 停留一拍整體淡出，**表演完才接 next**。**不可跳過**——刻意讓玩家完整看完開戰資訊才開打，沒有任何按鍵/點擊捷徑。純鏈驅動、玩家踩不觸發；格子畫在角落即可（0 格也可）。
  **資料在表、trigger 只填 id**：顯示名／頭像填 `MonsterData.csv` 新增的 `DisplayName`／`PortraitPath` 欄（PortraitPath＝Talk 立繪的 catalog id，例 `Modules/RedBridalGown/Talk/redBridalGown_angry`；頭像與對話立繪同一條管線 `DramaTalkDatabase.ResolvePortrait`，**不用新素材分類**）。⚠️ `Name` 欄是程式鍵（動畫資料夾索引），不能拿來顯示，所以才另開 DisplayName。
  程式：`BossIntroPanel`（UIPanel，Overlay 層、PausesGame；警告序列幀借 VfxManager 已載好的 `VfxData.AnimationSprites` 在 **UI 端 unscaled 逐格播**——不能用 `VfxManager.Spawn` 世界端播，暫停會凍住）。壓迫感配件每項可關：壓黑底版 `DimAlpha`、電影黑邊 `LetterboxHeight`、血色暈影 `VignetteAlpha`（程序生成漸層、Perlin 呼吸）、名字扭曲 `NameWarpAmount`/`NameWarpSpeed`（`NameWarpEffect` BaseMeshEffect 頂點特效、零素材）。姓名用**毛筆字型**：`NameFontPath`（預設 `Fonts/Bakudai/Bakudai-Bold`＝莫大毛筆，SIL OFL；經 `UIBuilder.LoadFont` 載入、載不到退回預設字型；全 UI 其他文字仍用內建字型）。**調表演**：節奏與版面全是 public 欄位——Play 模式在 Hierarchy 選 `[UIManager] → Layer_Overlay → BossIntroPanel`（第一次播過後才存在）即時調，重觸發就套用（版面每次 Begin 重算）；面板是執行期生成、Play 調的值退出不保存，定案後回填 BossIntroPanel.cs 的預設值。典型用法：`boss 房 onEnter → 對話 → bossIntro(monsterId=13) → togglePortal 封門`。
- **`onEnter` 進場觸發(自動)**：**一進這張地圖就自動觸發**（不用玩家踩、不用按 F），純鏈起點——自己不做事，全靠 `next` 接要做的事（播對話、給物品、鏡頭聚焦…）。`delaySeconds`（延遲秒數，留空=0）：進場後再等幾秒才觸發。
  - **不塗格子**：編輯器 trigger 頁籤選「進場觸發(自動)」→ 按「**＋ 手動新增空區域**」→ 從**區域清單**點選它（0格）→ 填參數。畫布上不佔任何格。
  - **點火時機**：地圖載入完全結束（載入頁已關、玩家已就位）→ **等進場效果（睜眼醒來）播完** → **等玩家「趴地→起身」表演完**（睜眼連動，見 [MAP_ENTER_EFFECT.md](MAP_ENTER_EFFECT.md) §1.5）→ 等延遲秒數 → 檢查通用條件 → 觸發。程式：`MapManager.FireEnterTriggersRoutine`。
  - **一次性控制**：預設 `關卡單次` = 每次進這張圖都觸發。只想觸發一次用 `重複規則=每周目/永久`，或 `周目上限=1`，或 條件旗標/道具條件。
  - **同圖多顆**：依區域清單順序依序點火，各自查條件；前一顆的鏈若開了對話，會**等對話關閉**才點下一顆。仍建議用條件把多顆錯開（例：一顆 `requireFlag=!x`、另一顆 `requireFlag=x`）。
  - 邊界：換圖時進行中的點火中止（新圖有自己的一輪）；被別的鏈 `next` 指到時＝純轉接（直接完成、接自己的 next）；單場景測試（無 MapManager）不會點火。
- **`unlockRoll` 解鎖抽選內容(鏈動作)**：`poolId`（哪個抽選池，＝`GachaPoolTable.csv` 的 `PoolId`，例 `weapon`／`blood`）、`itemId`（要加進池的道具 ID）。被鏈啟動時把這一筆寫進存檔的 `unlockedRollEntries`，該祭壇之後就抽得到它。**永久、跨輪迴、重複執行無害**（同一筆只會有一份）。典型用法：接在 boss 死亡旗標鏈上——`紅嫁衣死 → unlockRoll(blood, 302 幽靈血統藥劑)`、`榕樹妖死 → unlockRoll(weapon, 地刺戢)`。純鏈驅動、玩家踩不觸發、格子畫在角落即可。詳見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md) §3。

- **`factionWar` 三方陣營開戰(鏈動作)**：無參數。被鏈啟動時把三方陣營劇本切成「開戰」——狼人/吸血鬼兩族開始互咬（演戲傷害 1/100）＋攻擊玩家、切到可被玩家攻擊的層，立即接 next。狀態＝**關卡單次**（換 module 自動回和平）。典型：`對話/事件 → factionWar（完成寫旗標=部族開戰）`——同一顆順便讓和平版 NPC 退場（NPC 的「消失旗標」填同一旗）。詳見 [FACTION.md](FACTION.md)。
- **`joinFaction` 加入陣營(鏈動作)**：`faction`（填 `狼人`/`werewolf` 或 `吸血鬼`/`vampire`）。被鏈啟動時玩家結盟該族——該族不再攻擊玩家、玩家武器打不到它（切 Ally 層），立即接 next；認不得的值印 Warning、仍接 next 不卡鏈。典型：首領 NPC 對話 → next 接這顆。詳見 [FACTION.md](FACTION.md)。

**位置型（靠近按 F，不是鏈動作）**

- **`openPanel` 開啟介面(按F)**：`panelId`（要開哪個面板，目前只有 `gacha`）、`poolId`（`panelId=gacha` 時用：要開哪個抽選池）。玩家走到這幾格附近會出現星星＋「按 F」提示，按 F 開該面板。**地上物與觸發是兩件事**——祭壇的圖是地圖上的地上物（`rockAltar.png` ＋ `rockSlate_weapon.png` 之類的牌子），互動是**另外畫一顆 `openPanel` 觸發**蓋在祭壇前方玩家站得到的地板格上（跟 pickup 一樣要留意 [PROBLEMS.md](PROBLEMS.md) K1：感應格別只畫在實心物上）。條件欄位照常有效——祭壇填 `最低完成關卡數=1` ＝ 打過一關才開放，未達門檻走過去不會出現提示。
  > 程式端：`InteractionManager` 原本是寫死的 `enum PointKind` + 三處 switch，這次改成**可註冊的 `InteractKind` 表**（`TypeId`／`MarkerColor`／`Setup`／`Tip`／`Activate`），之後加互動型別只要在 `BuildKindRegistry()` 加一筆。

## 3.5 怪物出生點（`monsterSpawn`）：一次性／重複產生／隨機怪種

塗幾格＝生幾隻（一格一隻），這條規則兩種模式都一樣。四個欄位：

| 欄位 | 意義 |
|---|---|
| **怪物id(`\|`分隔)** | 單一 id（例 `5`）＝固定生這種；**多個以 `\|` 分隔**（例 `5\|7\|9`）＝每隻各自從中隨機挑一種（沿用專案慣例，同 `SummonIds`／`scriptIds`）。 |
| **死亡觸發旗標** | 這個出生點生的怪死亡時寫此旗標（見 §7）。兩種模式都有效；旗標只會在「首次成立」那一刻推鏈，重複產生的怪一直死也只推一次。 |
| **重複間隔秒** | **留空／0＝進圖生一次（原本的行為）**；填秒數＝每隔這麼久生一波。 |
| **同時存在上限** | 只有重複模式看這欄。這個出生點生的怪還活著幾隻 ≥ 上限時，那一波就少生／不生（死了才補）。**留空＝10**（保險預設，避免忘了填就無限堆積）。 |

**「什麼時候才開始生」用的是通用條件欄位，出生點沒有自己的一套。** 出生點會走 `TriggerChain.IsActive`，所以參數下方那組通用欄位（條件旗標／初始停用＋解鎖旗標／周目上下限／道具條件／完成關卡數）對它全部有效。兩種常見寫法差在「能不能再關掉」：

| 想要的效果 | 填哪個 |
|---|---|
| **按 F 開始、再按一次暫停** | **條件旗標**填開關的切換旗標。條件是每幀重算的**持續判定**，旗標取消就停止補怪（場上已生的不會消失、計時器停在原地）、恢復就繼續。⚠ **「初始停用」不要勾**——那是一次性解鎖語意，勾了又沒有鏈去解鎖就永遠不會生。 |
| 打倒某隻 boss／完成某事件後才開始湧怪，之後不再關 | **初始停用**打勾 ＋ 由某條鏈的 `next` 指到這顆出生點解鎖；要跨存讀檔記住就再填**解鎖旗標**。 |
| 「某旗標一成立就停止湧怪」 | **條件旗標**填 `!旗標名`（否定）。 |
| 第 2 周目以後這裡才有怪 | **周目下限**填 2。 |

⚠️ **「初始停用」與「條件旗標」不要同時填**——語意互斥（初始停用要靠鏈 `Activate` 解鎖，而 `Activate` 會先查條件旗標、不成立就整條鏈中止 → 永遠解不開），結果是**這顆出生點永遠不生怪**。填錯時 Console 會警告。
另外**「重複規則」對出生點無效**（填了不會有任何效果，Console 也會警告）：「殺掉後本趟不再重生」是 `RunProgress` 自動處理的，不需要也不看這欄。

實作上只有「有填條件」的出生點才交給 `MapMonsterRespawner` 逐幀判定，其餘維持原本進圖直接生完的路徑。**有條件的那些連第一波都交給 `Update`**，因為 `MapManager.PlaceAndSetup` 是 `SpawnMonsters()` → `SetupWatcher()`（`TriggerChain.Setup` 在後），載圖當下去查 `IsActive` 會讀到上一張地圖的停用集合。

**兩種模式的差別（重要）**

| | 一次性（重複間隔秒留空） | 重複產生（有填秒數） |
|---|---|---|
| 生成時機 | 進圖時一次 | 進圖時先生第一波，之後每隔 N 秒一波 |
| 記 RunProgress『已清』 | ✅ 記（殺掉後本趟換圖回來不再重生，Boss 也走這條） | ❌ **不記**——記了第一波死光就永遠不再生，整個功能失效 |
| 掉寶 | ✅ 照常（必掉銅錢＋35% 掉藥） | ✅ **照常掉**（刻意的；靠「同時存在上限」與地圖設計節制，不是靠不掉寶擋） |
| 上限 | 塗幾格就幾隻 | 同時存在上限 |

⚠️ **重複產生 ＋ 掉寶 ＝ 可以刷錢**。這與核心迴圈「關卡一次性、不可無限刷」的定案（見 [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md)）有張力，所以：把「同時存在上限」壓低、間隔拉長，或只用在有時限／有出口壓力的房間（例：撐到開門、boss 戰期間的雜兵）。

**計時細節**：用有縮放的 `Time.deltaTime`——開背包／對話暫停時不會偷偷累積。計時器 `MapMonsterRespawner` 掛在 `MapRoot` 下，換圖隨之銷毀（回到這張圖＝重新開始，第一波立刻生）。另外它會**主動停在兩個時段**：換圖載入中（讀取頁刻意不暫停遊戲，不擋的話舊圖會一路生到新圖去，見 [PROBLEMS.md](PROBLEMS.md) B8）、以及過關倒數／死亡等待（這兩段也不暫停，不擋會出現「都死了還在冒怪」）。

**多個 id 的挑選方式**：重複模式每隻都重新亂數挑；一次性模式改用「格座標穩定雜湊」挑（同一格在同一趟關卡換圖來回不會突然變成另一種怪，不同格則各不相同）。

程式：`MapLoader.SpawnMonstersFromMap`（解析＋分派）、`MapMonsterRespawner`（計時、條件判定與上限）、`MonsterSpawner.SpawnMonster(..., dropsLoot:)`、`MonsterController.SpawnKey`／`DropsLoot`（記進度與掉寶已拆成兩個獨立判斷）。

## 3.6 開關（`switch`）：靠近按 F 切換一個旗標

位置型。玩家走到這幾格附近會出現**青綠星星**＋提示，按 F ＝**把「切換旗標」翻成成立／取消**。它自己不做任何事——不開面板、不給道具、不播對話；**做什麼由「誰在看這個旗標」決定**：

| 誰在看 | 效果 |
|---|---|
| 怪物出生點的「條件旗標」 | 開始／暫停生怪 |
| 地上物的 `appearFlag` / `disappearFlag` | 東西出現／消失。⚠️ **單向**：`MapObjectRevealer` 聽的是「旗標首次成立」，關掉開關不會讓它變回來（`disappearFlag` 更是直接銷毀物件） |
| 其他 trigger 的「條件旗標」 | 那顆 trigger 可不可以觸發（開關切換後會自動重建互動點，星星與提示即時跟著出現／消失） |
| 自己的「完成寫旗標／接續觸發」 | **第一次開啟時**跑一次（所以也能當一般機關用：開門、播對話…） |

三個欄位：

| 欄位 | 意義 |
|---|---|
| **切換旗標** | 必填。按 F 要開關的旗標（用旗標登記表選）。**建議登記成「關卡單次」**——那種只活在記憶體、進新關卡就清空，最適合「這一趟的機關狀態」；登記成周目/永久的話會寫進存檔、下次進來還是開著的。 |
| **未啟動提示** | 旗標還沒成立時的提示後綴，留空＝「開始」→ 顯示「按 F 鍵開始」。 |
| **已啟動提示** | 旗標成立後的提示後綴，例「暫停」。**留空＝一次性開關**：按下去就收掉星星、不能再關。 |

⚠️ **開關的「圖」是地上物、不是這顆 trigger**：拉桿／石碑／按鈕要在地圖編輯器擺成地上物，感應格畫在它**前方玩家站得到的地板**（實心地上物那格走不進去，跟祭壇同一個坑，見 [PROBLEMS.md](PROBLEMS.md) K1）。

**組一個「按 F 才開始湧怪」的房間**（競技場就是這樣）：

| 擺什麼 | 怎麼填 |
|---|---|
| 旗標管理器 | 新增一個旗標，例 `開始湧怪`，範圍選**關卡單次** |
| 地上物 | 拉桿／石碑，擺在玩家出生點附近 |
| `switch` 開關 | 感應格畫在地上物前方；切換旗標＝`開始湧怪`、未啟動提示＝`開始`、已啟動提示＝`暫停` |
| `monsterSpawn` 出生點 | 照常填怪物 id／重複間隔秒／同時存在上限，**條件旗標＝`開始湧怪`**（「初始停用」不要勾） |

進圖 → 慢慢整理裝備（一隻怪都沒有）→ 走到拉桿按 F → 開始每 N 秒湧怪 → 想停下來調東西就再按一次 F。

程式：`InteractionManager` 的互動型別表加一筆（`TypeId = TriggerChain.TypeSwitch`）＋ `SwitchPoint`；旗標取消用新增的 `TriggerChain.ClearFlag`（與 `SetFlag` 對稱，三種範圍都支援）。
`SwitchPoint` 的順序是**先消耗自己、再跑鏈、最後（必要時）重建互動點**——反過來的話 `OnCompleted` 裡的 `setFlag` 會同步重建互動點，而此時本點還沒進「已消耗」，就會被原樣建回來＋長出一顆按不動的死星星（同 `CollectPickup` 的既有寫法）。
另外兩個實作上的取捨：**「這顆開關的鏈跑過了沒」記在自動旗標 `關卡:已開關:<id>` 而不是記憶體集合**（記憶體版換圖就清空，玩家「開→關→去隔壁房→回來→再開」會把整條鏈重播一次）——前綴 `關卡:` 是這次新增的、與 `永久:` 對稱的**強制關卡單次**前綴，專給「程式產生、作者沒辦法在旗標登記表登記」的自動旗標用（不加就會落到預設的周目而寫進存檔，之後整個周目再也不跑鏈）；
**切換後只有「這張圖真的有別的互動點在看這個旗標」時才重建互動點**（只掃別顆 trigger 的條件旗標——其他看旗標的東西都不靠重建：地上物走事件、出生點每幀輪詢、傳送點每幀動態判定），因為重建會把全圖星星砍掉重生、閃爍相位歸零，而主用途「開關 → 怪物出生點」根本不需要重建（出生點是每幀自己輪詢旗標的）。
⚠️ `ClearFlag` **不會**觸發 `OnFlagFirstSet`／`fireOnFlag`（那是「首次成立」專用），而取消後再成立會被當成又一次首次成立 → **別把接了 `fireOnFlag`（觀察旗標變動）的旗標拿來當開關的切換旗標**，不然那條鏈會重跑。

## 4. 範例編排

**邪佛大廳（✅ 已實裝於 Main_Square；條件於 2026-07-06 改為周目＋背包判定）：看全貌 → 對話 → 給劇本 → 傳送門開**
| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `邪佛全貌` | camZone | zoom=1.8, offsetY=8, next=`邪佛對話`（鏡頭拉伸**到位**才觸發） |
| `邪佛對話` | drama（cells=0 純鏈節點） | dramaId=3, **requireCycleMax=1**（只第 1 周目）, **requireItem=!104**（背包已有劇本＝談過了，不再播）, **requireClearsMax=0 ＋ onBlocked=跳過這顆繼續**（2026-07-28 加：已打過關就不再播，但後面照跑）, next=`給紅嫁衣劇本` |
| `給紅嫁衣劇本` | giveItem（角落 1 格） | itemId=104(劇本-紅嫁衣), count=1, **requireCycleMax=1**, **requireItem=!104**（初始進度＋背包沒劇本才給）, next=`劇本開門` |
| `劇本開門` | teleport（門口） | startDisabled=✓, enableFlag=`hallGateOpen`, linkedFx=`2d656e16`(綠幕), targetMapId=1 |

> **為何從 `!hallGateOpen` 改成周目＋背包**：旗標在輪迴（`ReincarnateInPlace`）會被清空，所以 `!hallGateOpen` 只能擋「同一周目內」重複；第 2 周目旗標被清 → 對話與給劇本又會觸發。改用 `requireCycleMax=1`（跨輪迴持久）＋ `requireNoItem=104`（背包已有就不重給/不重播）後語意才正確：**只在第 1 周目、且尚未持有劇本時**才觸發。
>
> ⚠️ **連帶待辦（第 2 周目的門）**：給劇本被限在第 1 周目後，第 2 周目起這條鏈不跑、`劇本開門`(startDisabled) 不會被解鎖 → 老手進不了關卡。這塊會由**下一階段的「點傳送門→放置劇情道具 UI→開門」**接手（玩家自己把想去的關卡劇本放進傳送門即開），屆時 `劇本開門` 的解鎖改由「放入劇本」驅動，不再靠這條自動鏈。在該 UI 完成前，第 2 周目的門暫時無法開啟。
>
> **運鏡插入點（2026-07 規劃，作者接線）**：把「給劇本」後面接上一段引導對話再飄鏡頭——`給紅嫁衣劇本` 的 `next` 改指向新的 `邪佛叫你去傳送門`（drama，Group3），它的 `next` 再指向 `鏡頭聚焦傳送門`（**cameraFocus**，格子畫在傳送門正中間一格，`dim=中央留洞`）。這樣順序＝交劇本→補話「通過那扇門…」→對話結束才飄鏡頭壓黑。⚠️ 前提：對話用 `next` 是**整段播完才觸發**（`ExecuteDrama`→面板關閉→`NotifyDramaClosed`→接 next），把關鍵對話 group 加成兩句以上即可自行驗證。
>
> **2026-07-28 補：打完一關後初始對話會重播**。`requireCycleMax=1` ＋ `requireItem=!104` 這組守門有漏洞——劇本一旦被消耗掉、周目又還是 1，兩個條件就同時再度成立。修法是加 `requireClearsMax=0`（本周目完成 0 關才播），但**一定要同時設 `onBlocked=跳過這顆繼續`**，否則擋掉這顆會把後面的 `給紅嫁衣劇本`／`劇本開門` 一起吞掉、玩家軟鎖在廣場。同理 `初入場景對話` 也設 `最高完成關卡數=0`。見 [PROBLEMS.md](PROBLEMS.md) K2。

**祭壇抽選（✅ 2026-07-28 實裝於 Main_Square；地上物與觸發分開）**
| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `武器祭壇` | openPanel（祭壇**前方**玩家站得到那排地板格） | panelId=`gacha`, poolId=`weapon`, **requireClearsMin=1**（打過一關才開放） |
| `裝備祭壇` | openPanel | panelId=`gacha`, poolId=`armor`, requireClearsMin=1 |
| `血統祭壇` | openPanel | panelId=`gacha`, poolId=`blood`, requireClearsMin=1 |
| `紅嫁衣死→給幽靈血統` | unlockRoll（角落 1 格，接在 boss 死亡旗標鏈上） | poolId=`blood`, itemId=302 |

> 祭壇的**圖**是地上物（`rockAltar.png` ＋ `rockSlate_weapon.png` 等牌子），要在地圖編輯器另外擺；觸發只是蓋在它前面的一片感應格。整套抽選見 [GACHA_SYSTEM.md](GACHA_SYSTEM.md)。

**一進房間就播對話（onEnter 標準用法；兩顆都是「手動新增空區域」的 0 格節點）**
| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `進房觸發` | onEnter 進場觸發(自動) | delaySeconds=留空(或填秒數), repeat=`每周目`(只想播一次時), next=`進房對話` |
| `進房對話` | drama（cells=0 純鏈節點） | dramaId=`<對話ID>` |

> 一次性看需求選：`重複規則=每周目`（下輪迴再播）、`永久`（一輩子一次）、`周目上限=1`（只第 1 周目）。留預設＝每次進房都播。

**紅嫁衣：沒殺家人 → 對話完幻境崩碎 → 傳送到榕樹妖（機制✅ 完備；擺 trigger 見 §7）**
| trigger | 類型 | 關鍵欄位 |
|---|---|---|
| `紅嫁衣對話` | drama | dramaId=`<新>`, requireFlag=`!killedFamily`, next=`破幻術` |
| `破幻術` | playScreenFx（角落） | **effectId=1**（破幻術）, duration=留空(或秒數), next=`送去榕樹妖` |
| `送去榕樹妖` | teleportTo（角落） | targetMapId=10（RedBridalGown_TreeDemon） |

> `killedFamily` 在旗標管理器設成 **關卡單次**（每次進紅嫁衣重算，見 §2.5）。

## 4.9 傳送門：放劇本開門（hub，劇本決定去哪關）

新流程（邪佛不再自動開門）：**給紅嫁衣劇本（`setFlag=tutorialPortal` 啟動新手教學）→ 靠近傳送門按 F 開傳送門 UI（`ScriptsPanel`，強制連背包並排開）→ 把劇本從背包拖進單格方框 → 按圓鈕 → 消耗劇本、開啟對應傳送點（目的地＝劇本指定關卡）**。

- **劇本→目的地**：`ItemTable.csv` 的 `TargetMapId`（＋`TargetEntrance`）決定這本劇本去哪關（劇本 104 = RedBridalGown map 1）。方框只收「有目的地」的道具（＝劇本）。以後每種劇本填自己的關卡即可（天生 hub）。
- **編輯器要放兩個 trigger**（畫在傳送門地上物上）：
  1. `傳送門互動`（**portal** 型）：玩家靠近按 F 開 UI。參數 `linkTeleport` ＝下面那個傳送點的**名字**。
  2. `傳送點`（**teleport** 型，`初始停用`＝✓）：按下開啟後被解鎖。目的地會被劇本**執行期覆寫**，所以 `targetMapId` 可留空或填後備；`enableFlag`（如 `hallGateOpen`）讓開過就記住。
- 程式：按鈕 → `TriggerChain.OpenPortal(linkTeleport, 劇本.TargetMapId, 落點)` ＝設目的地覆寫 ＋ `EnableRegion`（亮綠幕）。`TeleportWatcher` 踩到傳送點時優先讀覆寫目的地。
- 新手教學：`TutorialManager`（步驟式）由 `tutorialPortal` 旗標啟動，三步（開 UI→放劇本→按鈕）跟著 `ScriptsPanel` 的事件前進，做完寫 `永久:tutorialPortalDone` 不再重播。

## 4.95 新手教學（強制引導，寫死在程式）

新手教學是**一次性、每次都一樣**的強制引導，刻意寫死在程式（不做成可編輯）。程式：`Assets/Scripts/UI/TutorialManager.cs`（大腦）＋`GuideFingerPanel`（手指）＋`TutorialBlockerPanel`（遮罩，只放行指定元件可點）＋`TutorialHintPanel`（提示字）。

兩段：
- **找邪佛手指**：玩家觸發「初入場景對話」→ 手指指畫面上方；觸發「邪佛全貌」→ 收起。（靠 `TriggerChain.OnTriggerFired` 廣播觸發點名字，跟對話內容無關。）
- **傳送門強制流程**：偵測到背包出現紅嫁衣劇本 → 放開自由跑 → 走到傳送門可按 F 那刻定住只能按 F → 開了傳送門 UI＋背包 → 遮罩＋手指指劇本(只能點) → 劇本入方框 → 遮罩＋手指指開啟鈕(只能點) → 按下開門 → 結束，寫永久記號不再出現。

> **改動（2026-07）**：原本「給完劇本 → 鏡頭飄去傳送門＋黑幕」這段過場**已從教學程式移除**，改成**資料驅動**——用 `cameraFocus`（鏡頭聚焦）trigger 接在邪佛對話鏈後面表演（見 §3 與 §4）。好處是可以在給劇本後**先補幾句對話**（例如「通過那扇門，去完成你的天命」），對話結束才飄鏡頭，不會再出現「鏡頭已對著門卻跳獲得道具」的違和。教學程式只在背包一有劇本時就進入「等玩家走到傳送門」，鏡頭過場期間玩家被 `cameraFocus` 定住、走不到門，所以不會提早觸發按 F。

> ### ⚠️ 特別注意：新手教學「寫死清單」（改動前必看）
> 以下是硬寫在程式裡的值/名字，不是資料驅動。要改動教學或改到相關東西時，**這些要同步改**（都集中在 `TutorialManager.cs` 上方常數，除非另註）：
>
> | 寫死的東西 | 值 | 在哪 | 改的時候注意 |
> |---|---|---|---|
> | 紅嫁衣劇本的道具編號 | `104` | `TutorialManager.ScriptItemId` | 若 `ItemTable.csv` 的劇本改編號，這裡要一起改 |
> | 啟動「找邪佛手指」的觸發點名 | `初入場景對話` | `TutorialManager.TrigArrive` | 編輯器那個 drama 觸發點若改名，這裡要改 |
> | 收起「找邪佛手指」的觸發點名 | `邪佛全貌` | `TutorialManager.TrigSawBuddha` | 編輯器那個 camZone 若改名，這裡要改 |
> | 教學做過的永久記號 | `永久:tutorialPortalDone` | `TutorialManager.DoneFlag` | 純程式內部、**不進旗標管理器**；改名無妨，別跟別的撞即可 |
> | 手指圖路徑 | `UI/Common/Guide_Finger` | `GuideFingerPanel.Res` | 換圖改路徑 |
> | 啟動教學＋尾聲手指指向 | 傳送門互動點中心 | `InteractionManager.TryGetPortalWorld` | 靠地圖上的 `傳送門互動`(portal) 區域算；沒放就不會啟動教學。**飄鏡頭本身已改用 `cameraFocus` trigger（資料驅動），不在這**；這裡只剩「啟動判斷」與「按下開門後手指指回傳送門」用 |
> | 強制按 F 的觸發判斷 | 走到傳送門互動點的可互動範圍 | `InteractionManager.PlayerNearPortal` | 用 pickup 半徑判斷 |
>
> 另外教學啟動**刻意不靠旗標、不靠對話 id**，而是靠「背包有沒有紅嫁衣劇本」＋觸發點名字廣播——因為邪佛那段對話之後可能一直加句子，用對話 id 會壞。
>
> **通用元件（不只教學能用）**：
> - `GuideFingerPanel`（指引手指）：`ShowWorldGuide(anchor, worldTarget)` ＝手指出現在 anchor（玩家）頭上、指尖角度**隨時朝向世界目標**、往目標前後輕點（找路、指方向都用它）；`ShowAtUI(rect)` ＝指某個 UI 元件。手指圖 `Resources/UI/Common/Guide_Finger.png`（未旋轉時指尖朝左上 135°，`TipBaseAngle`）；頭上高度 `HeadWorldOffset`。
> - `TutorialDimPanel`（黑幕）：`ShowFullBlack()` 整螢幕壓黑（突顯教學文字）／`ShowSpotlightCenter()` 中央留一個**矩形洞**（用上下左右四塊實心黑框出來；突顯畫面中央的目標，如鏡頭對準的傳送門）／`Hide()`。濃度與洞大小＝`TutorialDimPanel` 上方常數 `DimColor`(0.6)、`HoleHalfX/Y`。
>   ⚠️ 中央洞刻意用「四塊實心黑」而非程序生成的圓洞貼圖——實測某些環境下貼圖版不顯示、實心色塊才穩定畫得出來。
> - `TutorialHintPanel`：畫面上方教學大字（字級已放大）。
> - `PlayerHintPanel`（玩家提示圖）：玩家頭上左上／右上各擺一張圖、指定張閃爍、到收起時機自動收。**由 `playerHint` trigger 資料驅動**（見 §3），不寫死在教學程式裡——移動教學（醒來對話→`playerHint`：左 `Guide_Wasd` 不閃＋右 `Guide_Press` 閃、收起=移動）、之後攻擊教學都用它。
>
> **各時機用到的表演**：`cameraFocus` 鏡頭聚焦時＝中央留洞黑幕（該 trigger 的 `dim` 參數決定，預設中央留洞）；「按 F 開啟傳送門」時＝整螢幕黑幕＋放大字；按下開啟後＝不強制、只用頭上手指指引玩家走向傳送門（走近收手指）。

## 5. 運作細節（改程式前讀）

- **完成時機**：pickup=入包後；drama=**面板關閉時**（DramaPanel/TalkPanel 的 OnClose → `TriggerChain.NotifyDramaClosed`）；giveItem=給完立即。
- **停用/條件的判定點**：teleport/cutscene 在 Watcher **每幀動態查** `TriggerChain.IsActive(region)`（解鎖瞬間生效、不必重建格表）；pickup/drama 互動點在**建點時過濾**，解鎖或旗標變動時由 `MapManager.RefreshTriggers()` → `InteractionManager.RebuildPoints()` 重建（已消耗的點不會復活，`_consumed` 集合記著）。
- **每次換圖** `MapManager.SetupWatcher()` 先 `TriggerChain.Setup(...)`：算 startDisabled/enableFlag 的初始啟用態、套 linkedFx 顯隱，**之後**才建各 watcher/互動點。
- 鏈到 drama 型 = 立即播對話（不管它在地圖哪裡、不需要玩家走過去）。
- **對話接對話會延後一幀**：對話關閉（`NotifyDramaClosed`）後的接鏈，透過 `TriggerChainRunner.NextFrame` 排到**下一幀**才跑。原因：對話面板在自己的 `OnClose` 裡若同步接鏈又去開新對話 = 重入，會讓「正在關的面板」把「剛開的新面板」關掉、`IsOpen` 殘留 true → 遊戲永久暫停（玩家卡死）。延後一幀等舊面板關乾淨再開新的即可避開。所以 `對話→(giveItem…)→對話→cameraFocus` 這種鏈每個對話節點之間會有一幀空檔（無感）。
- 對話面板沒開成（DramaTable 缺資料）→ 關閉事件不會來 → 鏈停在該節點，Console 有警告。
- 無 SaveManager 時（單場景測試）旗標退回 session 記憶體，行為一致但不持久。

## 6. 待補 / 邊界

- 鏈只在**同地圖**內找目標；跨圖劇情請用旗標接力（前圖 setFlag → 後圖 requireFlag/enableFlag）。
- pickup/drama 的「一次性」仍是**當次停留記憶**（離圖重進會復活）；要永久一次性請用 requireFlag=`!xxx` + setFlag=`xxx` 的組合，或等地圖狀態持久化 Phase 2（見 [MAP_SYSTEM.md](MAP_SYSTEM.md)）。

## 7. 接手實作指南：紅嫁衣「沒殺家人」分支（⏳ 未實作）

> 目標劇情：玩家在紅嫁衣關卡若**沒有殺任何怪（＝沒對她的家人出手）**，與紅嫁衣對話完畢後傳送到榕樹妖地圖（MapsTable **10** = `RedBridalGown_TreeDemon`）對決；若**殺過任一怪**，紅嫁衣生氣、與玩家對戰。
> 鏈系統、**殺怪偵測機制**（出生點的「死亡觸發旗標」欄，2026-07-09）、**破幻術轉場**（泛用 `playScreenFx` 鏈動作＋崩碎後處理＝螢幕特效 id 1，2026-07-09）、**關卡單次旗標**（2026-07-09）皆已完備；剩下是填對話、在編輯器擺 trigger，以及兩場頭目戰（AI／技能另做）。

### 步驟 1：怪物死亡寫旗標（✅ 機制已實作，改在編輯器填、不用寫程式）

偵測「有沒有殺怪」已做成**資料驅動、綁在每個出生點**：怪物出生點 trigger 多了「**死亡觸發旗標**」欄（`deathFlag`）。這個出生點生出來的怪死亡時，`MonsterController.Die()` 就把該旗標設為 true（`MapLoader.SpawnMonstersFromMap` 讀 `deathFlag` → `MonsterSpawner.SpawnMonster` → `MonsterController.DeathFlag`）。生命週期（周目／永久／關卡單次）由旗標登記表決定，`TriggerChain.SetFlag` 自動處理、無存檔退回記憶體。

做法（全在編輯器，零改程式）：

1. 旗標管理器把 `killedFamily` 建成 **關卡單次**旗標（切換鈕循環到「關卡單次」）——每次進紅嫁衣關卡歸零、只存記憶體，所以判的是「**這一趟**有沒有殺家人」（周目旗標會殘留到下一趟就錯了，見 §2.5）。
2. 紅嫁衣關（RedBridalGown 各張地圖）家人怪的 `monsterSpawn` 出生點，「死亡觸發旗標」欄選 `killedFamily`（輸入 id → 確認）。因為家人怪只出現在這關，這個旗標實質就等於「這一趟殺過家人／殺過任何怪」。
3. 存檔後跑 `Project Tools → Sync Map Assets`。

> **為什麼綁在出生點而不是 MonsterData.csv**：出生點是「這一次擺放」、CSV 是「怪物種類」。綁出生點可讓同種怪在不同房間死時寫不同旗標（或不寫），且全在編輯器編、不碰 CSV。
> **同一套機制也給地上物**：可破壞物件的選取面板有「**破壞旗標**」欄（`breakFlag`），打破它就寫該旗標——做「打破珍貴供品 → 改變劇情」用，見 [DESTRUCTIBLE_OBJECTS.md](DESTRUCTIBLE_OBJECTS.md)。

### 步驟 2：對話資料（填表）

1. `DramaTalkTable.csv`：新開一個群組（目前最大 Group=2，用 **3**），寫紅嫁衣的對話句。
   立繪已在 catalog：`Modules/RedBridalGown/Talk/redBridalGown`；主角側用 `Actor_<情緒>`。
   立繪大小/位置不對就用表尾六個微調欄（LeftScale/LeftOffsetX/…，見 [DRAMA.md](DRAMA.md)）。
2. `DramaTable.csv`：新增一列（目前最大 ID=3，用 **4**）：`4,,紅嫁衣對話,2,3`（Type=2 頭像對話、TalkGroup=3）。

### 步驟 3：編輯器放 trigger（紅嫁衣的最終房間地圖，哪張由作者決定）

1. `紅嫁衣對話`（drama）：塗在紅嫁衣 NPC 位置（Type=2 碰到自動觸發）。參數：
   `dramaId=4`、`requireFlag=!killedFamily`、`next=破幻術`。
2. `破幻術`（playScreenFx，「播放螢幕特效(鏈動作)」）：塗地圖角落 1 格即可。參數：
   `effectId=1`（點欄旁「螢幕特效表」可查，1＝破幻術）、`duration=`（空＝預設 2.2 秒；要更快/更慢就填秒數）、`next=送去榕樹妖`。對話關閉後就地播幻境崩碎、收尾全白，播完自動接傳送。
3. `送去榕樹妖`（teleportTo，「直接傳送(鏈動作)」）：塗地圖角落 1 格即可。參數：
   `targetMapId=10`、`targetEntrance=`（空 = 落在目標圖 playerSpawn；要指定落點就在榕樹妖圖放 teleport 區填 entranceId，這裡填同名）。
4. 存檔後跑 `Tools/sync_map_assets.sh`（或 Project Tools → Sync Map Assets）把地圖帶進主遊戲。

### 步驟 4：測試

- 沒殺家人 → 碰紅嫁衣 → 對話 → 播完幻境崩碎（裂紋→碎塊崩落→全白）→ 自動傳送到榕樹妖 ✓
  （崩碎期間遊戲暫停、玩家不能動；全白收尾接上跨關載入頁應無縫）
- 先殺任一家人（Console 應出現「旗標 killedFamily = 1」）→ 碰紅嫁衣 → **毫無反應**（requireFlag 擋掉）✓
- 存檔重開後旗標仍在（progress.flags）✓

### 留給作者的設計決定（實作時再問）

- **殺了家人的分支**要做什麼？（另一段對話 `requireFlag=killedFamily`？直接開打？）目前規格只定了「沒殺」分支。
- `killedFamily` 是「殺**任一**家人」就成立（目前設計）；若要「全殺才算」需要另做計數，不在本期。
- 榕樹妖打完之後的流程（回廣場？MarkModuleCleared？）未定義。
