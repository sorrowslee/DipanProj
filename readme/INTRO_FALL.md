# 序章墜落動畫 (Intro Fall：持續墜落深淵)

> 返回 [文件總覽](README.md)｜UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)｜產圖見 [AI_IMAGE_GEN_GUIDE.md](AI_IMAGE_GEN_GUIDE.md)

開場序章漫畫（一張張分鏡）播完後，主角墜落深谷——這段**墜落**用程式動畫表演：角色在前景墜落，背景是無限往上捲動的岩壁峽谷，途中「穿越」到異空間（色調轉詭異色＋放射速度線＋時空扭曲），最後鏡頭停止跟隨、角色越來越小沒入深淵、淡出載入正式遊戲場景。

全程式建構（零 prefab、零美術依賴；速度線/光霧/暗角等貼圖 runtime 程序生成），風格同專案既有的 `VfxManager` / `BlobShadow`。**漫畫播放本身不在此範圍**，本檔只談墜落動畫。

---

## 0. 放在獨立的 Intro 場景（不蓋在主場景上）

墜落動畫放在**獨立的 `Intro` 場景**，播完 `SceneManager.LoadScene` 進主遊戲場景 `MainScene`，而**不是**蓋在 `MainScene` 上。原因：主場景一啟動，玩家生成／地圖載入／怪物 AI／HUD 全部會跑起來；蓋在上面得想辦法把這些全壓住再解開，容易留下殘留狀態。獨立場景天然乾淨，且 `UIManager`（`DontDestroyOnLoad`）本來就為多場景設計，這條路已鋪好。

Intro 場景之後也可長成「標題畫面 / 新遊戲 vs 繼續 / 選角」入口。

---

## 1. Unity 接線（建立 Intro 場景）

1. `File → New Scene`，存成 `Assets/Scenes/Intro.unity`。
2. 場景保留預設的 Main Camera；新增一個空物件 `[IntroFall]`，掛上 **`IntroFallController`**（立繪欄位留空會自動從 Resources 載）。
3. **Intro 與 `MainScene` 兩個場景都要在 build 裡，且 `MainScene` 排第 0、`Intro` 第二**（2026-07-03 加入標題流程後開機要停在標題；`BuildScript.cs` 的 `options.scenes` 已如此設定。排錯順序的症狀與原因見 [PROBLEMS.md](PROBLEMS.md) **A10**。本檔早期寫「Intro 排第 0」已過時，2026-08-21 修正）。
4. 按 Play：一進場景自動播墜落 → 播完自動載 `MainScene`（落在 Tutorial_Cave，見 [MAP_SYSTEM.md](MAP_SYSTEM.md)；由 `MapManager.startModule = Tutorial` 決定）。

> 不需要手動接 Canvas：控制器在 `Awake` 自己建一整套 Screen-Space Overlay Canvas 與所有圖層。

---

## 2. 三段時間軸（鏡頭依時間自動切換）

依序播放，秒數都可在 Inspector 改：

| 段 | 預設秒數（欄位） | 鏡頭 | 表現 |
|---|---|---|---|
| 1 側面墜落 | `SideSeconds` = 3 | `Story_ActorFall_Side` | 整片岩壁峽谷無限往上捲、散佈短碎條速度線；色調**正常**（尚未穿越） |
| 2 正面墜落 | `FrontSeconds` = 3 | `Story_ActorFall_Front` | 放射速度線（俯衝）＋時空扭曲 shader；色調在 `ColorShiftSeconds` 內**轉成詭異色**（穿越時空） |
| 3 正面加速收尾 | `FinaleSeconds` = 2 | 正面 | 速度線爆衝、角色**越來越小沒入深淵**＋淡出黑幕 → 觸發 `OnComplete` / 載入下一場景 |

`Total = Side + Front + Finale`；切鏡頭由 `Tick()` 依 `_t` 自動決定（前 `SideSeconds` 秒側面，之後正面），不需手動指定。

---

## 3. 視覺圖層（由後到前）

1. **變色漸層背景**（`_bg`）：色盤隨時間平滑推移；側面段壓在 `NormalTone`（正常暗色），穿越後才漸染成 `Palette` 的詭異色。
2. **山壁背景**（`_rockBG`，側面段）：一張岩壁圖鋪滿整個畫面、**無限縱向往上捲動**（`uvRect.y` 遞減＝畫面往上＝角色往下墜）。切正面時淡出，露出虛空。
3. **側面速度線**（散佈短碎條）：見 §4。
4. **角色立繪**：側面 / 正面各一張，墜落擺盪；收尾縮小沒入。
5. **正面放射速度線**＋**時空扭曲 shader**：見 §5。
6. **光霧 / 暗角**：預設**關閉**（`ShowColorFog` / `ShowVignette`＝false；寬螢幕下方形貼圖會變成橢圓暗框/光暈，故預設不開）。
7. **白閃 / 黑幕**：穿越打點白閃；收尾淡出黑幕。

---

## 4. 側面速度線＝散佈的「短碎條」

不是一整條從上到下的直線，而是**到處散佈、每條只佔畫面高約 5~13%、上下交錯、不規律**的短碎條（兩端柔淡，像動漫速度線），靠快速往上流動表現墜落。畫在山壁之上、角色之後（不蓋臉）。貼圖 `MakeStreak` 程序生成（y 模數環繞 → 可無縫垂直平鋪捲動）。

| 欄位 | 預設 | 說明 |
|---|---|---|
| `ShowSideSpeedLines` | false | 側面速度線開關 |
| `SideSpeedDensity` | 0.3 | 碎條**數量**（≈ density×120 條），越小越少。**改完要重新 Play**（貼圖進場時生成） |
| `SideSpeedStrength` | 0.16 | **濃淡**，越小越淡 |
| `SideSpeedScroll` | 1.5 | 側面速度線**捲動速度**，越小越慢。**獨立計算**，不吃正面的 `SpeedLineFlowScale` |

---

## 5. 正面速度線＋時空扭曲（穿越異空間）

正面段是放射狀速度線從消失點往外不斷放大流動（俯衝感），再疊一支 shader 做漩渦/漣漪扭曲，像時空被攪動（速度線本身保留，只是會波動旋擰）。

* Shader：`Custom/IntroWarp`（`Assets/Resources/Shaders/IntroWarp.shader`），UI 用、對貼到的放射速度線做時間動畫的 UV 漩渦＋漣漪。
* 欄位：`EnableWarp`（開關）、`WarpStrength`（強度 0~2）。扭曲只作用在正面段。
* ⚠️ 若進正面整片變**洋紅**＝shader 沒編過（見 [PROBLEMS.md](PROBLEMS.md) E3）。先看 Console 紅字，或暫時關 `EnableWarp` 退回乾淨放射線。

---

## 5.5 正面：旋轉卍字（神聖 → 墮落）

正面墜落時，於角色**後方**加一個緩緩旋轉的**佛教卍字（左旋＝逆時針，象徵法輪/神聖）**＋柔光暈，隨「穿越進度」由**金色（神聖）漸變成紫色（墮落／入異界）**，半透明當背景光暈、不蓋住角色；側面段不出現，收尾沒入時跟著淡出，並帶輕微「呼吸」脈動。

* 圖來源：優先用 **`Assets/Resources/InitialStory/Manji.png`**（毛筆草書、白字去背 PNG，染色才準），用 `Texture2D` 載入＋`Sprite.Create`（不挑 import 類型）；沒圖時退回**程式生成**的卍字（粗筆、毛筆提按、邊緣毛躁、腳尖收鋒，純程式 `MakeManji`）。也可直接拖圖到 `ManjiImage` 欄覆寫。
* 顏色跟著 `_weird`（穿越進度）由 `ManjiGold` → `ManjiPurple` 線性內插，與既有色調穿越同步。

| 欄位 | 預設 | 說明 |
|---|---|---|
| `ShowManji` | true | 開關 |
| `ManjiRotateSpeed` | 32 | 旋轉速度（度/秒，正＝逆時針） |
| `ManjiSizeFraction` | 0.98 | 大小 = 螢幕高 × 此值 |
| `ManjiAlpha` | 0.55 | 不透明度上限（半透明光暈） |
| `ManjiGold` / `ManjiPurple` | 金 / 紫 | 起始（神聖）／結束（墮落）色 |
| `ManjiImage` | 空 | 自備卍字圖；留空＝自動抓 `Resources/InitialStory/Manji`，再退回程式生成 |
| `ManjiTintImage` | true | 用自備圖時是否仍套金→紫染色（圖須白色去背）；關＝保留圖原色只淡入淡出 |

> 卍字是**佛教左旋卍（逆時針）**，對應「神聖→墮落」寓意與燃燈古佛世界觀，與納粹符號（右旋、45°傾斜）方向/風格不同。

---

## 6. 色調穿越

* `NormalTone`：側面段的正常暗色（現實感）。
* `ColorShiftSeconds`（預設 1.5）：切到正面後，幾秒內由 `NormalTone` 漸染成 `Palette` 的詭異色（穿越時空的訊號，與山壁淡出同步）。
* `Palette`：留空用內建異世界色（午夜藍→靛紫→暗洋紅→血色→深青→異綠）；`ColorHoldSeconds` 控每色停留秒數。
* `SpeedLineFlowScale`（正面速度線流動倍率）、`SpeedRampMax`（整段下墜加速倍率，收尾再爆衝）。

---

## 7. 收尾與串接

| 欄位 | 預設 | 說明 |
|---|---|---|
| `AutoLoadNextScene` | true | 墜落播完自動載下一個場景 |
| `NextSceneName` | `MainScene` | 下一個場景名（**需在 build 場景清單裡**） |
| `OnComplete`（事件） | — | 播完（收尾結束）時觸發；想自己接「漫畫→墜落」「墜落→生玩家」就關掉 `AutoLoadNextScene`、訂閱此事件 |

公開 API：`Play()`（重播）、`Skip()`（直接收尾）、`SetView(FallView)`。測試鍵：**R** 重播、**Esc** 直接收尾（`SkipKey` / `ReplayKey`，正式上線可改）。

---

## 8. 角色大小（側面 / 正面分開）

| 欄位 | 預設 | 說明 |
|---|---|---|
| `SideCharHeightFraction` | 0.29 | 側面段角色高度 = 螢幕高 × 此值（側面較小，讓岩壁壯觀） |
| `FrontCharHeightFraction` | 0.58 | 正面段角色高度（俯衝特寫較大） |

切鏡頭時 `ConfigureView` 會自動套用對應大小。

---

## 9. 換素材

* **墜落立繪**：`Assets/GameAssets/Main/InitialStory/Story_ActorFall_Front.png` / `Story_ActorFall_Side.png`（需**去背**＝透明通道）。已複製一份到 `Assets/Resources/InitialStory/` 供 `Resources.Load` 載入；也可在 `FrontSprite` / `SideSprite` 欄直接拖圖覆寫。
* **岩壁圖**：`Assets/GameAssets/Main/InitialStory/Story_RockWall.png`（覆蓋即換），或在 `WallTexture` 欄拖圖。`RockScale` 調縮放。
  * **無縫接圖**：要「一張接一張完全看不出接縫」，圖最好**上下可平鋪**（頂邊接得上底邊）。若非上下無縫，每循環一圈會出現一條橫接縫；可改成「上下鏡像平鋪」消接縫（尚未做，需要再說）。

---

## 10. 注意事項 / 踩過的坑

* **改程式預設值不會更新場景上已存在的元件**：Unity 對「已序列化的欄位」保留舊值，改 C# 的 `= 預設值` 只影響**之後新加的元件**。所以調某個已存在欄位時要**直接在 Inspector 改**；若需要強制換新預設，可右鍵元件 → Reset，或把欄位**改名**（新名字會吃新預設）。本系統迭代時就靠「改名換新預設」避開這個雷（例如 `WallChasmWidth` → `WallWidthFrac` → 最後改回整片背景 `ShowRockBackground`）。
* **uvRect 捲動方向**：`uvRect.y` 遞增＝畫面內容往下＝看起來像往上飄；要角色**往下墜**，背景要**往上移**＝`uvRect.y` **遞減**。
* **速度線「碎條」而非整條**：整條從上到下的直線看起來像 bug；要的是散佈、短、上下交錯、不規律的碎條（§4）。
* 全螢幕單一純色（尤其洋紅）≈ shader 沒編過，見 [PROBLEMS.md](PROBLEMS.md) E3。

---

## 11. 相關檔案

* `Assets/Scripts/Intro/IntroFallController.cs` — 控制器（全部邏輯：圖層建構、時間軸、山壁、速度線、色調、扭曲、旋轉卍字、收尾、程序貼圖生成）。
* `Assets/Resources/Shaders/IntroWarp.shader` — 正面時空扭曲 shader（`Custom/IntroWarp`）。
* `Assets/Resources/InitialStory/` — 墜落立繪 `Story_ActorFall_Front/Side`、岩壁 `Story_RockWall`、卍字 `Manji.png`（控制器讀這份；原圖在 `GameAssets/Main/InitialStory/`）。

---

*建立於 2026-06-28：序章墜落程式動畫（獨立 Intro 場景、三段時間軸、岩壁無限捲動背景、散佈短碎條側面速度線、正面放射速度線＋`Custom/IntroWarp` 扭曲、色調穿越、收尾縮小沒入＋載入下一場景）。*
*2026-06-29 更新：正面加旋轉卍字（金→紫、自備圖 `Resources/InitialStory/Manji` 或程式生成）；下一場景改 `MainScene`（落 Tutorial_Cave）；Intro+MainScene 都需在 build 場景清單。*
