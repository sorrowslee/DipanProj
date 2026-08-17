#!/usr/bin/env python3
"""
佛光光環貼圖換色工具（readme/FALLEN_BUDDHA_LIGHT.md 的附屬腳本）

把 buddhaLight_01.png 換成任意色相，保留原本的柔邊漸層與 alpha。
用法：
    python3 recolor_aura.py <原圖.png> <輸出.png> <R> <G> <B> [增益]
    例：python3 recolor_aura.py buddhaLight_01.png out.png 0.40 0.16 0.98 1.55

⚠️ 三條踩過的雷（詳見 readme/PROBLEMS.md E12）：
 1. **一定要從原始暖橘版換色**，不要拿已經染過的再染一次（會二次壓暗）。
 2. **不能只加 tint 欄位**：AuraGlow 的算式是 col = 貼圖RGB × ... ×_TintColor（相乘），
    暖橘的貼圖乘紫色 tint 只會得到暗紅褐，所以顏色必須換進貼圖本身。
 3. **暗色系要補增益**：人眼對藍紫敏感度低。相對亮度 暖橘 0.808 vs 深紫 0.415（差 1.95x）、
    vs 偏藍紫 0.270（差 2.99x）。全補會糊成白色，實測補 1.3~1.6 倍再做軟壓縮最好。
"""
import sys
from PIL import Image
import numpy as np


def rel_lum(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def recolor(src_path, dst_path, target_rgb, gain=1.0):
    target = np.array(target_rgb, dtype=np.float32)
    im = Image.open(src_path).convert("RGBA")
    a = np.asarray(im).astype(np.float32) / 255.0
    rgb, alpha = a[..., :3], a[..., 3:]

    # 用最大通道當「這個像素有多亮」的骨架 → 完整保留原本的柔邊漸層與明暗結構
    lum = rgb.max(axis=-1, keepdims=True)
    out = lum * target * gain
    # 軟壓縮：核心過亮時往白偏（加色光的自然行為），不硬切以免出現色帶
    out = out / (1.0 + np.maximum(0.0, out.max(axis=-1, keepdims=True) - 1.0))
    out = np.clip(out, 0, 1)

    Image.fromarray((np.concatenate([out, alpha], axis=-1) * 255).astype(np.uint8)).save(dst_path)

    m = np.asarray(Image.open(dst_path)).astype(np.float32)
    mask = m[..., 3] > 30
    print(f"  來源相對亮度 {rel_lum(rgb.max(axis=-1).mean()*np.array([1,1,1])):.3f} → "
          f"目標 {rel_lum(target):.3f}（差 {rel_lum([1.0,0.78,0.52])/max(rel_lum(target),1e-6):.2f}x vs 原暖橘）")
    print(f"  輸出 {dst_path}  不透明區平均 RGB≈ {[round(m[..., i][mask].mean()) for i in range(3)]}")


if __name__ == "__main__":
    if len(sys.argv) < 6:
        print(__doc__)
        sys.exit(1)
    src, dst = sys.argv[1], sys.argv[2]
    rgb = [float(x) for x in sys.argv[3:6]]
    g = float(sys.argv[6]) if len(sys.argv) > 6 else 1.0
    recolor(src, dst, rgb, g)
