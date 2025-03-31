using System.Diagnostics;
using System.Linq;
using MOC;
using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CullingSystem : MonoBehaviour
{
    public CullingSystemStatData StatData;
    
    [SerializeField] private MeshRenderer[] occludersMeshRenderers;
    [SerializeField] private MeshRenderer[] occludeesMeshRenderers;
    private FrustumCulling _frustumCulling;
    private MaskedOcclusionCulling _maskedOcclusionCulling;
    private Camera _camera;
    private MeshRenderer[] _meshRenderers;
    private NativeArray<Bounds> _bounds;
    private NativeArray<bool> _cullingResults; // true -> invisible; false -> visible
    private readonly Stopwatch _stopwatch = new();
    // private int _occludedLayer;
    // private int _defaultLayer;

    private void Start()
    {
        // _occludedLayer = LayerMask.NameToLayer("MSOC_Occluded");
        // _defaultLayer = LayerMask.NameToLayer("Default");
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
        StatData.TotalObjectCount = _cullingResults.Length;
        StatData.FrustumCullingCount = -1; // 标记是否开始统计
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
        StatData.CostTimeFrustumCulling = _stopwatch.ElapsedTicks * 1000f / Stopwatch.Frequency;
        StatData.FrustumCullingCount = _cullingResults.Sum(it => it ? 1 : 0);

        _stopwatch.Restart();
        _maskedOcclusionCulling.Cull();
        _stopwatch.Stop();
        StatData.CostTimeMaskedOcclusionCulling = _stopwatch.ElapsedTicks * 1000f / Stopwatch.Frequency;
        StatData.MaskedOcclusionCullingCount = _cullingResults.Sum(it => it ? 1 : 0) - StatData.FrustumCullingCount;
        
        // for (var i = 0; i < _cullingResults.Length; i++)
        // {
        //     _meshRenderers[i].gameObject.layer = _cullingResults[i] ? _occludedLayer : _defaultLayer;
        // }
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

public struct CullingSystemStatData
{
    public float CostTimeFrustumCulling;
    public float CostTimeMaskedOcclusionCulling;
    public int TotalObjectCount;
    public int FrustumCullingCount;
    public int MaskedOcclusionCullingCount;
}