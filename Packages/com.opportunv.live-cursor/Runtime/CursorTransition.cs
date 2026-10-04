using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    /// <summary>In-between frames played when the cursor changes from one state to another.</summary>
    [Serializable]
    public sealed class CursorTransition
    {
        [SerializeField] private string _from;
        [SerializeField] private string _to;
        [SerializeField] private CursorClip _clip;
        [SerializeField] private bool _includesEndpoints;
        [SerializeField] private bool _reversible;
        [SerializeField]
        [Min(0f)]
        private float _reverseFrameDuration;

        /// <summary>Gets the state the transition starts from.</summary>
        public string From => _from;

        /// <summary>Gets the state the transition ends in.</summary>
        public string To => _to;

        /// <summary>Gets the transition frames, in forward order.</summary>
        public CursorClip Clip => _clip;

        /// <summary>Gets a value indicating whether the first and last frames repeat the first frames of the two states
        /// and are skipped during playback.</summary>
        public bool IncludesEndpoints => _includesEndpoints;

        /// <summary>Gets a value indicating whether the transition also plays backwards for the opposite
        /// direction.</summary>
        public bool Reversible => _reversible;

        /// <summary>Gets the time, in seconds, each frame is shown when playing backwards.</summary>
        public float ReverseFrameDuration => _reverseFrameDuration > 0f ? _reverseFrameDuration : _clip.FrameDuration;

        internal CursorTransition(
            string from,
            string to,
            CursorClip clip,
            bool includesEndpoints,
            bool reversible,
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
