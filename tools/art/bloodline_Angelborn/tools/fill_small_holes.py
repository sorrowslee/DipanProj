# 用法: python3 tools/fill_small_holes.py <去背後 RGBA> <原始 GPT 圖> <輸出> [寬度門檻px=21]
# 洋紅色系的寶石/紫火被色距去背誤判成背景、在身上挖出小洞：把「被身體包住、而且很窄（侵蝕 門檻px 就消失）」的透明區補回原圖顏色。
# 大的封閉空隙（手臂與身體間、翅膀之間）侵蝕後還在，會被保留成透明。
import sys, numpy as np
from PIL import Image, ImageDraw, ImageFilter
cut=Image.open(sys.argv[1]).convert('RGBA'); src=Image.open(sys.argv[2]).convert('RGB')
k=int(sys.argv[4]) if len(sys.argv)>4 and not sys.argv[4].startswith('--') else 21
A=np.asarray(cut)[...,3]; m=Image.fromarray(((A<128)*255).astype(np.uint8))
W,H=m.size
for x,y in [(0,0),(W-1,0),(0,H-1),(W-1,H-1)]: ImageDraw.floodfill(m,(x,y),128)
inner=Image.fromarray(((np.array(m)==255)*255).astype(np.uint8))
big=inner.filter(ImageFilter.MinFilter(k)).filter(ImageFilter.MaxFilter(k+4))
small=(np.array(inner)>0)&~(np.array(big)>0)
Sf=np.asarray(src).astype(float)
bgd=np.sqrt((Sf[...,0]-255)**2+Sf[...,1]**2+(Sf[...,2]-255)**2)
small&=bgd>35   # 原圖就是洋紅背景的（羽毛間隙）不補
# 可選 --rect x0,y0,x1,y1：只在這個框內補（框住盔甲軀幹、避開翅膀邊緣），框內不看顏色
if '--rect' in sys.argv:
    x0,y0,x1,y1=map(int,sys.argv[sys.argv.index('--rect')+1].split(','))
    R=np.zeros_like(small); R[y0:y1,x0:x1]=True
    small=(np.array(inner)>0)&~(np.array(big)>0)&R&(bgd>60)   # 框內也排除原圖就是洋紅背景的空隙
else:
    small&=(Sf[...,2]-Sf[...,0])>25   # 只補偏藍的紫（寶石/紫火）；r≈b 的洋紅混色是邊緣溢色，不補
out=np.array(cut); S=np.asarray(src); out[small,:3]=S[small]; out[small,3]=255
Image.fromarray(out).save(sys.argv[3],optimize=True); print('filled px',int(small.sum()))
