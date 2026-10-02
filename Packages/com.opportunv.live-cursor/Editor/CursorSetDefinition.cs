using System;
// ReSharper disable InconsistentNaming

namespace Opportunv.LiveCursor.Editor
{
    [Serializable]
    internal sealed class CursorSetDefinition
    {
        public int[] sizes = { 32, 48, 64 };
        public int[] hotspot;
        public CursorStateDefinition[] states;
        public CursorTransitionDefinition[] transitions;
        public CursorCodeDefinition code;
    }
}
