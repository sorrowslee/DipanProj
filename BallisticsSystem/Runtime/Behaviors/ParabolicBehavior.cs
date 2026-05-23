using UnityEngine;

namespace Sorrows.Ballistics
{
    /// <summary>
    /// 拋物線行為：接管子彈移動，地面位置從 startPos 線性插值到 targetPos，
    /// 視覺上加上「假高度」Y 偏移，模擬丟炸彈的弧線。抵達目標即觸發 OnGroundLanded 事件並結束。
    /// 飛行期間 CollisionMask = 0，不參與任何 layer 命中。
    ///
    /// 飛行時間採「固定秒數」模式（與距離無關）：建構子的 flightDuration = N 秒，
    /// 不論起點到目標多遠，都用這個時間抵達——遠的飛快、近的飛慢，便於同一發配方多顆炸彈同時抵達。
    /// 透過 Initialize() 由主遊戲端提供發射起點與目標點。
    /// </summary>
    public class ParabolicBehavior : IBulletBehavior
    {
        private readonly float _arcHeight;
        private readonly float _flightDuration;

        private Vector2 _arcStart;
        private Vector2 _arcEnd;
        private float _elapsed;
        private bool _isInitialized;
        private bool _hasFinished;

        public ParabolicBehavior(float arcHeight, float flightDuration)
        {
            _arcHeight = arcHeight;
            _flightDuration = Mathf.Max(0.0001f, flightDuration);
        }

        /// <summary>由主遊戲端在 Spawn 後呼叫，提供發射起點與目標點。</summary>
        public void Initialize(Vector2 startPos, Vector2 targetPos)
        {
            _arcStart = startPos;
            _arcEnd = targetPos;
            _elapsed = 0f;
            _isInitialized = true;
            _hasFinished = false;
        }

        public void OnSpawn(BulletInstance instance)
        {
            // 拋物線飛行不參與 layer 命中：mask = 0 → CircleCast 不會找到任何 collider
            instance.CollisionMask = 0;
            // 不依賴 LifeTime 自動超時，由本行為控制銷毀（落地時設 0）
            instance.LifeTime = -1f;
            // 清掉 BallisticsEngine 預設給的方向速度，由 OnProcessMovement 完全接管
            instance.Velocity = Vector2.zero;
        }

        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime)
        {
            if (!_isInitialized || _hasFinished)
            {
                velocity = Vector2.zero;
                return;
            }

            _elapsed += deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _flightDuration);

            Vector2 groundPos = Vector2.Lerp(_arcStart, _arcEnd, progress);
            float height = 4f * _arcHeight * progress * (1f - progress);

            Vector3 desiredPos = new Vector3(groundPos.x, groundPos.y + height, 0f);
            Vector3 currentPos = instance.transform.position;

            // 利用 BulletInstance 既有的 transform.position += Velocity * dt 流程驅動位置（與 OrbitalBehavior 同套）
            Vector3 deltaPos = desiredPos - currentPos;
            velocity = new Vector2(deltaPos.x, deltaPos.y) / Mathf.Max(0.0001f, deltaTime);

            if (progress >= 1f)
            {
                instance.RaiseGroundLanded(_arcEnd);
                _hasFinished = true;
                instance.LifeTime = 0f;
            }
        }

        public bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity)
        {
            // 飛行期間 CollisionMask = 0，理論上不會走到這裡；保險起見回傳 false
            return false;
        }
    }
}
