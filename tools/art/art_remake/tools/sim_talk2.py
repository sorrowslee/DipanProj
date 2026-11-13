# 模擬 TalkPanel 兩種排法（1920×1080）：主角血統立繪＝腰部以上（SetAvatarWaistUp），其他（NPC／怪物）＝內容框對齊＋PortraitTable。
# 用法: python3 tools/sim_talk2.py <out.jpg> <左圖> <右圖>   圖＝GameAssets 底下相對路徑（例 Modules/RedBridalGown/Talk/Priest_normal.png）
import sys, csv, numpy as np
from PIL import Image, ImageDraw

GA = '../../DipanProj_Main/Assets/GameAssets/'
import os
PT = os.environ.get('PT', '../../DipanProj_Main/Assets/Data/PortraitTable.csv')
SW, SH = 1920, 1080
BOX_TOP = 24 + 828 * (1500 / 2246)
# 腰部以上
HF, WF, HEADY, TOPLIM, BX = 235 / 1536, 800 / 1536, 975, 1072, 500
# 舊排法
AH, AOV, AIN, HALFBOX = 580, 130, 310, 750


def table():
    t = {}
    for row in csv.reader(open(PT, encoding='utf-8')):
        if not row or row[0].startswith('#') or row[0] == 'Id':
            continue
        f = lambda i, d: float(row[i]) if len(row) > i and row[i].strip() else d
        t[row[0].strip().lower()] = (f(1, 1.0), f(2, 0.0), f(3, 0.0), len(row) > 4 and row[4].strip() == '1')
    return t


TB = table()


def place(scr, path, right):
    im = Image.open(GA + path).convert('RGBA')
    cw, ch = im.size
    a = np.asarray(im)[..., 3] > 10
    ys, xs = np.nonzero(a)
    cid = path.rsplit('.', 1)[0].lower()
    sc, ox, oy, outer = TB.get(cid, (1.0, 0.0, 0.0, False))
    asp = cw / ch
    waist = cid.startswith('main/characters/talk/') and (abs(asp - 2 / 3) < 0.02 or abs(asp - 1.5) < 0.03)
    if waist:
        H = (HEADY - BOX_TOP) / (WF - HF) * sc
        W = H * asp
        top = HEADY + HF * H
        ctop = top - (ys.min() / ch) * H
        if ctop > TOPLIM:
            top -= ctop - TOPLIM
        bottom = top - H + oy
        cx = (700 / 1536) if cw > ch else (470 / 1024)
        if right:
            left = SW / 2 + BX - (1 - cx) * W + ox
        else:
            left = SW / 2 - BX - cx * W + ox
        mode = 'waist'
    else:
        cy = ys.max() - ys.min() + 1
        k = AH * sc / cy
        W, H = cw * k, ch * k
        # 內容框（以圖中心為原點、+上）
        right_from_c = (xs.max() + 1) - cw / 2
        bottom_from_c = ch / 2 - (ys.max() + 1)
        content_w = (xs.max() - xs.min() + 1) * k
        bottom_y = BOX_TOP - AOV + oy
        img_bottom = bottom_y - (ch / 2 + bottom_from_c) * k
        half_limit = max(HALFBOX, SW / 2) if outer else HALFBOX
        if right:
            ax = AIN + right_from_c * k + ox
            o = ax + ((xs.max() - xs.min() + 1) - (xs.max() + 1 - cw / 2)) * k  # content.x - rightFromCenter
            o = ax + ((xs.max() - xs.min() + 1) * k - right_from_c * k)
            if o > half_limit:
                ax -= o - half_limit
            left = SW / 2 + ax - W / 2
        else:
            inner_to_left = (cw / 2 + right_from_c) * k
            ax = -AIN - inner_to_left + ox
            o = ax + inner_to_left - content_w
            if o < -half_limit:
                ax += -half_limit - o
            left = SW / 2 + ax
        bottom = img_bottom
        mode = 'npc x%.2f' % sc
    if right:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    r = im.resize((max(1, round(W)), max(1, round(H))), Image.LANCZOS)
    scr.alpha_composite(r, (round(left), round(SH - bottom - H))) if left >= 0 and SH - bottom - H >= 0 else scr.paste(r, (round(left), round(SH - bottom - H)), r)
    return mode


base = Image.new('RGBA', (SW, SH), (60, 55, 50, 255))
m1 = place(base, sys.argv[2], False)
m2 = place(base, sys.argv[3], True)
d = ImageDraw.Draw(base)
d.rectangle([SW / 2 - 750, SH - BOX_TOP, SW / 2 + 750, SH - 24], fill=(30, 25, 20, 255), outline=(180, 140, 80, 255), width=6)
d.text((20, 20), f'L: {sys.argv[2]} [{m1}]', fill=(255, 255, 0))
d.text((20, 40), f'R: {sys.argv[3]} [{m2}]', fill=(255, 255, 0))
base.convert('RGB').resize((960, 540)).save(sys.argv[1], quality=85)
