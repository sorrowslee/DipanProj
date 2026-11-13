# 用法: python3 tools/fit_portrait_std.py <去背 RGBA> <輸出> [頭頂y 實測值]
# 2026-11-13 立繪標準（以核准的新血統成品為準）：1024×1536、全身含腳、腳碰底、頭頂≈235、身體中心≈470、右緣≤880。
# 沒給頭頂 → 只把內容貼底（不縮放）；給了頭頂 → 以畫布底為錨等比縮放，讓頭頂落在 235，水平把內容中心移到 470。
import sys, numpy as np
from PIL import Image, ImageDraw
src,dst=sys.argv[1],sys.argv[2]
im=Image.open(src).convert('RGBA')
if im.size!=(1024,1536): im=im.resize((1024,1536),Image.LANCZOS)
A=np.asarray(im)[...,3]>40; ys,xs=np.nonzero(A)
dy=1535-ys.max()
if dy>0: o=Image.new('RGBA',im.size,(0,0,0,0)); o.alpha_composite(im,(0,dy)); im=o
if len(sys.argv)>3:
    top=float(sys.argv[3])+max(dy,0); s=(1536-235)/(1536-top)
    big=im.resize((round(1024*s),round(1536*s)),Image.LANCZOS)
    A=np.asarray(big)[...,3]>40; ys,xs=np.nonzero(A)
    # 水平中心：取上半身（頭到腰）內容的中位數
    cx=np.median(xs[ys<ys.min()+(ys.max()-ys.min())*0.45])
    o=Image.new('RGBA',(1024,1536),(0,0,0,0)); o.paste(big,(round(470-cx),1536-big.height),big); im=o
    print(f'scale={s:.3f} cx={cx:.0f}')
im.save(dst,optimize=True)
A=np.asarray(im)[...,3]>40; ys,xs=np.nonzero(A)
print(f'最高點y={ys.min()}  底={ys.max()}  左右x={xs.min()}~{xs.max()}(≥64, ≤880)')
c=Image.new('RGBA',im.size,(128,128,128,255)); c.alpha_composite(im); d=ImageDraw.Draw(c)
for y,col in ((40,(255,255,0)),(235,(0,255,255)),(800,(255,0,0)),(1200,(255,128,0))): d.line([(0,y),(1023,y)],fill=col,width=3)
for x in (64,470,880): d.line([(x,0),(x,1535)],fill=(0,255,0),width=3)
c.convert('RGB').save(dst.replace('.png','_check.png'))
