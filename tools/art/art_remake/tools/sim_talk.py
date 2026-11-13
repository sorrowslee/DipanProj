# 模擬 TalkPanel.SetAvatarWaistUp（1920×1080）：python3 tools/sim_talk.py <out.jpg> <左 Talk 路徑> <右 Talk 路徑> [左Scale 右Scale]
import sys,numpy as np
from PIL import Image,ImageDraw
T='../../DipanProj_Main/Assets/GameAssets/Main/Characters/Talk/'
SW,SH=1920,1080; boxTop=24+828*(1500/2246)
HF,WF=235/1536,800/1536; HEADY=975; TOPLIM=1072; BX=500
def place(scr,path,right,scale=1.0):
    im=Image.open(T+path).convert('RGBA'); cw,ch=im.size; a=np.asarray(im)[...,3]>8; ys,xs=np.nonzero(a)
    H=(HEADY-boxTop)/(WF-HF)*scale; W=H*cw/ch
    top=HEADY+HF*H; ctop=top-(ys.min()/ch)*H
    if ctop>TOPLIM: top-=ctop-TOPLIM
    bottom=top-H; cx=(700/1536) if cw>ch else (470/1024)
    if right: im=im.transpose(Image.FLIP_LEFT_RIGHT); left=SW/2+BX-(1-cx)*W
    else: left=SW/2-BX-cx*W
    r=im.resize((round(W),round(H)),Image.LANCZOS)
    scr.alpha_composite(r,(round(left),round(SH-top)))
scr=Image.new('RGBA',(SW+2000,SH),(60,55,50,255))  # 右側多留空間避免越界
base=Image.new('RGBA',(SW,SH),(60,55,50,255))
sc=[float(x) for x in sys.argv[4:6]] if len(sys.argv)>4 else [1,1]
place(base,sys.argv[2],False,sc[0]); place(base,sys.argv[3],True,sc[1])
d=ImageDraw.Draw(base); d.rectangle([SW/2-750,SH-boxTop,SW/2+750,SH-24],fill=(30,25,20,255),outline=(180,140,80,255),width=6)
base.convert('RGB').resize((960,540)).save(sys.argv[1],quality=85)
