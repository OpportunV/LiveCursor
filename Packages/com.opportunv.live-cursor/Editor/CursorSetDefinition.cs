// ReSharper disable InconsistentNaming

#pragma warning disable SA1307
#pragma warning disable SA1401

using System;

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
