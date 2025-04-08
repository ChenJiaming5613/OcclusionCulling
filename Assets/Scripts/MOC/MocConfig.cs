namespace MOC
{
    public struct MocConfig
    {
        public const int TileWidth = 32;
        public const int TileHeight = 4;
        public const int SubTileWidth = 8;
        public const int SubTileHeight = 4;
        public const int NumBins = 4;
        public const int NumColsSubTile = TileWidth / SubTileWidth;
        public const int NumRowsSubTile = TileHeight / SubTileHeight;
        
        public int DepthBufferWidth;
        public int DepthBufferHeight;
        public float CoverageThreshold;
        public bool AsyncRasterizeOccluders;
        // public int SubPixelPrecision = 4;

        public int NumColsTile;
        public int NumRowsTile;
        public int BinWidth;
        public int NumColsTileInBin;
        
        public MocConfig(MocConfigAsset configAsset)
        {
            DepthBufferWidth = configAsset.depthBufferWidth;
            DepthBufferHeight = configAsset.depthBufferHeight;
            CoverageThreshold = configAsset.coverageThreshold;
            AsyncRasterizeOccluders = configAsset.asyncRasterizeOccluders;

            NumColsTile = DepthBufferWidth / TileWidth;
            NumRowsTile = DepthBufferHeight / TileHeight;
            BinWidth = DepthBufferWidth / NumBins;
            NumColsTileInBin = BinWidth / TileWidth;
        }
    }
}