# 用法: bash tools/do_sprite.sh <名稱_vN> [--top Y]   收最新下載→保紫去背→去紫邊→對齊 1024 → out/<名稱_vN>.png ＋ 跟舊圖並排 work/cmp_<名稱_vN>.jpg
set -e
cd "$(dirname "$0")/.."
k="s_$1"; shift
bash tools/grab_raw.sh "$k"
python3 tools/cutout_magenta_keep.py work/${k}_gpt.png work/${k}_cut.png >/dev/null
python3 tools/defringe.py work/${k}_cut.png work/${k}_cut.png >/dev/null
python3 tools/fit_sprite.py work/${k}_cut.png out/${k#s_}.png "$@"
