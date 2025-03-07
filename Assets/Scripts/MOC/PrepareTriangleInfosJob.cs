using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MOC
{
    [BurstCompile]
    public struct PrepareTriangleInfosJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> ScreenSpaceVertices;
        [ReadOnly] public NativeArray<int> Indices;
        
        [WriteOnly] public NativeArray<int4> TileRanges;
        [WriteOnly] public NativeArray<int3x3> EdgeParams;
        [WriteOnly] public NativeArray<float4> DepthParams;
        
        public void Execute(int triIdx)
        {
            var idx = triIdx * 3;
            var v0 = ScreenSpaceVertices[Indices[idx]];
            var v1 = ScreenSpaceVertices[Indices[idx + 1]];
            var v2 = ScreenSpaceVertices[Indices[idx + 2]];
            
            var iv0 = new int2(v0.xy + 0.5f); // TODO: Constants.SubPixelPrecision
            var iv1 = new int2(v1.xy + 0.5f);
            var iv2 = new int2(v2.xy + 0.5f);
            var area = (iv1.x - iv0.x) * (iv2.y - iv0.y) - (iv2.x - iv0.x) * (iv1.y - iv0.y);
            if (area > 0)
            {
                TileRanges[triIdx] = new int4(-1);
                return;
            }
            
            var pixelMinX = math.min(math.min(v0.x, v1.x), v2.x);
            var pixelMaxX = math.max(math.max(v0.x, v1.x), v2.x);
            var pixelMinY = math.min(math.min(v0.y, v1.y), v2.y);
            var pixelMaxY = math.max(math.max(v0.y, v1.y), v2.y);

            var tileMinX = math.clamp((int)math.floor(pixelMinX / Constants.TileWidth), 0, Constants.NumColsTile - 1);
            var tileMinY = math.clamp((int)math.floor(pixelMinY / Constants.TileHeight), 0, Constants.NumRowsTile - 1);
            var tileMaxX = math.clamp((int)math.ceil(pixelMaxX / Constants.TileWidth), 0, Constants.NumColsTile - 1);
            var tileMaxY = math.clamp((int)math.ceil(pixelMaxY / Constants.TileHeight), 0, Constants.NumRowsTile - 1);
            var tileRange = new int4(tileMinX, tileMinY, tileMaxX, tileMaxY);
            TileRanges[triIdx] = tileRange;
            
            var ivMin = new int2(tileMinX * Constants.TileWidth, tileMinY * Constants.TileHeight);
            int a01 = iv1.y - iv0.y, b01 = iv0.x - iv1.x;
            int a12 = iv2.y - iv1.y, b12 = iv1.x - iv2.x;
            int a20 = iv0.y - iv2.y, b20 = iv2.x - iv0.x;
            var w0Row = EdgeFunction(iv1, iv2, ivMin);
            var w1Row = EdgeFunction(iv2, iv0, ivMin);
            var w2Row = EdgeFunction(iv0, iv1, ivMin);
            EdgeParams[triIdx] = new int3x3(
                new int3(w0Row, w1Row, w2Row),
                new int3(a12, a20, a01),
                new int3(b12, b20, b01)
            );
            
            ComputeDepthPlane(v0, v1, v2, out var zPixelDx, out var zPixelDy);
            var zTriMax = math.max(math.max(v0.z, v1.z), v2.z);
            var zSubTileDx = zPixelDx * Constants.SubTileWidth;
            var zSubTileDy = zPixelDy * Constants.SubTileHeight;
            var zTileMinBase = v0.z
                  + zPixelDx * (tileMinX * Constants.TileWidth - v0.x)
                  + zPixelDy * (tileMinY * Constants.TileHeight - v0.y);
            var zSubTileMax = zTileMinBase + (zSubTileDx > 0 ? zSubTileDx : 0) + (zSubTileDy > 0 ? zSubTileDy : 0);
            DepthParams[triIdx] = new float4(zSubTileDx, zSubTileDy, zTriMax, zSubTileMax);
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
            var x2 = v2.x - v0.x;
            var x1 = v1.x - v0.x;
            var y1 = v1.y - v0.y;
            var y2 = v2.y - v0.y;
            var z1 = v1.z - v0.z;
            var z2 = v2.z - v0.z;

            var denominator = x1 * y2 - y1 * x2;
            var d = math.select(math.rcp(denominator), 0.0f, denominator == 0.0f); // avoid divide 0

            zPixelDx = (z1 * y2 - y1 * z2) * d;
            zPixelDy = (x1 * z2 - z1 * x2) * d;
        }
    }
}