using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    [Serializable]
    public sealed class CursorFrame
    {
        [SerializeField] private Texture2D[] _textures;
        [SerializeField] private Vector2[] _hotspots;

        public int SizeCount => _textures?.Length ?? 0;

        internal CursorFrame(Texture2D[] textures, Vector2[] hotspots)
        {
            _textures = textures;
            _hotspots = hotspots;
        }

        public Texture2D GetTexture(int sizeIndex)
        {
            return _textures[sizeIndex];
        }

        public Vector2 GetHotspot(int sizeIndex)
        {
            return _hotspots != null && sizeIndex < _hotspots.Length ? _hotspots[sizeIndex] : Vector2.zero;
        }
    }
}
