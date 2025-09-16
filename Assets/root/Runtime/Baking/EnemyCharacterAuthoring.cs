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
    public Health Health = new Health(10);
    
    [Range(0,100)]
    public int Chance;
    
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
                    AddComponent(entity, authoring.Health);
                    break;

                case Type.Projectile:
                    break;
                    
                case Type.EnemyTrapProjectile:
                    AddComponent<EnemyTrapProjectileAnimation>(entity);
                    AddComponent<MovementDisabled>(entity);
                    SetComponentEnabled<MovementDisabled>(entity, false);
                    AddComponent(entity, authoring.Health);
                    break;
            }
        }
    }
}