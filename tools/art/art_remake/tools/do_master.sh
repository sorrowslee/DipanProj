# 用法: bash tools/do_master.sh <key> [grey]   收最新下載 → 去背（預設洋紅保紫；grey＝灰底）→ 貼底 → portrait/<key>_normal_raw.png
set -e
cd "$(dirname "$0")/.."
k="m_$1"
bash tools/grab_raw.sh "$k"
if [ "$2" = "grey" ]; then python3 tools/cutout.py work/${k}_gpt.png work/${k}_cut.png >/dev/null; else python3 tools/cutout_magenta_keep.py work/${k}_gpt.png work/${k}_cut.png >/dev/null; python3 tools/defringe.py work/${k}_cut.png work/${k}_cut.png >/dev/null; fi
python3 tools/fit_portrait_std.py work/${k}_cut.png portrait/$1_normal_raw.png
