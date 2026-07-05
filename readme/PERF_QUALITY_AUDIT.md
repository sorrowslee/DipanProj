# 效能與畫質診斷報告（2026-07-05）

> 針對「PC 上 60fps 仍不順」「PC build 畫面粗糙」「UI 髒/顆粒大」「素材尺寸該怎麼訂」四個問題的完整調查。
> 結論先講：**四個問題都找到了明確原因，且都不需要換美術風格**。

---

## 0. 根因總表

| 症狀 | 根本原因 | 修法（見 §5） |
|---|---|---|
| fps 60 或更高仍不順 | 物理 50Hz（Fixed Timestep 0.02）×螢幕 60Hz 拍頻 + Player/Monster 的 Rigidbody2D **內插(Interpolate)全關** → 角色每秒約 10 次「跳半格」 | 開 Interpolate + Fixed Timestep 改 1/60 |
| 解 VSync 後 fps 狂升仍不順 | KVM 輸出仍是 60Hz，>60fps 畫面根本顯示不出來，只會撕裂＋節奏更亂；不順的源頭是上一列，不是 fps 不夠 | 同上，且 VSync 建議開著 |
| 世界畫面粗糙 | 地圖素材 256px/格，1080p 下一格只顯示 **108px**，等於 **0.42 倍縮小**，又用 **Point 濾波、無 mipmap** → 六成像素被隨機丟棄，產生噪點與移動閃爍 | 場景貼圖改 Bilinear + 開 mipmap |
| 編輯器裡卻不覺得粗糙 | Mac Retina 的 Game view 實際渲染像素密度高（接近 1:1 取樣），PC 1080p 才會掉到 0.42x | 同上（修完後兩邊一致） |
| UI 髒、顆粒大 | **不是風格問題**。icon/按鈕原圖 256~500px，實際顯示只有 45~70px，= **5~10 倍縮小**，Bilinear 只取 4 個 texel、無 mipmap → 高頻噪點全部漏進畫面 | 原圖縮到顯示尺寸的 ~2 倍（改 meta 的 maxTextureSize 即可） |

---

## 1. 問題一：不順暢（卡頓/judder）

### 證據
- `ProjectSettings/TimeManager.asset`：`Fixed Timestep: 0.02`（物理 50Hz）。
- `Player.prefab` / `Monster.prefab`：`m_Interpolate: 0`（內插關閉）。
- `PlayerController.cs:348`：移動走 `_rb.velocity`（FixedUpdate，本身正確）。
- 相機 `MapCameraController` 在 LateUpdate 用 SmoothDamp 跟 transform（正確）。

### 機制
物理 50Hz、渲染 60Hz → 每 6 幀就有 1 幀「物理沒更新」或「更新兩次」。內插關閉時角色位置直接吃物理步進 → 玩家/怪物以固定節奏抖動（約每秒 10 次）。**這個抖動跟 fps 高低無關**，所以解開 VSync 飆到 300fps 一樣不順。相機是平滑的、角色是抖的，兩者對比反而讓抖動更明顯。

### KVM / VSync 說明
KVM（ATEN，見 DISPLAY_SETTINGS.md）把輸出鎖 60Hz。60Hz 螢幕上：
- VSync 開：穩定 60fps、節奏均勻 → **正確選擇**。
- VSync 關：fps 數字好看，但螢幕每 16.7ms 只能顯示一張，多算的全丟掉還撕裂。
「fps 狂升還是不順」正是因為問題在 50Hz 物理抖動，不在輸出張數。

---

## 2. 問題二：世界畫面粗糙

### 關鍵數字
- 地磚原生 256px/格（管線硬性，`MapSpriteLoader`：ppu = 256/tileSize）。
- `tileSize = 1`、相機 `followViewHeightTiles = 10` → 畫面高 = 10 格。
- 1080p：1080 ÷ 10 = **一格只有 108 螢幕像素** → 素材被縮到 **0.42 倍**。
- `MapSpriteLoader.SceneFilterMode = FilterMode.Point`，`new Texture2D(2,2,RGBA32,false)` → **無 mipmap**。

### 機制
Point 濾波放大時是「像素風」；**縮小時是災難**——每個螢幕像素只隨機挑一個原圖像素、丟掉週邊 5 個，結果是噪點、斷線、鏡頭移動時整個畫面閃爍蠕動。這就是「粗糙、髒」的體感來源。4K 螢幕下是 0.84x 所以會好很多；Mac Retina Game view 接近 1:1 所以編輯器看不出來。

### 驗證方法（已內建！）
遊戲裡按 **F**（`SetSceneFilterMode` 切 Point/Bilinear）直接 A/B 對比。Bilinear 會明顯變乾淨（略軟）；加上 mipmap 後縮小取樣才完全正確。

### 附帶收益
相機 SmoothDamp 產生的子像素位置在 Point 濾波下會造成 texel 邊界蠕動；改 Bilinear 後這個閃爍也會消失。

---

## 3. 問題三：UI 髒／顆粒大

### 關鍵數字
| 資產 | 原圖 | 實際顯示（1080p） | 縮小倍率 |
|---|---|---|---|
| 物品 icon（`UI/Icons/Items/`） | 256px | InventoryPanel `ItemIconSize=70` × 面板 scale ≈ **45px** | ~5.7x |
| CloseBtn / DragIcon / PopupIcon 等 | 500px | ~48px | ~10x |
| 按鈕 LongBtn | 612×408 | ~200×130 | ~3x |

UI 用 Bilinear 但 `enableMipMap: 0`、`maxTextureSize: 2048`（不會被匯入器縮小）→ 5~10 倍縮小時 Bilinear 只取 2×2 texel，等於在 10×10 的區域裡亂抽 4 點 → 顆粒、髒邊。

### 結論：不用換風格
黑暗像素風本身沒問題，問題是「大圖硬塞小格子」。把原圖尺寸壓到顯示尺寸的 ~2 倍以內，同樣的圖會立刻變乾淨。最省力做法：**不動 PNG 檔，只改 .meta 的 `maxTextureSize`**（Unity 匯入器的縮圖品質很好）。

---

## 4. 問題四：資源尺寸規範

### 尺規基準
一格 = 1 world unit = **108 螢幕 px @1080p / 216 px @4K**。
規則：**世界內素材 = 佔幾格 × 256px**（保 4K 餘裕）；**UI 素材 = 顯示尺寸 × 2**。

| 類別 | 建議原圖尺寸 | 匯入 maxTextureSize | 備註 |
|---|---|---|---|
| 地磚 | 256/格（現制，勿動） | — | 管線硬性 |
| 玩家/怪物（1~2 格） | 256~512 | 512 | 現行 500 上限 ✅ 合理 |
| Boss/邪佛/場景大物（3 格+） | 每格 256，上限 1024 | 1024 | 1024 以上無收益 |
| 子彈/飛行物（≤1 格） | 128~256 | 256 | |
| 擊中/發射特效 | 256；全螢幕級 512 | 512 | 序列幀多時尤其要控 |
| 物品/武器 icon | **128~192** | **128** | 現 256~500 過大 → 髒的主因 |
| UI 小按鈕/游標類 | 顯示尺寸×2（多為 96~128） | 128 | 現 500 過大 |
| UI 大按鈕/牌匾 | ~256~512 長邊 | 512 | |
| 面板背景（背包/倉庫板） | ≤ 顯示大小（~1100×1400 OK） | 2048 | |
| 立繪/頭像 | 顯示高度×2，長邊 ≤1024 | 1024 | |
| 劇情大圖/開場漫畫/Loading/標題 | 1920×1080（長邊 ≤2048） | 2048 | 開壓縮（大圖壓縮肉眼難辨，省 build 體積） |

壓縮原則：全螢幕大圖開 `textureCompression`；小 icon 縮到 128 後保持不壓縮（小圖壓縮瑕疵明顯、體積本來就小）。

---

## 5. 修正清單（依優先序）

1. **開內插**：`Player.prefab`、`Monster.prefab` 的 Rigidbody2D `Interpolate = Interpolate`（m_Interpolate: 0→1）。→ 直接解決抖動。
2. **物理對齊 60Hz**：TimeManager `Fixed Timestep 0.02 → 0.01666667`。與 60Hz 螢幕同步，消除拍頻（開了內插後屬保險，成本為物理多跑 20%）。
3. **場景貼圖改 Bilinear + mipmap**：`MapSpriteLoader` 預設 `SceneFilterMode = Bilinear`；`new Texture2D(2,2,RGBA32,false)` 第三參數改 `true`（LoadImage 會自動生 mip chain；記憶體 +33%）。→ 解決世界畫面粗糙與移動閃爍。
4. **UI icon 縮尺寸**：批次改 `UI/Icons`、`UI/Common` 等 .meta 的 `maxTextureSize`（icon→128、按鈕→512），照 §4 表。→ 解決 UI 髒。
5. **VSync 保持開啟**（Ultra 檔已是 vSync=1），別為了 fps 數字關掉；上架前做玩家畫面設定時再開放選項（DISPLAY_SETTINGS.md 已規劃）。
6. （小項）`MonsterActuator.MoveTowards` 在 Update 路徑設 velocity——可運作，但建議搬到 FixedUpdate 節奏，非急迫。

### 驗證方式
- 修 1+2 後：走路盯角色本體（不是相機），抖動應消失；PerfHud（P）看幀時是否平穩。
- 修 3 前後：遊戲內按 F 即時對比。
- 修 4 後：開背包/彈窗看 icon 邊緣。

---

## 6. 修正結果（2026-07-05 實施）

| 項目 | 狀態 |
|---|---|
| ① Player/Monster 開 Interpolate＋Fixed Timestep 60Hz | ✅ 已實施，**實測卡頓明顯改善** |
| ② MapSpriteLoader 改 Bilinear＋mipmap | ✅ 已實施（F 鍵可切回 Point 對比） |
| ③ UI icon meta →128／按鈕→512＋開 mipmap | ✅ 已實施（PNG 未動） |
| 熱浪扭曲改低頻大波（Atmosphere 4/5/6） | ❌ 已還原——作者偏好原本高頻觀感，屬美術選擇非 bug |

補充發現：Main_Square（Map 12）特別粗糙的主因是 **Atmosphere=5 的熱浪扭曲**（每幀 ±2~3px 高頻重取樣整個畫面）疊加火雨（SceneEffect=1），與地圖大小（90×50 格）無關——鏡頭跟隨模式固定只顯示 10 格高，texel 密度不隨地圖尺寸變。若日後仍嫌該圖粗糙，可調 shader 熱浪係數（`Atmosphere.shader` mode 4/5/6 段的 0.0014/0.0011）或關閉該圖氛圍做對比。

---

## 7. 一句話總結

不順 = 50Hz 物理 + 沒開內插；粗糙 = 256px 素材被 Point 濾波縮到 0.42x；UI 髒 = 5~10 倍縮小又沒 mipmap。**全部是縮放/取樣管線問題，風格不用換，素材不用重畫**（icon 只要調匯入尺寸）。
