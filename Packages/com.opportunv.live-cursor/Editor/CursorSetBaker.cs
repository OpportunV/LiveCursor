using System;
using System.Collections.Generic;
using UnityEngine;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorSetBaker
    {
        public List<Texture2D> Textures { get; } = new();

        private const int PixelTolerance = 2;

        private readonly ICursorFrameLoader _loader;
        private readonly CursorImportReport _report;
        private int[] _sizes;
        private int _canvasWidth;
        private int _canvasHeight;

        public CursorSetBaker(ICursorFrameLoader loader, CursorImportReport report)
        {
            _loader = loader;
            _report = report;
        }

        public CursorSet Bake(CursorSetDefinition definition, string setName)
        {
            var set = ScriptableObject.CreateInstance<CursorSet>();
            set.name = setName;
            set.Initialize(Array.Empty<int>(), Array.Empty<Vector2>(), Array.Empty<CursorState>(),
                Array.Empty<CursorTransition>());

            if (!ValidateDefinition(definition))
            {
                return set;
            }

            var transitions = definition.transitions ?? Array.Empty<CursorTransitionDefinition>();
            var stateFrames = new List<Texture2D>[definition.states.Length];
            for (var i = 0; i < stateFrames.Length; i++)
            {
                stateFrames[i] = Load(definition.states[i].frames, StateLabel(definition.states[i]));
            }

            var transitionFrames = new List<Texture2D>[transitions.Length];
            for (var i = 0; i < transitionFrames.Length; i++)
            {
                transitionFrames[i] = Load(transitions[i].frames, TransitionLabel(transitions[i]));
            }

            if (_report.HasErrors || !ValidateFrames(definition, stateFrames, transitionFrames))
            {
                return set;
            }

            CheckEndpoints(definition, stateFrames, transitionFrames);

            _sizes = (int[])definition.sizes.Clone();
            var hotspots = new Vector2[_sizes.Length];
            for (var i = 0; i < _sizes.Length; i++)
            {
                hotspots[i] = ScaleHotspot(definition.hotspot, _sizes[i]);
            }

            var states = new CursorState[definition.states.Length];
            for (var i = 0; i < states.Length; i++)
            {
                var state = definition.states[i];
                var clip = BakeClip(state.name, stateFrames[i], state.frameDurationMs);
                states[i] = new(state.name, clip, state.loopDelayMs / 1000f);
            }

            var bakedTransitions = new CursorTransition[transitions.Length];
            for (var i = 0; i < bakedTransitions.Length; i++)
            {
                var transition = transitions[i];
                var clip = BakeClip($"{transition.from}>{transition.to}", transitionFrames[i],
                    transition.frameDurationMs);
                bakedTransitions[i] = new(transition.from, transition.to, clip, transition.includesEndpoints,
                    transition.reversible, transition.reverseFrameDurationMs / 1000f);
            }

            set.Initialize(_sizes, hotspots, states, bakedTransitions);
            return set;
        }

        private bool ValidateDefinition(CursorSetDefinition definition)
        {
            if (definition == null)
            {
                _report.Error("The cursor set definition is empty or not valid JSON.");
                return false;
            }

            if (definition.sizes is not { Length: > 0 })
            {
                _report.Error("'sizes' must list at least one cursor size, for example [32, 48, 64].");
            }
            else
            {
                HashSet<int> seen = new();
                foreach (var size in definition.sizes)
                {
                    if (size <= 0)
                    {
                        _report.Error($"Size {size} must be positive.");
                    }
                    else if (!seen.Add(size))
                    {
                        _report.Error($"Size {size} is listed twice.");
                    }
                }
            }

            if (definition.hotspot is not { Length: 2 })
            {
                _report.Error("'hotspot' must be [x, y] in source pixels from the top-left corner.");
            }

            if (definition.states is not { Length: > 0 })
            {
                _report.Error("'states' must define at least one state.");
                return false;
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var state in definition.states)
            {
                if (string.IsNullOrEmpty(state.name))
                {
                    _report.Error("Every state needs a 'name'.");
                    continue;
                }

                if (!names.Add(state.name))
                {
                    _report.Error($"State '{state.name}' is defined twice.");
                }

                if (state.frameDurationMs <= 0f)
                {
                    _report.Error($"{StateLabel(state)}: 'frameDurationMs' must be positive.");
                }

                if (state.loopDelayMs < 0f)
                {
                    _report.Error($"{StateLabel(state)}: 'loopDelayMs' cannot be negative.");
                }
            }

            HashSet<string> pairs = new(StringComparer.Ordinal);
            foreach (var transition in definition.transitions ?? Array.Empty<CursorTransitionDefinition>())
            {
                var label = TransitionLabel(transition);
                if (!names.Contains(transition.from ?? string.Empty))
                {
                    _report.Error($"{label}: unknown state '{transition.from}'.");
                }

                if (!names.Contains(transition.to ?? string.Empty))
                {
                    _report.Error($"{label}: unknown state '{transition.to}'.");
                }

                if (transition.from == transition.to)
                {
                    _report.Error($"{label}: 'from' and 'to' must differ.");
                }

                if (!pairs.Add($"{transition.from}>{transition.to}"))
                {
                    _report.Error($"{label} is defined twice.");
                }

                if (transition.frameDurationMs <= 0f)
                {
                    _report.Error($"{label}: 'frameDurationMs' must be positive.");
                }

                if (transition.reverseFrameDurationMs < 0f)
                {
                    _report.Error($"{label}: 'reverseFrameDurationMs' cannot be negative.");
                }
            }

            return !_report.HasErrors;
        }

        private List<Texture2D> Load(CursorFramesDefinition frames, string label)
        {
            List<Texture2D> output = new();
            _loader.TryLoad(frames, label, output, _report);
            return output;
        }

        private bool ValidateFrames(CursorSetDefinition definition, List<Texture2D>[] stateFrames,
            List<Texture2D>[] transitionFrames)
        {
            var reference = stateFrames[0][0];
            _canvasWidth = reference.width;
            _canvasHeight = reference.height;

            if (_canvasWidth != _canvasHeight)
            {
                _report.Error($"Frames must be square, but the canvas is {_canvasWidth}x{_canvasHeight}.");
            }

            for (var i = 0; i < stateFrames.Length; i++)
            {
                CheckCanvas(StateLabel(definition.states[i]), stateFrames[i]);
            }

            var transitions = definition.transitions ?? Array.Empty<CursorTransitionDefinition>();
            for (var i = 0; i < transitionFrames.Length; i++)
            {
                var transition = transitions[i];
                CheckCanvas(TransitionLabel(transition), transitionFrames[i]);
                if (transition.includesEndpoints && transitionFrames[i].Count < 3)
                {
                    _report.Warning(
                        $"{TransitionLabel(transition)} has no in-between frames once its endpoints are skipped; it will switch instantly.");
                }
            }

            var hotspot = definition.hotspot;
            if (hotspot[0] < 0 || hotspot[1] < 0 || hotspot[0] >= _canvasWidth || hotspot[1] >= _canvasHeight)
            {
                _report.Error(
                    $"Hotspot ({hotspot[0]}, {hotspot[1]}) is outside the {_canvasWidth}x{_canvasHeight} canvas.");
            }

            foreach (var size in definition.sizes)
            {
                if (size > _canvasWidth)
                {
                    _report.Warning(
                        $"Size {size} is larger than the {_canvasWidth}px source frames and will be upscaled.");
                }
            }

            return !_report.HasErrors;
        }

        private void CheckCanvas(string label, List<Texture2D> frames)
        {
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (frame.width == _canvasWidth && frame.height == _canvasHeight)
                {
                    continue;
                }

                _report.Error(
                    $"{label}: frame {i} ('{frame.name}') is {frame.width}x{frame.height}, but every frame must match the {_canvasWidth}x{_canvasHeight} canvas.");
                return;
            }
        }

        private void CheckEndpoints(CursorSetDefinition definition, List<Texture2D>[] stateFrames,
            List<Texture2D>[] transitionFrames)
        {
            var transitions = definition.transitions ?? Array.Empty<CursorTransitionDefinition>();
            for (var i = 0; i < transitions.Length; i++)
            {
                var transition = transitions[i];
                var frames = transitionFrames[i];
                if (!transition.includesEndpoints || frames.Count == 0)
                {
                    continue;
                }

                var from = stateFrames[IndexOfState(definition, transition.from)][0];
                var to = stateFrames[IndexOfState(definition, transition.to)][0];
                var startDifference = CountDifferentPixels(frames[0], from);
                if (startDifference > 0)
                {
                    _report.Warning(
                        $"{TransitionLabel(transition)}: first frame differs from '{transition.from}' frame 0 in {startDifference} pixels; the cursor will jump when the transition starts.");
                }

                var endDifference = CountDifferentPixels(frames[^1], to);
                if (endDifference > 0)
                {
                    _report.Warning(
                        $"{TransitionLabel(transition)}: last frame differs from '{transition.to}' frame 0 in {endDifference} pixels; the cursor will jump when the transition ends.");
                }
            }
        }

        private CursorClip BakeClip(string label, List<Texture2D> masters, float frameDurationMs)
        {
            var frames = new CursorFrame[masters.Count];
            for (var f = 0; f < masters.Count; f++)
            {
                var pixels = masters[f].GetPixels32();
                var textures = new Texture2D[_sizes.Length];
                for (var s = 0; s < _sizes.Length; s++)
                {
                    var size = _sizes[s];
                    var scaled = CursorFrameScaler.Scale(pixels, _canvasWidth, _canvasHeight, size, size);
                    Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
                    {
                        name = $"{label} {f:000} @{size}",
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        alphaIsTransparency = true,
                        hideFlags = HideFlags.HideInHierarchy
                    };
                    texture.SetPixels32(scaled);
                    texture.Apply(false, false);
                    Textures.Add(texture);
                    textures[s] = texture;
                }

                frames[f] = new(textures);
            }

            return new(frames, frameDurationMs / 1000f);
        }

        private Vector2 ScaleHotspot(int[] hotspot, int size)
        {
            var x = Mathf.Clamp(Mathf.Round(hotspot[0] * (float)size / _canvasWidth), 0f, size - 1);
            var y = Mathf.Clamp(Mathf.Round(hotspot[1] * (float)size / _canvasHeight), 0f, size - 1);
            return new(x, y);
        }

        private static int CountDifferentPixels(Texture2D left, Texture2D right)
        {
            var a = left.GetPixels32();
            var b = right.GetPixels32();
            var count = 0;
            for (var i = 0; i < a.Length; i++)
            {
                var p = a[i];
                var q = b[i];
                if (p.a == 0 && q.a == 0)
                {
                    continue;
                }

                if (Math.Abs(p.r - q.r) > PixelTolerance || Math.Abs(p.g - q.g) > PixelTolerance ||
                    Math.Abs(p.b - q.b) > PixelTolerance || Math.Abs(p.a - q.a) > PixelTolerance)
                {
                    count++;
                }
            }

            return count;
        }

        private static int IndexOfState(CursorSetDefinition definition, string name)
        {
            for (var i = 0; i < definition.states.Length; i++)
            {
                if (definition.states[i].name == name)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string StateLabel(CursorStateDefinition state)
        {
            return $"State '{state.name}'";
        }

        private static string TransitionLabel(CursorTransitionDefinition transition)
        {
            return $"Transition '{transition.from}' -> '{transition.to}'";
        }
    }
}
