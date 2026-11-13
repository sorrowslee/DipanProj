# 用法: python3 tools/tall2wide.py <去背 1024x1536 RGBA> <輸出 1536x1024> <頭頂y(長版座標)>
# 太寬的角色改寬版：先照長版標準（頭頂→235，腳貼底）算縮放，再 ×2/3 放進 1536×1024，內容框中心 → x700。
import sys, numpy as np
from PIL import Image
im=Image.open(sys.argv[1]).convert('RGBA')
A=np.asarray(im)[...,3]>40; ys,xs=np.nonzero(A); bot=ys.max()
top=float(sys.argv[3]); s=(1301/(bot-top))*(1024/1536)
big=im.resize((round(im.width*s),round(im.height*s)),Image.LANCZOS)
A=np.asarray(big)[...,3]>40; ys,xs=np.nonzero(A)
cx=(xs.min()+xs.max())/2
out=Image.new('RGBA',(1536,1024),(0,0,0,0))
out.alpha_composite(big,(int(round(700-cx)),int(1023-ys.max())))
out.save(sys.argv[2])
A=np.asarray(out)[...,3]>40; ys,xs=np.nonzero(A)
print('scale=%.3f top=%d bot=%d x=%d~%d'%(s,ys.min(),ys.max(),xs.min(),xs.max()))
