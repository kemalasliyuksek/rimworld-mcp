using System;
using System.Collections.Generic;
using UnityEngine;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Draws icons onto one image, so that an assistant can see what they look like. Game textures are
    /// usually not readable from code, so each one goes through a render texture first.
    /// </summary>
    internal static class IconSheet
    {
        public const int CellSize = 72;

        private const int Padding = 4;

        private static readonly Color Background = new Color(0.12f, 0.13f, 0.15f, 1f);

        public static string RenderToBase64Png(IList<Texture2D> icons, int columns)
        {
            int rows = (icons.Count + columns - 1) / columns;
            var sheet = new Texture2D(columns * CellSize, rows * CellSize, TextureFormat.RGBA32, mipChain: false);
            var cell = new Texture2D(CellSize - Padding * 2, CellSize - Padding * 2, TextureFormat.RGBA32, mipChain: false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(cell.width, cell.height, 0, RenderTextureFormat.ARGB32);
            try
            {
                var fill = new Color[sheet.width * sheet.height];
                for (int i = 0; i < fill.Length; i++)
                {
                    fill[i] = Background;
                }
                sheet.SetPixels(fill);

                for (int index = 0; index < icons.Count; index++)
                {
                    target.DiscardContents();
                    Graphics.Blit(icons[index], target);
                    RenderTexture.active = target;
                    cell.ReadPixels(new Rect(0, 0, cell.width, cell.height), 0, 0);
                    cell.Apply();
                    int row = rows - 1 - index / columns;
                    int x = index % columns * CellSize + Padding;
                    int y = row * CellSize + Padding;
                    Blend(sheet, cell, x, y);
                }
                sheet.Apply();
                return Convert.ToBase64String(sheet.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(cell);
                UnityEngine.Object.Destroy(sheet);
            }
        }

        private static void Blend(Texture2D sheet, Texture2D cell, int x, int y)
        {
            Color[] pixels = cell.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color color = pixels[i];
                pixels[i] = Color.Lerp(Background, new Color(color.r, color.g, color.b, 1f), color.a);
            }
            sheet.SetPixels(x, y, cell.width, cell.height, pixels);
        }
    }
}
