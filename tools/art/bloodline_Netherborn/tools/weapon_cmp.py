# 用法: python3 tools/weapon_cmp.py <master> <out> <img...>   各圖的「刀＋握刀手」區域（上半部）與母版並排＋母版輪廓疊線，給人眼比對
import sys
from PIL import Image, ImageChops, ImageFilter
m=Image.open(sys.argv[1]).convert('RGB'); box=(0,0,1024,560)
mc=m.crop(box)
edge=mc.convert('L').filter(ImageFilter.FIND_EDGES).point(lambda v:255 if v>60 else 0)
tiles=[mc]
for p in sys.argv[3:]:
    c=Image.open(p).convert('RGB').resize(m.size).crop(box)
    ov=c.copy(); ov.paste((0,255,0),mask=edge)   # 母版邊緣綠線疊在新圖上：刀若沒動，綠線會貼著刀
    tiles.append(ov)
W=512; H=280; out=Image.new('RGB',(W*len(tiles),H))
for i,t in enumerate(tiles): out.paste(t.resize((W,H)),(i*W,0))
out.save(sys.argv[2])
