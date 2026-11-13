# 用法: bash tools/install.sh <key> <Talk 下的相對路徑，例 Blazeborn/Fafnir>
# 備份原檔到 backup/Talk/<路徑>/（已有就不覆蓋）→ 六張覆蓋進遊戲 → 記到 installed.txt
set -e
cd "$(dirname "$0")/.."
T="../../DipanProj_Main/Assets/GameAssets/Main/Characters/Talk/$2"
mkdir -p "backup/Talk/$2"
for e in normal happy cry hurt angry fear; do
  [ -f "backup/Talk/$2/$e.png" ] || cp "$T/$e.png" "backup/Talk/$2/$e.png"
  [ -f "portrait/$1_$e.png" ] || { echo "缺 portrait/$1_$e.png"; exit 1; }
done
for e in normal happy cry hurt angry fear; do cp "portrait/$1_$e.png" "$T/$e.png"; done
grep -qxF "$2" installed.txt 2>/dev/null || echo "$2" >> installed.txt
echo "installed $2"
