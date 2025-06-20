using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MOC
{
    [BurstCompile]
    public struct BinRasterizerJob : IJobParallelFor
    {
        public int NumTris;
        public int NumRowsTile;
        public int NumColsTile;
        public int NumColsTileInBin;
        public int NumRowsTileInBin;
        public int NumBinCols;
        public int NumBinRows;
        [ReadOnly] public NativeArray<int4> TileRanges; // 三角形整个的range,需要clamp到bin中
        // [ReadOnly] public NativeArray<int3x3> EdgeParams;
        [ReadOnly] public NativeArray<float4> DepthParams;
        [ReadOnly] public NativeArray<float3> InvSlope;
        [ReadOnly] public NativeArray<int3> Vx;
        [ReadOnly] public NativeArray<int3> Vy;
        [ReadOnly] public NativeArray<bool3> IsRightEdge;
        [ReadOnly] public NativeArray<int3> FlatInfo;
        
        [NativeDisableParallelForRestriction] public NativeArray<Tile> Tiles;
        
        public void Execute(int binIdx)
        {
            var binRowIdx = binIdx / NumBinCols;
            var binColIdx = binIdx % NumBinCols;
            var binRange = new int4(
                binColIdx * NumColsTileInBin,
                binRowIdx * NumRowsTileInBin,
                binColIdx == NumBinCols - 1 ? NumColsTile - 1 : (binColIdx + 1) * NumColsTileInBin - 1, // 防止不能整除
                binRowIdx == NumBinRows - 1 ? NumRowsTile - 1 : (binRowIdx + 1) * NumRowsTileInBin - 1
            );
            for (var triIdx = 0; triIdx < NumTris; triIdx++)
            {
                var tileRange = TileRanges[triIdx];
                if (tileRange.x < 0) continue; // clipped
                var clampedTileRange = new int4(math.max(tileRange.xy, binRange.xy), math.min(tileRange.zw, binRange.zw));
                if (!math.all(clampedTileRange.xy <= clampedTileRange.zw)) continue;
                RasterizeTriangle(triIdx, clampedTileRange);
            }
        }

        private void RasterizeTriangle(int triIdx, in int4 clampedTileRange)
        {
            var tileRange = TileRanges[triIdx];
            var tileRangeOffset = clampedTileRange.xy - tileRange.xy;
            
            var depthParam = DepthParams[triIdx];
            var zTileDx = depthParam.x * MocConfig.NumColsSubTile;
            var zTileDy = depthParam.y * MocConfig.NumRowsSubTile;
            var zTileRow = depthParam.w + tileRangeOffset.x * zTileDx + tileRangeOffset.y * zTileDy;
            
            // var edgeParam = EdgeParams[triIdx];
            // var wTileDx = edgeParam.c1 * Constants.TileWidth;
            // var wTileDy = edgeParam.c2 * Constants.TileHeight;
            // var wTileRow = edgeParam.c0 + tileRangeOffset.x * wTileDx + tileRangeOffset.y * wTileDy;
            var invSlope = InvSlope[triIdx]; // 如果是flat三角形，则只有前两个斜率有效
            var vx = Vx[triIdx];
            var vy = Vy[triIdx];
            var isRightEdge = IsRightEdge[triIdx];
            var flatInfo = FlatInfo[triIdx]; // x: idx; y:flatY; z:flat类型 -1 bottom 0 not flat 1 top; 

            if (flatInfo.x == 0)
            {
                invSlope = new float3(invSlope[2], invSlope[1], invSlope[0]);
                vx = new int3(vx[2], vx[1], vx[0]);
                vy = new int3(vy[2], vy[1], vy[0]);
                isRightEdge = new bool3(isRightEdge[2], isRightEdge[1], isRightEdge[0]);
            }
            else if (flatInfo.x == 1)
            {
                invSlope = new float3(invSlope[0], invSlope[2], invSlope[1]);
                vx = new int3(vx[0], vx[2], vx[1]);
                vy = new int3(vy[0], vy[2], vy[1]);
                isRightEdge = new bool3(isRightEdge[0], isRightEdge[2], isRightEdge[1]);
            }
            
            var y = new int4(0, 1, 2, 3) + clampedTileRange.y * MocConfig.TileHeight;
            var x0 = new float4(y - vy[0]) * invSlope[0] + vx[0];
            var x1 = new float4(y - vy[1]) * invSlope[1] + vx[1];
            var x2 = flatInfo.x == -1 ? new float4(y - vy[2]) * invSlope[2] + vx[2] : new float4();
            var dx0 = MocConfig.TileHeight * invSlope[0];
            var dx1 = MocConfig.TileHeight * invSlope[1];
            var dx2 = MocConfig.TileHeight * invSlope[2];
            var rightMask = math.select(0u, ~0u, isRightEdge);
            var zSubTileDx = depthParam.x;
            var zTriMax = depthParam.z;
            var zOffsets = new float4(0, 1, 2, 3) * zSubTileDx;
            for (var tileY = clampedTileRange.y; tileY <= clampedTileRange.w; tileY++)
            {
                var z = zTileRow + zOffsets;
                var tileX = clampedTileRange.x;
                var tileIdx = tileY * NumColsTile + tileX;
                var xStart = tileX * MocConfig.TileWidth;
                for (; tileX <= clampedTileRange.z; tileX++)
                {
                    var ix0 = math.clamp(new int4(x0 - xStart), 0, 32);
                    var ix1 = math.clamp(new int4(x1 - xStart), 0, 32);
                    var ix2 = math.clamp(new int4(x2 - xStart), 0, 32);

                    RasterizeTile(
                        tileIdx, y, 
                        #if MOC_REVERSED_Z
                        math.max(z, zTriMax),
                        #else
                        math.min(z, zTriMax),
                        #endif
                        rightMask,
                        ix0, ix1, ix2,
                        flatInfo
                    );

                    z += zTileDx;
                    tileIdx++;
                    xStart += MocConfig.TileWidth;
                }
                zTileRow += zTileDy;
                y += MocConfig.TileHeight;
                x0 += dx0;
                x1 += dx1;
                x2 += dx2;
            }
        }

        private void RasterizeTile(
            int tileIdx, in int4 y,
            in float4 zMax,
            in uint3 rightMask,
            in int4 x0, in int4 x1, in int4 x2, // x0x1x2 in [0, 32]
            in int3 flatInfo
        )
        {
            var bitmask0 = new uint4(~0u >> x0.x, ~0u >> x0.y, ~0u >> x0.z, ~0u >> x0.w);
            bitmask0 = math.select(bitmask0, 0u, x0 == 32);
            bitmask0 ^= rightMask[0];
            if (math.all(bitmask0 == 0u)) return;
            
            var bitmask1 = new uint4(~0u >> x1.x, ~0u >> x1.y, ~0u >> x1.z, ~0u >> x1.w);
            bitmask1 = math.select(bitmask1, 0u, x1 == 32);
            bitmask1 ^= rightMask[1];
            if (math.all(bitmask1 == 0u)) return;

            var bitmask = bitmask0 & bitmask1;
            if (flatInfo.z == 0)
            {
                var bitmask2 = new uint4(~0u >> x2.x, ~0u >> x2.y, ~0u >> x2.z, ~0u >> x2.w);
                bitmask2 = math.select(bitmask2, 0u, x2 == 32);
                bitmask2 ^= rightMask[2];
                bitmask &= bitmask2;
            }
            else
            {
                bitmask &= math.select(0u, ~0u, (y - flatInfo.y >= 0) ^ (flatInfo.z > 0));
            }
            if (math.all(bitmask == 0u)) return;
            
            bitmask = ShuffleMask(bitmask);
            UpdateTile(tileIdx, bitmask, zMax);
        }

        private static uint4 ShuffleMask(in uint4 bitmask)
        {
            var bytes3 = bitmask & 0xFF;
            var bytes2 = (bitmask >> 8) & 0xFF;
            var bytes1 = (bitmask >> 16) & 0xFF;
            var bytes0 = (bitmask >> 24) & 0xFF;

            return new uint4(
                bytes0.x | (bytes0.y << 8) | (bytes0.z << 16) | (bytes0.w << 24),
                bytes1.x | (bytes1.y << 8) | (bytes1.z << 16) | (bytes1.w << 24),
                bytes2.x | (bytes2.y << 8) | (bytes2.z << 16) | (bytes2.w << 24),
                bytes3.x | (bytes3.y << 8) | (bytes3.z << 16) | (bytes3.w << 24)
            );
        }
        
        private void UpdateTile(int tileIdx, in uint4 bitmask, in float4 zMax)
        {
            var tile = Tiles[tileIdx];
            for (var i = 0; i < 4; i++)
            {
                #if MOC_REVERSED_Z
                if (tile.bitmask[i] == ~0u && zMax[i] <= tile.z[i]) continue;
                if (bitmask[i] == ~0u && zMax[i] > tile.z[i])
                #else
                if (tile.bitmask[i] == ~0u && zMax[i] >= tile.z[i]) continue;
                if (bitmask[i] == ~0u && zMax[i] < tile.z[i])
                #endif
                {
                    tile.z[i] = zMax[i];
                    tile.bitmask[i] = bitmask[i];
                    continue;
                }
                
                #if MOC_REVERSED_Z
                tile.z[i] = tile.bitmask[i] == 0u ? zMax[i] : math.min(tile.z[i], zMax[i]);
                #else
                tile.z[i] = tile.bitmask[i] == 0u ? zMax[i] : math.max(tile.z[i], zMax[i]);
                #endif
                tile.bitmask[i] |= bitmask[i];
            }
            Tiles[tileIdx] = tile;
        }
    }
}