using UnityEngine;
using System.Collections.Generic;
using Sorrows.Ballistics;

public enum BounceTarget { None, Environment, Enemy }
public enum GroundEffectTrigger { OnSpawn, OnHit, OnDeath }
public enum GroundEffectHitTarget { Enemy, Environment, Any, Ground }
public enum LaunchSource { Player, Offscreen }

public class RecipeEntry
{
    public int ID;
    public string Name;
    public ProjectileData Data;
    public BounceTarget BounceTarget;
    public int SubRecipeID = -1;
    public bool BlockedByEnvironment = true;
    public int GroundEffectID = 0;
    public GroundEffectTrigger GroundEffectTrigger = GroundEffectTrigger.OnHit;
    public GroundEffectHitTarget GroundEffectHitTarget = GroundEffectHitTarget.Enemy;
    public LaunchSource LaunchSource = LaunchSource.Player;
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
            if (v.Length < 14) continue; // column 14 (HomingTurnSpeed) 為選填，向下相容

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

            // SplitCount / SpreadAngle 始終記錄到 ProjectileData，方便拋物線等其他發射模式直接讀取；
            // HasSplit 仍只控制是否組裝 SplitBehavior（需 SpreadCount > 1 + 有 SplitTiming）
            data.SplitCount = Mathf.Max(1, spreadCount);
            data.SpreadAngle = spreadAngle;

            if (spreadCount > 1 && !string.IsNullOrEmpty(splitTimingStr))
            {
                data.HasSplit = true;
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

            if (v.Length >= 15)
            {
                float homingTurnSpeed = float.Parse(v[14].Trim());
                if (homingTurnSpeed > 0f)
                {
                    data.HasHoming = true;
                    data.HomingTurnSpeed = homingTurnSpeed;
                }
            }

            if (v.Length >= 16 && !string.IsNullOrWhiteSpace(v[15]))
            {
                int isOrbital = int.Parse(v[15].Trim());
                if (isOrbital > 0)
                {
                    data.IsOrbital = true;
                    data.OrbitalRadius = (v.Length >= 17 && !string.IsNullOrWhiteSpace(v[16])) ? float.Parse(v[16].Trim()) : 2f;
                    data.OrbitalCount = (v.Length >= 18 && !string.IsNullOrWhiteSpace(v[17])) ? int.Parse(v[17].Trim()) : 3;
                }
            }

            // 留空或 1 = 會被地形擋下（向下相容預設）；0 = 子彈穿透地形不被銷毀。
            entry.BlockedByEnvironment = true;
            if (v.Length >= 19 && !string.IsNullOrWhiteSpace(v[18]))
            {
                entry.BlockedByEnvironment = int.Parse(v[18].Trim()) != 0;
            }

            // 地面特效鏈式觸發：留空 / 0 = 不觸發；> 0 = 引用 GroundEffectTable 對應 ID。
            entry.GroundEffectID = 0;
            if (v.Length >= 20 && !string.IsNullOrWhiteSpace(v[19]))
            {
                entry.GroundEffectID = int.Parse(v[19].Trim());
            }

            entry.GroundEffectTrigger = GroundEffectTrigger.OnHit;
            if (v.Length >= 21 && !string.IsNullOrWhiteSpace(v[20]))
            {
                entry.GroundEffectTrigger = ParseGroundEffectTrigger(v[20].Trim());
            }

            // 地面特效命中過濾：留空 / Enemy = 只有打到怪物才觸發；Environment = 只有打到障礙物才觸發；Any = 兩者都觸發；Ground = 拋物線最終落地時觸發。
            entry.GroundEffectHitTarget = GroundEffectHitTarget.Enemy;
            if (v.Length >= 22 && !string.IsNullOrWhiteSpace(v[21]))
            {
                entry.GroundEffectHitTarget = ParseGroundEffectHitTarget(v[21].Trim());
            }

            // 拋物線武器：留空或 0 = 一般彈道；1 = 啟用拋物線（與 IsOrbital 互斥）
            if (v.Length >= 23 && !string.IsNullOrWhiteSpace(v[22]))
            {
                int isParabolic = int.Parse(v[22].Trim());
                if (isParabolic > 0)
                {
                    data.IsParabolic = true;
                    data.ArcHeight = (v.Length >= 24 && !string.IsNullOrWhiteSpace(v[23])) ? float.Parse(v[23].Trim()) : 2f;
                }
            }

            // 發射來源：留空或 Player = 從玩家位置發射；Offscreen = 從攝影機視野外隨機方向飛入
            entry.LaunchSource = LaunchSource.Player;
            if (v.Length >= 25 && !string.IsNullOrWhiteSpace(v[24]))
            {
                entry.LaunchSource = ParseLaunchSource(v[24].Trim());
            }

            // 拋物線專用：落點隨機半徑（世界單位），實際落點 = 目標 + Random.insideUnitCircle * 半徑
            if (v.Length >= 26 && !string.IsNullOrWhiteSpace(v[25]))
            {
                data.LandingScatterRadius = float.Parse(v[25].Trim());
            }

            // 雷射光束：留空或 0 = 一般彈道；1 = 啟用持續型雷射（與 IsOrbital / IsParabolic 互斥）
            if (v.Length >= 27 && !string.IsNullOrWhiteSpace(v[26]))
            {
                int isLaser = int.Parse(v[26].Trim());
                if (isLaser > 0)
                {
                    data.IsLaser = true;
                    data.DotInterval = (v.Length >= 28 && !string.IsNullOrWhiteSpace(v[27])) ? float.Parse(v[27].Trim()) : 0.5f;
                    data.BeamRange = (v.Length >= 29 && !string.IsNullOrWhiteSpace(v[28])) ? float.Parse(v[28].Trim()) : 20f;
                }
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
            sub.HasHoming = data.HasHoming;
            sub.HomingTurnSpeed = data.HomingTurnSpeed;
            sub.IsOrbital = data.IsOrbital;
            sub.OrbitalRadius = data.OrbitalRadius;
            sub.OrbitalCount = data.OrbitalCount;
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

    private static GroundEffectTrigger ParseGroundEffectTrigger(string value)
    {
        return value switch
        {
            "OnSpawn" => GroundEffectTrigger.OnSpawn,
            "OnHit" => GroundEffectTrigger.OnHit,
            "OnDeath" => GroundEffectTrigger.OnDeath,
            _ => GroundEffectTrigger.OnHit
        };
    }

    private static GroundEffectHitTarget ParseGroundEffectHitTarget(string value)
    {
        return value switch
        {
            "Environment" => GroundEffectHitTarget.Environment,
            "Any" => GroundEffectHitTarget.Any,
            "Ground" => GroundEffectHitTarget.Ground,
            "Enemy" => GroundEffectHitTarget.Enemy,
            _ => GroundEffectHitTarget.Enemy
        };
    }

    private static LaunchSource ParseLaunchSource(string value)
    {
        return value switch
        {
            "Offscreen" => LaunchSource.Offscreen,
            "Player" => LaunchSource.Player,
            _ => LaunchSource.Player
        };
    }
}
