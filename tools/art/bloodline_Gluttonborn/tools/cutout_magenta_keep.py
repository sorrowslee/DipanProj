# 用法: python3 tools/cutout_magenta_keep.py <in> <out>
# 洋紅底去背「保留紫色版」：cutout_magenta.py 會對全圖去洋紅溢色，紫色寶石/紫火紋/紫布會被洗成灰。
# 這版用「與純洋紅 #FF00FF 的色距」決定 alpha；去溢色做在邊緣，以及低彩度（中性色被染粉）的像素，高彩度紫色保留。
import sys, numpy as np
from PIL import Image, ImageFilter
im=Image.open(sys.argv[1]).convert('RGB'); a=np.asarray(im).astype(float)
d=np.sqrt((a[...,0]-255)**2+(a[...,1]-0)**2+(a[...,2]-255)**2)
alpha=np.clip((d-70)/80,0,1)
A=Image.fromarray((alpha*255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.6))
An=np.asarray(A).astype(float)/255
bg=Image.fromarray(((An<0.5)*255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7))
edge=(np.asarray(bg)>0)&(An>0)
r,g,b=a[...,0],a[...,1],a[...,2]
chroma=a.max(-1)-a.min(-1)
w=np.clip((90-chroma)/40,0,1)   # 低彩度（中性色上的洋紅溢色）全去，高彩度紫色保留
m=np.clip(np.minimum(r,b)-g,0,None)*np.maximum(edge,w)
a[...,0]-=m; a[...,2]-=m
out=Image.fromarray(a.clip(0,255).astype(np.uint8)).convert('RGBA'); out.putalpha(A); out.save(sys.argv[2],optimize=True)
print(sys.argv[2], out.size, A.point(lambda v:255 if v>10 else 0).getbbox())
