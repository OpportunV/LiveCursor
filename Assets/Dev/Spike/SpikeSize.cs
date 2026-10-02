using System;
using UnityEngine;

namespace Dev.Spike
{
    [Serializable]
    public sealed class SpikeSize
    {
        [SerializeField] private int _size;
        [SerializeField] private Vector2 _hotspot;
        [SerializeField] private Texture2D _grid;
        [SerializeField] private SpikeSequence[] _sequences;

        public int Size => _size;

        public Vector2 Hotspot => _hotspot;

        public Texture2D Grid => _grid;

        public SpikeSequence[] Sequences => _sequences;

        public SpikeSize(int size, Vector2 hotspot, Texture2D grid, SpikeSequence[] sequences)
        {
            _size = size;
            _hotspot = hotspot;
            _grid = grid;
            _sequences = sequences;
        }
    }
}