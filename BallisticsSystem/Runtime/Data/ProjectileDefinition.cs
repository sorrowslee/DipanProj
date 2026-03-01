using UnityEngine;
using System.Collections.Generic;

namespace Sorrows.Ballistics
{
    [CreateAssetMenu(fileName = "NewProjectile", menuName = "Ballistics/Projectile Definition")]
    public class ProjectileDefinition : ScriptableObject
    {
        [Header("基礎屬性")]
        public float Speed = 10f;
        public float LifeTime = 3f;
        public int PierceCount = 0; // 穿透次數：0為不穿透

        [Header("反彈屬性")]
        public bool HasBounce;
        public int MaxBounces = 3;

        [Header("分裂屬性")]
        public bool HasSplit;
        public SplitTiming Timing;
        public int SplitCount = 3;
        public float SpreadAngle = 60f;
        public ProjectileDefinition SubProjectileData;

        public List<IBulletBehavior> CreateBehaviors()
        {
            var list = new List<IBulletBehavior>();
            if (HasBounce) list.Add(new BounceBehavior(MaxBounces));
            if (HasSplit) list.Add(new SplitBehavior(SubProjectileData, SplitCount, SpreadAngle, Timing));
            return list;
        }
    }

    public enum SplitTiming { OnSpawn, OnDeath, OnHit }
}