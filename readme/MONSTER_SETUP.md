# 怪物量產設定 (Monster Setup — 路線 B：程式逐格動畫)

> 返回 [文件總覽](README.md)｜怪物 AI／受擊見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)｜地圖素材管線見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)、[DRAMA.md](DRAMA.md)（同套 catalog 載入）

怪物採「**一套共用、只換外觀＋套 CSV 數值**」的量產方式：**不必每隻怪拉 prefab、拉 Animation、切圖**。外觀由程式逐格播放（route B），圖走地圖素材管線（catalog + StreamingAssets），跟劇情大圖／頭像同一套。

為什麼不用 Unity Animator：怪物圖走「執行期載入」的地圖素材管線，而 Animator 的 AnimationClip 只能綁編輯期匯入的 sprite 資產，兩者不相容——所以改成程式每幀換 `SpriteRenderer.sprite`（專案既有 `AnimatedMapObject` / `VfxInstance` 也是這樣做）。

---

## 量產一隻新怪（重複這幾步）

1. **放圖**：在 `GameAssets/Modules/<關卡>/Monsters/SequenceImage/<怪名>/` 底下，每個動作開一個子資料夾放單張幀圖：
   - `idle/`（**必備**）、`walk/`（**必備**）、`attack/`（可選，沒放就不會有攻擊動畫）。
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
└─ attack/  attack_01.png ...              ← 可選
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
| `BrainType` | 目前 `Chase`（追玩家）；未來擴充其他 AI |
| `InvincibleTimeMs` / `KnockbackThreshold` / `KnockbackPercent` | 受擊反應（見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)） |
| **`PrefabPath`** | **route B 留空**。只有要沿用「自帶 Animator 的舊 prefab」才填（向下相容） |
| **`AnimFPS`** | **新增**：程式動畫播放幀率，留空＝8。走路會再依實際速度連動（防腳滑） |

---

## 防呆（有動畫才演、沒有就略過）

- `MonsterAnimator` 開場只載「放得到圖」的動作；`Has(state)` = 那個動作有沒有幀。
- 要播一個沒圖的狀態時，自動退回 **Attack→Walk→Idle**；一張圖都沒有就整個不動（不會把畫面清空）。
- 所以「**有 `attack/` 才會演攻擊、沒有就只走路/發呆**」是天生行為，加新怪不必動程式。
- ⚠️ **攻擊動畫 ≠ 攻擊邏輯**：目前「在攻擊範圍內（`AttackRange`）且有 attack 圖」就播攻擊動畫，傷害仍走既有的**接觸傷害**（`EnemyContactDamage`）。真正的「會攻擊的 AI ＋ 攻擊判定/傷害」是另一塊，之後再接。
- **死亡 / 受傷動畫**尚未納入（目前死亡直接銷毀）。要加時照 idle/walk/attack 同模式擴充 `MonsterAnimator` 的狀態詞彙。

---

## 運作 / 相關檔案

- `Assets/Scripts/AI/MonsterSpriteLibrary.cs`：載一次 catalog，依「`<怪名>/<state>`」索引怪物動作素材、給幀（`GetFrames`/`Has`）。
- `Assets/Scripts/AI/MonsterAnimator.cs`：程式逐格播放（Idle/Walk/Attack）、防呆退回、走路 fps 跟速度連動。
- `Assets/Scripts/AI/MonsterController.cs`：決定狀態（範圍內＋有 attack 圖→攻擊；移動→走路；靜止→發呆）並驅動 `MonsterAnimator`；無怪名/有 Animator 時退回舊 Animator。
- `Assets/Scripts/AI/MonsterSpawner.cs`：`PrefabPath` 留空時**程式建一隻通用怪**（零 prefab），外觀靠 `MonsterAnimator` 載圖。
- 同步管線（加新素材分類要三處一起改，見 [PROBLEMS.md](PROBLEMS.md) C3）：`Assets/Editor/MapAssetSyncTool.cs`、`Assets/Scripts/Map/MapIO.cs`、`Tools/sync_map_assets.sh`——皆已加 `Monsters/SequenceImage` 掃描。

> **左右翻面**由 `MonsterController` 控 `SpriteRenderer.flipX`（依玩家方向），與幀無關——所以走路圖只要畫「面朝一個方向」即可，和主角同規則。

### 碰撞框（自動貼合「圖的不透明像素」）

route B 怪物的碰撞框是一個**貼合 sprite 不透明像素的 `BoxCollider2D`**（不是整張畫布、也不是「以最大邊為半徑的圓」）——所以**圖的透明空白邊不會撐大碰撞範圍**。瘦長的鬼魂只有看得到的身體那塊會被碰到，不會「離很遠就被判定到」。

- 取代表幀（idle 第一幀）的不透明範圍算出 box（沿用家具用的 `MapSpriteLoader.GetAlphaLocalBox`），會隨怪物 `Scale` 一起縮放、和畫面上的圖對齊。
- **整體大小** → 調 CSV 的 `Scale`。**碰撞鬆緊**（box 比可見範圍外擴多少）→ 調 `MonsterController.HitboxPadding`（預設 0.2 世界單位）。
- 牆壁阻擋、子彈命中、接觸傷害（`EnemyContactDamage` 的幾何判定）全部共用這個 box。

---

## 第一個範例：ZhaYu（已遷成 route B）

ZhaYu 已從「prefab + Animator + 切片 sheet」遷成 route B：
- 圖切到 `GameAssets/Modules/Tutorial/Monsters/SequenceImage/ZhaYu/idle/`（1 張）、`/walk/`（8 張，由原 4000×500 sheet 切成 8 格）。
- `MonsterData.csv` 的 ZhaYu：`PrefabPath` 留空、`AnimFPS=8`。
- 沒有 `attack/` → 進遊戲只發呆＋走路（驗證防呆）。要試攻擊動畫就加一個 `ZhaYu/attack/` 資料夾、重跑 Sync。

> 舊的 `ZhaYu.prefab` / `Monsters_ZhaYu.controller` / 舊 sheet 仍留著但已不被使用（route B 不經它們）；確定新流程沒問題後可自行清掉。

---

*建立於 2026-06-26：怪物改走 route B（程式逐格動畫、地圖素材管線載圖、零 prefab/Animator/clip）。量產＝丟圖到 `Monsters/SequenceImage/<怪名>/<state>/` ＋ CSV 加一列；attack 可選、防呆。主角之後也會改走同套（血統換外型）。*
