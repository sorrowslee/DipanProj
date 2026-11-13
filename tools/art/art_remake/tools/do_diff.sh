# 用法: bash tools/do_diff.sh <key> <情緒>   收最新下載 → 自動去背 → 對齊 portrait/<key>_normal.png → portrait/<key>_<情緒>.png
set -e
cd "$(dirname "$0")/.."
k="d_$1_$2"
bash tools/grab_raw.sh "$k"
python3 tools/autocut.py work/${k}_gpt.png work/${k}_cut.png
python3 tools/align_diff.py portrait/$1_normal.png work/${k}_cut.png portrait/$1_$2.png
