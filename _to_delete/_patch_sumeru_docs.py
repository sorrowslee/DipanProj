import sys, os
root = sys.argv[1]
def edit(rel, pairs):
    p = os.path.join(root, rel); s = open(p, encoding='utf-8').read()
    for old, new in pairs:
        assert s.count(old) == 1, f"{rel}: ×{s.count(old)} {old[:90]}"
        s = s.replace(old, new)
    open(p, 'w', encoding='utf-8').write(s); print("patched", rel)

# 文件
edit("readme/RECIPE_DESCRIBE.md", [
("| 巨彈 `BulletScale` | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |",
 "| 須彌 `BulletScale`（施放大小） | ✓ | ✓ | ✓（含爆炸範圍） | ✗ | ✓ 光圈 | ✗ | ✓ 範圍＋雷柱 | ✗ | ✓ 半徑＋圖 | ✓ 半徑＋特效 | ✓ 寬度＋特效 |"),
("- 反彈珠對連鎖閃電**不再**增加跳數",
 "- 須彌珠（`BulletScale`，原名巨彈）＝**施放大小**：對非子彈模式是「範圍與視覺一起放大」（所見即所得，不會畫面變大傷害範圍沒變）；雷射／連鎖的粗細是 `BeamWidth`，不吃它。\n- 反彈珠對連鎖閃電**不再**增加跳數"),
("**武器表（WeaponTable）欄位** 也依模式分：`Damage` 除 Summon 外都吃；子彈外觀（`WeaponSpritePath`…`BulletScale`）只有 Normal／Orbital／Parabolic；",
 "**武器表（WeaponTable）欄位** 也依模式分：`Damage` 除 Summon 外都吃；子彈外觀（`WeaponSpritePath`…`AnimFPS`）只有 Normal／Orbital／Parabolic，`BulletScale`（施放大小）另外也給 Aura／SkyStrike／GroundCast／Melee／Dash（範圍與視覺一起放大）；"),
])
edit("readme/GEM_SOCKET.md", [
("| 7 | 巨彈珠 | `scale` | `gemIcon_scale.png` |", "| 7 | 須彌珠（原巨彈珠，2026-08-26 改名；效果＝施放大小：子彈變大、近戰／突進／法陣／落雷／佛光範圍與視覺一起放大） | `scale` | `gemIcon_scale.png` |"),
])

# PROGRESS
p = os.path.join(root, "readme/PROGRESS.md"); s = open(p, encoding='utf-8').read()
entry = """* [x] **「巨彈珠」改名「須彌珠」，效果從「子彈變大」擴成「施放大小」——近戰／突進／法陣／落雷／佛光也吃**（2026-08-26，見 [RECIPE_DESCRIBE.md](RECIPE_DESCRIBE.md) §4）：作者測血月鬼爪時發現近戰武器沒有任何「放大」可調——規格表裡 `BulletScale` 只給會飛子彈的三種模式，近戰只剩傷害與疾發兩顆珠有用，太苛。<br>**改法**：`BulletScale` 語意改成「施放大小」，對非子彈模式**範圍與視覺一起放大**（近戰半徑＋揮砍特效、突進寬度＋特效、法陣半徑＋圖、落雷 AOE＋雷柱、佛光光圈、拋物線落地爆炸半徑），所見即所得——刻意不做「只放大特效」，那會重演佛光那次「畫面變大傷害圈沒變」的騙人狀況（法陣走 `GroundEffectManager.Spawn` 的 `radiusScale` 而非 `visualScale`，理由同）。雷射／連鎖不動，粗細本來就是 `BeamWidth`。<br>**一個順手的簡化**：這幾條分支原本用 `CastVisualScale`（集氣快照＝2）放大視覺，現在改讀 `BulletScale`——集氣快照的 `BulletScale` 本來就 ×2，所以集氣時視覺仍 ×2，**範圍也跟著 ×2**（原本集氣只放大視覺），與「所見即所得」一致。子彈類的發射／擊中／軌跡特效仍用 `CastVisualScale`，不隨須彌珠變大。<br>**通則**：珠子的「有效」不只是「程式有沒有讀那一欄」，還要問「這種模式下這個概念叫什麼」——「子彈大小」對近戰無意義，但「施放大小」對每一種模式都有意義；先把概念泛化，再決定有效性，比直接判無效好得多。

"""
anchor = "* [x] **武器工坊：Unity EditorWindow 版"
assert anchor in s
s = s.replace(anchor, entry + anchor, 1)
open(p, 'w', encoding='utf-8').write(s); print("PROGRESS ok")
