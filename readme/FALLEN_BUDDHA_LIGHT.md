# 墮落佛光實驗存檔（紫色佛光 ／ 旋轉卍字 ／ 照明開關）

> 返回 [文件總覽](README.md)
>
> **一句話：2026-08-17 試了「佛光改紫＋加旋轉卍字＋拿掉照明」三件事，最後全部退回原狀，但機制與素材都留著。這份文件記錄「留下了什麼、放在哪、怎麼開回來」。**

遊戲目前的佛光就是**原本的暖金光圈**，名字是「佛光」，`LightRadius=3.5` 的提燈照明也照常運作。下面講的全部是**已備妥但沒啟用**的東西。

---

## 一、留下了什麼

| 東西 | 位置 | 現況 |
|---|---|---|
| 紫色佛光貼圖（兩版） | `readme/variants/` | 備用，未進 Assets |
| 換色腳本 | `readme/variants/recolor_aura.py` | 可產任意顏色 |
| 旋轉符號層 | `GroundEffectTable` 第 12 欄 `SigilPath` | 機制完成，全表留空 |
| 特效照明 | `GroundEffectTable` 第 13 欄 `LightRadius` | 機制完成，全表留空 |
| 疊色教訓 | [PROBLEMS.md](PROBLEMS.md) E12 / E13 | 已歸檔 |

> ⚠️ **紫色貼圖刻意放在 `readme/variants/` 而不是 `Assets/Resources/`。**
> `Resources/` 底下的圖會**無條件烘進每個 build**（見 [PROBLEMS.md](PROBLEMS.md) A9），放兩張沒在用的貼圖進去等於白白讓 build 變大。放 readme 底下 Unity 完全看不到，也不會生 `.meta`。

---

## 二、事情的來龍去脈

### 起點：兩個同心同色的圓
裝備佛光時畫面上會出現**兩個同心圓**，作者形容「脫褲子放屁」：

* **外圈** ＝ `ItemTable` 8 的 `LightRadius=3.5`，走 `AtmosphereController` 的提燈光圈，顏色 `LightSource.DefaultWarm` = RGB(1.00, 0.78, 0.52)
* **內圈** ＝ `GroundEffectTable` 2（`RenderMode=Glow`）的 `Radius=1.2` 光環，貼圖平均色 RGB(172, 136, 79)

色相幾乎相同、只差半徑 2.9 倍。外圈是「裝備著就有」、內圈是「開火才有」，功能不同但**視覺讀不出差別**。

**真正的元兇是兩圓之間那段空白暗環**——中間沒東西，眼睛就只讀到「兩個圈」。

### 試過的三個方向（依序）

1. **內圈換紫、改名「墮落佛光」**
   敘事上站得住腳：紫是邪佛與卍字的代表色，而 `DramaTalkTable` 第 37 列邪佛親口說「**吾贈汝佛燈一盞，汝好自為之**」——燈本來就是邪佛給的，紫光是伏筆不是穿幫。
   → 名字與敘事沒問題，但沒解決重疊感。
2. **在空白環補一個緩緩旋轉的卍字**（沿用開場墜落的 `Resources/InitialStory/Manji.png`）
   → 兩層都用加色，實際亮度打平，卍字獨大把光環洗掉。
3. **卍字改成 alpha 混合的暗紫剪影、縮進佛光圓內**
   → 技術上對了（見下面的通則），但整體觀感作者不買單。

### 另一個獨立實驗：拿掉照明
把 `ItemTable` 8 的 `LightRadius` 清空，測「暗場景只靠佛光那張發光的圖照明」的手感。
→ 可見範圍只剩半徑 1.2、且加色圖的邊界偏硬，太緊，也退回了。

### 結論
作者判斷整個視覺方向「不太 ok、也不漂亮」，**全部還原**。`ItemTable` / `WeaponTable` / `buddhaLight_01.png` 三個檔現在與 git 完全一致。

---

## 三、怎麼把紫色佛光開回來

1. 把 `readme/variants/buddhaLight_01_violet.png`（或 `_purple.png`）複製成
   `DipanProj_Main/Assets/Resources/GroundEffect/buddhaLight/buddhaLight_01.png`
2. `GroundEffectInstance.cs` 的 `AuraIntensity` 由 `1.4` 提高到 **`2.4`**
   （紫的感知亮度只有暖橘的一半，不提高會看不見；理由見 [PROBLEMS.md](PROBLEMS.md) E12）
3. 想改名 → `ItemTable` / `WeaponTable` 第 8 列的 `Name`
   ⚠️ **教學文案 `LanguageTable` 1001–1006 叫「佛燈」可以不動**（玩家第一次拿到時還不知道它墮落，留伏筆）；
   ⚠️ **觸發點名 `柴房佛燈拾取` 絕對不能改**，它寫死在 `TutorialManager.cs` 的清單裡，改了教學不會啟動。

**兩版紫的差別**

| 檔案 | 顏色 | 增益 | 平均色 | 什麼時候用 |
|---|---|---|---|---|
| `buddhaLight_01_purple.png` | (0.62, 0.30, 0.95) | 1.32 | RGB(140, 68, 215) | 比較「正」的紫。疊在暖光地板上會偏粉紅 |
| `buddhaLight_01_violet.png` | (0.40, 0.16, 0.98) | 1.55 | RGB(103, 41, 252) | 紅通道壓低，**疊在暖光池上仍讀得出紫**。較推薦 |

要別的顏色就跑腳本（**務必從原始暖橘版換色，不要拿已染過的再染**）：
```bash
cd readme/variants
python3 recolor_aura.py \
  ../../DipanProj_Main/Assets/Resources/GroundEffect/buddhaLight/buddhaLight_01.png \
  out.png  0.40 0.16 0.98  1.55
```

---

## 四、怎麼把卍字開回來

`GroundEffectTable` 第 12 欄 `SigilPath` 填 `InitialStory/Manji`，程式一行都不用動。
完整說明（參數表、資料流、三條反直覺的設計）見 **[GROUND_EFFECT.md](GROUND_EFFECT.md) 的「背景旋轉符號層（SigilPath）」一節**。

收斂到的參數（`GroundEffectInstance.cs` 上方）：倍率 `0.95`、轉速 `32°/秒`逆時針、深紫近黑剪影 `(0.16, 0.05, 0.26)`、覆蓋率 `0.85`、走 **alpha 混合**、排序 `order + 1`。

---

## 五、怎麼把「照明」改成別的行為

* **恢復原狀（現況）**：`ItemTable` 8 的 `LightRadius` = `3.5`
* **完全沒有照明**：把上面那格清空。可見度只剩佛光那張圖，半徑 1.2、邊界偏硬
* **開火才有一圈真正的照明**：上面那格清空，改在 `GroundEffectTable` 2 的第 13 欄 `LightRadius` 填值
  （兩者不要同時開，否則會變成「常亮的提燈」＋「開火時再多一盞」兩層光）

完整說明見 **[GROUND_EFFECT.md](GROUND_EFFECT.md) 的「發光半徑（LightRadius）」一節**。

> **一個待決事項**：裝備的照明目前是**取最大值不是累加**——
> `AtmosphereController.PlayerEquippedLightRadius()`：`if (d.LightRadius > max) max = d.LightRadius`。
> 作者提過「以後有些道具可以增加照亮範圍，就能累加」，若要那樣得先把這裡改成累加。
> （設計上可討論：兩盞燈該讓你看更遠，還是只是更亮？）

---

## 六、⭐ 這一輪最值錢的東西：三條疊色通則

正典在 [PROBLEMS.md](PROBLEMS.md) **E12 / E13**，這裡只放摘要。**要疊任何發光效果之前先去讀那兩則。**

1. **加色圖層的 `_Intensity` 不等於實際亮度。**
   算式是 `col = 貼圖RGB × 貼圖alpha × 顏色rgb × 顏色a × _Intensity`，**貼圖自身的 alpha 會先乘一刀**。實例：佛光貼圖中心 alpha 只有 **0.549**、卍字是白色去背所以是 **1.0**——所以 `1.4` 對 `0.85` 看似差 1.6 倍，實際上兩層**完全打平**。比較強弱要把整串乘出來。

2. **兩個都靠「比較亮」被看見的圖層疊在同一位置是零和的。**
   本體調亮符號消失、符號調實本體消失，調 alpha 永遠跳不出這個循環（實際調了三輪才想通）。解法是**換維度**：一層加色發光、一層 alpha 混合的暗剪影（吃光）。⚠️ 暗剪影必須畫在光的**上面**，畫在下面會被加色直接填亮。

3. **加色（`Blend One One`）永遠做不出「不透明」。**
   它只讓底下變亮、遮不住任何東西，所以「把加色圖層 alpha 調到 1 讓它變實心」本質上做不到。要實心就得換 alpha 混合。

**附帶兩條**
* 加色的紫疊在**暖色**光池上會變粉紅（加色只能往上加，紅通道會累積）。要在暖光裡讀得出紫，紅通道得壓低。
* **別拿自己合成的暗場景估疊色參數**。實機的提燈光池比想像中亮得多（從截圖量到暖光池中心約 RGB 0.47/0.37/0.27），照合成圖調好的值一進遊戲就被洗掉。**要估就拿實機截圖量地板亮度再算。**

---

## 七、一個架構教訓

卍字第一版**寫死在 `RenderMode=Glow` 分支裡**，被作者一句話問破：

> 「我之後做無形力場，那個卍字還會出現嗎？」

會。因為卍字綁的是 `RenderMode=Glow`，而那比武器類型 `Mode=Aura` **低了兩層**：

```
WeaponTable → RecipeTable(Mode=Aura) → GroundEffectID → GroundEffectTable(RenderMode)
```

等於**汙染了 `Glow` 的語意**——它原本只是「加色發光＋明滅」，卻夾帶一個佛教符號，以後任何想要明滅發光的特效都會被迫吃到。改成獨立 CSV 欄位後才乾淨。

**「只有一個使用者，先寫死」在這個專案通常是錯的。** CSV 資料驅動是既定哲學，寫死會把語意黏在錯的層級上，而且不會當場報錯——半年後才會以「為什麼我的力場裡有個卍字」的形式爆出來。

---

*2026-08-17 建立。*
