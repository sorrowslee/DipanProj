using System.Collections.Generic;
using UnityEngine;

namespace Sorrows.Ballistics
{
    public class BulletInstance : MonoBehaviour
    {
        public Vector2 Velocity;
        public float LifeTime = 3f;
        public LayerMask CollisionMask;

        private List<IBulletBehavior> _behaviors = new List<IBulletBehavior>();

        public void AddBehavior(IBulletBehavior behavior) => _behaviors.Add(behavior);
        public List<IBulletBehavior> GetBehaviors() => _behaviors;

        private void Update()
        {
            Vector2 currentPos = transform.position;
            float frameDist = Velocity.magnitude * Time.deltaTime;
            
            if (frameDist > 0)
            {
                Vector2 dir = Velocity.normalized;
                RaycastHit2D hit = Physics2D.Raycast(currentPos, dir, frameDist, CollisionMask);

                if (hit.collider != null)
                {
                    bool shouldDestroy = true; // 預設撞擊後銷毀

                    // 遍歷所有行為，讓它們各自處理碰撞
                    foreach (var behavior in _behaviors)
                    {
                        // 如果有任何一個行為（如反彈）回傳 true，代表子彈不該被銷毀
                        if (behavior.OnHit(this, hit, ref Velocity))
                        {
                            shouldDestroy = false;
                        }
                    }

                    if (shouldDestroy) 
                    {
                        Destroy(gameObject);
                    }
                    return; // 撞擊幀不執行最後的位移，避免穿牆
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