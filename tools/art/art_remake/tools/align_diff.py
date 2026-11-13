# 用法: python3 tools/align_diff.py <normal RGBA 1024x1536> <diff RGBA> <out>
# 把表情差分的大小/位置對齊 normal：在 1/8 解析度搜尋 縮放(0.8~1.25)×平移，讓上 65%（頭到腰）的不透明遮罩 IoU 最大；
# 以畫布底中心為錨縮放。輸出後底部若沒碰底就往下貼底（人物一定要碰底）。
import sys, numpy as np
from PIL import Image
N=Image.open(sys.argv[1]).convert('RGBA'); D=Image.open(sys.argv[2]).convert('RGBA')
if D.size!=N.size: D=D.resize(N.size,Image.LANCZOS)
W,H=N.size; f=8; w,h=W//f,H//f
nm=np.asarray(N.resize((w,h)))[...,3]>60
top=np.nonzero(nm.any(1))[0].min(); lim=int(top+(h-top)*0.65)
dm_img=D.resize((w,h))
best=(-1,1,0,0)
for s in np.arange(0.80,1.251,0.01):
    sw,sh=round(w*s),round(h*s); m=np.asarray(dm_img.resize((sw,sh)))[...,3]>60
    for dx in range(-12,13,2):
        for dy in range(-12,13,2):
            ox=round(w/2-sw/2)+dx; oy=h-sh+dy
            c=np.zeros((h,w),bool)
            x0,y0=max(ox,0),max(oy,0); x1,y1=min(ox+sw,w),min(oy+sh,h)
            if x1<=x0 or y1<=y0: continue
            c[y0:y1,x0:x1]=m[y0-oy:y1-oy,x0-ox:x1-ox]
            a=nm[:lim]; b=c[:lim]; iou=(a&b).sum()/max((a|b).sum(),1)
            if iou>best[0]: best=(iou,s,dx,dy)
iou,s,dx,dy=best
# 恆等（不縮放不平移）的 IoU：GPT 多半保持原尺寸，只是姿勢差很多（張手、彎腰）時搜尋會誤判成縮放 ⇒ 差不多就用恆等
m=np.asarray(dm_img)[...,3]>60; a=nm[:lim]; b=m[:lim]; id_iou=(a&b).sum()/max((a|b).sum(),1)
if best[0]-id_iou<0.06: iou,s,dx,dy=id_iou,1.0,0,0
sw,sh=round(W*s),round(H*s); big=D.resize((sw,sh),Image.LANCZOS)
out=Image.new('RGBA',(W,H),(0,0,0,0)); out.paste(big,(round(W/2-sw/2)+dx*f,H-sh+dy*f),big)
A=np.asarray(out)[...,3]>40; ys=np.nonzero(A.any(1))[0]
if ys.max()<H-1:
    o=Image.new('RGBA',(W,H),(0,0,0,0)); o.alpha_composite(out,(0,H-1-ys.max())); out=o
out.save(sys.argv[3],optimize=True)
A=np.asarray(out)[...,3]>40; ys,xs=np.nonzero(A)
print(f'IoU={iou:.3f} scale={s:.2f} dx={dx*f} dy={dy*f}  top={ys.min()} right={xs.max()}(≤880)')
