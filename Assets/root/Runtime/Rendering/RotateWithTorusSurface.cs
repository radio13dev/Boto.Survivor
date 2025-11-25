using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class RotateWithTorusSurface : MonoBehaviour
{
    public bool KeepHeight = false;
    public float Height = 0;

    void Update()
    {
        if (transform.hasChanged)
        {
            TorusMapper.SnapToSurface(transform.position, Height, out var height, out var normal);
            transform.rotation = Quaternion.LookRotation(math.cross(math.cross(normal, transform.forward), normal), normal);
            if (KeepHeight) transform.position = height;
            transform.hasChanged = false;
        }
    }
}
