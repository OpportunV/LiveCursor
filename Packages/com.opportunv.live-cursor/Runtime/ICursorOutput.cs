using UnityEngine;

namespace Opportunv.LiveCursor
{
    public interface ICursorOutput
    {
        public void Apply(Texture2D texture, Vector2 hotspot);

        public void Clear();
    }
}
