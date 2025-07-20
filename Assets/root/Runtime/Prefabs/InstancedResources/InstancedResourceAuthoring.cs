using System.Linq;
using BovineLabs.Saving;
using Unity.Entities;
using Unity.Transforms;
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
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddSharedComponent(entity, new InstancedResourceRequest(authoring.Particle.AssetIndex));
            AddComponent(entity, new LocalTransformLast());
            AddComponent<SpriteAnimFrame>(entity);
            
            if (authoring.Particle.Asset && authoring.Particle.Asset.Animated)
                AddComponent<SpriteAnimFrameTime>(entity);
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