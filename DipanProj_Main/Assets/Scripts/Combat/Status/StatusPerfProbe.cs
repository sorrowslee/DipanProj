using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// **狀態效果壓測用的效能顯示＋記錄**（第⑥階段，2026-11-18，只給作弊面板用）。
///
/// <para><b>顯示</b>：畫面右上角（左上角是既有的 P 鍵效能面板 PerfHud）——最近 1 秒的平均／最差幀時間、每幀 GC 配置、
/// 場上怪物數、身上有狀態的怪物數。每 0.5 秒才重算一次字串。</para>
///
/// <para><b>記錄</b>（作者：自己抄數字容易抄錯）：作弊面板按「開始記錄」→ 關掉面板玩一段 → 打開面板按「結束記錄」。
/// 只記<b>遊戲在跑的幀</b>（`timeScale` 為 0＝面板開著／暫停時不算），恢復後的頭 0.5 秒也不算（關面板那一下的抖動）。
/// 結束時把這一段的統計追加寫進一個 CSV（編輯器裡＝repo 的 <c>TempImage/perf_logs/perf_log.csv</c>，AI 直接讀得到；打包版＝persistentDataPath）。</para>
///
/// <para>⚠ 編輯器裡的數字會比打包後差（編輯器本身也在畫）。</para>
/// </summary>
public class StatusPerfProbe : MonoBehaviour
{
    static StatusPerfProbe _inst;
    public static bool Visible => _inst != null;
    public static bool Recording => _inst != null && _inst._rec;

    public static void Toggle()
    {
        if (_inst != null) { Destroy(_inst.gameObject); _inst = null; return; }
        Ensure();
    }

    static StatusPerfProbe Ensure()
    {
        if (_inst != null) return _inst;
        var go = new GameObject("StatusPerfProbe");
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<StatusPerfProbe>();
        return _inst;
    }

    // ───────────────────────── 即時顯示 ─────────────────────────

    readonly Queue<float> _frames = new Queue<float>();
    readonly Queue<long> _gc = new Queue<long>();
    float _sum, _refreshAt;
    long _gcSum;
    string _text = "";
    GUIStyle _style;
    int _monsters, _withStatus, _totalStatus, _controlled;

    // 每幀 GC 配置量（位元組）：Unity 內建計數器，正式版（非 Development）也拿得到。數字一直大於 0 ＝ 每幀都在 new 東西，久了會觸發 GC 卡頓。
    ProfilerRecorder _gcRec;
    // 主執行緒每幀時間（ms）：編輯器與 Development Build 才有；拿不到就記空白
    ProfilerRecorder _mainRec;

    void OnEnable()
    {
        _gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        _mainRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
    }

    void OnDisable()
    {
        if (_rec) EndRecord();   // 關掉顯示時還在記 → 先存檔，別讓資料不見
        _gcRec.Dispose();
        _mainRec.Dispose();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        long gc = _gcRec.Valid ? _gcRec.LastValue : 0;
        _frames.Enqueue(dt); _sum += dt;
        _gc.Enqueue(gc); _gcSum += gc;
        while (_sum > 1f && _frames.Count > 1) { _sum -= _frames.Dequeue(); _gcSum -= _gc.Dequeue(); }

        if (_rec) Sample(dt, gc);

        if (Time.unscaledTime < _refreshAt) return;
        _refreshAt = Time.unscaledTime + 0.5f;

        float worst = 0f;
        foreach (var f in _frames) if (f > worst) worst = f;
        float avg = _frames.Count > 0 ? _sum / _frames.Count : 0f;
        CountMonsters();

        float gcKbPerFrame = _gc.Count > 0 ? _gcSum / 1024f / _gc.Count : 0f;
        var sb = new StringBuilder();
        sb.Append($"平均 {avg * 1000f:0.0} ms（{(avg > 0f ? 1f / avg : 0f):0} FPS）　最差 {worst * 1000f:0.0} ms\n");
        sb.Append($"GC 配置 每幀 {gcKbPerFrame:0.0} KB（{(_gcRec.Valid ? "最近 1 秒平均" : "此版本拿不到")}）\n");
        sb.Append($"怪物 {_monsters}　有狀態 {_withStatus}（共 {_totalStatus} 種）　被控制 {_controlled}");
        if (_rec) sb.Append($"\n● 記錄中「{_recLabel}」：已記 {_recMs.Count} 幀（{_recSeconds:0.0} 秒）");
        _text = sb.ToString();
    }

    void CountMonsters()
    {
        _monsters = _withStatus = _totalStatus = _controlled = 0;
        var act = MonsterController.Active;
        for (int i = 0; i < act.Count; i++)
        {
            var m = act[i];
            if (m == null || m.IsDead) continue;
            _monsters++;
            var sc = m.Status;
            if (sc == null || sc.Count == 0) continue;
            _withStatus++;
            _totalStatus += sc.Count;
            if (sc.IsControlled) _controlled++;
        }
    }

    void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.UpperLeft };
            _style.normal.textColor = Color.white;
        }
        // 放右上角：左上角是既有的效能面板（P 鍵，PerfHud），兩個一起開不會疊在一起
        GUI.Box(new Rect(Screen.width - 692, 12, 680, _rec ? 128 : 100), _text, _style);
    }

    void OnDestroy() { if (_inst == this) _inst = null; }

    // ───────────────────────── 記錄 ─────────────────────────

    bool _rec;
    string _recLabel;
    DateTime _recStart;
    readonly List<float> _recMs = new List<float>(8192);
    readonly List<float> _recMainMs = new List<float>(8192);
    long _recGcTotal, _recGcMax;
    int _recGcFrames;            // 有配置（>0）的幀數
    double _recMonsterSum, _recStatusSum, _recControlledSum;
    int _recCountSamples;
    float _recSeconds, _resumeAt;
    bool _wasPaused = true;
    float _countAt;

    /// <summary>開始記一段。<paramref name="label"/> 留空＝用開始時的怪物數自動命名（例「怪物 100」）。</summary>
    public static void BeginRecord(string label = null)
    {
        var p = Ensure();
        if (p._rec) return;
        p.CountMonsters();
        p._rec = true;
        p._recLabel = string.IsNullOrEmpty(label) ? $"怪物 {p._monsters}" : label;
        p._recStart = DateTime.Now;
        p._recMs.Clear(); p._recMainMs.Clear();
        p._recGcTotal = p._recGcMax = 0; p._recGcFrames = 0;
        p._recMonsterSum = p._recStatusSum = p._recControlledSum = 0; p._recCountSamples = 0;
        p._recSeconds = 0f;
        p._wasPaused = true;      // 按的時候面板開著（暫停）；關掉之後才開始算
        p._countAt = 0f;
    }

    /// <summary>結束這一段並寫檔。回傳給玩家看的一行摘要（沒在記＝null）。</summary>
    public static string EndRecord()
    {
        if (_inst == null || !_inst._rec) return null;
        return _inst.FinishRecord();
    }

    void Sample(float dt, long gc)
    {
        // 暫停（面板開著）不算；恢復後頭 0.5 秒不算（關面板那一下的抖動）
        if (Time.timeScale <= 0f) { _wasPaused = true; return; }
        if (_wasPaused) { _wasPaused = false; _resumeAt = Time.unscaledTime + 0.5f; }
        if (Time.unscaledTime < _resumeAt) return;

        _recMs.Add(dt * 1000f);
        if (_mainRec.Valid && _mainRec.LastValue > 0) _recMainMs.Add(_mainRec.LastValue / 1_000_000f);   // 奈秒 → 毫秒
        _recGcTotal += gc;
        if (gc > _recGcMax) _recGcMax = gc;
        if (gc > 0) _recGcFrames++;
        _recSeconds += dt;

        // 怪物數每 0.25 秒取樣一次（不必每幀掃）
        if (Time.unscaledTime >= _countAt)
        {
            _countAt = Time.unscaledTime + 0.25f;
            CountMonsters();
            _recMonsterSum += _monsters; _recStatusSum += _withStatus; _recControlledSum += _controlled;
            _recCountSamples++;
        }
    }

    string FinishRecord()
    {
        _rec = false;
        int n = _recMs.Count;
        if (n == 0) return $"「{_recLabel}」沒有記到任何幀（面板要關掉、遊戲要在跑才會記）。";

        var sorted = new List<float>(_recMs); sorted.Sort();
        float avg = 0f; foreach (var v in _recMs) avg += v; avg /= n;
        float P(float q) => sorted[Mathf.Clamp(Mathf.CeilToInt(q * n) - 1, 0, n - 1)];
        int over33 = 0, over50 = 0;
        foreach (var v in _recMs) { if (v > 33.4f) over33++; if (v > 50f) over50++; }
        float mainAvg = 0f;
        if (_recMainMs.Count > 0) { foreach (var v in _recMainMs) mainAvg += v; mainAvg /= _recMainMs.Count; }
        float gcAvgKb = _recGcTotal / 1024f / n;
        float cnt = Mathf.Max(1, _recCountSamples);

        // CSV 一列（欄位見 Header）
        string row = string.Join(",", new[]
        {
            _recStart.ToString("yyyy-MM-dd HH:mm:ss"),
            Csv(_recLabel),
            _recSeconds.ToString("0.0"),
            n.ToString(),
            avg.ToString("0.00"),
            (avg > 0f ? 1000f / avg : 0f).ToString("0"),
            P(0.5f).ToString("0.00"),
            P(0.95f).ToString("0.00"),
            P(0.99f).ToString("0.00"),
            sorted[n - 1].ToString("0.00"),
            over33.ToString(),
            over50.ToString(),
            _recMainMs.Count > 0 ? mainAvg.ToString("0.00") : "",
            gcAvgKb.ToString("0.00"),
            (_recGcMax / 1024f).ToString("0.0"),
            (100f * _recGcFrames / n).ToString("0"),
            (_recMonsterSum / cnt).ToString("0"),
            (_recStatusSum / cnt).ToString("0"),
            (_recControlledSum / cnt).ToString("0"),
            Csv(SystemInfo.processorType),
            Application.isEditor ? "Editor" : (Debug.isDebugBuild ? "DevBuild" : "Release"),
            $"{Screen.width}x{Screen.height}",
            QualitySettings.vSyncCount.ToString(),
        });

        string path = LogPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool fresh = !File.Exists(path);
            using (var w = new StreamWriter(path, true, new UTF8Encoding(true)))
            {
                if (fresh) w.WriteLine(Header);
                w.WriteLine(row);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return $"「{_recLabel}」記好了但寫檔失敗：{ex.Message}";
        }
        Debug.Log($"[StatusPerfProbe] 記錄「{_recLabel}」→ {path}\n{Header}\n{row}");
        return $"「{_recLabel}」{_recSeconds:0} 秒：平均 {avg:0.0} ms、最差 {sorted[n - 1]:0.0} ms、GC {gcAvgKb:0.0} KB/幀（已寫入 perf_log.csv）";
    }

    const string Header =
        "時間,標籤,秒數,幀數,平均ms,平均FPS,中位ms,P95ms,P99ms,最差ms,超過33ms幀數,超過50ms幀數,主執行緒平均ms," +
        "GC平均KB每幀,GC單幀最大KB,有GC配置的幀百分比,平均怪物數,平均有狀態怪數,平均被控制數,CPU,執行環境,解析度,VSync";

    static string Csv(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", "，").Replace("\n", " ");

    /// <summary>編輯器：repo 的 TempImage/perf_logs/perf_log.csv（Assets 的上兩層）；打包版：persistentDataPath/perf_logs/。</summary>
    public static string LogPath()
    {
        if (Application.isEditor)
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TempImage", "perf_logs", "perf_log.csv"));
        return Path.Combine(Application.persistentDataPath, "perf_logs", "perf_log.csv");
    }
}
