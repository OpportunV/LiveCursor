using System;
using System.Collections.Generic;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    public sealed class CursorPlayer
    {
        public event Action<CursorStateId> StateEntered;

        public CursorSet Set { get; private set; }

        public CursorStateId CurrentState => _stateIndex >= 0 ? Set.GetStateId(_stateIndex) : default;

        public CursorStateId TargetState => _stateIndex >= 0 ? Set.GetStateId(TargetStateIndex()) : _requestedState;

        public CursorStateId RequestedState => _requestedState;

        public CursorStateId BaseState => _baseState;

        public int ActiveRequestCount => _requests.Count;

        public bool IsTransitioning => _transitioning;

        public int FrameIndex => _frameIndex;

        public int CursorSize => _sizeIndex >= 0 ? Set.GetSize(_sizeIndex) : 0;

        public bool IsIdleSuppressed => _idleSuppressors.Count > 0;

        public bool IsIdlePlaying => _idleEnabled && _idleSuppressors.Count == 0;

        public bool IdleEnabled
        {
            get => _idleEnabled;
            set
            {
                if (_idleEnabled == value)
                {
                    return;
                }

                _idleEnabled = value;
                RestartLoop();
            }
        }

        private const int MaxStepsPerTick = 4096;
        private const float MinFrameDuration = 0.001f;

        private readonly ICursorOutput _output;
        private readonly List<object> _idleSuppressors = new(4);
        private readonly List<CursorRequestEntry> _requests = new(8);
        private int _lastRequestId;
        private CursorStateId _baseState;
        private int _sizeIndex = -1;
        private int _systemCursorSize;
        private CursorStateId _requestedState;
        private int _stateIndex = -1;
        private bool _transitioning;
        private CursorTransition _transition;
        private int _transitionOrigin = -1;
        private int _transitionDestination = -1;
        private int _firstFrame;
        private int _lastFrame;
        private int _step;
        private float _frameDuration;
        private int _queuedState = -1;
        private int _frameIndex;
        private float _elapsed;
        private bool _waitingForLoop;
        private bool _idleEnabled = true;
        private bool _stateEnteredPending;
        private Texture2D _appliedTexture;
        private Vector2 _appliedHotspot;

        public CursorPlayer(ICursorOutput output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public void SetSet(CursorSet set)
        {
            Set = set;
            _transitioning = false;
            _transition = null;
            _queuedState = -1;
            _stateIndex = -1;
            _sizeIndex = -1;
            _appliedTexture = null;

            if (!set || set.StateCount == 0 || set.SizeCount == 0)
            {
                _output.Clear();
                return;
            }

            _sizeIndex = set.FindSizeIndex(_systemCursorSize);
            if (!_baseState.IsValid)
            {
                _baseState = set.GetStateId(0);
            }

            var index = set.FindState(_requestedState);
            EnterState(index >= 0 ? index : 0, false);
            Apply();
            RaisePendingEvent();
        }

        public void SetSystemCursorSize(int size)
        {
            _systemCursorSize = size;
            if (_sizeIndex < 0)
            {
                return;
            }

            var index = Set.FindSizeIndex(size);
            if (index == _sizeIndex)
            {
                return;
            }

            _sizeIndex = index;
            Apply();
        }

        public void SetState(string state, bool immediate = false)
        {
            SetState(new CursorStateId(state), immediate);
        }

        public void SetState(CursorStateId state, bool immediate = false)
        {
            _baseState = state;
            if (_requests.Count == 0)
            {
                Drive(state, immediate);
            }
        }

        public CursorRequest Request(CursorStateId state, int priority = 0, bool immediate = false)
        {
            var id = ++_lastRequestId;
            _requests.Add(new(id, state, priority));
            DriveEffective(immediate);
            return new(this, id);
        }

        public CursorRequest Request(string state, int priority = 0, bool immediate = false)
        {
            return Request(new CursorStateId(state), priority, immediate);
        }

        public void ReleaseAllRequests(bool immediate = false)
        {
            if (_requests.Count == 0)
            {
                return;
            }

            _requests.Clear();
            DriveEffective(immediate);
        }

        public void Tick(float deltaTime)
        {
            if (_stateIndex < 0 || deltaTime <= 0f)
            {
                return;
            }

            _elapsed += deltaTime;
            var steps = 0;
            while (Advance())
            {
                if (++steps >= MaxStepsPerTick)
                {
                    _elapsed = 0f;
                    break;
                }
            }

            Apply();
            RaisePendingEvent();
        }

        public void SuppressIdle(object token)
        {
            if (token == null || _idleSuppressors.Contains(token))
            {
                return;
            }

            _idleSuppressors.Add(token);
            if (_idleSuppressors.Count == 1)
            {
                RestartLoop();
            }
        }

        public void ReleaseIdle(object token)
        {
            if (!_idleSuppressors.Remove(token))
            {
                return;
            }

            if (_idleSuppressors.Count == 0)
            {
                RestartLoop();
            }
        }

        public void Warm()
        {
            if (_sizeIndex < 0)
            {
                return;
            }

            var hotspot = Set.GetHotspot(_sizeIndex);
            for (var i = 0; i < Set.StateCount; i++)
            {
                WarmClip(Set.GetState(i).Loop, hotspot);
            }

            for (var i = 0; i < Set.TransitionCount; i++)
            {
                WarmClip(Set.GetTransition(i).Clip, hotspot);
            }

            Refresh();
        }

        public void Refresh()
        {
            _appliedTexture = null;
            Apply();
        }

        public void Clear()
        {
            _appliedTexture = null;
            _output.Clear();
        }

        internal bool IsRequestActive(int id)
        {
            return FindRequest(id) >= 0;
        }

        internal void Release(int id, bool immediate)
        {
            var index = FindRequest(id);
            if (index < 0)
            {
                return;
            }

            _requests.RemoveAt(index);
            DriveEffective(immediate);
        }

        private int FindRequest(int id)
        {
            for (var i = 0; i < _requests.Count; i++)
            {
                if (_requests[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private CursorStateId EffectiveState()
        {
            if (_requests.Count == 0)
            {
                return _baseState;
            }

            var winner = _requests[0];
            for (var i = 1; i < _requests.Count; i++)
            {
                if (_requests[i].Priority >= winner.Priority)
                {
                    winner = _requests[i];
                }
            }

            return winner.State;
        }

        private void DriveEffective(bool immediate)
        {
            var state = EffectiveState();
            if (state.IsValid && (state != _requestedState || immediate))
            {
                Drive(state, immediate);
            }
        }

        private void Drive(CursorStateId state, bool immediate)
        {
            _requestedState = state;
            if (_stateIndex < 0)
            {
                return;
            }

            var target = Set.FindState(state);
            if (target < 0)
            {
                Debug.LogWarning($"[Live Cursor] Cursor set '{Set.name}' has no state '{state.Name}'.");
                return;
            }

            if (!_transitioning)
            {
                if (target != _stateIndex)
                {
                    StartTransition(_stateIndex, target, false);
                }
            }
            else if (target == _transitionDestination)
            {
                _queuedState = -1;
                if (immediate)
                {
                    _elapsed = 0f;
                    CompleteTransition();
                }
            }
            else if (target == _transitionOrigin && _transition.Reversible)
            {
                _queuedState = -1;
                Reverse();
                if (immediate)
                {
                    StepTransition();
                }
            }
            else
            {
                _queuedState = target;
                if (immediate)
                {
                    _elapsed = 0f;
                    CompleteTransition();
                }
            }

            Apply();
            RaisePendingEvent();
        }

        private int TargetStateIndex()
        {
            if (!_transitioning)
            {
                return _stateIndex;
            }

            return _queuedState >= 0 ? _queuedState : _transitionDestination;
        }

        private void StartTransition(int from, int to, bool keepElapsed)
        {
            var index = Set.FindTransition(from, to, out var reversed);
            if (index < 0)
            {
                EnterState(to, keepElapsed);
                return;
            }

            var transition = Set.GetTransition(index);
            var count = transition.Clip.FrameCount;
            var first = transition.IncludesEndpoints ? 1 : 0;
            var last = transition.IncludesEndpoints ? count - 2 : count - 1;
            if (last < first)
            {
                EnterState(to, keepElapsed);
                return;
            }

            _transition = transition;
            _transitioning = true;
            _transitionOrigin = from;
            _transitionDestination = to;
            _firstFrame = first;
            _lastFrame = last;
            _step = reversed ? -1 : 1;
            _frameIndex = reversed ? last : first;
            _frameDuration = StepFrameDuration();
            if (!keepElapsed)
            {
                _elapsed = 0f;
            }
        }

        private void Reverse()
        {
            (_transitionOrigin, _transitionDestination) = (_transitionDestination, _transitionOrigin);
            _step = -_step;
            _frameDuration = StepFrameDuration();
            _elapsed = 0f;
        }

        private float StepFrameDuration()
        {
            var duration = _step > 0 ? _transition.Clip.FrameDuration : _transition.ReverseFrameDuration;
            return Mathf.Max(duration, MinFrameDuration);
        }

        private void StepTransition()
        {
            _frameIndex += _step;
            if (_frameIndex < _firstFrame || _frameIndex > _lastFrame)
            {
                CompleteTransition();
            }
        }

        private void CompleteTransition()
        {
            var destination = _transitionDestination;
            _transitioning = false;
            _transition = null;
            EnterState(destination, true);

            if (_queuedState < 0)
            {
                return;
            }

            var queued = _queuedState;
            _queuedState = -1;
            if (queued != destination)
            {
                StartTransition(destination, queued, true);
            }
        }

        private void EnterState(int index, bool keepElapsed)
        {
            _stateIndex = index;
            _frameIndex = 0;
            _waitingForLoop = Set.GetState(index).LoopDelay > 0f;
            if (!keepElapsed)
            {
                _elapsed = 0f;
            }

            _stateEnteredPending = true;
        }

        private bool Advance()
        {
            if (_transitioning)
            {
                if (_elapsed < _frameDuration)
                {
                    return false;
                }

                _elapsed -= _frameDuration;
                StepTransition();
                return true;
            }

            var state = Set.GetState(_stateIndex);
            var loop = state.Loop;
            if (!IsIdlePlaying || loop.FrameCount <= 1)
            {
                _elapsed = 0f;
                return false;
            }

            if (_waitingForLoop)
            {
                if (_elapsed < state.LoopDelay)
                {
                    return false;
                }

                _elapsed -= state.LoopDelay;
                _waitingForLoop = false;
                return true;
            }

            var duration = Mathf.Max(loop.FrameDuration, MinFrameDuration);
            if (_elapsed < duration)
            {
                return false;
            }

            _elapsed -= duration;
            _frameIndex = (_frameIndex + 1) % loop.FrameCount;
            return true;
        }

        private void RestartLoop()
        {
            if (_stateIndex < 0 || _transitioning)
            {
                return;
            }

            _frameIndex = 0;
            _elapsed = 0f;
            _waitingForLoop = Set.GetState(_stateIndex).LoopDelay > 0f;
            Apply();
        }

        private Texture2D CurrentTexture()
        {
            if (_stateIndex < 0 || _sizeIndex < 0)
            {
                return null;
            }

            var clip = _transitioning ? _transition.Clip : Set.GetState(_stateIndex).Loop;
            if (_frameIndex >= clip.FrameCount)
            {
                return null;
            }

            return clip.GetFrame(_frameIndex).GetTexture(_sizeIndex);
        }

        private void Apply()
        {
            var texture = CurrentTexture();
            if (!texture)
            {
                return;
            }

            var hotspot = Set.GetHotspot(_sizeIndex);
            if (ReferenceEquals(texture, _appliedTexture) && hotspot == _appliedHotspot)
            {
                return;
            }

            _appliedTexture = texture;
            _appliedHotspot = hotspot;
            _output.Apply(texture, hotspot);
        }

        private void WarmClip(CursorClip clip, Vector2 hotspot)
        {
            for (var i = 0; i < clip.FrameCount; i++)
            {
                var texture = clip.GetFrame(i).GetTexture(_sizeIndex);
                if (texture != null)
                {
                    _output.Apply(texture, hotspot);
                }
            }
        }

        private void RaisePendingEvent()
        {
            if (!_stateEnteredPending)
            {
                return;
            }

            _stateEnteredPending = false;
            StateEntered?.Invoke(Set.GetStateId(_stateIndex));
        }
    }
}