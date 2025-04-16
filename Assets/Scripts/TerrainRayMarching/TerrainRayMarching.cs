using System.Diagnostics;
using System.IO;
using StatUtils;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace TerrainRayMarching
{
    // https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TerrainData.html
    public class TerrainRayMarching : MonoBehaviour
    {
        private struct CamParamsCache // 当camera的fov,aspect,near改变时需要更新
        {
            public float Near;
            public float Far;
            public float HalfWidth;
            public float HalfHeight;
            public float RightStep;
            public float UpStep;
        }

        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private int maxCount;
        [SerializeField] private Texture2D depthTexture;
        [SerializeField] private Terrain terrain;
        [SerializeField] private Camera cam;
        [SerializeField] private int2 depthBufferSize = new(960, 540);
        [SerializeField] private int2 numBinXY = new(10, 10);
        [SerializeField] private float rayMarchingStep = 0.1f;
        [SerializeField] private int downSampleCount = 3;
        [SerializeField] private float3 terrainSize;
        [SerializeField] private int originalHeightmapSize;
        [SerializeField] private int heightmapSize;
        [SerializeField] private bool savePng;
        private CamParamsCache _camParamsCache;
        private float3 _camPos;
        private float3 _bottomLeftCorner;
        private float3 _rightStep;
        private float3 _upStep;

        private NativeArray<float> _heightmap;
        private NativeArray<float> _depthBuffer;
        private int2 _binSize;
        private int _numBins;

        private readonly Stopwatch _stopwatch = new();
        private Indicator _rayMarchingIndicator;
        private int _count;

        private void Start()
        {
            terrainSize = terrain.terrainData.size;
            UpdateCamParamsCache();
            _depthBuffer = new NativeArray<float>(depthBufferSize.x * depthBufferSize.y, Allocator.Persistent);
            depthTexture = new Texture2D(depthBufferSize.x, depthBufferSize.y, TextureFormat.RFloat, false);
            _binSize = depthBufferSize / numBinXY;
            _numBins = numBinXY.x * numBinXY.y;
            
            AllocHeightMap();
        }

        private void Update()
        {
            Profiler.BeginSample("Terrain RayMarching");
            
            Profiler.BeginSample("Update Frustum Steps");
            UpdateFrustumSteps();
            Profiler.EndSample();
            
            _stopwatch.Restart();
            // RayMarching();
            RayMarchingAsync();
            _stopwatch.Stop();
            if (_count < maxCount)
            {
                _rayMarchingIndicator.Tick(_stopwatch.ElapsedTicks * 1000f / Stopwatch.Frequency);
                _count++;
            }
            text.text = _rayMarchingIndicator.GetStatusStr() + $"\nCount: {_count}";
            Profiler.EndSample();
        }

        [ContextMenu("Visualize Depth Texture")]
        private void VisualizeDepthTexture()
        {
            UpdateDepthTexture();
            if (savePng) SaveTextureAsPNG("Assets/Resources/TerrainRayMarching.png");
        }

        private void UpdateCamParamsCache()
        {
            var fov = cam.fieldOfView;
            var aspect = cam.aspect;
            _camParamsCache.Near = cam.nearClipPlane;
            _camParamsCache.Far = cam.farClipPlane;
            _camParamsCache.HalfHeight = _camParamsCache.Near * math.tan(math.radians(fov) * 0.5f);
            _camParamsCache.HalfWidth = _camParamsCache.HalfHeight * aspect;
            _camParamsCache.RightStep = _camParamsCache.HalfWidth * 2 / depthBufferSize.x;
            _camParamsCache.UpStep = _camParamsCache.HalfHeight * 2 / depthBufferSize.y;
        }
        
        private void UpdateFrustumSteps()
        {
            float3 forward = cam.transform.forward;
            float3 right = cam.transform.right;
            float3 up = cam.transform.up;

            _camPos = cam.transform.position;
            var nearCenter = _camPos + forward * _camParamsCache.Near;
            _bottomLeftCorner = nearCenter - right * _camParamsCache.HalfWidth - up * _camParamsCache.HalfHeight;
            _rightStep = right * _camParamsCache.RightStep;
            _upStep = up * _camParamsCache.UpStep;
        }

        private void RayMarching()
        {
            for (var y = 0; y < depthBufferSize.y; y++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("RayMarching", $"RayMarching {y} / {depthBufferSize.y}",
                        y * 1.0f / depthBufferSize.y))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogWarning("Processing cancelled by user.");
                    break;
                }

                for (var x = 0; x < depthBufferSize.x; x++)
                {
                    var point = _bottomLeftCorner + x * _rightStep + y * _upStep;
                    var dir = math.normalize(point - _camPos);
                    var depth = _camParamsCache.Near;
                    while (point.y > SampleHeight0(point))
                    {
                        if (!(point.x >= 0f && point.x < terrainSize.x && point.z >= 0f && point.z < terrainSize.z))
                        {
                            depth = _camParamsCache.Far;
                            break;
                        }
                        
                        // TODO: 提前退出：当point已经高于terrain最大高度并且是向上移动
                        
                        point += dir * rayMarchingStep;
                        depth += rayMarchingStep;
                    }
                    var depth01 = (depth - _camParamsCache.Near) / (_camParamsCache.Far - _camParamsCache.Near);
                    _depthBuffer[y * depthBufferSize.x + x] = depth01;
                }
            }
            EditorUtility.ClearProgressBar();
        }

        private void RayMarchingAsync()
        {
            var numPixels = _depthBuffer.Length;
            var rayMarchingJob = new RayMarchingJob
            {
                CamPos = _camPos,
                BottomLeftCorner = _bottomLeftCorner,
                RightStep = _rightStep,
                UpStep = _upStep,
                DepthBufferSize = depthBufferSize,
                BinSize = _binSize,
                NumBinXY = numBinXY,
                HeightmapSize = heightmapSize,
                TerrainSize = terrainSize,
                Near = _camParamsCache.Near,
                Far = _camParamsCache.Far,
                RayMarchingStep = rayMarchingStep,
                Heightmap = _heightmap,
                DepthBuffer = _depthBuffer
            };
            var rayMarchingJobHandle = rayMarchingJob.Schedule(_numBins, 1);

            var interpolateDepthBufferJob = new InterpolateDepthBufferJob
            {
                DepthBufferSize = depthBufferSize,
                DepthBuffer = _depthBuffer
            };
            interpolateDepthBufferJob.Schedule(numPixels, 64, rayMarchingJobHandle).Complete();
        }

        private void UpdateDepthTexture()
        {
            for (var y = 0; y < depthBufferSize.y; y++)
            {
                for (var x = 0; x < depthBufferSize.x; x++)
                {
                    var depth = _depthBuffer[y * depthBufferSize.x + x];
                    depthTexture.SetPixel(x, y, new Color(depth, 0, 0));
                }
            }
        }

        private void AllocHeightMap()
        {
            var terrainData = terrain.terrainData;
            originalHeightmapSize = terrainData.heightmapResolution;
            heightmapSize = originalHeightmapSize / downSampleCount;
            var heights = terrainData.GetHeights(0, 0, originalHeightmapSize, originalHeightmapSize);
            _heightmap = new NativeArray<float>(heightmapSize * heightmapSize, Allocator.Persistent);
            for (var y = 0; y < heightmapSize; y++)
            {
                for (var x = 0; x < heightmapSize; x++)
                {
                    var yStart = y * downSampleCount;
                    var xStart = x * downSampleCount;
                    var minHeight = heights[yStart, xStart];
                    for (var i = 0; i < downSampleCount; i++)
                    {
                        for (var j = 0; j < downSampleCount; j++)
                        {
                            minHeight = math.min(minHeight, heights[yStart + i, xStart + j]);
                        }
                    }
                    _heightmap[y * heightmapSize + x] = minHeight * terrainSize.y;
                }
            }
        }

        private float SampleHeight0(in float3 point)
        {
            return terrain.SampleHeight(point);
        }
        
        private float SampleHeight1(in float3 point) // slower than SampleHeight0
        {
            var uv = new int2(math.clamp(point.xz / terrainSize.xz, 0f, 0.99999f) * heightmapSize);
            var height = _heightmap[uv.y * heightmapSize + uv.x];
            return height;
        }

        private void OnDestroy()
        {
            _depthBuffer.Dispose();
            _heightmap.Dispose();
        }
        
        private void SaveTextureAsPNG(string path)
        {
            var pngBytes = depthTexture.EncodeToPNG();
            File.WriteAllBytes(path, pngBytes);
            Debug.Log("Texture saved as PNG to: " + path);
        }
    }
}