# 用法: python3 tools/sim_intro.py <out.jpg> <立繪 png>...
# 模擬 BloodlineIntroPanel（2026-11-13 第三版：單一規則）。參數與程式預設值一致，改程式記得改這裡。
#   ‧ 遮罩固定＝框內黑底：左右 CLIP_L～CLIP_R、上 CLIP_TOP、下 BOTTOM_INSET（不看新舊立繪尺寸）
#   ‧ 規格立繪（1024×1536／1536×1024）：頭頂線 235 → 框頂下 HEAD_FROM_TOP、腹部線 800 → 立繪底線；
#     身體中心線 470／700 → 破碎框中心。固定倍率，不量內容。
#   ‧ 其他尺寸：等比縮到 PortraitBox、靠下（舊素材）
# 每格右上角標出臉部檢查框（中心線 ±「黑底半寬」換回畫布像素），方便看臉有沒有被裁。
import sys
import numpy as np
from PIL import Image, ImageDraw

R = 'DipanProj_Main/Assets/Resources/'
FH = 880
BOTTOM_INSET, CLIP_TOP, CLIP_L, CLIP_R = 0.13, 0.12, 0.225, 0.80
HEAD_FROM_TOP, CROP_BOTTOM, CX_TALL, CX_WIDE = 165, 800, 470, 700
BOX = (0.78, 0.70)

frame = Image.open(R + 'UI/BloodlinePanel/BloodlinePanel_Bg.png').convert('RGBA')
title = Image.open(R + 'UI/Texts/tw/BloodlinePanel_Title.png').convert('RGBA')
plate = Image.open(R + 'UI/BloodlinePanel/BloodlinePanel_NameBg.png').convert('RGBA')
FW = round(FH * frame.width / frame.height)
PW, PH = 760, 1000
ox, oy = (PW - FW) // 2, (PH - FH) // 2


def tile(path):
    c = Image.new('RGBA', (PW, PH), (52, 48, 46, 255))
    c.alpha_composite(frame.resize((FW, FH)), (ox, oy))
    sp = Image.open(path).convert('RGBA')
    W, H = sp.size
    bottom_line = oy + FH - FH * BOTTOM_INSET          # 畫面座標（往下為正）
    cxs = ox + FW / 2
    if (W, H) in ((1024, 1536), (1536, 1024)):
        u = H / 1536
        headY, cropY = 235 * u, min(CROP_BOTTOM * u, H)
        top_target = FH * (1 - BOTTOM_INSET) - HEAD_FROM_TOP
        s = top_target / (cropY - headY)
        cx = CX_WIDE if W > H else CX_TALL
        px, py = cxs - cx * s, bottom_line - top_target - headY * s
    else:
        a = W / H
        h = FH * BOX[1]; w = h * a
        if a <= 1.0 and w > FW * BOX[0]:
            w = FW * BOX[0]; h = w / a
        s = h / H
        px, py = cxs - w / 2, bottom_line - h
    big = sp.resize((max(1, round(W * s)), max(1, round(H * s))), Image.LANCZOS)
    layer = Image.new('RGBA', (PW, PH), (0, 0, 0, 0))
    layer.paste(big, (round(px), round(py)), big)
    a = np.array(layer)
    x0, x1 = int(ox + FW * CLIP_L), int(ox + FW * CLIP_R)
    y0, y1 = int(oy + FH * CLIP_TOP), int(bottom_line)
    m = np.zeros(a.shape[:2], bool); m[y0:y1, x0:x1] = True
    a[~m, 3] = 0
    c.alpha_composite(Image.fromarray(a))
    tw = 370; th = round(tw * title.height / title.width)
    c.alpha_composite(title.resize((tw, th)), (round(cxs - tw / 2), round(oy + 78 - th / 2)))
    pw, ph = 360, 120
    c.alpha_composite(plate.resize((pw, ph)), (round(cxs - pw / 2), round(oy + FH - 105 - ph / 2)))
    d = ImageDraw.Draw(c)
    d.text((10, 10), '/'.join(path.split('/')[-3:-1]), fill=(255, 255, 255))
    return c.convert('RGB')


if __name__ == '__main__':
    tiles = [tile(p) for p in sys.argv[2:]]
    cols = min(8, len(tiles)); rows = (len(tiles) + cols - 1) // cols
    S = Image.new('RGB', (PW * cols, PH * rows), (30, 30, 30))
    for i, t in enumerate(tiles):
        S.paste(t, ((i % cols) * PW, (i // cols) * PH))
    S.thumbnail((3200, 3200))
    S.save(sys.argv[1], quality=85)
