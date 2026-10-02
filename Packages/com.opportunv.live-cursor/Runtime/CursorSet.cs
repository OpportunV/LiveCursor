using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    public sealed class CursorSet : ScriptableObject
    {
        [SerializeField] private int[] _sizes = { 32 };
        [SerializeField] private Vector2[] _hotspots = { Vector2.zero };
        [SerializeField] private CursorState[] _states = Array.Empty<CursorState>();
        [SerializeField] private CursorTransition[] _transitions = Array.Empty<CursorTransition>();

        public int SizeCount => _sizes.Length;

        public int StateCount => _states.Length;

        public int TransitionCount => _transitions.Length;

        private CursorStateId[] _stateIds;
        private int[] _transitionFrom;
        private int[] _transitionTo;

        public int GetSize(int index)
        {
            return _sizes[index];
        }

        public Vector2 GetHotspot(int sizeIndex)
        {
            return _hotspots[sizeIndex];
        }

        public CursorState GetState(int index)
        {
            return _states[index];
        }

        public CursorStateId GetStateId(int index)
        {
            EnsureLookup();
            return _stateIds[index];
        }

        public CursorTransition GetTransition(int index)
        {
            return _transitions[index];
        }

        public int FindState(CursorStateId id)
        {
            EnsureLookup();
            return IndexOf(id);
        }

        public int FindTransition(int from, int to, out bool reversed)
        {
            EnsureLookup();
            for (var i = 0; i < _transitions.Length; i++)
            {
                if (_transitionFrom[i] == from && _transitionTo[i] == to)
                {
                    reversed = false;
                    return i;
                }
            }

            for (var i = 0; i < _transitions.Length; i++)
            {
                if (_transitionFrom[i] == to && _transitionTo[i] == from && _transitions[i].Reversible)
                {
                    reversed = true;
                    return i;
                }
            }

            reversed = false;
            return -1;
        }

        public int FindSizeIndex(int systemCursorSize)
        {
            if (_sizes.Length == 0)
            {
                return -1;
            }

            var best = -1;
            var largest = 0;
            for (var i = 0; i < _sizes.Length; i++)
            {
                var size = _sizes[i];
                if (size > _sizes[largest])
                {
                    largest = i;
                }

                if (systemCursorSize > 0 && size >= systemCursorSize && (best < 0 || size < _sizes[best]))
                {
                    best = i;
                }
            }

            return best >= 0 ? best : largest;
        }

        internal void Initialize(int[] sizes, Vector2[] hotspots, CursorState[] states, CursorTransition[] transitions)
        {
            _sizes = sizes;
            _hotspots = hotspots;
            _states = states;
            _transitions = transitions;
            InvalidateLookup();
        }

        private void OnValidate()
        {
            InvalidateLookup();
        }

        private void InvalidateLookup()
        {
            _stateIds = null;
            _transitionFrom = null;
            _transitionTo = null;
        }

        private void EnsureLookup()
        {
            if (_stateIds != null)
            {
                return;
            }

            _stateIds = new CursorStateId[_states.Length];
            for (var i = 0; i < _states.Length; i++)
            {
                _stateIds[i] = new(_states[i].Name);
            }

            _transitionFrom = new int[_transitions.Length];
            _transitionTo = new int[_transitions.Length];
            for (var i = 0; i < _transitions.Length; i++)
            {
                _transitionFrom[i] = IndexOf(new(_transitions[i].From));
                _transitionTo[i] = IndexOf(new(_transitions[i].To));
            }
        }

        private int IndexOf(CursorStateId id)
        {
            for (var i = 0; i < _stateIds.Length; i++)
            {
                if (_stateIds[i] == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}