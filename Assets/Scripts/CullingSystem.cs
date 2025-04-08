using System.Diagnostics;
using System.Linq;
using MOC;
using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CullingSystem : MonoBehaviour
{
    public CullingSystemStatData StatData;
    [SerializeField] private MocConfigAsset configAsset;

    private FrustumCulling _frustumCulling;
    private MaskedOcclusionCulling _maskedOcclusionCulling;
    private Camera _camera;
    private MeshRenderer[] _meshRenderers;
    private MeshRenderer[] _occluderMeshRenderers;
    private NativeArray<Bounds> _bounds;
    private NativeArray<bool> _cullingResults; // true -> invisible; false -> visible
    private readonly Stopwatch _stopwatch = new();

    private void Start()
    {
        _camera = GetComponent<Camera>();

        var numObjects = _meshRenderers.Length;
        _bounds = new NativeArray<Bounds>(numObjects, Allocator.Persistent);
        for (var i = 0; i < numObjects; i++) // TODO: only process static objects
        {
            _bounds[i] = _meshRenderers[i].bounds;
        }
        _cullingResults = new NativeArray<bool>(numObjects, Allocator.Persistent);

        _frustumCulling = new FrustumCulling(_camera, _bounds, _cullingResults);
        _maskedOcclusionCulling = new MaskedOcclusionCulling(configAsset, _camera, _bounds, _occluderMeshRenderers, _cullingResults);

        StatData.TotalObjectCount = numObjects;
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
        _maskedOcclusionCulling.SyncPrevFrame();

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
    }

    public void SetMeshRenderers(MeshRenderer[] occluderMeshRenderers, MeshRenderer[] occludeeMeshRenderers)
    {
        _occluderMeshRenderers = occluderMeshRenderers;
        _meshRenderers = new MeshRenderer[occluderMeshRenderers.Length + occludeeMeshRenderers.Length];
        var idx = 0;
        foreach (var meshRenderer in occluderMeshRenderers)
        {
            _meshRenderers[idx++] = meshRenderer;
        }
        foreach (var meshRenderer in occludeeMeshRenderers)
        {
            _meshRenderers[idx++] = meshRenderer;
        }
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