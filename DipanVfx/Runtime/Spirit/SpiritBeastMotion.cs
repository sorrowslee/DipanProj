using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Vfx
{
    /// <summary>
    /// 靈獸的游法（2026-11-19，DipanVfx；說明見 readme/DIPAN_VFX.md §4e）。純計算、不畫東西：
    /// <list type="bullet">
    /// <item>頭：等速往前游；前進方向用幾個不同頻率的正弦疊出「平滑亂轉」；靠近畫面邊緣就往中間轉（不碰牆、不出畫面）。</item>
    /// <item>追蹤：有目標時以 <see cref="TurnRate"/>（度/秒）轉向目標，亂轉減弱成三成。</item>
    /// <item>身體：頭走過的軌跡往回取 BodyLength 那麼長（像貪食蛇），再加一道往尾巴傳的左右擺動。
    ///   剛放出來時軌跡還不夠長 ⇒ 身體從出生點「長出來」。</item>
    /// </list>
    /// 遊戲端（傷害判定）與畫面端（<see cref="SpiritBeast"/>）讀同一份 <see cref="BuildBody"/> 的結果。
    /// </summary>
    public sealed class SpiritBeastMotion
    {
        public Vector2 Head { get; private set; }
        /// <summary>前進方向（弧度）。</summary>
        public float Heading { get; private set; }
        public float Speed = 4f;
        /// <summary>追蹤轉向速度（度/秒）；有目標才用。</summary>
        public float TurnRate = 180f;
        public float Wander = 1f;
        public float EdgeMargin = 2.2f;

        const float TrailStep = 0.05f;
        readonly List<Vector2> _trail = new List<Vector2>(256);   // 舊 → 新
        float _t, _seed, _trailKeep = 12f;
        readonly List<Vector2> _normals = new List<Vector2>(128);

        public void Reset(Vector2 pos, float heading, float bodyLength, float seed)
        {
            Head = pos;
            Heading = heading;
            _seed = seed;
            _t = 0f;
            _trail.Clear();
            _trail.Add(pos);
            _trailKeep = bodyLength + 2f;
        }

        /// <summary>往前游一步。<paramref name="bounds"/>＝畫面範圍（世界座標；寬或高 ≤0 ⇒ 不限制）；<paramref name="target"/>＝追蹤目標（null＝亂游）。</summary>
        public void Step(float dt, Rect bounds, Vector2? target)
        {
            if (dt <= 0f) return;
            _t += dt;
            float s = _seed;
            float wander = Wander * (1.3f * Mathf.Sin(0.9f * _t + s) + 0.8f * Mathf.Sin(2.3f * _t + s * 2f) + 0.5f * Mathf.Sin(4.1f * _t + s * 3f));
            float turn;   // 弧度/秒
            if (target.HasValue)
            {
                Vector2 to = target.Value - Head;
                float want = Mathf.Atan2(to.y, to.x);
                float diff = Mathf.DeltaAngle(Heading * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float maxTurn = TurnRate * Mathf.Deg2Rad;
                turn = Mathf.Clamp(diff * 6f, -maxTurn, maxTurn) + wander * 0.3f;
            }
            else turn = wander;

            // 畫面邊緣：越出內框越用力轉回中間
            if (bounds.width > 0f && bounds.height > 0f)
            {
                float m = Mathf.Min(EdgeMargin, Mathf.Min(bounds.width, bounds.height) * 0.3f);
                float outX = Mathf.Max(0f, bounds.xMin + m - Head.x) + Mathf.Max(0f, Head.x - (bounds.xMax - m));
                float outY = Mathf.Max(0f, bounds.yMin + m - Head.y) + Mathf.Max(0f, Head.y - (bounds.yMax - m));
                float outside = outX + outY;
                if (outside > 0f)
                {
                    Vector2 c = bounds.center - Head;
                    float want = Mathf.Atan2(c.y, c.x);
                    float diff = Mathf.DeltaAngle(Heading * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                    turn += diff * Mathf.Clamp(outside / Mathf.Max(0.1f, m), 0f, 1f) * 4f;
                }
            }

            Heading += turn * dt;
            Head += new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * Speed * dt;

            if ((Head - _trail[_trail.Count - 1]).sqrMagnitude >= TrailStep * TrailStep) _trail.Add(Head);
            // 只留夠用的長度
            float acc = 0f;
            for (int i = _trail.Count - 1; i > 0; i--)
            {
                acc += (_trail[i] - _trail[i - 1]).magnitude;
                if (acc > _trailKeep) { _trail.RemoveRange(0, i - 1); break; }
            }
        }

        /// <summary>
        /// 取身體中心線（頭 → 尾），每 <paramref name="step"/> 一點，最多 <paramref name="length"/> 長；加上擺動。
        /// <paramref name="cum"/>＝每點到頭的距離。
        /// </summary>
        public void BuildBody(List<Vector2> pts, List<float> cum, float length, float step,
                              float waveAmp, float waveLength, float waveSpeed, float time)
        {
            pts.Clear(); cum.Clear();
            pts.Add(Head); cum.Add(0f);
            Vector2 prev = Head;
            float acc = 0f, next = step;
            // 從最新往回走（最新那點可能還沒進 _trail，先從 Head 接上）
            for (int i = _trail.Count - 1; i >= 0 && acc < length; i--)
            {
                Vector2 p = _trail[i];
                float seg = (p - prev).magnitude;
                while (seg > 1e-5f && acc + seg >= next && next <= length)
                {
                    float k = (next - acc) / seg;
                    pts.Add(Vector2.Lerp(prev, p, k));
                    cum.Add(next);
                    next += step;
                }
                acc += seg;
                prev = p;
            }
            if (pts.Count < 2) return;

            // 擺動：沿法線、往尾巴傳；頸部不擺、中段最大
            if (waveAmp <= 0f) return;
            _normals.Clear();
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(pts.Count - 1, i + 1)];
                Vector2 t = (a - b); float tl = t.magnitude;
                _normals.Add(tl < 1e-5f ? Vector2.zero : new Vector2(-t.y, t.x) / tl);
            }
            for (int i = 1; i < pts.Count; i++)
            {
                float c = cum[i];
                float env = Mathf.Clamp01(c / 1.2f) * (1f - 0.3f * c / Mathf.Max(0.01f, length));
                pts[i] += _normals[i] * (waveAmp * Mathf.Sin(c * Mathf.PI * 2f / waveLength - time * waveSpeed) * env);
            }
        }
    }
}
