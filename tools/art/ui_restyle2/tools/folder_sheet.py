import sys, os
from PIL import Image, ImageDraw, ImageFont
R=os.path.expanduser('~/mnt/DipanProj/DipanProj_Main/Assets/Resources/UI/')
out=sys.argv[1]; folders=sys.argv[2:]
files=[]
for f in folders:
    for root,_,fs in os.walk(R+f):
        for n in sorted(fs):
            if n.endswith('.png'): files.append(os.path.join(root,n))
CW,CH,LH=300,240,34; cols=5
rows=(len(files)+cols-1)//cols
S=Image.new('RGB',(cols*CW,rows*(CH+LH)),(18,18,18)); d=ImageDraw.Draw(S)
try: font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',12)
except: font=ImageFont.load_default()
for i,p in enumerate(files):
    x=(i%cols)*CW; y=(i//cols)*(CH+LH)
    # checker
    for cy in range(0,CH,16):
        for cx in range(0,CW,16):
            c=(44,44,44) if (cx//16+cy//16)%2 else (58,58,58)
            d.rectangle([x+cx,y+cy,x+cx+15,y+cy+15],fill=c)
    im=Image.open(p).convert('RGBA'); w,h=im.size
    s=min((CW-10)/w,(CH-10)/h); t=im.resize((max(1,int(w*s)),max(1,int(h*s))),Image.LANCZOS)
    S.paste(t,(x+(CW-t.width)//2,y+(CH-t.height)//2),t)
    rel=os.path.relpath(p,R)
    d.text((x+4,y+CH+2),rel[:46],fill=(220,220,220),font=font)
    d.text((x+4,y+CH+17),f'{w}x{h}',fill=(150,150,150),font=font)
S.save(out); print(out,len(files),S.size)
