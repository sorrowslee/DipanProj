using UnityEngine;
using System;

namespace Sorrows.Ballistics
{
    public static class BallisticsEngine
    {
        public static BulletInstance Spawn(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null, Sprite bulletSprite = null, float spriteAngleOffset = 0f, Vector3 scale = default, Sprite[] animationSprites = null, float animFPS = 0f)
        {
            return Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit, bulletSprite, spriteAngleOffset, scale, animationSprites, animFPS);
        }

        internal static BulletInstance Internal_SpawnSplit(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null, Vector3 scale = default)
        {
            return Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit, null, 0f, scale);
        }

        private static BulletInstance Internal_Create(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers, LayerMask nonBounceLayers, Action<BulletInstance, GameObject, RaycastHit2D> onHit, Sprite bulletSprite, float spriteAngleOffset, Vector3 scale = default, Sprite[] animationSprites = null, float animFPS = 0f)
        {
            GameObject go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            BulletInstance instance = go.GetComponent<BulletInstance>();
            if (instance != null)
            {
                if (onHit != null) instance.OnBulletHitObject += onHit;

                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();

                if (animationSprites != null && animationSprites.Length > 0)
                {
                    instance.AnimationSprites = animationSprites;
                    instance.AnimFPS = animFPS;
                    if (sr != null) sr.sprite = animationSprites[0];
                }
                else if (bulletSprite != null)
                {
                    if (sr != null) sr.sprite = bulletSprite;
                }

                if (spriteAngleOffset != 0f)
                    instance.SpriteAngleOffset = spriteAngleOffset;

                // scale 在 OnSpawn 之前套用，確保分裂彈生成子彈時能讀到正確的縮放值
                if (scale != default)
                    go.transform.localScale = scale;

                instance.Velocity = direction.normalized * def.Speed;
                instance.Radius = def.Radius;
                instance.LifeTime = def.LifeTime;
                instance.CollisionMask = collisionMask;
                instance.PierceableLayers = pierceableLayers;
                instance.NonBounceLayers = nonBounceLayers;
                instance.PierceCount = def.PierceCount;

                if (instance.SpriteAngleOffset != 0f)
                {
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    go.transform.rotation = Quaternion.Euler(0, 0, angle + instance.SpriteAngleOffset);
                }
                
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
