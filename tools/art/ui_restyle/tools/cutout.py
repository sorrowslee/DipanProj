# 用法: python3 tools/cutout.py <in.png> <out.png> [dist=24] [sat=20]  純灰底圖 → 透明（只清與四邊相連的灰）
import sys
import numpy as np
from collections import deque
from PIL import Image, ImageFilter
src,dst=sys.argv[1],sys.argv[2]; D=float(sys.argv[3]) if len(sys.argv)>3 else 24; S=float(sys.argv[4]) if len(sys.argv)>4 else 20
im=Image.open(src).convert('RGB'); a=np.asarray(im).astype(int); h,w,_=a.shape
edge=np.concatenate([a[:3].reshape(-1,3),a[:,:3].reshape(-1,3),a[:,-3:].reshape(-1,3)])
bg=np.median(edge,0); dist=np.sqrt(((a-bg)**2).sum(2)); sat=a.max(2)-a.min(2); isbg=(dist<D)&(sat<S)
vis=np.zeros((h,w),bool); q=deque()
for x in range(w):
    for y in (0,h-1):
        if isbg[y,x] and not vis[y,x]: vis[y,x]=1; q.append((y,x))
for y in range(h):
    for x in (0,w-1):
        if isbg[y,x] and not vis[y,x]: vis[y,x]=1; q.append((y,x))
while q:
    y,x=q.popleft()
    for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
        ny,nx=y+dy,x+dx
        if 0<=ny<h and 0<=nx<w and not vis[ny,nx] and isbg[ny,nx]: vis[ny,nx]=1; q.append((ny,nx))
hole=(dist<14)&(sat<10)   # 封閉的灰色空隙（腋下、腿間）：與底色幾乎相同才挖
al=Image.fromarray(np.where(vis|hole,0,255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
out=im.convert('RGBA'); out.putalpha(al); out.save(dst,optimize=True)
print(dst, out.size, 'bbox', al.point(lambda v:255 if v>10 else 0).getbbox())
