using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TerrainRayMarching
{
    [BurstCompile]
    public struct InterpolateDepthBufferJob : IJobParallelFor
    {
        public int2 DepthBufferSize;
        [NativeDisableParallelForRestriction] public NativeArray<float> DepthBuffer;
        
        public void Execute(int pixelIdx)
        {
            var y = pixelIdx / DepthBufferSize.x;
            var x = pixelIdx % DepthBufferSize.x;
            if ((x + y) % 2 == 0) return;

            var left = new int2(math.max(x - 1, 0), y);
            var right = new int2(math.min(x + 1, DepthBufferSize.x - 1), y);
            var bottom = new int2(x, math.max(y - 1, 0));
            var top = new int2(x, math.min(y + 1, DepthBufferSize.y - 1));
            var depth = DepthBuffer[left.y * DepthBufferSize.x + left.x];
            depth = math.max(depth, DepthBuffer[right.y * DepthBufferSize.x + right.x]);
            depth = math.max(depth, DepthBuffer[bottom.y * DepthBufferSize.x + bottom.x]);
            depth = math.max(depth, DepthBuffer[top.y * DepthBufferSize.x + top.x]);
            
            DepthBuffer[pixelIdx] = depth;
        }
    }
}