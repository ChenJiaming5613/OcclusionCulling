using Unity.Mathematics;

namespace MOC
{
    public class MocConfig
    {
        public const int TileWidth = 32;
        public const int TileHeight = 4;
        public const int SubTileWidth = 8;
        public const int SubTileHeight = 4;
        public const int NumColsSubTile = TileWidth / SubTileWidth;
        public const int NumRowsSubTile = TileHeight / SubTileHeight;
        
        public readonly int NumBinCols;
        public readonly int NumBinRows;
        public readonly int DepthBufferWidth;
        public readonly int DepthBufferHeight;
        public readonly float CoverageThreshold;
        public readonly bool AsyncRasterizeOccluders;
        public readonly int MaxNumRasterizeTris;
        // public int SubPixelPrecision = 4;

        public readonly int NumColsTile;
        public readonly int NumRowsTile;
        public readonly int NumColsTileInBin;
        public readonly int NumRowsTileInBin;

        public readonly int RayMarchingDownSampleCount;
        public readonly float3 RayMarchingStep;
        
        public MocConfig(MocConfigAsset configAsset)
        {
            NumBinCols = configAsset.numBinCols;
            NumBinRows = configAsset.numBinRows;
            DepthBufferWidth = configAsset.depthBufferWidth;
            DepthBufferHeight = configAsset.depthBufferHeight;
            CoverageThreshold = configAsset.coverageThreshold;
            AsyncRasterizeOccluders = configAsset.asyncRasterizeOccluders;
            MaxNumRasterizeTris = configAsset.maxNumRasterizeTris;

            NumColsTile = DepthBufferWidth / TileWidth;
            NumRowsTile = DepthBufferHeight / TileHeight;
            var binWidth = DepthBufferWidth / NumBinCols;
            NumColsTileInBin = binWidth / TileWidth;
            var binHeight = DepthBufferHeight / NumBinRows;
            NumRowsTileInBin = binHeight / TileHeight;
            
            RayMarchingDownSampleCount = configAsset.rayMarchingDownSampleCount;
            RayMarchingStep = configAsset.rayMarchingStep;
        }
    }
}