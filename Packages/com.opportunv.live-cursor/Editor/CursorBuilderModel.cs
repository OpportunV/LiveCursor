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
        public const string DefaultClassName = "CursorStates";

        public string SourceFolder { get; private set; }

        public string OutputPath { get; set; }

        public List<int> Sizes { get; } = new() { 32, 48, 64 };

        public Vector2Int Hotspot { get; set; }

        public List<CursorBuilderState> States { get; } = new();

        public List<CursorBuilderTransition> Transitions { get; } = new();

        public bool GenerateCode { get; set; }

        public string ClassName { get; set; } = DefaultClassName;

        public string Namespace { get; set; } = string.Empty;

        public string CodePath { get; set; }

        public static CursorBuilderModel FromFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            var folderName = Path.GetFileName(folder);
            CursorBuilderModel model = new()
            {
                SourceFolder = folder,
                OutputPath = $"{folder}/{folderName}.{CursorSetImporter.Extension}",
                CodePath = $"{folder}/{DefaultClassName}.cs"
            };

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
            var definition = JsonUtility.FromJson<CursorSetDefinition>(File.ReadAllText(definitionPath));
            if (definition != null && model.MatchesAnyClip(definition))
            {
                model.ApplyDefinition(definition);
            }

            return model;
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
                ClassName = string.IsNullOrEmpty(definition.code.className) ? DefaultClassName : definition.code.className;
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
                var clip = TakeClip(ResolveKey(directory, definitionState.frames), definitionState.name);
                CursorBuilderState state = new(clip)
                {
                    Name = definitionState.name,
                    FrameDurationMs = definitionState.frameDurationMs,
                    LoopDelayMs = definitionState.loopDelayMs
                };
                states.Add(state);
            }

            States.InsertRange(0, states);

            List<CursorBuilderTransition> transitions = new();
            foreach (var definitionTransition in definition.transitions ?? Array.Empty<CursorTransitionDefinition>())
            {
                var name = $"{definitionTransition.from}To{definitionTransition.to}";
                var clip = TakeClip(ResolveKey(directory, definitionTransition.frames), name);
                CursorBuilderTransition transition = new(clip, definitionTransition.from, definitionTransition.to)
                {
                    FrameDurationMs = definitionTransition.frameDurationMs,
                    Reversible = definitionTransition.reversible,
                    ReverseFrameDurationMs = definitionTransition.reverseFrameDurationMs,
                    IncludesEndpoints = definitionTransition.includesEndpoints
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
                    loopDelayMs = state.LoopDelayMs
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
                    reverseFrameDurationMs = transition.ReverseFrameDurationMs
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

            if (reference != null && reference.Width > 0)
            {
                if (reference.Width != reference.Height)
                {
                    report.Error($"Frames must be square, but the canvas is {reference.Width}x{reference.Height}.");
                }

                if (Hotspot.x < 0 || Hotspot.y < 0 || Hotspot.x >= reference.Width || Hotspot.y >= reference.Height)
                {
                    report.Error($"Hotspot ({Hotspot.x}, {Hotspot.y}) is outside the {reference.Width}x{reference.Height} canvas.");
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
            var frames = transition.Clip.FramePaths;
            if (from == null || to == null || from.IsMissing || to.IsMissing || frames.Count < 3)
            {
                return false;
            }

            return SameImage(frames[0], from.Clip.FramePaths[0]) && SameImage(frames[^1], to.Clip.FramePaths[0]);
        }

        private bool HasClip(string key)
        {
            return States.Exists(state => state.Clip.Key == key) ||
                   Transitions.Exists(transition => transition.Clip.Key == key);
        }

        private CursorScannedClip TakeClip(string key, string fallbackName)
        {
            var stateIndex = States.FindIndex(state => state.Clip.Key == key);
            if (stateIndex >= 0)
            {
                var clip = States[stateIndex].Clip;
                States.RemoveAt(stateIndex);
                return clip;
            }

            var transitionIndex = Transitions.FindIndex(transition => transition.Clip.Key == key);
            if (transitionIndex >= 0)
            {
                var clip = Transitions[transitionIndex].Clip;
                Transitions.RemoveAt(transitionIndex);
                return clip;
            }

            return new(fallbackName, key, Array.Empty<string>(), 0, 0, $"'{key}' was not found.");
        }

        private string OutputDirectory()
        {
            return Path.GetDirectoryName(OutputPath ?? string.Empty)?.Replace('\\', '/') ?? string.Empty;
        }

        private static void CheckClip(CursorScannedClip clip, string label, ref CursorScannedClip reference,
            CursorImportReport report)
        {
            if (clip.Problem != null)
            {
                report.Error($"{label}: {clip.Problem}");
                return;
            }

            if (reference == null)
            {
                reference = clip;
                return;
            }

            if (clip.Width != reference.Width || clip.Height != reference.Height)
            {
                report.Error(
                    $"{label}: frames are {clip.Width}x{clip.Height}, but '{reference.Name}' is {reference.Width}x{reference.Height}.");
            }
        }

        private static CursorFramesDefinition FramesFor(CursorScannedClip clip, string directory)
        {
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

        private static bool SameImage(string leftPath, string rightPath)
        {
            Texture2D left = new(2, 2, TextureFormat.RGBA32, false);
            Texture2D right = new(2, 2, TextureFormat.RGBA32, false);
            try
            {
                return left.LoadImage(File.ReadAllBytes(leftPath)) &&
                       right.LoadImage(File.ReadAllBytes(rightPath)) &&
                       left.width == right.width &&
                       left.height == right.height &&
                       CursorPixelComparer.CountDifferentPixels(left, right) == 0;
            }
            finally
            {
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
            }
        }
    }
}
