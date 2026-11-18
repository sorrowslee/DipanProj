using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sorrows.Ballistics
{
    /// <summary>
    /// 持續掃射型雷射光束。與 BulletInstance（會移動的點）不同，光束是「當下就存在的一條線」，
    /// 由 line-march（逐段行進）每幀重算路徑，讓追蹤(彎曲)、反彈(折線)、穿透、射程從同一個迴圈長出來。
    ///
    /// 2026-11-18 起<b>不畫任何東西</b>：外觀由主遊戲依武器 BeamFxID 掛 DipanVfx 的 <c>BeamVisual</c>，
    /// 讀 <see cref="Points"/> 畫（見 readme/DIPAN_VFX.md §4d）；火焰噴射器則沿 Points 鋪火焰。
    ///
    /// 邊界規範：本元件只負責幾何 + 回報命中，<b>絕不結算傷害</b>。
    /// 每隔 DotInterval 秒透過 OnBeamDamageTick 把「當下掃到的目標」回報給主遊戲，由主遊戲算傷害。
    /// </summary>
    public class LaserBeam : MonoBehaviour
    {
        public struct BeamHit
        {
            public GameObject Target;
            public Vector2 Point;
        }

        // ── 由 BallisticsEngine.SpawnBeam 設定的設定值 ──
        public Vector2 Origin;            // 砲口位置（玩家每幀更新）
        public Vector2 AimDirection;      // 初始瞄準方向（玩家每幀更新為「玩家→滑鼠」）
        public float Radius = 0.2f;       // 命中判定半徑（＝武器 BeamWidth / 2）
        public float BeamRange = 20f;     // 最大射程
        public int PierceCount = 0;       // 0=打到第一個就停；-1=無限穿透；>0=穿透 N 個
        public bool HasBounce;
        public int MaxBounces = 3;
        public bool HasHoming;
        public float HomingTurnSpeed = 180f;
        public float DotInterval = 0.5f;

        public LayerMask CollisionMask;
        public LayerMask PierceableLayers;
        public LayerMask NonBounceLayers;

        /// <summary>當前光束折線路徑（世界座標、唯讀）。主遊戲的光束外觀（BeamVisual）與火焰噴射器都讀這份。每幀在 Update 重算，外觀在 LateUpdate 讀 ⇒ 不落後一幀。</summary>
        public IReadOnlyList<Vector2> Points => _points;

        /// <summary>每隔 DotInterval 秒回報一次當下命中清單（主遊戲據此結算傷害 / 地面特效 / OnHit 分裂）。</summary>
        public Action<LaserBeam, List<BeamHit>> OnBeamDamageTick;

        /// <summary>
        /// 每隔 DotInterval 秒回報一次撞到的「環境」碰撞清單（牆 / 可破壞地上物等阻擋層）。
        /// 與 <see cref="OnBeamDamageTick"/> 分開：環境不是「敵人命中」，主遊戲可只用它扣可破壞物的血，
        /// 而不會在每面牆上噴擊中特效或觸發 OnHit 分裂。
        /// </summary>
        public Action<LaserBeam, List<BeamHit>> OnBeamEnvironmentTick;

        // homing 把 HomingTurnSpeed(度/秒) 換算成曲率時假定的名目光速；與既有追蹤彈手感對齊
        private const float NominalBeamSpeed = 20f;
        private const float HomingStep = 0.3f;        // 追蹤時的行進步長（越小曲線越平滑、越耗）
        private const float HomingAcquireRadius = 50f;
        private const int MaxMarchSteps = 2048;
        // CSV 以 BeamRange=-1 表示延伸到實用安全距離；反射次數仍完全服從 MaxBounces。
        private const float InfiniteBeamDistance = 200f;

        private readonly List<Vector2> _points = new List<Vector2>(64);
        private readonly List<BeamHit> _hits = new List<BeamHit>(16);
        private readonly HashSet<int> _hitSet = new HashSet<int>();
        private readonly List<BeamHit> _envHits = new List<BeamHit>(8);
        private readonly HashSet<int> _envHitSet = new HashSet<int>();
        private float _dotTimer;

        /// <summary>設定完所有欄位後呼叫一次（讓第一幀就結算一次傷害）。</summary>
        public void Setup()
        {
            _dotTimer = DotInterval;
        }

        private void Update()
        {
            MarchBeam();   // 重算路徑與命中
            TickDamage();
        }

        // ── 逐段行進：把追蹤/反彈/穿透/射程收斂進同一迴圈，產出折線頂點與命中清單 ──
        private void MarchBeam()
        {
            _points.Clear();
            _hits.Clear();
            _hitSet.Clear();
            _envHits.Clear();
            _envHitSet.Clear();

            Vector2 pos = Origin;
            Vector2 dir = AimDirection.sqrMagnitude > 0.0001f ? AimDirection.normalized : Vector2.right;
            _points.Add(pos);

            float remaining = BeamRange < 0f ? InfiniteBeamDistance : Mathf.Max(0.01f, BeamRange);
            int pierceLeft = PierceCount;
            int bouncesLeft = HasBounce ? Mathf.Max(0, MaxBounces) : 0;
            float step = HasHoming ? HomingStep : remaining;
            int safety = 0;

            // 牆（阻擋/反彈層）用細射線精準偵測；敵人（可穿透層）用粗圓掃描吃光束寬度。
            // 分開處理，徹底解決「厚光束撞薄牆」的掠射偵測不到 / 起點重疊等問題。
            int wallMask = CollisionMask.value & ~PierceableLayers.value;
            int enemyMask = CollisionMask.value & PierceableLayers.value;

            // 貼身敵人補抓：本專案 Physics2D.queriesStartInColliders = false，
            // 從砲口出發的 CircleCast 會「忽略重疊在光束起點的碰撞體」→ 怪物貼在玩家身上時雷射打不到。
            // 在砲口做一次 OverlapCircle 把這些怪補進命中清單（純記錄傷害、不擋光束、不改全域物理設定）。
            if (enemyMask != 0)
            {
                Collider2D[] muzzleHits = Physics2D.OverlapCircleAll(Origin, Radius, enemyMask);
                for (int e = 0; e < muzzleHits.Length; e++)
                {
                    int mid = muzzleHits[e].gameObject.GetInstanceID();
                    if (_hitSet.Contains(mid)) continue;
                    _hits.Add(new BeamHit { Target = muzzleHits[e].gameObject, Point = muzzleHits[e].transform.position });
                    _hitSet.Add(mid);
                    if (pierceLeft > 0) pierceLeft--;
                }
            }

            while (remaining > 0.0001f && safety++ < MaxMarchSteps)
            {
                float segLen = Mathf.Min(step, remaining);

                if (HasHoming)
                    dir = ApplyHoming(pos, dir, segLen);

                // 1) 細射線找牆：決定這一段最多能走多遠
                RaycastHit2D wall = (wallMask != 0) ? Physics2D.Raycast(pos, dir, segLen, wallMask) : default;
                float travel = (wall.collider != null) ? wall.distance : segLen;

                // 2) 粗圓掃描在 travel 內收集敵人命中（吃光束寬度）；穿透用盡則停在該敵人
                bool stoppedAtEnemy = false;
                Vector2 enemyStopPoint = pos;
                if (enemyMask != 0)
                {
                    RaycastHit2D[] eHits = Physics2D.CircleCastAll(pos, Radius, dir, travel, enemyMask);
                    for (int e = 0; e < eHits.Length; e++)
                    {
                        int eid = eHits[e].collider.gameObject.GetInstanceID();
                        if (_hitSet.Contains(eid)) continue;
                        _hits.Add(new BeamHit { Target = eHits[e].collider.gameObject, Point = eHits[e].point });
                        _hitSet.Add(eid);
                        if (pierceLeft == 0) { stoppedAtEnemy = true; enemyStopPoint = eHits[e].point; break; }
                        if (pierceLeft > 0) pierceLeft--;
                    }
                }

                // 3) 穿透用盡撞到敵人 → 停在該敵人
                if (stoppedAtEnemy)
                {
                    _points.Add(enemyStopPoint);
                    break;
                }

                // 4) 撞到牆 → 可反彈就折射，否則停（細射線命中點精準在牆面）
                if (wall.collider != null)
                {
                    int wallLayer = wall.collider.gameObject.layer;
                    bool canBounce = bouncesLeft > 0 && ((1 << wallLayer) & NonBounceLayers.value) == 0;

                    // 轉角頂點放「精準的牆面命中點」即可。貼牆的視覺由 RenderBeam 的轉角延伸負責
                    //（相鄰段沿各自方向往牆內延伸半寬、重疊 → 亮核確實補到牆面，且延伸落在畫面外）。
                    _points.Add(wall.point);

                    // 回報環境命中（牆／可破壞地上物），主遊戲據此扣可破壞物的血。每段去重一次。
                    int wid = wall.collider.gameObject.GetInstanceID();
                    if (_envHitSet.Add(wid))
                        _envHits.Add(new BeamHit { Target = wall.collider.gameObject, Point = wall.point });

                    if (canBounce)
                    {
                        pos = wall.point + wall.normal * 0.05f;
                        dir = Vector2.Reflect(dir, wall.normal).normalized;
                        bouncesLeft--;
                        remaining -= wall.distance;
                        continue;
                    }

                    break;
                }

                // 5) 這段沒撞到牆 → 前進
                pos += dir * segLen;
                remaining -= segLen;
                _points.Add(pos);
                if (!HasHoming) break; // 直射：一段到底
            }

            if (_points.Count < 2)
                _points.Add(Origin + dir * 0.1f);
        }

        private Vector2 ApplyHoming(Vector2 pos, Vector2 dir, float segLen)
        {
            Transform target = AcquireNearestTarget(pos);
            if (target == null) return dir;

            Vector2 toTarget = (Vector2)target.position - pos;
            if (toTarget.sqrMagnitude < 0.0001f) return dir;

            float curAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float tgtAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            // HomingTurnSpeed(度/秒) → 以名目光速換算成「這一步可轉幾度」，手感與既有追蹤彈一致
            float maxTurn = HomingTurnSpeed * (segLen / NominalBeamSpeed);
            float newAngle = Mathf.MoveTowardsAngle(curAngle, tgtAngle, maxTurn);
            float rad = newAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private Transform AcquireNearestTarget(Vector2 from)
        {
            if (PierceableLayers == 0) return null;

            Collider2D[] cols = Physics2D.OverlapCircleAll(from, HomingAcquireRadius, PierceableLayers);
            float bestDist = float.MaxValue;
            Transform best = null;
            foreach (var col in cols)
            {
                // 已被本光束記錄過的目標不再吸引轉向 → 讓光束彎向「下一個」敵人
                if (_hitSet.Contains(col.gameObject.GetInstanceID())) continue;
                float d = ((Vector2)col.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = col.transform; }
            }
            return best;
        }

        private void TickDamage()
        {
            if (OnBeamDamageTick == null && OnBeamEnvironmentTick == null) return;

            _dotTimer += Time.deltaTime;

            if (DotInterval <= 0f)
            {
                FireTick();
                return;
            }

            if (_dotTimer >= DotInterval)
            {
                _dotTimer -= DotInterval;
                FireTick();
            }
        }

        private void FireTick()
        {
            if (OnBeamDamageTick != null && _hits.Count > 0) OnBeamDamageTick(this, _hits);
            if (OnBeamEnvironmentTick != null && _envHits.Count > 0) OnBeamEnvironmentTick(this, _envHits);
        }
    }
}
