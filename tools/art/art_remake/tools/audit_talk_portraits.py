# 主角對話立繪全體盤點（2026-11-25）：照 TalkPanel.SetAvatarWaistUp 算每個血統六張表情在畫面上的落點。
# 用法（repo 根目錄）：python3 tools/art/art_remake/tools/audit_talk_portraits.py
# 欄位：頂(畫面)＝內容最高點的畫面 y（作者螢幕可見到 1041）；腳離畫面底＝內容最低點的畫面 y（正常 ≈59＝畫布底）；
#      框外露出＝腰線以下、身體中心後側超出對話框外緣的畫面距離（正常，翅膀尾巴腳都會）；越過中線＝面向側越過畫面中央多少。
# 規格：readme/art_direction/CHARACTER_ART_SPEC.md §2.0。單一角色的詳細檢查與 sheet 用 check_bloodline_portraits.py。
import glob, os, numpy as np
from PIL import Image
BOXTOP = 24 + 828 * 1500 / 2246; HF, WF, HEADY, TOPLIM, BX = 235/1536, 800/1536, 975, 1072, 500
Hdisp = (HEADY - BOXTOP) / (WF - HF)
EMO = ['normal','happy','angry','cry','fear','hurt']
root = 'DipanProj_Main/Assets/GameAssets/Main/Characters/Talk'
print(f'顯示高 H={Hdisp:.1f}  對話框頂={BOXTOP:.1f}')
print(f"{'血統':34s} 版 頂(畫面) 腳離畫面底 框外露出(後側) 越過中線 外緣碰畫布")
for d in sorted(glob.glob(root + '/*/*/')):
    name = d[len(root)+1:-1]
    rows = []
    for e in EMO:
        f = d + e + '.png'
        if not os.path.exists(f): continue
        im = Image.open(f); W, Hc = im.size
        a = np.asarray(im.convert('RGBA'))[..., 3] > 20
        ys, xs = np.nonzero(a)
        k = Hdisp / Hc; wide = W > Hc
        cx = (700/1536 if wide else 470/1024) * W
        top = HEADY + HF * Hdisp
        ctop = top - ys.min() * k
        if ctop > TOPLIM: top -= ctop - TOPLIM; ctop = TOPLIM
        bottom = top - Hdisp                    # 畫布底邊在畫面的 y
        feet = top - (ys.max() + 1) * k         # 內容最低點
        # 腰線以下（畫面 y < BOXTOP）、身體中心後側（畫布 x < cx）離中心多遠
        below = a[int(max(0, (top - BOXTOP) / k)):, :]
        bx = np.nonzero(below.any(0))[0]
        back = (cx - bx.min()) * k if len(bx) else 0    # 畫面單位
        expose = max(0, back - 250)                       # 對話框外緣在身體中心後側 250 處
        front = (xs.max() - cx) * k                       # 面向側
        cross = max(0, front - BX)                        # 越過畫面中線
        edge = 'L' if xs.min() <= 2 else ''
        edge += 'R' if xs.max() >= W - 3 else ''
        rows.append((e, ctop, feet, expose, cross, edge))
    if not rows: continue
    ct = max(r[1] for r in rows); ft = max(r[2] for r in rows); ex = max(r[3] for r in rows); cr = max(r[4] for r in rows)
    eg = ''.join(sorted(set(''.join(r[5] for r in rows))))
    kind = '寬' if wide else '長'
    flag = []
    if ct > 1041: flag.append('頂>1041(看是不是只有角/蒸汽)')
    if ft > 100 and ex > 0: flag.append('腳懸空')
    print(f'{name:34s} {kind}  {ct:6.0f}   {ft:6.0f}      {ex:6.0f}         {cr:5.0f}     {eg:3s} {" ".join(flag)}')
