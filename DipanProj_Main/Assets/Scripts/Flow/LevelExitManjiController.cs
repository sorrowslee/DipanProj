using System.Collections;
using UnityEngine;

namespace Dipan.Flow
{
    /// <summary>
    /// 過關／死亡的「卍字離場」世界座標特效（全程式、零 prefab）。沿用開場墜落的旋轉卍字（金→紫）美術，
    /// 但改成世界座標、包裹玩家：
    ///   ① 玩家身後出現一個很大的旋轉卍字（金色、淡入）。
    ///   ② 邊旋轉邊縮小，同時把玩家縮小吞入（金→紫）。
    ///   ③ 帶著玩家飛上天、淡出離開關卡。
    /// 播放期間遊戲已被 GameFlowManager 暫停（timeScale=0），本特效一律用 unscaledDeltaTime。
    /// 玩家的縮放會被縮到 ~0（視覺上被吞掉）；還原由呼叫端（GameFlowManager）在回廣場前做。
    ///
    /// 卍字圖：優先 Resources/InitialStory/Manji（與開場同一張）；載不到用程序生成（MakeManji，複製自 IntroFallController）。
    /// </summary>
    public class LevelExitManjiController : MonoBehaviour
    {
        // ── 節奏 / 外觀常數（要調表演改這裡）──
        const float FadeInTime = 0.45f;   // 卍字淡入（大、旋轉）
        const float WrapTime   = 1.25f;   // 縮小＋把玩家吞入
        const float RiseTime   = 0.95f;   // 飛上天＋淡出
        const float RotateSpeed = 210f;   // 旋轉速度（度/秒）
        const float StartSizeMul = 6.0f;  // 起始卍字大小 = 玩家高度 × 此
        const float EndSizeMul   = 1.15f; // 吞入時卍字大小 = 玩家高度 × 此
        const float RiseDistMul  = 9.0f;  // 飛上天距離 = 玩家高度 × 此
        const float MaxAlpha = 0.95f;
        // 排序：要壓在世界特效（VfxManager 預設 22000，如榕樹妖死亡火焰）與角色之上，卍字飛上天才不會被蓋住。
        // 16-bit 安全（<32767）；UI 是螢幕覆蓋層、永遠在更上面，不受影響。
        const int SortingOrder = 25000;

        static readonly Color Gold   = new Color(1f, 0.80f, 0.42f, 1f);   // 神聖（起始）
        static readonly Color Purple = new Color(0.56f, 0.30f, 0.78f, 1f); // 墮落（吞入時）

        /// <summary>播放離場特效；播完呼叫 onDone。player 可為 null（就只播卍字、不吞玩家）。</summary>
        public static void Play(Transform player, System.Action onDone)
        {
            var go = new GameObject("[LevelExitManji]");
            var c = go.AddComponent<LevelExitManjiController>();
            c.StartCoroutine(c.Run(player, onDone));
        }

        IEnumerator Run(Transform player, System.Action onDone)
        {
            // 玩家尺寸/位置（相機此時已暫停，位置固定）
            SpriteRenderer psr = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            float playerH = (psr != null && psr.bounds.size.y > 0.01f) ? psr.bounds.size.y : 1.95f;
            Vector3 center = player != null ? player.position : Vector3.zero;
            Vector3 playerOrigScale = player != null ? player.localScale : Vector3.one;

            // 卍字物件
            var manjiGo = new GameObject("Manji");
            manjiGo.transform.position = center;
            var sr = manjiGo.AddComponent<SpriteRenderer>();
            sr.sprite = ManjiSprite;
            sr.sortingOrder = SortingOrder;   // 壓在世界特效（含死亡火焰 22000）與角色之上，飛上天不被蓋住

            float spriteWorld = sr.sprite.bounds.size.y;   // scale=1 時的世界高（512px/100ppu≈5.12）
            if (spriteWorld < 0.01f) spriteWorld = 1f;
            float startScale = (playerH * StartSizeMul) / spriteWorld;
            float endScale   = (playerH * EndSizeMul) / spriteWorld;

            float angle = 0f;

            // ── 階段 ①：淡入（大、金色、旋轉）──
            for (float t = 0f; t < FadeInTime; t += Time.unscaledDeltaTime)
            {
                float k = t / FadeInTime;
                angle += RotateSpeed * Time.unscaledDeltaTime;
                SetManji(sr, manjiGo.transform, center, angle, startScale, Gold, MaxAlpha * k);
                yield return null;
            }

            // ── 階段 ②：縮小 + 把玩家吞入（金→紫）──
            for (float t = 0f; t < WrapTime; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / WrapTime);
                angle += RotateSpeed * Time.unscaledDeltaTime;
                float scale = Mathf.Lerp(startScale, endScale, k);
                Color col = Color.Lerp(Gold, Purple, k);
                SetManji(sr, manjiGo.transform, center, angle, scale, col, MaxAlpha);
                if (player != null) player.localScale = Vector3.Lerp(playerOrigScale, playerOrigScale * 0.02f, k);
                yield return null;
            }
            if (player != null) player.localScale = playerOrigScale * 0.02f;   // 玩家縮到幾乎不見（被吞）

            // ── 階段 ③：飛上天 + 淡出（連同玩家一起帶走）──
            float rise = playerH * RiseDistMul;
            for (float t = 0f; t < RiseTime; t += Time.unscaledDeltaTime)
            {
                float k = t / RiseTime;
                float ease = k * k;   // 加速上升
                angle += RotateSpeed * 1.4f * Time.unscaledDeltaTime;
                Vector3 pos = center + Vector3.up * (rise * ease);
                float scale = Mathf.Lerp(endScale, endScale * 0.5f, k);
                SetManji(sr, manjiGo.transform, pos, angle, scale, Purple, MaxAlpha * (1f - k));
                if (player != null) player.position = pos;   // 玩家跟著飛走
                yield return null;
            }

            Destroy(manjiGo);
            Destroy(gameObject);
            onDone?.Invoke();
        }

        static void SetManji(SpriteRenderer sr, Transform tr, Vector3 pos, float angle, float scale, Color col, float alpha)
        {
            tr.position = pos;
            tr.localEulerAngles = new Vector3(0f, 0f, angle);
            tr.localScale = Vector3.one * scale;
            var c = col; c.a = Mathf.Clamp01(alpha);
            sr.color = c;
        }

        // ───────────────────────── 卍字圖（載不到就程序生成）─────────────────────────

        static Sprite _sprite;
        static Sprite ManjiSprite
        {
            get
            {
                if (_sprite == null)
                {
                    // 用 Texture2D 載入＋建 Sprite（免去 PNG 未設 Sprite 類型的雷；同 IntroFallController）。
                    var tex = Resources.Load<Texture2D>("InitialStory/Manji");
                    if (tex == null) tex = MakeManji(512);
                    _sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
                return _sprite;
            }
        }

        // 程序生成毛筆草書卍字（左旋）。複製自 IntroFallController.MakeManji，維持一致外觀。
        static Texture2D MakeManji(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float c = (n - 1) * 0.5f;
            float L = n * 0.285f;
            float F = n * 0.245f;
            float baseHalf = n * 0.062f;
            float shear = 0.07f;

            var seg = new float[][]
            {
                new[]{ -L, 0f,  L, 0f, 0f },
                new[]{ 0f,-L,  0f, L, 0f },
                new[]{ 0f, L, -F, L, 1f },
                new[]{ -L, 0f,-L,-F, 1f },
                new[]{ 0f,-L,  F,-L, 1f },
                new[]{  L, 0f,  L, F, 1f },
            };

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float qx = x - c, qy = y - c;
                    qx += shear * qy;
                    float best = 0f;
                    for (int i = 0; i < seg.Length; i++)
                    {
                        var s = seg[i];
                        float vx = s[2] - s[0], vy = s[3] - s[1];
                        float wx = qx - s[0], wy = qy - s[1];
                        float len2 = vx * vx + vy * vy;
                        float tt = len2 > 1e-4f ? Mathf.Clamp01((wx * vx + wy * vy) / len2) : 0f;
                        float cx = s[0] + tt * vx, cy = s[1] + tt * vy;
                        float d = Mathf.Sqrt((qx - cx) * (qx - cx) + (qy - cy) * (qy - cy));
                        float w = baseHalf * (0.68f + 0.62f * Mathf.PerlinNoise(i * 5.3f + tt * 4.2f, 1.3f));
                        if (s[4] > 0.5f) w *= Mathf.SmoothStep(0f, 0.34f, 1f - tt);
                        float en = (Mathf.PerlinNoise(qx * 0.05f + 9f, qy * 0.05f + 4f) - 0.5f) * baseHalf * 0.85f;
                        float a = Mathf.Clamp01((w + en - d) / (baseHalf * 0.42f));
                        float dry = Mathf.PerlinNoise(qx * 0.035f - 3f, qy * 0.035f + i * 2f);
                        a *= Mathf.Clamp01(0.5f + 0.95f * dry);
                        best = Mathf.Max(best, a);
                    }
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(best));
                }
            t.SetPixels(px); t.Apply();
            return t;
        }
    }
}
