using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    /// <summary>One cursor frame, baked as a texture and hotspot for every size of its
    /// <see cref="CursorSet"/>.</summary>
    [Serializable]
    public sealed class CursorFrame
    {
        [SerializeField] private Texture2D[] _textures;
        [SerializeField] private Vector2[] _hotspots;

        /// <summary>Gets the number of baked sizes.</summary>
        public int SizeCount => _textures?.Length ?? 0;

        internal CursorFrame(Texture2D[] textures, Vector2[] hotspots)
        {
            _textures = textures;
            _hotspots = hotspots;
        }

        /// <summary>Returns the texture baked for the size at <paramref name="sizeIndex"/>.</summary>
        public Texture2D GetTexture(int sizeIndex)
        {
            return _textures[sizeIndex];
        }

        /// <summary>Returns the click point, in pixels from the top-left corner, for the size at
        /// <paramref name="sizeIndex"/>.</summary>
        public Vector2 GetHotspot(int sizeIndex)
        {
            return _hotspots != null && sizeIndex < _hotspots.Length ? _hotspots[sizeIndex] : Vector2.zero;
        }
    }
}
