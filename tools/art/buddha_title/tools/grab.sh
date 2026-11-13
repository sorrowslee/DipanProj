#!/bin/bash
# 用法：grab.sh <輸出檔名>  —— 取 ~/Downloads 最新 png（60 秒內），複製到 work/
D=$HOME/mnt/Downloads; W=$HOME/mnt/DipanProj/TempImage/buddha_title/work
f=$(ls -t "$D"/*.png | head -1); age=$(( $(date +%s) - $(stat -c %Y "$f") ))
if [ $age -gt 60 ]; then echo "STALE $age s: $f"; exit 1; fi
cp "$f" "$W/$1" && echo "OK $1 <- $(basename "$f") (${age}s)"
