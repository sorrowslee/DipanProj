using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sorrows.Ballistics
{
    public class BulletInstance : MonoBehaviour
    {
        public Vector2 Velocity;
        public float LifeTime = 3f;
        public float Radius = 0.1f; // 🟢 子彈判定半徑
        public LayerMask CollisionMask;
        public int PierceCount = 0; // 🟢 剩餘穿透次數

        public Action<BulletInstance, GameObject, RaycastHit2D> OnBulletHitObject;

        private List<IBulletBehavior> _behaviors = new List<IBulletBehavior>();
        private HashSet<int> _hitObjects = new HashSet<int>(); // 🟢 防止一幀多次傷害同一個對象

        public void AddBehavior(IBulletBehavior behavior) => _behaviors.Add(behavior);
        public List<IBulletBehavior> GetBehaviors() => _behaviors;

        private void Update()
        {
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

                    // 🟢 穿透邏輯：如果是敵人 (Layer 7) 且還有穿透次數
                    if (hit.collider.gameObject.layer == 7 && PierceCount > 0)
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
            if (LifeTime <= 0) Destroy(gameObject);
        }
    }
}