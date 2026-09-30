using System;
using UnityEngine;

namespace Sorrows.Ballistics
{
    /// <summary>
    /// 迴旋（RecipeTable <c>Mode=Boomerang</c>）。軌跡分三段，接點的方向與彎度都連續（不會有轉折）：
    /// <list type="number">
    /// <item><b>出手（Out）</b>：淚滴前半——沿瞄準方向出手、慢慢往<b>左</b>彎，橫著繞到終點（瞄準線上、距離 range）。</item>
    /// <item><b>繞圈（Loop，趟數 &gt; 1 才有）</b>：以擁有者為中心的橢圓，順時針從一個終點繞到另一個終點
    ///   （前 → 從擁有者<b>右側</b>掠過 → 身後 → 從<b>左側</b>掠過 → 前……），前後交替＝鐘擺感，
    ///   但**只從擁有者旁邊經過、不穿身體**——擁有者只是參考點。每半圈＝一趟。</item>
    /// <item><b>收回（Return）</b>：淚滴後半——從最後一個終點繞回來，尖端落在擁有者身上＝收回（銷毀）。
    ///   **只有這一次會回到擁有者身上。**</item>
    /// </list>
    /// 趟數 N ⇒ 出手 ＋ (N−1) 個半圈 ＋ 收回；經過的終點依序是 前、後、前、後……共 N 個。
    ///
    /// <para><b>為什麼不是每趟一個淚滴</b>（2026-09-30 第二版就是那樣）：尖端固定在擁有者身上，每趟回來都得急轉、瞄準身體中心穿過去，
    /// 數學上等速、看起來卻像「減速後硬擠過去」（作者：像蝴蝶在飛）。改成繞擁有者轉，中間幾趟就沒有尖端了。</para>
    ///
    /// <para><b>幾何</b>（相對「軸」＝第一趟的發射方向、「左」＝軸逆時針 90°）：
    /// 淚滴 前進＝range·(1−cosθ)/2、側向＝k·sinθ·(1−cosθ)，θ∈[0,2π]，總寬＝range×<see cref="WidthRatio"/>；
    /// 橢圓 半長軸＝range（端點就是終點）、半短軸 b＝2√2·k——這個 b 讓橢圓端點的曲率半徑（b²/range）
    /// 剛好等於淚滴終點的曲率半徑（8k²/range），所以出手→繞圈、繞圈→收回的接點連彎度都一樣。</para>
    ///
    /// <para><b>擁有者移動</b>：出手段錨點固定在出手點（不被拖著走）；繞圈段的中心以時間常數 <see cref="CenterFollowSeconds"/>
    /// 平滑追擁有者（進入繞圈時中心＝出手點，位置連續不跳）；收回段錨點用 smoothstep 從當時的中心漸移到擁有者身上 ⇒ **一定回到手上**。
    /// 繞圈與收回時，擁有者往反方向跑會把圈往後拖、抵掉前進，所以這兩段的實際速度**不低於 Speed**（<see cref="AdvanceWorld"/>）。</para>
    ///
    /// <para><b>等速</b>：每幀用二分法求「沿路徑前進 speed·dt」對應的參數（<see cref="Advance"/>）；一段走完剩下的步長直接帶進下一段。</para>
    ///
    /// <para><b>命中名單</b>：每經過一個終點、以及繞圈段掠過擁有者身旁（半圈的一半）各清一次（<see cref="BulletInstance.ClearHitHistory"/>）
    /// ——等於每趟「去一次、回一次」都能再打同一隻，趟數 N ⇒ 同一隻最多被打 2N 次。</para>
    ///
    /// <para><b>壽命由本行為接管</b>（OnSpawn 把 LifeTime 設 -1）。保險：**每一段**各自計時，上限 ＝ (該段長度 ÷ speed) ×
    /// <see cref="TripTimeSlack"/> ＋ <see cref="TripTimeExtra"/>；超時或擁有者被銷毀就淡出消失。</para>
    ///
    /// <para>穿牆／無限穿怪不是這裡做的：主遊戲把配方的 PierceCount 固定成 -1、牆放進可穿透層，所以 <see cref="OnHit"/> 一律回 false。
    /// 每顆子彈一個實例（有狀態），由發射端透過 <c>BallisticsEngine.Spawn(..., extraBehavior)</c> 工廠掛上。</para>
    /// </summary>
    public class BoomerangBehavior : IBulletBehavior
    {
        /// <summary>出手／收回淚滴的總寬 ÷ range（0.5 ＝ 射程 5 時寬 2.5）。作者 2026-09-30 拍板先寫死、不開配方欄。
        /// 繞圈橢圓的寬由它推出來（半短軸 ＝ 2√2·k，射程 5 時橢圓總寬約 5.4，離擁有者約 2.7）。</summary>
        public const float WidthRatio = 0.5f;
        /// <summary>繞圈段中心追擁有者的時間常數（秒）：越小跟得越緊，越大圈越「甩」。</summary>
        public const float CenterFollowSeconds = 0.35f;
        /// <summary>每段時間上限的寬限倍率。</summary>
        public const float TripTimeSlack = 2f;
        /// <summary>每段時間上限再加的固定秒數。</summary>
        public const float TripTimeExtra = 0.5f;
        /// <summary>超時／擁有者不見時的淡出秒數（期間不再命中任何東西）。</summary>
        public const float FadeSeconds = 0.25f;

        private enum Seg { Out, Loop, Return }

        private const float TwoPi = Mathf.PI * 2f;
        // sin(120°)·(1−cos120°) ＝ 淚滴側向函數 sinθ(1−cosθ) 的最大值
        private static readonly float SideMax = Mathf.Sin(TwoPi / 3f) * 1.5f;

        private readonly Transform _owner;
        private readonly Func<Vector2> _returnPoint;
        private readonly float _range;
        private readonly int _trips;

        private float _speed;
        private float _k;            // 淚滴側向係數
        private float _b;            // 繞圈橢圓半短軸
        private Vector2 _axis;       // 第一趟的發射方向（整把都用這條軸）
        private Vector2 _left;       // 軸的左手邊
        private Seg _seg;
        private float _p;            // 這一段的參數：Out 0→π、Loop 0→π、Return π→2π
        private int _sigma;          // +1＝目前的終點在前方，-1＝在身後
        private int _loopsLeft;
        private Vector2 _anchor;     // Out：出手點；Return：開始收回時的中心
        private Vector2 _center;     // Loop：繞圈中心（平滑追擁有者）
        private Vector2 _retCache;   // 這一幀擁有者的回程點（二分法會問很多次，只取一次）
        private float _tearHalfLen, _loopHalfLen;
        private float _segTimer, _segLimit;

        private bool _fading;
        private float _fadeTimer;
        private Vector2 _fadeVelocity;
        private SpriteRenderer _sr;
        private Color _baseColor;
        private bool _finished;

        /// <param name="owner">擁有者（丟出去的人）。被銷毀＝淡出。</param>
        /// <param name="returnPoint">繞圈中心／收回的點（例：玩家身體中心）；null＝<paramref name="owner"/> 的 position。</param>
        /// <param name="range">終點距離（世界單位）。</param>
        /// <param name="trips">趟數（最少 1）。</param>
        public BoomerangBehavior(Transform owner, Func<Vector2> returnPoint, float range, int trips)
        {
            _owner = owner;
            _returnPoint = returnPoint;
            _range = Mathf.Max(0.1f, range);
            _trips = Mathf.Max(1, trips);
        }

        public void OnSpawn(BulletInstance instance)
        {
            instance.LifeTime = -1f;   // 壽命改由本行為管（收回／超時淡出）
            _speed = Mathf.Max(0.01f, instance.Velocity.magnitude);
            _axis = instance.Velocity.sqrMagnitude > 1e-8f ? instance.Velocity.normalized : Vector2.right;
            _left = new Vector2(-_axis.y, _axis.x);
            _anchor = instance.transform.position;
            _center = _anchor;
            _retCache = _anchor;
            _k = (_range * WidthRatio * 0.5f) / SideMax;
            _b = 2f * Mathf.Sqrt(2f) * _k;
            _seg = Seg.Out;
            _p = 0f;
            _sigma = 1;
            _loopsLeft = _trips - 1;
            _tearHalfLen = HalfLength(true);
            _loopHalfLen = HalfLength(false);
            ResetSegTimer();
        }

        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime)
        {
            if (_finished) { velocity = Vector2.zero; return; }
            // 暫停中（dt=0）：速度不動——設成 0 的話 BulletInstance 會依速度方向轉圖，暫停瞬間圖會轉到 0 度；位移本來就是 velocity×0
            if (deltaTime <= 0f) return;

            if (_fading)
            {
                _fadeTimer += deltaTime;
                float t = Mathf.Clamp01(_fadeTimer / FadeSeconds);
                velocity = _fadeVelocity * (1f - t);
                if (_sr != null)
                {
                    Color c = _baseColor;
                    c.a = _baseColor.a * (1f - t);
                    _sr.color = c;
                }
                if (t >= 1f) Finish(instance, ref velocity);
                return;
            }

            // Unity 的 == null 對已銷毀物件也成立 ⇒ 擁有者不見就淡出，不去呼叫 _returnPoint
            if (_owner == null) { BeginFade(instance, velocity); return; }

            _retCache = _returnPoint != null ? _returnPoint() : (Vector2)_owner.position;
            if (_seg == Seg.Loop)
                _center = Vector2.Lerp(_center, _retCache, 1f - Mathf.Exp(-deltaTime / CenterFollowSeconds));

            _segTimer += deltaTime;
            if (_segTimer > _segLimit) { BeginFade(instance, velocity); return; }

            Vector2 pos = instance.transform.position;
            float remain = _speed * deltaTime;

            // 最多三輪：一幀之內走完一段，剩下的步長直接帶進下一段（出手→繞圈→收回的接點才不會頓一下）
            for (int pass = 0; pass < 3; pass++)
            {
                float end = SegEnd();
                float next = Advance(_p, remain, end);

                // 繞圈／收回時被擁有者往後拖：沿路徑多推進，讓實際速度不低於 Speed（不會停在半空）
                if ((_seg == Seg.Loop || (_seg == Seg.Return && next > Mathf.PI)) && next < end
                    && (World(next) - pos).magnitude < remain)
                    next = AdvanceWorld(next, pos, remain, end);

                if (next < end)
                {
                    // 繞圈段掠過擁有者身旁（半圈的一半）：這一趟的「回程」，可以再打一次
                    if (_seg == Seg.Loop && _p < Mathf.PI * 0.5f && next >= Mathf.PI * 0.5f) instance.ClearHitHistory();
                    _p = next;
                    velocity = (World(_p) - pos) / deltaTime;
                    return;
                }

                // ── 這一段走完 ──
                Vector2 endPos = World(end);
                remain = Mathf.Max(0f, remain - (endPos - pos).magnitude);

                if (_seg == Seg.Return)
                {
                    Finish(instance, ref velocity);   // 回到擁有者身上：收回（同一幀銷毀）
                    return;
                }

                instance.ClearHitHistory();   // 經過一個終點
                if (_seg == Seg.Out) { _center = _anchor; _sigma = 1; }   // 繞圈中心從出手點開始（位置連續）
                else _sigma = -_sigma;                                    // 繞到另一頭的終點

                if (_loopsLeft > 0) { _loopsLeft--; _seg = Seg.Loop; _p = 0f; }
                else { _seg = Seg.Return; _anchor = _center; _p = Mathf.PI; }
                ResetSegTimer();
            }

            // 保險（理論上走不到：一幀不可能走完三段）
            velocity = (World(_p) - pos) / deltaTime;
        }

        public bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity) => false;

        // ── 路徑 ──

        /// <summary>淚滴位移（軸＝sg·axis，左右跟著反）。</summary>
        private Vector2 Tear(float th, int sg)
        {
            float c = Mathf.Cos(th), s = Mathf.Sin(th);
            float fwd = _range * (1f - c) * 0.5f;
            float side = _k * s * (1f - c);
            return (_axis * fwd + _left * side) * sg;
        }

        /// <summary>繞圈橢圓相對中心的位移：t＝0 在 sg 那一頭的終點，順時針走到 t＝π 另一頭的終點。</summary>
        private Vector2 LoopOffset(float t, int sg)
            => (_axis * (_range * Mathf.Cos(t)) - _left * (_b * Mathf.Sin(t))) * sg;

        private Vector2 Local(float t)
        {
            switch (_seg)
            {
                case Seg.Out:  return Tear(t, 1);
                case Seg.Loop: return LoopOffset(t, _sigma);
                default:       return Tear(t, _sigma);
            }
        }

        private float SegEnd() => _seg == Seg.Return ? TwoPi : Mathf.PI;

        /// <summary>這一段參數 t 的世界座標。</summary>
        private Vector2 World(float t)
        {
            switch (_seg)
            {
                case Seg.Out:  return _anchor + Local(t);
                case Seg.Loop: return _center + Local(t);
                default:
                {
                    // 收回：錨點 smoothstep 從開始收回時的中心漸移到擁有者身上（t＝2π 時正好在身上）
                    float u = Mathf.Clamp01((t - Mathf.PI) / Mathf.PI);
                    float w = u * u * (3f - 2f * u);
                    return Vector2.Lerp(_anchor, _retCache, w) + Local(t);
                }
            }
        }

        /// <summary>t 到 t+Δ 的直線距離（每幀步長很短，弦長≈弧長）。</summary>
        private float Chord(float t, float d) => (Local(t + d) - Local(t)).magnitude;

        /// <summary>
        /// 從 t 沿路徑前進 <paramref name="dist"/>，回傳新的 t；會走到段尾就回 <paramref name="end"/>。
        /// 用「小步長倍增 → 二分」：淚滴尖端附近前進量與 Δt 是二次關係，導數線性估計會嚴重低估；
        /// 也不能直接拿「到段尾的弦長」判斷（淚滴一整圈的起點與終點是同一點）。
        /// </summary>
        private float Advance(float t, float dist, float end)
        {
            if (dist <= 0f) return t;
            float maxD = end - t;
            float lo = 0f, hi = Mathf.Min(0.05f, maxD);
            for (int g = 0; g < 16 && Chord(t, hi) < dist; g++)
            {
                if (hi >= maxD) return end;   // 剩下的路不到一步
                lo = hi;
                hi = Mathf.Min(hi * 2f, maxD);
            }
            if (Chord(t, hi) < dist) return end;
            for (int i = 0; i < 14; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Chord(t, mid) < dist) lo = mid; else hi = mid;
            }
            return t + hi;
        }

        /// <summary>從 t0 往後找「離 pos 的世界距離達到 dist」的 t；到段尾都不夠遠就回 end。</summary>
        private float AdvanceWorld(float t0, Vector2 pos, float dist, float end)
        {
            if ((World(end) - pos).magnitude < dist) return end;
            float lo = t0, hi = end;   // 不變式：lo 還不夠遠、hi 已經夠遠
            for (int i = 0; i < 16; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if ((World(mid) - pos).magnitude < dist) lo = mid; else hi = mid;
            }
            return hi;
        }

        /// <summary>淚滴半段（tear=true）或橢圓半圈的長度（數值積分，OnSpawn 算一次，給時間上限用）。</summary>
        private float HalfLength(bool tear)
        {
            const int N = 64;
            float len = 0f;
            Vector2 prev = tear ? Tear(0f, 1) : LoopOffset(0f, 1);
            for (int i = 1; i <= N; i++)
            {
                float t = Mathf.PI * i / N;
                Vector2 p = tear ? Tear(t, 1) : LoopOffset(t, 1);
                len += (p - prev).magnitude;
                prev = p;
            }
            return len;
        }

        private void ResetSegTimer()
        {
            _segTimer = 0f;
            float len = _seg == Seg.Loop ? _loopHalfLen : _tearHalfLen;
            _segLimit = len / _speed * TripTimeSlack + TripTimeExtra;
        }

        // ── 收尾 ──

        private void BeginFade(BulletInstance instance, Vector2 currentVelocity)
        {
            _fading = true;
            _fadeTimer = 0f;
            _fadeVelocity = currentVelocity;
            instance.CollisionMask = 0;   // 淡出期間不再命中任何東西（看起來是力盡，不該還在傷人）
            _sr = instance.GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        private void Finish(BulletInstance instance, ref Vector2 velocity)
        {
            _finished = true;
            velocity = Vector2.zero;
            instance.LifeTime = 0f;   // BulletInstance.Update 結尾倒數 ⇒ 同一幀銷毀（同 ParabolicBehavior 落地）
        }
    }
}
