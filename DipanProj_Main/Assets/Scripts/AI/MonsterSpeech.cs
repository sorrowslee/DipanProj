using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一句怪物台詞（由 CSV 句子1~句子4 解析而來，見 MonsterSpawner.ParseSpeechLine）。
/// </summary>
[System.Serializable]
public struct MonsterSpeechLine
{
    /// <summary>血量比例（%）≤ 此值才「解鎖」這句。無「N%:」前綴的句子＝100（血量永遠 ≤100，故一直可講）。</summary>
    public float UnlockAtPercent;
    /// <summary>要顯示的台詞文字。</summary>
    public string Text;
}

/// <summary>
/// 怪物「遊戲中說話」：發現玩家後，每隔一段時間從「目前血量已解鎖」的台詞裡隨機挑一句，
/// 在頭上跳出對話框（實際畫面交給 <see cref="Dipan.UI.MonsterSpeechPanel"/>）。
///
/// 掛法：由 <see cref="MonsterController"/> 在 Start 時「CSV 有填句子才自動掛」（同 BlobShadow 的自掛慣例）。
/// 資料來源：<see cref="MonsterController.SpeechLines"/>（來自 MonsterData 的 CSV）。
/// 解鎖判定：句子的 <see cref="MonsterSpeechLine.UnlockAtPercent"/> ≥ 當前血量% 才可講
///   → 例：門檻30 的句子在血量剩 30% 以下才會被挑到；無前綴（門檻100）一直可講。
///
/// <para><b>⭐ 門檻句是「跌破就一定喊」，不是只丟進隨機池</b>（2026-09-16）：帶「N%:」前綴的句子，在血量
/// <b>第一次</b> ≤ N 的那一刻**立刻強制播報**——不看間隔、不擲 <see cref="SpeakChance"/>、也不要求已經發現玩家
/// （會掉血就表示玩家早就動手了）。這樣才做得出「boss 放大絕時喊那句話」：紅嫁衣的
/// 「50%: 家人們，一起出來吧」與 <c>RedBridalGownBrain</c> 的大絕門檻是同一個 0.5，兩者自然同時發生。
/// 舊行為（只解鎖、之後隨機挑）保留：喊過的門檻句照樣留在池子裡，之後還會不時再講。</para>
/// 怪物死亡：MonsterController 死亡當幀銷毀 → 本元件與對話框一起消失（對話框由 Panel 端偵測 IsDead 立即移除）。
/// </summary>
public class MonsterSpeech : MonoBehaviour
{
    // ───────── 頻率參數（要調說話快慢/多寡改這裡）─────────
    // 「一句講完」到「下一次可能開口」的平均間隔秒數。想更聒噪就調小、更安靜就調大。
    const float SpeakIntervalSeconds = 10f;
    // 間隔的隨機抖動比例（±45%），讓多隻怪的節奏彼此錯開、不會整齊劃一。
    const float IntervalJitter = 0.45f;
    // 每次「時間到」時，真的開口的機率（0~1）。< 1 = 有時選擇不說 → 避免場上多隻同時發話、也讓整體更稀疏。
    const float SpeakChance = 0.55f;
    // 第一次發現玩家後，隔多久才第一次「可能開口」：取這個範圍的隨機值，讓每隻怪的起始時機都不同（去同步）。
    const float FirstDelayMin = 1.5f, FirstDelayMax = 9f;
    // 對話框顯示秒數（間隔是從「這句開始」再加這個時間起算）。
    const float BubbleDuration = 2.0f;

    // ── boss 專用加乘（boss 是主要劇情所在，講多一點且別因運氣不好整場沉默）──
    const float BossIntervalMul = 0.5f;   // 間隔（含第一次）減半＝頻率兩倍
    const float BossSpeakChance = 0.9f;   // 時間到時幾乎必說（一般怪是 SpeakChance）

    MonsterController _mc;
    List<MonsterSpeechLine> _lines;
    float _nextSpeakTime = float.MaxValue;   // 還沒發現玩家前不說話
    bool _armed;                             // 是否已排定第一句的時間

    readonly List<MonsterSpeechLine> _unlockedScratch = new List<MonsterSpeechLine>();
    bool[] _announced;   // 對應 _lines：這句門檻句是否已經「跌破時強制喊過」（只喊一次，之後回歸隨機池）

    /// <summary>由 MonsterController 掛上時呼叫，帶入自己（用來讀血量%、是否發現玩家、是否死亡）。</summary>
    public void Configure(MonsterController mc)
    {
        _mc = mc;
        _lines = (mc != null) ? mc.SpeechLines : null;
        _announced = new bool[(_lines != null) ? _lines.Count : 0];
    }

    void Update()
    {
        if (_mc == null || _mc.IsDead || _lines == null || _lines.Count == 0) return;

        // ① 血量門檻播報：帶「N%:」的句子，血量第一次跌破就**一定**喊。刻意擺在「發現玩家」那一關之前，
        //    也刻意不走間隔與機率——這是劇情節點（boss 放大絕時喊話），漏講就沒有第二次機會了。
        //    這一幀喊過就 return，不要再走下面的隨機那條，免得同一幀兩句互相蓋掉。
        if (TryAnnounceThreshold()) return;

        // 「發現玩家後才說」：還沒發現前不排時間；發現的那一刻排定第一句。
        if (!_mc.IsAwareOfPlayer)
        {
            _armed = false;
            return;
        }
        // boss 頻率加倍、幾乎必說（劇情要角）；一般怪用原本的間隔與機率。
        float intervalMul = _mc.IsBoss ? BossIntervalMul : 1f;
        float chance = _mc.IsBoss ? BossSpeakChance : SpeakChance;

        if (!_armed)
        {
            _armed = true;
            _nextSpeakTime = Time.time + Random.Range(FirstDelayMin, FirstDelayMax) * intervalMul;   // 起始時機隨機（去同步）；boss 更快開口
        }

        if (Time.time < _nextSpeakTime) return;

        // 時間到：有機率選擇「這次不說」，讓場上多隻怪不會一致發話、整體也更稀疏。
        bool spoke = Random.value < chance;
        if (spoke) SpeakOnce();

        // 下一次可能開口 = 現在 +（有說才加顯示時間）+ 隨機間隔。
        float gap = SpeakIntervalSeconds * intervalMul * Random.Range(1f - IntervalJitter, 1f + IntervalJitter);
        _nextSpeakTime = Time.time + (spoke ? BubbleDuration : 0f) + gap;
    }

    /// <summary>
    /// 血量門檻播報：找出「還沒喊過 ＋ 血量已經跌破」的門檻句，立刻講出來。有講回 true。
    ///
    /// <para><b>一次跌破好幾個門檻時只講最低的那一句</b>（例如一發大傷害從滿血打到剩 5%，同時跨過 30% 與 10%）：
    /// 最低的門檻＝劇情上最後面那句，講它最合理；其餘同時跌破的一併記成已播報，否則下一幀會接著喊，變成連珠炮。</para>
    /// </summary>
    bool TryAnnounceThreshold()
    {
        if (_announced == null || _announced.Length != _lines.Count) _announced = new bool[_lines.Count];

        float hpPct = _mc.HealthFraction * 100f;
        int best = -1;
        for (int i = 0; i < _lines.Count; i++)
        {
            if (_announced[i]) continue;
            float th = _lines[i].UnlockAtPercent;
            if (th >= 100f) continue;                     // 無前綴＝一直可講，不是門檻句
            if (hpPct > th + 0.001f) continue;            // 還沒跌破
            _announced[i] = true;                         // 跌破的都記起來（含這次不講的），避免下一幀連珠炮
            if (string.IsNullOrEmpty(_lines[i].Text)) continue;
            if (best < 0 || th < _lines[best].UnlockAtPercent) best = i;   // 取門檻最低的那句
        }
        if (best < 0) return false;

        Dipan.UI.MonsterSpeechPanel.Speak(_mc, _lines[best].Text, BubbleDuration);

        // 把隨機那條的節奏往後推一個完整間隔：不然緊接著又冒一句，會把這句劇情台詞蓋掉。
        float intervalMul = _mc.IsBoss ? BossIntervalMul : 1f;
        _armed = true;
        _nextSpeakTime = Time.time + BubbleDuration + SpeakIntervalSeconds * intervalMul;
        return true;
    }

    void SpeakOnce()
    {
        // 收集「目前血量已解鎖」的句子：門檻% ≥ 當前血量%。
        float hpPct = _mc.HealthFraction * 100f;
        _unlockedScratch.Clear();
        for (int i = 0; i < _lines.Count; i++)
        {
            if (hpPct <= _lines[i].UnlockAtPercent + 0.001f)
                _unlockedScratch.Add(_lines[i]);
        }
        if (_unlockedScratch.Count == 0) return;   // 理論上至少有無前綴句；保險

        var pick = _unlockedScratch[Random.Range(0, _unlockedScratch.Count)];
        if (!string.IsNullOrEmpty(pick.Text))
            Dipan.UI.MonsterSpeechPanel.Speak(_mc, pick.Text, BubbleDuration);
    }
}
