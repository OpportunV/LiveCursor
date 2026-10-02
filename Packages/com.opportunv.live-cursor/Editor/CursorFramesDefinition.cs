using System;
// ReSharper disable InconsistentNaming

namespace Opportunv.LiveCursor.Editor
{
    [Serializable]
    internal sealed class CursorFramesDefinition
    {
        public string folder;
        public string[] files;
        public string sheet;
        public int columns;
        public int rows;
        public int count;
    }
}
