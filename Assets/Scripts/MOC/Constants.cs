namespace MOC
{
    public static class Constants
    {
        // High
        // public const int ScreenWidth = 1920;
        // public const int ScreenHeight = 1080;
        // Medium
        public const int ScreenWidth = 960;
        public const int ScreenHeight = 540;
        // Low
        // public const int ScreenWidth = 256;
        // public const int ScreenHeight = 160;

        // FIXED
        public const int TileWidth = 32;
        public const int TileHeight = 4;
        public const int SubTileWidth = 8;
        public const int SubTileHeight = 4;
        public const int NumBins = 4;
        public const int NumColsTile = ScreenWidth / TileWidth;
        public const int NumRowsTile = ScreenHeight / TileHeight;
        public const int NumColsSubTile = TileWidth / SubTileWidth;
        public const int NumRowsSubTile = TileHeight / SubTileHeight;
        public const int BinWidth = ScreenWidth / NumBins;
        public const int NumColsTileInBin = BinWidth / TileWidth;
        // public const int SubPixelPrecision = 4;
    }
}