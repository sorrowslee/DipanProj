using UnityEngine;

namespace Sorrows.Ballistics
{
    public class RotationBehavior : IBulletBehavior
    {
        private float _rotationSpeed;

        public RotationBehavior(float rotationSpeed)
        {
            _rotationSpeed = rotationSpeed;
        }

        public void OnSpawn(BulletInstance instance) { }

        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime)
        {
            // 讓子彈在飛行過程中持續旋轉
            instance.transform.Rotate(0, 0, _rotationSpeed * deltaTime);
        }

        public bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity)
        {
            // 旋轉行為不影響碰撞邏輯
            return false;
        }
    }
}
