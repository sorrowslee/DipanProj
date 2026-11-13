# 用法: bash tools/grab_raw.sh <key>  → 取 ~/Downloads 最新 png（<40 秒）存成 work/<key>_gpt.png
set -e
cd "$(dirname "$0")/.."
f=$(ls -t "$HOME/mnt/Downloads"/*.png | head -1)
age=$(( $(date +%s) - $(stat -c %Y "$f") ))
echo "latest: $(basename "$f")  age=${age}s"
[ $age -lt 40 ] || { echo "STALE download"; exit 1; }
cp "$f" "work/$1_gpt.png"
python3 -c "from PIL import Image;im=Image.open('work/$1_gpt.png');print('size',im.size,im.mode)"
