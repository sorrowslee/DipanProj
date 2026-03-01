using UnityEngine;
using System.Collections.Generic;

namespace Sorrows.Ballistics
{
    [CreateAssetMenu(fileName = "NewProjectile", menuName = "Ballistics/Projectile Definition")]
    public class ProjectileDefinition : ScriptableObject
    {
        [Header("基礎屬性")]
        public float Speed = 10f;
        public float Radius = 0.1f;
        public float LifeTime = 3f;
        public float RotationSpeed = 0f; // 🟢 旋轉速度 (度/秒)
        public float FireInterval = 0.2f; // 🟢 發射間隔 (秒)
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
            
            // 🟢 如果有設定旋轉速度，則加入旋轉行為
            if (RotationSpeed != 0) list.Add(new RotationBehavior(RotationSpeed));
            
            return list;
        }
    }

    public enum SplitTiming { OnSpawn, OnDeath, OnHit }
}