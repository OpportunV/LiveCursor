using UnityEngine;

namespace Dev.Diagnostics
{
    public static class CursorPatternTextures
    {
        private static readonly Color32 _border = new(255, 40, 40, 255);
        private static readonly Color32 _dark = new(0, 0, 0, 255);
        private static readonly Color32 _light = new(255, 255, 255, 255);

        public static Texture2D Create(int size)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Checker {size}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    pixels[y * size + x] = edge ? _border : ((x + y) & 1) == 0 ? _dark : _light;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
