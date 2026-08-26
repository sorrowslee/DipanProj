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
        /// <summary>拋物線專用：飛行秒數——不論遠近都飛這麼久才落地（2026-08-26 起獨立欄位，原本借用 Speed）。</summary>
        public float FlightTime = 1f;
        public float ArcHeight = 2f;
        /// <summary>拋物線專用：落點隨機半徑（世界單位）。實際落點 = 目標點 + Random.insideUnitCircle * 此半徑。</summary>
        public float LandingScatterRadius = 0f;

        // ── 雷射光束（持續掃射型）。與 IsOrbital / IsParabolic 互斥。 ──
        public bool IsLaser;
        /// <summary>傷害節拍（秒）：光束每 N 秒對當下掃到的所有目標各結算一次傷害。&lt;= 0 視為每幀結算。</summary>
        public float DotInterval = 0.5f;
        /// <summary>光束最大射程（世界單位）。Speed / LifeTime 對光束無意義，改用此欄位限制長度。</summary>
        public float BeamRange = 20f;

        /// <summary>軌跡點間距（世界單位）：&gt; 0 時，子彈每飛 TrailStep 距離就觸發一次 OnTrailPoint（主遊戲沿路種特效，如地刺）。0 = 無軌跡。</summary>
        public float TrailStep = 0f;

        public List<IBulletBehavior> CreateBehaviors()
        {
            var list = new List<IBulletBehavior>();
            if (HasBounce) list.Add(new BounceBehavior(MaxBounces));
            if (HasSplit) list.Add(new SplitBehavior(SubProjectileData, SplitCount, SpreadAngle, Timing));
            if (RotationSpeed != 0) list.Add(new RotationBehavior(RotationSpeed));
            if (HasHoming) list.Add(new HomingBehavior(HomingTurnSpeed));
            if (IsOrbital) list.Add(new OrbitalBehavior(OrbitalRadius, Speed));
            // 拋物線：FlightTime = 飛行秒數，不論遠近都飛這麼久才落地（Speed 對拋物線無意義）
            if (IsParabolic) list.Add(new ParabolicBehavior(ArcHeight, FlightTime));
            return list;
        }
    }

    public enum SplitTiming { OnSpawn, OnDeath, OnHit }
}
