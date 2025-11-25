using Collisions;
using UnityEngine;
using Unity.Entities;

public class EnemyCharacterAuthoring : MonoBehaviour
{
    public enum Type
    {
        Generic,
        Projectile,
        EnemyTrapProjectile
    }
    
    public Type type;
    public int Health = 10;
    
    partial class Baker : Baker<EnemyCharacterAuthoring>
    {
        public override void Bake(EnemyCharacterAuthoring authoring)
        {
            if (!DependsOn(GetComponent<NetworkIdAuthoring>()))
            {
                Debug.LogError($"Enemies and projectiles can ONLY damage the player if they have a network ID");
                return;
            }
        
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<EnemyTag>(entity);
            
            
            switch (authoring.type)
            {
                case Type.Generic:
                    // Basic character setup
                    AddComponent<CharacterTag>(entity);
                    AddComponent(entity, new Health(authoring.Health));
                    AddComponent<SpawnAnimation>(entity);
                    SetComponentEnabled<SpawnAnimation>(entity, false);
                    
                    AddBuffer<Pending>(entity);
                    AddComponent<Pending.Dirty>(entity);
                    SetComponentEnabled<Pending.Dirty>(entity, false);
            
                    AddComponent<Cut>(entity);
                    SetComponentEnabled<Cut>(entity, false);
                    AddComponent<Degenerate>(entity);
                    SetComponentEnabled<Degenerate>(entity, false);
                    AddComponent<Subdivide>(entity);
                    AddComponent<Subdivide.Timer>(entity);
                    SetComponentEnabled<Subdivide>(entity, false);
                    AddComponent<Decimate>(entity);
                    SetComponentEnabled<Decimate>(entity, false);
                    AddComponent<Dissolve>(entity);
                    SetComponentEnabled<Dissolve>(entity, false);
                    AddComponent<Poke>(entity);
                    SetComponentEnabled<Poke>(entity, false);
                    break;

                case Type.Projectile:
                    break;
                    
                case Type.EnemyTrapProjectile:
                    AddComponent<EnemyTrapProjectileAnimation>(entity);
                    AddComponent<MovementDisabled>(entity);
                    SetComponentEnabled<MovementDisabled>(entity, false);
                    AddComponent(entity, new Health(authoring.Health));
                    break;
            }
        }
    }
}