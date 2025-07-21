using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class EnemySpawnerAuthoring : MonoBehaviour
{
    public EnemySpawner Spawner = new EnemySpawner()
    {
        SpawnChancePerFrame = 10,
        SpawnRadius = 10,
        SpawnBlockRadiusSqr = 9*9
    };
    partial class Baker : Baker<EnemySpawnerAuthoring>
    {
        public override void Bake(EnemySpawnerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            var spawner = authoring.Spawner;
            spawner.random = new Random((uint)authoring.GetInstanceID());
            AddComponent(entity, spawner);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 0.4f, 1f);
        Gizmos.DrawWireSphere(transform.position, Spawner.SpawnRadius);
        Gizmos.color = new Color(0.6f, 0.2f, 0.4f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, math.sqrt(Spawner.SpawnBlockRadiusSqr));
    }
}