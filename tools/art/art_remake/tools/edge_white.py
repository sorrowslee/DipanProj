# 用法: python3 tools/edge_white.py [--fix] <png>...  去背殘留的白／淺灰塊：低彩度、亮（>150）、而且碰到透明區（或離透明 ≤3px）的連通塊
import sys,numpy as np
from PIL import Image
from scipy import ndimage
fix='--fix' in sys.argv; fs=[f for f in sys.argv[1:] if f!='--fix']
for f in fs:
    im=Image.open(f).convert('RGBA'); a=np.asarray(im).astype(np.int32)
    rgb=a[...,:3]; al=a[...,3]; mx=rgb.max(-1); mn=rgb.min(-1)
    sat=(mx-mn)/np.maximum(mx,1)
    cand=(al>0)&(mx>150)&(sat<0.13)
    near=ndimage.binary_dilation(al<16,iterations=3)
    lab,n=ndimage.label(cand,structure=np.ones((3,3)))
    if n==0: continue
    touch=ndimage.maximum(near,lab,range(1,n+1)); sizes=ndimage.sum(cand,lab,range(1,n+1))
    bad=[i+1 for i in range(n) if touch[i] and sizes[i]<4000]
    tot=int(sum(sizes[i-1] for i in bad))
    if bad: print(f'{f}: {len(bad)} 塊 {tot}px')
    if fix and bad:
        kill=np.isin(lab,bad); kill=ndimage.binary_dilation(kill,iterations=1)&(al>0)&(mx>120)&(sat<0.2)
        b=np.asarray(im).copy(); b[kill]=0
        # 再清掉因此斷開的極小碎塊
        m=b[...,3]>8; l2,n2=ndimage.label(m,structure=np.ones((3,3)))
        if n2>1:
            s2=ndimage.sum(m,l2,range(1,n2+1)); keep=s2>=s2.max()*0.004
            b[~np.isin(l2,[i+1 for i in range(n2) if keep[i]])]=0
        Image.fromarray(b,'RGBA').save(f)
