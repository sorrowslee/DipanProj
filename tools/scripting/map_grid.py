# -*- coding: utf-8 -*-
"""
把一張 .dipanmap 印成文字格子圖——AI 看不到編輯器畫面，擺出生點／演員走位／踩點區前先看這張。

    python3 tools/scripting/map_grid.py BloodFang BloodFang_MainLobby
    python3 tools/scripting/map_grid.py path/to/X.dipanmap

圖例：# 不可走　. 可走　T 傳送點　N NPC　S 玩家出生點　其餘＝該格觸發型別的首字大寫（M=monsterSpawn、C=camZone…）
格子座標 (x, y)：x 往右、y 往下；世界座標換算用 dipan.center(m, x, y)／cell_of(m, wx, wy)。
"""
import sys, os
sys.dont_write_bytecode = True   # 不在 repo 裡留 __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dipan import *

if len(sys.argv) == 3: p = map_path(sys.argv[1], sys.argv[2])
elif len(sys.argv) == 2: p = sys.argv[1]
else: print(__doc__); sys.exit(1)

m = load(p); W, H, ok = grid(m)
marks = {}
for r in trig(m):
    q = r['params']
    if r['typeId'] == 'teleport':
        if 'markerX' in q:
            c = cell_of(m, q['markerX'], q['markerY'])
        else:
            xs = [c[0] for c in r['cells']]; ys = [c[1] for c in r['cells']]
            c = (round(sum(xs) / len(xs)), round(sum(ys) / len(ys))) if xs else None
        print(f'T  {r["name"]}  格{r["cells"]}  標記格{c}  {"初始停用" if q.get("startDisabled") is True else ""}')
        if c: marks[tuple(c)] = 'T'
    elif r['cells']:
        ch = 'S' if r['typeId'] == 'playerSpawn' else r['typeId'][0].upper()
        print(f'{ch}  {r["name"]} [{r["typeId"]}]  {len(r["cells"])}格  {r["cells"][:8]}{" …" if len(r["cells"]) > 8 else ""}')
        for c in r['cells']: marks.setdefault(tuple(c), ch)
for n in m.get('npcs') or []:
    c = cell_of(m, n['x'], n['y']); print(f'N  {n["name"]}  世界({n["x"]},{n["y"]})  格{c}'); marks[c] = 'N'
print(f'{W}x{H}  walkSubdiv {m.get("walkSubdiv", 1)}  origin({m["origin"]["x"]},{m["origin"]["y"]})  tileSize {m["tileSize"]}')
print('    ' + ''.join(str(x // 10) if x % 10 == 0 else ' ' for x in range(W)))
print('    ' + ''.join(str(x % 10) for x in range(W)))
for y in range(H):
    print(f'{y:3d} ' + ''.join(marks.get((x, y), '.' if ok[y][x] else '#') for x in range(W)))
