using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    [Serializable]
    public sealed class CursorClip
    {
        [SerializeField] private CursorFrame[] _frames;
        [SerializeField, Min(0.001f)] private float _frameDuration;

        public int FrameCount => _frames?.Length ?? 0;

        public float FrameDuration => _frameDuration;

        internal CursorClip(CursorFrame[] frames, float frameDuration)
        {
            _frames = frames;
            _frameDuration = frameDuration;
        }

        public CursorFrame GetFrame(int index)
        {
            return _frames[index];
        }
    }
}
