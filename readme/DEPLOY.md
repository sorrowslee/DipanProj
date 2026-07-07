# 打包與部署 (Build & Deploy — itch.io + butler)

> 返回 [文件總覽](README.md) ｜ **itch / butler 的安裝、登入、指令、換機設定、PC 端取得與相關疑難排解 → 集中在 [ITCH_BUTLER.md](ITCH_BUTLER.md)**（本篇只講打包流程本身）

主遊戲走「**Mac 打包 → `butler` 增量上傳到 itch.io → PC 用 itch app 自動增量更新**」的流程。相關功能都在 Unity 上方選單 **`Project Tools`**。

> **為什麼不再用 git 部署（2026-07-03 淘汰）**：舊流程把 Windows build 產物（`resources.assets`、`.resS`、`data.unity3d`…）rsync 進 `DipanProj_Deploy` 再 push 到 GitHub、PC 端 `git pull`。但 git 是為原始碼設計的，把「每次整包都變、又無法有效差分的大二進位檔」塞進去必然出事：① 單檔破 GitHub 100MB 上限（`.resS` 早就 176MB）② repo 歷史無限膨脹、clone/pull 越來越慢 ③ 傳輸幾乎整包重傳。**結論：build 產物不該進 git**。改用 itch 的 `butler`——位元組級差分上傳、無檔案大小限制、有版本可回溯、免費。

## 選單

| 選單 | 作用 |
|---|---|
| `Project Tools → Build and Deploy` | 建 Windows 版 → 用 `butler` 增量上傳到 itch（`sorrowslee/dipan:windows`） |
| `Project Tools → Build Mac Local` | 建 Mac 版到 `Builds/Mac_Test/`，可本機直接跑，用來驗證「專案/資料是否完整」（排除 Windows 模組變數） |
| `Project Tools → Sync Map Assets` | 地圖素材同步（見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)） |

## Build and Deploy 的完整流程

`BuildScript.cs` 依序做：

1. **檢查 Windows 模組**：沒裝 Windows Build Support 就中止並提示（在 Mac 上建 Windows 版必須裝此模組）。
2. **打包** Windows 版到 `DipanProj_Main/Builds/Windows_Test/`。**場景清單由 `BuildScript.cs` 的 `options.scenes` 指定（不是 Build Settings 視窗），目前為 `{ "Assets/Scenes/MainScene.unity", "Assets/Scenes/Intro.unity" }`**——**`MainScene` 必須排第 0**（開機停在這裡跑標題流程），`Intro` 排第二、只在「新建遊戲」時才載入播開場鏈。順序顛倒的症狀（開機直接播漫畫、墜落後全黑）見 [PROBLEMS.md](PROBLEMS.md) A10。
3. **驗收**：印出整個 BuildReport（每個 BuildStep 的錯誤/例外/警告）＋ 檢查 `_Data` 是否含核心資料檔（`globalgamemanagers` / `data.unity3d`）。**只有「成功 + 零錯誤 + 資料完整」才繼續部署**，杜絕半成品。若偵測到 `_Data` 不完整（多半是增量快取沿用舊壞資料），會自動清輸出 + `CleanBuildCache` 重建一次。
4. **部署**（`deploy_only.sh`）：用 `butler push` 把 `Builds/Windows_Test/` 增量上傳到 itch 的 `sorrowslee/dipan:windows` channel（帶日期版本號、排除 Burst 除錯資料夾）。第一次整包、之後只傳差分。

## 一次性設定 / PC 端取得 / 費用

**全部搬到 [ITCH_BUTLER.md](ITCH_BUTLER.md)**：裝 butler（含台灣 DNS 坑）、`butler login`、itch 專案與 email 驗證、部署腳本找 butler 的順序、**換機器首次設定檢查清單**、PC 用 itch app 取得、服務費用。

## 相關檔案

- `deploy_only.sh`（打包後）— `butler push Builds/Windows_Test sorrowslee/dipan:windows`（差分上傳、帶版本號、排除 `*_BurstDebugInformation_DoNotShip/*`）。
- `DipanProj_Main/Assets/Editor/BuildScript.cs` — 串起打包＋驗收＋部署的 Unity 選單。
- （已退休）`update_deploy.sh`／`DipanProj_Deploy` git repo — 舊 GitHub 部署用，已不再呼叫。

## 看懂部署結果（Console）

- `🎉 部署成功：已用 butler 增量上傳新版本到 itch（測試機用 itch app / butler 取得最新版即可）。` — 上傳成功。
- 想跟 itch 端確認推送狀態：`cd ~/butler && ./butler status sorrowslee/dipan:windows`（最新一筆 `✓`＝成功；判讀詳見 [ITCH_BUTLER.md](ITCH_BUTLER.md) §2/§3）。

## 疑難排解（打包篇；itch / butler 篇見 [ITCH_BUTLER.md](ITCH_BUTLER.md) §6）

- **`Build target 'StandaloneWindows64' not supported`**：Mac 沒裝（或裝不完整）Windows Build Support 模組。Unity Hub → Installs → 對該版本 Add/Remove Modules → 重裝 **Windows Build Support (Mono)** → **完全重啟 Unity**。
- **Windows 開遊戲跳 `Data folder not found` / `_Data` 缺核心資料**：成品不完整。主因多是**增量打包沿用了舊的不完整資料**（log 特徵：`player data was not rebuilt` / `Run script only build`）。`BuildScript` 已自動防呆（缺核心資料就清輸出 + `CleanBuildCache` 重建一次）。手動修：關 Unity → 刪 `Builds/Windows_Test` 與 `Library/Bee` → 重 build。
- 上傳/登入/DNS/Gatekeeper 等 **itch / butler 問題 → [ITCH_BUTLER.md](ITCH_BUTLER.md) §6**。打包更多坑見 [PROBLEMS.md](PROBLEMS.md) A 區。

## 排除法小抄

- Mac 版能跑、Windows 版不行 → 問題在 Windows 打包流程（模組）或上傳，**不是專案程式**。
- 上一版能打包、這版不行（同機同模組）→ 才需要懷疑這版的改動。
- 真正的失敗原因看 `Build and Deploy` 印出的 **BuildStep 紅字**，或 Mac 的 `~/Library/Logs/Unity/Editor.log`。
