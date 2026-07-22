using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全螢幕過場特效（睜眼／破幻術／馬賽克…）的「唯一登記表」。
///
/// 這是把原本散在兩處的螢幕特效 id 統一起來的單一來源：
///   - MapsTable.csv 的 <c>EnterEffect</c> 欄（進圖播一次）
///   - 劇情編輯器的 <c>screenFx</c> 步驟 / 觸發鏈的 <c>playScreenFx</c> 動作
/// 兩邊填「同一個 Id」就會打到「同一個效果」（都經 <see cref="ScreenFxPlayer.Play"/> 分派）。
///
/// 資料來源：<c>Resources/ScreenFxTable.csv</c>（首次存取時 lazy load）。
/// 欄位：Id, Name, Key, DurationSeconds, WakeUpPose, Notes
///   - Id           = 共用整數 id（EnterEffect 與 screenFx 都填它）。0 = 無特效。
///   - Name         = 顯示名稱（編輯器下拉用）。
///   - Key          = 穩定字串鍵（對應哪個控制器，純備註；實際分派在 ScreenFxPlayer.Play 的 switch）。
///   - DurationSeconds = 預設總長（秒）；呼叫端沒指定(-1)時用這個。&lt;=0 = 用控制器自己的預設。
///   - WakeUpPose   = 1＝這個效果當 EnterEffect 時連動玩家「趴地→起身」（目前只有睜眼）。
///   - Notes        = 說明。
///
/// ★ 新增一種螢幕特效的維護點：這張表加一列 + 寫 shader/控制器 + ScreenFxPlayer.Play 加 case
///   （＋編輯器 EditorUI 的 ScreenFxCatalog 同步一列，供下拉顯示）。
/// </summary>
public static class ScreenFxTable
{
    public class Row
    {
        public int id;
        public string name = "";
        public string key = "";
        public float duration = -1f;   // <=0 = 用控制器預設
        public bool wakeUpPose = false;
        public string notes = "";
    }

    static Dictionary<int, Row> _rows;

    static void EnsureLoaded()
    {
        if (_rows != null) return;
        _rows = new Dictionary<int, Row>();

        var ta = Resources.Load<TextAsset>("ScreenFxTable");
        if (ta == null || string.IsNullOrEmpty(ta.text))
        {
            Debug.LogWarning("[ScreenFxTable] 找不到或讀不到 Resources/ScreenFxTable.csv；螢幕特效以控制器預設運作。");
            return;
        }

        var lines = ta.text.Replace("\r", "").Split('\n');
        for (int i = 1; i < lines.Length; i++)   // 第 0 列是標頭
        {
            var ln = lines[i].Trim();
            if (ln.Length == 0) continue;
            var v = ln.Split(',');
            if (v.Length < 1 || !int.TryParse(v[0].Trim(), out int id)) continue;

            var row = new Row { id = id };
            if (v.Length > 1) row.name = v[1].Trim();
            if (v.Length > 2) row.key = v[2].Trim();
            if (v.Length > 3 && float.TryParse(v[3].Trim(), out float d)) row.duration = d;
            if (v.Length > 4) row.wakeUpPose = v[4].Trim() == "1";
            if (v.Length > 5) row.notes = v[5].Trim();
            _rows[id] = row;
        }
        Debug.Log($"[ScreenFxTable] 載入 {_rows.Count} 種全螢幕過場特效。");
    }

    /// <summary>查一列；沒有回 null。</summary>
    public static Row Get(int id) { EnsureLoaded(); return (_rows != null && _rows.TryGetValue(id, out var r)) ? r : null; }

    /// <summary>這個 id 當 EnterEffect 時要不要連動玩家「趴地→起身」（目前只有睜眼）。</summary>
    public static bool WakeUpPose(int id) { var r = Get(id); return r != null && r.wakeUpPose; }

    /// <summary>預設總長（秒）；&lt;=0 = 用控制器自己的預設。</summary>
    public static float Duration(int id) { var r = Get(id); return r != null ? r.duration : -1f; }

    /// <summary>顯示名稱。</summary>
    public static string Name(int id) { var r = Get(id); return r != null ? r.name : ""; }
}
