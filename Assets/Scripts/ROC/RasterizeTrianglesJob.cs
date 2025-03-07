using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ROC
{
    [BurstCompile]
    public struct RasterizeTrianglesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int4> Bounds;

        [ReadOnly] public NativeArray<int3x3> EdgeParams;

        [ReadOnly] public NativeArray<float3> DepthParams;

        [NativeDisableParallelForRestriction] public NativeArray<float> DepthBuffer;
        
        public void Execute(int triIdx)
        {
            RasterizeTriangle(triIdx);
        }
        
        private void RasterizeTriangle(int triIdx)
        {
            var bounds = Bounds[triIdx];
            if (bounds.x < 0) return; // culling
            
            var edgeW = EdgeParams[triIdx].c0;
            var edgeA = EdgeParams[triIdx].c1;
            var edgeB = EdgeParams[triIdx].c2;
            var depthParam = DepthParams[triIdx];
            var zRow = depthParam.x;
            var zPixelDx = depthParam.y;
            var zPixelDy = depthParam.z;
            for (var y = bounds.y; y <= bounds.w; y++)
            {
                var w = edgeW;
                var currZ = zRow;
                
                for (var x = bounds.x; x <= bounds.z; x++)
                {
                    // if (w.x >= 0 && w.y >= 0 && w.z >= 0)
                    if ((w.x | w.y | w.z) >= 0)
                    {
                        var index = y * Constants.ScreenWidth + x;
                        var depth = DepthBuffer[index];
                        if (currZ < depth)
                        {
                            DepthBuffer[index] = currZ; // TODO: atomic
                        }
                    }
                    w += edgeA;
                    currZ += zPixelDx;
                }

                edgeW += edgeB;
                zRow += zPixelDy;
            }
        }
    }
}