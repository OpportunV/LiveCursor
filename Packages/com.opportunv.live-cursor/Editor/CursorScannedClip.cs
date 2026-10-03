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

        public CursorSheetLayout Sheet { get; set; }

        public bool IsFolder => Folder != null;

        public bool CanBeSheet => FramePaths.Count == 1 && Problem == null;

        public bool IsSheet => CanBeSheet && Sheet.IsSheet;

        public int FrameCount => IsSheet ? Sheet.FrameCount : FramePaths.Count;

        public int FrameWidth => IsSheet ? Width / Sheet.Columns : Width;

        public int FrameHeight => IsSheet ? Height / Sheet.Rows : Height;

        public string Key => Folder ?? (FramePaths.Count > 0 ? FramePaths[0] : Name);

        public CursorScannedClip(string name, string folder, IReadOnlyList<string> framePaths, int width, int height,
            string problem, CursorSheetLayout sheet = default)
        {
            Name = name;
            Folder = folder;
            FramePaths = framePaths;
            Width = width;
            Height = height;
            Problem = problem;
            Sheet = sheet;
        }

        public bool Matches(string key)
        {
            return Key == key || (FramePaths.Count == 1 && FramePaths[0] == key);
        }

        public string SheetProblem()
        {
            if (!IsSheet || Sheet.Fits(Width, Height))
            {
                return null;
            }

            return $"{Width}x{Height} does not split into {Sheet.Columns}x{Sheet.Rows} cells of at least {CursorSheetLayout.MinCellSize} px.";
        }
    }
}
