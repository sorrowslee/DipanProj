using UnityEngine;
using System.Collections.Generic;
using Dipan.Data;

/// <summary>
/// 載入 <c>Assets/Data/WeaponTable.csv</c>、把 RecipeID 解析成 <see cref="RecipeEntry"/> 參照、載入子彈圖／光束素材。
/// 2026-08-26 起依表頭名稱取值（<see cref="CsvTable"/>），欄位可分群重排；每列走 <see cref="BuildWeapon"/>，
/// 武器效果模擬面板也用同一條路（<see cref="CreateTransient"/>）。
/// </summary>
public class WeaponManager : MonoBehaviour
{
    public TextAsset WeaponCSV;
    public RecipeManager RecipeManager;
    public GameObject BulletPrefab;
    /// <summary>目前武器的 WeaponTable ID；<b>0 = 沒有裝備武器</b>（此時 <see cref="GetCurrentWeapon"/> 回 null，玩家不能攻擊）。
    /// 由 <c>PlayerController.OnInventoryChanged</c> 依背包武器欄設定——裝備哪把就是哪把、卸下就回 0。</summary>
    public int CurrentWeaponID = 0;

    private Dictionary<int, WeaponData> _weapons = new Dictionary<int, WeaponData>();
    private readonly Dictionary<int, WeaponData> _recipeToWeapon = new Dictionary<int, WeaponData>(); // RecipeID → 使用該配方的武器（取最低 ID 那把）
    private List<int> _weaponIDs = new List<int>();   // 載入後排序保留；目前無人讀取（E 鍵循環切換移除後），留著供之後做武器圖鑑/除錯用
    private WeaponData _currentWeapon;

    /// <summary>所有武器（唯讀，原始表格資料）。給武器效果模擬面板「載入既有武器來改」用。</summary>
    public IReadOnlyDictionary<int, WeaponData> All => _weapons;

    /// <summary>
    /// **武器工坊（武器效果模擬）用的覆蓋**：不為 null 時 <see cref="GetCurrentWeapon"/> 一律回它（不看背包裝的是哪把），
    /// 所以 PlayerController 的全部發射路徑（離散／雷射／佛光／集氣）都會拿模擬武器去打。
    /// **鑲嵌照樣生效**：它跟正常武器一樣會過 <see cref="AbilityResolver"/>（作者要在模擬時真的去鍛造鑲珠子測），
    /// 珠子一變 <see cref="RefreshLoadout"/> 就重算。設回 null 就恢復正常。由 Assets/Editor/WeaponWorkbench.cs 設定；一般遊戲流程不要碰。
    /// </summary>
    public WeaponData SimulationOverride
    {
        get => _simBase;
        set { _simBase = value; RefreshCurrentWeapon(); }
    }
    private WeaponData _simBase;       // 工坊交進來的原始模擬武器
    private WeaponData _simResolved;   // 套完玩家鑲嵌後的模擬武器（真正被拿去打的那份）

    void Start()
    {
        LoadWeapons();
        // 這裡刻意「不」指定任何初始武器：沒有裝備武器就是沒有武器，按攻擊不該有反應。
        // （2026-07-27 前這裡會強制帶上武器表最高 ID，導致玩家一開場、或卸下裝備後仍能攻擊。）
        // 實際武器由 PlayerController 依背包武器欄呼叫 SwitchWeapon 設定；它在訂閱背包事件後會立刻做一次初始同步，
        // 讀檔還原也會經由 InventorySystem.RestoreState 結尾的 Raise() 走到同一條路，所以這裡不需要預設值。
        RefreshCurrentWeapon();
    }

    public WeaponData GetCurrentWeapon()
    {
        return _simResolved ?? _currentWeapon;
    }

    public void SwitchWeapon(int weaponID)
    {
        CurrentWeaponID = weaponID;
        RefreshCurrentWeapon();
    }

    /// <summary>
    /// 「玩家專屬」的武器解析器：把武器表上的原始資料 → 套上玩家身上所有裝備/鑲嵌給的能力 → 回傳一份拷貝。
    /// 由 PlayerController 在 Start 掛上（見 <see cref="PlayerAbilities"/>）。
    ///
    /// ⚠ 只作用在 <see cref="GetCurrentWeapon"/> 這條路——<see cref="GetWeapon"/> 一律回原始資料，
    /// 所以怪物（MonsterWeaponUser 走 GetWeapon）不會吃到玩家的鑲嵌加成。
    /// </summary>
    public static System.Func<WeaponData, WeaponData> AbilityResolver;

    /// <summary>關掉 Domain Reload 後 static 不會歸零，進 Play 時由 PlayModeStaticReset 呼叫。</summary>
    public static void ResetForPlayMode() => AbilityResolver = null;

    /// <summary>
    /// 鑲嵌內容變了但武器沒換時呼叫，重新解析一次目前武器。
    /// （只比武器 ID 會漏掉這種情況——珠子換了，ID 完全沒變。）
    /// </summary>
    public void RefreshLoadout() => RefreshCurrentWeapon();

    // 註：原本這裡有 SwitchToPreviousWeapon()（E 鍵循環切換整張武器表），
    // 已於 2026-07-27 移除——武器一律由背包武器欄決定，不再有繞過裝備的切換途徑。

    public WeaponData GetWeapon(int id)
    {
        // id <= 0 = 「沒有裝備武器」，是正常狀態而非錯誤 → 安靜回 null，不要洗 Console。
        if (id <= 0) return null;

        if (_weapons.TryGetValue(id, out WeaponData weapon))
            return weapon;

        Debug.LogError($"[WeaponTable] 找不到武器 ID {id}。");
        return null;
    }

    // 取「使用指定配方的武器」（落雷模式接 SubRecipeID 連鎖時，用連鎖配方對應的武器外觀/傷害）。找不到回 null。
    public WeaponData GetWeaponByRecipeID(int recipeID)
    {
        return _recipeToWeapon.TryGetValue(recipeID, out WeaponData w) ? w : null;
    }

    private void RefreshCurrentWeapon()
    {
        var baseWeapon = GetWeapon(CurrentWeaponID);
        // 玩家的能力容器在這裡把「表格資料」解析成「這個角色現在實際的武器」。
        // 沒掛解析器（例如編輯器測試場景）就照原樣用，行為與加入鑲嵌系統前一致。
        _currentWeapon = (AbilityResolver != null && baseWeapon != null) ? AbilityResolver(baseWeapon) : baseWeapon;
        // 模擬武器同樣套一次玩家的鑲嵌（工坊：真鑲珠子也要對模擬武器有效）
        _simResolved = (AbilityResolver != null && _simBase != null) ? AbilityResolver(_simBase) : _simBase;
    }

    /// <summary>
    /// 用「欄名 → 值」臨時建一把武器（**不會**登記進表；給武器效果模擬面板用）。
    /// <paramref name="recipe"/> 可以是表裡的配方或 <see cref="RecipeManager.CreateTransient"/> 做出來的臨時配方。
    /// </summary>
    public WeaponData CreateTransient(IReadOnlyDictionary<string, string> fields, RecipeEntry recipe, List<string> problems)
    {
        var w = BuildWeapon(fields, recipe, problems);
        LoadVisuals(w);
        return w;
    }

    private void LoadWeapons()
    {
        _weapons.Clear(); _recipeToWeapon.Clear(); _weaponIDs.Clear();
        if (WeaponCSV == null)
        {
            Debug.LogError("[WeaponTable] Inspector 的 WeaponCSV 沒有指定。");
            return;
        }
        if (BulletPrefab == null)
        {
            Debug.LogError("[WeaponTable] WeaponManager 的 BulletPrefab 沒有指定。");
            return;
        }

        var table = CsvTable.Parse(WeaponCSV.text, "WeaponTable");
        table.Require("ID", "Name", "Damage", "RecipeID");
        foreach (var err in table.Errors) Debug.LogError(err);
        var unknown = table.UnknownColumns(WeaponModeSpec.ColumnNames(FieldTable.Weapon));
        if (unknown.Count > 0)
            Debug.LogWarning($"[WeaponTable] 表頭有程式不認得的欄位（會被忽略）：{string.Join(", ", unknown)}。");

        var problems = new List<string>();
        foreach (var row in table.Rows)
        {
            problems.Clear();
            int recipeId = row.GetInt("RecipeID", 0);
            RecipeEntry recipe = (RecipeManager != null) ? RecipeManager.GetRecipe(recipeId) : null;
            var weapon = BuildWeapon(row.ToDictionary(), recipe, problems);
            if (weapon.ID <= 0)
            {
                Debug.LogError($"[WeaponTable] 第 {row.Line} 行的 ID 不是正整數，已略過。");
                continue;
            }
            foreach (var p in problems)
            {
                string msg = $"[WeaponTable] 武器 {weapon.ID}「{weapon.Name}」（第 {row.Line} 行）：{p}";
                if (p.StartsWith("[Error]")) Debug.LogError(msg); else Debug.LogWarning(msg);
            }
            LoadVisuals(weapon);

            _weapons[weapon.ID] = weapon;
            _weaponIDs.Add(weapon.ID);
            if (!_recipeToWeapon.ContainsKey(weapon.RecipeID))
                _recipeToWeapon[weapon.RecipeID] = weapon;   // 同配方多把武器時，取先載入（最低 ID）那把
        }

        _weaponIDs.Sort();
        Debug.Log($"[WeaponTable] 載入 {_weapons.Count} 把武器。");
    }

    /// <summary>從「欄名 → 值」建一把武器的資料（不載圖）。CSV 與模擬面板共用。</summary>
    private WeaponData BuildWeapon(IReadOnlyDictionary<string, string> f, RecipeEntry recipe, List<string> problems)
    {
        string S(string col) => (f != null && f.TryGetValue(col, out var v) && !string.IsNullOrWhiteSpace(v)) ? v.Trim() : "";

        var weapon = new WeaponData();
        weapon.ID = CsvFieldParse.Int(S("ID"), 0);
        weapon.Name = S("Name");
        weapon.Damage = CsvFieldParse.Float(S("Damage"), 1f);
        weapon.RecipeID = CsvFieldParse.Int(S("RecipeID"), 0);
        weapon.ManaCost = CsvFieldParse.Float(S("ManaCost"), 1f);   // 留空 = 1（見 readme/COMBAT.md）

        weapon.WeaponSpritePath = S("WeaponSpritePath");
        weapon.SpriteAngleOffset = CsvFieldParse.Float(S("SpriteAngleOffset"), 0f);
        weapon.WeaponAniPath = S("WeaponAniPath");
        weapon.WeaponAniNumber = CsvFieldParse.Int(S("WeaponAniNumber"), 0);
        weapon.AnimFPS = CsvFieldParse.Float(S("AnimFPS"), 0f);
        weapon.BulletScale = CsvFieldParse.Float(S("BulletScale"), 1f);

        // 雷射外觀欄位（只填編號；數字定義在 BeamStyleLibrary）
        int styleId = CsvFieldParse.Int(S("BeamStyle"), 2);   // 預設 2=標準雷射
        int colorId = CsvFieldParse.Int(S("BeamColor"), 9);   // 預設 9=白
        weapon.BeamStyle = BeamStyleLibrary.Get(styleId);
        weapon.BeamColor = BeamStyleLibrary.GetColor(colorId);
        weapon.BeamWidth = CsvFieldParse.Float(S("BeamWidth"), 0.5f);
        weapon.PixelBeamSet = S("PixelBeamSet");

        // 一次性特效 ID（引用 VfxTable）；留空 / 0 = 不觸發
        weapon.FireEffectID = CsvFieldParse.Int(S("FireEffectID"), 0);
        weapon.HitEffectID = CsvFieldParse.Int(S("HitEffectID"), 0);
        weapon.TrailEffectID = CsvFieldParse.Int(S("TrailEffectID"), 0);
        weapon.SummonEffectID = CsvFieldParse.Int(S("SummonEffectID"), 0);

        weapon.Recipe = recipe;
        weapon.BulletPrefab = BulletPrefab;

        if (recipe == null)
            problems?.Add($"[Error] RecipeID={weapon.RecipeID} 在 RecipeTable 找不到。");
        else if (problems != null && f != null)
            problems.AddRange(WeaponModeSpec.Validate(recipe.Mode, f, FieldTable.Weapon));

        return weapon;
    }

    /// <summary>依模式載入子彈圖／序列圖／光束素材。</summary>
    private void LoadVisuals(WeaponData weapon)
    {
        var recipe = weapon.Recipe;
        WeaponMode mode = recipe != null ? recipe.Mode : WeaponMode.Normal;

        // 雷射／連鎖及「非分段」落雷才需要光束素材；九霄雷獄的分段雷柱自帶 Sprite，不載無用光暈。
        if (mode == WeaponMode.Laser || mode == WeaponMode.Chain
            || (mode == WeaponMode.SkyStrike && recipe != null && !recipe.SegmentedColumn))
            LoadBeamAssets(weapon);

        if (!string.IsNullOrEmpty(weapon.WeaponAniPath) && weapon.WeaponAniNumber > 0)
        {
            var sprites = new Sprite[weapon.WeaponAniNumber];
            bool allLoaded = true;
            for (int fr = 0; fr < weapon.WeaponAniNumber; fr++)
            {
                string framePath = $"{weapon.WeaponAniPath}_{(fr + 1):D2}";
                sprites[fr] = Resources.Load<Sprite>(framePath);
                if (sprites[fr] == null)
                {
                    Debug.LogWarning($"[WeaponTable] 武器「{weapon.Name}」的序列圖 '{framePath}' 找不到。");
                    allLoaded = false;
                }
            }
            weapon.WeaponSprites = sprites;
            weapon.WeaponSprite = (allLoaded && sprites.Length > 0) ? sprites[0] : null;
        }
        else if (mode == WeaponMode.Laser)
        {
            // 雷射武器使用光束自身視覺（beam_core / 光暈），不需要 WeaponSprite
        }
        else if (!string.IsNullOrWhiteSpace(weapon.WeaponSpritePath))
        {
            Sprite sprite = Resources.Load<Sprite>(weapon.WeaponSpritePath);
            if (sprite != null)
                weapon.WeaponSprite = sprite;
            else
                Debug.LogWarning($"[WeaponTable] 武器「{weapon.Name}」的子彈圖 Resources 路徑 '{weapon.WeaponSpritePath}' 找不到。");
        }
        // WeaponSpritePath 留空且非動畫 = 隱形子彈（例如地刺，只靠 TrailEffectID 沿路種刺），不需飛行圖
    }

    // 雷射光暈素材路徑（相對 Resources/，不含副檔名）。光束本體已全參數化，不再需要 beam 貼圖。
    private const string BeamMuzzlePath = "Laser/laser_glow";
    private const string BeamImpactPath = "Laser/laser_impact";

    private void LoadBeamAssets(WeaponData weapon)
    {
        weapon.BeamMuzzleSprite = Resources.Load<Sprite>(BeamMuzzlePath);
        weapon.BeamImpactSprite = Resources.Load<Sprite>(BeamImpactPath);
    }
}
