using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dipan.UI
{
    /// <summary>
    /// 怪物頭上對話框（覆蓋層、不擋輸入、不暫停）。**通用元件**：可同時掛多個氣泡，各自跟著一隻怪物的頭頂跑。
    /// 由 <see cref="MonsterSpeech"/>（掛在怪物上）呼叫 <see cref="Speak"/> 生一個氣泡；顯示 duration 秒後淡出，
    /// 怪物死亡（IsDead / 物件被銷毀）立即移除。
    ///
    /// 底板美術：Resources/UI/InGame/InGame_TalkBg1、InGame_TalkBg2 兩張水墨泡泡，**隨機輪流**（載不到自動退回程序底板）。
    /// 兩張都是「尾巴在底部、預設放在怪物右上」畫的：
    ///   ‧ 左上（靠畫面右緣） → 底板水平鏡像（尾巴改指右下）。
    ///   ‧ 腳下（靠畫面上緣） → 底板垂直鏡像（尾巴改指上）。
    ///   ‧ 文字本身永遠不鏡像；用底板的「奶油色內文區」定位（鏡像時同步換到對應那側）。
    /// 定位（螢幕座標，沿用 PlayerHintPanel 的 WorldToScreen 做法）：把「尾巴尖」對到怪物頭頂/腳下，方向在開口那一刻決定、之後固定。
    /// 見 readme/UI_SYSTEM.md。
    /// </summary>
    public class MonsterSpeechPanel : UIPanel
    {
        public override UILayer Layer => UILayer.HUD;       // 常駐抬頭層：壓在背包/設定等視窗之下，合理
        public override bool BlocksGameplayInput => false;
        public override bool PausesGame => false;
        public override bool CloseOnEscape => false;
        public override bool InStack => false;

        // ── 版面常數（1920x1080 基準；要調氣泡大小改這裡）──
        const float BubbleWidth = 336f;     // 氣泡寬（高依原圖比例自動；不拉伸變形）。280×1.2＝336
        const int MaxFont = 40, MinFont = 22; // 文字 best-fit 上下限（短句大、長句自動縮）；面板放大＋字級上限拉高＝字更清楚
        const float HGap = 12f, VGap = 6f;  // 尾巴尖對到頭頂後，往氣泡本體方向再推一點（讓它偏右上/左上）
        const float FadeIn = 0.12f, FadeOut = 0.3f;

        // 邊緣判定門檻（viewport 0~1）：太靠左/右 → 對話翻到另一側；太靠上 → 改到腳下。
        const float LeftEdge = 0.25f, RightEdge = 0.75f, TopEdge = 0.80f;

        static readonly Color TextColor = new Color(0.12f, 0.09f, 0.08f, 1f);   // 深墨色（奶油底板上要用深字）

        // ── 兩張底板的資料（尾巴尖 + 奶油內文區，皆為原圖 0~1 比例；y 由「上」算）──
        //   量測自 InGame_TalkBg1/2.png（577x433，透明背景）。
        struct BubbleArt
        {
            public Sprite Sprite;
            public Vector2 TailTip;   // 尾巴尖（x 從左, y 從上）
            public Vector4 Cream;     // 內文奶油區 (x0, y0上, x1, y1上)
        }
        static BubbleArt[] _arts;

        static BubbleArt[] Arts
        {
            get
            {
                // null-check 自動重建（關掉 Domain Reload 後上一輪 Play 的 sprite 會被銷毀，這裡自動重載）。
                if (_arts == null || _arts.Length == 0 || _arts[0].Sprite == null)
                {
                    _arts = new BubbleArt[]
                    {
                        new BubbleArt { Sprite = LoadSprite("UI/InGame/InGame_TalkBg1"),
                                        TailTip = new Vector2(0.46f, 0.80f),
                                        Cream = new Vector4(0.185f, 0.229f, 0.841f, 0.619f) },
                        new BubbleArt { Sprite = LoadSprite("UI/InGame/InGame_TalkBg2"),
                                        TailTip = new Vector2(0.28f, 0.74f),
                                        Cream = new Vector4(0.205f, 0.266f, 0.802f, 0.594f) },
                    };
                }
                return _arts;
            }
        }

        // Resources 載圖：優先當 Sprite；若匯入型別是 Texture 就自己 Create 一張；都失敗回 null（呼叫端退回程序底板）。
        static Sprite LoadSprite(string path)
        {
            // UI/Texts/ 底下的是「圖片型文字」：實際檔案在 UI/Texts/<語言>/ 裡，
            // 這裡解析成當前語言的路徑，缺當前語言就退回母版（繁中）。見 Localization/LocalizedArt。
            path = Dipan.Localization.LocalizedArt.ResolveExisting(path);

            var sp = Resources.Load<Sprite>(path);
            if (sp != null) return sp;
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            Debug.LogWarning($"[MonsterSpeechPanel] 載不到底板 Resources/{path}（型別非 Sprite/Texture？）→ 暫用程序底板。");
            return null;
        }

        class Bubble
        {
            public RectTransform Root;
            public CanvasGroup Cg;
            public MonsterController Mc;
            public float Spawn;
            public float Duration;
            public bool Below;       // 錨在腳下（true）或頭頂（false）
            public float OffsetX, OffsetY;
        }

        readonly List<Bubble> _bubbles = new List<Bubble>();
        Camera _cam;
        int _artFlip;   // 兩張輪流（避免連續同一張）

        protected override void OnBuild() { /* 氣泡動態生成，這裡不建固定版面 */ }

        // ───────────────────────── 對外 API ─────────────────────────

        /// <summary>在某隻怪物頭上顯示一句話（duration 秒後淡出；怪物死亡立即消失）。</summary>
        public static void Speak(MonsterController mc, string text, float duration)
        {
            if (mc == null || string.IsNullOrEmpty(text)) return;
            var p = UIManager.Instance?.Open<MonsterSpeechPanel>();
            p?.AddBubble(mc, text, duration);
        }

        void AddBubble(MonsterController mc, string text, float duration)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // 同一隻怪若已有氣泡 → 先移除舊的（避免疊字）。
            for (int i = _bubbles.Count - 1; i >= 0; i--)
                if (_bubbles[i].Mc == mc) Remove(i);

            // 方向（開口這一刻決定）：靠左緣→右上、靠右緣→左上、中間隨機；靠上緣→改腳下。
            Vector3 head = HeadWorld(mc);
            Vector3 vp = _cam.WorldToViewportPoint(head);
            bool rightSide = vp.x < LeftEdge ? true : (vp.x > RightEdge ? false : (Random.value < 0.5f));
            bool below = vp.y > TopEdge;

            // 兩張輪流挑一張
            var arts = Arts;
            _artFlip ^= 1;
            var art = arts[_artFlip % arts.Length];

            // 容器（本體大小固定、保持原圖比例，不拉伸）
            var rootGo = UIBuilder.Create("SpeechBubble", transform);
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            float aspectH = 433f / 577f;   // 兩張同尺寸；有 sprite 就用 sprite 實際比例
            if (art.Sprite != null && art.Sprite.rect.width > 0f)
                aspectH = art.Sprite.rect.height / art.Sprite.rect.width;
            root.sizeDelta = new Vector2(BubbleWidth, BubbleWidth * aspectH);

            // 底板（填滿容器；鏡像只作用在底板，文字不受影響）
            Image bg;
            if (art.Sprite != null)
            {
                bg = UIBuilder.Image(root, "Bg", art.Sprite, Color.white);
                bg.type = Image.Type.Simple;
            }
            else
            {
                bg = UIBuilder.Image(root, "Bg", FallbackSprite, new Color(0.07f, 0.07f, 0.10f, 0.85f));
                bg.type = Image.Type.Sliced;
            }
            bg.raycastTarget = false;
            var bgrt = bg.rectTransform;
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
            bgrt.pivot = new Vector2(0.5f, 0.5f);
            bgrt.localScale = new Vector3(rightSide ? 1f : -1f, below ? -1f : 1f, 1f);   // 鏡像

            // 文字（放進奶油內文區；鏡像時同步換到對應那側；best-fit 自動縮放）
            var txt = UIBuilder.Text(root, "Msg", text, MaxFont, TextColor, TextAnchor.MiddleCenter);
            txt.raycastTarget = false;
            txt.fontStyle = FontStyle.Bold;   // 加粗，深墨色在奶油底上更清楚
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = MinFont;
            txt.resizeTextMaxSize = MaxFont;
            var ol = txt.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 1f, 1f, 0.35f);   // 淡淺色描邊，壓在墨邊上也讀得清楚
            ol.effectDistance = new Vector2(1f, -1f);
            ApplyTextRect(txt.rectTransform, art.Cream, rightSide, below);

            var cg = rootGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // 尾巴尖當錨點：算出對應 pivot（鏡像後尾巴換角）。
            float tx = rightSide ? art.TailTip.x : 1f - art.TailTip.x;
            float tyTop = below ? 1f - art.TailTip.y : art.TailTip.y;
            root.pivot = new Vector2(tx, 1f - tyTop);

            var b = new Bubble
            {
                Root = root, Cg = cg, Mc = mc,
                Spawn = Time.time, Duration = Mathf.Max(0.3f, duration),
                Below = below,
                OffsetX = rightSide ? HGap : -HGap,
                OffsetY = below ? -VGap : VGap,
            };
            _bubbles.Add(b);
            Reposition(b);   // 立刻擺對位置，避免第一幀出現在畫面中央
        }

        // 把文字 RectTransform 貼到底板的「奶油內文區」（鏡像時換到對應那一側，文字本身不翻）。
        static void ApplyTextRect(RectTransform trt, Vector4 cream, bool rightSide, bool below)
        {
            float cx0 = cream.x, cyTop0 = cream.y, cx1 = cream.z, cyTop1 = cream.w;
            float ax0, ax1;
            if (rightSide) { ax0 = cx0; ax1 = cx1; }
            else { ax0 = 1f - cx1; ax1 = 1f - cx0; }   // 水平鏡像
            float ay0, ay1;
            if (!below) { ay0 = 1f - cyTop1; ay1 = 1f - cyTop0; }   // y 由上轉為由下
            else { ay0 = cyTop0; ay1 = cyTop1; }                     // 垂直鏡像
            trt.anchorMin = new Vector2(ax0, ay0);
            trt.anchorMax = new Vector2(ax1, ay1);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
        }

        // ───────────────────────── 每幀更新 ─────────────────────────

        void Update()
        {
            if (!IsOpen || _bubbles.Count == 0) return;
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            float now = Time.time;
            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                var b = _bubbles[i];

                // 怪物死亡 / 被銷毀 → 立即移除氣泡
                if (b.Mc == null || b.Mc.IsDead) { Remove(i); continue; }

                float elapsed = now - b.Spawn;
                if (elapsed >= b.Duration) { Remove(i); continue; }

                Reposition(b);

                float a = 1f;
                if (elapsed < FadeIn) a = elapsed / FadeIn;
                else if (elapsed > b.Duration - FadeOut) a = Mathf.Clamp01((b.Duration - elapsed) / FadeOut);
                b.Cg.alpha = a;
            }
        }

        void Reposition(Bubble b)
        {
            Vector3 anchorWorld = b.Below ? FeetWorld(b.Mc) : HeadWorld(b.Mc);
            Vector2 screen = _cam.WorldToScreenPoint(anchorWorld);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, null, out Vector2 local))
                return;
            b.Root.anchoredPosition = local + new Vector2(b.OffsetX, b.OffsetY);
        }

        void Remove(int i)
        {
            var b = _bubbles[i];
            if (b.Root != null) Destroy(b.Root.gameObject);
            _bubbles.RemoveAt(i);
        }

        protected override void OnClose()
        {
            for (int i = _bubbles.Count - 1; i >= 0; i--) Remove(i);
        }

        // 頭頂 / 腳下世界座標：優先用碰撞框上下緣，抓不到就用 transform 加預設高度。
        static Vector3 HeadWorld(MonsterController mc)
        {
            var col = mc.GetComponent<Collider2D>();
            if (col != null) return new Vector3(col.bounds.center.x, col.bounds.max.y, mc.transform.position.z);
            return mc.transform.position + Vector3.up * 1.2f;
        }

        static Vector3 FeetWorld(MonsterController mc)
        {
            var col = mc.GetComponent<Collider2D>();
            if (col != null) return new Vector3(col.bounds.center.x, col.bounds.min.y, mc.transform.position.z);
            return mc.transform.position;
        }

        // ───────────────────────── 後備底板（載不到美術時用；程序生成圓角矩形）─────────────────────────

        static Sprite _fallback;
        static Sprite FallbackSprite
        {
            get
            {
                if (_fallback == null)
                {
                    const int S = 64, R = 20;
                    var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
                    { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                    var px = new Color32[S * S];
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                        {
                            float cx = Mathf.Clamp(x, R, S - 1 - R);
                            float cy = Mathf.Clamp(y, R, S - 1 - R);
                            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                            float a = Mathf.Clamp01(R + 0.5f - d);
                            px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                        }
                    tex.SetPixels32(px);
                    tex.Apply();
                    _fallback = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f),
                        100f, 0, SpriteMeshType.FullRect, new Vector4(R + 2, R + 2, R + 2, R + 2));
                }
                return _fallback;
            }
        }
    }
}
