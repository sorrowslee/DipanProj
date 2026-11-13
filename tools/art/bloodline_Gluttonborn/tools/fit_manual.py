# 用法: python3 tools/fit_manual.py <cut> <out> <頭頂y(來源)> <腳中點x(來源)> [腳底y(來源)]
# 手動指定來源圖的頭頂與腳中點（排除法杖、翅膀等高過頭頂的物件），對齊 CHARACTER_ART_SPEC：頭頂176、腳底864、中點512
import sys, numpy as np
from PIL import Image, ImageDraw
src,dst=sys.argv[1],sys.argv[2]; ht=float(sys.argv[3]); fx=float(sys.argv[4])
im=Image.open(src).convert('RGBA'); A=np.asarray(im)[...,3]>40
fy=float(sys.argv[5]) if len(sys.argv)>5 else np.nonzero(A)[0].max()+1
s=(864-176)/(fy-ht)
sm=im.resize((round(im.width*s),round(im.height*s)),Image.LANCZOS)
ox=round(512-fx*s); oy=round(864-fy*s)
out=Image.new('RGBA',(1024,1024),(0,0,0,0)); out.paste(sm,(ox,oy),sm); out.save(dst,optimize=True)
B=np.asarray(out)[...,3]>40; ys,xs=np.nonzero(B)
print(f'scale={s:.3f} 最高點y={ys.min()}(≥96) 腳底y={ys.max()} 左右x={xs.min()}~{xs.max()} 碰邊={xs.min()<=0 or xs.max()>=1023 or ys.min()<=0}')
chk=Image.new('RGBA',(1024,1024),(128,128,128,255)); chk.alpha_composite(out); d=ImageDraw.Draw(chk)
for y,c in ((96,(255,255,0)),(176,(0,255,255)),(780,(255,128,0)),(864,(255,0,0))): d.line([(0,y),(1023,y)],fill=c)
d.line([(512,0),(512,1023)],fill=(255,0,0)); chk.convert('RGB').save(dst.replace('.png','_check.png'))
