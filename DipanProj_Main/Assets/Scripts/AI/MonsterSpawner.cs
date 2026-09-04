using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public struct PrefabMapping
{
    public string Path;
    public GameObject Prefab;
}

public class MonsterSpawner : MonoBehaviour
{
    public TextAsset MonsterCSV;
    public List<PrefabMapping> PrefabMappings = new List<PrefabMapping>();
    public LayerMask EnemyLayer;
    
    private List<MonsterData> _monsterDatabase = new List<MonsterData>();
    private Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();

    void Awake()
    {
        InitializePrefabCache();
        LoadMonsterData();
    }

    private void InitializePrefabCache()
    {
        foreach (var mapping in PrefabMappings)
        {
            if (!string.IsNullOrEmpty(mapping.Path) && mapping.Prefab != null)
            {
                _prefabCache[mapping.Path] = mapping.Prefab;
            }
        }
    }

    [Header("自動生成設定")]
    public bool AutoSpawn = true;         // 關掉 = 純由地圖 monsterSpawn 出生點生怪（推薦給用 MapLoader 的場景）
    public int MonsterIDToSpawn = 1;      // 要生成的怪物 ID
    public float SpawnInterval = 1.0f;    // 生成間隔 (秒)
    public float SpawnRadius = 5.0f;      // 隨機生成範圍
    private float _spawnTimer = 0f;

    void Update()
    {
        if (!AutoSpawn) return;           // 地圖驅動模式：不自動亂生，改由 MapLoader 在出生點生怪
        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer = 0f;
            SpawnRandomMonster();
        }
    }

    private void SpawnRandomMonster()
    {
        // 在半徑內隨機取得位置 (以 Spawner 座標為中心)
        Vector2 randomOffset = Random.insideUnitCircle * SpawnRadius;
        Vector2 spawnPos = (Vector2)transform.position + randomOffset;
        
        SpawnMonster(MonsterIDToSpawn, spawnPos);
    }

    private void LoadMonsterData()
    {
        if (MonsterCSV == null)
        {
            Debug.LogError("Monster CSV is not assigned!");
            return;
        }

        string[] lines = MonsterCSV.text.Split('\n');
        // 跳過標題列 (ID,Name,HP,BrainType,Weapon,Scale,PrefabPath)
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            
            string[] values = lines[i].Split(',');
            if (values.Length < 7) continue;

            MonsterData data = new MonsterData();
            data.ID = int.Parse(values[0]);
            data.Name = values[1];
            data.HP = float.Parse(values[2]);
            data.BrainType = values[3].Trim();   // 必須 Trim：CSV 值常帶前導空白，不 Trim 會讓 BrainType switch（如 RedBridalGown）對不上而掉回 default=Chase
            data.Weapon = values[4].Trim();
            data.Scale = float.Parse(values[5]);
            data.PrefabPath = values[6].Trim();

            data.InvincibleTimeMs = values.Length > 7 ? float.Parse(values[7]) : 0;
            data.KnockbackThreshold = values.Length > 8 ? float.Parse(values[8]) : 0;
            data.KnockbackPercent = values.Length > 9 ? float.Parse(values[9]) : 0;

            data.ContactDamage = (values.Length > 10 && !string.IsNullOrWhiteSpace(values[10])) ? float.Parse(values[10]) : 10f;
            data.DamageReduction = (values.Length > 11 && !string.IsNullOrWhiteSpace(values[11])) ? float.Parse(values[11]) : 0f;
            data.Speed = (values.Length > 12 && !string.IsNullOrWhiteSpace(values[12])) ? float.Parse(values[12]) : 3f;
            data.AnimFPS = (values.Length > 13 && !string.IsNullOrWhiteSpace(values[13])) ? float.Parse(values[13]) : 8f;
            data.AttackInterval = (values.Length > 14 && !string.IsNullOrWhiteSpace(values[14])) ? float.Parse(values[14]) : 0.5f;
            data.DetectionRange = (values.Length > 15 && !string.IsNullOrWhiteSpace(values[15])) ? float.Parse(values[15]) : 10f;

            // 顯示名/頭像（boss 開戰資訊用；一般怪留空即可）。字串欄一律 Trim（CSV 值常帶空白，見 PROBLEMS F4）。
            data.DisplayName = (values.Length > 16 && !string.IsNullOrWhiteSpace(values[16])) ? values[16].Trim() : "";
            data.PortraitPath = (values.Length > 17 && !string.IsNullOrWhiteSpace(values[17])) ? values[17].Trim() : "";

            // 陣營（索引 22，表尾欄）：空＝Enemy。Werewolf/狼人、Vampire/吸血鬼、Neutral/中立（見 FactionRelations.Parse）。
            data.FactionStr = (values.Length > 22 && !string.IsNullOrWhiteSpace(values[22])) ? values[22].Trim() : "";

            // 遊戲中說話：句子1~句子4（索引 18~21）。每格可空；有內容才加入。格式見 ParseSpeechLine。
            // ⚠️ CSV 用半形逗號分欄 → 句子內不能有半形逗號，要用全形「，」（見 readme/PROBLEMS）。
            data.SpeechLines.Clear();
            for (int c = 18; c <= 21; c++)
            {
                if (values.Length <= c) break;
                if (ParseSpeechLine(values[c], out var line)) data.SpeechLines.Add(line);
            }

            _monsterDatabase.Add(data);
        }

        Debug.Log($"Loaded {_monsterDatabase.Count} monsters from CSV.");
    }

    /// <summary>
    /// 解析一格說話文本 → <see cref="MonsterSpeechLine"/>。空白格回 false（不加入）。
    /// 前綴「N%:」或「N%：」（半/全形冒號皆可）＝血量比例剩 N% 以下才解鎖；無前綴＝門檻 100（一直可講）。
    /// 例：「我要殺了你~~」→ 門檻100；「30%: 你真的惹怒我了」→ 門檻30、文字「你真的惹怒我了」。
    /// 只有「開頭是數字＋%＋冒號」才當門檻，一般句子含冒號（如「他說：快跑」）不受影響。
    /// </summary>
    private static bool ParseSpeechLine(string raw, out MonsterSpeechLine line)
    {
        line = new MonsterSpeechLine { UnlockAtPercent = 100f, Text = "" };
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string s = raw.Trim();
        int colon = s.IndexOf(':');
        if (colon < 0) colon = s.IndexOf('：');   // 全形冒號也吃
        if (colon > 0)
        {
            string head = s.Substring(0, colon).Trim();
            // head 必須形如「數字%」或「數字％」才視為門檻前綴
            if (head.Length >= 2 && (head[head.Length - 1] == '%' || head[head.Length - 1] == '％'))
            {
                string numPart = head.Substring(0, head.Length - 1).Trim();
                if (float.TryParse(numPart, out float pct))
                {
                    line.UnlockAtPercent = Mathf.Clamp(pct, 0f, 100f);
                    line.Text = s.Substring(colon + 1).Trim();
                    return !string.IsNullOrEmpty(line.Text);
                }
            }
        }

        line.Text = s;   // 無門檻前綴：整句都是文字，門檻 100（一直可講）
        return true;
    }

    /// <summary>依 ID 取怪物配方資料（找不到回 null、不印錯誤）。給 BossIntroPanel 等 UI 端查 DisplayName / PortraitPath 用。</summary>
    public MonsterData GetData(int id) => _monsterDatabase.Find(m => m.ID == id);

    /// <summary>
    /// 生一隻怪。
    /// <paramref name="spawnKey"/>：地圖一次性出生點的唯一 key（有值＝死亡時記進 RunProgress『已清』，本趟不再重生）；召喚物與「重複產生」的出生點留空。
    /// <paramref name="dropsLoot"/>：死亡是否掉寶。留 null＝沿用舊語意（有 spawnKey 才掉）；重複產生的出生點明確傳 true（不記進度但照常掉寶）。
    /// </summary>
    public GameObject SpawnMonster(int id, Vector2 position, string deathFlag = null, MonsterFaction faction = MonsterFaction.Enemy, string spawnKey = null, bool? dropsLoot = null)
    {
        MonsterData data = _monsterDatabase.Find(m => m.ID == id);
        if (data == null)
        {
            Debug.LogError($"Monster ID {id} not found!");
            return null;
        }

        // 陣營：呼叫端沒特別指定（＝預設 Enemy）時，改用 CSV 的 Faction 欄（空＝維持 Enemy）。
        // 召喚系統明確傳 PlayerAlly（玩家召喚物），優先於表——同一種怪被玩家召出來就是友軍。
        if (faction == MonsterFaction.Enemy && !string.IsNullOrWhiteSpace(data.FactionStr))
        {
            var parsed = FactionRelations.Parse(data.FactionStr);
            if (parsed != null) faction = parsed.Value;
            else Debug.LogWarning($"[MonsterSpawner] 怪物 {id}「{data.Name}」的 Faction 欄「{data.FactionStr}」認不得，維持 Enemy。");
        }

        GameObject go;
        if (!string.IsNullOrEmpty(data.PrefabPath) && _prefabCache.TryGetValue(data.PrefabPath, out GameObject prefab))
        {
            // 舊路線（向下相容）：CSV 有填 PrefabPath 且有對應 prefab → 用該 prefab（含自帶 Animator 的舊怪）。
            go = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            // 路線 B（量產）：沒指定 prefab → 程式建一隻通用怪，外觀由 MonsterAnimator 依怪名載圖。
            // 加新怪 = 丟圖到 Monsters/SequenceImage/<怪名>/ ＋ CSV 加一列，不必拉 prefab/Animation。
            go = BuildMonsterGameObject(data.Name, position);
        }

        // Layer：統一交給 FactionRelations.ApplyLayer——Enemy＝Inspector 指定的 EnemyLayer；PlayerAlly/Neutral＝Ally 層；
        // 部族（狼人/吸血鬼）依劇本狀態（和平/結盟＝Ally 層打不到、開戰未結盟＝Enemy 層可打）。
        int inspectorEnemyLayer = EnemyLayer != 0 ? Mathf.RoundToInt(Mathf.Log(EnemyLayer.value, 2)) : -1;
        FactionRelations.ApplyLayer(go, faction, inspectorEnemyLayer);

        // 設定縮放
        go.transform.localScale = Vector3.one * data.Scale;

        MonsterController controller = go.GetComponent<MonsterController>();
        if (controller == null)
        {
            controller = go.AddComponent<MonsterController>();
        }
        
        controller.Initialize(data);
        controller.Faction = faction;       // 陣營：決定追誰/打誰/在哪層（在 Start 之前設好，contact/目標選擇才讀得到）
        if (faction == MonsterFaction.PlayerAlly) controller.SetBrain(new AllyBrain());   // 玩家召喚物＝聰明跟班（跟玩家+打敵怪）
        controller.DeathFlag = deathFlag;   // 出生點 trigger 的「死亡觸發旗標」；此擺放專屬，空＝不寫旗標
        controller.SpawnKey = spawnKey;     // 關卡進度用：本張地圖唯一的出生點 key（有值＝一次性地圖出生怪，死了記進度）。見 RunProgress
        // 掉寶與「記不記進度」拆開：一次性出生點兩者都有；重複產生的出生點只掉寶不記進度；召喚物兩者都沒有（防無限刷）。
        controller.DropsLoot = dropsLoot ?? !string.IsNullOrEmpty(spawnKey);

        // 怪物常駐體光：暗地圖裡讓輪廓浮出來（見下方 AttachMonsterGlow）
        AttachMonsterGlow(go, data.Scale);

        // 🟢 初始面向設定：根據主角位置決定面向
        SetInitialOrientation(go);

        return go;   // 回傳生成物件（召喚技能用來追蹤同時存在上限；一般呼叫端可忽略）
    }

    // ───────────────────────── 怪物常駐體光 ─────────────────────────
    // 幽暗/噩夢/鬼霧那些暗地圖裡，怪物整隻沉在黑色中、連輪廓都看不出來（作者實機回報，紅嫁衣女殭屍
    // 也一樣）。給每隻怪掛一盞很小的 LightSource，讓它在暗處浮出一圈輪廓——用途與
    // AtmosphereController 的「玩家常駐體光」完全相同（照不了路、不影響點燈壓力），只是換成**陰冷青白**，
    // 暗場景裡一眼分得出敵我。適用**所有怪物、所有地圖**，不是只給某個關卡。
    //
    // ⚠ 為什麼掛在 SpawnMonster 而不是 MonsterController.Start()：
    //   **NPC 也是用 MonsterController 當地基**（NpcSpawner 一樣 AddComponent<MonsterController>），
    //   掛在那裡的話 NPC 會跟著發鬼光。放在這裡＝怪物有、NPC 沒有，意圖明確也不依賴元件加入的時序。
    //
    // ⚠ 不會擠掉場景的燈籠火把：LightSource.CollectNearest 的排序鍵是「距離 − 半徑」，
    //   體光半徑很小，遇到半徑大的場景燈天生就排在後面（同框 20 盞上限見 AtmosphereController.MaxLights）。
    //
    // ⚠ 只在「吃照明」的氛圍才看得見（幽暗2/噩夢3/深海恐怖9/鬼霧14/冷月18/燭火幽影19，或 type1+環境壓暗）——
    //   亮場景 shader 根本不讀光源，這盞燈不會有任何副作用。
    // ⚠ **不要照抄玩家體光的數值**（第一版就是這樣，作者實機回報「看起來似乎沒有光圈」）：
    //    玩家體光（半徑 1.2／亮度 0.35）的設計目的是「微弱到照不了路」——因為玩家本來就知道自己在哪，
    //    只需要一點點提示。怪物要的是**被認出來**，那是更高的門檻：在幽暗(mode 2，壓暗 0.8)的場景裡，
    //    0.35 的亮度只把 0.2 的底亮提到 0.3 上下，肉眼幾乎讀不出輪廓。
    private const float MonsterGlowRadius    = 1.2f;    // 同玩家：這一層只負責讓腳邊地面有一點光照感
    private const float MonsterGlowIntensity = 0.35f;   // 同玩家。⚠ 這層在深色地板上幾乎無效，別靠調它變亮
    private const float MonsterGlowSoftness  = 0.32f;   // 內圈小、邊緣瀰漫 ⇒ 是「浮出輪廓」不是「打一盞聚光燈」
    private static readonly Color MonsterGlowColor = new Color(0.72f, 0.90f, 0.98f, 1f);   // 陰冷青白
    // 加色光暈的亮度與大小（CharacterGlow）。**怪在暗地板上看不看得見，主要是這兩個值決定的**。
    // ⚠ 目標是「不要完全被黑暗吃掉、勉強看得到輪廓」，**不是給怪一盞跟著跑的燈**。
    //    2026-09-04 第一版設 0.30／1.9，實機作者回報「根本變成行動燈光」——這兩個值寧可偏低。
    //    Linear 色彩空間疊色比直覺重約一倍（PROBLEMS E11），往上調要很小步。
    private const float MonsterGlowAdditive = 0.11f;
    private const float MonsterGlowSize     = 1.35f;   // 光暈直徑 = 角色可見高度 × 此值（貼著輪廓，不要撐開）

    /// <summary>
    /// 給一隻怪掛常駐體光。已經自帶 <see cref="LightSource"/> 的 prefab（設計上就會發光的怪）不動它。
    ///
    /// ⚠ **這裡與 <c>MonsterController.Start</c> 各叫一次，兩邊都會呼叫本方法**（自己判重、不會掛兩顆）：
    ///   這裡是「知道 CSV Scale 的正規入口」；`MonsterController.Start` 那一道是**保險**，
    ///   涵蓋任何沒有經過 SpawnMonster 的生成路徑（2026-09-04：新娘房 boss 與她的召喚物實機沒有光，
    ///   走查所有已知路徑都該經過這裡卻沒生效，所以補上那一道）。
    /// </summary>
    public static void AttachMonsterGlow(GameObject go, float scale)
    {
        if (go == null) return;

        // ── 第一層：LightSource（走 AtmosphereController 的照明系統）──
        // 它讓怪**周圍的地面**跟著亮一點，有「被光照到」的感覺。
        // ⚠ 但這一層在**深色地板上幾乎無效**——那套光是乘法（type 2 幽暗是 col *= lerp(0.35,1,v)），
        //    作用是「把壓暗還原」而不是「加光」，在原本就很暗的地板上還原了也還是暗。
        //    所以一定要搭配下面第二層。詳見 CharacterGlow 檔頭。
        if (go.GetComponent<LightSource>() == null)   // 尊重 prefab 自帶的發光設定
        {
            var ls = go.AddComponent<LightSource>();
            ls.radius    = MonsterGlowRadius * Mathf.Max(0.5f, scale);   // 大隻的怪光圈跟著大（CSV 的 Scale 欄）
            ls.intensity = MonsterGlowIntensity;
            ls.softness  = MonsterGlowSoftness;
            ls.color     = MonsterGlowColor;
            ls.flicker   = 0f;   // 恆定不呼吸（同玩家體光）——場上一堆怪一起明滅，畫面會到處閃
        }

        // ── 第二層：CharacterGlow（角色背後的加色光暈）──
        // **這一層才是「不管地板多暗都看得見輪廓」的保證**（加色，不受乘法照明的限制）。
        if (go.GetComponent<CharacterGlow>() == null)
        {
            var cg = go.AddComponent<CharacterGlow>();
            cg.GlowColor = MonsterGlowColor;
            cg.Intensity = MonsterGlowAdditive;
            cg.SizeFactor = MonsterGlowSize;
        }
#if UNITY_EDITOR
        Debug.Log($"[MonsterGlow] 掛上體光：{go.name}　pos={go.transform.position.x:F1},{go.transform.position.y:F1}" +
                  $"　（LightSource r={MonsterGlowRadius * Mathf.Max(0.5f, scale):F2} i={MonsterGlowIntensity:F2}／加色光暈 i={MonsterGlowAdditive:F2}）");
#endif
    }

    /// <summary>
    /// 路線 B：程式建一隻通用怪的外殼（零 prefab）。只放最基本的 SpriteRenderer；
    /// MonsterController.Initialize / Start 會自行補上 MonsterActuator(帶 Rigidbody2D+CircleCollider2D)、
    /// MonsterAnimator(程式動畫)、HitReaction、接觸傷害、影子等。外觀由 MonsterAnimator 依怪名載圖。
    /// </summary>
    private GameObject BuildMonsterGameObject(string monsterName, Vector2 position)
    {
        var go = new GameObject(string.IsNullOrEmpty(monsterName) ? "Monster" : monsterName);
        go.transform.position = position;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;   // 初始值；實際排序由 MonsterController 掛的 YSortByFeet 每幀依腳底 Y 覆寫（見 MapDepthSort）

        go.AddComponent<MonsterController>();
        return go;
    }

    private void SetInitialOrientation(GameObject monsterGo)
    {
        // 透過 Tag 尋找玩家，不依賴任何具體玩家類別
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        SpriteRenderer sr = monsterGo.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        // 依「來源圖朝向」換算（同 MonsterController.HandleVisuals）：面右=玩家在右。
        // 來源朝右 → 面右不翻；來源朝左 → 面右才翻。MonsterController 的 _spriteRenderer 此時可能還沒備好，
        // 故直接讀其欄位、設這顆 sr（生成瞬間先擺對，之後每幀由 MonsterController 維持）。
        var mc = monsterGo.GetComponent<MonsterController>();
        bool srcFacesRight = (mc == null) || mc.SpriteSourceFacesRight;
        float diffX = playerObj.transform.position.x - monsterGo.transform.position.x;
        bool faceRight = diffX > 0;
        sr.flipX = (faceRight != srcFacesRight);
    }

    // 🟢 繪製生成區域，方便在編輯器中確認範圍是否有超出圍牆
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, SpawnRadius);
    }
}
