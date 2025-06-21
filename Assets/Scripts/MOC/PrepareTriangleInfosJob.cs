﻿using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MOC
{
    [BurstCompile]
    public struct PrepareTriangleInfosJob : IJobParallelFor
    {
        public int NumRowsTile;
        public int NumColsTile;
        
        [ReadOnly] public NativeArray<float3> ScreenSpaceVertices;
        
        [WriteOnly] public NativeArray<int4> TileRanges;
        // [WriteOnly] public NativeArray<int3x3> EdgeParams;
        [WriteOnly] public NativeArray<float4> DepthParams;
        [WriteOnly] public NativeArray<float3> InvSlope;
        [WriteOnly] public NativeArray<int3> Vx;
        [WriteOnly] public NativeArray<int3> Vy;
        [WriteOnly] public NativeArray<bool3> IsRightEdge;
        [WriteOnly] public NativeArray<int3> FlatInfo;
        
        public void Execute(int triIdx)
        {
            var idx = triIdx * 3;
            var v0 = ScreenSpaceVertices[idx];
            var v1 = ScreenSpaceVertices[idx + 1];
            var v2 = ScreenSpaceVertices[idx + 2];
            
            var iv0 = new int2(v0.xy + 0.5f); // TODO: Constants.SubPixelPrecision
            var iv1 = new int2(v1.xy + 0.5f);
            var iv2 = new int2(v2.xy + 0.5f);
            var area = (iv1.x - iv0.x) * (iv2.y - iv0.y) - (iv2.x - iv0.x) * (iv1.y - iv0.y);
            var z = new float3(v0.z, v1.z, v2.z);
            // if (area > 0)
            if (area > 0 || math.any(z < 0.0f) || math.any(z > 1.0f)) // discard instead clipping
            {
                TileRanges[triIdx] = new int4(-1);
                return;
            }
            
            var pixelMinX = math.min(math.min(v0.x, v1.x), v2.x);
            var pixelMaxX = math.max(math.max(v0.x, v1.x), v2.x);
            var pixelMinY = math.min(math.min(v0.y, v1.y), v2.y);
            var pixelMaxY = math.max(math.max(v0.y, v1.y), v2.y);

            var tileMinX = math.clamp((int)math.floor(pixelMinX / MocConfig.TileWidth), 0, NumColsTile - 1);
            var tileMinY = math.clamp((int)math.floor(pixelMinY / MocConfig.TileHeight), 0, NumRowsTile - 1);
            var tileMaxX = math.clamp((int)math.ceil(pixelMaxX / MocConfig.TileWidth), 0, NumColsTile - 1);
            var tileMaxY = math.clamp((int)math.ceil(pixelMaxY / MocConfig.TileHeight), 0, NumRowsTile - 1);
            var tileRange = new int4(tileMinX, tileMinY, tileMaxX, tileMaxY);
            TileRanges[triIdx] = tileRange;
            
            // var ivMin = new int2(tileMinX * Constants.TileWidth, tileMinY * Constants.TileHeight);
            // int a01 = iv1.y - iv0.y, b01 = iv0.x - iv1.x;
            // int a12 = iv2.y - iv1.y, b12 = iv1.x - iv2.x;
            // int a20 = iv0.y - iv2.y, b20 = iv2.x - iv0.x;
            // var w0Row = EdgeFunction(iv1, iv2, ivMin);
            // var w1Row = EdgeFunction(iv2, iv0, ivMin);
            // var w2Row = EdgeFunction(iv0, iv1, ivMin);
            // EdgeParams[triIdx] = new int3x3(
            //     new int3(w0Row, w1Row, w2Row),
            //     new int3(a12, a20, a01),
            //     new int3(b12, b20, b01)
            // );

            var dy = new int3(iv1.y - iv0.y, iv2.y - iv1.y, iv0.y - iv2.y);
            
            var zeroMask = math.select(0u, 1u, dy == 0);
            if (math.csum(zeroMask) >= 2)
            {
                TileRanges[triIdx] = new int4(-1);
                return;
            }
            
            var vy = new int3(iv0.y, iv1.y, iv2.y);
            var vx = new int3(iv0.x, iv1.x, iv2.x);
            var flatInfo = new int3(-1, 0, 0);
            var horizonEdgeIdx = dy[0] == 0 ? 0 : dy[1] == 0 ? 1 : dy[2] == 0 ? 2 : -1;
            if (horizonEdgeIdx != -1)
            {
                flatInfo.x = horizonEdgeIdx;
                flatInfo.y = vy[horizonEdgeIdx];
                flatInfo.z = dy[(horizonEdgeIdx + 1) % 3] > 0 ? -1 : 1;
            }
            var isRightEdge = dy < 0; // TODO: 三角形顺时针旋转？
            var dx = new int3(iv1.x - iv0.x, iv2.x - iv1.x, iv0.x - iv2.x);
            var invSlope = new float3(
                dy[0] == 0 ? 0f : dx[0] * 1.0f / dy[0],
                dy[1] == 0 ? 0f : dx[1] * 1.0f / dy[1],
                dy[2] == 0 ? 0f : dx[2] * 1.0f / dy[2]
            );
            InvSlope[triIdx] = invSlope;
            Vx[triIdx] = vx;
            Vy[triIdx] = vy;
            IsRightEdge[triIdx] = isRightEdge;
            FlatInfo[triIdx] = flatInfo;
            
            ComputeDepthPlane(v0, v1, v2, out var zPixelDx, out var zPixelDy);
            #if MOC_REVERSED_Z
            var zTriMax = math.min(math.min(v0.z, v1.z), v2.z);
            #else
            var zTriMax = math.max(math.max(v0.z, v1.z), v2.z);
            #endif
            var zSubTileDx = zPixelDx * MocConfig.SubTileWidth;
            var zSubTileDy = zPixelDy * MocConfig.SubTileHeight;
            var zTileMinBase = v0.z
                  + zPixelDx * (tileMinX * MocConfig.TileWidth - v0.x)
                  + zPixelDy * (tileMinY * MocConfig.TileHeight - v0.y);
            #if MOC_REVERSED_Z
            var zSubTileMax = zTileMinBase + (zSubTileDx < 0 ? zSubTileDx : 0) + (zSubTileDy < 0 ? zSubTileDy : 0);
            #else
            var zSubTileMax = zTileMinBase + (zSubTileDx > 0 ? zSubTileDx : 0) + (zSubTileDy > 0 ? zSubTileDy : 0);
            #endif
            DepthParams[triIdx] = new float4(zSubTileDx, zSubTileDy, zTriMax, zSubTileMax);
        }
        
        // [BurstCompile]
        // private static int EdgeFunction(in int2 v0, in int2 v1, in int2 p)
        // {
        //     return (v1.y - v0.y) * (p.x - v0.x) - (v1.x - v0.x) * (p.y - v0.y);
        // }
        
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