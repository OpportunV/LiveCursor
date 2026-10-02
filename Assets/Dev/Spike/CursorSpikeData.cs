using UnityEngine;

namespace Dev.Spike
{
    public sealed class CursorSpikeData : ScriptableObject
    {
        [SerializeField] private SpikeSize[] _sizes;

        public SpikeSize[] Sizes => _sizes;

        public void Initialize(SpikeSize[] sizes)
        {
            _sizes = sizes;
        }
    }
}
