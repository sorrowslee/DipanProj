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
| 系列 | 一個系列 = 三個階段。目前只有殭屍系列：**殭屍 → 毛殭 → 旱魃** |
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
```

這是「系列 ↔ 階段 ↔ 血統 Id」的**唯一真相**。`BloodlineSeriesTable.cs` 載入時順便建一份
「血統 Id → (系列, 第幾階)」的反查索引，`TryLocate()` 就是查它。

**血統 Id 慣例**：一個系列吃一個十位段（殭屍 10~12、下一個系列 20~22…），**Id 1 保留給人類**。

### 表B `Assets/Data/BloodlineTable.csv` — 每種血統的外型與屬性

```
Id,Key,DisplayName,SpriteFolder,BodyScale,WalkSpeed,Strength,Agility,Magic,Vitality,SkillId,Note
1,Human,人類,Base,1,5,5,5,1,1,,初始外型（最基礎數值）
10,Jiangshi,殭屍,Jiangshi,1,10,20,10,5,20,,殭屍系列 第一階
11,Maojiang,毛殭,Maojiang,1.5,10,40,15,15,40,,殭屍系列 第二階
12,Hanba,旱魃,Hanba,1.2,10,60,25,30,60,,殭屍系列 第三階
```

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

但**正規化只看高度、不看體積與姿勢**：殭屍那張是寬站姿的駝背剪影（可見寬 65 是因為身體側收，但輪廓張得開），
毛殭與旱魃是挺直的瘦長站姿——同樣高度下後兩者看起來就是小一號、也比較難從暗地板上讀出來。
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

目前三瓶：

| ID | 名稱 | 欄位 | 取得 |
|---|---|---|---|
| 301 | 血統藥劑・殭屍 | `BloodlineID=10` | 血統祭壇（`BaseBloodRoll.csv`，300 元、不連抽） |
| 310 | 血統進階藥劑・中階 | `BloodlineUpgrade=2` | 道具祭壇（`BaseItemRoll.csv`，權重 3） |
| 311 | 血統進階藥劑・高階 | `BloodlineUpgrade=3` | 道具祭壇（`BaseItemRoll.csv`，權重 1） |

icon 都在 `Resources/UI/Icons/Items/positions/bloodline/`：`bloodline_Jiangshi`（301）／`bloodline_lvup_middle`（310）／`bloodline_lvup_high`（311）。另有 `bloodline_Vampire` 是下一個系列（吸血鬼）的起始藥劑用圖，序列圖與立繪還沒做。

**為什麼進階藥劑放道具池不放血統池**：血統池賣的是「選一個系列」（一世一次的重大決定），
進階是之後的成長消耗品，兩件事分開。高階刻意比中階稀有——血統必須逐階喝，抽到高階但還在第一階
是會卡著的，別讓它太常出現。

`ItemData` 的三個判斷：`IsBloodlineStarter`（有 BloodlineID）、`IsBloodlineUpgrade`（有 BloodlineUpgrade）、
`IsBloodline`（前兩者任一，UI 用這個決定要不要走喝藥流程）。

---

## 4. 程式結構

```
BloodlineSeriesTable.cs   表A + 反查索引（血統 Id → 系列/階段）
BloodlineTable.cs         表B（外型 + 五屬性）
BloodlineSystem.cs        執行期系統：查詢、Plan/TryDrink、套用外型
BloodlineTransformFx.cs   變身演出的呼叫點（目前空實作）
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

`ApplyTo()` 現在只做一件事：**必要時 `pc.SetBloodline(def.SpriteFolder)`**。
屬性刻意什麼都不做（見開頭的警告）；技能只印一則 log（技能系統不存在）。

### 存檔

血統存在 `progress.flags["血統"]`，值 = 血統 Id 的字串（用 `SaveManager.GetFlagValue` 讀，不是 `GetFlag`）。

**刻意放周目層而不是 `lifetimeFlags`**：`ReincarnateInPlace` 會整個換掉 `progress`，
所以輪迴後自動回到人類，**不需要任何額外的重置程式碼**。存檔格式零改動。

**舊存檔救生艇**：存的血統 Id 在表B 找不到時（血統表刪過列——2026-08-18 移除了野魂 2／幽靈 3），
`CurrentBloodlineId` 與 `IsFixedThisCycle` 一律**當成未定型**，玩家可以重新選一次。
若照實回報「已定型成一個不存在的血統」，起始藥劑會被擋（而且訊息會變成自相矛盾的「你的血脈已定為『人類』」）、
進階藥劑也會被擋（找不到所屬系列），本世血統徹底卡死只能靠輪迴。

---

## 5. 變身演出（閃電 + 煙霧）

`BloodlineTransformFx.Play()` → 自建一個協程宿主 `BloodlineTransformFxRunner`（照 `LevelExitManjiController` 的樣板，跑完自毀）。

### 時間軸（總長約 6 秒）

| 時間 | 事件 |
|---|---|
| 0.00s | 關掉所有面板 → 掛輸入鎖 |
| 0.00s | 玩家 `dead` **正向**播放，倒下（25 幀 @12fps ≈ 2.08s） |
| 2.08s | 趴地定格；雷柱從畫面外生成 |
| 2.20s | **擊中**：螢幕震動 0.25s + 白閃（0.05 進 / 0.20 退） |
| 2.25s | 煙塵爆開（放大到玩家身高 ×1.6）+ 環繞電弧亮起 |
| 2.58s | 煙最濃 → **換成新血統，仍趴著** |
| 2.95s | 煙塵播完消散，露出新外貌 |
| 2.95s | 電弧殘留繼續繞 0.60s |
| 3.75s | 倒播 `dead`，爬起來（≈ 2.08s） |
| 5.83s | 解鎖，新血統站定 |

**節奏與外觀常數全部集中在 `BloodlineTransformFxRunner` 檔頭**，要調快慢改那幾個數字就好。

### 素材

| 用途 | 位置 | 幀數 | 來源包 |
|---|---|---|---|
| 雷柱（頂端） | `Resources/VfxEffects/TransformLightning/Start/Start_01~02` | 2 | Super Pixel Fantasy FX Pack 2 |
| 雷柱（身體，tileable） | `Resources/VfxEffects/TransformLightning/Loop/Loop_01~08` | 8 | 同上 |
| 煙塵 | `Resources/VfxEffects/TransformSmoke/`（VfxTable **30**） | 10 | Smoke Bursts |
| 環繞電弧 | `Resources/VfxEffects/TransformAura/`（VfxTable **31**） | 22 | Super Pixel Effects Pack 3 |

原始素材包保留在 `DipanProj_Main/血統特效/`（**在 Assets 外面**，不會被 Unity 匯入）。

**雷柱刻意不用 `end` 那兩張**：實測 `end` 與 `start` 是同兩張圖反過來，而且是「快消散的細電光」——既有的九霄雷獄早就註記過「end 會突然收細」，所以照它的做法：頂端 `start`、中間 `loop` 一路延伸到玩家身上。想改回來的話素材還在原始包裡。

排序層：雷柱 22000 < 電弧 22050 < 煙塵 22100（煙要蓋在最上面，才藏得住換裝那一刻）。⚠ 都必須 ≤ 32767，見 PROBLEMS E4。

### 尺寸與定位

- **一切尺寸都乘上 `max(變身前 BodyScale, 變身後 BodyScale)`**。只用變身前的話，換成更大的血統時，
  煙霧散開前那一段會露出新外型的頭尾。
- 煙霧與電弧的覆蓋高度 = `max(目前實際畫出來的高度, 站立高度 × 體型倍率)`；
  前者反映當下姿勢（趴著時比較矮），後者保證換成更大的血統仍蓋得住。
- **雷擊點是「可見圖的底部中心」＝角色腳下站的位置**（`SpriteRenderer.bounds` 的 `center.x` / `min.y`）。
  ⚠ **不要用 `transform.position`**——玩家的 sprite 是以 transform 為**中心**畫的，拿它當擊中點，
  電柱會停在胸口／肩膀高度（實測就是這個症狀）。煙霧與電弧則對齊 `bounds.center`（身體可見中心）。

### 重用的既有機制（都是這次順便補上的通用能力）

| 能力 | 位置 | 說明 |
|---|---|---|
| 拼接雷柱 | `SegmentedLightningColumn.Spawn(impact, cam, Style, …)` | 原本只給九霄雷獄用、素材路徑寫死；改成可指定 `Style`（路徑＋張數＋排序層），快取以路徑為鍵。**舊簽章保留，武器行為零改變**。 |
| 倒下動畫 | `PlayerAnimator.PlayFallDown(onDone, fpsMul)` | `PlayWakeUp` 的鏡像：正播 `dead`，**播完轉成趴地定格**（不是回 Idle）。 |
| 重定趴姿 | `PlayerAnimator.RefreshLyingPose()` | 換裝後必叫，見下面的坑 2。 |
| 中止表演 | `PlayerAnimator.CancelPose()` | 演出被外力打斷時解掉趴姿，否則角色永遠定格。 |
| 螢幕震動 | `MapCameraController.AddShake(秒, 振幅)` / `StopShake()` | **必須做在 MapCameraController 裡**——相機位置每幀由它獨佔寫入，另外掛震屏元件會互相蓋掉。偏移在 `LateUpdate` 最末端、`SmoothDamp` 之後才疊（先疊會被平滑吃掉，變成軟軟地飄一下）。 |
| 全螢幕閃光 | `ScreenFader.Flash(color, in, out)` / `ClearFlash()` | 與黑幕分開的獨立 Image + CanvasGroup，永不擋點擊。 |
| 具名輸入鎖 | `UIManager.SetExternalHold(owner, block, pause)` | 見 PROBLEMS **D13**。 |

### ⚠ 四個踩過的坑（改這段演出前必讀）

1. **喝藥當下遊戲是暫停的。** 確認視窗關掉後背包還開著，而背包 `PausesGame=true` → `timeScale=0`；而玩家動畫、`VfxInstance`、雷柱**全部吃 `Time.deltaTime`**，整段演出會凍住。所以開頭一定要 `CloseAll()`，且 `SetExternalHold` 的 **pause 必須是 false**。
2. **換裝會把趴姿打回站姿。** `SetBloodline` 內部重跑 `PlayerAnimator.Setup`，把 sprite 換成新血統的 idle 第 0 幀；但趴地定格旗標還在、`Update` 直接 return 不再更新 → **角色站著定格**。所以 `onSwap` 之後一定要 `RefreshLyingPose()`。
3. **玩家可能在演出中被打死**（演出期間他是被鎖住不能閃避的）。`Alive()` 有查 `PlayerController.IsDead`，死了就中止；而且輸入鎖用**具名持有者**，不會和死亡流程互相清掉對方的鎖（PROBLEMS D13）。
4. **演出期間玩家還是按得到 `B`/`K`/`Y`。** 那三個面板都會暫停遊戲 → 演出凍住。`StorageBagCoordinator` 查 `BloodlineTransformFxRunner.IsPlaying` 擋掉（PROBLEMS D14）。

### 保險絲

- `WaitPose` 等表演結束的三個出口：完成 / `IsWakeUpBusy` 變 false（表演被打斷）/ 逾時 6 秒。逾時計時用 **unscaled**，否則 timeScale=0 時連保險絲都凍住。
- `BloodlineSystem.TransformTimeout = 20f`：演出漏叫 `onFinished` 時強制解除收斂鎖。正常演出約 6 秒、最壞（兩段各逾時）約 14 秒，所以 20 秒。**之後若把演出加長到 15 秒以上，這裡要一起調大。**
- `BloodlineTransformFxRunner.IsPlaying` 是 static，已註冊 `PlayModeStaticReset`——殘留會讓下一次 Play 的背包熱鍵全部按不出來。

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

1. 表A 加一列：`2,Vampire,吸血鬼,20,21,22,...`
2. 表B 加三列：Id 20/21/22，填 `SpriteFolder`、`BodyScale`（先填 1，實機看過再調）與五屬性
3. 美術：`SequenceImage/<SpriteFolder>/{idle,walk,dead,attack}/` 與 `Talk/<SpriteFolder>/<8 種情緒>.png`
4. **跑 `Project Tools → Sync Map Assets`**
5. `ItemTable.csv` 加一瓶系列起始藥劑（`BloodlineID` = 該系列第一階的 Id）
6. `BaseBloodRoll.csv` 加一列（或做成 `unlockRoll` 觸發解鎖）

**進階藥劑不用動**——它是全系列通用的。程式碼一行都不用改。

---

## 8. 目前缺口

- **五個屬性只存不套用**，等角色屬性系統（見 §2 的警告）
- **玩家沒地方看自己的血統與階段**——之後跟角色資訊面板一起做。目前只有喝藥當下的 Toast
- `SkillId` 仍是死欄（技能系統不存在）
- 只有殭屍一個系列；吸血鬼系列已有 icon（`bloodline_Vampire.png`）但無序列圖與立繪
- 變身演出**沒有音效**（專案還沒有音訊系統）——雷擊與煙爆是這個遊戲裡最該有聲音的兩個瞬間，音訊系統做好後第一個要補的就是這裡
- 抽選面板的字串仍是 `const string`，未走語言表（全 GachaPanel 的既有問題）

---

## 相關文件

- [GACHA_SYSTEM.md](GACHA_SYSTEM.md) — 血統藥劑怎麼被抽出來（祭壇抽選四層資料架構）
- [CHARACTER_SETUP.md](CHARACTER_SETUP.md) — 主角外型的路線 B（程式逐格動畫、血統換外型）
- [DRAMA.md](DRAMA.md) — 對話立繪 `Actor_<情緒>` 依血統自動切資料夾
- [SAVE_SYSTEM.md](SAVE_SYSTEM.md) — `progress.flags` 與 `ReincarnateInPlace`
- [CORE_LOOP_DESIGN.md](CORE_LOOP_DESIGN.md) — 輪迴主軸與 access/power 分軸
