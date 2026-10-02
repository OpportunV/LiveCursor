using System;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    internal static class CursorPixelComparer
    {
        private const int PixelTolerance = 2;

        public static int CountDifferentPixels(Texture2D left, Texture2D right)
        {
            var a = left.GetPixels32();
            var b = right.GetPixels32();
            var count = 0;
            for (var i = 0; i < a.Length; i++)
            {
                var p = a[i];
                var q = b[i];
                if (p.a == 0 && q.a == 0)
                {
                    continue;
                }

                if (Math.Abs(p.r - q.r) > PixelTolerance || Math.Abs(p.g - q.g) > PixelTolerance ||
                    Math.Abs(p.b - q.b) > PixelTolerance || Math.Abs(p.a - q.a) > PixelTolerance)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
