# 用法: python3 tools/fit_portrait.py <去背 RGBA> <輸出 png>
# CHARACTER_ART_SPEC §2：1024×1536、人物碰底、最高點≥40、右緣≤880、左緣≥64。
# 不縮放（GPT 2:3 原生就是 1024×1536）；只在沒碰底時往下平移。輸出 <out>_check.png（灰底＋對齊線）與報告。
import sys, numpy as np
from PIL import Image, ImageDraw
src,dst=sys.argv[1],sys.argv[2]
im=Image.open(src).convert('RGBA')
if im.size!=(1024,1536): im=im.resize((1024,1536),Image.LANCZOS)
A=np.asarray(im)[...,3]>40; ys,xs=np.nonzero(A)
dy=1535-ys.max()
if dy>0:
    o=Image.new('RGBA',im.size,(0,0,0,0)); o.alpha_composite(im,(0,dy)); im=o
im.save(dst,optimize=True)
A=np.asarray(im)[...,3]>40; ys,xs=np.nonzero(A)
# 最上方 40 列內容的中心 x（大致是頭）
print(f'平移={dy}  最高點y={ys.min()}(≥40, 頭頂線180)  底={ys.max()}(1535)  左右x={xs.min()}~{xs.max()}(≥64, ≤880)')
c=Image.new('RGBA',im.size,(128,128,128,255)); c.alpha_composite(im); d=ImageDraw.Draw(c)
for y,col in ((40,(255,255,0)),(180,(0,255,255)),(330,(255,0,255)),(480,(255,128,0)),(1200,(255,0,0))): d.line([(0,y),(1023,y)],fill=col,width=2)
for x in (64,470,880): d.line([(x,0),(x,1535)],fill=(0,255,0),width=2)
c.convert('RGB').save(dst.replace('.png','_check.png'))
