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
    [HideInInspector] public Sprite[] AnimationSprites;
    [HideInInspector] public float AnimFPS;

        public Action<BulletInstance, GameObject, RaycastHit2D> OnBulletHitObject;
        // 拋物線最終落地通知（不走 layer 命中流程）；非拋物線彈不會觸發
        public Action<BulletInstance, Vector2> OnGroundLanded;

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

        public void AddBehavior(IBulletBehavior behavior) => _behaviors.Add(behavior);
        public List<IBulletBehavior> GetBehaviors() => _behaviors;
        public bool HasHit(int instanceId) => _hitObjects.Contains(instanceId);

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        // 生成時檢查起點周圍是否已有目標（處理 CircleCast 起點在 Collider 內部偵測不到的問題）
        public void CheckSpawnOverlap()
        {
            Collider2D col = Physics2D.OverlapCircle((Vector2)transform.position, Radius, CollisionMask);
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
            }

            transform.position += (Vector3)(Velocity * Time.deltaTime);

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

            // LifeTime < 0（例如 -1）表示不因時間銷毀；否則正常倒數
            if (LifeTime >= 0f)
            {
                LifeTime -= Time.deltaTime;
                if (LifeTime <= 0f) { _isDestroyed = true; Destroy(gameObject); }
            }
        }
    }
}