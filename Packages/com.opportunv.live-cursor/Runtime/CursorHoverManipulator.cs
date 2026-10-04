using System;
using UnityEngine.UIElements;

namespace Opportunv.LiveCursor
{
    /// <summary>Requests a cursor state while the pointer is over a UI Toolkit element, and an optional pressed state,
    /// one priority higher, while the left button is held. Add it with <c>element.AddManipulator(...)</c>.</summary>
    public sealed class CursorHoverManipulator : Manipulator
    {
        /// <summary>Gets the state requested while hovered.</summary>
        public CursorStateId State { get; }

        /// <summary>Gets the state requested while pressed, or the default id for none.</summary>
        public CursorStateId PressedState { get; }

        /// <summary>Gets the priority of the hover request.</summary>
        public int Priority { get; }

        /// <summary>Gets a value indicating whether the hover request is active.</summary>
        public bool IsHovered => _hover.IsActive;

        /// <summary>Gets a value indicating whether the pressed request is active.</summary>
        public bool IsPressed => _press.IsActive;

        private readonly CursorPlayer _player;
        private CursorRequest _hover;
        private CursorRequest _press;

        /// <summary>Initializes a new instance of the <see cref="CursorHoverManipulator"/> class that makes requests on
        /// <paramref name="player"/>.</summary>
        public CursorHoverManipulator(
            CursorPlayer player,
            CursorStateId state,
            CursorStateId pressedState = default,
            int priority = 0)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            State = state;
            PressedState = pressedState;
            Priority = priority;
        }

        /// <summary>Initializes a new instance of the <see cref="CursorHoverManipulator"/> class that makes requests on
        /// <paramref name="animator"/>.</summary>
        public CursorHoverManipulator(
            CursorAnimator animator,
            CursorStateId state,
            CursorStateId pressedState = default,
            int priority = 0)
            : this(animator ? animator.Player : null, state, pressedState, priority)
        {
        }

        /// <inheritdoc/>
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <inheritdoc/>
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
