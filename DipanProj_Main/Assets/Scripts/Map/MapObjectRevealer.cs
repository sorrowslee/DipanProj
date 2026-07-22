using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「靠旗標中途現身」的地上物顯現管理器（每張地圖一個，掛在地圖物件根下，換圖時隨 MapRoot 一起銷毀）。
///
/// 由 <see cref="MapLoader"/> 建立：進圖時凡是有 appearFlag 但旗標尚未成立的地上物，都「先建好、藏起來」
/// （關掉 SpriteRenderer/碰撞、動畫先不播），並登記到這裡。之後接 <see cref="TriggerChain.OnFlagFirstSet"/>——
/// 旗標首次成立時，把對應的物件（延遲 appearDelaySeconds 秒後）現身：開碰撞、（可選）淡入、動畫從第0幀起播
/// （播一次的停在最後一幀）。
///
/// 典型：打敗榕樹妖→對話結束→setFlag → 鬼魂與跪拜的紅嫁衣現身、紅嫁衣播一次跪拜停在跪姿。
/// </summary>
public class MapObjectRevealer : MonoBehaviour
{
    class Hidden
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Collider2D col;
        public AnimatedMapObject anim;   // 可為 null（非動畫物件）
        public float delay;
        public bool fade;
    }

    // 旗標裸名字 → 等待現身的物件清單。
    readonly Dictionary<string, List<Hidden>> _byFlag = new Dictionary<string, List<Hidden>>();

    // 旗標裸名字 → 旗標成立時要「消失（銷毀）」的物件清單（disappearFlag，與 _byFlag 相反）。
    readonly Dictionary<string, List<GameObject>> _disappearByFlag = new Dictionary<string, List<GameObject>>();

    void OnEnable()  { TriggerChain.OnFlagFirstSet += OnFlagFirstSet; }
    void OnDisable() { TriggerChain.OnFlagFirstSet -= OnFlagFirstSet; }

    /// <summary>登記一個「先藏起來、等旗標現身」的地上物。由 MapLoader 在建物件時呼叫。</summary>
    public void RegisterHidden(string flag, GameObject go, SpriteRenderer sr, Collider2D col,
                               AnimatedMapObject anim, float delaySeconds, bool fade)
    {
        if (string.IsNullOrEmpty(flag) || go == null) return;
        string key = flag.Trim();
        if (!_byFlag.TryGetValue(key, out var list)) { list = new List<Hidden>(); _byFlag[key] = list; }
        list.Add(new Hidden { go = go, sr = sr, col = col, anim = anim, delay = delaySeconds, fade = fade });
    }

    /// <summary>登記一個「等旗標成立就消失（銷毀）」的地上物。由 MapLoader 在建物件時呼叫（disappearFlag）。</summary>
    public void RegisterDisappear(string flag, GameObject go)
    {
        if (string.IsNullOrEmpty(flag) || go == null) return;
        string key = flag.Trim();
        if (!_disappearByFlag.TryGetValue(key, out var list)) { list = new List<GameObject>(); _disappearByFlag[key] = list; }
        list.Add(go);
    }

    void OnFlagFirstSet(string flagKey)
    {
        if (string.IsNullOrEmpty(flagKey)) return;
        string key = flagKey.Trim();

        // 消失：旗標成立 → 把登記在此旗標下的地上物銷毀（碰撞一併移除、路自動開通）。
        if (_disappearByFlag.TryGetValue(key, out var goList))
        {
            _disappearByFlag.Remove(key);   // 只處理一次
            foreach (var go in goList)
                if (go != null) Destroy(go);
        }

        if (!_byFlag.TryGetValue(key, out var list)) return;
        _byFlag.Remove(key);   // 只現身一次
        foreach (var h in list)
            if (h != null && h.go != null) StartCoroutine(RevealAfter(h));
    }

    IEnumerator RevealAfter(Hidden h)
    {
        if (h.delay > 0f) yield return new WaitForSeconds(h.delay);
        if (h == null || h.go == null) yield break;   // 期間換圖/銷毀

        // 淡入時先把 alpha 設 0，避免開啟當幀閃一下全不透明。
        if (h.fade && h.sr != null) { var c = h.sr.color; c.a = 0f; h.sr.color = c; }

        if (h.col != null) h.col.enabled = true;
        if (h.sr != null)  h.sr.enabled = true;
        if (h.anim != null) h.anim.PlayFromStart();    // 動畫一出現就從第0幀播（播一次的停最後一幀）

        if (h.fade && h.sr != null) yield return FadeIn(h.sr);
    }

    static IEnumerator FadeIn(SpriteRenderer sr)
    {
        const float dur = 0.45f;
        float t = 0f;
        Color c = sr.color;
        while (t < dur && sr != null)
        {
            t += Time.deltaTime;
            c.a = Mathf.Clamp01(t / dur);
            sr.color = c;
            yield return null;
        }
        if (sr != null) { c.a = 1f; sr.color = c; }
    }
}
