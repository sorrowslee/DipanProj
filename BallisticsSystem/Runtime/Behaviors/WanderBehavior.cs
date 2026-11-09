using UnityEngine;

namespace Sorrows.Ballistics
{
    /// <summary>
    /// 「飄移」：子彈的行進方向繞著原本的方向左右擺盪（蛇行），讓一群子彈看起來像各自飄蕩的冤魂，而不是一排飛彈。
    /// 配方表 <c>WanderAngle</c>（擺盪幅度，度）&gt; 0 才掛（2026-11-09，萬魂鬼手）。
    ///
    /// 作法：方向偏移 θ(t) = A·sin(2πf·t + φ)，每幀只把「這一幀 θ 的變化量」轉進速度向量，
    /// 所以長期平均方向不變（不會越飄越歪），追蹤（<see cref="HomingBehavior"/>）照樣能把它拉回目標。
    /// 每顆子彈各自隨機相位 φ 與頻率 f（±30%），同一波分裂出來的鬼魂不會整齊地同步擺動。
    /// 位移走速度向量，碰撞（CircleCast）照常。
    /// </summary>
    public class WanderBehavior : IBulletBehavior
    {
        private readonly float _amplitude;   // 度
        private readonly float _freq;        // Hz
        private readonly float _phase;       // 弧度
        private float _t;
        private float _lastOffset;

        public WanderBehavior(float amplitudeDeg, float frequency = 1.1f)
        {
            _amplitude = Mathf.Max(0f, amplitudeDeg);
            _freq = Mathf.Max(0.05f, frequency) * Random.Range(0.7f, 1.3f);
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _lastOffset = _amplitude * Mathf.Sin(_phase);
        }

        public void OnSpawn(BulletInstance instance) { }

        public void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime)
        {
            if (_amplitude <= 0f || deltaTime <= 0f) return;
            _t += deltaTime;
            float offset = _amplitude * Mathf.Sin(2f * Mathf.PI * _freq * _t + _phase);
            float delta = offset - _lastOffset;
            _lastOffset = offset;
            if (Mathf.Abs(delta) < 1e-4f) return;
            float rad = delta * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            velocity = new Vector2(c * velocity.x - s * velocity.y, s * velocity.x + c * velocity.y);
        }

        public bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity) => false;
    }
}
