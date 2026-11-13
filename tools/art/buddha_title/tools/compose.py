import numpy as np, cv2
from PIL import Image
def load(p): return np.array(Image.open(p)).astype(np.float32)/255
R=load('g02_grade.png'); H,W=R.shape[:2]
def gray(x):
    c=x[...,:3]*x[...,3:]+0.05*(1-x[...,3:]); return cv2.cvtColor(c,cv2.COLOR_RGB2GRAY)
# 頭＋手區域（會換）
def region_mask(feather=32):
    m=np.zeros((H,W),np.float32)
    cv2.ellipse(m,(700,270),(400,370),0,0,360,1,-1)          # 頭（含耳、髮）
    pts=np.array([[480,420],[930,420],[900,1180],[500,1180]],np.int32)  # 合掌雙手到手腕
    cv2.fillPoly(m,[pts],1)
    return cv2.GaussianBlur(m,(0,0),feather)
M=region_mask()
body=(cv2.GaussianBlur(M,(0,0),5)<0.02).astype(np.uint8)   # 對齊只看身體
def register(F):
    warp=np.eye(2,3,dtype=np.float32)
    g1=cv2.resize(gray(R),None,fx=.5,fy=.5); g2=cv2.resize(gray(F),None,fx=.5,fy=.5); bm=cv2.resize(body,None,fx=.5,fy=.5,interpolation=cv2.INTER_NEAREST)
    try:
        _,warp=cv2.findTransformECC(g1,g2,warp,cv2.MOTION_AFFINE,(cv2.TERM_CRITERIA_EPS|cv2.TERM_CRITERIA_COUNT,200,1e-6),bm,5)
    except cv2.error as e: print('ECC fail',e)
    warp[:,2]*=2
    out=cv2.warpAffine(F,warp,(W,H),flags=cv2.INTER_LINEAR|cv2.WARP_INVERSE_MAP,borderMode=cv2.BORDER_CONSTANT,borderValue=0)
    return out,warp
def premul(x): y=x.copy(); y[...,:3]*=y[...,3:]; return y
def unpremul(y): a=y[...,3:]; return np.dstack([np.where(a>1e-3,y[...,:3]/np.maximum(a,1e-3),0),a])
Rp=premul(R)
frames={}
for k in range(2,8):
    F=load(f'g0{k}_grade.png')
    if k==2: o=R
    else:
        Fa,w=register(F); print(k,'affine',np.round(w,3).tolist())
        o=unpremul(premul(Fa)*M[...,None]+Rp*(1-M[...,None]))
    frames[k]=o
seq=[2,2,3,4,5,6,7,7]
for i,k in enumerate(seq,1):
    Image.fromarray((frames[k].clip(0,1)*255+.5).astype(np.uint8),'RGBA').save(f'c_{i:02d}.png')
# 遮罩示意
Image.fromarray((M*255).astype(np.uint8)).save('mask.png')
