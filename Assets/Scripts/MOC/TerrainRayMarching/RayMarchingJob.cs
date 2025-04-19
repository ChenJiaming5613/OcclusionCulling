using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MOC.TerrainRayMarching
{
    [BurstCompile]
    public struct RayMarchingJob : IJobParallelFor
    {
        public float3 CamPos;
        public float3 BottomLeftCorner;
        public float3 RightStep;
        public float3 UpStep;
        public float3 RayMarchingStep;
        public float4x4 VpMatrix;

        public int2 DepthBufferSize;
        public int2 BinSize;
        public int2 BinGridSize; // DepthBufferSize / BinSize
        public int HeightmapSize;
        public int HolesMapSize;
        public float3 TerrainSize;

        [ReadOnly] public NativeArray<float> Heightmap;
        [ReadOnly] public NativeArray<bool> HolesMap;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float> DepthBuffer;
        
        public void Execute(int binIdx)
        {
            var binXY = new int2(binIdx % BinGridSize.x, binIdx / BinGridSize.x);
            var pixelStart = binXY * BinSize;
            var pixelEnd = new int2(
                binXY.x == BinGridSize.x - 1 ? DepthBufferSize.x : (binXY.x + 1) * BinSize.x,
                binXY.y == BinGridSize.y - 1 ? DepthBufferSize.y : (binXY.y + 1) * BinSize.y
            );
            var pixelIdxRow = pixelStart.y * DepthBufferSize.x + pixelStart.x;
            var blank = false;
            for (var y = pixelStart.y; y < pixelEnd.y; y++)
            {
                var pixelIdx = pixelIdxRow;
                if (blank)
                {
                    for (var x = pixelStart.x; x < pixelEnd.x; x++)
                    {
                        DepthBuffer[pixelIdx++] = 1.0f;
                    }
                    pixelIdxRow += DepthBufferSize.x;
                    continue;
                }
                var missAll = true;
                for (var x = pixelStart.x; x < pixelEnd.x; x++)
                {
                    if ((x + y) % 2 == 1)
                    {
                        pixelIdx++;
                        continue;
                    }
                    var point = BottomLeftCorner + x * RightStep + y * UpStep;
                    var dir = math.normalize(point - CamPos);
                    // var depth = Near;
                    var step = RayMarchingStep.x;
                    bool isOutside;
                    var existHole = false;
                    while (IsAboveTerrain(point, out isOutside, ref existHole))
                    {
                        // TODO: 提前退出：当point已经高于terrain最大高度并且是向上移动
                        point += dir * step;
                        if (step < RayMarchingStep.y) step += RayMarchingStep.z;
                    }

                    if (isOutside)
                    {
                        DepthBuffer[pixelIdx++] = 1.0f;
                        if (existHole) missAll = false;
                        continue;
                    }

                    var depth = CalcNdcDepth(point);
                    if (depth < 1.0f) missAll = false;
                    DepthBuffer[pixelIdx++] = depth;
                }
                if (missAll) blank = true;
                pixelIdxRow += DepthBufferSize.x;
            }
        }
        
        private bool IsAboveTerrain(in float3 point, out bool isOutside, ref bool existHole)
        {
            if (!(point.x >= 0f && point.x < TerrainSize.x && point.z >= 0f && point.z < TerrainSize.z))
            {
                isOutside = true;
                return false;
            }
            var uv = point.xz / TerrainSize.xz;
            var coord = new int2(uv * HeightmapSize);
            var height = Heightmap[coord.y * HeightmapSize + coord.x];
            isOutside = false;
            var isAbove = point.y > height;
            if (isAbove) return true;
            var coordHole = new int2(uv * HolesMapSize);
            var isHole = HolesMap[coordHole.y * HolesMapSize + coordHole.x];
            if (!isHole) return false;
            isOutside = true;
            existHole = true;
            return false;
        }

        private float CalcNdcDepth(in float3 point)
        {
            var clipSpacePoint = math.mul(VpMatrix, new float4(point, 1.0f));
            return (clipSpacePoint.z / clipSpacePoint.w) * 0.5f + 0.5f;
        }
    }
}