#!/bin/bash
# 打包「之後」執行：用 butler 把 Windows 成品「增量」上傳到 itch.io。
# 差分上傳（只傳有變動的位元組）、無單檔大小限制、不進 git —— 取代舊的 rsync + git push 流程。
#
# 前置（一次性）：在終端機 ./butler login 過一次（憑證存本機，之後免登入）。
# 若從 Unity GUI 觸發時 butler 找不到憑證，可改在環境變數設 BUTLER_API_KEY。

set -o pipefail

PROJECT_ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"     # = DipanProj
MAIN_PROJECT_PATH="$PROJECT_ROOT/DipanProj_Main"
BUILD_DIR="$MAIN_PROJECT_PATH/Builds/Windows_Test"

# itch 目標：帳號/專案:channel
TARGET="sorrowslee/dipan:windows"

# butler 執行檔位置：優先環境變數 BUTLER，其次 PATH 上的 butler，再退回 ~/butler/butler。
BUTLER="${BUTLER:-butler}"
if ! command -v "$BUTLER" >/dev/null 2>&1 && [ -x "$HOME/butler/butler" ]; then
    BUTLER="$HOME/butler/butler"
fi
if ! command -v "$BUTLER" >/dev/null 2>&1 && [ ! -x "$BUTLER" ]; then
    echo "❌ 找不到 butler。請確認已安裝，或設環境變數 BUTLER 指向 butler 執行檔（例如 ~/butler/butler）。"
    exit 1
fi

if [ ! -d "$BUILD_DIR" ]; then
    echo "❌ 找不到成品資料夾：$BUILD_DIR（Windows 是否真的建置成功？）"
    exit 1
fi

# 版本號：用日期時間，方便在 itch 後台辨識這是哪次的 build。
USERVERSION="$(date +'%Y.%m.%d-%H%M')"

echo "🚚 用 butler 增量上傳成品到 itch：$TARGET（版本 $USERVERSION）..."

# --ignore：排除不需上架的 Burst 除錯資訊（名稱本身就寫 DoNotShip）。
"$BUTLER" push "$BUILD_DIR" "$TARGET" \
    --userversion "$USERVERSION" \
    --ignore "*_BurstDebugInformation_DoNotShip/*"
RC=$?

if [ $RC -eq 0 ]; then
    echo "🎉 已上傳到 itch（$TARGET，版本 $USERVERSION）。測試機用 itch app 或 butler 取得最新版即可。"
    echo "DEPLOY_RESULT=PUSHED"
    exit 0
else
    echo "❌ butler push 失敗（exit $RC）。常見原因：沒 ./butler login、網路問題、或 Unity GUI 環境找不到 butler / 憑證。"
    echo "   先在終端機手動測試： \"$BUTLER\" push \"$BUILD_DIR\" \"$TARGET\""
    exit 1
fi
