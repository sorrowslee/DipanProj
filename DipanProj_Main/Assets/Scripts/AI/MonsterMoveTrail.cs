using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **移動拖尾特效**：怪在移動時，持續在**牠身後**（行進方向的反向）種一串 VfxTable 特效。
/// 給「有重量的東西」用——邪佛手掌壓過來時拖出的滾滾沙塵就是這支。
///
/// <para>資料驅動：`MonsterData.csv` 的 <c>MoveTrailFx</c> 欄，格式
/// <c>vfxId:大小倍率:每秒幾個</c>，多層用 <c>|</c> 分隔（例 <c>44:1:5|44:0.6:3</c>）。
/// 留空 ＝ 不掛這個元件，既有怪零影響。</para>
///
/// <para>⭐ <b>為什麼是「種在身後」而不是「腳下」</b>：拖尾要讀得出**行進方向**。
/// 種在正腳下的話，怪停著時會原地堆成一坨、移動時又跟得太緊，看起來像在冒煙而不是在推開地面。
/// 往反方向退 <see cref="BehindRatio"/> 個身寬，那團塵就會留在「牠剛剛輾過的地方」。</para>
///
/// <para>⚠ 排序：特效本身的 <c>SortingOrder</c> 在 VfxTable 裡設（ID 44 填 8 ＜ 角色的 10），
/// 所以沙塵會沉在怪腳下、不會蓋住本體。這裡不碰排序。</para>
///
/// <para>⚠ 用**有縮放的** <c>Time.deltaTime</c>：開背包／對話暫停時不會偷偷累積一堆塵
/// （同 <see cref="MapMonsterRespawner"/> 的計時）。</para>
/// </summary>
[DisallowMultipleComponent]
public class MonsterMoveTrail : MonoBehaviour
{
    /// <summary>一層拖尾。</summary>
    struct Layer
    {
        public int vfxId;
        public float sizeMul;      // 相對 VfxTable 那一列 Scale 的額外倍率
        public float perSecond;    // 每秒種幾個
        public float timer;
    }

    /// <summary>往行進方向的**反向**退多遠（單位：這隻怪的可見身寬）。</summary>
    public float BehindRatio = 0.45f;

    /// <summary>左右交替散開多遠（單位：可見身寬）。0 ＝ 全部排在正中線上（會像一條直線，很假）。</summary>
    public float SpreadRatio = 0.22f;

    /// <summary>每一團的大小隨機範圍（±），避免整串一模一樣。</summary>
    public float SizeJitter = 0.25f;

    /// <summary>位置隨機抖動（單位：可見身寬）。</summary>
    public float PosJitter = 0.12f;

    /// <summary>移動速度低於這個值就當作「停著」，不再種塵。</summary>
    public float MoveThreshold = 0.05f;

    readonly List<Layer> _layers = new List<Layer>();
    MonsterController _self;
    VfxManager _vfx;
    Vector2 _lastPos;
    int _side = 1;
    bool _ready;

    /// <summary>
    /// 由 <see cref="MonsterController.Initialize"/> 呼叫。<paramref name="spec"/> ＝ CSV 的 `MoveTrailFx` 原文。
    /// 解析不出任何一層就自己停用（不留一個每幀空轉的元件）。
    /// </summary>
    public void Configure(MonsterController self, string spec)
    {
        _self = self;
        _layers.Clear();

        if (!string.IsNullOrWhiteSpace(spec))
        {
            foreach (string part in spec.Split('|'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                string[] p = part.Trim().Split(':');
                if (!int.TryParse(p[0].Trim(), out int id) || id <= 0)
                {
                    Debug.LogWarning($"[MonsterMoveTrail]「{self?.MonsterName}」的 MoveTrailFx 片段「{part}」" +
                                     "解析不出 vfxId，略過。格式：vfxId:大小倍率:每秒幾個（多層用 | 分隔）");
                    continue;
                }
                float size = 1f, rate = 5f;
                if (p.Length > 1 && !string.IsNullOrWhiteSpace(p[1])) float.TryParse(p[1].Trim(), out size);
                if (p.Length > 2 && !string.IsNullOrWhiteSpace(p[2])) float.TryParse(p[2].Trim(), out rate);
                _layers.Add(new Layer
                {
                    vfxId = id,
                    sizeMul = Mathf.Max(0.05f, size),
                    perSecond = Mathf.Clamp(rate, 0.1f, 60f),
                    // 每層的相位錯開，不然兩層永遠同時出現、看起來像一團
                    timer = Random.value,
                });
            }
        }

        _ready = _layers.Count > 0;
        enabled = _ready;
        _lastPos = transform.position;

        if (_ready)
            Debug.Log($"[MonsterMoveTrail]「{self?.MonsterName}」掛上 {_layers.Count} 層移動拖尾：{spec}");
    }

    void Update()
    {
        if (!_ready || _self == null || _self.IsDead) return;

        Vector2 pos = transform.position;
        Vector2 delta = pos - _lastPos;
        _lastPos = pos;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 停著就不種（拖尾是「被推開的地面」，不是這隻怪在冒煙）
        float speed = delta.magnitude / dt;
        if (speed < MoveThreshold) return;

        Vector2 dir = delta.normalized;
        Vector2 side = new Vector2(-dir.y, dir.x);          // 行進方向的左手邊
        float bodyW = Mathf.Max(0.2f, BodyWidth());

        if (_vfx == null) _vfx = Object.FindObjectOfType<VfxManager>();
        if (_vfx == null) return;

        for (int i = 0; i < _layers.Count; i++)
        {
            var L = _layers[i];
            L.timer += dt * L.perSecond;
            while (L.timer >= 1f)
            {
                L.timer -= 1f;
                Spawn(L, dir, side, bodyW);
            }
            _layers[i] = L;
        }
    }

    void Spawn(Layer L, Vector2 dir, Vector2 side, float bodyW)
    {
        // 起點是**腳底**（畫在地上的東西一律對腳底，不要用 transform——那是畫布中心，見 PROBLEMS G13）
        Vector2 p = _self.FeetWorldPos;

        p -= dir * (BehindRatio * bodyW);                       // 退到身後＝牠剛輾過的地方
        p += side * (_side * SpreadRatio * bodyW);              // 左右交替，讓拖尾有寬度
        p += new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (PosJitter * bodyW);
        _side = -_side;

        float scale = L.sizeMul * (1f + Random.Range(-SizeJitter, SizeJitter));
        _vfx.Spawn(L.vfxId, p, 0f, scale);
    }

    /// <summary>這隻怪的可見身寬（世界單位，已含體型）。拿碰撞框量，體型一改就自動跟著變（同 F29 的通則）。</summary>
    float BodyWidth()
    {
        var col = GetComponent<Collider2D>();
        return col != null ? col.bounds.size.x : 1f;
    }
}
