using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    internal readonly struct CursorSheetLayout : IEquatable<CursorSheetLayout>
    {
        public const int MinCellSize = 8;

        public int Columns { get; }

        public int Rows { get; }

        public int Count { get; }

        public bool IsSheet => Columns > 0 && Rows > 0;

        public int CellCount => Columns * Rows;

        public int FrameCount => Count > 0 ? Mathf.Min(Count, CellCount) : CellCount;

        private static readonly Regex _suffix = new(@"^(?<name>.*?)[ _\-.]?(?<columns>\d+)[xX](?<rows>\d+)(?:[ _\-.](?<count>\d+))?$",
            RegexOptions.CultureInvariant);

        private static readonly Regex _text = new(@"^\s*(?<columns>\d+)\s*[xX]\s*(?<rows>\d+)\s*(?:[:/ _\-]\s*(?<count>\d+))?\s*$",
            RegexOptions.CultureInvariant);

        public CursorSheetLayout(int columns, int rows, int count)
        {
            Columns = columns;
            Rows = rows;
            Count = count;
        }

        public static bool TryParseFileName(string fileName, out string name, out CursorSheetLayout layout)
        {
            var match = _suffix.Match(fileName ?? string.Empty);
            name = match.Success ? match.Groups["name"].Value.TrimEnd(' ', '_', '-', '.') : fileName;
            layout = match.Success ? FromMatch(match) : default;
            return match.Success && name.Length > 0 && layout.CellCount > 1;
        }

        public static bool TryParse(string text, out CursorSheetLayout layout)
        {
            var match = _text.Match(text ?? string.Empty);
            layout = match.Success ? FromMatch(match) : default;
            return match.Success && layout.IsSheet;
        }

        public bool Fits(int width, int height)
        {
            return IsSheet && width % Columns == 0 && height % Rows == 0 && width / Columns >= MinCellSize &&
                   height / Rows >= MinCellSize;
        }

        public RectInt CellRect(int index, int width, int height)
        {
            var cellWidth = width / Columns;
            var cellHeight = height / Rows;
            var column = index % Columns;
            var row = index / Columns;
            return new(column * cellWidth, height - (row + 1) * cellHeight, cellWidth, cellHeight);
        }

        public bool Equals(CursorSheetLayout other)
        {
            return Columns == other.Columns && Rows == other.Rows && Count == other.Count;
        }

        public override bool Equals(object obj)
        {
            return obj is CursorSheetLayout other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Columns, Rows, Count);
        }

        public override string ToString()
        {
            if (!IsSheet)
            {
                return "1";
            }

            return Count > 0 && Count < CellCount ? $"{Columns}x{Rows}:{Count}" : $"{Columns}x{Rows}";
        }

        private static CursorSheetLayout FromMatch(Match match)
        {
            var columns = Parse(match.Groups["columns"]);
            var rows = Parse(match.Groups["rows"]);
            var count = Parse(match.Groups["count"]);
            return columns > 0 && rows > 0 ? new(columns, rows, count) : default;
        }

        private static int Parse(Group group)
        {
            return group.Success && int.TryParse(group.Value, out var value) && value <= 4096 ? value : 0;
        }
    }
}
