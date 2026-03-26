using UnityEngine;

namespace Sorrows.Ballistics
{
    public class BounceBehavior : IBulletBehavior
    {
        private int _maxBounces;
        private int _currentBounces = 0;

        public BounceBehavior(int maxBounces) => _maxBounces = maxBounces;

        public void OnSpawn(BulletInstance instance) { }
        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime) { }

        public bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity)
        {
            // 命中目標在 NonBounceLayers 內則不反彈，交由銷毀流程處理
            int hitLayer = hit.collider.gameObject.layer;
            if (((1 << hitLayer) & instance.NonBounceLayers) != 0)
            {
                return false;
            }

            if (_currentBounces < _maxBounces)
            {
                _currentBounces++;
                velocity = Vector2.Reflect(velocity, hit.normal);
                instance.transform.position = hit.point + hit.normal * 0.01f;
                return true; 
            }
            return false;
        }
    }
}