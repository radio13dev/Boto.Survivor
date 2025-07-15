using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

public class ProfileScripts : MonoBehaviour
{
    public MeshFilter TemplateFilter;
    public MeshRenderer TemplateRenderer;
    NativeArray<LocalTransform> m_Projectiles;
    NativeArray<Matrix4x4> m_ProjectilesMats;

    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    private void OnEnable()
    {
        m_Projectiles = new NativeArray<LocalTransform>(Profiling.k_ProfileCount, Allocator.Persistent);
        m_ProjectilesMats = new NativeArray<Matrix4x4>(Profiling.k_ProfileCount, Allocator.Persistent);
        for (int i = 0; i < m_Projectiles.Length; i++)
        {
            var p = new Vector3(Random.Range(-100, 100), Random.Range(-100, 100), 0);
            var q = Random.rotation;
            m_Projectiles[i] = LocalTransform.FromPositionRotation(p, q);
        }
    }

    private void Update()
    {
        AsyncRenderTransformGenerator asyncRenderTransformGenerator = new AsyncRenderTransformGenerator
        {
            magneticProjectiles = m_Projectiles,
            matrices = m_ProjectilesMats,
        };

        asyncRenderTransformGenerator.ScheduleParallel(Profiling.k_ProfileCount, 64, default).Complete();

        RenderParams renderParams = new RenderParams(TemplateRenderer.sharedMaterial);
        for (int i = 0; i < Profiling.k_ProfileCount; i += Profiling.k_MaxInstances)
        {
            int count = math.min(Profiling.k_MaxInstances, Profiling.k_ProfileCount - i);
            Graphics.RenderMeshInstanced(renderParams, TemplateFilter.sharedMesh, 0, m_ProjectilesMats, count, i);
        }
    }

    private void OnDisable()
    {
        m_Projectiles.Dispose();
        m_ProjectilesMats.Dispose();
    }

    [BurstCompile]
    struct AsyncRenderTransformGenerator : IJobFor
    {
        [ReadOnly] public NativeArray<LocalTransform> magneticProjectiles;

        [WriteOnly] public NativeArray<Matrix4x4> matrices;

        [BurstCompile]
        public void Execute(int index)
        {
            var projectile = magneticProjectiles[index];
            matrices[index] = projectile.ToMatrix();
        }
    }
}