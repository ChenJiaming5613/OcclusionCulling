using UnityEngine;

public class SetPose : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private Vector3 position = new(-221.3f, 22.642109f, 59.1f);
    [SerializeField] private Quaternion rotation = new(0.013601886f, 0.66943413f, -0.012701101f, 0.7426383f);
    [SerializeField] private float nearClipPlane = 0.3f;
    [SerializeField] private float farClipPlane = 1000.0f;
    [SerializeField] private float fieldOfView = 60.0f;
    
    
    [ContextMenu("Execute")]
    public void Execute()
    {
        cam.transform.SetPositionAndRotation(position, rotation);
        cam.nearClipPlane = nearClipPlane;
        cam.farClipPlane = farClipPlane;
        cam.fieldOfView = fieldOfView;
    }
}