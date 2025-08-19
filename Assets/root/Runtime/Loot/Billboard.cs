using System;
using Unity.Mathematics;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    private void Update()
    {
        //var camForward = CameraRegistry.Main.transform.forward;
        //var worldForward = (transform.position - CameraRegistry.Main.transform.position).normalized;
        //var cross = math.cross(math.cross(worldForward, camForward), worldForward);
        //transform.LookAt(CameraRegistry.Main.transform, cross);
        
        transform.rotation = quaternion.LookRotationSafe(-CameraRegistry.Main.transform.forward, CameraRegistry.Main.transform.up);
    }
}