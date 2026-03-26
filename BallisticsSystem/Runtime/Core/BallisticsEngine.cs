using UnityEngine;
using System;

namespace Sorrows.Ballistics
{
    public static class BallisticsEngine
    {
        // 🟢 修改：增加 Action 參數
        public static BulletInstance Spawn(ProjectileDefinition def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null)
        {
            return Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit);
        }

        internal static void Internal_SpawnSplit(ProjectileDefinition def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null)
        {
            Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit);
        }

        private static BulletInstance Internal_Create(ProjectileDefinition def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers, LayerMask nonBounceLayers, Action<BulletInstance, GameObject, RaycastHit2D> onHit)
        {
            GameObject go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            BulletInstance instance = go.GetComponent<BulletInstance>();
            if (instance != null)
            {
                // 關鍵：在任何行為執行前先訂閱事件，防止 OnSpawn 分裂彈漏掉訊息
                if (onHit != null) instance.OnBulletHitObject += onHit;

                instance.Velocity = direction.normalized * def.Speed;
                instance.Radius = def.Radius;
                instance.LifeTime = def.LifeTime;
                instance.CollisionMask = collisionMask;
                instance.PierceableLayers = pierceableLayers;
                instance.NonBounceLayers = nonBounceLayers;
                instance.PierceCount = def.PierceCount;
                
                foreach (var b in def.CreateBehaviors())
                {
                    instance.AddBehavior(b);
                }

                foreach (var b in instance.GetBehaviors())
                {
                    b.OnSpawn(instance);
                }

                // 生成時做一次近距離檢查，處理 CircleCast 起點在 Collider 內部時偵測不到的情況
                instance.CheckSpawnOverlap();
            }
            return instance;
        }
    }
}