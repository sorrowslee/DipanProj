# 多語系 / 語言表 (Localization)

> 返回 [文件總覽](README.md)
>
> **狀態：✅ 程式完成。** 預設中文、支援英文。
> - **字串**（2026-07-22）：全遊戲走 `Language.GetText(id)`，本體在 `LanguageTable.csv`。
> - **圖片型文字**（2026-08-19）：「畫成圖的字」走 `UI/Texts/<語言>/`，由 `LocalizedArt` 依當前語言解析、缺圖退回繁中。見下方 §圖片型文字。

全遊戲要顯示給玩家的字串，統一從 **`Language.GetText(id)`** 取，字串本體放 **`Assets/Data/LanguageTable.csv`**（欄位 `id,cn,en`）。這樣做的目的：字串集中一處、程式不寫死中文、之後畫面設定切英文（或再加語言）不必動任何程式。

## 快速上手

```csharp
using Dipan.Localization;

string s = Language.GetText(1001);        // 取當前語言的字串（預設中文）
Language.SetLanguage(Lang.EN);            // 切英文（GetText 即時生效、免重載）
Language.SetLanguage(Lang.CN);            // 切回中文
bool ok = Language.Has(1006);            // 有沒有這個 id（不含佔位）
```

- **預設語言＝中文**（`Language.Current = Lang.CN`）。切語言只要 `SetLanguage`，`GetText` 每次即時讀 `Current`，切換立即反映、不必重載表。
- **英文欄留空時自動退回中文**（還沒翻的字串不會變空白）。
- **找不到 id → 回傳 `[lang:id]` 佔位字串**（例：`[lang:1001]`），方便一眼抓漏 / 抓「表沒載到」。

## 圖片型文字（把字畫進 PNG 裡的那些）

有些字沒辦法用字型畫——標題、牌匾上的毛筆字、按鈕上的美術字。這些**翻譯不了**，
只能每種語言各出一張圖。做法是「**換資料夾、不換檔名**」：

```
Assets/Resources/UI/Texts/
  tw/ClearStagePanel_Title.png     ← 繁中（母版，一定要最齊全）
  en/ClearStagePanel_Title.png     ← 英文（還沒畫就先不放）
```

### 三條規則

1. **凡是「畫成圖的字」都放 `UI/Texts/<語言>/`。** 純美術的框、底版、按鈕底不算——
   那些不隨語言變，留在各自的面板資料夾。
2. **同一張圖在每個語言資料夾裡必須同名**，不要加 `_tw` / `_en` 尾綴。
   整套機制就是「路徑換語言資料夾、檔名不動」，加了尾綴就得為每種語言各寫一次檔名對照。
   （`TitlePanel_TW` / `TitlePanel_EN` 原本就是那樣，2026-08-19 一起改名成兩邊都叫 `TitlePanel_Title`。）
3. **缺圖自動退回母版（繁中）。** 所以英文版可以一張一張慢慢補，沒畫的顯示中文、不會開天窗，
   而且 Console 會提示「沒有 en 版，先用 tw 版頂著」（每張只講一次）。

### 程式怎麼寫

**呼叫端照舊寫邏輯路徑，不要自己拼語言資料夾**：

```csharp
LoadSprite("UI/Texts/ClearStagePanel_Title");   // → 實際載 UI/Texts/tw/ClearStagePanel_Title
```

解析發生在載圖函式裡（`Dipan.Localization.LocalizedArt.ResolveExisting`）。
全專案 **7 支 `LoadSprite` 都已經接上**（`UIBuilder`、`ResultPanel`、`SelectScriptPanel`、
`GachaPanel`、`ForgingPanel`、`SaveSlotPanel`、`MonsterSpeechPanel`）＋ `ItemIcons`，
所以任何面板都能直接載。**「哪一種語言」只有 `LocalizedArt` 知道**，加語言不用回頭改呼叫端。

### ⚠ 切語言時面板要重建

面板是「建一次、之後只顯示/隱藏」，而圖是在 `OnBuild` 當下載進 `Image` 的。
所以 `UIManager` 訂閱了 `Language.OnLanguageChanged`，**切語言時把所有快取的面板整個丟掉**，
下次開啟重建。不這麼做的話字串會變、圖不會變——最糟是**同一張卡上英文的關卡名配中文的「領取」鈕**
（有些圖每次重畫時載、有些 OnBuild 載一次）。半套比全舊更難看也更難查。

副作用：切語言會關掉當時開著的面板（含設定面板本身）。刻意的取捨——語言是罕見的一次性操作。

⚠ 新增「會顯示圖片型文字」的面板時不用做什麼，但**不要自己另外快取 Sprite**；
真要快取，key 必須用**解析後**的路徑（`ItemIcons` 就是這樣做的），
不然切語言會直接命中上一個語言那張。

### 命名的一個接縫

語言表的欄位叫 `cn`、列舉是 `Lang.CN`，但圖片資料夾叫 **`tw`**（內容是繁體中文）。
兩邊命名不一致是既成事實，**`LocalizedArt.FolderOf(Lang)` 是唯一的對照點**，不要在別處再寫一份。

### 目前的內容

| 檔名 | tw | en |
|---|---|---|
| `TitlePanel_Title` | 燃燈劫 | LAMPBLACK |
| `BloodlinePanel_Title` | 血統轉換 | BLOODLINE SHIFT |
| `ClearStagePanel_Title` | 通關結算 | STAGE CLEAR |
| `ClearStagePanel_DeadTitle` | 死亡結算 | DEATH |
| `ClearStagePanel_GainItemText` | 獲得獎勵 | REWARDS |
| `ClearStagePanel_ReturnText` | 返回廣場 | RETURN |
| `Text_Gain` | 領取 | CLAIM |
| `Text_StageName_RedBridalGown` | 紅嫁衣 | Red Bridal Gown |
| `BossInfo_Warning` | 強敵現身 | （作者已出圖） |

**兩種語言都齊了**（2026-08-19；`BossInfo_Warning` 2026-09-04 補上，用在 boss 開戰前奏，見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) 的 `bossIntro`）。英文用**全大寫**（英文遊戲 UI 的主流慣例）；
唯一例外是遊戲副標 `Rebirth of Ruin` 用 Title Case，與全大寫的主標形成層次。

## 怎麼測英文版

**改 `Language.DefaultLanguage` 一個常數**，進 Play 就是英文：

```csharp
// Scripts/Localization/Language.cs
public const Lang DefaultLanguage = Lang.EN;   // 正式版要是 Lang.CN
```

⚠ **不要只改 `Current` 的初始值**——`ResetForPlayMode` 每次進 Play 都會把 `Current` 設回
`DefaultLanguage`，只改初始值等於白改（實際踩過，見 [PROBLEMS.md](PROBLEMS.md) **D18**）。

**目前還沒有玩家可用的切換 UI**：全專案沒有任何一處呼叫 `SetLanguage`，設定面板裡也沒有語言選項。
管線是通的（切下去面板會正確重建），只差觸發它的介面。之後做的時候要一併決定：
要不要記進存檔、要不要開場先問一次、切換時被關掉的面板要不要重開。

### 還沒處理的

- **`VfxEffects/Warnning/symbol_warning_text_001_01..32`（32 幀 WARNING）**：Boss 開戰資訊用，
  走 `VfxTable.csv` id 14 的序列圖管線，**不經過 `LocalizedArt`**。它本身是英文素材，
  所以現在是「中文版沒有中文」。要做中文版得先讓 VfxTable 的路徑也能依語言解析。
- **`DEATH` 比 `STAGE CLEAR` 大一號**（實測畫出來的字高 184 vs 165，繁中版是對齊的 164/166）。
  兩張佔同一個位置、玩家會交替看到，建議讓 DEATH 用同樣的字高、自然短一截。純美術問題。
- **英文字的「份量」比中文輕**：版面吃固定寬度，英文比較寬 ⇒ 同寬度下字比較矮
  （領取→CLAIM 高度 −19%、紅嫁衣→Red Bridal Gown −23%）。全部都在容器內、不影響功能；
  想拉齊的話**不用重畫，把 en 那幾張的畫布上下裁掉一些**即可（留白變少＝同寬度下字變大）。

---

## 資料表：`Assets/Data/LanguageTable.csv`

id 分段：1001–1099 新手教學、2001–2099 血統系統、4001–4099 鍛造、5001–5099 選擇存檔、
**6001–6099 標題畫面**（2026-08-19 新增；6001＝開始遊戲／START GAME）。

欄位 `id,cn,en`。逗號分隔、**支援雙引號包覆**（引號內的逗號不分欄、`""`＝一個雙引號），所以英文常含逗號時用 `"..."` 包起來即可；字串內要換行用字面 `\n`（與 ItemTable 慣例一致）。

```
id,cn,en
# ── 新手教學：柴房佛燈（1001–1099）──
1001,走過去，撿起佛燈（靠近按 F）,Walk over and pick up the lamp (get close and press F)
1002,按 F 撿起佛燈,Press F to pick up the lamp
...
```

- **`#` 開頭或第 0 欄不是整數的列一律跳過**（可放註解列分段）。
- **id 分段慣例**（避免撞號、方便擴充）：每個功能自己拿一個千位段，在表裡用 `#` 註解列標出分段。目前已用：**1001–1099 ＝ 新手教學（柴房佛燈 1001–1006、儲藏室藥水 1007–1010）**、**4001–4099 ＝ 鍛造介面**、**5001–5099 ＝ 選擇存檔畫面**。（2001–／3001– 還沒人用。）

## 程式架構

| 檔案 | 角色 |
|---|---|
| `Assets/Scripts/Localization/Language.cs` | 靜態類別，**唯一取用入口**。`GetText`/`SetLanguage`/`Has`/`Reload`；懶漢載入一次、快取進 `Dictionary<int,Row>`；自帶引號感知 CSV 解析（同 `ItemDatabase` 慣例）。 |
| `Assets/Scripts/Localization/LanguageTableProvider.cs` | 被動 provider MonoBehaviour，持有 `languageCSV`（TextAsset）。掛在場景、把 CSV 拖進去。 |
| `Assets/Data/LanguageTable.csv` | 字串本體。 |

**為什麼要 provider**：資料表放在 `Assets/Data`（不在 `Resources/`），`Resources.Load` 找不到、也不會自動打進 build——非 Resources 資產要「有人在場景裡拿著它的序列化參照」才進得了 build。`Language` 又是靜態類別、場景上沒有可拖檔的物件，所以由 `LanguageTableProvider` 持有 CSV 參照，`Language` 載入時 `FindObjectOfType<LanguageTableProvider>()` 取用；找不到才退回 `Resources.Load<TextAsset>("Data/LanguageTable")`（一般沒有）。這跟 `ItemTableProvider`／`DramaTableProvider`／`ScreenFxTableProvider` 是**同一套路**（見 [PROBLEMS.md](PROBLEMS.md) I7）。

## Unity 接線（改表 / 新增字串前必看）

1. 場景（`MainScene` 的 GameManagers）上要有一個 **`LanguageTableProvider`** 元件，且把 `Assets/Data/LanguageTable.csv` **拖進 `languageCSV` 欄**。沒掛 / 沒拖 → 字串全變 `[lang:id]`（Console 會印警告指引）。
2. 改了 CSV 內容：TextAsset 需重新匯入（右鍵 CSV → Reimport）才會更新。
3. ⚠️ **已關 Domain Reload 的殘留坑**：`Language` 有 `ResetForPlayMode()`（把 `_rows` 設 null、下次重讀），由 `PlayModeStaticReset` 每次進 Play 最早期呼叫。**若曾在 provider 還沒接好時 Play 過一次**，static 會快取成空表、之後接好也不重載 → 字串照樣 `[lang:id]`；接好 provider 後**務必重編譯＋重新 Play 一次**讓 `ResetForPlayMode` 生效。（完整見 [PROBLEMS.md](PROBLEMS.md) I7 / I2。）

## 加一批新字串的維護點

1. `LanguageTable.csv` 新增列（挑一個沒用過的 id 段，開頭放 `#` 註解標明用途）；英文欄可先留空（會退回中文）。
2. 程式裡改成 `Language.GetText(<id>)` 取用，別再寫死中文字面。
3. 進 Unity Reimport CSV、確認 provider 有拖檔 → Play 驗證（沒接到會顯示 `[lang:id]`）。

---

*建立於 2026-07-22：多語系系統（`Language.GetText`／`LanguageTable.csv`／`LanguageTableProvider`，預設中文支援英文），第一批字串＝柴房佛燈新手教學提示（1001–1006）。*

*2026-08-01：新增 5001–5099「選擇存檔畫面」段（標題／欄位／空欄位／新建遊戲／進入遊戲／刪除角色／刪除確認／`{0}周目`／存檔損毀）。*
