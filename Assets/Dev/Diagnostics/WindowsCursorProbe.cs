using System;
using System.Runtime.InteropServices;

namespace Dev.Diagnostics
{
    public static class WindowsCursorProbe
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int SmCxCursor = 13;
        private const uint RrfRtRegDword = 0x00000010;
        private const uint GrGdiObjects = 0;
        private const uint GrUserObjects = 1;
        private static readonly IntPtr _hkeyCurrentUser = new(unchecked((int)0x80000001));

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CursorInfo
        {
            public int Size;
            public int Flags;
            public IntPtr Cursor;
            public Point Position;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IconInfo
        {
            public int IsIcon;
            public int HotspotX;
            public int HotspotY;
            public IntPtr Mask;
            public IntPtr Color;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Bitmap
        {
            public int Type;
            public int Width;
            public int Height;
            public int WidthBytes;
            public ushort Planes;
            public ushort BitsPixel;
            public IntPtr Bits;
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(ref CursorInfo info);

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);

        [DllImport("user32.dll")]
        private static extern uint GetGuiResources(IntPtr process, uint flags);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr handle, int size, out Bitmap bitmap);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegGetValueW(IntPtr key, string subKey, string valueName, uint flags, IntPtr type, out int data, ref uint dataSize);

        public static CursorProbeSnapshot Capture()
        {
            CursorProbeSnapshot snapshot = new()
            {
                Valid = true,
                SystemMetricWidth = GetSystemMetrics(SmCxCursor)
            };

            var window = GetActiveWindow();
            var dpi = window != IntPtr.Zero ? GetDpiForWindow(window) : 0;
            snapshot.WindowDpi = (int)dpi;
            snapshot.SystemMetricForDpiWidth = dpi != 0 ? GetSystemMetricsForDpi(SmCxCursor, dpi) : 0;

            uint dataSize = sizeof(int);
            if (RegGetValueW(_hkeyCurrentUser, "Control Panel\\Cursors", "CursorBaseSize", RrfRtRegDword, IntPtr.Zero, out var baseSize, ref dataSize) == 0)
            {
                snapshot.CursorBaseSize = baseSize;
            }

            CursorInfo cursorInfo = new()
            {
                Size = Marshal.SizeOf<CursorInfo>()
            };
            if (GetCursorInfo(ref cursorInfo) && cursorInfo.Cursor != IntPtr.Zero && GetIconInfo(cursorInfo.Cursor, out var iconInfo))
            {
                snapshot.ActiveHotspotX = iconInfo.HotspotX;
                snapshot.ActiveHotspotY = iconInfo.HotspotY;
                var bitmapHandle = iconInfo.Color != IntPtr.Zero ? iconInfo.Color : iconInfo.Mask;
                if (GetObject(bitmapHandle, Marshal.SizeOf<Bitmap>(), out var bitmap) != 0)
                {
                    snapshot.ActiveCursorWidth = bitmap.Width;
                    snapshot.ActiveCursorHeight = iconInfo.Color != IntPtr.Zero ? bitmap.Height : bitmap.Height / 2;
                }

                if (iconInfo.Color != IntPtr.Zero)
                {
                    DeleteObject(iconInfo.Color);
                }

                if (iconInfo.Mask != IntPtr.Zero)
                {
                    DeleteObject(iconInfo.Mask);
                }
            }

            var process = GetCurrentProcess();
            snapshot.GdiObjects = (int)GetGuiResources(process, GrGdiObjects);
            snapshot.UserObjects = (int)GetGuiResources(process, GrUserObjects);
            return snapshot;
        }
#else
        public static CursorProbeSnapshot Capture()
        {
            return new CursorProbeSnapshot();
        }
#endif
    }
}
