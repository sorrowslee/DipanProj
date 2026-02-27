using UnityEngine;
using System.Collections.Generic;

namespace Sorrows.Ballistics
{
    public enum SplitTiming { OnSpawn, OnHit }

    [CreateAssetMenu(fileName = "NewProjectile", menuName = "Ballistics/Projectile Definition")]
    public class ProjectileDefinition : ScriptableObject
    {
        [Header("基礎屬性")]
        public float Speed = 10f;
        public float LifeTime = 3f;

        [Header("反彈屬性")]
        public bool HasBounce = false;
        public int MaxBounces = 3;

        [Header("分裂屬性")]
        public bool HasSplit = false;
        public SplitTiming Timing = SplitTiming.OnHit;
        public int SplitCount = 3;
        public float SpreadAngle = 60f;
        public ProjectileDefinition SubProjectileData;

        public List<IBulletBehavior> CreateBehaviors()
        {
            List<IBulletBehavior> behaviors = new List<IBulletBehavior>();

            if (HasBounce)
                behaviors.Add(new BounceBehavior(MaxBounces));

            if (HasSplit && SubProjectileData != null)
                behaviors.Add(new SplitBehavior(SubProjectileData, SplitCount, SpreadAngle, Timing));

            return behaviors;
        }
    }
}