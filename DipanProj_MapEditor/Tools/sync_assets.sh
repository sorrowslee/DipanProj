#!/usr/bin/env bash
#
# sync_assets.sh — 地圖編輯器「開啟前置」資源同步腳本
# ---------------------------------------------------------------
# 從主專案 DipanProj_Main 把每個來源底下的 Environment（地上物）與
# Tiles（地磚）兩個資料夾，無條件覆蓋拷進本編輯器的 StreamingAssets/MapAssets/，
# 並生成 catalog.json，保證編輯時用的素材與主遊戲完全相同、路徑/ID 一致。
#
# 預設搬「Main 共用 + 全部 Modules」，編輯器內再用下拉選 module；
# catalog 每筆都標記 module（Main / 關卡名），編輯器靠它過濾、避免跨 module 混用。
#
# 用法：
#   ./sync_assets.sh            搬 Main + 所有 module（預設、推薦）
#   ./sync_assets.sh RedBridalGown   只搬 Main + 指定 module（省時用）
#
# 需要 bash + 標準工具（od / find）；Mac、Linux、Windows git-bash 皆可。
set -euo pipefail

ONLY="${1:-}"   # 留空 = 全部 module

# --- 路徑推導（以腳本所在位置為基準，跨機器穩定）---
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EDITOR_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"          # DipanProj_MapEditor/
REPO_ROOT="$(cd "$EDITOR_ROOT/.." && pwd)"           # DipanProj/
MAIN_ASSETS="$REPO_ROOT/DipanProj_Main/Assets/GameAssets"
TARGET="$EDITOR_ROOT/Assets/StreamingAssets/MapAssets"
CATALOG="$TARGET/catalog.json"

echo "主專案素材 : $MAIN_ASSETS"
echo "目標       : $TARGET"

if [[ ! -d "$MAIN_ASSETS/Main" ]]; then
  echo "✗ 找不到共用資源夾：$MAIN_ASSETS/Main" >&2; exit 1
fi

# 要搬的 module 清單
if [[ -n "$ONLY" ]]; then
  if [[ ! -d "$MAIN_ASSETS/Modules/$ONLY" ]]; then
    echo "✗ 找不到關卡資源夾：$MAIN_ASSETS/Modules/$ONLY" >&2
    echo "  （可用關卡：$(ls "$MAIN_ASSETS/Modules" 2>/dev/null | grep -v '\.meta$' | tr '\n' ' '))" >&2
    exit 1
  fi
  MODULES=("$ONLY")
else
  MODULES=()
  while IFS= read -r d; do MODULES+=("$d"); done \
    < <(ls "$MAIN_ASSETS/Modules" 2>/dev/null | grep -v '\.meta$')
fi
echo "Module     : ${MODULES[*]:-（無）}"

# --- 一律以主專案為準：清空目標再重建 ---
rm -rf "$TARGET"
mkdir -p "$TARGET"

# --- 讀 PNG 寬度（IHDR big-endian，純 od，不依賴 ImageMagick）---
png_width() {
  local b
  b=$(od -An -tu1 -j16 -N4 "$1") || { echo 0; return; }
  set -- $b
  echo $(( ($1<<24) + ($2<<16) + ($3<<8) + $4 ))
}

# --- 拷貝一個來源樹下的所有 PNG，保留相對路徑 ---
declare -a IDS PATHS CATS SIZES MODS
copy_tree() {
  local src_root="$1" prefix="$2" module="$3"
  [[ -d "$src_root" ]] || return 0
  while IFS= read -r -d '' f; do
    local rel="${f#$src_root/}"
    local dest_rel="$prefix/$rel"
    local dest="$TARGET/$dest_rel"
    mkdir -p "$(dirname "$dest")"
    cp "$f" "$dest"

    local id="${dest_rel%.png}"
    local cat; cat="$(basename "$(dirname "$f")")"
    local w; w="$(png_width "$f")"

    IDS+=("$id"); PATHS+=("$dest_rel"); CATS+=("$cat"); SIZES+=("$w"); MODS+=("$module")
  done < <(find "$src_root" -type f -iname '*.png' -print0)
}

# 只搬每個來源底下的 Environment（地上物）/ Tiles（地磚）/ Background（背景圖）
copy_module() {
  local base="$1" prefix="$2" module="$3"
  copy_tree "$base/Environment" "$prefix/Environment" "$module"
  copy_tree "$base/Tiles"       "$prefix/Tiles"       "$module"
  copy_tree "$base/Background"   "$prefix/Background"   "$module"
}

copy_module "$MAIN_ASSETS/Main" "Main" "Main"
for m in "${MODULES[@]}"; do
  copy_module "$MAIN_ASSETS/Modules/$m" "Modules/$m" "$m"
done

# --- 生成 catalog.json ---
{
  echo '{'
  echo '  "items": ['
  n=${#IDS[@]}
  for ((i=0; i<n; i++)); do
    comma=','; [[ $i -eq $((n-1)) ]] && comma=''
    printf '    { "id": "%s", "path": "%s", "category": "%s", "module": "%s", "pixelSize": %s, "ppu": 256 }%s\n' \
      "${IDS[$i]}" "${PATHS[$i]}" "${CATS[$i]}" "${MODS[$i]}" "${SIZES[$i]}" "$comma"
  done
  echo '  ]'
  echo '}'
} > "$CATALOG"

echo "✓ 已同步 ${#IDS[@]} 張 PNG（module: ${MODULES[*]:-Main only}），catalog → $CATALOG"
