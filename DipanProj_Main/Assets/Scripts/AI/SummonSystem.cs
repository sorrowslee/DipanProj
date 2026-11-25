using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 召喚的共用核心（擁有者無關）：玩家與怪物(boss)共用同一套「表驅動生怪」邏輯。
/// 從配方 ID 池隨機抽 <c>SummonCount</c> 隻，在 <paramref name="originPos"/> 周圍 <c>SummonRadius</c> 環上生成，
/// 但**生怪點一律要「怪站得住」**（`MapNavGrid.IsWalkableWorld`，見 <see cref="IsSpawnSpotOk"/>），
/// 避免怪出生在牆裡或牆外。
///
/// <para><b>每個 ID 各一隻（<c>SummonEachOnce=1</c>）</b>：改成「把池子裡的每一種各叫一隻出來」——不重複抽、
/// 忽略 <c>SummonCount</c>，且**生成角度平均分開**（不是各自隨機，否則十幾隻會擠成一團互相卡住）。
/// 給「一口氣叫齊一整組」的大絕用（紅嫁衣血量過半後的家人齊聚）。數量仍受 <c>SummonMaxAlive</c> 夾，
/// 所以那一筆配方要把上限填到 ≥ 池子大小才會一次到齊。</para>
/// 受 <c>SummonMaxAlive</c> 同時上限限制（呼叫端各持一份 <paramref name="aliveTracker"/>，死掉分身被 Unity 判 null 清掉）。
///
/// <para><b>召喚特效</b>：<paramref name="summonVfxId"/> &gt; 0 時，在每個生怪點播一次 VfxTable 特效並**同一幀生怪**
/// （邊播邊出現）；特效會依怪的可見高度縮放（見 VfxManager.SpawnSizedToHeight）。找不到 VfxManager 就只生怪。</para>
///
/// <para><b>定點召喚（<paramref name="atPoint"/>＝true，配方 <c>SummonAtCursor=1</c>，2026-11-24）</b>：
/// 呼叫端把 <paramref name="originPos"/> 傳成「滑鼠點的位置（已被 Range 夾住）」，第一隻**就生在那一點**
/// （站不住才往外找最近站得住的點），第二隻起才照 <c>SummonRadius</c> 環繞那一點。墨家機關塔用。</para>
///
/// <para><b>血量跟召喚者走</b>：怪物表 <c>OwnerHpRatio</c> &gt; 0 的怪，生成後 HP＝召喚者當下最大血量 × 比例
/// （見 <see cref="MonsterController.ApplyOwnerHp"/>；召喚者是玩家看 CombatStats、是怪看 MonsterController）。</para>
///
/// 冷卻由呼叫端各自管（玩家 _fireTimer、boss MonsterWeaponUser）。陣營：玩家=PlayerAlly、怪物/boss=Enemy。
/// </summary>
public static class SummonSystem
{
    /// <summary>目前是否還有空位可召喚（未達 SummonMaxAlive）。呼叫端可在扣魔/進冷卻前先問，避免扣了魔卻沒生怪。</summary>
    public static bool HasRoom(RecipeEntry recipe, List<GameObject> aliveTracker)
    {
        if (recipe == null || recipe.Mode != WeaponMode.Summon || aliveTracker == null) return false;
        if (recipe.SummonIds == null || recipe.SummonIds.Length == 0) return false;
        aliveTracker.RemoveAll(go => go == null);
        return aliveTracker.Count < recipe.SummonMaxAlive;
    }

    public static bool Cast(GameObject owner, Vector3 originPos, RecipeEntry recipe, List<GameObject> aliveTracker, MonsterFaction faction, int summonVfxId = 0, bool atPoint = false)
    {
        if (recipe == null || recipe.Mode != WeaponMode.Summon || aliveTracker == null) return false;

        aliveTracker.RemoveAll(go => go == null);
        if (recipe.SummonIds == null || recipe.SummonIds.Length == 0) return false;
        if (aliveTracker.Count >= recipe.SummonMaxAlive) return false;

        var spawner = Object.FindObjectOfType<MonsterSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[SummonSystem] 場景找不到 MonsterSpawner，召喚略過。");
            return false;
        }

        VfxManager vfx = (summonVfxId > 0) ? Object.FindObjectOfType<VfxManager>() : null;

        // eachOnce＝池裡每個 ID 各一隻（忽略 SummonCount）；否則照 SummonCount 隨機抽。
        bool eachOnce = recipe.SummonEachOnce;
        int want = eachOnce ? recipe.SummonIds.Length : Mathf.Max(1, recipe.SummonCount);
        int room = recipe.SummonMaxAlive - aliveTracker.Count;
        int n = Mathf.Min(want, room);

        // 角度平均分開（只有 eachOnce 用）：十幾隻同時出來若各自隨機取角，會擠成一團、互相卡位又看不出陣仗。
        // 起始角隨機 → 每次大絕的隊形不會一模一樣。
        float baseAngle = Random.value * Mathf.PI * 2f;
        float angleStep = (n > 0) ? (Mathf.PI * 2f / n) : 0f;

        int spawned = 0;
        for (int k = 0; k < n; k++)
        {
            int id = eachOnce ? recipe.SummonIds[k] : recipe.SummonIds[Random.Range(0, recipe.SummonIds.Length)];
            Vector2 pos = (atPoint && k == 0)
                ? FindSpawnPosAt((Vector2)originPos, recipe.SummonRadius, owner != null ? (Vector2)owner.transform.position : (Vector2)originPos)                             // 定點：第一隻就在那一點
                : eachOnce
                ? FindSpawnPosNear((Vector2)originPos, recipe.SummonRadius, baseAngle + angleStep * k)  // 分好的角度，被擋才微調
                : FindSpawnPos((Vector2)originPos, recipe.SummonRadius);                                // 避開牆/水的落點

            // 分身不帶 deathFlag（見 readme/TRIGGER_CHAIN.md §7）。玩家召喚=PlayerAlly、怪物召喚=Enemy。
            GameObject go = spawner.SpawnMonster(id, pos, null, faction);
            if (go == null) continue;
            aliveTracker.Add(go); spawned++;

            // 血量跟召喚者走（怪物表 OwnerHpRatio > 0 才有作用；機關塔＝玩家的 1/10）
            float ownerMax = OwnerMaxHealth(owner);
            if (ownerMax > 0f)
            {
                var mc = go.GetComponent<MonsterController>();
                if (mc != null) mc.ApplyOwnerHp(ownerMax);
            }

            // 召喚特效：同一幀在生怪點播，並依這隻怪的可見大小縮放（大怪大特效、小怪小特效）。
            if (vfx != null) PlaySpawnVfx(vfx, summonVfxId, go, pos);
        }
        return spawned > 0;
    }

    /// <summary>召喚者當下的最大血量：玩家看 <see cref="CombatStats"/>，怪（boss 召喚）看 <see cref="MonsterController"/>；都沒有回 0。</summary>
    private static float OwnerMaxHealth(GameObject owner)
    {
        if (owner == null) return 0f;
        var cs = owner.GetComponent<CombatStats>();
        if (cs != null) return cs.MaxHealth;
        var mc = owner.GetComponent<MonsterController>();
        return mc != null ? mc.MaxHealth : 0f;
    }

    /// <summary>
    /// 定點生怪（<c>SummonAtCursor</c>）：那一點站得住就直接用；站不住（點在牆上／水裡）就**由近往遠**一圈圈找，
    /// 挑離指定點最近、站得住的位置——跟 <see cref="FindSpawnPos"/>（由外往內縮向施放者）方向相反，
    /// 因為這裡要的是「盡量貼近玩家點的地方」。附近都站不住（點進一大片牆／水）就退回「在施放者身邊」（<see cref="FindSpawnPos"/>），
    /// 不用 <see cref="FallbackSpot"/> 的全圖隨機點——砲台跑到地圖另一頭比生在身邊更莫名其妙。
    /// </summary>
    private static Vector2 FindSpawnPosAt(Vector2 point, float radius, Vector2 casterPos)
    {
        var nav = MapNavGrid.Instance;
        if (IsSpawnSpotOk(nav, point)) return point;
        float maxR = Mathf.Max(1.5f, radius * 2f);
        for (float r = 0.3f; r <= maxR + 0.001f; r += 0.3f)
        {
            float a0 = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 12; i++)
            {
                float a = a0 + i * (Mathf.PI * 2f / 12f);
                Vector2 p = point + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (IsSpawnSpotOk(nav, p)) return p;
            }
        }
        return FindSpawnPos(casterPos, radius);
    }

    // 在施放者周圍找一個「怪站得住」的生怪點（見 IsSpawnSpotOk）：試多個角度、由外往內縮，挑第一個可用的。
    private static Vector2 FindSpawnPos(Vector2 origin, float radius)
    {
        var nav = MapNavGrid.Instance;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = radius * (1f - attempt * 0.07f);            // 逐次往內縮，靠近施放者（施放者站的地方通常可走）
            if (r < 0f) r = 0f;
            Vector2 p = origin + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            if (IsSpawnSpotOk(nav, p)) return p;
        }
        return FallbackSpot(nav, origin);
    }

    /// <summary>
    /// 指定角度的生怪點（eachOnce 用）：盡量維持「分配到的那個方向」，放不下就**先往外找、再往內擠**
    /// ——房間小的時候（新娘房可走區只有一小塊）往身邊擠反而更容易沒位置，放遠一點無所謂，出界才是不能接受的。
    /// 外層是半徑（依「離原本距離的偏離」排序，讓整群大致還在同一個環上、隊形好看），內層才偏擺角度。
    /// </summary>
    private static Vector2 FindSpawnPosNear(Vector2 origin, float radius, float angleRad)
    {
        var nav = MapNavGrid.Instance;
        for (int ri = 0; ri < NearRadiusMuls.Length; ri++)
        {
            float r = radius * NearRadiusMuls[ri];
            for (int i = 0; i < NearAngleOffsets.Length; i++)
            {
                float a = angleRad + NearAngleOffsets[i] * Mathf.Deg2Rad;
                Vector2 p = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (IsSpawnSpotOk(nav, p)) return p;
            }
        }
        return FallbackSpot(nav, origin);
    }

    // 半徑候選：原距離優先，接著往外、往內交替（外側排前面——放遠一點沒關係，擠在 boss 身上才難看）。
    private static readonly float[] NearRadiusMuls = { 1f, 1.25f, 0.75f, 1.5f, 0.55f, 1.8f, 0.4f, 2.2f };
    // 偏擺順序：先原角度，再左右愈偏愈多（保持「大致還在分配到的方向」，隊形才不會塌成一堆）。
    private static readonly float[] NearAngleOffsets = { 0f, 12f, -12f, 25f, -25f, 40f, -40f, 60f, -60f, 80f, -80f };

    /// <summary>
    /// 這個點能不能生怪＝<b>怪站得住嗎</b>。一律以 <see cref="MapNavGrid.IsWalkableWorld"/> 為準：
    /// 那份格子已經把「可走層 ＋ 地上物/牆的物理碰撞（<c>UnionPhysics</c>）＋ 怪身半徑淨空（<c>AgentRadius</c>）」
    /// 聯集好了，是全遊戲判斷「怪能不能站這裡」的同一份真相（A* 走的也是它）。
    ///
    /// <para>⚠ <b>不要用 <c>Physics2D.OverlapCircle</c> 判「這點是不是在牆裡」</b>：專案全域
    /// <c>queriesStartInColliders = false</c>，查詢起點若正好落在某個 collider <b>內部</b>，那個 collider 會被
    /// <b>略過</b> → 回 null → 被誤判成「空地」。**點愈深入牆裡愈容易通過檢查**，於是怪被生到牆外面去
    /// （2026-09-16 紅嫁衣大絕實測，作者回報鬼魂被召到牆外；同一個雷見 readme/PROBLEMS.md <b>B7</b>）。</para>
    ///
    /// <para>只有在沒有尋徑格時（單場景測試、地圖還沒建好）才退回物理查詢——那種場合本來就沒有牆可撞。</para>
    /// </summary>
    private static bool IsSpawnSpotOk(MapNavGrid nav, Vector2 p)
    {
        if (nav != null && nav.Ready) return nav.IsWalkableWorld(p);

        int mask = LayerMask.GetMask("Environment", "Water");
        if (mask == 0) return true;                       // 沒障礙層可查（保險）
        return Physics2D.OverlapCircle(p, 0.35f, mask) == null;   // 0.35 ≈ 怪的腳
    }

    /// <summary>環上怎麼試都沒位置時的最後落點：先在整張可走區裡隨機找一個站得住的點
    /// （寧可離 boss 遠，也不能生到牆外去），真的找不到才退回施放者腳下——他站著的地方一定可走。</summary>
    private static Vector2 FallbackSpot(MapNavGrid nav, Vector2 origin)
    {
        if (nav != null && nav.Ready)
            for (int t = 0; t < 8; t++)
                if (nav.TryGetRandomWalkable(out Vector2 w)) return w;
        return origin;
    }

    // 怪物在畫面上的「可見高度」（世界單位）：route B 怪一律正規化到 CharacterWorldHeight × transform.Scale。取不到回 0。
    /// <summary>
    /// 在生怪點播一次「出生／召喚」特效，依這隻怪的可見高度縮放（大怪大特效、小怪小特效）。
    /// 召喚武器與地圖出生點（MapMonsterRespawner 的「出生特效」欄）共用，兩邊外觀一致。
    /// </summary>
    public static void PlaySpawnVfx(VfxManager vfx, int vfxId, GameObject go, Vector2 pos)
    {
        if (vfx == null || vfxId <= 0 || go == null) return;
        float targetH = MonsterVisibleHeight(go);
        if (targetH > 0f) vfx.SpawnSizedToHeight(vfxId, pos, targetH);
        else vfx.Spawn(vfxId, pos, 0f, go.transform.localScale.x);
    }

    private static float MonsterVisibleHeight(GameObject go)
    {
        if (go == null) return 0f;
        var mc = go.GetComponent<MonsterController>();
        float s = go.transform.localScale.x;
        if (mc != null && mc.CharacterWorldHeight > 0f) return mc.CharacterWorldHeight * s;
        return 0f;
    }
}
