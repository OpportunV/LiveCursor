using System;
// ReSharper disable InconsistentNaming

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
