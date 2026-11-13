# 用法: bash tools/grab.sh <key>  → 把下載資料夾最新 png 收為 work/<key>_gpt.png 並跑 post
set -e
cd "$(dirname "$0")/.."
f=$(ls -t "$HOME/mnt/Downloads"/*.png | head -1)
age=$(( $(date +%s) - $(stat -c %Y "$f") ))
echo "latest: $(basename "$f")  age=${age}s"
[ $age -lt 40 ] || { echo "STALE download"; exit 1; }
cp "$f" "work/$1_gpt.png"
shift_key=$1; shift
python3 tools/post.py "$shift_key" "work/${shift_key}_gpt.png" "$@"
