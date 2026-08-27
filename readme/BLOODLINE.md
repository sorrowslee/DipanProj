# 血統系統（Bloodline）

返回 [文件總覽](README.md)

> **一句話**：血統分「系列」，一個系列三階；喝**系列起始藥劑**決定本世走哪一系列（不可逆），
> 之後喝**全系列通用的進階藥劑**逐階往上；輪迴後回到人類重來。
>
> ⚠ **血統目前不改變任何遊戲數值。** 表B 的五個屬性（行走速度/力量/敏捷/魔力/體力）只存不套用——
> 遊戲裡還沒有角色屬性系統。換血統在體感上**只有外型與對話立繪會變**，這是預期行為，不是壞了。

---

## 1. 玩法規則（拍板）

| 規則 | 內容 |
|---|---|
| 系列 | 一個系列 = 三個階段。目前有兩個系列：殭屍系列 **殭屍 → 毛殭 → 旱魃**、血族系列 **覓血者 → 血伯爵 → 該隱**、狂族系列 **狼人 → 望月者 → 芬里爾**（英文：血族系列 **Bloodborn**、第一階 **Bloodseeker**（也是外型資料夾名）；狂族系列 **Feralborn**、三階 `Werewolf`／`Moonwatcher`／`Fenrir`（2026-08-27 加）。2026-08-27 前叫「夜裔系列／夜裔／Nightborn」，PROBLEMS／PROGRESS 歷史條目裡的 Nightborn 就是它） |
| 起點 | 所有角色從「人類」（血統 Id 1、外型 Base）開始 |
| 選系列 | 喝**系列起始藥劑**（例：血統藥劑・殭屍）→ 變成該系列第一階。**本世只能選一次，不可逆，不能改吃別的系列** |
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
```

這是「系列 ↔ 階段 ↔ 血統 Id」的**唯一真相**。`BloodlineSeriesTable.cs` 載入時順便建一份
「血統 Id → (系列, 第幾階)」的反查索引，`TryLocate()` 就是查它。

**血統 Id 慣例**：一個系列吃一個十位段（殭屍 10~12、血族 20~22、狂族 30~32、下一個系列 40~42…），**Id 1 保留給人類**。

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
| `BloodlineID` | **系列起始藥劑**：指到 BloodlineTable 的 Id（慣例是該系列第一階） |
| `BloodlineUpgrade` | **進階藥劑**：目標階數（2 = 中階、3 = 高階）。不指定血統，實際變成什麼由表A 決定 |

目前五瓶：

| ID | 名稱 | 欄位 | 取得 |
|---|---|---|---|
| 301 | 血統藥劑・殭屍 | `BloodlineID=10` | 血統祭壇（`BaseBloodRoll.csv`，300 元、不連抽） |
| 302 | 血統藥劑・血族 | `BloodlineID=20` | 血統祭壇（同上，權重與殭屍同為 10） |
| 303 | 血統藥劑・狂族 | `BloodlineID=30` | 血統祭壇（同上，權重 10） |
| 310 | 血統進階藥劑・中階 | `BloodlineUpgrade=2` | 道具祭壇（`BaseItemRoll.csv`，權重 3） |
| 311 | 血統進階藥劑・高階 | `BloodlineUpgrade=3` | 道具祭壇（`BaseItemRoll.csv`，權重 1） |

icon 都在 `Resources/UI/Icons/Items/positions/bloodline/`：`bloodline_Jiangshi`（301）／`bloodline_Bloodborn`（302）／`bloodline_Feralborn`（303）／`bloodline_lvup_middle`（310）／`bloodline_lvup_high`（311）。

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
> 也因此 **`TryDrink` 的 `out string message` 成功與失敗都會填**（成功給 `DoneText`、失敗給 `Reason`），
> 呼叫端直接 Toast 就好，**不要自己記住成功文案**——那份會過期。

> ⚠ **不能喝的時候一定要在按鍵當下就擋下並說明理由**，不要先跳確認視窗、按完才發現沒反應。
> 這是明確要求的體驗，`Plan` 的 `Reason` 就是為此存在。

### 收斂式套用

`BloodlineSystem` 是常駐單例、自動生成、零接線，每幀比對「存檔裡的血統」與「已套用的血統」，
不一致才動作——所以不管存檔載入、換圖、玩家物件重建的順序如何，最後都會收斂到正確狀態。

`ApplyTo()` 現在只做一件事：**必要時 `pc.SetBloodline(def.SpriteFolder, def.BodyScale)`**。
五屬性刻意什麼都不做（見開頭的警告）；技能只印一則 log（技能系統不存在）。

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
| `NameFontSize` | 56 | |

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

## 6. 多語系

血統系統的字串佔 **2001–2099** 段（`LanguageTable.csv`）。取用一律走 `BloodlineSystem` 內的
`Txt(id, fallback)`，語言表還沒補也不會變成 `[lang:2001]`。

| id | 用途 |
|---|---|
| 2001–2005 | 通用擋下（不是藥劑／已定型／表裡找不到／背包沒有／沒載入角色） |
| 2006–2010 | 進階擋下（尚未覺醒／不屬任何系列／尚未成熟／已在此之上／已至頂點） |
| 2011–2012 | 確認視窗文案（起始／進階） |
| 2013–2015 | 成功 Toast（起始／進階）與泛用失敗 |

---

## 7. 怎麼加一個新系列

1. 表A 加一列（Id 接續，例如第四個系列）：`4,<系列Key>,<系列名>,40,41,42,...`
2. 表B 加三列：Id 40/41/42，填 `SpriteFolder`、`BodyScale`（先填 1，實機看過再調）與五屬性
3. 美術：`SequenceImage/<SpriteFolder>/{idle,walk,dead,attack}/` 與 `Talk/<SpriteFolder>/<8 種情緒>.png`
4. **跑 `Project Tools → Sync Map Assets`**
5. `ItemTable.csv` 加一瓶系列起始藥劑（`BloodlineID` = 該系列第一階的 Id）
6. `BaseBloodRoll.csv` 加一列（或做成 `unlockRoll` 觸發解鎖）

**進階藥劑不用動**——它是全系列通用的。程式碼一行都不用改。

---

## 8. 目前缺口

- **五個屬性只存不套用**，等角色屬性系統（見 §2 的警告）
- **玩家沒地方「事後」查自己的血統與階段**——喝下去當下有立繪揭示面板，但之後就沒地方看了。等角色資訊面板
- `SkillId` 仍是死欄（技能系統不存在）
- 目前三個系列（殭屍、血族、狂族），素材與資料都齊；再加系列照 §7 走。狂族的立繪除了 `normal` 之外目前是暫代圖（與旱魃同一張），美術到了直接覆蓋檔案即可
- **狂族的 `BodyScale`（狼人 1／望月者 1.3／芬里爾 1.5）是憑印象給的，實機看過再定**
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
