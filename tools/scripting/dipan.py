# -*- coding: utf-8 -*-
"""
燃燈劫劇本編排共用函式庫（AI 直接編 .dipanmap 用）。說明書：readme/SCRIPTING_GUIDE.md

用法（在任何腳本開頭）：
    import sys; sys.dont_write_bytecode = True; sys.path.insert(0, r"<repo>/tools/scripting"); from dipan import *
    m = load(map_path('BloodFang', 'BloodFang_MainLobby'))
    add(m, '對話-xx', 'drama', {'dramaId': '61', 'next': '開東門'})
    save(map_path('BloodFang', 'BloodFang_MainLobby'), m)      # 存完務必跑 validate_map.py

原則：
  ‧ 只透過這裡的 load/save 讀寫——save 保留 2 格縮排與檔尾換行，跟編輯器存出來的格式一致，diff 才乾淨。
  ‧ params 多數是「字串」（'15'、'5'），只有 startDisabled/show/pause/fullDamage 這類勾選是 JSON bool、
    markerX/Y、focusX/Y 是數字。用錯型別編輯器讀得進來但遊戲端判斷會錯，add() 不幫你轉，自己注意。
  ‧ 動作型（鏈動作）節點 cells 一律 []；要玩家踩才觸發的才塗格子。
"""
import json, os, csv, glob, secrets

# ───────────────────────── 路徑 ─────────────────────────
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
MAIN = os.path.join(ROOT, 'DipanProj_Main')
EDITOR = os.path.join(ROOT, 'DipanProj_MapEditor')
DATA = os.path.join(MAIN, 'Assets', 'Data')
MAPS = os.path.join(EDITOR, 'Maps')


def map_path(module, name):
    """map_path('BloodFang', 'BloodFang_MainLobby') → 編輯器那份 .dipanmap（作者 Sync 後才進主遊戲）。"""
    return os.path.join(MAPS, module, name + '.dipanmap')


def module_maps(module):
    return sorted(glob.glob(os.path.join(MAPS, module, '*.dipanmap')))


# ───────────────────────── 讀寫 ─────────────────────────
def load(p):
    with open(p, encoding='utf-8') as f:
        return json.load(f)


def save(p, m):
    raw = open(p, encoding='utf-8').read() if os.path.exists(p) else ''
    s = json.dumps(m, ensure_ascii=False, indent=2)
    if raw.endswith('\n'):
        s += '\n'
    with open(p, 'w', encoding='utf-8') as f:
        f.write(s)
    load(p)  # 寫完再讀一次，確保是合法 JSON


def trig(m):
    """Trigger 圖層的 regions（觸發點清單）。"""
    return [L for L in m['layers'] if L['type'] == 'Trigger'][0]['regions']


def by_name(m):
    return {r['name']: r for r in trig(m)}


def _newid(m):
    ids = {r['id'] for r in trig(m)}
    while True:
        h = secrets.token_hex(4)
        if h not in ids:
            return h


def add(m, name, typeId, params, cells=None):
    """新增觸發點。名稱在同一張圖內必須唯一（鏈靠名稱接）。"""
    rs = trig(m)
    assert name not in [r['name'] for r in rs], '重名：' + name
    r = {"id": _newid(m), "name": name, "typeId": typeId, "cells": [list(c) for c in (cells or [])], "params": params}
    rs.append(r)
    return r


def remove(m, name):
    rs = trig(m)
    hit = [r for r in rs if r['name'] == name]
    assert hit, '找不到：' + name
    rs.remove(hit[0])


# ───────────────────────── 格子 ─────────────────────────
def grid(m):
    """回傳 (W, H, ok)：ok[y][x]＝這一格「整格可走」（walkSubdiv 細格全部可走才算）。"""
    W, H, S = m['width'], m['height'], m.get('walkSubdiv', 1)
    wl = [L for L in m['layers'] if L['type'] == 'Walkable'][0]['blocked']
    ok = [[all(wl[y * S + j][x * S + i] == '0' for i in range(S) for j in range(S)) for x in range(W)] for y in range(H)]
    return W, H, ok


def cell_of(m, wx, wy):
    """世界座標 → 格子 (x, y)。y 往下增加（世界 y 是負的）。"""
    ox, oy, ts = m['origin']['x'], m['origin']['y'], m['tileSize']
    return int((wx - ox) // ts), int((oy - wy) // ts)


def center(m, x, y):
    """格子 → 該格中心的世界座標（演員 x/y、companion x/y、move 目標用這個）。"""
    ox, oy, ts = m['origin']['x'], m['origin']['y'], m['tileSize']
    return (ox + (x + .5) * ts, oy - (y + .5) * ts)


def cells_ok(m, cells):
    """檢查每格都可走，回傳 list 版。出生點／踩點區一律先過這關。"""
    W, H, ok = grid(m)
    for x, y in cells:
        assert 0 <= x < W and 0 <= y < H and ok[y][x], ('不可走', x, y)
    return [[x, y] for x, y in cells]


def mirror_x(m, cells):
    """左右鏡像（兩族對稱佈置用）。"""
    return [[m['width'] - 1 - x, y] for x, y in cells]


def spawn(m, name, monsterId, cells, **params):
    """怪物出生點。cells 一格一隻；params 會轉成字串（bool 保留）。"""
    p = {"monsterId": str(monsterId)}
    for k, v in params.items():
        p[k] = v if isinstance(v, bool) else str(v)
    return add(m, name, 'monsterSpawn', p, cells_ok(m, cells))


# ───────────────────────── 地上物碰撞 ─────────────────────────
# 可走層只管牆/水；地上物擋路靠的是自己身上的碰撞（PROBLEMS B9），map_grid 不算這個就會把家具當空地。
# 這裡照遊戲端 MapLoader.BuildObjectCollision 算：catalog 烘好的 footprint 遮罩（沒有就照
# ObjectFootprint.Scan 當場掃）× 擺放的縮放／翻轉／旋轉 × 位置；遮罩近實心（填滿率 ≥0.9）走 alpha 外接框。
# 需要 Pillow。walkable／passThrough 的物件不擋路、略過；有出現條件／旗標的一律當作會出現（保守）。
_CATALOG = None
_ASSET_ROOTS = [os.path.join(MAIN, 'Assets', 'StreamingAssets', 'MapAssets'),
                os.path.join(EDITOR, 'Assets', 'StreamingAssets', 'MapAssets')]


def _catalog():
    global _CATALOG
    if _CATALOG is None:
        _CATALOG = {}
        for root in reversed(_ASSET_ROOTS):   # 主遊戲那份（Sync 產的、有 footprint）優先，蓋過編輯器那份
            p = os.path.join(root, 'catalog.json')
            if os.path.exists(p):
                for it in load(p)['items']:
                    if root == _ASSET_ROOTS[0] or it['id'] not in _CATALOG:
                        _CATALOG[it['id']] = it
    return _CATALOG


def _scan_footprint(im, subdiv=8, coverage=0.25):
    """ObjectFootprint.Scan 的 Python 版（catalog 沒烘到時的退路）。"""
    cell = 256 // subdiv; w, h = im.size; a = im.getchannel('A').load()
    cols, rows = -(-w // cell), -(-h // cell); need = max(1, round(cell * cell * coverage)); out = []
    for cy in range(rows):
        row = ''
        for cx in range(cols):
            n = 0
            for y in range(cy * cell, min(h, cy * cell + cell)):
                for x in range(cx * cell, min(w, cx * cell + cell)):
                    if a[x, y] > 10:
                        n += 1
                if n >= need:
                    break
            row += '1' if n >= need else '0'
        out.append(row)
    return {'subdiv': subdiv, 'cols': cols, 'rows': rows, 'cells': out}


def object_blockers(m):
    """回傳 [(x0, y0, x1, y1, assetId), ...]：每個擋路地上物碰撞條的世界座標外接框。找不到素材的會印警告。"""
    from PIL import Image
    import math
    cat, ts = _catalog(), m['tileSize']
    ppu = 256 / ts
    out = []
    for L in m['layers']:
        if L['type'] != 'Game':
            continue
        for o in L.get('objects', []):
            if o.get('walkable') or o.get('passThrough'):
                continue
            it = cat.get(o['assetId'])
            src = it and next((os.path.join(r, it['path']) for r in _ASSET_ROOTS
                               if os.path.exists(os.path.join(r, it['path']))), None)
            if not src:
                print('⚠ 找不到地上物素材（沒 Sync？），不計入碰撞：' + o['assetId'])
                continue
            im = Image.open(src).convert('RGBA')
            cw, ch = im.size[0] / ppu, im.size[1] / ppu
            fp = it.get('footprint') or _scan_footprint(im)
            cs = ts / fp['subdiv']
            sol = [(x, y) for y, r in enumerate(fp['cells']) for x, c in enumerate(r) if c == '1']
            loc = []
            if sol:
                xs = [x for x, _ in sol]; ys = [y for _, y in sol]
                fill = len(sol) / ((max(xs) - min(xs) + 1) * (max(ys) - min(ys) + 1))
            if not sol or fill >= 0.9:   # 近實心 → 單一 alpha 外接框（同遊戲端）
                bb = im.getchannel('A').point(lambda v: 255 if v > 10 else 0).getbbox()
                if bb:
                    loc = [(-cw / 2 + bb[0] / ppu, ch / 2 - bb[1] / ppu, -cw / 2 + bb[2] / ppu, ch / 2 - bb[3] / ppu)]
            else:
                for x, y in sol:   # 最右欄／最下列要夾回畫布（PROBLEMS B9）
                    xa, xb, ya, yb = x * cs, min((x + 1) * cs, cw), y * cs, min((y + 1) * cs, ch)
                    if xb > xa and yb > ya:
                        loc.append((-cw / 2 + xa, ch / 2 - ya, -cw / 2 + xb, ch / 2 - yb))
            sx = (-1 if o.get('flipX') else 1) * o.get('scaleX', 1.0)
            sy = (-1 if o.get('flipY') else 1) * o.get('scaleY', 1.0)
            th = math.radians(o.get('rot', 0.0)); c, s = math.cos(th), math.sin(th)
            for l, t, r, b in loc:
                pts = [(o['x'] + px * sx * c - py * sy * s, o['y'] + px * sx * s + py * sy * c)
                       for px, py in ((l, t), (r, t), (r, b), (l, b))]
                out.append((min(p[0] for p in pts), min(p[1] for p in pts),
                            max(p[0] for p in pts), max(p[1] for p in pts), o['assetId']))
    return out


def obj_clearance(m, blockers, x, y):
    """格子 (x, y) 中心到最近地上物碰撞的距離（世界單位，0＝中心就在碰撞裡）。出生點建議 ≥0.5。"""
    import math
    wx, wy = center(m, x, y)
    return min([math.hypot(max(b[0] - wx, 0, wx - b[2]), max(b[1] - wy, 0, wy - b[3])) for b in blockers] or [99.0])


def obj_cover(m, blockers, x, y):
    """格子 (x, y) 內 4×4 取樣點有幾點落在地上物碰撞裡（0～16）。"""
    ox, oy, ts = m['origin']['x'], m['origin']['y'], m['tileSize']
    n = 0
    for i in range(4):
        for j in range(4):
            wx, wy = ox + (x + (i + .5) / 4) * ts, oy - (y + (j + .5) / 4) * ts
            n += any(b[0] <= wx <= b[2] and b[1] <= wy <= b[3] for b in blockers)
    return n


# ───────────────────────── 演出（cutscenes） ─────────────────────────
TARGET_VISIBLE_HEIGHT = 1.95   # 遊戲把一般角色可見高度對齊到 1.95 世界單位


def sprite_dir(folder):
    hits = glob.glob(os.path.join(MAIN, 'Assets', 'GameAssets', '*', 'Monsters', 'SequenceImage', folder)) + \
        glob.glob(os.path.join(MAIN, 'Assets', 'GameAssets', 'Modules', '*', 'Monsters', 'SequenceImage', folder)) + \
        glob.glob(os.path.join(MAIN, 'Assets', 'GameAssets', '*', 'Characters', 'SequenceImage', folder))
    return hits[0] if hits else None


def actor_scale(folder, unit_scale=1.0):
    """
    演員 scale（演員不走怪物的自動高度對齊，scale 是純 localScale、PPU 256）。
    要跟遊戲裡同一隻 NPC／怪一樣大：scale = 1.95 ÷ (idle 第一張可見像素高 ÷ 256) × NPC/怪物表的 Scale。
    用紅嫁衣作者手調的父親（3.0）驗算得 2.89，管家 1.81、血族士兵 3.70 均吻合。需要 Pillow。
    """
    from PIL import Image
    d = sprite_dir(folder)
    assert d, '找不到序列圖資料夾：' + folder
    ps = sorted(glob.glob(os.path.join(d, 'idle', '*.png')))
    assert ps, folder + ' 沒有 idle/*.png'
    bb = Image.open(ps[0]).getchannel('A').getbbox()
    return round(TARGET_VISIBLE_HEIGHT / ((bb[3] - bb[1]) / 256) * unit_scale, 2)


def actor(id, folder, x, y, facing='right', spawn=True, scale=None, unit_scale=1.0, flying=False, fps=8.0):
    return {"id": id, "kind": "npc", "spriteFolder": folder, "x": round(x, 3), "y": round(y, 3), "facing": facing,
            "spawnAtStart": spawn, "flying": flying,
            "scale": scale if scale is not None else actor_scale(folder, unit_scale), "animFps": fps}


def step(type, actorId='', x=None, y=None, facing='', dramaId=0, langId=0, seconds=0.0, speed=0.0, assetId='',
         zoom=1.0, flag='', par=False, bg=False):
    """
    type：move / face / wait / bubble / drama / camera / cameraFollow / spawn / despawn / setFlag / comic …
    bubble 預設擋住 seconds 秒，要邊走邊講 bg=True；par=True 與下一步同時開始。
    camera 有給 x/y 時 zoom 無效（MapCameraController 有焦點就強制 1）。
    """
    return {"type": type, "actorId": actorId, "hasPos": x is not None,
            "x": round(x, 3) if x is not None else 0.0, "y": round(y, 3) if y is not None else 0.0,
            "facing": facing, "dramaId": dramaId, "langId": langId, "seconds": float(seconds), "speed": float(speed),
            "assetId": assetId, "zoom": float(zoom), "flag": flag, "parallelNext": par, "background": bg}


def cutscene(id, actors, steps, lockInput=False, hideHud=False, setFlag='', requireFlag='', skippable=True,
             autoStart=False, hidePlayer=False):
    return {"id": id, "autoStartOnEnter": autoStart, "requireFlag": requireFlag, "setFlag": setFlag,
            "skippable": skippable, "lockInput": lockInput, "memoryFx": False, "hidePlayer": hidePlayer,
            "hideHud": hideHud, "actors": actors, "steps": steps}


def put_cutscene(m, cs):
    """新增或取代同 id 的演出段。"""
    lst = m.setdefault('cutscenes', [])
    for i, c in enumerate(lst):
        if c['id'] == cs['id']:
            lst[i] = cs
            return cs
    lst.append(cs)
    return cs


# ───────────────────────── 資料表 ─────────────────────────
def csv_rows(name):
    with open(os.path.join(DATA, name), encoding='utf-8-sig') as f:
        return [r for r in csv.reader(f) if r and not r[0].startswith('#')]


def csv_ids(name):
    return {r[0].strip() for r in csv_rows(name)[1:] if r and r[0].strip()}


def next_free_id(name, start, end=None):
    """在 start..end 之間找第一個沒用過的 id（語言表／對話表分段用）。"""
    used = csv_ids(name)
    i = start
    while str(i) in used:
        i += 1
    assert end is None or i <= end, f'{name} {start}-{end} 區段已滿'
    return i


def flags():
    """flags.json：{name: entry}。新旗標要加在這裡（編輯器與主遊戲共用的登記表）。"""
    return {f['name']: f for f in load(os.path.join(EDITOR, 'flags.json'))['flags']}


def trigger_types():
    return {t['typeId']: t for t in load(os.path.join(EDITOR, 'Assets', 'StreamingAssets', 'triggerTypes.json'))['types']}


def trigger_ref_keys():
    """會指向其他觸發點名稱的參數 key（next ＋ 型別宣告 isTriggerRef 的）。"""
    keys = {'next'}
    for t in trigger_types().values():
        for p in t.get('params', []):
            if p.get('isTriggerRef'):
                keys.add(p['key'])
    return keys


def maps_table():
    """MapsTable：{mapId: (name, module)}。"""
    return {r[0].strip(): (r[1].strip(), r[2].strip()) for r in csv_rows('MapsTable.csv')[1:] if r[0].strip().isdigit()}
