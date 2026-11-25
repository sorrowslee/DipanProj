using UnityEngine;

/// <summary>
/// 捲入死亡演出（2026-11-25，古神黑洞）：怪物被「致死捲入」的地面特效打死時，用牠死前那一格的樣子做一個殘影，
/// 一邊繞著黑洞中心旋轉、一邊縮小、被吸進中心後消失。
///
/// <para>由 <see cref="MonsterController"/> 的 LateUpdate 在銷毀自己之前呼叫 <see cref="Spawn"/>；
/// 殘影是獨立物件，不受怪物銷毀影響。純視覺：不碰傷害、掉寶、旗標（那些照正常死亡流程走）。</para>
///
/// <para>⚠ 用遊戲時間（Time.deltaTime）：暫停面板開著時演出一起停。</para>
/// </summary>
public class SuckInDeathFx : MonoBehaviour
{
    /// <summary>整段演出秒數。</summary>
    public const float Duration = 0.6f;
    /// <summary>捲入過程繞中心轉幾圈（殘影本身也同步自轉，看起來是被漩渦捲走）。</summary>
    const float Turns = 1.25f;
    /// <summary>最後這段比例開始淡出（前面維持不透明，看得清是誰被吸進去）。</summary>
    const float FadeFrom = 0.65f;

    SpriteRenderer _sr;
    Vector2 _center;
    float _startRadius, _startAngle, _t;
    Vector3 _baseScale;
    Color _baseColor;

    /// <summary>以 <paramref name="source"/> 目前的圖、翻面、大小、排序生一個殘影，捲進 <paramref name="center"/>。來源沒有圖就什麼都不做。</summary>
    public static void Spawn(SpriteRenderer source, Vector2 center)
    {
        if (source == null || source.sprite == null) return;

        var go = new GameObject("SuckInDeathFx");
        go.transform.position = source.transform.position;
        go.transform.localScale = source.transform.lossyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.color = source.color;
        sr.sharedMaterial = source.sharedMaterial;   // 同一個材質（SpriteFlash）：沒有 MPB ⇒ 不閃白、不染狀態色，就是牠原本的樣子
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder;

        var fx = go.AddComponent<SuckInDeathFx>();
        fx._sr = sr;
        fx._center = center;
        Vector2 off = (Vector2)go.transform.position - center;
        fx._startRadius = off.magnitude;
        fx._startAngle = Mathf.Atan2(off.y, off.x);
        fx._baseScale = go.transform.localScale;
        fx._baseColor = sr.color;
    }

    void Update()
    {
        _t += Time.deltaTime;
        float u = Mathf.Clamp01(_t / Duration);
        float pull = u * u;                         // 先慢後快：被吸住、越轉越快掉進去
        float ang = _startAngle + pull * Turns * Mathf.PI * 2f;
        float r = _startRadius * (1f - pull);
        transform.position = new Vector3(_center.x + Mathf.Cos(ang) * r, _center.y + Mathf.Sin(ang) * r, transform.position.z);
        transform.localEulerAngles = new Vector3(0f, 0f, -pull * Turns * 360f);
        float s = Mathf.Max(0f, 1f - pull);
        transform.localScale = new Vector3(_baseScale.x * s, _baseScale.y * s, _baseScale.z);
        if (u > FadeFrom)
        {
            Color c = _baseColor;
            c.a *= 1f - Mathf.InverseLerp(FadeFrom, 1f, u);
            _sr.color = c;
        }
        if (u >= 1f) Destroy(gameObject);
    }
}
