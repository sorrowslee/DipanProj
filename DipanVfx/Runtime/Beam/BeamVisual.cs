using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Vfx
{
    /// <summary>
    /// 光束外觀（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §4d）。只負責「畫」，不做任何判定：
    /// 路徑由遊戲端給（雷射＝<c>LaserBeam.Points</c>、連鎖＝目標連線、掃射＝源點→落點）。
    ///
    /// 組成（全部程序、不用圖）：
    /// <list type="bullet">
    /// <item>光柱：沿路徑鋪一條<b>連續</b>帶子（緩彎用斜接、頭尾圓頭；急轉折＝反彈點斷開成兩段，接縫藏在反彈光點底下），
    ///   shader <c>Custom/DipanBeam</c> 畫白熱核心＋流紋往前推＋能量脈衝鼓包＋外鞘翻騰（鏡光再加七彩色散與滑動亮紋）。</item>
    /// <item>電弧：<c>Custom/DipanArc</c>。螺旋＝一小段一小段繞著光柱往前竄（繞到後面變暗、畫在光柱底下）；
    ///   沿路徑＝整條鋸齒從頭連到尾、每秒重抽幾十次（電光／連鎖閃電）。</item>
    /// <item>砲口光球、命中光球、反彈光點：<c>Custom/DipanBeamOrb</c>；命中點噴火花（<see cref="VfxEmbers"/>）。</item>
    /// </list>
    ///
    /// 用法：
    /// <list type="bullet">
    /// <item>持續光束：<see cref="Create"/> 後 <see cref="Track"/>(beam.Points)，放開時 <see cref="Stop"/>。
    ///   <see cref="Track"/> 在 LateUpdate 讀清單 ⇒ 一定拿到這一幀算好的路徑，砲口不會落後一幀。</item>
    /// <item>一閃即逝（連鎖閃電）：<see cref="Flash"/>。</item>
    /// <item>每幀重餵、停餵自己淡出（掃射）：<see cref="SetPath"/>(pts, fadeLife)。</item>
    /// </list>
    /// 物件不放池：用完自己 Destroy；呼叫端用 Unity 的 <c>== null</c> 判斷「已經沒了」即可（被 Destroy 的物件 == null 為真）。
    /// </summary>
    public sealed class BeamVisual : MonoBehaviour
    {
        /// <summary>光束預設排序（沿用舊 LaserBeam 的 22000：永遠蓋在地圖與角色上）。</summary>
        public const int DefaultSortingOrder = 22000;

        const string BeamShader = "Custom/DipanBeam";
        const string ArcShader = "Custom/DipanArc";
        const string OrbShader = "Custom/DipanBeamOrb";

        /// <summary>
        /// 轉折角超過這個（度）就斷開光柱成兩段（各自圓頭、疊在轉角上）；以下用斜接連續帶子。
        /// 120° 剛好是斜接長度上限 2 倍半寬的位置。原本 70°，但寬光束（鏡光）一般反彈就會斷開、兩個圓頭疊出一團白（2026-11-18 改）。
        /// </summary>
        const float BreakAngle = 120f;
        /// <summary>轉折角超過這個（度）就在轉折點畫反彈光點（JointSize&gt;0 時）；追蹤彎曲每一步只轉幾度，不會誤觸。</summary>
        const float JointAngle = 25f;
        const float FadeInTime = 0.05f;
        const int MaxArcVerts = 6000;

        static readonly int IdColorCore = Shader.PropertyToID("_ColorCore");
        static readonly int IdColorMid = Shader.PropertyToID("_ColorMid");
        static readonly int IdColorEdge = Shader.PropertyToID("_ColorEdge");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdCore = Shader.PropertyToID("_Core");
        static readonly int IdSolid = Shader.PropertyToID("_Solid");
        static readonly int IdPad = Shader.PropertyToID("_PadScale");
        static readonly int IdFlow = Shader.PropertyToID("_Flow");
        static readonly int IdStreak = Shader.PropertyToID("_Streak");
        static readonly int IdPulseSpacing = Shader.PropertyToID("_PulseSpacing");
        static readonly int IdPulseAmt = Shader.PropertyToID("_PulseAmt");
        static readonly int IdPulseSpeed = Shader.PropertyToID("_PulseSpeed");
        static readonly int IdEdgeNoise = Shader.PropertyToID("_EdgeNoise");
        static readonly int IdChroma = Shader.PropertyToID("_Chroma");
        static readonly int IdShimmer = Shader.PropertyToID("_Shimmer");
        static readonly int IdShimmerSpacing = Shader.PropertyToID("_ShimmerSpacing");
        static readonly int IdShimmerSpeed = Shader.PropertyToID("_ShimmerSpeed");
        static readonly int IdFlicker = Shader.PropertyToID("_Flicker");
        static readonly int IdRays = Shader.PropertyToID("_Rays");
        static readonly int IdStar = Shader.PropertyToID("_Star");
        static readonly int IdFade = Shader.PropertyToID("_Fade");
        static readonly int IdSeed = Shader.PropertyToID("_Seed");
        static readonly int IdDim = Shader.PropertyToID("_Dim");

        // ── 狀態 ──
        int _fxId;
        float _width = 0.5f;
        string _layer = "Default";
        int _order = DefaultSortingOrder;
        float _seed, _age, _fadeIn;
        IReadOnlyList<Vector2> _track;
        readonly List<Vector2> _src = new List<Vector2>(64);   // SetPath／Flash 給的路徑（複本）
        readonly List<Vector2> _pts = new List<Vector2>(64);   // 這一幀要畫的路徑（去重、截長後）
        readonly List<float> _cum = new List<float>(64);        // _pts 的累積長度
        float _length;

        enum Life { Live, Stopping, Flash, FeedFade }
        Life _life = Life.Live;
        float _lifeT, _lifeLen, _sinceFeed;

        /// <summary>要不要畫砲口光球（掃射有自己的源點光球，關掉）。</summary>
        public bool ShowMuzzle = true;
        /// <summary>要不要畫命中光球與火花。</summary>
        public bool ShowImpact = true;

        public int FxId { get => _fxId; set => _fxId = value; }
        /// <summary>命中寬度（可見寬＝這個 × 表格 WidthMul）。</summary>
        public float Width { get => _width; set => _width = Mathf.Max(0.01f, value); }
        public BeamFxData Data => BeamFxDatabase.Get(_fxId);

        // ── 繪製物件 ──
        MeshRenderer _bodyMr, _arcFrontMr, _arcBackMr;
        Mesh _bodyMesh, _arcFrontMesh, _arcBackMesh;
        SpriteRenderer _muzzle, _impact;
        readonly List<SpriteRenderer> _joints = new List<SpriteRenderer>();
        MaterialPropertyBlock _mpb;

        // 網格暫存
        readonly List<Vector3> _v = new List<Vector3>(256);
        readonly List<Vector2> _uv = new List<Vector2>(256);
        readonly List<Vector2> _uv1 = new List<Vector2>(256);
        readonly List<int> _tri = new List<int>(512);
        readonly List<Vector3> _av = new List<Vector3>(512), _bv = new List<Vector3>(512);
        readonly List<Vector2> _auv = new List<Vector2>(512), _buv = new List<Vector2>(512);
        readonly List<Color> _ac = new List<Color>(512), _bc = new List<Color>(512);
        readonly List<int> _atri = new List<int>(1024), _btri = new List<int>(1024);
        readonly List<Vector2> _jointPos = new List<Vector2>(8);
        readonly List<Vector2> _strip = new List<Vector2>(64);
        readonly List<float> _stripA = new List<float>(64);

        // 電弧
        struct Arc { public float S0, Age, Life, Phase, Amp; public int Seed; }
        Arc[] _arcs = new Arc[0];
        float _sparkAcc;

        // ───────────────────────── 建立 ─────────────────────────

        /// <summary>建一條光束外觀（還沒有路徑，要接著 <see cref="Track"/> 或 <see cref="SetPath"/>）。</summary>
        /// <param name="fxId">BeamFxTable 的 ID（≤0＝預設光束）。</param>
        /// <param name="width">命中寬度（＝武器 BeamWidth × 角色縮放）。</param>
        public static BeamVisual Create(int fxId, float width, string sortingLayer = "Default",
                                        int sortingOrder = DefaultSortingOrder, Transform parent = null)
        {
            var go = new GameObject("BeamVisual");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;   // 網格頂點用世界座標
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var bv = go.AddComponent<BeamVisual>();
            bv._fxId = fxId;
            bv.Width = width;
            bv._layer = string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer;
            bv._order = sortingOrder;
            bv.Build();
            return bv;
        }

        /// <summary>一閃即逝的光束（連鎖閃電）：畫 <paramref name="life"/> 秒、邊閃邊淡出後自毀。</summary>
        public static BeamVisual Flash(int fxId, IReadOnlyList<Vector2> pts, float width, float life,
                                       string sortingLayer = "Default", int sortingOrder = DefaultSortingOrder,
                                       Transform parent = null)
        {
            var bv = Create(fxId, width, sortingLayer, sortingOrder, parent);
            bv.SetPath(pts);
            bv._life = Life.Flash;
            bv._lifeT = 0f;
            bv._lifeLen = Mathf.Max(0.02f, life);
            bv._fadeIn = 1f;      // 閃電一出現就是全亮
            bv._age = 1000f;      // 不做伸長
            return bv;
        }

        /// <summary>
        /// 跟著一份會自己更新的路徑清單（例如 <c>LaserBeam.Points</c>）；每個 LateUpdate 讀一次。
        /// </summary>
        public void Track(IReadOnlyList<Vector2> points)
        {
            _track = points;
        }

        /// <summary>
        /// 給一份路徑（複製一份）。<paramref name="fadeLife"/> &gt; 0：之後如果沒再呼叫，就在這麼多秒內自己淡出、自毀
        /// （掃射每幀重餵＝持續亮著；停餵＝自然淡掉）。
        /// </summary>
        public void SetPath(IReadOnlyList<Vector2> points, float fadeLife = 0f)
        {
            _track = null;
            _src.Clear();
            if (points != null) for (int i = 0; i < points.Count; i++) _src.Add(points[i]);
            if (fadeLife > 0f && _life != Life.Stopping && _life != Life.Flash)
            {
                _life = Life.FeedFade;
                _lifeLen = fadeLife;
                _sinceFeed = 0f;
            }
        }

        /// <summary>放開：照表格 FadeTime 淡出後自毀（重複呼叫無妨）。</summary>
        public void Stop()
        {
            if (_life == Life.Stopping) return;
            if (_life == Life.Flash) return;
            _life = Life.Stopping;
            _lifeT = 0f;
            _lifeLen = Data.FadeTime;
        }

        /// <summary>立刻消失（換場景、預覽器重設用）。</summary>
        public void Kill()
        {
            if (this != null) Destroy(gameObject);
        }

        void Build()
        {
            _seed = Random.value * 50f + 1f;
            _mpb = new MaterialPropertyBlock();
            _bodyMesh = NewMesh("Body"); _bodyMr = NewMeshChild("Body", _bodyMesh, BeamShader, _order);
            _arcBackMesh = NewMesh("ArcBack"); _arcBackMr = NewMeshChild("ArcBack", _arcBackMesh, ArcShader, _order - 1);
            _arcFrontMesh = NewMesh("ArcFront"); _arcFrontMr = NewMeshChild("ArcFront", _arcFrontMesh, ArcShader, _order + 1);
            _muzzle = NewOrb("Muzzle");
            _impact = NewOrb("Impact");
        }

        static Mesh NewMesh(string name)
        {
            var m = new Mesh { name = "BeamVisual " + name, hideFlags = HideFlags.DontSave };
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

        SpriteRenderer NewOrb(string name)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = VfxAssets.SquareSprite;
            var mat = VfxAssets.SharedMaterial(OrbShader);
            if (mat != null) sr.sharedMaterial = mat;
            sr.sortingLayerName = _layer;
            sr.sortingOrder = _order + 2;
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

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            var d = Data;
            _age += dt;
            _fadeIn = Mathf.Min(1f, _fadeIn + dt / FadeInTime);

            float fade = _fadeIn;
            switch (_life)
            {
                case Life.Stopping:
                    _lifeT += dt;
                    fade *= 1f - Mathf.Clamp01(_lifeT / _lifeLen);
                    if (_lifeT >= _lifeLen) { Destroy(gameObject); return; }
                    break;
                case Life.Flash:
                {
                    _lifeT += dt;
                    float k = Mathf.Clamp01(_lifeT / _lifeLen);
                    fade *= 1f - k * k;
                    if (_lifeT >= _lifeLen) { Destroy(gameObject); return; }
                    break;
                }
                case Life.FeedFade:
                    _sinceFeed += dt;
                    fade *= 1f - Mathf.Clamp01((_sinceFeed - 0.05f) / _lifeLen);
                    if (_sinceFeed >= _lifeLen + 0.05f) { Destroy(gameObject); return; }
                    break;
            }

            // 1) 取路徑、去重、伸長截斷
            var src = _track ?? (IReadOnlyList<Vector2>)_src;
            PreparePath(src, d);
            if (_pts.Count < 2)
            {
                SetVisible(false);
                return;
            }

            float hv = _width * d.WidthMul * 0.5f;   // 可見半寬

            // 2) 光柱
            _jointPos.Clear();
            if (d.HasBody) BuildBody(d, hv); else _bodyMesh.Clear();
            CollectJoints();
            _bodyMr.enabled = d.HasBody;
            ApplyBodyProps(d, hv, fade);

            // 3) 電弧
            BuildArcs(d, hv, dt);
            ApplyArcProps(d, fade);

            // 4) 光球、反彈光點、火花
            UpdateOrbs(d, hv, fade, dt);
        }

        void SetVisible(bool on)
        {
            if (!on)
            {
                _bodyMr.enabled = false;
                _arcFrontMr.enabled = _arcBackMr.enabled = false;
                _muzzle.enabled = _impact.enabled = false;
                for (int i = 0; i < _joints.Count; i++) _joints[i].enabled = false;
            }
        }

        void PreparePath(IReadOnlyList<Vector2> src, BeamFxData d)
        {
            _pts.Clear();
            _cum.Clear();
            _length = 0f;
            if (src == null || src.Count == 0) return;
            _pts.Add(src[0]);
            _cum.Add(0f);
            for (int i = 1; i < src.Count; i++)
            {
                Vector2 p = src[i];
                float seg = (p - _pts[_pts.Count - 1]).magnitude;
                if (seg < 1e-4f) continue;
                _pts.Add(p);
                _length += seg;
                _cum.Add(_length);
            }

            // 剛發射時往前伸長（氣功砲「推出去」的感覺）；判定一開始就是全長，視覺晚個零點幾秒追上。
            if (d.GrowSpeed > 0f && _life != Life.Flash)
            {
                float maxLen = _age * d.GrowSpeed;
                if (maxLen < _length) TruncatePath(Mathf.Max(0.02f, maxLen));
            }
        }

        void TruncatePath(float len)
        {
            for (int i = 1; i < _pts.Count; i++)
            {
                if (_cum[i] < len) continue;
                float segLen = _cum[i] - _cum[i - 1];
                float k = segLen > 1e-5f ? (len - _cum[i - 1]) / segLen : 0f;
                Vector2 p = Vector2.Lerp(_pts[i - 1], _pts[i], k);
                int remove = _pts.Count - i;
                _pts.RemoveRange(i, remove);
                _cum.RemoveRange(i, remove);
                _pts.Add(p);
                _cum.Add(len);
                _length = len;
                return;
            }
        }

        // ───────────────────────── 光柱網格 ─────────────────────────

        bool IsBreak(int i)
        {
            Vector2 a = (_pts[i] - _pts[i - 1]).normalized;
            Vector2 b = (_pts[i + 1] - _pts[i]).normalized;
            return Vector2.Dot(a, b) < Mathf.Cos(BreakAngle * Mathf.Deg2Rad);
        }

        void CollectJoints()
        {
            float c = Mathf.Cos(JointAngle * Mathf.Deg2Rad);
            for (int i = 1; i < _pts.Count - 1; i++)
            {
                Vector2 a = (_pts[i] - _pts[i - 1]).normalized;
                Vector2 b = (_pts[i + 1] - _pts[i]).normalized;
                if (Vector2.Dot(a, b) < c) _jointPos.Add(_pts[i]);
            }
        }

        void BuildBody(BeamFxData d, float hv)
        {
            float hg = hv * d.GlowWidth * (1f + d.PulseAmt);   // 網格半寬（含外暈與脈衝鼓起）
            _v.Clear(); _uv.Clear(); _uv1.Clear(); _tri.Clear();

            int start = 0;
            for (int i = 1; i < _pts.Count; i++)
            {
                bool last = i == _pts.Count - 1;
                if (!last && !IsBreak(i)) continue;
                AddBodyStrip(start, i, hg);
                start = i;
            }

            _bodyMesh.Clear();
            _bodyMesh.SetVertices(_v);
            _bodyMesh.SetUVs(0, _uv);
            _bodyMesh.SetUVs(1, _uv1);
            _bodyMesh.SetTriangles(_tri, 0, false);
            _bodyMesh.RecalculateBounds();
        }

        /// <summary>_pts[a..b] 鋪一段連續帶子：頭尾各加一個圓頭（長 hg），中間的點用斜接。</summary>
        void AddBodyStrip(int a, int b, float hg)
        {
            Vector2 d0 = (_pts[a + 1] - _pts[a]).normalized;
            Vector2 d1 = (_pts[b] - _pts[b - 1]).normalized;
            int first = _v.Count;

            // 頭圓頭
            AddPair(_pts[a] - d0 * hg, Perp(d0) * hg, _cum[a] - hg, 1f);
            for (int i = a; i <= b; i++)
            {
                Vector2 n;
                if (i == a) n = Perp(d0);
                else if (i == b) n = Perp(d1);
                else
                {
                    Vector2 n0 = Perp((_pts[i] - _pts[i - 1]).normalized);
                    Vector2 n1 = Perp((_pts[i + 1] - _pts[i]).normalized);
                    Vector2 m = (n0 + n1);
                    float ml = m.magnitude;
                    if (ml < 1e-4f) n = n0;
                    else
                    {
                        m /= ml;
                        float c = Mathf.Max(0.5f, Vector2.Dot(m, n0));   // 斜接長度上限 2 倍（＝BreakAngle 120° 的位置）
                        n = m / c;
                    }
                }
                AddPair(_pts[i], n * hg, _cum[i], 0f);
            }
            // 尾圓頭
            AddPair(_pts[b] + d1 * hg, Perp(d1) * hg, _cum[b] + hg, 1f);

            int pairs = (_v.Count - first) / 2;
            for (int k = 1; k < pairs; k++)
            {
                int i0 = first + (k - 1) * 2;
                _tri.Add(i0); _tri.Add(i0 + 1); _tri.Add(i0 + 3);
                _tri.Add(i0); _tri.Add(i0 + 3); _tri.Add(i0 + 2);
            }
        }

        void AddPair(Vector2 c, Vector2 off, float s, float cap)
        {
            _v.Add(c - off); _uv.Add(new Vector2(s, -1f)); _uv1.Add(new Vector2(cap, 0f));
            _v.Add(c + off); _uv.Add(new Vector2(s, 1f)); _uv1.Add(new Vector2(cap, 0f));
        }

        static Vector2 Perp(Vector2 d) => new Vector2(-d.y, d.x);

        void ApplyBodyProps(BeamFxData d, float hv, float fade)
        {
            _bodyMr.GetPropertyBlock(_mpb);
            _mpb.Clear();
            _mpb.SetColor(IdColorCore, d.ColorCore);
            _mpb.SetColor(IdColorMid, d.ColorMid);
            _mpb.SetColor(IdColorEdge, d.ColorEdge);
            _mpb.SetFloat(IdIntensity, d.BodyIntensity);
            _mpb.SetFloat(IdCore, d.CoreWidth);
            _mpb.SetFloat(IdSolid, d.Solid);
            _mpb.SetFloat(IdPad, d.GlowWidth * (1f + d.PulseAmt));
            _mpb.SetFloat(IdFlow, d.FlowSpeed);
            _mpb.SetFloat(IdStreak, d.StreakAmt);
            _mpb.SetFloat(IdPulseSpacing, d.PulseSpacing);
            _mpb.SetFloat(IdPulseAmt, d.PulseAmt);
            _mpb.SetFloat(IdPulseSpeed, d.PulseSpeed);
            _mpb.SetFloat(IdEdgeNoise, d.EdgeNoise);
            _mpb.SetFloat(IdChroma, d.Chroma);
            _mpb.SetFloat(IdShimmer, d.Shimmer);
            _mpb.SetFloat(IdShimmerSpacing, d.ShimmerSpacing);
            _mpb.SetFloat(IdShimmerSpeed, d.ShimmerSpeed);
            _mpb.SetFloat(IdFlicker, d.Flicker);
            _mpb.SetFloat(IdFade, fade);
            _mpb.SetFloat(IdSeed, _seed);
            _mpb.SetFloat(IdDim, d.Dim);
            _bodyMr.SetPropertyBlock(_mpb);
        }

        // ───────────────────────── 路徑取樣 ─────────────────────────

        /// <summary>沿路徑長度 s 取位置與切線；<paramref name="seg"/> 回傳所在的段（0 起），<paramref name="local"/>＝段內距離。</summary>
        void Sample(float s, ref int hint, out Vector2 pos, out Vector2 tan, out int seg, out float local)
        {
            int n = _pts.Count;
            s = Mathf.Clamp(s, 0f, _length);
            if (hint < 0 || hint >= n - 1 || _cum[hint] > s) hint = 0;
            while (hint < n - 2 && _cum[hint + 1] < s) hint++;
            float segLen = _cum[hint + 1] - _cum[hint];
            local = s - _cum[hint];
            float k = segLen > 1e-5f ? local / segLen : 0f;
            Vector2 a = _pts[hint], b = _pts[hint + 1];
            pos = Vector2.Lerp(a, b, k);
            tan = segLen > 1e-5f ? (b - a) / segLen : Vector2.right;
            seg = hint;
        }

        static float Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x8da6b343u ^ (uint)b * 0xd8163841u ^ (uint)c * 0xcb1ab31fu;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        // ───────────────────────── 電弧 ─────────────────────────

        void BuildArcs(BeamFxData d, float hv, float dt)
        {
            _av.Clear(); _auv.Clear(); _ac.Clear(); _atri.Clear();
            _bv.Clear(); _buv.Clear(); _bc.Clear(); _btri.Clear();
            bool on = d.HasArcs;
            if (on)
            {
                int tick = Mathf.FloorToInt((Time.time + _seed) * Mathf.Max(0.01f, d.ArcRate));
                if (d.ArcMode == BeamArcMode.Helix) BuildHelixArcs(d, hv, dt, tick);
                else BuildPathArcs(d, hv, tick);
            }
            FlushArcMesh(_arcFrontMesh, _av, _auv, _ac, _atri);
            FlushArcMesh(_arcBackMesh, _bv, _buv, _bc, _btri);
            _arcFrontMr.enabled = on && _av.Count > 0;
            _arcBackMr.enabled = on && _bv.Count > 0;
        }

        static void FlushArcMesh(Mesh m, List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> tri)
        {
            m.Clear();
            if (v.Count == 0) return;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetColors(c);
            m.SetTriangles(tri, 0, false);
            m.RecalculateBounds();
        }

        void BuildHelixArcs(BeamFxData d, float hv, float dt, int tick)
        {
            if (_arcs.Length != d.ArcCount)
            {
                _arcs = new Arc[d.ArcCount];
                for (int i = 0; i < _arcs.Length; i++) RespawnArc(ref _arcs[i], d, i, true);
            }

            float amp = d.ArcAmp * hv;
            float halfW = d.ArcWidth * hv * 1.5f;
            float twist = d.ArcTwist * Mathf.PI * 2f;
            float spin = Time.time * 5f;
            for (int i = 0; i < _arcs.Length; i++)
            {
                ref Arc a = ref _arcs[i];
                a.Age += dt;
                a.S0 += d.ArcTravel * dt;
                if (a.Age >= a.Life || a.S0 > _length) RespawnArc(ref a, d, i, false);

                float life01 = Mathf.Clamp01(a.Age / a.Life);
                float lifeEnv = Mathf.Sin(life01 * Mathf.PI);
                lifeEnv = Mathf.Sqrt(Mathf.Max(0f, lifeEnv));

                _strip.Clear(); _stripA.Clear();
                var depth = s_depth; depth.Clear();
                int hint = 0;
                int steps = Mathf.Max(2, Mathf.CeilToInt(d.ArcLength / d.ArcSegment));
                for (int k = 0; k <= steps; k++)
                {
                    float u = (float)k / steps;
                    float s = a.S0 + u * d.ArcLength;
                    if (s < 0f || s > _length) continue;
                    Sample(s, ref hint, out var p, out var tan, out _, out _);
                    Vector2 nrm = Perp(tan);
                    float ph = s * twist + a.Phase - spin;
                    float jit = (Hash(a.Seed, tick, k) * 2f - 1f) * d.ArcJitter * amp;
                    float jt = (Hash(a.Seed + 7, tick, k) * 2f - 1f) * d.ArcSegment * 0.35f;
                    _strip.Add(p + nrm * (Mathf.Sin(ph) * amp * a.Amp + jit) + tan * jt);
                    float env = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)));
                    _stripA.Add(env * lifeEnv);
                    depth.Add(Mathf.Cos(ph));
                }
                if (_strip.Count < 2) continue;

                // 前半（繞到光柱前面）畫在光柱上面、後半（繞到後面）畫在底下且變暗
                for (int k = 0; k < _stripA.Count; k++)
                {
                    float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.3f, 0.3f, depth[k]));
                    s_frontA.Add(_stripA[k] * front);
                    s_backA.Add(_stripA[k] * (1f - front) * 0.45f);
                }
                AddArcStrip(_strip, s_frontA, halfW, _av, _auv, _ac, _atri);
                AddArcStrip(_strip, s_backA, halfW, _bv, _buv, _bc, _btri);
                s_frontA.Clear(); s_backA.Clear();
            }
        }

        static readonly List<float> s_depth = new List<float>(64);
        static readonly List<float> s_frontA = new List<float>(64);
        static readonly List<float> s_backA = new List<float>(64);

        void RespawnArc(ref Arc a, BeamFxData d, int i, bool initial)
        {
            a.Age = initial ? Random.value * d.ArcLife : 0f;
            a.Life = d.ArcLife * Random.Range(0.7f, 1.3f);
            float span = Mathf.Max(0.01f, _length);
            a.S0 = Random.Range(-d.ArcLength * 0.4f, Mathf.Max(0f, span - d.ArcLength * 0.6f));
            a.Phase = Random.value * Mathf.PI * 2f;
            a.Amp = Random.Range(0.75f, 1.25f);   // 每段電弧繞的半徑不一樣 ⇒ 不會變成規律的彈簧
            a.Seed = Random.Range(1, 1 << 20) + i * 7919;
        }

        void BuildPathArcs(BeamFxData d, float hv, int tick)
        {
            float amp = d.ArcAmp * hv;
            float halfW = d.ArcWidth * hv * 1.5f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(_length / d.ArcSegment), 2, 400);
            int seedBase = Mathf.FloorToInt(_seed * 1000f);
            for (int strand = 0; strand < d.ArcCount; strand++)
            {
                int sd = seedBase + strand * 1013;
                float bright = strand == 0 ? 1f : 0.55f + 0.25f * Hash(sd, tick, 999);
                bright *= 0.75f + 0.25f * Hash(sd, tick, 998);
                float strandAmp = amp * (strand == 0 ? 0.8f : 1.15f);

                _strip.Clear(); _stripA.Clear();
                int hint = 0;
                float o = 0f;
                for (int k = 0; k <= steps; k++)
                {
                    float s = _length * k / steps;
                    Sample(s, ref hint, out var p, out var tan, out int seg, out float local);
                    float segLen = _cum[seg + 1] - _cum[seg];
                    // 每個節點（目標、反彈點）上釘死，中間亂竄
                    float toNode = Mathf.Min(local, segLen - local);
                    float pin = Mathf.Clamp01(toNode / Mathf.Max(0.05f, d.ArcSegment * 2.5f));
                    o = o * 0.45f + (Hash(sd, tick, k) * 2f - 1f) * strandAmp * (0.6f + d.ArcJitter);
                    _strip.Add(p + Perp(tan) * (o * pin));
                    _stripA.Add(bright);

                    // 分岔
                    if (d.ArcBranch > 0f && k > 0 && k < steps && Hash(sd + 31, tick, k) < d.ArcBranch)
                        AddBranch(p + Perp(tan) * (o * pin), tan, d, hv, sd, tick, k, bright, halfW);
                }
                AddArcStrip(_strip, _stripA, halfW * (strand == 0 ? 1f : 0.75f), _av, _auv, _ac, _atri);
            }
        }

        readonly List<Vector2> _branch = new List<Vector2>(8);
        readonly List<float> _branchA = new List<float>(8);

        void AddBranch(Vector2 from, Vector2 tan, BeamFxData d, float hv, int sd, int tick, int k, float bright, float halfW)
        {
            float side = Hash(sd + 53, tick, k) < 0.5f ? -1f : 1f;
            float ang = (25f + 35f * Hash(sd + 59, tick, k)) * side * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(tan.x * Mathf.Cos(ang) - tan.y * Mathf.Sin(ang), tan.x * Mathf.Sin(ang) + tan.y * Mathf.Cos(ang));
            int n = 2 + Mathf.FloorToInt(Hash(sd + 61, tick, k) * 3f);
            _branch.Clear(); _branchA.Clear();
            Vector2 p = from;
            _branch.Add(p); _branchA.Add(bright * 0.8f);
            for (int j = 1; j <= n; j++)
            {
                float jit = (Hash(sd + 67, tick, k * 16 + j) * 2f - 1f) * 0.6f;
                Vector2 dj = (dir + Perp(dir) * jit).normalized;
                p += dj * d.ArcSegment * 0.8f;
                _branch.Add(p);
                _branchA.Add(bright * 0.8f * (1f - (float)j / (n + 1)));
            }
            AddArcStrip(_branch, _branchA, halfW * 0.6f, _av, _auv, _ac, _atri);
        }

        void AddArcStrip(List<Vector2> pts, List<float> alpha, float halfW,
                         List<Vector3> v, List<Vector2> uv, List<Color> col, List<int> tri)
        {
            int n = pts.Count;
            if (n < 2 || v.Count + n * 2 > MaxArcVerts) return;
            bool any = false;
            for (int i = 0; i < n; i++) if (alpha[i] > 0.01f) { any = true; break; }
            if (!any) return;

            int baseIdx = v.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 dPrev = i > 0 ? (pts[i] - pts[i - 1]) : (pts[1] - pts[0]);
                Vector2 dNext = i < n - 1 ? (pts[i + 1] - pts[i]) : dPrev;
                Vector2 dir = (dPrev.normalized + dNext.normalized);
                if (dir.sqrMagnitude < 1e-6f) dir = dNext;
                Vector2 nrm = Perp(dir.normalized) * halfW;
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

        void ApplyArcProps(BeamFxData d, float fade)
        {
            ApplyArcPropsTo(_arcFrontMr, d, fade);
            ApplyArcPropsTo(_arcBackMr, d, fade);
        }

        void ApplyArcPropsTo(MeshRenderer mr, BeamFxData d, float fade)
        {
            mr.GetPropertyBlock(_mpb);
            _mpb.Clear();
            _mpb.SetColor(IdColor, d.ArcColor);
            _mpb.SetFloat(IdIntensity, d.ArcIntensity);
            _mpb.SetFloat(IdFade, fade);
            _mpb.SetFloat(IdDim, d.Dim);
            mr.SetPropertyBlock(_mpb);
        }

        // ───────────────────────── 光球與火花 ─────────────────────────

        void UpdateOrbs(BeamFxData d, float hv, float fade, float dt)
        {
            float vis = hv * 2f;
            float t = Time.time;
            bool grown = d.GrowSpeed <= 0f || _life == Life.Flash || _age * d.GrowSpeed >= _length;

            PlaceOrb(_muzzle, ShowMuzzle && d.MuzzleSize > 0f, _pts[0],
                     d.MuzzleSize * vis * (1f + d.OrbPulse * Mathf.Sin(t * 17f + _seed)), d, fade, 0f, 1f);
            PlaceOrb(_impact, ShowImpact && d.ImpactSize > 0f, _pts[_pts.Count - 1],
                     d.ImpactSize * vis * (1f + d.OrbPulse * Mathf.Sin(t * 13f + _seed * 2f)), d, fade, 0f, 3f);

            int j = 0;
            if (d.JointSize > 0f)
            {
                for (; j < _jointPos.Count; j++)
                {
                    if (j >= _joints.Count) _joints.Add(NewOrb("Joint"));
                    PlaceOrb(_joints[j], true, _jointPos[j],
                             d.JointSize * vis * (1f + d.OrbPulse * Mathf.Sin(t * 11f + j * 1.7f + _seed)), d, fade, d.OrbStar, 5f + j);
                }
            }
            for (; j < _joints.Count; j++) _joints[j].enabled = false;

            // 火花：命中端往回噴（打在東西上濺開）
            if (ShowImpact && d.SparkRate > 0f && grown && fade > 0.3f && _life != Life.Stopping)
            {
                _sparkAcc += d.SparkRate * dt;
                Vector2 tip = _pts[_pts.Count - 1];
                Vector2 back = (_pts[_pts.Count - 2] - tip).normalized;
                while (_sparkAcc >= 1f)
                {
                    _sparkAcc -= 1f;
                    float ang = Random.Range(-75f, 75f) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(back.x * Mathf.Cos(ang) - back.y * Mathf.Sin(ang), back.x * Mathf.Sin(ang) + back.y * Mathf.Cos(ang));
                    VfxEmbers.Emit(tip + Random.insideUnitCircle * hv * 0.5f,
                                   dir * d.SparkSpeed * Random.Range(0.5f, 1.2f),
                                   d.SparkColor, d.SparkSize * Random.Range(0.7f, 1.3f), Random.Range(0.2f, 0.4f),
                                   _layer, _order + 3);
                }
            }
        }

        void PlaceOrb(SpriteRenderer sr, bool on, Vector2 at, float size, BeamFxData d, float fade, float star, float seedOfs)
        {
            sr.enabled = on && size > 0.001f;
            if (!sr.enabled) return;
            sr.transform.position = new Vector3(at.x, at.y, 0f);
            sr.transform.localScale = new Vector3(size, size, 1f);
            sr.GetPropertyBlock(_mpb);
            _mpb.Clear();
            _mpb.SetColor(IdColorCore, d.ColorCore);
            _mpb.SetColor(IdColorMid, d.HasBody ? d.ColorMid : d.ArcColor);
            _mpb.SetFloat(IdIntensity, Mathf.Max(0.6f, d.HasBody ? d.BodyIntensity : d.ArcIntensity * 0.6f));
            _mpb.SetFloat(IdRays, d.OrbRays);
            _mpb.SetFloat(IdStar, star);
            _mpb.SetFloat(IdFade, fade);
            _mpb.SetFloat(IdSeed, _seed + seedOfs);
            _mpb.SetFloat(IdDim, d.Dim);
            sr.SetPropertyBlock(_mpb);
        }
    }
}
