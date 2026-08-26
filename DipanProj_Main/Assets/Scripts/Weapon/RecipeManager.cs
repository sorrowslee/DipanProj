using UnityEngine;
using System.Collections.Generic;
using Sorrows.Ballistics;
using Dipan.Data;

/// <summary>
/// 載入 <c>Assets/Data/RecipeTable.csv</c>（拖進 Inspector 的 <see cref="RecipeCSV"/>）。
///
/// <para>2026-08-26 起改成<b>依表頭名稱取值</b>（<see cref="CsvTable"/>），欄位可以任意分群重排、中間插新欄；
/// 每列交給 <see cref="RecipeEntry.FromFields"/> 建構，並用 <see cref="WeaponModeSpec"/> 做規格檢查——
/// 表上填錯會在 Console 直接看到「配方 N：某欄對某模式無效」。</para>
///
/// <para>資料類別在 <c>RecipeEntry.cs</c>；本檔只管「讀檔、印錯、二次解析 SubRecipeID」。</para>
/// </summary>
public class RecipeManager : MonoBehaviour
{
    public TextAsset RecipeCSV;

    private readonly Dictionary<int, RecipeEntry> _recipes = new Dictionary<int, RecipeEntry>();

    /// <summary>所有配方（唯讀）。給武器效果模擬面板「載入既有配方來改」用。</summary>
    public IReadOnlyDictionary<int, RecipeEntry> All => _recipes;

    void Awake()
    {
        LoadRecipes();
        ResolveSubRecipes();
        AutoFillMissingSubRecipes();
    }

    public RecipeEntry GetRecipe(int id)
    {
        if (_recipes.TryGetValue(id, out RecipeEntry entry))
            return entry;

        Debug.LogError($"[RecipeTable] 找不到配方 ID {id}。");
        return null;
    }

    /// <summary>
    /// 用「欄名 → 值」臨時建一列配方（**不會**登記進表；給武器效果模擬面板用）。
    /// 走的是與 CSV 完全相同的 <see cref="RecipeEntry.FromFields"/>，SubRecipeID 也會在這裡解析成參照。
    /// 問題清單（[Error]/[Warning] 開頭）回傳給呼叫端顯示。
    /// </summary>
    public RecipeEntry CreateTransient(IReadOnlyDictionary<string, string> fields, List<string> problems)
    {
        var e = RecipeEntry.FromFields(fields, problems);
        LinkSubRecipe(e);
        FillMissingSubRecipe(e);
        return e;
    }

    private void LoadRecipes()
    {
        _recipes.Clear();
        if (RecipeCSV == null)
        {
            Debug.LogError("[RecipeTable] Inspector 的 RecipeCSV 沒有指定。");
            return;
        }

        var table = CsvTable.Parse(RecipeCSV.text, "RecipeTable");
        table.Require("ID", "Name", "Mode");
        foreach (var err in table.Errors) Debug.LogError(err);

        // 表頭打錯字／多出來的欄：一次列出（值不會被讀到，等於白填）
        var unknown = table.UnknownColumns(WeaponModeSpec.ColumnNames(FieldTable.Recipe));
        if (unknown.Count > 0)
            Debug.LogWarning($"[RecipeTable] 表頭有程式不認得的欄位（會被忽略）：{string.Join(", ", unknown)}。可用欄位見 WeaponModeSpec。");

        var problems = new List<string>();
        foreach (var row in table.Rows)
        {
            problems.Clear();
            var entry = RecipeEntry.FromFields(row.ToDictionary(), problems);
            if (entry.ID <= 0)
            {
                Debug.LogError($"[RecipeTable] 第 {row.Line} 行的 ID 不是正整數，已略過。");
                continue;
            }
            if (_recipes.ContainsKey(entry.ID))
                Debug.LogError($"[RecipeTable] 配方 ID {entry.ID} 重複（第 {row.Line} 行），後者覆蓋前者。");

            foreach (var p in problems)
            {
                string msg = $"[RecipeTable] 配方 {entry.ID}「{entry.Name}」（第 {row.Line} 行）：{p}";
                if (p.StartsWith("[Error]")) Debug.LogError(msg); else Debug.LogWarning(msg);
            }
            _recipes[entry.ID] = entry;
        }

        Debug.Log($"[RecipeTable] 載入 {_recipes.Count} 筆配方。");
    }

    private void ResolveSubRecipes()
    {
        foreach (var kvp in _recipes) LinkSubRecipe(kvp.Value);
    }

    private void LinkSubRecipe(RecipeEntry entry)
    {
        if (entry.SubRecipeID <= 0) return;
        if (_recipes.TryGetValue(entry.SubRecipeID, out RecipeEntry subEntry))
        {
            entry.Data.SubProjectileData = subEntry.Data;
            entry.SubRecipe = subEntry;   // 保留子配方參考（落雷接連鎖時會讀 sub 的 Mode/ChainCount/ChainRadius）
        }
        else
        {
            Debug.LogError($"[RecipeTable] 配方 {entry.ID}「{entry.Name}」的 SubRecipeID={entry.SubRecipeID} 不存在。");
        }
    }

    private void AutoFillMissingSubRecipes()
    {
        foreach (var kvp in _recipes) FillMissingSubRecipe(kvp.Value);
    }

    /// <summary>會分裂但沒指定子配方 → 子彈繼承母彈屬性（不再分裂）。</summary>
    private static void FillMissingSubRecipe(RecipeEntry entry)
    {
        var data = entry.Data;
        if (!data.HasSplit || data.SubProjectileData != null) return;

        var sub = new ProjectileData();
        sub.Speed = data.Speed;
        sub.Radius = data.Radius;
        sub.LifeTime = data.LifeTime;
        sub.FireInterval = data.FireInterval;
        sub.RotationSpeed = data.RotationSpeed;
        sub.PierceCount = data.PierceCount;
        sub.HasBounce = data.HasBounce;
        sub.MaxBounces = data.MaxBounces;
        sub.HasHoming = data.HasHoming;
        sub.HomingTurnSpeed = data.HomingTurnSpeed;
        sub.IsOrbital = data.IsOrbital;
        sub.OrbitalRadius = data.OrbitalRadius;
        sub.OrbitalCount = data.OrbitalCount;
        sub.TrailStep = data.TrailStep;
        data.SubProjectileData = sub;
    }
}
