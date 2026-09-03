# 畫面與效能設定 (Display & Performance Settings)

> 返回 [文件總覽](README.md)｜存檔（`settings.json`）見 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)｜UI 框架見 [UI_SYSTEM.md](UI_SYSTEM.md)｜踩坑見 [PROBLEMS.md](PROBLEMS.md) E1
>
> **狀態：效能診斷面板 ✅ 已完成；玩家畫面設定選單 ⏳ 待做（上架前補）。**

本文件分兩半：上半是**已完成**的實機效能診斷面板（`PerfHud`）；下半是**待做**的玩家畫面設定（VSync / 幀率上限 / 視窗模式…）規劃，上架前補上。

---

## 一、效能診斷面板 `PerfHud`（✅ 已完成）

實機驗證系統狀況用的疊加面板，特別用來排查「Windows build 幀數低、Mac/編輯器卻順」這類跨平台疑問。

- **檔案**：`Assets/Scripts/Diagnostics/PerfHud.cs`（純程式建構、`RuntimeInitializeOnLoadMethod` 開場自動生成，仿 `VfxManager` / `SaveBootstrap`，零接線）。
- **開關**：按 **P** 顯示/隱藏（原本是 F3，因遠端偵錯 F 鍵被攔截而改成字母鍵）。
- **面板上的即時切換鈕**（滑鼠點，遠端最穩；也可鍵盤）：
  - **VSync(V)**：關 → 開·每刷新 → 開·每2刷 循環。
  - **目標FPS(T)**：不限 → 30 → 60 → 120 → 144 → 240 循環（VSync 開時 Unity 會忽略，按鈕上有註明）。
  - **場景濾波(F)**：Bilinear ↔ Point（見 PERF_QUALITY_AUDIT §2）。
  - **碰撞範圍(C)**：碰撞疊層（見 MAP_LOADER_SETUP）。
  - **角色色彩(G)**：【POC】角色場景融合的色彩處理 原狀 ↔ 色彩（`CharacterEnvPoc`）。
  - **角色取樣對齊背景(M)**：【過渡期】`CharacterMipBias` 開關，按鈕上顯示主角目前的 mipMapBias 與背景 px/格（見 PROBLEMS **E29**）。
- **顯示內容**：
  - FPS ＋ 每幀 ms ＋ **最差一幀**（卡頓指標）。
  - **CPU 主緒 ms / CPU 渲染 ms / GPU ms**（`FrameTimingManager`）＋ 自動判斷**瓶頸**（CPU bound / GPU bound / **受 VSync 限制**）。
  - **顯示卡名稱 ＋ 繪圖 API**（Windows 上可一眼看出是否跑在內顯）、CPU、RAM、VRAM。
  - VSync / 目標幀率 / **螢幕刷新率** / 解析度 / 全螢幕模式 / 畫質等級 / 色彩空間 / 記憶體。
  - （**僅編輯器**）Draw Call / Batches / SetPass / 三角形數——player build 沒有這些 runtime API（`UnityStats` 是 Editor-only），build 上改看 **GPU ms** 當 GPU 負載指標。
- **前置設定（已一併處理）**：`ProjectSettings` 的 **`enableFrameTimingStats: 1`** 已開啟，否則 build 上 CPU/GPU ms 會抓不到值。

> 自動縮放字級：面板會依畫面高度自動調字級（9–15px），小 Game view / 低解析度視窗也不會把底部資訊裁掉。

---

## 二、重要背景結論（為什麼會有這份文件）

排查「Windows build 幀數低」後確認：**那不是效能問題，是顯示線路 + VSync。** 詳見 [PROBLEMS.md](PROBLEMS.md) E1，摘要：

- 實測 GPU 一幀只畫 ~1.5ms（RTX 3060）、CPU ~0.3ms，引擎能力遠超數百 fps。
- 但 FPS 被鎖在 **59.9**、每幀 16.68ms 且**零掉幀** → 被 **VSync 鎖在螢幕刷新率**。
- 該遠端 PC 透過 **ATEN 4K HDMI（KVM/延長器）**出畫面，其 EDID 只給 ~60Hz；開發用 Mac 是 120Hz，對比之下才覺得「不順」。
- 結論：**遊戲健康、不需優化**。一般玩家用自己的螢幕直連顯卡，不會碰到這個（他們的順暢度＝自己螢幕的刷新率）。

---

## 三、玩家畫面設定（⏳ 待做，上架前補）

Steam 玩家的基本期待。核心＝**做一個畫面設定選單 ＋ 持久化 ＋ 開場套用**。

### 3.1 出廠預設策略（擇一）

| 預設 | 說明 |
|---|---|
| **VSync 開**（推薦預設） | 最安全：不撕裂、自動對齊任何刷新率、不空轉。代價是多約一幀輸入延遲。 |
| VSync 關 + `targetFrameRate = 螢幕刷新率` | 低延遲又不撕裂、不空轉。 |
| VSync 關 + 玩家自選上限 | 進階。 |

> **鐵則：永遠要有幀率上限。** 本遊戲畫面太輕（閒置可飆上千 fps），若「關 VSync 又不設上限」會讓玩家顯卡空轉、發熱、風扇狂轉、耗電、線圈嘯叫。走 VSync 關路線時**務必**同時設 `Application.targetFrameRate`。

### 3.2 選單應有的選項

- **VSync 開 / 關**
- **幀率上限**（30 / 60 / 120 / 144 / 240 / 不限）
- **視窗模式**（獨占全螢幕 / 無邊框視窗 / 視窗）
- **解析度**
- （之後可加）亮度 / 畫面震動開關 / UI 縮放等。

### 3.3 套用 API（開場與改設定時呼叫）

- VSync：`QualitySettings.vSyncCount = 0/1/2`
- 幀率上限：`Application.targetFrameRate = -1/30/60/...`（**只有 vSyncCount==0 時生效**）
- 視窗模式：`Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen / FullScreenWindow / Windowed`
- 解析度 + 刷新率：`Screen.SetResolution(w, h, mode, refreshRate)`

> **無邊框 vs 獨占全螢幕**：無邊框（`FullScreenWindow`，目前 build 用這個）會跟著**桌面當前刷新率**走、方便 Alt-Tab；要在高刷螢幕上拿到最高刷新率 + 最低延遲，需**獨占全螢幕**（`ExclusiveFullScreen`）配 `SetResolution` 指定刷新率。建議兩種都給、預設無邊框。

### 3.4 持久化與套用流程

1. **存哪**：接進 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) 規劃的 **`settings.json`**（全域設定、不綁角色，與音量/按鍵/語言同檔）。
2. **開場套用**：寫一個 `GraphicsSettings` 啟動套用器（仿 `SaveBootstrap` 用 `RuntimeInitializeOnLoadMethod` 自動跑），讀 `settings.json` → 套用上述 API；缺檔給預設。
3. **設定面板**：在 [UI_SYSTEM.md](UI_SYSTEM.md) 的 `UIPanel` 框架上建一個「設定」面板，玩家改了即時套用 + 寫回 `settings.json`。

> 註：`settings.json` 是「裝置偏好」，依 [SAVE_SYSTEM.md](SAVE_SYSTEM.md) §8 通常**不上 Steam Cloud**（避免跨機覆蓋）。

---

## 四、任務清單

**已完成**
- [x] `PerfHud` 效能診斷面板（FPS/幀時/CPU·GPU ms/瓶頸/顯卡/刷新率/記憶體，P 開關，V·T 切 VSync·幀率）。
- [x] `ProjectSettings.enableFrameTimingStats = 1`（build 上 CPU/GPU ms 才有值）。

**待做（上架前）**
- [ ] `GraphicsSettings` 啟動套用器（開場依 `settings.json` 套 VSync / 幀率上限 / 視窗模式 / 解析度）。
- [ ] `settings.json` 結構新增畫面設定欄位（接 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)）。
- [ ] 畫面設定 UI 面板（建在 [UI_SYSTEM.md](UI_SYSTEM.md) 上，即時套用 + 存檔）。
- [ ] 出廠預設拍板（建議 VSync 開；若改 VSync 關必附幀率上限）。
- [ ]（可選）獨占全螢幕模式 + 自選刷新率。

---

*建立於 2026-06-24：記錄已完成的 `PerfHud` 效能診斷面板，以及上架前要補的玩家畫面設定（VSync / 幀率上限 / 視窗模式，持久化進 `settings.json`、開場套用、UI 面板）。背景緣由（Windows「幀數低」實為 ATEN 60Hz 線路 + VSync）見 [PROBLEMS.md](PROBLEMS.md) E1。*
