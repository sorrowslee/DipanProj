using System.Collections.Generic;
using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 編輯器的「照明預覽」：把場景壓暗、再讓場上的燈把周圍照回來，讓擺燈時看得到接近遊戲的實際效果。
    ///
    /// 做法是一張蓋滿相機視野的四邊形（`Custom/EditorLightPreview`，相乘混合），每幀重算大小與光源陣列，
    /// 所以**改半徑／亮度／光色／搖晃、拖曳光源，畫面立刻跟著變**。
    ///
    /// 為什麼不是相機後處理：編輯器的參考線（光圈、選取框、格線）畫在 OnPostRender，
    /// 那是在相機算繪之後、後處理之前——用後處理會把參考線一起壓暗。改成四邊形參與正常算繪，
    /// OnPostRender 的線就會蓋在它上面維持清楚。
    ///
    /// ⚠ 與遊戲的差異（面板上也有寫）：
    ///   ① 這裡只模擬「壓暗＋照亮＋光色」，遊戲的氛圍 2 幽暗/3 噩夢還會另外去飽和、加冷色調。
    ///   ② 遊戲同框上限 12 盞（AtmosphereController.MaxLights），這裡最多畫 32 盞；
    ///      畫面內超過遊戲上限時 <see cref="OnScreenCount"/> 會超標，面板據此出示警告。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LightPreview : MonoBehaviour
    {
        /// <summary>與 EditorLightPreview.shader 的 MAX_LIGHTS 一致（改一邊要改另一邊）。</summary>
        public const int MaxPreviewLights = 32;

        /// <summary>遊戲端的同框上限（AtmosphereController.MaxLights）。只用來出示警告，不影響預覽。</summary>
        public const int GameMaxLights = 12;

        /// <summary>開關（由 EditorUI 的「照明預覽」鈕切換，PlayerPrefs 記住）。</summary>
        public bool Enabled { get; private set; }

        /// <summary>預覽用環境亮度 0~100（對應主專案 MapsTable.csv 的 EnvBright 欄）。100＝不壓暗。</summary>
        public int EnvBright { get; private set; } = 45;

        /// <summary>這一幀「光圈有碰到畫面」的盞數。超過 <see cref="GameMaxLights"/> 時代表遊戲裡會有燈被丟掉。</summary>
        public int OnScreenCount { get; private set; }

        /// <summary>這張地圖的總盞數（獨立光源＋地上物自帶的燈）。</summary>
        public int TotalCount { get; private set; }

        const string PrefEnabled = "MapEditor.LightPreview.Enabled";
        const string PrefEnvBright = "MapEditor.LightPreview.EnvBright";

        Camera _cam;
        GameObject _quad;
        MeshRenderer _mr;
        Material _mat;

        readonly Vector4[] _lightPos = new Vector4[MaxPreviewLights];
        readonly Vector4[] _lightTint = new Vector4[MaxPreviewLights];
        readonly List<Entry> _entries = new List<Entry>(MaxPreviewLights);

        // 每盞燈的搖晃相位種子。以「光源物件」為 key，這樣拖曳/改參數時相位不會被重設（不然會一直抖）。
        readonly Dictionary<object, float> _seeds = new Dictionary<object, float>();

        struct Entry
        {
            public Vector2 pos;
            public float radius, intensity, softness;
            public Color color;
        }

        void Awake()
        {
            _cam = GetComponent<Camera>();
            Enabled = PlayerPrefs.GetInt(PrefEnabled, 0) != 0;
            EnvBright = Mathf.Clamp(PlayerPrefs.GetInt(PrefEnvBright, 45), 0, 100);
        }

        public void SetEnabled(bool on)
        {
            Enabled = on;
            PlayerPrefs.SetInt(PrefEnabled, on ? 1 : 0);
            if (_quad != null) _quad.SetActive(on);
        }

        public void Toggle() => SetEnabled(!Enabled);

        public void SetEnvBright(int v)
        {
            EnvBright = Mathf.Clamp(v, 0, 100);
            PlayerPrefs.SetInt(PrefEnvBright, EnvBright);
        }

        bool EnsureQuad()
        {
            if (_quad != null) return true;

            // 先走 Resources.Load（放 Resources 底下才保證會被打包進獨立版；同主專案 AtmosphereController 的做法），
            // 找不到再退回 Shader.Find（在 Unity 編輯器裡跑一定找得到）。
            var sh = Resources.Load<Shader>("Shaders/EditorLightPreview");
            if (sh == null) sh = Shader.Find("Custom/EditorLightPreview");
            if (sh == null)
            {
                Debug.LogWarning("[LightPreview] 找不到 Custom/EditorLightPreview（應在 Assets/Resources/Shaders/），照明預覽停用。");
                enabled = false;
                return false;
            }

            _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };

            _quad = new GameObject("LightPreviewQuad");
            _quad.transform.SetParent(transform, false);   // 掛相機底下，跟著平移
            var mf = _quad.AddComponent<MeshFilter>();
            mf.mesh = BuildUnitQuad();
            _mr = _quad.AddComponent<MeshRenderer>();
            _mr.sharedMaterial = _mat;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            // 疊在所有地圖 sprite 之上。⚠ 本專案 sortingOrder 實質是 16-bit（見 readme/SCENE_EFFECT.md），
            // 大基底會繞回負值被背景蓋住，所以用接近 16-bit 上限的值；
            // 32767 留給「顯示底部ui」參考層（那層是對位用的，不該被壓暗）。
            _mr.sortingOrder = short.MaxValue - 1;   // 32766
            _quad.SetActive(Enabled);
            return true;
        }

        static Mesh BuildUnitQuad()
        {
            var m = new Mesh { name = "LightPreviewQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f,  0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        void LateUpdate()
        {
            if (!Enabled)
            {
                if (_quad != null && _quad.activeSelf) _quad.SetActive(false);
                return;
            }
            if (!EnsureQuad()) return;
            if (!_quad.activeSelf) _quad.SetActive(true);

            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null || !_cam.orthographic) return;

            // 四邊形貼齊當前視野（多 4% 邊界，縮放/平移時不會露出未覆蓋的邊）
            float h = _cam.orthographicSize * 2f * 1.04f;
            float w = h * _cam.aspect;
            _quad.transform.localPosition = new Vector3(0f, 0f, 1f);   // 相機前方一點點（正交，z 只要在近裁面之後）
            _quad.transform.localRotation = Quaternion.identity;
            _quad.transform.localScale = new Vector3(w, h, 1f);

            int n = CollectLights();
            _mat.SetFloat("_EnvDark", 1f - Mathf.Clamp01(EnvBright / 100f));
            _mat.SetFloat("_LightCount", n);
            _mat.SetVectorArray("_LightPos", _lightPos);
            _mat.SetVectorArray("_LightTint", _lightTint);
        }

        /// <summary>把地圖上兩種光源都收進 shader 陣列，回傳盞數；順便算出「畫面內盞數」給面板出示警告。</summary>
        int CollectLights()
        {
            _entries.Clear();
            TotalCount = 0;

            var map = MapSession.Instance?.Map;
            if (map != null)
            {
                // ① 獨立光源
                if (map.lights != null)
                {
                    foreach (var l in map.lights)
                    {
                        if (l == null || l.radius <= 0f) continue;
                        TotalCount++;
                        Add(l, new Vector2(l.x, l.y), l.radius, l.intensity, l.softness, l.color, l.flicker, l.flickerSpeed);
                    }
                }
                // ② 地上物自帶的燈
                var game = map.GameLayer;
                if (game != null && game.objects != null)
                {
                    foreach (var o in game.objects)
                    {
                        if (o == null || o.lightRadius <= 0f) continue;
                        TotalCount++;
                        Add(o, new Vector2(o.x, o.y), o.lightRadius, o.lightIntensity, o.lightSoftness,
                            o.lightColor, o.lightFlicker, o.lightFlickerSpeed);
                    }
                }
            }

            // 畫面內盞數（光圈與視野矩形有交集就算）——超過遊戲上限時面板會警告
            OnScreenCount = 0;
            if (_cam != null && _cam.orthographic)
            {
                float halfH = _cam.orthographicSize, halfW = halfH * _cam.aspect;
                Vector2 c = _cam.transform.position;
                foreach (var e in _entries)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(e.pos.x - c.x) - halfW);
                    float dy = Mathf.Max(0f, Mathf.Abs(e.pos.y - c.y) - halfH);
                    if (dx * dx + dy * dy <= e.radius * e.radius) OnScreenCount++;
                }
            }

            int n = Mathf.Min(_entries.Count, MaxPreviewLights);
            for (int i = 0; i < n; i++)
            {
                var e = _entries[i];
                float inner = e.radius * Mathf.Clamp01(e.softness > 0f ? e.softness : 0.46f);
                _lightPos[i] = new Vector4(e.pos.x, e.pos.y, e.radius, inner);
                _lightTint[i] = new Vector4(e.color.r, e.color.g, e.color.b, Mathf.Max(0f, e.intensity));
            }
            for (int i = n; i < MaxPreviewLights; i++) { _lightPos[i] = Vector4.zero; _lightTint[i] = Vector4.zero; }
            return n;
        }

        // 套上與遊戲端同一條油燈呼吸曲線，讓「搖晃強度/速度」在編輯器裡也看得出差別。
        void Add(object key, Vector2 pos, float radius, float intensity, float softness,
                 string hex, float flicker, float flickerSpeed)
        {
            float breathe = Breathe(key, flicker, flickerSpeed);
            _entries.Add(new Entry
            {
                pos = pos,
                radius = radius * breathe,
                intensity = (intensity > 0f ? intensity : 1f) * breathe,
                softness = softness,
                color = ParseHex(hex),
            });
        }

        float Breathe(object key, float flicker, float flickerSpeed)
        {
            if (flicker <= 0.001f) return 1f;
            if (!_seeds.TryGetValue(key, out float seed))
            {
                // 用登記順序當種子來源（不能用 Random，否則每次 Domain Reload 會跳），每盞相位不同即可
                seed = (_seeds.Count * 37.13f) % 1000f;
                _seeds[key] = seed;
            }
            float t = Time.time;
            float sp = Mathf.Max(0.01f, flickerSpeed);
            float slow = Mathf.PerlinNoise(seed, t * 0.55f * sp);
            float fast = Mathf.Sin(t * 3.2f * sp + seed) * 0.5f + 0.5f;
            float mix = Mathf.Clamp01(0.85f * slow + 0.15f * fast);
            return Mathf.Lerp(1f - 0.07f * flicker, 1f + 0.06f * flicker, mix);
        }

        static Color ParseHex(string hex)
        {
            var warm = new Color(1.00f, 0.78f, 0.52f, 1f);
            if (string.IsNullOrWhiteSpace(hex)) return warm;
            string s = hex.Trim().TrimStart('#');
            return ColorUtility.TryParseHtmlString("#" + s, out var c) ? c : warm;
        }
    }
}
