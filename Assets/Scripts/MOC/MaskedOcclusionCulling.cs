using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace MOC
{
    public class MaskedOcclusionCulling : MonoBehaviour
    {
        public float rasterizeCostTime;
        
        [SerializeField] private Camera cam;
        [SerializeField] private MeshFilter[] occludersMeshFilters;
        [SerializeField] private MeshFilter[] occludeesMeshFilters;
        private NativeArray<Tile> _tiles;
        private Matrix4x4 _vpMatrix;
        private MeshFilter[] _occludedMeshFilters;

        private void Start()
        {
            _tiles = new NativeArray<Tile>(Constants.NumRowsTile * Constants.NumColsTile, Allocator.Persistent);
            ClearTiles();
        }

        private void OnDestroy()
        {
            _tiles.Dispose();
        }

        private void Update()
        {
            // Prepare
            _vpMatrix = cam.projectionMatrix * cam.worldToCameraMatrix;
            if (cam.transform.hasChanged)
            {
                ClearTiles();
                cam.transform.hasChanged = false;
            }
            
            // Step1: Rasterize Occluders
            var sw = new Stopwatch();
            sw.Start();
            RasterizeOccluders();
            sw.Stop();
            rasterizeCostTime = sw.ElapsedMilliseconds;
            
            // Step2: Test Occludees
            TestOccludees();
        }

        public void SetOccluders(MeshFilter[] meshFilters)
        {
            occludersMeshFilters = meshFilters;
        }

        public void SetOccludees(MeshFilter[] meshFilters)
        {
            occludeesMeshFilters = meshFilters;
        }
        
        public Tile[] GetTiles()
        {
            return _tiles.ToArray();
        }

        public MeshFilter[] GetOccludedMeshFilters()
        {
            return _occludedMeshFilters;
        }
        
        private void ClearTiles()
        {
            var defaultTile = new Tile
            {
                bitmask = uint4.zero,
                // z0 = 1.0f,
                // z1 = 0.0f
                z = 1.0f
            };
            for (var i = 0; i < _tiles.Length; i++)
            {
                _tiles[i] = defaultTile;
            }
            Debug.Log("Clear Tiles Done!");
        }

        private void RasterizeOccluders()
        {
            Profiler.BeginSample("RasterizeOccluders");
            foreach (var meshFilter in occludersMeshFilters)
            {
                RasterizeMesh(meshFilter);
            }
            Profiler.EndSample();
        }

        private void TestOccludees()
        {
            Profiler.BeginSample("TestOccludees");
            Profiler.BeginSample("FillArray");
            var bounds = new NativeArray<Bounds>(occludeesMeshFilters.Length, Allocator.TempJob);
            var modelMatrices = new NativeArray<float4x4>(occludeesMeshFilters.Length, Allocator.TempJob);
            var occludeResults = new NativeArray<bool>(occludeesMeshFilters.Length, Allocator.TempJob);
            for (var i = 0; i < occludeesMeshFilters.Length; i++)
            {
                var meshFilter = occludeesMeshFilters[i];
                bounds[i] = meshFilter.mesh.bounds;
                modelMatrices[i] = meshFilter.transform.localToWorldMatrix;
            }
            Profiler.EndSample();
            
            var testOccludeesJob = new TestOccludeesJob
            {
                Bounds = bounds,
                Tiles = _tiles,
                ModelMatrices = modelMatrices,
                VpMatrix = _vpMatrix,
                OccludeResults = occludeResults
            };
            var testOccludeesJobHandle = testOccludeesJob.Schedule(bounds.Length, 64);
            testOccludeesJobHandle.Complete();
            var numOccluded = occludeResults.Count(result => result);
            _occludedMeshFilters = new MeshFilter[numOccluded];
            var idx = 0;
            for (var i = 0; i < occludeResults.Length; i++)
            {
                if (occludeResults[i])
                {
                    _occludedMeshFilters[idx++] = occludeesMeshFilters[i];
                }
            }
            bounds.Dispose();
            modelMatrices.Dispose();
            occludeResults.Dispose();
            Profiler.EndSample();
        }
        
        private void RasterizeMesh(MeshFilter meshFilter)
        {
            Profiler.BeginSample(nameof(RasterizeMesh));
            var mesh = meshFilter.sharedMesh;
            var mvpMatrix = _vpMatrix * meshFilter.transform.localToWorldMatrix;

            var localSpaceVertices = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob);
            var screenSpaceVertices = new NativeArray<float3>(localSpaceVertices.Length, Allocator.TempJob);
            var transformVerticesJob = new TransformVerticesJob
            {
                LocalSpaceVertices = localSpaceVertices,
                MvpMatrix = mvpMatrix,
                ScreenSpaceVertices = screenSpaceVertices
            };
            var transformVerticesJobHandle = transformVerticesJob.Schedule(localSpaceVertices.Length, 64);

            Assert.IsTrue(mesh.triangles.Length % 3 == 0);
            var numTri = mesh.triangles.Length / 3;
            var indices = new NativeArray<int>(mesh.triangles, Allocator.TempJob);
            var tileRanges = new NativeArray<int4>(numTri, Allocator.TempJob);
            var edgeParams = new NativeArray<int3x3>(numTri, Allocator.TempJob);
            var depthParams = new NativeArray<float4>(numTri, Allocator.TempJob);
            var prepareTriangleInfosJob = new PrepareTriangleInfosJob
            {
                ScreenSpaceVertices = screenSpaceVertices,
                Indices = indices,
                TileRanges = tileRanges,
                EdgeParams = edgeParams,
                DepthParams = depthParams,
            };
            var prepareTriangleInfosJobHandle =
                prepareTriangleInfosJob.Schedule(numTri, 64, transformVerticesJobHandle);

            // var rasterizeTrianglesJob = new RasterizeTrianglesJob
            // {
            //     TileRanges = tileRanges,
            //     EdgeParams = edgeParams,
            //     DepthParams = depthParams,
            //     Tiles = _tiles,
            // };
            // var rasterizeTriangleJobHandle = rasterizeTrianglesJob.Schedule(numTri, 64, prepareTriangleInfosJobHandle);
            // rasterizeTriangleJobHandle.Complete();

            var binRasterizerJob = new BinRasterizerJob
            {
                TileRanges = tileRanges,
                EdgeParams = edgeParams,
                DepthParams = depthParams,
                Tiles = _tiles
            };
            var binRasterizerJobHandle = binRasterizerJob.Schedule(Constants.NumBins, 64, prepareTriangleInfosJobHandle);
            binRasterizerJobHandle.Complete();

            localSpaceVertices.Dispose();
            screenSpaceVertices.Dispose();
            indices.Dispose();
            tileRanges.Dispose();
            edgeParams.Dispose();
            depthParams.Dispose();
            
            Profiler.EndSample();
        }
    }
}