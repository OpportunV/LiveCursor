using System;
using System.Runtime.InteropServices;

namespace Opportunv.LiveCursor
{
    public static class SystemCursorSize
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int SmCxCursor = 13;

        private static bool _dpiApiMissing;

        public static int Get()
        {
            if (!_dpiApiMissing)
            {
                try
                {
                    var window = GetActiveWindow();
                    if (window != IntPtr.Zero)
                    {
                        var dpi = GetDpiForWindow(window);
                        if (dpi != 0)
                        {
                            return GetSystemMetricsForDpi(SmCxCursor, dpi);
                        }
                    }
                }
                catch (EntryPointNotFoundException)
                {
                    _dpiApiMissing = true;
                }
            }

            return GetSystemMetrics(SmCxCursor);
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);
#else
        public static int Get()
        {
            return 0;
        }
#endif
    }
}
