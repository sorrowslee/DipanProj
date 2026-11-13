# 用法: python3 sheet.py <gpt.png> <cols> <rows> <key1,key2,...> [--apply]
# 把一張 N 格的圖示表切開：每格去灰底（四邊 flood fill + 全域灰）→ 內容置中放進 500×500（留 6% 邊）→ work/<key>_new.png
import sys, os, json, shutil
import numpy as np
from collections import deque
from PIL import Image, ImageFilter
D=os.path.dirname(os.path.abspath(__file__)); WORK=D+'/../work'
UI=os.path.expanduser('~/mnt/DipanProj/DipanProj_Main/Assets/Resources/UI/')
src,cols,rows,keys=sys.argv[1],int(sys.argv[2]),int(sys.argv[3]),sys.argv[4].split(',')
apply='--apply' in sys.argv
g=Image.open(src).convert('RGB'); W,H=g.size; cw,ch=W/cols,H/rows
for i,key in enumerate(keys):
    if key in ('','-'): continue
    c,r=i%cols,i//cols
    cell=g.crop((round(c*cw),round(r*ch),round((c+1)*cw),round((r+1)*ch)))
    a=np.asarray(cell).astype(int); h,w,_=a.shape
    edge=np.concatenate([a[:3].reshape(-1,3),a[-3:].reshape(-1,3),a[:,:3].reshape(-1,3),a[:,-3:].reshape(-1,3)])
    bg=np.median(edge,0); dist=np.sqrt(((a-bg)**2).sum(2)); sat=a.max(2)-a.min(2)
    isbg=(dist<40)&(sat<26)
    vis=np.zeros((h,w),bool); q=deque()
    for x in range(w):
        for y in (0,h-1):
            if isbg[y,x]: vis[y,x]=1; q.append((y,x))
    for y in range(h):
        for x in (0,w-1):
            if isbg[y,x] and not vis[y,x]: vis[y,x]=1; q.append((y,x))
    while q:
        y,x=q.popleft()
        for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
            ny,nx=y+dy,x+dx
            if 0<=ny<h and 0<=nx<w and not vis[ny,nx] and isbg[ny,nx]: vis[ny,nx]=1; q.append((ny,nx))
    # 封閉的灰色洞（環、把手內側）：與背景色極接近才挖
    hole=(dist<14)&(sat<10)
    alpha=np.where(vis|hole,0,255).astype(np.uint8)
    al=Image.fromarray(alpha).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
    im=cell.convert('RGBA'); im.putalpha(al)
    bb=al.point(lambda v:255 if v>127 else 0).getbbox()
    if not bb: print(key,'EMPTY'); continue
    piece=im.crop(bb); k=min(440/piece.width,440/piece.height)
    piece=piece.resize((max(1,round(piece.width*k)),max(1,round(piece.height*k))),Image.LANCZOS)
    out=Image.new('RGBA',(500,500),(0,0,0,0)); out.alpha_composite(piece,((500-piece.width)//2,(500-piece.height)//2))
    out.save(f'{WORK}/{key}_new.png')
    m=json.load(open(f'{WORK}/{key}.json'))
    msg=f'{key}: bbox {bb} -> {piece.size}'
    if apply:
        os.makedirs(os.path.dirname(UI+m['rel']),exist_ok=True); shutil.copyfile(f'{WORK}/{key}_new.png',UI+m['rel']); msg+=' APPLIED'
    print(msg)
