# 互動系統 (Interaction：靠近按 F ＋ 拾取點 ＋ 掉落物)

> 返回 [文件總覽](README.md)｜劇情觸發點見 [DRAMA.md](DRAMA.md)｜背包見 [INVENTORY.md](INVENTORY.md)｜地圖/傳送見 [MAP_SYSTEM.md](MAP_SYSTEM.md)、[MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)｜UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)

玩家面向的「靠近 → 看提示 → 按 **F** → 互動」一條龍。一個 `InteractionManager` 統一管理所有可互動目標：**地圖編輯器放的觸發點**（道具拾取點、劇情觸發點）＋**地上掉落物**。三者共用同一套「找最近目標 → 顯示提示 → 按 F」邏輯，所以不會互搶 F 鍵或提示。

> 設計哲學沿用專案紀律：全程式建構、零 prefab/Inspector 接線（仿 `VfxManager` / `TeleportWatcher`）；資料層（背包）不認識互動、互動層不認識檔案。

---

## 三種互動目標

| 目標 | 來源 | 標示 | 按 F 的效果 |
|---|---|---|---|
| **道具拾取點** | 編輯器 `pickup` trigger（`itemId` + `count`） | 金黃星星 | `itemId×count` 進背包；滿了溢出的掉成地上掉落物 |
| **劇情觸發點** | 編輯器 `drama` trigger（`dramaId`） | 紫色星星 | 開啟劇情介面（見 [DRAMA.md](DRAMA.md)） |
| **地上掉落物** | 程式 `DropLoot()`（拾取溢出、未來怪物掉落） | 道具 icon 縮小放地上 | 撿回背包（部分撿取：吃得下多少算多少） |

**提示文字**：拾取＝「按 F 鍵拾取 ＜道具名＞」、劇情＝「按 F 鍵」。

> 注意：拾取點與**劇情 Type 1（大圖+文字）**是**靠近按 F 觸發**。早期拾取點曾做成「踩到自動撿」，後改為與掉落物一致的「按 F」。
> **例外：劇情 Type 2（頭像對話）改成「碰到自動觸發」**（踏進區域 `dramaTouchRadius` 內就播，不需按鍵）——觸發方式依該 dramaId 在 DramaTable 的 `Type`，由 `InteractPoint.autoTrigger` 標記，見 [DRAMA.md](DRAMA.md)。

---

## 執行單元

| 檔案 | 角色 |
|---|---|
| `Assets/Scripts/Combat/InteractionManager.cs` | **大腦**：常駐單例（仿 InventorySystem）。每幀找最近且在 `pickupRadius` 內的目標、驅動提示、收 F 鍵互動。`SetupInteractPoints` 建觸發點、`DropLoot` 放掉落物、`ClearAll` 清場。 |
| `Assets/Scripts/Combat/GroundLoot.cs` | 地上掉落物世界物件：`SpriteRenderer` 用道具 icon，依 sprite 實際尺寸縮放到 `lootWorldSize`（與 PPU 無關）。持 `itemId/count`。 |
| `Assets/Scripts/Combat/InteractMarker.cs` | 觸發點的**星星標示特效**（拾取點＝金、劇情點＝紫）。純程式畫五角星 sprite（反鋸齒、共用快取）＋每顆星閃爍/脈動/浮動/自轉。撿掉/看完即連同銷毀。星星放在 `InteractOverlay` 層、由下方 Overlay 相機重畫（暗場景也可見）。 |
| `Assets/Scripts/Combat/OverlayCameraController.cs` | **Overlay 疊加相機**：把星星等世界標示畫在氛圍後處理之上、不被壓暗（見〈暗場景也看得到星星〉）。自動生成、跨場景常駐、零接線。 |
| `Assets/Scripts/UI/Panels/PickupTipPanel.cs` | 跟隨目標的「按 F 鍵…」提示（HUD 層；世界座標→螢幕→Canvas local 定位；本體常開、只切內容顯隱避免閃爍）。 |
| `Assets/Scripts/UI/Panels/AlertPanel.cs` | 中央 toast（HUD、不暫停/不擋輸入/不遮罩、約 2 秒淡出、多則往上疊）。`AlertPanel.Toast("…")` 任何系統可叫。 |

---

## 互動流程（`InteractionManager.Update`）

1. 清掉已銷毀的掉落物引用；場上沒有任何目標 → 收提示、return。
2. 開著 UI（背包/劇情等，`UIManager.IsGameplayInputBlocked`）→ 收提示、不互動、return。
3. 找玩家（`FindGameObjectWithTag("Player")` 快取），算出**最近且在 `pickupRadius` 內**的目標（掉落物比距離、觸發點比「到最近格中心」的距離，一起排名）。
4. 在目標上方顯示對應提示文字。
5. 按下 `F`（`interactKey`）→ 對最近那個互動：
   - **掉落物** → `InventorySystem.AddItem`（部分撿取，剩的留地上）。
   - **拾取點** → `AddItem`，吃得下的進背包、溢出的 `DropLoot` 到玩家腳下；**一律消耗該點**（星星消失）。
   - **劇情點** → `DramaPanel.Show(dramaId)`，**消耗該點**。

`AddItem` 回傳「放不下的剩餘」是整套「滿了掉地上」的關鍵；`InventorySystem` 完全不知道有互動這回事（維持資料層純粹）。

---

## 一次性與記憶範圍（重要）

觸發點（拾取/劇情）按 F 觸發後**立即消耗**（星星移除、當次停留不再觸發）。但「記憶」只活在 `InteractionManager` 的當次清單裡——**換地圖時 `MapManager.ClearAll` 清空、再 `SetupInteractPoints` 依新圖重建**，所以離開再回來觸發點會**重新出現**、地上掉落物會被清掉。

這是過渡行為：永久記錄（撿過/看過不再出現、地上未撿掉落物保留）屬 [MAP_SYSTEM.md](MAP_SYSTEM.md) 的 **Phase 2**（`consumedTriggers` / `groundLoot`），之後接存檔再做。觸發點區域已有穩定 `id`（編輯器產生），Phase 2 直接拿來當鍵。

---

## 接線（MapManager）

`InteractionManager` 是懶漢單例、自動生成，**不需要手動掛**。`MapManager` 在每次換圖：
- `ClearTransientGameplay()` → `InteractionManager.ClearAll()`（清掉上一張圖的掉落物與觸發點）。
- `SetupWatcher()` → `InteractionManager.SetupInteractPoints(map, pickupTypeId, dramaTypeId)`（依新圖的 `pickup`/`drama` trigger 重建觸發點＋星星）。

`MapLoader` 的 `pickupTypeId` / `dramaTypeId` 欄對應編輯器 trigger 的 `typeId`。runtime 直接讀 `region.GetString("itemId")` / `GetInt("count")` / `GetInt("dramaId")`，**不需要 triggerTypes.json**（那只是編輯器的 schema）。

---

## 可調參數（`InteractionManager` Inspector，全有預設）

| 欄位 | 預設 | 說明 |
|---|---|---|
| `interactKey` | `F` | 互動鍵（Space 已是攻擊、E 是切武器，故用 F） |
| `pickupRadius` | 1.2 | 進入此半徑才顯示提示、可互動 |
| `lootWorldSize` | 0.6 | 地上掉落物 icon 的世界大小（稍小於 1 格） |
| `sortingOrder` | 5 | 掉落物排序（低於角色 10，畫在地上） |
| `tipHeight` | 0.6 | 提示框相對目標的上方偏移 |
| `markerStarCount` | 5 | 每個觸發點的星星顆數 |
| `pickupMarkerColor` / `dramaMarkerColor` | 金 / 紫 | 拾取點 / 劇情點星星顏色 |
| `markerSortingOrder` | 20 | 星星排序（高於角色，浮在空中） |

星星的細部動畫（閃爍/脈動/浮動/自轉/散布/大小）可在 `InteractMarker` 上方欄位調。

---

## 暗場景也看得到星星（Overlay 疊加相機）

> **2026-07-23 加。**

**問題**：氛圍後處理（見 [ATMOSPHERE.md](ATMOSPHERE.md)）掛在**主相機**上，會壓暗它畫的所有世界物件。星星標示是世界 `SpriteRenderer`，在幽暗/噩夢場景（`Atmosphere` 2/3）光圈外會被壓到很暗、幾乎看不到；而「按 F」提示是 UI（Screen Space Overlay、在後處理之上）不受影響 → 會出現「**提示看得到、星星看不到**」（實例：儲藏室 `Atmosphere=2`，遠處藥水櫃的星星被壓暗；柴房 `Atmosphere=1` 不壓暗所以星星一直亮）。

**解法**：星星改由一台**疊在主相機之上、不做後處理**的相機重畫，就不會被壓暗。

- **`OverlayCameraController.cs`**（自動生成、跨場景常駐、零接線，同 `AtmosphereController` 模式）：每幀對齊 `Camera.main` 的視角/投影，只畫 **`InteractOverlay`** 這個 Unity Layer；`depth = 主相機 + 1`、`clearFlags = Depth`（只疊上去、不清色、不套 Atmosphere）；同時把主相機 `cullingMask` **去掉**這層（免得主相機又畫一份被壓暗的）。
- **`InteractMarker`** 生成星星時把它們（含父物件）放到 `InteractOverlay` 層（`LayerMask.NameToLayer(OverlayCameraController.LayerName)`）。**找不到這層就退回原本**（星星留在 Default、被壓暗），不會報錯。
- **圖層登記**：`InteractOverlay` 在 `ProjectSettings/TagManager.asset` 第 **9** 格。⚠️ 新機器 clone 專案後要確認這層在（Unity 讀 TagManager）；缺了的話 Console 會跳「找不到 Layer InteractOverlay」黃字警告、星星暫時退回被壓暗，重開 Unity 或補回該層即可。
- **層次順序**：場景（壓暗） → 星星（Overlay 相機） → HUD/UI（Screen Space Overlay，最上）。
- 這是專案**第一台第二相機**。任何「該永遠可見、又是世界物件」的東西（未來例如想讓地上掉落物 icon 在暗場景也看得到）都可放 `InteractOverlay` 層重用這台相機。

---

## 怎麼用（編輯器 + Unity）

1. **編輯器**：放「道具拾取點」trigger 填 `itemId`（對 [INVENTORY.md](INVENTORY.md) 的 `ItemTable`）＋ `count`（可疊道具一次給多個，留空=1）；放「劇情觸發點」trigger 填 `dramaId`（見 [DRAMA.md](DRAMA.md)）。存檔 → `Project Tools → Sync Map Assets`。
2. **Play**：靠近觸發點看到星星＋提示，按 **F**。拾取點背包滿了會把道具掉腳下，清完背包再走近按 F 撿回。

> ⚠️ **拾取點放在實心家具（櫃子/桌子等 `walkable:false` 物件）上時**：感應是量「玩家 → 最近感應格**中心**」在 `pickupRadius`(1.2) 內，而玩家會被家具碰撞體擋在前面、進不了家具那格 1.2 內 → 按 F 觸發不了（跟半徑大小關係不大，點在實心物裡要調到很大才搆得到）。把該 pickup 的**感應格延伸到家具前方（可站的地板）那排**（多格 pickup，`NearestCellSqr` 取最近格；手指 `center`＝各格平均、仍指家具前緣）即可，別只放家具那格——比調大全域 `pickupRadius`（會連帶放寬撿地上物/傳送點的距離）乾淨。實例見 [STOREROOM_POTION_TUTORIAL.md](STOREROOM_POTION_TUTORIAL.md)、坑見 [PROBLEMS.md](PROBLEMS.md) K1。

---

## 通用掉落入口（之後怪物掉落複用）

`InteractionManager.DropLoot(itemId, count, pos)` 是通用入口——目前用於「拾取點滿了溢出」，未來**怪物死亡掉落**直接呼叫它即可（同一套地上 icon＋按 F 撿）。

---

## 待辦（之後可加）

- Phase 2 永久記錄（撿過/看過不再出現、地上未撿掉落物保留＋存檔）——見 [MAP_SYSTEM.md](MAP_SYSTEM.md) §5、[SAVE_SYSTEM.md](SAVE_SYSTEM.md)。
- 怪物死亡掉落 → 接 `DropLoot`。
- 掉落物專屬地上圖（目前暫借背包 icon 縮小）。
- 數量拆分撿取、右鍵快速撿。

---

*建立於 2026-06-23：拾取點＋地上掉落物＋星星標示＋中央 toast，統一成「靠近按 F」的 `InteractionManager`（由 `LootManager` 一般化改名而來；`PickupMarker`→`InteractMarker`）。劇情觸發點共用本系統、見 [DRAMA.md](DRAMA.md)。*
