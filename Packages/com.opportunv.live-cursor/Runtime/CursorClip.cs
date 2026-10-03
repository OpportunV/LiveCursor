using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    /// <summary>A sequence of baked cursor frames played at a fixed rate.</summary>
    [Serializable]
    public sealed class CursorClip
    {
        [SerializeField] private CursorFrame[] _frames;
        [SerializeField, Min(0.001f)] private float _frameDuration;

        /// <summary>The number of frames.</summary>
        public int FrameCount => _frames?.Length ?? 0;

        /// <summary>Seconds each frame is shown.</summary>
        public float FrameDuration => _frameDuration;

        internal CursorClip(CursorFrame[] frames, float frameDuration)
        {
            _frames = frames;
            _frameDuration = frameDuration;
        }

        /// <summary>Returns the frame at <paramref name="index"/>.</summary>
        public CursorFrame GetFrame(int index)
        {
            return _frames[index];
        }
    }
}
