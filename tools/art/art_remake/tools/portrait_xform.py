# 用法: python3 tools/portrait_xform.py <in RGBA 1024x1536> <out> <scale> <anchor_x> <target_x>
# 以 (anchor_x, 畫布底) 為錨點等比縮放，再把 anchor_x 平移到 target_x；底邊對齊（超出底部的裁掉＝畫布底切過腳踝）。
# 同一角色所有表情用同一組參數，保證一致。
import sys
from PIL import Image
src,dst,s,ax,tx=sys.argv[1],sys.argv[2],float(sys.argv[3]),float(sys.argv[4]),float(sys.argv[5])
im=Image.open(src).convert('RGBA'); W,H=im.size
big=im.resize((round(W*s),round(H*s)),Image.LANCZOS)
ox=round(tx-ax*s); oy=H-big.height
out=Image.new('RGBA',(W,H),(0,0,0,0)); out.paste(big,(ox,oy),big)
out.save(dst,optimize=True)
