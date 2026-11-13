# 用法: python3 tools/place_by_lines.py <去背 RGBA> <輸出> <頭頂y> <腰y> <頭中心x>
# 以「頭頂→235、腰→800、頭中心→470」放進 1024×1536（轉換面板與對話框都用這兩條線，腰以下不顯示，腳可以出畫布）
import sys, numpy as np
from PIL import Image
src,dst=sys.argv[1],sys.argv[2]; top,waist,hx=map(float,sys.argv[3:6])
im=Image.open(src).convert('RGBA'); W,H=im.size
k=(800-235)/(waist-top)
a=np.asarray(im).astype(np.float32); al=a[...,3:4]/255
pre=Image.fromarray(np.concatenate([a[...,:3]*al,a[...,3:4]],-1).clip(0,255).astype(np.uint8),'RGBA')
nw,nh=round(W*k),round(H*k); sm=np.asarray(pre.resize((nw,nh),Image.LANCZOS)).astype(np.float32)
ox=round(470-hx*k); oy=round(235-top*k)
out=np.zeros((1536,1024,4),np.float32)
x0,y0=max(0,ox),max(0,oy); x1,y1=min(1024,ox+nw),min(1536,oy+nh)
out[y0:y1,x0:x1]=sm[y0-oy:y1-oy,x0-ox:x1-ox]
A=out[...,3:4]; rgb=np.where(A>0,out[...,:3]/np.maximum(A,1e-6)*255,0)
Image.fromarray(np.concatenate([rgb.clip(0,255),A],-1).astype(np.uint8),'RGBA').save(dst)
print(f'scale={k:.3f} offset=({ox},{oy})')
