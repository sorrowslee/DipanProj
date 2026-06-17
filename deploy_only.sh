#!/bin/bash

# --- 路徑設定修正 ---
# 取得此腳本所在的根目錄 (DipanProj)
PROJECT_ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# 修正：根據你的截圖，主專案資料夾是 DipanProj_Main
MAIN_PROJECT_PATH="$PROJECT_ROOT/DipanProj_Main"
# 修正：部署資料夾在 DipanProj 同層
DEPLOY_PATH="$PROJECT_ROOT/../DipanProj_Deploy"

echo "🚚 正在同步成品至 $DEPLOY_PATH..."

# 1. 搬移成品 (排除 .git)
mkdir -p "$DEPLOY_PATH"

# 關鍵修正：確保來源路徑正確指向主專案內的 Builds
rsync -av --delete --exclude ".git" "$MAIN_PROJECT_PATH/Builds/Windows_Test/" "$DEPLOY_PATH/"
if [ $? -ne 0 ]; then
    echo "❌ 同步失敗，請確認 $MAIN_PROJECT_PATH/Builds/Windows_Test/ 是否存在（Windows 是否真的建置成功）。"
    exit 1
fi

# 2. 推送到 GitHub（逐步檢查，誠實回報，不再假性成功）
cd "$DEPLOY_PATH" || { echo "❌ 進不去部署資料夾：$DEPLOY_PATH"; exit 1; }

if [ ! -d ".git" ]; then
    echo "❌ 部署資料夾不是 git repo：$DEPLOY_PATH"
    echo "   請先一次性設定（在終端機）："
    echo "   cd \"$DEPLOY_PATH\" && git init && git branch -M main && git remote add origin <你的GitHub URL> && git add . && git commit -m init && git push -u origin main"
    exit 1
fi

git add .

# 沒有變更就不算失敗，直接結束
if git diff --cached --quiet; then
    echo "ℹ️ 這次成品與遠端相同，無需推送（測試機已是最新）。"
    echo "DEPLOY_RESULT=NOCHANGE"
    exit 0
fi

git commit -m "Auto Deploy: $(date +'%Y-%m-%d %H:%M:%S')" || { echo "❌ git commit 失敗"; exit 1; }

if git push; then
    echo "🎉 已推送新版本到遠端。"
    echo "DEPLOY_RESULT=PUSHED"
else
    echo "❌ git push 失敗。常見原因：未設遠端 / 沒有上游分支 / 從 Unity 啟動的程序拿不到 git 憑證或 SSH key。"
    echo "   先在終端機手動測試： cd \"$DEPLOY_PATH\" && git push"
    exit 1
fi