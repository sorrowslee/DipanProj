# 用法: python3 tools/fit_portrait_wide.py <去背 RGBA> <輸出 png>
# CHARACTER_ART_SPEC §2.1 寬體型：1536×1024、人物碰底、最高點≥27、頭頂線120、眼線220、中心x700、左≥64 右≤1440。不縮放，只在沒碰底時下移。
import sys, numpy as np
from PIL import Image, ImageDraw
src,dst=sys.argv[1],sys.argv[2]
im=Image.open(src).convert('RGBA')
if im.size!=(1536,1024): im=im.resize((1536,1024),Image.LANCZOS)
A=np.asarray(im)[...,3]>40; ys,xs=np.nonzero(A); dy=1023-ys.max()
if dy>0:
    o=Image.new('RGBA',im.size,(0,0,0,0)); o.alpha_composite(im,(0,dy)); im=o
im.save(dst,optimize=True)
A=np.asarray(im)[...,3]>40; ys,xs=np.nonzero(A)
print(f'平移={dy}  最高點y={ys.min()}(≥27)  底={ys.max()}(1023)  左右x={xs.min()}~{xs.max()}(≥64, ≤1440)')
c=Image.new('RGBA',im.size,(128,128,128,255)); c.alpha_composite(im); d=ImageDraw.Draw(c)
for y,col in ((27,(255,255,0)),(120,(0,255,255)),(220,(255,0,255))): d.line([(0,y),(1535,y)],fill=col,width=2)
for x in (64,700,1440): d.line([(x,0),(x,1023)],fill=(0,255,0),width=2)
c.convert('RGB').save(dst.replace('.png','_check.png'))
