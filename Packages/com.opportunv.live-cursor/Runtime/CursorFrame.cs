using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    [Serializable]
    public sealed class CursorFrame
    {
        [SerializeField] private Texture2D[] _textures;

        public int SizeCount => _textures?.Length ?? 0;

        internal CursorFrame(Texture2D[] textures)
        {
            _textures = textures;
        }

        public Texture2D GetTexture(int sizeIndex)
        {
            return _textures[sizeIndex];
        }
    }
}
