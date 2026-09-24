using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sorrows.Ballistics
{
    public class BulletInstance : MonoBehaviour
    {
    [HideInInspector] public Vector2 Velocity;
    [HideInInspector] public float LifeTime = 3f;
    [HideInInspector] public float Radius = 0.1f;
    [HideInInspector] public LayerMask CollisionMask;
    [HideInInspector] public LayerMask PierceableLayers;
    [HideInInspector] public LayerMask NonBounceLayers;
    [HideInInspector] public int PierceCount = 0;
    [HideInInspector] public float SpriteAngleOffset = 0f;
    // 往左飛（Velocity.x < 0）時把圖上下翻轉：有「上下之分」的子彈圖（骷髏鬼頭…）跟著飛行角度轉 180° 會變倒立。
    // 只在 SpriteAngleOffset != 0（有跟著飛行方向轉）時才有意義。分裂子彈由 Instantiate 複製母彈而繼承。
    [HideInInspector] public bool FlipYWhenMovingLeft = false;
    [HideInInspector] public Sprite[] AnimationSprites;
    [HideInInspector] public float AnimFPS;
    // 軌跡點間距（世界單位）：> 0 時每飛這麼遠就觸發一次 OnTrailPoint。0 = 不產生軌跡。
    [HideInInspector] public float TrailStep = 0f;

        public Action<BulletInstance, GameObject, RaycastHit2D> OnBulletHitObject;
        // 拋物線最終落地通知（不走 layer 命中流程）；非拋物線彈不會觸發
        public Action<BulletInstance, Vector2> OnGroundLanded;
        // 沿飛行路徑每隔 TrailStep 距離回報一次「經過此點」。彈道系統不知道種的是什麼（尖刺/火痕…），由主遊戲決定。
        public Action<BulletInstance, Vector2> OnTrailPoint;

        /// <summary>FlipYWhenMovingLeft 用：水平速度往左就上下翻轉圖（x ≈ 0 時維持上一次的狀態，避免垂直飛行時抖動）。</summary>
        public void ApplyFacingFlip(float velocityX)
        {
            if (Mathf.Abs(velocityX) < 0.0001f) return;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _sr.flipY = velocityX < 0f;
        }

        /// <summary>
        /// 重疊中的碰撞體是否在子彈「背後或側邊」（子彈沒有往它裡面鑽）。
        /// 子彈中心本身就在碰撞體裡（ClosestPoint＝自己）時回 false ⇒ 照舊算命中（生在牆裡本來就該撞）。
        /// </summary>
        static bool IsBehindOverlap(Collider2D col, Vector2 pos, Vector2 moveDir)
        {
            if (moveDir.sqrMagnitude < 1e-8f) return false;
            Vector2 toSurface = col.ClosestPoint(pos) - pos;
            if (toSurface.sqrMagnitude < 1e-8f) return false;
            return Vector2.Dot(toSurface, moveDir) <= 0f;
        }

        public void RaiseGroundLanded(Vector2 landPos)
        {
            OnGroundLanded?.Invoke(this, landPos);
        }

        private List<IBulletBehavior> _behaviors = new List<IBulletBehavior>();
        private HashSet<int> _hitObjects = new HashSet<int>();
        private bool _isDestroyed = false;
        private SpriteRenderer _sr;
        private float _animTimer;
        private int _animFrame;
        private float _trailAccum;
        private Vector2 _trailLastPos;

        /// <summary>發射端額外掛的行為工廠（例：平行彈的 LaneBehavior）。每顆子彈呼叫一次拿新實例；OnSpawn 分裂出的子彈會繼承同一個工廠。NonSerialized：Instantiate 複製母彈時不能帶過去。</summary>
        [System.NonSerialized] public System.Func<IBulletBehavior> SpawnExtraBehavior;
        /// <summary>正在跑 OnSpawn（分裂子彈判斷「母彈還在出生點」用）。</summary>
        [System.NonSerialized] public bool IsSpawning;

        public void AddBehavior(IBulletBehavior behavior) => _behaviors.Add(behavior);
        public List<IBulletBehavior> GetBehaviors() => _behaviors;
        public bool HasHit(int instanceId) => _hitObjects.Contains(instanceId);

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _trailLastPos = transform.position;
        }

        // 生成時檢查起點周圍是否已有目標（處理 CircleCast 起點在 Collider 內部偵測不到的問題）
        public void CheckSpawnOverlap()
        {
            // 取「第一個不在背後」的重疊物：判定半徑大的子彈（餓鬼牙符 1.6）貼牆往外射時，
            // 圓圈會蓋到背後那面牆，不排除就會一出生就撞牆自爆（見 PROBLEMS F31）。
            Vector2 pos = transform.position;
            Collider2D col = null;
            foreach (var c in Physics2D.OverlapCircleAll(pos, Radius, CollisionMask))
            {
                if (c == null || IsBehindOverlap(c, pos, Velocity)) continue;
                col = c; break;
            }
            if (col == null) return;

            int id = col.gameObject.GetInstanceID();
            if (_hitObjects.Contains(id)) return;

            OnBulletHitObject?.Invoke(this, col.gameObject, new RaycastHit2D());
            _hitObjects.Add(id);

            // 穿透邏輯：PierceCount > 0 可穿透並遞減；PierceCount < 0 為無限穿透（不遞減）
            int hitLayer = col.gameObject.layer;
            if (((1 << hitLayer) & PierceableLayers) != 0 && (PierceCount > 0 || PierceCount < 0))
            {
                if (PierceCount > 0)
                    PierceCount--;
                return;
            }

            // 近距離重疊不適合做反彈（無法線方向），直接銷毀
            _isDestroyed = true;
            Destroy(gameObject);
        }

        private void Update()
        {
            if (_isDestroyed) return;

            Vector2 currentPos = transform.position;
            float frameDist = Velocity.magnitude * Time.deltaTime;
            
            if (frameDist > 0)
            {
                Vector2 dir = Velocity.normalized;
                // 🟢 使用 CircleCast 代替 Raycast，增加判定面積
                RaycastHit2D hit = Physics2D.CircleCast(currentPos, Radius, dir, frameDist, CollisionMask);
                // 起點就重疊（distance 0）而且那個東西在背後 ⇒ 子彈正在離開它，不算命中；改找這一步路上第一個真正擋路的。
                if (hit.collider != null && hit.distance <= 0f && IsBehindOverlap(hit.collider, currentPos, dir))
                {
                    hit = default;
                    foreach (var h in Physics2D.CircleCastAll(currentPos, Radius, dir, frameDist, CollisionMask))
                    {
                        if (h.collider == null) continue;
                        if (h.distance <= 0f && IsBehindOverlap(h.collider, currentPos, dir)) continue;
                        hit = h; break;
                    }
                }

                if (hit.collider != null)
                {
                    int id = hit.collider.gameObject.GetInstanceID();

                    // 🟢 如果還沒撞過這個東西，才觸發回報
                    if (!_hitObjects.Contains(id))
                    {
                        OnBulletHitObject?.Invoke(this, hit.collider.gameObject, hit);
                        _hitObjects.Add(id);
                    }

                    bool shouldDestroy = true;

                    // 穿透邏輯：PierceCount > 0 可穿透並遞減；PierceCount < 0 為無限穿透（不遞減）
                    int hitLayer = hit.collider.gameObject.layer;
                    if (((1 << hitLayer) & PierceableLayers) != 0 && (PierceCount > 0 || PierceCount < 0))
                    {
                        if (PierceCount > 0)
                            PierceCount--;
                        shouldDestroy = false; // 穿透時不銷毀
                    }

                    // 遍歷行為 (反彈等)
                    foreach (var behavior in _behaviors)
                    {
                        if (behavior.OnHit(this, hit, ref Velocity))
                        {
                            shouldDestroy = false;
                        }
                    }

                    if (shouldDestroy) 
                    {
                        _isDestroyed = true;
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            foreach (var behavior in _behaviors)
            {
                behavior.OnProcessMovement(this, ref Velocity, ref currentPos, Time.deltaTime);
            }

            if (SpriteAngleOffset != 0f)
            {
                float angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle + SpriteAngleOffset);
                if (FlipYWhenMovingLeft) ApplyFacingFlip(Velocity.x);
            }

            // 安全網：任何來源算出非有限（NaN/Inf）的速度都會讓 transform.position 變 NaN 並每幀狂洗 console。
            // 直接銷毀這顆壞彈，避免污染與洗版（例如上游落點被算成 NaN 時）。
            Vector2 step = Velocity * Time.deltaTime;
            if (float.IsNaN(step.x) || float.IsNaN(step.y) || float.IsInfinity(step.x) || float.IsInfinity(step.y))
            {
                _isDestroyed = true;
                Destroy(gameObject);
                return;
            }
            transform.position += (Vector3)step;

            if (AnimationSprites != null && AnimationSprites.Length > 1 && AnimFPS > 0 && _sr != null)
            {
                _animTimer += Time.deltaTime;
                float frameDuration = 1f / AnimFPS;
                if (_animTimer >= frameDuration)
                {
                    _animTimer -= frameDuration;
                    _animFrame = (_animFrame + 1) % AnimationSprites.Length;
                    _sr.sprite = AnimationSprites[_animFrame];
                }
            }

            // 軌跡點：每飛 TrailStep 距離回報一次「經過此點」（主遊戲沿路種特效）。
            // 用實際位移累計，故反彈/追蹤/分裂後的彎折路徑都能正確跟著種。
            if (TrailStep > 0f && OnTrailPoint != null)
            {
                Vector2 nowPos = transform.position;
                _trailAccum += Vector2.Distance(nowPos, _trailLastPos);
                _trailLastPos = nowPos;
                while (_trailAccum >= TrailStep)
                {
                    _trailAccum -= TrailStep;
                    OnTrailPoint.Invoke(this, transform.position);
                }
            }

            // LifeTime < 0（例如 -1）表示不因時間銷毀；否則正常倒數
            if (LifeTime >= 0f)
            {
                LifeTime -= Time.deltaTime;
                if (LifeTime <= 0f) { _isDestroyed = true; Destroy(gameObject); }
            }
        }
    }
}