# 怪物量產設定 (Monster Setup — 路線 B：程式逐格動畫)

> 返回 [文件總覽](README.md)｜怪物 AI／受擊見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)｜地圖素材管線見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)、[DRAMA.md](DRAMA.md)（同套 catalog 載入）

怪物採「**一套共用、只換外觀＋套 CSV 數值**」的量產方式：**不必每隻怪拉 prefab、拉 Animation、切圖**。外觀由程式逐格播放（route B），圖走地圖素材管線（catalog + StreamingAssets），跟劇情大圖／頭像同一套。

為什麼不用 Unity Animator：怪物圖走「執行期載入」的地圖素材管線，而 Animator 的 AnimationClip 只能綁編輯期匯入的 sprite 資產，兩者不相容——所以改成程式每幀換 `SpriteRenderer.sprite`（專案既有 `AnimatedMapObject` / `VfxInstance` 也是這樣做）。

---

## 量產一隻新怪（重複這幾步）

1. **放圖**：在 `GameAssets/Modules/<關卡>/Monsters/SequenceImage/<怪名>/` 底下，每個動作開一個子資料夾放單張幀圖：
   - `idle/`（**必備**）、`walk/`（**必備**）、`attack/`（可選，沒放就不會有攻擊動畫）、`pant/`（可選，喘息；目前只有紅嫁衣放完大絕時用，見 [BOSS_MODULE.md](BOSS_MODULE.md) §2）、`jump/`（可選，跳躍；只有 `BrainType=LeapSlam` 會播，見 [BOSS_MODULE.md](BOSS_MODULE.md) §9）。
   - ⚠ **`jump/` 要先量幀再用**：一個資料夾未必只裝一次跳躍——狂族皇家衛士那 25 張其實是「跳兩次」，整組播完會踩兩下。量法與幀事件見 [BOSS_MODULE.md](BOSS_MODULE.md) §9.1。
   - 檔名數字**補零**、依檔名排序＝播放順序：`idle_01.png`、`walk_01.png`…`walk_08.png`（超過 9 張務必兩位數，否則 `_10` 會排到 `_2` 前面）。
   - 不用組序列圖、不用在 Unity 切格——一張 PNG = 一幀。
2. **同步**：`Project Tools → Sync Map Assets`（把圖收進 catalog ＋ StreamingAssets）。
3. **填 CSV**：在 `Assets/Data/MonsterData.csv` 加一列（`Name` = 資料夾名 `<怪名>`，**`PrefabPath` 留空** = 走 route B），填 HP / Speed / ContactDamage / Scale / AnimFPS 等。
4. **擺進地圖**：地圖編輯器放 `monsterSpawn` 出生點、填這隻的 `monsterId`（= CSV 的 ID）。
   - （選填）**怪物id 可填多個**：以 `|` 分隔（例 `5|7|9`）＝每隻各自從中隨機挑一種生。
   - （選填）**重複間隔秒 ＋ 同時存在上限**：填了秒數就從「進圖生一次」變成「每隔 N 秒生一波」（一波＝每格各一隻），留空＝維持原本的一次性。詳見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3.5。
   - （選填）**死亡觸發旗標**欄：這個出生點生的怪死亡時把指定旗標設為 true（給觸發鏈 `requireFlag` 用，例：殺家人→`killedFamily`→劇情分支）。綁「這一次擺放」而非怪物種類、每個出生點各自設定；空＝不寫。旗標先在旗標管理器登記，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §7。
5. **進遊戲**：自動生這隻怪、套 CSV 數值、依怪名載圖。有 `attack/` 才會演攻擊、沒有就只發呆＋走路（防呆自動處理，零改程式）。

> **同一隻怪所有幀建議用相同像素尺寸**（例如都 500×500），否則切狀態會忽大忽小（同主角「站立／走路 PPU 要一致」的道理；route B 一律以 PPU 256 載入，尺寸由像素數決定）。整體大小用 CSV 的 `Scale` 微調。

---

## 資料夾結構

```
GameAssets/Modules/<關卡>/Monsters/SequenceImage/<怪名>/
├─ idle/    idle_01.png  idle_02.png ...   ← 必備（單張也可，就是靜態站姿）
├─ walk/    walk_01.png  walk_02.png ...   ← 必備
├─ attack/  attack_01.png ...              ← 可選
├─ pant/    pant_01.png ...                ← 可選（喘息；沒放會自動退回 idle）
└─ jump/    jump_01.png ...                ← 可選（跳躍；只有 BrainType=LeapSlam 會播，見 BOSS_MODULE §9）
                                              ↑ jump 的倍率欄是 JumpScale，但**留空的語義和其他動作不同**：
                                                其他動作留空＝自動依可見高對齊 idle；jump 留空＝**沿用 IdleScale、不做自動對齊**
                                                （跳躍的可見高度本來就是動作的內容，正規化會把它抵銷；理由見 BOSS_MODULE §9.7）
                                              ↑ pant 專屬兩個參數在 MonsterAnimator 上方：
                                                PantFpsMul（幀率倍率，預設 0.25＝比 AnimFPS 慢四倍）
                                                PantPingPong（乒乓來回播，預設 true＝首尾不必對接）
```

同步工具會把**每個「直接含 PNG 的動作葉資料夾」**收成一筆 catalog item（`category=Monsters`、`id`＝資料夾相對路徑、≥2 幀帶 `frameCount`/`frames`）。`MonsterSpriteLibrary` 再依「`<怪名>/<state>`」索引取用。

---

## CSV 欄位（`MonsterData.csv`）

沿用既有欄位，**新增 `AnimFPS`**（末欄）：

| 欄位 | 說明 |
|---|---|
| `ID` / `Name` | 編號 / 怪名（`Name` 要等於圖的資料夾名 `<怪名>`） |
| `HP` / `Speed` / `ContactDamage` / `DamageReduction` | 血量 / 移動速度 / 接觸傷害 / 受擊減傷% |
| `Scale` | 整體縮放（調大小） |
| `BrainType` | AI 模組。**近戰分兩種**（見 [BOSS_MODULE.md](BOSS_MODULE.md) §10.0）：`Chase`＝衝撞型（一路貼上去磨，**沒有 attack 圖**的怪用這個）、`MeleeChase`＝揮舞型（追到定點站定把攻擊動畫做完，**有 attack 圖**才能用）。<br>其他：`Pounce` 撲擊（§7）、`Archer` 射手（§8）、`LeapSlam` 跳躍踐踏（§9）、**`SuicideBomb` 自爆（§11，走到身邊引爆、不需要 attack 圖）**、`War` 陣營戰士、`RedBridalGown`／`BanyanTree` boss |
| `InvincibleTimeMs` / `KnockbackThreshold` / `KnockbackPercent` | 受擊反應（見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)） |
| **`PrefabPath`** | **route B 留空**。只有要沿用「自帶 Animator 的舊 prefab」才填（向下相容） |
| **`AnimFPS`** | **新增**：程式動畫播放幀率，留空＝8。走路會再依實際速度連動（防腳滑；倍率夾在 `MinMul`~`MaxMul`，見下） |
| **`JumpScale`** | **jump 顯示倍率**（表尾，接在 `AttackScale` 後面，2026-09-18）。有填就照填的走；<br>⚠ **留空的語義與上面三個不同**：其他動作留空＝走「自動依可見高對齊 idle」，**jump 留空＝直接沿用 `IdleScale`、不做自動對齊**。因為跳躍的可見高度本來就是動作的內容（蹲下時矮、騰空伸展時又不同），正規化等於把它抵銷，而且越蜷縮的幀被放得越大 ⇒ 騰空時怪會膨脹一圈（實測 1.05~1.31 倍）。詳見 [BOSS_MODULE.md](BOSS_MODULE.md) §9.7 |
| **`AttackHitFrame`** | **揮舞型近戰的命中幀**（表尾）：attack 的第幾張是「武器揮到位」。留空＝張數 × 0.7 粗估。<br>⚠ **務必逐怪量**，比例靠不住——實測 12 張的怪是第 9 幀（75%）、24 張的是第 8 幀（33%）、25 張的是第 11 幀（44%）。量法見 [BOSS_MODULE.md](BOSS_MODULE.md) §10.4 |
| **`ReleaseFrame`** | **射手型的放彈幀**（表尾，2026-09-22）：attack 的第幾張是「武器已舉定、可以射了」。只有 `BrainType=Archer` 會用到，留空＝14。<br>⚠ **幀號即事件**（同 `AttackHitFrame`）：改 `AnimFPS` 時機會自動跟著對。<br>⚠ **要逐怪量**：狂族弩手 attack 25 張取 14；ZhaYu_Gun 只有 22 張、**幀 6 就完全水平舉定**，取 9。沿用別隻的值會晚半秒才出手。量法見 [BOSS_MODULE.md](BOSS_MODULE.md) §8.2b |
| **`MoveTrailFx`** | **移動拖尾特效**（表尾，2026-09-22）：怪移動時持續在**身後**（行進方向的反向）種的 VfxTable 特效。格式 `vfxId:大小倍率:每秒幾個`，多層用 `\|` 分隔（例 `44:1:5\|44:0.55:3`）。留空＝不掛。<br>給「有重量的東西」用——邪佛手掌 `Buddha_Hand` 壓過來時的滾滾沙塵。<br>⚠ **種在身後不是腳下**：種正腳下的話，停著時會原地堆成一坨、移動時跟太緊，看起來像在冒煙而不是在推開地面。<br>⚠ 沙塵要沉在怪腳下 ⇒ 那一列 VfxTable 的 `SortingOrder` 要填 **< 10**（角色是 10）。ID 44 填的是 8。<br>手感常數（散開、抖動、大小隨機、停止門檻）在 `MonsterMoveTrail` 檔頭。 |
| **`DropTableId`** | **掉落表 ID**（表尾，2026-09-22）→ `Assets/Data/DropTable.csv` 的 `ID`。<br>⚠ **留空／0 ＝ 完全不掉寶**。導入這張表時既有的怪**一律填 1**（＝原本寫死的那組：銅錢 1~5、血瓶 17.5%、魔瓶 17.5%），所以導入本身零行為變化。<br>一張表有 8 個**獨立**掉落槽，格式 `itemId:機率%:數量`（`101:100:1-5`／`201:17.5`／`301`＝必掉 1 個）。加新組合＝CSV 加一列，不動程式。<br>⚠ 掉不掉還有一層前置：`DropsLoot`（召喚物／NPC 一律 false，避免無限刷）。 |
| **`BombDamage`／`BombRadius`／`BombFuse`** | **自爆型專用**（表尾三欄，2026-09-22）。只有 `BrainType=SuicideBomb` 會用到，其餘怪一律留空。<br>留空＝退路值：傷害＝`ContactDamage` × 3、半徑＝1.8、引信＝0.6 秒。<br>⚠ **半徑是「體型 1 時」的世界單位，實際會 ×`Scale`**（2026-09-22，見 [PROBLEMS.md](PROBLEMS.md) **F29**）——填表照體型 1 去想就好。另有防呆下限保證「引信點著就一定炸得到」。<br>⚠ **實際殺傷範圍 ＝ 縮放後半徑 ＋ 目標碰撞框半徑**（玩家約 0.5），同 `LeapRadius`。詳見 [BOSS_MODULE.md](BOSS_MODULE.md) §11 |
| **`LeapDamage`／`LeapRadius`** | **跳躍踐踏專用**（表尾兩欄，2026-09-18）。只有 `BrainType=LeapSlam` 會用到，其餘怪一律留空。<br>留空＝退路值：傷害＝`ContactDamage` × 2、半徑＝1.6。<br>⚠ **半徑是「體型 1 時」的世界單位，實際會 ×`Scale`**（2026-09-22，見 [PROBLEMS.md](PROBLEMS.md) **F29**）：皇家衛士 `Scale` 1.5 ⇒ 實際 2.4；想維持舊的 1.6 就填 1.067。<br>⚠ **實際殺傷範圍 ＝ 縮放後半徑 ＋ 目標碰撞框半徑**（玩家約 0.5），詳見 [BOSS_MODULE.md](BOSS_MODULE.md) §9.4 |
| **`IdleScale`／`WalkScale`／`AttackScale`** | **逐動作顯示倍率**（表尾三欄，2026-09-17）。**留空＝自動**（把該動作的可見高對齊 idle）；**有填＝在那個自動結果上「再乘」**（2026-09-22 改，見 [PROBLEMS.md](PROBLEMS.md) **G14**）——所以 **1.0 ＝ 跟留空一樣、1.1 ＝ 比平常大一成**。<br>⚠ 舊版是「有填就**覆寫**自動」，而自動倍率本來就常 > 1（攻擊／奔跑姿勢比站姿矮）⇒ **填 1.1 反而會縮小**（ZhaYu_HugeSword 的 attack 自動倍率 1.252，填 1.1 等於縮 12%）。`pant` 沿用 `IdleScale`。整體大小仍吃 `Scale` 欄，這三欄是在它之上的等比例微調。<br>⚠ **四足獸（狼/狗/豹）通常要填**：自動那套量高度，而奔跑姿勢身體壓低、高度矮 ⇒ 被**放大**。戰狼實測 walk 被自動放大 ×1.288、等效寬 221→285px（idle 才 181），填 `WalkScale=0.9` 之後差距從 57% 降到 10%。詳見 [PROBLEMS.md](PROBLEMS.md) **G12** |

> **⭐ 張數不必湊滿 25：有幾張就播幾張，但循環會變快。** 載入完全依 catalog 的 `frameCount`（同步工具掃資料夾數 PNG，沒有上限也沒有期待張數），播放是 `_idx = (_idx + 1) % frames.Length`；1 張＝靜態姿勢（catalog 只在 ≥2 幀時寫 `frames`）。現成例子：`ZhaYu/walk` 只有 8 張、家人幽靈 `Ghost_*` 的 idle 都只有 1 張，都正常。
> **但 `AnimFPS` 是「每秒幾幀」不是「整個動作幾秒」**，所以：
> ```
> 動作循環時間 = 張數 ÷ AnimFPS
> ```
> 紅嫁衣 `AnimFPS=25`、走路 25 張＝ 1.00 秒一循環；換成 AutoSprite 推薦的 perfect loop（例 15 張）而 `AnimFPS` 不動，就變成 0.60 秒一循環、動作快將近一倍（走路會像小碎步）。**張數換成幾張，就把 `AnimFPS` 一起改成幾**，節奏才會維持原樣。主角同理，只是 fps 在 `PlayerController` 的 Inspector 欄位 **Player Anim FPS**（預設 12）而不是 CSV。
> 另外兩件換圖時會遇到的事：**幀順序是檔名字典序**（`string.CompareOrdinal`），所以編號一定要補零（`_01`~`_15`；寫成 `_1`~`_15` 會排成 1,10,11,…,2,3）；**Sync 不會刪舊檔**，25 張換成 15 張時 `StreamingAssets` 裡舊的 16~25 號會留著，但 catalog 只列新的 15 張所以不影響播放，只是垃圾檔。

---

## 防呆（有動畫才演、沒有就略過）

- `MonsterAnimator` 開場只載「放得到圖」的動作；`Has(state)` = 那個動作有沒有幀。
- 要播一個沒圖的狀態時，自動退回 **Attack→Walk→Idle**；一張圖都沒有就整個不動（不會把畫面清空）。
- 所以「**有 `attack/` 才會演攻擊、沒有就只走路/發呆**」是天生行為，加新怪不必動程式。
- ⚠️ **攻擊動畫 ≠ 攻擊邏輯**：目前「在攻擊範圍內（`AttackRange`）且有 attack 圖」就播攻擊動畫，傷害仍走既有的**接觸傷害**（`EnemyContactDamage`）。真正的「會攻擊的 AI ＋ 攻擊判定/傷害」是另一塊，之後再接。
- **死亡 / 受傷動畫**尚未納入（目前死亡直接銷毀）。要加時照 idle/walk/attack 同模式擴充 `MonsterAnimator` 的狀態詞彙。
- **加一個新動作要動的只有 `MonsterAnimator`**（2026-09-16 加 `pant` 時實測）：`MonsterSpriteLibrary.GetFrames(怪名, 動作)` 與 Sync 工具都是**通用字串／掃「直接含 PNG 的葉資料夾」**，載圖與同步都不必改；影子錨點工具同理（會自動多算一組，取不到時 `TryGetShadowAnchor` 退回 idle）。`MonsterAnimator` 那邊固定五處：`State` 列舉、幀陣列欄位、`Setup` 載入＋`CharacterMipBias.Register`＋影子錨點、`FramesFor`、`Resolve` 的退回規則。**沒圖的怪不會噴 log**（`GetFrames` 找不到只是靜靜回 null 並快取），所以加動作不會汙染 Console。
  （2026-09-18 加 `jump` 時再次驗證：同樣只動那五處。）
- **要「播一次就停」的動作用 `MonsterAnimator.PlayOneShot(state, 起幀, 迄幀, fps倍率)`**（2026-09-18 加）：
  既有播放一律是**循環**，而且 `MonsterController.HandleVisuals` **每幀**都會依距離/位移呼叫 `SetState` 覆寫狀態 ——
  所以有頭有尾的動作（跳躍、之後的死亡/受傷）光靠 `SetState` 撐不過下一幀。
  one-shot 期間**完全忽略外部 `SetState`**，要交還控制權得明確 `CancelOneShot()`。
  另可讀 `OneShotFrame`（1-based，＝檔名編號）把「第幾幀」當事件用，這樣改 `AnimFPS` 時時機會自動跟著對。
  ⚠ 與 `SetState` 不同，**沒有那個動作的圖時 `PlayOneShot` 回 false 而不是自動退回**——呼叫端要有 plan B。

---

## 運作 / 相關檔案

- `Assets/Scripts/AI/MonsterSpriteLibrary.cs`：載一次 catalog，依「`<怪名>/<state>`」索引怪物動作素材、給幀（`GetFrames`/`Has`）。
- `Assets/Scripts/AI/MonsterAnimator.cs`：程式逐格播放（Idle/Walk/Attack）、防呆退回、走路 fps 跟速度連動。
  > **走路 fps = `AnimFPS × clamp(實際速度 / ReferenceSpeed, MinMul, MaxMul)`**，`ReferenceSpeed` ＝ CSV 的 `Speed`。
  > `MaxMul`（2026-09-17 加，預設 **2.5**）是「跑快時腳步能加速到幾倍」——**以前這個上限寫死 1**，
  > 對一般怪沒差（牠們的實際速度恆等於 `Speed` ⇒ 倍率恆為 1，所以預設 2.5 對既有怪是零變化），
  > 但**會短暫加速的怪**（撲擊型的狼衝刺時位移是平常 3 倍）腳步還是散步節奏 ⇒ 視覺上是「滑過去」不是「衝過去」。
  > **這類怪的「突襲感」是動畫節奏給的，不是位移數字給的**——調再快的速度，腳步不動一樣沒感覺。
- `Assets/Scripts/AI/MonsterController.cs`：決定狀態（範圍內＋有 attack 圖→攻擊；移動→走路；靜止→發呆）並驅動 `MonsterAnimator`；無怪名/有 Animator 時退回舊 Animator。
- `Assets/Scripts/AI/MonsterSpawner.cs`：`PrefabPath` 留空時**程式建一隻通用怪**（零 prefab），外觀靠 `MonsterAnimator` 載圖。
- 同步管線（加新素材分類要三處一起改，見 [PROBLEMS.md](PROBLEMS.md) C3）：`Assets/Editor/MapAssetSyncTool.cs`、`Assets/Scripts/Map/MapIO.cs`、`Tools/sync_map_assets.sh`——皆已加 `Monsters/SequenceImage` 掃描。

> ⚠⚠ **要在怪腳下畫東西，用 `MonsterController.FeetWorldPos`，不要用 `transform.position`**：
> route B 怪物的 sprite **pivot 是畫布中心（0.5）**，與玩家相反（玩家的 pivot 在腳底）。
> `GetFrames` 的「腳底對齊」只把**各動作之間**拉齊，基準幀的 pivot 刻意維持 0.5。
> 所以 transform 離腳底有**半個可見身高**那麼遠（狂族皇家衛士實測 1.27 世界單位）。
> 另有 `BodyCenterWorldPos`（可見身體中心）與 `VisibleBodyHeight`。踩過見 [PROBLEMS.md](PROBLEMS.md) **G13**。
> （`BlobShadow` 沒這個問題，它走影子錨點——**別因為影子看起來對就以為 transform 是腳底**。）

> **左右翻面**由 `MonsterController` 控 `SpriteRenderer.flipX`（依玩家方向），與幀無關——所以走路圖只要畫「面朝一個方向」即可，和主角同規則。

> **每隻怪自動有的東西**（不必在 CSV 或 prefab 做任何事）：`BlobShadow` 腳下影子（見 [SHADOW.md](SHADOW.md)）、
> `YSortByFeet` 依腳底排序、**常駐體光**——`MonsterSpawner.SpawnMonster` 會掛一盞很小的陰冷青白 `LightSource`，
> 讓怪在暗地圖裡看得見輪廓（見 [ATMOSPHERE.md](ATMOSPHERE.md)〈怪物常駐體光〉；prefab 自帶 `LightSource` 的怪不會被覆蓋）。

### 碰撞框（自動貼合「圖的不透明像素」）

route B 怪物的碰撞框是一個**貼合 sprite 不透明像素的 `BoxCollider2D`**（不是整張畫布、也不是「以最大邊為半徑的圓」）——所以**圖的透明空白邊不會撐大碰撞範圍**。瘦長的鬼魂只有看得到的身體那塊會被碰到，不會「離很遠就被判定到」。

- 取代表幀（idle 第一幀）的不透明範圍算出 box（沿用家具用的 `MapSpriteLoader.GetAlphaLocalBox`），會隨怪物 `Scale` 一起縮放、和畫面上的圖對齊。
  - `GetAlphaLocalBox` 回傳的 `LocalBox` 在 2026-08-18 加了 **`canvas`** 欄（整張畫布的世界尺寸）。
    **要算「可見內容佔畫布的比例」一定要除它，不能假設畫布是 256px**——玩家的腳底錨點 pivot 就靠它
    （見 [BLOODLINE.md](BLOODLINE.md) §2）。目前所有角色圖剛好都是 256×256，漏除會**靜默算對**，
    哪天丟一張 512px 的進來才會爆。怪物這邊只用 `size`/`offset`，不受影響。
- **整體大小** → 調 CSV 的 `Scale`。**碰撞鬆緊**（box 比可見範圍外擴多少）→ 調 `MonsterController.HitboxPadding`（預設 0.2 世界單位）。
- 牆壁阻擋、子彈命中、接觸傷害（`EnemyContactDamage` 的幾何判定）全部共用這個 box。
- ⚠⚠ **同一隻怪的所有動作必須是同一個畫布尺寸——這是硬契約，不是美觀建議**（2026-09-22 血淚，見 [PROBLEMS.md](PROBLEMS.md) **F28**）：
  腳底對齊的 pivot 補償要拿「基準幀（idle 第一幀）的腳底離畫布中心多遠」去補其他幀，
  兩個像素值**必須量自同一張畫布**。ZhaYu 的 idle 256px、walk 換成 500px 之後，
  walk 整組幀**往下位移 1.38 世界單位**（超過半個身高）⇒ 走路時怪瞬移貼到玩家身上、停下又彈回、對話框離圖很遠。
  **換素材時先比對畫布尺寸**（`ls` ＋ 一行 PIL），不一致就先統一。

---

## 第一個範例：ZhaYu（已遷成 route B）

ZhaYu 已從「prefab + Animator + 切片 sheet」遷成 route B：
- 圖切到 `GameAssets/Modules/Tutorial/Monsters/SequenceImage/ZhaYu/idle/`（1 張）、`/walk/`（8 張，由原 4000×500 sheet 切成 8 格）。
- `MonsterData.csv` 的 ZhaYu：`PrefabPath` 留空、`AnimFPS=8`。
- 沒有 `attack/` → 進遊戲只發呆＋走路（驗證防呆）。要試攻擊動畫就加一個 `ZhaYu/attack/` 資料夾、重跑 Sync。

> 舊的 `ZhaYu.prefab` / `Monsters_ZhaYu.controller` / 舊 sheet 仍留著但已不被使用（route B 不經它們）；確定新流程沒問題後可自行清掉。

---

*建立於 2026-06-26：怪物改走 route B（程式逐格動畫、地圖素材管線載圖、零 prefab/Animator/clip）。量產＝丟圖到 `Monsters/SequenceImage/<怪名>/<state>/` ＋ CSV 加一列；attack 可選、防呆。主角之後也會改走同套（血統換外型）。*
