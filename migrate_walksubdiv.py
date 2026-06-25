#!/usr/bin/env python3
"""
一次性轉檔：把舊 .dipanmap（可走層 = tile 解析度 2 態 '0'/'1'，牆由 environment trigger 標記）
升級為新模型（可走層 = 子格解析度三態：'0' 可走 / '1' 牆 / '2' 水）。

規則（完全沿用舊遊戲端 BuildCellColliders 的語意，行為不變、只是換成子格位元圖）：
  某 tile (x,y)：
    - 在 environment trigger 區域內            → '1' 牆
    - 否則 blocked=='1'（不可走但非環境）     → '2' 水/坑
    - 否則                                      → '0' 可走
  再把每個 tile 展開成 N×N 子格（同值）。最後移除 environment trigger 區域、寫入 walkSubdiv=N。
"""
import json, sys, os, collections

N = 4  # 細分倍率

def migrate_file(path):
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
    if d.get("walkSubdiv", 1) and d.get("walkSubdiv", 1) > 1:
        return "skip(已是新版)"

    w, h = d["width"], d["height"]
    layers = d["layers"]
    walk = next((L for L in layers if L.get("type") == "Walkable"), None)
    trig = next((L for L in layers if L.get("type") == "Trigger"), None)
    if walk is None:
        return "skip(無可走層)"

    blocked = walk.get("blocked") or []

    # 收集 environment trigger 格
    env = set()
    if trig and trig.get("regions"):
        for r in trig["regions"]:
            if r.get("typeId") == "environment":
                for c in r.get("cells", []):
                    if c and len(c) >= 2:
                        env.add((c[0], c[1]))

    def tile_state(x, y):
        if (x, y) in env:
            return '1'
        row = blocked[y] if y < len(blocked) else ""
        ch = row[x] if x < len(row) else '1'
        return '2' if ch == '1' else '0'

    # 建子格三態位元圖
    fine = []
    for y in range(h):
        for _ in range(N):
            chars = []
            for x in range(w):
                s = tile_state(x, y)
                chars.append(s * N)
            fine.append("".join(chars))
    walk["blocked"] = fine
    walk["name"] = "可走/牆/水"

    # 移除 environment 區域（資料已搬進可走層）
    removed = 0
    if trig and trig.get("regions"):
        before = len(trig["regions"])
        trig["regions"] = [r for r in trig["regions"] if r.get("typeId") != "environment"]
        removed = before - len(trig["regions"])

    # 插入 walkSubdiv（放在 height 之後，貼近編輯器類別欄位順序）
    out = collections.OrderedDict()
    for k, v in d.items():
        out[k] = v
        if k == "height":
            out["walkSubdiv"] = N
    if "walkSubdiv" not in out:
        out["walkSubdiv"] = N

    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
        f.write("\n")
    stats = collections.Counter("".join(fine))
    return f"migrated env_removed={removed} cells(walk/wall/water)={stats['0']}/{stats['1']}/{stats['2']}"

def main(roots):
    files = []
    for root in roots:
        for dp, _, fns in os.walk(root):
            if os.sep + "Builds" + os.sep in dp + os.sep:
                continue
            for fn in fns:
                if fn.endswith(".dipanmap"):
                    files.append(os.path.join(dp, fn))
    files.sort()
    for p in files:
        try:
            res = migrate_file(p)
        except Exception as e:
            res = f"ERROR {e}"
        rel = os.path.relpath(p)
        print(f"{res:60s} {rel}")
    print(f"\n共 {len(files)} 個檔案。")

if __name__ == "__main__":
    main(sys.argv[1:])
