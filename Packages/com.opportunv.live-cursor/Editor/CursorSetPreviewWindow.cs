using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorSetPreviewWindow : EditorWindow
    {
        [SerializeField] private CursorSet _set;
        [SerializeField] private string _setPath;
        [SerializeField] private int _size;
        [SerializeField] private float _speed = 1f;
        [SerializeField] private bool _paused;
        [SerializeField] private bool _idleDisabled;
        [SerializeField] private bool _immediate;
        [SerializeField] private bool _autoCycle;
        [SerializeField] private float _cycleInterval = 1.5f;
        [SerializeField] private bool _lightBackground;

        private const float StageSize = 256f;
        private const float TryAreaSize = 200f;
        private const float MaxDeltaTime = 0.25f;

        private static readonly Color _darkStage = new(0.16f, 0.16f, 0.16f);
        private static readonly Color _lightStage = new(0.85f, 0.85f, 0.85f);
        private static readonly Color _currentColor = new(0.24f, 0.42f, 0.7f);
        private static readonly Color _targetColor = new(0.95f, 0.7f, 0.25f);

        private readonly CursorPreviewOutput _output = new();
        private readonly List<Button> _stateButtons = new();
        private CursorPlayer _player;
        private double _lastTime;
        private float _cycleElapsed;
        private bool _hardwareActive;
        private ObjectField _setField;
        private DropdownField _sizeField;
        private Button _playButton;
        private VisualElement _stateRow;
        private VisualElement _stage;
        private VisualElement _tryArea;
        private Image _image;
        private VisualElement _hotspotMarker;
        private Label _status;

        public static void Open(CursorSet set)
        {
            var window = GetWindow<CursorSetPreviewWindow>("Cursor Set Preview", true, typeof(CursorSetBuilderWindow));
            window.minSize = new(540f, 440f);
            if (set)
            {
                window.SetCursorSet(set);
            }
        }

        private void OnEnable()
        {
            _player = new(_output);
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            CursorSetImportWatcher.Imported += OnSetImported;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            CursorSetImportWatcher.Imported -= OnSetImported;
            ReleaseHardware();
        }

        private void CreateGUI()
        {
            BuildLayout();
            if (_set == null && Selection.activeObject is CursorSet selected)
            {
                _set = selected;
            }

            ApplySet();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is CursorSet selected && selected != _set)
            {
                SetCursorSet(selected);
            }
        }

        private void SetCursorSet(CursorSet set)
        {
            _set = set;
            ApplySet();
        }

        private void BuildLayout()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 8f;

            _setField = new("Cursor set") { objectType = typeof(CursorSet), allowSceneObjects = false };
            _setField.RegisterValueChangedCallback(evt => SetCursorSet(evt.newValue as CursorSet));
            root.Add(_setField);

            var settings = Row();
            _sizeField = new("Size", new List<string>(), 0);
            _sizeField.style.width = 180f;
            _sizeField.labelElement.style.minWidth = 40f;
            _sizeField.RegisterValueChangedCallback(evt =>
            {
                _size = int.TryParse(evt.newValue.Replace(" px", string.Empty), out var size) ? size : 0;
                _player.SetSystemCursorSize(_size);
                RefreshFrame();
            });
            settings.Add(_sizeField);

            Slider speed = new("Speed", 0.1f, 2f) { value = _speed, showInputField = true };
            speed.style.flexGrow = 1f;
            speed.labelElement.style.minWidth = 50f;
            speed.RegisterValueChangedCallback(evt => _speed = evt.newValue);
            settings.Add(speed);
            root.Add(settings);

            var playback = Row();
            _playButton = new(TogglePause);
            _playButton.style.width = 70f;
            playback.Add(_playButton);
            playback.Add(ToggleField("Idle", !_idleDisabled, "Play each state's loop.", value =>
            {
                _idleDisabled = !value;
                _player.IdleEnabled = value;
            }));
            playback.Add(ToggleField("Immediate", _immediate,
                "Request states with immediate: true, as for click-driven changes.", value => _immediate = value));
            playback.Add(ToggleField("Auto-cycle", _autoCycle, "Step through the states on a timer.", value =>
            {
                _autoCycle = value;
                _cycleElapsed = 0f;
            }));
            FloatField interval = new() { value = _cycleInterval, tooltip = "Seconds between auto-cycle steps." };
            interval.style.width = 44f;
            interval.RegisterValueChangedCallback(evt => _cycleInterval = Mathf.Max(0.1f, evt.newValue));
            playback.Add(interval);
            playback.Add(ToggleField("Light background", _lightBackground, "Check the cursor against a light backdrop.",
                value =>
                {
                    _lightBackground = value;
                    RefreshBackground();
                }));
            root.Add(playback);

            _stateRow = Row();
            _stateRow.style.flexWrap = Wrap.Wrap;
            _stateRow.style.marginTop = 6f;
            root.Add(_stateRow);

            var stages = Row();
            stages.style.alignItems = Align.FlexStart;
            stages.style.marginTop = 6f;

            _stage = new();
            _stage.style.width = StageSize;
            _stage.style.height = StageSize;
            _stage.style.marginRight = 10f;
            _stage.style.overflow = Overflow.Hidden;
            _image = new() { scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            _image.style.position = Position.Absolute;
            _stage.Add(_image);
            _hotspotMarker = new() { pickingMode = PickingMode.Ignore };
            _hotspotMarker.style.position = Position.Absolute;
            SetBorder(_hotspotMarker, Color.red, 1f);
            _stage.Add(_hotspotMarker);
            stages.Add(_stage);

            VisualElement tryColumn = new();
            _tryArea = new IMGUIContainer(DrawTryArea);
            _tryArea.style.width = TryAreaSize;
            _tryArea.style.height = TryAreaSize;
            _tryArea.RegisterCallback<PointerEnterEvent>(_ => ActivateHardware());
            _tryArea.RegisterCallback<PointerLeaveEvent>(_ => ReleaseHardware());
            tryColumn.Add(_tryArea);
            tryColumn.Add(Muted("Move the pointer into this box to see the real hardware cursor."));
            tryColumn.style.width = TryAreaSize;
            stages.Add(tryColumn);
            root.Add(stages);

            _status = new();
            _status.style.marginTop = 6f;
            root.Add(_status);

            RefreshBackground();
            RefreshPlayButton();
        }

        private void ApplySet()
        {
            if (_setField == null)
            {
                return;
            }

            _setField.SetValueWithoutNotify(_set);
            _setPath = _set != null ? AssetDatabase.GetAssetPath(_set) : null;
            _player.SetSet(_set);
            _player.IdleEnabled = !_idleDisabled;
            _cycleElapsed = 0f;
            BuildSizes();
            BuildStateButtons();
            RefreshFrame();
        }

        private void BuildSizes()
        {
            List<string> choices = new();
            var count = _set != null ? _set.SizeCount : 0;
            var selected = count - 1;
            for (var i = 0; i < count; i++)
            {
                choices.Add($"{_set.GetSize(i)} px");
                if (_set.GetSize(i) == _size)
                {
                    selected = i;
                }
            }

            _sizeField.choices = choices;
            _sizeField.SetEnabled(choices.Count > 0);
            _sizeField.SetValueWithoutNotify(count > 0 ? choices[selected] : string.Empty);
            _size = count > 0 ? _set.GetSize(selected) : 0;
            _player.SetSystemCursorSize(_size);
        }

        private void BuildStateButtons()
        {
            _stateRow.Clear();
            _stateButtons.Clear();
            if (_set == null)
            {
                return;
            }

            for (var i = 0; i < _set.StateCount; i++)
            {
                var id = _set.GetStateId(i);
                Button button = new(() => RequestState(id)) { text = id.Name };
                SetBorder(button, Color.clear, 2f);
                _stateButtons.Add(button);
                _stateRow.Add(button);
            }
        }

        private void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = Mathf.Min((float)(now - _lastTime), MaxDeltaTime);
            _lastTime = now;
            if (!_player.Set || _paused)
            {
                return;
            }

            var scaled = delta * _speed;
            if (_autoCycle && !_player.IsTransitioning)
            {
                _cycleElapsed += scaled;
                if (_cycleElapsed >= _cycleInterval)
                {
                    _cycleElapsed = 0f;
                    var next = (_set.FindState(_player.TargetState) + 1) % _set.StateCount;
                    RequestState(_set.GetStateId(next));
                }
            }

            _player.Tick(scaled);
            if (_output.Changed)
            {
                RefreshFrame();
            }
        }

        private void RequestState(CursorStateId id)
        {
            _player.SetState(id, _immediate);
            _cycleElapsed = 0f;
            RefreshFrame();
        }

        private void TogglePause()
        {
            _paused = !_paused;
            RefreshPlayButton();
        }

        private void OnSetImported(string assetPath)
        {
            if (assetPath != _setPath)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                _set = AssetDatabase.LoadAssetAtPath<CursorSet>(assetPath);
                ApplySet();
            };
        }

        private void RefreshFrame()
        {
            _output.Changed = false;
            if (_image == null)
            {
                return;
            }

            var texture = _output.Texture;
            _image.image = texture;
            _image.style.display = texture != null ? DisplayStyle.Flex : DisplayStyle.None;
            _hotspotMarker.style.display = _image.style.display;
            if (texture != null)
            {
                var zoom = Mathf.Max(1, Mathf.FloorToInt(StageSize / Mathf.Max(texture.width, texture.height)));
                var width = texture.width * zoom;
                var height = texture.height * zoom;
                var left = (StageSize - width) / 2f;
                var top = (StageSize - height) / 2f;
                _image.style.left = left;
                _image.style.top = top;
                _image.style.width = width;
                _image.style.height = height;

                var marker = Mathf.Max(zoom, 3);
                var offset = (marker - zoom) / 2f;
                _hotspotMarker.style.left = left + Mathf.Floor(_output.Hotspot.x) * zoom - offset;
                _hotspotMarker.style.top = top + Mathf.Floor(_output.Hotspot.y) * zoom - offset;
                _hotspotMarker.style.width = marker;
                _hotspotMarker.style.height = marker;
            }

            if (_hardwareActive)
            {
                Cursor.SetCursor(texture, _output.Hotspot, CursorMode.Auto);
            }

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (!_player.Set)
            {
                _status.text = _set == null ? "Select a cursor set." : "This cursor set has no states.";
                return;
            }

            var target = _player.TargetState;
            if (_player.IsTransitioning)
            {
                _status.text = $"Transition to {target.Name}, frame {_player.FrameIndex}";
            }
            else
            {
                var state = _set.GetState(_set.FindState(_player.CurrentState));
                _status.text = $"{_player.CurrentState.Name}, frame {_player.FrameIndex + 1} of {state.Loop.FrameCount}" +
                               $", {_player.CursorSize} px";
            }

            for (var i = 0; i < _stateButtons.Count; i++)
            {
                var id = _set.GetStateId(i);
                var current = !_player.IsTransitioning && id == _player.CurrentState;
                var targeted = _player.IsTransitioning && id == target;
                _stateButtons[i].style.backgroundColor = current ? _currentColor : StyleKeyword.Null;
                SetBorder(_stateButtons[i], targeted ? _targetColor : Color.clear, 2f);
            }
        }

        private void RefreshBackground()
        {
            var color = _lightBackground ? _lightStage : _darkStage;
            _stage.style.backgroundColor = color;
            _tryArea.style.backgroundColor = color;
        }

        private void RefreshPlayButton()
        {
            _playButton.text = _paused ? "Play" : "Pause";
        }

        private void DrawTryArea()
        {
            if (_hardwareActive)
            {
                EditorGUIUtility.AddCursorRect(new(0f, 0f, TryAreaSize, TryAreaSize), MouseCursor.CustomCursor);
            }
        }

        private void ActivateHardware()
        {
            if (EditorApplication.isPlaying || _output.Texture == null)
            {
                return;
            }

            _hardwareActive = true;
            Cursor.SetCursor(_output.Texture, _output.Hotspot, CursorMode.Auto);
            _tryArea.MarkDirtyRepaint();
        }

        private void ReleaseHardware()
        {
            if (!_hardwareActive)
            {
                return;
            }

            _hardwareActive = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _tryArea?.MarkDirtyRepaint();
        }

        private static Toggle ToggleField(string text, bool value, string tooltip, System.Action<bool> changed)
        {
            Toggle toggle = new(text) { value = value, tooltip = tooltip };
            toggle.labelElement.style.minWidth = 0f;
            toggle.style.marginRight = 10f;
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return toggle;
        }

        private static VisualElement Row()
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2f;
            return row;
        }

        private static Label Muted(string text)
        {
            Label label = new(text);
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
        }
    }
}
