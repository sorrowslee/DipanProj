# 主角立繪 / 走路動畫設定教學 (Character Sprite & Animation Setup)

> 返回 [文件總覽](README.md)｜玩家控制器見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)｜怪物量產見 [MONSTER_SETUP.md](MONSTER_SETUP.md)

> ## ⚠️ 主角已改走「路線 B：程式逐格動畫（血統換外型）」（2026-06-26）
>
> 主角外型現在跟怪物**同一套**：程式逐格播放、圖走地圖素材管線、**不用 Unity Animator / clip**。換外型只要換資料夾名（血統），方便「血統」設計常常換外型。**本文以下舊的 Unity Animator 流程已被取代**（保留當參考；prefab 上的 Animator 進遊戲會被自動停用）。
>
> **資料夾慣例**（每個血統一個資料夾、每個動作一個子資料夾、單張 PNG 一幀）：
>
> ```
> GameAssets/Main/Characters/SequenceImage/<血統>/
> ├─ idle/   idle_01.png ...        ← 必備（單張也可＝靜態站姿）
> ├─ walk/   walk_01.png ...        ← 必備
> ├─ dead/   dead_01.png ...        ← 可選；一次性，播完停在最後一幀
> └─ attack/ attack_01.png ...      ← 可選；攻擊動畫——離散武器每次發射擺一次、持續武器（雷射/佛光）只在「開始放的那一下」擺一次（見 ACTORS_AND_COMBAT.md）；檔名可任意但需可排序
>
> **attack 的起手不必自己剪掉**：程式會比對這組圖與 idle 的輪廓，自動從「動作真正開始」的那一幀起播（見 ACTORS_AND_COMBAT.md「攻擊動畫怎麼播」與 PROBLEMS **G6**），所以素材可以照 AI 產出的完整動作直接丟進來。攻擊是**一次性**動畫（播完定格在最後一幀），不是循環——最後一幀請畫成「收勢」而不是「動到一半」。
> ```
> `Base` = 預設初始外型。檔名數字**補零**、依檔名排序＝播放順序（超過 9 張用兩位數）。同一血統所有幀建議**相同像素尺寸**（不然切狀態會忽大忽小）。
>
> **拿到的是「一張大序列圖」而不是單張幀？** 用 `Project Tools → Split Sprite Sheet` 系列工具切（每格 256×256）：
> 單張模式選檔就地切；「整包就地切割」選角色資料夾、遞迴把各動作資料夾裡的 sheet 一鍵全切成幀；
> 「切到檔名子資料夾」把 `idle.png/walk.png/…` 直接切成 `idle/`、`walk/` 資料夾結構。幀名自動符合本檔命名慣例（`前綴_01` 起兩位數）。

>
> **換外型**：把新血統資料夾（例 `Vampire/idle`、`Vampire/walk`）放好 → 跑 `Project Tools → Sync Map Assets` →
> 在 `Player` 的 `PlayerController` 把 **`Bloodline`** 設成 `Vampire`（或程式呼叫
> `PlayerController.SetBloodline("Vampire", bodyScale)`）。沒有 `dead/` 就不會演死亡動畫（防呆）。
> ⚠ **第二個參數是體型倍率、有預設值 1**——漏傳不會編譯錯，但會把 `BodyScale` 靜默重設成 1，
> 換血統後體型跑掉、影子與佛光圈跟著錯。實務上這支由 `BloodlineSystem` 統一呼叫，不要自己叫。
>
> **狀態**：idle/walk 循環、dead 一次性定格（死亡時觸發、不循環）。走路 fps 跟移動速度連動（防腳滑），同怪物。
> 相關程式：`Assets/Scripts/PlayerAnimator.cs`、`PlayerSpriteLibrary.cs`、`PlayerController.cs`（`Bloodline` 欄 + `SetBloodline`）。機制細節同 [MONSTER_SETUP.md](MONSTER_SETUP.md)。
>
> **dead 幀一圖三用（2026-07-07 起，2026-08-18 擴充）**：`dead/` 除了死亡，還被拿去演「趴下」與「爬起」：
>
> | API | 用途 |
> |---|---|
> | `PlayerAnimator.PlayFallDown(onDone, fpsMul)` | **正播**＝倒下，播完**轉成趴地定格**（不是回 idle）。血統變身用 |
> | `PlayerAnimator.HoldLyingPose()` | 直接定格在 dead 最後一幀（進場「睜眼醒來」用） |
> | `PlayerAnimator.PlayWakeUp(onDone, fpsMul)` | **倒播**＝爬起，播完回 idle |
> | `PlayerAnimator.RefreshLyingPose()` | 趴地期間換了外型後重新定幀（**換血統後必叫**，否則角色會站著定格） |
> | `PlayerAnimator.CancelPose()` | 中止表演回 idle（演出被打斷時收尾用） |
>
> 表演期間（`IsWakeUpBusy`）一般 `SetState` 全部被忽略，所以 `HandleVisuals` 每幀塞的 idle/walk 蓋不掉趴姿；
> 真死 `Dead` 例外（會打斷表演）。
>
> ⚠ 所以畫 dead 序列時 **「順播是倒下、倒播要能當爬起」已經不是建議而是硬需求**——
> 首幀＝站立、末幀＝完全趴平，中間別夾只適合單向的幀（例如噴血）。
> 見 [MAP_ENTER_EFFECT.md](MAP_ENTER_EFFECT.md) §1.5 與 [BLOODLINE.md](BLOODLINE.md) §5。
>
> **顯示高度與體型倍率（2026-08-18）**：`PlayerAnimator.Setup` 會依 **idle 的可見像素高度**把每個血統
> 正規化到同一個世界高度，所以不同來源、不同留白的圖進來都一樣高——基準是
> `PlayerController.CharacterWorldHeight`（1.95）× **`BodyScale`**（血統表的體型倍率，Base = 1）。
>
> 但跨血統的正規化**只看高度、不看體積與姿勢**：瘦長挺立的圖在同高度下就是比橫向壯碩的圖看起來小一號。
> `BodyScale` 就是拿來用眼睛校正那個落差的旋鈕。它是**純視覺**——不動碰撞框、不動戰鬥數值。
>
> **同一血統內，walk／attack 另外對齊 idle 的「體積尺度」（2026-08-27）**：AutoSprite 各動作的角色大小抓不準
> （Base 的 attack 整個人比 idle 粗一圈），所以 `PlayerAnimator.Setup` 對 walk／attack 各算一個縮放，
> 讓它的尺度＝idle 的尺度。尺度由 `PlayerSpriteLibrary.GetActionSize` 從圖算：**掃全部幀**、每幀量
> 可見框高與 √不透明像素數、各取**中位數**，尺度＝√(高 × √面積)。⚠ 不是只量高度——高度對「畫粗了」無感、
> 對「蹲下」反而會放大；也不是只量第一幀——第一幀常是起手。細節與量測數據見 [PROBLEMS.md](PROBLEMS.md) **G7**。
> dead 不正規化（躺姿本來就矮）。換圖後跑 `Project Tools → 角色 → 攻擊動畫幀數報告`，會印每個血統
> walk／attack 的縮放：**離 1 很遠代表那組圖大小抓歪了**（例：Base attack ×0.917）。
> 這裡沒有手填覆寫欄——只跟圖有關的資訊從圖算，抓歪了重做圖。
>
> **放大是以「可見腳底」為錨點往上長**，不是置中放大（置中會讓腳往下沉、像陷進地板）。
> 實作是 `PlayerSpriteLibrary.ApplyFootPivot` 依倍率倒推 sprite 的 pivot、就地 `Sprite.Create` 重建；
> **`BodyScale = 1` 時直接回傳原陣列、連 Sprite 都不重建**，所以不放大的血統是位元級零影響。
> ⚠ 刻意**不動 `MapSpriteLoader` 的預設 pivot**——那支是怪物／地上物／背景共用的。
>
> **要定位或縮放「掛在玩家身上」的特效**，用 `PlayerController` 的 `VisibleBodyHeight` /
> `FeetWorldPos` / `BodyCenterWorldPos`，**不要用 `transform.position`（那是畫布中心）、
> 也不要自己讀 `SpriteRenderer.bounds`**。完整說明見 [BLOODLINE.md](BLOODLINE.md) §2 與
> [PROBLEMS.md](PROBLEMS.md) **E14**。

> **碰撞框**：玩家碰撞框維持原本 prefab 上的設定（不像怪物那樣自動依圖貼合）——換血統若大小差很多，自行在 `Player` 上微調 collider。**`BodyScale` 不會放大碰撞框**（刻意；動 hitbox 會改手感）。

把一張「站立圖」+ 一張「走路序列圖」設定成「站著會待機、移動會走路」的主角。
本文件記錄完整流程，方便日後**換主角**時照做，不必重新摸索。

---

## ⚡ 快速提醒（不熟就先看這段，多數問題這裡就解決）

**目前專案實際設定（對照基準）**

| 檔案 | 路徑 | 尺寸 | 關鍵設定 |
|---|---|---|---|
| 站立圖 `Actor1.png` | `GameAssets/Main/Characters/SingleImage/` | 310×500 | Sprite Mode = **Single**、**PPU = 250** |
| 走路圖 `sequenceImage_Actor1.png` | `GameAssets/Main/Characters/SequenceImage/` | 2787×500，**9 格**（每格約 309×500） | Sprite Mode = **Multiple**、切法 = **Cell Count 9×1**、**PPU = 250** |

> 角色顯示高度 = 圖的像素高 ÷ PPU = 500 ÷ 250 = **2.0 個世界單位**。

**三條鐵則（忘記時回來看這三句）**

1. **走路圖一定用「依格數」切**（Grid By Cell **Count**），欄數 = 圖裡的人數（目前 9），**不要用固定像素大小**。
2. **站立圖和走路圖的 PPU 必須一樣**（目前都 250），否則站立／走路會忽大忽小。
3. **controller 的開關參數一定叫 `isMoving`（Bool）**，名字打錯動畫就不會切換（程式寫死的）。

**症狀 → 病因 → 解法（快速診斷表）**

| 症狀 | 病因 | 解法 |
|---|---|---|
| 走路時人被切半截 / 有空白格 | 用「固定像素大小」切圖 | Sprite Editor 改用 **Cell Count 9×1** 重切（見 §3） |
| **站立很小、一走路就變大**（或相反） | 兩張圖 **PPU 不一致** | 把站立圖 PPU 改成跟走路圖一樣（都 250）（見 §8） |
| 整個角色都太大／太小 | PPU 設太低／太高 | 兩張**一起**改成同一個數字，`PPU = 500 ÷ 想要高度` |
| 移動了動畫不切換、永遠站著 | controller 參數名不是 `isMoving` 或型別不對 | 參數改成 `isMoving`、Bool（見 §6） |
| 放開方向鍵還會多走一步才停 | transition 勾了 Has Exit Time | 兩條 transition 都**取消 Has Exit Time**（見 §6） |
| 腳沒踩在地上、像浮空 | Pivot 不對 | 把 Pivot 設成 Bottom（或微調角色 Y）（見 §8） |
| **進遊戲變黑剪影**（prefab 預覽卻正常、換哪張圖都黑） | Player prefab 的 SpriteRenderer **Color 被染暗** | 把 Color 設回純白 RGBA(255,255,255,255)（見 §10） |
| 死亡動畫不播、人定在原地不變 | Animator 沒 `isDead` 參數／名字不符／沒拉 Any State→Death | 見 §11.3 |
| 死了還能走動、還在放招 | `Die()` 沒清輸入·速度、沒收持續武器 | 見 §11.4 |

下面是完整步驟，換新主角時從 §1 開始照做。

---

## 0. 觀念：一個會走路的角色由 6 樣東西組成

都放在 `Assets/GameAssets/Main/Characters/`（動畫在其下 `Animations/`）：

| # | 檔案 | 角色 | 重點設定 |
|---|---|---|---|
| 1 | `SingleImage/ActorN.png` | 站立圖（待機用的單張圖） | Sprite Mode = **Single** |
| 2 | `SequenceImage/sequenceImage_ActorN.png` | 走路序列圖（橫向 N 格） | Sprite Mode = **Multiple**，切成 N 格 |
| 3 | `Animations/ActorN_Idle.anim` | 待機動畫（1 格 = 站立圖） | Loop |
| 4 | `Animations/ActorN_Run.anim` | 跑步動畫（N 格走路圖） | Loop |
| 5 | `Animations/ActorN.controller` | 動畫控制器（Idle ↔ Run 切換） | 用 `isMoving`(bool) 切換 |
| 6 | `GameAssets/Main/Prefabs/Player.prefab` | 主角本體 | 掛 controller + 預設 sprite |

`PlayerController.cs` 每幀做 `_animator.SetBool("isMoving", currentSpeed > 0.1f)`——
**所以 controller 的參數名一定要叫 `isMoving`（bool）**，名字打錯就不會切換動畫。
左右翻面由程式 `flipX` 處理，序列圖只需畫「面朝一個方向（右）」的走路循環即可。

---

## ⚠️ 最常踩的雷：走路圖「位置沒切正確」

**原因**：用「固定像素大小」切圖（Grid By Cell **Size**）。
若走路圖的總寬不是「格寬的整數倍」，固定像素去切就會切歪——半個人、空白格都來。
（例：目前走路圖 2787×500、9 格，每格 2787÷9 ≈ 309.7px，根本不是整數，用固定 size 必歪。）

**解法**：改用「依格數切」（Grid By Cell **Count**）= **Column = 格數、Row = 1**。
它會自動把整張圖平均切成 N 份，**不管總尺寸多少都對**。換任何新走路圖都用這招。

---

## 1. 把新圖放進專案

1. 站立圖放到 `…/Characters/SingleImage/`，命名 `Actor2.png`（換新主角時用新編號）。
2. 走路序列圖放到 `…/Characters/SequenceImage/`，命名 `sequenceImage_Actor2.png`。
3. 回 Unity，等它自動 import（Console 無紅錯）。

> 先**別刪舊角色的檔案**——留著當對照與後備，等新角色全部跑起來再決定要不要清。

---

## 2. 設定站立圖（Single）

點選站立圖，在 Inspector：

- **Texture Type** = `Sprite (2D and UI)`
- **Sprite Mode** = `Single`
- **Pixels Per Unit (PPU)** = **填得跟走路圖一樣**（目前是 **250**）← 這步是「站立／走路同大小」的關鍵
- **Filter Mode** = `Bilinear`（像素風想要硬邊可改 `Point (no filter)`）
- 按 **Apply**

---

## 3. 設定走路圖（Multiple，依格數切）

點選走路序列圖，在 Inspector：

- **Sprite Mode** = `Multiple`
- **Pixels Per Unit** = **250**（與站立圖一致）
- 按 **Apply** → 點 **Sprite Editor**（沒看到就先 Apply 一次）

在 Sprite Editor 視窗：

1. 左上 **Slice** 下拉。
2. **Type** 選 **`Grid By Cell Count`**（← 重點，不要選 By Cell Size）。
3. **Column = 圖裡的格數（目前 9）、Row = 1**。
4. **Pivot = Center**（之後想讓腳底貼地可改 Bottom）。
5. 按 **Slice** → 右上 **Apply** → 關掉視窗。

切完展開該圖，應看到 `…_0` ~ `…_8`（9 格）共 N 個子 sprite，每個都剛好框住一個完整的人。
**如果還是切歪**，多半是 Column 數填錯——回去對一下圖裡到底有幾個人。

---

## 4. 做待機動畫 `ActorN_Idle`

1. 在 Hierarchy 隨便拖一個空物件當暫時對象（或直接用場景裡的 Player）。
2. Project 視窗點選**站立單張圖**。
3. 把它**拖到該物件上** → 跳「Create New Animation」存檔對話框。
4. 存到 `…/Characters/Animations/`，檔名 **`ActorN_Idle`**（會順便自動生一個 controller）。
5. 打開 **Animation 視窗**，確認它只有 1 格、且 **Loop Time 打勾**（點該 .anim，Inspector 勾）。

---

## 5. 做跑步動畫 `ActorN_Run`

1. 在 Project 展開走路圖，**框選 `_0` ~ 最後一格全部**（點 `_0`，按住 Shift 點最後一個；目前是 `_8`）。
2. 一起**拖到同一個物件上** → 又跳存檔對話框。
3. 存成 **`ActorN_Run`**（同個 Animations 資料夾）。
4. 在 Animation 視窗確認格數對、順序由小到大、**Loop Time 打勾**。
5. 想調走路快慢：選該 .anim，Inspector 改 **Sample Rate**，或在 Animation 視窗把關鍵格拉開/收緊。

---

## 6. 設定動畫控制器（Idle ↔ Run）

第 4 步已自動生了一個 controller，把它整理乾淨：

1. 打開 **Animator 視窗**，左上選到那個 controller。
2. 左側 **Parameters** 頁籤 → **+** → **Bool** → 命名 **`isMoving`**（**名字一字不差、Bool**）。
3. 確認狀態圖有 **Idle**（用 `ActorN_Idle`）、**Run**（用 `ActorN_Run`）兩個狀態。
   - 缺哪個就把對應 .anim 拖進 Animator 空白處；點狀態看 Inspector 的 **Motion** 是否正確。
   - 在 **Idle** 上右鍵 → **Set as Layer Default State**（橘色 = 預設）。
4. 連線（右鍵狀態 → Make Transition）：
   - **Idle → Run**：Conditions 加 `isMoving` = **true**；**取消勾 Has Exit Time**。
   - **Run → Idle**：Conditions 加 `isMoving` = **false**；**取消勾 Has Exit Time**。
   - 兩條線 Transition Duration ≈ 0.25。
5. controller 檔名確認為 **`ActorN.controller`**，放在 Animations 資料夾。

> 「取消 Has Exit Time」很重要：勾著的話動畫要播完才肯切，會「鬆開方向鍵後角色還走一下」。

---

## 7. 換到 Player 主角身上

雙擊打開 `GameAssets/Main/Prefabs/Player.prefab`：

1. **Animator** 元件 → **Controller** 欄拖入新的 **`ActorN.controller`**。
2. **SpriteRenderer** 元件 → **Sprite** 欄拖入 **走路圖第一格**（`…_0`）或站立圖。
3. 存檔（Ctrl/Cmd+S）。

---

## 8. 大小不對怎麼調（PPU）★最容易忘的地方

角色顯示大小由 **PPU（Pixels Per Unit）** 決定，**數字越大角色越小**：

```
顯示高度（世界單位）= 圖的像素高 ÷ PPU
PPU = 圖的像素高 ÷ 想要的世界高度
```

**鐵則：站立圖與走路圖的 PPU 一定要相同**，不然站著一個大小、走路又一個大小（就是「站立小、走路大」的元凶）。

目前設定：兩張都 500px 高、PPU 都 **250** → 各 500 ÷ 250 = **2.0 單位**高，一致。

- 想整個角色變大 → 兩張**一起**把 PPU 調小（例如 200 → 2.5 單位）。
- 想變小 → 兩張一起調大（例如 333 → 1.5 單位）。
- **只要兩張數字一樣就不會忽大忽小。** 改完記得 Apply / 必要時對該圖右鍵 **Reimport**。

> 腳底沒踩在地上、像浮空：把 Sprite 的 **Pivot 改成 Bottom**（站立圖在 Inspector、走路圖在 Sprite Editor 重切時設），或微調角色 Y。影子（BlobShadow）會依 sprite 寬度自動跟著。

---

## 9. 收尾檢查清單（切不動時逐項對）

- [ ] 走路圖 Sprite Mode = Multiple，用 **Cell Count（格數）×1** 切，每格框住完整一個人。
- [ ] 站立圖、走路圖 **PPU 相同**（目前 250）。
- [ ] Idle / Run 兩個 .anim 都 **Loop Time 打勾**。
- [ ] controller 參數叫 **`isMoving`**、型別 **Bool**。
- [ ] Idle 是預設狀態（橘色）；兩條 transition 都**取消 Has Exit Time**、條件分別 true / false。
- [ ] Player prefab 的 Animator Controller、SpriteRenderer Sprite 都換成新角色。
- [ ] Player prefab 的 SpriteRenderer **Color = 純白（RGBA 255）**，否則進遊戲會變黑剪影（見 §10）。

---

## 10. 進遊戲變黑剪影（SpriteRenderer Color 被染暗）

**症狀**：角色放進遊戲後變一團黑／剪影，但在 Prefab 預覽視窗裡看得到（只是偏暗）；換哪張角色圖都一樣黑。

**原因**：`Player.prefab` 的 **SpriteRenderer → Color（色調 Tint）被設成很暗的顏色**。SpriteRenderer 會把圖片「乘上」這個顏色，等於整張壓暗；再進昏暗的氛圍地圖就變純黑。與圖片、材質、光照都無關。

**解法**：選 `Player` → Inspector 的 **Sprite Renderer** → **Color** 色塊 → 設成**純白**，RGBA 全部 255（尤其 **A=255**，別半透明）。白色 = 不染色 = 顯示圖片原色。

> 改完後角色在氛圍地圖裡仍會偏暗有氣氛（那是 [ATMOSPHERE.md](ATMOSPHERE.md) 的設計），但不再是純黑剪影。若某張圖仍嫌太暗，那是該地圖的 `Atmosphere` 設定，與此無關。
> 完整踩坑記錄見 [PROBLEMS.md](PROBLEMS.md) G1。

---

## 11. 加一個死亡狀態（Death State）

死亡狀態跟走路狀態是**同一套機制**，只差在「持續 vs 一次性」：

| | 走路 | 死亡 |
|---|---|---|
| 觸發參數 | Bool **`isMoving`** | Bool **`isDead`** |
| 行為 | Idle ↔ Run 來回切 | 一旦 `isDead=true` 就切到 Death，**之後定住不再切回** |
| 連法 | Idle↔Run 兩條 transition | **Any State → Death** 一條 |
| 動畫 Loop | 要 Loop | **不要 Loop**（播一次停在最後一格） |

**觸發點程式已經接好了**：血量歸零 → `CombatStats.OnDeath`（事件）→ `PlayerController.Die()`（已訂閱）。
所以你只要做兩件事：在 Animator 加 Death 狀態（§11.3）、在 `Die()` 裡叫動畫並停操作（§11.4）。

### 11.1 準備死亡圖

跟站立／走路一樣放進 `…/Characters/`：

- 想要「倒下的單張定格」→ 一張圖（如 `Actor1_Dead.png`），設 **Single**、**PPU 跟其他一致（250）**。
- 想要「倒下的連續動作」→ 一張橫向序列圖，設 **Multiple**、用 **Cell Count（格數）×1** 切（同 §3）。

### 11.2 做死亡動畫 `Actor1_Death`

同 §4／§5：把死亡圖（單張或整排子 sprite）拖到場景物件上 → 存成 **`Actor1_Death`**（放 Animations 資料夾）。

> **關鍵差別：這個 clip 的 Loop Time 要「取消勾選」**（死亡只播一次、停在最後一格，不要循環）。

### 11.3 在 Animator 加 Death 狀態

打開 Animator 視窗（選到 `Actor1.controller`）：

1. Parameters → **+** → **Bool** → 命名 **`isDead`**（一字不差）。
2. 把 `Actor1_Death` clip 拖進 Animator 空白處 → 生成 **Death** 狀態。
3. 在左上的 **Any State**（橘色塊）右鍵 → **Make Transition** → 點 **Death**，拉出「Any State → Death」。
4. 點那條線，Inspector 設：
   - Conditions 加 **`isDead` = true**。
   - **取消勾 Has Exit Time**。
   - **取消勾 Can Transition To Self**（重要：否則會一直重播第一幀卡住）。
   - Transition Duration 設 0（瞬間切換）。
5. **Death 狀態不要拉任何往外的箭頭**——死了就定住。

> 為什麼用「Any State」：死亡可能發生在站著或走路時，Any State = 「不管現在哪個狀態，只要 `isDead` 變 true 就切到 Death」。

### 11.4 程式：在 `Die()` 觸發動畫 + 停止操作

打開 `Assets/Scripts/PlayerController.cs`：

**(A) 把 `Die()` 改成這樣**（原本只有標記死亡 + Log）：

```csharp
private void Die()
{
    if (_isDead) return;
    _isDead = true;

    // 停止移動
    _moveInput = Vector2.zero;
    if (_rb != null) _rb.velocity = Vector2.zero;

    // 收掉持續型武器（雷射/佛光/環繞彈），免得人死了還在放招
    ClearActiveOrbitalBullets();
    ClearActiveBeams();
    ClearActiveAura();

    // 播死亡動畫（對應 Animator 的 Bool 參數 "isDead"）
    if (_animator != null) _animator.SetBool("isDead", true);

    Debug.Log("Player died!");
    // TODO: 之後接重生 / 讀檔 / 結束畫面（見 COMBAT.md §10）
}
```

**(B) 死後不能再操作**：在 `Update()` 最前面（`IsGameplayInputBlocked` 那段附近）加一行：

```csharp
if (_isDead) { _moveInput = Vector2.zero; return; }
```

**(C) 死後不會被輸入推動**：在 `FixedUpdate()` 最前面加：

```csharp
if (_isDead) return;
```

存檔回 Unity 編譯（Console 無紅錯）。

### 11.5 怎麼測

死亡 = HP 歸零。最快測法：把 Player 的 **PlayerMaxHealth** 暫時調很低（例如 5），進遊戲讓怪碰你（怪物接觸傷害，見 [COMBAT.md](COMBAT.md)），血一歸零就會切到 Death 動畫、人停住不能動。測完把血量調回去。

> 目前死亡只到「播動畫 + 停止操作」。後續的**重生 / 讀檔 / 結束畫面**屬死亡流程，規劃在 [COMBAT.md](COMBAT.md) §10，之後再接。

---

## 12. 走路動畫速度跟移動速度連動（避免「腳滑」）

讓走路播放速度跟著角色實際速度走：速度慢一半、走路動畫也慢一半，連續平滑、不卡。

- **機制**：元件 `Assets/Scripts/AnimatorSpeedByVelocity.cs` 每幀設 `Animator.speed = 實際速度 ÷ ReferenceSpeed`；**靜止時設回 1**（所以 Idle／死亡不受影響、不會凍住）。**純程式、零 Animator 參數**。
- **自動套用全角色**：`PlayerController.Start` 與 `MonsterController.Start` 已各自 `AddComponent<AnimatorSpeedByVelocity>()`（仿 BlobShadow）。**現有與未來的每隻角色都自動生效，不必逐隻在 Animator 加參數。**
- **ReferenceSpeed（參考速度）= 角色的「正常移動速度」**：玩家在 `PlayerController.Start` 自動帶入 `MoveSpeed`、怪物帶入 `MonsterActuator.MoveSpeed`。所以**正常走就是 1×（動畫滿幀、最順）**；只有實際速度**低於正常**時（未來的減速 debuff、類比搖桿半推）動畫才按比例變慢。
  - ⚠️ **常見誤區**：若把 ReferenceSpeed 設成固定大值（例如 5）而 MoveSpeed 只有 3，正常走就會一直用 3÷5＝0.6× 播放、永遠偏卡。**ReferenceSpeed 一定要等於該角色的正常速度。**
- **`MinMul` 預設 0.6**：放慢時最低只到 0.6×，避免掉到太低 fps 變超卡（代價是很慢時有一點腳滑，划算）。`MinMul`／`MaxMul` 在 `AnimatorSpeedByVelocity.cs` 上方可調。
- **限制**：像素序列圖格數少時，動畫放到很慢會看到「一格一格」（sprite 動畫的本質，不是 bug）。用 `MinMul` 夾住最慢倍率，或把走路圖畫更多格（12～16 格）即可更順。

### 測試時去哪調速度

- **玩家移動速度**：選 `Player` → Inspector 的 **PlayerController → MoveSpeed**。這同時就是「正常速度」（ReferenceSpeed 會自動跟著它），所以**改了 MoveSpeed 正常走仍是 1×（滿幀、不會變卡）**——改的是「正常」的定義，不是把動畫打折。
- **怪物移動速度**：改 **`Assets/Data/MonsterData.csv` 的 `Speed` 欄**（每種怪一列、各自設定，留空＝3），改完不必動程式。動畫的 ReferenceSpeed 會自動帶入這個值。（手動丟進場景、沒經 Spawner 的怪，才用 `MonsterActuator` 的預設 3。）

> 想實際看到「動畫變慢」的效果，要讓**實際速度低於正常速度**（例如之後做減速 debuff，把 velocity 乘上 0.5）。鍵盤是「全速或停」，所以單純走路一律 1×；這正是我們要的——正常走最順，被放慢才慢。

---

*建立於 2026-06-25：主角站立+走路動畫設定流程。最常見兩個雷：①走路圖用「固定像素大小」切→換尺寸不同的新圖就切歪，改用「依格數」切即解；②站立圖與走路圖 PPU 不一致→站立／走路忽大忽小，把兩張 PPU 設成一樣即解。*
*2026-06-25 更新：補上最上方「快速提醒 + 症狀對照表」，數字改為目前實際設定（站立 310×500、走路 2787×500 共 9 格、兩張 PPU 250）。*
*2026-06-25 更新：新增 §10（進遊戲變黑剪影＝SpriteRenderer Color 被染暗）、§11（加死亡狀態：Any State→Death + Bool `isDead` + Die() 觸發動畫並停操作；觸發點 CombatStats.OnDeath→Die 已接好）。*
*2026-06-25 更新：新增 §12（走路動畫速度跟移動速度連動＝`AnimatorSpeedByVelocity` 用 Animator.speed，玩家/怪物 Start 自動掛、零 Animator 參數）＋測試調速度的位置。*
