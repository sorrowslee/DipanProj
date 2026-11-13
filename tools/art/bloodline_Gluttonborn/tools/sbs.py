# 用法: python3 tools/sbs.py <out> <img...>   多張並排（每張縮 512×768），給人眼比對刀與握刀手是否與母版一致
import sys
from PIL import Image
ims=[Image.open(p).convert('RGB').resize((512,768)) for p in sys.argv[2:]]
o=Image.new('RGB',(512*len(ims),768))
for i,im in enumerate(ims): o.paste(im,(i*512,0))
o.save(sys.argv[1])
