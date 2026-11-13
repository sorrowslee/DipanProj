# 用法: python3 tools/cutout_magenta.py <in> <out>   洋紅底（#FF00FF）去背＋全圖去洋紅溢色（灰毛角色用）
import sys, numpy as np
from PIL import Image, ImageFilter
im=Image.open(sys.argv[1]).convert('RGB'); a=np.asarray(im).astype(float)
r,g,b=a[...,0],a[...,1],a[...,2]
mag=np.minimum(r,b)-g
alpha=np.clip(1-(mag-30)/90,0,1)
m=np.clip(mag,0,None)            # 去溢色：同時偏紅又偏藍的量（純紅、純藍不受影響）
a[...,0]-=m; a[...,2]-=m
A=Image.fromarray((alpha*255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.6))
out=Image.fromarray(a.clip(0,255).astype(np.uint8)).convert('RGBA'); out.putalpha(A); out.save(sys.argv[2],optimize=True)
print(sys.argv[2], out.size, A.point(lambda v:255 if v>10 else 0).getbbox())
