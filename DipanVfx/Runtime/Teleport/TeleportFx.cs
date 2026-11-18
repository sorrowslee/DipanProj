using System.Collections.Generic;
using UnityEngine;
using Dipan.Data;

namespace Dipan.Vfx
{
    /// <summary>
    /// TeleportFxTable 的一列：傳送點外型（2026-11-18，DipanVfx；說明見 readme/DIPAN_VFX.md §4c）。
    /// 主遊戲 MapLoader 的 <c>teleportFxStyleId</c> 指向這裡；全部是程序 shader，不用圖。留空＝預設。
    /// </summary>
    public sealed class TeleportFxData
    {
        public int ID;
        public string Name;

        public float Width = 1.75f;          // 光池（發光邊）寬度，世界單位
        public float Squash = 0.38f;         // 橢圓高／寬（俯視角的地面）
        public Color ColorCore = new Color(0.78f, 0.92f, 1f);
        public Color ColorRim = new Color(0.43f, 0.69f, 1f);
        public Color ColorDeep = new Color(0.055f, 0.1f, 0.19f);
        public float Intensity = 1.1f;
        public float Opacity = 0.6f;         // 池身不透明度（像一灘深水蓋住地板）
        public float SwirlSpeed = 0.22f;
        public float Twist = 7f;
        public float RippleFreq = 2f;
        public float RippleSpeed = 0.45f;
        public float Breathe = 0.08f;

        public float BeamHeight = 1.8f;      // 中央光柱高度（0＝不畫）
        public float BeamWidth = 0.45f;
        public float BeamIntensity = 0.8f;
        public float BeamSpeed = 0.6f;

        public float MoteRate = 5f;          // 每秒從池面升起幾顆光點（0＝不冒）
        public Color MoteColor = new Color(0.75f, 0.88f, 1f);
        public float MoteSize = 0.05f;

        public int SortingOrder = 5;         // 光池排序（地面；同舊傳送點 VfxTable 6）；光柱＋1、光點＋2
        public float FadeTime = 0.35f;       // 出現／被 togglePortal 打開時淡入的秒數
        public float Dim = 1f;
        public string Note = "";
    }

    /// <summary>TeleportFxTable 載入與查詢。主遊戲由 VfxManager 載入；地圖編輯器直接讀 DipanProj_Main 的 CSV。</summary>
    public static class TeleportFxDatabase
    {
        public const string TableName = "TeleportFxTable";
        static readonly Dictionary<int, TeleportFxData> _rows = new Dictionary<int, TeleportFxData>();
        static readonly HashSet<int> _warned = new HashSet<int>();

        public static bool Loaded { get; private set; }
        public static void ResetForPlayMode() { _rows.Clear(); _warned.Clear(); Loaded = false; }

        public static TeleportFxData Get(int id)
        {
            if (id <= 0) return null;
            if (_rows.TryGetValue(id, out var d)) return d;
            if (_warned.Add(id))
                Debug.LogWarning($"[TeleportFx] TeleportFxTable 沒有 ID {id}（或還沒載入）。主遊戲：VfxManager 的 Teleport Fx CSV 欄要拖 Assets/Data/TeleportFxTable.csv。");
            return null;
        }

        public static void LoadFromText(string text)
        {
            _rows.Clear();
            _warned.Clear();
            var table = CsvTable.Parse(text ?? "", TableName);
            table.Require("ID", "Name");
            foreach (var err in table.Errors) Debug.LogError(err);
            foreach (var row in table.Rows)
            {
                int id = row.GetInt("ID", 0);
                if (id <= 0) continue;
                var d = new TeleportFxData { ID = id, Name = row.Get("Name", $"#{id}") };
                d.Width = Mathf.Max(0.05f, row.GetFloat("Width", d.Width));
                d.Squash = Mathf.Clamp(row.GetFloat("Squash", d.Squash), 0.05f, 1f);
                d.ColorCore = VfxAssets.ParseColor(row.Get("ColorCore", ""), d.ColorCore);
                d.ColorRim = VfxAssets.ParseColor(row.Get("ColorRim", ""), d.ColorRim);
                d.ColorDeep = VfxAssets.ParseColor(row.Get("ColorDeep", ""), d.ColorDeep);
                d.Intensity = Mathf.Max(0f, row.GetFloat("Intensity", d.Intensity));
                d.Opacity = Mathf.Clamp01(row.GetFloat("Opacity", d.Opacity));
                d.SwirlSpeed = row.GetFloat("SwirlSpeed", d.SwirlSpeed);
                d.Twist = row.GetFloat("Twist", d.Twist);
                d.RippleFreq = Mathf.Max(0f, row.GetFloat("RippleFreq", d.RippleFreq));
                d.RippleSpeed = row.GetFloat("RippleSpeed", d.RippleSpeed);
                d.Breathe = Mathf.Clamp(row.GetFloat("Breathe", d.Breathe), 0f, 0.5f);
                d.BeamHeight = Mathf.Max(0f, row.GetFloat("BeamHeight", d.BeamHeight));
                d.BeamWidth = Mathf.Max(0.01f, row.GetFloat("BeamWidth", d.BeamWidth));
                d.BeamIntensity = Mathf.Max(0f, row.GetFloat("BeamIntensity", d.BeamIntensity));
                d.BeamSpeed = row.GetFloat("BeamSpeed", d.BeamSpeed);
                d.MoteRate = Mathf.Max(0f, row.GetFloat("MoteRate", d.MoteRate));
                d.MoteColor = VfxAssets.ParseColor(row.Get("MoteColor", ""), d.MoteColor);
                d.MoteSize = Mathf.Max(0.005f, row.GetFloat("MoteSize", d.MoteSize));
                d.SortingOrder = row.GetInt("SortingOrder", d.SortingOrder);
                d.FadeTime = Mathf.Max(0f, row.GetFloat("FadeTime", d.FadeTime));
                d.Dim = Mathf.Max(0f, row.GetFloat("Dim", d.Dim));
                d.Note = row.Get("Note", "");
                _rows[id] = d;
            }
            Loaded = true;
        }
    }

    /// <summary>
    /// **一個傳送點外型**（2026-11-18，DipanVfx）：光池（<c>Custom/DipanPortalPool</c>）＋中央光柱（<c>Custom/DipanLightBeam</c>）＋升起的光點。
    /// 掛在一個 GameObject 上就會一直播；**GameObject 停用就停、再啟用會淡入**（主遊戲 togglePortal 隱藏／恢復外型就是 SetActive）。
    /// 表格在播放中改了（預覽器）呼叫 <see cref="Play"/> 重套即可。
    /// </summary>
    public sealed class TeleportMarkerFx : MonoBehaviour
    {
        const string PoolShader = "Custom/DipanPortalPool";
        const string BeamShader = "Custom/DipanLightBeam";

        static readonly int IdColorCore = Shader.PropertyToID("_ColorCore");
        static readonly int IdColorRim = Shader.PropertyToID("_ColorRim");
        static readonly int IdColorDeep = Shader.PropertyToID("_ColorDeep");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdOpacity = Shader.PropertyToID("_Opacity");
        static readonly int IdSwirl = Shader.PropertyToID("_Swirl");
        static readonly int IdTwist = Shader.PropertyToID("_Twist");
        static readonly int IdRippleFreq = Shader.PropertyToID("_RippleFreq");
        static readonly int IdRippleSpeed = Shader.PropertyToID("_RippleSpeed");
        static readonly int IdBreathe = Shader.PropertyToID("_Breathe");
        static readonly int IdSpeed = Shader.PropertyToID("_Speed");
        static readonly int IdFade = Shader.PropertyToID("_Fade");
        static readonly int IdSeed = Shader.PropertyToID("_Seed");
        static readonly int IdDim = Shader.PropertyToID("_Dim");

        // 池緣在 shader 的 UV 半徑 0.92×0.82 處 ⇒ 方塊邊長＝光池寬 ÷ 這個
        const float RimUv = 0.92f * 0.82f;

        TeleportFxData _d;
        SpriteRenderer _pool, _beam;
        MaterialPropertyBlock _mpb;
        string _layer = "Default";
        int _order;
        float _fadeT, _moteAcc, _seed;

        /// <summary>整體透明度倍率（地圖編輯器把「不顯示外型」的傳送點畫成半透明）。</summary>
        public float Alpha = 1f;

        public TeleportFxData Data => _d;

        /// <summary>在 <paramref name="host"/> 上掛一個傳送點外型（TeleportFxTable 第 <paramref name="id"/> 列）；表裡沒有回 null。</summary>
        public static TeleportMarkerFx Create(int id, Transform parent, Vector2 pos, string sortingLayer = "Default")
        {
            var d = TeleportFxDatabase.Get(id);
            if (d == null) return null;
            var go = new GameObject("TeleportMarker");
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var fx = go.AddComponent<TeleportMarkerFx>();
            fx.Play(d, sortingLayer);
            return fx;
        }

        /// <summary>
        /// 套用一列設定（可重複呼叫）。<paramref name="sortingOrderOverride"/>：地圖編輯器的對位預覽要畫在地圖物件之上，
        /// 用它蓋掉表格的 SortingOrder；遊戲端不傳。
        /// </summary>
        public void Play(TeleportFxData d, string sortingLayer = "Default", int sortingOrderOverride = int.MinValue)
        {
            _d = d;
            _order = sortingOrderOverride != int.MinValue ? sortingOrderOverride : d.SortingOrder;
            _layer = string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer;
            if (_seed == 0f) _seed = Random.value * 30f + 1f;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_pool == null) _pool = NewChild("Pool");
            if (_beam == null) _beam = NewChild("Beam");

            float side = d.Width / RimUv;
            _pool.sprite = VfxAssets.SquareSprite;
            var pm = VfxAssets.SharedMaterial(PoolShader);
            if (pm != null) _pool.sharedMaterial = pm;
            _pool.sortingLayerName = _layer;
            _pool.sortingOrder = _order;
            _pool.transform.localPosition = Vector3.zero;
            _pool.transform.localScale = new Vector3(side, side * d.Squash, 1f);

            _beam.gameObject.SetActive(d.BeamHeight > 0f);
            _beam.sprite = VfxAssets.SquareSprite;
            var bm = VfxAssets.SharedMaterial(BeamShader);
            if (bm != null) _beam.sharedMaterial = bm;
            _beam.sortingLayerName = _layer;
            _beam.sortingOrder = _order + 1;
            _beam.transform.localPosition = new Vector3(0f, d.BeamHeight * 0.5f, 0f);   // 底邊在光池中心
            _beam.transform.localScale = new Vector3(d.BeamWidth, d.BeamHeight, 1f);

            _pool.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdColorCore, d.ColorCore);
            _mpb.SetColor(IdColorRim, d.ColorRim);
            _mpb.SetColor(IdColorDeep, d.ColorDeep);
            _mpb.SetFloat(IdIntensity, d.Intensity);
            _mpb.SetFloat(IdOpacity, d.Opacity);
            _mpb.SetFloat(IdSwirl, d.SwirlSpeed);
            _mpb.SetFloat(IdTwist, d.Twist);
            _mpb.SetFloat(IdRippleFreq, d.RippleFreq);
            _mpb.SetFloat(IdRippleSpeed, d.RippleSpeed);
            _mpb.SetFloat(IdBreathe, d.Breathe);
            _mpb.SetFloat(IdSeed, _seed);
            _mpb.SetFloat(IdDim, d.Dim);
            _mpb.SetTexture(VfxAssets.IdNoiseTex, VfxAssets.NoiseTexture);
            _pool.SetPropertyBlock(_mpb);

            _beam.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdColorCore, d.ColorCore);
            _mpb.SetColor(IdColorRim, d.ColorRim);
            _mpb.SetFloat(IdIntensity, d.BeamIntensity);
            _mpb.SetFloat(IdSpeed, d.BeamSpeed);
            _mpb.SetFloat(IdSeed, _seed);
            _mpb.SetFloat(IdDim, d.Dim);
            _mpb.SetTexture(VfxAssets.IdNoiseTex, VfxAssets.NoiseTexture);
            _beam.SetPropertyBlock(_mpb);

            ApplyFade();
        }

        SpriteRenderer NewChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            _fadeT = 0f;   // 每次出現（含 togglePortal 打開）都淡入
            _moteAcc = 0f;
        }

        void Update()
        {
            if (_d == null) return;
            float dt = Time.deltaTime;
            _fadeT += dt;
            ApplyFade();

            if (_d.MoteRate > 0f && Alpha > 0.5f)
            {
                _moteAcc += _d.MoteRate * dt;
                while (_moteAcc >= 1f)
                {
                    _moteAcc -= 1f;
                    float a = Random.value * Mathf.PI * 2f, u = Mathf.Sqrt(Random.value) * 0.9f;
                    Vector2 at = (Vector2)transform.position
                               + new Vector2(Mathf.Cos(a) * u * _d.Width * 0.5f, Mathf.Sin(a) * u * _d.Width * 0.5f * _d.Squash);
                    VfxEmbers.Emit(at, new Vector2(Random.Range(-0.05f, 0.05f), Random.Range(0.2f, 0.45f)),
                                   _d.MoteColor, _d.MoteSize * Random.Range(0.6f, 1.3f), Random.Range(1.4f, 2.6f),
                                   _layer, _order + 2);
                }
            }
        }

        void ApplyFade()
        {
            if (_pool == null || _mpb == null) return;
            float f = (_d.FadeTime > 0f ? Mathf.Clamp01(_fadeT / _d.FadeTime) : 1f) * Mathf.Clamp01(Alpha);
            _pool.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdFade, f);
            _pool.SetPropertyBlock(_mpb);
            if (_beam.gameObject.activeSelf)
            {
                _beam.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IdFade, f);
                _beam.SetPropertyBlock(_mpb);
            }
        }
    }
}
