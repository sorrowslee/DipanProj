using UnityEngine;

namespace Sorrows.Ballistics
{
    public class HomingBehavior : IBulletBehavior
    {
        private readonly float _turnSpeed;
        private Transform _target;

        /// <summary>舊行為的索敵半徑：等於「全場鎖最近的那隻」。</summary>
        public const float DefaultAcquireRadius = 50f;

        private readonly float _acquireRadius;
        /// <summary>在最近的幾隻裡隨機挑（1＝永遠挑最近，舊行為）。半徑小於預設時自動用 3，讓一群子彈分散追不同的怪。</summary>
        private readonly int _pickAmong;
        private float _retryTimer;   // 半徑內沒有怪時，隔一小段再找（不必每幀 OverlapCircle）
        private const float RebiteDelay = 0.5f;   // 分散模式：咬到後幾秒清命中名單（剛好夠穿過去、繞回來）
        private float _rebiteTimer = -1f;

        public HomingBehavior(float turnSpeed, float acquireRadius = DefaultAcquireRadius)
        {
            _turnSpeed = turnSpeed;
            _acquireRadius = acquireRadius > 0f ? acquireRadius : DefaultAcquireRadius;
            _pickAmong = _acquireRadius < DefaultAcquireRadius - 0.01f ? 3 : 1;
        }

        public void OnSpawn(BulletInstance instance) { }

        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime)
        {
            if (_rebiteTimer > 0f)
            {
                _rebiteTimer -= deltaTime;
                if (_rebiteTimer <= 0f) { instance.ClearHitHistory(); _retryTimer = 0f; }
            }

            if (_target == null)
            {
                _retryTimer -= deltaTime;
                if (_retryTimer > 0f) return;
                AcquireNearestTarget(instance);
                if (_target == null) { _retryTimer = _pickAmong > 1 ? 0.15f : 0f; return; }   // 舊行為（_pickAmong=1）照舊每幀找
            }

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
            {
                _target = null;
                // 分散模式（萬魂鬼手）：咬穿之後隔一小段清掉命中名單——鬼魂繞回來可以再咬同一隻（還是受穿透數限制）。
                // 舊行為（_pickAmong=1）不清，一顆子彈一輩子對同一隻只打一次。
                if (_pickAmong > 1) _rebiteTimer = RebiteDelay;
            }

            return false;
        }

        private void AcquireNearestTarget(BulletInstance instance)
        {
            LayerMask layers = instance.HomingLayers.value != 0 ? instance.HomingLayers : instance.PierceableLayers;
            if (layers == 0) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(
                (Vector2)instance.transform.position,
                _acquireRadius,
                layers);

            _target = null;

            if (_pickAmong <= 1)
            {
                // 舊行為：鎖最近的那隻
                float bestDist = float.MaxValue;
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
                return;
            }

            // 分散模式：取最近的 _pickAmong 隻，隨機挑一隻（同一波子彈不會全部撲向同一隻）
            Transform[] best = new Transform[_pickAmong];
            float[] bestD = new float[_pickAmong];
            for (int i = 0; i < _pickAmong; i++) bestD[i] = float.MaxValue;
            foreach (var col in hits)
            {
                if (instance.HasHit(col.gameObject.GetInstanceID())) continue;
                float dist = ((Vector2)col.transform.position - (Vector2)instance.transform.position).sqrMagnitude;
                for (int i = 0; i < _pickAmong; i++)
                {
                    if (dist < bestD[i])
                    {
                        for (int j = _pickAmong - 1; j > i; j--) { bestD[j] = bestD[j - 1]; best[j] = best[j - 1]; }
                        bestD[i] = dist; best[i] = col.transform;
                        break;
                    }
                }
            }
            int n = 0;
            while (n < _pickAmong && best[n] != null) n++;
            if (n > 0) _target = best[Random.Range(0, n)];
        }
    }
}
