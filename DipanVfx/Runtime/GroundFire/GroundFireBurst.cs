using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Vfx
{
    /// <summary>
    /// **地上燃燒**（2026-11-18，DipanVfx；GroundFireTable 一列＝一種，說明見 readme/DIPAN_VFX.md §4）。
    ///
    /// 一團地上燃燒＝三層：
    /// <list type="number">
    /// <item>焦土（shader <c>Custom/DipanScorch</c>，貼地、排在地面特效層）：起火時從中心往外燒開，裂紋餘燼一明一暗；
    ///   結束時餘燼由外往內冷卻，焦痕再留一下才淡掉。</item>
    /// <item>火舌（<see cref="FlameVisual"/>，立著、**跟角色一起 Y 排序**）：範圍內散著幾條，各燒一陣就換到別處重燃，
    ///   所以整片火一直在變；起火時錯開冒出，結束時一條條熄滅。</item>
    /// <item>火星（<see cref="VfxEmbers"/>）：從範圍內往上飄。</item>
    /// </list>
    /// 傷害**不在這裡**——仍是 GroundEffect 打的，這裡純畫面。物件池：用完 SetActive(false) 放回去。
    /// </summary>
    public sealed class GroundFireBurst : MonoBehaviour
    {
        const string ScorchShader = "Custom/DipanScorch";
        const int MaxTongues = 40;

        static readonly Stack<GroundFireBurst> _pool = new Stack<GroundFireBurst>();
        static Transform _poolRoot;

        static readonly int IdGlowTex = Shader.PropertyToID("_GlowTex");
        static readonly int IdGlowColor = Shader.PropertyToID("_GlowColor");
        static readonly int IdGlowIntensity = Shader.PropertyToID("_GlowIntensity");
        static readonly int IdReveal = Shader.PropertyToID("_Reveal");
        static readonly int IdHeat = Shader.PropertyToID("_Heat");
        static readonly int IdAlpha = Shader.PropertyToID("_Alpha");
        static readonly int IdSeed = Shader.PropertyToID("_Seed");
        static readonly int IdDim = Shader.PropertyToID("_Dim");

        public static void ResetForPlayMode()
        {
            _pool.Clear();
            _poolRoot = null;
        }

        /// <summary>
        /// 一團正在燒的火的「把手」：物件池會重用物件，所以帶著世代號，避免對已經換成別團的物件下指令。
        /// </summary>
        public struct Handle
        {
            readonly GroundFireBurst _b;
            readonly int _gen;
            internal Handle(GroundFireBurst b, int gen) { _b = b; _gen = gen; }
            public bool Valid => _b != null && _b._gen == _gen && _b._d != null;
            /// <summary>提早結束（開始熄滅）；已經在熄滅或早就結束了就不動。</summary>
            public void Stop() { if (Valid) _b.BeginEnd(); }
        }

        /// <summary>
        /// 在 <paramref name="pos"/> 燒 GroundFireTable 第 <paramref name="id"/> 列：範圍半徑 <paramref name="radius"/>、
        /// 燒 <paramref name="duration"/> 秒（&lt;0＝直到 <see cref="Handle.Stop"/>；熄滅落在最後 FadeTime 秒內）。
        /// <paramref name="groundOrder"/>＝焦土的排序（主遊戲地面特效＝8）；火舌另依 Y 排序。
        /// </summary>
        public static Handle Spawn(int id, Vector2 pos, float radius, float duration, string sortingLayer, int groundOrder)
            => Spawn(GroundFireDatabase.Get(id), pos, radius, duration, sortingLayer, groundOrder);

        public static Handle Spawn(GroundFireData d, Vector2 pos, float radius, float duration, string sortingLayer, int groundOrder)
        {
            if (d == null) return default;
            var b = Take();
            b.Play(d, pos, Mathf.Max(0.05f, radius), duration, sortingLayer, groundOrder);
            return new Handle(b, b._gen);
        }

        static GroundFireBurst Take()
        {
            while (_pool.Count > 0)
            {
                var b = _pool.Pop();
                if (b != null) { b.gameObject.SetActive(true); return b; }
            }
            if (_poolRoot == null) _poolRoot = new GameObject("DipanVfx GroundFirePool").transform;
            var go = new GameObject("GroundFire");
            go.transform.SetParent(_poolRoot, false);
            var gf = go.AddComponent<GroundFireBurst>();
            var sc = new GameObject("Scorch");
            sc.transform.SetParent(go.transform, false);
            gf._scorch = sc.AddComponent<SpriteRenderer>();
            return gf;
        }

        struct Slot
        {
            public FlameVisual flame;
            public int gen;
            public float next;     // 下一次在這格重燃的時間
        }

        GroundFireData _d;
        int _gen;
        SpriteRenderer _scorch;
        MaterialPropertyBlock _mpb;
        readonly List<Slot> _slots = new List<Slot>();
        string _layer;
        int _groundOrder;
        float _radius, _t, _endStart, _emberAcc, _seed;

        void Play(GroundFireData d, Vector2 pos, float radius, float duration, string layer, int groundOrder)
        {
            _d = d;
            _gen++;
            _layer = string.IsNullOrEmpty(layer) ? "Default" : layer;
            _groundOrder = groundOrder;
            _radius = radius;
            _t = 0f;
            _emberAcc = 0f;
            _seed = Random.value * 50f;
            _endStart = duration >= 0f ? Mathf.Max(0f, duration - d.FadeTime) : float.PositiveInfinity;
            transform.position = new Vector3(pos.x, pos.y, 0f);

            // 焦土
            if (d.Scorch != null)
            {
                _scorch.gameObject.SetActive(true);
                _scorch.sprite = d.Scorch;
                var mat = VfxAssets.SharedMaterial(ScorchShader);
                if (mat != null) _scorch.sharedMaterial = mat;
                _scorch.sortingLayerName = _layer;
                _scorch.sortingOrder = groundOrder;
                _scorch.flipX = Random.value < 0.5f;
                _scorch.transform.localRotation = Quaternion.identity;
                float diameter = radius * 2f * d.ScorchSize;
                float s = diameter / Mathf.Max(0.0001f, d.Scorch.bounds.size.x);
                _scorch.transform.localScale = new Vector3(s, s, 1f);
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                _scorch.GetPropertyBlock(_mpb);
                _mpb.SetTexture(IdGlowTex, d.Glow != null ? d.Glow.texture : Texture2D.blackTexture);
                _mpb.SetTexture(VfxAssets.IdNoiseTex, VfxAssets.NoiseTexture);
                _mpb.SetColor(IdGlowColor, d.GlowColor);
                _mpb.SetFloat(IdGlowIntensity, d.Glow != null ? d.GlowIntensity : 0f);
                _mpb.SetFloat(IdSeed, _seed);
                _mpb.SetFloat(IdDim, d.Dim);
                _scorch.SetPropertyBlock(_mpb);
            }
            else _scorch.gameObject.SetActive(false);

            // 火舌格子：數量按範圍半徑放大（半徑 1 時 CountMin～CountMax）
            _slots.Clear();
            int count = 0;
            if (d.FlameID > 0 && d.CountMax > 0)
                count = Mathf.Clamp(Mathf.RoundToInt(Random.Range(d.CountMin, d.CountMax + 1) * Mathf.Max(0.5f, radius)), 1, MaxTongues);
            for (int i = 0; i < count; i++)
                _slots.Add(new Slot { next = i == 0 ? 0f : Random.Range(0f, d.StaggerTime) });

            UpdateScorch();
        }

        /// <summary>開始收尾（提早結束用；已在收尾中不動）。</summary>
        void BeginEnd()
        {
            if (_d == null || _t >= _endStart) return;
            _endStart = _t;
        }

        void Update()
        {
            if (_d == null) { Recycle(); return; }
            float dt = Time.deltaTime;
            _t += dt;
            var d = _d;
            bool ending = _t >= _endStart;

            // 火舌：到時間就在範圍內隨機一點重燃；收尾時全部熄滅
            var flame = d.FlameID > 0 ? d.Flame : null;
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                bool alive = s.flame != null && s.flame.IsAlive && s.flame.Generation == s.gen;
                if (ending)
                {
                    if (alive) s.flame.Stop();
                    s.flame = null;
                }
                else if (flame != null && _t >= s.next)
                {
                    float u = Mathf.Sqrt(Random.value);                       // 面積均勻
                    Vector2 off = Random.insideUnitCircle.normalized * u * _radius * d.Fill;
                    Vector3 at = transform.position + new Vector3(off.x, off.y, 0f);
                    float size = d.FlameSize * Mathf.Lerp(1.15f, 0.7f, u);   // 中心的大、外圈的小
                    float life = d.TongueLife > 0f
                        ? d.TongueLife * Random.Range(0.8f, 1.2f)
                        : (float.IsInfinity(_endStart) ? -1f : _endStart - _t + flame.FadeTime);
                    var fv = FlameVisual.Spawn(flame, at, size / Mathf.Max(0.0001f, flame.Size), life,
                                               _layer, VfxAssets.OrderForY(at.y, _groundOrder + 1 + i));
                    s.flame = fv;
                    s.gen = fv != null ? fv.Generation : 0;
                    // 上一條還在熄的時候下一條就冒出來，畫面上不會有空檔
                    s.next = life > 0f ? _t + Mathf.Max(0.1f, life - flame.FadeTime) : float.PositiveInfinity;
                }
                _slots[i] = s;
            }

            // 火星
            float emberEnd = _endStart + d.FadeTime;
            if (d.EmberRate > 0f && _t < emberEnd && flame != null)
            {
                _emberAcc += d.EmberRate * Mathf.Max(0.5f, _radius) * dt * (ending ? 0.4f : 1f);
                while (_emberAcc >= 1f)
                {
                    _emberAcc -= 1f;
                    Vector2 off = Random.insideUnitCircle * _radius * d.Fill;
                    Vector2 at = (Vector2)transform.position + off + new Vector2(0f, Random.Range(0.1f, 0.5f) * d.FlameSize);
                    VfxEmbers.Emit(at, new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.6f, 1.2f)),
                                   flame.EmberColor, flame.EmberSize * Random.Range(0.7f, 1.4f), Random.Range(0.6f, 1.2f),
                                   _layer, VfxAssets.OrderForY(at.y, _groundOrder + 1 + MaxTongues) + 1);
                }
            }

            UpdateScorch();

            float end = _endStart + d.FadeTime + d.ScorchLinger + 0.05f;
            if (_t >= end) Recycle();
        }

        void UpdateScorch()
        {
            if (_d == null || !_scorch.gameObject.activeSelf) return;
            var d = _d;
            float reveal = Mathf.Clamp01(_t / d.IgniteTime);
            reveal = 1f - (1f - reveal) * (1f - reveal);
            float heat = 1f, alpha = 1f;
            if (_t >= _endStart)
            {
                heat = 1f - Mathf.Clamp01((_t - _endStart) / d.FadeTime);
                float lingerStart = _endStart + d.FadeTime;
                alpha = d.ScorchLinger > 0f ? 1f - Mathf.Clamp01((_t - lingerStart) / d.ScorchLinger)
                                            : (_t >= lingerStart ? 0f : 1f);
            }
            _scorch.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdReveal, reveal);
            _mpb.SetFloat(IdHeat, heat);
            _mpb.SetFloat(IdAlpha, alpha);
            _scorch.SetPropertyBlock(_mpb);
        }

        void Recycle()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.flame != null && s.flame.IsAlive && s.flame.Generation == s.gen) s.flame.Stop();
            }
            _slots.Clear();
            _d = null;
            gameObject.SetActive(false);
            _pool.Push(this);
        }
    }
}
