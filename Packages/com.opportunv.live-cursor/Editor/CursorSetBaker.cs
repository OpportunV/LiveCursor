using System;
using System.Collections.Generic;
using UnityEngine;
using static Opportunv.LiveCursor.Editor.CursorSetDefinitionValidator;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorSetBaker
    {
        public List<Texture2D> Textures { get; } = new();

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
            set.Initialize(Array.Empty<int>(), Array.Empty<CursorState>(), Array.Empty<CursorTransition>());

            if (!Validate(definition, _report))
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
            var states = new CursorState[definition.states.Length];
            for (var i = 0; i < states.Length; i++)
            {
                var state = definition.states[i];
                var hotspot = StateHotspot(definition, i);
                var clip = BakeClip(state.name, stateFrames[i], state.frameDurationMs, hotspot, hotspot);
                states[i] = new(state.name, clip, state.loopDelayMs / 1000f);
            }

            var bakedTransitions = new CursorTransition[transitions.Length];
            for (var i = 0; i < bakedTransitions.Length; i++)
            {
                var transition = transitions[i];
                var start = HasHotspot(transition.hotspot)
                    ? ToVector(transition.hotspot)
                    : StateHotspot(definition, IndexOfState(definition, transition.from));
                var end = HasHotspot(transition.hotspot)
                    ? ToVector(transition.hotspot)
                    : StateHotspot(definition, IndexOfState(definition, transition.to));
                var clip = BakeClip(
                    $"{transition.from}>{transition.to}",
                    transitionFrames[i],
                    transition.frameDurationMs,
                    start,
                    end);
                bakedTransitions[i] = new(
                    transition.from,
                    transition.to,
                    clip,
                    transition.includesEndpoints,
                    transition.reversible,
                    transition.reverseFrameDurationMs / 1000f);
            }

            set.Initialize(_sizes, states, bakedTransitions);
            return set;
        }

        private List<Texture2D> Load(CursorFramesDefinition frames, string label)
        {
            List<Texture2D> output = new();
            _loader.Load(frames, label, output, _report);
            return output;
        }

        private bool ValidateFrames(
            CursorSetDefinition definition,
            List<Texture2D>[] stateFrames,
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
                        $"{TransitionLabel(transition)} has no in-between frames once its endpoints are skipped; it " +
                        "will switch instantly.");
                }
            }

            CheckHotspot(definition.hotspot, "Hotspot");
            foreach (var state in definition.states)
            {
                if (HasHotspot(state.hotspot))
                {
                    CheckHotspot(state.hotspot, $"{StateLabel(state)}: hotspot");
                }
            }

            foreach (var transition in transitions)
            {
                if (HasHotspot(transition.hotspot))
                {
                    CheckHotspot(transition.hotspot, $"{TransitionLabel(transition)}: hotspot");
                }
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

        private void CheckHotspot(int[] hotspot, string label)
        {
            if (hotspot[0] < 0 || hotspot[1] < 0 || hotspot[0] >= _canvasWidth || hotspot[1] >= _canvasHeight)
            {
                _report.Error(
                    $"{label} ({hotspot[0]}, {hotspot[1]}) is outside the {_canvasWidth}x{_canvasHeight} canvas.");
            }
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
                    $"{label}: frame {i} ('{frame.name}') is {frame.width}x{frame.height}, " +
                    $"but every frame must match the {_canvasWidth}x{_canvasHeight} canvas.");
                return;
            }
        }

        private void CheckEndpoints(
            CursorSetDefinition definition,
            List<Texture2D>[] stateFrames,
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
                var startDifference = CursorPixelComparer.CountDifferentPixels(frames[0], from);
                if (startDifference > 0)
                {
                    _report.Warning(
                        $"{TransitionLabel(transition)}: first frame differs from '{transition.from}' frame 0 in " +
                        $"{startDifference} pixels; the cursor will jump when the transition starts.");
                }

                var endDifference = CursorPixelComparer.CountDifferentPixels(frames[^1], to);
                if (endDifference > 0)
                {
                    _report.Warning(
                        $"{TransitionLabel(transition)}: last frame differs from '{transition.to}' frame 0 in " +
                        $"{endDifference} pixels; the cursor will jump when the transition ends.");
                }
            }
        }

        private CursorClip BakeClip(
            string label,
            List<Texture2D> masters,
            float frameDurationMs,
            Vector2 startHotspot,
            Vector2 endHotspot)
        {
            var frames = new CursorFrame[masters.Count];
            for (var f = 0; f < masters.Count; f++)
            {
                var pixels = masters[f].GetPixels32();
                var progress = masters.Count > 1 ? f / (float)(masters.Count - 1) : 0f;
                var hotspot = Vector2.Lerp(startHotspot, endHotspot, progress);
                var textures = new Texture2D[_sizes.Length];
                var hotspots = new Vector2[_sizes.Length];
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
                    hotspots[s] = ScaleHotspot(hotspot, size);
                }

                frames[f] = new(textures, hotspots);
            }

            return new(frames, frameDurationMs / 1000f);
        }

        private Vector2 ScaleHotspot(Vector2 hotspot, int size)
        {
            var x = Mathf.Clamp(Mathf.Round(hotspot.x * size / _canvasWidth), 0f, size - 1);
            var y = Mathf.Clamp(Mathf.Round(hotspot.y * size / _canvasHeight), 0f, size - 1);
            return new(x, y);
        }

        private static Vector2 StateHotspot(CursorSetDefinition definition, int stateIndex)
        {
            var hotspot = definition.states[stateIndex].hotspot;
            return ToVector(HasHotspot(hotspot) ? hotspot : definition.hotspot);
        }

        private static Vector2 ToVector(int[] hotspot)
        {
            return new(hotspot[0], hotspot[1]);
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
    }
}
