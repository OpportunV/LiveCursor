// ReSharper disable InconsistentNaming

#pragma warning disable SA1307
#pragma warning disable SA1401

using System;

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
