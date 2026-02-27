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

if [ $? -eq 0 ]; then
    # 2. 推送到 GitHub
    cd "$DEPLOY_PATH"
    git add .
    git commit -m "Auto Deploy: $(date +'%Y-%m-%d %H:%M:%S')"
    git push
    echo "🎉 部署完成！"
else
    echo "❌ 同步失敗，請確認 $MAIN_PROJECT_PATH/Builds/Windows_Test/ 是否存在。"
    exit 1
fi