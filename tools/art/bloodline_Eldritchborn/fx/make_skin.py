# 觸手皮：裁出觸手帶 → 白底去背（逐欄輪廓、內部不透明）→ 取 4 個吸盤週期做左右無縫
import numpy as np
from PIL import Image
a = np.asarray(Image.open('tentacle_skin_gpt.png').convert('RGB')).astype(np.float32) / 255.
Hh, W, _ = a.shape
m = a.min(2)
# 每欄上下輪廓（從外往內第一個「不是白」的像素）
inside = np.zeros((Hh, W), np.float32)
for x in range(W):
    col = m[:, x] < 0.90
    ys = np.where(col)[0]
    top, bot = ys.min(), ys.max()
    inside[top:bot + 1, x] = 1
# 邊緣抗鋸齒：輪廓外 2px 內用白底反推的 alpha
alpha_w = np.clip(1 - m, 0, 1) / 0.12
from scipy.ndimage import binary_dilation
ring = binary_dilation(inside > 0, iterations=2) & (inside == 0)
alpha = inside.copy(); alpha[ring] = np.clip(alpha_w[ring], 0, 1)
# 邊緣色：從白底反推
rgb = a.copy()
sel = (alpha > 0) & (alpha < 1)
rgb[sel] = np.clip((a[sel] - (1 - alpha[sel][:, None])) / np.maximum(alpha[sel][:, None], 1e-3), 0, 1)
rows = np.where(alpha.max(1) > 0)[0]; y0, y1 = rows.min(), rows.max() + 1
print('band', y0, y1, y1 - y0)
P = 128; N = 4; OV = 48
x0 = 3 * P
seg = np.dstack([rgb, alpha])[y0:y1, x0 - OV: x0 + N * P].copy()   # 多拿左邊 OV 當交叉淡化
core = seg[:, OV:].copy()
# 右端最後 OV 跟「左邊多拿的那段」交叉：core 尾巴 → 漸變到 core 開頭之前那段（＝週期上同一處）
w = np.linspace(0, 1, OV)[None, :, None]
pre = seg[:, :OV]
core[:, -OV:] = core[:, -OV:] * (1 - w) + pre * w
out = (np.clip(core, 0, 1) * 255).astype(np.uint8)
Image.fromarray(out, 'RGBA').save('tentacle_skin.png')
print('out', out.shape)
# 檢查：橫向並排 3 次看接縫
t = np.concatenate([core] * 3, 1)
bg = np.ones_like(t[..., :3]) * 0.18
v = t[..., :3] * t[..., 3:] + bg * (1 - t[..., 3:])
Image.fromarray((v * 255).astype(np.uint8)).save('skin_tile3.png')
