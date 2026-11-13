# 用法: python3 tools/holes_black.py <in.png> <out.png> [T=14] [minArea=800] [maxMean=2.5]
# 補 cutout_black.py：被角色包住、不與四邊相連的「純黑底空洞」（手臂與身體、頭髮之間的縫）也挖透明。
# 判斷：max(rgb)<=T 的連通塊，面積 > minArea 且平均亮度 < maxMean（真底色幾乎全是 0；角色的暗部有紋理，平均會 >3）。
# 邊緣 2px 依亮度給半透明（同 cutout_black）。
import sys, numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage
src, dst = sys.argv[1], sys.argv[2]
T = int(sys.argv[3]) if len(sys.argv) > 3 else 14
minA = int(sys.argv[4]) if len(sys.argv) > 4 else 800
maxMean = float(sys.argv[5]) if len(sys.argv) > 5 else 2.5
a = np.array(Image.open(src).convert('RGBA'))
mx = a[..., :3].astype(int).max(2)
lab, k = ndimage.label((mx <= T) & (a[..., 3] > 0))
hole = np.zeros(mx.shape, bool); n = 0
if k:
    idx = np.arange(1, k + 1)
    sizes = ndimage.sum(np.ones_like(lab), lab, idx)
    means = ndimage.mean(mx, lab, idx)
    keep = idx[(sizes > minA) & (means < maxMean)]
    hole = np.isin(lab, keep); n = len(keep)
alpha = a[..., 3].astype(float)
alpha[hole] = 0
band = np.asarray(Image.fromarray((hole * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5))) > 0
band &= ~hole
soft = np.clip((mx - T) / (40 - T), 0, 1) * 255
alpha = np.where(band, np.minimum(alpha, soft), alpha)
a[..., 3] = alpha.astype(np.uint8)
Image.fromarray(a, 'RGBA').save(dst)
print(dst, 'holes', n, 'px', int(hole.sum()))
