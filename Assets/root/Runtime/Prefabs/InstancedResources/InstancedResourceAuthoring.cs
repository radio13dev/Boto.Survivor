using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Collisions;
using Unity.Entities;
using Unity.Mathematics;
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
            
            if (authoring.Particle.Asset.HasLifespan)
            {
                var component = GetComponent<HasLifespanAuthoring>();
                DependsOn(component);
                if (!component)
                {
                    Debug.LogError($"{authoring.gameObject} has a lifespan but is missing the {nameof(HasLifespanAuthoring)} component");
                    return;
                }
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
    
    [EditorButton]
    public void RegenerateColliders(float delta)
    {
        if (Particle.Asset && Particle.Asset.Mesh && Particle.Asset.Material)
        {
            // 'Imagine' the mesh at our position, create colliders on the xz-plane relative to that info
            var c = gameObject.AddComponent<MeshCollider>();
            try
            {
                var children = GetComponentsInChildren<Transform>();
                foreach (var child in children)
                    if (c.gameObject != child.gameObject)
                        DestroyImmediate(child.gameObject);
                    
                c.convex = true;
                c.sharedMesh = Particle.Asset.Mesh;
                var bounds = c.bounds;
                for (float x = bounds.min.x; x <= bounds.max.x; x += delta)
                for (float z = bounds.min.z; z <= bounds.max.z; z += delta)
                {
                    var p = new Vector3(x,0,z);
                    var pIn = c.ClosestPoint(p);
                    if (pIn == p)
                    {
                        var childObj = new GameObject($"C:({x},{0},{z})");
                        childObj.transform.SetParent(transform);
                        childObj.transform.SetPositionAndRotation(p, Quaternion.identity);
                        var childC = childObj.AddComponent<ColliderAuthoring>();
                        childC.ColliderType = ColliderType.Sphere;
                        childC.Radius = delta/childC.transform.lossyScale.x;
                    }
                }
            }
            finally
            {
                DestroyImmediate(c);
            }
        }
    }
#endif
}