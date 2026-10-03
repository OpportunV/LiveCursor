using System.IO;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    internal static class CursorClipFrameReader
    {
        public static Texture2D Read(CursorScannedClip clip, int index)
        {
            if (index < 0 || index >= clip.FrameCount)
            {
                return null;
            }

            var path = clip.IsSheet ? clip.FramePaths[0] : clip.FramePaths[index];
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
            if (!File.Exists(path) || !texture.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(texture);
                return null;
            }

            if (!clip.IsSheet || !clip.Sheet.Fits(texture.width, texture.height))
            {
                return texture;
            }

            var cell = Crop(texture, clip.Sheet.CellRect(index, texture.width, texture.height));
            Object.DestroyImmediate(texture);
            return cell;
        }

        public static Texture2D Crop(Texture2D source, RectInt rect)
        {
            Texture2D cell = new(rect.width, rect.height, TextureFormat.RGBA32, false);
            cell.SetPixels(source.GetPixels(rect.x, rect.y, rect.width, rect.height));
            cell.Apply(false, false);
            return cell;
        }
    }
}
