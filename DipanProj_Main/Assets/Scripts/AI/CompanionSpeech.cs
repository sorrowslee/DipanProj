using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 劇本同伴的頭上說話（2026-11-07，血狂之爭：選邊後跟著玩家的首領也會講話）。
///
/// <para>為什麼不用 <see cref="MonsterSpeech"/>：那套是「怪物發現玩家後才講」＋血量門檻句，
/// 同伴不會「發現玩家」（它就在玩家身邊、是 PlayerAlly），而且同伴要的是另一種節奏——平常閒聊、門一開就催玩家走。</para>
///
/// <para><b>兩組台詞</b>（MonsterData 表尾欄，一格多句用全形「／」分隔，見 <see cref="MonsterSpawner"/>）：</para>
/// <list type="bullet">
///   <item><b>閒聊</b> <c>CompanionLines</c>：平常每 <see cref="ChatIntervalMin"/>~<see cref="ChatIntervalMax"/> 秒隨機講一句。</item>
///   <item><b>催促</b> <c>CompanionUrgeLines</c>：本圖有傳送點「進圖之後才被打開、現在還開著」（<see cref="TriggerChain.PortalOpenedThisMap"/>）
///         ⇒ 門一開 <see cref="UrgeFirstDelay"/> 秒後先喊一句，之後每 <see cref="UrgeIntervalMin"/>~<see cref="UrgeIntervalMax"/> 秒一句，只講這組。
///         換圖（門的狀態重算）就回到閒聊。</item>
/// </list>
/// 同一組不會連續兩次挑到同一句。用有縮放的時間：對話、背包等暫停面板開著時不會講。
/// 掛法：<see cref="CompanionService.Summon"/> 生成同伴時，兩組有任一組有台詞才掛。退場時 <see cref="Silence"/>。
/// </summary>
public class CompanionSpeech : MonoBehaviour
{
    // ───────── 節奏（作者 2026-11-07：催促「不需要太常播放，5~6 秒一次即可」）─────────
    const float ChatIntervalMin = 9f, ChatIntervalMax = 13f;          // 閒聊：每句之間
    const float ChatFirstDelayMin = 3f, ChatFirstDelayMax = 7f;       // 閒聊：剛出現／從催促回到閒聊後第一句
    const float UrgeIntervalMin = 5f, UrgeIntervalMax = 6f;           // 催促：每句之間
    const float UrgeFirstDelay = 0.6f;                                // 門一開到第一句催促（跟開門的畫面錯開一點點）
    const float BubbleDuration = 2f;

    MonsterController _mc;
    float _next = float.MaxValue;
    bool _urging;
    bool _silenced;
    int _lastChat = -1, _lastUrge = -1;

    public void Configure(MonsterController mc)
    {
        _mc = mc;
        _urging = false;
        _next = Time.time + Random.Range(ChatFirstDelayMin, ChatFirstDelayMax);
    }

    /// <summary>之後不再自己開口（退場演出要講自己的那句時用）。</summary>
    public void Silence() => _silenced = true;

    void Update()
    {
        if (_mc == null || _mc.IsDead || _silenced) return;

        bool hasUrge = _mc.CompanionUrgeLines != null && _mc.CompanionUrgeLines.Count > 0;
        bool doorOpen = hasUrge && TriggerChain.PortalOpenedThisMap;
        if (doorOpen != _urging)
        {
            _urging = doorOpen;
            _next = Time.time + (_urging ? UrgeFirstDelay : Random.Range(ChatFirstDelayMin, ChatFirstDelayMax));
        }
        if (Time.time < _next) return;

        if (_urging)
        {
            Say(_mc.CompanionUrgeLines, ref _lastUrge);
            _next = Time.time + Random.Range(UrgeIntervalMin, UrgeIntervalMax);
        }
        else
        {
            Say(_mc.CompanionLines, ref _lastChat);
            _next = Time.time + Random.Range(ChatIntervalMin, ChatIntervalMax);
        }
    }

    void Say(List<string> pool, ref int last)
    {
        if (pool == null || pool.Count == 0) return;
        int i = Random.Range(0, pool.Count);
        if (pool.Count > 1 && i == last) i = (i + 1 + Random.Range(0, pool.Count - 1)) % pool.Count;   // 不連續兩次同一句
        last = i;
        Dipan.UI.MonsterSpeechPanel.Speak(_mc, pool[i], BubbleDuration);
    }
}
