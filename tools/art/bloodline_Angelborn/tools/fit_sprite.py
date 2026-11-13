# 用法: python3 tools/fit_sprite.py <去背後 RGBA> <輸出 1024 png> [--top Y]
# 依 CHARACTER_ART_SPEC §3.2 把角色對齊到 1024×1024：腳底 y=864、兩腳中點 x=512。
# 縮放：最高點→腳底 = 864-top（預設 top=176＝無頭飾；有帽冠角時傳較小的 top，例 120）。
# 同時輸出 <輸出>_check.png（灰底＋對齊線）與量測報告。
import sys, numpy as np
from PIL import Image, ImageDraw
src,dst=sys.argv[1],sys.argv[2]
top=176
if '--top' in sys.argv: top=int(sys.argv[sys.argv.index('--top')+1])
im=Image.open(src).convert('RGBA'); A=np.asarray(im)[...,3]>40
ys,xs=np.nonzero(A); y0,y1=ys.min(),ys.max()
def feet_mid(mask,y1,h):
    band=mask[max(0,int(y1-h*0.08)):y1+1]; col=band.any(0); segs=[];x=0;W=len(col)
    while x<W:
        if col[x]:
            s=x
            while x<W and col[x]: x+=1
            if x-s>=3: segs.append((s,x-1))
        else: x+=1
    # 每段的最低點
    low=[]
    for s,e in segs:
        r=np.nonzero(band[:,s:e+1].any(1))[0]; low.append((r.max(),s,e))
    low.sort(reverse=True)
    if len(low)>=2: (_,a1,b1),(_,a2,b2)=low[:2]; return ((a1+b1)/2+(a2+b2)/2)/2,len(segs)
    if low: return (low[0][1]+low[0][2])/2,len(segs)
    return xs.mean(),0
fx,nseg=feet_mid(A,y1,y1-y0)
s=(864-top)/(y1-y0+1)
nw,nh=round(im.width*s),round(im.height*s)
sm=im.resize((nw,nh),Image.LANCZOS)
ox=round(512-fx*s); oy=round(864-(y1+1)*s)
out=Image.new('RGBA',(1024,1024),(0,0,0,0)); out.alpha_composite(sm,(ox,oy)) if ox>=0 and oy>=0 else out.paste(sm,(ox,oy),sm)
out.save(dst,optimize=True)
B=np.asarray(out)[...,3]>40; ys,xs=np.nonzero(B)
fx2,nseg2=feet_mid(B,ys.max(),ys.max()-ys.min())
print(f'scale={s:.3f}  最高點y={ys.min()}(限≥96)  腳底y={ys.max()}(864)  腳中點x={fx2:.0f}(512±6)  左右x={xs.min()}~{xs.max()}(主體160~864,長物96~928)  底帶腳段數={nseg2}  碰邊={xs.min()<=0 or xs.max()>=1023 or ys.min()<=0}')
# 底帶 760~864 以外不該有東西往下垂到 780 以下：回報 780~864 的段寬
chk=Image.new('RGBA',(1024,1024),(128,128,128,255)); chk.alpha_composite(out); d=ImageDraw.Draw(chk)
for y,c in ((96,(255,255,0)),(176,(0,255,255)),(780,(255,128,0)),(864,(255,0,0))): d.line([(0,y),(1023,y)],fill=c)
for x in (160,512,864): d.line([(x,0),(x,1023)],fill=(0,255,0))
chk.convert('RGB').save(dst.replace('.png','_check.png'))
