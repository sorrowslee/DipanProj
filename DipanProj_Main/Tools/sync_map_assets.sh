#!/usr/bin/env bash
# 同步地圖素材到主遊戲的 StreamingAssets，供 runtime MapLoader 載入。
#
# 從 Assets/GameAssets/{Main,Modules/<關卡>} 底下，只拿 Environment/ Tiles/ Background/ Drama/ Talk/
# 這幾個資料夾的 PNG，依原相對路徑複製進 Assets/StreamingAssets/MapAssets/（無條件覆蓋），
# 並生成 catalog.json（id / path / category / module / pixelSize / ppu）。
#
# 用法：
#   ./sync_map_assets.sh            # 同步全部 module
#   ./sync_map_assets.sh <關卡>     # 只同步單一 module（例：RedBridalGown）
#
# id 慣例 = 相對 GameAssets 的路徑去副檔名，與 .dipanmap 內的 assetId / backgroundId 完全一致。
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SRC_ROOT="$PROJ_DIR/Assets/GameAssets"
DST_ROOT="$PROJ_DIR/Assets/StreamingAssets/MapAssets"
ONLY_MODULE="${1:-}"
PPU=256
# ⚠ 這份清單與 C# 版是「兩份獨立實作」，改分類時兩邊都要改：
#    ① Assets/Scripts/Map/MapAssetCategories.cs 的 All（MapIO 與 MapAssetSyncTool 共用那一份）
#    ② 這一行
#    漏改不會報錯、只會靜默少同步（見 readme/PROBLEMS.md C1/C3/C5/I4/F16）。
CATS=(Environment Tiles Background Drama Talk)

python3 - "$SRC_ROOT" "$DST_ROOT" "$ONLY_MODULE" "$PPU" "${CATS[@]}" <<'PY'
import os, sys, json, shutil, struct

src_root, dst_root, only_module, ppu = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
cats = set(sys.argv[5:])

def png_width(path):
    with open(path, 'rb') as f:
        head = f.read(24)
    if len(head) < 24 or head[:8] != b'\x89PNG\r\n\x1a\n':
        return 0
    return struct.unpack('>I', head[16:20])[0]

def source_dirs():
    # Main（共用）+ Modules/<關卡>
    main = os.path.join(src_root, 'Main')
    if os.path.isdir(main):
        yield 'Main', main
    mods = os.path.join(src_root, 'Modules')
    if os.path.isdir(mods):
        for name in sorted(os.listdir(mods)):
            p = os.path.join(mods, name)
            if os.path.isdir(p) and (not only_module or name == only_module):
                yield name, p

# 無條件覆蓋既有檔案（copy2 會覆蓋）。不整個刪資料夾，避免某些檔案系統的刪除權限問題；
# 若來源已刪除的素材想清乾淨，手動刪 MapAssets 後再跑即可。
os.makedirs(dst_root, exist_ok=True)

# 0) 先從地圖編輯器拉地圖：Main → GameAssets/Main/Maps；其它 → GameAssets/Modules/<模組>/Maps。
# src_root = .../DipanProj_Main/Assets/GameAssets → 上三層到 DipanProj，再進 DipanProj_MapEditor/Maps
editor_maps = os.path.normpath(os.path.join(src_root, '..', '..', '..', 'DipanProj_MapEditor', 'Maps'))
pulled = 0
if os.path.isdir(editor_maps):
    for module in sorted(os.listdir(editor_maps)):
        mod_dir = os.path.join(editor_maps, module)
        if not os.path.isdir(mod_dir):
            continue
        if only_module and module != only_module:
            continue
        # Main 模組 → GameAssets/Main；其它 → GameAssets/Modules/<模組>
        target_module = os.path.join(src_root, 'Main') if module == 'Main' \
            else os.path.join(src_root, 'Modules', module)
        if not os.path.isdir(target_module):
            print(f"   [警告] 編輯器有「{module}」的地圖，但 GameAssets 無對應目錄（{target_module}），略過")
            continue
        target_maps = os.path.join(target_module, 'Maps')
        os.makedirs(target_maps, exist_ok=True)
        for root, _dirs, files in os.walk(mod_dir):
            for fn in files:
                if fn.lower().endswith('.dipanmap'):
                    shutil.copy2(os.path.join(root, fn), os.path.join(target_maps, fn))
                    pulled += 1
else:
    print(f"   [警告] 找不到編輯器 Maps 資料夾，略過拉地圖：{editor_maps}")

items = []
for module, base in source_dirs():
    for cat in cats:
        cdir = os.path.join(base, cat)
        if not os.path.isdir(cdir):
            continue
        # Talk 允許「每個 NPC 一個子資料夾」（例 Talk/Buddha/Buddha_normal.png → id=…/Talk/Buddha/Buddha_normal），
        # 遞迴收所有 PNG、各成一筆靜態素材（見 PROBLEMS.md C5；其餘類別維持只收第一層，
        # 因 Environment 的子資料夾另有「動畫物件」語意，不能混用）。
        if cat == 'Talk':
            png_list = []
            for _root, _dirs, _files in sorted(os.walk(cdir)):
                for fn in sorted(_files):
                    if fn.lower().endswith('.png'):
                        png_list.append(os.path.join(_root, fn))
        else:
            png_list = [os.path.join(cdir, fn) for fn in sorted(os.listdir(cdir))
                        if fn.lower().endswith('.png')]
        for abs_src in png_list:
            rel = os.path.relpath(abs_src, src_root)          # e.g. Modules/RedBridalGown/Environment/x.png
            abs_dst = os.path.join(dst_root, rel)
            os.makedirs(os.path.dirname(abs_dst), exist_ok=True)
            shutil.copy2(abs_src, abs_dst)
            items.append({
                "id": os.path.splitext(rel)[0].replace(os.sep, '/'),
                "path": rel.replace(os.sep, '/'),
                "category": cat,
                "module": module,
                "pixelSize": png_width(abs_src),
                "ppu": ppu,
            })

    # 逐格動畫素材（路線 B）：怪物 Monsters/SequenceImage/、玩家 Characters/SequenceImage/，
    # 每個「直接含 PNG」的葉資料夾（<名稱>/<state>/）收成一筆 catalog item
    # （category=Monsters/Characters、id=資料夾相對路徑、≥2 幀帶 frameCount/frames，依檔名排序）。
    for category_dir in ('Monsters', 'Characters'):
        seq_root = os.path.join(base, category_dir, 'SequenceImage')
        if not os.path.isdir(seq_root):
            continue
        for root_dir, _dirs, files in sorted(os.walk(seq_root)):
            # 只收「子資料夾」裡的幀；直接放在 SequenceImage/ 下的散圖（舊 sheet）略過。
            if os.path.normpath(root_dir) == os.path.normpath(seq_root):
                continue
            frame_files = sorted(fn for fn in files if fn.lower().endswith('.png'))
            if not frame_files:
                continue
            frames_rel = []
            for fn in frame_files:
                abs_src = os.path.join(root_dir, fn)
                rel = os.path.relpath(abs_src, src_root)
                abs_dst = os.path.join(dst_root, rel)
                os.makedirs(os.path.dirname(abs_dst), exist_ok=True)
                shutil.copy2(abs_src, abs_dst)
                frames_rel.append(rel.replace(os.sep, '/'))
            id_rel = os.path.relpath(root_dir, src_root).replace(os.sep, '/')
            item = {
                "id": id_rel,
                "path": frames_rel[0],
                "category": category_dir,
                "module": module,
                "pixelSize": png_width(os.path.join(root_dir, frame_files[0])),
                "ppu": ppu,
            }
            if len(frames_rel) >= 2:
                item["frameCount"] = len(frames_rel)
                item["frames"] = frames_rel
            items.append(item)

    # 主角情緒立繪：Characters/Talk/<血統>/<情緒>.png（單張），複製進 StreamingAssets、各收成一筆靜態素材
    # （category=Talk、id=相對路徑去副檔名，例 Main/Characters/Talk/Base/angry）。
    # 供 DramaTalkDatabase 的 Actor_<情緒> 立繪解析（依目前血統定位）。
    talk_root = os.path.join(base, 'Characters', 'Talk')
    if os.path.isdir(talk_root):
        for root_dir, _dirs, files in sorted(os.walk(talk_root)):
            for fn in sorted(fn for fn in files if fn.lower().endswith('.png')):
                abs_src = os.path.join(root_dir, fn)
                rel = os.path.relpath(abs_src, src_root)
                abs_dst = os.path.join(dst_root, rel)
                os.makedirs(os.path.dirname(abs_dst), exist_ok=True)
                shutil.copy2(abs_src, abs_dst)
                items.append({
                    "id": os.path.splitext(rel)[0].replace(os.sep, '/'),
                    "path": rel.replace(os.sep, '/'),
                    "category": "Talk",
                    "module": module,
                    "pixelSize": png_width(abs_src),
                    "ppu": ppu,
                })

with open(os.path.join(dst_root, 'catalog.json'), 'w', encoding='utf-8') as f:
    json.dump({"items": items}, f, ensure_ascii=False, indent=2)

# 另外把每個 module 的 Maps/*.dipanmap 也複製進來（runtime 直接讀，可打包），保留相對路徑。
map_count = 0
for module, base in source_dirs():
    mdir = os.path.join(base, 'Maps')
    if not os.path.isdir(mdir):
        continue
    for fn in sorted(os.listdir(mdir)):
        if not fn.lower().endswith('.dipanmap'):
            continue
        abs_src = os.path.join(mdir, fn)
        rel = os.path.relpath(abs_src, src_root)
        abs_dst = os.path.join(dst_root, rel)
        os.makedirs(os.path.dirname(abs_dst), exist_ok=True)
        shutil.copy2(abs_src, abs_dst)
        map_count += 1

# 旗標登記表 flags.json（編輯器旗標管理器產出）：從 DipanProj_MapEditor/flags.json 帶進 StreamingAssets/MapAssets/，
# 供遊戲端 FlagRegistry 查旗標生命週期（周目/永久）。找不到就略過（尚未建登記表時）。
flag_src = os.path.normpath(os.path.join(editor_maps, '..', 'flags.json'))
flag_copied = False
if os.path.isfile(flag_src):
    shutil.copy2(flag_src, os.path.join(dst_root, 'flags.json'))
    flag_copied = True

print(f"[sync_map_assets] 從編輯器拉入 {pulled} 張地圖；推送 {len(items)} 筆素材、{map_count} 張地圖"
      f"{'、flags.json' if flag_copied else ''} → {dst_root}")
for it in items:
    print("   ", it["module"], it["category"], it["id"])
PY
