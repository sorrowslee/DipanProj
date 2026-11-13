# 用法: python3 tools/sheet6.py <key>  → work/sheet_<key>.jpg（六表情並排，灰底＋235/800 線）
import sys
from PIL import Image, ImageDraw
k=sys.argv[1]; em=['normal','happy','cry','hurt','angry','fear']
c=Image.new('RGB',(1024*6,1536),(128,128,128))
for i,e in enumerate(em):
    im=Image.open(f'portrait/{k}_{e}.png').convert('RGBA'); t=Image.new('RGBA',im.size,(128,128,128,255)); t.alpha_composite(im); c.paste(t.convert('RGB'),(i*1024,0))
d=ImageDraw.Draw(c)
for y,col in ((235,(0,255,255)),(800,(255,0,0))): d.line([(0,y),(c.width,y)],fill=col,width=4)
for i in range(6): d.line([(i*1024+880,0),(i*1024+880,1536)],fill=(0,255,0),width=3)
c.thumbnail((2400,700)); c.save(f'work/sheet_{k}.jpg',quality=86)
