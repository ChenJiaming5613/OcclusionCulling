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

        private readonly MocConfig _config;
        private readonly Camera _camera;
        private readonly int _numBins;
        private NativeArray<Tile> _tiles;
        private Matrix4x4 _vpMatrix;

        private readonly int _numObjects;
        private readonly NativeArray<Bounds> _bounds;
        private readonly NativeArray<bool> _cullingResults;
        private JobHandle _rasterizeOccludersJobHandle;

        // occluders
        private readonly int _numOccluders;
        private NativeArray<float3> _localSpaceVertices;
        private NativeArray<int> _localSpaceVertexOffsets;
        private NativeArray<int> _indices;
        private NativeArray<int> _indexOffsets;
        private NativeArray<float4x4> _modelMatrices;
        private NativeArray<float4x4> _mvpMatrices;
        private NativeArray<bool> _occluderFlags;
        private NativeArray<int> _occluderNumTri;
        private NativeArray<int> _fillOffsets;
        private NativeArray<OccluderSortInfo> _occluderInfos;

        // triangles
        private NativeArray<int> _numRasterizeTris;
        private NativeArray<float3> _screenSpaceVertices;
        private NativeArray<int4> _tileRanges;
        private NativeArray<float3> _invSlope;
        private NativeArray<int3> _vx;
        private NativeArray<int3> _vy;
        private NativeArray<bool3> _isRightEdge;
        private NativeArray<int3> _flatInfo;
        private NativeArray<float4> _depthParams;
        
        private TerrainRayMarching.TerrainRayMarching _terrainRayMarching;

        private readonly Stopwatch _stopwatch = new();

        /// <summary>
        /// 所有遮挡物的bound和cullingResult信息按occluderMeshRenderers排列顺序存储在bounds和cullingResults的最前面
        /// </summary>
        /// <param name="configAsset">MOC算法配置</param>
        /// <param name="camera">参与遮挡剔除的相机</param>
        /// <param name="bounds">所有物体的包围盒（世界空间）</param>
        /// <param name="occluderMeshRenderers">所有遮挡物的MeshRenderer</param>
        /// <param name="cullingResults">所有物体的剔除结果：true为剔除，false为不剔除</param>
        public MaskedOcclusionCulling(MocConfigAsset configAsset, Camera camera, NativeArray<Bounds> bounds, MeshRenderer[] occluderMeshRenderers,
            NativeArray<bool> cullingResults)
        {
            _config = new MocConfig(configAsset);
            _camera = camera;
            _numBins = _config.NumBinCols * _config.NumBinRows;
            _numObjects = bounds.Length;
            _bounds = bounds;
            _cullingResults = cullingResults;
            
            _tiles = new NativeArray<Tile>(_config.NumRowsTile * _config.NumColsTile, Allocator.Persistent);
            ClearTiles();
            
            // occluders
            _numOccluders = occluderMeshRenderers.Length;
            var occluderMeshFilters = occluderMeshRenderers.Select(it => it.GetComponent<MeshFilter>()).ToArray();
            var numOccluderVertices = occluderMeshFilters.Sum(it => it.mesh.vertices.Length);
            var numOccluderIndices = occluderMeshFilters.Sum(it => it.mesh.triangles.Length);
            _localSpaceVertices = new NativeArray<float3>(numOccluderVertices, Allocator.Persistent);
            _indices = new NativeArray<int>(numOccluderIndices, Allocator.Persistent);
            _localSpaceVertexOffsets = new NativeArray<int>(_numOccluders, Allocator.Persistent);
            _indexOffsets = new NativeArray<int>(_numOccluders, Allocator.Persistent);
            _modelMatrices = new NativeArray<float4x4>(_numOccluders, Allocator.Persistent);
            _mvpMatrices = new NativeArray<float4x4>(_numOccluders, Allocator.Persistent);
            _occluderFlags = new NativeArray<bool>(_numOccluders, Allocator.Persistent);
            _occluderNumTri = new NativeArray<int>(_numOccluders, Allocator.Persistent);
            _fillOffsets = new NativeArray<int>(_numOccluders, Allocator.Persistent);
            _occluderInfos = new NativeArray<OccluderSortInfo>(_numOccluders, Allocator.Persistent);
            // var numTotalTris = 0;
            {
                var idxVertex = 0;
                var idxIndex = 0;
                var numVertices = 0;
                var numIndices = 0;
                for (var i = 0; i < _numOccluders; i++)
                {
                    var meshFilter = occluderMeshFilters[i];
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
                    // numTotalTris += _occluderNumTri[i];
                    _modelMatrices[i] = occluderMeshFilters[i].transform.localToWorldMatrix; // TODO: only process static occluders
                }
            }

            // triangles
            var maxNumRasterizeTris = _config.MaxNumRasterizeTris;
            _numRasterizeTris = new NativeArray<int>(1, Allocator.Persistent);
            _screenSpaceVertices = new NativeArray<float3>(maxNumRasterizeTris * 3, Allocator.Persistent);
            _tileRanges = new NativeArray<int4>(maxNumRasterizeTris, Allocator.Persistent);
            _invSlope = new NativeArray<float3>(maxNumRasterizeTris, Allocator.Persistent);
            _vx = new NativeArray<int3>(maxNumRasterizeTris, Allocator.Persistent);
            _vy = new NativeArray<int3>(maxNumRasterizeTris, Allocator.Persistent);
            _isRightEdge = new NativeArray<bool3>(maxNumRasterizeTris, Allocator.Persistent);
            _flatInfo = new NativeArray<int3>(maxNumRasterizeTris, Allocator.Persistent);
            _depthParams = new NativeArray<float4>(maxNumRasterizeTris, Allocator.Persistent);
        }

        public void VisualizeDepthTexture()
        {
            _terrainRayMarching.VisualizeDepthTexture();
        }

        public void Dispose()
        {
            SyncPrevFrame();

            _tiles.Dispose();
            
            // occluders
            _localSpaceVertices.Dispose();
            _localSpaceVertexOffsets.Dispose();
            _indices.Dispose();
            _indexOffsets.Dispose();
            _modelMatrices.Dispose();
            _mvpMatrices.Dispose();
            _occluderFlags.Dispose();
            _occluderNumTri.Dispose();
            _fillOffsets.Dispose();
            _occluderInfos.Dispose();

            // triangles
            _numRasterizeTris.Dispose();
            _screenSpaceVertices.Dispose();
            _tileRanges.Dispose();
            _invSlope.Dispose();
            _vx.Dispose();
            _vy.Dispose();
            _isRightEdge.Dispose();
            _flatInfo.Dispose();
            _depthParams.Dispose();
            
            _terrainRayMarching?.Dispose();
        }

        public void SyncPrevFrame()
        {
            if (_config.AsyncRasterizeOccluders)
            {
                _rasterizeOccludersJobHandle.Complete();
            }
        }
        
        public void Cull()
        {
            // Step0: Select Occluders
            UpdateMvpMatrixAndSelectOccluders().Complete();

            if (!_config.AsyncRasterizeOccluders) ClearAndRasterizeOccluders();
            
            // Step2: Test Occludees
            _stopwatch.Restart();
            Profiler.BeginSample("TestOccludees");
            TestOccludees().Complete();
            Profiler.EndSample();
            _stopwatch.Stop();
            StatData.CostTimeOccludees = GetCostTime();
            
            if (_config.AsyncRasterizeOccluders) ClearAndRasterizeOccluders();
        }

        public void SetTerrain(Terrain terrain)
        {
            _terrainRayMarching = new TerrainRayMarching.TerrainRayMarching(terrain, _camera, _config, _tiles);
        }

        private void ClearAndRasterizeOccluders()
        {
            // Step0: Clear Tiles
            _stopwatch.Restart();
            Profiler.BeginSample("ClearTiles");
            ClearTiles();
            Profiler.EndSample();
            _stopwatch.Stop();
            StatData.CostTimeClear = GetCostTime();

            // Step1: Rasterize Occluders
            _stopwatch.Restart();
            Profiler.BeginSample("RasterizeOccluders");
            _rasterizeOccludersJobHandle = RasterizeOccluders();
            if (!_config.AsyncRasterizeOccluders) _rasterizeOccludersJobHandle.Complete();
            Profiler.EndSample();
            _stopwatch.Stop();
            StatData.CostTimeOccluders = GetCostTime();
        }

        public MocConfig GetConfig()
        {
            return _config;
        }

        public Tile[] GetTiles()
        {
            return _tiles.ToArray();
        }
        
        public NativeArray<bool> GetOccluderFlags()
        {
            return _occluderFlags;
        }

        private void ClearTiles()
        {
            unsafe
            {
                var defaultTile = new Tile
                {
                    bitmask = uint4.zero,
                    #if MOC_REVERSED_Z
                    z = 0.0f
                    #else
                    z = 1.0f
                    #endif
                };
                UnsafeUtility.MemCpyReplicate(_tiles.GetUnsafePtr(), &defaultTile, sizeof(Tile), _tiles.Length);
            }
        }

        private JobHandle UpdateMvpMatrixAndSelectOccluders()
        {
            _vpMatrix = GetProjectionMatrix() * _camera.worldToCameraMatrix;
            _terrainRayMarching?.UpdateVpMatrix(_vpMatrix);

            var updateMvpAndSelectOccludersJob = new UpdateMvpAndSelectOccludersJob
            {
                CullingResults = _cullingResults,
                Bounds = _bounds,
                ModelMatrices = _modelMatrices,
                VpMatrix = _vpMatrix,
                CoverageThreshold = _config.CoverageThreshold,
                MvpMatrices = _mvpMatrices,
                OccluderFlags = _occluderFlags,
                OccluderInfos = _occluderInfos,
            };
            return updateMvpAndSelectOccludersJob.Schedule(_numOccluders, 64);
        }

        private JobHandle RasterizeOccluders()
        {
            JobHandle terrainRayMarchingJobHandle = default;
            if (_terrainRayMarching != null)
            {
                terrainRayMarchingJobHandle = _terrainRayMarching.RayMarching();
            }
            
            var updateMatricesJob = new UpdateFillOffsetsJob
            {
                MaxNumRasterizeTris = _config.MaxNumRasterizeTris,
                NumOccluders = _numOccluders,
                // CullingResults = _cullingResults,
                // OccluderFlags = _occluderFlags,
                OccluderNumTri = _occluderNumTri,
                FillOffsets = _fillOffsets,
                NumRasterizeTris = _numRasterizeTris,
                OccluderInfos = _occluderInfos
            };
            updateMatricesJob.Run();

            var transformVerticesJob = new TransformVerticesJob
            {
                DepthBufferWidth = _config.DepthBufferWidth,
                DepthBufferHeight = _config.DepthBufferHeight,
                LocalSpaceVertices = _localSpaceVertices,
                LocalSpaceVertexOffsets = _localSpaceVertexOffsets,
                Indices = _indices,
                IndexOffsets = _indexOffsets,
                MvpMatrices = _mvpMatrices,
                FillOffsets = _fillOffsets,
                // CullingResults = _cullingResults,
                // OccluderFlags = _occluderFlags,
                OccluderInfos = _occluderInfos,
                ScreenSpaceVertices = _screenSpaceVertices
            };
            var transformVerticesJobHandle = transformVerticesJob.Schedule(_numOccluders, 64);

            var prepareTriangleInfosJob = new PrepareTriangleInfosJob
            {
                NumRowsTile = _config.NumRowsTile,
                NumColsTile = _config.NumColsTile,
                ScreenSpaceVertices = _screenSpaceVertices,
                TileRanges = _tileRanges,
                InvSlope = _invSlope,
                Vx = _vx,
                Vy = _vy,
                IsRightEdge = _isRightEdge,
                FlatInfo = _flatInfo,
                DepthParams = _depthParams,
            };
            var numTotalTris = _numRasterizeTris[0];
            var prepareTriangleInfosJobHandle =
                prepareTriangleInfosJob.Schedule(numTotalTris, 64, transformVerticesJobHandle);

            var binRasterizerJob = new BinRasterizerJob
            {
                NumTris = numTotalTris,
                NumRowsTile = _config.NumRowsTile,
                NumColsTile = _config.NumColsTile,
                NumColsTileInBin = _config.NumColsTileInBin,
                NumRowsTileInBin = _config.NumRowsTileInBin,
                NumBinCols = _config.NumBinCols,
                NumBinRows = _config.NumBinRows,
                TileRanges = _tileRanges,
                InvSlope = _invSlope,
                Vx = _vx,
                Vy = _vy,
                IsRightEdge = _isRightEdge,
                FlatInfo = _flatInfo,
                DepthParams = _depthParams,
                Tiles = _tiles
            };
            // StatData.TotalTriCount = numTotalTris;
            // var clippingTriCount = 0;
            // for (var i = 0; i < numTotalTris; i++)
            // {
            //     if (_tileRanges[i].x == -1) clippingTriCount++;
            // }
            // StatData.ClippedTriCount = clippingTriCount;
            
            // binRasterizerJob.Run(Constants.NumBins);
            return binRasterizerJob.Schedule(_numBins, 1,
                JobHandle.CombineDependencies(prepareTriangleInfosJobHandle, terrainRayMarchingJobHandle));
        }

        private JobHandle TestOccludees()
        {
            var testOccludeesJob = new TestOccludeesJob
            {
                NumOccluders = _numOccluders,
                DepthBufferWidth = _config.DepthBufferWidth,
                DepthBufferHeight = _config.DepthBufferHeight,
                NumColsTile = _config.NumColsTile,
                VpMatrix = _vpMatrix,
                Bounds = _bounds,
                OccluderFlags = _occluderFlags,
                Tiles = _tiles,
                CullingResults = _cullingResults
            };
            return testOccludeesJob.Schedule(_numObjects, 64);
        }

        private Matrix4x4 GetProjectionMatrix()
        {
            #if MOC_REVERSED_Z
            return GL.GetGPUProjectionMatrix(_camera.projectionMatrix, false);
            #else
            return _camera.projectionMatrix;
            #endif
        }

        private float GetCostTime()
        {
            return _stopwatch.ElapsedTicks * 1000f / Stopwatch.Frequency;
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