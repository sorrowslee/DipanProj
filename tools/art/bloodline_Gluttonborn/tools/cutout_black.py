# 用法: python3 tools/cutout_black.py <in> <out> [T=14]
# 純黑底去背：從四邊 flood fill「max(rgb)<=T」的像素當背景；邊緣 2px 帶依亮度給半透明（不吃角色內部暗部，因為不與四邊相連）
import sys, numpy as np
from collections import deque
from PIL import Image, ImageFilter
src,dst=sys.argv[1],sys.argv[2]; T=int(sys.argv[3]) if len(sys.argv)>3 else 14
im=Image.open(src).convert('RGB'); a=np.asarray(im).astype(int); H,W,_=a.shape
mx=a.max(2); cand=mx<=T; bg=np.zeros((H,W),bool); q=deque()
for x in range(W):
    for y in (0,H-1):
        if cand[y,x] and not bg[y,x]: bg[y,x]=True; q.append((y,x))
for y in range(H):
    for x in (0,W-1):
        if cand[y,x] and not bg[y,x]: bg[y,x]=True; q.append((y,x))
while q:
    y,x=q.popleft()
    for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
        ny,nx=y+dy,x+dx
        if 0<=ny<H and 0<=nx<W and cand[ny,nx] and not bg[ny,nx]: bg[ny,nx]=True; q.append((ny,nx))
alpha=np.where(bg,0,255).astype(float)
# 邊緣帶：背景外 2px 內，亮度低者給部分透明
band=np.asarray(Image.fromarray((bg*255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)))>0
band&=~bg
soft=np.clip((mx-T)/(40-T),0,1)*255
alpha=np.where(band,np.minimum(alpha,soft),alpha)
out=np.dstack([a,alpha]).astype(np.uint8)
Image.fromarray(out,'RGBA').save(dst); print(dst,'bg%',round(bg.mean()*100,1))
