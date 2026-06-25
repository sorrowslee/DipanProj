using UnityEngine;

/// <summary>
/// 單一浮動傷害數字的「動態表演」：往上飄 + 前段彈出(pop) + 後段淡出，壽命到自毀。
/// 主數字與描邊陰影（子物件 "Shadow"）一起縮放/位移，淡出時兩者 alpha 同步。
///
/// **換表演風格主要改本檔的 <see cref="Animate"/>**（噴飛彈跳 / 銳利重擊 / 旋轉甩飛 / 拖曳殘影…）。
/// `SetScale` / `SetAlpha` 已幫你同步好主數字與陰影，動畫只要呼叫它們即可。見 readme/COMBAT.md。
/// </summary>
[RequireComponent(typeof(TextMesh))]
public class DamageNumberInstance : MonoBehaviour
{
    protected float _life;
    protected float _maxLife;
    protected float _riseSpeed;
    protected float _driftX;

    TextMesh _tm;        // 主數字
    TextMesh _shadow;    // 描邊陰影（子物件）
    Color _baseColor;
    Color _shadowBase;

    public void Init(float lifetime, float riseSpeed)
    {
        _maxLife = _life = Mathf.Max(0.05f, lifetime);
        _riseSpeed = riseSpeed;
        _driftX = Random.Range(-0.4f, 0.4f);

        _tm = GetComponent<TextMesh>();
        _baseColor = (_tm != null) ? _tm.color : Color.white;
        var sh = transform.Find("Shadow");
        if (sh != null)
        {
            _shadow = sh.GetComponent<TextMesh>();
            if (_shadow != null) _shadowBase = _shadow.color;
        }

        SetScale(0.6f);   // 從小彈出
        OnInit();
    }

    /// <summary>各表演風格可在此做初始化（隨機初速、角速度…）。</summary>
    protected virtual void OnInit() { }

    void Update()
    {
        _life -= Time.deltaTime;
        if (_life <= 0f) { Destroy(gameObject); return; }
        Animate(Time.deltaTime, 1f - _life / _maxLife);   // t: 0→1
    }

    /// <summary>動態表演本體。t = 已過比例(0→1)。換風格改這裡。</summary>
    protected virtual void Animate(float dt, float t)
    {
        // 上升 + 輕微水平漂移
        transform.position += new Vector3(_driftX * dt, _riseSpeed * dt, 0f);

        // pop：前 15% 0.6→1.05，接著回落到 1
        float s = (t < 0.15f)
            ? Mathf.Lerp(0.6f, 1.05f, t / 0.15f)
            : Mathf.Lerp(1.05f, 1f, Mathf.InverseLerp(0.15f, 0.35f, t));
        SetScale(s);

        // 後 30% 淡出
        SetAlpha(t < 0.7f ? 1f : Mathf.InverseLerp(1f, 0.7f, t));
    }

    // ── 共用 helper：同步主數字 + 陰影 ──

    protected void SetScale(float s) => transform.localScale = Vector3.one * s;

    protected void SetAlpha(float a)
    {
        if (_tm != null) { var c = _baseColor; c.a = _baseColor.a * a; _tm.color = c; }
        if (_shadow != null) { var c = _shadowBase; c.a = _shadowBase.a * a; _shadow.color = c; }
    }
}
