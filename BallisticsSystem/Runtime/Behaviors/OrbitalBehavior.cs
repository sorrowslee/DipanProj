using UnityEngine;

namespace Sorrows.Ballistics
{
    public class OrbitalBehavior : IBulletBehavior
    {
        private readonly float _radius;
        private readonly float _speed;
        private Transform _orbitCenter;
        private float _currentAngle;
        private bool _isOrbiting = true;
        private Vector2 _lastTangent;

        public bool IsOrbiting => _isOrbiting;

        public OrbitalBehavior(float radius, float speed)
        {
            _radius = radius;
            _speed = speed;
        }

        public void Initialize(Transform center, float startAngle)
        {
            _orbitCenter = center;
            _currentAngle = startAngle;
        }

        public void OnSpawn(BulletInstance instance) { }

        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime)
        {
            if (!_isOrbiting || _orbitCenter == null)
            {
                _isOrbiting = false;
                return;
            }

            float angularSpeed = _speed / _radius;
            _currentAngle += angularSpeed * deltaTime;

            Vector2 centerPos = (Vector2)_orbitCenter.position;
            Vector2 targetPos = centerPos
                + new Vector2(Mathf.Cos(_currentAngle), Mathf.Sin(_currentAngle)) * _radius;

            velocity = (targetPos - (Vector2)instance.transform.position) / deltaTime;
            _lastTangent = velocity;
        }

        public bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity)
        {
            if (!_isOrbiting) return false;

            if (_lastTangent.sqrMagnitude > 0.001f)
            {
                float dot = Vector2.Dot(velocity.normalized, _lastTangent.normalized);
                if (dot < 0.5f)
                    _isOrbiting = false;
            }

            return false;
        }
    }
}
