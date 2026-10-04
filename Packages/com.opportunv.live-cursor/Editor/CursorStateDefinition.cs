// ReSharper disable InconsistentNaming

#pragma warning disable SA1307
#pragma warning disable SA1401

using System;

namespace Opportunv.LiveCursor.Editor
{
    [Serializable]
    internal sealed class CursorStateDefinition
    {
        public string name;
        public CursorFramesDefinition frames;
        public float frameDurationMs = 100f;
        public float loopDelayMs;
        public int[] hotspot;
    }
}
#pragma warning restore SA1307
#pragma warning restore SA1401
