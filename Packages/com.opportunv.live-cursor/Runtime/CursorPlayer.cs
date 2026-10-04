using System;
using System.Collections.Generic;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    /// <summary>Plays a <see cref="CursorSet"/>: shows the requested state's loop, runs transitions between states and
    /// sends every frame to an <see cref="ICursorOutput"/>. Call <see cref="Tick"/> once per frame.</summary>
    public sealed class CursorPlayer
    {
        /// <summary>Raised when a state is entered, after any transition into it has finished.</summary>
        public event Action<CursorStateId> StateEntered;

        /// <summary>Gets the cursor set being played, or <c>null</c>.</summary>
        public CursorSet Set { get; private set; }

        /// <summary>Gets the state currently shown. During a transition this is the state it started
        /// from.</summary>
        public CursorStateId CurrentState => _stateIndex >= 0 ? Set.GetStateId(_stateIndex) : default;

        /// <summary>Gets the state the cursor is heading to: the current state, or where the running transition
        /// will end.</summary>
        public CursorStateId TargetState => _stateIndex >= 0 ? Set.GetStateId(TargetStateIndex()) : _requestedState;

        /// <summary>Gets the state set with <see cref="SetState(CursorStateId, bool)"/>, shown while no request is
        /// active.</summary>
        public CursorStateId BaseState => _baseState;

        /// <summary>Gets the number of active requests.</summary>
        public int ActiveRequestCount => _requests.Count;

        /// <summary>Gets a value indicating whether a transition is playing.</summary>
        public bool IsTransitioning => _transitioning;

        /// <summary>Gets the index of the frame shown in the current loop or transition.</summary>
        public int FrameIndex => _frameIndex;

        /// <summary>Gets the baked size in use, in pixels, or 0 when no set is playing.</summary>
        public int CursorSize => _sizeIndex >= 0 ? Set.GetSize(_sizeIndex) : 0;

        /// <summary>Gets a value indicating whether any token passed to <see cref="SuppressIdle"/> is still holding the
        /// loops.</summary>
        public bool IsIdleSuppressed => _idleSuppressors.Count > 0;

        /// <summary>Gets a value indicating whether state loops are currently playing.</summary>
        public bool IsIdlePlaying => _idleEnabled && _idleSuppressors.Count == 0;

        /// <summary>Gets or sets a value indicating whether state loops play. When off, each state shows its first
        /// frame; transitions still play.</summary>
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

        /// <summary>Initializes a new instance of the <see cref="CursorPlayer"/> class that sends frames to
        /// <paramref name="output"/>.</summary>
        public CursorPlayer(ICursorOutput output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>Plays <paramref name="set"/>, keeping the requested state when the set has it and otherwise
        /// starting from its first state. Pass <c>null</c> to stop and restore the default cursor.</summary>
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

        /// <summary>Picks the smallest baked size that covers <paramref name="size"/> pixels, or the largest
        /// one.</summary>
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

        /// <summary>Sets the base state by name. Prefer the <see cref="CursorStateId"/> overload with generated
        /// constants.</summary>
        public void SetState(string state, bool immediate = false)
        {
            SetState(new CursorStateId(state), immediate);
        }

        /// <summary>Sets the base state, shown while no request is active. Pass <paramref name="immediate"/> for
        /// click-driven changes: the first frame of the change shows during this call.</summary>
        public void SetState(CursorStateId state, bool immediate = false)
        {
            _baseState = state;
            if (_requests.Count == 0)
            {
                Drive(state, immediate);
            }
        }

        /// <summary>Shows <paramref name="state"/> on top of the base state until the returned handle is released. The
        /// highest priority wins and ties go to the newest request; releasing falls back to the next request or the
        /// base state.</summary>
        public CursorRequest Request(CursorStateId state, int priority = 0, bool immediate = false)
        {
            var id = ++_lastRequestId;
            _requests.Add(new(id, state, priority));
            DriveEffective(immediate);
            return new(this, id);
        }

        /// <summary>Requests a state by name. Prefer the <see cref="CursorStateId"/> overload with generated
        /// constants.</summary>
        public CursorRequest Request(string state, int priority = 0, bool immediate = false)
        {
            return Request(new CursorStateId(state), priority, immediate);
        }

        /// <summary>Releases every active request and returns to the base state.</summary>
        public void ReleaseAllRequests(bool immediate = false)
        {
            if (_requests.Count == 0)
            {
                return;
            }

            _requests.Clear();
            DriveEffective(immediate);
        }

        /// <summary>Advances playback by <paramref name="deltaTime"/> seconds.</summary>
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

        /// <summary>Holds state loops on their first frame until <see cref="ReleaseIdle"/> is called with the same
        /// <paramref name="token"/>. Several tokens can hold the loops at once.</summary>
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

        /// <summary>Releases a token passed to <see cref="SuppressIdle"/>.</summary>
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

        /// <summary>Shows every frame of the current size once, so later changes skip the OS's first-use cost. Call it
        /// during a loading screen; it can take a few hundred milliseconds.</summary>
        public void Warm()
        {
            if (_sizeIndex < 0)
            {
                return;
            }

            for (var i = 0; i < Set.StateCount; i++)
            {
                WarmClip(Set.GetState(i).Loop);
            }

            for (var i = 0; i < Set.TransitionCount; i++)
            {
                WarmClip(Set.GetTransition(i).Clip);
            }

            Refresh();
        }

        /// <summary>Sends the current frame to the output again, for example after the application regains
        /// focus.</summary>
        public void Refresh()
        {
            _appliedTexture = null;
            Apply();
        }

        /// <summary>Restores the default cursor without changing the playback state.</summary>
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
            if (state.IsValid && state != _requestedState)
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
            else if (TrySwitchTransition(target))
            {
                _queuedState = -1;
            }
            else
            {
                _queuedState = target;
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
            if (!TryGetPlayableRange(transition, out var first, out var last))
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

        private bool TrySwitchTransition(int target)
        {
            var index = Set.FindTransition(_transitionOrigin, target, out var reversed);
            if (index < 0)
            {
                return false;
            }

            var transition = Set.GetTransition(index);
            if (!TryGetPlayableRange(transition, out var first, out var last))
            {
                return false;
            }

            var offset = Mathf.RoundToInt(TransitionProgress() * (last - first));
            _transition = transition;
            _transitionDestination = target;
            _firstFrame = first;
            _lastFrame = last;
            _step = reversed ? -1 : 1;
            _frameIndex = reversed ? last - offset : first + offset;
            _frameDuration = StepFrameDuration();
            return true;
        }

        private float TransitionProgress()
        {
            var span = _lastFrame - _firstFrame;
            if (span <= 0)
            {
                return 0f;
            }

            var played = _step > 0 ? _frameIndex - _firstFrame : _lastFrame - _frameIndex;
            return Mathf.Clamp01(played / (float)span);
        }

        private static bool TryGetPlayableRange(CursorTransition transition, out int first, out int last)
        {
            var count = transition.Clip.FrameCount;
            first = transition.IncludesEndpoints ? 1 : 0;
            last = transition.IncludesEndpoints ? count - 2 : count - 1;
            return last >= first;
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

        private CursorFrame CurrentFrame()
        {
            if (_stateIndex < 0 || _sizeIndex < 0)
            {
                return null;
            }

            var clip = _transitioning ? _transition.Clip : Set.GetState(_stateIndex).Loop;
            return _frameIndex < clip.FrameCount ? clip.GetFrame(_frameIndex) : null;
        }

        private void Apply()
        {
            var frame = CurrentFrame();
            var texture = frame?.GetTexture(_sizeIndex);
            if (!texture)
            {
                return;
            }

            var hotspot = frame.GetHotspot(_sizeIndex);
            if (ReferenceEquals(texture, _appliedTexture) && hotspot == _appliedHotspot)
            {
                return;
            }

            _appliedTexture = texture;
            _appliedHotspot = hotspot;
            _output.Apply(texture, hotspot);
        }

        private void WarmClip(CursorClip clip)
        {
            for (var i = 0; i < clip.FrameCount; i++)
            {
                var frame = clip.GetFrame(i);
                var texture = frame.GetTexture(_sizeIndex);
                if (texture)
                {
                    _output.Apply(texture, frame.GetHotspot(_sizeIndex));
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