# 用法: python3 tools/seq_measure.py <SequenceImage 底下的角色資料夾>...   例: .../SequenceImage/Feralborn/Moonwatcher
# 量每個動作（idle/walk/attack/dead）的：幀數、畫布、第一幀可見高／寬、體積尺度 √(高×√面積)、輪廓面積 √面積、
# 腳底距畫布中心（G24）、全幀中位數。dead 跟 idle 比大小、腳底差多少，一眼看出要不要修（PROBLEMS G23／G24）。
# ⚠ 第一幀是趴姿／彎腰時，體積尺度會誤判偏小——改看「sqrtA 比」或用 head_scale.py。
import sys, glob, numpy as np
from PIL import Image


def stats(p):
    a = np.asarray(Image.open(p).convert('RGBA'))[..., 3] > 10
    ys, xs = np.nonzero(a)
    H, W = a.shape
    h = ys.max() - ys.min() + 1
    area = a.sum()
    return dict(W=W, H=H, h=h, w=xs.max() - xs.min() + 1, vol=(h * area ** 0.5) ** 0.5,
                sqrtA=area ** 0.5, foot=(H - 1 - ys.max()) - H / 2)


for d in sys.argv[1:]:
    print('==', d)
    base = None
    for act in ['idle', 'walk', 'attack', 'dead']:
        fs = sorted(glob.glob(f'{d}/{act}/*.png'))
        if not fs:
            continue
        st = [stats(f) for f in fs]
        s0 = st[0]
        med = lambda k: float(np.median([s[k] for s in st]))
        if act == 'idle':
            base = (s0['vol'], med('sqrtA'), s0['foot'])
        line = (f"{act:6s} {len(fs):2d}張 畫布{s0['W']}x{s0['H']}  第一幀 高{s0['h']} 寬{s0['w']} "
                f"vol {s0['vol']:.1f} sqrtA {s0['sqrtA']:.1f} 腳底{s0['foot']:+.0f}  | 中位 vol {med('vol'):.1f}")
        if base and act != 'idle':
            line += f"  | 比 idle: vol {s0['vol']/base[0]:.3f} sqrtA {s0['sqrtA']/base[1]:.3f} 腳底差 {s0['foot']-base[2]:+.0f}px"
        print(line)
