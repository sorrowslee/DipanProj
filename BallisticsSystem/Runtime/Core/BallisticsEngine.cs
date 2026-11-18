using UnityEngine;
using System;
using System.Collections.Generic;

namespace Sorrows.Ballistics
{
    public static class BallisticsEngine
    {
        /// <summary>
        /// 生成一道持續型雷射光束（純程式建構，不需要 prefab）。光束只算幾何與命中、不畫東西；
        /// 外觀由主遊戲讀 <see cref="LaserBeam.Points"/> 自己畫（DipanVfx 的 BeamVisual；火焰噴射器沿路鋪火焰）。
        /// 主遊戲負責每幀更新 beam.Origin / beam.AimDirection，並在 onTick 結算傷害。
        /// </summary>
        /// <param name="beamWidth">命中寬度（直徑）；命中判定半徑 = beamWidth / 2。</param>
        public static LaserBeam SpawnBeam(ProjectileData def, Vector2 origin, Vector2 aimDir,
            LayerMask collisionMask, LayerMask pierceableLayers, LayerMask nonBounceLayers,
            float beamWidth, Action<LaserBeam, List<LaserBeam.BeamHit>> onTick)
        {
            var go = new GameObject("LaserBeam");
            var beam = go.AddComponent<LaserBeam>();

            beam.Origin = origin;
            beam.AimDirection = aimDir;
            // 命中判定半徑 = beamWidth / 2（不使用配方 Radius；外觀寬度也從同一個 BeamWidth 推出，所見即所得）。
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

            beam.Setup();
            return beam;
        }

        public static BulletInstance Spawn(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null, Sprite bulletSprite = null, float spriteAngleOffset = 0f, Vector3 scale = default, Sprite[] animationSprites = null, float animFPS = 0f, Action<BulletInstance, Vector2> onTrailPoint = null, Func<IBulletBehavior> extraBehavior = null, bool flipYWhenMovingLeft = false, LayerMask homingLayers = default)
        {
            // hideIfNoSprite=true：初始發射時若沒給圖 = 隱形子彈（地刺/火焰噴射器的隱形載體）。
            // 分裂子彈走 Internal_SpawnSplit（hideIfNoSprite=false），保留從母彈複製來的圖、不被清空。
            return Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit, bulletSprite, spriteAngleOffset, scale, animationSprites, animFPS, onTrailPoint, true, extraBehavior, flipYWhenMovingLeft, homingLayers);
        }

        internal static BulletInstance Internal_SpawnSplit(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null, Vector3 scale = default, Action<BulletInstance, Vector2> onTrailPoint = null, LayerMask homingLayers = default)
        {
            // 母彈還在出生點（OnSpawn 分裂）→ 子彈繼承發射端掛的額外行為（平行彈的 LaneBehavior），否則整排只有母彈會散開。
            // OnHit／OnDeath 分裂發生在遠處，不繼承。
            var parent = prefab != null ? prefab.GetComponent<BulletInstance>() : null;
            var inherit = (parent != null && parent.IsSpawning) ? parent.SpawnExtraBehavior : null;
            return Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit, null, 0f, scale, null, 0f, onTrailPoint, false, inherit, false, homingLayers);
        }

        private static BulletInstance Internal_Create(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers, LayerMask nonBounceLayers, Action<BulletInstance, GameObject, RaycastHit2D> onHit, Sprite bulletSprite, float spriteAngleOffset, Vector3 scale = default, Sprite[] animationSprites = null, float animFPS = 0f, Action<BulletInstance, Vector2> onTrailPoint = null, bool hideIfNoSprite = false, Func<IBulletBehavior> extraBehavior = null, bool flipYWhenMovingLeft = false, LayerMask homingLayers = default)
        {
            GameObject go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            BulletInstance instance = go.GetComponent<BulletInstance>();
            if (instance != null)
            {
                if (onHit != null) instance.OnBulletHitObject += onHit;
                if (onTrailPoint != null) instance.OnTrailPoint += onTrailPoint;

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
                else if (hideIfNoSprite && sr != null)
                {
                    // 初始發射且沒給任何圖 = 隱形子彈（地刺/火焰：載體不顯示，靠沿路特效呈現）。
                    // 分裂子彈不走這裡（hideIfNoSprite=false），保留從母彈複製來的圖，避免整排分裂彈消失。
                    sr.sprite = null;
                }

                if (spriteAngleOffset != 0f)
                    instance.SpriteAngleOffset = spriteAngleOffset;
                // 分裂子彈傳 false：保留 Instantiate 從母彈複製來的值（與 SpriteAngleOffset 同理）
                if (flipYWhenMovingLeft)
                    instance.FlipYWhenMovingLeft = true;

                // scale 在 OnSpawn 之前套用，確保分裂彈生成子彈時能讀到正確的縮放值
                if (scale != default)
                    go.transform.localScale = scale;

                instance.Velocity = direction.normalized * def.Speed;
                instance.Radius = def.Radius;
                instance.LifeTime = def.LifeTime;
                instance.CollisionMask = collisionMask;
                instance.PierceableLayers = pierceableLayers;
                instance.NonBounceLayers = nonBounceLayers;
                instance.HomingLayers = homingLayers.value != 0 ? homingLayers : pierceableLayers;   // 沒給＝舊行為
                instance.PierceCount = def.PierceCount;
                instance.TrailStep = def.TrailStep;

                if (instance.SpriteAngleOffset != 0f)
                {
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    go.transform.rotation = Quaternion.Euler(0, 0, angle + instance.SpriteAngleOffset);
                    if (instance.FlipYWhenMovingLeft) instance.ApplyFacingFlip(direction.x);
                }
                
                foreach (var b in def.CreateBehaviors())
                {
                    instance.AddBehavior(b);
                }
                // 發射端額外行為排在配方行為之後：分裂（OnSpawn）先用純前進方向產子彈，平行的側向速度才加上去
                instance.SpawnExtraBehavior = extraBehavior;
                if (extraBehavior != null)
                {
                    var extra = extraBehavior();
                    if (extra != null) instance.AddBehavior(extra);
                }

                instance.IsSpawning = true;
                foreach (var b in instance.GetBehaviors())
                {
                    b.OnSpawn(instance);
                }
                instance.IsSpawning = false;

                instance.CheckSpawnOverlap();
            }
            return instance;
        }
    }
}
