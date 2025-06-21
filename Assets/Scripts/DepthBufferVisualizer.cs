﻿using System.IO;
using MOC;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[RequireComponent(typeof(CullingSystem))]
public class DepthBufferVisualizer : MonoBehaviour
{
    [SerializeField] private Texture2D depthBuffer;
    [SerializeField] private bool showBitmask;
    [SerializeField] private bool visTileBorder;
    [SerializeField] private bool savePng;
    [SerializeField] private bool reverse;
    [SerializeField] private bool remapMinMax;
    [SerializeField] private float depthScale = 1.0f;
    [SerializeField] private Vector2 depthRange;
    private CullingSystem _cullingSystem;
    private MaskedOcclusionCulling _moc;
        
    public void Visualize()
    {
        _cullingSystem = GetComponent<CullingSystem>();
        _moc = _cullingSystem.GetMaskedOcclusionCulling();
        CreateDepthBufferIfNeeded();
        UpdateDepthBuffer();
        if (savePng) SaveTextureAsPNG("Assets/Resources/msoc_depth.png");
    }

    private void CreateDepthBufferIfNeeded()
    {
        var config = _moc.GetConfig();
        if (!depthBuffer || depthBuffer.width != config.DepthBufferWidth ||
            depthBuffer.height != config.DepthBufferHeight)
        {
            depthBuffer = new Texture2D(config.DepthBufferWidth, config.DepthBufferHeight, GraphicsFormat.R32_SFloat, TextureCreationFlags.None)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
        }
    }

    private void UpdateDepthBuffer()
    {
        var tiles = _moc.GetTiles();
        var config = _moc.GetConfig();
        UpdateDepthRange(tiles);
        for (var i = 0; i < tiles.Length; i++)
        {
            var tileRow = i / config.NumColsTile;
            var tileCol = i % config.NumColsTile;
            UpdateTile(tileRow, tileCol, tiles[i]);
        }
        if (visTileBorder) ApplyTileBorder();
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
                if (tile.z[i] > 0) minDepth = Mathf.Min(minDepth, tile.z[i]);
                maxDepth = Mathf.Max(maxDepth, tile.z[i]);
            }
        }
        depthRange = new Vector2(minDepth, maxDepth);
    }

    private void UpdateTile(int tileRow, int tileCol, in Tile tile)
    {
        // NumRowsSubTile = 1
        for (var subTileCol = 0; subTileCol < MocConfig.NumColsSubTile; subTileCol++)
        {
            var pixelRowStart = tileRow * MocConfig.TileHeight;
            var pixelColStart = tileCol * MocConfig.TileWidth + subTileCol * MocConfig.SubTileWidth;
            var bitmask = tile.bitmask[subTileCol];
            var z = tile.z[subTileCol];
            UpdateSubTile(pixelRowStart, pixelColStart, bitmask, z);
        }
    }

    private void UpdateSubTile(int pixelRowStart, int pixelColStart, uint bitmask, float z)
    {
        for (var row = 0; row < MocConfig.SubTileHeight; row++)
        {
            for (var col = 0; col < MocConfig.SubTileWidth; col++)
            {
                var idx = (MocConfig.SubTileHeight - 1 - row) * MocConfig.SubTileWidth + col;
                var bitValue = (bitmask >> (31 - idx)) & 1;
                var pixelRow = pixelRowStart + row;
                var pixelCol = pixelColStart + col;

                if (showBitmask)
                {
                    depthBuffer.SetPixel(pixelCol, pixelRow, new Color(bitValue, 0, 0, 0));
                    continue;
                }

                if (bitValue == 1)
                {
                    z = Mathf.Clamp(z, 0.0f, 1.0f);
                    if (reverse) z = 1.0f - z;
                    if (remapMinMax) z = (z - depthRange.x) / (depthRange.y - depthRange.x);
                    depthBuffer.SetPixel(pixelCol, pixelRow, new Color(z * depthScale, 0, 0, 0));
                }
                else
                {
                    depthBuffer.SetPixel(pixelCol, pixelRow, new Color(0, 0, 0, 0));
                }
            }
        }
    }

    private void ApplyTileBorder()
    {
        for (var y = 0; y < depthBuffer.height; y++)
        {
            for (var x = 0; x < depthBuffer.width; x++)
            {
                if (x % MocConfig.TileWidth != 0 && y % MocConfig.TileHeight != 0) continue;
                var color = depthBuffer.GetPixel(x, y) * 0.5f;
                depthBuffer.SetPixel(x, y, new Color(color.r, color.g, color.b, 1.0f));
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