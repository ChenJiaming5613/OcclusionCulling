using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;

namespace MOC
{
    public class MaskedOcclusionCulling : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private GameObject targetGameObject;
        [SerializeField] private MeshFilter[] meshFilters;
        private NativeArray<Tile> _tiles;

        private void Start()
        {
            Assert.IsTrue(meshFilters != null && cam);
            if (targetGameObject != null)
            {
                meshFilters = targetGameObject.GetComponentsInChildren<MeshFilter>()
                    .Where(it => it.gameObject.activeSelf).ToArray();
                var eye = targetGameObject.transform.Find("Eye");
                if (eye != null)
                {
                    cam.transform.position = eye.position;
                    cam.transform.rotation = eye.rotation;
                    Debug.Log("Applied Camera Posture!");
                }
            }
            _tiles = new NativeArray<Tile>(Constants.NumRowsTile * Constants.NumColsTile, Allocator.Persistent);
            ClearTiles();
        }

        private void OnDestroy()
        {
            _tiles.Dispose();
        }

        private void Update()
        {
            foreach (var meshFilter in meshFilters)
            {
                RasterizeMesh(meshFilter);
            }
        }

        public Tile[] GetTiles()
        {
            return _tiles.ToArray();
        }
        
        private void ClearTiles()
        {
            var defaultTile = new Tile
            {
                bitmask = uint4.zero,
                z0 = float.MaxValue,
                z1 = 0.0f
            };
            for (var i = 0; i < _tiles.Length; i++)
            {
                _tiles[i] = defaultTile;
            }
        }

        private void RasterizeMesh(MeshFilter meshFilter)
        {
            Profiler.BeginSample(nameof(RasterizeMesh));
            var mesh = meshFilter.sharedMesh;
            var mvpMatrixRaw = cam.projectionMatrix *
                               cam.worldToCameraMatrix *
                               meshFilter.transform.localToWorldMatrix;
            var mvpMatrix = new float4x4(
                mvpMatrixRaw.GetColumn(0), mvpMatrixRaw.GetColumn(1),
                mvpMatrixRaw.GetColumn(2), mvpMatrixRaw.GetColumn(3));

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

            var rasterizeTrianglesJob = new RasterizeTrianglesJob
            {
                TileRanges = tileRanges,
                EdgeParams = edgeParams,
                DepthParams = depthParams,
                Tiles = _tiles,
            };
            var rasterizeTriangleJobHandle = rasterizeTrianglesJob.Schedule(numTri, 64, prepareTriangleInfosJobHandle);
            rasterizeTriangleJobHandle.Complete();
            
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