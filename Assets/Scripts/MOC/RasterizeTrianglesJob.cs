using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MOC
{
    [BurstCompile]
    public struct RasterizeTrianglesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int4> TileRanges;
        [ReadOnly] public NativeArray<int3x3> EdgeParams;
        [ReadOnly] public NativeArray<float4> DepthParams;
        
        [NativeDisableParallelForRestriction] public NativeArray<Tile> Tiles;
        
        public void Execute(int triIdx)
        {
            var tileRange = TileRanges[triIdx];
            if (tileRange.x < 0) return; // clipped
            
            var depthParam = DepthParams[triIdx];
            var zTileDx = depthParam.x * Constants.NumColsSubTile;
            var zTileDy = depthParam.y * Constants.NumRowsSubTile;
            var zTileRow = depthParam.w;
            var edgeParam = EdgeParams[triIdx];
            var wTileRow = edgeParam.c0;
            var wTileDx = edgeParam.c1 * Constants.TileWidth;
            var wTileDy = edgeParam.c2 * Constants.TileHeight;
            for (var tileY = tileRange.y; tileY <= tileRange.w; tileY++)
            {
                var zTileBase = zTileRow;
                var wTileBase = wTileRow;
                for (var tileX = tileRange.x; tileX <= tileRange.z; tileX++)
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
            UpdateTile(tileIdx, ref bitmask, ref zMax);
        }
        
        // private void UpdateTile(int tileIdx, ref uint4 bitmask, ref float4 zMax)
        // {
        //     var tile = Tiles[tileIdx];
        //     var dist1T = tile.z1 - zMax;
        //     var dist01 = tile.z0 - tile.z1;
        //     var flags = dist1T > dist01;
        //     for (var i = 0; i < 4; i++)
        //     {
        //         if (!flags[i]) continue;
        //         tile.z1[i] = 0.0f;
        //         tile.bitmask[i] = 0u;
        //     }
        //     
        //     tile.bitmask |= bitmask;
        //     tile.z1 = math.max(tile.z1, zMax);
        //     flags = tile.bitmask == ~0u;
        //     for (var i = 0; i < 4; i++)
        //     {
        //         if (!flags[i]) continue;
        //         tile.z0[i] = tile.z1[i];
        //         tile.z1[i] = 0.0f;
        //         tile.bitmask[i] = 0u;
        //     }
        //     Tiles[tileIdx] = tile;
        // }

        private void UpdateTile(int tileIdx, ref uint4 bitmask, ref float4 zMax)
        {
            var tile = Tiles[tileIdx];
            for (var i = 0; i < 4; i++)
            {
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