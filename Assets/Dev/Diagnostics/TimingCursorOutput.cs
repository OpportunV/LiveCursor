using System.Diagnostics;
using Opportunv.LiveCursor;
using UnityEngine;

namespace Dev.Diagnostics
{
    public sealed class TimingCursorOutput : ICursorOutput
    {
        public int Count { get; private set; }

        public double TotalMs { get; private set; }

        public double MaxMs { get; private set; }

        public double AverageMs => Count > 0 ? TotalMs / Count : 0d;

        private readonly HardwareCursorOutput _hardware = new();
        private readonly Stopwatch _stopwatch = new();

        public void Apply(Texture2D texture, Vector2 hotspot)
        {
            _stopwatch.Restart();
            _hardware.Apply(texture, hotspot);
            _stopwatch.Stop();
            var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
            Count++;
            TotalMs += elapsed;
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
            TotalMs = 0d;
            MaxMs = 0d;
        }
    }
}
