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

        public Action<BulletInstance, GameObject, RaycastHit2D> OnBulletHitObject;

        private List<IBulletBehavior> _behaviors = new List<IBulletBehavior>();
        private HashSet<int> _hitObjects = new HashSet<int>();
        private bool _isDestroyed = false;

        public void AddBehavior(IBulletBehavior behavior) => _behaviors.Add(behavior);
        public List<IBulletBehavior> GetBehaviors() => _behaviors;

        // 生成時檢查起點周圍是否已有目標（處理 CircleCast 起點在 Collider 內部偵測不到的問題）
        public void CheckSpawnOverlap()
        {
            Collider2D col = Physics2D.OverlapCircle((Vector2)transform.position, Radius, CollisionMask);
            if (col == null) return;

            int id = col.gameObject.GetInstanceID();
            if (_hitObjects.Contains(id)) return;

            OnBulletHitObject?.Invoke(this, col.gameObject, new RaycastHit2D());
            _hitObjects.Add(id);

            // 穿透邏輯：有穿透次數則不銷毀
            int hitLayer = col.gameObject.layer;
            if (((1 << hitLayer) & PierceableLayers) != 0 && PierceCount > 0)
            {
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

                    // 穿透邏輯：命中目標在 PierceableLayers 內且還有穿透次數
                    int hitLayer = hit.collider.gameObject.layer;
                    if (((1 << hitLayer) & PierceableLayers) != 0 && PierceCount > 0)
                    {
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

            transform.position += (Vector3)(Velocity * Time.deltaTime);

            LifeTime -= Time.deltaTime;
            if (LifeTime <= 0) { _isDestroyed = true; Destroy(gameObject); }
        }
    }
}