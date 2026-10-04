// ReSharper disable InconsistentNaming

#pragma warning disable SA1307
#pragma warning disable SA1401

using System;

namespace Opportunv.LiveCursor.Editor
{
    [Serializable]
    internal sealed class CursorTransitionDefinition
    {
        public string from;
        public string to;
        public CursorFramesDefinition frames;
        public float frameDurationMs = 33f;
        public bool includesEndpoints = true;
        public bool reversible = true;
        public float reverseFrameDurationMs;
        public int[] hotspot;
    }
}
