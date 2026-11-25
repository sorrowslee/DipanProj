# 克蘇魯「虛化觸手往左右拍地」動作模擬（暫代程序觸手皮；GPT 皮與 shader 之後換）
# 座標：數學座標 y 往上，單位＝角色高 H；腳底 y=0、角色中心 x=0。
import numpy as np, sys, math
from PIL import Image

H = 300                      # 角色可見高（px）
FPS = 20
OUT = sys.argv[1] if len(sys.argv) > 1 else 'tent.gif'

def srgb2lin(c): c = np.array(c, np.float32) / 255.; return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def lin2srgb(c): c = np.clip(c, 0, 1); return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)
def ss(a, b, x): t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
def clamp01(x): return min(1.0, max(0.0, x))
def ease_out_back(x, k=1.6): x = clamp01(x); return 1 + (k + 1) * (x - 1) ** 3 + k * (x - 1) ** 2
def ease_in_out(x): x = clamp01(x); return x * x * (3 - 2 * x)

# ---------- 噪聲（煙霧密度圖） ----------
def load_gray(p, size=512):
    return np.asarray(Image.open(p).convert('L').resize((size, size), Image.BILINEAR)).astype(np.float32) / 255.
SA = load_gray('BossIntroPanel_Smoke1.png'); SB = load_gray('BossIntroPanel_Smoke2.png')
def samp(tex, u, v):
    n = tex.shape[0]; x = (u % 1) * n - 0.5; y = (v % 1) * n - 0.5
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int); fx = x - x0; fy = y - y0
    x0 %= n; y0 %= n; x1 = (x0 + 1) % n; y1 = (y0 + 1) % n
    return tex[y0, x0] * (1 - fx) * (1 - fy) + tex[y0, x1] * fx * (1 - fy) + tex[y1, x0] * (1 - fx) * fy + tex[y1, x1] * fx * fy

# ---------- 角色 ----------
spr = Image.open('Cthulhu.png').convert('RGBA')
a = np.asarray(spr)[..., 3]; ys, xs = np.where(a > 10)
top, bot = ys.min(), ys.max(); sc = H / (bot - top)
spr = spr.resize((int(spr.width * sc), int(spr.height * sc)), Image.LANCZOS)
S = np.asarray(spr).astype(np.float32) / 255.
SL = srgb2lin((S[..., :3] * 255).astype(int)); SAl = S[..., 3]
feet_py = bot * sc; cx_px = 512 * sc   # 素材規格：中心 512、腳底 864

W, Hc = 1080, 560
GROUND_PY = Hc - 70          # 腳底在畫布的 y
CX = W // 2
def to_px(x, y): return CX + x * H, GROUND_PY - y * H
BG = np.ones((Hc, W, 3), np.float32) * srgb2lin((46, 44, 48))

# ---------- 觸手皮（正式）----------
import os
SKIN = None; SKIN_ASPECT = 1.0; SKIN_BRIGHT = 1.0
if os.environ.get('SKIN'):
    _sk = np.asarray(Image.open(os.environ['SKIN']).convert('RGBA')).astype(np.float32) / 255.
    SKIN = np.dstack([srgb2lin((_sk[..., :3] * 255).astype(int)), _sk[..., 3]])
    SKIN_ASPECT = _sk.shape[1] / _sk.shape[0]
    SKIN_BRIGHT = float(os.environ.get('SKIN_BRIGHT', '1.0'))
# ---------- 觸手參數 ----------
N = 64                       # 節數
COL_BACK = srgb2lin((34, 104, 94)); COL_BELLY = srgb2lin((62, 30, 66))
COL_SUCK = srgb2lin((226, 108, 132)); COL_WISP = srgb2lin((40, 22, 52)); COL_RIM = srgb2lin((70, 225, 210))
OPACITY = 0.72

VARIANTS = [  # 根部（相對角色）、長度、根寬、落地點深度
    dict(root=(-0.06, 0.64), rootR=0.04, L=1.75, w0=0.10, gy=0.03),
    dict(root=(-0.10, 0.56), rootR=0.00, L=1.60, w0=0.09, gy=-0.06),
]

def pose_angles(phase, t, seed):
    """回傳每節的絕對角度（度，0＝往外、90＝往上），在『往右』座標系。"""
    s = np.arange(N) / (N - 1)
    sway = 8 * np.sin(t * 2.3 + seed + s * 4.0) * s
    rest = 80 - 230 * s ** 1.5 + sway                    # 舉起、尖端往外勾下（問號形）
    wind = 102 - 185 * s ** 1.35 + sway * 0.5             # 往後上方立起蓄力
    slap = SLAP_A0 - SLAP_K * s                          # 往外甩下去（著地後由地面約束攤平）
    slap = np.maximum(slap, -88)
    return s, rest, wind, slap

FLAT_R = 0.16
TL = dict(emerge=0.45, sway=0.30, wind=0.32, slap=0.20, linger=0.55, fade=0.60)
T_TOTAL = sum(TL.values())
DELAY = 0.55
SLAP_A0, SLAP_K = 35, 280                                             # 揮鞭：尖端比根部晚多少（比例）

def tentacle_state(tl, seed, var):
    """tl＝這條觸手自己的時間。回傳 (點列, 可見度 vis, 溶解進度 form/gone, 衝擊時間)"""
    s, rest, wind, slap = pose_angles(0, tl, seed)
    t0 = TL['emerge']; t1 = t0 + TL['sway']; t2 = t1 + TL['wind']; t3 = t2 + TL['slap']; t4 = t3 + TL['linger']
    if tl < t1:
        ang = rest
    elif tl < t2:
        k = ease_in_out((tl - t1) / TL['wind']); ang = rest * (1 - k) + wind * k
    elif tl < t3:
        tau = (tl - t2) / TL['slap']
        pi = np.clip((tau * (1 + DELAY) - s * DELAY), 0, 1)
        k = np.array([ease_out_back(p, 1.2) for p in pi])
        ang = wind * (1 - k) + slap * k
    else:
        writhe = 7 * np.sin((tl - t3) * 9 - s * 9) * s * math.exp(-(tl - t3) * 2.5)
        lift = ease_in_out((tl - t4) / TL['fade']) * 25 * (1 - s) if tl > t4 else 0
        ang = slap + writhe + lift
    # 正向運動學＋地面約束：碰到地面就沿地面往外攤平
    L = var['L']; ds = L / (N - 1); gy = var['gy']
    pts = np.zeros((N, 2)); pts[0] = (0, var['root'][1]); grounded = np.zeros(N, bool)
    for i in range(1, N):
        th = math.radians(ang[i - 1])
        h = pts[i - 1, 1] - gy
        lim = -88 * clamp01(h / FLAT_R)
        a_deg = max(ang[i - 1], lim)
        th = math.radians(a_deg)
        nx = pts[i - 1, 0] + math.cos(th) * ds; ny = max(gy, pts[i - 1, 1] + math.sin(th) * ds)
        if ny <= gy + 0.012: grounded[i] = True
        pts[i] = (nx, ny)
    # 可見度：凝結／散去
    form = clamp01(tl / TL['emerge'])
    gone = clamp01((tl - t4) / TL['fade'])
    # 第一次著地的時間（給波紋）
    return pts, form, gone, grounded

def contact_point(pts, grounded):
    idx = np.where(grounded)[0]
    return pts[idx[0]] if len(idx) else None

def render_tentacle(img, pts_out, side, var, tl, seed, form, gone):
    """pts_out：往右座標系的點；side=+1 右、-1 左（鏡射）。畫在 img（線性、預乘合成）。"""
    rx = var['root'][0] if side < 0 else var['rootR']
    P = np.stack([rx + side * pts_out[:, 0], pts_out[:, 1]], 1)
    px = np.array([to_px(x, y) for x, y in P])
    s = np.arange(N) / (N - 1)
    w = var['w0'] * (1 - 0.84 * s ** 1.1) * H                 # 半寬 px
    glow_w = 1.6
    pad = int(w.max() * glow_w) + 4
    x0 = int(max(0, px[:, 0].min() - pad)); x1 = int(min(W, px[:, 0].max() + pad))
    y0 = int(max(0, px[:, 1].min() - pad)); y1 = int(min(Hc, px[:, 1].max() + pad))
    if x1 <= x0 or y1 <= y0: return
    gx, gy_ = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
    Q = np.stack([gx.ravel(), gy_.ravel()], 1)
    A = px[:-1]; B = px[1:]; AB = B - A; L2 = (AB ** 2).sum(1) + 1e-9
    best_d = np.full(len(Q), 1e9); best_s = np.zeros(len(Q)); best_sign = np.ones(len(Q))
    for i in range(N - 1):
        AQ = Q - A[i]; t = np.clip((AQ @ AB[i]) / L2[i], 0, 1)
        C = A[i] + t[:, None] * AB[i]; d = np.sqrt(((Q - C) ** 2).sum(1))
        m = d < best_d
        best_d[m] = d[m]; best_s[m] = (s[i] + t[m] * (s[i + 1] - s[i]))
        cr = AB[i, 0] * AQ[m, 1] - AB[i, 1] * AQ[m, 0]          # 影像座標（y 往下）的叉積
        best_sign[m] = np.sign(cr + 1e-9)
    wl = np.interp(best_s, s, w)
    v = best_d / wl * best_sign * side                          # 影像座標：side=+1 時 cr<0 ＝ 上方（背）
    v = -v                                                      # +1＝背（上緣）、−1＝腹（吸盤）
    av = np.abs(v)
    # 沿身體的世界長度（貼圖座標）
    seglen = np.sqrt(L2); cum = np.concatenate([[0], np.cumsum(seglen)])
    sl = np.interp(best_s, s, cum)
    if SKIN is not None:
        # ---- 正式皮：沿身體 u＝∫ds/(皮厚×寬高比)、v＝背(+1)→圖上緣 ----
        tw = 2 * w * SKIN_ASPECT
        ucum = np.concatenate([[0], np.cumsum(seglen / np.maximum((tw[:-1] + tw[1:]) * 0.5, 1e-3))])
        uu = np.interp(best_s, s, ucum)
        vv_t = np.clip(0.5 - 0.5 * v * 0.98, 0, 1)
        th_, tw_ = SKIN.shape[0], SKIN.shape[1]
        xx = (uu % 1) * tw_ - 0.5; yy = vv_t * (th_ - 1)
        x0_ = np.floor(xx).astype(int); y0_ = np.clip(np.floor(yy).astype(int), 0, th_ - 2); fx = (xx - x0_)[:, None]; fy = (yy - y0_)[:, None]
        x0_ %= tw_; x1_ = (x0_ + 1) % tw_
        smp = SKIN[y0_, x0_] * (1 - fx) * (1 - fy) + SKIN[y0_, x1_] * fx * (1 - fy) + SKIN[y0_ + 1, x0_] * (1 - fx) * fy + SKIN[y0_ + 1, x1_] * fx * fy
        skin_a = smp[:, 3]
        # ---- 暫代皮：背青綠、腹暗紫、腹側一排吸盤 ----
    tback = ss(-0.6, 0.6, v)
    col = COL_BELLY[None] * (1 - tback[:, None]) + COL_BACK[None] * tback[:, None]
    spacing = np.maximum(wl * 0.95, 3)
    ph = (sl / spacing) % 1.0
    du = (ph - 0.5) * spacing; dv = (v + 0.55) * wl
    r = np.sqrt(du ** 2 + dv ** 2); rs = wl * 0.36
    ring = (0.45 + 0.55 * ss(rs * 0.4, rs * 0.6, r)) * (1 - ss(rs * 0.85, rs * 1.0, r))
    col = col * (1 - ring[:, None] * 0.85) + COL_SUCK[None] * ring[:, None] * 0.85
    if SKIN is not None:
        col = smp[:, :3] * SKIN_BRIGHT
    # ---- 虛化 ----
    tm = tl + seed * 7
    n1 = samp(SA, sl / H * 1.1 - tm * 0.35, v * 0.25 + seed)
    n2 = samp(SB, sl / H * 2.3 - tm * 0.6, v * 0.5 + 0.3 + seed)
    mott = 0.30 + 0.70 * ss(0.25, 0.75, n1 * 0.6 + n2 * 0.4)
    body = (1 - ss(0.82, 1.0, av)) * (av < 1)
    root_fade = ss(0.0, 0.12, best_s)                           # 根部淡入（藏在身後）
    tip_fade = 1 - ss(0.88, 1.0, best_s) * 0.6
    # 溶解（凝結：門檻往下；散去：尖端先散）
    m = samp(SB, sl / H * 0.9 + seed * 0.21, v * 0.3 + 0.57)
    th = 1 - form
    keep = ss(th - 0.15, th + 0.15, m * 0.6 + 0.4 * (1 - best_s * 0.3))
    vv = gone * 1.7 - (1 - best_s) * 0.6 + (m - 0.5) * 0.4
    keep *= 1 - ss(0, 0.3, vv)
    if SKIN is not None: body = body * np.clip(skin_a * 1.0, 0, 1)
    alpha = body * mott * OPACITY * root_fade * tip_fade * keep
    # 外緣青光（加色）：貼著輪廓一圈＋外暈
    rim = np.exp(-((av - 0.95) / 0.16) ** 2) * (av < glow_w)
    rim *= (0.35 + 0.65 * n2) * root_fade * keep * tip_fade
    # 外緣煙絲：輪廓外 1.0~1.6 倍半寬、往尖端流的噪聲（暗紫遮擋）
    wisp = ss(0.55, 0.8, samp(SA, sl / H * 1.7 - tm * 0.8, v * 0.45 + seed * 0.3)) * (1 - ss(1.0, glow_w, av)) * (av > 0.9)
    wisp *= 0.45 * root_fade * keep * tip_fade
    alpha = alpha + wisp * (1 - alpha)
    col = np.where((av > 0.9)[:, None], COL_WISP[None], col)
    out_rgb = col * alpha[:, None] + COL_RIM[None] * (rim * 0.45)[:, None]
    hh, ww = y1 - y0, x1 - x0
    out_rgb = out_rgb.reshape(hh, ww, 3); alpha = alpha.reshape(hh, ww)
    reg = img[y0:y1, x0:x1]
    img[y0:y1, x0:x1] = out_rgb + reg * (1 - alpha[..., None])

def render_ripple(img, cpt, age):
    if age < 0 or age > 0.5: return
    k = age / 0.5
    cxp, cyp = to_px(*cpt)
    R = (0.06 + 0.20 * (1 - (1 - k) ** 2)) * H
    yy, xx = np.mgrid[0:Hc, 0:W]
    x0, x1 = int(max(0, cxp - R - 6)), int(min(W, cxp + R + 6)); y0, y1 = int(max(0, cyp - R * 0.35 - 6)), int(min(Hc, cyp + R * 0.35 + 6))
    if x1 <= x0 or y1 <= y0: return
    gx, gy = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
    d = np.sqrt(((gx - cxp) / R) ** 2 + ((gy - cyp) / (R * 0.35)) ** 2)
    ring = np.exp(-((d - 1) / 0.08) ** 2) * (1 - k) ** 2 * 0.55
    img[y0:y1, x0:x1] += COL_RIM[None, None] * ring[..., None]

def comp_sprite(img):
    ox = int(CX - cx_px); oy = int(GROUND_PY - feet_py)
    h, w = SAl.shape
    img[oy:oy + h, ox:ox + w] = SL * SAl[..., None] + img[oy:oy + h, ox:ox + w] * (1 - SAl[..., None])

STAG = 1.05; PERIOD = 4 * STAG
def frame(t):
    img = BG.copy()
    ripples = []
    k0 = int(math.floor((t - T_TOTAL) / STAG)) - 1
    for k in range(k0, int(t / STAG) + 1):
        tl = t - k * STAG
        if tl < 0 or tl > T_TOTAL: continue
        side = 1 if k % 2 == 0 else -1
        var = VARIANTS[(k // 2) % 2]
        seed = (k % 4) * 1.7
        pts, form, gone, grounded = tentacle_state(tl, seed, var)
        render_tentacle(img, pts, side, var, tl, seed, form, gone)
        # 波紋：從第一次著地開始
        t2 = TL['emerge'] + TL['sway'] + TL['wind']
        for tt in np.linspace(t2, t2 + TL['slap'], 9):
            if tt > tl: break
            p2, _, _, g2 = tentacle_state(tt, seed, var)
            if g2.any():
                c = contact_point(p2, g2); rx = var['root'][0] if side < 0 else var['rootR']; cp = (rx + side * c[0], c[1])
                ripples.append((cp, tl - tt)); break
    for cp, age in ripples: render_ripple(img, cp, age)
    comp_sprite(img)
    return img

if __name__ == '__main__':
    t_start = 10 * STAG
    nfr = int(PERIOD * FPS)
    frames = []
    for f in range(nfr):
        im = frame(t_start + f / FPS)
        frames.append(Image.fromarray((lin2srgb(im) * 255).astype(np.uint8)))
        if f % 12 == 0: print('frame', f, '/', nfr, flush=True)
    frames[0].save(OUT, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0)
    for i in (12, 20, 30, 46):
        frames[i].save(f'still_{i}.png')
    print('done', OUT)
