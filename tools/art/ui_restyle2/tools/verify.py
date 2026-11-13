# 用法: python3 tools/verify.py <key> x0,y0,x1,y1 [...]   → 檢查新圖關鍵內容區：是否不透明、是否平整深色；並輸出標註圖
import sys, json, os
import numpy as np
from PIL import Image, ImageDraw
D=os.path.dirname(os.path.abspath(__file__)); WORK=D+'/../work'
UI=os.path.expanduser('~/mnt/DipanProj/DipanProj_Main/Assets/Resources/UI/')
key=sys.argv[1]; rects=[tuple(map(int,r.split(','))) for r in sys.argv[2:]]
m=json.load(open(f'{WORK}/{key}.json'))
new=Image.open(f'{WORK}/{key}_new.png').convert('RGBA'); old=Image.open(UI+m['rel']).convert('RGBA')
def stat(im,r):
    a=np.asarray(im.crop(r)).astype(float); L=a[...,:3].mean(2); al=a[...,3]
    return L.mean(), L.std(), (al>250).mean()
ok=True
for r in rects:
    ln,sn,on=stat(new,r); lo,so,oo=stat(old,r)
    good = on>0.99 and ln<70 and sn<22
    ok&=good
    print(f'{r}: NEW lum={ln:.0f} std={sn:.1f} opaque={on:.2f} | OLD lum={lo:.0f} std={so:.1f} {"OK" if good else "CHECK"}')
S=Image.new('RGBA',(new.width*2+20,new.height),(60,60,60,255))
for i,im in enumerate((old,new)):
    S.alpha_composite(im,(i*(new.width+20),0)); d=ImageDraw.Draw(S)
    for r in rects: d.rectangle([r[0]+i*(new.width+20),r[1],r[2]+i*(new.width+20),r[3]],outline=(255,40,40,255),width=4)
k=min(1.0,1600/S.width); S=S.resize((int(S.width*k),int(S.height*k)),Image.LANCZOS)
S.convert('RGB').save(f'{WORK}/{key}_verify.png'); print('ALL OK' if ok else 'NEEDS LOOK', f'{WORK}/{key}_verify.png')
