# 血統系統（Bloodline）

返回 [文件總覽](README.md)

> **一句話**：血統分「系列」，一個系列三階；喝**系列起始藥劑**決定本世走哪一系列（不可逆），
> 之後喝**全系列通用的進階藥劑**逐階往上；輪迴後回到人類重來。
>
> ⚠ **表B 五個屬性裡，目前只有 `WalkSpeed` 真的會套用**（2026-09-09 接上，見 §4）。
> 力量／敏捷／魔力／體力仍然只存不套用——遊戲裡還沒有角色屬性系統。所以換血統在體感上
> **會變的是外型、對話立繪與移動速度**，戰力不變，這是預期行為、不是壞了。
> ⚠ `WalkSpeed` **留空或 ≤0 ＝不套用**，維持 `PlayerController` 的 Inspector 值（預設 5）。

---

## 1. 玩法規則（拍板）

| 規則 | 內容 |
|---|---|
| 系列 | 一個系列 = 三個階段。目前有五個系列：殭屍系列 **殭屍 → 毛殭 → 旱魃**、血族系列 **覓血者 → 血伯爵 → 該隱**、狂族系列 **狼人 → 望月者 → 芬里爾**、土裔系列 **石像鬼 → 山嶽巨人 → 泰坦**、靈根系列 **築基期 → 元嬰期 → 化神期**（英文：血族系列 **Bloodborn**、第一階 **Bloodseeker**（也是外型資料夾名）；狂族系列 **Feralborn**、三階 `Werewolf`／`Moonwatcher`／`Fenrir`（2026-08-27 加）。2026-08-27 前叫「夜裔系列／夜裔／Nightborn」，PROBLEMS／PROGRESS 歷史條目裡的 Nightborn 就是它；土裔系列 **Gaiaborn**、三階 `Gargoyle`／`MountainGiant`／`Titan`（2026-09-09 加）；靈根系列 **SpiritRoot**、三階 `Foundation`／`Nascent Soul`／`Divine Form`（2026-09-10 加，後兩個資料夾名含空白）。⚠ **靈根系列在 2026-09-10 當天曾短暫叫「靈脈／SpiritVein」**，同日全面改名為靈根／SpiritRoot；資料夾、icon、兩張表與文件都已一併更新，專案裡不該再有 SpiritVein 這個字） |
| 起點 | 所有角色從「人類」（血統 Id 1、外型 Base）開始 |
| 選系列 | 喝**系列起始藥劑**（例：血統藥劑・殭屍）→ 變成那瓶藥劑指定的血統。**本世只能選一次，不可逆，不能改吃別的系列** |
| 直達 | 起始藥劑指定的**不一定是第一階**（2026-09-10 起）：`BloodlineID` 指到第 2／3 階的藥劑＝**跳過前面階段直接變成那一階**（例「血統藥劑・旱魃」一口變旱魃）。仍然算「選系列」，所以一樣一世一次、喝完就定型 |
| 升階 | 喝**血統進階藥劑・中階/高階**→ 沿目前系列往上一階。**全系列通用**（不是每個系列各做三瓶） |
| 逐階 | 必須一階一階走。還在第 1 階的人喝高階藥劑會被擋下，並告訴他要先進階為「毛殭」 |
| 不可退 | 已在第 2 階的人喝中階藥劑會被擋（不會退化） |
| 輪迴 | 轉生後**血統歸零回人類**。藥劑本身是道具，可以被輪迴帶物選中留到下一世再喝 |

### ⚠ 已知的節奏隱憂（待關卡內容變多後再調）

輪迴不保留血統 + 三階段各要一瓶藥 ⇒ **每個周目都得重新從人類爬到旱魃，而一個周目只玩 7 關**。
如果三瓶在 7 關內湊不齊，旱魃實質上玩家很難看到。現在刻意不處理（關卡池只有一關，調了也沒意義），
等關卡多起來再決定是調抽選權重、還是把進階藥劑改成關卡獎勵。

---

## 2. 兩張表

### 表A `Assets/Data/BloodlineSeriesTable.csv` — 系列 → 三階段

```
SeriesId,Key,DisplayName,Stage1Id,Stage2Id,Stage3Id,Note
1,Jiangshi,殭屍,10,11,12,殭屍系列：殭屍 → 毛殭 → 旱魃
2,Bloodborn,血族,20,21,22,血族系列：覓血者 → 血伯爵 → 該隱
3,Feralborn,狂族,30,31,32,狂族系列：狼人 → 望月者 → 芬里爾
4,Gaiaborn,土裔,40,41,42,土裔系列：石像鬼 → 山嶽巨人 → 泰坦
5,SpiritRoot,靈根,50,51,52,靈根系列：築基期 → 元嬰期 → 化神期
```

這是「系列 ↔ 階段 ↔ 血統 Id」的**唯一真相**。`BloodlineSeriesTable.cs` 載入時順便建一份
「血統 Id → (系列, 第幾階)」的反查索引，`TryLocate()` 就是查它。

**血統 Id 慣例**：一個系列吃一個十位段（殭屍 10~12、血族 20~22、狂族 30~32、土裔 40~42、靈根 50~52、下一個系列 60~62…），**Id 1 保留給人類**。

### 表B `Assets/Data/BloodlineTable.csv` — 每種血統的外型與屬性

```
Id,Key,DisplayName,SpriteFolder,BodyScale,WalkSpeed,Strength,Agility,Magic,Vitality,SkillId,Note
1,Human,人類,Base,1,5,5,5,1,1,,初始外型（最基礎數值）
10,Jiangshi,殭屍,Jiangshi,1,10,20,10,5,20,,殭屍系列 第一階
11,Maojiang,毛殭,Maojiang,1.5,10,40,15,15,40,,殭屍系列 第二階
12,Hanba,旱魃,Hanba,1.2,10,60,25,30,60,,殭屍系列 第三階
20,Bloodseeker,覓血者,Bloodseeker,1,10,15,25,20,15,,血族系列 第一階
21,CrimsonCount,血伯爵,Crimson Count,1,10,35,40,40,35,,血族系列 第二階
22,Cain,該隱,Cain,1,10,60,55,60,55,,血族系列 第三階
30,Werewolf,狼人,Werewolf,1,10,25,15,5,20,,狂族系列 第一階
31,Moonwatcher,望月者,Moonwatcher,1.3,10,45,25,10,45,,狂族系列 第二階
32,Fenrir,芬里爾,Fenrir,1.5,10,70,40,15,70,,狂族系列 第三階
40,Gargoyle,石像鬼,Gargoyle,1,10,25,8,10,25,,土裔系列 第一階
41,MountainGiant,山嶽巨人,MountainGiant,1,10,50,12,15,50,,土裔系列 第二階
42,Titan,泰坦,Titan,1,10,80,20,25,80,,土裔系列 第三階
50,Foundation,築基期,SpiritRoot/Foundation,1,,15,20,30,15,,靈根系列 第一階
51,NascentSoul,元嬰期,SpiritRoot/Nascent Soul,1,,25,30,55,30,,靈根系列 第二階
52,DivineForm,化神期,SpiritRoot/Divine Form,1,,40,45,85,50,,靈根系列 第三階
```

⚠ **`SpriteFolder` 允許含空白**（`Crimson Count`）。catalog 存的是相對路徑、載圖走 `File.ReadAllBytes`，
`PlayerSpriteLibrary` 的鍵只 `Trim()` 前後空白，中間的空白會原樣保留並正確比對——所以 CSV 照資料夾實際名稱填即可，
**不要自作主張把空白去掉**（去掉就對不上資料夾了）。

### `BodyScale`（體型倍率，以人類 Base 為 1）

**純視覺**：只改「角色圖畫多大」（`PlayerController.CharacterWorldHeight × BodyScale` 交給
`PlayerAnimator.Setup` 換算每張圖的縮放），**不動碰撞框、不動任何數值**。留空/≤0 = 1，程式端夾在 0.2~5。

> ⚠ **碰撞框不會跟著變。** 玩家的 collider 是固定的，1.5 倍體型的角色打起來跟 1 倍一樣大。
> 目前是刻意的（動 hitbox 會改變手感），但如果之後體型差距拉大到影響判讀，要一起處理。

**放大是以「可見腳底」為錨點往上長**，不是置中放大。置中 pivot 會讓角色上下同時長，1.5 倍等於腳往下沉
快半格、看起來像陷進地板。實作是依體型倒推 sprite 的 pivot：
`pivotY = fy − (fy − 0.5) / BodyScale`（fy = 可見內容底緣佔畫布高的比例）。
**BodyScale = 1 時剛好回到 0.5**＝專案原本的置中 pivot，所以不放大的血統是位元級零影響。
好處之一：`YSortByFeet` 那套「用 transform.y 當腳底代理」的假設在任何體型下都還成立，遮蔽關係不會跑掉。

### 哪些東西會跟著體型走

| 對象 | 怎麼跟 |
|---|---|
| 角色圖 | `CharacterWorldHeight × BodyScale` → `PlayerAnimator.Setup` |
| 腳下影子 | `BlobShadow.Refresh()` |
| **佛光光環** | `GroundEffectManager.Spawn(..., radiusScale: BodyScale)`，**視覺與傷害一起**放大 |
| 集氣光圈 / 喝藥特效 | 生成當下讀角色高度（本來就會跟），位置改對齊可見身體中心 |
| 變身演出的煙霧／電弧／雷柱 | 取變身前後較大的倍率 |
| 提燈照明（`LightRadius`） | **刻意不跟**——照明範圍是「那盞燈」的屬性，不因拿燈的人變大就照得更遠 |
| 子彈大小／雷射寬度／環繞彈半徑 | **不跟**——那些是武器的屬性 |
| 擊退距離 | **刻意補償回去**（`HitReactionHandler.WidthScaleCompensation`）。它本來是「角色圖寬 × 百分比」，不補償的話 1.5 倍體型會被擊退 1.5 倍遠，BodyScale 就不再是純視覺 |

> 🧯 **會撐過體型變更的持續型效果集中在 `PlayerController.RefreshBodyScaledVisuals()`。
> 之後再加這類「持續掛在玩家身上」的效果，記得在那裡補一行**，否則它會停在舊尺寸。
> 相關的坑與通則見 [PROBLEMS.md](PROBLEMS.md) **E14**。

### 位置要用哪個座標

`transform.position` 是**畫布中心**，不是身體中心、也不是腳。體型放大後（腳底錨點）身體整個往上長，
釘在 transform 的東西會沉到小腿附近。也**不要自己讀 `SpriteRenderer.bounds`**——含不含透明留白
在這條「執行期 `Sprite.Create`」的管線上沒有保證。一律用 `PlayerController` 這三個：

| 屬性 | 用途 |
|---|---|
| `VisibleBodyHeight` | 目前姿勢下可見身體的高度（趴著時比站著矮） |
| `FeetWorldPos` | 腳下站的位置。雷擊、落點這類「要對準腳」的用它 |
| `BodyCenterWorldPos` | 可見身體中心。光環、煙霧這類「要罩住身體」的用它 |

這三個是 `PlayerAnimator` 在 `Setup` 時從縮放參數**解析算出來**的（不是量貼圖），所以每幀取用零成本、也不受
Unity 怎麼算 bounds 影響。

**為什麼需要這一欄**（引擎明明已經正規化過了）：`PlayerAnimator.Setup` 會依 idle 的**可見像素高度**
把每個血統縮放到同一個世界高度，所以四組素材量出來其實高度差不多——

| 血統 | 畫布 | 可見寬 | 可見高 |
|---|---|---|---|
| Base | 256×256 | 99 | 193 |
| 殭屍 | 256×256 | **65** | 174 |
| 毛殭 | 256×256 | **106** | 197 |
| 旱魃 | 256×256 | 91 | 175 |
| 覓血者 | 256×256 | 78 | 174 |
| 血伯爵 | 256×256 | 91 | **157** |
| 該隱 | 256×256 | 73 | **138** |
| 狼人 | 256×256 | 74 | **204** |
| 望月者 | 256×256 | 128 | 186 |
| 芬里爾 | 256×256 | 138 | 177 |

但**跨血統的正規化只看高度、不看體積與姿勢**（同一血統內的 walk／attack 則另外用「體積尺度」對齊自己的 idle，
見 [CHARACTER_SETUP.md](CHARACTER_SETUP.md) 顯示高度段、[PROBLEMS.md](PROBLEMS.md) **G7**——那是修「一出手就長大」的，
跟這一欄要解的「不同血統看起來大小不一」是兩件事）：殭屍那張是寬站姿的駝背剪影（可見寬 65 是因為身體側收，但輪廓張得開），
毛殭與旱魃是挺直的瘦長站姿——同樣高度下後兩者看起來就是小一號、也比較難從暗地板上讀出來。

⚠ **血族系列的可見高一階比一階矮**（174 → 157 → **138**），該隱只有 Base 的 71%。正規化是「把可見高拉成同一個世界高度」，
所以該隱那組圖會被**放大約 1.4 倍**才畫出來（其他血統約 1.0~1.1）——像素密度最低、邊緣最容易糊，而且**放得越大、`BodyScale` 再往上加就越糊**。
這三個的 `BodyScale` 目前一律先填 1，實機看過再調（改 CSV 即時生效）。
`BodyScale` 就是拿來用眼睛校正這個落差的，**調到看順眼為止即可，不必追求數學上的等高**。

變身演出的煙霧、環繞電弧與雷柱粗細都會自動跟著這個倍率放大（取變身前後**較大**的那一個），不必另外調。
影子也會在換血統時重新量（`BlobShadow.Refresh()`）。

**表B 刻意不存 SeriesId 與 Stage。** 兩張表都寫隸屬關係就會對不上，所以表B 只回答
「這個血統長什麼樣、數值多少」，「它是誰的第幾階」一律問表A。

**`SpriteFolder` 一欄同時決定兩件事**（所以兩邊資料夾必須同名）：

- 動作序列圖 `GameAssets/Main/Characters/SequenceImage/<SpriteFolder>/{idle,walk,dead,attack}/`
- 對話立繪 `GameAssets/Main/Characters/Talk/<SpriteFolder>/<情緒>.png`

> **`SpriteFolder` 是一段「相對路徑」，不是單一資料夾名**（2026-09-09 起）。兩個資料夾都依**系列**分了一層，
> 所以現在填的是 `Feralborn/Werewolf` 這種帶斜線的值；只有人類 `Base` 還在根層（它不屬於任何系列）。
> 系列資料夾名 ＝ 表A 的 `Key`，所以殭屍系列會出現 `Jiangshi/Jiangshi`（系列與第一階同名），這是刻意的——
> 規則統一成「資料夾名就是表A 的 Key」，加系列時不用想。
>
> 為什麼這樣改不會壞：**四處掃描器（`MapAssetSyncTool.cs`、`MapIO.cs`、`Tools/sync_map_assets.sh` 的序列圖／立繪掃描）
> 本來就是遞迴的**（掃到「直接含 PNG 的葉資料夾」為止），而 `PlayerSpriteLibrary` 的鍵是
> `Characters/SequenceImage/` 之後的**整段**尾巴、`DramaTalkDatabase` 是字串串接 —— 所以中間插幾層都對得上。
> **唯一要配合改的是影子錨點工具**（它原本寫死兩層），見 [PROBLEMS.md](PROBLEMS.md) **C12**。

> 🧯 **加了新的血統資料夾，一定要跑 `Project Tools → Sync Map Assets`。**
> 這兩條路都是走 catalog（StreamingAssets），沒同步的話執行期一張圖都載不到，
> 角色會變成**只剩影子**，Console 會有 `[PlayerAnimator] 血統「X」找不到任何外型圖`。

### 舊版三個數值欄已移除

表B 原本有 `MaxHpAdd` / `MoveSpeedMul` / `OutgoingDamageBonusPercent` 三個**會真的生效**的欄位，
那是屬性系統還沒有時的權宜概念。2026-08-18 加入五屬性時一併移除，理由是「行走速度 vs MoveSpeedMul」
概念重疊，留著會在真正的屬性系統做好時變成兩套來源打架。
連帶也解掉了一個既有 bug：`ReviveFull()` 會呼叫 `CombatStats.Init()` 把最大生命打回 Inspector 基礎值，
而 `BloodlineSystem` 因為 `_appliedId` 沒變不會重套 → **死一次回廣場後血統的 HP 修正就消失**。
現在血統不碰數值，這個坑自然不存在；未來接屬性系統時要記得這件事。

---

## 3. 道具

`ItemTable.csv` 用**兩個互斥的欄位**區分兩種藥劑（新增的 `BloodlineUpgrade` 是第 18 欄 / index 17）：

| 欄位 | 意義 |
|---|---|
| `BloodlineID` | **系列起始藥劑**：指到 BloodlineTable 的 Id。**指哪一階都可以**——第一階＝一般的選系列藥劑；第 2／3 階＝**直達藥劑**，跳過前面階段一口變成那一階（2026-09-10 起） |
| `BloodlineUpgrade` | **進階藥劑**：目標階數（2 = 中階、3 = 高階）。不指定血統，實際變成什麼由表A 決定 |

目前十七瓶。**ID 編號規則**：`30x` ＝各系列第一階、`32x` ＝第二階直達、`33x` ＝第三階直達，
末位固定是系列序（1 殭屍／2 血族／3 狂族／4 土裔／5 靈根）；`310`／`311` 是兩瓶全系列通用的進階藥劑。

| 系列 | 第一階（30x） | 第二階直達（32x） | 第三階直達（33x） |
|---|---|---|---|
| 殭屍 | 301 1階殭屍藥劑 `=10` | 321 2階殭屍藥劑 `=11` | 331 3階殭屍藥劑 `=12` |
| 血族 | 302 1階血族藥劑 `=20` | 322 2階血族藥劑 `=21` | 332 3階血族藥劑 `=22` |
| 狂族 | 303 1階狂族藥劑 `=30` | 323 2階狂族藥劑 `=31` | 333 3階狂族藥劑 `=32` |
| 土裔 | 304 1階土裔藥劑 `=40` | 324 2階土裔藥劑 `=41` | 334 3階土裔藥劑 `=42` |
| 靈根 | 305 1階靈根藥劑 `=50` | 325 2階靈根藥劑 `=51` | 335 3階靈根藥劑 `=52` |

> **命名與文案的規矩**（2026-09-10 作者拍板）：
> - **名稱寫「幾階＋系列」，不寫會變成什麼**：`<階>階<系列>藥劑`（例「2階殭屍藥劑」）。
>   系列名照表A 的 `DisplayName`。**不要用階段的血統名當藥劑名**（「血統藥劑・毛殭」那種寫法已作廢）。
> - **`TipStats` 只放形貌描述，不點名、不贅述**：「體生白毛，刀槍不入」——一句話帶過變化後的樣子，
>   讓玩家知道會變強、但不知道會變成誰。`TipLore` 同理，不寫血統名也不寫典故名。
>   **不要加「喝下後，」這種開頭，也不要在後面補「本世不可更改系列」**——喝的時候確認視窗本來就會講，
>   tip 裡再寫一次是贅字（2026-09-10 作者拿掉）。
> - **懸念保留到喝下去為止**：確認視窗用的是 `data.Name`（＝藥劑名，不洩漏），
>   而 `DoneText`（「血脈已定：{0}」）與立繪揭示面板用的是 `def.DisplayName`（＝真正的血統名）——
>   **揭曉點剛好落在變身演出結束的那一刻**，這是既有流程自然給的，不用特別做。

| ID | 名稱 | 欄位 | 取得 |
|---|---|---|---|
| 301~305 | 各系列第一階（`<1>階<系列>藥劑`） | `BloodlineID`＝該系列 Stage1Id | 血統祭壇（`BaseBloodRoll.csv`，300 元、不連抽，五瓶權重都是 10） |
| 321~325 / 331~335 | 第二／三階直達（`<2\|3>階<系列>藥劑`） | `BloodlineID`＝該階的血統 Id | ⏳ **還沒放進任何池**（等作者決定投放方式，見 §8） |
| 310 | 血統進階藥劑・中階 | `BloodlineUpgrade=2` | 道具祭壇（`BaseItemRoll.csv`，權重 3） |
| 311 | 血統進階藥劑・高階 | `BloodlineUpgrade=3` | 道具祭壇（`BaseItemRoll.csv`，權重 1） |

icon 都在 `Resources/UI/Icons/Items/positions/bloodline/`：一個系列共用一張圖
（`bloodline_Jiangshi`／`bloodline_Bloodborn`／`bloodline_Feralborn`／`bloodline_Gaiaborn`／`bloodline_SpiritRoot`，
該系列的三瓶都用它），另外兩瓶進階藥劑是 `bloodline_lvup_middle`（310）／`bloodline_lvup_high`（311）。

> **直達藥劑為什麼不用改程式**：`PlanStarter` 只做兩件事——查 `BloodlineID` 在不在表B、以及本世是否已定型；
> **它從來沒有檢查「必須是第一階」**（第一階只是填表的慣例，不是規則）。所以「指到第 3 階的起始藥劑」
> 天生就是合法的，語意剛好就是「跳過前面直接變成那一階」。
>
> ⚠ **喝完直達藥劑之後仍然要逐階**：喝了 321（毛殭，第 2 階）的人，`PlanUpgrade` 查到目前是第 2 階，
> 再喝 310（中階）會被擋（「已在此之上」），喝 311（高階）才過。這是既有規則自然延伸出來的，沒有特例。

**為什麼進階藥劑放道具池不放血統池**：血統池賣的是「選一個系列」（一世一次的重大決定），
進階是之後的成長消耗品，兩件事分開。高階刻意比中階稀有——血統必須逐階喝，抽到高階但還在第一階
是會卡著的，別讓它太常出現。

`ItemData` 的三個判斷：`IsBloodlineStarter`（有 BloodlineID）、`IsBloodlineUpgrade`（有 BloodlineUpgrade）、
`IsBloodline`（前兩者任一，UI 用這個決定要不要走喝藥流程）。

---

> ⚠ **喝的方式只有一種：在背包裡對藥劑按滑鼠右鍵**（會先跳確認視窗）。
> **左鍵完全沒有動作**——2026-08-19 之前左鍵也會喝，那是誤點就定終身的地雷，已移除。
> 全遊戲的「左鍵搬移／右鍵使用」鐵則見 [INVENTORY.md](INVENTORY.md)，
> 使用的唯一入口是 `Inventory/ItemUse.cs`（血統只是它的其中一個分支）。

---

## 4. 程式結構

```
BloodlineSeriesTable.cs   表A + 反查索引（血統 Id → 系列/階段）
BloodlineTable.cs         表B（外型 + 體型倍率 + 五屬性）
BloodlineSystem.cs        執行期系統：查詢、Plan/TryDrink、套用外型與體型
BloodlineTransformFx.cs   變身演出（§5）＋ 協程宿主 BloodlineTransformFxRunner
GachaTableProvider.cs     兩張表的 TextAsset 由它持有（場景 GameManagers 上）
```

### 唯一入口：`Plan` → `TryDrink`

UI **刻意不懂任何血統規則**。`InventoryPanel` 只做三件事，全部的判斷都在 `BloodlineSystem`：

```csharp
var plan = BloodlineSystem.Plan(itemId);   // 不改變任何狀態
if (!plan.Ok) { AlertPanel.Toast(plan.Reason); return; }   // ← 右鍵當下就擋下並說明
ConfirmPopup.Show(plan.ConfirmText, () => {
    BloodlineSystem.TryDrink(itemId, out string message);   // 成功/失敗的訊息都由它給
    AlertPanel.Toast(message);
});
```

`DrinkPlan` 帶四樣東西：`Ok` / `Reason`（不能喝的說明）/ `ConfirmText` / `DoneText`。
以後改規則、加系列、改文案都只動 `BloodlineSystem`，UI 一行不用改。

> ⚠ **`TryDrink` 內部會自己重新 `Plan` 一次**，不信任 UI 幾秒前算好的結果——
> 確認視窗開著的期間狀態可能變了（東西被搬進倉庫、被別的路徑喝掉）。
> 也因此 **`TryDrink` 的 `out string message` 由它負責填**，呼叫端直接 Toast 就好，
> **不要自己記住文案**——那份會過期。

> ⚠ **成功時刻意不跳 Toast**（2026-09-10 作者要求移除「血脈已定：xxx」）：喝完緊接著就是
> 變身演出 ＋ 立繪揭示面板，那個面板整整四秒都在講「你變成什麼了」，再跳一則 Toast 是重複、
> 而且會壓在演出上面。實作是讓 `TryDrink` **成功時把 message 填成空字串**——
> 照 `ItemUse` 既有的約定「message 為空 ＝ 這次使用不需要對玩家說話」（回血藥本來就是這樣），
> 所以三個呼叫端一行都不用改。
> `DrinkPlan.DoneText` 仍然算出來、語言表 2013/2014 也還在，只是目前沒有人顯示它；要恢復就在呼叫端 Toast 它。
>
> ⚠ **失敗的訊息一定要留**——「不能喝的時候要在按鍵當下就擋下並說明理由」是明確要求的體驗（見下）。

> ⚠ **不能喝的時候一定要在按鍵當下就擋下並說明理由**，不要先跳確認視窗、按完才發現沒反應。
> 這是明確要求的體驗，`Plan` 的 `Reason` 就是為此存在。

### 收斂式套用

`BloodlineSystem` 是常駐單例、自動生成、零接線，每幀比對「存檔裡的血統」與「已套用的血統」，
不一致才動作——所以不管存檔載入、換圖、玩家物件重建的順序如何，最後都會收斂到正確狀態。

`ApplyTo()` 做兩件事：**必要時 `pc.SetBloodline(def.SpriteFolder, def.BodyScale)`**，
然後 **`pc.SetMoveSpeed(def.WalkSpeed)`**（留空／≤0 就跳過）。其餘四個屬性刻意什麼都不做（見開頭的警告）；
技能只印一則 log（技能系統不存在）。

> ⚠ **換速度一定要走 `PlayerController.SetMoveSpeed()`，不能直接寫 `pc.MoveSpeed`。**
> 「正常走 ＝ 走路動畫 1 倍速」的基準有**兩份**，都是初始化時抄走 `MoveSpeed` 的：
> `PlayerAnimator.ReferenceSpeed`（路線 B 的逐格動畫）與 `AnimatorSpeedByVelocity.ReferenceSpeed`
> （舊 Animator 那條，目前停用但元件還在）。只改速度不改基準 ⇒ 速度兩倍、走路動畫也跟著播兩倍速。
> `SetMoveSpeed` 刻意**不重跑** `PlayerAnimator.Setup`——那支會重載圖、重算縮放並把 sprite 打回 idle 第 0 幀，
> 在變身演出正趴著的時候呼叫就是 §5 的坑 2。基準是 public 欄位，直接設就好。

> 🧯 **接魔力／體力之前要先解掉一個坑**：`ReviveFull()` 會呼叫 `CombatStats.Init()`
> 把血魔上限打回 Inspector 基礎值，而 `BloodlineSystem` 因為 `_appliedId` 沒變不會重套
> ⇒ **死一次回廣場，血統的上限修正就消失**。舊版血統加 HP 就是死在這裡（見下方「舊版三個數值欄已移除」）。

> ⚠ 「必要時」的判斷**必須同時比對外型資料夾與體型倍率**
> （`pc.Bloodline != def.SpriteFolder || !Mathf.Approximately(pc.BodyScale, def.BodyScale)`）。
> 只比資料夾名的話，調完 CSV 的 `BodyScale` 重新載入會發現沒反應。

### 存檔

血統存在 `progress.flags["血統"]`，值 = 血統 Id 的字串（用 `SaveManager.GetFlagValue` 讀，不是 `GetFlag`）。

**刻意放周目層而不是 `lifetimeFlags`**：`ReincarnateInPlace` 會整個換掉 `progress`，
所以輪迴後自動回到人類，**不需要任何額外的重置程式碼**。存檔格式零改動。

**舊存檔救生艇**：存的血統 Id 在表B 找不到時（血統表刪過列——2026-08-18 移除了野魂 2／幽靈 3），
`CurrentBloodlineId` 與 `IsFixedThisCycle` 一律**當成未定型**，玩家可以重新選一次。
若照實回報「已定型成一個不存在的血統」，起始藥劑會被擋（而且訊息會變成自相矛盾的「你的血脈已定為『人類』」）、
進階藥劑也會被擋（找不到所屬系列），本世血統徹底卡死只能靠輪迴。

---

## 5. 變身表演（世界演出 → 立繪揭示）

喝下藥劑之後是**兩段接力的表演**，中間沒有間隙：

1. **世界演出**（`BloodlineTransformFx.Play()`）：倒下 → 天雷 → 煙霧裡換外型 → 爬起。約 6 秒。
2. **立繪揭示**（`BloodlineIntroPanel`）：舊立繪斑駁剝落 → 新立繪浮現 ＋ 血統名。約 4 秒。

`BloodlineTransformFx.Play()` → 自建一個協程宿主 `BloodlineTransformFxRunner`（照 `LevelExitManjiController` 的樣板，跑完自毀）。

### ⚠ 全程遊戲是暫停的（`timeScale = 0`）

**這是刻意的，而且是整段表演最重要的一個前提。** 表演期間玩家被鎖住不能閃避，
不凍住世界的話怪物會照打、玩家可能在變身途中被打死（那會變成「屍體爬起來」，
而且死亡流程和演出的輸入鎖會打架）。

代價是**每一個計時器都必須換成 unscaled**。漏掉任何一項，那一項就會整段凍在第一格：

| 元件 | 開關 |
|---|---|
| 玩家倒下／趴地／爬起 | `PlayerAnimator.UnscaledPose`（只影響姿勢表演，走路待機仍吃 `Time.deltaTime`） |
| 煙塵、環繞電弧 | `VfxInstance.Unscaled` |
| 拼接雷柱 | `SegmentedLightningColumn.Unscaled` |
| 演出協程的等待 | `BloodlineTransformFxRunner.Wait()`（直接寫死 unscaled） |
| 立繪面板 | 整支面板本來就全 unscaled（同 `BossIntroPanel` 慣例） |
| 螢幕震動、白閃 | **本來就是 unscaled**，不用動 |

這幾個 `Unscaled` 旗標**預設都是 false**，所以一般戰鬥特效的行為完全沒變。

> 2026-08-19 之前是反過來的：`SetExternalHold` 的 pause 傳 `false`、全部吃 `Time.deltaTime`。
> 那時的註解會告訴你「pause 必須是 false」——那句話現在是錯的，見 [PROBLEMS.md](PROBLEMS.md) **D15**。

### 橫跨兩段的輸入鎖

世界演出與立繪面板**各自也會鎖自己那一段**，但真正保證「中間不會有一幀鬆手」的是
`BloodlineSystem` 掛的具名 hold `"BloodlinePerformance"`——從 `TryDrink` 成功的那一刻，
一路壓到立繪面板淡出結束為止。

為什麼需要這一層：世界演出的 `finally` 是**先 `ReleaseHold()` 再回呼**，而面板要延一幀才開得起來
（避免 `OnClose` 重入，見 PROBLEMS D8）。中間那一兩幀若沒人壓著，`timeScale` 會彈回 1、
玩家可動、怪物動一下——會看得出來卡一格。

### 時間軸：世界演出（約 6 秒）

| 時間 | 事件 |
|---|---|
| 0.00s | 關掉所有面板 → 掛輸入鎖 **＋ 暫停遊戲**（之後全程 unscaled 計時） |
| 0.00s | 玩家 `dead` **正向**播放，倒下（25 幀 @12fps ≈ 2.08s） |
| 2.08s | 趴地定格；雷柱從畫面外生成 |
| 2.20s | **擊中**：螢幕震動 0.25s + 白閃（0.05 進 / 0.20 退） |
| 2.25s | 煙塵爆開（放大到玩家身高 ×1.6）+ 環繞電弧亮起 |
| 2.58s | 煙最濃 → **換成新血統，仍趴著** |
| 2.95s | 煙塵播完消散，露出新外貌 |
| 2.95s | 電弧殘留繼續繞 0.60s |
| 3.75s | 倒播 `dead`，爬起來（≈ 2.08s） |
| 5.83s | 世界演出結束 → **交棒給立繪揭示面板**（暫停不解除） |

**節奏與外觀常數全部集中在 `BloodlineTransformFxRunner` 檔頭**，要調快慢改那幾個數字就好。

### 素材

| 用途 | 位置 | 幀數 | 來源包 |
|---|---|---|---|
| 雷柱（**整根都用這個**，tileable） | `Resources/VfxEffects/TransformLightning/Loop/Loop_01~08` | 8 | Super Pixel Fantasy FX Pack 2 |
| ~~雷柱頂端雷首~~（**未使用**，留著備用） | `Resources/VfxEffects/TransformLightning/Start/Start_01~02` | 2 | 同上 |
| 煙塵 | `Resources/VfxEffects/TransformSmoke/`（VfxTable **30**） | 10 | Smoke Bursts |
| 環繞電弧 | `Resources/VfxEffects/TransformAura/`（VfxTable **31**） | 22 | Super Pixel Effects Pack 3 |

原始素材包保留在 `DipanProj_Main/血統特效/`（**在 Assets 外面**，不會被 Unity 匯入）。

**雷柱三段素材一個都沒用到頂端，整根都是 `loop`**（2026-08-18 實測後改的）：

- `end` 不用：實測它與 `start` 是**同兩張圖反過來**，而且是「快消散的細電光」。既有的九霄雷獄早就註記過
  「end 會突然收細」。
- `start`（雷首）**也不用**——這是實機看過才發現的。量出來的數字說明一切：

  | | 不透明像素 | **接縫處的邊緣寬度** |
  |---|---|---|
  | `Start` | 193 / 428 | **1~2 px** |
  | `Loop` | 1262~2039 | **5~17 px** |

  一根 1~2px 的髮絲接在 5~17px 的粗電柱上頭，接縫非常明顯。**還有第二個原因**：`capFrame` 的算式是
  `floor(elapsed × fps × 0.35)`，雷首只有 2 張、約 0.15 秒就播到底然後**整段凍住**，
  底下的 loop 卻在跑 8 幀循環——變成一根靜止的髮絲蓋在閃爍的柱子上。

  純 loop 沒有這兩個問題：loop 本身**上下貫穿、可平鋪**，疊起來零接縫；而柱頂本來就延伸到畫面外
  （`topY` 取視窗上緣再往上 12%），所以也不會看到「斷頭」。

  素材都還留在 `Resources/VfxEffects/TransformLightning/Start/`，想試回來只要把 `LightningStyle`
  的第一個參數填回路徑、第三個參數填 2 即可。**九霄雷獄維持原樣**（它節奏短、雷首多半落在畫面外）。

排序層：雷柱 22000 < 電弧 22050 < 煙塵 22100（煙要蓋在最上面，才藏得住換裝那一刻）。⚠ 都必須 ≤ 32767，見 PROBLEMS E4。

### 尺寸與定位

- **一切尺寸都乘上 `max(變身前 BodyScale, 變身後 BodyScale)`**。只用變身前的話，換成更大的血統時，
  煙霧散開前那一段會露出新外型的頭尾。
- 煙霧與電弧的覆蓋高度 = `max(目前實際畫出來的高度, 站立高度 × 體型倍率)`；
  前者反映當下姿勢（趴著時比較矮），後者保證換成更大的血統仍蓋得住。
- **位置一律走 §2「位置要用哪個座標」那三個屬性**：雷擊點用 `PlayerController.FeetWorldPos`
  （對準腳），煙霧與電弧用 `BodyCenterWorldPos`（罩住身體），覆蓋高度用 `VisibleBodyHeight`。
  ⚠ **不要用 `transform.position`**（那是畫布中心，拿它當擊中點電柱會停在胸口／肩膀高度，實測症狀），
  也**不要自己讀 `SpriteRenderer.bounds`**（含不含透明留白沒有保證）。詳見 [PROBLEMS.md](PROBLEMS.md) **E14**。

### 重用的既有機制（都是這次順便補上的通用能力）

| 能力 | 位置 | 說明 |
|---|---|---|
| 拼接雷柱 | `SegmentedLightningColumn.Spawn(impact, cam, Style, …)` | 原本只給九霄雷獄用、素材路徑寫死；改成可指定 `Style`（路徑＋張數＋排序層），快取以路徑為鍵。**舊簽章保留，武器行為零改變**。 |
| 倒下動畫 | `PlayerAnimator.PlayFallDown(onDone, fpsMul)` | `PlayWakeUp` 的鏡像：正播 `dead`，**播完轉成趴地定格**（不是回 Idle）。 |
| 重定趴姿 | `PlayerAnimator.RefreshLyingPose()` | 換裝後必叫，見下面的坑 2。 |
| 中止表演 | `PlayerAnimator.CancelPose()` | 演出被外力打斷時解掉趴姿，否則角色永遠定格。 |
| 螢幕震動 | `MapCameraController.AddShake(秒, 振幅)` / `StopShake()` | **必須做在 MapCameraController 裡**——相機位置每幀由它獨佔寫入，另外掛震屏元件會互相蓋掉。偏移在 `LateUpdate` 最末端、`SmoothDamp` 之後才疊（先疊會被平滑吃掉，變成軟軟地飄一下）。 |
| 全螢幕閃光 | `ScreenFader.Flash(color, in, out)` / `ClearFlash()` | 與黑幕分開的獨立 Image + CanvasGroup，永不擋點擊。 |
| 具名輸入鎖 | `UIManager.SetExternalHold(owner, block, pause)` | 見 PROBLEMS **D13**。舊的兩參數多載共用一個預設 key，**新程式一律帶 owner**。 |
| 影子重量 | `BlobShadow.Refresh()` | 影子只在 `Start` 量一次；換外型／改體型後要重量，否則會停在舊尺寸。 |
| 地面特效半徑倍率 | `GroundEffectManager.Spawn(…, radiusScale)` / `GroundEffectInstance.SetRadiusScale()` | **視覺與傷害一起**縮放（`visualScale` 只縮視覺，畫面會騙人）。見 [GROUND_EFFECT.md](GROUND_EFFECT.md)。 |
| 暫停中仍會動的特效 | `VfxInstance.Unscaled` / `SegmentedLightningColumn.Unscaled` | 兩個都預設 `false`＝行為零改變。`Spawn` 都會回傳實體，生出來直接設旗標即可，不用改任何簽章。 |
| 暫停中仍會動的姿勢動畫 | `PlayerAnimator.UnscaledPose` | 只影響倒下／趴地／爬起三段；走路待機仍吃遊戲時間（暫停時本來就該停）。 |
| ESC 不會插進演出 | `UIManager` 的 ESC 分支加 `!_inputBlocked` | 沒有入堆疊視窗、但輸入被非面板系統鎖住時（過場／教學／演出），ESC 不再開設定面板。**這是全域行為，也保護了 `BossIntroPanel` 與各種過場。** |

### ⚠ 四個踩過的坑（改這段演出前必讀）

1. **暫停播放 ⇒ 每一個計時器都必須是 unscaled。** 見上面那張表。開頭仍然要 `CloseAll()`——背包若開著會整片蓋在演出上面。
2. **換裝會把趴姿打回站姿。** `SetBloodline` 內部重跑 `PlayerAnimator.Setup`，把 sprite 換成新血統的 idle 第 0 幀；但趴地定格旗標還在、`Update` 直接 return 不再更新 → **角色站著定格**。所以 `onSwap` 之後一定要 `RefreshLyingPose()`。
3. **玩家可能在演出中被打死**（演出期間他是被鎖住不能閃避的）。`Alive()` 有查 `PlayerController.IsDead`，死了就中止；而且輸入鎖用**具名持有者**，不會和死亡流程互相清掉對方的鎖（PROBLEMS D13）。
4. **表演期間玩家還是按得到 `B`/`K`/`Y`，也按得到 `ESC`。** 那三個面板會整片蓋在表演上面。`StorageBagCoordinator` 查 **`BloodlineSystem.IsPerforming`**（＝世界演出 ∪ 立繪面板，兩段的單一真相）擋掉；`ESC` 則由 `UIManager` 那邊擋——沒有任何入堆疊的視窗開著、但輸入被非面板系統鎖住時，ESC 不再開設定面板（順便也保護了 `BossIntroPanel` 與各種過場）。⚠ 兩處都**不能**改查 `IsGameplayInputBlocked` 的原始值當熱鍵條件，背包開著時它本來就是 true，那樣 `B` 會關不掉背包（PROBLEMS D14）。

### 收尾：血統揭示面板 `BloodlineIntroPanel`

爬起來之後開的 UI 表演，讓玩家看清楚自己變成了什麼。**不可跳過**（刻意沒有任何按鍵捷徑）。

| 時間 | 事件 |
|---|---|
| 0.00s | 壓黑遮罩 ＋ 破碎框底版 ＋ 頂端標題「血統轉換」隨面板淡入，框內是**變身前**的血統立繪 |
| 1.00s | 舊立繪開始斑駁剝落（`_Cutoff` 0→1，暗紅燒蝕邊） |
| 1.12s | 新血統立繪同步從空白浮現（`_Cutoff` 1→0，**不同亂數種子**＝破法不一樣） |
| 1.25s | 姓名底版從下方飄上來（ease-out） |
| 1.85s | 血統名在牌匾上浮現（扭曲抖動 → 復原＋淡入，借 `NameWarpEffect`） |
| 2.65s | 停 1 秒讓玩家看清楚 |
| 3.65s | 自己淡出 0.4 秒 → **淡完才** Close → 才解除暫停 |

**資料來源全部是既有管線，沒有新的載圖程式**：

| 東西 | 來源 |
|---|---|
| 立繪 | `DramaTalkDatabase.ResolvePortrait("Actor_normal", 血統資料夾)`＝ Talk 立繪同一條 catalog 管線 |
| 血統名 | `BloodlineTable.NameOf(id)` |
| 破碎框底版 | `Resources/UI/BloodlinePanel/BloodlinePanel_Bg`（1024×1536） |
| 姓名底版 | `Resources/UI/BloodlinePanel/BloodlinePanel_NameBg`（866×288＝比例 3.007，**血統專用的淺色石碑**） |
| 頂端標題「血統轉換」 | **`Resources/UI/Texts/BloodlinePanel_Title`**（866×288，與石碑同比例） |

⚠ **標題不在 `UI/BloodlinePanel/`，在共用的 `UI/Texts/`。** 那是全專案「**圖片型文字**」的資料夾。
凡是「畫成圖的字」都放那裡；純美術的框、石碑、按鈕底不算（不隨語言變，留在各自的面板資料夾）。

實體檔案在 **`UI/Texts/tw/BloodlinePanel_Title`**（英文版之後放 `UI/Texts/en/`，**同名**）。
程式裡的 `標題圖` 欄位寫的是**邏輯路徑** `UI/Texts/BloodlinePanel_Title`，
由 `UIBuilder.LoadSprite` → `LocalizedArt.ResolveExisting` 換成當前語言，缺圖退回繁中。
見 [LOCALIZATION.md](LOCALIZATION.md) §圖片型文字。
| 毛筆字型 | `Fonts/Bakudai/Bakudai-Bold`（同 BossIntroPanel／GachaPanel／ForgingPanel） |

**斑駁溶解著色器** `Resources/Shaders/BloodlineDissolve.shader`：uGUI 材質，hash 值噪 2 個八度
（粗塊決定哪一片先掉、細粒讓邊緣毛躁），**不吃任何貼圖**。單一參數 `_Cutoff` 同時做正反兩個方向，
所以兩張立繪各掛一份材質、各推自己的數字就好，不需要 invert 開關。
著色器載不到時面板會**退化成整張圖的 alpha 淡入淡出**，表演節奏一模一樣。

⚠ **三個要知道的點**（改這支面板前必讀）：

1. **收尾淡出是面板自己做的，不是交給 `UIPanel` 的淡出。** `UIPanel.DoClose()` 是「先叫 `OnClose`、
   **再**開始淡出」，把解鎖掛在 `OnClose` 上的話，暫停會在畫面還有八成不透明度時就解除——
   玩家等於在一片幾乎全黑的遮罩後面被丟回戰場。所以流程是「自己淡到全透明 → 才 Close
   （此時 `FadeDuration` 回 0，立刻收）→ 才回呼」。見 [PROBLEMS.md](PROBLEMS.md) **D16**。
2. **立繪比例不保證一致。** `Base` 是 1122×1402（比例 0.80），殭屍三階與血族三階都是 1024×1536（0.667）。
   所以立繪一律「**等比縮到框內的 `PortraitBox` 並靠下對齊**」，不能照高度縮——照高度縮的話
   Base 會比破碎框還寬、直接撐出框外。哪天把 Base 重畫成 1024×1536 就能拿掉這個顧慮。
3. **兩張立繪各自一份 `Material`。** `_Cutoff` 是材質參數，共用一份的話兩張會一起溶解。
   材質是 instance 欄位 ＋ `HideAndDontSave`，隨面板生滅（關掉 Domain Reload 後 static 快取
   會拿到上一輪已銷毀的物件，見 PROBLEMS I 系）。
4. **姓名石碑是淺色的 ⇒ 字必須是深色。** `NameColor` 預設深血紅 `(0.30, 0.035, 0.035)`，
   **不是** `BossIntroPanel` 那個暖金色（那張牌匾是深色的）。連帶地，石碑圖萬一載不到，
   後備的純色底也刻意是**淺石色**而不是半透明黑——墊深色底的話字會整個看不見。
   字型仍然是全專案共用的毛筆字 `Fonts/Bakudai/Bakudai-Bold`。

**版面預設值**（1920×1080 參考解析度；都可在 Inspector 調）：

| 欄位 | 值 | 說明 |
|---|---|---|
| `FrameHeight` / `FrameY` | 880 / +30 | 破碎框（寬依原圖比例＝587） |
| `TitleW` | 370 | 標題（高依原圖比例＝123） |
| `TitleYFromFrameTop` | 78 | 標題中心距框**頂**邊 78px ⇒ 底邊落在距框頂 140px |
| `PortraitBox` | (0.78, 0.70) | 立繪等比縮到「框寬 ×0.78、框高 ×0.70」之內 |
| `PortraitBottomInset` | 0.13 | 立繪底邊距框底 ＝ 框高 ×0.13（114px）。石碑上緣在 165px，所以石碑會蓋住立繪最下面約 50px（與示意圖一致） |
| `PlateW` / `PlateH` | 360 / 120 | 石碑（維持 866:288 ＝ 3.007 的比例，改寬要同步改高） |
| `PlateYFromFrameBottom` | 105 | 石碑中心距框底 105px ⇒ 正好壓在框的下緣裡 |
| `NameArea` | (0.20, 0.22, 0.60, 0.56) | 字在石碑上的區域，避開左右尖刺與上下中央的裝飾 |
| `NameFontSize` | 56 | **上限**，不是固定值——字級會依字數夾成 `min(56, 字區寬 ÷ 字數)`（`FitFontSize`）。字區寬 ＝ `PlateW` × `NameArea.width` ＝ 216px ⇒ 三個字 56（零影響）、四個字 54、五個字 43。不夾的話第 4 個字會被 uGUI 的 `Wrap` 折到第二行（「山嶽巨人」踩到的，見 [PROBLEMS.md](PROBLEMS.md) **E31**） |

⚠ **標題和立繪在搶同一塊空間**：`立繪頭頂距框頂 = 框高 − PortraitBottomInset×框高 − 立繪高`，
而 `標題底邊距框頂 = TitleYFromFrameTop + 標題高 ÷ 2`。前者要大於後者，否則標題會壓在角色頭上。
目前算下來是 **150 vs 140，留 10 的餘裕**——調大 `PortraitBox.y` 或調小 `TitleYFromFrameTop` 之前先算一下。

實際算過：殭屍／毛殭 411×616（頭頂距框頂 150），Base 458×572（距框頂 194，因為它比較寬、被寬度卡住）。

**調表演**：所有節奏／版面／溶解參數都是 `public` 欄位。Play 模式中在 Hierarchy 選
`[UIManager] → Layer_Overlay → BloodlineIntroPanel`（第一次播過後才存在）即可即時調，
重新喝一次藥立刻套用。⚠ Play 模式調的值退出後不會保存，調到滿意要回填程式碼的預設值。

### 保險絲

- `WaitPose` 等表演結束的三個出口：完成 / `IsWakeUpBusy` 變 false（表演被打斷）/ 逾時 6 秒。逾時計時用 **unscaled**，否則 timeScale=0 時連保險絲都凍住。
- `BloodlineSystem.TransformTimeout = 30f`：任一段漏叫回呼時強制解除收斂鎖**與 external hold**（後者更要緊：沒放的話玩家整場不能動而且沒有任何錯誤訊息）。正常世界演出約 6 秒 ＋ 立繪面板約 4 秒，最壞（倒下/爬起各逾時 6 秒）約 22 秒，所以 30 秒。**之後若把整段加長到 25 秒以上，這裡要一起調大。**
- `BloodlineTransformFxRunner.IsPlaying` 與 `BloodlineIntroPanel.IsShowing` 都是 static，**兩個都已註冊 `PlayModeStaticReset`**——殘留會讓下一次 Play 的背包熱鍵全部按不出來。
- `BloodlineSystem.FinishPerformance()` 是 **idempotent** 的（正常路徑一次、保險絲可能再一次）。
- 立繪面板的 `OnDestroy` 也會保底放行回呼（退出 Play／被外力銷毀時走不到 `OnClose`）。

## 5b. 第三階的「神格特效」（2026-09-10）

四個第三階（旱魃／該隱／芬里爾／泰坦）設定上是神明一般的存在，所以多了三層常駐視覺。
**三層各自獨立、由表決定、留空就整層不存在**——所以這一節講的東西對其他九個血統是完全不生效的。

### 三欄開關（表B 尾端五欄）

| 欄位 | 作用 | 留空 |
|---|---|---|
| `AuraVfxId` | 環繞全身的特效，填 `VfxTable` 的 id | 沒有這一層 |
| `HaloStyle` | 背後圓盤樣式：`Disc` / `Disk` / `Cracked` / `Crescent` / `Stone` | 沒有這一層 |
| `HaloColor` | 圓盤顏色，六位 hex（可省略 `#`） | 白 |
| `TrailStyle` | 移動足跡：`Fade`（角色虛影）/ `Step`（腳下放特效，底部貼地）/ `Ground`（腳下放特效，中心對齊） | 沒有這一層 |
| `TrailColor` | 染色。用素材原色就留空 | 白＝不染色 |
| `TrailVfxId` | `Step`／`Ground` 要放的 VfxTable id。`Fade` 不吃這一欄 | 那一層不會有表現（會印警告） |
| `OrbitVfxId` | **身體周圍浮空繞行的物件**用的 VfxTable id（必須 `Loop=1`、`Duration=-1`） | 沒有這一層 |

⚠ **刻意不在程式裡判斷「是不是第三階」。** 誰有特效由表決定——之後想給二階也加一點光、
或讓某隻 boss 共用同一套，都只要填表，程式一行不用改（專案的 CSV 資料驅動鐵則）。

⚠ **顏色請填「感覺值的一半」**：專案是 Linear 色彩空間，亮色疊暗底比直覺重一倍（[PROBLEMS.md](PROBLEMS.md) **E11**）。

⚠ **現有 12 列刻意留在 12 欄**（沒有補尾逗號）。`CsvUtil.Field` 讀不到就回空字串，所以「舊列＝三層全關」
是天生成立的，不必也不要去幫它們補值。

### 目前填了誰

| 血統 | AuraVfxId | HaloStyle | HaloColor | TrailStyle | TrailColor |
|---|---|---|---|---|---|
| 該隱（22） | （留空） | Disc | A01018 | （留空） | — |
| 旱魃（12） | （留空） | **Disk** | C08A1E | （留空） | — |
| 芬里爾（32） | （留空） | （留空） | — | **Step** | **33**（火焰腳印） |
| 泰坦（42） | （留空） | （留空） | （留空） | （留空） | — ／ **`OrbitVfxId`=36**（環繞碎石） |
| 化神期（52） | **37**（青雷） | （留空） | （留空） | （留空） | — |

### ⭐ 一個血統只掛一層（2026-09-10 作者拍板）

**每個血統只給一個獨特特效，不要一個角色身上掛好幾層。**

理由是**產能**：血統最少會做到 20 個以上（目前 13＋修真系列三階已進表），
如果一個角色就吃掉兩三層，特效很快就不夠分，而且到後面每個血統都是「環＋電光＋殘影」的排列組合，
反而每個都長得像。一個角色一個專屬的、一眼認得出來的東西，比堆疊三層有效。

所以該隱與旱魃的 `TrailStyle` 在 2026-09-10 清掉了——**環繞電光與移動殘影兩層現在都沒有血統在用**，
它們是留給後面的血統的（程式與素材都在，填表就能用）。

⚠ **這是原則不是限制**：程式沒有擋，某個特別重要的血統真的需要兩層還是填得下去。
但填之前先問「這個角色非得兩層不可嗎」。

⚠ 連帶的推論：**辨識度的預算變得很緊**。20 個血統要 20 個看得出差別的東西，
而「換個顏色」已經被驗證過不夠（旱魃第一版）。真正拉得開差異的是**輪廓與位置**——
目前的庫存是 Halo 的 5 種樣式 × 身光／頭光兩種尺寸位置，加上環繞特效與殘影兩層各自的變化。
分配要規劃，見 [TODO.md](TODO.md)。

⚠ **旱魃用的是 `Disk`（金色實心頭光）不是 `Cracked`**（2026-09-10 作者實機看過後改的）：
第一版給它 `Cracked` 龜裂赤日輪，作者的評語是「跟該隱太像」——同樣是罩住全身的大圓環，只是換個顏色，
遠看分不出來。改成**只比頭大一圈、貼在腦後的金色實心圓**（佛像頭光那種），象徵旱災之神，
與該隱的血色大環從輪廓上就是兩種東西。`Cracked` 樣式保留在程式裡沒刪，目前沒有血統在用。

**這帶出一條通則：血統之間的辨識度要靠「輪廓與位置」，不能只靠顏色。**
所以尺寸與位置改成由樣式決定（見下），而不是所有血統共用一組數字。

⚠ **該隱刻意不用環繞電光**（2026-09-10 作者實機看過後決定）：紅色電弧的白色像素太搶眼、蓋過角色本身，
他要的是「只有背後那圈紅環」。`VfxTable` 的 id 32 與那 22 張紅色電弧素材留著沒刪。
**同一組素材的藍色版後來給了化神期**（id 37）——修真題材配青雷，而且藍色比紅色安靜。
要再加就是複製 `fx3_lightning_aura` 的另一個顏色、加一列 VfxTable、填欄位，程式完全不用動。

其餘三個第三階**還沒填**，設計方向（作者 2026-09-10 拍板）是：
旱魃＝橙電弧＋龜裂赤日輪（`Cracked`）＋焦紅餘燼；芬里爾＝藍電弧＋殘月輪（`Crescent`）＋冷藍虛影、間隔要短；
泰坦＝碎石環繞或黃電弧＋岩輪（`Stone`）＋腳下揚塵。

**加一個血統要做的事**：① 把特效庫 `fx3_lightning_aura` 的對應顏色 22 張複製進
`Resources/VfxEffects/BloodlineAura/<血統>/`（檔名 `_01`~`_22`）② `VfxTable` 加一列（**必須 `Loop=1`、`Duration=-1`**）
③ 表B 那一列填上五欄。**程式不用動。**

### 三個元件

| 元件 | 做什麼 | 掛法 |
|---|---|---|
| `BloodlineAura` | 環繞電光：一顆無限循環的 Vfx 掛在玩家身上 | 玩家的子物件（同變身演出） |
| `BloodlineHalo` | 背後圓盤：**程式畫的**貼圖，自轉＋呼吸 | **獨立物件**（同 BlobShadow／CharacterGlow） |
| `BloodlineTrail` | 移動足跡：複製 sprite 或在腳下放 Vfx | 每個都是獨立物件 |
| `BloodlineOrbit` | 身體周圍浮空繞行的物件 | 玩家的子物件（位置每幀自己算） |

三層都由 `BloodlineSystem.ApplyTo` → `ApplyGodhoodFx` 收斂式套用，與換外型、換速度同一條路，
所以玩家物件重建（換圖、死亡回廣場）之後會自動重掛，沒有額外的補掛程式碼。

### ⚠ 五個踩過／繞過的坑（改這三層前必讀）

1. **常駐特效的排序不能用 `VfxTable` 的 `SortingOrder`。** 那是表演層的固定值（變身電弧 22050），
   短演出無所謂；常駐就會變成「玩家走到柱子後面、電光還飄在柱子前面」。`BloodlineAura` 每幀改成
   **與角色同一條 Y 排序算式**（`MapDepthSort.Order(transform.y + YSortByFeet.FeetYOffset)`）再 +1。
   ⚠ 刻意**不直接讀角色的 `sortingOrder`**——那是 `YSortByFeet` 在 LateUpdate 寫的，同為 LateUpdate 沒有順序保證。
2. **圓盤必須畫在角色之下。** 圓盤與身上的電光都是加色發光，疊在同一位置就是**零和**（**E13**）；
   但圓盤在背後、中央被角色不透明的身體擋住、只露外圈，兩層就不爭同一塊亮度。把 `SortOffset` 改成正值會踩到 E13。
3. **圓盤貼圖是 premultiplied（RGB = alpha）。** 所以透明區 RGB = 0，用 `AdditiveGlow` 才不會疊出一個方塊
   （佛光那個坑，**E12**）。要換成手繪圖的話，那張圖也得符合這個前提，否則要改用 `AuraGlow`。
4. **`Duration=-1` 的特效一定要有人 Destroy。** 這裡是 `BloodlineAura` 負責。而且**表格填錯會變成每幀生一顆**——
   特效播完自毀 → 元件看到不見了補生一顆 → 無限循環。已加防呆（連三次「生出來馬上就沒」就停用該層並印警告）。
5. **環要用平頂的 `Band`、不要用高斯的 `Ring`。** 高斯環在細的時候看起來像一條線，
   但一加粗就整條糊成一團暈——作者要的是「粗的線」不是「暈開的光」（2026-09-10 實機回饋）。
   `Ring` 留給「柔暈」那種本來就沒有明確邊界的層用。
6. **不要用 `Mathf.SmoothStep` 當 GLSL 的 smoothstep。** Unity 那支是「用平滑過的 t 在 from 與 to 之間插值」，
   不是「以 from/to 當門檻對 x 取值」。寫成 `Mathf.SmoothStep(0f, 1f, 已Clamp01的值)` 剛好等價，
   但只要門檻不是 0/1 就會**靜默算錯**。本檔自己寫了 `SStep(e0, e1, x)`，一律用它。見 [PROBLEMS.md](PROBLEMS.md) **E32**。
7. **殘影不能掛在玩家底下**（會跟著玩家走，就不是殘影了），而且要連 `flipX` 與 `lossyScale` 一起複製——
   角色圖是執行期 `Sprite.Create` 出來的（腳底 pivot），只抄 sprite 會得到面向相反或大小不對的殘影。

### 尺寸與位置由樣式決定（`StyleDefaults`）

「身光」（罩住整個身體的大圓）與「頭光」（只比頭大一點、貼在腦後）本來就是兩種東西，
所以直徑與圓心高度是**樣式的一部分**，不是一組全域數字：

| 樣式 | 直徑（×角色高） | 圓心上移（×角色高） | 誰在用 |
|---|---|---|---|
| `Disk` | 0.36 | 0.34 | 旱魃（頭光） |
| 其餘 | 1.15 | 0.18 | 該隱（身光） |

`Disk` 那兩個數字是**拿實機截圖量出來的**：角色可見高 216px、頭部落在上緣約 75px 內
⇒ 直徑 0.36 ≈ 77px（比頭大一圈）、圓心 0.34 ≈ 頭部中心。

元件上的 `DiameterRatio` / `CenterYRatio` 只在**取消勾選 `UseStyleDefaults`** 時才生效（微調用）。

### 兩個外觀旋鈕（`RingWidth` / `FillAmount`）

`BloodlineHalo.RingWidth` 是主環的粗細：0.10 ≈ 細線、**0.20 = 目前預設**、0.30 以上是厚重的光帶。
主環的半徑會**隨粗細自動往內縮**（`mainR = 0.86 - (w - 0.10) × 0.55`）——不縮的話加粗會讓環的外半部
撞上 r=1 的外緣柔化而被切平。

⚠ 這兩個旋鈕在 `Disk` 樣式下**意義不同**：`RingWidth` 變成「外緣柔化的寬度」、
`FillAmount` 變成「外緣要不要再亮一圈」。實心圓沒有環，自然也沒有環的粗細。

`FillAmount` 是圓盤內部的填充濃度：**0 = 只有環**（乾淨）、**0.35 = 目前預設**、1 = 中間補成一片實心的面。
對 Cracked 的日面、Crescent 的月面、Disc 的中央柔暈統一生效。中央大部分會被角色身體擋住，
所以它實際影響的是「身體兩側露出多少面」。

⚠ 這兩個值都會**重畫整張 512×512 貼圖**。`LateUpdate` 有偵測，所以 **Play 模式中拉滑桿就能即時看到變化**
（只有值真的變了才重畫，平常只是兩次浮點比較）；但別把它們接到每幀變動的東西上。

離線預覽圖在 `TempImage/BloodlineHalo/`（`halo_all.png` 四樣式總覽、`hanba_fill.png` 日面濃淡對照），
是拿同一套算式在 Python 重跑、模擬石板地上的 Linear 加色畫出來的。**改算式後想先看形狀不必進 Unity**，
但要記得把 `Shape()` 的改動同步過去，否則預覽會騙人。

### `Step` / `Ground`：腳下的一次性特效

與 `Fade` 的差別是**放什麼**：`Fade` 複製角色當下那格 sprite 當虛影，這兩種則是在**腳下**
（`FeetWorldPos`）放一個一次性的 Vfx。

**`Step` 與 `Ground` 的差別只有 pivot 怎麼對**：

| 樣式 | 對齊 | 給什麼用 | 誰在用 |
|---|---|---|---|
| `Step` | 底部貼齊腳底 | 立起來的東西（火焰、塵柱） | 芬里爾的火焰腳印 |
| `Ground` | 躺平貼地的東西（衝擊環、塵捲、裂痕） | 同上 | 泰坦的踏地揚塵 |

⚠ **兩者的 pivot 補償實測後是一樣的（都 0.5）。** 原本設計成「`Ground` 中心對齊腳底」，
理由是躺平的環圓心就該在腳的位置——但實機看起來一樣偏下（作者：「太下面了，跟之前芬里爾的狀況差不多」）。
原因是 **`FeetWorldPos` 已經是可見身體的最底緣**，任何往下延伸的東西都會跑到角色前面去，
跟它躺不躺平無關。樣式的區分先留著（語意上仍有意義，也還有 `FootFxYRatioOverride` 可各自覆寫），
但預設值目前相同。

⚠ **樣式決定的是「機制與怎麼對齊」，不是「用什麼素材」**——素材一律由 `TrailVfxId` 指定。
第一版把樣式命名成 `Fire` 是個錯誤：泰坦放的是衝擊環，卻得在表裡填 `Fire`。
（`Fire`／`Dust` 保留為 `Step` 的別名，舊資料不會壞。）

**火本身的一切都寫在 `VfxTable` 那一列**——動畫、幀率、壽命、排序。元件只負責「什麼時候放、放在哪」。
所以**要改火燒多久、多快熄滅，改 CSV 就好，不用動程式**：id 33 目前是 12 幀 @18fps ≈ **0.67 秒**燒完。

三個位置相關的參數在元件上（Play 中即時生效）：

| 欄位 | 預設 | 作用 |
|---|---|---|
| `FootFxSizeRatio` | 0.20 | 特效高度 = 角色高 × 此值 ×「VfxTable 那一列的 `Scale`」。<br>⚠ **每個腳步特效的相對大小調 VfxTable 的 `Scale` 欄**，不要動這個全域值。<br>⚠⚠ 縮放是按**高度**算的，扁素材的寬度會跟著放大好幾倍（泰坦的揚塵是 104×24，`Scale` 0.65 算出來是 0.46 高 × **1.98 寬**）——**看到小於 1 的 Scale 不代表畫出來比較小**，詳見 [VFX.md](VFX.md) |
| `StepPhaseA` / `StepPhaseB` | 0.25 / 0.75 | **腳踏地的兩個相位**。腳步特效跟走路動畫的幀同步，在跨過這兩點時各放一個 |
| `SpacingRatio` | 0.30 | 每走「角色高 × 此值」放一個。**只有殘影（`Fade`）和「拿不到走路幀」的退路才用它** |
| `FootSpread` | 0.05 | 左右腳相對**行進方向**偏多少。0 = 全部踩在正中線上 |
| `FootFxYRatioOverride` | -1 | **pivot 補償的覆寫**。留 -1 ＝ 依樣式自動（`Step` 0.5／`Ground` 0） |

⚠ **pivot 補償不是可有可無的微調。** `VfxManager` 生出來的 sprite 是**中心 pivot**，
直接擺在 `FeetWorldPos` 的話特效會有一半落在腳底線**以下**，看起來像踩在角色前面的地上而不是腳下
（2026-09-10 實機：作者回報「太下面了」，量出來火高 46px、角色高 213px，中心對齊等於低了 23px）。
**任何「在腳下／地面放特效」的地方都要記得補這半個高度**——但只對「立起來」的特效成立，
躺平的環（`Ground`）反而就是要中心對齊，抬了會往上飄。

⚠ **頻率跟動畫幀走，不跟距離走。** 用「走了多遠放一個」的話，一步會放幾個取決於移動速度——
泰坦 `WalkSpeed` 2、芬里爾 5，差 2.5 倍，調好一邊另一邊必定錯（作者回報泰坦「特效播太快」就是這個）。
改成問 `PlayerAnimator.WalkFrame`／`WalkFrameCount` 算出循環相位，在 `StepPhaseA`／`StepPhaseB`
跨越時各放一個 ⇒ **一個走路循環固定兩個，不管血統跑多快**。拿不到走路幀時才退回距離模式。

⚠ 左右偏移是**垂直於行進方向**算的，不是世界 X 軸——俯視角可以往任意方向走，
用 X 軸偏的話往上下走時腳印會排成一直線。

⚠ 排序走 `VfxTable` 的 `SortingOrder`＝**8**（地面特效那一層：在可走地上物之上、角色與立體地上物之下）。
這一層與環繞電光不同、**不需要每幀接管排序**——腳印是留在地上的一次性特效，本來就該被柱子擋住。

### `BloodlineOrbit`：身體周圍浮空繞行的物件（泰坦的碎石）

第四層，2026-09-10 加。與 `BloodlineAura`（一整張罩住身體的序列圖）的差別是**好幾個離散的小東西各自繞軌道**——
所以它是唯一會出現「東西轉到角色背後、又轉回身前」的層。泰坦的環繞碎石用它。

**三個讓它看起來是「繞著轉」而不是「貼在畫面上畫圈」的細節**：

1. **軌道要壓扁**（`Flatten` 0.38）。俯視角看一個水平圓，投影出來是扁橢圓。不壓扁的話石頭會沿正圓上下跑，
   看起來像在角色臉前面畫圈。
2. **前後要換排序**（關鍵）。轉到畫面**上方**＝身後 ⇒ 排序低於角色；轉到**下方**＝身前 ⇒ 高於角色。
   少了這一步就只是一圈貼在同一平面的東西。
3. **遠近縮放**（`DepthScale` 0.25）。後面的縮小、前面的放大。

可調參數都在元件上、Play 中即時生效：`Count`（顆數 4）、`RadiusRatio`（軌道半徑 0.40）、
`Flatten`、`HeightRatio`（軌道高度 0）、`Speed`（40 度/秒）、`SizeRatio`（單顆大小 0.13）、
`Bob`／`BobHz`（上下浮動）。

⚠ 素材那一列的 `SortingOrder` 對這層**不生效**（每幀被接管），與 `BloodlineAura` 同理。

### 環繞電光的間歇播放（`BloodlineAura.PulseGap`）

常駐電弧一直不停地閃會太吵、也把角色本身蓋掉（該隱那次退回的原因之一）。
`PulseGap`（預設 **2.5 秒**）讓它變成「**播一輪 → 留白 → 再播一輪**」；填 0 則維持無限循環。

作法是**播完一輪就讓它自毀，空檔過了再生一顆新的**。
⚠ 刻意**不是**把 renderer 關掉再打開——那樣動畫會在隱藏期間繼續跑，再現身時從播到一半的地方接，
看起來像卡了一下。重生才會每次都從第 0 幀開始。

「一輪多長」是元件從 `VfxTable` 算的（張數 ÷ `AnimFPS`），**改表格的幀率這裡自動跟上**，不必兩邊對。

⚠ 別把這個跟 `AnimFPS` 搞混：`AnimFPS` 是「同一輪播多快」（調它會讓電弧本身變慢，像慢動作），
`PulseGap` 是「兩輪之間隔多久」。要「閃一下、停一會、再閃一下」的是後者。

### 什麼時候顯示（idle / walk / attack 才開）

三層都只在 **idle / walk / attack** 這三種「站著的動作」顯示；一進入 **dead 幀的姿勢就整組關掉**，
回到那三種再開。判斷走 `PlayerAnimator.BodyFxVisible`。

**為什麼不是「對位對準一點」就好**：`dead/` 這組圖是[一圖三用](CHARACTER_SETUP.md)的——死亡、
**血統變身的倒下與爬起**都在用它。趴著的時候「頭」在**水平方向**的某一端，而圓盤的定位是
「從身體中心**往上**偏移」⇒ 往上偏多少都對不到躺著的頭。**這不是參數問題，是定位模型不成立**，
所以正解是關掉而不是硬對。（症狀：喝血統藥劑變身時人倒下了、旱魃的頭光還留在原地。）

判斷條件用的是 `PlayerAnimator.GeomState`——**與那三個幾何屬性（`VisibleHeight` / `FeetOffsetY` /
`BodyCenterOffsetY`）同一個真相來源**。它回 `Dead` 就代表「身體幾何已經換成趴姿」，
正好等於「特效的定位基準已經失效」的那段時間，不必另外定義一套狀態。

⚠ `BodyFxVisible` 是**白名單**（列出可以顯示的三種），不是「不是 Dead 就顯示」。
之後若加新動作（受傷、施法、被抓住…），白名單會讓它**預設關閉**，不會冒出「特效浮在半空」的意外；
確認那個姿勢對得到位再把它加進白名單。

**通則**：**任何「掛在角色身上、靠身體幾何定位」的持續型特效，都要問這個屬性。**
角色的姿勢會換，而「站著」的定位算式在躺著時不成立——這跟 `RefreshBodyScaledVisuals()`
那條（體型會變）是同一類問題的兩面：一個是尺寸會變，一個是姿勢會變。

### 位置與體型

全部走 §2「位置要用哪個座標」那三個屬性：圓盤與電光用 `BodyCenterWorldPos`、大小用 `ScaledCharacterHeight`。
**兩層「撐過體型變更」的效果已登記在 `PlayerController.RefreshBodyScaledVisuals()`**——芬里爾 1.5 倍、
望月者 1.3 倍，不重建的話特效會停在舊尺寸（**E14**）。殘影不必登記：它每生一個都當場讀角色高度。

---

## 6. 多語系

血統系統的字串佔 **2001–2099** 段（`LanguageTable.csv`）。取用一律走 `BloodlineSystem` 內的
`Txt(id, fallback)`，語言表還沒補也不會變成 `[lang:2001]`。

| id | 用途 |
|---|---|
| 2001–2005 | 通用擋下（不是藥劑／已定型／表裡找不到／背包沒有／沒載入角色） |
| 2006–2010 | 進階擋下（尚未覺醒／不屬任何系列／尚未成熟／已在此之上／已至頂點） |
| 2011–2012 | 確認視窗文案（起始／進階） |
| 2013–2015 | 成功說明（起始／進階）與泛用失敗。⚠ 前兩者**目前沒有人顯示**（成功不跳 Toast），2015 仍在用 |

---

## 7. 怎麼加一個新系列

1. 表A 加一列（Id 接續，例如第六個系列）：`6,<系列Key>,<系列名>,60,61,62,...`
2. 表B 加三列：Id 60/61/62，填 `SpriteFolder`、`BodyScale`（先填 1，實機看過再調）與五屬性
   —— 只填到 `Note`（第 12 欄）就好，後面的光環／拖尾欄**留空不補逗號**，跟現有列一樣
3. 美術：`SequenceImage/<系列Key>/<SpriteFolder>/{idle,walk,dead,attack}/` 與 `Talk/<系列Key>/<SpriteFolder>/<8 種情緒>.png`
   —— **兩個資料夾都要開一層系列資料夾**（名字＝表A 的 `Key`），表B 的 `SpriteFolder` 就填 `<系列Key>/<角色資料夾>`
4. **跑 `Project Tools → Sync Map Assets`**
5. `ItemTable.csv` 加**三瓶**（見 §3 的編號規則）：`30x` 第一階（`BloodlineID` = Stage1Id）、
   `32x` 第二階直達（= Stage2Id）、`33x` 第三階直達（= Stage3Id）。三瓶共用同一張系列 icon
6. `BaseBloodRoll.csv` 加一列（只放 `30x` 那瓶；直達藥劑目前不進池，見 §8）（或做成 `unlockRoll` 觸發解鎖）

**進階藥劑不用動**——它是全系列通用的。程式碼一行都不用改。

---

## 8. 目前缺口

- **力量／敏捷／魔力／體力仍只存不套用**，等角色屬性系統（`WalkSpeed` 已於 2026-09-09 接上，見 §4）。力量沒有攻擊力欄位可對（傷害在武器表）、敏捷目前遊戲裡沒有任何對應物；魔力／體力要對到 `CombatStats` 的 MaxMana／MaxHealth，得先解掉 §4 那個 `ReviveFull()` 的坑
- **表B 現有的 `WalkSpeed` 數字是屬性系統前的佔位值**（人類 5、其他 12 個血統一律 10），接上之後等於「一喝藥就兩倍速」。要嘛重填成實際想要的速度、要嘛清空該格（＝不改速度），實機調過再定
- **玩家沒地方「事後」查自己的血統與階段**——喝下去當下有立繪揭示面板，但之後就沒地方看了。等角色資訊面板
- `SkillId` 仍是死欄（技能系統不存在）
- 目前五個系列（殭屍、血族、狂族、土裔、靈根），資料都齊；再加系列照 §7 走。狂族的立繪除了 `normal` 之外目前是暫代圖（與旱魃同一張），美術到了直接覆蓋檔案即可
- **狂族的 `BodyScale`（狼人 1／望月者 1.3／芬里爾 1.5）是憑印象給的，實機看過再定**
- **十瓶直達藥劑（321~325／331~335）還沒放進任何池**，現在只有 `giveItem` 之類的鏈動作拿得到。要決定投放方式：
  放血統池會稀釋「選系列」的意義（池裡會變成 15 瓶、抽到第一階的機率剩三分之一）；比較合理的方向是
  **當關卡獎勵或 `unlockRoll` 解鎖**，或另開一個高價的池。作者決定
- **靈根三階的 `WalkSpeed` 刻意留空**（＝不套用、維持 Inspector 的 5），沒有跟著其他血統填 10——
  因為那些 10 本來就是待重填的佔位值（見上一條）。作者統一決定要填什麼時，靈根這三格也要一起填
- **土裔三階的 `BodyScale` 全是 1、五屬性也是佔位**，都還沒實機看過。設定上石像鬼 → 山嶽巨人 → 泰坦是越變越大，而正規化會把三組圖拉成同高，`BodyScale` 就是拿來把體型差別做出來的那一欄——實機看過再調（改 CSV 即時生效）
- ~~土裔三階的 attack 中了 G6 那個坑~~ → **2026-09-09 已修**（[PROBLEMS.md](PROBLEMS.md) **G9**）：加了「曲線沒有可辨識的峰就整段照播」的失效偵測，石像鬼 2→25 幀、泰坦 4→23 幀，順手也修好了狼人與覓血者（兩個都是 2→25），其餘九個血統一格不動。山嶽巨人維持 4 幀（它曲線有結構，那就是它的動作長度）
- **狼人的攻擊動畫幾乎播不出來（只播 2 幀）**：起播／結束幀演算法（PROBLEMS G6）是拿「跟 idle 站姿的差異」當動作曲線，狼人的 idle 是直立、attack 整段都是前傾寬站姿 ⇒ 第 1 幀就到峰值 100%、結束幀＝第 2 幀。芬里爾也只播 3 幀（第 11~13）。這是演算法的前提（idle 與 attack 起手相似）被素材打破，不是素材壞掉；要嘛重做 attack 讓起手接近 idle，要嘛改成「相對 attack 自己第 1 幀」的曲線。記在 TODO
- **血族三階的 `BodyScale` 全是 1，還沒實機看過**——該隱那張 idle 可見高只有 138px（Base 193），正規化會把它放大約 1.4 倍，是最需要用眼睛校正的一個
- 變身表演**沒有音效**（專案還沒有音訊系統）——雷擊、煙爆、立繪剝落是這個遊戲裡最該有聲音的三個瞬間，音訊系統做好後第一個要補的就是這裡
- **`Talk/Base/normal.png` 是 1122×1402，與其他三張（1024×1536）比例不同**。立繪揭示面板已用「等比縮到框內、靠下對齊」吸收掉，但第一次喝藥（人類 → 殭屍）那一幕人類會比殭屍小一圈。重畫成 1024×1536 就完全對齊
- 立繪揭示只用 `normal` 表情。之後若想讓不同血統用不同表情（例如旱魃用 `proud`），把 `BloodlineIntroPanel.PortraitEmotion` 改成從表B 讀一個新欄位即可
- 抽選面板的字串仍是 `const string`，未走語言表（全 GachaPanel 的既有問題）
- **`BodyScale` 不影響碰撞框**（刻意；動 hitbox 會改手感）。體型差距若拉大到影響判讀要再處理
- **佛光的傷害半徑會跟著體型放大**：半徑 ×1.5 ＝ 面積 ×2.25，而每拍傷害不變 ⇒ 大體型血統的佛光 DPS 實質更高。這是「看到的就是打得到的」帶來的必然結果，做平衡時記得
- `BodyScale` 目前的數字（殭屍 1／毛殭 1.5／旱魃 1.2）是憑印象給的，要實機看過再定；改 CSV 即時生效

---

## 相關文件

- [GACHA_SYSTEM.md](GACHA_SYSTEM.md) — 血統藥劑怎麼被抽出來（祭壇抽選四層資料架構）
- [CHARACTER_SETUP.md](CHARACTER_SETUP.md) — 主角外型的路線 B（程式逐格動畫、血統換外型）
- [DRAMA.md](DRAMA.md) — 對話立繪 `Actor_<情緒>` 依血統自動切資料夾
- [SAVE_SYSTEM.md](SAVE_SYSTEM.md) — `progress.flags` 與 `ReincarnateInPlace`
- [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) — 輪迴主軸與 access/power 分軸
