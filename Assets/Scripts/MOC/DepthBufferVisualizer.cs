using System.IO;
using UnityEngine;

namespace MOC
{
    [RequireComponent(typeof(MaskedOcclusionCulling))]
    public class DepthBufferVisualizer : MonoBehaviour
    {
        [SerializeField] private Texture2D depthBuffer;
        [SerializeField] private Color z0Color = Color.black;
        [SerializeField] private Color z1Color = Color.white;
        [SerializeField] private bool showBitmask;
        [SerializeField] private bool savePng;
        [SerializeField] private bool reverse;
        [SerializeField] private bool remapMinMax;
        [SerializeField]private Vector2 depthRange;
        private MaskedOcclusionCulling _moc;
        
        public void Visualize()
        {
            _moc = GetComponent<MaskedOcclusionCulling>();
            CreateDepthBufferIfNeeded();
            UpdateDepthBuffer();
            if (savePng) SaveTextureAsPNG("Assets/Resources/msoc_depth.png");
        }

        private void CreateDepthBufferIfNeeded()
        {
            if (!depthBuffer || depthBuffer.width != Constants.ScreenWidth ||
                depthBuffer.height != Constants.ScreenHeight)
            {
                depthBuffer = new Texture2D(Constants.ScreenWidth, Constants.ScreenHeight, TextureFormat.ARGB32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
            }
        }

        private void UpdateDepthBuffer()
        {
            var tiles = _moc.GetTiles();
            UpdateDepthRange(tiles);
            for (var i = 0; i < tiles.Length; i++)
            {
                var tileRow = i / Constants.NumColsTile;
                var tileCol = i % Constants.NumColsTile;
                UpdateTile(tileRow, tileCol, tiles[i]);
            }
        }

        private void UpdateDepthRange(Tile[] tiles)
        {
            var minDepth = float.MaxValue;
            var maxDepth = float.MinValue;
            foreach (var tile in tiles)
            {
                for (var i = 0; i < 4; i++)
                {
                    if (Mathf.Approximately(tile.z[i], 1.0f)) continue;
                    minDepth = Mathf.Min(minDepth, tile.z[i]);
                    maxDepth = Mathf.Max(maxDepth, tile.z[i]);
                }
            }
            depthRange = new Vector2(minDepth, maxDepth);
        }

        private void UpdateTile(int tileRow, int tileCol, in Tile tile)
        {
            // NumRowsSubTile = 1
            for (var subTileCol = 0; subTileCol < Constants.NumColsSubTile; subTileCol++)
            {
                var pixelRowStart = tileRow * Constants.TileHeight;
                var pixelColStart = tileCol * Constants.TileWidth + subTileCol * Constants.SubTileWidth;
                var bitmask = tile.bitmask[subTileCol];
                var z = tile.z[subTileCol];
                UpdateSubTile(pixelRowStart, pixelColStart, bitmask, z);
            }
        }

        private void UpdateSubTile(int pixelRowStart, int pixelColStart, uint bitmask, float z)
        {
            for (var row = 0; row < Constants.SubTileHeight; row++)
            {
                for (var col = 0; col < Constants.SubTileWidth; col++)
                {
                    var idx = row * Constants.SubTileWidth + col;
                    var bitValue = (bitmask >> idx) & 1;
                    var pixelRow = pixelRowStart + row;
                    var pixelCol = pixelColStart + col;

                    if (showBitmask)
                    {
                        depthBuffer.SetPixel(pixelCol, pixelRow, bitValue == 1 ? z1Color : z0Color);
                        continue;
                    }

                    if (bitValue == 1)
                    {
                        z = Mathf.Clamp(z, 0.0f, 1.0f);
                        if (reverse) z = 1.0f - z;
                        if (remapMinMax) z = (z - depthRange.x) / (depthRange.y - depthRange.x);
                        depthBuffer.SetPixel(pixelCol, pixelRow, new Color(z, z, z, 1.0f));
                    }
                    else
                    {
                        depthBuffer.SetPixel(pixelCol, pixelRow, z0Color);
                    }
                }
            }
        }
        
        private void SaveTextureAsPNG(string path)
        {
            var pngBytes = depthBuffer.EncodeToPNG();
            File.WriteAllBytes(path, pngBytes);
            Debug.Log("Texture saved as PNG to: " + path);
        }
    }
}