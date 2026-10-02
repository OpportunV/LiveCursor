using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    [Serializable]
    public sealed class CursorState
    {
        [SerializeField] private string _name;
        [SerializeField] private CursorClip _loop;
        [SerializeField, Min(0f)] private float _loopDelay;

        public string Name => _name;

        public CursorClip Loop => _loop;

        public float LoopDelay => _loopDelay;

        internal CursorState(string name, CursorClip loop, float loopDelay)
        {
            _name = name;
            _loop = loop;
            _loopDelay = loopDelay;
        }
    }
}
