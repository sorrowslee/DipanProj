# itch.io & butler 手冊（安裝、設定、指令、疑難排解）

> 返回 [文件總覽](README.md) ｜ 打包流程本身（Unity 選單、BuildScript、驗收）見 [DEPLOY.md](DEPLOY.md)
>
> **itch / butler 相關的問題一律先來這裡找**：安裝、登入、上傳、查版本、PC 端取得、所有踩過的雷都收在本文件。

butler 是 itch 官方的命令列上傳工具；本專案用它把 Windows build **增量上傳**到 itch.io（`sorrowslee/dipan:windows`），PC 用 itch app 自動差分更新。

> **為什麼不用 git 部署（2026-07-03 淘汰）**：build 產物是「每次整包都變的大二進位」，塞 git 必撞 GitHub 100MB 單檔上限、repo 無限膨脹、幾乎整包重傳。butler＝位元組級差分上傳、無大小限制、有版本可回溯、免費。詳見 [DEPLOY.md](DEPLOY.md) 開頭。

---

## 1. 一次性設定（每台 Mac 各自做一次）

### 1.1 裝 butler 到 `~/butler/`

**注意坑**：官方 CDN 主機 `broth.itch.ovh` 在某些台灣 ISP（HiNet）DNS 解不到（NXDOMAIN）——**直接從 itch.io 的 butler 專案頁用瀏覽器下載**，繞開那個主機：

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

> `butler push` 實際連的是 `itch.io`（解析得到），所以上傳沒問題；只有 `butler upgrade`（自我更新）會用到解不到的 broth 主機——那步不做也不影響上傳。

### 1.2 登入

```bash
cd ~/butler && ./butler login
```

開瀏覽器授權一次，憑證存本機。**每台 Mac 各自登一次**（同一 itch 帳號多台授權沒問題）；無法互動登入的環境可改設環境變數 `BUTLER_API_KEY`。

### 1.3 itch 專案（帳號層級，只做一次、換機免重做）

一個 itch project：帳號 `sorrowslee`、URL `dipan`、channel 用 `windows`（channel 名是 push 時指定，不用先建）。測試期把專案設 **Draft（草稿）**＝只有你自己看得到。
**注意**：itch 規定**先驗證帳號 email 才能 push build**（沒驗證會回 `400 … verify your account's email address`）。

### 1.4 部署腳本怎麼找到 butler

`deploy_only.sh` 找 butler 的順序：環境變數 `BUTLER` → PATH 上的 `butler` → `~/butler/butler`。照 1.1 裝到 `~/butler/` 就能被 Unity 觸發的部署腳本找到。

### 1.5 換機器檢查清單（新 Mac 首次）

1. git pull 到最新（拿到 butler 版 `deploy_only.sh` / `BuildScript.cs`）。
2. 裝 butler 到 `~/butler/butler`（§1.1）。
3. `butler login`（§1.2）。
4. 裝 Unity **Windows Build Support (Mono)** 模組（每台、每個 Unity 版本各自裝）。

免重做的：itch email 驗證、itch 專案（帳號層級共用）。**第一次從新機器 push 不會整包重傳**（butler 拿 itch 上「上一版簽章」做差分，與機器無關）；只有第一次「打包」慢（本機 `Library/Bee` 快取重建）。

---

## 2. 常用指令

> 都在 `cd ~/butler` 下執行（或把 `~/butler` 加進 PATH 後直接 `butler …`）。

| 指令 | 作用 |
|---|---|
| `./butler -V` | 印版本（驗證安裝成功） |
| `./butler login` | 登入（一次性，憑證存本機） |
| `./butler push <資料夾> sorrowslee/dipan:windows --userversion <版本>` | 上傳（平常不用手打——`Project Tools → Build and Deploy` 會經 `deploy_only.sh` 自動執行） |
| `./butler status sorrowslee/dipan:windows` | **查推送是否成功**：列出該 channel 的 build 編號/版本/狀態，最新一筆 `✓`（processed / up to date）＝成功；pending/processing＝itch 還在處理（推完馬上查可能還沒好，等幾十秒再查） |

## 3. 看懂部署結果（Unity Console）

- `🎉 部署成功：已用 butler 增量上傳新版本到 itch…` — 上傳成功。
- butler 那行 `For channel 'windows': last build is N, downloading its signature` ＝ 它抓上一版簽章做差分，代表**增量有生效**（只傳變動位元組）。
- 想跟 itch 端再次確認：`./butler status sorrowslee/dipan:windows`（見 §2）。

## 4. PC 端取得（itch app）

1. PC 瀏覽器打開 `https://itch.io/app` → 下載安裝 itch 桌面 app。
2. 用 `sorrowslee` 登入（遊戲是 Draft，只有登入本人看得到）。
3. 開啟 `https://sorrowslee.itch.io/dipan` → **Install**（抓 `windows` channel）。
4. 之後 Mac 每次 Build and Deploy 推新版，itch app **自動偵測、只下載差分**，按 **Launch** 即玩。

> 也可改用 butler 在 PC 端拉檔＋雙擊 bat，但 itch app 的差分自動更新最省事。

## 5. 服務與費用

**全程免費**：butler 工具免費、itch 託管免費、Draft 私人測試免費。單一 build「未壓縮總和」上限 30GB（超過可申請）。抽成只發生在「賣遊戲」時（自己設分潤），與私下測試無關。

---

## 6. 疑難排解（itch / butler 篇）

- **`butler: command not found` / 部署腳本找不到 butler**：確認裝在 `~/butler/butler`，或設環境變數 `BUTLER` 指向執行檔（找尋順序見 §1.4）。
- **`itch.io API error (400): … verify your account's email address`**：先去信箱點 itch 的確認信驗證 email，再重 push。
- **`curl: (6) Could not resolve host: broth.itch.ovh`**：HiNet 等 DNS 解不到 broth 主機。**改用瀏覽器從 `https://itchio.itch.io/butler` 下載**（走 itch.io，不碰 broth）；或把系統 DNS 換成 `1.1.1.1`/`8.8.8.8`。`butler upgrade` 同理會失敗，不做即可。
- **Mac 下載的 butler 被 Gatekeeper 擋（「無法打開/已損毀」）**：`xattr -dr com.apple.quarantine ~/butler`。
- **推送後 itch 上看不到新版**：先 `./butler status sorrowslee/dipan:windows`——若最新 build 還在 processing 就是正常，等它變 `✓`；若根本沒新 build，回 Unity Console 看 `deploy_only.sh` 段是否有紅字。
- **登入視窗開不出來 / 無頭環境**：用 `BUTLER_API_KEY` 環境變數代替 `butler login`（API key 在 itch 網站 Settings → API keys 產生）。

> **打包本身**的問題（`_Data` 不完整、`StandaloneWindows64 not supported`、場景順序、Editor.log 判讀…）不在本篇——見 [DEPLOY.md](DEPLOY.md) 疑難排解與 [PROBLEMS.md](PROBLEMS.md) A 區。

## 相關檔案

- `deploy_only.sh` — `butler push Builds/Windows_Test sorrowslee/dipan:windows`（差分上傳、帶日期版本號、排除 `*_BurstDebugInformation_DoNotShip/*`）。
- `DipanProj_Main/Assets/Editor/BuildScript.cs` — 串起打包＋驗收＋部署的 Unity 選單（見 [DEPLOY.md](DEPLOY.md)）。
