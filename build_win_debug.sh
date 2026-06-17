#!/bin/bash
# 用「批次模式」單獨建一次 Windows 版，完整打包紀錄寫到桌面 dipan_build.log，
# 並在最後印出關鍵段落，方便貼給別人看「為什麼缺資料」。
#
# 用法（執行前請先【完全關閉】DipanProj_Main 的 Unity 編輯器）：
#   bash ~/Documents/workspaces/myProject/DipanProj/build_win_debug.sh
#
# 若 Unity 版本資料夾名不同，改下面 VERSION 即可。

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"   # = DipanProj
PROJECT="$SCRIPT_DIR/DipanProj_Main"
VERSION="2022.3.62f3"
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
LOG="$HOME/Desktop/dipan_build.log"

if [ ! -x "$UNITY" ]; then
    echo "❌ 找不到 Unity 執行檔：$UNITY"
    echo "   先跑 'ls /Applications/Unity/Hub/Editor/' 看實際版本資料夾名，再改腳本裡的 VERSION。"
    exit 1
fi

if [ ! -d "$PROJECT" ]; then
    echo "❌ 找不到專案：$PROJECT"
    exit 1
fi

echo "🚀 批次打包中（請確認 DipanProj_Main 的 Unity 已關閉）... log → $LOG"
"$UNITY" -batchmode -quit -projectPath "$PROJECT" -executeMethod BuildScript.BuildWindowsOnly -logFile "$LOG"
echo "Unity 結束碼：$?"

echo ""
echo "==================== log 關鍵段落 ===================="
grep -niE "standalonewindows|win64|il2cpp|stagingarea|build report|not supported|denied|not permitted|cannot|failed|exception|error|gatekeeper|quarantine|killed" "$LOG" | tail -100
echo "======================================================"
echo "完整紀錄在：$LOG（可整份貼上或傳檔）"
