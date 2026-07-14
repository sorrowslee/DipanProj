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
- 螢幕座標覆蓋層（HUD 層、不擋輸入、不暫停），沿用 `PlayerHintPanel` 的 `WorldToScreen` 跟頭頂做法；可同時多個氣泡各跟一隻怪。
- 底板：`Resources/UI/InGame/InGame_TalkBg1`、`Bg2` 兩張水墨泡泡**隨機輪流**（載不到自動退回程序生成的圓角底板）。文字用內建動態字體（中文靠系統字體 fallback，同 TalkPanel）。
- **避邊定位**：預設右上；靠畫面左緣→右上、靠右緣→左上；靠上緣→改到腳下。方向在開口那一刻決定、之後固定。底板依方向**鏡像**（尾巴永遠指回怪物），文字不鏡像。兩張圖的尾巴尖與內文奶油區座標記在 panel 常數裡。
- 顯示約 2 秒淡出；**怪物死亡立即消失**。
- 版面常數：`BubbleWidth`、`MaxFont/MinFont`（best-fit 自動縮放）。

## 相關檔案
| 檔案 | 角色 |
|---|---|
| `Assets/Data/MonsterData.csv` | 句子1~句子4 欄（TextAsset，直接載入、**不需 Sync**） |
| `Scripts/AI/MonsterData.cs` | `SpeechLines` 欄位 + `MonsterSpeechLine`（在 MonsterSpeech.cs） |
| `Scripts/AI/MonsterSpawner.cs` | 解析句子欄（`ParseSpeechLine`，含 %門檻） |
| `Scripts/AI/MonsterController.cs` | `SpeechLines` / `IsAwareOfPlayer` / `IsBoss`；Start 時有句子才掛 `MonsterSpeech` |
| `Scripts/AI/MonsterSpeech.cs` | 說話元件：頻率/機率/隨機起始/blood 門檻挑句 |
| `Scripts/UI/Panels/MonsterSpeechPanel.cs` | 對話框呈現：底板輪流、避邊、鏡像、best-fit |

## 雷點
- 底板 PNG 的 Texture Type 建議設 Sprite；若是 Default，載入有後備會自動轉，不會開天窗。
- 這功能不動怪物 sprite 那條 StreamingAssets 管線、底板走 `Resources`，所以**不需要跑 Sync**。
