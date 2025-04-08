using System.Collections.Generic;
using MOC;
using UnityEngine;

[RequireComponent(typeof(CullingSystem))]
public class CullingVisualizer : MonoBehaviour
{
    [SerializeField] private bool enableVisOccluders;
    private CullingSystem _cullingSystem;
    private MaskedOcclusionCulling _occlusionCulling;

    private void Start()
    {
        _cullingSystem = GetComponent<CullingSystem>();
    }

    private void Update()
    {
        var meshRenderers = _cullingSystem.GetMeshRenderers();
        var cullingResults = _cullingSystem.GetCullingResults();
        _occlusionCulling ??= _cullingSystem.GetMaskedOcclusionCulling();
        var occluderFlags = _occlusionCulling.GetOccluderFlags();
        var numOccluders = occluderFlags.Length;
        var occludedMeshRenderers = new List<MeshRenderer>();
        var visibleMeshRenderers = new List<MeshRenderer>();
        var occluderMeshRenderers = new List<MeshRenderer>();
        for (var i = 0; i < cullingResults.Length; i++)
        {
            var meshRenderer = meshRenderers[i];
            if (cullingResults[i]) occludedMeshRenderers.Add(meshRenderer);
            else if (enableVisOccluders && i < numOccluders && occluderFlags[i]) occluderMeshRenderers.Add(meshRenderer);
            else visibleMeshRenderers.Add(meshRenderer);
        }
        ChangeRenderersColor(visibleMeshRenderers, Color.gray);
        ChangeRenderersColor(occluderMeshRenderers, Color.green);
        ChangeRenderersColor(occludedMeshRenderers, Color.red);
    }

    private static void ChangeRenderersColor(List<MeshRenderer> renderers, Color color)
    {
        foreach (var currRenderer in renderers)
        {
            currRenderer.material.color = color;
        }
    }
}