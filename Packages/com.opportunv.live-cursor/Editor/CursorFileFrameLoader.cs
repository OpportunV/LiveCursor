using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorFileFrameLoader : ICursorFrameLoader, IDisposable
    {
        public IReadOnlyList<string> Dependencies => _dependencies;

        private readonly string _baseDirectory;
        private readonly List<string> _dependencies = new();
        private readonly List<Texture2D> _created = new();

        public CursorFileFrameLoader(string baseDirectory)
        {
            _baseDirectory = baseDirectory.Replace('\\', '/');
        }

        public bool TryLoad(CursorFramesDefinition frames, string clipLabel, List<Texture2D> output,
            CursorImportReport report)
        {
            if (frames == null)
            {
                report.Error($"{clipLabel}: no frames defined.");
                return false;
            }

            if (!string.IsNullOrEmpty(frames.sheet))
            {
                return TryLoadSheet(frames, clipLabel, output, report);
            }

            if (frames.files is { Length: > 0 })
            {
                return TryLoadFiles(frames.files, clipLabel, output, report);
            }

            if (!string.IsNullOrEmpty(frames.folder))
            {
                return TryLoadFolder(frames.folder, clipLabel, output, report);
            }

            report.Error($"{clipLabel}: frames need a 'folder', 'files' or 'sheet'.");
            return false;
        }

        public void Dispose()
        {
            foreach (var texture in _created)
            {
                Object.DestroyImmediate(texture);
            }

            _created.Clear();
        }

        private bool TryLoadFolder(string folder, string clipLabel, List<Texture2D> output, CursorImportReport report)
        {
            var path = Resolve(folder);
            if (!Directory.Exists(path))
            {
                report.Error($"{clipLabel}: folder '{path}' does not exist.");
                return false;
            }

            var files = Directory.GetFiles(path, "*.png", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                report.Error($"{clipLabel}: folder '{path}' has no PNG files.");
                return false;
            }

            Array.Sort(files, NaturalStringComparer.Instance);
            foreach (var file in files)
            {
                if (!TryLoadImage(file.Replace('\\', '/'), clipLabel, report, out var texture))
                {
                    return false;
                }

                output.Add(texture);
            }

            return true;
        }

        private bool TryLoadFiles(string[] files, string clipLabel, List<Texture2D> output, CursorImportReport report)
        {
            foreach (var file in files)
            {
                if (!TryLoadImage(Resolve(file), clipLabel, report, out var texture))
                {
                    return false;
                }

                output.Add(texture);
            }

            return true;
        }

        private bool TryLoadSheet(CursorFramesDefinition frames, string clipLabel, List<Texture2D> output,
            CursorImportReport report)
        {
            if (frames.columns <= 0 || frames.rows <= 0)
            {
                report.Error($"{clipLabel}: a sheet needs positive 'columns' and 'rows'.");
                return false;
            }

            if (!TryLoadImage(Resolve(frames.sheet), clipLabel, report, out var sheet))
            {
                return false;
            }

            if (sheet.width % frames.columns != 0 || sheet.height % frames.rows != 0)
            {
                report.Error(
                    $"{clipLabel}: sheet {sheet.width}x{sheet.height} does not divide into {frames.columns}x{frames.rows} cells.");
                return false;
            }

            var cellWidth = sheet.width / frames.columns;
            var cellHeight = sheet.height / frames.rows;
            var cells = frames.columns * frames.rows;
            var count = frames.count > 0 ? Mathf.Min(frames.count, cells) : cells;
            for (var i = 0; i < count; i++)
            {
                var column = i % frames.columns;
                var row = i / frames.columns;
                var x = column * cellWidth;
                var y = sheet.height - (row + 1) * cellHeight;
                Texture2D cell = new(cellWidth, cellHeight, TextureFormat.RGBA32, false)
                {
                    name = $"{sheet.name}_{i}"
                };
                cell.SetPixels(sheet.GetPixels(x, y, cellWidth, cellHeight));
                cell.Apply(false, false);
                _created.Add(cell);
                output.Add(cell);
            }

            return true;
        }

        private bool TryLoadImage(string path, string clipLabel, CursorImportReport report, out Texture2D texture)
        {
            texture = null;
            if (!File.Exists(path))
            {
                report.Error($"{clipLabel}: file '{path}' does not exist.");
                return false;
            }

            _dependencies.Add(path);
            texture = new(2, 2, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            _created.Add(texture);
            if (texture.LoadImage(File.ReadAllBytes(path), false))
            {
                return true;
            }

            report.Error($"{clipLabel}: '{path}' is not a readable PNG.");
            return false;
        }

        private string Resolve(string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, relativePath));
            return Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath).Replace('\\', '/');
        }
    }
}
