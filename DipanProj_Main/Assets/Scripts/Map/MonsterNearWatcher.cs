using System;
using UnityEngine;

/// <summary>
/// **怪物逼近**（鏈動作 <c>monsterNear</c> 的執行體，2026-09-23 為夢境佛掌收尾而做）：
/// 被鏈啟動後每幀盯著場上指定種類的怪，**牠的身體邊緣離玩家身體邊緣 ≤ 指定距離**的那一刻觸發一次（接 next），然後自己收掉。
///
/// <para>量的是「兩個碰撞框之間的空隙」（<see cref="Physics2D.Distance"/>），不是中心點距離——
/// 佛掌 Scale 6、身體好幾個單位寬，拿中心點算的話「1.5 單位」會在牠早就壓到玩家身上之後才成立。</para>
///
/// <para><b>停住怪物</b>（預設開）：觸發那一刻把那隻怪設成 <see cref="MonsterController.Caged"/>（只鎖移動）。
/// 理由：接下來通常是對話（會暫停）→ 換圖（讀取頁**不暫停**、有停留秒數），這段期間怪若照走，
/// 會在讀取頁後面真的撞上玩家、扣血甚至打死他——畫面上看不到，但存檔裡的血量是真的少了。</para>
///
/// <para>掛在 MapRoot 底下：換圖時隨地圖一起銷毀，不會跨圖殘留。</para>
/// </summary>
public class MonsterNearWatcher : MonoBehaviour
{
    const float PollInterval = 0.05f;   // 不必每幀掃全場的怪

    int _monsterId;        // 0＝任何敵對怪
    float _distance;
    bool _freeze;
    Action _onNear;
    string _who;
    float _nextPoll;
    Collider2D _playerCol;
    Transform _player;

    /// <summary>開始盯。找不到地圖（MapRoot）時回 false，呼叫端自己決定要不要直接接 next。</summary>
    public static bool Watch(int monsterId, float distance, bool freeze, string who, Action onNear)
    {
        var loader = UnityEngine.Object.FindObjectOfType<MapLoader>();
        var root = loader != null ? loader.MapRoot : null;
        if (root == null) return false;

        var go = new GameObject($"[MonsterNearWatcher] {who}");
        go.transform.SetParent(root, false);
        var w = go.AddComponent<MonsterNearWatcher>();
        w._monsterId = monsterId;
        w._distance = Mathf.Max(0f, distance);
        w._freeze = freeze;
        w._who = who;
        w._onNear = onNear;
        return true;
    }

    void Update()
    {
        if (Time.time < _nextPoll) return;
        _nextPoll = Time.time + PollInterval;

        if (_player == null)
        {
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo == null) return;
            _player = pgo.transform;
            _playerCol = pgo.GetComponent<Collider2D>();
        }

        foreach (var mc in FindObjectsOfType<MonsterController>())
        {
            if (mc == null || mc.IsDead) continue;
            if (mc.Faction == MonsterFaction.PlayerAlly) continue;
            if (_monsterId > 0 && mc.DataId != _monsterId) continue;
            if (Gap(mc) > _distance) continue;

            if (_freeze) mc.Caged = true;   // 只鎖移動（同骨牢）；理由見檔頭
            Debug.Log($"[MonsterNearWatcher] 「{_who}」：{mc.name} 逼近到 {Gap(mc):0.00}（門檻 {_distance:0.##}）→ 觸發" +
                      (_freeze ? "，並停住牠。" : "。"));
            var cb = _onNear;
            _onNear = null;
            Destroy(gameObject);
            cb?.Invoke();
            return;
        }
    }

    /// <summary>怪的身體與玩家身體之間的空隙（重疊時為 0）。任一邊沒有碰撞框就退回中心距離。</summary>
    float Gap(MonsterController mc)
    {
        var mcol = mc.GetComponent<Collider2D>();
        if (mcol != null && _playerCol != null)
        {
            var d = Physics2D.Distance(mcol, _playerCol);
            if (d.isValid) return Mathf.Max(0f, d.distance);
        }
        return Vector2.Distance(mc.transform.position, _player.position);
    }
}
