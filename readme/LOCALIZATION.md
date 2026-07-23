# 多語系 / 語言表 (Localization)

> 返回 [文件總覽](README.md)
>
> **狀態：✅ 程式完成（2026-07-22）。** 預設中文、支援英文，全遊戲字串走同一個取用入口。第一批字串＝柴房佛燈新手教學（1001–1006）。

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

## 資料表：`Assets/Data/LanguageTable.csv`

欄位 `id,cn,en`。逗號分隔、**支援雙引號包覆**（引號內的逗號不分欄、`""`＝一個雙引號），所以英文常含逗號時用 `"..."` 包起來即可；字串內要換行用字面 `\n`（與 ItemTable 慣例一致）。

```
id,cn,en
# ── 新手教學：柴房佛燈（1001–1099）──
1001,走過去，撿起佛燈（靠近按 F）,Walk over and pick up the lamp (get close and press F)
1002,按 F 撿起佛燈,Press F to pick up the lamp
...
```

- **`#` 開頭或第 0 欄不是整數的列一律跳過**（可放註解列分段）。
- **id 分段慣例**（避免撞號、方便擴充）：**1001–1099 ＝ 新手教學：柴房佛燈**。之後每個功能自己拿一個百／千位段（例：設定選單 2001–、劇情提示 3001–…），在表裡用 `#` 註解列標出分段。

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
