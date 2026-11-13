# numpy 版的 BloodlineGhost shader＋元件擺位（與 .shader／.cs 逐段對照；改一邊要兩邊一起改）
# 用法: python3 preview.py <ghost_tex.png> <out.png> [sideOffset heightAbove size]
import sys, numpy as np
from PIL import Image
D='./'
def load_gray(p,size=512):
    return np.asarray(Image.open(p).convert('L').resize((size,size),Image.BILINEAR)).astype(np.float32)/255.
SA=load_gray(D+'BossIntroPanel_Smoke1.png'); SB=load_gray(D+'BossIntroPanel_Smoke2.png')
GT=np.asarray(Image.open(sys.argv[1]).convert('RGBA')).astype(np.float32)/255.
def samp(tex,u,v):
    n=tex.shape[0]; x=(u%1)*n-0.5; y=(v%1)*n-0.5
    x0=np.floor(x).astype(int); y0=np.floor(y).astype(int); fx=x-x0; fy=y-y0
    x0%=n; y0%=n; x1=(x0+1)%n; y1=(y0+1)%n
    if tex.ndim==3: fx=fx[...,None]; fy=fy[...,None]
    return tex[y0,x0]*(1-fx)*(1-fy)+tex[y0,x1]*fx*(1-fy)+tex[y1,x0]*(1-fx)*fy+tex[y1,x1]*fx*fy
def gsamp(u,v):  # 幽靈貼圖：clamp，uv y 往上 ⇒ 貼圖列由上往下
    n=GT.shape[0]; inside=((u>=0)&(u<=1)&(v>=0)&(v<=1)).astype(np.float32)
    x=np.clip(u*n-0.5,0,n-1.001); y=np.clip((1-v)*n-0.5,0,n-1.001)
    x0=np.floor(x).astype(int); y0=np.floor(y).astype(int); fx=(x-x0)[...,None]; fy=(y-y0)[...,None]
    s=GT[y0,x0]*(1-fx)*(1-fy)+GT[y0,x0+1]*fx*(1-fy)+GT[y0+1,x0]*(1-fx)*fy+GT[y0+1,x0+1]*fx*fy
    return s, inside
def ss(a,b,x): t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)
def srgb2lin(c): c=np.array(c)/255.; return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def lin2srgb(c): c=np.clip(c,0,1); return np.where(c<=0.0031308,c*12.92,1.055*c**(1/2.4)-0.055)
P=dict(Ink=srgb2lin((8,6,8)),Pale=srgb2lin((225,216,196)),Glow=srgb2lin((112,20,20)),GlowAmt=1.0,GlowR=0.025,
       Opacity=0.88,Flicker=0.35,Mottle=0.25,Head=(0.5,0.36),TailStart=0.26,TailWarp=0.07,HeadWarp=0.006,WarpSpeed=0.12)
def ghost(N,t,gT,seed,flip,vis=1.0):
    u,v=np.meshgrid((np.arange(N)+0.5)/N,1-(np.arange(N)+0.5)/N)
    if flip: u=1-u
    dh=np.sqrt((u-P['Head'][0])**2+(v-P['Head'][1])**2); tail=ss(P['TailStart'],P['TailStart']+0.28,dh)
    wu=u*0.8+seed*0.37; wv=v*0.8+seed*0.11
    wx=samp(SB,wu+t*0.013,wv-t*P['WarpSpeed']); wy=samp(SB,wu+0.37-t*0.017,wv+0.71-t*P['WarpSpeed']*0.8)
    k=(P['TailWarp']*tail+P['HeadWarp'])*2
    gu=u+(wx-0.5)*k; gv=v+(wy-0.5)*k
    s,ins=gsamp(gu,gv); a=s[...,3]*ins; lum=s[...,0]
    mot=samp(SA,u*1.3+seed,v*1.3-t*0.05)
    col=P['Ink']*(1-lum[...,None])+P['Pale']*lum[...,None]
    a=a*((1-P['Mottle'])+P['Mottle']*mot)*(1-0.15*tail)
    r=P['GlowR']; blur=np.zeros_like(a)
    for dx,dy in [(r,0),(-r,0),(0,r),(0,-r),(r*.707,r*.707),(-r*.707,r*.707),(r*.707,-r*.707),(-r*.707,-r*.707)]:
        sb,ib=gsamp(gu+dx,gv+dy); blur+=sb[...,3]*ib
    blur/=8; glow=np.clip(blur-s[...,3]*ins,0,1)*P['GlowAmt']
    m=samp(SB,u*0.9+0.13+seed*0.21,v*0.9+0.57)
    form=np.clip(gT,0,1); gone=np.clip(gT-1,0,1); th=1-form
    keep=ss(th-0.18,th+0.18,m*0.55+np.maximum(a,blur)*0.45)
    vv=gone*1.6-(1-tail)*0.55+(m-0.5)*0.35; keep*=1-ss(0,0.35,vv)
    outA=np.clip(a*keep*vis*P['Opacity'],0,1)
    rgb=np.clip(col*outA[...,None]+P['Glow']*(glow*keep*vis*P['Opacity'])[...,None],0,1)
    oa=np.clip(outA+glow*keep*vis*P['Opacity']*0.35,0,1)
    return rgb,oa
if __name__=='__main__':
    out=sys.argv[2]
    side=float(sys.argv[3]) if len(sys.argv)>3 else 0.62
    above=float(sys.argv[4]) if len(sys.argv)>4 else 0.22
    size=float(sys.argv[5]) if len(sys.argv)>5 else 0.6
    TA='../../../DipanProj_Main/Assets/GameAssets/Main/Characters/SequenceImage/Gluttonborn/Taotie/idle/Taotie-iso_idle_right-trimmed_01.png'
    spr=np.asarray(Image.open(TA).convert('RGBA')).astype(np.float32)/255.
    sc=3; spr=np.asarray(Image.fromarray((spr*255).astype(np.uint8)).resize((256*sc,256*sc),Image.LANCZOS)).astype(np.float32)/255.
    al=spr[...,3]; ys,xs=np.where(al>0.04); top,bot=ys.min(),ys.max(); H=bot-top; cx=(xs.min()+xs.max())/2; cy=(top+bot)/2
    W,Hc=1100,800; bg=np.ones((Hc,W,3),np.float32)*srgb2lin((46,44,48))
    ox=W//2-int(cx); oy=Hc-120-int(bot)
    panels=[]
    for title,gTs,t in [('凝結中',(0.55,-1),0.0),('左右都在',(1.0,1.0),1.3),('散去中',(1.5,1.0),2.6)]:
        img=bg.copy()
        def comp(rgb,a,x0,y0):
            h,w=a.shape; y1,x1=y0+h,x0+w
            sy0,sx0=max(0,-y0),max(0,-x0); y0c,x0c=max(0,y0),max(0,x0); y1c,x1c=min(Hc,y1),min(W,x1)
            r=rgb[sy0:sy0+(y1c-y0c),sx0:sx0+(x1c-x0c)]; aa=a[sy0:sy0+(y1c-y0c),sx0:sx0+(x1c-x0c)][...,None]
            img[y0c:y1c,x0c:x1c]=r+img[y0c:y1c,x0c:x1c]*(1-aa)
        N=int(H*size)
        for gi,(gT,flipSide) in enumerate([(gTs[0],0),(gTs[1],1)]):
            if gT<0: continue
            sidef=-1 if gi==0 else 1
            rgb,a=ghost(N,t,gT,seed=gi*3.7,flip=(gi==0))
            gx=int(ox+cx+sidef*H*side-N/2); gy=int(oy+cy-H*above-N/2)
            comp(rgb,a,gx,gy)
        # 角色蓋在上面（幽靈畫在角色之下）
        sl=srgb2lin((spr[...,:3]*255).astype(int)); sa=spr[...,3]
        comp(sl*sa[...,None],sa,ox,oy)
        panels.append((title,img))
    row=np.concatenate([p[1] for p in panels],axis=1)
    Image.fromarray((lin2srgb(row)*255).astype(np.uint8)).save(out); print(out)
