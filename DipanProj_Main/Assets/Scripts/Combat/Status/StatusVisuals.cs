using System.Collections.Generic;
using UnityEngine;
using Dipan.Vfx;

/// <summary>
/// 狀態效果的<b>身上呈現</b>（2026-11-18，見 readme/STATUS_EFFECT.md §5）：染色、中毒的綠泡、怪物頭上的圖示（燃燒只靠染色）。
/// 玩家頭上不放圖示（玩家看底部狀態列 StatusBarView）。
///
/// <para>由 <see cref="StatusController"/> 在第一次有狀態時掛上；之後只在狀態增減時 <see cref="Refresh"/>。
/// <b>LateUpdate 只在有東西要跟著動時才開</b>（有泡泡、或頭上有圖示），其餘時間元件停用——大批怪物時不吃 Update。</para>
///
/// <para>染色走 <c>HitReactionHandler.SetStatusTint</c>（那個 renderer 的 MPB 唯一寫入點），只看減益：DoT ＞ 控制 ＞ 數值減益，
/// 增益不染色（例：Nidhogg 一直在上增傷，玩家不該一直是橘色的）。</para>
/// </summary>
[DisallowMultipleComponent]
public class StatusVisuals : MonoBehaviour
{
    // ── 可調常數 ──
    const float TintStrength = 0.35f;     // 染色強度（0～1）
    const int MaxHeadIcons = 4;           // 怪物頭上最多幾個圖示
    const float HeadIconSize = 0.34f;     // 頭上圖示邊長（世界單位）
    const float HeadIconGap = 0.05f;
    const float HeadIconLift = 0.22f;     // 圖示中心離頭頂多高
    const int HeadIconOrder = 23900;      // 在角色之上、傷害數字（24000）之下
    const float BubbleRate = 5f;          // 中毒每秒冒幾顆泡
    static readonly Color TextShadow = new Color(0f, 0f, 0f, 0.85f);

    StatusController _sc;
    MonsterController _mc;
    PlayerController _pc;
    HitReactionHandler _hr;
    SpriteRenderer _bodySr;
    readonly List<StatusController.View> _views = new List<StatusController.View>();

    // 頭上圖示（只有怪物）：不掛在怪物底下——怪物的 Scale 會把圖示一起放大縮小。OnDestroy 一起收。
    Transform _iconRoot;
    readonly List<SpriteRenderer> _iconSr = new List<SpriteRenderer>();
    readonly List<TextMesh> _iconTm = new List<TextMesh>();
    readonly List<TextMesh> _iconTmShadow = new List<TextMesh>();
    int _shown;

    // 身上特效
    Color _poisonColor;
    bool _poisoned;
    float _bubbleAcc;

    void Awake()
    {
        _sc = GetComponent<StatusController>();
        _mc = GetComponent<MonsterController>();
        _pc = GetComponent<PlayerController>();
        _hr = GetComponent<HitReactionHandler>();
        _bodySr = GetComponent<SpriteRenderer>();
        if (_bodySr == null) _bodySr = GetComponentInChildren<SpriteRenderer>();
        enabled = false;
    }

    bool Dead => (_mc != null && _mc.IsDead) || (_pc != null && _pc.IsDead);

    /// <summary>狀態增減時由 StatusController 呼叫。</summary>
    public void Refresh()
    {
        if (_sc == null) return;
        if (_hr == null) _hr = GetComponent<HitReactionHandler>();   // 掛上時機可能早於 HitReactionHandler
        _sc.GetViews(_views);

        // 1) 染色：DoT ＞ 控制 ＞ 數值減益；增益不染
        Color tint = Color.white;
        int best = 0;
        _poisoned = false;
        for (int i = 0; i < _views.Count; i++)
        {
            var d = _views[i].Def;
            // 燃燒（Fire）不放火、只靠染色（作者 2026-11-18：玩家與怪物都不要身上的火）
            if (d.Category == StatusCategory.Dot && d.DamageType == DamageType.Poison) { _poisoned = true; _poisonColor = d.Color; }
            if (d.Kind != StatusKind.Debuff) continue;
            int pri = d.Category == StatusCategory.Dot ? 3 : d.Category == StatusCategory.Control ? 2 : 1;
            if (pri > best) { best = pri; tint = d.Color; }
        }
        if (_hr != null) _hr.SetStatusTint(tint, best > 0 ? TintStrength : 0f);

        // 2) 怪物頭上圖示
        if (_mc != null) RebuildHeadIcons();

        enabled = !Dead && (_poisoned || _shown > 0);
    }

    void RebuildHeadIcons()
    {
        // 減益（含 DoT、控制）排前面，再來增益；各自照表上順序
        int n = 0;
        for (int pass = 0; pass < 2 && n < MaxHeadIcons; pass++)
            for (int i = 0; i < _views.Count && n < MaxHeadIcons; i++)
            {
                var v = _views[i];
                bool debuff = v.Def.Kind == StatusKind.Debuff;
                if ((pass == 0) != debuff) continue;
                var sp = StatusIcons.Get(v.Def);
                if (sp == null) continue;
                EnsureIcon(n);
                _iconSr[n].sprite = sp;
                _iconSr[n].gameObject.SetActive(true);
                bool stack = v.Stacks > 1;
                _iconTm[n].gameObject.SetActive(stack);
                _iconTmShadow[n].gameObject.SetActive(stack);
                if (stack) { _iconTm[n].text = v.Stacks.ToString(); _iconTmShadow[n].text = _iconTm[n].text; }
                n++;
            }
        for (int i = n; i < _iconSr.Count; i++) _iconSr[i].gameObject.SetActive(false);
        _shown = n;
        if (_iconRoot != null) _iconRoot.gameObject.SetActive(n > 0);
        if (n > 0) LayoutIcons();
    }

    void EnsureIcon(int i)
    {
        if (_iconRoot == null)
        {
            var root = new GameObject("StatusIcons_" + name);
            _iconRoot = root.transform;
        }
        while (_iconSr.Count <= i)
        {
            var go = new GameObject("Icon" + _iconSr.Count);
            go.transform.SetParent(_iconRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = HeadIconOrder;
            _iconSr.Add(sr);
            _iconTmShadow.Add(MakeStackText(go.transform, TextShadow, HeadIconOrder + 1, new Vector3(0.025f, -0.025f, 0f)));
            _iconTm.Add(MakeStackText(go.transform, Color.white, HeadIconOrder + 2, Vector3.zero));
        }
    }

    static TextMesh MakeStackText(Transform parent, Color c, int order, Vector3 offset)
    {
        var go = new GameObject("Stack");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0.32f, -0.3f, 0f) + offset;   // 圖示右下角
        var tm = go.AddComponent<TextMesh>();
        tm.font = Dipan.UI.UIBuilder.DefaultFont;
        tm.fontSize = 48;
        tm.characterSize = 0.09f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = c;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && tm.font != null) { mr.sharedMaterial = tm.font.material; mr.sortingOrder = order; }
        go.SetActive(false);
        return tm;
    }

    void LayoutIcons()
    {
        float step = HeadIconSize + HeadIconGap;
        float x0 = -(_shown - 1) * step * 0.5f;
        float s = HeadIconSize;   // sprite 1 單位寬（PixelsPerUnit 128、一格 128px）
        for (int i = 0; i < _shown; i++)
        {
            var t = _iconSr[i].transform;
            t.localPosition = new Vector3(x0 + i * step, 0f, 0f);
            t.localScale = new Vector3(s, s, 1f);
        }
    }

    void LateUpdate()
    {
        if (Dead) { HideAll(); enabled = false; return; }

        Vector2 feet, center;
        float h;
        if (_mc != null) { feet = _mc.FeetWorldPos; center = _mc.BodyCenterWorldPos; h = _mc.VisibleBodyHeight; }
        else if (_pc != null) { feet = _pc.FeetWorldPos; center = _pc.BodyCenterWorldPos; h = _pc.VisibleBodyHeight; }
        else return;

        string layer = _bodySr != null ? _bodySr.sortingLayerName : "Default";
        int order = _bodySr != null ? _bodySr.sortingOrder : 0;

        // 中毒：從身上往上冒綠色小泡（共用 VfxEmbers，全場上限 400 顆，不會無限長）
        if (_poisoned)
        {
            _bubbleAcc += Time.deltaTime * BubbleRate;
            while (_bubbleAcc >= 1f)
            {
                _bubbleAcc -= 1f;
                var p = new Vector2(center.x + Random.Range(-0.25f, 0.25f) * h * 0.5f,
                                    center.y + Random.Range(-0.35f, 0.2f) * h);
                VfxEmbers.Emit(p, new Vector2(Random.Range(-0.08f, 0.08f), Random.Range(0.35f, 0.7f)),
                               _poisonColor, Random.Range(0.05f, 0.09f), Random.Range(0.6f, 1.0f), layer, order + 2);
            }
        }

        // 頭上圖示
        if (_shown > 0 && _iconRoot != null)
            _iconRoot.position = new Vector3(feet.x, feet.y + h + HeadIconLift, 0f);
    }

    void HideAll()
    {
        if (_iconRoot != null) _iconRoot.gameObject.SetActive(false);
        _shown = 0;
        if (_hr != null) _hr.SetStatusTint(Color.white, 0f);
    }

    void OnDestroy()
    {
        if (_iconRoot != null) Destroy(_iconRoot.gameObject);
    }
}
