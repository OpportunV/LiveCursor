using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Opportunv.LiveCursor.Samples
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class UIToolkitDemo : MonoBehaviour
    {
        [SerializeField] private CursorAnimator _cursor;
        [SerializeField] private CursorSet[] _skins;
        [SerializeField] private float _taskSeconds = 3f;
        [SerializeField] private int _busyPriority = 50;

        private const string SelectedClass = "chip--selected";

        private readonly List<Button> _skinButtons = new();
        private Label _status;
        private Button _busyButton;
        private Button _idleButton;
        private CursorRequest _busy;
        private float _taskRemaining;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            Hover(root.Q("pointer-card"), DemoCursorStates.Pointer);
            Hover(root.Q("text-card"), DemoCursorStates.Text);
            Hover(root.Q("grab-card"), DemoCursorStates.Grab, DemoCursorStates.Grabbing);
            Hover(root.Q("aim-card"), DemoCursorStates.Crosshair);
            Hover(root.Q("blocked-card"), DemoCursorStates.Blocked);

            var task = root.Q<Button>("task-button");
            task.clicked += StartTask;
            Hover(task, DemoCursorStates.Pointer);

            _busyButton = root.Q<Button>("busy-button");
            _busyButton.clicked += ToggleBusy;
            Hover(_busyButton, DemoCursorStates.Pointer);

            _idleButton = root.Q<Button>("idle-button");
            _idleButton.clicked += ToggleIdle;
            Hover(_idleButton, DemoCursorStates.Pointer);

            var skins = root.Q("skins");
            skins.Clear();
            _skinButtons.Clear();
            foreach (var skin in _skins)
            {
                Button button = new(() => SetSkin(skin)) { text = skin.name };
                button.AddToClassList("chip");
                Hover(button, DemoCursorStates.Pointer);
                skins.Add(button);
                _skinButtons.Add(button);
            }

            _status = root.Q<Label>("status");
            RefreshButtons();
        }

        private void OnDisable()
        {
            _busy.Release();
        }

        private void Update()
        {
            if (_taskRemaining > 0f)
            {
                _taskRemaining -= Time.unscaledDeltaTime;
                if (_taskRemaining <= 0f)
                {
                    _cursor.SetState(DemoCursorStates.Default);
                }
            }

            _status.text = DemoStatus.Describe(_cursor);
        }

        private void Hover(VisualElement element, CursorStateId state, CursorStateId pressedState = default)
        {
            element.AddManipulator(new CursorHoverManipulator(_cursor, state, pressedState));
        }

        private void StartTask()
        {
            _taskRemaining = _taskSeconds;
            _cursor.SetState(DemoCursorStates.Busy);
        }

        private void ToggleBusy()
        {
            if (_busy.IsActive)
            {
                _busy.Release();
            }
            else
            {
                _busy = _cursor.Request(DemoCursorStates.Busy, _busyPriority);
            }

            RefreshButtons();
        }

        private void ToggleIdle()
        {
            _cursor.IdleEnabled = !_cursor.IdleEnabled;
            RefreshButtons();
        }

        private void SetSkin(CursorSet skin)
        {
            _cursor.SetCursorSet(skin);
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            _busyButton.text = _busy.IsActive ? "Release Busy" : "Hold Busy over everything";
            _idleButton.text = _cursor.IdleEnabled ? "Idle: on" : "Idle: off";
            for (var i = 0; i < _skinButtons.Count; i++)
            {
                _skinButtons[i].EnableInClassList(SelectedClass, _skins[i] == _cursor.CursorSet);
            }
        }
    }
}
