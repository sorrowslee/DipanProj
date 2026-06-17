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
            BeamStyle style, Color beamColor, float beamWidth,
            Sprite muzzleSprite, Sprite impactSprite,
            Action<LaserBeam, List<LaserBeam.BeamHit>> onTick,
            bool drawBeam = true)
        {
            var go = new GameObject("LaserBeam");
            // 渲染改為自繪 mesh：RequireComponent 會在 AddComponent<LaserBeam> 時自動補上 MeshFilter/MeshRenderer。
            // mesh 頂點用世界座標、transform 由 LaserBeam.Setup 歸零，故這裡不需要設位置。
            var beam = go.AddComponent<LaserBeam>();

            beam.Origin = origin;
            beam.AimDirection = aimDir;
            // 所見即所得：命中判定半徑 = 視覺半寬 = beamWidth / 2（mesh 半寬與 CircleCast 半徑共用同一值）。
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
            beam.DrawBeam = drawBeam;   // false = 火焰模式：不畫光束 mesh，路徑交給主遊戲沿路鋪火焰

            beam.Setup(style, beamColor, beamWidth, muzzleSprite, impactSprite);
            return beam;
        }

        /// <summary>
        /// 連鎖閃電視覺：生成一個「只渲染一條已算好折線、短命淡出後自毀」的 LaserBeam。
        /// 目標搜尋與傷害由主遊戲負責（本工廠不回報命中、不結算傷害），這裡只把雷射的折線 mesh 渲染當電弧視覺複用。
        /// </summary>
        /// <param name="points">世界座標折線：玩家→怪A→怪B…（可含鋸齒抖動點）。</param>
        /// <param name="life">閃一下的存活秒數（在這段時間內漸暗淡出）。</param>
        public static LaserBeam SpawnChainVisual(List<Vector2> points,
            BeamStyle style, Color beamColor, float beamWidth,
            Sprite muzzleSprite, Sprite impactSprite, float life)
        {
            var go = new GameObject("ChainLightning");
            var beam = go.AddComponent<LaserBeam>();
            // 所見即所得的半寬；不 march、不回報傷害（OnBeamDamageTick 保持 null）。
            beam.Radius = Mathf.Max(0.01f, beamWidth * 0.5f);
            beam.DrawBeam = true;
            beam.Setup(style, beamColor, beamWidth, muzzleSprite, impactSprite);
            beam.SetStaticPath(points, life);
            return beam;
        }

        public static BulletInstance Spawn(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null, Sprite bulletSprite = null, float spriteAngleOffset = 0f, Vector3 scale = default, Sprite[] animationSprites = null, float animFPS = 0f, Action<BulletInstance, Vector2> onTrailPoint = null)
        {
            // hideIfNoSprite=true：初始發射時若沒給圖 = 隱形子彈（地刺/火焰噴射器的隱形載體）。
            // 分裂子彈走 Internal_SpawnSplit（hideIfNoSprite=false），保留從母彈複製來的圖、不被清空。
            return Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit, bulletSprite, spriteAngleOffset, scale, animationSprites, animFPS, onTrailPoint, true);
        }

        internal static BulletInstance Internal_SpawnSplit(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers = default, LayerMask nonBounceLayers = default, Action<BulletInstance, GameObject, RaycastHit2D> onHit = null, Vector3 scale = default, Action<BulletInstance, Vector2> onTrailPoint = null)
        {
            return Internal_Create(def, prefab, position, direction, collisionMask, pierceableLayers, nonBounceLayers, onHit, null, 0f, scale, null, 0f, onTrailPoint);
        }

        private static BulletInstance Internal_Create(ProjectileData def, GameObject prefab, Vector2 position, Vector2 direction, LayerMask collisionMask, LayerMask pierceableLayers, LayerMask nonBounceLayers, Action<BulletInstance, GameObject, RaycastHit2D> onHit, Sprite bulletSprite, float spriteAngleOffset, Vector3 scale = default, Sprite[] animationSprites = null, float animFPS = 0f, Action<BulletInstance, Vector2> onTrailPoint = null, bool hideIfNoSprite = false)
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
                instance.TrailStep = def.TrailStep;

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
