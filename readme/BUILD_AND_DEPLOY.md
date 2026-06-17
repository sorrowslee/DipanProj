# 打包與部署 (Build & Deploy)

> 返回 [文件總覽](README.md)

主遊戲走「**Mac 打包 → 推上 GitHub(`DipanProj_Deploy`) → 遠端 Windows 機器 git pull 測試**」的流程。相關功能都在 Unity 上方選單 **`Project Tools`**。

## 選單

| 選單 | 作用 |
|---|---|
| `Project Tools → Build and Deploy` | 建 Windows 版 → 同步成品到 `DipanProj_Deploy` → 推上 GitHub |
| `Project Tools → Build (Mac, 本機測試)` | 建 Mac 版到 `Builds/Mac_Test/`,可本機直接跑,用來驗證「專案/資料是否完整」(排除 Windows 模組變數) |
| `Project Tools → Sync Map Assets` | 地圖素材同步(見 [MAP_LOADER_SETUP.md](MAP_LOADER_SETUP.md)) |

## Build and Deploy 的完整流程

`BuildScript.cs` 依序做:

1. **檢查 Windows 模組**:沒裝 Windows Build Support 就中止並提示(在 Mac 上建 Windows 版必須裝此模組)。
2. **打包前對齊部署資料夾**(`update_deploy.sh`):把 `DipanProj_Deploy` 本地 `main` **無條件對齊遠端 `main`**(`git fetch` → `git checkout -B main origin/main` → `git reset --hard origin/main` → `git clean -fd`)。有任何衝突/本地改動一律以遠端為準。**同步失敗就中止打包**,避免之後因「本地落後遠端」而 push 失敗。
3. **打包** Windows 版到 `DipanProj_Main/Builds/Windows_Test/`。
4. **驗收**:印出整個 BuildReport(每個 BuildStep 的錯誤/例外/警告)+ 檢查 `_Data` 是否含核心資料檔(`globalgamemanagers` / `data.unity3d`)。**只有「成功 + 零錯誤 + 資料完整」才繼續部署**,杜絕半成品。
5. **部署**(`deploy_only.sh`):`rsync` 成品到 `DipanProj_Deploy` → `git add/commit/push`,逐步檢查、誠實回報(不是 repo、無變更、push 失敗都會明講)。

## 一次性設定:DipanProj_Deploy 必須是 git repo

`DipanProj_Deploy` 與 `DipanProj` 同層,是一個獨立的 git repo(只放 Windows 成品)。第一次要先設好遠端:
```
cd <與 DipanProj 同層>/DipanProj_Deploy
git init
git branch -M main
git remote add origin <你的GitHub repo網址>
git add . && git commit -m "init deploy"
git push -u origin main
```

## 相關腳本

- `update_deploy.sh`(打包前)— 把 `DipanProj_Deploy` 對齊遠端 main。
- `deploy_only.sh`(打包後)— rsync 成品 + commit + push。
- `DipanProj_Main/Assets/Editor/BuildScript.cs` — 串起上面整個流程的 Unity 選單。

## 疑難排解(踩過的雷)

- **`Build target 'StandaloneWindows64' not supported`**:Mac 沒裝(或裝不完整)Windows Build Support 模組。Unity Hub → Installs → 對 `2022.3.62f3` Add/Remove Modules → 重裝 **Windows Build Support (Mono)** → **完全重啟 Unity**。
- **Windows 開遊戲跳 `Data folder not found` / `_Data` 缺 `globalgamemanagers` 等核心資料**:成品不完整。兩種來源:
  1. **打包本身失敗**(Windows 模組/Postprocess 問題)→ 看 BuildReport 紅字;可先用 `Build (Mac, 本機測試)` 確認專案沒問題(Mac 版能跑 = 專案 OK)。
  2. **git 沒同步**(本次真正原因):部署資料夾沒先 pull/對齊遠端就 push,推送失敗、遠端是舊/半成品,Windows pull 下來自然缺檔。已由步驟 2 的打包前對齊解決。
- **`git push` 失敗**:常見「本地落後遠端」(已由打包前對齊解決),或「從 Unity GUI 啟動的程序拿不到 git 憑證/SSH key」→ 先在終端機手動 `git fetch` / `git push` 一次把憑證帶起來。
- **不要用 git 傳超過 100MB 的單檔**:GitHub 會擋。Unity build 若有超大檔,考慮改用 zip release(目前專案的 build 沒有超標檔)。

## 排除法小抄

- Mac 版能跑、Windows 版不行 → 問題在 Windows 打包流程(模組)或 git 傳輸,**不是專案程式**。
- 上一版能打包、這版不行(同機同模組)→ 才需要懷疑這版的改動。
- 真正的失敗原因看 `Build and Deploy` 印出的 **BuildStep 紅字**,或 Mac 的 `~/Library/Logs/Unity/Editor.log`。
