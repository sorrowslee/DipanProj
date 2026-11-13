# 用法: python3 tools/cutout_magenta_border.py <in> <out>
# 洋紅底去背「只去跟畫布邊相連的背景」：內部的紫／洋紅（發光紫眼、紫寶石）不會被當成背景挖掉。
# 2026-11-13 邪佛重繪：cutout_magenta_keep 把紫色發光眼睛一起去掉（紫光太接近 #FF00FF）。
import sys, numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage
im=Image.open(sys.argv[1]).convert('RGB'); a=np.asarray(im).astype(float)
d=np.sqrt((a[...,0]-255)**2+(a[...,1]-0)**2+(a[...,2]-255)**2)
cand=d<150                                   # 可能是背景（含邊緣混色）
lab,n=ndimage.label(cand)
border=set(np.unique(np.concatenate([lab[0],lab[-1],lab[:,0],lab[:,-1]])))-{0}
bgmask=np.isin(lab,list(border))
alpha=np.where(bgmask,np.clip((d-70)/80,0,1),1.0)
A=Image.fromarray((alpha*255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.6))
An=np.asarray(A).astype(float)/255
# 只在背景邊緣去洋紅溢色
near=ndimage.binary_dilation(bgmask,iterations=4)&(An>0)
m=np.clip(np.minimum(a[...,0],a[...,2])-a[...,1],0,None)*near
a[...,0]-=m; a[...,2]-=m
out=Image.fromarray(a.clip(0,255).astype(np.uint8)).convert('RGBA'); out.putalpha(A); out.save(sys.argv[2],optimize=True)
print(sys.argv[2], out.size, A.point(lambda v:255 if v>10 else 0).getbbox(), 'bg comps',len(border))
