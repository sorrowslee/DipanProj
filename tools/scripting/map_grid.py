# -*- coding: utf-8 -*-
"""
把一張 .dipanmap 印成文字格子圖——AI 看不到編輯器畫面，擺出生點／演員走位／踩點區前先看這張。

    python3 tools/scripting/map_grid.py BloodFang BloodFang_MainLobby
    python3 tools/scripting/map_grid.py path/to/X.dipanmap

圖例：# 不可走　. 可走　T 傳送點　N NPC　S 玩家出生點　其餘＝該格觸發型別的首字大寫（M=monsterSpawn、C=camZone…）
      O 地上物擋住大半格　o 地上物擋住一角　! 出生點有問題（不可走，或中心離地上物碰撞不到 0.5 格；飛行怪不檢查）
      地上物碰撞照遊戲端算（PROBLEMS B9），可走層上看不出來；需要 Pillow，沒裝就只畫可走層。
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
try:
    blk = object_blockers(m)
except ImportError:
    blk = None; print('⚠ 沒有 Pillow，地上物不畫（pip install pillow）')
marks = {}
bad = []
# 飛行怪（MonsterData BrainType＝Flying／Bat，同 MonsterController 判 IsFlyer）只被牆擋、蝙蝠還會倒吊在上方，
# 生在牆上或家具上是刻意的 ⇒ 不檢查。monsterId 有多種（16|17）時全部會飛才略過。
_brain = {r[0].strip(): r[3].strip() for r in csv_rows('MonsterData.csv')[1:] if len(r) > 3}
def _flyer(mid): return all(_brain.get(i.strip()) in ('Flying', 'Bat') for i in str(mid).split('|'))
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
        for c in r['cells']:
            if r['typeId'] == 'monsterSpawn' and not _flyer(r['params'].get('monsterId', '')):
                x, y = c
                inside = 0 <= x < W and 0 <= y < H
                d = obj_clearance(m, blk, x, y) if blk is not None else 99.0
                if not inside or not ok[y][x] or d < 0.5:
                    ch_ = '!'
                    bad.append(f'{r["name"]} {c} ' + ('不可走' if not inside or not ok[y][x] else f'離地上物 {d:.2f} 格'))
                    marks[tuple(c)] = ch_
                    continue
            marks.setdefault(tuple(c), ch)
for n in m.get('npcs') or []:
    c = cell_of(m, n['x'], n['y']); print(f'N  {n["name"]}  世界({n["x"]},{n["y"]})  格{c}'); marks[c] = 'N'
print(f'{W}x{H}  walkSubdiv {m.get("walkSubdiv", 1)}  origin({m["origin"]["x"]},{m["origin"]["y"]})  tileSize {m["tileSize"]}')
print('    ' + ''.join(str(x // 10) if x % 10 == 0 else ' ' for x in range(W)))
print('    ' + ''.join(str(x % 10) for x in range(W)))
def cell_ch(x, y):
    if (x, y) in marks: return marks[(x, y)]
    if not ok[y][x]: return '#'
    if blk:
        n = obj_cover(m, blk, x, y)
        if n >= 8: return 'O'
        if n > 0: return 'o'
    return '.'
for y in range(H):
    print(f'{y:3d} ' + ''.join(cell_ch(x, y) for x in range(W)))
if blk is not None:
    print(f'地上物碰撞條 {len(blk)} 條')
    print('⚠ 出生點有問題：\n  ' + '\n  '.join(bad) if bad else '出生點檢查：全部 OK（可走、離地上物 ≥0.5 格）')
