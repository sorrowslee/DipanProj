# 用法: python3 tools/cmp_std.py <out.jpg> <立繪>...  與核准立繪（餓鬼、神諭者、鬼差）並排，灰底＋標準線
import sys
from PIL import Image, ImageDraw
T='../../DipanProj_Main/Assets/GameAssets/Main/Characters/Talk/'
ps=[T+'Gluttonborn/Preta/normal.png',T+'Angelborn/Oracle/normal.png']+sys.argv[2:]
c=Image.new('RGB',(1024*len(ps),1536),(128,128,128))
for i,p in enumerate(ps):
    im=Image.open(p).convert('RGBA'); t=Image.new('RGBA',im.size,(128,128,128,255)); t.alpha_composite(im); c.paste(t.convert('RGB'),(i*1024,0))
d=ImageDraw.Draw(c)
for y,col in ((235,(0,255,255)),(800,(255,0,0))): d.line([(0,y),(c.width,y)],fill=col,width=4)
c.thumbnail((1800,900)); c.save(sys.argv[1],quality=86)
