# 打包與部署 (Build & Deploy — itch.io + butler)

> 返回 [文件總覽](README.md)

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

## 一次性設定

### A. Mac 端裝 butler

butler 是 itch 官方的命令列上傳工具。**注意坑**：官方 CDN 主機 `broth.itch.ovh` 在某些台灣 ISP（HiNet）DNS 會解不到（NXDOMAIN）——所以**直接從 itch.io 的 butler 專案頁用瀏覽器下載**，繞開那個主機：

1. 確認晶片：`uname -m`（`arm64`=Apple Silicon、`x86_64`=Intel）。
2. 瀏覽器打開 `https://itchio.itch.io/butler` → Download → 依晶片挑 `butler-darwin-arm64.zip` 或「butler for macOS 64-bit (stable)」。
3. 解壓、解除 Gatekeeper 隔離、驗證：
   ```bash
   mkdir -p ~/butler && cd ~/butler
   unzip -o ~/Downloads/butler-darwin-arm64.zip     # Intel 換成你下載到的檔名
   chmod +x butler
   xattr -dr com.apple.quarantine .                 # 沒解隔離會被 Gatekeeper 擋
   ./butler -V                                       # 印出版本 = 成功
   ```
4. 登入（一次就好，憑證存本機）：`./butler login`。

> `butler push` 實際連的是 `itch.io`（解析得到），所以上傳沒問題；只有 `butler upgrade`（自我更新）會用到解不到的 broth 主機——那步不做也不影響上傳。

### B. itch 專案

一個 itch project：帳號 `sorrowslee`、URL `dipan`、channel 用 `windows`（channel 名是 push 時指定，不用先建）。測試期把專案設 **Draft（草稿）**＝只有你自己看得到。**注意**：itch 規定**先驗證帳號 email 才能 push build**（沒驗證會回 `400 … verify your account's email address`）。

### C. 從 Unity GUI 觸發時的路徑/憑證

`deploy_only.sh` 找 butler 的順序：環境變數 `BUTLER` → PATH 上的 `butler` → `~/butler/butler`。所以照上面裝到 `~/butler/` 就能被 Unity 觸發的部署腳本找到。若憑證找不到（少見），可在環境變數設 `BUTLER_API_KEY`。

### D. 換機器 / 多台 Mac 首次設定

專案程式會透過 git 同步，但 **butler 屬「機器本機」的東西不會跟 git 走**。所以另一台 Mac 首次要各自做這幾步（做完就跟原本那台一樣）：

1. **git pull 到最新**：確保拿到 butler 版 `deploy_only.sh` / `BuildScript.cs`（及最新專案程式）。
2. **裝 butler 到 `~/butler/butler`**：同上面 A 的瀏覽器下載法（broth 解不到就從 `itchio.itch.io/butler` 下載＋`xattr -dr com.apple.quarantine`）。
3. **`butler login`**：憑證存本機，每台要各自登一次（同一 itch 帳號多台授權沒問題）；或設 `BUTLER_API_KEY`。
4. **裝 Unity Windows Build Support (Mono) 模組**：每台、每個 Unity 版本都要各自裝。

免重做的：itch email 驗證（帳號層級）、itch 專案（`sorrowslee/dipan` 共用）。

> **第一次從新機器 push 不會整包重傳**：butler 是拿 itch 上「上一版的簽章」做差分，只傳變動位元組（跟哪台機器無關）。只有第一次「打包」會慢些（本機 `Library/Bee` 增量快取要重建），那是打包耗時、不是上傳。

## PC 端取得（itch app）

**取代舊的 `pull_and_run.bat`**（不再需要 git pull）：

1. PC 瀏覽器打開 `https://itch.io/app` → 下載安裝 itch 桌面 app。
2. 用 `sorrowslee` 登入（遊戲是 Draft，只有登入本人看得到）。
3. 開啟專案 `https://sorrowslee.itch.io/dipan` → **Install**（抓 `windows` channel）。
4. 之後 Mac 每次 Build and Deploy 推新版，itch app **自動偵測、只下載差分**，按 **Launch** 即玩。

> 也可改用 butler 在 PC 端拉檔＋雙擊 bat，但 itch app 的差分自動更新最省事。

## 服務與費用

**全程免費**：butler 工具免費、itch 託管遊戲免費、Draft 私人測試免費。單一 build「未壓縮總和」上限 30GB（超過可跟官方申請）。抽成只發生在「賣遊戲」時（自己設分潤），與私下測試無關。

## 相關檔案

- `deploy_only.sh`（打包後）— `butler push Builds/Windows_Test sorrowslee/dipan:windows`（差分上傳、帶版本號、排除 `*_BurstDebugInformation_DoNotShip/*`）。
- `DipanProj_Main/Assets/Editor/BuildScript.cs` — 串起打包＋驗收＋部署的 Unity 選單。
- （已退休）`update_deploy.sh`／`DipanProj_Deploy` git repo — 舊 GitHub 部署用，已不再呼叫。

## 看懂部署結果（Console）

- `🎉 部署成功：已用 butler 增量上傳新版本到 itch（測試機用 itch app / butler 取得最新版即可）。` — 上傳成功。
- butler 那行 `For channel 'windows': last build is N, downloading its signature` = 它抓上一版簽章做差分，代表增量有生效（只傳變動位元組）。

## 疑難排解（踩過的雷）

- **`Build target 'StandaloneWindows64' not supported`**：Mac 沒裝（或裝不完整）Windows Build Support 模組。Unity Hub → Installs → 對該版本 Add/Remove Modules → 重裝 **Windows Build Support (Mono)** → **完全重啟 Unity**。
- **Windows 開遊戲跳 `Data folder not found` / `_Data` 缺核心資料**：成品不完整。主因多是**增量打包沿用了舊的不完整資料**（log 特徵：`player data was not rebuilt` / `Run script only build`）。`BuildScript` 已自動防呆（缺核心資料就清輸出 + `CleanBuildCache` 重建一次）。手動修：關 Unity → 刪 `Builds/Windows_Test` 與 `Library/Bee` → 重 build。
- **`butler: command not found` / 部署腳本找不到 butler**：確認裝在 `~/butler/butler`，或設環境變數 `BUTLER` 指向執行檔。
- **`itch.io API error (400): … verify your account's email address`**：先去信箱點 itch 的確認信驗證 email，再重 push。
- **`curl: (6) Could not resolve host: broth.itch.ovh`**：HiNet 等 DNS 解不到 broth 主機。**改用瀏覽器從 `https://itchio.itch.io/butler` 下載**（走 itch.io，不碰 broth）；或把系統 DNS 換成 `1.1.1.1`/`8.8.8.8`。
- **Mac 下載的 butler 被 Gatekeeper 擋**：`xattr -dr com.apple.quarantine ~/butler`。

## 排除法小抄

- Mac 版能跑、Windows 版不行 → 問題在 Windows 打包流程（模組）或上傳，**不是專案程式**。
- 上一版能打包、這版不行（同機同模組）→ 才需要懷疑這版的改動。
- 真正的失敗原因看 `Build and Deploy` 印出的 **BuildStep 紅字**，或 Mac 的 `~/Library/Logs/Unity/Editor.log`。
