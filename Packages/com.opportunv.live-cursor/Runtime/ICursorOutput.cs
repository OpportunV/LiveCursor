using UnityEngine;

namespace Opportunv.LiveCursor
{
    /// <summary>Receives the frames a <see cref="CursorPlayer"/> shows. <see cref="HardwareCursorOutput"/> sets the OS
    /// cursor; implement this to draw the cursor elsewhere or to record frames in tests.</summary>
    public interface ICursorOutput
    {
        /// <summary>Shows <paramref name="texture"/> with its click point at <paramref name="hotspot"/>, in pixels from
        /// the top-left corner.</summary>
        public void Apply(Texture2D texture, Vector2 hotspot);

        /// <summary>Restores the default cursor.</summary>
        public void Clear();
    }
}
