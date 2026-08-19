# 底部操控列 HUD ＋ 液體血球 (Bottom Control Panel ＋ Liquid Orbs)

> 返回 [文件總覽](README.md)
>
> HP/MP 數值來源見 [COMBAT.md](COMBAT.md)（`CombatStats`）；UI 底層框架見 [UI_SYSTEM.md](UI_SYSTEM.md)。
>
> **狀態：✅ 完成、實機驗證通過（2026-07-16）。** 取代舊的左上角血/魔條 [HudPanel](UI_SYSTEM.md)（舊檔保留未刪、只是不再開啟）。

Diablo 風的底部操控列：整條石雕框（燃燈佛、法輪、血瓶槽）＋ 左 **HP 紅球**、右 **MP 藍球**。兩顆血球是**著色器即時畫的液體**——液面依血量升降、受擊/耗魔時左右搖晃再回穩，滿血時液面持續微微波動。全程式建構、零 prefab/Inspector 接線（同專案風格）。

---

## 1. 元件

| 檔案 | 角色 |
|---|---|
| `Assets/Resources/Shaders/LiquidOrb.shader` | 液體球著色器 `Custom/LiquidOrb`（Built-in 管線、掛 uGUI RawImage）。液面線＝依 `_Fill` 的水平線＋兩道正弦漣漪＋`_Slosh` 傾斜/上下晃；再疊球面明暗、液面亮邊、內部流動噪訊。亮度/高光/描邊/液面亮邊都是參數。 |
| `Assets/Scripts/UI/LiquidOrb.cs` | 單顆血球元件：建 RawImage＋material，用「阻尼彈簧」在 C# 算搖晃量灌進 `_Slosh`。`Init(liquid,deep,label)`＋每幀 `SetStats(cur,max)`。滑鼠懸停在圓形範圍內顯示「label cur/max」數字。時間走 `unscaledDeltaTime`（暫停時仍微動）。 |
| `Assets/Scripts/UI/Panels/BottomHudPanel.cs` | HUD 面板：載框圖、把兩顆球擺在量到的圓心、**兩格血瓶槽鏡像顯示背包綁定的藥水（icon＋剩餘數量，訂閱 `InventorySystem.OnChanged` 即時更新）**。特性同舊 HudPanel（HUD 層、不暫停、不擋輸入、不遮罩、不入 ESC 堆疊、換場景保留）。 |
| `Assets/Resources/UI/BottomControlPanel/BottomControlPanel_Bg.png` | 框圖素材（2172×724，Sprite）。 |

由 `PlayerController.Start` 開啟：`UIManager.Instance.Open<Dipan.UI.BottomHudPanel>()`。

---

## 2. 液體球怎麼運作

- **資料**：每幀讀玩家身上的 `CombatStats`（`Health/MaxHealth`、`Mana/MaxMana`），球的液面平滑追上「當前/上限」比例。
- **搖晃（阻尼彈簧）**：液面每次變動給一個衝量 → 彈簧把它拉回、阻尼讓它收斂，這個帶正負的搖晃量灌進著色器 `_Slosh`，液面就左右傾斜＋上下微晃再回穩。受擊、耗魔、喝瓶都會觸發。
- **靜止微動**：著色器的正弦漣漪永遠存在（很小），所以滿血時液面也在微微波動。
- **懸停數字**：滑鼠移到球上（`ICanvasRaycastFilter` 把方形命中框限縮成圓形，角落不觸發）在球上方顯示真實當前值，受擊時即時跳動。

---

## 3. 版面座標（框圖像素，原圖 2172×724）

> ⚠️ 框圖的紅/藍球是**實心畫進去、不是鏤空**。所以液體球畫在框**之上**、剛好蓋住實心球、停在 socket 邊緣（不必重切圖）。

- 紅球圓心 `(210, 350)`、藍球圓心 `(1980, 350)`、半徑 `115`。
- 血瓶槽內框 `133×140`，中心 `(994, 412)`（左＝鍵1）與 `(1164, 412)`（右＝鍵2）。**鏡像顯示背包藥水格綁定的藥水**（icon＋背包剩餘數量），只呈現、不互動——拖放/綁定都在背包做。
- 框不透明內容 y 範圍 `[109, 606]`（用來對齊螢幕底）。
- 螢幕呈現：`DisplayWidth = 1180` 等比縮放、底部置中。

以上皆為 `BottomHudPanel.cs` 上方常數。

---

## 4. 可調參數（都不寫死，實機微調用）

**顏色**（`BottomHudPanel.cs`）：`HpLiquid / HpDeep / MpLiquid / MpDeep`。目前是配合暗場景調過的暗紅、深藍寶石。

**亮度旋鈕**（`LiquidOrb.cs` 上方；為了在全暗場景不刺眼）：

| 參數 | 現值 | 作用 |
|---|---|---|
| `Brightness` | 0.72 | 整體亮度倍率（想更暗就降） |
| `Gloss` | 0.18 | 球上白色高光點強度（設 0＝完全無高光、最沉） |
| `RimStrength` | **0** | 球周圍白色描邊（**已定案去掉**；要回復設 0.26） |
| `SurfStrength` | 0.18 | 液面亮邊強度 |

**搖晃手感**（`LiquidOrb.cs`）：`ApproachSpeed`（液面追上速度）、`SloshImpulse`（晃多大）、`SloshStiffness`（回穩多急）、`SloshDamping`（多快靜下）、`SloshMax`（上限）。

---

## 5. 踩坑 / 注意

- **框圖實心球、非鏤空** → 液體球疊在框上蓋住它（見 §3）。若之後改成鏤空框，改成把液體球畫在框「之下」即可。
- **玻璃反光素材 `BottomControlPanel_Bubble.png` 不能用**：圓內部是一整片不透明灰（alpha 255、RGB ~190），疊上去會把液體蓋成灰。已改由著色器自生高光，該圖**已棄用/刪除**。
- **框圖匯入 `maxTextureSize` = 2048**，但原圖 2172 寬會被 Unity 縮一點；想更銳可調 4096＋Compression None（同專案 UI 去壓縮慣例，見 [PROBLEMS.md](PROBLEMS.md) G2/G3）。
- 著色器 `_T` 用 `unscaledTime`：HUD 不暫停，但開背包（暫停）時液面仍要微動，所以不吃 `Time.timeScale`。
- **液體球著色器別硬寫 `ZTest Always`**：自繪 material 硬寫 `ZTest Always` 會無視畫布 `sortingOrder`、穿透畫到上層視窗（背包）之上——開背包時血球會蓋在背包上、連背包壓底的半透明黑幕都蓋不住。已改成標準 uGUI 的 `ZTest [unity_GUIZTestMode]`，血球才會跟隨層級（HUD 層 `sortingOrder=0` < Window 層 `100`，正確被背包蓋住）。

---

## 6. 待接

- ✅ **血瓶槽已接玩法**：鏡像顯示背包綁定的藥水、按 **1/2** 喝（見下方 §7 與 [INVENTORY.md](INVENTORY.md) 藥水系統）。藥水冷卻未做。
- 技能列（框中段）先留空，按鈕內容未定。

---

## 7. 藥水格（喝藥）— 與背包對齊

底部兩格血瓶槽是**背包藥水格的鏡像顯示**：綁定/拖放/解綁全部在背包介面做（**左鍵**點背包裡的藥水＝綁定，見 [INVENTORY.md](INVENTORY.md) 的「藥水系統」），這裡只讀同一份資料畫出來。
⚠ 這兩格**沒有點擊處理**，點它不會喝。喝的方式有兩種：數字鍵 **1／2**，或在背包裡對藥水按**右鍵**——兩條路都走同一支 `Inventory/ItemUse.cs`。

- **資料來源**：`InventorySystem.GetPotionSlot(0/1)` = 綁定的藥劑**種類 ID**（跟背包一起存檔）。左格＝索引 0＝鍵 **1**、右格＝索引 1＝鍵 **2**，與背包藥水格一一對應。
- **顯示**：每格畫該藥水 icon ＋背包剩餘數量（`CountOf`）；訂閱 `InventorySystem.OnChanged`，設定/更換/喝掉/歸零時即時同步（某種類用完 → 該格自動清空）。實作在 `BottomHudPanel.MakePotionDisplay/RefreshPotions`。
  > ⚠ **icon 一律走 `ItemIcons.Apply`，不要直接讀 `data.Icon`**（2026-08-07 改）。那裡面會做大小正規化（`UI/IconFit.cs`）——物品 icon 的透明留白差很多（量過的 30 張從 41% 到 100%），直接讀 `data.Icon` 的話留白多的藥水圖會小得很誇張。見 [INVENTORY.md](INVENTORY.md)、[PROBLEMS.md](PROBLEMS.md) E10。
- **喝**：由自動生成的常駐 `PotionHotkeys` 在遊戲中（非開背包/暫停）按 1/2 觸發：套效果（`HealHp/HealMp`）＋扣背包一瓶＋在玩家身上播喝藥特效（`PlayerController.PlayDrinkPotionVfx`，隨玩家外型大小縮放，見 [VFX.md](VFX.md)）。滿血/滿魔也照喝照扣。
- **邊界**：HUD 這兩格是唯讀顯示、不放互動元件（不攔截點擊）。

---

*建立於 2026-07-16：底部操控列 HUD ＋ 液體血球（著色器液體、阻尼彈簧搖晃、懸停數字、暗場景調色、去描邊定案）。取代舊左上角 HudPanel。*
*2026-07-16 更新：血瓶槽接上玩法——鏡像顯示背包綁定的藥水（icon＋數量、訂閱 OnChanged 即時同步），按 1/2 喝（`PotionHotkeys`）；修正液體球著色器 `ZTest Always` → `ZTest [unity_GUIZTestMode]`（原本會穿透蓋住背包）；血瓶槽座標改 `133×140` @ `(994,412)/(1164,412)`。*
