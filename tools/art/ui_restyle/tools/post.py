# 用法: python3 post.py <key> <下載檔路徑> [--apply] [--min-iou 0.9]
import sys, json, os, shutil
import numpy as np
from collections import deque
from PIL import Image, ImageFilter
D=os.path.dirname(os.path.abspath(__file__)); WORK=D+'/../work'; BK=D+'/../applied_backup'
UI=os.path.expanduser('~/mnt/DipanProj/DipanProj_Main/Assets/Resources/UI/')
key,src=sys.argv[1],sys.argv[2]; apply='--apply' in sys.argv
miniou=float(sys.argv[sys.argv.index('--min-iou')+1]) if '--min-iou' in sys.argv else 0.90
m=json.load(open(f'{WORK}/{key}.json'))
g=Image.open(src).convert('RGB'); GW,GH=g.size
sx=GW/m['CW']; sy=GH/m['CH']
crop=g.crop((round(m['ox']*sx),round(m['oy']*sy),round((m['ox']+m['w'])*sx),round((m['oy']+m['h'])*sy)))
crop=crop.resize((m['w'],m['h']),Image.LANCZOS)
orig=Image.open(m.get('orig') or UI+m['rel']).convert('RGBA'); oa=np.asarray(orig.getchannel('A'))>127
if m['transp']:
    a=np.asarray(crop).astype(int); H,W,_=a.shape
    # 背景色：取畫布四角（裁切外框）的實際灰
    full=np.asarray(g).astype(int); bg=np.median(np.concatenate([full[:4,:].reshape(-1,3),full[-4:,:].reshape(-1,3),full[:,:4].reshape(-1,3),full[:,-4:].reshape(-1,3)]),0)
    dist=np.sqrt(((a-bg)**2).sum(2)); sat=a.max(2)-a.min(2)
    isbg=(dist<38)&(sat<25)
    vis=np.zeros((H,W),bool); q=deque()
    for x in range(W):
        for y in (0,H-1):
            if isbg[y,x] and not vis[y,x]: vis[y,x]=1; q.append((y,x))
    for y in range(H):
        for x in (0,W-1):
            if isbg[y,x] and not vis[y,x]: vis[y,x]=1; q.append((y,x))
    while q:
        y,x=q.popleft()
        for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
            ny,nx=y+dy,x+dx
            if 0<=ny<H and 0<=nx<W and not vis[ny,nx] and isbg[ny,nx]: vis[ny,nx]=1; q.append((ny,nx))
    # 原圖完全透明、且 GPT 也接近灰的區域 → 一律透明（處理內部鏤空）
    hole=(~oa)&isbg
    if '--killgrey' in sys.argv:
        mn=a.mean(2); hole=hole|((sat<28)&(mn>50)&(mn<200))
    alpha=np.where(vis|hole,0,255).astype(np.uint8)
    al=Image.fromarray(alpha).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    out=crop.convert('RGBA'); out.putalpha(al)
    if '--fitbbox' in sys.argv:
        nb=out.getchannel('A').point(lambda v:255 if v>127 else 0).getbbox(); ob=orig.getchannel('A').point(lambda v:255 if v>127 else 0).getbbox()
        piece=out.crop(nb).resize((ob[2]-ob[0],ob[3]-ob[1]),Image.LANCZOS)
        out=Image.new('RGBA',out.size,(0,0,0,0)); out.paste(piece,(ob[0],ob[1]))
        al=out.getchannel('A')
    na=np.asarray(al)>127
    iou=(na&oa).sum()/max(1,(na|oa).sum())
else:
    if '--trimgrey' in sys.argv:
        a=np.asarray(crop).astype(int); grey=((a.max(2)-a.min(2))<22)&(a.mean(2)>85)
        rows=np.where(grey.mean(1)<0.6)[0]; cols=np.where(grey.mean(0)<0.6)[0]
        crop=crop.crop((cols.min(),rows.min(),cols.max()+1,rows.max()+1)).resize((m['w'],m['h']),Image.LANCZOS)
    if '--greyfill' in sys.argv:
        a=np.asarray(crop).astype(int); g=((a.max(2)-a.min(2))<22)&(a.mean(2)>58)
        H,W=g.shape; mask=np.zeros((H,W),bool); q=deque()
        for x in range(W):
            for y in (0,H-1):
                if g[y,x] and not mask[y,x]: mask[y,x]=1; q.append((y,x))
        for y in range(H):
            for x in (0,W-1):
                if g[y,x] and not mask[y,x]: mask[y,x]=1; q.append((y,x))
        while q:
            y,x=q.popleft()
            for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
                ny,nx=y+dy,x+dx
                if 0<=ny<H and 0<=nx<W and not mask[ny,nx] and g[ny,nx]: mask[ny,nx]=1; q.append((ny,nx))
        a[mask]=[30,26,22]; crop=Image.fromarray(a.astype(np.uint8))
    out=crop.convert('RGBA'); iou=1.0
out.save(f'{WORK}/{key}_new.png')
def edges(img):
    t=Image.new('RGBA',img.size,(128,128,128,255)); t.alpha_composite(img)
    t=t.convert('L').resize((max(8,img.size[0]//4),max(8,img.size[1]//4)),Image.LANCZOS).filter(ImageFilter.FIND_EDGES).filter(ImageFilter.GaussianBlur(3))
    v=np.asarray(t).astype(float).ravel(); v-=v.mean(); return v/(np.linalg.norm(v)+1e-9)
edge=float((edges(orig)*edges(out)).sum())
# 對照縮圖
S=600; pv=Image.new('RGB',(S*2+10,S),(40,40,40))
for i,img in enumerate((orig,out)):
    t=img.copy(); k=min(S/t.size[0],S/t.size[1]); t=t.resize((max(1,round(t.size[0]*k)),max(1,round(t.size[1]*k))),Image.LANCZOS); bgc=Image.new('RGBA',t.size,(40,40,40,255)); bgc.alpha_composite(t); pv.paste(bgc.convert('RGB'),(i*(S+10),0))
pv.save(f'{WORK}/{key}_cmp.png')
ok=iou>=miniou
print(f'{key}: EDGE={edge:.3f} IoU={iou:.3f} {"PASS" if ok else "FAIL"}')
if apply and ok:
    b=f'{BK}/{key}.png'
    if not os.path.exists(b) and os.path.exists(UI+m['rel']): shutil.copyfile(UI+m['rel'],b)
    shutil.copyfile(f'{WORK}/{key}_new.png',UI+m['rel'])
    print('APPLIED ->',m['rel'])
