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
        [ReadOnly] public NativeArray<int4> TileRanges; // 三角形整个的range,需要clamp到bin中
        [ReadOnly] public NativeArray<int3x3> EdgeParams;
        [ReadOnly] public NativeArray<float4> DepthParams;
        
        [NativeDisableParallelForRestriction] public NativeArray<Tile> Tiles;
        
        public void Execute(int binIdx)
        {
            var binRange = new int4(
                binIdx * Constants.NumColsTileInBin,
                0,
                (binIdx + 1) * Constants.NumColsTileInBin - 1,
                Constants.NumRowsTile - 1
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
            var zTileDx = depthParam.x * Constants.NumColsSubTile;
            var zTileDy = depthParam.y * Constants.NumRowsSubTile;
            var zTileRow = depthParam.w + tileRangeOffset.x * zTileDx + tileRangeOffset.y * zTileDy;
            
            var edgeParam = EdgeParams[triIdx];
            var wTileDx = edgeParam.c1 * Constants.TileWidth;
            var wTileDy = edgeParam.c2 * Constants.TileHeight;
            var wTileRow = edgeParam.c0 + tileRangeOffset.x * wTileDx + tileRangeOffset.y * wTileDy;
            
            for (var tileY = clampedTileRange.y; tileY <= clampedTileRange.w; tileY++)
            {
                var zTileBase = zTileRow;
                var wTileBase = wTileRow;
                for (var tileX = clampedTileRange.x; tileX <= clampedTileRange.z; tileX++)
                {
                    RasterizeTile(
                        tileX, tileY, 
                        depthParam.x, depthParam.z, zTileBase,
                        edgeParam.c1, edgeParam.c2, wTileBase
                    );
                    zTileBase += zTileDx;
                    wTileBase += wTileDx;
                }
                zTileRow += zTileDy;
                wTileRow += wTileDy;
            }
        }

        private void RasterizeTile(
            int tileX, int tileY,
            float zSubTileDx, float zTriMax, float zBase,
            in int3 wPixelDx, in int3 wPixelDy, in int3 wBase
        )
        {
            var tileIdx = tileY * Constants.NumColsTile + tileX;
            var bitmask = uint4.zero;
            var zMax = math.min(new float4(0, 1, 2, 3) * zSubTileDx + zBase, zTriMax);
            
            // TODO: 如果四个点都在三角形内，则bitmask=1

            var maskIdx = 0;
            var wRow = wBase;
            for (var y = 0; y < Constants.TileHeight; y++)
            {
                var w = wRow;
                for (var x = 0; x < Constants.TileWidth; x++)
                {
                    // if (w is { x: >= 0, y: >= 0, z: >= 0 })
                    if ((w.x | w.y | w.z) >= 0)
                    {
                        bitmask[x / Constants.SubTileWidth] |= (uint)(1 << (maskIdx + x % Constants.SubTileWidth));
                    }

                    w += wPixelDx;
                }

                wRow += wPixelDy;
                maskIdx += Constants.SubTileWidth;
            }
            
            if (math.all(bitmask == 0u)) return; // TODO
            UpdateTile(tileIdx, bitmask, zMax);
        }
        
        private void UpdateTile(int tileIdx, in uint4 bitmask, in float4 zMax)
        {
            var tile = Tiles[tileIdx];
            for (var i = 0; i < 4; i++)
            {
                if (tile.bitmask[i] == ~0u && zMax[i] >= tile.z[i]) continue;
                if (bitmask[i] == ~0u && zMax[i] < tile.z[i])
                {
                    tile.z[i] = zMax[i];
                    tile.bitmask[i] = bitmask[i];
                    continue;
                }
                tile.z[i] = tile.bitmask[i] == 0u ? zMax[i] : math.max(tile.z[i], zMax[i]);
                tile.bitmask[i] |= bitmask[i];
            }
            Tiles[tileIdx] = tile;
        }
    }
}