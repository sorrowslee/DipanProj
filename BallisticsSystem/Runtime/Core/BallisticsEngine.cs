using UnityEngine;

namespace Sorrows.Ballistics
{
    public static class BallisticsEngine
    {
        public static BulletInstance Spawn(ProjectileDefinition def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask mask)
        {
            return Internal_Create(def, prefab, position, direction, mask);
        }

        internal static void Internal_SpawnSplit(ProjectileDefinition def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask mask)
        {
            Internal_Create(def, prefab, position, direction, mask);
        }

        private static BulletInstance Internal_Create(ProjectileDefinition def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask mask)
        {
            GameObject go = Object.Instantiate(prefab, position, Quaternion.identity);
            BulletInstance instance = go.GetComponent<BulletInstance>();
            if (instance != null)
            {
                instance.Velocity = direction.normalized * def.Speed;
                instance.LifeTime = def.LifeTime;
                instance.CollisionMask = mask;
                
                foreach (var b in def.CreateBehaviors())
                {
                    instance.AddBehavior(b);
                }

                // 讓所有行為執行初始化
                foreach (var b in instance.GetBehaviors())
                {
                    b.OnSpawn(instance);
                }
            }
            return instance;
        }
    }
}