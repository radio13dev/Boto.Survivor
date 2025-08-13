using UnityEngine;

[ExecuteInEditMode]
public class SizeCameraWithResolution : MonoBehaviour
{
    public float CombinedRate = 0;
    public float InvRate = 0;
    public float WidthRate = 0;
    public float HeightRate = 0.4f;
    Camera _camera;

    Camera Camera
    {
        get
        {
            if (!_camera) _camera = GetComponent<Camera>();
            return _camera;
        }
    }

    // Update is called once per frame
    [ExecuteInEditMode]
    void Update()
    {
        Camera.orthographicSize = CombinedRate * Screen.width / Screen.height
            + InvRate * Screen.height / Screen.width
            + WidthRate * Screen.width
            + HeightRate * Screen.height;   
    }
}
