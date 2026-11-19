using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Dipan.Vfx
{
    /// <summary>
    /// 一隻靈獸（2026-11-19，DipanVfx；說明見 readme/DIPAN_VFX.md §4e）：放出來後在畫面裡亂游（或追目標），
    /// 時間到淡出自毀。只管「游」跟「畫」，**不做傷害**——遊戲端每隔一段時間讀 <see cref="BodyPoints"/>／<see cref="HalfWidths"/>
    /// 自己判定（主遊戲 <c>SpiritStrike</c>）。
    ///
    /// 組成（外觀全部來自 SpiritFxTable 一列）：
    /// <list type="bullet">
    /// <item>身體：沿中心線鋪一條帶子（<c>Custom/DipanSpiritBody</c>），把左右無縫的身體貼圖一節一節貼上去；頸粗尾細、尾端淡出。
    ///   往左游時整條上下翻，背鬃永遠在畫面上方。</item>
    /// <item>頭：側面頭圖（<c>Custom/DipanAddSprite</c>），錨點對在脖子；往左游上下翻；傾斜角限制在 ±HeadMaxTilt。</item>
    /// <item>爪：每個掛點一對（近側畫在身上、遠側畫在身下且暗），跟著游動前後划。</item>
    /// <item>纏身電弧（繞著身體、前半畫在上面後半畫在下面）＋尾端散開的電弧（<c>Custom/DipanArc</c>）。</item>
    /// </list>
    /// </summary>
    public sealed class SpiritBeast : MonoBehaviour
    {
        const string BodyShader = "Custom/DipanSpiritBody";
        const string SpriteShader = "Custom/DipanAddSprite";
        const string ArcShader = "Custom/DipanArc";
        const float BodyStep = 0.15f;

        static readonly int IdMainTex = Shader.PropertyToID("_MainTex");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdGlowColor = Shader.PropertyToID("_GlowColor");
        static readonly int IdGlowAmount = Shader.PropertyToID("_GlowAmount");
        static readonly int IdGlowWidth = Shader.PropertyToID("_GlowWidth");
        static readonly int IdFade = Shader.PropertyToID("_Fade");
        static readonly int IdDim = Shader.PropertyToID("_Dim");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdOpaque = Shader.PropertyToID("_Opaque");
        static readonly int IdTileLen = Shader.PropertyToID("_TileLen");
        static readonly int IdSmokeAmount = Shader.PropertyToID("_SmokeAmount");
        static readonly int IdSmokeColor = Shader.PropertyToID("_SmokeColor");
        static readonly int IdSmokeSpeed = Shader.PropertyToID("_SmokeSpeed");

        // ── 設定 ──
        int _fxId;
        float _scale = 1f;
        float _life = 10f;
        string _layer = "Default";
        public readonly SpiritBeastMotion Motion = new SpiritBeastMotion();

        /// <summary>畫面範圍（世界座標）；null＝用主相機的可見範圍。預覽器傳自己的舞台。</summary>
        public Func<Rect> Bounds;
        /// <summary>追蹤目標；null 或回傳 null＝亂游。遊戲端只在有追蹤等級時給。</summary>
        public Func<Vector2, Vector2?> Target;

        public SpiritFxData Data => SpiritFxDatabase.Get(_fxId);
        public float Age { get; private set; }
        /// <summary>還在「活著」（淡出中就不算，遊戲端不再判定傷害）。</summary>
        public bool IsActive => !_stopping;
        public float Scale => _scale;

        // ── 身體（遊戲端判定也讀這兩份）──
        readonly List<Vector2> _pts = new List<Vector2>(96);
        readonly List<float> _cum = new List<float>(96);
        readonly List<float> _half = new List<float>(96);
        public IReadOnlyList<Vector2> BodyPoints => _pts;
        /// <summary>每個身體點的可見半寬（世界單位，＝傷害判定半徑）。</summary>
        public IReadOnlyList<float> HalfWidths => _half;

        bool _stopping;
        float _stopT;
        float _seed;
        bool _headLeft;

        // ── 繪製物件 ──
        MeshRenderer _bodyMr, _arcFrontMr, _arcBackMr;
        Mesh _bodyMesh, _arcFrontMesh, _arcBackMesh;
        SpriteRenderer _head, _headGlow;

        // 身後的煙團（世界座標留在原地、慢慢散開上飄）
        struct Puff { public SpriteRenderer Sr; public Vector2 Pos; public float Age, Life, Size0, Grow, Rot; }
        readonly List<Puff> _puffs = new List<Puff>(64);
        readonly Stack<SpriteRenderer> _puffFree = new Stack<SpriteRenderer>();
        float _puffAcc;
        readonly List<SpriteRenderer> _clawNear = new List<SpriteRenderer>(), _clawFar = new List<SpriteRenderer>();
        MaterialPropertyBlock _mpb;

        readonly List<Vector3> _v = new List<Vector3>(256);
        readonly List<Vector2> _uv = new List<Vector2>(256);
        readonly List<Color> _c = new List<Color>(256);
        readonly List<int> _tri = new List<int>(512);
        readonly List<Vector3> _av = new List<Vector3>(512), _bv = new List<Vector3>(512);
        readonly List<Vector2> _auv = new List<Vector2>(512), _buv = new List<Vector2>(512);
        readonly List<Color> _ac = new List<Color>(512), _bc = new List<Color>(512);
        readonly List<int> _atri = new List<int>(1024), _btri = new List<int>(1024);
        readonly List<Vector2> _strip = new List<Vector2>(32);
        readonly List<float> _fa = new List<float>(32), _ba = new List<float>(32);

        struct Arc { public float S0, Age, Life, Phase, Amp; public int Seed; }
        Arc[] _arcs = new Arc[0];

        // ───────────────────────── 建立 ─────────────────────────

        /// <summary>
        /// 放一隻靈獸。<paramref name="scale"/>＝整體大小倍率（武器 BulletScale）；<paramref name="speed"/>＝游速（世界單位/秒）；
        /// <paramref name="life"/>＝游幾秒後淡出；<paramref name="turnRate"/>＝追蹤轉向（度/秒）。表裡沒有這列回 null。
        /// </summary>
        public static SpiritBeast Spawn(int fxId, Vector2 pos, float headingRad, float scale, float speed, float life,
                                        float turnRate = 180f, string sortingLayer = "Default", Transform parent = null)
        {
            var d = SpiritFxDatabase.Get(fxId);
            if (d == null) return null;
            var go = new GameObject("SpiritBeast " + d.Name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var sb = go.AddComponent<SpiritBeast>();
            sb._fxId = fxId;
            sb._scale = Mathf.Max(0.05f, scale);
            sb._life = Mathf.Max(0.1f, life);
            sb._layer = string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer;
            sb._seed = Random.value * 100f;
            sb.Motion.Speed = Mathf.Max(0.1f, speed);
            sb.Motion.TurnRate = turnRate;
            sb.Motion.Wander = d.Wander;
            sb.Motion.EdgeMargin = d.EdgeMargin;
            sb.Motion.Reset(pos, headingRad, d.BodyLength * sb._scale, sb._seed);
            sb.Build(d);
            return sb;
        }

        /// <summary>開始淡出（照表的 FadeOut），淡完自毀。重複呼叫無妨。</summary>
        public void Stop()
        {
            if (_stopping) return;
            _stopping = true;
            _stopT = 0f;
        }

        /// <summary>立刻消失（換場景、預覽器重設）。</summary>
        public void Kill() { if (this != null) Destroy(gameObject); }

        void Build(SpiritFxData d)
        {
            _mpb = new MaterialPropertyBlock();
            _bodyMesh = NewMesh("Body"); _bodyMr = NewMeshChild("Body", _bodyMesh, BodyShader, d.SortingOrder);
            _arcBackMesh = NewMesh("ArcBack"); _arcBackMr = NewMeshChild("ArcBack", _arcBackMesh, ArcShader, d.SortingOrder - 2);
            _arcFrontMesh = NewMesh("ArcFront"); _arcFrontMr = NewMeshChild("ArcFront", _arcFrontMesh, ArcShader, d.SortingOrder + 3);
            _head = NewSprite("Head", d.SortingOrder + 4);
            _headGlow = NewSprite("HeadGlow", d.SortingOrder + 5);
        }

        static Mesh NewMesh(string name)
        {
            var m = new Mesh { name = "SpiritBeast " + name, hideFlags = HideFlags.DontSave };
            m.MarkDynamic();
            return m;
        }

        MeshRenderer NewMeshChild(string name, Mesh mesh, string shader, int order)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = VfxAssets.SharedMaterial(shader);
            if (mat != null) mr.sharedMaterial = mat;
            mr.sortingLayerName = _layer;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        SpriteRenderer NewSprite(string name, int order)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            var mat = VfxAssets.SharedMaterial(SpriteShader);
            if (mat != null) sr.sharedMaterial = mat;
            sr.sortingLayerName = _layer;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        void OnDestroy()
        {
            if (_bodyMesh != null) Destroy(_bodyMesh);
            if (_arcFrontMesh != null) Destroy(_arcFrontMesh);
            if (_arcBackMesh != null) Destroy(_arcBackMesh);
        }

        // ───────────────────────── 每幀 ─────────────────────────

        static Rect CameraRect()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return default;
            float h = cam.orthographicSize * 2f, w = h * cam.aspect;
            Vector3 c = cam.transform.position;
            return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
        }

        void Update()
        {
            var d = Data;
            if (d == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            Age += dt;
            if (!_stopping && Age >= _life) Stop();
            if (_stopping)
            {
                _stopT += dt;
                if (_stopT >= d.FadeOut) { Destroy(gameObject); return; }
            }

            Motion.Wander = d.Wander;
            Motion.EdgeMargin = d.EdgeMargin * _scale;
            Rect b = Bounds != null ? Bounds() : CameraRect();
            Vector2? target = (!_stopping && Target != null) ? Target(Motion.Head) : null;
            Motion.Step(dt, b, target);
            Motion.BuildBody(_pts, _cum, d.BodyLength * _scale, BodyStep * _scale,
                             d.WaveAmp * _scale, d.WaveLength * _scale, d.WaveSpeed, Age + _seed);
            ComputeWidths(d);
        }

        void ComputeWidths(SpiritFxData d)
        {
            _half.Clear();
            float full = d.BodyLength * _scale;
            float w0 = d.BodyWidth * _scale;
            for (int i = 0; i < _cum.Count; i++)
            {
                float u = _cum[i] / full;
                float taper = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(d.TaperStart, 1f, u));
                _half.Add(w0 * (1f - 0.15f * u) * (1f - taper * (1f - d.TailWidth)) * 0.5f);
            }
        }

        void LateUpdate()
        {
            var d = Data;
            if (d == null || _pts.Count < 2)
            {
                if (_bodyMr != null) { _bodyMr.enabled = false; _arcFrontMr.enabled = false; _arcBackMr.enabled = false; }
                if (_head != null) _head.enabled = false;
                return;
            }
            float fade = Mathf.Clamp01(Age / d.FadeIn);
            if (_stopping) fade *= 1f - Mathf.Clamp01(_stopT / d.FadeOut);

            Vector2 hdir = _pts[0] - _pts[Mathf.Min(3, _pts.Count - 1)];
            if (hdir.sqrMagnitude < 1e-6f) hdir = new Vector2(Mathf.Cos(Motion.Heading), Mathf.Sin(Motion.Heading));
            // 左右翻轉加一點遲滯，免得接近垂直時一直翻來翻去
            if (_headLeft ? hdir.x > 0.15f * hdir.magnitude : hdir.x < -0.15f * hdir.magnitude) _headLeft = !_headLeft;

            BuildBodyMesh(d, fade);
            BuildArcs(d, fade);
            PlaceHead(d, hdir, fade);
            PlaceClaws(d, fade);
            UpdatePuffs(d, fade);
        }

        // ───────────────────────── 煙團 ─────────────────────────

        void UpdatePuffs(SpiritFxData d, float fade)
        {
            float dt = Time.deltaTime;
            // 發射：沿身體 3～9.5 成處隨機一點、往身體兩側偏一點；淡出中不再冒
            if (d.PuffRate > 0f && !_stopping && _pts.Count >= 2)
            {
                _puffAcc += d.PuffRate * dt;
                float L = _cum[_cum.Count - 1];
                while (_puffAcc >= 1f && _puffs.Count < 160)
                {
                    _puffAcc -= 1f;
                    Sample(Random.Range(0.3f, 0.95f) * L, out var p, out var fwd, out var half);
                    var n = new Vector2(-fwd.y, fwd.x);
                    var sr = _puffFree.Count > 0 ? _puffFree.Pop() : NewSprite("Puff", d.SortingOrder - 3);
                    sr.sprite = VfxAssets.SmokeSprite;
                    sr.sortingOrder = d.SortingOrder - 3;
                    sr.enabled = true;
                    _puffs.Add(new Puff
                    {
                        Sr = sr, Pos = p + n * Random.Range(-0.9f, 0.9f) * half, Age = 0f,
                        Life = d.PuffLife * Random.Range(0.7f, 1.3f), Size0 = half * d.PuffSize, Grow = d.PuffGrow, Rot = Random.value * 360f,
                    });
                }
                if (_puffAcc > 1f) _puffAcc = 1f;
            }

            Vector2 rise = new Vector2(0f, d.PuffRise * _scale);
            float opaque = 1f;   // 煙團一律會遮住地板（暗色煙）
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var pf = _puffs[i];
                pf.Age += dt;
                if (pf.Age >= pf.Life)
                {
                    pf.Sr.enabled = false;
                    _puffFree.Push(pf.Sr);
                    _puffs.RemoveAt(i);
                    continue;
                }
                float k = pf.Age / pf.Life;
                pf.Pos += rise * dt;
                float size = pf.Size0 * (1f + pf.Grow * k);
                pf.Sr.transform.position = new Vector3(pf.Pos.x, pf.Pos.y, 0f);
                float sw = pf.Sr.sprite != null ? pf.Sr.sprite.bounds.size.x : 1f;
                float sc = size / Mathf.Max(1e-4f, sw);
                pf.Sr.transform.localScale = new Vector3(sc, sc, 1f);
                pf.Sr.transform.rotation = Quaternion.Euler(0f, 0f, pf.Rot + k * 40f);   // 慢慢轉，像煙在翻
                pf.Sr.color = new Color(d.SmokeColor.r, d.SmokeColor.g, d.SmokeColor.b, Mathf.Sin(k * Mathf.PI) * d.PuffAlpha);
                SetSpriteProps(pf.Sr, 1f, 1f, fade, 1f, opaque);
                _puffs[i] = pf;
            }
        }

        // ───────────────────────── 身體 ─────────────────────────

        Vector2 Tangent(int i)   // 指向頭
        {
            Vector2 a = _pts[Mathf.Max(0, i - 1)], b = _pts[Mathf.Min(_pts.Count - 1, i + 1)];
            Vector2 t = a - b; float l = t.magnitude;
            return l > 1e-5f ? t / l : Vector2.right;
        }

        /// <summary>背的那一側（往右游＝上方；往左游時整條翻過來，背還是在上方）。</summary>
        Vector2 BackNormal(Vector2 towardHead)
        {
            var n = new Vector2(-towardHead.y, towardHead.x);
            return _headLeft ? -n : n;
        }

        void BuildBodyMesh(SpiritFxData d, float fade)
        {
            _v.Clear(); _uv.Clear(); _c.Clear(); _tri.Clear();
            bool show = d.Body != null && (d.BodyIntensity > 0f || d.SmokeAmount > 0f);
            _bodyMr.enabled = show;
            if (!show) { _bodyMesh.Clear(); return; }

            float full = d.BodyLength * _scale;
            float aspect = (float)d.Body.width / Mathf.Max(1, d.Body.height);
            float tileLen = Mathf.Max(0.05f, d.BodyWidth * _scale * aspect * d.TileScale);
            for (int i = 0; i < _pts.Count; i++)
            {
                Vector2 n = BackNormal(Tangent(i));
                float hg = _half[i] * d.GlowWidth;
                float u = _cum[i] / full;
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(d.TailFade, 1f, u));
                float s = _cum[i] / tileLen;
                var col = new Color(1f, 1f, 1f, a);
                _v.Add(_pts[i] - n * hg); _uv.Add(new Vector2(s, -1f)); _c.Add(col);
                _v.Add(_pts[i] + n * hg); _uv.Add(new Vector2(s, 1f)); _c.Add(col);
                if (i > 0)
                {
                    int k = (i - 1) * 2;
                    _tri.Add(k); _tri.Add(k + 1); _tri.Add(k + 3);
                    _tri.Add(k); _tri.Add(k + 3); _tri.Add(k + 2);
                }
            }
            _bodyMesh.Clear();
            _bodyMesh.SetVertices(_v);
            _bodyMesh.SetUVs(0, _uv);
            _bodyMesh.SetColors(_c);
            _bodyMesh.SetTriangles(_tri, 0, false);
            _bodyMesh.RecalculateBounds();

            _bodyMr.GetPropertyBlock(_mpb);
            _mpb.Clear();
            _mpb.SetTexture(IdMainTex, d.Body);
            _mpb.SetFloat(IdIntensity, d.BodyIntensity);
            _mpb.SetColor(IdGlowColor, d.GlowColor);
            _mpb.SetFloat(IdGlowAmount, d.GlowAmount);
            _mpb.SetFloat(IdGlowWidth, d.GlowWidth);
            _mpb.SetFloat(IdOpaque, Opaque(d));
            _mpb.SetFloat(IdTileLen, tileLen);
            _mpb.SetFloat(IdSmokeAmount, d.SmokeAmount);
            _mpb.SetColor(IdSmokeColor, d.SmokeColor);
            _mpb.SetFloat(IdSmokeSpeed, d.SmokeSpeed);
            _mpb.SetFloat(IdFade, fade);
            _mpb.SetFloat(IdDim, d.Dim);
            _bodyMr.SetPropertyBlock(_mpb);
        }

        void Sample(float s, out Vector2 pos, out Vector2 towardHead, out float half)
        {
            int n = _pts.Count;
            s = Mathf.Clamp(s, 0f, _cum[n - 1]);
            int i = 0;
            while (i < n - 2 && _cum[i + 1] < s) i++;
            float seg = _cum[i + 1] - _cum[i];
            float k = seg > 1e-5f ? (s - _cum[i]) / seg : 0f;
            pos = Vector2.Lerp(_pts[i], _pts[i + 1], k);
            Vector2 t = _pts[i] - _pts[i + 1]; float l = t.magnitude;
            towardHead = l > 1e-5f ? t / l : Vector2.right;
            half = Mathf.Lerp(_half[i], _half[i + 1], k);
        }

        // ───────────────────────── 頭、爪 ─────────────────────────

        void PlaceHead(SpiritFxData d, Vector2 hdir, float fade)
        {
            _head.enabled = d.Head != null;
            if (d.Head == null) { _headGlow.enabled = false; return; }
            _head.sprite = d.Head;
            _head.flipY = _headLeft;
            float ang = Mathf.Atan2(hdir.y, hdir.x) * Mathf.Rad2Deg;
            if (_headLeft)
            {
                float rel = Mathf.DeltaAngle(180f, ang);
                ang = 180f + Mathf.Clamp(rel, -d.HeadMaxTilt, d.HeadMaxTilt);
            }
            else ang = Mathf.Clamp(Mathf.DeltaAngle(0f, ang), -d.HeadMaxTilt, d.HeadMaxTilt);
            PlaceSprite(_head, d.Head, d.HeadWidth * _scale, d.HeadAnchor, _pts[0], ang, _headLeft);
            SetSpriteProps(_head, d.HeadIntensity, 1f, fade, d.Dim, Opaque(d));

            // 發光層（紅眼等）：跟頭圖同尺寸同裁切 ⇒ 同一個擺法；永遠加色、會脈動
            _headGlow.enabled = d.HeadGlow != null;
            if (d.HeadGlow == null) return;
            _headGlow.sprite = d.HeadGlow;
            _headGlow.flipY = _headLeft;
            PlaceSprite(_headGlow, d.HeadGlow, d.HeadWidth * _scale, d.HeadAnchor, _pts[0], ang, _headLeft);
            float pulse = 1f + d.HeadGlowPulse * Mathf.Sin(Age * d.HeadGlowPulseSpeed + _seed);
            SetSpriteProps(_headGlow, d.HeadGlowIntensity, pulse, fade, d.Dim, 0f);
        }

        static float Opaque(SpiritFxData d) => d.Blend == SpiritBlend.Alpha ? 1f : 0f;

        /// <summary>把 sprite 放成寬 <paramref name="width"/>、以 <paramref name="anchor"/>（從左、從上的比例）對準 <paramref name="at"/>、轉 <paramref name="angleDeg"/>。</summary>
        static void PlaceSprite(SpriteRenderer sr, Sprite sp, float width, Vector2 anchor, Vector2 at, float angleDeg, bool flipY)
        {
            Vector2 size = sp.bounds.size;
            float sc = size.x > 1e-5f ? width / size.x : 1f;
            // sprite 本地座標（以 pivot 為原點）中錨點的位置
            Vector2 piv = sp.pivot / sp.rect.size;   // 0~1，從左下
            Vector2 local = new Vector2((anchor.x - piv.x) * size.x, ((1f - anchor.y) - piv.y) * size.y);
            if (flipY) local.y = -local.y;   // SpriteRenderer.flipY 以 pivot 為軸上下翻
            local *= sc;
            Quaternion q = Quaternion.Euler(0f, 0f, angleDeg);
            Vector2 off = q * local;
            sr.transform.position = new Vector3(at.x - off.x, at.y - off.y, 0f);
            sr.transform.rotation = q;
            sr.transform.localScale = new Vector3(sc, sc, 1f);
        }

        void SetSpriteProps(SpriteRenderer sr, float intensity, float bright, float fade, float dim, float opaque)
        {
            sr.GetPropertyBlock(_mpb);
            _mpb.Clear();
            if (sr.sprite != null) _mpb.SetTexture(IdMainTex, sr.sprite.texture);
            _mpb.SetFloat(IdIntensity, intensity * bright);
            _mpb.SetFloat(IdFade, fade);
            _mpb.SetFloat(IdDim, dim);
            _mpb.SetFloat(IdOpaque, opaque);
            sr.SetPropertyBlock(_mpb);
        }

        void PlaceClaws(SpiritFxData d, float fade)
        {
            int need = d.Claw != null ? d.ClawAt.Length : 0;
            while (_clawNear.Count < need)
            {
                _clawNear.Add(NewSprite("ClawNear", d.SortingOrder + 2));
                _clawFar.Add(NewSprite("ClawFar", d.SortingOrder - 1));
            }
            for (int k = 0; k < _clawNear.Count; k++)
            {
                bool on = k < need;
                _clawNear[k].enabled = on; _clawFar[k].enabled = on;
                if (!on) continue;
                float s = d.ClawAt[k] * _cum[_cum.Count - 1];
                Sample(s, out var p, out var fwd, out var half);
                Vector2 belly = -BackNormal(fwd);
                float ang = Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg;
                float swing = d.ClawSwing * Mathf.Sin(Age * d.ClawSwingSpeed + k * 1.7f) * (_headLeft ? -1f : 1f);
                float w = d.ClawWidth * d.BodyWidth * _scale;
                // 遠側：往頭那邊錯開一點、暗；近側：畫在身上
                Vector2 farPos = p + belly * half * 0.62f + fwd * 0.35f * half;
                Vector2 nearPos = p + belly * half * 0.62f - fwd * 0.1f * half;
                float tilt = _headLeft ? -1f : 1f;
                PutClaw(_clawFar[k], d, w, farPos, ang + swing + 8f * tilt, d.ClawFarDim, fade);
                PutClaw(_clawNear[k], d, w, nearPos, ang + swing - 6f * tilt, 1f, fade);
            }
        }

        void PutClaw(SpriteRenderer sr, SpiritFxData d, float width, Vector2 pos, float angle, float bright, float fade)
        {
            sr.sprite = d.Claw;
            sr.flipY = _headLeft;
            PlaceSprite(sr, d.Claw, width, d.ClawAnchor, pos, angle, _headLeft);
            // 遠側那隻：加色模式＝調暗；遮擋模式＝亮度照常（暗色生物本來就黑），只是畫在身體底下
            SetSpriteProps(sr, d.HeadIntensity, d.Blend == SpiritBlend.Alpha ? 1f : bright, fade, d.Dim, Opaque(d));
        }

        // ───────────────────────── 電弧 ─────────────────────────

        static float Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x8da6b343u ^ (uint)b * 0xd8163841u ^ (uint)c * 0xcb1ab31fu;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        void BuildArcs(SpiritFxData d, float fade)
        {
            _av.Clear(); _auv.Clear(); _ac.Clear(); _atri.Clear();
            _bv.Clear(); _buv.Clear(); _bc.Clear(); _btri.Clear();
            float L = _cum[_cum.Count - 1];
            int tick = Mathf.FloorToInt((Age + _seed) * Mathf.Max(0.01f, d.ArcRate));
            float halfW = d.ArcWidth * _scale * 1.5f;
            float dt = Time.deltaTime;

            // 纏身電弧
            if (_arcs.Length != d.ArcCount)
            {
                _arcs = new Arc[d.ArcCount];
                for (int i = 0; i < _arcs.Length; i++) Respawn(ref _arcs[i], d, L, true, i);
            }
            float len = d.ArcLength * _scale;
            int steps = Mathf.Max(2, Mathf.CeilToInt(d.ArcLength / 0.12f));
            for (int i = 0; i < _arcs.Length; i++)
            {
                ref Arc a = ref _arcs[i];
                a.Age += dt;
                if (a.Age >= a.Life) Respawn(ref a, d, L, false, i);
                float lifeEnv = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(a.Age / a.Life) * Mathf.PI)));
                _strip.Clear(); _fa.Clear(); _ba.Clear();
                for (int q = 0; q <= steps; q++)
                {
                    float u = (float)q / steps;
                    float s = a.S0 + u * len;
                    if (s < 0f || s > L) continue;
                    Sample(s, out var p, out var t, out var half);
                    Vector2 n = new Vector2(-t.y, t.x);
                    float ph = s / _scale * d.ArcTwist * Mathf.PI * 2f + a.Phase - Age * 5f;
                    float jit = (Hash(a.Seed, tick, q) * 2f - 1f) * 0.12f * half;
                    _strip.Add(p + n * (Mathf.Sin(ph) * half * 1.05f * a.Amp + jit));
                    float env = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))) * lifeEnv;
                    float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.3f, 0.3f, Mathf.Cos(ph)));
                    _fa.Add(env * front); _ba.Add(env * (1f - front) * 0.45f);
                }
                AddStrip(_strip, _fa, halfW, _av, _auv, _ac, _atri);
                AddStrip(_strip, _ba, halfW, _bv, _buv, _bc, _btri);
            }

            // 尾巴散開的電弧
            if (d.TailArcs > 0 && _pts.Count >= 6)
            {
                Vector2 tail = _pts[_pts.Count - 1];
                Vector2 dir = (tail - _pts[_pts.Count - 6]).normalized;
                float segLen = d.TailArcLength * _scale / 5f;
                for (int k = 0; k < d.TailArcs; k++)
                {
                    _strip.Clear(); _fa.Clear();
                    _strip.Add(tail); _fa.Add(1f);
                    float spread = d.TailArcs > 1 ? (k / (float)(d.TailArcs - 1) - 0.5f) : 0f;
                    float ang = spread + (Hash(k, tick, 5) - 0.5f) * 0.6f;
                    Vector2 dk = Rot(dir, ang);
                    Vector2 q = tail;
                    for (int j = 0; j < 5; j++)
                    {
                        float jit = (Hash(k + 9, tick, j) - 0.5f) * 1.2f;
                        Vector2 step = (dk + new Vector2(-dk.y, dk.x) * jit).normalized;
                        q += step * segLen;
                        _strip.Add(q); _fa.Add(1f - (j + 1) / 6f);
                    }
                    for (int j = 0; j < _fa.Count; j++) _fa[j] *= d.TailArcIntensity / Mathf.Max(0.01f, d.ArcIntensity);   // 跟纏身電弧共用材質＝同色，亮度用頂點 a 調
                    AddStrip(_strip, _fa, halfW * 0.8f, _av, _auv, _ac, _atri);
                }
            }

            Flush(_arcFrontMesh, _av, _auv, _ac, _atri);
            Flush(_arcBackMesh, _bv, _buv, _bc, _btri);
            _arcFrontMr.enabled = _av.Count > 0;
            _arcBackMr.enabled = _bv.Count > 0;
            SetArcProps(_arcFrontMr, d, fade);
            SetArcProps(_arcBackMr, d, fade);
        }

        static Vector2 Rot(Vector2 v, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        void Respawn(ref Arc a, SpiritFxData d, float L, bool initial, int i)
        {
            a.Age = initial ? Random.value * 0.3f : 0f;
            a.Life = Random.Range(0.25f, 0.45f);
            a.S0 = Random.Range(-0.5f * _scale, Mathf.Max(0f, L * 0.8f));
            a.Phase = Random.value * Mathf.PI * 2f;
            a.Amp = Random.Range(0.85f, 1.25f);
            a.Seed = Random.Range(1, 1 << 20) + i * 7919;
        }

        void SetArcProps(MeshRenderer mr, SpiritFxData d, float fade)
        {
            mr.GetPropertyBlock(_mpb);
            _mpb.Clear();
            _mpb.SetColor(IdColor, d.ArcColor);
            _mpb.SetFloat(IdIntensity, d.ArcIntensity);
            _mpb.SetFloat(IdFade, fade);
            _mpb.SetFloat(IdDim, d.Dim);
            mr.SetPropertyBlock(_mpb);
        }

        static void Flush(Mesh m, List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> tri)
        {
            m.Clear();
            if (v.Count == 0) return;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetColors(c);
            m.SetTriangles(tri, 0, false);
            m.RecalculateBounds();
        }

        static void AddStrip(List<Vector2> pts, List<float> alpha, float halfW,
                             List<Vector3> v, List<Vector2> uv, List<Color> col, List<int> tri)
        {
            int n = pts.Count;
            if (n < 2 || v.Count + n * 2 > 6000) return;
            bool any = false;
            for (int i = 0; i < n; i++) if (alpha[i] > 0.01f) { any = true; break; }
            if (!any) return;
            int baseIdx = v.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 dPrev = i > 0 ? (pts[i] - pts[i - 1]) : (pts[1] - pts[0]);
                Vector2 dNext = i < n - 1 ? (pts[i + 1] - pts[i]) : dPrev;
                Vector2 dir = dPrev.normalized + dNext.normalized;
                if (dir.sqrMagnitude < 1e-6f) dir = dNext;
                dir.Normalize();
                Vector2 nrm = new Vector2(-dir.y, dir.x) * halfW;
                float u = (float)i / (n - 1);
                var c = new Color(1f, 1f, 1f, alpha[i]);
                v.Add(pts[i] - nrm); uv.Add(new Vector2(u, -1f)); col.Add(c);
                v.Add(pts[i] + nrm); uv.Add(new Vector2(u, 1f)); col.Add(c);
                if (i > 0)
                {
                    int a = baseIdx + (i - 1) * 2;
                    tri.Add(a); tri.Add(a + 1); tri.Add(a + 3);
                    tri.Add(a); tri.Add(a + 3); tri.Add(a + 2);
                }
            }
        }
    }
}
