using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(CullingSystem))]
public class CullingExecute : MonoBehaviour
{
    private int _occludedLayer;
    private int _defaultLayer;
    private CullingSystem _cullingSystem;
    private NativeArray<bool> _cullingResults;
    private MeshRenderer[] _meshRenderers;

    private void Start()
    {
        _occludedLayer = LayerMask.NameToLayer("MSOC_Occluded");
        _defaultLayer = LayerMask.NameToLayer("Default");
    }

    private void LateUpdate()
    {
        if (!_cullingSystem)
        {
            _cullingSystem = GetComponent<CullingSystem>();
            _cullingResults = _cullingSystem.GetCullingResults();
            _meshRenderers = _cullingSystem.GetMeshRenderers();
        }
        for (var i = 0; i < _cullingResults.Length; i++)
        {
           _meshRenderers[i].gameObject.layer = _cullingResults[i] ? _occludedLayer : _defaultLayer;
        }
    }
}