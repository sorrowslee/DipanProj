# numpy 版的 BloodlineMist shader（與 .shader 逐段對照；改一邊要兩邊一起改）
import numpy as np
from PIL import Image
U='/mnt/user-data/uploads/DipanProj/TempImage/bloodline_Netherborn/fx/'
def load_gray(p,size=512):
    return np.asarray(Image.open(p).convert('L').resize((size,size),Image.BILINEAR)).astype(np.float32)/255.
SA=load_gray(U+'BossIntroPanel_Smoke1.png'); SB=load_gray(U+'BossIntroPanel_Smoke2.png')
SK=np.asarray(Image.open(U+'soul_skull.png').convert('RGBA')).astype(np.float32)/255.
def samp(tex,u,v):   # repeat + bilinear
    n=tex.shape[0]; x=(u%1)*n-0.5; y=(v%1)*n-0.5
    x0=np.floor(x).astype(int); y0=np.floor(y).astype(int); fx=x-x0; fy=y-y0
    x0%=n; y0%=n; x1=(x0+1)%n; y1=(y0+1)%n
    t=tex
    return (t[y0,x0]*(1-fx)*(1-fy)+t[y0,x1]*fx*(1-fy)+t[y1,x0]*(1-fx)*fy+t[y1,x1]*fx*fy)
def sampclamp(tex,u,v):
    n=tex.shape[0]; x=np.clip((u*n).astype(int),0,n-1); y=np.clip((v*n).astype(int),0,n-1); return tex[y,x]
def ss(a,b,x): t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)
def rot(u,v,a): s,c=np.sin(a),np.cos(a); return c*u-s*v, s*u+c*v
def srgb2lin(c): c=np.array(c)/255.; return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def lin2srgb(c): c=np.clip(c,0,1); return np.where(c<=0.0031308,c*12.92,1.055*c**(1/2.4)-0.055)

P=dict(Deep=srgb2lin((5,3,9)),Glow=srgb2lin((96,48,156)),SkullCol=srgb2lin((140,122,180)),
       DensityLo=0.10,DensityHi=0.85,FogMin=0.55,MaxOp=0.95,Wisp=0.35,Core=0.0,Scatter=0.05,
       TexScale=1.2,Flow=0.8,Rise=0.10,Warp=0.12,Inner=0.10,Outer=0.33,Bite=0.30,TopFade0=0.55,TopFade1=0.98)

def mist(W,H,t,skull=None):
    # uv：x 0..1 左到右，y 0..1 下到上（同 Unity）
    u,v=np.meshgrid((np.arange(W)+0.5)/W,1-(np.arange(H)+0.5)/H)
    tt=t*P['Flow']
    qx=u-0.5; qy=v-0.45
    wfac=np.interp(v,[0,1],[1.0,0.55])                 # 越高越窄（往上竄的焰形）
    d=np.sqrt((qx/wfac*1.2)**2+(qy*0.8)**2)
    su=u*P['TexScale']; sv=v*P['TexScale']-tt*P['Rise']   # 整體往上流
    wx=samp(SB,su*0.25+tt*0.013,sv*0.25-tt*0.021); wy=samp(SB,su*0.25+0.37-tt*0.017,sv*0.25+0.71+tt*0.011)
    wu=su+(wx-0.5)*P['Warp']*2; wv=sv+(wy-0.5)*P['Warp']*2
    r1=rot(wu,wv,0.0); a1=samp(SA,r1[0]*0.42+tt*0.010,r1[1]*0.42-tt*0.022)
    r2=rot(wu,wv,2.1); a2=samp(SA,r2[0]*0.75-tt*0.015,r2[1]*0.75-tt*0.031)
    thick=1-(1-a1)*(1-a2)
    r3=rot(wu,wv,1.1); b1=samp(SB,r3[0]*0.60+tt*0.021,r3[1]*0.60-tt*0.040)
    r4=rot(wu,wv,4.0); b2=samp(SB,r4[0]*1.05-tt*0.029,r4[1]*1.05-tt*0.055)
    wisp=np.maximum(b1,b2)
    radial=1-ss(P['Inner'],P['Outer'],d-(thick-0.5)*P['Bite'])
    radial*=1-ss(P['TopFade0'],P['TopFade1'],v)*(1-thick*0.5)   # 頂端散掉
    radial*=ss(0.02,0.16,v)   # 底部收掉（不在腳下鋪一片）
    body=ss(P['DensityLo'],P['DensityHi'],thick)*radial
    k=np.clip(body*0.15,0,1)[...,None]   # 主體幾乎全黑
    col=P['Deep']*(1-k)+P['Glow']*k
    col=col+P['Glow']*(wisp*P['Wisp']*body*(1-body*0.6))[...,None]   # 紫色細絲偏在霧的稀薄處＝邊緣+P['Glow']*(np.clip(body,0,1)**4*P['Core'])[...,None]
    rim=ss(0.02,0.18,radial)*(1-ss(0.22,0.45,radial))*ss(0.05,0.5,thick)   # 只取霧團最外緣那一窄條
    col=col+P['Glow']*(rim*0.55)[...,None]   # 紫色冥光勾邊（黑霧在暗地板上只能靠這圈讀出形狀）
    a=(P['FogMin']+(P['MaxOp']-P['FogMin'])*body)*radial
    scatter=P['Glow']*((body*P['Scatter'])+rim*0.05)[...,None]
    if skull is not None:
        cx,cy,sz,c=skull          # c: 凝結進度 0..1（>1 之後是散掉）
        ku=(u-cx)/sz+0.5; kv=(v-cy)/(sz*W/H)+0.5   # 與 shader 同：_SkullPos.w = 寬/高
        inside=(ku>0)&(ku<1)&(kv>0)&(kv<1)
        s=sampclamp(SK,ku,1-kv)   # 貼圖 y 由上往下
        lum=s[...,0]; sa=s[...,3]*inside
        m=samp(SB,ku*0.9+0.13,kv*0.9+0.57)
        form=np.clip(c,0,1); gone=np.clip(c-1,0,1)
        th=1-form                    # 凝結：門檻由 1 降到 0
        keep=ss(th-0.18,th+0.18,m*0.55+sa*0.45)*(1-ss(0,1,gone*1.4-(1-kv)*0.4))  # 散掉：上方先散
        sk=sa*keep*0.85
        skc=P['Deep']*(1-lum[...,None])+P['SkullCol']*lum[...,None]
        col=col*(1-sk[...,None])+skc*sk[...,None]
        a=np.maximum(a,sk*0.92)
        scatter=scatter+P['Glow']*(sk*lum*0.10)[...,None]
    outA=np.clip(a,0,1)
    rgb=np.clip(col*outA[...,None]+scatter,0,1)
    return rgb,outA

def frame(t,skull,label):
    cain=Image.open(U+'cain_idle.png').convert('RGBA'); ca=np.asarray(cain)[...,3]>40; ys,xs=np.nonzero(ca)
    S=2; h=(ys.max()-ys.min())*S; feetY=ys.max()*S; cx=(xs.min()+xs.max())/2*S
    Wc,Hc=640,860
    bg=np.ones((Hc,Wc,3))*srgb2lin((36,32,34))
    # 霧 quad：寬 1.7h、高 2.4h，中心在身體中心上方 0.30h
    qw,qh=int(1.7*h),int(2.4*h)
    ox=Wc/2; footY=Hc-140; bodyC=footY-h/2
    qcy=bodyC-0.30*h
    rgb,al=mist(qw,qh,t,skull)
    x0=int(ox-qw/2); y0=int(qcy-qh/2)
    reg=bg[y0:y0+qh,x0:x0+qw]; reg[:]=rgb+reg*(1-al[...,None])
    img=Image.fromarray((lin2srgb(bg)*255).astype(np.uint8))
    big=cain.resize((256*S,256*S),Image.NEAREST)
    img.paste(big,(int(ox-cx),int(footY-feetY)),big)
    return img
# 三個階段：凝結中、成形、散去；骷髏在左肩後上方
frames=[frame(0.0,(0.37,0.56,0.24,0.45),'a'),frame(0.6,(0.37,0.59,0.24,1.0),'b'),frame(1.4,(0.37,0.65,0.24,1.55),'c'),frame(2.5,None,'d')]
frames=[f.crop((120,180,520,780)) for f in frames]
W=sum(f.width for f in frames); out=Image.new('RGB',(W,frames[0].height))
x=0
for f in frames: out.paste(f,(x,0)); x+=f.width
out.save('/tmp/claude-0/mist/preview.png'); print(out.size)
