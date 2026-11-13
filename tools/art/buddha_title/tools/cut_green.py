import sys, numpy as np, cv2
from PIL import Image
def cut(path, size=1440):
    im=np.array(Image.open(path).convert('RGB')).astype(np.float32)
    r,g,b=im[...,0],im[...,1],im[...,2]
    # 綠度 = g 比 max(r,b) 高多少
    gx=g-np.maximum(r,b)
    a=1-np.clip((gx-25)/(120-25),0,1)   # gx<=25 不透明, >=120 全透明
    # 去綠溢色：邊緣 g 壓到 max(r,b)
    spill=np.clip(gx,0,None)
    g2=np.where(gx>0, np.maximum(r,b)+ (gx)*0.0, g)
    out=np.dstack([r,g2,b,a*255]).clip(0,255).astype(np.uint8)
    o=Image.fromarray(out,'RGBA')
    # 預乘縮放
    arr=np.array(o).astype(np.float32)/255; arr[...,:3]*=arr[...,3:4]
    pm=Image.fromarray((arr*255).astype(np.uint8),'RGBA').resize((size,size),Image.LANCZOS)
    p=np.array(pm).astype(np.float32)/255; al=p[...,3:4]; p[...,:3]=np.where(al>1e-3,p[...,:3]/np.maximum(al,1e-3),0)
    return Image.fromarray((p.clip(0,1)*255+.5).astype(np.uint8),'RGBA')
if __name__=='__main__':
    cut(sys.argv[1]).save(sys.argv[2])
