# 印出 ~/Downloads 中比參考檔更新的最新 png
ls -t "$HOME/mnt/Downloads"/*.png 2>/dev/null | head -1
