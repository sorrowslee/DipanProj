# 用法（BLOODLINE.md §5b〈`TentacleStyle`〉）：同資料夾放 Cthulhu.png（ArtSource/Bloodlines/Eldritchborn/）、
#   BossIntroPanel_Smoke1.png／Smoke2.png（Resources/UI/BossIntroPanel/），然後
#   SKIN=../../../../DipanProj_Main/Assets/Resources/VfxEffects/BloodlineTentacle/tentacle_skin.png SKIN_BRIGHT=2.2 python3 sim3.py out.gif
# 算式對應 Scripts/Gacha/BloodlineTentacle.cs（動作）與 sim.py render_tentacle ＝ Shaders/BloodlineTentacle.shader（外觀）；改一邊要兩邊一起改。
# 克蘇魯觸手 v5：六條在虛空中亂舞（被抓起來想掙脫的活章魚），不碰地面
import sys, math, numpy as np
from PIL import Image
OUT = sys.argv[1] if len(sys.argv) > 1 else 'tent_v5.gif'
sys.argv = [sys.argv[0]]
import sim
sim.Hc = 700; sim.GROUND_PY = sim.Hc - 80; sim.BG = np.ones((sim.Hc, sim.W, 3), np.float32) * sim.srgb2lin((46, 44, 48))
from sim import ss, clamp01, lin2srgb, N

FPS = 20; DUR = 3.0
T_IN0, T_FORM, T_SHOW, T_FADE, T_TAIL = 0.5, 0.3, 3.0, 0.5, 0.5
TOTAL = T_IN0 + T_FORM + T_SHOW + T_FADE + T_TAIL
rng = np.random.default_rng(11)
s_arr = np.arange(N) / (N - 1)

def smooth_noise(n_keys, dur, rng):
    """1D 平滑亂數：等距關鍵點 → Catmull-Rom；回傳 f(t) ∈ 約 [-1,1]"""
    keys = rng.uniform(-1, 1, n_keys + 3)
    step = dur / n_keys
    def f(t):
        x = t / step + 1; i = int(math.floor(x)); u = x - i
        i = max(1, min(i, n_keys + 1))
        p0, p1, p2, p3 = keys[i - 1], keys[i], keys[i + 1], keys[min(i + 2, n_keys + 2)]
        return 0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u ** 3)
    return f

# 每側三條：上（肩）、中、下（腰）；base＝根部方向的中心（往外座標，0＝水平往外、90＝往上）
BASE = [dict(ry=0.70, L=1.55, w0=0.095, lo=-15, hi=118),
        dict(ry=0.58, L=1.45, w0=0.088, lo=-50, hi=100),
        dict(ry=0.46, L=1.30, w0=0.080, lo=-70, hi=62)]
DT = 1 / 200.
TT = np.arange(-0.5, TOTAL + 0.5, DT)
TENTS = []
for side in (-1, 1):
    for j, b in enumerate(BASE):
        t = dict(side=side, rx=(-0.06 - 0.03 * j) if side < 0 else (0.03 + 0.02 * j), seed=rng.uniform(0, 10), **b)
        nb = smooth_noise(int(DUR * 1.2), DUR + 1, rng)       # 根部方向漂移
        nc = smooth_noise(int(DUR * 1.6), DUR + 1, rng)       # 捲曲（正＝往上捲、負＝往下捲）
        nc2 = smooth_noise(int(DUR * 2.6), DUR + 1, rng)
        nl = smooth_noise(int(DUR * 1.0), DUR + 1, rng)       # 收縮
        # 突然抽動：亂數脈衝 → 平滑包絡；同時加快時間（相位積分）
        burst = np.zeros_like(TT)
        tb = rng.uniform(0, 0.6)
        while tb < DUR + 0.5:
            amp = rng.uniform(0.5, 1.0); width = rng.uniform(0.18, 0.45)
            burst += amp * np.exp(-((TT - tb) / width) ** 2)
            tb += rng.uniform(0.35, 1.3)
        burst = np.clip(burst, 0, 1.2)
        speed = 1.0 + 2.6 * burst
        phase = np.cumsum(speed) * DT                           # 被加速的時間
        # 甩鞭：根部方向在大範圍內猛力來回甩（每段 0.12~0.22 秒、甩過頭再彈回），間隔很短
        rootA = np.zeros_like(TT); cur = rng.uniform(b['lo'], b['hi']); tc = rng.uniform(-0.3, 0.2)
        segs = []
        while tc < TOTAL + 0.6:
            for _ in range(20):
                nxt = rng.uniform(b['lo'], b['hi'])
                if abs(nxt - cur) >= 100: break
            dur = rng.uniform(0.16, 0.28); hold = rng.uniform(0.04, 0.44)
            segs.append((tc, dur, cur, nxt)); cur = nxt; tc += dur + hold
        for i, x in enumerate(TT):
            v = segs[0][2]
            for (t0, d, a, bb) in segs:
                if x < t0: break
                u = (x - t0) / d
                if u >= 1: v = bb; continue
                v = a + (bb - a) * sim.ease_out_back(u, 1.9)
            rootA[i] = v
        t.update(rootA=rootA, lag=rng.uniform(0.18, 0.26), stagger=rng.uniform(0, 0.08))
        t.update(nb=nb, nc=nc, nc2=nc2, nl=nl, burst=burst, phase=phase,
                 wf=rng.uniform(2.2, 3.4), wl=rng.uniform(5.5, 8.5), wf2=rng.uniform(4.5, 6.5))
        TENTS.append(t)

def at(arr, t): return float(np.interp(t, TT, arr))

def pose(tt, t):
    ph = at(tt['phase'], t); bu = at(tt['burst'], t)
    # 每節跟著根部角度、延遲 s*lag ⇒ 鞭子沿身體傳下去、尖端最後甩過去
    tdel = t - s_arr * tt['lag']
    base = np.interp(tdel, TT, tt['rootA'])
    # 甩的角速度越大，尾段越往反方向拖（拖曳）
    vel = (np.interp(tdel, TT, tt['rootA']) - np.interp(tdel - 0.03, TT, tt['rootA'])) / 0.03
    drag = -np.clip(vel, -1250, 1250) * 0.09 * s_arr ** 1.5
    curl = 150 * tt['nc'](ph * 0.45) + 60 * tt['nc2'](ph * 0.8)
    # 行進波（根→尖），抽動時振幅變大
    amp = 16 + 26 * bu
    wave = amp * np.sin(ph * tt['wf'] * 2 * math.pi * 0.35 + tt['seed'] - s_arr * tt['wl']) * s_arr ** 0.8 \
         + 10 * np.sin(ph * tt['wf2'] + tt['seed'] * 2 - s_arr * 3.5) * s_arr ** 2
    ang = base + drag + curl * s_arr ** 1.6 + wave * 0.6
    Lk = 0.86 + 0.14 * (0.5 + 0.5 * tt['nl'](ph * 0.5))     # 伸縮
    return ang, Lk, bu

def fk(tt, ang, Lk):
    ds = tt['L'] * Lk / (N - 1)
    pts = np.zeros((N, 2)); pts[0] = (0, tt['ry'])
    for i in range(1, N):
        th = math.radians(ang[i - 1])
        pts[i] = (pts[i - 1, 0] + math.cos(th) * ds, pts[i - 1, 1] + math.sin(th) * ds)
    return pts

def frame(t):
    img = sim.BG.copy()
    for tt in sorted(TENTS, key=lambda x: -x['ry']):
        ta = t - T_IN0 - tt['stagger']
        form = clamp01(ta / T_FORM); gone = clamp01((ta - T_FORM - T_SHOW) / T_FADE)
        if form <= 0 or gone >= 1: continue
        ang, Lk, bu = pose(tt, t)
        pts = fk(tt, ang, Lk)
        v = abs(np.interp(t, TT, tt['rootA']) - np.interp(t - 0.05, TT, tt['rootA'])) / 0.05
        if v > 175:   # 甩得快：前一刻的殘影（模擬動態模糊）
            a2, L2, _ = pose(tt, t - 0.07); p2 = fk(tt, a2, L2)
            sim.OPACITY = 0.18 * min(1.0, (v - 175) / 300)
            sim.render_tentacle(img, p2, tt['side'], dict(root=(tt['rx'], tt['ry']), rootR=tt['rx'], w0=tt['w0']), t, tt['seed'], form, gone)
        sim.OPACITY = 0.42 + 0.33 * min(1.0, v / 350)
        var = dict(root=(tt['rx'], tt['ry']), rootR=tt['rx'], w0=tt['w0'])
        sim.render_tentacle(img, pts, tt['side'], var, t, tt['seed'], form, gone)
    sim.comp_sprite(img)
    return img

if __name__ == '__main__':
    frames = []
    nfr = int(TOTAL * FPS)
    for f in range(nfr):
        frames.append(Image.fromarray((lin2srgb(frame(f / FPS)) * 255).astype(np.uint8)))
        if f % 30 == 0: print('frame', f, '/', nfr, flush=True)
    frames[0].save(OUT, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0)
    print('done')
