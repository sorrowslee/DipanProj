using UnityEngine;

/// <summary>
/// 劇本同伴「說完話往下走、走出畫面就消失」（2026-11-07，血狂之爭大門道別）。由 <see cref="CompanionService.LeaveAll"/> 換上。
///
/// 流程：原地站 <c>talkSeconds</c>（頭上那句道別剛跳出來，讓玩家看完）→ 往畫面下方走 →
/// 身體中心離開鏡頭下緣 <see cref="OffscreenMargin"/>（視窗比例）就算走出畫面：通知 + 銷毀。
/// 走了 <see cref="WalkTimeout"/> 秒還在畫面裡（被牆擋住、鏡頭跟著拉下去…）⇒ 原地淡出 <see cref="FadeSeconds"/> 秒再銷毀，保證一定會收掉、鏈不會卡住。
/// 時間用 ctx.DeltaTime（有縮放）：暫停面板開著時不會走。
/// </summary>
public class CompanionLeaveBrain : IMonsterBrain
{
    const float WalkTimeout = 6f;
    const float FadeSeconds = 0.5f;
    const float OffscreenMargin = 0.15f;   // route B 怪物 pivot 在畫布中心（PROBLEMS G13）：中心再往下約半個身高才算整隻離開畫面

    readonly float _talkSeconds;
    readonly System.Action _onGone;
    float _t;
    float _fade = -1f;
    bool _init, _done;
    Vector2 _dest;
    SpriteRenderer[] _renderers;
    Color[] _baseColors;

    public CompanionLeaveBrain(float talkSeconds, System.Action onGone)
    {
        _talkSeconds = Mathf.Max(0f, talkSeconds);
        _onGone = onGone;
    }

    public void Think(in MonsterContext ctx)
    {
        var self = ctx.Self;
        var act = ctx.Actuator;
        if (_done || self == null || act == null) return;

        if (!_init)
        {
            _init = true;
            // 收掉同伴戰鬥時的姿勢接管（夏綠蒂的原地旋轉＝BrainPoseActive＋attack 循環）與揮到一半的攻擊，不然會邊轉邊走。
            self.BrainPoseActive = false;
            if (self.Anim != null) self.Anim.CancelOneShot();
            _dest = (Vector2)act.transform.position + Vector2.down * 40f;
        }

        _t += ctx.DeltaTime;

        if (_fade >= 0f)
        {
            act.Stop();
            _fade += ctx.DeltaTime;
            SetAlpha(1f - Mathf.Clamp01(_fade / FadeSeconds));
            if (_fade >= FadeSeconds) Finish(self);
            return;
        }

        if (_t < _talkSeconds) { act.Stop(); return; }

        act.MoveTowards(_dest);

        var cam = Camera.main;
        if (cam != null && cam.WorldToViewportPoint(act.transform.position).y < -OffscreenMargin)
        {
            Finish(self);
            return;
        }
        if (_t - _talkSeconds > WalkTimeout)
        {
            Debug.Log($"[CompanionLeaveBrain] 同伴「{self.MonsterName}」走了 {WalkTimeout} 秒還在畫面裡，改成原地淡出。");
            _fade = 0f;
            _renderers = self.GetComponentsInChildren<SpriteRenderer>();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;
        }
    }

    void SetAlpha(float k)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            var c = _baseColors[i]; c.a *= k; _renderers[i].color = c;
        }
    }

    void Finish(MonsterController self)
    {
        _done = true;
        try { _onGone?.Invoke(); }
        catch (System.Exception e) { Debug.LogError($"[CompanionLeaveBrain] 退場完成回呼丟出例外：{e}"); }
        Object.Destroy(self.gameObject);
    }
}
