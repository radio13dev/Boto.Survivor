using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace Collisions
{
    public partial struct RegenerateJob : IJob
    {
        public NativeTrees.NativeOctree<Entity> tree;
        [ReadOnly] public NativeArray<Entity> entities;
        [ReadOnly] public NativeArray<Collider> colliders;
        [ReadOnly] public NativeArray<LocalTransform> transforms;

        public void Execute()
        {
            tree.Clear();
            for (int i = 0; i < colliders.Length; i++)
                tree.Insert(entities[i], colliders[i].Add(transforms[i]));
        }
    }
    public partial struct RegenerateJob_Collider : IJob
    {
        public NativeTrees.NativeOctree<(Entity e, Collider c)> tree;
        [ReadOnly] public NativeArray<Entity> entities;
        [ReadOnly] public NativeArray<Collider> colliders;
        [ReadOnly] public NativeArray<LocalTransform> transforms;

        public void Execute()
        {
            tree.Clear();
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i].Apply(transforms[i]);
                tree.Insert((entities[i], c), c.AABB);
            }
        }
    }
    public partial struct RegenerateJob_NetworkId : IJob
    {
        public NativeTrees.NativeOctree<(Entity, NetworkId, Collider)> tree;
        [ReadOnly] public NativeArray<Entity> entities;
        [ReadOnly] public NativeArray<NetworkId> networkIds;
        [ReadOnly] public NativeArray<Collider> colliders;
        [ReadOnly] public NativeArray<LocalTransform> transforms;

        public void Execute()
        {
            tree.Clear();
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i].Apply(transforms[i]);
                tree.Insert((entities[i], networkIds[i], c), c.AABB);
            }
        }
    }
}