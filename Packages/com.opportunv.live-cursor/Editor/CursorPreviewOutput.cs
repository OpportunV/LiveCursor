using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorPreviewOutput : ICursorOutput
    {
        public Texture2D Texture { get; private set; }

        public Vector2 Hotspot { get; private set; }

        public bool Changed { get; set; }

        public void Apply(Texture2D texture, Vector2 hotspot)
        {
            Texture = texture;
            Hotspot = hotspot;
            Changed = true;
        }

        public void Clear()
        {
            Texture = null;
            Hotspot = Vector2.zero;
            Changed = true;
        }
    }
}
