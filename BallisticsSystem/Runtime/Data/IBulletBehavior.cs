using UnityEngine;

namespace Sorrows.Ballistics
{
    public interface IBulletBehavior
    {
        void OnSpawn(BulletInstance instance);
        void OnProcessMovement(BulletInstance instance, ref Vector2 velocity, ref Vector2 position, float deltaTime);
        bool OnHit(BulletInstance instance, RaycastHit2D hit, ref Vector2 velocity);
    }
}