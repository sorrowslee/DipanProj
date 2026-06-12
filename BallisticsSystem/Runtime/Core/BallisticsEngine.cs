using UnityEngine;
using System;
using System.Collections.Generic;

namespace Sorrows.Ballistics
{
    public static class BallisticsEngine
    {
        /// <summary>
        /// 生成一道持續型雷射光束（純程式建構，不需要 prefab）。
        /// 主遊戲負責每幀更新 beam.Origin / beam.AimDirection，並在 onTick 結算傷害。
        /// </summary>
        public static LaserBeam SpawnBeam(ProjectileData def, Vector2 origin, Vector2 aimDir,
            LayerMask collisionMask, LayerMask pierceableLayers, LayerMask nonBounceLayers,
            Texture2D beamTex, Color beamColor, float beamWidth, float scrollSpeed,
            Sprite muzzleSprite, Sprite impactSprite,
            Action<LaserBeam, List<LaserBeam.BeamHit>> onTick)
        {
            var go = new GameObject("LaserBeam");
            // 渲染改為自繪 mesh：RequireComponent 會在 AddComponent<LaserBeam> 時自動補上 MeshFilter/MeshRenderer。
            // mesh 頂點用世界座標、transform 由 LaserBeam.Setup 歸零，故這裡不需要設位置。
            var beam = go.AddComponent<LaserBeam>();

            beam.Origin = origin;
            beam.AimDirection = aimDir;
            // 所見即所得：命中判定寬度直接由視覺寬度 BeamWidth 決定
            //（LineRenderer 寬度 = beamWidth = 直徑，CircleCast 半徑 = beamWidth / 2，掃出的命中帶寬剛好 = 視覺寬度）。
            // 不再使用配方 Radius，避免「視覺寬度」「命中寬度」兩個欄位互相打架。
            beam.Radius = Mathf.Max(0.01f, beamWidth * 0.5f);
            beam.BeamRange = def.BeamRange;
            beam.PierceCount = def.PierceCount;
            beam.HasBounce = def.HasBounce;
            beam.MaxBounces = def.MaxBounces;
            beam.HasHoming = def.HasHoming;
            beam.HomingTurnSpeed = def.HomingTurnSpeed;
            beam.DotInterval = def.DotInterval;
            beam.CollisionMask = collisionMask;
            beam.PierceableLayers = pierceableLayers;
            beam.NonBounceLayers = nonBounceLayers;
            beam.OnBeamDamageTick = onTick;

            beam.Setup(beamTex, beamColor, beamWidth, scrollSpeed, muzzleSprite, impactSprite);
            return beam;
        }

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
