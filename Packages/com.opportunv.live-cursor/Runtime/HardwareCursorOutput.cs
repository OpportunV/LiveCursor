using UnityEngine;

namespace Opportunv.LiveCursor
{
    public sealed class HardwareCursorOutput : ICursorOutput
    {
        public void Apply(Texture2D texture, Vector2 hotspot)
        {
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
        }

        public void Clear()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
