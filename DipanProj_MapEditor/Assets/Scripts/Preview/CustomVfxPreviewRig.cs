using System.Collections.Generic;
using UnityEngine;
using Dipan.Vfx;

namespace DipanMapEditor.Preview
{
    /// <summary>
    /// 「自製特效」預覽舞台（2026-11-18，見 readme/DIPAN_VFX.md §6）：在遠離地圖的角落（<see cref="Origin"/>）
    /// 擺一塊有格線的地板、一支專用相機拍到 RenderTexture，面板把它畫出來。
    /// 特效本體一律用 DipanVfx 套件裡**跟遊戲同一份**的程式與 shader 生出來，所以這裡看到的＝遊戲裡的樣子
    /// （差別只在：遊戲的火舌會跟角色一起 Y 排序、這裡沒有角色）。
    ///
    /// 依選到的種類自動重播：地刺＝模擬一顆子彈往右飛、沿路刺出；火焰＝靜止／環繞／墜落三種；地上燃燒＝燒完停一下再燒；
    /// 光束＝持續掃動／反彈折線／連鎖閃電／重複發射四種；靈獸＝在鏡頭範圍內亂游／追著參考人形。
    /// 面板沒在畫（切到別的工具或素材庫分頁）時自己停下、清掉特效。
    /// </summary>
    public sealed class CustomVfxPreviewRig : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(5000f, 5000f, 0f);
        const string Layer = "Default";

        public enum Kind { None, Spike, Flame, GroundFire, Explosion, Teleport, Beam, Spirit }
        public enum FlameMotion { Still, Orbit, Fall }
        public enum BeamMotion { Sweep, Bounce, Chain, Refire }
        public enum SpiritMotion { Wander, Chase }

        static CustomVfxPreviewRig _inst;
        public static CustomVfxPreviewRig Instance
        {
            get
            {
                if (_inst != null) return _inst;
                var go = new GameObject("CustomVfxPreviewRig");
                _inst = go.AddComponent<CustomVfxPreviewRig>();
                return _inst;
            }
        }

        /// <summary>面板每次 OnGUI 畫舞台時更新；超過幾幀沒更新＝面板被收起來了 ⇒ 停播。</summary>
        public static int LastDrawFrame = -100;

        // ── 對外設定（面板改）──
        public Kind CurrentKind { get; private set; }
        public int CurrentId { get; private set; }
        public FlameMotion Motion = FlameMotion.Orbit;
        public float OrbitRadius = 1.5f;      // 環繞彈配方 9 的 OrbitalRadius
        public float OrbitSpeed = 4f;         // 切線速度（配方 Speed）
        public int OrbitCount = 3;
        public float FireRadius = 1.6f;       // GroundEffect 8 的 Radius
        public float FireDuration = 3f;       // GroundEffect 8 的 Duration
        public float BlastRadius = 5f;        // 火焰拋擲彈：AreaRadius 2.5 × BulletScale 2
        public float SpikeSpeed = 8f;         // 地裂刺配方 19
        public float SpikeStep = 1.33f;
        public BeamMotion BeamMode = BeamMotion.Sweep;
        public float BeamWidth = 0.5f;
        public SpiritMotion SpiritMode = SpiritMotion.Wander;
        public float SpiritSpeed = 4f;        // 配方 Speed（空＝4）
        public float SpiritScale = 1f;        // 武器 BulletScale        // 武器表 BeamWidth（命中寬度）：5＝1、29＝0.38、9＝0.25、66＝0.3、78＝0.25
        public float ViewSize = 3f;           // 相機 orthographicSize（遊戲一般房間約 5）
        public bool BrightFloor;
        public bool ShowFigure = true;

        Camera _cam;
        RenderTexture _rt;
        SpriteRenderer _floor, _figBody, _figHead;
        LineRenderer _ring;
        readonly List<Transform> _hosts = new List<Transform>();
        readonly List<FlameVisual> _flames = new List<FlameVisual>();
        readonly List<int> _flameGens = new List<int>();
        GroundFireBurst.Handle _fire;
        float _t, _nextCycle;
        bool _active;

        public RenderTexture Texture => _rt;

        void Awake()
        {
            var camGo = new GameObject("CustomVfxPreviewCam");
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = Origin + new Vector3(0f, 0.6f, -10f);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = ViewSize;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
            _cam.depth = -50;
            _cam.enabled = false;

            _floor = MakeSprite("Floor", FloorSprite(), -200);
            _floor.transform.position = Origin;
            _floor.transform.localScale = Vector3.one * 24f;   // 24×24 單位，格線每 1 單位

            _figBody = MakeSprite("FigureBody", VfxAssets.DotSprite, 0);
            _figHead = MakeSprite("FigureHead", VfxAssets.DotSprite, 0);
            var figColor = new Color(0.45f, 0.48f, 0.55f, 0.55f);
            _figBody.color = figColor; _figHead.color = figColor;
            // 參考人形：遊戲角色可見高度約 1.95（PreviewSpriteLoader 的 GameCharacterWorldHeight）
            _figBody.transform.localScale = new Vector3(0.75f, 1.45f, 1f);
            _figHead.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            var ringGo = new GameObject("DamageRing");
            ringGo.transform.SetParent(transform, false);
            _ring = ringGo.AddComponent<LineRenderer>();
            _ring.useWorldSpace = true;
            _ring.loop = true;
            _ring.widthMultiplier = 0.03f;
            _ring.positionCount = 64;
            _ring.material = new Material(Shader.Find("Sprites/Default"));
            _ring.startColor = _ring.endColor = new Color(1f, 1f, 1f, 0.35f);
            _ring.sortingOrder = 500;   // 傷害範圍圈：細線畫在最上面
            _ring.enabled = false;

            for (int i = 0; i < 8; i++)
            {
                var h = new GameObject("Host" + i).transform;
                h.SetParent(transform, false);
                h.gameObject.AddComponent<SpriteRenderer>().sortingOrder = 100;   // FlameVisual 會跟著宿主的排序
                h.gameObject.SetActive(false);
                _hosts.Add(h);
            }
        }

        SpriteRenderer MakeSprite(string name, Sprite sp, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingLayerName = Layer;
            sr.sortingOrder = order;
            return sr;
        }

        static Sprite _floorSprite;
        static Sprite FloorSprite()
        {
            if (_floorSprite != null) return _floorSprite;
            const int N = 240;   // 24 格 × 10px
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                bool line = x % 10 == 0 || y % 10 == 0;   // 每 1 世界單位一條格線（比地板亮一點）
                byte v = line ? (byte)255 : (byte)205;
                px[y * N + x] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            _floorSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _floorSprite;
        }

        /// <summary>選一個要預覽的東西（重新開始播）。</summary>
        public void Select(Kind kind, int id)
        {
            CurrentKind = kind;
            CurrentId = id;
            Restart();
        }

        /// <summary>參數改了（表格重讀過）：清掉現在的、立刻重播。</summary>
        public void Restart()
        {
            ClearEffects();
            _t = 0f;
            _nextCycle = 0f;
        }

        void ClearEffects()
        {
            for (int i = 0; i < _flames.Count; i++)
                if (_flames[i] != null && _flames[i].IsAlive && _flames[i].Generation == _flameGens[i]) _flames[i].Stop();
            _flames.Clear();
            _flameGens.Clear();
            foreach (var h in _hosts) h.gameObject.SetActive(false);   // 宿主停用 ⇒ 附著的火自己熄
            _fire.Stop();
            _fire = default;
            if (_tele != null) { Destroy(_tele.gameObject); _tele = null; }
            if (_beam != null) { _beam.Kill(); _beam = null; }
            if (_spirit != null) { _spirit.Kill(); _spirit = null; }
        }

        public void EnsureTexture(int w, int h)
        {
            w = Mathf.Clamp(w, 64, 2048); h = Mathf.Clamp(h, 64, 2048);
            if (_rt != null && _rt.width == w && _rt.height == h) return;
            if (_rt != null) { _cam.targetTexture = null; _rt.Release(); Destroy(_rt); }
            _rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "CustomVfxPreview" };
            _cam.targetTexture = _rt;
        }

        void Update()
        {
            bool shouldRun = Time.frameCount - LastDrawFrame <= 3 && CurrentKind != Kind.None;
            if (!shouldRun)
            {
                if (_active) { ClearEffects(); _cam.enabled = false; _active = false; }
                return;
            }
            if (!_active) { _active = true; _cam.enabled = true; Restart(); }

            _cam.orthographicSize = Mathf.Clamp(ViewSize, 1f, 10f);
            float g = BrightFloor ? 0.55f : 0.16f;
            _floor.color = BrightFloor ? new Color(0.62f, 0.56f, 0.46f) : new Color(0.16f, 0.15f, 0.15f);
            _cam.backgroundColor = new Color(g * 0.3f, g * 0.3f, g * 0.35f);

            // 參考人形（站在舞台左邊）
            _figBody.gameObject.SetActive(ShowFigure);
            _figHead.gameObject.SetActive(ShowFigure);
            Vector3 figFeet = Origin + new Vector3(-2.6f, -0.4f, 0f);
            _figBody.transform.position = figFeet + new Vector3(0f, 0.72f, 0f);
            _figHead.transform.position = figFeet + new Vector3(0f, 1.7f, 0f);

            _t += Time.deltaTime;
            switch (CurrentKind)
            {
                case Kind.Spike: TickSpike(); break;
                case Kind.Flame: TickFlame(); break;
                case Kind.GroundFire: TickGroundFire(); break;
                case Kind.Explosion: TickExplosion(); break;
                case Kind.Teleport: TickTeleport(); break;
                case Kind.Beam: TickBeam(); break;
                case Kind.Spirit: TickSpirit(); break;
            }
        }

        // ── 地刺：每 2.4 秒一顆看不見的子彈從左飛到右，每 TrailStep 刺一組 ──
        float _spikeX, _spikeNextX;
        void TickSpike()
        {
            _ring.enabled = false;
            if (_t >= _nextCycle)
            {
                _nextCycle = _t + 2.4f;
                _spikeX = -3.5f;
                _spikeNextX = _spikeX;
            }
            if (_spikeX <= 3.5f)
            {
                _spikeX += SpikeSpeed * Time.deltaTime;
                while (_spikeNextX <= _spikeX && _spikeNextX <= 3.5f)
                {
                    GroundSpikeBurst.Spawn(CurrentId, (Vector2)Origin + new Vector2(_spikeNextX, -0.2f), Vector2.right, 1f, Layer, 5);
                    _spikeNextX += Mathf.Max(0.1f, SpikeStep);
                }
            }
        }

        // ── 火焰：靜止（一團擺著）／環繞（像火焰環繞彈）／墜落（像焚天火雨，落地後停一下再來）──
        void TickFlame()
        {
            _ring.enabled = false;
            var center = Origin + new Vector3(0f, 0.6f, 0f);
            switch (Motion)
            {
                case FlameMotion.Still:
                    if (_flames.Count == 0) AttachFlames(1);
                    _hosts[0].position = center;
                    break;
                case FlameMotion.Orbit:
                    int n = Mathf.Clamp(OrbitCount, 1, _hosts.Count);
                    if (_flames.Count != n) { ClearEffects(); AttachFlames(n); }
                    float r = Mathf.Max(0.2f, OrbitRadius);
                    float ang = _t * OrbitSpeed / r;
                    for (int i = 0; i < n; i++)
                    {
                        float a = ang + i * Mathf.PI * 2f / n;
                        _hosts[i].position = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r;
                    }
                    break;
                case FlameMotion.Fall:
                    const float Flight = 1.2f, Gap = 0.6f;
                    float cyc = _t % (Flight + Gap);
                    if (cyc < Flight)
                    {
                        if (_flames.Count == 0) AttachFlames(1);
                        float p = cyc / Flight;
                        Vector3 from = Origin + new Vector3(-4f, 5f, 0f), to = Origin + new Vector3(0.8f, -0.4f, 0f);
                        _hosts[0].position = Vector3.Lerp(from, to, p);
                    }
                    else if (_flames.Count > 0) ClearEffects();   // 落地：宿主停用 ⇒ 火熄滅
                    break;
            }
        }

        void AttachFlames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                var h = _hosts[i];
                h.gameObject.SetActive(true);
                var fv = FlameVisual.Attach(CurrentId, h, 1f);
                if (fv == null) continue;
                _flames.Add(fv);
                _flameGens.Add(fv.Generation);
            }
        }

        // ── 地上燃燒：燒完停 0.6 秒再燒；畫出傷害範圍圈 ──
        void TickGroundFire()
        {
            float r = Mathf.Max(0.1f, FireRadius);
            _ring.enabled = true;
            for (int i = 0; i < _ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / _ring.positionCount;
                _ring.SetPosition(i, Origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
            }
            if (_t >= _nextCycle)
            {
                var d = GroundFireDatabase.Get(CurrentId);
                float linger = d != null ? d.ScorchLinger : 0.8f;
                _nextCycle = _t + Mathf.Max(0.5f, FireDuration) + linger + 0.6f;
                _fire = GroundFireBurst.Spawn(CurrentId, Origin, r, Mathf.Max(0.5f, FireDuration), Layer, -120);
            }
        }

        // ── 傳送點：一直擺著；表格改了 Restart 會重建 ──
        TeleportMarkerFx _tele;
        void TickTeleport()
        {
            _ring.enabled = false;
            if (_tele == null)
            {
                var d = TeleportFxDatabase.Get(CurrentId);
                if (d == null) return;
                _tele = TeleportMarkerFx.Create(CurrentId, transform, (Vector2)Origin + new Vector2(0f, -0.3f), Layer);
            }
        }

        // ── 爆炸：每 2 秒炸一次；白圈＝傷害範圍 ──
        void TickExplosion()
        {
            float r = Mathf.Max(0.1f, BlastRadius);
            _ring.enabled = true;
            for (int i = 0; i < _ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / _ring.positionCount;
                _ring.SetPosition(i, Origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
            }
            if (_t >= _nextCycle)
            {
                _nextCycle = _t + 2f;
                ExplosionBurst.Spawn(CurrentId, Origin, r, Layer, 100, -120);
            }
        }

        // ── 光束：持續掃動（雷射按住掃）／反彈折線（看接頭與反彈光點）／連鎖閃電（每 0.5 秒閃一次）／重複發射（看伸長與放開淡出）──
        BeamVisual _beam;
        readonly List<Vector2> _beamPts = new List<Vector2>(8);
        void TickBeam()
        {
            _ring.enabled = false;
            Vector2 o = Origin;
            Vector2 hand = o + new Vector2(-2.3f, 0.6f);   // 參考人形的手
            float w = Mathf.Max(0.02f, BeamWidth);
            _beamPts.Clear();
            switch (BeamMode)
            {
                case BeamMotion.Sweep:
                {
                    float ang = Mathf.Sin(_t * 0.9f) * 28f * Mathf.Deg2Rad;
                    _beamPts.Add(hand);
                    _beamPts.Add(hand + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 6.5f);
                    if (_beam == null) { _beam = BeamVisual.Create(CurrentId, w, Layer, BeamVisual.DefaultSortingOrder, transform); }
                    _beam.Width = w;
                    _beam.SetPath(_beamPts);
                    break;
                }
                case BeamMotion.Bounce:
                    _beamPts.Add(hand);
                    _beamPts.Add(o + new Vector2(-0.6f, 2.3f));
                    _beamPts.Add(o + new Vector2(1.2f, -1.2f));
                    _beamPts.Add(o + new Vector2(3.0f, 1.9f));
                    _beamPts.Add(o + new Vector2(4.2f, 0.2f));
                    if (_beam == null) { _beam = BeamVisual.Create(CurrentId, w, Layer, BeamVisual.DefaultSortingOrder, transform); }
                    _beam.Width = w;
                    _beam.SetPath(_beamPts);
                    break;
                case BeamMotion.Chain:
                    if (_t >= _nextCycle)
                    {
                        _nextCycle = _t + 0.5f;
                        _beamPts.Add(hand);
                        _beamPts.Add(o + new Vector2(-0.4f, 1.2f + Random.Range(-0.3f, 0.3f)));
                        _beamPts.Add(o + new Vector2(1.3f, -0.6f + Random.Range(-0.3f, 0.3f)));
                        _beamPts.Add(o + new Vector2(3.2f, 0.9f + Random.Range(-0.3f, 0.3f)));
                        BeamVisual.Flash(CurrentId, _beamPts, w, 0.16f, Layer, BeamVisual.DefaultSortingOrder, transform);   // 同 PlayerController.ChainFlashDuration
                    }
                    break;
                case BeamMotion.Refire:
                    if (_t >= _nextCycle)
                    {
                        _nextCycle = _t + 2.2f;
                        _beam = BeamVisual.Create(CurrentId, w, Layer, BeamVisual.DefaultSortingOrder, transform);
                    }
                    if (_beam != null)
                    {
                        _beamPts.Add(hand);
                        _beamPts.Add(hand + new Vector2(6.5f, 0f));
                        _beam.SetPath(_beamPts);
                        if (_t > _nextCycle - 0.8f) { _beam.Stop(); _beam = null; }   // 放開：照 FadeTime 淡出
                    }
                    break;
            }
        }

        // ── 靈獸：在鏡頭範圍內一直游（亂游，或追著參考人形）；游速、大小可調；表格改了 Restart 重放 ──
        SpiritBeast _spirit;
        Rect StageRect()
        {
            float h = _cam.orthographicSize * 2f, w = h * _cam.aspect;
            Vector3 c = _cam.transform.position;
            return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
        }
        void TickSpirit()
        {
            _ring.enabled = false;
            if (_spirit == null)
            {
                if (SpiritFxDatabase.Get(CurrentId) == null) return;
                Vector2 start = (Vector2)Origin + new Vector2(-2.3f, 0.6f);   // 從參考人形身上放出去
                _spirit = SpiritBeast.Spawn(CurrentId, start, 0f, Mathf.Max(0.05f, SpiritScale), Mathf.Max(0.1f, SpiritSpeed),
                                            1e6f, 120f, Layer, transform);
                if (_spirit == null) return;
                _spirit.Bounds = StageRect;
            }
            _spirit.Motion.Speed = Mathf.Max(0.1f, SpiritSpeed);
            Vector2 fig = _figBody.transform.position;
            _spirit.Target = SpiritMode == SpiritMotion.Chase ? (System.Func<Vector2, Vector2?>)(_ => fig) : null;
        }

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
