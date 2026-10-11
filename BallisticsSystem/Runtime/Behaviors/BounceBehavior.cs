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
                // ⚠ 用 hit.centroid（碰到牆那一刻「圓心」的位置），不是 hit.point（牆面上的接觸點）。
                //   CircleCast 的 point 在牆面上；把圓心放到牆面上＝整顆子彈往牆裡陷一個判定半徑。
                //   小子彈（半徑 0.1）看不出來，血火咒（半徑 0.8）一反彈就整顆鑽進牆裡、看起來像被牆吃掉（2026-10-11）。
                instance.transform.position = hit.centroid + hit.normal * 0.01f;
                return true; 
            }
            return false;
        }
    }
}