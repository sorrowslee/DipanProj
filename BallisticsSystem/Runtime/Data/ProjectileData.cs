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

        public bool IsParabolic;
        public float ArcHeight = 2f;
        /// <summary>拋物線專用：落點隨機半徑（世界單位）。實際落點 = 目標點 + Random.insideUnitCircle * 此半徑。</summary>
        public float LandingScatterRadius = 0f;

        public List<IBulletBehavior> CreateBehaviors()
        {
            var list = new List<IBulletBehavior>();
            if (HasBounce) list.Add(new BounceBehavior(MaxBounces));
            if (HasSplit) list.Add(new SplitBehavior(SubProjectileData, SplitCount, SpreadAngle, Timing));
            if (RotationSpeed != 0) list.Add(new RotationBehavior(RotationSpeed));
            if (HasHoming) list.Add(new HomingBehavior(HomingTurnSpeed));
            if (IsOrbital) list.Add(new OrbitalBehavior(OrbitalRadius, Speed));
            // 拋物線：Speed 欄位重新解讀為「飛行時間（秒）」，Speed=1 表示無論遠近都飛 1 秒抵達
            if (IsParabolic) list.Add(new ParabolicBehavior(ArcHeight, Speed));
            return list;
        }
    }

    public enum SplitTiming { OnSpawn, OnDeath, OnHit }
}
