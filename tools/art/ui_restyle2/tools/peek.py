# python3 peek.py out.png key1 key2 ...  → 新圖(work/<key>_new.png) 排一排，深底
import sys
from PIL import Image
out=sys.argv[1]; ks=sys.argv[2:]; S=200
sh=Image.new('RGBA',(S*len(ks),S),(40,36,34,255))
for i,k in enumerate(ks):
    im=Image.open(f'work/{k}_new.png').convert('RGBA').resize((S,S),Image.LANCZOS); sh.alpha_composite(im,(i*S,0))
sh.convert('RGB').save(out)
