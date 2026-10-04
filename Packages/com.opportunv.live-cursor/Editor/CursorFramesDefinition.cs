// ReSharper disable InconsistentNaming

#pragma warning disable SA1307
#pragma warning disable SA1401

using System;

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
