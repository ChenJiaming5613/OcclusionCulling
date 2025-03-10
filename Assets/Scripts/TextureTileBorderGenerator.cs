using System.IO;
using UnityEngine;

// TODO: 不正确的复制
public class TextureTileBorderGenerator : MonoBehaviour
{
    [SerializeField] private Texture2D originalTexture;
    [SerializeField] private int tileWidth = 32;
    [SerializeField] private int tileHeight = 4;
    [SerializeField] private Color borderColor = Color.black;
    [SerializeField] private bool savePng;
    [SerializeField] private Texture2D outputTexture;
    
    [ContextMenu("Apply")]
    public void ApplyBorders()
    {
        var originalWidth = originalTexture.width;
        var originalHeight = originalTexture.height;

        var tileCountX = originalWidth / tileWidth;
        var tileCountY = originalHeight / tileHeight;

        var newWidth = originalWidth + tileCountX + 1;
        var newHeight = originalHeight + tileCountY + 1;

        var newPixels = new Color32[newWidth * newHeight];
        var originalPixels = originalTexture.GetPixels32();

        System.Array.Fill(newPixels, new Color32(0, 0, 0, 0));

        for (var ty = 0; ty < tileCountY; ty++)
        {
            for (var tx = 0; tx < tileCountX; tx++)
            {
                CopyTilePixels(
                    originalPixels, newPixels,
                    tx, ty,
                    originalWidth, newWidth,
                    tileWidth, tileHeight);
            }
        }

        for (var ty = 0; ty < tileCountY; ty++)
        {
            for (var tx = 0; tx < tileCountX; tx++)
            {
                DrawTileBorder(newPixels, tx, ty, newWidth, newHeight);
            }
        }

        outputTexture = new Texture2D(newWidth, newHeight);
        outputTexture.SetPixels32(newPixels);
        outputTexture.Apply();
        if (savePng) SavePng();
    }

    private void SavePng()
    {
        var pngBytes = outputTexture.EncodeToPNG();
        var path = $"Assets/Resources/{originalTexture.name}_border.png";
        File.WriteAllBytes(path, pngBytes);
        Debug.Log("Texture saved as PNG to: " + path);
    }

    private static void CopyTilePixels(
        Color32[] source, Color32[] destination,
        int tileX, int tileY,
        int originalWidth, int newWidth,
        int tileWidth, int tileHeight)
    {
        var newTileX = tileX * (tileWidth + 1) + 1;
        var newTileY = tileY * (tileHeight + 1) + 1;

        for (var y = 0; y < tileHeight; y++)
        {
            for (var x = 0; x < tileWidth; x++)
            {
                var srcX = tileX * tileWidth + x;
                var srcY = tileY * tileHeight + y;
                var dstX = newTileX + x;
                var dstY = newTileY + y;

                destination[dstY * newWidth + dstX] = source[srcY * originalWidth + srcX];
            }
        }
    }

    private void DrawTileBorder(Color32[] pixels, int tileX, int tileY, int width, int height)
    {
        var borderLeft = tileX * (tileWidth + 1);
        var borderRight = borderLeft + tileWidth + 1;
        var borderTop = tileY * (tileHeight + 1);
        var borderBottom = borderTop + tileHeight + 1;
        
        for (var x = borderLeft; x <= borderRight; x++)
        {
            SafeSetPixel(pixels, x, borderTop, width, height, borderColor);
            SafeSetPixel(pixels, x, borderBottom, width, height, borderColor);
        }

        for (var y = borderTop; y <= borderBottom; y++)
        {
            SafeSetPixel(pixels, borderLeft, y, width, height, borderColor);
            SafeSetPixel(pixels, borderRight, y, width, height, borderColor);
        }
    }

    private static void SafeSetPixel(Color32[] pixels, int x, int y, int width, int height, Color32 color)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            pixels[y * width + x] = color;
        }
    }
}