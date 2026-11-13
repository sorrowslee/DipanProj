#!/usr/bin/env python3
"""把 installed.txt 裡重繪過的立繪補進 PortraitTable.csv（長版 1.15/-139；寬版 1.2/-180/OuterToScreen 1）。已有列者略過。"""
import sys,os
from PIL import Image
ROOT=os.path.expanduser('~/mnt/DipanProj/DipanProj_Main/Assets')
CSV=ROOT+'/Data/PortraitTable.csv'
TALK=ROOT+'/GameAssets/Main/Characters/Talk/'
EMOS=['normal','happy','angry','cry','fear','hurt']
lines=open(CSV,encoding='utf-8').read().splitlines()
have={l.split(',')[0] for l in lines if l and not l.startswith('#')}
paths=[p.strip() for p in open('installed.txt') if p.strip()]
add=[]
for p in dict.fromkeys(paths):
    f=TALK+p+'/normal.png'
    if not os.path.exists(f): print('MISSING',f); continue
    w,h=Image.open(f).size
    if (w,h)==(1024,1536): row='1.15,,-139,,%s：2026-11-13 統一重繪（1024×1536 全身、頭頂≈235）⇒ 照〈頂端 975 規則〉1.15／−139'
    elif (w,h)==(1536,1024): row='1.2,,-180,1,%s：2026-11-13 統一重繪（寬版 1536×1024、頭頂≈157）⇒ 比照其他寬版 1.2／−180／OuterToScreen 1'
    else: print('skip',p,w,h); continue
    for e in EMOS:
        i='Main/Characters/Talk/%s/%s'%(p,e)
        if i in have: continue
        add.append(i+','+row%p.split('/')[-1])
if add:
    open(CSV,'a',encoding='utf-8').write(('\n' if not lines[-1]=='' else '')+'\n'.join(add)+'\n')
print('added',len(add))
