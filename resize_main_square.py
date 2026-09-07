#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
一次性轉檔：Main_Square.dipanmap 由 90x50 格改為 48x36 格。

為什麼是這兩組數字：背景 Stage_Square.png 是 4:3（1448x1086、現為 2896x2172），
原本卻被拉伸貼到 90:50（1.8:1）的畫布 → 背景在遊戲裡被橫向拉寬 35%。
36 x (1448/1086) = 48.0，所以 48x36 = 「把背景還原成原始比例」。

因此這不是等比縮放，兩軸各自的倍率是：
    SX = 48/90 = 0.533333...   （世界 X 與格 X）
    SY = 36/50 = 0.72          （世界 Y 與格 Y）

地上物尺寸（scale）依作者拍板統一 x0.72（跟縱向對齊；俯視角下站立物的大小感看縱向）。

各類資料的處理方式：
  objects / lights / npcs(含 waypoints) / sceneFx  → 世界座標直接乘，尺寸乘 SCALE_MUL
  trigger regions.cells                            → 用「舊格中心映射後 floor」再去重
                                                     （不是相交即納入：那會讓三座祭壇的
                                                      區域互相重疊、一格觸發兩個面板）
  walkable.blocked                                 → 面積多數決重取樣（無偏，不偏袒牆或可走）
"""
import json, sys, os, collections

OLD_W, OLD_H = 90, 50
NEW_W, NEW_H = 48, 36
SX = NEW_W / OLD_W          # 0.533333...
SY = NEW_H / OLD_H          # 0.72
SCALE_MUL = SY              # 地上物 / 光源半徑 / 特效大小 的統一縮放

# 世界座標：x 往右為正、y 往上為正（地圖在 y<0 側）。兩者都只是乘倍率。
def mx(v): return round(v * SX, 6)
def my(v): return round(v * SY, 6)
def ms(v): return round(v * SCALE_MUL, 6)


def convert_objects(objs, log):
    for o in objs:
        o["x"] = mx(o["x"])
        o["y"] = my(o["y"])
        # sortKey 在本專案恆等於 y（實測 308/308），跟著走
        if "sortKey" in o:
            o["sortKey"] = o["y"]
        for k in ("scaleX", "scaleY"):
            if k in o and o[k]:
                o[k] = ms(o[k])
        # 綁在地上物上的光源半徑也是世界單位
        if o.get("lightRadius"):
            o["lightRadius"] = ms(o["lightRadius"])
    log.append(f"objects: {len(objs)} 筆座標與尺寸已映射")


def convert_cells(cells):
    """舊格 -> 新格：取舊格中心映射後 floor，再去重、排序。

    刻意不用「相交即納入」：一個舊格橫向只有 0.533 格寬，相交法會讓相鄰區域
    共用邊界格 —— 三座祭壇 (x33~35 / 36~38 / 39~41) 會重疊，玩家站一格可能
    同時觸發兩個 gacha 面板。中心點法算出來三區是 {17,18} / {19,20} / {21,22}，
    彼此不沾邊。
    """
    if not cells:
        return cells
    out = set()
    for c in cells:
        nx = int((c[0] + 0.5) * SX)
        ny = int((c[1] + 0.5) * SY)
        nx = max(0, min(NEW_W - 1, nx))
        ny = max(0, min(NEW_H - 1, ny))
        out.add((nx, ny))
    return [[x, y] for x, y in sorted(out)]


def convert_regions(regions, log):
    for r in regions:
        before = len(r.get("cells") or [])
        r["cells"] = convert_cells(r.get("cells") or [])
        after = len(r["cells"])
        p = r.get("params") or {}
        # 傳送點錨點（點模式）：位置照兩軸、踩踏矩形寬照 X 高照 Y
        # cameraFocus 的聚焦錨點 focusX/focusY 同理（2026-09-07 新增，見 TriggerChain.TryFocusAnchor）
        for key in ("markerX", "markerY", "markerW", "markerH", "focusX", "focusY"):
            if key in p and p[key] not in ("", None):
                mul = SX if key in ("markerX", "markerW", "focusX") else SY
                p[key] = round(float(p[key]) * mul, 6)
        # camZone 的位移是世界單位（值以字串存）
        for key in ("offsetX", "offsetY"):
            if key in p and str(p[key]).strip() != "":
                mul = SX if key == "offsetX" else SY
                p[key] = str(round(float(p[key]) * mul, 4))
        log.append(f'  {r.get("typeId"):13s} "{r.get("name")}"  cells {before} -> {after}')


def convert_lights(lights, log):
    for li in lights:
        li["x"] = mx(li["x"])
        li["y"] = my(li["y"])
        if li.get("radius"):
            li["radius"] = ms(li["radius"])
    log.append(f"lights: {len(lights)} 盞已映射（radius x{SCALE_MUL}）")


def convert_npcs(npcs, log):
    for n in npcs:
        n["x"] = mx(n["x"])
        n["y"] = my(n["y"])
        for wp in n.get("waypoints") or []:
            wp["x"] = mx(wp["x"])
            wp["y"] = my(wp["y"])
    log.append(f"npcs: {len(npcs)} 個已映射（含巡邏點）")


def convert_scenefx(fxs, log):
    for f in fxs:
        f["startX"] = mx(f["startX"]); f["startY"] = my(f["startY"])
        if "endX" in f: f["endX"] = mx(f["endX"])
        if "endY" in f: f["endY"] = my(f["endY"])
        # w/h/bulge 是特效自身的尺寸，統一用 SCALE_MUL 免得粒子被拉扁
        for k in ("w", "h", "bulge"):
            if k in f and f[k]:
                f[k] = ms(f[k])
    log.append(f"sceneFx: {len(fxs)} 個已映射（w/h/bulge x{SCALE_MUL}）")


def resample_walkable(rows, subdiv, log):
    """三態位元圖重取樣：每個新子格對它覆蓋到的舊子格做面積多數決。

    無偏（不偏袒牆也不偏袒可走）：偏向牆會做出隱形牆（PROBLEMS B9 的經典症狀），
    偏向可走會讓玩家走進背景牆裡。平手時取覆蓋中心點的舊值。
    """
    old_fw, old_fh = OLD_W * subdiv, OLD_H * subdiv
    new_fw, new_fh = NEW_W * subdiv, NEW_H * subdiv
    assert len(rows) == old_fh and len(rows[0]) == old_fw, "舊可走層尺寸與 width/height 對不上"

    out = []
    for ny in range(new_fh):
        y0 = ny * old_fh / new_fh
        y1 = (ny + 1) * old_fh / new_fh
        line = []
        for nx in range(new_fw):
            x0 = nx * old_fw / new_fw
            x1 = (nx + 1) * old_fw / new_fw
            vote = collections.Counter()
            for oy in range(int(y0), min(old_fh, int(y1 - 1e-9) + 1)):
                ov = min(y1, oy + 1) - max(y0, oy)
                if ov <= 0: continue
                for ox in range(int(x0), min(old_fw, int(x1 - 1e-9) + 1)):
                    oh = min(x1, ox + 1) - max(x0, ox)
                    if oh <= 0: continue
                    vote[rows[oy][ox]] += ov * oh
            cy = min(old_fh - 1, int((y0 + y1) / 2))
            cx = min(old_fw - 1, int((x0 + x1) / 2))
            if not vote:
                line.append(rows[cy][cx])
            else:
                top = max(vote.values())
                tied = [k for k, v in vote.items() if abs(v - top) < 1e-9]
                line.append(tied[0] if len(tied) == 1 else rows[cy][cx])
        out.append("".join(line))
    log.append(f"walkable: 子格 {old_fw}x{old_fh} -> {new_fw}x{new_fh}（面積多數決）")
    return out


def main(src, dst):
    with open(src, encoding="utf-8") as f:
        d = json.load(f)

    assert d["width"] == OLD_W and d["height"] == OLD_H, \
        f'來源不是 {OLD_W}x{OLD_H}（實際 {d["width"]}x{d["height"]}），可能已經轉過了'

    log = []
    layers = {l["type"]: l for l in d["layers"]}

    convert_objects(layers["Game"].get("objects") or [], log)

    log.append("trigger regions:")
    convert_regions(layers["Trigger"].get("regions") or [], log)

    layers["Walkable"]["blocked"] = resample_walkable(
        layers["Walkable"]["blocked"], d.get("walkSubdiv", 1), log)

    convert_lights(d.get("lights") or [], log)
    convert_npcs(d.get("npcs") or [], log)
    convert_scenefx(d.get("sceneFx") or [], log)

    d["width"], d["height"] = NEW_W, NEW_H

    with open(dst, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=2)

    print(f"來源 {src}")
    print(f"輸出 {dst}")
    print(f"畫布 {OLD_W}x{OLD_H} -> {NEW_W}x{NEW_H}   SX={SX:.6f} SY={SY:.6f} scale x{SCALE_MUL}")
    for l in log:
        print(" ", l)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
