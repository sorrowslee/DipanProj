using UnityEngine;
using System.Collections.Generic;
using Sorrows.Ballistics;

public enum BounceTarget { None, Environment, Enemy }

public class RecipeEntry
{
    public int ID;
    public string Name;
    public ProjectileData Data;
    public BounceTarget BounceTarget;
    public int SubRecipeID = -1;
}

public class RecipeManager : MonoBehaviour
{
    public TextAsset RecipeCSV;

    private Dictionary<int, RecipeEntry> _recipes = new Dictionary<int, RecipeEntry>();

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

        Debug.LogError($"Recipe ID {id} not found!");
        return null;
    }

    private void LoadRecipes()
    {
        if (RecipeCSV == null)
        {
            Debug.LogError("Recipe CSV is not assigned!");
            return;
        }

        string[] lines = RecipeCSV.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] v = lines[i].Split(',');
            if (v.Length < 14) continue;

            var entry = new RecipeEntry();
            entry.ID = int.Parse(v[0]);
            entry.Name = v[1].Trim();

            var data = new ProjectileData();
            data.Speed = float.Parse(v[2]);
            data.Radius = float.Parse(v[3]);
            data.LifeTime = float.Parse(v[4]);
            data.FireInterval = float.Parse(v[5]);
            data.RotationSpeed = float.Parse(v[6]);
            data.PierceCount = int.Parse(v[7]);

            int spreadCount = int.Parse(v[8]);
            float spreadAngle = float.Parse(v[9]);
            string splitTimingStr = v[10].Trim();
            string subRecipeStr = v[11].Trim();

            if (spreadCount > 1 && !string.IsNullOrEmpty(splitTimingStr))
            {
                data.HasSplit = true;
                data.SplitCount = spreadCount;
                data.SpreadAngle = spreadAngle;
                data.Timing = ParseSplitTiming(splitTimingStr);
            }

            if (!string.IsNullOrEmpty(subRecipeStr))
            {
                entry.SubRecipeID = int.Parse(subRecipeStr);
            }

            string bounceStr = v[12].Trim();
            entry.BounceTarget = ParseBounceTarget(bounceStr);

            int maxBounces = int.Parse(v[13].Trim());
            if (entry.BounceTarget != BounceTarget.None && maxBounces > 0)
            {
                data.HasBounce = true;
                data.MaxBounces = maxBounces;
            }

            entry.Data = data;
            _recipes[entry.ID] = entry;
        }

        Debug.Log($"Loaded {_recipes.Count} recipes from CSV.");
    }

    private void ResolveSubRecipes()
    {
        foreach (var kvp in _recipes)
        {
            var entry = kvp.Value;
            if (entry.SubRecipeID > 0 && _recipes.TryGetValue(entry.SubRecipeID, out RecipeEntry subEntry))
            {
                entry.Data.SubProjectileData = subEntry.Data;
            }
        }
    }

    private void AutoFillMissingSubRecipes()
    {
        foreach (var kvp in _recipes)
        {
            var data = kvp.Value.Data;
            if (!data.HasSplit || data.SubProjectileData != null) continue;

            var sub = new ProjectileData();
            sub.Speed = data.Speed;
            sub.Radius = data.Radius;
            sub.LifeTime = data.LifeTime;
            sub.FireInterval = data.FireInterval;
            sub.RotationSpeed = data.RotationSpeed;
            sub.PierceCount = data.PierceCount;
            sub.HasBounce = data.HasBounce;
            sub.MaxBounces = data.MaxBounces;
            data.SubProjectileData = sub;
        }
    }

    private static BounceTarget ParseBounceTarget(string value)
    {
        return value switch
        {
            "Environment" => BounceTarget.Environment,
            "Enemy" => BounceTarget.Enemy,
            _ => BounceTarget.None
        };
    }

    private static SplitTiming ParseSplitTiming(string value)
    {
        return value switch
        {
            "OnSpawn" => SplitTiming.OnSpawn,
            "OnHit" => SplitTiming.OnHit,
            "OnDeath" => SplitTiming.OnDeath,
            _ => SplitTiming.OnSpawn
        };
    }
}
