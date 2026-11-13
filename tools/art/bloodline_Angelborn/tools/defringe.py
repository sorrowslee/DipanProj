# 用法: python3 tools/defringe.py <RGBA in> <out> [寬度px=4]
# 去掉洋紅背景殘留的紫色描邊：只看「離透明背景 寬度px 以內」的像素，偏洋紅（r、b 都明顯高於 g、且 r≈b）者
# 半透明就直接透明，不透明就把洋紅成分扣掉（減到 g 的水準）。內部的紫寶石、紫火紋不在邊緣帶，不受影響。
import sys, numpy as np
from PIL import Image, ImageFilter
im=Image.open(sys.argv[1]).convert('RGBA'); w=int(sys.argv[3]) if len(sys.argv)>3 else 4
a=np.asarray(im).astype(float); A=a[...,3]
bg=Image.fromarray(((A<20)*255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2*w+1))
edge=(np.asarray(bg)>0)&(A>=20)
r,g,b=a[...,0],a[...,1],a[...,2]
mag=np.minimum(r,b)-g
fr=edge&(mag>25)&(np.abs(r-b)<70)
semi=fr&(A<200)
a[semi,3]=0
hard=fr&~semi
m=np.where(hard,mag,0); a[...,0]-=m; a[...,2]-=m
Image.fromarray(a.clip(0,255).astype(np.uint8)).save(sys.argv[2],optimize=True)
print('fringe px',int(fr.sum()),'cleared',int(semi.sum()))
