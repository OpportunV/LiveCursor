using System;
using UnityEngine.UIElements;

namespace Opportunv.LiveCursor
{
    public sealed class CursorHoverManipulator : Manipulator
    {
        public CursorStateId State { get; }

        public CursorStateId PressedState { get; }

        public int Priority { get; }

        public bool IsHovered => _hover.IsActive;

        public bool IsPressed => _press.IsActive;

        private readonly CursorPlayer _player;
        private CursorRequest _hover;
        private CursorRequest _press;

        public CursorHoverManipulator(CursorPlayer player, CursorStateId state, CursorStateId pressedState = default,
            int priority = 0)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            State = state;
            PressedState = pressedState;
            Priority = priority;
        }

        public CursorHoverManipulator(CursorAnimator animator, CursorStateId state,
            CursorStateId pressedState = default, int priority = 0)
            : this(animator ? animator.Player : null, state, pressedState, priority)
        {
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            ReleaseAll();
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (!_hover.IsActive && State.IsValid)
            {
                _hover = _player.Request(State, Priority);
            }
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            ReleaseAll();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 && !_press.IsActive && PressedState.IsValid)
            {
                _press = _player.Request(PressedState, Priority + 1, true);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.button == 0)
            {
                _press.Release(true);
            }
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            ReleaseAll();
        }

        private void ReleaseAll()
        {
            _press.Release();
            _hover.Release();
        }
    }
}
