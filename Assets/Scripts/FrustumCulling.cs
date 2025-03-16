using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

public class FrustumCulling
{
    private readonly Camera _camera;
    private readonly Plane[] _planesArray;
    private readonly NativeArray<bool> _cullingResults;
    private readonly NativeArray<Bounds> _bounds;
    private NativeArray<Plane> _planes;
    
    public FrustumCulling(Camera cam, NativeArray<Bounds> bounds, NativeArray<bool> cullingResults)
    {
        _camera = cam;
        _bounds = bounds;
        _cullingResults = cullingResults;
        const int numFrustumPlanes = 6;
        _planesArray = new Plane[numFrustumPlanes];
        _planes = new NativeArray<Plane>(numFrustumPlanes, Allocator.Persistent);
    }

    public void Cull()
    {
        Profiler.BeginSample("FrustumCulling");
        
        Profiler.BeginSample("UpdateCamPlanes");
        unsafe
        {
            GeometryUtility.CalculateFrustumPlanes(_camera, _planesArray);
            fixed (Plane* pPlanesArray = _planesArray)
            {
                UnsafeUtility.MemCpy(_planes.GetUnsafePtr(), pPlanesArray, sizeof(Plane) * _planesArray.Length);
            }
        }
        Profiler.EndSample();

        var frustumCullingJob = new FrustumCullingJob
        {
            Planes = _planes,
            Bounds = _bounds,
            CullingResults = _cullingResults
        };
        var frustumCullingJobHandle = frustumCullingJob.Schedule(_bounds.Length, 64);
        frustumCullingJobHandle.Complete();
        Profiler.EndSample();
    }

    public void Dispose()
    {
        _planes.Dispose();
    }

    [BurstCompile]
    private struct FrustumCullingJob : IJobParallelFor // TODO: https://iquilezles.org/articles/frustumcorrect/
    {
        [ReadOnly] public NativeArray<Plane> Planes;
        [ReadOnly] public NativeArray<Bounds> Bounds;
        [WriteOnly] public NativeArray<bool> CullingResults;
    
        public void Execute(int index)
        {
            var bounds = Bounds[index];
            var isOccluded = false;
            
            foreach (var plane in Planes)
            {
                var center = bounds.center;
                var extents = bounds.extents;

                var distance = plane.GetDistanceToPoint(center);
                var radius = extents.x * Mathf.Abs(plane.normal.x)
                             + extents.y * Mathf.Abs(plane.normal.y)
                             + extents.z * Mathf.Abs(plane.normal.z);

                if (distance + radius < 0)
                {
                    isOccluded = true;
                    break;
                }
            }

            CullingResults[index] = isOccluded;
        }
    }
}