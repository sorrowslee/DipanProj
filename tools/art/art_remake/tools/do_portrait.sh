# 用法: bash tools/do_portrait.sh <角色> <情緒> [wide]   收最新下載→保紫去背→去紫邊→對齊到 portrait/<角色>_<情緒>.png
set -e
cd "$(dirname "$0")/.."
k="p_${1}_${2}"
bash tools/grab_raw.sh "$k"
python3 tools/cutout_magenta_keep.py work/${k}_gpt.png work/${k}_cut.png >/dev/null
python3 tools/defringe.py work/${k}_cut.png work/${k}_cut.png >/dev/null
if [ "$3" = "wide" ]; then python3 tools/fit_portrait_wide.py work/${k}_cut.png portrait/${1}_${2}.png; else python3 tools/fit_portrait.py work/${k}_cut.png portrait/${1}_${2}.png; fi
