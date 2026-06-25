using UnityEngine;

/// <summary>
/// 浮動傷害數字 — 表演風格【銳利重擊 (punchy)】。
/// 原地「砰」地爆裂式超大彈出(overshoot)、命中瞬間輕微抖動，回彈定格後俐落上飄淡出。乾淨有力、不花俏。
/// 主數字與描邊陰影（子物件 "Shadow"）一起動，淡出時兩者 alpha 同步。見 readme/COMBAT.md。
/// （分支 feat/dmgnum-punchy；換風格只改本檔，Manager 共用。）
/// </summary>
[RequireComponent(typeof(TextMesh))]
public class DamageNumberInstance : MonoBehaviour
{
    const float Overshoot = 1.5f;    // 彈出最大倍率
    const float ShakeTime = 0.16f;   // 命中抖動持續比例
    const float ShakeAmp = 0.10f;    // 抖動幅度（世界單位）
    const float LateRise = 1.1f;     // 後段上飄速度

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

        transform.localScale = Vector3.one * 0.2f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _life -= dt;
        if (_life <= 0f) { Destroy(gameObject); return; }
        float t = 1f - _life / _maxLife;

        // 爆裂彈出：0.2→Overshoot（前 12%，急煞）→ 回落到 1（28% 定格）
        float s;
        if (t < 0.12f) s = Mathf.Lerp(0.2f, Overshoot, EaseOut(t / 0.12f));
        else if (t < 0.28f) s = Mathf.Lerp(Overshoot, 1f, (t - 0.12f) / 0.16f);
        else s = 1f;
        SetScale(s);

        // 命中抖動（前段）+ 後段俐落上飄
        float shake = (t < ShakeTime) ? (1f - t / ShakeTime) * ShakeAmp : 0f;
        Vector3 jitter = new Vector3(
            (Mathf.PerlinNoise(_seed, Time.time * 45f) - 0.5f) * shake,
            (Mathf.PerlinNoise(_seed + 5f, Time.time * 45f) - 0.5f) * shake, 0f);
        float rise = (t > 0.45f) ? (t - 0.45f) * LateRise : 0f;
        transform.position = _spawn + jitter + Vector3.up * rise;

        SetAlpha(t < 0.65f ? 1f : Mathf.InverseLerp(1f, 0.65f, t));
    }

    static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

    void SetScale(float s) => transform.localScale = Vector3.one * s;

    void SetAlpha(float a)
    {
        if (_tm != null) { var c = _baseColor; c.a = _baseColor.a * a; _tm.color = c; }
        if (_shadow != null) { var c = _shadowBase; c.a = _shadowBase.a * a; _shadow.color = c; }
    }
}
