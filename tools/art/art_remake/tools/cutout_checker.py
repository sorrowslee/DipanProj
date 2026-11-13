# 用法: python3 tools/cutout_checker.py <in> <out>   GPT 畫出「假透明棋盤格」底：與四邊相連、低彩度(<14)且亮度>185 的像素當背景
import sys, numpy as np
from scipy import ndimage
from PIL import Image, ImageFilter
im=Image.open(sys.argv[1]).convert('RGB'); a=np.asarray(im).astype(int)
sat=a.max(2)-a.min(2); val=a.max(2)
cand=(sat<14)&(val>185)
lab,n=ndimage.label(cand)
edge=set(np.unique(np.concatenate([lab[0],lab[-1],lab[:,0],lab[:,-1]])))-{0}
bg=np.isin(lab,list(edge))
# 被角色包住的白底洞（斗篷破口）：近純白(>236)、低彩度、面積≥60 的連通塊也當背景（眼睛高光很小，不會被吃）
hole=(sat<12)&(val>236)&~bg
hl,hn=ndimage.label(hole)
if hn:
    sz=ndimage.sum(hole,hl,range(1,hn+1)); big=np.nonzero(sz>=25)[0]+1
    bg|=np.isin(hl,big)
al=Image.fromarray(np.where(bg,0,255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
out=im.convert('RGBA'); out.putalpha(al); out.save(sys.argv[2]); print('bg%',round(bg.mean()*100,1))
