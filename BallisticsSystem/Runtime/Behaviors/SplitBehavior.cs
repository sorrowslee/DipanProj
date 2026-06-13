using UnityEngine;

namespace Sorrows.Ballistics
{
    public class SplitBehavior : IBulletBehavior
    {
        private ProjectileData _subDef;
        private int _count;
        private float _angle;
        private SplitTiming _timing;
        private bool _hasSplitInThisFrame = false;

        public SplitBehavior(ProjectileData def, int count, float angle, SplitTiming timing)
        {
            _subDef = def; _count = count; _angle = angle; _timing = timing;
        }

        public void OnSpawn(BulletInstance instance)
        {
            if (_timing == SplitTiming.OnSpawn)
            {
                ExecuteSplit(instance);
                Object.Destroy(instance.gameObject);
            }
        }

        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime) 
        {
            _hasSplitInThisFrame = false;
        }

        public bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity)
        {
            if (_timing == SplitTiming.OnHit && !_hasSplitInThisFrame)
            {
                ExecuteSplit(instance);
                _hasSplitInThisFrame = true;
            }
            return false;
        }

        private void ExecuteSplit(BulletInstance instance)
        {
            if (_count <= 0 || _subDef == null) return;

            float startAngle = -_angle / 2f;
            float angleStep = _count > 1 ? _angle / (_count - 1) : 0;

            for (int i = 0; i < _count; i++)
            {
                float currentAngle = startAngle + (angleStep * i);
                Vector2 dir = Quaternion.Euler(0, 0, currentAngle) * instance.Velocity.normalized;

                BallisticsEngine.Internal_SpawnSplit(_subDef, instance.gameObject, instance.transform.position, dir, instance.CollisionMask, instance.PierceableLayers, instance.NonBounceLayers, instance.OnBulletHitObject, instance.transform.localScale, instance.OnTrailPoint);
            }
        }
    }
}
