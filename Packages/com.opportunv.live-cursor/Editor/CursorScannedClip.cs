using System.Collections.Generic;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorScannedClip
    {
        public string Name { get; }

        public string Folder { get; }

        public IReadOnlyList<string> FramePaths { get; }

        public int Width { get; }

        public int Height { get; }

        public string Problem { get; }

        public bool IsFolder => Folder != null;

        public string Key => Folder ?? (FramePaths.Count > 0 ? FramePaths[0] : Name);

        public CursorScannedClip(string name, string folder, IReadOnlyList<string> framePaths, int width, int height,
            string problem)
        {
            Name = name;
            Folder = folder;
            FramePaths = framePaths;
            Width = width;
            Height = height;
            Problem = problem;
        }
    }
}
