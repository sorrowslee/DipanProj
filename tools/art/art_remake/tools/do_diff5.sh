# 用法: bash tools/do_diff5.sh <key>   收 ~/Downloads 最新一批「-1～-5」（120 秒內）→ 1開心 2難過 3受傷 4憤怒 5畏懼 → 自動去背 → 對齊 portrait/<key>_normal.png
set -e
cd "$(dirname "$0")/.."
emo=(x happy cry hurt angry fear)
now=$(date +%s)
for i in 1 2 3 4 5; do
  f=$(ls -t "$HOME/mnt/Downloads"/*-$i.png | head -1)
  age=$(( now - $(stat -c %Y "$f") )); [ $age -lt ${MAXAGE:-120} ] || { echo "STALE $i $f"; exit 1; }
  k="d_$1_${emo[$i]}"; cp "$f" "work/${k}_gpt.png"
  echo "$i → ${emo[$i]}  ($(basename "$f"))"
  python3 tools/autocut.py work/${k}_gpt.png work/${k}_cut.png
  python3 tools/align_diff.py portrait/$1_normal.png work/${k}_cut.png portrait/$1_${emo[$i]}.png
done
