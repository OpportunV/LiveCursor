namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorBuilderTransition
    {
        public CursorScannedClip Clip { get; }

        public bool Include { get; set; } = true;

        public string From { get; set; }

        public string To { get; set; }

        public float FrameDurationMs { get; set; } = CursorBuilderModel.DefaultTransitionFrameMs;

        public bool Reversible { get; set; } = true;

        public float ReverseFrameDurationMs { get; set; }

        public bool IncludesEndpoints { get; set; } = true;

        public bool IsMissing => Clip.FramePaths.Count == 0;

        public CursorBuilderTransition(CursorScannedClip clip, string from, string to)
        {
            Clip = clip;
            From = from;
            To = to;
        }
    }
}
