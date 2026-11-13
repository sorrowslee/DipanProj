# 用法: python3 tools/topgrid.py <out.jpg> <立繪>...  每張取上方 0~600、畫 50px 格線（看頭頂實際位置用）
import sys
from PIL import Image, ImageDraw
ps=sys.argv[2:]; c=Image.new('RGB',(600*len(ps),360),(0,0,0))
for i,p in enumerate(ps):
    im=Image.open(p).convert('RGBA').crop((112,0,912,600)); t=Image.new('RGBA',im.size,(128,128,128,255)); t.alpha_composite(im); d=ImageDraw.Draw(t)
    for y in range(0,600,50): d.line([(0,y),(800,y)],fill=(255,255,0) if y%100==0 else (0,220,255),width=2); d.text((4,y+2),str(y),fill=(255,255,0))
    c.paste(t.convert('RGB').resize((600,450)).crop((0,0,600,360)),(i*600,0))
c.save(sys.argv[1],quality=86)
