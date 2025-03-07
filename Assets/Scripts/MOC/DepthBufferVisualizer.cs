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
            if (depthBuffer == null ||
                depthBuffer.width != Constants.ScreenWidth ||
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
            for (var i = 0; i < tiles.Length; i++)
            {
                var tileRow = i / Constants.NumColsTile;
                var tileCol = i % Constants.NumColsTile;
                UpdateTile(tileRow, tileCol, tiles[i]);
            }
        }

        private void UpdateTile(int tileRow, int tileCol, in Tile tile)
        {
            // NumRowsSubTile = 1
            for (var subTileCol = 0; subTileCol < Constants.NumColsSubTile; subTileCol++)
            {
                var pixelRowStart = tileRow * Constants.TileHeight;
                var pixelColStart = tileCol * Constants.TileWidth + subTileCol * Constants.SubTileWidth;
                var bitmask = tile.bitmask[subTileCol];
                var z0 = tile.z0[subTileCol];
                var z1 = tile.z1[subTileCol];
                UpdateSubTile(pixelRowStart, pixelColStart, bitmask, z0, z1);
            }
        }

        private void UpdateSubTile(int pixelRowStart, int pixelColStart, uint bitmask, float z0, float z1)
        {
            for (var row = 0; row < Constants.SubTileHeight; row++)
            {
                for (var col = 0; col < Constants.SubTileWidth; col++)
                {
                    var idx = row * Constants.SubTileWidth + col;
                    var bitValue = (bitmask >> (31 - idx)) & 1; 
                    var pixelRow = pixelRowStart + row;
                    var pixelCol = pixelColStart + col;

                    if (showBitmask)
                    {
                        depthBuffer.SetPixel(pixelCol, pixelRow, bitValue == 1 ? z1Color : z0Color);
                    }
                    else
                    {
                        var weightZ0 = Mathf.Clamp(z0, 0.0f, 1.0f);
                        var weightZ1 = Mathf.Clamp(z1, 0.0f, 1.0f);
                        if (reverse)
                        {
                            weightZ0 = 1.0f - weightZ0;
                            weightZ1 = 1.0f - weightZ1;
                        }
                        depthBuffer.SetPixel(pixelCol, pixelRow, bitValue == 1
                            ? new Color(weightZ1, weightZ1, weightZ1, 1.0f)
                            : new Color(weightZ0, weightZ0, weightZ0, 1.0f));
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