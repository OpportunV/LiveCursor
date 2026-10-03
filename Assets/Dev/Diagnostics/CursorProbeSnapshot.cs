namespace Dev.Diagnostics
{
    public struct CursorProbeSnapshot
    {
        public int SystemMetricWidth;
        public int SystemMetricForDpiWidth;
        public int WindowDpi;
        public int CursorBaseSize;
        public int ActiveCursorWidth;
        public int ActiveCursorHeight;
        public int ActiveHotspotX;
        public int ActiveHotspotY;
        public int GdiObjects;
        public int UserObjects;
        public bool Valid;
    }
}