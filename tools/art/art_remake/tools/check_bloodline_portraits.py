#!/usr/bin/env python3
"""主角血統立繪的「上線前驗證」：一次跑完**對話框**與**血統轉換面板**兩種排法，並自動檢查規格。
每做一個新血統（或換任何一張主角立繪）都要跑，見 readme/BLOODLINE.md §7「立繪驗證」。

用法（在 repo 根目錄 DipanProj/ 跑）:
  python3 tools/art/art_remake/tools/check_bloodline_portraits.py <系列Key>/<角色資料夾> [<系列Key>/<角色資料夾> ...]
例:
  python3 tools/art/art_remake/tools/check_bloodline_portraits.py Machinaborn/MachinaIncarnate Machinaborn/AutomatonTyrant Machinaborn/Mozi
輸出:
  TempImage/portrait_check/<角色資料夾>.jpg   ← **一定要打開用眼睛看**（自動檢查抓不到「臉在哪」）
  終端機逐條 PASS／WARN／FAIL；任何 FAIL 結束碼 1。
環境變數:
  TALK=<資料夾>   改讀別的立繪根目錄（預設 DipanProj_Main/Assets/GameAssets/Main/Characters/Talk），
                  用來「先驗再覆蓋」：把候選圖放到 TempImage/.../talk/<系列>/<角色>/ 再指過去。

兩種排法的參數照抄程式（改程式記得改這裡）:
  對話框  TalkPanel.SetAvatarWaistUp：頭頂線 235/1536 → 畫面 975、腰線 800/1536 → 對話框上緣（≈577）、
          身體中心線 470/1024（寬版 700/1536）→ 畫面中心 ±500；右側立繪水平鏡像；內容頂緣超過 1072 整張往下推。
  轉換面板 BloodlineIntroPanel 規格路徑：頭頂線 → 框頂下 165、腹部線 800 → 立繪底線、中心線 → 破碎框中心，
          只顯示框內黑底（左右 0.225～0.80、上 0.12、下 0.13）。
"""
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

EMOTIONS = ['normal', 'happy', 'angry', 'cry', 'fear', 'hurt']
TALK = os.environ.get('TALK', 'DipanProj_Main/Assets/GameAssets/Main/Characters/Talk')
RES = 'DipanProj_Main/Assets/Resources/'
OUT_DIR = 'TempImage/portrait_check'
ALPHA = 20

# ── 對話框（TalkPanel）──
SW, SH = 1920, 1080
BOX_TOP = 24 + 828 * (1500 / 2246)        # 對話框上緣（畫面 y，從底往上）
BOX_L, BOX_R = SW / 2 - 750, SW / 2 + 750  # 對話框左右緣（DisplayWidth 1500；外框花飾不算，保守）
HF, WF, HEADY, TOPLIM, BX = 235 / 1536, 800 / 1536, 975, 1072, 500
# ── 轉換面板（BloodlineIntroPanel）──
FH = 880
BOTTOM_INSET, CLIP_TOP, CLIP_L, CLIP_R = 0.13, 0.12, 0.225, 0.80
HEAD_FROM_TOP, CROP_BOTTOM = 165, 800

results = []


def report(level, who, msg):
    results.append(level)
    print(f'  [{level}] {who}: {msg}')


def alpha_of(im):
    return np.asarray(im)[..., 3] > ALPHA


def spec_lines(W, H):
    u = H / 1536
    return u, 235 * u, 800 * u, (700 if W > H else 470)


# ───────────── 對話框模擬 ─────────────
def talk_place(im, right):
    """回傳 (縮放後的圖, 畫面左上角 x, y(往下為正), 映射函式 canvas(x,y)->screen(x,y往上))"""
    cw, ch = im.size
    a = alpha_of(im)
    ys = np.nonzero(a.any(1))[0]
    H = (HEADY - BOX_TOP) / (WF - HF)
    W = H * cw / ch
    top = HEADY + HF * H
    ctop = top - (ys.min() / ch) * H
    if ctop > TOPLIM:
        top -= ctop - TOPLIM
    cx = 700 / 1536 if cw > ch else 470 / 1024
    left = SW / 2 + BX - (1 - cx) * W if right else SW / 2 - BX - cx * W
    k = W / cw

    def to_screen(x, y):
        sx = left + (W - (x + 0.5) * k) if right else left + (x + 0.5) * k
        return sx, top - (y + 0.5) * k

    src = im.transpose(Image.FLIP_LEFT_RIGHT) if right else im
    return src.resize((max(1, round(W)), max(1, round(H))), Image.LANCZOS), left, SH - top, to_screen


def talk_sheet(im, label):
    scr = Image.new('RGBA', (SW, SH), (60, 55, 50, 255))
    for right in (False, True):
        r, x, y, _ = talk_place(im, right)
        scr.paste(r, (round(x), round(y)), r)
    d = ImageDraw.Draw(scr)
    d.rectangle([BOX_L, SH - BOX_TOP, BOX_R, SH - 24], fill=(30, 25, 20, 255), outline=(180, 140, 80, 255), width=6)
    yh = SH - HEADY
    d.line([(0, yh), (SW, yh)], fill=(255, 60, 60, 255), width=3)
    d.text((20, yh + 6), 'head line 975 (author widescreen cuts around here)', fill=(255, 90, 90, 255))
    d.text((20, 20), label, fill=(255, 255, 0, 255))
    return scr.convert('RGB').resize((640, 360), Image.LANCZOS)


SPILL_WARN = 8   # 腰線以下露出對話框邊外超過幾 px 就提醒（只提醒、不判 FAIL：露出的是尾巴還是腳，要人看）


def talk_metrics(im):
    """回傳 {'spill': 最多露出框外幾 px（左右兩側取大）, 'top': 內容頂端在畫面的高度（取左右較高）}"""
    a = alpha_of(im)
    rr, cc = np.nonzero(a)
    spill, top = 0.0, 0.0
    for right in (False, True):
        _, _, _, f = talk_place(im, right)
        sx, sy = f(cc, rr)
        hid = sy < BOX_TOP                      # 在對話框上緣以下，只有落在框內才會被蓋住
        if hid.any():
            spill = max(spill, float(BOX_L - sx[hid].min()), float(sx[hid].max() - BOX_R))
        top = max(top, float(sy.max()))
    return {'spill': spill, 'top': top}


def check_talk(ims, who):
    m = {e: talk_metrics(im) for e, im in ims.items() if im.size in ((1024, 1536), (1536, 1024))}
    if not m:
        return
    worst = max(m.values(), key=lambda v: v['spill'])['spill']
    bad = [f"{e} {v['spill']:.0f}px" for e, v in m.items() if v['spill'] > SPILL_WARN]
    if worst > SPILL_WARN:
        report('WARN', who, f'對話框：腰線以下的內容露在對話框左右邊外（{", ".join(bad)}）——看 sheet：露出的是尾巴、衣襬可接受'
               f'（九尾天狐的尾巴露約 470px 是設計）；**手、腳、身體露出去不行**（機偶暴君第一版：跨步的腳、垂下的手露 30～40px 就很明顯）')
    tops = {e: v['top'] for e, v in m.items()}
    hi = max(tops.values())
    if hi > SH - 2:
        report('FAIL', who, f'對話框：內容頂端超出畫面（{hi:.0f}／1080）')
    elif hi > HEADY + 8:
        lst = ', '.join(f'{e} +{t - HEADY:.0f}' for e, t in tops.items() if t > HEADY + 8)
        report('WARN', who, f'對話框：內容高出頭頂線 975（{lst}）——作者寬螢幕在頭頂線附近以上會被切。'
               '**高出去的只能是頭髮尖、角、耳、蒸汽**這類切掉也無妨的；身體（煙囪、帽子、舉高的手）高出去＝不合格，要整組往下移／縮小')


# ───────────── 轉換面板模擬 ─────────────
def intro_tile(im, label):
    frame = Image.open(RES + 'UI/BloodlinePanel/BloodlinePanel_Bg.png').convert('RGBA')
    FW = round(FH * frame.width / frame.height)
    PW, PH = 760, 1000
    ox, oy = (PW - FW) // 2, (PH - FH) // 2
    c = Image.new('RGBA', (PW, PH), (52, 48, 46, 255))
    c.alpha_composite(frame.resize((FW, FH)), (ox, oy))
    W, H = im.size
    u, headY, cropY, cxp = spec_lines(W, H)
    cropY = min(CROP_BOTTOM * u, H)
    bottom_line = oy + FH - FH * BOTTOM_INSET
    cxs = ox + FW / 2
    top_target = FH * (1 - BOTTOM_INSET) - HEAD_FROM_TOP
    s = top_target / (cropY - headY)
    px, py = cxs - cxp * s, bottom_line - top_target - headY * s
    big = im.resize((max(1, round(W * s)), max(1, round(H * s))), Image.LANCZOS)
    layer = Image.new('RGBA', (PW, PH), (0, 0, 0, 0))
    layer.paste(big, (round(px), round(py)), big)
    arr = np.array(layer)
    x0, x1 = int(ox + FW * CLIP_L), int(ox + FW * CLIP_R)
    y0, y1 = int(oy + FH * CLIP_TOP), int(bottom_line)
    m = np.zeros(arr.shape[:2], bool)
    m[y0:y1, x0:x1] = True
    arr[~m, 3] = 0
    c.alpha_composite(Image.fromarray(arr))
    # 可見窗換回畫布 px（給檢查用）
    win = ((x0 - px) / s, (x1 - px) / s, (y0 - py) / s, (y1 - py) / s)
    d = ImageDraw.Draw(c)
    d.text((10, 10), label + ' (bloodline intro panel)', fill=(255, 255, 255))
    return c.convert('RGB'), win


def check_intro(im, who):
    W, H = im.size
    _, win = intro_tile(im, '')
    a = alpha_of(im)
    x0, x1, y0, y1 = [int(round(v)) for v in win]
    sub = a[max(0, y0):min(H, y1), max(0, x0):min(W, x1)]
    fill = sub.mean() if sub.size else 0
    if fill < 0.25:
        report('FAIL', who, f'轉換面板可見窗只有 {fill:.0%} 有內容（人跑出窗外／太小）')
    u, headY, _, cxp = spec_lines(W, H)
    # 頭部帶：內容頂端 ～ 頭頂線＋人形頭高（約 185，寬版 ×2/3）；它的右緣超出可見窗就是臉被裁
    ys = np.nonzero(a.any(1))[0]
    band = a[ys.min():int(headY + 185 * u)]
    xs = np.nonzero(band.any(0))[0]
    if len(xs) and xs.max() > x1:
        report('WARN', who, f'轉換面板：頭部那一帶的右緣 x={xs.max()} 超出可見窗右緣 {x1}——**打開圖看臉有沒有被裁**；'
               '被裁就整組表情水平平移（CHARACTER_ART_SPEC §2.0「臉要落在中心線附近」）')


# ───────────── 規格與表情一致性 ─────────────
def check_spec(ims, who):
    sizes = {e: im.size for e, im in ims.items()}
    for e, (W, H) in sizes.items():
        if (W, H) not in ((1024, 1536), (1536, 1024)):
            report('FAIL', f'{who}/{e}', f'畫布 {W}×{H} 不是規格尺寸（1024×1536／寬版 1536×1024）→ 程式會走舊排法，兩個面板都會亂')
    if len(set(sizes.values())) > 1:
        report('FAIL', who, f'六張畫布尺寸不一致：{sizes}')
    boxes = {}
    for e, im in ims.items():
        W, H = im.size
        u, headY, waistY, _ = spec_lines(W, H)
        a = alpha_of(im)
        ys = np.nonzero(a.any(1))[0]
        xs = np.nonzero(a.any(0))[0]
        boxes[e] = (xs.min(), ys.min(), xs.max(), ys.max(), H)
        if ys.max() < waistY + 40 * u:
            report('FAIL', f'{who}/{e}', f'內容底端 y={ys.max()} 沒蓋過腰線 {waistY:.0f}——對話框上緣會露出空隙（人浮在框上）')
    if len(boxes) > 1:
        tops = [b[1] for b in boxes.values()]; bots = [b[3] for b in boxes.values()]; rights = [b[2] for b in boxes.values()]
        H = list(boxes.values())[0][4]
        if max(tops) - min(tops) > 0.01 * H or max(bots) - min(bots) > 0.01 * H or max(rights) - min(rights) > 30:
            report('WARN', who, f'六張表情的內容框不一致（頂差 {max(tops) - min(tops)}、底差 {max(bots) - min(bots)}、右緣差 {max(rights) - min(rights)}px；'
                   '規格 §2.5：頂底 ≤1%、右緣 ≤30）——人形角色要修；大動作表情（非人形）看 sheet 確認臉沒有跳來跳去')


def run(char):
    who = char
    folder = os.path.join(TALK, char)
    ims = {}
    for e in EMOTIONS:
        p = os.path.join(folder, e + '.png')
        if not os.path.exists(p):
            report('FAIL', who, f'缺 {e}.png')
            continue
        ims[e] = Image.open(p).convert('RGBA')
    if not ims:
        return
    print(f'== {char}')
    check_spec(ims, who)
    check_talk(ims, who)
    if 'normal' in ims and ims['normal'].size in ((1024, 1536), (1536, 1024)):
        check_intro(ims['normal'], f'{who}/normal')
    # 總表：左 3×2 對話框（每格左右各放一次），右 轉換面板（normal）
    talks = [talk_sheet(ims[e], f'{char} / {e}') for e in EMOTIONS if e in ims]
    intro = intro_tile(ims['normal'], char)[0] if 'normal' in ims else Image.new('RGB', (760, 1000))
    intro = intro.resize((round(intro.width * 1080 / intro.height), 1080))
    S = Image.new('RGB', (640 * 2 + intro.width, 1080), (25, 25, 25))
    for i, t in enumerate(talks):
        S.paste(t, ((i % 2) * 640, (i // 2) * 360))
    S.paste(intro, (1280, 0))
    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, char.replace('/', '_') + '.jpg')
    S.save(out, quality=88)
    print(f'  → {out}（打開看：臉在不在、頂端有沒有被紅線切、腰線以下有沒有露出框外、六張位置有沒有跳）')


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    for c in sys.argv[1:]:
        run(c)
    n_fail = results.count('FAIL'); n_warn = results.count('WARN')
    print(f'\n結果：FAIL {n_fail}、WARN {n_warn}' + ('（全部通過）' if not n_fail and not n_warn else ''))
    sys.exit(1 if n_fail else 0)
