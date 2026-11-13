# 用法: python3 tools/shadow_preview.py <out.jpg> <角色路徑>...  每角色一列 idle/walk/attack/dead 第 0 與中間幀，畫上表裡的影子（WidthFactor 以 1 近似）
import sys,csv,glob,numpy as np
from PIL import Image,ImageDraw
SEQ='../../DipanProj_Main/Assets/GameAssets/Main/Characters/SequenceImage/'
tab={r[0].lower():r for r in csv.reader(open('../../DipanProj_Main/Assets/Data/ShadowAnchorTable.csv',encoding='utf-8')) if r}
rows=[]
for c in sys.argv[2:]:
    tiles=[]
    for a in ['idle','walk','attack','dead']:
        fs=sorted(glob.glob(SEQ+c+'/'+a+'/*.png'))
        r=tab.get(('characters/'+c+'/'+a).lower())
        for f in [fs[0],fs[len(fs)//2]] if fs else []:
            im=Image.open(f).convert('RGBA'); W,H=im.size
            bg=Image.new('RGBA',(W,H),(150,140,120,255))
            if r:
                x=W/2+float(r[1]); y=H-float(r[2]); w=float(r[3])
                sh=Image.new('RGBA',(W,H),(0,0,0,0)); ImageDraw.Draw(sh).ellipse([x-w/2,y-w/4,x+w/2,y+w/4],fill=(0,0,0,110)); bg.alpha_composite(sh)
            bg.alpha_composite(im); t=bg.convert('RGB').resize((256,256))
            tiles.append(t)
    rows.append(tiles)
c=Image.new('RGB',(256*8,256*len(rows)),(40,40,40))
for i,ts in enumerate(rows):
    for j,t in enumerate(ts): c.paste(t,(j*256,i*256))
c.save(sys.argv[1],quality=85)
