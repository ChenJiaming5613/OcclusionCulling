using System.Diagnostics;
using MOC;
using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CullingSystem : MonoBehaviour
{
    public float costTimeFrustumCulling;
    public float costTimeMaskedOcclusionCulling;
    
    [SerializeField] private MeshRenderer[] occludersMeshRenderers;
    [SerializeField] private MeshRenderer[] occludeesMeshRenderers;
    private FrustumCulling _frustumCulling;
    private MaskedOcclusionCulling _maskedOcclusionCulling;
    private Camera _camera;
    private MeshRenderer[] _meshRenderers;
    private NativeArray<Bounds> _bounds;
    private NativeArray<bool> _cullingResults; // true -> invisible; false -> visible
    private readonly Stopwatch _stopwatch = new();

    private void Start()
    {
        _camera = GetComponent<Camera>();
        
        _meshRenderers = new MeshRenderer[occludersMeshRenderers.Length + occludeesMeshRenderers.Length];
        {
            var idx = 0;
            foreach (var meshRenderer in occludersMeshRenderers)
            {
                _meshRenderers[idx++] = meshRenderer;
            }

            foreach (var meshRenderer in occludeesMeshRenderers)
            {
                _meshRenderers[idx++] = meshRenderer;
            }
        }
        
        _bounds = new NativeArray<Bounds>(_meshRenderers.Length, Allocator.Persistent);
        for (var i = 0; i < _bounds.Length; i++) // TODO: only process static objects
        {
            var meshRenderer = _meshRenderers[i];
            _bounds[i] = meshRenderer.bounds;
        }
        _cullingResults = new NativeArray<bool>(_meshRenderers.Length, Allocator.Persistent);
        _frustumCulling = new FrustumCulling(_camera, _bounds, _cullingResults);
        _maskedOcclusionCulling =
            new MaskedOcclusionCulling(_camera, occludersMeshRenderers,
                new NativeSlice<Bounds>(_bounds, occludersMeshRenderers.Length), _cullingResults);
    }

    private void OnDestroy()
    {
        _frustumCulling.Dispose();
        _maskedOcclusionCulling.Dispose();
        _bounds.Dispose();
        _cullingResults.Dispose();
    }

    private void Update()
    {
        _stopwatch.Restart();
        _frustumCulling.Cull();
        _stopwatch.Stop();
        costTimeFrustumCulling = _stopwatch.ElapsedTicks * 1000f / Stopwatch.Frequency;

        _stopwatch.Restart();
        _maskedOcclusionCulling.Cull();
        _stopwatch.Stop();
        costTimeMaskedOcclusionCulling = _stopwatch.ElapsedTicks * 1000f / Stopwatch.Frequency;
    }

    public void SetOccludersAndOccludees(MeshRenderer[] occluders, MeshRenderer[] occludees)
    {
        occludersMeshRenderers = occluders;
        occludeesMeshRenderers = occludees;
    }

    public MeshRenderer[] GetMeshRenderers()
    {
        return _meshRenderers;
    }
    
    public NativeArray<bool> GetCullingResults()
    {
        return _cullingResults;
    }
    
    public FrustumCulling GetFrustumCulling()
    {
        return _frustumCulling;
    }

    public MaskedOcclusionCulling GetMaskedOcclusionCulling()
    {
        return _maskedOcclusionCulling;
    }
}