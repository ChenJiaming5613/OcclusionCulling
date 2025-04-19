using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MOC.TerrainRayMarching
{
    [BurstCompile]
    public struct InterpolateDepthBufferJob : IJobParallelFor
    {
        public int2 TileGridSize;
        public int2 DepthBufferSize;
        [ReadOnly] public NativeArray<float> DepthBuffer;
        [WriteOnly] public NativeArray<Tile> Tiles;
        
        public void Execute(int tileIdx)
        {
            var tile = new Tile
            {
                bitmask = 0u,
                z = 1.0f
            };
            var tileY = tileIdx / TileGridSize.x;
            var tileX = tileIdx % TileGridSize.x;
            var y = tileY;
            var x = tileX * 8 + (y % 2 == 0 ? 1 : 0);
            // var pixelIdx = y * DepthBufferSize.x + x;
            for (var subTileIdx = 0; subTileIdx < 4; subTileIdx++)
            {
                var left = new int2(math.max(x - 1, 0), y);
                var right = new int2(math.min(x + 1, DepthBufferSize.x - 1), y);
                var bottom = new int2(x, math.max(y - 1, 0));
                var top = new int2(x, math.min(y + 1, DepthBufferSize.y - 1));
                var depth = DepthBuffer[left.y * DepthBufferSize.x + left.x];
                depth = math.max(depth, DepthBuffer[right.y * DepthBufferSize.x + right.x]);
                depth = math.max(depth, DepthBuffer[bottom.y * DepthBufferSize.x + bottom.x]);
                depth = math.max(depth, DepthBuffer[top.y * DepthBufferSize.x + top.x]);
                x += 2;
                // DepthBuffer[pixelIdx] = depth;
                // pixelIdx += 2;
                if (Mathf.Approximately(depth, 1.0f)) continue;
                tile.bitmask[subTileIdx] = ~0u;
                tile.z[subTileIdx] = depth;
            }
            Tiles[tileIdx] = tile;
        }
    }
}

// TODO:回退到sprint-008,resize 0.5, save png