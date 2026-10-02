using System;
using System.Collections.Generic;
using System.IO;

namespace Opportunv.LiveCursor.Editor
{
    internal static class CursorFolderScanner
    {
        private static readonly byte[] _pngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static List<CursorScannedClip> Scan(string rootFolder)
        {
            List<CursorScannedClip> clips = new();
            rootFolder = rootFolder.Replace('\\', '/').TrimEnd('/');
            if (!Directory.Exists(rootFolder))
            {
                return clips;
            }

            ScanFolder(rootFolder, rootFolder, clips);
            MakeNamesUnique(clips);
            return clips;
        }

        public static bool TryReadPngSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            var header = new byte[24];
            using (var stream = File.OpenRead(path))
            {
                if (stream.Read(header, 0, header.Length) != header.Length)
                {
                    return false;
                }
            }

            for (var i = 0; i < _pngSignature.Length; i++)
            {
                if (header[i] != _pngSignature[i])
                {
                    return false;
                }
            }

            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return true;
        }

        private static void ScanFolder(string folder, string rootFolder, List<CursorScannedClip> clips)
        {
            var files = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
            Array.Sort(files, NaturalStringComparer.Instance);
            for (var i = 0; i < files.Length; i++)
            {
                files[i] = files[i].Replace('\\', '/');
            }

            if (files.Length > 0)
            {
                var folderName = Path.GetFileName(folder);
                if (files.Length == 1 && folder != rootFolder)
                {
                    clips.Add(CreateClip(folderName, folder, files));
                }
                else if (IsSequence(files))
                {
                    clips.Add(CreateClip(folderName, folder, files));
                }
                else
                {
                    foreach (var file in files)
                    {
                        clips.Add(CreateClip(Path.GetFileNameWithoutExtension(file), null, new[] { file }));
                    }
                }
            }

            var folders = Directory.GetDirectories(folder);
            Array.Sort(folders, NaturalStringComparer.Instance);
            foreach (var child in folders)
            {
                ScanFolder(child.Replace('\\', '/'), rootFolder, clips);
            }
        }

        private static bool IsSequence(string[] files)
        {
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length == 0 || !char.IsDigit(name[^1]))
                {
                    return false;
                }
            }

            return true;
        }

        private static CursorScannedClip CreateClip(string name, string folder, string[] files)
        {
            var width = 0;
            var height = 0;
            string problem = null;
            for (var i = 0; i < files.Length; i++)
            {
                if (!TryReadPngSize(files[i], out var frameWidth, out var frameHeight))
                {
                    problem ??= $"'{files[i]}' is not a valid PNG.";
                    continue;
                }

                if (i == 0)
                {
                    width = frameWidth;
                    height = frameHeight;
                }
                else if (frameWidth != width || frameHeight != height)
                {
                    problem ??=
                        $"'{Path.GetFileName(files[i])}' is {frameWidth}x{frameHeight}, but the first frame is {width}x{height}.";
                }
            }

            return new(name, folder, files, width, height, problem);
        }

        private static void MakeNamesUnique(List<CursorScannedClip> clips)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (seen.Add(clip.Name))
                {
                    continue;
                }

                var parent = Path.GetFileName(Path.GetDirectoryName(clip.Key) ?? string.Empty);
                var name = $"{parent}{clip.Name}";
                var suffix = 2;
                while (!seen.Add(name))
                {
                    name = $"{parent}{clip.Name}{suffix++}";
                }

                clips[i] = new(name, clip.Folder, clip.FramePaths, clip.Width, clip.Height, clip.Problem);
            }
        }
    }
}
