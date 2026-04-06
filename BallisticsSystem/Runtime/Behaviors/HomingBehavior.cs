using UnityEngine;

namespace Sorrows.Ballistics
{
    public class HomingBehavior : IBulletBehavior
    {
        private readonly float _turnSpeed;
        private Transform _target;

        private const float AcquireRadius = 50f;

        public HomingBehavior(float turnSpeed) => _turnSpeed = turnSpeed;

        public void OnSpawn(BulletInstance instance) { }

        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime)
        {
            if (_target == null)
                AcquireNearestTarget(instance);

            if (_target == null) return;

            Vector2 toTarget = (Vector2)_target.position - (Vector2)instance.transform.position;
            if (toTarget.sqrMagnitude < 0.001f) return;

            float currentAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            float targetAngle  = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            float newAngle     = Mathf.MoveTowardsAngle(currentAngle, targetAngle, _turnSpeed * deltaTime);

            float rad = newAngle * Mathf.Deg2Rad;
            velocity  = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * velocity.magnitude;
        }

        public bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity)
        {
            // 命中的目標是可穿透層（怪物），且正好是目前追蹤的對象 → 清除快取，下幀重新選目標
            int hitLayer = hit.collider.gameObject.layer;
            bool isPierceable = ((1 << hitLayer) & instance.PierceableLayers) != 0;
            if (isPierceable && _target != null && _target.gameObject == hit.collider.gameObject)
                _target = null;

            return false;
        }

        private void AcquireNearestTarget(BulletInstance instance)
        {
            if (instance.PierceableLayers == 0) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(
                (Vector2)instance.transform.position,
                AcquireRadius,
                instance.PierceableLayers);

            float bestDist = float.MaxValue;
            _target = null;

            foreach (var col in hits)
            {
                // 已經打過的目標不再追蹤
                if (instance.HasHit(col.gameObject.GetInstanceID())) continue;

                float dist = ((Vector2)col.transform.position - (Vector2)instance.transform.position).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    _target  = col.transform;
                }
            }
        }
    }
}
