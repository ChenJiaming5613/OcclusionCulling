using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TerrainRayMarching
{
    [BurstCompile]
    public struct RayMarchingJob : IJobParallelFor
    {
        public float3 CamPos;
        public float3 BottomLeftCorner;
        public float3 RightStep;
        public float3 UpStep;
        public int2 DepthBufferSize;
        public int2 BinSize;
        public int2 NumBinXY; // DepthBufferSize / BinSize
        public int HeightmapSize;
        public float3 TerrainSize;
        public float Near;
        public float Far;
        public float RayMarchingStep;

        [ReadOnly] public NativeArray<float> Heightmap;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float> DepthBuffer;
        
        public void Execute(int binIdx)
        {
            var binXY = new int2(binIdx % NumBinXY.x, binIdx / NumBinXY.x);
            var pixelStart = binXY * BinSize;
            var pixelEnd = new int2(
                binXY.x == NumBinXY.x - 1 ? DepthBufferSize.x : (binXY.x + 1) * BinSize.x,
                binXY.y == NumBinXY.y - 1 ? DepthBufferSize.y : (binXY.y + 1) * BinSize.y
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
                    // var y = pixelIdx / DepthBufferSize.x;
                    // var x = pixelIdx % DepthBufferSize.x;
                    if ((x + y) % 2 == 1)
                    {
                        pixelIdx++;
                        continue;
                    }
                    var point = BottomLeftCorner + x * RightStep + y * UpStep;
                    var dir = math.normalize(point - CamPos);
                    var depth = Near;
                    while (IsAboveTerrain(point, ref depth))
                    {
                        // TODO: 提前退出：当point已经高于terrain最大高度并且是向上移动
                        point += dir * RayMarchingStep;
                        depth += RayMarchingStep;
                    }
                    var depth01 = (depth - Near) / (Far - Near);
                    if (depth01 < 1.0f) missAll = false;
                    DepthBuffer[pixelIdx++] = depth01;
                }
                if (missAll) blank = true;
                pixelIdxRow += DepthBufferSize.x;
            }
        }
        
        private bool IsAboveTerrain(in float3 point, ref float depth)
        {
            if (!(point.x >= 0f && point.x < TerrainSize.x && point.z >= 0f && point.z < TerrainSize.z))
            {
                depth = Far;
                return false;
            }
            var uv = point.xz / TerrainSize.xz;
            var coord = new int2(uv * HeightmapSize);
            var height = Heightmap[coord.y * HeightmapSize + coord.x];
            return point.y > height;
        }
    }
}