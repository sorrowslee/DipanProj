# PROBLEMS 封存區（已淘汰條目原文照錄）

> 由 [../PROBLEMS.md](../PROBLEMS.md) 搬入的**已淘汰情境**條目，原文一字未改；原位留有存根、編號永不重用。
> ⚠ 條目內的相對連結（如 `PROBLEMS.md`、`LASER.md`）是以**原位置 `readme/` 為基準**寫的，封存後差一層目錄——查閱時自行對應到 `../<檔名>`，不改原文。
> 這些描述的是**當時**的環境與流程，不代表現況；現況以 PROBLEMS.md 正檔與各主題文件為準。

---

### A3. 部署 `git push` 失敗 / `fatal: not a git repository`（⚠️ 已淘汰情境）
> **2026-07-03 起部署改用 itch.io + butler，build 不再進 git，本坑不再發生。新流程見 [DEPLOY.md](DEPLOY.md)。** 以下保留存查。
- **症狀**:打包成功但推送失敗;或 stderr 出現 `not a git repository`、`non-fast-forward`。
- **原因**:`DipanProj_Deploy` 還不是 git repo;或本地 main 落後遠端;或從 Unity GUI 啟動的程序拿不到 git 憑證/SSH key。

---

### A9. `git push` 被拒：`File ...resources.assets.resS ... exceeds GitHub's 100 MB limit`（⚠️ 已淘汰情境）
> **根治：2026-07-03 起部署改用 itch.io + butler，build 產物不再進 git，就沒有 GitHub 100MB 單檔限制的問題了（見 [DEPLOY.md](DEPLOY.md)）。** 以下的「壓縮 build 貼圖」仍可作為縮小 build 體積的一般參考。
- **症狀**:Build and Deploy 後 push，GitHub 退回，說某個檔（通常 `*_Data/resources.assets.resS`）超過 100MB。
- **原因**:`resources.assets.resS` 是 Unity 烘進 build 的「資源資料流」——**凡是放在 `Assets/Resources/` 的貼圖都會進這個檔，且在 build 內展開成大尺寸**（接近未壓縮）。本專案 `Resources/InitialStory`（開場漫畫＋墜落大圖）＋ `Resources/UI`（大張面板底圖）疊起來就破百 MB。
- **解法（治本）**:把那批大圖的**匯入設定**壓小——選取 `Resources/InitialStory`（及 UI 大底圖）→ Inspector：`Max Size` 設 1024（或留 2048）＋勾 **Use Crunch Compression**（或 `Compression = Normal`）＋取消 **Generate Mip Maps** → Apply → 重新 Build。觀念：
  - `Max Size`/壓縮改的是「**匯入後的版本**」（存在 Library，編輯器與 build 都用它），**不動原始 PNG**；build 裡裝的是這份處理版（原始 PNG 不會被打進遊戲），所以 build 變小，且**編輯器與 build 解析度一致**。
  - 開場圖走 `Resources.Load` → **吃**匯入設定；地圖素材在 `StreamingAssets`、用 raw bytes 載 → **不吃**匯入設定（原樣複製進 build，要縮得改檔案本身）。
- **解法（治標）**:Deploy repo 改用 Git LFS 追 `*.resS *.assets *.bundle`（遠端 pull 的機器也要裝 LFS）。
