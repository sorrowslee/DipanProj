using System.Collections.Generic;
using UnityEngine;

namespace Dipan.Vfx
{
    /// <summary>
    /// **一團會燒的火**（2026-11-18，DipanVfx；FlameTable 一列＝一種火，說明見 readme/DIPAN_VFX.md §3）。
    ///
    /// 兩種用法：
    /// <list type="bullet">
    /// <item><see cref="Attach"/>：跟著某個物件（子彈）跑。火**不跟著物件旋轉**（永遠往上燒），
    ///   改成依移動速度往反方向倒、拉長，再甩出小火與火星。物件消失（子彈被 Destroy／停用）時自己熄滅。
    ///   排序跟著那個物件的 SpriteRenderer（再往前 <see cref="FlameData.SortingOffset"/> 層），所以前後關係跟原本的子彈一樣。</item>
    /// <item><see cref="Spawn"/>：放在固定位置燒一段時間（地上燃燒的火舌、甩出來的小火）。</item>
    /// </list>
    /// 外型全在 shader <c>Custom/DipanFlame</c>；這裡只負責擺位、大小、傾斜、起燃／熄滅時間。
    /// 物件池：熄滅後 SetActive(false) 放回去重用。
    /// </summary>
    public sealed class FlameVisual : MonoBehaviour
    {
        const string ShaderName = "Custom/DipanFlame";

        static readonly Stack<FlameVisual> _pool = new Stack<FlameVisual>();
        static Transform _poolRoot;

        static readonly int IdColorCore = Shader.PropertyToID("_ColorCore");
        static readonly int IdColorMid = Shader.PropertyToID("_ColorMid");
        static readonly int IdColorEdge = Shader.PropertyToID("_ColorEdge");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdAdditive = Shader.PropertyToID("_Additive");
        static readonly int IdScroll = Shader.PropertyToID("_ScrollSpeed");
        static readonly int IdDistort = Shader.PropertyToID("_Distort");
        static readonly int IdErode = Shader.PropertyToID("_Erode");
        static readonly int IdNoiseScale = Shader.PropertyToID("_NoiseScale");
        static readonly int IdBaseY = Shader.PropertyToID("_BaseY");
        static readonly int IdLean = Shader.PropertyToID("_Lean");
        static readonly int IdIgnite = Shader.PropertyToID("_Ignite");
        static readonly int IdFade = Shader.PropertyToID("_Fade");
        static readonly int IdSeed = Shader.PropertyToID("_Seed");
        static readonly int IdDim = Shader.PropertyToID("_Dim");

        public static void ResetForPlayMode()
        {
            _pool.Clear();
            _poolRoot = null;
        }

        // ───────────────────────── 對外 ─────────────────────────

        /// <summary>
        /// 讓 <paramref name="host"/> 身上燒起 FlameTable 第 <paramref name="flameId"/> 列的火。
        /// <paramref name="sizeMul"/>＝再乘的大小倍率（例：玩家體型）。回傳 null＝表裡沒這列或沒有圖。
        /// </summary>
        public static FlameVisual Attach(int flameId, Transform host, float sizeMul = 1f)
        {
            var d = FlameDatabase.Get(flameId);
            if (d == null || !d.HasSprites || host == null) return null;
            var f = Take();
            var hostSr = host.GetComponent<SpriteRenderer>();
            string layer = hostSr != null ? hostSr.sortingLayerName : "Default";
            int order = hostSr != null ? hostSr.sortingOrder + d.SortingOffset : 0;
            f.Play(d, host.position, sizeMul, -1f, layer, order, allowTrail: true);
            f._host = host;
            f._hostSr = hostSr;
            f._attached = true;
            return f;
        }

        /// <summary>
        /// 在 <paramref name="pos"/> 燒一團火，<paramref name="life"/> 秒後自己熄滅（含熄滅時間；&lt;0＝直到 <see cref="Stop"/>）。
        /// <paramref name="pos"/> 是火的「掛點」（Anchor＝0 時就是根部，例如地上燃燒的火舌貼地）。
        /// </summary>
        public static FlameVisual Spawn(FlameData d, Vector3 pos, float sizeMul, float life,
                                        string sortingLayer, int sortingOrder, bool allowTrail = false)
        {
            if (d == null || !d.HasSprites) return null;
            var f = Take();
            f.Play(d, pos, sizeMul, life, sortingLayer, sortingOrder, allowTrail);
            return f;
        }

        public static FlameVisual Spawn(int flameId, Vector3 pos, float sizeMul, float life,
                                        string sortingLayer, int sortingOrder)
            => Spawn(FlameDatabase.Get(flameId), pos, sizeMul, life, sortingLayer, sortingOrder);

        /// <summary>開始熄滅（已在熄滅中就不動）。</summary>
        public void Stop()
        {
            if (_d == null || _fadeStart >= 0f) return;
            _fadeStart = _t;
        }

        public bool IsAlive => _d != null;
        public FlameData Data => _d;
        /// <summary>世代號：物件池重用時 +1。持有參照的一方比對它，免得對已經換成別團的火下指令。</summary>
        public int Generation => _gen;

        /// <summary>改排序（地上燃燒用：每團火依自己的 Y 排）。</summary>
        public void SetSorting(string layer, int order)
        {
            if (_sr == null) return;
            _sr.sortingLayerName = layer;
            _sr.sortingOrder = order;
        }

        // ───────────────────────── 內部 ─────────────────────────

        static FlameVisual Take()
        {
            while (_pool.Count > 0)
            {
                var f = _pool.Pop();
                if (f != null) { f.gameObject.SetActive(true); return f; }   // 換場景被銷毀的會是 null
            }
            if (_poolRoot == null) _poolRoot = new GameObject("DipanVfx FlamePool").transform;
            var go = new GameObject("Flame");
            go.transform.SetParent(_poolRoot, false);
            var fv = go.AddComponent<FlameVisual>();
            var child = new GameObject("Sprite");
            child.transform.SetParent(go.transform, false);
            fv._sr = child.AddComponent<SpriteRenderer>();
            return fv;
        }

        SpriteRenderer _sr;
        MaterialPropertyBlock _mpb;
        FlameData _d;
        Transform _host;
        SpriteRenderer _hostSr;
        float _t, _life, _fadeStart;
        float _sizeMul, _jitter, _seed, _lean, _stretch;
        float _trailAcc, _emberAcc;
        bool _allowTrail;
        // ⚠ 不能用「_host != null」判斷是不是跟著宿主：宿主被 Destroy 後 Unity 的 == null 會回 true，
        //   於是連「宿主沒了要熄滅」這個分支都進不去，火就永遠留在原地（2026-11-18 火焰環繞彈結束後一圈火不消失）。
        bool _attached;
        Vector3 _lastPos;
        bool _hasLast;
        int _gen;

        void Play(FlameData d, Vector3 pos, float sizeMul, float life, string layer, int order, bool allowTrail)
        {
            _d = d;
            _gen++;
            _attached = false;
            _host = null;
            _hostSr = null;
            _t = 0f;
            _life = life;
            _fadeStart = -1f;
            _sizeMul = sizeMul > 0f ? sizeMul : 1f;
            _jitter = 1f + Random.Range(-d.SizeJitter, d.SizeJitter);
            _seed = Random.value * 50f;
            _lean = 0f;
            _stretch = 1f;
            _trailAcc = _emberAcc = 0f;
            _allowTrail = allowTrail;
            _hasLast = false;

            transform.position = pos;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            _sr.sprite = d.Sprites[Random.Range(0, d.Sprites.Length)];
            _sr.flipX = Random.value < 0.5f;
            var mat = VfxAssets.SharedMaterial(ShaderName);
            if (mat != null) _sr.sharedMaterial = mat;
            _sr.sortingLayerName = string.IsNullOrEmpty(layer) ? "Default" : layer;
            _sr.sortingOrder = order;
            _sr.color = Color.white;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _sr.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdColorCore, d.ColorCore);
            _mpb.SetColor(IdColorMid, d.ColorMid);
            _mpb.SetColor(IdColorEdge, d.ColorEdge);
            _mpb.SetFloat(IdIntensity, d.Intensity);
            _mpb.SetFloat(IdAdditive, d.Additive);
            _mpb.SetFloat(IdScroll, d.ScrollSpeed);
            _mpb.SetFloat(IdDistort, d.Distort);
            _mpb.SetFloat(IdErode, d.Erode);
            _mpb.SetFloat(IdNoiseScale, d.NoiseScale);
            _mpb.SetFloat(IdBaseY, PivotV(_sr.sprite));
            _mpb.SetFloat(IdSeed, _seed);
            _mpb.SetFloat(IdDim, d.Dim);
            _mpb.SetTexture(VfxAssets.IdNoiseTex, VfxAssets.NoiseTexture);
            _sr.SetPropertyBlock(_mpb);

            UpdatePose(0f);
        }

        /// <summary>sprite 的 pivot 在貼圖 V 方向的位置（0＝底、1＝頂）＝火的根部。</summary>
        static float PivotV(Sprite sp)
        {
            if (sp == null || sp.rect.height <= 0f) return 0.1f;
            return Mathf.Clamp01(sp.pivot.y / sp.rect.height);
        }

        void LateUpdate()
        {
            if (_d == null) { Recycle(); return; }
            float dt = Time.deltaTime;
            _t += dt;

            // 跟著宿主；宿主沒了就熄滅（留在原地燒完最後一下）
            if (_attached)
            {
                if (_host == null || !_host.gameObject.activeInHierarchy)
                {
                    _attached = false;
                    _host = null;
                    _hostSr = null;
                    Stop();
                }
                else
                {
                    transform.position = _host.position;
                    if (_hostSr != null)
                    {
                        _sr.sortingLayerID = _hostSr.sortingLayerID;
                        _sr.sortingOrder = _hostSr.sortingOrder + _d.SortingOffset;
                    }
                }
            }
            if (_life >= 0f && _fadeStart < 0f && _t >= Mathf.Max(0f, _life - _d.FadeTime)) Stop();

            // 速度 → 傾斜、拉長（平滑，免得每幀抖）
            Vector3 p = transform.position;
            Vector2 vel = Vector2.zero;
            if (_hasLast && dt > 0.0001f) vel = (p - _lastPos) / dt;
            _lastPos = p;
            _hasLast = true;
            float k = 1f - Mathf.Exp(-10f * dt);
            float targetLean = Mathf.Clamp(-vel.x * _d.LeanPerSpeed, -_d.LeanMax, _d.LeanMax);
            float targetStretch = 1f + Mathf.Min(_d.StretchMax, vel.magnitude * _d.StretchPerSpeed);
            _lean = Mathf.Lerp(_lean, targetLean, k);
            _stretch = Mathf.Lerp(_stretch, targetStretch, k);

            float fade = _fadeStart >= 0f ? Mathf.Clamp01((_t - _fadeStart) / _d.FadeTime) : 0f;
            UpdatePose(fade);

            if (fade < 1f && _fadeStart < 0f) EmitExtras(dt, p, vel);
            if (fade >= 1f) Recycle();
        }

        void UpdatePose(float fade)
        {
            var sp = _sr.sprite;
            if (sp == null) return;
            float ignite = _d.IgniteTime > 0f ? Mathf.Clamp01(_t / _d.IgniteTime) : 1f;
            ignite = 1f - (1f - ignite) * (1f - ignite);   // easeOut：一下子竄起來

            float flick = 1f + _d.Flicker * (Mathf.PerlinNoise(_seed, _t * 6f) - 0.5f) * 2f;
            float h = sp.bounds.size.y;
            float scale = _d.Size * _sizeMul * _jitter / Mathf.Max(0.0001f, h);
            float sx = scale * flick, sy = scale * flick * _stretch;
            _sr.transform.localScale = new Vector3(sx, sy, 1f);
            // pivot＝根部；Anchor 把火往下移，讓掛點落在火身上（0＝根部、0.5＝中間）
            float visibleH = h * (1f - PivotV(sp)) * sy;
            _sr.transform.localPosition = new Vector3(0f, -_d.Anchor * visibleH, 0f);

            _sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdLean, _sr.flipX ? -_lean : _lean);   // 左右翻時 UV 也翻了
            _mpb.SetFloat(IdIgnite, ignite);
            _mpb.SetFloat(IdFade, fade);
            _sr.SetPropertyBlock(_mpb);
        }

        void EmitExtras(float dt, Vector3 p, Vector2 vel)
        {
            var d = _d;
            float speed = vel.magnitude;
            float size = d.Size * _sizeMul * _jitter;
            // 甩出的小火：只在移動時甩（越快甩越多），留在原地很快燒完 ⇒ 一條斷斷續續的火尾
            if (_allowTrail && d.TrailRate > 0f && speed > 0.5f)
            {
                _trailAcc += d.TrailRate * Mathf.Clamp01(speed / 4f) * dt;
                while (_trailAcc >= 1f)
                {
                    _trailAcc -= 1f;
                    Vector3 at = p + (Vector3)(Random.insideUnitCircle * size * 0.15f);
                    Spawn(d, at, _sizeMul * _jitter * d.TrailSize, d.TrailLife,
                          _sr.sortingLayerName, _sr.sortingOrder - 1, allowTrail: false);
                }
            }
            // 火星：從火身上冒、往上飄
            if (d.EmberRate > 0f)
            {
                _emberAcc += d.EmberRate * dt;
                while (_emberAcc >= 1f)
                {
                    _emberAcc -= 1f;
                    Vector2 at = (Vector2)p + new Vector2(Random.Range(-0.25f, 0.25f) * size,
                                                          (0.3f - d.Anchor) * size + Random.Range(0f, 0.4f) * size);
                    Vector2 v = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.5f, 1.1f)) - vel * 0.15f;
                    VfxEmbers.Emit(at, v, d.EmberColor, d.EmberSize * Random.Range(0.7f, 1.3f) * _sizeMul,
                                   Random.Range(0.5f, 1f), _sr.sortingLayerName, _sr.sortingOrder + 1);
                }
            }
        }

        void Recycle()
        {
            _d = null;
            _host = null;
            _hostSr = null;
            gameObject.SetActive(false);
            _pool.Push(this);
        }
    }
}
