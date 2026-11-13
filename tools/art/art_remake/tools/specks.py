# 用法: python3 tools/specks.py [--fix] <png>...   找「沒連到主體」的小碎塊（去背殘渣）；--fix 就地清掉
import sys,numpy as np
from PIL import Image
from scipy import ndimage
fix='--fix' in sys.argv; fs=[f for f in sys.argv[1:] if f!='--fix']
for f in fs:
    im=Image.open(f).convert('RGBA'); a=np.asarray(im).copy(); m=a[...,3]>8
    lab,n=ndimage.label(m,structure=np.ones((3,3)))
    if n==0: continue
    sizes=ndimage.sum(m,lab,range(1,n+1)); main=sizes.max()
    bad=[i+1 for i,s in enumerate(sizes) if s<main*0.004]
    tot=int(sum(sizes[i-1] for i in bad))
    if bad:
        print(f'{f}: {len(bad)} 塊 共 {tot}px（主體 {int(main)}）')
        if fix:
            kill=np.isin(lab,bad); a[kill]=0; Image.fromarray(a,'RGBA').save(f)
