import numpy as np, cv2
from PIL import Image
from scipy.interpolate import RBFInterpolator
A=np.array(Image.open('g01v2_grade.png')).astype(np.float32)/255
B=np.array(Image.open('g08_grade.png')).astype(np.float32)/255
H,W=A.shape[:2]
# 對應點 (A=第1幀, B=第8幀)
pairs=[
 ((640,25),(700,15)),    # 頭頂
 ((415,250),(465,230)),  # 頭左緣
 ((880,180),(920,180)),  # 頭右緣（髮）
 ((610,285),(705,160)),  # 額頭裂縫
 ((530,410),(605,258)),  # 左眼
 ((715,368),(785,253)),  # 右眼
 ((910,280),(925,330)),  # 右耳
 ((850,450),(850,420)),  # 右下顎
 ((595,430),(675,410)),  # 指尖
 ((560,600),(625,600)),  # 手掌左緣中段
 ((660,600),(715,600)),  # 手掌右緣中段
 ((580,850),(620,850)),  # 手腕
]
fixed=[(0,0),(W-1,0),(0,H-1),(W-1,H-1),(W//2,H-1),(0,H//2),(W-1,H//2),(W//2,0),
       (1080,300),(1250,500),(300,550),(200,800),(860,770),(1150,860),(900,1300),(400,1300),(1100,1100)]
PA=np.array([a for a,b in pairs]+fixed,np.float64); PB=np.array([b for a,b in pairs]+fixed,np.float64)
step=8
gy,gx=np.mgrid[0:H:step,0:W:step]; G=np.stack([gx.ravel(),gy.ravel()],1).astype(np.float64)
def mapping(dst_pts,src_pts):
    f=RBFInterpolator(dst_pts,src_pts-dst_pts,kernel='thin_plate_spline',smoothing=1.0)
    d=f(G).reshape(gy.shape+(2,)).astype(np.float32)
    d=cv2.resize(d,(W,H),interpolation=cv2.INTER_CUBIC)
    X,Y=np.meshgrid(np.arange(W,dtype=np.float32),np.arange(H,dtype=np.float32))
    return X+d[...,0],Y+d[...,1]
def premul(x): y=x.copy(); y[...,:3]*=y[...,3:]; return y
Ap,Bp=premul(A),premul(B)
ts=[0,0.077,0.23,0.46,0.69,0.92,1,1]
def smooth(t,k=0.3):  # 混合權重：中段集中切換，減少兩張重疊的時間
    lo,hi=0.5-k,0.5+k
    u=np.clip((t-lo)/(hi-lo),0,1); return u*u*(3-2*u)
for i,t in enumerate(ts,1):
    if t in(0,1): o=A if t==0 else B
    else:
        Pt=(1-t)*PA+t*PB
        mx,my=mapping(Pt,PA); wa=cv2.remap(Ap,mx,my,cv2.INTER_LINEAR,borderValue=0)
        mx,my=mapping(Pt,PB); wb=cv2.remap(Bp,mx,my,cv2.INTER_LINEAR,borderValue=0)
        w=smooth(t); o=wa*(1-w)+wb*w
        al=o[...,3:]; o=np.dstack([np.where(al>1e-3,o[...,:3]/np.maximum(al,1e-3),0),al])
    Image.fromarray((o.clip(0,1)*255+.5).astype(np.uint8),'RGBA').save(f'm_{i:02d}.png')
