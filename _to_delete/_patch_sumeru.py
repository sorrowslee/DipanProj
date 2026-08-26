import sys, os
root = sys.argv[1]
def edit(rel, pairs):
    p = os.path.join(root, rel); s = open(p, encoding='utf-8').read()
    for old, new in pairs:
        assert s.count(old) == 1, f"{rel}: ×{s.count(old)} {old[:90]}"
        s = s.replace(old, new)
    open(p, 'w', encoding='utf-8').write(s); print("patched", rel)

PC = "DipanProj_Main/Assets/Scripts/PlayerController.cs"
edit(PC, [
# 近戰：範圍與視覺一起吃 BulletScale（須彌珠）；集氣快照的 BulletScale 已 ×2，所以視覺不再另乘 CastVisualScale
("""        float radius = weapon.Recipe.AreaRadius > 0f ? weapon.Recipe.AreaRadius : 2f;
        float halfAngle = Mathf.Clamp(weapon.Recipe.MeleeAngle, 1f, 360f) * 0.5f;
        float visualAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        Vector2 visualPos = origin + aim * (radius * 0.45f);
        if (_vfxManager != null && weapon.HitEffectID > 0)
            _vfxManager.Spawn(weapon.HitEffectID, visualPos, visualAngle, weapon.CastVisualScale);""",
 """        // 範圍與視覺一起乘 BulletScale（須彌珠＝施放大小；集氣快照的 BulletScale 已 ×2，所以這裡不再另乘 CastVisualScale）
        float radius = (weapon.Recipe.AreaRadius > 0f ? weapon.Recipe.AreaRadius : 2f) * weapon.BulletScale;
        float halfAngle = Mathf.Clamp(weapon.Recipe.MeleeAngle, 1f, 360f) * 0.5f;
        float visualAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        Vector2 visualPos = origin + aim * (radius * 0.45f);
        if (_vfxManager != null && weapon.HitEffectID > 0)
            _vfxManager.Spawn(weapon.HitEffectID, visualPos, visualAngle, weapon.BulletScale);"""),
# 突進
("""        float width = Mathf.Max(0.2f, weapon.Recipe.DashWidth);""",
 """        float width = Mathf.Max(0.2f, weapon.Recipe.DashWidth * weapon.BulletScale);   // 須彌珠：掃擊寬度與視覺一起放大"""),
("""            _vfxManager.Spawn(weapon.HitEffectID, mid, angle, weapon.CastVisualScale);""",
 """            _vfxManager.Spawn(weapon.HitEffectID, mid, angle, weapon.BulletScale);"""),
# 法陣：用 radiusScale（半徑與圖一起放大），不用只放大圖的 visualScale——畫面才不會騙人
("""        _groundEffectManager.Spawn(weapon.Recipe.GroundEffectID, target, weapon.Damage, weapon.CastVisualScale);""",
 """        // 須彌珠／集氣：走 radiusScale 讓傷害半徑與圖一起放大（visualScale 只放大圖，畫面會騙人）
        _groundEffectManager.Spawn(weapon.Recipe.GroundEffectID, target, weapon.Damage, 1f, weapon.BulletScale);"""),
# 落雷
("""            SegmentedLightningColumn.Spawn(impact, Camera.main, 1.5f * weapon.CastVisualScale);""",
 """            SegmentedLightningColumn.Spawn(impact, Camera.main, 1.5f * weapon.BulletScale);"""),
("""            boltOffset *= weapon.CastVisualScale;
            _vfxManager.Spawn(weapon.HitEffectID, impact + Vector2.up * boltOffset, 0f, weapon.CastVisualScale);""",
 """            boltOffset *= weapon.BulletScale;
            _vfxManager.Spawn(weapon.HitEffectID, impact + Vector2.up * boltOffset, 0f, weapon.BulletScale);"""),
("""        float radius = (weapon.Recipe != null && weapon.Recipe.AreaRadius > 0f) ? weapon.Recipe.AreaRadius : SkyStrikeDefaultBlast;""",
 """        float radius = ((weapon.Recipe != null && weapon.Recipe.AreaRadius > 0f) ? weapon.Recipe.AreaRadius : SkyStrikeDefaultBlast) * weapon.BulletScale;   // 須彌珠"""),
("""            _groundEffectManager.Spawn(weapon.Recipe.GroundEffectID, impact, -1f, weapon.CastVisualScale);""",
 """            _groundEffectManager.Spawn(weapon.Recipe.GroundEffectID, impact, -1f, 1f, weapon.BulletScale);"""),
# 拋物線落地爆炸
("""        float radius = firedWeapon.Recipe.AreaRadius;
        if (radius <= 0f || firedWeapon.Damage <= 0f) return;""",
 """        float radius = firedWeapon.Recipe.AreaRadius * firedWeapon.BulletScale;   // 須彌珠：爆炸範圍跟子彈一起變大
        if (radius <= 0f || firedWeapon.Damage <= 0f) return;"""),
# 佛光：半徑倍率 = 體型 × 須彌
("""            _activeAura = _groundEffectManager.Spawn(auraId, BodyCenterWorldPos, weapon.Damage,
                                                     1f, BodyScale);""",
 """            _activeAura = _groundEffectManager.Spawn(auraId, BodyCenterWorldPos, weapon.Damage,
                                                     1f, BodyScale * weapon.BulletScale);   // 須彌珠：光圈半徑與圖一起放大"""),
("""        if (_activeAura != null) _activeAura.SetRadiusScale(BodyScale);""",
 """        if (_activeAura != null) _activeAura.SetRadiusScale(BodyScale * (_activeAuraWeapon != null ? _activeAuraWeapon.BulletScale : 1f));"""),
])

SPEC = "DipanProj_Main/Assets/Scripts/Weapon/WeaponModeSpec.cs"
edit(SPEC, [
("""            F(W, "BulletScale",       FieldKind.Float, "子彈外觀", "子彈縮放", "1", 0.05f, 20f),""",
 """            F(W, "BulletScale",       FieldKind.Float, "子彈外觀", "施放大小", "1", 0.05f, 20f, "子彈變大；近戰／突進／法陣／落雷／佛光／拋物線爆炸＝範圍與視覺一起放大（須彌珠改這欄）；雷射與連鎖不吃（粗細看 BeamWidth）"),"""),
# Aura
("""            .Req("GroundEffectID").Eff("Damage")
            .Lbl("GroundEffectID", "佛光圓（GroundEffectTable ID，Duration=-1）");""",
 """            .Req("GroundEffectID").Eff("Damage", "BulletScale")
            .Lbl("GroundEffectID", "佛光圓（GroundEffectTable ID，Duration=-1）").Lbl("BulletScale", "光圈大小");"""),
# SkyStrike
("""            .Eff("Damage", "HitEffectID").Eff(BeamVisual)
            .Lbl("SpreadCount", "落點數").Lbl("AreaRadius", "落雷範圍半徑")""",
 """            .Eff("Damage", "HitEffectID", "BulletScale").Eff(BeamVisual)
            .Lbl("SpreadCount", "落點數").Lbl("AreaRadius", "落雷範圍半徑").Lbl("BulletScale", "落雷大小（範圍與雷柱）")"""),
# GroundCast
("""            .Eff("FireInterval").Req("GroundEffectID").Eff("Range").Eff(Charge)
            .Eff("Damage", "HitEffectID")
            .Lbl("GroundEffectID", "法陣（GroundEffectTable ID）").Lbl("Range", "施放距離");""",
 """            .Eff("FireInterval").Req("GroundEffectID").Eff("Range").Eff(Charge)
            .Eff("Damage", "HitEffectID", "BulletScale")
            .Lbl("GroundEffectID", "法陣（GroundEffectTable ID）").Lbl("Range", "施放距離").Lbl("BulletScale", "法陣大小（半徑與圖）");"""),
# Melee
("""            .Eff("FireInterval", "AreaRadius", "MeleeAngle").Eff(Charge)
            .Eff("Damage", "HitEffectID")
            .Lbl("AreaRadius", "攻擊半徑");""",
 """            .Eff("FireInterval", "AreaRadius", "MeleeAngle").Eff(Charge)
            .Eff("Damage", "HitEffectID", "BulletScale")
            .Lbl("AreaRadius", "攻擊半徑").Lbl("BulletScale", "揮砍大小（半徑與特效）");"""),
# Dash
("""            .Eff("FireInterval", "DashDistance", "DashWidth").Eff(Charge)
            .Eff("Damage", "HitEffectID");""",
 """            .Eff("FireInterval", "DashDistance", "DashWidth").Eff(Charge)
            .Eff("Damage", "HitEffectID", "BulletScale")
            .Lbl("BulletScale", "掃擊大小（寬度與特效）");"""),
])

edit("DipanProj_Main/Assets/Data/GemTable.csv", [
("7,巨彈,BulletScale,Weapon,10%,20%,50%,scale,,子彈視覺大小",
 "7,須彌,BulletScale,Weapon,10%,20%,50%,scale,,施放大小：子彈變大；近戰/突進/法陣/落雷/佛光/拋物線爆炸的範圍與視覺一起放大。雷射與連鎖不吃（粗細看武器表 BeamWidth）"),
])
edit("DipanProj_Main/Assets/Data/ItemTable.csv", [
("407,巨彈珠,Gem,None,,1,能力珠・巨彈,鑲進裝備的孔位　放大子彈,",
 "407,須彌珠,Gem,None,,1,能力珠・須彌,鑲進裝備的孔位　放大範圍,"),
])

# 文件
edit("readme/RECIPE_DESCRIBE.md", [
("| 巨彈 `BulletScale` | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |",
 "| 須彌 `BulletScale`（施放大小） | ✓ | ✓ | ✓（含爆炸範圍） | ✗ | ✓ 光圈 | ✗ | ✓ 範圍＋雷柱 | ✗ | ✓ 半徑＋圖 | ✓ 半徑＋特效 | ✓ 寬度＋特效 |"),
("- 反彈珠對連鎖閃電**不再**增加跳數",
 "- 須彌珠（`BulletScale`，原名巨彈）＝**施放大小**：對非子彈模式是「範圍與視覺一起放大」（所見即所得，不會畫面變大傷害範圍沒變）；雷射／連鎖的粗細是 `BeamWidth`，不吃它。\n- 反彈珠對連鎖閃電**不再**增加跳數"),
("| **武器表（WeaponTable）欄位** 也依模式分：`Damage` 除 Summon 外都吃；子彈外觀（`WeaponSpritePath`…`BulletScale`）只有 Normal／Orbital／Parabolic；",
 "| **武器表（WeaponTable）欄位** 也依模式分：`Damage` 除 Summon 外都吃；子彈外觀（`WeaponSpritePath`…`AnimFPS`）只有 Normal／Orbital／Parabolic，`BulletScale`（施放大小）另外也給 Aura／SkyStrike／GroundCast／Melee／Dash（範圍與視覺一起放大）；"),
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
