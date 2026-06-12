using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sorrows.Ballistics
{
    /// <summary>
    /// 持續掃射型雷射光束。與 BulletInstance（會移動的點）不同，光束是「當下就存在的一條線」，
    /// 由 line-march（逐段行進）每幀重算路徑，讓追蹤(彎曲)、反彈(折線)、穿透、射程從同一個迴圈長出來。
    ///
    /// 渲染採「自繪 mesh」：每段一個四邊形，轉角依夾角延伸讓相鄰段重疊補滿凸角。
    /// 這是刻意不用 LineRenderer —— 它的轉角接縫對「厚光束＋尖反彈」無解（圓角→接縫內縮離牆遠；
    /// 不圓角→尖角把某段寬度擠扁）。自繪 mesh 由我們指定每個頂點，亮核與寬度完全可控。
    ///
    /// 邊界規範：本元件只負責幾何 + 渲染 + 回報命中，<b>絕不結算傷害</b>。
    /// 每隔 DotInterval 秒透過 OnBeamDamageTick 把「當下掃到的目標」回報給主遊戲，由主遊戲算傷害。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
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
        public float Radius = 0.2f;       // 命中判定半徑（同時作為視覺半寬）
        public float BeamRange = 20f;     // 最大射程
        public int PierceCount = 0;       // 0=打到第一個就停；-1=無限穿透；>0=穿透 N 個
        public bool HasBounce;
        public int MaxBounces = 3;
        public bool HasHoming;
        public float HomingTurnSpeed = 180f;
        public float DotInterval = 0.5f;
        public float ScrollSpeed = 0f;

        public LayerMask CollisionMask;
        public LayerMask PierceableLayers;
        public LayerMask NonBounceLayers;

        /// <summary>每隔 DotInterval 秒回報一次當下命中清單（主遊戲據此結算傷害 / 地面特效 / OnHit 分裂）。</summary>
        public Action<LaserBeam, List<BeamHit>> OnBeamDamageTick;

        // homing 把 HomingTurnSpeed(度/秒) 換算成曲率時假定的名目光速；與既有追蹤彈手感對齊
        private const float NominalBeamSpeed = 20f;
        private const float HomingStep = 0.3f;        // 追蹤時的行進步長（越小曲線越平滑、越耗）
        private const float HomingAcquireRadius = 50f;
        private const int MaxMarchSteps = 2048;

        private Shader _shader;
        private Material _beamMat;
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private Mesh _mesh;
        private Color _beamColor = Color.white;
        private float _tileLen = 1f;       // 一個貼圖循環對應的世界長度（保持貼圖不被拉伸）
        private Transform _muzzle, _impact;
        private readonly List<Material> _ownedMaterials = new List<Material>();

        private readonly List<Vector2> _points = new List<Vector2>(64);
        private readonly List<BeamHit> _hits = new List<BeamHit>(16);
        private readonly HashSet<int> _hitSet = new HashSet<int>();
        private float _dotTimer;
        private float _scroll;

        // mesh 重建用緩衝（避免每幀配置）
        private readonly List<Vector3> _verts = new List<Vector3>(256);
        private readonly List<Vector2> _uvs = new List<Vector2>(256);
        private readonly List<Color> _cols = new List<Color>(256);
        private readonly List<int> _tris = new List<int>(512);
        private readonly List<float> _ext = new List<float>(64);

        /// <summary>建立視覺（自繪 mesh 材質 + 砲口/命中光暈）。在設定完所有欄位後呼叫一次。</summary>
        public void Setup(Texture2D beamTex, Color beamColor, float beamWidth, float scrollSpeed,
                          Sprite muzzleSprite, Sprite impactSprite)
        {
            // mesh 頂點直接用世界座標 → 把本物件 transform 歸零，避免被 transform 再次位移。
            // （砲口/命中光暈是子物件，仍用世界 position 每幀更新，不受影響。）
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            ScrollSpeed = scrollSpeed;
            _beamColor = beamColor;
            // 貼圖 1024x256（長:寬 = 4:1）；一個循環 = 4×光束寬，能量波帶才不會被拉長/壓扁
            _tileLen = Mathf.Max(0.01f, beamWidth * 4f);

            _shader = Shader.Find("Custom/AdditiveBeam");
            if (_shader == null) _shader = Shader.Find("Sprites/Default"); // 後備，至少能顯示

            _beamMat = new Material(_shader);
            _ownedMaterials.Add(_beamMat);
            if (beamTex != null)
            {
                beamTex.wrapMode = TextureWrapMode.Repeat;
                _beamMat.mainTexture = beamTex;
            }

            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            _mesh = new Mesh { name = "LaserBeamMesh" };
            _mesh.MarkDynamic();
            _mf.sharedMesh = _mesh;
            _mr.sharedMaterial = _beamMat;
            _mr.sortingOrder = 50;

            // 光暈倍率刻意調小（原本 3.5 / 4.5 會讓頭尾比本體放大快 3~4 倍）。
            // 改成略大於光束寬度即可，讓「變粗」表現在光束本體上、頭尾保持收斂。
            if (muzzleSprite != null) _muzzle = CreateGlow("MuzzleGlow", muzzleSprite, beamColor, beamWidth * 1.3f, 55);
            if (impactSprite != null) _impact = CreateGlow("ImpactGlow", impactSprite, beamColor, beamWidth * 1.8f, 60);

            _dotTimer = DotInterval; // 讓第一幀即可結算一次傷害
        }

        private Transform CreateGlow(string glowName, Sprite sprite, Color color, float desiredDiameter, int sortingOrder)
        {
            var go = new GameObject(glowName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            var mat = new Material(_shader);
            // 圓形光暈不需要「白熱核心」(會變成一條橫向白條) 也不需要脈動，關掉這兩個雷射本體專用效果
            if (mat.HasProperty("_CoreWhiteness")) mat.SetFloat("_CoreWhiteness", 0f);
            if (mat.HasProperty("_FlickerStrength")) mat.SetFloat("_FlickerStrength", 0f);
            _ownedMaterials.Add(mat);
            sr.material = mat;
            sr.sortingOrder = sortingOrder;

            float spriteSize = sprite.bounds.size.x;
            if (spriteSize > 0.0001f)
            {
                float s = desiredDiameter / spriteSize;
                go.transform.localScale = new Vector3(s, s, 1f);
            }
            return go.transform;
        }

        private void Update()
        {
            MarchBeam();
            RenderBeam();
            TickDamage();
        }

        // ── 逐段行進：把追蹤/反彈/穿透/射程收斂進同一迴圈，產出折線頂點與命中清單 ──
        private void MarchBeam()
        {
            _points.Clear();
            _hits.Clear();
            _hitSet.Clear();

            Vector2 pos = Origin;
            Vector2 dir = AimDirection.sqrMagnitude > 0.0001f ? AimDirection.normalized : Vector2.right;
            _points.Add(pos);

            float remaining = Mathf.Max(0.01f, BeamRange);
            int pierceLeft = PierceCount;
            int bouncesLeft = HasBounce ? MaxBounces : 0;
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

        // ── 自繪 mesh：每段一個四邊形，轉角依夾角延伸讓相鄰段重疊補滿凸角 ──
        private void RenderBeam()
        {
            if (_mesh == null) return;

            float half = Radius;
            int n = _points.Count;

            // 1) 算每個頂點的「轉角延伸量」：直段/緩彎≈0、尖反彈≈半寬（上限半寬，避免過度重疊變亮）。
            //    端點(砲口/命中)不延伸，交給光暈收尾。
            _ext.Clear();
            for (int i = 0; i < n; i++)
            {
                if (i == 0 || i == n - 1) { _ext.Add(0f); continue; }
                Vector2 din = (_points[i] - _points[i - 1]);
                Vector2 dout = (_points[i + 1] - _points[i]);
                if (din.sqrMagnitude < 1e-8f || dout.sqrMagnitude < 1e-8f) { _ext.Add(0f); continue; }
                din.Normalize(); dout.Normalize();
                float cos = Mathf.Clamp(Vector2.Dot(din, dout), -1f, 1f);
                // tan(轉角/2) = sqrt((1-cos)/(1+cos))：直走→0、原路折返→∞（夾住到 1）
                float t = (1f + cos > 1e-4f) ? Mathf.Sqrt((1f - cos) / (1f + cos)) : 1f;
                _ext.Add(half * Mathf.Min(t, 1f));
            }

            // 2) 逐段建四邊形
            _verts.Clear(); _uvs.Clear(); _cols.Clear(); _tris.Clear();
            float cumLen = 0f;
            int segCount = n - 1;
            for (int s = 0; s < segCount; s++)
            {
                Vector2 p0 = _points[s];
                Vector2 p1 = _points[s + 1];
                Vector2 d = p1 - p0;
                float segLen = d.magnitude;
                if (segLen < 1e-5f) continue;
                d /= segLen;
                Vector2 perp = new Vector2(-d.y, d.x) * half;

                float extStart = _ext[s];
                float extEnd = _ext[s + 1];
                Vector2 a = p0 - d * extStart;
                Vector2 b = p1 + d * extEnd;

                float uA = (cumLen - extStart) / _tileLen;
                float uB = (cumLen + segLen + extEnd) / _tileLen;

                int baseIdx = _verts.Count;
                _verts.Add(a - perp); _uvs.Add(new Vector2(uA, 0f)); _cols.Add(_beamColor);
                _verts.Add(a + perp); _uvs.Add(new Vector2(uA, 1f)); _cols.Add(_beamColor);
                _verts.Add(b + perp); _uvs.Add(new Vector2(uB, 1f)); _cols.Add(_beamColor);
                _verts.Add(b - perp); _uvs.Add(new Vector2(uB, 0f)); _cols.Add(_beamColor);

                _tris.Add(baseIdx + 0); _tris.Add(baseIdx + 1); _tris.Add(baseIdx + 2);
                _tris.Add(baseIdx + 0); _tris.Add(baseIdx + 2); _tris.Add(baseIdx + 3);

                cumLen += segLen;
            }

            _mesh.Clear();
            if (_verts.Count >= 3)
            {
                _mesh.SetVertices(_verts);
                _mesh.SetUVs(0, _uvs);
                _mesh.SetColors(_cols);
                _mesh.SetTriangles(_tris, 0, true);
            }

            // 能量流動：捲動貼圖 UV（shader 的 TRANSFORM_TEX 會套用此 offset）
            if (_beamMat != null && Mathf.Abs(ScrollSpeed) > 0.0001f)
            {
                _scroll -= ScrollSpeed * Time.deltaTime;
                _beamMat.SetTextureOffset("_MainTex", new Vector2(_scroll, 0f));
            }

            if (_muzzle != null) _muzzle.position = _points[0];
            if (_impact != null) _impact.position = _points[n - 1];
        }

        private void TickDamage()
        {
            if (OnBeamDamageTick == null) return;

            _dotTimer += Time.deltaTime;

            if (DotInterval <= 0f)
            {
                if (_hits.Count > 0) OnBeamDamageTick(this, _hits);
                return;
            }

            if (_dotTimer >= DotInterval)
            {
                _dotTimer -= DotInterval;
                if (_hits.Count > 0) OnBeamDamageTick(this, _hits);
            }
        }

        private void OnDestroy()
        {
            foreach (var m in _ownedMaterials)
                if (m != null) Destroy(m);
            _ownedMaterials.Clear();
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
