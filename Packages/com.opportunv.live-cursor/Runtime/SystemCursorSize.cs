using System.Runtime.InteropServices;

namespace Opportunv.LiveCursor
{
    /// <summary>Reads the size, in pixels, at which the OS creates hardware cursors.</summary>
    public static class SystemCursorSize
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int SmCxCursor = 13;

        /// <summary>Returns the system cursor size, or 0 where it is unknown.</summary>
        public static int Get()
        {
            return GetSystemMetrics(SmCxCursor);
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);
#else
        /// <summary>Returns the system cursor size, or 0 where it is unknown.</summary>
        public static int Get()
        {
            return 0;
        }
#endif
    }
}
