using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    internal static class CursorFrameScaler
    {
        private static readonly float[] _srgbToLinear = CreateSrgbToLinearTable();

        public static Color32[] Scale(Color32[] source, int sourceWidth, int sourceHeight, int width, int height)
        {
            if (sourceWidth == width && sourceHeight == height)
            {
                return (Color32[])source.Clone();
            }

            var output = new Color32[width * height];
            var scaleX = (float)sourceWidth / width;
            var scaleY = (float)sourceHeight / height;

            for (var y = 0; y < height; y++)
            {
                var y0 = y * scaleY;
                var y1 = y0 + scaleY;
                var yEnd = Mathf.Min(Mathf.CeilToInt(y1), sourceHeight);
                for (var x = 0; x < width; x++)
                {
                    var x0 = x * scaleX;
                    var x1 = x0 + scaleX;
                    var xEnd = Mathf.Min(Mathf.CeilToInt(x1), sourceWidth);
                    var r = 0f;
                    var g = 0f;
                    var b = 0f;
                    var a = 0f;
                    var weight = 0f;

                    for (var sy = (int)y0; sy < yEnd; sy++)
                    {
                        var wy = Mathf.Min(y1, sy + 1) - Mathf.Max(y0, sy);
                        for (var sx = (int)x0; sx < xEnd; sx++)
                        {
                            var wx = Mathf.Min(x1, sx + 1) - Mathf.Max(x0, sx);
                            var w = wx * wy;
                            var pixel = source[sy * sourceWidth + sx];
                            var alpha = pixel.a / 255f * w;
                            r += _srgbToLinear[pixel.r] * alpha;
                            g += _srgbToLinear[pixel.g] * alpha;
                            b += _srgbToLinear[pixel.b] * alpha;
                            a += alpha;
                            weight += w;
                        }
                    }

                    output[y * width + x] = a > 0f && weight > 0f
                        ? new(ToSrgb(r / a), ToSrgb(g / a), ToSrgb(b / a), ToByte(a / weight))
                        : new Color32(0, 0, 0, 0);
                }
            }

            return output;
        }

        private static float[] CreateSrgbToLinearTable()
        {
            var table = new float[256];
            for (var i = 0; i < table.Length; i++)
            {
                table[i] = Mathf.GammaToLinearSpace(i / 255f);
            }

            return table;
        }

        private static byte ToSrgb(float linear)
        {
            return ToByte(Mathf.LinearToGammaSpace(Mathf.Clamp01(linear)));
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }
    }
}
