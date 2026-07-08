#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
organize_bundle.py — 把 allEffects/ 底下「一大堆結構各異的 unTied 特效包」通用整理進 Effects/。

作法（不管每包內部長怎樣都通用）：
- 遞迴找「直接含 frameXXXX.png 的資料夾」= 一個動畫序列（spritesheet 等非 frame 檔自動略過）。
- 只取 large 尺寸（skip small）；沒有 size 標記的（如 wills 系列）一律保留。
- 變體(顏色/風格)偵測：<名稱>_large_<色> 的 <色>、或 style_A~D、否則 default。
- 正規化輸出成預覽器吃的三層結構：Effects/<包名>/<動畫名>/<變體>/<動畫名>_NNN.png
- 跳過 Super Pixel Effects Gigapack（已由 organize_effects.py 整理過）。

輸出：DipanProj_MapEditor/Effects（Assets 外，Unity 不追蹤）。複製、可重跑。
用法：python3 organize_bundle.py [--dry]
"""
import os
import re
import sys
import shutil

EDITOR_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(EDITOR_ROOT, "allEffects")
DST = os.path.join(EDITOR_ROOT, "Effects")

SKIP_PACKS = {"Super Pixel Effects Gigapack"}  # 已整理過，避免重複

FRAME_RE = re.compile(r"^frame\d+\.png$", re.IGNORECASE)
# size/color 可能在段中間、後面還有字尾（如 name_small_blue_back）：色=size 之後第一個 token，其餘併入名稱
SIZECOLOR_RE = re.compile(r"^(.*?)_(large|small)_(.+)$", re.IGNORECASE)
STYLE_RE = re.compile(r"^style_[A-Za-z0-9]+$", re.IGNORECASE)
WRAPPER_SEGS = {"png", "frames"}  # 純容器層，命名時略過


def nkey(s):
    return [int(t) if t.isdigit() else t.lower() for t in re.split(r"(\d+)", s)]


def normalize(rel_segments):
    """把 leaf 的相對路徑段 → (anim_name, variant, size)。size='small' 表示要跳過。"""
    segs = [s for s in rel_segments if s.lower() not in WRAPPER_SEGS]
    size = None
    variant = None
    name_parts = []
    for seg in segs:
        m = SIZECOLOR_RE.match(seg)
        if m:
            size = m.group(2).lower()
            rest = m.group(3).split("_")
            variant = rest[0].lower()          # 色
            if m.group(1):
                name_parts.append(m.group(1))  # size 之前的名稱
            name_parts.extend(rest[1:])         # size/色 之後的字尾(如 back/front)併入名稱
            continue
        if STYLE_RE.match(seg):
            variant = seg.lower()
            continue
        name_parts.append(seg)
    anim = "_".join(name_parts) if name_parts else "anim"
    return anim, (variant or "default"), size


def main():
    dry = "--dry" in sys.argv
    filters = [a for a in sys.argv[1:] if not a.startswith("--")]  # 只處理名稱含這些子字串的包
    if not os.path.isdir(SRC):
        print("[錯誤] 找不到來源：", SRC)
        sys.exit(1)

    packs = sorted(d for d in os.listdir(SRC) if os.path.isdir(os.path.join(SRC, d)))
    if filters:
        packs = [p for p in packs if any(f in p for f in filters)]
    grand_anim = grand_frames = 0
    report = []

    for pack in packs:
        if pack in SKIP_PACKS:
            report.append((pack, "略過（已整理）", 0, 0))
            continue
        pack_dir = os.path.join(SRC, pack)
        pack_anim = pack_frames = skipped_small = 0

        for root, _dirs, files in os.walk(pack_dir):
            frames = sorted((f for f in files if FRAME_RE.match(f)), key=nkey)
            if len(frames) < 1:
                continue
            rel = os.path.relpath(root, pack_dir)
            rel_segments = [] if rel == "." else rel.split(os.sep)
            anim, variant, size = normalize(rel_segments)
            if size == "small":
                skipped_small += 1
                continue

            out_dir = os.path.join(DST, pack, anim, variant)
            width = max(3, len(str(len(frames))))
            if not dry:
                os.makedirs(out_dir, exist_ok=True)
                # 沙箱不允許刪除既有檔，改用 copyfile 覆寫（正確命名不含 _large_/_small_，不會與舊垃圾撞名）
                for i, f in enumerate(frames, 1):
                    shutil.copyfile(os.path.join(root, f),
                                    os.path.join(out_dir, f"{anim}_{i:0{width}d}.png"))
            pack_anim += 1
            pack_frames += len(frames)

        grand_anim += pack_anim
        grand_frames += pack_frames
        note = f"skip small×{skipped_small}" if skipped_small else ""
        report.append((pack, note, pack_anim, pack_frames))

    print("=" * 66)
    print("整理" + ("（乾跑）" if dry else "完成") + f" → {DST}")
    print(f"{'包':42s}{'動畫':>6}{'幀':>8}  備註")
    for pack, note, a, f in report:
        print(f"{pack[:40]:42s}{a:>6}{f:>8}  {note}")
    print("-" * 66)
    print(f"{'總計':42s}{grand_anim:>6}{grand_frames:>8}")
    print("=" * 66)


if __name__ == "__main__":
    main()
