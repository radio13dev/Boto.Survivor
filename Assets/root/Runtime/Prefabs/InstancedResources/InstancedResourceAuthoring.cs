using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Collisions;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

[Save]
public struct InstancedResourceRequest : ISharedComponentData
{
    public readonly int ToSpawn;

    public InstancedResourceRequest(int toSpawn)
    {
        ToSpawn = toSpawn;
    }
    
    public GameManager.InstancedResources Get(World world)
    {
        return world.EntityManager.GetSingletonBuffer<GameManager.InstancedResources>(true)[ToSpawn];
    }
}

#if UNITY_EDITOR
[ExecuteInEditMode]
#endif
public class InstancedResourceAuthoring : MonoBehaviour
{
    public DatabaseRef<InstancedResource, InstancedResourcesDatabase> Particle = new();

    public class Baker : Baker<InstancedResourceAuthoring>
    {
        public override void Bake(InstancedResourceAuthoring authoring)
        {
            DependsOn(authoring.Particle.Asset);
            if (!authoring.Particle.Asset)
            {
                Debug.LogError($"Invalid asset on {authoring}: {authoring.Particle.Asset}");
                return;
            }
        
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddSharedComponent(entity, new InstancedResourceRequest(authoring.Particle.GetAssetIndex()));
            
            if (authoring.Particle.Asset.UseLastTransform)
            {
                AddComponent(entity, new LocalTransformLast());
            }
            if (authoring.Particle.Asset.Animated)
            {
                AddComponent<SpriteAnimFrame>(entity);
                AddComponent<SpriteAnimFrameTime>(entity);
            }
            if (authoring.Particle.Asset.HasLifespan)
            {
                AddComponent<SpawnTimeCreated>(entity);
                AddComponent<DestroyAtTime>(entity);
            }
            if (authoring.Particle.Asset.IsTorus)
            {
                AddComponent<TorusMin>(entity);
            }
            if (authoring.Particle.Asset.IsCone)
            {
                AddComponent<TorusCone>(entity);
            }
        }
    }

#if UNITY_EDITOR
    private void OnEnable() {

        UnityEditor.SceneView.duringSceneGui -= OnSceneGUI;
        UnityEditor.SceneView.duringSceneGui += OnSceneGUI;
    }   

    private void OnDisable() {

        UnityEditor.SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSceneGUI(SceneView sceneView) {

        Draw(sceneView.camera);
    }

    private void Update() {

        Draw(Camera.main);
    }

    private void Draw(Camera camera) {

        if (camera && Particle.Asset && Particle.Asset.Mesh && Particle.Asset.Material)
        {
            Matrix4x4 matrix = camera.transform.localToWorldMatrix * Matrix4x4.TRS(Vector3.forward * 10, Quaternion.identity, Vector3.one);

            //For some reason, this stops Unity from piling up the draw commands?
            //material.enableInstancing = true;

            //No trail, object visible twice in scene, scene mesh visible in game.
            Graphics.DrawMesh(Particle.Asset.Mesh, transform.localToWorldMatrix, Particle.Asset.Material, gameObject.layer, camera);
           
            //Leaves a small trail, object only visible in camera
            //Graphics.DrawMesh(mesh, matrix, material, gameObject.layer, camera);
        }
    }
#endif
}