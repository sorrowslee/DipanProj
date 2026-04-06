using UnityEngine;
using System;

namespace Sorrows.Ballistics
{
    public static class BallisticsEngine
    {
        public static BulletInstance Spawn(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null, Sprite bulletSprite = null)
        {
            return Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit, bulletSprite);
        }

        internal static void Internal_SpawnSplit(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null)
        {
            Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit, null);
        }

        private static BulletInstance Internal_Create(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers, LayerMask nonBounceLayers, Action<BulletInstance, GameObject, RaycastHit2D> onHit, Sprite bulletSprite)
        {
            GameObject go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            BulletInstance instance = go.GetComponent<BulletInstance>();
            if (instance != null)
            {
                if (onHit != null) instance.OnBulletHitObject += onHit;

                if (bulletSprite != null)
                {
                    SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sprite = bulletSprite;
                }

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

                instance.CheckSpawnOverlap();
            }
            return instance;
        }
    }
}
