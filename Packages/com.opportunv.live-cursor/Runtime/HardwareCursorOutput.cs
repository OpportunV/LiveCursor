using UnityEngine;

namespace Opportunv.LiveCursor
{
    /// <summary>Shows frames as the OS hardware cursor with
    /// <see cref="Cursor.SetCursor(Texture2D, Vector2, CursorMode)"/>.</summary>
    public sealed class HardwareCursorOutput : ICursorOutput
    {
        /// <summary>Sets the hardware cursor.</summary>
        public void Apply(Texture2D texture, Vector2 hotspot)
        {
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
        }

        /// <summary>Restores the default OS cursor.</summary>
        public void Clear()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}