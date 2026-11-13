# 把切角牌的內面換成背包選中頁籤的深緋紅漆面（保留原紋理明暗）。用法: redface.py in.png out.png [darken]
import sys, numpy as np
from collections import deque
from PIL import Image, ImageFilter
import os
UI=os.path.expanduser('~/mnt/DipanProj/DipanProj_Main/Assets/Resources/UI/')
src,dst=sys.argv[1],sys.argv[2]; dk=float(sys.argv[3]) if len(sys.argv)>3 else 1.0
im=Image.open(src).convert('RGBA'); a=np.asarray(im).astype(float); H,W,_=a.shape
L=a[...,:3].mean(2); al=a[...,3]
# 參考：背包選中頁籤面色
ref=np.asarray(Image.open(UI+'InventoryPanel/inventoryPanel_Cell_Selected.png').convert('RGBA')).astype(float)
rh,rw,_=ref.shape; rc=ref[rh*2//5:rh*3//5, rw*2//5:rw*3//5,:3].reshape(-1,3); refc=rc.mean(0)
# 內面：從中心 flood，亮度低於框線
cand=(al>250)&(L<55)
vis=np.zeros((H,W),bool); q=deque([(H//2,W//2)]); vis[H//2,W//2]=True
while q:
    y,x=q.popleft()
    for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
        ny,nx=y+dy,x+dx
        if 0<=ny<H and 0<=nx<W and not vis[ny,nx] and cand[ny,nx]: vis[ny,nx]=True; q.append((ny,nx))
m=np.asarray(Image.fromarray((vis*255).astype(np.uint8)).filter(ImageFilter.MinFilter(5)).filter(ImageFilter.GaussianBlur(1.0))).astype(float)/255
fl=L[vis].mean()
tex=(L/fl)[...,None]
red=np.clip(refc*tex*dk,0,255)
out=a.copy(); out[...,:3]=a[...,:3]*(1-m[...,None])+red*m[...,None]
Image.fromarray(out.astype(np.uint8),'RGBA').save(dst)
print('face px',vis.sum(),'ref colour',refc.round(), 'face mean lum',round(fl,1))
