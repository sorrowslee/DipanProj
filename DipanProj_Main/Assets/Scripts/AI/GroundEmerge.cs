using UnityEngine;

/// <summary>
/// 讓一隻怪（目前只有墨子的機關塔）**從土裡升起**、時間到再**沉回土裡**（2026-11-24）。
///
/// 做法：在牠腳底那條線放一個看不見的 <see cref="SpriteMask"/>（只罩「地面以上」），牠的 SpriteRenderer 改成只在遮罩內顯示；
/// 然後把整隻往下移一個身高、再往上推回原位——超出地面以下的部分被遮掉，看起來就是從土裡鑽出來。
/// 升起時左右抖動、在塔身中心冒一團煙（VfxTable <see cref="DustVfxId"/>，大小對齊塔高）；影子在升起完成前藏起來（影子在地面上，不該先出現）。
/// ⚠ 2026-11-24 試過兩版都被作者退：① VfxTable 8「沙土爆炸」（以中心為錨的大爆炸，生在腳底 ⇒ 一半在地下、比塔大兩倍）；
///   ② 特效庫 fx1_impact_dust 棕色貼地塵浪（不適合）。③ 現在＝作者在特效預覽器挑的 fanfx1_magic_puff_B 白色（VfxTable 62「機關塔出場煙」），
///   畫布下緣貼地面線、畫布高＝塔高 × <see cref="DustSizeMul"/>：煙從土裡冒出往上飄，塔跟著在煙裡升起，煙散了塔已經站好。
///
/// ⚠ 為什麼不用 AutoSprite 做 deploy 動畫：AutoSprite 拿一張圖做動作，畫面裡沒有地面，只能原地伸展、做不出「從土裡出來」（作者 2026-11-24 試過）。
/// ⚠ 移動的是整隻（route B 怪的 SpriteRenderer 在根物件上），碰撞框也會跟著移；升起期間大腦什麼都不做，所以沒差。
/// </summary>
[DisallowMultipleComponent]
public class GroundEmerge : MonoBehaviour
{
    /// <summary>升起／沉下要幾秒。</summary>
    public float RiseSeconds = 0.6f;
    public float SinkSeconds = 0.5f;
    /// <summary>左右抖動幅度（× 可見身高）。</summary>
    public float ShakeAmount = 0.03f;
    /// <summary>出場／退場的煙（VfxTable；62＝機關塔出場煙）。0＝不放。</summary>
    public int DustVfxId = 62;
    /// <summary>煙的畫布高 ＝ 可見身高 × 此值。這套圖（64×64）的雲團寬約占畫布 63%，從畫布下緣冒出、一路往上飄到頂，
    /// 所以 1.7 ≈ 雲團跟塔一樣寬。</summary>
    public float DustSizeMul = 1.7f;
    /// <summary>畫布中心離地面線多高（× 畫布高）。0.4 ＝畫布下緣在地面下一點點：煙從土裡冒出、往上蓋過整座塔。</summary>
    public float DustCenterUp = 0.4f;

    enum Phase { None, Rising, Up, Sinking, Gone }
    Phase _phase = Phase.None;
    float _t;
    Vector3 _home;          // 升起完成時的位置
    float _depth;           // 往下藏多深（＝可見身高＋一點餘裕）
    GameObject _maskGo;
    SpriteRenderer _sr;
    Rigidbody2D _rb;
    MonsterController _mc;
    BlobShadow _shadow;
    System.Action _onSunk;
    Vector2 _groundFeet;    // 地面線（升起完成時的腳底）
    float _height = 1.2f;   // 可見身高

    public bool IsUp => _phase == Phase.Up;
    public bool IsBusy => _phase == Phase.Rising || _phase == Phase.Sinking;

    /// <summary>開始升起。只在第一次呼叫有效。</summary>
    public void BeginRise()
    {
        if (_phase != Phase.None) return;
        _mc = GetComponent<MonsterController>();
        _sr = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        _shadow = GetComponent<BlobShadow>();
        _home = transform.position;

        float h = _mc != null && _mc.VisibleBodyHeight > 0.01f ? _mc.VisibleBodyHeight : 1.2f;
        _depth = h * 1.15f;
        Vector2 feet = _mc != null ? _mc.FeetWorldPos : (Vector2)transform.position;
        _groundFeet = feet;
        _height = h;

        // 遮罩：底邊貼在腳底線，往上蓋一大塊（4 倍身高見方）
        _maskGo = new GameObject(name + "_GroundMask");
        _maskGo.transform.SetParent(transform.parent, false);
        var tex = Texture2D.whiteTexture;
        var mask = _maskGo.AddComponent<SpriteMask>();
        mask.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), tex.width);
        _maskGo.transform.position = new Vector3(feet.x, feet.y, 0f);
        _maskGo.transform.localScale = new Vector3(h * 4f, h * 4f, 1f);
        if (_sr != null) _sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        if (_shadow != null) _shadow.SetVisible(false);
        Dust(feet);
        SetY(_home.y - _depth);
        _phase = Phase.Rising;
        _t = 0f;
    }

    /// <summary>開始沉回土裡；沉完呼叫 <paramref name="onSunk"/>（通常是銷毀自己）。</summary>
    public void BeginSink(System.Action onSunk)
    {
        if (_phase == Phase.Sinking || _phase == Phase.Gone) return;
        if (_phase == Phase.None) { onSunk?.Invoke(); _phase = Phase.Gone; return; }
        _onSunk = onSunk;
        _home = new Vector3(transform.position.x, _phase == Phase.Up ? transform.position.y : _home.y, transform.position.z);
        if (_shadow != null) _shadow.SetVisible(false);
        if (_mc != null) { _groundFeet = _mc.FeetWorldPos; if (_mc.VisibleBodyHeight > 0.01f) _height = _mc.VisibleBodyHeight; }
        Dust(_groundFeet);
        _phase = Phase.Sinking;
        _t = 0f;
    }

    void LateUpdate()
    {
        if (_phase != Phase.Rising && _phase != Phase.Sinking) return;
        _t += Time.deltaTime;
        float h = _depth / 1.15f;
        float shake = Mathf.Sin(_t * 70f) * ShakeAmount * h;
        if (_phase == Phase.Rising)
        {
            float u = Mathf.Clamp01(_t / Mathf.Max(0.01f, RiseSeconds));
            float e = 1f - (1f - u) * (1f - u) * (1f - u);   // ease-out：一開始衝得快、最後穩住
            SetY(_home.y - _depth * (1f - e), shake * (1f - u));
            if (u >= 1f)
            {
                SetY(_home.y, 0f);
                _phase = Phase.Up;
                if (_sr != null) _sr.maskInteraction = SpriteMaskInteraction.None;
                if (_maskGo != null) { Destroy(_maskGo); _maskGo = null; }
                if (_shadow != null) _shadow.SetVisible(true);
            }
        }
        else
        {
            if (_maskGo == null && _sr != null)
            {
                // 沉下要重新掛遮罩（升起完成時已拆掉）
                Vector2 feet = _mc != null ? _mc.FeetWorldPos : (Vector2)transform.position;
                float hh = _mc != null && _mc.VisibleBodyHeight > 0.01f ? _mc.VisibleBodyHeight : h;
                _maskGo = new GameObject(name + "_GroundMask");
                _maskGo.transform.SetParent(transform.parent, false);
                var tex = Texture2D.whiteTexture;
                var mask = _maskGo.AddComponent<SpriteMask>();
                mask.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), tex.width);
                _maskGo.transform.position = new Vector3(feet.x, feet.y, 0f);
                _maskGo.transform.localScale = new Vector3(hh * 4f, hh * 4f, 1f);
                _sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
            float u = Mathf.Clamp01(_t / Mathf.Max(0.01f, SinkSeconds));
            SetY(_home.y - _depth * u * u, shake);
            if (u >= 1f)
            {
                _phase = Phase.Gone;
                var cb = _onSunk; _onSunk = null;
                cb?.Invoke();
            }
        }
    }

    void SetY(float y, float dx = 0f)
    {
        var p = new Vector3(_home.x + dx, y, _home.z);
        transform.position = p;
        if (_rb != null) _rb.position = p;
    }

    /// <summary>在塔的位置放一團煙（從地面冒出往上飄），畫布高對齊塔高（VfxTable 的 Scale 之上再乘倍率）。<paramref name="feet"/>＝地面線。</summary>
    void Dust(Vector2 feet)
    {
        if (DustVfxId <= 0) return;
        var vfx = FindObjectOfType<VfxManager>();
        if (vfx == null) return;
        var data = vfx.GetEffect(DustVfxId);
        float extra = 1f, canvasWorldH = _height * DustSizeMul;
        if (data != null && data.AnimationSprites != null && data.AnimationSprites.Length > 0 && data.AnimationSprites[0] != null)
        {
            var sp = data.AnimationSprites[0];
            float canvasH = sp.pixelsPerUnit > 0.0001f ? sp.rect.height / sp.pixelsPerUnit : sp.bounds.size.y;   // 用畫布高，不用 bounds（Tight 網格第一幀只有一小點）
            float nativeH = canvasH * Mathf.Max(0.0001f, data.Scale);
            if (nativeH > 0.0001f) extra = (_height * DustSizeMul) / nativeH;
        }
        vfx.Spawn(DustVfxId, feet + new Vector2(0f, canvasWorldH * DustCenterUp), 0f, extra);
    }

    void OnDestroy()
    {
        if (_maskGo != null) Destroy(_maskGo);
    }
}
