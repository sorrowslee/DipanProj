#!/usr/bin/env python3
"""主角對話立繪「照規格重排」（CHARACTER_ART_SPEC §2.0，2026-11-25 窄版／寬版統一）。

輸入：同一角色六張表情的去背圖（任意尺寸，RGBA），檔名 <前綴>_<情緒>.png 或 <情緒>.png。
輸出：規格畫布（長版 1024×1536／寬版 1536×1024）的六張 <情緒>.png。

規則（跟 TalkPanel／BloodlineIntroPanel 的排法對應，見 CHARACTER_ART_SPEC §2.0）：
  ① 倍率：六張共用一個倍率，由 normal 決定——normal 的**最高點**對到頭頂線（長 235／寬 157，可用 --top 改）、
     **最低點**對到畫布底邊（長 1535／寬 1023）。
  ② 垂直：每一張的**最低點都貼畫布底**（人站在地上；蹲、縮的表情頭會比較低，這是對的）。
  ③ 水平：每一張「下半身（內容最下面 25%）」的中心對到身體中心線（長 470／寬 700，可用 --cx 改）。
  ④ 不裁切、不淡出任何部分（框外露出的翅膀、尾巴、腳都是設計，見 §2.0「框外」）。
用法（repo 根目錄）:
  python3 tools/art/art_remake/tools/fit_talk_portrait.py <來源資料夾> <輸出資料夾> --wide|--tall [--prefix 角色_] [--top 157] [--cx 700]
做完一定要跑 check_bloodline_portraits.py（TALK= 指到輸出資料夾的上兩層）看 sheet。
"""
import argparse, os
import numpy as np
from PIL import Image

EMO = ['normal', 'happy', 'angry', 'cry', 'fear', 'hurt']


def bbox(im, thr=40):
    a = np.asarray(im)[..., 3] > thr
    ys, xs = np.nonzero(a)
    return a, xs.min(), ys.min(), xs.max(), ys.max()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('src'); ap.add_argument('dst')
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument('--wide', action='store_true'); g.add_argument('--tall', action='store_true')
    ap.add_argument('--prefix', default='')
    ap.add_argument('--top', type=float, default=None, help='normal 最高點要落在的 y（預設＝頭頂線 235／157）')
    ap.add_argument('--cx', type=float, default=None, help='下半身中心要落在的 x（預設＝身體中心線 470／700）')
    a = ap.parse_args()
    W, H = (1536, 1024) if a.wide else (1024, 1536)
    top = a.top if a.top is not None else (157 if a.wide else 235)
    cx = a.cx if a.cx is not None else (700 if a.wide else 470)
    os.makedirs(a.dst, exist_ok=True)

    def load(e):
        return Image.open(os.path.join(a.src, f'{a.prefix}{e}.png')).convert('RGBA')

    _, _, t0, _, b0 = bbox(load('normal'))
    K = (H - top) / (b0 + 1 - t0)
    print(f'倍率 K={K:.4f}（normal 內容高 {b0 + 1 - t0}px → {H - top:.0f}px）')
    for e in EMO:
        p = os.path.join(a.src, f'{a.prefix}{e}.png')
        if not os.path.exists(p):
            print('  缺', e); continue
        im = load(e)
        sm = im.resize((max(1, round(im.width * K)), max(1, round(im.height * K))), Image.LANCZOS)
        al, x0, y0, x1, y1 = bbox(sm)
        leg = al[int(y1 - (y1 - y0) * 0.25):y1 + 1]
        lx = np.nonzero(leg.any(0))[0]
        fx = (lx.min() + lx.max()) / 2
        ox, oy = round(cx - fx), H - 1 - y1
        out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        out.paste(sm, (ox, oy), sm)
        out.save(os.path.join(a.dst, f'{e}.png'))
        bb = out.getchannel('A').point(lambda v: 255 if v > 40 else 0).getbbox()
        cut = []
        if ox + x0 < 0: cut.append(f'左緣被畫布切 {-(ox + x0)}px')
        if ox + x1 >= W: cut.append(f'右緣被畫布切 {ox + x1 - W + 1}px')
        if oy + y0 < 0: cut.append(f'頂端被畫布切 {-(oy + y0)}px')
        print(f'  {e}: 內容框 {bb} {"⚠ " + "、".join(cut) if cut else ""}')


if __name__ == '__main__':
    main()
