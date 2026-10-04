using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Opportunv.LiveCursor.Tests.Editor
{
    internal sealed class TestCursorSetBuilder : IDisposable
    {
        private readonly List<Object> _objects = new();
        private readonly List<CursorState> _states = new();
        private readonly List<CursorTransition> _transitions = new();
        private int[] _sizes = { 32 };

        public TestCursorSetBuilder WithSizes(params int[] sizes)
        {
            _sizes = sizes;
            return this;
        }

        public TestCursorSetBuilder AddState(string name, int loopFrames, float frameDuration, float loopDelay = 0f)
        {
            _states.Add(new(name, CreateClip(name, loopFrames, frameDuration), loopDelay));
            return this;
        }

        public TestCursorSetBuilder AddTransition(
            string from,
            string to,
            int frames,
            float frameDuration,
            bool includesEndpoints = true,
            bool reversible = true,
            float reverseFrameDuration = 0f)
        {
            var clip = CreateClip($"{from}>{to}", frames, frameDuration);
            _transitions.Add(new(from, to, clip, includesEndpoints, reversible, reverseFrameDuration));
            return this;
        }

        public CursorSet Build(string name = "TestSet")
        {
            var set = ScriptableObject.CreateInstance<CursorSet>();
            set.name = name;
            set.Initialize(_sizes, _states.ToArray(), _transitions.ToArray());
            _objects.Add(set);
            _states.Clear();
            _transitions.Clear();
            return set;
        }

        public void Dispose()
        {
            foreach (var obj in _objects)
            {
                Object.DestroyImmediate(obj);
            }

            _objects.Clear();
        }

        public static string FrameName(string clip, int frame, int size = 32)
        {
            return $"{clip}:{frame}@{size}";
        }

        private CursorClip CreateClip(string prefix, int frameCount, float frameDuration)
        {
            var frames = new CursorFrame[frameCount];
            for (var f = 0; f < frameCount; f++)
            {
                var textures = new Texture2D[_sizes.Length];
                var hotspots = new Vector2[_sizes.Length];
                for (var s = 0; s < _sizes.Length; s++)
                {
                    hotspots[s] = new(_sizes[s] / 4f, _sizes[s] / 4f);
                    Texture2D texture = new(1, 1)
                    {
                        name = FrameName(prefix, f, _sizes[s])
                    };
                    _objects.Add(texture);
                    textures[s] = texture;
                }

                frames[f] = new(textures, hotspots);
            }

            return new(frames, frameDuration);
        }
    }
}
