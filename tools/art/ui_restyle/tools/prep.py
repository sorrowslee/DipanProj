# 用法: python3 prep.py <相對 UI 的路徑>   → work/<key>_src.png + work/<key>.json
import sys, json, os
from PIL import Image
UI=os.path.expanduser('~/mnt/DipanProj/DipanProj_Main/Assets/Resources/UI/')
WORK=os.path.join(os.path.dirname(__file__),'..','work')
rel=sys.argv[1]; key=rel.replace('/','__')[:-4]
orig=None
if len(sys.argv)>2:   # 新檔：來源圖不在 UI 底下（相對 Resources），統一放進 500×500 透明畫布
    src=Image.open(UI+'../'+sys.argv[2]).convert('RGBA'); k=min(500/src.width,500/src.height)
    src=src.resize((max(1,round(src.width*k)),max(1,round(src.height*k))),Image.NEAREST if max(src.size)<200 else Image.LANCZOS)
    im=Image.new('RGBA',(500,500),(0,0,0,0)); im.alpha_composite(src,((500-src.width)//2,(500-src.height)//2))
    orig=os.path.abspath(f'{WORK}/{key}_orig.png'); im.save(orig)
else:
    im=Image.open(UI+rel).convert('RGBA')
w,h=im.size; r=w/h
cands={'1x1':1.0,'3x2':1.5,'2x3':2/3}
# 選「能容納且浪費最少」的比例：比例最接近
name=min(cands,key=lambda k: abs(__import__('math').log(cands[k]/r)))
R=cands[name]
if r>=R: CW=w; CH=round(w/R)
else: CH=h; CW=round(h*R)
canvas=Image.new('RGBA',(CW,CH),(128,128,128,255))
ox=(CW-w)//2; oy=(CH-h)//2
canvas.alpha_composite(im,(ox,oy))
canvas.convert('RGB').save(f'{WORK}/{key}_src.png')
json.dump(dict(rel=rel,orig=orig,w=w,h=h,CW=CW,CH=CH,ox=ox,oy=oy,ratio=name,transp=im.getchannel('A').getextrema()[0]<250),open(f'{WORK}/{key}.json','w'))
print(key, name, (CW,CH), 'transp', im.getchannel('A').getextrema()[0]<250)
