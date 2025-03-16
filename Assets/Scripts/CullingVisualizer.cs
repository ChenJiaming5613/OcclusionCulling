using System.Collections.Generic;
using System.Linq;
using MOC;
using UnityEngine;

[RequireComponent(typeof(CullingSystem))]
public class CullingVisualizer : MonoBehaviour
{
    private CullingSystem _cullingSystem;
    private MaskedOcclusionCulling _occlusionCulling;
    private readonly Dictionary<Renderer, Color> _rendererToColor = new();

    private void Start()
    {
        _cullingSystem = GetComponent<CullingSystem>();
    }

    private void Update()
    {
        var occludeesMeshRenderers = _cullingSystem.GetMeshRenderers();
        var occludeResults = _cullingSystem.GetCullingResults();
        var occludedMeshRenderers = new List<MeshRenderer>();
        for (var i = 0; i < occludeResults.Length; i++)
        {
            if (occludeResults[i]) occludedMeshRenderers.Add(occludeesMeshRenderers[i]);
        }
            
        var meshFiltersSet = new HashSet<Renderer>();
        foreach (var meshRenderer in occludedMeshRenderers)
        {
            if (!_rendererToColor.ContainsKey(meshRenderer))
            {
                _rendererToColor.Add(meshRenderer, meshRenderer.material.color);
            }
            meshRenderer.material.color = Color.red;
            meshFiltersSet.Add(meshRenderer);
        }
        foreach (var pair in _rendererToColor.Where(pair => !meshFiltersSet.Contains(pair.Key)))
        {
            pair.Key.material.color = pair.Value;
        }
    }
}