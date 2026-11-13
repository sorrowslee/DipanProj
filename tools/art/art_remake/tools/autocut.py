# 用法: python3 tools/autocut.py <in> <out>   依四角底色自動選去背：黑底→cutout_black+holes_black、洋紅→cutout_magenta_keep+defringe、灰→cutout
import sys, os, subprocess, numpy as np
from PIL import Image
src,dst=sys.argv[1],sys.argv[2]
a=np.asarray(Image.open(src).convert('RGB')).astype(int)
corner=np.concatenate([a[:8,:8].reshape(-1,3),a[:8,-8:].reshape(-1,3),a[-8:,:8].reshape(-1,3),a[-8:,-8:].reshape(-1,3)])
r,g,b=np.median(corner,0)
T='tools/'
def run(*args): subprocess.run(['python3']+list(args),check=True,stdout=subprocess.DEVNULL)
if max(r,g,b)<40:
    run(T+'cutout_black.py',src,dst,'4')
    if not os.environ.get('NOHOLES'): run(T+'holes_black.py',dst,dst,'4')
    kind='black'
elif r>180 and b>180 and g<90: run(T+'cutout_magenta_keep.py',src,dst); run(T+'defringe.py',dst,dst); kind='magenta'
elif min(r,g,b)>200: run(T+'cutout_checker.py',src,dst); kind='light/checker'
else: run(T+'cutout.py',src,dst); kind='grey'
print('bg',kind,(r,g,b))
