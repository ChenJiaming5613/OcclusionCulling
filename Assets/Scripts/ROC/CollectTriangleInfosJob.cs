using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace ROC
{
    public struct CollectTriangleInfosJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> ScreenSpaceVertices;

        [ReadOnly] public NativeArray<int> Indices;

        [WriteOnly] public NativeArray<int4> Bounds;

        [WriteOnly] public NativeArray<int3x3> EdgeParams;

        [WriteOnly] public NativeArray<float3> DepthParams;
        
        public void Execute(int triIdx)
        {
            var idx = triIdx * 3;
            var v0 = ScreenSpaceVertices[Indices[idx]];
            var v1 = ScreenSpaceVertices[Indices[idx + 1]];
            var v2 = ScreenSpaceVertices[Indices[idx + 2]];
            
            var iv0 = new int2(v0.xy);
            var iv1 = new int2(v1.xy);
            var iv2 = new int2(v2.xy);
            
            // backface culling
            var area = (iv1.x - iv0.x) * (iv2.y - iv0.y) - (iv2.x - iv0.x) * (iv1.y - iv0.y);
            if (area > 0)
            {
                Bounds[triIdx] = new int4(-1);
                return;
            }
            
            var vMinX = clamp((int)floor(min(min(iv0.x, iv1.x), iv2.x)), 0, Constants.ScreenWidth - 1);
            var vMaxX = clamp((int)ceil(max(max(iv0.x, iv1.x), iv2.x)), 0, Constants.ScreenWidth - 1);
            var vMinY = clamp((int)floor(min(min(iv0.y, iv1.y), iv2.y)), 0, Constants.ScreenHeight - 1);
            var vMaxY = clamp((int)ceil(max(max(iv0.y, iv1.y), iv2.y)), 0, Constants.ScreenHeight - 1);
            
            int a01 = iv1.y - iv0.y, b01 = iv0.x - iv1.x;
            int a12 = iv2.y - iv1.y, b12 = iv1.x - iv2.x;
            int a20 = iv0.y - iv2.y, b20 = iv2.x - iv0.x;
            
            var vMin = new int2(vMinX, vMinY);
            var w0Row = EdgeFunction(iv1, iv2, vMin);
            var w1Row = EdgeFunction(iv2, iv0, vMin);
            var w2Row = EdgeFunction(iv0, iv1, vMin);
            ComputeDepthPlane(v0, v1, v2, out var zPixelDx, out var zPixelDy);
            var zBase = v0.z + (vMin.x - v0.x) * zPixelDx + (vMin.y - v0.y) * zPixelDy;
            
            Bounds[triIdx] = new int4(vMinX, vMinY, vMaxX, vMaxY);
            EdgeParams[triIdx] = new int3x3(
                new int3(w0Row, w1Row, w2Row),
                new int3(a12, a20, a01),
                new int3(b12, b20, b01)
            );
            DepthParams[triIdx] = new float3(zBase, zPixelDx, zPixelDy);
        }
        
        [BurstCompile]
        private static int EdgeFunction(in int2 v0, in int2 v1, in int2 p)
        {
            return (v1.y - v0.y) * (p.x - v0.x) - (v1.x - v0.x) * (p.y - v0.y);
        }
        
        [BurstCompile]
        private static void ComputeDepthPlane(
            in float3 v0, in float3 v1, in float3 v2,
            out float zPixelDx, out float zPixelDy
        )
        {
            var x1 = v1.x - v0.x;
            var x2 = v2.x - v0.x;
            var y1 = v1.y - v0.y;
            var y2 = v2.y - v0.y;
            var z1 = v1.z - v0.z;
            var z2 = v2.z - v0.z;

            // 计算分母 d = 1.0f / (x1*y2 - y1*x2)
            var denominator = (x1 * y2) - (y1 * x2);
            var d = select(rcp(denominator), 0.0f, denominator == 0.0f); // 安全除法，避免除零

            zPixelDx = (z1 * y2 - y1 * z2) * d;
            zPixelDy = (x1 * z2 - z1 * x2) * d;
        }
    }
}