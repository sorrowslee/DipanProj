using System.Collections.Generic;
using UnityEngine;
using Dipan.MapRuntime;   // TriggerRegion

/// <summary>
/// 怪物出生點的執行期驅動（每張地圖最多一個，掛在 MapRoot 下，換圖時隨 MapRoot 一起銷毀）。
///
/// 由 <see cref="MapLoader"/> 在建圖時登記，負責兩種「不能在載圖當下直接生完」的出生點：
///   ① **重複產生**（填了「重複間隔秒」）：每隔 N 秒生一波，一波＝該區域每格各一隻。
///   ② **有條件**（填了觸發鏈的通用條件欄位：條件旗標／初始停用／周目／道具／完成關卡數）：條件不成立就不生。
/// 兩者可以並用（等開關按下 → 才開始每 N 秒湧怪）。沒填這些的出生點仍由 MapLoader 當場生完，不經過這裡。
///
/// 規則（詳見 readme/TRIGGER_CHAIN.md §3.5）：
///   ‧ 條件是**持續判定**不是一次性事件：`TriggerChain.IsActive` 每幀重算，條件旗標被取消（例如開關再按一次）
///     就暫停、重新成立就繼續——所以「按 F 開始／再按一次暫停」不用另外寫狀態，交給旗標即可。
///     （「初始停用」則是一次性解鎖語意：要靠鏈 Activate，解鎖後就不會再關。）
///   ‧ 同時存在上限：這個區域生的怪還活著幾隻 ≥ 上限時，那一波就少生／不生（死了才補）。
///   ‧ 重複產生**不記** RunProgress『已清』（記了第一波死光就永遠不再生），但**照常掉寶**；
///     一次性（含等旗標的）照舊記進度＋掉寶，換圖回來不會重生。
///   ‧ 用 Time.deltaTime（有縮放）計時：開背包／對話暫停時不會偷偷累積。
/// </summary>
public class MapMonsterRespawner : MonoBehaviour
{
    /// <summary>「同時存在上限」留空時的保險值（避免作者忘了填就無限堆積）。</summary>
    public const int DefaultMaxAlive = 10;

    class Wave
    {
        public int[] monsterIds;          // 可填多個 → 每隻各自隨機挑一個
        public List<Vector2> points;      // 該區域每一格的中心（一波每格各生一隻）
        public List<string> spawnKeys;    // 與 points 同索引；null＝重複產生（不記進度）
        public string deathFlag;
        public TriggerRegion region;      // null＝沒有任何條件、永遠可以生；有值＝每幀查 TriggerChain.IsActive
        public float interval;            // <=0 ＝一次性（旗標成立時生一波就結束）
        public int maxAlive;
        public int mapId;                 // 記 RunProgress 用（一次性才需要）
        public float timer;
        public bool startedOnce;          // 已經生過第一波（重複模式用來判斷「剛開啟」）
        public bool doneOneShot;          // 一次性：已經生過了
        public string regionName;
        public readonly List<GameObject> alive = new List<GameObject>();

        // ── 波次（2026-09-22）──
        public TriggerRegion chainRegion;  // 推鏈用（全滅時走 TriggerChain.OnCompleted）。與 region 不同：
                                           // region 是「有沒有條件要每幀查」，這個是「這顆 trigger 本身」，一定有值。
        public int maxWaves;               // 0／留空＝無限波（＝維持舊行為，永遠不會全滅收尾）
        public int wavesFired;             // 已經生出去的波數。⚠ **一隻都沒生出來的那一波不計數**
                                           //   （作者拍板：20 波＝實實在在 20 批怪；被「同時存在上限」擋掉時不該空轉）
        public string waveGroup;           // 波次群組名（留空＝自己一組）。同組的出生點要全部打完才算全滅
        public bool cleared;               // 已經收過尾（只推一次鏈）

        // ── 接續時機（2026-09-23）──
        public bool nextOnSpawn;           // true＝「生出來時」就推鏈（第一次真的生出至少一隻的那一刻），全滅時不再推
        public bool spawnNotified;         // 已經推過（只推一次）
    }

    readonly List<Wave> _waves = new List<Wave>();
    MonsterSpawner _spawner;

    /// <summary>
    /// 登記一個出生點。若沒有條件擋著，會**立刻生第一波**（進圖就有怪，與一次性模式的手感一致）。
    /// <paramref name="region"/> 傳 null＝這顆沒填任何條件（就別去查 TriggerChain，此時它可能還沒 Setup 到本圖）。
    /// 回傳這一波實際生了幾隻。
    /// </summary>
    public int Register(MonsterSpawner spawner, int[] monsterIds, List<Vector2> points, List<string> spawnKeys,
                        string deathFlag, TriggerRegion region, float interval, int maxAlive, int mapId, string regionName,
                        TriggerRegion chainRegion = null, int maxWaves = 0, string waveGroup = null,
                        bool nextOnSpawn = false)
    {
        if (spawner == null || monsterIds == null || monsterIds.Length == 0) return 0;
        if (points == null || points.Count == 0) return 0;

        _spawner = spawner;
        var w = new Wave
        {
            monsterIds = monsterIds,
            points = points,
            spawnKeys = spawnKeys,
            deathFlag = deathFlag,
            region = region,
            interval = interval > 0f ? Mathf.Max(0.1f, interval) : 0f,   // 防呆：間隔太小會每幀狂生
            // 同時存在上限只有「重複產生」在看；一次性出生點維持原本「塗幾格生幾隻」、不設上限
            // （不然填了條件的一次性出生點會突然被砍成 10 隻，而且剩下的格子永遠不會補）。
            maxAlive = maxAlive > 0 ? maxAlive : (interval > 0f ? DefaultMaxAlive : int.MaxValue),
            mapId = mapId,
            regionName = regionName,
            chainRegion = chainRegion,
            maxWaves = Mathf.Max(0, maxWaves),
            waveGroup = string.IsNullOrWhiteSpace(waveGroup) ? null : waveGroup.Trim(),
            nextOnSpawn = nextOnSpawn,
        };
        _waves.Add(w);

        string mode = w.interval > 0f ? $"每 {w.interval} 秒一波" : "一次性";
        string gate = w.region == null ? "進圖就開始" : "等觸發鏈條件成立才開始（條件旗標/初始停用…）";
        string cap = w.interval > 0f ? $"同時存在上限 {w.maxAlive}"
                   : (maxAlive > 0 ? $"一次性不設上限（填的 {maxAlive} 已忽略）" : "一次性不設上限");
        string waves = w.maxWaves > 0 ? $"、共 {w.maxWaves} 波" : "";
        string grp = w.waveGroup != null ? $"、群組「{w.waveGroup}」" : "";
        Debug.Log($"[MapMonsterRespawner] 出生點「{regionName}」：{mode}、{gate}、每波最多 {points.Count} 隻、" +
                  $"{cap}{waves}{grp}、怪物 id [{string.Join("|", monsterIds)}]。");

        // ⚠ 有條件的出生點**刻意不在這裡判定**：本方法是由 MapLoader.SpawnMonstersFromMap 呼叫的，
        // 而 MapManager.PlaceAndSetup 的順序是 SpawnMonsters() → SetupWatcher()（後者才 TriggerChain.Setup）。
        // 此刻 TriggerChain 的停用集合還是**上一張地圖**的內容，這時查 IsActive 會得到錯的答案：
        // 只勾「初始停用」的出生點會被判成可以生 → 當場生一波，之後 Setup 才把它停用（＝初始停用被靜默忽略一次）。
        // 交給 Update 就沒事——respawner 是在載圖協程裡 AddComponent 的，第一次 Update 必定晚於 Setup。
        return w.region == null ? Fire(w) : 0;
    }

    void Update()
    {
        if (_spawner == null) return;

        // ⚠ 換圖期間一定要停手。本元件掛在 MapRoot 下、換圖時會隨之銷毀，但**跨 module 的換圖不是同一幀**：
        // MapManager 先開讀取頁（不暫停、timeScale 仍是 1）→ 停留數秒 → ClearTransientGameplay() 清光場上的怪
        // → 分幀預載素材（可達數秒）→ 最後才 LoadMapRoutine → Teardown() 銷毀舊 MapRoot。
        // 中間這段舊 respawner 還活著，照生的怪是 Instantiate 到場景根（不在 MapRoot 下），
        // 會躲過那次 ClearTransientGameplay 一路跟到新地圖去（座標還是舊圖的，可能卡在牆裡）。
        if (MapManager.Instance != null && MapManager.Instance.IsLoading) return;

        // 過關倒數／死亡等待這兩段刻意不暫停（玩家還能撿戰利品），但「都死了還在冒怪」很怪，這裡一起停。
        if (Dipan.Flow.GameFlowManager.Instance != null && Dipan.Flow.GameFlowManager.Instance.IsEndingLevel) return;

        for (int i = 0; i < _waves.Count; i++)
        {
            var w = _waves[i];

            // 先把已死（被 Destroy）的參照清掉——「還活著幾隻」是上限與全滅判定共用的同一份資料。
            w.alive.RemoveAll(go => go == null);

            // 波次打完了 → 不再生，改為等場上清空（見 TryFinishGroup）
            if (WavesDone(w)) { TryFinishGroup(w); continue; }

            // 條件是「持續判定」：不成立就停在原地（計時器不累積），成立了就接著跑。
            if (!IsOn(w)) continue;

            if (w.interval <= 0f)          // 一次性（等旗標型）：成立的那一刻生一波，之後不再管
            {
                if (!w.doneOneShot) Fire(w);
                continue;
            }

            // 第一次啟動：立刻先來一波，不用等滿 N 秒（暫停後恢復不算，會接續原本的倒數）。
            if (!w.startedOnce) { Fire(w); continue; }

            w.timer += Time.deltaTime;
            if (w.timer < w.interval) continue;
            w.timer = 0f;
            Fire(w);
        }
    }

    /// <summary>
    /// 這個出生點的波次是不是已經出完了。
    /// <para>一次性出生點：生過那一波就算出完（所以「把這房間的怪殺光就開門」也走同一條收尾路徑）。</para>
    /// <para>重複產生：`maxWaves` 留空／0 ＝ **無限波**，永遠不會出完 ⇒ 也永遠不會收尾（＝2026-09-22 之前的行為）。</para>
    /// </summary>
    static bool WavesDone(Wave w)
        => w.interval <= 0f ? w.doneOneShot
                            : (w.maxWaves > 0 && w.wavesFired >= w.maxWaves);

    /// <summary>
    /// 全滅收尾：**同群組**的出生點全部「波次出完 ＋ 場上一隻不剩」時，推一次鏈
    /// （<see cref="TriggerChain.OnCompleted"/> ＝ 寫「完成寫旗標」＋ 跑「接續觸發」）。
    ///
    /// <para>⭐ 沒有新的觸發欄位：monsterSpawn 本來就有通用的 `接續觸發`／`完成寫旗標`，
    /// 這裡只是替它定義了「什麼叫完成」——**這一組怪被清空的那一刻**。</para>
    ///
    /// <para>群組：`waveGroup` 留空 ⇒ 自己一組（只看自己）。填同一個名字的多顆出生點 ⇒ 要**全部**打完才算。
    /// 推鏈的是群組裡**有填 `接續觸發`／`完成寫旗標`** 的那些（通常只在一顆上填）。</para>
    ///
    /// <para>⚠ 清場不會誤判：換圖載入中（`IsLoading`）與過關／死亡倒數（`IsEndingLevel`）時
    /// <c>Update</c> 整個提前 return，根本走不到這裡。</para>
    /// </summary>
    void TryFinishGroup(Wave w)
    {
        if (w.cleared) return;

        // 同群組（含自己）全部都要「波次出完且場上清空」
        for (int i = 0; i < _waves.Count; i++)
        {
            var o = _waves[i];
            if (!SameGroup(w, o)) continue;
            o.alive.RemoveAll(go => go == null);
            if (!WavesDone(o) || o.alive.Count > 0) return;
        }

        // 整組一起標記，並讓「有填鏈」的每一顆各自推一次
        for (int i = 0; i < _waves.Count; i++)
        {
            var o = _waves[i];
            if (!SameGroup(w, o) || o.cleared) continue;
            o.cleared = true;
            if (o.chainRegion == null) continue;
            if (o.nextOnSpawn) continue;   // 「生出來時」那一種：鏈在生出來那刻就推過了，全滅不再推第二次

            string grpTag = o.waveGroup != null ? $"（群組 {o.waveGroup}）" : "";
            bool hasChain = !string.IsNullOrEmpty(o.chainRegion.GetString("next"))
                         || !string.IsNullOrEmpty(o.chainRegion.GetString("setFlag"));
            if (!hasChain)
            {
                // ⚠ 常見的配置漏填：波次設好了、怪也清光了，但沒填「接續觸發／完成寫旗標」
                //    ⇒ 全滅之後什麼都不會發生，而且**完全沒有訊息**，很難查（作者 2026-09-22 實際踩到）。
                Debug.LogWarning($"[MapMonsterRespawner] 出生點「{o.regionName}」{grpTag} 波次全數清空，" +
                                 "但它沒有填「接續觸發(next)」或「完成寫旗標(setFlag)」⇒ 沒有任何後續。" +
                                 "要在清光後播對話/開門，請在這顆出生點的通用欄位填「接續觸發」。");
                continue;
            }
            Debug.Log($"[MapMonsterRespawner] 出生點「{o.regionName}」{grpTag} 波次全數清空 → 推鏈。");
            TriggerChain.OnCompleted(o.chainRegion);
        }
    }

    /// <summary>同一個波次群組？兩邊都留空 ⇒ 只有自己算同組（各自獨立收尾）。</summary>
    static bool SameGroup(Wave a, Wave b)
        => (a.waveGroup == null || b.waveGroup == null) ? ReferenceEquals(a, b)
                                                        : a.waveGroup == b.waveGroup;

    /// <summary>這個出生點現在該不該生：沒填條件＝永遠可以；有填＝看觸發鏈條件當下成不成立（停用/條件旗標/周目/道具/完成關卡數）。</summary>
    static bool IsOn(Wave w) => w.region == null || TriggerChain.IsActive(w.region);

    /// <summary>
    /// 生一波：每格各一隻，生到「同時存在上限」就停手。
    /// <para>⚠ <b>一隻都沒生出來的那一波不計入 <c>wavesFired</c></b>（作者 2026-09-22 拍板）：
    /// 「20 波」的語意是**實實在在 20 批怪**。被「同時存在上限」整個擋掉時若照樣計數，
    /// 玩家只要躲著不打，波數就會空轉跑完、一隻怪都沒出現就宣告全滅。</para>
    /// </summary>
    int Fire(Wave w)
    {
        w.startedOnce = true;
        w.timer = 0f;
        if (w.interval <= 0f) w.doneOneShot = true;

        // 先清掉已死（被 Destroy）的參照，才知道現在還活著幾隻。
        w.alive.RemoveAll(go => go == null);

        bool runActive = RunProgress.Exists && RunProgress.Instance.RunActive;
        int spawned = 0;
        for (int i = 0; i < w.points.Count; i++)
        {
            if (w.interval > 0f && w.alive.Count >= w.maxAlive) break;   // 場上夠多了，這一波剩下的格子跳過（一次性不設限）

            // 一次性出生點有 spawnKey：死了記進度（本趟不再重生），所以先問問這格是不是已經清掉了。
            string key = (w.spawnKeys != null && i < w.spawnKeys.Count) ? w.spawnKeys[i] : null;
            if (!string.IsNullOrEmpty(key) && runActive && RunProgress.Instance.IsSpawnKilled(w.mapId, key)) continue;

            int id = w.monsterIds.Length == 1
                   ? w.monsterIds[0]
                   : w.monsterIds[Random.Range(0, w.monsterIds.Length)];

            // 重複產生：spawnKey 留空＝不記 RunProgress『已清』（不然死一次就永遠不再生），dropsLoot 明確給 true＝照常掉寶。
            var go = _spawner.SpawnMonster(id, w.points[i], w.deathFlag, MonsterFaction.Enemy, key,
                                           dropsLoot: string.IsNullOrEmpty(key) ? true : (bool?)null);
            if (go == null) continue;
            w.alive.Add(go);
            spawned++;
        }

        if (spawned > 0) w.wavesFired++;   // 見本方法的註解：生不出來就不算一波

        // 接續時機＝生出來時（2026-09-23 夢境佛掌）：第一次真的生出怪的那一刻就推鏈，而不是等全滅。
        // 理由：佛掌這種「不會死的怪」全滅永遠不會發生，後面要接的偵測／演出就永遠接不上。
        if (spawned > 0 && w.nextOnSpawn && !w.spawnNotified && w.chainRegion != null)
        {
            w.spawnNotified = true;
            Debug.Log($"[MapMonsterRespawner] 出生點「{w.regionName}」接續時機＝生出來時 → 推鏈。");
            TriggerChain.OnCompleted(w.chainRegion);
        }
        return spawned;
    }
}
