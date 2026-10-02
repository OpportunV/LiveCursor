namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorBuilderState
    {
        public CursorScannedClip Clip { get; }

        public bool Include { get; set; } = true;

        public string Name { get; set; }

        public float FrameDurationMs { get; set; } = CursorBuilderModel.DefaultStateFrameMs;

        public float LoopDelayMs { get; set; }

        public bool IsMissing => Clip.FramePaths.Count == 0;

        public CursorBuilderState(CursorScannedClip clip)
        {
            Clip = clip;
            Name = clip.Name;
        }
    }
}
