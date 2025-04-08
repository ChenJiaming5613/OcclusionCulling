using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MOC
{
    [BurstCompile]
    public struct TestOccludeesJob : IJobParallelFor
    {
        public int NumOccluders;
        public int DepthBufferWidth;
        public int DepthBufferHeight;
        public int NumColsTile;
        public float4x4 VpMatrix;
        [ReadOnly] public NativeSlice<Bounds> Bounds;
        [ReadOnly] public NativeArray<Tile> Tiles;
        [ReadOnly] public NativeArray<bool> OccluderFlags;
        // [NativeDisableParallelForRestriction] public NativeArray<Tile> Tiles;
        public NativeSlice<bool> CullingResults;
        
        public void Execute(int objIdx) // TODO: 没有执行？！！
        {
            if (CullingResults[objIdx]) return;
            if (objIdx < NumOccluders && OccluderFlags[objIdx]) return;
            var depthBufferSize = new int2(DepthBufferWidth, DepthBufferHeight);
            ComputeRectAndClosestDepth(depthBufferSize, Bounds[objIdx], VpMatrix, out var rect, out var closestDepth);
            var tileSize = new int2(MocConfig.TileWidth, MocConfig.TileHeight);
            var tileMin = rect.xz / tileSize;
            var tileMax = rect.yw / tileSize;
            for (var tileY = tileMin.y; tileY <= tileMax.y; tileY++)
            {
                for (var tileX = tileMin.x; tileX <= tileMax.x; tileX++)
                {
                    var tileRect = new int4(
                        tileX * MocConfig.TileWidth,
                        (tileX + 1) * MocConfig.TileWidth - 1,
                        tileY * MocConfig.TileHeight,
                        (tileY + 1) * MocConfig.TileHeight - 1
                    );
                    var intersectRect = ComputeRectIntersection(tileRect, rect);

                    intersectRect.xy -= tileRect.x;
                    intersectRect.zw -= tileRect.z;
                    var coverage = ComputeRectCoverage(intersectRect);
                    // var tmp = ToBinaryString(coverage);
                    // Debug.Log(tmp);
                    var bitmask = ShuffleBitmask(coverage);
                    var tileIdx = tileY * NumColsTile + tileX;
                    var tile = Tiles[tileIdx];
                    var isOccluded = true;
                    for (var i = 0; i < 4; i++)
                    {
                        // tile.bitmask[i] |= bitmask[i];
                        // tile.z[i] = closestDepth;
                        // continue;
                        // if (closestDepth > tile.z0[i]) continue;
                        if (closestDepth > tile.z[i] && tile.bitmask[i] == (bitmask[i] | tile.bitmask[i])) continue;
                        
                        isOccluded = false;
                        break;
                    }
                    // Tiles[tileIdx] = tile;
                    if (isOccluded) continue;
                    CullingResults[objIdx] = false;
                    return;
                }
            }
            CullingResults[objIdx] = true;
        }

        private static void ComputeRectAndClosestDepth(
            in int2 depthBufferSize, in Bounds bounds, in float4x4 mvpMatrix,
            out int4 rect, out float closestDepth
        )
        {
            var corners = new NativeArray<float4>(8, Allocator.Temp);
            var boundsMin = new float3(bounds.min);
            var boundsMax = new float3(bounds.max);
            corners[0] = new float4(boundsMin.x, boundsMin.y, boundsMin.z, 1.0f);
            corners[1] = new float4(boundsMax.x, boundsMin.y, boundsMin.z, 1.0f);
            corners[2] = new float4(boundsMin.x, boundsMax.y, boundsMin.z, 1.0f);
            corners[3] = new float4(boundsMax.x, boundsMax.y, boundsMin.z, 1.0f);
            corners[4] = new float4(boundsMin.x, boundsMin.y, boundsMax.z, 1.0f);
            corners[5] = new float4(boundsMax.x, boundsMin.y, boundsMax.z, 1.0f);
            corners[6] = new float4(boundsMin.x, boundsMax.y, boundsMax.z, 1.0f);
            corners[7] = new float4(boundsMax.x, boundsMax.y, boundsMax.z, 1.0f);
            var screenMin = new int2(int.MaxValue, int.MaxValue);
            var screenMax = new int2(int.MinValue, int.MinValue);

            var screenSize = new int3(depthBufferSize, 1);
            closestDepth = float.MaxValue;
            for (var i = 0; i < 8; i++)
            {
                var clipSpacePoint = math.mul(mvpMatrix, corners[i]);
                var screenSpacePoint = (clipSpacePoint.xyz / clipSpacePoint.w * 0.5f + 0.5f) * screenSize;
                var screenXY = math.clamp(new int2(screenSpacePoint.xy), 0, screenSize.xy - 1);
                screenMin = math.min(screenMin, screenXY);
                screenMax = math.max(screenMax, screenXY);
                closestDepth = math.min(closestDepth, screenSpacePoint.z);
            }

            rect = new int4(screenMin.x, screenMax.x, screenMin.y, screenMax.y);
            corners.Dispose();
        }

        private static int4 ComputeRectIntersection(int4 rect1, int4 rect2)
        {
            var rectMin = math.max(rect1.xz, rect2.xz);
            var rectMax = math.min(rect1.yw, rect2.yw);
            return new int4(rectMin.x, rectMax.x, rectMin.y, rectMax.y);
        }
        
        /// <summary>
        /// ensure endCol > startCol
        /// </summary>
        /// <param name="rect"> xxyy: startCol, endCol, startRow, endRow</param>
        /// <returns></returns>
        private static uint4 ComputeRectCoverage(int4 rect)
        {
            // compute col mask
            var bitCount = rect.y - rect.x + 1;
            var bits = (1UL << bitCount) - 1;
            var colMask = (uint)((bits << rect.x) & 0xFFFFFFFFU);
            // broadcast to all cols
            var colMasks = new uint4(colMask);

            var rows = new int4(0, 1, 2, 3);
            // compute row mask
            var rowMaskFlags = (rows >= rect.z) & (rows <= rect.w);
            var rowMasks = math.select(0u, 0xFFFFFFFFu, rowMaskFlags);
            
            // combine mask
            var result = colMasks & rowMasks;
            return result;
        }

        private static uint4 ShuffleBitmask(uint4 bitmask)
        {
            // 提取每个uint32的四个字节（小端序：第一个字节是最低位）
            var a = bitmask & 0x000000FF;        // 第0个字节（a, e, i, m）
            var b = (bitmask >> 8) & 0x000000FF; // 第1个字节（b, f, j, n）
            var c = (bitmask >> 16) & 0x000000FF;// 第2个字节（c, g, k, o）
            var d = (bitmask >> 24) & 0x000000FF;// 第3个字节（d, h, l, p）

            // 将四个原始行的对应字节组合成新的uint32
            var newX = a.x | (a.y << 8) | (a.z << 16) | (a.w << 24); // a e i m
            var newY = b.x | (b.y << 8) | (b.z << 16) | (b.w << 24); // b f j n
            var newZ = c.x | (c.y << 8) | (c.z << 16) | (c.w << 24); // c g k o
            var newW = d.x | (d.y << 8) | (d.z << 16) | (d.w << 24); // d h l p

            // 返回转置后的uint4
            return new uint4(newX, newY, newZ, newW);
        }
        
        // private static string ToBinaryString(uint4 vector)
        // {
        //     return $"{Convert.ToString(vector.x, 2).PadLeft(32, '0')}" +
        //            $"\n{Convert.ToString(vector.y, 2).PadLeft(32, '0')}" +
        //            $"\n{Convert.ToString(vector.z, 2).PadLeft(32, '0')}" +
        //            $"\n{Convert.ToString(vector.w, 2).PadLeft(32, '0')}";
        // }
    }
}