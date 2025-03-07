using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;

namespace ROC
{
    public class DepthBufferRasterizer : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private GameObject targetGameObject;
        [SerializeField] private MeshFilter[] meshFilters;
        [SerializeField] private Texture2D depthTexture;
        [SerializeField] private bool savePng;
        [SerializeField] private bool reversed;
        private NativeArray<float> _depthBuffer;
        
        private void Start()
        {
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
            if (_depthBuffer.IsCreated) _depthBuffer.Dispose();
            _depthBuffer = new NativeArray<float>(Constants.ScreenWidth * Constants.ScreenHeight, Allocator.Persistent);
            ClearDepthBuffer();
        }

        private void OnDestroy()
        {
            if (_depthBuffer.IsCreated) _depthBuffer.Dispose();
        }

        private void Update()
        {
            Profiler.BeginSample("DepthBufferRasterizer");
            Assert.IsTrue(cam && meshFilters != null);
            foreach (var meshFilter in meshFilters)
            {
                RasterizeMesh(meshFilter);
            }
            Profiler.EndSample();
        }

        private void ClearDepthBuffer()
        {
            const float clearDepth = 1.0f;
            for (var i = 0; i < _depthBuffer.Length; i++)
            {
                _depthBuffer[i] = clearDepth;
            }
        }
        
        public void ConvertToTexture()
        {
            if (!depthTexture || depthTexture.width != Constants.ScreenWidth || depthTexture.height != Constants.ScreenHeight)
            {
                depthTexture = new Texture2D(Constants.ScreenWidth, Constants.ScreenHeight, TextureFormat.ARGB32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
            }
            for (var y = 0; y < Constants.ScreenHeight; y++)
            {
                for (var x = 0; x < Constants.ScreenWidth; x++)
                {
                    var index = y * Constants.ScreenWidth + x;
                    var depth = reversed ? 1.0f - _depthBuffer[index] : _depthBuffer[index];
                    depthTexture.SetPixel(x, y, new Color(depth, depth, depth, 1.0f));
                }
            }

            if (!savePng) return;
            var pngBytes = depthTexture.EncodeToPNG();
            var path = $"Assets/Resources/gt_depth.png";
            File.WriteAllBytes(path, pngBytes);
            Debug.Log("Texture saved as PNG to: " + path);
        }

        private void RasterizeMesh(MeshFilter meshFilter)
        {
            Profiler.BeginSample("RasterizeMesh");
            
            // Step 1
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
                ScreenSpaceVertices = screenSpaceVertices,
                MvpMatrix = mvpMatrix
            };
            var transformVerticesJobHandle = transformVerticesJob.Schedule(localSpaceVertices.Length, 64);

            // Step 2
            var indices = new NativeArray<int>(mesh.triangles, Allocator.TempJob);
            var numTri = indices.Length / 3;
            var bounds = new NativeArray<int4>(numTri, Allocator.TempJob);
            var edgeParams = new NativeArray<int3x3>(numTri, Allocator.TempJob);
            var depthParams = new NativeArray<float3>(numTri, Allocator.TempJob);
            var collectTriangleInfosJob = new CollectTriangleInfosJob
            {
                ScreenSpaceVertices = screenSpaceVertices,
                Indices = indices,
                Bounds = bounds,
                EdgeParams = edgeParams,
                DepthParams = depthParams
            };
            var collectTriangleInfosJobHandle = collectTriangleInfosJob.Schedule(numTri, 64, transformVerticesJobHandle);

            // Step 3
            var rasterizeTrianglesJob = new RasterizeTrianglesJob
            {
                Bounds = bounds,
                EdgeParams = edgeParams,
                DepthParams = depthParams,
                DepthBuffer = _depthBuffer
            };
            var rasterizeTrianglesJobHandle =
                rasterizeTrianglesJob.Schedule(numTri, 64, collectTriangleInfosJobHandle);
            rasterizeTrianglesJobHandle.Complete();
            
            localSpaceVertices.Dispose();
            screenSpaceVertices.Dispose();
            indices.Dispose();
            bounds.Dispose();
            edgeParams.Dispose();
            depthParams.Dispose();
            Profiler.EndSample();
        }
    }
}