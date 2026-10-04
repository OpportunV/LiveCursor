using System.Diagnostics;
using Opportunv.LiveCursor;
using UnityEngine;

namespace Dev.Diagnostics
{
    public sealed class TimingCursorOutput : ICursorOutput
    {
        public int Count { get; private set; }

        public double MaxMs { get; private set; }

        public double AverageMs => Count > 0 ? _totalMs / Count : 0d;

        private readonly HardwareCursorOutput _hardware = new();
        private readonly Stopwatch _stopwatch = new();
        private double _totalMs;

        public void Apply(Texture2D texture, Vector2 hotspot)
        {
            _stopwatch.Restart();
            _hardware.Apply(texture, hotspot);
            _stopwatch.Stop();
            var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
            Count++;
            _totalMs += elapsed;
            if (elapsed > MaxMs)
            {
                MaxMs = elapsed;
            }
        }

        public void Clear()
        {
            _hardware.Clear();
        }

        public void ResetStats()
        {
            Count = 0;
            _totalMs = 0d;
            MaxMs = 0d;
        }
    }
}
