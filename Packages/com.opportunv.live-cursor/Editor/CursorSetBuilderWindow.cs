using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorSetBuilderWindow : EditorWindow
    {
        [SerializeField] private string _sourceFolder;
        [SerializeField] private string _definitionPath;

        private const float PreviewSize = 256f;
        private const float NumberWidth = 64f;
        private const float StateNameWidth = 150f;
        private const float StateDropdownWidth = 130f;
        private const int MaxMessages = 30;
        private const string OwnClassChoice = "Own class";
        private const string DefaultHotspotChoice = "Default (all states)";
        private const float HotspotWidth = 70f;

        private static readonly Color _mutedText = new(0.6f, 0.6f, 0.6f);
        private static readonly Color _previewBackground = new(0.16f, 0.16f, 0.16f);
        private static readonly Color _inheritedMarker = new(1f, 1f, 1f, 0.55f);

        private readonly Dictionary<CursorBuilderState, Button> _hotspotButtons = new();

        private CursorBuilderModel _model;
        private Texture2D _previewTexture;
        private int _previewZoom = 1;
        private ScrollView _content;
        private VisualElement _messages;
        private VisualElement _transitionsContainer;
        private VisualElement _previewFrame;
        private Image _previewImage;
        private VisualElement _hotspotMarker;
        private IntegerField _hotspotX;
        private IntegerField _hotspotY;
        private DropdownField _hotspotTarget;
        private Toggle _ownHotspot;
        private CursorBuilderState _hotspotState;
        private Button _createButton;

        public static void OpenForFolder(string folder)
        {
            var window = Open();
            window._sourceFolder = folder;
            window._definitionPath = FindExistingDefinition(folder);
            window.Reload();
        }

        public static void OpenForDefinition(string definitionPath)
        {
            var window = Open();
            window._definitionPath = definitionPath;
            window._sourceFolder = Path.GetDirectoryName(definitionPath)?.Replace('\\', '/');
            window.Reload();
        }

        private static CursorSetBuilderWindow Open()
        {
            var window = GetWindow<CursorSetBuilderWindow>("Cursor Set Builder", true, typeof(CursorSetPreviewWindow));
            window.minSize = new(760f, 480f);
            return window;
        }

        private static string FindExistingDefinition(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            var files = Directory.GetFiles(folder, $"*.{CursorSetImporter.Extension}", SearchOption.TopDirectoryOnly);
            return files.Length == 1 ? files[0].Replace('\\', '/') : null;
        }

        private void CreateGUI()
        {
            Reload();
        }

        private void OnDisable()
        {
            DestroyPreview();
        }

        private void Reload()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            _model = null;
            _hotspotState = null;
            if (!string.IsNullOrEmpty(_definitionPath) && File.Exists(_definitionPath))
            {
                _model = CursorBuilderModel.FromDefinitionFile(_definitionPath);
            }
            else if (!string.IsNullOrEmpty(_sourceFolder) && Directory.Exists(_sourceFolder) &&
                     _sourceFolder != "Assets")
            {
                _model = CursorBuilderModel.FromFolder(_sourceFolder);
            }

            Rebuild();
        }

        private void Rebuild()
        {
            rootVisualElement.Clear();
            _content = new();
            _content.style.paddingLeft = 8f;
            _content.style.paddingRight = 8f;
            _content.style.paddingTop = 6f;
            _content.style.paddingBottom = 8f;
            rootVisualElement.Add(_content);

            BuildSource();
            if (_model == null)
            {
                _content.Add(new HelpBox(
                    "Choose a folder that contains the cursor frames. Every subfolder of numbered PNGs and every sprite sheet named like 'Grab_4x2.png' becomes a state or, when it is named like 'DefaultToGrab', a transition.",
                    HelpBoxMessageType.Info));
                return;
            }

            BuildOutput();
            BuildHotspot();
            BuildStates();
            _transitionsContainer = new();
            _content.Add(_transitionsContainer);
            BuildTransitions();
            BuildCode();

            _messages = new();
            _messages.style.marginTop = 8f;
            _content.Add(_messages);

            _createButton = new(Create);
            _createButton.style.height = 28f;
            _createButton.style.marginTop = 6f;
            _content.Add(_createButton);

            RefreshPreview();
            RefreshMessages();
        }

        private void BuildSource()
        {
            var row = Row();
            var label = new Label("Source folder");
            label.style.width = 110f;
            row.Add(label);

            TextField field = new() { value = _sourceFolder ?? string.Empty, isReadOnly = true };
            field.style.flexGrow = 1f;
            row.Add(field);

            row.Add(new Button(BrowseFolder) { text = "Browse…" });
            Button rescan = new(Reload) { text = "Rescan" };
            rescan.SetEnabled(_model != null);
            row.Add(rescan);
            _content.Add(row);
        }

        private void BuildOutput()
        {
            var section = Section("Output");

            TextField output = new("Cursor set file") { value = _model.OutputPath };
            output.RegisterValueChangedCallback(evt =>
            {
                _model.OutputPath = evt.newValue.Replace('\\', '/');
                RefreshMessages();
            });
            section.Add(output);

            TextField sizes = new("Sizes (px)") { value = string.Join(", ", _model.Sizes) };
            sizes.tooltip = "Square sizes to bake. At runtime the smallest size covering the system cursor is used.";
            sizes.RegisterValueChangedCallback(evt =>
            {
                ParseSizes(evt.newValue);
                RefreshMessages();
            });
            section.Add(sizes);
        }

        private void BuildHotspot()
        {
            var section = Section("Hotspot");
            var row = Row();
            row.style.alignItems = Align.FlexStart;

            _previewFrame = new();
            _previewFrame.style.backgroundColor = _previewBackground;
            _previewFrame.style.marginRight = 10f;
            _previewImage = new() { scaleMode = ScaleMode.StretchToFill };
            _previewImage.RegisterCallback<PointerDownEvent>(evt => SetHotspotFromPointer(evt.localPosition));
            _previewImage.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if ((evt.pressedButtons & 1) != 0)
                {
                    SetHotspotFromPointer(evt.localPosition);
                }
            });
            _previewFrame.Add(_previewImage);

            _hotspotMarker = new() { pickingMode = PickingMode.Ignore };
            _hotspotMarker.style.position = Position.Absolute;
            SetBorder(_hotspotMarker, Color.red, 1f);
            _previewFrame.Add(_hotspotMarker);
            row.Add(_previewFrame);

            VisualElement fields = new();
            fields.style.flexGrow = 1f;
            _hotspotTarget = new("Hotspot for", new List<string>(), 0);
            _hotspotTarget.RegisterValueChangedCallback(evt =>
                SelectHotspotTarget(_model.States.Find(state => state.Include && state.Name == evt.newValue)));
            _ownHotspot = new("Own hotspot")
            {
                tooltip = "Use a click point for this state instead of the default. Transitions move between the two."
            };
            _ownHotspot.RegisterValueChangedCallback(evt =>
            {
                if (_hotspotState == null)
                {
                    return;
                }

                _hotspotState.Hotspot = _model.Hotspot;
                _hotspotState.HasOwnHotspot = evt.newValue;
                RefreshHotspotEditor();
                RefreshMessages();
            });
            _hotspotX = new("X");
            _hotspotY = new("Y");
            _hotspotX.RegisterValueChangedCallback(evt => SetHotspot(new(evt.newValue, _hotspotY.value)));
            _hotspotY.RegisterValueChangedCallback(evt => SetHotspot(new(_hotspotX.value, evt.newValue)));
            fields.Add(_hotspotTarget);
            fields.Add(_ownHotspot);
            fields.Add(_hotspotX);
            fields.Add(_hotspotY);
            fields.Add(Muted(
                "Click or drag on the frame to set the click point. Pick a state to give it its own, for example the centre of a text beam or crosshair.\nCoordinates are source pixels from the top-left corner and are scaled for every size."));
            row.Add(fields);
            section.Add(row);
            RefreshHotspotChoices();
        }

        private void RefreshHotspotChoices()
        {
            if (_hotspotTarget == null)
            {
                return;
            }

            if (_hotspotState is { Include: false })
            {
                _hotspotState = null;
            }

            List<string> choices = new() { DefaultHotspotChoice };
            choices.AddRange(_model.IncludedStateNames());
            _hotspotTarget.choices = choices;
            _hotspotTarget.SetValueWithoutNotify(_hotspotState?.Name ?? DefaultHotspotChoice);
            RefreshHotspotEditor();
        }

        private void SelectHotspotTarget(CursorBuilderState state)
        {
            _hotspotState = state;
            _hotspotTarget.SetValueWithoutNotify(state?.Name ?? DefaultHotspotChoice);
            RefreshPreview();
            RefreshHotspotEditor();
        }

        private void RefreshHotspotEditor()
        {
            if (_hotspotX == null)
            {
                return;
            }

            _ownHotspot.style.display = _hotspotState != null ? DisplayStyle.Flex : DisplayStyle.None;
            _ownHotspot.SetValueWithoutNotify(_hotspotState is { HasOwnHotspot: true });
            var hotspot = _model.HotspotOf(_hotspotState);
            _hotspotX.SetValueWithoutNotify(hotspot.x);
            _hotspotY.SetValueWithoutNotify(hotspot.y);
            PlaceMarker();

            foreach (var pair in _hotspotButtons)
            {
                pair.Value.text = HotspotLabel(pair.Key);
            }
        }

        private string HotspotLabel(CursorBuilderState state)
        {
            return state.HasOwnHotspot ? $"{state.Hotspot.x}, {state.Hotspot.y}" : "default";
        }

        private void BuildStates()
        {
            var section = Section($"States ({_model.States.Count})");
            var header = Row();
            header.Add(Fixed(new Label(string.Empty), 22f));
            header.Add(Fixed(Muted("Name"), StateNameWidth));
            header.Add(Fixed(Muted("Frames"), NumberWidth));
            header.Add(Fixed(Muted("Frame ms"), NumberWidth));
            header.Add(Fixed(Muted("Delay ms"), NumberWidth));
            header.Add(Fixed(Muted("Hotspot"), HotspotWidth));
            header.Add(Muted("Source"));
            section.Add(header);

            _hotspotButtons.Clear();
            foreach (var state in _model.States)
            {
                section.Add(StateRow(state));
            }
        }

        private VisualElement StateRow(CursorBuilderState state)
        {
            var row = Row();

            Toggle include = new() { value = state.Include, tooltip = "Include this state" };
            include.RegisterValueChangedCallback(evt =>
            {
                state.Include = evt.newValue;
                BuildTransitions();
                RefreshHotspotChoices();
                RefreshPreview();
                RefreshMessages();
            });
            row.Add(Fixed(include, 22f));

            TextField textField = new() { value = state.Name, isDelayed = true };
            textField.RegisterValueChangedCallback(evt => RenameState(state, evt.newValue));
            row.Add(Fixed(textField, StateNameWidth));

            row.Add(Fixed(FramesField(state.Clip, state.IsMissing), NumberWidth));

            FloatField duration = new() { value = state.FrameDurationMs };
            duration.RegisterValueChangedCallback(evt =>
            {
                state.FrameDurationMs = evt.newValue;
                RefreshMessages();
            });
            row.Add(Fixed(duration, NumberWidth));

            FloatField delay = new() { value = state.LoopDelayMs };
            delay.RegisterValueChangedCallback(evt =>
            {
                state.LoopDelayMs = evt.newValue;
                RefreshMessages();
            });
            row.Add(Fixed(delay, NumberWidth));

            Button hotspot = new(() => SelectHotspotTarget(state.Include ? state : null))
            {
                text = HotspotLabel(state),
                tooltip = "Edit this state's hotspot."
            };
            _hotspotButtons[state] = hotspot;
            row.Add(Fixed(hotspot, HotspotWidth));

            row.Add(SourceLabel(state.Clip));
            return row;
        }

        private void BuildTransitions()
        {
            _transitionsContainer.Clear();
            var section = Section($"Transitions ({_model.Transitions.Count})", _transitionsContainer);
            if (_model.Transitions.Count == 0)
            {
                section.Add(Muted("No transition folders found. Name them like 'DefaultToGrab' or 'Default_to_Grab'."));
                return;
            }

            var header = Row();
            header.Add(Fixed(new Label(string.Empty), 22f));
            header.Add(Fixed(Muted("From"), StateDropdownWidth));
            header.Add(Fixed(Muted("To"), StateDropdownWidth));
            header.Add(Fixed(Muted("Frames"), NumberWidth));
            header.Add(Fixed(Muted("Frame ms"), NumberWidth));
            header.Add(Fixed(Muted("Total"), NumberWidth));
            header.Add(Fixed(Muted("Ends"), 40f));
            header.Add(Fixed(Muted("Reverse"), 54f));
            header.Add(Fixed(Muted("Rev. ms"), NumberWidth));
            header.Add(Muted("Source"));
            section.Add(header);

            var stateNames = _model.IncludedStateNames();
            foreach (var transition in _model.Transitions)
            {
                section.Add(TransitionRow(transition, stateNames));
            }
        }

        private VisualElement TransitionRow(CursorBuilderTransition transition, List<string> stateNames)
        {
            var row = Row();

            Toggle include = new() { value = transition.Include, tooltip = "Include this transition" };
            include.RegisterValueChangedCallback(evt =>
            {
                transition.Include = evt.newValue;
                RefreshMessages();
            });
            row.Add(Fixed(include, 22f));

            row.Add(Fixed(StateDropdown(transition.From, stateNames, value =>
            {
                transition.From = value;
                RefreshMessages();
            }), StateDropdownWidth));
            row.Add(Fixed(StateDropdown(transition.To, stateNames, value =>
            {
                transition.To = value;
                RefreshMessages();
            }), StateDropdownWidth));

            row.Add(Fixed(FramesField(transition.Clip, transition.IsMissing), NumberWidth));

            var total = new Label(TotalLabel(transition));
            total.tooltip = "Time from the request until the destination state shows.";

            FloatField duration = new() { value = transition.FrameDurationMs };
            duration.RegisterValueChangedCallback(evt =>
            {
                transition.FrameDurationMs = evt.newValue;
                total.text = TotalLabel(transition);
                RefreshMessages();
            });
            row.Add(Fixed(duration, NumberWidth));
            row.Add(Fixed(total, NumberWidth));

            Toggle endpoints = new()
            {
                value = transition.IncludesEndpoints,
                tooltip = "The first and last frames repeat the two states' first frames and are skipped during playback."
            };
            endpoints.RegisterValueChangedCallback(evt =>
            {
                transition.IncludesEndpoints = evt.newValue;
                total.text = TotalLabel(transition);
                RefreshMessages();
            });
            row.Add(Fixed(endpoints, 40f));

            FloatField reverseDuration = new()
            {
                value = transition.ReverseFrameDurationMs,
                tooltip = "Frame time when played backwards. 0 uses the forward frame time."
            };
            reverseDuration.SetEnabled(transition.Reversible);
            reverseDuration.RegisterValueChangedCallback(evt =>
            {
                transition.ReverseFrameDurationMs = evt.newValue;
                RefreshMessages();
            });

            Toggle reversible = new()
            {
                value = transition.Reversible,
                tooltip = "Play this transition backwards for the opposite direction."
            };
            reversible.RegisterValueChangedCallback(evt =>
            {
                transition.Reversible = evt.newValue;
                reverseDuration.SetEnabled(evt.newValue);
                RefreshMessages();
            });
            row.Add(Fixed(reversible, 54f));
            row.Add(Fixed(reverseDuration, NumberWidth));

            row.Add(SourceLabel(transition.Clip));
            return row;
        }

        private void BuildCode()
        {
            var section = Section("State constants");
            section.Add(Muted(
                "Generates a static class with a CursorStateId field per state, regenerated on every import. Interchangeable sets, such as skins picked in settings, should share one class."));

            List<CursorStateCodeTarget> targets = new();
            List<string> shareChoices = new() { OwnClassChoice };
            foreach (var target in CursorStateCodePostprocessor.FindTargets())
            {
                if (target.SetPaths.Count == 1 && target.SetPaths[0] == _model.OutputPath)
                {
                    continue;
                }

                targets.Add(target);
                shareChoices.Add($"{target.FullName}  ({string.Join(", ", target.SetPaths.ConvertAll(Path.GetFileName))})");
            }

            DropdownField share = new("Share with", shareChoices, SharedChoice(targets, shareChoices));
            TextField className = new("Class name") { value = _model.ClassName, isDelayed = true };
            TextField @namespace = new("Namespace") { value = _model.Namespace, isDelayed = true };
            TextField path = new("File") { value = _model.CodePath, isDelayed = true };

            void SyncFields()
            {
                className.SetValueWithoutNotify(_model.ClassName);
                @namespace.SetValueWithoutNotify(_model.Namespace);
                path.SetValueWithoutNotify(_model.CodePath);
                share.SetValueWithoutNotify(SharedChoice(targets, shareChoices));
                RefreshMessages();
            }

            void SetFieldsEnabled(bool enabled)
            {
                share.SetEnabled(enabled && targets.Count > 0);
                className.SetEnabled(enabled);
                @namespace.SetEnabled(enabled);
                path.SetEnabled(enabled);
            }

            Toggle generate = new("Generate") { value = _model.GenerateCode };
            generate.RegisterValueChangedCallback(evt =>
            {
                _model.GenerateCode = evt.newValue;
                SetFieldsEnabled(evt.newValue);
                RefreshMessages();
            });

            share.RegisterValueChangedCallback(evt =>
            {
                var index = shareChoices.IndexOf(evt.newValue) - 1;
                if (index >= 0)
                {
                    _model.ShareWith(targets[index]);
                }
                else
                {
                    _model.NameClassAfterOutput();
                }

                SyncFields();
            });
            className.RegisterValueChangedCallback(evt =>
            {
                _model.RenameClass(evt.newValue.Trim());
                SyncFields();
            });
            @namespace.RegisterValueChangedCallback(evt =>
            {
                _model.Namespace = evt.newValue.Trim();
                SyncFields();
            });
            path.RegisterValueChangedCallback(evt =>
            {
                _model.CodePath = evt.newValue.Trim().Replace('\\', '/');
                SyncFields();
            });

            SetFieldsEnabled(_model.GenerateCode);
            section.Add(generate);
            section.Add(share);
            section.Add(className);
            section.Add(@namespace);
            section.Add(path);
        }

        private VisualElement FramesField(CursorScannedClip clip, bool missing)
        {
            if (missing || !clip.CanBeSheet)
            {
                return new Label(missing ? "missing" : clip.FrameCount.ToString());
            }

            TextField field = new() { value = clip.Sheet.ToString(), isDelayed = true };
            field.tooltip = SheetTooltip(clip);
            field.RegisterValueChangedCallback(evt =>
            {
                var text = evt.newValue.Trim();
                CursorSheetLayout layout = default;
                if (text.Length > 0 && text != "1" && !CursorSheetLayout.TryParse(text, out layout))
                {
                    field.SetValueWithoutNotify(clip.Sheet.ToString());
                    return;
                }

                _model.SetSheet(clip, layout);
                field.SetValueWithoutNotify(clip.Sheet.ToString());
                field.tooltip = SheetTooltip(clip);
                BuildTransitions();
                RefreshPreview();
                RefreshMessages();
            });
            return field;
        }

        private string SharedChoice(List<CursorStateCodeTarget> targets, List<string> shareChoices)
        {
            var index = targets.FindIndex(target => target.CodePath == _model.CodePath);
            return shareChoices[index + 1];
        }

        private void RenameState(CursorBuilderState state, string newName)
        {
            var oldName = state.Name;
            state.Name = newName.Trim();
            foreach (var transition in _model.Transitions)
            {
                if (transition.From == oldName)
                {
                    transition.From = state.Name;
                }

                if (transition.To == oldName)
                {
                    transition.To = state.Name;
                }
            }

            BuildTransitions();
            RefreshHotspotChoices();
            RefreshMessages();
        }

        private void SetHotspotFromPointer(Vector3 localPosition)
        {
            var x = Mathf.FloorToInt(localPosition.x / _previewZoom);
            var y = Mathf.FloorToInt(localPosition.y / _previewZoom);
            _hotspotX.SetValueWithoutNotify(x);
            _hotspotY.SetValueWithoutNotify(y);
            SetHotspot(new(x, y));
        }

        private void SetHotspot(Vector2Int hotspot)
        {
            if (_hotspotState != null)
            {
                _hotspotState.HasOwnHotspot = true;
                _hotspotState.Hotspot = hotspot;
            }
            else
            {
                _model.Hotspot = hotspot;
            }

            RefreshHotspotEditor();
            RefreshMessages();
        }

        private void RefreshPreview()
        {
            DestroyPreview();
            var clip = _hotspotState is { IsMissing: false } ? _hotspotState.Clip : _model.HotspotPreviewClip();
            if (clip == null)
            {
                _previewFrame.style.display = DisplayStyle.None;
                return;
            }

            _previewTexture = CursorClipFrameReader.Read(clip, 0);
            if (!_previewTexture)
            {
                _previewFrame.style.display = DisplayStyle.None;
                return;
            }

            _previewTexture.filterMode = FilterMode.Point;
            _previewTexture.hideFlags = HideFlags.HideAndDontSave;
            _previewZoom = Mathf.Max(1, Mathf.FloorToInt(PreviewSize / Mathf.Max(_previewTexture.width,
                _previewTexture.height)));

            var width = _previewTexture.width * _previewZoom;
            var height = _previewTexture.height * _previewZoom;
            _previewFrame.style.display = DisplayStyle.Flex;
            _previewFrame.style.width = width;
            _previewFrame.style.height = height;
            _previewImage.image = _previewTexture;
            _previewImage.style.width = width;
            _previewImage.style.height = height;
            PlaceMarker();
        }

        private void PlaceMarker()
        {
            if (!_previewTexture)
            {
                return;
            }

            var size = Mathf.Max(_previewZoom, 3);
            var offset = (size - _previewZoom) / 2f;
            var hotspot = _model.HotspotOf(_hotspotState);
            _hotspotMarker.style.left = hotspot.x * _previewZoom - offset;
            _hotspotMarker.style.top = hotspot.y * _previewZoom - offset;
            SetBorder(_hotspotMarker, _hotspotState is { HasOwnHotspot: false } ? _inheritedMarker : Color.red, 1f);
            _hotspotMarker.style.width = size;
            _hotspotMarker.style.height = size;
        }

        private void RefreshMessages()
        {
            if (_messages == null)
            {
                return;
            }

            _messages.Clear();
            CursorImportReport report = new();
            _model.Validate(report);

            var shown = 0;
            foreach (var error in report.Errors)
            {
                if (shown++ < MaxMessages)
                {
                    _messages.Add(new HelpBox(error, HelpBoxMessageType.Error));
                }
            }

            foreach (var warning in report.Warnings)
            {
                if (shown++ < MaxMessages)
                {
                    _messages.Add(new HelpBox(warning, HelpBoxMessageType.Warning));
                }
            }

            var exists = File.Exists(_model.OutputPath);
            _createButton.text = exists ? "Update Cursor Set" : "Create Cursor Set";
            _createButton.SetEnabled(!report.HasErrors);
        }

        private void Create()
        {
            CursorImportReport report = new();
            _model.Validate(report);
            if (report.HasErrors)
            {
                RefreshMessages();
                return;
            }

            var path = _model.OutputPath;
            if (File.Exists(path) && path != _definitionPath && !EditorUtility.DisplayDialog("Overwrite cursor set?",
                    $"'{path}' already exists. Replace it with the set from this window?", "Replace", "Cancel"))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path, CursorSetDefinitionWriter.Write(_model.ToDefinition()));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            _definitionPath = path;

            var asset = AssetDatabase.LoadAssetAtPath<CursorSet>(path);
            if (asset)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            RefreshMessages();
        }

        private void BrowseFolder()
        {
            var start = string.IsNullOrEmpty(_sourceFolder) ? Application.dataPath : Path.GetFullPath(_sourceFolder);
            var selected = EditorUtility.OpenFolderPanel("Choose the cursor frames folder", start, string.Empty);
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            var projectPath = ToProjectPath(selected);
            if (projectPath == null)
            {
                EditorUtility.DisplayDialog("Folder outside the project",
                    "Choose a folder inside this project's Assets folder.", "OK");
                return;
            }

            _sourceFolder = projectPath;
            _definitionPath = FindExistingDefinition(projectPath);
            Reload();
        }

        private void ParseSizes(string text)
        {
            _model.Sizes.Clear();
            foreach (var part in text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                _model.Sizes.Add(int.TryParse(part, out var size) ? size : 0);
            }
        }

        private void DestroyPreview()
        {
            if (!_previewTexture)
            {
                return;
            }

            DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }

        private static string ToProjectPath(string absolutePath)
        {
            var normalized = Path.GetFullPath(absolutePath).Replace('\\', '/').TrimEnd('/');
            var assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            if (normalized == assets)
            {
                return "Assets";
            }

            return normalized.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase)
                ? "Assets" + normalized.Substring(assets.Length)
                : null;
        }

        private static string SheetTooltip(CursorScannedClip clip)
        {
            var frames = clip.IsSheet ? $"{clip.FrameCount} frames of {clip.FrameWidth}x{clip.FrameHeight}" : "1 frame";
            return $"{frames}. Type a sprite sheet layout as columns x rows, optionally with a frame count " +
                   "for a partly filled last row, e.g. 4x2 or 4x2:7. Type 1 for a single image.";
        }

        private static string TotalLabel(CursorBuilderTransition transition)
        {
            var frames = transition.Clip.FrameCount - (transition.IncludesEndpoints ? 2 : 0);
            return $"{Mathf.Max(0, frames) * transition.FrameDurationMs:0} ms";
        }

        private VisualElement Section(string text, VisualElement parent = null)
        {
            Foldout foldout = new() { text = text, value = true };
            foldout.style.marginTop = 8f;
            (parent ?? _content).Add(foldout);
            return foldout;
        }

        private static DropdownField StateDropdown(string value, List<string> stateNames, Action<string> changed)
        {
            List<string> choices = new(stateNames);
            if (!string.IsNullOrEmpty(value) && !choices.Contains(value))
            {
                choices.Add(value);
            }

            DropdownField dropdown = new(choices, value ?? string.Empty);
            dropdown.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return dropdown;
        }

        private static VisualElement Row()
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2f;
            return row;
        }

        private static T Fixed<T>(T element, float width) where T : VisualElement
        {
            element.style.width = width;
            element.style.flexShrink = 0f;
            element.style.marginLeft = 0f;
            element.style.marginRight = 4f;
            return element;
        }

        private static Label Muted(string text)
        {
            Label label = new(text);
            label.style.color = _mutedText;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static Label SourceLabel(CursorScannedClip clip)
        {
            var label = Muted(clip.Key);
            label.tooltip = clip.Problem ?? clip.Key;
            label.style.flexShrink = 1f;
            label.style.overflow = Overflow.Hidden;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.textOverflow = TextOverflow.Ellipsis;
            if (clip.Problem != null)
            {
                label.style.color = new Color(0.9f, 0.4f, 0.3f);
            }

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
