using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    /// <summary>Plays a <see cref="CursorSet"/> as the hardware cursor. Add one to the scene, or register it with your
    /// dependency injection container, and change states through it.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Live Cursor/Cursor Animator")]
    public sealed class CursorAnimator : MonoBehaviour
    {
        [SerializeField, Tooltip("The cursor set to play.")]
        private CursorSet _cursorSet;
        [SerializeField, CursorStateName, Tooltip("The state shown when the scene starts.")]
        private string _initialState = "Default";
        [SerializeField, Tooltip("Play each state's loop. When off, states show their first frame.")]
        private bool _idleEnabled = true;
        [SerializeField, Tooltip("Show every frame once when a set is assigned, so later changes are instant.")]
        private bool _warmOnSetChange;
        [SerializeField, Min(0)]
        [Tooltip("Cursor size in pixels to use instead of the system size. 0 uses the system size.")]
        private int _sizeOverride;

        /// <summary>Raised when a state is entered, after any transition into it has finished.</summary>
        public event Action<CursorStateId> StateEntered
        {
            add => _player.StateEntered += value;
            remove => _player.StateEntered -= value;
        }

        /// <summary>The player driving the cursor.</summary>
        public CursorPlayer Player => _player;

        /// <summary>The cursor set being played.</summary>
        public CursorSet CursorSet => _cursorSet;

        /// <summary>The state currently shown. During a transition this is the state it started from.</summary>
        public CursorStateId CurrentState => _player.CurrentState;

        /// <summary>The state the cursor is heading to.</summary>
        public CursorStateId TargetState => _player.TargetState;

        /// <summary>Whether state loops play. When off, each state shows its first frame; transitions still
        /// play.</summary>
        public bool IdleEnabled
        {
            get => _idleEnabled;
            set
            {
                _idleEnabled = value;
                _player.IdleEnabled = value;
            }
        }

        /// <summary>Switches to <paramref name="set"/>, keeping the requested state when the set has it.</summary>
        public void SetCursorSet(CursorSet set)
        {
            _cursorSet = set;
            if (isActiveAndEnabled)
            {
                ApplySet();
            }
        }

        /// <summary>Sets the base state, shown while no request is active. Pass <paramref name="immediate"/> for
        /// click-driven changes: the first frame of the change shows during this call.</summary>
        public void SetState(CursorStateId state, bool immediate = false)
        {
            _player.SetState(state, immediate);
        }

        /// <summary>Sets the base state by name. Prefer the <see cref="CursorStateId"/> overload with generated
        /// constants.</summary>
        public void SetState(string state, bool immediate = false)
        {
            _player.SetState(state, immediate);
        }

        /// <summary>Shows <paramref name="state"/> on top of the base state until the returned handle is released. The
        /// highest priority wins and ties go to the newest request; releasing falls back to the next request or the
        /// base state.</summary>
        public CursorRequest Request(CursorStateId state, int priority = 0, bool immediate = false)
        {
            return _player.Request(state, priority, immediate);
        }

        /// <summary>Requests a state by name. Prefer the <see cref="CursorStateId"/> overload with generated
        /// constants.</summary>
        public CursorRequest Request(string state, int priority = 0, bool immediate = false)
        {
            return _player.Request(state, priority, immediate);
        }

        /// <summary>Releases every active request and returns to the base state.</summary>
        public void ReleaseAllRequests(bool immediate = false)
        {
            _player.ReleaseAllRequests(immediate);
        }

        /// <summary>Holds state loops on their first frame until <see cref="ReleaseIdle"/> is called with the same
        /// <paramref name="token"/>.</summary>
        public void SuppressIdle(object token)
        {
            _player.SuppressIdle(token);
        }

        /// <summary>Releases a token passed to <see cref="SuppressIdle"/>.</summary>
        public void ReleaseIdle(object token)
        {
            _player.ReleaseIdle(token);
        }

        /// <summary>Shows every frame once so later changes skip the OS's first-use cost. Call it during a loading
        /// screen.</summary>
        public void Warm()
        {
            _player.Warm();
        }

        private const float SizeCheckInterval = 1f;

        private readonly CursorPlayer _player = new(new HardwareCursorOutput());
        private float _sizeCheckTimer;

        private void Awake()
        {
            _player.IdleEnabled = _idleEnabled;
            _player.SetSystemCursorSize(ResolveCursorSize());
            if (!string.IsNullOrEmpty(_initialState))
            {
                _player.SetState(_initialState);
            }
        }

        private void OnEnable()
        {
            ApplySet();
        }

        private void OnDisable()
        {
            _player.Clear();
        }

        private void Update()
        {
            var deltaTime = Time.unscaledDeltaTime;
            _player.Tick(deltaTime);

            _sizeCheckTimer += deltaTime;
            if (_sizeCheckTimer >= SizeCheckInterval)
            {
                _sizeCheckTimer = 0f;
                _player.SetSystemCursorSize(ResolveCursorSize());
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && isActiveAndEnabled)
            {
                _player.Refresh();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                _player.IdleEnabled = _idleEnabled;
            }
        }
#endif

        private void ApplySet()
        {
            _player.SetSet(_cursorSet);
            if (_warmOnSetChange)
            {
                _player.Warm();
            }
        }

        private int ResolveCursorSize()
        {
            return _sizeOverride > 0 ? _sizeOverride : SystemCursorSize.Get();
        }
    }
}