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
                tree.Insert(entities[i], colliders[i].Add(transforms[i].Position));
        }
    }
}