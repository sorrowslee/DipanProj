using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Vfx
{
    /// <summary>
    /// **一次爆炸**（2026-11-18，DipanVfx；ExplosionTable 一列＝一種，說明見 readme/DIPAN_VFX.md §4b）。
    ///
    /// 一次爆炸＝五層：
    /// <list type="number">
    /// <item>白光：瞬間亮一下就收（加色光點）。</item>
    /// <item>火球（<c>Custom/DipanFireball</c>）：幾團錯開炸開、從小膨脹，白熱 → 橘 → 暗紅，外緣先冷成黑煙、被噪聲溶掉，邊燒邊往上飄。</item>
    /// <item>黑煙：火球之後冒出、慢慢升起散掉（同一個 shader 的黑煙模式，會遮住地板）。</item>
    /// <item>衝擊波圈（<c>Custom/DipanShockRing</c>）：貼地，從中心擴到**範圍半徑**＝真正打得到的範圍。</item>
    /// <item>火星：往外噴。</item>
    /// </list>
    /// 火球／黑煙／白光的大小用 min(範圍半徑, MaxVisualRadius)，大範圍的爆炸也不會整個畫面被火蓋住；衝擊波圈不受限。
    /// 傷害**不在這裡**。物件池：演完 SetActive(false) 放回去。
    /// </summary>
    public sealed class ExplosionBurst : MonoBehaviour
    {
        const string FireballShader = "Custom/DipanFireball";
        const string RingShader = "Custom/DipanShockRing";
        const string GlowShader = "Custom/DipanGlowDot";

        static readonly Stack<ExplosionBurst> _pool = new Stack<ExplosionBurst>();
        static Transform _poolRoot;

        static readonly int IdColorCore = Shader.PropertyToID("_ColorCore");
        static readonly int IdColorMid = Shader.PropertyToID("_ColorMid");
        static readonly int IdColorEdge = Shader.PropertyToID("_ColorEdge");
        static readonly int IdSmokeColor = Shader.PropertyToID("_SmokeColor");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdAdditive = Shader.PropertyToID("_Additive");
        static readonly int IdAge = Shader.PropertyToID("_Age");
        static readonly int IdSmoke = Shader.PropertyToID("_Smoke");
        static readonly int IdSwirl = Shader.PropertyToID("_Swirl");
        static readonly int IdSeed = Shader.PropertyToID("_Seed");
        static readonly int IdDim = Shader.PropertyToID("_Dim");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdWidth = Shader.PropertyToID("_Width");
        static readonly int IdAlpha = Shader.PropertyToID("_Alpha");

        public static void ResetForPlayMode()
        {
            _pool.Clear();
            _poolRoot = null;
        }

        /// <summary>
        /// 在 <paramref name="pos"/> 炸一次 ExplosionTable 第 <paramref name="id"/> 列。
        /// <paramref name="radius"/>＝範圍半徑（&lt;=0 用表的 Radius）；<paramref name="order"/>＝火球的排序（一般特效層），
        /// <paramref name="groundOrder"/>＝衝擊波圈的排序（地面特效層）。
        /// </summary>
        public static void Spawn(int id, Vector2 pos, float radius, string sortingLayer, int order, int groundOrder)
            => Spawn(ExplosionDatabase.Get(id), pos, radius, sortingLayer, order, groundOrder);

        public static void Spawn(ExplosionData d, Vector2 pos, float radius, string sortingLayer, int order, int groundOrder)
        {
            if (d == null) return;
            var b = Take();
            b.Play(d, pos, radius > 0f ? radius : d.Radius, string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer, order, groundOrder);
        }

        static ExplosionBurst Take()
        {
            while (_pool.Count > 0)
            {
                var b = _pool.Pop();
                if (b != null) { b.gameObject.SetActive(true); return b; }
            }
            if (_poolRoot == null) _poolRoot = new GameObject("DipanVfx ExplosionPool").transform;
            var go = new GameObject("Explosion");
            go.transform.SetParent(_poolRoot, false);
            var eb = go.AddComponent<ExplosionBurst>();
            eb._flash = eb.NewRenderer("Flash");
            eb._ring = eb.NewRenderer("Ring");
            return eb;
        }

        SpriteRenderer NewRenderer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<SpriteRenderer>();
        }

        sealed class Puff
        {
            public SpriteRenderer sr;
            public bool smoke, active;
            public float delay, life, size0, size1, rise, seed;
            public Vector2 off;
        }

        readonly List<Puff> _puffs = new List<Puff>();
        SpriteRenderer _flash, _ring;
        MaterialPropertyBlock _mpb;
        ExplosionData _d;
        float _t, _end, _radius, _vis;

        void Play(ExplosionData d, Vector2 pos, float radius, string layer, int order, int groundOrder)
        {
            _d = d;
            _t = 0f;
            _radius = radius;
            _vis = Mathf.Min(radius, d.MaxVisualRadius);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            transform.position = new Vector3(pos.x, pos.y, 0f);
            _end = 0f;

            // ── 火球與黑煙 ──
            int fire = d.HasSprites ? Random.Range(d.FireballMin, d.FireballMax + 1) : 0;
            int smoke = d.HasSprites ? d.SmokeCount : 0;
            int total = fire + smoke;
            while (_puffs.Count < total) _puffs.Add(new Puff { sr = NewRenderer("Puff") });
            var mat = VfxAssets.SharedMaterial(FireballShader);
            for (int i = 0; i < _puffs.Count; i++)
            {
                var p = _puffs[i];
                p.active = i < total;
                p.sr.gameObject.SetActive(p.active);
                if (!p.active) continue;
                p.smoke = i >= fire;
                var sp = d.Sprites[Random.Range(0, d.Sprites.Length)];
                p.sr.sprite = sp;
                p.sr.flipX = Random.value < 0.5f;
                if (mat != null) p.sr.sharedMaterial = mat;
                p.sr.sortingLayerName = layer;
                p.seed = Random.value * 40f;
                float spriteW = Mathf.Max(0.0001f, sp.bounds.size.x);
                if (!p.smoke)
                {
                    p.delay = i == 0 ? 0f : Random.Range(0f, d.Stagger);
                    p.life = d.FireballLife * Random.Range(0.85f, 1.15f);
                    float dia = _vis * d.FireballSize * (i == 0 ? 1.15f : Random.Range(0.75f, 1f));
                    p.size1 = dia / spriteW;
                    p.size0 = p.size1 * d.FireballGrow;
                    p.off = i == 0 ? Vector2.zero : Random.insideUnitCircle * _vis * d.FireballSpread;
                    p.rise = d.FireballRise * _vis * Random.Range(0.7f, 1.2f);
                    p.sr.sortingOrder = order + (i == 0 ? fire : i);   // 第一團（最大、在中心）畫在最前面
                }
                else
                {
                    p.delay = d.SmokeDelay + Random.Range(0f, 0.12f);
                    p.life = d.SmokeLife * Random.Range(0.85f, 1.15f);
                    float dia = _vis * d.SmokeSize * Random.Range(0.8f, 1.1f);
                    p.size1 = dia / spriteW;
                    p.size0 = p.size1 * 0.55f;
                    p.off = Random.insideUnitCircle * _vis * d.FireballSpread * 0.8f;
                    p.rise = d.SmokeRise * _vis * Random.Range(0.8f, 1.2f);
                    p.sr.sortingOrder = order - 10 + (i - fire);   // 煙在火球後面
                }
                p.sr.GetPropertyBlock(_mpb);
                _mpb.SetColor(IdColorCore, d.ColorCore);
                _mpb.SetColor(IdColorMid, d.ColorMid);
                _mpb.SetColor(IdColorEdge, d.ColorEdge);
                _mpb.SetColor(IdSmokeColor, d.SmokeColor);
                _mpb.SetFloat(IdIntensity, d.Intensity);
                _mpb.SetFloat(IdAdditive, d.Additive);
                _mpb.SetFloat(IdSmoke, p.smoke ? 1f : 0f);
                _mpb.SetFloat(IdSwirl, d.Swirl);
                _mpb.SetFloat(IdSeed, p.seed);
                _mpb.SetFloat(IdDim, d.Dim);
                _mpb.SetFloat(IdAge, 0f);
                _mpb.SetTexture(VfxAssets.IdNoiseTex, VfxAssets.NoiseTexture);
                p.sr.SetPropertyBlock(_mpb);
                _end = Mathf.Max(_end, p.delay + p.life);
                ApplyPuff(p, 0f);
            }

            // ── 白光 ──
            _flash.gameObject.SetActive(d.FlashSize > 0f);
            if (d.FlashSize > 0f)
            {
                _flash.sprite = VfxAssets.DotSprite;
                var gm = VfxAssets.SharedMaterial(GlowShader);
                if (gm != null) _flash.sharedMaterial = gm;
                _flash.sortingLayerName = layer;
                _flash.sortingOrder = order + total + 5;
                _end = Mathf.Max(_end, d.FlashTime);
            }

            // ── 衝擊波圈（貼地、畫在真正的範圍上）──
            _ring.gameObject.SetActive(d.RingWidth > 0f);
            if (d.RingWidth > 0f)
            {
                _ring.sprite = VfxAssets.SquareSprite;
                var rm = VfxAssets.SharedMaterial(RingShader);
                if (rm != null) _ring.sharedMaterial = rm;
                _ring.sortingLayerName = layer;
                _ring.sortingOrder = groundOrder;
                _ring.GetPropertyBlock(_mpb);
                _mpb.SetColor(IdColor, d.RingColor);
                _mpb.SetFloat(IdIntensity, d.RingIntensity);
                _mpb.SetFloat(IdWidth, d.RingWidth);
                _mpb.SetFloat(IdSeed, Random.value * 20f);
                _mpb.SetFloat(IdDim, d.Dim);
                _mpb.SetTexture(VfxAssets.IdNoiseTex, VfxAssets.NoiseTexture);
                _ring.SetPropertyBlock(_mpb);
                _end = Mathf.Max(_end, d.RingTime * 1.6f);
            }

            // ── 火星：一次噴出 ──
            float sp0 = d.SparkSpeed * Mathf.Max(0.5f, _vis / 1.5f);
            for (int i = 0; i < d.SparkCount; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                VfxEmbers.Emit(pos + dir * _vis * 0.2f, dir * sp0 * Random.Range(0.5f, 1.2f) + Vector2.up * 0.5f,
                               d.SparkColor, d.SparkSize * Random.Range(0.6f, 1.4f), Random.Range(0.35f, 0.8f),
                               layer, order + total + 6);
            }

            UpdateFlashRing();
        }

        void ApplyPuff(Puff p, float t)
        {
            float local = t - p.delay;
            if (local < 0f) { p.sr.enabled = false; return; }
            p.sr.enabled = true;
            float age = Mathf.Clamp01(local / p.life);
            float grow = 1f - (1f - age) * (1f - age) * (1f - age);          // easeOutCubic：一下子膨脹
            float s = Mathf.Lerp(p.size0, p.size1, grow);
            p.sr.transform.localScale = new Vector3(s, s, 1f);
            p.sr.transform.localPosition = new Vector3(p.off.x, p.off.y + p.rise * age, 0f);
            p.sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdAge, age);
            p.sr.SetPropertyBlock(_mpb);
        }

        void UpdateFlashRing()
        {
            var d = _d;
            if (_flash.gameObject.activeSelf)
            {
                float k = Mathf.Clamp01(_t / d.FlashTime);
                float s = _vis * d.FlashSize * Mathf.Lerp(0.5f, 1.2f, 1f - (1f - k) * (1f - k));
                _flash.transform.localScale = new Vector3(s, s, 1f);
                var c = d.FlashColor; c.a = (1f - k) * (1f - k);
                _flash.color = c;
            }
            if (_ring.gameObject.activeSelf)
            {
                float k = Mathf.Clamp01(_t / d.RingTime);
                float grow = 1f - (1f - k) * (1f - k);
                // 圈畫在 UV 半徑 0.92 ⇒ 方塊邊長＝2R/0.92 時圈剛好在 R
                float s = 2f * _radius / 0.92f * Mathf.Lerp(0.15f, 1f, grow);
                _ring.transform.localScale = new Vector3(s, s, 1f);
                float fade = Mathf.Clamp01((_t - d.RingTime * 0.6f) / (d.RingTime));
                _ring.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IdAlpha, 1f - fade);
                _ring.SetPropertyBlock(_mpb);
            }
        }

        void Update()
        {
            if (_d == null) { Recycle(); return; }
            _t += Time.deltaTime;
            for (int i = 0; i < _puffs.Count; i++)
                if (_puffs[i].active) ApplyPuff(_puffs[i], _t);
            UpdateFlashRing();
            if (_t >= _end + 0.02f) Recycle();
        }

        void Recycle()
        {
            _d = null;
            gameObject.SetActive(false);
            _pool.Push(this);
        }
    }
}
