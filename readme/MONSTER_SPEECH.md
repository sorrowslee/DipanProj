# 怪物說話（頭上對話框）

> 怪物在遊戲中發現玩家後，頭上會不定時冒出對話框講一句話（水墨泡泡底板＋文字）。台詞資料驅動：填在 `MonsterData.csv` 的「句子1~句子4」欄。boss 講得更頻繁。全程式、零 prefab。

## 什麼時候讀
- 加/改怪物台詞、血量門檻。
- 調說話頻率、對話框大小/位置、換底板美術。

## 資料：MonsterData.csv 的「句子1~句子4」
每隻怪最多 4 句，每格格式二選一：
- 一般句：`我要殺了你~~`（一直可講）。
- 帶血量門檻：`30%: 你真的惹怒我了`（血量比例 **≤ 30%** 才解鎖；半/全形冒號皆可）。

發現玩家後，每隔一段時間從「目前血量已解鎖」的句子裡**隨機挑一句**講。例：句3=30%、句4=10% → 血量 >30% 只會挑 1、2；10%~30% 挑 1~3；<10% 全開。

> ⚠️ **句子內不能用半形逗號 `,`**（CSV 靠它分欄），要用全形「，」。

## 頻率 / 行為（`Scripts/AI/MonsterSpeech.cs` 最上面常數）
- `SpeakIntervalSeconds`（一般怪平均間隔，預設 10）、`IntervalJitter`（±抖動去同步）、`SpeakChance`（時間到真的開口的機率 0.55＝有時不說）、`FirstDelayMin/Max`（第一次開口的隨機起始時機）、`BubbleDuration`（顯示秒數 2）。
- **boss 加乘**：`BossIntervalMul`（間隔減半＝頻率兩倍）、`BossSpeakChance`（0.9 幾乎必說，避免劇情要角整場沉默）。boss 判定 = `MonsterController.IsBoss`（在 `Initialize` 依 BrainType 設；新增 boss brain 記得在該 case 設 `IsBoss = true`）。
- 「發現玩家」= `MonsterController.IsAwareOfPlayer`（偵測到玩家後黏著為 true）。

## 對話框（`Scripts/UI/Panels/MonsterSpeechPanel.cs`）

> **⚠ 2026-08-22 起這個面板是通用元件、不再綁怪物**：內部只記「一個 `Transform` 目標 ＋ 一個『還在不在』的判斷委派」，所以劇情演出的 `bubble` 步驟也用同一套（見 [CUTSCENE_DIRECTOR.md](CUTSCENE_DIRECTOR.md) §8）。
> 兩個入口：`Speak(MonsterController mc, text, duration)`（**怪物端零改動**，薄包裝、死亡即消失）與 `Speak(Transform target, text, duration)`（劇情演員、玩家…，目標被銷毀即消失）。
> 頭頂/腳下座標三段優先序：**玩家**→`PlayerController.FeetWorldPos`/`VisibleBodyHeight`（見 [PROBLEMS.md](PROBLEMS.md) **E14**）；**有 Collider2D**→碰撞框上下緣（怪物走這條，行為完全不變）；**只有 SpriteRenderer**→`bounds`（劇情 npc 演員沒有 Collider2D，一定要有這段）。

- 螢幕座標覆蓋層（HUD 層、不擋輸入、不暫停），沿用 `PlayerHintPanel` 的 `WorldToScreen` 跟頭頂做法；可同時多個氣泡各跟一個目標。
- 底板：`Resources/UI/InGame/InGame_TalkBg1`、`Bg2` 兩張水墨泡泡**隨機輪流**（載不到自動退回程序生成的圓角底板）。文字用內建動態字體（中文靠系統字體 fallback，同 TalkPanel）。
- **避邊定位**：預設右上；靠畫面左緣→右上、靠右緣→左上；靠上緣→改到腳下。方向在開口那一刻決定、之後固定。底板依方向**鏡像**（尾巴永遠指回怪物），文字不鏡像。兩張圖的尾巴尖與內文奶油區座標記在 panel 常數裡。
- 顯示約 2 秒淡出；**怪物死亡（或劇情演員被銷毀）立即消失**。同一個目標再開口會先移除舊氣泡，不會疊字。
- 版面常數：`BubbleWidth`、`MaxFont/MinFont`、`LineHeightRatio`。

### 字級是自己算的，**不用 uGUI 的 best-fit**（2026-08-22 改）

**規則：同一句話永遠是同一個字級；`\n` 只決定「在哪裡斷行」，不影響大小。**

換掉 best-fit 的兩個理由（都會讓同一句台詞忽大忽小）：

1. **手動換行會讓字變大**。best-fit ＝「找一個塞得下的最大字級」，把「這個給你吧，就剩兩張了」改寫成「這個給你吧`\n`就剩兩張了」只是想控制斷句，但兩行各 5 字「塞得下更大的字」，於是同一句話大了一號。
2. **抽到不同底板也會不一樣大**。兩張水墨泡泡的奶油內文區不一樣（220×98 vs 200×83），而底板是隨機輪流的。

現在的算法（`MonsterSpeechPanel.ComputeFontSize`）：

- **參考框固定**＝兩張底板中**較小**的那個奶油區（200×83），所以抽到哪張都同一個字級，而且大的那張一定塞得下。
- 中日韓字元寬 ≈ 1 個字級、ASCII/數字 ≈ 0.55；行高 ≈ 字級 × 1.15。
- **有手動 `\n` 時先跑第一輪**：要求每一段剛好佔一行（尊重作者排的斷句），從 `MaxFont` 往下找第一個塞得下的。連 `MinFont` 都塞不下才落到第二輪自動折行。
- 沒有 `\n` 就直接第二輪：依總字數估要折成幾行。
- 估不準時 `verticalOverflow = Overflow` 兜底，寧可溢出一點也不要被裁掉。

### ✍️ 填台詞時的字數感（現行版面）

參考框寬 200px、字級上限 40 下限 22 ⇒ **一行的字數決定字級**：

| 一行字數 | 字級 |
|---|---|
| ≤ 5 | 35（上限，最大也就這麼大——高度只放得下兩行） |
| 6 | 33 |
| 7 | 28 |
| 8 | 25 |
| 9 | 22（下限） |
| ≥ 10 | 22 且會再自動折行 |

所以**每行控制在 5~6 個字**看起來最一致；一句話裡兩段長度差很多時，字級是由**最長那一段**決定的。想整體放大就調 `BubbleWidth`（底板等比變大、參考框跟著變寬），只調 `MaxFont` 沒有用——那條上限現在是被高度卡住的。

## 相關檔案
| 檔案 | 角色 |
|---|---|
| `Assets/Data/MonsterData.csv` | 句子1~句子4 欄（TextAsset，直接載入、**不需 Sync**） |
| `Scripts/AI/MonsterData.cs` | `SpeechLines` 欄位 + `MonsterSpeechLine`（在 MonsterSpeech.cs） |
| `Scripts/AI/MonsterSpawner.cs` | 解析句子欄（`ParseSpeechLine`，含 %門檻） |
| `Scripts/AI/MonsterController.cs` | `SpeechLines` / `IsAwareOfPlayer` / `IsBoss`；Start 時有句子才掛 `MonsterSpeech` |
| `Scripts/AI/MonsterSpeech.cs` | 說話元件：頻率/機率/隨機起始/blood 門檻挑句 |
| `Scripts/UI/Panels/MonsterSpeechPanel.cs` | 對話框呈現：底板輪流、避邊、鏡像、best-fit。**通用元件**，怪物與劇情演員共用 |
| `Scripts/Cutscene/CutsceneDirector.cs` | 劇情的 `bubble` 步驟：文字走 `Language.GetText(langId)`，呼叫 `Speak(Transform,…)` |

## 雷點
- 底板 PNG 的 Texture Type 建議設 Sprite；若是 Default，載入有後備會自動轉，不會開天窗。
- 這功能不動怪物 sprite 那條 StreamingAssets 管線、底板走 `Resources`，所以**不需要跑 Sync**。
