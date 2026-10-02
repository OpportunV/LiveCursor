using System;
// ReSharper disable InconsistentNaming

namespace Opportunv.LiveCursor.Editor
{
    [Serializable]
    internal sealed class CursorCodeDefinition
    {
        public string className;
        public string @namespace;
        public string path;
    }
}
