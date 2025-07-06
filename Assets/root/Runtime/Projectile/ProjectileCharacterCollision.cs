using Unity.Entities;

public struct ProjectileCharacterCollision : IBufferElementData
{
    public Entity Projectile;
    
    public ProjectileCharacterCollision(Entity projectile)
    {
        Projectile = projectile;
    }
}

[UpdateInGroup(typeof(ProjectileCollisionSystemGroup))]
[UpdateAfter(typeof(ProjectileCollisionSystem))]
public partial struct HandleProjectileCharacterCollisions : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            
        }.Schedule();
    }
    
    partial struct Job : IJobEntity
    {
        public void Execute(Entity entity, ref DynamicBuffer<ProjectileCharacterCollision> collisions, ref Health health)
        {
            for (int i = 0; i < collisions.Length; i++)
            {
                health.Value -= 1;
            }
        
            collisions.Clear();
        }
    }
}