using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **狀態效果壓測用的效能顯示**（第⑥階段，2026-11-18，只給作弊面板用）：畫面左上角顯示
/// 最近 1 秒的平均／最差幀時間、場上怪物數、身上有狀態的怪物數。作者在 Unity 裡量「50～100 隻怪都上狀態」時看這個。
///
/// <para>用 OnGUI 畫（開發工具，不進正式 UI）；每 0.5 秒才重算一次字串，平常幾乎沒有成本。關掉＝整個物件銷毀。</para>
/// <para>⚠ 編輯器裡的數字會比打包後差（編輯器本身也在畫）。要準確請看 Profiler，或打 Development Build 量。</para>
/// </summary>
public class StatusPerfProbe : MonoBehaviour
{
    static StatusPerfProbe _inst;
    public static bool Visible => _inst != null;

    public static void Toggle()
    {
        if (_inst != null) { Destroy(_inst.gameObject); _inst = null; return; }
        var go = new GameObject("StatusPerfProbe");
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<StatusPerfProbe>();
    }

    readonly Queue<float> _frames = new Queue<float>();
    float _sum, _refreshAt;
    string _text = "";
    GUIStyle _style;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _frames.Enqueue(dt); _sum += dt;
        while (_sum > 1f && _frames.Count > 1) _sum -= _frames.Dequeue();
        if (Time.unscaledTime < _refreshAt) return;
        _refreshAt = Time.unscaledTime + 0.5f;

        float worst = 0f;
        foreach (var f in _frames) if (f > worst) worst = f;
        float avg = _frames.Count > 0 ? _sum / _frames.Count : 0f;

        int monsters = 0, withStatus = 0, totalStatus = 0, controlled = 0;
        var act = MonsterController.Active;
        for (int i = 0; i < act.Count; i++)
        {
            var m = act[i];
            if (m == null || m.IsDead) continue;
            monsters++;
            var sc = m.Status;
            if (sc == null || sc.Count == 0) continue;
            withStatus++;
            totalStatus += sc.Count;
            if (sc.IsControlled) controlled++;
        }
        _text = $"平均 {avg * 1000f:0.0} ms（{(avg > 0f ? 1f / avg : 0f):0} FPS）　最差 {worst * 1000f:0.0} ms\n" +
                $"怪物 {monsters}　有狀態 {withStatus}（共 {totalStatus} 種）　被控制 {controlled}";
    }

    void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.UpperLeft };
            _style.normal.textColor = Color.white;
        }
        GUI.Box(new Rect(12, 12, 620, 70), _text, _style);
    }

    void OnDestroy() { if (_inst == this) _inst = null; }
}
