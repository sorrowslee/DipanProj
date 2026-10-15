# -*- coding: utf-8 -*-
"""
劇本地圖驗證（AI 每次改完 .dipanmap 必跑；說明書 readme/SCRIPTING_GUIDE.md §5）。

    python3 tools/scripting/validate_map.py BloodFang              # 驗整個模組（含跨圖檢查）
    python3 tools/scripting/validate_map.py BloodFang --chain      # 另外印出每張圖的觸發鏈
    python3 tools/scripting/validate_map.py path/to/X.dipanmap ... # 只驗指定檔

✗＝錯誤（遊戲裡一定壞）；△＝警告（多半是坑，確認是刻意的就可以留）。結束碼＝錯誤數。
"""
import sys, os, re
sys.dont_write_bytecode = True   # 不在 repo 裡留 __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dipan import *

args = [a for a in sys.argv[1:] if not a.startswith('--')]
SHOW_CHAIN = '--chain' in sys.argv
files = []
for a in args:
    files += [a] if a.endswith('.dipanmap') else module_maps(a)
if not files:
    print(__doc__); sys.exit(1)

FLAGS = flags()
TYPES = trigger_types()
REFKEYS = trigger_ref_keys()
DRAMA = {r[0].strip(): r for r in csv_rows('DramaTable.csv')[1:]}
TALK_GROUPS = {r[1].strip() for r in csv_rows('DramaTalkTable.csv')[1:] if len(r) > 1}
LANGS = csv_ids('LanguageTable.csv')
MONS = csv_ids('MonsterData.csv')
ITEMS = csv_ids('ItemTable.csv')
MAPTAB = maps_table()
SELF_FIRING = {'onEnter', 'watchFlag', 'teleport', 'playerSpawn', 'monsterSpawn'}

errs = warns = 0
def E(*a):
    global errs; errs += 1; print('  ✗', *a)
def W(*a):
    global warns; warns += 1; print('  △', *a)

_entr_cache = {}
def entrances_of(map_id):
    if map_id in _entr_cache: return _entr_cache[map_id]
    name, module = MAPTAB.get(str(map_id), (None, None))
    p = map_path(module, name) if name else None
    s = None
    if p and os.path.exists(p):
        s = {r['params'].get('entranceId') for r in trig(load(p)) if r['typeId'] == 'teleport'}
    _entr_cache[map_id] = s
    return s

flag_reads, flag_writes = {}, set()
def read_flag(v, where):
    if not v: return
    n = v.strip().lstrip('!')
    if n not in FLAGS: E(where, '旗標未登記（flags.json）', n)
    flag_reads.setdefault(n, where)
def write_flag(v, where):
    if not v: return
    n = v.strip().lstrip('!')
    if n not in FLAGS: E(where, '旗標未登記（flags.json）', n)
    flag_writes.add(n)
def cond(c, where):
    for part in (c or '').split('|'):
        part = part.strip().lstrip('!')
        if part.startswith('flag:'): read_flag(part[5:], where)

for f in files:
    m = load(f); W_, H_, ok = grid(m); rs = trig(m)
    names = [r['name'] for r in rs]
    print(f'## {os.path.basename(f)}  {len(rs)} 觸發／{len(m.get("npcs") or [])} NPC／{len(m.get("cutscenes") or [])} 演出')
    dup = sorted({n for n in names if names.count(n) > 1})
    if dup: E('觸發點重名（鏈靠名稱接，重名會接錯）', dup)
    nm = {r['name']: r for r in rs}
    csids = {(c.get('id') or '').lower() for c in m.get('cutscenes') or []}
    edges, referenced, opened = {}, set(), set()

    for r in rs:
        p, t, n = r['params'], r['typeId'], r['name']
        if t not in TYPES: E(n, 'typeId 不存在', t)
        outs = [p[k] for k in REFKEYS if p.get(k)]
        for o in outs:
            if o not in nm: E(n, '接續找不到', o)
            referenced.add(o)
        edges[n] = outs
        for k in ('requireFlag', 'fireOnFlag', 'enableFlag'): read_flag(p.get(k), f'{n}.{k}')
        for k in ('setFlag', 'deathFlag'): write_flag(p.get(k), f'{n}.{k}')
        cond(p.get('conditions'), n)
        for c in r['cells']:
            x, y = c
            if not (0 <= x < W_ and 0 <= y < H_): E(n, '格子超出地圖', c)

        if t == 'togglePortal':
            for tg in (p.get('target') or '').split(','):
                tg = tg.strip(); hit = nm.get(tg)
                if not hit or hit['typeId'] != 'teleport': E(n, 'target 不是本圖的傳送點', tg)
                elif p.get('show') is True: opened.add(tg)
        if t == 'teleport':
            tid, te = p.get('targetMapId'), p.get('targetEntrance')
            if tid and str(tid) not in MAPTAB: E(n, 'targetMapId 不在 MapsTable', tid)
            elif tid and te:
                ents = entrances_of(tid)
                if ents is not None and te not in ents: E(n, 'targetEntrance 在目標圖找不到（拼字？）', te)
        if t == 'playCutscene' and p.get('cutsceneId') and p['cutsceneId'].lower() not in csids:
            E(n, 'cutsceneId 本圖沒有這段演出', p['cutsceneId'])
        if t in ('monsterSpawn', 'bossIntro', 'companion') and p.get('monsterId'):
            for mid in str(p['monsterId']).split('|'):
                if mid.strip() not in MONS: E(n, 'monsterId 不存在', mid)
        if t == 'monsterSpawn':
            if p.get('startDisabled') is True and (p.get('requireFlag') or p.get('conditions')):
                E(n, '「初始停用」＋條件旗標互斥：初始停用是一次性解鎖，沒有鏈去解鎖就永遠不生')
            if not r['cells']: W(n, '出生點 0 格＝一隻都不生（一格一隻）')
        if t == 'giveItem' and str(p.get('itemId')) not in ITEMS: E(n, 'itemId 不存在', p.get('itemId'))
        if t == 'drama':
            d = DRAMA.get(str(p.get('dramaId')))
            if not d: E(n, 'dramaId 不在 DramaTable', p.get('dramaId'))
            elif len(d) > 4 and d[3].strip() == '2' and d[4].strip() not in TALK_GROUPS:
                E(n, f'對話型 drama 的 TalkGroup {d[4]} 在 DramaTalkTable 找不到')
        if t == 'timedChoice':
            for k in ('textId', 'subTextId', 'optionA', 'optionB'):
                if p.get(k) and str(p[k]) not in LANGS: E(n, k, '語言 id 不存在', p[k])
            if p.get('next'): W(n, 'timedChoice 不吃通用 next（遊戲端會忽略），用 nextA/nextB/nextTimeout')
        if t == 'watchFlag' and not p.get('fireOnFlag'): E(n, 'watchFlag 沒填 fireOnFlag，永遠不會觸發')
        if t == 'clearLevel' and r['cells']: W(n, 'clearLevel 塗格子不會踩點過關（PROBLEMS B18），要用 camZone/onEnter 接過來')

    for n in m.get('npcs') or []:
        if n.get('next'):
            if n['next'] not in nm: E('NPC', n['name'], 'next 找不到', n['next'])
            referenced.add(n['next'])
        read_flag(n.get('disappearFlag'), 'NPC ' + n['name']); cond(n.get('conditions'), 'NPC ' + n['name'])

    # 波次群組：群組裡有人接 next、但全都是「生出來時」⇒ 全滅時沒有人推鏈（21 血蝠的坑）
    groups = {}
    for r in rs:
        if r['typeId'] == 'monsterSpawn' and r['params'].get('waveGroup'):
            groups.setdefault(r['params']['waveGroup'], []).append(r)
    for g, lst in groups.items():
        nx = [r for r in lst if r['params'].get('next')]
        if nx and all((r['params'].get('nextWhen') or '').strip() == '生出來時' for r in nx):
            W(f'波次群組「{g}」', '接 next 的出生點全是「生出來時」⇒ 生成當下就推鏈、全滅時沒人推；清場開門要留一顆「全滅後」')

    # 傳送點：初始停用卻沒人開 ⇒ 永遠是關的
    for r in rs:
        if r['typeId'] == 'teleport' and r['params'].get('startDisabled') is True \
                and r['name'] not in opened and not r['params'].get('enableFlag'):
            W(r['name'], '初始停用，本圖沒有 togglePortal(show) 開它、也沒有 enableFlag ⇒ 永遠關著（回頭門刻意的就忽略）')

    # 孤兒：不是自己會觸發的型別、沒塗格子、也沒人接它 ⇒ 永遠不會跑
    for r in rs:
        if r['name'] not in referenced and not r['cells'] and r['typeId'] not in SELF_FIRING:
            W(r['name'], f'孤兒節點（{r["typeId"]}，0 格、沒有人接它）⇒ 永遠不會觸發')

    # 環
    seen_cycles = set()
    def dfs(u, stack):
        if u in stack:
            cyc = tuple(stack[stack.index(u):])
            if frozenset(cyc) not in seen_cycles:
                seen_cycles.add(frozenset(cyc)); E('觸發鏈成環', ' → '.join(cyc + (u,)))
            return
        for v in edges.get(u, []): dfs(v, stack + [u])
    for u in edges: dfs(u, [])

    # 演出
    for c in m.get('cutscenes') or []:
        ids = {a['id'] for a in c['actors']}
        write_flag(c.get('setFlag'), 'cutscene ' + c['id']); read_flag(c.get('requireFlag'), 'cutscene ' + c['id'])
        for a in c['actors']:
            if a.get('kind') == 'npc' and not sprite_dir(a['spriteFolder']): E(c['id'], 'spriteFolder 找不到序列圖', a['spriteFolder'])
        cam_pos = False
        for i, s in enumerate(c['steps']):
            if s['actorId'] and s['actorId'] not in ids: E(c['id'], f'第{i}步 actorId 不存在', s['actorId'])
            if s['type'] == 'move' and s['hasPos']:
                cx, cy = cell_of(m, s['x'], s['y'])
                if not (0 <= cx < W_ and 0 <= cy < H_ and ok[cy][cx]): E(c['id'], f'第{i}步 move 目標不可走', (s['x'], s['y']), (cx, cy))
            if s['type'] == 'bubble' and str(s['langId']) not in LANGS: E(c['id'], f'第{i}步 bubble langId 不存在', s['langId'])
            if s['type'] == 'drama' and str(s['dramaId']) not in DRAMA: E(c['id'], f'第{i}步 dramaId 不存在', s['dramaId'])
            if s['type'] == 'setFlag': write_flag(s['flag'], c['id'])
            if s['type'] == 'camera' and s['hasPos'] and abs(s['zoom'] - 1) > 1e-6:
                W(c['id'], f'第{i}步 camera 有座標時 zoom 不生效')

    if SHOW_CHAIN:
        for r in rs:
            if r['typeId'] in ('teleport', 'playerSpawn'): continue
            p = r['params']
            tag = ' '.join(f'{k}={p[k]}' for k in ('requireFlag', 'conditions', 'fireOnFlag', 'setFlag', 'cutsceneId',
                                                     'dramaId', 'target', 'show', 'monsterId', 'waveGroup', 'onBlocked', 'repeat')
                           if p.get(k) not in (None, ''))
            print(f'   {r["name"]} [{r["typeId"]}{"·" + str(len(r["cells"])) + "格" if r["cells"] else ""}] → {", ".join(edges[r["name"]]) or "-"}  {tag}')

# 跨圖：旗標只有人讀、沒有人寫（只在驗整個模組時有意義）
if len(files) > 1:
    for n, where in sorted(flag_reads.items()):
        if n not in flag_writes:
            W(f'旗標 {n} 被讀（例：{where}），但這批地圖裡沒有任何地方寫它（別的模組／程式寫的就忽略）')

print(f'錯誤 {errs}　警告 {warns}')
sys.exit(errs)
