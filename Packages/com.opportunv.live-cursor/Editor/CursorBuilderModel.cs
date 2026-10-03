using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorBuilderModel
    {
        public const float DefaultStateFrameMs = 100f;
        public const float DefaultTransitionFrameMs = 33f;

        public string SourceFolder { get; private set; }

        public string OutputPath { get; set; }

        public List<int> Sizes { get; } = new() { 32, 48, 64 };

        public Vector2Int Hotspot { get; set; }

        public List<CursorBuilderState> States { get; } = new();

        public List<CursorBuilderTransition> Transitions { get; } = new();

        public bool GenerateCode { get; set; }

        public string ClassName { get; set; }

        public string Namespace { get; set; } = string.Empty;

        public string CodePath { get; set; }

        public static CursorBuilderModel FromFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            var folderName = Path.GetFileName(folder);
            CursorBuilderModel model = new()
            {
                SourceFolder = folder,
                OutputPath = $"{folder}/{folderName}.{CursorSetImporter.Extension}"
            };
            model.NameClassAfterOutput();

            var clips = CursorFolderScanner.Scan(folder);
            List<string> allNames = new();
            foreach (var clip in clips)
            {
                allNames.Add(clip.Name);
            }

            List<string> stateNames = new();
            foreach (var clip in clips)
            {
                if (!CursorNameMatcher.TryMatchTransition(clip.Name, allNames, out _, out _))
                {
                    stateNames.Add(clip.Name);
                }
            }

            foreach (var clip in clips)
            {
                if (!stateNames.Contains(clip.Name) &&
                    CursorNameMatcher.TryMatchTransition(clip.Name, stateNames, out var from, out var to))
                {
                    model.Transitions.Add(new(clip, from, to));
                }
                else
                {
                    model.States.Add(new(clip));
                }
            }

            foreach (var transition in model.Transitions)
            {
                transition.IncludesEndpoints = model.DetectEndpoints(transition);
            }

            return model;
        }

        public static CursorBuilderModel FromDefinitionFile(string definitionPath)
        {
            var folder = Path.GetDirectoryName(definitionPath)?.Replace('\\', '/') ?? string.Empty;
            var model = FromFolder(folder);
            model.OutputPath = definitionPath.Replace('\\', '/');
            model.NameClassAfterOutput();
            var definition = JsonUtility.FromJson<CursorSetDefinition>(File.ReadAllText(definitionPath));
            if (definition != null && model.MatchesAnyClip(definition))
            {
                model.ApplyDefinition(definition);
            }

            return model;
        }

        public void NameClassAfterOutput()
        {
            ClassName = CursorStateCodeGenerator.ToIdentifier(Path.GetFileNameWithoutExtension(OutputPath));
            CodePath = $"{OutputDirectory()}/{ClassName}.cs";
        }

        public void ShareWith(CursorStateCodeTarget target)
        {
            ClassName = target.ClassName;
            Namespace = target.Namespace;
            CodePath = target.CodePath;
        }

        public void RenameClass(string className)
        {
            if (!string.IsNullOrEmpty(CodePath) &&
                string.Equals(Path.GetFileNameWithoutExtension(CodePath), ClassName, StringComparison.Ordinal))
            {
                var directory = Path.GetDirectoryName(CodePath)?.Replace('\\', '/');
                CodePath = string.IsNullOrEmpty(directory) ? $"{className}.cs" : $"{directory}/{className}.cs";
            }

            ClassName = className;
        }

        public bool MatchesAnyClip(CursorSetDefinition definition)
        {
            var directory = OutputDirectory();
            foreach (var state in definition.states ?? Array.Empty<CursorStateDefinition>())
            {
                if (HasClip(ResolveKey(directory, state.frames)))
                {
                    return true;
                }
            }

            foreach (var transition in definition.transitions ?? Array.Empty<CursorTransitionDefinition>())
            {
                if (HasClip(ResolveKey(directory, transition.frames)))
                {
                    return true;
                }
            }

            return false;
        }

        public void ApplyDefinition(CursorSetDefinition definition)
        {
            var directory = OutputDirectory();
            if (definition.sizes is { Length: > 0 })
            {
                Sizes.Clear();
                Sizes.AddRange(definition.sizes);
            }

            if (definition.hotspot is { Length: 2 })
            {
                Hotspot = new(definition.hotspot[0], definition.hotspot[1]);
            }

            if (definition.code != null && !string.IsNullOrEmpty(definition.code.path))
            {
                GenerateCode = true;
                ClassName = string.IsNullOrEmpty(definition.code.className) ? ClassName : definition.code.className;
                Namespace = definition.code.@namespace ?? string.Empty;
                CodePath = Resolve(directory, definition.code.path);
            }

            foreach (var state in States)
            {
                state.Include = false;
            }

            foreach (var transition in Transitions)
            {
                transition.Include = false;
            }

            List<CursorBuilderState> states = new();
            foreach (var definitionState in definition.states ?? Array.Empty<CursorStateDefinition>())
            {
                var clip = TakeClip(directory, definitionState.frames, definitionState.name);
                var ownHotspot = CursorSetDefinitionValidator.HasHotspot(definitionState.hotspot);
                CursorBuilderState state = new(clip)
                {
                    Name = definitionState.name,
                    FrameDurationMs = definitionState.frameDurationMs,
                    LoopDelayMs = definitionState.loopDelayMs,
                    HasOwnHotspot = ownHotspot,
                    Hotspot = ownHotspot ? new(definitionState.hotspot[0], definitionState.hotspot[1]) : Hotspot
                };
                states.Add(state);
            }

            States.InsertRange(0, states);

            List<CursorBuilderTransition> transitions = new();
            foreach (var definitionTransition in definition.transitions ?? Array.Empty<CursorTransitionDefinition>())
            {
                var name = $"{definitionTransition.from}To{definitionTransition.to}";
                var clip = TakeClip(directory, definitionTransition.frames, name);
                CursorBuilderTransition transition = new(clip, definitionTransition.from, definitionTransition.to)
                {
                    FrameDurationMs = definitionTransition.frameDurationMs,
                    Reversible = definitionTransition.reversible,
                    ReverseFrameDurationMs = definitionTransition.reverseFrameDurationMs,
                    IncludesEndpoints = definitionTransition.includesEndpoints,
                    Hotspot = CursorSetDefinitionValidator.HasHotspot(definitionTransition.hotspot)
                        ? definitionTransition.hotspot
                        : null
                };
                transitions.Add(transition);
            }

            Transitions.InsertRange(0, transitions);
        }

        public CursorSetDefinition ToDefinition()
        {
            var directory = OutputDirectory();
            List<CursorStateDefinition> states = new();
            foreach (var state in States)
            {
                if (!state.Include)
                {
                    continue;
                }

                states.Add(new()
                {
                    name = state.Name,
                    frames = FramesFor(state.Clip, directory),
                    frameDurationMs = state.FrameDurationMs,
                    loopDelayMs = state.LoopDelayMs,
                    hotspot = state.HasOwnHotspot ? new[] { state.Hotspot.x, state.Hotspot.y } : null
                });
            }

            List<CursorTransitionDefinition> transitions = new();
            foreach (var transition in Transitions)
            {
                if (!transition.Include)
                {
                    continue;
                }

                transitions.Add(new()
                {
                    from = transition.From,
                    to = transition.To,
                    frames = FramesFor(transition.Clip, directory),
                    frameDurationMs = transition.FrameDurationMs,
                    includesEndpoints = transition.IncludesEndpoints,
                    reversible = transition.Reversible,
                    reverseFrameDurationMs = transition.ReverseFrameDurationMs,
                    hotspot = transition.Hotspot
                });
            }

            return new()
            {
                sizes = Sizes.ToArray(),
                hotspot = new[] { Hotspot.x, Hotspot.y },
                states = states.ToArray(),
                transitions = transitions.ToArray(),
                code = GenerateCode
                    ? new()
                    {
                        className = ClassName,
                        @namespace = Namespace,
                        path = string.IsNullOrEmpty(CodePath) ? string.Empty : Relative(directory, CodePath)
                    }
                    : null
            };
        }

        public void Validate(CursorImportReport report)
        {
            if (!IsProjectPath(OutputPath) ||
                !OutputPath.EndsWith($".{CursorSetImporter.Extension}", StringComparison.OrdinalIgnoreCase))
            {
                report.Error($"The output file must be inside Assets or Packages and end with .{CursorSetImporter.Extension}.");
            }

            CursorSetDefinitionValidator.Validate(ToDefinition(), report);

            CursorScannedClip reference = null;
            foreach (var state in States)
            {
                if (state.Include)
                {
                    CheckClip(state.Clip, $"State '{state.Name}'", ref reference, report);
                }
            }

            foreach (var transition in Transitions)
            {
                if (transition.Include)
                {
                    CheckClip(transition.Clip, $"Transition '{transition.From}' -> '{transition.To}'", ref reference,
                        report);
                }
            }

            if (reference != null && reference.FrameWidth > 0)
            {
                var width = reference.FrameWidth;
                var height = reference.FrameHeight;
                if (width != height)
                {
                    report.Error($"Frames must be square, but the canvas is {width}x{height}.");
                }

                CheckHotspot(Hotspot, "Hotspot", width, height, report);
                foreach (var state in States)
                {
                    if (state.Include && state.HasOwnHotspot)
                    {
                        CheckHotspot(state.Hotspot, $"State '{state.Name}' hotspot", width, height, report);
                    }
                }
            }

            if (!GenerateCode)
            {
                return;
            }

            if (!CursorStateCodeGenerator.IsValidIdentifier(ClassName))
            {
                report.Error($"'{ClassName}' is not a valid class name.");
            }

            if (!string.IsNullOrEmpty(Namespace) && !CursorStateCodeGenerator.IsValidNamespace(Namespace))
            {
                report.Error($"'{Namespace}' is not a valid namespace.");
            }

            if (!IsProjectPath(CodePath) || !CodePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                report.Error("The constants file must be inside Assets or Packages and end with .cs.");
            }
        }

        public void SetSheet(CursorScannedClip clip, CursorSheetLayout layout)
        {
            clip.Sheet = layout;
            foreach (var transition in Transitions)
            {
                var from = States.Find(state => state.Name == transition.From);
                var to = States.Find(state => state.Name == transition.To);
                if (transition.Clip == clip || from?.Clip == clip || to?.Clip == clip)
                {
                    transition.IncludesEndpoints = DetectEndpoints(transition);
                }
            }
        }

        public Vector2Int HotspotOf(CursorBuilderState state)
        {
            return state is { HasOwnHotspot: true } ? state.Hotspot : Hotspot;
        }

        public List<string> IncludedStateNames()
        {
            List<string> names = new();
            foreach (var state in States)
            {
                if (state.Include && !string.IsNullOrEmpty(state.Name) && !names.Contains(state.Name))
                {
                    names.Add(state.Name);
                }
            }

            return names;
        }

        public CursorScannedClip HotspotPreviewClip()
        {
            foreach (var state in States)
            {
                if (state.Include && !state.IsMissing)
                {
                    return state.Clip;
                }
            }

            foreach (var state in States)
            {
                if (!state.IsMissing)
                {
                    return state.Clip;
                }
            }

            return null;
        }

        private bool DetectEndpoints(CursorBuilderTransition transition)
        {
            var from = States.Find(state => state.Name == transition.From);
            var to = States.Find(state => state.Name == transition.To);
            var clip = transition.Clip;
            if (from == null || to == null || from.IsMissing || to.IsMissing || clip.FrameCount < 3)
            {
                return false;
            }

            return SameFrame(clip, 0, from.Clip, 0) && SameFrame(clip, clip.FrameCount - 1, to.Clip, 0);
        }

        private bool HasClip(string key)
        {
            return States.Exists(state => state.Clip.Matches(key)) ||
                   Transitions.Exists(transition => transition.Clip.Matches(key));
        }

        private CursorScannedClip TakeClip(string directory, CursorFramesDefinition frames, string fallbackName)
        {
            var key = ResolveKey(directory, frames);
            CursorScannedClip clip = null;
            var stateIndex = States.FindIndex(state => state.Clip.Matches(key));
            var transitionIndex = Transitions.FindIndex(transition => transition.Clip.Matches(key));
            if (stateIndex >= 0)
            {
                clip = States[stateIndex].Clip;
                States.RemoveAt(stateIndex);
            }
            else if (transitionIndex >= 0)
            {
                clip = Transitions[transitionIndex].Clip;
                Transitions.RemoveAt(transitionIndex);
            }

            if (clip == null)
            {
                return new(fallbackName, key, Array.Empty<string>(), 0, 0, $"'{key}' was not found.");
            }

            clip.Sheet = frames != null && !string.IsNullOrEmpty(frames.sheet)
                ? new(frames.columns, frames.rows, frames.count)
                : default;
            return clip;
        }

        private string OutputDirectory()
        {
            return Path.GetDirectoryName(OutputPath ?? string.Empty)?.Replace('\\', '/') ?? string.Empty;
        }

        private static void CheckHotspot(Vector2Int hotspot, string label, int width, int height,
            CursorImportReport report)
        {
            if (hotspot.x < 0 || hotspot.y < 0 || hotspot.x >= width || hotspot.y >= height)
            {
                report.Error($"{label} ({hotspot.x}, {hotspot.y}) is outside the {width}x{height} canvas.");
            }
        }

        private static void CheckClip(CursorScannedClip clip, string label, ref CursorScannedClip reference,
            CursorImportReport report)
        {
            var problem = clip.Problem ?? clip.SheetProblem();
            if (problem != null)
            {
                report.Error($"{label}: {problem}");
                return;
            }

            if (reference == null)
            {
                reference = clip;
                return;
            }

            if (clip.FrameWidth != reference.FrameWidth || clip.FrameHeight != reference.FrameHeight)
            {
                report.Error(
                    $"{label}: frames are {clip.FrameWidth}x{clip.FrameHeight}, but '{reference.Name}' is {reference.FrameWidth}x{reference.FrameHeight}.");
            }
        }

        private static CursorFramesDefinition FramesFor(CursorScannedClip clip, string directory)
        {
            if (clip.IsSheet)
            {
                return new()
                {
                    sheet = Relative(directory, clip.FramePaths[0]),
                    columns = clip.Sheet.Columns,
                    rows = clip.Sheet.Rows,
                    count = clip.Sheet.Count
                };
            }

            if (clip.IsFolder)
            {
                return new() { folder = Relative(directory, clip.Folder) };
            }

            var files = new string[clip.FramePaths.Count];
            for (var i = 0; i < files.Length; i++)
            {
                files[i] = Relative(directory, clip.FramePaths[i]);
            }

            return new() { files = files };
        }

        private static string ResolveKey(string directory, CursorFramesDefinition frames)
        {
            if (frames == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(frames.folder))
            {
                return Resolve(directory, frames.folder);
            }

            if (frames.files is { Length: > 0 })
            {
                return Resolve(directory, frames.files[0]);
            }

            return Resolve(directory, frames.sheet ?? string.Empty);
        }

        private static string Resolve(string directory, string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(directory, relativePath));
            return Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath).Replace('\\', '/');
        }

        private static string Relative(string directory, string path)
        {
            return Path.GetRelativePath(directory, path).Replace('\\', '/');
        }

        private static bool IsProjectPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.StartsWith("Assets/", StringComparison.Ordinal) ||
                    path.StartsWith("Packages/", StringComparison.Ordinal));
        }

        private static bool SameFrame(CursorScannedClip leftClip, int leftIndex, CursorScannedClip rightClip,
            int rightIndex)
        {
            var left = CursorClipFrameReader.Read(leftClip, leftIndex);
            var right = CursorClipFrameReader.Read(rightClip, rightIndex);
            try
            {
                return left && right &&
                       left.width == right.width &&
                       left.height == right.height &&
                       CursorPixelComparer.CountDifferentPixels(left, right) == 0;
            }
            finally
            {
                if (left)
                {
                    Object.DestroyImmediate(left);
                }

                if (right)
                {
                    Object.DestroyImmediate(right);
                }
            }
        }
    }
}
