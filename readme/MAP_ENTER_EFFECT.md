# 進場一次性效果 (Map Enter Effect) — 睜眼醒來

> 返回 [文件總覽](README.md)
>
> 與**持續性**的 [ATMOSPHERE.md](ATMOSPHERE.md)（螢幕氛圍後處理）、[SCENE_EFFECT.md](SCENE_EFFECT.md)（世界端場景特效）分工：本系統是**進圖時播一次就結束的過場**。
>
> **狀態：✅ 程式完成（2026-07-03），待 Unity 實機驗證。** 第一個效果：睜眼醒來（用在初始洞窟 Main_Cave）。

一種**進入地圖時播放一次**的螢幕後處理過場，語意是「一次性事件」而非「持續狀態」——所以刻意不塞進 Atmosphere / SceneEffect（那兩個是換圖就套上、一直套著的持續系統）。獨立成一套，之後「闔眼昏迷」「中毒暈眩」「被打到眼前發黑」都能往這裡加型別。

---

## 1. 睜眼醒來（type 1）

用在**初始洞窟 Main_Cave**：主角從高處墜落昏迷，進洞窟場景後「睜開眼睛」的感覺。時間軸（總長 `duration`，預設 2.6 秒，可調）：

```
承接全黑（接墜落昏迷）→ 眼皮裂開一條縫（露出模糊、偏暗的洞窟）→ 沉重眨一下（闔回）
→ 逐漸對焦（模糊轉清晰）、亮度回正、眼皮完全睜開 → 移除後處理、恢復正常畫面
```

視覺：上下對稱的**杏眼狀眼皮遮罩**（中間開得多、兩側窄）＋**景深模糊由大轉 0**（剛醒視線糊）＋**亮度由暗回正**＋**暗角越沒睜開越重**。

### 1.5 玩家「趴地 → 起身」連動（2026-07-07 加）

睜眼（EnterEffect=1）自動連動玩家的甦醒表演，**零新素材**——爬起動畫＝把該血統的 `dead` 逐格幀**倒著播**（趴地幀 → 站立幀）：

```
進圖放好玩家 → 立刻定格在 dead 最後一幀（趴地）→ 睜眼過場播放（遊戲暫停）
→ 睜眼播完 → 倒播 dead＝爬起（定住輸入、不暫停）→ 回 idle、恢復操作 → 才點火「進場觸發」
```

- 程式：`PlayerAnimator.HoldLyingPose()`（趴地定格）＋ `PlayerAnimator.PlayWakeUp(onDone)`（倒播爬起，速率同 BaseFps）；
  `MapManager.PlaceAndSetup` 只記需求（`_wakeUpWanted`），趴地與起身都在 `MapManager.FireEnterTriggersRoutine` 執行——
  因為玩家**第一次生成**時 `PlayerAnimator.Setup` 在 `Start()` 才載幀，PlaceAndSetup 當下拿不到 dead 圖；
  協程開跑時已載好，且睜眼開頭全黑（眼皮閉合）蓋住趴下前的站姿瞬間。
- 表演期間 `SetState` 被忽略（HandleVisuals 每幀塞 Idle 也蓋不掉趴姿）；**真死（Dead）例外**會打斷表演。
- 防呆：該血統沒有 `dead/` 圖 → 整段跳過（只播睜眼、不趴地）。
- 順序保證：**onEnter 進場觸發（對話等）一定在起身完成後才點火**（見 [TRIGGER_CHAIN.md](TRIGGER_CHAIN.md) §3）。

---

## 2. 檔案與運作

| 檔案 | 角色 |
|---|---|
| `Assets/Resources/Shaders/EyeOpen.shader` | 全螢幕後處理：眼皮/杏眼遮罩(`_Open`) + 圓盤模糊(`_Blur`) + 亮度暗角(`_Bright`)。 |
| `Assets/Scripts/MapFx/EyeOpenController.cs` | 自生成常駐單例；`Play()` 用 3 條 `AnimationCurve`（`_Open`/`_Bright`/`_Blur`，眨眼藏在 open 曲線關鍵影格）驅動時間軸；播放中把 `EyeOpenBlit` 掛到 `Camera.main`，播完停用該 blit 恢復畫面。仿 `AtmosphereController`。 |
| `EyeOpenBlit`（同檔） | 相機上的 `OnRenderImage` 全螢幕 Blit（同 `AtmosphereBlit`）。 |

**資料驅動**：`MapsTable.csv` 第 9 欄 `EnterEffect`（0=無、1=睜眼；向下相容，缺欄/空/無法解析都當 0）。`MapManager.PlaceAndSetup` 進圖時呼叫 `EyeOpenController.ApplyMapEnterEffect(row.enterEffect)`（就在套 Atmosphere / SceneEffect 的同一處）。**每次進該圖都播一次**（非持久狀態、無存檔旗標）。

- 後處理只作用在主相機畫面；Screen Space Overlay 的 UI（HUD/面板）在其後合成、不受影響。相機上同時有 `AtmosphereBlit` 時，睜眼在其後合成（過場疊在氛圍之上），播完停用、回到只剩氛圍。
- 與載入頁的銜接：時間軸開頭是「全黑停一下」，剛好蓋過 `LoadingPanel` 收尾的那一下，交接無縫。
- **播放中暫停＋擋操作**：`Play()` 呼叫 `UIManager.SetExternalHold(true, true)`（新增的「非面板系統暫停/擋輸入」掛勾）→ 遊戲暫停（`Time.timeScale=0`）且玩家不能操作（`PlayerController` 查 `IsGameplayInputBlocked`）；`Finish()` 解除。睜眼時間軸用 `unscaledDeltaTime`，所以暫停中照樣播。此掛勾與面板需求一起參與 `UIManager.Recompute`，**不會被載入頁關閉時的重算覆蓋**（若直接設 `Time.timeScale` 就會被蓋，這是本作法的重點）。場景被切換打斷時 `OnSceneLoaded` 會安全解除。

---

## 3. 怎麼用 / 調整

- **哪張地圖要睜眼**：`MapsTable.csv` 該列 `EnterEffect` 填 `1`（目前 Main_Cave（11）已填）。要拿掉就改回 0/留空。
- **節奏/強度**：改 `EyeOpenController` 的 `duration`（總長）、`maxBlur`（剛醒模糊強度），或 `Awake` 裡三條 `AnimationCurve` 的關鍵影格（眨幾下、裂開多少、亮度曲線）。
- **加新型別**（如闔眼昏迷）：在 `EyeOpenController` 加對應時間軸、`ApplyMapEnterEffect` 依 type 分派，`MapsTable` 的 `EnterEffect` 填新編號即可。

---

## 4. 實機驗證

1. 開 Unity 等編譯，Console 無紅錯（新程式在預設 `Assembly-CSharp`；shader 在 `Resources/Shaders`）。
2. 進入 Main_Cave（新建遊戲走開場鏈墜落後、或直接把 Main 模組設起始關卡測）：畫面應**由全黑 → 眼皮撐開 → 眨一下 → 對焦清晰**，約 2.6 秒後恢復正常、可操作。
3. 若 `MapsTable.csv` 剛改完沒生效：右鍵該 CSV → Reimport（TextAsset 需重新匯入）。
4. 若完全沒效果、Console 出現「找不到 Resources/Shaders/EyeOpen」：確認 shader 檔在 `Assets/Resources/Shaders/`。

---

*建立於 2026-07-03：進場一次性效果系統＋第一個效果「睜眼醒來」（後處理版：杏眼遮罩＋模糊對焦＋亮度暗角，MapsTable `EnterEffect` 欄驅動、進圖播一次）。*
