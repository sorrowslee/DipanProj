import sys, numpy as np, cv2
from PIL import Image
def grade(new_p, ref_p, out_p, strength=1.0):
    n=np.array(Image.open(new_p).convert('RGBA')).astype(np.float32)/255
    r=np.array(Image.open(ref_p).convert('RGBA')).astype(np.float32)/255
    def lab(x): return cv2.cvtColor(x[...,:3].copy(),cv2.COLOR_RGB2Lab)
    ln,lr=lab(n),lab(r)
    mn=n[...,3]>0.9; mr=r[...,3]>0.9
    # 保護發光：紫眼與紅岩漿（高彩度）不調
    hsv=cv2.cvtColor(n[...,:3].copy(),cv2.COLOR_RGB2HSV)
    glow=((hsv[...,1]>0.45)&(hsv[...,2]>0.25)).astype(np.float32)
    glow=cv2.GaussianBlur(cv2.dilate(glow,np.ones((5,5))),(0,0),2)
    out=ln.copy()
    for c in range(3):
        a=ln[...,c][mn]; b=lr[...,c][mr]
        s=b.std()/max(a.std(),1e-3) if c==0 else 1.0
        if c>0: s=min(1.0,b.std()/max(a.std(),1e-3))
        out[...,c]=(ln[...,c]-a.mean())*s+b.mean()
    out=ln*(1-strength)+out*strength
    o=cv2.cvtColor(out.astype(np.float32),cv2.COLOR_Lab2RGB)
    o=o*(1-glow[...,None])+n[...,:3]*glow[...,None]
    Image.fromarray((np.dstack([o.clip(0,1),n[...,3]])*255+.5).astype(np.uint8),'RGBA').save(out_p)
if __name__=='__main__': grade(sys.argv[1],sys.argv[2],sys.argv[3],float(sys.argv[4]) if len(sys.argv)>4 else 1.0)
