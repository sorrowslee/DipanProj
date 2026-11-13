# 用法: python3 tools/head_scale.py <SequenceImage 底下的角色資料夾> [動作...]
# 用「頭部模板比對」量各動作相對 idle 的真實大小（姿勢不同時比體積公式準）：
# 取 idle 第一幀頂端往下 70px 的頭部當模板，在其他動作的幾張幀裡用 0.8～1.8 倍縮放做 matchTemplate，取最相似的倍率。
# 輸出 >1 代表該動作畫得比 idle 大。idle 自己的幾幀會 ≈1.00（相似度 >0.9）可當驗證。
# 2026-11-13 望月者 v3：dead 量到 1.52、walk 1.01～1.12（需要 opencv：pip install opencv-python）。
import sys, glob, numpy as np, cv2


def load(p):
    a = cv2.imread(p, cv2.IMREAD_UNCHANGED)
    al = a[..., 3:4] / 255.0
    return (a[..., :3] * al + 90 * (1 - al)).astype(np.uint8), a[..., 3]


d = sys.argv[1]
acts = sys.argv[2:] or ['walk', 'attack', 'dead']
idle, ia = load(sorted(glob.glob(d + '/idle/*.png'))[0])
ys, xs = np.nonzero(ia > 10)
top = ys.min()
cx = int(np.median(xs[ys < top + 40]))
T = idle[top:top + 70, max(0, cx - 45):cx + 45]


def best(img, lo, hi):
    b = (-1, 0)
    for s in np.arange(lo, hi, 0.01):
        t = cv2.resize(T, None, fx=s, fy=s, interpolation=cv2.INTER_AREA)
        if t.shape[0] >= img.shape[0] or t.shape[1] >= img.shape[1]:
            continue
        v = cv2.matchTemplate(img, t, cv2.TM_CCOEFF_NORMED).max()
        if v > b[0]:
            b = (v, s)
    return round(float(b[1]), 2), round(float(b[0]), 3)


for act in acts:
    fs = sorted(glob.glob(f'{d}/{act}/*.png'))
    if not fs:
        continue
    print(act, [(k + 1, *best(load(fs[k])[0], 0.8, 1.8)) for k in sorted({0, len(fs) // 4, len(fs) // 2})])
ifs = sorted(glob.glob(d + '/idle/*.png'))
print('idle(驗證)', [(k + 1, *best(load(ifs[k])[0], 0.8, 1.3)) for k in sorted({len(ifs) // 4, len(ifs) // 2})])
