using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **一組從地底刺出來的尖刺**（2026-11-17，取代舊的地刺序列圖；規格見 readme/GROUND_SPIKE.md）。
///
/// 子彈每飛 TrailStep 就在當下位置呼叫 <see cref="Spawn"/> 長一組：
///   ① 每根刺從地平線下沿著自己的軸往上推出（shader <c>Custom/GroundSpike</c> 把地平線以下切掉 ⇒ 尖端先冒出），
///      推到頂時縱向拉長一點再彈回（力道感）；一叢裡各根錯開一點點冒出。
///   ② 停留 → 收尾：縮回地底，或原地淡出（表格 RetractMode）。
///   ③ 腳下一張裂口、破土瞬間一團揚塵。
/// 傷害**不在這裡**：仍是那顆看不見的子彈打的，這裡純畫面。
///
/// 物件池：用完 SetActive(false) 放回池裡重用，不 Destroy（分裂／平行疊高時一秒可能長上百根）。
/// 池子的根物件跟著場景走；換場景後池裡的物件被銷毀，取用時會自動略過重建。
/// </summary>
public class GroundSpikeBurst : MonoBehaviour
{
    // ───────────────────────── 物件池 ─────────────────────────

    const string ShaderName = "Custom/GroundSpike";
    static readonly Stack<GroundSpikeBurst> _pool = new Stack<GroundSpikeBurst>();
    static Transform _poolRoot;
    static Material _spikeMat;
    static bool _shaderMissingWarned;

    static readonly int IdGroundY = Shader.PropertyToID("_GroundY");
    static readonly int IdFade = Shader.PropertyToID("_Fade");
    static readonly int IdDim = Shader.PropertyToID("_Dim");

    /// <summary>關掉 Domain Reload 後 static 不歸零；進 Play 時由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode()
    {
        _pool.Clear();
        _poolRoot = null;
        _spikeMat = null;
        _shaderMissingWarned = false;
    }

    /// <summary>
    /// 在 <paramref name="groundPos"/> 長一組 GroundSpikeTable 第 <paramref name="spikeId"/> 列的尖刺。
    /// <paramref name="travelDir"/>＝子彈前進方向（決定往哪邊傾斜、圖要不要左右翻）；
    /// <paramref name="extraScale"/>＝武器的 CastVisualScale；排序層與預設排序值由 VfxManager 傳入。
    /// </summary>
    public static void Spawn(int spikeId, Vector2 groundPos, Vector2 travelDir, float extraScale,
                             string sortingLayerName, int defaultSortingOrder)
    {
        var data = GroundSpikeDatabase.Get(spikeId);
        if (data == null) return;
        var burst = Take();
        burst.Play(data, groundPos, travelDir, extraScale > 0f ? extraScale : 1f,
                   sortingLayerName, data.HasSortingOrder ? data.SortingOrder : defaultSortingOrder);
    }

    static GroundSpikeBurst Take()
    {
        while (_pool.Count > 0)
        {
            var b = _pool.Pop();
            if (b != null) { b.gameObject.SetActive(true); return b; }   // 換過場景被銷毀的會是 null，略過
        }
        if (_poolRoot == null) _poolRoot = new GameObject("GroundSpikePool").transform;
        var go = new GameObject("GroundSpikeBurst");
        go.transform.SetParent(_poolRoot, false);
        return go.AddComponent<GroundSpikeBurst>();
    }

    static Material SpikeMaterial()
    {
        if (_spikeMat != null) return _spikeMat;
        var sh = Shader.Find(ShaderName);
        if (sh == null)
        {
            if (!_shaderMissingWarned)
            {
                _shaderMissingWarned = true;
                Debug.LogWarning($"[GroundSpike] 找不到 shader {ShaderName}（Assets/Resources/Shaders/GroundSpike.shader）。尖刺改用預設材質，地平線以下不會被切掉。");
            }
            return null;
        }
        _spikeMat = new Material(sh) { name = "GroundSpike (runtime)" };
        return _spikeMat;
    }

    // ───────────────────────── 單組演出 ─────────────────────────

    sealed class Spike
    {
        public Transform holder;      // 放在這根的地面點、帶傾斜角；子物件沿 holder 的 Y 軸推出／縮回
        public SpriteRenderer sr;
        public float delay;           // 錯開冒出的延遲
        public float scale;           // sprite 的等比縮放
        public bool active;
    }

    readonly List<Spike> _spikes = new List<Spike>();
    SpriteRenderer _crack, _dust;
    MaterialPropertyBlock _mpb;

    GroundSpikeData _d;
    float _t;            // 從 Play 起算的時間
    float _endTime;      // 這組全部演完的時間
    float _dustBaseScale, _crackBaseScale;
    int _activeCount;

    void Play(GroundSpikeData d, Vector2 groundPos, Vector2 travelDir, float extraScale, string layer, int order)
    {
        _d = d;
        _t = 0f;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        transform.position = new Vector3(groundPos.x, groundPos.y, 0f);

        Vector2 dir = travelDir.sqrMagnitude > 0.0001f ? travelDir.normalized : Vector2.right;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        // 往左飛就把圖左右翻（圖預設往右彎）；幾乎垂直飛時隨機，免得一整排都朝同一邊
        bool faceLeft = Mathf.Abs(dir.x) < 0.2f ? Random.value < 0.5f : dir.x < 0f;
        float sideFactor = Mathf.Clamp01(Mathf.Abs(dir.x) + 0.25f);   // 上下飛時傾斜變小，看起來才不會歪

        int count = Random.Range(d.CountMin, d.CountMax + 1);
        _activeCount = count;
        while (_spikes.Count < count) _spikes.Add(CreateSpike());

        var mat = SpikeMaterial();
        float unitScale = d.Height * extraScale / d.TallestSpriteHeight;
        float maxDelay = 0f;
        int lastSprite = -1;

        for (int i = 0; i < _spikes.Count; i++)
        {
            var s = _spikes[i];
            s.active = i < count;
            s.holder.gameObject.SetActive(s.active);
            if (!s.active) continue;

            // 挑圖：一叢裡盡量不跟上一根重複
            int pick = Random.Range(0, d.Spikes.Length);
            if (d.Spikes.Length > 1 && pick == lastSprite) pick = (pick + 1 + Random.Range(0, d.Spikes.Length - 1)) % d.Spikes.Length;
            lastSprite = pick;
            s.sr.sprite = d.Spikes[pick];
            s.sr.flipX = faceLeft;
            if (mat != null) s.sr.sharedMaterial = mat;
            s.sr.sortingLayerName = layer;

            // 位置：沿前進方向的垂直方向散開＋前後一點點
            Vector2 off = (i == 0) ? Vector2.zero
                : perp * Random.Range(-d.Spread, d.Spread) * extraScale + dir * Random.Range(-d.Spread, d.Spread) * 0.5f * extraScale;
            s.holder.position = new Vector3(groundPos.x + off.x, groundPos.y + off.y, 0f);

            // 傾斜：往前進方向倒（右飛＝順時針＝負角度）
            float lean = (d.LeanAngle * sideFactor + Random.Range(-d.LeanJitter, d.LeanJitter)) * (faceLeft ? 1f : -1f);
            s.holder.rotation = Quaternion.Euler(0f, 0f, lean);

            s.scale = unitScale * (1f + Random.Range(-d.HeightJitter, d.HeightJitter));
            s.delay = (i == 0) ? 0f : Random.Range(0f, d.StaggerTime);
            maxDelay = Mathf.Max(maxDelay, s.delay);

            // shader 參數：地平線＝這根的根部（世界 Y）
            s.sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdGroundY, s.holder.position.y);
            _mpb.SetFloat(IdFade, 1f);
            _mpb.SetFloat(IdDim, d.Dim);
            s.sr.SetPropertyBlock(_mpb);

            ApplySpikePose(s, 0f, 1f);   // 一開始整根埋在地下
        }

        // 一叢裡比較前面（Y 小、離鏡頭近）的畫在上面
        var order_ = new List<Spike>();
        for (int i = 0; i < count; i++) order_.Add(_spikes[i]);
        order_.Sort((a, b) => b.holder.position.y.CompareTo(a.holder.position.y));
        for (int i = 0; i < order_.Count; i++) order_[i].sr.sortingOrder = order + i;

        // 裂口：貼在地上、畫在刺後面
        if (d.Crack != null)
        {
            if (_crack == null) _crack = CreateFlat("Crack");
            _crack.gameObject.SetActive(true);
            _crack.sprite = d.Crack;
            _crack.sortingLayerName = layer;
            _crack.sortingOrder = order - 1;
            _crackBaseScale = d.CrackSize * extraScale / Mathf.Max(0.0001f, d.Crack.bounds.size.x);
            _crack.transform.position = new Vector3(groundPos.x, groundPos.y, 0f);
            _crack.transform.localScale = Vector3.one * _crackBaseScale;
            _crack.color = new Color(1f, 1f, 1f, 0f);
        }
        else if (_crack != null) _crack.gameObject.SetActive(false);

        // 揚塵：破土那一下噴出來、畫在刺前面
        if (d.Dust != null)
        {
            if (_dust == null) _dust = CreateFlat("Dust");
            _dust.gameObject.SetActive(true);
            _dust.sprite = d.Dust;
            _dust.sortingLayerName = layer;
            _dust.sortingOrder = order + count + 10;
            _dustBaseScale = d.DustSize * extraScale / Mathf.Max(0.0001f, d.Dust.bounds.size.x);
            _dust.transform.position = new Vector3(groundPos.x, groundPos.y + d.Height * extraScale * 0.15f, 0f);
            _dust.transform.localScale = Vector3.one * _dustBaseScale * 0.6f;
            _dust.color = new Color(1f, 1f, 1f, 0f);
        }
        else if (_dust != null) _dust.gameObject.SetActive(false);

        _endTime = Mathf.Max(maxDelay + d.RiseTime + d.HoldTime + d.RetractTime, d.DustTime) + 0.02f;
    }

    Spike CreateSpike()
    {
        var holder = new GameObject("Spike").transform;
        holder.SetParent(transform, false);
        var child = new GameObject("Sprite");
        child.transform.SetParent(holder, false);
        var sr = child.AddComponent<SpriteRenderer>();
        return new Spike { holder = holder, sr = sr };
    }

    SpriteRenderer CreateFlat(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.AddComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 擺一根刺：<paramref name="rise"/> 0＝整根在地平線下、1＝根部剛好在地平線上；
    /// <paramref name="stretch"/>＝縱向拉長倍率（刺出瞬間的力道）。沿 holder 自己的 Y 軸推，所以傾斜的刺也是順著刺身冒出來。
    /// </summary>
    static void ApplySpikePose(Spike s, float rise, float stretch)
    {
        var sp = s.sr.sprite;
        if (sp == null) return;
        float sx = s.scale, sy = s.scale * stretch;
        var b = sp.bounds;   // sprite 本地單位（含 pivot 偏移）
        float height = b.size.y * sy;
        // 根部對齊 holder 原點，再往下沉 (1-rise) 根高；多沉 3% 確保 rise=0 時連尖端都看不到
        float y = -b.min.y * sy - (1f - rise) * height * 1.03f;
        // 左右翻時 sprite 的水平中心會換邊；讓根部中心對齊 holder
        float cx = (b.min.x + b.max.x) * 0.5f * sx;
        float x = s.sr.flipX ? cx : -cx;
        s.sr.transform.localPosition = new Vector3(x, y, 0f);
        s.sr.transform.localScale = new Vector3(sx, sy, 1f);
    }

    void Update()
    {
        if (_d == null) { Recycle(); return; }
        _t += Time.deltaTime;
        var d = _d;
        const float SettleTime = 0.1f;   // 拉長後彈回原長的時間

        for (int i = 0; i < _spikes.Count; i++)
        {
            var s = _spikes[i];
            if (!s.active) continue;
            float t = _t - s.delay;
            float rise, stretch = 1f, fade = 1f;

            if (t <= 0f) { rise = 0f; }
            else if (t < d.RiseTime)
            {
                float p = t / d.RiseTime;
                rise = 1f - (1f - p) * (1f - p) * (1f - p);          // easeOutCubic：一下子竄出來
                stretch = 1f + d.Overshoot * p;                        // 越接近頂越拉長
            }
            else
            {
                float th = t - d.RiseTime;
                float settle = Mathf.Clamp01(th / SettleTime);
                stretch = 1f + d.Overshoot * (1f - settle);            // 彈回原長
                float tr = th - d.HoldTime;
                if (tr <= 0f) rise = 1f;
                else
                {
                    float q = Mathf.Clamp01(tr / d.RetractTime);
                    if (d.RetractIntoGround) { rise = 1f - q * q; fade = 1f; }   // easeIn：先慢後快縮回去
                    else { rise = 1f; fade = 1f - q; }
                }
            }

            ApplySpikePose(s, rise, stretch);
            if (!d.RetractIntoGround)
            {
                s.sr.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IdFade, fade);
                s.sr.SetPropertyBlock(_mpb);
            }
        }

        // 裂口：破土時淡入，收尾時跟著淡出
        if (_crack != null && _crack.gameObject.activeSelf)
        {
            float inA = Mathf.Clamp01(_t / Mathf.Max(0.01f, d.RiseTime));
            float outStart = _endTime - d.RetractTime;
            float outA = 1f - Mathf.Clamp01((_t - outStart) / Mathf.Max(0.01f, d.RetractTime));
            _crack.color = new Color(1f, 1f, 1f, Mathf.Min(inA, outA));
        }

        // 揚塵：破土那一刻冒出、一邊脹大一邊散掉
        if (_dust != null && _dust.gameObject.activeSelf)
        {
            float p = Mathf.Clamp01(_t / d.DustTime);
            _dust.transform.localScale = Vector3.one * _dustBaseScale * Mathf.Lerp(0.6f, 1.15f, 1f - (1f - p) * (1f - p));
            _dust.color = new Color(1f, 1f, 1f, p < 0.15f ? p / 0.15f * 0.9f : 0.9f * (1f - (p - 0.15f) / 0.85f));
        }

        if (_t >= _endTime) Recycle();
    }

    void Recycle()
    {
        _d = null;
        gameObject.SetActive(false);
        _pool.Push(this);
    }
}
