using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;

/// <summary>
/// 清場血球（2026-11-06，作者需求）：**每條「戰鬥鏈」的最後一組怪打完時，當下掉 1~2 顆血球**，當作前往下一處前的回血。
/// 與「每隻怪 10% 隨機掉」並存（那條照舊，見 <see cref="BloodOrb"/>）。
///
/// ⭐ **直接讀地圖編輯器的資料判斷，不用逐張設定、也不用等待**（作者 2026-11-06 拍板的規則）：
///   ‧ 怪物出生點靠「接續觸發」（next／nextA／nextB／nextTimeout／等待節點的 targets，中間可以經過對話、選擇、等待等任何節點）
///     或「完成寫旗標／死亡寫旗標 → watchFlag／以該旗標為條件的出生點」串起來的，算**同一條戰鬥鏈**。
///   ‧ **一路追到鏈上最後一個出生點**：還接著別的出生點的那一組打完不掉（＝中間的波次），
///     最後一組（後面沒有再接出生點）的怪全部死光那一刻才掉。
///   ‧ 分支（選擇、二選一）只會走其中一邊：**這一趟從沒開打過**的出生點一律不算，走過的那一邊打完就掉。
///   ‧ 同一個 `waveGroup` 的出生點、或被同一個東西叫出來的，算同一組（同時開打的同一場）。
///   ‧ 沒接在別的出生點後面的，各自是一條鏈（例：走到某處才叫出的伏兵＝第二條鏈，打完也會再掉一次）。
///     進圖就生的出生點（含 onEnter 叫出來的）合起來算一條「進圖戰鬥」。
///
/// ‧ 資格同掉寶：本圖這一趟至少殺過一隻 <c>DropTableId &gt; 0</c> 的怪（夢境教學那類不掉）。Boss 有填就照掉。
/// ‧ 每條鏈一趟只給一次（RunProgress 記著；怪清了不會復生，換圖回來也不會再觸發）。
/// ‧ 不會結束的鏈：無限波（重複產生、`maxWaves` 留空）——`validate_map.py` 會警告。
///
/// 文件：readme/RUN_PROGRESS.md §9.1。
/// </summary>
public static class MapClearReward
{
    /// <summary>MonsterController 死亡時呼叫（只在關卡 run 內、且這隻是會掉寶的敵怪 DropsLoot）。在怪物 Destroy 之前呼叫。</summary>
    public static void OnEnemyKilled(MonsterController mc, Vector2 groundPos)
    {
        if (mc == null || !RunProgress.Exists || !RunProgress.Instance.RunActive) return;
        if (!InteractionManager.Exists || MapManager.Instance == null) return;

        int mapId = MapManager.Instance.CurrentMapId;
        var rp = RunProgress.Instance;
        if (mc.DropTableId > 0) rp.MarkLootKill(mapId);
        if (!rp.HadLootKill(mapId)) return;

        var resp = Object.FindObjectOfType<MapMonsterRespawner>();
        string regionId = RegionIdFromKey(mc.SpawnKey);
        if (regionId == null && resp != null) regionId = resp.RegionIdOf(mc.gameObject);
        if (regionId == null) return;                         // 不是地圖出生點生的

        var g = Graph.For(TriggerChain.CurrentRegions);
        if (g == null || !g.groupOf.TryGetValue(regionId, out string key)) return;
        if (rp.IsBattleGiven(mapId, key)) return;

        // 只看「這一趟開打過」的出生點（分支沒走到的那邊不算）：
        //   ‧ 開打過、而且後面還接著別組出生點 → 不是最後一組（下一組打完才掉）；
        //   ‧ 開打過、還沒清光 → 戰鬥還沒結束。
        var members = g.members[key];
        bool anyStarted = false;
        for (int i = 0; i < members.Count; i++)
        {
            SpawnState(members[i], resp, out bool started, out bool cleared);
            if (!started) continue;
            anyStarted = true;
            if (g.downstream[members[i]].Count > 0) return;
            if (!cleared) return;
        }
        if (!anyStarted) return;

        rp.MarkBattleGiven(mapId, key);
        int n = Random.Range(BloodOrb.ClearDropMin, BloodOrb.ClearDropMax + 1);
        for (int i = 0; i < n; i++)
            InteractionManager.Instance.DropBloodOrb(groundPos);   // 落點（離死亡處一段距離、可走格、彼此錯開）由 DropBloodOrb 挑
        Debug.Log($"[MapClearReward] 地圖 #{mapId} 戰鬥鏈「{key}」結束 → 掉 {n} 顆血球。");
    }

    // SpawnKey 格式＝"<區域id>#<x>,<y>"（MapLoader／MapMonsterRespawner 的一次性出生點）；重複產生的是 null。
    static string RegionIdFromKey(string spawnKey)
    {
        if (string.IsNullOrEmpty(spawnKey)) return null;
        int i = spawnKey.IndexOf('#');
        return i > 0 ? spawnKey.Substring(0, i) : null;
    }

    static void SpawnState(string regionId, MapMonsterRespawner resp, out bool started, out bool cleared)
    {
        if (resp != null && resp.TryGetSpawnState(regionId, out started, out cleared)) return;
        // 進圖就當場生完的一般出生點：一定開打過；場上沒有它生的活怪＝清光
        started = true;
        cleared = true;
        string prefix = regionId + "#";
        var list = MonsterController.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m != null && !m.IsDead && m.SpawnKey != null && m.SpawnKey.StartsWith(prefix)) { cleared = false; return; }
        }
    }

    // ───────────────────────── 戰鬥鏈分析（只讀地圖資料，每張圖算一次）─────────────────────────

    sealed class Graph
    {
        public readonly Dictionary<string, List<string>> downstream = new Dictionary<string, List<string>>(); // 出生點 → 鏈上接到的「別組」出生點（同組的不算）
        public readonly Dictionary<string, string> groupOf = new Dictionary<string, string>();               // 出生點 → 所屬戰鬥鏈這一組的 key
        public readonly Dictionary<string, List<string>> members = new Dictionary<string, List<string>>();    // key → 同一組的出生點

        static IReadOnlyList<TriggerRegion> _cachedFor;
        static Graph _cached;

        // targets＝「等待(鏈動作)」的同時啟動（2026-11-07，逗號分隔多筆）；其餘都是單筆，Split 後也只有一個元素。
        static readonly string[] NextKeys = { "next", "nextA", "nextB", "nextTimeout", "targets" };
        static readonly string[] FlagOutKeys = { "setFlag", "deathFlag" };
        static readonly string[] ConditionKeys =
            { "requireFlag", "requireCycleMin", "requireCycleMax", "requireItem", "requireClearsMin", "requireClearsMax", "conditions" };

        public static Graph For(IReadOnlyList<TriggerRegion> regions)
        {
            if (regions == null) return null;
            if (!ReferenceEquals(regions, _cachedFor)) { _cached = Build(regions); _cachedFor = regions; }
            return _cached;
        }

        static Graph Build(IReadOnlyList<TriggerRegion> regions)
        {
            var g = new Graph();
            var byName = new Dictionary<string, TriggerRegion>();
            var byId = new Dictionary<string, TriggerRegion>();
            foreach (var r in regions)
            {
                if (r == null) continue;
                if (!string.IsNullOrEmpty(r.name) && !byName.ContainsKey(r.name)) byName[r.name] = r;   // 同名取第一個（同 TriggerChain.Find）
                if (!string.IsNullOrEmpty(r.id)) byId[r.id] = r;
            }
            TriggerRegion Find(string nameOrId)
            {
                if (string.IsNullOrEmpty(nameOrId)) return null;
                string t = nameOrId.Trim();
                return byName.TryGetValue(t, out var hit) ? hit : (byId.TryGetValue(t, out var hit2) ? hit2 : null);
            }
            bool IsSpawn(TriggerRegion tr) => tr != null && tr.typeId == TriggerChain.TypeMonsterSpawn;

            // 有向邊：接續觸發＋旗標（寫旗標 → fireOnFlag 對上的／requireFlag 對上的）
            var succ = new Dictionary<string, List<string>>();
            var pred = new Dictionary<string, List<string>>();
            foreach (var r in byId.Values) { succ[r.id] = new List<string>(); pred[r.id] = new List<string>(); }
            void Edge(TriggerRegion from, TriggerRegion to)
            {
                if (from == null || to == null || from == to || succ[from.id].Contains(to.id)) return;
                succ[from.id].Add(to.id); pred[to.id].Add(from.id);
            }
            foreach (var r in byId.Values)
            {
                foreach (var k in NextKeys)
                    foreach (var part in r.GetString(k).Split(','))
                        Edge(r, Find(part));
                foreach (var k in FlagOutKeys)
                {
                    string flag = r.GetString(k).Trim();
                    if (flag.Length == 0) continue;
                    foreach (var o in byId.Values)
                        if (o.GetString("fireOnFlag").Trim() == flag || o.GetString("requireFlag").Trim() == flag) Edge(r, o);
                }
            }

            var spawns = new List<TriggerRegion>();
            foreach (var r in byId.Values) if (IsSpawn(r)) spawns.Add(r);

            // 下游：從出生點往後走，經過非出生點節點，碰到出生點就收下、不再往後
            foreach (var s in spawns)
            {
                var found = new List<string>();
                var seen = new HashSet<string> { s.id };
                var q = new Queue<string>(succ[s.id]);
                while (q.Count > 0)
                {
                    string x = q.Dequeue();
                    if (!seen.Add(x)) continue;
                    if (IsSpawn(byId[x])) { if (!found.Contains(x)) found.Add(x); continue; }
                    foreach (var y in succ[x]) q.Enqueue(y);
                }
                g.downstream[s.id] = found;
            }

            // 分組：往上找「是誰把我叫出來的」，算出每顆的初始 key
            var initKey = new Dictionary<string, string>();
            foreach (var s in spawns)
            {
                var spawnAnc = new List<string>();
                var roots = new List<string>();
                var seen = new HashSet<string> { s.id };
                var q = new Queue<string>(pred[s.id]);
                while (q.Count > 0)
                {
                    string x = q.Dequeue();
                    if (!seen.Add(x)) continue;
                    if (IsSpawn(byId[x])) { spawnAnc.Add(x); continue; }
                    if (pred[x].Count == 0) roots.Add(x);
                    foreach (var y in pred[x]) q.Enqueue(y);
                }
                string key;
                if (spawnAnc.Count > 0) { spawnAnc.Sort(System.StringComparer.Ordinal); key = "after:" + spawnAnc[0]; }
                else if (roots.Count > 0)
                {
                    bool atLoad = false;
                    foreach (var rid in roots) if (byId[rid].typeId == TriggerChain.TypeOnEnter) { atLoad = true; break; }
                    roots.Sort(System.StringComparer.Ordinal);
                    key = atLoad ? "load" : "src:" + roots[0];
                }
                else if (pred[s.id].Count == 0 && !HasCondition(s)) key = "load";   // 進圖就生
                else key = "cond:" + s.GetString("requireFlag").Trim() + "|" + s.GetString("conditions").Trim();   // 條件由外部決定：同條件的算同一場
                initKey[s.id] = key;
            }

            // 併組（union-find）：同初始 key、或同 waveGroup（同時開打的同一場）
            var parent = new Dictionary<string, string>();
            foreach (var s in spawns) parent[s.id] = s.id;
            string Root(string x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            void Union(string x, string y) { x = Root(x); y = Root(y); if (x != y) parent[y] = x; }
            var firstByKey = new Dictionary<string, string>();
            var firstByWave = new Dictionary<string, string>();
            foreach (var s in spawns)
            {
                if (firstByKey.TryGetValue(initKey[s.id], out var k0)) Union(k0, s.id); else firstByKey[initKey[s.id]] = s.id;
                string wg = s.GetString("waveGroup").Trim();
                if (wg.Length > 0) { if (firstByWave.TryGetValue(wg, out var w0)) Union(w0, s.id); else firstByWave[wg] = s.id; }
            }
            // 組 key＝組內最小的初始 key（穩定、好讀）
            var keyOfRoot = new Dictionary<string, string>();
            foreach (var s in spawns)
            {
                string r0 = Root(s.id), k = initKey[s.id];
                if (!keyOfRoot.TryGetValue(r0, out var cur) || string.CompareOrdinal(k, cur) < 0) keyOfRoot[r0] = k;
            }
            foreach (var s in spawns)
            {
                string key = keyOfRoot[Root(s.id)];
                g.groupOf[s.id] = key;
                if (!g.members.TryGetValue(key, out var lst)) g.members[key] = lst = new List<string>();
                lst.Add(s.id);
            }
            // 下游只留「別組」的（同組＝同一場，例：雙首領的第二隻是第一隻「生出來時」接出來的）
            foreach (var s in spawns)
                g.downstream[s.id].RemoveAll(d => g.groupOf.TryGetValue(d, out var dk) && dk == g.groupOf[s.id]);
            return g;
        }

        static bool HasCondition(TriggerRegion r)
        {
            if (r.GetBool("startDisabled")) return true;
            foreach (var k in ConditionKeys) if (r.GetString(k).Trim().Length > 0) return true;
            return false;
        }
    }
}
