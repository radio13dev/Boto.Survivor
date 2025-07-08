/*
 using Unity.Entities;

namespace Collisions
{
    [UpdateInGroup(typeof(GameLogicSystemGroup))]
    [UpdateAfter(typeof(ProjectileCollisionSystem))]
    public partial struct HandleProjectileCharacterCollisions : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var q = SystemAPI.QueryBuilder().WithAll<Collisions>().Build();
            
            new Job().Schedule();
        }
    
        partial struct Job : IJobEntity
        {
            public void Execute(Entity entity, in DynamicBuffer<Collisions> collisions, ref Health health)
            {
                for (int i = 0; i < collisions.Length; i++)
                {
                    health.Value -= 1;
                }
            }
        }
    }
}
*/