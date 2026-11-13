#!/usr/bin/env python3
"""ShadowAnchorMath 的 Python 移植（逐行照 Scripts/ShadowAnchor.cs），離線算影子錨點。
用法: python3 tools/shadow_anchor.py <SequenceImage 下的角色路徑> [動作...]"""
import sys, glob, numpy as np
from PIL import Image
SEQ='../../DipanProj_Main/Assets/GameAssets/Main/Characters/SequenceImage/'
TH=10; BAND=0.15; GAP=2; LIFT=0.06; MINW=1.0; TAIL=0.34; LC=0.25; LCW=0.15
def measure(path):
    a=np.asarray(Image.open(path).convert('RGBA'))[...,3][::-1]  # 原點左下
    h,w=a.shape; m=a>TH
    ys,xs=np.nonzero(m)
    if len(xs)==0: return None
    minX,maxX,minY,maxY=xs.min(),xs.max(),ys.min(),ys.max()
    visH=maxY-minY+1; band=max(2,round(visH*BAND)); bandTop=min(maxY,minY+band-1)
    sub=m[minY:bandTop+1]
    colHas=sub.any(0); colBottom=np.where(colHas, sub.argmax(0)+minY, -1)
    runs=[]; start=-1; last=-1; gap=0; cb=h
    for x in range(minX,maxX+2):
        has = x<=maxX and colHas[x]
        if has:
            if start<0: start=x; cb=h
            last=x; gap=0; cb=min(cb,colBottom[x])
        elif start>=0:
            gap+=1
            if gap>GAP or x>maxX: runs.append((start,last,cb)); start=-1
    if start>=0: runs.append((start,last,cb))
    boxW=maxX-minX+1
    if len(runs)>=2:
        a_=b_=None
        for i,r in enumerate(runs):
            if a_ is None or r[2]<runs[a_][2]: b_=a_; a_=i
            elif b_ is None or r[2]<runs[b_][2]: b_=i
        ra,rb=runs[a_],runs[b_]
        ax=((ra[0]+ra[1]+1)/2+(rb[0]+rb[1]+1)/2)/2; by=(ra[2]+rb[2])/2
        wd=max(max(ra[1],rb[1])-min(ra[0],rb[0])+1, round(boxW*MINW))
    else:
        r=runs[0]; ax=(minX+maxX+1)/2; by=r[2]+visH*LIFT; wd=max(r[1]-r[0]+1, round(boxW*MINW))
    return dict(ax=ax,by=by,w=wd,minX=minX,maxX=maxX,minY=minY,maxY=maxY,W=w,H=h)
def combine(path,action):
    fs=sorted(glob.glob(path+'/*.png')); st=[measure(f) for f in fs]
    lying = action=='dead'
    frm = max(0,len(st)-max(1,int(np.ceil(len(st)*TAIL)))) if lying else 0
    xs=[];ys=[];ws=[]
    for s in st[frm:]:
        if not s: continue
        if lying:
            xs.append((s['minX']+s['maxX']+1)/2-s['W']/2)
            ys.append(s['minY']+min((s['maxY']-s['minY']+1)*LC,(s['maxX']-s['minX']+1)*LCW)); ws.append(s['maxX']-s['minX']+1)
        else:
            xs.append(s['ax']-s['W']/2); ys.append(s['by']); ws.append(s['w'])
    return round(float(np.median(xs)),1),round(float(np.median(ys)),1),round(float(np.median(ws))),len(fs),st[0]['W'],st[0]['H']
if __name__=='__main__':
    c=sys.argv[1]; acts=sys.argv[2:] or ['idle','walk','attack','dead']
    for a in acts: print(c,a,combine(SEQ+c+'/'+a,a))
