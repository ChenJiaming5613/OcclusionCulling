using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace MOC
{
    public class MaskedOcclusionCulling
    {
        public MaskedOcclusionCullingStatData StatData;
        
        private readonly Camera _camera;
        private NativeArray<Tile> _tiles;
        private Matrix4x4 _vpMatrix;

        private NativeArray<float3> _localSpaceVertices;
        private NativeArray<int> _localSpaceVertexOffsets;
        private NativeArray<int> _indices;
        private NativeArray<int> _indexOffsets;
        private NativeArray<float4x4> _modelMatrices;
        private NativeArray<float4x4> _mvpMatrices;
        private NativeArray<int> _numTotalTris;
        private NativeArray<int> _fillOffsets;
        private NativeArray<int> _occluderNumTri;
        private NativeArray<float3> _screenSpaceVertices;
        private NativeArray<int4> _tileRanges;
        // private NativeArray<int3x3> _edgeParams;
        private NativeArray<float3> _invSlope;
        private NativeArray<int3> _vx;
        private NativeArray<int3> _vy;
        private NativeArray<bool3> _isRightEdge;
        private NativeArray<int3> _flatInfo;
        private NativeArray<float4> _depthParams;
        private NativeSlice<Bounds> _bounds;
        private readonly NativeSlice<bool> _occluderCullingResults;
        private readonly NativeSlice<bool> _occludeeCullingResults;

        private readonly Stopwatch _stopwatch = new();

        public MaskedOcclusionCulling(Camera camera,
            MeshRenderer[] occludersMeshRenderers, NativeSlice<Bounds> occludeesBounds,
            NativeArray<bool> cullingResults)
        {
            _camera = camera;
            _bounds = occludeesBounds;
            var occludersMeshFilters = occludersMeshRenderers.Select(it => it.GetComponent<MeshFilter>()).ToArray();
            var numOccluders = occludersMeshRenderers.Length;
            _occluderCullingResults = new NativeSlice<bool>(cullingResults, 0, numOccluders);
            _occludeeCullingResults = new NativeSlice<bool>(cullingResults, numOccluders);
            
            _tiles = new NativeArray<Tile>(Constants.NumRowsTile * Constants.NumColsTile, Allocator.Persistent);
            ClearTiles();

            var numTotalVertices = occludersMeshFilters.Sum(it => it.mesh.vertices.Length);
            var numTotalIndices = occludersMeshFilters.Sum(it => it.mesh.triangles.Length);
            _localSpaceVertices = new NativeArray<float3>(numTotalVertices, Allocator.Persistent);
            _indices = new NativeArray<int>(numTotalIndices, Allocator.Persistent);
            _localSpaceVertexOffsets = new NativeArray<int>(numOccluders, Allocator.Persistent);
            _indexOffsets = new NativeArray<int>(numOccluders, Allocator.Persistent);
            _modelMatrices = new NativeArray<float4x4>(numOccluders, Allocator.Persistent);
            _mvpMatrices = new NativeArray<float4x4>(numOccluders, Allocator.Persistent);
            _fillOffsets = new NativeArray<int>(numOccluders, Allocator.Persistent);
            _occluderNumTri = new NativeArray<int>(numOccluders, Allocator.Persistent);
            _numTotalTris = new NativeArray<int>(1, Allocator.Persistent);
            var numTris = 0;
            {
                var idxVertex = 0;
                var idxIndex = 0;
                var numVertices = 0;
                var numIndices = 0;
                for (var i = 0; i < numOccluders; i++)
                {
                    var meshFilter = occludersMeshFilters[i];
                    var mesh = meshFilter.sharedMesh;
                    foreach (var vertex in mesh.vertices)
                    {
                        _localSpaceVertices[idxVertex++] = vertex;
                    }
                    foreach (var triangle in mesh.triangles)
                    {
                        _indices[idxIndex++] = triangle;
                    }
                    _localSpaceVertexOffsets[i] = numVertices;
                    _indexOffsets[i] = numIndices;
                    numVertices += mesh.vertices.Length;
                    numIndices += mesh.triangles.Length;
                    _occluderNumTri[i] = mesh.triangles.Length / 3;
                    numTris += _occluderNumTri[i];
                    _modelMatrices[i] = occludersMeshFilters[i].transform.localToWorldMatrix;
                }
            }
            
            _screenSpaceVertices = new NativeArray<float3>(numTris * 3, Allocator.Persistent);
            _tileRanges = new NativeArray<int4>(numTris, Allocator.Persistent);
            // _edgeParams = new NativeArray<int3x3>(numTris, Allocator.Persistent);
            _invSlope = new NativeArray<float3>(numTris, Allocator.Persistent);
            _vx = new NativeArray<int3>(numTris, Allocator.Persistent);
            _vy = new NativeArray<int3>(numTris, Allocator.Persistent);
            _isRightEdge = new NativeArray<bool3>(numTris, Allocator.Persistent);
            _flatInfo = new NativeArray<int3>(numTris, Allocator.Persistent);
            _depthParams = new NativeArray<float4>(numTris, Allocator.Persistent);
        }

        public void Dispose()
        {
            _tiles.Dispose();
            
            _localSpaceVertices.Dispose();
            _localSpaceVertexOffsets.Dispose();
            _indices.Dispose();
            _indexOffsets.Dispose();
            _modelMatrices.Dispose();
            _mvpMatrices.Dispose();
            _numTotalTris.Dispose();
            _fillOffsets.Dispose();
            _occluderNumTri.Dispose();
            _screenSpaceVertices.Dispose();
            
            _tileRanges.Dispose();
            // _edgeParams.Dispose();
            _invSlope.Dispose();
            _vx.Dispose();
            _vy.Dispose();
            _isRightEdge.Dispose();
            _flatInfo.Dispose();
            _depthParams.Dispose();
        }

        public void Cull()
        {
            // Prepare
            _vpMatrix = _camera.projectionMatrix * _camera.worldToCameraMatrix;
            _stopwatch.Restart();
            ClearTiles();
            _stopwatch.Stop();
            StatData.CostTimeClear = _stopwatch.ElapsedTicks * 1000f / Stopwatch.Frequency;
            
            // Step1: Rasterize Occluders
            _stopwatch.Restart();
            RasterizeOccluders();
            _stopwatch.Stop();
            StatData.CostTimeOccluders = _stopwatch.ElapsedTicks * 1000f / Stopwatch.Frequency;
            
            // Step2: Test Occludees
            _stopwatch.Restart();
            TestOccludees();
            _stopwatch.Stop();
            StatData.CostTimeOccludees = _stopwatch.ElapsedTicks * 1000f / Stopwatch.Frequency;
        }

        public Tile[] GetTiles()
        {
            return _tiles.ToArray();
        }

        private void ClearTiles()
        {
            Profiler.BeginSample("Clear Tiles");
            unsafe
            {
                var defaultTile = new Tile
                {
                    bitmask = uint4.zero,
                    // z0 = 1.0f,
                    // z1 = 0.0f
                    z = 1.0f
                };
                UnsafeUtility.MemCpyReplicate(_tiles.GetUnsafePtr(), &defaultTile, sizeof(Tile), _tiles.Length);
            }
            Profiler.EndSample();
        }

        private void RasterizeOccluders()
        {
            Profiler.BeginSample("RasterizeOccluders");
            
            Profiler.BeginSample("TransformMesh");
            var numOccluders = _occluderCullingResults.Length;
            var updateMatricesJob = new UpdateMatricesJob
            {
                NumOccluders = numOccluders,
                VpMatrix = _vpMatrix,
                OccluderCullingResults = _occluderCullingResults,
                OccluderNumTri = _occluderNumTri,
                ModelMatrices = _modelMatrices,
                FillOffsets = _fillOffsets,
                MvpMatrices = _mvpMatrices,
                NumTotalTris = _numTotalTris
            };
            updateMatricesJob.Run();
            var numTotalTris = _numTotalTris[0];
            var transformVerticesJob = new TransformVerticesJob
            {
                LocalSpaceVertices = _localSpaceVertices,
                LocalSpaceVertexOffsets = _localSpaceVertexOffsets,
                Indices = _indices,
                IndexOffsets = _indexOffsets,
                MvpMatrices = _mvpMatrices,
                FillOffsets = _fillOffsets,
                CullingResults = _occluderCullingResults,
                ScreenSpaceVertices = _screenSpaceVertices
            };
            var transformVerticesJobHandle = transformVerticesJob.Schedule(numOccluders, 64);
            transformVerticesJobHandle.Complete();
            Profiler.EndSample();
            
            
            var prepareTriangleInfosJob = new PrepareTriangleInfosJob
            {
                ScreenSpaceVertices = _screenSpaceVertices,
                TileRanges = _tileRanges,
                // EdgeParams = _edgeParams,
                InvSlope = _invSlope,
                Vx = _vx,
                Vy = _vy,
                IsRightEdge = _isRightEdge,
                FlatInfo = _flatInfo,
                DepthParams = _depthParams,
            };
            var prepareTriangleInfosJobHandle =
                prepareTriangleInfosJob.Schedule(numTotalTris, 64);

            // var rasterizeTrianglesJob = new RasterizeTrianglesJob
            // {
            //     TileRanges = _tileRanges,
            //     EdgeParams = _edgeParams,
            //     DepthParams = _depthParams,
            //     Tiles = _tiles,
            // };
            // // var rasterizeTriangleJobHandle = rasterizeTrianglesJob.Schedule(numTotalTris, 64, prepareTriangleInfosJobHandle);
            // // rasterizeTriangleJobHandle.Complete();
            // prepareTriangleInfosJobHandle.Complete();
            // rasterizeTrianglesJob.Run(numTotalTris);

            var binRasterizerJob = new BinRasterizerJob
            {
                NumTris = numTotalTris,
                TileRanges = _tileRanges,
                // EdgeParams = _edgeParams,
                InvSlope = _invSlope,
                Vx = _vx,
                Vy = _vy,
                IsRightEdge = _isRightEdge,
                FlatInfo = _flatInfo,
                DepthParams = _depthParams,
                Tiles = _tiles
            };
            prepareTriangleInfosJobHandle.Complete();
            // StatData.TotalTriCount = numTotalTris;
            // var clippingTriCount = 0;
            // for (var i = 0; i < numTotalTris; i++)
            // {
            //     if (_tileRanges[i].x == -1) clippingTriCount++;
            // }
            // StatData.ClippedTriCount = clippingTriCount;
            var binRasterizerJobHandle = binRasterizerJob.Schedule(Constants.NumBins, 1);
            binRasterizerJobHandle.Complete();
            // binRasterizerJob.Run(Constants.NumBins);
            Profiler.EndSample();
        }

        private void TestOccludees()
        {
            Profiler.BeginSample("TestOccludees");
            var testOccludeesJob = new TestOccludeesJob
            {
                Bounds = _bounds,
                Tiles = _tiles,
                VpMatrix = _vpMatrix,
                CullingResults = _occludeeCullingResults
            };
            var testOccludeesJobHandle = testOccludeesJob.Schedule(_bounds.Length, 64);
            testOccludeesJobHandle.Complete();
            Profiler.EndSample();
        }
    }

    public struct MaskedOcclusionCullingStatData
    {
        public float CostTimeOccluders;
        public float CostTimeOccludees;
        public float CostTimeClear;
        // public int TotalTriCount;
        // public int ClippedTriCount; // backface culling & near plane clipping & zero triangle clipping
    }
}