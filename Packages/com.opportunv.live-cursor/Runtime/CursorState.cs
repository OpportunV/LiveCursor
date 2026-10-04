using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    /// <summary>A named cursor state of a <see cref="CursorSet"/> and the loop it plays while active.</summary>
    [Serializable]
    public sealed class CursorState
    {
        [SerializeField] private string _name;
        [SerializeField] private CursorClip _loop;
        [SerializeField]
        [Min(0f)]
        private float _loopDelay;

        /// <summary>Gets the state name, as used by <see cref="CursorStateId"/>.</summary>
        public string Name => _name;

        /// <summary>Gets the frames played while the state is active.</summary>
        public CursorClip Loop => _loop;

        /// <summary>Gets the time, in seconds, the first frame is held after entering the state before the loop
        /// starts.</summary>
        public float LoopDelay => _loopDelay;

        internal CursorState(string name, CursorClip loop, float loopDelay)
        {
            _name = name;
            _loop = loop;
            _loopDelay = loopDelay;
        }
    }
}
