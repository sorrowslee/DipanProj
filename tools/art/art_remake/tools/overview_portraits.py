# 用法: python3 tools/overview_portraits.py <out.jpg> [path...]  （預設 installed.txt），灰底並排 normal，標準線 235/800
import sys
from PIL import Image, ImageDraw
T='../../DipanProj_Main/Assets/GameAssets/Main/Characters/Talk/'
ps=sys.argv[2:] or list(dict.fromkeys(l.strip() for l in open('installed.txt') if l.strip()))
cols=8; cw,ch=512,768
rows=(len(ps)+cols-1)//cols
c=Image.new('RGB',(cw*cols,ch*rows),(128,128,128)); d=ImageDraw.Draw(c)
for i,p in enumerate(ps):
    im=Image.open(T+p+'/normal.png').convert('RGBA')
    if im.size==(1536,1024):
        im=im.resize((2304,1536),Image.LANCZOS).crop((538,0,1562,1536))  # 寬版＝長版 2/3：放大 1.5 倍後以 x700 為中心裁成長版等效
    t=Image.new('RGBA',im.size,(128,128,128,255)); t.alpha_composite(im); t=t.convert('RGB').resize((cw,ch),Image.LANCZOS)
    x,y=(i%cols)*cw,(i//cols)*ch; c.paste(t,(x,y)); d.text((x+8,y+8),p,fill=(255,255,255))
    for yy,col in ((235,(0,255,255)),(800,(255,0,0))): d.line([(x,y+yy//2),(x+cw,y+yy//2)],fill=col,width=1)
c.save(sys.argv[1],quality=85); print(c.size)
