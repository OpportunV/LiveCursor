using UnityEngine;
using UnityEngine.EventSystems;

namespace Opportunv.LiveCursor.UGUI
{
    /// <summary>Requests a cursor state while the pointer is over this uGUI element or collider, and an optional
    /// pressed state, one priority higher, while the left button is held. Colliders need a physics raycaster on the
    /// camera.</summary>
    [AddComponentMenu("Live Cursor/Cursor Hover")]
    public sealed class CursorHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField]
        [Tooltip("The animator to use. When empty, the first one in the scene is used.")]
        private CursorAnimator _animator;
        [SerializeField]
        [CursorStateName]
        [Tooltip("The state shown while the pointer is over this object.")]
        private string _state;
        [SerializeField]
        [CursorStateName(true)]
        [Tooltip("The state shown while the left button is held. Optional.")]
        private string _pressedState;
        [SerializeField]
        [Tooltip("Requests with a higher priority win over this one.")]
        private int _priority;

        /// <summary>Gets or sets the animator to make requests on. When empty, the first one found in the scene is
        /// used.</summary>
        public CursorAnimator Animator
        {
            get => _animator;
            set
            {
                ReleaseAll();
                _animator = value;
            }
        }

        /// <summary>Gets a value indicating whether the hover request is active.</summary>
        public bool IsHovered => _hover.IsActive;

        /// <summary>Gets a value indicating whether the pressed request is active.</summary>
        public bool IsPressed => _press.IsActive;

        private CursorStateId _stateId;
        private CursorStateId _pressedStateId;
        private CursorRequest _hover;
        private CursorRequest _press;

        /// <summary>Sets the animator, states and priority from code.</summary>
        public void Configure(
            CursorAnimator animator,
            CursorStateId state,
            CursorStateId pressedState = default,
            int priority = 0)
        {
            ReleaseAll();
            _animator = animator;
            _state = state.IsValid ? state.Name : string.Empty;
            _pressedState = pressedState.IsValid ? pressedState.Name : string.Empty;
            _priority = priority;
            CacheIds();
        }

        /// <summary>Requests the hover state.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_hover.IsActive && _stateId.IsValid && ResolveAnimator())
            {
                _hover = _animator.Request(_stateId, _priority);
            }
        }

        /// <summary>Releases the hover and pressed states.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            ReleaseAll();
        }

        /// <summary>Requests the pressed state.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !_press.IsActive &&
                _pressedStateId.IsValid && ResolveAnimator())
            {
                _press = _animator.Request(_pressedStateId, _priority + 1, true);
            }
        }

        /// <summary>Releases the pressed state.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _press.Release(true);
            }
        }

        private void Awake()
        {
            CacheIds();
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheIds();
        }
#endif

        private void CacheIds()
        {
            _stateId = string.IsNullOrEmpty(_state) ? default : new CursorStateId(_state);
            _pressedStateId = string.IsNullOrEmpty(_pressedState) ? default : new CursorStateId(_pressedState);
        }

        private bool ResolveAnimator()
        {
            if (!_animator)
            {
                _animator = FindAnyObjectByType<CursorAnimator>();
            }

            return _animator;
        }

        private void ReleaseAll()
        {
            _press.Release();
            _hover.Release();
        }
    }
}
