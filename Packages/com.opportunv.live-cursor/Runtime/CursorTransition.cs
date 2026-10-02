using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    [Serializable]
    public sealed class CursorTransition
    {
        [SerializeField] private string _from;
        [SerializeField] private string _to;
        [SerializeField] private CursorClip _clip;
        [SerializeField] private bool _includesEndpoints;
        [SerializeField] private bool _reversible;
        [SerializeField, Min(0f)] private float _reverseFrameDuration;

        public string From => _from;

        public string To => _to;

        public CursorClip Clip => _clip;

        public bool IncludesEndpoints => _includesEndpoints;

        public bool Reversible => _reversible;

        public float ReverseFrameDuration => _reverseFrameDuration > 0f ? _reverseFrameDuration : _clip.FrameDuration;

        internal CursorTransition(string from, string to, CursorClip clip, bool includesEndpoints, bool reversible,
            float reverseFrameDuration)
        {
            _from = from;
            _to = to;
            _clip = clip;
            _includesEndpoints = includesEndpoints;
            _reversible = reversible;
            _reverseFrameDuration = reverseFrameDuration;
        }
    }
}
