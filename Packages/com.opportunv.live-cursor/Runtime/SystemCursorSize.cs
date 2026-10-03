using System.Runtime.InteropServices;

namespace Opportunv.LiveCursor
{
    public static class SystemCursorSize
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int SmCxCursor = 13;

        public static int Get()
        {
            return GetSystemMetrics(SmCxCursor);
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);
#else
        public static int Get()
        {
            return 0;
        }
#endif
    }
}
