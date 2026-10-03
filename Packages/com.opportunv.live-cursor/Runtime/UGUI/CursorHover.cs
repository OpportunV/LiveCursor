using UnityEngine;
using UnityEngine.EventSystems;

namespace Opportunv.LiveCursor
{
    [AddComponentMenu("Live Cursor/Cursor Hover")]
    public sealed class CursorHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private CursorAnimator _animator;
        [SerializeField, CursorStateName] private string _state;
        [SerializeField, CursorStateName(true)] private string _pressedState;
        [SerializeField] private int _priority;

        public CursorAnimator Animator
        {
            get => _animator;
            set
            {
                ReleaseAll();
                _animator = value;
            }
        }

        public bool IsHovered => _hover.IsActive;

        public bool IsPressed => _press.IsActive;

        private CursorStateId _stateId;
        private CursorStateId _pressedStateId;
        private CursorRequest _hover;
        private CursorRequest _press;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_hover.IsActive && _stateId.IsValid && ResolveAnimator())
            {
                _hover = _animator.Request(_stateId, _priority);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ReleaseAll();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !_press.IsActive &&
                _pressedStateId.IsValid && ResolveAnimator())
            {
                _press = _animator.Request(_pressedStateId, _priority + 1, true);
            }
        }

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
