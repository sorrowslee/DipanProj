# 用法: python3 overview.py <輸出檔> <rel1> <rel2> ...   新圖（現在 Resources 內）縮到 80px，放大 2x 檢視；上排原圖下排新圖
import sys, os
from PIL import Image
UI=os.path.expanduser('~/mnt/DipanProj/DipanProj_Main/Assets/Resources/UI/'); BK=os.path.dirname(os.path.abspath(__file__))+'/../applied_backup/'
out=sys.argv[1]; rels=sys.argv[2:]; S=90; n=len(rels)
sh=Image.new('RGBA',(n*S,2*S),(30,28,30,255))
def fit(im,s):
    bb=im.getchannel('A').getbbox(); im=im.crop(bb) if bb else im; k=min(s/im.width,s/im.height); return im.resize((max(1,round(im.width*k)),max(1,round(im.height*k))),Image.LANCZOS)
for i,r in enumerate(rels):
    key=r.replace('/','__')[:-4]
    old=BK+key+'.png'
    for row,p in enumerate((old if os.path.exists(old) else UI+r, UI+r)):
        t=fit(Image.open(p).convert('RGBA'),80); sh.alpha_composite(t,(i*S+5+(80-t.width)//2,row*S+5+(80-t.height)//2))
sh=sh.resize((sh.width*2,sh.height*2),Image.NEAREST); sh.convert('RGB').save(out); print(out, sh.size)
