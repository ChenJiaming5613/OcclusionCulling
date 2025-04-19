using System.Diagnostics;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace MOC.TerrainRayMarching
{
    public class TerrainRayMarching
    {
        private readonly Terrain _terrain;
        private readonly Camera _camera;
        private readonly MocConfig _config;
        private readonly NativeArray<Tile> _tiles;
        
        private readonly float3 _terrainSize;
        private NativeArray<float> _heightmap;
        private NativeArray<bool> _holesMap;
        private int _originalHeightmapSize;
        private int _heightmapSize;
        private int _holesMapSize;

        private NativeArray<float> _depthBuffer;
        private readonly int2 _depthBufferSize;
        private readonly int2 _binGridSize;
        private readonly int2 _binSize;
        private readonly int _numBins;
        private readonly int2 _tileGridSize;
        private readonly int _numTiles;
        
        private CamParams _camParams;
        private float3 _camPos;
        private float3 _bottomLeftCorner;
        private float3 _rightStep;
        private float3 _upStep;
        private Matrix4x4 _vpMatrix;

        public TerrainRayMarching(Terrain terrain, Camera camera, MocConfig config, NativeArray<Tile> tiles)
        {
            _terrain = terrain;
            _camera = camera;
            _config = config;
            _tiles = tiles;

            _terrainSize = terrain.terrainData.size;
            _depthBufferSize = new int2(
                _config.NumColsTile * MocConfig.NumColsSubTile * (MocConfig.SubTileWidth / MocConfig.SubTileHeight),
                _config.NumRowsTile * MocConfig.NumRowsSubTile); // 每4x4个像素为一个ray marching像素
            _binSize = new int2(2, _depthBufferSize.y);
            _binGridSize = _depthBufferSize / _binSize;
            Debug.Log($"Bin Grid Size: {_binGridSize}");
            _numBins = _binGridSize.x * _binGridSize.y;
            _tileGridSize = new int2(_config.NumColsTile, _config.NumRowsTile);
            _numTiles = _config.NumColsTile * _config.NumRowsTile;

            UpdateCamParams();
            _depthBuffer = new NativeArray<float>(_depthBufferSize.x * _depthBufferSize.y, Allocator.Persistent);
            
            AllocHeightMap();
            AllocHolesMap();
        }

        public void UpdateVpMatrix(in Matrix4x4 vpMatrix)
        {
            _vpMatrix = vpMatrix;
        }
        
        public JobHandle RayMarching()
        {
            Profiler.BeginSample("Update Frustum Steps");
            UpdateFrustumSteps();
            Profiler.EndSample();
            var rayMarchingJob = new RayMarchingJob
            {
                CamPos = _camPos,
                BottomLeftCorner = _bottomLeftCorner,
                RightStep = _rightStep,
                UpStep = _upStep,
                VpMatrix = _vpMatrix,
                RayMarchingStep = _config.RayMarchingStep,
                DepthBufferSize = _depthBufferSize,
                BinSize = _binSize,
                BinGridSize = _binGridSize,
                HeightmapSize = _heightmapSize,
                HolesMapSize = _holesMapSize,
                TerrainSize = _terrainSize,
                Heightmap = _heightmap,
                HolesMap = _holesMap,
                DepthBuffer = _depthBuffer
            };
            var rayMarchingJobHandle = rayMarchingJob.Schedule(_numBins, 1);

            var interpolateDepthBufferJob = new InterpolateDepthBufferJob
            {
                TileGridSize = _tileGridSize,
                DepthBufferSize = _depthBufferSize,
                Tiles = _tiles,
                DepthBuffer = _depthBuffer
            };

            return interpolateDepthBufferJob.Schedule(_numTiles, 64, rayMarchingJobHandle);
        }
        
        public void Dispose()
        {
            _depthBuffer.Dispose();
            _heightmap.Dispose();
            _holesMap.Dispose();
        }

        private void UpdateCamParams()
        {
            var fov = _camera.fieldOfView;
            var aspect = _camera.aspect;
            _camParams.Near = _camera.nearClipPlane;
            _camParams.Far = _camera.farClipPlane;
            _camParams.HalfHeight = _camParams.Near * math.tan(math.radians(fov) * 0.5f);
            _camParams.HalfWidth = _camParams.HalfHeight * aspect;
            _camParams.RightStepPerPixel = _camParams.HalfWidth * 2 / _depthBufferSize.x;
            _camParams.UpStepPerPixel = _camParams.HalfHeight * 2 / _depthBufferSize.y;
        }
        
        private void UpdateFrustumSteps()
        {
            float3 forward = _camera.transform.forward;
            float3 right = _camera.transform.right;
            float3 up = _camera.transform.up;

            _camPos = _camera.transform.position;
            var nearCenter = _camPos + forward * _camParams.Near;
            _bottomLeftCorner = nearCenter - right * _camParams.HalfWidth - up * _camParams.HalfHeight;
            _rightStep = right * _camParams.RightStepPerPixel;
            _upStep = up * _camParams.UpStepPerPixel;
        }

        private void AllocHeightMap()
        {
            var downSampleCount = _config.RayMarchingDownSampleCount;
            var terrainData = _terrain.terrainData;
            _originalHeightmapSize = terrainData.heightmapResolution;
            _heightmapSize = _originalHeightmapSize / downSampleCount;
            var heights = terrainData.GetHeights(0, 0, _originalHeightmapSize, _originalHeightmapSize);
            _heightmap = new NativeArray<float>(_heightmapSize * _heightmapSize, Allocator.Persistent);
            for (var y = 0; y < _heightmapSize; y++)
            {
                for (var x = 0; x < _heightmapSize; x++)
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
                    _heightmap[y * _heightmapSize + x] = minHeight * _terrainSize.y;
                }
            }
        }

        private void AllocHolesMap()
        {
            var terrainData = _terrain.terrainData;
            _holesMapSize = terrainData.holesResolution;
            var holes = terrainData.GetHoles(0, 0, _holesMapSize, _holesMapSize);
            _holesMap = new NativeArray<bool>(_holesMapSize * _holesMapSize, Allocator.Persistent);
            var idx = 0;
            for (var y = 0; y < _holesMapSize; y++)
            {
                for (var x = 0; x < _holesMapSize; x++)
                {
                    _holesMap[idx++] = !holes[y, x];
                }
            }
        }

        public void VisualizeDepthTexture()
        {
            var depthTexture = new Texture2D(_depthBufferSize.x, _depthBufferSize.y, TextureFormat.RFloat, false);
            for (var y = 0; y < _depthBufferSize.y; y++)
            {
                for (var x = 0; x < _depthBufferSize.x; x++)
                {
                    var depth = _depthBuffer[y * _depthBufferSize.x + x];
                    depthTexture.SetPixel(x, y, new Color(depth, 0, 0));
                }
            }
            
            var pngBytes = depthTexture.EncodeToPNG();
            const string path = "Assets/Resources/TerrainRayMarching.png";
            File.WriteAllBytes(path, pngBytes);
            Debug.Log("Texture saved as PNG to: " + path);
        }

        private struct CamParams // 当camera的fov,aspect,near改变时需要更新
        {
            public float Near;
            public float Far;
            public float HalfWidth; // 进平面半宽
            public float HalfHeight; // 进平面半高
            public float RightStepPerPixel;
            public float UpStepPerPixel;
        }
    }
}