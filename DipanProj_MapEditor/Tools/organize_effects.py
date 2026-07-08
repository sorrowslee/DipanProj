#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
organize_effects.py — 把「Super Pixel Effects Gigapack」整理成預覽器/遊戲可用的乾淨結構。

做的事：
- 來源：DipanProj_MapEditor/Assets/Resources/Super Pixel Effects Gigapack/PNG
- 只取 PNG + large 尺寸（忽略 small、spritesheet、.meta）
- 保留分層：<類別>/<效果>/<顏色>/
- 幀改名成我們的慣例：<效果>_001.png 起（1-based、補零 3 位）
- 輸出：DipanProj_MapEditor/Assets/StreamingAssets/Effects
- 複製（不動原始 Gigapack）；可重複執行（預設會先清空輸出夾再重建）

用法：
  python3 organize_effects.py            整理全部
  python3 organize_effects.py --dry      只統計、不寫檔
  python3 organize_effects.py --exclude Symbols    整理時排除某些類別
"""
import os
import re
import sys
import shutil

EDITOR_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(EDITOR_ROOT, "Assets", "Resources", "Super Pixel Effects Gigapack", "PNG")
DST = os.path.join(EDITOR_ROOT, "Assets", "StreamingAssets", "Effects")

PAD = 3  # 幀補零位數（有效果達 120 幀，故用 3 位）


def natural_key(name):
    """依檔名自然排序（frame0000 < frame0010），穩定當作播放順序。"""
    return [int(t) if t.isdigit() else t.lower()
            for t in re.split(r"(\d+)", name)]


def main():
    dry = "--dry" in sys.argv
    excludes = set()
    if "--exclude" in sys.argv:
        i = sys.argv.index("--exclude")
        excludes = set(sys.argv[i + 1:i + 2])

    if not os.path.isdir(SRC):
        print("[錯誤] 找不到來源資料夾：", SRC)
        sys.exit(1)

    if not dry and os.path.isdir(DST):
        shutil.rmtree(DST)  # 一律以來源為準，先清空重建（可重複執行）

    n_cat = n_eff = n_var = n_frame = 0
    per_cat = {}

    for cat in sorted(os.listdir(SRC)):
        cat_dir = os.path.join(SRC, cat)
        if not os.path.isdir(cat_dir) or cat in excludes:
            continue
        n_cat += 1
        for eff in sorted(os.listdir(cat_dir)):
            eff_dir = os.path.join(cat_dir, eff)
            if not os.path.isdir(eff_dir):
                continue
            eff_had_variant = False
            for var in sorted(os.listdir(eff_dir)):
                var_dir = os.path.join(eff_dir, var)
                if not os.path.isdir(var_dir):
                    continue
                if "_large_" not in var:      # 只取 large 尺寸
                    continue
                color = var.split("_large_", 1)[1]
                pngs = sorted(
                    (f for f in os.listdir(var_dir) if f.lower().endswith(".png")),
                    key=natural_key,
                )
                if not pngs:
                    continue
                out_dir = os.path.join(DST, cat, eff, color)
                if not dry:
                    os.makedirs(out_dir, exist_ok=True)
                    for idx, fn in enumerate(pngs, start=1):
                        new_name = f"{eff}_{idx:0{PAD}d}.png"
                        shutil.copyfile(os.path.join(var_dir, fn),
                                        os.path.join(out_dir, new_name))
                n_var += 1
                n_frame += len(pngs)
                eff_had_variant = True
            if eff_had_variant:
                n_eff += 1
                per_cat[cat] = per_cat.get(cat, 0) + 1

    print("=" * 48)
    print("整理" + ("（乾跑，未寫檔）" if dry else "完成"))
    print(f"  類別 {n_cat}、效果 {n_eff}、變體(效果×色) {n_var}、幀 {n_frame}")
    for c in sorted(per_cat):
        print(f"    {c:<16} {per_cat[c]} 個效果")
    print("  輸出：", DST)
    print("=" * 48)


if __name__ == "__main__":
    main()
