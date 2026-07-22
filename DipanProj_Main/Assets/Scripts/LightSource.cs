using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「會發光的世界物件」標記（地上的佛燈、火把、香爐…）。掛在地上物上（由 MapLoader 依 lightRadius 掛），
/// 自己登記進靜態表；<see cref="AtmosphereController"/> 在暗氛圍下、玩家身上沒發光裝時，取最近的一個當光圈中心。
///
/// 單一光源版：目前只會有一個光圈，所以 <see cref="Nearest"/> 只回最近的一個（多光同框需改 shader，之後再說）。
/// </summary>
public class LightSource : MonoBehaviour
{
    public float radius;

    static readonly List<LightSource> _all = new List<LightSource>();

    void OnEnable()  { if (!_all.Contains(this)) _all.Add(this); }
    void OnDisable() { _all.Remove(this); }   // 被撿走/換圖銷毀時自動退出（Unity 在 OnDestroy 前會先呼叫 OnDisable）

    /// <summary>離 from 最近、且 radius>0 的發光物（沒有回 null）。</summary>
    public static LightSource Nearest(Vector3 from)
    {
        LightSource best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < _all.Count; i++)
        {
            var ls = _all[i];
            if (ls == null || ls.radius <= 0f) continue;
            float d = ((Vector2)ls.transform.position - (Vector2)from).sqrMagnitude;
            if (d < bestSq) { bestSq = d; best = ls; }
        }
        return best;
    }

    /// <summary>清空登記表（PlayModeStaticReset 用；已關 Domain Reload，避免跨 Play 殘留）。</summary>
    public static void ClearAll() => _all.Clear();
}
