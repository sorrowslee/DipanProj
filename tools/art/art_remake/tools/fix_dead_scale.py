#!/usr/bin/env python3
"""G23/G24：dead 序列以第一幀腳底為中心縮放到 idle 的體積尺度，再整數平移讓腳底距畫布中心與 idle 第一幀相同。
用法: python3 fix_dead_scale.py [--scale F] <idle 資料夾> <dead 資料夾...>（就地改寫）
--scale：指定倍率（第一幀是趴姿／彎腰時體積尺度不準，改用輪廓面積比算）；1＝只平移、不重取樣。"""
import sys, glob, numpy as np
from PIL import Image
def first_stats(d):
    f=sorted(glob.glob(d+'/*.png'))[0]; im=Image.open(f).convert('RGBA'); a=np.asarray(im)[...,3]>10
    ys,xs=np.nonzero(a); H=a.shape[0]
    h=ys.max()-ys.min()+1; vol=(h*a.sum()**0.5)**0.5
    return vol, ys.max(), H
args=sys.argv[1:]
force=None
if args[0]=='--scale': force=float(args[1]); args=args[2:]
iv,ib,iH=first_stats(args[0]); idle_off=(iH-1-ib)-iH/2   # 腳底距畫布中心（往下為負）
for d in args[1:]:
    dv,db,dH=first_stats(d); f=force if force else iv/dv
    foot_from_bottom=dH-1-db
    target_from_bottom=dH/2+idle_off
    shift=round(target_from_bottom-foot_from_bottom)
    for p in sorted(glob.glob(d+'/*.png')):
        im=Image.open(p).convert('RGBA'); W,H=im.size
        if abs(f-1)<1e-6:   # 只平移：整數像素，不重取樣
            sh=round(target_from_bottom-foot_from_bottom); a=np.asarray(im); out=np.zeros_like(a)
            if sh>=0: out[:H-sh]=a[sh:]
            else: out[-sh:]=a[:H+sh]
            Image.fromarray(out,'RGBA').save(p); continue
        a=np.asarray(im).astype(np.float32); al=a[...,3:4]/255.0
        pre=np.concatenate([a[...,:3]*al,a[...,3:4]],-1)
        big=Image.fromarray(np.clip(pre,0,255).astype(np.uint8),'RGBA')
        nw,nh=round(W*f),round(H*f); sm=np.asarray(big.resize((nw,nh),Image.LANCZOS)).astype(np.float32)
        out=np.zeros((H,W,4),np.float32)
        # 以 (W/2, db) 為縮放中心：新圖中該點落在 (nw/2, db*f)，要放到 (W/2, db)，再往上平移 shift
        shift=round(target_from_bottom-foot_from_bottom)
        ox=round(W/2-nw/2); oy=round(db-db*f)-shift
        x0,y0=max(0,ox),max(0,oy); x1,y1=min(W,ox+nw),min(H,oy+nh)
        out[y0:y1,x0:x1]=sm[y0-oy:y1-oy,x0-ox:x1-ox]
        A=out[...,3:4]; rgb=np.where(A>0,out[...,:3]/np.maximum(A,1e-6)*255.0,0)
        res=np.concatenate([np.clip(rgb,0,255),A],-1).astype(np.uint8)
        Image.fromarray(res,'RGBA').save(p)
    print(d,'scale=%.3f shift_up=%d'%(f,shift))
