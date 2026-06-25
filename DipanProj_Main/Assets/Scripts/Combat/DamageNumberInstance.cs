using UnityEngine;

/// <summary>
/// 浮動傷害數字 — 表演風格【銳利重擊 (punchy)】。
/// 命中瞬間「砰」地爆裂式超大彈出 + **白閃** + 彈性回彈（衝過頭→縮過頭→定格）+ 命中抖動，最後俐落上飄淡出。
/// 主數字與描邊陰影（子物件 "Shadow"）一起動，淡出時兩者 alpha 同步（白閃只作用在主數字）。見 readme/COMBAT.md。
/// （分支 feat/dmgnum-punchy；換風格只改本檔，Manager 共用。要更兇/更收斂就調下方常數。）
/// </summary>
[RequireComponent(typeof(TextMesh))]
public class DamageNumberInstance : MonoBehaviour
{
    const float Overshoot = 1.9f;    // 彈出最大倍率（越大越誇張）
    const float Undershoot = 0.88f;  // 回彈時縮過頭的倍率（< 1 才有 Q 彈感）
    const float ShakeTime = 0.20f;   // 命中抖動持續比例
    const float ShakeAmp = 0.18f;    // 抖動幅度（世界單位）
    const float FlashTime = 0.12f;   // 命中白閃持續比例
    const float RiseStart = 0.5f;    // 後段開始上飄的比例
    const float LateRise = 1.4f;     // 後段上飄速度

    float _life, _maxLife;
    Vector3 _spawn;
    float _seed;

    TextMesh _tm, _shadow;
    Color _baseColor, _shadowBase;

    public void Init(float lifetime, float riseSpeed)
    {
        _maxLife = _life = Mathf.Max(0.05f, lifetime);
        _spawn = transform.position;
        _seed = Random.value * 10f;

        _tm = GetComponent<TextMesh>();
        _baseColor = (_tm != null) ? _tm.color : Color.white;
        var sh = transform.Find("Shadow");
        if (sh != null) { _shadow = sh.GetComponent<TextMesh>(); if (_shadow != null) _shadowBase = _shadow.color; }

        transform.localScale = Vector3.one * 0.3f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _life -= dt;
        if (_life <= 0f) { Destroy(gameObject); return; }
        float t = 1f - _life / _maxLife;

        // 彈性縮放：爆出(0.3→1.9) → 縮過頭(→0.88) → 安定(→1)
        float s;
        if (t < 0.10f) s = Mathf.Lerp(0.3f, Overshoot, EaseOut(t / 0.10f));
        else if (t < 0.22f) s = Mathf.Lerp(Overshoot, Undershoot, (t - 0.10f) / 0.12f);
        else if (t < 0.32f) s = Mathf.Lerp(Undershoot, 1f, (t - 0.22f) / 0.10f);
        else s = 1f;
        SetScale(s);

        // 命中抖動（前段，幅度隨時間衰減）+ 後段俐落上飄
        float shake = (t < ShakeTime) ? (1f - t / ShakeTime) * ShakeAmp : 0f;
        Vector3 jitter = new Vector3(
            (Mathf.PerlinNoise(_seed, Time.time * 50f) - 0.5f) * 2f * shake,
            (Mathf.PerlinNoise(_seed + 7f, Time.time * 50f) - 0.5f) * 2f * shake, 0f);
        float rise = (t > RiseStart) ? (t - RiseStart) * LateRise : 0f;
        transform.position = _spawn + jitter + Vector3.up * rise;

        // 命中白閃（前段由白回到原色）+ 後 35% 淡出
        float whiteMix = (t < FlashTime) ? (1f - t / FlashTime) : 0f;
        float alpha = (t < 0.65f) ? 1f : Mathf.InverseLerp(1f, 0.65f, t);
        ApplyVisual(whiteMix, alpha);
    }

    static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

    void SetScale(float s) => transform.localScale = Vector3.one * s;

    // 主數字：顏色在白與原色間插值（白閃）、alpha 控淡出；陰影：固定黑、只跟 alpha
    void ApplyVisual(float whiteMix, float alpha)
    {
        if (_tm != null)
        {
            Color c = Color.Lerp(_baseColor, Color.white, whiteMix);
            c.a = alpha;
            _tm.color = c;
        }
        if (_shadow != null)
        {
            Color c = _shadowBase;
            c.a = _shadowBase.a * alpha;
            _shadow.color = c;
        }
    }
}
