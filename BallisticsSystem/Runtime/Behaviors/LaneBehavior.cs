using UnityEngine;

namespace Sorrows.Ballistics
{
    /// <summary>
    /// 平行彈（RecipeTable ParallelCount）的「散開再拉直」行為。
    /// 所有平行彈都從玩家位置出生（不會生在牆裡），出生時多一個側向速度，在 duration 秒內線性衰減到 0，
    /// 之後就是純平行飛行。位移走的是速度向量，所以 <see cref="BulletInstance"/> 的 CircleCast 碰撞照常生效——
    /// 貼牆那幾道會正常撞牆／反彈，不會側滑進牆。
    /// 側向初速 = 2 × 目標偏移 ÷ duration（線性衰減的總位移 = v0 × T ÷ 2）。
    /// 每顆子彈一個實例（有狀態），由 BallisticsEngine 的 spawn extra-behavior 工廠產生；OnSpawn 分裂的子彈也各自拿一個。
    /// </summary>
    public class LaneBehavior : IBulletBehavior
    {
        private readonly Vector2 _lateral;   // 出生時加上去的側向速度
        private readonly float _duration;
        private Vector2 _left;               // 還沒收回的側向速度

        public LaneBehavior(Vector2 lateralOffset, float duration)
        {
            _duration = Mathf.Max(0.01f, duration);
            _lateral = lateralOffset * (2f / _duration);
            _left = _lateral;
        }

        public void OnSpawn(BulletInstance instance)
        {
            instance.Velocity += _lateral;
        }

        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime)
        {
            if (_left.sqrMagnitude <= 0f) return;
            Vector2 step = _lateral * (deltaTime / _duration);
            if (step.sqrMagnitude >= _left.sqrMagnitude) step = _left;
            velocity -= step;
            _left -= step;
        }

        public bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity) => false;
    }
}
