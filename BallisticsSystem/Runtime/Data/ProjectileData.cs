using System.Collections.Generic;

namespace Sorrows.Ballistics
{
    public class ProjectileData
    {
        public float Speed = 10f;
        public float Radius = 0.1f;
        /// <summary>存活秒數；&lt; 0（例如 -1）表示不因時間銷毀。</summary>
        public float LifeTime = 3f;
        public float RotationSpeed = 0f;
        public float FireInterval = 0.2f;
        /// <summary>穿透次數；&lt; 0（例如 -1）表示無限穿透。</summary>
        public int PierceCount = 0;

        public bool HasBounce;
        public int MaxBounces = 3;

        public bool HasHoming;
        public float HomingTurnSpeed = 180f;

        public bool HasSplit;
        public SplitTiming Timing;
        public int SplitCount = 3;
        public float SpreadAngle = 60f;
        public ProjectileData SubProjectileData;

        public bool IsOrbital;
        public float OrbitalRadius = 2f;
        public int OrbitalCount = 3;

        public List<IBulletBehavior> CreateBehaviors()
        {
            var list = new List<IBulletBehavior>();
            if (HasBounce) list.Add(new BounceBehavior(MaxBounces));
            if (HasSplit) list.Add(new SplitBehavior(SubProjectileData, SplitCount, SpreadAngle, Timing));
            if (RotationSpeed != 0) list.Add(new RotationBehavior(RotationSpeed));
            if (HasHoming) list.Add(new HomingBehavior(HomingTurnSpeed));
            if (IsOrbital) list.Add(new OrbitalBehavior(OrbitalRadius, Speed));
            return list;
        }
    }

    public enum SplitTiming { OnSpawn, OnDeath, OnHit }
}
