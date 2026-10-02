using System.Collections.Generic;
using UnityEngine;

namespace Opportunv.LiveCursor.Tests.Editor
{
    internal sealed class RecordingCursorOutput : ICursorOutput
    {
        public int ApplyCount => _applied.Count;

        public int ClearCount { get; private set; }

        public string Last => _applied.Count > 0 ? _applied[^1].name : null;

        public Vector2 LastHotspot { get; private set; }

        private readonly List<Texture2D> _applied = new(4096);

        public void Apply(Texture2D texture, Vector2 hotspot)
        {
            _applied.Add(texture);
            LastHotspot = hotspot;
        }

        public void Clear()
        {
            ClearCount++;
        }

        public string NameAt(int index)
        {
            return _applied[index].name;
        }
    }
}