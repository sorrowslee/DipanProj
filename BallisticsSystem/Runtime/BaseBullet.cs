using UnityEngine;

namespace Sorrows.Ballistics
{
    public class BaseBullet : MonoBehaviour
    {
        public Vector2 Velocity;
        public float LifeTime = 3f; // 3 秒後自動消失

        private void Update()
        {
            // 基礎飛行邏輯：純位移
            transform.Translate(Velocity * Time.deltaTime);

            // 簡易生命週期管理
            LifeTime -= Time.deltaTime;
            if (LifeTime <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}