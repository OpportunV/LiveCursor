using System.Collections.Generic;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorImportReport
    {
        public List<string> Errors { get; } = new();

        public List<string> Warnings { get; } = new();

        public bool HasErrors => Errors.Count > 0;

        public void Error(string message)
        {
            Errors.Add(message);
        }

        public void Warning(string message)
        {
            Warnings.Add(message);
        }
    }
}
