#!/bin/bash
# 打包「之前」執行：把 DipanProj_Deploy 本地 main 無條件對齊遠端 main，
# 避免之後 rsync 新成品 + push 時因為「本地落後遠端」而推送失敗。
# 有任何衝突/本地改動，一律以遠端 main 為準（reset --hard + clean）。

PROJECT_ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"   # = DipanProj
DEPLOY_PATH="$PROJECT_ROOT/../DipanProj_Deploy"

if [ ! -d "$DEPLOY_PATH/.git" ]; then
    echo "❌ 部署資料夾不是 git repo：$DEPLOY_PATH"
    echo "   請先一次性設定： cd \"$DEPLOY_PATH\" && git init && git branch -M main && git remote add origin <你的GitHub URL> && git add . && git commit -m init && git push -u origin main"
    exit 1
fi

cd "$DEPLOY_PATH" || { echo "❌ 進不去部署資料夾：$DEPLOY_PATH"; exit 1; }

echo "🔄 同步 DipanProj_Deploy 至遠端 main..."

git fetch origin || { echo "❌ git fetch 失敗（檢查網路 / 遠端 / 認證）。"; exit 1; }

# 遠端必須有 main 分支
if ! git rev-parse --verify --quiet origin/main >/dev/null; then
    echo "❌ 遠端沒有 origin/main 分支，請先在部署資料夾 push 一次 main。"
    exit 1
fi

# 強制本地 main 指向遠端 main，工作區與未追蹤檔一併對齊（無條件以遠端為準）
git checkout -B main origin/main || { echo "❌ 切換/建立 main 失敗。"; exit 1; }
git reset --hard origin/main      || { echo "❌ reset --hard origin/main 失敗。"; exit 1; }
git clean -fd

echo "✅ 已對齊遠端 main：$(git rev-parse --short HEAD)"
