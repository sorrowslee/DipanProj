#!/usr/bin/env bash
#
# sync_assets.sh — 地圖編輯器「開啟前置」資源同步腳本
# ---------------------------------------------------------------
# 從主專案 DipanProj_Main 把每個來源底下的 Environment（地上物）、Tiles（地磚）、
# Background（背景圖）三個資料夾，無條件覆蓋拷進本編輯器的 StreamingAssets/MapAssets/，
# 並生成 catalog.json，保證編輯時用的素材與主遊戲完全相同、路徑/ID 一致。
#
# 動畫地上物（多張圖做成一個物件）：
#   在 Environment/ 底下放一個「子資料夾」，裡面放該物件的多張幀圖（建議補零命名，
#   例如 frame_01.png … frame_08.png，依檔名排序即播放順序）。同步時會把整個資料夾
#   收成「一筆」catalog item（category 仍是 Environment、id = 資料夾相對路徑），
#   並附上 frameCount 與 frames（各幀相對路徑）。直接放在 Environment/ 的單張 PNG 維持為靜態物件。
#
# 預設搬「Main 共用 + 全部 Modules」，編輯器內再用下拉選 module；
# catalog 每筆都標記 module（Main / 關卡名），編輯器靠它過濾、避免跨 module 混用。
#
# 用法：
#   ./sync_assets.sh            搬 Main + 所有 module（預設、推薦）
#   ./sync_assets.sh RedBridalGown   只搬 Main + 指定 module（省時用）
#
# 需要 bash + 標準工具（od / find / sort）；Mac、Linux、Windows git-bash 皆可。
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

# 每筆 catalog 記錄並列存於這些陣列；FCOUNTS / FRAMES_JSON 用於動畫物件（靜態 = 1 / 空字串）。
declare -a IDS PATHS CATS SIZES MODS FCOUNTS FRAMES_JSON

# 登記一筆素材（靜態單張）。
register_static() {
  local id="$1" path="$2" cat="$3" mod="$4" size="$5"
  IDS+=("$id"); PATHS+=("$path"); CATS+=("$cat"); MODS+=("$mod"); SIZES+=("$size")
  FCOUNTS+=(1); FRAMES_JSON+=("")
}

# --- 拷貝某來源資料夾「直接位於其下」的 PNG（不遞迴），每張登記成靜態素材 ---
# 用於 Tiles / Background，以及 Environment 內直接擺放的單張靜態物件。
copy_flat() {
  local src_root="$1" prefix="$2" module="$3" category="$4"
  [[ -d "$src_root" ]] || return 0
  while IFS= read -r -d '' f; do
    local rel="${f#"$src_root"/}"
    local dest_rel="$prefix/$rel"
    local dest="$TARGET/$dest_rel"
    mkdir -p "$(dirname "$dest")"
    cp "$f" "$dest"
    register_static "${dest_rel%.png}" "$dest_rel" "$category" "$module" "$(png_width "$f")"
  done < <(find "$src_root" -maxdepth 1 -type f -iname '*.png' -print0)
}

# --- Environment：直接擺的單張 = 靜態；每個子資料夾 = 一個動畫物件 ---
copy_environment() {
  local base="$1" prefix="$2" module="$3"
  local env="$base/Environment"
  [[ -d "$env" ]] || return 0

  # 1) 直接位於 Environment/ 的單張 PNG → 靜態物件（沿用原行為）。
  copy_flat "$env" "$prefix/Environment" "$module" "Environment"

  # 2) Environment/ 底下的每個子資料夾 → 一個動畫物件（多幀收成一筆）。
  local d
  while IFS= read -r d; do
    [[ -n "$d" ]] || continue
    local name; name="$(basename "$d")"

    # 收集子資料夾內的 PNG，依檔名排序 = 播放順序（建議補零命名）。
    local frames=()
    while IFS= read -r fr; do
      [[ -n "$fr" ]] && frames+=("$fr")
    done < <(find "$d" -maxdepth 1 -type f -iname '*.png' | LC_ALL=C sort)
    [[ ${#frames[@]} -gt 0 ]] || continue

    # 拷貝各幀、記錄相對路徑。
    local frames_rel=()
    for fr in "${frames[@]}"; do
      local frel="$prefix/Environment/$name/$(basename "$fr")"
      local fdest="$TARGET/$frel"
      mkdir -p "$(dirname "$fdest")"
      cp "$fr" "$fdest"
      frames_rel+=("$frel")
    done

    if [[ ${#frames_rel[@]} -lt 2 ]]; then
      # 只有一張 → 當靜態（避免「單張資料夾」變成意義不大的動畫）。
      register_static "$prefix/Environment/$name" "${frames_rel[0]}" "Environment" "$module" "$(png_width "${frames[0]}")"
      continue
    fi

    # 組 frames JSON 陣列字串。
    local fj="[" j
    for ((j=0; j<${#frames_rel[@]}; j++)); do
      [[ $j -gt 0 ]] && fj+=", "
      fj+="\"${frames_rel[$j]}\""
    done
    fj+="]"

    IDS+=("$prefix/Environment/$name")
    PATHS+=("${frames_rel[0]}")               # 第一幀 = 預覽/whole sprite/碰撞框來源
    CATS+=("Environment")
    MODS+=("$module")
    SIZES+=("$(png_width "${frames[0]}")")
    FCOUNTS+=("${#frames_rel[@]}")
    FRAMES_JSON+=("$fj")
  done < <(find "$env" -mindepth 1 -maxdepth 1 -type d | LC_ALL=C sort)
}

# 每個來源底下：Environment（含動畫子資料夾）/ Tiles / Background
copy_module() {
  local base="$1" prefix="$2" module="$3"
  copy_environment "$base" "$prefix" "$module"
  copy_flat "$base/Tiles"      "$prefix/Tiles"      "$module" "Tiles"
  copy_flat "$base/Background"  "$prefix/Background"  "$module" "Background"
}

copy_module "$MAIN_ASSETS/Main" "Main" "Main"
for m in "${MODULES[@]}"; do
  copy_module "$MAIN_ASSETS/Modules/$m" "Modules/$m" "$m"
done

# --- 生成 catalog.json（動畫物件多帶 frameCount / frames）---
{
  echo '{'
  echo '  "items": ['
  n=${#IDS[@]}
  for ((i=0; i<n; i++)); do
    comma=','; [[ $i -eq $((n-1)) ]] && comma=''
    extra=''
    if [[ "${FCOUNTS[$i]}" -gt 1 ]]; then
      extra=", \"frameCount\": ${FCOUNTS[$i]}, \"frames\": ${FRAMES_JSON[$i]}"
    fi
    printf '    { "id": "%s", "path": "%s", "category": "%s", "module": "%s", "pixelSize": %s, "ppu": 256%s }%s\n' \
      "${IDS[$i]}" "${PATHS[$i]}" "${CATS[$i]}" "${MODS[$i]}" "${SIZES[$i]}" "$extra" "$comma"
  done
  echo '  ]'
  echo '}'
} > "$CATALOG"

anim=0; for c in "${FCOUNTS[@]}"; do [[ "$c" -gt 1 ]] && anim=$((anim+1)); done
echo "✓ 已同步 ${#IDS[@]} 筆素材（其中動畫物件 $anim 個；module: ${MODULES[*]:-Main only}），catalog → $CATALOG"
