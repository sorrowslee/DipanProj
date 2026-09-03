# 角色影子 (Blob Shadow)

> 返回 [文件總覽](README.md)｜角色見 [ACTORS_AND_COMBAT.md](ACTORS_AND_COMBAT.md)

俯視角的「腳下橢圓影子」：在角色腳下放一個半透明深色橢圓，畫在角色之下、地面之上，每幀跟著角色走。**只要出現在遊戲中的角色（玩家／怪物）都會有影子。**

為什麼用 blob shadow 而不是即時光照投影：本專案是 **Built-in 算繪管線、沒有 2D 燈光**（見 [ATMOSPHERE.md](ATMOSPHERE.md)），blob shadow 是俯視角最常見、最省、最穩的做法，零光照依賴。

## 做法

* 元件 `Assets/Scripts/BlobShadow.cs`：掛在角色上即可。玩家由 `PlayerController.Start`、怪物由 `MonsterController.Start` 各自 `AddComponent<BlobShadow>()`（已接好）。
* 影子是**獨立 GameObject**（不是角色的子物件）——避免被角色的 `flipX` 翻轉或 `localScale` 縮放二次影響；每幀 `LateUpdate` 把影子移到角色腳下。角色銷毀時 `OnDestroy` 自動清掉影子。
* 影子圖是**程序生成的柔邊圓**（中心實、邊緣淡的 alpha 貼圖，白色靠 `SpriteRenderer.color` 染成黑半透明），整個遊戲**共用一張**（static 快取）。零 prefab、零美術。
* 排序設在角色 `sortingOrder` 之下一階（畫在角色腳下、地面之上）。

### 定位：影子錨點表（2026-09-03 起，資料驅動）

**為什麼**：AutoSprite 產的序列圖**沒有把腳錨在畫布的固定點**——主角 idle 的 25 幀腳都在畫布中心左邊 25px、walk 卻兩腳跨在中心兩側；
狼人 idle 腳底離畫布底 27px、walk 是 46px。以前 X 一律用 `transform.position`（＝畫布中心）、Y 用 idle 第 0 幀量一次，
所以「idle 偏、走路剛好準」。純程式猜腳在哪已證明有反例（披風、長袍、爪子；[PROBLEMS.md](PROBLEMS.md) **E28**），
所以改成 **程式先算出八成正確的預設值 → 存成表 → 看拼圖不對的角色手改、標 manual、之後永不覆寫**。

**表**：`Assets/Data/ShadowAnchorTable.csv`，一列＝一個「角色/動作」：

| 欄 | 意義 |
|---|---|
| `Key` | `Characters/<血統>/<動作>` 或 `Monsters/<怪名>/<動作>`（＝資料夾路徑，不分大小寫） |
| `AnchorX` | 影子中心 X（px，相對畫布中心，+右，**未翻面**的來源圖方向） |
| `AnchorY` | 影子中心 Y（px，從畫布底往上＝可見腳底） |
| `WidthPx` | 影子寬（px；`BlobShadow.WidthFactor` 再乘上去） |
| `Source` | `auto`＝工具算的、重算會被覆寫；**`manual`＝手改的、永不覆寫** |
| `Frames` / `CanvasW` / `CanvasH` | 算時的幀數與畫布尺寸（幀數變了＝換過圖，「只算新的」也會重算 auto 列） |

**工具**（`Assets/Editor/ShadowAnchorTool.cs`，Project Tools → 角色）：
- **計算影子錨點（只算新的、出檢視圖）**：遞迴掃 `GameAssets/**/(Characters|Monsters)/SequenceImage/<角色>/<動作>/*.png`，表裡沒有的算進去、已有的不動；
  並在專案根 `TempImage/ShadowAnchors/`（gitignored）輸出**每個角色一張拼圖**——每列一個動作（idle/walk/attack/dead）、每列 4 幀，
  灰橢圓＝影子、紅十字＝錨點、淡線＝畫布中心。**不用進遊戲就能一次看全部角色。**
- **重算所有 auto 影子錨點（manual 不動）**：換了一批圖時用。
- **影子錨點檢視圖（只出圖、不改表）**：手改完表想確認時用。
- 掃的是 GameAssets 原檔，新資料夾不必先 Sync；但進遊戲看圖仍要 Sync Map Assets。

**演算法**（`ShadowAnchorMath`，**唯一一條路徑**，工具與 runtime 退路都呼叫它）：在「最底一帶」（可見高度最底 15%）把有像素的欄連成段＝找腳；
兩段以上 → 取**最低的兩段**當兩隻腳（披風、破布條也會垂進帶內，但它們的底比腳高），X＝兩段中心的中點、Y＝兩段最低列的平均（＝兩腳之間的地面接觸點）；只有一段（走路兩腿交叉、衣襬連住兩腿、遠腳太高）→ X＝可見框中心、Y＝該段最低列再往上抬可見高×6%（跟兩段時「兩腳底平均」的高度一致；不抬的話影子壓在近腳鞋底、半顆吊在角色下面看起來偏低偏外）。
每幀算完**取全幀中位數**；Width＝max(兩腳跨距, 可見框寬)（＝舊版的寬；只用腳跨距時瘦長角色會縮成一小顆）。**一個動作一組固定值、不逐幀**——逐幀會讓影子跟著跨步左右滑，比偏一點更怪。
**躺姿（`dead`）另一套**：整個剪影都貼在地上、沒有腳可找——只取序列**最後 1/3** 的幀（前段是倒下過程），X 用剪影中心、Y 在剪影底緣往上 min(高×25%, 寬×15%)（正中心會被身體遮掉上半、看起來飄在身後；跪坐那種又高又窄的死姿要靠寬的上限壓住）、寬＝剪影寬，影子從身體下緣露出來。
⚠ 一版只取最底 6% 的水平平均，影子會壓在**近腳**上（3/4 俯視遠腳比近腳高、不在帶內），作者實機一看主角 idle 就偏——影子要在兩腳之間，不是在最低的那隻腳下。

**遊戲端**：`PlayerAnimator`／`MonsterAnimator` 實作 `IShadowAnchorSource`，Setup 時把各動作的錨點取好（表 → 沒列就當場用同一條演算法算）；
`BlobShadow` 每幀問「目前動作」的錨點，用**當前 sprite** 的 PPU／pivot（bodyScale 的腳底 pivot 自動跟上）／`lossyScale`／`flipX` 換成世界位置，
換動作時位移用 `AnchorSmoothTime`（0.08 秒）平滑、轉身直接跳。沒有錨點來源（舊 Animator 怪）走舊路：Start 量一次 idle 幀。

**手改流程**：見下一節〈手動調整影子（作者操作手冊）〉。
**要作者做一次**：GameManagers 掛 `ShadowAnchorTableProvider`、把 `Assets/Data/ShadowAnchorTable.csv` 拖進去（沒掛時編輯器會直接讀檔案並印提醒，**build 裡則整個退回自動計算**——結果一樣、只是沒有手改的覆寫）。

## 手動調整影子（作者操作手冊）

演算法已經定版（2026-09-03 五輪後停手），**之後某個角色的影子不對，一律改表，不再動演算法**。整個流程不用碰程式：

### 1. 找到那一列

打開 `Assets/Data/ShadowAnchorTable.csv`（Excel／Numbers／VS Code 都行，存檔保持 UTF-8、逗號分隔），找 `Key`：

| 角色 | Key 長這樣 |
|---|---|
| 主角某血統的某動作 | `characters/<血統資料夾名>/<idle\|walk\|attack\|dead>`，例 `characters/maojiang/idle` |
| 怪物／NPC 的某動作 | `monsters/<怪名資料夾名>/<動作>`，例 `monsters/family_father/walk` |

Key 不分大小寫，就是 `GameAssets/**/SequenceImage/` 底下的資料夾路徑。**每個動作各一列**：idle 對了不代表 walk 對，要分別看。

### 2. 三個數字的意思（都是**像素、以 256×256 原圖畫布為準**，跟遊戲裡的縮放無關）

```
         畫布 256×256
   ┌──────────────────────┐
   │                      │
   │        角色           │   AnchorX：影子中心離「畫布垂直中線」幾像素
   │       ／│＼           │            0＝中線、正＝往右、負＝往左
   │        │             │            ⚠ 一律以「原圖面向」為準（AutoSprite 的圖面朝右），
   │       ／ ＼           │              遊戲裡角色轉身時程式會自動鏡射，不用管左右
   │      ●───●           │
   │   ↑ AnchorY：影子中心離「畫布底邊」幾像素（0＝貼齊畫布底，越大越高）
   └──────────────────────┘
   WidthPx：影子橢圓的寬（像素）；高固定是寬的一半（BlobShadow.HeightRatio）
```

- **影子要往左／右**：改 `AnchorX`（以原圖面向為準；圖面朝右時「往腳前方」＝加、「往背後」＝減）。
- **影子要往上／下**：改 `AnchorY`（要高＝加）。站姿的合理值是「近腳鞋底往上約可見身高的 6%」（256 圖約 10~12px），
  也就是影子中心落在**兩腳之間**——主角 idle 是 41（鞋底 30）可當參考。
- **影子要大／小**：改 `WidthPx`。站姿≈角色可見框寬；躺姿≈剪影寬。
- 每次改 5~10px 就看得出差別；一格＝256px，10px 約等於遊戲裡 0.04 格。

### 3. 把 `Source` 改成 `manual`

這一格不改，下次跑「重算所有 auto」會被蓋回去。改成 `manual` 後兩個工具都不會再碰這列（只有你手動改回 `auto` 才會重算）。
`Note` 欄可以寫為什麼（例：`披風垂到腳邊、自動算會偏左`），給以後的自己看。

### 4. 看結果

- **不進遊戲**：`Project Tools → 角色 → 影子錨點檢視圖（只出圖、不改表）`，開 `TempImage/ShadowAnchors/<Characters|Monsters>_<角色>.png`：
  每列一個動作（idle / walk / attack / dead 順序）、每列 4 幀，灰橢圓＝影子、紅十字＝錨點、淡線＝畫布中線。這張圖用的就是表裡的值，改完表重出即可。
- **進遊戲**：直接 Play。錨點表是 `ShadowAnchorTableProvider` 拖進去的 TextAsset，**改完 CSV 要讓 Unity 重新匯入**
  （視窗切回 Unity 會自動 Refresh；沒反應就對 CSV 右鍵 Reimport），Play 時會重新載表。

### 5. 常見狀況速查

| 看到 | 多半是 | 改 |
|---|---|---|
| 影子壓在其中一隻腳上、另一隻腳浮在影子外 | Y 太低（落在近腳鞋底） | `AnchorY` +8~12 |
| 影子整個往角色背後偏（無論面向哪邊都偏同一側） | 披風／背包／爪子把自動算的 X 帶偏 | `AnchorX` 往腳的方向調 |
| 影子太小、像一顆點 | 自動算只抓到一隻腳 | `WidthPx` 改成可見框寬左右 |
| 躺姿影子飄在身體後面 | Y 太高 | `AnchorY` 往下（躺姿合理值≈剪影底往上 15~25% 高） |
| 換了新圖之後又歪了 | 表裡是舊圖算的 | `Source` 改回 `auto` 再跑「重算所有 auto」，或直接手填 |
| walk 對、idle 偏（或反過來） | 兩個動作各一列，只改了一列 | 兩列都看 |

### 6. 新角色進來

放好序列圖 → `Project Tools → 角色 → 計算影子錨點（只算新的、出檢視圖）` → 看那張新拼圖 → 有問題照上面手改。
沒跑工具也能玩：遊戲會用同一條演算法當場算，只是沒有手改覆寫。

## 可調參數（`BlobShadow` Inspector / 程式預設）

| 欄位 | 預設 | 說明 |
|---|---|---|
| `ShadowColor` | 黑 alpha 0.3 | 影子顏色與濃淡 |
| `WidthFactor` | 1.1 | 影子寬 = 角色世界寬 × 此值 |
| `HeightRatio` | 0.5 | 影子高 / 寬（越小越扁、俯視感越強） |
| `VerticalOffset` | 0 | 腳底再往下(正)/上(負)微調 |
| `SortingOrderBelow` | 1 | 比角色 sortingOrder 低幾階 |
| `AnchorSmoothTime` | 0.08 | 錨點路換動作時位移的平滑秒數（0＝不平滑） |

> 想讓影子更淡/更大/更扁，調上面這幾個值即可（改 `BlobShadow.cs` 上方預設，或在 Inspector 對個別角色調）。

## 給新角色加影子

任何之後出現的角色（NPC、Boss…），在它的初始化加一行即可：

```csharp
if (GetComponent<BlobShadow>() == null) gameObject.AddComponent<BlobShadow>();
```

## 尺寸何時重算（2026-08-18；錨點路已改成每幀依動作取寬）

* （舊路）影子大小在 `Start` 量一次，之後**由呼叫端主動 `BlobShadow.Refresh()`** 重量。錨點路每幀依目前動作的 `WidthPx` 換算，寬變了才改 localScale；`Refresh()` 仍要叫（換血統後重抓錨點來源、位移不平滑直接跳）。
* 目前唯一的呼叫點是 `PlayerController.RefreshBodyScaledVisuals()`——換血統／改體型倍率時觸發。
  不重量的話，換成 1.5 倍體型的血統後腳下會頂著一塊明顯偏小的影子。
* **刻意不每幀更新**：重量要掃一次 alpha 像素，每幀做太貴。角色顯示大小只有在換外型時才會變，
  改成「事件驅動」剛好。之後若做出「跳躍時影子變小」這種需求，再叫一次 `Refresh()` 即可。

## 限制 / 之後可加

* 目前是固定橢圓；之後若要「跳躍時影子變小、離地拉開」之類，可在 `BlobShadow` 依角色狀態調 scale / 位置。
* 大型可破壞地上物（家具）目前**沒有**影子（只角色有）；要的話也可掛 `BlobShadow`。

---

*建立於 2026-06-25：腳下橢圓 blob shadow（程序生成柔邊圓、共用快取、獨立物件跟隨、依角色寬度自動縮放），玩家與怪物自動掛上。2026-09-03：定位改走影子錨點表（每角色每動作一組、工具自動算＋手改覆寫）。*
