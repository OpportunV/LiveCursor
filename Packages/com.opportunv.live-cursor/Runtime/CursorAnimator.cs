using System;
using UnityEngine;

namespace Opportunv.LiveCursor
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Live Cursor/Cursor Animator")]
    public sealed class CursorAnimator : MonoBehaviour
    {
        [SerializeField] private CursorSet _cursorSet;
        [SerializeField] private string _initialState = "Default";
        [SerializeField] private bool _idleEnabled = true;
        [SerializeField] private bool _warmOnSetChange;
        [SerializeField, Min(0)] private int _sizeOverride;

        public event Action<CursorStateId> StateEntered
        {
            add => _player.StateEntered += value;
            remove => _player.StateEntered -= value;
        }

        public CursorPlayer Player => _player;

        public CursorSet CursorSet => _cursorSet;

        public CursorStateId CurrentState => _player.CurrentState;

        public CursorStateId TargetState => _player.TargetState;

        public bool IdleEnabled
        {
            get => _idleEnabled;
            set
            {
                _idleEnabled = value;
                _player.IdleEnabled = value;
            }
        }

        public void SetCursorSet(CursorSet set)
        {
            _cursorSet = set;
            if (isActiveAndEnabled)
            {
                ApplySet();
            }
        }

        public void SetState(CursorStateId state, bool immediate = false)
        {
            _player.SetState(state, immediate);
        }

        public void SetState(string state, bool immediate = false)
        {
            _player.SetState(state, immediate);
        }

        public void SuppressIdle(object token)
        {
            _player.SuppressIdle(token);
        }

        public void ReleaseIdle(object token)
        {
            _player.ReleaseIdle(token);
        }

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