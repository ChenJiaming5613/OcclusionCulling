using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

[RequireComponent(typeof(CullingSystem), typeof(Camera))]
public class FastApplyOccludersAndOccludees : MonoBehaviour
{
    [SerializeField] private GameObject targetGameObject;
    private CullingSystem _cullingSystem;
    private Camera _camera;
        
    private void OnEnable()
    {
        _cullingSystem = GetComponent<CullingSystem>();
        _camera = GetComponent<Camera>();

        // Apply Occluders & Occludees
        Assert.IsTrue(targetGameObject != null);
        var occludersMeshRenderers = Array.Empty<MeshRenderer>();
        var occludeesMeshRenderers = Array.Empty<MeshRenderer>();
        var occluders = targetGameObject.transform.Find("Occluders");
        if (occluders != null)
        {
            occludersMeshRenderers = occluders.GetComponentsInChildren<MeshRenderer>()
                .Where(it => it.gameObject.activeSelf).ToArray();
            Debug.Log($"Num Occluders: {occludersMeshRenderers.Length}");
        }
        var occludees = targetGameObject.transform.Find("Occludees");
        if (occludees != null)
        {
            occludeesMeshRenderers = occludees.GetComponentsInChildren<MeshRenderer>()
                .Where(it => it.gameObject.activeSelf).ToArray();
            Debug.Log($"Num Occludees: {occludeesMeshRenderers.Length}");
        }
        _cullingSystem.SetOccludersAndOccludees(occludersMeshRenderers, occludeesMeshRenderers);
        
        // Apply Camera Pose
        var eye = targetGameObject.transform.Find("Eye");
        if (eye == null) return;
        _camera.transform.position = eye.position;
        _camera.transform.rotation = eye.rotation;
        Debug.Log("Applied Camera Posture!");
    }
}