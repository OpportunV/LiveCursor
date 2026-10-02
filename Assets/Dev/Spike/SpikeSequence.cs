using System;
using UnityEngine;

namespace Dev.Spike
{
    [Serializable]
    public sealed class SpikeSequence
    {
        [SerializeField] private string _name;
        [SerializeField] private float _frameDuration;
        [SerializeField] private bool _pingPong;
        [SerializeField] private Texture2D[] _frames;

        public string Name => _name;

        public float FrameDuration => _frameDuration;

        public bool PingPong => _pingPong;

        public Texture2D[] Frames => _frames;

        public SpikeSequence(string name, float frameDuration, bool pingPong, Texture2D[] frames)
        {
            _name = name;
            _frameDuration = frameDuration;
            _pingPong = pingPong;
            _frames = frames;
        }
    }
}