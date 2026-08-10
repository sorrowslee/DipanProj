using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「會發光的世界物件」標記（地上的佛燈、火把、燈籠、香爐…）。掛在地上物上（由 MapLoader 依「發光半徑」掛），
/// 自己登記進靜態表；<see cref="AtmosphereController"/> 每幀取「離玩家最近的 N 盞」餵給氛圍 shader。
///
/// 多光源版（2026-08-10）：先前只支援一盞（Nearest 只回最近的一個、shader 只有一組光圈參數），
/// 一整排火炬只有最近那支會亮。現在改成一次可同框 <see cref="AtmosphereController.MaxLights"/> 盞，
/// 且每盞有自己的亮度／光色／搖晃節奏（各自的亂數種子 → 不會整場同步呼吸）。
///
/// 參數全部來自地圖編輯器的地上物面板（發光半徑／亮度／光色／搖晃強度／搖晃速度／邊緣柔和度），
/// 存在 .dipanmap 的 objects[] 裡，由 MapLoader 原樣轉進本元件。
/// </summary>
public class LightSource : MonoBehaviour
{
    /// <summary>發光半徑（世界單位）＝光照得到多遠。0 以下＝不發光（不會被收集）。</summary>
    public float radius;

    /// <summary>亮度倍率：1＝標準；&lt;1 微光（快熄的燭火）；&gt;1 刺眼（爐火、探照燈）。</summary>
    public float intensity = 1f;

    /// <summary>光色（暖橘＝火把、慘白＝月光、青綠＝鬼火）。只影響色偏，不影響亮度。</summary>
    public Color color = DefaultWarm;

    /// <summary>搖晃強度：0＝完全不動（電燈、月光）；1＝標準燭火；2＝狂亂火焰。</summary>
    public float flicker = 1f;

    /// <summary>搖晃速度倍率：小＝油燈慢慢晃；大＝營火急促跳動。</summary>
    public float flickerSpeed = 1f;

    /// <summary>邊緣柔和度＝內圈(全亮)半徑佔外圈的比例。小＝瀰漫柔邊；大＝範圍內均勻、邊緣硬（聚光燈）。</summary>
    public float softness = 0.46f;

    /// <summary>預設暖橘（火把/油燈）。編輯器沒填光色時用這個。</summary>
    public static readonly Color DefaultWarm = new Color(1.00f, 0.78f, 0.52f, 1f);

    // 每盞燈自己的相位種子 → 同一張圖上的火把不會同步呼吸（單光源時代是全域共用一組，看起來很假）。
    private float _seed;

    static readonly List<LightSource> _all = new List<LightSource>();

    void Awake() { _seed = Random.value * 1000f; }

    void OnEnable()  { if (!_all.Contains(this)) _all.Add(this); }
    void OnDisable() { _all.Remove(this); }   // 被撿走/破壞/換圖銷毀時自動退出（Unity 在 OnDestroy 前會先呼叫 OnDisable）

    /// <summary>
    /// 這一瞬間的「搖晃倍率」（1 附近）。半徑與亮度共用同一個值 → 火焰變大時也變亮，比只縮放半徑自然。
    /// 搖晃強度 0 時固定回 1（完全不動）。沿用單光源時代的油燈曲線：Perlin 慢漂為主 + 一點慢正弦。
    /// </summary>
    public float Breathe(float time)
    {
        if (flicker <= 0.001f) return 1f;
        float sp = Mathf.Max(0.01f, flickerSpeed);
        float slow = Mathf.PerlinNoise(_seed, time * 0.55f * sp);
        float fast = Mathf.Sin(time * 3.2f * sp + _seed) * 0.5f + 0.5f;
        float mix  = Mathf.Clamp01(0.85f * slow + 0.15f * fast);
        // flicker=1 時等同舊值 0.93~1.06；flicker=0 不動；flicker=2 幅度加倍。
        return Mathf.Lerp(1f - 0.07f * flicker, 1f + 0.06f * flicker, mix);
    }

    /// <summary>
    /// 取離 <paramref name="from"/> 最近的 <paramref name="max"/> 盞發光物，寫進 <paramref name="result"/>（由近到遠）。
    /// 排序鍵用「距離 − 半徑」：遠處一盞大燈的光圈可能仍照到畫面，不該被近處的小燭火擠掉。
    /// </summary>
    public static void CollectNearest(Vector3 from, List<LightSource> result, int max)
    {
        result.Clear();
        if (max <= 0) return;

        for (int i = 0; i < _all.Count; i++)
        {
            var ls = _all[i];
            if (ls == null || ls.radius <= 0f || ls.intensity <= 0f) continue;

            float key = ((Vector2)ls.transform.position - (Vector2)from).magnitude - ls.radius;

            // 插入排序進固定長度的小清單（max 很小，成本可忽略；避免每幀配置陣列/Sort 的 GC）。
            int at = result.Count;
            while (at > 0 && SortKey(result[at - 1], from) > key) at--;
            if (at >= max) continue;                       // 比清單裡最遠的還遠 → 丟掉
            result.Insert(at, ls);
            if (result.Count > max) result.RemoveAt(result.Count - 1);
        }
    }

    static float SortKey(LightSource ls, Vector3 from)
        => ((Vector2)ls.transform.position - (Vector2)from).magnitude - ls.radius;

    /// <summary>清空登記表（PlayModeStaticReset 用；已關 Domain Reload，避免跨 Play 殘留）。</summary>
    public static void ClearAll() => _all.Clear();
}
