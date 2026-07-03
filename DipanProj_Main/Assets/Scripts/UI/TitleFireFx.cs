using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 標題畫面的「UI 端」火焰表演（全螢幕落火 ＋ 標題燃燒）。
    ///
    /// 為什麼不直接用 MapsTable 的 SceneEffect 火雨（<c>SceneEffectController</c>／<c>FireRain</c>）：
    /// 那套是**世界端 SpriteRenderer**、綁 <c>Camera.main</c> 與地圖邊界、且用 <c>Time.deltaTime</c>——
    /// 但標題面板會暫停遊戲（<c>timeScale=0</c>，deltaTime=0 → 火不動），且整片不透明 UI 蓋在世界相機上、世界端火球會被蓋掉。
    /// 所以這裡改做**畫在 Canvas 上、用 <c>unscaledDeltaTime</c>** 的 UI 版，暫停中照燒。視覺沿用火雨的程序生成佔位圖（<c>SceneEffectSprites</c>）。
    ///
    /// 兩部分：
    ///   ① 落火（embersRoot 全螢幕）：火星/火條從上方持續飄落、閃爍、微飄。
    ///   ② 標題燃燒（titleRect）：背後脈動火光 ＋ 沿標題下緣往上竄、放大淡出的火舌。
    /// 全程式建構、零 prefab，掛在 TitlePanel 底下即可。參數為上方常數，直接改。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleFireFx : MonoBehaviour
    {
        // ── 落火（全螢幕）──
        const float EmbersPerSecond = 7f;    // 每秒生幾顆落火（放緩、變少）
        const int   EmberMaxAlive   = 55;    // 同時上限（效能保護）
        const float EmberFallMin    = 70f;   // 落速下限（像素/秒）
        const float EmberFallMax    = 190f;  // 落速上限
        const float EmberSizeMin    = 8f;    // 火星大小下限（像素）
        const float EmberSizeMax    = 26f;   // 火星大小上限
        const float EmberDriftMax   = 38f;   // 水平飄移幅度（像素/秒）
        const float StreakChance    = 0.55f; // 有多少比例畫成帶尾火條（其餘為柔和火星）

        // ── 標題燃燒 ──
        const float FlamesPerSecond = 30f;   // 每秒生幾道火舌
        const int   FlameMaxAlive   = 80;    // 同時上限
        const float TitleGlowAlpha  = 0.40f; // 標題背後火光基礎亮度

        RectTransform _emberRoot;
        RectTransform _titleRect;
        Image _titleGlow;

        float _emberAccum, _flameAccum, _glowT;
        readonly List<GameObject> _embers = new List<GameObject>();
        readonly List<GameObject> _flames = new List<GameObject>();
        Sprite _emberStreak, _emberSoft, _flame;

        public void Init(RectTransform emberRoot, RectTransform titleRect, Image titleGlow)
        {
            _emberRoot = emberRoot;
            _titleRect = titleRect;
            _titleGlow = titleGlow;
            _emberStreak = SceneEffectSprites.Comet();   // 帶尾火球（複用火雨素材）
            _emberSoft   = SceneEffectSprites.Glow();     // 柔和光點
            _flame       = FlameSprite();                 // 火舌（水滴狀，本檔生成）
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;   // 暫停中仍動
            _embers.RemoveAll(o => o == null);
            _flames.RemoveAll(o => o == null);

            if (_emberRoot != null)
            {
                _emberAccum += dt * EmbersPerSecond;
                while (_emberAccum >= 1f) { _emberAccum -= 1f; if (_embers.Count < EmberMaxAlive) SpawnEmber(); }
            }

            if (_titleRect != null)
            {
                _flameAccum += dt * FlamesPerSecond;
                while (_flameAccum >= 1f) { _flameAccum -= 1f; if (_flames.Count < FlameMaxAlive) SpawnFlame(); }
            }

            if (_titleGlow != null)   // 標題火光脈動（柏林噪訊 + 微抖）
            {
                _glowT += dt;
                float f = TitleGlowAlpha
                          + 0.14f * (Mathf.PerlinNoise(_glowT * 3.5f, 0.3f) - 0.5f) * 2f
                          + 0.05f * Mathf.Sin(_glowT * 11f);
                var c = _titleGlow.color; c.a = Mathf.Clamp01(f); _titleGlow.color = c;
            }
        }

        void SpawnEmber()
        {
            float w = _emberRoot.rect.width, h = _emberRoot.rect.height;
            var go = new GameObject("Ember", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_emberRoot, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Random.Range(-w * 0.5f, w * 0.5f), h * 0.5f + Random.Range(0f, 90f));

            bool streak = Random.value < StreakChance;
            float size = Random.Range(EmberSizeMin, EmberSizeMax);
            rt.sizeDelta = streak ? new Vector2(size, size * Random.Range(1.8f, 2.8f)) : new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.sprite = streak ? _emberStreak : _emberSoft;
            img.raycastTarget = false;
            img.color = WarmColor();
            if (streak) rt.localRotation = Quaternion.Euler(0f, 0f, 180f);   // 火球頭朝下（落下方向）

            go.AddComponent<UiFallingEmber>().Init(rt, img,
                Random.Range(EmberFallMin, EmberFallMax),
                Random.Range(-EmberDriftMax, EmberDriftMax),
                -h * 0.5f - 80f);
            _embers.Add(go);
        }

        void SpawnFlame()
        {
            float tw = _titleRect.rect.width, th = _titleRect.rect.height;
            var go = new GameObject("Flame", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_titleRect, false);              // 當標題子物件 → 畫在標題圖之上、火舌舔過字
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.15f);
            rt.anchoredPosition = new Vector2(Random.Range(-tw * 0.5f, tw * 0.5f), Random.Range(-th * 0.30f, th * 0.05f));

            // 高瘦火舌：高 = th 的 0.35~0.7，寬只有高的一半左右（柔邊 + 抖動讓它像火而非三角）。
            float height = Random.Range(th * 0.35f, th * 0.7f);
            rt.sizeDelta = new Vector2(height * 0.5f, height);

            var img = go.AddComponent<Image>();
            img.sprite = _flame;
            img.raycastTarget = false;
            img.color = WarmColor();

            go.AddComponent<UiRisingFlame>().Init(rt, img,
                Random.Range(th * 0.4f, th * 0.9f),   // 上竄高度
                Random.Range(0.5f, 0.95f));           // 壽命
            _flames.Add(go);
        }

        static Color WarmColor()
        {
            return Color.Lerp(new Color(1f, 0.32f, 0.06f, 1f), new Color(1f, 0.86f, 0.42f, 1f), Random.value);
        }

        // ── 火舌佔位圖（柔邊火焰：高斯橫向羽化，不再是硬邊三角錐；亮黃芯、橘紅外緣）──
        static Sprite _flameCache;
        static Sprite FlameSprite()
        {
            if (_flameCache != null) return _flameCache;
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            Color core = new Color(1f, 0.95f, 0.62f, 1f);   // 亮黃芯
            Color mid  = new Color(1f, 0.55f, 0.14f, 1f);   // 橘
            Color edge = new Color(0.95f, 0.20f, 0.03f, 1f);// 橘紅外緣
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float nx = (x + 0.5f) / n, ny = (y + 0.5f) / n;
                    // 寬度包絡：底部略窄→下段最寬→往頂收尖（火焰輪廓），全程平滑。
                    float width = Mathf.Lerp(0.30f, 0.015f, Mathf.SmoothStep(0f, 1f, Mathf.Pow(ny, 0.85f)));
                    float dx = Mathf.Abs(nx - 0.5f) / Mathf.Max(0.001f, width);
                    float horiz = Mathf.Exp(-dx * dx * 2.4f);                     // 高斯：邊緣柔化（關鍵，去三角硬邊）
                    float vert = Mathf.SmoothStep(0f, 0.16f, ny)                  // 貼底淡入
                               * Mathf.SmoothStep(1f, 0.72f, ny);                 // 近頂淡出
                    float a = horiz * vert;
                    float coreness = horiz * (1f - ny * 0.55f);                   // 下段中軸最亮
                    Color c = coreness > 0.5f ? Color.Lerp(mid, core, (coreness - 0.5f) * 2f)
                                              : Color.Lerp(edge, mid, coreness * 2f);
                    c.a *= Mathf.Clamp01(a) * 0.92f;
                    px[y * n + x] = c;
                }
            tex.SetPixels32(px); tex.Apply();
            _flameCache = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0f), 64f);
            return _flameCache;
        }
    }

    /// <summary>單顆落火：往下掉 ＋ 水平微飄 ＋ 閃爍，掉出畫面就自毀。用 unscaledTime。</summary>
    [DisallowMultipleComponent]
    public class UiFallingEmber : MonoBehaviour
    {
        RectTransform _rt; Image _img; float _fall, _drift, _killY, _flick, _baseA;
        public void Init(RectTransform rt, Image img, float fall, float drift, float killY)
        {
            _rt = rt; _img = img; _fall = fall; _drift = drift; _killY = killY;
            _flick = Random.value * 10f; _baseA = Random.Range(0.7f, 1f);
        }
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var p = _rt.anchoredPosition;
            p.y -= _fall * dt; p.x += _drift * dt;
            _rt.anchoredPosition = p;
            _flick += dt * 12f;
            var c = _img.color; c.a = _baseA * (0.65f + 0.35f * Mathf.Sin(_flick)); _img.color = c;
            if (p.y < _killY) Destroy(gameObject);
        }
    }

    /// <summary>單道火舌：沿標題往上竄 ＋ 左右擺動 ＋ 寬度抖動閃爍 ＋ 淡入淡出後自毀。用 unscaledTime。</summary>
    [DisallowMultipleComponent]
    public class UiRisingFlame : MonoBehaviour
    {
        RectTransform _rt; Image _img; float _rise, _life, _t, _baseW, _baseH, _seed; Vector2 _start;
        public void Init(RectTransform rt, Image img, float rise, float life)
        {
            _rt = rt; _img = img; _rise = rise; _life = Mathf.Max(0.05f, life);
            _start = rt.anchoredPosition; _baseW = rt.sizeDelta.x; _baseH = rt.sizeDelta.y;
            _seed = Random.value * 100f;
        }
        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = _t / _life;
            if (k >= 1f) { Destroy(gameObject); return; }

            // 上竄（越後越快）＋ 左右擺動。
            var p = _start;
            p.y += _rise * Mathf.SmoothStep(0f, 1f, k);
            p.x += Mathf.Sin((_t + _seed) * 8f) * 6f;
            _rt.anchoredPosition = p;

            // 寬度抖動（柏林噪訊）＋ 往上整體收縮 ＋ 輕微搖擺，讓火舌「活」起來、不像固定形狀。
            float widthFlick = 0.7f + 0.55f * Mathf.PerlinNoise((_t + _seed) * 6f, _seed);
            float shrink = Mathf.Lerp(1f, 0.25f, k);
            _rt.sizeDelta = new Vector2(_baseW * shrink * widthFlick, _baseH * Mathf.Lerp(1f, 0.6f, k));
            _rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((_t + _seed) * 6.5f) * 7f);

            // 淡入（前 18%）→ 淡出，避免「憑空出現一塊」。
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.18f));
            float fadeOut = Mathf.Lerp(0.9f, 0f, Mathf.SmoothStep(0f, 1f, k));
            var c = _img.color; c.a = fadeIn * fadeOut; _img.color = c;
        }
    }
}
