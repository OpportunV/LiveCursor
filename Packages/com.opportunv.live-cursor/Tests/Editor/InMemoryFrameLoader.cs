using System;
using System.Collections.Generic;
using Opportunv.LiveCursor.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Opportunv.LiveCursor.Tests.Editor
{
    internal sealed class InMemoryFrameLoader : ICursorFrameLoader, IDisposable
    {
        private readonly Dictionary<string, List<Texture2D>> _clips = new();
        private readonly List<Texture2D> _created = new();

        public void Add(string folder, params Texture2D[] frames)
        {
            _clips[folder] = new(frames);
        }

        public Texture2D CreateFrame(int size, Color32 color, string name = "Frame")
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = name
            };
            var pixels = new Color32[size * size];
            Array.Fill(pixels, color);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _created.Add(texture);
            return texture;
        }

        public void Load(
            CursorFramesDefinition frames,
            string clipLabel,
            List<Texture2D> output,
            CursorImportReport report)
        {
            if (frames?.folder != null && _clips.TryGetValue(frames.folder, out var clip))
            {
                output.AddRange(clip);
                return;
            }

            report.Error($"{clipLabel}: no frames.");
        }

        public void Dispose()
        {
            foreach (var texture in _created)
            {
                Object.DestroyImmediate(texture);
            }

            _created.Clear();
        }
    }
}
