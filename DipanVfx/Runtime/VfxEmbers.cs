using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Vfx
{
    /// <summary>
    /// 火星／餘燼粒子（2026-11-18，DipanVfx）：一顆顆加色小光點往上飄、左右晃、越飄越小越淡。
    /// 一個元件管全部粒子（不是一顆一個 MonoBehaviour），上限 <see cref="MaxParticles"/> 顆，滿了就丟掉新的。
    /// 火焰（<see cref="FlameVisual"/>）與地上燃燒（<see cref="GroundFireBurst"/>）共用。
    /// </summary>
    public sealed class VfxEmbers : MonoBehaviour
    {
        const string ShaderName = "Custom/DipanGlowDot";
        public const int MaxParticles = 400;

        static VfxEmbers _inst;

        struct P
        {
            public SpriteRenderer sr;
            public Vector2 pos, vel;
            public float age, life, size, phase;
            public Color color;
        }

        readonly List<P> _live = new List<P>();
        readonly Stack<SpriteRenderer> _free = new Stack<SpriteRenderer>();

        public static void ResetForPlayMode() { _inst = null; }

        /// <summary>
        /// 冒一顆火星。<paramref name="vel"/>＝初速（世界單位/秒；一般給往上 0.6～1.2）；
        /// 之後會自己往上浮、左右晃、變小變淡。
        /// </summary>
        public static void Emit(Vector2 pos, Vector2 vel, Color color, float size, float life,
                                string sortingLayer, int sortingOrder)
        {
            var m = Instance;
            if (m == null || m._live.Count >= MaxParticles) return;
            var sr = m.TakeRenderer();
            sr.sortingLayerName = string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer;
            sr.sortingOrder = sortingOrder;
            m._live.Add(new P
            {
                sr = sr, pos = pos, vel = vel, age = 0f, life = Mathf.Max(0.05f, life), size = size,
                phase = Random.value * 10f, color = color,
            });
            Apply(m._live[m._live.Count - 1]);
        }

        static VfxEmbers Instance
        {
            get
            {
                if (_inst != null) return _inst;
                var go = new GameObject("DipanVfx Embers");
                _inst = go.AddComponent<VfxEmbers>();
                return _inst;
            }
        }

        SpriteRenderer TakeRenderer()
        {
            while (_free.Count > 0)
            {
                var r = _free.Pop();
                if (r != null) { r.gameObject.SetActive(true); return r; }
            }
            var go = new GameObject("Ember");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VfxAssets.DotSprite;
            var mat = VfxAssets.SharedMaterial(ShaderName);
            if (mat != null) sr.sharedMaterial = mat;
            return sr;
        }

        static void Apply(P p)
        {
            float k = Mathf.Clamp01(p.age / p.life);
            float a = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;   // 先亮起來再慢慢暗
            var c = p.color; c.a *= a;
            p.sr.color = c;
            p.sr.transform.position = new Vector3(p.pos.x, p.pos.y, 0f);
            p.sr.transform.localScale = Vector3.one * Mathf.Max(0.001f, p.size * (1f - 0.6f * k));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                if (p.sr == null) { _live.RemoveAt(i); continue; }   // 換場景被銷毀
                p.age += dt;
                if (p.age >= p.life)
                {
                    p.sr.gameObject.SetActive(false);
                    _free.Push(p.sr);
                    _live.RemoveAt(i);
                    continue;
                }
                // 往上浮、慢慢減速；左右蛇行
                p.vel.y += 0.35f * dt;
                p.vel *= 1f - 0.9f * dt;
                float wob = Mathf.Sin((p.age + p.phase) * 7f) * 0.35f;
                p.pos += (p.vel + new Vector2(wob, 0f)) * dt;
                _live[i] = p;
                Apply(p);
            }
        }

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
        }
    }
}
